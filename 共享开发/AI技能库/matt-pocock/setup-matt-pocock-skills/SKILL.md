---
name: setup-matt-pocock-skills
description: "为工程类技能配置本仓库：设置工单系统（issue tracker）、分诊标签（triage label）词表与领域文档（domain doc）布局。首次使用其他工程类技能之前运行一次。"
disable-model-invocation: true
---

# 配置 Matt Pocock 的技能

搭出工程类技能所预设的按仓库配置：

- **工单系统**：工单存放的位置（默认 GitHub；本地 markdown 也开箱即可用）
- **分诊标签**：五个标准分诊角色所用的字符串
- **领域文档**：`GLOSSARY.md` 与 ADR 存放的位置，以及读取它们的消费规则

这是提示词驱动的技能，不是确定性脚本。先探查，把发现摆出来，与用户确认，然后才写入。

## 流程

### 1. 探查

查看当前仓库，弄清它的起点状态。有什么就读什么，不要假设：

- `git remote -v` 与 `.git/config`：这是不是 GitHub 仓库？是哪一个？
- 仓库根目录的 `AGENTS.md` 与 `CLAUDE.md`：是否存在其中之一？其中是否已有 `## Agent skills` 小节？
- 仓库根目录的 `GLOSSARY.md` 与 `GLOSSARY-MAP.md`，以及遗留的 `CONTEXT.md` / `CONTEXT-MAP.md`。若有遗留文件，提出用 `git mv` 把它改成对应的 `GLOSSARY` 名称，并在 `docs/agents/domain.md` 里改写这些名称。
- `docs/adr/` 以及任何 `src/*/docs/adr/` 目录
- `docs/agents/`：本技能此前的产物是否已经存在？
- `.scratch/`：这是本地 markdown 工单系统约定已在使用的迹象
- `triage` 技能是否已安装？（与本技能同级的 `triage` 技能目录，或你的可用技能列表里有 `triage`。）这决定 B 小节是否执行。
- 多包仓库线索：`pnpm-workspace.yaml`、`package.json` 里的 `workspaces` 字段，或装着成规模的 `packages/*`（自带 `src/`）。这些只在真正大型的多包仓库里出现；没有这些线索就是单上下文，几乎每个仓库都是如此。

### 2. 摆出发现并提问

概述哪些已存在、哪些缺失。然后按顺序过各小节。一节问一次，得到答复再进下一节。

每节先给推荐答案，让用户一个词就能接受。只有选项确实分岔时才给一句解释；探查已经定了的小节整节跳过（`triage` 未安装时跳过 B 小节，没有多包仓库时跳过 C 小节）。

**A 小节：工单系统。**

> 解释：所谓“工单系统”，就是这个仓库的工单存放的地方。`to-tickets`、`triage`、`to-spec` 这类技能都从它读取、向它写入。它们需要知道是调用 `gh issue create`、在 `.scratch/` 下写一个 markdown 文件，还是走你描述的其他流程。选你实际跟踪本仓库工作的那个地方。

默认姿态：这些技能是按 GitHub 设计的。某个 `git remote` 指向 GitHub 就提议 GitHub。某个 `git remote` 指向 GitLab（`gitlab.com` 或自建主机）就提议 GitLab。其余情况（或用户另有偏好）给出：

