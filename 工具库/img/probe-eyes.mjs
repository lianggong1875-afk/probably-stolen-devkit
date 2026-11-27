import { createRequire } from "node:module"
const req = createRequire("D:/Deepseek Harness EAC/dsh-desktop/node_modules/")
const sharp = req("sharp")
const f = process.argv[2]
const { data, info } = await sharp(f).ensureAlpha().raw().toBuffer({ resolveWithObject: true })
const { width, height } = info
const px = (x, y) => { const i = (y * width + x) * 4; return [data[i], data[i+1], data[i+2]] }
const hex = c => "#" + c.map(v => v.toString(16).padStart(2,"0")).join("")
// 在眼睛一带扫一片，找出饱和度最高的若干颜色（瞳孔/虹膜颜色最饱和）
const y0 = 300, y1 = 400, x0 = 400, x1 = 660
const hits = []
for (let y = y0; y < y1; y += 2) {
  for (let x = x0; x < x1; x += 2) {
    const [r,g,b] = px(x,y)
    const max = Math.max(r,g,b), min = Math.min(r,g,b)
    const sat = max - min
    if (sat > 28 && max > 60 && max < 245) hits.push({ x, y, r, g, b, sat })
  }
}
hits.sort((a,b) => b.sat - a.sat)
console.log("最饱和的 12 个点（可能含虹膜/装饰色）：")
for (const h of hits.slice(0,12)) console.log(`  (${h.x},${h.y}) ${hex([h.r,h.g,h.b])} sat=${h.sat}`)
// 统计色相分布
const buckets = {}
for (const h of hits) {
  let hue
  const max = Math.max(h.r,h.g,h.b), min = Math.min(h.r,h.g,h.b), d = max-min
  if (d === 0) continue
  if (max === h.r) hue = 60 * (((h.g-h.b)/d) % 6)
  else if (max === h.g) hue = 60 * ((h.b-h.r)/d + 2)
  else hue = 60 * ((h.r-h.g)/d + 4)
  if (hue < 0) hue += 360
  const band = Math.floor(hue/30)*30
  buckets[band] = (buckets[band]||0)+1
}
console.log("色相分布（30° 一档）：")
Object.keys(buckets).sort((a,b)=>buckets[b]-buckets[a]).forEach(k => console.log(`  ${k}°-${+k+30}°: ${buckets[k]}`))