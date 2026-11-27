using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace ModDevBridge;

// Native customer blueprints and queue membership, not a dialogue/quest engine.
internal sealed class CustomerTools
{
    sealed class Reference { public string Id, World; public WeakReference Customer; public DateTime Issued; }
    readonly List<Reference> references = new List<Reference>();
    readonly Func<DateTime> clock;
    public CustomerTools(Func<DateTime> clock = null) { this.clock = clock ?? (() => DateTime.UtcNow); }
    static readonly string[] Presets = { "appraisal_hand", "aug_med" };
    static JArray PresetInfo() => new JArray(
        new JObject { ["preset"] = "appraisal_hand", ["nativeFactory"] = "StoreClientListAppraisal.CreateAppraisal1",
            ["description"] = "Native hand-appraisal customer; dialogue later creates two random hands on the counter" },
        new JObject { ["preset"] = "aug_med", ["nativeFactory"] = "StoreClientList.CreateAugMed",
            ["description"] = "Native medicine-buying aug; optional tier 1..4, omitted means native random roll" });
    static StoreClient Current(PlayerStore store) => store.currentClientInstance?.GetClientBlueprint();
    string Remember(StoreClient customer, string world)
    {
        references.RemoveAll(r => r.World != world || clock() - r.Issued > TimeSpan.FromMinutes(5) || !r.Customer.IsAlive);
        var old = references.FirstOrDefault(r => ReferenceEquals(r.Customer.Target,customer));
        if (old != null) return old.Id;
        var entry = new Reference { Id = Guid.NewGuid().ToString("N"), World = world, Customer = new WeakReference(customer), Issued = clock() };
        references.Add(entry); while (references.Count > 512) references.RemoveAt(0); return entry.Id;
    }
    StoreClient Resolve(string reference, string world)
    {
        var entry = references.FirstOrDefault(r => r.Id == reference);
        if (entry == null || entry.World != world || clock() - entry.Issued > TimeSpan.FromMinutes(5) || !(entry.Customer.Target is StoreClient client))
            throw new BridgeFailure("customer_ref_unavailable","Customer reference is unknown, expired, collected or belongs to a previous world; query again","customers.resolve");
        return client;
    }
    JObject Describe(StoreClient customer, string world, string state, int? index, bool details)
    {
        var row = new JObject { ["customerRef"] = Remember(customer,world), ["id"] = customer.identifier,
            ["displayName"] = customer.displayName, ["intent"] = customer.clientIntent.ToString(), ["faction"] = customer.clientFaction,
            ["state"] = state, ["queueIndex"] = index, ["isAug"] = customer.isAug, ["augTier"] = customer.isAug ? (int?)customer.augTier : null,
            ["isSecurity"] = customer.isSecurity, ["isWanted"] = customer.isWanted, ["dismissable"] = customer.dismissable };
        if (details)
        {
            row["sprite"] = customer.spriteName; row["budget"] = customer.GetBudget();
            row["isAugScanned"] = customer.isAugScanned; row["augIdleTell"] = customer.augIdleTell.ToString();
            row["cooldown"] = customer.cooldown; row["cooldownStartsOnArrival"] = customer.cooldownStartsOnArrival;
            var tags = customer.clientBuyingTagList;
            row["buyingTags"] = tags == null ? new JArray() : new JArray(tags.Take(20));
            row["buyingTagsComplete"] = tags == null || tags.Count <= 20;
        }
        return row;
    }
    public JObject Query(string world, JObject args)
    {
        string scope = (string)args["scope"] ?? "all";
        int offset = ObservationOutput.Offset(args), limit = args["limit"] == null ? 20 : ObservationOutput.Limit(args);
        if (!new[] { "all","current","queue","presets" }.Contains(scope)) throw new ArgumentException("Unknown customer scope");
        string reference = (string)args["customer_ref"];
        if (scope == "presets")
        {
            if (reference != null || args["customer_id"] != null || args["name_contains"] != null || args["intent"] != null)
                throw new ArgumentException("Customer filters do not apply to preset discovery");
            var presets = PresetInfo(); var page = new JArray(presets.Skip(offset).Take(limit));
            return new JObject { ["worldId"] = world,["scope"] = scope,["entries"] = page,
                ["totalMatches"] = presets.Count,["nextOffset"] = offset + page.Count < presets.Count ? (int?)(offset + page.Count) : null,["searchComplete"] = true };
        }
        if (!PlayerStore.HasInstance || PlayerStore.Instance.storeClientManager?.clientStack == null)
            throw new BridgeFailure("customer_queue_unavailable","Loaded store customer queue is unavailable","customers.query");
        var store = PlayerStore.Instance; var queue = store.storeClientManager.clientStack; var current = Current(store);
        int max = (int?)args["max_customers"] ?? 512;
        if (max < 1 || max > 2000) throw new ArgumentException("max_customers must be 1..2000");
        bool details = (bool?)args["include_details"] ?? false;
        var wanted = reference == null ? null : Resolve(reference,world);
        string id = (string)args["customer_id"], name = (string)args["name_contains"], intent = (string)args["intent"];
        Func<StoreClient,bool> match = c => c != null && (wanted == null || ReferenceEquals(c,wanted)) && (id == null || c.identifier == id)
            && (name == null || (c.displayName ?? "").IndexOf(name,StringComparison.OrdinalIgnoreCase) >= 0)
            && (intent == null || c.clientIntent.ToString().Equals(intent,StringComparison.OrdinalIgnoreCase));
        var entries = new JArray(); int matched = 0, scanned = 0;
        Action<StoreClient,string,int?> add = (c,state,index) => {
            if (!match(c)) return;
            if (matched++ >= offset && entries.Count < limit) entries.Add(Describe(c,world,state,index,details));
        };
        if (scope != "queue" && current != null) add(current,"current",null);
        if (scope != "current") for (; scanned < queue.Count && scanned < max; scanned++) add(queue[scanned],"queued",scanned);
        bool complete = scope == "current" || scanned == queue.Count;
        // A live reference absent from these observations does not prove completion.
        string referenceState = wanted == null ? null : ReferenceEquals(current,wanted) ? "current" :
            queue.Take(scanned).Any(c => ReferenceEquals(c,wanted)) ? "queued" : scope == "all" && complete ? "not_observed" : "unknown";
        return new JObject { ["worldId"] = world,["scope"] = scope,["entries"] = entries,["currentPresent"] = current != null,
            ["queueCount"] = queue.Count,["scannedQueueEntries"] = scanned,["totalMatches"] = matched,["searchComplete"] = complete,
            ["incompleteReasons"] = complete ? new JArray() : new JArray("customer_limit"),
            ["nextOffset"] = offset + entries.Count < matched ? (int?)(offset + entries.Count) : null,
            ["referenceState"] = referenceState,["interactionCompleted"] = null,["pages"] = "live" };
    }
    public JObject Enqueue(string world, JObject args, ResourceScope owner, Func<string> currentWorld)
    {
        if (owner == null) throw new BridgeFailure("session_required","Customer insertion requires an owning test session","customers.preflight");
        string preset = (string)args["preset"];
        if (!Presets.Contains(preset)) throw new BridgeFailure("customer_preset_not_found","Use query_customers scope=presets to discover supported native generators","customers.preflight");
        int tier = (int?)args["tier"] ?? -1;
        if (args["tier"] != null && (preset != "aug_med" || tier < 1 || tier > 4)) throw new ArgumentException("tier is 1..4 and only applies to aug_med");
        var store = PlayerStore.Instance; var manager = store?.storeClientManager; var queue = manager?.clientStack;
        if (queue == null) throw new BridgeFailure("customer_queue_unavailable","Loaded store customer queue is unavailable","customers.preflight");
        if (queue.Count >= 2000) throw new BridgeFailure("customer_queue_full","Queue has reached the bridge insertion limit (2000)","customers.preflight");
        var result = new JObject { ["worldId"] = world,["preset"] = preset,["completed"] = false,["queued"] = false,
            ["tracked"] = false,["queueBefore"] = queue.Count,["effectVerification"] = "queue_membership_only",["interactionCompleted"] = null,
            ["cleanupScope"] = "Remove only this object while still queued and not current; no dialogue/transaction rollback",
            ["sideEffectsNotRestored"] = new JArray("Native random state, cooldown trackers, deferred-customer storage, factory/hooks and later interaction effects") };
        string stage = "customers.create"; StoreClient customer = null;
        try
        {
            customer = preset == "aug_med" ? StoreClientList.CreateAugMed(tier) : StoreClientListAppraisal.CreateAppraisal1();
            if (customer == null) throw new InvalidOperationException("Native generator returned null");
            if (world != currentWorld() || !ReferenceEquals(PlayerStore.Instance,store) || !ReferenceEquals(store.storeClientManager,manager) || !ReferenceEquals(manager.clientStack,queue))
                throw new InvalidOperationException("World or customer queue changed during creation");
            if (ReferenceEquals(Current(store),customer) || queue.Any(c => ReferenceEquals(c,customer)))
                throw new InvalidOperationException("Native generator returned an existing customer; it was not claimed");
            string customerRef = Remember(customer,world); result["customerRef"] = customerRef;
            bool attempted = false;
            owner.Track("customer:" + customerRef,() => {
                if (!attempted) return;
                if (world != currentWorld() || !ReferenceEquals(PlayerStore.Instance,store) || !ReferenceEquals(store.storeClientManager,manager) || !ReferenceEquals(manager.clientStack,queue))
                    throw new CleanupSkippedException("World/customer queue changed; no customer removed");
                if (ReferenceEquals(Current(store),customer)) throw new CleanupSkippedException("Customer is current; interaction is outside queue cleanup scope");
                int index = queue.FindIndex(c => ReferenceEquals(c,customer));
                if (index < 0)
                {
                    if (!(bool)result["queued"]) return;
                    throw new CleanupSkippedException("Customer no longer queued; completion and side effects are unknown");
                }
                queue.RemoveAt(index);
                if (queue.Any(c => ReferenceEquals(c,customer))) throw new InvalidOperationException("Duplicate reference remains; inspect queue before retrying cleanup");
            });
            result["tracked"] = true; stage = "customers.enqueue"; attempted = true;
            try { manager.AddNextClient(customer); }
            finally
            {
                int index = queue.FindIndex(c => ReferenceEquals(c,customer));
                result["queued"] = index >= 0; result["queueIndex"] = index >= 0 ? (int?)index : null; result["queueAfter"] = queue.Count;
            }
            result["completed"] = (bool)result["queued"];
            result["customer"] = Describe(customer,world,(bool)result["queued"] ? "queued" : "not_observed",(int?)result["queueIndex"],true);
            if (!(bool)result["queued"]) result["reason"] = "native_did_not_enqueue";
            return result;
        }
        catch (Exception error) { throw new BridgeFailure("customer_enqueue_failed",error.Message,stage,result,inner:error); }
    }
}
