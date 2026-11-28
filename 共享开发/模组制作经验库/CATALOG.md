# 深空当铺工作区｜低 Token 打标总路由（CATALOG）

> **自动生成**（`python 工具库\kb-tools\kb_pipeline.py build`）｜更新时间：`2026-11-28 07:58:56`
> **省 Token 铁律**：不要整篇硬读 50~100 KB 大手册！查接口/踩坑先跑：
> `python 工具库\kb-tools\kb_pipeline.py lookup "<关键词或类名>" [--read]`（直给精确行号小节 + 模板 + 红线，单次 ~400 tok）
> 速扫全部工坊结论 → [DISTILL_DIGEST.md](<DISTILL_DIGEST.md>) ｜ 按域筛选 → `kb_pipeline.py query --domain <域>`

- **总计**：**163** 项 + **405** 个手册小节索引（规范 `27` ｜ 蒸馏 `99` ｜ 零能力模板 `16` ｜ 工具 `18` ｜ 技能 `3`），覆盖 **156** 个独立工坊 ID。
- **可信度**：`L1实测`（编译/ModScanner/真机）＞ `L2源码核对`（`Assembly-CSharp`）＞ `L3反编译` ＞ `L4⚠️含禁抄`（仅学原理，严禁照抄）。

## 1. 按系统域一行路由

