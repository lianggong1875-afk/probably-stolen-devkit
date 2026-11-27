# 清单填写

编辑 `portrait-studio-provider.json`：

- providerId 与 manifest ID 一致；revision 标识接入范围，catalogRevision 标识清单，resourceRevision 标识不可变图片。
- 每个显示位置一个 target，人物、照片和场景分开；自定义身份使用创建者 mod ID 与稳定 key。
- 每张候选放在 resourceSets；填写相对 PNG、内容哈希、完整 texture 和 geometry（rect、PPU、pivot、border）。
- target 用 candidateResources 引用候选，defaultMode=resource 与 defaultResource 指定独立运行默认图；已有工程用 source 默认。
- 清单根、资源组、位置和图片全部填写 renderMode: static；contract 固定 static-display-transaction/1。

字段定义和限制见[公开协议](静态资源公开协议.md)与[Schema](portrait-studio-static-v1.schema.json)。
