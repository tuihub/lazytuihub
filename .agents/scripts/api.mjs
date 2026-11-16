import {spawnSync} from 'node:child_process';
import {glob, mkdir, mkdtemp, readFile, rm, writeFile} from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {parse} from 'yaml';
import {renderApiDocs, updateApiDocs} from './api-docs.mjs';

const root = fileURLToPath(new URL('../../', import.meta.url));
const protoRoot = path.join(root, 'src/LazyTuiHub.Api/Protos');
const project = path.join(root, 'src/LazyTuiHub.Api/LazyTuiHub.Api.csproj');
const mode = process.argv[2];

try {
    if (!['lint', 'generate', 'check'].includes(mode)) throw new Error('Use lint, generate, or check.');
    const files = (await Array.fromAsync(glob('**/*.proto', {cwd: protoRoot}))).sort();
    const common = run('dotnet', ['msbuild', project, '-nologo', '-getProperty:PkgGoogle_Api_CommonProtos'], true);
    const protoc = run('mise', ['where', 'protoc'], true);
    const include = [protoRoot, path.join(common, 'content/protos'), path.join(protoc, 'include')]
        .flatMap(directory => ['-I', directory]);
    if (mode === 'lint') {
        run('api-linter', ['--set-exit-status', ...include, ...files]);
    } else {
        await generate(files, include);
    }
} catch (error) {
    console.error(error.message);
    process.exitCode = 1;
}

async function generate(files, include) {
    const temporary = await mkdtemp(path.join(os.tmpdir(), 'lazytuihub-api-'));
    try {
        const openapi = path.join(temporary, 'openapi.yaml');
        run('protoc', [
            ...include,
            `--openapi_out=${temporary}`,
            '--openapi_opt=version=v1,title=LazyTuiHub API,enum_type=string,default_response=false',
            ...files,
        ]);
        const {clients, ...settings} = JSON.parse(await readFile(path.join(root, '.agents/api/nswag.json'), 'utf8'));
        const document = parse(await readFile(openapi, 'utf8'));
        const sdk = JSON.parse(await readFile(path.join(root, 'global.json'), 'utf8')).sdk.version;
        const configuration = path.join(temporary, 'nswag.json');
        const artifacts = [[openapi, 'src/LazyTuiHub.Api/Generated/openapi.yaml']];
        for (const {service, ...clientSettings} of clients) {
            const client = path.join(temporary, `${clientSettings.className}.g.cs`);
            const selected = {...document, paths: Object.fromEntries(Object.entries(document.paths).filter(([, operations]) =>
                Object.values(operations).some(operation => operation.operationId?.startsWith(`${service}_`))))};
            await writeFile(configuration, JSON.stringify({
                runtime: `Net${Number.parseInt(sdk, 10)}0`,
                documentGenerator: {fromDocument: {json: JSON.stringify(selected)}},
                codeGenerators: {openApiToCSharpClient: {...settings, ...clientSettings, output: client}},
            }));
            run('dotnet', ['nswag', 'run', configuration]);
            artifacts.push([client, `src/LazyTuiHub.Client/Generated/${clientSettings.className}.g.cs`]);
        }
        const pages = await renderApiDocs(document, files);
        for (const [source, destination] of artifacts) {
            const content = (await readFile(source, 'utf8')).replaceAll('\r\n', '\n');
            const output = path.join(root, destination);
            if (mode === 'generate') {
                await mkdir(path.dirname(output), {recursive: true});
                await writeFile(output, content);
            } else if (content !== (await readFile(output, 'utf8')).replaceAll('\r\n', '\n')) {
                throw new Error(`${destination} differs from Proto. Run \`mise run generate:api\`.`);
            }
        }
        await updateApiDocs(path.join(root, 'docs/api'), pages, mode);
        console.log(mode === 'generate' ? 'Generated OpenAPI, HTTP client, and Markdown reference.' : 'API artifacts and Markdown reference match Proto.');
    } finally {
        await rm(temporary, {recursive: true, force: true});
    }
}

function run(command, args, capture = false) {
    const result = spawnSync(command, args, {
        cwd: command === 'protoc' || command === 'api-linter' ? protoRoot : root,
        encoding: 'utf8',
        stdio: capture ? ['ignore', 'pipe', 'inherit'] : 'inherit',
    });
    if (result.error) throw result.error;
    if (result.status !== 0) throw new Error(`${command} failed with status ${result.status}.`);
    return capture ? result.stdout.trim() : undefined;
}
