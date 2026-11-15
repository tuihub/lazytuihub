import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {mkdtemp, mkdir, readFile, rename, rm, writeFile} from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import {test} from 'node:test';
import {fileURLToPath} from 'node:url';
import {parseDocument} from 'yaml';

const script = fileURLToPath(new URL('../scripts/check-note-history.mjs', import.meta.url));
const first = '.agents/notes/2026-11-15-decision.en.md';
const second = '.agents/notes/2026-11-15-next.en.md';
const statuses = ['proposed', 'implemented', 'rejected', 'superseded', 'archived'];
const schema = parseDocument(await readFile(new URL('../schemas/note-metadata.yaml', import.meta.url), 'utf8')).toJSON();
const category = schema.properties.category.enum[0];
const references = (...names) => `superseded_by: ${JSON.stringify(names)}\n`;
function note(status, extra = '', body = 'Original rationale.') {
    const defaultReferences = ['superseded', 'archived'].includes(status) && !extra.includes('superseded_by:')
        ? references(path.basename(second)) : '';
    return `---\nstatus: ${status}\ncategory: ${category}\n${defaultReferences}${extra}---\n\n# Decision\n\n${body}\n`;
}

function git(directory, ...args) {
    const result = spawnSync('git', args, {cwd: directory, encoding: 'utf8'});
    assert.ifError(result.error);
    assert.equal(result.status, 0, result.stderr);
    return result.stdout.trim();
}

async function write(directory, name, content) {
    const destination = path.join(directory, name);
    await mkdir(path.dirname(destination), {recursive: true});
    await writeFile(destination, content);
}

async function fixture(t, files = {}, commit = true) {
    const directory = await mkdtemp(path.join(os.tmpdir(), 'note history-'));
    t.after(() => rm(directory, {recursive: true, force: true}));
    git(directory, 'init', '--quiet');
    git(directory, 'config', 'user.name', 'Note history test');
    git(directory, 'config', 'user.email', 'note-history@example.invalid');
    git(directory, 'config', 'commit.gpgsign', 'false');
    git(directory, 'config', 'core.autocrlf', 'false');
    for (const [name, content] of Object.entries(files)) await write(directory, name, content);
    if (commit) save(directory);
    return directory;
}

function save(directory) {
    git(directory, 'add', '--all');
    git(directory, 'commit', '--quiet', '--allow-empty', '-m', 'Fixture snapshot');
    return git(directory, 'rev-parse', 'HEAD');
}

function run(directory, ...args) {
    const result = spawnSync(process.execPath, [script, ...args], {cwd: directory, encoding: 'utf8'});
    assert.ifError(result.error);
    return result;
}

function passes(directory, ...args) {
    const result = run(directory, ...args);
    assert.equal(result.status, 0, result.stderr);
    return result;
}

function fails(directory, pattern, ...args) {
    const result = run(directory, ...args);
    assert.equal(result.status, 1, result.stdout);
    assert.match(result.stderr, pattern);
    return result;
}

test('an unborn repository and newly seen notes need no proposed commit', async (t) => {
    const directory = await fixture(t, {}, false);
    for (const status of statuses) {
        await write(directory, `.agents/notes/2026-11-15-${status}.en.md`, note(status));
    }
    assert.match(passes(directory).stdout, /baseline is empty/);
    save(directory);
    passes(directory);
    for (const status of statuses) {
        await write(directory, `.agents/notes/2026-11-16-${status}.en.md`, note(status));
    }
    passes(directory);
});

test('proposed notes can be edited and reach any subsequent state between snapshots', async (t) => {
    const directory = await fixture(t, {[first]: note('proposed')});
    for (const status of statuses) {
        await write(directory, first, note(status, 'custom: changed\n', 'Revised rationale.'));
        passes(directory);
    }
});

test('implemented notes stay byte-frozen while retaining implemented status', async (t) => {
    const directory = await fixture(t, {[first]: note('implemented')});
    passes(directory);
    for (const changed of [note('implemented', '', 'Changed rationale.'), note('implemented', 'custom: value\n'), note('implemented').replace(`category: ${category}`, `category: "${category}"`), note('implemented', references(path.basename(second)))]) {
        await write(directory, first, changed);
        fails(directory, /implemented note is frozen/);
    }
});

test('partial or complete coverage preserves body and other metadata but allows YAML reformatting', async (t) => {
    const directory = await fixture(t, {[first]: note('implemented', 'custom: {enabled: true, tags: [one, two]}\n')});
    for (const status of ['superseded', 'archived']) {
        const replacement = `---\n# Metadata may be reordered during coverage changes.\ncustom:\n  tags: [one, two]\n  enabled: true\n${references(path.basename(second))}category: "${category}"\nstatus: ${status}\n---\n\n# Decision\n\nOriginal rationale.\n`;
        await write(directory, first, replacement);
        passes(directory);
        for (const changed of [replacement.replace('Original rationale.', 'Changed rationale.'), replacement.replace('enabled: true', 'enabled: false'), replacement.replace(`category: "${category}"`, 'category: changed-category')]) {
            await write(directory, first, changed);
            fails(directory, /may change only status and superseded_by/);
        }
    }
});

test('implemented notes cannot return to proposed or become rejected', async (t) => {
    const directory = await fixture(t, {[first]: note('implemented')});
    for (const status of ['proposed', 'rejected']) {
        await write(directory, first, note(status));
        fails(directory, new RegExp(`from implemented to ${status}`));
    }
});

