# Zoh Framework（工坊 3845965318）蒸馏档案

> **源**：`缓存文件夹/decomp_ws/3845965318`（384 个 .cs，实测 26 459 行）｜**入口**：`_side_ZohFramework/ZohFramework/ModEntry.cs`（`IMod`，id = `zoh_framework`）｜**语言版本**：`RuntimeConfig.SupportedZohVersion = "0.40.0"`
> **本质**：一门内嵌故事脚本语言（`.zoh`）的完整自研实现 —— 预处理 → 词法 → 语法 → AST 校验 → 协程式树遍历求值器，外加一层把脚本原语硬接到《深空当铺》顾客/物品/新闻/存档的宿主适配层。
> 类名、方法名、verb 名全部来自反编译源码；未追到实现处标「（推测）」。代码块中 `//IL_xxxx` 反编译噪声行已删。

---

## 1. 整体架构

### 1.1 三层 + 一条宿主边

| 层 | 命名空间 | 行数量级 | 职责 |
|---|---|---|---|
| ① **语言运行时** | `Zoh.Runtime.*`（Lexing/Parsing/Parsing.Ast/Expressions/Preprocessing/Execution/Validation*/Verbs*/Types/Storage/Diagnostics/Interpolation/Variables/Shims） | ~20 000 | 词法、语法、AST、诊断、求值器（`ZohRuntime`+`Context`）、verb 驱动、值系统、持久化 |
| ② **宿主适配** | `ZohFramework` | ~4 500 | `ModEntry`、`StoryRegistry`（发现/注册）、`GameHost`（游戏桥）、`ScenePump`（求值器→对白壳）、`StoryVerbs`（`ps.*`）、`DialogueScene`、`TaggedStorage` |
| ③ **对外 API** | `ZohFramework.Api`（独立程序集，84 行） | — | 6 接口 + 1 委托 + 1 异常，供第三方编译期引用；④ 自测见 `SelfTest.cs`（1 968 行，内置真机自测，注册成 ModHelper 调试菜单项） |

关键设计：**①层零 `UnityEngine` 依赖**（连 `Math`/`Random`/`Hex` 都在 `Zoh.Runtime.Shims` 做了替身），可整包搬去别的游戏。

### 1.2 编译流水线（六段，全串在 [`ZohRuntime.Compile`](缓存文件夹/decomp_ws/3845965318/_side_ZohFramework/Zoh.Runtime.Execution/ZohRuntime.cs#L228)）

```
main.zoh 原文
 ① StoryHeaderScanner.Scan   先单独扫头（StoryHeaderScanner.cs:19），不编译正文就能拿到 scenes/uses/smalltalk
 ② IPreprocessor 链（按 Priority）：ZohVersionPreprocessor(int.MinValue) 校验 zoh_version 主版本号 /
    EmbedPreprocessor(100) 处理 #embed "path"; 递归拼接，路径锁在本 mod 目录（ModFileReader.cs:17-48）/
    MacroPreprocessor(200) 处理 |%name%| 定义与 |%name a b%| 调用、|%1| 取参。每段重建 SourceMap，行号可反查
 ③ Lexer.Tokenize            65 种 TokenType，header/body/expression 三模式共用
 ④ Parser.Parse              递归下降 + 错误恢复（Resync/Absorb/OpenKind 栈，最大嵌套 128）
 ⑤ CompiledStory.FromAst     语句数组 + Labels 字典 + Contracts（检查点参数表）
 ⑥ IStoryValidator 链        RequiredVerbsValidator(-100)/JumpTarget/StoryId/CaseOnlyDifference
                             /VerbResolution + 每个 verb 自己的 IVerbValidator
 诊断 ≥ Error → 抛 CompilationException；否则 CompiledStory 入库
```

| 关键类 | 职责 |
|---|---|
| `Parser`（1 506 行） | 语法糖全在此展开：`*x <- 3`→`core.var.set`，`-> *x`→`capture`，`====> @l`→`core.nav.jump` |
| `VerbRegistry` | verb 表 + **后缀解析**（`/jump` 命中 `/core.nav.jump`）+ 歧义检测 + 别名（VerbRegistry.cs:144-205） |
| `IVerbDriver`/`DriverResult` | `Complete` 或 `Suspend(Continuation)` —— **这就是协程**，整个运行时的心脏 |
| `Context`（847 行） | 执行上下文：变量表、defers 栈、终止倒计时、9 态状态机 |
| `Diagnostic` / `ScenePump` | 前者带 `StoryId/StoryLine/Via/Suggestion`；后者把 suspend 点翻译成 `SceneStep`（Line/Choice/Wait/End）供 `DialogueScene` 消费 |

### 1.3 从「被发现」到「被执行」

