# 使用准备

## 版本与 Full 切换 / Editions and switching to Full

工坊运行 **Compatible 兼容版**：保留 MCP 查询、UI 操作、配置、场景和测试会话，不载入外部探针 DLL，不热重载 MOD。`FullSource/` 提供完整 C# 源码、工程与构建说明，不含第二个 DLL。`get_development_environment` 或 `/bridge/status` 的 `edition` 表示实际运行版本。

需要探针或热重载时，Agent 先读 [Full 构建与切换说明](FullSource/BUILD.md)，说明动态代码执行、本地开发者模式的信任范围和停止工坊自动更新等风险，取得用户明确同意后，在独立工程目录编译。先把 Compatible 安装成本地 MOD，由用户批准并手动开启开发者模式，再退出游戏换成 Full DLL 与配套 manifest。不要同时加载工坊与本地副本，不修改工坊目录、加载器或信任数据库。未经同意继续用 Compatible。

Workshop runs **Compatible**. FullSource contains readable sources and a project, not another DLL. Before switching for probes/hot reload, the agent reads [BUILD.md](FullSource/BUILD.md), explains runtime execution, local Developer-mode trust and loss of automatic Workshop updates, and obtains explicit consent. Compile outside Workshop/game folders. Approve the local Compatible copy first, have the player manually enable Developer mode, quit, then replace its DLL and manifest with Full. Keep one active copy and verify the running edition after restarting.

给 AI Coding Agent：先完成工具准备和风险确认，再按第 3 项启用游戏内 MOD，并阅读 [AI_GUIDE.md](AI_GUIDE.md) 连接游戏。请用用户使用的语言沟通。静态审查可以跳过。

在用户授权范围内完成准备与开发，已有授权覆盖的工作直接继续。需要扩大授权范围，或出现此前未说明的高风险影响时，再说明并确认。破坏性实验或向正式存档写入持久变更前，备份受影响文件。

## 1. 在用户授权下准备开发工具

先检查已有工具，优先复用可用的 .NET SDK 和 .NET 程序集反编译工具。例如：用 `dotnet --list-sdks` 检查 SDK，用 `ilspycmd --version` 检查 ILSpy 命令行工具；也可以使用用户已有的同类工具。

缺少工具时，说明需要下载什么、来源及安装位置，按上述授权范围完成下载和安装。

- **C# 编译工具**：获取与用户 Windows 环境及所选工具兼容、仍受支持的 .NET SDK。SDK 提供编译器；只安装 Runtime 无法满足编译需求。游戏仍使用自带的 Unity Mono，编译 MOD 时引用当前游戏的 Managed 程序集。
- **反编译工具**：可选择 ILSpy / ilspycmd，或用户认可的同类工具。Agent 可优先选择能导出 C# 源码的命令行版本。工具和反编译输出放在用户选择的工具/工程目录中。

官方入口：

- [.NET SDK 下载](https://dotnet.microsoft.com/download/dotnet)与 [Windows 安装说明](https://learn.microsoft.com/en-us/dotnet/core/install/windows)。
- [ILSpy 项目及发行包](https://github.com/icsharpcode/ILSpy)与 [ilspycmd 官方 NuGet 包](https://www.nuget.org/packages/ilspycmd)。根据所选版本核对 SDK/运行时要求。

**下载失败时由 Agent 继续协助：**

1. 区分网络不可达、安装权限和版本不兼容，寻找另一个官方入口。例如 GitHub 无法访问时，可检查官方 NuGet 包是否可用。
2. Agent 能指定搜索引擎或操作浏览器时，优先使用 Bing 搜索官方站点、作者发行页和安装说明；内置搜索无法指定引擎时，使用其可用搜索能力。找到的下载来源需要核对发布者，不能只依据搜索排名。
3. 官方入口仍不可达时，提示用户手动下载。给出具体下载页、所需版本/架构或文件名，以及建议保存位置；让用户提供已下载文件或所在路径，随后由 Agent 检查并继续配置。

下载失败后提供明确下一步，不反复重试同一地址，也不要求用户自行研究工具。不要擅自安装代理、修改网络设置或关闭系统防护。工具可用后，简要告知用户其版本与位置。

这些工具用于编译 MOD、反编译和审查程序集；连接本 MOD 的 MCP 本身不需要安装 SDK。

## 2. 风险确认与静态审查（审查可跳过）

工具准备完成后，说明当前包内 `ModDevBridgeNext.dll` 的能力与风险。作者建议反编译后进行静态审查，检查是否存在恶意或超出 MOD 开发用途的代码。用户可以选择审查，也可以跳过审查并继续使用。

风险说明应包含：

- DLL 在游戏进程中执行，受游戏进程的操作系统权限约束；Compatible 声明文件和网络能力，Full 另声明动态代码加载与外部反射能力，需要本地开发者模式。
- 两版都有文件访问和游戏状态修改能力；Full 另提供探针加载和 MOD 热切换，探针代码也在游戏进程中运行。
- MCP 监听 `127.0.0.1`，无需令牌；本机其他程序也可能访问端点并发起操作。
- 开发操作可能影响存档、配置或其他 MOD。测试会话的资源清理不能恢复所有副作用。

用户选择审查时，确认实际要使用的 DLL 路径并记录其 SHA256，使用反编译工具读取该文件的代码、程序集引用和嵌入内容。重点检查文件访问范围、网络目标、数据收集或外传、启动入口、动态加载来源，以及与声明用途无关的行为；区分本 MOD 的预期开发能力与可疑实现。审查无需先启用或执行目标 DLL。

报告检查范围、发现和未能判断的部分；“未发现可疑行为”不等于保证不存在恶意代码。发现可疑行为时，指出具体位置并交由用户决定是否继续。用户已确认继续使用或选择跳过审查时，直接进入下一项；跳过不表示已经审查通过。

## 3. 在游戏里启用 MOD 后再开始

风险确认完成后，提醒用户在游戏的 MOD 管理界面中启用 **用 AI 开发 MOD**，按游戏提示批准所需能力并重启游戏。

**订阅不等于启用。游戏运行且本 MOD 已启用后，Agent 才能连接 MCP，开始游戏内的观察、实验和验证。** 按 [AI_GUIDE.md](AI_GUIDE.md) 验证实际连接；已有可用连接时直接继续，无需再次要求用户启用或重启。

如果宿主早于游戏启动而保持连接失败，游戏启动后由 Agent 按连接指南主动恢复 MCP 连接。首次需要进存档实验时，确认用户指定的测试存档，再由 Agent 通过游戏 UI 加载；已明确选择且正确加载时直接继续。
