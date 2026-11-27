# -*- coding: utf-8 -*-
"""读/改存档索引里的槽位列表（只动 activeSaveSlotId，其它字节照抄）。

  python slot_index.py show       读当前值
  python slot_index.py only 1     只留 slot 1（进测试档用；改前自动备份原值）
  python slot_index.py restore    把 activeSaveSlotId 还原成备份里的原值

这台机器上已经不再需要收窄索引了：卡面文本过期的问题由 fix_index.py 从根本
修好（preview_<slot> 现在与磁盘存档一致），可以按卡面「第N天 / N信用点」直接
认出槽位。本工具留作应急手段，只在卡面也坏了、界面完全没法辨认时用。

历史坑（务必别再踩）：旧的 restore() 是 shutil.copy2(整文件备份, INDEX) ——
把整个索引回退到备份那一刻，连每个槽的 dayCount/credit/时间戳一起回退，
于是卡面开始显示一个已经不存在的存档状态，点进去的槽和卡面写的对不上。
现在 restore() 只回填 activeSaveSlotId 这一个字段，元数据一个字节都不碰。
"""
import os
import re
import shutil
import sys

SAVE = r"C:\Users\qianxia1\AppData\LocalLow\Questing Goose Studio\Probably Stolen"
INDEX = os.path.join(SAVE, "saves_index.es3")
IDS_BAK = os.path.join(SAVE, "saves_index.es3.bak-slotindex-ids")
LEGACY_BAK = os.path.join(SAVE, "saves_index.es3.bak-slotindex")
PATTERN = re.compile(r'("activeSaveSlotId":\{[^{}]*?"value":\[)([0-9,]*)(\])', re.S)


def read_text():
    with open(INDEX, "r", encoding="utf-8-sig") as f:
        return f.read()


def write_text(t):
    with open(INDEX, "w", encoding="utf-8", newline="") as f:
        f.write(t)


def current_ids():
    m = PATTERN.search(read_text())
    return m


def show():
    t = read_text()
    m = PATTERN.search(t)
    print("size=%d activeSaveSlotId=%s" % (len(t.encode("utf-8")), m.group(2) if m else "?"))
    if not m:
        print("pattern not found; header:", t[:200])
    for label, path in (("ids backup", IDS_BAK), ("legacy whole-file backup", LEGACY_BAK)):
        print("  %-27s %s" % (label, "present" if os.path.exists(path) else "-"))


def only(ids):
    m = current_ids()
    if not m:
        raise SystemExit("activeSaveSlotId 没找到，别乱改")
    if not os.path.exists(IDS_BAK):
        with open(IDS_BAK, "w", encoding="utf-8", newline="") as f:
            f.write(m.group(2))
        print("saved original ids ->", os.path.basename(IDS_BAK), "=[%s]" % m.group(2))
    t = read_text()
    new_val = ",".join(str(i) for i in ids)
    write_text(t[:m.start(2)] + new_val + t[m.end(2):])
    print("activeSaveSlotId %s -> [%s]" % (m.group(2), new_val))
    show()


def restore():
    original = None
    if os.path.exists(IDS_BAK):
        with open(IDS_BAK, "r", encoding="utf-8") as f:
            original = f.read().strip()
    elif os.path.exists(LEGACY_BAK):
        # Only mine the original id list out of a legacy whole-file backup.
        # Never copy that file back: it would roll back every preview too.
        with open(LEGACY_BAK, "r", encoding="utf-8-sig") as f:
            m = PATTERN.search(f.read())
        if not m:
            raise SystemExit("legacy backup has no activeSaveSlotId either")
        original = m.group(2)
        print("(recovered ids from legacy backup)")
    if original is None:
        raise SystemExit("没有 id 备份可还原")
    m = current_ids()
    if not m:
        raise SystemExit("activeSaveSlotId 没找到，不动手")
    t = read_text()
    write_text(t[:m.start(2)] + original + t[m.end(2):])
    print("activeSaveSlotId %s -> [%s] (metadata untouched)" % (m.group(2), original))
    show()


def main():
    cmd = sys.argv[1] if len(sys.argv) > 1 else "show"
    if cmd == "show":
        show()
    elif cmd == "only":
        only([int(x) for x in sys.argv[2:]])
    elif cmd == "restore":
        restore()
    else:
        raise SystemExit("用法: show | only <id...> | restore")


if __name__ == "__main__":
    main()
