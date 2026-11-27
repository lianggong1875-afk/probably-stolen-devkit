using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Newtonsoft.Json.Linq;

namespace ModDevBridge;

// Two narrow queries over the game's inventory graph, not a reflection browser.
internal sealed class ItemQueries
{
    internal static readonly string[] RootNames = { "inv", "back", "back_counter", "front", "doc", "showcase", "trash", "vending_fountain", "vending_machine", "afterhours", "hidden", "trashcan" };
    private sealed class Identity : IEqualityComparer<GraphNodeStorage>
    {
        public bool Equals(GraphNodeStorage a, GraphNodeStorage b) => ReferenceEquals(a, b);
        public int GetHashCode(GraphNodeStorage a) => RuntimeHelpers.GetHashCode(a);
    }
    private sealed class Reference { public string Id, World; public WeakReference Item; public DateTime Issued; }
    private sealed class Node { public GraphNodeStorage Value; public string Root; public int Depth; }
    private sealed class Found { public GameItem Item; public string Root, DisplayName; public int Depth; public JObject Semantics; }
    readonly List<Reference> references = new List<Reference>();
    readonly Func<DateTime> clock;
    static readonly FieldInfo selectedItem = typeof(ItemSelectHandler).GetField("currentItem", BindingFlags.NonPublic | BindingFlags.Instance);
    static readonly Dictionary<string, PropertyInfo> callbacks = typeof(GameItemFunc).GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(p => typeof(Delegate).IsAssignableFrom(p.PropertyType) && p.GetIndexParameters().Length == 0).ToDictionary(p => p.Name);
    public ItemQueries(Func<DateTime> clock = null) { this.clock = clock ?? (() => DateTime.UtcNow); }
    static bool Destroyed(GameItem item) => item is GameItemElement element && element.IsDestroyed();
    static KeyValuePair<string, GraphNodeStorage>[] Roots()
    {
        var e = EmporiumEntry.Instance;
        GraphNodeStorage[] roots = { e.invElement, e.backInvinvElement, e.backInvinvElementCounter, e.frontInvinvElement, e.docInvElement,
            e.showcaseElement, e.trashInvElement, e.vendingFountainElement, e.vendingMachineElement, e.afterhourWindow, e.hiddenElement, e.trashcanInvElement };
        return RootNames.Zip(roots, (name, node) => new KeyValuePair<string, GraphNodeStorage>(name, node)).ToArray();
    }
    internal string Remember(GameItem item, string world)
    {
        references.RemoveAll(r => r.World != world || clock() - r.Issued > TimeSpan.FromMinutes(5) || !r.Item.IsAlive);
        var old = references.FirstOrDefault(r => ReferenceEquals(r.Item.Target, item));
        if (old != null) return old.Id;
        var next = new Reference { Id = Guid.NewGuid().ToString("N"), World = world, Item = new WeakReference(item), Issued = clock() };
        references.Add(next); while (references.Count > 512) references.RemoveAt(0); return next.Id;
    }
    internal GameItem Resolve(string world, string id, bool allowDestroyed = false)
    {
        var reference = references.FirstOrDefault(r => r.Id == id);
        if (reference == null || clock() - reference.Issued > TimeSpan.FromMinutes(5)) throw new BridgeFailure("item_reference_unavailable", "Item reference expired, was evicted or belongs to another bridge process; query items again", "items.lookup");
        if (reference.World != world) throw new BridgeFailure("item_world_mismatch", "Item reference belongs to a previous world; query again", "items.lookup");
        var item = reference.Item.Target as GameItem;
        if (item == null) throw new BridgeFailure("item_reference_unavailable", "Item object is no longer available; query again", "items.lookup");
        if (!allowDestroyed && Destroyed(item)) throw new BridgeFailure("item_destroyed", "Referenced native item was destroyed; query again for any replacement", "items.lookup");
        return item;
    }
    public JObject Find(string world, JObject args)
    {
        int limit = ObservationOutput.Limit(args), offset = ObservationOutput.Offset(args);
        int maxNodes = (int?)args["max_nodes"] ?? 2000, maxDepth = (int?)args["max_depth"] ?? 16;
        if (maxNodes < 1 || maxNodes > 10000 || maxDepth < 0 || maxDepth > 64) throw new ArgumentException("max_nodes must be 1..10000, max_depth 0..64");
        string scope = (string)args["scope"] ?? "store", rootFilter = (string)args["root"], id = (string)args["item_id"], name = (string)args["name_contains"];
        if (!new[] { "store", "inspected", "selected" }.Contains(scope)) throw new ArgumentException("scope must be store, inspected or selected");
        if (rootFilter != null && (scope != "store" || !RootNames.Contains(rootFilter))) throw new ArgumentException("root requires store scope and a known root alias");
        bool recursive = scope == "store" && ((bool?)args["recursive"] ?? true);
        bool? owned = (bool?)args["owned"];
        bool? stolen = (bool?)args["stolen"], contraband = (bool?)args["contraband"], smuggler = (bool?)args["in_smuggler_bay"], concealed = (bool?)args["concealed"];
        int? minLevel = (int?)args["contraband_level_min"];
        if (minLevel.HasValue && (minLevel < 0 || minLevel > 4)) throw new ArgumentException("contraband_level_min must be 0..4");
        bool semantics = (bool?)args["include_semantics"] == true || stolen.HasValue || contraband.HasValue || smuggler.HasValue || concealed.HasValue || minLevel.HasValue;
        var roots = scope == "store" ? Roots().Where(p => rootFilter == null || p.Key == rootFilter).ToArray() : Array.Empty<KeyValuePair<string, GraphNodeStorage>>();
        var pending = new Queue<Node>(); var scheduled = new HashSet<GraphNodeStorage>(new Identity());
        var reasons = new HashSet<string>(); var errors = new JArray(); var found = new List<Found>(); int visited = 0, destroyed = 0;
        bool sourceAvailable = scope == "store" || (scope == "inspected" ? InspectionUIManager.Instance != null : ItemSelectHandler.current != null);
        if (!sourceAvailable) reasons.Add("current_ui_unavailable");
        Action<GraphNodeStorage, string, int> enqueue = (value, root, depth) => {
            if (value == null || scheduled.Contains(value)) return;
            if (scheduled.Count >= maxNodes) { reasons.Add("node_limit"); return; }
            scheduled.Add(value); pending.Enqueue(new Node { Value = value, Root = root, Depth = depth });
        };
        if (scope == "store") foreach (var pair in roots) enqueue(pair.Value, pair.Key, 0);
        else if (scope == "inspected") enqueue(InspectionUIManager.Instance?.currentInspectingItem, scope, 0);
        else
        {
            if (selectedItem == null) throw new BridgeFailure("selection_api_unavailable", "Known native selected-item field is unavailable in this game build", "items.selection");
            enqueue(ItemSelectHandler.current == null ? null : selectedItem.GetValue(ItemSelectHandler.current) as GameItem, scope, 0);
        }
        while (pending.Count > 0)
        {
            var node = pending.Dequeue(); visited++;
            try
            {
                if (node.Value is GameItem item)
                {
                    if (Destroyed(item)) { destroyed++; continue; }
                    if ((id == null || item.identifier == id) && (!owned.HasValue || GeneralHelper.IsItemOwned(item) == owned.Value))
                    {
                        string display = item.GetDisplayName(true);
                        if (string.IsNullOrEmpty(name) || (display ?? "").IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            JObject state = semantics ? ItemSemantics.Read(item) : null;
                            bool unknown = false, rejected = false;
                            Func<string,bool?,bool> matches = (field,want) => {
                                if (!want.HasValue) return true;
                                if (state[field]?.Type != JTokenType.Boolean) { unknown = true; return false; }
                                bool equal = (bool)state[field] == want.Value;
                                if (!equal) rejected = true;
                                return equal;
                            };
                            bool keep = matches("stolen",stolen) & matches("contraband",contraband) & matches("inSmugglerBay",smuggler) & matches("concealedFromInspection",concealed);
                            if (minLevel.HasValue)
                            {
                                if (state["contrabandLevel"]?.Type != JTokenType.Integer) { unknown = true; keep = false; }
                                else { bool enough = (int)state["contrabandLevel"] >= minLevel.Value; keep &= enough; if (!enough) rejected = true; }
                            }
                            if (unknown && !rejected) reasons.Add("semantic_filter_unknown");
                            if (keep) found.Add(new Found { Item = item, Root = node.Root, Depth = node.Depth, DisplayName = display, Semantics = state });
                        }
                    }
                    // Shallow search traverses root windows/inventories but stops
                    // at items, so container contents are explicitly excluded.
                    if (!recursive) continue;
                }
                if (scope != "store") continue;
                var children = node.Value.children;
                if (children == null) continue;
                if (node.Depth >= maxDepth)
                {
                    if (children.Any(c => c != null && !scheduled.Contains(c))) reasons.Add("depth_limit");
                    continue;
                }
                foreach (var child in children)
                {
                    if (child != null && !scheduled.Contains(child) && scheduled.Count >= maxNodes) { reasons.Add("node_limit"); break; }
                    enqueue(child, node.Root, node.Depth + 1);
                }
            }
            catch (Exception error)
            {
                reasons.Add("node_read_failed");
                if (errors.Count < 8) errors.Add(new JObject { ["root"] = node.Root, ["depth"] = node.Depth, ["error"] = BridgeFailure.Describe(error, "items.read") });
            }
        }
        var rows = found.Skip(offset).Take(limit).Select(f => {
            var row = new JObject {
            ["itemRef"] = Remember(f.Item, world), ["itemId"] = f.Item.identifier, ["nativeUniqueId"] = f.Item.uniqueId,
            ["displayName"] = f.DisplayName, ["unitCount"] = f.Item.unitCount,
            ["owned"] = GeneralHelper.IsItemOwned(f.Item), ["root"] = f.Root, ["depth"] = f.Depth,
            ["parentInventory"] = f.Item.parentInventory?.identifier };
            if (semantics) row["semantics"] = f.Semantics;
            return (JToken)row;
        }).ToArray();
        var page = ObservationOutput.Page(rows, 0, limit);
        page["offset"] = offset; page["total"] = found.Count; page["omitted"] = found.Count - rows.Length;
        page["nextOffset"] = (long)offset + rows.Length < found.Count ? new JValue(offset + rows.Length) : JValue.CreateNull();
        bool searchComplete = reasons.Count == 0;
        page["complete"] = searchComplete && rows.Length == found.Count; page["truncated"] = !searchComplete || rows.Length < found.Count;
        page["worldId"] = world; page["scope"] = scope; page["recursive"] = recursive;
        page["sourceAvailable"] = sourceAvailable;
        page["instanceCount"] = found.Count; page["unitCount"] = found.Sum(f => (long)f.Item.unitCount);
        page["countsExactWithinScope"] = searchComplete; page["searchComplete"] = searchComplete;
        page["roots"] = new JArray(roots.Select(p => new JObject { ["name"] = p.Key, ["available"] = p.Value != null }));
        page["visitedNodes"] = visited; page["destroyedSkipped"] = destroyed; page["incompleteReasons"] = new JArray(reasons.OrderBy(x => x)); page["errors"] = errors;
        page["referenceLifetimeSeconds"] = 300; page["referenceCapacity"] = 512;
        page["semanticsIncluded"] = semantics;
        page["coverage"] = "Only the named live storage roots or current UI item; empty does not mean absent from the whole game. Pages observe live state, not an immutable snapshot.";
        return page;
    }
    public JObject Details(string world, JObject args)
    {
        int limit = ObservationOutput.Limit(args), offset = ObservationOutput.Offset(args);
        string id = (string)args["item_ref"];
        var item = Resolve(world,id);
        var requestedCallbacks = (args["callbacks"] as JArray)?.Values<string>().Distinct().ToArray() ?? Array.Empty<string>();
        if (requestedCallbacks.Length > 12 || requestedCallbacks.Any(c => c == null || !callbacks.ContainsKey(c))) throw new ArgumentException("callbacks must name up to 12 delegate properties on GameItemFunc");
        var result = new JObject {
            ["itemRef"] = id, ["worldId"] = world, ["itemId"] = item.identifier, ["nativeUniqueId"] = item.uniqueId,
            ["displayName"] = item.GetDisplayName(true), ["type"] = item.GetType().FullName, ["assembly"] = item.GetType().Assembly.GetName().Name,
            ["unitCount"] = item.unitCount, ["owned"] = GeneralHelper.IsItemOwned(item), ["parentInventory"] = item.parentInventory?.identifier,
            ["destructionCheck"] = item is GameItemElement ? "native_element_alive" : "unsupported_item_type",
            ["itemTypes"] = item.itemTypes == null ? new JArray() : new JArray(item.itemTypes),
            ["tagDetailsIncluded"] = (bool?)args["include_tags"] == true || args["tags"] != null,
            ["callbackDetailsIncluded"] = requestedCallbacks.Length > 0 };
        if ((bool?)args["include_semantics"] == true || (bool?)args["include_location"] == true)
            result["semantics"] = ItemSemantics.Read(item,(bool?)args["include_location"] == true);
        if ((bool?)args["include_text"] == true)
            result["text"] = new JObject { ["shortDescription"] = item.shortDescription,["flavorText"] = item.flavorText,["source"] = "current_item_fields" };
        if ((bool)result["tagDetailsIncluded"])
        {
            string[] names = (args["tags"] as JArray)?.Values<string>().ToArray();
            var tags = item.modifiedState?.dict;
            // Read the dictionary directly: GetTag/GetTagReadonly may create a missing tag.
            var values = tags == null ? Array.Empty<JToken>() : tags.Where(p => p.Value != null && (names == null ? p.Value.valueEnabled : names.Contains(p.Key)))
                .OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => (JToken)new JObject { ["key"] = p.Key, ["enabled"] = p.Value.valueEnabled,
                    ["int"] = p.Value.valueInt, ["long"] = p.Value.valueLong, ["float"] = p.Value.valueFloat, ["double"] = p.Value.valueDouble,
                    ["bool"] = p.Value.valueBool, ["string"] = p.Value.valueString }).ToArray();
            result["tags"] = ObservationOutput.Page(values, offset, limit);
            result["missingTags"] = names == null ? new JArray() : new JArray(names.Where(n => tags == null || !tags.ContainsKey(n)));
            result["tagSource"] = "modifiedState";
        }
        if (requestedCallbacks.Length > 0)
        {
            result["callbacks"] = new JArray(requestedCallbacks.Select(c => {
                var delegates = (callbacks[c].GetValue(item, null) as Delegate)?.GetInvocationList() ?? Array.Empty<Delegate>();
                var page = ObservationOutput.Page(delegates.Select(d => (JToken)new JObject { ["method"] = d.Method.ToString(), ["type"] = d.Method.DeclaringType?.FullName, ["assembly"] = d.Method.DeclaringType?.Assembly.GetName().Name }), offset, limit);
                page["name"] = c; return page;
            }));
        }
        return result;
    }
}
