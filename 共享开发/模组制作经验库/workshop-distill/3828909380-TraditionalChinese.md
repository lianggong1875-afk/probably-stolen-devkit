# 繁体中文翻译包 / Traditional Chinese Translation｜工坊 3828909380

> 工坊 id `3828909380`｜程序集 `ProbablyStolenZhHant.dll`（88,064 B）｜manifest `dbfoxtw.zh-hant` v2.14.1｜`<Capabilities />` **零能力**
> 反编译源：`缓存文件夹\decomp_ws\3828909380`（25 个 `.cs` / 5,354 行，ILSpy 输出，**变量名由反编译器生成**；本文行号均相对该目录）
> 为什么值得读：**1.39 万订阅的纯外围模组，一行存档都不碰**——它把"翻译"做成了显示层拦截 + 只读字库 + OFL 字体补字，是本工作区「文字层」「零能力边界」「二次分发授权」三件事的现成范本

---

## 0. 一句话结论

它**没有独占 `LocHelper.SetLocale`**，也没有往 `I2.Loc`/Unity Localization 里塞任何 CSV 或 `StringTableEntry`。做法是：**只在 `TMP_Text` 写入的那一刻把简中换成繁中**，同时留一张 `instanceID → 原文` 的回读表，让 `t.text` 读出来仍然是**原文**。字库一个字不改，存档一个字节不写，所以它和别的翻译包、和我们的模组天然可叠加（唯二交集见 §2.5）。

---

## 1. 规模与结构

### 1.1 三分账（包 9,772,225 B）

| 类 | 内容 | 体量 | 占比 |
| --- | --- | --- | --- |
| **代码** | 25 个 `.cs` / 5,354 行 + `ProbablyStolenZhHant.dll` | 88,064 B | 0.9 % |
| **数据** | 7 张自制表（`.tsv`/`.txt`，1,856 行）+ OpenCC 6 本字典（54,861 行） | 149,647 + 1,075,062 B | 12.5 % |
| **资源** | `data/handwritten.ttf`（Yuji Syuku 繁体改写版） | **8,444,028 B** | **86.4 %** |

结论：**9.3 MB 里 86 % 是一个字体文件**。译文条数并不存在——译文是运行时用 OpenCC 从游戏自己的简中字库算出来的。

### 1.2 5,354 行的分布（代码全是逻辑，没有一处译文硬编码）

| 文件 | 行数 | 职责 |
| --- | --- | --- |
| `TextConverter.cs` | **1,844** | 转换主脑：整句/模板/短词/触发字/写死英文规则/缓存/漏翻收集 |
| `ZhHantMod.cs` | 918 | `IMod` 入口 + 字库异步加载 + 字体回退 + 场景/语言切换重建 |
| `Translator.cs` | 712 | 六张数据表的解析 + 逐条修正 + 缺翻补译 + 单位 + 缩进 |
| `MissingTranslations.cs` | 530 | `LocHelper.Get` Postfix（缺翻补译）+ 画面缺翻消息替换 |
| `OpenCC.cs` | 351 | OpenCC 字典装载与最长匹配转换（自实现，非调库） |
| `MentorNotePatch.cs` / `MissingMessage.cs` / `NewsPatch.cs` / `Units.cs` | 244 / 138 / 125 / 111 | 行距 / 缺翻消息模板反解 / 新闻缩进 / 单位换算 |
| `ShownText.cs` + 9 个 `SetText*Patch` + `TextGetter/SetterPatch` + `TextAnimatorPatch` + 2 个杂项 | 54 + 172 + 26 + 49 + 50 | 回读表与全部拦截点 |


`TextConverter.cs` 一家占 **34.5 %** —— 真正的成本不在"简→繁"，在"**什么该转、什么绝对不能转**"。**5,354 行全是代码，译文/词表 0 行在 `.cs` 里**（数据全在 `data/` 的 1,856 行 tsv + 54,861 行 OpenCC 文本表）。

## 2. 它怎么做这件事

### 2.1 注入入口：不是 `SetLocale`，是 `TMP_Text`

