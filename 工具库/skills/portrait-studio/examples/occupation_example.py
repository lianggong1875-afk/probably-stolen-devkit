"""Apply the occupation example to a newly generated, unbuilt template."""
import copy
import json
from pathlib import Path


def apply(source):
    source = Path(source)
    path = source / 'portrait-studio-provider.json'
    catalog = json.loads(path.read_text(encoding='utf-8'))
    managed_path = source / 'portrait-studio-managed.json'
    managed = json.loads(managed_path.read_text(encoding='utf-8')) if managed_path.exists() else None
    targets = managed['targets'] if managed else catalog['targets']
    base_target, base_group = targets[0], catalog['resourceSets'][0]
    new_targets, groups = [], []
    # Same native body, distinct roles; a third role demonstrates shared fallback.
    for ident, key in [('doctor', 'vanilla_client_lowLevelDoctor'),
                       ('chemist', 'vanilla_client_lowerLevelChemist'),
                       ('junker', 'vanilla_client_junker'),
                       ('shared-scientist', 'maleSci1')]:
        t, group = copy.deepcopy(base_target), copy.deepcopy(base_group)
        t['id'] = group['id'] = ident
        t['subject']['key'] = group['subject']['key'] = t['binding']['key'] = key
        for ref in t['candidateResources'] + [t['defaultResource']]:
            ref['setId'] = ident
        if ident == 'chemist':
            t['defaultResource'] = copy.deepcopy(t['candidateResources'][1])
        new_targets.append(t)
        groups.append(group)
    catalog['resourceSets'] = groups
    catalog['portraitSets'] = [dict(id='set-one', name='立绘1', renderMode='static', entries=[t['candidateResources'][0] for t in new_targets]),
                              dict(id='set-two', name='立绘2', renderMode='static', entries=[t['candidateResources'][1] for t in new_targets])]
    if managed:
        managed['targets'] = new_targets
        managed_path.write_text(json.dumps(managed, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    else:
        catalog['targets'] = new_targets
    path.write_text(json.dumps(catalog, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
    declared = {a['path'] for g in groups for a in g['resources']}
    # Only clean the fresh template's unused example PNGs before building.
    for directory in ('Portraits', 'WantedPortraits', 'SceneBackgrounds'):
        for png in (source/directory).rglob('*.png'):
            if png.relative_to(source).as_posix() not in declared:
                png.unlink()


if __name__ == '__main__':
    import argparse
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('source', type=Path)
    apply(parser.parse_args().source)
