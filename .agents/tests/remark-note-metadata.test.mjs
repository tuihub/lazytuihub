import assert from 'node:assert/strict';
import {mkdtemp, mkdir, readFile, rm, writeFile} from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import {test} from 'node:test';
import {remark} from 'remark';
import remarkFrontmatter from 'remark-frontmatter';
import {parseDocument} from 'yaml';
import remarkNoteMetadata, {isNotePath, parseNoteName} from '../scripts/remark-note-metadata.mjs';

const processor = remark().use(remarkFrontmatter).use(remarkNoteMetadata);
const schema = parseDocument(await readFile(new URL('../schemas/note-metadata.yaml', import.meta.url), 'utf8')).toJSON();
const categoryLine = `category: ${JSON.stringify(schema.properties.category.enum[0])}`;

function note(status = 'proposed', extra = '') {
    return `---\nstatus: ${status}\n${categoryLine}\n${extra}---\n\n# Decision\n\nContext and rationale.\n`;
}

function references(...names) {
    return `superseded_by: ${JSON.stringify(names)}\n`;
}

async function fixture(t, files) {
    const directory = await mkdtemp(path.join(os.tmpdir(), 'note-metadata-'));
    t.after(() => rm(directory, {recursive: true, force: true}));
    for (const [name, content] of Object.entries(files)) {
        const destination = path.join(directory, name);
        await mkdir(path.dirname(destination), {recursive: true});
        await writeFile(destination, content);
    }
    return directory;
}

async function lint(directory, relativePath) {
    const absolutePath = path.join(directory, relativePath);
    return processor.process({cwd: directory, path: absolutePath, value: await readFile(absolutePath, 'utf8')});
}

test('note names use real calendar dates and preserve unrestricted topic text', () => {
    assert.deepEqual(parseNoteName('2024-02-29-研发 方案.zh.md'), {
        date: '2024-02-29', topic: '研发 方案', language: 'zh'
    });
    assert.ok(parseNoteName('2099-01-01-future.en.md'));
    for (const name of ['2026-02-29-invalid.zh.md', '2026-13-01-invalid.en.md', '2026-01-01-.zh.md', 'guide.md']) {
        assert.equal(parseNoteName(name), undefined, name);
    }
    assert.ok(isNotePath('.agents/notes/2026-11-15-topic.en.md', '/project'));
    assert.equal(isNotePath('.agents/notes/nested/2026-11-15-topic.en.md', '/project'), false);
    assert.equal(isNotePath('docs/2026-11-15-topic.en.md', '/project'), false);
});

test('all current states use the schema category whitelist without a Git baseline', async (t) => {
    const files = {};
    for (const status of ['proposed', 'implemented', 'rejected']) {
        files[`.agents/notes/2026-11-15-${status}.zh.md`] = note(status);
    }
    files['.agents/notes/2026-11-15-old.zh.md'] = note('superseded', references('2020-01-01-current.en.md'));
    files['.agents/notes/2026-11-15-archived.zh.md'] = note('archived', references('2020-01-01-current.en.md'));
    files['.agents/notes/2020-01-01-current.en.md'] = note('implemented');
    const directory = await fixture(t, files);
    for (const name of Object.keys(files)) {
        const result = await lint(directory, name);
        assert.equal(result.messages.length, 0, result.messages.join('\n'));
        assert.equal(await readFile(path.join(directory, name), 'utf8'), files[name]);
    }
});

test('notes require YAML mappings, known statuses and schema category choices', async (t) => {
    const files = {
        '.agents/notes/2026-11-15-plain.zh.md': '# No metadata\n',
        '.agents/notes/2026-11-15-array.zh.md': '---\n- proposed\n---\n',
        '.agents/notes/2026-11-15-malformed.zh.md': '---\nstatus: [\n---\n',
        '.agents/notes/2026-11-15-status.zh.md': note('draft'),
        '.agents/notes/2026-11-15-category.zh.md': note().replace(categoryLine, 'category: "  "'),
        '.agents/notes/2026-11-15-missing.zh.md': `---\n${categoryLine}\n---\n`,
        '.agents/notes/2026-11-15-missing-category.zh.md': '---\nstatus: proposed\n---\n',
        '.agents/notes/2026-11-15-category-type.zh.md': note().replace(categoryLine, 'category: [repository]'),
        '.agents/notes/guide.zh.md': note()
    };
    const directory = await fixture(t, files);
    const expected = ['frontmatter', 'schema', 'frontmatter', 'schema', 'schema', 'schema', 'schema', 'schema', 'filename'];
    for (const [index, name] of Object.keys(files).entries()) {
        const result = await lint(directory, name);
        assert.ok(result.messages.some((message) => message.ruleId === expected[index]), `${name}: ${result.messages.join('\n')}`);
    }
});

