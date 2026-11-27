# -*- coding: utf-8 -*-
"""
gen_fz_art.py —— 本地 fz 网关辅助生图脚本（走 127.0.0.1:7890 -> api.ttk.homes）
说明：仅读取 DSH_HOME/.credentials.yaml 的 FZ_API_KEY，严禁触碰按美元计费的 OpenAI 官方 Key。
"""
import argparse
import base64
import json
import os
import re
import urllib.error
import urllib.request


def load_cred(env_key):
    cred_path = os.path.join(os.environ.get("DSH_HOME", ""), ".credentials.yaml")
    if not os.path.exists(cred_path):
        return ""
    with open(cred_path, "r", encoding="utf-8") as f:
        txt = f.read()
    m = re.search(rf"^\s*{re.escape(env_key)}:\s*([^\r\n]+)", txt, re.MULTILINE)
    if not m:
        return ""
    return m.group(1).strip().strip("'").strip('"')


def make_opener():
    proxy = urllib.request.ProxyHandler({
        "http": "http://127.0.0.1:7890",
        "https": "http://127.0.0.1:7890",
    })
    return urllib.request.build_opener(proxy)


def extract_image_bytes(resp_obj):
    opener = make_opener()
    s = json.dumps(resp_obj)
    m = re.search(r"data:image/(?:png|jpeg|webp);base64,([A-Za-z0-9+/=]+)", s)
    if m:
        return base64.b64decode(m.group(1))
    if isinstance(resp_obj, dict) and "data" in resp_obj:
        for item in resp_obj["data"]:
            if isinstance(item, dict):
                if item.get("b64_json"):
                    return base64.b64decode(item["b64_json"])
                if item.get("url"):
                    req = urllib.request.Request(item["url"], headers={"User-Agent": "Mozilla/5.0"})
                    with opener.open(req, timeout=45) as r:
                        return r.read()
    m_url = re.search(r"!\[[^\]]*\]\((https?://[^)\s]+)\)", s)
    if m_url:
        req = urllib.request.Request(m_url.group(1), headers={"User-Agent": "Mozilla/5.0"})
        with opener.open(req, timeout=45) as r:
            return r.read()
    m_raw = re.search(r"(https?://[^\s\"')]+?\.(?:png|jpg|jpeg|webp)[^\s\"')]*)", s)
    if m_raw:
        req = urllib.request.Request(m_raw.group(1), headers={"User-Agent": "Mozilla/5.0"})
        with opener.open(req, timeout=45) as r:
            return r.read()
    return None


def generate(prompt, out_path, model="[按次计费-4X]gpt-image-2.6", size="1024x1024"):
    fz_key = load_cred("FZ_API_KEY")
    if not fz_key:
        raise SystemExit("未在 .credentials.yaml 中找到 FZ_API_KEY")
    opener = make_opener()
    headers = {
        "Content-Type": "application/json",
        "Authorization": f"Bearer {fz_key}",
        "User-Agent": "Mozilla/5.0",
    }
    try:
        body = json.dumps({
            "model": model,
            "prompt": prompt,
            "n": 1,
            "size": size,
            "response_format": "b64_json",
        }).encode("utf-8")
        req = urllib.request.Request("https://api.ttk.homes/v1/images/generations", data=body, headers=headers)
        with opener.open(req, timeout=120) as r:
            resp = json.loads(r.read().decode("utf-8"))
        img = extract_image_bytes(resp)
        if img:
            os.makedirs(os.path.dirname(os.path.abspath(out_path)), exist_ok=True)
            with open(out_path, "wb") as f:
                f.write(img)
            return "images/generations", len(img)
    except Exception as e:
        print(f"[info] images/generations fallback ({e})")

    body = json.dumps({"model": model, "messages": [{"role": "user", "content": prompt}]}).encode("utf-8")
    req = urllib.request.Request("https://api.ttk.homes/v1/chat/completions", data=body, headers=headers)
    with opener.open(req, timeout=120) as r:
        resp = json.loads(r.read().decode("utf-8"))
    img = extract_image_bytes(resp)
    if not img:
        raise RuntimeError(f"No image extracted: {str(resp)[:300]}")
    os.makedirs(os.path.dirname(os.path.abspath(out_path)), exist_ok=True)
    with open(out_path, "wb") as f:
        f.write(img)
    return "chat/completions", len(img)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("prompt")
    ap.add_argument("out")
    ap.add_argument("--model", default="[按次计费-4X]gpt-image-2.6")
    ap.add_argument("--size", default="1024x1024")
    args = ap.parse_args()
    ep, n = generate(args.prompt, args.out, args.model, args.size)
    print(f"OK via {ep}: {n} bytes -> {args.out}")


if __name__ == "__main__":
    main()
