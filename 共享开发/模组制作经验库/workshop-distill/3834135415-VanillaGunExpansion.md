# 3834135415 · Vanilla Gun Expansion / 原版枪械拓展 —— 反编译蒸馏档案

> **id** `vanilla_gun_expansion`（工坊 3834135415，作者 **KlN**，`ModVersion 0.6.3`，`GameVersions 0.46D`，`<Capabilities />` 与 `<Prerequisites />` **双空自闭合**）
> **反编译目录** `缓存文件夹\decomp_ws\3834135415`（**328 个 `.cs` / 92,689 行**，单命名空间 `VanillaGunExpansion`，**单层平铺、连子目录都没有**；透视报告：20 个 `ModHook` / 27 个 `ModHelper` / `ModConfig: For` / **237 处特性式 `[HarmonyPatch]` + 1 处手动 `Harmony.Patch`** / `reflection(12 文件)`；包内 739 张 PNG、6 套本地化 CSV、3 本 JSON 手册，9.6 MB）
> **为什么值得读**：**本库唯一一个"数据面 100% 寄生原版、表现面 100% 自己造"的枪械模组**——枪的六维/槽位/违禁/许可全走 `GunHelper.InitGun`（全库只调 1 次），但为了"夜里开一家地下靶场打枪"，作者自建了 **2D 精灵→3D 多边形挤出的枪械渲染器 + 姿态/装填动作引擎（2.1 万行）**。想学"什么时候该自建表现层、什么时候必须寄生原版"，它和 `3834619049-Gunsmith.md` 正好是一对反例；想学"9 万行怎么被一个可选子玩法吃掉"，它是第三个独立样本。

> **方法诚实声明**：先做三张分布表（§2.1–2.3），再按签名扫全库（`[HarmonyPatch]` 244 处 / `HarmonyAfter|Before` 10 处 / `ModHelper` 212 处 / `SetGameItemType` 15 处 / `EnableTag` 全量 / 逐文件行级分类），**精读 12 个文件**：`VanillaGunExpansionMod`、`Items`、`Stage2`、`Cfg`、`Compat`+`CompatRules`、`CompanyGunRules`、`CompanyGunArt`、`Range`（注册段）、`RangeGunModels`、`RangeGunShell`（头部 + `Draw`）、`GunFacts`、`History`、`HiddenDamage`（补丁段）、`MercRental`（存档段）、`RetiredItems`、`Nitration`、`CartridgeLoading`、`QuickReplaceRack`、`AgreedPrice`。**未通读 9.3 万行**。行数按 `wc -l` 口径（与 `92,689` 对齐）。

## 1. 一句话结论

**"每把枪一个类、每个配件一个类"这个假设不成立——它一行枪械逻辑类都没有（8 把新枪 = 8 行数据 + 8 段美术字面量）。9.3 万行的真实形状是：46% 在一个可选的地下靶场（其中 22.7% 是自建 3D 枪械渲染器）、7.5% 涂装、6.5% 名枪账本、5.8% 战损模型——真正面向原版枪械体系的"扩展"只占约 11%；而且 92,683 行里只有 48,643 行是有效语句（52.5%），28.9% 是独占一行的花括号、10.5% 是 ILSpy 的 `//IL_` 噪声。**

它既不是 PCExpansion 的"60% 事务脚手架"，也不是 GoFishing 的"单文件 24.6% 神类 + 每档 UI 写两遍"：**它是"一个自建渲染引擎 + 一个小游戏把行数吃掉"，而且这个引擎真的在解决原版没提供的问题。**

**那 9.3 万行值不值？值，但不是"枪械扩展"值——是"一个地下靶场的 3D 枪械渲染器"值。** 46% 的靶场里，那 22.7% 的渲染器（手描多边形 + 挤出）是原版完全没有、**真正的原创资产**；而能学的"原版体系扩展"只有约 11%，Gunsmith 用 1/4 体量就做到了几乎同样的事——**因为 Gunsmith 连表现都寄生原版，VGE 把表现自己造了**。所以：学它的数据面寄生/表驱动/注册账本/落盘刷状态/旧档洗澡/让路告知（几千行可落地）；**不要**学它的同方法多类补丁与硬编码别家 id 却不声明依赖；**要不要学它自建渲染，取决于我们是否也有一个"原版没有的表现需求"并愿意付 2 万行——没有就别开这个头。**

**它是面向玩家的野心型个人工程，不是自用工具**：44 个独立开关分 7 段（`Cfg.cs:133-184`）、**6 种语言 × 2,695 个本地化键**、3 本游戏内 JSON 手册（`Books/vge_manual.json` 等）、一段剧情、以及对 **6 个其他模组**的兼容处理（`chip_slot_expansion`、`gunsmith_expansion`、`gse*` 系列、`hxl.merchant.stockpager`、`cs_weapon_case`、`vanilla_chem_ext`）。9.4 千订阅**不是质量判决**——它 v0.6.3、名字带 "Vanilla"（容易被当成"原版补丁"跳过）、又把最大卖点（靶场）藏在 44 个开关的"深度内容"总闸后面。

## 2. 规模与结构

### 2.1 物理目录 → 行数（328 文件 / 92,689 行）

