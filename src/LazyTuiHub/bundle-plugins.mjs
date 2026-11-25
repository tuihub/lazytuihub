import {readFile, writeFile} from 'node:fs/promises';
import path from 'node:path';

const directory = path.resolve(process.argv[2]);
const plugins = new Map();
const files = new Map();
const index = path.join(directory, 'plugins.json');

async function lines(name) {
    const text = await readFile(path.join(directory, name), 'utf8');
    return text.trim().split(/\r?\n/).filter(Boolean);
}

for (const line of await lines('selection')) {
    const [project, required] = line.split('|');
    const folder = path.dirname(project);
    const owner = path.basename(folder) === 'runtime' ? path.dirname(folder) : folder;
    const manifest = path.join(owner, 'plugin.json');
    const plugin = JSON.parse(await readFile(manifest, 'utf8'));
    if (typeof plugin.id !== 'string' || !/^lazytuihub\.plugin\.(?:[a-z0-9]+(?:-[a-z0-9]+)*\.)+[a-z0-9]+(?:-[a-z0-9]+)*$/.test(plugin.id)) {
        throw new Error(`Invalid plugin ID in ${manifest}: ${plugin.id}`);
    }
    if ([...plugins.values()].some(entry => entry.id === plugin.id)) throw new Error(`Duplicate plugin ID: ${plugin.id}`);
    plugin.assembly = path.posix.join(plugin.id, plugin.assembly);
    plugin.required = required === 'true';
    plugins.set(path.normalize(project), plugin);
    files.set(path.posix.join(plugin.id, 'plugin.json'), manifest);
}

const entries = new Set(Array.from(plugins.values(), plugin => path.posix.basename(plugin.assembly)));
const shared = new Set((await lines('shared')).map(file => path.normalize(file)));
for (const line of await lines('payload')) {
    const [project, source, relative] = line.split('|');
    const plugin = plugins.get(path.normalize(project));
    const target = path.normalize(relative || path.basename(source));
    if (target !== path.posix.basename(plugin.assembly) && (shared.has(target) || entries.has(target))) continue;
    files.set(path.join(plugin.id, target), source);
}

const json = JSON.stringify(Array.from(plugins.values(), ({id, required}) => ({id, required})), null, 2) + '\n';
const previous = await readFile(index, 'utf8').catch(error => {
    if (error.code !== 'ENOENT') throw error;
});
if (json !== previous) await writeFile(index, json);
files.set('plugins.json', index);
for (const [target, source] of files) console.log(`${source}|plugins/${target}`);
