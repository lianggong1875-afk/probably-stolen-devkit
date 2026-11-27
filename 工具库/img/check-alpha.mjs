import { createRequire } from "node:module"
const req = createRequire("D:/Deepseek Harness EAC/dsh-desktop/node_modules/")
const sharp = req("sharp")
const f = process.argv[2]
const { data, info } = await sharp(f).ensureAlpha().raw().toBuffer({ resolveWithObject: true })
const { width, height } = info
const px = (x, y) => { const i = (y * width + x) * 4; return [data[i], data[i+1], data[i+2], data[i+3]] }
console.log("size", width + "x" + height)
for (const [x, y, label] of [[5,5,"左上角"],[width-5,5,"右上角"],[5,height-5,"左下角"],[width>>1,10,"顶部中央"],[width>>1,height>>1,"正中(人物)"],[width>>1,height-5,"底部中央"],[130,140,"呆毛附近"]]) {
  console.log(label, `(${x},${y})`, "rgba=" + px(x,y).join(","))
}
let opaque = 0, transparent = 0, partial = 0
for (let i = 3; i < data.length; i += 4) {
  if (data[i] === 0) transparent++
  else if (data[i] === 255) opaque++
  else partial++
}
const total = width * height
console.log("alpha: 全透明 " + (transparent*100/total).toFixed(1) + "%, 不透明 " + (opaque*100/total).toFixed(1) + "%, 半透明 " + (partial*100/total).toFixed(2) + "%")