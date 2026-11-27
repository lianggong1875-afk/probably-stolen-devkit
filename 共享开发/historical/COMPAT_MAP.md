# 兼容性地图：本机 19 个已装模组的补丁占用情况

> 方法：反编译每个工坊 DLL（`ilspycmd -p`），扫 `[HarmonyPatch(typeof(X), "M")]`、`ModHook.X +=`、`dir.Set("id"`。
> 证据等级：`[静态 · 反编译当前版本]`。游戏或模组更新后需重跑。
> 快照：`probably-stolen\decomp\workshop-mods\<workshopId>\`

## 1. 结论先行：三个高危区

| 高危区 | 被谁占 | 对本项目的含义 |
|---|---|---|
| **模组管理 UI**：`ModConfigWindow.Open/Apply/BuildEntry/ReadWidget/WriteWidget`、`ModUIManager.ShowMod/CreateRow/OpenPanel/PopulateLayoutList`、`ModElement.Init` | 5 个模组（DocumentThickness、PhonebookPage、NewspaperIllustrations、VanillaMachineAnimations、WaterSeller）+ 2 个（Hagicode、CommissaryExpansion 部分） | **绝对不要碰。** 这是全服最拥挤的一处，任何 Prefix 返回 false 都会让 5 个模组的补丁一起失效。 |
| **物品交互链**：`ItemMultiSelectHandler.OnEventPress`、`ItemMouseDragHandler.EndDrag`、`ItemContextHandler.OnEventInit/OnEventPress/TriggerButton/TryOpenSortMenu/SetButtonColor`、`ItemMouseDoubleClickHandler.ClickAction`、`SlotMarker.TryAcceptOnce`、`WindowsHandler.Cancel`、`RichTextGenerator.InsertItemInOtherItem` | SmartInventoryArrange（v3.17.68，2.6 MB，深度占用全套）+ PhonebookPage + DocumentThickness + CommissaryExpansion | 做「右键菜单 / 多选 / 拖拽落点」时必须用 **Postfix + 只改返回值**，或在**自身矩形/自身物品**范围内才 `return false`。这就是知识库坑 #34/#47 的实际战场。 |
| **客户与夜间结算**：`PlayerStore.GetNextClient/SetupWaitingClient/QueueFuturClient/EndNight/SaveGame/FindAllItem`、`StoreClientManager.Pick*`、`RandomClientPool.Pick`、`StoreClientListDict.CreateStoreClient`、`StoreClientList.AddIfNotOwn`、`StoreUIManager.*`、`MapUIManager.ActivateClientOutsideStore` | CutePortraits、MaidensPawnshopHD、WaterSeller、PhonebookPage、CommissaryExpansion、DocumentThickness、StellarOptimizer、AugGunHighlight | 做客户/商人类功能优先用官方 `ModHook.OnGenerateCustomer*`；真要补丁，只能在 Postfix 里追加，不能拦截原逻辑。 |

次级热点（2 个模组共用，仍需避开 Prefix 拦截）：`InputActionManager.Update`（DocumentThickness + VanillaMachineAnimations）、`SpriteDict.GetSprite`（Hagicode + CommissaryExpansion）、`GameItemElement.GetTooltipBasic/ValidateSelectLocked/HandleInnateModSprite`（VanillaMachineAnimations + CommissaryExpansion）、`GameItem.SetSprite/SetSpriteAndShape/CloneLinked/ToggleSlotItem`（DocumentThickness + SmartInventoryArrange 部分）、`GeneralHelper.SetItemOwned/SetCustomName`、`CustomEventHandler.Update/UpdateRayCast`、`MiscItemDirectory.Newspaper/FoodStamp`。

## 2. 两个值得单独记住的样本

**① `DirectoryMaster.Item` 是可以补的 —— 前提是显式写参数类型**
NewspaperIllustrations 实测在跑（`trust=Match running=True`）：

```csharp
[HarmonyPatch(typeof(DirectoryMaster), "Item", new Type[] { typeof(string), typeof(bool) })]
```

当前程序集里 `Item` 确实有两个重载：`Item(string identifier, bool isOwned = true)` 与 `Item<K>(K identifier) where K : Enum`。所以知识库坑 #1 的**结论成立**（按名字匹配会 `AmbiguousMatchException`），但「不能补」不准确 —— 正确说法是**必须用 `new Type[]{...}` 指定签名**。本项目若需要拦截物品创建，照此写法并加 try/catch。

**② `ModHook.FireOnGameLoadedLate` 被 Hagicode 的模组更新器直接补丁了**
说明框架自身的事件派发点也在别人的火力范围内。含义：我们的 `ModHook.X +=` 订阅仍有效（订阅是往委托链上追加，不受派发点补丁影响），但如果我们的处理器依赖「在别人之后/之前执行」，那是不成立的。**只依赖自己拿到的参数，不要假设执行顺序。**

## 3. ModHook 事件占用分布（44 个事件中实际被用的）

| 事件 | 用它的模组 |
|---|---|
| `OnGameLoadedLate` | 鉴定助手、DocumentThickness、Hagicode、CommissaryExpansion、WaterSeller |
| `OnGameLoadedInit` | SmartInventoryArrange、DocumentThickness |
| `OnGameLoadedNormal` | 鉴定规则、PhonebookPage |
| `OnNewGameStarted` | 鉴定助手、鉴定规则、CommissaryExpansion |
| `OnModItemDirectoryInit` | DocumentThickness、CommissaryExpansion、WaterSeller |
| `OnModAssetsLoaded` | DocumentThickness、VanillaMachineAnimations、NewspaperIllustrations |
| `OnGenerateCustomerNormal` | DocumentThickness、Hagicode |
| `OnGenerateCustomerEarly` | CommissaryExpansion |
| `OnWakingUpLate` | DocumentThickness、Hagicode |
| `OnShutterOpenedLate` | DeepSpaceTrainer、CommissaryExpansion |
| `OnGoingSleepEarly` | DocumentThickness |
| `OnLootTablesLoaded` | DocumentThickness |
| `OnModConfigApplied` | DocumentThickness |
| `OnCreateTooltipLate` | 鉴定助手 |

**未被任何模组占用**（44 − 上表）：`OnGameLoadedEarly`、`OnDifficultyApplied`、`OnGoingSleepLate`、`OnHandlingNightlyServicesEarly/Late`、`OnLeavingStore*`、`OnReturningStore*`、`OnShutterOpenedEarly`、`OnShutterClosedEarly/Late`、`OnWakingUpEarly`、`OnGenerateCustomerVeryEarly/VeryLate/Late`、`OnPlaceInventorInventoryItemEarly/Late`、`OnPaperOpened`、`OnAfterhour*`（6 个）、`OnCreateTooltipEarly`、`OnCreateDevTooltip*`、`OnLootRolled`、`OnNetworkUpgradeUnlocked`、`OnGameLoadedEarly`。→ 这些是**零竞争的干净入口**，能覆盖需求就优先选它们。

## 4. 本项目自定的兼容性红线

用户要求：**与已装模组兼容，但绝不依赖它们；纯原版环境（只装本模组）也必须完整可用。**

因此：

1. **编译期**：只引用游戏自己的 `Managed/` 程序集 + `0Harmony`。不引用任何第三方模组 DLL，不打包 `0Harmony`（会触发 `bundled-framework` 拒载）。
2. **manifest 不写 `<Prerequisites>`**。装不装别的模组都不影响加载。
3. **不覆盖委托，只追加**。`mayInventoryAddItemFunc` / `onSlotAddItemEndFunc` / `mayThisTargetItemFunc` 这类字段必须 `Delegate.Combine`（`+=`），不能 `=`。原版和其它模组的行为要能继续跑。
4. **优先 Postfix**。Prefix 只在「明确属于自己」的对象上返回 false（自己的 tag、自己的容器、自己的窗口矩形且窗口可见），其余一律放行。
5. **撤补丁只撤自己**：`harmony.UnpatchAll(harmony.Id)`，绝不用无参 `UnpatchAll()`。
6. **命名全部加前缀**：物品 ID、tag 名、Harmony ID、配置 key、本地化 key。避免与 19 个模组以及未来模组撞车。
7. **不碰**：模组管理 UI 全家、`ModHook.Fire*` 内部、`ModTrust*` / `ModLoader.*` 信任层（后者是官方红线，见知识库 docs/10）。
8. **不改存档结构、不改原版面板顺序**（知识库坑 #23/#24/#30）。这是与别人冲突面最小、也是旧档最安全的路。
9. **不假设执行顺序**：自己的处理器只依赖自己收到的参数与自己的状态，不读取别人中间结果。
10. **异常必须吞在入口**：每帧/每点击路径上的补丁全程 try/catch。别人补丁抛异常 → 框架会捕获并记录；我们抛出去就可能打断整条输入链（知识库坑 #10/#14 描述的就是这种「读档后整局无法操作」）。

## 5. 待验证（等有第一个真实模组后补）

- `[待验]` SmartInventoryArrange 对 `ItemContextHandler.OnEventPress` 的补丁是 Prefix 还是 Postfix、是否会跳过原方法。这决定我们的右键浮窗方案要不要换。
- `[待验]` 同时装 CommissaryExpansion + DocumentThickness 时 `InputActionManager.Update` 的补丁叠加是否改变输入行为。
- `[待验]` 实测 19 个模组共存下开窗/拖拽的帧耗时基线（作为后续性能回归的对照）。
