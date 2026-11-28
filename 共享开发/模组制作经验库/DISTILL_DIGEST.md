# 工坊蒸馏知识｜二次提纯高密度速查卡（DISTILL_DIGEST）

> **压缩源**：99 份工坊蒸馏档案（原始 35,376 行 / 涵盖 156 个独立工坊 ID）→ **金字塔两级提纯**（更新时间：`2026-11-28 07:58:56`）。
> **省 Token 用法**：先扫「零、12 条黄金定论」解决 90% 决策；需查独门案例再扫下方 1 行增量招式；需原版签名/行号直接跑 `python 工具库/kb-tools/kb_pipeline.py lookup "<词>" --read`。

## 零、二次蒸馏核心定论（16 批 170 模组 / ~193 万行源码共性提纯）

> 下方 12 条已对照 `Assembly-CSharp` 核真并跨多模组互证，涵盖 90% 高频设计决策；**查具体 API 行号/踩坑细节请直接跑 `python 工具库/kb-tools/kb_pipeline.py lookup "<关键词>" --read`**，勿整篇硬读 100 KB 大手册。

1. **架构与 0-Patch 优先**：先查原版三扩展面（`ModHelper` 145 / `ModHook` 44 / `GameItemFunc` 51）；合集四态选型：50+ 静态委托表 / 20~50 自描述注册表 / 热开关用可摘挂表 / 3~5 设施用常驻+早退；同类实体 ≥15 件用数据表驱动；200+ 补丁必配分相优先级（`800` 准备 / `0` 常规 / `-50` 裁决）+ 作用域令牌 + 模态仲裁。
2. **贴图解析链定论**：`GameItemElement.ResolveSpriteByName` 两条分支互不交叉——`useSpriteFromMod=true` 走 `LoadedMod.Sprites`（不进 `SpriteDict`），`false` 走 `RenderHandler.LoadFromAtlas`。故**物品图标自愈挂 `ResolveSpriteByName` Prefix（声明 `HarmonyBefore`）**，切回原版图集必清 `useSpriteFromMod`；**立绘留 `SpriteDict.GetSprite`（`Priority.Low` 只认己键）**；角标首选 0-Patch `GameItemFunc.getItemSpriteModifierForItemFunc`。
3. **机器、永久加成与流体**：新机器用 `SetupBatterySlot`+`SetupModuleBay`+`SetupManualSlot`+`InitMachinery`+`STANDARD/TURBO`+断电退款；临时加速用 `TransientTagBoost`（Prefix 暂存 + Finalizer 还原）；**永久加成禁直改 `TOTAL_PERCENTAGE_*`**（拔模块会被 `ModuleHelper.ComputeModuleEffect` 冲零，须存私有 tag + Postfix 重算）；给机器窗加面板 Postfix `MachineHelper.CreateMachineryWindow`（或机器类 `CreateMachineInventoryWindow`）调 `MachineryPanelRow.GetPanel/Add` 判重；液体为 `CURRENT_PART_<LIQUID>` 整数微升 tag，搬运用 `WaterHelper.SplitProportionally`/`TransferLiquid`。
4. **物品发放、搬运守恒与堆叠红线**：判注册用 `DirectoryMaster.Has<GameItem>`；发玩家 `Item(id, true)`、NPC `Item(id, false)`；造成品机器/工具走 `PreBuiltItemHelper`；`TryAcceptAllOrDestroy`/`EmporiumTryAdd`/`TryAddDirectSellingItemToTable` 满仓会**静默销毁**，须先 `GraphUtils.CanAccept` 并验 `parentInventory == targetInv`；原位换件用 `GraphUtils.SwapItemsInPlace`；`GameItem.GetSplitItem` **不复制私有 `state`**，带状态物品禁开 `mayStackItemFunc` 堆叠。
5. **真伪、鉴定与防剧透**：真伪/成色唯一真相是 `ItemFeature.realCondition.identifier`（禁读显示名/品类/`fakeCondition`）；排序/整理严禁读 `realCondition`；**Tooltip 是渲染层**会被美化模组折叠，唯一赝品提示必须画在自控检视窗/估价单；禁对 Appraisal Assistant 5 红线方法（`GameItem.IsTag`、`AppraisalHelper.GetCorrectBaseValue` 等）做破坏性 Prefix。
6. **数值读取与交易/经济**：读最终声望/钱/进度用 **Prefix 存档 + Postfix 差分**（绝不复算难度/perk/±200 封顶公式）；补货/重掷调 `StoreClientList.AddScavGeneralStock`/`PlaceInventorInventory`/`PlaceSupplierInventory`（不自建 `Random`）；营收统计挂 `RunStats.OnItemSold` Postfix；复杂经济走影子账本 + 日种子回放；注意 `GetCurrentValue()` 认 `IsTag` 而 `NegociationData.IsNegociationDataAffectItem` 认 `GetGameItemType()`。
7. **事件、委托与异步三件套**：自定义事件用自持蓝图 + `StoreEventManager.ActivateEvent` + `negociationDatas`（零价格补丁）；扩展原版委托挂 `StoreRequest.FillRequest`/`ActivateRequest`；先扣钱后发货必分态——失败记 `pending_delivery` 开卷帘重试；异步加载必带三件套（`_loadGen++` 丢弃并 `Destroy` 旧资源 + `ConcurrentQueue` 主线程泵 + 让位前缀）。
8. **夜出地点五档与回店发奖**：①特性改随机源 → ②蹭原版地点换背景 → ②′`ModHelper.AddAfterhourLocation`（11 参）+ 自建全屏舞台 → ③′`AfterhourLocation.Register` 零反射真子类 → ③反射伪造出门（慎用）。取消行程走 `EmporiumEntry.OnArriveStore(fromMap: true)` 防误扣次数；奖励落账回店校验 `IN_STORE` 幂等发。
9. **UI、模态隔离与本地化**：模态窗挡背后物品拖拽只写 `__instance.hoveringStoreUI = true`；改命中/拖拽须**判定位翻返回值 + 动作位 `return false` 成对**；禁在 `Init`/`OnEnable` 缓存 `LocHelper.Get`；CJK 字体走 `TMP_Settings.fallbackFontAssets`；禁以展示文本括号/中文原句做解析键。
10. **存档防污染、旧档迁移与热重载**：能存 `GameItem.state` tag 就不建全局表；`PlayerStore.storeService`/`PhoneClientDict` 等 ES3 序列化表必须在 `SaveGame`/`InitialSave`/`WriteSlotFile` 的 **Prefix 摘除 + Finalizer 还原**（勿放 Postfix）；旧档改名/清下线 ID 挂 `SaveManager.DecodeNodes` Prefix；访问 `PlayerStore.Instance` 前必判 `PlayerStore.HasInstance`。
11. **跨模组冲突 4 大高频病根（15 例实证）**：①**整块失效**=热点 Prefix 抢 `return false` 或 Transpiler 互相拆坏指令（改 Postfix 追加、闸门挂动作位不挂判定位）；②**卡过夜/存档**=非事务写盘（须备份→改→复核→回滚）；③**顺序静默失效**=`HarmonyAfter/Before` 的 `mod.<ID>` 大小写与对方 `manifest.xml` 不符（不报错！）；④**旁路原版状态机半残**=须照写原版状态 tag、走原版暂停与 `EmporiumOption` 音量。
12. **零能力安全与加载器红线**：正式版 `<Capabilities/>` 用 `ModHelper.ReadModFile*`/`*ModDataFile*`（含收件箱）+ `ModReflect`；禁放 `obj/`/`bin/`/`.ps1` 进 mod 目录（`payload-file` 拒载）；严禁 `[ModRestricted("loader-target")]`、`ES3.Save("modOrder"/"modTrust")`、拼 `char[]` 绕扫描、空 `catch {}` 及补丁 `<>c` 编译器生成名。

