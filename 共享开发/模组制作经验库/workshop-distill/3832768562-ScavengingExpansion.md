# 3832768562 Scavenging Expansion（拾荒拓展：背包、技能与焚化炉）｜蒸馏档案

> **工坊 id**：`3832768562` · `eotn_scavenger_skills` · ModVersion **2.0.9** · 署名 EOTN；`manifest.xml` 自闭合空 `<Capabilities />`、单程序集 `ScavengerSkills.dll`。描述称「支持九种模组语言，可在配置里手选韩语」——实际 9 语同级且 `ko` 早在表内（`L.cs:13`），这句是历史残留。
> **反编译目录**：`缓存文件夹\decomp_ws\3832768562\`（190 个 `.cs` / **24,814 行**、109 张内嵌 PNG、27 个 `.trans`＝3 表 × 9 语、1 个 `.csproj`；透视报告 `缓存文件夹\decomp_ws\_reports\3832768562.txt`）。
> **为什么值得读**：全工坊**唯一**把「39 项技能 × 三系树状自绘 UI × 物品自带可扩容容器 × 现场改造原版地面」四件事装进同一个 2.5 万行包的样本。三个手法直接能用：① **技能是一张 39 行静态表 + 一堆 `Id == "xxx"` 的 getter**，树/前置/上限/费用全从 Id 推，没有 39 个类；② **容器形状存成 `'0'/'1'` 字符串 tag，并在 `SaveManager.DecodeNodes` 之前从 `SaveItemNode.itemState.dict` 抢读**，读档才能一次建出正确窗口；③ **改原版结构前先验「形状原封未动」，一旦别人动过就永久让位、绝不搬一格物品**。

## 1. 规模与结构

| 项 | 值 |
| --- | --- |
| 源文件 | 190 个 `.cs`（`ScavengerSkills` 180 / `.Characters.Alice` 6 / `.Characters` 2 / `.Characters.Jin` 1 / `Properties` 1）；`netstandard2.1`、`LangVersion 14.0` |
| 行数 | 24,814（`ScavengerSkills` 20,205 / Alice 2,840 / Jin 1,625 / Characters 132） |
| Harmony / 反射 | **65 处特性式 `[HarmonyPatch]`（65 文件）**：17 个文件写 `bool Prefix`（**14 个会 `return false`**）、25 个写 `void Prefix`、37 个有 Postfix、**8 个 Finalizer**、**0 Transpiler**、**0 处 `new Harmony()`**；反射只 4 文件，唯一硬反射是 `AccessTools.Method(typeof(ItemMouseDragHandler), "AbortDrag")`（`FurnaceSingleDropPatch.cs:10`） |
| 资源 | 109 张 PNG 全部 `GetManifestResourceStream` 内嵌（**不进游戏贴图目录**，零能力友好）；`Localization`/`Characters.Alice`/`Characters.Jin` 三张 `.trans` × 9 语 |
| 官方接口 | `ModHook` 13 种、`ModHelper` 17 个；UI 触点 `CustomUIBuilder`/`CustomUIManager`/`PixelWindow`/`PaperUI`/`InspectionUIManager` |
| 热重载 | **无** `HotReloadPrepare/Activate` —— 见 §4 红线 1 |

**目录→行数→职责**

| 域 | 文件 / 行数 | 职责 |
| --- | --- | --- |
| 拾荒 12 + 加成 12 | 33 / 4,510 | **拾荒现场**：`ScavengingControls`/`ScavengingEntry`/`Search*`/`ScavOverflow`/`ScavGround`/`ScavengingAccess`/`ScavengingHud` + 11 个 `*Patch`（一键搜索/拾取、扩地、溢出补入、经验条、掉落台账）；**拾荒加成**：`DiscoveryLoot`/`InsightLoot`/`BattlefieldLoot`/`BuridanLoot`/`ToolRoll*` + 6 个 `*Patch`（违禁品/枪械/战场遗址/慧眼识珠四路额外掉落） |
| 技能模型 + UI | 14 / 4,190 | 42 行静态表 + 760 行存档读数/校验/洗点/迁移 + 99 行公式；1,397 行手工排版窗（三页树 + 详情 + 缩放平移 + 悬停动画）、300 行主题、46 行设置窗 |
| **焚化炉** | 19 / 3,702 | **最大单域**：自绘屏幕像素炉 + 修理 + 冶炼/热力结算 + 产出暂存 + 夜间好友剧情 |
| **背包** | 21 / 3,032 | 物品/形状/改造件/工具槽/卸货/补购/伪装格 |
| **角色剧情** `AliceModule` `JinModule` `StoryRules` `PartsRoll` `CharacterPhones` | 9 / 4,597 | 艾丽丝（改造件供应 + 教学）与金（冶炼课 + 配方书），本档案不展开 |
| 骨架 `Core` `Settings` `L` `LanguageRuntime` `FramePatch` `FrameTasks` `PanelDriver` `F8Bridge` `F8ProtocolState` | 9 / 1,432 | 生命周期、配置、9 语兜底、逐帧任务、跨模组 F8 桥 |

**行数怎么花掉的**：**不是**「每项技能一个类/一个文件」。39 项技能的**数据**只占 42 行（`SkillCatalog.cs:7-48`），行为加起来 <400 行；2.5 万行主要在三块：① **自绘 UI**（技能树 1,397 + 焚化炉表现 746 + 状态/冶炼/结果窗 ~1,500）；② **焚化炉玩法链**（热值表、修理、产出暂存、夜间剧情 ≈ 2,400）；③ **两条角色剧情**（4,597）。补丁层极薄：65 个 patch 文件多为 10~50 行。上表按**主要域**归拢（行数含该域 `*Patch`），另有约 3.3k 行散落在 ≤40 行的小 patch 与 `ReleaseNotice`/`Diagnostics`/`SettingsWindow` 这类零散工具里，不单独列。

## 2. 它怎么做（点名到原版类与方法）

### 2.1 39 项技能：一张表 + 一堆 Id 分支的 getter（第 1 问）

**技能是「类」，但只有一个类**：`SkillDefinition`（422 行）无子类、无策略对象，全部差异写成按 Id 判定的属性。

| 概念 | 怎么表达 | 位置 |
| --- | --- | --- |
| 三系归属 | `Tree` getter：`IsFurnace`→`Furnace`；`bag/tools/ergonomics/disguise`→`Backpack`；其余→`Scavenging`。**没有独立的树结构数据** | `SkillDefinition.cs:89` |
| 树节点 vs 列表叶 | `IsTreeNode` / `IsRecipe` / `IsQuantity` / `IsUnlock` / `IsDiscovery` / `IsCombustion`，全是 `StartsWith` 与 `Id ==` | `:73-124` |
| 前置 | `Parent` getter：8 层嵌套 `if` 拼出的 DAG；`quantity_ingot`→`smelt_start`，其余 `quantity_*`→`"recipe_" + Id.Substring(9)` | `:172-218` |
| 前置等级 | `ParentRequiredRank`：默认 1，**只有 `burn3` 要 `burn1` 满 10 级** | `:220` |
| 等级上限 | `MaxRank` getter：`smelting`30 / `tools`9 / `disguise`13 / `quality`5 / `IsUnlock`1 / `IsDiscovery`3 / `IsCombustion`10 / 其余读配置（**0＝无上限**） | `:260-302` |
| 费用 | `Cost`+`CostStep`+`CostAtRank`/`CostForRanks`；后者用 `SumSteps` 闭式算「每 N 级涨 1 点」的等差段 | `:304-404` |

**上限/费用是「硬编码 + 配置」混着的**：兜底是 `Balance.Get(ConfigId + ".max", DefaultMaxRank, 0, 1000000)`，`Balance.Read` 接到运行期快照（`Settings.cs:171`）；但 `tools=9`/`disguise=13`/`quality=5` 这些**硬编码分支直接短路配置**，`AddSkill` 也刻意不给它们生成 `.max` 项（`Settings.cs:192`）。改数值前先确认走哪条分支。第三套数是 `PricingRank(id)`：非 `disguise` 返回 `LearnedRank`、`disguise` 返回 `Rank`（`SkillProgression.cs:302`）——因为 `Rank` 含「剧情/改造件白送的等级」（`alice.<id>`、`story.<id>`），白送的不该涨价。

**预加点（Ctrl+5 / Shift+10）不是直接扣点，是草稿 → 批量校验 → 一次性提交**：`ClickRanks()` 给级数 → `SkillDraft.Additions` 里**每加一级就用 `ValidateBatch` 全量复验**，失败回滚该级（`SkillDraft.cs:18-35`）→ 点确认才 `Commit`。批量校验必须把「同批里先加的前置」算进去：`EffectiveRank(parent) + additions[parent]`（`SkillProgression.cs:684`），否则「一次加 10 级 `smelt_start` 再加 `recipe_parts`」会被误判前置不足。`SkillDraft.Remove(live,id)` 反向级联：删前置后循环删除失效后代（`:62-79`）。

**存档**：全落 `ModHelper` 私有键，**不碰原版存档结构**（`GameSkillStore.cs:9-28`）：`skills.xp` / `rank.<id>` / `story.<id>` / `alice.<id>` / `spent` / `furnace.earned|spent` / `tree.*`。**等级不存**，由 xp 二分反推（`SkillProgression.cs:48-70`）。

### 2.2 F8 窗口：`CustomUIBuilder` 建窗 + 关布局组手摆 + 自造 viewport（第 2 问）

**基类不是 `PixelWindow` + 排版三段式**：F8 走 `CustomUIManager.Instance.CreateWindow(...)` → `CustomUIBuilder` → `.Handle` 是 `CustomUIWindow`（`SkillPanel.cs:279`）；`PixelWindow` 只用在一处——给背包挂「工具槽」原版机械窗（`BackpackTools.cs:105-158`）。排版（`SkillPanel.cs:1069-1130`）：① `SetSize(1030, 730+增高).Center().SetDraggable(true).SetCloseOnEscape(true)`，先按根 Canvas 实尺寸算 `min(1.08, w/1030, h/730)`，再拉高最多 210px，让 1080p 与 720p 都放得下；② **把 `ContentRoot` 上所有 `LayoutGroup` 与 `ContentSizeFitter` 全部 `enabled=false`**，此后一切走 `Place()` 绝对定位（锚 `(0,1)`、pivot `(0,1)`、`anchoredPosition=(x,-y)`）；③ 子元素**必须先由 builder 加进 `ContentRoot`**，再整体 reparent 到页：`while (ContentRoot.childCount > childCount) GetChild(childCount).SetParent(page)`（`:1062`）——builder 只能往 `ContentRoot` 追加，这是自绘窗口的必踩点。

**缩放/平移不抖的三件事**（`SkillTreeViewport.cs`，205 行，实现 `IScrollHandler/IBeginDrag/IDrag/IEndDrag`）：

| 手法 | 代码 |
| --- | --- |
| 指针锚缩放 | 先取指针局部坐标当 pivot，再 `anchoredPosition = pivot - (pivot - pos) * (newZoom/zoom)`，**最后**才 `localScale`（`:46-84`）。顺序反了就跳 |
| 指数缩放 | `zoom * Mathf.Pow(1.12f, e.scrollDelta.y)`（`:32`）：每格固定 12%，与滚轮 DPI 无关；±按钮复用同因子（`SkillPanel.cs:1192-1199`，缩小用 `25f/28f` 让 100% 能回正） |
| 上下限 | `Clamp(v, min(0.5, w/contentW, h/contentH), 2.5)`：下限保证图永远填满视口 |
| 拖动 | 取 `e.position` 与 `e.position - e.delta` 两次局部坐标，差值加给 `anchoredPosition`（`:148-170`）——用局部坐标而非屏幕坐标，DPI 缩放也跟手 |
| 每帧兜底 | 缩放/拖动后立刻 `Clamp()`：x∈[-(contentW*zoom)+40, viewW-40]、y∈[-viewH+40, contentH*zoom-40]（`:177-192`），永远留 40px 图在视口内 |
| 拖动≠点击 | `OnBeginDrag` 置 `e.eligibleForClick=false`（`:92-98`），配 `SkillIconInput.OnPointerClick` 开头 `if (data.dragging) return`；三棵树各建一个页 `RectTransform`，`pan.Content` 指向当前页，切页 `ResetView()` 后再 `SetActive`（`SkillPanel.cs:1296-1301`） |

**节点布局是算法不是手填**（`:886-1068`）：有前置的树走 `LayoutBranch` 递归（`x = 24 + lane*228`、`y = 12 + depth*184 + lane%3*9`，**最后那 9px 是故意加的行抖动**）；无前置的拾荒 12 根走网格（4 列、每列错 `num2%2*38`、按 `abs(2-num3)*6` 起拱）。连线是自绘 `MaskableGraphic`：`SkillRootLine.OnPopulateMesh` 用三次贝塞尔采 6~28 段生带状三角面，线宽从 4 收到 1.5（`SkillRootLine.cs:14-37`）。

**悬停像素动画 = 1 张 16 帧横排图集 + 每帧独立时长表**（`SkillIcons.cs`）：静态图 `SkillPoster.<id>.png`（64×64），动画 `SkillArt.<id>.png`（**64×16 宽**，`:100`）；`frameDurations[16] = {210,100,95,90,85,85,90,110,195,110,90,85,85,90,95,145}`（`:14`）累计成 `cycleDuration`，`elapsed*1000 % cycleDuration` 逐段扣减定位（`:87-96`）——**不做等分，节奏有呼吸感**，这是像不像素味的关键；**只缓存一只**：换目标即 `EndHover()` 把上一只 16 个 Sprite 与整张 Texture `Destroy`（`:97-107`、`:119-133`），峰值 ≈ 1 张 1024×64 RGBA。驱动是**帧回调不是协程**：`PanelDriver.LateUpdate → FrameTasks → SkillPanel.Update → TickHover`（`PanelDriver.cs:58-67`、`SkillPanel.cs:194-250`、`:645-655`）。`LoadTexture` 会校验尺寸、`filterMode=Point`（`:45-50`），缺图直接抛 `InvalidDataException`。

**点选读故事 + 输入互斥**：`Select → selected=id → Dirty=true → Refresh()` 重刷右侧详情：`detailFlavor`（`L.Get("flavor."+id)`，`SkillPanel.cs:394`）、`detailDescription`、`detailGate`（前置/费用/上限）、`detailStatus`；文本块是带 `RectMask2D` 的 `ScrollRect`，`SetScrollText` 用 `TMP.GetPreferredValues` 算高再 `SetSizeWithCurrentAnchors`，换文本时 `StopMovement()` 回顶（`:742-790`）。开窗即 `BeginInputShield()`（`:630-643`）——对 `ItemMouseDoubleClickHandler.current`/`ItemDefaultActivateHandler.current` 做 `UpdateReset()`、把「面板可见」记进 `PanelInputShield`，否则点图标会连带触发原版物品双击使用。

### 2.3 背包：一个物品 + 自带窗口，形状存在自己身上（第 3 问）

**是「一个物品 + 自带 inventory」，不是原版容器变体**：`ItemDirectory.CreateEmptyItem("eotn_scavenger_backpack")` + `DirectoryUtils.CreateInventoryWindow(map, width, true)` + `ContainerHelper.InitBackpackItem`（`Backpack.cs:130-152`）——形状与宽高在**建窗那一刻**就得给出，这正是读档难点。

| tag | 类型 | 语义 |
| --- | --- | --- |
| `SCAV_BAG_MAP` | string | 行主序 `'0'`（可用）/`'1'`（挖掉），长度 = 宽×高，上限 1600（40×40） |
| `SCAV_BAG_WIDTH` | int | 每行字符数（3~40）；没有高度字段，`len / width` 推 |
| `SCAV_BAG_PROGRESS` | string(long) | **每 10000 单位解锁 1 格**的余数 |
| `SCAV_RETROFIT_SUPPORT`/`_FRAME`/`_TOOLS` | int | 支架加格 / 允许收机器 / 工具槽硬件数 |
| `SCAV_RETROFIT_KIND` | int | 改造件类型（`Plate/Support/Liner/Frame/Tools`，`BackpackUpgrade.cs:3-10`） |

**3×3 起步 + 逐格扩容**：上限格 `LimitWithSupport(level, supports)` 从 5×5 起（`bag.baseColumns/baseRows=5`），形状只点亮 9 格 → 可用与视觉都是 **3×3**；点亮顺序由 `Order()` 决定：先铺满中央 3×3，再按「对角线 L 形」向外扩，所以扩容是**从中心长出去**而非从左下补（`BackpackRules.cs:96-108`）。`Progress / 10000` 决定这轮点亮几格（`Backpack.cs:225-238`）；材料折算 `BackpackRules.MaterialUnits`（`junk`2500 / `scrap_metal`5000 / `rare_electronic`125000 / `metal_ingot` 按纯度 5~10 档 20000~35000……`:110-170`）；`NeededUnits` 只补「缺的格数」，多给的材料不吞（`:172-180`）；上限由等级推（5 级一档、先加列后加行、封顶 12×12，`:19-53`），支架可提前买一档。

**「拖材料到图标上、松开左键改造」挂的是原版 `GameItem.Target` 三链，不是自定义 drop 回调**：

```
ItemMouseDragHandler.EndDrag (原版 :636-642)
  └─ if (current.MayTarget(last) && current.CanTarget(last)) current.Target(last)
        ├─ MayTarget  ← Prefix MayTargetPatch  → Backpack.Match + CanSubmit
        ├─ CanTarget  ← Prefix CanTargetPatch  → 同上
        └─ Target     ← Prefix TargetPatch     → Backpack.Match + Queue，return false 吃掉原版
