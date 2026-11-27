#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""创意工坊蒸馏队列（长期任务的状态机与选批器）。

职责只有四件，别的都不管（订阅/反编译/写档案由 SOP 驱动）：
  1. 台账：`共享开发/模组制作经验库/蒸馏台账.json` —— 唯一事实源，记谁蒸馏过、在第几批、档案名。
  2. 选批：从未蒸馏池里按「订阅量 × 体积 × 扩展关键词」打分，排除已蒸馏与纯美术重绘包。
  3. 记账：一批做完后 mark 写入台账，同时把「为蒸馏而订阅、待退订」的 id 记进 pending_unsub。
  4. 复核：status 打印进度，pend 打印待退订清单。

用法：
  python distill_queue.py init                 # 首次：从现有档案与 19 索引反推台账（幂等）
  python distill_queue.py refresh              # 刷新工坊列表（3 种排序 × 12 页）
  python distill_queue.py next --n 10          # 打印下一批候选，落 queue-batchN.json/md
  python distill_queue.py next --n 10 --min-subs 20000 --only-kw Expansion,Overhaul
  python distill_queue.py mark --batch 8 --ids 3829730904,3832840208 --archive-map 3829730904=BrewingExpansion
  python distill_queue.py mark --batch 8 --ids ... --no-unsub     # 用户自己订阅的，别记退订
  python distill_queue.py pend                 # 待退订清单
  python distill_queue.py unsubbed 3829730904  # 退订完成后销账
  python distill_queue.py status
