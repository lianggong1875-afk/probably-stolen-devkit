# 工坊 3832819040 架构蒸馏档案 —— `DeepSpaceClinic_InventoryTest`《深空诊所2077 / Deep Space Clinic 2077》v0.1.0-ui.227

> **工坊 id**：`3832819040`｜9.7 千订阅｜`manifest.xml` 里 `Author` 写的是「Local development」、`ModVersion 0.1.0-ui.227`、`<Capabilities>` 是 **`reflection-external` + `dynamic-code` 两条非空能力**（对照我们的零能力发布红线，这一包**不能照抄**，见 §5）｜装配 `Clinic.InventoryTest.dll`（939 KB）+ `Textures\` + 一份 41 KB、由作者自己写的 `README.md`（ui.1→ui.227 变更流水）。
> **反编译目录**：`缓存文件夹\decomp_ws\3832819040\`（ILSpy 产物：**94 个 `.cs` / 37,634 行**；除 `Properties\AssemblyInfo.cs` 12 行外，93 个文件**全部平铺在 `Clinic.InventoryTest\` 一个目录里**，无子目录、无 namespace 分层）。
> **为什么值得读**：本库目前**唯一一个把"玩法内嵌小游戏"做到闭环**的样本——6 套棋盘/时窗小游戏（推箱子、反射镜光路、走线、接线、肌腱+张力、准星停留）+ 1 层自由笔画 + 1 段锯切拖动 + 逐像素创口模拟，全部是纯 C#、零 Unity 依赖的类，由 1 个 1,112 行的 `SurgerySession` 统一编排；它同时给出了「原版顾客 → 患者 → 检测仪 → 手术 → 结算 → 术后回访」的完整链路和一套**三数值（生命 / 神经负荷 / 义体化程度）容量模型**。抄它的玩法内核与数据模型可以，抄它的类组织不行（§2.4）。

> **方法诚实声明**：先做分布表（§2），再按签名扫全库（`[HarmonyPatch]` 特性 **0 处**、手动 `Harmony.Patch` **14 处**（report:10-24）、`ModHook` 订阅 **38 处 / 10 个事件**、`ModHelper` 调用 **0 处**、`CustomUIBuilder` 窗口 **8 个**、`dsc_` 前缀 id **363 个**），精读 20 个文件：`SurgerySession`、`ImplantPacking`、`LaserReflection`、`FreehandSurface`、`SurgicalWound`、`TendonConnection`、`VoltageConnection`、`NeuralSnake`、`BallisticCalibration`、`ShoulderReplacement`、`ClinicIntake`、`ClinicPatient`、`PatientExam`、`PilotState`、`PilotProfile`、`PilotPatients`、`ScannerReadout`、`ClinicSettlement`、`PostoperativeRisk`、`TestHotkey` + `TestMod.cs` 的 12 处定点（入口/注册/患者绑定/开局/结算/补丁/转译器）。**未通读 20,549 行的 `TestMod.cs`**；行数口径为 `wc -l`（37,634）。

---

## 1. 一句话结论

**手术是"小游戏"，不是"一串概率判定"。** 6 套小游戏（推箱子 / 反射镜光路 / 走线贪吃蛇 / 接线配对 / 肌腱形状+张力时窗 / 准星停留）+ 1 层自由笔画（消毒覆盖率、切口带宽、四针缝合）**判定全部确定性**——成败只看"格子是否到位、是否越界、是否停在时窗内"，没有一次随机判定；随机只用在两处：**开局布局洗牌**（`NeuralSnake`/`VoltageConnection` 的静态 `seeds`，同种子同盘面）与**术后并发症那一次掷骰**（`PostoperativeRisk.Percent(errors)` 给百分比，`Random.value` 掷一次，决定并发症等级 0/1/2 —— `PilotState.cs:166`）。失败与重试也全部确定性：错误累加 → 扣分表 → `Risk = min(95, Errors*12)`；超时 → `TimedOut` → 可 `RetryPuzzle()` 只重置当前小游戏、保留已扣分。

---

## 2. 规模与结构

### 2.1 文件 → 行数 → 职责（94 文件 / 37,634 行，单目录平铺）

| 文件 / 组 | 行数 | 占比 | 职责 |
| --- | ---: | ---: | --- |
| `TestMod.cs` | 20,549 | **54.6%** | 一切：`IMod` 入口、物品目录注册、8 个窗口的全部自绘 UI、患者绑定与剧情/商业/结局、补丁宿主 |
| `AllNpcAppearance.cs` | 4,249 | 11.3% | 原版 NPC 立绘分类表（治安/服装/类别 + 逐 sprite 配置），纯数据 |
| `ClinicText.cs` | 1,896 | 5.0% | 中英对照文案表（1,804 行含字符串字面量）+ `T()` 查表 |
| `SurgerySession.cs` | 1,112 | 3.0% | **手术 7 阶段状态机**：器械匹配、进度、错误、时限、重试、阶段跳转 |
| 小游戏 + 躯体模拟（18 文件） | 3,331 | 8.9% | `ImplantPacking` / `LaserReflection` / `NeuralSnake` / `VoltageConnection` / `TendonConnection` / `BallisticCalibration` / `ShoulderReplacement` / `EyeReplacement` / `FreehandSurface` / `SurgicalWound` / `EyeContour` / `SpineIncisions` / `WoundCare` / `AnesthesiaCourse` / `InstrumentWear` … |
| 患者与诊疗模型（11 文件） | 1,467 | 3.9% | `ClinicPatient`（9 个病例模板）/ `PatientExam` / `ClinicIntake`（状态机）/ `PilotState`（侧车存档）/ `PilotProfile` / `PilotPatients`（224 个患者档案）/ `ScannerReadout` / `ExamImplant` / `PilotImplant` / `CustomPatients` / `AdamPatient` |
| 商业 / 剧情 / 结算（15 文件） | 1,976 | 5.3% | `ClinicCampaign`（教学进度到结局）/ `MedicalLedger` / `ClinicPricing` / `ClinicSettlement` / `ViktorShopCatalog` / `RelicStory` / `AdamFinale` / `MedicalVisit` / `PostoperativeRisk` / `CyberpsychosisRules` … |
| 美术 / 输入 / 小组件（28 文件） | 2,140 | 5.7% | 面板美术（`AssemblyBoardArt`/`NeuralBoardArt`/`ShoulderPortraitArt`/`EyeSurgeryArt`）、输入组件（`ClinicStrokeInput`/`ClinicEyeDragInput`/`ClinicShoulderDockInput`/`ClinicGloveHover`）、`TestHotkey`（Update 驱动）、`TestItem`（物品元素子类） |
| 其余 17 文件 | 902 | 2.4% | 手册/测试流程/工具函数 |
| `Properties\AssemblyInfo.cs` | 12 | 0.03% | 反编译残留 |

### 2.2 `TestMod.cs` 内部（601 个方法，按方法名分桶，误差 ±3%）

| 桶 | 行数 | 占比（占 TestMod） | 方法数 |
| --- | ---: | ---: | ---: |
| 自绘 UI（`Draw*` 2,591 + `Render*` 1,539） | 4,130 | 20.7% | 61 |
| 手术流程与患者/检测/接诊 | 3,287 | 16.5% | 122 |
| 剧情 / 来访 / 对白 / 结局 | 1,886 | 9.5% | 59 |
| 美术、立绘、图标、配色 | 1,810 | 9.1% | 47 |
| 物品 / 义体 / 定价 / 结算 | 1,279 | 6.4% | 50 |
| 存档与恢复（`ModData` 键） | 324 | 1.6% | 18 |
| 文案 / 本地化装配 | 424 | 2.1% | 20 |
| Harmony 补丁体 | 52 | 0.3% | 3 |
| 其它工具/装配（`Create*`/`Ensure*`/`Configure*`/`Tick*`/`Refresh*`…） | 6,722 | 33.8% | 221 |

没有任何单个方法超过 **260 行**（最大是 `RenderPreparationToolbar` 257 行），所以它不是"一个巨方法"，而是**601 个中号方法堆在同一个类里**。

### 2.3 冻结副本与生成代码判断

- **整包 100% 是反编译产物**：`//IL_xxxx` 注释 **2,209 行（5.9%，全部集中在 `TestMod.cs`）**、满屏 `(object)` / `(MethodBase)` / `(HarmonyMethod)null` 强制转换、`val`/`num`/`text2` 局变量名、Tab 缩进 + CRLF；`.csproj` 是 `LangVersion 14.0` / `netstandard2.1`（反编译器生成，作者真实工程是 Unity 2021.3 + C# 9）。**不能当原始源码读，也不能直接重回编译**。
- 纯括号行 **10,958 行（29.0%）**、空行 **2,186 行（5.8%）**、含字符串字面量的行 **6,807 行（18.0%）**。
- 去噪后"有信息量"的行约 **22,000 行**（58.9%）。

