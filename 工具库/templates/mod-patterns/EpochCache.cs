using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using UnityEngine;

namespace ModTemplates
{
    /// <summary>
    /// 「代际失效」缓存：缓存一个算起来很贵的原版结果（容器总估价、真伪汇总、机器加成…），
    /// 又要保证永不过期。
    ///
    /// 核心思路：不去逐条删除缓存，而是维护一个全局 **epoch（代际）计数器**；
    /// 任何会改变结果的改动都 `Bump()` 一次，读缓存时代际不符就整条作废。
    /// 于是"让所有缓存失效"= 一次 `Interlocked.Increment`，成本 O(1)。
    ///
    /// 来源蒸馏：Value Cache（工坊 3843892181）。零能力。
    ///
    /// 三条铁律：
    ///   1. 键必须包含**所有**影响结果的外部状态（事件、加价、纯度、日期…），否则会返回过期值（这类 bug 最常见）；
    ///   2. 缓存容器用 `ConditionalWeakTable`，被缓存对象被回收，缓存自动消失，不会泄漏 GameItem；
    ///   3. 只允许主线程读写（记下主线程 id 校验），并设容量上限自愈。
    /// </summary>
    internal static class EpochCache
    {
        private const int MaxTrackedItems = 50000;
        private const int MaxEntriesPerItem = 32;

        private static long epoch = 1;
        private static int mainThreadId;
        private static int trackedItems;
        private static bool enabled = true;

        private static ConditionalWeakTable<GameItem, ItemCache> table =
            new ConditionalWeakTable<GameItem, ItemCache>();

        private sealed class ItemCache
        {
            internal long Epoch;
            internal readonly Dictionary<string, long> Entries = new Dictionary<string, long>(8, StringComparer.Ordinal);
        }

        /// <summary>模组启用时调一次（读档 / 热重载也要再调，把陈旧缓存整体丢掉）。</summary>
        internal static void Initialize(bool enable = true)
        {
            mainThreadId = Thread.CurrentThread.ManagedThreadId;
            table = new ConditionalWeakTable<GameItem, ItemCache>();
            trackedItems = 0;
            enabled = enable;
            Interlocked.Increment(ref epoch);
        }

        /// <summary>关掉立刻回原版逐次计算（给玩家一个"性能 vs 正确性"的开关）。</summary>
        internal static void SetEnabled(bool value)
        {
            enabled = value;
            Initialize(value);
        }

        /// <summary>任何会影响缓存结果的改动都调它。成本只是一次原子自增。</summary>
        internal static void Bump()
        {
            Interlocked.Increment(ref epoch);
        }

        /// <summary>把"影响结果的外部状态"压成一个字符串键。拼完必须是完整键，不允许缺字段。</summary>
        internal static string Key(params object[] parts)
        {
            if (parts == null || parts.Length == 0) return string.Empty;
            var sb = new StringBuilder(64);
            foreach (object p in parts)
            {
                sb.Append(p == null ? "~" : p.ToString());
                sb.Append('\u001f');                       // 不可见分隔符，避免内容里出现分隔符造成歧义
            }
            return sb.ToString();
        }

        /// <summary>尝试读缓存。返回 false 表示让调用方老老实实算一遍。</summary>
        internal static bool TryGet(GameItem item, string key, out long value)
        {
            value = 0;
            if (!enabled || item == null || string.IsNullOrEmpty(key)) return false;
            if (Thread.CurrentThread.ManagedThreadId != mainThreadId) return false;

            try
            {
                if (!table.TryGetValue(item, out ItemCache cache)) return false;

                long now = Interlocked.Read(ref epoch);
                if (cache.Epoch != now)
                {
                    cache.Entries.Clear();                 // 代际变了：整条作废，不是逐条删
                    cache.Epoch = now;
                    return false;
                }
                return cache.Entries.TryGetValue(key, out value);
            }
            catch (Exception e)
            {
                Debug.LogError("[EpochCache] get failed: " + e);
                return false;
            }
        }

        /// <summary>存缓存。容量到顶时整体重置，宁可丢缓存也不让内存无限长。</summary>
        internal static void Store(GameItem item, string key, long value)
        {
            if (!enabled || item == null || string.IsNullOrEmpty(key)) return;
            if (Thread.CurrentThread.ManagedThreadId != mainThreadId) return;

            try
            {
                if (!table.TryGetValue(item, out ItemCache cache))
                {
                    if (trackedItems >= MaxTrackedItems) Reset("item cap");
                    cache = new ItemCache();
                    table.Add(item, cache);
                    trackedItems++;
                }

                long now = Interlocked.Read(ref epoch);
                if (cache.Epoch != now)
                {
                    cache.Entries.Clear();
                    cache.Epoch = now;
                }
                if (cache.Entries.Count < MaxEntriesPerItem) cache.Entries[key] = value;
            }
            catch (Exception e)
            {
                Debug.LogError("[EpochCache] store failed: " + e);
            }
        }

        private static void Reset(string reason)
        {
            table = new ConditionalWeakTable<GameItem, ItemCache>();
            trackedItems = 0;
            Interlocked.Increment(ref epoch);
            Debug.Log("[EpochCache] reset: " + reason);
        }
    }

    /// <summary>
    /// 示例：给「容器总估价」加缓存。真正用的时候把 GetContainerValue 换成原版那条贵的路径。
    /// </summary>
    internal static class EpochCacheUsageExample
    {
        /// <summary>影响估价的所有外部状态都要进键：店铺加价、市场加成、生效中的事件签名…</summary>
        private static string BuildKey(GameItem container)
        {
            PlayerStore store = PlayerStore.HasInstance ? PlayerStore.Instance : null;
            StoreStation station = StoreStation.Instance;
            int retailMarkup = store != null ? store.retailMarkup : 0;
            string events = station != null && station.storeEventManager != null
                ? BuildEventSignature(station.storeEventManager)
                : string.Empty;
            return EpochCache.Key(container.identifier, container.uniqueId, retailMarkup, events);
        }

        private static string BuildEventSignature(StoreEventManager mgr)
        {
            List<StoreEvent> active = mgr.GetActiveEvents();
            if (active == null || active.Count == 0) return string.Empty;
            var sb = new StringBuilder(64);
            foreach (StoreEvent ev in active)
            {
                if (ev == null) { sb.Append('|'); continue; }
                sb.Append(ev.identifier).Append('\u001f');
                if (ev.negociationDatas != null)
                    foreach (NegociationData neg in ev.negociationDatas)
                        if (neg != null) sb.Append(neg.itemType).Append('=').Append(neg.value).Append('\u001f');
                sb.Append('\u001d');
            }
            return sb.ToString();
        }

        /// <summary>读估价：先查缓存，miss 再算并回填。</summary>
        internal static long GetContainerValue(GameItem container)
        {
            if (container == null) return 0;
            string key = BuildKey(container);
            if (EpochCache.TryGet(container, key, out long cached)) return cached;

            long value = 0;
            List<GameItem> children = container.FindAllChildItems();
            if (children != null)
                foreach (GameItem child in children)
                    if (child != null) value += child.GetCurrentValue();

            EpochCache.Store(container, key, value);
            return value;
        }
    }
}
