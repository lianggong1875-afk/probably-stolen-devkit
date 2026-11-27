# -*- coding: utf-8 -*-
"""从 Portraits/ 目录生成 portrait-studio-provider.json（通用版）。

为什么必须有这个脚本：清单里每张图都要写 width/height/contentHash（PNG 的 SHA256）
以及 geometry（rect/PPU/pivot/border）。手写必然和实际文件对不上，而
portrait-studio-pack 的 validate.py 会逐条比对尺寸与哈希，错了直接 FAIL。

约定：一个 sprite key 一个文件夹 `Portraits/<key>/`，文件夹里的每个 PNG 就是一个
候选（差分放进去即可）。多张图时第一张（按文件名排序）作为 defaultResource。

用法：
  python gen_manifest.py --source <包工程目录>
  python gen_manifest.py --source <包> --ppu-map "dnpc_:694.8,sw_:706.49" --pivot 0.5,0.657
  python gen_manifest.py --source <包> --set-name "深空电玩" --dry

PPU 取值规则（后者优先）：
  1. --ppu 给的全局值（默认 100）
  2. --ppu-map 里最长匹配前缀的值
PPU 必须是目标模组**实际注册**的值，不能按 PNG 尺寸猜 —— 猜错立绘大小就变了。
"""
import argparse
import hashlib
import json
import struct
import sys
from pathlib import Path


def png_size(path):
    with open(path, 'rb') as f:
        head = f.read(24)
    if head[:8] != b'\x89PNG\r\n\x1a\n':
        raise SystemExit('not a PNG: %s' % path)
    return struct.unpack('>II', head[16:24])


def sha256(path):
    h = hashlib.sha256()
    with open(path, 'rb') as f:
        for chunk in iter(lambda: f.read(1 << 20), b''):
            h.update(chunk)
    return h.hexdigest()


def parse_ppu_map(text):
    out = []
    if not text:
        return out
    for part in text.split(','):
        part = part.strip()
        if not part:
            continue
        if ':' not in part:
            raise SystemExit('--ppu-map entries need prefix:ppu, got %r' % part)
        prefix, value = part.rsplit(':', 1)
        out.append((prefix, float(value)))
    # 最长前缀优先
    out.sort(key=lambda p: len(p[0]), reverse=True)
    return out


def ppu_for(key, global_ppu, ppu_map):
    for prefix, value in ppu_map:
        if prefix and key.startswith(prefix):
            return value
    return global_ppu


def main():
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument('--source', required=True, type=Path, help='包工程目录（含 Portraits\\）')
    ap.add_argument('--provider-id', default=None, help='默认取清单里已有的 providerId')
    ap.add_argument('--ppu', type=float, default=100.0, help='全局 PPU（默认 100）')
    ap.add_argument('--ppu-map', default='', help='prefix:ppu,prefix:ppu（最长前缀优先）')
    ap.add_argument('--pivot', default='0.5,0.5', help='pivotX,pivotY（默认 0.5,0.5）')
    ap.add_argument('--set-name', default='全部立绘', help='portraitSets 里那一套的名字')
    ap.add_argument('--revision', default='1')
    ap.add_argument('--dry', action='store_true')
    a = ap.parse_args()

    src = a.source.resolve()
    manifest_path = src / 'portrait-studio-provider.json'
    if not manifest_path.exists():
        raise SystemExit('找不到 %s —— 先用 new_pack.py 建工程' % manifest_path)

    root = json.loads(manifest_path.read_text(encoding='utf-8'))
    provider_id = a.provider_id or root.get('providerId')
    if not provider_id:
        raise SystemExit('清单里没有 providerId，请用 --provider-id 指定')

    try:
        pivot_x, pivot_y = (float(x) for x in a.pivot.split(','))
    except Exception:
        raise SystemExit('--pivot 需要 "x,y"，例如 0.5,0.657')

    ppu_map = parse_ppu_map(a.ppu_map)
    portraits = src / 'Portraits'
    if not portraits.is_dir():
        raise SystemExit('找不到 %s' % portraits)

    keys = sorted(p.name for p in portraits.iterdir() if p.is_dir())
    if not keys:
        raise SystemExit('Portraits\\ 下没有角色文件夹')

    targets, sets, set_entries, total_imgs = [], [], [], 0
    for key in keys:
        pngs = sorted((portraits / key).glob('*.png'))
        if not pngs:
            print('  skip %-18s (empty folder)' % key)
            continue
        ppu = ppu_for(key, a.ppu, ppu_map)
        set_id = key + '-main'
        resources = []
        for png in pngs:
            w, h = png_size(png)
            resources.append({
                'id': png.stem,
                'resourceRevision': a.revision,
                'renderMode': 'static',
                'path': str(png.relative_to(src)).replace('\\', '/'),
                'texture': {
                    'width': w, 'height': h, 'contentHash': sha256(png),
                    'filter': 'Point',
                    'wrapU': 'Clamp', 'wrapV': 'Clamp', 'wrapW': 'Clamp',
                    'anisoLevel': 0, 'mipMapBias': 0, 'colorSpace': 'sRGB',
                    'mipmaps': {'mode': 'none'},
                },
                'geometry': {
                    'rect': {'x': 0, 'y': 0, 'width': w, 'height': h},
                    'pixelsPerUnit': ppu,
                    'pivotX': pivot_x, 'pivotY': pivot_y,
                    'border': {'left': 0, 'bottom': 0, 'right': 0, 'top': 0},
                    'meshType': 'FullRect',
                },
            })
            total_imgs += 1
        refs = [{'providerId': provider_id, 'setId': set_id,
                 'id': r['id'], 'revision': a.revision} for r in resources]
        targets.append({
            'id': set_id,
            'kind': 'character',
            'renderMode': 'static',
            # owner=game 表示"显示表面由游戏原版渲染"，binding 用 sprite-key 指向该 key
            'subject': {'owner': 'game', 'key': key},
            'slot': 'main',
            'binding': {'type': 'sprite-key', 'key': key},
            'defaultMode': 'resource',
            'defaultResource': refs[0],
            'candidateResources': refs,
        })
        sets.append({
            'id': set_id,
            'kind': 'character',
            'renderMode': 'static',
            'subject': {'owner': 'game', 'key': key},
            'slot': 'main',
            'resources': resources,
        })
        set_entries.append(refs[0])
        print('  %-18s ppu=%-8g %d image(s)' % (key, ppu, len(pngs)))

    if not targets:
        raise SystemExit('一张图都没找到')

    out = {
        'format': 'portrait-studio-static/1',
        'contract': 'static-display-transaction/1',
        'providerId': provider_id,
        'revision': a.revision,
        'catalogRevision': a.revision,
        'role': 'writer',
        'renderMode': 'static',
        'targets': targets,
        'resourceSets': sets,
        'portraitSets': [{'id': 'default', 'name': a.set_name,
                          'renderMode': 'static', 'entries': set_entries}],
    }

    if not a.dry:
        manifest_path.write_text(json.dumps(out, ensure_ascii=False, indent=2) + '\n',
                                 encoding='utf-8')
    print('targets=%d resourceSets=%d images=%d -> %s%s'
          % (len(targets), len(sets), total_imgs, manifest_path.name,
             ' (dry, not written)' if a.dry else ''))
    if not a.dry:
        print('下一步：python <kit>\\scripts\\validate.py "%s"' % src)


if __name__ == '__main__':
    main()
