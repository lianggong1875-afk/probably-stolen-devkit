"""Local portrait framing editor. Standard library; never resamples PNGs or launches the game."""
import argparse
import copy
import hashlib
from http.server import BaseHTTPRequestHandler, HTTPServer
import json
from pathlib import Path
import secrets
import time
import webbrowser

from validate import inside, schema_check, validate

MARKER = 'portrait-studio-provider.json'


def sha(data):
    return hashlib.sha256(data).hexdigest()


def transform_geometry(geometry, scale=1, x=0, y=0):
    """Move by local world units. Positive x/y moves the artwork right/up."""
    import math
    if not all(isinstance(v, (int, float)) and not isinstance(v, bool) and math.isfinite(v) for v in (scale, x, y)) or scale <= 0:
        raise ValueError('Finite positive scale and finite offsets required')
    g = copy.deepcopy(geometry)
    g['pixelsPerUnit'] /= scale
    g['pivotX'] -= x * g['pixelsPerUnit'] / g['rect']['width']
    g['pivotY'] -= y * g['pixelsPerUnit'] / g['rect']['height']
    return g


class Editor:
    def __init__(self, source):
        self.root = Path(source).resolve()
        if any(p.lower() == 'mods' for p in self.root.parts):
            raise ValueError('Edit the source outside Mods; build a separate runtime package afterwards')
        validate(self.root)
        if (self.root/'portrait-studio-managed.json').is_file():
            from managed_validate import validate as validate_managed
            validate_managed(self.root)
        here = Path(__file__).resolve().parent.parent
        self.schema = json.loads(next(p for p in (here/'protocol/portrait-studio-static-v1.schema.json', here/'references/portrait-studio-static-v1.schema.json') if p.is_file()).read_text(encoding='utf-8'))

    def snapshot(self):
        data = (self.root/MARKER).read_bytes()
        return dict(baseHash=sha(data), catalog=json.loads(data.decode('utf-8-sig')))

    def image(self, path):
        catalog = self.snapshot()['catalog']
        allowed = {a['path'] for s in catalog['resourceSets'] for a in s['resources']}
        if path not in allowed:
            raise ValueError('Only declared artwork can be read')
        return inside(self.root, path).read_bytes()

    def save(self, request):
        state = self.snapshot()
        if request.get('baseHash') != state['baseHash']:
            raise ValueError('清单已被其他程序修改，请重新载入再调整')
        edits = request.get('edits')
        if not isinstance(edits, list) or len(edits) > 16384:
            raise ValueError('Invalid edits')
        catalog = state['catalog']
        index = {(s['id'], a['id'], a['resourceRevision']): a for s in catalog['resourceSets'] for a in s['resources']}
        replacements, seen = {}, set()
        for edit in edits:
            key = (edit['setId'], edit['id'], edit['revision'])
            if key in seen or key not in index:
                raise ValueError('Unknown or repeated resource')
            seen.add(key)
            asset = index[key]
            g = edit['geometry']
            # This editor changes placement, never crops or changes slicing metadata.
            old = asset['geometry']; t = asset['texture']
            full = dict(x=0, y=0, width=t['width'], height=t['height'])
            if g.get('rect') not in (old['rect'], full) or g.get('border') != old['border'] or g.get('meshType') != old['meshType']:
                raise ValueError('Only placement and restoring the full image are supported')
            if g == old:
                continue
            asset['geometry'] = copy.deepcopy(g)
            revision = 'frame-' + sha(json.dumps([t['contentHash'], g], sort_keys=True, allow_nan=False).encode())[:16]
            asset['resourceRevision'] = revision
            replacements[(catalog['providerId'], *key)] = revision
        if not replacements:
            return dict(changed=0, **state)

        def update_refs(value):
            if isinstance(value, dict):
                if all(k in value for k in ('providerId', 'setId', 'id', 'revision')):
                    revision = replacements.get(tuple(value[k] for k in ('providerId', 'setId', 'id', 'revision')))
                    if revision:
                        value['revision'] = revision
                for child in value.values():
                    update_refs(child)
            elif isinstance(value, list):
                for child in value:
                    update_refs(child)

        update_refs(catalog)
        stamp = 'frame-' + sha(json.dumps(catalog, sort_keys=True, allow_nan=False).encode())[:16]
        catalog['catalogRevision'] = stamp
        if 'revision' in catalog:
            catalog['revision'] = stamp
        schema_check(catalog, self.schema, self.schema)
        for group in catalog['resourceSets']:
            for a in group['resources']:
                g, t = a['geometry'], a['texture']; r, b = g['rect'], g['border']
                if r['x']+r['width'] > t['width'] or r['y']+r['height'] > t['height'] or b['left']+b['right'] > r['width'] or b['bottom']+b['top'] > r['height']:
                    raise ValueError('Geometry exceeds image bounds')

        writes = {self.root/MARKER: catalog}
        managed = self.root/'portrait-studio-managed.json'
        if managed.is_file():
            m = json.loads(managed.read_text(encoding='utf-8-sig'))
            update_refs(m); m['revision'] = stamp; writes[managed] = m
        previous = {p: p.read_bytes() for p in writes}
        # Recheck both documents before committing. Backups stay in source, outside runtime.
        if sha(previous[self.root/MARKER]) != request['baseHash']:
            raise ValueError('Catalog changed while saving')
        backup = self.root/'.portrait-framing'/str(time.time_ns())
        backup.mkdir(parents=True)
        for p, data in previous.items():
            (backup/p.name).write_bytes(data)
        try:
            for p, value in writes.items():
                temporary = p.with_suffix(p.suffix+'.framing-tmp')
                temporary.write_text(json.dumps(value, ensure_ascii=False, indent=2, allow_nan=False)+'\n', encoding='utf-8')
                temporary.replace(p)
            validate(self.root)
            if managed.is_file():
                from managed_validate import validate as validate_managed
                validate_managed(self.root)
        except Exception:
            for p, data in previous.items():
                p.write_bytes(data)
            raise
        return dict(changed=len(replacements), **self.snapshot())


