using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace ModDevBridge;

// Machine sandbox: turns "wait two nights to see whether a machine actually
// produces" into one call, without touching any native rule. Everything here
// goes through the game's own doors:
//   - DebugPanel.SkipNightMachine() is the shipped "skip night (machines)" helper.
//   - MachineProgressHelper.Start/Continue/Finish are the only legitimate way to
//     move machine progress; the two progress tags below are the ones the game
//     itself reads back.
// This class only sequences those calls and reports before/after values.
internal static class MachineSandbox
{
    private const string StateTag = "MACHINE_STATE_TAG";
    private const string CurrentTag = "MACHINE_PROGRESS_CURRENT_TAG";
    private const string TargetTag = "MACHINE_PROGRESS_TARGET_TAG";
    private const string SpeedTag = "CURRENT_PROCESSING_SPEED_TAG";
    // The desequencer keeps its own progress/state under different names, and the
    // game's own constant has a typo baked into the value: DESEQUENCER_* names,
    // "DESEQUQUENCER_PROGRESS_TAG" value. Read the value, never re-derive the name.
    private const string DesequencerStateTag = "DESEQUENCER_STATE_TAG";
    private const string DesequencerProgressTag = "DESEQUQUENCER_PROGRESS_TAG";
    private const string EnergyTag = "power_source_item_energy";
    private const string MaxEnergyTag = "power_source_item_max_energy";
    private const string PowerSourceTag = "power_source_item";

    // PlayerStore.FindAllItem(isOwn) defaults to true, which silently drops items
    // that lost IS_OWNED_TAG. Machines we care about are whatever is physically in
    // the shop, so ask for everything and filter by type ourselves.
    private static List<GameItem> AllMachines()
    {
        if (!PlayerStore.HasInstance || PlayerStore.Instance == null) return new List<GameItem>();
        List<GameItem> all;
        try { all = PlayerStore.Instance.FindAllItem(false); }
        catch { return new List<GameItem>(); }
        if (all == null) return new List<GameItem>();
        return all.Where(it => it != null &&
            (it.IsGameItemType("MACHINE") || it.IsTag("MACHINE") || it.IsTag("STANDARD_MACHINE_TAG")))
            .ToList();
    }

    private static List<GameItem> Select(JObject a)
    {
        var machines = AllMachines();
        var target = ((string)a["target"] ?? "all").Trim();
        if (target.Length == 0 || target == "all") return machines;
        return machines.Where(m =>
            string.Equals(m.identifier, target, StringComparison.OrdinalIgnoreCase) ||
            (m.name != null && m.name.IndexOf(target, StringComparison.OrdinalIgnoreCase) >= 0) ||
            m.uniqueId.ToString() == target).ToList();
    }

    private static int Progress(GameItem item)
    {
        if (item.state == null) return 0;
        int value = item.state.IsTag(CurrentTag) ? item.state.GetTag(CurrentTag).valueInt : 0;
        if (item.state.IsTag(DesequencerProgressTag))
            value = Math.Max(value, item.state.GetTag(DesequencerProgressTag).valueInt);
        return value;
    }

    private static int Target(GameItem item)
    {
        if (item.state == null || !item.state.IsTag(TargetTag)) return 0;
        return item.state.GetTag(TargetTag).valueInt;
    }

    private static string MachineState(GameItem item)
    {
        if (item.state == null) return null;
        if (item.state.IsTag(StateTag)) return item.state.GetTag(StateTag).valueString;
        if (item.state.IsTag(DesequencerStateTag)) return item.state.GetTag(DesequencerStateTag).valueString;
        return null;
    }

    // Machines hold their cell inside their own content window. Walking the item
    // graph is more general than hard-coding MachineryPanelRow -> Panels[0] ->
    // MachineryIntake, which only matches machines that use that exact UI layout.
    private static GameItem Battery(GameItem machine)
    {
        List<GameItem> children;
        try { children = GraphUtils.FindAllChildrenType<GameItem>(machine); }
        catch { return null; }
        if (children == null) return null;
        return children.FirstOrDefault(c => c != null && c.state != null &&
            c.IsTag(PowerSourceTag) && c.state.IsTag(EnergyTag));
    }

    private static int MaxEnergy(GameItem battery)
    {
        if (battery.state != null && battery.state.IsTag(MaxEnergyTag))
        {
            int declared = battery.state.GetTag(MaxEnergyTag).valueInt;
            if (declared > 0) return declared;
        }
        return 100;
    }

    private static int Energy(GameItem battery) =>
        battery.state != null && battery.state.IsTag(EnergyTag) ? battery.state.GetTag(EnergyTag).valueInt : 0;

    private static JObject Recharge(GameItem machine)
    {
        var battery = Battery(machine);
        if (battery == null) return null;
        int before = Energy(battery);
        int max = MaxEnergy(battery);
        // SetPowerSourceAt also refreshes the native energy sprite, so the UI and
        // any later inspection agree with the value we wrote.
        PowerHelper.SetPowerSourceAt(battery, max);
        return new JObject {
            ["battery"] = battery.name, ["energyBefore"] = before,
            ["energyAfter"] = Energy(battery), ["energyMax"] = max
        };
    }

    // Mirrors PlayerStore.EndNight's three passes for one item, in the same order.
    private static void NightPass(GameItem item)
    {
        item.onCycleEndEarlySlotItemFunc?.Invoke(item, item.parentInventory, item.GetIncompleteSlotInInventory());
        item.onCycleEndSlotItemFunc?.Invoke(item, item.parentInventory, item.GetIncompleteSlotInInventory());
        item.onCycleEndLateSlotItemFunc?.Invoke(item, item.parentInventory, item.GetIncompleteSlotInInventory());
    }

