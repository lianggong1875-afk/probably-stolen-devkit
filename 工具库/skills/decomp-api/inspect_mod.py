# -*- coding: utf-8 -*-
"""《深空当铺》模组架构与原版接口静态透视工具。

支持两种输入：
  1. 传反编译后的源码目录（含 .cs 文件）
  2. 直接传模组 .dll 文件（自动调用 工具库/build/ilspycmd 临时反编译后分析并清理）

提取内容：
  - IMod 入口类 & 是否包含 HotReloadPrepare/HotReloadActivate 钩子
  - 全部特性式 [HarmonyPatch] 与手动 .Patch() 目标
  - 全部 ModHook / ModHelper / ModConfig / ModReflect 调用清单
  - UI 与原版核心系统触点
  - 潜在权限触发点静态预警（files / network / dynamic-code / reflection）

用法：
  python inspect_mod.py <源码目录或DLL路径> [--json] [--managed <Managed目录>]
"""
import argparse
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile
from pathlib import Path

DEFAULT_MANAGED = r"D:\steam\steamapps\common\Probably Stolen\Probably Stolen_Data\Managed"

PATTERNS = {
    "imod": re.compile(r"^\s*(?:public|internal)\s+(?:sealed\s+|partial\s+)*class\s+(\w+)\s*:[^{]*\bIMod\b", re.M),
    "hot_prepare": re.compile(r"\bpublic\s+void\s+HotReloadPrepare\s*\(\s*\)"),
    "hot_activate": re.compile(r"\bpublic\s+void\s+HotReloadActivate\s*\(\s*\)"),
    "harmony_attr": re.compile(r"\[HarmonyPatch\(([^\]]+)\]"),
    "harmony_attr_full": re.compile(r"\[HarmonyPatch\(([^\]]+)\)\]"),
    "manual_patch": re.compile(r"\.Patch\s*\(([^;\n]{1,160})"),
    "mod_hook": re.compile(r"\bModHook\.(\w+)"),
    "mod_helper": re.compile(r"\bModHelper\.(\w+)"),
    "mod_config": re.compile(r"\bModConfig\.(\w+)"),
    "mod_reflect": re.compile(r"\bModReflect\.(\w+)"),
    "ui_touch": re.compile(
        r"\b(CustomUIManager|CustomUIBuilder|CustomUIDebugPanel|InspectionUIManager|"
        r"PaperUI|PixelWindow|StoreEventManager|ES3Type|AppraisalRankHelper|"
        r"AppraisalItemRegistry|DialogUIManager|DialoguePhoneUIManager)\b"
    ),
    "cap_files": re.compile(r"\b(?:System\.IO\.)?(?:File|Directory|FileStream|StreamReader|StreamWriter)\.\w+"),
    "cap_network": re.compile(r"\b(HttpListener|HttpClient|WebClient|TcpClient|UdpClient|Socket)\b"),
    "cap_dynamic": re.compile(r"\b(Assembly\.Load|Assembly\.LoadFrom|Assembly\.LoadFile|DynamicMethod|ILGenerator)\b"),
    "cap_reflect": re.compile(r"\b(AccessTools\.(?:Method|Field|Property)|Type\.GetType|\.GetMethod\s*\(|\.GetField\s*\()"),
}


def find_ilspycmd() -> Path | None:
    here = Path(__file__).resolve()
    for parent in here.parents:
        cand = parent / "build" / "ilspycmd" / "ilspycmd.cmd"
        if cand.is_file():
            return cand
        cand2 = parent / "工具库" / "build" / "ilspycmd" / "ilspycmd.cmd"
        if cand2.is_file():
            return cand2
    return None


def analyze_dir(src_dir: Path) -> dict:
    cs_files = sorted(p for p in src_dir.rglob("*.cs"))
    imods = []
    has_hot_prepare = False
    has_hot_activate = False
    harmony_attrs = []
    manual_patches = []
    mod_hooks = set()
    mod_helpers = set()
    mod_configs = set()
    mod_reflects = set()
    ui_touches = set()
    cap_hints = {"files": [], "network": [], "dynamic-code": [], "reflection": []}

    for cs in cs_files:
        rel = str(cs.relative_to(src_dir))
        text = cs.read_text(encoding="utf-8-sig", errors="replace")

        for m in PATTERNS["imod"].finditer(text):
            imods.append(f"{rel} -> {m.group(1)}")
        if PATTERNS["hot_prepare"].search(text):
            has_hot_prepare = True
        if PATTERNS["hot_activate"].search(text):
            has_hot_activate = True

        for m in PATTERNS["harmony_attr_full"].finditer(text):
            harmony_attrs.append(f"{rel}: [{m.group(1).strip()}]")
        for m in PATTERNS["manual_patch"].finditer(text):
            manual_patches.append(f"{rel}: .Patch({m.group(1).strip()[:120]})")

        mod_hooks.update(PATTERNS["mod_hook"].findall(text))
        mod_helpers.update(PATTERNS["mod_helper"].findall(text))
        mod_configs.update(PATTERNS["mod_config"].findall(text))
        mod_reflects.update(PATTERNS["mod_reflect"].findall(text))
        ui_touches.update(PATTERNS["ui_touch"].findall(text))

        if PATTERNS["cap_files"].search(text):
            cap_hints["files"].append(rel)
        if PATTERNS["cap_network"].search(text):
            cap_hints["network"].append(rel)
        if PATTERNS["cap_dynamic"].search(text):
            cap_hints["dynamic-code"].append(rel)
        if PATTERNS["cap_reflect"].search(text):
            cap_hints["reflection"].append(rel)

    return {
        "source": str(src_dir),
        "cs_files": len(cs_files),
        "imods": imods,
        "hot_reload_ready": has_hot_prepare and has_hot_activate,
        "mod_hooks": sorted(mod_hooks),
        "mod_helpers": sorted(mod_helpers),
        "mod_configs": sorted(mod_configs),
        "mod_reflects": sorted(mod_reflects),
        "system_touches": sorted(ui_touches),
        "harmony_attr_patches": harmony_attrs,
        "manual_patches": manual_patches,
        "capability_hints": {k: v[:10] for k, v in cap_hints.items() if v},
        "capability_hint_counts": {k: len(v) for k, v in cap_hints.items() if v},
    }


