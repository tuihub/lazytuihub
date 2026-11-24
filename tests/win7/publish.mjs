import {execFileSync} from 'node:child_process';
import {rm, writeFile} from 'node:fs/promises';
import {join} from 'node:path';

const mode = process.argv[2];
if (mode !== 'jit' && mode !== 'aot') throw new Error('Choose jit or aot.');
const payload = join('artifacts', 'win7', mode);

function dotnet(...args) {
    execFileSync('dotnet', args, {stdio: 'inherit'});
}

async function publish(project, directory, ...options) {
    const output = join(payload, directory);
    // Incremental publish can retain a newer DLL from an earlier dependency version.
    await rm(output, {recursive: true, force: true});
    dotnet('publish', project, '-c', 'Release', '-r', 'win-x64', '--self-contained', ...options, '-o', output);
}

if (mode === 'aot') {
    await publish('src/LazyTuiHub.Cli', 'cli', '-p:Win7Aot=true', '--artifacts-path', 'artifacts/win7/aot-build');
    await writeFile(join(payload, 'client-only'), '');
} else {
    dotnet('build', 'src/LazyTuiHub', '-c', 'Release');
    await publish('src/LazyTuiHub.Cli', 'cli');
    await publish('src/LazyTuiHub', 'host');
}
