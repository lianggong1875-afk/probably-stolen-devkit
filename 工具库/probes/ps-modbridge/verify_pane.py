# -*- coding: utf-8 -*-
"""进档后自动验「兼容鉴定」浮窗：造一件我方盒装盘 → 开原版放大镜 → 读界面上的浮窗文本与位置。

用法：python verify_pane.py [物品 id]
默认 gd_g_thatgame（正版「那个游戏 VI」）。
"""
import json
import os
import shutil
import sys
import time

from mcp_cli import mcp_tool, payload

DLL = r"D:\0深空当铺模组开发\缓存文件夹\probe-build\ArcInspectProbe2.dll"
MODULE_DIR = r"C:\Users\qianxia1\AppData\Local\ModDevBridgeNext\modules"
NAME = "ArcInspectProbe2.dll"


def q(name, args, timeout=300):
    return payload(mcp_tool(name, args, timeout=timeout))


def st_text(res):
    if isinstance(res, dict):
        t = res.get("text")
        if isinstance(t, str):
            return t
    return json.dumps(res, ensure_ascii=False)[:300]


def ui_snapshot():
    return q("query_ui", {"view": "overview", "max_nodes": 1500}).get("entries") or []


def main():
    item_id = sys.argv[1] if len(sys.argv) > 1 else "gd_g_thatgame"
    os.makedirs(MODULE_DIR, exist_ok=True)
    shutil.copy(DLL, os.path.join(MODULE_DIR, NAME))

    st = q("get_game_status", {})
    if not st.get("ready"):
        raise SystemExit("还没进游戏")
    sess = q("open_test_session", {})
    sid = sess.get("sessionId")
    wid = sess.get("worldId") or st.get("worldId")
    if not sid:
        st2 = q("get_game_status", {})
        sid = st2.get("sessionId")
        wid = st2.get("worldId") or wid
    if not sid:
        raise SystemExit("拿不到 sessionId")
    print("[0] session=%s world=%s" % (sid, wid))

    lp = q("load_probe_module", {"session_id": sid, "world_id": wid, "filename": NAME})
    print("[1] load:", st_text(lp)[:120])
    if lp.get("ok") is False:
        raise SystemExit("探针装载失败（一个会话只能装一个，先重启游戏）")

    def probe(cmd):
        return st_text(q("call_probe", {"session_id": sid, "world_id": wid, "command": cmd}))

    print("[2] inspect:", probe("inspect"))
    print("[3] spawn  :", probe("spawn " + item_id))
    found = probe("find gd_")
    print("[4] find   :", found[:200])
    uid = None
    for tok in found.replace("|", " ").split():
        if tok.isdigit():
            uid = tok
            break
    if not uid:
        raise SystemExit("没拿到 uniqueId")
    print("[5] open   :", probe("open " + uid))
    time.sleep(2.5)
    print("[6] state  :", probe("inspect"))

    entries = ui_snapshot()
    hits = []
    for e in entries:
        t = (e.get("text") or "")
        if any(k in t for k in ("兼容鉴定", "物品：", "成色：", "疑点：", "只读")):
            hits.append(e)
    print("[7] 浮窗条目 %d 个:" % len(hits))
    for e in hits:
        d = q("get_ui_details", {"world_id": wid, "ui_ref": e["uiRef"]})
        sb = (d.get("rect") or {}).get("screenBounds") or {}
        print("    %-40r  screen=(%s,%s) %sx%s" % (
            (e.get("text") or "")[:38], sb.get("x"), sb.get("y"), sb.get("width"), sb.get("height")))
    if not hits:
        print("    (浮窗没出现 —— 看 Player.log 里的 [Arcade] appraisal pane)")

    print("[8] close  :", probe("close"))


if __name__ == "__main__":
    main()
