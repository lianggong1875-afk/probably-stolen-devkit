# 深空串门：可能是联机的 / Probably Multiplayer（真人顾客联机）｜工坊 3828909239

> 蒸馏自反编译源码 `缓存文件夹\decomp_ws\3828909239`（166 个 `.cs` / 约 2.6 万行；**34 个 HarmonyPatch 类**、**9 处 `ModHelper` 调用**、网络实现集中在 6 个文件；版本 `ProbablyMultiplayer 2.8.4`，入口 `ProbablyMultiplayer\Core.cs -> Core`）。
> 价值：**这是工坊里唯一一个"把真人玩家接进原版顾客流程 + 双方各自独立存档做真实物品交易"的完整样本**。11 万订阅、协议版本 35、仍在自己维护中继服务器。
> 一句话本质：**它不是 P2P 联机游戏，而是"一个服务器权威的串门会话 + 两边各自跑单机存档"**——服务器只持有"会话对象"（谁在谁店里、选中了什么、报价多少、成交流水号），物品与钱始终只在各自本地存档里改动。

## 0. 先做事实核对（几个容易误传的点）

| 常见说法 | 源码实测 |
| --- | --- |
| "25 个 HarmonyPatch" | **34 个 Patch 类**（实测）：24 个 `Human*`（真人顾客流程接管）+ 8 个 `ModWindow*` 与 `RetiringInputDeactivate`（自制窗口抢输入）+ 1 个 `CompanionInventoryPointer` |
| "用了 `StoreClientManager` / `SetClientSource`" | **本模组没有调用这两个 API**（它走的是更底层的手工路径）。它用的是 `StoreClientList.CreateInventor()` 造一个 `StoreClient` 数据模板 → `StoreClientInstance.CreateClientInstance(val)` → 直接把结果赋给 `PlayerStore.Instance.currentClientInstance`（NativeVisit.cs:355 / 379 / 402 / 408） |

> ⚠️ **本档案初稿的一处更正（2026-11-27 由主模型复核）**：初稿写的是"原版没有 `StoreClientManager` / `ModHelper.SetClientSource`"，**这是错的**。已在 `共享开发\reference\Assembly-CSharp\` 核对：
> - `StoreClientManager.cs` 存在（2239 行，含 `public void GenerateClient()`）；
> - `ModHelper.cs` 里有 `public static void SetClientSource(StoreClient client, string modId)`；
> - `StoreClientList.cs`(4252) / `StoreClientInstance.cs`(98) / `StoreClient.cs`(1303) 都在。
>
> 正确表述是：**原版有这两个口子，是本模组选择不用它们**（它要接管整个顾客生命周期，官方口子只够"声明来源"）。
> 对我们的实际结论也要改：要做"借壳真人/自定义顾客"，**先试 `ModHelper.SetClientSource` + `ModHook.OnGenerateCustomerVeryLate`**（官方口子，零 Patch），只有在官方不够用时才照本档案 §4 的手工路径。这条也说明了本经验库铁律的必要性：**任何"原版没有 X"的结论必须去 reference 目录核对，别凭模组里没出现就断言原版没有**。
| "Steam P2P / UDP" | 不是。**自写 WebSocket 客户端**（`CompressedWebSocket.cs`，TcpClient+SslStream 手写 RFC6455）+ 官方 HTTPS 发现服务 + 第三方中继服务器（`*.lemoc.icu`）。Steam 只用来**认证身份**（`ModHelper.RequestSteamAuthTicket` 或网页 code 换 token） |
| "同步库存/钱/天气" | **都不同步**。同步的只有"会话内的要约与选择"。天气没有任何代码涉及；天数只作为"在店/在夜"的在场广播 |
| "host 权威" | 不是 host 权威，是**服务器权威**：两端都是"发意图 + 整份覆盖服务端下发的会话快照"（见 §3） |

> 服务器端代码不在反编译产物里，所有服务端行为都是从客户端代码反推的，文中标「（推测）」。

## 1. 整体架构

### 1.1 三层分工

```
发现层  NetworkRouting：GET …/routes → 校验 protocol/minimumClientVersion/路由表(1..8)
        → 对每条路由并发 probe（2 次往返测 RTT，6s 超时）→ 选 RTT 最低
        → 回退：backup 域名 / 裸 IP:14514 / 玩家 ModData/server.txt
传输层  CompressedWebSocket（TcpClient+SslStream，手写 RFC6455 + permessage-deflate）
        NetworkRoute（endpoint 校验 + SPKI pin）／PinnedHttpHandler（pin 下的 GET）
        MarketConnection（收发循环/心跳/失败注入）／MarketInbox（32MB·256 条背压）
        ModWork（ModHelper.RunInBackground ↔ RunOnMainThread 的 Task 包装）
会话层  Protocol（信封 requestId/connectionId/visitId + Version=35）
        MultiplayerRuntime（★唯一状态机：连接、在场、匹配、会话、Handle 分派）
        NativeVisit（真人顾客）／ItemTransfer（快照+两阶段提交）／CounterOfferCache
        TradeUI（议价）／NativeChat（聊天）／StoresUI（店铺目录+排队）／ProfileState
表现层  OverlayUI·SocialWindow·WindowInput·MessageDialog（自制窗口框架）
        ChatWindow／ChatBubble*／Stickers／Apng·PngRaster·SpriteImageStore／ModText
