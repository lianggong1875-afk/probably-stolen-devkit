# 高级模组编程架构与工程模式

提炼自 `Developer Tools`（249 文件）、`SDJYBN Framework`、`Station Events`、`ModDevBridgeNext` 与 `MCPBridge` 源码。

## 1. 按规模选架构，拒绝套娃

- **玩法/内容模组**：`IMod` 入口 + 静态内容表 + 领域类 + 微型单职责 Patch。内容放表里，逻辑写一处。
- **轻量依赖库（SDJYBN 范式）**：用 `_isHost = manifest.ID == ModId` 区分宿主与被引用态；`Boot()` 幂等；功能开关采用「配置项 → 内存字典 → 默认 true」三级回退，异常永不误关核心服务。
- **大型工具模组（Developer Tools 范式）**：`Runner`（单帧驱动）→ `*Tools`（纯逻辑 `Read → ValidateRecord → ApplyCore`）→ `*Record`（不可变快照防并发漂移）→ `*Patch`（单行转发）→ `*Api`（外部扩展点）。

## 2. ES3 存档无污染防坏档（Station Events 范式）

只要模组向会被 Easy Save 3 序列化的原版容器（如 `StoreEventManager`）写入自定义对象，就必须做写盘剥离，防止玩家卸载模组后坏档：

1. `[HarmonyPatch(typeof(PlayerStore), "SaveGame", new Type[] { })]`：`Prefix` 把本模组自定义事件/状态采集进 `ModHelper.SetModData`；`Finalizer` 清理临时保存态。
2. `[HarmonyPatch(typeof(ES3Type), "WriteProperties")]`：`Prefix(ref object __0)` 命中 `StoreEventManager` 时，用 `MemberwiseClone` 浅克隆一份**过滤掉本模组前缀条目**的干净副本替换 `__0` 让 ES3 写盘。
3. 读档时在 `ModHook.OnGameLoadedEarly` 从 `ModData` 无损重建塞回管理器。

## 3. 大型游戏内模态 UI 的输入隔离（Developer Tools 范式）

弹出带输入框、搜索、滚动条的 `CustomUIWindow` 时，四步根治误触与点击穿透：

1. **延迟释放状态机（`ModalInputGate`）**：窗口开启或任意鼠标键按下时锁定 `captured = true` 并设 `releaseThrough = Time.frameCount + 1`；关窗时调 `OnClosing()`，多锁 1 帧且等全鼠标键抬起再交还输入，杜绝点关闭穿到背后物品。
2. **全屏遮罩 Backdrop**：在窗口同父节点下挂半透明 `Image (raycastTarget = true)`，每帧保持 `SiblingIndex == window.root.GetSiblingIndex() - 1`。
3. **开窗清焦点**：`EventSystem.current?.SetSelectedGameObject(null)` + `CustomEventHandler.ClearDoubleClickTarget()` + `InputActionManager.current.Reset()`。
4. **精准拦截点**：在 `Blocked` 时拦 `InputActionManager.Update`、`KeyBindHelper.GetKeyDown/GetBackDown`、`EventSystem.RaycastAll`、`CustomUIManager.HandleEscape`、`ScrollRect.OnScroll`、`TMP_InputField.OnScroll`。

## 4. 防掉帧与防丢物模式

- **分帧预算目录索引**：扫 `DirectoryMaster.GetIdentifierList<GameItem>()` 时每帧最多实例化 2 件且限制 `Time.realtimeSinceStartup - start <= 0.002f`（2ms 帧预算），每件用 `try { ... } finally { item?.Destroy(); }` 立刻销毁。
- **持久化回滚交付队列（SplitQueue 范式）**：产出多件物品时，先整体投放、任一落点失败（`parentInventory != targetInv`）即 `DestroyAll` 回滚并把源物品 `uniqueId + snapshot` 持久化到 `ModData` 排队；重试时跳过拖拽态，先核对快照未变再执行。
- **Tooltip 异常安全作用域**：若在 `GameItemElement.GetTooltipBasic` Prefix 临时改写了状态，把还原闭包存进 `out List<Action> __state`，在 `Postfix` **与 `Finalizer`** 双重还原。

## 5. 开发桥接、后台线程与热重载模式

- **内嵌 HTTP 安全边界**：强制 `IPAddress.IsLoopback && req.Url.Host == "127.0.0.1" && req.Headers["Origin"] == null`；后台线程走 `ModHelper.RunInBackground(token => ...)` 并在取消令牌触发时关闭监听与全部活动上下文。
- **主线程调度器**：区分单帧 `EnqueueAsync<T>` 与多帧 `EnqueueCoroutineAsync<T>`（互斥标记 `_isCoroutineRunning` 保证截图/拖拽串行执行），配合 `CancellationTokenSource.CancelAfter` 防死等。
- **世界代际守卫**：追踪 `PlayerStore.Instance` 与 `NewGameData.Instance.isInMainMenu`，变化即刷新 `WorldId`，作废旧对象句柄与测试会话；缓存游戏对象一律用 `WeakReference<T>` + Unity 假 null 判空（`u != null && u == null`）。
- **热重载五阶段事务**：模组入口暴露 `public void HotReloadPrepare()` 与 `public void HotReloadActivate()`，切换按 `old.prepare → old.disable → old.unpatch → next.patch → next.init → next.enable → next.activate → commit` 执行，失败按逆序精确补偿；每次重编候选 DLL 必须换**唯一程序集名**（同进程旧程序集常驻内存）。
- **资产热重载**：贴图原地 `texture.LoadImage(bytes)` + 清 `RenderHandler.atlasCache`；书页走 `PaperBookRegistry.RemoveMod/LoadMod`；战利品清 `LootRegistry.journal` 并重调 `ModLootFile.Load`；在场物品同步原型字段后调 `(item as GameItemElement)?.Validate()`。
