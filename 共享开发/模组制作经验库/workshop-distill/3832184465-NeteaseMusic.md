# 深空音乐：可能是抑云 / DangoMusic｜工坊 3832184465

> 工坊 id `3832184465`｜程序集 `DangoMusic.dll`（169,472 B）｜manifest `zhinian-music-dev` v1.0.0｜`<Capabilities>` = **`files` + `network`**
> 反编译源：`缓存文件夹\decomp_ws\3832184465`（29 个 `.cs` / 11,889 行，ILSpy 输出，**变量名由反编译器生成**；行号相对该目录）
> 为什么值得读：3 万订阅的**纯外围**模组——不碰存档、不碰物品，用 6 个 Prefix 把原版磁带播放器整个换成网络音乐播放器；同时它是本工作区里少见的**"合法带 `files`/`network` 的成品"**，能力边界、IMGUI 自绘 UI、网络音频流三件事都能当参照

---

## 0. 一句话结论（先回答最要紧的问题）

**它是内置播放器，不拉起任何外部程序或网页。** 全目录 grep `Process.|OpenURL|Application.Open|ShellExecute|explorer|steam://` **零命中**。界面是自己用 **IMGUI（`OnGUI` + 166 处 `GUI.*`）** 画的 4,139 行自绘窗口，音频是 **`UnityWebRequestMultimedia.GetAudioClip(url, AudioType.MPEG)` 边下边播进一个私有 `AudioSource`**。

**能力**：manifest 明写 `files` + `network`，与源码完全对得上——`files` 用于 cookie/账号 JSON 落盘（`NativeAccounts.cs:118-120`）与读自己的 `Images/*.png`；`network` 用于 `HttpWebRequest`（`MultiHttp.cs:41-67`）与 `UnityWebRequest`。**它一个音频文件都没带**（见 §1.1），所以 3 万订阅卖的是"通道"，不是"曲库"。

---

## 1. 规模与结构

### 1.1 三分账（包 1,257,364 B，33 个文件）

| 类 | 内容 | 体量 | 占比 |
| --- | --- | --- | --- |
| **代码** | 29 个 `.cs` / 11,889 行 → `DangoMusic.dll` | 169,472 B | 13.5 % |
| **数据** | **0**（无音频、无曲库、无字幕；登录态运行时才生成 `netease_cookie.txt` / `accounts/*.json`） | 0 B | 0 % |
| **资源** | `Images/*.png` 11 张按钮贴图 + `preview.png` | 715,997 B | 56.9 % |
| （附）作者把**源码 `src/` 一起发了** | `.cs` + `.csproj` + `Directory.Build.props` | 329,607 B | 26.2 % |

**与 ①（9.3 MB）的差别就是版权边界的教科书**：① 的 86 % 是**可再分发**的 OFL 字体；② 的音频体积是 **0**，一首歌都不进包——运行时按**玩家自己的账号权限**向平台取流，`enhance/player/url` 拿不到就退免费外链，再不行就"试听片段"（`NeteaseApi.cs:930-951`、`:983`）。

### 1.2 11,889 行的分布（这是"UI 9 / 平台 1"的极端案例）

| 域 | 文件 | 行数 | 占比 |
| --- | --- | --- | --- |
| **UI（歌单/播放器/登录/歌词滚动/进度条/迷你模式）** | `MusicUi.cs` **4,139** | 4,139 | **34.8 %** |
| **平台适配（4 家：网易云/QQ/酷狗/Spotify）** | `NeteaseApi` 1,558、`QqLogin` 1,719、`NeteaseCrypto` 111、`QqApi` 928、`KugouApi` 938、`KugouLogin` 699、`SpotifyApi` 220、`MultiApi` 125、`MultiHttp` 146、`MusicSources` 50、`NativeAccount(s)` 218 | 6,712 | **56.5 %** |
| **播放器内核** | `MusicPlayer` 550、`RuntimeDriver` 75、`LyricData` 29、`NeteaseTrack` 45、`SongUrl` 14、`NeteasePlaylist` 18、`LyricLine` 10、`RepeatMode` 8 | 749 | 6.3 % |
| **接线** | 6 个 `HookCassette*.cs` 172 + `DangoMusicMod` 105 + `AssemblyInfo` 12 | 289 | 2.4 % |

