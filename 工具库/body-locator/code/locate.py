#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""locate.py —— 从一段文本里定位「正文」区间 [start, end)。

模型：GoForceX/body-locator-small-v1（transition-span-locator，底模 mmBERT-small-zho-32768）
  输入整段文本（含 JSON / 各种标记包裹都可以）
  输出一个连续区间，text[start:end] 就是正文原文；它不改写任何字符
  dev exact 93.6%，body 字符 F1 0.9946；窗口 4096 token、上限 8192 token

为什么要有这个薄壳：随包的 `code/infer.py` 自身就是完整 CLI，但本机
`tools\\python314` 是隔离模式（isolated=1 / safe_path=True / ignore_environment=1），
既不读 PYTHONPATH 也不把脚本目录塞进 sys.path，直接跑会 ModuleNotFoundError。
这里补上 sys.path 并把默认路径、默认精度都定好，调起来短。

用法：
  python locate.py --input <文件> [--output <正文输出文件>]
  python locate.py --text "一段文本"
  python locate.py --stdin
选项：
  --json            同时打印完整结果（span / token_count / chunk_count / time_ms）
  --device cpu      强制 CPU（默认自动）
  --span S E        只在给定区间内找（坐标相对原文）

精度说明：固定走 float32。给 `checkpoint.load` 传 dtype 会把权重转成 half，
但 `data.model_inputs` 造出来的输入张量仍是 float32，forward 会报
`mat1 and mat2 must have the same dtype`。要用半精度得同时改 data.py，
不值当——57.5M 参数在 GPU 上 float32 已经够快。
"""
from __future__ import annotations

import argparse
import json
import os
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))          # 关键：隔离模式下必须自己补

# 缓存目录必须指到「缓存文件夹」，不能留在原地，原因有两条：
#  ① 工具库目录是 Medium 完整性级别，而 python 子进程是 Low，受 No-Write-Up 限制，
#     在工具库里建目录会直接 PermissionError [WinError 5]；缓存文件夹是 Low，可写。
#  ② torch 默认会把 inductor 缓存建在当前工作目录（cwd）下，命名 torchinductor_<用户名>，
#     谁从哪跑来就在哪留一个，会污染工作区根目录。
_WORKSPACE = HERE.parents[2]                       # code -> body-locator -> 工具库 -> 工作区根
_TMP = _WORKSPACE / "缓存文件夹" / "body-locator-tmp"
_TMP.mkdir(parents=True, exist_ok=True)
for _k in ("TEMP", "TMP", "TMPDIR", "TORCHINDUCTOR_CACHE_DIR", "TRITON_CACHE_DIR", "TORCH_HOME"):
    os.environ.setdefault(_k, str(_TMP))
    os.environ[_k] = str(_TMP)                     # 已存在的也覆盖，别让它指回旧位置

DEFAULT_CHECKPOINT = HERE.parent / "models" / "best_fp16.pt"


def main() -> int:
    ap = argparse.ArgumentParser(description="定位文本里的正文区间")
    src = ap.add_mutually_exclusive_group(required=True)
    src.add_argument("--input", help="UTF-8 文本文件")
    src.add_argument("--text", help="直接给文本")
    src.add_argument("--stdin", action="store_true", help="从标准输入读")
    ap.add_argument("--output", help="把正文写到这个文件")
    ap.add_argument("--checkpoint", default=str(DEFAULT_CHECKPOINT))
    ap.add_argument("--device", default=None, help="cpu / cuda，默认自动")
    ap.add_argument("--span", nargs=2, type=int, metavar=("S", "E"))
    ap.add_argument("--json", action="store_true", help="打印完整结果")
    a = ap.parse_args()

    if a.input:
        with open(a.input, encoding="utf-8", newline="") as f:
            text = f.read()
    elif a.text is not None:
        text = a.text
    else:
        text = sys.stdin.read()
    if not text:
        print("错误：文本为空", file=sys.stderr)
        return 2

    import torch
    from infer import BodyLocator, TokenLimitError

    device = a.device or ("cuda" if torch.cuda.is_available() else "cpu")
    dtype = None                          # 固定 float32，理由见文件头

    locator = BodyLocator(a.checkpoint, device, None, dtype=dtype)
    try:
        result = locator.locate(text, tuple(a.span) if a.span else None)
    except TokenLimitError as e:
        print(f"错误：{e}（超长就先按字符切块，再逐块定位）", file=sys.stderr)
        return 3

    start, end = result["span"]
    if a.output:
        with open(a.output, "w", encoding="utf-8", newline="") as f:
            f.write(result["body"])

    print(f"区间 [{start}, {end})  正文 {end - start} 字 / 全文 {result['char_count']} 字")
    print(f"token {result['token_count']}（{result['chunk_count']} 块）  {result['time_ms']} ms  {device}")
    if a.json or not a.output:
        print(json.dumps({k: v for k, v in result.items() if k not in ("body", "text")},
                         ensure_ascii=False, indent=2))
    if not a.output:
        body = result["body"]
        preview = body if len(body) <= 400 else body[:400] + "…"
        print("--- 正文 ---")
        print(preview)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
