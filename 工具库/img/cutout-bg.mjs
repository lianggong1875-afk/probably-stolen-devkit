#!/usr/bin/env node
/**
 * cutout-bg.mjs — 白底立绘抠图（连通域版）
 *
 * 为什么不能用"亮就清掉"的简单阈值：
 *   芙蕾雅是银白长发 + 白外套，人物身上的高光和纯白背景的数值完全重叠。
 *   一刀切下去会把头发和衣服挖出一堆洞。
 *
 * 正确做法：只清除**与画面边缘连通**的近白像素（flood fill from border）。
 *   人物内部被头发/衣服围住的白底缝隙不会被误删。
 *   边缘再做一圈按亮度衰减的羽化，压掉白边光晕。
 *
 * 另外：本工具会把所有 alpha 先恢复成 255。
 *   被早期脚本抠坏的图，RGB 通常还在（那些脚本只改 alpha），
 *   所以这一步能把原图反推回来，重新抠一次。
 *
 * 用法：
 *   node cutout-bg.mjs --in 源.png --out 目标.png [--threshold 238] [--spread 16] [--feather 40] [--debug]
 *   --debug 额外输出一张 alpha=0 区域涂成品红的可视化图，用来检查有没有挖穿人物
 */

import fs from 'node:fs'
import path from 'node:path'
import { createRequire } from 'node:module'

function loadSharp() {
  for (const base of [
    'D:/Deepseek Harness EAC/dsh-desktop/node_modules/',
    'D:/Deepseek Harness EAC/dsh-desktop/',
  ]) {
    try { return createRequire(base)('sharp') } catch {}
  }
  throw new Error('sharp not found under dsh-desktop/node_modules')
}

const argv = process.argv.slice(2)
const opt = { threshold: 238, spread: 16, feather: 40, debug: false }
for (let i = 0; i < argv.length; i++) {
  const a = argv[i]
  if (a === '--in') opt.in = argv[++i]
  else if (a === '--out') opt.out = argv[++i]
  else if (a === '--threshold') opt.threshold = Number(argv[++i])
  else if (a === '--spread') opt.spread = Number(argv[++i])
  else if (a === '--feather') opt.feather = Number(argv[++i])
  else if (a === '--debug') opt.debug = true
  else if (a === '--help') opt.help = true
}
if (opt.help || !opt.in || !opt.out) {
  console.error('usage: node cutout-bg.mjs --in src.png --out dst.png [--threshold 238] [--spread 16] [--feather 40] [--debug]')
  process.exit(2)
}

const sharp = loadSharp()
const { data, info } = await sharp(opt.in).ensureAlpha().raw().toBuffer({ resolveWithObject: true })
const W = info.width, H = info.height
const N = W * H
if (info.channels !== 4) throw new Error(`expected RGBA, got ${info.channels}`)

// 1) 还原成不透明原图 —— 旧脚本只改了 alpha，RGB 还在
let restored = 0
for (let i = 0; i < N; i++) {
  if (data[i * 4 + 3] !== 255) restored++
  data[i * 4 + 3] = 255
}

// 2) 标出"近白"候选
const T = opt.threshold, S = opt.spread
const white = new Uint8Array(N)
for (let i = 0; i < N; i++) {
  const o = i * 4
  const r = data[o], g = data[o + 1], b = data[o + 2]
  const mn = Math.min(r, g, b), mx = Math.max(r, g, b)
  white[i] = mn >= T && mx - mn <= S ? 1 : 0
}

// 3) 从四边 flood fill，只沿近白像素扩散
const bg = new Uint8Array(N)
const stack = new Int32Array(N)
let sp = 0
const seed = (i) => {
  if (white[i] && !bg[i]) { bg[i] = 1; stack[sp++] = i }
}
for (let x = 0; x < W; x++) { seed(x); seed((H - 1) * W + x) }
for (let y = 0; y < H; y++) { seed(y * W); seed(y * W + W - 1) }
while (sp > 0) {
  const i = stack[--sp]
  const x = i % W, y = (i - x) / W
  if (x > 0) seed(i - 1)
  if (x < W - 1) seed(i + 1)
  if (y > 0) seed(i - W)
  if (y < H - 1) seed(i + W)
}

// 4) 清背景 + 边缘羽化
let cleared = 0, feathered = 0
for (let i = 0; i < N; i++) {
  if (bg[i]) { data[i * 4 + 3] = 0; cleared++ }
}
const F = Math.max(1, opt.feather)
for (let y = 0; y < H; y++) {
  for (let x = 0; x < W; x++) {
    const i = y * W + x
    if (bg[i]) continue
    let adjacent = false
    for (let dy = -1; dy <= 1 && !adjacent; dy++) {
      const ny = y + dy
      if (ny < 0 || ny >= H) continue
      for (let dx = -1; dx <= 1; dx++) {
        const nx = x + dx
        if (nx < 0 || nx >= W) continue
        if (bg[ny * W + nx]) { adjacent = true; break }
      }
    }
    if (!adjacent) continue
    const o = i * 4
    const mn = Math.min(data[o], data[o + 1], data[o + 2])
    const lo = T - F
    if (mn >= lo) {
      const k = (mn - lo) / F
      data[o + 3] = Math.max(0, Math.round(255 * (1 - Math.min(1, k))))
      feathered++
    }
  }
}

// 5) 内容包围盒（给游戏里算 ppu / pivot 用）
let bx = W, by = H, bw = 0, bh = 0
{
  let minX = W, maxX = -1, minY = H, maxY = -1
  for (let y = 0; y < H; y++) {
    for (let x = 0; x < W; x++) {
      if (data[(y * W + x) * 4 + 3] <= 8) continue
      if (x < minX) minX = x
      if (x > maxX) maxX = x
      if (y < minY) minY = y
      if (y > maxY) maxY = y
    }
  }
  if (maxX >= minX) { bx = minX; by = minY; bw = maxX - minX + 1; bh = maxY - minY + 1 }
}

fs.mkdirSync(path.dirname(path.resolve(opt.out)), { recursive: true })
const buf = await sharp(data, { raw: { width: W, height: H, channels: 4 } })
  .png({ compressionLevel: 9 })
  .toBuffer()
fs.writeFileSync(opt.out, buf)

// 6) 可视化：alpha=0 涂品红，一眼看出有没有挖穿人物
if (opt.debug) {
  const dbg = Buffer.from(data)
  for (let i = 0; i < N; i++) {
    const o = i * 4
    if (dbg[o + 3] <= 8) { dbg[o] = 255; dbg[o + 1] = 0; dbg[o + 2] = 255; dbg[o + 3] = 255 }
  }
  const dbgOut = opt.out.replace(/\.png$/i, '') + '.debug.png'
  fs.writeFileSync(dbgOut, await sharp(dbg, { raw: { width: W, height: H, channels: 4 } }).png().toBuffer())
  console.error(`debug       ${dbgOut}`)
}

// by 用图片顶部为原点的坐标，这里额外报一份 Unity（左下原点）的
const unityBy = H - (by + bh)
console.error(
  `cutout      ${W}x${H} -> ${opt.out}  ${(buf.length / 1024).toFixed(0)} KB\n` +
  `  restored opaque ${restored}\n` +
  `  cleared ${cleared} (${(cleared / N * 100).toFixed(1)}%)  feathered ${feathered}\n` +
  `  content ${bw}x${bh}  top-left-origin bbox=(${bx},${by})  unity bboxY=${unityBy}`
)