```

`Backpack.Match(材料, 背包)`（`Backpack.cs:284-305`）是「算不算改造动作」的唯一判定：目标是背包时要求 `SubmitMode`；`SubmitMode`（`:27-41`）＝**拾荒现场恒真**（`ScavengingEntry.Present`），**店内必须按右 Shift（KeyCode 303）**；目标是乌托邦实现器走另一支线（材料喂养）。**另一半挂在改造件自己身上**：`BackpackRetrofitApi.Configure(part, kind)` 给每种改造件装原版自带的三个委托位（`BackpackRetrofitApi.cs:77-89`）：

```csharp
part.mayThisTargetItemFunc = (s,b) => Core.SkillsActive && Backpack.IsBag(b);
part.canThisTargetItemFunc = (s,b) => Core.SkillsActive && Backpack.CanSubmit(b,s);
part.onThisTargetItemFunc  = (s,b) => Backpack.Queue(b,s);
```

**两条路都通**：材料走 Harmony 拦 `GameItem.Target`，改造件走原版 `mayThis/canThis/onThis` 委托位——后者完全不用 patch，是更干净的一半。**`Queue` 只入队，真正扣除在帧末 `Flush`**（`Backpack.cs:316-429`）：由 `InputActionManager.Update` 的 Prefix（`FramePatch.cs:10`，`before` 组）、`PlayerStore.SaveGame` Prefix（`SavePatch.cs:13`）、`GameMaster.QuitToMenu` Prefix（`QuitPatch.cs:10`）三处驱动。「松手」到「格数变化」隔一帧，这才能在单次拖拽里合并多件材料并合并成一条 `Notify`（`:424-428`）。配套防呆：`MaterialSlotPatch` 在 `GameItem.MayHaveValidInventorySlot` 里把「材料塞进背包」直接判 false（`MaterialSlotPatch.cs:8-20`）——同一手势的两种原版解释必须显式互斥。

**读档恢复（最巧的一段）**：`SaveManager.DecodeNodes` 的 **Prefix** 先遍历 `List<SaveItemNode>`，找 `identifier == "eotn_scavenger_backpack"` 的节点，**直接从 `savedItem.itemState.dict` 读原始 tag**（尚未解码成 GameItem），把 `(map,width)` 压进线程静态队列（`RestorePatch.cs:36-77`）；`Backpack.Create()` 里 `TakeLayout()` 取出，**一次建出正确形状**（`Backpack.cs:131-145`）；**Postfix** 再对 `bag.tempLink` 调 `Refresh` 补形状（`RestorePatch.cs:79-92`）。校验极严：宽 3~40、`len%width==0`、高 3~40、字符只许 `'0'/'1'`，任一不满足退回 `Shape(9,5,5)`（`:70-74`）。

### 2.4 拾荒现场：一键搜索/拾取 + 扩地守卫（第 4 问）

**一键搜索/一键拾取是现场自绘按钮**（非新窗口）：挂在 `EmporiumEntry.Instance.afterhourWindow` 上的 `MachineryButton`（`ScavengingControls.cs`，673 行），文字/帮助/tooltip 全走 `L.Get`，切语言整组重建。

| 机制 | 实现 | 出处 |
| --- | --- | --- |
| 一键搜索 | `ScavengingSearchSequence`（`remaining=10000` 保险）+ 反复 `ScavengingEntry.TryInvoke()` 点原版搜索按钮；**1 次/帧**（`ScavengingFrameBudget.MaxSteps=1`，那个 3ms 分支实际不可达） | `ScavengingSearchSequence.cs:7-16`、`ScavengingFrameBudget.cs:5-20` |
| 中断条件 | 每步前后查 `Ready/OwnerMatches/InputAvailable`，按住左右键即停；连续两次「观察不到消耗」就停，**不重试不回滚** | `ScavengingControls.cs:426-490` |
| 一键拾取 | 每帧 4 件：先 `ScavengingPocketApi.Collect` 进背包，再依次试左口袋/右口袋 | `:507-540` |
| 拾不完 | `ScavOverflow`：放不下的物品**保持 `parentInventory==null`**，每帧鼠标松开补投，失败留缓冲；另开分页窗（10 条/页） | `ScavOverflow.cs:60-120` |

**扩地守卫（本档案重点）**：`ScavGround`（472 行）把原版拾荒地面 `12×9` 换成 `12×15`，**只在五个条件同时成立时**才动手（`ScavGround.cs:58-101`）：

| # | 条件 | 含义 |
| --- | --- | --- |
| 1 | `owned == null && !leaseCeded` | 本次会话未接管过，且没因「别人改过」而永久放弃 |
| 2 | `ScavengingScene.IsAvailable && Settings.ExpandedGround` | 在拾荒现场（技能就绪 ∧ 搜索入口在场）∧ 玩家开了开关（**默认关**，`Settings.cs:127`） |
| 3 | `afterhourInventory != null && window.handler != null && window.IsVisible()` | 地面与窗口真活着、可见 |
| 4 | `childItems.Count == 0` | **地面必须是空的**（掉一件也不改） |
| 5 | `PlainShape(shape, 12, 9)` | **形状原封未动**：宽高正好 12×9、`minX/minY==0`、`orientation==0`、`!flipped`、越界哨兵 `GetLocal(-1,-1)==1`，且 12×9 每格都可用 |

`PlainShape` 的 `orientation/flipped/minX/minY` 四连检查是关键：`GridShape` 带旋转/翻转/原点，**只看宽高会被「旋转 90° 后仍是 12×9 的另一张图」骗过**。全绿才 `SetShape(12,15)` 并把 `expanded = grid.inventoryShape` 记下（`:92-93`）。

**归还三重让位**（`Restore(bool boundary)`，`:138-211`）：① **形状主权** `OwnsShape` 要求「仍是我设的那张满格 12×15」，被换/挖洞/旋转即 `leaseCeded=true` **永久放弃**（`:147`）；② **越界物品不搬不删**：缩小前遍历 `childItems`，凡 `modifiedShape` 不在原版 12×9 内就**不缩**，若仍在同一地点则 `suspended=true` 并挂 `OnAfterhourLeaveLate/ArriveEarly`，**等真离开场景再试**（`:145-173`）；③ 边界仍不行只打一行日志「不搬、不删、也不重试」（`:168-173`）后放弃。**窗口尺寸适配**：`Fit(EmporiumEntry)` 挂 `EmporiumEntry.Validate` 的 **Postfix**，`BeforeValidate` 在 **Prefix** 先把上次改的 `localScale/localPosition` 还原（`ScavGroundLayoutPatch.cs:9-24` → `ScavGround.cs:276-290`）——为的是「12×15 窗口在 720p 会出屏」：用 `GetWorldCorners` + `InverseTransformPoint` 算包围盒，按 `(canvasW-16)/bboxW`、`(canvasH-60)/bboxH` 取最小缩放，再推回距边 8px / 距顶 52px 的安全区（`:267-389`）。**任何改别人窗口 transform 的模组都必须有这对 Prefix/Postfix**。

### 2.5 经验、等级与两种点数（第 5 问）

| 项 | 规则 | 出处 |
| --- | --- | --- |
| 经验来源 | ①每真正消耗 1 次拾荒机会 → `xp.base=10`；②战利品 `TotalValue` → `ceil(value/5)`；③地点修正 `GetModifier("eotn_scavenging_xp_percent")`；④一次动作消耗 N 次时第 1 次带价值、其余只给基础经验 | `Skills.cs:55-84`、`SkillMath.cs:45-53`、`SearchLootCapture.cs:232-236` |
| 等级曲线 | 阈值表：`first=100`，每级 ×(1+5%)，到 `cap=1250` 后线性；差量表静态缓存，等级在 `[0, 607400097]` 上二分 | `SkillProgression.cs:48-70`、`:161-193` |
| 普通技能点 | **「等级即点数」**：`Points = max(0, Level - Spent)`，没有单独发点 | `:325-328` |
| 冶炼点 | 独立账本 `furnace.earned - furnace.spent`，只能点焚化炉系；烧垃圾按 `Requirement(rank)` 产出（基础 300 格、每级 -4） | `:82-86`、`FurnaceProgress.cs:49-75` |
| 可拖动经验条 | `ScavengingHud`：自建 `Canvas`（`sortingOrder=1`、参考 1920×1080）挂 `PanelDriver`，16 段填充；拖动记 `grab` 偏移，松手把**归一化坐标**写 `PlayerPrefs`（`…xpHud.x/y`，默认 0.6/0.035） | `ScavengingHud.cs:56-104`、`:200-222`、`:297-352` |
| 洗点 | `ResetQuote` 按树算「可退点数 + 价格（点数×50）」，每树首次免费（`furnace.freeReset` + 位掩码 `tree.freeResetsUsed`）；提交前**重算 quote 并要求完全一致**，防 UI 陈旧 | `:368-414` |

### 2.6 焚化炉：纯自绘的新物件，不是给原版机器加功能（第 6 问）

**没有任何原版机器被改 + 接料靠 Prefix 掐死原版拖拽终点**（不是注册 drop 目标）。`ScrapFurnace` 在拾荒现场建 `Canvas`（`ScreenSpaceOverlay`、`sortingOrder=-100`）+ 一张 `Image`，锚在 `afterhourWindow` 左下角、尺寸随窗口与屏幕算（`ScrapFurnace.cs:112-200`）；炉体是**像素图 + 50 个粒子 Image**（36 火星 + 14 烟）自己算轨迹、30fps（`:293-318`）。**命中判定逐像素 alpha**：`ScreenPointToLocalPointInRectangle` → `rect.Contains` → `texture.GetPixel(x,y).a >= 0.5f`（`:440-497`），前面还要排掉「鼠标下有原版 `PixelWindow`」「鼠标下有 `CustomUIWindowFrame`」两种遮挡。接料：`ItemMouseDragHandler.EndDrag` Prefix（`FurnaceSingleDropPatch.cs:12-29`）→ 关假背景 → 反射 `AbortDrag` → `Burn` → `return false`；`ItemMultiSelectHandler.EndGroupDrag` Prefix（`FurnaceGroupDropPatch.cs:10-21`）→ `Cancel()` → `Burn` → `return false`；悬停提示靠 `ItemTooltipHandler.OnEventUpdate` Postfix 自己 `OverlayHandler.ShowTooltip`（`FurnaceTooltipPatch.cs:9-24`）。

**「修好才能用」是它与其他焚化炉模组最大不同**：`FurnaceRepair` 需 **50000 单位**材料（`furnace.repair.units`），折算复用 `BackpackRules.MaterialUnits`，提交要求材料在当前地面且无子物品（`FurnaceRepair.cs:32-89`）；修好前显示 `scrap-furnace-damaged.png`，修好后换 `scrap-furnace.png` 并 `SmeltingPanel.Dock`（`ScrapFurnace.cs:202-211`）。而 `furnace` 技能节点的解锁条件就是这个 `Repaired`（`SkillProgression.cs:236-256`），所以**它不能靠点数买**（`CanLearn` 直接返回 `FurnaceRepair.help`，`:582-586`）。**产出链**：`FurnaceProgress`（`furnace.progress|pending|earned|smeltingEarned`）→ `FurnaceOutput`（暂存 `PixelWindow` + `Queue<GameItem>`）→ `FurnaceOutputSave`（**自写 JSON 进 `ModData`**：`SaveItemNode`+`ItemFeature` 列表，`FurnaceOutputSave.cs:36-148`）；夜间结算 `FurnaceNightRewards` 用 `heat_shock/undercity_friendship/power_abundance` 生成次日礼物快照（`furnace.friendship.gifts`）。

### 2.7 Patch 靶点表（原版方法 → Prefix/Postfix → 目的 → 风险）

| 原版方法 | Patch | 目的 | 风险 |
| --- | --- | --- | --- |
| `PlayerStore.LoadGame` / `SaveGame` | P + **Finalizer** / P | 置 `Loading` 清缓存；存盘前 Flush 三块 | **中**：Flush 抛异常会连累存盘（用 `RunStage` 逐个 try 兜） |
| `PlayerStore.StartNewGame` / `ResetData` / `GameMaster.QuitToMenu` | P / P+Post / P+Post | 清状态；新局 Postfix `ClearModData` **整包清空**；退出前 Flush | 低（整包 Clear 会连带清别的键） |
| `SaveManager.DecodeNodes` | P + Post + **Finalizer** | 预读背包形状 tag；解码后补形状 | **高**：读 `SaveItemNode.itemState.dict` 原始结构，存档格式一变就静默退回 5×5 |
| `InputActionManager.Update` | P + Post | 逐帧任务总管（12 个 Tick） | **中**：每帧必跑；`FrameTasks` 对失败任务 5s 内跳过 |
| `EmporiumEntry.Validate` | P + Post | 布局前还原 transform、布局后适配窗口 | **高**：改别人窗口 scale/position，必须成对 |
| `GameItem.MayTarget`/`CanTarget`/`Target` | P ×3（**都 return false**） | 「材料/改造件拖到背包上」唯一入口 | **高**：`Target` 是原版物品互用总闸，吃错废掉全部互用 |
| `GameItem.MayHaveValidInventorySlot` / `GameGridInventory.MayHaveValidInventorySlot` | P（false） / Post | 阻止材料被当成「塞进背包」；追加背包准入三规则 | 中 |
| `GameItem.SetSpriteAndShape(string,string)` | P `[HarmonyPriority(800)]`（false） | 与 `better_scavenging` 冲突时改走自家贴图 | 低（只认自家 atlas 名） |
| `RenderHandler.LoadFromAtlas(string,string)` | P（false） | 自家贴图不落盘也能显示 | 低——**替代了危险的 `SpriteDict.GetSprite`** |
| `ItemMouseDragHandler.EndDrag` / `ItemMultiSelectHandler.EndGroupDrag` / `ItemTooltipHandler.OnEventUpdate` | P（false）×2 + Post | 拖到炉子上即焚 + 悬停提示 | **高**：拖拽三链核心，必须 `AbortDrag`/`Cancel` 收尾 |
| `ScavHelper.ScavengeDumpingGrounds` / `SpawnDumpingGroundLoot` | P + Post + **Finalizer** ×2 | 记一次拾荒、算价值/经验；每次抽取判违禁品/枪械加成 | **高**：拾荒主循环，`ThreadStatic` 上下文栈靠 Finalizer 收（`AttemptPatch.cs:51-93`） |
| `ScavHelper.GetMaxScavAttempts`/`GetLuck`/`GetMinorWoundChance`/`GetMajorWoundChance`/`EventModifier` | Post | 五个技能加成 | 低 |
| `SlotMarker.TryAcceptOnce`/`StackItemUnchecked`/`GraphUtils.TryAcceptAll` | P/Post | 掉落台账：谁进了地面、进了几件 | **中**：`TryAcceptAll` 是全局投放口，靠 `IsGroundOffer` 白名单过滤 |
| `ContrabandHelper.RecursiveInspect` | P（false） | 伪装保护格内物品不被记为可疑 | **中**：查禁玩法核心，误判让玩家白赚 |
| `Button.Press` / `StoreUIManager.Notify` / `ResolveEscape` | P / P / P | 捕获原版搜索按钮点击；压掉批量重复提示；Esc 先停批量 | 中 / 低（**不碰 `StoreUIManager.Update`**） |
| `AudioManager.Play2` / `TMPLocalizer.OnLocaleChanged` / `ModConfigWindow.Open` / `GameOverUIManager.DisplayUI` | P/Post | 静音刷屏、切语言重刷、结局文案 | 低 |

### 2.8 已知热点核对（我们自己的冲突扫描清单）

| 热点 | 是否碰 | 说明 |
| --- | --- | --- |
| `SpriteDict.GetSprite` / `StoreUIManager.Update` / `PlayerStore.EndNight` | **全不碰** | 贴图走 `RenderHandler.LoadFromAtlas` Prefix（`SpritePatch.cs:7-22`，只在自己 atlas 名下短路，**这姿势可以抄**）；UI 只碰 `Notify`/`ResolveEscape`；夜间结算走 `ModHook.OnShutterClosedLate`（`Core.cs:119`）而非 patch `EndNight` |
| `GraphUtils.*` 拖拽三链 | **只碰 `TryAcceptAll` 一个 Postfix** | 且只记账，不改返回值 |
| `ItemMouseDragHandler` / `ItemMultiSelectHandler` | **碰，且 Prefix + return false** | 与所有拖拽类模组的最大冲突面（§4 红线 5） |
| `MachineDnaManipulator` / `ModuleHelper` | 不碰 | 与机器升级类模组零耦合 |

## 3. 关键代码摘录

**A. 技能表 + 前置/上限全是 Id 分支**（`SkillCatalog.cs:7`、`SkillDefinition.cs:172`）

```csharp
internal static readonly SkillDefinition[] All = new SkillDefinition[39] {
    // 拾荒 12：thorough … firearms；背包 4：bag/tools/ergonomics/disguise
    // 焚化炉 23：furnace/smelt_start/heat_start/quality/smelting/quantity_*6/recipe_*5/
    //           burn1/burn3/power/thermal/heat_shock/undercity_friendship/power_abundance
    new SkillDefinition("thorough", "满载而归", "每级增加1%额外抽取概率…", 0, 1, 1, 0),  // maxRank=0 → 无上限
    new SkillDefinition("endurance", "野外耐力", "每级增加1次拾荒机会…", 7, 3),
    new SkillDefinition("tools", "多功能背包", "每级增加一个工具专用槽…", 9, 1, 0, 0),
    new SkillDefinition("quantity_weapon", "skill.quantity.weapon.name", "Settings.weapon.quantity",
                        5, 1, 0, 0, nameIsKey: true),   // 该开关决定查表还是用字面量
};

