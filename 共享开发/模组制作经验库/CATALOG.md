# 深空当铺工作区｜压缩版打标总索引（CATALOG）

> **自动生成**（`python 工具库\kb-tools\kb_pipeline.py build`）｜更新时间：`2026-11-28 01:03:51`
> **零侵入并发设计**：不改 `workshop-distill/*.md` 正文；新批次落盘后重跑 `build` 即可刷新本索引与 [蒸馏压缩速查卡](<DISTILL_DIGEST.md>)。
> **秒级检索**：`python 工具库\kb-tools\kb_pipeline.py query --domain <域> [--trust L1,L2] [--api <原版类>] [--keyword <词>]`

- **总计**：**98** 项（规范 `23` ｜ 蒸馏档案 `42` ｜ 零能力 C# 模板 `13` ｜ 工具 `17` ｜ 技能 `3`），覆盖 **82** 个独立工坊 ID。
- **可信度**：`L1实测`（编译/ModScanner/真机验过）＞ `L2源码核对`（核对 `Assembly-CSharp`）＞ `L3反编译` ＞ `L4⚠️含禁抄`（仅学原理，严禁照抄）。

## 1. 按系统域一行路由（零重复）

| 域 | 适用场景 | 核心规范 / 模板 / 关键工具与代表蒸馏 |
| --- | --- | --- |
| `items` | 物品目录、发放、占格、容器精确投放、标签与品类 | [开发流程与经验](<../开发流程与经验.md>) · [07-踩坑台账](<07-踩坑台账.md>) · [GLOSSARY](<../GLOSSARY.md>) · [03-原版接口速查](<03-原版接口速查.md>) · [09-模组架构与物品调用规范](<09-模组架构与物品调用规范.md>) · [20-各类模组制作规范](<20-各类模组制作规范.md>) · [ReadyChain.cs](<../../工具库/templates/mod-patterns/ReadyChain.cs>) · [SafeSpriteHelper.cs](<../../工具库/templates/mod-patterns/SafeSpriteHelper.cs>) · [事件与内容扩展四例](<workshop-distill/事件与内容扩展四例.md>) · [生产链与自动化四例](<workshop-distill/生产链与自动化四例.md>) · [3828902092-ElectronicsExpansion](<workshop-distill/3828902092-ElectronicsExpansion.md>) · [3829012791-ItemSpawner](<workshop-distill/3829012791-ItemSpawner.md>) … |
| `machines` | 机器逻辑、过夜周期、流体/化学品、机器动画 | [07-踩坑台账](<07-踩坑台账.md>) · [03-原版接口速查](<03-原版接口速查.md>) · [15-机器运行动画与雪碧图规范](<15-机器运行动画与雪碧图规范.md>) · [MachineManualSlot.cs](<../../工具库/templates/mod-patterns/MachineManualSlot.cs>) · [SpriteSheetMachineAnim.cs](<../../工具库/templates/mod-patterns/SpriteSheetMachineAnim.cs>) · [TransientTagBoost.cs](<../../工具库/templates/mod-patterns/TransientTagBoost.cs>) · [pack-spritesheet](<../../工具库/img/pack-spritesheet.py>) · [报纸配图与机器动画两标杆](<workshop-distill/报纸配图与机器动画两标杆.md>) · [生产链与自动化四例](<workshop-distill/生产链与自动化四例.md>) · [3828902092-ElectronicsExpansion](<workshop-distill/3828902092-ElectronicsExpansion.md>) · [3834649530-SmartArrangement](<workshop-distill/3834649530-SmartArrangement.md>) · [3845199370-PipelineController](<workshop-distill/3845199370-PipelineController.md>) … |
| `appraisal` | 鉴定品类、鉴定等级、放大镜双视图、真伪/成色判定、交互纸张 | [06-玩法与文案规范](<06-玩法与文案规范.md>) · [GLOSSARY](<../GLOSSARY.md>) · [03-原版接口速查](<03-原版接口速查.md>) · [13-鉴定扩展与交互纸张规范](<13-鉴定扩展与交互纸张规范.md>) · [CustomInspectLens.cs](<../../工具库/templates/mod-patterns/CustomInspectLens.cs>) · [3828921912-AppraisalAssistant](<workshop-distill/3828921912-AppraisalAssistant.md>) · [3845680734-QiluoItemEditor](<workshop-distill/3845680734-QiluoItemEditor.md>) · [UI深度扩展八例](<workshop-distill/UI深度扩展八例.md>) · [两类内容扩展-鉴定服务与零Patch养殖](<workshop-distill/两类内容扩展-鉴定服务与零Patch养殖.md>) |
| `events` | 空间站事件、新闻报纸、日历预告、Better Newspapers 兼容 | [07-踩坑台账](<07-踩坑台账.md>) · [11-事件系统接入规范](<11-事件系统接入规范.md>) · [16-报纸事件配图与BetterNewspapers规范](<16-报纸事件配图与BetterNewspapers规范.md>) · [20-各类模组制作规范](<20-各类模组制作规范.md>) · [Es3SafePersistence.cs](<../../工具库/templates/mod-patterns/Es3SafePersistence.cs>) · [NewspaperArtHelper.cs](<../../工具库/templates/mod-patterns/NewspaperArtHelper.cs>) · [newsprint-tone](<../../工具库/img/newsprint-tone.py>) · [3833966506-StationEvents](<workshop-distill/3833966506-StationEvents.md>) · [事件与内容扩展四例](<workshop-distill/事件与内容扩展四例.md>) · [报纸配图与机器动画两标杆](<workshop-distill/报纸配图与机器动画两标杆.md>) · [3845965318-ZohFramework](<workshop-distill/3845965318-ZohFramework.md>) · [玩法模块三例-职业拍卖灾难](<workshop-distill/玩法模块三例-职业拍卖灾难.md>) |
| `afterhour` | 打烊后夜晚外出地点、商店摊位、CG舞台、夜晚地图事件 | [07-踩坑台账](<07-踩坑台账.md>) · [18-夜晚外出区域与探索扩展规范](<18-夜晚外出区域与探索扩展规范.md>) · [3838944803-UpperLevels](<workshop-distill/3838944803-UpperLevels.md>) · [3842773239-WildNetAPI](<workshop-distill/3842773239-WildNetAPI.md>) · [3844162371-Sakurazaki角色模组](<workshop-distill/3844162371-Sakurazaki角色模组.md>) · [玩法模块三例-职业拍卖灾难](<workshop-distill/玩法模块三例-职业拍卖灾难.md>) |
| `customers` | 顾客生成、对话系统、声望、拒收/识破、电话簿 | [03-原版接口速查](<03-原版接口速查.md>) · [08-立绘接入与PortraitStudio](<08-立绘接入与PortraitStudio.md>) · [11-事件系统接入规范](<11-事件系统接入规范.md>) · [18-夜晚外出区域与探索扩展规范](<18-夜晚外出区域与探索扩展规范.md>) · [3833966506-StationEvents](<workshop-distill/3833966506-StationEvents.md>) · [事件与内容扩展四例](<workshop-distill/事件与内容扩展四例.md>) · [美术与角色模组三级形态与立绘包规格](<workshop-distill/美术与角色模组三级形态与立绘包规格.md>) · [3828909239-ProbablyMultiplayer](<workshop-distill/3828909239-ProbablyMultiplayer.md>) · [3838944803-UpperLevels](<workshop-distill/3838944803-UpperLevels.md>) · [3844162371-Sakurazaki角色模组](<workshop-distill/3844162371-Sakurazaki角色模组.md>) · [3845197390-NightRegulars立绘框架](<workshop-distill/3845197390-NightRegulars立绘框架.md>) · [3845965318-ZohFramework](<workshop-distill/3845965318-ZohFramework.md>) … |
| `ui` | CustomUI面板、Tooltip、热键、模态输入隔离、像素窗口、字体本地化 | [07-踩坑台账](<07-踩坑台账.md>) · [03-原版接口速查](<03-原版接口速查.md>) · [13-鉴定扩展与交互纸张规范](<13-鉴定扩展与交互纸张规范.md>) · [14-模组高级编程架构与工程模式](<14-模组高级编程架构与工程模式.md>) · [20-各类模组制作规范](<20-各类模组制作规范.md>) · [CustomInspectLens.cs](<../../工具库/templates/mod-patterns/CustomInspectLens.cs>) · [MachineManualSlot.cs](<../../工具库/templates/mod-patterns/MachineManualSlot.cs>) · [ModalInputGate.cs](<../../工具库/templates/mod-patterns/ModalInputGate.cs>) · [3833966506-StationEvents](<workshop-distill/3833966506-StationEvents.md>) · [3828893950-DangpuPlus](<workshop-distill/3828893950-DangpuPlus.md>) · [3828902092-ElectronicsExpansion](<workshop-distill/3828902092-ElectronicsExpansion.md>) · [3829012791-ItemSpawner](<workshop-distill/3829012791-ItemSpawner.md>) … |
| `economy` | 经济模型、影子账本、价格修正、拍卖、金融/公司系统 | [EpochCache.cs](<../../工具库/templates/mod-patterns/EpochCache.cs>) · [3843892181-ValueCache](<workshop-distill/3843892181-ValueCache.md>) · [玩法模块三例-职业拍卖灾难](<workshop-distill/玩法模块三例-职业拍卖灾难.md>) · [金融与公司两个经济系统](<workshop-distill/金融与公司两个经济系统.md>) |
| `persistence` | 存档读写、ES3防坏档投影、ModData私有数据、读档管线 | [03-原版接口速查](<03-原版接口速查.md>) · [11-事件系统接入规范](<11-事件系统接入规范.md>) · [14-模组高级编程架构与工程模式](<14-模组高级编程架构与工程模式.md>) · [20-各类模组制作规范](<20-各类模组制作规范.md>) · [Es3SafePersistence.cs](<../../工具库/templates/mod-patterns/Es3SafePersistence.cs>) · [ModDataInbox.cs](<../../工具库/templates/mod-patterns/ModDataInbox.cs>) · [3833966506-StationEvents](<workshop-distill/3833966506-StationEvents.md>) · [生产链与自动化四例](<workshop-distill/生产链与自动化四例.md>) · [3828909239-ProbablyMultiplayer](<workshop-distill/3828909239-ProbablyMultiplayer.md>) · [3841509159-ClaimsAndUprisings](<workshop-distill/3841509159-ClaimsAndUprisings.md>) · [3845754309-AIStoreManager](<workshop-distill/3845754309-AIStoreManager.md>) · [金融与公司两个经济系统](<workshop-distill/金融与公司两个经济系统.md>) … |
| `media` | 立绘规格、PortraitStudio、高清相机租约、贴图去边、WAV配音 | [06-玩法与文案规范](<06-玩法与文案规范.md>) · [08-立绘接入与PortraitStudio](<08-立绘接入与PortraitStudio.md>) · [12-立绘引擎与音画资源架构](<12-立绘引擎与音画资源架构.md>) · [15-机器运行动画与雪碧图规范](<15-机器运行动画与雪碧图规范.md>) · [16-报纸事件配图与BetterNewspapers规范](<16-报纸事件配图与BetterNewspapers规范.md>) · [18-夜晚外出区域与探索扩展规范](<18-夜晚外出区域与探索扩展规范.md>) · [20-各类模组制作规范](<20-各类模组制作规范.md>) · [HdCameraLease.cs](<../../工具库/templates/mod-patterns/HdCameraLease.cs>) · [NewspaperArtHelper.cs](<../../工具库/templates/mod-patterns/NewspaperArtHelper.cs>) · [SafeSpriteHelper.cs](<../../工具库/templates/mod-patterns/SafeSpriteHelper.cs>) · [SpriteSheetMachineAnim.cs](<../../工具库/templates/mod-patterns/SpriteSheetMachineAnim.cs>) · [WavPcmDecoder.cs](<../../工具库/templates/mod-patterns/WavPcmDecoder.cs>) … |
| `perf` | 性能优化、代际缓存、弱表、分帧预算、过夜卡顿排查 | [14-模组高级编程架构与工程模式](<14-模组高级编程架构与工程模式.md>) · [20-各类模组制作规范](<20-各类模组制作规范.md>) · [EpochCache.cs](<../../工具库/templates/mod-patterns/EpochCache.cs>) · [3828902092-ElectronicsExpansion](<workshop-distill/3828902092-ElectronicsExpansion.md>) · [3829206666-SmartInventoryArrange](<workshop-distill/3829206666-SmartInventoryArrange.md>) · [3834649530-SmartArrangement](<workshop-distill/3834649530-SmartArrangement.md>) · [3840533881-LagInspector](<workshop-distill/3840533881-LagInspector.md>) · [3843892181-ValueCache](<workshop-distill/3843892181-ValueCache.md>) · ⚠️[3844377158-SleepLagOptimizer](<workshop-distill/3844377158-SleepLagOptimizer.md>) · ⚠️[3846086230-ProgressBar](<workshop-distill/3846086230-ProgressBar.md>) |
| `interop` | 跨模组通信、Harmony优先级、静态/运行期冲突扫描、加载器安全红线 | [开发流程与经验](<../开发流程与经验.md>) · [02-加载器与权限](<02-加载器与权限.md>) · [07-踩坑台账](<07-踩坑台账.md>) · [10-开发桥接器能力速查](<10-开发桥接器能力速查.md>) · [03-原版接口速查](<03-原版接口速查.md>) · [08-立绘接入与PortraitStudio](<08-立绘接入与PortraitStudio.md>) · [09-模组架构与物品调用规范](<09-模组架构与物品调用规范.md>) · [12-立绘引擎与音画资源架构](<12-立绘引擎与音画资源架构.md>) · [14-模组高级编程架构与工程模式](<14-模组高级编程架构与工程模式.md>) · [16-报纸事件配图与BetterNewspapers规范](<16-报纸事件配图与BetterNewspapers规范.md>) · [20-各类模组制作规范](<20-各类模组制作规范.md>) · [HdCameraLease.cs](<../../工具库/templates/mod-patterns/HdCameraLease.cs>) … |
| `multiplayer` | 多人联机架构与外部进程通信通道 | [ModDataInbox.cs](<../../工具库/templates/mod-patterns/ModDataInbox.cs>) · [3828909239-ProbablyMultiplayer](<workshop-distill/3828909239-ProbablyMultiplayer.md>) · [3845754309-AIStoreManager](<workshop-distill/3845754309-AIStoreManager.md>) · [补充两例-调试面板与双人联机](<workshop-distill/补充两例-调试面板与双人联机.md>) |
| `architecture` | 大型/DLC级模组架构、单点总闸、多功合集组织、脚本DSL | [19-工坊模组逆向与蒸馏](<19-工坊模组逆向与蒸馏.md>) · [09-模组架构与物品调用规范](<09-模组架构与物品调用规范.md>) · [14-模组高级编程架构与工程模式](<14-模组高级编程架构与工程模式.md>) · [20-各类模组制作规范](<20-各类模组制作规范.md>) · [ReadyChain.cs](<../../工具库/templates/mod-patterns/ReadyChain.cs>) · [kb_pipeline](<../../工具库/kb-tools/kb_pipeline.py>) · [生产链与自动化四例](<workshop-distill/生产链与自动化四例.md>) · [3828893950-DangpuPlus](<workshop-distill/3828893950-DangpuPlus.md>) · [3828902092-ElectronicsExpansion](<workshop-distill/3828902092-ElectronicsExpansion.md>) · [3829012791-ItemSpawner](<workshop-distill/3829012791-ItemSpawner.md>) · [3829206666-SmartInventoryArrange](<workshop-distill/3829206666-SmartInventoryArrange.md>) · [3829957165-PenQoL补丁表](<workshop-distill/3829957165-PenQoL补丁表.md>) … |
| `workflow` | 环境构建、ModScanner零能力扫描、离线回归、真机桥接探针、分支备份发布 | [分支与备份规范](<../分支与备份规范.md>) · [开发流程与经验](<../开发流程与经验.md>) · [01-环境与工具链](<01-环境与工具链.md>) · [02-加载器与权限](<02-加载器与权限.md>) · [04-真机测试与探针](<04-真机测试与探针.md>) · [05-测试与回归](<05-测试与回归.md>) · [06-玩法与文案规范](<06-玩法与文案规范.md>) · [07-踩坑台账](<07-踩坑台账.md>) · [10-开发桥接器能力速查](<10-开发桥接器能力速查.md>) · [17-工坊发布与SteamUGC上传](<17-工坊发布与SteamUGC上传.md>) · [19-工坊模组逆向与蒸馏](<19-工坊模组逆向与蒸馏.md>) · [GLOSSARY](<../GLOSSARY.md>) … |

