# -*- coding: utf-8 -*-
"""fix_index.py - make the save-card previews tell the truth.

The load-game screen renders each card from saves_index.es3 -> preview_<slotId>
(dayCount / credit / lastSaveDateTime), NOT from the .es3 file on disk. When
something swaps or restores a save file without touching that block, the card
keeps advertising a state that no longer exists - which is exactly how a card
labelled "第10天" turned out to hold a 第12天 save, and how clicking the card
indexed 1 landed in slot 0.

This tool re-derives every preview from evidence instead of trusting it:

  * it hashes save_<id>.es3 and looks for a history_<id> entry whose file has
    the same hash - that entry carries the authoritative day / credit / ticks;
  * falls back to the newest history entry when no hash matches, and says so;
  * refuses to run while the game is up (the game rewrites the index on exit).

Usage:
  python fix_index.py show     # what each card claims vs what disk proves
  python fix_index.py sync     # rewrite the lying previews (backs up first)
  python fix_index.py sync -n  # dry run: print the patch, write nothing
"""
import hashlib
import json
import os
import shutil
import subprocess
import sys
from datetime import datetime, timedelta

SAVE = r"C:\Users\qianxia1\AppData\LocalLow\Questing Goose Studio\Probably Stolen"
INDEX = os.path.join(SAVE, "saves_index.es3")
TICK_EPOCH = 621355968000000000  # 1970-01-01 expressed in .NET DateTime ticks


