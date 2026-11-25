import { spawn } from 'node:child_process';
import { createServer } from 'node:http';
import { mkdir, mkdtemp, readFile, writeFile, copyFile } from 'node:fs/promises';
import { dirname, resolve, extname, relative } from 'node:path';
import { fileURLToPath } from 'node:url';
import os from 'node:os';

const directory = dirname(fileURLToPath(import.meta.url));
const repository = resolve(directory, '../../..');
const output = resolve(repository, '.local/design-baselines');
const site = resolve(output, 'web');
process.env.PLAYWRIGHT_BROWSERS_PATH = resolve(output, 'browsers');

function run(command, args, options = {}) {
  return new Promise((accept, reject) => {
    const child = spawn(command, args, { cwd: directory, stdio: 'inherit', ...options });
    child.on('error', reject);
    child.on('exit', code => code === 0 ? accept() : reject(new Error(`${command} exited with ${code}`)));
  });
}

async function setup() {
  await run('pnpm', ['install', '--frozen-lockfile']);
  await run('pnpm', ['exec', 'playwright', 'install', 'chromium', '--only-shell']);
  const fontDirectory = resolve(site, 'fonts');
  await mkdir(fontDirectory, { recursive: true });
  const source = 'https://fonts.googleapis.com/css2?family=Roboto:wght@400;500;700&display=swap';
  const headers = { 'User-Agent': 'Mozilla/5.0 (compatible; LazyTuiHub; +https://github.com/tuihub/lazytuihub)' };
  const response = await fetch(source, { headers });
  if (!response.ok) throw new Error(`Roboto CSS: ${response.status}`);
  const css = await response.text();
  const blocks = [...css.matchAll(/@font-face\s*\{[^}]+\}/g)].map(match => match[0]);
  if (blocks.length === 0) throw new Error('Google Fonts did not return the requested Roboto faces.');
  const urls = [...new Set(blocks.join('\n').match(/https:\/\/fonts\.gstatic\.com\/[^)\s]+/g))];
  let local = blocks.join('\n');
  for (const [index, url] of urls.entries()) {
    const font = await fetch(url, { headers });
    if (!font.ok) throw new Error(`Roboto font: ${font.status}`);
    const name = `roboto-${index}${new URL(url).pathname.endsWith('.woff2') ? '.woff2' : '.ttf'}`;
    await writeFile(resolve(fontDirectory, name), Buffer.from(await font.arrayBuffer()));
    local = local.replaceAll(url, `./${name}`);
  }
  await writeFile(resolve(fontDirectory, 'roboto.css'), local);
  await writeFile(resolve(fontDirectory, 'sources.json'), JSON.stringify({ stylesheet: source, urls }, null, 2));
}

async function buildWeb() {
  const { build } = await import('esbuild');
  await mkdir(site, { recursive: true });
  await build({ entryPoints: [resolve(directory, 'web/fluent.jsx'), resolve(directory, 'web/material.js')],
    outdir: site, bundle: true, format: 'esm', minify: true, define: { 'process.env.NODE_ENV': '"production"' } });
  await copyFile(resolve(directory, 'web/layout.css'), resolve(site, 'layout.css'));
  for (const system of ['fluent', 'material']) {
    await writeFile(resolve(site, `${system}.html`), `<!doctype html><html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>${system} official component baseline</title><link rel="stylesheet" href="layout.css">${system === 'material' ? '<link rel="stylesheet" href="fonts/roboto.css">' : ''}</head><body><div id="root"></div><script type="module" src="${system}.js"></script></body></html>`);
  }
  await writeFile(resolve(site, 'index.html'), '<!doctype html><html lang="en"><meta charset="utf-8"><title>Design references</title><h1>Official component baselines</h1><ul>' + ['fluent', 'material'].flatMap(system => ['light', 'dark'].map(theme => `<li><a href="${system}.html?theme=${theme}">${system} / ${theme}</a></li>`)).join('') + '</ul><h2>Avalonia implementations</h2><p>Installed and candidate libraries, not official design references.</p><a href="avalonia.html">Compare rendered control specimens</a></html>');
  await writeFile(resolve(site, 'avalonia.html'), `<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>Avalonia theme specimens</title><style>body{font:16px system-ui;margin:24px}main{display:grid;grid-template-columns:repeat(auto-fit,minmax(450px,1fr));gap:24px}img{width:100%;border:1px solid #aaa}label{margin-right:24px}select{font:inherit}article{min-width:0}</style><h1>Avalonia theme specimens</h1><p>These specimens use library templates directly. They do not use LazyTuiHub page styles. The official references remain on the <a href="/">home page</a>.</p><p>Generate images with <code>mise -E development run render:design-baselines:avalonia</code>.</p><label>Color <select id="color"><option>light</option><option>dark</option></select></label><label>State <select id="state"><option>default</option><option>hover</option><option>focus</option><option>pressed</option><option>popup</option></select></label><main></main><script>
const systems = {fluent:'Avalonia Fluent',macos:'Devolutions macOS',material2:'Material.Avalonia',fluentavalonia:'FluentAvalonia',material3:'CCSWE Material 3'};
function show(){document.querySelector('main').innerHTML=Object.entries(systems).map(([key,label])=>{const stem='avalonia/'+key+'-'+document.querySelector('#color').value+'-'+document.querySelector('#state').value;return '<article><h2>'+label+'</h2><a href="'+stem+'.png"><img src="'+stem+'.png" alt="'+label+'"></a><a href="'+stem+'.json">Control and resolved font measurements</a></article>';}).join('');}
document.querySelectorAll('select').forEach(select=>select.addEventListener('change',show));show();
</script></html>`);
}

