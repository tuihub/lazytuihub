import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {copyFile, mkdtemp, mkdir, readFile, realpath, rm, writeFile} from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import {test} from 'node:test';
import {fileURLToPath} from 'node:url';

const root = fileURLToPath(new URL('../../', import.meta.url));
const props = await readFile(path.join(root, 'Directory.Build.props'), 'utf8');
const framework = props.match(/<TargetFramework>([^<]+)<\/TargetFramework>/)[1];
const editorconfig = await readFile(path.join(root, '.editorconfig'), 'utf8');

function run(command, args, cwd = root) {
    const result = spawnSync(command, args, {
        cwd,
        encoding: 'utf8',
        env: {...process.env, DOTNET_CLI_USE_MSBUILD_SERVER: '0', MSBUILDUSESERVER: '0'}
    });
    assert.ifError(result.error);
    return {...result, output: result.stdout + result.stderr};
}

async function directory(t) {
    const temporary = await realpath(await mkdtemp(path.join(os.tmpdir(), 'csharp lint ')));
    t.after(() => rm(temporary, {recursive: true, force: true}));
    await copyFile(path.join(root, 'global.json'), path.join(temporary, 'global.json'));
    await writeFile(path.join(temporary, '.editorconfig'), editorconfig);
    await writeFile(path.join(temporary, 'NuGet.Config'), '<configuration><packageSources><clear /></packageSources></configuration>');
    return temporary;
}

async function fixture(t, isTest = false) {
    const cwd = await directory(t);
    const file = path.join(cwd, 'Fixture.csproj');
    await writeFile(file, `<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>${framework}</TargetFramework><ImplicitUsings>enable</ImplicitUsings><IsTestProject>${isTest}</IsTestProject></PropertyGroup></Project>`);
    return {
        cwd,
        file,
        args: [
            file,
            `-p:DirectoryBuildPropsPath=${path.join(root, 'Directory.Build.props')}`,
            `-p:DirectoryBuildTargetsPath=${path.join(root, 'Directory.Build.targets')}`
        ]
    };
}

function build(fixture, ...args) {
    return run('dotnet', ['build', ...fixture.args, '--nologo', '--verbosity', 'quiet', '--disable-build-servers', ...args], fixture.cwd);
}

const defects = `namespace Fixture;
public class Checks : IDisposable
{
    private readonly Stream resource = File.OpenRead("fixture");
    public void Dispose() { GC.SuppressFinalize(this); }
    public static int DeadCondition(int input)
    {
        if (input != 0) return 1;
        if (input != 0) return 2;
        return 0;
    }
    public static int Leak()
    {
        var stream = File.OpenRead("fixture");
        return stream.ReadByte();
    }
    public static Task Cancel(CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        return Task.Delay(42);
    }
    public static Task PrematureDisposal()
    {
        using var stream = new MemoryStream();
        return ConsumeAsync(stream);
    }
    private static async Task ConsumeAsync(Stream stream)
    {
        await Task.Delay(42);
        await stream.CopyToAsync(Stream.Null);
    }
}`;

test('product defects fail the gate while negative tests inherit no product analyzers', async (t) => {
    const product = await fixture(t);
    await writeFile(path.join(product.cwd, 'Checks.cs'), defects);
    const failure = build(product, '-warnaserror');
    assert.notEqual(failure.status, 0, failure.output);
    for (const id of ['CA1508', 'CA2000', 'CA2213', 'CA2025', 'MA0040']) assert.match(failure.output, new RegExp(`\\b${id}\\b`), failure.output);

    const tests = await fixture(t, true);
    await writeFile(path.join(tests.cwd, 'Checks.cs'), defects);
    const pass = build(tests, '-warnaserror');
    assert.equal(pass.status, 0, pass.output);
    const configuration = run('dotnet', ['msbuild', ...tests.args, '-getProperty:EnableNETAnalyzers,EnforceCodeStyleInBuild', '-getItem:PackageReference'], tests.cwd);
    assert.equal(configuration.status, 0, configuration.output);
    const evaluated = JSON.parse(configuration.stdout);
    assert.equal(evaluated.Properties.EnableNETAnalyzers, 'false');
    assert.equal(evaluated.Properties.EnforceCodeStyleInBuild, 'false');
    assert.deepEqual(evaluated.Items.PackageReference, []);

    await writeFile(path.join(tests.cwd, 'Nullable.cs'), 'namespace Fixture; public static class Negative { public static int Read(string? value) => value.Length; }');
    const nullable = build(tests, '-warnaserror');
    assert.notEqual(nullable.status, 0, nullable.output);
    assert.match(nullable.output, /CS8602/, nullable.output);
});

