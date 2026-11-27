# 变更记录

## 1.5.0 — 2026-11-24

新增 2 条踩坑（#51、#52），修正批量放置的建议，并把工坊默认描述改为英文（面向国际读者）。DLL 未变。

- `docs/04`：新增 #51「分帧 + 每帧 Validate」是最差组合（71 件累计约 360 次重复验证，连续掉 9 帧；同步一次做完 + 单次刷新更快）、#52 大件上架要先预检再前置（`TryAddDirectSellingItemToTable` 失败会直接 Destroy）。
- `docs/05` 第 4 节重写：拆成「≤100 件同步一次做完」与「必须保持响应时分帧但每帧不 Validate」两条路线。
- `docs/11` 新增 4.1「同步 vs 分帧」实测；排查清单补一条。
- `docs/08` 症状表补 2 行。
- README / AI_GUIDE：52 条、索引补 2 行、速览第 10 条补「分帧不等于更快」。
- `publish/desc_en.txt` 重写为面向国际读者的完整英文介绍；工坊默认描述改为英文，`schinese` 变体保持中文，`english` 变体同步。

## 1.4.0 — 2026-11-17

新增 9 条踩坑（#42～#50）与三节配方，来自 11-09 ～ 11-15 的批量发酵、词条角标、异形大机器、手柄支持与部署踩坑。DLL 未变。

- `docs/04`：新增 #42 拖拽委托是 AND 聚合（目标侧 false 会否掉原版工具）、#43 消耗材料交给 `EndDrag` 收尾、#44 批量结算不要逐件调「默认 Validate」的 helper、#45 手改 `modTrust` 会让整个信任库失效（`modTrustMac` 签名）、#46 自动部署必须 DLL + manifest 一起复制、#47 拖到容器图标批量处理要先挡 `MayHaveValidInventorySlot`、#48 三套坐标原点（Unity 客户区左下 / Windows 屏幕左上）、#49 非矩形大 shape 的节点创建成本、#50 没有候选目标时不要逐个全仓库扫描。
- `docs/05`：新增第 14 节「批量手势：拖到容器图标上」、第 15 节「词条角标」、第 16 节「非矩形大 shape」。
- `docs/03`：新增 6.5「交互委托」、9.7「输入与原生光标」；6 节补效果 tag 与超载、9.6 补 `FindChildType` / `FindAllChildrenType` / `SetShape(shape,width)`；按功能查表补 4 行。
- `docs/06`：补「自动部署漏 manifest」与「申请了能力要在描述里说明」。
- `docs/08`：症状表补 9 行。
- README / AI_GUIDE：50 条、速览 10 条、索引新增 4 行。

## 1.3.0 — 2026-11-11

新增第 11 篇 `docs/11-性能优化-大网格拖拽.md` + 坑 #41，来自「模组集成器」15×15 网格框选拖动卡死/死机的修复（2026-11-11 实机确认明显改善）。DLL 未变。

- `docs/11`：完整因果链（原版 `OnAddedModuleToGrid` → `ComputeAllModuleTempStat` → 每件 3 次 `ValidateShapeState` → `parentInventory.Validate()` + 父链递归 = 平方级）、四条可复用优化（帧末合并 / 拖拽期间不结算 / 大列表 `skipValidate` 批量写 + `SyncModifiedState` / 全图扫描缓存）、顺序陷阱（先 sync 再跑依赖 `modifiedState` 的 `onAnyAction`）、通用排查清单。
- `docs/04`：新增坑 #41（精简版，指向第 11 篇）。
- `README.md`：目录、索引、速览新增第 9 条（大网格重算）、踩坑条数 40 → 41。
- `AI_GUIDE.md`：任务 A 增加性能篇阅读项；文件分类 `01`~`09` → `01`~`11`、条目号 1~41。

## 1.2.0 — 2026-11-11

新增第 10 篇 `docs/10-官方红线清单.md`：**官方明令禁止的「信任层」动作**（批准 / 启用 / 停用 / 排序 / 开发者模式 / 移除 / 写信任记录 / 换信任列表 / 取消订阅 / `Process.Start` / `SaveFiles.*`）。这类调用会被 `ModScanner` 判 `loader-target` **直接拒绝加载**，运行时还有 `RequireNoMod()` 遍历调用栈抛 `UnauthorizedAccessException`（反射、协程、`ExecuteEvents` 派发点击都绕不过）。DLL 未变。

