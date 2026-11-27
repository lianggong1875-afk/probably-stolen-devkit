# Progress Bar（读档/过夜进度条与分帧加载）｜工坊 3846086230

> 蒸馏自反编译源码 `缓存文件夹\decomp_ws\3846086230`（29 个 .cs / 1720 行）。
> 价值：**完整逆向出原版存档解码管线（SaveState → GameItem 树 → 挂接库存）**，并给出"把一次性同步加载拆成分帧"的工程范式。

## 1. 它解决什么

原版读档把整棵物品树在**一次方法调用**里重建（`PlayerStore.DecodeSaveItem`），主线程全程阻塞：大店要卡 40 秒，没有一帧画面、看不出进展。

本模组：Prefix 拦下 `DecodeSaveItem`，自己按 12ms/帧的预算分四个阶段重建，同时挂一个覆盖层显示真实进度；任何一步出错就 `Fallback(原因)` 回退到原版一次性加载。

## 2. 原版存档数据结构（从代码反推，字段名可信）

```csharp
class SaveState        { List<SaveItemNode> saveItems; }
class SaveItemNode {
    long   uuid;                    // 本次读档内的临时标识，用于父子挂接
    string identifier;              // 物品 ID（DirectoryMaster 的键）
    string itemType;                // 完整类型名，== typeof(GameCharacterItem).FullName 时走 CharacterItem
    int    unitCount;
    ItemState itemState, itemStateModified;     // state / modifiedState 的源
    ItemShape itemShape, itemModifiedShape;     // shape / modifiedShape 的源（.Build() 还原）
    string name, shortDescription, flavorText;  // 均经 LocHelper.RelocalizeIfMissing()
    long   uniqueId;
    long   unitBaseValue, unitValue, lateUnitValue, backupUnitValue;
    List<string> itemTypes;                     // 品类标签集合
    string spriteAtlasPath, spritePath;
    List<long> childItems;                      // 子物品 uuid
    List<int>  childItemInventoryNode;          // 子物品落在第几个"非物品节点"（即容器）里
}
```

**关键洞察**：原版存档只存"物品 + 它挂在第 N 个容器节点下"，容器本身是物品的一部分（`GraphNodeStorage` 树）。所以重建必须分两步：先建物品、再按 BFS 序把容器编号对齐。

## 3. 四阶段分帧重建（可直接照抄的骨架）

```csharp
enum Phase { Idle, Create, Link, Post, Attach }
// 每帧 while 循环里跑，超过 0.012f 就 break 等下一帧
const float Budget = 0.012f;

Phase.Create  : 逐条 CreateNode(node)  → _items[uuid] = item
Phase.Link    : 逐条按 node.childItems/childItemInventoryNode 挂接
Phase.Post    : 逐物品跑原版 load 钩子全家桶 + item.onLoaded(item)
Phase.Attach  : 把临时 root 里的物品 Expel → 目标 inventory.UncheckedAccept → 销毁临时 root
```

### 3.1 Create 阶段（单个物品重建的完整字段清单）

```csharp
GameItem item = node.itemType == typeof(GameCharacterItem).FullName
    ? DirectoryMaster.CharacterItem(node.identifier)      // 角色物品专用工厂
    : DirectoryMaster.Item(node.identifier, true);        // 常规物品，true = 给玩家
if (item == null) return null;                            // ID 不存在 → 直接跳过（不炸）

item.unitCount       = node.unitCount;
item.state           = node.itemState.Clone();
item.modifiedState   = node.itemState.Clone();            // 注意：源码用的也是 itemState（可疑，见第 7 节）
item.shape           = node.itemShape.Build();
item.modifiedShape   = node.itemModifiedShape.Build();
if (node.name != null) item.name = LocHelper.RelocalizeIfMissing(node.name);
item.shortDescription = LocHelper.RelocalizeIfMissing(node.shortDescription);
item.flavorText       = LocHelper.RelocalizeIfMissing(node.flavorText);
item.uniqueId        = node.uniqueId;
item.unitBaseValue   = node.unitBaseValue;
item.unitValue       = node.unitValue;
item.lateUnitValue   = node.lateUnitValue;
item.backupUnitValue = node.backupUnitValue;
item.itemTypes       = node.itemTypes.ToList();
item.SetSprite(node.spriteAtlasPath, node.spritePath);
StorageExpansionHelper.RestoreTabs(item);                 // 扩容分页必须单独还原
```