| 目录 | 文件 | 行数 | 占比 | 职责 |
| --- | ---: | ---: | ---: | --- |
| `VanillaGunExpansion\` | 327 | 92,683 | **100.0%** | 一切：物品表、打印链、涂装、靶场、名枪、战损、补丁，全平铺在这层 |
| `Properties\` | 1 | 6 | 0.0% | `AssemblyInfo` 反编译残留 |

**"按目录看结构"在这里彻底失效——不是 PCExpansion 那样 94% 挤在一个 namespace，而是 100% 挤在一个 namespace 且连一层子目录都没有。** 分层只靠文件名前缀（`Range*` / `Paint*` / `Hidden*` / `Famous*` / `Company*` / `*Patch`）。

### 2.2 功能簇 → 行数（按文件名前缀聚类，覆盖全部 92,683 行）

| 功能簇 | 文件 | 行数 | 占比 | 干什么 |
| --- | ---: | ---: | ---: | --- |
| **`Range*`（地下靶场）** | 68 | **42,676** | **46.0%** | 打烊后的射击小游戏：3D 枪械视模型、飞行靶、命中判定、周六联赛、租枪位 |
| `Paint*`（涂装） | 14 | 6,906 | 7.5% | 程序化涂装图案 + 贴纸 + 刮漆 |
| `Famous*`（名枪） | 20 | 5,983 | 6.5% | 履历累积 → 成名 → 命名 → 收藏家上门 → 报纸 |
| `Hidden*`（战损/隐损） | 9 | 5,366 | 5.8% | 街头冲突流出的枪带"看不见的损伤"，需鉴定与修复 |
| `GunMod*`（配件 UI/数值） | 14 | 4,168 | 4.5% | 自绘配件对比面板、配件装/卸手势 |
| `Collector*`+`MercRental`+`Rental*`+`UL*` | 10 | 3,731 | 4.0% | 6 种新客人 + 枪械出租契约 |
| `Company*`（四家公司 8 把新枪） | 4 | 2,277 | 2.5% | **唯一"扩展原版枪械体系"的部分**：数值表 + 美术表 + 注册 |
| `Powder*`+`Cartridge*`+`Nitration` | 10 | 2,089 | 2.3% | 弹壳 + 按克称火药 + 棉花硝化制火药 |
| `Printer*`+`Chip*` | 15 | 1,791 | 1.9% | 芯片架、超频、成品率、批量 |
| `Ammo*`+`AmmoBox*` | 14 | 1,571 | 1.7% | 口径弹药、弹药盒自动装填与议价 |
| `Story*`+`Cabinet*` | 11 | 2,552 | 2.8% | 一段剧情分支；九格枪柜与展示 |
| 其余（补丁壳、基础设施、编解码、手册） | 138 | 13,573 | 14.6% | 101 个 `*Patch.cs`（2,689 行）+ 存档 + 配置 |

**`Range*` 内部再切分（这 46% 到底花在哪）**：

| 子块 | 文件 | 行数 | 占总库 |
| --- | ---: | ---: | ---: |
| **3D 渲染 / 姿态 / 精灵锚点** | 16 | **21,061** | **22.7%** |
| 射击局 / 命中反馈 / 音效 | 15 | 10,050 | 10.8% |
| UI 布局 / 日历 / 右键菜单 | 11 | 3,515 | 3.8% |
| 场地经济 / 租位 / 许可 | 13 | 3,421 | 3.7% |
| `Range.cs` 本体（地点注册 + 终端页 + 保养 + 联赛） | 1 | 3,372 | 3.6% |
| 其它（大气层、弹壳回收、存档快照…） | 12 | 1,257 | 1.4% |

> **为了"能在一家地下靶场里把枪举起来打"，作者写了 21,061 行渲染代码——占全模组 22.7%，而这只是"枪械扩展"的一个附加玩法。**

### 2.3 按后缀角色 / 按数据-逻辑-UI

**按后缀角色（桶间互斥）**：这个包的命名**没有形成体系**，用后缀切只能覆盖 31.8%。

| 角色 | 文件 | 行数 | 占比 |
| --- | ---: | ---: | ---: |
| `*Patch` 补丁壳 | 101 | 2,689 | 2.9% |
| 渲染/美术生成（`*Art *Mesh *Geometry *Pose *Raster *Forms *Look *Capture *Draw *Sprite* *Glint`） | 20 | 12,897 | 13.9% |
| UI 布局/页面（`*Layout *Page *Sheet *Panel *View *Window *Menu *Rows *Cells *Strip`） | 27 | 7,716 | 8.3% |
| 规则/数值（`*Rules *Stats *Yield *Recipes *Permits *Catalogue *Archive *Ledger *Facts`） | 23 | 6,199 | 6.7% |
| **其余（无后缀巨型宿主：`RangeGunShell` `RangeGame` `Paint` `Range` `RangeShotFeedback` `GunModUI` `MercRental` `HiddenDamage`…）** | 156 | **63,182** | **68.2%** |

**按"数据 vs 逻辑 vs UI"（按文件内主导标记判定：`Vector3/Color32/Face/Mesh` ↔ `RectTransform/TreeNodeRender/TMP` ↔ `[HarmonyPatch]`；误差 ±4%）**：

| 层 | 文件 | 行数 | 占比 | 构成 |
| --- | ---: | ---: | ---: | --- |
| **自绘渲染 / 像素与 3D 几何** | 39 | 37,699 | **40.7%** | 靶场网格挤出、姿态、精灵捕获、涂装像素合成 |
| **自绘 UGUI 窗口** | 33 | 19,292 | 20.8% | `CustomUIWindow`/`TreeNodeRender` 堆出的终端、配件面板、日历、枪柜 |
| 纯规则 / 数据 / 状态 / 编解码 | 109 | 25,836 | 27.9% | `Story` 1247、`FamousArchive` 1050、`CompanyGunArt` 1046（纯字面量表）、`RangeEconomy` 812、`RangeWeaponRules` 737、`Ammo` 735、`History` 717、`ChipRack` 526 |
| Harmony 补丁 + 直连原版逻辑 | 146 | 9,856 | 10.6% | 244 处 `[HarmonyPatch]`（**142 处嵌在非 `*Patch` 命名的文件里**） |

**三个"看着像 9.3 万行、其实不是"的事实**：

1. **有效语句只有 48,643 行（52.5%）**：92,683 行里 7,598 行空行、**9,690 行 `//IL_xxxx`**、**26,752 行只有 `{`/`}`/`};`**（28.9%——大量对象初始化器，每个字段独占一行）。**引用行数必须换算**，否则与 Gunsmith 的 2 万行不可比（同为 ILSpy 产物，噪声同比例）。
2. **最大单文件 10.0%**：`RangeGunShell.cs` 9,261 行（GoFishing 是 24.6%）；第 2–10 名合计 37.6%。**没有"单文件神类"，但有"单子系统黑洞"**。
3. **无冻结旧引擎、无生成代码**：0 处重新声明 `PlayerStore`/`GameItem`/`SaveManager`；透视报告的 `dynamic-code(2 文件)` 是 `AccessTools.Method("类:方法")` 字符串解析 + `GetPatchInfo` 探测（`QuickReplaceRack.cs:20-31`），不是 `Assembly.Load`/`Emit`。

### 2.4 与 Gunsmith（3834619049）的规模对比

| 维度 | **Gunsmith** `3834619049` | **Vanilla Gun Expansion** `3834135415` | 倍率 |
| --- | --- | --- | --- |
| 文件 / 行数 | 95 / 20,980 | **328 / 92,689** | 3.5× / 4.4× |
| `[HarmonyPatch]` | 32 类 | **244 处 / 165 文件** | 7.6× |
| 新枪 | **13 把**（11 致命 + 2 非致命） | **8 把**（4 家公司 × 2） | 0.6× |
| 可打印配件 | 14 自建 + 7 张配件卡带 | **28 条配方 = 18 个原版配件 + 10 个自建** | 1.4× |
| **枪械数据结构** | **零自定义**：`GameItem` + `GameSlotInventory` + `GunHelper.InitGun` | **零枪械逻辑类**，但有 **3 张自定义 DTO**（数值/美术/3D 模型，见 §3.2） | — |
| 表现层 | **寄生原版** `parentItemSpriteModifierForItemFunc` 叠加图层 | **自建** 2D 图层合成 + **2D→3D 多边形挤出** + 姿态引擎 | — |
| 存档 | 全在原版物品标签 / `modData` | 全在原版物品标签（`VGE_*`）+ `modData` 2 键 | — |
| 代码分布 | 均质：8 个功能组各 1–3 千行 | **46% 在一个可选子系统** | — |

