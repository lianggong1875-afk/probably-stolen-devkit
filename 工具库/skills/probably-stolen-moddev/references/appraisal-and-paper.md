# 鉴定扩展、自定义放大镜、交互纸张与关系网科技树

提炼自工坊鉴定生态四件套：`Appraiser+`、`AppraisalAssistant`、`AppraisalRules`、`AppraisalAssistantCompatibility`（四者全部实现零能力 `<Capabilities/>`）。

## 1. 注册自定义鉴定品类与突破鉴定等级上限

1. **物品工厂**（`ModHook.OnModItemDirectoryInit`）：
   ```csharp
   var item = dir.Empty();
   item.EnableTag("INSPECTABLE_TAG");
   AppraisalHelper.InitAppraisalItem(item, defaultEstimate);
   ```
2. **鉴定注册**（`OnEnable` / `OnDisable` 成对增删）：
   ```csharp
   ModHelper.AddAppraisalItem(modId, typeId, itemId, unlockRank,
       () => CreateRandom(), item => CalcValue(item), item => IsMyType(item),
       guideItemId, () => LocalizedName());
   // OnDisable: ModHelper.RemoveAppraisalItems(modId);
   ```
3. **扩充鉴定师等级**：通过 `ModReflect.GetStatic<AppraisalRankHelper.Rank[]>(typeof(AppraisalRankHelper), "Ranks")` 零能力读取并备份原版 4 级表，配合 `AppraisalRankHelper`（`GetRank/SetRank/CanProgress/Promote/GetCertificateRank/GetRankData/GetRankName`）与 `StoreClientListAppraisal.CreatePromotionOffer` 的小补丁平滑扩级，停用时按备份还原。
4. **自定义开局剧本**：`ModHelper.AddStartingScenario(modId, id, titleFn, bodyFn, gearFns, onStart, iconKey, tagFn, tone, skipIntro)`，停用调用 `RemoveStartingScenarios(modId)`。

## 2. 自定义物品接入原版放大镜（双树镜像 + LensFollow）

在 `[HarmonyPatch(typeof(InspectionUIManager), "OnInspectItem")]` Postfix 中：
- 底图挂到 `InspectionUIManager.Instance.ribwichUISmall.transform.parent`，并将 `SiblingIndex` 设为 `magnifyingGlass.transform.GetSiblingIndex()`，使放大镜盖在上面；
- 放大版挂到 `InspectionUIManager.Instance.ribwichUILarge.transform.parent`，用同一套绘图代码画第二遍；
- 给放大版挂 `LensFollow` 组件，在 `LateUpdate()` 按公式实时同步：
  ```csharp
  Vector3 p = lens.position + zoom * (target.position - lens.position);
  p.z = target.position.z;
  transform.position = p;
  float ps = transform.parent != null ? transform.parent.lossyScale.x : 1f;
  float s = ps == 0f ? zoom : zoom * target.lossyScale.x / ps;
  transform.localScale = new Vector3(s, s, 1f);
  ```
- 在 `CloseUI`、`ResetUI` 与 `OnDisable` 中清理双根节点。

## 3. JSON 驱动的交互式 Paper（计算器 / 终端 / 手册）

不需要手搓 uGUI 就能做出带拖拽、按钮悬停贴图、点击音效和动态文本的便携仪器：
- 在 `Books/<bookId>.json` 中定义 `background`、`width/height`、`draggable`、`chrome`（含 `action: "close"` 按钮）与 `pages[].elements`（`type: "text"` 带 `id`，`type: "button"` 带 `action` / `sprite` / `hoverSprite`）。
- C# 里零 Harmony 接线：
  ```csharp
  ModHelper.OnPaperOpened(modId, "my_terminal", ui => {
      ui.OnAction("btn_scan", () => { if (ui.IsAlive) ui.SetText("disp", RunScan()); });
      if (ui.IsAlive) ui.SetText("disp", InitialText());
  });
  ```

## 4. 把功能做成关系网科技树升级（AppraisalRules 范式）

在非主菜单状态下（`NewGameData.Instance != null && !NewGameData.Instance.isInMainMenu`）：
```csharp
ModHelper.SetNetworkTabLabel("MyTab", () => GetTabLabel());
ModHelper.AddNetworkUpgrade(modId, "UPGRADE_1", 300, "MyTab",
    () => title, () => desc, null, "MyTab", modId + ":icon1",
    prerequisites: null, tier: 0, () => subtitle, () => boughtText);
```
运行时通过 `ModHelper.IsNetworkUpgradeUnlocked("UPGRADE_1")` 控制能力开放，卸载时调 `RemoveNetworkUpgrades(modId)` 与 `SetNetworkTabLabel("MyTab", null)`。

## 5. 鉴定辅助、改造体探测与零依赖跨模组兼容

- **改造体（Augmented）三大原生特征**：
  1. `StoreClientMono` 私有 `idleAnimator` / `idleShakeHorizontal` 呈水平高频微震（`axis.x > 0.9f && axis.y < 0.01f`）；
  2. `SpriteDict` 内含 `augLower/Upper/Bm` 专用立绘池；
  3. 监听 `Febucci.UI.Core.TypewriterCore` 打字机完成事件捕捉异常台词。
- **安全填入估价**：确认 `clientIntent == StoreClient.ClientIntent.APPRAISAL_SERVICE`、`appraisalPanel.activeInHierarchy`、`apprisalServiceInputField.IsInteractable()` 且非只读，用 `SetTextWithoutNotify` 填数；自动提交前须检查 `!NegociationUIManager.IsTradeLocked()` 且对话未激活，再触发持久化绑定了 `OnAppraisalServiceEstimated` 的唯一按钮。
- **零强依赖兼容第三方模组**：不引用对方 DLL，直接通过 `item.identifier`、`IsTag`、`GetTagReadonly`、`FindItemFeatureByCategory` 读取状态，在 `ModHook.OnCreateTooltipLate` 里用 `RenderHandler.ColorPalette` 输出兼容读数，异常做 30s 节流日志。
