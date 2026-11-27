# mattpocock/skills 中文适配版

上游：[mattpocock/skills](https://github.com/mattpocock/skills)（MIT 许可，作者 Matt Pocock）。
上游版本：commit `6f7f783`（2026-11-25）。本目录是仓库正式发布集合（`engineering/` + `productivity/` 两桶，共 27 个技能）的中文版。

## 为什么是这 27 个

上游把技能分四桶：`engineering/`、`productivity/`、`misc/`、`in-progress/`。只有前两桶随插件正式发布，也正好覆盖软件开发与通用工作流。另外两桶的情况：

- `misc/`：上游已冻结、不再维护。里面是 TypeScript 类型断言迁移、课程脚手架、Husky pre-commit、Claude Code git 钩子，与本工作区（Unity/Mono C# + 本地 Git）无关。
- `in-progress/`：上游标注为 beta，用于公开征求意见。里面多为写作类（`writing-*`）、Claude CLI 交接、TypeScript 包边界、CI 工厂编排。

这些目录**已经下载**到 `缓存文件夹\mattpocock-skills-src\skills\`，需要时可以随时翻出来看。

## 目录约定

```
matt-pocock/
├─ _翻译规范.md          中文化的硬约束（改译文前必读）
├─ _术语对照.md          全库唯一译法
├─ _本工作区落地.md      这些技能怎么接到我们的构建/回归/工单上
├─ README.md             本文件
└─ <技能名>/
   ├─ SKILL.md           中文技能正文（智能体执行的指令）
   ├─ SKILL.en.md        英文原文（对照基底，不安装）
   ├─ <参考文档>.md      中文参考文档
   └─ <参考文档>.en.md   英文原文
```

英文基底一律带 `.en.md` 后缀，不进 DSH 技能目录。这样译文可以随时对回原文，也不用怕改丢细节。

## 安装

技能装进当前 dsh-dpx 环境的 DSH 技能目录（`$env:DSH_HOME\skills\`）：

```powershell
pwsh -File .\共享开发\AI技能库\sync-skills.ps1 -DryRun   # 先看会写哪些
pwsh -File .\共享开发\AI技能库\sync-skills.ps1           # 实际同步
```

同步完成后，新技能在**下一个会话**的技能清单里生效。当前会话的技能表不会热更新。

## 更新上游

```powershell
cd .\缓存文件夹\mattpocock-skills-src
git pull
```

拉完以后，逐个比对 `skills/<桶>/<技能>/SKILL.md` 与本地 `SKILL.en.md`：有差异的重新翻译，再跑一次同步脚本。注意上游没有 em-dash 规范，我们保留中文标点，不做字符替换。

## 与上游的差异

只做语言层与安装层的改动，技能内容不做本地化改写：

| 项目 | 上游 | 本目录 |
| --- | --- | --- |
| 语言 | 英文 | 中文（代码块、命令、路径、技能名保持英文） |
| 触发方式 | `disable-model-invocation: true` 标记"只能用户触发" | **原样保留，DSH 认这个字段**：16 个技能因此不进模型技能清单，只能靠用户打 `/名字` 唤起；另外 11 个模型可自动调用或用户手动调用 |
| 斜杠命令 | `/grill-me` 等是 Claude Code 命令 | DSH 原生支持 `/技能名` 手势，打出来就能注入技能正文，详见 `_本工作区落地.md` |
| 工单系统 | GitHub / GitLab / 本地 `.scratch/` | 本地 `工单/` 目录，见 `_本工作区落地.md` |
| 工具链命令 | pnpm / tsc / vitest | `Build-Mod.ps1` / `Run-Regression.ps1`，同样见落地说明 |
