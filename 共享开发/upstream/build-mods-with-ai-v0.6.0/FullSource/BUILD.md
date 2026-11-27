# 在本机编译并启用 Full / Build and enable Full locally

本目录只有源码，不含第二个可执行 DLL。工坊运行 Compatible；Full 由 Agent 在游戏外编译，通过游戏的本地开发者模式加载。

## Agent：先说明风险并取得同意

需要加载探针或热重载 MOD 时，用用户的语言说明：

- Full 会在游戏进程内执行后来编译的 DLL，可修改存档、配置和其他 MOD；这些代码不受首次启用时的扫描覆盖。
- 本地开发者模式会信任这个 MOD 文件夹之后的代码变化，包括扫描拒绝的代码。Full 仍可能显示能力警告。
- MCP 仅监听本机、没有认证；本机程序也可能发起调用。资源清理不能撤销全部副作用。
- 需要先保存工坊包，再改用本地 MOD；本地副本不会随工坊自动更新。可以保留订阅，当前游戏会优先使用同 ID 的本地副本并跳过工坊副本。

等待用户明确同意此切换。已有明确涵盖这些风险的授权可以复用。未同意时继续使用 Compatible，不安装 Full，不修改信任记录或开启开发者模式。风险说明与源码审查分别处理；允许用户跳过静态审查。

## 在独立工程目录编译

1. 将**整个工坊包**复制到用户的工程目录，保留原始 Compatible DLL、manifest、文档和本目录。记录包版本与 DLL SHA256，不编辑 Steam 管理的副本。
2. 校验 `source-hashes.json` 中每个相对路径的 SHA256。它用于检查源码完整性，与发布者身份验证是两回事。
3. 检查已有 `dotnet --list-sdks`。工程使用 .NET 10 或兼容它的更新 SDK 作为编译器，运行时引用来自当前游戏的 Mono Managed 目录，无第三方 NuGet 包或游戏内编译器。
4. 在复制出的工程中运行，替换为实际绝对路径：

```powershell
dotnet build 'G:/MyProjects/BridgePackage/FullSource/ModDevBridgeNext.csproj' --configuration Release --artifacts-path 'G:/MyProjects/BridgeBuild/full' '-p:GameManaged=D:/SteamLibrary/steamapps/common/Probably Stolen/Probably Stolen_Data/Managed' '-p:BridgeEdition=Full'
```

DLL 位于 artifacts 下 `bin/ModDevBridgeNext/release/ModDevBridgeNext.dll`，以实际输出为准。不要把 `obj`、`bin`、SDK 引用程序集或 `.deps.json` 等构建文件复制进 MOD 目录。Full 使用 `BRIDGE_FULL` 条件编译；不定义就是 Compatible。`-p:BridgeEdition=Compatible` 可以重编兼容版，使用另一个 artifacts 路径。

## 首次切换：先批准本地 Compatible，再换 Full

当前游戏的首次启用检查仍会拒绝危险能力，因此先建立本地 Compatible 的批准记录。

1. 保存好工坊包与已编译 Full DLL，结束测试会话，退出游戏。正在正式存档中时，先由用户决定是否保存进度。
2. 保留一个本地 `ModDevBridgeNext` 安装位置。当前游戏遇到同 ID 的本地和工坊副本时优先使用本地，工坊副本会被跳过，因此可以保留订阅。不要启动两个相同端口的 MCP。
3. 把保存的 **Compatible 包**复制到 `<游戏>/Mods/ModDevBridgeNext/`。此时使用原包根目录的 Compatible manifest 与 DLL。
4. 启动游戏，由用户批准并启用这个**本地**副本。批准后详情页才会显示“开发者模式”；由用户手动打开，再退出游戏。Agent 不调用 `SetDeveloperMode`，不改写信任数据库。
5. 游戏退出后，只用编译的 Full DLL 替换本地 `ModDevBridgeNext.dll`，用 `FullSource/manifest.xml` 替换本地根目录的 `manifest.xml`。保留相同 MOD ID 与 DLL 名，以及文档和预览图。保留 Compatible 原件用于恢复。
6. 重启并重新发现端点。通过 `/bridge/status` 和 `get_development_environment` 核验 `edition=full`、`dynamicModules=true`、`releaseId`、实际 SDK 路径。用 `tools/list` 确认 `load_probe_module`、`call_probe`、`reload_mod` 出现；编译成功或磁盘替换不能证明运行版本已经切换。

本地开发者模式不放行黑名单、游戏强制停止记录或全局暂停。遇到这些状态时报告游戏给出的原因。未来游戏改变流程时，重新核对当前程序集，不修改加载器绕过检查。

## 更新与恢复

Full 不随 Steam 自动更新。获取新版源码后在工程目录重编，退出游戏再替换 DLL 与配套 manifest，重启核验版本。不要把 Full DLL 放回工坊目录。

恢复时退出游戏，将本地 DLL 和 manifest 一起换回保存的 Compatible 配套文件；重启确认 `edition=compatible`，再由用户关闭开发者模式并按提示重新批准。回到工坊管理时先退出并移走本地副本，确认仍订阅工坊条目，再启动并按提示批准工坊来源。两个版本共用运行目录和端口；探针程序集要重启才能卸出，游戏或存档修改不会因换版自动撤销。

## English instructions for agents

Workshop ships one Compatible DLL plus readable sources. Full loads probes and hot reloads MODs at runtime. Explain that later DLLs run inside the game, can change saves/config/other MODs, and are not covered by the initial scan. Local Developer mode trusts later folder changes even when scanning refuses them. Loopback MCP has no authentication; cleanup is not a complete rollback. Explain moving away from automatic Workshop updates and obtain explicit consent. Reuse prior consent only when it covers these risks. Otherwise stay on Compatible.

Copy the entire package into an independent user project, verify source-hashes.json, and run the build command above with the actual game Managed path. The SDK supplies the compiler; runtime references come from Unity Mono. Keep all build outputs outside Workshop/game folders.

Preserve the Compatible package and Full output, then quit. Keep one local installation and install the original Compatible package there. The current game prioritizes the local copy over a Workshop copy with the same ID, so the player can remain subscribed; Workshop updates do not update local Full. Have the player approve/enable the local copy and manually turn on Developer mode in its details, then quit. Replace only the local DLL with the built Full DLL and the local manifest with FullSource/manifest.xml. Do not edit trust records or call loader approval/developer-mode APIs. Restart and verify edition=full, matching release identity/SDK path, and the three additional MCP tools. Developer mode does not override blocklists, forced-stop records or a global pause.

For recovery, quit and restore the saved Compatible DLL and manifest together, restart and verify edition=compatible, then have the player turn Developer mode off and approve as prompted. To return to Workshop, quit, move the local copy out of Mods, ensure the item is subscribed, then restart and approve the Workshop source as prompted. Local Full updates require rebuilding; never put a Full binary or build artifacts in the Workshop folder.
