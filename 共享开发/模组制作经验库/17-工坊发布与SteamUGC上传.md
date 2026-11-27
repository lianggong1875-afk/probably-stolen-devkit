# 17 · 工坊发布与 SteamUGC 上传

> 已验证：两款模组首次发布成功（《深空电玩》`3846263529`、《深空当铺 · 可能是群友》`3846263756`，2026-11-26，账号 凉宫千夏 `76561198227891812`）。
> 发布工具：[工具库/build/WorkshopUploader](../../工具库/build/WorkshopUploader/Program.cs) + [Publish-Workshop.ps1](../../工具库/build/Publish-Workshop.ps1)。

---

## 1. 铁律：发布必须在游戏外做

游戏内调用 `SteamUGC` 会被 `ModScanner` 判为越权，所以「模组里挂个上传按钮」这条路走不通。正确姿势是**独立进程**连接本机已登录的 Steam 会话：

- 身份来自 Steam 客户端当前登录用户，**不需要密码、cookie、API key**；
- 只能操作自己账号名下的物品；
- 发布记录留在各自 `PROGRESS.md` 的「工坊发布记录」表，序号 `r1`、`r2`……只增不减、不进工坊页面。

## 2. 环境三件套

| 需要的东西 | 实际位置（本机） |
| --- | --- |
| Steamworks.NET 托管绑定 | `Probably Stolen_Data\Managed\com.rlabrecque.steamworks.net.dll` |
| Steam 原生库 | `Probably Stolen_Data\Plugins\x86_64\steam_api64.dll` |
| dotnet SDK（编译独立工具） | `C:\Program Files\dotnet\sdk\10.0.404` |

**坑 1：`steam_api64.dll` 必须复制到可执行文件所在目录。** 绑定里是 `[DllImport("steam_api64")]`，走默认搜索路径，只放在游戏 `Plugins\` 下解析不到。不需要写 `NativeLibrary.SetDllImportResolver`。

**坑 2：先设 AppId 环境变量再 `Init`。** 否则 `steam_api64` 不知道要连哪个 app：
```csharp
Environment.SetEnvironmentVariable("SteamAppId", "4348910");
Environment.SetEnvironmentVariable("SteamGameId", "4348910");
```

**坑 3：net10 引用 Unity Mono 编译的 Steamworks.NET 没问题**，纯托管代码，`PlatformTarget=x64` 即可编译运行。

## 3. 异步回调必须 RunCallbacks + KeepAlive

`CreateItem` / `SubmitItemUpdate` 都返回 `SteamAPICall_t`，结果通过 `CallResult<T>.Set()` 异步送回来。两个硬要求：

```csharp
var call = CallResult<CreateItemResult_t>.Create((r, io) => { done = true; /* 取 r.m_eResult */ });
call.Set(handle);
while (!done && sw.ElapsedMilliseconds < 120000) { SteamAPI.RunCallbacks(); Thread.Sleep(50); }
GC.KeepAlive(call);   // 不写这句，回调委托可能在回调前被 GC 回收，表现为「永远等不到」
```

- 只 `Set` 不 `RunCallbacks` → 回调永远不来；
- `CallResult` 用完即弃（局部变量）→ 可能被回收，必须 `GC.KeepAlive`。

## 4. 可见性枚举顺序（别记错）

```csharp
ERemoteStoragePublishedFileVisibility
  0 = Public（公开）
  1 = FriendsOnly（仅好友）
  2 = Private（私有）
  3 = Unlisted（未列出）
