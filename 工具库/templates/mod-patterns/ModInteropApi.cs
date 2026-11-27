using System;
using System.Collections.Generic;

namespace ModTemplates
{
    /// <summary>
    /// 零能力跨模组协议模板：使用原版 ModHelper.Expose 与 ModHelper.OnMessage 暴露版本化 API，
    /// 消费方无需编译引用提供方 DLL，也不触发 reflection-external 能力扫描。
    /// 实证来源：深空电玩 (ArcInterop) / 深空当铺：可能是群友 (DspInterop)。
    /// </summary>
    internal static class ModInteropApi
    {
        internal static void RegisterApi(
            string modId,
            string version,
            Action<string, object> onCustomMessage = null)
        {
            Dictionary<string, object> api = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["version"] = version,
                ["ping"] = (Func<string>)(() => modId + "@" + version),
            };
            ModHelper.Expose(modId, "api.v1", api);

            if (onCustomMessage != null)
                ModHelper.OnMessage(modId, modId + ".command.v1", onCustomMessage);
        }

        internal static int Broadcast(string senderModId, string targetChannel, Dictionary<string, object> payload)
        {
            return ModHelper.SendMessage(senderModId, targetChannel, payload);
        }
    }
}