---

## 一、大型 / DLC / 巨型架构与合集（9）

- [DangpuPlus](<workshop-distill/3828893950-DangpuPlus.md>)：50+常驻合集用FeaturePack静态委托表+熔断；渲染只成对包裹
- [ElectronicsExpansion](<workshop-distill/3828902092-ElectronicsExpansion.md>)：389补丁分相(800/0/-50)+JSON Schema证伪+Seal()禁重序号
- [PenQoL补丁表](<workshop-distill/3829957165-PenQoL补丁表.md>)：N开关合并1补丁且逐条try/catch；Applied对照GetPatchInfo验真
- [StoreQoLSuite](<workshop-distill/3832013611-StoreQoLSuite.md>)：子类化GameSlotInventory免全局Patch；撤离失败封存档
- [UpperLevels](<workshop-distill/3838944803-UpperLevels.md>)：不建新场景，Difficulty.Current.source单点总闸+原地换皮换客
- [ClaimsAndUprisings](<workshop-distill/3841509159-ClaimsAndUprisings.md>)：IDisposable作用域+单点模态仲裁+Fork/原子提交压薄233补丁
- [ZohFramework](<workshop-distill/3845965318-ZohFramework.md>)：0 Patch外置剧本DSL+坏脚本降级普通客；allDoneDialogue插闲聊
- [玩法模块三例-职业拍卖灾难](<workshop-distill/玩法模块三例-职业拍卖灾难.md>)：营业用OnShutter、夜出回店发奖、连环用StoreEvent+老档补纸条
- ⚠️[PerksProbablyFun](<workshop-distill/3834226532-PerksProbablyFun.md>)：用SaveFiles.IndexSave存跨档预设，零IO合规导出 ｜ **⚠️禁**：禁大量catch吞错、GetNegociatedValue递归Prefix双算、掐脏标及Update无门跑

