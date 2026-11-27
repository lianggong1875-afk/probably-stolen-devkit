import fs from "node:fs"
import { createRequire } from "node:module"
const sharp = createRequire("D:/Deepseek Harness EAC/dsh-desktop/node_modules/")("sharp")
const src = "D:/0TavernWeave/probably-stolen/assets/freya-hd-source.webp"
const out = "D:/0TavernWeave/probably-stolen/mods/freya/Textures/freya_portrait.png"
const m = await sharp(src).metadata()
const targetH = 1100
const targetW = Math.round(m.width * targetH / m.height)
await sharp(src).ensureAlpha()
  .resize({ width: targetW, height: targetH, kernel: "lanczos3" })
  .png({ compressionLevel: 9 })
  .toFile(out)
const om = await sharp(out).metadata()
console.log(`resized ${m.width}x${m.height} -> ${om.width}x${om.height}  ${(fs.statSync(out).size/1024).toFixed(0)} KB`)
const { data, info } = await sharp(out).ensureAlpha().raw().toBuffer({ resolveWithObject: true })
const W = info.width, H = info.height
let minX=W,maxX=-1,minY=H,maxY=-1
for(let y=0;y<H;y++) for(let x=0;x<W;x++){ if(data[(y*W+x)*4+3]<=8) continue; if(x<minX)minX=x; if(x>maxX)maxX=x; if(y<minY)minY=y; if(y>maxY)maxY=y }
console.log(`content bbox top-origin=(${minX},${minY}) size=${maxX-minX+1}x${maxY-minY+1}`)
console.log(`expected game log: content=${maxX-minX+1}x${maxY-minY+1}@(${minX},${H-(maxY+1)})`)