test('owned resources and awaited operations are accepted', async (t) => {
    const product = await fixture(t);
    await writeFile(path.join(product.cwd, 'Owned.cs'), `namespace Fixture;
public static class Owned
{
    public static StreamReader TransferOwnership() => new(File.OpenRead("fixture"));
    public static async Task UseAsync(CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream();
        await Task.Delay(42, cancellationToken);
        await stream.CopyToAsync(Stream.Null, cancellationToken);
    }
}`);
    const result = build(product, '-p:WarningsAsErrors=CA2000%3BCA2025%3BMA0040');
    assert.equal(result.status, 0, result.output);
});

test('configured complexity and method-length boundaries are enforced', async (t) => {
    const product = await fixture(t);
    const source = path.join(product.cwd, 'Thresholds.cs');
    const branches = (count) => Array.from({length: count}, (_, i) => `if (input == ${i}) GC.KeepAlive(input);`).join('\n');
    const cases = [
        ['S3776', (count) => `public static void Method(int input) { ${branches(count)} }`, 20],
        ['S3776', (count) => `public static int Property { get { int input = Environment.TickCount; ${branches(count)} return input; } }`, 20],
        ['S1541', (count) => `public static void Method(int input) { ${branches(count - 1)} }`, 30],
        ['MA0051', (count) => `public static void Method(object input) {\n${'GC.KeepAlive(input);\n'.repeat(count)}}`, 50],
        ['MA0051', (count) => `public static void Method() {\n${'// Arrange a step.\n'.repeat(count - 1)}}`, 100]
    ];
    for (const [id, body, limit] of cases) {
        await writeFile(source, `namespace Fixture; public static class Thresholds { ${body(limit)} }`);
        const atLimit = build(product, `-p:WarningsAsErrors=${id}`);
        assert.equal(atLimit.status, 0, atLimit.output);
        await writeFile(source, `namespace Fixture; public static class Thresholds { ${body(limit + 1)} }`);
        const overLimit = build(product, `-p:WarningsAsErrors=${id}`);
        assert.notEqual(overLimit.status, 0, overLimit.output);
        assert.match(overLimit.output, new RegExp(`\\b${id}\\b`), overLimit.output);
    }
});

test('format checking fails without rewriting and format:code can repair the source', async (t) => {
    const cwd = await directory(t);
    const file = path.join(cwd, 'Checks.cs');
    const unformatted = 'namespace Fixture{public class Checks{public int Value{get;set;}}}';
    await writeFile(file, unformatted);
    assert.notEqual(run('dotnet', ['csharpier', 'check', file]).status, 0);
    assert.equal(await readFile(file, 'utf8'), unformatted);
    const formatted = run('dotnet', ['csharpier', 'format', file, '--no-cache']);
    assert.equal(formatted.status, 0, formatted.output);
    const checked = run('dotnet', ['csharpier', 'check', file]);
    assert.equal(checked.status, 0, checked.output);
});

test('renamed product clones fail while tests and generated files are excluded', async (t) => {
    const cwd = await directory(t);
    await mkdir(path.join(cwd, 'src'));
    await mkdir(path.join(cwd, 'tests'));
    await writeFile(path.join(cwd, '.jscpd.json'), await readFile(path.join(root, '.jscpd.json')));
    const code = `namespace Fixture;
public static class Original
{
    public static int Calculate(int input)
    {
        int total = input + 2;
        if (input < 0) { total = Math.Abs(input); }
        var bytes = BitConverter.GetBytes(total);
        foreach (var item in bytes) { total ^= item; }
        for (int index = 0; index < bytes.Length; index++) { total += bytes[index] * index; }
        switch (total % 3)
        {
            case 0: total++; break;
            case 1: total -= 3; break;
            default: total *= 2; break;
        }
        try { total = checked(total * 11); }
        catch (OverflowException) { total = 0; }
        return total;
    }
}`;
    const renamed = code.replaceAll('Original', 'Renamed').replaceAll('input', 'argument').replaceAll('total', 'result').replaceAll('11', '13');
    await writeFile(path.join(cwd, 'src/Original.cs'), code);
    await writeFile(path.join(cwd, 'tests/Renamed.cs'), renamed);
    await writeFile(path.join(cwd, 'src/Renamed.g.cs'), renamed);
    const command = [path.join(root, 'node_modules/jscpd/run-jscpd.js'), '--no-colors'];
    const excluded = run(process.execPath, command, cwd);
    assert.equal(excluded.status, 0, excluded.output);
    await writeFile(path.join(cwd, 'src/Renamed.cs'), renamed);
    const duplicated = run(process.execPath, command, cwd);
    assert.notEqual(duplicated.status, 0, duplicated.output);
    assert.match(duplicated.output, /Original\.cs/, duplicated.output);
    assert.match(duplicated.output, /Renamed\.cs/, duplicated.output);
});