## 二、机器、生产链、自动化与流体（13）

- ★[生产链与自动化四例](<workshop-distill/生产链与自动化四例.md>)：标准机器四件套+DecodeNodes改名迁移+工作区容器EndNight自动化
- [BetterBottlePrinter](<workshop-distill/3829069448-BetterBottlePrinter.md>)：扩槽白名单先调oldGate再追加，防吞他模组规则
- [MachineStatusBadges](<workshop-distill/3830844233-MachineStatusBadges.md>)：静态徽记走原生SpriteModifier零Patch；动态用2ms分帧泵
- [RhodesPharma](<workshop-distill/3830995342-RhodesPharma.md>)：WriteSlotFile前后摘挂派系防脏档；副本槽+文件IPC自动验收
- [FarmStory](<workshop-distill/3832249311-FarmStory.md>)：改存档id须在DecodeNodes前；IsDecoding期间禁写迁移
- [PipelineController](<workshop-distill/3845199370-PipelineController.md>)：GameItemFunc三委托代Patch，管网图存物品Tag按拓扑结算
- [内容三例-人脉拓展·更好储物·空白卡压制](<workshop-distill/内容三例-人脉拓展·更好储物·空白卡压制.md>)：关系网存进度免造NPC；过滤委托吃扩容套件；单int tag零补丁造机
- [内容四例-水塔·集成芯片·酵母·存储分页](<workshop-distill/内容四例-水塔·集成芯片·酵母·存储分页.md>)：LockInv焊水箱；TagDiff抢槽；单帧改道归仓；Postfix换落页
- [机器改造与流体操作四例](<workshop-distill/机器改造与流体操作四例.md>)：机器加速Prefix暂存+Finalizer还原；流体先min(Free,Vol)双端设
- [机器自动化三例-自学习增强·全自动制药·养鼠工厂](<workshop-distill/机器自动化三例-自学习增强·全自动制药·养鼠工厂.md>)：Projector壳零补丁;假Item重算原工艺;规则存tag决策纯函数
- [液体系统三例-扫描仪·水商·水龙头](<workshop-distill/液体系统三例-扫描仪·水商·水龙头.md>)：改原UI记账还原;TMP用link画条;分段闸门控次数不改公式+三段归还
- ⚠️[MaiLeJi](<workshop-distill/3828927781-MaiLeJi.md>)：向atlasCache写合成图集可零补丁绕开SpriteDict ｜ **⚠️禁**：禁799处空catch吞错；禁WriteSlotFile摘活对象防异常丢数据；缺UnpatchAll禁热更
- ⚠️[MoreDeviceUpgrades](<workshop-distill/3831439549-MoreDeviceUpgrades.md>)：永久加成存私有tag+ComputeModuleEffect重算不丢不叠 ｜ **⚠️禁**：禁抄181空catch、25跳过Prefix、TryAcceptOnce=0拖拽hack及lambda补丁

