# 《Probably Stolen（深空当铺）》模组开发避坑指南与标准架构手册

> **用途**：记录在本项目（`DeepSpacePawn` 角色模组与 `DeepSpaceArcade` 深空电玩模组）开发过程中踩过的所有引擎机制坑、UI 层级坑、美术规格坑、文案陷阱与测试环境铁律。后续任何主模型或子代理接手前**必须先通读本手册**，严禁自己瞎猜硬写、严禁不看画面只看编译通过！

---

## 一、最高开发铁律（违反任意一条直接返工）

1. **画面效果是唯一验收标准**：
   - 代码编译通过、`ok: true` 约等于零！每次涉及 UI、物品贴图、立绘、Paper 书册的改动，**必须在游戏内真机打开对应界面并用 `screenshot.ps1` 截图肉眼检查**！
   - 必须与原版同类界面同屏或并排对比（例如原版香烟/粮票鉴定 vs 模组物品鉴定），确认比例、层级、遮罩、字体、像素锐度一致。
2. **能套用原版/成熟模组绝不自造轮子**：
   - 动手写任何系统前，先查 `D:\0TavernWeave\probably-stolen\decomp\Assembly-CSharp\`（原版反编译库）以及 `decomp\AppraiserPlus\`、`decomp\AppraisalAssistant\`、`decomp\VanillaMachineAnimations\`。
   - 原版有的 UI 面板、按钮、对话流、音效、折扣词条（如 `InspectableHelper.OnConfrontSuccess`、`InspectionUIManager.OnConfrontFailed`），直接复用，严禁自己拉一个黑底白字塑料感 Canvas 硬盖在屏幕上！
3. **主从模型分工铁律**：
   - **主模型**负责架构设计、技术方案定型、反编译源码调研、画面审美把关与最终验收。
   - **下位代码执行**可交由子代理（DeepSeek）按主模型给出的**精确类名、节点路径、方法签名与坐标规格**编写代码与跑自动化测试，严禁让 DeepSeek 自由发挥设计 UI 或瞎编文案。
   - **文案铁律**：严禁 AI 八股文（严禁名词堆砌、排比句、每段结尾喊口号升华、说明书画外音如“拖动鼠标/滚动滚轮”）；必须参考原版游戏真实中文汉化表（`_vanilla_loc.json`）的朴素、冷幽默、具体动作语感，调用第三方网关按次计费模型（`[按次计费5X]gemini-4-carbon-反重力`，走 `api.ttk.homes`，**严禁用按量计费**）或由主模型严格把关洗稿，且符合西方深空科幻背景（严禁掌柜、伙计、上秤、镖局等古风词，NPC 台词内不写死数字价格）。

---

## 二、放大镜检视与真伪对质系统（`InspectionUIManager`）大坑全解

这是此前出 Bug 最多、反复返工十余次的重灾区，根因全部在于**没看原版场景层级就瞎挂节点**。

### 坑 1：放大镜镜片内看不到放大画面 / 放大图层溢出全屏 / 出现方形补丁
- **错误做法**：
  - 把放大图层（`_large`）直接挂在 `ui.panel.transform`（`_InspectionUI`）或 `ui.magnifyingGlass.transform.parent` 下（这两者其实是同一个节点！），导致没有圆形遮罩裁剪，大图直接把整个托盘和放大镜盖死；
  - 或者自己给 `_large` 加 `RectMask2D` 盖在放大镜上面，结果在圆形放大镜上贴了一块丑陋的方形马赛克膏药；
  - 或者把 `ui.magnifyingGlass` 隐藏掉，变成没有放大镜的干看。
- **原版真实层级结构（实测探针 `arcrealhier` 验证）**：
  ```text
  _InspectionUI (ui.panel, 912x804)
    ├── ConfrontPanel (右下角疑点对质栏)
    ├── AppraisalPanel (鉴定委员会估价栏)
    ├── _CigaretteInspection / _Ribwich (各品类 Small 托盘小图，位于放大镜下层)
    └── MagnifiyingGlasse (ui.magnifyingGlass, 484x456, 带 UIDragHandler 可鼠标拖动!)
          └── Mask (484x456, 带圆形镜片 Image + Unity Mask 模板裁剪组件!)
                └── _CigaretteInspection / _Ribwich (各品类 Large 2x 放大图，挂 CounterMoveChild!)
  ```
- **标准正确实现（已实测跑通，见 `ArcInspect.EnsureView`）**：
  1. **小图根容器 `small`**：挂在 `ui.panel.transform`（`_InspectionUI`）下，尺寸 `900x800` 居中，**必须 `small.transform.SetSiblingIndex(ui.magnifyingGlass.transform.GetSiblingIndex())`**，确保托盘上的实物处在放大镜正下方。
  2. **大图根容器 `large`**：**必须挂在 `ui.ribwichUILarge.transform.parent` —— 实测就是 `MagnifiyingGlasse/Mask` 这个带 Unity `Mask` 的圆形遮罩节点！** 这样原版圆形遮罩自动把大图裁成镜片形状，镜片边缘高光与框体天然融合，**完全不需要自己加 `RectMask2D`，也绝对不能盖到放大镜上面**（盖上去就是在圆形镜片上贴方块膏药）。
  3. **镜头跟随同步（`ArcLensFollow`）**：挂在 `large` 上，`LateUpdate` 里：
     ```csharp
     Vector3 lensPos = lens.position;                                  // lens = Mask 节点
     Vector3 pos = lensPos + zoom * (target.position - lensPos);        // target = small
     pos.z = target.position.z;
     transform.position = pos;
     float parentScale = transform.parent != null ? transform.parent.lossyScale.x : 1f;
     float k = parentScale == 0f ? zoom : (zoom * target.lossyScale.x / parentScale);
     transform.localScale = new Vector3(k, k, 1f);
     ```
     `zoom = 2f`（对齐原版 `ribwichUILarge.localScale`）。
  4. **尺寸换算**：256px 盘面 → `ArtScale = 1.6f`（约 410px），放进 900×800 根容器；`large` 由 `LensFollow` 再放大 2 倍并被 Mask 裁圆，于是镜片里正好是盘面中心的放大特写。
  5. **像素锐化**：取模组 Sprite 时强制 `sprite.texture.filterMode = FilterMode.Point`，否则放大 2 倍会糊成油画。
  6. **验证方法**：真机截图必须同时看到「镜片里的放大局部」和「镜片外桌上完整盘面」才算对（`shots/inspect-v10.png` 是合格样张）。


### 坑 2：检视贴图放大后糊成油画马赛克
- **原因**：`ModLoader` 加载模组 PNG 生成 `Texture2D` 时默认 `filterMode = FilterMode.Bilinear`。在检视面板放大 2x~4x 后像素边缘全部糊掉。
- **解法**：获取模组检视 `Sprite` 时，强制将 `sprite.texture.filterMode = FilterMode.Point;`！同时不要用 256x256 的 AI 杂色糊图缩成 32x32，检视底图应当是像素颗粒清晰（例如 96x96 ~ 128x128 逻辑像素，按 `scale = 3f ~ 4f` 显示在托盘，镜片内再 2x 放大）的干净像素画。

### 坑 3：`AddArt` 初始化时 `sprite == null` 导致 `sizeDelta` 为 `0x0` 画面全空
- **原因**：预创建 `Image` 节点时 `sprite` 尚未赋值，没设 `sizeDelta`；后续 `Show()` 换 `sprite` 时只改了 `img.sprite = face`，忘了重算 `rectTransform.sizeDelta`，导致图片宽高为 `0x0` 隐形。
- **解法**：每次切换 `sprite` 时同步更新 `rt.sizeDelta = new Vector2(sprite.rect.width * scale, sprite.rect.height * scale)`，或者像 `AppraiserPlus` 一样每次 `Show()` 先 `Clear()` 销毁旧节点再按当前物品干净重建。

### 坑 4：`InspectionUIManager.toggle4 / toggle5` 空引用与疑点栏乱弹
- **坑点 A（原版字段未绑定）**：原版 `InspectionUIManager` 虽然声明了 `toggle1..5`，但在部分场景状态或直接取组件文本时要小心；同时原版 `OpenCigaretteConfrontPanel` 先调 `ResetAllToggle()` 再按需激活 `toggle1..5` 并调 `OnGenuineClaim()` 设置标准标题栏。
- **坑点 B（对自己的物品也弹对质框）**：原版 `CanOpenConfrontPanel(item)` 明确检查 `!GeneralHelper.IsItemOwned(item) && !item.IsTag("ALREADY_CONFRONTED_TAG") && !IsConfrontLocked()`。**玩家检查自己仓库里的货绝不该弹出右下角「对质」面板**（没有卖家在场跟谁对质？）。只有当顾客上门卖货（`!IsItemOwned`）时才开 `confrontPanel`！
- **坑点 C（对质没有顾客对话与奖惩反馈）**：原版 `OnConfront` 并不是静默关面板，而是通过 `DialogUIManager.Instance.StartDialogue(dialogue)` 让顾客开口认怂降价（调 `ExposeFeature()` + `InspectableHelper.OnConfrontSuccess(item)` 给予拆穿折扣）或恼怒反驳（调 `InspectionUIManager.Instance.OnConfrontFailed()` 扣 2 点阵营声望并播放 `audioFail`）。自定义品类对质必须完整接入这套对话与奖惩闭环！

### 坑 5：游戏里有两种完全不同的“放大镜”机制，千万别混淆！
1. **工具物品「放大镜」（id: `magnifier`, tag: `MAGNIFYING_GLASS`）**：拖拽到带 `INSPECTABLE_TAG` 的物品上松手 → 触发 `InspectionUIManager.Instance.OnInspectItem(item)`（打开右上灰色托盘 + 可拖动放大镜 UI）。
2. **柜台交互设备「放大镜屏幕」（`StoreMagnifyingGlass` / `StoreUIManager.EnterMagnifyingMode`）**：有顾客在场时点击切换 `shootingCamera` 正交缩放观察顾客本人（看义体/特征）。

---

## 三、ModLoader 生命周期、Harmony 与编译部署坑

### 坑 6：把 `ModDevBridgeNext.dll` 复制进游戏 `Managed` 目录导致桥接瘫痪
- **现象**：为了编译探针方便把 `ModDevBridgeNext.dll` 拷进 `Probably Stolen_Data\Managed\`，重启游戏后 `ModLoader` 直接报错拒绝加载桥接模组：`ModDevBridgeNext.dll has the assembly name 'ModDevBridgeNext', which belongs to the game or its frameworks; a mod cannot ship its own copy`，导致 MCP 端口彻底失联。
- **铁律**：**绝对禁止往游戏 `Managed` 目录永久放置任何模组 DLL！** 编译探针时若临时拷入引用，`try...finally` 必须在编译结束瞬间删除，并校验 `Test-Path` 为 `False`。

### 坑 7：模组 `Init` 阶段 `ModItemDirectory` 尚未就绪
- **现象**：在 `IMod.Init` 里注册物品时，主菜单场景还没有 `ModItemDirectory` 实例，注册会静默落空。
- **解法**：同时挂 `ModHook.OnModItemDirectoryInit` 与 `ModHook.OnGameLoadedNormal`，在存档载入进入场景后再执行一次 `RegisterIntoLive()` 幂等覆盖注册（先 `dir.Remove(id)` 再 `dir.Add(id, factory)`）。

### 坑 8：物品工厂（Factory）执行期间 `item.identifier` 为空字符串
- **现象**：在 `dir.Add(id, CreateItem)` 的 `CreateItem()` 内部读取 `item.identifier`（例如用来判断是不是本周发售新作加溢价），读出来永远是 `""`，因为 `ModItemDirectory` 是在工厂函数返回之后才给 `item.identifier` 赋值的！
- **解法**：在工厂函数内必须显式传入 `id` 或元数据对象（如 `Title`），绝不在工厂内部读 `item.identifier`。

### 坑 9：Harmony Patch 重载方法引发 `AmbiguousMatchException` 炸毁整个模组
- **现象**：`MachineProgressHelper.StartProgressTypeMachine` 有 `(GameItem, GameItem)` 和 `(GameItem, int)` 两个重载，只写 `[HarmonyPatch(typeof(MachineProgressHelper), "StartProgressTypeMachine")]` 会直接在 `harmony.PatchAll()` 抛异常，导致整个模组 `running=False`。
- **解法**：凡是 Patch 任何外部方法，必须检查是否有重载；若有重载，必须用 `new Type[] { ... }` 显式指定参数签名。同理严禁重复 `PatchAll` 同一个 `HarmonyId`（会导致数值翻倍）。

### 坑 10：条件词条价格修饰字段与暴露方法混淆
- **`ItemCondition.modValue` vs `modValueDisplay`**：真正参与价格计算（`1 + modValue / 100.0`）的是 **`modValue`**！`modValueDisplay` 仅仅是 UI 显示数字，只改 `modValueDisplay` 价格一分钱都不会变！
- **`ExposeFeature()` vs `DiscoverFeature()`**：让伪装词条（`fakeCondition` -> `realCondition`）真正揭穿并生效掉价，必须调用 **`feature.ExposeFeature()`**；`DiscoverFeature()` 只解锁图鉴记录，不会揭穿当前物品伪装。

### 坑 11：容器获取父物品与竖放/横放切换回滚
- `GameInventory` 没有 `ownerItem` 字段，正确写法是 `(item.parentInventory as GameInventory)?.GetParentItem()`。
- 物品改变占格形状（如光盘 2x2 平放 ↔ 1x2 竖放）时，**必须先 `inv.Expel(item)` 移出背包 -> 换贴图与 `GridShapeBuilder` -> 再 `inv.TryAcceptAll(item)`**；如果空间不足放不回去，必须立刻还原旧贴图和旧形状再塞回原位，否则物品会卡死或凭空消失！

### 坑 12：精灵字典丢失与 `PortraitStudio` 兼容
- 游戏在特定阶段会重建 `SpriteDict`，不能用自己的 `HashSet` 缓存“已注册过”，每次必须检查字典里是否真实存在；通过 Prefix/Postfix 挂载 `SpriteDict.GetSprite` 自愈，同时这也是让 `PortraitStudio` 自动识别模组立绘的必要钩子。

---

## 四、美术资产规格与一致性铁律

### 坑 13：同一个物品的“柜台小图 / 竖放图 / 放大镜高清图”各画各的、完全对不上
- **惨痛教训**：柜台 32x32 是一个样，放大镜里 256x256 是另一个样，竖放碟盒又是第三个样；或者给光盘中心硬画一圈毫无美感的青黄色几何条码圈把角色脸全挡住。
- **正确管线**：
  1. **游戏光盘必须用现实对应大作的真实封面做图生图（Img2Img）像素化捏他**（如 GTA6、艾尔登法环、黑神话、生化危机4、无主之地、Minecraft、赛博朋克2077、星空），保留玩家一眼能认出的标志性构图与主色调，微调细节去文字化。
  2. **单一母版同源派生**：同一款游戏的盒装图、盘面检视图、竖放盒脊图必须从同一张像素化母版裁切/派生，严禁分三次文生图抽盲盒！
  3. **像素边缘硬化与去半透明脏边**：所有像素图标必须四边泛洪抠底后做 Alpha 二值化（要么 0 全透，要么 255 实心），严禁留半透明紫边/灰边；外轮廓统一使用原版描边色 `#292234`（1px），**不做低色数调色板强行量化**（否则金手指和霓虹灯条会被平均成死灰）。
  4. **格数比例符合真实直觉**：1 格 = 16x16px。显卡用 `48x32`（3x2=6格横向长卡），内存条用 `48x16`（3x1=3格细长横条），光盘盒 `32x32`（2x2），竖放盒 `16x32`（1x2），机器与大收纳盒 `48x48`（3x3，正面平视矩形机箱，色板 `#88948A`/`#5F6868`/`#44464C`/`#316669`/`#449384`）。

