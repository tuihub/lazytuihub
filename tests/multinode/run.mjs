import assert from 'node:assert/strict';
import {execFileSync, spawnSync} from 'node:child_process';
import {createHash, randomUUID} from 'node:crypto';
import {mkdir, mkdtempDisposable, readFile, rm, writeFile} from 'node:fs/promises';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {publishHost} from '../../.agents/scripts/release.mjs';

const root = fileURLToPath(new URL('../../', import.meta.url));
const project = 'lazytuihub-test-' + randomUUID().slice(0, 8);
const artifacts = path.join(root, 'artifacts/multinode');
await mkdir(artifacts, {recursive: true});
await using publish = await mkdtempDisposable(path.join(artifacts, 'publish-'));
const platform = process.env.DOCKER_DEFAULT_PLATFORM
    || execFileSync('docker', ['version', '--format', '{{.Server.Os}}/{{.Server.Arch}}'], {encoding: 'utf8'}).trim();
const [os, arch] = platform.split('/');
const rid = os + '-' + (arch === 'amd64' ? 'x64' : arch);
publishHost(rid, publish.path);
const {runtimeOptions} = JSON.parse(await readFile(path.join(publish.path, 'LazyTuiHub.runtimeconfig.json'), 'utf8'));
const runtime = runtimeOptions.includedFrameworks.find(framework => framework.name === 'Microsoft.NETCore.App');
const env = {...process.env, DOTNET_RUNTIME_TAG: runtime.version.split('.').slice(0, 2).join('.'), TEST_PUBLISH_DIR: publish.path,
    B_RESOURCE: 'compute.batch', WORKER_A_TOKEN: randomUUID(), WORKER_B_TOKEN: randomUUID(), WORKER_C_TOKEN: randomUUID()};
