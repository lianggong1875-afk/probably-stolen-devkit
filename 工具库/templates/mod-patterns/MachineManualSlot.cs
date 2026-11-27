using System;
using System.Collections.Generic;
using UnityEngine;

namespace ModTemplates
{
    /// <summary>
    /// 把一整份文档塞进机器的「说明书槽」——玩家点开机器就能读到，不用另做 UI。
    ///
    /// 来源蒸馏：Pipeline Controller（工坊 3845199370，它把整套流水线说明塞进控制器自带的说明书槽）。
    /// 零能力。
    ///
    /// 做法：说明书槽是原版机器就有的「便签槽」，槽里放的其实是一件 `postit` 物品；
    /// 把它改成 `DOCUMENT` + 带 `MACHINE_MANUAL_TAG`，再把正文写进 `shortDescription`，
    /// 最后 `UncheckedAccept` 塞进槽里。开局与读档后各补一次（老存档也能补上）。
    ///
    /// 提醒：正文写在 `shortDescription` 里，所以别做超长文本（几万字会让 tooltip 排版炸）；
    /// 需要长文档就把内容分卷，或用 Paper（交互式纸张）那套。
    /// </summary>
    internal static class MachineManualSlot
    {
        private const string PostItId = "postit";
        private const string DocumentType = "DOCUMENT";
        private const string ManualTag = "MACHINE_MANUAL_TAG";

        /// <summary>
        /// 给机器塞一份说明书。<paramref name="docSlotInventory"/> 是「说明书槽」这个 inventory。
        /// 槽里已经有东西就不动（避免覆盖玩家自己的便签）。
        /// </summary>
        /// <returns>成功塞入返回 true。</returns>
        internal static bool Ensure(GameSlotInventory docSlotInventory, string title, string body)
        {
            if (docSlotInventory == null) return false;
            try
            {
                if (docSlotInventory.childItem != null) return false;      // 已经有东西（玩家便签），不覆盖

                GameItem doc = DirectoryMaster.Item(PostItId, true);
                if (doc == null)
                {
                    Debug.LogWarning("[MachineManualSlot] postit 拿不到，说明书没塞进去");
                    return false;
                }

                if (!string.IsNullOrEmpty(title)) doc.name = title;
                doc.shortDescription = body ?? string.Empty;
                doc.unitValue = 0;                                        // 别让说明书被当货卖钱
                doc.SetGameItemType(DocumentType);
                doc.EnableTag(ManualTag);

                GraphUtils.TryAcceptAll(docSlotInventory, doc, -1);
                return docSlotInventory.childItem == doc;                  // 引用比较校验落点
            }
            catch (Exception e)
            {
                Debug.LogError("[MachineManualSlot] ensure failed: " + e);
                return false;
            }
        }

        /// <summary>
        /// 从机器物品上取「说明书槽」的 inventory。机器没有可写便签槽时返回 null。
        /// 这条路走的是机器面板的行结构，所以调用前要确保面板已经建好（读档后 / 打开窗口时）。
        /// </summary>
        internal static GameSlotInventory FindManualSlot(GameItem machine)
        {
            if (machine == null) return null;
            try
            {
                PixelWindow window = machine.contentWindow;
                if (window == null) return null;
                MachineryPanelRow row = window.childElement as MachineryPanelRow;
                if (row == null) return null;
                MachineryPanel panel = row.GetPanel("note");               // 便签槽固定叫 "note"
                if (panel == null) return null;

                List<GraphNodeStorage> nodes = MachineryContainerUtils.Collect(panel.content);
                if (nodes == null) return null;
                foreach (GraphNodeStorage node in nodes)
                    if (node is GameSlotInventory slot) return slot;
                return null;
            }
            catch (Exception e)
            {
                Debug.LogError("[MachineManualSlot] find slot failed: " + e);
                return null;
            }
        }

        /// <summary>
        /// 用法示例（放在你自己的模组里）：
        /// <code>
        /// // 开局 / 读档后各补一次，老存档里的机器也会补上说明书
        /// ModHook.OnNewGameStarted    += _ => FillAllMine();
        /// ModHook.OnGameLoadedLate    += FillAllMine;
        /// // 玩家自己新造出来的机器，在建造完成的那一刻补
        /// // （用 OnPlaceInventorInventoryItemLate 或机器自己的 onLoaded）
        /// </code>
        /// </summary>
        internal static void FillAllMine(string machineIdentifier, string title, string body)
        {
            EmporiumEntry store = EmporiumEntry.Instance;
            if (store == null) return;
            List<GameItem> all = store.GetAllItems();
            if (all == null) return;
            foreach (GameItem item in all)
            {
                if (item == null || !string.Equals(item.identifier, machineIdentifier, StringComparison.Ordinal)) continue;
                GameSlotInventory slot = FindManualSlot(item);
                if (slot != null) Ensure(slot, title, body);
            }
        }
    }
}
