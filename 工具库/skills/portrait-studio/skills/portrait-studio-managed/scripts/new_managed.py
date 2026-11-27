"""Create a manager-dependent source image pack; never installs or edits an existing mod."""
import argparse
import hashlib
import json
import re
import shutil
from pathlib import Path
import xml.etree.ElementTree as ET

if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--output', type=Path, required=True)
    p.add_argument('--id', required=True)
    p.add_argument('--name', required=True)
    a = p.parse_args()
    if not re.fullmatch(r'[A-Za-z0-9_.-]{1,160}', a.id) or a.id in ('PortraitStudio', 'PortraitStudio.AuthorKit'):
        p.error('Choose a unique mod ID')
    if a.output.exists():
        p.error('Output must not exist')
    if any(part.lower() == 'mods' for part in a.output.resolve().parts):
        p.error('Create source outside Mods')
    template = Path(__file__).resolve().parent.parent / 'assets/template'
    shutil.copytree(template, a.output, ignore=shutil.ignore_patterns('bin', 'obj', 'artifacts', '__pycache__'))
    path = a.output / 'manifest.xml'
    tree = ET.parse(path)
    tree.find('ID').text = a.id
    tree.find('Name').text = a.name
    identity = 'PortraitPack_' + hashlib.sha256(a.id.encode('utf-8')).hexdigest()
    tree.find('./Assemblies/Assembly').text = identity + '.dll'
    tree.write(path, encoding='utf-8', xml_declaration=True)
    project = a.output / 'PortraitPack.csproj'
    project_tree = ET.parse(project)
    project_tree.find('./PropertyGroup/AssemblyName').text = identity
    project_tree.write(project, encoding='utf-8', xml_declaration=True)
    entry = a.output / 'Entry.cs'
    entry.write_text('namespace ' + identity + '\n{\n' + entry.read_text(encoding='utf-8') + '\n}\n', encoding='utf-8')
    for name in ('portrait-studio-provider.json', 'portrait-studio-managed.json'):
        path = a.output / name
        c = json.loads(path.read_text(encoding='utf-8'))
        old = c['providerId']
        c['providerId'] = a.id
        for group in c.get('portraitSets', []):
            for ref in group['entries']:
                if ref['providerId'] == old:
                    ref['providerId'] = a.id
        for target in c.get('targets', []):
            for ref in target['candidateResources'] + [target['defaultResource']]:
                if ref['providerId'] == old:
                    ref['providerId'] = a.id
        path.write_text(json.dumps(c, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(a.output.absolute())
