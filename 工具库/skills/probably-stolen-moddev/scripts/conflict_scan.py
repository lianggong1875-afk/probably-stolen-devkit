# -*- coding: utf-8 -*-
"""跨模组静态冲突扫描器（《深空当铺》/ Probably Stolen）

解决的问题：我们自己的模组会不会和别人打架？工坊那个 2.5 万订阅的冲突检测器只认 3 类冲突
（声明冲突 / 同名 DLL / 25 条固定 IL 靶表），而且**不查 identifier 重复、语言 key 覆盖、atlas 同名**。
这个脚本补上那部分：把我们自己的源码目录和一批已装模组的反编译目录放在一起扫，
输出「同一个原版方法被谁和谁同时 Patch」「同一个自定义 identifier 被谁和谁同时注册」等硬证据。

输入：任意个「含 .cs 的目录」（我们自己项目的源码目录 / distill_prep.py 产出的反编译目录），
      以及可选的工坊模组目录（含 manifest.xml / .dll）用来读能力声明与 DLL 指纹。

用法：
  python conflict_scan.py --dir 缓存文件夹\\decomp_ws --dir 深空电玩\\src --out 缓存文件夹\\conflict-report.md
  python conflict_scan.py --workshop-root "D:\\steam\\steamapps\\workshop\\content\\4348910" --dir 深空当铺：可能是群友
  python conflict_scan.py --dir <A> --dir <B> --json

退出码：0 = 没发现问题；2 = 有 ERROR 级发现；3 = 有 WARN 级发现。
"""

import argparse
import hashlib
import itertools
import json
import os
import re
import sys
import xml.etree.ElementTree as ET

# ---------------------------------------------------------------- 原版热点分级
# 依据：Mod Conflict Scanner 蒸馏（workshop-distill/3832823827）暴露的红色靶点 +
# 我们自己的经验（19/20 号经验库文件）。key = 归一化后的 "类型.方法"，value = (级别, 理由)
HOTSPOTS = {
    # —— ERROR：全局显示层，两个模组同时接管必然互斥 ——
    "GameItem.get_name": ("ERROR", "全局物品名显示；两个模组同时接管会互相覆盖"),
    "GameItem.set_name": ("ERROR", "全局物品名写入"),
    "GameItem.get_shortDescription": ("ERROR", "全局物品描述显示"),
    "GameItem.GetDisplayName": ("ERROR", "全局显示名"),
    "LocHelper.SetLocale": ("ERROR", "全局语言切换；多个模组接管会让文案错乱"),
    "LocHelper.Get": ("ERROR", "全局取词入口；接管会吞掉其它模组的注入"),
    "FontLoader": ("ERROR", "全局字体加载"),
    "TMP_Settings": ("ERROR", "TMP 全局设置/回退字体"),
    # —— WARN：存档链与物品元数据 ——
    "PlayerStore.SaveGame": ("WARN", "存档写入链；单个命中会放大坏档风险"),
    "PlayerStore.InitialSave": ("WARN", "初次存档链"),
    "PlayerStore.LoadGame": ("WARN", "读档链"),
    "PlayerStore.DecodeSaveItem": ("WARN", "存档解码；整段替换会与同类模组互斥"),
    "SaveManager.Save": ("WARN", "存档落盘"),
    "SaveManager.EncodeNodes": ("WARN", "存档编码"),
    "SaveManager.DecodeNodes": ("WARN", "存档解码"),
    "GameItem.SetGameItemType": ("WARN", "品类写入；写非原版类型会刷 Translation Error"),
    "ItemFeature.realCondition": ("WARN", "真伪判定地基"),
    "GraphUtils.EmporiumTryAdd": ("WARN", "会兜底投放，别当精确投放用"),
    "GameItem.ResolveSpriteByName": ("WARN", "贴图解析；接管会影响所有模组物品图标"),
    "ChemicalHelper.RunNightlyChemicalSimulation": ("WARN", "过夜化学模拟；整段替换=与所有优化模组互斥"),
    "PlayerStore.EndNight": ("WARN", "过夜主流程；整段替换风险极高"),
    "GameItem.IsTag": ("WARN", "Appraisal Assistant 指纹红线方法；Patch 会影响其读取第三方鉴定品类，须纯增量且显式定序"),
    "AppraisalHelper.GetCorrectBaseValue": ("WARN", "Appraisal Assistant 指纹红线方法；严禁破坏性 Prefix 替换"),
    "InputActionManager.Update": ("WARN", "全局输入热点；多模组重叠须做模态门控并保证 Clear 兜底"),
    "PlayerStore.GetNextClient": ("WARN", "访客队列高频争夺点；多模组共存须显式声明优先级分相（准备/常规/裁决）"),
}