test('the schema whitelist rejects unknown fields rather than removing them', async (t) => {
    const relativePath = '.agents/notes/2026-11-15-unknown.en.md';
    const directory = await fixture(t, {[relativePath]: note()});
    for (const extra of ['custom: {tags: [one, two]}\n', 'date: 2026-11-15\n']) {
        const source = note('proposed', extra);
        await writeFile(path.join(directory, relativePath), source);
        const result = await lint(directory, relativePath);
        assert.ok(result.messages.some((message) => message.ruleId === 'schema' && /additional properties/.test(message.reason)), result.messages.join('\n'));
        assert.equal(await readFile(path.join(directory, relativePath), 'utf8'), source);
    }
});

test('replacement metadata is optional outside coverage states but always follows the schema format', async (t) => {
    const relativePath = '.agents/notes/2026-11-15-optional.en.md';
    const directory = await fixture(t, {[relativePath]: note()});
    for (const status of ['proposed', 'implemented', 'rejected']) {
        await writeFile(path.join(directory, relativePath), note(status, references('2026-11-15-unwritten.zh.md')));
        assert.equal((await lint(directory, relativePath)).messages.length, 0);
    }
    for (const value of [null, 42, '2026-11-15-next.zh.md', [], [null], [42], [''], ['  '], ['../2026-11-15-next.zh.md'], ['folder\\2026-11-15-next.zh.md'], ['next.txt']]) {
        await writeFile(path.join(directory, relativePath), note('proposed', `superseded_by: ${JSON.stringify(value)}\n`));
        const result = await lint(directory, relativePath);
        assert.ok(result.messages.some((message) => message.ruleId === 'schema'), `${value}: ${result.messages.join('\n')}`);
    }
    for (const status of ['superseded', 'archived']) {
        await writeFile(path.join(directory, relativePath), note(status));
        const result = await lint(directory, relativePath);
        assert.ok(result.messages.some((message) => message.ruleId === 'schema' && /superseded_by/.test(message.reason)), result.messages.join('\n'));
    }
});

test('note-specific metadata does not constrain other directories or nested content', async (t) => {
    const files = {
        'docs/guide.zh.md': '# Guide\n',
        '.agents/other.zh.md': '# Other agent document\n',
        '.agents/notes/nested/guide.zh.md': '# Outside the flat note directory\n'
    };
    const directory = await fixture(t, files);
    for (const name of Object.keys(files)) {
        assert.equal((await lint(directory, name)).messages.length, 0);
    }
});

test('replacement references require an existing same-directory note and reject self references', async (t) => {
    const fileName = '2026-11-15-old.zh.md';
    const relativePath = `.agents/notes/${fileName}`;
    const directory = await fixture(t, {[relativePath]: note('superseded')});
    for (const status of ['superseded', 'archived']) {
        for (const replacement of [fileName, '2026-11-15-missing.zh.md', '2026-02-29-invalid-date.zh.md']) {
            await writeFile(path.join(directory, relativePath), note(status, references(replacement)));
            const result = await lint(directory, relativePath);
            assert.ok(result.messages.some((message) => message.ruleId === 'superseded-by'), result.messages.join('\n'));
        }
    }
});

test('full coverage can precede full and partial coverage ending in an implemented note', async (t) => {
    const files = {
        '.agents/notes/2026-11-15-old.zh.md': note('archived', references('2026-11-15-full.en.md')),
        '.agents/notes/2026-11-15-full.en.md': note('archived', references('2026-11-15-middle.en.md')),
        '.agents/notes/2026-11-15-middle.en.md': note('superseded', references('2026-11-15-current.zh.md')),
        '.agents/notes/2026-11-15-current.zh.md': note('implemented')
    };
    const directory = await fixture(t, files);
    for (const name of Object.keys(files)) {
        assert.equal((await lint(directory, name)).messages.length, 0);
    }

    for (const status of ['proposed', 'rejected']) {
        await writeFile(path.join(directory, '.agents/notes/2026-11-15-current.zh.md'), note(status));
        const result = await lint(directory, '.agents/notes/2026-11-15-old.zh.md');
        assert.ok(result.messages.some((message) => message.ruleId === 'superseded-by'), result.messages.join('\n'));
    }
});

test('a note partially covering another note cannot itself be directly partially covered', async (t) => {
    const files = {
        '.agents/notes/2026-11-15-old.zh.md': note('superseded', references('2026-11-15-current.zh.md', '2026-11-15-middle.en.md')),
        '.agents/notes/2026-11-15-middle.en.md': note('implemented'),
        '.agents/notes/2026-11-15-current.zh.md': note('implemented')
    };
    const directory = await fixture(t, files);
    await writeFile(path.join(directory, '.agents/notes/2026-11-15-middle.en.md'), note('superseded', references('2026-11-15-current.zh.md')));
    assert.equal((await lint(directory, '.agents/notes/2026-11-15-middle.en.md')).messages.length, 0);
    const result = await lint(directory, '.agents/notes/2026-11-15-old.zh.md');
    assert.ok(result.messages.some((message) => message.ruleId === 'superseded-by' && /must be implemented or archived/.test(message.reason)), result.messages.join('\n'));
});

