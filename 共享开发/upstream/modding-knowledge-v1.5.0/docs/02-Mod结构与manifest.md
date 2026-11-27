# 02 · Mod 结构与 manifest

> 一个模组能被游戏识别，只需要两样东西：`manifest.xml` 和一个**实现了 `IMod` 的 DLL**。
> 证据等级：解析规则为 `[静态]`（读 `ModLoader.ParseManifestCore` 反编译），加载行为为 `[实测]`。

---

## 1. 最小目录

```
Mods/
└─ MyMod/
   ├─ manifest.xml        ← 必须
   ├─ MyMod.dll           ← 必须：至少含一个 IMod 实现
   ├─ preview.png         ← 工坊缩略图（1280×720），游戏不读
   ├─ Textures/           ← 可选：物品贴图
   │  └─ Items/
   └─ README.md           ← 可选：给自己和玩家看
```

游戏只认 `Mods\<任意文件夹名>\manifest.xml`。**文件夹名不影响模组 ID**，ID 由 manifest 决定；如果两个目录声明同一个 ID，第一个胜出（本地目录优先于工坊副本）。

---

## 2. manifest.xml

```xml
<?xml version="1.0" encoding="utf-8"?>
<Manifest>
  <ID>my-mod-id</ID>
  <Name>我的模组（工坊标题也用这个）</Name>
  <Author>你的名字</Author>
  <ModVersion>1.0.0</ModVersion>
  <Description>一句话说明玩法，会显示在模组列表里。</Description>
  <GameVersions>
  </GameVersions>
  <Prerequisites>
  </Prerequisites>
  <Assemblies>
    <Assembly>MyMod.dll</Assembly>
  </Assemblies>
  <Capabilities>
  </Capabilities>
</Manifest>
```

### 字段规则

| 字段 | 必须 | 说明 |
|---|---|---|
| `ID` | ✅ | 只允许字母、数字、`_` `-` `.`，不能以 `.` 开头，≤64 字符。**决定启用状态、信任指纹、上传器匹配**，发布后不要改。 |
| `Name` | ✅ | 显示名。**上传器会用它覆盖工坊标题**，所以工坊标题必须与它一致，否则下次重传会被改回去。 |
| `Author` | 建议 | 显示用。 |
| `ModVersion` | 建议 | 语义化版本；发布流程要 bump。 |
| `Description` | 建议 | 模组列表里的简介。工坊页面描述是另一份（见 06），两者独立。 |
| `GameVersions` | 可空 | 每项 `<Version>`；不填表示不限制。 |
| `Prerequisites` | 可空 | 每项 `<Mod>`，值填别的模组 ID；未满足时会校验失败。 |
| `Assemblies` | ✅ | **至少一个** `<Assembly>`。相对路径、必须在模组文件夹内、必须以 `.dll` 结尾。 |
| `Capabilities` | ✅ | **元素必须存在**，没有就写空标签 `<Capabilities/>`，否则整份 manifest 解析失败。 |

### 两个反直觉的点（都踩过）

1. **`<Capabilities>` 不能省。** 反编译原文：没这个元素 → `has no <Capabilities> element (write <Capabilities/> when the mod needs none)`，整个模组不加载。
2. **上传器硬校验根节点名是 `<Manifest>`。** 游戏自己的解析器只看 `root.Element("ID")`，不看根节点名；但创意工坊上传器的 `ManifestReader` 会把根节点写成 `<Mod>` 的包直接拒掉（"根节点应为 &lt;Manifest&gt;"）。**统一写 `<Manifest>`，两边都过。**

---

## 3. IMod：模组的唯一入口

```csharp
public interface IMod
{
    void Init(ModManifest manifest);
    void OnEnable();
    void OnDisable();
}
```

```csharp
using HarmonyLib;
using UnityEngine;

public sealed class MyMod : IMod
{
    public void Init(ModManifest manifest)
    {
        Debug.Log("[MyMod] Init " + manifest.ModVersion);
        // 这里做「静态注册」：订阅 ModHook、注册物品工厂、打补丁
    }

    public void OnEnable() { /* 玩家在菜单里启用时（通常伴随重启） */ }

    public void OnDisable() { /* 停用：反订阅、撤补丁（只撤自己的） */ }
}
```