async function serve() {
  await buildWeb();
  const types = { '.html': 'text/html', '.js': 'text/javascript', '.css': 'text/css', '.woff2': 'font/woff2', '.ttf': 'font/ttf', '.png': 'image/png', '.json': 'application/json' };
  const server = createServer(async (request, response) => {
    const path = resolve(site, `.${decodeURIComponent(new URL(request.url, 'http://localhost').pathname === '/' ? '/index.html' : new URL(request.url, 'http://localhost').pathname)}`);
    if (relative(site, path).startsWith('..')) { response.writeHead(403).end(); return; }
    let content;
    try { content = await readFile(path); }
    catch (error) {
      response.writeHead(error.code === 'ENOENT' ? 404 : 500).end(String(error));
      return;
    }
    response.writeHead(200, { 'Content-Type': types[extname(path)] ?? 'application/octet-stream' }).end(content);
  });
  await new Promise(accept => server.listen(0, '127.0.0.1', accept));
  return { server, url: `http://127.0.0.1:${server.address().port}` };
}

async function capture(page, system, theme, state, browser) {
  await page.waitForTimeout(800); // Let the libraries finish their normal state transitions.
  const stem = `${system}-${theme}-${state}`;
  await page.screenshot({ path: resolve(output, `${stem}.png`), fullPage: true });
  const measurements = await page.evaluate(() => {
    const properties = ['fontFamily', 'fontSize', 'fontWeight', 'lineHeight', 'letterSpacing', 'color', 'backgroundColor',
      'borderColor', 'borderWidth', 'borderRadius', 'padding', 'gap', 'boxShadow', 'opacity'];
    const measure = element => {
      const bounds = element.getBoundingClientRect();
      const style = getComputedStyle(element);
      return { tag: element.localName, classes: element.className?.baseVal ?? element.className, bounds: bounds.toJSON(),
        text: element.childElementCount === 0 ? element.textContent : undefined,
        css: Object.fromEntries(properties.map(property => [property, style[property]])),
        disabled: element.disabled ?? false, selected: element.selected ?? element.getAttribute('aria-selected'),
        hover: element.matches(':hover'), focus: element.matches(':focus'), focusVisible: element.matches(':focus-visible') };
    };
    const descend = element => [element, ...[...element.children].flatMap(descend), ...(element.shadowRoot ? [...element.shadowRoot.children].flatMap(descend) : [])];
    return { scope: window.baselineScope, tokens: window.baselineTokens, devicePixelRatio, viewport: { width: innerWidth, height: innerHeight },
      components: [...document.querySelectorAll('[data-measure]')].map(element => ({ name: element.dataset.measure,
        nodes: descend(element).filter(node => node.getBoundingClientRect().width > 0 && !['style', 'svg', 'path'].includes(node.localName)).map(measure) })) };
  });
  const cdp = await page.context().newCDPSession(page);
  await cdp.send('DOM.enable'); await cdp.send('CSS.enable');
  const { root } = await cdp.send('DOM.getDocument', { depth: -1, pierce: true });
  for (const component of measurements.components) {
    const { nodeId } = await cdp.send('DOM.querySelector', { nodeId: root.nodeId, selector: `[data-measure="${component.name}"]` });
    if (nodeId) component.platformFonts = (await cdp.send('CSS.getPlatformFontsForNode', { nodeId })).fonts;
  }
  await cdp.detach();
  const dependencies = JSON.parse(await readFile(resolve(directory, 'package.json'), 'utf8')).dependencies;
  await writeFile(resolve(output, `${stem}.json`), JSON.stringify({ system, theme, state, browser: browser.version(),
    platform: os.platform(), release: os.release(), architecture: os.arch(), dependencies, ...measurements }, null, 2));
  console.log(`Rendered ${stem}`);
}