### 坑 13b：Paper 手册的美术与版式（这一块返工次数最多）
- **纸张**：原版是**干净浅米灰**（约 `#d8d4c4`），**不做旧、不加噪点、不压暗四角**。我第一版做成脏黄做旧纸，被直接否掉。
- **每页固定装饰**（画在底图上，文字图片元素必须避开）：
  - 顶部居中企业抬头「空间站贸易委员会」+ 左右水平装饰线（y ≈ 20~70）
  - 右上圆形钢印（英文两行 + 中间横线），圆心约 (752,168) 半径 74
  - 标题分隔线 y = 250，页脚分隔线 y = 1104
  - 可用正文区：**x 78~822, y 262~1080**
- **示意图必须是「局部特写对比」，不是整件物品缩略**。原版讲注射器指示灯就画三个指示灯特写、讲标签就画标签实物、讲序列号就画三条条码，每张旁边配**绿字「正品」/ 红字「错误示例」**。把整张光盘缩到 200px 放上去，玩家根本看不出缺现在哪。
- **AI 出图的正确姿势（关键）**：
  1. 必须传**参考图**：`style-board.png`（像素风基准）+ **原版手册截图**（版式基准，`shots/ref-vanilla-manual.png`）。不给参考它画出来的东西和游戏完全是两个画风。
  2. 让 AI **只画插图本身**，明确禁止它画纸张、页框、抬头、正文 —— 否则它会画出一整页。
  3. AI 画完后再用脚本叠红绿标注和编号，**不要让 AI 写字**（必乱码）。