// Parent getter 骨架（:172-218）：焚化炉 → null（唯一根）；heat_shock/burn1/smelt_start/heat_start → "furnace"；
// recipe_*/quality/smelting → "smelt_start"；quantity_* → "recipe_" + Id.Substring(9)（quantity_ingot 例外）；
// tools/ergonomics/disguise → "bag"；thermal/power → "heat_start"；其余 → null。
internal int ParentRequiredRank => (Id == "burn3") ? 10 : 1;   // 只有极致燃烧要满级前置
```

**B. Ctrl 5 级 / Shift 10 级 + 双击**（`SkillIconInput.cs:34`、`:47`）

```csharp
internal static int ClickRanks() {
    if (Input.GetKey((KeyCode)304) || Input.GetKey((KeyCode)303)) return 10;  // LeftShift / RightShift
    if (Input.GetKey((KeyCode)306) || Input.GetKey((KeyCode)305)) return 5;   // LeftControl / RightControl
    return 1;
}   // OnPointerClick：data.dragging 直接 return；右键 Remove 撤 1 级；左键先 Select（→ 读故事）
    // 再 Add(ClickRanks())，仅 num > 1 或 data.clickCount == 2 才真的加点。
```

**C. 缩放不抖的那 12 行**（`SkillTreeViewport.cs:46`）

```csharp
private void SetZoom(float value, Vector2 pivot) {     // pivot = 指针在 View 内的局部坐标
    float min  = Mathf.Min(new float[3] { 0.5f, View.rect.width / Content.rect.width,
                                                View.rect.height / Content.rect.height });
    float num2 = Mathf.Clamp(value, min, 2.5f);
    Vector2 pos = Content.anchoredPosition;
    Content.anchoredPosition = pivot - (pivot - pos) * (num2 / zoom);   // ① 先补偿位移
    zoom = num2;  Content.localScale = Vector3.one * zoom;              // ② 再改缩放
    Clamp();                                                            // ③ 最后夹紧
    Changed?.Invoke(zoom);
}
public void OnScroll(PointerEventData e) {              // 每格 12%，与滚轮 DPI 无关
    Vector2 pivot = default;
    RectTransformUtility.ScreenPointToLocalPointInRectangle(View, e.position, e.pressEventCamera, ref pivot);
    SetZoom(zoom * Mathf.Pow(1.12f, e.scrollDelta.y), pivot);
    e.Use();
}
```

**D. 16 帧带节奏的悬停动画**（`SkillIcons.cs:14` + `:87`）

```csharp
private static readonly int[] frameDurations = { 210,100,95,90,85,85,90,110,195,110,90,85,85,90,95,145 };
private static readonly int cycleDuration = ArraySum(frameDurations);   // 1,575ms，16 帧不同时长
private static readonly Sprite[] frames = new Sprite[16];               // 按需惰性创建

