# 工坊蒸馏知识｜高密度压缩速查卡（DISTILL_DIGEST）

> **压缩源**：42 份工坊蒸馏档案（原始 16,154 行 / 涵盖 82 个独立工坊 ID）→ 无损提纯为一张紧凑速查卡。更新时间：`2026-11-28 01:03:51`。
> **用法**：写方案/排雷前先扫这张卡（每条 1 行直给结论 + ⚠️ 禁抄红线），需要原样 C# 行号证据时再点开对应档案。

## 一、大型 / DLC / 巨型架构与合集组织（7 份）

- `L3反编译` [3828893950-DangpuPlus](<workshop-distill/3828893950-DangpuPlus.md>)：常驻逻辑合集用 FeaturePack 静态委托表 + 异常熔断；渲染层只做进出上下文成对包裹，不整段替换绘制 ｜ **⚠️ 别抄**：不支持热摘除单个功能补丁；不要在简单模组里硬套 103 项框架
- `L3反编译` [3828902092-ElectronicsExpansion](<workshop-distill/3828902092-ElectronicsExpansion.md>)：200+实体先钉死 JSON Schema+加载期不变量证伪；同钩子多补丁用优先级分相(800准备/0常规/-50裁决)；ModuleLifecycle.Seal()禁重复序号；critical失败自卸保原版存档 ｜ **⚠️ 别抄**：严禁抄其 2.1 万行全量反射契约表、1.2 万行单文件 OnGUI、漏装主程序集与空 critical 集
- `L3反编译` [3829957165-PenQoL补丁表](<workshop-distill/3829957165-PenQoL补丁表.md>)：支持运行中热开关单功能且 N 开关合并 1 补丁；每处 AccessTools.Method 显式传 Type[] 参数表 ｜ **⚠️ 别抄**：每条补丁的目标解析+安装必须独立 try/catch（勿写循环外实参求值导致单条 AmbiguousMatch 拖垮全表）；Applied 标记须对照 Harmony.GetPatchInfo 真实状态
- `L3反编译` [3838944803-UpperLevels](<workshop-distill/3838944803-UpperLevels.md>)：不建 Unity 新场景，把整片上层区压成 Difficulty.Current.source 单点布尔开关，店面原地换皮+AddClient改客群，零入侵存档 ｜ **⚠️ 别抄**：267 Patch 仅靠首行早退有效，小模组切勿照搬大规模 Patch 面
- `L3反编译` [3841509159-ClaimsAndUprisings](<workshop-distill/3841509159-ClaimsAndUprisings.md>)：用 IDisposable 查询作用域令牌 + 单点模态仲裁器 + Fork/校验/原子提交三件基础设施，把 233 个 Patch 压成 30 行早退薄片 ｜ **⚠️ 别抄**：不要把 395 个文件平铺在单目录；不要靠 119 个分散早退门替代统一门面
- `L3反编译` [3845965318-ZohFramework](<workshop-distill/3845965318-ZohFramework.md>)：0 Patch 建剧本 DSL 引擎，文本外置方便非程序员编辑；坏脚本降级成普通顾客不崩档；安全注入闲聊用 allDoneDialogue + ConditionalWeakTable
- `L3反编译` [玩法模块三例-职业拍卖灾难](<workshop-distill/玩法模块三例-职业拍卖灾难.md>)：按玩法节奏选挂点：营业日用 OnShutterOpened/Closed、夜出用 AddAfterhourLocation+回店发奖、跨日连环用 StoreEvent+老档补前置纸条 ｜ **⚠️ 别抄**：勿抄单文件 6000 行巨石与 297 个分散 ModData 散键（应结构化收纳）

## 二、机器、生产链、自动化与流体（3 份）

