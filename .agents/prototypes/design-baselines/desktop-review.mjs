import { mkdir, mkdtemp, readFile, rm, writeFile, rename } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { basename, join } from 'node:path';
import { chromium } from 'playwright';
import { PNG } from 'pngjs';
import { alignment, crop, difference, ink, pixelBox } from './review-pixels.mjs';

const designs = ['Fluent','MacOS','Material'], modes = ['Light','Dark'];
const components = ['card','add','chip'];
const round = n => Number(n.toFixed(2));
const size = box => box ? `${box[2]} × ${box[3]}` : '∅';
const range = values => values.some(v => v == null) ? 'unmeasurable' :
  `[${round(Math.min(...values))}, ${round(Math.max(...values))}] DIP`;
const writePng = (path, image) => writeFile(path, PNG.sync.write(image));
const figure = (path, title, zoom = false) => `<figure><img src="${path}.png" alt=""${zoom ? ' class="zoom"' : ''}><figcaption>${title}</figcaption></figure>`;

async function reference(page, url, mode, directory) {
  await page.goto(`${url}/fluent.html?theme=${mode.toLowerCase()}`);
  const button = page.locator('[data-measure="add-game"]');
  await button.scrollIntoViewIfNeeded();
  await page.evaluate(() => document.fonts.ready);
  const info = await button.evaluate(button => {
    const bounds = rect => [rect.x, rect.y, rect.width, rect.height];
    const node = [...button.childNodes].find(node => node.nodeType === Node.TEXT_NODE && node.textContent.trim() === 'Add game');
    const text = document.createRange();
    text.selectNodeContents(node);
    return { boxes: { add: bounds(button.getBoundingClientRect()), icon: bounds(button.querySelector('svg').getBoundingClientRect()),
      label: bounds(text.getBoundingClientRect()) }, font: getComputedStyle(button).font, scale: devicePixelRatio, scope: window.baselineScope };
  });
  const frame = PNG.sync.read(await page.screenshot());
  await writePng(join(directory, `official-${mode}.png`), crop(frame, pixelBox(info.boxes.add, info.scale, 2)));
  return { ...info, ink: alignment(frame, info.boxes, info.scale) };
}

async function analyzeScene(input, directory, name) {
  const source = join(input, name);
  const { states, ...scene } = JSON.parse(await readFile(join(source,'scene.json'), 'utf8'));
  const frame = async state => PNG.sync.read(await readFile(join(source, state + '.png')));
  const rest = await frame('rest'), repeat = await frame('repeat');
  const { boxes, scale } = scene;
  scene.alignment = alignment(rest, boxes, scale);
  scene.contrast = {};
  for (const key of ['hint','monogram']) {
    const sample = crop(rest, pixelBox(boxes[key], scale));
    scene.contrast[key] = ink(sample).contrast;
    await writePng(join(directory, `${name}-${key}.png`), sample);
  }
  scene.components = {};
  for (const key of components) {
    const box = pixelBox(boxes[key], scale, 6);
    const getCrop = image => crop(image, box);
    const baseline = getCrop(rest), repeated = getCrop(repeat);
    const prefix = `${name}-${key}`;
    await writePng(join(directory,prefix + '-rest.png'), baseline);
    const images = { rest: baseline };
    const measurements = {};
    for (const state of states.filter(s => s.startsWith(key + '-'))) {
      const short = state.slice(key.length+1);
      const initial = ['both','focus-leave'].includes(short) ? 'focus' : 'rest';
      const current = getCrop(await frame(state));
      images[short] = current;
      const { mask, stats } = difference(images[initial], current, baseline, repeated);
      await writePng(join(directory, `${prefix}-${short}.png`), current);
      await writePng(join(directory, `${prefix}-${short}-diff.png`), mask);
      measurements[short] = { initial, ...stats };
    }
    scene.components[key] = { box, measurements };
  }
  return scene;
}

function describeLayers(stats) {
  if (!stats.outer) return 'No change above the repeated-rest noise.';
  const [x,y,w,h] = stats.outer;
  if (!stats.inner) return `Changed envelope ${size(stats.outer)} px.`;
  const [a,b,c,d] = stats.inner;
  const prompt = (a !== x || b !== y || c !== w || d !== h) ? 'Review the two different paint bounds. ' : '';
  return `${prompt}Changed envelope ${size(stats.outer)} px, stronger envelope ${size(stats.inner)} px. Insets left / top / right / bottom: ${[a-x,b-y,x+w-a-c,y+h-b-d].join(' / ')} px.`;
}

