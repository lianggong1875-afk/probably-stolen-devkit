#!/usr/bin/env node
/**
 * process-portrait.mjs — 把角色立绘处理成游戏能用的 PNG：
 *   1. 白底转透明（含边缘羽化，避免锯齿白边）
 *   2. 保留原始像素尺寸（缩放交给游戏里的 Sprite.Create PPU）
 *   3. 可选 -trim 裁掉四周全透明边
 *
 * 用 sharp（dsh-desktop 自带），不装依赖。
 *
 * 用法：
 *   node process-portrait.mjs --in 源.png --out 目标.png [--threshold 235] [--feather 18] [--trim]
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
      const req = createRequire(base)
      return req('sharp')
    } catch {}
  }
  throw new Error('sharp not found under dsh-desktop/node_modules')
}

const argv = process.argv.slice(2)
const opt = { threshold: 235, feather: 18, trim: false }
for (let i = 0; i < argv.length; i++) {
  const a = argv[i]
  if (a === '--in') opt.in = argv[++i]
  else if (a === '--out') opt.out = argv[++i]
  else if (a === '--threshold') opt.threshold = Number(argv[++i])
  else if (a === '--feather') opt.feather = Number(argv[++i])
  else if (a === '--trim') opt.trim = true
  else if (a === '--help') opt.help = true
}

if (opt.help || !opt.in || !opt.out) {
  process.stdout.write(
    fs.readFileSync(new URL(import.meta.url), 'utf8').split('*/')[0].replace(/^#![^\n]*\n/, '') + '\n',
  )
  process.exit(0)
}

const sharp = loadSharp()

const img = sharp(opt.in).ensureAlpha()
const meta = await sharp(opt.in).metadata()
const { data, info } = await img.raw().toBuffer({ resolveWithObject: true })
const { width, height, channels } = info
if (channels !== 4) throw new Error(`expected RGBA, got ${channels} channels`)

// 白底判定：三通道都高于阈值 → 透明；接近阈值的做线性羽化，避免锯齿白边。
const t = opt.threshold
const f = Math.max(1, opt.feather)
let cleared = 0
for (let i = 0; i < data.length; i += 4) {
  const r = data[i], g = data[i + 1], b = data[i + 2]
  const minC = Math.min(r, g, b)
  const maxC = Math.max(r, g, b)
  const isWhitish = minC >= t - f && maxC - minC <= 24   // 近中性色，避免误删浅色皮肤
  if (!isWhitish) continue

  if (minC >= t) {
    data[i + 3] = 0
    cleared++
  } else {
    // 阈值附近的边缘：按亮度线性降到半透明，压掉白边光晕
    const k = (minC - (t - f)) / f          // 0..1
    data[i + 3] = Math.round(data[i + 3] * (1 - k))
    cleared++
  }
}

let out = sharp(data, { raw: { width, height, channels: 4 } })

if (opt.trim) {
  // trim 需要基于 alpha，先转一次 png buffer 再裁
  const buf = await out.png().toBuffer()
  out = sharp(buf).trim({ threshold: 1 })
}

fs.mkdirSync(path.dirname(path.resolve(opt.out)), { recursive: true })
const finalBuf = await out.png({ compressionLevel: 9 }).toBuffer()
fs.writeFileSync(opt.out, finalBuf)

const after = await sharp(opt.out).metadata()
process.stderr.write(
  `✓ ${path.basename(opt.out)}  ${after.width}x${after.height}  ` +
  `${(finalBuf.length / 1024).toFixed(0)} KB  ` +
  `(源 ${meta.width}x${meta.height} ${meta.format}, 清掉 ${cleared} px 背景)\n`,
)
