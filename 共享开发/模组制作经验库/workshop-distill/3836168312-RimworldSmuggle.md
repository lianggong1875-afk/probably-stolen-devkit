# 3836168312 边缘当铺：可能是偷渡 / Probably Rimworld

> **id**：`3836168312`｜`probably_rimworld` v1.1.2｜作者 Chnangua｜1.06 万订阅｜映射基线 `0.46D`｜`<Capabilities />` 空（零能力）｜ModHook：`OnModAssetsLoaded`｜HotReload 钩子：无
> **反编译目录**：`缓存文件夹\decomp_ws\3836168312\`（`.cs` 2,525 行 + `AssemblyInfo` 12 行 = 2,537 行；包体 6,962,414 B）
> **为什么值得读**：**名字是骗人的**。「偷渡」是 `Rimworld → 可能是偷渡` 的谐音梗，里面**没有一丝偷渡/查获玩法**。它是全库工程纪律最严的一份**立绘替换**：一套「白名单 + 尺寸指纹 + 修改记账 + 可回滚」的资源接管框架，正好补我们立绘层最缺的那块——**别人也在改同一张 sprite 时怎么办**。

## 一、规模与结构

| 文件 | 行数 | 占比 | 角色 |
| --- | --- | --- | --- |
| `ProbablyRimworld/PortraitRuntime.cs` | 1,090 | 43.2% | 运行时：字典接管、场景直改、过场图、50% 池、还原 |
| `ProbablyRimworld/ConfigurationReader.cs` | 499 | 19.8% | **手写 JSON 解析器**（零 Newtonsoft 依赖） |
| `ProbablyRimworld/PortraitModePolicy.cs` | 191 | 7.6% | 40 个原版普通顾客 id → 立绘族映射 + 族名解析 |
| `ProbablyRimworld/ConfigurationValidator.cs` | 179 | 7.1% | 三道闸：schema/MVID、条目合法性、白名单 |
| `ProbablyRimworld/OverrideJournal.cs` | 129 | 5.1% | **泛型修改记账本**（Apply / RestoreAll / RestoreOne） |
| `ApprovedBindings.cs` | 46 | 1.8% | 200+ 条硬编码签名白名单（最长一行 3 KB） |
| 其余 21 个文件 | 402 | 15.9% | 补丁壳 6 个（共 88 行）、数据类 4 个、诊断 4 个 |

**五文件吃掉 83%**（PortraitRuntime + Reader + Policy + Validator + Journal）。补丁壳只有 88 行 —— 所有判断都在托管层，补丁只做转发。

### 6.6 MB 的构成：**97.7% 是图**

| 组成 | 字节 | 占比 |
| --- | --- | --- |
| `Portraits/*.png`（**215 张**） | 6,802,118 | 97.7% |
| `ProbablyRimworld.dll` | 102,400 | 1.5% |
| `portraits.json`（配置） | 55,238 | 0.8% |
| `README.txt` + `manifest.xml` | ~2,600 | 0.04% |

单图平均 32.9 KB（最大 88 KB），**没有雪碧图、没有图集**——一张立绘一个文件。三种固定尺寸：顾客立绘 **288×211**、发明家场景 960×540、通缉令纸 666×930。

## 二、它怎么替换立绘（三个挂点）

### 挂点 A：`SpriteDict.spriteDictionary` 原地替换（主战场）

原版所有顾客立绘都存在一个 `Dictionary<string, Sprite>`（`Assembly-CSharp/SpriteDict.cs:127`）。模组在 `Awake` / `InitializeDictionary` 之后把这张表里的条目**换掉值**：

```csharp
// PortraitRuntime.cs:385-424（简化）
foreach (PortraitEntry item in configuration.Dictionary) {
    if (!dict.TryGetValue(item.runtimeKey, out Sprite value) || !Object.op_Implicit(value))
        { WarnOnce(...); continue; }                       // 键不在就报，**不建兜底图**
    if (originalByReplacement.ContainsKey(value)) continue; // 已经是我们的，跳过
    if (!Matches(value, item))
        { WarnOnce("original:" + key, "original guard mismatch"); continue; }  // ← 指纹不符就放手
    Sprite val = Replacement(value, item);
    switch (dictionaryJournal.Apply(dict, item.runtimeKey, value, val)) { ... }
}
```

`Matches` 就是「这真的还是原版那张图吗」的判据 —— **不是 MVID、不是 hash**，而是 `sprite.name` + `sprite.rect` 宽高 + `pixelsPerUnit > 0`（`:689-704`）。别的立绘模组只要换了同一键的图（名字或尺寸变了），这条就自动让位。

### 挂点 B：场景内 `SpriteRenderer` / `Image`，与过场图 `TransitUI.ResolveImage`

`ApplyDirectScenes`（`:775-848`）按 `sceneName + hierarchyPath + componentType` 精确定位，且要求 **`GetComponents<T>().Length == 1`**（组件不唯一就不动）。目标只有三个实体（`portraits.json` 的 `directScene`）：发明家场景的 `SpriteRenderer`、通缉令纸的 `Image`、两个安保 NPC。
`ApplyDirectScenes` 之外，过场图走 `TransitUI.ResolveImage`（`:193-235`）：只放行 `travelPreview` 里那一条（`imageKey == "dr_jackson"`，960×540），并要求 `original.rect` 的 `x/y` 都为 0（`TravelPreviewPolicy.cs:16-23`）——防止把图集里的子图当成整图。

### 两个「可选模式」

`PortraitModeSettings`（`portrait_mode` 配置项，**改完必须完整重启**）：

| 模式 | 行为 | 触发点 |
| --- | --- | --- |
| `完整替换`（默认） | 字典 + 场景 + 过场图全上 | 场景加载 / `ShowStage` / 字典重建 |
| `扩展普通 NPC 立绘池` | **原图保留**，只有 40 个普通顾客 id 有 50% 概率换同族立绘 | `PlayerStore.GetNextClient` |

50% 池的关键在 `StablePortraitChoices`：用 `ConditionalWeakTable<StoreClient, Choice>` 把「这次抽到什么」**挂在顾客对象上**，同一顾客在场期间反复查询结果不变（`StablePortraitChoices.cs:24-41`）；`portrait_mode` 里的说明也写了「特殊人物不参与」（`Eligible` 要求 `!isWanted && !isStealthWanted && wantedPosterId 空 && eventSourceId 空`，`PortraitModePolicy.cs:77-84`）。

## 三、Patch 靶点表

| # | 靶点（原版签名） | 位置 | 类型 | 干什么 |
| --- | --- | --- | --- | --- |
| 1 | `SpriteDict.Awake`（`private`，`Assembly-CSharp/SpriteDict.cs:133`） | `DictionaryAwakePatch.cs:5-11` | Postfix | 字典就绪 → `Refresh()` |
| 2 | `SpriteDict.InitializeDictionary`（`private`，`:146`） | `DictionaryRebuildPatch.cs:5-11` | Postfix | 字典重建（换语言/换图集）后再上 |
| 3 | `SpriteDict.ShowClientSprite(string)`（`:242`） | `PortraitQueryPatch.cs:7-14` | Postfix `(string __0, Sprite __result)` | **只诊断不打补丁**：把每次查询归类打日志 |
| 4 | `TransitUI.ResolveImage(string)`（`private static`，`TransitUI.cs:338`） | `TravelPreviewPatch.cs:7-14` | Postfix `ref Sprite __result` | 过场图替换 |
| 5 | `MapUIManager.ShowStage(AfterhourLocation)`（`private`，`MapUIManager.cs:305`） | `MapStagePatch.cs:5-11` | Postfix | **借它当「夜晚地点舞台刚显示」的重绘信号** → `Refresh()` |
| 6 | `PlayerStore.GetNextClient`（`:1331`） | `OrdinaryCustomerArrivedPatch.cs:5-19` | Pre/Post + `out StoreClientInstance? __state` | 50% 池模式：顾客变了就重抽立绘 |

**`ShowStage` 这一条最能说明它的定位**：原版 `Travel → ShowStage(location)`（`MapUIManager.cs:184`）是夜晚外出到地点的舞台显示，模组**只挂它的 Postfix 重画一遍立绘**，不注册地点、不插手流程、不改 `Current`。

## 四、五档阶梯定位：**不在五档里（第 0 档·纯表现层）**

`14-模组高级编程架构与工程模式.md` §9.4 的五档（① 开局特性 / ② 蹭原版地点 / ②′ 官方注册面 + 自建舞台 / ③ 子类 + 反射伪造 `Current` / ③′ 官方 `Register` 真子类）**全部是关于「把新玩法接到原版地点与流程上」**。

本模组一个都不占：

- 全库 `AddAfterhourLocation` **0 次**、`AfterhourLocation` 子类 **0 个**、`ModReflect` **0 次**；
- 不碰营业日、不碰过夜、不碰外出次数、不加任何 `StoreEvent`；
- 它的三个挂点是**数据层（sprite 字典）**、**表现层（场景里的 `SpriteRenderer`/`Image`）**、**过场动画（`TransitUI`）**。

**正确归位：它是 §9.4 之外的「第 0 档」——纯表现层替换，与 §9.3「表现层可以完全无状态」同类。** 判断口诀：**它的补丁即使全部删掉，游戏流程一秒都不变，只是画变回去。**

## 五、查获 / 风险系统：**没有**（问题 3 是空集）

- 全库 grep `ContrabandHelper` / `SecData` / `Inspect` / `Smuggl` 只命中一处：`PortraitModePolicy.cs:48` 的 `["scavInspectable"] = "Scav"` —— 那是**一个原版普通顾客 id** 的字符串，与查获无关。无概率、无判定、无失败后果。
- 原版那套**是存在的**（`StoreClientManager.dayUntilInspection` / `INSPECTION_MIN_TRAFFICKING_CRIME = 50`，`Assembly-CSharp/StoreClientManager.cs:73-85`），本模组**完全没碰**。对照：**我们自己的《深空电玩》#7 才有真查获**（`StartInspection` → 没收销毁 → 台账 `fine` → 1500 保释，见 `深空电玩/PROGRESS.md`）。

**结论：「偷渡」纯属标题谐音，不要按玩法模组去读它。**

## 六、关键代码摘录

**A. 三道闸（`ConfigurationValidator.cs:56-61` + `109`）** —— 配置不是「读进来就用」：

```csharp
if (config == null || config.schemaVersion != 1
    || config.gameVersion != "0.46D"
    || config.gameAssemblyMvid != "83045710-adce-4e78-8ff1-b43a9399ec0d")   // ← 绑死某一次构建
    throw new ArgumentException("Unsupported configuration schema or build lock.");

// 条目级：资源名/路径/尺寸合法 + runtimeKey 不重复 + asset 不自相矛盾 + **签名在白名单里**
if (!ValidAsset(e) || !SafeName(e.runtimeKey) || dup[key] != 1
    || conflict.Contains(e.resourceKey) || !ApprovedBindings.IsApproved(e))
    warn("config dictionary entry skipped: " + key);   // 跳过而非抛错
```

**B. 修改记账（`OverrideJournal.cs:41-79`）** —— 「改前记账、改时校验、被抢就让」：

```csharp
internal ApplyResult Apply(TTarget target, string slot, TValue original, TValue replacement) {
    foreach (Record r in records)
        if (r.Target == target && r.Slot == slot)
            return get(target, slot) == r.Replacement ? ApplyResult.AlreadyApplied : ApplyResult.Superseded;
    if (get(target, slot) != original) return ApplyResult.Superseded;   // 别人先改了 → 放手
    Record item = new Record(target, slot, original, replacement);
    records.Add(item);
    try { set(target, slot, replacement); }
    catch { /* 回滚：只有确认自己写进去了才撤记录 */ ... throw; }
    return ApplyResult.Applied;
}
```

**C. 换图不缩水（`PortraitRuntime.cs:741-752`）** —— 保住原 sprite 的 pivot 比例、ppu 与 border：

```csharp
Vector2 pivot = new Vector2(original.pivot.x / original.rect.width, original.pivot.y / original.rect.height);
Sprite val2 = Sprite.Create(tex, new Rect(0, 0, entry.width, entry.height),
                           pivot, original.pixelsPerUnit, 0u, SpriteMeshType.FullRect, original.border);
