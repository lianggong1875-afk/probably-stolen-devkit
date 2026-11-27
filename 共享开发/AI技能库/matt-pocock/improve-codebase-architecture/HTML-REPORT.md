# HTML 报告格式

架构评审渲染成单个自包含的 HTML 文件，放在操作系统临时目录里。Tailwind 和 Mermaid 都通过 CDN 引入。图状图表交给 Mermaid 可靠渲染；更具编辑感的视觉（体量图、剖面图）用手写 div 与内联 SVG。两者混着用：不要什么都靠 Mermaid，那样会开始显得千篇一律。

## 骨架

```html
<!doctype html>
<html lang="en">
  <head>
    <meta charset="utf-8" />
    <title>Architecture review for {{repo name}}</title>
    <script src="https://cdn.tailwindcss.com"></script>
    <script type="module">
      import mermaid from "https://cdn.jsdelivr.net/npm/mermaid@11/dist/mermaid.esm.min.mjs";
      mermaid.initialize({ startOnLoad: true, theme: "neutral", securityLevel: "loose" });
    </script>
    <style>
      /* small custom layer for things Tailwind doesn't cover cleanly:
         dashed seam lines, hand-drawn-feeling arrow heads, etc. */
      .seam { stroke-dasharray: 4 4; }
      .leak { stroke: #dc2626; }
      .deep { background: linear-gradient(135deg, #0f172a, #1e293b); }
    </style>
  </head>
  <body class="bg-stone-50 text-slate-900 font-sans">
    <main class="max-w-5xl mx-auto px-6 py-12 space-y-12">
      <header>...</header>
      <section id="candidates" class="space-y-10">...</section>
      <section id="top-recommendation">...</section>
    </main>
  </body>
</html>
```

## 页眉

仓库名、日期，加一条紧凑的图例：实心方框 = 模块，虚线 = 接缝，红色箭头 = 泄漏，深色粗框 = 深模块。不写引言段。直接进候选方案。

## 候选卡片

图承担主要分量。文字稀疏、平实，直接用术语表词汇（来自 `/codebase-design` 技能），不搞仪式感。

每个候选方案是一个 `<article>`：

- **标题**：短，点出深化动作（例如“合并 Order 接单流水线”）。
- **徽标行**：推荐强度（`Strong` = 翠绿，`Worth exploring` = 琥珀，`Speculative` = 石板灰），再加一个依赖类别的标签（`in-process`、`local-substitutable`、`ports & adapters`、`mock`）。
- **涉及文件**：等宽字体列表，`font-mono text-sm`。
- **改前/改后对比图**：核心。两列并排。见下面的模式。
- **问题**：一句话。哪里疼。
- **方案**：一句话。改什么。
- **收益**：要点列表，每条不超过 6 个词。例如“测试只打一个接口”“定价逻辑不再泄漏”“删掉 4 个浅包装”。
- **ADR 提示块**（如适用）：一行，放在琥珀色底的框里。

不要解释性段落。如果一张图需要一段话才看得懂，就重画那张图。

## 图表模式

挑适合该候选方案的模式。混着用。不要让每张图都长得一样。多样性本身就是重点的一部分。

### Mermaid 图（依赖/调用流的主力）

要表达的重点是“X 调 Y、Y 调 Z，看看这一团乱”时，用 Mermaid 的 `flowchart` 或 `graph`。把它包进 Tailwind 样式的卡片里，别让它像是空降的。用 classDef 上样式，把泄漏边染红、把深模块染深。时序图很适合“改前：6 次往返；改后：1 次。”

```html
<div class="rounded-lg border border-slate-200 bg-white p-4">
  <pre class="mermaid">
    flowchart LR
      A[OrderHandler] --> B[OrderValidator]
      B --> C[OrderRepo]
      C -.leak.-> D[PricingClient]
      classDef leak stroke:#dc2626,stroke-width:2px;
      class C,D leak
  </pre>
</div>
```

### 手写方块与箭头（当 Mermaid 的布局跟你较劲时）

模块用带边框和标签的 `<div>`。箭头用内联 SVG 的 `<line>` 或 `<path>` 元素，绝对定位在一个相对定位的容器上。当你想让“改后”的图看起来像一个带粗边框、内部灰掉的深模块时就用它，因为 Mermaid 渲染不出那种分量。

### 剖面图（适合分层的浅）

把水平色带（`h-12 border-l-4`）堆起来，展示一次调用穿过的层。改前：6 个薄层，每层都没做什么事。改后：1 个厚色带，标注合并后的职责。

### 体量图（适合“接口和实现一样宽”）

每个模块两个矩形：一个表示接口的表面积，一个表示实现。改前：接口矩形几乎和实现矩形一样高（浅）。改后：接口矩形矮，实现矩形高（深）。

### 调用图折叠

改前：一棵函数调用树渲染成层层嵌套的方框。改后：同一棵树折叠成一个方框，如今变成内部的那些调用在框内淡显。

## 样式指导

- 偏编辑感，不要企业仪表盘。留足空白。标题可选衬线字体（`font-serif` 配 stone/slate 很搭）。
- 用色克制：一个主色（翠绿或靛蓝），加上表示泄漏的红和表示警告的琥珀。
- 图高控制在约 320px，让改前/改后能舒服地并排摆着，不用滚动。
- 图内的模块标签用 `text-xs uppercase tracking-wider`，读起来像示意图，不像界面。
- 全文只有两处脚本：Tailwind CDN 与 Mermaid 的 ESM 引入。除此之外报告是静态的：没有应用代码，除 Mermaid 自身的渲染外没有任何交互。

## 首选推荐一节

一张更大的卡片。候选方案名、一句说明为什么、指向它自己卡片的锚点链接。就这些。

## 语气

语言平实、简洁，但架构名词和动词直接取自 `/codebase-design` 技能。简洁不是跑偏的借口。

**只用这些词：** 模块（module）、接口（interface）、实现（implementation）、深度（depth）、深（deep）、浅（shallow）、接缝（seam）、适配器（adapter）、杠杆（leverage）、局部性（locality）。

**绝不要替换成：** 组件（component）、服务（service）、单元（unit，指 module 时）· API、签名（signature，指 interface 时）· 边界（boundary，指 seam 时）· 层、包装（layer、wrapper，指 module 时，当你的意思就是模块）。

**符合这种语气的说法：**

- “Order 接单模块是浅的：接口几乎等同于实现。”
- “定价从接缝处泄漏。”
- “深化：一个接口，一处测试点。”
- “两个适配器才让接缝成立：生产用 HTTP，测试用内存实现。”

**收益要点**用术语表的词说清收益：*“局部性：bug 集中在一个模块里”*、*“杠杆：一个接口，N 个调用点”*、*“接口缩小；实现吸收掉那些包装”*。不要写*“更易维护”*或*“代码更干净”*，那些词不在术语表里，也不配占位。

不含糊，不铺垫，不写“值得注意的是……”。一句话如果能写成要点，就写成要点。一个要点如果能删，就删掉。某个术语不在 `/codebase-design` 的术语表里，先去找术语表里已有的词，别自己造一个新的。