```
OnEnable()                                        ModEntry.cs:22
  StoryRegistry.RegisterAll() → 遍历 ModLoader.Mods
  判 IsConsumer：Manifest!=null && IsEnabled && ModFileExists(id,"ZohFramework/main.zoh")  StoryRegistry.cs:598
  RegisterConsumer → 建 GameHost → 读头(uses/smalltalk) → CollectExtensions
    → new ScenePump(Schedule) 全量编译一次，仅为拿 scenes 列表
    → 校验 smalltalk.at 每个时刻都有对应 @smalltalk_xxx 检查点
    → StoreClientListDict.storeClientDict["<modId>.<scene>"] = () => CreateClient(...)
    → 挂 3 个 ModHelper 调试项（next scene now / reset story / log state）
  ModHook.OnModAssetsLoaded += ...   ← 兜住后加载的 mod

每日 ModHook.OnGenerateCustomerVeryLate(manager)  StoryRegistry.cs:312
  ① CustomerRegistryLoader.Reload()（重扫所有 mod 的 regular_customers.txt / customer_lists.txt）
  ② RegisterAll() 增量补注册
  ③ 每个 mod 跑 Schedule 段 → /ps.queue 写进 host.queued → DrainQueued 后 AddClient/AddNextClient
  ④ SmallTalkInjector.Reset(); ArmLine(manager)   给原版常客挂闲聊钩子

顾客到店 → 原版取用 storeClientDict → StoryRegistry.CreateClient  StoryRegistry.cs:364
  ① Setup pass（变量只读）填 ClientSpec   ② 造 StoreClient → ModHelper.SetClientSource(client, modId)
  ③ DialogueScene.Attach(client.mainDialogue) 对话按需惰性生成   ④ client.CompleteClientCreation(true)
```

**三个 pass 是核心约定**（`StoryPass`: `Schedule=0/Setup=1/Live=2`）：

| pass | 触发 | 允许 | 证据 |
|---|---|---|---|
| `Schedule` | 每日生成顾客时跑完 | `/ps.queue`、`/ps.news`；`/ps.client` 无效 | `RunToEnd` 上限 16 步，多余只 Warn（ScenePump.cs:366-378） |
| `Setup` | 顾客被实例化时跑一步 | `/ps.client name:/intent:/…`；变量**只读** | `new TaggedStorage(host, readOnly:true, …)`（StoryRegistry.cs:390） |
| `Live` | 玩家真对话时逐句推进 | 一切改动（给钱、发物品、改声望） | 每次翻页推进一步 |

### 1.4 编译产物与缓存策略（重要负面结论）

- **无跨场景/跨帧缓存**。`ZohRuntime` 是 `ScenePump` 私有字段（ScenePump.cs:127），每个 pump 都 `new ZohRuntime` 后 `LoadStory`。`_storyCache` 只在单个 runtime 内有效，用途仅两条：让 `#embed` 进来的子故事可被 `/jump story:id` 找到；同 id 重复装载报 `duplicate_story_id`（ZohRuntime.cs:196-217）。
- 一位顾客一天约触发 **3 次完整读盘 + 预处理 + 词法 + 语法 + 校验**（Schedule / Setup / Live 首翻页）。
- 唯一"缓存"是 `StoryRegistry.textStories`（内存注入的故事源，自测用）与 `StoreClientListDict` 的工厂委托。**这同时也是它的热重载机制**，见 §4「热重载 / 重编译」。

---

## 2. DSL 语法与内建

### 2.1 文件骨架

```
My Story Name                       ← 第一行 = 故事名（也是默认 story id）
scenes: ["visit", "complaint"];     ← 元数据 key: value; 逐条
uses: ["other_mod"];
required_verbs: "*";
zoh_version: "0.40.0";
===
@schedule                           ← 检查点，@ 紧贴名字
/ps.queue "visit";
/exit;
@visit
/ps.client name: "cust_lin", intent: "trade", buys: ["LUXURY_ITEM"], budget: 400;
/converse "hello_key";
```

代码里真正被读的元数据键：

| 键 | 类型 | 作用 | 证据 |
|---|---|---|---|
| `scenes` | `list<string>` | 可被 `/ps.queue` 排队的场景；缺失 Warn "story header has no scenes" | ScenePump.cs:407-427 |
| `uses` | `list<string>` | 依赖哪些 mod 的 C# verb 提供者 | StoryHeaderInfo.cs:42-55 |
| `smalltalk` | `{"at":{"arrive":20,…},"customers":{"include":[…],"except":[…]}}` | 闲聊插话概率（0-100） | StoryHeaderInfo.cs:56-155 |
| `required_verbs` | `"*"` 或 `list` | 出现/指定的 verb 必须可解析，缺则 **Fatal 整篇失败** | RequiredVerbsValidator.cs:16-57 |
| `zoh_version` | `"MAJOR.MINOR.PATCH"`/`"latest"` | 主版本不符 Fatal，次版本超前 Warning | ZohVersionPreprocessor.cs:41-49 |
| `id` | `string` | 覆盖 story id（默认故事名），用于跨 mod 跳转与去重 | StoryHeader.cs:11-21 |

### 2.2 语法要素