const compose = ['compose', '-p', project, '-f', 'tests/multinode/compose.yaml'];
const evidence = {project, cases: []};
const resultFile = path.join(artifacts, 'result.json');
function docker(args, mustSucceed = true) {
    const result = spawnSync('docker', args, {cwd: root, env, encoding: 'utf8'});
    if (result.error) throw result.error;
    if (mustSucceed && result.status !== 0) throw new Error(result.stdout + result.stderr);
    return result;
}
function cmd(...args) { return docker([...compose, ...args]).stdout.trim(); }
function on(instance, domain, action, ...args) {
    return [...compose, 'exec', '-T', instance, './LazyTuiHub', '--database', '/workspace/main.db',
        '--command', domain, '--action', action, ...args];
}
function app(...args) { return on('a', ...args); }
function call(...args) { return JSON.parse(docker(app(...args)).stdout); }
function addresses(service) {
    const id = cmd('ps', '-q', service);
    const networks = JSON.parse(docker(['inspect', '-f', '{{json .NetworkSettings.Networks}}', id]).stdout);
    return Object.fromEntries(Object.entries(networks).map(([name, info]) => [name.slice(project.length + 1), info.IPAddress]));
}
function hits(source) {
    return JSON.parse(cmd('exec', '-T', source, 'python', '-c',
        "import urllib.request; print(urllib.request.urlopen('http://127.0.0.1:8080/hits').read().decode())"));
}
function graph(option, name, ...additional) {
    const plan = call('workflow', 'plan', option, name, ...additional);
    return Object.fromEntries(plan.nodes.map(node => [node.id, node.candidates]));
}
function runGraph(option, name, ...additional) {
    const result = docker(app('workflow', 'run', option, name, '--progress', 'true', ...additional));
    const events = [...result.stderr.matchAll(/^\{[\s\S]*?^\}/gm)].map(match => JSON.parse(match[0]));
    assert(events.length > 0, result.stderr);
    return {events, output: result.stdout};
}
function record(name, facts) {
    evidence.cases.push({name, ...facts});
    console.log(name, JSON.stringify(facts));
}
const probe = `import sys,time,urllib.request,urllib.error
for attempt in range(50):
    try: urllib.request.urlopen('http://' + sys.argv[1] + ':8080/', timeout=1)
    except urllib.error.HTTPError as err:
        if err.code == 401: break
        raise
    except OSError: time.sleep(0.2)
else: raise RuntimeError('worker unavailable or unprotected')`;
function ready(service) { cmd('exec', '-T', 'shared-feed', 'python', '-c', probe, service); }
const digestFile = '/scenario/digest.json';
const digestExpected = createHash('sha256').update('跨实例的文本计算').digest('hex');
function digest(instance) {
    const {events, output} = runGraph('--pipeline-file', digestFile);
    assert.equal(JSON.parse(output).payload, digestExpected);
    assert(events.some(event => event.nodeId === 'digest' && event.state === 'delivered' && event.instance === instance));
    record('text digest on ' + instance, {attempts: events.filter(event => event.state === 'attempt').map(event => event.instance), digest: digestExpected});
}
function verifyAccountScopedWrite() {
    env.A_ACCESS_ENABLED = 'true';
    cmd('up', '-d', 'a'); ready('a');
    const adminPassword = randomUUID();
    const options = ['--Access:Enabled', 'true', '--Access:AdminUser', 'operator', '--Access:AdminPassword', adminPassword];
    const invoke = (domain, action, ...args) =>
        JSON.parse(docker(on('d', domain, action, ...options, ...args)).stdout);
    const session = invoke('access', 'login', '--username', 'operator', '--Access:Password', adminPassword);
    options.push('--Access:Session', session.token);
    docker(on('d', 'access', 'set-device-owner', ...options, '--device', 'D', '--username', 'operator'));
    const shared = invoke('rss', 'add', '--feed-url', 'http://shared-feed:8080/feed.xml?scoped').name;
    const plan = invoke('workflow', 'plan', '--pipeline-name', 'rss.ingest');
    assert.deepEqual(plan.nodes.find(node => node.id === 'fetch').candidates, ['D']);
    assert.deepEqual(plan.nodes.find(node => node.id === 'store').candidates, ['A']);
    assert.equal(invoke('rss', 'refresh', '--name', shared).processedArticleCount, 1);

    const query = `import json,pathlib,shutil,sqlite3,sys,tempfile
with tempfile.TemporaryDirectory() as snapshot:
    for name in ('main.db', 'main.db-wal'):
        source = pathlib.Path('/primary') / name
        if source.exists():
            shutil.copy(source, snapshot)
    with sqlite3.connect(pathlib.Path(snapshot) / 'main.db') as db:
        rows = db.execute("SELECT account FROM data_relations WHERE source_type = 'rss.feed' AND source_key = ?", (sys.argv[1],))
        print(json.dumps([row[0] for row in rows]))`;
    const accounts = JSON.parse(cmd('exec', '-T', 'shared-feed', 'python', '-c', query, shared.split('/')[1]));
    assert.deepEqual(accounts, [session.account]);
    record('account-scoped write on remote storage without its job grant', {fetch: 'D', store: 'A', accountVerified: true});
}
try {
    await rm(resultFile, {force: true});
    cmd('up', '-d', '--wait', '--wait-timeout', '90');
    ready('a'); ready('b'); ready('c');
    const a = addresses('a'), b = addresses('b'), c = addresses('c');
    assert(!a.private && b.private && c.private);
    assert(!addresses('private-feed').control);
    assert.notEqual(docker([...compose, 'exec', '-T', 'a', 'getent', 'hosts', 'private-feed'], false).status, 0);
    assert(cmd('exec', '-T', 'b', 'getent', 'hosts', 'private-feed').includes('private-feed'));
    const rssPlan = call('workflow', 'plan', '--pipeline-name', 'rss.ingest');
    const store = rssPlan.nodes.find(node => node.type === 'articles.store');
    assert.equal(store.executionMode, 'Owned');
    assert.deepEqual(store.requirements, ['workspace.primary']);
    assert.deepEqual(store.candidates, ['A']);
    const noPrimary = docker(app('workflow', 'plan', '--pipeline-name', 'rss.ingest', '--Cluster:Resources:0', 'scratch'), false);
    assert.notEqual(noPrimary.status, 0);
    const missing = JSON.parse(noPrimary.stdout).problems;
    assert(missing.some(problem => /node 'store' \(articles\.store\).*workspace\.primary/.test(problem.reason)));
    const placement = graph('--pipeline-name', 'rss.ingest');
    assert.deepEqual(placement.fetch, ['A', 'B', 'C']);
    assert.deepEqual(placement.store, ['A']);
    record('network isolation and placement', {a, b, c, placement, missingPrimary: missing});
    const add = source => call('rss', 'add', '--feed-url', 'http://' + source + ':8080/feed.xml').name;
    const refresh = feed => call('rss', 'refresh', '--name', feed).processedArticleCount;
    const shared = add('shared-feed'), restricted = add('private-feed');
    const {events} = runGraph('--pipeline-name', 'rss.ingest');
    assert.deepEqual(hits('private-feed'), [b.private]);
    assert.deepEqual(hits('shared-feed'), [a.control]);
    const privateEvents = events.filter(event => event.nodeId === 'fetch' && event.reference?.key === restricted.split('/')[1]);
    assert.deepEqual(privateEvents.map(event => [event.state, event.instance]),
        [['attempt','A'], ['unavailable','A'], ['attempt','B'], ['delivered','B']]);
    assert(events.some(event => event.nodeId === 'store' && event.state === 'completed' && event.instance === 'A'));
    const list = () => call('articles', 'list').articles;
    assert.deepEqual(list().map(item => item.title).sort(), ['private article', 'shared article']);
    const article = list().find(item => item.source === restricted);
    assert(article);
    call('articles', 'read', '--name', article.name);
    record('per-source placement and primary persistence', {local: hits('shared-feed'), fallback: hits('private-feed'), article: article.name, attempts: privateEvents});
    assert.deepEqual(graph('--pipeline-file', digestFile).digest, ['B','C']);
    digest('B');
    const setPolicy = status => cmd('exec', '-T', 'private-feed', 'python', '-c',
        'import pathlib,sys; pathlib.Path("/tmp/source-policy.json").write_text(sys.argv[1])', JSON.stringify({[b.private]: status}));
    setPolicy(503);
    assert.equal(refresh(restricted), 1);
    assert.deepEqual(hits('private-feed').slice(-2), [b.private, c.private]);
    record('source temporarily unavailable from B', {attempts: hits('private-feed').slice(-2)});
    setPolicy(404);
    const beforeRejection = hits('private-feed').length;
    const rejected = docker(app('rss', 'refresh', '--name', restricted), false);
    assert.notEqual(rejected.status, 0);
    assert.match(rejected.stdout + rejected.stderr, /upstream source returned HTTP 404/i);
    assert.deepEqual(hits('private-feed').slice(beforeRejection), [b.private]);
    record('terminal source rejection stops fallback', {attempts: hits('private-feed').slice(beforeRejection)});
    setPolicy(null);
    env.B_RESOURCE = 'scratch';
    cmd('up', '-d', 'b'); ready('b');
    const afterWithdrawal = graph('--pipeline-file', digestFile).digest;
    assert.deepEqual(afterWithdrawal, ['C']);
    digest('C');
    const afterRegistration = graph('--pipeline-file', digestFile, '--Cluster:Resources:1', 'compute.batch').digest;
    assert.deepEqual(afterRegistration, ['A','C']);
    const localDigest = runGraph('--pipeline-file', digestFile, '--Cluster:Resources:1', 'compute.batch');
    const localDelivery = localDigest.events.find(event => event.nodeId === 'digest' && event.state === 'delivered');
    assert.equal(localDelivery?.instance, 'A');
    assert.equal(JSON.parse(localDigest.output).payload, digestExpected);
    record('unchanged digest workflow follows resource registration', {afterWithdrawal, afterRegistration, executedOn: localDelivery.instance});
    cmd('stop', 'b');
    assert.equal(refresh(restricted), 1);
    assert.equal(hits('private-feed').at(-1), c.private);
    record('first peer offline', {fetchedBy: 'C', sourceHits: hits('private-feed')});
    digest('C');
    cmd('stop', 'c');
    const failure = docker(app('rss', 'refresh', '--name', restricted), false);
    assert.notEqual(failure.status, 0);
    const explanation = failure.stderr + failure.stdout;
    for (const id of ['A:', 'http://b:8080/', 'http://c:8080/']) assert(explanation.includes(id), explanation);
    assert.equal(refresh(shared), 1);
    record('all peers offline', {privateRefresh: explanation.split('\n').find(line => line.includes('No instance completed')), sharedRefresh: 'local success'});
    cmd('restart', 'a');
    assert.equal(list().length, 2);
    assert(list().some(item => item.name === article.name && item.read));
    cmd('start', 'b'); ready('b');
    assert.equal(refresh(restricted), 1);
    assert.equal(hits('private-feed').at(-1), addresses('b').private);
    assert.equal(list().length, 2);
    assert(list().some(item => item.name === article.name && item.read));
    record('primary restart and peer recovery', {fetchedBy: 'B', article: article.name, read: true, count: 2});
    ready('a');
    const fromD = (...args) => JSON.parse(docker(on('d', ...args)).stdout);
    const remoteSource = fromD('rss', 'add', '--feed-url', 'http://shared-feed:8080/feed.xml').name;
    const remotePlan = fromD('workflow', 'plan', '--pipeline-name', 'rss.ingest').nodes;
    assert.deepEqual(remotePlan.find(node => node.id === 'store').candidates, ['A']);
    assert.deepEqual(remotePlan.find(node => node.id === 'fetch').candidates, ['D']);
    const ambiguous = docker(on('d', 'workflow', 'plan', '--pipeline-name', 'rss.ingest',
        '--Cluster:Resources:0', 'workspace.primary'), false);
    assert.notEqual(ambiguous.status, 0);
    assert.match(ambiguous.stdout, /Ambiguous ownership.*workspace.primary.*D, A/);
    assert.equal(fromD('rss', 'refresh', '--name', remoteSource).processedArticleCount, 1);
    assert.equal(hits('shared-feed').at(-1), addresses('d').control);
    const saved = list().find(item => item.source === remoteSource);
    assert(saved);
    const relations = call('workflow', 'relations', '--data-type', 'rss.feed', '--data-key', remoteSource.split('/')[1]);
    assert(relations.some(relation => relation.target.key === saved.name.split('/')[1]));
    record('remote coordinator writes through the primary owner', {fetch: remotePlan.find(node => node.id === 'fetch').candidates, store: remotePlan.find(node => node.id === 'store').candidates, conflictingOwners: JSON.parse(ambiguous.stdout).problems, article: saved.name, source: remoteSource, relations});
    const unconfirmed = fromD('rss', 'add', '--feed-url', 'http://shared-feed:8080/feed.xml?lost').name;
    const result = docker(on('d', 'rss', 'refresh', '--name', unconfirmed, '--Cluster:Peers:0:Url', 'http://lost-reply:8080/'), false);
    assert.notEqual(result.status, 0);
    assert.match(result.stdout + result.stderr, /outcome is unknown/i);
    const writes = Number(cmd('exec', '-T', 'shared-feed', 'python', '-c',
        "import urllib.request; print(urllib.request.urlopen('http://lost-reply:8080/count').read().decode())"));
    assert.equal(writes, 1);
    const committed = list().find(item => item.source === unconfirmed);
    assert(committed);
    record('lost write acknowledgement', {confirmedByCoordinator: false, committedOnA: committed.name, writeRequests: writes});
    verifyAccountScopedWrite();
    await writeFile(resultFile, JSON.stringify(evidence, null, 2) + '\n');
} finally {
    cmd('down', '--volumes');
}
