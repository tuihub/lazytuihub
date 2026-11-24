import {execFileSync} from 'node:child_process';
import {mkdir, mkdtempDisposable, rename, writeFile} from 'node:fs/promises';
import {join, resolve} from 'node:path';

const host = 'src/LazyTuiHub/LazyTuiHub.csproj';
const client = 'src/LazyTuiHub.Cli/LazyTuiHub.Cli.csproj';

function dotnet(...args) {
    execFileSync('dotnet', args, {stdio: 'inherit'});
}

export function publishHost(rid, directory) {
    // The host ships managed plugins from their portable Release builds.
    dotnet('build', host, '-c', 'Release');
    dotnet('publish', host, '-c', 'Release', '-r', rid, '--self-contained', '-o', directory);
}

async function release() {
    const [requested, ...extra] = process.argv.slice(2);
    const rid = requested ?? execFileSync('dotnet', ['msbuild', host, '-getProperty:NETCoreSdkPortableRuntimeIdentifier'], {encoding: 'utf8'}).trim();
    if (extra.length || !/^[a-z0-9][a-z0-9.-]*$/i.test(rid)) throw new Error('Usage: mise run release [runtime-identifier]');

    const root = resolve('.local/release');
    await mkdir(root, {recursive: true});
    await using stage = await mkdtempDisposable(join(root, '.publish-'));
    const output = join(stage.path, 'release');
    const mac = rid.startsWith('osx-');
    const contents = join(output, 'LazyTuiHub.app', 'Contents');
    const hostOutput = mac ? join(contents, 'MacOS') : join(output, 'host');

    publishHost(rid, hostOutput);
    dotnet('publish', client, '-c', 'Release', '-r', rid, '--self-contained', '-o', join(output, 'cli'));
    if (mac) {
        const version = execFileSync('dotnet', ['msbuild', host, '-p:Configuration=Release', '-target:GetAssemblyVersion', '-getProperty:AssemblyVersion'], {encoding: 'utf8'}).trim().split('.').slice(0, 3).join('.');
        await writeFile(join(contents, 'Info.plist'), `<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
  <key>CFBundleIdentifier</key><string>dev.tuihub.lazytuihub</string>
  <key>CFBundleName</key><string>LazyTuiHub</string>
  <key>CFBundleExecutable</key><string>LazyTuiHub</string>
  <key>CFBundlePackageType</key><string>APPL</string>
  <key>CFBundleShortVersionString</key><string>${version}</string>
  <key>CFBundleVersion</key><string>${version}</string>
  <key>NSHighResolutionCapable</key><true/>
</dict></plist>
`);
    }

    const destination = join(root, rid);
    const previous = join(stage.path, 'previous');
    let replaced = false;
    try {
        await rename(destination, previous);
        replaced = true;
    } catch (error) {
        if (error.code !== 'ENOENT') throw error;
    }
    try {
        await rename(output, destination);
    } catch (error) {
        if (replaced) await rename(previous, destination);
        throw error;
    }
    console.log(destination);
}

if (import.meta.main) await release();