## 2. 全量资产唯一总表（每项只写 1 次）

### 核心经验规范与工程手册（23 项）

| 可信度 | 能力 | 域 | 文件 | 一句话用途 / ⚠️ 别抄 |
| --- | --- | --- | --- | --- |
| `L1实测` | `n/a` | `workflow` | [分支与备份规范.md](<../分支与备份规范.md>) | 开工建分支、五道合并门禁、四路多盘备份、恢复演练与回滚纪律 |
| `L1实测` | `zero-cap` | `workflow,items,interop` | [开发流程与经验.md](<../开发流程与经验.md>) | 六条模组编程硬规范（拒套娃/异常暴露/Harmony重载/物品铁律/零能力/真测试） |
| `L1实测` | `n/a` | `workflow` | [01-环境与工具链.md](<01-环境与工具链.md>) | 查本机目录分工、构建/回归命令、PS5.1 编码规则 |
| `L1实测` | `zero-cap` | `interop,workflow` | [02-加载器与权限.md](<02-加载器与权限.md>) | 查 ModScanner IL 扫描规则、零能力 <Capabilities/> 与哈希核验 |
| `L1实测` | `n/a` | `workflow` | [04-真机测试与探针.md](<04-真机测试与探针.md>) | Slot 1 真机测试流程、防碰 Slot 0、桥接与键鼠拖拽实操 |
| `L1实测` | `n/a` | `workflow` | [05-测试与回归.md](<05-测试与回归.md>) | 跑 Run-Regression.ps1 与写防假绿的真实行为断言 |
| `L1实测` | `n/a` | `appraisal,media,workflow` | [06-玩法与文案规范.md](<06-玩法与文案规范.md>) | 西方深空科幻文案红线、realCondition 判定铁律、立绘绿边消除 |
| `L1实测` | `n/a` | `items,machines,ui` | [07-踩坑台账.md](<07-踩坑台账.md>) | 遇到异常症状（紫问号、穿模、假绿、热重载失效、桥接报错）第一时间按现象反查 |
| `L1实测` | `cap-network` | `workflow,interop` | [10-开发桥接器能力速查.md](<10-开发桥接器能力速查.md>) | 查 ModDevBridgeNext MCP 工具契约、热重载与 C# 探针编写 |
| `L1实测` | `n/a` | `workflow` | [17-工坊发布与SteamUGC上传.md](<17-工坊发布与SteamUGC上传.md>) | 独立进程 SteamUGC 上传、封面规格、白名单洁净度与发布核对 |
| `L1实测` | `n/a` | `workflow,architecture` | [19-工坊模组逆向与蒸馏.md](<19-工坊模组逆向与蒸馏.md>) | 工坊抓取/筛选/反编译/精读六问与各批次蒸馏总账 |
| `L2源码核对` | `n/a` | `appraisal,items,workflow` | [GLOSSARY.md](<../GLOSSARY.md>) | 全工作区唯一术语表（成色/真伪/品类/标签/零能力/Slot 0与1 唯一叫法） |
| `L2源码核对` | `zero-cap` | `items,machines,appraisal` | [03-原版接口速查.md](<03-原版接口速查.md>) | 写代码前查原版 ModHelper(145)/ModHook(44)/GameItemFunc(51) 及物品/机器/顾客/液体权威签名 |
| `L2源码核对` | `zero-cap` | `media,customers,interop` | [08-立绘接入与PortraitStudio.md](<08-立绘接入与PortraitStudio.md>) | 让 NPC 立绘被 Portrait Studio 识别且零能力不交控制权、1728x1266 硬规格 |
| `L2源码核对` | `zero-cap` | `items,architecture,interop` | [09-模组架构与物品调用规范.md](<09-模组架构与物品调用规范.md>) | 物品发现/三入口发放语义/精确投放/就绪检查链与异常边界策略 |
| `L2源码核对` | `zero-cap` | `events,customers,persistence` | [11-事件系统接入规范.md](<11-事件系统接入规范.md>) | 做空间站事件（原版池与自持蓝图双路线）、涨跌价 negociationDatas、日历与报纸 |
| `L2源码核对` | `zero-cap` | `media,interop` | [12-立绘引擎与音画资源架构.md](<12-立绘引擎与音画资源架构.md>) | 高清去像素化租约、高清道具图标 PPU 公式与防脏边、零依赖 WAV 配音挂载 |
| `L2源码核对` | `zero-cap` | `appraisal,ui` | [13-鉴定扩展与交互纸张规范.md](<13-鉴定扩展与交互纸张规范.md>) | 自定义鉴定品类/扩级、放大镜双树 LensFollow、JSON 交互纸张、关系网科技树 |
| `L2源码核对` | `zero-cap` | `architecture,persistence,ui` | [14-模组高级编程架构与工程模式.md](<14-模组高级编程架构与工程模式.md>) | ES3 防坏档投影、模态窗口防穿透、分帧索引、回滚队列、大合集三种组织模式 |
| `L2源码核对` | `zero-cap` | `machines,media` | [15-机器运行动画与雪碧图规范.md](<15-机器运行动画与雪碧图规范.md>) | 机器 8 列雪碧图运行动画、零存档污染 Image.sprite 替换、供电与状态判定 |
| `L2源码核对` | `zero-cap` | `events,media,interop` | [16-报纸事件配图与BetterNewspapers规范.md](<16-报纸事件配图与BetterNewspapers规范.md>) | 9 色新闻纸半色调配图、HarmonyAfter(mod.<id>) 与 Better Newspapers 共存避让 |
| `L2源码核对` | `zero-cap` | `afterhour,media,customers` | [18-夜晚外出区域与探索扩展规范.md](<18-夜晚外出区域与探索扩展规范.md>) | 新增打烊夜出地点、博士式摊位防丢货 Leave 铁律、全屏 CG 舞台与地图事件 |
| `L2源码核对` | `zero-cap` | `architecture,items,ui` | [20-各类模组制作规范.md](<20-各类模组制作规范.md>) | 开工前按模组类型对号入座查十二条总规、禁忌、验收清单与冲突红线 |