| 要素 | 写法 | 备注 |
|---|---|---|
| 检查点 / 带参检查点 | `@name` / `@name(args)` | 重复报 `duplicate_checkpoint`；参数进 `Contracts` |
| verb 调用 | `/verb 参数, key: 值;` | `;` 收尾，`/` 紧贴名字；`/core.var.set` 与后缀 `/set` 等价 |
| **块形式** | `/sequence/ … /;` | `/` **紧贴** verb 名开块，`/;` 收尾；中间可混语句与语法糖（Parser.cs:731 有专门报错文案） |
| 立即调用 | `/name //other;` | 参数里当场求值另一个 verb |
| 变量引用 | `*x` / `~x` / `^x`（三种 `Scope`） | `$*x` 亦合法 |
| 赋值/读取/捕获糖 | `*x <- 3;` / `<- *x;` / `-> *x;` | → `core.var.set` / `get` / `capture` |
| 跳转/分叉/调用糖 | `====> @l;` / `====+ @l;` / `<===+ @l;` | → `core.nav.jump` / `fork` / `call`；跨故事写 `@story:id` |
| 表达式 | `$( a + b * 2 )` | 独立小语言（`ExpressionLexer/Parser/Evaluator`）；另有 `$#(…)`、`$?(…)` 两 token，语义未追到（推测） |
| 字符串 | `"…"` / `'…'` / 三引号多行 / `$"…"` | `${表达式}` 内插由 `ZohInterpolator` 处理 |
| verb 值 | `` /`expr` `` / `/ "text"` | → `core.eval.evaluate` / `interpolate`，可当参数传给 `/if` |
| 列表 / 映射 | `[1,2,"a"]` / `{ "k": v }` | `ValueAst.List` / `Map`；修饰符 `/verb [attr] [attr: 值]` 前必须留空格（Parser.cs:775） |
| 注释 | `:: 行注释` / `:::` 块 `:::` | Lexer.cs:533-587；**表达式模式不识别** |
| 嵌入 / 宏 | `#embed "sub.zoh";` / `\|%m%\|` | 唯一支持的 `#` 指令，其它报 "Unknown directive"；未知宏报 `unknown_macro` |

### 2.3 内建 verb 总表（`VerbRegistry` 后缀解析：`/jump` ≡ `/core.nav.jump`）

**A. `ps.*` —— 游戏专用 15 个（`StoryVerbs.cs`），本档案最有价值的部分**

| 名字 | 参数 | 作用 | 底层调用 | 行号 |
|---|---|---|---|---|
| `/ps.entry` | — | 当前入口场景名 | `Env.Entry` | 95-108 |
| `/ps.today` | — | 今天第几天 | `StoreStation.Instance.dayCounter` | 110-123 |
| `/ps.queue` | `"scene"` | 排进今日顾客队列 | `GameHost.Queue` → `AddClient/AddNextClient` | 125-151 |
| `/ps.client` | `name sprite intent faction wholesale bargain accept done report buys budget` | **仅 Setup pass**，写 `ClientSpec` | 11 个字段 | 153-308 |
| `/ps.owns` | `"itemId"` | 玩家是否持有 | `EmporiumEntry.GetAllItems()` + `GeneralHelper.IsItemOwned` | 310-327 |
| `/ps.remove_item` | `"itemId"` | 销毁玩家该物品 | `GraphUtils.TryDestroyAll(list, -1)` | 329-351 |
| `/ps.pay` | `整数` | 加钱 | `ModHelper.AddCredit(n, true)` | 353-375 |
| `/ps.offer` | `"itemId", owned:` | 直接摆上柜台 | `PlayerStore.AddDirectSellingItemToTable` | 377-404 |
| `/ps.face` | `"sprite"` | 换当前顾客立绘 | `StoreClientMono.Instance.image.sprite = SpriteDict.Instance.GetSprite(name)` | 406-428 |
| `/ps.night_log` | `"k1","k2",…, color:"cc7755"` | 夜间日志 | `PlayerStore.AddNightLog(text[,color])` | 430-486 |
| `/ps.rep` | `"security"/"lower", by:±20` | 读/改声望，**仅 Live 能改** | `StoreReputation.GetReputation`/`ModReputation` | 488-525 |
| `/ps.inspect` | — | 召安检员进队 | `StoreClientList.CreateInspectionClient` | 527-548 |
| `/ps.give` | `"itemId"` | 发物品给玩家 | `ModHelper.SpawnItem`+`SetItemOwnerShip(item,true)`+`GraphUtils.EmporiumTryAdd` | 550-571 |
| `/ps.news` | `"head","body", id: days: duration: importance:` | 发报纸新闻 | `StoreEventManager.ActivateEvent`/`QueueFuturEvent`/`RemoveAllEvent` | 573-664 |
| `/ps.news_active` | `"modId:id"` | 新闻是否在播 | `StoreEventManager.IsEventActive` | 666-683 |

**B. `core.*` / `store.*` / `interact.*` / `media.*`**