## 三、物品、容器、整理排序与编辑（20）

- [ItemSpawner](<workshop-distill/3829012791-ItemSpawner.md>)：只读遍历目录探测用完必Destroy；Lib/App双层解耦
- [SmartInventoryArrange](<workshop-distill/3829206666-SmartInventoryArrange.md>)：排序只读可见态禁碰realCondition；原地重排失败整体回滚
- [DeepSpaceIceCity](<workshop-distill/3829298269-DeepSpaceIceCity.md>)：原版倒水不搬自造溶质，1.5s内体积增减配对按比例搬元数据
- [PensPipette](<workshop-distill/3829409725-PensPipette.md>)：物品委托不存盘，在OnCreateTooltipLate引用对比可自愈
- [PCExpansion](<workshop-distill/3829413447-PCExpansion.md>)：隔离itemHeap借EncodeNodes可无损序列化离线物品
- [FishingCuisine](<workshop-distill/3830031787-FishingCuisine.md>)：先判电量→再判成品可造→再进事务扣料；双ModHook延迟激活失败双清零
- [VanillaFoodExpansion](<workshop-distill/3830798369-VanillaFoodExpansion.md>)：CD写StorePhoneClient原生字段；1:1轮换不挤没原版货
- [VanillaGunExpansion](<workshop-distill/3834135415-VanillaGunExpansion.md>)：WriteSlotFile Prefix落盘瞬间刷内存态入ModData
- [Gunsmith](<workshop-distill/3834619049-Gunsmith.md>)：只做PRINTER_CHIP_TAG卡带即可打印，零新UI与清单
- [SmartArrangement](<workshop-distill/3834649530-SmartArrangement.md>)：模块舱位图退火求解+多目标预览+事务装入支持reserveRoom
- [ShadowverseCardShop](<workshop-distill/3843132012-ShadowverseCardShop.md>)：uGUI补间零依赖开包；溢价挂ItemFeature不改unitValue
- [StorageControlTerminal](<workshop-distill/3845671416-StorageControlTerminal.md>)：0 Patch机器：MACHINE+onActivateSlotItemFunc开CustomUI聚合仓
- [QiluoItemEditor](<workshop-distill/3845680734-QiluoItemEditor.md>)：GameItem运行期可安全改字段 vs 受保护内部字段全量查表
- [小玩法四例-结婚·开箱·刮刮乐·堆叠卡](<workshop-distill/小玩法四例-结婚·开箱·刮刮乐·堆叠卡.md>)：插队回读;解锁后置防重摇;手势调RevealAll;无态单拆有态用队列
- [柜台与容器三例-扩容·挂钩·鼠鼠](<workshop-distill/柜台与容器三例-扩容·挂钩·鼠鼠.md>)：扩柜平移minY;借调走SaveGame收口;换皮守占格填alpha=1
- [超小功能三例之二-鉴定单·耐久显示·招牌](<workshop-distill/超小功能三例之二-鉴定单·耐久显示·招牌.md>)：无回调靠HashSet差分;磨损换Sprite不叠层;改展示换输入不抄公式
- [超小功能四例-开局特性·保箱子·名额·对话框](<workshop-distill/超小功能四例-开局特性·保箱子·名额·对话框.md>)：延迟上货须补空台欺骗;真值/显示分开不改cost;绝对YUI全赋值点幂等挂
- ⚠️[ContainerLink](<workshop-distill/3844123767-ContainerLink.md>)：容器链接拓扑存版本化Tag+事务搬运，不造新容器类 ｜ **⚠️禁**：严禁抄其大量空 catch {} 静默吞异常
- ⚠️[DigitalVault](<workshop-distill/3845554726-DigitalVault.md>)：多件物品编码存容器Tag做数字仓，零新增ES3顶层键 ｜ **⚠️禁**：严禁抄其 96 处空 catch {} 静默吞异常
- ⚠️[SpriteHeal](<workshop-distill/3845751834-SpriteHeal.md>)：切回原版图集必清useSpriteFromMod，否则ResolveSpriteByName变问号 ｜ **⚠️禁**：不要抄其内部空 catch {}