```

### 1.2 生命周期（谁在什么时候启停）

- `Core.OnEnable`：`ModWork.Start()` → `NetworkRouting.Start(discovery, () => ModStorage.ReadText("server.txt"))` → 建 `DontDestroyOnLoad` 的 `MultiplayerDriver` GameObject → 组件 `MultiplayerDriver`。
- 每帧只有一个总入口：`Core.Update()` 依次调十几个 `Guard(...)`（`Core.Guard` 统一 try/catch + 打 `LogError` + 尽量弹 UI 提示），其中 `MultiplayerRuntime.Tick()` 是网络/会话主循环。
- `MultiplayerDriver`（9 行）只负责把 Unity 的 `Update` 转发给 `Core.Update`。
- `Core.OnDisable`：`NetworkRouting.Stop()` → 销毁 driver → `MultiplayerRuntime.Disconnect(shuttingDown:true)`。
- **读档/换场景成对处理**：`HumanExitPatch` 盯 `GameMaster.QuitToMenu/QuitToDesktop/NewGame`、`PlayerStore.LoadGame`、`EscapeUIManager.OnMainMenu/OnExit/OnPlaytestExitClicked` → `MultiplayerRuntime.BeforeSceneChange()`；`HumanShopLoadedPatch` 盯 `EmporiumEntry.Start` 与 `PlayerStore.LoadGame` 的 Postfix → `AfterShopLoad()`。`Tick` 里还有一道兜底：`PlayerStore.Instance.runID != boundRun` 就重走一遍场景切换清理（MultiplayerRuntime.cs:475-479）。

### 1.3 它刻意不做什么（这个"不做"划得很关键）

1. **不同步世界**：不搬顾客、不搬库存、不搬天数、不搬天气。
2. **不共享存档**：双方各跑各的 `PlayerStore`，没有任何一次"把对方存档覆盖过来"的写操作。
3. **不做自动重连**：连接死了就是死了，靠 UI 上的 Retry 由玩家手动重连（§2.7）。
4. **不做跨版本兼容**：协议对不上直接弹"请更新"，不降级（§2.6）。

## 2. 传输与协议

### 2.1 连什么地址

- 发现：`https://probably-multiplayer-discovery.lemoc.icu/routes`（`Core.DiscoveryEndpoint`）。
- 回退：`http://45.205.31.70:14514/routes`（明文 HTTP + 硬编码 IP）、`https://probably-multiplayer-discovery-backup.lemoc.icu/routes`。依次尝试，成功即止（NetworkRouting.cs:45-83）。
- 玩家可用 `ModData/server.txt` 覆盖（只允许 http/https；覆盖后**清空回退列表**）。
- 路由 URL 校验极严：必须 `wss`（或 loopback 的 `ws`）、非 loopback 时路径必须是 `/connect`、禁止 userinfo/query/fragment；**直连 IP 必须带 `spkiSha256` 公钥 pin**，否则拒绝（NetworkRoute.cs:84-101）。

### 2.2 传输实现（自己写的，不是 `ClientWebSocket`）

- 手写 HTTP Upgrade（`Sec-WebSocket-Version: 13` + 随机 16 字节 key + `Sec-WebSocket-Accept` 用 SHA1 校验）。
- **强制压缩协商**：请求 `permessage-deflate; client_no_context_takeover; server_no_context_takeover`，服务端不按这条协商就抛异常（CompressedWebSocket.cs:124-139）。
- 自己解帧：校验 RSV 位、长度扩展的规范形式、控制帧不得分片、close/ping/pong、UTF-8 严格校验、**单帧 8,000,000 字节上限**。
- 发送：帧头 + 4 字节随机掩码；`data.Length >= 1024` 才 deflate，客户端消息上限 4,000,000 字节；`SemaphoreSlim` 串行化写。
- `OnTraffic` 回调把每次收字节的时间戳写进 `lastReceived`——这是心跳判定"死链"的唯一依据。

### 2.3 序列化与信封

`Protocol`（62 行）是全部序列化约定：

- Newtonsoft.Json，`CamelCasePropertyNamesContractResolver`、`DateParseHandling.None`、`MaxDepth = 64`。
- 信封三件套：`requestId`（客户端生成，用于把异步回复和请求对上）、`connectionId`（`welcome` 里服务端下发，之后每条上行都带）、`visitId`（只有"visit 内"命令才带，见 `MultiplayerRuntime.Send` 里的白名单：`offer/quote/ready/leave/chat_typing/reject_item/select/withdraw_quote/buy/applied/chat`；其余命令没有 visit 上下文就抛 `No active human visit`）。
- **物品走的是原版存档片段**：`JsonUtility.FromJson<SaveState>(PlayerStore.Instance.EncodeItem(item))` 直接拿游戏自己的编码结果当 `WireItem.Data`，再**额外塞一个 `itemFeatures` 字典**（`ItemFeatureData.Write`，键为 `uniqueId`，值是 `[ES3Serializable]` 标记的 `ItemFeature`/`ItemCondition` 成员）。所以它是"把原版存档里的一棵树原样过线"。
- 读侧**严格**：`WireJson.GetProperty` 缺字段直接抛 `JsonException("Missing field: …")`，不静默给默认值。
- 收件箱有背压：累计 >32MB 或 >256 条就 `Enqueue` 返回 false → 连接层直接判定 `Incoming multiplayer messages exceeded the buffer limit` 并断开。

### 2.4 消息类型清单（从代码里数出来的，不是猜的）

**上行（客户端 → 服务器）**：
`steam_signin`、`steam_ticket`、`hello`、`status_request`、`probe`、`presence`、`availability`、`visit`、`cancel_wait`、`match_reply`、`leave`、`offer`、`select`、`quote`、`withdraw_quote`、`buy`、`ready`、`applied`、`reject_item`、`chat`、`chat_typing`、`store_list`、`store_save`、`store_open`、`store_close`、`store_join`、`store_cancel`、`store_presence`、`friend_request`、`friend_alias`、`friend_remove`、`friend_visit`、`friend_visit_cancel`、`friend_visit_reply`、`friend_reply`、`friend_chat`、`friend_chat_typing`、`friend_chat_more`、`rename`、`set_tags`、`set_bubble_position`、`equip_skin`、`equip_bubble`、`equip_banner`、`buy_skin`、`buy_bubble`、`buy_banner`、`redeem_skin`、`sticker_catalog`、`sticker_art`、`bubble_art`、`event_state`、`event_draw`、`event_answer`、`event_history`、`calendar_page`、`calendar_claim`、`visit_feedback`、`visit_report`、`moderator_chat`、`acknowledge_guidelines`