| 命名空间 | verb | 要点 |
|---|---|---|
| `core.var` | `set get capture drop flag type count parse` | `flag` 是上下文级（不进存档）；`parse` 把文本转 zoh 值 |
| `core.flow` | `if loop while foreach switch sequence do exit defer recover` | `/if 条件, 然后verb, is:, else:`；循环必须块形式 |
| `core.nav` | `jump fork call` | 参数 `(storyId?, label)`；`call` 等子上下文并取返回值 |
| `core.signal` | `sleep wait signal` | `/sleep` 在宿主场景夹到 5 000 ms（ScenePump.cs:82,314） |
| `core.eval` | `evaluate`(`eval`) `interpolate`(`itpl`) `scan` | `scan` 339 行，最复杂，未细读语法 |
| `core.math` | `increase decrease round roll wroll rand` | 后三个同一 `RollDriver` 注册三次 |
| `core.collection` | `has any first append insert remove clear` | 列表/映射操作；`core.channel` 的 `open push pull close` 提供队列通道，`pull` 可挂起等值 |
| `core.debug` | `info warning error fatal assert diagnose` | 直接产 `Diagnostic`；`fatal` 终止上下文 |
| `store` | `write`(`save`) `read`(`load`) `erase purge` | `PersistentStorage`→`TaggedStorage`→`ModHelper.SetModData` |
| `interact` | `converse`(`conv`) | `by: portrait: style: tag: append: wait: timeout:`；多段内容逐句挂起 |
| | `choose` | **每 3 参一组**：`可见(bool), 文本键, 值`；`prompt:` 带标题；UI 只有 3 气泡，超出 Warn 后丢弃 |
| | `chooseFrom` / `prompt` | 宿主 `ScenePump` 对这两个直接回惰性空答案（ScenePump.cs:190-198） |
| `media` | `show hide focus unfocus play playOne stop pause resume setVolume` | **驱动类齐全但 `ScenePump` 从不调 `RegisterMedia()`**，此命名空间在任何 runtime 都不存在；写 `/media.play` 会解析失败 |

---

## 3. 与游戏系统的接线

### 3.1 ModHook（只有 2 个）

```csharp
ModHook.OnModAssetsLoaded        += OnModAssetsLoaded;                          // ModEntry.cs:28
ModHook.OnGenerateCustomerVeryLate += StoryRegistry.OnGenerateCustomerVeryLate; // ModEntry.cs:29
```

`OnEnable` 先 `RegisterAll()` 补扫（mod 加载顺序不确定），再靠 `OnModAssetsLoaded` 兜住后加载者；`OnDisable` 成对解绑 + `UnregisterAll()` 清 `StoreClientListDict`、调试函数、各字典。**零 Harmony 补丁**，全部走公开 API + 事件委托。

### 3.2 生成 / 接管顾客（`SetClientSource`）

`StoryRegistry.CreateClient`（StoryRegistry.cs:364-515）是全代码最密的一段：

1. mod 未启用 → 退化成 `StoreClientList.CreateScavGeneral()`，**不崩**；`Setup` 抛异常同样退化。
2. 组装 `StoreClient`：`identifier="<modId>.<scene>"`、`displayName=host.Localize(NameKey)`、`spriteName=ClientSpec.Portrait(...)`、`clientFaction="FACTION_LOWER_LEVEL"`、`isIntroduced=true`、`nonShootable=true`、`CanReturn=false`、`cooldown=0`；`intent` 映射 buy+sell→3、sell→2、buy→1、其余→5。
3. `faction:"security"/"upper"` → `StoreClientFactionSetup.InitSecurity` / `InitUpperLevelCitizen`。
4. **`ModHelper.SetClientSource(client, modId)`** ← 归属声明。
5. 交易侧：`SetWholesale`/`SetBudget`/`clientBuyingTagList.AddRange`/`AddBasicDialog()`；挂 `onItemSold`/`onItemBought` 把成交写进存档并转给 C# 扩展。
6. `client.tryInterceptReport` ← `/ps.client report:` 抢原版"举报"按钮；`new DialogueScene(...).Attach(client.mainDialogue)` → `CompleteClientCreation(true)`。

**提前入队解耦**：schedule pass 的 `/ps.queue` 只往 `host.queued` 写名字，事后 `DrainQueued()` 再落 `AddClient/AddNextClient`（StoryRegistry.cs:343-361）。

**闲聊插话**：`SmallTalkInjector` 用 `ConditionalWeakTable<StoreClient, Armed>` 标记已挂；把 `client.allDoneDialogue` 换成 gate 页、`client.tryInterceptDismiss` 换成拦截器，触发时临时建 `SmallTalkScene` pump 再 `DialogUIManager.Instance.StartDialogue(...)`（SmallTalkInjector.cs:189-232）。`HardMarked(client)` 排除通缉/扩音器/枪单/雇佣/采购/已有分支对话的原版顾客。

### 3.3 物品 / 金钱 / 声望 / 新闻