```

**D. PNG 守卫（`PngGuard.cs:5-28`）** —— 33 字节起、8 MB 上限、验 magic + `IHDR` + 宽高必须与配置**完全相等**，随后 `LoadImage` 再验一次实际尺寸：

```csharp
if (bytes == null || bytes.Length < 33 || bytes.Length > 8388608) return false;
// 137 80 78 71 13 10 26 10 → 长度 13 → "IHDR" → width/height 大端比对
```

## 七、踩坑与红线

1. **MVID 硬锁与「版本只是信息」自相矛盾**。`ConfigurationValidator` 把 `gameAssemblyMvid` 写成常量（`83045710-…`），游戏一换 build 整张表作废并 `throw`；而 `RuntimeVersionPolicy.Describe` 又打日志说「version label is informational, not a compatibility guarantee」（`RuntimeVersionPolicy.cs:5-8`）。**实际行为是「一次构建一把锁」**——我们照抄时要么全押 MVID，要么别写这句自相矛盾的话。
2. **白名单硬编码在 DLL 里**（`ApprovedBindings.cs:8-35`，200+ 条 `runtimeKey|资源|原名|宽|高` 拼接串）。用户加一张图 = 作者改 DLL 发版。**这是它最大的扩展性缺陷**，也是它无法像 PortraitRuntime 那样做内容包的根本原因。
3. **288×211 是死尺寸**。`PngGuard` 要求 IHDR 与配置一致，配置又要求与白名单一致 —— 想上 2 倍图要改三处。
4. **指纹太弱**：`Matches` 只看 `name + rect + ppu > 0`。别的模组如果**沿用了同一个名字和尺寸**，这里认不出来，会覆盖别人的图（能被 `OverrideJournal` 的 `Superseded` 挡住，但只在「别人先写、我们后写」的顺序下）。
5. **卸载后可能不释放**：`RestoreAndRelease` 只有在三本账全清、且全场景扫描没发现残留替换图时才 `Destroy` 贴图（`:1044-1067`）；否则 `return false`，`assetsReady` 保持 `true`，图留在显存里。这是**故意的**（防止销毁仍在显示的 sprite），代价是热重载场景下的显存泄漏。
6. **50% 池不持久**：选择挂在 `ConditionalWeakTable<StoreClient,…>` 上，顾客对象一销毁就没了（**推测**：同一 id 下次出现会重新抽签，立绘可能跟上次不一样）。
7. **`portrait_mode` 改完必须完整退出重启**（配置项说明原文），因为它只在 `Init` 时读一次（`PortraitModeSettings.cs:11-28` 的 `captured` 闸）。
8. **与 `PortraitRuntime`（3831627217）互斥**。那个运行时启动时会检测 legacy 立绘模组并直接拒绝挂钩（字符串证据 `"Legacy portrait mod enabled; disable it before enabling PortraitRuntime. No hooks installed."`，见 `立绘核心库与两个小工具.md` §一）。**两者同时开，必有一边不生效。**
9. **`Object.op_Implicit` 满场飞**（`value`、`image`、`instance`）—— Unity 假 null 检查一次不漏。这与我们 `07-踩坑台账` 里「对 `MonoBehaviour` 用 `??` 」是同一条命门。
10. **零存档、零 ES3**：`ApplyResult` / 选择 / 缓存全部内存态，读档由原版重建 `SpriteDict` 后重新 `Refresh()` 覆盖回来。**没有私有键**，卸载干净。

## 八、对我们的可用点

### 1. 《深空电玩》#1c 八张 `dnpc_*` 立绘（1470×1070 / ppu 694.8）

我们换纹理时最该抄的是 `Sprite.Create` 那 4 行（上面摘录 C）：**pivot 按比例折算、`pixelsPerUnit` 沿用原图、`border` 沿用原图**。直接把新 `Sprite` 塞进 `SpriteRenderer.sprite` 而不折算 pivot，会出现「图没换错，但人物位置偏了半个身位」——电玩八张立绘是严格居中贴合台面的，一偏就露馅。

### 2. 《深空电玩》`ArcPortrait` 的显存治理

`PortraitRuntime` 有**三层缓存 + 负缓存 + 上限**：`textures`（按 `resourceKey`）/ `sprites`（按原 sprite 分桶）/ `failedResources`（失败不再重试）/ 1024 张硬上限（`:716-720`）。对照我们本轮刚修的「替换纹理显式 `Destroy(existing)` 消除 ~100MB 泄漏」（`深空电玩/PROGRESS.md` 阶段 2）：**我们缺的正是 `failedResources` 这层负缓存**——坏图每帧重试解码是最贵的失败模式。

### 3. 《深空当铺：可能是群友》热重载的对称还原

`OverrideJournal`（129 行、泛型、零 Unity 依赖）就是我方 `HotReloadPrepare/Activate` 需要的那个东西：`Apply` 记原值、`RestoreAll` 逆序还原、`RestoreOne` 单点还原、`ForgetDestroyedTargets` 清理已被销毁的目标。**我们现在是手写 `Unregister()`，逐个字段还原**——换成这本账，热重载漏还原的概率直接归零。

### 4. 《深空电玩》/《深空当铺》外置立绘加载的入参守卫

`PngGuard`（34 行）可直接搬：33 字节下限 + 8 MB 上限 + magic + IHDR 宽高双向核对，`LoadImage` 之后再验一次实际尺寸。我们的立绘外置加载（PEF 兜底 / 自建 pack）目前没有这层，坏一张图就是一次 `LogError` 洪水或一次卡帧。

### 5. 反面清单（**别**照抄）

❌ 硬编码白名单（加图要改 DLL，我们做内容包必须文件即数据）｜❌ `gameAssemblyMvid` 硬锁（跨版本热更必炸）｜❌ 死尺寸 288×211（电玩立绘 1470×1070，尺寸必须来自图本身）｜❌ 「改完重启游戏生效」（我们的配置项要能即时生效）。
