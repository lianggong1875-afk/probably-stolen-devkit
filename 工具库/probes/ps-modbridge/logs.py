# -*- coding: utf-8 -*-
"""logs.py - query the bridge log buffer without fighting PowerShell quoting.

  python logs.py burner
  python logs.py --atlas deepspace_arcade     # summarise missing-sprite warnings
  python logs.py --level Warning --limit 40 texture
"""
import re
import sys

from mcp_cli import mcp_tool, payload

ATLAS_RE = re.compile(r"Sprite name '([^']+)' in atlas '([^']+)' was not found")


def fetch(contains, limit, level=None, after=None):
    args = {"limit": limit}
    if contains:
        args["contains"] = contains
    if level:
        args["level"] = level
    if after is not None:
        args["after"] = after
    return payload(mcp_tool("get_game_logs", args))


def main():
    argv = sys.argv[1:]
    limit = 100
    level = None
    if "--limit" in argv:
        i = argv.index("--limit")
        limit = int(argv[i + 1])
        del argv[i:i + 2]
    if "--level" in argv:
        i = argv.index("--level")
        level = argv[i + 1]
        del argv[i:i + 2]

    if "--atlas" in argv:
        i = argv.index("--atlas")
        atlas = argv[i + 1] if i + 1 < len(argv) else "deepspace_arcade"
        d = fetch(atlas, max(limit, 200))
        names, by_atlas = [], {}
        for e in d.get("entries") or []:
            m = ATLAS_RE.search(e.get("message") or "")
            if m:
                names.append(m.group(1))
                by_atlas.setdefault(m.group(2), []).append(m.group(1))
        print("matched=%s latest=%s overwritten=%s lost=%s"
              % (d.get("matched"), d.get("latestCursor"),
                 d.get("bufferOverwritten"), d.get("lostEntries")))
        for a, ns in by_atlas.items():
            print("  atlas %-20s missing %d sprite(s)" % (a, len(ns)))
        runs = [n for n in names if n.startswith("burner_run")]
        print("  burner_run_* missing :", sorted(set(runs)) if runs else "NONE")
        print("  first names          :", names[:10])
        return

    contains = argv[0] if argv else ""
    d = fetch(contains, limit, level)
    print("matched=%s latest=%s overwritten=%s lost=%s"
          % (d.get("matched"), d.get("latestCursor"),
             d.get("bufferOverwritten"), d.get("lostEntries")))
    for e in d.get("entries") or []:
        print("  [%-7s] %s" % (e.get("level"), (e.get("message") or "")[:170]))


if __name__ == "__main__":
    main()