**下行（服务器 → 客户端）**：
`welcome`、`status_response`、`waiting`、`match_proposed`、`match_cancelled`、`no_visitor`、`visit_started`、`visit_updated`、`visit_profile`、`visit_name`、`visit_bubble_position`、`bubble_position_set`、`offers_updated`、`prepare`、`ready`(e)、`commit`、`complete`、`left`、`abort`、`chat`、`chat_typing`、`cancelled`、`ack`、`error`、`announcement`、`alert`、`moderation`、`network_error`、`steam_ready`、`steam_ticket_required`、`steam_ticket_failed`、`steam_signin_required`、`conduct_thresholds`、`community_guidelines`、`guidelines_acknowledged`、`profile_updated`、`skin_tokens_updated`、`skin_redeemed`、`skin_equipped`、`skin_purchased`、`banner_purchased`、`bubble_purchased`、`cosmetic_tabs`、`sticker_catalog`、`sticker_art`、`stickers_changed`、`bubble_art`、`event_state`、`event_changed`、`event_result`、`event_answer`、`event_history`、`calendar_status`、`calendar_page`、`calendar_claim`、`calendar_changed`、`store_list`、`store_saved`、`store_state`、`store_queue`、`store_queue_closed`

分派方式：`Handle` 开头先让各子模块"认领"（`NameRequiredUI/ModeratorChatUI/StoresUI/TagsUI/VisitReportForm/FeedbackUI/SocialState.Handle` 返回 true 就 return），剩下的大 `switch (text.Length)` + 首字母 + 全串比较（反编译产物形态，源码大概率是普通 switch）。

### 2.5 心跳 / 超时 / 断线（全部实测常量）

| 项 | 值 | 位置 |
| --- | --- | --- |
| 发现清单请求超时 | 5s，body ≤ 16384 字节 | NetworkRouting.cs:193-198 |
| 路由探测 | 6s 内 2 次 `probe`/`sequence` 往返，取平均 | NetworkRouting.cs:240-296 |
| 心跳 | 每 5s 发 `status_request`（带 connectionId） | MarketConnection.cs:182-195 |
| 死链判定 | **90s 无任何下行流量**（含 status 回包）→ `Connection stalled` | MarketConnection.cs:184-187 |
| 上行卡死 | 30s 拿不到发送许可/写不完 → `stalled: sending … 30 seconds` | MarketConnection.cs:244-257 |
| 等待 `welcome` | 15s（普通）/ 50s（已请求 Steam ticket）/ 5min（网页登录）/ 30s（发出 hello 后） | MultiplayerRuntime.cs:178, 221, 657, 670 |
| 响铃等真人上门 | 12s，超时 → 断开 + 立刻叫原版顾客 | MultiplayerRuntime.cs:385, 500-505 |
| 匹配保留 | 服务端下发 `expiresInMs`；好友串门本地再压 9s | MultiplayerRuntime.cs:991-1015；FriendVisitPreparation.cs:16 |
| 顾客走进来（动画） | 10s 未收到 `OnArrived` 就手动补一次 `OnArrived` | NativeVisit.cs:413, 933-947 |
| 帧/消息上限 | 单帧 8MB、客户端消息 4MB、收件箱 32MB / 256 条 | CompressedWebSocket.cs:19,148；MarketInbox.cs:15 |

### 2.6 协议版本号与不兼容怎么处理（对我们最重要的一条）

**版本信息有三条独立维度，全部在握手时上报或校验：**

```csharp
// Protocol.cs:10-12
internal const int Version = 35;
internal const string GameBuild = "VERSION 100";
```

**三道闸门，任何一道不过都直接拒绝，不做兼容：**

1. **发现清单闸门**：`routes` 响应必须给 `protocol` 与 `minimumClientVersion`（三段式 semver）；`protocol != 35` 或本机 `Core.Version`（程序集 3 段版本）低于最低版本 → 抛 `UpdateRequiredException`（见 §6 模式 A）。
2. **握手上报**：`steam_signin` 与 `hello` 两条消息都带 `protocol: 35` + `clientVersion: Core.Version` + `build: "VERSION 100"`（MultiplayerRuntime.cs:183-192, 222-241）。
3. **运行期推翻**：服务端任何 `error` 带 `code: "update_required"` → `UpdateRequired = true` → `Disconnect(forceEnd:true)` → 弹 **无 Retry 按钮**的模态框；此后 `StartConnection` 一旦看到 `UpdateRequired` 就直接 `ShowUpdateRequired()` 返回（MultiplayerRuntime.cs:1192-1199, 149-153；MultiplayerUI.cs:219-229）。

另外连接层也留了一条：`MarketConnection` 捕到 `UpdateRequiredException` 时会**伪造一条 `error/update_required` 消息塞进收件箱**，让 UI 层统一处理（MarketConnection.cs:143-152）。

**结论**：它把"版本不兼容"当成**唯一一种不可自动恢复的错误**——不降级、不忽略、不祈祷，直接告诉玩家去更新。这是 11 万订阅、跨多个游戏版本迭代还活着的前提。

### 2.7 没有自动重连（这是设计选择，不是缺陷）

- `Tick` 里没有退避重连；`network_error` 一到就整套 `Disconnect()`：清 Session、清所有子 UI、`SteamIdentity` 取消票据、物品预览全部销毁、原版顾客状态还原。
- 恢复路径只有两条：玩家点 Failure 弹窗上的 **Retry**（→ `SignIn`/`Connect`），或下次进店自动重新握手（`signInOnly` 分支，MultiplayerRuntime.cs:164-173）。
- 「好友私聊 checkpoint」是唯一带断线补偿的东西：`FriendChatHistories.StartSync()` 在 `hello` 里带上 `friendChatAfter`，重连后按 checkpoint 补拉历史并去重（FriendChatHistories.cs:54 的日志直接写了 `deduplicated on reconnect`）。

## 3. 状态同步模型

### 3.1 到底同步了什么

