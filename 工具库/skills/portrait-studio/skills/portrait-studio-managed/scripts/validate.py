"""Offline static-v1 package validator. Standard library only; never loads a mod DLL."""
import argparse
import csv
from functools import lru_cache
import hashlib
import json
import math
import re
import struct
import zlib
from pathlib import Path
import xml.etree.ElementTree as ET

FORMAT = 'portrait-studio-static/1'
MARKER = 'portrait-studio-provider.json'

def require(ok, message):
    if not ok:
        raise ValueError(message)

def inside(root, relative):
    require(isinstance(relative, str) and relative and '\\' not in relative and ':' not in relative,
            'Use relative slash-separated paths')
    parts = relative.split('/')
    require(not any(p in ('', '.', '..') for p in parts), 'Unsafe path')
    p = root.joinpath(*parts)
    require(p.resolve().is_relative_to(root.resolve()), 'Path escaped package')
    require(not any(q.is_symlink() or (hasattr(q, 'is_junction') and q.is_junction()) for q in (p, *p.parents)), 'Linked path rejected')
    return p

def png(path):
    require(path.stat().st_size <= 33554432, 'PNG exceeds 32 MiB')
    data = path.read_bytes()
    require(data[:8] == b'\x89PNG\r\n\x1a\n', 'Invalid PNG signature')
    pos, chunks, compressed, wh, seen_end = 8, [], bytearray(), None, False
    while pos < len(data):
        require(pos + 12 <= len(data), 'Truncated PNG')
        size = struct.unpack('>I', data[pos:pos+4])[0]
        kind, payload = data[pos+4:pos+8], data[pos+8:pos+8+size]
        require(pos + size + 12 <= len(data), 'Truncated PNG chunk')
        require(zlib.crc32(kind + payload) & 0xffffffff == struct.unpack('>I', data[pos+8+size:pos+12+size])[0], 'PNG CRC mismatch')
        require(kind not in (b'acTL', b'fcTL', b'fdAT'), 'Animated PNG rejected')
        if kind == b'IHDR':
            require(not chunks and len(payload) == 13, 'Invalid IHDR')
            w, h, depth, color, compression, filtering, interlace = struct.unpack('>IIBBBBB', payload)
            require(0 < w <= 8192 and 0 < h <= 8192 and w*h <= 16777216, 'PNG dimensions rejected')
            require(color in (0, 2, 3, 4, 6) and depth in ({0:(1,2,4,8,16),2:(8,16),3:(1,2,4,8),4:(8,16),6:(8,16)}[color]), 'Invalid PNG color/depth')
            require(compression == filtering == 0 and interlace in (0,1), 'Invalid PNG encoding')
            wh = (w,h)
        if kind == b'IDAT': compressed.extend(payload)
        chunks.append(kind)
        pos += size + 12
        if kind == b'IEND':
            require(size == 0 and pos == len(data), 'Trailing PNG data')
            seen_end = True
            break
    require(wh and seen_end and compressed, 'Incomplete PNG')
    decoder = zlib.decompressobj()
    raw = decoder.decompress(compressed, 150_000_001)
    require(len(raw) <= 150_000_000 and decoder.eof and not decoder.unused_data, 'Invalid/oversized PNG stream')
    if interlace == 0:
        channels = {0:1,2:3,3:1,4:2,6:4}[color]
        stride = (w * channels * depth + 7)//8 + 1
        require(len(raw) == stride * h and all(raw[y*stride] <= 4 for y in range(h)), 'Invalid PNG scanlines')
    return wh

