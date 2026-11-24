import { PNG } from 'pngjs';

export function pixelBox([x, y, w, h], scale, margin = 0) {
  const left = Math.floor((x - margin) * scale);
  const top = Math.floor((y - margin) * scale);
  return [left, top, Math.ceil((x + w + margin) * scale) - left, Math.ceil((y + h + margin) * scale) - top];
}

export function crop(image, [x, y, width, height]) {
  if (x < 0 || y < 0 || x + width > image.width || y + height > image.height || !width || !height)
    throw new Error(`Region ${[x,y,width,height]} outside ${image.width} × ${image.height}`);
  const result = new PNG({ width, height });
  PNG.bitblt(image, result, x, y, width, height, 0, 0);
  return result;
}

function distance(a, b, i) {
  return Math.max(Math.abs(a[i] - b[i]), Math.abs(a[i+1] - b[i+1]), Math.abs(a[i+2] - b[i+2]));
}

function envelope(values, width, threshold) {
  let left = width, top = values.length, right = -1, bottom = -1;
  for (let i = 0; i < values.length; i++) {
    if (values[i] < threshold) continue;
    const x = i % width, y = Math.floor(i / width);
    left = Math.min(left, x); right = Math.max(right, x);
    top = Math.min(top, y); bottom = Math.max(bottom, y);
  }
  return right < 0 ? null : [left, top, right - left + 1, bottom - top + 1];
}

export function difference(before, after, rest, repeat) {
  const { width, height } = before;
  const values = new Uint8Array(width * height), histogram = new Uint32Array(256);
  let noisy = 0, raw = 0;
  for (let p = 0; p < values.length; p++) {
    const delta = distance(before.data, after.data, p * 4);
    const noise = distance(rest.data, repeat.data, p * 4);
    if (noise) noisy++;
    if (delta) raw++;
    values[p] = delta > noise ? delta : 0;
    histogram[values[p]]++;
  }
  // Find the two most common nonzero paint changes. Isolated high-contrast pixels
  // (for example, antialiased text) should not set the color scale for an entire tile.
  const levels = Array.from(histogram.keys()).filter(i => i > 0 && histogram[i])
    .sort((a, b) => histogram[b] - histogram[a] || a - b);
  const stronger = levels.length > 1 ? Math.max(levels[0], levels[1]) : null;
  const mask = new PNG({width, height});
  for (let p = 0; p < values.length; p++) {
    const rgb = values[p] === 0 ? [244,245,247] : stronger && values[p] >= stronger ? [21,80,123] : [112,180,212];
    mask.data.set([...rgb, 255], p * 4);
  }
  return { mask, stats: { raw, noisy, changed: values.length - histogram[0],
    outer: envelope(values, width, 1), stronger, inner: stronger && envelope(values, width, stronger),
    levels: levels.map(level => [level, histogram[level]]) } };
}

function luminance(rgb) {
  const [r,g,b] = rgb.map(byte => {
    const s = byte / 255;
    return s <= 0.04045 ? s / 12.92 : ((s + 0.055) / 1.055) ** 2.4;
  });
  return 0.2126*r + 0.7152*g + 0.0722*b;
}

export function ink(image) {
  const colors = new Map();
  const { data, width, height } = image;
  for (let i = 0; i < data.length; i += 4) {
    const key = data.readUIntBE(i, 3);
    colors.set(key, (colors.get(key) ?? 0) + 1);
  }
  const [bg] = [...colors].sort((a,b) => b[1]-a[1])[0];
  const background = [bg >> 16, bg >> 8 & 255, bg & 255];
  const values = new Uint8Array(width * height);
  let maximum = 0, contrast = 1;
  const base = luminance(background);
  for (const key of colors.keys()) {
    const value = luminance([key >> 16, key >> 8 & 255, key & 255]);
    contrast = Math.max(contrast, (Math.max(base,value)+0.05) / (Math.min(base,value)+0.05));
  }
  for (let p = 0; p < values.length; p++) {
    values[p] = Math.max(...background.map((channel, i) => Math.abs(channel - data[p*4+i])));
    maximum = Math.max(maximum, values[p]);
  }
  // Sweep antialias coverage; the individual cutoffs do not decide acceptance.
  const centers = [0.2, 0.35, 0.5, 0.65].map(fraction => {
    const box = maximum && envelope(values, width, Math.ceil(maximum * fraction));
    return box ? box[1] + box[3] / 2 : null;
  });
  return { background, maximum, contrast, centers };
}

export function alignment(image, boxes, scale) {
  const parts = ['icon','label'].map(key => {
    const box = pixelBox(boxes[key], scale);
    return { box, ...ink(crop(image, box)) };
  });
  const [icon, label] = parts;
  const offsets = label.centers.map((y, i) => y == null || icon.centers[i] == null ? null :
    (label.box[1] + y - icon.box[1] - icon.centers[i]) / scale);
  return { icon, label, offsets };
}
