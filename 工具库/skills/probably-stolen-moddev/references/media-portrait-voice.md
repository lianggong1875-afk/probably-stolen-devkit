# 立绘引擎集成、高清去像素化、道具切图与配音挂载

提炼自 `PortraitStudio`（152 文件）、`PortraitStudio.AuthorKit`（立绘 SDK / 道具切图 / 配音 SDK）、`Vanilla Machine Animations`（机器雪碧图动画）与 `Better Newspapers`（报纸事件配图与版面编排）。

## 1. 让独立角色被 Portrait Studio 识别（且不交出控制权）

Portrait Studio 对模组角色有三层发现：
1. **Mono.Cecil IL 扫描（推荐，零能力）**：引擎遍历已启用模组 DLL 的 IL，找 `stfld StoreClient::spriteName` 且紧邻上一条指令是 `ldstr` 的字面量，自动归属到本模组。
   - **做法**：加一个永不调用的静态方法，列出每个自定义立绘/差分 key 的朴素字面量赋值 `client.spriteName = "my_key";`。
   - **禁区**：绝不要“优化”成数组循环或查表，否则 IL 丢失 `ldstr + stfld` 配对。
   - **不影响自带表情差分**：只要模组不注册 `ModHelper.OnMessage(id, "PortraitStudio:static-v1", ...)` 与 `ModHelper.Expose(id, "portrait-studio-static-active/1", ...)`，Studio 就拿不到写图权，只做只读目录归属。
2. **特性式补丁发现**：`[HarmonyPatch(typeof(SpriteDict), "GetSprite")]` 会被扫入 `DirectSpritePatchIds`（手写 `harmony.Patch(...)` 扫不到），仅在确实需要该缝时使用。
3. **协议接入（static-v1 / managed / dynamic）**：仅当确认「要让玩家用 Portrait Studio 替换我们 NPC 立绘」时才编入对应 SDK；独立立绘包读磁盘图必然带 `files` 能力。

## 2. 跨模组零反射高清去像素化租约

关闭 `EmporiumEntry.Instance.pixelPerfectCamera` 时不要直接写 `enabled = false/true`，否则多模组卸载会打架。采用场景树共享租约：
- 在 `pixelPerfectCamera.transform` 下找共享节点 `"PortraitStudio.AuthorHD.Leases"`；
- 若相机本来就是关的且无共享节点，不介入；
- 开启高清时在共享节点下挂一个自己的子 `GameObject` token，每帧维持 `camera.enabled = false`（防切焦点重置）；
- 释放时销毁 token；当 `root.childCount == 0` 时，先改名 `root.gameObject.name = LeaseRoot + ".released"` 再销毁（防同帧其他模组拿到将死节点），然后调 `EmporiumOption.Instance.ApplyPixelPerfectCamera()` 还原玩家真实画质设置。

## 3. 高清道具图标三分离与防脏边

- **贴图**：像素风素材在 `ModHook.OnModAssetsLoaded` 里把 `mod.Sprites` / `mod.PaperSprites` 设为 `FilterMode.Point`；高清非像素图保留原分辨率并可开 Mipmap + `FilterMode.Trilinear`。
- **显示 PPU 公式**（参考 PPU = 100，每格 16px）：
  `PPU = 100f * Mathf.Max(texWidth / (gridCols * 16f), texHeight / (gridRows * 16f))`
  绝不要设 PPU = 1（会导致 `SetNativeSize` 放大 100 倍）。
- **边缘防白边/绿边**：对 `alpha == 0` 的透明像素，用最近可见像素的 RGB 扩散填充（保持 `alpha = 0` 不动）；从白底母图裁切时先做 1.45px 边缘抗锯齿反混色，并同时在浅底 `#f2eee6` 与深底 `#202730` 双底验收。
- **GPU 无损导出不可读图集**：`RenderTexture.GetTemporary` → `Graphics.Blit` → `Texture2D.ReadPixels(sprite.textureRect)` → `EncodeToPNG` → 立即释放 `RenderTexture` 与临时 `Texture2D`。

## 4. 零第三方依赖的配音包挂载（AuthorVoice 范式）

1. **纯 C# WAV 解码**：直接解析 RIFF PCM16（1~2 声道、8~96 kHz、≤120s、≤32 MiB），在 `ModHelper.RunInBackground` 线程中解码成 `float[]`（每 4096 样本轮询 `CancellationToken`），主线程用 `AudioClip.Create` + `SetData` 播放。
2. **台词 Key 溯源（突破纯文本丢失与字符串驻留）**：
   - Postfix 挂 `LocHelper.GetLocalizedDialogue` / `GetLocalizedDialoguePhone` / `GetLocalized` / `GetLocalizedOrDefault`，用 `result = new string(result.ToCharArray())` 制造独立对象身份，登记进 `ConditionalWeakTable<string, Origin>`；
   - Postfix 挂 `Dialogue.SetText(string, string)`，把溯源接力到 `ConditionalWeakTable<Dialogue, Origin>`；
   - 通过 `ModHelper.Expose(modId, "voice.origin/1", Func<string, string>)` 允许多个配音包接力溯源；
   - 监听 `DialogUIManager` 与 `DialoguePhoneUIManager` 的 `DisplayClientText`/`DisplayerPlayerText`/`DisplaySecText`/`PlayClientSingleText` 播放，并在 `OnAdvancePressed`/`EndConversation`/`HideAllDialogUI`/`InterruptDialog` Prefix 立即停播，绝不干扰原版对话流。

