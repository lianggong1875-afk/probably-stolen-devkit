# 工单系统：GitHub

本仓库的工单与规格说明以 GitHub 工单的形式存放。所有操作都用 `gh` CLI。

## 约定

- **创建工单**：`gh issue create --title "..." --body "..."`。多行正文用 heredoc。
- **读取工单**：`gh issue view <number> --json number,title,body,labels,comments`。
- **列出工单**：`gh issue list --state open --json number,title,body,labels,comments --jq '[.[] | {number, title, body, labels: [.labels[].name], comments: [.comments[].body]}]'`，配上合适的 `--label` 与 `--state` 过滤。
- **把工单变成某个父工单的子工单**：`gh issue create --parent <parent> ...`，或事后 `gh issue edit <parent> --add-sub-issue <child>`（`gh` 2.94+）。老版本 `gh`：`gh api --method POST repos/<owner>/<repo>/issues/<parent>/sub_issues -F sub_issue_id=<child-db-id>`（数据库 id，见下面的**阻塞关系**）。没有子工单功能时，在子工单正文顶部写 `Part of #<parent>`。
- **阻塞关系**：GitHub 的**原生工单依赖**，这是规范形态，界面上可见。加一条边用 `gh issue create --blocked-by <n>,<n> ...` 或 `gh issue edit <child> --add-blocked-by <blocker>`（`gh` 2.94+）。老版本 `gh`：`gh api --method POST repos/<owner>/<repo>/issues/<child>/dependencies/blocked_by -F issue_id=<blocker-db-id>`，其中 `<blocker-db-id>` 是阻塞方的数字**数据库 id**（`gh api repos/<owner>/<repo>/issues/<n> --jq .id`，_不是_ `#number` 或 `node_id`）。依赖功能不可用时，退回在子工单正文顶部写一行 `Blocked by: #<n>, #<n>`。所有阻塞方都关闭时，工单才算解除阻塞。
- **统计未关闭的阻塞方**：`gh api repos/<owner>/<repo>/issues/<n> --jq .issue_dependencies_summary.blocked_by`（只算未关闭的阻塞方；0 表示可以开工）。`gh issue view <n> --json blockedBy` 也会列出已关闭的阻塞方，所以要过滤：`--jq '[.blockedBy.nodes[] | select(.state == "OPEN")] | length'`。
- **在工单下评论**：`gh issue comment <number> --body "..."`
- **加标签 / 去标签**：`gh issue edit <number> --add-label "..."` / `--remove-label "..."`
- **关闭**：先用 `gh issue comment <number> --body "..."` 发出说明，再 `gh issue close <number>`。对已经关闭的工单，`gh issue close --comment` 会丢掉评论，但仍以 0 退出。

仓库从 `git remote -v` 推断；在克隆目录里运行时 `gh` 会自动这么做。

只向用户自己的仓库提工单。绝不要提交到 `mattpocock/skills`（这些技能来自的仓库），除非用户拥有它。在 fork 里，`gh` 可能默认指向上游父仓库，所以一个会话里第一次 `gh issue create` 之前，用 `gh repo view --json nameWithOwner` 核对目标；如果不是用户的仓库，就与用户确认。

## PR 作为分诊面

**PR 作为请求面：no。** _（若本仓库把外部 PR 当作功能请求，就设为 `yes`；`/triage` 会读这个开关。）_

设为 `yes` 时，PR 走与工单相同的标签与状态，用对应的 `gh pr` 命令：

- **读取 PR**：`gh pr view <number>`，然后 `gh pr view <number> --comments`（非 TTY 下 `--comments` 只打印评论，不打印正文），看差异用 `gh pr diff <number>`。
- **列出待分诊的外部 PR**：`gh api --paginate 'repos/{owner}/{repo}/pulls?state=open' --jq '.[] | select(.author_association | IN("OWNER","MEMBER","COLLABORATOR") | not) | {number, title, author: .user.login, author_association, labels: [.labels[].name]}'`。
- **评论 / 加标签 / 关闭**：`gh pr comment`、`gh pr edit --add-label`/`--remove-label`、`gh pr close`。

GitHub 的工单与 PR 共用一个编号空间，所以裸的 `#42` 可能是其中之一：先用 `gh pr view 42` 解析，解析不到再退回 `gh issue view 42`。

## 当某个技能说“发布到工单系统”

创建一个 GitHub 工单。

## 当某个技能说“取出相关工单”

按上面**读取工单**的方式读。

## 探路操作

`/wayfinder` 使用。**地图**是一个单独的工单，其中的**子工单**就是各条工单。

- **地图**：一个打了 `wayfinder:map` 标签的单独工单，正文承载笔记 / 目前决策 / 迷雾。`gh issue create --label wayfinder:map`。
- **子工单**：一个以 GitHub 子工单形式挂到地图上的工单（见**把工单变成某个父工单的子工单**）。子工单功能未启用时，把子工单加进地图正文的任务列表，并在子工单正文顶部写 `Part of #<map>`。标签：`wayfinder:<type>`（`research`/`prototype`/`grilling`/`task`）。一经认领，工单就指派给主导开发者。
- **阻塞关系**：见“约定”下的**阻塞关系**。
- **前沿查询**：列出地图下未关闭的子工单（`gh issue list --state open`，限定在地图的子工单 / 任务列表内），去掉任何有未关闭阻塞方的（见**统计未关闭的阻塞方**，或 `Blocked by` 行里有未关闭工单）、或已有指派人的；地图顺序最靠前的胜出。
- **认领**：`gh issue edit <n> --add-assignee @me`，这是本会话的第一次写入。
- **解决**：`gh issue comment <n> --body "<answer>"`，然后 `gh issue close <n>`，再把一条上下文指针（要点 + 链接）追加到地图的目前决策里。