# 能注册出"全局标识"的方法名 → 注册的是什么
REGISTER_CALLS = {
    "AddNetworkUpgrade": "关系网节点 id",
    "AddAppraisalItem": "鉴定品类 id",
    "AddStartingPerk": "开局特性 id",
    "AddStartingScenario": "开局剧本 id",
    "AddDifficulty": "难度 id",
    "AddAfterhourLocation": "夜晚外出地点 id",
    "AddAfterhourEvent": "夜晚事件 id",
    "RegisterPaperBook": "交互纸张 id",
    "RegisterLootTable": "掉落表 id",
    "RegisterLootGroup": "掉落组 id",
    "EnableTag": "物品标签",
}

# 额外直写注册点（不经过 ModHelper.Add* 的全局字典/电话/状态键）
EXTRA_REG_LABELS = {
    "client_id": "客户蓝图 id",
    "phone_func": "电话对话 id",
    "phone_number": "电话号码",
    "state_key": "存档状态键",
}

# 已知存在多重载的原版高危方法：不显式写 Type[] 参数表会触发 Harmony AmbiguousMatchException
KNOWN_OVERLOADED_TARGETS = {
    "DirectoryMaster.Item",
    "ContrabandHelper.InitContrabandItem",
    "GraphUtils.EmporiumTryAdd",
}

PATCH_RE = re.compile(r"\[(?:HarmonyLib\.)?HarmonyPatch\s*\(((?:[^()\[\]]|\([^()]*\)|\[[^\]]*\]){1,400}?)\)\s*\]", re.S)
LINE_COMMENT_RE = re.compile(r"//[^\n]*")
BLOCK_COMMENT_RE = re.compile(r"/\*.*?\*/", re.S)
TYPEOF_RE = re.compile(r"typeof\s*\(\s*([\w\.]+)\s*\)")
STR_RE = re.compile(r'"([^"]{1,120})"')
NAMEOF_RE = re.compile(r"nameof\s*\(\s*[\w\.]*?(\w+)\s*\)")
REG_RE = re.compile(r"ModHelper\.(\w+)\s*\(\s*([^;]{0,400}?)\)\s*;", re.S)
IDENT_RE = re.compile(r'"([a-z0-9_\.\-]{3,80})"')
CLIENT_DICT_RE = re.compile(r'storeClientDict\s*\[\s*"([a-zA-Z0-9_\.\-]{2,80})"\s*\]\s*=')
PHONE_FUNC_RE = re.compile(r'DialogFuncs\s*\[\s*"([a-zA-Z0-9_\.\-]{2,80})"\s*\]\s*=')
PHONE_NUM_RE = re.compile(r'(?:new\s+StorePhoneClient\s*\(\s*|PhoneNumber\s*=\s*)(\d{3,10})L?')
STATE_KEY_RE = re.compile(r'itemDisplayNameCache\s*\[\s*"(__[a-zA-Z0-9_\.\-]+__)"\s*\]')
# 顺序声明：用来判断"这处重叠有没有被作者主动协调过"
PRIORITY_RE = re.compile(r"\[HarmonyPriority\s*\(")
BEFORE_RE = re.compile(r"\[HarmonyBefore\s*\(([^\)]*)\)")
AFTER_RE = re.compile(r"\[HarmonyAfter\s*\(([^\)]*)\)")


def has_cs(root):
    for _ in iter_cs(root):
        return True
    return False


def derive_name(root):
    """目录名太通用（src / code）时，用上一级目录名当模组名。"""
    base = os.path.basename(root.rstrip("\\/"))
    if base.lower() in ("src", "code", "source", "sources", "scripts", "decompiled"):
        parent = os.path.basename(os.path.dirname(root.rstrip("\\/")))
        if parent:
            return parent
    return base


def iter_cs(root):
    for dp, _, fs in os.walk(root):
        for f in fs:
            if f.endswith(".cs"):
                yield os.path.join(dp, f)


def norm_target(raw):
    """把 [HarmonyPatch(...)] 里的参数归一成 'Type.Method'。"""
    m_type = TYPEOF_RE.search(raw)
    if not m_type:
        return None
    typ = m_type.group(1).split(".")[-1]
    # 方法名：优先 nameof(...)，其次第一个字符串字面量
    m_name = NAMEOF_RE.search(raw)
    if m_name:
        return "%s.%s" % (typ, m_name.group(1))
    strs = STR_RE.findall(raw)
    if strs:
        return "%s.%s" % (typ, strs[0])
    return None


