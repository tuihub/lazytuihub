import { spawnSync } from 'node:child_process';
import { cpSync, mkdirSync, realpathSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = dirname(fileURLToPath(import.meta.url));
const [command = 'check', requested = 'both', ...extra] = process.argv.slice(2);
const configurations = {
  avalonia: { project: 'Avalonia', tfm: 'net11.0', assembly: 'UiComparison.Avalonia' },
  uno: { project: 'Uno', tfm: 'net11.0-desktop', assembly: 'UiComparison.Uno' },
};
const selected = requested === 'both' ? Object.keys(configurations) : [requested];
if (!['check', 'run', 'bundle'].includes(command) || selected.some(name => !configurations[name])) {
  throw new Error('Usage: ui-comparison.mjs check|run|bundle [both|avalonia|uno] [app arguments]');
}
if (command === 'run' && selected.length !== 1) throw new Error('Choose one framework to run.');

function exec(executable, args, options = {}) {
  const result = spawnSync(executable, args, { cwd: root, stdio: 'inherit', ...options });
  if (result.error) throw result.error;
  if (result.status !== 0) process.exit(result.status ?? 1);
  return result;
}

const artifacts = join(root, 'artifacts');
mkdirSync(artifacts, { recursive: true });
for (const name of selected) {
  const config = configurations[name];
  const project = join(root, config.project, config.project + '.csproj');
  exec('dotnet', ['build', project, '-c', 'Release', '--disable-build-servers', '-m:1', '-nr:false', '-warnaserror']);
  const output = join(root, config.project, 'bin', 'Release', config.tfm);
  const dll = join(output, config.assembly + '.dll');
  const env = { ...process.env, UI_COMPARISON_ARTIFACTS: join(artifacts, name) };
  if (command === 'check') {
    exec('dotnet', [dll, '--verify'], { env });
    if (name === 'uno') exec('dotnet', [dll, '--verify', '--cupertino'], { env: { ...env, UI_COMPARISON_ARTIFACTS: join(artifacts, 'uno-cupertino') } });
    exec('dotnet', [dll, '--verify', '--without-plugin'], { env: { ...env, UI_COMPARISON_ARTIFACTS: join(artifacts, name + '-disabled') } });
  } else if (command === 'run') {
    exec('dotnet', [dll, ...extra.filter(value => value !== '--')], { env });
  } else {
    if (process.platform !== 'darwin') throw new Error('The local .app wrapper is for macOS. Use run on other systems.');
    const cupertino = name === 'uno' && extra.includes('--cupertino');
    const variant = cupertino ? '-Cupertino' : '';
    const instance = name + (cupertino ? '-cupertino' : '');
    const app = join(artifacts, 'LazyTuiHub-' + config.project + variant + '.app');
    const macos = join(app, 'Contents', 'MacOS');
    mkdirSync(macos, { recursive: true });
    cpSync(output, macos, { recursive: true });
    const which = spawnSync('mise', ['which', 'dotnet'], { encoding: 'utf8' });
    if (which.status !== 0) throw new Error(which.stderr || 'mise could not locate dotnet.');
    const dotnetRoot = dirname(realpathSync(which.stdout.trim()));
    const quote = value => "'" + value.replaceAll("'", "'\\''") + "'";
    const launcher = '#!/bin/sh\nexport DOTNET_ROOT=' + quote(dotnetRoot)
      + '\nexport UI_COMPARISON_ARTIFACTS=' + quote(join(artifacts, instance + '-native'))
      + '\nexec ' + quote(join(macos, config.assembly)) + ' ' + extra.filter(value => value !== '--').map(quote).join(' ') + ' "$@" > ' + quote(join(artifacts, instance + '-native.log')) + ' 2>&1\n';
    writeFileSync(join(macos, 'launch'), launcher, { mode: 0o755 });
    writeFileSync(join(app, 'Contents', 'Info.plist'), '<?xml version="1.0" encoding="UTF-8"?>\n'
      + '<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">\n'
      + '<plist version="1.0"><dict>'
      + '<key>CFBundleExecutable</key><string>launch</string>'
      + '<key>CFBundleIdentifier</key><string>dev.lazytuihub.prototype.' + name + (cupertino ? '.cupertino' : '') + '</string>'
      + '<key>CFBundleName</key><string>LazyTuiHub ' + config.project + variant + '</string>'
      + '<key>CFBundlePackageType</key><string>APPL</string>'
      + '<key>NSHighResolutionCapable</key><true/>'
      + '</dict></plist>\n');
    console.log('Local development app: ' + app);
  }
}