### 2.4 它属于哪一类？（对照本库经验值）

| 对照样本 | 特征 | 本包 |
| --- | --- | --- |
| `PC expansion`（12.3 万行，真逻辑 6%） | 巨型数据表 + 薄逻辑 | **不是**：数据只占 17%（`AllNpcAppearance`+`ClinicText`+患者表），手术内核 12.3% 是真算法 |
| `GoFishing`（单文件 24.6%） | 一个巨型静态类 + 按文件拆补丁 | **同族但更极端**：单文件 **54.6%**，且没有"按文件拆补丁"那层，601 个方法全在一个类里 |
| `Perks`（41% 是括号行） | 括号/样板堆行数 | **接近但不成立**：括号 29%，且反编译注水 5.9% |
| 判断 | — | **"单文件巨石 + 自绘 UI + 真内核"型**：UI 约 30%（真逻辑内占比），手术+小游戏内核 12.3%，内容数据 17%。真正稀缺的东西（小游戏 3.3k 行）只占 8.9%，被 55% 的 UI/胶水包着 |

**结论**：值得学的是 §4 那 5 段内核代码和 §3 的数据模型；**不值得学**的是组织方式（见 §5 第 1 条）。

---

## 3. 它怎么做这件事

### 3.1 原版有医疗/健康系统吗？有，但完全不够用 → 它自建

先查 `共享开发/reference/Assembly-CSharp/`：

- 原版健康只有**玩家自己的一根标量**：`HealthData.woundState`（0~10）+ `isWoundStable` + `isWoundedFresh`，`SERIOUS_WOUND_THRESHOLD = 5`，`HealthData.IsSeriouslyWounded()` = `woundState > 5`，夜里 `HandleNightlyWound()` 自愈/恶化（`HealthData.cs:44-136`）；受伤会扣夜间行动次数（`DumpingGroundLocation.cs:103`），养殖类物品能降低 `woundState`（`AquacultureItems.cs:237-286`）。
- 原版"医疗"只是**物品分类**：`MedicalHelper.InitPillItem()` = `SetGameItemType("MEDICAL")` + `EnableTag("ITEM_MEDICAL_TAG")`（`MedicalHelper.cs:3-9`）。
- 全库检索：模组里**没有任何一处**读 `healthData` / `woundState` / `IsSeriouslyWounded`。**结论：不复用，完全自建**；只借用原版的 `MEDICAL` / `STORAGE` / `MACHINE` / `DOCUMENT` 品类与 `ITEM_MEDICAL_TAG` 等标签（`TestMod.cs:2282-2283`、`8592`、`2393-2394`）。