> **本档案最重要的一行对比**：**Gunsmith 连"枪长什么样"都寄生在原版（`ItemSpriteModifier` 的锚点名 + 偏移表，见 `3834619049-Gunsmith.md` §2.2）；Vanilla Gun Expansion 只把"枪是什么"寄生在原版（`GunHelper.InitGun` 一次调用），把"枪长什么样、怎么动"整套自己造了。** 两条路都不是"自建枪械模型替换原版"，但它比 Gunsmith 多出的 7 万行，**一分钱没花在游戏性数据上，全花在表现层**。

## 3. 它怎么做这件事

### 3.1 "Vanilla" 是什么意思：**不是改数值，是把原版枪械"制造化"**

manifest 原话是「从金属锭到成品枪都能自己做」「框架、改装件和弹药都能用芯片打印」。源码逐条对上（`Items.cs:41-71, 121-130`）：

| 动作 | 证据 | 含义 |
| --- | --- | --- |
| **18 个原版配件变可打印** | `Items.Attachments`：`grip / short_stock / bipod / compensator / internal_springs / stock / barrel / trigger_group / pistol_switch / ammo_display / mag_10mm_smg / mag_10mm_smg_high / grip_gel / silencer2 / laser2 / match_barrel / mag_10mm_smg_box / gl` | 全是原版 `GunModDirectory` 的 id（已在 `共享开发/reference/Assembly-CSharp/GunModDirectory.cs` 核实）；只配"配方 + 卡带"，**没改六维** |
| **4 个原版枪框架变可打印** | `Items.cs:121-124`：`Frame(d,"vge_frame_glock","glock_receiver",150,2)` / `revolver` / `smg` / `shotgun` → `GunFrameHelper.InitFrame(val, gunId, contraband)` | 打出来的是**原版枪的框架**，玩家用原版 `gun_part` 点框架合枪 |
| **2 把原版非致命枪变可打印** | `Items.cs:129-130`：`vge_chip_stun_gun`→`stun_gun`、`vge_chip_tazer`→`tazer` | 原版枪 id，只是配了一张卡带 |
| **8 把新枪** | `CompanyGunRules.Guns`（meridian / frontier / blueshield / echelon 四家） | 唯一"新内容" |
| **给原版转轮枪开改装窗** | `CompanyAttachments.cs:34` `Revolvers = { "revolver", "vge_packhorse", "vge_breakbranch" }`；`:36-60` `RevolverWindow()` 给它挂 5 槽 `GunModHelper.CreateGunModWindow` | 原版 `revolver` 本来没有 `contentWindow`，不挂就装不上"水套/气缸" |
| **不动原版数值** | 全库 0 处 Rebalance；`GunModStatsPatch.cs:14-23` 对 `GunHelper.UpdateCurrentGunStats` 只做 `GunModUI.RefreshStats`；`MultiplyBaseStatPatch.cs:9-21` 只做"剥掉别家 `GSE_SELF_MADE_TAG` 后重算" | **扩适用范围，不改原版数字** |

**结论：`Vanilla` = "以原版枪械体系为地基去做制造/涂装/靶场"，不是数值 overhaul，更不是替换。**

### 3.2 枪械数据表长什么样：**三张表，各管一件事**

**① 数值表 `CompanyGunRules.Gun`（`CompanyGunRules.cs:9-63`）—— 17 个字段**

```csharp
// CompanyGunRules.cs:93-102 —— 8 行就是 8 把枪的全部玩法数据
internal static readonly Gun[] Guns = new Gun[8]
{
    new Gun("lineguard",   "meridian",   10,   2, 35, 45, 42, 60, 36, 60, 60, 240, 180, 450, 81, ("metal_ingot",1),("printer_plastic",1)),
    new Gun("watchtower",  "meridian",   10,   3, 65, 45, 60, 50, 25, 35, 40, 280, 215, 450, 76, ("metal_ingot",2),("printer_plastic",1)),
    new Gun("packhorse",   "frontier",   12,   3, 75, 45, 80, 25, 35, 40, 65, 300, 230, 450, 86, ("metal_ingot",2),("printer_plastic",1)),
    new Gun("breakbranch", "frontier",   45,   2, 38, 45, 60, 45, 40, 70, 70, 250, 190, 450, 90, ("metal_ingot",1),("printer_plastic",1)),
    new Gun("escort",      "blueshield", 7301, 1, 18, 17, 30, 75, 50, 40, 60, 180, 135, 350, 60, ("printer_plastic",2),("common_electronic",1)),
    new Gun("cordon",      "blueshield", 7301, 2, 30, 60, 42, 50, 40, 40, 50, 240, 180, 450, 70, ("metal_ingot",1),("printer_plastic",1),("common_electronic",1)),
    new Gun("hush",        "echelon",    7301, 2, 27, 80, 40, 50, 60, 55, 30, 650, 490, 450, 74, ("metal_ingot",1),("printer_plastic",1),("rare_electronic",1)),
    new Gun("boundary",    "echelon",    7301, 3, 70, 90, 40, 45, 35, 75, 20, 900, 675, 600, 94, ("metal_ingot",2),("printer_plastic",1),("rare_electronic",1))
};
```

字段序（`:41` 构造函数）：`name, maker, caliber, tier, firepower, style, reliability, recoil, ergonomics, accuracy, repair, value, frameValue, price, progress, params (item,count)[] materials`。`Id` 与 `Chip` 由 `name` 自动拼（`Id="vge_"+name`、`Chip="vge_chip_frame_"+name`，`:44-45`）。**没有"解锁条件"字段**——解锁走网络升级（§3.4）。`Caliber 7301` 是自造"能量口径"（`EnergyCaliber`，`:77`）。

**② 美术/挂点表 `CompanyGunArt.Gun`（`CompanyGunArt.cs:28-49`）—— 15 个字段**

```csharp
internal sealed class Gun {
    public string Sprite, FrameSprite;         // 枪本体 / 框架贴图
    public string[] Layers;                    // 图层名序列（barrel/stock/receiver/lights…）
    public int Width, Height, Cells, FrameCells;
    public Mount[] Mounts;                     // ★ 每枪一组挂点：Attachment/X/Y/Sprite/Col/Row/Cols/Rows/RefitCell
    public (string Sprite,int X,int Y)? SightSeat;
    public int[] Slots, Anchors, Well;
    public int ChargeX, ChargeY;
    public Dictionary<string, Dictionary<int,string>> Charge;   // 口径→装弹动画帧图
}
```