### 零能力 C# 架构模板（13 个全通过真实编译 + ModScanner）（13 项）

| 可信度 | 能力 | 域 | 文件 | 一句话用途 / ⚠️ 别抄 |
| --- | --- | --- | --- | --- |
| `L1实测` | `zero-cap` | `appraisal,ui` | [CustomInspectLens.cs](<../../工具库/templates/mod-patterns/CustomInspectLens.cs>) | 自定义物品接入原版放大镜双视图 + LensFollow 同步 |
| `L1实测` | `zero-cap` | `perf,economy` | [EpochCache.cs](<../../工具库/templates/mod-patterns/EpochCache.cs>) | ConditionalWeakTable + 全局 epoch 代际失效缓存（O(1) 清空、防内存泄漏） |
| `L1实测` | `zero-cap` | `persistence,events` | [Es3SafePersistence.cs](<../../工具库/templates/mod-patterns/Es3SafePersistence.cs>) | ES3Type.WriteProperties 浅克隆投影剥离（卸载不坏档），编译需引 Assembly-CSharp-firstpass.dll |
| `L1实测` | `zero-cap` | `media,interop` | [HdCameraLease.cs](<../../工具库/templates/mod-patterns/HdCameraLease.cs>) | 零反射跨模组 PixelPerfectCamera 高清去像素化共享租约 |
| `L1实测` | `zero-cap` | `machines,ui` | [MachineManualSlot.cs](<../../工具库/templates/mod-patterns/MachineManualSlot.cs>) | 把说明文档直接放进原版机器自带说明书槽（免做额外 UI） |
| `L1实测` | `zero-cap` | `multiplayer,persistence,workflow` | [ModDataInbox.cs](<../../工具库/templates/mod-patterns/ModDataInbox.cs>) | 用原版白名单 ModData 文件收件箱与外部脚本/AI 通信（不开端口且实测零能力） |
| `L1实测` | `zero-cap` | `ui` | [ModalInputGate.cs](<../../工具库/templates/mod-patterns/ModalInputGate.cs>) | 游戏内大型模态面板防关窗点击穿透状态机与焦点清理 |
| `L1实测` | `zero-cap` | `events,media,interop` | [NewspaperArtHelper.cs](<../../工具库/templates/mod-patterns/NewspaperArtHelper.cs>) | 报纸事件配图 9 色复古相框、CenterCrop 与 Better Newspapers 避让排版 |
| `L1实测` | `zero-cap` | `items,architecture` | [ReadyChain.cs](<../../工具库/templates/mod-patterns/ReadyChain.cs>) | 店内就绪检查链（先判 PlayerStore.HasInstance）与 Unity 假 null 检测 |
| `L1实测` | `zero-cap` | `media,items` | [SafeSpriteHelper.cs](<../../工具库/templates/mod-patterns/SafeSpriteHelper.cs>) | 像素贴图 Point 锐化、高清道具 PPU 计算、不可读纹理 GPU 导出 PNG |
| `L1实测` | `zero-cap` | `machines,media` | [SpriteSheetMachineAnim.cs](<../../工具库/templates/mod-patterns/SpriteSheetMachineAnim.cs>) | 8 列网格雪碧图机器运行动画（零存档污染、150ms 节流、供电与让位保护） |
| `L1实测` | `zero-cap` | `machines,interop` | [TransientTagBoost.cs](<../../工具库/templates/mod-patterns/TransientTagBoost.cs>) | 临时改数值→调原版→Postfix+Finalizer 还原（不整段替换原版机器入口） |
| `L1实测` | `zero-cap` | `media` | [WavPcmDecoder.cs](<../../工具库/templates/mod-patterns/WavPcmDecoder.cs>) | 纯 C# 解析 RIFF PCM16 WAV 转 AudioClip（零第三方依赖） |