| 需求 | 路径 | 备注 |
|---|---|---|
| 发物品 | `ModHelper.SpawnItem(id)`→`SetItemOwnerShip(item,true)`→`GraphUtils.EmporiumTryAdd(EmporiumEntry.Instance.invElement, item)`→`OwnedCount` 前后差判成功 | 先 `PreBuiltItemHelper.preBuiltItems.ContainsKey` + `DirectoryMaster.Has<GameItem>` 双查（GameHost.cs:383-401），不用 Spawn 试探 |
| 加钱 / 扣物品 | `ModHelper.AddCredit(n, true)`；扣物品走 `FindOwned`（`GeneralHelper.IsItemOwned` 过滤）→ `GraphUtils.TryDestroyAll([item], -1)` | GameHost.cs:176-179 |
| 声望 | `StoreReputation.GetStoreReputation`/`ModReputation`；`lower`→`FACTION_LOWER_LEVEL`，其余→`FACTION_SECURITY` | 夹 ±20；`ReputationUIManager.Instance` 与 `AchievementTracker.Instance` 都非空才传 flag |
| 报纸 | 造 `StoreEvent{ identifier="<modId>:<id>", eventArea=(EventArea)2, eventType=(EventType)2, … }`→`RemoveAllEvent` 去重→`days==0 ? ActivateEvent : QueueFuturEvent` | `LoadedEvents` getter 先判 `PlayerStore.HasInstance`（GameHost.cs:31-41） |
| 夜间日志 / 立绘 | `PlayerStore.AddNightLog(text[, "cc7755"])`；`CustomUIManager.Instance.GetModSprite(modId, name, true)` → 塞 `SpriteDict.Instance.spriteDictionary[name]` | 后者是"第三方图片变原版可识别 sprite"的通用手法 |

`/ps.inspect` 值得抄：先判 `StoreReputation.IsPerkUnlocked("SEC_VIP")` 直接放弃，再查队列里是否已有 `securityInspector` 防重复，最后**回查 `val.clientStack.Contains(val2)` 确认真进队**（GameHost.cs:354-381）。

### 3.4 与 `DialogUIManager` 的交互

核心技巧：**每建一页 `Dialogue` 就设一个 `startAction`，页面被显示的那一刻才求脚本的下一步**。`ISceneHost` 把 UI 抽象成 6 个方法（`SetText/AddChoice/EndConversation/Leave/Resume/Hold`），`GameHost` 实现。`AddChoice` 用 `DirectoryMaster.Item("$", true)` 这个原版"空选项"傀儡物品造纯文本选项（GameHost.cs:70-73）。`ReportGate`（58 行）解决"举报按钮只能按一次 + Live 结束与举报结束谁先谁后"，用 `Armed` + `TryPress()` + `LiveEnd()`/`HandlerEnd()` 互相抑制。

---

## 4. 错误处理与容错

**诊断形态**：`Diagnostic.ToString()` 形如 `missing_required_verb: Required verb not available: /media.play (mymod/ZohFramework/main.zoh:12) via /core.nav.call (mymod/ZohFramework/main.zoh:8)`。`Via` 链让 `#embed` + `/call` 的深层报错一路指回源头，`SourceMap.Translate` 负责行号映射。呈现只有一条路径：**`ModEntry.Log.Warning(...)`**，前缀 `[<ModName>]: `。**没有游戏内弹窗** —— 玩家只看到"顾客没来"或"对话突然结束"，原因全在日志。

**三道护栏（坏脚本不崩游戏）**：

1. **编译期**：`CompilationException` 带 `OutcomeClass`（`BadStory`/`RuntimeFailure`）。`RegisterConsumer` catch 后 Warn `"story does not load: …"` 并把 scenes 置空，该 mod 静默退化为"没有顾客"（StoryRegistry.cs:84-93）。
2. **场景级步数预算**：`ScenePump.Next()` 最多 **64 轮 tick × 每轮 1 000 步 = 64 000 步**，超了 `EndHere("… an endless loop? …")`（ScenePump.cs:266-303）；`ZohRuntime.Gate` 另按 `Config.StepBudget` 每 tick 计量（ZohRuntime.cs:747-751）。
3. **帧级**：`Context.AllowanceOut` → `Abandon(call)` + `StepPauseRequest`，控制权还回游戏循环（ZohRuntime.cs:497-537）。

**其他容错点**：`/converse`、`/choose` 出现在 `/sequence` 或 `/loop` 里 → Warn 丢弃（ScenePump.cs:429-435）；`/fork` 上下文的 `/converse` → 忽略 + Warn；`NameKey==null` → Warn 并用场景名；`report:` 指向不存在检查点 → Warn + 关掉举报（ScenePump.cs:398-405）。**第三方代码隔离最到位**：C# verb 抛异常 → `ExtensionVerbDriver` catch → Warn 全栈 + 转 `Fatal` 诊断（ExtensionVerbDriver.cs:110-118）；`RegisterStory` 抛异常 → `ExtensionSet.Absorb` 用「记长度 → `RemoveRange`」把半注册的 verb/handler 全部回滚（ExtensionSet.cs:167-192）。

