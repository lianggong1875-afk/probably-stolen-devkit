#!/usr/bin/env node
/**
 * recolor-eyes.mjs — 把立绘的虹膜调成明确的蓝色。
 *
 * 背景：原图虹膜是淡灰蓝（HSL 里饱和度只有 0.15 左右，看起来发灰），
 * 而眼角的青色装饰标记饱和度很高（#40c2b6，色相约 176°）。
 * 两者色相不同，可以只提虹膜不动标记。
 *
 * 做法：只在指定的眼睛矩形内，对「色相 185°–255° 且饱和度 > 0.12」的像素
 * 把色相拉到目标蓝、饱和度放大，明度保持不变（保留高光和阴影）。
 *
 * 用法：
 *   node recolor-eyes.mjs --in a.png --out b.png [--hue 214] [--sat 2.4]
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
  throw new Error('sharp not found')
}

const argv = process.argv.slice(2)
const opt = { hue: 214, sat: 2.4, minSat: 0.10 }
for (let i = 0; i < argv.length; i++) {
  const a = argv[i]
  if (a === '--in') opt.in = argv[++i]
  else if (a === '--out') opt.out = argv[++i]
  else if (a === '--hue') opt.hue = Number(argv[++i])
  else if (a === '--sat') opt.sat = Number(argv[++i])
  else if (a === '--min-sat') opt.minSat = Number(argv[++i])
  else if (a === '--help') opt.help = true
}
if (opt.help || !opt.in || !opt.out) {
  process.stdout.write(
    fs.readFileSync(new URL(import.meta.url), 'utf8').split('*/')[0].replace(/^#![^\n]*\n/, '') + '\n',
  )
  process.exit(0)
}

const sharp = loadSharp()
const { data, info } = await sharp(opt.in).ensureAlpha().raw().toBuffer({ resolveWithObject: true })
const { width, height } = info

// 眼睛矩形（按 1024x765 的这张图画定；换了图要重算）
const boxes = [
  { x0: 432, x1: 500, y0: 315, y1: 372 },   // 左眼
  { x0: 532, x1: 600, y0: 315, y1: 372 },   // 右眼
]

function rgb2hsl(r, g, b) {
  r /= 255; g /= 255; b /= 255
  const max = Math.max(r, g, b), min = Math.min(r, g, b)
  const l = (max + min) / 2
  if (max === min) return [0, 0, l]
  const d = max - min
  const s = l > 0.5 ? d / (2 - max - min) : d / (max + min)
  let h
  if (max === r) h = ((g - b) / d + (g < b ? 6 : 0))
  else if (max === g) h = (b - r) / d + 2
  else h = (r - g) / d + 4
  return [h * 60, s, l]
}

function hsl2rgb(h, s, l) {
  h = ((h % 360) + 360) % 360
  const c = (1 - Math.abs(2 * l - 1)) * s
  const x = c * (1 - Math.abs(((h / 60) % 2) - 1))
  const m = l - c / 2
  let r, g, b
  if (h < 60) [r, g, b] = [c, x, 0]
  else if (h < 120) [r, g, b] = [x, c, 0]
  else if (h < 180) [r, g, b] = [0, c, x]
  else if (h < 240) [r, g, b] = [0, x, c]
  else if (h < 300) [r, g, b] = [x, 0, c]
  else [r, g, b] = [c, 0, x]
  return [(r + m) * 255, (g + m) * 255, (b + m) * 255]
}

let changed = 0
for (const box of boxes) {
  for (let y = box.y0; y < box.y1; y++) {
    for (let x = box.x0; x < box.x1; x++) {
      const i = (y * width + x) * 4
      if (data[i + 3] < 8) continue
      const [h, s, l] = rgb2hsl(data[i], data[i + 1], data[i + 2])
      if (h < 185 || h > 255) continue      // 避开青色装饰标记(~176°)与皮肤(~25°)
      if (s < opt.minSat) continue          // 避开眼白（几乎无饱和）
      if (l < 0.12 || l > 0.95) continue
      const ns = Math.min(1, s * opt.sat)
      const [r, g, b] = hsl2rgb(opt.hue, ns, l)
      data[i] = Math.round(r)
      data[i + 1] = Math.round(g)
      data[i + 2] = Math.round(b)
      changed++
    }
  }
}

fs.mkdirSync(path.dirname(path.resolve(opt.out)), { recursive: true })
const buf = await sharp(data, { raw: { width, height, channels: 4 } }).png({ compressionLevel: 9 }).toBuffer()
fs.writeFileSync(opt.out, buf)
process.stderr.write(`✓ 瞳色调整完成，改动 ${changed} px → ${path.basename(opt.out)} (${(buf.length/1024).toFixed(0)} KB)\n`)