| 域 | 适用场景 | 核心规范 / 模板 / 关键工具与代表蒸馏 |
| --- | --- | --- |
| `items` | 物品目录、发放、占格、容器精确投放、标签与品类 | [开发流程与经验](<../开发流程与经验.md>) · [07-踩坑台账](<07-踩坑台账.md>) · [GLOSSARY](<../GLOSSARY.md>) · [03-原版接口速查](<03-原版接口速查.md>) · [09-模组架构与物品调用规范](<09-模组架构与物品调用规范.md>) (+42) |
| `machines` | 机器逻辑、过夜周期、流体/化学品、机器动画 | [07-踩坑台账](<07-踩坑台账.md>) · [03-原版接口速查](<03-原版接口速查.md>) · [15-机器运行动画与雪碧图规范](<15-机器运行动画与雪碧图规范.md>) · [MachineManualSlot.cs](<../../工具库/templates/mod-patterns/MachineManualSlot.cs>) · [SpriteSheetMachineAnim.cs](<../../工具库/templates/mod-patterns/SpriteSheetMachineAnim.cs>) (+25) |
| `appraisal` | 鉴定品类、鉴定等级、放大镜双视图、真伪/成色判定、交互纸张 | [06-玩法与文案规范](<06-玩法与文案规范.md>) · [GLOSSARY](<../GLOSSARY.md>) · [03-原版接口速查](<03-原版接口速查.md>) · [13-鉴定扩展与交互纸张规范](<13-鉴定扩展与交互纸张规范.md>) · [CustomInspectLens.cs](<../../工具库/templates/mod-patterns/CustomInspectLens.cs>) (+8) |
| `events` | 空间站事件、新闻报纸、日历预告、Better Newspapers 兼容 | [07-踩坑台账](<07-踩坑台账.md>) · [11-事件系统接入规范](<11-事件系统接入规范.md>) · [16-报纸事件配图与BetterNewspapers规范](<16-报纸事件配图与BetterNewspapers规范.md>) · [20-各类模组制作规范](<20-各类模组制作规范.md>) · [Es3SafePersistence.cs](<../../工具库/templates/mod-patterns/Es3SafePersistence.cs>) (+11) |
| `afterhour` | 打烊后夜晚外出地点、商店摊位、CG舞台、夜晚地图事件 | [07-踩坑台账](<07-踩坑台账.md>) · [18-夜晚外出区域与探索扩展规范](<18-夜晚外出区域与探索扩展规范.md>) · [UpperDistrictHeist](<workshop-distill/3828902221-UpperDistrictHeist.md>) · [GoFishing](<workshop-distill/3829110153-GoFishing.md>) · [ScavengingExpansion](<workshop-distill/3832768562-ScavengingExpansion.md>) (+9) |
| `customers` | 顾客生成、对话系统、声望、拒收/识破、电话簿 | [03-原版接口速查](<03-原版接口速查.md>) · [08-立绘接入与PortraitStudio](<08-立绘接入与PortraitStudio.md>) · [11-事件系统接入规范](<11-事件系统接入规范.md>) · [18-夜晚外出区域与探索扩展规范](<18-夜晚外出区域与探索扩展规范.md>) · [SaveSafeStripPatch.cs](<../../工具库/templates/mod-patterns/SaveSafeStripPatch.cs>) (+24) |
| `ui` | CustomUI面板、Tooltip、热键、模态输入隔离、像素窗口、字体本地化 | [07-踩坑台账](<07-踩坑台账.md>) · [03-原版接口速查](<03-原版接口速查.md>) · [13-鉴定扩展与交互纸张规范](<13-鉴定扩展与交互纸张规范.md>) · [14-模组高级编程架构与工程模式](<14-模组高级编程架构与工程模式.md>) · [20-各类模组制作规范](<20-各类模组制作规范.md>) (+45) |
| `economy` | 经济模型、影子账本、价格修正、拍卖、金融/公司系统 | [EpochCache.cs](<../../工具库/templates/mod-patterns/EpochCache.cs>) · [UpperDistrictHeist](<workshop-distill/3828902221-UpperDistrictHeist.md>) · [XiaowoTradePerks](<workshop-distill/3828949943-XiaowoTradePerks.md>) · [FactionReputation](<workshop-distill/3828971731-FactionReputation.md>) · [KitchenBusiness](<workshop-distill/3830523891-KitchenBusiness.md>) (+12) |
| `persistence` | 存档读写、ES3防坏档投影、ModData私有数据、读档管线 | [03-原版接口速查](<03-原版接口速查.md>) · [11-事件系统接入规范](<11-事件系统接入规范.md>) · [14-模组高级编程架构与工程模式](<14-模组高级编程架构与工程模式.md>) · [20-各类模组制作规范](<20-各类模组制作规范.md>) · [21-跨模组冲突案例库](<21-跨模组冲突案例库.md>) (+30) |
| `media` | 立绘规格、PortraitStudio、高清相机租约、贴图去边、WAV配音 | [06-玩法与文案规范](<06-玩法与文案规范.md>) · [08-立绘接入与PortraitStudio](<08-立绘接入与PortraitStudio.md>) · [12-立绘引擎与音画资源架构](<12-立绘引擎与音画资源架构.md>) · [15-机器运行动画与雪碧图规范](<15-机器运行动画与雪碧图规范.md>) · [16-报纸事件配图与BetterNewspapers规范](<16-报纸事件配图与BetterNewspapers规范.md>) (+26) |
| `perf` | 性能优化、代际缓存、弱表、分帧预算、过夜卡顿排查 | [14-模组高级编程架构与工程模式](<14-模组高级编程架构与工程模式.md>) · [20-各类模组制作规范](<20-各类模组制作规范.md>) · [CrispPortraitCatalog.cs](<../../工具库/templates/mod-patterns/CrispPortraitCatalog.cs>) · [EpochCache.cs](<../../工具库/templates/mod-patterns/EpochCache.cs>) · [ElectronicsExpansion](<workshop-distill/3828902092-ElectronicsExpansion.md>) (+14) |
| `interop` | 跨模组通信、Harmony优先级、静态/运行期冲突扫描、加载器安全红线 | [开发流程与经验](<../开发流程与经验.md>) · [02-加载器与权限](<02-加载器与权限.md>) · [07-踩坑台账](<07-踩坑台账.md>) · [10-开发桥接器能力速查](<10-开发桥接器能力速查.md>) · [03-原版接口速查](<03-原版接口速查.md>) (+45) |
| `multiplayer` | 多人联机架构与外部进程通信通道 | [ModDataInbox.cs](<../../工具库/templates/mod-patterns/ModDataInbox.cs>) · [ProbablyMultiplayer](<workshop-distill/3828909239-ProbablyMultiplayer.md>) · [AIStoreManager](<workshop-distill/3845754309-AIStoreManager.md>) · [补充两例-调试面板与双人联机](<workshop-distill/补充两例-调试面板与双人联机.md>) |
| `architecture` | 大型/DLC级模组架构、单点总闸、多功合集组织、脚本DSL | [19-工坊模组逆向与蒸馏](<19-工坊模组逆向与蒸馏.md>) · [09-模组架构与物品调用规范](<09-模组架构与物品调用规范.md>) · [14-模组高级编程架构与工程模式](<14-模组高级编程架构与工程模式.md>) · [20-各类模组制作规范](<20-各类模组制作规范.md>) · [21-跨模组冲突案例库](<21-跨模组冲突案例库.md>) (+35) |
| `workflow` | 环境构建、ModScanner零能力扫描、离线回归、真机桥接探针、分支备份发布 | [分支与备份规范](<../分支与备份规范.md>) · [开发流程与经验](<../开发流程与经验.md>) · [01-环境与工具链](<01-环境与工具链.md>) · [02-加载器与权限](<02-加载器与权限.md>) · [04-真机测试与探针](<04-真机测试与探针.md>) (+34) |