**热重载 / 重编译**：**没有 `HotReloadPrepare/Activate`，没有文件监听**。`OnDisable` 清空全部运行态（存档变量保留）。
- 但因「每场景现编译」，**改完 `main.zoh` 再触发一次顾客生成（或点 ModHelper 的 "next scene now"），新脚本立刻生效** —— 隐式热重载，代价是每次全量编译。
- 存档变量在 `ModHelper` 的 mod-data 里、前缀 `zoh.`，重载不清；只有 `reset story` 调试项会 `PurgeAll()`。
- `UnregisterAll` 里调 `StoreClientListDict.ClearDisplayNameCache()`，避免改名后显示旧名。

---

## 5. 第三方接入方式

### 5.1 文件布局（路径硬编码在 StoryRegistry.cs:12 与 CustomerRegistryLoader.cs:9-13）

```
MyMod/
├─ manifest.xml                    ← id = 自己的 mod id
├─ MyMod.dll                       ← 可选：实现 IMod / IStoryExtension
├─ Localization/…                  ← 台词/名字/新闻，键走 ModHelper.GetLocalized
└─ ZohFramework/
   ├─ main.zoh                     ← 【必需】唯一入口
   ├─ regular_customers.txt        ← 【可选】把原版顾客标成"常客"（闲聊只对常客生效）
   ├─ customer_lists.txt           ← 【可选】自定义名单，供 smalltalk customers 引用；parts/*.zoh 可被 #embed 引入
```

**无 DLL 也能工作**（纯脚本 mod）；要暴露 C# 能力才需 DLL + `IStoryExtension`。

### 5.2 依赖声明

manifest 层 `<Prerequisite id="zoh_framework"/>` 由 ModLoader 排序（推测：本 DLL 内无相关代码）。脚本层 `uses: ["other_mod_id"];` 声明"我要 other_mod 的 C# verb"：provider 后加载时 `RefreshUsersOf(providerId)` 自动补上（StoryRegistry.cs:293-310）；缺失只 Warn "provider X is not loaded or not enabled; its verbs are missing"。

### 5.3 对外 C# 入口（`ZohFramework.Api` 全部 8 个类型）

| 类型 | 成员 | 要点 |
|---|---|---|
| `IStoryExtension` | `void RegisterStory(IStoryRegistrar)` | 你的 `IMod` 实现它即被自动发现（`consumerInstance is IStoryExtension`，ExtensionSet.cs:137） |
| `IStoryRegistrar` | `ConsumerId` / `Variables` | |
| | `AddVerb(string qualifiedName, StoryVerb verb, params StoryPass[] passes)` | 名须匹配 `^[a-z_][a-z0-9_]*\.[a-z_][a-z0-9_]*$`（ExtensionSet.cs:79）；`ps` 与所有 runtime 命名空间被拒；**默认只挂 Live pass** |
| | `OnArrival`/`OnPurchase`/`OnSceneEnd` | 三个事件钩子，`Action<IStoryEvent>` |
| `StoryVerb` | `delegate object StoryVerb(IStoryCall call)` | 返回 `null/bool/整数/小数/string`（`ValueBridge` 只支持标量） |
| `IStoryCall` / `IStoryEvent` | 调用侧：`ConsumerId/Scene/Pass/Args/Named/Variables/Warn`；事件侧：`ConsumerId/Scene/ItemId/Day/Variables` | |
| `IStoryVariables` | `Get/Set/Remove(name[, value])` | 非标量抛 `ArgumentException`（StoryVariables.cs:28-31） |
| `StoryPass` / `StoryVerbException` | `Schedule/Setup/Live`；`(string message)` | 想让脚本收到干净失败原因就抛 `StoryVerbException` |

源码自带范例（`SelfTestStories.Provider`，SelfTestStories.cs:8-14）：

```csharp
public sealed class Provider : IStoryExtension
{
    public void RegisterStory(IStoryRegistrar story)
    {
        story.AddVerb("zst.echo",
            (IStoryCall call) => (call.Args.Count <= 0) ? null : call.Args[0],
            StoryPass.Schedule, StoryPass.Setup, StoryPass.Live);
    }
}
```

脚本侧即可 `/zst.echo 4; -> *x;`。

### 5.4 名单文件格式与上线自查

`CustomerRegistry.ParseLine`（CustomerRegistry.cs:64-91）：`#` 开头是注释，一行一个 id 表示加，`-id` 表示移除；一行多 id 或含空格 → Warn "one id per line"。**所有 mod 的名单叠加合并**（`CustomerRegistryLoader.ModFiles` 遍历 `ModLoader.Mods`，CustomerRegistryLoader.cs:54-73），多个扩展可往同一份常客表里加人。

