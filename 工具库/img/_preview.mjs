import { createRequire } from "node:module"
const req = createRequire("D:/Deepseek Harness EAC/dsh-desktop/node_modules/")
const sharp = req("sharp")
const src = "D:/0TavernWeave/probably-stolen/mods/freya/Textures/freya_portrait.fix.png"
const meta = await sharp(src).metadata()
const bg = await sharp({ create: { width: meta.width, height: meta.height, channels: 3, background: { r: 46, g: 52, b: 58 } } }).png().toBuffer()
const composed = await sharp(bg).composite([{ input: src }]).png().toBuffer()
await sharp(composed).png().toFile("D:/0TavernWeave/probably-stolen/shots/portrait-dark-preview.png")
await sharp(composed).extract({ left: 280, top: 180, width: 460, height: 320 }).resize(920, 640, { kernel: "nearest" }).png().toFile("D:/0TavernWeave/probably-stolen/shots/portrait-edge-crop.png")
console.log("preview ok")