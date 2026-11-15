import {spawnSync} from 'node:child_process';
import {lstat} from 'node:fs/promises';

const maximumBytes = 1024 * 1024;

try {
    const files = spawnSync('git', ['ls-files', '-z', '--cached', '--others', '--exclude-standard'], {encoding: 'utf8'});
    if (files.error) throw files.error;
    if (files.status !== 0) throw new Error(files.stderr.trim());

    const oversized = [];
    for (const file of new Set(files.stdout.split('\0').filter(Boolean))) {
        let metadata;
        try {
            metadata = await lstat(file);
        } catch (error) {
            if (error.code === 'ENOENT') continue;
            throw error;
        }
        if (metadata.isFile() && metadata.size > maximumBytes) oversized.push(`${file} (${metadata.size} bytes)`);
    }

    if (oversized.length) {
        console.error(`Repository files must not exceed 1 MiB (${maximumBytes} bytes):\n${oversized.join('\n')}`);
        process.exitCode = 1;
    } else {
        console.log('Repository files are within the 1 MiB limit.');
    }
} catch (error) {
    console.error(error.message);
    process.exitCode = 1;
}
