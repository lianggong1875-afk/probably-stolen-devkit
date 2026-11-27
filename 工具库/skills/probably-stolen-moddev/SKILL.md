---
name: probably-stolen-moddev
description: 《深空当铺》（Probably Stolen）Unity/Mono C# 模组架构设计、编码实现、逆向参考与真机测试全流程技能。当用户要为《深空当铺》开发新模组或扩展功能（自定义物品/机器/鉴定品类/放大镜检视/交互纸张与计算器/空间站事件/夜晚外出地点与地图事件/顾客与立绘/配音/关系网科技树/开局剧本/游戏内UI/热重载与桥接工具）、排查 ModLoader 权限扫描与存档坏档、批量反编译蒸馏工坊模组、或做跨模组兼容与冲突排查时，务必调用本技能。
---

# 《深空当铺》（Probably Stolen）模组开发技能

本技能由工坊核心标杆模组（`Developer Tools`、`SDJYBN Framework`、`Station Events`、`Appraiser+ / AppraisalAssistant / AppraisalRules / AppraisalCompat`、`ModDevBridgeNext / MCPBridge`、`PortraitStudio / AuthorKit`、`Vanilla Machine Animations`、`Better Newspapers`、《樱咲羽衣 · 下层区诊所》、《食品券扩展与增强》）+ 2026-11 二轮工坊蒸馏（15 个模组：容器连接、数字仓储、存储控制终端、Zoh 脚本框架、冲突检测器、卡顿纠察、过夜优化、读档进度条、估价缓存、贴图自愈、文字层、Wild's Network API、扩展框架伴生）的反编译源码及实战闭环蒸馏而成。

## 1. 先按需求路由，加载对应参考文档

不要把所有参考一次性塞进上下文，按当前任务只读对应专题：

| 任务场景 | 先读哪个参考文件 |
| --- | --- |
| **开工定规范**：这一类型模组必须守什么、绝对不能做什么、验收跑什么 | `references/mod-type-standards.md` |
| **要读别人的工坊模组**：怎么筛/订阅/批量反编译/读透/归档，以及工具链踩坑 | `references/workshop-reverse-and-distill.md` |
| 查原版 `ModHook` / `ModHelper` / `ModReflect` / 物品发放 / 机器过夜 / 真伪与拒收 / 事件 API | `references/host-api-and-hooks.md` |
| 定模组架构、做 ES3 无污染防坏档、写模态 UI 输入隔离、分帧索引、回滚交付、开发桥接/热重载 | `references/architecture-patterns.md` |
| 做自定义鉴定品类/扩级、自定义放大镜细看、交互式 Paper/计算器 JSON、关系网升级、开局剧本、零依赖兼容第三方模组 | `references/appraisal-and-paper.md` |
| 扩展打烊后夜晚外出地点（商店摊位/剧情打工 CG/全屏舞台/夜晚地图事件/卡片扩展）与对应美术规格 | `references/afterhour-locations.md` |
| 立绘被 Portrait Studio 识别（零能力 IL 声明）、高清去像素化租约、高清道具图标与去边、零依赖 WAV 配音挂载、机器 8 列雪碧图运行动画、**报纸事件配图与 Better Newspapers 兼容** | `references/media-portrait-voice.md` |
| 反编译并秒级透视某个第三方工坊模组或本地 DLL 的入口/Patch/钩子/权限 | 运行 `scripts/inspect_mod.py <目录或DLL>` |
| **查"我们会不会和别人冲突"**：同一原版方法被谁和谁 Patch、注册 id 重复、同名 DLL、能力差异 | 运行 `scripts/conflict_scan.py --dir <我们源码> --dir <反编译目录> …` |
| 发布或更新 Steam 创意工坊、写独立的 SteamUGC 上传助手、上传后页面打不开、封面与发布包洁净度 | `references/workshop-publishing.md` |

## 2. 宿主环境与硬性底线

1. **运行目标**：Unity `2021.3.45f2` / Mono / `.NET Framework 4.7.2` / C# 9。编译引用一律指向游戏当前 `Managed\` 目录。
2. **正式版零权限优先**：
   - `ModLoader` 的权限扫描基于 **IL 调用点**——只要编译进 DLL 的方法体里出现了 `File.ReadAllBytes`、`HttpListener`、`Assembly.Load` 或对外部类型的反射调用，即使分支永不执行也会被标出对应能力。
   - 零能力模组须保留自闭合 `<Capabilities/>`（见 `assets/manifest.zero-cap.xml`），更新时可 `trust=Updated` 自动生效无需玩家反复人工授权。
   - 访问原版私有静态字段时优先用游戏官方 **`ModReflect.GetStatic<T>(typeof(Target), "field")`**（零能力，不触发 `reflection-external`）。
3. **禁止过度抽象与静默吞错**：
   - 优先使用 `ModHook` 与 `ModHelper` 公开 API；必须改原版时先写 Harmony `Prefix/Postfix`，尽量不写 IL `Transpiler`。
   - 严禁在内部逻辑里写空 `catch { }` 吞异常；只在 Unity 帧回调与 Harmony 边界处捕获并打 `Debug.LogError` 保留堆栈。
