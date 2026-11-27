"""Build an isolated copy, then stage a runtime whitelist. Never installs or launches."""
import argparse
import shutil
import subprocess
import tempfile
from pathlib import Path
from validate import validate

def main():
    p=argparse.ArgumentParser(description=__doc__)
    p.add_argument('--game-root',required=True,type=Path)
    p.add_argument('--source',required=True,type=Path)
    p.add_argument('--output',required=True,type=Path)
    a=p.parse_args()
    source, output=a.source.resolve(),a.output.absolute()
    if output.exists(): p.error('Output must not exist; existing work is never overwritten')
    if output.resolve().is_relative_to(source): p.error('Output must be outside source')
    c,files=validate(source)
    output.parent.mkdir(parents=True,exist_ok=True)
    # Parent chosen by caller outside Mods; work and obj remain separate from shipped sources.
    if any(part.lower()=='mods' for part in output.parts): p.error('Build outside Mods, install separately when requested')
    with tempfile.TemporaryDirectory(prefix='static-build-',dir=output.parent) as temporary:
        work=Path(temporary)/'src'
        shutil.copytree(source,work,ignore=shutil.ignore_patterns('bin','obj','artifacts','.git','.portrait-framing'))
        projects=list(work.glob('*.csproj'))
        if len(projects)!=1: p.error('Source needs exactly one root csproj')
        subprocess.run(['dotnet','build',str(projects[0]),'-c','Release',f'-p:GameRoot={a.game_root.resolve()}','--nologo'],check=True,timeout=180)
        staged=Path(temporary)/'package'
        staged.mkdir()
        for relative in files:
            origin=work/'artifacts'/'build'/relative if relative.endswith('.dll') else source/relative
            dest=staged/relative
            dest.parent.mkdir(parents=True,exist_ok=True)
            shutil.copy2(origin,dest)
        validate(staged,True)
        shutil.copytree(staged,output)
    print(f'PASS staged {c["providerId"]}: {output}')

if __name__=='__main__': main()