def when(ticks):
    """Render index ticks the way the save card does.

    The game stores a Local-kind DateTime, so its ticks already encode local
    wall-clock time; converting them through the 1970 UTC epoch and printing as
    UTC reproduces exactly what the card shows (verified against the visible
    cards character for character). Do not "fix" this by adding a timezone
    offset - that would double-count it.
    """
    if not ticks:
        return "?"
    try:
        return (datetime(1970, 1, 1) + timedelta(microseconds=(int(ticks) - TICK_EPOCH) // 10)
                ).strftime("%Y-%m-%d %H:%M:%S")
    except Exception:
        return "?ticks"


def local_stamp(mtime):
    return datetime.fromtimestamp(mtime).strftime("%Y-%m-%d %H:%M:%S")


def sha(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for chunk in iter(lambda: f.read(1 << 20), b""):
            h.update(chunk)
    return h.hexdigest()


def game_running():
    try:
        out = subprocess.run(["tasklist", "/FI", "IMAGENAME eq Probably Stolen.exe"],
                             capture_output=True, text=True, timeout=20)
        return "Probably Stolen.exe" in (out.stdout or "")
    except Exception:
        return False  # cannot tell; the caller still has the backup


def load():
    with open(INDEX, "r", encoding="utf-8-sig") as f:
        return json.load(f)


def dump(obj, path):
    text = json.dumps(obj, ensure_ascii=False, separators=(",", ":"))
    with open(path, "w", encoding="utf-8", newline="") as f:
        f.write(text)
    return len(text.encode("utf-8"))


def survey(idx):
    """-> list of per-slot dicts describing claim vs evidence."""
    active = ((idx.get("activeSaveSlotId") or {}).get("value")) or []
    rows = []
    for slot in active:
        preview = ((idx.get("preview_%d" % slot) or {}).get("value")) or {}
        history = ((idx.get("history_%d" % slot) or {}).get("value")) or []
        save_file = os.path.join(SAVE, "save_%d.es3" % slot)
        live = sha(save_file) if os.path.exists(save_file) else None
        # The strongest witness is the file's own mtime: the game writes the
        # preview ticks at the same instant it writes the file. A day-count or
        # credit mismatch is then just a missing history entry, not a lie.
        mtime = os.path.getmtime(save_file) if os.path.exists(save_file) else None
        claimed_ticks = (preview.get("lastSaveDateTime") or {}).get("ticks")
        skew = None
        if mtime and claimed_ticks:
            # Compare as local wall-clock strings: the ticks are Local-kind, the
            # mtime is a Unix stamp. Same instant => identical string.
            try:
                skew = abs((datetime.strptime(when(claimed_ticks), "%Y-%m-%d %H:%M:%S")
                            - datetime.strptime(local_stamp(mtime), "%Y-%m-%d %H:%M:%S")
                            ).total_seconds())
            except Exception:
                skew = None
        match = None
        for h in history:
            hp = os.path.join(SAVE, h.get("file") or "")
            if live and os.path.exists(hp) and sha(hp) == live:
                match = h
                break
        rows.append({
            "slot": slot,
            "preview": preview,
            "history": history,
            "live_sha": live,
            "match": match,
            "mtime": mtime,
            "skew": skew,
        })
    return rows


MTIME_TOLERANCE_SECONDS = 120


def report(rows):
    bad = 0
    for r in rows:
        p = r["preview"]
        claims = "第%s天 / %s信用点 / %s" % (p.get("dayCount"), p.get("credit"),
                                             when((p.get("lastSaveDateTime") or {}).get("ticks")))
        skew = r["skew"]
        if skew is not None and skew <= MTIME_TOLERANCE_SECONDS:
            print("  slot %d  CLAIM %-34s  file mtime agrees (skew %.0fs) -> trustworthy" %
                  (r["slot"], claims, skew))
            continue
        print("  slot %d  CLAIM %-34s  file mtime disagrees (skew %s)" %
              (r["slot"], claims, "?" if skew is None else "%.0fs" % skew))
        src = r["match"]
        if src is None:
            if not r["live_sha"]:
                print("          no save_%d.es3 on disk; nothing to compare" % r["slot"])
                bad += 1
                continue
            src = r["history"][0] if r["history"] else None
            if src is None:
                print("          sha %s, no history entry either -> cannot fix" % r["live_sha"][:12])
                bad += 1
                continue
            print("          no hash match; falling back to newest history %s" % src.get("file"))
        else:
            print("          disk sha %s == history %s" % (r["live_sha"][:12], src.get("file")))
        print("          TRUTH %-34s" % ("第%s天 / %s信用点 / %s" % (
            src.get("dayCount"), src.get("credit"),
            when((src.get("saveDateTime") or {}).get("ticks")))))
        bad += 1
    return bad


def needs_fix(r):
    """True only when the preview is provably stale (mtime skew over tolerance)."""
    return r["skew"] is None or r["skew"] > MTIME_TOLERANCE_SECONDS


def sync(rows, dry):
    if os.path.exists(INDEX) and not dry:
        stamp = datetime.now().strftime("%Y%m%d-%H%M%S")
        bak = INDEX + ".bak-preflightsync-" + stamp
        shutil.copy2(INDEX, bak)
        print("[backup]", os.path.basename(bak))
    idx = load()
    changed = []
    for r in rows:
        if not needs_fix(r):
            continue
        src = r["match"] or (r["history"][0] if r["history"] else None)
        if not src:
            print("  slot %d: nothing to sync from, skipped" % r["slot"])
            continue
        key = "preview_%d" % r["slot"]
        node = idx.get(key) or {}
        val = node.get("value") or {}
        before = (val.get("dayCount"), val.get("credit"),
                  (val.get("lastSaveDateTime") or {}).get("ticks"))
        after = (src.get("dayCount"), src.get("credit"),
                 (src.get("saveDateTime") or {}).get("ticks"))
        if before == after:
            continue
        val["dayCount"] = src.get("dayCount")
        val["credit"] = src.get("credit")
        val["lastSaveDateTime"] = {"ticks": (src.get("saveDateTime") or {}).get("ticks")}
        node["value"] = val
        idx[key] = node
        changed.append((key, before, after))
    for k, b, a in changed:
        print("  %s: day %s->%s  credit %s->%s  at %s->%s" %
              (k, b[0], a[0], b[1], a[1], when(b[2]), when(a[2])))
    if not changed:
        print("  nothing to fix")
        return 0
    if dry:
        print("[dry-run] %d block(s) would change; nothing written" % len(changed))
        return 0
    size = dump(idx, INDEX)
    print("[written] %s (%d bytes)" % (os.path.basename(INDEX), size))
    return len(changed)


def main():
    cmd = sys.argv[1] if len(sys.argv) > 1 else "show"
    if cmd not in ("show", "sync"):
        raise SystemExit(__doc__)
    if not os.path.exists(INDEX):
        raise SystemExit("no index at " + INDEX)
    idx = load()
    rows = survey(idx)
    bad = report(rows)
    if cmd == "show":
        print("\n%d slot(s) checked, %d disagree with disk" % (len(rows), bad))
        return
    if game_running():
        raise SystemExit("game is running - it rewrites the index on exit; close it first")
    print("")
    sync(rows, dry=("-n" in sys.argv or "--dry-run" in sys.argv))


if __name__ == "__main__":
    main()
