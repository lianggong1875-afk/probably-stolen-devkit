# -*- coding: utf-8 -*-
"""从反编译出来的 .cs 里提取公开 API 清单。

用途：研读别人的模组时快速拿到"它到底提供了什么接口"，而不是逐行翻几万行。
按类型分组输出：常量、公开静态方法、公开实例方法、属性、嵌套类型。

用法：
  python extract_api.py <文件.cs> [--kind const|static|instance|prop|type] [--type 类型名]
  python extract_api.py <文件.cs> --summary          # 只看类型与方法计数
"""
import argparse
import re
import sys
from collections import OrderedDict

TYPE_RE = re.compile(
    r'^\s*(?:public|internal|private|protected)\s+'
    r'(?:(?:sealed|abstract|static|partial|unsafe|readonly)\s+)*'
    r'(class|struct|interface|enum)\s+(\w+)')
CONST_RE = re.compile(
    r'^\s*(?:public|internal)\s+const\s+([\w<>\[\],\.\?]+)\s+(\w+)\s*=\s*(.+?);\s*$')
STATIC_M_RE = re.compile(
    r'^\s*(?:public|internal)\s+static\s+(?:extern\s+|unsafe\s+)?'
    r'([\w<>\[\],\.\?\s]+?)\s+(\w+)\s*\(([^)]*)')
INST_M_RE = re.compile(
    r'^\s*public\s+(?!static)(?!class|interface|enum|struct|const|readonly\s+static)'
    r'(?:override\s+|virtual\s+|unsafe\s+|extern\s+)?'
    r'([\w<>\[\],\.\?\s]+?)\s+(\w+)\s*\(([^)]*)')
PROP_RE = re.compile(r'^\s*public\s+(?:static\s+)?([\w<>\[\],\.\?]+)\s+(\w+)\s*\{\s*(get|set)')


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('file')
    ap.add_argument('--kind', default=None)
    ap.add_argument('--type', dest='type_name', default=None)
    ap.add_argument('--summary', action='store_true')
    a = ap.parse_args()

    cur_type = '<top>'
    data = OrderedDict()

    with open(a.file, encoding='utf-8-sig', errors='replace') as f:
        for raw in f:
            line = raw.rstrip('\n')
            m = TYPE_RE.match(line)
            if m:
                cur_type = m.group(2)
                data.setdefault(cur_type, {'kind': m.group(1), 'const': [],
                                           'static': [], 'instance': [], 'prop': []})
                continue
            bucket = data.setdefault(cur_type, {'kind': '?', 'const': [],
                                                'static': [], 'instance': [], 'prop': []})
            m = CONST_RE.match(line)
            if m:
                bucket['const'].append((m.group(2), m.group(1), m.group(3).strip()))
                continue
            m = STATIC_M_RE.match(line)
            if m:
                ret = ' '.join(m.group(1).split())
                bucket['static'].append((m.group(2), ret, ' '.join(m.group(3).split())[:100]))
                continue
            m = INST_M_RE.match(line)
            if m:
                ret = ' '.join(m.group(1).split())
                bucket['instance'].append((m.group(2), ret, ' '.join(m.group(3).split())[:100]))
                continue
            m = PROP_RE.match(line)
            if m:
                bucket['prop'].append((m.group(2), ' '.join(m.group(1).split())))

    if a.summary:
        print('%-42s %-9s %5s %7s %9s %5s' % ('type', 'kind', 'const', 'static', 'instance', 'prop'))
        for name, b in data.items():
            if name == '<top>' and not any(b[k] for k in ('const', 'static', 'instance', 'prop')):
                continue
            print('%-42s %-9s %5d %7d %9d %5d'
                  % (name, b['kind'], len(b['const']), len(b['static']),
                     len(b['instance']), len(b['prop'])))
        total = sum(len(b[k]) for b in data.values() for k in ('const', 'static', 'instance', 'prop'))
        print('-- %d types, %d members --' % (len(data), total))
        return

    for name, b in data.items():
        if a.type_name and name != a.type_name:
            continue
        if name == '<top>' and not any(b[k] for k in ('const', 'static', 'instance', 'prop')):
            continue
        print('=' * 78)
        print('%s  (%s)' % (name, b['kind']))
        if b['const'] and (a.kind in (None, 'const')):
            print('  -- const --')
            for n, t, v in b['const']:
                print('    %-38s %-10s = %s' % (n, t, v[:90]))
        if b['static'] and (a.kind in (None, 'static')):
            print('  -- static --')
            for n, t, args in b['static']:
                print('    %-26s %-28s (%s)' % (n, t, args))
        if b['instance'] and (a.kind in (None, 'instance')):
            print('  -- instance --')
            for n, t, args in b['instance']:
                print('    %-26s %-28s (%s)' % (n, t, args))
        if b['prop'] and (a.kind in (None, 'prop')):
            print('  -- property --')
            for n, t in b['prop']:
                print('    %-26s %s' % (n, t))


if __name__ == '__main__':
    main()
