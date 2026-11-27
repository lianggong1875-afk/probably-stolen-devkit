# -*- coding: utf-8 -*-
"""深空当铺工作区：知识库自动打标、原版源码验真、检索与模板实测流水线。

设计原则（适配另一边持续往 workshop-distill/ 增量写入新档案的场景）：
  1. 零侵入打标：不改动 workshop-distill/*.md 正文，标签由规则抽取 + tags-override.json 侧车合并，
     任何时候另一边新增了第 7/8 批蒸馏档案，重跑 `build` 即可自动收录、打标、更新路由。
  2. 权威验真：直接对照 共享开发/reference/Assembly-CSharp/（944个原版.cs）和游戏真实 ModScanner.Scan。
  3. AI 友好检索：`query` 子命令按 domain / trust / capability / api / keyword 输出紧凑结构化卡片，
     同时强提醒 `must_avoid` 避坑红线，防止下位模型把反面教材当范本抄。

用法：
  python 工具库/kb-tools/kb_pipeline.py build
  python 工具库/kb-tools/kb_pipeline.py query --domain machines --trust L1,L2
  python 工具库/kb-tools/kb_pipeline.py query --api StoreEventManager
  python 工具库/kb-tools/kb_pipeline.py audit
  python 工具库/kb-tools/kb_pipeline.py verify-templates
"""
import argparse
import glob
import hashlib
import json
import os
import re
import shutil
import subprocess
import sys
from datetime import datetime

HERE = os.path.dirname(os.path.abspath(__file__))
WORKSPACE = os.path.abspath(os.path.join(HERE, "..", ".."))
KB_DIR = os.path.join(WORKSPACE, "共享开发", "模组制作经验库")
DISTILL_DIR = os.path.join(KB_DIR, "workshop-distill")
REF_DIR = os.path.join(WORKSPACE, "共享开发", "reference", "Assembly-CSharp")
TEMPLATES_DIR = os.path.join(WORKSPACE, "工具库", "templates", "mod-patterns")
SKILL_LOCAL = os.path.join(WORKSPACE, "工具库", "skills", "probably-stolen-moddev")
TAXONOMY_PATH = os.path.join(HERE, "taxonomy.json")
OVERRIDE_PATH = os.path.join(HERE, "tags-override.json")
CATALOG_JSON = os.path.join(KB_DIR, "CATALOG.json")
CATALOG_MD = os.path.join(KB_DIR, "CATALOG.md")
DIGEST_MD = os.path.join(KB_DIR, "DISTILL_DIGEST.md")
DEFAULT_MANAGED = r"D:\steam\steamapps\common\Probably Stolen\Probably Stolen_Data\Managed"

# 域关键词自动分类规则（面向增量新增的 workshop-distill/*.md 与未手标条目）
DOMAIN_RULES = {
    "items": [r"DirectoryMaster", r"GameItem\b", r"ItemSpawner", r"EnableTag", r"SetGameItemType", r"GraphUtils", r"GridShape", r"容器", r"拼柜", r"整理", r"排列"],
    "machines": [r"MachineHelper", r"MachineProgressHelper", r"ChemicalHelper", r"WaterHelper", r"Liquid\b", r"机器", r"流体", r"雪碧图", r"过夜.*化学"],
    "appraisal": [r"Appraisal", r"InspectionUIManager", r"realCondition", r"fakeCondition", r"ItemFeature", r"放大镜", r"鉴定", r"PaperUI", r"交互纸张"],
    "events": [r"StoreEvent", r"NewsUIManager", r"AdvCalendarUIManager", r"报纸", r"新闻", r"空间站事件", r"日历"],
    "afterhour": [r"Afterhour", r"MapUIManager", r"TransitUI", r"夜晚", r"夜出", r"打烊后", r"拍卖场"],
    "customers": [r"StoreClient", r"DialogUIManager", r"StoreReputation", r"PhoneBook", r"StorePhoneClient", r"顾客", r"访客", r"对话", r"声望"],
    "ui": [r"CustomUIManager", r"CustomUIBuilder", r"PixelWindow", r"LocHelper", r"TMP_Settings", r"FontLoader", r"Tooltip", r"快捷键", r"模态"],
    "economy": [r"GetCurrentValue", r"GetNegociatedValue", r"NegociationData", r"经济", r"交易所", r"行情", r"股票", r"贷款", r"公司", r"拍卖"],
    "persistence": [r"ES3", r"SaveGame", r"LoadGame", r"DecodeSaveItem", r"ModData", r"SaveManager", r"存档", r"读档", r"坏档"],
    "media": [r"SpriteDict", r"PortraitStudio", r"PixelPerfectCamera", r"AudioClip", r"立绘", r"配音", r"贴图", r"去边", r"高清"],
    "perf": [r"ConditionalWeakTable", r"Stopwatch", r"缓存", r"卡顿", r"分帧", r"优化", r"Lag", r"epoch"],
    "interop": [r"ModLoader", r"ModScanner", r"ModReflect", r"HarmonyBefore", r"HarmonyAfter", r"冲突", r"跨模组", r"SendMessage", r"Expose"],
    "multiplayer": [r"WebSocket", r"TcpClient", r"联机", r"Multiplayer", r"收件箱", r"外部进程"],
    "architecture": [r"DLC", r"总闸", r"DSL", r"脚手架", r"分层", r"仲裁", r"架构", r"框架", r"巨型"],
    "workflow": [r"Build-Mod", r"Run-Regression", r"SteamUGC", r"WorkshopUploader", r"ModDevBridge", r"热重载", r"真机", r"Slot 1", r"分支", r"门禁"],
}

ANTIPATTERN_SIGNALS = [
    (r"整段替换.*(?:RunNightlyChemicalSimulation|DecodeSaveItem|EndNight|SaveGame)", "整段替换原版入口方法，与同类模组互斥"),
    (r"空\s*`?catch\s*\{\s*\}`?", "包含大量空 catch {} 静默吞异常"),
    (r"new string\s*\(\s*char\[\]|绕过.*扫描|规避扫描", "尝试规避 ModScanner 安全扫描，严禁效仿"),
    (r"零鉴权|CORS\s*`?\*`?", "内嵌 HTTP 服务无鉴权/CORS 通配，存在跨站控制风险"),
    (r"吞掉原版\s*fallback", "吞掉原版回退链，可能导致缺失语言退化为裸键"),
]

