"""One-time migration cleanup. Default: preflight only; --apply writes receipt."""
import collections
import csv
import hashlib
import json
from pathlib import Path
import stat
import sys

OLD = Path(r'D:\0TavernWeave\probably-stolen')
NEW = Path(__file__).resolve().parents[2]
SHARED = Path(__file__).resolve().parents[1]
G = Path(r'D:\steam\steamapps\common\Probably Stolen')
SAVES = Path(r'C:\Users\qianxia1\AppData\LocalLow\Questing Goose Studio\Probably Stolen')
PLACEHOLDER = OLD.parent / 'ArcadeProbe2.cs'
PROTECTED = {
    SAVES / 'save_0.es3': '87df36a4aff162023a98c317e38165584d843648d7336de80198af2c0c91be9d',
    SAVES / 'save_1.es3': '574127ffe89cc8686f565a118f8261f6da0cb54f6d907fdb40c59db7643e2ab4',
    G / 'Mods/DeepSpacePawn/DeepSpacePawn.dll': '5fae5f9dbba6ebeff5d40b7c75f7e7044538a2d23a6237071e1ff5aff3fc8eeb',
    G / 'Mods/DeepSpaceArcade/DeepSpaceArcade.dll': '1359620c18061b811634440c52f29eca141786742b935c98755c6661323c00d4',
}
KEEP = ('_backup', 'books_bak', 'old_mapped_dlls')
assert __debug__, 'Do not run with -O'
assert OLD.resolve(strict=True) == OLD and NEW == Path(r'D:\0深空当铺模组开发')


