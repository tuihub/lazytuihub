import assert from 'node:assert/strict';
import test from 'node:test';
import { PNG } from 'pngjs';
import { alignment, difference, pixelBox } from './review-pixels.mjs';

function canvas(width, height) {
  const image = new PNG({ width, height });
  image.data.fill(255);
  return image;
}

function paint(image, [x, y, width, height], gray) {
  for (let row = y; row < y + height; row++) {
    for (let column = x; column < x + width; column++) {
      const i = (row * image.width + column) * 4;
      image.data.fill(gray, i, i + 3);
    }
  }
}

test('separates nested paint bounds and discounts only local repeat noise', () => {
  const rest = canvas(12, 12), repeat = canvas(12, 12), hover = canvas(12, 12);
  paint(repeat, [0, 0, 1, 1], 180);
  paint(hover, [0, 0, 1, 1], 180);
  paint(hover, [1, 1, 10, 10], 245);
  paint(hover, [3, 2, 6, 8], 200);
  const { stats } = difference(rest, hover, rest, repeat);
  assert.equal(stats.noisy, 1);
  assert.equal(stats.raw, 101);
  assert.equal(stats.changed, 100);
  assert.deepEqual(stats.outer, [1, 1, 10, 10]);
  assert.deepEqual(stats.inner, [3, 2, 6, 8]);
  assert.equal(difference(rest, rest, rest, repeat).stats.outer, null);
});

test('optical offset follows the ink at either polarity and converts device pixels to DIPs', () => {
  const boxes = { icon: [0, 0, 5, 8], label: [5, 0, 5, 8] };
  const frame = canvas(20, 16);
  assert.ok(alignment(frame, boxes, 2).offsets.every(value => value === null));
  paint(frame, [2, 6, 6, 6], 22);
  paint(frame, [12, 2, 6, 6], 22);
  for (const invert of [false, true]) {
    if (invert) for (let i = 0; i < frame.data.length; i++) {
      if (i % 4 !== 3) frame.data[i] = 255 - frame.data[i];
    }
    assert.deepEqual(alignment(frame, boxes, 2).offsets, [-2, -2, -2, -2]);
  }
  assert.deepEqual(pixelBox([0.25, 0.25, 2, 3], 2), [0, 0, 5, 7]);
});
