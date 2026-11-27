# -*- coding: utf-8 -*-
"""把当前界面上所有可点条目连 path 一起打出来，方便按 Plate[N] 定位。

用法：python probe_ui.py [关键字]
"""
import json
import sys

from mcp_cli import mcp_tool, payload


def q(name, args, timeout=300):
    return payload(mcp_tool(name, args, timeout=timeout))


kw = sys.argv[1] if len(sys.argv) > 1 else None
r = q("query_ui", {"view": "overview", "max_nodes": 1000})
wid = r.get("worldId")
print("world=%s total=%s" % (wid, r.get("total")))
for e in r.get("entries") or []:
    role = e.get("role")
    if role in ("Drag", "Window"):
        continue
    text = (e.get("text") or "").replace("\n", " ")[:70]
    if kw and kw not in text and kw not in (e.get("name") or ""):
        continue
    d = q("get_ui_details", {"world_id": wid, "ui_ref": e["uiRef"]})
    path = d.get("path") or ""
    ref = (d.get("observation") or {}).get("actionRef")
    print("name=%s role=%s text=%r path=%s action=%s" % (e.get("name"), role, text, path, "yes" if ref else "-"))
