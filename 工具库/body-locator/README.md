# body-locator —— 长文本正文定位器

给一段**带脚手架的长文本**（LLM 原始输出、带 JSON 包裹、混着各种标记块），输出**正文所在的连续区间** `[start, end)`。坐标是原始 Unicode 码点偏移，**它不改写任何字符**，`text[start:end]` 就是正文原文。

## 什么时候用它

- 拿 LLM 批量产出文案/台词，输出里混着前言、`<dream_plot>`、`<Status_block>`、结尾选项块，要可靠地把正文摘出来
- 长文本里想快速定位「哪一段才是要的东西」，不想写正则一个个试

**不该用它**：它只给区间不改文；要改写还得自己来。单条短文本（几百字）用正则或肉眼更快，起模型要 ~5 s。

## 规格

| 项 | 值 |
| --- | --- |
| 模型 | `GoForceX/body-locator-small-v1`，架构 `transition-span-locator` |
| 底模 | `alphaedge-ai/mmBERT-small-zho-32768`（ModernBERT，hidden 384 / 22 层 / vocab 32768） |
| 参数 | 57.5 M |
| 窗口 / 上限 | window 4096、overlap 128、max_total_tokens **8192**（超了直接报错，不截断） |
| 输出 | 单一连续区间 `[start, end)`，Unicode 码点偏移 |
| dev 指标 | exact 307/328（**93.6%**），body 字符 F1 **0.9946** |
| 权重 | `models/best_fp16.pt` 115 MB（fp16 存储，加载后按 float32 跑） |

## 怎么跑

**必须用 `D:\0TavernWeave\tools\python314\python.exe`** —— 只有它带 `torch 2.13.0+cu130` + `transformers 5.23.0`。当前环境的 python 3.15 没有 torch。

```powershell
$py  = 'D:\0TavernWeave\tools\python314\python.exe'
$dir = 'D:\0深空当铺模组开发\工具库\body-locator'
$tmp = 'D:\0深空当铺模组开发\缓存文件夹'

Push-Location "$dir\code"          # 在 code 目录里跑，路径用相对写法
& $py locate.py --input '..\examples\example_input.txt' --output "$tmp\out.txt"
Pop-Location
```

实测（官方样例 718 字）：`span=[151,448)`、正文 297 字、token 522、**400 ms**、CUDA。与官方 `examples/example_body.txt` **逐字一致**。

其他用法：

```powershell
& $py locate.py --text "一段带脚手架的文本"
& $py locate.py --stdin < some.txt            # 重定向文件而不是管道
& $py locate.py --input x.txt --json          # 打完整结果（span/token_count/chunk_count/time_ms）
& $py locate.py --input x.txt --span 200 600  # 只在给定区间里找
& $py locate.py --input x.txt --device cpu    # 没显卡时
```

不给 `--output` 就只打印区间和正文前 400 字。

## 三个坑（都踩过，别再踩）

**① 中文绝对路径不能经 PowerShell 传给 python。**
`python314` 开着 UTF-8 模式（`sys.getfilesystemencoding()` = `utf-8`），而 PowerShell 5.1 用 GBK 传参，`sys.argv` 里的中文路径会变成 `D:\0��յ���ģ�鿪��\`，然后 `open()` 报 `FileNotFoundError` 或 `PermissionError`（看起来像权限问题，其实是路径坏了）。
→ **在 `code` 目录里用相对路径**，或者把输入/输出写到纯 ASCII 路径。脚本内部的默认路径走 `__file__`，不受影响。

**② `工具库` 目录 python 写不进去，缓存放 `缓存文件夹`。**
`工具库` 是 **Medium 完整性级别**，而 python 子进程是 **Low**，受 No-Write-Up 限制 → 在工具库里 `mkdir` 直接 `PermissionError [WinError 5]`。`缓存文件夹` 是 Low，可写。
→ `locate.py` 已经把 `TEMP` / `TMP` / `TORCHINDUCTOR_CACHE_DIR` / `TRITON_CACHE_DIR` / `TORCH_HOME` 全指到 `缓存文件夹\body-locator-tmp`。**输出文件也请写到 `缓存文件夹`**。

**③ torch 会把 `torchinductor_<用户名>` 建在当前工作目录。**
谁从哪个目录起进程，就在哪儿留一个空目录。第一次从工作区根跑，根目录就多了一个 `torchinductor_凉宫千夏`。
→ 上面的环境变量已经钉住了；另外**跑之前先 `Push-Location` 到 `code`**，别从工作区根起。

**④ 别给 `checkpoint.load` 传 dtype。**
传 `float16` 会把权重转 half，但 `data.model_inputs` 造的输入张量还是 float32，forward 会报 `mat1 and mat2 must have the same dtype`。要半精度得连着改 `data.py`，不值当——57.5M 参数 float32 在 GPU 上 400 ms 已经够快。

## 目录

```
body-locator/
  code/
    locate.py         ← 入口（我们写的薄壳：补 sys.path、定缓存目录、简化参数）
    infer.py          ← 上游 CLI（BodyLocator / TokenLimitError）
    model.py encode.py span.py structure.py transition.py checkpoint.py data.py
    config.json requirements.txt
  models/
    best_fp16.pt      ← 115 MB
    tokenizer/        ← tokenizer.json + tokenizer_config.json（checkpoint.py 会自动找同目录的 tokenizer/）
  examples/
    example_input.txt / example_body.txt   ← 官方样例与期望输出，用来回归
```

`code/` 里除 `locate.py` 外全是上游原样，没改。

## 来源与脱离依赖

- 模型：`https://huggingface.co/GoForceX/body-locator-small-v1`（fp16 权重 + tokenizer + 推理代码）
- 从旧工作区 `D:\0TavernWeave\tools\shubing-router\_refs\body-locator\` 搬来，**已去掉 `__pycache__`**
- **仍依赖旧区的一件东西**：`tools\python314`（torch + transformers）。要彻底脱离旧区，得在当前环境装 `torch>=2.3` + `transformers>=4.40`（约 2–3 GB），或者在别的机器上准备同版本环境。上游 `requirements.txt` 写 `transformers<5`，但实测 5.23.0 可用。

## 相关

- 工具库总索引：[`工具库\README.md`](../README.md)
- 上游完整的接入设计（服务化、分块、二次保护）：旧工作区 `tools/shubing-router` 的 `serve_locator.py` + `app/locator.py`
