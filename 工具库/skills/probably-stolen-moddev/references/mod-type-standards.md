# 各类模组制作规范（速查版）

**什么时候读**：动手写任何一种模组之前，先来这里按类型对号入座。完整版（含更多细节与验收清单）：
`共享开发\模组制作经验库\20-各类模组制作规范.md`。

---

## 0. 十条规定（跨所有类型）

1. **只写自己的键**：物品 Tag 用 `<模组短名>_<用途>`，ModData 键用 `<modid>.<用途>`。
2. **判注册用 `DirectoryMaster.Has<GameItem>(id)`**，绝不用 `ItemSpawner.Spawn` 试探；同名冲突保留先到者 + Warn。
3. **Patch 只 Postfix 追加**，改返回值必须有一句话理由。
4. **重载方法必须写显式 `new Type[]{...}`**。
5. **不整段替换原版入口**（过夜/读档/存档/化学模拟）。
6. **强制顺序显式声明**：`[HarmonyPriority]` / `[HarmonyBefore("mod.<id>")]` / `[HarmonyAfter("mod.<id>")]`。
7. **能力最小化**：主体零能力，需要文件读写的切 companion 模组。
8. **异常：对内暴露、边界捕获**，禁止空 `catch {}`。
9. **静态状态三处成对重置**：`OnDisable` / 读档 / 热重载。
10. **自检进日志**：引用型数据（前置、配方、剧情 id、容器绑定）在 `OnGameLoadedLate` 做完整性 + 环检测并 Warn。
11. **开工前先翻原版口子，最后才写 Harmony**：`ModHelper` **144 个** public static 方法、`GameItemFunc` **51 个**委托属性（拖拽 target 三组九个、容器准入、堆叠、槽位数量/优先级、父子形状/贴图/状态修饰、自定义 tooltip 富文本、每秒 tick、读档回调）、`ModHook` **37 个**钩子。三处都找不到再 Patch。工坊实证：零 Patch 能做出带 UI 的机器、剧本引擎、流水线控制器。
12. **⛔ 绝不碰模组加载器状态**：不调 `ModLoader.SetModEnabled/ReorderMods/ApproveAndEnable`、`ModTrust.Approve`（带 `[ModRestricted("loader-target")]`，**编译即 Refused、运行即抛异常**）；连 `ES3.Save("modOrder", …)` 字面量调用也会被 ModScanner 硬拒。只读 `ModLoader.Mods` / `IsModEnabled` / `TrustState` / `RestartRequired`。别用 `new string(char[])` 拼键名规避扫描。

## 1. 内容扩展（物品 / 机器 / 配方）

- 机器不需要 `contentWindow`：`SetGameItemType("MACHINE")` + `onActivateSlotItemFunc` 开自定义窗口。
- 占格别手写数字：`SetSpriteAndShapeFromMod(modId, key)` + `ValidateShapeState()`。
- 精确投放：`GraphUtils.CanAccept` 预检 → `TryAccept*`（返回未放入量）→ 引用比较 `item.parentInventory == targetInv`。
- 读档恢复：挂 `item.onLoaded`，或按原版分工（数据 `MachineHelper.LoadUpgrade`，显示 `SpriteHelper.UpdateWaterContainerSprite`）。
- 真伪只认 `ItemFeature.realCondition.identifier`；品类只 `EnableTag`。
- 禁忌：换图后不清 `useSpriteFromMod`（→ 白底问号）；启动早期读 `LocHelper.Get` 并缓存（→ `invalid operation handle`）。

## 2. QoL / UI

- 快捷键用 `ModHelper.AddGameOptionKeyBind`，别自己轮询 `Input.GetKeyDown`。
- 面板走 `CustomUIManager.CreateWindow` + `CustomUIBuilder`；不要手搓 `PixelWindow`（除非接管原版窗口）。
- 挂载点优先 `OnCreateTooltipLate` / `OnGameLoadedLate` / `OnShutterOpened*`。
- 禁忌：全局拦 Input / EventSystem；在 `Update` 里做重活；抢全局显示层（`GameItem.get_name` 等）。

## 3. 性能优化