`Mounts` 是这套设计的核心：**每个配件在每一把枪上有自己的贴图名与像素偏移**（`lineguard` 14 个挂点 `:78`、`watchtower` 6 个 `:246`、`cordon` 8 个 `:331`）。`CompanyGunArt.cs:93-96` 直接写 `Attachment = "gsx_holo_sight", Sprite = "gsx_holo_sight_view"`——**对 Gunsmith 的硬编码挂点**（见 §5 红线 2）。

**③ 3D 模型表 `RangeGunModels.Model`（`RangeGunModels.cs:223-265`，靶场专用）**
`Id / Mechanism(Action 枚举：Slide Bolt Revolver Break Pump Fixed IndexedCylinder) / Width / Kick / Length / TubeDiameter / Company / Frame / Order[] / Modules / Mouth / Grip / Sockets`。**一把枪在靶场里"怎么动"由 `Mechanism` 一个枚举决定，不是由子类决定。**

**加一把新枪要动几个文件（实测）**：

| # | 文件 | 改动 | 必须? |
| --- | --- | --- | --- |
| 1 | `CompanyGunRules.cs` | `Guns` 数组 +1 行（17 参数） | ✅ |
| 2 | `CompanyGunArt.cs` | `Guns` 字典 +1 项（Sprite/FrameSprite/Layers/尺寸/6–14 个 `Mount` 字面量，约 40–80 行） | ✅ |
| 3 | `Localization/{en,zh,ja,ko,fr,ru}.csv` | 各 2 键：`vge_<name>_name`、`vge_<name>_chip_note` | ✅ |
| 4 | 贴图集 | `vge_<name>`、`vge_frame_<name>`、每个配件 `*_view` | ✅ |
| 5 | `RangeGunModels.StockModels()` | +1 段 ~40 行多边形字面量（要在靶场里能打才要） | ⭕ |
| 6 | `CompanyGunRules.BodyPart()` | 若吃专属机体配件，switch 里 +1 case | ⭕ |
| — | `Items.Register()` | **零改动**——`foreach (gun in Guns)` 自动注册 枪 + 框架 + 卡带三件（`Items.cs:108-120`） | 无需 |

**即"最小 4 个文件（2 张 C# 表 + 6 语言 CSV + 美术），要进靶场再加 1 个"。** 对比 Gunsmith"每把枪一个 100 行 `Gun()` 函数体"，这里是**填表**。

### 3.3 "可打印"走哪条路：**和 Gunsmith 同一条 `PRINTER_CHIP_TAG` 路线，但多两级**

```csharp
// Items.cs:251-275（已删 //IL_ 噪声）
val.EnableTag("PRINTER_CHIP_TAG", false);
if (frame) val.EnableTag("PRINTER_FRAME_CHIP_TAG", false);
bool needMetal = mats.Any(m => m.Item1 == "metal_ingot");
MachineProgressHelper.InitProgressSourceItem(val, progress, target, true, count, needMetal ? "Metal Printing Module" : null);
if (frame) MachineProgressHelper.AddRequiredMachineTag(val, "CNC Module");
MachineProgressHelper.SetAdditionalRequiredItems(val, new List<(string,int)>(mats));
AudioHelper.InitSmallModule(val);
ModHelper.SetSource(val, "vanilla_gun_expansion", VanillaGunExpansionMod.Name);
```

**与 Gunsmith 完全同构**（`3834619049-Gunsmith.md` §2.3）。真正的差别在卡带之外：

| 环节 | Gunsmith | VGE |
| --- | --- | --- |
| 配件卡带 | 7 张 | **28 张**（含 18 个原版配件） |
| 芯片存放 | `ChipPouch` 容器 | **打印机的"芯片架"**：`AllowOnlyTaggedItem(rack,"PRINTER_CHIP_TAG")` + `slotPriorityFunc` 让芯片优先入架（`ChipRack.cs:104-105`） |
| 超频 / 成品率 | 只有 `HandlePrintedItemQuality` | `PrinterOverclock` 332（挂 `MachineryHelper.GetMachinePowerUsage`，功耗换速度）+ `PrinterYield` 130 + `Quality` 38 |
| **从金属锭炼起** | 无 | `IngotRecipes` 184（**默认关闭**；manifest 专门警告"开过的先关掉，读档后再存一次"） |
| **弹壳 → 按克装火药** | 无 | `CartridgeLoading` 761 + `PowderMeasure` 423：先印弹壳，再把火药以**克数**写进物品（`UseCountHelper.SetCurrentUseCount` 管弹壳、`PowderMeasure.SetGrams` 管火药，`CartridgeLoading.cs:39-43`） |
| **火药从哪来** | 无 | `Nitration` 134：Prefix 掉 `PlantHarvest.Output`，**棉花纤维/织物 → 火药克数**（`Nitration.cs:11-31`）——把原版农业系统当化工原料管道 |

**制造链因此是：棉花 → 火药（克）→ +弹壳 → 弹药 → 卡带 → 枪框架 → +枪管/机匣 → 枪。比 Gunsmith 深两跳，且后两跳是"手动作业 + 自绘窗口"，不是原版机器。**

### 3.4 靶场：**用公开 API 注册原版 afterhour location，不伪造**

```csharp
// Range.cs:708-722
public static void Register() {
    Func<bool> live = () => Usable && Up("VGE_RANGE");
    ModHelper.AddAfterhourLocation(new RangeLocker.Location {
        id = "vge_range", source = "vanilla_gun_expansion",
        title = () => L("range_title"),
        imageKey = "vanilla_gun_expansion:vge_range_scene",
        backgroundKey = "vanilla_gun_expansion:vge_range_scene",
        onLeave = OnLeave, buildPanel = BuildPanel, onTick = Tick,
        isDiscovered = live, isAvailable = live
    });
    ModHelper.AddAfterhourEvent("vanilla_gun_expansion", "vge_range_league", "vge_range",
        () => L("range_league_title"), 1, () => L("range_league_desc", Prize(0)), null, 0f,
        (AfterhourEventTone)0, () => Usable, null, null,
        new AfterhourWeekday[1] { (AfterhourWeekday)6 }, false);   // 周六联赛
}
```

