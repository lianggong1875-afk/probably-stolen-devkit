# Probably Extend Framework · File Companion（框架本体/伴生模组拆分）｜工坊 3841912659

> 蒸馏自反编译源码 `缓存文件夹\decomp_ws\3841912659`（3 个 .cs / 913 行）。
> 价值：**"框架本体 + 需要文件权限的伴生模组"这种拆分方式**，以及一套**约定式本地图片来源**（丢文件夹就生效，不用写 C#）。

## 1. 为什么拆成两个模组

主框架 `Lune.ProbablyExtendFramework` 是零能力的；本模组 `Lune.ProbablyExtendFramework.Files` 需要 `files` 能力（读各模组目录 + 写自己的 ModData 目录）。

拆开的好处：
- 玩家只想用立绘/贴图扩展时可以直接装本体（零能力，`trust=Updated` 自动生效）；
- 需要"读任意文件"的那一半单独装、单独授权，官方在简介里明确写了"默认不需要启用"；
- 能力扫描时，需要 files 的部分不会连累主体。

**这是一种值得抄的发布架构**：把"必须吃权限"的能力切到 companion 里，主体保持零能力。

## 2. 它做什么：本地图片来源（约定式）

```
<ModData>\Lune.ProbablyExtendFramework\Local\      ← 玩家自己丢图
    lower/female/boss/a.png                       ← 每层文件夹名都成为标签：lower、female、boss
    Clara.png                                     ← 新立绘，ID = 文件名
    Clara_ep1.png                                 ← 同前缀更长名 → Clara 的差分 ep1
    Clara_ep1_accept.png                          ← 差分 ep1_accept
    femaleLower3.png                              ← 文件名叫原版立绘键 → 给那张立绘多一个来源
    storage_bay_red.png                           ← 道具 ID + 差分名
    femaleLower/xxx.png                           ← 文件夹名是来客池 → 该立绘带上池标签并参与抽取
```

作者在 `Local\说明.txt` 里把规则写成了玩家能读的中文（`LocalReadme` 常量），初始化时自动写出——**"自解释的数据目录"** 是个好习惯。

## 3. 关键实现点

| 关注点 | 做法 |
| --- | --- |
| 拿主框架位置 | `ModLoader.Mods?.Find(m => m?.Manifest.ID == "Lune.ProbablyExtendFramework.Files")?.FolderPath` |
| 本地目录 | `Path.Combine(ModHelper.GetModDataFolder("Lune.ProbablyExtendFramework"), "Local")`，不存在就 `Directory.CreateDirectory` |
| 扫描 | `Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)`，只认 `.png/.jpg/.jpeg` |
| 注入主框架 | `Painter.UseLocalLister(Func<IList<string[]>>)` —— **注入回调**，而不是让主框架去读文件（主框架保持零能力） |
| 热更新 | 比较文件大小/修改时间，变了就重画，**不要求改名** |
| 重扫 | 读档时自动扫一次；另有"重新扫描"按钮 |
| 立绘包装 | `PackShelf.Offer(text, IEnumerable<PackEntry>)` / `PackShelf.Announce(...)` / `PackShelf.LooksLikePortrait(stem)` |
| 桩模块 | 内部有一个 `FolderStore`（`ReadText/ReadBytes/WriteText/WriteBytes/Delete/List`）把"缓存目录读写"封成一个小类 |

## 4. 我们能用在哪

1. **"可能是群友"的立绘接入**：不必依赖 Portrait Studio 才能加立绘。我们可以照这套"Local 目录 + 约定命名 + 注入回调"给玩家提供**自助丢图**能力（我们主体保持零能力，读写文件的部分放到一个可选 companion 模组里）。
2. **我们的机器贴图/物品图标**同理：把"外部素材来源"设计成"目录 + 命名约定"，比让玩家改 JSON 友好得多。
3. **发布架构**：如果将来"深空电玩"要做"自定义街机卡带导入"（需要 files），也应该拆成 companion。

## 5. 局限与风险（诚实标注）

- 我**只做了结构化侦察**（grep 方法名 + 读 `LocalReadme` 常量与初始化段），没有完整通读 530 行的 `PacksEntry.cs` 与 371 行的 `DllResources.cs`；`DllResources` 具体怎么从 DLL 里抽资源、`Painter`/`PackShelf`（在主框架里，未反编译）的协议细节**未确认**。
- 该模组依赖主框架 `Probably Extend Framework`（工坊 id 3841908335 一类，本批未下载），所以只看到了 companion 侧。
- 它对文件时间戳的依赖意味着**网络盘的时钟漂移会导致重画判定异常**（推测）。

## 6. 待补

如果要照抄，需要先下载并蒸馏它的主框架（`Probably Extend Framework` 本体），确认 `Painter.UseLocalLister`、`PackShelf.Offer` 的确切契约。已记入第二批候选清单。
