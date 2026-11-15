import {spawn} from 'node:child_process';
import {readFile} from 'node:fs/promises';
import path from 'node:path';
import {parseArgs, isDeepStrictEqual} from 'node:util';
import {parseFrontmatter} from './frontmatter.mjs';
import {isNotePath} from './remark-note-metadata.mjs';

const proposedSuccessors = new Set(['proposed', 'implemented', 'rejected', 'superseded', 'archived']);

async function git(cwd, args, allowFailure = false) {
    const result = await new Promise((resolve, reject) => {
        const child = spawn('git', args, {cwd, stdio: ['ignore', 'pipe', 'pipe']});
        const stdout = [];
        const stderr = [];
        child.stdout.on('data', (chunk) => stdout.push(chunk));
        child.stderr.on('data', (chunk) => stderr.push(chunk));
        child.on('error', (error) => reject(new Error(`Cannot run Git: ${error.message}`)));
        child.on('close', (code) => resolve({code, stdout: Buffer.concat(stdout), stderr: Buffer.concat(stderr)}));
    });
    if (result.code !== 0 && !allowFailure) {
        throw new Error(`Git ${args[0]} failed: ${result.stderr.toString('utf8').trim() || `exit ${result.code}`}`);
    }
    return result;
}

function snapshot(bytes) {
    const source = bytes.toString('utf8');
    const {meta, range} = parseFrontmatter(source);
    if (!range || !meta || typeof meta !== 'object' || Array.isArray(meta)) {
        throw new Error('Cannot compare history without YAML frontmatter mapping.');
    }
    const {status, superseded_by, ...otherMetadata} = meta;
    return {
        status,
        otherMetadata,
        body: source.slice(0, range.start) + source.slice(range.end)
    };
}

function changeProblem(previousBytes, currentBytes) {
    if (currentBytes && previousBytes.equals(currentBytes)) return;

    const previous = snapshot(previousBytes);
    if (!currentBytes) {
        if (previous.status !== 'proposed') return `Cannot delete a ${previous.status} note.`;
        return;
    }

    if (previous.status === 'rejected' || previous.status === 'archived') {
        return `A ${previous.status} note is frozen; its contents cannot change.`;
    }

    const current = snapshot(currentBytes);
    if (previous.status === 'proposed' && proposedSuccessors.has(current.status)) return;
    if ((previous.status === 'implemented' || previous.status === 'superseded')
        && (current.status === 'superseded' || current.status === 'archived')) {
        if (previous.body !== current.body || !isDeepStrictEqual(previous.otherMetadata, current.otherMetadata)) {
            return `Updating coverage of a ${previous.status} note may change only status and superseded_by metadata.`;
        }
        return;
    }
    if (previous.status === 'implemented' && current.status === 'implemented') {
        return 'An implemented note is frozen until it is superseded or archived.';
    }
    return `Cannot move a note from ${previous.status} to ${current.status}.`;
}

async function main() {
    const {values} = parseArgs({options: {base: {type: 'string'}, help: {type: 'boolean'}}});
    if (values.help) {
        console.log('Usage: node .agents/scripts/check-note-history.mjs [--base <commit>]');
        console.log('Compare existing notes with HEAD (or --base); an unborn repository has an empty baseline.');
        console.log('Check single-direction snapshot compatibility, not each uncommitted operation or the full Git history.');
        console.log('New notes may first appear in any valid state. Current metadata is checked by the Markdown lint.');
        return;
    }

    const rootResult = await git(process.cwd(), ['rev-parse', '--show-toplevel']);
    const root = rootResult.stdout.toString('utf8').trim();
    const base = values.base ?? 'HEAD';
    const resolved = await git(root, ['rev-parse', '--verify', '--quiet', '--end-of-options', `${base}^{commit}`], true);
    if (resolved.code !== 0) {
        if (values.base !== undefined) throw new Error(`Cannot resolve --base ${JSON.stringify(base)} to a Git commit.`);
        console.log('Note history: no HEAD commit; the baseline is empty.');
        return;
    }

    const commit = resolved.stdout.toString('utf8').trim();
    const listed = await git(root, ['ls-tree', '-r', '--name-only', '-z', '--full-tree', commit, '--', '.agents/notes/']);
    const notes = listed.stdout.toString('utf8').split('\0').filter((name) => name && isNotePath(name, root));
    const problems = [];
    for (const name of notes) {
        try {
            const previous = await git(root, ['cat-file', '--filters', `${commit}:${name}`]);
            let current;
            try {
                current = await readFile(path.join(root, name));
            } catch (error) {
                if (error.code !== 'ENOENT') throw error;
            }
            const problem = changeProblem(previous.stdout, current);
            if (problem) problems.push(`${name}: ${problem}`);
        } catch (error) {
            problems.push(`${name}: ${error.message}`);
        }
    }

    if (problems.length) {
        console.error(problems.join('\n'));
        process.exitCode = 1;
    } else {
        console.log(`Note history: ${notes.length} existing note(s) compatible with ${base}.`);
    }
}

try {
    await main();
} catch (error) {
    console.error(error.message);
    process.exitCode = 1;
}