### 生命周期要点

- **`Init` 在游戏启动早期执行**，早于读档。此时 `PlayerStore.Instance` **可能还是 null**。
  - 不能在 `Init` 里访问 `PlayerStore.Instance`、`NetworkUpgrade.RegisterModUpgrade` 等依赖场景对象的东西（见坑 #18）。
  - 需要游戏内对象时，订阅 `ModHook.OnGameLoadedNormal` 或 `OnNewGameStarted`，并用静态 bool 防重复。
- 物品注册通常挂在 `ModHook.OnModItemDirectoryInit` 里（拿 `ModItemDirectory` 调 `Set`），不要放 `Init`。
- `OnDisable` 要成对清理：`+=` 过的钩子 `-=`，打过补丁的 `Unpatch`（只限自己的 Harmony ID）。

---

## 4. 加载与信任机制（会影响你的发布流程）

游戏启动时：

1. 扫 `Mods\` 下每个目录，解析 manifest。
2. 校验：ID 合法、Assemblies 非空且在目录内、文件存在、**至少一个 DLL 含 `IMod` 类型**（按字节找 `IMod\0` 字符串）。
3. 计算**文件指纹**（目录内文件内容哈希）。
4. 与本地信任记录比对：
   - `Unknown`：第一次见 → 不加载，等玩家在 Mods 菜单点「启用/信任」。
   - `Match`：文件完全没变 → 加载。
   - `Updated`：版本变了，但**没有申请新的能力** → 自动加载。
   - `Changed`：来源变了 / 指纹对不上 / 扫描被拒 / 申请了新能力 → 不加载，要玩家重新信任。
   - `DeveloperMode`：本地模组的开发者模式，会跳过扫描拒绝直接加载（**不能代替正常验收**）。
5. 新模组默认**禁用**（`modOrder` 里没有）。

对作者的实际含义：

- 发布更新时**只要不新增 Capabilities**，老玩家不需要重新点信任。
- 改了 `ID` 等于换了一个模组，玩家要重新信任；改了 `Name` 会改变指纹。
- 想确认某个版本到底加载了没有：看 `Player.log` 的 `[ModLoader] audit:` 行（`trust=Match` 才正常）。

---

## 5. 模组目录里能放什么

**只放**：`manifest.xml`、`*.dll`（且必须全部写进 `<Assemblies>`）、`preview.png`、`Textures/`、`Sounds/`、纯文本（`.md` / `.txt` / `.json` / `.csv`）。

**绝对不要放**：`.bak`、`.orig`、`.zip`、`.ps1`、`.exe`、`.bat`、源码 `.cs`、旧版本 DLL。

原因：扫描器会把 manifest 没列出的可执行文件判成 **`payload-file`**，拒绝整个模组，日志只有一行 `Changed: scan refused (payload-file: ...)`，而游戏里的表现是「所有功能凭空消失」。详见坑 #16 与 [07-安全边界](07-安全边界.md)。

---

## 6. 贴图与资源

- 物品贴图放 `Mods\<模组>\Textures\Items\<名字>.png`，代码里用 `SetSpriteAndShapeFromMod("<manifest ID>", "<名字>")`。
- 形状不要依赖贴图 alpha 自动推断：代码里显式 `item.SetShape(new GridShapeBuilder(w, h).SetDataFill(w, h, 1))`。实测 2×2 的图不显式设形状时会只占 1×2（见 [05-代码配方](05-代码配方.md) 第 5 节）。
- 音效放 `Sounds/`，按官方 README 的命名约定加载。

---

## 7. 一个能直接抄的空壳

```csharp
using UnityEngine;

public sealed class EmptyMod : IMod
{
    public void Init(ModManifest manifest)
    {
        Debug.Log("[EmptyMod] loaded " + manifest.ModVersion);
    }
    public void OnEnable() { }
    public void OnDisable() { }
}
```

本知识库自己的 DLL 就是这样——证明「加载成功」但不改变任何东西。