async function renderWeb() {
  const { chromium } = await import('playwright');
  const { server, url } = await serve();
  const browser = await chromium.launch({ args: ['--disable-gpu'] });
  try {
    for (const system of ['fluent', 'material']) {
      for (const theme of ['light', 'dark']) {
        const page = await browser.newPage({ viewport: { width: 1120, height: 900 }, deviceScaleFactor: 1, colorScheme: theme });
        const failures = [];
        page.on('pageerror', error => failures.push(error.message));
        page.on('response', response => { if (response.status() >= 400) failures.push(`${response.status()} ${response.url()}`); });
        await page.goto(`${url}/${system}.html?theme=${theme}`);
        await page.waitForFunction(() => window.baselineReady && document.querySelector('[data-measure="title"]'));
        await page.evaluate(() => document.fonts.ready);
        if (system === 'material' && !await page.evaluate(() => document.fonts.check('16px Roboto'))) throw new Error('Roboto is not loaded.');
        await capture(page, system, theme, 'default', browser);
        await page.locator('[data-action="primary"]').hover();
        await capture(page, system, theme, 'hover', browser);
        await page.mouse.move(0, 0);
        await page.keyboard.press('Tab');
        await page.locator('[data-action="input"]').evaluate(element => (element.querySelector('input') ?? element).focus());
        await capture(page, system, theme, 'focus', browser);
        await page.locator('[data-action="popup"]').click();
        await capture(page, system, theme, 'popup', browser);
        await page.keyboard.press('Escape');
        await page.locator('[data-action="select"]').click();
        await capture(page, system, theme, 'selector', browser);
        await page.keyboard.press('Escape');
        await page.setViewportSize({ width: 430, height: 900 });
        await page.mouse.move(0, 0);
        await capture(page, system, theme, 'narrow', browser);
        if (failures.length) throw new Error(failures.join('\n'));
        await page.close();
      }
    }
  } finally { await browser.close(); await new Promise(accept => server.close(accept)); }
}

async function renderAvalonia() {
  await buildWeb();
  const project = resolve(directory, 'Avalonia/Avalonia.csproj');
  await run('dotnet', ['build', project, '--nologo']);
  for (const system of ['fluent', 'macos', 'material2', 'fluentavalonia', 'material3']) {
    await run('dotnet', ['run', '--no-build', '--project', project, '--', system, resolve(site, 'avalonia')]);
  }
}

async function renderMacOS() {
  if (process.platform !== 'darwin') throw new Error('The native AppKit baseline requires macOS and Xcode command-line tools. Use render web elsewhere.');
  const binary = resolve(output, 'appkit-baseline');
  await run('xcrun', ['swiftc', resolve(directory, 'macos/main.swift'), '-o', binary, '-framework', 'AppKit']);
  await run(binary, [output]);
}

await mkdir(output, { recursive: true });
const [command = 'render', target = 'all'] = process.argv.slice(2);
if (command === 'setup') await setup();
else if (command === 'review') {
  await run(process.execPath, ['--test', 'review-pixels.test.mjs']);
  const { desktopReview } = await import('./desktop-review.mjs');
  const { dependencies } = JSON.parse(await readFile(resolve(directory, 'package.json'), 'utf8'));
  await desktopReview({ repository, run, serve, dependencies });
}
else if (command === 'serve') console.log(`Official component specimens: ${(await serve()).url}`);
else if (command === 'render' && target === 'macos-library') {
  if (process.platform !== 'darwin') throw new Error('The native library reference requires macOS.');
  const binary = resolve(output, 'appkit-library');
  await run('xcrun', ['swiftc', resolve(directory, 'macos-library/main.swift'), '-o', binary]);
  const args = process.argv.slice(4);
  const captures = args.includes('--show') ? output : await mkdtemp(resolve(output, 'macos-library-'));
  await run(binary, [captures, resolve(repository, 'tests/LazyTuiHub.Tests/Fixtures/review-cover.png'), ...args]);
}
else if (command === 'render' && ['all', 'web', 'macos', 'avalonia'].includes(target)) {
  if (target === 'all' || target === 'web') await renderWeb();
  if (target === 'all' || target === 'avalonia') await renderAvalonia();
  if (target === 'all' || target === 'macos') await renderMacOS();
} else throw new Error('Usage: baseline.mjs setup | serve | review | render [all|web|macos|avalonia|macos-library [--show]]');
