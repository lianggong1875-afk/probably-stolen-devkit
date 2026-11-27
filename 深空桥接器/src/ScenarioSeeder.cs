using System;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace ModDevBridge;

internal static class ScenarioSeeder
{
    static readonly string[] AutoOrder = { "inv", "back", "front" };
    static readonly string[] Places = { "auto", "inv", "back", "front", "trash", "hidden", "showcase" };
    static bool Alive(GameItem item) => item != null && (!(item is GameItemElement e) || !e.IsDestroyed());
    static JObject Measure(string world, string id) => new ItemQueries().Find(world,new JObject {
        ["item_id"] = id,["max_nodes"] = 10000,["max_depth"] = 64,["limit"] = 1 });

    public static object Seed(JObject args, ResourceScope owner, string world, Func<string> currentWorld)
    {
        if (!PlayerStore.HasInstance || EmporiumEntry.Instance == null) throw new InvalidOperationException("No loaded save");
        var store = PlayerStore.Instance; var emporium = EmporiumEntry.Instance;
        bool track = (bool?)args["track"] ?? true, strict = (bool?)args["strict"] ?? false;
        if (track && owner == null) throw new BridgeFailure("session_required","Tracking requires an explicit owning session","seed.preflight");
        if (args["cash"] != null && args["cash_delta"] != null) throw new ArgumentException("Use cash (absolute target) or cash_delta (increment), not both");
        int cashBefore = store.GetCash();
        int? target = args["cash"] != null ? (int?)args["cash"] : args["cash_delta"] != null ? checked(cashBefore + (int)args["cash_delta"]) : (int?)null;
        int delta = target.HasValue ? checked(target.Value - cashBefore) : 0;
        if (args["items"] != null && !(args["items"] is JArray)) throw new ArgumentException("items must be an array");
        if (args["modData"] != null && !(args["modData"] is JObject)) throw new ArgumentException("modData must be an object");
        int totalUnits = 0;
        if (args["items"] is JArray specs)
        {
            if (specs.Count > 100) throw new ArgumentException("At most 100 item entries");
            foreach (var raw in specs)
            {
                if (!(raw is JObject spec) || string.IsNullOrWhiteSpace((string)spec["id"])) throw new ArgumentException("items[].id is required");
                int units = (int?)spec["count"] ?? 1;
                if (units < 1 || units > 999 || (totalUnits += units) > 1000) throw new ArgumentException("count is 1..999 units; at most 1000 requested units per call");
                string id = (string)spec["id"], where = (string)spec["where"] ?? "auto";
                if (!Places.Contains(where)) throw new ArgumentException("Unknown inventory alias: " + where);
                if (where != "auto" && FindGrid(emporium,where) == null) throw new ArgumentException("Inventory unavailable: " + where);
                if (DirectoryMaster.GetFactory<GameItem>(id) == null)
                    throw new BridgeFailure("item_factory_not_found","No registered GameItem factory: " + id,"seed.preflight");
            }
        }
        if (args["modData"] is JObject data && data.Properties().Any(p => p.Value.Type != JTokenType.String && p.Value.Type != JTokenType.Null))
            throw new ArgumentException("modData values must be strings or null");
        var warnings = new JArray(); var itemResults = new JArray();
        var result = new JObject { ["trackingRequested"] = track,["tracked"] = false,["registeredResources"] = 0,
            ["completed"] = false,["warnings"] = warnings,["items"] = itemResults,["factoryCalls"] = 0,
            ["effectVerification"] = "selected_state_only",["sideEffectsNotRestored"] = new JArray() };
        int resources = 0; bool complete = true, covered = true;
        string stage = "seed.cash";
        Action checkpoint = () => {
            if (world != currentWorld() || !ReferenceEquals(PlayerStore.Instance,store))
                throw new InvalidOperationException("World/save changed during seed; inspect partial result");
        };
        Action<string,Action> register = (name,cleanup) => {
            TrackSave(owner,store,world,currentWorld,name,cleanup); resources++; result["registeredResources"] = resources;
        };
        try
        {
            checkpoint();
            if (target.HasValue)
            {
                int applied = cashBefore;
                var cash = new JObject { ["mode"] = args["cash_delta"] != null ? "add" : "set",["before"] = cashBefore,
                    ["target"] = target.Value,["requestedDelta"] = delta,["after"] = cashBefore,["actualDelta"] = 0,["changed"] = false };
                result["cash"] = cash;
                if (delta != 0)
                {
                    if (track) register("cash",() => {
                        if (store.GetCash() != applied) throw new CleanupSkippedException("Cash changed after seed; later value preserved");
                        try { store.ModCash(checked(cashBefore-store.GetCash()),playSound:false); }
                        finally { applied = store.GetCash(); }
                        if (applied != cashBefore) throw new InvalidOperationException("Cash restoration readback mismatch");
                    });
                    try { store.ModCash(delta,playSound:false); }
                    finally { applied = store.GetCash(); cash["after"] = applied; cash["actualDelta"] = (long)applied-cashBefore; cash["changed"] = applied != cashBefore; }
                }
                cash["targetMatched"] = applied == target.Value;
                if (applied != target.Value) { complete = false; if (strict) throw new InvalidOperationException("Cash target readback mismatch"); }
            }
            if (args["items"] is JArray items)
            foreach (JObject spec in items)
            {
                checkpoint(); stage = "seed.items";
                string id = (string)spec["id"], where = (string)spec["where"] ?? "auto";
                int requested = (int?)spec["count"] ?? 1, accepted = 0;
                var before = Measure(world,id); var details = new JArray();
                var row = new JObject { ["id"] = id,["requestedUnits"] = requested,["acceptedUnits"] = 0,["where"] = where,
                    ["factoryCalls"] = 0,["createdInstances"] = 0,["items"] = details,["complete"] = false,["quantityVerified"] = false };
                itemResults.Add(row);
                try
                {
                    while (accepted < requested)
                    {
                        checkpoint();
                        row["factoryCalls"] = (int)row["factoryCalls"]+1;
                        result["factoryCalls"] = (int)result["factoryCalls"]+1;
                        result["sideEffectsNotRestored"] = new JArray("Native unique-ID allocation, permit handling and callback side effects are outside cleanup scope");
                        var item = DirectoryMaster.Item(id);
                        if (!Alive(item)) throw new InvalidOperationException("Registered factory returned null/destroyed item");
                        row["createdInstances"] = (int)row["createdInstances"]+1;
                        if (item.parentInventory != null) { covered = false; throw new InvalidOperationException("Factory returned an attached item; it was not claimed or destroyed"); }
                        int batch = item.unitCount, expectedUnits = batch;
                        var expectedParent = item.parentInventory;
                        var detail = new JObject { ["nativeUniqueId"] = item.uniqueId,["factoryUnits"] = batch,["acceptedUnits"] = 0,["tracked"] = track,["placement"] = "not_attempted" };
                        details.Add(detail);
                        if (track) register("item:"+id,() => {
                            if (!Alive(item)) return;
                            if (!ReferenceEquals(item.parentInventory,expectedParent) || item.unitCount != expectedUnits)
                                throw new CleanupSkippedException("Created item moved or quantity changed; later item preserved");
                            try { item.Destroy(); }
                            finally { expectedParent = item.parentInventory; expectedUnits = item.unitCount; }
                            if (item.parentInventory != null || item is GameItemElement && Alive(item))
                                throw new InvalidOperationException("Created item destruction readback mismatch");
                        });
                        try
                        {
                            if (batch < 1 || batch > requested-accepted)
                            { detail["placement"] = "factory_batch_exceeds_remaining_or_invalid"; break; }
                            foreach (string name in where == "auto" ? AutoOrder : new[] { where })
                            {
                                checkpoint(); var grid = FindGrid(emporium,name); if (grid == null) continue;
                                int got;
                                if (track)
                                {
                                    var slot = grid.TryFindOneValidInventorySlot(item);
                                    if (slot == null || !slot.IsValid()) { detail["placement"] = "no_native_slot"; continue; }
                                    if (slot.targetItem != null || slot.numTransfer < batch || item.MaxNumRemove() < batch ||
                                        !ReferenceEquals(slot.item,item) || slot.inventory == null || slot.itemGridShape == null)
                                    { detail["placement"] = "tracked_merge_or_split_refused"; continue; }
                                    detail["placement"] = "native_call_started";
                                    got = slot.TryAcceptOnce(batch);
                                }
                                else
                                {
                                    detail["placement"] = "native_call_started";
                                    int amount = item.unitCount, remaining = GraphUtils.TryAcceptAll(grid,item);
                                    got = amount-remaining;
                                    if (remaining < 0 || remaining > amount) throw new InvalidOperationException("Native placement returned invalid remaining units");
                                }
                                if (got < 0 || got > batch-(int)detail["acceptedUnits"]) throw new InvalidOperationException("Native placement accepted count outside current batch");
                                accepted += got; detail["acceptedUnits"] = (int)detail["acceptedUnits"]+got;
                                row["acceptedUnits"] = accepted; detail["where"] = name;
                                if (track && (got != batch || item.parentInventory == null || item.unitCount != batch))
                                { covered = false; throw new InvalidOperationException("Whole-item placement readback mismatch; inspect actual state"); }
                                if ((int)detail["acceptedUnits"] >= batch) { detail["placement"] = "accepted"; break; }
                            }
                            if ((int)detail["acceptedUnits"] != batch) { if (!track) detail["placement"] = "partial_or_no_space"; break; }
                        }
                        finally
                        {
                            expectedUnits = item.unitCount; expectedParent = item.parentInventory;
                            detail["parent"] = item.parentInventory?.identifier; detail["sourceUnitsAfter"] = item.unitCount;
                            if (world == currentWorld() && Alive(item) && item.parentInventory == null)
                            {
                                try { item.Destroy(); detail["unplacedSourceCleanup"] = "destroy_returned"; }
                                catch (Exception error) { covered = false; detail["unplacedSourceCleanup"] = BridgeFailure.Describe(error,"seed.discard"); throw; }
                                finally { expectedUnits = item.unitCount; expectedParent = item.parentInventory; }
                            }
                        }
                    }
                }
                finally
                {
                    var after = Measure(world,id);
                    bool exact = (bool)before["countsExactWithinScope"] && (bool)after["countsExactWithinScope"] && world == currentWorld();
                    long observed = (long)after["unitCount"]-(long)before["unitCount"];
                    row["measurement"] = new JObject { ["scope"] = "query_items_store_roots",["beforeUnits"] = before["unitCount"],["afterUnits"] = after["unitCount"],["complete"] = exact };
                    row["observedUnitChange"] = exact ? new JValue(observed) : JValue.CreateNull();
                    row["quantityVerified"] = exact && observed == accepted;
                    row["complete"] = accepted == requested && exact && observed == accepted;
                    row["unacceptedUnits"] = requested-accepted;
                }
                if (!(bool)row["complete"])
                { complete = false; warnings.Add("Incomplete item request: "+id); if (strict) throw new InvalidOperationException("Could not verify all requested units: "+id); }
            }
            if (args["modData"] is JObject modData)
            {
                checkpoint(); stage = "seed.modData"; var values = new JArray(); result["modData"] = values;
                var dict = store.ModData();
                foreach (var p in modData.Properties())
                {
                    string key = p.Name; bool existed = dict.TryGetValue(key,out string old), remove = p.Value.Type == JTokenType.Null;
                    string next = remove ? null : (string)p.Value;
                    bool changed = existed != !remove || existed && old != next;
                    values.Add(new JObject { ["key"] = key,["before"] = existed ? old : null,["after"] = next,["removed"] = remove,["changed"] = changed });
                    if (!changed) continue;
                    if (track) register("modData:"+key,() => {
                        bool present = dict.TryGetValue(key,out string current);
                        if (present != !remove || present && current != next) throw new CleanupSkippedException("modData changed after seed: "+key);
                        if (existed) dict[key] = old; else dict.Remove(key);
                    });
                    if (remove) dict.Remove(key); else dict[key] = next;
                }
            }
            checkpoint(); result["completed"] = complete;
            return result;
        }
        catch (Exception error)
        {
            result["resources"] = owner == null ? new JArray() : JArray.FromObject(owner.Report());
            throw new BridgeFailure("seed_failed",error.Message,stage,result,"Inspect actual changes; strict is not a transaction",error);
        }
        finally { result["tracked"] = track && resources > 0 && covered; }
    }
    static void TrackSave(ResourceScope owner, PlayerStore store, string world, Func<string> currentWorld, string name, Action cleanup)
    {
        owner.Track(name,() => {
            if (world != currentWorld() || !PlayerStore.HasInstance || !ReferenceEquals(PlayerStore.Instance,store))
                throw new CleanupSkippedException("World/save changed; restoration was not attempted");
            cleanup();
        });
    }
    static GraphNodeStorage FindGrid(EmporiumEntry e, string where)
    {
        switch (where)
        {
            case "inv": return e.invElement; case "back": return e.backInvinvElement; case "front": return e.frontInvinvElement;
            case "trash": return e.trashInvElement; case "hidden": return e.hiddenElement; case "showcase": return e.showcaseElement; default: return null;
        }
    }
}