def schema_check(value, schema, root_schema):
    if '$ref' in schema:
        node=root_schema
        for key in schema['$ref'].split('/')[1:]: node=node[key]
        schema_check(value,node,root_schema)
        return
    if 'const' in schema: require(value==schema['const'], 'Wrong constant')
    if 'enum' in schema: require(value in schema['enum'], 'Unexpected value: '+repr(value))
    if 'type' in schema:
        matches={'object':isinstance(value,dict),'array':isinstance(value,list),'string':isinstance(value,str),'integer':type(value)==int,'number':type(value) in (int,float) and math.isfinite(value),'boolean':type(value)==bool}
        require(matches[schema['type']], 'Wrong type: '+schema['type'])
    if isinstance(value,dict):
        require(all(k in value for k in schema.get('required',[])), 'Missing required field: '+repr(schema.get('required')))
        props=schema.get('properties',{})
        if schema.get('additionalProperties') is False: require(not set(value)-set(props), 'Unexpected fields: '+repr(set(value)-set(props)))
        for k,v in value.items():
            if k in props: schema_check(v,props[k],root_schema)
    if isinstance(value,list):
        require(len(value)>=schema.get('minItems',0) and len(value)<=schema.get('maxItems',float('inf')), 'Array size')
        for item in value:
            if 'items' in schema: schema_check(item,schema['items'],root_schema)
    if isinstance(value,str):
        require(len(value)>=schema.get('minLength',0) and len(value)<=schema.get('maxLength',float('inf')), 'String length')
        if 'pattern' in schema: require(re.search(schema['pattern'],value), 'String pattern')
    if type(value) in (int,float):
        require(math.isfinite(value) and value>=schema.get('minimum',-float('inf')) and value<=schema.get('maximum',float('inf')) and value>schema.get('exclusiveMinimum',-float('inf')), 'Number outside range')
    if 'oneOf' in schema:
        count=0
        for option in schema['oneOf']:
            try: schema_check(value,option,root_schema); count+=1
            except ValueError: pass
        require(count==1, 'Expected exactly one supported structure')
    for option in schema.get('allOf',[]): schema_check(value,option,root_schema)
    if 'not' in schema:
        try: schema_check(value,schema['not'],root_schema)
        except ValueError: pass
        else: raise ValueError('Forbidden fields/structure')
    if 'if' in schema:
        try: schema_check(value,schema['if'],root_schema); condition=True
        except ValueError: condition=False
        branch='then' if condition else 'else'
        if branch in schema: schema_check(value,schema[branch],root_schema)

def unique_object(pairs):
    obj={}
    for k,v in pairs:
        require(k not in obj, 'Duplicate JSON property: '+k)
        obj[k]=v
    return obj

def identity(obj):
    sub=obj['subject']
    if sub['owner'] == 'game' and sub['key'].startswith('vanilla_client_'):
        require(sub['key'] in occupation_keys(), 'Unknown occupation key; use canonical key from vanilla-client-roles.tsv')
        require(obj['kind'] == 'character' and obj['slot'] == 'main', 'Occupation requires character/main')
    return obj['kind'],sub['owner'],sub['key'],obj['slot']

@lru_cache(maxsize=1)
def occupation_keys():
    here = Path(__file__).resolve().parent.parent
    path = next(p for p in (here/'protocol/vanilla-client-roles.tsv', here/'references/vanilla-client-roles.tsv') if p.is_file())
    with path.open(encoding='utf-8', newline='') as stream:
        return frozenset(row['key'] for row in csv.DictReader(stream, delimiter='\t'))

def reference_key(ref):
    return tuple(ref[k] for k in ('providerId','setId','id','revision'))

def validate_portrait_sets(c, targets=None):
    sets = {s['id']: s for s in c['resourceSets']}
    ids = set()
    by_identity = {identity(t): t for t in targets} if targets is not None else None
    for collection in c.get('portraitSets', []):
        require(collection['id'] not in ids and collection['name'].strip(), 'Duplicate portrait set ID or blank name')
        ids.add(collection['id'])
        seen = set()
        for ref in collection['entries']:
            require(ref['providerId'] == c['providerId'] and ref['setId'] in sets, 'Portrait set resources must be local')
            group = sets[ref['setId']]
            require(any(a['id'] == ref['id'] and a['resourceRevision'] == ref['revision'] for a in group['resources']), 'Portrait set resource revision missing')
            key = identity(group)
            require(key not in seen, 'Portrait set contains duplicate display identity')
            seen.add(key)
            if by_identity is not None:
                require(key in by_identity and reference_key(ref) in {reference_key(r) for r in by_identity[key]['candidateResources']}, 'Portrait set entry must be a declared target candidate')

