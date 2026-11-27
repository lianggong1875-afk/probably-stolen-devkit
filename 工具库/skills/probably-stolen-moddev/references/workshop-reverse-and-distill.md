# 工坊模组逆向与蒸馏（方法与流水线）

**什么时候读**：要读别人的工坊模组、要给某个功能找"原版到底怎么实现的"证据、要判断某个第三方模组值不值得学。

**本工作区的完整版**（含全部踩坑与目录规范）：`共享开发\模组制作经验库\19-工坊模组逆向与蒸馏.md`；逐个模组的蒸馏档案在 `共享开发\模组制作经验库\workshop-distill\`。

---

## 1. 流水线

```text
① 扫列表   python 工具库\workshop-tools\fetch_workshop.py list --pages 10
           → 缓存文件夹\workshop-scan\items.md（id / 名称 / 订阅 / 收藏 / 体积 / 是否已下载）
② 读简介   python fetch_workshop.py brief --ids-file 候选.txt --chars 620
           → 批量导出「名称 + 简介 + 订阅数」，据此筛（用户要求：先读名称与简介再决定订阅）
③ 订阅下载 工具库\build\WorkshopUploader\bin\Release\net10.0\WorkshopUploader.exe --fetch "@候选.txt"
           （同目录需 steam_api64.dll + steam_appid.txt=4348910；--list-subs 可核对状态）
④ 反编译   python 工具库\workshop-tools\distill_prep.py --ids-file 候选.txt
           → 反编译到 缓存文件夹\decomp_ws\<id>\，透视报告到 _reports\<id>.txt
