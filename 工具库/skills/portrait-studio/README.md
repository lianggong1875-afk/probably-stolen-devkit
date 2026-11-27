# Portrait Studio 工具包（我们的用法索引）

来源：Steam 工坊 `3840289739` 的 `立绘\`。搬入 `skills\`(4 套) + `protocol\` + `examples\` + 两个入口 md。

**没搬**：`tools\`（25 MB，自带 python 运行时的构图工具）和 `图文介绍\`（6 MB docx 手册）。要调构图就直接双击工坊里 `立绘\启动立绘调整工具.cmd`。

---

## 一、先判断走哪条路线（选错要重做）

只看两个问题：**最终写图的人是谁**、**立绘会不会自己变**。

| 你的模组现在是什么状态 | 用哪套 | 落地形态 |
| --- | --- | --- |
| 立绘由**游戏原版**渲染，模组只提供 sprite 名字（**深空电玩就是这种**） | **什么都不用做** | 见下面「最重要的一条」——主模组自己就够，别做独立包 |
| 确实要让**第三方管理器**能替换/托管这些立绘 | `portrait-studio-pack` | 独立包模组，模板自带 StaticBridge，按 `binding: sprite-key` 绑 key |
| 模组**自己持有** SpriteRenderer/Image 并自己写图 | `portrait-studio-adapter` | 在原工程编入 SDK，加静态接口 |
| 愿意把显示和大小位置**全部交给管理器** | `portrait-studio-managed` | 管理器负责最终写图 |
| 立绘会**自动变表情 / 播动作 / 按剧情换图** | `portrait-studio-dynamic` | 保留自己的动画，增加 `dynamic-display-control/1` 交接 |

**混合模组必须分开声明**：静态位置走 pack/adapter，会自动变的那部分走 dynamic；同一显示位置不能同时登记静态 writer 和托管。

### 最重要的一条（2026-11-26 定案，花钱买来的）

**「立绘由游戏原版渲染、模组只提供 sprite 名字」这个状态，不需要做任何包。**

深空电玩曾经按 `portrait-studio-pack` 做过 `deepspace_arcade_portraits`（12 图 + 116 KB DLL，带 `files` 能力），
装上去在 Modding 界面卡了三轮批准，最后**删掉了**。原因是主模组零能力已经把显示做完了：

```csharp
// 主模组自己干的事，全程零能力
mod.Sprites[key] = sp;              // ArcAssets.cs：把 Textures\*.png 收进模组贴图表
c.spriteName = Face(face, …);       // ArcClients.cs：客人立绘指过去
// 游戏 SpriteDict 自己查表渲染，就这样
```

独立包唯一多出来的是「让装了 Portrait Studio 的玩家能替换我们 NPC 的立绘」，代价是：
多一个模组要同步版本、带 `files` 每次改图都要人工批准、离开 Portrait Studio 就只是在重复劳动。

**判断口诀**：先问「这个包离开对方模组还剩下什么」。剩下的是改画面设置之类不读文件的事，
就零能力并进主模组；剩下的没有，就别做。

真要「被 Portrait Studio 认出来」也不必做包 —— 用**特性式** `[HarmonyPatch(typeof(SpriteDict), "GetSprite")]`
即可，它的 `HasSpriteDictGetSpritePatch` 扫 `CustomAttributeData` 会命中（手写 `harmony.Patch()` 扫不到）。
但这只让它把我们列为图源，不等于能让玩家替换，收益有限，别为它加没必要的缝。

---

## 二、标准流程（**只在确实要做独立包时**才用）

⚠️ 深空电玩不需要走这套，见上面「最重要的一条」。

1. **建工程**
   ```
   python scripts\new_pack.py --output <新目录> --id <唯一ID> --name <名称>
   ```
   目录必须不存在。`--id` 会被写成程序集名 `PortraitPack_<sha256(id)>`，同时改写 csproj 与 manifest，**别手改**，否则和 Unity 的 MonoBehaviour 标识对不上。

2. **放图**：`Portraits\<sprite-key>\*.png`
   - **一个 sprite key 一个文件夹**；同一个 key 放多张 PNG = 多候选（差分就放这里）
   - 文件名随意，`default.png` 只是默认候选的名字

3. **生成清单**：**不要手写** `portrait-studio-provider.json`
   - 每张图要 `width/height/contentHash`(PNG 的 SHA256) + `geometry`(rect/PPU/pivot/border)，手写必然和文件对不上
   - 用本目录的通用生成器（扫描 `Portraits\<key>\*.png`，自动算尺寸/哈希）：
     ```
     python gen_manifest.py --source <包工程> --ppu-map "dnpc_:694.8,sw_:706.49" --pivot "0.5,0.657" --set-name "<套装名>"
     ```
     `--ppu-map` 是 `前缀:ppu` 逗号分隔、**最长前缀优先**；`--ppu` 给全局默认。PPU 必须取目标模组实际注册值。
     幂等，换图后重跑即可。

4. **校验**：`python scripts\validate.py <源码目录>` → 必须 `PASS`

5. **构建**：
   ```
   python scripts\build.py --game-root <游戏目录> --source <源码目录> --output <成品目录>
   ```
   `--output` **不能在 `--source` 里面**，否则 `Output must be outside source`。成品 = manifest + DLL + 清单 + 图片。

6. **装机**：整目录复制到 `Mods\<包名>\`

7. **人工批准**：见第四节。

---

## 三、关键约束（全是踩出来的）

- **PPU / pivot 必须用目标模组实际注册的值**，否则立绘大小会变。
  深空电玩：`dnpc_*` 1470×1070 ppu=694.8、`sw_*` 796×1088 ppu=706.49，pivot 都是 (0.5, 0.657)。
- 产出的包**必然带 `<Capability>files</Capability>`**（SDK 用 `File.ReadAllBytes` 读图），属有能力模组。
- `subject.owner: "game"` + `binding: {type:"sprite-key", key:"<key>"}` = "替换这个 sprite key" —— 这正是"游戏原版渲染 + 模组提供 sprite 名字"的接法。
- **会自动切换的多个 sprite key 各建各的 target**。例如银狼 `sw_portrait/happy/sad/afraid` 是四个独立 key、由模组按剧情切换，就建四个 target；塞成一个 target 的四个候选会把自动切换压成一张固定图。
- 清单里 `role` 只有三种：`writer`（有 targets，要发 hello/commit）、`resources`（**只供图，根上不允许 targets**）、`both`。想让新角色出现在可选列表里，**只能 writer/both**。

---

## 四、信任：有能力模组必须人工批准

看 `%LOCALAPPDATA%Low\Questing Goose Studio\Probably Stolen\mod-audit.log` 里这个模组那行：

| 状态 | 含义 | 处理 |
| --- | --- | --- |
| `trust=NotChecked` | 首次安装，未批准 | **不会加载**，需人工批准 |
| `trust=Changed` | 包内容变了（含哈希） | **批准失效**，需重新批准 |
| `trust=Updated` / `trust=Match` + `running=True` | 正常 | 无需处理 |

批准只能在游戏的 **Modding 界面**点，**脚本不能代批**（AGENTS.md 明令禁止绕信任）。

对照：**零能力**模组（`caps=none` + manifest `<Capabilities />`）更新走 `trust=Updated`，可自动接受 —— 所以自家的零能力模组改代码不用每次批准。

---

## 五、脚本清单

| 脚本 | 作用 |
| --- | --- |
| **`gen_manifest.py`**（本目录，我们写的通用版） | 扫 `Portraits\<key>\*.png` 生成整份清单：自动算尺寸/SHA256/几何，支持 `--ppu-map` 按前缀分 PPU |
| `skills\portrait-studio-pack\scripts\new_pack.py` | 从模板生成新包工程 |
| `skills\portrait-studio-pack\scripts\validate.py` | 校验清单与图片一致性（尺寸/哈希/几何） |
| `skills\portrait-studio-pack\scripts\build.py` | 编 DLL 并摊平成品目录 |
| `skills\portrait-studio-pack\scripts\framing.py` | 拖动/缩放/选锚点调构图（GUI 在工坊 `tools\`，未搬） |
| `skills\portrait-studio-managed\scripts\new_managed.py` | 托管路线工程生成 |
| `skills\portrait-studio-dynamic\scripts\validate.py` | 动态路线校验 |

## 六、协议速查

| 要做什么 | 读哪个 |
| --- | --- |
| 静态资源公开协议（字段全解） | `protocol\静态资源公开协议.md` + `protocol\portrait-studio-static-v1.schema.json` |
| 动态立绘交接 | `protocol\动态立绘接入协议.md` + `protocol\portrait-studio-dynamic.schema.json` |
| 托管 | `protocol\托管显示协议.md` + `protocol\portrait-studio-managed.schema.json` |
| 原版职业 / 图片键 | `protocol\vanilla-client-roles.tsv`、`protocol\characters.tsv`、`protocol\原版职业与图片键.md` |
| 图片预算与转换 | `protocol\图片处理与预算.md` |
| 可构建范例 | `examples\范例入口.md`、`protocol\static-examples.json` |

方法论与踩坑记录见 `共享开发\模组制作经验库\08-立绘接入与PortraitStudio.md`。