| 类别 | 是否过线 | 形态 | 权威在哪 |
| --- | --- | --- | --- |
| 账号档案（昵称、信誉 reputation、行为分 conduct、皮肤/贴纸/气泡/横幅、SkinTokens 币） | 是 | **整份快照覆盖**（`welcome.profile`、`profile_updated`） | 服务端账号库 |
| 在场（day + day/night） | 是 | 事件（`presence`，变化才发） | 客户端自报，服务端只转发 |
| 可接待（`availability`）、店铺在场（`store_presence`） | 是 | 事件（布尔值变化才发） | 客户端自报 |
| 匹配（`match_proposed` / `match_reply` / `match_cancelled`） | 是 | 请求-应答 + 服务端保留 | **服务端**（含过期时间） |
| 串门会话（谁在谁店、选中列表、报价、成交流水号、被拒物品） | 是 | **整份 `VisitSession` 覆盖**（`visit_started` / `visit_updated`） | **服务端** |
| 柜台商品（`offers_updated` / `offer`） | 是 | 事件携带 `WireItem[]` 整表 | 卖家客户端生成、服务端代存转发 |
| 交易（`prepare` / `commit` + `ready` / `applied`） | 是 | 服务端下发的 `Trade` 指令 | **服务端**（两端都必须确认） |
| 库存/钱/天数/天气/顾客池/声誉（游戏内存档的） | **否** | — | 各自的本地存档 |

### 3.2 权威归属：服务端持有会话对象，两端都是"视图"

`VisitSession`（43 行）就是那个被反复整体覆盖的对象：

```csharp
internal sealed class VisitSession
{
	public bool Friend { get; set; }
	public bool Store { get; set; }
	public string Id { get; set; } = "";
	public Peer Host { get; set; } = new Peer();
	public Peer Visitor { get; set; } = new Peer();
	public Dictionary<string, List<WireItem>> Offers { get; set; } = …;      // 按 sellerId 分桶
	public Dictionary<string, List<RejectedItem>> Rejected { get; set; } = …;// 对端缺内容类型的子节点
	public string? BuyerId { get; set; }
	public List<string> Selected { get; set; } = …;
	public int Revision { get; set; }                                        // ★冲突钥匙
	public Quote? Quote { get; set; }
	public string? TradeId { get; set; }
}
```

关键点：**客户端从不敢自己算出"现在是该成交了"，它只发意图**（`select` / `offer` / `quote` / `withdraw_quote` / `buy` / `ready` / `applied` / `leave`），服务端算完把 `visit_updated` 整份推回来，客户端 `NativeVisit.Apply(Session)` 无条件覆盖本地视图。
唯一"双方各自表决"的地方是 `visit_started`：**两端各自判断自己这一侧能不能接（在店/在夜/没在交易/没超次数），任一侧拒绝就 `leave`**（见 §6 模式 B）。

### 3.3 冲突与竞态怎么处理

- **`Revision` 单调号**：报价要带 `revision = session.Revision`，成交要带 `revision = session.Quote.Revision`。过期就等于被拒（服务端回 `error`），客户端重画 UI 而已——不做本地乐观更新。
- **报价撤回**：卖家一改价格输入框就自动 `withdraw_quote`（TradeUI.cs:208-222）；撤回期间再点确认不丢，而是 `confirmAfterWithdrawal = true` 排队，等服务端确认 `Quote == null` 后再补发（TradeUI.cs:297-305）。
- **"物品变了就别报了"**：`ReservedSale` 记下报价那一刻物品树的身份三元组（uniqueId / identifier / unitCount / 父容器），后续 `HoldsQuotedGoods` 一变就作废报价，抛 `An offered item changed. Offer it again.`（NativeVisit.cs:112-125；ReservedSale.cs:51-75）。
- **`ready` 阶段的失败**是可撤回的：`ItemTransfer.Prepare` 抛异常 → 立刻回一条 `ready{ok:false, reason}` → 服务端放弃这笔 → 双方库存都不动（MultiplayerRuntime.cs:844-869）。

### 3.4 "你店里的东西永远是你的"——代码里的四条实现链

这是它宣传语的真正落点，逐条都能指到行：

1. **对面看到的永远不是你的真物品，是不够格的克隆**。卖家柜台上的东西在对面只以 `Preview` 存在：`ItemTransfer.Restore` 把存档片段解出来之后**整棵树的 uniqueId 全部改成负数**（从 `-2000000000` 递增，`AssignIdentity`），再 `MakePreview` 剥掉 `IS_OWNED_TAG`、置 `forceDisableUse/forceDisableActivate`（ItemTransfer.cs:170-217）。这类克隆物品在原版逻辑里既不能卖也不能用，只能看。
2. **克隆被全面封死交互**：`SlotMarker.AcceptUnchecked` 前缀直接返回 0 拒绝放进任何容器（`HumanCounterPlacementPatch`）、双击开容器被拒（`HumanPreviewContainerPatch`）、价值查询返回快照里的 `BaseValue` 而不是实时估价（`HumanPreviewBaseValuePatch`）。
3. **真实物品只在"两阶段提交"里动，而且带落点预检**。卖家侧：`Prepare` 只把物品"预留"（`ReservedSale` + `lockAllInventoryFlag` 锁全局背包）；买家侧：`Prepare` 先造一个**影子背包**（`GameAccess.CreateStagingInventory` 复制柜台形状），用一堆 `processed_cheese` 占位把"柜台现有占用"复刻出来，再把要收的物品塞进去试——**塞不下就直接失败，绝不让物品凭空消失**（ItemTransfer.cs:261-302）。只有服务端下发 `commit` 才 `Commit`：改钱、改物品归属、`EmporiumEntry.Validate`，异常时钱和落点全部回滚（ItemTransfer.cs:316-394）。
4. **写存档的入口只有一个**：`ModStorage` 只碰 `ModHelper.*ModDataFile*`（模组私有目录：steam 会话、称呼、聊天 checkpoint），**从不触碰存档结构**；`MultiplayerState` 的计数写在 `PlayerStore.modData["lemocuber_probablymultiplayer.*"]` 私有键里（MultiplayerState.cs:47-77）。断线时 `Disconnect` 会把预览全部销毁、预留物品 `ReturnOriginal` 归位、原版 `currentClientInstance/isClientArrived/playerLocation/lastVisitedClient` 四个字段还原成进入前的快照（`RestoreClientState`，NativeVisit.cs:1123-1132）。

### 3.5 存档互相覆盖的可能性

