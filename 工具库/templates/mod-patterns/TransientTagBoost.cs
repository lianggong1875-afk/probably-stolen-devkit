using System;
using HarmonyLib;
using UnityEngine;

namespace ModTemplates
{
    /// <summary>
    /// 「临时改数值 → 让原版照常执行 → 立刻还原」骨架。
    ///
    /// 用它给原版机器临时放大加工速度 / 产出 / 耗电，而**不替换**原版方法的实现。
    /// 这样做的好处：不与任何同类模组互斥（替换原版入口才会互斥，工坊里 3 个模组抢
    /// `RunNightlyChemicalSimulation` 就是反面教材），关掉模组立刻回原版。
    ///
    /// 来源蒸馏：Machine Fusion（工坊 3845287659，ProgressSpeedPatch）。零能力。
    ///
    /// 三个必须照抄的细节：
    ///   1. `__state` 用 **class** 且初值 null —— null 表示"这次没动过"，还原天然幂等；
    ///   2. 同时挂 Postfix 与 **Finalizer** —— 原版方法抛异常也必须还原，否则数值永久被放大；
    ///   3. Prefix 第一句判 `SaveManager.IsDecoding` —— 读档期间改标签会被写进存档。
    /// </summary>
    internal static class TransientTagBoost
    {
        /// <summary>一次 boost 的现场：原值 + 目标物品 + 标签名。null 表示没改过。</summary>
        internal sealed class State
        {
            internal GameItem Item;
            internal string TagKey;
            internal int Original;
        }

        /// <summary>把 int 标签临时改成 <paramref name="value"/>，返回现场供还原用。</summary>
        /// <remarks>标签不存在或读取失败时返回 null（表示"没动过"），调用方直接放行原版即可。</remarks>
        internal static State Begin(GameItem item, string tagKey, int value)
        {
            if (item == null || string.IsNullOrEmpty(tagKey)) return null;
            int original;
            try { original = item.GetTagReadonly(tagKey).GetInt(); }
            catch (Exception) { return null; }                 // 没这个标签 = 不是目标机器，不碰
            if (original == value) return null;                // 没变化，别白写一次

            Write(item, tagKey, value);
            return new State { Item = item, TagKey = tagKey, Original = original };
        }

        /// <summary>还原现场。可以重复调用（null 时直接返回）。</summary>
        internal static void Restore(State state)
        {
            if (state == null || state.Item == null) return;
            Write(state.Item, state.TagKey, state.Original);
        }

        /// <summary>按倍率算新值（四舍五入，最低 1，避免把非 0 值算成 0 导致机器停摆）。</summary>
        internal static int Scale(int original, float factor)
        {
            int scaled = (int)Math.Round(original * factor);
            return scaled < 1 ? 1 : scaled;
        }

        private static void Write(GameItem item, string tagKey, int value)
        {
            try
            {
                item.ModifyTag(tagKey, ts => ts.Enable().SetInt(value), true);
                item.SyncModifiedState();                       // 不同步的话，读档可能拿到旧值
            }
            catch (Exception e)
            {
                Debug.LogError("[TransientTagBoost] modify tag failed: " + tagKey + " -> " + e);
            }
        }
    }

    /// <summary>
    /// 示例：把「机器加工速度」按等级临时放大，让原版 `ContinueProgressTypeMachine` 照常跑。
    /// 想换靶点就换方法名与标签名，骨架不用动。常见的靶点：
    ///   <list type="bullet">
    ///   <item><c>MachineProgressHelper.ContinueProgressTypeMachine</c> + <c>CURRENT_PROCESSING_SPEED_TAG</c></item>
    ///   <item><c>MachineryHelper.GetMachinePowerUsage</c>（耗电，改返回值而不是改标签）</item>
    ///   <item><c>MachineMoistureFarm.GetOutputVolume</c> / <c>MachinePurifier.PurifyContainer</c></item>
    ///   <item><c>HydroponicHelper.ContinueGrowing</c>（水培生长）</item>
    ///   </list>
    /// </summary>
    [HarmonyPatch(typeof(MachineProgressHelper), "ContinueProgressTypeMachine")]
    internal static class TransientSpeedBoostExamplePatch
    {
        /// <summary>把这个换成你自己的等级来源（物品标签 / ModData / 网络科技树…）。</summary>
        private static int LevelOf(GameItem machine)
        {
            try { return Math.Max(1, machine.GetTagReadonly("MY_MOD_MACHINE_LEVEL_INT").GetInt()); }
            catch (Exception) { return 1; }
        }

        [HarmonyPrefix]
        private static void Prefix(GameItem machine, out TransientTagBoost.State __state)
        {
            __state = null;
            try
            {
                if (machine == null) return;
                if (SaveManager.IsDecoding) return;                 // 读档中：一个字都不改
                int level = LevelOf(machine);
                if (level <= 1) return;                             // 1 级 = 原版行为，完全不动
                int speed;
                try { speed = machine.GetTagReadonly("CURRENT_PROCESSING_SPEED_TAG").GetInt(); }
                catch (Exception) { return; }                       // 不是进度型机器
                if (speed == 0) return;
                __state = TransientTagBoost.Begin(machine, "CURRENT_PROCESSING_SPEED_TAG",
                                                  TransientTagBoost.Scale(speed, 1f + 0.8f * (level - 1)));
            }
            catch (Exception e) { Debug.LogError("[TransientSpeedBoost] prefix failed: " + e); }
        }

        [HarmonyPostfix]
        private static void Postfix(TransientTagBoost.State __state)
        {
            try { TransientTagBoost.Restore(__state); }
            catch (Exception e) { Debug.LogError("[TransientSpeedBoost] postfix restore failed: " + e); }
        }

        /// <summary>原版方法抛异常时也必须还原，否则数值永久被放大。</summary>
        [HarmonyFinalizer]
        private static Exception Finalizer(TransientTagBoost.State __state, Exception __exception)
        {
            try { TransientTagBoost.Restore(__state); }
            catch (Exception e) { Debug.LogError("[TransientSpeedBoost] finalizer restore failed: " + e); }
            return __exception;                                     // 原样放行，不吞原版异常
        }
    }
}
