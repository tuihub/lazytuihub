import { execFileSync, spawn } from 'node:child_process';
import { randomBytes } from 'node:crypto';
import { mkdtemp, readFile, writeFile, rm, mkdir } from 'node:fs/promises';
import { createServer } from 'node:http';
import { once } from 'node:events';
import { join, resolve } from 'node:path';
import { tmpdir } from 'node:os';
import { createInterface } from 'node:readline';
import { fileURLToPath } from 'node:url';

// Supply an isolated account, one article, and a private TLS trust chain for a real server.
const payload = resolve(process.argv[2] ?? '.local/win7/fixture');
const app = execFileSync('dotnet', [
    'msbuild', 'src/LazyTuiHub/LazyTuiHub.csproj', '-p:Configuration=Release', '-getProperty:TargetPath'
], {encoding: 'utf8', env: {...process.env, DOTNET_CLI_USE_MSBUILD_SERVER: '0'}}).trim();
const source = fileURLToPath(new URL('.', import.meta.url));
const work = await mkdtemp(join(tmpdir(), 'lth-win7-primary-'));
const secret = randomBytes(24).toString('hex');
const environment = { ...process.env, Access__Enabled: 'true', Access__AdminUser: 'lab', Access__AdminPassword: secret };
const database = join(work, 'workspace.db');
const web = createServer(async (request, response) => {
    if (request.url.split('?')[0] === '/feed') {
        response.writeHead(200,{'Content-Type':'application/rss+xml'});
        response.end('<rss version="2.0"><channel><title>Win7 fixture</title><link>https://example.org</link><description>Reader test</description><item><guid>win7-test</guid><title>Compatibility article</title></item></channel></rss>');
    } else if (request.url === '/lab.crl') {
        response.writeHead(200,{'Content-Type':'application/pkix-crl'});
        response.end(await readFile(join(work,'lab.crl')));
    } else {
        response.writeHead(404);
        response.end();
    }
});
web.listen(0,'127.0.0.1');
await once(web,'listening');
const port = web.address().port;

function openssl(...args) {
    execFileSync('openssl',args,{cwd:work,stdio:['ignore','ignore','inherit']});
}
async function command(group, action, args = [], session) {
    const proc = spawn('dotnet', [
        app, '--database', database, '--Logging:LogLevel:Default', 'None', '--command', group, '--action', action, ...args
    ], {
        env: {...environment, Access__Password: secret, Access__Session: session ?? ''},
        stdio: ['ignore', 'pipe', 'inherit']
    });
    const chunks = [];
    proc.stdout.on('data', chunk => chunks.push(chunk));
    const [code] = await once(proc, 'close');
    if (code !== 0) throw new Error(`${group} ${action} exited with ${code}`);
    const output = Buffer.concat(chunks).toString().trim();
    return output ? JSON.parse(output) : null;
}

let primary;
try {
    await mkdir(payload,{recursive: true});
    await writeFile(join(work,'index'),'');
    await writeFile(join(work,'serial'),'01\n');
    await writeFile(join(work,'number'),'01\n');
    await writeFile(join(work,'ca.cnf'),`[ca]
default_ca=local
[local]
database=index
new_certs_dir=.
certificate=root.pem
private_key=root.key
default_md=sha256
policy=policy
serial=serial
crlnumber=number
default_days=3
default_crl_days=3
[policy]
commonName=supplied
[server]
basicConstraints=critical,CA:FALSE
keyUsage=critical,digitalSignature,keyEncipherment
extendedKeyUsage=serverAuth
subjectAltName=IP:10.0.2.2,IP:127.0.0.1
crlDistributionPoints=URI:http://10.0.2.2:${port}/lab.crl
`);
    openssl('req','-new','-x509','-newkey','rsa:2048','-nodes','-subj','/CN=LazyTuiHub disposable lab CA','-days','3','-keyout','root.key','-out','root.pem','-addext','basicConstraints=critical,CA:TRUE','-addext','keyUsage=critical,keyCertSign,cRLSign');
    openssl('req','-new','-newkey','rsa:2048','-nodes','-subj','/CN=10.0.2.2','-keyout','tls.key','-out','tls.csr');
    openssl('ca','-batch','-config','ca.cnf','-extensions','server','-in','tls.csr','-out','tls.pem');
    openssl('ca','-config','ca.cnf','-gencrl','-out','crl.pem');
    openssl('crl','-in','crl.pem','-outform','DER','-out','lab.crl');
    openssl('pkcs12','-export','-out','tls.pfx','-inkey','tls.key','-in','tls.pem','-certfile','root.pem','-passout','pass:'+secret);
    openssl('x509','-in','root.pem','-outform','DER','-out',join(payload,'lab-ca.cer'));
    const {token} = await command('access','login',['--username','lab']);
    await command('access','set-device-owner',['--device','local','--username','lab'],token);
    const feed = await command('rss','add',['--feed-url',`http://127.0.0.1:${port}/feed`],token);
    await command('rss','refresh',['--name',feed.name],token);
    await command('access','logout',[],token);
    primary = spawn('dotnet', [app, '--database', database, '--urls', 'https://127.0.0.1:0'], {
        env: {
            ...environment,
            Kestrel__Certificates__Default__Path: join(work, 'tls.pfx'),
            Kestrel__Certificates__Default__Password: secret
        },
        stdio: ['ignore', 'pipe', 'inherit']
    });
    const lines = createInterface({input: primary.stdout});
    let endpoint;
    for await (const line of lines) {
        const match = line.match(/Now listening on: https:\/\/127\.0\.0\.1:(\d+)/);
        if (match) {
            endpoint = 'https://10.0.2.2:' + match[1];
            break;
        }
    }
    if (!endpoint) throw new Error('The HTTPS primary did not start.');
    primary.stdout.resume();
    await writeFile(join(payload,'password.txt'),secret,{mode: 0o600});
    await writeFile(join(payload,'endpoint.txt'),endpoint);
    for (const name of ['probe.ps1', 'updates.ps1', 'test.cmd']) {
        const script = await readFile(join(source, name), 'utf8');
        await writeFile(join(payload, name), script.replaceAll(/\r?\n/g, '\r\n'));
    }
    console.log(`Fixture ready for QEMU: ${endpoint} (payload ${payload})`);
    const stop = () => primary.kill();
    process.once('SIGTERM',stop);
    process.once('SIGINT',stop);
    await once(primary,'close');
} finally {
    if (primary && primary.exitCode === null) primary.kill();
    web.close();
    await rm(work,{recursive: true,force: true});
    for (const name of ['password.txt','endpoint.txt','lab-ca.cer']) await rm(join(payload,name),{force: true});
}
