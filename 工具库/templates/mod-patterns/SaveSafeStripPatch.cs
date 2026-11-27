using System;
using System.Collections.Generic;

namespace ModTemplates
{
    /// <summary>
    /// 自定义睡前服务 / 电话条目写盘剥离模板（零能力，卸载零污染）：
    /// 在 PlayerStore.SaveGame / InitialSave 的 Prefix 中临时摘除自定义 StoreService 与 StorePhoneClient，
    /// 把易失状态（如 isUsing / currentCooldown）回写到模组私有存档键，Finalizer 无条件恢复内存态。
    /// 这样玩家卸载模组后，原版存档 100% 干净，不会出现幽灵扣款或电话簿报错。
    /// 实证来源：深空当铺：可能是群友 (FreyaNightly / FreyaPhone / FreyaLocalization)。
    /// </summary>
    internal static class SaveSafeStripHelper
    {
        private static readonly List<StoreService> DetachedServices = new List<StoreService>();
        private static readonly Dictionary<long, StorePhoneClient> DetachedPhones = new Dictionary<long, StorePhoneClient>();

        internal static void DetachOwnedEntries(
            PlayerStore store,
            Func<string, bool> isOwnServiceId,
            Func<long, bool> isOwnPhoneNumber,
            Action<StoreService> onSaveServiceState = null,
            Action<long, StorePhoneClient> onSavePhoneState = null)
        {
            DetachedServices.Clear();
            DetachedPhones.Clear();
            if (store == null) return;

            if (store.storeService != null && isOwnServiceId != null)
            {
                for (int i = store.storeService.Count - 1; i >= 0; i--)
                {
                    StoreService s = store.storeService[i];
                    if (s != null && isOwnServiceId(s.id))
                    {
                        if (onSaveServiceState != null) onSaveServiceState(s);
                        DetachedServices.Add(s);
                        store.storeService.RemoveAt(i);
                    }
                }
            }

            if (store.PhoneClientDict != null && isOwnPhoneNumber != null)
            {
                List<long> toRemove = null;
                foreach (KeyValuePair<long, StorePhoneClient> kv in store.PhoneClientDict)
                {
                    if (isOwnPhoneNumber(kv.Key))
                    {
                        if (toRemove == null) toRemove = new List<long>();
                        toRemove.Add(kv.Key);
                    }
                }
                if (toRemove != null)
                {
                    for (int i = 0; i < toRemove.Count; i++)
                    {
                        long num = toRemove[i];
                        StorePhoneClient c = store.PhoneClientDict[num];
                        if (onSavePhoneState != null && c != null) onSavePhoneState(num, c);
                        DetachedPhones[num] = c;
                        store.PhoneClientDict.Remove(num);
                    }
                }
            }
        }

        internal static void RestoreOwnedEntries(PlayerStore store)
        {
            if (store != null)
            {
                if (store.storeService != null)
                {
                    for (int i = 0; i < DetachedServices.Count; i++)
                    {
                        StoreService s = DetachedServices[i];
                        if (!store.storeService.Contains(s)) store.storeService.Add(s);
                    }
                }
                if (store.PhoneClientDict != null)
                {
                    foreach (KeyValuePair<long, StorePhoneClient> kv in DetachedPhones)
                        store.PhoneClientDict[kv.Key] = kv.Value;
                }
            }
            DetachedServices.Clear();
            DetachedPhones.Clear();
        }
    }
}
