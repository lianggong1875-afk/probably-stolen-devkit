# 08 立绘接入与 Portrait Studio

操作步骤在 `工具库\skills\portrait-studio\README.md`。这里只记**原理、判断方法和教训**。

---

## 一、Portrait Studio 是怎么"找角色"的（2026-11-26 完整反编译确认）

> 引擎完整三层发现、跨模组协议总线、高清去像素化租约、道具切图与配音 SDK 架构见 [12-立绘引擎与音画资源架构](12-立绘引擎与音画资源架构.md)。

结论修正：它对模组角色有**三层发现**，而**不需要做独立包、也不用进白名单就能被认出归属**的正确路子是 **IL 字面量扫描**：

1. **Mono.Cecil IL 扫描（推荐，零能力）**：`CharacterCatalogRuntime.Scan` 遍历已启用模组 DLL 的方法 IL，找 `stfld StoreClient::spriteName` 且前一条指令紧跟 `ldstr` 的字面量，自动归属到该模组（如深空电玩的 `ArcPortraitDeclaration.cs`，放一个永不调用的方法列出 12 条 `client.spriteName = "字面量";`）。如果代码里写的是 `c.spriteName = Face(...)` 方法调用，扫描就看不到，角色会掉进「来源未识别」。
2. **特性式补丁发现**：`HasSpriteDictGetSpritePatch` 扫 `[HarmonyPatch(typeof(SpriteDict), "GetSprite")]`（见下文第三节·补）。
3. **协议注册 + 早期硬编码白名单**：`RegisterProvider(id, name)` 里还留了一批早期工坊模组的硬编码白名单：

`ai.emporium`、`shu_merchant`、`probably_eleanor_followup`、`more_girls`、`baishe.metalslug`、
`rhodes_island`、`girlfrontline.mystery_doll`、`player.launchgiftportraits`、`echo_protocol`、
`kuroya.tarot`、`CyberElectronics`、`CorpPresence`、`visual_probe_20261031_three` …