**"音频解码"占 0 行**——解码全交给 Unity 的 `DownloadHandlerAudioClip`（FMOD），模组只写"要哪条 URL、什么时候播、进度怎么同步"。**真正的成本在"四家平台的登录态与会话"（`QqLogin`+`KugouLogin` 合计 2,418 行）**，这一条对我们最有警示意义。

## 2. 它怎么做这件事

### 2.1 接原版磁带播放器：6 个靶点，全部 `Prefix` 短路

原版 `MachineCassettePlayer`（`共享开发/reference/Assembly-CSharp/MachineCassettePlayer.cs`，361 行）是**静态工具类**：按 `CASETTE_PLAYER_STATE_INT`（0 停 / 1 播 / 2 暂停）走 `AudioManager.Instance.PlayMusic(trackId)`，`trackId` 来自卡带物品的 `CASSETTE_CONTENT` 标签。

| 原版方法 | Patch | 行为 | 出处 |
| --- | --- | --- | --- |
| `OnPlayButtonPressed(GameItem, GameGridInventory)` | **Prefix → false** | 没播过 → `MusicUi.Open()`；有歌或界面开着 → `MusicPlayer.PlayPause()` | `HookCassettePlay.cs:5-30` |
| `OnStopButtonPressed(GameItem)` | **Prefix → false** | `Stop()` + `MusicUi.Close()` | `HookCassetteStop.cs:5-27` |
| `OnPreviousButtonPressed` / `OnNextButtonPressed` | **Prefix → false** | `Prev()` / `Next()`（`PositionSec>5` 先归零再退上一首） | `HookCassetteNext.cs:5-23`、`HookCassettePrev.cs:5-23` |
| `ChangeVolumeClick(MachineryText, bool)` | **Prefix → false** | 每次 ±0.08（原版 ±0.05，且改的是 `EmporiumOption.musicVolume`） | `HookCassetteVolume.cs:8-23` |
| `CreateMachineInventoryWindow(...)` | **Postfix（改 `__result`）** | 往窗口的 `MachineryPanelRow` **插第 5 个面板**「云音乐」`MachineryButton` | `HookCassetteUiButton.cs:6-40` |

**Panel 插入是这个模组最漂亮的一段**（不复制窗口、不改原版布局，只加一个按钮）：

```csharp
var (val, _, _, _, _, _, _) = __result;                       // HookCassetteUiButton.cs:21
PixelElement childElement = val.childElement;
MachineryPanelRow val2 = (MachineryPanelRow)((childElement is MachineryPanelRow) ? childElement : null);
if (val2 != null && val2.GetPanel("cloudmusic") == null) {    // 幂等闸门：重开窗口不叠按钮
    MachineryButton val3 = new MachineryButton("云音乐", () => MusicUi.Open(), -1, -1);
    val2.Add(new MachineryPanel((string)null, (PixelElement)val3, "cloudmusic"));
}
```

`MachineryPanelRow.Add(MachineryPanel)` / `GetPanel(string role)` 都是原版公开方法（`MachineryPanelRow.cs:114`、`:163`），`MachineryButton(string text, Action callback, int w, int h)` 也是公开构造（`MachineryButton.cs:43`）——**零反射、零 Transpiler**。

### 2.2 UI：IMGUI 自绘，一套皮肤 + 一只 OS 字体

- 只有一个 MonoBehaviour（`RuntimeDriver` 挂在 `DontDestroyOnLoad` 的 `GameObject` 上），`OnGUI()` 里 `MusicUi.Draw()`（`RuntimeDriver.cs:71-74`）；
- 字体**不走 TMP**：`Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","Microsoft YaHei UI","SimHei","Noto Sans CJK SC","Arial"}, 16)`，全失败才退 `Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")`（`MusicUi.cs:1619-1622`）——**这正好绕开了 TMP 字形回退/字体图集那道坎**；
- 皮肤每帧体检：`_font`/`_skin` 为 null、或 `S("playIcon").normal.background == null` 就清缓存重建（`MusicUi.cs:1565-1604`）——IMGUI 运行期生成的 `Texture2D` 会被场景切换/热重载打掉，这是必须的**自愈**。

### 2.3 音频：网络流 → `AudioClip` → 私有 `AudioSource`

