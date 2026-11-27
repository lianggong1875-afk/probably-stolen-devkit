"""Validate explicit manager rendering ownership and local static resources. No DLL is executed."""
import argparse
import json
from pathlib import Path
import xml.etree.ElementTree as ET
from validate import validate as resource_validate, require, inside, schema_check, unique_object, identity, reference_key, validate_portrait_sets

MARKER = 'portrait-studio-managed.json'


def validate(root, runtime=False):
    root = Path(root).absolute()
    c, files = resource_validate(root, False)
    path = inside(root, MARKER)
    require(path.stat().st_size <= 1048576, 'Managed declaration exceeds 1 MiB')
    d = json.loads(path.read_text(encoding='utf-8-sig'), object_pairs_hook=unique_object)
    here = Path(__file__).resolve().parent.parent
    schema = json.loads(next(p for p in [here / 'protocol/portrait-studio-managed.schema.json', here / 'references/portrait-studio-managed.schema.json'] if p.is_file()).read_text(encoding='utf-8'))
    schema_check(d, schema, schema)
    require(c['role'] == 'resources', 'Managed provider must not advertise a writer bridge')
    require(d['providerId'] == c['providerId'], 'Managed/provider identity mismatch')
    manifest = ET.parse(root / 'manifest.xml').getroot()
    require('PortraitStudio' in [e.text for e in manifest.findall('./Prerequisites/Mod')], 'Managed package requires PortraitStudio')
    ids, identities = set(), set()
    sets = {s['id']: s for s in c['resourceSets']}
    for t in d['targets']:
        require(t['id'] not in ids and identity(t) not in identities, 'Duplicate managed target/identity')
        ids.add(t['id'])
        identities.add(identity(t))
        b = t['binding']
        kind, owner, key, slot = identity(t)
        if owner == 'game' and key.startswith('vanilla_client_'):
            require(b['type'] == 'sprite-key', 'Occupation requires sprite-key binding')
        if b['type'] != 'custom':
            require(owner == 'game' and b.get('key') == key, 'Native managed identity mismatch')
        if b['type'] == 'sprite-key':
            require(kind == 'character' and slot == 'main', 'Managed character binding mismatch')
        if b['type'] == 'wanted-photo':
            require(kind == 'wanted' and slot == 'photo' and key in [f'wanted{i}' for i in range(1, 10)], 'Managed photo binding mismatch')
        if b['type'] == 'scene-key':
            require(kind == 'scene' and key in ('dumping_ground', 'drjackson', 'drjackson2', 'commissary') and slot == ('travel-card' if key == 'drjackson' else 'background'), 'Managed scene binding mismatch')
        refs = t['candidateResources']
        keys = [reference_key(r) for r in refs]
        require(len(set(keys)) == len(keys) and reference_key(t['defaultResource']) in keys, 'Managed default must be one of distinct candidates')
        for ref in refs:
            require(ref['providerId'] == c['providerId'] and ref['setId'] in sets, 'Managed resource must be local')
            group = sets[ref['setId']]
            require(identity(t) == identity(group), 'Managed resource display identity mismatch')
            asset = next((a for a in group['resources'] if a['id'] == ref['id'] and a['resourceRevision'] == ref['revision']), None)
            require(asset is not None, 'Managed resource revision missing')
            if b['type'] == 'scene-key':
                require(asset['geometry']['rect']['width'] * 9 == asset['geometry']['rect']['height'] * 16, 'Managed scene rect must be 16:9')
    validate_portrait_sets(c, d['targets'])
    files.add(MARKER)
    if runtime:
        require(all(inside(root, f).is_file() for f in files), 'Missing managed runtime file')
        actual = {p.relative_to(root).as_posix() for p in root.rglob('*') if p.is_file()}
        require(actual == files, 'Managed runtime whitelist mismatch: ' + repr(sorted(actual ^ files)))
    return d, files


if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('package', type=Path)
    p.add_argument('--runtime', action='store_true')
    a = p.parse_args()
    try:
        d, files = validate(a.package, a.runtime)
        print(f"PASS {d['providerId']}: {len(d['targets'])} explicit managed static targets, {len(files)} files")
    except (ValueError, OSError, ET.ParseError, KeyError, TypeError) as e:
        p.exit(1, f'FAIL: {e}\n')
