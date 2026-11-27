# -*- coding: utf-8 -*-
"""MCP CLI wrapper around: node mcp.mjs tool <name> @args.json [--save out.json]

Usage: python mcp_cli.py <toolName> <jsonArgsOrFile> [--raw]
Prints the tool's text payload (JSON when the payload is JSON).
"""
import json, os, subprocess, sys, tempfile, time

NODE = r"C:\Program Files\nodejs\node.exe"
MCP = os.path.join(os.path.dirname(os.path.abspath(__file__)), "mcp.mjs")
URL = os.environ.get("MODBRIDGE_URL", "http://127.0.0.1:18774/mcp")
# 临时文件跟着工具走（工具库\probes\ps-modbridge\tmp），不再挂在会被清掉的缓存目录下。
TMP = os.environ.get("MODBRIDGE_TMP", os.path.join(os.path.dirname(os.path.abspath(__file__)), "tmp"))
os.makedirs(TMP, exist_ok=True)


def mcp_tool(name, args, timeout=300, raw=False):
    stamp = "%d_%d" % (os.getpid(), int(time.time() * 1000) % 1000000)
    af = os.path.join(TMP, "args_%s.json" % stamp)
    sf = os.path.join(TMP, "out_%s.json" % stamp)
    with open(af, "w", encoding="utf-8") as f:
        json.dump(args, f, ensure_ascii=False)
    cmd = [NODE, MCP, "tool", name, "@" + af, "--compact", "--url", URL, "--save", sf]
    try:
        p = subprocess.run(cmd, capture_output=True, timeout=timeout)
        out = (p.stdout or b"").decode("utf-8", "replace")
        err = (p.stderr or b"").decode("utf-8", "replace")
        saved = None
        if os.path.exists(sf):
            try:
                with open(sf, "r", encoding="utf-8-sig") as f:
                    saved = json.load(f)
            except Exception:
                saved = None
        return {"_stdout": out, "_stderr": err, "_rc": p.returncode, "_saved": saved}
    finally:
        for x in (af, sf):
            try: os.remove(x)
            except Exception: pass


def texts(res):
    """Return concatenated text content from tool result."""
    saved = res.get("_saved")
    node = saved
    # unwrap common envelope shapes
    for _ in range(4):
        if isinstance(node, dict) and "content" in node and isinstance(node["content"], list):
            return "\n".join(i.get("text", "") for i in node["content"]
                             if isinstance(i, dict) and i.get("type") == "text")
        if isinstance(node, dict) and "result" in node and "content" not in node:
            node = node["result"]; continue
        if isinstance(node, dict) and "toolResult" in node:
            node = node["toolResult"]; continue
        break
    if isinstance(node, dict) and "content" in node and isinstance(node["content"], list):
        return "\n".join(i.get("text", "") for i in node["content"]
                         if isinstance(i, dict) and i.get("type") == "text")
    if saved is not None:
        return json.dumps(saved, ensure_ascii=False)
    return res.get("_stdout", "")


def payload(res):
    """Best-effort parsed payload object."""
    t = texts(res)
    try:
        v = json.loads(t)
        for _ in range(3):
            if isinstance(v, dict) and isinstance(v.get("result"), dict):
                v = v["result"]; continue
            break
        return v
    except Exception:
        pass
    i, j = t.find("{"), t.rfind("}")
    if i >= 0 and j > i:
        try: return json.loads(t[i:j + 1])
        except Exception: pass
    return {"_raw": t}


if __name__ == "__main__":
    name = sys.argv[1]
    a = sys.argv[2] if len(sys.argv) > 2 else "{}"
    args = json.loads(a) if a.strip().startswith(("{", "[")) else json.load(open(a, encoding="utf-8-sig"))
    r = mcp_tool(name, args)
    if "--raw" in sys.argv:
        print(json.dumps(r, ensure_ascii=False)[:6000])
    else:
        print(texts(r))