- [ ] `ZohFramework/main.zoh` 存在、首行故事名、含 `===`；有顾客时 `scenes:` 必须列全，`/ps.queue "x"` 的 x 必须在表里（否则 Fatal）。
- [ ] 台词/名字/新闻全走本地化键；取不到会 Warn 并回退显示键名。`name:` 必填，否则显示名 = 场景 id。`intent:"buy"/"trade"` 必须配 `buys: [...]`。
- [ ] `required_verbs` 按需声明（`"*"` 最严）；自定义立绘需 `CustomUIManager.GetModSprite(modId, name, true)` 能取到；`/sleep` 单次 ≤ 5 000 ms。

---

## 6. 可复用模式

### 模式 1：脚本语言四段流水线 —— 可插拔预处理链 + SourceMap

把「读文本 → 变换文本 → 记位置映射」做成 `IPreprocessor` 链，每段独立报错并修正行号；任何要支持 `#embed`/宏/版本校验的小语言都用得上。`ZohRuntime.cs:232-254`

```csharp
	foreach (IPreprocessor preprocessor in Handlers.Preprocessors)
	{
		PreprocessorResult preprocessorResult;
		try
		{
			preprocessorResult = preprocessor.Process(new PreprocessorContext(text, sourcePath, header)
			{
				RuntimeFlags = _flags,
				SourceMap = map
			});
		}
		catch (Exception ex)
		{
			throw HandlerCrashed(diagnostics, "Preprocessor " + preprocessor.GetType().Name, ex, file, header.Id);
		}
		diagnostics.AddRange(preprocessorResult.Diagnostics.Select(WithStory));
		if (preprocessorResult.Halt)
		{
			throw LoadFailed(diagnostics);
		}
		text = preprocessorResult.ProcessedText;
		map = preprocessorResult.SourceMap ?? map;
	}
```

### 模式 2：自动发现 + 事件驱动补注册

「按约定路径探测文件」而非配置登记；启动全扫 + `OnModAssetsLoaded` 兜底，`ContainsKey` 防重入，整段 `try/catch` 只 Warn 不抛。`StoryRegistry.cs:59-72` 与 `:598-605`

```csharp
private static void TryRegisterConsumer(LoadedMod mod)
{
	try
	{
		if (mod?.Manifest != null && IsConsumer(mod) && !hosts.ContainsKey(mod.Manifest.ID))
		{
			RegisterConsumer(mod);
		}
	}
	catch (Exception ex)
	{
		ModEntry.Log.Warning((object)("Could not register story mod " + mod?.Manifest?.ID + ": " + ex));
	}
}

private static bool IsConsumer(LoadedMod mod)
{
	if (mod?.Manifest != null && mod.IsEnabled)
	{
		return ModHelper.ModFileExists(mod.Manifest.ID, "ZohFramework/main.zoh");
	}
	return false;
}
```

### 模式 3：文本脚本驱动对话 —— `startAction` 作为"下一次求值"

不预生成整串对白，而是**每页挂 `startAction`，被显示时才向脚本要下一步**；页数无上限，内存只占当前页。`DialogueScene.cs:73-84` + `:114-132`

```csharp
private Dialogue NewPage(ZohValue answer, double sleptMs = 0.0)
{
	Dialogue page = new Dialogue();
	host.SetText(page, speaker, "...");
	page.SetStartAction((Action)(() =>
	{
		Fill(page, answer, isFirst: false, sleptMs);
	}));
	return page;
}

	switch (sceneStep.Kind)
	{
	case SceneStepKind.Line:   lastLine = host.Localize(sceneStep.Text); Show(page, Title(sceneStep), lastLine); page.SetNextDialogue(NewPage(null)); break;
	case SceneStepKind.Choice: Show(page, Title(sceneStep), (sceneStep.Text != null) ? host.Localize(sceneStep.Text) : lastLine); AddChoices(page, sceneStep); break;
	case SceneStepKind.Wait:   host.SetText(page, shownTitle, shownText); host.Hold(page, sceneStep.Milliseconds, (double slept) => NewPage(null, slept)); break;
	default:                   Finish(page, isFirst); break;
	}
```

### 模式 4：模组变量进存档 —— 私有前缀 + 事务式写入

键统一 `zoh.<store>.<var>`（`TaggedStorage.cs:190-193`），值用 1 字符类型前缀文本编码（`n:`/`b:true`/`i:123`/`f:1.5R`/`s:文本`，只允许标量），**绝不写 JSON 进 mod-data**；写入先记旧值、出错逐个回滚。`TaggedStorage.cs:143-166`

```csharp
private void Apply(IReadOnlyList<KeyValuePair<string, string>> changes)
{
	List<KeyValuePair<string, string>> list = new List<KeyValuePair<string, string>>();
	HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
	try
	{
		foreach (KeyValuePair<string, string> change in changes)
		{
			if (hashSet.Add(change.Key)) { list.Add(new KeyValuePair<string, string>(change.Key, store.Get(change.Key))); }
			if (change.Value == null) { store.Remove(change.Key); } else { store.Set(change.Key, change.Value); }
		}
	}
	catch (Exception ex)
	{
		bool flag = true;
		foreach (KeyValuePair<string, string> item in list)
		{
			try { if (item.Value == null) { store.Remove(item.Key); } else { store.Set(item.Key, item.Value); } }
			catch (Exception) { flag = false; }
		}
		throw new StorageException("the game's mod data refused a change (" + ex.Message + ")" + (flag ? "; nothing was changed" : "; some keys stay changed"), ex);
	}
}
```

