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

const payload = (await lines('payload')).map(line => line.split('|'));
const assemblies = new Map(payload.filter(([, , , id]) => id)
    .map(([project, source, , id]) => [path.normalize(project), {id, assembly: path.basename(source)}]));

for (const line of await lines('selection')) {
    const [project, required] = line.split('|');
    const plugin = assemblies.get(path.normalize(project));
    if (!plugin) throw new Error(`Missing plugin identity or entry assembly for ${project}`);
    if ([...plugins.values()].some(entry => entry.id === plugin.id)) throw new Error(`Duplicate plugin ID: ${plugin.id}`);
    plugins.set(path.normalize(project), {...plugin, required: required === 'true'});
}

const entries = new Set(Array.from(plugins.values(), plugin => plugin.assembly));
const shared = new Set((await lines('shared')).map(file => path.normalize(file)));
for (const [project, source, relative] of payload) {
    const plugin = plugins.get(path.normalize(project));
    const target = path.normalize(relative || path.basename(source));
    if (target.endsWith('.pdb')) continue;
    if (target !== plugin.assembly && (shared.has(target) || entries.has(target))) continue;
    files.set(path.join(plugin.id, target), source);
}

for (const {id, assembly} of plugins.values()) {
    const members = [...files.keys()].filter(file => file.startsWith(id + path.sep));
    const entry = path.join(id, assembly);
    const deps = path.join(id, path.basename(assembly, '.dll') + '.deps.json');
    if (members.every(file => file === entry || file === deps)) files.delete(deps);
}

const json = JSON.stringify(Array.from(plugins.values(), ({id, assembly, required}) => ({id, assembly, required})), null, 2) + '\n';
const previous = await readFile(index, 'utf8').catch(error => {
    if (error.code !== 'ENOENT') throw error;
});
if (json !== previous) await writeFile(index, json);
files.set('plugins.json', index);
for (const [target, source] of files) console.log(`${source}|plugins/${target}`);
