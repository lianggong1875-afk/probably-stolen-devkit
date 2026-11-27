"""Build two author examples outside Mods. Does not install or launch a game."""
import argparse
import json
import os
from pathlib import Path
import subprocess
import sys
from occupation_example import apply as apply_occupations


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--game-root', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--occupations', action='store_true', help='Build occupation identities and shared-image fallback examples')
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    output = args.output.resolve()
    if output.exists():
        parser.error('Output must not exist')
    if output.is_relative_to(root) or any(p.lower() == 'mods' for p in output.parts):
        parser.error('Build outside the author kit and Mods')
    game = args.game_root.resolve()
    if not (game / 'Probably Stolen_Data/Managed/Assembly-CSharp.dll').is_file():
        parser.error('GameRoot must contain the target game assemblies')
    skills = root / 'skills' if (root / 'skills').is_dir() else root
    output.mkdir(parents=True)
    env = os.environ.copy()
    env['PYTHONDONTWRITEBYTECODE'] = '1'
    rows = []
    for skill, create, build, ident, name in [
        ('portrait-studio-pack', 'new_pack.py', 'build.py', 'example.portraitpack', '独立静态立绘范例'),
        ('portrait-studio-managed', 'new_managed.py', 'build_managed.py', 'example.managedportraits', '管理器托管静态立绘范例'),
    ]:
        if args.occupations:
            ident += '.occupations'
            name += '（职业）'
        scripts = skills / skill / 'scripts'
        source, package = output / 'source' / ident, output / 'runtime' / ident
        commands = [
            [sys.executable, '-B', str(scripts / create), '--output', str(source), '--id', ident, '--name', name],
            [sys.executable, '-B', str(scripts / build), '--game-root', str(game), '--source', str(source), '--output', str(package)],
        ]
        for index, command in enumerate(commands):
            if index == 1 and args.occupations:
                apply_occupations(source)
            result = subprocess.run(command, stdout=subprocess.PIPE, stderr=subprocess.STDOUT,
                                    text=True, encoding='utf-8', errors='replace', env=env, timeout=240)
            (output / f'{ident}-{index}.log').write_text(result.stdout, encoding='utf-8')
            if result.returncode:
                raise RuntimeError(f'{ident} failed; read {output / f"{ident}-{index}.log"}')
        rows.append(dict(providerId=ident, source=str(source), runtime=str(package)))
        print(f'Built {ident}: {package}', flush=True)
    (output / 'examples.json').write_text(json.dumps(dict(examples=rows, installed=False, gameLaunched=False),
                                                     ensure_ascii=False, indent=2), encoding='utf-8')


if __name__ == '__main__':
    main()