### 3.2 "患者"是什么：**原版顾客 + 一层侧车病历**，不是新顾客类型

三层拼起来：

1. **档案（静态）**：`PilotPatients.Profiles[224]`（`PilotPatients.cs:11-`，每条 = `PilotProfile(id, 名字, 原版 spriteKey, CaseIndex 0~8, 锚点 x/y)`），`CaseIndex` 指向 `ClinicPatient.Cases[9]` 这 9 个病例模板（义体 key + 部位 + 患者原话 + 左右侧，`ClinicPatient.cs:10-23`）。
2. **登记进原版来客表**：`RegisterPilotFactories()` 把 224 个 id 注册成 `StoreClientListDict.storeClientDict[id] = () => CreatePilotClient(p)`（`TestMod.cs:10098-10111`）；再用 `ModHook.OnGenerateCustomerVeryLate` 的 9 个订阅把不同支线（清创 / 商业 / 战役 / 术后回访 / 遗物 / 结局）排进当天队列（`TestMod.cs:19716-19748`）。
3. **到店即绑定**：`UpdatePilotPatient()` 每帧比对 `PlayerStore.Instance.currentClientInstance` 的 blueprint id 是否命中档案；命中就换 `mainDialogue` 为患者原话、给 `StoreClientMono.Instance` 挂 `ClinicPatientConversation`（点立绘致谢）、用 `new ClinicIntake(PilotRequest(p), MedicalExam(p, PilotRecords()[p.Id]))` 建本次接诊（`TestMod.cs:10422-10490`）。

**病情存哪**：`PilotState`（每人一条，`Health`/`NeuralLoad`/`ImplantLoad`/`NeuralQuarter`/`ImplantQuarter`/`Operations`/`Dismissals`/`Dead`/`WoundStatus`/`Installed[]`），序列化成 `|` 分隔文本写进 **`PlayerStore.Instance.ModData()["DeepSpaceClinic.PilotPatients.v1"]`**（`TestMod.cs` 的 `SavePilotRecords`；`PilotState.cs:224` 的 `Encode()`）——只写模组私有键，不动原版存档结构。

### 3.3 容量模型（本模组的核心数值设计）

`PatientExam` = `Health`(0-100) + `NeuralLoad`(0-100) + `ImplantLoad`(0-100) + `Infection` + `Bleeding` + `Implants[]`；为显示 1/4 精度额外存 `neuralQuarter`/`implantQuarter`（`PatientExam.cs:7-20`）。

- **精神承受余量** = `100 - NeuralLoad`；**义体化程度** = `ImplantLoad`（`ScannerReadout.cs:9-10`）。
- **阈值**：`NeuralLoad >= 100` 即"赛博精神病"（`CyberpsychosisRules.IsPsychotic`）。处于精神病状态的患者**不能接诊**——`ClinicIntake.Accept()` 直接抛「精神承受余量已归零，本次不能接诊」，除非走脑机疗程（`ClinicIntake.cs:60-79`）。
- **术前判据**由 3 个布尔量组成：`ClinicAdmissionRules.CanPrepare(clinical, stage, hasReport, cyberpsychotic)` = 已接诊 & 有报告 & 非精神病（`ClinicAdmissionRules.cs:7-17`）。
- **术后写入**：`PilotState.Treat(...)` 里 `NeuralLoad/ImplantLoad` 以 1/4 点为单位累加、封顶 100，超限则把该义体的 `Complication` 直接置 3（赛博精神病）（`PilotState.cs:183-203`）。

**这就是答案：不是"随机能不能装"，而是三条 0~100 的槽位 + 一条硬阈值；检测仪读的是精确值（int），不是区间。**

### 3.4 改造（义体）是物品

- 12 个 `dsc_test_implant_*`：`skin / eyes / hands / neural / kerenzikov / sandevistan / gorilla / mantis / tendons / brain / titan / wound`（`PilotState.ImplantName` 的 switch 即全表，`PilotState.cs:99-113`）。
- 走原版物品管线：`ModHook.OnModItemDirectoryInit += Register` → `Register(ModItemDirectory)` 里 `if (!directory.Has(id)) directory.Add(id, factory)`（`TestMod.cs:19851-19937`；约 70 个注册项 = 20 种耗材 + 12 种义体 + 12 本手册 + 9 种模具 + 助熔剂/器械/机器/部件）。
- 质量与纯度走**自定义 tag**：`DSC_IMPLANT_QUALITY`（0~4，决定难度档）、`DSC_IMPLANT_PURITY`（真实纯度，`BasicForge.cs:11-21`）；由 `dsc_clinic_implant_forge_v2` 用模具 + 助熔剂 + 材料现场锻造（`BasicForge.Keys/Fluxes/BoxMaterials`）。
- **装上怎么表示**：不占装备槽，落进 `PilotState.Installed`（`PilotImplant{Id, Body, Side, Condition, Complication, Quality}`），下次检测仪按 `Installed` 重建 `ExamImplant[]`（`PilotState.cs:75-97`）；单侧/双侧由 `ArmReplacementRules.RequiresAmputation(id)` + `Side` 表达（大猩猩/螳螂刀支持 left/right/both）。

### 3.5 检测仪：真物品 + 专用安装槽 + 自绘 overlay 读数窗