def digest(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()


def check(p, boundary):
    assert p.is_relative_to(boundary) and p != boundary and p.resolve(strict=True) == p and p.is_file(), p
    for node in (p, *p.parents):
        assert not node.lstat().st_file_attributes & stat.FILE_ATTRIBUTE_REPARSE_POINT, node
        if node == boundary:
            break


for protected, expected in PROTECTED.items():
    assert digest(protected) == expected, protected
receipt = SHARED / 'migration-cleanup.csv'
assert not receipt.exists(), 'One-time cleanup already has a receipt'
with (SHARED / 'migration-copy.csv').open(encoding='utf-8-sig', newline='') as stream:
    rows = list(csv.DictReader(stream))
assert len(rows) == 2059
eligible = [r for r in rows if r['old_copy_eligible'] == '1']
assert len(eligible) == 940
items = {}
archives = {}
for r in eligible:
    p = Path(r['source'])
    dst = NEW / r['destination_relative_to_new_root']
    check(p, OLD)
    rel = p.relative_to(OLD)
    assert rel.parts[0] in {'assets', 'build', 'drafts', 'mods', 'notes', 'probes', 'shots', 'tmp'}, p
    assert not any(x.lower().startswith(KEEP) for x in rel.parts), p
    assert p.suffix.lower() not in {'.es3', '.sav', '.bak'}, p
    check(dst, NEW)
    size, sha = int(r['bytes']), r['sha256_at_copy']
    assert p.stat().st_size == dst.stat().st_size == size and digest(p) == digest(dst) == sha, p
    assert p not in items
    items[p] = ('verified_old_copy', sha, size, dst)
    if dst.suffix.lower() == '.dll':
        archives[sha] = dst


def scratch(p, why, dst=None):
    check(p, OLD if p != PLACEHOLDER else OLD.parent)
    assert p not in items
    items[p] = (why, digest(p), p.stat().st_size, dst)


args = OLD / 'build/mcp-args'
names = ('click-load click-loadlatest click-save click close empty gid load load10 load8 load8b load9 loc loc2 '
         'probe q qi qi2 qui quit ray reload session').split() + [f'reload{i}' for i in range(3, 22)]
allowed_arg = set('world_id action target_ref session_id item_ref include_tags filename table keys command root recursive max_depth include_semantics text_contains include_inactive x y mod_id'.split())
for name in names:
    f = args / (name + '.json')
    assert set(json.loads(f.read_bytes())) <= allowed_arg, f
    scratch(f, 'old_runtime_request')
for name, length in [('ui-front.json', 0), ('sess.txt', 65), ('world.txt', 32)]:
    f = args / name
    assert f.stat().st_size == length
    scratch(f, 'old_session_or_empty')
for rel, keys in [
    ('mods/arcade/_test/session.json', {'SID', 'WID', 'PROBE'}),
    ('mods/arcade/_test/session_fixed.json', {'SID', 'WID', 'PROBE'}),
    ('mods/arcade/_test/session_v3.json', {'SID', 'WID', 'PROBE'}),
    ('mods/freya/_debug_archive/session_ids.json', {'SID', 'WID'}),
    ('mcp-log/arg-close1.json', {'session_id'}),
    ('mcp-log/ui10.json', {'limit'}),
]:
    f = OLD / rel
    assert set(json.loads(f.read_bytes())) == keys, f
    scratch(f, 'old_session_or_request')
allowed_query = set('view max_nodes limit text_contains ui_ref world_id level contains table keys include_inactive name_contains offset'.split())
for name in 'f1 f2 glog glog2 glog3 loc_dlg loc_types p1 p2 ui_all ui_find ui_find2 ui_mods2 ui_mods3 ui_mods_all ui_sb'.split():
    f = OLD / 'tmp' / (name + '.json')
    assert set(json.loads(f.read_bytes())) <= allowed_query, f
    scratch(f, 'old_runtime_request')
output = OLD / 'tmp/ui_all_out.json'
assert 'referenceMaxLifetimeSeconds' in json.loads(output.read_bytes())['result']
scratch(output, 'transient_ui_reference_dump')
assert PLACEHOLDER.stat().st_size == 0
scratch(PLACEHOLDER, 'empty_placeholder')
obj = OLD / 'mods/arcade/obj'
for name in ('DeepSpaceArcade.csproj.nuget.dgspec.json', 'DeepSpaceArcade.csproj.nuget.g.props', 'DeepSpaceArcade.csproj.nuget.g.targets', 'project.assets.json', 'project.nuget.cache'):
    scratch(obj / name, 'generated_msbuild_cache')
for folder in ['Release', *(f'r{i}' for i in range(1, 13))]:
    for name in ('.NETFramework,Version=v4.7.2.AssemblyAttributes.cs', 'DeepSpaceArcade.assets.cache', 'DeepSpaceArcade.csproj.AssemblyReference.cache', 'DeepSpaceArcade.csproj.CoreCompileInputs.cache', 'DeepSpaceArcade.csproj.FileListAbsolute.txt', 'DeepSpaceArcade.GeneratedMSBuildEditorConfig.editorconfig'):
        scratch(obj / folder / name, 'generated_msbuild_cache')
    filename = f'DeepSpaceArcade_v020_{folder}.dll' if folder != 'Release' else 'DeepSpaceArcade.dll'
    p = obj / folder / filename
    if (match := archives.get(digest(p))) is not None:
        scratch(p, 'generated_dll_with_verified_copy', match)
    else:
        print('retain unique obj binary:', p.relative_to(OLD))

print('preflight', len(items), 'files;', sum(item[2] for item in items.values()), 'bytes;', dict(collections.Counter(item[0] for item in items.values())))
print('JSON candidates:', sum(p.suffix.lower() == '.json' for p in items))
assert sys.argv[1:] in ([], ['--apply'])
if sys.argv[1:]:
    with receipt.open('x', encoding='utf-8-sig', newline='') as stream:
        writer = csv.writer(stream)
        writer.writerow(['path', 'reason', 'sha256', 'bytes', 'verified_copy', 'outcome'])
        results = collections.Counter()
        for p, (why, sha, size, dst) in items.items():
            try:
                check(p, OLD if p != PLACEHOLDER else OLD.parent)
                assert p.stat().st_size == size and digest(p) == sha
                if dst is not None:
                    check(dst, NEW)
                    assert dst.stat().st_size == size and digest(dst) == sha
                p.unlink()
                outcome = 'removed'
            except (OSError, AssertionError):
                outcome = 'skipped-change-or-io-error'
            writer.writerow([p, why, sha, size, dst or '', outcome])
            stream.flush()
            results[outcome] += 1
    for protected, expected in PROTECTED.items():
        assert digest(protected) == expected, protected
    print('actual:', dict(results), '; installed DLLs and saves match original hashes')
    assert results['removed'] == len(items), 'See cleanup receipt for skips'
