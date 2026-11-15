import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {mkdtemp, mkdir, rm, writeFile} from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import {test} from 'node:test';
import {fileURLToPath} from 'node:url';
import {parseFrontmatter} from '../scripts/frontmatter.mjs';

const script = fileURLToPath(new URL('../scripts/filter-frontmatter.mjs', import.meta.url));

async function fixture(t, files = {}) {
    const directory = await mkdtemp(path.join(os.tmpdir(), 'frontmatter-filter-'));
    t.after(() => rm(directory, {recursive: true, force: true}));
    for (const [name, content] of Object.entries(files)) {
        const file = path.join(directory, name);
        await mkdir(path.dirname(file), {recursive: true});
        await writeFile(file, content);
    }
    return directory;
}

function run(directory, ...args) {
    const result = spawnSync(process.execPath, [script, ...args, ...(directory ? [directory] : [])], {encoding: 'utf8'});
    assert.ifError(result.error);
    return result;
}

function records(directory, ...args) {
    const result = run(directory, '--json', ...args);
    assert.equal(result.status, 0, result.stderr);
    return JSON.parse(result.stdout);
}

const markdown = (status, category) => `---\nstatus: ${status}\ncategory: ${category}\n---\n# Document\n`;

test('empty, missing, and unspecified directories produce empty results', async (t) => {
    const directory = await fixture(t);
    assert.deepEqual(records(directory), []);
    assert.deepEqual(records(path.join(directory, 'missing')), []);
    assert.deepEqual(records(undefined), []);
    assert.match(run(directory).stdout, /No matching Markdown files/);
    const help = run(directory, '--help');
    assert.equal(help.status, 0, help.stderr);
    assert.match(help.stdout, /without recursion/);
    assert.match(help.stdout, /No filename, language, date, or metadata schema is validated/);
});

test('repeated field filters use AND and keep each language file independent', async (t) => {
    const directory = await fixture(t, {
        'guide.en.md': markdown('draft', 'architecture'),
        'guide.zh.md': markdown('ready', 'architecture'),
        'checks.md': markdown('ready', 'tooling'),
        'misc.markdown': markdown('other-value', 'architecture')
    });
    assert.equal(records(directory).length, 4);
    assert.equal(records(directory, '--where', 'status=ready').length, 2);
    assert.equal(records(directory, '--where', 'category=architecture').length, 3);
    const selected = records(directory, '--where', 'status=ready', '--where', 'category=architecture');
    assert.equal(selected.length, 1);
    assert.equal(path.basename(selected[0].path), 'guide.zh.md');
    assert.deepEqual(Object.keys(selected[0]), ['path', 'meta']);
    assert.match(run(directory, '--where', 'category=tooling').stdout, /checks\.md\t/);
});

test('YAML values and extra fields are retained without imposing a schema', async (t) => {
    const directory = await fixture(t, {
        'custom.md': '---\nstatus: ""\ncategory: [one, two]\nenabled: true\ncount: 3\n"key.with.dot": a=b\ncontext: |\n  first line\n  second line\nextra: {enabled: true}\n---\n# Document\n',
        'plain.md': '# No frontmatter\n',
        'scalar.md': '---\nhello\n---\n# Scalar YAML\n'
    });
    const all = records(directory);
    assert.equal(all[0].meta.status, '');
    assert.deepEqual(all[0].meta.category, ['one', 'two']);
    assert.equal(all[0].meta.context, 'first line\nsecond line\n');
    assert.deepEqual(all[0].meta.extra, {enabled: true});
    assert.deepEqual(all[1].meta, {});
    assert.equal(all[2].meta, 'hello');
    assert.equal(records(directory, '--where', 'key.with.dot=a=b').length, 1);
    assert.equal(records(directory, '--where', 'status=""').length, 1);
    assert.equal(records(directory, '--where', 'enabled=true').length, 1);
    assert.equal(records(directory, '--where', 'count=3').length, 1);
    assert.deepEqual(records(directory, '--where', 'missing=value'), []);
});

test('YAML filter values distinguish strings, scalars, null, and absent fields', async (t) => {
    const directory = await fixture(t, {
        'typed.md': '---\nflag: false\nyear: 2026\nnullable: null\ntags: [one, two]\nconfig: {enabled: true}\n---\n',
        'strings.md': '---\nflag: "false"\nyear: "2026"\nnullable: ""\ntags: [two, one]\nconfig: {enabled: false}\n---\n',
        'missing.md': '# No metadata\n'
    });
    const selected = (...args) => records(directory, ...args).map((record) => path.basename(record.path));
    assert.deepEqual(selected('--where', 'flag=false'), ['typed.md']);
    assert.deepEqual(selected('--where', 'flag="false"'), ['strings.md']);
    assert.deepEqual(selected('--where', 'year=2026'), ['typed.md']);
    assert.deepEqual(selected('--where', 'year="2026"'), ['strings.md']);
    assert.deepEqual(selected('--where', 'nullable=null'), ['typed.md']);
    assert.deepEqual(selected('--where', 'nullable='), ['typed.md']);
    assert.deepEqual(selected('--where', 'nullable=""'), ['strings.md']);
    assert.deepEqual(selected('--where', 'missing=null'), []);
    assert.deepEqual(selected('--where', 'tags=[one, two]'), ['typed.md']);
    assert.deepEqual(selected('--where', 'config={enabled: true}'), ['typed.md']);
});

test('listing is flat, ordered by filename, and reads ordinary Markdown names', async (t) => {
    const directory = await fixture(t, {
        'z.MD': '# Last\n',
        'a.markdown': '# First\n',
        'README.md': '# Ordinary unpaired file\n',
        'nested/hidden.md': '---\nkey: [broken\n---\n',
        'ignored.txt': '---\nkey: [broken\n---\n'
    });
    assert.deepEqual(records(directory).map((entry) => path.basename(entry.path)), [
        'README.md', 'a.markdown', 'z.MD'
    ]);
});

test('malformed YAML is an error even when a filter would not select the file', async (t) => {
    const directory = await fixture(t, {'broken.md': '---\nkey: [broken\n---\n'});
    const result = run(directory, '--json', '--where', 'missing=value');
    assert.equal(result.status, 1);
    assert.equal(result.stdout, '');
    assert.match(result.stderr, /Cannot read frontmatter from .*broken\.md/);
});

test('read errors and malformed filter arguments fail clearly', async (t) => {
    const directory = await fixture(t, {'not-a-directory': 'contents'});
    let result = run(path.join(directory, 'not-a-directory'), '--json');
    assert.equal(result.status, 1);
    assert.equal(result.stdout, '');
    assert.match(result.stderr, /Cannot read directory/);
    result = run(directory, '--where', 'missing-separator');
    assert.equal(result.status, 1);
    assert.match(result.stderr, /Expected key=value/);
    result = run(directory, '--where', 'key=[broken');
    assert.equal(result.status, 1);
    assert.equal(result.stdout, '');
    assert.match(result.stderr, /Invalid YAML value for --where key/);
});

test('the shared parser reports the actual frontmatter source range', () => {
    const source = '---\ntitle: 中文\n---\n\n# Body\n\n---\n';
    const parsed = parseFrontmatter(source);
    assert.deepEqual(parsed.meta, {title: '中文'});
    assert.equal(source.slice(parsed.range.start, parsed.range.end), '---\ntitle: 中文\n---');
    assert.match(source.slice(parsed.range.end), /# Body/);
    assert.deepEqual(parseFrontmatter('# No YAML\n'), {meta: {}, range: null});
});