- 物品 `dsc_test_supply_scan`（可反复使用）；安装槽 `dsc_clinic_scanner_station`（2 格，`GameInventoryFunc.mayInventoryAddItemFunc = candidate => candidate.identifier == "dsc_test_supply_scan" && IsItemOwned(candidate)` —— 只收检测仪，`TestMod.cs:14314-14325`）。
- 点槽 → `CustomUIManager.Instance.CreateWindow("dsc_medical_scanner", "神经与义体检测仪", "overlay", …, Style 2).SetSize(1040, 680)`（`TestMod.cs:13937`），三条 meter + 义体列表（部位 / 名称 / 成色 / 完好度 / 并发症）+ 人体标记点（`DrawScannerMeter`、`DrawScannerImplants`、`ScannerReadout.IconKey/LocationLabel/MarkerRegion`）。
- 数值是**精确 int**，但给"分档提示语"而不是裸数字：`NeuralReserveHint(0)` = 「已占用 100/100 · 无剩余承受余量」（`ScannerReadout.cs:23-31`）。
- 原版就有的赛博精神病顾客走 `PatientExam.NativeCyberpsychotic()` = `Health=-1, NeuralLoad=100, load=-1, DetailedStatusKnown=false`（`PatientExam.cs:49-56`）——**用 -1 表示"这台仪器测不出来"**。

### 3.6 六个小游戏 + 一层笔画 + 一段锯切

| 小游戏 | 文件 | 机制 | 输入 |
| --- | --- | --- | --- |
| 植入物就位（推箱子） | `ImplantPacking.cs` 160 行 | 6×6 / 7×7 / 8×8 三档，柱状障碍 + 金色目标位；`Complete` = 所有块都在目标位 | 方向键（WASD/箭头），`Move(dx,dy)` 单步推 |
| 义眼光路（反射镜） | `LaserReflection.cs` 345 行 | 12×10 场，5/6/7 面镜子，每镜 8/12/16 个角度档；光束逐段求交，打到目标累计 0.45 秒才击穿；打到 `Critical` 保护单元立刻 `Failed` | ←→ 选镜、↑↓ 旋转、空格开光 |
| 神经/脊柱走线 | `NeuralSnake.cs` 429 行 | 36×26 网格多板接力，贪吃蛇式逐格前进，撞墙即 `Crashed`；脊柱档 `StepSeconds` 加倍 | 方向键（支持"提前三格"缓冲 `bufferEarlyTurns`） |
| 机械臂接线 | `VoltageConnection.cs` 226 行 | 6 根线 + 符号 + 域值，10 组预置布局随机取，配对源与端口 | 点选源 → 点端口 |
| 肌腱接口 | `TendonConnection.cs` 263 行 | 5 种接口形状（bitmask + 旋转）配对；接上后可**校正张力**：三角波 `tensionPhase` 在 `TensionWidth=0.22` 的窗口内停下 | 点选 + 空格停表 |
| 弹道协处理器 | `BallisticCalibration.cs` 133 行 | 准星在漂移目标（正弦，难度 1/2 有振幅）`Radius=0.105` 内**连续按住 0.65 秒** | 方向键移动、按住 |
| 自由笔画层 | `FreehandSurface.cs` 335 行 | 288 格消毒覆盖 ≥76%、切口必须落在 `y∈[0.35,0.65]` 且 `x∈[0.12,0.88]`、缝合 4 针各占一个 bin | 鼠标按下/拖动（`ClinicStrokeInput`，`useDragThreshold=false`） |
| 肩关节切离 | `ShoulderReplacement.cs` 301 行 | 32 段深度数组 + 锯片接触秒数 + 出血量，阶段 `Exposed→Wiring→Finished` | 按住拖动锯 |

### 3.7 一个患者从进门到做完手术（完整链路）

