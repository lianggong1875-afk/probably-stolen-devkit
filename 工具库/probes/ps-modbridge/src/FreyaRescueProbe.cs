// FreyaRescueProbe.cs —— 剧情客户卡死时的救急探针（2026-11-26）
//
// 背景：FreyaStory.NewDialogueClient 造的剧情客户是 dismissable = false，
// 正常靠选项节点的 endAction 调 CurrentClientLeave() 自己离场。
// 一旦那条 endAction 没被排上（例如选项后没有回复台词），
// 客户就永久钉在柜台上，遣散按钮被 dismissable 挡住，玩家看到「现在无法遣散当前顾客」，
// 整个存档无法推进。这个探针只做一件事：绕开 dismissable 直接遣散。
using System;
using ModDevBridge;

namespace FreyaRescue
{
    public sealed class FreyaRescueProbe : ModDevBridge.IDevModule
    {
        public string Start()
        {
            return "FreyaRescueProbe ready: state | rescue";
        }

        public string Execute(string command)
        {
            string cmd = (command ?? "").Trim().ToLowerInvariant();
            if (cmd.Length == 0) return "empty command; try state";
            if (cmd == "state") return State();
            if (cmd == "rescue") return Rescue();
            return "unknown: " + cmd + " (try state | rescue)";
        }

        public void Stop() { }

        private static string State()
        {
            if (!PlayerStore.HasInstance) return "PlayerStore not ready";
            PlayerStore ps = PlayerStore.Instance;
            StoreClientInstance inst = ps.currentClientInstance;
            if (inst == null) return "no current client (counter is empty)";
            StoreClient bp = inst.GetClientBlueprint();
            if (bp == null) return "current client has null blueprint";
            return "id=" + bp.identifier
                + " | name=" + bp.displayName
                + " | intent=" + bp.clientIntent
                + " | dismissable=" + bp.dismissable
                + " | nonShootable=" + bp.nonShootable
                + " | arrestable=" + bp.arrestable;
        }

        private static string Rescue()
        {
            if (!PlayerStore.HasInstance) return "PlayerStore not ready";
            PlayerStore ps = PlayerStore.Instance;
            StoreClientInstance inst = ps.currentClientInstance;
            if (inst == null) return "no current client; nothing to do";

            StoreClient bp = inst.GetClientBlueprint();
            string who = bp == null ? "(null blueprint)" : (bp.identifier + " / " + bp.displayName);

            // 绕开 dismissable 检查直接走原版遣散：清 OnDismiss、销毁非本人货、UI 离场。
            // callNextWhenPossible = false：不自动叫下一位，交回玩家自己摇铃。
            ps.DismissCurrentClient(callNextWhenPossible: false, right: true, interruptDialog: true);

            return "rescue: dismissed " + who
                + " | current now = " + (ps.currentClientInstance == null ? "null (ok)" : "still present");
        }
    }
}