### 3.2 Link 阶段：容器编号对齐（最容易踩坑的地方）

```csharp
// 把 rootItem 的图结构按 BFS 展开，跳过"本身就是 GameItem"的节点，其余（容器）从 0 开始编号
static Dictionary<int, GraphNodeStorage> MapInventories(GameItem rootItem)
{
    var map = new Dictionary<int, GraphNodeStorage>();
    var queue = new Queue<GraphNodeStorage>();
    queue.Enqueue(rootItem);
    int idx = 0;
    while (queue.Count > 0)
    {
        var node = queue.Dequeue();
        if (ReferenceEquals(node, rootItem))          // root 自己不算容器，只把子节点入队
        { foreach (var c in node.children) queue.Enqueue(c); continue; }
        if (node is GameItem) continue;               // 子物品本身跳过（它的容器在下一层）
        if (!map.ContainsKey(idx)) map[idx] = node;
        foreach (var c in node.children) queue.Enqueue(c);
        idx++;
    }
    return map;
}

// 挂接：childItems[i] 放进 childItemInventoryNode[i] 号容器
for (int i = 0; i < Math.Min(node.childItems.Count, node.childItemInventoryNode.Count); i++)
    if (_items.TryGetValue(node.childItems[i], out var child)
        && map.TryGetValue(node.childItemInventoryNode[i], out var inv))
        ((GameInventory)inv).UncheckedAccept(child);   // 绕过容量/过滤校验，读档必须用 UncheckedAccept
```

> `UncheckedAccept` 与 `TryAccept` 的区别在这里非常关键：**读档必须用 UncheckedAccept**，因为此时容器的过滤规则/容量还没初始化完，`TryAccept` 会拒收导致物品丢失。

### 3.3 Post 阶段：原版读档收尾钩子清单（极有价值）

原版每个物品读档后要跑的 helper，逐条列出（顺序即源码顺序）：

```csharp
MachineHelper.LoadUpgrade(item);                    // 机器升级/模组数据
SpriteHelper.UpdateWaterContainerSprite(item);      // 液位贴图
GeneralHelper.LoadLockItem(item);                   // 上锁容器
PlantTraitList.LoadEdibleProduce(item);             // 可食用产物
PlantTraitList.LoadTasteFeature(item);              // 风味特性
GeneralHelper.LoadNonOwnedToolTag(item);            // 非本人所有工具
GeneralHelper.LoadImportantStoryItemTag(item);      // 剧情物品
GeneralHelper.LoadHazardousWasteTag(item);          // 危险废料
ScavHelper.LoadScavTokenTag(item);                  // 拾荒凭证
LockHelper.LoadLockedContainer(item);               // 锁定容器
MachineBrokenHelper.LoadBrokenMachine(item);        // 损坏机器
MachineHelper.OnLoad(item);                         // 机器通用收尾
MachineTurboBoosterAdv.LoadTurboBoostEligibleTag(item);
CurrentConverterHelper.OnLoad(item);                // 电流转换器
item.onLoaded?.Invoke(item);                        // 物品自身的读档回调（我们自己的自定义物品也靠这个）
```

**这条清单说明：如果我们自定义了 GameItem 子类/特性，只有在 `onLoaded` 或对应 helper 里恢复自己的私有数据，否则读档会丢。**

### 3.4 Attach 阶段

```csharp
var realTarget = DecodeInterceptor.Target;   // 原版本来要写进去的那个 inventory
var child = _root.contentWindow.child;       // 临时 root 的容器
foreach (var node in child.children.ToList())
    if (node is GameItem gi)
    {
        ((GameInventory)child).Expel(gi);                 // 先从临时容器摘出
        realTarget.UncheckedAccept(gi);                   // 再塞进真容器
        GraphUtils.DestroyIfUnplaced(gi, "load: target inventory refused a saved item"); // 兜底销毁，防幽灵物品
    }
_root.Destroy();
```

`GraphUtils.DestroyIfUnplaced(item, reason)` 是原版提供的兜底工具：**塞不进容器就销毁**，避免留下没有 parentInventory 的幽灵物品。我们做任何"精确投放"失败兜底时应该用同一个方法。