| 原版类.方法 | 重载/签名 | Patch 类型 | 干什么 | 出处 |
| --- | --- | --- | --- | --- |
| `TMP_Text.SetText` | `(string, bool)` | **Prefix** | 转换 + 记录 `instanceID → 原文` | `SetTextPatch.cs:7-17` |
| `TMP_Text.SetText` | `(string, float×1…8)` 共 8 个 | **Prefix** | 只转换（**不记录**，见 §4-③） | `SetTextFloat1…8Patch.cs:14-17` |
| `TMP_Text.text` | `get_text()` | **Postfix** | 读的时候**换回原文** | `TextGetterPatch.cs:9-12` |
| `TMP_Text.text` | `set_text(string)` | **Prefix** | 同 `SetText(string,bool)` | `TextSetterPatch.cs:9-12` |
| `TAnimCore.ConvertText`（Text Animator） | 动态 `AccessTools` | **Prefix** | 打字机效果前置转换，否则逐字动画拿到的是简中 | `TextAnimatorPatch.cs:15-48` |
| `LocHelper.Get` | `(string, string, object[])` | **Postfix** | **只补缺翻**，不改已有译文 | `MissingTranslations.cs:14-60` |
| `NewsUIElement.PopulateUI` | `(StoreEvent)` | **Postfix** | 每日新闻开头 5 个半角空格 → 全角缩进 | `NewsPatch.cs:46-106` |
| `PaperUIManager.OpenUI` | `(string,string,string,int)` | **Prefix + Postfix + Finalizer** | 导师笔记段首缩进 + 行距按"中/西文字体行高比之差"重算 | `MentorNotePatch.cs:46-110`、`:170-230` |

**没有一个 Patch 落在 `LocHelper.SetLocale` 上**（全目录 grep `SetLocale|SelectedLocaleChanged` **零命中**）。语言切换靠每帧比对 `LocalizationSettings.SelectedLocale.Identifier.Code.StartsWith("zh")` 自己判断（`ZhHantMod.cs:775-823`），中文才转换、英文原样放过。

### 2.2 简繁不是"翻译"，是**三级词表映射**

自实现 OpenCC s2tw 流程，**不是字符一一对应**：`_norm`（CJK 兼容表）先归一化异体（`OpenCC.cs:209-218`）→ `_phrases`（STPhrases，49,238 行）**最长匹配优先**、最多 63 字（`OpenCC.cs:91-106`、`65-77`）→ 词组没命中才退到 `_chars`（STCharacters，4,012 行）单字 → 最后做 `_twPhrases`/`_twChars`（台湾用词/字形）第二遍（`OpenCC.cs:197-206`）。**一词多义全靠第二层。**

一词多义的**原始数据**长这样（包内实文件）：

```
STCharacters.txt:   干	幹 乾 干 榦        ← 单字给 4 个候选
STPhrases.txt:      晾干→晾乾  莖干→莖幹  欄干→欄干   ← 词组定死
STCharacters.txt:   后	後 后
STPhrases.txt:      皇后→皇后  午後→午後
```

单字表取"空格前第一个"当兜底（`OpenCC.cs:36-37`），命中词组就整段替换。**所以"干"的歧义不是被猜出来的，是被 49,238 行词组的命中率压下去的**——词表没覆盖的组合就是错译，这也是它自带 `terms.tsv`（259 行术语校正）与 `overrides.tsv`（1,266 条逐条修正）的原因。

再往上还有第四层，专治"OpenCC 治不了的"：

| 表 | 行数 | 作用 |
| --- | --- | --- |
| `hardcoded.tsv` | 308 | **写死在代码里的英文**（不进字库的字符串）→ 正则替换成繁中 |
| `overrides.tsv` | 1,266 | 表/key + **自动转换结果的 SHA-256 前 16 码** + 位置片段修正 |
| `terms.tsv` | 259 | 台湾用词换词（"概率→機率"式），长词优先 |
| `fills.tsv` | 3 | 游戏简中**本身就缺翻**的条目补译文 |
| `args.tsv` / `indent.tsv` | 1 / 19 | 补占位符 / 导师笔记段落缩进 |
| `tables.txt` | 14 | 要读哪 14 张 `StringTable`（`ClientName`…`UI`） |

`overrides.tsv` 的哈希守卫是本档案最值得抄的一招：

