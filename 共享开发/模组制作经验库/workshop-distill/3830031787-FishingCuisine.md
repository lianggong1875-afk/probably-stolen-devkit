# 钓鱼料理扩展（Fishing Cuisine Expansion）2.1.1｜蒸馏档案

> **工坊 id** `3830031787`｜内部 mod id `fishing-cuisine-expansion`｜`PSFishingCuisineExpansion.dll` 2.1.1｜作者 ALR｜`GameVersions 0.46D`｜`<Prerequisites><Mod>ps-fishing-gameplay</Mod></Prerequisites>`、`<Capabilities/>` 自闭合（`manifest.xml`，全文单行 462 B）｜1.5 万订阅。
> **反编译目录** `缓存文件夹\decomp_ws\3830031787\`（**105 个 `.cs` / 9,412 行**；另嵌入 6 个 JSON/TXT 资源共 1,718 行——items 699、fish-materials 251、译文 648、简介 89、菜谱 26、契约 5；包内 `Textures/Items` 37 张 PNG、`Localization` 四语 CSV，整包 1.6 MB）｜透视报告 `缓存文件夹\decomp_ws\_reports\3830031787.txt`（20 处特性式 `HarmonyPatch`、3 个 ModHook、1 个 ModReflect）。
> **为什么值得读**：它是**上一批 GoFishing（`3829110153`）声明的那个"硬前置"本体**——两份 manifest **互相把对方写成前置**，而两边 csproj 都没引用对方程序集（见一）。同时它是"内容包"的稀有诚实样本：26 道菜的**配方只有一张 29 行的表**，剩下 9,383 行全花在"料理台 UI + 食客订单 + 手册 + 认证"四件套上——正好回答"9,412 行做配方包是不是太多"。

---

## 一、规模与结构

### 1.1 目录 → 行数 → 职责（105 文件 / 9,412 行）

| 目录（namespace） | 文件 | 行数 | 占比 | 职责 |
| --- | ---: | ---: | ---: | --- |
| `FishingCuisineExpansion\`（根） | 20 | 3,122 | 33.2% | 入口 `Core`、**2,248 行的 `Runtime`**、本地化、模组页展示、纸张物品 |
| `.Logic.Cooking\` | 8 | 1,028 | 10.9% | 纯规则：`CookingScoringRules` **757 行**、方法/配方/食材规则 |
| `.Mod.Manual\` | 3 | 789 | 8.4% | **料理手册/菜谱翻页 UI**（`ManualSession` 713 行，借原版 `PaperUIManager`） |
| `.Mod.Orders\` + `.Mod.Orders.UI\` | 5 | 1,334 | 14.2% | 食客预约与送货（`OrderService` 539、`DeliveryHintView` 428、`PatronConversation` 165） |
| `.Mod.Progress\` + `.Logic.Progress\` + `CuisineJson\` | 12 | 1,055 | 11.2% | 料理认证（付费解锁）、配方解锁表、证书、**自造严格 JSON 解析器**（`StrictJson` 208 行） |
| `.Mod.Cooking.Services/State/Ingredients/Persistence` | 13 | 804 | 8.5% | 工位会话、事务、食材读取、烹饪历史 |
| `.Mod.Items\` + `.Mod.Integration.Fishing/Legacy` | 21 | 679 | 7.2% | 物品目录 JSON → `GameItem`、7 个附件适配器、钓鱼包食材契约、罐头批次解析 |
| `.Mod.Patches\` | 18 | **332** | 3.5% | 20 个补丁壳，**一靶点一文件，均 14~24 行**（另 `Properties\` 2 文件 19 行为反编译残留） |

### 1.2 "配方"到底占多少：**29 行**

- 26 道菜的配方 = `Runtime.cs:222-250` 的**一张 `RecipeDefinition[26]` 表**，一条一行，靠 `Recipe(...)` 双重重载补默认值（`Runtime.cs:1453-1461`）→ **1.1 行/条**，与 `FarmStory` 四条表压到 1.2 行/件是同一量级；**不是"每条配方一个类"**——全库 0 个 `*Recipe` 类，只有 1 个 `record`（`Runtime.cs:49`）。
- 菜品实体 = `FishingCuisineExpansion.content.items.json` 699 行 / **40 件**（28 `dish` + 5 `seasoning` + 3 `cookware` + `station`/`certificate`/`manual`/`cookbook` 各 1），一条 `ItemDef` ≈ 17 行（`CuisineItemDefinition` 32 行全覆盖：Id/名/描述/Flavor/Value/W+H/Calories/Uses/Kind/Asset/Types/Attachments）——与 `Vanilla Food Expansion` 111 件一条 `ItemDef` 同形。

### 1.3 那 9,412 行花在哪：**四件套 + 一份手写数据**

| 层 | 行数 | 判据 |
| --- | ---: | --- |
| 料理台 UI（窗口、槽位、火力按钮、预览图、升级第二段窗口） | ~1,100 | `Runtime.cs:798-1302` + `StationSession`/`StationView` |
| 食客订单与送餐（预约、星级、台词、结算拦截） | ~1,500 | `.Mod.Orders*` 1,334 + `Runtime.cs:1818-2035` |
| 手册/菜谱翻页 + 认证解锁进度 | 1,441 | `.Mod.Manual` 789 + `.Mod.Progress*` 652 |
| **逐鱼种手写修正表** | ~540 | `CookingScoringRules` 的 `DriedFishMethodModifier`(98-286) + `FishMethodModifier`(287-636)，33 个鱼 id × 7 种做法的 `switch` |
| 自造 JSON 解析器 | 403 | `CuisineJson/*`，而工程**已引用 Newtonsoft.Json**（`PSFishingCuisineExpansion.csproj:37`） |

> **结论**：9,412 行里"配方"占不到 0.3%，**真正贵的是把"一台能放料、能选火、能出菜"的窗口从原版机器工厂里借出来并重排**，以及给它配一整套食客/手册/认证的经济闭环。想做"只加菜"的包，成本应该压在 1,500 行以内。

---

## 二、它怎么做这件事

### 2.1 依赖：**manifest 互相写死前置，代码零引用**

- 本包 `<Prerequisites><Mod>ps-fishing-gameplay</Mod></Prerequisites>`（GoFishing 的 mod id）；而 GoFishing 的 manifest 反过来写 `<Mod>fishing-cuisine-expansion</Mod>`（`3829110153-GoFishing.md:3`）。**两份 manifest 形成前置环**。
- 原版加载器对此**只警告不拦**：`OrderByPrerequisites` 的 DFS 用 `visiting` 集合兜环，命中时 `LogWarning("... is part of a prerequisite cycle; its load order is left as found.")`，然后**照常都加载**（`共享开发/reference/Assembly-CSharp/ModLoader.cs:1264-1267`）；`ValidatePrerequisites` 对缺失前置也**只 `LogWarning`，从不禁用模组**（同文件 `:1685-1699`）。所以**这两条前置声明纯属"我看得见你"的礼节，加载顺序无硬要求**。
- 真正的耦合是**编译期零引用 + 运行期字符串契约**：`PSFishingCuisineExpansion.csproj:32-42` 的 `Reference` 只有 `Assembly-CSharp`/Unity 模块/`Newtonsoft.Json`/`0Harmony`，**没有 `PSFishingGameplay`**。它对钓鱼包的全部依赖是 **58 个 `gf_*` 物品 id 字面量**与 4 个原版 tag 名（`FISH_ITEM_TAG`、`GF_CAN_BATCH_V1`、`GF_NATIVE_MATERIAL_SOURCE_V1`、`CALORIE_VALUE_TAG`，`IngredientReader.cs:28-89`）。
- **反向门面 + 优雅降级**：本包公开 32 行的 `CuisineUnlockApi`（`CuisineUnlockApi.cs:5-31`，`Installed=>true` + `Ready/Unlocked/Price/TryPay(out msg)`），GoFishing 用 `ModReflect.FindType("FishingCuisineExpansion.CuisineUnlockApi")` + `ModHelper.IsModRunning` 反射调用它（GoFishing 侧证据见 `3829110153-GoFishing.md:378`）。而代码里对"钓鱼包不在"**没有任何前置拦截**：缺了它，`MeatIngredients`（只有 `raw_meat`/`small_raw_meat` 两个原版 id）仍能走唯一一条肉菜配方，其余 25 道因主材不存在永远匹配不上——**退化成 1/26 而不是崩**。这正是 GoFishing 那份档案判它"声明与实现不一致"的另一半。

### 2.2 启动顺序：**双闸门 + 可取消的延迟激活**

`Core.OnEnable` 不直接 `Install`，而是先登记两个 ModHook 再 `Request()`（`Core.cs:46-70`）：

```csharp
activation = new CuisineDeferredActivation(CuisinePrerequisites.AssetsAvailable, StartCuisine);
ModHook.OnModAssetsLoaded += AssetsReady;        // 自己的贴图/JSON 载入完
ModHook.OnModItemDirectoryInit += DirectoryReady; // 原版物品目录建立完
activation.Request();                            // 立刻试一次，不成就等下一次 Signal
```
`CuisineDeferredActivation` 只有 51 行：`Request()` 置位后立刻 `Signal()`，`Signal()` 幂等（`!Requested || Started || !ready()` 直接返回），`start()` 抛异常时**把 Requested/Started 双清零再 rethrow**（`CuisineDeferredActivation.cs:27-44`）——所以一次失败不会变成"永远启动不了"，也不会变成"启动两次"。`AssetsReady` 还自带身份过滤：只认 `mod.Manifest.ID == "fishing-cuisine-expansion"`（`Core.cs:19-26`）。

### 2.3 配方挂在哪：**原版根本没有做菜系统**

- 证据：`共享开发/reference/Assembly-CSharp/`（948 个原版类）里 **没有 `Cooking`、没有 `Ingredient*`、没有 `IngredientSize`**，`grep -rl Cooking` 只命中 `StoreBookData.cs` 的一处文案；食物侧只有 `FoodItemHelper`/`FoodItemDirectory`。原版的 `RecipeManager`/`CraftingRecipeManager` 是**制造**（crafting），不是烹饪。
- 所以它是**自建工位 + 借原版机器窗口工厂**：`MachineFurnace.CreateMachineInventoryWindow(1, 1, true, true, 48)` 一次拿到「PixelWindow + 电池槽 + 厨具槽 + 料理格 + 成品格 + 便签格」六件套，然后把原窗口的子元素 `Detach` 掉、自己拼一个 1×2/2×1 的 `GridPixelElement` 版式，塞进"火力"与"制作"两个 47×16 按钮，最后 `station.SetContentWindow(window)`（`Runtime.cs:1029-1062`）。
- 槽位准入是**原版容器 API**，不是自定义 Inventory：`mayInventoryAddItemFunc` 挂 `CanAcceptCuisineIngredient/CanAcceptCuisineCookware`（`Runtime.cs:1067-1070`），成品格用 `ContainerHelper.AllowOnlyTaggedItemsOr(inv, ["FCE_CUISINE_OUTPUT"], false)`、便签格允许 `FCE_CUISINE_NOTE` 或 `TUTORIAL_PAPER_TAG`（`:1071-1072`）。**电量**走 `MachineHelper.SetupBatterySlot` + `PowerHelper.CanDrawPowerSource(battery, 5)`（`:1066`、`CookingService.cs:63`）。
- **窗口是"两段式"的**：`InitializeStation` 建基础版（`Runtime.cs:1015`），`UpgradeStationWindow` 再给老存档补"调料槽"（`:1090-1120` 起）——因为 2.x 加槽位时旧档的 station 已经建成老窗口，直接重建会丢内容，所以**加槽只做增量补丁**。这条对我们是现成的"给已存在物品扩容量"范式。

### 2.4 食材规格：**没有 `IngredientSize`，只有快照 + 原版 tag**

`IngredientSnapshot`（`Logic.Cooking/IngredientSnapshot.cs`，36 行）是全部"规格"的载体：`Id / RecipeIdentity / Count / IsOwned / UnitValue / WeightGrams / Calories / IsMain / IsAuxiliary / IsSeasoning / Kind(MainKind) / IsDried`。

- **重量/热量**：鱼走 `AquacultureHelper.GetWeightGrams(item)`、其余 `GeneralHelper.GetWeight(item)`（`IngredientReader.cs:28`）；活鱼取 `AquacultureHelper.GetMeatKcal`、死鱼取 `FISH_CARCASS_KCAL_TAG`、其余 `CALORIE_VALUE_TAG`（`:81-90`）——**规格 = 克数与千卡，不是"份数/尺寸"**。
- **分类**：六档 `MainKind`（`FreshFish/Fillet/DriedFish/ProcessedFish/Meat/FishPart`，`Runtime.cs:31-39`）由 tag 决定，不是由 id 白名单决定；白名单只做**准入**（`IsAllowed`，`:76`）。
- 配方白名单是**启动时一次性 fail-fast 校验**：`Recipes.SelectMany(r => r.Required)` 里任何 id 不在 `MainIngredients ∪ AllowedAuxiliaryIngredients` 就 `LogError("配方白名单校验失败：...")` 并 `return false`，整个模组 `Ready()` 恒假（`Runtime.cs:324-337`、`:315-322`）。

### 2.5 26 道菜怎么区分"用哪种鱼"：**偏好主材 + 物品 tag + 三级产物**

- 配方表里每条带 `PreferredMainId`（正是某个 `gf_*` 鱼/鱼片 id，`Runtime.cs:224-249`）。
- 匹配时**主材身份**先进 tag：`CookingMainIdentity` 把鱼→`gf_*`（整鱼）、罐头→`gf_salt/fce_plain_fish_can` 等身份（`FishCanIngredientReader.cs:56-63`）、鱼片→`Materials[].RecipeIdentity`（`IngredientReader.cs:61-69`）。
- 命中判定三级（`CookingService.cs:70-87`）：`recipe != null && recipe.PreferredMainId == mainId` → **正品菜**（`fce_dish_recipe_<recipe.Id>`）；`recipe != null` 但主材不符 → **鱼盒饭** `fce_dish_fish_box_meal`（描述固定为"似乎有哪里不对，或许有更适合的主材"，`Runtime.cs:121`）；`recipe == null` → **难吃的饭** `fce_dish_bad_meal`。**同一道菜谱吃任意鱼都能出，只是分档**——这就是它不需要"33 种鱼 × 26 道菜"爆炸的原因。
- 排序与惩罚也是表驱动：`CookingRecipeRules.Find` 先排除混用盐种（`HasMixedSeasoningTypes`→直接 null），再按「必需材料数降序、主材相符优先」尝试，多余辅料按每个 -10 计入 `IncompatiblePenalty`（上限 20）（`CookingRecipeRules.cs:9-42`、`:44-52`）；主材不符另有 `RecipeMainPenalty`（`CookingScoringRules.cs:74-88`）。
- 少数鱼带"独占加成"：`IsFishKingMain`（`gf_niorun_fillet`/`gf_golden_fish`/…，`Runtime.cs:553-568`）、`CanUseMultipleFish`（只有小黄鱼/沙丁允许一条配方吃多条，`content.fish-materials.json` 的 `MultiFishIngredients`）。做法由「厨具 × 火力 × 是否放鱼油」解出 7 种（`CookingMethodRules.cs:5-46`，全库最干净的一页）。

### 2.6 Patch 靶点表（20 处；`.Mod.Patches` 18 文件 332 行 + 根 2 文件）

| 原版类型 | 方法（重载已写全） | 类型 | 目的 | 风险 |
| --- | --- | --- | --- | --- |
| `PlayerStore` | `AddedItemToClientArea(GameItem)` | Prefix(可跳过) | 把菜递给食客，而不是进普通客区 | 中：抢原版交货语义 |
| `PlayerStore` | `CanSellThisItem(GameItem,bool)` / `CanSellItemFromTable(bool)` | Prefix 改 `__result` | 食客只收料理、不收别的货 | 中：**同方法被 2 个补丁打**（GoFishing 也打） |
| `PlayerStore` | `SellItem(GameItem)` / `SellItemFromTable()` / `AddDirectSellingItemToTable` ×2 重载 | Prefix 返回 false | 禁止对食客走普通售卖与上货 | 中 |
| `PlayerStore` | `OpenShutter` / `LoadGame` | Prefix | 开局安排订单 / 读档前清会话 | 低（`LoadGame` 是 **Prefix+Postfix 各一个文件**） |
| `StoreClientManager` | `OnNewDay` / `ClientGenCheck` | Postfix | 每日重排食客档期 | 低 |
| `StoreClientMono` | `OnArrived` | Postfix | 食客到场时弹送货提示 | 低 |
| `EndOfDayUIManager` | `InitUI()` | Prefix | 往日结算面板塞"预约"页 | 低-中 |
| `LocHelper` | `GetLocalizedUI(string, object[])` | Prefix（按 key 早退） | 只拦 `fce_patron_contact_tooltip` 一个键 | 低（**键门禁做对**） |
| `PaperUIManager` | `OpenUI(string,string,string,int)` | Prefix 改 3 个 `ref string` | 把"手册"纸页替换成自算页 | 中：改纸张正文 |
| `ItemMouseDoubleClickHandler` | `DoubleClickAction(GameItem,Vector2)` | Prefix | 未持有的菜谱不许双击阅读 | 低 |
| `GameItemElement` | `Destroy()` | Prefix | 物品销毁时清工位会话，防哑会话泄漏 | 低 |
| `ModUIManager` / `ModElement` | `ShowMod(LoadedMod)` / `Init(string,string)` | Postfix / Prefix | 模组管理页里给**自己**条目做本地化名与详情 | 低（见下） |

**`.Mod.Patches` 的 18 处全部**都写 `if (Runtime.IsInstalled) return ...; return true;` 的前置闸门，未安装时一行都不干预原版——这是它能和 GoFishing（231 处补丁）共存的前提；另 2 处（`ModUIManager.ShowMod` / `ModElement.Init`）只做呈现，且自身按 `id` 早退。

---

## 三、关键代码摘录

### 3.1 26 道菜 = 一张表（`FishingCuisineExpansion/Runtime.cs:222-250`，29 行）

```csharp
internal static readonly RecipeDefinition[] Recipes = new RecipeDefinition[26]
{
    Recipe("pan_great_maw", MainKind.FishPart, "煎制", "大裂口", "gf_shark_head", 95, "fce_cooking_wine", "fce_vinegar", "fce_sugar"),
    Recipe("pan_jade", MainKind.FreshFish, "煎制", "翠玉龙飨", "gf_wuzhiqi", 95, "fce_cooking_wine", "fce_light_soy", "fce_sugar"),
    Recipe("steam_fallen_red", MainKind.FreshFish, "清蒸", "落红", "gf_lofurin", 95, "wine_berry", "fce_vinegar", "fce_sugar"),
    Recipe("raw_dream_sail", MainKind.Fillet, "鱼生", "梦帆", "gf_niorun_fillet", 95, "processed_cheese", "fce_sugar"),
    Recipe("grill_crab_curtain", MainKind.FreshFish, "碳烤", "盛大蟹幕", "gf_emperor_red_crab", 95, "gf_fish_oil", "gf_blazing_chili_pepper", "fce_cooking_wine"),
    Recipe("boil_clear", MainKind.FreshFish, "白灼", "一尾清汤", "gf_small_yellow_croaker", true),
    Recipe("boil_soy", MainKind.Fillet, "白灼", "酱淋鱼片", "gf_yellow_croaker_fillet", false, "fce_light_soy", "fce_vinegar"),
    // …共 26 条，末条 raw_crystal 收在 :249
};
```
`Recipe(...)` 两个重载只做两件事：`starter ? 80 : 90`、`Math.Clamp(targetScore, 1, 100)`、`required ?? Array.Empty<string>()`（`Runtime.cs:1453-1461`）。

### 3.2 配方匹配：**排序 + 必需材料 + 多余辅料惩罚**（`FishingCuisineExpansion.Logic.Cooking/CookingRecipeRules.cs:9-42`）

```csharp
public static CookingRecipeSnapshot Find(IEnumerable<CookingRecipeSnapshot> recipes, CookingMainKind main,
                                         string method, IngredientSnapshot[] items, string mainId)
{
    if (CookingIngredientRules.HasMixedSeasoningTypes(items)) return null;   // 混盐种直接不成立
    foreach (var recipe in recipes
        .Where(r => r.Main == main && r.Method == method)
        .OrderByDescending(r => r.Required.Length)                            // 材料多的优先
        .ThenByDescending(r => string.Equals(r.PreferredMainId, mainId, StringComparison.OrdinalIgnoreCase)))
    {
        if ((main != CookingMainKind.ProcessedFish
             || string.Equals(recipe.PreferredMainId, mainId, StringComparison.OrdinalIgnoreCase))
            && recipe.Required.All(id => CookingIngredientRules.HasIngredient(items, id))
            && !items.Where(i => !i.IsMain).Any(i => { /* 盐/辣椒是通配，其它多余辅料一票否决 */ }))
            return recipe;
    }
    return null;
}
```

### 3.3 料理台窗口：解构原版机器窗口再重排（`FishingCuisineExpansion/Runtime.cs:1029-1072`，节选）

```csharp
(PixelWindow, GameSlotInventory, GameInventory, GameGridInventory, GameInventory, GameSlotInventory) made =
    MachineFurnace.CreateMachineInventoryWindow(1, 1, true, true, 48);        // 借原版机器窗口工厂