def print_report(data: dict) -> None:
    print(f"# 模组静态架构透视：{data['source']}")
    print(f"- 源码文件数：{data['cs_files']}")
    print(f"- IMod 入口：{', '.join(data['imods']) or '(未发现)'}")
    print(f"- 热重载钩子（HotReloadPrepare/Activate）：{'是' if data['hot_reload_ready'] else '否'}")
    print(f"- ModHook ({len(data['mod_hooks'])}): {', '.join(data['mod_hooks']) or '-'}")
    print(f"- ModHelper ({len(data['mod_helpers'])}): {', '.join(data['mod_helpers']) or '-'}")
    if data["mod_configs"]:
        print(f"- ModConfig: {', '.join(data['mod_configs'])}")
    if data["mod_reflects"]:
        print(f"- ModReflect: {', '.join(data['mod_reflects'])}")
    print(f"- 核心系统触点: {', '.join(data['system_touches']) or '-'}")
    if data["capability_hint_counts"]:
        hints = ", ".join(f"{k}({v}文件)" for k, v in data["capability_hint_counts"].items())
        print(f"- 潜在权限信号: {hints}")
    else:
        print("- 潜在权限信号: 无明显高权 API（倾向零能力）")
    if data["harmony_attr_patches"]:
        print(f"\n## 特性式 HarmonyPatch ({len(data['harmony_attr_patches'])})")
        for item in data["harmony_attr_patches"][:50]:
            print(f"  - {item}")
        if len(data["harmony_attr_patches"]) > 50:
            print(f"  ... 共 {len(data['harmony_attr_patches'])} 处")
    if data["manual_patches"]:
        print(f"\n## 手动 Harmony.Patch ({len(data['manual_patches'])})")
        for item in data["manual_patches"][:30]:
            print(f"  - {item}")
        if len(data["manual_patches"]) > 30:
            print(f"  ... 共 {len(data['manual_patches'])} 处")


def main() -> int:
    ap = argparse.ArgumentParser(description="《深空当铺》模组架构与原版接口静态透视工具")
    ap.add_argument("target", help="反编译后的源码目录，或模组 .dll 文件路径")
    ap.add_argument("--json", action="store_true", help="以 JSON 格式输出")
    ap.add_argument("--managed", default=DEFAULT_MANAGED, help="游戏 Managed 目录（反编译 DLL 时使用）")
    args = ap.parse_args()

    target = Path(args.target).resolve()
    if not target.exists():
        print(f"路径不存在: {target}", file=sys.stderr)
        return 1

    tmp_dir = None
    try:
        scan_dir = target
        if target.is_file() and target.suffix.lower() == ".dll":
            ilspy = find_ilspycmd()
            if ilspy is None:
                print("未找到 ilspycmd.cmd，请先反编译为目录再分析。", file=sys.stderr)
                return 2
            tmp_dir = Path(tempfile.mkdtemp(prefix="ps_inspect_"))
            cmd = [str(ilspy), "-p", "-o", str(tmp_dir)]
            if args.managed and Path(args.managed).is_dir():
                cmd += ["-r", args.managed]
            cmd.append(str(target))
            proc = subprocess.run(cmd, capture_output=True, text=True, encoding="utf-8", errors="replace")
            if proc.returncode != 0:
                print(f"ilspycmd 反编译失败:\n{proc.stderr}", file=sys.stderr)
                return proc.returncode
            scan_dir = tmp_dir

        data = analyze_dir(scan_dir)
        data["source"] = str(target)
        if args.json:
            print(json.dumps(data, ensure_ascii=False, indent=2))
        else:
            print_report(data)
        return 0
    finally:
        if tmp_dir and tmp_dir.exists():
            shutil.rmtree(tmp_dir, ignore_errors=True)


if __name__ == "__main__":
    raise SystemExit(main())