```
Dialogue	dialog_georgio2_4	0e31a3fa42b54b5a	2	4	我們
```
载入时重算"自动转换结果"的哈希，**对不上就整条停用并报 `StaleOverrides`**（`ZhHantMod.cs:543-546`）——上游游戏改一句台词，位置型修正自动失效而不是错位改字。

### 2.3 「什么该转」的判据：从字库差集自动学，不硬编码简体字表

`_trigger[c] = 这个字在转换中会变  &&  这个字从没在任何译文里出现过`（`TextConverter.cs:433-440`）；
`_traditional[k] = 这是 OpenCC 输出字  &&  这个字从没在任何**原文**里出现过`（`TextConverter.cs:443-449`）；
于是 `NeedsConversion` 只看两件事：**含触发字 + 不含繁体专用字**（`TextConverter.cs:944-980`）。后者是**幂等闸门**——已经是繁中的文本（别的模组给的、玩家输入的）不会被二次转换。

`OpenCC.cs:141-147` 还有个隐蔽修正：单字 key（如"干"）虽然是输出字，也要从 `_outputs` 里**注销**，否则"干"会被误判成"已经是繁体"。

### 2.4 字体：两条腿都走，但一条自己带

| 路子 | 做法 | 出处 |
| --- | --- | --- |
| **A. CJK 全局回退** | 找 `NotoSansSC*` 的 `TMP_FontAsset`，`TMP_Settings.fallbackFontAssets.Add()` | `ZhHantMod.cs:580-606` |
| **B. 自带手写体** | `new Font(path)` + `TMP_FontAsset.CreateFontAsset(...)` 复制原字体的 pointSize/padding/图集尺寸，再替换 `FontLoader.simplifiedChineseHandwrittenFont` 与场景里所有同名字体的 `TMP_Text.font` | `ZhHantMod.cs:643-773` |

为什么必须带 B：游戏给"导师签名 / 兑换签名 / 导师纸条第 N 段"用的是**手写体**（原版 `MaShanZheng*`），繁体字形它没有；只加 A 会掉回黑体，签名就不像签名了。`data/handwritten.ttf` 是 **Yuji Syuku（OFL 1.1）的繁体改写版，官方声明用自身字形拼了 15 个繁体字**（`data/handwritten-font-LICENSE.txt`）。

### 2.5 和原版/其他翻译包共存

| 场景 | 结果 | 依据 |
| --- | --- | --- |
| 只装它 | 简中语言 + 繁中显示；切英文原样不转 | `ZhHantMod.cs:793-799` |
| 与 `3845556624` Locale Error Fix 同装 | **不崩、不错乱，但本模组的"缺翻补译"会静默失效**：对方 Prefix `return false` 把 `__result` 直接换成 key，本模组 Postfix 只认游戏自带的"缺翻消息模板"（`MissingMessage.From(NoTranslationFoundMessage)`，`MissingTranslations.cs:203-226`），认不出 key → 不补 | 两处源码对照 |
| 与 `3845895085` CJK Font Fallback 同装 | 各自往 `fallbackFontAssets` 追加，**幂等**（`Contains` 判重，`ZhHantMod.cs:599`） | — |
| 与另一个"显示层翻译"同装 | 会**串联转换**（A 转完 B 再转）；本模组的繁体闸门能挡掉大部分，但两个包都改同一句时仍以 `HarmonyPriority` 决定谁在外层 | （推测，未实测同装） |
| 存档 / 字库 / 原版文件 | **一个字节都不写**（除 `debug` 开关下的 `misses.tsv` 落在 ModData） | 全目录无 `ES3`、无 `SetEntry`、无 `SharedData` 写入 |

## 3. 关键代码摘录

**① 显示层转换 + 回读还原**（`SetTextPatch.cs:7-17` + `ZhHantMod.cs:275-302` + `ShownText.cs:21-53`）

