using System;
using Newtonsoft.Json.Linq;

namespace ModDevBridge;

internal sealed class InspectionTools
{
    readonly ItemQueries items;
    public InspectionTools(ItemQueries items) { this.items = items; }
    public JObject State(string world)
    {
        var ui = InspectionUIManager.Instance;
        var target = ui == null ? null : ui.currentInspectingItem;
        return new JObject { ["worldId"] = world,["available"] = ui != null,
            ["panelActive"] = ui?.panel != null ? (bool?)ui.panel.activeSelf : null,
            ["activeInHierarchy"] = ui?.panel != null ? (bool?)ui.panel.activeInHierarchy : null,
            ["appraisalPanelActive"] = ui?.appraisalPanel != null ? (bool?)ui.appraisalPanel.activeSelf : null,
            ["confrontPanelActive"] = ui?.confrontPanel != null ? (bool?)ui.confrontPanel.activeSelf : null,
            ["itemRef"] = target == null ? null : items.Remember(target,world),["itemId"] = target?.identifier,
            ["itemAlive"] = target == null ? (bool?)null : !(target is GameItemElement e) || !e.IsDestroyed(),
            ["verification"] = "native_target_and_active_flags_only" };
    }
    public JObject Operate(string world, JObject args, ResourceScope owner, Func<string> currentWorld)
    {
        string action = (string)args["action"];
        if (action != "open" && action != "close") throw new ArgumentException("action must be open or close");
        if (owner == null) throw new BridgeFailure("session_required","Inspection operations require an owning test session","inspection.preflight");
        var target = items.Resolve(world,(string)args["item_ref"],allowDestroyed: action == "close");
        var ui = InspectionUIManager.Instance;
        if (ui == null || ui.panel == null) throw new BridgeFailure("inspection_unavailable","Native inspection UI is unavailable","inspection.preflight");
        var before = State(world); var previous = ui.currentInspectingItem;
        bool open = action == "open";
        if (open && previous != null && !ReferenceEquals(previous,target) && ui.panel.activeSelf && (bool?)args["replace"] != true)
            throw new BridgeFailure("inspection_conflict","Another item is open; pass replace=true to switch the native inspection","inspection.preflight",new { before });
        if (!open && previous != null && !ReferenceEquals(previous,target))
            throw new BridgeFailure("inspection_target_mismatch","Current inspection changed; no window was closed","inspection.preflight",new { before });
        var result = new JObject { ["action"] = action,["before"] = before,["nativeInvoked"] = false,["resourceRegistered"] = false,
            ["completed"] = false,["effectVerification"] = "native_target_and_active_flags_only",
            ["cleanupScope"] = "Close this target only if still current in the same UI/world; previous UI is not restored",
            ["sideEffectsNotRestored"] = new JArray("Native item/UI initialization callbacks, resets, audio and item tag/ID changes") };
        bool needed = open ? !ReferenceEquals(previous,target) || !ui.panel.activeSelf : previous != null || ui.panel.activeSelf;
        if (needed && open)
        {
            owner.Track("inspection:"+(string)args["item_ref"],() => {
                if (world != currentWorld() || !ReferenceEquals(InspectionUIManager.Instance,ui))
                    throw new CleanupSkippedException("Inspection world/UI changed; later UI preserved");
                if (ui.currentInspectingItem == null && !ui.panel.activeSelf) return;
                if (!ReferenceEquals(ui.currentInspectingItem,target)) throw new CleanupSkippedException("Inspection target changed; later UI preserved");
                ui.CloseUI();
                if (ui.currentInspectingItem != null || ui.panel.activeSelf) throw new InvalidOperationException("Inspection close readback mismatch");
            });
            result["resourceRegistered"] = true;
        }
        Exception failure = null;
        try
        {
            if (needed) { result["nativeInvoked"] = true; if (open) ui.OnInspectItem(target); else ui.CloseUI(); }
        }
        catch (Exception error) { failure = error; }
        try
        {
            result["after"] = State(world);
            result["targetMatched"] = ReferenceEquals(ui.currentInspectingItem,target);
            result["completed"] = open ? ReferenceEquals(ui.currentInspectingItem,target) && ui.panel.activeSelf : ui.currentInspectingItem == null && !ui.panel.activeSelf;
        }
        catch (Exception error) { result["readbackError"] = BridgeFailure.Describe(error,"inspection.readback"); failure = failure ?? error; }
        if (failure != null) throw new BridgeFailure("inspection_operation_failed",failure.Message,"inspection."+action,result,inner:failure);
        return result;
    }
}