## 2. 核心资产总表（99 份蒸馏明细已压缩至 DISTILL_DIGEST.md，此处不重复）

### 核心经验规范与工程手册（>5k tok 标注 ⚡ 请用 lookup 按节切读）（27 项）

| 可信度 | 体量 | 域 | 文件 | 一句话用途 |
| --- | --- | --- | --- | --- |
| `L1实测` | `~4523` | `workflow` | [分支与备份规范.md](<../分支与备份规范.md>) | 开工建分支、五道合并门禁、四路多盘备份、恢复演练与回滚纪律 |
| `L1实测` | `~2224` | `workflow,items` | [开发流程与经验.md](<../开发流程与经验.md>) | 六条模组编程硬规范（拒套娃/异常暴露/Harmony重载/物品铁律/零能力/真测试） |
| `L1实测` | `~1623` | `workflow` | [01-环境与工具链.md](<01-环境与工具链.md>) | 查本机目录分工、构建/回归命令、PS5.1 编码规则 |
| `L1实测` | `~1283` | `interop,workflow` | [02-加载器与权限.md](<02-加载器与权限.md>) | 查 ModScanner IL 扫描规则、零能力 <Capabilities/> 与哈希核验 |
| `L1实测` | `~3728` | `workflow` | [04-真机测试与探针.md](<04-真机测试与探针.md>) | Slot 1 真机测试流程、防碰 Slot 0、桥接与键鼠拖拽实操 |
| `L1实测` | `~576` | `workflow` | [05-测试与回归.md](<05-测试与回归.md>) | 跑 Run-Regression.ps1 与写防假绿的真实行为断言 |
| `L1实测` | `~1415` | `appraisal,media` | [06-玩法与文案规范.md](<06-玩法与文案规范.md>) | 西方深空科幻文案红线、realCondition 判定铁律、立绘绿边消除 |
| `L1实测` | `⚡~19.1k` | `items,machines` | [07-踩坑台账.md](<07-踩坑台账.md>) | 遇到异常症状（紫问号、穿模、假绿、热重载失效、桥接报错）第一时间按现象反查 |
| `L1实测` | `⚡~7.7k` | `workflow,interop` | [10-开发桥接器能力速查.md](<10-开发桥接器能力速查.md>) | 查 ModDevBridgeNext MCP 工具契约、热重载与 C# 探针编写 |
| `L1实测` | `~2348` | `workflow` | [17-工坊发布与SteamUGC上传.md](<17-工坊发布与SteamUGC上传.md>) | 独立进程 SteamUGC 上传、封面规格、白名单洁净度与发布核对 |
| `L1实测` | `⚡~24.6k` | `workflow,architecture` | [19-工坊模组逆向与蒸馏.md](<19-工坊模组逆向与蒸馏.md>) | 工坊抓取/筛选/反编译/精读六问与各批次蒸馏总账 |
| `L1实测` | `~2874` | `workflow` | [DISTILL_SOP.md](<../../工具库/workshop-tools/DISTILL_SOP.md>) | 工坊模组长期自动蒸馏作业书：选批纪律、9 步流水线、档案骨架与防漏提交红线 |
| `L2源码核对` | `~1342` | `appraisal,items` | [GLOSSARY.md](<../GLOSSARY.md>) | 全工作区唯一术语表（成色/真伪/品类/标签/零能力/Slot 0与1 唯一叫法） |
| `L2源码核对` | `⚡~34.3k` | `items,machines` | [03-原版接口速查.md](<03-原版接口速查.md>) | 写代码前查原版 ModHelper(145)/ModHook(44)/GameItemFunc(51) 及物品/机器/顾客/液体权威签名 |
| `L2源码核对` | `~4991` | `media,customers` | [08-立绘接入与PortraitStudio.md](<08-立绘接入与PortraitStudio.md>) | 让 NPC 立绘被 Portrait Studio 识别且零能力不交控制权、1728x1266 硬规格 |
| `L2源码核对` | `⚡~5.1k` | `items,architecture` | [09-模组架构与物品调用规范.md](<09-模组架构与物品调用规范.md>) | 物品发现/三入口发放语义/精确投放/就绪检查链与异常边界策略 |
| `L2源码核对` | `⚡~7.0k` | `events,customers` | [11-事件系统接入规范.md](<11-事件系统接入规范.md>) | 做空间站事件（原版池与自持蓝图双路线）、涨跌价 negociationDatas、日历与报纸 |
| `L2源码核对` | `~3626` | `media,interop` | [12-立绘引擎与音画资源架构.md](<12-立绘引擎与音画资源架构.md>) | 高清去像素化租约、高清道具图标 PPU 公式与防脏边、零依赖 WAV 配音挂载 |
| `L2源码核对` | `~3464` | `appraisal,ui` | [13-鉴定扩展与交互纸张规范.md](<13-鉴定扩展与交互纸张规范.md>) | 自定义鉴定品类/扩级、放大镜双树 LensFollow、JSON 交互纸张、关系网科技树 |
| `L2源码核对` | `⚡~8.4k` | `architecture,persistence` | [14-模组高级编程架构与工程模式.md](<14-模组高级编程架构与工程模式.md>) | ES3 防坏档投影、模态窗口防穿透、分帧索引、回滚队列、大合集三种组织模式 |
| `L2源码核对` | `~3090` | `machines,media` | [15-机器运行动画与雪碧图规范.md](<15-机器运行动画与雪碧图规范.md>) | 机器 8 列雪碧图运行动画、零存档污染 Image.sprite 替换、供电与状态判定 |
| `L2源码核对` | `~2445` | `events,media` | [16-报纸事件配图与BetterNewspapers规范.md](<16-报纸事件配图与BetterNewspapers规范.md>) | 9 色新闻纸半色调配图、HarmonyAfter(mod.<id>) 与 Better Newspapers 共存避让 |
| `L2源码核对` | `~4510` | `afterhour,media` | [18-夜晚外出区域与探索扩展规范.md](<18-夜晚外出区域与探索扩展规范.md>) | 新增打烊夜出地点、博士式摊位防丢货 Leave 铁律、全屏 CG 舞台与地图事件 |
| `L2源码核对` | `⚡~10.0k` | `architecture,items` | [20-各类模组制作规范.md](<20-各类模组制作规范.md>) | 开工前按模组类型对号入座查十二条总规、禁忌、验收清单与冲突红线 |
| `L2源码核对` | `~2829` | `interop,persistence` | [21-跨模组冲突案例库.md](<21-跨模组冲突案例库.md>) | 按玩家可见故障症状反查 15 例真实跨模组冲突（整块失效/过不了夜/主菜单崩）与软修法 |
| `L2源码核对` | `~2305` | `workflow,architecture` | [README.md](<README.md>) | 经验库总入口与 01~21 专题手册按任务路由表 |
| `L2源码核对` | `~2311` | `workflow,interop` | [待办-代码改进项.md](<待办-代码改进项.md>) | 从 16 批蒸馏实证中筛出的我方双模组 P0~P3 代码重构与兼容加固清单 |

