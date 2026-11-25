import {lstat, mkdir, readFile, readdir, rm, writeFile} from 'node:fs/promises';
import path from 'node:path';
import {createOpenApiMarkdownRenderer} from '@scalar/openapi-to-markdown';

const methods = new Set(['get', 'put', 'post', 'delete', 'options', 'head', 'patch', 'trace']);

export async function renderApiDocs(document, sources) {
    const renderer = await createOpenApiMarkdownRenderer(withStableDateExamples(document));
    const pages = new Map();
    const operations = [];
    const models = Object.keys(document.components?.schemas || {}).sort();
    const references = {
        mode: 'linked',
        resolveUrl: ({name}) => `../models/${name}.md`,
    };

    for (const url of Object.keys(document.paths || {}).sort()) {
        for (const method of Object.keys(document.paths[url]).sort()) {
            if (!methods.has(method)) continue;
            const operation = document.paths[url][method];
            const filename = `operations/${operation.operationId}.md`;
            if (pages.has(filename)) throw new Error(`Duplicate operation ID: ${operation.operationId}`);
            pages.set(filename, await renderer.render({
                operation: {path: url, method},
                schemaReferences: references,
            }));
            operations.push(`- [${method.toUpperCase()} ${url}](${filename})`);
        }
    }
    for (const model of models) {
        pages.set(`models/${model}.md`, await renderer.render({model, schemaReferences: references}));
    }

    pages.set('index.md', [
        `# ${document.info.title}`,
        '> Generated from Proto declarations. Run `mise run generate:api` to update; do not edit `docs/api`.',
        document.info.description,
        `**API version:** \`${document.info.version}\``,
        '## Operations',
        operations.join('\n'),
        '## Models',
        models.map(name => `- [${name}](models/${name}.md)`).join('\n'),
        '## Source declarations',
        sources.map(source => `- [${source.name}](../../${source.path})`).join('\n'),
    ].filter(Boolean).join('\n\n') + '\n');

    for (const [filename, content] of pages) {
        // Scalar bounds schema expansion; never publish a reference with omitted fields.
        if (/\[(?:Maximum schema depth reached|Schema output truncated)\]/.test(content)) {
            throw new Error(`Incomplete schema documentation in ${filename}; split the Proto model into named messages.`);
        }
        pages.set(filename, content.trimEnd() + '\n');
    }
    return pages;
}

function withStableDateExamples(document) {
    // Scalar otherwise inserts the wall clock into synthesized date examples.
    // Keep supplied examples/defaults opaque and change only the renderer's copy.
    const copy = structuredClone(document);
    const opaque = new Set(['example', 'examples', 'default', 'const', 'enum']);
    const schemaMaps = new Set(['schemas', 'properties', 'patternProperties', '$defs', 'definitions']);
    const dates = {'date-time': '2000-01-01T00:00:00.000Z', date: '2000-01-01', time: '00:00:00.000Z'};
    function visit(value) {
        if (value === null || typeof value !== 'object') return;
        if (value.type === 'string' && Object.hasOwn(dates, value.format) &&
            ![...opaque].some(key => Object.hasOwn(value, key))) {
            value.example = dates[value.format];
        }
        for (const [key, child] of Object.entries(value)) {
            if (schemaMaps.has(key) && child && typeof child === 'object') Object.values(child).forEach(visit);
            else if (!opaque.has(key) && !key.startsWith('x-')) visit(child);
        }
    }
    visit(copy);
    return copy;
}

export async function updateApiDocs(directory, pages, mode) {
    let entries;
    try {
        if ((await lstat(directory)).isSymbolicLink()) {
            throw new Error('The generated API documentation directory must not be a symlink.');
        }
        entries = await readdir(directory, {recursive: true, withFileTypes: true});
    } catch (error) {
        if (error.code !== 'ENOENT') throw error;
        entries = [];
    }
    const existing = entries.filter(entry => !entry.isDirectory()).map(entry => {
        if (entry.isSymbolicLink()) throw new Error(`Unexpected symlink in generated API documentation: ${entry.name}`);
        return path.relative(directory, path.join(entry.parentPath, entry.name)).split(path.sep).join('/');
    }).sort();
    const stale = existing.filter(filename => !pages.has(filename));
    if (mode === 'check') {
        if (stale.length) throw new Error(`Unexpected generated API documentation: ${stale.join(', ')}. Run \`mise run generate:api\`.`);
        for (const [filename, content] of pages) {
            const actual = existing.includes(filename) ? await readFile(path.join(directory, filename), 'utf8') : '';
            if (actual.replaceAll('\r\n', '\n') !== content) {
                throw new Error(`docs/api/${filename} differs from Proto. Run \`mise run generate:api\`.`);
            }
        }
    } else {
        for (const filename of stale) await rm(path.join(directory, filename));
        for (const [filename, content] of pages) {
            const output = path.join(directory, filename);
            await mkdir(path.dirname(output), {recursive: true});
            await writeFile(output, content);
        }
    }
}