# 明确不属于 Assembly-CSharp 成员校验的常见外部/Unity/CLR 类型
SKIP_TYPES = {
    "UnityEngine", "System", "Debug", "File", "Directory", "Path", "Mathf",
    "Vector2", "Vector3", "Vector4", "Color", "Time", "Input", "Screen",
    "Camera", "GameObject", "Transform", "RectTransform", "Canvas", "Sprite",
    "Texture2D", "RenderTexture", "Graphics", "AudioClip", "AudioSource",
    "Resources", "SceneManager", "ConditionalWeakTable", "Encoding", "Convert",
    "Array", "Enumerable", "String", "Int32", "ModConfig", "JsonUtility",
    "Newtonsoft", "Harmony", "Priority", "AccessTools", "EventSystem",
    "TMP_Settings", "IPAddress", "CancellationTokenSource", "Stopwatch",
    "Thread", "SteamUGC", "SteamAPI", "CallResult", "Environment",
    "PlayerPrefs", "Physics", "Object", "Component", "Behaviour",
    "MonoBehaviour", "Image", "RawImage", "ScrollRect", "LayoutElement",
    "TMP_InputField", "Application", "GUILayout", "GUI", "Type", "Delegate",
    "OpCodes", "BindingFlags", "StringComparer", "StringComparison", "Guid",
    "Console", "Math", "Task", "List", "Dictionary", "HashSet", "Action",
    "Func", "Exception", "InvalidOperationException", "UnauthorizedAccessException",
    "XmlDocument", "MethodBase", "FieldInfo", "MethodInfo", "Assembly",
    "FileInfo", "DirectoryInfo", "MemoryStream", "StreamReader", "StreamWriter",
    "PixelPerfectCamera", "ES3", "ES3Type", "ES3Types",
}

SKIP_MEMBERS = {"cs", "md", "dll", "json", "xml", "png", "wav", "txt", "ps1", "py", "exe", "cmd", "es3", "zoh"}


def read_text(path):
    with open(path, encoding="utf-8-sig", errors="replace") as f:
        return f.read()


def rel_posix(path):
    return os.path.relpath(path, WORKSPACE).replace("\\", "/")


def sha256_file(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(65536), b""):
            h.update(chunk)
    return h.hexdigest()


def load_ref_types():
    types = {}
    if not os.path.isdir(REF_DIR):
        return types
    for p in glob.glob(os.path.join(REF_DIR, "*.cs")):
        name = os.path.splitext(os.path.basename(p))[0]
        types[name] = read_text(p)
    return types


def extract_title(text, fallback):
    for line in text.splitlines():
        s = line.strip()
        if s.startswith("# "):
            return s[2:].strip()
    return fallback


def infer_domains(text, max_domains=4):
    scores = []
    for dom, pats in DOMAIN_RULES.items():
        score = 0
        for pat in pats:
            hits = re.findall(pat, text, re.I)
            if hits:
                score += min(len(hits), 4)
        if score >= 2:
            scores.append((score, dom))
    scores.sort(reverse=True)
    if not scores:
        return ["architecture"]
    return [d for _, d in scores[:max_domains]]


def infer_capability(text, layer):
    if layer == "tool-script":
        return "n/a"
    if re.search(r"<Capabilities\s*/>|零能力|Refused\s*=\s*False.*Capabilities.*空|无明显高权", text):
        if re.search(r"HttpListener|WebSocket|TcpClient|Socket|network", text):
            return "mixed"
        return "zero-cap"
    caps = set()
    if re.search(r"HttpListener|WebSocket|TcpClient|UdpClient|\bnetwork\b", text):
        caps.add("cap-network")
    if re.search(r"File\.Read|File\.Write|Directory\.Get|StreamWriter|需\s*`?files`?", text):
        caps.add("cap-files")
    if re.search(r"reflection-external", text):
        caps.add("cap-reflection")
    if len(caps) > 1:
        return "mixed"
    if len(caps) == 1:
        return next(iter(caps))
    return "zero-cap" if layer in ("template-cs", "workshop-distill") else "n/a"


def extract_api_symbols(text, ref_types, limit=16):
    found = {}
    for m in re.finditer(r"\b([A-Z][A-Za-z0-9_]{2,})\b", text):
        sym = m.group(1)
        if sym in ref_types and sym not in SKIP_TYPES:
            found[sym] = found.get(sym, 0) + 1
    ordered = sorted(found.items(), key=lambda kv: (-kv[1], kv[0]))
    return [k for k, _ in ordered[:limit]]


def extract_workshop_ids(filename, text):
    ids = set(re.findall(r"\b(38\d{8})\b", filename + "\n" + text[:2500]))
    return sorted(ids)


def detect_antipatterns(text):
    notes = []
    for pat, msg in ANTIPATTERN_SIGNALS:
        for m in re.finditer(pat, text, re.I):
            start = max(0, m.start() - 28)
            ctx = text[start:m.start()]
            # 排除明确否定/对比语境（例如"没有整段替换""0 处空 catch""与整段替换正好相反"）
            if re.search(r"没有|0\s*处|零\s*处|绝不|严禁|勿抄|别抄|相反|未见|而非", ctx):
                continue
            notes.append(msg)
            break
    return notes


def summarize_best_for(text, title):
    for line in text.splitlines()[:25]:
        s = line.strip()
        m = re.match(r"^[>\-\*\s]*\*\*(?:什么时候读它?|一句话价值|这份文件回答|定位|解决什么问题)\*\*[：:]\s*(.+)", s)
        if m:
            return m.group(1).strip()[:140]
        m2 = re.match(r"^[>\-\*\s]*(?:定位|一句话价值|用法)[：:]\s*(.+)", s)
        if m2:
            return m2.group(1).strip()[:140]
    return title