- `L2源码核对` [生产链与自动化四例](<workshop-distill/生产链与自动化四例.md>)：新机器走 SetupBatterySlot+SetupModuleBay+SetupManualSlot+InitMachinery+STANDARD/TURBO 标签+断电退款；0 处自建加工 tick；存档改名走 SaveManager.DecodeNodes Prefix；小体量自动化靠工作区容器+FindAllChildrenType+EndNight Postfix
- `L3反编译` [3845199370-PipelineController](<workshop-distill/3845199370-PipelineController.md>)：靠 GameItemFunc 委托（target/activate/tick）替代大规模 Harmony Patch，管网图序列化在物品 Tag 上按拓扑序结算
- `L3反编译` [机器改造与流体操作四例](<workshop-distill/机器改造与流体操作四例.md>)：改机器加速用 Prefix 暂存+Postfix/Finalizer 还原（TransientTagBoost）；实时化只改触发时机不重写加工；液体按 part 分开存，定量搬运先取 min(Free,Vol) 再双端 SetChemContentUL

## 三、物品、容器、整理排序与编辑（8 份）

- `L3反编译` [3829012791-ItemSpawner](<workshop-distill/3829012791-ItemSpawner.md>)：遍历目录做只读探测后必须 Destroy；Lib/App 两层拆分便于复用物品查询与编辑核心
- `L3反编译` [3829206666-SmartInventoryArrange](<workshop-distill/3829206666-SmartInventoryArrange.md>)：自动整理严守 real-state-appraisal-ignored 只读玩家可见信息；modifiedShape 原地重排失败即整体回滚并协同接管撤销栈 ｜ **⚠️ 别抄**：整理/排序功能严禁读取 ItemFeature.realCondition（会直接剧透真假货）
- `L3反编译` [3834649530-SmartArrangement](<workshop-distill/3834649530-SmartArrangement.md>)：模块舱优化用位图棋盘做离线退火求解，给出多目标对比后事务式装入，支持 reserveRoom 预留指定空位
- `L3反编译` [3845671416-StorageControlTerminal](<workshop-distill/3845671416-StorageControlTerminal.md>)：0 Patch 也能造复杂机器：SetGameItemType(MACHINE) + onActivateSlotItemFunc 开 CustomUIManager.CreateWindow + 跨容器聚合索引
- `L3反编译` [3845680734-QiluoItemEditor](<workshop-distill/3845680734-QiluoItemEditor.md>)：完整盘点 GameItem 运行期可安全修改字段 vs 受保护内部字段，适合物品编辑/克隆/贴图修复查表
- `L4⚠️含禁抄` [3844123767-ContainerLink](<workshop-distill/3844123767-ContainerLink.md>)：不造新容器类型，把链接拓扑存在版本化 Tag 里并用事务式搬运同步，避免破坏原版容器序列化 ｜ **⚠️ 别抄**：严禁抄其大量空 catch {} 静默吞异常
- `L4⚠️含禁抄` [3845554726-DigitalVault](<workshop-distill/3845554726-DigitalVault.md>)：把多件物品压成编码串存进容器物品 Tag 实现数字仓储，零新增 ES3 顶层键 ｜ **⚠️ 别抄**：严禁抄其 96 处空 catch {} 静默吞异常
- `L4⚠️含禁抄` [3845751834-SpriteHeal](<workshop-distill/3845751834-SpriteHeal.md>)：从模组贴图切换回原版图集时必须同步清 useSpriteFromMod 标志，否则 ResolveSpriteByName 继续查模组表返回白底问号 ｜ **⚠️ 别抄**：不要抄其内部空 catch {}

## 四、事件、经济、拍卖与跨日玩法（4 份）

- `L2源码核对` [3833966506-StationEvents](<workshop-distill/3833966506-StationEvents.md>)：不写原版蓝图表而自持列表+ActivateEvent；涨跌价直接填 negociationDatas 零价格 Patch；ES3 浅克隆剥离+天数回放；展示层只换容器不重写逻辑 ｜ **⚠️ 别抄**：非海量事件勿抄其 2 处 HandleNewEvent* IL Transpiler（双改冲突点且未声明优先级）
- `L2源码核对` [事件与内容扩展四例](<workshop-distill/事件与内容扩展四例.md>)：事件与掉落表原版均有公开扩展口（StoreEventBlueprint/LootRegistry/OnLootRolled），优先走官方钩子不整段替换 ｜ **⚠️ 别抄**：勿抄 Scavenging Events 放弃官方口子去整段替换拾荒方法
- `L2源码核对` [报纸配图与机器动画两标杆](<workshop-distill/报纸配图与机器动画两标杆.md>)：VMA 用 OwnedFrames/HasForeignSprite 做让位式 Image.sprite 替换且缺图不占位；BN 自己不写 HarmonyAfter，我方必须写 HarmonyAfter(mod.local.newspaper.illustrations)
- `L3反编译` [金融与公司两个经济系统](<workshop-distill/金融与公司两个经济系统.md>)：复杂经济首选 Exchange 影子账本（零改 GetCurrentValue、只存日种子按需回放历史 K 线、ModHelper.AddCredit 闭环） ｜ **⚠️ 别抄**：慎抄 CorpPresence 重度改写存档链