- 这是 **GoFishing 三档里的"档②偏③"**：公开 `AddAfterhourLocation` + 真正的 `AfterhourLocation` 子类（`RangeLocker.Location`），**没有**反射伪造 `MapUIManager.Current`（GoFishing 那种 `ModReflect.Set(map,"Current",…)` 这里 0 处）。代价是必须**自己画完整个靶场**（21k 行渲染 + 3.5k 布局）。
- **整条进度线全是原版网络升级**：`Range.RegisterUpgrades()`（`Range.cs:744-766`）一次注册 **16 级** `ModHelper.AddNetworkUpgrade`（`VGE_RANGE` 1500 → 租位 `RENT1..3` → 归还/枪柜/弹匣位/补弹/保养/规则/上油 → 扩建 `EXPAND1..3` → 两级赞助 `SPONSOR1..2`），前置用 `pre` 字符串串链。**零自定义商店界面。**
- 场地维持费、擦枪、租枪位、弹壳回收各有 `*Rules`/`*Economy` 文件，纯数值无 UI。

### 3.5 名枪 + 战损：**两条都寄生在"物品标签"，不建第二套存档**

- **履历**：`History.cs:14-30` 定义标签键——`VGE_HIST_N_INT`（条数，上限 40）、`VGE_HIST_{i}_STRING`（每条履历文本，`EntryData()` 逐条 yield）、`VGE_ENGAGED_INT`（交战次数）、`VGE_LEAGUE_PODIUM_INT`、`VGE_FAMOUS_INT`、`VGE_FAMOUS_UID`、`VGE_MAINT_DAY_INT`。**一把枪的全部故事就是它自己身上的一串 tag**，卖出/买回/读档都跟着走。
- **成名判定**：`FamousStory`/`FamousArchive` 读这些计数（`FamousEngaged=20`、`FamousPodiums=3`、`FamousBonus=15`），过线后被命名、被收藏家 `vge_famous_collector` 盯上、上报纸（`FamousNews`）。命名不改 `name`，而是**新加一行类型文本**（`TypeTagPatch.cs:9-24` Prefix `CommonTooltipHelper.CreateItemTypeTooltip`）。
- **战损**：`HiddenDamageRules` 定义 6 个来源（`War/Circulation/Mercenary/Rental/Seller/Range`）+ 概率（40/25/20/10/10/10）+ 上限 3 处 + 每处掉 10（`:18-26`）；`HiddenDamage*.cs` 用 `ItemFeature`（`VGE_HISTORY`、`VGE_WAR_DAMAGED`）+ 自绘遮罩把"看不见的损伤"画在枪贴图上（`HiddenDamageMarks` 1500 + `HiddenDamageRegions` 1118 + `HiddenDamageWindow` 799）。
- **成色判定只认 `realCondition`**（`GunFacts.cs:38-46`：`FindItemFeatureByCategory("CATEGORY_EQUIPMENT_CONDITION").realCondition.identifier`，索引 0 新 → 4 差）——与本工作区硬规范一致。

### 3.6 新客人：**字典直插 6 个工厂**（`VanillaGunExpansionMod.cs:35-43`）

`StoreClientListDict.storeClientDict[key] = factory`：`vge_rev_gunsmith`→`RevGunsmith.Create`（退休枪匠，每周）、`vge_famous_collector`→`CollectorCall.Create`（名枪收藏家，电话 + 上门）、`vge_gunsmith_bulk`→`GunsmithBulk.Create`（批量枪匠，每天）、`vge_rev_qm_extra`/`vge_rev_raider_extra`/`vge_sec_surplus_extra`→`SellerGoods.*`（给 3 种原版商人加货）。Gunsmith 也用过同一套路（`Progression.cs:229-253`）——**这是本游戏最便宜的新客人接口**。

### 3.7 Patch 靶点表（244 处；★ = 撞热点）

| 靶点（原版类.方法） | 类型 | 目的 | 风险 | 撞几次 |
| --- | --- | --- | --- | --- |
| **★ `DirectoryMaster.Item(string,bool)`** | Postfix | **每造一把枪就把 `WeakReference` 塞进列表**供战损/名枪扫描（`HiddenDamage.cs:57-77`、`GunBayPatch.cs:6`、`PrinterPatch.cs:6`） | **最高**：全游戏最热的物品工厂 | 3 |
| **★ `SaveManager.DecodeNodes`** | Prefix/Postfix/Finalizer/Transpiler | 读档修正 7 个子系统的标签/形状/雪碧（`RetiredItems`、`SavedPowder`、`SavedHopperFilter`、`PrinterLayoutRead`、`StoreContentsRead`、`CompanyGuns`、`BoxDispenserLoadArt`）；另有 `EncodeNode`/`EncodeNodes`/`DecodeNodesInternal` 3+2 处管写档 | 高：**7 个类各打一次，类间无顺序保证** | **7** |
| ★ `PlayerStore.WriteSlotFile` | Prefix/Finalizer | **落盘瞬间刷新**租枪契约、名枪纸条、UL 卡带（`MercRental.cs:623-629`）；`SaveFiles.RotateIntoHistory` 轮转前再刷一次（`:611-619`、`RangeSaveBeforeRotation.cs`） | 中高：写档路径 | 3 + 2 |
| ★ `GunHelper.UpdateCurrentGunStats` | Postfix / Prefix+Postfix+**Transpiler** | 刷新自绘配件面板；`CompanyGuns` 还改口径名与命令枪判定 | 中：每次重算六维都跑 | 3 |
| ★ `GunHelper.SetEquipmentCondition` / `MultiplyBaseStat` / `DisassembleWeapon` | Postfix / Prefix | 战损上限、剥别家 `GSE_SELF_MADE_TAG` 后重算、拆解走自家逻辑 | 中 | 2 / 1 / 1 |
| ★ `GameItemElement.ResolveSpriteByName` / `ValidateSelectLocked` | Postfix / Transpiler | 弹药盒/枪架/枪柜/涂装换皮；选中判定 | 中：每次画物品都调 | 4 / 3 |
| ★ `ItemContextHandler.UpdateConditions` / `SetButtonColor` / `TriggerButton` | Prefix/Postfix | 三套子系统（弹药盒排序、修枪菜单、靶场菜单）各往**右键菜单**塞按钮 | 中高：同一菜单被三拨人改 | 3 / 3 / 3 |
| ★ `TooltipUseHelper.HasUseTooltip` / `HandleUseTooltip` | Prefix/Postfix | 上油、修战损、电焊翻新的"能对谁用" | 低中 | 2 / 2 |
| ★ `StoreClientListDict.CreateStoreClient` | Postfix | 给 `gse*` 系列客户挂"议价成交"标记（`AgreedPrice.cs:6-30`） | 低（首行字符串比较即返回） | 1 |
| `MachinePrinter.FinishPrinting` + `MachineProgressHelper.*`（7 处） | Prefix/Postfix/Finalizer | 成品入库、成品率、超频、芯片同步、打印保活 | 中：原版打印机链路被打成筛子 | 7 |
| `StoreClientListGun.CreateDeliveryFollowUp` | **Transpiler** ×2 / Postfix ×2 | 送货上门改成"枪被调包/战损"分支 | **高**：Transpiler + 同方法 4 个类 | **4** |
| `GunOrderData.PlayerHasGunOfTier`/`GetTotalReward`/`IsAccepted`/`CreateServiceGun` | Transpiler / Postfix / Prefix | 新枪计入档位、声望溢价、服务枪替换 | 中 | 5 |
| `NegociationUIManager.*`（`BuildGunOrder` `GunRequirementRow` `BuildSignature` `OnOfferAcceptClick` `SectionTitle` `CurrentValueSuffix`） | Prefix/Postfix | 订单面板加行、加签章、跳收购价 | 中 | 6 |
| `EmporiumEntry.ResetData`/`OnDestroy`/`RestoreStoreUIAfterReturn`/`ClearFrontInv` | Prefix/Postfix | 离店/回店关靶场窗口、放资源 | 中：场景切换必跑 | 4 |
| `StoreUIManager.CloseTopmostEscapeTarget` / `StartShootoutSequence` | Prefix/Postfix | ESC 优先关靶场窗口；冲突事件 | 中 | 2 |
| `EventSystem.RaycastAll` / `CustomUIManager.CreateWindow` / `CustomUIBuilder.Spawn`+`AddHeader` | Postfix/Prefix | 靶场页面遮挡（**全局输入**）；像素字体；配置段标题 | 高：全 UI 共享 | 1 / 3 |
| `WindowsHandler.CloseWindow` / `UIFamilyLayer.Adopt` / `TreeNodeRender.ResetParent` | Prefix/Postfix | 窗口层级与关闭归属 | 中高：通用窗口系统 | 4 |
| `PlayerStore.CanEndDay`/`EndDay`/`BeginDay`/`SaveGame`/`StealItem`/`SellItem` | Prefix/Postfix/Finalizer | 打烊校验、日结、租枪归还、名枪被偷 | 中 | 6 |
| `StoreReputation.OnItemTraded` / `RunStats.OnItemSold\|OnItemBought\|OnAugShot\|OnVendingSale` | Postfix | 枪械声望、名枪计数 | 低 | 6 |
| `ToolDirectory.CreateWelder` / `MeleeWeaponItemDirectory.CreateBoxCutter` / `MaterialDirectory.CanIngotTarget` / `MapUIManager.OnGoingOutside` | Postfix/Prefix | 新工具/材料交互；进靶场时收拾随身包 | 低 | 5 / 1 |
| `QuickReplace.Targets.Find`（**别的模组的方法**） | **手动** `Harmony.Patch` | 与 `QuickReplace` 抢枪架替换 | 低（解析失败即跳过，§4⑤） | 1 |