def build_catalog(out_dir=None):
    taxonomy = json.loads(read_text(TAXONOMY_PATH))
    overrides = json.loads(read_text(OVERRIDE_PATH)).get("entries", {})
    ref_types = load_ref_types()
    target_json = os.path.join(out_dir, "CATALOG.json") if out_dir else CATALOG_JSON
    target_md = os.path.join(out_dir, "CATALOG.md") if out_dir else CATALOG_MD
    target_digest = os.path.join(out_dir, "DISTILL_DIGEST.md") if out_dir else DIGEST_MD
    if out_dir:
        os.makedirs(out_dir, exist_ok=True)

    entries = []

    # 1. 经验库主文档（01..20）+ 核心协作文档
    guide_paths = sorted(glob.glob(os.path.join(KB_DIR, "[0-9][0-9]-*.md")))
    extra_guides = [
        os.path.join(WORKSPACE, "共享开发", "开发流程与经验.md"),
        os.path.join(WORKSPACE, "共享开发", "GLOSSARY.md"),
        os.path.join(WORKSPACE, "共享开发", "分支与备份规范.md"),
        os.path.join(WORKSPACE, "共享开发", "SKILL.md"),
    ]
    for p in guide_paths + [g for g in extra_guides if os.path.isfile(g)]:
        rel = rel_posix(p)
        txt = read_text(p)
        title = extract_title(txt, os.path.basename(p))
        ov = overrides.get(rel, {})
        layer = "skill" if rel == "共享开发/SKILL.md" else "doc-guide"
        entry = {
            "id": os.path.splitext(os.path.basename(p))[0],
            "path": rel,
            "title": title,
            "layer": layer,
            "domains": ov.get("domains", infer_domains(txt)),
            "trust": ov.get("trust", "L2-verified-host"),
            "capability": ov.get("capability", infer_capability(txt, layer)),
            "intent": ov.get("intent", ["api-lookup"]),
            "status": ov.get("status", "active"),
            "lines": len(txt.splitlines()),
            "workshop_ids": extract_workshop_ids(os.path.basename(p), txt),
            "api_symbols": extract_api_symbols(txt, ref_types, limit=18),
            "best_for": ov.get("best_for", summarize_best_for(txt, title)),
            "must_avoid": ov.get("must_avoid", ""),
        }
        entries.append(entry)

    # 2. 工坊蒸馏档案（自动扫描全部 .md，天然兼容另一边持续增量写入）
    for p in sorted(glob.glob(os.path.join(DISTILL_DIR, "*.md"))):
        rel = rel_posix(p)
        txt = read_text(p)
        title = extract_title(txt, os.path.basename(p))
        ov = overrides.get(rel, {})
        anti = detect_antipatterns(txt)
        default_trust = "L4-caution-antipattern" if anti else "L3-distilled-source"
        entry = {
            "id": "distill:" + os.path.splitext(os.path.basename(p))[0],
            "path": rel,
            "title": title,
            "layer": "workshop-distill",
            "domains": ov.get("domains", infer_domains(txt)),
            "trust": ov.get("trust", default_trust),
            "capability": ov.get("capability", infer_capability(txt, "workshop-distill")),
            "intent": ov.get("intent", ["case-study"]),
            "status": ov.get("status", "active"),
            "lines": len(txt.splitlines()),
            "workshop_ids": extract_workshop_ids(os.path.basename(p), txt),
            "api_symbols": extract_api_symbols(txt, ref_types, limit=14),
            "best_for": ov.get("best_for", summarize_best_for(txt, title)),
            "digest": ov.get("digest", ""),
            "must_avoid": ov.get("must_avoid", "") if rel in overrides else ("；".join(anti[:2]) if anti else ""),
            "curated": rel in overrides,
        }
        entries.append(entry)

    # 3. C# 架构模板（13 个）
    for p in sorted(glob.glob(os.path.join(TEMPLATES_DIR, "*.cs"))):
        rel = rel_posix(p)
        txt = read_text(p)
        ov = overrides.get(rel, {})
        entry = {
            "id": "tpl:" + os.path.splitext(os.path.basename(p))[0],
            "path": rel,
            "title": os.path.basename(p),
            "layer": "template-cs",
            "domains": ov.get("domains", infer_domains(txt, max_domains=3)),
            "trust": ov.get("trust", "L1-verified-runtime"),
            "capability": ov.get("capability", "zero-cap"),
            "intent": ov.get("intent", ["pattern-reuse"]),
            "status": ov.get("status", "active"),
            "lines": len(txt.splitlines()),
            "workshop_ids": extract_workshop_ids(os.path.basename(p), txt),
            "api_symbols": extract_api_symbols(txt, ref_types, limit=12),
            "best_for": ov.get("best_for", os.path.basename(p)),
            "must_avoid": ov.get("must_avoid", ""),
        }
        entries.append(entry)

    # 4. 核心工具脚本与主技能
    tool_catalog = [
        ("工具库/build/Build-Mod.ps1", ["workflow"], "L1-verified-runtime", "n/a", ["tooling"], "统一编译开发版/-PublicRelease零能力版并支持-Scan/-Install"),
        ("工具库/build/Test-ArcadeLoaderScan.ps1", ["workflow", "interop"], "L1-verified-runtime", "n/a", ["tooling"], "调用游戏真实 ModScanner 离线扫描候选 DLL 权限"),
        ("工具库/build/New-FeatureBranch.ps1", ["workflow"], "L1-verified-runtime", "n/a", ["tooling"], "按规范同时创建本地与云端功能分支"),
        ("工具库/build/Invoke-PreMergeGate.ps1", ["workflow", "interop"], "L1-verified-runtime", "n/a", ["tooling"], "合并前五道门禁一键执行（编译/零能力/回归/冲突/洁净）"),
        ("工具库/build/New-ReleasePackage.ps1", ["workflow"], "L1-verified-runtime", "n/a", ["tooling"], "工坊零能力发布封装、封面与白名单校验"),
        ("工具库/build/Publish-Workshop.ps1", ["workflow"], "L1-verified-runtime", "n/a", ["tooling"], "工坊打包上传（须用户确认；默认不覆盖线上描述）"),
        ("工具库/build/Backup-All.ps1", ["workflow"], "L1-verified-runtime", "n/a", ["tooling"], "本地裸仓镜像 + bundle + 美术文案资产包一键多路备份"),
        ("工具库/regression/Run-Regression.ps1", ["workflow"], "L1-verified-runtime", "n/a", ["tooling"], "运行离线真行为与核心不变量回归套件"),
        ("工具库/workshop-tools/fetch_workshop.py", ["workflow"], "L1-verified-runtime", "n/a", ["tooling"], "抓取 Steam 创意工坊列表/简介摘要/本地订阅对比"),
        ("工具库/workshop-tools/distill_prep.py", ["workflow"], "L1-verified-runtime", "n/a", ["tooling"], "批量挑主 DLL 反编译并生成透视报告"),
        ("工具库/workshop-tools/conflict_scan.py", ["interop", "workflow"], "L1-verified-runtime", "n/a", ["tooling"], "跨模组静态冲突扫描（支持 --gate 与 HarmonyBefore/After 识别）"),
        ("工具库/skills/decomp-api/inspect_mod.py", ["workflow", "interop"], "L1-verified-runtime", "n/a", ["tooling"], "秒级透视源码目录或 DLL 的 IMod/HarmonyPatch/Hook/权限信号"),
        ("工具库/skills/decomp-api/extract_api.py", ["workflow"], "L1-verified-runtime", "n/a", ["tooling"], "从反编译 .cs 秒级提取公开 API 清单"),
        ("工具库/kb-tools/kb_pipeline.py", ["workflow", "architecture"], "L1-verified-runtime", "n/a", ["tooling"], "知识库自动打标、原版源码验真、按标签秒检、13模板零能力实测"),
        ("工具库/kb-tools/Sync-ModdevSkill.ps1", ["workflow"], "L1-verified-runtime", "n/a", ["tooling"], "对齐双副本脚本并同步 probably-stolen-moddev 到 DSH_HOME（SHA-256 核验）"),
        ("工具库/img/pack-spritesheet.py", ["machines", "media"], "L1-verified-runtime", "n/a", ["tooling"], "机器 8 列雪碧图打包/校验/拆解"),
        ("工具库/img/newsprint-tone.py", ["events", "media"], "L1-verified-runtime", "n/a", ["tooling"], "9 色泛黄新闻纸 + 2x2 半色调网点图片转换"),
        ("工具库/skills/probably-stolen-moddev/SKILL.md", ["architecture", "items", "machines", "appraisal", "events", "afterhour", "media", "interop", "workflow"], "L1-verified-runtime", "zero-cap", ["must-read-before-coding", "pattern-reuse"], "《深空当铺》全流程核心领域开发技能（路由总入口）"),
        ("共享开发/AI技能库/README.md", ["workflow"], "L1-verified-runtime", "n/a", ["tooling"], "流程层 AI 技能库（mattpocock/skills 27 个中文化方法论与工作区落地）"),
    ]
    for rel, doms, trust, cap, intent, desc in tool_catalog:
        full = os.path.join(WORKSPACE, rel.replace("/", os.sep))
        if not os.path.isfile(full):
            continue
        txt = read_text(full)
        layer = "skill" if rel.endswith("SKILL.md") or "AI技能库" in rel else "tool-script"
        entries.append({
            "id": ("skill:" if layer == "skill" else "tool:") + os.path.basename(full),
            "path": rel,
            "title": extract_title(txt, os.path.basename(full)) if rel.endswith(".md") else os.path.basename(full),
            "layer": layer,
            "domains": doms,
            "trust": trust,
            "capability": cap,
            "intent": intent,
            "status": "active",
            "lines": len(txt.splitlines()),
            "workshop_ids": [],
            "api_symbols": [],
            "best_for": desc,
            "must_avoid": "",
        })

    # 汇总统计与原版符号反向索引
    stats = {
        "total": len(entries),
        "by_layer": {},
        "by_trust": {},
        "by_domain": {},
        "unique_workshop_ids": len({wid for e in entries for wid in e["workshop_ids"]}),
    }
    api_index = {}
    for e in entries:
        stats["by_layer"][e["layer"]] = stats["by_layer"].get(e["layer"], 0) + 1
        stats["by_trust"][e["trust"]] = stats["by_trust"].get(e["trust"], 0) + 1
        for d in e["domains"]:
            stats["by_domain"][d] = stats["by_domain"].get(d, 0) + 1
        for sym in e["api_symbols"]:
            api_index.setdefault(sym, []).append(e["path"])

    catalog = {
        "schema_version": "1.0",
        "generated_at": datetime.now().strftime("%Y-%m-%d %H:%M:%S"),
        "taxonomy_ref": "工具库/kb-tools/taxonomy.json",
        "stats": stats,
        "entries": entries,
        "api_index": {k: v for k, v in sorted(api_index.items()) if len(v) >= 2},
    }

    with open(target_json, "w", encoding="utf-8", newline="\n") as f:
        json.dump(catalog, f, ensure_ascii=False, indent=2)

    write_catalog_md(catalog, taxonomy, target_md)
    write_digest_md(catalog, target_digest)
    distill_total = stats["by_layer"].get("workshop-distill", 0)
    curated_cnt = sum(1 for e in entries if e["layer"] == "workshop-distill" and e.get("curated"))
    print(f"[OK] CATALOG.json / CATALOG.md / DISTILL_DIGEST.md 已生成（目录：{os.path.dirname(target_json)}）：共 {stats['total']} 项 "
          f"(doc-guide={stats['by_layer'].get('doc-guide',0)}, "
          f"workshop-distill={distill_total}[精标{curated_cnt}/{distill_total}], "
          f"template-cs={stats['by_layer'].get('template-cs',0)}, "
          f"tool-script={stats['by_layer'].get('tool-script',0)}, "
          f"skill={stats['by_layer'].get('skill',0)}), "
          f"涵盖 {stats['unique_workshop_ids']} 个独立工坊 ID。")


