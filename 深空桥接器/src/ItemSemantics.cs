using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace ModDevBridge;

internal static class ItemSemantics
{
    // Some native helpers call GetTagReadonly, which inserts a missing tag.
    // Invoke those helpers only when the tag already exists, otherwise report unknown.
    public static JObject Read(GameItem item, bool location = false)
    {
        var errors = new JObject(); var result = new JObject();
        Action<string,Func<JToken>> read = (name,get) => {
            try { result[name] = get(); }
            catch (Exception error) { result[name] = null; errors[name] = BridgeFailure.Describe(error,"items.semantics."+name); }
        };
        read("stolen",() => new JValue(StolenHelper.IsStolenItem(item)));
        read("contraband",() => new JValue(ContrabandHelper.IsContraband(item)));
        read("contrabandLevel",() => {
            if (result["contraband"]?.Type != JTokenType.Boolean) throw new InvalidOperationException("Contraband predicate unavailable");
            if (!(bool)result["contraband"]) return new JValue(0);
            if (item.modifiedState?.dict == null || !item.modifiedState.dict.TryGetValue("CONTRABAND_LEVEL",out var tag) || tag == null)
                throw new InvalidOperationException("CONTRABAND_LEVEL is absent; native getter would create it");
            return new JValue(ContrabandHelper.GetContrabandLevelInInt(item));
        });
        read("stolenHeat",() => {
            if (result["stolen"]?.Type != JTokenType.Boolean || !(bool)result["stolen"]) return JValue.CreateNull();
            if (item.modifiedState?.dict == null || !item.modifiedState.dict.TryGetValue("STOLEN_VALUE_INT",out var tag) || tag == null)
                throw new InvalidOperationException("STOLEN_VALUE_INT is absent; native getter would create it");
            return new JValue(StolenHelper.GetFoundChance(item));
        });
        read("concealedFromInspection",() => {
            if (result["stolen"]?.Type != JTokenType.Boolean || result["contraband"]?.Type != JTokenType.Boolean)
                throw new InvalidOperationException("Required native predicates unavailable");
            if ((bool)result["stolen"] || (bool)result["contraband"])
                if (item.modifiedState?.dict == null || !item.modifiedState.dict.TryGetValue("CONTRABAND_LEVEL",out var tag) || tag == null)
                    throw new InvalidOperationException("CONTRABAND_LEVEL is absent; native concealment getter would create it");
            return new JValue(ContrabandHelper.IsConcealedFromInspection(item));
        });
        var containers = new JArray();
        read("inSmugglerBay",() => {
            var active = new HashSet<GraphNodeStorage>(); var done = new HashSet<GraphNodeStorage>(); int visited = 0;
            Action<GraphNodeStorage,int> visit = null;
            visit = (node,depth) => {
                if (node == null || done.Contains(node)) return;
                if (depth > 32 || ++visited > 256) throw new InvalidOperationException("Parent graph exceeds safe native lookup budget");
                if (!active.Add(node)) throw new InvalidOperationException("Parent graph cycle; native recursive lookup was not invoked");
                if (location) containers.Add(new JObject { ["id"] = node is GameItem parent ? parent.identifier : node is GameInventory inv ? inv.identifier : null,
                    ["type"] = node.GetType().Name,["distance"] = depth });
                if (node.parents != null) foreach (var parent in node.parents) visit(parent,depth+1);
                active.Remove(node); done.Add(node);
            };
            visit(item.parentInventory,0);
            return new JValue(item.IsRootedInSmugglerBay());
        });
        if (location) { result["ancestorContainers"] = containers; result["locationComplete"] = errors["inSmugglerBay"] == null; }
        result["complete"] = errors.Count == 0; result["errors"] = errors;
        result["verification"] = "native_predicates_not_inspection_outcome";
        return result;
    }
}
