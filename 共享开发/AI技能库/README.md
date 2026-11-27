# AI 技能库

给这个工作区用的 AI 技能集合。目前只有一套：`matt-pocock/`，是 [mattpocock/skills](https://github.com/mattpocock/skills) 的中文适配版，共 27 个技能。

## 目录

| 路径 | 用途 |
| --- | --- |
| `matt-pocock/` | 27 个技能，一个技能一个目录，中文正文 + `*.en.md` 英文基底 |
| `matt-pocock/README.md` | 技能库说明、安装、更新上游 |
| `matt-pocock/_本工作区落地.md` | 这些技能怎么接到我们的构建、回归、工单和斜杠命令上 |
| `matt-pocock/评估-对深空当铺模组开发.md` | 逐个技能对本项目的价值判断，含不建议用的那些 |
| `matt-pocock/_翻译规范.md` | 中文化的硬约束，改译文前必读 |
| `matt-pocock/_术语对照.md` | 全库固定译法 |
| `sync-skills.ps1` | 把中文技能同步进 DSH 技能目录 |
| `verify-skills.ps1` | 校验译文与英文基底的结构对齐 |

## 常用命令

```powershell
# 本机只有 Windows PowerShell 5.1（无 pwsh）
# 看会写哪些文件，不动盘
powershell -NoProfile -ExecutionPolicy Bypass -File .\共享开发\AI技能库\sync-skills.ps1 -DryRun

# 实际同步（写 $env:DSH_HOME\skills）
powershell -NoProfile -ExecutionPolicy Bypass -File .\共享开发\AI技能库\sync-skills.ps1

# 校验译文结构对齐
powershell -NoProfile -ExecutionPolicy Bypass -File .\共享开发\AI技能库\verify-skills.ps1
```

改完技能正文或重新翻译上游之后，先跑校验，再跑同步。

## 与其它目录的分工

- `共享开发\模组制作经验库\` 管**事实**：原版 API、踩坑、判定规则、工坊模组蒸馏。
- `共享开发\AI技能库\` 管**方法**：怎么在动手前把需求问清楚，怎么在提交前把代码审干净。

两者不重叠，也不互相替代。技能不知道 `realCondition` 是什么，经验库不会告诉智能体该怎么跟用户对齐需求。