1. **Day 打开卷帘门** → `ModHook.OnShutterOpenedLate` 里各支线排程（`WoundVisitSchedule.Due(day)` 要求"距上次 ≥2 天"，`WoundVisitSchedule.cs:9-14`）。
2. **当天队列生成** → `ModHook.OnGenerateCustomerVeryLate` 把 `AppendWoundVisits / AppendCampaignVisits / AppendPostoperativeFollowups …` 的结果压进 `clientStack`（`TestMod.cs:19736-19748`）。
3. **顾客到店** → `StoreClientInstance` 的 blueprint id 命中 `PilotPatients` → `UpdatePilotPatient` 换台词、挂 `ClinicPatientConversation`、建 `ClinicIntake`（`TestMod.cs:10422+`）。
4. **玩家开诊所窗** → 柜台上的手套/入口 `ClinicGloveHover` 悬停高亮，点击 → `dsc_clinic_intake`（1010×750 overlay，`TestMod.cs:8057`）。
5. **看患者诉求** → `ClinicPatient.Speech` 一句话 + 需要的义体 key + 部位（对话即"听他说"）。
6. **拿检测仪**（背包里的 `dsc_test_supply_scan`）**放进安装槽**（`dsc_clinic_scanner_station`）。
7. **扫描** → `ClinicIntake.Scan()`：`Arrived → Scanned`，把真实 `PatientExam` 挂到 `Report`（`ClinicIntake.cs:45-54`）。
8. **读报告** → `dsc_medical_scanner` 窗：生命 / 精神承受余量 / 义体化程度 + 义体清单 + 人体标记。
9. **决定**：接诊（`Accept()`，精神病患者被拒）或遣散（`Dismiss()`）。
10. **备料** → `dsc_preparation_controls`（520×430/590）；`RequireSurgicalTools(session)` 逐项校验准备区里**真有**对应器械（缺就抛「准备区缺少可用器械：…」，`TestMod.cs:11995-12027`）。
11. **开始手术** → `StartSurgery()`（`TestMod.cs:18643-18800`）：校验麻醉（`ArmReplacementRules.CanStart` 要求真实纯度 ≥90%）、算难度 `ClinicPricing.DifficultyForQuality`、`new SurgerySession(difficulty, neural, optical, bodyKey, anesthesia, limbReplacement, skinPacking, woundCare)`、`LockPreparedContainers()` 把准备区窗口 `Lock()+Hide()`、写入账单。
12. **第 0 步 消毒**（`FreehandSurface`，拖过 76% 的格子）；**第 1 步 铺洞巾**（点 1 处高亮）；**第 2 步 切开**（笔画在带宽内 + 32 bin 覆盖，或脊柱切开 `SpineIncisions`）；**第 3 步 植入**（按部位派发到 §3.6 的某个小游戏）；**第 4 步 缝合**（4 针）；**第 5 步 包扎**；**第 6 步 结果**。
13. **每步都要先选器械**：`CurrentTool` 基本等于 `RequiredTools[Stage]`（脑机/义眼/机械臂另有分支，`SurgerySession.cs:98-133`）；`InstrumentProfiles.ForBody(body)` 给出 6 件（消毒/洞巾/主刀/植入器械/缝合/敷料，`InstrumentProfiles.cs:5-33`）；选错器械 → `Record("本步使用了不合适的器械", 1)` 扣分不推进。
14. **限时**（`SurgerySession.cs:477-499`）：默认 45/30/18 秒（难度 0/1/2）；脊柱 ×2、义眼 90、机械臂 `60-d×10`、腿部 60、皮下护甲 35；`ClockStarted` 要等玩家**第一次真正动手**（按方向键 / 点肌腱）才置 true（`:799-855`），所以读报告、选器械不算时间。
15. **失败与重试**：超时 → `TimedOut` + `Record("植入超时", 3)`；神经碰撞 → `Record("神经线路碰撞", 2)` 且脊柱需 `ReinstallRequired`；肌腱形状错 → 扣 15 秒；`RetryPuzzle()` 只重置当前小游戏并保留已扣分（`SurgerySession.cs:1047-1079`）。
16. **强制跳过**：`ForceStep()` 允许跳过（`Record("跳过未完成操作", 2)`），但脑机步骤跳过 = 直接 `DestroyBrain()`（`SurgerySession.cs:1013-1045`）。
17. **结算**：错误按原因归并成 `SurgicalDeduction[]`（`ClinicSettlement.Summarize` 用 `GroupBy(Reason)`，把"用了不适合本步的器械"三种措辞统一成"器械选择不当"），`Total` 决定并发症百分比 `min(95, Total*12 + 义体纯度惩罚)`（`TestMod.cs:14727`、`PostoperativeRisk.cs:8-11`）。
18. **掷一次骰**：`PilotState.Treat(..., roll: Random.value)` → `Complication = roll*100 < risk ? (back?2:1) : 0`（`PilotState.cs:166`）；再 `PlanPostoperativeFollowup` 第二次掷骰决定"两天后是否回来复诊"（`TestMod.cs:10861-10879`）。
19. **写入病历**：`SavePilotRecords()` → `ModData["DeepSpaceClinic.PilotPatients.v1"]`；扣 1 个义体（`unitCount-1` 或 `Destroy()`）；`SettleImplantPayment`；若术后 `Cyberpsychotic` 则给顾客打赛博精神病表现。
20. **回访与升级**：`ClinicCampaign` 记 `BasicCases/EyeCases/GorillaCases/MantisCases/NeuralCases` 计数；`FirstAid.Count>=1` → `MentorDue` → 维克托招募来访（"跟着你学"）→ 推荐患者 → `AdamUnlocked = NeuralCases>0`（`ClinicCampaign.cs:7-51`）。

### 3.8 Patch 靶点表（14 处手动 `Harmony.Patch`，分散在 9 个 `Register*` 方法里，**0 个 `[HarmonyPatch]` 特性**）

| # | 目标方法 | 类型 | 目的 | 风险 / 是否热点 |
| --- | --- | --- | --- | --- |
| 1 | `WildNetworkUI.Open(bool)`（**他模组**，`TypeByName`） | Prefix `SyncClinicBusinessEntry` | 打开他人网表 UI 时同步诊所商业入口 | 低（`TypeByName==null` 就跳过） |
| 2 | `NetworkUpgradeLayout.Build(IList<NetworkUpgrade>)`（**他模组**） | Prefix `FilterWageBrotherNetwork` | 过滤网表升级列表 | 低 |
| 3 | `SecData.GetCrimeString(string)` | Postfix `ImplantCrimeDisplay` | 义体违禁名显示 | 中（文本/罪证显示） |
| 4 | `SecData.IsContrabandCrime(string)` | Postfix `ClassifyImplantCrime` | 把义体归入违禁分类 | 中 |
| 5 | `SecData.CheckSoldContraband(GameItem, StoreClient)` | Prefix `FinaleCrimeBefore` + Postfix `FinaleCrimeAfter` | 统计违禁手术收入成罪证 | **高**（销赃主路径，原版安全系统核心） |
| 6 | `GameItem.GetNegociatedValue()` | Postfix `FinaleSalePrice` | 改成交价 | **高**（每件物品议价都走，价格热点） |
| 7 | `PlayerStore.UpdateServiceCost()` | Postfix `FinaleServicesChanged` | 服务成本刷新后重算 | 中 |
| 8 | `StoreClient.CanClientExposeThisFeature(ItemFeature)` | Postfix `ExposeImplantLabel` | 让义体可被顾客识破 | 中（对质链） |
| 9 | `ItemFeature.GetClientExposeDialog()` | Prefix `ImplantConfrontDialogue` | 对质台词替换 | 中 |
| 10 | `GameItemElement.GetTooltipBasic()` | Postfix `AppendAnestheticPurity` + **Transpiler** `HideClinicDisabledTooltip` | 追加麻醉真实纯度 / 隐藏"不可用"提示行 | **高**（Transpiler + 反射 `CodeInstruction.labels` 私有字段；有 `LogWarning` 兜底，`TestMod.cs:8838-8866`） |
| 11 | `ItemMouseHandler` / `ItemMouseDragHandler` / `ItemMouseDoubleClickHandler` 的 `OnEventPress(ISet<KeyCode>)` | Prefix `AllowInventoryPress` ×3 | 术中拦截背包点击 | **高**（输入热点，3 个类型循环打） |
| 12 | `GameOptionRegistry.EnsureVanilla()` | Postfix `EnsureClinicShortcutOptions` | 注册诊所快捷键选项 | 低 |
| 13 | `WageSurvival.SurvivalTrade.AddPriceLabel(GameItem,string,string,int,bool,int,Func<string>)`（**他模组**） | Prefix `AllowSurvivalPriceLabel` | 他模组价格标签兼容 | 低（缺类型即不 patch） |
| 14 | `WageSurvival.SurvivalPrice.ApplySurvivalSell(GameItem,long,bool)`（**他模组**） | Prefix `AllowSurvivalSalePrice` | 他模组出售价兼容 | 低 |