def trust_badge(trust):
    return {
        "L1-verified-runtime": "`L1实测`",
        "L2-verified-host": "`L2源码核对`",
        "L3-distilled-source": "`L3反编译`",
        "L4-caution-antipattern": "`L4⚠️含禁抄`",
    }.get(trust, f"`{trust}`")


def write_catalog_md(catalog, taxonomy, target_md=CATALOG_MD):
    """极致压缩版总索引：每项仅出现一次，按域一行速查，彻底消除跨域重复膨胀。"""
    dom_desc = taxonomy["dimensions"]["domain"]
    entries = [e for e in catalog["entries"] if e.get("status") != "superseded"]
    st = catalog["stats"]

    lines = [
        "# 深空当铺工作区｜压缩版打标总索引（CATALOG）",
        "",
        f"> **自动生成**（`python 工具库\\kb-tools\\kb_pipeline.py build`）｜更新时间：`{catalog['generated_at']}`",
        "> **零侵入并发设计**：不改 `workshop-distill/*.md` 正文；新批次落盘后重跑 `build` 即可刷新本索引与 [蒸馏压缩速查卡](<DISTILL_DIGEST.md>)。",
        "> **秒级检索**：`python 工具库\\kb-tools\\kb_pipeline.py query --domain <域> [--trust L1,L2] [--api <原版类>] [--keyword <词>]`",
        "",
        f"- **总计**：**{st['total']}** 项（规范 `{st['by_layer'].get('doc-guide',0)}` ｜ 蒸馏档案 `{st['by_layer'].get('workshop-distill',0)}` ｜ 零能力 C# 模板 `{st['by_layer'].get('template-cs',0)}` ｜ 工具 `{st['by_layer'].get('tool-script',0)}` ｜ 技能 `{st['by_layer'].get('skill',0)}`），覆盖 **{st['unique_workshop_ids']}** 个独立工坊 ID。",
        "- **可信度**：`L1实测`（编译/ModScanner/真机验过）＞ `L2源码核对`（核对 `Assembly-CSharp`）＞ `L3反编译` ＞ `L4⚠️含禁抄`（仅学原理，严禁照抄）。",
        "",
        "## 1. 按系统域一行路由（零重复）",
        "",
        "| 域 | 适用场景 | 核心规范 / 模板 / 关键工具与代表蒸馏 |",
        "| --- | --- | --- |",
    ]

    layer_order = {"skill": 0, "doc-guide": 1, "template-cs": 2, "tool-script": 3, "workshop-distill": 4}
    for dom, desc in dom_desc.items():
        matched = [e for e in entries if dom in e["domains"]]
        if not matched:
            continue
        matched.sort(key=lambda e: (layer_order.get(e["layer"], 9), e["trust"], e["path"]))
        picks = []
        for e in matched:
            if e["layer"] == "skill" and len(matched) > 5:
                continue
            rel_from_kb = os.path.relpath(os.path.join(WORKSPACE, e["path"].replace("/", os.sep)), KB_DIR).replace("\\", "/")
            short = os.path.splitext(os.path.basename(e["path"]))[0]
            if e["layer"] == "template-cs":
                short = os.path.basename(e["path"])
            prefix = "⚠️" if e["trust"].startswith("L4") else ""
            picks.append(f"{prefix}[{short}](<{rel_from_kb}>)")
        lines.append(f"| `{dom}` | {desc} | {' · '.join(picks[:12])}{' …' if len(picks) > 12 else ''} |")

    lines.extend(["", "## 2. 全量资产唯一总表（每项只写 1 次）", ""])
    sections = [
        ("核心经验规范与工程手册", "doc-guide"),
        ("零能力 C# 架构模板（13 个全通过真实编译 + ModScanner）", "template-cs"),
        ("工坊反编译蒸馏档案（压缩要点见 DISTILL_DIGEST.md）", "workshop-distill"),
        ("工具脚本与 AI 技能", "tool-script"),
    ]
    for sec_title, target_layer in sections:
        group = [e for e in entries if e["layer"] == target_layer or (target_layer == "tool-script" and e["layer"] == "skill")]
        group.sort(key=lambda e: (e["trust"], e["path"]))
        lines.append(f"### {sec_title}（{len(group)} 项）")
        lines.append("")
        lines.append("| 可信度 | 能力 | 域 | 文件 | 一句话用途 / ⚠️ 别抄 |")
        lines.append("| --- | --- | --- | --- | --- |")
        for e in group:
            rel_from_kb = os.path.relpath(os.path.join(WORKSPACE, e["path"].replace("/", os.sep)), KB_DIR).replace("\\", "/")
            label = os.path.basename(e["path"])
            doms = ",".join(e["domains"][:3])
            note = e["best_for"].replace("|", "\\|")
            if e["must_avoid"]:
                note += f" **⚠️ {e['must_avoid'].replace('|', '\\|')}**"
            lines.append(f"| {trust_badge(e['trust'])} | `{e['capability']}` | `{doms}` | [{label}](<{rel_from_kb}>) | {note} |")
        lines.append("")

    cautions = [e for e in entries if e["must_avoid"]]
    if cautions:
        lines.append("## 3. ⚠️ 全库反面教材与禁抄红线汇总")
        lines.append("")
        lines.append("| 资料 | 可信度 | 绝对不要抄什么 |")
        lines.append("| --- | --- | --- |")
        for e in cautions:
            rel_from_kb = os.path.relpath(os.path.join(WORKSPACE, e["path"].replace("/", os.sep)), KB_DIR).replace("\\", "/")
            lines.append(f"| [{os.path.basename(e['path'])}](<{rel_from_kb}>) | {trust_badge(e['trust'])} | {e['must_avoid'].replace('|', '\\|')} |")
        lines.append("")

    with open(target_md, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lines))


