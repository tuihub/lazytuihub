import assert from 'node:assert/strict';
import {mkdtemp, readFile, rm, writeFile} from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import {test} from 'node:test';
import {renderApiDocs, updateApiDocs} from '../scripts/api-docs.mjs';

test('reference pages retain authored examples and link recursive message schemas', async () => {
    const document = {
        openapi: '3.0.3',
        info: {title: 'Example API', version: 'v1'},
        paths: {'/v1/widgets/{widget}': {get: {
            operationId: 'GetWidget',
            description: 'Returns a widget.\n\nExample response:\n\n```json\n{"name":"widgets/example"}\n```',
            parameters: [{name: 'widget', in: 'path', required: true, schema: {type: 'string'}}],
            responses: {'200': {
                description: 'The widget.',
                content: {'application/json': {schema: {$ref: '#/components/schemas/Widget'}}},
            }},
        }}},
        components: {schemas: {Widget: {
            type: 'object',
            properties: {
                name: {type: 'string', description: 'Output only. The resource name.', readOnly: true},
                parent: {$ref: '#/components/schemas/Widget'},
            },
        }}},
    };
    const pages = await renderApiDocs(document, ['example.proto']);
    assert.match(pages.get('index.md'), /operations\/GetWidget\.md/);
    assert.match(pages.get('index.md'), /Protos\/example\.proto/);
    const operation = pages.get('operations/GetWidget.md');
    assert.match(operation, /```json\n\{"name":"widgets\/example"\}\n```/);
    assert.match(operation, /Output only\. The resource name\./);
    assert.match(operation, /string.*, readOnly/);
    assert.match(operation, /\[Widget\]\(\.\.\/models\/Widget\.md\)/);
    assert.match(pages.get('models/Widget.md'), /\[Widget\]\(\.\.\/models\/Widget\.md\)/);
    assert.doesNotMatch([...pages.values()].join('\n'), /\[Circular Reference\]/);
});

test('upstream schema truncation fails generation instead of publishing incomplete fields', async () => {
    let schema = {type: 'string', description: 'Deepest field.'};
    for (let depth = 0; depth < 70; depth++) schema = {type: 'object', properties: {child: schema}};
    await assert.rejects(renderApiDocs({
        openapi: '3.0.3', info: {title: 'Deep API', version: 'v1'},
        paths: {}, components: {schemas: {Deep: schema}},
    }, []), /Incomplete schema documentation/);
});

test('checks detect edited, missing, extra, and obsolete pages without rewriting', async (t) => {
    const temporary = await mkdtemp(path.join(os.tmpdir(), 'api-docs-'));
    t.after(() => rm(temporary, {recursive: true, force: true}));
    const directory = path.join(temporary, 'api');
    const pages = new Map([['index.md', '# API\n'], ['operations/Example.md', '# Example\n']]);
    await assert.rejects(updateApiDocs(directory, pages, 'check'), /differs from Proto/);
    await updateApiDocs(directory, pages, 'generate');
    await updateApiDocs(directory, pages, 'check');

    const operation = path.join(directory, 'operations/Example.md');
    await writeFile(operation, '# Manual edit\n');
    await assert.rejects(updateApiDocs(directory, pages, 'check'), /differs from Proto/);
    assert.equal(await readFile(operation, 'utf8'), '# Manual edit\n');
    await rm(operation);
    await assert.rejects(updateApiDocs(directory, pages, 'check'), /differs from Proto/);
    await updateApiDocs(directory, pages, 'generate');

    await writeFile(path.join(directory, '.handwritten.md'), '# Unexpected\n');
    await assert.rejects(updateApiDocs(directory, pages, 'check'), /Unexpected generated API documentation/);
    assert.equal(await readFile(path.join(directory, '.handwritten.md'), 'utf8'), '# Unexpected\n');
    const reduced = new Map([['index.md', '# Updated API\n']]);
    await updateApiDocs(directory, reduced, 'generate');
    await updateApiDocs(directory, reduced, 'check');
    await assert.rejects(readFile(operation), {code: 'ENOENT'});
    await assert.rejects(readFile(path.join(directory, '.handwritten.md')), {code: 'ENOENT'});
});