```csharp
internal static void ConvertAndRecord(TMP_Text t, ref string s) {      // ZhHantMod.cs:275
    if (Conv == null) return;
    try {
        if (_preparedShown != null && (object)s == _preparedShown && t.GetInstanceID() == _preparedId) {
            string preparedOriginal = _preparedOriginal;   // 自己 PrepareNext 过的，别重复转换
            ClearPrepared();
            Shown.Record(t.GetInstanceID(), preparedOriginal, s);
        } else {
            string text = s;                               // 原文
            string s2 = text;
            MissingTranslations.RepairDisplay(ref s2);      // 先补画面上的缺翻
            s = Conv.Convert(s2);                          // 再简→繁
            Shown.Record(t.GetInstanceID(), text, s);      // 记 原文 ↔ 显示
        }
    } catch (Exception ex) { Error("轉換失敗：" + ex.Message); }
}

public string Restore(long id, string current) {                        // ShownText.cs:40
    if (current != null && _map.TryGetValue(id, out var v) && v.Item1 == current) { Restored++; return v.Item2; }
    return current;                            // 只有"当前显示值 == 我换上去的那个"才还原，上限 20000 条满了整表清空
}
```

**② 词表最长匹配（一词多义的全部机制）**（`OpenCC.cs:91-106`）

```csharp
public int Match(string s, int start, int end, out string value) {
    value = null;
    if (!_lengths.TryGetValue(s[start], out var value2)) return 0;   // 首字→"该首字所有词长"位图（ulong 的 bit n = 有 n 字词）
    for (int num = Math.Min(63, end - start); num >= 1; num--)
        if ((value2 & (ulong)(1L << num)) != 0L && _map.TryGetValue(s.Substring(start, num), out value))
            return num;                                              // 长词优先
    return 0;
}
```

**③ 幂等闸门：含简体专用字 且 不含繁体专用字 才转**（`TextConverter.cs:944-980` 摘要）

```csharp
public bool NeedsConversion(string s) {                  // TextConverter.cs:944
    bool flag = false, flag2 = false;
    for (int i = 0; i < s.Length; i++) {
        char c = s[i];
        if (_trigger[(uint)c]) return true;              // 命中简体专用字，立刻要转
        if (_traditional[(uint)c]) flag2 = true;         // 已经是繁体
        else if (!(flag | flag2) && _phraseStart[(uint)c] && i + 1 < s.Length) { /* 触发词逐条 CompareOrdinal */ }
    }
    if (flag) return !flag2;                              // 又像简又像繁 → 不动
    return false;
}
```

**④ 自制字体要"长得像"原字体，且必须把材质补回去**（`ZhHantMod.cs:740-773`）

```csharp
private static TMP_FontAsset CreateFontLike(string path, TMP_FontAsset orig) {   // ZhHantMod.cs:740
    Font val = new Font(path) { hideFlags = (HideFlags)32 };
    FaceInfo faceInfo = orig.faceInfo;                 // 尺寸全部抄原字体，否则字号/字距跳变
    TMP_FontAsset val2 = TMP_FontAsset.CreateFontAsset(val, faceInfo.pointSize, orig.atlasPadding,
        orig.atlasRenderMode, orig.atlasWidth, orig.atlasHeight, (AtlasPopulationMode)1, true);
    Material material = val2.material;
    Texture texture = material.GetTexture("_MainTex");   // 先抓住自己的 SDF 图集
    material.CopyPropertiesFromMaterial(orig.material);  // 抄原字体的 shader/渐变/描边参数
    material.SetTexture("_MainTex", texture);            // 再把图集换回来（不换回来 = 空白/糊字）
    material.SetFloat("_GradientScale", (float)(val2.atlasPadding + 1));
    return val2;
}
```

---

## 4. 踩坑与红线

**① 它对我们「`LocHelper.SetLocale` 是 ERROR 级热点」这条台账的意义：本模组不算占用。** 它只做 `LocHelper.Get` 的 **Postfix**（读侧、可叠加），从不改语言、不改字库。真正会打架的是 `3845556624` 那种 **Prefix `return false`、永不调原版** 的改法——两个"接管式"补丁同装才会让文案错乱。

**② 三个必须自己实现的细节，抄漏一个就出事故：**
- `get_text` 回读还原必须**同时比对"我换上去的值"**（`ShownText.cs:47`），只按 `instanceID` 还原会把其他模组后来写的值吃掉；
- 表项上限 `MaxEntries=20000` 满了是**整表清空**（`ShownText.cs:32-36`）——用 `Dictionary` 不做 LRU，换内存换简单，代价是偶发一次全量重转；
- 8 个 `(string, float…)` 重载**故意不记录**：签名里没有 `__instance`，拿不到 `instanceID`（`SetTextFloat1Patch.cs:14-17`）。这类是数值/单位标签，读回繁中不影响逻辑。

