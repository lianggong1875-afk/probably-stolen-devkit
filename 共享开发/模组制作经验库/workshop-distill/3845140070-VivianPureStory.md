# 薇薇安 · 纯剧情 / Vivian - Pure Story（工坊 3845140070）

> `manifest.ID = vivian_pure_story` ｜ `ModVersion 1.1.1` ｜ 程序集 `VivianPureStory.dll`（136,192 B）｜ `<Capabilities />` 空（零能力）｜ 声明 `0.46D` ｜ 反编译源码 `缓存文件夹\decomp_ws\3845140070`（67 个 `.cs` / 8,409 行，命名空间 `VivianPureStory`）｜ 透视报告 `_reports\3845140070.txt`（38 处特性式 Patch / 15 个靶类）
> **为什么值得读**：这是本库唯一一个**"三语全程配音 + 幕表 + 三结局"做到发布级、却一个新物品都没注册、一处经济数值都没改**的成品。它给"纯剧情模组"提供了一整套可复制的骨架：幕 = `story_stage` 整数 + 一个 `switch` 工厂 + `Dialogue.AddChoice` 分支；进度 = 十几个 `ModData` 私有键；F11 面板 = 一张写死的 15 行幕表按 `story_stage` 上色。
> **最狠的一处**：配音**没走 `Sounds/` 那套 fire-and-forget 的 `PlaySound`**，而是自己 `ReadModFileBytes` → 手写 RIFF 解析 → `AudioClip.Create`+`SetData`（`VivianVoice.cs:509-595`），只为了拿到 `clip.length`；有了长度才能"这句没播完就不许推进对话/不许赶客"（三个补丁），这是"配音不被打断"的真正实现。
> 口径要小心：商店简介说 **19 幕**、外面常引 **14 幕**，而 F11 面板里写死的是 **15 行幕表**、`story_stage` 只走 **0→13**（§2.1 逐条对账）。

## 1. 规模与结构：8,409 行怎么花的

| 类别 | 文件 | 行 | 占比 | 说明 |
| --- | ---: | ---: | ---: | --- |
| **剧本台词（以 C# 字符串字面量形态存在）** | 6 | 2,401 | 29% | `VivianEventClients.cs` 1,257 / `VivianRomance.cs` 469 / `VivianSides.cs` 400 / `VivianBargainLines.cs` 191 / `VivianClient.cs` 56 / `VivianE5Dialogues.cs` 28。台词直接写在 `Line("……")` / `new Dialogue().SetText(名字, "……")` 里，**无外部剧本文件** |
| 配音 / 多语言 | 2 | 921 | 11% | `VivianVoice.cs` 596、`VivianI18n.cs` 325 |
| 状态机 / 排期 / 事件 | 9 | 1,335 | 16% | `VivianSchedule.cs` 402、`VivianRobbery.cs` 254、`VivianDismiss.cs` 206、`VivianEvents.cs` 120、`VivianState.cs` 44、`VivianEventGuard.cs` 70、两个 Watchdog 147、`VivianStare.cs` 92 |
| UI（F11 面板 / 选项页） | 3 | 865 | 10% | `VivianJournal.cs` 635、`VivianOptions.cs` 199、`VivianJournalHost.cs` 31 |
| 立绘 / CG / BGM | 3 | 750 | 9% | `VivianPortrait.cs` 499、`VivianCg.cs` 165、`VivianBgm.cs` 86 |
| **Harmony 补丁** | 34 | 988 | 12% | 平均 29 行/个，**0 处 IL Transpiler**，全是 Prefix/Postfix + 早退 `return true` |
| 调试台 | 1 | 451 | 5% | `VivianDebug.cs`：**43 个** `ModHelper.AddDebugFunction`（含 12 幕逐幕跳转）＋ Ctrl+F9 组合键（正式包里没摘） |
| 交互小件（枪 / 箱 / 烟 / 印章 / 礼） | 7 | 525 | 6% | `VivianGun` 113、`VivianCrate` 170、`VivianCigarette` 51、`VivianStamp` 47、`VivianConfrontUtil` 52、`VivianE4Gift` 50、`VivianPeer` 42 |

