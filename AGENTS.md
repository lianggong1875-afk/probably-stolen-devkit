# 深空当铺模组工作区规范

工作区根目录：`D:\0深空当铺模组开发`（本地 Git 对象库在 `Git仓库\`，直接在根目录执行 `git` 命令即可）。

## 1. 项目路由与目录分工

- **《深空电玩》**：[深空电玩/PROGRESS.md](深空电玩/PROGRESS.md)（`deepspace_arcade` / `DeepSpaceArcade.dll`）
- **《深空当铺：可能是群友》**：[深空当铺：可能是群友/PROGRESS.md](深空当铺：可能是群友/PROGRESS.md)（`freya_cornell` / `DeepSpacePawn.dll`）
- **开发桥接器源码**：[深空桥接器/README.md](深空桥接器/README.md)（要给游戏内 MCP 桥加工具直接改这里，别在玩法项目里写探针绕路）
- **经验库**：[共享开发/模组制作经验库/README.md](共享开发/模组制作经验库/README.md)
- **术语表**：[共享开发/GLOSSARY.md](共享开发/GLOSSARY.md)（成色、真伪、品类、零能力等词的唯一叫法，讨论前先对齐）
- **AI 技能库**：[共享开发/AI技能库/README.md](共享开发/AI技能库/README.md)（mattpocock/skills 中文化版，流程层方法；与本工作区工具链的对接见 [落地说明](共享开发/AI技能库/matt-pocock/_本工作区落地.md)）
- **工具库**：[工具库/README.md](工具库/README.md)
- **云备份**：[共享开发/云备份说明.md](共享开发/云备份说明.md)（三个私有 GitHub 仓库的分工与同步，`共享开发/云备份.ps1` 执行）
- **分支与备份规范**：[共享开发/分支与备份规范.md](共享开发/分支与备份规范.md)（**每次开工前必读**：分支模型、五道合并门禁、四路备份、三张检查清单）
- **缓存目录**：`缓存文件夹\`（反编译、临时输出用完就清；`save-backup-20261126-0640\` 存档备份禁止删除）

规则：两款模组源码、美术、构建输出互不引用；新编译产物统一落各自项目 `build/`；完成一批有效改动就打一次清晰的本地 `git commit`，并顺手更新对应项目的 `PROGRESS.md`。

## 2. 硬底线（不可破）

1. **Slot 0 主档禁碰**：绝不进入、修改或覆盖 Slot 0；真机测试只进 Slot 1，进档后确认 `saveSlot` 和 `worldId`。
2. **付费调用先确认**：未经用户明确同意，不得发起消耗美元的 OpenAI 官方补全/生图请求（连通性验证只用免费 `GET /v1/models`）。
3. **工坊发布先确认**：正式打包上传 Steam 工坊前须经用户确认。
4. **文案红线**：文案与单据保持**西方深空科幻工业语感**（空间站企业、防务承包商、审计合规、灰市流转），严禁中式古风/老当铺市井词（如“上秤/压柜/收执/风头”），严禁游戏化/数值化措辞（如“+XX%/品质模组/稀有度”）。

## 3. 模组编程硬规范（严禁防御性编程垃圾）

详细技术细节见 [开发流程与编程规范](共享开发/开发流程与经验.md)。写代码必须守住以下六条：

1. **简单直白，拒绝套娃**：选最简单可维护的实现，最少文件和抽象层。**严禁**为了离线假测试把一个功能硬拆成 `*Policy / *Bridge / *Seams / *Patch` 五六个文件；**严禁**臆想不存在的多模组重入攻击而写几百行票据栈、代际锁、五重核对状态机。
2. **异常策略：对内暴露，边界捕获**：**严禁**在内部逻辑里写 `try { ... } catch { }` 静默吞异常（坏了不报错最误事）。只在会拖垮游戏循环的 Unity 帧回调或 Harmony 入口处做边界捕获，并且必须打 `LogError` 留下堆栈。
3. **Harmony 与生命周期**：优先 `ModHook` 和原生公开 API；能用 Prefix/Postfix 就绝不写 IL Transpiler；补丁命中重载方法时必须显式写参数类型表；访问 `PlayerStore.Instance` 前先判 `PlayerStore.HasInstance`；静态 UI/缓存引用在读档（`OnGameLoadedNormal`）和热重载（`HotReloadPrepare/Activate`）时成对重置。
4. **物品、标签与判定铁律**：
   - 判物品是否已注册用 `DirectoryMaster.Has<GameItem>(id)`，不用 `ItemSpawner.Spawn` 试；
   - 发给玩家用 `DirectoryMaster.Item(id, true)`，发给 NPC 用 `Item(id, false)`；
   - 精确投放到目标容器用 `GraphUtils.CanAccept` + `TryAccept`，校验落点用引用比较 `item.parentInventory == targetInv`；
   - 自定义品类只用 `EnableTag`，不往 `SetGameItemType` 写非原版类型（避免蓝字 `[Translation Error]`）；
   - 真伪/成色判定**只认 `realCondition`**，绝不读显示名或 `fakeCondition`；
   - 持久化只写模组私有键，不改原版存档结构。
5. **发布零权限要求**：开发版允许带 `files` 能力方便热更/补图，但正式发布候选（`-PublicRelease`）必须在源码层面彻底消除外部能力依赖，manifest 写自闭合 `<Capabilities/>`，并通过真实 `ModScanner` 零能力扫描。
6. **真测试，不写假断言**：离线测试只测真实行为和核心不变量（文案、判定、定价等），**严禁**写 `Contains(源码, "某变量名")` 这类锁死重构的字面量断言。

## 4. 开发测试与脚本规范

- **常规开发闭环**：改代码 → `Build-Mod.ps1` 编译（开发版 + `-PublicRelease` 校验零能力）→ 跑 `Run-Regression.ps1` → 按需直接装机、热重载或进真机 Slot 1 验证（包括桥接工具、探针或 `native-input.ps1` 键鼠模拟）。
- **验证要诚实**：编译通过不等于游戏运行正常，日志正常不等于画面正确，未真机验证的项如实标明。
- **编码铁律**：Windows PowerShell 5.1 脚本（`.ps1`）必须保存为 **UTF-8 with BOM**；JSON 配置文件必须 **UTF-8 无 BOM**；改文本用文件编辑工具，不用 PowerShell 管道往返；禁止使用 `nul`/`con` 等 Windows 保留文件名。
- **shell 铁律**：本机**只有 Windows PowerShell 5.1，没有 `pwsh`（PowerShell 7）**。脚本里要从别的进程调 `.ps1`，用 `Join-Path $PSHOME 'powershell.exe'`，不要写 `pwsh`。另外 `Write-Host` 的输出走 information stream，函数里捕获子脚本输出要用 `*>&1`，用 `2>&1` 抓不到。

## 5. 分支、门禁与备份（硬流程，细则见 [分支与备份规范](共享开发/分支与备份规范.md)）

1. **`main` 只接受合并，不直接提交**。永远可编译、真机可跑、零能力达标。
2. **开工先建分支，本地和云端同时建**（云端那一步不能省，它是本地硬盘故障时的唯一异地副本）：
   ```powershell
   .\工具库\build\New-FeatureBranch.ps1 -Name feat/arcade-burner-animation
   ```
   命名：`feat|fix|exp|release/<域>-<短句>`，域 ∈ `arcade` / `freya` / `tools` / `kb`；全小写、连字符分词、**不写中文**；一个分支只做一件事。
3. **合并前必须过五道门禁**，任何一道红都不许并：
   ```powershell
   .\工具库\build\Invoke-PreMergeGate.ps1 -Mod All
   ```
   门禁 = ①开发版编译 ②正式候选 + 零能力扫描 ③离线回归 ④跨模组冲突扫描（ERROR 必须为 0）⑤工作区洁净。
   **真机验证（只进 Slot 1）不进脚本门禁，但发布前必须有结论；没验就写"未真机验证"。**
4. **合并后打 tag 并上云**：
   ```powershell
   git switch main ; git merge --no-ff feat/xxx
   git tag -a vX.Y.Z -m "..."
   .\共享开发\云备份.ps1 -Branch main
   .\工具库\build\Backup-All.ps1
   ```
5. **备份要四路，缺一路不算备份**：① 本地工作区 + git ② 云端三仓 ③ **另一块物理盘的裸仓镜像** ④ bundle 单文件；美术与文案另打资产 zip + SHA-256 清单（图是最不可再生的东西）。
6. **回滚纪律**：没合并的直接丢分支；**合并后发现坏了用 `git revert`，绝不 `reset --hard`**（会改写历史让云端对不上）；工作区物理损坏从裸仓镜像或 bundle 恢复，恢复后先核 `build\public` 的 DLL SHA-256 与 `PROGRESS.md` 是否一致。
