# -*- coding: utf-8 -*-
"""load_slot.py - load a save by what its CARD says, then prove the slot matches.

Why not by Plate index: indexes shift depending on how many slots exist, and an
earlier helper hard-coded "Plate[1] is the test save", which silently loaded
slot 0 (the 51-day main save) instead. The card body text, on the other hand,
carries the day count, credit and timestamp rendered straight out of
saves_index.es3 -> preview_<slotId>, so after fix_index.py has run it is the most
reliable witness on screen.

  python load_slot.py                 # slot 1 / 第12天  (the test save)
  python load_slot.py --slot 1 --day 12
  python load_slot.py --day 12 --any  # accept whatever slot it lands in

Safety: this never writes. It refuses to run when already inside a different
save, and it always re-reads get_game_status afterwards so a wrong slot is
reported loudly instead of being used.
"""
import argparse
import json
import re
import sys
import time

from mcp_cli import mcp_tool, payload

DAY_RE = re.compile(r"第\s*(\d+)\s*天")


def q(n, a, timeout=300):
    return payload(mcp_tool(n, a, timeout=timeout))


def status():
    return q("get_game_status", {})


def cards():
    """-> (worldId, [(day, credit, text, plate_index, load_latest_entry)])"""
    r = q("query_ui", {"view": "overview", "limit": 200})
    wid = r.get("worldId")
    entries = r.get("entries") or []
    out = []
    for e in entries:
        txt = e.get("text") or ""
        if not (e.get("role") == "PointerClick" and txt.startswith("第")):
            continue
        m = DAY_RE.search(txt)
        if not m:
            continue
        det = q("get_ui_details", {"world_id": wid, "ui_ref": e["uiRef"]})
        path = det.get("path") or ""
        pm = re.search(r"Plate\[(\d+)\]", path)
        plate = int(pm.group(1)) if pm else None
        out.append({
            "day": int(m.group(1)),
            "text": txt,
            "plate": plate,
            "path": path,
        })
    # attach the matching LoadLatest button by plate index
    for c in out:
        c["button"] = None
        for e in entries:
            if e.get("name") != "LoadLatest":
                continue
            det = q("get_ui_details", {"world_id": wid, "ui_ref": e["uiRef"]})
            p = det.get("path") or ""
            pm = re.search(r"Plate\[(\d+)\]", p)
            if pm and c["plate"] is not None and int(pm.group(1)) == c["plate"]:
                c["button"] = e
                break
    return wid, out


def open_load_menu(wid, entries_probe):
    """Make sure the 载入游戏 window is showing. -> (worldId, True/False)"""
    r = q("query_ui", {"view": "overview", "limit": 200})
    wid = r.get("worldId") or wid
    es = r.get("entries") or []
    if any((e.get("text") or "").startswith("第") and "信用点" in (e.get("text") or "") for e in es):
        return wid, True
    btn = next((e for e in es if (e.get("text") or "") == "载入游戏" and e.get("targetRef")), None)
    if not btn:
        return wid, False
    q("operate_ui", {"world_id": wid, "action": "click", "target_ref": btn["targetRef"]})
    time.sleep(2.5)
    return wid, True


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--slot", type=int, default=1)
    ap.add_argument("--day", type=int, default=12)
    ap.add_argument("--any", action="store_true", help="do not require an exact slot")
    a = ap.parse_args()

    st = status()
    print("[0] scene=%s ready=%s slot=%s day=%s" %
          (st.get("scene"), st.get("ready"), st.get("saveSlot"), st.get("day")))

    if st.get("ready"):
        got_slot, got_day = st.get("saveSlot"), st.get("day")
        if got_slot == 0 and not a.any:
            sys.exit("已在 slot 0（主档）—— 先退回主菜单，别在这个档里做事")
        if got_slot == a.slot or a.any:
            print("已经在目标存档里 (slot=%s day=%s)，不重复载入" % (got_slot, got_day))
            return
        sys.exit("已在别处 (slot=%s day=%s)；先退回主菜单" % (got_slot, got_day))

    wid = st.get("worldId")
    wid, ok = open_load_menu(wid, None)
    if not ok:
        sys.exit("找不到「载入游戏」按钮，界面不在主菜单？")

    wid, cs = cards()
    print("[1] 卡面 (%d 张):" % len(cs))
    for c in cs:
        print("      Plate[%s] %s%s" % (c["plate"], c["text"].split(" | 版本")[0],
                                        "" if c["button"] else "   <no LoadLatest>"))

    target = next((c for c in cs if c["day"] == a.day and c["button"]), None)
    if not target:
        sys.exit("没有卡面写着第%d天 的存档卡" % a.day)
    print("[2] 选 Plate[%s] = %s" % (target["plate"], target["text"].split(" | 版本")[0]))

    res = q("operate_ui", {"world_id": wid, "action": "click",
                           "target_ref": target["button"]["targetRef"]})
    print("[3] click ->", {k: res.get(k) for k in ("action", "topHitName") if k in res})

    st2 = None
    for _ in range(45):
        time.sleep(2)
        s = status()
        if s.get("ready") and s.get("saveSlot") is not None:
            st2 = s
            break
    if not st2:
        sys.exit("载入超时")
    print("[4] 已载入:", json.dumps({k: st2.get(k) for k in
                                    ("ready", "saveSlot", "day", "worldId", "scene")},
                                   ensure_ascii=False))
    if not a.any and st2.get("saveSlot") != a.slot:
        sys.exit("落进 slot %s，不是 %s —— 停手，什么都不做" % (st2.get("saveSlot"), a.slot))
    print("OK slot=%s day=%s world=%s" % (st2.get("saveSlot"), st2.get("day"), st2.get("worldId")))


if __name__ == "__main__":
    main()
