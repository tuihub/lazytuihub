import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {mkdtemp, mkdir, rm, writeFile} from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import {test} from 'node:test';
import {fileURLToPath} from 'node:url';

const script = fileURLToPath(new URL('../scripts/check-file-sizes.mjs', import.meta.url));

function run(command, args, cwd) {
    const result = spawnSync(command, args, {cwd, encoding: 'utf8'});
    assert.ifError(result.error);
    return result;
}

test('tracked and new files over 1 MiB fail, while the boundary and ignored outputs are accepted', async (t) => {
    const cwd = await mkdtemp(path.join(os.tmpdir(), 'file size checks '));
    t.after(() => rm(cwd, {recursive: true, force: true}));
    assert.equal(run('git', ['init', '--quiet'], cwd).status, 0);
    await writeFile(path.join(cwd, '.gitignore'), 'output/\ntracked.bin\n');
    await mkdir(path.join(cwd, 'output'));
    await writeFile(path.join(cwd, 'output/generated.bin'), Buffer.alloc(1024 * 1024 + 1));
    await writeFile(path.join(cwd, 'boundary.bin'), Buffer.alloc(1024 * 1024));
    assert.equal(run(process.execPath, [script], cwd).status, 0);

    await writeFile(path.join(cwd, 'new file.bin'), Buffer.alloc(1024 * 1024 + 1));
    const newFile = run(process.execPath, [script], cwd);
    assert.equal(newFile.status, 1);
    assert.match(newFile.stderr, /new file\.bin \(1048577 bytes\)/);
    await rm(path.join(cwd, 'new file.bin'));

    await writeFile(path.join(cwd, 'tracked.bin'), Buffer.alloc(1024 * 1024 + 1));
    assert.equal(run('git', ['add', '--force', 'tracked.bin'], cwd).status, 0);
    const tracked = run(process.execPath, [script], cwd);
    assert.equal(tracked.status, 1);
    assert.match(tracked.stderr, /tracked\.bin/);

    await rm(path.join(cwd, 'tracked.bin'));
    assert.equal(run(process.execPath, [script], cwd).status, 0);
});

test('the 17th handwritten C# file in one directory suggests reviewing its architecture', async (t) => {
    const cwd = await mkdtemp(path.join(os.tmpdir(), 'directory counts '));
    t.after(() => rm(cwd, {recursive: true, force: true}));
    assert.equal(run('git', ['init', '--quiet'], cwd).status, 0);
    const folder = path.join(cwd, 'tests');
    await mkdir(path.join(folder, 'Nested'), {recursive: true});
    await mkdir(path.join(folder, 'Generated'));
    const files = [
        ...Array.from({length: 14}, (_, n) => `Type${n}.cs`),
        'Editor.axaml.cs', 'Type0.Metadata.cs', 'Editor.axaml', 'Nested/Child.cs',
        'Auto.g.cs', 'Auto.g.i.cs', 'Auto.Generated.cs', 'Auto.Designer.cs', 'Generated/Client.cs',
    ];
    await Promise.all(files.map(file => writeFile(path.join(folder, file), '')));
    assert.equal(run('git', ['add', 'tests'], cwd).status, 0);
    assert.equal(run(process.execPath, [script], cwd).status, 0);

    await writeFile(path.join(folder, 'Extra.cs'), '');
    const result = run(process.execPath, [script], cwd);
    assert.equal(result.status, 1);
    assert.match(result.stderr, /tests: 17 handwritten C# files \(limit 16\).*architecture/);
    await rm(path.join(folder, 'Type1.cs'));
    assert.equal(run(process.execPath, [script], cwd).status, 0);
});
