"""Validate an author's dynamic declaration using only the Python standard library."""
import argparse
import json
from pathlib import Path
import unicodedata
import xml.etree.ElementTree as ET


def check(condition, message):
    if not condition:
        raise ValueError(message)


def pairs(values):
    result = {}
    for key, value in values:
        check(key not in result, '重复字段: '+key)
        result[key] = value
    return result


def fields(value, names):
    check(isinstance(value, dict) and set(value) == set(names.split()), '字段缺失或多余: '+names)


def ident(value):
    check(isinstance(value, str) and value.strip() and len(value.encode('utf-16-le'))//2 <= 160
          and not any(unicodedata.category(c) == 'Cc' for c in value), '身份或名称无效')
    return value


def validate(root):
    root = Path(root)
    file = root/'portrait-studio-dynamic.json'
    check(file.stat().st_size <= 1048576, '清单超过 1 MiB')
    data = json.loads(file.read_text(encoding='utf-8-sig'), object_pairs_hook=pairs)
    fields(data, 'format contract providerId revision renderMode targets')
    check(data['format'] == 'portrait-studio-dynamic/1' and data['contract'] == 'dynamic-display-control/1'
          and data['renderMode'] == 'dynamic', '必须明确声明动态接口')
    provider = ident(data['providerId']); ident(data['revision'])
    manifest = root/'manifest.xml'
    if manifest.exists():
        check(ET.parse(manifest).getroot().findtext('ID') == provider, 'manifest ID 与 providerId 不同')
    targets = data['targets']
    check(isinstance(targets, list) and 1 <= len(targets) <= 512, 'target 数量应为 1～512')
    ids, identities, total = set(), set(), 0
    for target in targets:
        fields(target, 'id kind subject slot renderMode binding layoutOwner defaultCandidate candidates')
        target_id = ident(target['id']); fields(target['subject'], 'owner key'); fields(target['binding'], 'type key')
        owner, key = ident(target['subject']['owner']), ident(target['subject']['key'])
        kind, slot, binding = target['kind'], target['slot'], target['binding']['type']
        check(target_id not in ids, '重复 target ID'); ids.add(target_id)
        check(owner in ('game', provider) and target['binding']['key'] == key, '创建者或 binding key 错误')
        check((owner == 'game') == (binding != 'custom'), '自定义身份必须用 custom binding')
        valid = {('character', 'main'): {'sprite-key', 'client-role', 'custom'},
                 ('wanted', 'photo'): {'wanted-key', 'custom'}, ('scene', 'background'): {'scene-key', 'custom'}}
        check(binding in valid.get((kind, slot), set()), 'kind/slot/binding 不匹配')
        check(target['renderMode'] == 'dynamic' and target['layoutOwner'] == 'provider', '动态位置由作者负责布局')
        identity = (kind, owner, key, slot); check(identity not in identities, '重复显示身份'); identities.add(identity)
        candidates = target['candidates']; check(isinstance(candidates, list) and 1 <= len(candidates) <= 256, '候选数量应为 1～256')
        candidate_ids = set()
        for candidate in candidates:
            fields(candidate, 'id label'); cid = ident(candidate['id']); ident(candidate['label'])
            check(cid not in candidate_ids, '重复候选'); candidate_ids.add(cid)
        check(target['defaultCandidate'] in candidate_ids, '默认候选不存在')
        total += len(candidates); check(total <= 4096, '总候选超过 4096')
    for name, field in [('portrait-studio-provider.json', 'targets'), ('portrait-studio-managed.json', 'targets')]:
        file = root/name
        if not file.exists():
            continue
        static = json.loads(file.read_text(encoding='utf-8-sig'), object_pairs_hook=pairs)
        for target in static.get(field, []):
            subject = target.get('subject', {})
            check((target.get('kind'), subject.get('owner'), subject.get('key'), target.get('slot')) not in identities,
                  '同一位置同时声明静态 writer/托管与动态；请拆开显示范围')
    return data


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('source', type=Path)
    args = parser.parse_args()
    try:
        data = validate(args.source)
    except (ValueError, OSError, ET.ParseError) as error:
        parser.exit(1, str(error)+'\n')
    print(f"PASS {data['providerId']}: {len(data['targets'])} dynamic targets")