未列入上表的只剩入口 `VivianMod.cs` 161 行（9 个 `ModHook` 订阅 + `HotkeyLoop` 协程 + `JournalHost` 生成）与 `Properties\AssemblyInfo.cs` 12 行；上表合计 8,236 行，与 `wc -l` 的 8,409 对得上。

| 资源 | 量 | 明细 |
| --- | --- | --- |
| 配音 | **676 个 WAV / 117 MB** | `Audio\` 216（中）＋ `Audio\en\` 230 ＋ `Audio\ja\` 230 |
| 索引表 | `Audio\voice-map*.txt` 6 份（173/179/179 行 ＋ NPC 43/51/51）＋ `voice-manifest.json`（`{text,file}` 数组） | 键 = 台词文本 |
| 剧本辅助数据 | `Data\` 9 个文件 | `i18n-en/ja.tsv` 各 294 行、`bargain-{zh,en,ja}.tsv`、`expr-map{,-en,-ja}.txt`、`romance-visitors.txt` |
| 立绘 / CG | `Textures\Items\` 13 张（1.9 MB） | `femaleUpperVivian4x` ＋ 7 个表情后缀；`cg_vivian_true.jpg`、`cg_vivian_rival.jpg`；`femaleUpperVesper4x`＋`maleUpperThug4x` 两个配角 |
| 音乐 | `Sounds\maou_bgm_orchestra04.mp3` 3.7 MB | 走**标准 `<mod>/Sounds/`** 协议（§3.1 有对比） |

**数据 / 逻辑 / 美术三分**：真相是 **台词没有外置**——294 条英/日译文在 `Data\i18n-*.tsv`，**中文原文在代码里**；所以"剧本数据"这一栏其实是 2,401 行 C#，美术（117 MB 音频 + 13 张贴图）全部外置。

## 2. 幕怎么组织（12 幕主线 / 15 行幕表 + 状态机 + 分支）

### 2.1 主线 12 幕 / 幕表 15 行：一幕一个工厂方法，不是一幕一个类

调度是一个 `switch` 表达式（`VivianEventClients.cs:16-34`），`story_stage` 0→11 依次映射到 `CreateE1DailyBuyer` … `CreateE10Courier`（含 `E6b` 收箱支线），7 号返回 `null`（交给抢劫模块），12 号以后另走大结局：

```csharp
internal static StoreClient CreateForStage(int stage) => WithIncidentSnark(stage switch {
    0 => CreateE1DailyBuyer(), 1 => CreateMentorProxy(), 2 => CreateE3CigaretteSeller(),
    3 => CreateE4StampSeller(), 4 => CreateE5HandAppraisal(), 5 => CreateE6CrateSeller(),
    6 => CreateE6bCrateReturn(), 7 => null, 8 => CreateE7Jealous(),
    9 => CreateLoanSharkProxy(), 10 => CreateE9Farewell(), 11 => CreateE10Courier(), _ => null });
