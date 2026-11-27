# 深空當鋪：店長可能是智能體 / White Dragon Ledger｜工坊 3845754309

> 蒸馏自反编译源码 `缓存文件夹\decomp_ws\3845754309`（15 个 .cs，其中 `InventoryObserver.cs` 单文件 **14484 行**；`LedgerBuild.cs` 内嵌 build commit `2853544`）。
> Python MCP 侧源码不在反编译缓存里，取自作者 MIT 仓库 `github.com/Mirasupiritto/WhiteDragonLedger` 的 `mcp/*.py`（落盘在 `缓存文件夹\wdl-src\`，临时，可清）。
> 价值：**工坊里最完整的"游戏状态 → 结构化账本 → 外部 AI 决策 → 回落到原版公开 API"闭环样本**。C# 侧零 Harmony、零截图、零网络能力，只做两件事：把店内状态写成 JSON 增量日志，把玩家操作暴露成 `{tool, action, args}` 命令。

版本事实（工坊描述 + 代码）：适用游戏 0.46D，模组 v0.31.0，MIT。代码实测：**零 Harmony 补丁**、零 `DllImport`、零 `Process`、零 `System.Net`、零直接 `File.*`/`Directory.*`（全部经 `ModHelper.*ModDataFile*`）。

---

## 0. 先纠正一个前提：SQLite 不在 DLL 里

工坊描述说"透過本機 SQLite 帳本把店內資訊整理給 AI 讀"。这句只对了一半——**代码证据说明这是两层分工，SQLite 在 Python 宿主侧，不在模组 DLL 里**：

| 层 | 技术 | 证据 |
| --- | --- | --- |
| 游戏侧（本模组 DLL） | **没有 SQLite**。全部经 `ModHelper` 写 JSON 到游戏 `ModData/white_dragon_ledger/` | `WhiteDragonLedger.csproj:14-24` 只引用 UnityEngine.CoreModule / Assembly-CSharp / Newtonsoft.Json / System.Core / Unity.TextMeshPro / UnityEngine.UI / Unity.Localization / UIModule / InputLegacyModule；15 个 .cs 里 `sqlite` 零命中、无 `DllImport`；唯一落盘口 `Write()` → `ModHelper.WriteModDataFileText`（`InventoryObserver.cs:6809-6812`） |
| 宿主侧（Python MCP） | SQLite `%LOCALAPPDATA%/WhiteDragonLedger/account.sqlite3`，把 JSON 增量日志**回放**成关系表 | `mcp/ledger_mcp.py`（默认 `--journal` = `<游戏>/AppData/LocalLow/Questing Goose Studio/Probably Stolen/ModData/white_dragon_ledger`，默认 `--database` 如上）；`mcp/database.py:42-84` 建表 |

为什么选 SQLite 而不是文件/内存（作者 README 原话 + 代码印证）："一次建置基準，多次增量查詢…透過 `cursor` 追蹤增量變動（`shop_update`）…交易票據、歷史異動、垃圾桶與已通知的加熱紀錄皆持久化在 SQLite，大幅降低上下文負擔與 token 消耗，斷線重連也能安全續接。"
代码层面这个收益确实全落在**读侧**：`ledger.py:92-124` 只把 `cursor+1 .. revision` 之间的日志文件读进 SQLite，之后任意按 `name/area/owned/save_id` 过滤分页（`ledger.py:129-146`），不必把几百行库存 JSON 整个塞进上下文。并发与崩溃安全靠 `BEGIN IMMEDIATE` + 8s timeout（`database.py:99-101`），裸文件做不到。结论：SQLite 不是必需项，真正的设计是"**游戏侧只追加增量，读侧自己建索引**"。

---

## 1. 状态账本

### 1.1 目录与文件清单（全部 `ModData/white_dragon_ledger/`）

| 文件 | 角色 | 写入点 |
| --- | --- | --- |
| `scene.json` | **场景收据**：status / location / title / tool_region / session / revision / dll_commit / travel_options | `PublishScene` `:10159-10232`（随场景变化，每次 `revision++`） |
| `head.json` | **游标头**：status、generation、revision、slot、phase、full_scans、load_scans + 10 个 `*_protocol` 版本号 | `:6889`（读档置 `waiting_for_load`）、`:7001-7020` |
| `{generation}-{n}.json` | **追加日志**：`n=1` 是 `op:baseline` 全量；之后 `op:delta` 带 `upsert/remove/purifier_stock/capacities/identity_changed` | `Publish` `:6976-7022` |
| `{generation}-stock-categories.json` | 类目 id → 显示名映射（让 AI 说人话） | `:7058` |
| `desk-{generation}.json` | **店内实况汇总视图**（顾客/柜台/机器/电话/电脑/暗格/夜报/容量），1 Hz | `RefreshDesk` `:11774-11848` |
| `lots-{generation}.json` | 进货批次台账（供应商、单价、成员、卖出价） | `PublishLots` `:11570-11583` |
| `trash-{gen}-{ver}.json` | 垃圾桶清单（吸引力惩罚来源） | `:10936` |
| `sale-progress-{gen}.json` | 卖货流水线中途进度（断线可续） | `:9312` |
| `receipt-{command_id}.json` / `last-receipt-{channel}.json` | **命令回执**（幂等去重靠它） | `StoreCommandReceipt` `:2114-2123` |
| 入站：`counter-/journey-/move-/watch-/report-/sale-{gen}.json` | 外部 AI 下达的命令信箱 | 见 §3 |

另有**存档内**持久层（跨读档不丢）：`ModHelper.SetModData("white_dragon_ledger", "purchase_lots" / "last_night", json)`（`:11548-11549`、`:11645`、`:11741`）。
而文件账本按 `generation = Guid.NewGuid()`（`:7037`）**每次读档重开一代**，清理由 `ResetLoad`（`:6834-6890`）统一执行，末尾立刻写 `head.json` 的 `waiting_for_load`。

### 1.2 写进去的是哪些游戏状态

`DayView()`（`:11362-11417`）一眼看全：`health / security / wanted / last_save / calendar / trash / sleep / night_report / appeal / status_summary / nuclear_waste / capacities`。`RefreshDesk()`（`:11808-11841`）再叠上 `customer / day_info / phone / counter / available_strips / machines / portable_purifiers / smuggler_bays / last_night`。逐类字段：

- **物品行**：`id/name/kind/quantity` + 几何 `size{w,h}`、`slot{x,y}`、`rotatable` + 容器 `parent_id/container_path/access` + 液体水 `water_ml/liquid_ml/capacity_ml/empty/contains_water/liquid_main` + 电池 `charge_remaining/charge_max` + `estimated_value` + `contraband/trade_contraband` + `categories` + `bottle_label`（`AddGeometryFields:5839`、`ItemSize:5343`、`ItemSlot:5359`、`InventoryCapacity:5408`、`AddWaterContainerFields:5730`、`EnrichStockRow:5846`）。
- **顾客与生意**：`visit`、`customer{name, remaining budget}`、`client_role`、对话行 `lines`、`stage`（`ready / not_selling / tablet_closed / speaking / choice_required / confirmation_required`）、报价 `offer{...}`、`dialog_choices`（`CounterView:2392-2511`；气泡取值 `DialogChoices:3133`）。
- **需求平板**：`PanelDemand`（`:8054-8139`）走 `NegociationUIManager.Instance.uiObject → NegotiationTabletFrame.infoHost` 的子节点，按本地化标签 `ui_nego_restrictions` / `ui_nego_interests` 把"限制/兴趣"两类 Tag 行读成结构化数据；遇到 `Probe` 子节点返回 `panel_updating`（正在刷新），不硬读半截。
- **机器与工作站**：`MachineRows:11681`、窗口控件树 `ReadWindow:13816` + `DescribeControl:13651`（给每个 `PixelElement` 打 `role/label/path`，让 AI 说得出"按 26℃ 那格按钮"）、加热读数 `HeatingView:1722`。
- **账单/夜报/盘查**：`NightBill:7894`、`TrashSummary:10880`、`NightReportView:11199`、`SecurityView:10624`（`evidence_level`、`poster.pulled`、`contraband{inventory/showcase/smuggler_bay/hidden_grid/hidden_grid_exposed}`，来源 `ContrabandCounts:10571-10622`）。

### 1.3 多久写一次：不是定时，是"变了才写"

`FlushChanges()`（`:7136-7189`）是第一道闸：

- 提前返回（`:7147-7151`）：`changed.Count == 0 && phase == 当前 storeState && Time.realtimeSinceStartup < nextView` → 一次 IO 都不做。
- 真比较：purifier 库存与容量各自 `JToken.DeepEquals` 与上次发布值比（`:7158-7160`），身份变化队列非空才算脏（`:7161`）。
- 有变化才 `Publish`（`:7187`），一次最多写 2 个文件（`{gen}-{n}.json` + `head.json`）。
- `desk-{gen}.json` 是 1 Hz 定时（`:11807`），但**序列化成字符串后跟上一次比**，相同就不写（`:11842-11847`）——一份视图整天不动就整天零写。
- 各轮询各自节流：柜台/旅程/移动 150ms，机台/加热 100ms，订单 200ms，接待 250ms（`:3090, :5021, :6609, :7370, :7409, :8952, :10095`）。

---

## 2. 动作抽象层：可调用动作清单 + 落到哪个原版 API

### 2.1 结构：一个发射口 + 五个信箱 + 若干子分派

`ActAtCounter`（`:2721-3082`）是店内主分派；`ActOnJourney`（`:6613-6807`）管跨场景与出门；`ActInShop`（`:10800-10859`）管门/装备/开枪/关日历这类店门口动作。子分派：`ItemPanelAction:6528`、`ItemMenuAction:6077`、`WorkstationAction:14013`、`WaterUseAction:12983`、`WaterPanelAction:12754`、`BargainAction:1185`、`ActOnBarter:1519`、`ActOnBill:7980`、`ActOnPhone:8440`、`HeatAction:5067`、`NetworkAction:13468`、`PayInformationReceipt:5254`、`TogglePosterAction:10678`、`ActOnReception:8966`、`ActOutside:4286`、`ReportAction:9104`、`SubmitDecision:3321`、`SubmitSleep:11419`、`SortInventory:5301`、`RequestSupplierQuote:2020`。

### 2.2 动作清单（从 `case` 标签实测抓出）

| 域 | 动作 |
| --- | --- |
| 开店/接待 | `open_shop`、`close_shop`、`ring`、`dismiss`、`auto_receive`、`door`、`confirm_packing` |
| 对话/议价 | `advance`、`page`、`tablet`、`buy`、`choose_dialogue`、`response_choice`、`bargain`、`barter`、`request_quote`、`pay_info` |
| 物品/容器 | `move`（source/target，支持 `bay:<id>`）、`item_panel`、`item_menu`(`open/use/equip/unequip/activate/configure/toggle/unload/pin`)、`transfer_slot`、`transfer_slots`、`inspect_window`、`inspect_archive`、`close_archive`、`visible_windows`、`sort_inventory` |
| 机器/化工 | `adjust_control`、`press_control`、`heat_control`、`water_use`(`preview/apply/apply_now`)、`water_panel`、`watch`（等机器数值跨阈值） |
| 黑科技 | `toggle_poster`（拉海报开/合隐藏格）、暗格直搬（`bay:<id>` 当 source/destination 用） |
| 夜生活 | `sleep`、`prepare_sleep`、`night_service`（核账）、`phone_keypad`/`dial`/`phone_choice`/`phone_advance`、`scavenge`、`return`（可带 `abandon_ground`）、`open_bag`/`close_bag` |
| 剧情/开局 | `specializations.choose`、`starting_perks`、`starting_difficulty.select`、`scratch_card`(`opened/revealed/closed`) |
| 执法/电脑 | `report`、`wild_network`(`tab/select/close`) |
| 卖货流水 | `sale`（`OrderStep` 分步推进，`:10073-10138`） |

### 2.3 八个"动作 → 原版方法名"实例（全部有行号）

1. **开门（推卷帘）**：`StoreShutterButton.Instance.OnClick()` → 回读 `PlayerStore.Instance.storeState == 1` 报 `opened`，否则 `open_unconfirmed`（`:2791-2811`）。
2. **叫铃接待**：`Object.FindObjectOfType<StoreBell>().RingBell()`，随后 `ObserveVisit()` 抓 `visitId`，拿不到就 `ring_unconfirmed`（`:2853-2870`）。
3. **顾客进门**：`Object.FindObjectOfType<StoreDoor>().OnClick()`（`:10809`）。
4. **买断/批发**：先把台面货全搬到后柜 `ModHelper.MoveItemToBackCounter(item)` 并逐件校验落点，再 `NegociationUIManager.Instance.OnBuyAllClick()`（零售）或 `OnWholesaleClick()`（批发），最后比对 `cash_after/paid` 与报价（`:2989-3076`）。
5. **对话推进**：`DialogUIManager.Instance.OnAdvancePressed()`，前置比对 `CounterView()["page"]` 防重复翻页（`:2893-2906`）。
6. **试报价（询价后归位）**：`ModHelper.MoveItemToBackCounter` → 读 `value.GetNegociatedValue()` → **`finally { ModHelper.MoveItemToFrontCounter(value) }`**，并核对 `parentInventory == frontInvinvElement`（`:2045-2072`）。"借用原版 UI 语义取数、用完必然归位"的教科书写法。
7. **拾荒/返回**：`MapUIManager.Instance.ScavengeButtonClick()`（`:4332`）、`Return()` / `ReturnNow()`（`:4311-4316`）；拾荒后对比 `InjuryView()` 的 `wound_state` 前后值，判 `completed / minor_wound / major_wound`（`:4343-4357`）。
8. **移动物品**：`GraphUtils.CanAccept(target, item)` 预判 → `TransferAndMeasure`（内部 `GraphUtils.TryAcceptOnce`，`:9460`）→ 事后用 `item.parentInventory == 目标` 引用比较确认；装不下给 `no_space` + `AttachSpaceFailure`（`:4444-4465`）；兜底归位链 `MoveItemToInventory / MoveItemToDisplayCase / TryAcceptOnce`（`ReturnToOriginWithFallback:9446-9469`）。

补充三处反射兜底（原版没给公开入口的地方）：难度页 `NewGameDifficultyUI.TurnPage/OnPlateClicked`（`StartingDifficulty.cs:11-17, 89-91`）、开局专精 `MainMenuUIController.Instance.OnScenarioContinue(scenario)`（`Specializations.cs:64`）、讨价还价面板 `BargainUIManager` 的私有方法/字段（`InventoryObserver.cs:998-1006`）。


`Receipt()`（`:7441-7481`）固定回：`command_id / status / generation / slot / revision / phase / item_id / from / requested_area / expected_revision`，业务再补 `next_tool`（如 `:2941-2942` 的 `next_tool: item_panel` + `after_close_next_tool: reopen_trade_tablet`）。
状态词表本身就是它的文档：`expired`、`stale_observation`、`scene_changed`、`visit_mismatch`、`shop_closed`、`tablet_closed`、`no_space`、`over_limit`、`insufficient_cash`、`*_unconfirmed`、`outcome_unknown`。**没有"抛异常给 AI"这条路**——异常只在边界 `Debug.LogException` 后落成 `outcome_unknown`（`:56-63`、`:7427-7434`）。

---

## 3. 外部 AI 的接口：文件信箱 + 硬链接原子投递（不是 HTTP，也不是命名管道）

### 3.1 谁在轮询

游戏内：`InventoryObserver : MonoBehaviour` 挂在 `DontDestroyOnLoad` 的 `"White Dragon Ledger Observer"` 上（`LedgerMod.cs:19-21`），`LateUpdate()`（`:7081-7134`）里逐个 `Check*`：

```csharp
// InventoryObserver.cs:7403-7414 —— move 信箱的轮询（其余信箱同构）
private void CheckMove()
{
    if (Time.realtimeSinceStartup < nextCheck)
    {
        return;
    }
    nextCheck = Time.realtimeSinceStartup + 0.15f;
    string text = "move-" + generation + ".json";
    if (!ModHelper.ModDataFileExists("white_dragon_ledger", text))
    {
        return;
    }
```

宿主内：Python MCP（stdio）——`exchange()` 写命令进信箱，然后 75ms 一次读回执、最多等 8 秒（`command_transport.py:114`）。**双端都靠轮询文件**，没有 socket、没有管道、没有开端口（也就不存在抢端口、鉴权、与别的模组冲突）。

### 3.2 投递的原子性：临时文件 + `os.link`

```python
# mcp/command_transport.py:79-118（节选，保持原样）
def exchange(folder, inbox, request, read_receipt, previous_command=None, *, wait_seconds=8):
    command = request["command_id"]
    joined = False
    fd, name = tempfile.mkstemp(dir=folder, prefix="posting-", suffix=".tmp")
    try:
        with os.fdopen(fd, "w", encoding="utf-8") as stream:
            json.dump({**request, "expires_ms": int(time.time()*1000)+6000}, stream)
        # Unity publishes the receipt just before removing the inbox. A batch may
        # reach this point while the preceding confirmed command still owns it.
        handoff_deadline = time.monotonic() + 0.5 if previous_command else None
        while True:
            try:
                os.link(name, inbox)
                break
            except FileExistsError:
                ...
                return {"status": "busy", "command_id": command}
    finally:
        Path(name).unlink(missing_ok=True)
    result, timed_out = poll(read_receipt, timeout=wait_seconds, interval=0.075)
    if not timed_out:
        return replayed(result) if joined else result
    return {"status": "unconfirmed", "command_id": command,
            "detail": "Check the receipt with the original arguments before another action."}
```

`os.link` 原子落位 → Unity 永远读不到"写了一半的 JSON"；`expires_ms = now + 6000` 让游戏侧能拒收过期命令；上一单还没被消费时返回 `busy` 而不是硬覆盖。

### 3.3 幂等与去重（三层）

1. **确定性 command_id**：`uuid5(NAMESPACE_URL, namespace + json.dumps(plan, sort_keys=True))`（`command_transport.py:19-22`）——同一意图重复提交得到同一个 id。
2. **宿主侧先查回执**：`check_existing` 命中直接返回并标 `replayed: true`（`command_transport.py:126-129`、`:33-35`）。
3. **游戏侧去重**：`AlreadyHandled(channel, command, generation)`（`:2167-2182`）——有 `receipt-{id}.json` 就删信箱并跳过执行；`last-receipt-{channel}.json` 可反哺补齐丢失的回执（`:2125-2165`）。

### 3.4 出错 / 超时怎么降级到人工

四道门，逐级退让，**绝不自动重试**：

1. **命令过期**：每个 `Act*` 第一件事是 `expires_ms < now` → `"expired"`（`:2732`、`:6615`、`:7510`）。
2. **快照过期**：比对 `generation` + `revision` → `"stale_observation"` / `"scene_changed"`（`:2739-2742`、`:6625-6628`），AI 必须重读账本再决策。
3. **代理失效**：`visitId` 对不上 → `VisitMismatchStatus`（`:8740-8756`，返回 `customer_left` / `customer_replaced`）。
4. **场景不对 / 无人应答**：`tool_region()` 把场景映射成 `pawnshop / new_game / new_game_perks / new_game_difficulty / destination_selection / afterhours / offline / scene_only`（`scene.py:73-88`），`SceneServer.available()` 只放行当前区域工具，越界返回 `{"tool_status":"unavailable_in_scene"}`（`scene_server.py:197-202, :258-260`），`game_over` 直接短路（`:250-253`）。

关键词：**模组自己从不主动行动**。没有 AI 时它只写账本 + 做有限家务（`FileCounterPapers` 把柜台上遗留的单据塞回库存，`:3506-3558`，带 `Interactable.isPaused` / `NegociationUIManager.IsTradeLocked()` / `lockAllInventoryFlag != 0` / `IsItemLocked` / `IsDisplayOnly` 五重门），玩家照常手动玩；AI 只负责"决定按哪个原版按钮"。
真正需要人眼的事（只看名字的通缉照、改造体外貌、纯图片图鉴）由 README 约定成 `player_visual_check` / `wanted_name_match` 这类"请人看一眼"的回传，账本侧只提供 `SecurityView` / `ContrabandCounts` 的数值字段（`:10571-10646`）供 AI 判断"该不该叫人"。

---

## 4. 为什么能连跑十几天不卡

**一个入口 + 三条约束**：

1. **只有一个 `LateUpdate`**（`:7081-7134`），全部子任务自己算下次到期时间（`nextView / nextCheck / nextCounterPoll / nextMachinePoll / nextJourneyPoll / nextOrderPoll / nextReceptionCheck / nextHeatCheck`）。没有协程风暴，没有 `Update` 里的大对象分配。
2. **事件驱动优先于轮询**：`Track()`（`:6930-6944`）给每个被监视容器挂原版三组委托 `onSlotAddItemLateFunc / onSlotRemoveItemLateFunc / onSlotChangeAmountItemFunc`，只有真变动的物品进 `changed` 这个 `HashSet<GameItem>`（`Changed:6921-6928`）；卸装时 `DetachAreas()` 逐容器 `Delegate.Remove`（`:6907-6919`），读档与销毁各成对清理一次。
3. **写侧与读侧都做"没变化就不发"**：
   - 写侧：§1.3 的两道闸。
   - 读侧：`_slim_shop_update`（`scene_server.py:98-175`）把 delta 里"与上次完全相同的行"和"回执已经给过的行"剔掉；全空就只回 `{"status":"unchanged","revision":…,"cursor":…}`。

**与"截图 + 模拟点击"的本质区别**（对着代码说，不是对着宣传）：

| 维度 | 本模组 | 截图 + 模拟点击 |
| --- | --- | --- |
| 喂给 AI 的形态 | 定长字段、`Formatting 0` 无缩进 JSON、稳定排序 → 前缀几乎不变，prompt cache 命中率高 | 每帧位图，像素噪声让缓存失效，token 与上下文暴涨 |
| 回给游戏的形态 | 直接调原版公开方法（`OnClick / RingBell / OnAdvancePressed / CanAccept + TryAcceptOnce`） | `SendInput` / `mouse_event` 伪造事件，坐标一改就错位 |
| 失败可诊断性 | 每步有 `status` 枚举 + `revision`，能说清卡在哪一关 | 点歪了只知道"没反应" |
| 耦合面 | 与原版 UI 的**逻辑层**（方法名/字段名）耦合，不依赖分辨率与布局 | 与**像素层**耦合，分辨率/缩放/主题都影响 |
| 可自动化断言 | 账本是纯文本，可直接断言 | 只能截图比对 |

遗留代价：`InventoryObserver.cs` 14484 行单体 MonoBehaviour（约 490 个成员），是我们规范明确反对的形态。它换来的是"所有状态都在一个类里，不用跨文件对齐生命周期"——可以理解，但不建议照抄（见 §6 末）。

---

## 5. 风险

| 风险 | 事实 / 证据 | 评价 |
| --- | --- | --- |
| **能力声明** | 反编译缓存里**没有 manifest.xml**（只有 csproj），`<Capabilities>` 具体内容**（推测）**。代码侧唯一对外口是 `ModHelper.*ModDataFile*`（11 处）+ 公开原版 API | 与"零能力、或仅 `files`"相符；`files` 是否为写 ModData 所必需见 `03-原版接口速查` 注记（**（推测）** 需 `files`） |
| **真正的权限在宿主** | Python MCP 是独立进程：读游戏 `ModData` 目录、写 `%LOCALAPPDATA%` 下的 SQLite，还会 `subprocess.run(["git", …])` 查 commit（`scene.py:94-97`） | 游戏侧零能力 ≠ 整体零权限，玩家装的是"模组 + 一个本地 Python 服务" |
| **AI 决策错会不会坏档** | 好消息：从不 patch `SaveGame`/ES3，只写 `ModData` 私有键与私有目录文件；物品搬运一律先 `CanAccept` 预判、后 `parentInventory ==` 引用比对、失败走 `ReturnToOriginWithFallback` 归位（`:4444-4465, :9446-9469`） | 结构性坏档风险低 |
| **不可撤销的自主行为** | `FileCounterPapers` 自动把柜台单据搬进库存（`:3506-3558`）；`buy` 前 `ClearNonOrderBackTable` 会清后柜（`:2993, :3603-3626`）；`OnBuyAllClick()` 按下后无法回滚 | 都是"便利家务"，但对"柜台摆位有讲究"的玩家是隐性干预 |
| **半成功状态** | 回执存在 `outcome_unknown`（`:54`、`:7426`）与 `*_unconfirmed` 一族；宿主超时返回 `unconfirmed` 并要求"先查清再动" | 需要 AI 读账本二次确认，对 LLM 的自制力是硬要求 |
| **反射耦合** | 7 处私有成员反射：`EndOfDayUIManager.rows`、`WantedUIManager.wantedElements`、`WildNetworkUI.frame/currentTab/selectedId`（`:517, :699, :800-804`）、`BargainUIManager` 的 `GetField/GetMethod`（`:998-1006`）、`NewGameDifficultyUI` 三件套（`StartingDifficulty.cs:11-17`） | 游戏一改私有字段名就静默失效；但它把反射集中到类顶部 + `Supported` 门控 + 返回 `unsupported_layout`（`StartingDifficulty.cs:19-29`），不会崩 |
| **版本漂移** | DLL 内嵌 `dll_commit`（`LedgerBuild.cs:5`，写进 `scene.json:10187`），MCP 启动用 `git log` 比对，不一致就附 `version_mismatch` 警告但不阻挡（`scene.py:104-114`） | 值得抄的"编译期指纹" |
| **适合发布吗** | 这类"AI 代打 + 外部进程"能力对玩家是不透明的（玩家可能不知道自己被改了开局选择，`StartingPerks/StartingDifficulty` 就能改） | 我方若做同类功能，**只放 companion 模组**，不塞进正式发布的主模组 |

---

## 6. 可复用范式（附原样代码）

### 范式 A：游戏状态 → 增量账本（baseline + delta + 游标头）

写侧只做"追加 + 覆盖头"，读侧自己回放：

```csharp
// InventoryObserver.cs:6997-7022
		int num = sequence + 1;
		record["phase"] = JToken.op_Implicit(phase);
		record["day"] = JToken.op_Implicit(StoreStation.Instance.dayCounter);
		Write(generation + "-" + num + ".json", record);
		Write("head.json", new JObject
		{
			["status"] = JToken.op_Implicit("observed"),
			["bag_stock_protocol"] = JToken.op_Implicit(1),
			["item_details_protocol"] = JToken.op_Implicit(1),
			["trade_panel_protocol"] = JToken.op_Implicit(1),
			["stock_categories_protocol"] = JToken.op_Implicit(1),
			["move_location_protocol"] = JToken.op_Implicit(1),
			["counter_move_protocol"] = JToken.op_implicit(1),
			["move_protocol"] = JToken.op_Implicit(1),
			["desk_protocol"] = JToken.op_Implicit(1),
			["purifier_take_protocol"] = JToken.op_Implicit(1),
			["purifier_put_protocol"] = JToken.op_Implicit(1),
			["generation"] = JToken.op_Implicit(generation),
			["revision"] = JToken.op_Implicit(num),
			["slot"] = JToken.op_Implicit(slot),
			["phase"] = JToken.op_Implicit(phase),
			["full_scans"] = JToken.op_Implicit(fullScans),
			["load_scans"] = JToken.op_Implicit(loadScans)
		});
		sequence = num;
```

（`counter_move_protocol` 那行的 `op_implicit` 小写是反编译产物原样，引用时别"顺手修正"。）
读侧回放（游标 + 严格校验 + 协议版本触发重放）：

```python
# mcp/ledger.py:69-92
        previous = db.execute("SELECT generation, revision FROM checkpoint WHERE id=1").fetchone()
        cursor = previous["revision"] if previous and previous["generation"] == generation else 0
        if cursor > revision:
            raise ValueError("Game journal revision moved backwards in the same load")
        # An older MCP process can import this load while omitting its new columns.
        # Rebuild the local cache from published records; the game keeps its scan count.
        if cursor and head.get("stock_categories_protocol") and db.execute(
            "SELECT 1 FROM stock WHERE categories IS NULL LIMIT 1"
        ).fetchone():
            cursor = 0
        if cursor and head.get("item_details_protocol") and db.execute(
            "SELECT 1 FROM stock WHERE size_w IS NULL LIMIT 1"
        ).fetchone():
            cursor = 0
        if head.get("trade_panel_protocol"):
            # Older readers advance checkpoint 1 without importing purifier snapshots.
            extended = db.execute("SELECT revision FROM checkpoint WHERE id=2 AND generation=?", (generation,)).fetchone()
            cursor = min(cursor, extended["revision"]) if extended else 0
        if head.get("bag_stock_protocol"):
            bag_cursor = db.execute("SELECT revision FROM checkpoint WHERE id=3 AND generation=?", (generation,)).fetchone()
            cursor = min(cursor, bag_cursor["revision"]) if bag_cursor else 0
        recorded = db.execute("SELECT MAX(rev) FROM book_events WHERE gen=?", (generation,)).fetchone()[0]
        cursor = min(cursor, recorded or 0)
```

### 范式 B：动作注册表（一个 switch + 统一回执 + 状态词表）

```csharp
// InventoryObserver.cs:2755-2782
		switch (text)
		{
		case "item_panel":
			return ItemPanelAction(request, receipt);
		case "item_menu":
			return ItemMenuAction(request, receipt);
		case "sort_inventory":
			return SortInventory(receipt);
		case "toggle_poster":
			return TogglePosterAction(request, receipt);
		case "inspect_window":
		case "transfer_slot":
		case "transfer_slots":
		case "adjust_control":
		case "press_control":
		case "inspect_archive":
		case "close_archive":
			return WorkstationAction(request, receipt);
		case "visible_windows":
			return ObserveTradeWindows(receipt);
		case "bargain":
			return BargainAction(request, receipt);
		case "barter":
			return ActOnBarter(request, receipt);
		case "water_use":
			return WaterUseAction(request, receipt);
		case "water_panel":
			return WaterPanelAction(request, receipt);
```

配套的"动作 = 原版 UI 入口 + 前置条件 + 状态词"：

```csharp
// InventoryObserver.cs:10804-10824（节选）
		case "door":
			if (!(bool)view["can_request_door"])
			{
				return "door_unavailable";
			}
			Object.FindObjectOfType<StoreDoor>().OnClick();
			AttachResponseChoicesIfOpen(receipt);
			return "door_requested";
		case "confirm_packing":
			if (!(bool)view["packing_confirmation"])
			{
				return "no_confirmation";
			}
			MapUIManager.Instance.OnGoOutsideConfirm();
			AttachResponseChoicesIfOpen(receipt);
			return "packing_requested";
```

关键点：**前置条件从账本视图里读**（`view["can_request_door"]`），而不是自己再判断一遍游戏状态——账本与动作共用同一份事实来源，AI 看到什么就能按什么。

### 范式 C：外部决策回环（信箱 + 幂等 + 过期 + 回执）

```csharp
// InventoryObserver.cs:33-68（节选）
			string text = channel + "-" + generation + ".json";
			if (!ModHelper.ModDataFileExists("white_dragon_ledger", text))
			{
				return false;
			}
			JObject val = ReadInbox(text);
			if (val == null)
			{
				return true;
			}
			string text2 = (string)val["command_id"];
			if (!AlreadyHandled(channel, text2, generation))
			{
				JObject val2 = lastReceipt;
				if (text2 != (string)((val2 != null) ? val2["command_id"] : null))
				{
					lastReceipt = new JObject
					{
						["generation"] = JToken.op_Implicit(generation),
						["slot"] = JToken.op_Implicit(slot),
						["command_id"] = JToken.op_Implicit(text2),
						["status"] = JToken.op_Implicit("outcome_unknown")
					};
					try
					{
						lastReceipt["status"] = JToken.op_Implicit(act(val, lastReceipt));
					}
					catch (Exception ex)
					{
						Debug.LogException(ex);
					}
				}
				StoreCommandReceipt(channel, lastReceipt);
			}
			ModHelper.DeleteModDataFile("white_dragon_ledger", text);
			return true;
```

三个要点：①先把回执**预置成 `outcome_unknown`** 再执行，异常也留得下一条可查的回执；②执行完（无论成败）立即删信箱；③非法 JSON 里拿不到合法 `command_id` 的直接丢弃并 `Debug.LogWarning`（`ReadInbox:2080-2112`）——外部输入写坏了不能把游戏带崩。

### 范式 D：降级到人工 / 给同类模组让位（显式状态码，不静默）

```csharp
// InventorySorting.cs:33-47（完整）
	public static string Blocker(JObject receipt)
	{
		if (mode.Value == "off")
		{
			return "inventory_sort_disabled";
		}
		LoadedMod val = sorters.FirstOrDefault((LoadedMod mod) => mod.IsEnabled);
		if (mode.Value == "auto" && val != null)
		{
			receipt["provider"] = JToken.op_Implicit(val.Manifest.Name);
			receipt["workshop_id"] = JToken.op_Implicit(val.WorkshopId.ToString());
			return "external_sorter_active";
		}
		return null;
	}
```

```python
# mcp/scene.py:73-88（完整）
def tool_region(scene):
    if at_shop(scene):
        return "pawnshop"
    if scene.get("status") == "selecting_difficulty":
        return "new_game_difficulty"
    if scene.get("status") == "selecting_perks":
        return "new_game_perks"
    if scene.get("status") == "selecting_specialization":
        return "new_game"
    if scene.get("status") == "selecting_destination":
        return "destination_selection"
    if scene.get("status") in {"waiting_for_load", "waiting_for_scene_observer", "mod_not_loaded"}:
        return "offline"
    if scene.get("status") == "ready" and scene.get("location"):
        return "afterhours"
    return "scene_only"
```

`InventorySorting.Observe` 里硬编码了三个"整理类模组"的工坊 id（`3829206666 / 3829730652 / 3837294570`），检测到启用就退让，并回 `external_sorter_active` + 提供者名字——**冲突以状态码形式告诉 AI，而不是静默什么都不做**。

---

## 对我们的可用点

**直接能搬的三层结构（优先级最高）**

1. **`head.json`（游标）+ `{gen}-{n}.json`（baseline/delta）+ `desk-{gen}.json`（1 Hz 汇总视图）** 这套三件套，正好补上我们桥接器现在最大的短板：每次工具调用都是"重新扫一遍世界再全量回包"，热点就在库存与容器。改成"首次 baseline，之后只发 upsert/remove + `revision`"，AI 侧拿 `revision` 判断要不要重读，token 与帧耗时都会掉一个量级。落地建议：桥接器工具加 `since_revision` 参数，没变化就回 `{"status":"unchanged"}`（照抄 `scene_server.py:168-169`）。
2. **动作注册表 + 统一回执状态词表**：我们的桥接工具出错时爱直接抛异常或返回 `{success:false}`。改抄它：固定回 `command_id/status/revision/phase/next_tool`，状态用可枚举词（`expired / stale_observation / scene_changed / no_space / *_unconfirmed / outcome_unknown`），并且写操作前**先把回执预置成 `outcome_unknown`**（`InventoryObserver.cs:49-65`）。这样热重载或崩在半路时，下一次调用能查到"上一单到底成没成"。
3. **确定性 command_id + 回执落盘 + `expires_ms`**（`command_transport.py:19-22, 79-118`）。我们虽是进程内桥接（不需要跨进程信箱），但这三件对**自动化测试**至关重要：测试脚本重复投同一"意图"不会造成二次下单，超时能明确判 `unconfirmed` 而不是当失败重试。

**另外三个小但高价值的点**

4. **"借用原版 UI 语义取数，用完必然归位"**：`request_quote` 的 `MoveToBackCounter → GetNegociatedValue → finally { MoveItemToFrontCounter }` + 落点引用比对（`:2045-2072`）。自动化测试要读"这台机器现在报多少价"，最稳的路径就是临时把物品摆到原版会算价的位置、读完立刻放回，而不是自己重算定价公式（必然跟原版版本漂移）。
5. **让原版面板当渲染器**（`NativeItemPanels.cs` + `ItemPanelView.cs` + `PublishItemPanel:6423`）：打开原版面板、把它扫成 `{Key, Controls, Text, Actions}`、动作映射成可调用字典。我们做"可能是群友"的顾客面板时可以省一大截 UI 工作，也天然贴合游戏风格、不跟 UI 主题冲突。
6. **集中声明反射 + `Supported` 门控 + 专用状态码**（`StartingDifficulty.cs:11-29`）：我们已有 `ModReflect.GetStatic<T>` 这种零能力替代，但"所有私有成员访问集中在类顶部声明为 `static FieldInfo`、配一个 `Supported` 属性、不支持就返回 `unsupported_layout` 而不是崩"这套要抄——游戏更新后是"功能降级"而不是"整批补丁失败"。

**不要抄的两点**

7. **单文件 14484 行的巨型 MonoBehaviour**：作者用它规避跨文件状态同步，代价是不可维护。我们按规范拆文件，但必须把"生命周期成对清理"（`Track`/`DetachAreas`、`ResetLoad`）当硬约束，拆开后漏一处就是脏委托。
8. **把 SQLite 塞进 C# 模组**：没必要。游戏侧只写 JSON 增量，索引/查询放在自己进程里（`BEGIN IMMEDIATE` 那种并发保护在单进程宿主里也不需要）。唯一值得保留的是它的"协议版本号驱动重放"——`*_protocol` 字段变了就让读侧 `cursor = 0` 重建（`ledger.py:75-89`），比"让用户手动清缓存重开"友好。

**能力与发布口径**

9. 这套东西整体需要"游戏内暴露文件 + 游戏外起一个本地服务"。按我们的发布红线：**主模组保持 `<Capabilities/>` 自闭合、零能力**；账本/动作层若要给我们自己用，走开发桥接器（`深空桥接器/`，游戏内 MCP，不开端口不落文件）；若真要做成给玩家用的"AI 店长"，只做 companion 模组，并在模组菜单里显式告知"该模组会写 ModData 目录并被外部进程读取"。