| 环节 | 做法 | 出处 |
| --- | --- | --- |
| 取播放地址 | `MultiApi.SongUrl` 三级回退：`enhance/player/url`（带登录 cookie）→ 同曲替代版本 → 免费外链 `song/media/outer/url?id=…mp3` | `NeteaseApi.cs:930-951` |
| 下载+解码 | `UnityWebRequestMultimedia.GetAudioClip(url, (AudioType)13)`，`timeout=30`。反编译看到的是裸数字 13，**作者随包发的源码里写的是 `AudioType.MPEG`**（`src/MusicPlayer.cs:238`），可据此确认 13 = MPEG | `MusicPlayer.cs:324-389` |
| 播放 | `RuntimeDriver.Awake()` 里 `gameObject.AddComponent<AudioSource>()`，`spatialBlend=0`（纯 2D） | `RuntimeDriver.cs:14-22` |
| 防串台 | **`_loadGen` 代际计数**：切歌即 `++`，回调里 `gen != _loadGen` 一律丢弃并 `Destroy(clip)` | `MusicPlayer.cs:326-357` |
| 播完检测 | 不用 `isPlaying` 直接判，而是 `PositionSec >= length - 0.35`（平台差异兜底） | `MusicPlayer.cs:495-506` |
| 线程 | 网络在 `ThreadPool`（`RunAsync`），回主线程一律经 `ConcurrentQueue<Action>`（`RunOnMain`） | `RuntimeDriver.cs:24-42` |

### 2.4 平台适配：四家、两套加密、三种登录

`MusicSources.cs:8-14` 写死 `Netease=0 / QQ=1 / Kugou=2 / Spotify=3`，`MultiApi` 每个方法都是同构四分支（`:5-60`）。网易云走 **WEAPI**：两次 AES-128-CBC（`0CoJUm6Qyw8W8jud` + 随机 16 位）再用 `System.Numerics.BigInteger.ModPow` 做 RSA（`NeteaseCrypto.cs:14-45`）——**这是 `System.Numerics.dll` 与 `System.Security.Cryptography` 都要引的原因**。扫码登录靠 `api.qrserver.com` 生成二维码 PNG（`MusicUi.cs:3681`），QQ 的扫码接口已失效 → 退化为"手工粘 cookie"（`MusicUi.cs:3871`、`:3274`）。

### 2.5 能力边界：申报与用法严格一致，但有两处"灰"

| 能力 | 真实用途 | 判定 |
| --- | --- | --- |
| `files` | `Path.GetDirectoryName(Assembly.Location)` 定 mod 目录（`DangoMusicMod.cs:21`）、`netease_cookie.txt`、`accounts/<source>.json`、`login_debug.txt` 崩溃日志 | 合理但**全走 `System.IO.File`，不是 `ModHelper.*ModDataFile*` 白名单 API**——我们照抄就会过不了零能力扫描 |
| `network` | `HttpWebRequest`（含 `Proxy=null`、伪 UA、Referer 伪装）+ `UnityWebRequest` | 与申报一致 |
| 无 `reflection` | 6 个 Patch 全部 `typeof(MachineCassettePlayer)` 强类型 + 公开构造 | **干净**，值得抄 |

---

## 3. 关键代码摘录

**① 传输层：`HttpWebRequest` 全 sync、失败一律返回 null**（`MultiHttp.cs:37-67`）

```csharp
public static string Get(string url, string referer) {
    try {
        HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(url);
        httpWebRequest.Method = "GET";
        httpWebRequest.Timeout = 15000;
        httpWebRequest.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) ... Chrome/124.0 Safari/537.36";
        httpWebRequest.Accept = "application/json, text/plain, */*";
        httpWebRequest.Proxy = null;                 // 显式绕系统代理
        httpWebRequest.KeepAlive = false;
        if (!string.IsNullOrEmpty(referer)) httpWebRequest.Referer = referer;
        using HttpWebResponse httpWebResponse = (HttpWebResponse)httpWebRequest.GetResponse();
        ... 读流 → MemoryStream → Encoding.UTF8.GetString(...)
    } catch { return null; }                         // ← 全静默，见 §4-③
}
```

**② 播放内核：代际计数 + 只下整段**（`MusicPlayer.cs:324-389` 摘要）