var grid = new GridPixelElement(1, 2, true).SetSpacing(0, 2);
made.Item1.Detach(made.Item1.childElement);                                 // 拆掉原版排版
grid.Attach(made.Item1.childElement, 96, 48);
var buttons = new GridPixelElement(2, 1, true).SetSpacing(2, 0);
var heat = new ButtonElement(HeatLabel(station), 47, 16, () => { if (session.OwnsWindow(made.Item1)) CycleHeat(station); }, 4, "interface_click");
var make = new ButtonElement(L("制作"), 47, 16, () => { if (session.OwnsWindow(made.Item1)) Cook(station); }, 2, "interface_click");
made.Item1.SetTitle(L("料理台"));
station.SetContentWindow(made.Item1);
MachineHelper.SetupBatterySlot(made.Item2, station);
made.Item3.mayInventoryAddItemFunc = (item, inv) => GeneralHelper.IsItemOwned(item) && CanAcceptCuisineCookware(item);
made.Item4.mayInventoryAddItemFunc = (item, inv) => GeneralHelper.IsItemOwned(item) && CanAcceptCuisineIngredient(item);
ContainerHelper.AllowOnlyTaggedItemsOr(made.Item5, new[] { "FCE_CUISINE_OUTPUT" }, false);
```
注意 `session.OwnsWindow(window)` 这道**所有权检查**：按钮回调不看 owner 就不敢动（防止旧窗口残留按钮操作新会话），实现见 `StationSession.OwnsWindow`（`:50-57`）。

### 3.4 三级产物 + 电量门 + 事务（`FishingCuisineExpansion.Mod.Cooking.Services/CookingService.cs:56-118`，节选）

```csharp
if (method != "鱼生")                                    // 鱼生不耗电，其余要 5 点
{
    var batteryItem = battery.childItems?.Count > 0 ? battery.childItems[0] : null;
    if (!PowerHelper.CanDrawPowerSource(batteryItem, 5)) { context.Feedback(L("电池槽需要至少 5 点可用能量。"), 4f); return; }
}
var recipe = cookingSelection.Recipe;
bool exact = recipe != null && string.Equals(recipe.PreferredMainId, cookingSelection.MainId, StringComparison.OrdinalIgnoreCase);
string dishId = recipe == null ? "fce_dish_bad_meal" : (exact ? "fce_dish_recipe_" + recipe.Id : "fce_dish_fish_box_meal");
if (!Runtime.DishById.TryGetValue(dishId, out var def)) { context.Feedback(L("料理成品未注册，食材未消耗。"), 4f); return; }
var outcome = CookingOutcomeCore.Calculate(new CookingOutcomeInput(/* 克重合计 */, /* 成本 */, recipe != null, recipe?.TargetScore ?? 0,
        recipe != null ? Runtime.RecipeMainPenalty(recipe, mainId) : 0, /* 做法修正 */, /* 鼠鱼加辣彩蛋 */));