**做法值得抄的两点**：① 14 处里 **4 处是对其它工坊模组的软补丁**，一律 `AccessTools.TypeByName("WageSurvival.…")` + `methodInfo != null` 才 Patch；② `ModHook` 优先（38 处订阅 10 个事件），Harmony 只用在"原版没有钩子"的地方。

---

## 4. 关键代码摘录（Tab 缩进已展成 2 空格；`//IL_` 噪声已删）

### 4.1 推箱子判定 —— `ImplantPacking.cs:136-159`

```csharp
public bool Move(int dx, int dy)
{
  if (Complete || Math.Abs(dx) + Math.Abs(dy) != 1) return false;   // 只收单步
  (int, int) tuple = (Player.x + dx, Player.y + dy);
  if (IsWall(tuple.Item1, tuple.Item2)) return false;
  int num = Array.IndexOf(Blocks, tuple);
  if (num >= 0)                       // 前方有块 → 块也推一格，被挡就整体失败
  {
    (int, int) tuple2 = (tuple.Item1 + dx, tuple.Item2 + dy);
    if (IsWall(tuple2.Item1, tuple2.Item2) || Array.IndexOf(Blocks, tuple2) >= 0) return false;
    Blocks[num] = tuple2;             // 玩家不动时块也不动（推失败则整步无效）
  }
  Player = tuple;
  return true;
}
// 胜利条件：Complete => Blocks.All(b => Array.IndexOf(Targets, b) >= 0)   (ImplantPacking.cs:22)
```

### 4.2 反射镜光路：命中 → 累计曝光 0.45 秒；打到保护单元 → 立刻失败 —— `LaserReflection.cs:297-329`

```csharp
if (kind == 1)                                  // 命中镜子
{
  (double, double) item = Mirrors[index];
  Beam.Add(item);
  if (!hashSet.Add((index, (int)Math.Round(Math.Atan2(dy, dx) * 10000.0)))) break;  // 同一面/同一角度重复 → 终止，防死循环
  double num4 = 2.0 * orientations[Angles[index]] - Math.Atan2(dy, dx);              // 镜面反射
  x = item.Item1; y = item.Item2;
  dx = Math.Cos(num4); dy = Math.Sin(num4);
  num2 = index;                                  // 下一次不许再打自己
  continue;
}
Beam.Add((x + dx * distance, y + dy * distance));
if (kind == 3)                                   // 打到 Critical 保护单元
{
  Failed = true; Firing = false; return true;    // 立即失败，不退曝光
}
Exposure[index] += delta;                        // 打到目标：按帧累计
if (Exposure[index] >= 0.45)
{
  Broken[index] = true;                          // 击穿
  if (Complete) Firing = false;                  // 全部击穿 → 过关
}
return true;
```

`Rotate` 里还有一条关键体验设计：**转任何一面镜子都会清掉失败态与光束**，让玩家"边转边看"，不用先关电源（`LaserReflection.cs:163-178`）。

### 4.3 器械 + 顺序判定（手术主体就是这条）—— `SurgerySession.cs:753-784`

```csharp
public bool Use(int target)
{
  if (Complete || Done || Stage == 3) return false;
  if (Selected == "") { Feedback = "请先点击下方器械图标选择。"; return false; }
  if (target < Progress) { Feedback = "这里已完成，请点击高亮位置。"; return false; }
  if (Selected != CurrentTool) { Record("使用了不适合本步的器械", 1); return false; }  // 只扣分，不阻断
  int num = ((Stage == 1 || Stage == 5) ? 1 : 4);        // 铺洞巾 1 处、其余 4 处高亮
  if (target != Progress) { Record("操作顺序不符，请点击高亮位置", 1); return false; }
  Progress++;
  Done = Progress == num;
  Feedback = (Done ? "本步完成，可以下一步。" : "操作完成，请继续点击下一处高亮。");
  return true;
}
```

### 4.4 失败 / 超时 / 重试 —— `SurgerySession.cs:949-1010`（节选）

```csharp
if (Laser != null)
{
  bool flag3 = Laser.Tick(delta);
  if (Laser.Failed) { Record("光束击中了保护单元", 2); ClockStarted = false; }  // 停表等人重来
  if (Laser.Complete) { Done = true; Feedback = "光学校准完成。"; }
  return flag3 | flag;
}
if (Snake == null || Snake.Crashed || !Snake.Running) return flag;
snakeElapsed += delta;
while (snakeElapsed + 1E-09 >= Snake.StepSeconds && !Snake.Complete && !Snake.Crashed && Snake.Running)
{ snakeElapsed = Math.Max(0.0, snakeElapsed - Snake.StepSeconds); Snake.Step(); … }
if (Snake.Crashed)
{
  Record("神经线路碰撞", 2);
  ClockStarted = Brain;
  if (Spinal) { SpinalLinked = false; ReinstallRequired = true; }   // 脊柱：从头接
}
…
TimedOut = true;
if (Brain) { DestroyBrain("脑机植入超时"); return true; }           // 不可挽回
if (Spinal) { SpinalLinked = false; ReinstallRequired = true; }
Record("植入超时", 3);
```