### 零能力 C# 架构模板（16 个全通过真实编译 + ModScanner）（16 项）

| 可信度 | 能力 | 域 | 文件 | 一句话用途 |
| --- | --- | --- | --- | --- |
| `L1实测` | `zero-cap` | `media,perf` | [CrispPortraitCatalog.cs](<../../工具库/templates/mod-patterns/CrispPortraitCatalog.cs>) | 像素立绘无 Mipmap+Point 零显存泄漏重建、O(1)缓存与跨天/读档 SpriteDict 自愈 |
| `L1实测` | `zero-cap` | `appraisal,ui` | [CustomInspectLens.cs](<../../工具库/templates/mod-patterns/CustomInspectLens.cs>) | 自定义物品接入原版放大镜双视图 + LensFollow 同步 |
| `L1实测` | `zero-cap` | `perf,economy` | [EpochCache.cs](<../../工具库/templates/mod-patterns/EpochCache.cs>) | ConditionalWeakTable + 全局 epoch 代际失效缓存（O(1) 清空、防内存泄漏） |
| `L1实测` | `zero-cap` | `persistence,events` | [Es3SafePersistence.cs](<../../工具库/templates/mod-patterns/Es3SafePersistence.cs>) | ES3Type.WriteProperties 浅克隆投影剥离（卸载不坏档），编译需引 Assembly-CSharp-firstpass.dll |
| `L1实测` | `zero-cap` | `media,interop` | [HdCameraLease.cs](<../../工具库/templates/mod-patterns/HdCameraLease.cs>) | 零反射跨模组 PixelPerfectCamera 高清去像素化共享租约 |
| `L1实测` | `zero-cap` | `machines,ui` | [MachineManualSlot.cs](<../../工具库/templates/mod-patterns/MachineManualSlot.cs>) | 把说明文档直接放进原版机器自带说明书槽（免做额外 UI） |
| `L1实测` | `zero-cap` | `multiplayer,persistence` | [ModDataInbox.cs](<../../工具库/templates/mod-patterns/ModDataInbox.cs>) | 用原版白名单 ModData 文件收件箱与外部脚本/AI 通信（不开端口且实测零能力） |
| `L1实测` | `zero-cap` | `interop,architecture` | [ModInteropApi.cs](<../../工具库/templates/mod-patterns/ModInteropApi.cs>) | 用 ModHelper.Expose/OnMessage/SendMessage 暴露版本化跨模组 API（零外部引用零反射） |
| `L1实测` | `zero-cap` | `ui` | [ModalInputGate.cs](<../../工具库/templates/mod-patterns/ModalInputGate.cs>) | 游戏内大型模态面板防关窗点击穿透状态机与焦点清理 |
| `L1实测` | `zero-cap` | `events,media` | [NewspaperArtHelper.cs](<../../工具库/templates/mod-patterns/NewspaperArtHelper.cs>) | 报纸事件配图 9 色复古相框、CenterCrop 与 Better Newspapers 避让排版 |
| `L1实测` | `zero-cap` | `items,architecture` | [ReadyChain.cs](<../../工具库/templates/mod-patterns/ReadyChain.cs>) | 店内就绪检查链（先判 PlayerStore.HasInstance）与 Unity 假 null 检测 |
| `L1实测` | `zero-cap` | `media,items` | [SafeSpriteHelper.cs](<../../工具库/templates/mod-patterns/SafeSpriteHelper.cs>) | 像素贴图 Point 锐化、高清道具 PPU 计算、不可读纹理 GPU 导出 PNG |
| `L1实测` | `zero-cap` | `persistence,customers` | [SaveSafeStripPatch.cs](<../../工具库/templates/mod-patterns/SaveSafeStripPatch.cs>) | SaveGame/InitialSave Prefix 摘除自定义 StoreService 与 StorePhoneClient、Finalizer 还原（卸载零污染） |
| `L1实测` | `zero-cap` | `machines,media` | [SpriteSheetMachineAnim.cs](<../../工具库/templates/mod-patterns/SpriteSheetMachineAnim.cs>) | 8 列网格雪碧图机器运行动画（零存档污染、150ms 节流、供电与让位保护） |
| `L1实测` | `zero-cap` | `machines,interop` | [TransientTagBoost.cs](<../../工具库/templates/mod-patterns/TransientTagBoost.cs>) | 临时改数值→调原版→Postfix+Finalizer 还原（不整段替换原版机器入口） |
| `L1实测` | `zero-cap` | `media` | [WavPcmDecoder.cs](<../../工具库/templates/mod-patterns/WavPcmDecoder.cs>) | 纯 C# 解析 RIFF PCM16 WAV 转 AudioClip（零第三方依赖） |

