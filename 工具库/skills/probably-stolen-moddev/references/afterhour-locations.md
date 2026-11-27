# 夜晚外出区域与探索扩展专题

本参考由原版 `AfterhourLocation` / `MapUIManager` / `TransitUI` / `AfterhourEvents` / `ModHelper` 源码，以及工坊模组《樱咲羽衣 · 下层区诊所》（`SakurazakiUi`）、《食品券扩展与增强》（`CommissaryExpansion`）、《Pen 的便利性优化》（`Pen_mod_QoL_Tweaks`）反编译蒸馏而成。

## 1. 四种扩展模式选择

1. **极简按钮菜单（最省事）**：直接调用 `ModHelper.AddAfterhourLocation(modId, id, title, onArrive, onLeave, buildPanel, imageKey, backgroundKey, ambienceId, isDiscovered, isAvailable)`，原版自动套 `"afterhour_location"` 窗口并附带「返回」按钮。
2. **博士式夜晚商店 / 谈判摊位**：继承 `AfterhourLocation`（或配置 `DelegateAfterhourLocation`）：
   - `ShowsStoreCounters => true`，并实现 `GetFrontCounterPosition(w, h)` 与 `GetBackCounterPosition(w, h)`；
   - `Arrive()` 中展示 `frontInvWindow`/`backInvWindow`，`MapUIManager.Instance.TransferBackInvToCounter()`，摆出 `isOwend: false` 的货，实例化并激活外部 `StoreClient`（参考 `DrJacksonLocation.Arrive`）；
   - **`Leave()` 生死铁律**：调用 `ResetFrontCounterGrid()` / `ClearFrontInv()` / `ClearBackInv()` **之前**，必须先遍历前后柜台将所有 `GeneralHelper.IsItemOwned(item)` 物品 `CloseContentWindow(true)` + `Expel` 暂存，清台并 `TransferCounterToBackInv()` 后再 `EmporiumTryAdd(backInvWindow, item)` 塞回后仓。
3. **全屏 CG / 视觉小说打工（《樱咲羽衣》模式）**：
   - 继承 `AfterhourLocation`，`UsesAfterhourBag => false`；
   - `StageObject` 自持 `GameObject`（含背景 `SpriteRenderer` `sortingOrder=20` 与 CG `SpriteRenderer` `sortingOrder=22`），在 `Tick()` 中按 `Camera.main.orthographicSize * 2f` 与 `aspect` 计算 `Aspect Fill`（背景）与 `Aspect Fit`（CG）；
   - `BuildPanel` 禁用原版根节点 `LayoutGroup`/`Graphic`/`Mask` 并隐藏其余原生子物体，在自建 `surface` 上做底部对话框；
   - 回店发奖不在 `Leave()` 里塞库，而是在存档中落账，于 `ModHook.OnAfterhourLeaveLate` / `OnGameLoadedLate` / `OnWakingUpLate` 校验 `PlayerLocation.IN_STORE` 后幂等投递并核对实际入库数量。
4. **扩展已有夜晚地点与地图事件**：
   - `ModHelper.AddAfterhourEvent(...)` 往已有或新增地点挂星期几/随机事件、状态效果 `AfterhourEffect` 或 `closesLocation = true` 闭店锁；并用 `ModHelper.GetAfterhourModifier(locationId, key)` 取叠加修正；
   - 扩展站台卡片说明：Postfix `TransitUI.BuildEventList(RectTransform, AfterhourLocation, float)`，往返回 `ScrollRect.content` 挂载带 `LayoutElement` 且 `raycastTarget = false` 的子控件。

## 2. 资源加载与零能力铁律

- **`ImageKey` 与 `BackgroundKey` 必须写 `"<modId>:<spriteKey>"`**（否则 `CustomUIManager.GetSprite` 不查模组贴图），且**必须同时把 Sprite 写入 `mod.Sprites[key]`**（`GetModSprite` 不查 `mod.Textures`！放在 `Textures/` 根目录的大背景不会自动进 `mod.Sprites`，需在 `OnModAssetsLoaded` 手动 `Sprite.Create` 并赋值）。
- **零能力可读外置文案与存盘**：
  - 读多语言台词/配置：`ModHelper.ReadModFileText(modId, "Dialogue/night.zh-CN.txt")`（白名单沙箱读取，不触发 `files` 权限）；
  - 存夜晚状态/每日次数：`ModHelper.GetModData` / `SetModData` 与 `PlayerStore.Instance.ModData()`（不污染 ES3 存档结构）；
  - 夜晚环境音：`ModHelper.RegisterAfterhourAmbience(modId, ambienceId, soundKey)`（在 `OnModAssetsLoaded` 后注册）。

## 3. 美术资产推荐规格

| 资源 | 推荐分辨率 | 存放与过滤设置 | 说明 |
| --- | --- | --- | --- |
| 地图卡片缩略图 / 全屏底图 | `16:9`（如 `1672×941` 或 `1280×720`） | `Textures/<key>.png` + 手动进 `mod.Sprites` | 站台卡片槽 `296×168`（`16:9` Aspect Fill），可与底图复用；插画用 `Bilinear`，像素风用 `mip 0` 重建 + `Point` |
| 夜晚事件 / 打工 CG | `16:9`（如 `1672×941`） | `Textures/<key>.png` | `sortingOrder = 22`，显示时把底层 `room.color` 染成 `Color.black` 当黑边 |
| 关联 NPC 立绘与换装差分 | `~1466×1073`（或等比高分） | `Textures/`（像素风须去 Mipmap + `Point`） | `ppu = tex.height / 1.54f`（或 `rect.height * 100f / 175f`），`pivot = (0.5f, 0.657f)` |