4. **扩展优先，替换最后（2026-11 二轮蒸馏新增铁律）**：
   - 原版 `ModHelper` 有 **144 个 public static 扩展点**（掉落表、关系网节点、开局剧本/特性、难度、鉴定品类、夜晚地点/事件、交互纸张、音效、ModData、跨模组消息总线、协程/主线程…）。**先在这 144 个里找口子，找不到才考虑 Harmony**。
   - **绝不整段替换原版入口方法**（`RunNightlyChemicalSimulation`、`DecodeSaveItem`、`EndNight`、`SaveGame` 类）。工坊实测：3 个模组同时整段替换化学模拟，装两个就只有一个生效。
   - 能挂 `ModHook` 就不要 Patch 原版方法；要改返回值必须有"非拦不可"的一句话理由。
   - 强制顺序要显式：`[HarmonyPriority]` / `[HarmonyBefore("mod.<id>")]` / `[HarmonyAfter("mod.<id>")]`（`ModLoader` 给的 Harmony 实例 ID 是 `"mod." + manifest.ID`）。
   - 跨模组协作**不要引用别人的 DLL**，用原版 `ModHelper.Expose/IsModRunning/SendMessage/OnMessage`，或"约定 GameObject 名 + `BroadcastMessage` + 带版本的 `Dictionary<string,object>`"（Wild's Network 范式）。
5. **重载与生命周期铁律**：
   - Harmony Patch 命中重载方法（如 `DirectoryMaster.Item`、`PlayerStore.SaveGame`）必须显式传 `new Type[] { ... }`，否则 `AmbiguousMatchException` 会中断整批补丁。
   - 访问 `PlayerStore.Instance` 前必须先判 `PlayerStore.HasInstance`（直接读 `Instance` 会隐式创建半初始化对象）。
   - 静态 UI 与缓存引用必须在 `ModHook.OnGameLoadedInit/Normal`、`OnDisable` 与 `HotReloadPrepare/Activate` 中成对清理。

## 3. 物品、真伪与容器四大铁律

1. **判存在不用 Spawn**：判断物品 ID 是否注册用 `DirectoryMaster.Has<GameItem>(id)` 并排除 `ItemSpawnerHiddenList.IsHidden(id)`；绝不用 `ItemSpawner.Spawn(id)` 试探（无效 ID 也会返回空壳）。
2. **三入口语义分明**：
   - `DirectoryMaster.Item(id, true)`：发给玩家（设 owned + uniqueId + 剥离工厂预装子物品）；
   - `DirectoryMaster.Item(id, false)`：发给 NPC；
   - `ItemSpawner.Spawn(id)`：仅做只读探针测量，用完必须 `try { ... } finally { item.Destroy(); }`，否则残留幽灵 UI。
3. **精确投放与引用比对**：`GraphUtils.EmporiumTryAdd` 会自动兜底塞进背包。要精确放进机器槽或指定容器，用 `GraphUtils.CanAccept(inv, item)` + `GraphUtils.TryAccept(inv, item)`，校验落点用引用比较 `item.parentInventory == targetInv`（绝不用 `!= null`）。
4. **真伪与品类**：真伪判定一律以 `ItemFeature.realCondition.identifier` 为准（绝不读显示名或 `fakeCondition`）；自定义品类用 `EnableTag`，绝不向 `SetGameItemType` 写入原版没有的类型（否则刷 `[Translation Error]`）。

## 4. 标准开发闭环

```text
1. 选架构（优先 ModHook/ModHelper/JSON Paper，再考虑 Harmony）
   → 参考 assets/ModEntry.template.cs 写清晰入口（建议预留 HotReloadPrepare / HotReloadActivate）
2. 编译并执行真实 ModScanner 能力扫描
   → 确认正式版 Refused=False 且 Capabilities 为空
3. 离线行为测试（测定价/规则/数据不变量，不写绑定源码字面量的假断言）
4. 真机闭环验证（严禁触碰玩家主档 Slot 0，只在测试槽位操作）
   → 通过本地开发桥接器（ModDevBridgeNext）读运行态/UI树/热重载/快进机器周期，确认画面与日志均正常
```

## 5. 脚手架与工具速用

- 透视任意已反编译模组或工坊 DLL：
  ```bash
  python scripts/inspect_mod.py "<反编译目录或DLL路径>"
  ```
- 跨模组冲突扫描（提交前门禁；退出码 2=ERROR / 3=WARN）：
  ```bash
  python scripts/conflict_scan.py --dir <我们源码目录> --dir <工坊模组反编译目录> [--out report.md]
  ```
- 批量反编译一批工坊模组 + 出透视报告：
  ```bash
  python 工具库/workshop-tools/distill_prep.py --ids-file <id清单.txt>
  ```
- 工坊模组抓取 / 简介筛选 / 订阅下载 / 反编译 / 取消订阅的完整流水线：见 `references/workshop-reverse-and-distill.md`
- 零能力清单起点：`assets/manifest.zero-cap.xml`
- 标准模组入口起点：`assets/ModEntry.template.cs`

## 6. 原版扩展点优先于 Harmony（2026-11 二轮蒸馏的核心认知）

原版 `ModHelper` 有 **144 个 public static 方法**，覆盖掉落表、关系网节点、开局剧本/特性、难度、鉴定品类、夜晚外出地点与事件、交互纸张、音效、ModData 持久化、跨模组消息总线（`Expose/IsModRunning/SendMessage/OnMessage`）、协程与主线程调度、机器与物品搬运。`GameItemFunc` 另有 **51 个委托属性**（拖拽三组九个 target 委托、容器准入、堆叠、槽位数量与优先级、父子形状/贴图/状态修饰、自定义 tooltip 富文本、每秒 tick、读档回调），`ModHook` 有 **37 个钩子**。**先在这三处找口子**。完整清单（分组、已核对签名）在
`共享开发\模组制作经验库\03-原版接口速查.md`。
