# -*- coding: utf-8 -*-
"""quit_game.py - leave the game through its own 退出游戏 button.

Never TaskKill: a killed process can leave the .es3 files mid-write. Clicking
the menu button lets the game flush and shut down on its own terms.

Exits 0 when the process is gone, 3 when the button could not be found (then it
falls back to asking the user rather than forcing anything).
"""
import sys
import time

from mcp_cli import mcp_tool, payload


def q(n, a, timeout=300):
    return payload(mcp_tool(n, a, timeout=timeout))


def running():
    r = q("get_game_status", {})
    return r.get("ok") is not False and r.get("scene") is not None


CLOSERS = ("BackButton", "CloseButton", "Close", "BtnClose")


def scan():
    """-> (worldId, quit_entry, [closer_entry, ...])"""
    r = q("query_ui", {"view": "overview", "limit": 200})
    es = r.get("entries") or []
    quit_btn = next((e for e in es
                     if (e.get("text") or "") == "退出游戏" and e.get("targetRef")), None)
    closers = [e for e in es
               if (e.get("name") or "") in CLOSERS and e.get("targetRef")]
    return r.get("worldId"), quit_btn, closers


# A sub-window (载入游戏) that is still open swallows the main-menu click: the
# bridge reports topHitName=ColumnScroll instead of the button. Close everything
# on top first, then click quit.
for attempt in range(4):
    wid, btn, closers = scan()
    if btn and not closers:
        print("[quit] clicking 退出游戏 (attempt %d)" % (attempt + 1))
        r = q("operate_ui", {"world_id": wid, "action": "click",
                             "target_ref": btn["targetRef"]})
        print("   ->", {k: r.get(k) for k in ("action", "topHitName") if k in r})
        if r.get("topHitName") in (None, "", "None"):
            break
        print("   blocked by", r.get("topHitName"))
    elif closers:
        c = closers[0]
        print("[quit] closing sub-window via %s" % c.get("name"))
        q("operate_ui", {"world_id": wid, "action": "click", "target_ref": c["targetRef"]})
    else:
        print("[quit] neither quit button nor closer visible (attempt %d)" % (attempt + 1))
    time.sleep(2.0)
else:
    print("could not reach 退出游戏; leaving the game alone", file=sys.stderr)
    sys.exit(3)

for i in range(30):
    time.sleep(1.0)
    try:
        if not running():
            print("[quit] game is gone after %ds" % (i + 1))
            sys.exit(0)
    except Exception:
        print("[quit] bridge unreachable after %ds (process closing)" % (i + 1))
        sys.exit(0)
print("[quit] still running after 30s", file=sys.stderr)
sys.exit(4)