### 工坊反编译蒸馏档案（压缩要点见 DISTILL_DIGEST.md）（42 项）

| 可信度 | 能力 | 域 | 文件 | 一句话用途 / ⚠️ 别抄 |
| --- | --- | --- | --- | --- |
| `L2源码核对` | `zero-cap` | `events,persistence,customers` | [3833966506-StationEvents.md](<workshop-distill/3833966506-StationEvents.md>) | 事件模组标杆：私有蓝图表+ActivateEvent+negociationDatas零改价+ES3投影+天数回放 **⚠️ 非海量事件勿抄其 2 处 HandleNewEvent* IL Transpiler（双改冲突点且未声明优先级）** |
| `L2源码核对` | `zero-cap` | `events,customers,items` | [事件与内容扩展四例.md](<workshop-distill/事件与内容扩展四例.md>) | 卷帘门事件展示、拾荒事件注入、外置新闻数据、熟练度等级挂原版数值四例对照 **⚠️ 勿抄 Scavenging Events 放弃官方口子去整段替换拾荒方法** |
| `L2源码核对` | `zero-cap` | `events,machines,media` | [报纸配图与机器动画两标杆.md](<workshop-distill/报纸配图与机器动画两标杆.md>) | Better Newspapers 与 Vanilla Machine Animations 源码对账（修正 15/16 号文档 10 处误差） |
| `L2源码核对` | `zero-cap` | `machines,items,persistence` | [生产链与自动化四例.md](<workshop-distill/生产链与自动化四例.md>) | 酿酒/化学/水培塔/Station 四例：标准机器四件套、零整段替换、DecodeNodes 存档迁移与 19 文件总控自动化 |
| `L2源码核对` | `zero-cap` | `media,customers` | [美术与角色模组三级形态与立绘包规格.md](<workshop-distill/美术与角色模组三级形态与立绘包规格.md>) | 美术模组三级形态（L1 3文件/L2 15文件/L3 58文件）、1728×1266 立绘硬规格与空壳资源包 |
| `L3反编译` | `zero-cap` | `architecture,ui` | [3828893950-DangpuPlus.md](<workshop-distill/3828893950-DangpuPlus.md>) | 巨无霸多功能合集组织范本(a)：静态委托表 FeaturePack(103项)+熔断+配置门面 **⚠️ 不支持热摘除单个功能补丁；不要在简单模组里硬套 103 项框架** |
| `L3反编译` | `mixed` | `architecture,items,machines` | [3828902092-ElectronicsExpansion.md](<workshop-distill/3828902092-ElectronicsExpansion.md>) | 全工坊最大(18.7万行/389 Patch)超巨型模组：JSON内容表+有序阶段注册+补丁分相(800/0/-50)+依赖键缓存 **⚠️ 严禁抄其 2.1 万行全量反射契约表、1.2 万行单文件 OnGUI、漏装主程序集与空 critical 集** |
| `L3反编译` | `cap-network` | `multiplayer,customers,persistence` | [3828909239-ProbablyMultiplayer.md](<workshop-distill/3828909239-ProbablyMultiplayer.md>) | 自写 WebSocket + 服务器权威联机架构；真人借壳进原版顾客流程与物品树序列化隔离 |
| `L3反编译` | `zero-cap` | `appraisal,interop` | [3828921912-AppraisalAssistant.md](<workshop-distill/3828921912-AppraisalAssistant.md>) | 45万订阅鉴定助手：IL指纹+GetPatchInfo递归+IL解释器，以及 5 个共存红线方法 **⚠️ 严禁对其 5 个红线方法（GameItem.IsTag、AppraisalHelper.GetCorrectBaseValue、AppHand/Ribwich/PlasmaHelper.GetCorrectValue、AppraisalItemType.Matches、AppraisalItemRegistry.Find）做破坏性 Prefix** |
| `L3反编译` | `zero-cap` | `items,ui,architecture` | [3829012791-ItemSpawner.md](<workshop-distill/3829012791-ItemSpawner.md>) | 物品目录三级索引、字段增量编辑状态模型与 Lib/App 两层拆分架构 |
| `L3反编译` | `zero-cap` | `items,ui,perf` | [3829206666-SmartInventoryArrange.md](<workshop-distill/3829206666-SmartInventoryArrange.md>) | 61万订阅标杆：20项字典序目标、防剧透 real-state-appraisal-ignored、modifiedShape原地重排与回滚 **⚠️ 整理/排序功能严禁读取 ItemFeature.realCondition（会直接剧透真假货）** |
| `L3反编译` | `zero-cap` | `architecture,ui,interop` | [3829957165-PenQoL补丁表.md](<workshop-distill/3829957165-PenQoL补丁表.md>) | 多功能合集组织范本(c)：特性化补丁对象表 + 方法级热摘挂 Unpatch(target, method) **⚠️ 每条补丁的目标解析+安装必须独立 try/catch（勿写循环外实参求值导致单条 AmbiguousMatch 拖垮全表）；Applied 标记须对照 Harmony.GetPatchInfo 真实状态** |
| `L3反编译` | `zero-cap` | `interop,workflow` | [3832823827-ModConflictScanner.md](<workshop-distill/3832823827-ModConflictScanner.md>) | 工坊冲突检测器实证：三类冲突模型与全局显示层/本地化层两类红色硬靶点 |
| `L3反编译` | `zero-cap` | `items,machines,perf` | [3834649530-SmartArrangement.md](<workshop-distill/3834649530-SmartArrangement.md>) | 机器模块舱位图棋盘 + 模拟退火搜索 + 四卡方案对比 + 事务式装机与留空算法 |
| `L3反编译` | `zero-cap` | `architecture,afterhour,customers` | [3838944803-UpperLevels.md](<workshop-distill/3838944803-UpperLevels.md>) | DLC 级新区域：用 Difficulty.Current.source 单点总闸压成开关且零入侵存档 **⚠️ 267 Patch 仅靠首行早退有效，小模组切勿照搬大规模 Patch 面** |
| `L3反编译` | `zero-cap` | `perf,interop,workflow` | [3840533881-LagInspector.md](<workshop-distill/3840533881-LagInspector.md>) | Harmony.GetPatchInfo(method).Owners 做补丁归属归因与 Stopwatch 帧栈耗时统计 |
| `L3反编译` | `zero-cap` | `architecture,ui,persistence` | [3841509159-ClaimsAndUprisings.md](<workshop-distill/3841509159-ClaimsAndUprisings.md>) | 233 Patch 巨型模组降维三件套：作用域租约令牌 + 单点模态仲裁器 + 原子代际构建 **⚠️ 不要把 395 个文件平铺在单目录；不要靠 119 个分散早退门替代统一门面** |
| `L3反编译` | `zero-cap` | `media,architecture,interop` | [3841909877-ProbablyExtendFramework.md](<workshop-distill/3841909877-ProbablyExtendFramework.md>) | 多功能组织范本(b)与贴图引擎：自描述 Patch 注册表(延迟解析+Report)+sig注入使主体零能力 |
| `L3反编译` | `cap-files` | `media,interop,architecture` | [3841912659-ExtendFrameworkFiles.md](<workshop-distill/3841912659-ExtendFrameworkFiles.md>) | 框架本体(零能力) + 文件伴生模组(files能力) 拆分范式与约定式本地素材目录 |
| `L3反编译` | `zero-cap` | `interop,ui,afterhour` | [3842773239-WildNetAPI.md](<workshop-distill/3842773239-WildNetAPI.md>) | 跨模组零硬依赖消息总线（约定 GameObject + BroadcastMessage + 版本化字典）与 SelfCheck 环检测 |
| `L3反编译` | `zero-cap` | `perf,economy` | [3843892181-ValueCache.md](<workshop-distill/3843892181-ValueCache.md>) | 弱表 ConditionalWeakTable + 全局 epoch 代际失效缓存（EpochCache.cs 来源） **⚠️ 不要抄其 44 个无 HarmonyPriority 的密集 Patch，必须显式声明优先级** |
| `L3反编译` | `zero-cap` | `afterhour,customers,media` | [3844162371-Sakurazaki角色模组.md](<workshop-distill/3844162371-Sakurazaki角色模组.md>) | 完整 L3 角色模组范本：全屏 CG 打工舞台、外置多语言台词、回店幂等发奖与原版 NPC 选项插入 |
| `L3反编译` | `zero-cap` | `media,customers` | [3845197390-NightRegulars立绘框架.md](<workshop-distill/3845197390-NightRegulars立绘框架.md>) | 程序/美术分离的立绘框架：按需加载 + extreme→adult→serene 永不空白回退链 |
| `L3反编译` | `zero-cap` | `machines,items,architecture` | [3845199370-PipelineController.md](<workshop-distill/3845199370-PipelineController.md>) | 图结构存物品 Tag + 拓扑 Tick + 靠 GameItemFunc 委托接管交互（仅 5 个外围 Patch） |
| `L3反编译` | `zero-cap` | `items,ui,architecture` | [3845671416-StorageControlTerminal.md](<workshop-distill/3845671416-StorageControlTerminal.md>) | 零 Harmony Patch 做出带 CustomUI 界面的跨容器聚合存储终端标杆 |
| `L3反编译` | `zero-cap` | `items,appraisal,interop` | [3845680734-QiluoItemEditor.md](<workshop-distill/3845680734-QiluoItemEditor.md>) | GameItem 全字段可编辑权威字典与受保护字段黑名单 |
| `L3反编译` | `zero-cap` | `multiplayer,persistence,workflow` | [3845754309-AIStoreManager.md](<workshop-distill/3845754309-AIStoreManager.md>) | 用原版白名单 ModData JSON 文件收件箱实现零端口外部进程/AI 通信（ModDataInbox.cs 来源） |
| `L3反编译` | `zero-cap` | `architecture,customers,events` | [3845965318-ZohFramework.md](<workshop-distill/3845965318-ZohFramework.md>) | 零 Patch 自研脚本 DSL 引擎（词法/语法/三 Pass 执行）、安全闲聊注入与坏脚本降级 |
| `L3反编译` | `zero-cap` | `ui,items,appraisal` | [UI深度扩展八例.md](<workshop-distill/UI深度扩展八例.md>) | 八种 UI 扩展改法与代价对照（Tooltip 钩子、窗口缩放接管 getter、轮盘防输入锁死） |
| `L3反编译` | `zero-cap` | `appraisal,items,architecture` | [两类内容扩展-鉴定服务与零Patch养殖.md](<workshop-distill/两类内容扩展-鉴定服务与零Patch养殖.md>) | 鉴定/盘查/保险/服务费 20 个靶点表 + 零 Patch 做整套养殖玩法（32文件/5792行）骨架 **⚠️ 同一靶点（如 OnEstimate）挂多个补丁时必须显式声明 HarmonyPriority** |
| `L3反编译` | `zero-cap` | `machines,items,interop` | [机器改造与流体操作四例.md](<workshop-distill/机器改造与流体操作四例.md>) | 原版机器槽位全景、TransientTagBoost 临时改数值范式、实时触发时机与液体原子定量搬运 |
| `L3反编译` | `cap-network` | `workflow,interop,architecture` | [桥接器与开发框架两例.md](<workshop-distill/桥接器与开发框架两例.md>) | MCP Bridge vs 深空桥接器安全边界对照 + SDJYBN 轻量框架结构 **⚠️ 严禁抄 MCP Bridge 零鉴权 + CORS *（本机任意网页可驱动游戏）** |
| `L3反编译` | `zero-cap` | `architecture,events,afterhour` | [玩法模块三例-职业拍卖灾难.md](<workshop-distill/玩法模块三例-职业拍卖灾难.md>) | 新玩法接原版三时段选型（营业日开关门 / 打烊夜出地图 / 跨日 StoreEvent 链） **⚠️ 勿抄单文件 6000 行巨石与 297 个分散 ModData 散键（应结构化收纳）** |
| `L3反编译` | `mixed` | `ui,multiplayer,workflow` | [补充两例-调试面板与双人联机.md](<workshop-distill/补充两例-调试面板与双人联机.md>) | 调试面板（改 getter 不改字段）与 TCP+镜像视图双人联机路线对照 |
| `L3反编译` | `zero-cap` | `economy,architecture,persistence` | [金融与公司两个经济系统.md](<workshop-distill/金融与公司两个经济系统.md>) | 影子账本（Exchange：不改原版价格+日种子回放 K 线）vs 内容经济（CorpPresence：九企档位）路线选型 **⚠️ 慎抄 CorpPresence 重度改写存档链** |
| `L4⚠️含禁抄` | `zero-cap` | `items,machines,interop` | [3844123767-ContainerLink.md](<workshop-distill/3844123767-ContainerLink.md>) | 不新增容器类型的跨柜连接（版本化 Tag 存图 + 事务式物品搬运） **⚠️ 严禁抄其大量空 catch {} 静默吞异常** |
| `L4⚠️含禁抄` | `zero-cap` | `perf,machines` | [3844377158-SleepLagOptimizer.md](<workshop-distill/3844377158-SleepLagOptimizer.md>) | 原版过夜 360 tick 化学模拟真相与 Prefix 开标志 / Finalizer 关标志延迟校验范式 **⚠️ 严禁抄其整段替换 RunNightlyChemicalSimulation（与所有同类优化模组互斥）** |
| `L4⚠️含禁抄` | `zero-cap` | `items,persistence` | [3845554726-DigitalVault.md](<workshop-distill/3845554726-DigitalVault.md>) | 物品数据化（将物品状态编码进 Tag 字符串存储）思路参考 **⚠️ 严禁抄其 96 处空 catch {} 静默吞异常** |
| `L4⚠️含禁抄` | `mixed` | `ui,interop` | [3845556624+3845895085-文字层逆向.md](<workshop-distill/3845556624+3845895085-文字层逆向.md>) | 本地化表初始化时序（早期读 LocHelper.Get 报 invalid operation handle）与 TMP 全局 CJK 回退字体 **⚠️ 严禁抄 Locale Error Fix 吞掉原版 fallback 链导致缺词退化为裸 key** |
| `L4⚠️含禁抄` | `zero-cap` | `items,media,interop` | [3845751834-SpriteHeal.md](<workshop-distill/3845751834-SpriteHeal.md>) | 原版 useSpriteFromMod 粘滞 bug 逆向（切回原版图集未清标志→白底问号） **⚠️ 不要抄其内部空 catch {}** |
| `L4⚠️含禁抄` | `zero-cap` | `persistence,perf,ui` | [3846086230-ProgressBar.md](<workshop-distill/3846086230-ProgressBar.md>) | 原版读档管线四阶段逆向（SaveState→Decode→Link→挂载）与分帧重建 + 模态 InputGate **⚠️ 严禁在不具备完整出错回退的前提下整段替换 DecodeSaveItem** |
| `L4⚠️含禁抄` | `mixed` | `interop,persistence` | [模组列表管理五例.md](<workshop-distill/模组列表管理五例.md>) | SaveFile.es3 modOrder/modTrust HMAC 结构与 ModLoader 只读公开面实证 **⚠️ 严禁调用 [ModRestricted(loader-target)]、严禁 ES3.Save(modOrder/modTrust)、严禁像 Mod Preloader 拼 char[] 绕过 ModScanner** |

