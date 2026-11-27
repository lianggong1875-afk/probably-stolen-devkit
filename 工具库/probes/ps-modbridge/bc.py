# -*- coding: utf-8 -*-
"""bc.py - one-shot bridge call that survives PowerShell's argument mangling.

Why this exists: Windows PowerShell 5.1 strips the double quotes out of a
native command's argument, so

    python mcp_cli.py query_ui '{"view":"overview"}'

arrives at python as  {view:overview}  and json.loads dies. Only quote-free
payloads like '{}' make it through. This shim therefore prefers stdin:

    '{"view":"overview"}' | python bc.py query_ui
    python bc.py query_ui @args.json

Usage:  bc.py <toolName> [json | @file]   (JSON may also come from stdin)
        add --pretty for indented output, --keys to list only the top keys.
Prints the tool payload as JSON on one line.
"""
import json
import sys

from mcp_cli import mcp_tool, payload


def read_args(argv):
    if len(argv) > 2 and not argv[2].startswith("--"):
        spec = argv[2]
        if spec.startswith("@"):
            with open(spec[1:], encoding="utf-8-sig") as f:
                return json.load(f)
        try:
            return json.loads(spec)
        except Exception:
            pass
    if sys.stdin is not None and not sys.stdin.isatty():
        # PowerShell pipes UTF-8 *with* a BOM; json.loads refuses U+FEFF.
        raw = sys.stdin.read().lstrip("\ufeff").strip()
        if raw:
            return json.loads(raw)
    return {}


def main():
    if len(sys.argv) < 2:
        sys.exit(__doc__)
    tool = sys.argv[1]
    args = read_args(sys.argv)
    res = mcp_tool(tool, args)
    data = payload(res)
    if "--keys" in sys.argv and isinstance(data, dict):
        print(json.dumps(sorted(data.keys()), ensure_ascii=False))
        return
    if "--pretty" in sys.argv:
        print(json.dumps(data, ensure_ascii=False, indent=2))
    else:
        print(json.dumps(data, ensure_ascii=False))
    if res.get("_rc", 0) != 0:
        print("rc=%s stderr=%s" % (res.get("_rc"), (res.get("_stderr") or "")[:400]), file=sys.stderr)


if __name__ == "__main__":
    main()