**没碰的热点（实测 0 处）**：`SpriteDict.GetSprite`（只调 `SpriteDict.GetRandomUpperSpriteName()` 等取立绘名，`CollectorCall.cs:539` 等 9 处，**不是补丁**）、`StoreUIManager.Update`、`GameItem.IsTag`、`PlayerStore.EndNight`（碰的是 `EndDay`/`CanEndDay`）。
**碰了的热点**：`StoreClientListDict.CreateStoreClient`（1 次，安全）、`DirectoryMaster.Item`（3 次，**危险**）、`EventSystem.RaycastAll`、`CustomUIManager.CreateWindow`、`SaveManager.DecodeNodes`（**7 次，最危险**）。

## 4. 关键代码摘录

**① 注册/反注册账本：Init 里 40 行订阅，OnDisable 里 40 行对称退订**（`VanillaGunExpansionMod.cs:17-43` 与 `:220-256`）

```csharp
ModHook.OnModItemDirectoryInit += Items.Register;
ModHook.OnModItemDirectoryInit += Workshop.Register;
ModHook.OnModItemDirectoryInit += Ammo.Register;
ModHook.OnModItemDirectoryInit += CartridgeLoading.Register;
ModHook.OnGameLoadedInit += LegacyStoreRecovery.Begin;
ModHook.OnGameLoadedLate += ChipRack.FixOrder;
ModHook.OnGameLoadedInit += QuickReplaceRack.Patch;
... // 全部 40 条
StoreClientListDict.storeClientDict["vge_rev_gunsmith"]     = RevGunsmith.Create;
StoreClientListDict.storeClientDict["vge_famous_collector"] = CollectorCall.Create;

// :220-256 —— 退订也带名字，出问题能定位到"哪一步没退干净"
Stage2.Release("vge_rev_gunsmith",      () => { StoreClientListDict.storeClientDict.Remove("vge_rev_gunsmith"); });
Stage2.Release("RemoveNetworkUpgrades", () => { ModHelper.RemoveNetworkUpgrades("vanilla_gun_expansion"); });
Stage2.Release("Items.Unregister", Items.Unregister);
// Stage2.cs:177-187 —— 唯一的异常边界：仅包住"卸载"，且打日志带步骤名
internal static void Release(string name, Action step) {
    try { step(); } catch (Exception ex) { Debug.LogWarning("[vanilla_gun_expansion] disable " + name + ": " + ex); }
}
```

**② 物品注册是"遍历表"，不是"手写 N 块"**（`Items.cs:108-120`）

```csharp
foreach (CompanyGunRules.Gun gun in CompanyGunRules.Guns) {
    string frame = CompanyGunArt.Guns[gun.Id].FrameSprite;
    Add(d, gun.Id,   () => CompanyGuns.Gun(d, gun));                                          // 枪
    Add(d, frame,    () => Frame(d, frame, gun.Id, gun.FrameValue, gun.Tier, gun.Maker));      // 框架
    Add(d, gun.Chip, () => {                                                                   // 卡带
        GameItem val = Chip(d, frame, 1, gun.Price, gun.Progress, "vge_chip_weapon", frame: true, gun.Materials);
        val.shortDescription = val.shortDescription + "\n"
            + VanillaGunExpansionMod.L("chip_company_note", gun.Value, VanillaGunExpansionMod.L(gun.Id + "_chip_note"));
        return val;
    });
}
```

**③ 3D 枪械模型的"一行一把枪"**（`RangeGunModels.cs:705-710`，节选，原行极长）

