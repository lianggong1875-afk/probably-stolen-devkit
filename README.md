# 深空当铺模组开发工作区

根目录规则见 [AGENTS.md](AGENTS.md)，编程与发布细则见 [开发流程与编程规范](共享开发/开发流程与经验.md)。

## 项目索引

| 项目 | 进度入口 | 标识 | 说明 |
| --- | --- | --- | --- |
| 深空电玩 | [深空电玩/PROGRESS.md](深空电玩/PROGRESS.md) | `deepspace_arcade` · `DeepSpaceArcade.dll` | 二手游戏店、光盘/硬件鉴定、刻录机、行情与来客 |
| 深空当铺：可能是群友 | [深空当铺：可能是群友/PROGRESS.md](深空当铺：可能是群友/PROGRESS.md) | `freya_cornell` · `DeepSpacePawn.dll` | 芙蕾雅·柯涅尔、伞萝萝、塞壬角色与剧情模组 |
| 深空桥接器 | [深空桥接器/PROGRESS.md](深空桥接器/PROGRESS.md) | `ModDevBridgeNext` | 游戏内 MCP 开发桥本地源码工程 |

## 常用入口

- **经验库**：[共享开发/模组制作经验库/README.md](共享开发/模组制作经验库/README.md)（原版 API、权限扫描、踩坑台账、桥接器速查、事件系统）
- **工具库**：[工具库/README.md](工具库/README.md)（统一构建、能力扫描、精简回归、真机探针）
- **版本控制**：本地 Git 仓库位于 `Git仓库\`，直接在工作区根目录运行 `git status` / `git log` / `git diff`。

## 快速命令

```powershell
# 编译深空电玩（输出到 深空电玩\build\latest\，可直接覆盖）
.\工具库\build\Build-Mod.ps1 -Mod Arcade

# 编译零能力正式发布候选并扫能力
.\工具库\build\Build-Mod.ps1 -Mod Arcade -PublicRelease -Scan

# 编译并同步 DLL/Textures/Books 到游戏 Mods 目录
.\工具库\build\Build-Mod.ps1 -Mod Arcade -Install

# 运行离线回归
.\工具库\regression\Run-Regression.ps1
```