```

幕与幕之间只靠两个键推进（`VivianSchedule.cs:160-205`）：`next_day`（该来的那天）与 `story_stage`（下一幕号）。到期就 `manager.AddClient(client)`、`story_stage+1`、算下个 `next_day`（普通 +3 天，5/6 幕 +2 天，因为抢劫插在中间）。**分支不靠树，靠事件里的临时选择**：

- 幕内选项统一走 `AddChoice(root, playerText, Action<Dialogue> onChosen)`（`VivianEventClients.cs:1153-1161`），20 处；实现是 `root.AddChoice(DirectoryMaster.Item("$", true), "", "", branch, callback, null)`——借原版 `Dialogue.AddChoice` 的"物品 + 分支 + 回调"三件套，用 `"$"`（钱）做占位物品即可做出纯文字选项。
- 跨幕分支落成布尔事实：E6 没买那箱货 → `crate_bought=false`，下一幕直接跳过 E6b 并把抢劫提前（`VivianSchedule.cs:181-188`）；好感/让价/报警/掏枪各自置位（§2.2）。

F11 面板里那张**写死的 15 行幕表**（`VivianJournal.cs:420-429`）才是对外口径：`对门新邻 / 旧友带话 / 上等烟丝 / 一张旧票 / 一只铁手 / 清仓一件 / 收箱过客 / 箱未启封 / 闭门之前 / 带伤叩门 / 账外之人 / 最后一笔 / 迟到的信 / 对门灯火 / 不速之客`，配一个 15 元映射数组 `{0,1,2,3,4,5,-1,6,-1,-1,9,10,11,12,13}`（`-1` = 独立事件，不吃 `story_stage`）。

**"几幕"的三个口径可以对上**：调试台的 43 个按钮把主线切成 **12 幕**（`第1幕 初次 / 第2幕 导师 / 第3幕 香烟 / 第4幕 食品券 / 第5幕 机械手 / 第6幕 物资箱 / 第7幕 箱子回访 / 第8幕 关店抢劫 / 第9幕 捉奸 / 第10幕 贷款+导师 / 第11幕 道别 / 第12幕 来信·好、来信·坏`）；F11 幕表在这 12 幕之外多了 3 行独立事件（`收箱过客`/`箱未启封`/`带伤叩门`，映射值 `-1`）；商店简介的 19 幕则连结局与彩蛋一起算。**引用时以 12 幕（主线）为准**。

### 2.2 状态机：全部 `ModData` 私有键，三结局 = 三行布尔

`VivianState.cs:11-29` 只有四个 4 行函数，键统一加前缀 `vivian_`。关键键：

| 键 | 含义 | 写入点 |
| --- | --- | --- |
| `story_stage` / `next_day` | 下一幕号 / 该来的天 | `VivianSchedule.cs:194`、`:204` |
| `buyout_day` / `IsPropertyPaid` | 玩家买下店铺那天 | `Patch_VivianBuyoutDay`（`PlayerStore.OnStoreOwned` Postfix） |
| `story_done_day` / `true_ending` / `vivian_dead` | 主线收束、好坏走向、她是否已死 | `VivianEventClients.cs:99-102`、`VivianRobbery.cs` |
| `crate_bought` / `robbery_day` / `scout_day` / `wounded_day` | 四个支线触发点 | `VivianSchedule.cs:87-110`（`ShiftFollowing`：主/支线迟到就整体顺延） |
| `jealous_ok` / `jealous_done` / `jealous2_done` / `nag_done` | 四个独立事件的"已看" | 各事件 + `VivianRomance` |
| `report_vivian` / `gun_vivian` | 玩家按过警报 / 朝她掏过枪 | `VivianGun.MarkGun` 等 |

三结局是**纯判定、不落新键**（`VivianJournal.cs:607-633`）：`好 = e11_done && true_ending && (jealous_ok || !jealous_done)`、`普通 = e11_done && true_ending && !jealous_ok`、`坏 = !true_ending && (story_done_day>0 || e11_done)`。

F11"到第几幕"= 拿 `story_stage` 去比那张 15 元映射表，每行产出五态之一 `Seen / Current / Pending / Skipped / Dead`（`St` 枚举 `:10-17`；未到显示"第 N 幕　？？？"，跳过显示原因备注，如 `"当时没买下那箱"`），顶上一行 `ProgressLine(...)` 汇总。窗口本身是 `CustomUIManager.Window("vivian_journal", …, "rear")` + `BeginTabs/BeginTab/AddRow`，热键默认 `KeyCode 292`（F11）存 `journal_key` 键。

## 3. 它怎么做这件事（原版类/方法 + Patch 靶点）

它碰过的原版类一共 15 个，按用途分四摊：**说话**——`DialogUIManager`（9 处）、`Dialogue`、`StoreClientDialogList`（5 处）；**客人**——`StoreClient`（4 处）、`StoreClientManager.AddClient/AddNextClient`、`PlayerStore.DismissCurrentClient`；**店里机关**——`StoreBell`、`StoreShutterButton`、`StoreSecButton`、`Shotgun`、`LockHelper`、`BargainUIManager`、`InsStampHelper`/`InsCigaretteHelper`、`AppraisalServiceHelper`；**资源与设置**——`SpriteDict.spriteDictionary`、`StoreClientMono`、`AudioManager.audioSourceMusic`/`audioDirectory`、`GameOptionRegistry`。下面按"配音 / 不改经济 / 补丁全表 / 表现层"四块讲。

### 3.1 三语配音：不走 `Sounds/`，走自解码；路径按语言分目录，键 = 台词文本

| 环节 | 做法 | 位置 |
| --- | --- | --- |
| 语言判定 | `ModHelper.GetCurrentLocale()` → `zh*`/`ja*`/其余→`en`；`Active = 语言≠zh` | `VivianI18n.cs:20-70` |
| 文件路径 | zh 读 `Audio\v00.wav`；en/ja 读 `Audio\en\v00.wav`、`Audio\ja\v00.wav`（`Active ? "Audio/"+lang+"/" : "Audio/"`） | `VivianVoice.cs:517` |
| 索引 | `Audio\voice-map{,-en,-ja}.txt`，一行 `文件名<TAB>该语言台词`；NPC 另一张 `voice-map-npc*.txt`，三列 `文件名<TAB>说话人<TAB>台词` | `VivianVoice.cs:50-52`、`:97-110`、`:146-169` |
| 查表 | 把正在显示的 `dialogue.plainText` **规范化**（去所有空白、丢弃 `<…>` 富文本标签）后当键查表 | `VivianVoice.cs:186-211`、`:223`、`:296` |
| 解码 | `ReadModFileBytes` → 手验 `RIFF/WAVE` 头、遍历 `fmt `/`data` 块、只接受 PCM 16bit → `AudioClip.Create` + `SetData` | `:535-595` |
| 播放 | 自建 `DontDestroyOnLoad` 的 `AudioSource`（`spatialBlend=0`），`Play` 前先 `Stop()` + `ClearPending()` | `:460-507` |

**"同一句台词换语言不打断"** 由三件事共同保证：

1. `_clips` 以**文件名**为键缓存（`:509-533`），换语言时 `ResetLanguageCaches()` 整体清空并 `Stop()`（`:429-437`）——不会出现"旧语言 clip 被当成新语言复用"。
2. `_playEndAt = realtimeSinceStartup + clip.length + 0.5f`（`:476`），留 0.5 s 尾巴；`WaitRoutine`（`:419-427`）同时等"到点"和"`isPlaying` 变假"两个条件满足才 `Flush()` 挂起的动作。
3. 三个补丁把"推进/离开"挂到 `WaitThen` / `HoldLeave` 上：`DialogUIManager.ContinueConversation` 被 Prefix 拦下（`Patch_VivianVoiceAdvance`）、`DialogUIManager.ScheduleFlow` 的 `ref Action` 被包一层（`Patch_VivianVoiceSchedule`）、`PlayerStore.DismissCurrentClient` 被 Prefix 拦下、先 `InterruptDialog()` 再原样重调自己（`Patch_VivianVoiceDismiss`，靠 `identifier` 比对防重入）。`InterruptDialog` Postfix 则 `SkipAndResume()` 清挂起（`Patch_VivianVoiceInterrupt`）。

**关键的取舍**：`Patch_VivianVoiceAdvance` 里对 `identifier.StartsWith("vivian")` 直接 `return true`——**薇薇安自己的台词不挂起**（玩家可狂点跳过），只有配角/NPC 台词强制等配音播完。这是"不打断"和"别烦人"的折中，值得原样抄。

### 3.2 怎么做到不改经济、不碰机制

| 声称 | 代码事实 |
| --- | --- |
| 没有新物品 | 不订阅 `ModHook.OnModItemDirectoryInit`，全仓无 `Directory<GameItem>.Add`；`ModHelper.SpawnItem` 只有 1 处真用：`SpawnItem("common_electronic")`（`VivianEventClients.cs:1088`）。信纸用原版 `PreBuiltItemHelper.CreateHandnote(text,"","薇薇安",1)`（`:104`） |
| 不改价格 | 无 `unitValue` 写入；`Patch_VivianDiscountWatch` 对 `BargainUIManager.OfferDiscount` 只做 **Postfix 计数**，不动折扣本身 |
| 不碰经济 | `SetBudget` 只作用于**自家客人**（`:233`、`:301`、`:890`、`VivianSides.cs:215/277`）；全仓唯一写 `PlayerStore.IsPropertyPaid` 的地方是**调试跳章**（`VivianEvents.cs:37`，由 `VivianDebug` 调用），正常流程要求玩家真的买下店铺 |
| 不碰机制 | 0 处 Transpiler；38 个补丁全是 Prefix/Postfix，绝大多数只是"不是我就 `return true`"或只读旁观；**真正动数据的只有 4 类**：`Patch_VivianI18nLine` 改 `Dialogue` 本体、`Patch_VivianAfterBuy` 换 `__result`、`Patch_VivianVoiceSchedule` 包 `ref Action`、`VivianOptions` 往选项表注册三项 |
| 避免冲突 | 顾客 id 全部带前缀 `vivian_*`，另加 6 个具名 NPC id（`e2_mentor`/`e8_mentor`/`loan_shark`/`courier_e10`/`robber_e7`/`record_keeper`），集中在 `VivianI18n.IsOurs`（`:99-126`）一处判定；不给原版顾客改名、不改原版对话树 |

**"用原版字段代替补丁"的范式**（这是最值钱的部分）：`VivianGun.Setup`（`VivianGun.cs:5-15`）只写 `StoreClient` 的 6 个公共字段——`forceShootable / survivesShot / holsterResumesDialogue / unholsterDialogue / shotSurvivedDialogue / onShotDead`——就实现了"朝她掏枪有专属对白、开枪也打不死"；关店拦截用 `client.tryInterceptDismiss`（`VivianEventClients.cs:1166`）；成交后对白用 `client.allDoneDialogue` 覆盖（`Patch_VivianAfterBuy`）。

### 3.3 Patch 靶点表（38 处 / 15 个类 / 0 Transpiler）

| 靶类 | 处数 | 补丁形态与目的 |
| --- | ---: | --- |
| `DialogUIManager` | 9 | `DisplayClientText`(配音+立绘+翻译 3 处 Postfix/Prefix)、`DisplayerPlayerText`、`DisplaySecText`(2)、`ContinueConversation`(挂起推进)、`InterruptDialog`(清挂起)、`ScheduleFlow`(包 `ref Action`) |
| `PlayerStore` | 5 | `OnStoreOwned`(只记 `buyout_day`)、`DismissCurrentClient`(挂起离开 / 事件守卫两处) |
| `StoreClientDialogList` | 5 | `ClientAlarmInitialReactionDialog`、`ClientArrestDialog`、`CreateShutterDownClose`、`CreateAfterBuying/Selling…`(替换 `__result`) |
| `StoreClient` | 4 | `StartMainDialogue`(凝视/对峙拦截)、`CompleteClientCreation`(客户端 i18n) |
| `AppraisalServiceHelper` | 4 | `GetAcceptEstimate / GetPatienceRunOut / GetTooHigh / GetTooLow` 四个鉴定对白（E5「一只铁手」） |
| `StoreShutterButton` | 2 | `OnClick`（抢劫当天的关店流程）、`EndAndClose`（收尾） |
| 其余 9 个类各 1 处 | 9 | `StoreBell.RingBell`、`StoreUIManager.QueueClientArriveAnimation`、`StoreSecButton.OnClick`、`Shotgun.OnClick`、`LockHelper.UnlockContainer`、`BargainUIManager.OfferDiscount`、`InsStampHelper.CreateConfrontDialogue`、`InsCigaretteHelper.CreateConfrontDialogue`、`GameOptionUIManager.OpenUI` |

### 3.4 表现层：8 表情 / 2 CG / 2 个"彩蛋"

| 件 | 实现 | 位置 |
| --- | --- | --- |
| **8 表情** | `OnModAssetsLoaded` 时把 `Textures\Items\femaleUpperVivian4x{,_serious,_happy,_smug,_embarrassed,_annoyed,_sad,_wounded}.png` 逐张 `Sprite.Create`（pivot/PPU 抄原版 `femaleUpper1`），塞进 `SpriteDict.Instance.spriteDictionary`；切表情只 `StoreClientMono.Instance.image.sprite = …`，**零补丁**。表情由 `Data/expr-map-<lang>.txt`（`表情<TAB>台词`）按行文本查 | `VivianPortrait.cs:20`、`:97-166`、`:338-347`、`:405-416` |
| **2 张 CG** | `VivianCg.Capture(LoadedMod)` 把 `mod.Sprites["cg_vivian_true"/"cg_vivian_rival"]` 收进字典；`Show(key)` **自建一个 `sortingOrder=30000` 的 overlay Canvas**（`CanvasScaler` 1920×1080 + 黑底 `Image` + 可点关闭的 `Button` + 图片 `Image`），顺带 `VivianBgm.Play()`；`Hide()` 销毁 | `VivianCg.cs:28-49`、`:70-119`、`VivianBgm.cs:13-60` |
| **2 个彩蛋（推测所指）** | 两个"你对她做过坏事"的标记：`report_vivian`（按店里警铃举报她）、`gun_vivian`（朝她掏枪）。任一置位后，**之后每一幕开场**都会经 `WithIncidentSnark` 在最前面插一句她的一嘴（“……还有，别动不动就按警报。”/“……这次你可别再乱摸你柜台底下那把破枪了。”），各只播一次（`*_said` 布尔） | `VivianEventClients.cs:36-66`、`VivianGun.MarkGun`、`VivianRobbery` |

三个部件都**没有改 `SpriteDict` 之外的原版数据**：表情是往原版字典里加 key，CG 是自建 Canvas，彩蛋是往自己的 `ModData` 写一个 bool。顺带说明"不改经济"的边界到这里也守住了——她挨枪不会死（`survivesShot`），也不会掉钱。

## 4. 关键代码摘录

**① 自解码 WAV（`VivianVoice.cs:535-595` 核心 20 行）**——为了拿 `clip.length`

```csharp
int fmt = 1, ch = 22050, bits = 16, dataAt = -1, dataLen = 0, len = 0;
for (int i = 12; i + 8 <= b.Length; i += 8 + len + (len & 1)) {
    string id = Encoding.ASCII.GetString(b, i, 4); len = BitConverter.ToInt32(b, i + 4);
    if (id == "fmt " && i + 24 <= b.Length) {
        short tag = BitConverter.ToInt16(b, i + 8); ch = BitConverter.ToInt16(b, i + 10);
        int hz = BitConverter.ToInt32(b, i + 12); bits = BitConverter.ToInt16(b, i + 22);
        if (tag != 1 || bits != 16) return null;              // 只认 PCM 16bit
    } else if (id == "data") { dataAt = i + 8; dataLen = Math.Min(len, b.Length - dataAt); }
}
float[] f = new float[dataLen / 2];
for (int j = 0; j < f.Length; j++) f[j] = BitConverter.ToInt16(b, dataAt + j * 2) / 32768f;
AudioClip clip = AudioClip.Create(name, f.Length, ch, hz, false);
clip.SetData(f, 0);
```

**② 等播完再放行（`VivianVoice.cs:419-427` + `:350-360`）**

```csharp
private static IEnumerator WaitRoutine() {
    while (Time.realtimeSinceStartup < _playEndAt
           && _source != null && _source.isPlaying) yield return null;
    _waitRoutine = null;
    Flush();                                   // 依次执行 pendingContinuation / pendingLeave
}
internal static void WaitThen(Action next) { _pendingContinuation = next; EnsureWaiter(); }
internal static void HoldLeave(Action leaveNow) { _pendingLeave = leaveNow; EnsureWaiter(); }
```

**③ 只挂起 NPC、不挂起薇薇安（`Patch_VivianVoiceAdvance`，原版那 6 行 `instance==null` 判空已压成 `?.`）**

```csharp
private static bool Prefix() {
    if (!VivianVoice.IsPlayingAny) return true;
    var c = PlayerStore.Instance?.currentClientInstance?.GetClientBlueprint();
    if (c == null || c.identifier == null || !VivianVoice.IsOurSpeaker(c.identifier)) return true;
    if (c.identifier.StartsWith("vivian", StringComparison.Ordinal)) return true;   // 自己的台词可秒点
    VivianVoice.WaitThen(() => DialogUIManager.Instance?.ContinueConversation());
    return false;
}
```

**④ 翻译是"改 Dialogue 本体"，不是换 UI 文案（`VivianI18n.cs:293-324`）**

```csharp
string[] t = TAll(node.plainText);        // 一句中文可以对应多段译文
if (t.Length <= 1) { if (t[0] != plainText) node.SetText(node.title, t[0]); return; }
if (!flag) { node.SetText(node.title, t[0] + t[1]); return; }   // 有选项：两句拼一行
Dialogue second = new Dialogue();          // 无选项：拆成两个连续节点，先原文后译文
second.SetText(node.title, t[1]);
second.priority = node.priority;
second.endAction = node.endAction;  second.endActionDelay = node.endActionDelay;
second.nextDialogue = node.nextDialogue;
node.endAction = null;
node.SetText(node.title, t[0]);
node.SetNextDialogue(second);
```

## 5. 踩坑与红线

1. **翻译表与配音表都拿中文原文当键**（`i18n-en.tsv` 第 1 列 = 中文原句；`voice-map.txt` 第 2 列 = 中文原句）。改台词改一个字 → 译文与配音**同时静默失配**，游戏里表现为"这句话没声音了"，只有日志里一行 `voice map loaded` 看不出差异。要做同类功能，至少加一条启动自检（`代码里的台词集合` − `voice-map 键集合` = 空）。
2. **`_clips` 以文件名为键**（`:511`）：两句不同台词若指向同一个 wav 就共用一条缓存，改配音必须换文件名，别指望"重载"。
3. **`Patch_VivianVoiceDismiss` 在 Prefix 里重调 `DismissCurrentClient`**（先 `InterruptDialog()` 再原样调自己），靠 `identifier` 比对保证只重入一层。这是本模组唯一"自己调自己"的补丁，抄它必须带那层 id 校验。
4. **抢警铃/猎枪的点击会被吞**：`Patch_VivianAlarmLock`/`Patch_VivianShotgunLock` 在 `VivianRobbery.Active` 时 `return false` 并改播威胁台词，玩家会以为"按钮坏了"。**给反馈**（台词/抖动）是这类"接管交互"的最低要求。
5. **三份 `expr-map` 必须与三份 `i18n` 同步**：立绘表情是按**当前语言的行文本**查表（`VivianPortrait.SetForLine`，`:388-395`），而 `Patch_VivianI18nLine`（Prefix，翻译）先于 `Patch_VivianVoiceClient`（Postfix，配音+表情）执行——顺序对了才查得到 `expr-map-en/ja`。改任一 Harmony 顺序都会让表情全变 neutral。
6. **451 行调试台留在正式包**：Ctrl+F9 系列（加钱 +10000、跳明天、重置排班）＋ **43 个** `ModHelper.AddDebugFunction` 按钮，`<Capabilities/>` 仍是空的，但玩家按得到。可接受（我们自己的开发版也这样），但发布候选至少要确认它不动存档结构。
7. **对外口径三套**：manifest 19 幕 / 调试台 12 幕 / F11 面板 15 行。做档案或移植时以 `VivianJournal.cs:420-429` 那张数组和 `VivianSchedule` 的 stage 常量为唯一事实。

## 6. 对我们的可用点

> 对应项目：`深空当铺：可能是群友`（`freya_cornell`）、`深空电玩`（`deepspace_arcade`）

| 可用点 | 搬到哪 | 怎么搬（点名到接口） |
| --- | --- | --- |
| **"幕 = 整数 + switch 工厂"的剧情骨架** | 深空当铺（开局剧本 / 关系网主线）、深空电玩（街机剧情） | 照 `VivianSchedule.OnGenerateEarly`：`story_stage` + `next_day` 两键驱动 `manager.AddClient(...)`；幕内分支用 `Dialogue.AddChoice(DirectoryMaster.Item("$",true), "", "", branch, cb, null)`；跨幕分支只落布尔事实，不建树 |
| **三语配音 + "没播完不许走"** | 深空当铺（顾客配音、到账语音升级版） | 直接抄 `VivianVoice.DecodePcmWav` + 自建 `AudioSource` + `_playEndAt = length + 0.5`；挂起点只需 3 个：`DialogUIManager.ContinueConversation`(Prefix)、`PlayerStore.DismissCurrentClient`(Prefix 重入)、`DialogUIManager.InterruptDialog`(Postfix 清挂起)。BGM 那种 fire-and-forget 走 `ModHelper.GetSoundIdentifier` + `AudioManager.audioDirectory.GetAudioClip`（`VivianBgm.cs:22-38`） |
| **8 表情 = 注入 `SpriteDict` + 文本表情表** | 深空当铺（立绘/关系网）、深空电玩（角色表情） | `VivianPortrait`：`SpriteDict.Instance.spriteDictionary["femaleUpperVivian4x" + 后缀] = Sprite` 直接塞（不写补丁），切表情只 `StoreClientMono.Instance.image.sprite = …`；表情由 `Data/expr-map-<lang>.txt`（`表情<TAB>台词`）决定，一句一查 |
| **F11 式剧情记录面板 + 选项页注册** | 深空电玩（进度/成就面板）、深空当铺（档案柜） | `CustomUIManager.Window(id, 标题, "rear")` + `BeginTabs/BeginTab/AddRow`；热键与开关用 `GameOptionRegistry.GetOrAddSection / Toggle / KeyBind / Dropdown`（`VivianOptions.cs:45-180`），**键位不用自己轮询**——它自己的轮询只留给 Ctrl+F9 调试组合 |
| **"加东西但不碰经济"的五条手法** | 深空当铺（剧情奖励、线索道具） | ①信纸用 `PreBuiltItemHelper.CreateHandnote(...)` 而不是注册新物品；②投放用 `GraphUtils.TryAcceptAll(inv, item, 1)` + `ModHelper.Delay` 重试（`VivianEventClients.cs:111-166`）；③给客人加设定只写 `StoreClient` 公共字段（`forceShootable`/`allDoneDialogue`/`tryInterceptDismiss`）；④溢价类效果走 `ItemFeature`，不写 `unitValue`；⑤新顾客 id 一律 `前缀_` + 集中一处 `IsOurs` 判断 |