var dish = DirectoryMaster.Item(def.Id, true);
if (dish == null) { context.Feedback(L("料理成品未能创建，食材未消耗。"), 4f); return; }   // 先确认能造，再扣料
transaction.TrackDish(dish);
dish.SetName(L(source)); dish.unitValue = outcome.Value;
FoodItemHelper.InitFoodItem(dish, kcal, false, false);                                    // 交回原版食物系统
```
**顺序是"先判电量 → 再判成品可造 → 才进事务扣料"**，`CookingTransaction`（114 行）负责失败回滚——这条顺序是本档案最该抄的一段。

---

## 四、踩坑与红线

1. **不要在两个包里互相写死 `<Prerequisites>`**。原版加载器只警告不禁用（`ModLoader.cs:1264-1267`、`:1694-1697`），但**模组管理器/玩家会把它当硬依赖**：GoFishing 那份档案已判"声明硬依赖会让玩家装不上，即使代码能优雅降级"（`3829110153-GoFishing.md:378`）。正解是**单边声明**（谁用谁声明），或干脆空 `<Prerequisites/>` + 代码里 `ModHelper.IsModRunning` 探测。
2. **字符串契约要当成 API 管理**：58 个 `gf_*` 字面量散在 9 个文件里，钓鱼包改名我们无从得知。若抄这套，**把这些 id 收进一份 JSON 契约（它已有 `content.fish-materials.json` 251 行可扩展）+ 启动时校验**，别散在 `switch` 里。
3. **`InstallEnglishTranslations` 是死代码、`SushiBoardId` 是悬空常量**：前者签名齐全并绑定 `official-localization-2.0.1` 契约，函数体却直接 `return false`（`LocalizationRuntime.cs:44-47`）；后者全库仅声明一次（`Runtime.cs:89`）、`items.json` 里没有这件厨具，"鱼生"实际靠"不放厨具 + 无火 + 无油"判出（`CookingMethodRules.cs:7-13`）。抄之前别把这两处当"已实现"。
4. **自造 403 行 JSON 解析器是纯负担**：`CuisineJson/*`（`StrictJson` 208 行）只为了在 `FishCanIngredientReader` 里解析一个 5 字段的罐头批次 tag（`:39-55`），而工程已经引用了 `Newtonsoft.Json`（`.csproj:33`）。这是"防御性自造轮子"的典型。
5. **33 鱼 × 7 做法的修正写成了两个巨型 `switch`**（`CookingScoringRules.cs:98-636`，约 540 行）。加一条鱼要改代码、加一条做法要全改——**这里才是该上 JSON 表的地方**（`BaseScore` 的 6×7=42 格同理，`：7-72`）。
6. **补丁按"序号命名"是反模式**：`CuisinePlayerStoreLoadGamePrefix5Patch` / `...Postfix6Patch` 这类名字（18 个文件），从类名看不出靶点，只能靠文件内 `[HarmonyPatch]` 反查。我们自己坚持 `XxxPatch` 用方法名命名。
7. **`ValidateRecipeWhitelist` 失败 = 整个模组不激活**（只打一条中文 `LogError`）。这是"宁可不能用也不出错菜"的取舍，值得学；但要配一条**玩家看得懂的人话**（它只写日志，玩家只会看到"模组没反应"）。

---

## 五、对我们的可用点

1. **`CuisineDeferredActivation` 三段闸门（51 行）→《深空电玩》的"等原版物品目录就绪再注册"**。我们若干个 `OnModItemDirectoryInit` 模块各自判 `Ready`，可以统一成这一份 `Request/Signal/Cancel`：`Requested` 与 `Started` 分开、异常时双清零、`Cancel` 对称。位置：`CuisineDeferredActivation.cs:15-50`，可直接抄成 `ArcDeferredActivation`。
2. **`ModPresentationRuntime`：只给自己的模组页条目做本地化**（`ModPresentationRuntime.cs:38-53`）。`ModElement.Init(string id, ref string name)` 里比较 `id == 自己`，再用 `WeakReference<ModElement>` 列表跟随语言切换刷新。→《深空电玩》《深空当铺：可能是群友》目前的模组管理页名字是 manifest 里的中文，中英切换时都不动；这套 90 行能直接补上，而且键门禁保证**不碰别人的条目**。
3. **"借 `MachineFurnace.CreateMachineInventoryWindow` + 自己重排 UI"→《深空电玩》的刻录机/整备台若要加槽位**。我们现在的机器窗口是原版机器直出；若要做"多一个成品格/一个耗材格"，这是不新造机器类的最小路径，配套 `MachineHelper.SetupBatterySlot` + `ContainerHelper.AllowOnlyTaggedItemsOr` 做准入。注意它给的教训：**已存在的物品要在 `UpgradeStationWindow` 里做增量补窗口**（`Runtime.cs:1090`），不能重建。
4. **三级产物（正品 / 鱼盒饭 / 难吃的饭）+ 主材身份 tag →《深空当铺：可能是群友》塞壬验水/伞萝萝点心的"半成品"档位**。《深空电玩》的"翻新整备"同理：翻新成功=正品、翻新但件不匹配=降级件、失败=废件，用**一个 `FCE_DISH_MAIN` 式 tag 记主材身份**（`Runtime.cs:163-179` 的 `FCE_DISH_*` 常量组），显示名与结算都从 tag 读，不读物品名。
5. **`CuisineUnlockApi` 式的 32 行跨模组门面**（`Installed=>true` 哨兵 + `Ready/Price/Try*(out message)`）→《深空电玩》与《深空当铺：可能是群友》之间的互操作可以照这个形状：**一个静态类、只暴露只读状态与 `Try` 前缀命令、参数只用 BCL 类型不暴露自家类型**（这样反射方不用引用我们的程序集）。我们已有的 `DspInterop`（群友 api.v1）可对齐这套命名。
6. **"先判电量 → 再判成品可造 → 才扣料"的三段式**（`CookingService.cs:60-113`）→《深空电玩》刻录机（先判电池与空白盘、再判能否创建盘、最后扣料）与"概率维修"（先判件对不对、再判故障、最后扣件）都应该显式写成这三段，任何一段失败都保证"料没动"。

---

## 附：待验证 / 风险

- `CookingScoringRules`（757 行）只通读 `BaseScore`/`RecipeMainPenalty`/`FishMethodModifier` 的代表分支与 `FishTraitText` 头部，**中间约 500 行逐鱼数据未逐行读**；`ManualSession`（713 行）只读方法表与 `SubstituteManualPage` 入口，"翻页代际锁"时序未核；`DeliveryHintView`（428 行）未读；`CookingOutcome/CookingOutcomeCore` 只看了调用点，**份数/得分公式未验**。
- 与 GoFishing 的联动**全部靠 id 与 tag 字符串，未真机验证**；"缺 GoFishing 时退化成 1/26"是从 `content.fish-materials.json` 的 `MeatIngredients` 只有两个原版 id 推的（推测）。本档案不含真机结论，所有"能跑"的说法都来自源码静态阅读。
