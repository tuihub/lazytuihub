import {readFile} from 'node:fs/promises';
import path from 'node:path';
import ignore from 'ignore';

const checkedFileSets = new WeakMap();

export default function remarkBilingualStructure(options, fileSet) {
    const processor = this;
    const unpairedFiles = new Set(options?.unpairedFiles || []);
    let checkedPairs;

    if (fileSet) {
        checkedPairs = checkedFileSets.get(fileSet);
        if (!checkedPairs) {
            checkedPairs = new Set();
            checkedFileSets.set(fileSet, checkedPairs);
        }
    }

    return async function checkBilingualStructure(tree, file) {
        if (!file.path) {
            report(file, 'filename', 'Bilingual documents need a .zh.md or .en.md file path.', tree);
            return;
        }

        const filePath = path.resolve(file.cwd, file.path);
        let ignoreFile;
        try {
            ignoreFile = await findIgnoreFile(path.dirname(filePath));
        } catch (error) {
            report(file, 'ignore', `Cannot read .remarkignore: ${error.message}`, tree);
            return;
        }

        const isIgnored = (candidate) => ignoreFile
            ? ignoreFile.matcher.ignores(path.relative(ignoreFile.directory, candidate))
            : false;

        if (isIgnored(filePath) || unpairedFiles.has(path.basename(filePath))) return;

        const language = /\.(zh|en)\.md$/.exec(filePath);
        if (!language) {
            report(file, 'filename', 'Name Markdown documents with a .zh.md or .en.md suffix.', tree);
            return;
        }

        const partnerPath = filePath.slice(0, -language[0].length)
            + (language[1] === 'zh' ? '.en.md' : '.zh.md');
        if (isIgnored(partnerPath)) return;

        const pairKey = [filePath, partnerPath].sort().join('\0');
        if (checkedPairs?.has(pairKey)) return;
        checkedPairs?.add(pairKey);

        let partnerText;
        try {
            partnerText = await readFile(partnerPath, 'utf8');
        } catch (error) {
            const missing = error.code === 'ENOENT' || error.code === 'ENOTDIR';
            report(
                file,
                missing ? 'missing-partner' : 'read-partner',
                missing
                    ? `Missing bilingual partner: ${path.basename(partnerPath)}.`
                    : `Cannot read bilingual partner ${path.basename(partnerPath)}: ${error.message}`,
                tree
            );
            return;
        }

        const partnerTree = processor.parse({path: partnerPath, value: partnerText});
        const current = structure(tree);
        const partner = structure(partnerTree);
        const partnerName = path.basename(partnerPath);
        const headingCount = Math.max(current.headings.length, partner.headings.length);

        for (let index = 0; index < headingCount; index++) {
            const here = current.headings[index];
            const there = partner.headings[index];
            if (here?.depth !== there?.depth) {
                report(
                    file,
                    'headings',
                    `Heading ${index + 1} differs from ${partnerName}: `
                        + `${here ? `h${here.depth}` : 'absent'} here, `
                        + `${there ? `h${there.depth}` : 'absent'} in the partner.`,
                    here || tree
                );
                break;
            }
        }

        if (!sameCounts(current.codeLanguages, partner.codeLanguages)) {
            report(
                file,
                'code-blocks',
                `Code-block languages or counts differ from ${partnerName}: `
                    + `${describeCounts(current.codeLanguages)} here; `
                    + `${describeCounts(partner.codeLanguages)} in the partner.`,
                current.firstCode || tree
            );
        }
    };
}

async function findIgnoreFile(directory) {
    // Match remark-cli: the nearest .remarkignore applies, rather than merging ancestors.
    while (true) {
        try {
            const content = await readFile(path.join(directory, '.remarkignore'), 'utf8');
            return {directory, matcher: ignore().add(content)};
        } catch (error) {
            if (error.code !== 'ENOENT') throw error;
        }

        const parent = path.dirname(directory);
        if (parent === directory) return undefined;
        directory = parent;
    }
}

function structure(tree) {
    const result = {headings: [], codeLanguages: new Map(), firstCode: undefined};

    function visit(node) {
        if (node.type === 'heading') result.headings.push(node);
        if (node.type === 'code') {
            const language = node.lang || '';
            result.codeLanguages.set(language, (result.codeLanguages.get(language) || 0) + 1);
            result.firstCode ||= node;
        }
        for (const child of node.children || []) visit(child);
    }

    visit(tree);
    return result;
}

function sameCounts(left, right) {
    return left.size === right.size
        && [...left].every(([language, count]) => right.get(language) === count);
}

function describeCounts(counts) {
    return [...counts.keys()].sort()
        .map((language) => `${language || '(unlabelled)'}: ${counts.get(language)}`)
        .join(', ') || 'none';
}

function report(file, ruleId, reason, node) {
    file.message(reason, {place: node.position, source: 'bilingual', ruleId});
}
