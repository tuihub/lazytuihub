import {spawn} from 'node:child_process';
import {randomBytes} from 'node:crypto';
import {access, copyFile, link, mkdir, mkdtemp, readFile, rm, writeFile} from 'node:fs/promises';
import {tmpdir} from 'node:os';
import {basename, join, resolve} from 'node:path';
import {fileURLToPath} from 'node:url';

const [action, input, snapshot] = process.argv.slice(2);
if (!input || !['install', 'run'].includes(action)) {
    throw new Error('Use install WINDOWS_ISO or run PAYLOAD_DIRECTORY [BASE_DISK].');
}
const root = resolve('artifacts/win7');
const base = action === 'run' && snapshot ? resolve(snapshot) : join(root, 'base.qcow2');
const source = fileURLToPath(new URL('.', import.meta.url));
await mkdir(root, {recursive: true});
const work = await mkdtemp(join(tmpdir(), 'lth-win7-'));
const serial = join(work, 'serial.txt');

function exec(program, args) {
    return new Promise((done, fail) => {
        const child = spawn(program, args, {stdio: 'inherit'});
        child.on('error', fail);
        child.on('exit', (code, signal) => code === 0 ? done() : fail(new Error(`${program}: ${signal ?? code}`)));
    });
}

function image(folder, path) {
    return exec('xorriso', ['-as', 'mkisofs', '-quiet', '-J', '-joliet-long', '-r', '-o', path, folder]);
}

function cd(path, index) {
    return ['-drive', `file=${path},media=cdrom,readonly=on,if=ide,index=${index}`];
}

async function boot(disk, devices) {
    console.log(`QEMU monitor: ${work}/monitor.sock\nGuest serial: ${serial}`);
    await exec('qemu-system-x86_64', [
        '-machine', 'pc', '-accel', 'tcg', '-cpu', 'max', '-smp', '2', '-m', '3072',
        '-drive', `file=${disk},format=qcow2,if=ide,index=0`, ...devices,
        '-nic', 'user,model=e1000', '-vga', 'std', '-display', 'none',
        '-serial', `file:${serial}`, '-monitor', `unix:${work}/monitor.sock,server=on,wait=off`
    ]);
}

async function install() {
    await access(resolve(input));
    const staging = await mkdtemp(join(root, 'installation-'));
    const disk = join(staging, 'base.qcow2');
    const seed = join(work, 'seed');
    await mkdir(seed);
    const answer = await readFile(join(source, 'unattend.xml'), 'utf8');
    await writeFile(join(seed, 'Autounattend.xml'), answer.replaceAll('LAB_PASSWORD', randomBytes(18).toString('hex')));
    for (const name of ['prepare.cmd', 'boot-test.cmd']) {
        const script = await readFile(join(source, name), 'utf8');
        await writeFile(join(seed, name), script.replaceAll(/\r?\n/g, '\r\n'));
    }
    const iso = join(work, 'setup.iso');
    await image(seed, iso);
    await exec('qemu-img', ['create', '-f', 'qcow2', disk, '40G']);
    await boot(disk, [...cd(resolve(input), 2), ...cd(iso, 3), '-boot', 'order=c,once=d']);
    if (!(await readFile(serial, 'utf8')).includes('LTH-BASE-READY')) {
        throw new Error(`Setup did not finish. Disk: ${disk}`);
    }
    // Hard-link promotion keeps an existing baseline intact, including concurrent installations.
    await link(disk, base);
    await rm(staging, {recursive: true});
}

async function run() {
    await access(base);
    const payload = resolve(input);
    await access(join(payload, 'test.cmd'));
    const iso = join(work, 'payload.iso');
    const disk = join(work, 'overlay.qcow2');
    await image(payload, iso);
    await exec('qemu-img', ['create', '-f', 'qcow2', '-F', 'qcow2', '-b', base, disk]);
    await boot(disk, cd(iso, 2));
    const output = await readFile(serial, 'utf8');
    process.stdout.write(output);
    await copyFile(serial, join(root, basename(payload) + '-result.txt'));
    if (!output.includes('LTH-GUEST-END')) throw new Error('Guest stopped before reporting its result.');
    if (!output.includes('LTH-TEST-PASS')) process.exitCode = 1;
}

try {
    if (action === 'install') {
        try {
            await access(base);
            throw new Error('Base already exists. Choose run to use it.');
        } catch (error) {
            if (error.code !== 'ENOENT') throw error;
        }
        await install();
    } else {
        await run();
    }
} catch (error) {
    console.error(`Diagnostic files retained in ${work}`);
    throw error;
}
await rm(work, {recursive: true});
