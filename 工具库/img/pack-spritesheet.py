# -*- coding: utf-8 -*-
"""
pack-spritesheet.py —— 《深空当铺》机器动画 8 列雪碧图打包与拆解工具
（对齐 Vanilla Machine Animations 美术与切图规范）

用法:
  打包散帧为 8 列雪碧图:
    python pack-spritesheet.py --out arc_burner_running__frames.png burner_run_0.png ... burner_run_7.png
    python pack-spritesheet.py --glob "burner_run_*.png" --out arc_burner_running__frames.png

  校验或拆解已有雪碧图:
    python pack-spritesheet.py --inspect arc_burner_running__frames.png --frame-width 48 --frame-height 48
    python pack-spritesheet.py --unpack arc_burner_running__frames.png --frame-width 48 --frame-height 48 --out-dir out_frames

规范（Vanilla Machine Animations 标准）:
  - 固定 8 列横向排布（Columns = 8），从左上角第 0 帧开始按行从左到右排列；
  - 整张图宽 = frame_width * 8，高 = frame_height * ceil(frame_count / 8)；
  - 所有帧必须严格等尺寸（且建议为 16 的倍数，匹配游戏 gridSize=16）；
  - 透明背景 RGBA，保留未预乘透明像素（不做双线性缩放）。
"""
import argparse
import glob
import math
import os
import re
import sys

from PIL import Image


COLUMNS = 8


def natural_key(path):
    name = os.path.basename(path)
    return [int(p) if p.isdigit() else p.lower() for p in re.split(r"(\d+)", name)]


def pack_frames(inputs, out_path, columns=COLUMNS):
    if not inputs:
        raise SystemExit("未提供任何输入帧文件")

    frames = []
    size0 = None
    for p in inputs:
        im = Image.open(p).convert("RGBA")
        if size0 is None:
            size0 = im.size
        elif im.size != size0:
            raise SystemExit(f"帧尺寸不一致: {p} 为 {im.size}, 预期 {size0}")
        frames.append((p, im))

    fw, fh = size0
    rows = math.ceil(len(frames) / float(columns))
    sheet_w = fw * columns
    sheet_h = fh * rows

    sheet = Image.new("RGBA", (sheet_w, sheet_h), (0, 0, 0, 0))
    for idx, (_, im) in enumerate(frames):
        col = idx % columns
        row = idx // columns
        sheet.paste(im, (col * fw, row * fh))

    out_dir = os.path.dirname(os.path.abspath(out_path))
    if out_dir and not os.path.isdir(out_dir):
        os.makedirs(out_dir, exist_ok=True)
    sheet.save(out_path, format="PNG")
    print(f"OK packed {len(frames)} frames ({fw}x{fh}) -> {out_path} ({sheet_w}x{sheet_h}, cols={columns}, rows={rows})")


def inspect_or_unpack(sheet_path, fw, fh, out_dir=None, columns=COLUMNS):
    sheet = Image.open(sheet_path).convert("RGBA")
    sw, sh = sheet.size
    if sw != fw * columns or sh % fh != 0:
        raise SystemExit(
            f"雪碧图尺寸不合规范: {sheet_path} 为 {sw}x{sh}, "
            f"预期宽={fw * columns} (cols={columns}), 高为 {fh} 的整数倍"
        )
    rows = sh // fh
    total = rows * columns
    non_empty = 0
    if out_dir:
        os.makedirs(out_dir, exist_ok=True)

    for idx in range(total):
        col = idx % columns
        row = idx // columns
        x = col * fw
        y = row * fh
        cell = sheet.crop((x, y, x + fw, y + fh))
        bbox = cell.getbbox()
        if bbox is not None:
            non_empty += 1
        if out_dir:
            cell.save(os.path.join(out_dir, f"frame_{idx:02d}.png"), format="PNG")

    print(f"OK inspected {sheet_path}: {sw}x{sh}, grid={columns}x{rows}, frame={fw}x{fh}, non_empty={non_empty}/{total}")


def main():
    ap = argparse.ArgumentParser(description="《深空当铺》机器动画 8 列雪碧图打包与校验工具")
    ap.add_argument("inputs", nargs="*", help="输入帧路径")
    ap.add_argument("--glob", dest="glob_pat", help="按通配符收集输入帧（自动按数字自然排序）")
    ap.add_argument("--out", help="输出雪碧图路径")
    ap.add_argument("--inspect", help="校验指定雪碧图")
    ap.add_argument("--unpack", help="拆解指定雪碧图")
    ap.add_argument("--out-dir", help="拆解输出目录")
    ap.add_argument("--frame-width", type=int, default=48, help="单帧宽度（默认 48）")
    ap.add_argument("--frame-height", type=int, default=48, help="单帧高度（默认 48）")
    ap.add_argument("--columns", type=int, default=COLUMNS, help="列数（默认 8）")
    args = ap.parse_args()

    if args.inspect:
        inspect_or_unpack(args.inspect, args.frame_width, args.frame_height, None, args.columns)
        return

    if args.unpack:
        if not args.out_dir:
            raise SystemExit("--unpack 需要指定 --out-dir")
        inspect_or_unpack(args.unpack, args.frame_width, args.frame_height, args.out_dir, args.columns)
        return

    files = list(args.inputs)
    if args.glob_pat:
        files.extend(glob.glob(args.glob_pat))
    files = sorted(dict.fromkeys(files), key=natural_key)
    if not args.out:
        raise SystemExit("打包模式需要指定 --out")
    pack_frames(files, args.out, args.columns)


if __name__ == "__main__":
    main()
