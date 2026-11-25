import {spawnSync} from 'node:child_process';
import {lstat} from 'node:fs/promises';
import {posix as path} from 'node:path';

const maximumBytes = 1024 * 1024;
const maximumCSharpFiles = 16;

function isHandwrittenCSharp(file) {
    return file.toLowerCase().endsWith('.cs')
        && !/\.(?:g|g\.i|generated|designer)\.cs$/i.test(file)
        && !file.split('/').includes('Generated');
}

try {
    const files = spawnSync('git', ['ls-files', '-z', '--cached', '--others', '--exclude-standard'], {encoding: 'utf8'});
    if (files.error) throw files.error;
    if (files.status !== 0) throw new Error(files.stderr.trim());

    const oversized = [];
    const counts = new Map();
    for (const file of new Set(files.stdout.split('\0').filter(Boolean))) {
        let metadata;
        try {
            metadata = await lstat(file);
        } catch (error) {
            if (error.code === 'ENOENT') continue;
            throw error;
        }
        if (!metadata.isFile()) continue;
        if (isHandwrittenCSharp(file)) {
            const directory = path.dirname(file);
            counts.set(directory, (counts.get(directory) ?? 0) + 1);
        }
        if (metadata.size > maximumBytes) oversized.push(`${file} (${metadata.size} bytes)`);
    }

    if (oversized.length) {
        console.error(`Repository files must not exceed 1 MiB (${maximumBytes} bytes):\n${oversized.join('\n')}`);
        process.exitCode = 1;
    }
    for (const [directory, count] of [...counts].sort(([a], [b]) => a.localeCompare(b))) {
        if (count <= maximumCSharpFiles) continue;
        console.error(`${directory}: ${count} handwritten C# files (limit ${maximumCSharpFiles}). Consider reviewing the directory's architecture and grouping related files by responsibility.`);
        process.exitCode = 1;
    }
    if (!process.exitCode) console.log('Repository file sizes and directory counts are within their limits.');
} catch (error) {
    console.error(error.message);
    process.exitCode = 1;
}
