"""Build a managed-only image pack outside Mods, staging only declared runtime files."""
import argparse
import shutil
import subprocess
import tempfile
from pathlib import Path
from managed_validate import validate


def main():
    p = argparse.ArgumentParser(description=__doc__)
    for name in ('game-root', 'source', 'output'):
        p.add_argument('--' + name, required=True, type=Path)
    a = p.parse_args()
    source, output = a.source.resolve(), a.output.absolute()
    if output.exists() or output.resolve().is_relative_to(source):
        p.error('Use a new output directory outside source')
    if any(part.lower() == 'mods' for path in (source, output.resolve()) for part in path.parts):
        p.error('Source and output must be outside Mods; install separately')
    d, files = validate(source)
    output.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(prefix='managed-build-', dir=output.parent) as tmp:
        work = Path(tmp) / 'src'
        shutil.copytree(source, work, ignore=shutil.ignore_patterns('bin', 'obj', 'artifacts', '.git', '__pycache__'))
        projects = list(work.glob('*.csproj'))
        if len(projects) != 1:
            p.error('Source needs exactly one root csproj')
        subprocess.run(['dotnet', 'build', str(projects[0]), '-c', 'Release', f'-p:GameRoot={a.game_root.resolve()}', '--nologo'], check=True, timeout=180)
        package = Path(tmp) / 'package'
        package.mkdir()
        for relative in files:
            origin = work / 'artifacts/build' / relative if relative.endswith('.dll') else source / relative
            dest = package / relative
            dest.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(origin, dest)
        validate(package, True)
        shutil.copytree(package, output)
    print(f"PASS staged managed {d['providerId']}: {output}")


if __name__ == '__main__':
    main()