## 5. 机器运行动画与 8 列雪碧图规范（Vanilla Machine Animations 范式）

1. **美术雪碧图规范**：
   - 将动画帧合并为单张 8 列网格 PNG 放入 `Textures/Items/<asset>__frames.png`（宽 = `frameW * 8`，高 = `frameH * ceil(frames / 8)`），保留 RGBA 透明背景与 `FilterMode.Point`（可用 `pack-spritesheet.py` 打包/校验）。
   - Unity `Sprite.Create` 从左上角第 0 帧逐行切图：`x = (i % 8) * w`，`y = sheet.height - (i / 8 + 1) * h`，对齐静态原图归一化 `pivot = (vanilla.pivot.x / w, vanilla.pivot.y / h)` 与 `vanilla.pixelsPerUnit`。
   - 用毫秒级延迟数组 `int[] delays` + 前缀和 `Array.BinarySearch` 映射 `Time.unscaledTimeAsDouble`，支持非均匀帧率且不随帧率漂移；在 `OnDisable`/`HotReloadPrepare` 显式 `Destroy` 切出的 `Sprite`。
2. **零存档污染与防冲突**：
   - Postfix 挂 `[HarmonyPatch(typeof(GameItemElement), "ValidateSelectLocked", new Type[] { typeof(bool) })]` 追踪实体元素，**绝不调用 `SetSpriteQuick` 改写 `GameItem.spritePath`**，只替换 `background.image.sprite` 与 `fakeBackground.image.sprite`（拖拽虚影）。
   - 播放期间暂设 `alphaHitTestMinimumThreshold = 0f` 防透明像素导致 tooltip 闪烁，停播或销毁时精准还原原图与阈值；若 `image.sprite` 被未知外部来源替换（非 `Vanilla`/`PreviousVanilla`/自建帧），立即让位。
3. **供电与状态判定铁律**：
   - 白天拔走电池不会立刻改写 `MACHINE_STATE_TAG`（仍保留 `"STATE_WORKING"`），所以不能仅凭状态标签判播；必须校验 `!item.IsTag("BROKEN_MACHINE_TAG") && MachineHelper.CanPower(item)`，并采用 150ms 节流轮询 + 槽位插拔回调即时清零缓存。
   - 若热重载或读档后场上已有机器未再次走 `ValidateSelectLocked`，在 `HotReloadActivate`/`OnGameLoadedNormal` 扫描 `FindAllItem()` 补调 `Track()`。

## 6. 报纸事件配图与 Better Newspapers 兼容（`Better Newspapers` 范式）

1. **9 色复古油墨与 2×2 半色调美术规范**：
   - 放在模组根目录 `Textures/*.png`，尺寸 `512×512`（由 `128×128` 像素网格 4× 最近邻放大，PNG-P 索引色），`FilterMode.Point`、`mipMapBias = -3f`。
   - 色板固定使用 9 阶泛黄新闻纸油墨（`#383631` ~ `#F6F2CF`）与 2×2 拜耳矩阵 `[32, 160, 224, 96]`（可用 `newsprint-tone.py` 一键转换）。
   - 相框使用 `40×40` 九宫格像素边框（`Vector4(14,14,14,14)` slice、`6px` inset、`RawImage` + `CenterCrop` 等比裁切）。
2. **双模组共存排版铁律**：
   - `ModLoader` 分配给模组的 Harmony ID 是 **`"mod." + manifest.ID`**。必须在 `NewsUIManager.PopulateUI` Postfix 写 `[HarmonyAfter("mod.local.newspaper.illustrations")]` + `[HarmonyPriority(Priority.Last)]`，才能在 Better Newspapers 重排之后读取真实坐标。
   - 遍历 `layout1Elements..layout4Elements`（而非只查 `allElements`），调用 `Canvas.ForceUpdateCanvases()` + `ForceMeshUpdate(true, true)` 测算最后一行 `descender`。
   - 避让 Better Newspapers 的 `"Newspaper footer"` / `"Newspaper page navigation"` / `"柜台笑谈"` / `"街头剪贴"` / `"Column rule"` 与同栏下方标题，若有激活态 `"Printed photograph"` 则不重复贴图。