def write_digest_md(catalog, target_digest=DIGEST_MD):
    """生成超紧凑蒸馏速查压缩卡：把 1.3MB+ 原始蒸馏档案压缩为 ~110 行高密度决策卡片。"""
    distills = [e for e in catalog["entries"] if e["layer"] == "workshop-distill"]
    total_lines = sum(e.get("lines", 0) for e in distills)

    clusters = [
        ("一、大型 / DLC / 巨型架构与合集组织", {"architecture"}),
        ("二、机器、生产链、自动化与流体", {"machines"}),
        ("三、物品、容器、整理排序与编辑", {"items"}),
        ("四、事件、经济、拍卖与跨日玩法", {"events", "economy"}),
        ("五、鉴定、放大镜、顾客与夜出", {"appraisal", "afterhour", "customers"}),
        ("六、UI、字体本地化与交互隔离", {"ui"}),
        ("七、立绘、美术规格与贴图框架", {"media"}),
        ("八、性能优化、缓存与读档管线", {"perf", "persistence"}),
        ("九、跨模组互操作、冲突、联机与外部通道", {"interop", "multiplayer", "workflow"}),
    ]

    lines = [
        "# 工坊蒸馏知识｜高密度压缩速查卡（DISTILL_DIGEST）",
        "",
        f"> **压缩源**：{len(distills)} 份工坊蒸馏档案（原始 {total_lines:,} 行 / 涵盖 {catalog['stats']['unique_workshop_ids']} 个独立工坊 ID）→ 无损提纯为一张紧凑速查卡。更新时间：`{catalog['generated_at']}`。",
        "> **用法**：写方案/排雷前先扫这张卡（每条 1 行直给结论 + ⚠️ 禁抄红线），需要原样 C# 行号证据时再点开对应档案。",
        "",
    ]

    placed = set()
    for heading, dom_set in clusters:
        bucket = []
        for e in distills:
            if e["path"] in placed:
                continue
            # 按主域（第一个 domain）优先归桶，避免同一份档案被印两遍
            primary = e["domains"][0] if e["domains"] else "architecture"
            if primary in dom_set:
                bucket.append(e)
                placed.add(e["path"])
        if not bucket:
            continue
        bucket.sort(key=lambda x: (x["trust"], x["path"]))
        lines.append(f"## {heading}（{len(bucket)} 份）")
        lines.append("")
        for e in bucket:
            rel_from_kb = os.path.relpath(os.path.join(WORKSPACE, e["path"].replace("/", os.sep)), KB_DIR).replace("\\", "/")
            short = os.path.splitext(os.path.basename(e["path"]))[0]
            body = (e.get("digest") or e["best_for"]).strip()
            avoid = f" ｜ **⚠️ 别抄**：{e['must_avoid']}" if e.get("must_avoid") else ""
            lines.append(f"- {trust_badge(e['trust'])} [{short}](<{rel_from_kb}>)：{body}{avoid}")
        lines.append("")

    # 兜底未归桶项
    rem = [e for e in distills if e["path"] not in placed]
    if rem:
        lines.append(f"## 十、其他新增蒸馏档案（{len(rem)} 份）")
        lines.append("")
        for e in rem:
            rel_from_kb = os.path.relpath(os.path.join(WORKSPACE, e["path"].replace("/", os.sep)), KB_DIR).replace("\\", "/")
            short = os.path.splitext(os.path.basename(e["path"]))[0]
            body = (e.get("digest") or e["best_for"]).strip()
            avoid = f" ｜ **⚠️ 别抄**：{e['must_avoid']}" if e.get("must_avoid") else ""
            lines.append(f"- {trust_badge(e['trust'])} [{short}](<{rel_from_kb}>)：{body}{avoid}")
        lines.append("")

    with open(target_digest, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(lines))


