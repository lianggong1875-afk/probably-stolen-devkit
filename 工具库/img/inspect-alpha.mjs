#!/usr/bin/env node
// 检查被抠过的 PNG 里，全透明像素的 RGB 是否还保留着原始颜色。
// 如果保留，就能反推出原始白底图，重新做一次正确的抠图。

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

const sharp = loadSharp()
const file = process.argv[2]
if (!file) { console.error('usage: node inspect-alpha.mjs <png>'); process.exit(2) }

const { data, info } = await sharp(file).ensureAlpha().raw().toBuffer({ resolveWithObject: true })
const { width, height, channels } = info
const n = width * height

let transparent = 0, zeroed = 0, preserved = 0
let nearWhite = 0, nearBlack = 0
const samples = []
for (let i = 0; i < n; i++) {
  const o = i * 4
  if (data[o + 3] !== 0) continue
  transparent++
  const r = data[o], g = data[o + 1], b = data[o + 2]
  if (r === 0 && g === 0 && b === 0) { zeroed++; continue }
  preserved++
  if (r >= 250 && g >= 250 && b >= 250) nearWhite++
  else if (r < 20 && g < 20 && b < 20) nearBlack++
  if (samples.length < 8) samples.push(`${r},${g},${b}`)
}

console.log(`file         ${file}`)
console.log(`size         ${width}x${height} ch=${channels}`)
console.log(`transparent  ${transparent} (${(transparent / n * 100).toFixed(1)}%)`)
console.log(`  rgb zeroed ${zeroed}`)
console.log(`  rgb kept   ${preserved}`)
console.log(`  of kept: nearWhite=${nearWhite} nearBlack=${nearBlack}`)
console.log(`samples      ${samples.join(' | ')}`)
console.log(preserved > transparent * 0.9
  ? '=> RGB 基本保留，可以反推原始白底图'
  : '=> RGB 大多被清零，无法反推')