"""

import argparse
import json
import math
import os
import re
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
WORKSPACE = os.path.abspath(os.path.join(HERE, "..", ".."))
sys.path.insert(0, HERE)

import fetch_workshop as fw  # noqa: E402  同目录，复用抓取/接口/落盘

LEDGER = os.path.join(WORKSPACE, "共享开发", "模组制作经验库", "蒸馏台账.json")
DISTILL_DIR = os.path.join(WORKSPACE, "共享开发", "模组制作经验库", "workshop-distill")
INDEX_MD = os.path.join(WORKSPACE, "共享开发", "模组制作经验库", "19-工坊模组逆向与蒸馏.md")
SCAN_JSON = os.path.join(WORKSPACE, "缓存文件夹", "workshop-scan", "workshop_items.json")
QUEUE_DIR = os.path.join(WORKSPACE, "缓存文件夹", "workshop-scan")

# 内容/系统扩展的强信号：出现即加分（说明这是"加东西"而不是"改外观"）
KW_STRONG = ("expansion", "overhaul", "framework", "extended", "扩展", "拓展", "dlc",
             "suite", "legacy", "rework", "core", "industry", "industry")
# 玩法系统类信号
KW_SYSTEM = ("automation", "automat", "自动化", "自动", "machine", "机器", "factory", "工厂",
             "production", "生产", "pipeline", "管线", "management", "经营", "career", "职业",
             "economy", "经济", "stock", "股票", "reputation", "声望", "skill", "perk", "特性")
# 纯美术重绘包信号：按用户要求跳过（只记美术需求，不蒸馏重画的东西）
KW_ART_ONLY = ("娘化", "feminized", "reskin", "redraw", "重绘", "立绘包", "portrait pack",
               "hd original", "texture pack", "贴图包", "hand-drawn", "手绘")


def load_ledger():
    if os.path.isfile(LEDGER):
        with open(LEDGER, encoding="utf-8") as f:
            return json.load(f)
    return {"version": 1, "updated": "", "items": {}, "pending_unsub": [], "skip": {},
            "batches": {}}


def save_ledger(led):
    led["updated"] = time.strftime("%Y-%m-%d %H:%M")
    os.makedirs(os.path.dirname(LEDGER), exist_ok=True)
    with open(LEDGER, "w", encoding="utf-8", newline="\n") as f:
        json.dump(led, f, ensure_ascii=False, indent=1, sort_keys=True)
    return LEDGER


def load_items():
    if not os.path.isfile(SCAN_JSON):
        return {}
    with open(SCAN_JSON, encoding="utf-8") as f:
        return json.load(f)


def subs_of(d):
    return int(d.get("lifetime_subscriptions") or d.get("subscriptions") or 0)


def size_mb(d):
    return int(d.get("file_size") or 0) / 1048576.0


def cmd_init(args):
    """从现有档案文件名 + 19 索引表格反推「已蒸馏」，只补不覆盖。"""
    led = load_ledger()
    items = led.setdefault("items", {})
    added = 0
    removed = 0
    for f in sorted(os.listdir(DISTILL_DIR)):
        if not f.endswith(".md"):
            continue
        # 文件名里的 id
        name_ids = set(re.findall(r"\b(3\d{9})\b", f))
        # 正文头部的 id：合并档（如「生产链与自动化四例」）文件名里没有 id，靠正文识别。
        # 规则要防两类误判（实测各踩一次）：
        #   ① 正文里引用了别的模组（"该模组依赖主框架（工坊 id xxx，本批未下载）"）
        #   ② 存档 JSON 示例里的 id（workshopOwners 的值）
        try:
            text = open(os.path.join(DISTILL_DIR, f), encoding="utf-8").read()
        except Exception:
            text = ""
        head = "\n".join(text.splitlines()[:60])
        body_ids = set()
        for line in head.splitlines():
            if any(k in line for k in ("workshopOwners", '":', "未下载", "未收录")):
                continue                      # 存档/JSON 示例或"未取得"的引用
            for pid in re.findall(r"\b(3\d{9})\b", line):
                # 真蒸馏对象在整篇里会反复出现；偶发引用通常只出现一两次
                if len(re.findall(r"\b%s\b" % pid, text)) >= 3:
                    body_ids.add(pid)
        for pid in sorted(name_ids | body_ids):
            src = "archive-name" if pid in name_ids else "archive-head"
            if pid not in items:
                items[pid] = {"archive": f, "batch": None, "done": None, "source": src}
                added += 1
            items[pid].setdefault("source", src)
            # 补记归属档案：archive-head 条目只补空的，别覆盖已有的
            if not items[pid].get("archive"):
                items[pid]["archive"] = f
        # 复评：旧版规则抓错的 archive-head 条目，现在不满足就撤掉（禁止误判永久生效）
        for pid in list(items):
            it = items[pid]
            if it.get("source") == "archive-head" and it.get("archive") == f and pid not in (name_ids | body_ids):
                items.pop(pid)
                removed += 1
    if os.path.isfile(INDEX_MD):
        text = open(INDEX_MD, encoding="utf-8").read()
        cur_batch = None
        for line in text.splitlines():
            m = re.search(r"第[一二三四五六七八九十]+批（batch-(\d+)）", line)
            if m:
                cur_batch = int(m.group(1))
            if line.startswith("|"):
                for pid in re.findall(r"\b(3\d{9})\b", line):
                    if pid not in items:
                        items[pid] = {"archive": "", "batch": cur_batch,
                                      "done": None, "source": "index-table"}
                        added += 1
                    elif items[pid].get("batch") is None and cur_batch:
                        items[pid]["batch"] = cur_batch
    led["skip"] = led.get("skip") or {}
    path = save_ledger(led)
    print("[init] 台账 %s：已蒸馏 %d 项（本次新增 %d，撤掉误判 %d）"
          % (path, len(items), added, removed))
    return 0


def cmd_refresh(args):
    collected = {}
    for sort in fw.SORTS:
        for page in range(1, args.pages + 1):
            ids, _ = fw.list_page_ids(sort, page)
            for i in ids:
                collected.setdefault(i, None)
            print("  %-10s p%-2d 新 %d / 合计 %d" % (sort, page, len(ids), len(collected)))
            if not ids:
                break
    details = fw.api_details(list(collected))
    js, md = fw.save(details)
    print("[refresh] %d 项 → %s" % (len(details), js))
    return 0


def score(d, led):
    pid = str(d.get("publishedfileid"))
    subs = subs_of(d)
    mb = size_mb(d)
    title = (d.get("title") or "").lower()
    desc = (d.get("description") or "").lower()
    blob = title + " " + desc[:400]

    s = math.log10(max(subs, 1) + 10) * 10.0          # 46 万订阅 ≈ 56.6；3 千 ≈ 34.8
    if mb >= 20:
        s += 9
    elif mb >= 5:
        s += 6
    elif mb >= 1:
        s += 3
    elif mb < 0.05:
        s -= 3                                       # 十几 KB 的多半是单点小补丁
    for kw in KW_STRONG:
        if kw in blob:
            s += 6
            break
    for kw in KW_SYSTEM:
        if kw in blob:
            s += 3
            break
    for kw in KW_ART_ONLY:
        if kw in blob:
            s -= 40                                     # 纯重绘包：直接沉底
            break
    return s


def cmd_next(args):
    led = load_ledger()
    items = load_items()
    if not items:
        print("[next] 没有工坊列表快照，先跑 refresh", file=sys.stderr)
        return 2
    done = set(led.get("items", {}))
    skip = led.get("skip", {})
    local = set(fw.local_ids())
    subs_names = set()
    for i in (args.exclude or []):
        subs_names.add(i)

    rows = []
    for pid, d in items.items():
        if pid in done or pid in skip:
            continue
        if d.get("banned"):
            continue
        if args.min_subs and subs_of(d) < args.min_subs:
            continue
        blob = ((d.get("title") or "") + " " + (d.get("description") or "")[:400]).lower()
        if args.only_kw and not any(k.lower() in blob for k in args.only_kw.split(",")):
            continue
        rows.append({
            "id": pid,
            "title": d.get("title") or "",
            "subs": subs_of(d),
            "mb": round(size_mb(d), 1),
            "updated": time.strftime("%Y-%m-%d", time.localtime(int(d.get("time_updated") or 0))),
            "local": pid in local,
            "score": round(score(d, led), 1),
            "desc": re.sub(r"\s+", " ", re.sub(r"\[[^\]]*\]", "", d.get("description") or ""))[:260],
        })
    rows.sort(key=lambda r: -r["score"])
    top = rows[:args.n]

    batch_no = args.batch
    if batch_no is None:
        nums = [int(k) for k in led.get("batches", {}) if str(k).isdigit()]
        batch_no = (max(nums) + 1) if nums else 1

    print("未蒸馏池 %d 项（已蒸馏 %d / 跳过 %d），取前 %d 项作为 batch-%d"
          % (len(rows), len(done), len(skip), len(top), batch_no))
    print()
    print("| # | 分数 | 订阅 | 体积 | id | 名称 | 已下载 |")
    print("| --- | --- | --- | --- | --- | --- | --- |")
    for n, r in enumerate(top, 1):
        print("| %d | %s | %d | %s MB | %s | %s | %s |"
              % (n, r["score"], r["subs"], r["mb"], r["id"], r["title"].replace("|", "/"),
                 "是" if r["local"] else ""))
    print()
    for n, r in enumerate(top, 1):
        print("%2d. %s (%s) —— %s" % (n, r["title"], r["id"], r["desc"][:200]))

    os.makedirs(QUEUE_DIR, exist_ok=True)
    out = os.path.join(QUEUE_DIR, "queue-batch%d.json" % batch_no)
    with open(out, "w", encoding="utf-8", newline="\n") as f:
        json.dump({"batch": batch_no, "created": time.strftime("%Y-%m-%d %H:%M"),
                   "candidates": top}, f, ensure_ascii=False, indent=1)
    print("\n[next] 候选清单 → %s" % out)
    return 0


def cmd_mark(args):
    led = load_ledger()
    items = led.setdefault("items", {})
    batch = args.batch
    ids = [i.strip() for i in args.ids.split(",") if i.strip()]
    pool = load_items()
    amap = {}
    for pair in (args.archive_map or "").split(","):
        if "=" in pair:
            k, v = pair.split("=", 1)
            amap[k.strip()] = v.strip()
    for pid in ids:
        d = pool.get(pid, {})
        items[pid] = {
            "title": d.get("title") or items.get(pid, {}).get("title", ""),
            "subs": subs_of(d) if d else items.get(pid, {}).get("subs", 0),
            "mb": round(size_mb(d), 1) if d else items.get(pid, {}).get("mb", 0),
            "batch": batch,
            "done": time.strftime("%Y-%m-%d"),
            "archive": amap.get(pid, items.get(pid, {}).get("archive", "")),
            "source": "distill",
        }
    led.setdefault("batches", {})[str(batch)] = sorted(ids)
    if not args.no_unsub:
        pend = set(led.get("pending_unsub", []))
        pend.update(ids)
        led["pending_unsub"] = sorted(pend)
    path = save_ledger(led)
    print("[mark] batch-%s 记入 %d 项 → %s" % (batch, len(ids), path))
    if not args.no_unsub:
        print("[mark] 待退订 %d 项：%s" % (len(led["pending_unsub"]), ",".join(led["pending_unsub"])))
    return 0


def cmd_pend(args):
    led = load_ledger()
    pend = led.get("pending_unsub", [])
    pool = load_items()
    if not pend:
        print("[pend] 没有待退订项")
        return 0
    print("[pend] 待退订 %d 项（这些是为蒸馏而订阅的，蒸馏完必须退）：" % len(pend))
    for pid in pend:
        t = pool.get(pid, {}).get("title", "")
        print("  %s  %s" % (pid, t))
    print("\n批量退订：\n  WorkshopUploader.exe --unsub %s\n  然后：python distill_queue.py unsubbed %s"
          % (",".join(pend), ",".join(pend)))
    return 0


def cmd_unsubbed(args):
    led = load_ledger()
    ids = set()
    for a in args.ids:
        ids.update(x.strip() for x in a.split(",") if x.strip())
    before = len(led.get("pending_unsub", []))
    led["pending_unsub"] = sorted(i for i in led.get("pending_unsub", []) if i not in ids)
    save_ledger(led)
    print("[unsubbed] 销账 %d 项，剩 %d 项待退订" % (before - len(led["pending_unsub"]),
                                              len(led["pending_unsub"])))
    return 0


def cmd_status(args):
    led = load_ledger()
    items = led.get("items", {})
    pool = load_items()
    done = set(items)
    remaining = [p for p in pool if p not in done and p not in led.get("skip", {})]
    high = [p for p in remaining if subs_of(pool[p]) >= 20000]
    big = [p for p in remaining if size_mb(pool[p]) >= 5]
    print("台账：已蒸馏 %d 项｜工坊池 %d 项｜未蒸馏 %d 项（订阅≥2万：%d，体积≥5MB：%d）"
          % (len(done), len(pool), len(remaining), len(high), len(big)))
    if led.get("batches"):
        print("批次：" + "，".join("batch-%s=%d 项" % (k, len(v))
                                   for k, v in sorted(led["batches"].items(), key=lambda x: int(x[0]))))
    if led.get("pending_unsub"):
        print("⚠ 待退订 %d 项：%s" % (len(led["pending_unsub"]), ",".join(led["pending_unsub"])))
    if args.top:
        rows = sorted(remaining, key=lambda p: -subs_of(pool[p]))[:args.top]
        print("\n未蒸馏里订阅最高的 %d 项：" % len(rows))
        for p in rows:
            print("  %7d | %6.1f MB | %s | %s" % (subs_of(pool[p]), size_mb(pool[p]), p,
                                                  (pool[p].get("title") or "")[:60]))
    return 0


def main():
    ap = argparse.ArgumentParser(description="创意工坊蒸馏队列")
    sub = ap.add_subparsers(dest="cmd")

    sub.add_parser("init")

    p = sub.add_parser("refresh")
    p.add_argument("--pages", type=int, default=12)

    p = sub.add_parser("next")
    p.add_argument("--n", type=int, default=10)
    p.add_argument("--min-subs", type=int, default=0)
    p.add_argument("--only-kw", default="")
    p.add_argument("--batch", type=int, default=None)
    p.add_argument("--exclude", nargs="*", default=[])

    p = sub.add_parser("mark")
    p.add_argument("--batch", type=int, required=True)
    p.add_argument("--ids", required=True)
    p.add_argument("--archive-map", default="")
    p.add_argument("--no-unsub", action="store_true")

    p = sub.add_parser("pend")

    p = sub.add_parser("unsubbed")
    p.add_argument("ids", nargs="+")

    p = sub.add_parser("status")
    p.add_argument("--top", type=int, default=0)

    args = ap.parse_args()
    fn = {"init": cmd_init, "refresh": cmd_refresh, "next": cmd_next, "mark": cmd_mark,
          "pend": cmd_pend, "unsubbed": cmd_unsubbed, "status": cmd_status}.get(args.cmd)
    if not fn:
        ap.print_help()
        return 1
    return fn(args)


if __name__ == "__main__":
    sys.exit(main())