function compareOffsets(product, official) {
  if ([...product,...official].some(v => v == null)) return 'Insufficient visible ink to compare.';
  return Math.max(...product) < Math.min(...official) || Math.min(...product) > Math.max(...official)
    ? 'Review alignment: the measured ranges do not overlap.' : 'The sampled ranges overlap.';
}

function componentPanel(name, key, scene) {
  const { measurements } = scene.components[key];
  const prefix = `${name}-${key}-`;
  const states = Object.entries(measurements).map(([state, sample]) =>
    `<section><b>${state}</b><div class="pair">${figure(prefix + state,'Product')}${figure(prefix + state + '-diff',`Δ vs ${sample.initial}`)}</div><small>${sample.changed} changed pixels</small></section>`).join('');
  return `<details><summary>${{card:'Cover card',add:'Add game',chip:'Selected filter'}[key]} · all states</summary><div class="strip">${figure(prefix+'rest','Rest')}${states}</div></details>`;
}

function section(name, scene, ref) {
  const [design, mode] = name.split('-');
  const { measurements } = scene.components.card;
  const offsets = scene.alignment.offsets;
  const refInfo = design === 'Fluent' ? `${figure('official-'+mode,'Microsoft Fluent UI React · '+range(ref.ink.offsets),true)}` : '';
  const motion = scene.motion;
  const midpoints = motion.samples.filter(([,v]) => v > 0 && v < 1);
  const motionText = motion.reduced ? 'Reduced motion active' :
    midpoints.length ? `${midpoints.length} intermediate opacity samples` : 'No intermediate frame observed';
  return `<article id="${name}"><h2>${design} · ${mode}</h2>
    <div class="group"><div><h3>Cover hover</h3><div class="pair">${figure(name+'-card-rest','Rest')}${figure(name+'-card-hover','Over artwork')}${figure(name+'-card-hover-diff','Changed pixels')}</div>
    <p>${describeLayers(measurements.hover)}</p><p>Frame ${size(scene.boxes.frame)} DIP · artwork ${size(scene.boxes.poster)} DIP. Repeated rest: ${measurements.hover.noisy} noisy pixels. Pointer exit: ${measurements.leave.changed} residual pixels; exit while focused: ${measurements['focus-leave'].changed}.</p></div>
    <div><h3>Text relative to the plus</h3><div class="buttons">${figure(name+'-add-rest','Product · '+range(offsets),true)}${refInfo}</div>
    <p>${design === 'Fluent' ? compareOffsets(offsets,ref.ink.offsets) : 'Product measurement only.'} Negative values place text above the icon. Product font: ${scene.font}, ${scene.FontSize} DIP.</p></div></div>
    <div class="pair">${figure(name+'-hint',`Search hint · ${round(scene.contrast.hint)}:1`)}${figure(name+'-monogram',`Fallback monogram · ${round(scene.contrast.monogram)}:1`)}</div>
    <p>${scene.unsettled.length ? `Capture did not settle: ${scene.unsettled.join(', ')}. ` : ''}Inspector reveal: ${motionText}. Final opacity ${motion.finalOpacity}. Headless scale ${scene.scale}×.</p>
    ${components.map(key=>componentPanel(name,key,scene)).join('')}</article>`;
}

function overview(scenes, refs) {
  const rows = Object.entries(scenes).map(([name, scene]) => {
    const [design, mode] = name.split('-');
    const { hover, leave } = scene.components.card.measurements;
    return `<tr><th scope="row"><a href="#${name}">${design} · ${mode}</a></th>
      <td>${size(hover.outer)} / ${size(hover.inner)}</td><td>${leave.changed}</td>
      <td>${range(scene.alignment.offsets)}</td><td>${design === 'Fluent' ? range(refs[mode].ink.offsets) : '—'}</td></tr>`;
  });
  return `<div style="overflow:auto"><table><thead><tr><th>Design</th><th>Hover envelopes (px)</th><th>Residual pixels on exit</th><th>Text − plus</th><th>Fluent Web reference</th></tr></thead>
    <tbody>${rows.join('')}</tbody></table></div>`;
}

