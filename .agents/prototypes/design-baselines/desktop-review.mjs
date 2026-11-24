import { mkdir, mkdtemp, readFile, readdir, rename, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { basename, join } from 'node:path';
import { chromium } from 'playwright';
import { PNG } from 'pngjs';
import { alignment, crop, difference, ink, pixelBox } from './review-pixels.mjs';

const round = value => Number(value.toFixed(2));
const size = box => box ? `${box[2]} × ${box[3]}` : '∅';
const range = values => values.some(value => value == null) ? 'unmeasurable' :
  `[${round(Math.min(...values))}, ${round(Math.max(...values))}] DIP`;
const writePng = (path, image) => writeFile(path, PNG.sync.write(image));
const figure = (path, title, zoom = false) =>
  `<figure><img src="${path}.png" alt=""${zoom ? ' class="zoom"' : ''}><figcaption>${title}</figcaption></figure>`;

async function reference(page, url, specimen, mode, directory) {
  await page.goto(`${url}/fluent.html?theme=${mode.toLowerCase()}`);
  const button = page.locator(`[data-measure="${specimen}"]`);
  await button.scrollIntoViewIfNeeded();
  await page.evaluate(() => document.fonts.ready);
  const info = await button.evaluate(button => {
    const bounds = rect => [rect.x, rect.y, rect.width, rect.height];
    const walker = document.createTreeWalker(button, NodeFilter.SHOW_TEXT);
    let node;
    while ((node = walker.nextNode()) && !node.textContent.trim()) { /* Skip whitespace. */ }
    const text = document.createRange();
    text.selectNodeContents(node);
    return { boxes: { button: bounds(button.getBoundingClientRect()), icon: bounds(button.querySelector('svg').getBoundingClientRect()),
      label: bounds(text.getBoundingClientRect()) }, font: getComputedStyle(button).font, scale: devicePixelRatio, scope: window.baselineScope };
  });
  const frame = PNG.sync.read(await page.screenshot());
  const image = `official-${specimen}-${mode}`;
  await writePng(join(directory, image + '.png'), crop(frame, pixelBox(info.boxes.button, info.scale)));
  return { ...info, image, ink: alignment(frame, info.boxes, info.scale) };
}

function frameRegion(frame, bounds, scale) {
  // Include the space immediately outside the control, up to the viewport edge.
  const [x, y, width, height] = pixelBox(bounds, scale, 6);
  const left = Math.max(0, x), top = Math.max(0, y);
  return [left, top, Math.min(frame.width, x + width) - left, Math.min(frame.height, y + height) - top];
}

async function analyzeScene(source, directory) {
  const scene = JSON.parse(await readFile(join(source, 'scene.json'), 'utf8'));
  const frame = async state => PNG.sync.read(await readFile(join(source, state + '.png')));
  const rest = await frame('rest'), repeat = await frame('repeat');
  const { boxes, scale } = scene;
  scene.id = `${scene.page}-${scene.design}-${scene.mode}`;
  for (const [key, title] of Object.entries(scene.contrast)) {
    const sample = crop(rest, pixelBox(boxes[key], scale));
    const image = `${scene.id}-${key}`;
    scene.contrast[key] = { title, ratio: ink(sample).contrast, image };
    await writePng(join(directory, image + '.png'), sample);
  }
  for (const [key, target] of Object.entries(scene.components)) {
    const box = frameRegion(rest, boxes[key], scale);
    const baseline = crop(rest, box), repeated = crop(repeat, box);
    const prefix = `${scene.id}-${key}`;
    await writePng(join(directory, prefix + '-rest.png'), baseline);
    const images = { rest: baseline };
    target.box = box;
    target.prefix = prefix;
    target.measurements = {};
    if (target.alignment) {
      const { icon, label } = target.alignment;
      target.alignment.ink = alignment(rest, {icon: boxes[icon], label: boxes[label]}, scale);
    }
    for (const [state, initial] of Object.entries(target.samples)) {
      const current = crop(await frame(key + '-' + state), box);
      images[state] = current;
      const { mask, stats } = difference(images[initial], current, baseline, repeated);
      await writePng(join(directory, `${prefix}-${state}.png`), current);
      await writePng(join(directory, `${prefix}-${state}-diff.png`), mask);
      target.measurements[state] = { initial, ...stats };
    }
  }
  return scene;
}

function describeLayers(stats) {
  if (!stats.outer) return 'No change above the measured repeat noise.';
  if (!stats.inner) return `Changed envelope ${size(stats.outer)} px.`;
  const [x,y,w,h] = stats.outer, [a,b,c,d] = stats.inner;
  const insets = [a-x, b-y, x+w-a-c, y+h-b-d];
  return `Changed envelope ${size(stats.outer)} px; stronger envelope ${size(stats.inner)} px. ` +
    (insets.some(Boolean) ? `Review the insets (left/top/right/bottom): ${insets.join(' / ')} px.` : 'Both have the same bounds.');
}

function alignmentPanel(target, ref) {
  if (!target.alignment) return '';
  const {font, size, ink} = target.alignment;
  let comparison = '';
  if (ref) {
    const a = ink.offsets, b = ref.ink.offsets;
    const comparable = [...a, ...b].every(Number.isFinite);
    const gap = Math.max(Math.min(...a) - Math.max(...b), Math.min(...b) - Math.max(...a), 0);
    const result = !comparable ? 'Insufficient visible ink.' : gap ? `Nearest measured offsets differ by ${round(gap)} DIP.` : 'Measured ranges overlap.';
    comparison = figure(ref.image, 'Fluent Web · ' + range(b), true) + `<p>${result}</p>`;
  }
  return `<div class="pair">${figure(target.prefix+'-rest',`Product · ${range(ink.offsets)} · ${font}, ${size} DIP`,true)}${comparison}</div>`;
}

function componentPanel(target, ref) {
  const { prefix, measurements } = target;
  const states = Object.entries(measurements).map(([state, sample]) =>
    `<section><b>${state}</b><div class="pair">${figure(prefix+'-'+state,'Product')}${figure(prefix+'-'+state+'-diff','Δ vs '+sample.initial)}</div><small>${sample.changed} changed pixels</small></section>`).join('');
  return `<h4>${target.title}</h4>${alignmentPanel(target, ref)}<div class="pair">${figure(prefix+'-rest','Rest')}${figure(prefix+'-hover','Hover')}${figure(prefix+'-hover-diff','Changed pixels')}</div>
    <p>${describeLayers(measurements.hover)} Repeat noise: ${measurements.hover.noisy} pixels. Residuals after pointer exit: ${measurements.leave.changed}, while focused: ${measurements['focus-leave'].changed}.</p>
    <details><summary>All interaction states</summary><div class="strip">${states}</div></details>`;
}

function reportHtml(scenes, refs, time) {
  const specimen = (scene, target) => refs[target.alignment?.reference + '-' + scene.mode];
  const rows = scenes.flatMap(scene => Object.values(scene.components).map(target => {
    const hover = target.measurements.hover, ref = specimen(scene, target);
    return `<tr><th><a href="#${scene.id}">${scene.page} · ${scene.design} · ${scene.mode}</a></th>
      <td>${target.title}</td><td>${size(hover.outer)} / ${size(hover.inner)}</td><td>${target.measurements.leave.changed}</td>
      <td>${target.alignment ? range(target.alignment.ink.offsets) : '—'}</td><td>${ref ? range(ref.ink.offsets) : '—'}</td></tr>`;
  }));
  const sections = scenes.map(scene => {
    const targets = Object.values(scene.components).map(target => componentPanel(target, specimen(scene, target)));
    const contrast = Object.values(scene.contrast).map(c => figure(c.image, `${c.title} · ${round(c.ratio)}:1`));
    const motion = scene.motion;
    const reveal = motion ? `<p>Reveal: ${motion.reduced ? 'reduced motion' : motion.samples.filter(([,v]) => v > 0 && v < 1).length + ' intermediate opacity samples'}. Final opacity ${motion.finalOpacity}.</p>` : '';
    return `<article id="${scene.id}"><h2>${scene.page} · ${scene.design} · ${scene.mode}</h2>
      ${scene.unsettled.length ? '<p>Capture did not settle: '+scene.unsettled.join(', ')+'</p>' : ''}
      ${targets.join('')}<div class="pair">${contrast.join('')}</div>${reveal}</article>`;
  });
  return `<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
  <title>Desktop interaction review</title><style>
  *{box-sizing:border-box}body{font:16px/1.45 system-ui,sans-serif;color:#203242;background:#fafafa;margin:0 auto;max-width:1220px;padding:24px}
  h1,h2,h4{line-height:1.2}article{background:white;border:1px solid #ccd3da;padding:20px;margin:20px 0}
  .pair,.strip{display:flex;align-items:flex-start;gap:12px;flex-wrap:wrap}figure{margin:0;max-width:100%}img{max-width:100%;display:block}.zoom{zoom:2;image-rendering:pixelated}
  table{border-collapse:collapse}th,td{text-align:left;padding:5px 10px;border-bottom:1px solid #d6dce1}
  figcaption,small{color:#455768;font-size:13px}summary{cursor:pointer;padding:10px 0;font-weight:600}.strip section{padding:8px;border:1px solid #ddd}a{color:#155888}</style>
  <h1>Desktop interaction review</h1><p>${time} · <a href="measurements.json">Measurements and environment</a></p>
  <div style="overflow:auto"><table><thead><tr><th>Scene</th><th>Control</th><th>Hover envelopes (px)</th><th>Exit residual (px)</th><th>Text − icon</th><th>Official specimen</th></tr></thead><tbody>${rows.join('')}</tbody></table></div>
  <p>Blue shows changes above the noise at each pixel in two rest frames. Dark blue starts at the higher of the two most frequent nonzero RGB differences. Envelopes include gaps. Ink centers use four cutoffs from 20% to 65% of the strongest channel difference from the local background. Negative offsets place text above the icon. Contrast compares the dominant background with the highest-contrast pixel in the crop. Use the official component and platform guidance to interpret differences.</p>
  ${sections.join('')}</html>`;
}

async function subdirectories(path) {
  const entries = await readdir(path, {withFileTypes: true});
  return entries.filter(entry => entry.isDirectory()).map(entry => entry.name).sort();
}

async function captureReferences(scenes, serve, directory) {
  const requests = new Map();
  for (const scene of scenes) {
    for (const target of Object.values(scene.components)) {
      const key = target.alignment?.reference;
      if (key) requests.set(key + '-' + scene.mode, [key, scene.mode]);
    }
  }
  const refs = {};
  if (!requests.size) return { refs };
  const browser = await chromium.launch({args:['--disable-gpu']});
  try {
    const {server, url} = await serve();
    try {
      const page = await browser.newPage({viewport:{width:1120,height:1100},deviceScaleFactor:1});
      for (const [id, [key, mode]] of requests) refs[id] = await reference(page, url, key, mode, directory);
    } finally { await new Promise(done => server.close(done)); }
    return { refs, browser: browser.version() };
  } finally { await browser.close(); }
}

export async function desktopReview({ repository, run, serve, dependencies }) {
  const root = join(repository, '.local/desktop-review');
  await mkdir(root, { recursive: true });
  const directory = await mkdtemp(join(root,'run-'));
  const input = await mkdtemp(join(tmpdir(),'lazytuihub-visual-'));
  try {
    await run('dotnet', ['run','--project','tests/LazyTuiHub.Tests','--','-trait','DesktopReview=true'],
      {cwd:repository, env:{ ...process.env, LAZYTUIHUB_VISUAL_REVIEW:input }});
    const scenes = [];
    for (const page of await subdirectories(input)) {
      for (const variant of await subdirectories(join(input, page))) {
        scenes.push(await analyzeScene(join(input, page, variant), directory));
      }
    }
    if (!scenes.length) throw new Error('No DesktopReview scenes emitted measurements.');
    const { refs, browser } = await captureReferences(scenes, serve, directory);
    const time = new Date().toISOString();
    await writeFile(join(directory,'measurements.json'),JSON.stringify({time,browser,dependencies,refs,scenes},null,2));
    await writeFile(join(directory,'index.html'),reportHtml(scenes,refs,time));
    const link = `${basename(directory)}/index.html`;
    const redirect = join(directory,'latest.html');
    await writeFile(redirect,`<!doctype html><meta charset="utf-8"><meta http-equiv="refresh" content="0;url=${link}"><a href="${link}">Latest interaction review</a>`);
    await rename(redirect,join(root,'index.html'));
    console.log(`Desktop interaction review: ${join(root,'index.html')}`);
  } catch (error) {
    await rm(directory, {recursive:true,force:true});
    throw error;
  } finally { await rm(input, {recursive:true,force:true}); }
}
