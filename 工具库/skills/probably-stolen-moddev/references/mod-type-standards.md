# 各类模组制作规范（速查版）

**什么时候读**：动手写任何一种模组之前，先按类型对号入座。完整版见 `共享开发/模组制作经验库/20-各类模组制作规范.md`。

---

## 0. 十二条跨类型铁律

1. **只写自己的键**：物品 Tag 用 `<模组短名>_<用途>`，ModData 键用 `<modid>.<用途>`，绝不覆写原版或他模组键。
2. **判注册用 `DirectoryMaster.Has<GameItem>(id)`**，绝不用 `ItemSpawner.Spawn` 试探；同名冲突保留先到者 + Warn。
3. **Patch 优先 Postfix 追加**，拦截返回值必须有一句话理由写进注释。
4. **重载方法必须写显式 `new Type[] { ... }`**，避免 `AmbiguousMatchException` 中断整批补丁。
5. **不整段替换原版入口**（`RunNightlyChemicalSimulation` / `DecodeSaveItem` / `EndNight` / `SaveGame`）。
6. **强制顺序显式声明**：`[HarmonyPriority]` / `[HarmonyBefore("mod.<id>")]` / `[HarmonyAfter("mod.<id>")]`。
7. **能力最小化**：主体零能力（`<Capabilities/>`），文件直读需求切 companion 模组；`ModHelper.*ModDataFile*` 属于白名单零能力。
8. **异常：对内暴露、边界捕获**，禁止内部空 `catch {}`，边界捕获必打 `Debug.LogError` 留堆栈。
9. **静态状态三处成对重置**：`OnDisable` / 读档（`OnGameLoadedInit/Normal`）/ 热重载（`HotReloadPrepare/Activate`）。
10. **自检进日志**：引用型数据（前置、配方、剧情 id、容器绑定）在 `OnGameLoadedLate` 做存在性与环检测并 Warn。
11. **原版扩展面优先于 Harmony**：先查 `ModHelper`（**145 方法**）、`GameItemFunc`（**51 委托**）、`ModHook`（**44 事件**），找不到口子再写 Patch。
12. **⛔ 绝不碰模组加载器状态**：禁用带 `[ModRestricted("loader-target")]` 的 `ModLoader.SetModEnabled/ReorderMods/ApproveAndEnable`、`ModTrust.Approve` 以及 `ES3.Save("modOrder"/"modTrust", ...)`（编译即 `Refused`）；只读 `ModLoader.Mods` / `IsModEnabled` / `TrustState` / `RestartRequired`。

## 1. 内容扩展（物品 / 机器 / 配方）

- 机器无需 `contentWindow`：`SetGameItemType("MACHINE")` + `onActivateSlotItemFunc` 开 `CustomUIManager.CreateWindow`。
- 占格用 `SetSpriteAndShapeFromMod(modId, key)` + `ValidateShapeState()`（镂空图标注意区域采样防穿模）。
- 精确投放：`GraphUtils.CanAccept` → `TryAccept*` → 引用比较 `item.parentInventory == targetInv`。
- 读档恢复：挂 `item.onLoaded`，按原版分工（数据 `MachineHelper.LoadUpgrade`，显示 `SpriteHelper.UpdateWaterContainerSprite`）。
- 真伪只认 `ItemFeature.realCondition.identifier`；自定义品类只用 `EnableTag`。
- 禁忌：切原版图集后不清 `useSpriteFromMod`（→ 白底问号）；启动早期读并缓存 `LocHelper.Get`（→ `invalid operation handle`）。

## 2. QoL / UI / 整理排序

- 快捷键用 `ModHelper.AddGameOptionKeyBind`；面板用 `CustomUIManager.CreateWindow` + `CustomUIBuilder`；模态窗加 `ModalInputGate.cs` 防关窗穿透。
- **排序/整理防剧透红线**：严禁读取 `realCondition` 或内部真实价值（否则直接泄露假货，61 万订阅 Smart Inventory Arrange 铁律）。
- 禁忌：全局拦 `Input` / `EventSystem`；在 `Update` 里无节流重算（至少 0.12~0.25s）；无优先级覆写全局显示层（`GameItem.GetDisplayName` 等）。