### 4.5 自由笔画：采样 / 带宽 / 覆盖阈值 —— `FreehandSurface.cs:91-118、170-174`

```csharp
double num3 = Math.Sqrt(num * num + num2 * num2);
if (num3 > 0.5) { active = false; return false; }              // 一帧跳太远（瞬移鼠标）直接判废
int num4 = Math.Max(1, (int)Math.Ceiling(num3 / 0.018));       // 0.018 归一化单位补点
for (int i = 1; i <= num4; i++)
{
  double num5 = lastX + num * i / num4, num6 = lastY + num2 * i / num4;
  if (!Inside(num5, num6)) continue;
  if (strokeStage == 0)                                        // 消毒：椭圆刷子 12×24 = 288 格
    for (int j = 0; j < 12; j++) for (int k = 0; k < 24; k++)
    {
      double num7 = (k + 0.5) / 24.0 - num5, num8 = (j + 0.5) / 12.0 - num6;
      int num9 = j * 24 + k;
      if (num7 * num7 / 0.012 + num8 * num8 / 0.045 <= 1.0 && !Clean[num9]) { Clean[num9] = true; result = true; }
    }
  …
}
…
if (strokeStage == 0 && Count(Clean) >= Clean.Length * 0.76) session.FinishManual();   // 76% 覆盖即过
```

切口判定同函数 `149-156`：`num6 < 0.35 || num6 > 0.65` 记"出界"，`num5∈[0.12,0.88]` 才点亮 `cutBins[0..31]`；**出界不阻断，只让覆盖不够，于是过不了关**——这就是"手感惩罚代替弹窗惩罚"。

### 4.6 唯一的骰子 —— `PilotState.cs:166` + `PostoperativeRisk.cs:8-20`

```csharp
int num2 = ((roll * 100.0 < Math.Max(0, Math.Min(95, risk)))
            ? ((body == "back") ? 2 : 1)     // 背部更糟：术后神经刺激
            : 0);                            // 0 = 无并发症
…
public static int Percent(int errors, int extra = 0) => Math.Max(0, Math.Min(95, errors * 12 + extra));
public static bool Returns(int percent, int roll) { … return roll < percent; }
```

---

## 5. 踩坑与红线

1. **单文件 20,549 行 / 601 方法 / 零命名空间分层（红线）**：所有 UI、剧情、注册、存档键、补丁全在一个 `TestMod` 类里，改任何一处都要全文搜。**我们绝不这样写**——`深空电玩` 47 个源文件、`可能是群友` 29 个源文件的粒度才是正确量级。
2. **`<Capabilities>` 非空（红线）**：声明了 `reflection-external` + `dynamic-code`。原因很具体——`TestMod.cs:8848` 用 `typeof(CodeInstruction).GetField("labels")` 反射搬 IL 标签，并在转译器里探测 `"mech_tooltip_disabled"` 字符串字面量。**我们的正式候选必须自闭合 `<Capabilities/>`**，所以这套"改 tooltip 的 IL 注入"不能搬；要改 tooltip 应走 Postfix 追加行，不碰原版那行。
3. **"看起来是纯 UI 却没钩子"**：它没有 `MonoBehaviour` 之外的帧驱动可用，只能 `host = new GameObject(...); host.AddComponent<TestHotkey>(); DontDestroyOnLoad(host)`（`TestMod.cs:19754-19756`），然后 `TestHotkey.Update()` **每帧串行调用 21 个 `Tick*`**（`TestHotkey.cs:8-28`）。虽然有 `window.IsVisible` 早退，但 `TickClinicLocalization`/`EnsureCustomPortraits` 这类也在每帧跑。**我们的等价物应是"窗口开关时登记/注销"，而不是常驻轮询。**
4. **异常当流程控制（可接受，但要配套）**：`ClinicIntake.Accept/BeginSurgery/Finish` 在状态错时直接 `throw new InvalidOperationException("请先用检测仪扫描，再决定接诊。")`。这是"内部暴露"的正确姿势，但 UI 层必须 catch 成提示——它在 `StartSurgery` 里给了范例：`try { ConsumeAnesthetic(); flow.Begin(Roster()); } catch { ReleaseSessionLocks(); throw; }`（`TestMod.cs:18778-18786`）。
5. **物品会在手术中被拿走**：所以 `LockPreparedContainers()` 锁窗口 + 结算时二次校验 `if (val == null || val.unitCount < 1) throw "手术义体已移走，未写入患者病历。"`（`TestMod.cs:10657-10660`）。**这条必须抄**：任何"打开 UI 后消耗物品"的流程都要在提交点重新校验。
6. **存档键版本碎片**：`ModData` 里有 `DeepSpaceClinic.PilotPatients.v1`、`WoundPaymentPending.v2.<id>`、`CompletedWound.v3.<id>`、`WoundCourse.v1.<id>`、`PostFollowup.v1.<id>:<visit>`、`Preparation.v1`、`RelicJohnny.v1`、`AdamFinale.v1` … **按患者 id 拼键**，键数量随患者数线性膨胀。每条自带 `Encode/Decode` + `FormatException` 兜底（读坏保留原数据），这点是对的；但**键版本 v1/v2/v3 并存**说明迁移靠"新键覆盖旧键"，长期难清理。
7. **时间限制 + 键鼠混用**：同一套小游戏用 `Input.GetKey`（WASD + 方向键 + 小键盘 97/100/119/115）+ 鼠标拖拽双通道（`TestMod.cs:5641-5770`），**没有手柄/触屏适配**；时限 18 秒（难度 2）对新手极不友好——这也是它"小众"的原因之一。
8. **反编译注水会骗人**：`TestMod.cs` 有 2,123 行 `//IL_` 注释、6,081 行纯括号。**统计行数时先减掉这两项，否则会把"反编译器的排版"当成"作者的代码量"。**
9. **`ModHelper` 用了 0 次（反面参考）**：原版 145 个 `ModHelper` 公开方法它一个没调，全部自己手搓——直接读写 `StoreClientListDict.storeClientDict` 字典、直接 `PlayerStore.Instance.ModData()` 拼键、直接 `AccessTools` 反射他模组类型。能跑，但等于放弃了原版给的兼容层与零能力白名单；**我们照 §3.1 的 `ModHook`/`ModHelper` 优先顺序走**。

