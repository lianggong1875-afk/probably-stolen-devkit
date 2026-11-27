# -*- coding: utf-8 -*-
"""批量反编译工坊模组 + 生成架构透视报告（蒸馏前置步骤）。

对每个工坊物品目录：
  1. 找出模组主 DLL（排除 Unity/Harmony/Newtonsoft 等第三方运行时库）
  2. 用 工具库\\build\\ilspycmd 反编译成 C# 项目到 缓存文件夹\\decomp_ws\\<id>\\
  3. 用 工具库\\skills\\decomp-api\\inspect_mod.py 生成透视报告到 缓存文件夹\\decomp_ws\\_reports\\<id>.txt

用法：
  python distill_prep.py --ids-file <清单>       # 也可以是逗号/空格分隔的 id
  python distill_prep.py 3842773239 3845965318
  python distill_prep.py --ids-file x.txt --force # 已反编译的也重做
"""
import argparse
import os
import re
import subprocess
import sys
import time

WORKSHOP = r"D:\steam\steamapps\workshop\content\4348910"
CACHE = r"D:\0深空当铺模组开发\缓存文件夹\decomp_ws"
REPORTS = os.path.join(CACHE, "_reports")
ILSPY = r"D:\0深空当铺模组开发\工具库\build\ilspycmd\ilspycmd.cmd"
INSPECT = r"D:\0深空当铺模组开发\工具库\skills\decomp-api\inspect_mod.py"
PYTHON = sys.executable

# 第三方运行时库，不是模组本体
SKIP_DLL = ("0harmony", "microsoft.", "system.", "newtonsoft", "es3", "steamworks",
            "unityengine", "moddevbridge", "il2cpp", "mono.", "netstandard")


def pick_dlls(root):
    found = []
    for dp, _, fs in os.walk(root):
        for f in fs:
            if f.lower().endswith(".dll"):
                found.append(os.path.join(dp, f))
    main = [d for d in found if not any(s in os.path.basename(d).lower() for s in SKIP_DLL)]
    return main, found


def run(cmd, timeout=600):
    p = subprocess.run(cmd, capture_output=True, text=True, encoding="utf-8", errors="replace",
                       timeout=timeout, shell=False)
    return p.returncode, (p.stdout or "") + (p.stderr or "")


def decompile(pid, force=False):
    src = os.path.join(WORKSHOP, pid)
    out = os.path.join(CACHE, pid)
    if not os.path.isdir(src):
        return None, "工坊目录不存在: " + src
    main, all_dlls = pick_dlls(src)
    if not main:
        return None, "没找到模组 DLL（可能纯资源模组）: %d 个 dll=%s" % (len(all_dlls), all_dlls)

    if os.path.isdir(out) and not force:
        existing = [f for f in os.listdir(out) if f.endswith(".cs")]
        if existing:
            return out, "已存在（跳过反编译）"

    os.makedirs(out, exist_ok=True)
    logs = []
    # 主 DLL 走 -p（整项目），其余 DLL 也反编译但平铺到子目录，避免同名覆盖
    for idx, dll in enumerate(main):
        dest = out if idx == 0 else os.path.join(out, "_side_" + os.path.splitext(os.path.basename(dll))[0])
        os.makedirs(dest, exist_ok=True)
        code, txt = run([ILSPY, "-p", "-o", dest, dll])
        logs.append("%s -> rc=%d %s" % (os.path.basename(dll), code, txt.strip()[:300]))
        if code != 0:
            # 退回按类型导出（有的 DLL 没有 pdb/project 结构）
            code2, txt2 = run([ILSPY, "-o", dest, dll])
            logs.append("  退回单文件模式 rc=%d %s" % (code2, txt2.strip()[:300]))
    return out, " | ".join(logs)


def inspect(src_dir, pid):
    os.makedirs(REPORTS, exist_ok=True)
    dst = os.path.join(REPORTS, pid + ".txt")
    code, txt = run([PYTHON, INSPECT, src_dir], timeout=300)
    with open(dst, "w", encoding="utf-8", newline="\n") as f:
        f.write(txt)
        if code != 0:
            f.write("\n\n[inspect rc=%d]\n" % code)
    return dst, code


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("ids", nargs="*")
    ap.add_argument("--ids-file")
    ap.add_argument("--force", action="store_true")
    a = ap.parse_args()

    ids = list(a.ids)
    if a.ids_file:
        with open(a.ids_file, encoding="utf-8") as f:
            for line in f:
                line = line.split("#")[0].strip()
                if line:
                    ids.extend(line.replace(",", " ").split())
    ids = [i for i in dict.fromkeys(ids) if i.isdigit()]
    if not ids:
        print("没有 id")
        return

    for pid in ids:
        t0 = time.time()
        print("=" * 70)
        print("[%s] 反编译…" % pid)
        out, msg = decompile(pid, a.force)
        print("   " + str(msg))
        if not out:
            continue
        rp, rc = inspect(out, pid)
        print("[%s] 透视报告 -> %s (rc=%d, %.1fs)" % (pid, rp, rc, time.time() - t0))


if __name__ == "__main__":
    main()
