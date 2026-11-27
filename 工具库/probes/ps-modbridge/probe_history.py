# -*- coding: utf-8 -*-
"""展开每张存档卡，把它的历史备份行（天数/时间）连同 Plate 索引打出来。

只展开、只读，不点任何 Load / LoadLatest。

用法：python probe_history.py
"""
import json
import time

from mcp_cli import mcp_tool, payload


def q(name, args, timeout=300):
    return payload(mcp_tool(name, args, timeout=timeout))


def click(wid, ui_ref):
    d = q("get_ui_details", {"world_id": wid, "ui_ref": ui_ref})
    ref = (d.get("observation") or {}).get("actionRef")
    if not ref:
        return {"_error": "no actionRef"}
    return q("operate_ui", {"world_id": wid, "action": "click", "target_ref": ref})


def snap():
    r = q("query_ui", {"view": "overview", "limit": 200})
    return r.get("worldId"), (r.get("entries") or [])


def in_load_menu(entries):
    return any((e.get("text") or "").startswith("第") and "信用点" in (e.get("text") or "") for e in entries)


wid, entries = snap()
if not in_load_menu(entries):
    btn = next((e for e in entries if (e.get("text") or "") == "载入游戏"), None)
    if not btn:
        raise SystemExit("既不在主菜单也不在载入界面")
    print("[0] 点「载入游戏」")
    click(wid, btn["uiRef"])
    time.sleep(2.5)
    wid, entries = snap()

bodies = [e for e in entries if e.get("role") == "PointerClick" and (e.get("text") or "").startswith("第")]
print("[1] 找到 %d 张卡：" % len(bodies))
for e in bodies:
    print("    %r" % (e.get("text") or "")[:60])

for idx, carrier in enumerate(bodies):
    print("\n--- 展开第 %d 张卡 ---" % idx)
    click(wid, carrier["uiRef"])
    time.sleep(1.2)
    wid, entries = snap()
    plates = {}
    for e in entries:
        if e.get("name") not in ("Info", "Load", "Date"):
            continue
        d = q("get_ui_details", {"world_id": wid, "ui_ref": e["uiRef"]})
        path = d.get("path") or ""
        if "/Expanded" not in path:
            continue
        key = path.split("/Expanded")[0].split("/")[-1]
        plates.setdefault(key, []).append((e.get("name"), (e.get("text") or "")[:40], path.split("/")[-1]))
    for key, rows in plates.items():
        print("  %s:" % key)
        for name, text, node in rows:
            print("    %-5s %r  (%s)" % (name, text, node))
    # 收起，避免两张卡同时展开
    click(wid, carrier["uiRef"])
    time.sleep(0.8)