---

## 7. 对我们的可用点

### 7.1 「深空电玩」`DeepSpaceArcade.dll`

| 可用点 | 怎么用 |
|---|---|
| **小游戏剧本脚本化** | 关卡/台词/结算文案现在写死在 C# 或 JSON。可搬 `Zoh.Runtime.Lexing/Parsing`（零游戏依赖）做极简 DSL：`@标签` + 台词 + `@结算`。 |
| **惰性翻页对话机** | 街机区 NPC 搭话、通关彩蛋对白，用「每页挂 `startAction`」而非预生成整串 `Dialogue`。 |
| **缺天然驱动点 / 通关播报** | 电玩城没有"每日召唤顾客"这种钩子，需自建触发（进入街机区/选中机台），否则要自己加编译缓存（§1.4）；播报可照搬 `/ps.night_log` + `ModHelper.GetLocalized`（GameHost.cs:204-219）。 |

### 7.2 「深空当铺：可能是群友」`DeepSpacePawn.dll`（最直接受益方）

| 可用点 | 具体落点 |
|---|---|
| **顾客台词数据驱动** | 把群友到店台词搬成 `@arrive_lin /converse "lin_arrive_01";` 形态。改文案不用重编译 DLL，也改不坏判定逻辑。 |
| **`/ps.client` 即现成顾客 schema** | `name sprite intent faction wholesale bargain accept done report buys budget` 这 11 个字段正好覆盖"身份+交易意图+立绘+举报检查点"。**照这套字段名设计我们的顾客数据表**，将来做 Zoh 兼容层零翻译成本。 |
| **`ReportGate` 一次性按钮范式** | 群友剧情里"举报/揭发/告密"这类只能按一次、且与正常结束竞争的位置，直接抄 58 行。 |
| **`SmallTalkInjector` 插话范式** | 给**原版**顾客加"群友乱入吐槽"正是这套：`ConditionalWeakTable` 标记 + 替换 `allDoneDialogue` + 拦截 `tryInterceptDismiss` + `HardMarked` 白名单排除。比 Harmony patch 安全得多。 |
| **`StoryState` 存档键约定 / `TaggedStorage` 事务写入** | `last_scene`/`last_visit_day`/`bought_<itemId>`/`sold_<itemId>`（StoryState.cs:7-13），配私有前缀；我们目前直接 `ModHelper.SetModData` 一把梭，群友剧情多变量联动时应抄它的"失败全回滚 + 抛 `StorageException` 说明是否半套"。 |
| **`ExtensionSet` 命名空间防冲突** | 若对外暴露 verb/命令，抄 `RuntimeNamespaces()` + 正则校验 + "被占了就 Warn 跳过、不抛"的软着陆（ExtensionSet.cs:256-307）。 |
| **内置游戏内自测** | `SelfTest.cs` 1 968 行，注册进 `ModHelper.AddDebugFunction("zoh_framework","Modding",…,…)`。我们有 `Run-Regression.ps1`，但游戏内自测（顾客能否生成/闲聊能否插/新闻能否发）目前只能真机手点 —— 这层可照搬。 |

### 7.3 三条工程结论

① **`VerbRegistry` 后缀解析 + `CaseNearMatch`**：`/jump` 命中 `/core.nav.jump`，大小写写错给友好提示 —— 任何"给内容作者用的命令表"都该这么做。② **`GameHost` 所有资源获取先判空、再 Warn、再回退**（`SpriteDict.Instance == null`、`PlayerStore.HasInstance`、`LoadedEvents == null`），从不静默吞异常。③ **游戏桥抽成 `IStoryGame`/`IKeyValueStore`/`ISceneHost` 三个接口**，语言层只认接口 —— 这是它能写出 1 968 行离线自测的根本原因（`SelfTestHost` 无存档也能跑全套）。

---

## 附：未验证 / 推测

- **`media.*` 为何不注册**：`RegisterMedia` 只在构造 `s_moduleVerbs` 的临时 registry 里被调用，`ScenePump` 从不调用。（推测：为将来的独立播放器预留，或早期残留。）
- **manifest `<Prerequisite>`**：本 DLL 无相关代码，属 ModLoader 能力（推测）。
- **`$#(…)` / `$?(…)`**：Lexer 明确产出 `DollarHashParen`/`DollarQuestionParen`，未追到 Parser 的具体语义分支（推测：带格式/带条件的表达式变体）。
- **`core.eval.scan`**（339 行的 `ScanDriver`）：只确认它与 `interpolate`/`evaluate` 并列注册，匹配语法未细读。**被 `#embed` 的子文件可省头部**：由 `SelfTestStories.ProviderBody` 常量以 `===` 开头推断（SelfTestStories.cs:66），未在 Parser 里逐行确认。
