# -*- coding: utf-8 -*-
"""
newsprint-tone.py —— 《深空当铺》Better Newspapers 风格报纸配图转换工具
（1:1 复刻 Better Newspapers 的 ClippingPress.cs 9 色油墨色板与 2×2 拜耳半色调抖动）

用法:
  将任意输入图（AI原图/像素画）转为 512×512 复古泛黄新闻纸像素版画:
    python newsprint-tone.py input.png output.png
    python newsprint-tone.py input.png output.png --grid 128 --out-size 512 --contrast 1.15

原理:
  1. 先转为灰度亮度并做可调对比度拉伸，按最近邻/盒采样缩到目标像素网格（默认 128×128）；
  2. 采用 Better Newspapers 原版 9 阶新闻纸油墨色板（#383631 ~ #F6F2CF）；
  3. 采用原版 2×2 拜耳半色调抖动矩阵 [32, 160, 224, 96] 对中间灰阶做网点过渡；
  4. 最近邻整数倍放大到 512×512，保存为调色板模式 PNG（每张仅数 KB，FilterMode.Point 锐利无糊边）。
"""
import argparse
import os
from PIL import Image, ImageEnhance

INKS = [
    (56, 54, 49),     # #383631 最深油墨黑
    (75, 71, 62),     # #4B473E 深灰褐
    (111, 104, 88),   # #6F6858 中暗灰褐
    (131, 123, 105),  # #837B69 中灰褐
    (152, 143, 123),  # #988F7B 中性暖灰
    (181, 172, 144),  # #B5AC90 浅灰米
    (208, 200, 167),  # #D0C8A7 暖米白
    (235, 227, 189),  # #EBE3BD 高光纸黄
    (246, 242, 207),  # #F6F2CF 最亮高光纸白
]

SCREEN = (32, 160, 224, 96)


def tone_pixel(r, g, b, x, y, chart=False):
    lum = (54 * r + 183 * g + 19 * b + 128) >> 8
    if chart:
        sat = max(r, g, b) - min(r, g, b)
        if b > g and b > r:
            delta = 10
        elif r > g and g > b:
            delta = 34 if (g * 4 > r * 3) else 20
        elif b > r and b * 4 > g * 3:
            delta = 17
        else:
            delta = -18
        lum += delta * min(sat, 75) // 75

    lum = max(0, min(lum, 255))
    scaled = lum * (len(INKS) - 1)
    idx = scaled // 255
    rem = scaled % 255
    threshold = 128 if chart else SCREEN[(x & 1) + 2 * (y & 1)]
    if rem > threshold and idx + 1 < len(INKS):
        idx += 1
    return INKS[idx]


def convert_to_newsprint(in_path, out_path, grid=128, out_size=512, contrast=1.1, brightness=1.0, chart=False):
    im = Image.open(in_path).convert("RGB")
    if abs(contrast - 1.0) > 1e-3:
        im = ImageEnhance.Contrast(im).enhance(contrast)
    if abs(brightness - 1.0) > 1e-3:
        im = ImageEnhance.Brightness(im).enhance(brightness)

    # 居中裁剪为正方形后下采样到 pixel grid（如 128×128）
    w, h = im.size
    side = min(w, h)
    left = (w - side) // 2
    top = (h - side) // 2
    im = im.crop((left, top, left + side, top + side))
    small = im.resize((grid, grid), Image.Resampling.BILINEAR)

    src_px = small.load()
    toned = Image.new("RGB", (grid, grid))
    dst_px = toned.load()
    for y in range(grid):
        for x in range(grid):
            r, g, b = src_px[x, y]
            dst_px[x, y] = tone_pixel(r, g, b, x, y, chart=chart)

    big = toned.resize((out_size, out_size), Image.Resampling.NEAREST)
    quant = big.quantize(colors=len(INKS), method=Image.Quantize.FASTOCTREE, dither=Image.Dither.NONE)

    out_dir = os.path.dirname(os.path.abspath(out_path))
    if out_dir and not os.path.isdir(out_dir):
        os.makedirs(out_dir, exist_ok=True)
    quant.save(out_path, format="PNG", optimize=True)
    print(f"OK {in_path} -> {out_path} ({out_size}x{out_size}, grid={grid}, colors={len(quant.getcolors() or [])})")


def main():
    ap = argparse.ArgumentParser(description="Better Newspapers 风格 9 色油墨半色调报纸配图转换器")
    ap.add_argument("input", help="输入图片路径")
    ap.add_argument("output", help="输出 512×512 PNG 路径")
    ap.add_argument("--grid", type=int, default=128, help="逻辑像素网格分辨率（默认 128）")
    ap.add_argument("--out-size", type=int, default=512, help="输出尺寸（默认 512）")
    ap.add_argument("--contrast", type=float, default=1.1, help="输入图预处理对比度（默认 1.1）")
    ap.add_argument("--brightness", type=float, default=1.0, help="输入图预处理亮度（默认 1.0）")
    ap.add_argument("--chart", action="store_true", help="使用图表无网点平滑量化模式")
    args = ap.parse_args()
    convert_to_newsprint(args.input, args.output, args.grid, args.out_size, args.contrast, args.brightness, args.chart)


if __name__ == "__main__":
    main()