### 工具脚本与 AI 技能（20 项）

| 可信度 | 能力 | 域 | 文件 | 一句话用途 |
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
| `L1实测` | `n/a` | `workflow,architecture` | [kb_pipeline.py](<../../工具库/kb-tools/kb_pipeline.py>) | 知识库打标/节级索引/极速低Token定位(lookup)/源码验真/16模板零能力实测 |
| `L1实测` | `n/a` | `workflow` | [Run-Regression.ps1](<../../工具库/regression/Run-Regression.ps1>) | 运行离线真行为与核心不变量回归套件 |
| `L1实测` | `n/a` | `workflow` | [extract_api.py](<../../工具库/skills/decomp-api/extract_api.py>) | 从反编译 .cs 秒级提取公开 API 清单 |
| `L1实测` | `n/a` | `workflow,interop` | [inspect_mod.py](<../../工具库/skills/decomp-api/inspect_mod.py>) | 秒级透视源码目录或 DLL 的 IMod/HarmonyPatch/Hook/权限信号 |
| `L1实测` | `zero-cap` | `architecture,items` | [SKILL.md](<../../工具库/skills/probably-stolen-moddev/SKILL.md>) | 《深空当铺》全流程核心领域开发技能（低Token渐进路由总入口） |
| `L1实测` | `n/a` | `interop,workflow` | [conflict_scan.py](<../../工具库/workshop-tools/conflict_scan.py>) | 跨模组静态冲突扫描（41热点/--gate/HarmonyBefore/After识别） |
| `L1实测` | `n/a` | `workflow` | [distill_prep.py](<../../工具库/workshop-tools/distill_prep.py>) | 批量挑主 DLL 反编译并生成透视报告 |
| `L1实测` | `n/a` | `workflow` | [distill_queue.py](<../../工具库/workshop-tools/distill_queue.py>) | 工坊蒸馏队列与唯一事实源台账（选批打分/记账/退订欠账体检） |
| `L1实测` | `n/a` | `workflow` | [fetch_workshop.py](<../../工具库/workshop-tools/fetch_workshop.py>) | 抓取 Steam 创意工坊列表/简介摘要/本地订阅对比 |

