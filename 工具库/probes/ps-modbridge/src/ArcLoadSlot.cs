using System;
using System.Reflection;
using System.Text;

namespace ModDevBridge
{
    /// <summary>
    /// 探针：读存档槽顺序、按槽号载入。
    ///
    /// 为什么需要它：游戏「载入游戏」界面上的卡片与槽号不一定对得上（本机就错位了：
    /// 卡面写「第10天」，点它的 LoadLatest 却进了 saveSlot 0 的第51天主档）。
    /// 探针直接调游戏自己的载入入口 <c>LoadGameUI.OnLoadClicked(int slotId, string file, string version)</c>，
    /// 按真实槽号载入，并且只读/不写盘。
    ///
    /// 命令：
    ///   info              —— 游戏程序集、HasSave、当前槽
    ///   order             —— SaveUIManager.GetOrderedSlotIds()（卡片显示顺序）
    ///   preview &lt;slot&gt;    —— SaveUIManager.GetPreview(slot) 的关键字段
    ///   load &lt;slot&gt; [file] —— 调 OnLoadClicked(slot, file, null)
    /// </summary>
    public sealed class ArcLoadSlot : IDevModule
    {
        public string Start() { return "ArcLoadSlot ready"; }

        public string Execute(string command)
        {
            try
            {
                string cmd = string.IsNullOrEmpty(command) ? "info" : command.Trim();
                string[] parts = cmd.Split(' ');
                string verb = parts[0].ToLowerInvariant();
                if (verb == "info") return Info();
                if (verb == "order") return Order();
                if (verb == "preview") return Preview(parts.Length > 1 ? int.Parse(parts[1]) : 0);
                if (verb == "load") return Load(
                    parts.Length > 1 ? int.Parse(parts[1]) : 1,
                    parts.Length > 2 ? parts[2] : null);
                return "unknown command: " + cmd;
            }
            catch (Exception e)
            {
                return "error: " + e.GetType().Name + ": " + e.Message;
            }
        }

        public void Stop() { }

        private static Assembly Game { get { return typeof(SaveManager).Assembly; } }

        private static string Info()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("game=").Append(Game.GetName().Name);
            sb.Append(" hasSave=").Append(SaveManager.HasSave());
            try
            {
                sb.Append(" ordered=").Append(Order());
            }
            catch (Exception e) { sb.Append(" ordered-failed:").Append(e.GetType().Name); }
            return sb.ToString();
        }

        private static string Order()
        {
            StringBuilder sb = new StringBuilder();
            System.Collections.Generic.List<int> ids = SaveUIManager.GetOrderedSlotIds();
            if (ids == null) return "ordered=null";
            for (int i = 0; i < ids.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(ids[i]);
            }
            return sb.ToString();
        }

        private static string Preview(int slotId)
        {
            SavePreviewData d = SaveUIManager.GetPreview(slotId);
            if (d == null) return "preview " + slotId + " = null";
            return "slot=" + d.slotId + " day=" + d.dayCount + " credit=" + d.credit
                   + " name=" + d.GetDisplayName() + " version=" + d.version;
        }

        private static string Load(int slotId, string file)
        {
            Type t = Game.GetType("LoadGameUI", false);
            if (t == null) return "type LoadGameUI not found";
            MethodInfo m = t.GetMethod("OnLoadClicked",
                BindingFlags.NonPublic | BindingFlags.Static, null,
                new Type[] { typeof(int), typeof(string), typeof(string) }, null);
            if (m == null) return "method OnLoadClicked(int,string,string) not found";
            m.Invoke(null, new object[] { slotId, file, null });
            return "invoked OnLoadClicked(slot=" + slotId + ", file=" + (file ?? "null") + ")";
        }
    }
}