def cmd_query(args):
    if not os.path.isfile(CATALOG_JSON):
        print("[ERROR] 尚未生成 CATALOG.json，请先运行 build", file=sys.stderr)
        sys.exit(2)
    catalog = json.loads(read_text(CATALOG_JSON))
    entries = catalog["entries"]

    trusts = set(t.strip() for t in args.trust.split(",") if t.strip()) if args.trust else None
    results = []
    for e in entries:
        if args.layer and e["layer"] != args.layer:
            continue
        if args.domain and args.domain not in e["domains"]:
            continue
        if args.capability and e["capability"] != args.capability:
            continue
        if trusts and not any(e["trust"].startswith(t) for t in trusts):
            continue
        if args.api:
            api_q = args.api.lower()
            if not any(api_q in s.lower() for s in e["api_symbols"]):
                continue
        if args.keyword:
            kw = args.keyword.lower()
            blob = " ".join([e["title"], e["path"], e["best_for"], e["must_avoid"], " ".join(e["api_symbols"]), " ".join(e["workshop_ids"])]).lower()
            if kw not in blob:
                continue
        results.append(e)

    layer_order = {"doc-guide": 0, "template-cs": 1, "skill": 2, "tool-script": 3, "workshop-distill": 4}
    results.sort(key=lambda e: (layer_order.get(e["layer"], 9), e["trust"], e["path"]))
    results = results[: args.limit]

    if args.json:
        print(json.dumps(results, ensure_ascii=False, indent=2))
        return

    print(f"# 命中 {len(results)} 项（总库 {catalog['stats']['total']} 项）")
    for e in results:
        avoid = f"\n  ⚠️ 别抄：{e['must_avoid']}" if e["must_avoid"] else ""
        dig = f"\n  压缩结论：{e['digest']}" if e.get("digest") else ""
        apis = ", ".join(e["api_symbols"][:8])
        api_line = f"\n  原版类：{apis}" if apis else ""
        print(f"- [{e['trust']}] [{e['layer']}] [{e['capability']}] {e['path']}\n  用途：{e['best_for']}{dig}{api_line}{avoid}")


def check_member_in_cs(cs_text, member):
    if re.search(r"\b" + re.escape(member) + r"\b", cs_text):
        return True
    # 支持 IL getter/setter 命名（例如 get_widthPixels -> widthPixels）
    if member.startswith(("get_", "set_")) and len(member) > 4:
        prop = member[4:]
        if re.search(r"\b" + re.escape(prop) + r"\b", cs_text):
            return True
    return False


