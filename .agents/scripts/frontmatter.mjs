import {remark} from 'remark';
import remarkFrontmatter from 'remark-frontmatter';
import {parseDocument} from 'yaml';

const parser = remark().use(remarkFrontmatter);

export function parseFrontmatter(source, tree = parser.parse(source)) {
    const frontmatter = tree.children.find((node) => node.type === 'yaml');
    if (!frontmatter) return {meta: {}, range: null};

    const document = parseDocument(frontmatter.value);
    if (document.errors.length) throw document.errors[0];
    return {
        meta: document.toJSON() ?? {},
        range: {
            start: frontmatter.position.start.offset,
            end: frontmatter.position.end.offset
        }
    };
}