- `docs/10`：两道闸门的机制、被标注成员全表（`ModLoader` 5 个 + `ModTrust` 6 个 + 其它类别）、真实被拒案例、30 秒自查命令（`ilspycmd` 搜 `ModRestricted`）、合规替代做法、给 AI 的硬规则。
- `docs/07`：开头指向第 10 篇，说明「红线不是可以用 `<Capabilities>` 声明换通过的」。
- `README.md` / `AI_GUIDE.md`：目录、索引、「最值钱的 7 条」→ 8 条；AI 阅读顺序新增「任务 0：需求带 一键/批量/自动 字样先判红线」。
- 触发原因：2026-11-11 一个「一键全选启用」模组（`quick-enable`）在启用时被 `loader-target: … approves a mod, which only the player does` 当场拒绝，确认属于官方设计禁令，已作废。

## 1.1.1 — 2026-11-08

修正 `docs/05` 第 13.4 节的落点示例：原来的三元表达式在「拖到容器物品上」时会返回 null，改成清晰的 if/else 与 `MainInventoryOf` 占位。

## 1.1.0 — 2026-11-08

新增 10 条踩坑（#31～#40）与一篇「自绘窗口」配方，来自仓储终端、暗格储藏、可负债交易三个新模组的实战。DLL 未变。

- `docs/04`：新增 #31 自建窗口不要借用 `GameItemElement`、#32 `GameGridInventory.handler` 尺寸为 0、#33 `PixelWindow.handler` 漏判、#34 隐藏窗口仍拦截点击（多选组拖失效）、#35 `TryInventorySlot` 传源形状只有第一件能放、#36 拖拽失败不要溢出到仓库、#37 不要自建 `TMP_InputField`、#38 `SetPixels32` 行序导致像素 HUD 上下翻转、#39 不要用持久 tag 表达暗格、#40 补丁 early return 范围过宽。
- `docs/05`：新增第 13 节「自绘窗口：汇总、搜索、拖拽 / 框选取出」。
- `docs/03`：新增 9.5「违禁品与搜查」、9.6「窗口、射线与自绘 UI」，按功能查表补 3 行。
- `docs/06`：补充新建物品冷却期与 `GetPublishedFileDetails` 索引延迟（`result=9`）。
- `docs/08`：症状速查表补 8 行（窗口命中、框选失效、拖拽只进一件、像素 HUD 翻转等）。
- README / AI_GUIDE：条数、索引入口同步。

## 1.0.1 — 2026-11-06

文档修正（不涉及功能，DLL 未变）。

- 修正 `docs/04` 第 28 条的结局示例：`DisplayUI` 改为 `GameOverUIManager.Instance.DisplayUI`，`RunStats.OnRunEnded` 的日期改用 `StoreStation.GetDayCounter()`。
- 修正 `docs/03` 的搬物品 API：`inventory.TryFindOneValidInventorySlot(item).TryAcceptOnce(n)`。
- `docs/04` 的「补充」小节标题层级修正。
- manifest 描述里的踩坑条目数改为 30。

## 1.0.0 — 2026-11-06

首次发布。

- 新增 `README.md`（人类入口）与 `AI_GUIDE.md`（AI 阅读协议）。
- 新增 9 篇文档：
  - `01-环境与工具链`：运行时、反编译、csc 编译模板、编码坑、加载调试循环。
  - `02-Mod结构与manifest`：最小目录、字段规则、IMod 生命周期、信任机制、目录白名单。
  - `03-官方API地图`：ModHook 全表、物品/机器/液体/模组/库存/客户/UI/存档 API、按功能查表。
  - `04-踩坑清单`：25 条原始坑 + 5 条补充，每条含现象/原因/做法。
  - `05-代码配方`：右键浮窗、HUD、机器、批量协程、存档状态、自定义结局、拖拽、商人、旧档补偿。
  - `06-发布与工坊`：发布命令、标题/描述/语言、封面、10 项检查、失败码。
  - `07-安全边界`：扫描器判定项、能力声明、发布前自审。
  - `08-调试与证据`：四层排查、症状速查表、存档核对、证据格式。
  - `09-给AI的协作方法`：常见错误、7 步工作流、审查清单、交接模板、提示词模板。
- 空壳 DLL：只打两行日志，不改变任何游戏内容。
