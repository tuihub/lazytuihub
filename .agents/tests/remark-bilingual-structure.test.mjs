import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {mkdtemp, mkdir, rm, writeFile} from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import {test} from 'node:test';
import {fileURLToPath} from 'node:url';

const cli = fileURLToPath(new URL('../../node_modules/remark-cli/cli.js', import.meta.url));
const plugin = fileURLToPath(new URL('../scripts/remark-bilingual-structure.mjs', import.meta.url));
const gfm = fileURLToPath(import.meta.resolve('remark-gfm'));
const frontmatter = fileURLToPath(import.meta.resolve('remark-frontmatter'));

async function fixture(t, files) {
    const directory = await mkdtemp(path.join(os.tmpdir(), 'bilingual-'));
    t.after(() => rm(directory, {recursive: true, force: true}));
    for (const [name, content] of Object.entries({'.remarkignore': '', ...files})) {
        const destination = path.join(directory, name);
        await mkdir(path.dirname(destination), {recursive: true});
        await writeFile(destination, content);
    }
    return directory;
}

function lint(directory, files = ['.'], {unpairedFiles, unpairedPaths, plugins = []} = {}) {
    const options = Object.entries({unpairedFiles, unpairedPaths})
        .filter(([, value]) => value !== undefined)
        .map(([key, value]) => `${key}:${JSON.stringify(value)}`).join(',');
    const bilingual = options ? `${plugin}=${options}` : plugin;
    const result = spawnSync(process.execPath, [
        cli, '--no-config', '--ext', 'md', '--frail', '--no-stdout', '--no-color',
        '--use', gfm, '--use', frontmatter, '--use', bilingual,
        ...plugins.flatMap((entry) => ['--use', entry]), ...files
    ], {cwd: directory, encoding: 'utf8'});
    assert.ifError(result.error);
    return {status: result.status, output: result.stdout + result.stderr};
}

test('translated headings and changed code contents retain the same structure', async (t) => {
    const directory = await fixture(t, {
        'guide.zh.md': '---\ntitle: 指南\n---\n# 指南\n\n## 安装\n\n```csharp\nConsole.WriteLine("你好");\n```\n',
        'guide.en.md': '---\ntitle: Guide\n---\n# Guide\n\n## Install\n\n```csharp\nConsole.WriteLine("Hello");\n```\n'
    });
    const result = lint(directory);
    assert.equal(result.status, 0, result.output);
});

test('heading omissions and depth changes fail, including when only one side is linted', async (t) => {
    const directory = await fixture(t, {
        'guide.zh.md': '# 指南\n\n## 安装\n',
        'guide.en.md': '# Guide\n'
    });
    let result = lint(directory, ['guide.en.md']);
    assert.equal(result.status, 1, result.output);
    assert.match(result.output, /headings\s+bilingual/);

    await writeFile(path.join(directory, 'guide.en.md'), '# Guide\n\n### Install\n');
    result = lint(directory);
    assert.equal(result.status, 1, result.output);
    assert.equal((result.output.match(/headings\s+bilingual/g) || []).length, 1, result.output);
});

test('code-block languages and counts fail without imposing code-block order', async (t) => {
    const directory = await fixture(t, {
        'guide.zh.md': '# 指南\n\n```csharp\n中文内容\n```\n\n```sh\necho 你好\n```\n',
        'guide.en.md': '# Guide\n\n```sh\necho Hello\n```\n\n```csharp\nEnglish contents\n```\n'
    });
    let result = lint(directory);
    assert.equal(result.status, 0, result.output);

    await writeFile(path.join(directory, 'guide.en.md'), '# Guide\n\n```json\n{}\n```\n\n```sh\necho Hello\n```\n');
    result = lint(directory);
    assert.equal(result.status, 1, result.output);
    assert.match(result.output, /code-blocks\s+bilingual/);

    await writeFile(path.join(directory, 'guide.en.md'), '# Guide\n\n```sh\necho Hello\n```\n');
    result = lint(directory);
    assert.equal(result.status, 1, result.output);
    assert.match(result.output, /code-blocks\s+bilingual/);
});

test('all document candidates require the language suffix and an existing partner', async (t) => {
    const directory = await fixture(t, {
        'guide.md': '# Guide\n',
        'intro.zh.md': '# 简介\n'
    });
    const result = lint(directory);
    assert.equal(result.status, 1, result.output);
    assert.match(result.output, /filename\s+bilingual/);
    assert.match(result.output, /missing-partner\s+bilingual/);
});

