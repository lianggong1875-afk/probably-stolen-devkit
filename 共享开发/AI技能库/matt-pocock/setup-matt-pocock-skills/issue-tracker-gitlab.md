# 工单系统：GitLab

本仓库的工单与规格说明以 GitLab 工单的形式存放。所有操作都用 [`glab`](https://gitlab.com/gitlab-org/cli) CLI。

## 约定

- **创建工单**：`glab issue create --title "..." --description "..."`。多行描述用 heredoc。传 `--description -` 打开编辑器。
- **读取工单**：`glab issue view <number> --comments`。机器可读输出用 `-F json`。
- **列出工单**：`glab issue list -O json`，配上合适的 `--label` 过滤。
- **在工单下评论**：`glab issue note <number> --message "..."`。GitLab 把评论叫“note”。
- **加标签 / 去标签**：`glab issue update <number> --label "..."` / `--unlabel "..."`。多个标签可以用逗号分隔，也可以重复该参数。
- **关闭**：`glab issue close <number>`。`glab issue close` 不接受关闭评论，所以先用 `glab issue note <number> --message "..."` 发出说明，再关闭。
- **合并请求**：GitLab 把 PR 叫“merge request”。用 `glab mr create`、`glab mr view`、`glab mr note` 等，形态与 `gh pr ...` 相同，`pr` 换成 `mr`，`comment`/`--body` 换成 `note`/`--message`。

仓库从 `git remote -v` 推断；在克隆目录里运行时 `glab` 会自动这么做。

## MR 作为分诊面

**MR 作为请求面：no。** _（若本仓库把外部合并请求当作功能请求，就设为 `yes`；`/triage` 会读这个开关。）_

设为 `yes` 时，MR 走与工单相同的标签与状态，用对应的 `glab mr` 命令：

- **读取 MR**：`glab mr view <number> --comments`，看差异用 `glab mr diff <number>`。
- **列出待分诊的外部 MR**：`glab mr list -O json`，然后只保留作者不是项目成员或所有者的 MR（贡献者提的 MR，而不是维护者正在进行的工作）。
- **评论 / 加标签 / 关闭**：`glab mr note`、`glab mr update --label`/`--unlabel`、`glab mr close`。

与 GitHub 不同，GitLab 的工单与 MR 分开编号，所以只要知道维护者说的是哪一面，`#42` 就没有歧义。

## 当某个技能说“发布到工单系统”

创建一个 GitLab 工单。

## 当某个技能说“取出相关工单”

运行 `glab issue view <number> --comments`。

## 探路操作

`/wayfinder` 使用。**地图**是一个单独的工单，其中的**子工单**就是各条工单。

- **地图**：一个打了 `wayfinder:map` 标签的单独工单，正文承载笔记 / 目前决策 / 迷雾。`glab issue create --label wayfinder:map`。（在带原生 epic 的 GitLab 付费层级上，也可以用一个 epic 承载地图；打标签的工单在所有层级都能用。）
- **子工单**：一个在描述顶部带 `Part of #<map>`、链接到地图（`glab issue create --linked-issues <map> --link-type relates_to`）、并带 `wayfinder:<type>` 标签（`research`/`prototype`/`grilling`/`task`）的工单。一经认领，工单就指派给主导开发者。
- **阻塞关系**：GitLab 的**原生阻塞链接**，这是规范形态，界面上可见。用 `/blocked_by #<n>` 快捷动作添加，以 note 形式发出（`glab issue note <child> --message "/blocked_by #<blocker>"`）。原生阻塞链接是 Premium/Ultimate 特性；在免费层级（或功能不可用处）退回在描述顶部写一行 `Blocked by: #<n>, #<n>`。所有阻塞方都关闭时，工单才算解除阻塞。
- **前沿查询**：用 `glab api projects/:id/issues/<map>/links` 列出地图的子工单，保留带 `wayfinder:<type>` 标签且未关闭的；去掉任何有未关闭阻塞方的：指向未关闭工单的原生 `blocked_by` 链接（`glab api projects/:id/issues/<child-iid>/links`）、`Blocked by` 行里的未关闭工单、或已有指派人的；地图顺序最靠前的胜出。
- **认领**：`glab issue update <n> --assignee @me`，这是本会话的第一次写入。
- **解决**：`glab issue note <n> --message "<answer>"`，然后 `glab issue close <n>`，再把一条上下文指针（要点 + 链接）追加到地图的目前决策里。
