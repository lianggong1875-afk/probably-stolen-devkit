# -*- coding: utf-8 -*-
"""star.py - 斯塔真机验证的调用助手：复用已开的测试会话，向游戏内探针发命令。"""
import json
import sys

sys.path.insert(0, r"D:\0深空当铺模组开发\工具库\probes\ps-modbridge")
import mcp_cli  # noqa: E402

SESSION_FILE = r"D:\0深空当铺模组开发\工具库\probes\ps-modbridge\tmp\star_session.json"


def call(name, args, timeout=300):
    r = mcp_cli.mcp_tool(name, args, timeout=timeout)
    s = r.get("_saved")
    if s is None:
        return {"_err": ((r.get("_stderr") or "") + (r.get("_stdout") or ""))[:400]}
    return s


def result(name, args, timeout=300):
    return call(name, args, timeout).get("result", {})


# 上一次 open_test_session 拿到的会话（单资源会话，重复 open 会失败）
KNOWN_SESSIONS = ["96d9225b707e4cc39353766fc400daec"]


def session():
    st = result("get_game_status", {})
    wid = st.get("worldId")
    try:
        with open(SESSION_FILE, "r", encoding="utf-8") as f:
            d = json.load(f)
        if d.get("worldId") == wid:
            return d["sessionId"], wid
    except Exception:
        pass
    raw = call("open_test_session", {})
    r = raw.get("result") or {}
    sid = r.get("sessionId") or raw.get("sessionId")
    if not sid:
        for cand in KNOWN_SESSIONS:
            sid = cand
            break
    d = {"sessionId": sid, "worldId": wid}
    import os
    os.makedirs(os.path.dirname(SESSION_FILE), exist_ok=True)
    with open(SESSION_FILE, "w", encoding="utf-8") as f:
        json.dump(d, f)
    return d["sessionId"], d["worldId"]


def probe(cmd, timeout=300):
    sid, wid = session()
    r = result("call_probe", {"command": cmd, "session_id": sid, "world_id": wid}, timeout)
    return r


def load_module(filename):
    sid, wid = session()
    return result("load_probe_module", {"filename": filename, "session_id": sid, "world_id": wid})


if __name__ == "__main__":
    cmd = sys.argv[1] if len(sys.argv) > 1 else "state"
    if cmd == "load":
        print(json.dumps(load_module(sys.argv[2]), ensure_ascii=False)[:600])
    else:
        print(json.dumps(probe(" ".join(sys.argv[1:])), ensure_ascii=False)[:2000])