白名单外的模组，它只能建出角色文件夹（`MyPortraitPack\<key>\`），但**认不出归属**。

三条旁证（判断问题时可以直接查这三个文件）：

| 文件 | 说明 |
| --- | --- |
| `PortraitStudio\spriteKeys.json` | 它 dump 的 sprite 目录。425 条 = 410 个原版 288×211 + **15 个 AI商栈 `portrait_*` 1152×844**（在名单里）。我们的 `dnpc_*`/`sw_*` 一个都没有 |
| `packs\MyPortraitPack\角色文件夹对照表.csv` | 我们 12 条写「来源未识别（可能未启用）」，群友的 `freya_cornell_portrait`/`shuangluoluo_portrait`/`siren_portrait` 已能写成「深空当铺 · 可能是群友」 |
| `PortraitStudio\status.txt` | `enabled, 0 target(s)` —— 启用着，但列表里没有可选目标 |

**顺带破除一个误判**：`spriteKeys.json` 里的 `portrait_*` 是 1152×844、不是原版 288×211，
说明它**不按尺寸过滤**；所以"我们的图尺寸不一样所以进不去"这个猜想是错的。

---

## 二、怀疑"兼容问题"时的判断顺序

先别改自己的命名或注册方式去迎合别人。按这个顺序查：

1. 对方有没有**通用扫描**入口？（反编译看有没有遍历 `ModLoader.Mods` 的逻辑）
2. 它的目录文件里有没有你的 key？（`spriteKeys.json` 之类）
3. 它的归属表里你的条目写什么？（`角色文件夹对照表.csv` 的「来源模组」列）
4. 它的状态文件怎么说？（`status.txt` 的 `N target(s)`）

四条都指向"它不认你" → **走它公开的接入协议**，而不是猜它的内部约定。

---

## 三、我们走哪条路：主模组自己就够，不做独立立绘包（2026-11-26 定案）

判断只需要两个问题：

1. **最终写图的人是谁？**
2. **立绘会不会自己变？**

深空电玩的答案是：**写图者是游戏原版**。`ArcAssets.cs` 把 `Textures\*.png` 加载进 `mod.Sprites[key]`
（`mod.Sprites[key] = sp`），`ArcClients.cs` 里 `c.spriteName = Face(face, …)` 指过去，
游戏 `SpriteDict` 自己查表渲染。8 个 NPC 是静态单图、银狼四态是模组按剧情切 4 个独立 sprite key，
**两者都在这一条链上，主模组 `<Capabilities />` 零能力就能跑通**（真机验过：`summon arc_collector` 出来
`sprite=dnpc_buyer_03`，走我们的 key 没退回原版）。

### 曾经做过的独立包，已删除

按官方 `portrait-studio-pack` 模板做过一个 `deepspace_arcade_portraits`（12 图 + 116 KB DLL + 27 KB 协议清单，
带 `files` 能力），**2026-11-26 删除**。删的理由，以后别再走一遍：

- 它和主模组是**两条并行链路**，而主模组零能力已经把显示做完 —— 它不参与"我们自己的立绘能不能显示"
- 它带 `files` → **每次改图都要在 Modding 界面人工批准**；主模组零能力可以 `trust=Updated` 自动接受
- 它 manifest 自述「独立静态换图；提供 static-v1 接口，**需支持该协议的管理器才能协作**」——
  离开 Portrait Studio 它就只是在重复主模组已经做完的事
- 它唯一的独有价值是「让装了 Portrait Studio 的玩家能替换我们 NPC 的立绘」。这个需求从来没确认过，默认不需要
- 附带的一个还有点用的功能是 `StandaloneHighResolution`（关掉场景像素化），但那是改画面设置，
  不需要读文件，**零能力就可以并进主模组**，不必为它养一个模组

**一句话**：先问"这个包离开对方模组还剩下什么"，剩下的东西能不能零能力并进主模组。
能并的就并，不能并又没人要的，就别做。

## 三·补、只想「被 Portrait Studio 认出来」该怎么做

不用做包、不用编 SDK。它会**自动发现**自己 patch 了 `SpriteDict.GetSprite` 的模组作为图源：

```csharp
// PortraitStudio 侧（反编译确认）
if (!string.IsNullOrEmpty(modId) && HasSpriteDictGetSpritePatch(folder)) {
    DirectSpritePatchIds.Add(modId);
    Debug.Log("[PortraitStudio] automatic source hook detected: " + name + " (SpriteDict.GetSprite)");
}
```

`HasSpriteDictGetSpritePatch` 是**扫模组 DLL 的 `CustomAttributeData`**，所以：

- ✅ 用**特性式** `[HarmonyPatch(typeof(SpriteDict), "GetSprite")]` 写的补丁能被扫到
- ❌ 手写 `harmony.Patch(AccessTools.Method(...))` 扫不到（Portrait Studio 自己就是手写式，所以它自己不在这张表里）

`SpriteDict` 是编译期已知类型，用特性式不触发 `reflection-external`，**主模组可以保持零能力**。

⚠️ 但要想清楚这一步的收益：它只让 Portrait Studio 把我们的 `Textures\*.png` 当作可选图源列出来，
**不等于**能让玩家替换我们的立绘（那要走 writer 协议，也就是那个已删的包）。
在没确认"玩家确实想在 Studio 里换我们 NPC 立绘"之前，**不要为了被识别而加一个我们并不需要的补丁** ——
多一个缝就多一个游戏更新后会炸的地方。

---

## 四、踩坑

**① 我们自己的立绘（主模组链路）**

| 坑 | 现象 | 正确做法 |
| --- | --- | --- |
| 用错的 PPU/pivot | 立绘大小位置和原版不一致 | PPU/pivot 取**我们自己注册进 `mod.Sprites` 的实际值**（`dnpc_*` 694.8、`sw_*` 706.49、pivot (0.5,0.657)），不要按 PNG 尺寸猜 |
| 贴图放错目录 | `SetSpriteShape` 找不到 key，物品/立绘保持旧样子 | 立绘走 `Textures\<key>.png`，由 `ArcAssets` 收进 `mod.Sprites[key]`；日志里会打 `no sprite 'xxx'` |
| 透明像素留着绿幕色 | Point 采样从相邻透明像素取色，边缘泛绿 | 对透明像素做边缘色扩散（alpha 不动），别用缩放/羽化去糊 |

**② 只有在做独立包时才会碰到的（现在不做包了，留作工具库知识）**

| 坑 | 现象 | 正确做法 |
| --- | --- | --- |
| 手写清单 | 尺寸/哈希/几何和实际文件对不上，`validate.py` 失败 | 一律用脚本从 PNG 算 |
| `build.py --output` 放在 `--source` 里 | `Output must be outside source` | 成品目录放源码树外 |
| 以为产出的包是零能力 | 装上去 `running=False trust=NotChecked`，功能全无 | SDK 用 `File.ReadAllBytes`，包**必然带 `files`**，首次必须人工批准 |
| 改了包内容指望自动生效 | `trust=Changed` → 不加载 | 有能力模组每次内容变更都要重新批准；只有零能力模组能 `trust=Updated` 自动接受 |
| 把自动切换的多个 key 塞成一个 target 的多个候选 | 自动表情被压成一张固定图 | 每个 sprite key 建一个 target |
| 备份 DLL 放在 Mods 目录里 | 加载器扫描报 `payload-file: xxx -> executable content`，整个包 `Refused: True` | 备份放项目 `build\` 内，Mods 目录只留运行所需文件 |

---

## 五、协议里 `role` 的三种取值（只与独立包有关，留档）

- `writer` / `both`：**有 `targets`**，发 writer hello、要 acquire/commit → 必须编 SDK
- `resources`：**根上不允许 `targets` 或 `binding`**，不发 hello、不 acquire → 只表示"可供图"

想让新角色出现在可选列表里绕不过 `writer`；`resources` 只能给已存在的 target 当候选。
**我们不做包，这一节暂时用不上**，留着是因为以后如果要接别的管理器可能还会碰到同一套语义。

---

## 六、待补

- [x] ~~银狼四态按静态 target 接入后实测是否跟随~~ —— 独立包已删，这条作废
- [ ] 8 个 NPC 目前是 1:1 复制原图，之后可换高清差分（**改 `Textures\*.png` 即可，主模组零能力，不涉及批准**）
- [ ] 如果哪天确认"玩家确实想在 Portrait Studio 里换我们 NPC 的立绘" → 再按第三节·补做特性 patch，或重新评估独立包

## 七、立绘包规格与美术模组三级形态（2026-11 二轮蒸馏，工坊实测）

完整版见 `workshop-distill\美术与角色模组三级形态与立绘包规格.md`。这里只留**动手要用的硬参数**。

### 7.1 立绘图片规格（原版与主流美术包共用口径，照这个做就对得上）

| 项 | 值 |
| --- | --- |
| 尺寸 | **1728 × 1266** |
| `pixelsPerUnit` | **600.0** |
| pivot | **(0.5, 0.4976303317535544)** —— 不是 0.5，别四舍五入 |
| Filter | **Point** |
| Wrap（U/V/W） | Clamp |
| mipmaps | **none** |
| colorSpace | sRGB |
| meshType | FullRect |

> 依据：`Non-AI NPC Portraits`（3846193221）的 `portrait-studio-provider.json` 里 94 个资源全部是这组值。

### 7.1b 另一套口径：直接注册进 `SpriteDict` 时（樱咲羽衣路线，⚠️ 两套别混用）

`樱咲羽衣·诊所`（3844162371）是**自己** `Sprite.Create` 然后把 sprite 直接塞进 `SpriteDict.spriteDictionary` 的，它用的是另一套公式：

```csharp
Sprite.Create(tex, rect,
    pivot: new Vector2(0.5f, 136f / 175f),          // ≈ 0.7771
    pixelsPerUnit: texHeight * 100f / 175f,          // ≈ 613（1466×1073 的图）
    extrude: 0,
    meshType: SpriteMeshType.FullRect);