### 工具脚本与 AI 技能（19 项）

| 可信度 | 能力 | 域 | 文件 | 一句话用途 / ⚠️ 别抄 |
| --- | --- | --- | --- | --- |
| `L1实测` | `n/a` | `workflow` | [README.md](<../AI技能库/README.md>) | 流程层 AI 技能库（mattpocock/skills 27 个中文化方法论与工作区落地） |
| `L1实测` | `n/a` | `workflow` | [Backup-All.ps1](<../../工具库/build/Backup-All.ps1>) | 本地裸仓镜像 + bundle + 美术文案资产包一键多路备份 |
| `L1实测` | `n/a` | `workflow` | [Build-Mod.ps1](<../../工具库/build/Build-Mod.ps1>) | 统一编译开发版/-PublicRelease零能力版并支持-Scan/-Install |
| `L1实测` | `n/a` | `workflow,interop` | [Invoke-PreMergeGate.ps1](<../../工具库/build/Invoke-PreMergeGate.ps1>) | 合并前五道门禁一键执行（编译/零能力/回归/冲突/洁净） |
| `L1实测` | `n/a` | `workflow` | [New-FeatureBranch.ps1](<../../工具库/build/New-FeatureBranch.ps1>) | 按规范同时创建本地与云端功能分支 |
| `L1实测` | `n/a` | `workflow` | [New-ReleasePackage.ps1](<../../工具库/build/New-ReleasePackage.ps1>) | 工坊零能力发布封装、封面与白名单校验 |
| `L1实测` | `n/a` | `workflow` | [Publish-Workshop.ps1](<../../工具库/build/Publish-Workshop.ps1>) | 工坊打包上传（须用户确认；默认不覆盖线上描述） |
| `L1实测` | `n/a` | `workflow,interop` | [Test-ArcadeLoaderScan.ps1](<../../工具库/build/Test-ArcadeLoaderScan.ps1>) | 调用游戏真实 ModScanner 离线扫描候选 DLL 权限 |
| `L1实测` | `n/a` | `events,media` | [newsprint-tone.py](<../../工具库/img/newsprint-tone.py>) | 9 色泛黄新闻纸 + 2x2 半色调网点图片转换 |
| `L1实测` | `n/a` | `machines,media` | [pack-spritesheet.py](<../../工具库/img/pack-spritesheet.py>) | 机器 8 列雪碧图打包/校验/拆解 |
| `L1实测` | `n/a` | `workflow` | [Sync-ModdevSkill.ps1](<../../工具库/kb-tools/Sync-ModdevSkill.ps1>) | 对齐双副本脚本并同步 probably-stolen-moddev 到 DSH_HOME（SHA-256 核验） |
| `L1实测` | `n/a` | `workflow,architecture` | [kb_pipeline.py](<../../工具库/kb-tools/kb_pipeline.py>) | 知识库自动打标、原版源码验真、按标签秒检、13模板零能力实测 |
| `L1实测` | `n/a` | `workflow` | [Run-Regression.ps1](<../../工具库/regression/Run-Regression.ps1>) | 运行离线真行为与核心不变量回归套件 |
| `L1实测` | `n/a` | `workflow` | [extract_api.py](<../../工具库/skills/decomp-api/extract_api.py>) | 从反编译 .cs 秒级提取公开 API 清单 |
| `L1实测` | `n/a` | `workflow,interop` | [inspect_mod.py](<../../工具库/skills/decomp-api/inspect_mod.py>) | 秒级透视源码目录或 DLL 的 IMod/HarmonyPatch/Hook/权限信号 |
| `L1实测` | `zero-cap` | `architecture,items,machines` | [SKILL.md](<../../工具库/skills/probably-stolen-moddev/SKILL.md>) | 《深空当铺》全流程核心领域开发技能（路由总入口） |
| `L1实测` | `n/a` | `interop,workflow` | [conflict_scan.py](<../../工具库/workshop-tools/conflict_scan.py>) | 跨模组静态冲突扫描（支持 --gate 与 HarmonyBefore/After 识别） |
| `L1实测` | `n/a` | `workflow` | [distill_prep.py](<../../工具库/workshop-tools/distill_prep.py>) | 批量挑主 DLL 反编译并生成透视报告 |
| `L1实测` | `n/a` | `workflow` | [fetch_workshop.py](<../../工具库/workshop-tools/fetch_workshop.py>) | 抓取 Steam 创意工坊列表/简介摘要/本地订阅对比 |