## 五、鉴定、放大镜、顾客与夜出（3 份）

- `L3反编译` [3828921912-AppraisalAssistant](<workshop-distill/3828921912-AppraisalAssistant.md>)：不暴露外部服务，靠 PriceSnapshot 克隆执行 getCorrectValue；自家注册鉴定品类改读 modifiedState.dict 可绕开其 IsTag 解释器拒识 ｜ **⚠️ 别抄**：严禁对其 5 个红线方法（GameItem.IsTag、AppraisalHelper.GetCorrectBaseValue、AppHand/Ribwich/PlasmaHelper.GetCorrectValue、AppraisalItemType.Matches、AppraisalItemRegistry.Find）做破坏性 Prefix
- `L3反编译` [3844162371-Sakurazaki角色模组](<workshop-distill/3844162371-Sakurazaki角色模组.md>)：夜出全屏 CG 舞台用 StageObject+Aspect Fill/Fit；奖励不在 Leave() 塞库而是落账回店校验 IN_STORE 后幂等发；插原版 NPC 选项沿 nextDialogue 找首个 choices>=2 节点
- `L3反编译` [两类内容扩展-鉴定服务与零Patch养殖](<workshop-distill/两类内容扩展-鉴定服务与零Patch养殖.md>)：Extended Appraisals 绘出鉴定服务 20 个接入靶点；Hatch Ranching 实证 0 Harmony Patch 仅靠 ModHelper+GameItemFunc 可做完整养殖系统 ｜ **⚠️ 别抄**：同一靶点（如 OnEstimate）挂多个补丁时必须显式声明 HarmonyPriority

## 六、UI、字体本地化与交互隔离（3 份）

- `L3反编译` [UI深度扩展八例](<workshop-distill/UI深度扩展八例.md>)：Tooltip 优先 ModHook.OnCreateTooltipLate；改窗口尺寸必须接管 getter 链并声明优先级；Update 轮询节流 0.12~0.25s；屏蔽输入必须配 Clear() 兜底防永久失输入
- `L3反编译` [补充两例-调试面板与双人联机](<workshop-distill/补充两例-调试面板与双人联机.md>)：调试注入优先改 getter 不改原版字段；双人联机另辟 TCP 镜像视图路线；慎 Patch 全局 InputActionManager（多模组高频冲突区）
- `L4⚠️含禁抄` [3845556624+3845895085-文字层逆向](<workshop-distill/3845556624+3845895085-文字层逆向.md>)：不要在 Init/OnEnable 缓存 LocHelper.Get（表未就绪会抛 invalid operation handle）；CJK 显示靠挂 TMP_Settings.fallbackFontAssets + ForceMeshUpdate ｜ **⚠️ 别抄**：严禁抄 Locale Error Fix 吞掉原版 fallback 链导致缺词退化为裸 key

## 七、立绘、美术规格与贴图框架（4 份）

- `L2源码核对` [美术与角色模组三级形态与立绘包规格](<workshop-distill/美术与角色模组三级形态与立绘包规格.md>)：立绘硬规格 1728×1266/PPU 600/pivot(0.5,0.4976)/Point/无mipmap；换脸只换 SpriteRenderer.sprite 且条件还原，不改 SpriteDict；纯图包可用空壳 DLL + provider JSON
- `L3反编译` [3841909877-ProbablyExtendFramework](<workshop-distill/3841909877-ProbablyExtendFramework.md>)：自描述 Patch 注册表用 Func<MethodBase> 延迟解析+逐条 catch+Report() 秒定位失效补丁；JSON 声明式接贴图，靠伴生模组把文件权限剥离出主体
- `L3反编译` [3841912659-ExtendFrameworkFiles](<workshop-distill/3841912659-ExtendFrameworkFiles.md>)：主体保持零能力，把必须直读硬盘的外部素材目录剥进独立伴生模组，支持丢图即生效
- `L3反编译` [3845197390-NightRegulars立绘框架](<workshop-distill/3845197390-NightRegulars立绘框架.md>)：立绘按需加载并设三层回退链确保永不空白；程序框架与独立美术包解耦

