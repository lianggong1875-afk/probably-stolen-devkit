#!/usr/bin/env node
/**
 * trim.mjs — 只裁掉四周全透明边，不动任何像素值。
 *
 * 为什么单独一个：process-portrait.mjs 的 --trim 会连带重跑一遍白底判定，
 * 对已经去过背景的图是多余的风险。立绘的透明边必须裁掉，否则
 * Sprite.Create 的底部锚点会落在空白上，人物整体被顶出画面。
 *
 *   node trim.mjs --in 源.png --out 目标.png [--threshold 1]
 */

import fs from 'node:fs'
import path from 'node:path'
import { createRequire } from 'node:module'

function loadSharp() {
  for (const base of [
    'D:/Deepseek Harness EAC/dsh-desktop/node_modules/',
    'D:/Deepseek Harness EAC/dsh-desktop/',
  ]) {
    try {
      return createRequire(base)('sharp')
    } catch {}
  }
  throw new Error('sharp not found under dsh-desktop/node_modules')
}

const argv = process.argv.slice(2)
const opt = {}
for (let i = 0; i < argv.length; i++) {
  const a = argv[i]
  if (a === '--in') opt.in = argv[++i]
  else if (a === '--out') opt.out = argv[++i]
  else if (a === '--threshold') opt.threshold = Number(argv[++i])
  else if (a === '--help') opt.help = true
}

if (opt.help || !opt.in || !opt.out) {
  console.error('usage: node trim.mjs --in src.png --out dst.png [--threshold 1]')
  process.exit(2)
}

const sharp = loadSharp()
const before = await sharp(opt.in).metadata()
const png = await sharp(opt.in).ensureAlpha().png().toBuffer()
const result = await sharp(png)
  .trim({ threshold: Number.isFinite(opt.threshold) ? opt.threshold : 1 })
  .png({ compressionLevel: 9 })
  .toBuffer({ resolveWithObject: true })

fs.mkdirSync(path.dirname(path.resolve(opt.out)), { recursive: true })
fs.writeFileSync(opt.out, result.data)

console.log(
  `trim ${before.width}x${before.height} -> ${result.info.width}x${result.info.height} ` +
    `${(result.data.length / 1024).toFixed(0)} KB -> ${opt.out}`,
)
