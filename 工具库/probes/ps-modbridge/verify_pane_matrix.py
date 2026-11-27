# -*- coding: utf-8 -*-
"""一次验多件：正版盘 / 盗版盘各造一件，分别开放大镜，把「兼容鉴定」浮窗的行全打出来。

用法：python verify_pane_matrix.py
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
ITEMS = sys.argv[1:] or ["gd_g_thatgame", "gd_p_thatgame"]


def q(name, args, timeout=300):
    return payload(mcp_tool(name, args, timeout=timeout))


def st_text(res):
    if isinstance(res, dict) and isinstance(res.get("text"), str):
        return res["text"]
    return json.dumps(res, ensure_ascii=False)[:300]


def window_lines():
    r = q("query_ui", {"view": "overview", "max_nodes": 1500})
    out = []
    for e in r.get("entries") or []:
        t = e.get("text") or ""
        if any(k in t for k in ("兼容鉴定", "物品：", "检测：", "标签：", "成色：", "疑点：", "只读")):
            out.append(e)
    return out


def main():
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
        sid, wid = st2.get("sessionId"), st2.get("worldId") or wid
    lp = q("load_probe_module", {"session_id": sid, "world_id": wid, "filename": NAME})
    print("[load]", st_text(lp)[:100])

    def probe(cmd):
        return st_text(q("call_probe", {"session_id": sid, "world_id": wid, "command": cmd}))

    for item in ITEMS:
        print("\n=== %s ===" % item)
        print(" spawn:", probe("spawn " + item))
        prefix = item.split("_")[0] + "_"
        found = probe("find " + prefix)
        pairs = {}
        toks = [t for t in found.replace("|", " ").split() if t]
        for i in range(0, len(toks) - 1, 2):
            if toks[i].isdigit():
                pairs[toks[i + 1]] = toks[i]
        uid = pairs.get(item)
        if not uid:
            print(" 没找到刚造的物品，find=", found[:160])
            continue
        print(" open :", probe("open " + uid))
        time.sleep(2.0)
        hits = window_lines()
        if not hits:
            print(" 浮窗没出现")
        for e in hits:
            print("   %r" % (e.get("text") or ""))
        probe("close")
        time.sleep(0.5)


if __name__ == "__main__":
    main()