## 八、性能优化、缓存与读档管线（4 份）

- `L3反编译` [3840533881-LagInspector](<workshop-distill/3840533881-LagInspector.md>)：性能定位用 Stopwatch 分阶段计时 + Harmony.GetPatchInfo(m).Owners 精确归因到具体模组；归因缓存不缓存空结果以看见晚加载补丁
- `L3反编译` [3843892181-ValueCache](<workshop-distill/3843892181-ValueCache.md>)：缓存用 ConditionalWeakTable 防 GameItem 内存泄漏，全局 epoch++ 实现 O(1) 全量失效，键必须包含事件/加价/纯度/日期 ｜ **⚠️ 别抄**：不要抄其 44 个无 HarmonyPriority 的密集 Patch，必须显式声明优先级
- `L4⚠️含禁抄` [3844377158-SleepLagOptimizer](<workshop-distill/3844377158-SleepLagOptimizer.md>)：原版过夜每晚跑 360 次化学模拟；批处理延迟校验必须用 Prefix 开标志 + Finalizer 关标志防异常路径泄漏 ｜ **⚠️ 别抄**：严禁抄其整段替换 RunNightlyChemicalSimulation（与所有同类优化模组互斥）
- `L4⚠️含禁抄` [3846086230-ProgressBar](<workshop-distill/3846086230-ProgressBar.md>)：读档管线拆成 Decode/Link/Build 分帧执行可消卡顿，容器编号 Link 对齐最易出错；出错必须能回退原版读档 ｜ **⚠️ 别抄**：严禁在不具备完整出错回退的前提下整段替换 DecodeSaveItem

## 九、跨模组互操作、冲突、联机与外部通道（6 份）

- `L3反编译` [3828909239-ProbablyMultiplayer](<workshop-distill/3828909239-ProbablyMultiplayer.md>)：联机绝不直接写对方 PlayerStore，只传物品树存档片段并在对端重建；注意 EncodeItem 往 savedItemFeatureList 塞残留的坑
- `L3反编译` [3832823827-ModConflictScanner](<workshop-distill/3832823827-ModConflictScanner.md>)：工坊检测器只查 Declared/Assembly/25 条固定 IL 靶表，不查 identifier/语言 key/atlas 重复；红色硬冲突集中在全局物品名与本地化字体层
- `L3反编译` [3842773239-WildNetAPI](<workshop-distill/3842773239-WildNetAPI.md>)：不引用对方 DLL，用原版 ModHelper.Expose/SendMessage 或约定 GameObject+带版本号字典互操作；读档后跑 DFS 三色环检测与缺失前置自检
- `L3反编译` [3845754309-AIStoreManager](<workshop-distill/3845754309-AIStoreManager.md>)：不开 HTTP 端口也能连外部脚本/AI：用 ModHelper.*ModDataFile* 做消费即删+代号命名+坏命令丢弃的文件收件箱（实测零能力）
- `L3反编译` [桥接器与开发框架两例](<workshop-distill/桥接器与开发框架两例.md>)：内嵌 HTTP 桥必须守死 IsLoopback + Host==127.0.0.1 + Origin==null 三重边界；轻量库用 _isHost 区分宿主与被引用态 ｜ **⚠️ 别抄**：严禁抄 MCP Bridge 零鉴权 + CORS *（本机任意网页可驱动游戏）
- `L4⚠️含禁抄` [模组列表管理五例](<workshop-distill/模组列表管理五例.md>)：模组启用态存在 SaveFile.es3 的 modOrder/modTrust（带 HMAC）；我们只读 ModLoader.Mods/IsModEnabled/TrustState ｜ **⚠️ 别抄**：严禁调用 [ModRestricted(loader-target)]、严禁 ES3.Save(modOrder/modTrust)、严禁像 Mod Preloader 拼 char[] 绕过 ModScanner