## 3. 性能优化

- 先用 `Stopwatch` 量出热点再动刀，必须留前后数字。
- 缓存三要素：键含全部外部状态（事件/加价/纯度/日期）+ `ConditionalWeakTable` 弱表防泄漏 + 全局 epoch 代际失效 + 可一键关闭（模板 `EpochCache.cs`）。
- 批处理延迟用 `Prefix 开标志 / Finalizer 关标志`（Postfix 在异常路径不执行）。

## 4. 事件 / 剧情 / 顾客 / 经济

- 事件用 `StoreEventActionDict` + `ActivateEvent` + `negociationDatas`（零改价）+ `Es3SafePersistence.cs` 防坏档；过夜介入挂 `OnHandlingNightlyServicesEarly/Late`。
- 经济系统优先**影子账本**（Exchange 范式：不 Patch `GetCurrentValue`，历史 K 线存日种子回放），慎改原版存档链。
- 顾客用 `OnGenerateCustomerVeryEarly/VeryLate` + `ModHelper.SetClientSource`；长对话用 `startAction` 惰性翻页；剧情文本外置。

## 5. 框架 / 互操作 / 工具

- 零硬依赖通信：`ModHelper.Expose/IsModRunning/SendMessage/OnMessage`，或约定 GameObject + `BroadcastMessage` + 带版本号字典。
- 调试入口走 `CustomUIDebugPanel.Add("Modding", ...)`（只认 14 个原版固定分类），跨线程用 `RunOnMainThread` + `Delay` + generation 过期抑制；外部通道优先零能力 `ModDataInbox.cs`。
- 禁忌：`Assembly.Load` 他人 DLL（触发 `dynamic-code`）；`Process.Start`（触发 `process`）；内嵌 HTTP 零鉴权或 `CORS *`。

## 6. 立绘 / 美术 / 音频

- **立绘硬规格**：`1728 × 1266` / `pixelsPerUnit = 600` / `pivot = (0.5, 0.4976)` / `FilterMode.Point` / `Clamp` / 无 mipmap / sRGB / FullRect；透明区必须做边缘色扩散消绿边（`alpha==0` 不留绿幕 RGB）。
- **换脸不改字典**：只换 `SpriteRenderer.sprite` 引用，替换前扫 `ModLoader.Mods` 避让，还原写成条件式 `if (renderer.sprite == mine)`。
- 批量包带作者前缀避免撞名；CJK 保证 `TMP_Settings.fallbackFontAssets` 回退链。

## 7. 存档 / 读档增强（高风险）

- 能不写就不写；只写私有键；接管读档必须实现失败回退原版（宁卡不坏档）；改写前备份到 ModData，坏数据只读锁死。

## 8. 合并前门禁与冲突红线

```text
[ ] .\工具库\build\Invoke-PreMergeGate.ps1 -Mod <Arcade|Freya|All> 五道全绿
    （开发版编译 / -PublicRelease -Scan 零能力 / 离线回归 / conflict_scan --gate ERROR=0 / 工作区洁净）
[ ] 涉及知识库或技能改动：python 工具库\kb-tools\kb_pipeline.py build && audit 零 ERROR
[ ] 真机 Slot 1：进档 → 用功能 → 存档 → 退出 → 读档 → 再验（绝不碰 Slot 0）
[ ] 日志无 [Translation Error] / invalid operation handle / NullReferenceException
```

- **红色硬冲突靶点**：多个模组同时 Patch 全局物品显示层（`GameItem.GetDisplayName` 等）或本地化/字体层（`LocHelper.SetLocale`/`FontLoader`/`TMP_Settings`）。**绝不同时重改存档链与全局显示层**。
- **Appraisal Assistant 5 红线方法**：`AppraisalHelper.GetCorrectBaseValue`、`GameItem.IsTag`、`AppHandHelper|RibwichHelper|PlasmaHelper.GetCorrectValue`、`AppraisalItemType.Matches`、`AppraisalItemRegistry.Find`。