## 四、事件、经济、交易与跨日玩法（12）

- ★[StationEvents](<workshop-distill/3833966506-StationEvents.md>)：自持蓝图+ActivateEvent+negociationDatas零价格补丁；ES3浅克隆剥离
- ★[事件与内容扩展四例](<workshop-distill/事件与内容扩展四例.md>)：事件/掉落优先走StoreEventBlueprint/LootRegistry/OnLootRolled官口
- ★[报纸配图与机器动画两标杆](<workshop-distill/报纸配图与机器动画两标杆.md>)：VMA用OwnedFrames让位换图；我方必写HarmonyAfter(BN)
- [XiaowoTradePerks](<workshop-distill/3828949943-XiaowoTradePerks.md>)：IsPerkActive是O(n)线性扫表，热路径必按读档代次缓存
- [FactionReputation](<workshop-distill/3828971731-FactionReputation.md>)：声望增量对齐用Exact+(原生-Projection)，防跨模组覆盖
- [KitchenBusiness](<workshop-distill/3830523891-KitchenBusiness.md>)：按卷帘门插windowUICanvas层序；批量每帧2件+3ms熔断
- [VisitorCalendar](<workshop-distill/3832181449-VisitorCalendar.md>)：倒计时在OPEN须减1；伪StoreEvent必设IsKnown=true
- [FenceAndCaseExpansion](<workshop-distill/3837619429-FenceAndCaseExpansion.md>)：预算剪枝剔超价候选再归一权重，ModData存跨箱pity保底
- [HenchmanReputation](<workshop-distill/3837976033-HenchmanReputation.md>)：StoreRequest.ActivateRequest寄生市场电脑，零UI
- [交易系统四例-电话商店·交易预览·天赋商店·库存重摇](<workshop-distill/交易系统四例-电话商店·交易预览·天赋商店·库存重摇.md>)：装箱代列表；镜像+事后对账；OnItemSold累额；摘货重跑回滚
- [委托系统三例-委托扩展·化学品委托·佣兵派遣](<workshop-distill/委托系统三例-委托扩展·化学品委托·佣兵派遣.md>)：三刀扩板不造单；验收先记账后销毁；EncodeItem+过夜钩子零补丁派遣
- [金融与公司两个经济系统](<workshop-distill/金融与公司两个经济系统.md>)：复杂经济走Exchange影子账本+日种子回放K线，零改GetCurrentValue

## 五、鉴定、顾客、委托与夜出（13）

- [UpperDistrictHeist](<workshop-distill/3828902221-UpperDistrictHeist.md>)：取消出发必调OnArriveStore(true)，false扣一次外出
- [AppraisalAssistant](<workshop-distill/3828921912-AppraisalAssistant.md>)：PriceSnapshot克隆估价；自注册鉴定读modifiedState.dict避拒识
- [GoFishing](<workshop-distill/3829110153-GoFishing.md>)：modData反序列化失败即拉黑该键拒写入，宁停玩法不覆存档
- [AppraisalRules](<workshop-distill/3831174434-AppraisalRules.md>)：调用≠Patch免触鉴定助手红线；PointerSwallow吃事件防点穿
- [ScavengingExpansion](<workshop-distill/3832768562-ScavengingExpansion.md>)：LoadFromAtlas按自家atlas短路，避SpriteDict热点
- [TobaccoExpansion](<workshop-distill/3832840208-TobaccoExpansion.md>)：ThreadStatic配GetCurrentValueMod真估不改物
- [AIEmporium](<workshop-distill/3832941698-AIEmporium.md>)：WriteSlotFile摘电话簿并用Finalizer还原防脏档
- [CombatContracts](<workshop-distill/3834364038-CombatContracts.md>)：IsPerkUnlocked在拉闸期拦截原版处决perk，无痛改接自卫战
- [EleanorStory](<workshop-distill/3834588749-EleanorStory.md>)：读confrontSucessDiscount等原版特征判识货，零价格补丁
- [TradeCity](<workshop-distill/3841532994-TradeCity.md>)：AfterhourLocation.Register直注真子类，免反射0补丁
- [Sakurazaki角色模组](<workshop-distill/3844162371-Sakurazaki角色模组.md>)：夜出CG用StageObject；奖励落账回店验IN_STORE幂等发
- [VivianPureStory](<workshop-distill/3845140070-VivianPureStory.md>)：手解WAV取length控配音不打断；StoreClient字段代补丁
- [两类内容扩展-鉴定服务与零Patch养殖](<workshop-distill/两类内容扩展-鉴定服务与零Patch养殖.md>)：Extended Appraisals 20鉴定靶点；Hatch Ranching实证0 Patch养殖

