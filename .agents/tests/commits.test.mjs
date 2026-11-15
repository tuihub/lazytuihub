import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {mkdtemp, mkdir, readFile, rm, writeFile} from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import {test} from 'node:test';
import {fileURLToPath} from 'node:url';
import {validateCommitMessage} from '../scripts/commits.mjs';

const root = fileURLToPath(new URL('../../', import.meta.url));
const script = path.join(root, '.agents/scripts/commits.mjs');
const hookDirectory = path.join(root, '.agents/githooks');

test('Conventional Commits allow custom types, Unicode, case, punctuation, and unrestricted lengths', () => {
    const messages = [
        'feat: support storage adapters',
        'fix(core): avoid duplicate writes',
        'CustomType(scope)!: Add API.',
        '打包(工具): 初始化',
        'release-tools: package',
        `RESEARCH(Project Area): ${'Long descriptive text. '.repeat(100)}`,
        'feat!: replace the wire format',
        'feat(api)!: return task handles',
        'refactor: simplify scheduling\n\nBREAKING CHANGE: callback arguments change',
        'refactor: simplify scheduling\n\nBREAKING-CHANGE: callback arguments change',
        'feat!: replace the wire format\n\nBREAKING CHANGE: clients must upgrade',
        'fix: preserve context\n\nExplain why the old context was lost.\nContinue with another line.\n\nReviewed-by: A Developer\nRefs #42',
        'feat: replace scheduling\n\nBREAKING CHANGE: callbacks run later\nConsumers must await completion.\n\nReviewed-by: A Developer',
        'docs: discuss compatibility\n\nA breaking change may need a migration.\n* BREAKING CHANGE: this is a body example\n-custom-\nThis body is free-form.'
    ];
    for (const message of messages) assert.deepEqual(validateCommitMessage(message), [], message);
});

test('mandatory header syntax rejects malformed messages without merge or revert exemptions', () => {
    for (const message of ['', 'plain message', ': missing type', 'feat: ', 'feat(): empty scope', 'feat( ): blank scope', 'feat:missing space', 'feat(scope)! missing colon', 'Merge branch main', 'Revert "prior change"', 'v1.2.3', '\nfeat: preceded by a blank line']) {
        assert.notEqual(validateCommitMessage(message).length, 0, message);
    }
    assert.deepEqual(validateCommitMessage('revert: restore previous behavior'), []);
});

test('body and recognized footers require blank-line boundaries', () => {
    for (const message of ['fix: preserve state\nBody without a blank line.', 'fix: preserve state\n\nExplain the change.\nRefs: #42', 'feat: update API\n\nExplain the change.\nBREAKING CHANGE: replace the signature']) {
        assert.ok(validateCommitMessage(message).some((problem) => /blank line/.test(problem)), message);
    }
    assert.deepEqual(validateCommitMessage('fix: preserve state\r\n\r\nBody on CRLF lines.\r\n'), []);
});

test('breaking-change footer tokens require uppercase, colon-space, and a description', () => {
    for (const footer of ['breaking change: altered behavior', 'Breaking-Change: altered behavior', 'BREAKING CHANGE:altered behavior', 'BREAKING CHANGE: ', 'BREAKING-CHANGE: ']) {
        const message = `feat: update API\n\n${footer}`;
        assert.notEqual(validateCommitMessage(message).length, 0, footer);
    }
    assert.deepEqual(validateCommitMessage('feat: update API\n\nBREAKING CHANGE: first change\nBREAKING CHANGE: second change'), []);
    for (const footer of ['BREAKING CHANGE:missing space', 'BREAKING-CHANGE:\tmissing space']) {
        assert.notEqual(validateCommitMessage(`feat: update API\n\nBREAKING CHANGE: first change\n${footer}`).length, 0);
    }
    for (const footer of ['Reviewed-by:\tA Developer', 'Refs\t#42']) {
        assert.notEqual(validateCommitMessage(`fix: preserve state\n\n${footer}`).length, 0);
    }
});

async function directory(t) {
    const temporary = await mkdtemp(path.join(os.tmpdir(), 'commit checks '));
    t.after(() => rm(temporary, {recursive: true, force: true}));
    return temporary;
}

function git(cwd, ...args) {
    const result = spawnSync('git', args, {cwd, encoding: 'utf8'});
    assert.ifError(result.error);
    assert.equal(result.status, 0, result.stderr);
    return result.stdout.trim();
}

