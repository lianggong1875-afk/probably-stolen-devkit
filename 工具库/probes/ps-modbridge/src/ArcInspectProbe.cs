using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace ModDevBridge
{
    /// <summary>
    /// 探针：在已载入的世界里找我们的物品，并用原版入口打开放大镜。
    ///
    /// 为什么需要：Compatible 版的 query_items 在这台机器上返回空，拿不到 item_ref；
    /// 而 operate_inspection 只能吃 item_ref。所以直接用原版 API 打开检视，
    /// 再用 query_ui 只读地看「兼容鉴定」窗口有没有出现。
    ///
    /// 命令：
    ///   find [前缀]      —— 扫店里容器，列出 identifier 以该前缀开头的物品（uniqueId + id + 名字）
    ///   open &lt;uniqueId&gt; —— InspectionUIManager.Instance.OnInspectItem(item)
    ///   close            —— InspectionUIManager.Instance.CloseUI()
    ///   inspect          —— 读当前检视的目标与面板状态
    /// </summary>
    public sealed class ArcInspectProbe3 : IDevModule
    {
        public string Start() { return "ArcInspectProbe3 ready"; }

        public string Execute(string command)
        {
            try
            {
                string cmd = string.IsNullOrEmpty(command) ? "inspect" : command.Trim();
                string[] parts = cmd.Split(' ');
                string verb = parts[0].ToLowerInvariant();
                if (verb == "find") return Find(parts.Length > 1 ? parts[1] : "gd_");
                if (verb == "spawn") return Spawn(parts.Length > 1 ? parts[1] : "gd_g_thatgame");
                if (verb == "open") return Open(int.Parse(parts[1]));
                if (verb == "close") return Close();
                if (verb == "inspect") return Current();
                if (verb == "assets") return Assets();
                if (verb == "label") return Label(int.Parse(parts[1]));
                if (verb == "conds") return Conditions(int.Parse(parts[1]));
                if (verb == "testexpose") return TestExpose(parts.Length > 1 ? parts[1] : "gd_p_thatgame");
                return "unknown command: " + cmd;
            }
            catch (Exception e)
            {
                return "error: " + e.GetType().Name + ": " + e.Message;
            }
        }

        /// <summary>造一件我们的货放进前柜，方便在放大镜里看浮窗（测试档用，不存档）。</summary>
        private static string Spawn(string id)
        {
            GameItem item = DirectoryMaster.Item(id, false);
            if (item == null) return "factory returned null for " + id;
            try { GeneralHelper.SetItemOwned(item, true); } catch { }
            EmporiumEntry entry = EmporiumEntry.Instance;
            if (entry == null || entry.frontInvinvElement == null) return "no emporium front";
            GraphUtils.EmporiumTryAdd(entry.frontInvinvElement, item);
            bool placed = false;
            try { placed = item.parentInventory != null; } catch { }
            return "spawned " + id + " uniqueId=" + item.uniqueId + " placed=" + placed;
        }

        public void Stop() { }

        private static string Find(string prefix)
        {
            StringBuilder sb = new StringBuilder();
            try
            {
                EmporiumEntry entry = EmporiumEntry.Instance;
                if (entry == null) return "no emporium";
                List<GameItem> items = new List<GameItem>();
                Collect(entry.frontInvinvElement, items);
                int n = 0;
                for (int i = 0; i < items.Count; i++)
                {
                    GameItem it = items[i];
                    if (it == null) continue;
                    string id = null;
                    try { id = it.identifier; } catch { }
                    if (string.IsNullOrEmpty(id)) continue;
                    if (!string.IsNullOrEmpty(prefix) && !id.StartsWith(prefix, StringComparison.Ordinal)) continue;
                    n++;
                    sb.Append(it.uniqueId).Append(' ').Append(id).Append(" | ");
                    if (n >= 20) break;
                }
                if (n == 0) sb.Append("none (scanned ").Append(items.Count).Append(')');
            }
            catch (Exception e)
            {
                sb.Append("find failed: ").Append(e.GetType().Name).Append(':').Append(e.Message);
            }
            return sb.ToString();
        }

        private static void Collect(GraphNodeStorage node, List<GameItem> into)
        {
            if (node == null) return;
            try
            {
                GameItem self = node as GameItem;
                if (self != null) into.Add(self);
                List<GameItem> kids = null;
                try { kids = GraphUtils.FindChildrenType<GameItem>(node); } catch { }
                if (kids == null) return;
                for (int i = 0; i < kids.Count; i++)
                {
                    if (kids[i] != null && !ReferenceEquals(kids[i], self)) into.Add(kids[i]);
                }
            }
            catch { }
        }

        /// <summary>
        /// 读本模组 4 张立绘 + 若干物品图标的纹理属性。
        /// 公开版（-PublicRelease）不读盘，像素立绘走"从内存 mip 0 重建"，
        /// 重建成功的话 mip=1 / filter=Point / wrap=Clamp；还是 mip>1 就说明重建没生效、立绘会糊。
        /// </summary>
        private static string Assets()
        {
            StringBuilder sb = new StringBuilder();
            LoadedMod mod = null;
            List<LoadedMod> mods = ModLoader.Mods;
            if (mods != null)
            {
                for (int i = 0; i < mods.Count; i++)
                {
                    LoadedMod m = mods[i];
                    if (m == null || m.Manifest == null) continue;
                    if (m.Manifest.ID == "deepspace_arcade") { mod = m; break; }
                }
            }
            if (mod == null) return "own LoadedMod not found";
            sb.Append("mod=").Append(mod.Manifest.ID).AppendLine();
            string[] portraits = { "sw_portrait", "sw_happy", "sw_sad", "sw_afraid" };
            for (int i = 0; i < portraits.Length; i++)
            {
                string key = portraits[i];
                Texture2D t = null;
                if (mod.Textures != null) mod.Textures.TryGetValue(key, out t);
                if (t == null) { sb.Append("TEXTURE ").Append(key).AppendLine(" = MISSING"); continue; }
                bool crisp = t.mipmapCount <= 1 && t.filterMode == FilterMode.Point && t.wrapMode == TextureWrapMode.Clamp;
                sb.Append("TEXTURE ").Append(key).Append(" = ").Append(t.width).Append('x').Append(t.height)
                  .Append(" mip=").Append(t.mipmapCount).Append(" filter=").Append(t.filterMode)
                  .Append(" wrap=").Append(t.wrapMode).Append(" crisp=").Append(crisp).AppendLine();
            }
            string[] icons = { "gd_box_thatgame", "gd_hi_thatgame", "hw_hi_gpu", "hw_hi_ram", "arc_disc_case", "arc_refurb_kit" };
            for (int i = 0; i < icons.Length; i++)
            {
                string key = icons[i];
                Sprite s = null;
                if (mod.Sprites != null) mod.Sprites.TryGetValue(key, out s);
                if (s == null) { sb.Append("SPRITE ").Append(key).AppendLine(" = MISSING"); continue; }
                string tex = s.texture == null ? "null" : (s.texture.width + "x" + s.texture.height);
                sb.Append("SPRITE ").Append(key).Append(" = ").Append(s.rect.width).Append('x').Append(s.rect.height)
                  .Append(" tex=").Append(tex).AppendLine();
            }
            return sb.ToString();
        }
        /// <summary>用原版入口打开标签打印机面板（等价于玩家走到机器旁使用）。</summary>
        private static string Label(int uniqueId)
        {
            EmporiumEntry entry = EmporiumEntry.Instance;
            if (entry == null) return "no emporium";
            GameItem item = entry.FindItemByUniqueID(uniqueId);
            if (item == null) return "item " + uniqueId + " not found";
            LabelerUIManager ui = LabelerUIManager.Instance;
            if (ui == null) return "LabelerUIManager.Instance is null";
            ui.OpenUI(item);
            return "labeler opened on " + (item.identifier ?? "?") + " (" + (item.name ?? "?") + ")";
        }

        /// <summary>把一件货身上所有 ItemFeature 的真实层/印刷层打出来 —— 验「标签只改公开层」用。</summary>
        private static string Conditions(int uniqueId)
        {
            EmporiumEntry entry = EmporiumEntry.Instance;
            if (entry == null) return "no emporium";
            GameItem item = entry.FindItemByUniqueID(uniqueId);
            if (item == null) return "item " + uniqueId + " not found";
            StringBuilder sb = new StringBuilder();
            sb.Append("id=").Append(item.identifier ?? "?").Append(" name=").Append(item.name ?? "?").AppendLine();
            List<ItemFeature> fs = item.itemFeatures;
            if (fs == null) { sb.AppendLine("no features"); return sb.ToString(); }
            for (int i = 0; i < fs.Count; i++)
            {
                ItemFeature f = fs[i];
                if (f == null) continue;
                string real = f.realCondition == null ? "null" : f.realCondition.identifier;
                string fake = f.fakeCondition == null ? "null" : f.fakeCondition.identifier;
                sb.Append("feature[").Append(i).Append("] cat=").Append(f.category ?? "?")
                  .Append(" real=").Append(real).Append(" fake=").Append(fake)
                  .Append(" disabled=").Append(f.isDisabled).Append(" useCondition=").Append(f.useCondition)
                  .AppendLine();
            }
            return sb.ToString();
        }
        private static string Open(int uniqueId)
        {
            EmporiumEntry entry = EmporiumEntry.Instance;
            if (entry == null) return "no emporium";
            GameItem item = entry.FindItemByUniqueID(uniqueId);
            if (item == null) return "item " + uniqueId + " not found";
            InspectionUIManager ui = InspectionUIManager.Instance;
            if (ui == null) return "no inspection ui";
            ui.OnInspectItem(item);
            return "opened " + uniqueId + " (" + SafeId(item) + ")";
        }

        private static string Close()
        {
            InspectionUIManager ui = InspectionUIManager.Instance;
            if (ui == null) return "no inspection ui";
            ui.CloseUI();
            return "closed";
        }

        private static string Current()
        {
            InspectionUIManager ui = InspectionUIManager.Instance;
            if (ui == null) return "no inspection ui";
            string id = null;
            try { id = ui.currentInspectingItem == null ? null : ui.currentInspectingItem.identifier; } catch { }
            bool open = false;
            try { open = ui.panel != null && ui.panel.activeInHierarchy; } catch { }
            return "panelOpen=" + open + " current=" + (id ?? "null");
        }

        /// <summary>
        /// 验「客户识破假货」这条路：造一件假货 → 读它真实的 fake/real 条件 →
        /// 调原版 GetClientExposeDialog（我们的补丁应该给出台词，而不是像原版那样 NRE）→
        /// 调原版 ExposeFeature 把真相翻开 → 再看议价前后变化。
        /// 只碰这件临时造的货，不写档。
        /// </summary>
        private static string TestExpose(string id)
        {
            GameItem item = DirectoryMaster.Item(id, false);
            if (item == null) return "no item " + id;
            string category = id.StartsWith("hw_", StringComparison.Ordinal)
                ? "CATEGORY_HW_CONDITION" : "CATEGORY_GAME_LICENSE";
            ItemFeature f = null;
            try { f = item.FindItemFeatureByCategory(category); } catch { }
            if (f == null) return "no condition feature for " + category;

            StringBuilder sb = new StringBuilder();
            sb.Append("category=").Append(f.category);
            sb.Append(" fake=").Append(f.fakeCondition == null ? "null" : f.fakeCondition.identifier);
            sb.Append(" real=").Append(f.realCondition == null ? "null" : f.realCondition.identifier);
            sb.Append(" exposed=").Append(f.IsExposed());

            string line;
            try
            {
                Dialogue d = f.GetClientExposeDialog();
                line = d == null ? "null" : d.plainText;
            }
            catch (Exception e)
            {
                line = "THREW " + e.GetType().Name + ": " + e.Message;
            }
            sb.Append(" | dialog=").Append(line);

            long before = -1, after = -1;
            try { before = item.GetNegociatedValue(); } catch (Exception e) { sb.Append(" v1err=").Append(e.GetType().Name); }
            try { f.ExposeFeature(lowkey: true, playBad: false); }
            catch (Exception e) { sb.Append(" exposeErr=").Append(e.GetType().Name).Append(':').Append(e.Message); }
            try { after = item.GetNegociatedValue(); } catch { }
            sb.Append(" | value ").Append(before).Append(" -> ").Append(after);
            sb.Append(" exposedAfter=").Append(f.IsExposed());
            try { item.Destroy(); } catch { }
            return sb.ToString();
        }

        private static string SafeId(GameItem item)
        {
            try { return item == null ? "null" : item.identifier; } catch { return "?"; }
        }
    }
}