def cmd_audit(_args):
    errors = []
    warns = []
    ref_types = load_ref_types()

    # 1. 原版核心扩展面实测数量核对
    if "ModHelper" in ref_types and "ModHook" in ref_types and "GameItemFunc" in ref_types:
        mh_cnt = len(re.findall(r"^\s*public\s+static\s+[^\(=;\{]+\s+(\w+)\s*(?:<[^>]+>)?\s*\(", ref_types["ModHelper"], re.M))
        hk_cnt = len(re.findall(r"^\s*public\s+static\s+event\s+[\w<>,\.\s]+\s+(\w+)\s*;", ref_types["ModHook"], re.M))
        fn_cnt = len(re.findall(r"^\s*public\s+[\w<>,\.\s\[\]]+\s+(\w+)\s*\{\s*get\s*;\s*set\s*;", ref_types["GameItemFunc"], re.M))
        print(f"[INFO] 原版源码实测：ModHelper={mh_cnt} 方法, ModHook={hk_cnt} 事件, GameItemFunc={fn_cnt} 委托")
        for p in [
            os.path.join(KB_DIR, "20-各类模组制作规范.md"),
            os.path.join(SKILL_LOCAL, "SKILL.md"),
            os.path.join(SKILL_LOCAL, "references", "mod-type-standards.md"),
        ]:
            if os.path.isfile(p):
                t = read_text(p)
                if "37 个钩子" in t or "144 个" in t:
                    warns.append(f"旧版 API 计数未同步：{rel_posix(p)}（应为 ModHelper {mh_cnt} / ModHook {hk_cnt}）")

    # 2. 原版 Type.Member 符号验真（覆盖经验库主文档 + 技能文档）
    doc_files = sorted(
        glob.glob(os.path.join(KB_DIR, "[0-9][0-9]-*.md"))
        + glob.glob(os.path.join(SKILL_LOCAL, "references", "*.md"))
        + [os.path.join(SKILL_LOCAL, "SKILL.md"), os.path.join(WORKSPACE, "共享开发", "开发流程与经验.md")]
    )
    ref_pat = re.compile(r"`([A-Z][A-Za-z0-9_]+)\.([A-Za-z_][A-Za-z0-9_]+)")
    checked = 0
    for doc in doc_files:
        if not os.path.isfile(doc):
            continue
        txt = read_text(doc)
        rel = rel_posix(doc)
        seen_local = set()
        for m in ref_pat.finditer(txt):
            typ, mem = m.group(1), m.group(2)
            if typ in SKIP_TYPES or mem in SKIP_MEMBERS or typ not in ref_types:
                continue
            checked += 1
            if not check_member_in_cs(ref_types[typ], mem):
                # 忽略继承自 MonoBehaviour/Object 的通用成员（如 name / transform / gameObject / enabled）
                if mem in ("name", "get_name", "set_name", "transform", "gameObject", "enabled", "Instance", "GetComponent", "SetActive"):
                    continue
                key = (rel, typ, mem)
                if key not in seen_local:
                    seen_local.add(key)
                    errors.append(f"原版源码中未找到符号 {typ}.{mem}（出现于 {rel}）")
    print(f"[INFO] 已对照 Assembly-CSharp 核验 {checked} 处反引号 API 引用")

    # 3. 经验库文件名编号与一级标题一致性
    for p in sorted(glob.glob(os.path.join(KB_DIR, "[0-9][0-9]-*.md"))):
        base = os.path.basename(p)
        file_num = base[:2]
        first = extract_title(read_text(p), "")
        m = re.match(r"^(\d{2})[\-\s]", first)
        if m and m.group(1) != file_num:
            errors.append(f"标题编号与文件名不符：{rel_posix(p)} 标题为 '# {first}'")

    # 4. 本机无 pwsh 铁律检查（文档中教人敲 `pwsh -File`）
    for p in doc_files + [os.path.join(WORKSPACE, "共享开发", "AI技能库", "README.md")]:
        if os.path.isfile(p):
            txt = read_text(p)
            if re.search(r"^\s*pwsh\s+-File", txt, re.M):
                warns.append(f"文档含本机不存在的 `pwsh -File` 命令：{rel_posix(p)}")

    # 5. 双副本脚本与技能同步哈希一致性检查
    hash_pairs = [
        ("conflict_scan.py", os.path.join(WORKSPACE, "工具库", "workshop-tools", "conflict_scan.py"),
         os.path.join(SKILL_LOCAL, "scripts", "conflict_scan.py")),
        ("inspect_mod.py", os.path.join(WORKSPACE, "工具库", "skills", "decomp-api", "inspect_mod.py"),
         os.path.join(SKILL_LOCAL, "scripts", "inspect_mod.py")),
    ]
    for label, a, b in hash_pairs:
        if os.path.isfile(a) and os.path.isfile(b):
            if sha256_file(a) != sha256_file(b):
                errors.append(f"双副本哈希漂移 [{label}]：{rel_posix(a)} != {rel_posix(b)}")

    dsh_home = os.environ.get("DSH_HOME", "")
    if dsh_home:
        dsh_skill = os.path.join(dsh_home, "skills", "probably-stolen-moddev")
        if os.path.isdir(dsh_skill):
            local_files = {os.path.relpath(p, SKILL_LOCAL).replace("\\", "/"): sha256_file(p)
                           for p in glob.glob(os.path.join(SKILL_LOCAL, "**", "*"), recursive=True) if os.path.isfile(p)}
            dsh_files = {os.path.relpath(p, dsh_skill).replace("\\", "/"): sha256_file(p)
                         for p in glob.glob(os.path.join(dsh_skill, "**", "*"), recursive=True) if os.path.isfile(p)}
            if local_files != dsh_files:
                diff_keys = sorted(set(local_files) ^ set(dsh_files)) + sorted(
                    k for k in set(local_files) & set(dsh_files) if local_files[k] != dsh_files[k]
                )
                errors.append(f"本地技能与 DSH_HOME 不同步（{len(diff_keys)} 个文件差异）：{', '.join(diff_keys[:6])}")

    # 6. 增量覆盖率与人工精标覆盖率检查（另一边新蒸馏的档案是否已 build 进 CATALOG.json）
    if os.path.isfile(CATALOG_JSON):
        cat_entries = json.loads(read_text(CATALOG_JSON))["entries"]
        cat_paths = {e["path"] for e in cat_entries}
        disk_distills = {rel_posix(p) for p in glob.glob(os.path.join(DISTILL_DIR, "*.md"))}
        missing_in_cat = sorted(disk_distills - cat_paths)
        if missing_in_cat:
            warns.append(f"workshop-distill 新增 {len(missing_in_cat)} 份档案尚未进 CATALOG.json，请运行 build：{', '.join(os.path.basename(x) for x in missing_in_cat[:5])}")
        distill_entries = [e for e in cat_entries if e["layer"] == "workshop-distill"]
        uncurated = [os.path.basename(e["path"]) for e in distill_entries if not e.get("curated")]
        print(f"[INFO] 蒸馏档案精标覆盖率：{len(distill_entries) - len(uncurated)}/{len(distill_entries)}")
        if uncurated:
            warns.append(f"有 {len(uncurated)} 份新增蒸馏档案尚未使用侧车精标（已自动打标，可按需补 tags-override.json）：{', '.join(uncurated[:5])}")
    else:
        errors.append("缺少 CATALOG.json，请先运行 build")

    print(f"=== AUDIT SUMMARY: ERROR={len(errors)}, WARN={len(warns)} ===")
    for e in errors:
        print(f"  [ERROR] {e}")
    for w in warns:
        print(f"  [WARN]  {w}")

    if errors:
        sys.exit(2)


