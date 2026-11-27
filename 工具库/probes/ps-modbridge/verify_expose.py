# -*- coding: utf-8 -*-
"""验「客户当场识破假货」这条路：台词 + 暴露后的价格变化。

用法：python verify_expose.py
"""
import json
import os
import shutil

from mcp_cli import mcp_tool, payload

DLL = r"D:\0深空当铺模组开发\缓存文件夹\probe-build\ArcInspectProbe3.dll"
MODULE_DIR = r"C:\Users\qianxia1\AppData\Local\ModDevBridgeNext\modules"
NAME = "ArcInspectProbe3.dll"


def q(name, args, timeout=300):
    return payload(mcp_tool(name, args, timeout=timeout))


def text(res):
    if isinstance(res, dict) and isinstance(res.get("text"), str):
        return res["text"]
    return json.dumps(res, ensure_ascii=False)[:300]


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
    print("[load]", text(q("load_probe_module", {"session_id": sid, "world_id": wid, "filename": NAME}))[:120])

    def probe(cmd):
        return text(q("call_probe", {"session_id": sid, "world_id": wid, "command": cmd}))

    for item in ("gd_p_thatgame", "gd_g_thatgame", "hw_gpu_mk2_refurb"):
        print("\n=== %s ===" % item)
        print(probe("testexpose " + item))


if __name__ == "__main__":
    main()