- **Paper 元素坐标**：`x`/`y` 是**元素的左上角**，不是中心！写 `x=450, w=700` 会直接跑到画布外。
- **手册显示尺寸**：`width/height` 别超过 `620×827`，太大会把整个游戏画面糊住。
- **别为了截图去改游戏分辨率**：改注册表 `HKCU\Software\Questing Goose Studio\Probably Stolen` 下的 `Screenmanager Resolution *` 会导致下次启动要玩家手动确认，非常烦人。用 960×540 截图，靠 `zoom` 放大看细节即可。


---

## 五、本机环境、编码与存档安全铁律

### 坑 14：Windows PowerShell 5.1 的 UTF-8 BOM 投毒
- 本机 `powershell.exe`（5.1）执行 `Set-Content -Encoding UTF8` 会强制写入 `EF BB BF`（BOM 头），导致下游 Node.js `JSON.parse` 或部分 JSON 读取器直接崩溃；而无 BOM 的 `.ps1` 若含中文会被 PS 5.1 按 GBK 解码成乱码并写坏源码！
- **铁律**：修改源码和 JSON 一律优先用 `edit` / `write` 工具，或 Python `open(..., encoding='utf-8', newline='\n')` 无 BOM 写入，写完校验前 3 字节不等于 `EF BB BF`。

### 坑 15：测试环境与玩家主档绝对隔离（最高红线）
- **`save_0.es3`（第 51 天主档）是玩家的真实心血存档，严禁读取、覆盖或触碰！** 每次测试前后校验其 SHA256（`87DF36A4AFF162023A98C317E38165584D843648D7336DE80198AF2C0C91BE9D`）。
- 只准载入 **Slot 1 测试档（预览文本含「第12天」）**，定位存档必须按 UI 文本匹配，严禁按 `Plate` 下标盲点！
- 严禁调用 `save` 或 `endsleep`（会触发自动存档污染测试档基线）。
- `ModDevBridgeNext` 的 `load_probe_module` 不能重复加载同名程序集，每次重编探针必须递增文件名（如 `FreyaProbe75.dll`），换探针走 `swap_probe.py`。