def scan_dir(root):
    """扫一个目录，返回该"模组"的静态指纹。"""
    res = {
        "root": root,
        "name": derive_name(root),
        "mod_id": "",              # manifest <ID>，用于匹配 HarmonyBefore/After 里的 "mod.<id>"
        "patches": set(),          # 'Type.Method'
        "patch_order": {},         # 'Type.Method' -> {'priority': bool, 'before': set, 'after': set}
        "missing_overload_types": set(),  # 命中已知多重载方法但未写 Type[] 参数表的目标
        "registers": {},           # 'AddNetworkUpgrade' / 'client_id' ... -> set(id)
        "files": 0,
        "manifest": None,
        "caps": [],
        "dlls": [],
    }

    man = None
    # manifest 可能在本目录，也可能在上一级（我们把 --dir 指向项目的 src 子目录时）
    for base in (root, os.path.abspath(os.path.join(root, os.pardir))):
        for cand in ("manifest.xml", os.path.join("About", "About.xml")):
            p = os.path.join(base, cand)
            if os.path.isfile(p):
                man = p
                break
        if man:
            break
    if man:
        try:
            tree = ET.parse(man)
            r = tree.getroot()
            res["name"] = (r.findtext("Name") or res["name"]).strip()
            res["mod_id"] = (r.findtext("ID") or "").strip()
            caps = r.find("Capabilities")
            if caps is not None:
                res["caps"] = [(c.text or c.tag).strip() for c in caps if (c.text or c.tag).strip()]
            elif r.find("Capabilities") is None:
                res["caps"] = []       # 自闭合 <Capabilities/> 也是零能力
        except Exception as e:
            res["manifest"] = "解析失败: %s" % e

    for dp, _, fs in os.walk(root):
        for f in fs:
            if f.lower().endswith(".dll"):
                p = os.path.join(dp, f)
                res["dlls"].append(f)

    for path in iter_cs(root):
        res["files"] += 1
        try:
            text = open(path, encoding="utf-8", errors="replace").read()
        except OSError:
            continue
        # 剥离注释避免把文档示例里的 [HarmonyPatch(...)] 误判为真实补丁
        text = BLOCK_COMMENT_RE.sub(lambda m: "\n" * m.group(0).count("\n"), text)
        text = LINE_COMMENT_RE.sub("", text)
        for m in PATCH_RE.finditer(text):
            raw = m.group(1)
            t = norm_target(raw)
            if not t:
                continue
            res["patches"].add(t)

            # 检测已知多重载方法是否缺少 Type[] 参数表
            if t in KNOWN_OVERLOADED_TARGETS:
                has_types = ("new Type" in raw) or (len(TYPEOF_RE.findall(raw)) >= 2)
                if not has_types:
                    res["missing_overload_types"].add(t)

            # 方法级顺序声明：只提取紧邻本 [HarmonyPatch] 的特性块（向上到上个语句/大括号，向下到类声明或下个 Patch）
            pre_start = max(0, m.start() - 400)
            pre = text[pre_start:m.start()]
            # 截断到最后一个分号或大括号之后，防止串到上一个类
            cut = max(pre.rfind(";"), pre.rfind("}"), pre.rfind("{"))
            if cut >= 0:
                pre = pre[cut + 1:]
            post_end = min(len(text), m.end() + 400)
            post = text[m.end():post_end]
            next_patch = post.find("[HarmonyPatch")
            if next_patch >= 0:
                post = post[:next_patch]
            brace = post.find("{")
            if brace >= 0:
                # 只追踪到本类的闭合大括号为止，绝不串到下一个类的类级特性
                depth = 0
                end_i = brace
                for idx in range(brace, min(len(post), brace + 1200)):
                    ch = post[idx]
                    if ch == "{":
                        depth += 1
                    elif ch == "}":
                        depth -= 1
                        if depth == 0:
                            end_i = idx + 1
                            break
                window = pre + post[:end_i]
            else:
                window = pre + post

            entry = res["patch_order"].setdefault(t, {"priority": False, "before": set(), "after": set()})
            if PRIORITY_RE.search(window):
                entry["priority"] = True
            for grp in BEFORE_RE.findall(window):
                entry["before"].update(IDENT_RE.findall(grp))
            for grp in AFTER_RE.findall(window):
                entry["after"].update(IDENT_RE.findall(grp))

        for name, argstr in REG_RE.findall(text):
            if name in REGISTER_CALLS:
                # 取参数里第二、三个字符串字面量当 id（modId 通常是第一个）
                ids = IDENT_RE.findall(argstr)
                for v in ids[1:3]:
                    res["registers"].setdefault(name, set()).add(v)

        # 额外直写注册点扫描
        for v in CLIENT_DICT_RE.findall(text):
            res["registers"].setdefault("client_id", set()).add(v)
        for v in PHONE_FUNC_RE.findall(text):
            res["registers"].setdefault("phone_func", set()).add(v)
        for v in PHONE_NUM_RE.findall(text):
            res["registers"].setdefault("phone_number", set()).add(v)
        for v in STATE_KEY_RE.findall(text):
            res["registers"].setdefault("state_key", set()).add(v)

    return res