def validate(root, runtime=False):
    root=Path(root).absolute()
    marker=inside(root,MARKER)
    require(marker.stat().st_size<=1048576,'Catalog exceeds 1 MiB')
    c=json.loads(marker.read_text(encoding='utf-8-sig'),object_pairs_hook=unique_object)
    here=Path(__file__).resolve().parent.parent
    candidates=[here/'protocol/portrait-studio-static-v1.schema.json',here/'references/portrait-studio-static-v1.schema.json']
    schema=json.loads(next(p for p in candidates if p.is_file()).read_text(encoding='utf-8'))
    schema_check(c,schema,schema)
    manifest=ET.parse(root/'manifest.xml').getroot()
    require(manifest.findtext('ID')==c['providerId'] and re.fullmatch(r'[A-Za-z0-9_.-]+',c['providerId']), 'Manifest/provider ID mismatch')
    files={MARKER,'manifest.xml'}
    sets={}
    for group in c['resourceSets']:
        identity(group)
        require(group['id'] not in sets,'Duplicate resource set')
        assets={}
        for asset in group['resources']:
            key=(asset['id'],asset['resourceRevision'])
            require(key not in assets,'Duplicate resource revision')
            assets[key]=asset
            t,g=asset['texture'],asset['geometry']
            require(t['width']*t['height']<=16777216,'Texture pixel limit')
            levels=[(asset['path'],t['width'],t['height'],t['contentHash'])]
            mip=t['mipmaps']
            if mip['mode']=='provided':
                w,h=t['width'],t['height']
                for i,m in enumerate(mip['levels'],1):
                    require(w>1 or h>1,'Extra mip level')
                    w,h=max(1,w//2),max(1,h//2)
                    require(m['level']==i and (m['width'],m['height'])==(w,h),'Mip chain dimensions/order')
                    levels.append((m['path'],w,h,m['sha256']))
                require((w,h)==(1,1),'Mip chain must reach 1x1')
            for path,w,h,sha in levels:
                img=inside(root,path)
                require(img.suffix.lower()=='.png','Static PNG required')
                require(png(img)==(w,h),'PNG dimensions mismatch')
                require(hashlib.sha256(img.read_bytes()).hexdigest()==sha,'PNG hash mismatch')
                files.add(path)
            r,b=g['rect'],g['border']
            require(r['x']+r['width']<=t['width'] and r['y']+r['height']<=t['height'],'Sprite rect outside texture')
            require(b['left']+b['right']<=r['width'] and b['bottom']+b['top']<=r['height'],'Border exceeds rect')
        sets[group['id']]=(group,assets)
    ids,identities=set(),set()
    for t in c.get('targets',[]):
        require(t['id'] not in ids and identity(t) not in identities,'Duplicate writer target/identity')
        ids.add(t['id']);identities.add(identity(t))
        b=t['binding'];kind,owner,key,slot=identity(t)
        if owner=='game' and key.startswith('vanilla_client_'): require(b['type']=='sprite-key','Occupation requires sprite-key binding')
        if b['type']!='custom': require(owner=='game' and b.get('key')==key,'Native identity mismatch')
        if b['type']=='sprite-key': require(kind=='character' and slot=='main','Character binding mismatch')
        if b['type']=='wanted-photo': require(kind=='wanted' and slot=='photo' and key in [f'wanted{i}' for i in range(1,10)],'Photo binding mismatch')
        if b['type']=='scene-key': require(kind=='scene' and key in ('dumping_ground','drjackson','drjackson2','commissary') and slot==('travel-card' if key=='drjackson' else 'background'),'Scene binding mismatch')
        refs=t['candidateResources']
        require(len({reference_key(r) for r in refs})==len(refs),'Duplicate candidate reference')
        for ref in refs+([t['defaultResource']] if 'defaultResource' in t else []):
            if ref['providerId']!=c['providerId']: continue
            require(ref['setId'] in sets,'Referenced set missing')
            group,assets=sets[ref['setId']]
            require(identity(group)==identity(t),'Resource display identity mismatch')
            asset=assets.get((ref['id'],ref['revision']))
            require(asset is not None,'Referenced resource revision missing')
            if b['type']=='scene-key':
                rect=asset['geometry']['rect'];require(rect['width']*9==rect['height']*16,'Native scene rect must be 16:9')
    validate_portrait_sets(c, c.get('targets') if c['role'] != 'resources' else None)
    assemblies=[e.text for e in manifest.findall('./Assemblies/Assembly')]
    require((assemblies or c['role']=='resources') and all(x and '/' not in x and '\\' not in x and x.endswith('.dll') for x in assemblies),'Root runtime assembly required for writer')
    files.update(assemblies)
    if runtime:
        require(all(inside(root,f).is_file() for f in files),'Missing runtime file')
        actual={p.relative_to(root).as_posix() for p in root.rglob('*') if p.is_file()}
        require(actual==files,'Runtime whitelist mismatch: '+repr(sorted(actual^files)))
    return c,files

if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('package',type=Path);p.add_argument('--runtime',action='store_true')
    a=p.parse_args()
    try:
        c,files=validate(a.package,a.runtime)
        print(f"PASS {c['providerId']}: {len(c.get('targets',[]))} static targets, {len(files)} declared files")
    except (ValueError,OSError,ET.ParseError,KeyError,TypeError,zlib.error) as e:
        p.exit(1,f'FAIL: {e}\n')