test('covering notes can be archived repeatedly without first archiving a partial predecessor', async (t) => {
    const relativePath = '.agents/notes/2026-11-15-old.zh.md';
    const original = note('superseded', references('2026-11-15-first.en.md'));
    const directory = await fixture(t, {
        [relativePath]: original,
        '.agents/notes/2026-11-15-first.en.md': note('implemented'),
        '.agents/notes/2026-11-15-second.zh.md': note('implemented'),
        '.agents/notes/2026-11-15-current.en.md': note('implemented')
    });
    assert.equal((await lint(directory, relativePath)).messages.length, 0);
    await writeFile(path.join(directory, '.agents/notes/2026-11-15-first.en.md'), note('archived', references('2026-11-15-second.zh.md')));
    assert.equal((await lint(directory, relativePath)).messages.length, 0);
    await writeFile(path.join(directory, '.agents/notes/2026-11-15-second.zh.md'), note('archived', references('2026-11-15-current.en.md')));
    assert.equal((await lint(directory, relativePath)).messages.length, 0);
    assert.equal(await readFile(path.join(directory, relativePath), 'utf8'), original);
});

test('an archived boundary permits another partial edge without propagating ancestral constraints', async (t) => {
    const files = {
        '.agents/notes/2026-11-15-old.zh.md': note('superseded', references('2026-11-15-full.en.md')),
        '.agents/notes/2026-11-15-full.en.md': note('archived', references('2026-11-15-partial.zh.md')),
        '.agents/notes/2026-11-15-partial.zh.md': note('superseded', references('2026-11-15-current.en.md')),
        '.agents/notes/2026-11-15-current.en.md': note('implemented')
    };
    const directory = await fixture(t, files);
    for (const name of Object.keys(files)) {
        assert.equal((await lint(directory, name)).messages.length, 0);
    }
});

test('multiple fully covered branches may merge into one note that is later partially covered', async (t) => {
    const files = {
        '.agents/notes/2026-11-15-root.en.md': note('archived', references('2026-11-15-first.zh.md', '2026-11-15-second.zh.md')),
        '.agents/notes/2026-11-15-first.zh.md': note('archived', references('2026-11-15-middle.en.md')),
        '.agents/notes/2026-11-15-second.zh.md': note('archived', references('2026-11-15-middle.en.md')),
        '.agents/notes/2026-11-15-middle.en.md': note('superseded', references('2026-11-15-current.zh.md')),
        '.agents/notes/2026-11-15-current.zh.md': note('implemented')
    };
    const directory = await fixture(t, files);
    for (const name of Object.keys(files)) {
        assert.equal((await lint(directory, name)).messages.length, 0);
    }
});

test('partial and full coverage allow multiple implemented targets and repeated references', async (t) => {
    const relativePath = '.agents/notes/2026-11-15-old.zh.md';
    const files = {
        [relativePath]: note('superseded'),
        '.agents/notes/2026-11-15-first.en.md': note('implemented'),
        '.agents/notes/2026-11-15-second.zh.md': note('implemented')
    };
    const directory = await fixture(t, files);
    for (const status of ['superseded', 'archived']) {
        await writeFile(path.join(directory, relativePath), note(status, references('2026-11-15-first.en.md', '2026-11-15-second.zh.md', '2026-11-15-first.en.md')));
        assert.equal((await lint(directory, relativePath)).messages.length, 0);
        await writeFile(path.join(directory, relativePath), note(status, references('2026-11-15-first.en.md', '2026-11-15-missing.zh.md')));
        const result = await lint(directory, relativePath);
        assert.ok(result.messages.some((message) => message.ruleId === 'superseded-by' && /Cannot read/.test(message.reason)), result.messages.join('\n'));
    }
});

test('replacement cycles have no valid implemented endpoint', async (t) => {
    const directory = await fixture(t, {
        '.agents/notes/2026-11-15-first.zh.md': note('archived', references('2026-11-15-current.zh.md', '2026-11-15-second.en.md')),
        '.agents/notes/2026-11-15-second.en.md': note('archived', references('2026-11-15-first.zh.md')),
        '.agents/notes/2026-11-15-current.zh.md': note('implemented')
    });
    const result = await lint(directory, '.agents/notes/2026-11-15-first.zh.md');
    assert.ok(result.messages.some((message) => message.ruleId === 'superseded-by' && /cycle/.test(message.reason)));
});
