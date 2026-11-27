# 3831439549 More Device Upgrades（机器升级·非官方更新）｜接手移植档案

> **工坊 id**：`3831439549` · `more_device_upgrades` · ModVersion **1.9.111** · 署名 HeXiaolu（Nexus 上 `MoreUpdate`（原 836838507）的**修复与移植版**）；包内 `manifest.xml`，`<Capabilities />` 为空是作者刻意写的（注释称只用 `AccessTools.FieldRefAccess<typeof,typeof>("字面量")`，不算运行期算名）。
> **反编译目录**：`缓存文件夹\decomp_ws\3831439549\`（253 个 `.cs` / **43,064 行** + `Properties\AssemblyInfo.cs`；透视报告 `缓存文件夹\decomp_ws\_reports\3831439549.txt`）。
> **为什么值得读**：全工坊唯一把「机器二级升级（互斥路线 + 拖料 + 进度）」「坏机器修理」「NPC 剧情链」三件事都做成可玩闭环的模组，而且它是**在别人的 40k 行代码上做移植**——我们要的机器改造接口、要避开的模块重算陷阱、要抄的"用物品 tag 当存档"手法，全在这一个包里；它踩过的版本适配坑（贴图丢失、形状丢失、尺寸丢失）我们也正在踩。

## 1. 规模与结构

### 1.1 硬数字

| 项 | 值 |
| --- | --- |
| 源文件 | 253 个 `.cs`（另 1 个 `AssemblyInfo.cs`），全在单目录 `MoreDeviceUpgrades\`，无子命名空间、**全部 `internal`** |
| 行数 | 43,064（反编译器计入 `//IL_xxxx` 噪声；实际逻辑约 4.0 万行） |
| Harmony | **178 处特性式 `[HarmonyPatch]`**（169 文件）+ **6 处手动 `new Harmony().Patch(...)`**（`CartelBox`/`DrServices`/`LayoutRefresh`/`ModuleGuard`/`RasXlt`/`TooltipUpgrade`）；纯 Postfix 121 文件、纯 Prefix 28、双挂 3，其中 **25 个文件的 Prefix 会 `return false`**；**0 Transpiler** |
| 资源 / 官方接口 | `Textures\Items\`（3 张坏机器图 `broken_*.png`、熔炉 4 套皮肤 on/off 共 8 张）、`Localization\{en,zh,ru,pt}.csv`（约 740 key × 4）、`Sounds\mesu_shot.wav`、`Videos\mesu_shoot.mp4`； `ModHook` 6 种（`OnGameLoadedInit`×9、`OnHandlingNightlyServicesLate`×4、`OnNewGameStarted`×3、`OnGameLoadedNormal`、`OnCreateTooltipLate`、`OnPlaceInventorInventoryItemLate`）；`ModHelper` 15 个（`AddNetworkUpgrade`/`GetModData*`/`GetLocalized`/`PlaySound`/`CreatePaper`/`SetNetworkTabLabel`…） |
| 热重载 | **无** `HotReloadPrepare/Activate`（报告第 4 行「否」）——这是它对我们最大的架构缺口 |

### 1.2 目录→行数→职责

| 域 | 文件 / 行数 | 职责 |
| --- | --- | --- |
| 鱼缸 `RasXlt*` `RasUp` | 33 / 4,064 | 循环水养殖：水体/滤食/幼崽/切鱼/肉 kcal（全模组最大单域，24 个 `*Patch.cs` 围绕一个鱼缸） |
| 文案与界面 `TooltipUpgrade` `UILoc` `AdvUi` `PanelGrid` | 7 / 3,906 | 手搓 tooltip 富文本（1,059 行）、手搓双语兜底表（2,092 行）、标题栏按钮与菜单（773 行） |
| `R917*` 杰克逊博士终章 | 8 / 3,814 | 全屏剧情：开枪（`StoreUIManager.Shoot`）、警察到场、TMP 三层气泡、播放 mp4/wav |
| 水培 `Hydro*` | 15 / 3,584 | 四套模块 + 高级路径（内循环/巨大作物/保种） |
| **高级路径核心 `Adv*`** | 5 / 3,398 | 状态机、配方表、锁定、效果应用、邻接热管理（**本文主角**） |
| 引擎 `Engine` `Defs` `Core` `HardMode` | 7 / 2,393 | 基础升级表（22 条 `BonusDef`）、tag 读写、属性重算、困难模式 |
| 医生服务 `Dr*` | 19 / 2,337 | 23 条 `NetworkUpgrade`（`drjackson` 标签页）、恩惠卡、UI 行/卡 |
| 改造人剧情 `Aug*` | 24 / 2,301 | 捕获器/改写器/复制器三件道具 + 2/6/10/13 阶段剧情 |
| 冰箱 `Fridge*` | 10 / 1,986 | 分温区、闪冻、甜品机 |
| 化学 `Chem*` `Purifier` | 14 / 1,789 | 成型机/搅拌机/加热板/净水器 |
| **坏机器修理 `MachineRepair` `RepairUi` `Broken*`** | 5 / 1,446 | 修理框架（**第二主角**） |
| 充能器 `Recharger*` | 7 / 1,392 | 电池仓、快充、售电 |
| 饲养 `Cage*` `Feed*` | 14 / 1,376 | 笼子扩容/疾病/免疫、饲料机 |
| 拾荒商 `Scav*` | 11 / 1,102 | 拾荒客黑市交易链 |
| 药盒 `Pharma*` `CartelBox` | 5 / 1,014 | 药盒拆包 + 卡特尔「灰尘」闭包补丁 |
| 打印机 / 解序 / 熔炉 / 批发商 / 酒架 / DNA / 警报 | 8+4+4+6+7+8+6 / 5,664 | 各 600~970 行 |

**行数怎么花掉的（回答第 6 问）**：**不是**"每台机器一个类"。配置全是**数据表**——基础升级一张 `Defs.All = new BonusDef[22]`（`Defs.cs:7`），高级路径一张 `AdvRecipe[]` per family（11 张表，`Adv.cs:110`~`:1187`）——两台表加起来不到 1,200 行。真正吃掉 43k 的是三块：**① 每台机器 1~4 个行为改写模块**（鱼缸 4,064 / 水培 3,584 / 冰箱 1,986 / 充能器 1,392 …）；**② 自绘 UI 与文案**（tooltip + 双语表 + 标题栏 = 3,906）；**③ 一条全屏剧情链**（R917 = 3,814）。补丁层反而极薄：161 个 `*Patch.cs` 合计 5,782 行，平均 36 行/个（165 个文件 ≤ 60 行）。

### 1.3 冻结副本判断：三查三否

| 查什么 | 结论 | 证据 |
| --- | --- | --- |
| 有第三方库/旧版代码被编进去？ | **没有第三方库、没有整份旧代码，但有大量死开关/死数据** | 全目录单命名空间 `MoreDeviceUpgrades`； `Adv.AllEnabled = true`、`Adv.LockSingle = false`、`Adv.OriginalMask = {6,4,7}`（`Adv.cs:104-108`，只在 `AllEnabled == false` 时生效）、`Furnace.IngotSwapWhenFull = false`（`Furnace.cs:15`，`if (!IngotSwapWhenFull)` 两处分支永久走同一侧）、`Furnace.ExtentDiagnosed`（诊断残留） |
| 有"数据在、效果没写"的空壳？ | **有 1 处整表空转 + 若干空 case** | `PurifierRecipes`（3 条配方，`Adv.cs:227`）永远选不到也吃不到料：`Adv.Implemented()`（`Adv.cs:1293`）把 `Purifier` 排除、`ApplyEffect` 没有 `Purifier` 分支；`ApplyEffect` 里 `case 3: break;`（`Adv.cs:1663/1679/…`）是永不出现的 dir |

> 结论：**这是一个"活代码"包，可放心当参考实现读；但要记住三处"看着有、其实没有"：净水器高级路径、模块化熔炉配方（dir 8）、`AddBonus` 的参数**（见 §4 陷阱 1）。

## 2. 它怎么做（点名到原版类与方法）

### 2.1 骨架：三层 + 一条"重算而不累加"的主轴

```
拖拽层  ItemMouseDragHandler.Drag/StartDrag/EndDrag (Prefix/Postfix) + DragState 三态旗
        ├─ GameItem.TryFindOneValidInventorySlot / MayHaveValidInventorySlot  → 回答"能不能放进这台机器"
        └─ SlotMarker.TryAcceptOnce                                            → 回答"放进去转几件"
