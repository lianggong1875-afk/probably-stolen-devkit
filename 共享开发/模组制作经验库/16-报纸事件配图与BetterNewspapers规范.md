# 16 报纸事件配图与 Better Newspapers 规范

**什么时候读它**：要给模组自己的空间站事件/新闻加上报纸配图、需要对齐游戏原生报纸与工坊《更好的报纸 / Better Newspapers》的视觉风格、或者需要与 Better Newspapers 并存不重叠排版时。

**来源**：反编译蒸馏工坊标杆模组 **更好的报纸：报纸事件配图 | Better Newspapers**（`local.newspaper.illustrations`，工坊 `3839534065`，v0.8.1，zero-cap 零能力），并结合《深空电玩》实机双模组并存截图验证。

---

## 一、美术与印刷风格规范（1:1 复刻 ClippingPress 引擎）

### 1. 图片尺寸与纹理参数
- **存放路径**：放在模组根目录 `Textures/*.png`（冷启动自动加载进 `mod.Textures[name]`）。
- **推荐尺寸**：`512 × 512` px（由 `128 × 128` 逻辑像素网格最近邻 `4×` 放大，索引色模式 `PNG-P`，单张仅 2–12 KB）。
- **Unity 加载参数**：
  ```csharp
  tex.filterMode = FilterMode.Point;
  if (tex.mipmapCount > 1) tex.mipMapBias = -3f;
  ```

### 2. 官方 9 色复古新闻纸油墨色板与 2×2 拜耳抖动
Better Newspapers 的核心视觉统一感来自 `ClippingPress.cs` 固定的 9 阶色板与 2×2 印刷网点矩阵：
- **9 阶油墨色板（RGB，从深炭黑到高光纸白）**：
  - `#383631` `(56, 54, 49)` — 最深轮廓与剪影
  - `#4B473E` `(75, 71, 62)` — 深灰褐阴影
  - `#6F6858` `(111, 104, 88)` — 中暗灰褐
  - `#837B69` `(131, 123, 105)` — 中灰褐
  - `#988F7B` `(152, 143, 123)` — 中性暖灰
  - `#B5AC90` `(181, 172, 144)` — 浅灰米
  - `#D0C8A7` `(208, 200, 167)` — 暖米白
  - `#EBE3BD` `(235, 227, 189)` — 高光纸黄
  - `#F6F2CF` `(246, 242, 207)` — 最亮纸白高光
- **2×2 拜耳半色调矩阵**：阈值表 `[32, 160, 224, 96]`（按 `(x & 1) + 2 * (y & 1)` 索引），在灰阶过渡区生成细腻像素印刷网点。
- **一键转换工具**：使用 `工具库\img\newsprint-tone.py` 可将任意高清无字原图一键转为完全一致的 9 色 512×512 版画 PNG。

### 3. 九宫格磨损像素相框
- 准备一张 `40 × 40` px 透明圆角相框（外 4px 深油墨 `#383631` 带随机 1–2px 印刷缺口，内 2px 暖米 `#D0C8A7`，中心白色留空）。
- 用 `Sprite.Create(tex, rect, (0.5, 0.5), 100f, 0, FullRect, new Vector4(14, 14, 14, 14))` 配合 `Image.Type.Sliced` 拉伸，`RawImage` 保持 `6px` 内边距（`offsetMin = (6,6)`, `offsetMax = (-6,-6)`），并通过 `CenterCrop` 按容器宽高比居中裁剪，保证任意栏宽下主体不变形。

---

## 二、代码实现与 Better Newspapers 共存规范

### 1. Better Newspapers 无第三方注册接口
反编译确认 `NewspaperIllustrations.dll` 只读自身 `mod.Textures` 与内部静态字典 `eventTags` / `eventVariants`，没有提供 `ModHelper.Expose` 跨模组注册入口。因此模组自己的报纸配图应独立挂在 `NewsUIManager.PopulateUI` 上，并做到**裸跑原版与装了 Better Newspapers 时均完美适配**。

### 2. 四条必守技术细节（实机踩坑得出）

1. **ModLoader 的真实 Harmony ID 是 `"mod." + manifest.ID`**：
   - `ModLoader.cs:1611` 使用 `new Harmony("mod." + manifest.ID)` 创建补丁实例；
   - 因此要排在 Better Newspapers（`local.newspaper.illustrations`）排版之后运行，特性必须写 **`[HarmonyAfter("mod.local.newspaper.illustrations")]`**（写 `"local.newspaper.illustrations"` 不会命中，导致我方 Postfix 先于对方执行、读到未重排前的旧尺寸并压住「柜台笑谈」与后续栏目标题！）。
2. **遍历 `layout1Elements..layout4Elements`，不能只看 `allElements`**：
   - `NewsUIManager` 实际激活填充的是 `layout1Elements` / `layout2Elements` / `layout3Elements` / `layout4Elements`（在部分场景 `allElements` 为空）。四个列表均为 `public`，合并去重遍历即可（零反射）。
3. **重排后必须强制刷新画布与文本网格**：
   - 进入测量前调用 `Canvas.ForceUpdateCanvases()`，对每张卡片的 `textContent` 调用 `tmp.ForceMeshUpdate(true, true)`，再遍历 `textInfo.lineInfo[i].descender`（仅统计 `characterCount > 0` 的可见行）取得真实文字底端。
4. **避让重排控件与缩高文本盒**：
   - Better Newspapers 会把无图新闻的 `textContent.rectTransform` 高度缩成恰好包住文字（导致 `rect.yMin` 贴在最后一行）。检测到存在 `"Newspaper footer"` / `"Newspaper page navigation"` / `"柜台笑谈"` / `"街头剪贴"` 时，底边应以页面底界（避开页脚 96px）和同栏下方激活标题、`柜台笑谈`、`街头剪贴`、`Column rule` 顶部为准，不死卡 `rect.yMin`；高度不足 `60px` 时宁可留白不硬塞。
   - 若同格已有 `"Printed photograph"`（激活态），直接让位不重叠。

可复用零能力模板见 `工具库/templates/mod-patterns/NewspaperArtHelper.cs`。
