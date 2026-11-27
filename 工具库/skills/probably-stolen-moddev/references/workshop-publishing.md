# 发布到 Steam 创意工坊（Probably Stolen，appid 4348910）

> 已在 2026-11-26 实测发布两款模组：《深空电玩》`3846263529`、《深空当铺 · 可能是群友》`3846263756`。
> 完整版见工作区 `共享开发/模组制作经验库/17-工坊发布与SteamUGC上传.md`，可复用工具见 `工具库/build/WorkshopUploader` 与 `Publish-Workshop.ps1`。

## 1. 只能在游戏外上传

游戏内调用 `SteamUGC` 会被 `ModScanner` 拒绝。用**独立进程**连接本机已登录的 Steam 会话，身份自动是当前用户，不需要密码 / cookie / API key，也只能改自己名下的物品。

## 2. 环境三件套

| 需要 | 位置 |
| --- | --- |
| Steamworks.NET 托管绑定 | `<Game>_Data\Managed\com.rlabrecque.steamworks.net.dll` |
| Steam 原生库 | `<Game>_Data\Plugins\x86_64\steam_api64.dll` |
| dotnet SDK | 编译抓 Steam 的助手程序 |

- **`steam_api64.dll` 必须复制到助手程序输出目录**——绑定用 `[DllImport("steam_api64")]` 走默认搜索路径，留在 `Plugins\` 下解析不到。
- 先设 `Environment.SetEnvironmentVariable("SteamAppId","4348910")` 与 `SteamGameId`，再 `SteamAPI.Init()` / `InitEx()`。
- net10 引用 Unity Mono 编译的 Steamworks.NET 可行，`PlatformTarget=x64`。

## 3. 异步回调两条硬要求

```csharp
var call = CallResult<CreateItemResult_t>.Create((r, io) => { /* m_eResult / m_nPublishedFileId */ });
call.Set(SteamUGC.CreateItem(appId, EWorkshopFileType.k_EWorkshopFileTypeCommunity));
while (!done && 未超时) { SteamAPI.RunCallbacks(); Thread.Sleep(50); }
GC.KeepAlive(call);   // 少了这句回调可能等不到
```

`StartItemUpdate` 之后逐个 `SetItemTitle/Description/UpdateLanguage/Content/Preview/Visibility`，再 `SubmitItemUpdate`，同样等 `SubmitItemUpdateResult_t`；进度用 `GetItemUpdateProgress`。

## 4. 可见性枚举

`0=Public`、`1=FriendsOnly`、`2=Private`、`3=Unlisted`。
新建物品建议先 `2` 私有验页面，再改 `0` 公开——改可见性不用重传内容。

## 5. 上传成功要交叉核对

`k_EResultOK` 只代表服务端收下。用 `CreateQueryUGCDetailsRequest` + `SendQueryUGCRequest` 查回 `SteamUGCDetails_t`，核对 `m_nFileSize` 与本地包体积、`m_bBanned`、`m_eVisibility`、`m_ulSteamIDOwner`。

刚发布的物品网页可能 HTTP 500、`RequestUGCDetails` 可能返回 `result=9`，**等 10 分钟**，以 `SteamUGC` 查询为准，别重传（会限流）。

## 6. 发布包洁净度

`SetItemContent(目录)` 整目录上传、无扩展名过滤。白名单：`.dll .xml .png .json .txt .md .wav`；禁 `.cs .ps1 .bak .zip .exe` 与旧 DLL。封面 `preview.png` 必须 **1280×720 且 < 1 MB**。

`<Capabilities />` 要自闭合：清空子节点后设 `$caps.IsEmpty = $true`，否则 `XmlDocument.Save` 会写成带换行的开闭对。

## 7. 标题会被覆盖

工坊标题 = `manifest.xml` 的 `<Name>`，每次完整提交都会覆盖。改网页标题必须同步改 manifest。描述按语言分开存，中文界面只跑 `--lang schinese` 时默认语言那份不会更新。