    private static bool PushProgress(GameItem item)
    {
        int target = Target(item);
        if (target <= 0 || item.state == null) return false;
        bool moved = false;
        if (item.state.IsTag(CurrentTag))
        {
            item.ModifyTag(CurrentTag, state => state.Enable().SetInt(target));
            moved = true;
        }
        if (item.state.IsTag(DesequencerProgressTag))
        {
            item.ModifyTag(DesequencerProgressTag, state => state.Enable().SetInt(target));
            moved = true;
        }
        return moved;
    }

    private static JObject Describe(GameItem item)
    {
        var row = new JObject {
            ["uniqueId"] = item.uniqueId.ToString(),
            ["name"] = item.name,
            ["identifier"] = item.identifier,
            ["progress"] = Progress(item),
            ["target"] = Target(item),
            ["machineState"] = MachineState(item),
            ["hasBattery"] = Battery(item) != null
        };
        if (item.state != null && item.state.IsTag(SpeedTag))
            row["speedPerCycle"] = item.state.GetTag(SpeedTag).valueInt;
        return row;
    }

    private static JObject Sweep(JObject a, bool recharge, Func<GameItem, JObject> action)
    {
        var targets = Select(a);
        if (targets.Count == 0)
            throw new KeyNotFoundException("No machines matched target '" + ((string)a["target"] ?? "all") + "'.");
        var rows = new JArray();
        foreach (var machine in targets)
        {
            var row = new JObject {
                ["name"] = machine.name, ["identifier"] = machine.identifier,
                ["uniqueId"] = machine.uniqueId.ToString(),
                ["progressBefore"] = Progress(machine), ["target"] = Target(machine)
            };
            if (recharge)
            {
                var cell = Recharge(machine);
                if (cell != null) row["autoRecharge"] = cell;
            }
            var extra = action(machine);
            if (extra != null) foreach (var p in extra.Properties()) row[p.Name] = p.Value;
            row["progressAfter"] = Progress(machine);
            row["machineState"] = MachineState(machine);
            rows.Add(row);
        }
        return new JObject { ["count"] = rows.Count, ["machines"] = rows };
    }

    public static object Find(JObject a)
    {
        var targets = Select(a);
        var rows = new JArray(targets.Select(m => (JToken)Describe(m)));
        return new JObject { ["count"] = rows.Count, ["scope"] = "FindAllItem(false)", ["machines"] = rows };
    }

    // The shipped helper: early pass, cold-marker reset, main pass, late pass,
    // cold-feature expiry. No progress tag is touched directly.
    public static object SkipNight(JObject a)
    {
        bool recharge = (bool?)a["auto_recharge"] ?? true;
        var targets = Select(a);
        if (targets.Count == 0)
            throw new KeyNotFoundException("No machines matched target '" + ((string)a["target"] ?? "all") + "'.");
        JArray recharged = null;
        if (recharge)
        {
            recharged = new JArray();
            foreach (var m in targets)
            {
                var cell = Recharge(m);
                if (cell != null) { cell["machine"] = m.name; recharged.Add(cell); }
            }
        }
        int before = targets.Sum(Progress);
        DebugPanel.SkipNightMachine();
        var after = new JArray(targets.Select(m => (JToken)Describe(m)));
        return new JObject {
            ["mode"] = "skip_night", ["native"] = "DebugPanel.SkipNightMachine()",
            ["count"] = after.Count, ["progressSumBefore"] = before,
            ["progressSumAfter"] = targets.Sum(Progress),
            ["recharged"] = recharged ?? new JArray(), ["machines"] = after
        };
    }

    public static object FastForward(JObject a)
    {
        string mode = (((string)a["mode"] ?? "one_cycle")).ToLowerInvariant();
        bool recharge = (bool?)a["auto_recharge"] ?? true;
        int nights = (int?)a["nights"] ?? 1;
        if (mode == "nights" && (nights < 1 || nights > 30))
            throw new ArgumentException("nights must be 1..30");
        if (mode != "one_cycle" && mode != "complete" && mode != "nights")
            throw new ArgumentException("mode must be one_cycle, complete or nights");

        var body = Sweep(a, recharge, machine =>
        {
            var row = new JObject { ["mode"] = mode };
            if (mode == "complete")
            {
                row["progressPinned"] = PushProgress(machine);
                NightPass(machine);
            }
            else
            {
                int passes = mode == "nights" ? nights : 1;
                for (int i = 0; i < passes; i++)
                {
                    if (recharge && mode == "nights") Recharge(machine);
                    NightPass(machine);
                }
                row["passes"] = passes;
            }
            return row;
        });
        body["mode"] = mode;
        if (mode == "nights") body["nights"] = nights;
        return body;
    }

    public static object RechargeAll(JObject a)
    {
        var targets = Select(a);
        if (targets.Count == 0)
            throw new KeyNotFoundException("No machines matched target '" + ((string)a["target"] ?? "all") + "'.");
        var rows = new JArray();
        int changed = 0;
        foreach (var machine in targets)
        {
            var cell = Recharge(machine);
            if (cell == null) continue;
            cell["machine"] = machine.name;
            if ((int)cell["energyBefore"] != (int)cell["energyAfter"]) changed++;
            rows.Add(cell);
        }
        return new JObject { ["count"] = rows.Count, ["changed"] = changed, ["batteries"] = rows };
    }
}