def cmd_verify_templates(args):
    managed = args.managed or DEFAULT_MANAGED
    if not os.path.isdir(managed):
        print(f"[ERROR] 找不到游戏 Managed 目录：{managed}", file=sys.stderr)
        sys.exit(2)

    tmp_dir = os.path.join(WORKSPACE, "缓存文件夹", "kb-template-verify")
    if os.path.isdir(tmp_dir):
        shutil.rmtree(tmp_dir)
    os.makedirs(tmp_dir, exist_ok=True)

    cs_files = sorted(glob.glob(os.path.join(TEMPLATES_DIR, "*.cs")))
    compile_items = "\n".join(f'    <Compile Include="{p}" />' for p in cs_files)
    refs = [
        "Assembly-CSharp", "Assembly-CSharp-firstpass", "0Harmony",
        "UnityEngine", "UnityEngine.CoreModule", "UnityEngine.UI",
        "UnityEngine.UIModule", "UnityEngine.InputLegacyModule",
        "UnityEngine.ImageConversionModule", "UnityEngine.AudioModule",
        "Unity.2D.PixelPerfect", "Unity.TextMeshPro", "netstandard",
    ]
    ref_items = "\n".join(f'    <Reference Include="{r}"><HintPath>{os.path.join(managed, r + ".dll")}</HintPath></Reference>' for r in refs)
    csproj = f"""<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net472</TargetFramework>
    <LangVersion>9.0</LangVersion>
    <AssemblyName>AllModPatternsVerify</AssemblyName>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
    <NoWarn>CS0618</NoWarn>
  </PropertyGroup>
  <ItemGroup>
{compile_items}
  </ItemGroup>
  <ItemGroup>
{ref_items}
  </ItemGroup>
</Project>
"""
    proj_path = os.path.join(tmp_dir, "AllModPatternsVerify.csproj")
    with open(proj_path, "w", encoding="utf-8", newline="\n") as f:
        f.write(csproj)

    print(f"[1/2] 真实编译全部 {len(cs_files)} 个 C# 模板...")
    p1 = subprocess.run(["dotnet", "build", proj_path, "-c", "Release", "--nologo", "-v", "minimal", "--no-restore"],
                        capture_output=True, text=True, encoding="utf-8", errors="replace")
    if p1.returncode != 0:
        # 首次无 obj 需还原
        p1 = subprocess.run(["dotnet", "build", proj_path, "-c", "Release", "--nologo", "-v", "minimal"],
                            capture_output=True, text=True, encoding="utf-8", errors="replace")
    if p1.returncode != 0:
        print(p1.stdout + p1.stderr)
        sys.exit(2)
    print(f"[OK] {len(cs_files)} 个模板编译通过（0 错误）")

    clean_mod = os.path.join(tmp_dir, "clean-mod")
    os.makedirs(clean_mod, exist_ok=True)
    with open(os.path.join(clean_mod, "manifest.xml"), "w", encoding="utf-8", newline="\n") as f:
        f.write('<?xml version="1.0" encoding="utf-8"?>\n<ModManifest>\n  <ID>kb.template.verify</ID>\n  <Name>TemplateVerify</Name>\n  <Version>1.0.0</Version>\n  <Author>kb</Author>\n  <Capabilities />\n</ModManifest>\n')

    dll_path = os.path.join(tmp_dir, "bin", "Release", "net472", "AllModPatternsVerify.dll")
    scan_script = os.path.join(WORKSPACE, "工具库", "build", "Test-ArcadeLoaderScan.ps1")
    ps_exe = os.path.join(os.environ.get("WINDIR", r"C:\Windows"), "System32", "WindowsPowerShell", "v1.0", "powershell.exe")
    print("[2/2] 调用游戏真实 ModScanner 扫描零能力...")
    p2 = subprocess.run([ps_exe, "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", scan_script,
                         "-Candidate", dll_path, "-ContentDir", clean_mod],
                        capture_output=True, text=True, encoding="utf-8", errors="replace")
    out = (p2.stdout or "") + (p2.stderr or "")
    print(out.strip())
    refused_ok = bool(re.search(r"Refused:\s*False", out))
    cap_ok = bool(re.search(r"(?m)^Capabilities:\s*$", out))
    shutil.rmtree(tmp_dir, ignore_errors=True)
    if p2.returncode != 0 or not refused_ok or not cap_ok:
        print("[FAIL] 模板零能力扫描未通过！", file=sys.stderr)
        sys.exit(2)
    print(f"[PASS] 全部 {len(cs_files)} 个 C# 模板通过真实编译 + ModScanner 零能力扫描（Refused: False, Capabilities: 空）")


def main():
    ap = argparse.ArgumentParser(description="深空当铺知识库打标/查询/质检/模板实测流水线")
    sub = ap.add_subparsers(dest="cmd", required=True)

    pb = sub.add_parser("build", help="重新扫描全工作区并生成 CATALOG.json + CATALOG.md")
    pb.add_argument("--out-dir", default=None, help="可选输出目录（默认 共享开发/模组制作经验库）")

    pq = sub.add_parser("query", help="按标签/API/关键词秒查知识卡片")
    pq.add_argument("--domain", help="系统域（如 machines / events / appraisal / ui / persistence）")
    pq.add_argument("--layer", help="资产层（doc-guide / workshop-distill / template-cs / tool-script / skill）")
    pq.add_argument("--trust", help="可信度前缀逗号分隔（如 L1,L2）")
    pq.add_argument("--capability", help="权限档位（zero-cap / cap-files / cap-network / mixed / n/a）")
    pq.add_argument("--api", help="原版类名（如 StoreEventManager / GameItem / ModHelper）")
    pq.add_argument("--keyword", help="全文元信息关键词")
    pq.add_argument("--limit", type=int, default=15)
    pq.add_argument("--json", action="store_true")

    sub.add_parser("audit", help="执行全库 API 验真、双副本哈希与一致性门禁")

    pv = sub.add_parser("verify-templates", help="真实编译 13 个 C# 模板并用 ModScanner 验零能力")
    pv.add_argument("--managed", default=DEFAULT_MANAGED)

    args = ap.parse_args()
    if args.cmd == "build":
        build_catalog(args.out_dir)
    elif args.cmd == "query":
        cmd_query(args)
    elif args.cmd == "audit":
        cmd_audit(args)
    elif args.cmd == "verify-templates":
        cmd_verify_templates(args)


if __name__ == "__main__":
    main()