- **GitHub**：工单存放在仓库的 GitHub Issues 里（用 `gh` CLI）
- **GitLab**：工单存放在仓库的 GitLab Issues 里（用 [`glab`](https://gitlab.com/gitlab-org/cli) CLI）
- **本地 markdown**：工单以文件形式存放在本仓库 `.scratch/<feature>/` 下（适合个人项目或没有 remote 的仓库）
- **其他**（Jira、Linear 等）：请用户用一段话描述该流程；技能会把它记为自由文本

把选择记录到 `docs/agents/issue-tracker.md`。GitHub 与 GitLab 模板带一个“PR 作为请求面”的开关，默认**关闭**。保持关闭，也不要主动提：想把外部 PR 放进分诊队列的用户，之后可以自己在文件里翻这个开关。

GitHub 或 GitLab 工单系统需要一个可达的仓库。用户选了其中一种、却没有任何 `git remote` 指向该主机时，向用户要仓库 URL 并接好（需要时 `git init`，`git remote add origin <url>`），再往下走。完成标准：在本目录内 `gh repo view` / `glab repo view` 能打印出该项目；打印不出来就停下，告诉用户缺什么。

**B 小节：分诊标签词表。** 若 `triage` 技能未安装（探查已经告诉你），整节跳过，未安装的技能不需要标签。

若已安装，只问一个问题：

> 要保留默认的分诊标签吗？（推荐：**是**）

默认值就是五个标准角色，每个标签字符串与角色名相同：`needs-triage`、`needs-info`、`ready-for-agent`、`ready-for-human`、`wontfix`。回答**是**就照原样写入。只有用户说不（通常是因为他们的工单系统已经在用别的名字，例如用 `bug:triage` 表示 `needs-triage`）时，才收集这些覆盖项，好让 `triage` 套用已有标签而不是造出重复标签。

**C 小节：领域文档。** 默认用**单上下文**（仓库根目录一个 `GLOSSARY.md` + `docs/adr/`）。这适合几乎所有仓库，直接写入，不用问。

只有探查发现多包仓库线索时，才给出**多上下文**选项（根目录一个 `GLOSSARY-MAP.md`，指向各上下文各自的 `GLOSSARY.md`）。然后确认他们要用哪种布局。

### 3. 确认并编辑

给用户看这几项的草稿：

- 要加到 `CLAUDE.md` / `AGENTS.md`（取被编辑的那个）里的 `## Agent skills` 块（选择规则见第 4 步）
- `docs/agents/issue-tracker.md`、`docs/agents/domain.md` 与 `docs/agents/triage-labels.md` 的内容（最后一项只在 `triage` 已安装时）

让他们在写入前修改。

### 4. 写入

**选定要编辑的文件：**

- 若 `CLAUDE.md` 存在，编辑它。
- 否则若 `AGENTS.md` 存在，编辑它。
- 两者都不存在时，问用户要创建哪一个；不要替他们选。

`CLAUDE.md` 已存在时绝不要创建 `AGENTS.md`（反之亦然）；始终编辑已经存在的那个。

若选定的文件里已有 `## Agent skills` 块，就地更新其内容，不要追加一份重复的。不要覆盖用户对周边小节的改动。

这个块：

```markdown
## Agent skills

### Issue tracker

[one-line summary of where issues are tracked]. See `docs/agents/issue-tracker.md`.

### Triage labels

[one-line summary of the label vocabulary]. See `docs/agents/triage-labels.md`.

### Domain docs

[one-line summary of layout: "single-context" or "multi-context"]. See `docs/agents/domain.md`.
```

只有当 `triage` 已安装且 B 小节执行过时，才包含 `### Triage labels` 子块，并写出 `docs/agents/triage-labels.md`。否则两者都省略。

B 小节在 GitHub 或 GitLab 上执行时，为工单系统缺少的每个已配置标签建上标签（`gh label create` / `glab label create`）。

然后以本技能目录里的种子模板为起点写出这些文档文件：

- [issue-tracker-github.md](./issue-tracker-github.md)：GitHub 工单系统
- [issue-tracker-gitlab.md](./issue-tracker-gitlab.md)：GitLab 工单系统
- [issue-tracker-local.md](./issue-tracker-local.md)：本地 markdown 工单系统
- [triage-labels.md](./triage-labels.md)：标签映射（只在 `triage` 已安装时）
- [domain.md](./domain.md)：领域文档的消费规则 + 布局

对“其他”工单系统，按用户的描述从零写 `docs/agents/issue-tracker.md`。

### 5. 收尾

告诉用户配置已完成，以及哪些工程类技能此后会读这些文件。提一句他们之后可以直接编辑 `docs/agents/*.md`；只有想换工单系统或从头重来时，才需要重跑本技能。