```csharp
private static IEnumerator LoadAudioRoutine(string url) {
    int gen = _loadGen;                                        // 起手记代
    UnityWebRequest uwr = UnityWebRequestMultimedia.GetAudioClip(url, (AudioType)13);  // 13 = MPEG（源码 AudioType.MPEG）
    try {
        uwr.timeout = 30;
        yield return uwr.SendWebRequest();
        if (gen != _loadGen) yield break;                      // 期间切歌了 → 丢弃
        if ((int)uwr.result != 1) { IsLoading = false; StatusText = "播放失败：" + uwr.error; TryAutoSkip(); yield break; }
        AudioClip val2 = ((DownloadHandlerAudioClip)uwr.downloadHandler).audioClip;
        if ((Object)(object)val2 == (Object)null) { StatusText = "音频解码失败"; TryAutoSkip(); yield break; }
        _source.Stop();
        if ((Object)(object)_source.clip != (Object)null) Object.Destroy(_source.clip);   // 旧 clip 必须手动销毁
        _source.clip = val2;
        _source.time = (float)Math.Min(PositionSec, Math.Max(0.0, (double)val2.length - 0.1));
        _source.Play();
    } finally { ((IDisposable)uwr)?.Dispose(); }
}
```

**③ 主线程泵：网络/解密全在后台，UI 只在主线程碰**（`RuntimeDriver.cs:24-64` 摘要）

```csharp
private readonly ConcurrentQueue<Action> _main = new ConcurrentQueue<Action>();
internal void RunAsync(Action a) => ThreadPool.QueueUserWorkItem(_ => { try { a(); } catch (Exception ex) { Debug.LogException(ex); } });
internal void RunOnMain(Action a) => _main.Enqueue(a);
private void Update() {
    while (_main.TryDequeue(out var result)) { try { result(); } catch (Exception ex) { Debug.LogException(ex); } }
    MusicPlayer.Tick();
    if (MusicUi.IsOpen && Input.GetKeyDown((KeyCode)27)) MusicUi.Close();   // Esc 关窗，音乐继续
}
```

**④ 三级取流回退**（`NeteaseApi.cs:930-951` 摘要）

```csharp
public static SongUrl SongUrl(long id) {
    SongUrl songUrl = PlayerUrl(id);                       // ① 带 cookie 走账号权限（br=192000）
    if (songUrl != null) return songUrl;
    long num = AlternativeVersion(id);                      // ② 原曲下架 → 找同曲替代版本
    if (num > 0 && (songUrl = PlayerUrl(num)) != null) { songUrl.Replaced = true; return songUrl; }
    return new SongUrl { Url = "https://music.163.com/song/media/outer/url?id=" + id + ".mp3" };  // ③ 免费外链
}
```

---

## 4. 踩坑与红线

**① 六个 Prefix 全 `return false`，"原版状态机"整体被旁路——这是它最大的功能缺口，也是我们最不该照抄的地方。**
- 原版 `OnPlayButtonPressed` 会写 `CASETTE_PLAYER_STATE_INT` / `CURRENT_PLAYING_TRACK_ID_TAG` 并调 `RefreshPlayingFrames`（`MachineCassettePlayer.cs:143-171`、`:214-227`）。Prefix 短路后这些全不执行 → **卡带机播放时动画帧不动、存档里状态仍是"停止"、重进游戏不会续播**（推测：未真机复现，但代码路径是确定的）。
- 原版 `PauseForGameOver()`（`:182-192`）只 Pause 原版 `AudioManager`；模组的 `AudioSource` 不在其内 → **游戏结束时音乐继续响**。
- 原版音量改的是 `EmporiumOption.Instance.musicVolume` + `ApplyMusicVolume()`（`:119-127`）；模组的 `Volume=0.75` 是独立字段（`MusicPlayer.cs:23`）→ **设置里的音乐音量对本模组无效**。

**② 我们照抄必踩的两条硬红线：**
- `System.IO.File/Directory/Path` 直接用 = **`files` 能力**。我们要落 cookie/账号这类数据，只能走 `ModHelper.ReadModFileText` / `ModHelper.*ModDataFile*`（白名单沙箱，实测零能力），否则正式候选过不了零能力扫描。
- `HttpWebRequest` / `UnityWebRequest` = **`network` 能力**。我们两个项目的正式发布是 `<Capabilities/>` 自闭合，**任何联网功能都不能进正式包**——要做只能在开发版分支上做。

