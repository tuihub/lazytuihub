import {readFile, readdir} from 'node:fs/promises';
import path from 'node:path';
import {isDeepStrictEqual, parseArgs} from 'node:util';
import {parseDocument} from 'yaml';
import {parseFrontmatter} from './frontmatter.mjs';

const help = `Usage: filter-frontmatter.mjs [directory] [--where key=value]... [--json]

Read .md and .markdown files directly in the directory, without recursion.
Files are ordered by filename. No directory argument, a missing directory, or no files is an empty result.

--where key=value  Match a literal top-level field against a parsed YAML value.
                   Repeat for AND. Values may be scalars, arrays, or mappings;
                   dots in keys are literal, and values may contain '='.
                   An empty value means YAML null; use YAML quotes for strings,
                   preserving them in the shell, e.g. --where 'flag="false"'.
                   Invalid YAML values fail the command.
--json             Print one {path, meta} record per file as a JSON array.
--help             Show this help.

Without YAML frontmatter, meta is {}. Without filters, every Markdown file is listed.
A missing field does not match. No filename, language, date, or metadata schema is validated.
Each file is independent. This tool only reads; malformed YAML and read errors fail the command.
`;

try {
    const {values, positionals} = parseArgs({
        options: {
            where: {type: 'string', multiple: true},
            json: {type: 'boolean'},
            help: {type: 'boolean'}
        },
        allowPositionals: true
    });

    if (values.help) {
        process.stdout.write(help);
    } else {
        if (positionals.length > 1) throw new Error('Expected at most one directory.');
        const filters = (values.where || []).map((expression) => {
            const separator = expression.indexOf('=');
            if (separator === -1) throw new Error(`Expected key=value for --where: ${expression}`);
            const key = expression.slice(0, separator);
            const document = parseDocument(expression.slice(separator + 1));
            if (document.errors.length) {
                throw new Error(`Invalid YAML value for --where ${key}: ${document.errors[0].message}`);
            }
            return [key, document.toJSON()];
        });
        const records = positionals.length
            ? await filterFiles(path.resolve(positionals[0]), filters)
            : [];

        if (values.json) {
            process.stdout.write(JSON.stringify(records, null, 2) + '\n');
        } else if (records.length === 0) {
            process.stdout.write('No matching Markdown files.\n');
        } else {
            for (const record of records) {
                process.stdout.write(`${path.relative(process.cwd(), record.path) || record.path}\t`
                    + `${JSON.stringify(record.meta)}\n`);
            }
        }
    }
} catch (error) {
    console.error(`filter-frontmatter: ${error.message}`);
    process.exitCode = 1;
}

async function filterFiles(directory, filters) {
    let entries;
    try {
        entries = await readdir(directory, {withFileTypes: true});
    } catch (error) {
        if (error.code === 'ENOENT') return [];
        throw new Error(`Cannot read directory ${directory}: ${error.message}`, {cause: error});
    }

    entries.sort((left, right) => left.name < right.name ? -1 : left.name > right.name ? 1 : 0);
    const records = [];
    for (const entry of entries) {
        if ((!entry.isFile() && !entry.isSymbolicLink()) || !/\.(md|markdown)$/i.test(entry.name)) continue;
        const file = path.join(directory, entry.name);
        let meta;
        try {
            ({meta} = parseFrontmatter(await readFile(file, 'utf8')));
        } catch (error) {
            throw new Error(`Cannot read frontmatter from ${file}: ${error.message}`, {cause: error});
        }
        if (filters.every(([key, value]) => meta !== null && typeof meta === 'object'
            && !Array.isArray(meta) && Object.hasOwn(meta, key)
            && isDeepStrictEqual(meta[key], value))) {
            records.push({path: file, meta});
        }
    }
    return records;
}