## 六、UI、布局交互与本地化（11）

- [TraditionalChinese](<workshop-distill/3828909380-TraditionalChinese.md>)：建TMP_FontAsset抄原材质后必还原_MainTex，否则白块
- [ShowItemNames](<workshop-distill/3829415336-ShowItemNames.md>)：挂InstantiateText读GetCustomName避改名冲突
- [BlackMarketMaterials](<workshop-distill/3832460122-BlackMarketMaterials.md>)：物品贴纸须拦IBeginDrag+alphaHitTest防拖走与点穿
- [DeepSpaceClinic](<workshop-distill/3832819040-DeepSpaceClinic.md>)：小游戏纯C#零Unity确定性判定；2格STORAGE+过滤委托即成专槽
- [BetterCashier](<workshop-distill/3833978053-BetterCashier.md>)：折叠行只关Visuals与ignoreLayout不销毁，保留原版行句柄
- [UI布局四例-议价面板·储物缩放·货物翻页·交易显示](<workshop-distill/UI布局四例-议价面板·储物缩放·货物翻页·交易显示.md>)：移窗翻基准；缩窗补边界；扣溢出货翻页；议价行改right须缓存
- [UI深度扩展八例](<workshop-distill/UI深度扩展八例.md>)：Tooltip走OnCreateTooltipLate；改窗接管getter链；Update节流0.15s
- [信息显示四例-阵营·物品详情·鉴定角标·幸运特性](<workshop-distill/信息显示四例-阵营·物品详情·鉴定角标·幸运特性.md>)：隐原窗画卡；后置追阵营；Prefix挂角标；GetLuck总闸夹取
- [补充两例-调试面板与双人联机](<workshop-distill/补充两例-调试面板与双人联机.md>)：调试改getter不改字段；慎Patch全局InputActionManager高频冲突区
- [超小功能三例-声望弹字·框选·到账语音](<workshop-distill/超小功能三例-声望弹字·框选·到账语音.md>)：读终值用前后差分;判定位翻bool动作吞掉;语音自建源串行播
- ⚠️[3845556624+3845895085-文字层逆向](<workshop-distill/3845556624+3845895085-文字层逆向.md>)：禁Init缓存LocHelper.Get；CJK挂TMP_Settings.fallbackFontAssets ｜ **⚠️禁**：严禁抄 Locale Error Fix 吞掉原版 fallback 链导致缺词退化为裸 key

## 七、立绘、美术、贴图与音频（9）

