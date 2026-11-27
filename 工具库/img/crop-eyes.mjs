import { createRequire } from "node:module"
const req = createRequire("D:/Deepseek Harness EAC/dsh-desktop/node_modules/")
const sharp = req("sharp")
const [src, out] = process.argv.slice(2)
// 脸部上半：眼睛一带
await sharp(src).extract({ left: 420, top: 290, width: 200, height: 110 }).resize(800, 440, { kernel: "nearest" }).png().toFile(out)
console.log("cropped ->", out)