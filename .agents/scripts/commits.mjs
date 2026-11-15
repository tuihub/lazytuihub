import {spawn} from 'node:child_process';
import {readFile, readdir} from 'node:fs/promises';
import path from 'node:path';
import {pathToFileURL} from 'node:url';
import {parseArgs} from 'node:util';
import {CommitParser} from 'conventional-commits-parser';

const hooksPath = '.agents/githooks';
const breakingFooterPattern = /^(BREAKING CHANGE|BREAKING-CHANGE):\s*(.*)$/i;
const parser = new CommitParser({
    headerPattern: /^([^\s():!]+)(?:\(([^()\r\n]+)\))?(!)?: (.+)$/u,
    headerCorrespondence: ['type', 'scope', 'breaking', 'subject'],
    notesPattern: () => breakingFooterPattern,
    fieldPattern: /$a/
});

export function validateCommitMessage(source) {
    const message = source.replaceAll('\r\n', '\n');
    const lines = message.split('\n');
    if (!message.trim()) return ['The commit message is empty.'];
    const commit = parser.parse(message);
    const problems = [];
    if (commit.header !== lines[0] || !commit.type || !commit.subject?.trim() || (commit.scope !== null && commit.scope !== undefined && !commit.scope.trim())) {
        problems.push('Use type(optional scope)!: description, with a non-empty type, scope when provided, and description.');
    }
    if (lines.slice(1).some((line) => line.trim()) && lines[1].trim()) {
        problems.push('Separate the description from the body or footers with a blank line.');
    }
    if (commit.footer) {
        const offset = message.lastIndexOf(commit.footer);
        if (offset >= 0 && message.slice(0, offset).split('\n').at(-2)?.trim()) {
            problems.push('Separate the footer section from the body with a blank line.');
        }
        for (const line of commit.footer.split('\n')) {
            const marker = /^(BREAKING CHANGE|[\w-]+)(:\s+|\s+#).+/i.exec(line);
            if (marker && !marker[2].startsWith(': ') && marker[2] !== ' #') {
                problems.push('Separate a footer token from its value with ": " or " #".');
            }
        }
    }
    const noteLines = lines.slice(1).filter((line) => breakingFooterPattern.test(line));
    for (const [index, line] of noteLines.entries()) {
        const note = commit.notes[index];
        if (!['BREAKING CHANGE', 'BREAKING-CHANGE'].includes(note.title)) {
            problems.push('Write the breaking-change footer token as BREAKING CHANGE or BREAKING-CHANGE.');
        }
        if (!note.text.trim()) problems.push('A breaking-change footer needs a description.');
        if (!line.startsWith(`${note.title}: `)) {
            problems.push('Follow a breaking-change footer token with a colon and a space.');
        }
    }
    return problems;
}

async function git(args, allowFailure = false) {
    const result = await new Promise((resolve, reject) => {
        const child = spawn('git', args, {stdio: ['ignore', 'pipe', 'pipe']});
        const stdout = [];
        const stderr = [];
        child.stdout.on('data', (chunk) => stdout.push(chunk));
        child.stderr.on('data', (chunk) => stderr.push(chunk));
        child.on('error', reject);
        child.on('close', (code) => resolve({code, stdout: Buffer.concat(stdout).toString('utf8'), stderr: Buffer.concat(stderr).toString('utf8')}));
    });
    if (result.code !== 0 && !allowFailure) throw new Error(`Git ${args[0]} failed: ${result.stderr.trim() || `exit ${result.code}`}`);
    return result;
}

async function setupHooks() {
    const root = (await git(['rev-parse', '--show-toplevel'])).stdout.trim();
    await readFile(path.join(root, hooksPath, 'commit-msg'));
    const configured = await git(['config', '--get', 'core.hooksPath'], true);
    if (configured.code === 0 && configured.stdout.trim() !== hooksPath) {
        throw new Error('An existing core.hooksPath is configured. Preserve it and arrange hook integration explicitly.');
    }
    if (configured.code !== 0 && configured.code !== 1) throw new Error(configured.stderr.trim());
    if (configured.code === 1) {
        const directory = (await git(['rev-parse', '--git-path', 'hooks'])).stdout.trim();
        let entries;
        try {
            entries = await readdir(directory, {withFileTypes: true});
        } catch (error) {
            if (error.code !== 'ENOENT') throw error;
            entries = [];
        }
        if (entries.some((entry) => !entry.name.endsWith('.sample') && (entry.isFile() || entry.isSymbolicLink()))) {
            throw new Error('Existing Git hooks were found. Preserve them and arrange hook integration explicitly.');
        }
    }
    await git(['config', '--local', 'core.hooksPath', hooksPath]);
    console.log(`Local Git hooks enabled: ${hooksPath}`);
}

async function main() {
    const {values, positionals} = parseArgs({
        options: {edit: {type: 'string'}, revision: {type: 'string'}, help: {type: 'boolean'}},
        allowPositionals: true
    });
    if (values.help) {
        console.log('Usage: commits.mjs check [--edit <message-file> | --revision <commit>]');
        console.log('       commits.mjs setup');
        console.log('Check Conventional Commits 1.0.0 without rewriting the message. The default revision is HEAD.');
        console.log('Setup configures this repository only and refuses to replace existing custom hooks.');
        return;
    }
    if (positionals.length !== 1 || !['check', 'setup'].includes(positionals[0])) throw new Error('Choose check or setup; use --help for usage.');
    if (positionals[0] === 'setup') {
        if (values.edit !== undefined || values.revision !== undefined) throw new Error('Setup does not accept message or revision options.');
        await setupHooks();
        return;
    }
    if (values.edit !== undefined && values.revision !== undefined) throw new Error('Choose --edit or --revision, not both.');
    let message;
    if (values.edit !== undefined) message = await readFile(values.edit, 'utf8');
    else {
        const revision = values.revision ?? 'HEAD';
        const resolved = await git(['rev-parse', '--verify', '--quiet', '--end-of-options', `${revision}^{commit}`]);
        message = (await git(['show', '-s', '--format=%B', resolved.stdout.trim()])).stdout;
    }
    const problems = validateCommitMessage(message);
    if (problems.length) {
        console.error(problems.join('\n'));
        process.exitCode = 1;
    } else console.log('Commit message follows Conventional Commits 1.0.0.');
}

if (process.argv[1] && pathToFileURL(path.resolve(process.argv[1])).href === import.meta.url) {
    try {
        await main();
    } catch (error) {
        console.error(error.message);
        process.exitCode = 1;
    }
}
