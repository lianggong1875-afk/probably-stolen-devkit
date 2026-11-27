# 深空当铺通用 C# 模组架构模板包

本目录收录从工坊 6 大核心模组（Developer Tools、SDJYBN Framework、Station Events、鉴定扩展家族、双开发桥接器、Portrait Studio 引擎与制作包）反编译源码中蒸馏出的**即插即用、零套娃、无外部依赖** C# 模式模板。

使用时按需复制到自己模组的 `src/` 并修改命名空间，不要把整套不需要的文件一股脑拖进去。

| 模板文件 | 解决什么问题 | 权限影响 | 对应经验库 |
| --- | --- | --- | --- |
| [`ReadyChain.cs`](ReadyChain.cs) | 店内就绪检查链（先判 `PlayerStore.HasInstance`）与 Unity 假 null 检测 | 零能力 | [09](../../共享开发/模组制作经验库/09-模组架构与物品调用规范.md) |
| [`HdCameraLease.cs`](HdCameraLease.cs) | 零反射跨模组 `PixelPerfectCamera` 高清去像素化租约 | 零能力 | [12](../../共享开发/模组制作经验库/12-立绘引擎与音画资源架构.md) |
| [`CustomInspectLens.cs`](CustomInspectLens.cs) | 自定义物品接入原版放大镜双视图 + `LensFollow` 镜头同步 | 零能力 | [13](../../共享开发/模组制作经验库/13-鉴定扩展与交互纸张规范.md) |
| [`Es3SafePersistence.cs`](Es3SafePersistence.cs) | `ES3Type.WriteProperties` 浅克隆投影剥离，防止卸载模组坏档 | 零能力 | [14](../../共享开发/模组制作经验库/14-模组高级编程架构与工程模式.md) |
| [`ModalInputGate.cs`](ModalInputGate.cs) | 游戏内大型弹窗防关窗点击穿透状态机与焦点清理 | 零能力 | [14](../../共享开发/模组制作经验库/14-模组高级编程架构与工程模式.md) |
| [`WavPcmDecoder.cs`](WavPcmDecoder.cs) | 纯 C# 解析 RIFF PCM16 WAV 并转 `AudioClip`（支持后台线程） | 零能力（若读磁盘文件则需 `files`） | [12](../../共享开发/模组制作经验库/12-立绘引擎与音画资源架构.md) |
| [`SafeSpriteHelper.cs`](SafeSpriteHelper.cs) | 像素贴图 `FilterMode.Point` 锐化、高清道具 PPU 公式、GPU 不可读纹理导出 PNG | 零能力 | [12](../../共享开发/模组制作经验库/12-立绘引擎与音画资源架构.md) / [14](../../共享开发/模组制作经验库/14-模组高级编程架构与工程模式.md) |
| [`SpriteSheetMachineAnim.cs`](SpriteSheetMachineAnim.cs) | 8 列网格雪碧图机器运行动画播放器（零存档污染、150ms 节流、供电/悬停阈值保护） | 零能力 | [15](../../共享开发/模组制作经验库/15-机器运行动画与雪碧图规范.md) |
| [`NewspaperArtHelper.cs`](NewspaperArtHelper.cs) | 报纸事件配图渲染器（9 色复古相框、等比 `CenterCrop`、与 Better Newspapers 兼容排版避让） | 零能力 | [16](../../共享开发/模组制作经验库/16-报纸事件配图与BetterNewspapers规范.md) |