---

## 6. 对我们的可用点（点名到具体功能）

### 6.1 《深空电玩》

1. **把"概率维修"升级成"确定性小游戏"**：现有 `概率维修`（无故障不扣材料，失败扣 1 件并累计试次 +5%）是纯掷骰；可照 `SurgerySession` 的骨架加一层"每步 `RequiredTools[Stage]` + `Progress/Done/Errors` + `Record(原因, 点数)`"，把维修做成 3~4 步（拆壳 → 换件 → 校准 → 上电），错误累加进 `SurgicalDeduction` 式扣分表，最后仍然只有**一次**掷骰决定"是否复发"。内核类建议直接照 `ImplantPacking.cs` 的写法（纯 C#、坐标全 int、`Complete` 是派生属性、无 Unity 依赖），放 `src/ArcRepairPuzzle.cs`，UI 只读状态不写判定。
2. **`FreehandSurface` 的 288 格覆盖法**可直接用于 `翻新整备`：擦洗/抛光做成"拖过 76% 的表面格 + 不许越界"，比"点一下进度条"有手感，且实现只需 `bool[288]` + 一个 `IPointerDragHandler`（`ClinicStrokeInput.cs:23-45` 那 40 行就是全部输入层，含 `e.useDragThreshold = false` 这一句关键）。
3. **`ClinicSettlement.Encode/Decode` 的版本化文本账单**（`ClinicSettlement.cs:40-92`：`v2|患者|访客序|付款|并发症%` + 每行 `base64(中文原因)|点数|次数`）可直接用于 `罚没/保释台账` 与 `对峙记录`：比裸 JSON 抗改、可 SQL 式肉眼 diff、`v1/v2` 双读法给了迁移范例。
4. **跨模组软补丁范式**（`TestMod.cs:16530-16560`）：`AccessTools.TypeByName("WageSurvival.…")` → `methodInfo == null` 就静默跳过 → 只在存在时 Patch。电玩现在与 `护肝修改器`/`Better Newspapers` 共存靠"不碰"，可以再进一步：**探测到对方存在时给一行自家兼容 Prefix**，把冲突从"抢同一方法"变成"分层"。
5. **检测仪式的"精确值 + 分档话术"**（`ScannerReadout.NeuralReserveHint`）：电玩的"四档眼力"目前是纯档位；若要给玩家"看得见的余量"，抄它的"给区间话术、不给裸数字"写法（`100 - 已占用` 这种反向表达尤其适合"眼力"）。

### 6.2 《深空当铺：可能是群友》

6. **访客"档案 + 侧车记录 + 到店绑定"三件套**：群友已有 `DspState` / `DspVisitorKit`，但缺"按患者/角色存**多字段**病史"这一层。诊所的 `PilotProfile`（静态档案：sprite/case/锚点）↔ `PilotState`（动态：数值 + `Installed[]` + 编码成一行 `|` 文本）分离，正好补上 `DspState` 现在扁平的问题；`PilotState.Encode/Decode`（`PilotState.cs:224-300`）是现成的"向后兼容 + 越界修正"模板。
7. **到店即改写 `mainDialogue`**（`TestMod.cs:10493`：`currentClientInstance.GetClientBlueprint().mainDialogue = new Dialogue().SetText("CLIENT", PilotRequest(pilotProfile).Speech)`）：群友给伞萝萝/塞壬加"当日状态台词"（今天水质不合格 / 今天没带好货）可以走这条路，**不用改原版对话系统**，且天然随档恢复（只要在 `OnGameLoadedLate` 也跑一遍）。
8. **"专用安装槽只收一件物品"**（`TestMod.cs:14325` 的 `mayInventoryAddItemFunc`）：群友的 `塞壬验水`/`滤芯` 若要"把试纸插进检测仪"，这就是原版最省的表达——一个 2 格 `STORAGE` 物品 + 收件过滤器，不需要新机器。

---

## 7. 一页速查（给自己下次用）

- **想抄小游戏**：`ImplantPacking`（推箱子 160 行）、`LaserReflection`（光路 345 行）、`FreehandSurface`（笔画 335 行）三个文件单独拿走都能编译（只依赖 `System`）。
- **想抄"数值容量"**：`PatientExam` + `ScannerReadout` + `CyberpsychosisRules`（共约 270 行）就是全部。
- **想抄"结算"**：`SurgicalDeduction` + `ClinicSettlement`（124 行）+ `PostoperativeRisk`（20 行）。
- **不要抄**：`TestMod.cs` 的任何组织方式、`GetField("labels")` 反射搬 IL、每帧 21 个 Tick、`<Capabilities>` 非空。

