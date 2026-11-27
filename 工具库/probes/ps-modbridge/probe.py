# -*- coding: utf-8 -*-
"""probe.py - run one command on ArcSummonProbe inside the running game.

  python probe.py who
  python probe.py "summon dnpc_buyer_03"
  python probe.py --raw state

Wraps the three-step dance the bridge requires (open_test_session ->
load_probe_module -> call_probe) and reuses a session that is already open, so
this is safe to call repeatedly while the game stays up.

Notes that cost time to learn:
  * a newly rebuilt DLL is only picked up after a game restart; if you get
    session_mismatch, close the game and relaunch instead of retrying;
  * load_probe_module takes the file NAME, not a path - the DLL must already sit
    in the modules directory.
"""
import glob
import json
import os
import re
import sys
import time

from mcp_cli import mcp_tool, payload

MODULE_DIR = r"C:\Users\qianxia1\AppData\Local\ModDevBridgeNext\modules"


def newest_module():
    """Pick the highest-numbered ArcSummonProbe*.dll in the module directory.

    The bridge refuses a second load of an already-loaded assembly name
    ("rebuild with a unique name"), and a game restart is not always welcome, so
    every rebuild gets a new number and this picks the latest. Falls back to the
    plain name.
    """
    best, best_n = None, -1
    for path in glob.glob(os.path.join(MODULE_DIR, "ArcSummonProbe*.dll")):
        m = re.search(r"ArcSummonProbe(\d*)\.dll$", os.path.basename(path), re.I)
        if not m:
            continue
        n = int(m.group(1)) if m.group(1) else 1
        if n > best_n:
            best, best_n = os.path.basename(path), n
    return best or "ArcSummonProbe.dll"


def q(name, args, timeout=300):
    return payload(mcp_tool(name, args, timeout=timeout))


def ensure():
    st = q("get_game_status", {})
    if not st.get("ready"):
        raise SystemExit("game not ready (ready=false) - load a save first")
    sess = q("open_test_session", {})
    sid = sess.get("sessionId")
    wid = sess.get("worldId") or st.get("worldId")
    if not sid:
        st2 = q("get_game_status", {})
        sid = st2.get("sessionId") or st2.get("activeSessionId")
        wid = st2.get("worldId") or wid
    if not sid:
        raise SystemExit("no sessionId: " + json.dumps(sess, ensure_ascii=False)[:300])
    lp = q("load_probe_module", {"session_id": sid, "world_id": wid, "filename": newest_module()})
    ok = lp.get("loaded") if isinstance(lp, dict) else None
    err = str((lp or {}).get("error") or "")
    # "Stop the active module first" just means a probe from an earlier call in
    # this same game run is still resident - call_probe will reach it.
    if not ok and "Stop the active module" not in err and err:
        raise SystemExit("load_probe_module failed: " + json.dumps(lp, ensure_ascii=False)[:300])
    return sid, wid


def main():
    argv = [a for a in sys.argv[1:]]
    raw = "--raw" in argv
    argv = [a for a in argv if a != "--raw"]
    if not argv:
        raise SystemExit(__doc__)
    cmd = " ".join(argv)
    sid, wid = ensure()
    t0 = time.time()
    res = q("call_probe", {"session_id": sid, "world_id": wid, "command": cmd})
    dt = time.time() - t0
    if raw:
        print(json.dumps(res, ensure_ascii=False, indent=2))
        return
    # payload() already unwrapped the JSON-RPC envelope, so call_probe's own
    # shape ({text, assertionVerified}) is the payload itself - do not unwrap
    # "result" again or everything prints as None.
    out = res
    if isinstance(res, dict):
        if "text" in res:
            out = res["text"]
        elif isinstance(res.get("result"), (dict, list, str)):
            out = res["result"]
    if isinstance(out, (dict, list)):
        print(json.dumps(out, ensure_ascii=False, indent=2))
    else:
        print(out)
    if isinstance(res, dict) and res.get("error"):
        print("ERROR: " + str(res.get("error")), file=sys.stderr)
    print("[%.2fs]" % dt, file=sys.stderr)


if __name__ == "__main__":
    main()