不存在。没有任何代码路径写对方的 `PlayerStore`；唯一跨端的数据是"物品树的存档片段字符串"，而它在对面是**新建对象**，不是引用。它甚至专门处理了一个坑：原版 `EncodeItem` 会往 `PlayerStore.savedItemFeatureList` 里塞东西，所以捕获前后做 `RemoveRange` 回滚（ItemTransfer.cs:78-88），防止"只是看一眼库存就把自己的存档临时列表污染了"。

## 4. 真人顾客接管（把玩家塞进原版顾客流程）

### 4.1 伪造一个 `StoreClient`

`NativeVisit.Begin` 干的事：先快照原版四个字段，再造一个"发明家"类型的 `StoreClient` 数据模板，把回调全部置空（**不让原版任何脚本化对话/成交逻辑插手**），然后 `CreateClientInstance`：

```csharp
// NativeVisit.cs:355-380（原样）
		StoreClient val = StoreClientList.CreateInventor();
		string text = (val.spriteName = PlayerSprite.Register((IsVisitor ? session.Host : session.Visitor).SpriteUrl));
		val.possibleSprites.Clear();
		val.possibleSprites.Add(text);
		val.identifier = ClientId;                       // "lemocuber_probablymultiplayer_human"
		val.displayName = peerName;
		val.clientIntent = (ClientIntent)3;
		val.dismissable = true;
		val.nonShootable = true;
		val.arrestable = false;
		val.isBargainAvailable = false;                  // 关掉原版砍价
		val.isOfferWholesale = false;
		val.mainDialogue = null;                         // 关掉原版对话脚本
		val.repeatDialogue = null;
		val.allDoneDialogue = null;
		val.OnArrival = null;
		val.OnDismiss = null;
		val.onItemBought = null;
		val.onItemSold = null;
		val.onDealAccepted = null;
		val.onItemAddedToCounters = null;
		val.onItemRemovedToCounters = null;
		val.onItemAddedToClient = null;
		val.onItemAddedToWeighted = null;
		human = StoreClientInstance.CreateClientInstance(val);
		Active = true;
```

再分两种角色落地（NativeVisit.cs:396-421）：

- **我是访客**：`NightVisitTravel.Enter(...)` 把地图切到"夜里去别人店"的场景 → `lastVisitedClient = human` → `playerLocation = 1`（表示"不在自己店"）→ `currentClientInstance = human` → `isClientArrived = true`。
- **我是店主**：`currentClientInstance = human`、`isClientArrived = false` → `StoreUIManager.Instance.DisplayClient()` + 设头像 + `clientGameObject.StartApprochingStore()`（**借用原版的走进店动画**），并起 10s 兜底计时器。

### 4.2 24 个 `Human*` Patch 的分工

| 目的 | Patch（原版方法） |
| --- | --- |
| 不让原版抢"下一个顾客" | `TryCallNextClient` → `TryNextHuman()` 决定是否拦；`OnNextClientArrived` 直接拦掉 |
| 不让原版推进对话 | `DialogUIManager.OnCurrentTextDisplayed` 拦掉；`SetNormalMode` 之后重新套用对方气泡颜色/位置 |
| 走进/走出动画只认这一个对象 | `StoreClientMono.OnArrived`（→`NativeVisit.OnArrived`）、`OnLeft`（→`FinishEnd`），都先比较 `__instance == StoreUIManager.Instance.clientGameObject` |
| 关掉原版议价 | `NegociationUIManager.OpenUI` → 改成开 `TradeUI`；`BargainUIManager.OpenUI/ToggleUI` 拦掉 |
| 关掉原版 9 个成交入口 | `PlayerStore` 的 `BuyItemFromTable/BuyItem/SellItem/SellItemFromTable/AcceptBarter/HandleAutoNegoUIDisplay/PlacedItemForBuying/PlacedItemForSelling/PlacedItemForBarter` 全 Prefix 拦 |
| 柜台物品只准放"自己的" | `SlotMarker.AcceptUnchecked`（见上文） |
| 预览物品的价与容器只读 | `GameItem.GetRefreshedValue/GetCurrentValue/GetNegociatedValue`、`ItemMouseDoubleClickHandler.OpenContentAction` |
| 库存变动感知 | `GameItem.AddParent/RemoveParent` 的 Postfix → 只在 `Active && !Syncing && !Applying` 时通知 `CounterOfferCache` |
| 不许带病离场 | `PlayerStore.EndNight`、`MapUIManager.OpenUI`、`MapUIManager.LeaveUpgradeMerchant`、`StoreShutterButton.EndAndClose`、`PlayerStore.DismissCurrentClient` |
| 场景切换/读档 | `HumanExitPatch`（7 个方法）、`HumanShopLoadedPatch`（2 个方法） |
| 聊天输入不被原版抢 | `InputActionManager.UpdateKeys`、`TMP_InputField.DeactivateInputField`、`InputActionManager.Update`、`QuantumConsole/KeyBinderModule.Update`、`DialogUIManager.OnCurrentTextDisplayed` |

### 4.3 聊天怎么接上 `DialogUIManager`

`NativeChat`（277 行）**不复用原版对话系统，而是"劫持它的气泡"**：

- `Begin()`：`InterruptDialog()` + `HideAllDialogUI()`，然后把 `DialogUIManager.clientBubble` / `clientNameBubble` 的 `RectTransform`、锚点位置、原始 sprite 全部存下来（离开时逐个还原）。
- 收消息：`Receive(...)` → 压进 `pending` 队列；`ShowPending()` 时调 `instance.DisplayClientText(new Dialogue().SetText("CLIENT", 文本))`，再把 `clientNameTMP.text` 改成对方昵称、打开名牌气泡。
- **自己的消息走原版历史**：`sender == 自己` 时只 `UpdateDialogHistory("昵称: 文本")`，不弹气泡。
- 表情贴纸不是文字：`stickerId != null` 时在气泡上方挂一个 120×120 的 `OverlayUI` 图片层，8 秒后销毁。
- 气泡颜色/位移由对端上传（`visit_bubble_position` → `Peer.BubbleX/BubbleY/DialogColor`），`ApplyDialog()` 负责套用；`SetNormalMode` 之后要重套一次，因为原版会重置。

### 4.4 议价怎么和原版 `NegociationUIManager` 对齐

