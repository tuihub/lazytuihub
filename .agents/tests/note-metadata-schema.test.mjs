import assert from 'node:assert/strict';
import {readFile} from 'node:fs/promises';
import {test} from 'node:test';
import Ajv from 'ajv';
import {parseDocument} from 'yaml';

const document = parseDocument(await readFile(new URL('../schemas/note-metadata.yaml', import.meta.url), 'utf8'));
assert.deepEqual(document.errors, []);
const schema = document.toJSON();
const validate = new Ajv().compile(schema);
const category = schema.properties.category.enum[0];
const replacement = '2026-11-15-replacement.en.md';

test('category choices come only from the editable schema whitelist', () => {
    for (const value of schema.properties.category.enum) {
        assert.ok(validate({status: 'proposed', category: value}));
    }
    let newCategory = 'additional-category';
    while (schema.properties.category.enum.includes(newCategory)) newCategory += '-new';
    assert.equal(validate({status: 'proposed', category: newCategory}), false);

    const extended = structuredClone(schema);
    extended.properties.category.enum.push(newCategory);
    assert.ok(new Ajv().compile(extended)({status: 'proposed', category: newCategory}));
});

test('partial and complete coverage states require one through twelve replacement filenames', () => {
    for (const status of ['superseded', 'archived']) {
        assert.equal(validate({status, category}), false);
        assert.ok(validate({status, category, superseded_by: [replacement]}));
        const twelve = Array.from({length: 12}, (_, index) => `2026-11-15-replacement-${index}.en.md`);
        assert.ok(validate({status, category, superseded_by: twelve}));
        assert.equal(validate({status, category, superseded_by: [...twelve, replacement]}), false);
        assert.equal(validate({status, category, superseded_by: []}), false);
        assert.equal(validate({status, category, superseded_by: replacement}), false);
        assert.equal(validate({status, category, superseded_by: ['../replacement.en.md']}), false);
        assert.ok(validate({status, category, superseded_by: [replacement, replacement]}));
    }
});

test('other states retain optional replacement metadata', () => {
    for (const status of ['proposed', 'implemented', 'rejected']) {
        assert.ok(validate({status, category}));
        assert.ok(validate({status, category, superseded_by: [replacement]}));
    }
});