**③ 错误处理是"反面教材"**：`MultiHttp` 的每个方法都 `catch { return null; }`（`MultiHttp.cs:63-66`、`:120-123`），`NeteaseApi` 里大量 `catch {}`。结果：**网断了、API 改版了、cookie 过期了，玩家只会看到"加载中…"永远转圈**，日志里什么都没有。按本工作区规范，边界捕获必须 `Debug.LogError` 留堆栈。

**④ `obj/`、`bin/`、`.ps1` 放进 mod 文件夹 = 扫描器拒绝启用。** 作者在 `Directory.Build.props` 里把 `MSBuildProjectExtensionsPath` / `BaseIntermediateOutputPath` 重定向到 mod 目录之外的 `_dangomusic_build\obj`（`src/Directory.Build.props:1-8`），README 也写明编译脚本**故意放在游戏根目录**——因为 `ModScanner` 把文件夹里的 `.ps1` 判为 `payload-file`。这条对我们打包流程是直接约束。

**⑤ 目标框架是 `netstandard2.1`（`src/DangoMusic.csproj:4`），与我们 `net472` 不同**；它引了 `UnityEngine.IMGUIModule` / `TextRenderingModule` / `AudioModule` / `UnityWebRequestModule` 与游戏自带的 `Newtonsoft.Json`。抄工程配置时别照搬 TFM。

**⑥ 合规上它是"干净的"但**不是**可复用的**：包里 0 音频、0 曲库，运行时用玩家自己的账号取流；但它逆向的是**平台私有接口**（WEAPI 加密、`enhance/player/url`、二维码轮询），且带 Referer/UA 伪装。**这类东西不能出现在我们的正式包**，也不适合作为"音频播放"的技术参照——要放音频请走 `ModLoader.LoadSoundsCoroutine`（`<mod>/Sounds/`，键 = 文件名去扩展名）+ 自建 `DontDestroyOnLoad` AudioSource，见 `19-工坊模组逆向与蒸馏.md:217`。

---

## 5. 对我们的可用点

| 目标 | 可用点 | 落点 |
| --- | --- | --- |
| **《深空电玩》** | ①**给原版机器窗口加一个自定义面板**的标准姿势：Postfix 改 `CreateMachineInventoryWindow` 的 `__result` → `(MachineryPanelRow)window.childElement` → `GetPanel("自己id") == null` 判重 → `Add(new MachineryPanel(null, new MachineryButton("名字", 回调), "自己id"))`。零反射、不动原版布局、重开不叠按钮 | 街机机台窗口加「排行榜 / 兑换 / 说明书」按钮；`PanelRow` 也适用于任何 `MachineHelper.CreateMachineryWindow` 出来的机器 |
| **《深空当铺：可能是群友》** | ②**机器按键"接管→转发"的模式**：`Prefix` 里先判 `Active`、再判自己的状态决定是否 `return false`，异常时 `return true` 把控制权还给原版（`HookCassettePlay.cs:12-28`）——比 Transpiler 安全得多，且天然"坏了自动退回原版"。③**`MusicUi` 的 IMGUI 全套件**（滚动 `BeginClip`+`Scrollbar`、自绘滑杆拖拽、`Trim/Trim2` 按像素截断加省略号、`DragSlider` 三态事件）是**不依赖 TMP/PixelWindow 的全屏自绘 UI** 现成参考 | 群友的「群聊面板 / 名片夹 / 排行榜」；当铺的「订单台账 / 长文本浏览」 |
| **两个项目通用（工程）** | ④**代际计数 `_loadGen`** 是异步资源加载的通用护身符：任何"发请求→回主线程→改 UI"的链路都必须带它，并且**丢弃时 `Destroy` 掉拿回来的 `AudioClip`/`Texture2D`**（`MusicPlayer.cs:326-357`、`:443-451`）。⑤**`ConcurrentQueue<Action>` 主线程泵**（`RuntimeDriver.cs:12-63`）：`IMod` 没有 `Update`，挂一个 `DontDestroyOnLoad` 的 MonoBehaviour 当驱动器是标准解法——我们的天际线/自动化线程也能直接套 | 异步加载（图、音、网络）、长时间计算回主线程 |
| **音频能力对照** | ⑥**判断一个模组要不要 `files`/`network`，看它"音频从哪来"最快**：内置文件 → `files`；流式/接口 → `network`；走原版 `AudioManager`/`LoadSoundsCoroutine` → **零能力**。我们做 BGM/配音一律选第三条 | `深空电玩` 街机音效、群友到账语音 |