def serve(editor, page, port=0, timeout=1800, open_browser=True):
    token = secrets.token_urlsafe(32)

    class Handler(BaseHTTPRequestHandler):
        def reply(self, code, data, kind='application/json; charset=utf-8'):
            if not isinstance(data, bytes):
                data = json.dumps(data, ensure_ascii=False, allow_nan=False).encode()
            self.send_response(code)
            self.send_header('Content-Type', kind)
            self.send_header('Content-Length', str(len(data)))
            self.send_header('Cache-Control', 'no-store')
            self.send_header('X-Content-Type-Options', 'nosniff')
            self.end_headers(); self.wfile.write(data)

        def authorized(self):
            from urllib.parse import urlsplit
            host = self.headers.get('Host')
            origin = self.headers.get('Origin')
            return host == f'127.0.0.1:{self.server.server_port}' and (not origin or origin == 'http://'+host) and self.headers.get('X-Framing-Token') == token

        def do_GET(self):
            from urllib.parse import parse_qs, urlsplit
            url = urlsplit(self.path)
            if url.path == '/':
                if self.headers.get('Host') != f'127.0.0.1:{self.server.server_port}':
                    self.reply(403, dict(error='Local host required')); return
                self.reply(200, page.read_text(encoding='utf-8').replace('__TOKEN__', token).encode(), 'text/html; charset=utf-8'); return
            if not self.authorized():
                self.reply(403, dict(error='Local editor token required')); return
            try:
                if url.path == '/catalog':
                    self.reply(200, editor.snapshot())
                elif url.path == '/image':
                    self.reply(200, editor.image(parse_qs(url.query)['path'][0]), 'image/png')
                else:
                    self.reply(404, dict(error='Not found'))
            except (ValueError, OSError, KeyError) as error:
                self.reply(400, dict(error=str(error)))

        def do_POST(self):
            if not self.authorized() or self.path != '/save':
                self.reply(403, dict(error='Local editor token required')); return
            try:
                size = int(self.headers.get('Content-Length', '0'))
                if not 0 < size <= 4*1024*1024:
                    raise ValueError('Request size rejected')
                request = json.loads(self.rfile.read(size), parse_constant=lambda v: (_ for _ in ()).throw(ValueError('Finite numbers required')))
                self.reply(200, editor.save(request))
            except (ValueError, OSError, KeyError, TypeError) as error:
                self.reply(400, dict(error=str(error)))

        def log_message(self, *args):
            pass

    server = HTTPServer(('127.0.0.1', port), Handler)
    server.timeout = .5
    server.socket.settimeout(2)
    url = f'http://127.0.0.1:{server.server_port}/'
    print('构图调整：'+url+' （仅本机；Ctrl+C 结束）', flush=True)
    if open_browser:
        webbrowser.open(url)
    deadline = time.monotonic()+timeout
    try:
        while time.monotonic() < deadline:
            server.handle_request()
    except KeyboardInterrupt:
        pass
    finally:
        server.server_close()


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--source', required=True, type=Path)
    p.add_argument('--port', type=int, default=0)
    p.add_argument('--timeout', type=int, default=1800)
    p.add_argument('--no-browser', action='store_true')
    args = p.parse_args()
    page = Path(__file__).resolve().parent/'framing.html'
    if not page.is_file():
        p.error('Keep framing.html alongside framing.py')
    if not 1 <= args.timeout <= 43200:
        p.error('Timeout must be 1–43200 seconds')
    serve(Editor(args.source), page, args.port, args.timeout, not args.no_browser)


if __name__ == '__main__':
    main()