```csharp
private static Model[] StockModels() {
    return new Model[15] {              // 7 把原版枪 + 8 把自家枪
        M("glock_receiver", Action.Slide, 98, 100, 1f, 20,
          (Width: 64, Height: 48, Left: 0, Top: 3, InkWidth: 63, InkHeight: 41),
          Layer("base"),
          Layer("frame", C(Section.Rail, 0, 35,19, 59,23),
                         C(Section.Trigger, 0, 23,23, 27,24, 23,24, 28,25, 24,25, 29,27),
                         F(Section.Fore, 0, 34,0, 60,3, ...)
                    ).At(RangeGunArt.Mount.Rear, 0, 23).At(RangeGunArt.Mount.Magazine, 15, 43)
                     .Hand(14.49f, 25.50f).Seat(9, 40, 0.3f),
          Layer("slide", ...).At(RangeGunArt.Mount.Top, 16, 9, Section.Slide).Mouth(63, 10.96f, 9),
          Layer("lights")),
        M("revolver", Action.Revolver, 109, 128, 1.1f, 16, ...), ...
    };
}
```

> `C(...)` = 凸多边形轮廓，`F(...)` = 面，`Back(...)` = 背面，`.At(Mount,x,y)` = 挂点，`.Mouth()/.Wheel()/.Port()/.Seat()/.Hand()` = 枪口/转轮/抛壳口/弹膛/持握。**这些坐标是作者对着贴图一个像素一个像素描出来的**——这就是 21k 行渲染代码的真正成本；喂给它们的通用引擎是 `RangeGunGeometry.Profile`（`:6-33`，按高度线性插值出各部位截面宽度）+ `RangeGunShell.Draw(pose, motion)`（`:685`，按关键帧区间 `Segment(t,0.32f,0.385f)` 之类推进装填动作）。

**④ 不建第二套存档：把内存状态在"落盘瞬间"推进 modData**（`MercRental.cs:611-629`）

```csharp
[HarmonyPatch(typeof(SaveFiles), "RotateIntoHistory")]
private static class BeforeRotation {
    private static void Prefix(int slotId) {
        if (PlayerStore.HasInstance && PlayerStore.Instance.saveSlotId == slotId) BeforeWrite(PlayerStore.Instance);
    }
}
[HarmonyPatch(typeof(PlayerStore), "WriteSlotFile")]
private static class BeforeSlotWrite {
    private static void Prefix(PlayerStore __instance) { BeforeWrite(__instance); }
}
```

**⑤ 给"可能不存在"的别的模组打补丁：解析失败即跳过 + 幂等**（`QuickReplaceRack.cs:20-31`）

```csharp
MethodInfo methodInfo = Target();                       // AccessTools.Method("QuickReplace.Targets:Find", ...)
if (!(methodInfo == null)) {
    MethodInfo postfix = AccessTools.Method(typeof(QuickReplaceRack), "Postfix", null, null);
    Patches patchInfo = Harmony.GetPatchInfo(methodInfo);
    if (patchInfo == null || !patchInfo.Postfixes.Any(p =>
            p.owner == "mod.vanilla_gun_expansion" && p.PatchMethod == postfix))
        new Harmony("mod.vanilla_gun_expansion").Patch(methodInfo, null, new HarmonyMethod(postfix), null, null);
}
```

> 这 12 行同时证明加载器的 Harmony owner 命名是 **`mod.<manifest.Id>`**（小写下划线）——由此可反推 §5 红线 1。

**⑥ 旧档迁移：读档前先把"已下线的 id"和"旧形态 id"洗一遍**（`RetiredItems.cs:7-16`）

```csharp
[HarmonyPatch(typeof(SaveManager), "DecodeNodes")]
internal static class RetiredItems {
    private static void Prefix(List<SaveItemNode> __0) {
        SaveNodes.DropRetired(__0);
        foreach (SaveItemNode item in __0) item.identifier = SaveNodes.ChipId(item.identifier);
    }
}
```

**⑦ 对其他模组"让路"并告诉玩家**（`CompatRules.cs:8-22` + `Compat.cs:15-25`）

```csharp
internal static readonly string[] ChipPanelMods = new string[1] { "chip_slot_expansion" };  // 只让给一家
internal static string Claimant(string[] mods, Func<string,bool> loaded) {
    foreach (string text in mods) if (loaded(text)) return text;
    return null;
}
public static bool RackOff { get { if (!rackOff) return rackOff = RackClaimant() != null; return true; } }
// 再由 Compat.Tell() 用原版 GenericConfirmationUIManager.OpenPanelSingleButton 告诉玩家：
// "芯片架让给 XX 了" / "帮你把 N 张芯片搬回来了"（Compat.cs:65-80）
```

## 5. 踩坑与红线