internal static Sprite HoverFrame(string id, float elapsed) {
    int num = (int)(elapsed * 1000.0 % cycleDuration);
    int num2 = 0;
    while (num2 < frameDurations.Length - 1 && num >= frameDurations[num2]) num -= frameDurations[num2++];
    if (num2 == 0) return Get(id);                    // 第 0 帧复用静态图，省一次贴图
    if (activeId != id) {                             // 只缓存「当前悬停的那一只」
        EndHover();                                   // 销毁上一只 16 张 Sprite + 整张 Texture
        activeAtlas = LoadTexture("SkillArt." + id + ".png", 64 * frames.Length);  // 64×16 横排
        activeId = id;
    }
    if (frames[num2] == null) frames[num2] = MakeFrame(activeAtlas, num2);   // MakeFrame 每次切 Rect
    return frames[num2];
}
```

**E. 「拖材料到背包图标」：判定 → 入队 → 帧末结算**（`Backpack.cs:27`、`:284`、`:360`，`TargetPatch.cs:5`）

```csharp
internal static bool SubmitMode {              // 拾荒现场恒真；店内必须按右 Shift（303）
    get {
        if (!Core.SkillsActive) return false;
        if (!ScavengingEntry.Present && !Input.GetKey((KeyCode)304))   // 现场外且没按左 Shift
            return Input.GetKey((KeyCode)303);                         // → 看右 Shift
        return true;
    }
}

