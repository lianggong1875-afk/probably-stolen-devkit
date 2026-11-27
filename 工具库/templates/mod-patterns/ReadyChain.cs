using UnityEngine;

namespace ModTemplates
{
    /// <summary>
    /// 标准店内就绪检查链与 Unity 假 null 检测。
    /// 来源蒸馏：Developer Tools (SpawnEngine.Ready) + MCPBridge。
    /// 零能力，直接复制修改命名空间即可使用。
    /// </summary>
    internal static class ReadyChain
    {
        /// <summary>
        /// 确认玩家已进入店内存档且核心单例/背包已就绪。
        /// 必须先判 PlayerStore.HasInstance，绝不直接读 PlayerStore.Instance（会触发半初始化实例化）。
        /// </summary>
        internal static bool IsStoreReady()
        {
            if (!PlayerStore.HasInstance)
                return false;
            if (EmporiumEntry.Instance == null || EmporiumEntry.Instance.invElement == null)
                return false;
            if (StoreUIManager.Instance == null)
                return false;
            if (PlayerStore.Instance.playerLocation != 0)
                return false;
            if (MapUIManager.Instance != null && MapUIManager.Instance.IsOutside)
                return false;
            return true;
        }

        /// <summary>
        /// 检测 Unity 假 null（C# 引用仍在但底层原生对象已 Destroy）。
        /// </summary>
        internal static bool IsDestroyedUnityObject(object obj)
        {
            return obj is UnityEngine.Object u && !object.ReferenceEquals(u, null) && u == null;
        }
    }
}