```

含义：**让立绘在世界里恒为 1.75 单位高**（因为 ppu 的定义是"每单位多少像素"，100 px/unit 是常数）。它实测 15 张都是约 1466×1073 RGBA、非像素日系半身、长宽比 1.366；配角 288×211 套同一公式也成立。

**两套口径为什么不同、该用哪个**：

| 场景 | 用哪套 |
| --- | --- |
| 走 Portrait Studio 提供方协议（`portrait-studio-provider.json`） | 1728×1266 / PPU 600 / pivot 0.4976（§7.1） |
| 自己 `Sprite.Create` 后塞进 `SpriteDict` | 任意 w×h + `pivot=(0.5, 136/175)` + `ppu = h*100/175` |

> ⚠️ **待核**：我们旧笔记里记过 `pivot=(0.5, 0.657)`，与上面两个数都不同。三套值并存说明**原版立绘的真实 pivot 还没被我们核实过**。做立绘包之前必须去 `共享开发\reference\Assembly-CSharp\SpriteDict.cs` / 原版立绘资源里核对一次，否则脸会上下错位。已记入 `07-踩坑台账` 待办。
>
> 补充（2026-11-27 已核到这一步）：**看 png 无法确定 pivot** —— 原版立绘是 Unity Sprite 资源，`pivot` / `pixelsPerUnit` 编码在资源元数据里，不在 png 里；原版 `ModLoader` 给**模组贴图**建 sprite 时用的是 `pivot (0.5,0.5)` + `ppu 100`（`ModLoader.cs:1749/1775`），那是"模组贴图"的口径，不是立绘的口径。
> 所以**唯一可靠的确认方式是真机读**：进档后用桥接器/探针读
> `SpriteDict.Instance.spriteDictionary["femaleLower1"].pivot` 与 `.pixelsPerUnit`（顺带 `.rect`、`.texture.width/height`），一次就能定死。这条已列入真机待办。

### 7.1c `SpriteDict` 的立绘其实分池存放（做替换/自愈时必须知道）

`SpriteDict` 不是一个字典，而是**一个通用列表 + 十几个分池列表**，全部是 `private List<SpriteEntry>`：

```
spriteEntries（通用）
scavMale/scavFemale   secMale/secFemale   medMale/medFemale   engMale/engFemale
secCorpoMale/secCorpoFemale   lowerMale/lowerFemale   revMale/revFemale   wanted…
```

`SpriteEntry { string key; Sprite sprite; }`。

这解释了两件事：
1. **少女当铺为什么要反射遍历"所有 `List<SpriteEntry>` 类型字段"** —— 它不知道（也不该硬编码）立绘按池拆成这么多列表，遍历同类型字段是最稳的做法。
2. 我们/框架那种"Patch `SpriteDict.GetSprite(key)`"的做法之所以能工作，是因为对外只有 key→sprite 这一层；但**要枚举"原版有哪些立绘 key"就必须把所有分池都扫一遍**。

### 7.2 三级形态（选方案前先看这张表）

| 级别 | 做法 | 代码量 | 冲突面 |
| --- | --- | --- | --- |
| L1 单点装饰 | `SceneManager.sceneLoaded` + 自建组件挂到某个场景物体 | 3 文件 / ~450 行 | 几乎零 |
| L2 全店换脸 | **只换 `SpriteRenderer.sprite` 引用，不碰 `SpriteDict`** | 15 文件 / ~800 行 | 低 |
| L3 完整角色模组 | 自定义 NPC + 剧情 + 功能地点 | 58 文件 / 数千行 | 中 |
| 纯资源包 | 按 `portrait-studio-provider.json` 放图 + 薄 provider DLL | 0~1 个薄 DLL | 零 |

### 7.3 「换脸」要照抄的三条（来自少女当铺 CutePortraits）

1. **不修改 `SpriteDict` 字典**，只在需要时 `renderer.sprite = 我们的图`。这是与"Patch `SpriteDict.GetSprite`"的本质区别——后者会和所有同类模组排队打架（我们现在就是这样，还被迫加了兜底 Postfix）。
2. **替换前先避让**：扫 `ModLoader.Mods` 的 `Textures` / `Sprites` / `PaperSprites`，当前 `texture` 若属于别的模组就跳过。
3. **还原要写成条件式**：`if (renderer.sprite == 我们换的那张) renderer.sprite = 原图;` —— 否则会把别人后来换的图一起还原掉。

### 7.4 命名约定

- 传统 `Textures/` 包：文件名 = key，形如 `cp_portrait_<原版立绘key>.png`（`cp_portrait_femaleLower1.png`）
- 批量生成包：`<原版key>_<作者前缀>_<子池>_<f|m><NN>.png`（`femaleScav_psrl_scav_f01.png`）——**带作者前缀避免撞名**
- Portrait Studio 包：`Portraits/<key>/handdrawn.png` + JSON 描述
