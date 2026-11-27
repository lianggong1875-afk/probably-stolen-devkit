#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Steam 创意工坊抓取器（Probably Stolen / appid 4348910）

用途：在做"挑选值得反编译蒸馏的模组"这一步时，先拿到**名称 + 简介 + 订阅数/评分/文件体积**，
再决定订阅哪些。不做任何写入 Steam 状态的动作。

子命令：
  list    抓创意工坊列表页（多种排序 × 多页）→ 汇总 id → 调 Steam API 批量取详情
  detail  取单个/多个物品的详情并打印完整简介
  local   列出本机已下载（已订阅并落地）的工坊物品，并标注详情

输出：<工作区>/缓存文件夹/workshop-scan/workshop_items.json 与 items.md（人读摘要）

用法：
  python fetch_workshop.py list --pages 8
  python fetch_workshop.py detail 3828921912 3842430578
  python fetch_workshop.py local
"""

import argparse
import html
import json
import os
import re
import sys
import time
import urllib.error
import urllib.parse
import urllib.request

APPID = "4348910"
UA = ("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
      "(KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36")

HERE = os.path.dirname(os.path.abspath(__file__))
WORKSPACE = os.path.abspath(os.path.join(HERE, "..", ".."))
CACHE_DIR = os.path.join(WORKSPACE, "缓存文件夹", "workshop-scan")
STEAM_WORKSHOP_DIR = os.path.join("D:\\", "Steam", "steamapps", "workshop", "content", APPID)

SORTS = ["trend", "mostrecent", "toprated"]


def http(url, data=None, timeout=45, retries=3):
    last = None
    for i in range(retries):
        try:
            req = urllib.request.Request(url, data=data, headers={"User-Agent": UA})
            with urllib.request.urlopen(req, timeout=timeout) as r:
                return r.read()
        except Exception as e:  # 网络抖动重试；最终仍失败就抛出，不静默
            last = e
            time.sleep(1.5 * (i + 1))
    raise last


def list_page_ids(sort, page):
    url = ("https://steamcommunity.com/workshop/browse/?appid=%s"
           "&browsesort=%s&section=readytouseitems&actualsort=%s&p=%d"
           % (APPID, sort, sort, page))
    try:
        raw = http(url).decode("utf-8", "replace")
    except Exception as e:
        print("  [warn] 列表页失败 %s p%d: %s" % (sort, page, e), file=sys.stderr)
        return [], []
    ids = re.findall(r"sharedfiles/filedetails/\?id=(\d+)", raw)
    titles = re.findall(r'class="workshopItemTitle[^"]*"[^>]*>(.*?)</div>', raw, re.S)
    # 去重保序
    seen, out = set(), []
    for i in ids:
        if i not in seen:
            seen.add(i)
            out.append(i)
    clean_titles = [html.unescape(re.sub(r"<[^>]+>", "", t)).strip() for t in titles]
    return out, clean_titles


def api_details(ids):
    """Steam 公开接口，批量取标题/简介/订阅数等。无 key 也可用。"""
    out = {}
    ids = list(ids)
    for start in range(0, len(ids), 80):
        chunk = ids[start:start + 80]
        body = "itemcount=%d" % len(chunk)
        for n, pid in enumerate(chunk):
            body += "&publishedfileids%%5B%d%%5D=%s" % (n, pid)
        data = body.encode()
        raw = http("https://api.steampowered.com/ISteamRemoteStorage/GetPublishedFileDetails/v1/",
                   data=data)
        js = json.loads(raw.decode("utf-8", "replace"))
        for d in js.get("response", {}).get("publishedfiledetails", []):
            out[str(d.get("publishedfileid"))] = d
        time.sleep(0.4)
    return out


def local_ids():
    if not os.path.isdir(STEAM_WORKSHOP_DIR):
        return []
    return sorted(d for d in os.listdir(STEAM_WORKSHOP_DIR)
                  if os.path.isdir(os.path.join(STEAM_WORKSHOP_DIR, d)))


def fmt_row(d):
    pid = str(d.get("publishedfileid"))
    title = d.get("title") or "(无标题)"
    subs = d.get("subscriptions", d.get("lifetime_subscriptions", 0)) or 0
    fup = d.get("favorited", 0) or 0
    views = d.get("views", 0) or 0
    size = int(d.get("file_size", 0) or 0)
    upd = time.strftime("%Y-%m-%d", time.localtime(int(d.get("time_updated", 0) or 0)))
    return "| %s | %s | %s | %s | %s | %s KB | %s |" % (
        pid, title.replace("|", "/"), subs, fup, views, size // 1024, upd)


def save(items):
    os.makedirs(CACHE_DIR, exist_ok=True)
    js_path = os.path.join(CACHE_DIR, "workshop_items.json")
    old = {}
    if os.path.isfile(js_path):
        try:
            old = json.load(open(js_path, encoding="utf-8"))
        except Exception:
            old = {}
    old.update(items)
    with open(js_path, "w", encoding="utf-8", newline="\n") as f:
        json.dump(old, f, ensure_ascii=False, indent=1)

    rows = sorted(old.values(), key=lambda d: -(int(d.get("subscriptions", 0) or 0)))
    local = set(local_ids())
    md = ["# Probably Stolen 创意工坊扫描结果", "",
          "自动生成于 %s；本地已下载 %d 项。" % (time.strftime("%Y-%m-%d %H:%M"), len(local)), "",
          "| id | 名称 | 订阅 | 收藏 | 浏览 | 体积 | 更新 | 本地 |",
          "| --- | --- | --- | --- | --- | --- | --- | --- |"]
    for d in rows:
        md.append(fmt_row(d) + (" 已下载 |" if str(d.get("publishedfileid")) in local else " |"))
    md_path = os.path.join(CACHE_DIR, "items.md")
    with open(md_path, "w", encoding="utf-8", newline="\n") as f:
        f.write("\n".join(md) + "\n")
    return js_path, md_path


def cmd_list(args):
    collected = {}
    for sort in SORTS:
        prev = None
        for page in range(1, args.pages + 1):
            ids, titles = list_page_ids(sort, page)
            if not ids:
                break
            new = [i for i in ids if i not in collected]
            for i in ids:
                collected.setdefault(i, None)
            print("  %-10s p%-2d 抓到 %2d 项（新 %2d）" % (sort, page, len(ids), len(new)))
            if prev == ids:
                break
            prev = ids
            time.sleep(0.6)
    print("列表合计 %d 项，抓详情…" % len(collected))
    details = api_details(collected.keys())
    missing = [i for i in collected if i not in details]
    print("详情 %d 条；缺失 %d 条" % (len(details), len(missing)))
    js_path, md_path = save(details)
    print("已写入：%s\n        %s" % (js_path, md_path))
    local = set(local_ids())
    print("\n=== 本地已下载、但本次列表未覆盖的 id（可能已下架/未出现在排序页） ===")
    print("、".join(sorted(local - set(details))) or "(无)")
    print("\n=== 详情缺失的 id ===")
    print("、".join(missing) or "(无)")


def print_detail(d):
    print("=" * 78)
    print("id      : %s" % d.get("publishedfileid"))
    print("名称    : %s" % (d.get("title") or "(无)"))
    print("订阅/收藏/浏览: %s / %s / %s" % (d.get("subscriptions", 0), d.get("favorited", 0), d.get("views", 0)))
    print("体积    : %s KB" % (int(d.get("file_size", 0) or 0) // 1024))
    print("更新    : %s" % time.strftime("%Y-%m-%d", time.localtime(int(d.get("time_updated", 0) or 0))))
    tags = d.get("tags") or []
    if tags:
        print("标签    : %s" % "、".join(t.get("tag", "") for t in tags))
    print("-" * 78)
    print(d.get("description") or "(无简介)")
    print()


def cmd_detail(args):
    details = api_details(args.ids)
    for pid in args.ids:
        d = details.get(str(pid))
        if not d:
            print("!! %s 查询失败" % pid)
            continue
        print_detail(d)
    save(details)


def cmd_brief(args):
    """紧凑输出：id | 名称 | 订阅 | 体积 | 简介前 N 字（用于大批量快速筛选）"""
    ids = args.ids
    if args.ids_file:
        with open(args.ids_file, encoding="utf-8") as f:
            for line in f:
                line = line.split("#")[0].strip()
                if line:
                    ids.extend(line.replace(",", " ").split())
    ids = [i for i in dict.fromkeys(ids) if i.isdigit()]
    details = api_details(ids)
    lines = []
    for pid in ids:
        d = details.get(pid)
        if not d:
            lines.append("## %s 查询失败" % pid)
            continue
        desc = (d.get("description") or "").strip()
        desc = re.sub(r"\[/?[^\]]{1,20}\]", "", desc)   # 去 BBCode
        desc = re.sub(r"https?://\S+", "", desc)
        desc = re.sub(r"\s+", " ", desc).strip()
        lines.append("## %s | %s | 订阅 %s | 收藏 %s | %s KB | 更新 %s" % (
            pid, d.get("title") or "(无)", d.get("subscriptions", 0), d.get("favorited", 0),
            int(d.get("file_size", 0) or 0) // 1024,
            time.strftime("%Y-%m-%d", time.localtime(int(d.get("time_updated", 0) or 0)))))
        lines.append(desc[:args.chars] + ("…" if len(desc) > args.chars else ""))
        lines.append("")
    text = "\n".join(lines)
    out = args.out or os.path.join(CACHE_DIR, "brief.md")
    os.makedirs(os.path.dirname(out) or ".", exist_ok=True)
    with open(out, "w", encoding="utf-8", newline="\n") as f:
        f.write(text + "\n")
    print("已写入 %s（%d 项）" % (out, len(ids)))
    save(details)


def cmd_local(args):
    ids = local_ids()
    print("本地已下载 %d 项，抓详情…" % len(ids))
    details = api_details(ids)
    # 本地体积
    sizes = {}
    for pid in ids:
        total = 0
        for root, _, files in os.walk(os.path.join(STEAM_WORKSHOP_DIR, pid)):
            for fn in files:
                try:
                    total += os.path.getsize(os.path.join(root, fn))
                except OSError:
                    pass
        sizes[pid] = total
    rows = []
    for pid in ids:
        d = details.get(pid, {"publishedfileid": pid, "title": "(查询失败)"})
        rows.append((int(d.get("subscriptions", 0) or 0), pid, d))
    rows.sort(reverse=True)
    for subs, pid, d in rows:
        print("| %s | %s | 订阅 %s | 收藏 %s | 更新 %s | 本地 %s KB |"
              % (pid, d.get("title", "?"), subs, d.get("favorited", 0),
                 time.strftime("%Y-%m-%d", time.localtime(int(d.get("time_updated", 0) or 0))),
                 sizes.get(pid, 0) // 1024))
    save(details)


def main():
    ap = argparse.ArgumentParser()
    sub = ap.add_subparsers(dest="cmd", required=True)
    p = sub.add_parser("list")
    p.add_argument("--pages", type=int, default=8)
    p.set_defaults(fn=cmd_list)
    p = sub.add_parser("detail")
    p.add_argument("ids", nargs="+")
    p.set_defaults(fn=cmd_detail)
    p = sub.add_parser("local")
    p.set_defaults(fn=cmd_local)
    p = sub.add_parser("brief")
    p.add_argument("ids", nargs="*")
    p.add_argument("--ids-file")
    p.add_argument("--chars", type=int, default=700)
    p.add_argument("--out")
    p.set_defaults(fn=cmd_brief)
    a = ap.parse_args()
    a.fn(a)


if __name__ == "__main__":
    main()