## 3. ⚠️ 全库反面教材与禁抄红线汇总

| 资料 | 可信度 | 绝对不要抄什么 |
| --- | --- | --- |
| [3828893950-DangpuPlus.md](<workshop-distill/3828893950-DangpuPlus.md>) | `L3反编译` | 不支持热摘除单个功能补丁；不要在简单模组里硬套 103 项框架 |
| [3828902092-ElectronicsExpansion.md](<workshop-distill/3828902092-ElectronicsExpansion.md>) | `L3反编译` | 严禁抄其 2.1 万行全量反射契约表、1.2 万行单文件 OnGUI、漏装主程序集与空 critical 集 |
| [3828921912-AppraisalAssistant.md](<workshop-distill/3828921912-AppraisalAssistant.md>) | `L3反编译` | 严禁对其 5 个红线方法（GameItem.IsTag、AppraisalHelper.GetCorrectBaseValue、AppHand/Ribwich/PlasmaHelper.GetCorrectValue、AppraisalItemType.Matches、AppraisalItemRegistry.Find）做破坏性 Prefix |
| [3829206666-SmartInventoryArrange.md](<workshop-distill/3829206666-SmartInventoryArrange.md>) | `L3反编译` | 整理/排序功能严禁读取 ItemFeature.realCondition（会直接剧透真假货） |
| [3829957165-PenQoL补丁表.md](<workshop-distill/3829957165-PenQoL补丁表.md>) | `L3反编译` | 每条补丁的目标解析+安装必须独立 try/catch（勿写循环外实参求值导致单条 AmbiguousMatch 拖垮全表）；Applied 标记须对照 Harmony.GetPatchInfo 真实状态 |
| [3833966506-StationEvents.md](<workshop-distill/3833966506-StationEvents.md>) | `L2源码核对` | 非海量事件勿抄其 2 处 HandleNewEvent* IL Transpiler（双改冲突点且未声明优先级） |
| [3838944803-UpperLevels.md](<workshop-distill/3838944803-UpperLevels.md>) | `L3反编译` | 267 Patch 仅靠首行早退有效，小模组切勿照搬大规模 Patch 面 |
| [3841509159-ClaimsAndUprisings.md](<workshop-distill/3841509159-ClaimsAndUprisings.md>) | `L3反编译` | 不要把 395 个文件平铺在单目录；不要靠 119 个分散早退门替代统一门面 |
| [3843892181-ValueCache.md](<workshop-distill/3843892181-ValueCache.md>) | `L3反编译` | 不要抄其 44 个无 HarmonyPriority 的密集 Patch，必须显式声明优先级 |
| [3844123767-ContainerLink.md](<workshop-distill/3844123767-ContainerLink.md>) | `L4⚠️含禁抄` | 严禁抄其大量空 catch {} 静默吞异常 |
| [3844377158-SleepLagOptimizer.md](<workshop-distill/3844377158-SleepLagOptimizer.md>) | `L4⚠️含禁抄` | 严禁抄其整段替换 RunNightlyChemicalSimulation（与所有同类优化模组互斥） |
| [3845554726-DigitalVault.md](<workshop-distill/3845554726-DigitalVault.md>) | `L4⚠️含禁抄` | 严禁抄其 96 处空 catch {} 静默吞异常 |
| [3845556624+3845895085-文字层逆向.md](<workshop-distill/3845556624+3845895085-文字层逆向.md>) | `L4⚠️含禁抄` | 严禁抄 Locale Error Fix 吞掉原版 fallback 链导致缺词退化为裸 key |
| [3845751834-SpriteHeal.md](<workshop-distill/3845751834-SpriteHeal.md>) | `L4⚠️含禁抄` | 不要抄其内部空 catch {} |
| [3846086230-ProgressBar.md](<workshop-distill/3846086230-ProgressBar.md>) | `L4⚠️含禁抄` | 严禁在不具备完整出错回退的前提下整段替换 DecodeSaveItem |
| [两类内容扩展-鉴定服务与零Patch养殖.md](<workshop-distill/两类内容扩展-鉴定服务与零Patch养殖.md>) | `L3反编译` | 同一靶点（如 OnEstimate）挂多个补丁时必须显式声明 HarmonyPriority |
| [事件与内容扩展四例.md](<workshop-distill/事件与内容扩展四例.md>) | `L2源码核对` | 勿抄 Scavenging Events 放弃官方口子去整段替换拾荒方法 |
| [桥接器与开发框架两例.md](<workshop-distill/桥接器与开发框架两例.md>) | `L3反编译` | 严禁抄 MCP Bridge 零鉴权 + CORS *（本机任意网页可驱动游戏） |
| [模组列表管理五例.md](<workshop-distill/模组列表管理五例.md>) | `L4⚠️含禁抄` | 严禁调用 [ModRestricted(loader-target)]、严禁 ES3.Save(modOrder/modTrust)、严禁像 Mod Preloader 拼 char[] 绕过 ModScanner |
| [玩法模块三例-职业拍卖灾难.md](<workshop-distill/玩法模块三例-职业拍卖灾难.md>) | `L3反编译` | 勿抄单文件 6000 行巨石与 297 个分散 ModData 散键（应结构化收纳） |
| [金融与公司两个经济系统.md](<workshop-distill/金融与公司两个经济系统.md>) | `L3反编译` | 慎抄 CorpPresence 重度改写存档链 |