async function repository(t) {
    const temporary = await directory(t);
    git(temporary, 'init', '--quiet');
    git(temporary, 'config', 'user.name', 'Commit check test');
    git(temporary, 'config', 'user.email', 'commit-check@example.invalid');
    git(temporary, 'config', 'commit.gpgsign', 'false');
    git(temporary, 'commit', '--quiet', '--allow-empty', '-m', 'chore: create a fixture');
    await mkdir(path.join(temporary, '.agents/githooks'), {recursive: true});
    await writeFile(path.join(temporary, '.agents/githooks/commit-msg'), '#!/bin/sh\nexit 0\n');
    return temporary;
}

function run(cwd, ...args) {
    const result = spawnSync(process.execPath, [script, ...args], {cwd, encoding: 'utf8'});
    assert.ifError(result.error);
    return result;
}

test('explicit checks read message files and revisions without rewriting them', async (t) => {
    const temporary = await repository(t);
    const file = path.join(temporary, 'message with spaces.txt');
    const message = 'CustomType(scope)!: Add API.\n';
    await writeFile(file, message);
    assert.equal(run(temporary, 'check', '--edit', file).status, 0);
    assert.equal(await readFile(file, 'utf8'), message);
    assert.equal(run(temporary, 'check').status, 0);
    assert.equal(run(temporary, 'check', '--revision', 'HEAD').status, 0);
    assert.equal(run(temporary, 'check', '--revision', 'missing-commit').status, 1);
    assert.equal(run(temporary, 'check', '--edit', file, '--revision', 'HEAD').status, 1);
    await writeFile(file, 'not conventional\n');
    assert.equal(run(temporary, 'check', '--edit', file).status, 1);
    assert.equal(await readFile(file, 'utf8'), 'not conventional\n');
});

test('a missing parser dependency fails without restoring packages', async (t) => {
    const temporary = await directory(t);
    const isolatedScript = path.join(temporary, 'commits.mjs');
    const file = path.join(temporary, 'message.txt');
    await writeFile(isolatedScript, await readFile(script));
    await writeFile(file, 'chore: check missing dependencies\n');
    const result = spawnSync(process.execPath, [isolatedScript, 'check', '--edit', file], {cwd: temporary, encoding: 'utf8'});
    assert.ifError(result.error);
    assert.equal(result.status, 1);
    assert.match(result.stderr, /Cannot find package 'conventional-commits-parser'/);
    await assert.rejects(readFile(path.join(temporary, 'node_modules/conventional-commits-parser/package.json')), {code: 'ENOENT'});
});

test('hook setup is repository-local, idempotent, and preserves custom hooks', async (t) => {
    const temporary = await repository(t);
    assert.equal(run(temporary, 'setup').status, 0);
    assert.equal(git(temporary, 'config', '--local', '--get', 'core.hooksPath'), '.agents/githooks');
    assert.equal(run(temporary, 'setup').status, 0);
    git(temporary, 'config', '--local', 'core.hooksPath', 'custom-hooks');
    const configured = run(temporary, 'setup');
    assert.equal(configured.status, 1);
    assert.match(configured.stderr, /existing core.hooksPath/);
    assert.equal(git(temporary, 'config', '--local', '--get', 'core.hooksPath'), 'custom-hooks');

    const existing = await repository(t);
    const custom = path.join(existing, '.git/hooks/commit-msg');
    await writeFile(custom, '#!/bin/sh\nexit 0\n');
    const result = run(existing, 'setup');
    assert.equal(result.status, 1);
    assert.match(result.stderr, /Existing Git hooks/);
    assert.equal(await readFile(custom, 'utf8'), '#!/bin/sh\nexit 0\n');
});

test('Git executes the real hook and safely forwards a message filename containing spaces', async (t) => {
    const temporary = await directory(t);
    const file = path.join(temporary, 'commit message.txt');
    for (const [message, expected] of [['feat(api)!: return task handles\n', 0], ['invalid commit message\n', 1]]) {
        await writeFile(file, message);
        const result = spawnSync('git', ['-c', `core.hooksPath=${hookDirectory}`, 'hook', 'run', 'commit-msg', '--', file], {cwd: root, encoding: 'utf8'});
        assert.ifError(result.error);
        assert.equal(result.status, expected, result.stderr);
        assert.equal(await readFile(file, 'utf8'), message);
    }
});