- 先量后改（Stopwatch + 分阶段日志），必须给前后数字。
- 缓存三要素：①键含所有外部状态（事件/加价/纯度/日期）②失效用**代际计数**③可一键关闭。
- 缓存容器用 `ConditionalWeakTable`，加容量上限自愈；读写限主线程。
- 批处理延迟：`Prefix 打开标志 / **Finalizer** 关闭标志`（Postfix 在异常路径不执行），flush 按依赖顺序。
- 禁忌：整段替换原版入口；`Update` 里全场景扫描；缓存里存 `GameItem` 强引用。

## 4. 事件 / 剧情 / 顾客

- 事件走 `StoreEventManager`；过夜介入挂 `OnHandlingNightlyServicesEarly/Late`。
- 顾客用 `OnGenerateCustomerVeryEarly/VeryLate` + `ModHelper.SetClientSource` 声明归属。
- 长对话用"惰性翻页"（每页显示时才求下一步），别一次塞满 UI。
- 剧情数据外置（脚本/JSON），C# 只放解释器。
- 禁忌：生成流程里同步做重活（顾客会"消失"）；不判 `PlayerStore.HasInstance` 就用 `Instance`。

## 5. 框架 / 互操作

- 零硬依赖：原版 `ModHelper.Expose/IsModRunning/SendMessage/OnMessage`，或"约定 GameObject 名 + `BroadcastMessage` + 带版本的 `Dictionary<string,object>`"。
- 软前置缺失只提示不阻塞；同名 id 先到者赢；协议必带版本号。
- 框架自带 `SelfCheck`（孤立节点 / 缺失前置 / 循环前置 DFS 三色 / 重复注册）。
- 禁忌：`Assembly.Load` 别人的 dll（触发 `dynamic-code`）；Prefix 返回 false 抢别人的执行。

## 6. 工具 / 调试

- 入口用 `AddDebugFunction`/`RemoveDebugFunctions`；跨线程用 `RunOnMainThread` + `Delay` + generation 计数器做过期抑制。
- 日志写自己 ModData 目录；数据目录里自动生成 `说明.txt`。
- 禁忌：调试默认开启；`Process.Start`（`process` 能力 = 正式版红线）。

## 7. 立绘 / 美术 / 音频

- 支持"目录 + 命名约定"的外部来源（丢图即生效，按修改时间重画）；短名=本体，同前缀长名=差分；文件夹名成为标签。
- 回退链永不空白（extreme → adult → serene）。
- CJK 文本必须保证字体回退链（`TMP_Settings.fallbackFontAssets` + `ForceMeshUpdate`）。
- 禁忌：`Resources.Load` 模组目录外资源；4K 原图直塞；覆盖原版图集。

## 8. 存档 / 读档增强（高风险）

- 能不写就不写；只写私有键；`item.onLoaded` 恢复自定义数据。
- 接管读档必须实现"出错回退原版"（宁可卡不能坏档）。
- 改写前备份到 ModData，失败回滚；自写持久化结构必须版本化 + 严格全量校验，坏数据只读锁死。
- 禁忌：改原版 ES3 键；写入路径上非幂等操作；用 try/catch 吞解码异常。

## 9. 提交前自查

```text
[ ] Build-Mod.ps1 编译通过
[ ] -PublicRelease -Scan：Refused=False 且 Capabilities 为空
[ ] Run-Regression.ps1 全绿
[ ] conflict_scan.py 无 ERROR（--dir 我们源码 --dir 已装模组反编译目录）
[ ] 真机 Slot 1：进档 → 用功能 → 存档 → 退出 → 读档 → 再验
[ ] 与 3 个以上大模组同开跑一轮
[ ] 日志无 [Translation Error] / invalid operation handle / 空引用
```

## 10. 冲突红线（工坊冲突检测器的实证）

- **只有两类算"红色硬冲突"**：多个模组同时 Patch 全局**物品显示层**（`GameItem.get_name` / `get_shortDescription` / `GetDisplayName`）或**本地化层**（`LocHelper.SetLocale` / `FontLoader` / `TMP_Settings`）。
- 单个模组 Patch **存档链**（`PlayerStore` 存读链）会被放大自风险：**绝不能既改存档链又改全局显示层**。
- 该检测器**不查** identifier 重复、语言 key 覆盖、atlas 同名、manifest ID 重复 —— "没被标红" ≠ 没问题。