```

**新建物品默认建议先 `2` 私有**跑通，确认页面无误再 `0` 公开；改可见性不需要重传内容，`--no-content` 式只改属性即可（`StartItemUpdate` → `SetItemVisibility` → `SubmitItemUpdate`）。

## 5. 上传成功 ≠ 内容正确：用 fileSize 交叉核对

提交回调 `k_EResultOK` 只说明 Steam 收下了。做一次服务端查询，把 `SteamUGCDetails_t.m_nFileSize` 和本地包体积对比：

```csharp
var ids = new[]{ new PublishedFileId_t(fileId) };
UGCQueryHandle_t q = SteamUGC.CreateQueryUGCDetailsRequest(ids, 1);
SteamUGC.SetReturnLongDescription(q, true);
CallResult<SteamUGCQueryCompleted_t>.Create(...);  // 里层 GetQueryUGCResult + GetQueryUGCPreviewURL
SteamUGC.ReleaseQueryUGCRequest(handle);           // 别忘了释放
```

实测两款包本地/服务端体积一致（5.6 MB ↔ `5911154`、9.2 MB ↔ `9628192`），`banned=False`、`visibility=Public`、`owner` 是本人，才算发出去。

**坑 4：刚发布的物品网页打不开是正常的。** 立刻访问 `sharedfiles/filedetails/?id=...` 可能 HTTP 500，`RequestUGCDetails` 也可能返回 `result=9`（FileNotFound）。等 10 分钟；**以 `SteamUGC` 查询结果为准，别当失败重传**（重传会触发限流）。

## 6. 标题 / 描述 / 语言三处会打架

- 工坊标题 = `manifest.xml` 的 `<Name>`，每次完整提交都会覆盖。想在网页改标题必须同步改 manifest，否则下次重传被打回去。
- 描述按语言分开存：`schinese` 和默认语言是两份。中文界面只跑 `--lang schinese` 的话，默认语言那份还是旧的。
- **只改简介时必须带 `--lang schinese`。** 不传 `--lang` 改的是「默认语言」那一份，中文界面的玩家看到的是旧的（实测：改完再查 `desc length` 还是旧长度，加 `--lang schinese` 重传才生效）。两份都要新的话，先不带语言传一次、再带 `schinese` 传一次。
- **`SetItemUpdateLanguage` + 不给描述 = 把那个语言的简介清空。** 这条最阴：只改内容/更新说明时带了 `--lang schinese` 却没带 `--desc-file`，提交后中文简介直接变空（实测 `desc length` 从 326 变 0）。工具侧铁律「只有真要改描述时才设语言」，见 `WorkshopUploader/Program.cs` 里那段判断。
- **工坊简介是作者在网页上手写的东西，别拿本地文件去覆盖它。** `SetItemDescription` 一调就顶掉，Steam 不保留描述的历史版本，本地也没备份的话就永久丢失（实测踩过：用本地生成的简介文本上传，把作者手写的简介覆盖了两次，找不回来）。发布脚本默认必须**不**碰简介，要改时显式开关。
- 更新说明（change note）写清「会不会影响旧档」，比功能列表更重要。
- **只改更新说明、不重传内容**：`StartItemUpdate` 后不调 `SetItemContent` 即可（`WorkshopUploader --id X --note "..."` 不带 `--content`）。Steam 会保留原内容，只更新 note。

## 7. 发布包洁净度靠自觉

`SetItemContent(目录)` 会**整目录上传，无扩展名过滤**。发布前跑一遍白名单校验（已内置在 `New-ReleasePackage.ps1`）：

- 允许：`.dll` `.xml` `.png` `.json` `.txt` `.md` `.wav`
- 禁止：`.cs` `.ps1` `.bak` `.zip` `.exe` `.bat`、旧版本 DLL、反编译输出、缓存
- 封面 `preview.png`：**1280×720（16:9）且 < 1 MB**，否则 Steam 卡片会黑边填充或直接拒绝

`<Capabilities />` 用自闭合写法：清空子节点后要设 `$caps.IsEmpty = $true`，否则 `XmlDocument.Save` 会输出 `<Capabilities></Capabilities>` 这种带换行的开闭对。

## 8. 一键发布

```powershell
# 新建物品（先打包再上传，公开）
.\工具库\build\Publish-Workshop.ps1 -Mod Arcade -Visibility 0
# 更新已有物品（复用同一个 PublishedFileId）
.\工具库\build\Publish-Workshop.ps1 -Mod Freya -Id 3846263756 -Note "v0.7.1 ..."
# 只查服务端详情
.\工具库\build\Publish-Workshop.ps1 -Mod Arcade -VerifyOnly -Id 3846263529
# 只验 Steam 连通与账号
dotnet <bin>\WorkshopUploader.dll --dry-run
```

`--dry-run` 输出形如 `[init] persona=... | steamId=... | appId=4348910 | ownsApp=True`，能跑出这行就说明 Steam 会话、app 归属、原生库解析都对了。