function reportHtml(scenes, refs, time) {
  return `<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
  <title>Game library interaction review</title><style>
  *{box-sizing:border-box}body{font:16px/1.45 system-ui,sans-serif;color:#203242;background:#fafafa;margin:0 auto;max-width:1300px;padding:24px}
  h1,h2,h3{line-height:1.2}article{background:white;border:1px solid #ccd3da;padding:20px;margin:20px 0}
  .group,.pair,.strip,.buttons{display:flex;align-items:flex-start;gap:12px;flex-wrap:wrap}.group{gap:28px}.group>div{flex:1 1 370px;min-width:0}figure{margin:0;max-width:100%}img{max-width:100%;display:block}.zoom{zoom:2;image-rendering:pixelated}
  table{border-collapse:collapse}th,td{text-align:left;padding:6px 12px;border-bottom:1px solid #d6dce1}
  figcaption,small{color:#455768;font-size:13px}summary{cursor:pointer;padding:10px 0;font-weight:600}.strip section{padding:8px;border:1px solid #ddd}a{color:#155888}nav{display:flex;gap:16px;flex-wrap:wrap}</style>
  <h1>Game library interaction review</h1><p>${time} · <a href="measurements.json">Measurements and environment</a></p>
  ${overview(scenes, refs)}
  <p>Blue shows RGB changes greater than the noise measured at that pixel between two rest frames. Dark blue starts at the higher of the two most frequent nonzero changes. Each envelope encloses the changed pixels, including any gaps. Ink centers use four cutoffs from 20% to 65% of the strongest channel difference from the local background. Contrast compares the dominant background with the highest-contrast pixel in each text crop.</p>
  <p>The Fluent comparison uses Microsoft’s Web component with the same label and icon on this host. Inspect the enlarged glyphs and state crops alongside the design guidance. The measurements identify review targets; they do not certify native Windows rendering or accessibility compliance.</p>
  ${Object.entries(scenes).map(([k,v])=>section(k,v,refs[k.split('-')[1]])).join('')}</html>`;
}

export async function desktopReview({ repository, run, serve, dependencies }) {
  const root = join(repository,'.local/desktop-review');
  await mkdir(root, { recursive: true });
  const directory = await mkdtemp(join(root,'run-'));
  const input = await mkdtemp(join(tmpdir(),'lazytuihub-visual-'));
  try {
    await run('dotnet', ['run','--project','tests/LazyTuiHub.Tests','--','-method','LazyTuiHub.Tests.DesktopTests.GameShelfInteractionStates'],
      {cwd:repository, env:{ ...process.env, LAZYTUIHUB_VISUAL_REVIEW:input }});
    const refs = {}, scenes = {};
    const browser = await chromium.launch({args:['--disable-gpu']});
    const version = browser.version();
    try {
      const {server,url} = await serve();
      try {
        const page = await browser.newPage({viewport:{width:1120,height:1100},deviceScaleFactor:1});
        for (const mode of modes) refs[mode] = await reference(page,url,mode,directory);
      } finally { await new Promise(done => server.close(done)); }
    } finally { await browser.close(); }
    for (const design of designs) for (const mode of modes) {
      const name = design+'-'+mode;
      scenes[name] = await analyzeScene(input,directory,name);
    }
    const time = new Date().toISOString();
    await writeFile(join(directory,'measurements.json'),JSON.stringify({time,browser:version,dependencies,refs,scenes},null,2));
    await writeFile(join(directory,'index.html'),reportHtml(scenes,refs,time));
    const link = `${basename(directory)}/index.html`;
    const redirect = join(directory,'latest.html');
    await writeFile(redirect,`<!doctype html><meta charset="utf-8"><meta http-equiv="refresh" content="0;url=${link}"><a href="${link}">Latest interaction review</a>`);
    await rename(redirect,join(root,'index.html'));
    console.log(`Game library interaction review: ${join(root,'index.html')}`);
  } catch (error) {
    await rm(directory, {recursive:true,force:true});
    throw error;
  } finally { await rm(input, {recursive:true,force:true}); }
}