internal static bool Match(GameItem source, GameItem target) {   // source=材料，target=背包
    if (UtopiaGrowth.IsDevice(target)) return Core.SkillsActive && source != target && BaseYield(source) > 0;
    if (!IsBag(target) || !SubmitMode || source == target) return false;
    return BaseYield(source) > 0;                                // 该材料能折多少单位
}

[HarmonyPatch(typeof(GameItem), "Target")]                       // 拦截原版「物品互用」终点
internal static class TargetPatch {
    private static bool Prefix(GameItem __instance, GameItem targetItem) {
        if (!Backpack.Match(__instance, targetItem)) return true;
        Backpack.Queue(targetItem, __instance);                  // 只入队，帧末 Flush 才扣
        return false;
    }
}

// ── 帧末结算：Backpack.FlushMaterials()（Backpack.cs:360）──
private static void FlushMaterials() {
    var array = pending.ToArray();  pending.Clear();
    foreach (var kv in array) {
        var key = kv.Key; var value = kv.Value;                  // key=材料, value=背包
        if (!CanSubmit(value, key)) continue;
        if (UtopiaGrowth.IsDevice(value)) { UtopiaGrowth.Feed(key, value, commit: true); continue; }
        Refresh(value);
        if (BackpackRetrofitApi.IsPart(key)) { BackpackRetrofitApi.Apply(key, value); continue; }   // 改造件
        var limit = BackpackRetrofitApi.Limit(value);
        long yield = Yield(key), progress = Progress(value);
        int need = BackpackRules.NeededUnits(progress, limit.width * limit.height - Slots(value),
                                             yield, Math.Min(key.unitCount, key.MaxNumRemove()));
        if (need == 0) continue;
        int before = Slots(value);
        if (need == key.unitCount) key.Destroy(); else key.SetUnitCount(key.unitCount - need);
        SetProgress(value, progress + yield * need);
        Refresh(value);                                          // 形状在这里真正长格
        BackpackUpgradeFx.Play(value, Slots(value) > before);
    }
    // 末尾把本轮各家材料/解锁格数合并成一条 StoreUIManager.Notify（:424-428），并整段 try/catch 报 Core.Error
}
```

**F. 扩地：五个条件全绿才动手 + 归还三重让位**（`ScavGround.cs:58`、`:103`、`:138`）

```csharp
private static void TryAcquire() {
    if (owned != null || leaseCeded || !ScavengingScene.IsAvailable || !Settings.ExpandedGround) return;
    var entry = EmporiumEntry.Instance; var grid = entry?.afterhourInventory;
    if (grid == null) return;
    if (entry.afterhourWindow?.handler == null || !entry.afterhourWindow.IsVisible()
        || grid.childItems.Count != 0                      // ④ 地面必须空
        || !PlainShape(grid.inventoryShape, 12, 9))         // ⑤ 形状必须原封未动
        return;
    try {
        owned = grid; window = entry.afterhourWindow; locationOwner = MapUIManager.Instance.Current;
        originalScale = appliedScale = window.rectTransform.localScale;          // 记下原值
        originalPosition = appliedPosition = window.rectTransform.localPosition;
        grid.SetShape(12, 15);                             // 只改这一处
        expanded = grid.inventoryShape;                    // 记下「我设的那张」
        entry.Validate(true);
    } catch (Exception ex) { Core.Error("Expand scavenging ground", ex); Restore(); }
}