test('an ignored document or partner skips the pair even if the partner does not exist', async (t) => {
    const directory = await fixture(t, {
        '.remarkignore': 'guide.en.md\nignored.md\narchive/\n',
        'guide.zh.md': '# 指南\n',
        'ignored.md': '# Unpaired\n',
        'archive/unpaired.zh.md': '# Ignored\n'
    });
    const result = lint(directory);
    assert.equal(result.status, 0, result.output);
});

test('nested .remarkignore and negation use the native nearest-file semantics', async (t) => {
    const directory = await fixture(t, {
        '.remarkignore': '*.en.md\n',
        'root.zh.md': '# 根\n',
        'docs/.remarkignore': '*.md\n!guide.zh.md\n!guide.en.md\n',
        'docs/guide.zh.md': '# 指南\n\n## 安装\n',
        'docs/guide.en.md': '# Guide\n\n### Install\n',
        'docs/ignored.md': '# Unpaired\n'
    });
    let result = lint(directory);
    assert.equal(result.status, 1, result.output);
    assert.match(result.output, /headings\s+bilingual/);
    assert.doesNotMatch(result.output, /(?:filename|missing-partner)\s+bilingual/);

    await writeFile(path.join(directory, 'docs/guide.en.md'), '# Guide\n\n## Install\n');
    result = lint(directory);
    assert.equal(result.status, 0, result.output);
});

test('.gitignore does not exclude documents from the bilingual policy', async (t) => {
    const directory = await fixture(t, {
        '.gitignore': 'guide.md\n',
        'guide.md': '# Guide\n'
    });
    const result = lint(directory);
    assert.equal(result.status, 1, result.output);
    assert.match(result.output, /filename\s+bilingual/);
});

test('only configured native interface names are exempt from bilingual naming', async (t) => {
    const directory = await fixture(t, {
        'AGENTS.md': '# Repository instructions\n',
        '.agents/skills/example/SKILL.md': '# Workflow instructions\n'
    });
    let result = lint(directory);
    assert.equal(result.status, 1, result.output);
    assert.match(result.output, /filename\s+bilingual/);

    const options = {unpairedFiles: ['AGENTS.md', 'SKILL.md']};
    result = lint(directory, ['.'], options);
    assert.equal(result.status, 0, result.output);

    await writeFile(path.join(directory, 'README.md'), '# Readme\n');
    result = lint(directory, ['.'], options);
    assert.equal(result.status, 1, result.output);
    assert.match(result.output, /filename\s+bilingual/);
});

test('native interface exemptions retain the other remark rules', async (t) => {
    const directory = await fixture(t, {
        'AGENTS.md': '# Instructions\n\n### Skipped level\n\n```\ncode\n```\n'
    });
    const result = lint(directory, ['.'], {
        unpairedFiles: ['AGENTS.md', 'SKILL.md'],
        plugins: [
            fileURLToPath(import.meta.resolve('remark-lint-fenced-code-flag')),
            fileURLToPath(import.meta.resolve('remark-lint-heading-increment'))
        ]
    });
    assert.equal(result.status, 1, result.output);
    assert.match(result.output, /fenced-code-flag/);
    assert.match(result.output, /heading-increment/);
    assert.doesNotMatch(result.output, /filename\s+bilingual|missing-partner\s+bilingual/);
});

test('the English API exception is scoped to its root directory and retains Markdown checks', async (t) => {
    const directory = await fixture(t, {
        'docs/api/index.md': '# API\n',
        'docs/api/models/Message.md': '# Message\n',
        'docs/development/guide.en.md': '# Guide\n',
        'other/docs/api/index.md': '# Other API\n'
    });
    const options = {unpairedFiles: ['AGENTS.md'], unpairedPaths: ['/docs/api/']};
    let result = lint(directory, ['docs/api'], options);
    assert.equal(result.status, 0, result.output);
    result = lint(directory, ['docs/development/guide.en.md'], options);
    assert.equal(result.status, 1, result.output);
    assert.match(result.output, /missing-partner\s+bilingual/);
    result = lint(directory, ['other/docs/api/index.md'], options);
    assert.equal(result.status, 1, result.output);
    assert.match(result.output, /filename\s+bilingual/);

    await writeFile(path.join(directory, 'docs/api/index.md'), '# API\n\n### Skipped heading\n');
    result = lint(directory, ['docs/api'], {
        ...options,
        plugins: [fileURLToPath(import.meta.resolve('remark-lint-heading-increment'))]
    });
    assert.equal(result.status, 1, result.output);
    assert.match(result.output, /heading-increment/);
    assert.doesNotMatch(result.output, /filename\s+bilingual|missing-partner\s+bilingual/);
});