## 4. 其他可复用工程技巧

| 技巧 | 做法 |
| --- | --- |
| JSON 解析不占主线程 | `Task.Run(() => JsonUtility.FromJson<SaveState>(json))`，主线程只轮询 `_parsedState` |
| 加载期禁止输入 | `InputGate.Begin()/End()` + 每帧 `InputGate.Tick()` |
| 进度条要"真进度" | 用 `cursor/total` 分段映射到 0.15~0.75、0.75~0.85、0.85~0.95，而不是假动画 |
| 出错必须回退 | 任何阶段异常 → `DecodeInterceptor.Fallback(reason)` → 走原版一次性加载，宁可卡也不能坏档 |
| 帧预算可观测 | 记录每阶段 `ms / frames / worst gap`，日志里输出 |

## 5. 它揭示的原版过夜管线（Probe 系列 Patch 的副产物）

`ProgressBar` 为了显示"过夜进度"，Patch 了过夜流程里的全部服务，等于给了我们一张**过夜阶段清单**：

```
PlayerStore.EndNight
 ├─ ModHook.FireOnHandlingNightlyServicesEarly / Late   （原版给模组的过夜前后钩子）
 ├─ PlayerStore.HandlePower            （供电结算）
 ├─ PlayerStore.HandleOperation        （机器运行）
 ├─ PlayerStore.HandleJanitorial       （清洁）
 ├─ PlayerStore.HandleInsurance        （保险）
 ├─ PlayerStore.HandleVandalism        （破坏事件）
 ├─ PlayerStore.HandleButcherHunting   （屠宰/狩猎）
 ├─ PlayerStore.HandleOtherServices    （其余服务）
 ├─ ChemicalHelper.RunNightlyChemicalSimulation  （化学模拟，360 tick）
 ├─ HopperHelper.RunWindow             （漏斗窗口）
 ├─ DebrisHelper.OnNight               （垃圾/残骸）
 ├─ SaveManager.Save                   （过夜存档）
 └─ StoreUIManager.FinishBlackscreenSequence（黑屏结束）
```

**做"过夜类"功能的模组应该挂 `ModHook.OnHandlingNightlyServicesEarly/Late`，而不是硬 Patch 上面任何一个方法** —— 顺序有保证、不抢占别人的 Patch。

## 6. 与我们的项目直接相关

- 我们若给自定义物品加"读档后恢复私有状态"，**必须**挂 `item.onLoaded` 或自己写 `LoadXxxTag` 式 helper，并在 `DirectoryMaster.Item` 之后手动补数据。
- 自定义机器若要存"内部进度/液位/温度"，参考 `MachineHelper.LoadUpgrade` 与 `SpriteHelper.UpdateWaterContainerSprite` 的分工：**数据走 MachineHelper，显示走 SpriteHelper**。
- 我们自己的模组如果想要"读档进度条"，照抄四阶段骨架即可（约 400 行），但要注意：它会整体替换原版解码，与其它做读档增强的模组天然冲突（本模组选择"独占 + 出错回退"）。

## 7. 源码里的可疑点（不要照抄）

1. `Create()` 里把 `modifiedState` 也用 `node.itemState.Clone()` 赋值，而不是 `itemStateModified`。若是反编译丢失了字段，可能只是命名不同；但**如果确实是原版写法，就意味着 modifiedState 会被重置成 state** —— 我们读档恢复自定义数据时应自己再赋值一次。
2. `MapInventories` 的编号逻辑依赖"BFS 序恰好等于存档记录的容器序"，这在原版是约定；我们自己要实现时必须用原版同一遍历顺序，否则子物品会挂错容器。
3. 它在 `AttachStep` 里对每个物品 `Expel` 后立刻 `UncheckedAccept`，中间没有做容量校验——若目标容器已满，靠 `DestroyIfUnplaced` 销毁，会**丢物品**。我们自己写时应该先 `CanAccept` 预检。

## 8. 待验证

- `PlayerStore.DecodeSaveItem` 的确切签名与返回值语义（本模组只按 Prefix 拦截并返回 false，未跟进原版后续流程）。
- 上述过夜阶段顺序是我从 Probe Patch 的名称推断的，未真机断点确认。
