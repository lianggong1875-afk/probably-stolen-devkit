// ArcSummonProbe.cs —— 真机「请客上门」探针（本模组的派客操控台）
//
// 为什么有它（2026-11-26）：
//   桥接自带的 enqueue_customer 只认两个**写死**的 preset（CustomerTools.cs:15
//   `{ "appraisal_hand", "aug_med" }`），派不出本模组的 arc_* 客人；
//   而 query_ui 里根本没有开门入口——卷帘是 3D 场景物件，桥接只能点 UI Canvas。
//   于是「来客上门」这条线一直验不动，直到在群友模组找到现成做法：
//     D:\0深空当铺模组开发\深空当铺：可能是群友\evidence\probes\FreyaProbe.cs
//   它的 `summon` 就是 CreateStoreClient(id) → storeClientManager.AddNextClient(c)。
//   这里把那套命令收成本模组可用的最小版本，**放进工具库常驻**，别再每轮重造。
//
// 编译（把 Managed / Bridge 换成你机器上的路径）：
//   $managed = 'D:\steam\steamapps\common\Probably Stolen\Probably Stolen_Data\Managed'
//   $bridge  = 'D:\steam\steamapps\common\Probably Stolen\Mods\ModDevBridgeNext\ModDevBridgeNext.dll'
//   $sdk = (Get-ChildItem 'C:\Program Files\dotnet\sdk' -Directory |
//           Where-Object { Test-Path (Join-Path $_.FullName 'Roslyn\bincore\csc.dll') } |
//           Sort-Object { [version]$_.Name } -Descending | Select-Object -First 1).FullName
//   $refs = @('mscorlib','System','System.Core','UnityEngine.CoreModule','UnityEngine.TextRenderingModule') |
//           ForEach-Object { "/r:$managed\$_.dll" }
//   & dotnet (Join-Path $sdk 'Roslyn\bincore\csc.dll') /nologo /target:library `
//       /out:ArcSummonProbe.dll @refs "/r:$bridge" src\ArcSummonProbe.cs
//
// 用（程序集名要唯一，改名不换身份）：
//   load_probe_module  session_id world_id filename=ArcSummonProbe.dll
//   call_probe         session_id world_id command="who"
//                      command="summon arc_collector"
//
// 命令：
//   who                 列出 StoreClientListDict 里所有注册过的客人 id（先确认 arc_* 在不在）
//   summon <id>         造一个客人，插到队列**最前**（下一个就是它）
//   solocall <id>       清空队列后只放这一个（店里立刻只剩它）
//   gen                 走原版随机派客（GenerateClient）
//   top [n]             读当前队列（默认 10 条）：identifier / displayName / spriteName
//   stackclear          清空队列
//   closeall            拉下卷帘（StoreShutterButton.CloseShutter）
//   openall             拉开卷帘（OpenShutter）——客人要店门开着才会走进来
//   state               读当前柜台客人与队列长度（current / queue / shutter）
//   refuse <itemId>      问当前柜台客人收不收这件货（造游离实例判定，不往柜台放）
//                        例：refuse gd_p_brokenring / refuse gd_g_brokenring / refuse hw_gpu_mk2_refurb
//   ring                摇铃叫客（StoreBell.OnClick）—— 队列里的人要靠这一下才走到柜台
//   bg [on|off]         设 Application.runInBackground —— **后台也让游戏跑循环**，不用抢焦点
//                       客人不进场十有八九是因为这个默认 false
//
// ⚠ 这是**改动游戏状态**的探针（造客、清队、关店）。只在测试档里用。
using System.Collections;
using System.IO;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace ArcadeProbe
{
    public sealed class ArcSummonProbe : ModDevBridge.IDevModule
    {
        public string Start()
        {
            return "ArcSummonProbe ready: who | summon <id> | solocall <id> | gen | top [n] | stackclear | closeall";
        }

        public string Execute(string command)
        {
            string cmd = (command ?? "").Trim();
            if (cmd.Length == 0) return "empty command; try who";
            string[] parts = cmd.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string verb = parts[0].ToLowerInvariant();
            string arg = parts.Length > 1 ? parts[1] : null;

            try
            {
                switch (verb)
                {
                    case "who": return Who();
                    case "summon": return Summon(arg, false);
                    case "solocall": return Summon(arg, true);
                    case "gen": return Gen();
                    case "top": return Top(arg);
                    case "stackclear": return StackClear();
                    case "closeall": return CloseAll();
                    case "openall": return OpenAll();
                    case "state": return State();
                    case "bg": return Background(arg);
                    case "time": return TimeState();
                    case "ring": return Ring();
                    case "refuse": return Refuse(arg);
                    case "expose": return Expose(arg);
                    case "confront": return Confront(arg);
                    case "size": return Size(arg);
                    case "label": return Label(arg);
                    case "closelabel": return CloseLabel();
                    case "typename": return TypeName(arg);
                    case "spoof": return Spoof(arg);
                    case "confrontline": return ConfrontLine(arg);
                    case "fits": return Fits(parts);
                    case "tags": return Tags(arg);
                    case "showcase": return Showcase(arg);
                    case "feed": return Feed(parts);
                    case "inside": return Inside(arg);
                    case "place": return Place(arg);
                    case "shot": return Shot(arg);
                    case "feedlive": return FeedLive(parts);
                    case "minspect": return MInspect(arg);
                    case "animcheck": return AnimCheck(arg);
                    case "animbattery": return AnimBattery(parts);
                    case "inspectitem": return InspectItem(arg);
                    case "closeinspect": return CloseInspect();
                    case "refurbtest": return RefurbTest();
                    case "repairtest": return RepairTest();
                    case "showcasetest": return ShowcaseTest();
                    case "piracytest": return PiracyTest();
                    case "markettest": return MarketTest();
                    case "inteltest": return IntelTest();
                    case "sleep": return SleepOneDay();
                    case "news": return NewsCmd(arg);
                    default: return "unknown command '" + verb + "'; try who";
                }
            }
            catch (Exception e)
            {
                return "ERR " + verb + ": " + e.GetType().Name + ": " + e.Message;
            }
        }

        public void Stop() { }

        // ---------------------------------------------------------------- 命令

        private static string Who()
        {
            Dictionary<string, Func<StoreClient>> dict = StoreClientListDict.storeClientDict;
            if (dict == null) return "storeClientDict is null";
            List<string> ids = new List<string>(dict.Keys);
            ids.Sort(StringComparer.Ordinal);
            StringBuilder sb = new StringBuilder();
            sb.Append("registered=").Append(ids.Count).Append('\n');
            for (int i = 0; i < ids.Count; i++)
            {
                bool ours = ids[i].StartsWith("arc_", StringComparison.Ordinal);
                sb.Append(ours ? " * " : "   ").Append(ids[i]).Append('\n');
            }
            sb.Append("( * = this mod's own clients )");
            return sb.ToString();
        }

        private static string Summon(string id, bool solo)
        {
            if (string.IsNullOrEmpty(id)) return "usage: summon <id> (see 'who')";
            StoreClient c = null;
            try { c = StoreClientListDict.CreateStoreClient(id); }
            catch (Exception e) { return "CreateStoreClient(" + id + ") threw " + e.GetType().Name + ": " + e.Message; }
            if (c == null) return "CreateStoreClient(" + id + ") returned null — id not registered?";

            StoreClientManager mgr = PlayerStore.Instance.storeClientManager;
            if (mgr == null) return "storeClientManager is null";

            if (solo)
            {
                int before = mgr.clientStack == null ? 0 : mgr.clientStack.Count;
                if (mgr.clientStack != null) mgr.clientStack.Clear();
                mgr.clientStack.Add(c);
                return "solocall " + id + " (cleared " + before + ") -> " + Describe(c);
            }
            mgr.AddNextClient(c);
            return "summon " + id + " -> " + Describe(c);
        }

        private static string Gen()
        {
            StoreClientManager mgr = PlayerStore.Instance.storeClientManager;
            if (mgr == null) return "storeClientManager is null";
            mgr.GenerateClient();
            return "GenerateClient() ok; queue=" + (mgr.clientStack == null ? -1 : mgr.clientStack.Count);
        }

        private static string Top(string arg)
        {
            int n = 10;
            if (!string.IsNullOrEmpty(arg)) int.TryParse(arg, out n);
            StoreClientManager mgr = PlayerStore.Instance.storeClientManager;
            if (mgr == null || mgr.clientStack == null) return "queue unavailable";
            StringBuilder sb = new StringBuilder();
            sb.Append("queue=").Append(mgr.clientStack.Count).Append('\n');
            int lim = Math.Min(n, mgr.clientStack.Count);
            for (int i = 0; i < lim; i++) sb.Append('[').Append(i).Append("] ").Append(Describe(mgr.clientStack[i])).Append('\n');
            return sb.ToString();
        }

        private static string StackClear()
        {
            StoreClientManager mgr = PlayerStore.Instance.storeClientManager;
            if (mgr == null || mgr.clientStack == null) return "queue unavailable";
            int n = mgr.clientStack.Count;
            mgr.clientStack.Clear();
            return "cleared " + n + " queued client(s)";
        }

        /// <summary>
        /// 打开/关闭后台运行。这是解决「客人在后台不进场」的正路，而且**不用抢焦点**：
        /// Unity 默认 runInBackground = false，窗口失焦就停循环，客人永远走不到柜台。
        /// </summary>
        /// <summary>最近一次 `place` 放进店里的物品，供 `feed here …` 用。</summary>
        private static GameItem _lastPlaced;

        /// <summary>
        /// 把一件造出来的货**真正放进店里**（`GraphUtils.EmporiumTryAdd(item)` 单参版走
        /// `EmporiumEntry.Instance.invElement`）。这一步很关键：
        /// 只 spawn 不放进世界的机器是**游离**的 —— 它的容器 identifier 全为空、
        /// `EmporiumTryAdd` 会 fallback 到背包、进度也不会跑（068 实证）。
        /// 放进店里之后它就变成世界物品，后续才能装料、跑进度。
        /// </summary>
        private static string Place(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return "usage: place <itemId>";
            GameItem item = null;
            try { item = ItemSpawner.Spawn(itemId); }
            catch (Exception e) { return "spawn threw " + e.GetType().Name; }
            if (item == null) return "spawn returned null for " + itemId;
            try { GraphUtils.EmporiumTryAdd(item); }
            catch (Exception e) { return "EmporiumTryAdd threw " + e.GetType().Name + ": " + e.Message; }
            _lastPlaced = item;
            StringBuilder sb = new StringBuilder();
            sb.Append("placed=").Append(itemId);
            sb.Append(" parent=").Append(item.parentInventory == null ? "none" : item.parentInventory.identifier);
            try
            {
                List<GameInventory> invs = GraphUtils.FindChildrenType<GameInventory>(item);
                sb.Append(" invs=").Append(invs == null ? -1 : invs.Count);
                if (invs != null) for (int i = 0; i < invs.Count && i < 8; i++)
                    sb.Append(' ').Append(invs[i] == null ? "<null>" : ("[" + invs[i].identifier + ":" + (invs[i].childItems == null ? -1 : invs[i].childItems.Count) + "]"));
            }
            catch { }
            return sb.ToString();
        }

        /// <summary>
        /// 把一件货塞进某台机器的容器里。`GraphUtils.FindChildrenType&lt;GameInventory&gt;(machine)` 拿到它的
        /// 所有容器，逐个 `EmporiumTryAdd`；放成功后 `GameItem.parentInventory` 会被赋值，以此判定。
        /// 用法：feed &lt;machineId&gt; &lt;itemId&gt; [itemId...]
        /// </summary>
        private static string Feed(string[] parts)
        {
            if (parts == null || parts.Length < 3) return "usage: feed <machineId> <itemId> [itemId...]";
            GameItem machine = null;
            if (parts[1] == "here") machine = _lastPlaced;
            else try { machine = ItemSpawner.Spawn(parts[1]); }
            catch (Exception e) { return "spawn(" + parts[1] + ") threw " + e.GetType().Name; }
            if (machine == null) return "cannot spawn " + parts[1];

            List<GameInventory> invs = null;
            try { invs = GraphUtils.FindChildrenType<GameInventory>(machine); }
            catch (Exception e) { return "FindChildrenType threw " + e.GetType().Name; }
            if (invs == null || invs.Count == 0) return "machine=" + parts[1] + " has no GameInventory child";

            StringBuilder sb = new StringBuilder();
            sb.Append("machine=").Append(parts[1]).Append(" inventories=").Append(invs.Count);
            sb.Append("\n  inv ids:");
            for (int k = 0; k < invs.Count; k++)
            {
                GameInventory inv = invs[k];
                if (inv == null) { sb.Append(" <null>"); continue; }
                sb.Append(' ').Append(inv.identifier).Append('(').Append(inv.childItems == null ? -1 : inv.childItems.Count).Append(')');
            }
            sb.Append("\n  inv ids:");
            for (int k = 0; k < invs.Count; k++)
            {
                GameInventory inv = invs[k];
                if (inv == null) { sb.Append(" <null>"); continue; }
                sb.Append(' ').Append(inv.identifier).Append('(').Append(inv.childItems == null ? -1 : inv.childItems.Count).Append(')');
            }
            for (int i = 2; i < parts.Length; i++)
            {
                GameItem it = null;
                try { it = ItemSpawner.Spawn(parts[i]); } catch { }
                if (it == null) { sb.Append("\n  ").Append(parts[i]).Append(" spawn=null"); continue; }
                for (int k = 0; k < invs.Count; k++)
                {
                    GameInventory inv = invs[k];
                    if (inv == null) continue;
                    // backInv 是背包类大容器，来者不拒，会把它吞掉却不进机器的格子
                    if (!string.IsNullOrEmpty(inv.identifier)
                        && inv.identifier.IndexOf("backInv", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    try { GraphUtils.EmporiumTryAdd(inv, it); } catch { }
                    if (it.parentInventory != null) break;
                }
                sb.Append("\n  ").Append(parts[i]).Append(" placed=").Append(it.parentInventory != null);
                if (it.parentInventory != null)
                {
                    sb.Append(" in=").Append(it.parentInventory.identifier);
                }
            }
            return sb.ToString();
        }

        /// <summary>列出某台机器/容器里现有的货（`FindChildrenType&lt;GameItem&gt;`）。</summary>
        private static string Inside(string machineId)
        {
            if (string.IsNullOrEmpty(machineId)) return "usage: inside <machineId>";
            GameItem machine = null;
            try { machine = ItemSpawner.Spawn(machineId); } catch { }
            if (machine == null) return "cannot spawn " + machineId;
            List<GameItem> kids = null;
            try { kids = GraphUtils.FindChildrenType<GameItem>(machine); } catch { }
            if (kids == null) return "machine=" + machineId + " FindChildrenType=null";
            StringBuilder sb = new StringBuilder();
            sb.Append("machine=").Append(machineId).Append(" contains=").Append(kids.Count);
            for (int i = 0; i < kids.Count && i < 20; i++)
            {
                GameItem k = kids[i];
                if (k == null) continue;
                sb.Append("\n  [").Append(i).Append("] ").Append(k.identifier).Append(" name=").Append(k.name);
            }
            return sb.ToString();
        }

        /// <summary>
        /// 验展柜派客。`ShowcaseHelper` 的三个入口都是 public：
        ///   · `HasMerchandise()` 展柜里有没有货；
        ///   · `GetDisplayCaseType()` 展柜里有哪些类型；
        ///   · `CreateClientByStockType(type)` 「类型 → 来客 id」映射。
        /// 本模组对后者的内部调用点做了缝，所以重点看 `ARC_GAME_GOODS` 会不会映射到本模组的来客。
        /// 无参打全部；带参只打那一个类型的映射。
        /// </summary>
        private static string Showcase(string arg)
        {
            StringBuilder sb = new StringBuilder();
            if (!string.IsNullOrEmpty(arg))
            {
                try { sb.Append("map(").Append(arg).Append(")=[").Append(ShowcaseHelper.CreateClientByStockType(arg)).Append(']'); }
                catch (Exception e) { sb.Append("map ERR ").Append(e.GetType().Name).Append(": ").Append(e.Message); }
                return sb.ToString();
            }
            try { sb.Append("hasMerchandise=").Append(ShowcaseHelper.HasMerchandise()); }
            catch (Exception e) { sb.Append("hasMerchandise=ERR ").Append(e.GetType().Name); }
            try
            {
                List<string> types = ShowcaseHelper.GetDisplayCaseType();
                sb.Append("\n  types=[");
                if (types == null) sb.Append("<null>");
                else for (int i = 0; i < types.Count; i++) { if (i > 0) sb.Append(", "); sb.Append(types[i]); }
                sb.Append(']');
            }
            catch (Exception e) { sb.Append("\n  types=ERR ").Append(e.GetType().Name); }
            foreach (string t in new string[] { "ARC_GAME_GOODS", "ARC_HARDWARE_GOODS", "LUXURY_ITEM", "TOOL" })
            {
                try { sb.Append("\n  map(").Append(t).Append(")=[").Append(ShowcaseHelper.CreateClientByStockType(t)).Append(']'); }
                catch (Exception e) { sb.Append("\n  map(").Append(t).Append(")=ERR ").Append(e.GetType().Name); }
            }
            return sb.ToString();
        }

        /// <summary>本模组与原版常用标注的候选清单。`GameItem` 没有「列出全部 tag」的公开接口，
        /// 只能按名字问 `IsTag`，所以内置这份清单逐条问。</summary>
        private static readonly string[] KnownTags =
        {
            "ARC_BURNER_TAG", "ARC_BURN_CHIP_TAG", "ARC_HW_V",
            "ARC_GAME_GOODS", "ARC_HARDWARE_GOODS", "GAME_DISC_TAG", "GAME_HARDWARE_TAG",
            "GAME_GPU_TAG", "GAME_RAM_TAG", "GAME_CONSOLE_TAG",
            "CONTAINER_TAG", "STANDARD_MACHINE_TAG", "PROGRESS_ITEM_TAG",
            "MACHINE_PROGRESS_TARGET_TAG", "NOT_FOR_RESALE", "CONTRABAND_ITEM_TAG", "IMPORTANT_TAG"
        };

        /// <summary>
        /// 打一件货身上命中的已知标注，顺带打它的 itemTypes（`GetGameItemType`）。
        /// 用来验「刻录机带 ARC_BURNER_TAG、芯片带 ARC_BURN_CHIP_TAG、盘带 GAME_DISC_TAG」这类品类事实。
        /// </summary>
        private static string Tags(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return "usage: tags <itemId>  e.g. tags arc_burner";
            GameItem item = null;
            try { item = ItemSpawner.Spawn(itemId); }
            catch (Exception e) { return "spawn threw " + e.GetType().Name; }
            if (item == null) return "spawn returned null for " + itemId;

            StringBuilder sb = new StringBuilder();
            sb.Append("item=").Append(itemId);
            try
            {
                List<string> types = item.GetGameItemType();
                sb.Append("\n  types=[");
                if (types != null) for (int i = 0; i < types.Count; i++) { if (i > 0) sb.Append(", "); sb.Append(types[i]); }
                sb.Append(']');
            }
            catch { }
            sb.Append("\n  tags:");
            int hit = 0;
            for (int i = 0; i < KnownTags.Length; i++)
            {
                bool has = false;
                try { has = item.IsTag(KnownTags[i]); } catch { continue; }
                if (!has) continue;
                sb.Append(' ').Append(KnownTags[i]);
                hit++;
            }
            if (hit == 0) sb.Append(" (none of the known ones)");
            // 对照组：一个确定不存在的 tag 必须是 false，否则说明 IsTag 本身不可信
            bool ctrl = false;
            try { ctrl = item.IsTag("NO_SUCH_TAG_XYZ"); } catch { ctrl = true; }
            sb.Append("\n  control NO_SUCH_TAG_XYZ=").Append(ctrl);
            return sb.ToString();
        }

        /// <summary>
        /// 问「这件货装不装得进这个容器」。走原版 `PixelWindow.MayHaveValidInventorySlot(item)`
        /// 与 `TryFindOneValidInventorySlot(item)` —— 不需要把 GameInventory 抠出来。
        /// 用法：fits &lt;boxId&gt; &lt;itemId&gt;…（可一次问多件，逐件打一行）
        /// </summary>
        private static string Fits(string[] parts)
        {
            if (parts == null || parts.Length < 3)
                return "usage: fits <boxId> <itemId> [itemId...]  e.g. fits arc_disc_case gd_g_brokenring hw_console";
            string boxId = parts[1];
            GameItem box = null;
            try { box = ItemSpawner.Spawn(boxId); }
            catch (Exception e) { return "spawn(" + boxId + ") threw " + e.GetType().Name; }
            if (box == null) return "cannot spawn " + boxId;
            PixelWindow w = box.contentWindow;
            if (w == null) return "box=" + boxId + " contentWindow=null (not a container)";

            StringBuilder sb = new StringBuilder();
            sb.Append("box=").Append(boxId).Append("  window=").Append(w.GetType().Name);
            for (int i = 2; i < parts.Length; i++)
            {
                string id = parts[i];
                sb.Append("\n  ").Append(id);
                GameItem it = null;
                try { it = ItemSpawner.Spawn(id); }
                catch (Exception e) { sb.Append("  spawn ERR ").Append(e.GetType().Name); continue; }
                if (it == null) { sb.Append("  spawn=null"); continue; }
                bool may = false;
                try { may = w.MayHaveValidInventorySlot(it); } catch (Exception e) { sb.Append("  may ERR ").Append(e.GetType().Name); continue; }
                SlotMarker slot = null;
                try { slot = w.TryFindOneValidInventorySlot(it); } catch { }
                sb.Append("  mayFit=").Append(may).Append(" slot=").Append(slot == null ? "none" : "found");
            }
            return sb.ToString();
        }

        /// <summary>
        /// 取**对峙台词原文**。造一件盗版贴正版标的货，直接调 `ItemFeature.CreateCustomerConfrontDialog()`
        /// —— 本模组对它有 Postfix，按 category + 真实条件换成本模组自己那句。
        /// 这是 057 里「对话节点已翻页、没读到原文」那个缺口的补法：不弹对话，直接拿字符串。
        /// </summary>
        private static string ConfrontLine(string arg)
        {
            string genuineId = string.IsNullOrEmpty(arg) ? "gd_g_brokenring" : arg;
            if (genuineId.IndexOf("gd_g_", StringComparison.Ordinal) != 0) return "usage: confrontline [gd_g_<title>]";
            string pirateId = "gd_p_" + genuineId.Substring(5);

            GameItem genuine = null, pirate = null;
            try { genuine = ItemSpawner.Spawn(genuineId); pirate = ItemSpawner.Spawn(pirateId); }
            catch (Exception e) { return "spawn threw " + e.GetType().Name; }
            if (genuine == null) return "cannot spawn " + genuineId;
            if (pirate == null) return "cannot spawn " + pirateId;

            ItemFeature gf = null, pf = null;
            try
            {
                gf = genuine.FindItemFeatureByCategory("CATEGORY_GAME_LICENSE");
                pf = pirate.FindItemFeatureByCategory("CATEGORY_GAME_LICENSE");
            }
            catch (Exception e) { return "FindItemFeatureByCategory threw " + e.GetType().Name; }
            if (gf == null || gf.fakeCondition == null) return "no genuine license on " + genuineId;
            if (pf == null || pf.realCondition == null) return "no pirate license on " + pirateId;
            try { pf.SetConditions(gf.fakeCondition, pf.realCondition); }
            catch (Exception e) { return "SetConditions threw " + e.GetType().Name; }

            Dialogue dlg = null;
            try { dlg = pf.CreateCustomerConfrontDialog(); }
            catch (Exception e) { return "CreateCustomerConfrontDialog threw " + e.GetType().Name + ": " + e.Message; }
            if (dlg == null) return "CreateCustomerConfrontDialog returned null (real=" + pf.realCondition.identifier + ")";

            StringBuilder sb = new StringBuilder();
            sb.Append("item=").Append(pirateId).Append(" real=").Append(pf.realCondition.identifier);
            sb.Append("\n  title=[").Append(dlg.title).Append(']');
            sb.Append("\n  plainText=[").Append(dlg.plainText).Append(']');
            try { sb.Append("\n  text=[").Append(dlg.text == null ? "<null>" : dlg.text.ToString()).Append(']'); }
            catch (Exception e) { sb.Append("\n  text=ERR ").Append(e.GetType().Name); }

            // 对照：硬件翻新件走的是另一句
            try
            {
                GameItem hwNew = ItemSpawner.Spawn("hw_gpu_mk2");
                GameItem hwRef = ItemSpawner.Spawn("hw_gpu_mk2" + "_refurb");
                if (hwNew != null && hwRef != null)
                {
                    ItemFeature nf = hwNew.FindItemFeatureByCategory("CATEGORY_HW_CONDITION");
                    ItemFeature rf = hwRef.FindItemFeatureByCategory("CATEGORY_HW_CONDITION");
                    if (nf != null && rf != null && nf.fakeCondition != null && rf.realCondition != null)
                    {
                        rf.SetConditions(nf.fakeCondition, rf.realCondition);
                        Dialogue hd = rf.CreateCustomerConfrontDialog();
                        sb.Append("\n  hw real=").Append(rf.realCondition.identifier)
                          .Append(" line=[").Append(hd == null ? "<null>" : hd.plainText).Append(']');
                    }
                }
            }
            catch (Exception e) { sb.Append("\n  hw ERR ").Append(e.GetType().Name); }
            return sb.ToString();
        }

        /// <summary>
        /// 造一件**盗版贴正版标**的货，看伪装层会不会泄露真实值。
        /// 做法不依赖任何 id 字符串：从正版盘上取下「正版条件对象」，塞进盗版盘的 fakeCondition，
        /// 于是 real=盗版、fake=正版，`IsFeatureMatch()` 为假、`WillClientExposeFeature()` 为真。
        /// 然后打 `GetPublicDisplay()`（伪装层，**绝不能**泄露真实）与 `GetExposedString()`（曝光后才给真实）。
        /// </summary>
        private static string Spoof(string arg)
        {
            string genuineId = string.IsNullOrEmpty(arg) ? "gd_g_brokenring" : arg;
            if (genuineId.IndexOf("gd_g_", StringComparison.Ordinal) != 0) return "usage: spoof [gd_g_<title>]  default gd_g_brokenring";
            string pirateId = "gd_p_" + genuineId.Substring(5);

            GameItem genuine = null, pirate = null;
            try { genuine = ItemSpawner.Spawn(genuineId); pirate = ItemSpawner.Spawn(pirateId); }
            catch (Exception e) { return "spawn threw " + e.GetType().Name; }
            if (genuine == null) return "cannot spawn " + genuineId;
            if (pirate == null) return "cannot spawn " + pirateId;

            ItemFeature gf = null, pf = null;
            try
            {
                gf = genuine.FindItemFeatureByCategory("CATEGORY_GAME_LICENSE");
                pf = pirate.FindItemFeatureByCategory("CATEGORY_GAME_LICENSE");
            }
            catch (Exception e) { return "FindItemFeatureByCategory threw " + e.GetType().Name; }
            if (gf == null || gf.fakeCondition == null) return "no genuine license condition on " + genuineId;
            if (pf == null || pf.realCondition == null) return "no pirate license condition on " + pirateId;

            try { pf.SetConditions(gf.fakeCondition, pf.realCondition); }
            catch (Exception e) { return "SetConditions threw " + e.GetType().Name + ": " + e.Message; }

            StringBuilder sb = new StringBuilder();
            sb.Append("item=").Append(pirateId);
            sb.Append("\n  real=").Append(pf.realCondition.identifier);
            sb.Append(" fake=").Append(pf.fakeCondition.identifier);
            try { sb.Append("\n  IsFeatureMatch=").Append(pf.IsFeatureMatch()); } catch { }
            try { sb.Append(" WillClientExpose=").Append(pf.WillClientExposeFeature()); } catch { }
            try { sb.Append("\n  public=[").Append(pf.GetPublicDisplay()).Append(']'); } catch (Exception e) { sb.Append("\n  public=ERR ").Append(e.GetType().Name); }
            try { sb.Append("\n  exposed=[").Append(pf.GetExposedString()).Append(']'); } catch (Exception e) { sb.Append("\n  exposed=ERR ").Append(e.GetType().Name); }
            try { sb.Append("\n  realDisp=[").Append(pf.GetBaseRealDisplay()).Append(']'); } catch { }
            return sb.ToString();
        }

        /// <summary>
        /// 打原版 `TypeHelper.GetTypeDisplayName(typeConst)` —— 本模组对它有 Harmony Prefix 短路。
        /// 传本模组的专类常量（`ARC_GAME_GOODS` / `ARC_HARDWARE_GOODS`）应返回「游戏」/「硬件」；
        /// 传原版常量应照常返回原版名字（证明我们没把别人的名字搞坏）。
        /// </summary>
        private static string TypeName(string arg)
        {
            if (string.IsNullOrEmpty(arg)) return "usage: typename <typeConst>  e.g. ARC_GAME_GOODS";
            string r = null;
            try { r = TypeHelper.GetTypeDisplayName(arg); }
            catch (Exception e) { return "GetTypeDisplayName(" + arg + ") threw " + e.GetType().Name + ": " + e.Message; }
            return "const=" + arg + " => [" + r + "] len=" + (r == null ? -1 : r.Length);
        }

        /// <summary>
        /// 打开原版标签打印机面板（`LabelerUIManager.Instance.OpenUI(item)`）。
        /// 打开后用 `query_ui` 读面板里的行，确认本模组的「版权标识」等行真的出现在原生面板上。
        /// 不替它点任何 Dropdown —— 只读，不改条件。
        /// </summary>
        private static string Label(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return "usage: label <itemId>";
            GameItem item = null;
            try { item = ItemSpawner.Spawn(itemId); }
            catch (Exception e) { return "spawn threw " + e.GetType().Name; }
            if (item == null) return "spawn returned null for " + itemId;
            LabelerUIManager mgr = LabelerUIManager.Instance;
            if (mgr == null) return "LabelerUIManager.Instance is null";
            mgr.OpenUI(item);
            return "OpenUI(" + itemId + ") ok";
        }

        private static string CloseLabel()
        {
            LabelerUIManager mgr = LabelerUIManager.Instance;
            if (mgr == null) return "LabelerUIManager.Instance is null";
            mgr.CloseUI();
            return "CloseUI() ok";
        }

        /// <summary>
        /// 读一件货的**容器窗口尺寸**（`GameItem.contentWindow` → `PixelWindow`）。
        /// 用来实证「光盘收纳盒是 6×6 而不是旧版 12×12」这类改动有没有真装进游戏。
        /// 属性名不猜，用反射把 PixelWindow 上所有 int 属性全打出来。
        /// </summary>
        private static string Size(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return "usage: size <itemId>  e.g. size arc_disc_case";
            GameItem item = null;
            try { item = ItemSpawner.Spawn(itemId); }
            catch (Exception e) { return "spawn threw " + e.GetType().Name; }
            if (item == null) return "spawn returned null for " + itemId;

            PixelWindow w = item.contentWindow;
            if (w == null) return "item=" + itemId + " contentWindow=null (not a container?)";

            StringBuilder sb = new StringBuilder();
            sb.Append("item=").Append(itemId).Append(" window=").Append(w.GetType().Name);
            System.Reflection.PropertyInfo[] ps = w.GetType().GetProperties(
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            for (int i = 0; i < ps.Length; i++)
            {
                if (ps[i].PropertyType != typeof(int)) continue;
                object v = null;
                try { v = ps[i].GetValue(w, null); } catch { continue; }
                sb.Append(' ').Append(ps[i].Name).Append('=').Append(v);
            }
            return sb.ToString();
        }

        /// <summary>
        /// **真触发一次当场对峙**：把货交给客人自己的 `ClientExposeFeature(item)`，
        /// 原作会在这里弹对峙对话（台词走本模组 `ArcClientExpose` 的 Postfix）、
        /// 扣声望 -4、并把 feature 标记成已曝光。返回每个 feature 曝光前后的 IsExposed()，
        /// 对话文本用 `query_ui` 另读。
        /// </summary>
        private static string Confront(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return "usage: confront <itemId>";
            GameItem item = null;
            try { item = ItemSpawner.Spawn(itemId); }
            catch (Exception e) { return "ItemSpawner.Spawn threw " + e.GetType().Name; }
            if (item == null) return "spawn returned null for " + itemId;

            StoreClient cur = null;
            try
            {
                StoreClientInstance inst = PlayerStore.Instance.currentClientInstance;
                if (inst != null) cur = inst.GetClientBlueprint();
            }
            catch { }
            if (cur == null) return "no client at the counter (openall -> summon <id> -> ring first)";

            List<ItemFeature> fs = item.itemFeatures;
            StringBuilder sb = new StringBuilder();
            sb.Append("client=").Append(cur.identifier).Append(" item=").Append(itemId);
            sb.Append("\n  before:");
            if (fs != null) for (int i = 0; i < fs.Count; i++) sb.Append(' ').Append(fs[i] != null && fs[i].IsExposed());

            try { cur.ClientExposeFeature(item); }
            catch (Exception e) { sb.Append("\n  CONFRONT ERR ").Append(e.GetType().Name).Append(": ").Append(e.Message); }

            sb.Append("\n  after :");
            if (fs != null) for (int i = 0; i < fs.Count; i++) sb.Append(' ').Append(fs[i] != null && fs[i].IsExposed());
            return sb.ToString();
        }

        /// <summary>
        /// 问「当前这位客人**有没有本事**看出这件货的猫腻」。逐条 feature 调
        /// `canClientExposeFeature(feature, client)` —— 这正是四档眼力的判定位，
        /// 收藏家（Trained）对 `CATEGORY_GAME_LICENSE` 该是 true，游客（Blunt）该是 false。
        /// 同时打 `WillClientExposeFeature()`（真假是否不一致，即「值不值得识破」），
        /// 两者都真才会真识破。
        /// </summary>
        private static string Expose(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return "usage: expose <itemId>";
            GameItem item = null;
            try { item = ItemSpawner.Spawn(itemId); }
            catch (Exception e) { return "ItemSpawner.Spawn threw " + e.GetType().Name; }
            if (item == null) return "spawn returned null for " + itemId;

            StoreClient cur = null;
            try
            {
                StoreClientInstance inst = PlayerStore.Instance.currentClientInstance;
                if (inst != null) cur = inst.GetClientBlueprint();
            }
            catch { }
            if (cur == null) return "no client at the counter (openall -> summon <id> -> ring first)";

            StringBuilder sb = new StringBuilder();
            sb.Append("client=").Append(cur.identifier).Append(" item=").Append(itemId);
            List<ItemFeature> fs = item.itemFeatures;
            if (fs != null)
            {
                for (int i = 0; i < fs.Count; i++)
                {
                    ItemFeature f = fs[i];
                    if (f == null) continue;
                    bool can = false;
                    try { can = cur.canClientExposeFeature != null && cur.canClientExposeFeature(f, cur); }
                    catch (Exception e) { sb.Append("\n  ERR ").Append(e.GetType().Name); continue; }
                    bool will = false;
                    try { will = f.WillClientExposeFeature(); } catch { }
                    sb.Append("\n  ").Append(f.category).Append(" / ").Append(f.identifier)
                      .Append("  canExpose=").Append(can)
                      .Append(" willExpose=").Append(will);
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// 问「当前柜台这位客人收不收这件货」。造一个**游离**的物品实例做判定，不往柜台放，
        /// 所以不会污染交易台。走的是原作 `IsClientBuyingThisItem` → 第一条就是
        /// `IsClientRefusingItem`（一票否决），正是禁收的正确验法。
        /// </summary>
        private static string Refuse(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return "usage: refuse <itemId>  e.g. gd_p_brokenring / gd_g_brokenring";
            GameItem item = null;
            try { item = ItemSpawner.Spawn(itemId); }
            catch (Exception e) { return "ItemSpawner.Spawn(" + itemId + ") threw " + e.GetType().Name + ": " + e.Message; }
            if (item == null) return "ItemSpawner.Spawn(" + itemId + ") returned null";

            StoreClient cur = null;
            try
            {
                StoreClientInstance inst = PlayerStore.Instance.currentClientInstance;
                if (inst != null) cur = inst.GetClientBlueprint();
            }
            catch { }
            if (cur == null) return "no client at the counter (do openall -> summon <id> -> ring first)";

            StringBuilder sb = new StringBuilder();
            sb.Append("client=").Append(cur.identifier);
            sb.Append(" item=").Append(itemId);
            try { sb.Append(" refusing=").Append(cur.IsClientRefusingItem(item)); }
            catch (Exception e) { sb.Append(" refusing=ERR ").Append(e.GetType().Name); }
            try { sb.Append(" buying=").Append(cur.IsClientBuyingThisItem(item)); }
            catch (Exception e) { sb.Append(" buying=ERR ").Append(e.GetType().Name); }
            return sb.ToString();
        }

        /// <summary>
        /// 摇铃叫客（`StoreBell.OnClick()` → `RingBell()`）。**这才是让队列里的客人走到柜台的入口**：
        /// 光 openall + summon 只会排一队人，没人叫就一直站着不走。
        /// </summary>
        private static string Ring()
        {
            StoreBell bell = UnityEngine.Object.FindObjectOfType<StoreBell>();
            if (bell == null) return "no StoreBell in scene (store not open?)";
            bell.OnClick();
            return "StoreBell.OnClick() ok";
        }

        /// <summary>读时钟：timeScale 是否为 0、帧号是否在涨 —— 用来区分「游戏停了」和「状态机没触发」。</summary>
        private static string TimeState()
        {
            return "timeScale=" + Time.timeScale
                 + " frame=" + Time.frameCount
                 + " time=" + Time.time.ToString("F2")
                 + " realtime=" + Time.realtimeSinceStartup.ToString("F1");
        }

        private static string Background(string arg)
        {
            bool on = !(arg != null && (arg == "off" || arg == "0" || arg.Equals("false", StringComparison.OrdinalIgnoreCase)));
            Application.runInBackground = on;
            return "runInBackground=" + Application.runInBackground;
        }

        private static string State()
        {
            StringBuilder sb = new StringBuilder();
            try
            {
                StoreClientInstance inst = PlayerStore.Instance.currentClientInstance;
                if (inst == null) sb.Append("current=none");
                else
                {
                    StoreClient bp = null;
                    try { bp = inst.GetClientBlueprint(); } catch { }
                    sb.Append("current=").Append(bp == null ? "<null blueprint>" : Describe(bp));
                }
            }
            catch (Exception e) { sb.Append("current=ERR ").Append(e.GetType().Name); }
            try
            {
                StoreClientManager mgr = PlayerStore.Instance.storeClientManager;
                sb.Append(" | queue=").Append(mgr == null || mgr.clientStack == null ? -1 : mgr.clientStack.Count);
            }
            catch (Exception e) { sb.Append(" | queue=ERR ").Append(e.GetType().Name); }
            try
            {
                StoreShutterButton sh = StoreShutterButton.Instance;
                sb.Append(" | shutter=").Append(sh == null ? "null" : "present");
            }
            catch { sb.Append(" | shutter=?"); }
            return sb.ToString();
        }

        /// <summary>
        /// In-process frame capture. Machine slots and inventory cells are
        /// PixelWindow/PixelElement self-drawn, so query_ui cannot see them and operate_ui
        /// cannot click them - real-mouse work has to start from a picture. ScreenCapture
        /// only yields a frame at end-of-frame, so this follows the vanilla pattern
        /// (PaperEditorFiles.ScreenshotRoutine): host a coroutine on a live MonoBehaviour,
        /// yield WaitForEndOfFrame, then CaptureScreenshotAsTexture. Landing dir is
        /// Application.persistentDataPath. Usage: shot [name.png]
        /// </summary>
        private static string Shot(string arg)
        {
            string name = arg;
            if (string.IsNullOrEmpty(name))
                name = "arc-shot-" + DateTime.Now.ToString("HHmmss") + ".png";
            if (!name.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) name += ".png";
            MonoBehaviour host = null;
            try { StoreShutterButton shutter = StoreShutterButton.Instance; if (shutter != null) host = shutter; } catch { }
            if (host == null)
            {
                MonoBehaviour[] all = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>();
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i] != null && all[i].isActiveAndEnabled) { host = all[i]; break; }
                }
            }
            if (host == null) return "shot: no MonoBehaviour available to host the coroutine";
            host.StartCoroutine(ShotRoutine(name));
            return "queued=" + name + " host=" + host.GetType().Name + " dir=" + Application.persistentDataPath;
        }

        private static IEnumerator ShotRoutine(string name)
        {
            yield return new WaitForEndOfFrame();
            Texture2D tex = ScreenCapture.CaptureScreenshotAsTexture();
            if (tex == null) yield break;
            byte[] png = tex.EncodeToPNG();
            UnityEngine.Object.Destroy(tex);
            try { File.WriteAllBytes(Path.Combine(Application.persistentDataPath, name), png); }
            catch { }
        }

        /// <summary>
        /// Find the machine the player actually owns. PlayerStore.FindAllItem() walks
        /// front/back/hidden/showcase/... windows, which is exactly the set EndNight ticks,
        /// so anything found here will really run a cycle at night.
        /// </summary>
        private static GameItem FindLive(string machineId, out int scanned)
        {
            scanned = 0;
            List<GameItem> all = null;
            try { all = PlayerStore.Instance.FindAllItem(); } catch { return null; }
            if (all == null) return null;
            scanned = all.Count;
            for (int i = 0; i < all.Count; i++)
            {
                GameItem cand = all[i];
                if (cand == null) continue;
                if (cand.identifier == machineId) return cand;
            }
            return null;
        }

        /// <summary>
        /// Stuff items into the LIVE machine. Usage: feedlive <machineId> <itemId...>
        /// Unlike `feed` this does not spawn a ghost - it walks the machine own
        /// GameInventory children and lets each slot filter decide, so a battery only
        /// lands in the power slot and a blank only in the input grid.
        /// </summary>
        private static string FeedLive(string[] parts)
        {
            if (parts == null || parts.Length < 3) return "usage: feedlive <machineId> <itemId...>";
            int scanned = 0;
            GameItem machine = FindLive(parts[1], out scanned);
            if (machine == null) return "no live " + parts[1] + " among " + scanned + " owned items";
            List<GameInventory> invs = null;
            try { invs = GraphUtils.FindChildrenType<GameInventory>(machine); } catch { }
            if (invs == null || invs.Count == 0) return "machine=" + parts[1] + " has no GameInventory child";
            StringBuilder sb = new StringBuilder();
            sb.Append("machine=").Append(parts[1]).Append(" owned=").Append(scanned).Append(" invs=").Append(invs.Count);
            for (int i = 2; i < parts.Length; i++)
            {
                GameItem it = null;
                try { it = ItemSpawner.Spawn(parts[i]); } catch { }
                if (it == null) { sb.Append("\n  ").Append(parts[i]).Append(" spawn=null"); continue; }
                string landed = null;
                for (int k = 0; k < invs.Count; k++)
                {
                    GameInventory inv = invs[k];
                    if (inv == null) continue;
                    // CanAccept consults the slot mayInventoryAddItemFunc; TryAccept adds
                    // there and nowhere else. EmporiumTryAdd would reroute to the backpack.
                    bool can = false;
                    try { can = GraphUtils.CanAccept(inv, it); } catch { }
                    if (!can) continue;
                    try { GraphUtils.TryAccept(inv, it); } catch { }
                    if (it.parentInventory != null) { landed = k + " " + (inv.identifier ?? "anon"); break; }
                }
                sb.Append("\n  ").Append(parts[i]).Append(" placed=").Append(it.parentInventory != null);
                if (landed != null) sb.Append(" slot=").Append(landed);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Read the LIVE machine: state, progress, where it lives, what each slot holds.
        /// Usage: minspect <machineId>
        /// </summary>
        private static string MInspect(string machineId)
        {
            if (string.IsNullOrEmpty(machineId)) return "usage: minspect <machineId>";
            List<GameItem> all = null;
            try { all = PlayerStore.Instance.FindAllItem(); } catch { }
            if (all == null) return "FindAllItem unavailable";
            StringBuilder sb = new StringBuilder();
            sb.Append("scan=").Append(all.Count).Append(" lookingFor=").Append(machineId);
            int found = 0;
            for (int i = 0; i < all.Count; i++)
            {
                GameItem m = all[i];
                if (m == null || m.identifier != machineId) continue;
                sb.Append("\n#").Append(found).Append(" parent=").Append(m.parentInventory == null ? "none" : (m.parentInventory.identifier ?? "anon"));
                string state = "?";
                try { state = MachineProgressHelper.GetMachineState(m); } catch { }
                sb.Append(" state=").Append(state);
                try { sb.Append(" progress=").Append(m.GetTagReadonly("MACHINE_PROGRESS_CURRENT_TAG").GetInt()); } catch { }
                try { sb.Append(" speed=").Append(m.GetTagReadonly("CURRENT_PROCESSING_SPEED_TAG").GetInt()); } catch { }
                try { sb.Append(" powerUsage=").Append(MachineryHelper.GetMachinePowerUsage(m)); } catch { }
                try { sb.Append(" unitVal=").Append(m.unitValue).Append(" baseVal=").Append(m.GetBaseValue()).Append(" curVal=").Append(m.GetCurrentValue(false, true, true, false, 0L)).Append(" noChildCur=").Append(m.GetCurrentValue(false, false, true, false, 0L)); } catch { }
                try
                {
                    if (m.itemFeatures != null && m.itemFeatures.Count > 0)
                    {
                        sb.Append(" feats=[");
                        for (int f = 0; f < m.itemFeatures.Count; f++)
                            sb.Append(f > 0 ? "," : "").Append(m.itemFeatures[f]?.identifier).Append(":").Append(m.itemFeatures[f]?.GetCurrentValueMod());
                        sb.Append("]");
                    }
                }
                catch { }
                List<GameInventory> invs = null;
                try { invs = GraphUtils.FindChildrenType<GameInventory>(m); } catch { }
                if (invs != null)
                {
                    sb.Append(" invs=").Append(invs.Count);
                    for (int k = 0; k < invs.Count; k++)
                    {
                        GameInventory inv = invs[k];
                        if (inv == null) { sb.Append(" [").Append(k).Append(" null]"); continue; }
                        int n = inv.childItems == null ? -1 : inv.childItems.Count;
                        sb.Append(" [").Append(k).Append(" ").Append(inv.identifier ?? "anon").Append(" ").Append(n).Append("]");
                        if (inv.childItems != null)
                        {
                            for (int j = 0; j < inv.childItems.Count && j < 6; j++)
                            {
                                GameItem c = inv.childItems[j];
                                if (c != null) sb.Append(" ").Append(c.identifier);
                            }
                        }
                    }
                }
                found++;
            }
            if (found == 0) sb.Append("\n(no live ").Append(machineId).Append(")");
            else sb.Append("\nfound=").Append(found);
            return sb.ToString();
        }

        private static GameItem _parkedBattery;

        private static string AnimCheck(string machineId)
        {
            string targetId = string.IsNullOrEmpty(machineId) ? "arc_burner" : machineId;
            List<GameItem> all = PlayerStore.Instance.FindAllItem();
            if (all == null) return "FindAllItem null";
            StringBuilder sb = new StringBuilder();
            int idx = 0;
            for (int i = 0; i < all.Count; i++)
            {
                GameItem m = all[i];
                if (m == null || m.identifier != targetId) continue;
                GameItemElement el = m as GameItemElement;
                UnityEngine.UI.Image img = el != null && el.background != null ? el.background.image : null;
                Sprite sp = img != null ? img.sprite : null;
                string rectStr = sp == null ? "null" : (sp.rect.x + "," + sp.rect.y + "," + sp.rect.width + "x" + sp.rect.height);
                string texStr = sp == null || sp.texture == null ? "null" : (sp.texture.width + "x" + sp.texture.height);
                bool canPow = false;
                try { canPow = MachineHelper.CanPower(m); } catch { }
                string st = "?";
                try { st = MachineProgressHelper.GetMachineState(m); } catch { }
                sb.Append("#").Append(idx++)
                  .Append(" state=").Append(st)
                  .Append(" canPower=").Append(canPow)
                  .Append(" spritePath=").Append(m.spritePath)
                  .Append(" imgRect=").Append(rectStr)
                  .Append(" tex=").Append(texStr)
                  .Append(" hit=").Append(img != null ? img.alphaHitTestMinimumThreshold.ToString("0.##") : "?")
                  .Append('\n');
            }
            return idx == 0 ? "none found" : sb.ToString().TrimEnd();
        }

        private static string AnimBattery(string[] parts)
        {
            if (parts == null || parts.Length < 3) return "usage: animbattery <idx> <pull|put|putchip|putblank|clearready>";
            int idx;
            if (!int.TryParse(parts[1], out idx)) return "bad idx";
            string op = parts[2].ToLowerInvariant();
            List<GameItem> all = PlayerStore.Instance.FindAllItem();
            List<GameItem> burners = new List<GameItem>();
            if (all != null)
            {
                for (int i = 0; i < all.Count; i++)
                    if (all[i] != null && all[i].identifier == "arc_burner") burners.Add(all[i]);
            }
            if (idx < 0 || idx >= burners.Count) return "idx out of range: " + burners.Count;
            GameItem m = burners[idx];
            GameSlotInventory pslot = MachineHelper.GetBatterySlot(m);
            if (op == "pull")
            {
                if (pslot == null || pslot.childItem == null) return "no battery in slot";
                _parkedBattery = pslot.childItem;
                GraphUtils.EmporiumTryAdd(_parkedBattery);
                return "pulled battery=" + _parkedBattery.identifier + " stillInPower=" + (pslot.childItem != null);
            }
            if (op == "put")
            {
                if (pslot == null) return "pslot null";
                GameItem bat = _parkedBattery ?? DirectoryMaster.Item("energy_credit", true);
                _parkedBattery = null;
                if (GraphUtils.CanAccept(pslot, bat)) GraphUtils.TryAccept(pslot, bat);
                return "put battery inSlot=" + (bat.parentInventory == pslot);
            }
            if (op == "putchip")
            {
                GameSlotInventory cslot = MachineHelper.GetBodyStorage(m, 3) as GameSlotInventory;
                if (cslot == null) return "cslot null";
                GameItem chip = DirectoryMaster.Item("arc_chip_brokenring", true);
                if (GraphUtils.CanAccept(cslot, chip)) GraphUtils.TryAccept(cslot, chip);
                return "putchip inSlot=" + (chip.parentInventory == cslot);
            }
            if (op == "putblank")
            {
                GameInventory islot = MachineHelper.GetBodyStorage(m, 2) as GameInventory;
                if (islot == null) return "islot null";
                GameItem blank = DirectoryMaster.Item("gd_blank", true);
                if (GraphUtils.CanAccept(islot, blank)) GraphUtils.TryAccept(islot, blank);
                return "putblank inSlot=" + (blank.parentInventory == islot);
            }
            if (op == "clearready")
            {
                GameSlotInventory cslot = MachineHelper.GetBodyStorage(m, 3) as GameSlotInventory;
                GameInventory islot = MachineHelper.GetBodyStorage(m, 2) as GameInventory;
                if (cslot != null && cslot.childItem != null) cslot.childItem.Destroy();
                if (islot != null && islot.childItems != null)
                {
                    for (int i = islot.childItems.Count - 1; i >= 0; i--)
                        if (islot.childItems[i] != null) islot.childItems[i].Destroy();
                }
                if (pslot != null && pslot.childItem != null) pslot.childItem.Destroy();
                return "cleared ready slots";
            }
            return "unknown op: " + op;
        }

        private static GameItem _inspectedTmp;

        private static string InspectItem(string itemId)
        {
            string id = string.IsNullOrEmpty(itemId) ? "gd_p_brokenring" : itemId;
            if (_inspectedTmp != null) { try { _inspectedTmp.Destroy(); } catch { } _inspectedTmp = null; }
            _inspectedTmp = DirectoryMaster.Item(id, true);
            if (_inspectedTmp == null) return "failed to create " + id;
            InspectionUIManager.Instance.OnInspectItem(_inspectedTmp);
            return "inspecting id=" + id + " name=" + _inspectedTmp.name;
        }

        private static string CloseInspect()
        {
            try { InspectionUIManager.Instance.CloseUI(); } catch { }
            if (_inspectedTmp != null) { try { _inspectedTmp.Destroy(); } catch { } _inspectedTmp = null; }
            return "closed inspect";
        }

        private static string RefurbTest()
        {
            StringBuilder sb = new StringBuilder();
            GameItem kit = DirectoryMaster.Item("arc_refurb_kit", true);
            GameItem polish = DirectoryMaster.Item("arc_disc_polish", true);
            GameItem gpuRefurb = DirectoryMaster.Item("hw_gpu_mk2_refurb", true);
            GameItem gpuNew = DirectoryMaster.Item("hw_gpu_mk2", true);
            GameItem pirate = DirectoryMaster.Item("gd_p_brokenring", true);
            GameItem blank = DirectoryMaster.Item("gd_blank", true);
            try
            {
                // Make sure pirate disc has noticeable wear so polish actually acts
                ItemFeature wear = pirate.FindItemFeatureByCategory("CATEGORY_DISC_WEAR");
                ItemFeature licBefore = pirate.FindItemFeatureByCategory("CATEGORY_GAME_LICENSE");
                string licBeforeId = licBefore != null && licBefore.realCondition != null ? licBefore.realCondition.identifier : "?";

                bool canKitOnRefurb = kit.mayThisTargetItemFunc != null && kit.mayThisTargetItemFunc(kit, gpuRefurb);
                bool canKitOnNew = kit.mayThisTargetItemFunc != null && kit.mayThisTargetItemFunc(kit, gpuNew);
                long gpuValBefore = gpuRefurb.GetCurrentValue(false, true, false, false, 0L);
                if (canKitOnRefurb && kit.onThisTargetItemFunc != null) kit.onThisTargetItemFunc(kit, gpuRefurb);
                long gpuValAfter = gpuRefurb.GetCurrentValue(false, true, false, false, 0L);
                ItemFeature hwCondAfter = gpuRefurb.FindItemFeatureByCategory("CATEGORY_HW_CONDITION");
                string hwCondId = hwCondAfter != null && hwCondAfter.realCondition != null ? hwCondAfter.realCondition.identifier : "?";

                bool canPolishOnPirate = polish.mayThisTargetItemFunc != null && polish.mayThisTargetItemFunc(polish, pirate);
                bool canPolishOnBlank = polish.mayThisTargetItemFunc != null && polish.mayThisTargetItemFunc(polish, blank);
                if (canPolishOnPirate && polish.onThisTargetItemFunc != null) polish.onThisTargetItemFunc(polish, pirate);
                ItemFeature licAfter = pirate.FindItemFeatureByCategory("CATEGORY_GAME_LICENSE");
                string licAfterId = licAfter != null && licAfter.realCondition != null ? licAfter.realCondition.identifier : "?";

                sb.Append("kitOnRefurb=").Append(canKitOnRefurb)
                  .Append(" kitOnNew=").Append(canKitOnNew)
                  .Append(" gpuVal=").Append(gpuValBefore).Append("->").Append(gpuValAfter)
                  .Append(" hwCondAfter=").Append(hwCondId)
                  .Append(" | polishOnPirate=").Append(canPolishOnPirate)
                  .Append(" polishOnBlank=").Append(canPolishOnBlank)
                  .Append(" lic=").Append(licBeforeId).Append("->").Append(licAfterId);
            }
            finally
            {
                try { kit?.Destroy(); } catch { }
                try { polish?.Destroy(); } catch { }
                try { gpuRefurb?.Destroy(); } catch { }
                try { gpuNew?.Destroy(); } catch { }
                try { pirate?.Destroy(); } catch { }
                try { blank?.Destroy(); } catch { }
            }
            return sb.ToString();
        }

        private static string RepairTest()
        {
            List<GameItem> all = PlayerStore.Instance.FindAllItem();
            GameItem burner = null;
            if (all != null)
            {
                for (int i = 0; i < all.Count; i++)
                {
                    if (all[i] != null && all[i].identifier == "arc_burner"
                        && MachineProgressHelper.GetMachineState(all[i]) == "STATE_READY")
                    { burner = all[i]; break; }
                }
            }
            if (burner == null) return "no READY arc_burner found";

            GameItem part = DirectoryMaster.Item("arc_repair_part", true);
            GameItem blank = DirectoryMaster.Item("gd_blank", true);
            StringBuilder sb = new StringBuilder();
            try
            {
                bool wrongMay = blank.mayThisTargetItemFunc != null && blank.mayThisTargetItemFunc(blank, burner);
                bool partMay = part.mayThisTargetItemFunc != null && part.mayThisTargetItemFunc(part, burner);
                part.SetUnitCount(25);
                // 1) No fault yet -> should refuse and NOT consume part
                part.onThisTargetItemFunc?.Invoke(part, burner);
                long countAfterNoFault = part.unitCount;

                // 2) Raise fault with try=0 -> fail or succeed; force try=19 (100% chance) on second run
                burner.ModifyTag("ARC_BURN_FAULT", s => s.Enable().SetInt(1));
                burner.ModifyTag("ARC_BURN_FAULT_TRY", s => s.Enable().SetInt(0));
                part.onThisTargetItemFunc?.Invoke(part, burner);
                long countAfterTry1 = part.unitCount;
                bool stillFaultAfterTry1 = burner.IsTag("ARC_BURN_FAULT");
                int tryCount1 = burner.GetTagReadonly("ARC_BURN_FAULT_TRY").GetInt();

                if (stillFaultAfterTry1)
                {
                    burner.ModifyTag("ARC_BURN_FAULT_TRY", s => s.Enable().SetInt(19)); // 5 + 19*5 = 100%
                    part.onThisTargetItemFunc?.Invoke(part, burner);
                }
                bool faultAfter100 = burner.IsTag("ARC_BURN_FAULT");
                sb.Append("wrongMay=").Append(wrongMay)
                  .Append(" partMay=").Append(partMay)
                  .Append(" countNoFault=").Append(countAfterNoFault)
                  .Append(" countAfterTry1=").Append(countAfterTry1)
                  .Append(" stillFault1=").Append(stillFaultAfterTry1)
                  .Append(" tryCount1=").Append(tryCount1)
                  .Append(" faultAfter100=").Append(faultAfter100);
            }
            finally
            {
                burner.ModifyTag("ARC_BURN_FAULT", s => s.Disable().Reset());
                burner.ModifyTag("ARC_BURN_FAULT_TRY", s => s.Disable().Reset());
                try { part?.Destroy(); } catch { }
                try { blank?.Destroy(); } catch { }
            }
            return sb.ToString();
        }

        private static string ShowcaseTest()
        {
            if (EmporiumEntry.Instance == null || EmporiumEntry.Instance.showcaseElement == null)
                return "showcaseElement unavailable";
            GameInventory scInv = EmporiumEntry.Instance.showcaseElement;
            GameItem disc = DirectoryMaster.Item("gd_g_brokenring", true);
            GameItem gpu = DirectoryMaster.Item("hw_gpu_mk2", true);
            StringBuilder sb = new StringBuilder();
            try
            {
                bool addDisc = GraphUtils.CanAccept(scInv, disc) && GraphUtils.TryAccept(scInv, disc) >= 0;
                if (disc.parentInventory != scInv) GraphUtils.EmporiumTryAdd(scInv, disc);
                List<string> typesDisc = ShowcaseHelper.GetDisplayCaseType();
                int valDisc = ShowcaseHelper.GetTypeTotalValue("ARC_GAME_GOODS");
                string clientDisc = ShowcaseHelper.CreateClientByStockType("ARC_GAME_GOODS");
                List<StoreClient> spawnedDisc = ShowcaseHelper.HandleDisplayCase();
                sb.Append("discPlaced=").Append(disc.parentInventory == scInv)
                  .Append(" hasMerch=").Append(ShowcaseHelper.HasMerchandise())
                  .Append(" types=[").Append(string.Join(",", typesDisc ?? new List<string>())).Append("]")
                  .Append(" gameVal=").Append(valDisc)
                  .Append(" mapGame=").Append(clientDisc)
                  .Append(" handleCount=").Append(spawnedDisc == null ? 0 : spawnedDisc.Count);

                disc.Destroy();
                GraphUtils.TryAccept(scInv, gpu);
                if (gpu.parentInventory != scInv) GraphUtils.EmporiumTryAdd(scInv, gpu);
                List<string> typesHw = ShowcaseHelper.GetDisplayCaseType();
                int valHw = ShowcaseHelper.GetTypeTotalValue("ARC_HARDWARE_GOODS");
                string clientHw = ShowcaseHelper.CreateClientByStockType("ARC_HARDWARE_GOODS");
                sb.Append("\nhwPlaced=").Append(gpu.parentInventory == scInv)
                  .Append(" types=[").Append(string.Join(",", typesHw ?? new List<string>())).Append("]")
                  .Append(" hwVal=").Append(valHw)
                  .Append(" mapHw=").Append(clientHw);
            }
            finally
            {
                try { if (disc != null && disc.parentInventory != null) disc.Destroy(); } catch { }
                try { if (gpu != null && gpu.parentInventory != null) gpu.Destroy(); } catch { }
            }
            return sb.ToString();
        }

        private static string PiracyTest()
        {
            Dictionary<string, string> cache = PlayerStore.Instance.itemDisplayNameCache;
            string oldLedger = null;
            bool hadOld = cache != null && cache.TryGetValue("__arcade_piracy__", out oldLedger);

            GameItem p1 = DirectoryMaster.Item("gd_p_brokenring", true);
            GameItem p2 = DirectoryMaster.Item("gd_p_starfield", true);
            List<GameInventory> backInvs = EmporiumEntry.Instance != null && EmporiumEntry.Instance.backInvWindow != null
                ? GraphUtils.FindChildrenType<GameInventory>(EmporiumEntry.Instance.backInvWindow)
                : null;
            GameInventory back = backInvs != null && backInvs.Count > 0 ? backInvs[0] : null;
            if (back == null) return "backInvWindow null";
            GraphUtils.TryAccept(back, p1);
            GraphUtils.TryAccept(back, p2);
            bool inBefore = (p1.parentInventory == back) && (p2.parentInventory == back);
            int cashBefore = PlayerStore.Instance.playerCash;
            StringBuilder sb = new StringBuilder();
            try
            {
                // Run inspection with security (not fake inspector)
                ContrabandHelper.StartInspection(2, false);
                string ledgerAfter = null;
                if (cache != null) cache.TryGetValue("__arcade_piracy__", out ledgerAfter);
                bool p1Gone = (p1 as GameItemElement)?.IsDestroyed() == true || p1.parentInventory == null;
                bool p2Gone = (p2 as GameItemElement)?.IsDestroyed() == true || p2.parentInventory == null;
                sb.Append("inBefore=").Append(inBefore)
                  .Append(" p1Gone=").Append(p1Gone)
                  .Append(" p2Gone=").Append(p2Gone)
                  .Append(" cash=").Append(cashBefore).Append("->").Append(PlayerStore.Instance.playerCash)
                  .Append(" ledger=").Append(ledgerAfter ?? "<null>");
            }
            finally
            {
                try { if (p1 != null && !((p1 as GameItemElement)?.IsDestroyed() ?? false)) p1.Destroy(); } catch { }
                try { if (p2 != null && !((p2 as GameItemElement)?.IsDestroyed() ?? false)) p2.Destroy(); } catch { }
                PlayerStore.Instance.playerCash = cashBefore;
                if (cache != null)
                {
                    if (hadOld) cache["__arcade_piracy__"] = oldLedger;
                    else cache.Remove("__arcade_piracy__");
                }
            }
            return sb.ToString();
        }

        private static string MarketTest()
        {
            StoreEventManager mgr = StoreStation.Instance != null ? StoreStation.Instance.storeEventManager : null;
            if (mgr == null) return "mgr null";
            StringBuilder sb = new StringBuilder();
            sb.Append("day=").Append(StoreStation.GetDayCounter()).Append(" active=[");
            List<StoreEvent> act = mgr.GetActiveEvents();
            if (act != null)
            {
                for (int i = 0; i < act.Count; i++)
                {
                    StoreEvent ev = act[i];
                    if (ev == null) continue;
                    sb.Append("\n  ").Append(ev.identifier).Append(" name=").Append(ev.newsName)
                      .Append(" eff=").Append((ev.GetEventPriceEffect() ?? "").Replace("\n", "|"));
                }
            }
            sb.Append("\n] future=[");
            if (mgr.futurEvents != null)
            {
                for (int i = 0; i < mgr.futurEvents.Count; i++)
                {
                    StoreEvent ev = mgr.futurEvents[i];
                    int d = mgr.futurEventsDay != null && i < mgr.futurEventsDay.Count ? mgr.futurEventsDay[i] : -1;
                    if (ev != null) sb.Append("\n  ").Append(ev.identifier).Append(" inDays=").Append(d).Append(" known=").Append(ev.IsKnown);
                }
            }
            sb.Append("\n]");
            GameItem d1 = DirectoryMaster.Item("gd_g_brokenring", true);
            GameItem d2 = DirectoryMaster.Item("gd_g_thatgame", true);
            GameItem d3 = DirectoryMaster.Item("gd_g_blockcraft", true);
            try
            {
                sb.Append("\nbrokenring base=").Append(d1.GetBaseValue())
                  .Append(" cur(ev=T)=").Append(d1.GetCurrentValue(false, true, true, false, 0L))
                  .Append(" cur(ev=F)=").Append(d1.GetCurrentValue(false, true, false, false, 0L));
                sb.Append("\nthatgame base=").Append(d2.GetBaseValue())
                  .Append(" cur(ev=T)=").Append(d2.GetCurrentValue(false, true, true, false, 0L))
                  .Append(" cur(ev=F)=").Append(d2.GetCurrentValue(false, true, false, false, 0L));
                sb.Append("\nblockcraft base=").Append(d3.GetBaseValue())
                  .Append(" cur(ev=T)=").Append(d3.GetCurrentValue(false, true, true, false, 0L))
                  .Append(" cur(ev=F)=").Append(d3.GetCurrentValue(false, true, false, false, 0L));
            }
            finally
            {
                try { d1?.Destroy(); } catch { }
                try { d2?.Destroy(); } catch { }
                try { d3?.Destroy(); } catch { }
            }
            return sb.ToString();
        }

        private static string IntelTest()
        {
            bool has = StoreClientListInformation.HasAvailableInformation();
            StoreClient c = StoreClientListInformation.CreateShadyCustomer();
            return "hasInfo=" + has + " shady=" + (c == null ? "null" : (c.identifier + "/" + c.serviceCost));
        }

        private static string SleepOneDay()
        {
            EndOfDayUIManager ui = EndOfDayUIManager.Instance;
            if (ui == null) return "EndOfDayUIManager.Instance null";
            ui.OnEndDayButtonClick();
            return "OnEndDayButtonClick() invoked from day=" + StoreStation.GetDayCounter();
        }

        private static string NewsCmd(string arg)
        {
            NewsUIManager mgr = NewsUIManager.Instance;
            if (mgr == null) return "NewsUIManager.Instance null";
            if (arg == "open" || string.IsNullOrEmpty(arg))
            {
                if (!mgr.uiGameObject.activeSelf) mgr.ToggleUI();
                else mgr.PopulateUI();
            }
            else if (arg == "close")
            {
                mgr.CloseUI();
                return "news closed";
            }
            else if (arg == "testpga" || arg == "testglut" || arg == "testleak" || arg == "testrelease")
            {
                StoreEventManager em = StoreStation.Instance.storeEventManager;
                string id = arg == "testpga" ? "arc_pga_awards"
                    : arg == "testglut" ? "arc_disc_glut"
                    : arg == "testleak" ? "arc_news_thatgame_leak"
                    : "arc_news_thatgame_release";
                StoreEvent ev = new StoreEvent
                {
                    identifier = id,
                    eventType = StoreEvent.EventType.NORMALE,
                    newsName = "测试事件：" + id,
                    newsDescription = "这是一条用于验证深空电玩报纸配图与 Better Newspapers 排版兼容性的测试报道正文。",
                    duration = 2,
                    isHiddenFromNewsPaper = false,
                    negociationDatas = new List<NegociationData>()
                };
                if (!em.IsEventActive(id)) em.ActivateEvent(ev, false, true);
                if (!mgr.uiGameObject.activeSelf) mgr.ToggleUI();
                else mgr.PopulateUI();
            }
            int activePhotos = 0;
            StringBuilder sb = new StringBuilder();
            List<NewsUIElement> allEls = new List<NewsUIElement>();
            HashSet<NewsUIElement> seen = new HashSet<NewsUIElement>();
            foreach (List<NewsUIElement> lst in new[] { mgr.layout1Elements, mgr.layout2Elements, mgr.layout3Elements, mgr.layout4Elements, mgr.allElements })
            {
                if (lst == null) continue;
                for (int i = 0; i < lst.Count; i++)
                    if (lst[i] != null && seen.Add(lst[i])) allEls.Add(lst[i]);
            }
            for (int i = 0; i < allEls.Count; i++)
            {
                NewsUIElement el = allEls[i];
                if (el == null || !el.gameObject.activeInHierarchy || el.savedStoreEvent == null) continue;
                Transform arc = el.textContent != null ? el.textContent.rectTransform.Find("ArcNewsPhoto") : null;
                Transform bn = el.textContent != null ? el.textContent.rectTransform.Find("Printed photograph") : null;
                string arcInfo = arc != null && arc.gameObject.activeSelf ? ((RectTransform)arc).sizeDelta.ToString() : "off";
                string bnInfo = bn != null && bn.gameObject.activeSelf ? ((RectTransform)bn).sizeDelta.ToString() : "off";
                if (arc != null && arc.gameObject.activeSelf) activePhotos++;
                string dbg = "";
                if (el.textContent != null)
                {
                    RectTransform trt = el.textContent.rectTransform;
                    Rect tr = trt.rect;
                    el.textContent.ForceMeshUpdate(true, true);
                    int lc = el.textContent.textInfo.lineCount;
                    float lastDesc = lc > 0 ? el.textContent.textInfo.lineInfo[lc - 1].descender : -999f;
                    dbg = " pv=" + trt.pivot + " r=" + tr + " lc=" + lc + " desc=" + lastDesc.ToString("0.#") + " m=" + el.textContent.margin;
                }
                sb.Append("\n  slot[").Append(i).Append("] id=").Append(el.savedStoreEvent.identifier)
                  .Append(" arcPhoto=").Append(arcInfo).Append(" bnPhoto=").Append(bnInfo).Append(dbg);
            }
            return "news open activeArcPhotos=" + activePhotos + sb.ToString();
        }

        private static string OpenAll()
        {
            StoreShutterButton shutter = StoreShutterButton.Instance;
            if (shutter == null) return "StoreShutterButton.Instance is null";
            shutter.OpenShutter();
            return "OpenShutter() ok";
        }

        private static string CloseAll()
        {
            StoreShutterButton shutter = StoreShutterButton.Instance;
            if (shutter == null) return "StoreShutterButton.Instance is null";
            shutter.CloseShutter();
            return "CloseShutter() ok";
        }

        // ---------------------------------------------------------------- 工具

        /// <summary>把一位客人压成一行：id / 显示名 / 立绘 key / 阵营 / 意图 / 预算。</summary>
        private static string Describe(StoreClient c)
        {
            if (c == null) return "<null client>";
            StringBuilder sb = new StringBuilder();
            sb.Append("id=").Append(c.identifier);
            sb.Append(" name=").Append(c.displayName);
            sb.Append(" sprite=").Append(c.spriteName);
            sb.Append(" faction=").Append(c.clientFaction);
            sb.Append(" intent=").Append(c.clientIntent);
            try
            {
                if (c.clientBlackIdList != null && c.clientBlackIdList.Count > 0)
                    sb.Append(" refuseIds=").Append(c.clientBlackIdList.Count);
            }
            catch { }
            return sb.ToString();
        }
    }
}