| # | 坑 | 证据 | 我们照抄时怎么办 |
| --- | --- | --- | --- |
| 1 | **跨模组排序 id 大小写写错 = 静默失效**。`QuickReplaceRack.cs:8` 证明 owner 格式是 `mod.<manifest.Id>`（小写下划线），但另有 5 处写成 `mod.GunsmithExpansion`（`HiddenDamage.cs:34`、`Paint.cs:274`、`OrderReputationPrice.cs:7`、`GunCirculation.cs:89`、`OrderRewardPatch.cs:6`）——**这 5 处永远不命中** | `QuickReplaceRack.cs:8,20`；`OrderReputationPrice.cs:7-8` 甚至同一方法上"After 大写版 / Before 小写版"两头下注 | 排序依赖必须**从对方 manifest 抄 id 原文**；发布前用回归脚本断言"每个 HarmonyAfter 目标 id 在已装模组里存在" |
| 2 | **对另一个模组的物品 id 硬编码**：8 把枪的挂点表写死 `gsx_holo_sight` / `gsx_tactical_light` / `gsx_*_view`（`CompanyGunArt.cs:93-96, 122-125` + 另 9 处），但 **`<Prerequisites />` 是空的** | `manifest.xml:11`；`CompanyGunRules.cs:79-83` | 软依赖要 (a) 写进 `<Prerequisites>`，或 (b) 在注册时 `Loaded("gunsmith_expansion")` 才加这些挂点。**VGE 选了第三条：不声明、直接写死**——没装 Gunsmith 时这 8 个挂点就是死数据 |
| 3 | **同原版方法被 N 个类各打一次，类间顺序无保证**：`DecodeNodes` 7 次、`ResolveSpriteByName` 4 次、`CreateDeliveryFollowUp` 4 次、`WriteSlotFile` 3 次、`ItemContextHandler.*` 3×3 次 | §3.7 末列 | 模组内对同一方法的多处补丁应**合并到一个宿主类**并显式排序，或至少加 `[HarmonyPriority]` + 注释说明先后 |
| 4 | **`DirectoryMaster.Item` 是全游戏最热的工厂，被打了 3 次**；其中一次每次造枪都 `guns.Add(new WeakReference(...))`，靠定期反向清扫回收 | `HiddenDamage.cs:57-77, 770-786` | 需要"物品全局追踪"时用**事件/增量**（在真正关心的入口 Postfix），不要在物品工厂上挂列表 |
| 5 | **Transpiler 用了 6 处**（`CreateDeliveryFollowUp`×2、`UpdateCurrentGunStats`、`GetCaliberName`、`ValidateSelectLocked`、`PlayerHasGunOfTier`、`DecodeNodesInternal`） | §3.7 | 与我方规范相反（"能用 Prefix/Postfix 就绝不写 Transpiler"）。它这么写是要改**返回值拼接与档位判定**；同类需求优先 Postfix + `ref __result` |
| 6 | **"卸载前必须先清空"变成玩家义务**：manifest 里 4 句警告（清空芯片库/枪柜/靶场、等租枪归还、清储藏区多出的行、关掉金属锭配方再存一次） | `manifest.xml:7` | 这是"状态散落 6 个子系统"的代价。我们应**卸载时自动收拢**（`Items.Unregister` 已做 `DirectoryMaster.Remove`，但物品实体搬迁没做） |
| 7 | **`Release()` 吞异常只打 Warning**，且是全库唯一异常边界 | `Stage2.cs:177-187` | 卸载路径可接受，但要区分"绑定失败=Warning / 运行异常=Error+堆栈"，并让回归断言每个 patch 至少绑定一次（与 Gunsmith 档案红线 3 同） |
| 8 | **开关被缓存成静态 bool**：`RackOn` 在 `Cfg.Define()` 当场读 `.Value` 存死（`Cfg.cs:144`），改配置不重启不生效；`DeepOn` 总闸又被 `RangeOn/PaintOn` 二次收窄 | `Cfg.cs:21, 109-131, 144` | 开关要**一处判定**（永远读 `.Value`）；总闸只做短路，别在子功能里再判一次 |
| 9 | **`Range.cs:79-135` 的 `Escape` 直接接管 ESC**（Prefix+Postfix 改 `__result`），靠 `GetCurrentAfterhourLocationId()=="vge_range"` 做门禁 | `Range.cs:79-135` | 接管全局输入先判"当前场景是不是我"，且必须留兜底 `return true`（这里做到了） |
| 10 | **红线：只写自己前缀**（它做到了）——`SetGameItemType` 全是原版类型（`TOOL/DOCUMENT/MODDABLE/MATERIAL/WEAPON/STORAGE/PRINTER_MODULE/HYDROPONIC_MODULE/FIREARM/AMMUNITION`），自有标全 `VGE_*`/`vge_` | `grep SetGameItemType` 全量；`GunFacts.cs:22` 的 `stun_gun` 判定走 id 比较 | **这条要抄**：新品类用 `EnableTag` + 工具提示加行（`TypeTagPatch`），绝不新增 `GameItemType` |

## 6. 对我们的可用点

1. **（两者）"落盘瞬间刷状态"比"日结时刷状态"更可靠**：`PlayerStore.WriteSlotFile` Prefix + `SaveFiles.RotateIntoHistory` Prefix 双挂钩（`MercRental.cs:611-629`），把内存里的契约/纸条/卡带在**真正写文件的那一刻**推进 `modData`。我们的 ES3 写盘剥离只解决"卸载干净"，**加上这一招才解决"崩溃/回滚前最后一次状态没落盘"**。落到 `深空当铺：可能是群友` 的寄售合约与租借单、`深空电玩` 的机器工作态。

2. **（两者）旧档迁移：读档前先洗澡**。`RetiredItems` 共 17 行（`RetiredItems.cs:7-16`）= `DropRetired(nodes)` + `ChipId(identifier)` 两步，就在 `SaveManager.DecodeNodes` 的 Prefix 里。我们的 `Items.Unregister` 已在卸载时清目录，但**旧档里还留着已下线的 id**——这一段正是我们缺的另一半（`3834619049-Gunsmith.md` §4 也点过）。

3. **（两者）"注册/反注册账本"模式**：`VanillaGunExpansionMod.OnDisable` 把 40 条退订写成 40 行 `Stage2.Release("名字", () => …)`（`:88-256`），`Stage2.Disable` 同构（`Stage2.cs:53-176`）。我们的模组也有 `HotReloadPrepare/Activate` 与静态引用重置，但**没有"名字"这个维度**——加上它，热重载出问题时日志直接告诉你哪一步没退干净。

4. **（深空电玩）"内容表 → 自动注册"**：`foreach (var gun in Guns) { Add(枪); Add(框架); Add(卡带); }`（`Items.cs:108-120`）。我们做新机型/新卡带时若每台机器要手写"本体 + 卡带 + 框架"三段，就该收敛成一张表 + 一个循环——**这一条能直接砍掉电玩那边大量重复注册代码**。

5. **（深空当铺：可能是群友）名品"履历"用物品标签承载，不建第二套账本**：`History.cs:14-30` 的 8 个键（`VGE_HIST_N_INT` 上限 40、`VGE_HIST_{i}_STRING`、`VGE_ENGAGED_INT`、`VGE_FAMOUS_INT`、`VGE_FAMOUS_UID`）+ `EntryData()` 逐条 yield。当铺的"成色/真伪/流转史/鉴定记录"可用同一形状（判定照抄 `GunFacts.Condition`，`GunFacts.cs:38-46`），**物品卖出/买回/读档都跟着走，不需要迁移脚本**。

6. **（两者）跨模组"让路 + 告知" + 给"可能不存在"的模组打补丁**：`CompatRules.Claimant(白名单, Loaded)` + `Compat.Tell()` 用原版 `GenericConfirmationUIManager` 弹"芯片架让给 XX 了，帮你把 N 张搬回来"（`Compat.cs:15-80`）；`QuickReplaceRack.cs:20-31` 的"字符串解析 → null 跳过 → `GetPatchInfo` 探测同 owner 同 postfix 再打"是**幂等 + 失败即静默跳过 + owner 用小写 manifest id** 的标准姿势。我们的冲突扫描现在只"报 ERROR 让人去改"，加这两条就变成**运行期优雅降级 + 玩家可感知**。

> **补充判据（深空电玩）**：VGE 敢自己写 21k 行渲染，是因为原版**根本没有"把枪举起来打"的表现**——这是"原版没有的能力"，不是"原版有但不好看"；反过来，枪在背包里的样子它老老实实寄生原版（`SetSpriteAndShapeFromMod` + `Layers` + `Mounts` 全是数据表）。**自建 UI 的边界就在这里：原版没有的交互才自建，原版有的别重画**（GoFishing 越了这条线，把 16 页 UI 写了两遍）。