def order_ok(a, b, target):
    """按方法粒度判断两个模组在 target 上是否已有显式顺序声明。"""
    oa = a["patch_order"].get(target) or {"priority": False, "before": set(), "after": set()}
    ob = b["patch_order"].get(target) or {"priority": False, "before": set(), "after": set()}
    if not oa["priority"] and not ob["priority"]:
        return False
    aid, bid = a["mod_id"], b["mod_id"]
    if not aid or not bid:
        return False
    a_ref_b = ("mod." + bid) in oa["before"] or ("mod." + bid) in oa["after"]
    b_ref_a = ("mod." + aid) in ob["before"] or ("mod." + aid) in ob["after"]
    return a_ref_b or b_ref_a


def hash_dlls(workshop_root):
    """同名 DLL 不同哈希 = 覆盖安装，是硬冲突。"""
    table = {}
    if not workshop_root or not os.path.isdir(workshop_root):
        return table
    for d in sorted(os.listdir(workshop_root)):
        p = os.path.join(workshop_root, d)
        if not os.path.isdir(p):
            continue
        for dp, _, fs in os.walk(p):
            for f in fs:
                if f.lower().endswith(".dll"):
                    fp = os.path.join(dp, f)
                    try:
                        h = hashlib.sha256(open(fp, "rb").read()).hexdigest()[:16]
                        sz = os.path.getsize(fp)
                    except OSError:
                        continue
                    table.setdefault(f.lower(), []).append((d, h, sz, f))
    return table


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--dir", action="append", default=[], help="含 .cs 的目录（可多次给）")
    ap.add_argument("--workshop-root", default=r"D:\steam\steamapps\workshop\content\4348910")
    ap.add_argument("--out", default=None, help="markdown 报告输出路径")
    ap.add_argument("--json", action="store_true", help="额外打印 JSON")
    ap.add_argument("--gate", action="store_true",
                    help="门禁模式：单模组命中热点降级为 WARN（已知设计决策不阻塞合并）")
    a = ap.parse_args()

    dirs = [os.path.abspath(d) for d in a.dir if os.path.isdir(d)]
    if not dirs:
        print("没有有效的 --dir（目录不存在或为空）", file=sys.stderr)
        return 1

    # 自动展开：给的是一个"容器目录"（自身既没有 .cs 也没有 manifest.xml，但子目录里有 .cs）
    # 时，把子目录各当成一个模组。
    expanded = []
    for d in dirs:
        direct_cs = [f for f in os.listdir(d) if f.endswith(".cs")] if os.path.isdir(d) else []
        has_manifest = os.path.isfile(os.path.join(d, "manifest.xml"))
        if direct_cs or has_manifest:
            expanded.append(d)
            continue
        subs = [os.path.join(d, s) for s in sorted(os.listdir(d))
                if os.path.isdir(os.path.join(d, s)) and has_cs(os.path.join(d, s))]
        if subs:
            expanded.extend(subs)
        else:
            expanded.append(d)          # 交回去让它报"0 个 .cs"
    dirs = expanded

    mods = [scan_dir(d) for d in dirs]
    mods = [m for m in mods if m["files"] > 0]

    findings = []      # (level, kind, subject, detail)

    # 1) Harmony 目标交集（并判断这处重叠有没有被显式声明过顺序）
    by_target = {}
    for m in mods:
        for t in m["patches"]:
            by_target.setdefault(t, []).append(m)
    for target, owners in sorted(by_target.items()):
        uniq = {}
        for m in owners:
            uniq[m["name"]] = m
        if len(uniq) < 2:
            continue
        level, why = HOTSPOTS.get(target, ("INFO", "多个模组同时 Patch 同一个方法"))
        pairs = list(itertools.combinations(list(uniq.values()), 2))
        coordinated = all(order_ok(a, b, target) for a, b in pairs)
        if not coordinated and level == "INFO":
            level = "WARN"
        note = "｜顺序：已声明" if coordinated else "｜顺序：**未声明**（谁先跑随缘）"
        findings.append((level, "patch-overlap", target,
                         "%d 个模组同时 Patch：%s（%s）%s"
                         % (len(uniq), "、".join(sorted(uniq)), why, note)))

    # 1b) 已知多重载方法缺少 Type[] 参数表
    for m in mods:
        for t in sorted(m["missing_overload_types"]):
            findings.append(("ERROR", "overload-missing-types", t,
                             "%s 对已知多重载方法 %s 未显式声明 Type[] 参数表，加载可能触发 AmbiguousMatchException"
                             % (m["name"], t)))

    # 2) 单模组命中原版热点（哪怕只有一个，也要提示风险）
    for m in mods:
        for t in m["patches"]:
            if t in HOTSPOTS and len(by_target.get(t, ())) < 2:
                level, why = HOTSPOTS[t]
                # --gate 模式：单模组命中热点是"已知的设计决策"（我们明知故犯并加固过），
                # 不该永远堵住合并 → 降级为 WARN；多重重叠/重复注册/同名 DLL 仍算 ERROR。
                if a.gate and level == "ERROR":
                    level = "WARN"
                    why = why + "｜gate: 单模组热点，已作为已知设计决策接受"
                findings.append((level, "hotspot", t, "%s 命中原版热点（%s）" % (m["name"], why)))

    # 3) 注册标识重复
    reg_labels = dict(REGISTER_CALLS)
    reg_labels.update(EXTRA_REG_LABELS)
    by_reg = {}
    for m in mods:
        for api, ids in m["registers"].items():
            for i in ids:
                by_reg.setdefault((api, i), set()).add(m["name"])
    for (api, ident), owners in sorted(by_reg.items()):
        if len(owners) >= 2:
            findings.append(("ERROR", "id-collision", "%s(%s)" % (reg_labels.get(api, api), ident),
                             "被多个模组注册：%s（先到者赢，后到的会被忽略或不生效）" % "、".join(sorted(owners))))

    # 4) DLL 同名不同内容
    dlls = hash_dlls(a.workshop_root)
    for name, entries in sorted(dlls.items()):
        hs = {e[1] for e in entries}
        if len(hs) > 1:
            findings.append(("WARN", "dll-collision", name,
                             "同名 DLL 有 %d 个不同版本：%s" % (len(hs), "、".join("%s(%s)" % (e[3], e[1][:8]) for e in entries))))

    # 5) 能力清单（信息项，便于判断谁需要用户授权）
    for m in mods:
        if m["caps"]:
            findings.append(("INFO", "capabilities", m["name"], "声明能力：" + "、".join(m["caps"])))

    order = {"ERROR": 0, "WARN": 1, "INFO": 2}
    findings.sort(key=lambda f: (order.get(f[0], 9), f[1], f[2]))

    lines = ["# 跨模组静态冲突扫描", "",
             "扫描对象（%d 个）：" % len(mods), ""]
    for m in mods:
        lines.append("- `%s`：%d 个 .cs，%d 处 HarmonyPatch，能力 %s"
                     % (m["name"], m["files"], len(m["patches"]),
                        ("、".join(m["caps"]) if m["caps"] else "无")))
    lines += ["", "## 发现", ""]
    if not findings:
        lines.append("没有发现冲突（仅代表静态扫描层面）。")
    else:
        for level, kind, subject, detail in findings:
            lines.append("- **%s** `%s`（%s）：%s" % (level, subject, kind, detail))

    errs = sum(1 for f in findings if f[0] == "ERROR")
    warns = sum(1 for f in findings if f[0] == "WARN")
    lines += ["", "## 结论", "",
              "ERROR %d 条 / WARN %d 条 / INFO %d 条。"
              % (errs, warns, len(findings) - errs - warns), "",
              "> 说明：本工具只看**静态**证据（Patch 目标、注册调用、DLL 指纹、能力声明）。"
              "运行时行为冲突、时序冲突、数据语义冲突它查不出来——那部分仍要靠真机同开验证。"]

    text = "\n".join(lines) + "\n"
    if a.out:
        os.makedirs(os.path.dirname(os.path.abspath(a.out)) or ".", exist_ok=True)
        with open(a.out, "w", encoding="utf-8", newline="\n") as f:
            f.write(text)
        print("已写入 %s" % a.out)
    else:
        print(text)
    if a.json:
        print(json.dumps([{"level": f[0], "kind": f[1], "subject": f[2], "detail": f[3]} for f in findings],
                         ensure_ascii=False, indent=1))
    print("ERROR=%d WARN=%d INFO=%d" % (errs, warns, len(findings) - errs - warns))
    return 2 if errs else (3 if warns else 0)


if __name__ == "__main__":
    sys.exit(main())