**没有对齐，是替换**：`NegociationUIManager.OpenUI` 被 Prefix 改成 `TradeUI.Open()`，`TradeUI.Begin()` 直接把 `NegociationUIManager.Instance.uiObject` 和 `BargainUIManager.Instance.uiObject` 一起 `SetActive(false)`（TradeUI.cs:82-89）。

自制的 `TradeUI`（446 行）复刻了原版议价窗的语义但换了状态机：

- 价格默认值是"选中物品 BaseValue 之和"，输入被限制为 `0..2000000000` 的整数（TradeUI.cs:229-233）。
- 卖家侧显示 `Confirm price`，买家侧显示 `Buy`，余额不足时按钮灰掉；状态行覆盖"等待对端/价格已确认/余额不足/物品更新中/交易提交中"五态。
- 所有状态都从 `Session` 派生（`Quote`、`TradeId`、`Selected`、`Revision`），所以**对端/服务端一改，窗口自己就重画**；`selectionKey`/`itemKey` 两个字符串做脏检查，避免每帧重建 UI。
- 原版"砍价"（`BargainUIManager`）在真人对话里被彻底关掉，砍价只保留在 NPC 顾客流程里。

## 5. 安全与健壮

### 5.1 传输面（这部分做得相当硬）

- 强制 WSS + 可选 SPKI pin（`spkiSha256`，32 字节，`RuntimeCompat` 手写 DER 解析取 SubjectPublicKeyInfo，常数时间比较）。
- 升级响应校验：拒绝重复 `Sec-WebSocket-Accept`、强制 `Upgrade: websocket` / `Connection: Upgrade`、强制独立的 `permessage-deflate`（no_context_takeover 双向）。
- 全部输入做长度上限：HTTP body 16KB、帧 8MB、客户端消息 4MB、收件箱 32MB/256 条、inflate 输出 8MB（防解压炸弹）；严格 UTF-8（`throwOnInvalidBytes`）+ 帧"最短长度编码"校验（防歧义帧）。

### 5.2 消息面

- 信封缺失即抛异常（`WireJson`），不做默认值兜底。
- `visitId` 必须等于当前 Session（`CurrentVisit`），否则整条消息丢弃——这一条挡住了"上一个串门会话的迟到消息打乱当前会话"。
- 数字字段全部做语义范围检查：`ReadCounts` 校验 `online>=1、0<=visiting<=online、0<=waiting<=online-visiting` 等（MarketConnection.cs:165-176）；`Protocol.ClampDay` 把天数夹到 `1..100000`；`MultiplayerState.CountToday` 夹到 `1..10`；`NameRequiredUI` 限 1..40 字符。
- 身份值全部正则校验：`steamId ^[1-9][0-9]{16}$`、会话令牌 `^[a-f0-9]{64}$`、登录 code `^[A-F0-9]{10}$`、房间码 `^[a-zA-Z0-9_-]{0,16}$`、路由名 `A-Z0-9_-` 且 1..12 字符。

### 5.3 物品面：它防住了什么、没防住什么

**防住了（有代码证据）**：

- 非法/无法解码的物品：`Restore` 里 `DirectoryMaster.Has<GameItem>` 逐个检查整棵树，缺类型就抛 `MissingItemTypeException`（带节点下标），客户端回 `reject_item`，对方 UI 显示"你的某个子物品对这个玩家不可用"（`ItemRejectionPaths` 生成 `A > B > C` 路径）。服务端也会主动下发 `item_forbidden` + `rejectedItems`。
- 解码后与被报价时不一致：`identifier` 必须对得上、树节点数必须等于存档节点数、解码出的物品不能已经有父容器（ItemTransfer.cs:150-161）。
- 塞不进去的物品：`Prepare` 的影子背包预检（见 §3.4）。
- 钱不够/会溢出/单方面刷状态：买家侧 `playerCash < Price` 抛、卖家侧 `playerCash > int.MaxValue - Price` 抛；`Selected` 与 `Quote` 只听服务端的，本地 `select` 只是上报。

**没防住（重要结论）**：

- **物品内容本身没有签名，也没有服务端校验**。`WireItem.Data` 是发送方客户端自己 `EncodeItem` 出来的字符串，服务端只是转发 + 检查"对面能不能解码"。一个改过的客户端完全可以构造一棵"合法但不存在于自己存档"的物品树发过去（标记：**代码里没有任何 hash/签名/服务端二次校验**，此为读码结论，不是推测）。
- **钱的对价由卖家自己填**（`quote.price`），服务端只校验 `0..2e9` 区间。所以"1 块钱买你整个柜台"在协议层是合法的，靠的是双方自愿 + 举报/行为分体系（`visit_report`、`visit_feedback`、`conduct_thresholds`、`moderation`）。
- **`reject_item` / `item_forbidden` 的语义是"内容不匹配"（缺模组/缺原版物品），不是"疑似作弊"**。别把它当反作弊读。

### 5.4 断线时本地存档怎么保护

按优先级排的六道：

1. `Disconnect` 是**幂等且全覆盖**的：预览销毁 → 预留物品 `ReturnOriginal` → `ItemTransfer.Release()`（卸掉 `lockAllInventoryFlag`，且只在 `runID` 还是同一个时才减，防止跨档减错）→ `NativeVisit.End()`。
2. `BeforeSceneChange` 在 7 个退出/读档入口前跑（含 `LoadGame`、`NewGame`、测试退出），确保不会有"改到一半的交易"跨档残留。
3. 交易里的每一步都有回滚：`Commit` 失败先把 `playerCash` 还原、把已落位的物品 `Expel`，再抛。
4. `AbortStartup` 用 `Cleanup(stage, action)` 分阶段清理，**每阶段独立 try/catch 并打 Warning**——一个阶段炸了不会拖垮后面五个。
5. 访客启动失败时直接 `PauseAfterVisitFailure`：把整个模组置 `MultiplayerPaused = true`，**直到读档才解锁**（MultiplayerRuntime.cs:1531-1587）。宁可这次不联机，也不在半坏状态下继续。
6. `HumanSleepPatch`/`HumanShutterClosePatch`/`HumanNightShopLeavePatch`/`HumanBagContinuationPatch` 把所有"能把你带离现场"的原版入口逐个封住，并给出可读提示（"Leave or cancel the visit before going to sleep."）。

