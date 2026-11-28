# -*- coding: utf-8 -*-
"""bs.py - 本轮的桥接小助手：query → 立刻 click（同一进程内用 target_ref，绝不凭坐标点）。"""
import json
import sys
import time

sys.path.insert(0, r"D:\0深空当铺模组开发\工具库\probes\ps-modbridge")
import mcp_cli  # noqa: E402


def tool(name, args, timeout=300):
    r = mcp_cli.mcp_tool(name, args, timeout=timeout)
    saved = r.get("_saved")
    if saved is None:
        out = (r.get("_stdout") or "").strip()
        err = (r.get("_stderr") or "").strip()
        raise RuntimeError("no saved payload for %s; rc=%s out=%s err=%s"
                           % (name, r.get("_rc"), out[:400], err[:400]))
    return saved


def result_of(saved):
    return saved.get("result", saved) if isinstance(saved, dict) else saved


def query(args):
    return result_of(tool("query_ui", args))


def find_button(res, text):
    for e in res.get("entries", []):
        if e.get("role") == "Button" and text in (e.get("text") or ""):
            return e
    return None


def click_text(text, extra=None, verbose=True):
    args = {"text_contains": text, "max_nodes": 120}
    if extra:
        args.update(extra)
    res = query(args)
    e = find_button(res, text)
    if e is None:
        hits = [(x.get("role"), x.get("text")) for x in res.get("entries", [])]
        if verbose:
            print("NO BUTTON '%s'; entries=%s" % (text, hits[:12]))
        return False
    ref = e.get("targetRef")
    if not ref:
        if verbose:
            print("BUTTON '%s' has no targetRef (not clickable now)" % text)
        return False
    wid = status().get("worldId")
    out = result_of(tool("operate_ui", {"action": "click", "target_ref": ref, "world_id": wid}))
    if verbose:
        print("clicked '%s' -> %s" % (text, json.dumps(out, ensure_ascii=False)[:300]))
    time.sleep(1.2)
    return True


def status():
    return result_of(tool("get_game_status", {}))


if __name__ == "__main__":
    cmd = sys.argv[1] if len(sys.argv) > 1 else "status"
    if cmd == "status":
        st = status()
        print(json.dumps({k: st.get(k) for k in
                          ("ready", "mainMenu", "scene", "saveSlot", "day", "worldId", "sessionId")},
                         ensure_ascii=False))
    elif cmd == "click":
        print(click_text(sys.argv[2]))
    elif cmd == "texts":
        res = query({"max_nodes": 200, "max_depth": 6})
        for e in res.get("entries", []):
            t = e.get("text") or ""
            if t:
                print("%-10s %s" % (e.get("role"), t[:80]))