- ★[美术与角色模组三级形态与立绘包规格](<workshop-distill/美术与角色模组三级形态与立绘包规格.md>)：立绘1728x1266/PPU600/Point；换脸改SpriteRenderer不污染SpriteDict
- [HogwartsFacilityReskin](<workshop-distill/3832140957-HogwartsFacilityReskin.md>)：31层ColorGrading非零才override；换皮须等像素
- [RimworldSmuggle](<workshop-distill/3836168312-RimworldSmuggle.md>)：Sprite.Create按原rect归一化pivot+沿用ppu防立绘偏移
- [StorageBayAppearance](<workshop-distill/3837983447-StorageBayAppearance.md>)：外观存state tag随档；OnDisable先重绘还原再Unpatch
- [ProbablyExtendFramework](<workshop-distill/3841909877-ProbablyExtendFramework.md>)：自描述Patch注册表Func<MethodBase>延迟解析+Report()秒排错
- [ExtendFrameworkFiles](<workshop-distill/3841912659-ExtendFrameworkFiles.md>)：主体保持零能力，磁盘直读素材剥入独立伴生模组
- [NightRegulars立绘框架](<workshop-distill/3845197390-NightRegulars立绘框架.md>)：立绘按需加载三层回退永不空白；框架与纯图包解耦
- [立绘核心库与两个小工具](<workshop-distill/立绘核心库与两个小工具.md>)：_pending幂等;零补丁搬场景;SwapItemsInPlace验守恒
- ⚠️[NeteaseMusic](<workshop-distill/3832184465-NeteaseMusic.md>)：MachineryPanelRow.GetPanel判重再Add零反射插钮 ｜ **⚠️禁**：禁6连Prefix短路毁磁带状态机与音量；禁空catch吞网错；正式包禁System.IO/联网/放.ps1

## 八、性能优化、缓存与存档读档（5）

- [LagInspector](<workshop-distill/3840533881-LagInspector.md>)：Stopwatch分阶段计时+GetPatchInfo.Owners归因(不缓存空结果)
- [ValueCache](<workshop-distill/3843892181-ValueCache.md>)：ConditionalWeakTable防泄漏+全局epoch++ O(1)失效
- [交互与设置四例-快速存档·直接替换·批量贴标·模组设置](<workshop-distill/交互与设置四例-快速存档·直接替换·批量贴标·模组设置.md>)：验写盘存；EndDrag换物；写fake贴标；Find筛开设置
- ⚠️[SleepLagOptimizer](<workshop-distill/3844377158-SleepLagOptimizer.md>)：过夜360次化学模拟；批处理标志须Prefix开+Finalizer关 ｜ **⚠️禁**：严禁抄其整段替换 RunNightlyChemicalSimulation（与所有同类优化模组互斥）
- ⚠️[ProgressBar](<workshop-distill/3846086230-ProgressBar.md>)：读档拆Decode/Link/Build分帧消卡顿；须保原版回退路径 ｜ **⚠️禁**：严禁在不具备完整出错回退的前提下整段替换 DecodeSaveItem

## 九、跨模组冲突、互操作与外部通道（7）

- [ProbablyMultiplayer](<workshop-distill/3828909239-ProbablyMultiplayer.md>)：联机传物品树片段对端重建；防EncodeItem残留savedItemFeatureList
- [ElectronicsCompatPatch](<workshop-distill/3832805089-ElectronicsCompatPatch.md>)：软探测滤IsEnabled与LoadError空；暴露能力位供补丁礼让
- [ModConflictScanner](<workshop-distill/3832823827-ModConflictScanner.md>)：工坊扫描器只查25固定IL靶；红冲突集中在物品名与字体层
- [WildNetAPI](<workshop-distill/3842773239-WildNetAPI.md>)：ModHelper.Expose/SendMessage零引用互操作+读档DFS环检测
- [AIStoreManager](<workshop-distill/3845754309-AIStoreManager.md>)：ModHelper.*ModDataFile*消费即删文件收件箱，零能力连外部AI
- [桥接器与开发框架两例](<workshop-distill/桥接器与开发框架两例.md>)：HTTP桥守IsLoopback+Host+Origin三重边界；_isHost区分宿主
- ⚠️[模组列表管理五例](<workshop-distill/模组列表管理五例.md>)：只读ModLoader.Mods/IsModEnabled/TrustState；禁碰modOrder/Trust ｜ **⚠️禁**：严禁调用 [ModRestricted(loader-target)]、严禁 ES3.Save(modOrder/modTrust)、严禁像 Mod Preloader 拼 char[] 绕过 ModScanner