## 6. 可复用模式（4 个）

### 模式 A：协议版本协商——"三重闸门 + 零降级"

要点：版本号是**常量**，出现在发现清单、握手、错误码三处；任何不匹配立刻进入"只能更新"的终态，且该终态对玩家可见、对代码是短路。

```csharp
// NetworkRouting.cs:196-214（原样）
				using HttpResponseMessage response = await bootstrapHttp.GetAsync(discovery, HttpCompletionOption.ResponseHeadersRead, deadline.Token).ConfigureAwait(continueOnCapturedContext: false);
				response.EnsureSuccessStatusCode();
				byte[] bytes = await ReadLimited(response.Content, 16384, deadline.Token).ConfigureAwait(continueOnCapturedContext: false);
				JToken value = Protocol.Parse(Encoding.UTF8.GetString(bytes));
				string text = value.GetProperty("minimumClientVersion").GetString();
				if (text == null || text.Split('.', StringSplitOptions.None).Length != 3 || !Version.TryParse(text, out var result) || result.Build < 0)
				{
					throw new IOException("Invalid minimum client version");
				}
				if (value.GetProperty("protocol").GetInt32() != 35 || new Version(Core.Version) < result)
				{
					throw new UpdateRequiredException();
				}
				JToken property = value.GetProperty("routes");
				int arrayLength = property.GetArrayLength();
				if ((arrayLength < 1 || arrayLength > 8) ? true : false)
				{
					throw new IOException("Invalid route count");
				}
```

配套的另一半（同一条链的终点）：`error{code:"update_required"}` → `UpdateRequired = true` → 断开 → 弹**没有 Retry** 的框 → 此后所有连接尝试被 `StartConnection` 早退拦截（MultiplayerRuntime.cs:149-153, 1192-1199）。

### 模式 B：服务端权威会话——客户端只发意图，快照整份覆盖

同一文件两段（中间的类型分支略）：

```csharp
// MultiplayerRuntime.cs:938-950（原样）
				if (text == "visit_updated" && CurrentVisit(message))
				{
					Session.Revision = message.GetProperty("revision").GetInt32();
					Session.BuyerId = message.GetProperty("buyerId").GetString();
					Session.Selected = Protocol.Decode<List<string>>(message.GetProperty("selected"));
					Session.Quote = (((int)message.GetProperty("quote").Type == 10) ? null : Protocol.Decode<Quote>(message.GetProperty("quote")));
					Session.TradeId = message.GetProperty("tradeId").GetString();
					Session.Rejected = Protocol.Decode<Dictionary<string, List<RejectedItem>>>(message.GetProperty("rejected"));
					NativeVisit.ApplyOfferRevision(Session);
					NativeVisit.Apply(Session);
					MultiplayerUI.Refresh();
				}
				return;
// MultiplayerRuntime.cs:1034-1039（原样）
				if (text == "offers_updated" && CurrentVisit(message))
				{
					Session.Offers[message.GetProperty("sellerId").GetString()] = Protocol.Decode<List<WireItem>>(message.GetProperty("items"));
					NativeVisit.ClearForbiddenItems(message.GetProperty("sellerId").GetString());
					NativeVisit.Apply(Session);
				}
```

价值：**没有任何本地"我先记着，等对端确认"的乐观状态**。UI 永远从 `Session` 派生，所以"两端看到的东西不一样"这件事在结构上不可能发生。代价是每次操作都要一个 round-trip（它接受这个代价）。

### 模式 C：把真人塞进单机流程——"借壳 + 全回调置空 + 全面封路"

§4.1 的 `Begin`（NativeVisit.cs:355-380）是核心：**造一个原版类型的数据对象，但把它的所有脚本化回调置 null，再把它挂到原版自己管理的那几个字段上**。之后用 24 个 `Human*` Prefix 把原版所有会"推进这个对象"的入口逐个关掉，只留"走进来/走出去"两个动画回调。

我们内部提炼的三条经验：

1. **先置空回调再挂上去**——顺序反了就会有一帧被原版脚本驱动，那一帧就能写坏存档。
2. **动画不要自己写**：`StartApprochingStore()` / `StartLeavingStore()` 直接复用，配合 10 秒兜底计时器（回调不来就手动补 `OnArrived`）。
3. **必须快照并还原原版字段**：`currentClientInstance / lastVisitedClient / isClientArrived / playerLocation` 四个字段进出各一次（`RestoreClientState`），否则结束串门后原版顾客系统会处于半状态。

### 模式 D：断线降级——"退回单机，而不是让玩家卡住"

```csharp
// MultiplayerRuntime.cs:1403-1426（原样）
			if (text == "network_error")
			{
				bool flag3 = BellRequest != null;
				bool flag4 = signInOnly && SteamIdentity.AccessReady && PlayerNameUI.Visible;
				bool flag5 = signInOnly && PlayerNameUI.Visible;
				Disconnect();
				if (flag3)
				{
					ResumeNext();
				}
				Core.Log?.Warning("Connection lost: " + (message.GetProperty("message").GetString() ?? "Unknown error"));
				if (flag4)
				{
					SignIn();
				}
				else if (flag5)
				{
					SteamIdentity.Notice = "Could not connect to multiplayer. Please try again.";
				}
				else
				{
					MultiplayerUI.Failure("Could not connect to multiplayer. Please try again.");
				}
			}
```

关键在 `flag3`：如果此刻正处于"响铃等真人上门"（`BellRequest != null`），断线后**立刻 `ResumeNext()` 叫一个原版 NPC 顾客进来**（并弹一条 "Server timed out; normal customer called."）。玩家看到的不是"卡住的空店"，而是"真人没来，来了个 NPC"。这是"联机能力失效 → 单机能力接管"的标准降级，而不是把错误丢给玩家。

## 7. 对我们的可用点

### 7.1 对《深空电玩》`deepspace_arcade`