**③ SDF 材质必须"抄参数但保留自己图集"**，顺序错了就是空白字（§3-④）。另外它**没有**对已存在的 `TMP_Text` 调 `ForceMeshUpdate()`，而是 `val.text = text` 重设一遍触发重排（`ZhHantMod.cs:370-379`）——与 `03-原版接口速查` 的"加完 fallback 要 `ForceMeshUpdate`"是等效的两种写法。

**④ 行距不是常数，是"按字体行高比算"，但基线是硬编码断言**：`MentorNotePatch.cs:20` 写死 `Original = -6.24f`，只有当前值仍是 `-6.24±0.005` 才动手；新值 = `-6.24 + 100 * (Ratio(notoSans) − Ratio(body.font))`，`Ratio(f) = faceInfo.lineHeight * scale / pointSize`（`:172-215`、`:233-243`）。**算不出来就不改**（字体为 null / 中西文同一字体 / 行高相同，各有独立日志），**官方一改版面也自己退让关掉**——这是外围美化类模组该有的姿态。

**⑤ 语言判定不能缓存死**：`UpdateLocale()` 每 1 秒查一次 `SelectedLocale`（`ZhHantMod.cs:890-894`），换语言后对全场景 `TMP_Text` 做一次 `StaleAfterLocaleChange` 重转（`:825-850`）。启动早期**不读**任何文案（`EnsureData` 只读自己的 `data/*`），避开"`invalid operation handle`"那条老坑。

**⑥ 授权：代码 MIT、数据 Apache-2.0（OpenCC）、字库 OFL-1.1 且是改写版**——三份 LICENSE 都在包内（`LICENSE.txt` / `data/opencc/LICENSE` / `data/handwritten-font-LICENSE.txt`），改写说明写清了"用自身字形拼了哪 15 个繁体字"。**二次分发前必须逐条照抄这套声明**，尤其 OFL 的"改名 + 保留版权声明"要求。

---

## 5. 对我们的可用点

| 目标 | 可用点 | 落点 |
| --- | --- | --- |
| **《深空当铺：可能是群友》** | 改名/昵称/顾客称呼是**玩家与模组都能写的自由文本**，最怕被别的翻译包二次转换。直接复用 `ShownText` 那套"显示值 ↔ 原文 + 当前值相等才还原"的回读表，我们自己的文案也走一遍 `NeedsConversion` 风格的闸门（含简才转、含繁不转），保证**群友的繁中玩家看着不乱、玩家输入的名字永不被改** | 顾客称呼 / 名片 / 群聊消息 |
| **《深空电玩》** | 街机界面要显示分数、奖杯名、成就描述。抄 `Units` + `units` 换算与 `hardcoded.tsv` 的"写死英文用正则改"两条：**游戏里不进字库的英文串**（我们自己 `SetText` 的）不该指望本地化表，走一张小正则表最省事 | 街机分数板 / 奖杯弹窗 |
| **两个项目通用（工程）** | ①**"显示层转换 + 回读还原"是通用的文案注入姿势**：任何"想改玩家看到的字又不想改数据"的需求（黑话滤镜、审查替换、错别字修复）都能套；②`overrides.tsv` 的**位置修正 + 自动结果哈希守卫**可直接搬给我们的文案表——上游原文一变，修正自动失效而不是错位；③**自建 `TMP_FontAsset` 的材质补丁顺序**（§3-④）是给纸片/立绘文字换字体的唯一正确写法 | `深空电玩`、`深空当铺：可能是群友` 的文案层 |
| **字体资产路线** | 要给模组配 CJK 字体时照它的分工：**能靠全局 fallback（NotoSansSC）解决的一律不打包**，只有原版用了特定手写体/科幻体（`FontLoader.simplifiedChineseHandwrittenFont` 等 17 个角色）才自带字体，并**保留 OFL 原件与改写声明** | 纸片 / 报纸 / 单据排版 |
