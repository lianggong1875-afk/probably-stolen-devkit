using System;
using System.Collections.Generic;
using System.Reflection;
using ES3Types;
using HarmonyLib;

namespace ModTemplates
{
    /// <summary>
    /// ES3 存档无污染投影骨架：保存时把自定义 StoreEvent 剥离出原版管理器并存入 ModHelper.SetModData，
    /// 保证卸载模组后玩家存档 100% 不坏档。
    /// 来源蒸馏：Station Events (NativePersistence + SavePreparationPatch + SerializationProjectionPatch)。零能力。
    /// </summary>
    internal static class Es3SafeEventProjection
    {
        private static readonly FieldInfo ActiveField =
            typeof(StoreEventManager).GetField("activeEvents", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo CloneMethod =
            typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>由模组设置自己事件 id 的命名前缀，例如 "my.mod:"。</summary>
        internal static string ModEventPrefix = "my.mod:";

        internal static bool IsOwn(string id) =>
            !string.IsNullOrEmpty(id) && id.StartsWith(ModEventPrefix, StringComparison.Ordinal);

        /// <summary>克隆一份不含本模组自定义事件和冷却的干净 StoreEventManager 供 ES3 写盘。</summary>
        internal static object FilterForEs3Write(object obj)
        {
            if (!(obj is StoreEventManager mgr) || ActiveField == null || CloneMethod == null)
                return obj;

            var clean = (StoreEventManager)CloneMethod.Invoke(mgr, null);

            var active = ActiveField.GetValue(mgr) as List<StoreEvent>;
            var cleanActive = new List<StoreEvent>();
            if (active != null)
            {
                foreach (var ev in active)
                    if (ev == null || !IsOwn(ev.identifier)) cleanActive.Add(ev);
            }
            ActiveField.SetValue(clean, cleanActive);

            var cleanFuture = new List<StoreEvent>();
            var cleanDays = new List<int>();
            if (mgr.futurEvents != null && mgr.futurEventsDay != null)
            {
                int n = Math.Min(mgr.futurEvents.Count, mgr.futurEventsDay.Count);
                for (int i = 0; i < n; i++)
                {
                    var ev = mgr.futurEvents[i];
                    if (ev == null || !IsOwn(ev.identifier))
                    {
                        cleanFuture.Add(ev);
                        cleanDays.Add(mgr.futurEventsDay[i]);
                    }
                }
            }
            clean.futurEvents = cleanFuture;
            clean.futurEventsDay = cleanDays;

            var cleanCooldowns = new Dictionary<string, int>(StringComparer.Ordinal);
            if (mgr.eventTracker != null)
            {
                foreach (var kv in mgr.eventTracker)
                    if (!IsOwn(kv.Key)) cleanCooldowns[kv.Key] = kv.Value;
            }
            clean.eventTracker = cleanCooldowns;

            return clean;
        }
    }

    // 启用方式：在自己模组里取消注释并配合 SaveGame Prefix/Finalizer 将自定义事件写入 ModHelper.SetModData
    // [HarmonyPatch(typeof(ES3Type), "WriteProperties")]
    // internal static class MyEs3ProjectionPatch
    // {
    //     [HarmonyPrefix]
    //     private static void Prefix(ref object __0) => __0 = Es3SafeEventProjection.FilterForEs3Write(__0);
    // }
}