## 3. ⚠️ L4 级反面教材与严禁照抄红线（11 项）

| 资料 | 绝对不要抄什么 |
| --- | --- |
| [3828927781-MaiLeJi.md](<workshop-distill/3828927781-MaiLeJi.md>) | 禁799处空catch吞错；禁WriteSlotFile摘活对象防异常丢数据；缺UnpatchAll禁热更 |
| [3831439549-MoreDeviceUpgrades.md](<workshop-distill/3831439549-MoreDeviceUpgrades.md>) | 禁抄181空catch、25跳过Prefix、TryAcceptOnce=0拖拽hack及lambda补丁 |
| [3832184465-NeteaseMusic.md](<workshop-distill/3832184465-NeteaseMusic.md>) | 禁6连Prefix短路毁磁带状态机与音量；禁空catch吞网错；正式包禁System.IO/联网/放.ps1 |
| [3834226532-PerksProbablyFun.md](<workshop-distill/3834226532-PerksProbablyFun.md>) | 禁大量catch吞错、GetNegociatedValue递归Prefix双算、掐脏标及Update无门跑 |
| [3844123767-ContainerLink.md](<workshop-distill/3844123767-ContainerLink.md>) | 严禁抄其大量空 catch {} 静默吞异常 |
| [3844377158-SleepLagOptimizer.md](<workshop-distill/3844377158-SleepLagOptimizer.md>) | 严禁抄其整段替换 RunNightlyChemicalSimulation（与所有同类优化模组互斥） |
| [3845554726-DigitalVault.md](<workshop-distill/3845554726-DigitalVault.md>) | 严禁抄其 96 处空 catch {} 静默吞异常 |
| [3845556624+3845895085-文字层逆向.md](<workshop-distill/3845556624+3845895085-文字层逆向.md>) | 严禁抄 Locale Error Fix 吞掉原版 fallback 链导致缺词退化为裸 key |
| [3845751834-SpriteHeal.md](<workshop-distill/3845751834-SpriteHeal.md>) | 不要抄其内部空 catch {} |
| [3846086230-ProgressBar.md](<workshop-distill/3846086230-ProgressBar.md>) | 严禁在不具备完整出错回退的前提下整段替换 DecodeSaveItem |
| [模组列表管理五例.md](<workshop-distill/模组列表管理五例.md>) | 严禁调用 [ModRestricted(loader-target)]、严禁 ES3.Save(modOrder/modTrust)、严禁像 Mod Preloader 拼 char[] 绕过 ModScanner |