- **模式 A 直接可搬**：如果电玩将来有"排行榜/联机对战/共享机台"，用"常量协议号 + 清单校验 + 单一 `UpdateRequired` 终态 UI"三件套，比写兼容层便宜得多。注意它把"最低客户端版本"做成三段 semver 是必要的（程序集版本就是三段）。
- **模式 D 的降级语义**："联机不可用 → 立刻退回单机流程并给一句人话"，比弹错误框好。电玩里对应的是"远程对手掉线 → 自动切本地 AI"。
- 可借的小工具：`ModWork` 那种把 `ModHelper.RunInBackground / RunOnMainThread` 包成 `Task<T>` 的写法（含 `CancellationToken` 生命周期 + `OnMainThread` 的 `TaskCompletionSource` 派发），是"网络/IO 线程绝不碰 Unity 对象"的干净边界，几十行就能抄。
- **不建议抄**：`CompressedWebSocket`（自己实现 RFC6455）。除非我们真的需要 permessage-deflate 且不想依赖平台 WebSocket——那是 350 行高风险代码。

### 7.2 对《深空当铺：可能是群友》`freya_cornell` / `DeepSpacePawn`

这个模组给我们最大的启示不是"联机"，而是——**"群友"其实不需要真联机，只需要模式 C 的"借壳"**：

1. **把群友做成"真人顾客"的本地版**：用同一套 `StoreClientList.CreateInventor()` + `CreateClientInstance` + 挂到 `PlayerStore.currentClientInstance` 的手法，把"群友 NPC"塞进原版顾客流程。好处是**白拿原版所有表现**：走进店动画、头像位、对话框、名牌气泡、离场动画、`isClientArrived` 参与的原版判定。这比我们自己造一套"对话 UI"成本低一个数量级。
   - 抄它必须抄的三件事：回调全置空、进出各快照/还原四个原版字段、10 秒兜底 `OnArrived`。
   - 我们的规范额外要求：进入前后要判 `PlayerStore.HasInstance`；Patch 里访问 `PlayerStore.Instance` 前先判。
2. **`NativeChat` 的"劫持气泡"法**：不重做对话框，只接管 `DialogUIManager.clientBubble` 的位置/颜色/名牌文本，离开时还原原始 sprite。群友说话就是"改原版气泡内容"。这一招对"群友台词/表情包"极其划算。
3. **模式 B 的轻量版可用于"回放"**：群友的聊天/议价如果要做"异步留言/离线回复"，可以照抄"服务端/存档持有一份会话快照，UI 只从快照派生"的结构——甚至不需要服务器，把快照存进模组私有 ModData 即可。这样 `VisitSession` 那份 schema（host/visitor/offers/rejected/selected/quote/revision/tradeId…）直接变成"群友串门记录的存档结构"。
4. **物品交换千万别共享库存**：如果将来要做"群友送你的东西"，抄 §3.4 的四条（预览用负数 uniqueId 假物品 + 剥 `IS_OWNED_TAG` + 落点用影子背包预检 + 提交带回滚）。**不要**抄它的"物品内容不过校验"——我们本地跑，直接 `DirectoryMaster.Item(id, true)` 生成更安全，别让存档片段跨进程过线。

### 7.3 如果哪天真的要做联机：该避开什么坑

| 坑 | 它的表现 | 我们该怎么做 |
| --- | --- | --- |
| **供应商锁定** | 硬编码 `*.lemoc.icu` + 一个裸 IP 明文回退；域名一挂，11 万订阅全灭 | 要联机就必须有"自建 + 玩家可配 server.txt + 至少两条独立回退"；且别把裸 IP 写死 |
| **服务器全权威 = 必须有服务器** | 所有会话状态都在服务端，客户端无法 P2P；断服即无功能 | 我们没运维，就别承诺联机。要么走"异步数据交换"（存档片段/留言），要么做本地同机多窗口 |
| **物品数据不过校验** | `WireItem.Data` 是发送方自定义存档片段，服务端只查"能不能解码" | 若真过线：只传 `identifier + unitCount + 明确字段白名单`，收方用 `DirectoryMaster.Item(id, true)` **本地生成**，绝不反序列化对端的存档字符串 |
| **无自动重连** | 掉线即散场，靠玩家点 Retry | 我们要做就做指数退避 + 会话续租（`visitId` + 序号重放），至少"重连后回到同一个 visit" |
| **单一巨型 `Handle`（约 800 行 if/else 链）** | 反编译出来是 `switch(text.Length)` 套首字母比较，加一个消息类型要改三处 | 用 `Dictionary<string, Action<JToken>>` 注册表 + 每个模块 `TryHandle` 自注册；消息类型是字符串常量表 |
| **34 个 Patch，其中 9 个在抢输入/遮挡** | 自制窗口自己做焦点、射线、Esc、滚轮拦截（`EventSystem.RaycastAll` 都改了） | 我们做 UI 一律走原版 `CustomUIWindow`/`SocialWindow` 那套公开窗口，别 Patch `EventSystem`；真要做输入隔离，只拦 `TMP_InputField` |
| **`PlayerStore.Instance` 裸取** | 多处直接 `.Instance`（靠外层 `ShopReady` 兜住） | 按我们的规范：先 `PlayerStore.HasInstance` 再取，Harmony 入口做边界捕获 + `LogError` |
| **空 catch / 静默降级** | 少数处 `catch { return false; }`（如证书校验）、`catch { Log?.Warning(...) }` 继续跑 | 内部逻辑不吞异常；仅在 Harmony/帧回调边界捕获并打 `LogError` 带堆栈 |
| **性能：柜台物品全量编码** | 每件物品要 `EncodeItem` 一遍（原版序列化），所以它才需要 `CounterOfferCache` 分帧（每帧最多 4 件或 1ms）+ 3.5MB 上限 + "物品太多"提示 | 任何"把库存过线/落盘"的功能都必须分帧 + 硬上限 + 明确的 UI 反馈，别在主线程全量遍历 |

### 7.4 一句话总结

**它真正值得抄的不是"网络编程"，而是三个"边界设计"**：版本边界（不兼容就拒绝）、权威边界（谁持有状态谁说了算，另一方只发意图）、失败边界（联机挂了立刻退回单机，而不是让玩家卡住）。而"真人顾客借壳进原版流程"这一招，是它给《可能是群友》最直接的礼物。
