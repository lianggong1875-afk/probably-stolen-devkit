# 立绘框架：按需加载 + 套装回退 + 高清去像素（Immortal Night Bazaar 的 yg_regulars）｜工坊 3845197390

> 蒸馏自 `缓存文件夹\decomp_ws\3845197390`（38 个 .cs / 4472 行，"Immortal Night Bazaar - Core"，即作者口中的 `yg_regulars`）。
> 价值：**一个"只有程序、美术另发订阅项"的立绘框架**——它怎么按需读图、怎么在缺套装时回退、怎么关掉像素相机做高清，全都可以直接搬到我们自己的立绘接入上。
> 注意：本档是**结构化侦察 + 关键调用核对**，没有通读全部 4472 行，标「（推测）」处勿当定论。

## 1. 它的分发结构（对我们最有启发的一点）

作者把立绘拆成**一个 Core（只有逻辑）+ 三个纯美术订阅项**（serene / adult / extreme）。Core 里有一条铁律：

> 缺套装时不崩、不空白，按 `extreme → adult → serene` 回退。

代码证据：

```csharp
bool extreme = PortraitSets.Has("extreme");
bool adult   = PortraitSets.Has("nsfw");
bool serene  = PortraitSets.Has("sfw");
// 实际取哪套
string set = PortraitSet == "extreme" && extreme ? "extreme"
           : PortraitSet != "extreme" ? (SpicyVoices ? "nsfw" : "sfw")
           : "extreme";       // 兜底仍是 extreme，外层再判 has
Log("[yg_regulars] 立绘走按需加载：extreme=" + extreme + " nsfw=" + adult + " sfw=" + serene
    + "，只有被显示的那几张才会从内容包读进来");
```

**这是"内容可拆包"的正确做法**：逻辑一次发布，美术按需订阅；玩家只订喜欢的包，缺包自动降级。我们做立绘/贴图扩展时也应该这样拆（body 零能力，美术包独立）。

## 2. 按需加载：只读被显示的那几张

```csharp
byte[] png = PortraitSets.Read(set, key);              // 从内容包读原始 png 字节
byte[] menu = PortraitSets.ReadMenu(set, key);         // 菜单美术同理
...
Texture2D tex = ...;                                   // 由 png 解出（内部实现未逐行核对）
Sprite sp = Sprite.Create(tex,
    new Rect(0f, 0f, tex.width, tex.height),           // 整图
    new Vector2(0.5f, 0.5f),                           // 中心锚点
    pixelsPerUnit, 0u, SpriteMeshType.FullRect);
```

要点：
- **`PortraitSets.Has(set)` 只做"存在性探测"，不加载**；真正 `Sprite.Create` 发生在该立绘要被显示时。
- 好处：一套立绘几百张，玩家一辈子也看不完全部；不预加载就没有"进游戏卡十秒"的问题。
- 这是**我们做立绘包时必须学的**：不要 `OnModAssetsLoaded` 里把全部 png 都 `Sprite.Create` 一遍（内存爆炸 + 启动卡顿）。

## 3. 高清去像素：关掉 PixelPerfectCamera（这条最实用）

它有一个 `hd_portraits` 开关，配置项文案（作者原话，直接说明机制）：

> 开：立绘按原生分辨率渲染，同时把游戏的 **`PixelPerfectCamera` 关掉**，并把分辨率切到 `1280×720 / 1600×900 / 2560×1440` 这类非像素档位。
> 关：保留游戏原本的像素相机与分辨率（立绘会被像素相机再次放大，边缘变粗）。

- **改动下次启动生效** —— 因为分辨率与相机是启动期配置，不能中途改。
- 这与我们经验库里 `08-立绘接入与PortraitStudio` 记的"零反射高清去像素化租约"是同一件事的另一种实现：**工坊里至少有两条去像素路线**（租约式 / 直接关 PixelPerfectCamera + 改分辨率）。

## 4. 它覆盖的"美术表面"清单（做美术包时要考虑的全部落点）

| 文件 | 覆盖什么 |
| --- | --- |
| `PortraitReplacer`(627) | 顾客/角色立绘 |
| `WantedPosterReplacer`(391)、`WantedBoardReplacer`(243) | 通缉令、公告板 |
| `ShopSceneReplacer`(325) | 店内场景美术 |
| `MenuArt`(289) | 主菜单/加载图 |
| `ClientVoice`(411) | 顾客配音（Dialogue 侧） |
| `KeyTable`(411) | 立绘键表（哪个角色哪些差分用哪个 key） |
| `ClientNormalize`(191) | 客户数据归一（把原版顾客映射到自己的立绘键） |

**这张表本身就是"一个立绘包要准备什么"的清单**：不止角色立绘，还有通缉令、公告板、场景、菜单、配音、键表、客户映射。

## 5. 配置项设计（值得抄）

它的每个配置项都带**长文案说明**，且明确写了"这是不加载，不是删文件"：

```
drop_nsfw_set : 只用清雅版：不再加载 NSFW 主套的贴图，一律用清雅立绘包
  → 注意：这是「不加载」，不是「删文件」——硬盘上的占用还在
    （本模组不读写模组目录以外的文件，这是游戏反作弊的硬要求）
  → 想真正腾出硬盘：在 Steam 取消订阅再重新订阅只含程序的版本，或手动删 png
  → 即时生效
hd_portraits  : 高清立绘：临时关掉像素相机
  → 改动下次启动生效
```

两个可学的点：
1. **明确区分"不加载"与"删文件"**，并解释为什么（不读模组目录以外的文件 = 反作弊硬要求）。
2. **标注生效时机**（"即时生效" / "下次启动生效"），减少玩家困惑。

## 6. 对我们的可用点

1. **立绘包按需加载**：我们给"可能是群友"做立绘（或其他美术包）时，用"存在性探测 + 显示时 `Sprite.Create`"，别在加载期全量解码。
2. **套装回退链**：`高配 → 中配 → 基础`，缺包自动降级不空白；配置项里给"只用哪一档"的开关。
3. **高清方案**：要么走 `08` 里的租约式去像素，要么照这里"关 `PixelPerfectCamera` + 切非像素分辨率（下次启动生效）"。
4. **美术包清单**：直接照 §4 那张表准备资产（立绘/通缉令/公告板/场景/菜单/配音/键表/客户映射）。
5. **配置文案规范**：每条写清"做什么 / 为什么 / 生效时机 / 副作用"。

## 7. 待验证 / 风险

- `PortraitSets.Read` 的内部（怎么从内容包按路径读 png、有没有缓存、有没有尺寸校验）**未核对**，标「（推测）」。
- 关闭 `PixelPerfectCamera` 是否会影响其它模组/原版其它像素美术（它只在自己开关打开时关，且需要重启），未真机验证。
- 该模组自带 `WantedElement.OnEnable/Start` 两个 Patch（见透视报告），说明通缉令替换需要拦原版生命周期；我们若做同类替换要注意时序。
