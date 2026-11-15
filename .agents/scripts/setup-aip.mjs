import {spawnSync} from 'node:child_process';
import {mkdir, mkdtemp, rename, rm, stat} from 'node:fs/promises';
import path from 'node:path';
import {fileURLToPath} from 'node:url';

const root = fileURLToPath(new URL('../../', import.meta.url));
const directory = path.join(root, '.agents/references/google-aip');
const [repository, revision, mode] = process.argv.slice(2);

try {
    if (!repository || !revision) throw new Error('Pass the repository and revision declared in mise.toml.');
    const exists = await stat(directory).then(() => true, error => {
        if (error.code === 'ENOENT') return false;
        throw error;
    });
    if (mode === '--check') {
        if (!exists) throw new Error('AIP references are missing. Run `mise run setup:aip`.');
        verifyCheckout(directory);
        if (git(['rev-parse', 'HEAD'], directory) !== revision) {
            throw new Error('AIP references do not match mise.toml. Run `mise run setup:aip`.');
        }
    } else if (exists) {
        verifyCheckout(directory);
        if (git(['rev-parse', 'HEAD'], directory) !== revision) {
            git(['fetch', '--depth=1', 'origin', revision], directory);
            git(['checkout', '--detach', revision], directory);
        }
    } else {
        await mkdir(path.dirname(directory), {recursive: true});
        const temporary = await mkdtemp(path.join(path.dirname(directory), '.google-aip-'));
        try {
            git(['clone', '--no-checkout', '--depth=1', '--', repository, temporary], root);
            git(['fetch', '--depth=1', 'origin', revision], temporary);
            git(['checkout', '--detach', revision], temporary);
            await rename(temporary, directory);
        } finally {
            await rm(temporary, {recursive: true, force: true});
        }
    }
    console.log(`Official AIP references ready at ${revision}.`);
} catch (error) {
    console.error(error.message);
    process.exitCode = 1;
}

function verifyCheckout(location) {
    if (path.resolve(git(['rev-parse', '--show-toplevel'], location)) !== path.resolve(location)) {
        throw new Error('The AIP reference directory must be a separate Git checkout.');
    }
    if (git(['remote', 'get-url', 'origin'], location) !== repository) {
        throw new Error('The AIP checkout origin differs from mise.toml; it was left unchanged.');
    }
    if (git(['status', '--porcelain'], location)) {
        throw new Error('The AIP checkout has local changes; it was left unchanged.');
    }
}

function git(args, cwd) {
    const result = spawnSync('git', args, {cwd, encoding: 'utf8', stdio: ['ignore', 'pipe', 'inherit']});
    if (result.error) throw result.error;
    if (result.status !== 0) throw new Error(`git ${args[0]} failed.`);
    return result.stdout.trim();
}