test('rejected and archived notes reject every content or status change', async (t) => {
    for (const status of ['rejected', 'archived']) {
        const directory = await fixture(t, {[first]: note(status)});
        passes(directory);
        for (const changed of [note(status, '', 'Changed.'), note(status).replace(`category: ${category}`, `category: "${category}"`), ...statuses.filter((other) => other !== status).map((other) => note(other))]) {
            await write(directory, first, changed);
            fails(directory, new RegExp(`${status} note is frozen`));
        }
    }
});

test('superseded notes may maintain coverage references and become archived', async (t) => {
    const original = [path.basename(second), '2026-11-15-third.zh.md'];
    const directory = await fixture(t, {[first]: note('superseded', references(...original))});
    passes(directory);
    const lists = [original, [...original, '2026-11-15-fourth.en.md'], [original[1]], ['2026-11-15-other.en.md'], [...original].reverse()];
    for (const status of ['superseded', 'archived']) {
        for (const names of lists) {
            await write(directory, first, note(status, references(...names)));
            passes(directory);
        }
    }
    for (const changed of [note('superseded', references(...original), 'Changed body.'), note('archived', references(...original)).replace(`category: ${category}`, 'category: changed-category')]) {
        await write(directory, first, changed);
        fails(directory, /may change only status and superseded_by/);
    }
    for (const status of ['proposed', 'implemented', 'rejected']) {
        await write(directory, first, note(status));
        fails(directory, new RegExp(`from superseded to ${status}`));
    }
});

test('archival freezes the reference list selected at the transition, including its order', async (t) => {
    const original = [path.basename(second), '2026-11-15-third.zh.md'];
    const directory = await fixture(t, {[first]: note('superseded', references(path.basename(second)))});
    await write(directory, first, note('archived', references(...original)));
    passes(directory);
    save(directory);
    passes(directory);
    for (const names of [[...original, '2026-11-15-fourth.en.md'], [original[0]], [...original].reverse()]) {
        await write(directory, first, note('archived', references(...names)));
        fails(directory, /archived note is frozen/);
    }
});

test('deleting proposed notes is allowed while frozen deletion and rename fail', async (t) => {
    for (const status of statuses) {
        const directory = await fixture(t, {[first]: note(status)});
        await rm(path.join(directory, first));
        if (status === 'proposed') passes(directory);
        else fails(directory, new RegExp(`Cannot delete a ${status} note`));
    }
    const directory = await fixture(t, {[first]: note('implemented')});
    await rename(path.join(directory, first), path.join(directory, second));
    fails(directory, /Cannot delete a implemented note/);
});

test('actual working-tree files determine the result, including recreated untracked paths', async (t) => {
    const directory = await fixture(t, {[first]: note('implemented')});
    await write(directory, first, note('implemented', '', 'Staged illegal change.'));
    git(directory, 'add', '--all');
    await write(directory, first, note('implemented'));
    passes(directory);
    git(directory, 'rm', '--force', '--', first);
    await write(directory, first, note('implemented'));
    passes(directory);
    await write(directory, first, note('implemented', '', 'Untracked illegal change.'));
    fails(directory, /implemented note is frozen/);
});

test('an explicit earlier commit supplies the baseline without replaying intermediate commits', async (t) => {
    const directory = await fixture(t, {[first]: note('proposed')});
    const proposed = git(directory, 'rev-parse', 'HEAD');
    await write(directory, first, note('implemented', '', 'Revised while proposed.'));
    save(directory);
    await write(directory, first, note('archived', references(path.basename(second)), 'A later revision.'));
    passes(directory, '--base', proposed);
    fails(directory, /may change only status and superseded_by/);
    assert.match(passes(directory, '--help').stdout, /not each uncommitted operation or the full Git history/);
});

test('invalid bases and non-repositories fail clearly', async (t) => {
    const directory = await fixture(t, {}, false);
    fails(directory, /Cannot resolve --base/, '--base', 'missing-commit');
    fails(directory, /Cannot resolve --base/, '--base=--all');
    const outside = await mkdtemp(path.join(os.tmpdir(), 'note-history-outside-'));
    t.after(() => rm(outside, {recursive: true, force: true}));
    fails(outside, /Git rev-parse failed/);
    const env = Object.fromEntries(Object.entries(process.env).filter(([key]) => key.toLowerCase() !== 'path'));
    env.PATH = outside;
    const missingGit = spawnSync(process.execPath, [script], {cwd: directory, env, encoding: 'utf8'});
    assert.ifError(missingGit.error);
    assert.equal(missingGit.status, 1);
    assert.match(missingGit.stderr, /Cannot run Git/);
});

test('all frozen-note violations are reported together', async (t) => {
    const directory = await fixture(t, {[first]: note('implemented'), [second]: note('rejected')});
    await write(directory, first, note('implemented', '', 'Changed.'));
    await rm(path.join(directory, second));
    const result = fails(directory, /implemented note is frozen/);
    assert.match(result.stderr, /Cannot delete a rejected note/);
});

test('Git checkout EOL conversion does not create a frozen-note false positive', async (t) => {
    const directory = await fixture(t, {[first]: note('implemented')});
    await write(directory, first, note('implemented').replaceAll('\n', '\r\n'));
    fails(directory, /implemented note is frozen/);
    git(directory, 'config', 'core.autocrlf', 'true');
    await rm(path.join(directory, first));
    git(directory, 'checkout', 'HEAD', '--', first);
    const checkedOut = await readFile(path.join(directory, first), 'utf8');
    assert.ok(checkedOut.includes('\r\n'));
    passes(directory);
    await write(directory, first, checkedOut.replace('Original rationale.', 'A real edit.'));
    fails(directory, /implemented note is frozen/);
});
