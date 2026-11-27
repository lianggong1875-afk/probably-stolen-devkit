# -*- coding: utf-8 -*-
"""装 ArcInspectProbe 探针，在我方物品上打开原版放大镜，然后只读地看「兼容鉴定」窗口。

用法：python probe_inspect.py [前缀]
默认前缀 gd_（盒装光盘）。显卡/内存用 hw_。
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


def ensure():
    os.makedirs(MODULE_DIR, exist_ok=True)
    shutil.copy(DLL, os.path.join(MODULE_DIR, NAME))
    st = q("get_game_status", {})
    if not st.get("ready"):
        raise SystemExit("还没进游戏（ready=false），先载入存档")
    sess = q("open_test_session", {})
    sid = sess.get("sessionId")
    wid = sess.get("worldId") or st.get("worldId")
    if not sid:
        # 已经有 session 在跑时 open 会失败；这时用状态里的那个
        st2 = q("get_game_status", {})
        sid = st2.get("sessionId") or st2.get("activeSessionId")
        wid = st2.get("worldId") or wid
        if not sid:
            raise SystemExit("拿不到 sessionId: %s" % json.dumps(sess, ensure_ascii=False)[:400])
    lp = q("load_probe_module", {"session_id": sid, "world_id": wid, "filename": NAME})
    print("[probe] load %s" % json.dumps(lp, ensure_ascii=False)[:160])
    return sid, wid


def probe(sid, wid, cmd):
    return q("call_probe", {"session_id": sid, "world_id": wid, "command": cmd})


def pane_lines(wid):
    """把界面里跟浮窗有关的文本抓出来。"""
    r = q("query_ui", {"view": "overview", "max_nodes": 1000})
    out = []
    for e in r.get("entries") or []:
        t = e.get("text") or ""
        if not t:
            continue
        if "兼容鉴定" in t or "物品：" in t or "成色：" in t or "疑点：" in t or "只读" in t:
            out.append(t)
    return out


def main():
    prefix = sys.argv[1] if len(sys.argv) > 1 else "gd_"
    sid, wid = ensure()
    print("[probe] inspect ->", json.dumps(probe(sid, wid, "inspect"), ensure_ascii=False)[:160])
    found = probe(sid, wid, "find " + prefix)
    print("[probe] find  ->", json.dumps(found, ensure_ascii=False)[:400])
    text = found.get("result") if isinstance(found, dict) else None
    if not isinstance(text, str):
        text = json.dumps(found, ensure_ascii=False)
    uid = None
    for tok in str(text).replace("|", " ").split():
        if tok.isdigit():
            uid = tok
            break
    if not uid:
        raise SystemExit("没找到可用的物品（前缀 %s）" % prefix)
    print("[probe] open  ->", json.dumps(probe(sid, wid, "open " + uid), ensure_ascii=False)[:200])
    time.sleep(2.0)
    print("[probe] state ->", json.dumps(probe(sid, wid, "inspect"), ensure_ascii=False)[:160])
    lines = pane_lines(wid)
    print("[pane] %d 行:" % len(lines))
    for l in lines:
        print("   ", l.replace("\n", " / "))
    if not lines:
        print("    (浮窗没有出现)")
    print("[probe] close ->", json.dumps(probe(sid, wid, "close"), ensure_ascii=False)[:120])


if __name__ == "__main__":
    main()
