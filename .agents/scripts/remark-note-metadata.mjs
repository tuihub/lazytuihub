import {readFile} from 'node:fs/promises';
import path from 'node:path';
import Ajv from 'ajv';
import {parseDocument} from 'yaml';
import {parseFrontmatter} from './frontmatter.mjs';

const schema = parseDocument(await readFile(new URL('../schemas/note-metadata.yaml', import.meta.url), 'utf8'));
if (schema.errors.length) throw schema.errors[0];
const ajv = new Ajv({allErrors: true});
const validateMetadata = ajv.compile(schema.toJSON());
const coverageStates = new Set(['superseded', 'archived']);

export default function remarkNoteMetadata() {
    return async function checkNoteMetadata(tree, file) {
        if (!file.path) return;

        const filePath = path.resolve(file.cwd, file.path);
        const notesDirectory = path.resolve(file.cwd, '.agents/notes');
        if (path.dirname(filePath) !== notesDirectory || !filePath.endsWith('.md')) return;

        if (!parseNoteName(path.basename(filePath))) {
            report(file, 'filename', 'Name notes YYYY-MM-DD-topic.zh.md or YYYY-MM-DD-topic.en.md with a valid date.', tree);
        }

        let frontmatter;
        try {
            frontmatter = parseFrontmatter(String(file), tree);
        } catch (error) {
            report(file, 'frontmatter', error.message, tree);
            return;
        }

        if (!frontmatter.range) {
            report(file, 'frontmatter', 'Notes need YAML frontmatter matching the Note metadata schema.', tree);
            return;
        }

        const metadata = frontmatter.meta;
        const place = tree.children[0] || tree;
        if (!validateMetadata(metadata)) {
            for (const error of validateMetadata.errors) {
                const property = error.keyword === 'additionalProperties' ? ` (${error.params.additionalProperty})` : '';
                report(file, 'schema', ajv.errorsText([error], {dataVar: 'metadata'}) + property, place);
            }
            return;
        }

        if (coverageStates.has(metadata.status)) {
            const problem = await replacementProblem(filePath, metadata);
            if (problem) report(file, 'superseded-by', problem, place);
        }
    };
}

export function parseNoteName(name) {
    const match = /^(\d{4}-\d{2}-\d{2})-(.+)\.(zh|en)\.md$/u.exec(name);
    if (!match || !match[2].trim()) return undefined;

    const date = new Date(`${match[1]}T00:00:00Z`);
    if (Number.isNaN(date.getTime()) || date.toISOString().slice(0, 10) !== match[1]) return undefined;

    return {date: match[1], topic: match[2], language: match[3]};
}

export function isNotePath(filePath, root = process.cwd()) {
    const absolutePath = path.resolve(root, filePath);
    return path.dirname(absolutePath) === path.resolve(root, '.agents/notes')
        && Boolean(parseNoteName(path.basename(absolutePath)));
}

async function replacementProblem(filePath, metadata) {
    const metadataByPath = new Map([[filePath, metadata]]);
    const active = new Set();
    const checked = new Set();
    return visit(filePath);

    async function visit(currentPath) {
        if (checked.has(currentPath)) return;
        const current = metadataByPath.get(currentPath);
        active.add(currentPath);
        try {
            for (const name of current.superseded_by) {
                if (!parseNoteName(name)) {
                    return `superseded_by in ${path.basename(currentPath)} must name notes in the same directory.`;
                }

                const replacementPath = path.join(path.dirname(filePath), name);
                if (replacementPath === currentPath) return `${name} cannot supersede itself.`;
                if (active.has(replacementPath)) return 'The superseded_by graph contains a cycle.';

                let replacement = metadataByPath.get(replacementPath);
                if (!replacement) {
                    let frontmatter;
                    try {
                        frontmatter = parseFrontmatter(await readFile(replacementPath, 'utf8'));
                    } catch (error) {
                        return `Cannot read replacement note ${name}: ${error.message}`;
                    }
                    if (!frontmatter.range || !validateMetadata(frontmatter.meta)) {
                        return `Replacement note ${name} must match the Note metadata schema.`;
                    }
                    replacement = frontmatter.meta;
                    metadataByPath.set(replacementPath, replacement);
                }
                if (current.status === 'superseded' && !['implemented', 'archived'].includes(replacement.status)) {
                    return `Replacement note ${name} partially covers ${path.basename(currentPath)} and must be implemented or archived.`;
                }
                if (replacement.status !== 'implemented' && !coverageStates.has(replacement.status)) {
                    return `Replacement note ${name} must be implemented, superseded, or archived.`;
                }
                if (coverageStates.has(replacement.status)) {
                    const problem = await visit(replacementPath);
                    if (problem) return problem;
                }
            }
            checked.add(currentPath);
        } finally {
            active.delete(currentPath);
        }
    }
}

function report(file, ruleId, reason, node) {
    file.message(reason, {place: node.position, source: 'note-metadata', ruleId});
}
