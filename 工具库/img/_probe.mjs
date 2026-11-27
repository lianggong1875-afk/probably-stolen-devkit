import { createRequire } from "node:module"
const sharp = createRequire("D:/Deepseek Harness EAC/dsh-desktop/node_modules/")("sharp")
const f = "D:/0TavernWeave/probably-stolen/assets/freya-hd-source.webp"
const { data, info } = await sharp(f).ensureAlpha().raw().toBuffer({ resolveWithObject: true })
const W = info.width, H = info.height, N = W*H
let opaque=0, partial=0, transparent=0
let partialRgbZero=0, partialLight=0
const hist = new Array(9).fill(0)
for(let i=0;i<N;i++){
  const o=i*4, a=data[o+3]
  if(a===255) opaque++
  else if(a===0) transparent++
  else {
    partial++
    const r=data[o],g=data[o+1],b=data[o+2]
    if(r===0&&g===0&&b===0) partialRgbZero++
    else if(Math.min(r,g,b)>=200) partialLight++
    hist[Math.min(8, Math.floor(a/32))]++
  }
}
console.log(`opaque=${(opaque/N*100).toFixed(1)}%  partial=${(partial/N*100).toFixed(2)}%  transparent=${(transparent/N*100).toFixed(1)}%`)
console.log(`半透明像素中 RGB=0(纯黑) 的占 ${(partialRgbZero/Math.max(1,partial)*100).toFixed(1)}%  偏亮(>=200) 的占 ${(partialLight/Math.max(1,partial)*100).toFixed(1)}%`)
console.log(`alpha 分档(0..255 每32一档): ${hist.join(' | ')}`)
// 取样几个边缘像素
let shown=0
for(let y=0;y<H&&shown<6;y+=7){ for(let x=0;x<W&&shown<6;x+=7){ const o=(y*W+x)*4; const a=data[o+3]; if(a>0&&a<255){ console.log(`  edge px(${x},${y}) rgba=${data[o]},${data[o+1]},${data[o+2]},${a}`); shown++ } } }