⑤ 精读     按 §3 六问读源码 → 写 共享开发\模组制作经验库\workshop-distill\<id>-<短名>.md
⑥ 收尾     WorkshopUploader.exe --unsub "@候选.txt"   ← 蒸馏完取消订阅，别让订阅列表堆积
```

## 2. 怎么筛（799 项里值得读的不超过 60 个）

1. 简介里出现 **API / 框架 / 兼容 / 让位 / 协议 / 脚本 / 缓存 / 归因** 这类技术词的优先。
2. 装上后看 `inspect_mod.py` 的 Patch 面：**Patch 多且目标集中** = 啃过硬骨头；**Patch 为 0 但功能复杂**（Zoh、存储控制终端）= 找到了纯公开 API 的路，价值更高。
3. **小而刁的最值钱**：180 行的贴图自愈解释了一个原版标记位 bug；341 行的文字层模组解释了本地化时序。
4. 大而全的只看架构，不看细节实现。
5. 纯美术/纯数值搬运的跳过（除非研究资源格式）。

## 3. 每个模组只回答六个问题（蒸馏档案也按这六节写）

1. **入口与生命周期**：`IMod` 实现、挂了哪些 `ModHook`、静态状态在哪重置（`OnDisable` / 读档 / 热重载）。
2. **数据结构**：状态存在哪（物品 Tag / ModData / ES3 / 原版字段）、键是什么、怎么持久化。
3. **Patch 语义**：每个 Patch 拦哪个原版方法、为什么必须拦这里、拦完返回什么。
4. **与原版的边界**：怎么避免碰坏 `parentInventory` / 真伪 / 存档语义。
5. **风险与限制**：作者标注的坑 + 你从代码里看出的坑（含"别抄"的部分）。
6. **可复用模式**：2~4 个带原样代码摘录（标文件名与行号）的范式 + 对我们的可用点。

**纪律**：读不到、推断的写「（推测）」；没真机验证的单独成节；**任何原版 API 结论落进经验库前必须去 `共享开发\reference\Assembly-CSharp\`（947 个原版 .cs / 24.8 万行）grep 核对**，别只信第三方模组的反编译代码。

## 4. 读别人代码时「不要抄」的东西

| 别抄 | 为什么 |
| --- | --- |
| 大范围空 `catch {}`（DigitalVault 96 处） | 违反本仓库异常规范 |
| 整段替换原版入口（SleepLag 换掉 `RunNightlyChemicalSimulation`） | 与所有同类模组互斥（工坊实证：3 个模组这么干） |
| 无 `HarmonyPriority` 的密集 Patch（ValueCache 44 个） | 顺序随缘，与别人同类 Patch 打架 |
| 吞掉原版 fallback 链（Locale Error Fix 永不执行原版） | 某些语言下退化成显示 key |
| 反编译产物里的 `if (!(a <= b))` 反向条件 | 多是反编译器风格，不代表作者原意 |

## 5. 工具链踩坑（本机已验证）

| 症状 | 修法 |
| --- | --- |
| `curl.exe` 访问 steamcommunity 报 `schannel: SEC_E_NO_CREDENTIALS` | 改用 Python `urllib`（走 OpenSSL） |
| pwsh 里 `tasklist` / `netstat` 被拒 | 用 Python socket 探端口；靠 `WorkshopUploader --list-subs` 拿状态 |
| `Invoke-RestMethod` 打 Steam API 报连接关闭 | PS 5.1 默认 TLS1.0 → 用 Python |
| Python 中文输出在 pwsh 显示 `����` | 跑前 `[Console]::OutputEncoding = [Text.Encoding]::UTF8` + `$env:PYTHONIOENCODING='utf-8'`，或让脚本写 UTF-8 文件再用 read 看 |
| `dotnet build` 报 `DllNotFoundException: steam_api64` | 复制 `Managed\..\Plugins\x86_64\steam_api64.dll` 到输出目录 + 写 `steam_appid.txt` |
| `SteamUGC.GetItemState(id, out state)` 编译不过 | Steamworks.NET 里它是**返回值**：`uint state = SteamUGC.GetItemState(pid);` |
| `ilspycmd` 出 `[HarmonyPatch(/*Could not decode attribute arguments.*/)]` | 结合透视报告与文件名推断，别据猜测写结论 |
| 经验库文件编号撞车 | 新建前先 `Get-ChildItem` 看现有最大编号（本次就撞过 18） |

## 6. 蒸馏产出四件套（用户明确要求）

1. **技能**：通用结论并入本技能 `references/`；
2. **经验库**：接口进 `03-原版接口速查`、坑进 `07-踩坑台账`、范式进 `14-模组高级编程架构与工程模式`；
3. **工具**：把"只有人力能做"的步骤做成脚本（如跨模组冲突扫描 `conflict_scan.py`）；
4. **规范**：按模组类型沉淀到 `20-各类模组制作规范`，本技能另存一份精简版 `mod-type-standards.md`。

## 7. 已蒸馏清单（2026-11 二轮，15 个）

| id | 模组 | 核心收获 |
| --- | --- | --- |
| 3832823827 | Mod Conflict Scanner | 冲突只有 3 类；红色靶点仅 itemname / loc 两类 |
| 3840533881 | Lag Inspector | `Harmony.GetPatchInfo(method).Owners` 做补丁归属归因 |
| 3842773239 | Wild's Network API | 跨模组消息总线（零硬依赖）+ 自检范式 |
| 3844123767 | Container Link | 不改容器类型的"拼柜"；事务式改动 + 版本化 Tag |
| 3844377158 | Sleep Lag Optimizer | 原版过夜 360 tick、温度模型、延迟校验范式 |
| 3845554726 | Digital Storage | 物品数据化（编码进物品 Tag 字符串） |
| 3845671416 | Storage Control Terminal | 零 Harmony 做机器 UI + 跨容器聚合索引 |
| 3845751834 | Sprite Heal | `useSpriteFromMod` 粘滞 bug |
| 3845965318 | Zoh Framework | 自研脚本 DSL（词法/语法/三 pass 执行） |
| 3846086230 | Progress Bar | 原版读档管线四阶段 + 分帧重建 |
| 3843892181 | Value Cache | 弱表 + 代际失效缓存 |
| 3845556624 + 3845895085 | Locale Error Fix + CJK Font Fallback | 文字表时序 + TMP 全局字体回退 |
| 3841912659 | Extend Framework Files | 主体/伴生拆分 + 约定式本地素材目录 |