private static bool PlainShape(GridShape map, int width, int height) {
    if (map == null || map.width != width || map.height != height
        || map.minX != 0 || map.minY != 0 || map.orientation != 0 || map.flipped
        || map.GetLocal(-1, -1) != 1) return false;        // 越界哨兵 = 1
    for (int i = 0; i < height; i++) for (int j = 0; j < width; j++)
        if (map.GetLocal(j, i) != 0) return false;         // 12×9 每格都必须可用
    return true;
}

private static void Restore(bool boundary) {
    if (owned == null) return;
    bool flag = false;
    try {
        if (!OwnsShape) { leaseCeded = true; return; }      // ① 形状被别人动过 → 永久放弃
        RestoreTransform();
        foreach (GameItem child in owned.childItems) {
            var s = child.modifiedShape;
            if (s != null && ScavengingControlsRules.FitsOriginal(s.minX, s.minY, s.maxX, s.maxY))
                continue;                                   // 仍在原版 12×9 内，安全
            if (!boundary && locationOwner == MapUIManager.Instance.Current) {
                flag = (suspended = true);                  // ② 同地点内先挂到场景边界再试
                if (!boundarySubscribed) {
                    boundarySubscribed = true;
                    ModHook.OnAfterhourLeaveLate += BoundaryRestore;
                    ModHook.OnAfterhourArriveEarly += BoundaryRestore;
                }   return;                                 // 越界物品在手上：这次什么都不做
            }
            Core.Log?.Log("Ground layout restoration skipped at scene boundary: unexpected retained " +
                          "items; no items moved or deleted and no later retries.");  // ③ 明说不重试
            return;
        }
        owned.SetShape(12, 9);  window?.Validate();
    } catch (Exception ex) { Core.Error("Restore scavenging ground", ex); }
    finally { if (!flag) { owned = null; expanded = window = null; locationOwner = null; } }
}
```

**G. 读档前抢读背包形状**（`RestorePatch.cs:36`）

```csharp
[HarmonyPatch(typeof(SaveManager), "DecodeNodes")]
internal static class RestorePatch {
    [ThreadStatic] private static DecodeScope scope;
    internal static (string map, int width) TakeLayout()        // Backpack.Create() 里调用
        => (scope == null || scope.layouts.Count <= 0) ? InitialLayout() : scope.layouts.Dequeue();
    private static void Prefix(List<SaveItemNode> savedItems, out DecodeScope __state) {
        __state = scope; scope = null;
        if (savedItems == null) return;
        foreach (var savedItem in savedItems) {
            if (savedItem?.identifier != "eotn_scavenger_backpack") continue;
            scope ??= new DecodeScope();  scope.bags.Add(savedItem);
            string text = null; int num = 0;
            var dict = savedItem.itemState?.dict;               // 尚未解码，直接读原始 tag 字典
            if (dict != null) {
                if (dict.TryGetValue("SCAV_BAG_MAP",   out var v)) text = v.GetString();
                if (dict.TryGetValue("SCAV_BAG_WIDTH", out var w)) num  = w.GetInt();
            }
            // 校验（:70-74）：宽 3~40、len%width==0、高 3~40、字符只许 '0'/'1'；否则退回 Shape(9,5,5)+宽 5
            if (num < 3 || num > 40 || string.IsNullOrEmpty(text) || text.Length % num != 0
                || text.Length / num < 3 || text.Length / num > 40 || text.Any(c => c != '0' && c != '1'))
            { text = BackpackRules.Shape(9, 5, 5); num = 5; }
            scope.layouts.Enqueue((text, num));
        }
    }
    private static void Postfix() {   // 解码完成后 tempLink 才有值，此时补形状
        foreach (var bag in scope.bags) if (Backpack.IsBag(bag.tempLink)) Backpack.Refresh(bag.tempLink);
    }
}
```

## 4. 踩坑与红线

1. **没有热重载钩子**（`HotReloadPrepare/Activate` 全缺）。`SkillPanel.window`、`BackpackTools.pockets`、`ScrapFurnace.root`、`ScavengingHud.current` 等 30+ 个 static 引用只靠 `OnDisable` 收。按我们 `AGENTS.md` 这条**不能照抄**：热重载必须成对重置，否则旧 DLL 的 `GameObject` 会在 Unity 假 null 下变成活僵尸——它的 `Alive(...)` 系列判空（`BackpackTools.cs:63-86`）就是被迫补的。
2. **`PlayerStore.Instance` 到处直接解引用**（`Skills.cs:88`、`SkillPanel.cs:288`、`ScrapFurnace.cs:11`），靠 `Core.SkillsActive`（`Core.cs:28-38`，内部才判 `HasInstance`）当总闸。这个「总闸 + 假 null」组合在菜单/读档时序里是脆的，照抄必须逐点补判。
3. **内部逻辑遍地 try/catch 吞异常**（`FlushMaterials`/`Restore`/`Fit`/`Tick`/`Refresh` 全 `catch { Core.Error }`）——**违反我们「对内暴露、边界捕获」硬规范**。这是为「一个坏物品不能卡死整晚」，代价是坏档后没人知道哪步没生效。抄它的守卫逻辑时**只抄判定，不抄吞异常**。
4. **点数账本拆成多份计数器，然后一路打补丁**：`Spent`/`tree.bagPaid`/`tree.backpackSpent`/`tree.splitSpend` 分摊用 `Math.Round`（`SkillProgression.cs:90-136`），之后又加 `tree.progressionV3`、`tree.disguiseMerged`、`story.skillsVersion`、`discovery.teacherVersion`、`furnace.pointsVersion`、`tree.craftRemoved` **六个版本号**做迁移（`:444-547`）。教训：**账本一次设计到位**，洗点费用要从 `(id, rank)` 幂等反算（它最后正是这么修的）。
5. **拖拽拦截＝最高冲突面**：`EndDrag`/`EndGroupDrag` 的 `return false` 会**整个吃掉**原版落点，两个模组同时改必互相打断（先加载的赢）；它自己也要反射 `AbortDrag` 收尾（`FurnaceSingleDropPatch.cs:10`），说明这类 Prefix **只能"替代"不能"追加"**。只想在某个 UI 上接物品，优先走 `GameItem.Target` 那条自带委托位的路。
6. **`PlainShape` 四连检查不能省**：只看 `width/height` 会把「旋转 90° 的 12×9」当原版；`orientation/flipped/minX/minY` + 越界哨兵 `GetLocal(-1,-1)==1` 是判断「这张图还是不是原版那张」的唯一可靠办法。同理 `Fit` 里判 `appliedScale/appliedPosition` 是否仍等于自己上次写的值，不等于就 `transformCeded=true` 永久放手（`ScavGround.cs:213-235`）——**改别人东西的正确姿势：记下自己写的值，发现被覆盖就让位**。
7. **`MaxRank` 三套来源混用**（硬编码分支 / 配置 / `0=无上限`），`AddSkill` 还故意不给某些技能生成 `.max`（`Settings.cs:192`）；`CostStep` 对 `outings` 有 `×2` 特例（`SkillDefinition.cs:371`）。改平衡前必须确认走哪条分支，否则改了配置不生效。**引用透视报告的数字也要复核**：`_reports` 里「共 57 处」是截断值，实际 65 处；`ScavengingFrameBudget.MayStart` 的 `elapsed < 3.0` 是**死分支**（`completed` 只能是 0），实际就是 1 次/帧。

## 5. 对我们的可用点

| # | 可用点 | 落到哪个功能 | 怎么用 |
| --- | --- | --- | --- |
| 1 | **`SkillTreeViewport` 那套缩放/平移/夹紧**（205 行、零依赖，只认 `RectTransform`） | 《深空电玩》排行榜 / 周选报纸长图；《深空当铺》关系网图 | 整文件照搬（换命名空间即可）：`OnScroll` 指针锚 + `SetZoom` 三行顺序 + `Clamp` 40px 留边 + `eligibleForClick=false`，直接治我们自绘窗口「滚轮缩放乱跳、拖到一半图飞走」 |
| 2 | **16 帧独立时长表 + 只缓存一只的悬停动画** | 《深空电玩》卡带/刻录机待机动画、街机框体呼吸灯 | `frameDurations[16]` + `HoverFrame(id, elapsed)` 直接搬；节奏抄它的「慢-快-慢」（210→85→145）比等分好看；切目标即 `Destroy` 旧图集的策略，正好治我们立绘那类 ~100MB 显存问题 |
| 3 | **`RenderHandler.LoadFromAtlas` Prefix 代替 `SpriteDict.GetSprite`** | 《深空电玩》8 张 `dnpc_*` 立绘自愈 | 我们现在在 `SpriteDict.GetSprite` 上做自愈（已知热点）。改成「atlas 名 + sprite 名精确匹配时短路、其余 `return true`」的 `LoadFromAtlas` Prefix，冲突面小一个数量级；`[HarmonyPriority(800)]` + 只认自家 atlas 名是它规避 `better_scavenging` 的全部技巧（`BackpackSpriteShapePatch.cs:13-31`） |
| 4 | **`Shape/Order/PreserveLayout` 三件套 + `'0'/'1'` 形状 tag + `DecodeNodes` 预读** | 《深空电玩》收纳盒 6×6 → 可扩容；任何「物品自带容器」 | 收纳盒现在固定 6×6。扩容照它：形状存物品 tag、`Order()` 决定「从中心长出去」的点亮顺序、`PreserveLayout` 保证加宽后老格坐标不变（`BackpackRules.cs:55-108`）；配 `RestorePatch` 的预读，读档才不丢格 |
| 5 | **`BackpackGrantState` 到货状态机 + `F8Bridge` 跨模组协议** | 《深空当铺》`DspVisitorPhones` 电话约货次日上门；`DspInterop api.v1` 面板互斥 | `entitled/received/order/orderDay` 四键 + `TryOrder` 先扣钱后置位 + `Refund` 对称退款 + `Due(day)` 判次日（`BackpackGrantState.cs:22-75`）——正好补「7 天 CD + 次日送达」缺的那块；协议抄 `eotn_retrofit_rights:f8.hello/ready/v1.openSkills/closeSkills` 握手 + `DelegatesHotkey` 热键让位（`F8Bridge.cs:25-52`），比自己搓「谁先加载谁赢」稳 |

**一句话结论**：这包值得读的不是「39 项技能」（那是 42 行数据），而是它用三个低成本机制解决的三件难事——在 `RectTransform` 层面自己实现 viewport、把可变容器形状塞进物品 tag 并在解码前抢读、改原版结构前先做「原封未动」的双向校验并准备好永久让位。第 4、5 条恰好是我们两个项目的下一步。