结算层  Engine.HandleDrop(machine, material)                                    → 唯一的落料总入口
        ├─ Defs.All 22 条基础升级（每件 +N 次）　└─ Adv.TryFeed 高级路径逐材料投喂（每件 +1 或 +纯度进度）
存档层  物品自己的 TagSystem：item.state / item.modifiedState（Engine.GetInt/SetInt）；**属性不累加**，
        靠 Engine.SyncTotals 从私有 tag 重算进原版 TOTAL_PERCENTAGE_*（§3 摘录 E）
```

关键点：**原版机器属性标签是"计算结果"而不是"存款"**。`TOTAL_PERCENTAGE_QUALITY_BONUS_INT` 这三个常量由原版 `ModuleHelper` 拥有（`共享开发/reference/Assembly-CSharp/ModuleHelper.cs:59`、`MachineryHelper.cs:427` 读取），装/卸模块时 `ModuleHelper.ComputeModuleEffect` 会整体重写它们。所以本模组从不"加一点进去"，每次都是 `模组贡献 + 本模组贡献` 重算一遍，并额外用 `ModuleGuard` 补一刀（§3 摘录 E）。

### 2.2 高级升级路径：三选一锁定 + 拖料 + 进度条（第 1 问）

#### ① 状态存在哪：**机器物品自己的 tag 里，一个 int 当位掩码**

| tag | 键名 | 语义 |
| --- | --- | --- |
| 选择掩码 | `MOD_FURNACE_ADV` | 位掩码：`1`=并行冶炼 `2`=热管理 `4`=应用结晶学 `8`=模块化（`Adv.cs:54-94` 定义 `FridgeDir*`/`HydroDir*`/`PrinterDir*`… 全部是 1/2/4(+8)） |
| 完成掩码 | `MOD_FURNACE_ADV_DONE` | 同 bit 语义，完成即置位（`Adv.cs:1615-1622`）；模块化另有一键 `MOD_FURNACE_MODULAR_DONE`（0/1）给 `SlotCount` 用 |
| 每材料进度 | `MOD_FURNACE_ADV_PAR_INGOT` 之类 | 一条材料一个 int，值 = 已投件数/纯度进度（`Adv.cs:179/187/203/211/219`） |

**没有独立存档表、不碰 ES3、不写 `ModData`**（剧情链才用 `ModData`，见 2.5）。`Engine.GetInt/SetInt`（`Engine.cs:133/220`）双写：先 `item.ModifyTag(tag, ts => ts.Enable().SetInt(v), true)`，再 `item.modifiedState.GetTag(tag).Enable().SetInt(v)`——**两个 TagSystem 都写**，读时先 `state.IsTag` 再退回 `item.IsTag / GetTagReadonly`（`Engine.cs:142-183`）。这是它能"存进存档、读档还在、还能被其它模组读到"的全部秘密，代价是写失败会双份日志（`Engine.cs:202/215`）。

#### ② "三选一"到底有哪几个按钮

`AdvUi.Show` 遍历 `Adv.Table(family)`，**只给 `Adv.IsEnabled(family, dir)` 为真的路线生成按钮**（`AdvUi.cs:126-137`）。熔炉：

```csharp
// Adv.cs:1321-1326
internal static bool IsEnabled(AdvFamily f, int dir)
{
    if (f == AdvFamily.Furnace && dir == 8) return false;   // 模块化配方被硬关；其余族按 dir 位判定 }
```

于是熔炉**只显示 3 个按钮**（1 并行 / 2 热管理 / 4 结晶学）——这就是"三选一"的来源。第 4 条（模块化，`Adv.cs:112-144`）数据还在，但被 `dr_modular`（医生网络升级 1200 信用点，`DrServices.cs:185`）取代了：`SlotCount` 读的是 `DrServices.ModularUnlocked()`，不是配方完成位（`Adv.cs:1416-1422`）。**（推测）** 作者把"第二槽位"的获取方式从"熔炉自己吃 2 台熔炉 + 500 进度铁锭"改成了"向医生买"，旧配方留着没删。

#### ③ 锁定怎么实现（**软锁在 UI，硬锁在数据**）

"选/取消"唯一入口是 `ToggleSelect`（**全文见 §3 摘录 A**）：已完成位拒绝改动；已选未完成位允许取消；槽满时踢掉最低位那条在途路线（**且不清它的进度 tag**，换回来接着用）；多槽机必须先做完一条才能选下一条。

- `SlotLimit`：熔炉 `SlotCount`（1 或 2）、充能器 1/2（医生 `dr_recharger_slot`），**其余族恒为 1**（`Adv.cs:1425-1440`）。
- "选完就不给改"其实是三层叠加：**a.** `AdvUi.Show` 里 `flag2 = IsChosen && SlotsFull`，一旦在途或完成，菜单切成"已选清单"模式，**根本不渲染按钮**（`AdvUi.cs:117-142`）；**b.** `ToggleSelect` 拒绝已完成位；**c.** `SelectionLocked`（`Adv.cs:1881`）只在 `LockSingle` 为真时参与 `AdvancedUnlocked`，而本版 `LockSingle = false` —— 所以**数据层是"可以取消"的，锁是 UI 锁**。想做成真硬锁，把 `LockSingle` 打开或给 `ToggleSelect` 加一条 `if (IsChosen) return false;` 即可。
- 防重选还有一条：完成位永不复位，`TryRemoveAll`（螺丝刀）才会清零（§2.2⑥）。

#### ④ 拖料判定：**自己写容器回调，不是 `GraphUtils.CanAccept`**

判定链由三个 Prefix 组成，全部靠 `DragState`（一个静态三态旗）区分阶段：

| 补丁 | 靶点 | 动作 |
| --- | --- | --- |
| `DragPhasePatch.cs:5` + `MayHaveValidSlotPatch.cs:5` | `ItemMouseDragHandler.Drag` / `GameItem.MayHaveValidInventorySlot` | Drag 期间置 `InDragPreview` 不干扰原版；高亮判定能升级就 `__result = true; return false`（骗原版"这里能放"） |
| `TryFindOneValidSlotPatch.cs:5` | `GameItem.TryFindOneValidInventorySlot` | 找落点：`Engine.IsUpgradeablePair` → 返回 `machine.GetIncompleteSlotInInventory()` 并 `slot.numTransfer = 1`（`Engine.cs:1202/1219`）；螺丝刀则走拆除分支 |
| `SlotAcceptOncePatch.cs:5` | `SlotMarker.TryAcceptOnce` | **`__result = 0; return false;`** —— 让原版"成功放入 0 件"，物理上什么都不移动 |
| `EndDragPatch.cs:5` | `ItemMouseDragHandler.EndDrag` | Prefix 置 `InEndDrag`，Postfix 反查落点机器 `lastItem` 再 `Engine.HandleDrop(machine, material)`（`EndDragPatch.cs:174`） |

`IsUpgradeablePair`（`Engine.cs:1102-1200`）就是全部业务判定：机器 id → 材料 id → 排除机器零件 → 修机器优先 → 机型开关 → 打印机/净水器特例 → `FindDef` 命中基础表 → 否则问 `Adv.WantsMaterial`。**好处**：不注册任何自定义容器、不抢 `GraphUtils` 的准入；**代价**：`numTransfer = 1` 是攒出来的 hack（见 §4 陷阱 2）。

#### ⑤ 进度条：**复用原版 tooltip 的富文本，自己画字符条**

没有自绘 UI 控件，也没有进度窗口——"进度条"就是 `RichTextBuilder` 里的一行字符：

```csharp
// TooltipUpgrade.cs:936-938（AppendAdvBlock 内）
int num3 = Engine.GetInt(item, advIngredient.Tag);
int num4 = Adv.NeedOf(advIngredient);
AddLine(builder, UILoc.K(advIngredient.MatKey) + " " + num3 + "/" + num4 + " " + AdvBar(num3, num4),
        (num3 >= num4) ? 8950922 : 4367018);
```

`AdvBar`（`TooltipUpgrade.cs:969`）＝ `max<=10 ? max : 10` 个字符位，实心 `■` / 空心 `□`；`AdvUi.Bar`（`AdvUi.cs:176`）是菜单里的同款。熔炉还在同处叠了"炉衬/传感器"两条原始进度条 `SlotBar` 与已获加成汇总（`TooltipUpgrade.cs:619-666`）。挂点用官方事件 `ModHook.OnCreateTooltipLate`，**若检测到 `item_details` 模组在场则改挂 `ModInternal.CreateTooltipLate` 的 Postfix 抢最后一位**（`TooltipUpgrade.cs:102-133`）。

**⑥ 完成与拆除**：`Adv.TryFeed` 每投一件 → 写进度 → `Engine.ConsumeUpgrade`（销毁 1 件，`StartingPerk.IsTinkerer()` 有 20% 免料，`Engine.cs:982`）→ `AddValueByMaterial`（机器 `unitValue += 材料价×3/4`，`Engine.cs:936`）→ `RecipeComplete` 则 `Complete`（写完成位 + `ApplyEffect` + 刷标题）。拆：把**任意 id 含 "screwdriver" 的件**拖上去（`Adv.IsScrewdriver` 用 `IndexOf("screwdriver")`，`Adv.cs:2083`），`CanRemove` 要求"全部在途路线都已完成"（`Adv.cs:2101`），`TryRemoveAll` 清零所有 tag → 逆效果（第二种植位恢复、警报模组区还原、热管理重算…）→ **材料不退**（`Adv.cs:2104-2203`）。

### 2.3 每台机器的三条路径（第 2 问）

「材料」列为一件/一份的消耗（`Family` 按 tag 族匹配，`Exact` 精确 id）；`→` 后为效果。前置门槛是**该机器基础升级满级**（`Adv.AdvancedUnlocked`，`Adv.cs:1890-1952`）。

| 机器（id） | 前置门槛（基础 tag 满） | ①dir=1 | ②dir=2 | ③dir=4 |
| --- | --- | --- | --- | --- |
| 熔炉 `furnace` | `INGOT_UPGRADES≥9` + `CIRCUIT_UPGRADES≥10` | **并行冶炼** 铁锭×5 + 电子×5 → 过夜多工作 1 次，每 300% 性能再 +1（`Adv.cs:1857-1859`、`TooltipUpgrade.cs:959-963`） | **热管理** 电子×7 + 铁锭×1 + 线缆×2 → 相邻设备 +15%质量/+40%性能/+40%效率；相邻也是熔炉则翻倍 | **应用结晶学** 铁锭×12 + 电路×2 → +1000%质量/+500%性能/+50%效率 |
| 净水器 `water_purifier` | — | 集成式（集水器×2+净水器×1+塑料×10+电子×4） | 工业化（管道枪×2+铁锭进度700+塑料×4+电子×2） | 反应式（铁锭进度700+塑料×10+线缆×2+电子×2） |
| 酒架 `wine_rack` | `CIRCUIT≥10` + `WIRE≥7` | **陈化窖藏** 电子×6 + 塑料×6 + 酒酵母×12 → 发酵中的酒可入陈化器，次日直接发酵完（必定大师年份） | **双效酵母** 电子×4 + 铁锭×8 + 无限酵母×2 → 每夜追加陈化天数 ×2 | （无 dir4） |
| 水培 `hydroponic` | `THERMAL_UPGRADES≥8`+`CIRCULATION≥5`+`Hydroponic.IsDone` | **完美内循环** 电子×6+塑料×6+普通化学品×4+UV 灯×1 → 水量足且纯度≥96% 时按最佳状态结算、永不掉健康；**第二种植位停用** | **巨大作物诱发器** 化学品×8+电子×12+铁锭×4+线缆×3 → 全程不掉健康则额外 1 个巨大作物 | **保种机** 塑料×5+电子×4+化学品×3+线缆×2 → 每次收成返还 1 颗同种种子（不掉健康则 2 颗） |
| 打印机 `printer` | `LATHE≥5`+`CAD≥8`+`SCRAP≥10`+`EXPAND≥9` | **多工位打印臂** 电子×6+线缆×5+焊枪×2 → 每次打印额外 +1 件 | **闭环供料** 螺母×8+线缆剪×3+线缆×6 → 原料减半（向上取整，最少 1） | **微米级打印** 枪械零件×8+电子×5+线缆×4 → 武器成色 +1 档、枪械六项 +5、后坐力控制 +5 |
| DNA 操纵仪 `plant_dna_manipulator` | `PCR≥10` + `CRISPR≥4` | **去芜存菁** 电子×6+线缆×5+普通化学品×4 → 基因复杂度封顶 5 | **定向诱变** 管制化学品×6+电子×4+线缆×3 → 对已有性状再加级时 +1 | **双联测序** 电子×5+线缆×6+塑料×3 → 每次提取额外 +1 张同性状盘 |
| 警报 `security_alarm` | 无（恒开） | **高清摄像头** 电子×5+线缆×4+塑料×3 → 每派系声望 1→2 点/晚，上限 4 派系 8 点 | **专供警戒** 电子×8+线缆×5+铁锭×4 → 模组区缩为 1 枚（多的退回），该派系 6 点/晚 | **专业化报警设备** 电子×6+铁锭×6+普通化学品×4 → 性能 +500%（防盗率 550%） |
| 解序器 `desequencer` | 无 | **去标识制卡** 电子×5+线缆×4 → 产出的卡不带违禁标记 | **自动供卡** 电子×5+线缆×2+塑料×10 → 免空白卡持续出卡 | **双联刻录** 电子×6+塑料×4+铁锭×4 → 输出区 2×4，每次额外 1 张 |
| 成型机 `chem_finisher` | 无 | **源头改良** 电子×6+普通化学品×6+管制化学品×4 → 有效成分需求降到 85% | **污染管控法** 线缆×4+棉布×2+滤水器×2 → 成品纯度阶级 +1 | **精包装** 铁锭×10+电子×4+塑料×10 → 消耗 ×4 原料，产出药盒（4 份，盒价 = 原药×5） |
| 冰箱 `fridge` | `FRIDGE_STORAGE≥7`+`FRIDGE_POWER≥7`+`MOD_FRIDGE_EXPAND≥5`+`MOD_FRIDGE_CABLE≥CableNeed` | （无 dir1） | **冷冻保鲜** 普通化学品×6+螺母×12+电子×6 → 食品可入冷藏/冷冻区，冻过得冷冻加成标签 | **甜品机** 塑料×12+线缆×6+电子×6 → 帧末重建窗口加制冰水槽，过夜把原料转冰激凌 |
| 充能器 `recharger` | `MOD_RECHARGER_CAPACITY≥1`+`FAST_CHARGE≥1` | **电池集装箱** 铁锭×8 → 电池格 4×4 → 4×8（32 格） | **超频效率** 线缆×8+电子×4+铁锭×2 → 夜间充电量再翻倍（3→6→12→24） | **电力供应** 电流转换器×1+线缆×6+电子×1 → 过夜满电溢出按 0.5 信用点/度售出 |

> 表里"三条"对熔炉而言是 1/2/4，第 4 条（模块化）被 §2.2② 关掉；冰箱、酒架只有两条。生成按钮前还会过 `Adv.IsEnabled`，所以**表里有、游戏里没按钮**是常态，读代码时别把表当功能清单。

### 2.4 坏机器修复框架（第 3 问）

**原版没有"修理"，只有"拆解"。** 原版 `MachineBrokenHelper`（`共享开发/reference/Assembly-CSharp/MachineBrokenHelper.cs`）给坏机器 `EnableTag("BROKEN_MACHINE_TAG")` + 三个计数器（螺丝 3 / 线缆 3 / 焊点 4），玩家用**螺丝刀/线缆剪/焊枪**去拆：前两者各返还对应零件并把 `unitValue` 扣 33%，**焊枪最后直接 `machine.Destroy()`**（`:184`）。也就是说原版坏机器 = 废料回收，`ruined` 只用于系统模块（`ModuleHelper.AddStuckRuinedModule`、`RuinedMachineDirectory` 是空目录类）。

本模组的做法是**在同一个 `BROKEN_MACHINE_TAG` 上叠一层"可修复"语义**，不新增机器、不改原版流程：

| 环节 | 实现 |
| --- | --- |
| 状态 + 病位 | 一律复用 `BROKEN_MACHINE_TAG`（`MachineRepair.IsBrokenMachine`，`MachineRepair.cs:242-256`），坏机器照常不能加工（原版 `LoadBrokenMachine` 已清空回调）； `MDU_REPAIR_DAMAGE` 一个 int 位掩码，首次访问时随机 2~3 个部位并**永久固化**（`MDU_REPAIR_INIT` 守卫，`MachineRepair.cs:258-279`） |
| 投料 | 每个部位一个 `MDU_REPAIR_P{part}_M{mat}` 计数器（`ProgressTag`，`:306`）；`WantsMaterial/TryFeed`（`:575/598`）与 §2.2 同款，整段复用 `Engine` 那套拖料链 |
| 完成 | `Finish`（`:671`）→ `PreBuiltItemHelper.Create{Alarm,Furnace,MoistureFarm}()` 造一台**全新正常机器** → `TransferContents` 搬电池与模块（`:741`）→ `GraphUtils.SwapItemsInPlace(normal, broken)` **原位换机**（保位置/朝向）→ 销毁旧的 → `ApplyFinalUpgrades` |
| 第 4/5 部位 = 毕业改造 | 第 4 部位完成：`+25/+25/+25` 并挂 `mdu_repair_new` 售价 +25% 特征；第 5 部位：挂 `mdu_repair_luxury` +300% 并 `SetGameItemType("LUXURY_ITEM")`（`:805-836`）——**注意这条踩我们红线**，见 §4 陷阱 5 |
| 入口 | 医生网络升级 `dr_repair`（400）解锁服务、`dr_repair_adv`（1000）解锁第 4/5 部位（`DrServices.cs:244/248`）；坏机器由零件批发商上架（`PartsWholesaler.cs:127-143`，`WholesalerBrokenMachines` 开关） |
| 可扩展性 | **硬编码 `Recipe[]` 三台**（`MachineRepair.cs:84-203`：集水器/熔炉/警报），`RecipeFor` 线性查 id（`:205`）。没有注册表、没有外部 API——"可扩展"只到"你再往数组里加一条"（且 `Finish` 里的 `switch` 还要加一支），**不是**数据驱动的框架 |

**配套的贴图/形状自愈**（这才是这套框架真正费劲的地方）：原版坏机器走 `Items/items_broken_machine` 图集 + `SetSpriteAndShape`（`MachineBrokenHelper.cs:19-28`），本版运行时**这个图集取不到**，于是作者自供 3 张 PNG，并写了 `BrokenSkin`：`ResolveSpriteByName` Prefix 直接返回自造 `Sprite`（`Sprite.Create` 时**借用第一张成功解析的原版 sprite 的 `pixelsPerUnit` 与 `pivot`**，`BrokenSkin.cs:178-181`），`SetSpriteAndShape` Postfix 再把形状改成从原版正常机器图采样出的 3×3（`BrokenSkin.ShapeFor`，`BrokenShapePatch.cs:15-22`）。`BrokenSkinOn` 是独立开关，默认开。

### 2.5 医生剧情链（第 4 问）

**状态机存在 `ModData`（不是物品 tag）**，键全部 `more_device_upgrades` 命名空间下（`AugStory.GetInt/SetInt/GetBool/SetBool`，`AugStory.cs:9-55`）：

| 键 | 类型 | 含义 |
| --- | --- | --- |
| `augStoryRewriteCount` | int | 累计"改写成功的改造人"数（唯一进度来源） |
| `augStoryPendingStage` | int | 已排期但还没上门的阶段（**过夜时写，次日生成顾客时消费**） |
| `augStoryStage{2,4,6,8}Done` | bool | 阶段已发过，永不复位 |
| `augStoryNoInventorMarkup` | bool | 博士永久取消供应商加价标签（终章级开关） |
| `docStoryOff` | bool | **新档快照**：`OnNewGameStarted` 时把配置值写进存档（`DoctorStoryGate.cs:22-45`），改配置也不影响老档 |

推进链（**为什么"失败"几乎没有出口**）：

```
AugCaptureRun.Run(device)  电量 25 → 当前顾客 isAug ？→ AugRoster.Add + 说一句 + 让顾客离场
        └ AugStory.Bump()  count++
过夜 ModHook.OnHandlingNightlyServicesLate → AugStory.OnNightly()（AugStory.cs:189-219）
        count >= Threshold(stage) 且该阶段未 Done → SetPending(stage)   // 门槛 2/6/10/13
次日 StoreClientManager.GenerateClient 的 Postfix（AugStorySpawnPatch.cs:9-53）
        Pending != 0 → Build(stage) → AddNextClient(排队首) → **立刻 MarkDone(stage)**
```

三个必须记住的设计决定：
1. **门槛与保险丝同体**：`Threshold()`（`AugStoryDef.cs:17-28`）= 13/10/6/2，配置成"从高到低试，命中即排期"，且 `Pending != 0` 时直接 return——一晚只排一个，避免刷爆顾客队列。
2. **`MarkDone` 在"排到队首"时就写，不在"剧情演完"时写**（`AugStorySpawnPatch.cs:46`）。所以玩家把博士赶走/不打烊就退出，这一阶段就**永久失去**（除改存档外无补救）——作者用"给一次就不再来"换实现简单。要稳妥就在 `FinishClient`/离场回调里再补一次校验（推测：原版 `StoreClient.allDoneDialogue` 的 EndAction 是唯一可靠完成信号）。
3. **奖励挂在访客的 `BarterOffer`/`Dialogue.endAction` 上**，不是发到背包了事：阶段 2 四选一材料×8、阶段 4 三选一系统模块×3（`BuildStage2/4`，`AugStory.cs:445-531`）；阶段 6 直接改原版博士铺货（`StoreClientList.CreateInventor()` + `MarkNoMarkup()` + `PlaceInventorInventory`），阶段 8 是整条 R917 剧情（3,814 行）——**阶段 8 的触发方式是玩家在店里开枪**（`StoreUIManager.Shoot` Prefix，`R917Show.cs:12-25`）。

改造人三件道具用标准的 `ModItemDirectory.InitDirectory` Postfix 注册（`AugCaptureRegisterPatch.cs:9-25`，先 `Has(id)` 再 `Add`），效果全部挂在 `GameItem.CanActivateSlotItem/MayActivateSlotItem/ActivateSlotItem` 三个原版虚方法上（各 3 个补丁）——这是"每个道具一个设备类 + 三个激活补丁"的模板，值得抄。

### 2.6 Patch 靶点表（第 5 问）

**规模**：178 处特性式 + 6 处手动；**0 Transpiler**；Prefix 会真跳原版的 25 个文件。按原版类聚合（次数 = 补丁处数）：

| 原版靶点 | 次数 | 手段 | 目的 | 风险 |
| --- | --- | --- | --- | --- |
| `ModItemDirectory.InitDirectory` | 8 | Postfix | 注册自己的物品（改造仪/药盒/恩惠卡…），先 `Has` 再 `Add` | 与我们 `ModHook.OnModItemDirectoryInit` 顺序无关，**安全** |
| `PlayerStore.EndNight` | 6 | 5 Postfix + 1 Prefix | 夜间结算（鱼缸/笼子/冰箱/酒架/充能器售电/拾荒交付） | ⚠️ 我们自己声明"绝不整段替换 EndNight"——它没替换、只 Postfix，**可共存**，但 6 家都要按 `PlayerStore.HasInstance` 判空 |
| `StoreUIManager.Update` | 4 | Prefix+Postfix 混合，**未写参数类型表** | 分帧泵（尺寸重放/冰箱甜品机/鱼缸/笼子） | ⚠️ **与《深空电玩》`ArcExpressions`/`ArcMachinesPatches`、群友 `FreyaExpressions` 同一靶点**；原版是 `private void Update()` 单重载，暂不歧义，但**顺序敏感**（它用 `%N` 分帧） |
| `GraphUtils.TryAcceptAll` / `EmporiumTryAdd` | 1+1 | Prefix（带 `new Type[]{}` 类型表） | 鱼缸"什么都能吃"+ 幼崽直接进缸 | ⚠️ **全局热方法**，我们每处投放都过它；它 `EnsureAdmit*` 会改容器准入谓词 |
| `GameItem.TryFindOneValidInventorySlot` / `MayHaveValidInventorySlot` | 1+1 | Prefix 返回 false | 拖料判定（§2.2④） | ⚠️ 拖拽全局；与任何"改拖拽/分堆"模组正面冲突 |
| `SlotMarker.TryAcceptOnce` | 1 | Prefix `__result=0` | 假装放进 0 件（同一判定链，见 §2.2④） | ⚠️ 拖拽全局；`numTransfer` 会被别家读到 |
| `ItemMouseDragHandler.EndDrag`/`Drag`/`StartDrag`/`AbortDrag` | 4 | Prefix/Postfix | 落料总入口 + 阶段旗 | ⚠️ 全局拖拽 |
| `GameItem.set_contentWindow` + `PixelWindow.SetTitle` | 3+1 | Postfix | 标题栏注入按钮 + 标题前加 `<color=#FF5252>` 已装模块摘要（`_titleBusy` 防重入） | 中：**任何改标题的模组都会互踩**，它用"记 baseTitle + 剥前缀"做到幂等 |
| `GameItemElement.ResolveSpriteByName` | 3 | Prefix 返回自造 Sprite | ① mod 精灵读档丢失兜底 ② 坏机器贴图 ③ 熔炉皮肤 | ⚠️ **热方法**；注意它**不碰 `SpriteDict.GetSprite`**（那是我们的热点），但它的 Prefix 会让**我们的 `SpriteDict.GetSprite` 补丁对机器图标失效** |
| `GameItem.SetSpriteAndShape` / `set_uniqueId` / `set_parentInventory` / `AddItemFeature` | 1+3+1+1 | Prefix/Postfix | 形状自愈、解序唯一 id、改造人标记、博士加价剥离 | 中 |
| `MachineHelper.OnLoad` + `MachineryHelper.InitMachinery/InitDrawType/InitProgressType` | 4 | Postfix → `Engine.RestoreMachine` | **读档后把自定义面板/形状/尺寸重放一遍** | ✅ 关键经验：`MachineHelper.*` 只挂这 4 个，**没动 `GetBatterySlot/GetModules/GetSlot`** |
| `ModuleHelper.ComputeModuleEffect(List<GameItem>, GameItem)` | 1 手动 | Postfix（`ModuleGuard`） | 模块重算后补回本模组加成 | ⚠️ 与任何"改模块效果"的模组互踩，但它只读不写 |
| `PlayerStore.DismissCurrentClient` | 2 | 1 Prefix（**无类型表**）+1 | 改写器"无害化"结算 | ⚠️ 我们两个模组都挂了 3-bool 重载 → 顺序必须显式 |
| `StoreClientManager.GenerateClient` | 3 | Postfix | 博士/工具商/批发商插队 | ⚠️ 与群友"熟客排期"同靶点 → 需 `HarmonyBefore/After` |
| `HusbandryHelper.*` / `HydroponicHelper.*` / `AquacultureHelper.*` | 12 | Prefix/Postfix | 饲养/水培/鱼缸行为改写 | 中（域内自洽） |
| 各 `Machine*` 类（Alarm/Deseq/Dna/Printer/Chem/Fridge/Wine/Feed/RAS/HeatingPlate…） | 40+ | Prefix/Postfix | 高级路径效果落地；多为"读我的 tag 后修正参数"，属正确姿势 | 中 |
| `GameItem.IsTag` / `SpriteDict.GetSprite` / `AppraisalHelper.GetCorrectBaseValue` | **0** | — | — | ✅ **四个热点里三个没碰**（见 §5 热点栏） |

**结论（热点对比）**：我们四个热点里它**只擦到"贴图解析"这一条边**——用 `GameItemElement.ResolveSpriteByName` 而不是 `SpriteDict.GetSprite`，用 `ItemFeature.SetPreExposedValueMod` 而不是 `AppraisalHelper.GetCorrectBaseValue`，完全不动 `GameItem.IsTag`。它真正的重合面在**拖拽/投放三条主链**（`TryFindOneValidInventorySlot`、`TryAcceptOnce`、`GraphUtils.TryAcceptAll`）和 **`StoreUIManager.Update`**。

## 3. 关键代码摘录

### A. 三选一锁定 + 槽位（`Adv.cs:1881-1993` 精选）

```csharp
internal static bool SelectionLocked(GameItem machine, AdvFamily f)
{
    if (LockSingle && IsChosen(machine, f)) return SlotsFull(machine, f);
    return false;                       // 本版 LockSingle = false → 锁只剩 UI 层
}
internal static int SlotLimit(GameItem machine, AdvFamily f) => f switch {
    AdvFamily.Furnace => SlotCount(machine),                              // 1 或 2（医生 dr_modular）
    AdvFamily.Recharger => DrServices.RechargerSlotUnlocked() ? 2 : 1,    // 1 或 2（医生 dr_recharger_slot）
    _ => 1 };
internal static bool ToggleSelect(GameItem machine, AdvFamily f, int dir)
{
    if (machine == null || !IsEnabled(f, dir)) return false;
    int done = DoneMask(machine, f);
    if ((done & dir) != 0) return false;                       // 已完成不可再动
    int sel = SelectedMask(machine, f);
    if ((sel & dir) != 0) { Engine.SetInt(machine, SelTag(f), sel & ~dir); return true; }
    int limit = SlotLimit(machine, f);
    if (CountInProgress(machine, f) >= limit)
    {
        int cand = sel & ~done;
        if (cand == 0) return false;
        sel &= ~LowestBit(cand);                               // 槽满 → 替换最低位（进度 tag 不清）
    }
    else if (limit > 1 && (sel & ~done) != 0) return false;     // 多槽：必须做完一条再选下一条
    Engine.SetInt(machine, SelTag(f), sel | dir);
    return true;
}
```

### B. 拖料：三个 Prefix 骗过原版（`TryFindOneValidSlotPatch.cs` / `SlotAcceptOncePatch.cs` / `EndDragPatch.cs`）

```csharp
[HarmonyPatch(typeof(GameItem), "TryFindOneValidInventorySlot")]
internal static class TryFindOneValidSlotPatch
{ private static bool Prefix(GameItem __instance, GameItem item, ref SlotMarker __result)
  {
        if (!Core.IsOn() || DragState.InDragPreview) return true;
        if (DragState.InEndDrag) {                                // 松手那一帧不给原版插槽
            if (!Engine.IsUpgradeablePair(__instance, item)) return true; __result = null; return false; }
        if (Adv.IsScrewdriver(item))                              // 螺丝刀：只要"全部完成"就可拆
        { if (!Adv.CanRemove(__instance)) return true; __result = null; return false; }
        if (!Engine.IsUpgradeablePair(__instance, item)) return true;
        SlotMarker slot = Engine.GetIncompleteSlot(__instance);   // machine.GetIncompleteSlotInInventory()
        if (slot == null) return true;
        Engine.MarkValid(slot);                                   // slot.numTransfer = 1
        __result = slot; return false;
  } }
// 让"放进 0 件"被原版当成成功；真正的结算在松手后
[HarmonyPatch(typeof(SlotMarker), "TryAcceptOnce")]
internal static class SlotAcceptOncePatch
{ private static bool Prefix(SlotMarker __instance, ref int __result)
  { /* …判 DragState.InEndDrag + IsUpgradeablePair… */  __result = 0; return false; } }
// EndDrag.Postfix：反查落点机器 → 唯一的结算入口
GameItem machine = DragRefs.LastItem(handler);                    // AccessTools.FieldRefAccess("lastItem")
GameItem material = handler.GetCurrentItem();
if (machine != null && material != null) Engine.HandleDrop(machine, material);   // EndDragPatch.cs:174
```

### C. 投料 + 完成（`Adv.cs:1551-1637` 精简）

```csharp
internal static bool TryFeed(GameItem machine, GameItem material, string matId)
{
    if (!TryFamily(machine, out var family) || !Implemented(family) || !MachineSwitch.On(machine)) return false;
    int sel = SelectedMask(machine, family), done = DoneMask(machine, family);
    if (sel == 0) return false;                       // 没选路线 → 一律不收
    foreach (AdvRecipe r in Table(family))
    {
        if ((sel & r.Dir) == 0 || (done & r.Dir) != 0 || !IsEnabled(family, r.Dir)) continue;
        foreach (AdvIngredient p in r.Parts)
        {
            if (!IngredientMatches(p, matId)) continue;
            int cur = Engine.GetInt(machine, p.Tag), need = NeedOf(p);
            if (cur >= need) continue;
            int add = p.ByPurity ? IngotAdvProgress(material) : 1;     // 铁锭按纯度一次推 25~300 点
            Engine.SetInt(machine, p.Tag, Math.Min(cur + add, need));   // ① 先记进度
            Engine.ConsumeUpgrade(material);                            // ② 销毁 1 件（改装能手 20% 免料）
            Engine.AddValueByMaterial(machine, material);               // ③ 机器涨价（材料价 ×3/4）
            Furnace.PlaySuccessSfx();
            if (RecipeComplete(machine, r)) Complete(machine, family, r);
            return true;                                                // 一次只走一条材料
        }
    }
    return false;
}
internal static void Complete(GameItem machine, AdvFamily f, AdvRecipe r)
{
    Engine.SetInt(machine, DoneTag(f), DoneMask(machine, f) | r.Dir);
    if (f == AdvFamily.Furnace && r.Dir == 8) Engine.SetInt(machine, "MOD_FURNACE_MODULAR_DONE", 1);
    ApplyEffect(machine, f, r.Dir);                    // 各 family×dir 一支 switch（Adv.cs:1639-1874）
    FurnaceSkin.Refresh(machine); AdvUi.RefreshWindowTitle(machine);   // 标题立刻显示"已装：并行冶炼"
}
```

### D. 进度条就在 tooltip 里（`TooltipUpgrade.cs:922-941/969`）

```csharp
AddLine(builder, UILoc.KF("fui_adv_slot", Adv.CountInProgress(item, family), Adv.SlotLimit(item, family)), 4365501);
foreach (AdvRecipe r in Adv.Table(family))
{
    if ((sel & r.Dir) == 0) continue;
    bool isDone = (done & r.Dir) != 0;
    AddLine(builder, UILoc.K(r.NameKey) + (isDone ? " - 已完成" : " - 在途"), isDone ? 8950922 : 4367018);
    if (!isDone) foreach (AdvIngredient p in r.Parts)
    {
        int cur = Engine.GetInt(item, p.Tag), need = Adv.NeedOf(p);
        AddLine(builder, UILoc.K(p.MatKey) + " " + cur + "/" + need + " " + AdvBar(cur, need), (cur >= need) ? 8950922 : 4367018);
    }
}   // AdvBar：max<=10 时一格一件，否则 10 格等比，实心 '■' / 空心 '□'
```

### E. 重算而不是累加 + 模块守护（`Engine.cs:529-579`、`ModuleGuard.cs:31-40`）

```csharp
internal static void SyncTotals(GameItem machine)
{
    if (machine == null || _syncingTotals) return;          // 防重入
    _syncingTotals = true;
    try
    {
        if (!SumModuleTemps(machine, out int mq, out int mp, out int mf))   // 原版模组 TEMP 三项
        { ApplyMachineStat(machine); return; }                            // 读不到模组格 → 不动 TOTAL
        ComputeModContribution(machine, out int q, out int p, out int f);  // 本模组贡献（含热管理/修理 +25）
        SetInt(machine, "TOTAL_PERCENTAGE_QUALITY_BONUS_INT",     mq + q);
        SetInt(machine, "TOTAL_PERCENTAGE_PERFORMANCE_BONUS_INT", mp + p);
        if (SetInt(machine, "TOTAL_PERCENTAGE_POWER_BONUS_INT",   mf + f)) RecomputeDraw(machine);  // 耗电重算
        ApplyMachineStat(machine);                                        // ModuleHelper.ApplyBasicModuleEffect
    }
    finally { _syncingTotals = false; }
}
// 原版装/卸模块会整体重写这三个 tag → 必须补一刀
[HarmonyPatch(typeof(ModuleHelper), "ComputeModuleEffect", new Type[] { typeof(List<GameItem>), typeof(GameItem) })]
internal static class ModuleGuard { private static void AfterModuleRecompute(GameItem __1) => Engine.ReapplyAfterModuleRecompute(__1); }
```

### F. 坏机器原位换成好机器（`MachineRepair.cs:671-739` 精简）

```csharp
internal static bool Finish(GameItem broken)
{
    Recipe r = RecipeFor(broken);
    if (r == null || !CanFinish(broken)) return false;
    GameItem normal = r.Id switch { "security_alarm" => PreBuiltItemHelper.CreateAlarm(),
        "furnace" => PreBuiltItemHelper.CreateFurnace(), "moisture_farm" => PreBuiltItemHelper.CreateMoistureFarm(), _ => null };
    if (normal == null) return false;
    GraphUtils.EmporiumTryAdd(normal);                                // 先找地方落脚
    TransferContents(broken, normal);                                 // 电池 + 模块（先 SetModuleUnstuck 再 Expel/TryAcceptAll）
    if (!GraphUtils.SwapItemsInPlace(normal, broken)) return false;   // 原位互换（保留位置/朝向）
    ApplyFinalUpgrades(broken, normal); broken.Destroy();             // 第 4/5 部位毕业加值后销毁旧机
    return true;
}
```

### G. 剧情推进两段（`AugStory.cs:189-219` + `AugStorySpawnPatch.cs:27-49`）

```csharp
private static void OnNightly()   // ModHook.OnHandlingNightlyServicesLate
{
    if (!Core.IsOn() || DoctorStoryGate.Off()) return;
    int count = Count();
    if (count < 2 || Pending() != 0) return;                  // 一晚只排一个
    foreach (int stage in AugStoryDef.Stages)                 // {8,6,4,2}
        if (count >= AugStoryDef.Threshold(stage) && !Done(stage)) { SetPending(stage); break; }
}
// GenerateClient.Postfix
int stage = AugStory.Pending(); if (stage == 0) return;
AugStory.SetPending(0);
StoreClient c = AugStory.Build(stage);
try { __instance.AddNextClient(c); } catch { __instance.AddClient(c); }
AugStory.MarkDone(stage);                                     // 排到队首就算"已发"，错过不补
```

## 4. 踩坑与红线

1. **`Engine.AddBonus(machine, q, p, f)` 的三个参数是装饰品**——它内部只调 `SyncTotals(machine)`（`Engine.cs:782-797`）。所以 `TryRemoveAll` 里 `AddBonus(machine, -1000, -500, -50)`（`Adv.cs:2142`）是死代码，真正生效的是"先把 tag 清零再重算"。**看这份代码时千万别把 `AddBonus` 当累加器**；自己写的话，签名里别留不用的参数。
2. **`slot.numTransfer = 1` 是臆造出来的合同**（`Engine.cs:1219`）。靠"原版以为转了 1 件、其实转 0 件"来驱动自己的结算，任何改拖拽/分堆的模组（SmartArrangement、ContainerLink…）都可能让它落空：表现是"拖上去没反应"或"材料凭空消失"。**要做机器拖料，更稳的两条路**：① 用原版 `onSlotAddItemFunc` 容器回调（本模组自己也用这套做别的机器）；② 保留 `CanAccept/TryAccept` 语义，把材料真放进机器的一个隐藏槽再在 `onSlotAddItemEndFunc` 里消耗。
3. **181 处 `catch` 里绝大多数是空的或只 `LogV`**（`Core.LogV` 受 `Verbose` 开关控制，默认 **false**）。排障时第一件事是打开 mod 设置里的 `Verbose` 与 `Diag`（`Core.cs:48-49`），否则错误全被吞。这是它"能跑但说不清为什么能跑"的根因。
4. **文案与实现已经漂移**：manifest 写"应用结晶学：电子元件×4 + 金属锭×4"，代码是"铁锭×12 + 电路×2"（`Adv.cs:145-168`）。**以代码为唯一真相**，改数值时顺手改 manifest。
5. **`SetGameItemType("LUXURY_ITEM")`**（`MachineRepair.cs:828`）——我们的规范是"自定义品类只用 `EnableTag`，绝不往 `SetGameItemType` 写非原版类型"。它写的是原版 `LUXURY_TYPE` 常量（`MachineRepair.cs:70`），勉强合规；但我们照抄时**必须确认目标类型字符串是原版枚举里的**，否则蓝字 `[Translation Error]`。
6. **`CartelBox` 按编译器生成名找 lambda**（`CartelBox.cs:29-44`：`StoreClientListCartel+<>c__DisplayClass5_1` 里 `b__0..b__15` 逐个试挂）。游戏一重编译就全废。这是"没有其它入口时的最后一招"，不是范式。
7. **`StoreUIManager.Update` 4 个补丁都不写参数类型表**（`CageSlotPumpPatch.cs:5` 等）。当前原版是 `private void Update()` 单重载所以侥幸；一旦官方加重载就会**静默全挂或全命中**。我们自己必须写 `new Type[0]`。
8. **25 个 Prefix 会 `return false` 跳过原版**，其中 `StoreUIManager.Shoot`（剧情期接管玩家开枪）、`BargainUIManager.OpenUI`、`DialogUIManager.DisplayClientText/DisplayChoice/SelectChoice/HideChoiceBubble`、`PlayerStore.TryPlayerDismissCurrentClient` 都属于"整段接管 UI 流程"——与任何改对话/议价/开枪的模组互斥。**我们不复刻这些挂点**。
9. **零热重载**：所有静态字典（`AdvUi._headed/_baseTitle/_winOwner`、`FurnaceSkin.Sprites/Elements`、`BrokenSkin.Sprites/Shapes`、`ModSpriteHeal.Healed`、`DragState`）只在 `OnGameLoadedInit` 里清，`OnDisable` 只管 `Unsubscribe`。**热重载后标题/皮肤会错乱**——我们抄它任何静态 UI 表时，必须补 `HotReloadPrepare/Activate` 成对重置。

### 接手别人模组：移植清单（他做成"移植成功"靠的就是这 11 条）

| # | 动作 | 本模组的证据 |
| --- | --- | --- |
| 1 | **先读 manifest + 版本号，再看代码** | `manifest.xml` 的 `<ID>/<ModVersion>/<Description>` 已重写成中文详表；`Core.Version` 与 `ModVersion` 严格对齐 |
| 2 | **`OnEnable` 的 `Xxx.Install()` 顺序 = 功能地图** | `Core.cs:83-138` 列了 20 个 `Install()`，一屏看完 20 个子系统 |
| 3 | **给每个原版挂点加"挂点缺失"分支**，别裸 `AccessTools.Method` | `LayoutRefresh.cs:20-30`、`ModuleGuard.cs:24-28` 都有 `if (methodInfo == null) LogWarning; return;` |
| 4 | **读档恢复是重灾区**：形状/尺寸/面板都会被原版重建冲掉 | `MachineHelper.OnLoad` + `MachineryHelper.InitMachinery` 双 Postfix 打 `Engine.RestoreMachine`；`SizeRestore` 读档后再跑 8 轮×12 帧重放（`SizeRestore.cs:8-10`） |
| 5 | **模块重算会冲掉加成** → 挂 `ComputeModuleEffect` Postfix 重算 | `ModuleGuard.cs`（§3 摘录 E） |
| 6 | **贴图丢失要先判断"图集是不是模组 id"** | `ModSpriteHealPatch`：`useSpriteFromMod == false` 且 `spriteAtlasPath` 不含 `/` 且等于某 `LoadedMod.Manifest.ID` → 直接从该 mod 的 `Sprites` 字典取（`ModSpriteHealPatch.cs:15-60`）。**这条对所有模组通用，可直接抄** |
| 7 | **新加机器支持时，先看数据表能不能容下，别再写一个类** | `Defs.All` 一张表 + `AdvFamily` 一个 enum 覆盖 11 台；加一台 = 加表项 + `FamilyOf` 一支 case + `ApplyEffect` 一支 |
| 8 | **改别人的设计要留旧路径开关**，并写日志说明 | `IsEnabled(Furnace, 8) = false` 与 `AllEnabled/LockSingle/OriginalMask` 三个常量，就是"我正在改你的设计"的注脚 |
| 9 | **数值/文案双源必然漂移**，改完跑一遍自己对表 | §4 陷阱 4 |
| 10 | **给每个新功能一个总开关**（`ModConfig`「Machines」页 19 台逐台开关） | `Core.cs:59-71` 循环 `MachineSwitch.Machines` 生成配置项，异常单台不影响整体 |
| 11 | **能力声明要能自证**：只用 `FieldRefAccess<,>("字面量")` 这类"非运行期算名"的反射 | manifest 注释 + 全包 10 个文件用到反射，无 `Assembly.Load`/`System.IO` |

## 5. 对我们的可用点（点名到项目功能）

### 《深空电玩》（`deepspace_arcade` / `DeepSpaceArcade.dll`，47 源文件）

| 可用点 | 落到哪个功能 | 怎么用 |
| --- | --- | --- |
| **① 高级路径三件套（选择掩码 + 进度掩码 + 每材料计数，全部写在物品 tag）** | 刻录机 / 未来"机器改造"扩展 | 直接照 §2.2① 的键名结构（`MOD_X_ADV` / `_DONE` / 每材料一 tag）；进度与选择都进存档且**不碰 ES3**，比另开 `ModData` 表更省事，卸载后残留的只是几个无害 int tag |
| **② 标题栏 `AddHeaderButton` + tooltip 富文本进度条** | 刻录机窗口（`ArcMachinesPatches` 的机器面板）、整备套件悬停 | `PixelWindow.AddHeaderButton(new MachineryButton(text, action, w, h))`（`AdvUi.cs:430-441`）+ `ModHook.OnCreateTooltipLate` 追加 `■□` 进度行；**注意与现有 `SpriteDict.GetSprite` 补丁无关，可共存** |
| **③ `ModuleHelper.ComputeModuleEffect` Postfix 重算守护** | 我们给机器加的一切数值（刻录机 TURBO、未来机器改造） | 我们现在用 `TransientTagBoost`（Prefix 暂存/还原）解决"临时改值"；**"永久加成"要另一套**——照 `SyncTotals` 写"私有 tag → 重算原版 TOTAL"，并在 `ComputeModuleEffect` 后补一刀，否则玩家一装模块加成就丢 |
| **④ `ResolveSpriteByName` 兜底 vs 我们的 `SpriteDict.GetSprite`** | `ArcPortrait`/`ArcSpriteDictGetSpritePatch`、8 张 `dnpc_*` 自愈 | 事实：**它不 patch `SpriteDict.GetSprite`**，但它的 Prefix 会让我们的 `SpriteDict.GetSprite` 补丁对"读档后 `useSpriteFromMod` 丢失的机器/物品图标"完全失效（前缀已经返回了）。若我们也要做物品图标自愈，**应当补 `ResolveSpriteByName` 而不是加码 `SpriteDict`**，并给它 `[HarmonyBefore("mod.more_device_upgrades")]` 之类的顺序声明（若同时装） |
| **⑤ 坏机器修理框架 → 现有"概率维修"升级** | 功能 #6 概率维修（`ARC_BURN_FAULT`、25% 起、失败扣 1 件加 5%） | 我们已有"硬件件被烧坏→拿件修"的循环；这套 `MDU_REPAIR_DAMAGE` 位掩码 + 分部位材料清单（`PartDef{MatIds,Needs}`）可以直接把"一次判定"扩成"三部位逐件投喂"，且 `Finish` 的 `SwapItemsInPlace` 原位换机正是我们换`STATE_WORKING`盘/机器时要的写法 |
| **⑥ 读档重放（`SizeRestore` 8 轮×12 帧）** | 刻录机 `SetupManualSlot`/`SetupModuleBay` 后的面板尺寸 | 我们的机器扩容/手动槽在 `InitMachinery` 之后还会被 `RestoreOwnersContentTabs` 重建；照 `LayoutRefresh`（`PixelWindow.RestoreOwnersContentTabs` Postfix）再复核一遍形状与面板，比在 `Update` 里死等更省 |

### 《深空当铺：可能是群友》（`freya_cornell` / `DeepSpacePawn.dll`，28 源文件）

| 可用点 | 落到哪个功能 | 怎么用 |
| --- | --- | --- |
| **① 剧情阶段机（`ModData` + Pending + Done 位）** | 芙蕾雅主线、伞萝萝/塞壬周日程、电话 7721/7734 | `augStoryPendingStage` 的"过夜排期 / 生成顾客时消费"两段式，正好套我们的"次日带货上门"；**但必须改掉它"排到队首即 MarkDone"的写法**（§2.5 第 2 点），改成 `allDoneDialogue.EndAction` 里才 MarkDone，否则玩家早退就永久丢一次出场 |
| **② `GenerateClient` Postfix 插队 + 新增 `AddNextClient` 失败回退** | 熟客/剧情访客插队（现在走 `StoreClientList` 排期） | 它的 `try { AddNextClient } catch { AddClient }` 是保命写法；与我们的排期同靶点，**必须显式 `[HarmonyPriority]`/`HarmonyBefore`**（`ModLoader` 的 Harmony ID 带 `mod.` 前缀） |
| **③ `DismissCurrentClient` 上的"离场副作用"补丁** | 角色离场留白、B5/B7 早退不丢 | 我们两个模组都挂了 3-bool 重载；本模组挂的是**无类型表**版本（`AugReproRun.cs:9`），共存时用 `__instance.currentClientInstance.GetClientBlueprint().isAug` 这类判空链即可，但顺序必须声明 |
| **④ 三件"设备型道具"模板（3 个激活补丁 + 1 个注册补丁）** | 若要做"验水仪/改造仪"类手持设备（塞壬验水已是类似物） | `CanActivateSlotItem`/`MayActivateSlotItem`/`ActivateSlotItem` 三件套 + `ModItemDirectory.InitDirectory` Postfix（先 `Has` 再 `Add`）+ `AugDevice.TryDraw`（`PowerHelper.CanDrawPowerSource/DrawPowerSource` 扣电）——**扣电+目标判定+离场**四步是一条完整可抄的链 |
| **⑤ `ItemFeature.SetPreExposedValueMod` 挂售价** | 名片/赠品/稀有货的"溢价标签" | `MachineRepair.AddPriceFeature`（`MachineRepair.cs:838-868`）：`new ItemFeature(FeatureType) { identifier, category, valueStage, isFeatureMatch=true, isExposable=false }` + `SetPreExposedValueMod(mod, true)` + `AddItemFeature`。比我们改 `unitValue` 干净：**是"特征"不是"数字"，能被鉴定/展示系统正确读**（注意：对外展示用 `SetPublicDisplay`，别泄露真值） |

> **最该抄的一件事**（跨两个项目）：把"永久加成"从"往原版标签里加数"改成"**私有 tag 存档 + 每次重算**"（§3 摘录 E）。它一并解决了三件事——模块装卸不丢、读档不重复叠加、卸载后原版数值能自愈；而这三件正是我们过去靠 `TransientTagBoost` + 手工还原最容易漏的地方。
