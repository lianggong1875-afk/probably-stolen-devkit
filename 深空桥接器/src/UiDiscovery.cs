using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ModDevBridge;

// One bounded observation supplies both the compact overview and raw nodes.
// Associations are structural facts, not inferred game actions or visibility.
internal static class UiDiscovery
{
    sealed class Node
    {
        public GameObject Go;
        public Node Parent, Owner;
        public int Depth;
        public string Text, Role, Path;
        public bool PathComplete;
        public Component[] Components;
        public bool IsControl;
        public readonly List<Node> Labels = new List<Node>();
    }
    static bool Contains(string value, string filter) => string.IsNullOrEmpty(filter) || (value ?? "").IndexOf(filter,StringComparison.OrdinalIgnoreCase) >= 0;
    static string Role(Component[] components)
    {
        var selectable = components.OfType<Selectable>().FirstOrDefault();
        if (selectable != null) return selectable.GetType().Name;
        if (components.Any(c => c is IPointerClickHandler)) return "PointerClick";
        if (components.Any(c => c is IDragHandler)) return "Drag";
        if (components.Any(c => c is IPointerDownHandler)) return "PointerDown";
        return null;
    }
    static string Clip(string text, int length) => text != null && text.Length > length ? text.Substring(0,length) : text;
    static JObject Texts(IEnumerable<Node> source, bool scanComplete)
    {
        var all = source.Where(n => !string.IsNullOrWhiteSpace(n.Text)).ToArray();
        return new JObject { ["texts"] = new JArray(all.Take(6).Select(n => Clip(n.Text,160))),
            ["complete"] = scanComplete && all.Length <= 6 && all.All(n => n.Text.Length <= 160) };
    }
    static bool Within(Node node, Node parent)
    {
        for (var n = node; n != null; n = n.Parent) if (n == parent) return true;
        return false;
    }
    static JObject Context(Node node, List<Node> texts, bool scanComplete, UiQueries refs, string world)
    {
        // A nearby container's standalone text often identifies a repeated row.
        // Do not pull labels from unrelated sibling controls into its context.
        var control = node.Owner ?? node;
        if ((control.Role == "PointerClick" || control.Role == "PointerDown") && control.Labels.Count > 0)
            return new JObject { ["texts"] = new JArray(), ["complete"] = scanComplete };
        var parent = control.Parent;
        for (int i = 0; parent != null && i < 3; i++, parent = parent.Parent)
        {
            if (parent.Role == "Canvas" || parent.Role == "Window") break;
            // Do not climb from one repeated branch into the list containing
            // its peers. History captions and sibling rows are separate scopes.
            if (parent.Labels.Count == 0 && parent.Go.transform.childCount > 1 &&
                texts.Any(t => !Within(t,control) && Within(t,parent) && t.Owner != null && !Within(t.Owner,control) && t.Owner != parent)) break;
            // Native row containers and text captions can themselves implement
            // IPointerClickHandler. Their captions remain useful context; only
            // labels owned by a separate Selectable are excluded here.
            var nearby = texts.Where(t => !Within(t,control) && Within(t,parent) &&
                (t.Owner == null || !t.Owner.Components.OfType<Selectable>().Any())).ToArray();
            if (nearby.Length == 0) continue;
            var result = Texts(nearby,scanComplete); result["group"] = parent.Go.name;
            result["rootRef"] = refs.Remember(parent.Go,world); return result;
        }
        return new JObject { ["texts"] = new JArray(), ["complete"] = scanComplete };
    }
    public static JObject Find(UiQueries refs, string world, JObject args)
    {
        string view = (string)args["view"] ?? "overview";
        if (view != "overview" && view != "nodes") throw new ArgumentException("view must be overview or nodes");
        int limit = args["limit"] == null ? 20 : ObservationOutput.Limit(args), offset = ObservationOutput.Offset(args);
        int maxNodes = (int?)args["max_nodes"] ?? 2000, maxDepth = (int?)args["max_depth"] ?? 16;
        if (maxNodes < 1 || maxNodes > 10000 || maxDepth < 0 || maxDepth > 64) throw new ArgumentException("max_nodes must be 1..10000, max_depth 0..64");
        string scope = (string)args["scope"] ?? "canvases", rootRef = (string)args["root_ref"];
        if (scope != "canvases" && scope != "windows") throw new ArgumentException("scope must be canvases or windows");
        if (rootRef != null && args["scope"] != null) throw new ArgumentException("Use either root_ref or scope");
        bool inactive = (bool?)args["include_inactive"] ?? false, details = (bool?)args["include_details"] ?? false;
        bool registryAvailable = WindowsHandler.current != null && WindowsHandler.current.visibleWindows != null;
        var windows = registryAvailable ? WindowsHandler.current.visibleWindows.Where(w => w != null && w.handler != null).Select(w => w.handler.gameObject).Distinct().ToArray() : Array.Empty<GameObject>();
        GameObject[] roots;
        if (rootRef != null) { roots = new[] { refs.Resolve(rootRef,world) }; scope = "reference"; }
        else if (scope == "windows") roots = windows;
        else roots = UnityEngine.Object.FindObjectsOfType<Canvas>(true).Where(c => c != null && c.isRootCanvas && c.gameObject.scene.IsValid() && c.gameObject.scene.isLoaded).Select(c => c.gameObject).ToArray();
        var pending = new Queue<Node>(); var visited = new HashSet<int>(); var reasons = new HashSet<string>(); var errors = new JArray();
        var nodes = new List<Node>(); int scanned = 0, excluded = 0;
        if (scope == "windows" && !registryAvailable) reasons.Add("window_registry_unavailable");
        Action<GameObject,Node,int> add = (go,parent,depth) => {
            if (go == null || visited.Contains(go.GetInstanceID())) return;
            if (visited.Count >= maxNodes) { reasons.Add("node_limit"); return; }
            visited.Add(go.GetInstanceID()); pending.Enqueue(new Node { Go = go,Parent = parent,Depth = depth });
        };
        foreach (var root in roots.OrderBy(r => r.GetInstanceID())) add(root,null,0);
        while (pending.Count > 0)
        {
            var n = pending.Dequeue(); if (n.Go == null) { reasons.Add("object_disappeared"); continue; }
            scanned++;
            try
            {
                if (!inactive && !n.Go.activeInHierarchy) { excluded++; continue; }
                n.Components = n.Go.GetComponents<Component>().Where(c => c != null).ToArray();
                n.Role = Role(n.Components); n.IsControl = n.Role != null;
                if (windows.Contains(n.Go)) n.Role = "Window";
                else if (n.Role == null) n.Role = n.Components.OfType<Canvas>().Any(c => c.isRootCanvas) ? "Canvas" : "Node";
                if (details || view == "nodes" || args["path_contains"] != null)
                { n.Path = UiQueries.Path(n.Go.transform,out n.PathComplete); if (!n.PathComplete) reasons.Add("path_limit"); }
                n.Text = UiQueries.TextValue(n.Go);
                // Disabled text components are not advertised as active labels.
                if (!inactive && !(n.Components.OfType<Graphic>().Any(g => g.isActiveAndEnabled) || n.IsControl)) n.Text = null;
                // Drag-only containers (e.g. movable windows) must not absorb
                // every standalone caption in their content as a button label.
                n.Owner = n.Role == "Window" || n.Role == "Canvas" ? null : n.Components.OfType<Selectable>().Any() || n.Components.Any(c => c is IPointerClickHandler) ? n : n.Parent?.Owner;
                if (!string.IsNullOrWhiteSpace(n.Text) && n.Owner != null) n.Owner.Labels.Add(n);
                nodes.Add(n);
                if (n.Depth >= maxDepth) { if (n.Go.transform.childCount > 0) reasons.Add("depth_limit"); continue; }
                for (int i = 0; i < n.Go.transform.childCount; i++)
                {
                    if (visited.Count >= maxNodes) { reasons.Add("node_limit"); break; }
                    add(n.Go.transform.GetChild(i).gameObject,n,n.Depth+1);
                }
            }
            catch (Exception error) { reasons.Add("node_read_failed"); if (errors.Count < 8) errors.Add(BridgeFailure.Describe(error,"ui.read")); }
        }
        var texts = nodes.Where(n => !string.IsNullOrWhiteSpace(n.Text)).ToList();
        bool targeted = args["name_contains"] != null || args["component"] != null || args["path_contains"] != null || rootRef != null;
        Func<Node,bool> unlabeledHandler = n => (n.Role == "PointerClick" || n.Role == "PointerDown") && n.Labels.Count == 0;
        int excludedUnlabeled = view == "overview" && !targeted ? nodes.Count(unlabeledHandler) : 0;
        var found = nodes.Where(n => {
            if (view == "overview")
            {
                if (!targeted && unlabeledHandler(n)) return false;
                if (!n.IsControl && !(n.Owner == null && !string.IsNullOrWhiteSpace(n.Text)) && !(targeted && (n.Role == "Canvas" || n.Role == "Window"))) return false;
            }
            if (!Contains(n.Go.name,(string)args["name_contains"])) return false;
            string component = (string)args["component"];
            if (!string.IsNullOrEmpty(component) && !n.Components.Any(c => c.GetType().Name == component || c.GetType().FullName == component)) return false;
            if (args["path_contains"] != null && !Contains(n.Path,(string)args["path_contains"])) return false;
            return Contains(n.Text,(string)args["text_contains"]) || n.IsControl && n.Labels.Any(t => Contains(t.Text,(string)args["text_contains"]));
        }).ToList();
        var result = ObservationOutput.Page(found.Select(n => (JToken)new JValue(n.Go.GetInstanceID())),offset,limit);
        bool complete = reasons.Count == 0;
        result["entries"] = new JArray(found.Skip(offset).Take(limit).Select(n => {
            var labelNodes = n.IsControl ? n.Labels : new List<Node> { n };
            var label = Texts(labelNodes,complete);
            string role = n.Role == "Node" && !string.IsNullOrWhiteSpace(n.Text) ? "Text" : n.Role;
            var row = new JObject { ["uiRef"] = refs.Remember(n.Go,world), ["name"] = n.Go.name, ["role"] = role,
                ["text"] = string.Join(" | ",label["texts"].Values<string>()), ["textComplete"] = label["complete"],
                ["activeSelf"] = n.Go.activeSelf,["activeInHierarchy"] = n.Go.activeInHierarchy,
                ["targetRef"] = n.IsControl ? refs.Remember(n.Go,world) : n.Owner == null ? null : refs.Remember(n.Owner.Go,world), ["context"] = Context(n,texts,complete,refs,world) };
            // Raw nodes can be drag destinations even without a click/drag
            // handler of their own (IDropHandler-only panels, empty slots).
            var target = n.IsControl ? n : n.Owner ?? (view == "nodes" ? n : null);
            row["observation"] = target == null ? null : UiObservation.Read(target.Go,refs,world);
            if (n.IsControl || n.Owner != null) row["targetRef"] = row["observation"]?["actionRef"]?.DeepClone();
            var selectable = n.Components.OfType<Selectable>().FirstOrDefault();
            if (selectable != null) row["controlState"] = new JObject { ["enabled"] = selectable.enabled,["interactable"] = selectable.IsInteractable() };
            Node group = n.Parent; while (group != null && group.Role != "Window" && group.Role != "Canvas") group = group.Parent;
            if (group != null) row["group"] = group.Go.name;
            if (details || view == "nodes") { row["path"] = n.Path; row["pathComplete"] = n.PathComplete;
                row["instanceId"] = n.Go.GetInstanceID(); row["componentTypes"] = new JArray(n.Components.Select(c => c.GetType().FullName)); }
            return row;
        }));
        result["searchComplete"] = complete; result["complete"] = (bool)result["complete"] && complete; result["truncated"] = (bool)result["truncated"] || !complete;
        result["worldId"] = world; result["scope"] = scope; result["view"] = view;
        result["sourceAvailable"] = scope != "windows" || registryAvailable;
        result["rootCount"] = roots.Length; result["visitedNodes"] = scanned; result["excludedInactiveBranches"] = excluded;
        result["excludedUnlabeledHandlers"] = excludedUnlabeled;
        result["groups"] = ObservationOutput.Page(nodes.Where(n => n.Role == "Canvas" || n.Role == "Window").Select(n => (JToken)new JObject {
            ["uiRef"] = refs.Remember(n.Go,world),["name"] = n.Go.name,["role"] = n.Role,
            ["observedControls"] = nodes.Count(c => c.IsControl && Within(c,n)),
            ["observedTextNodes"] = texts.Count(t => Within(t,n)),["countsComplete"] = complete }),0,32);
        result["includeInactive"] = inactive; result["incompleteReasons"] = new JArray(reasons.OrderBy(x => x)); result["errors"] = errors;
        result["screen"] = new JObject { ["width"] = Screen.width,["height"] = Screen.height,["origin"] = "bottom-left" };
        result["interpretation"] = "Active hierarchy/current text, not verified visibility/clickability. targetRef embeds its observation guard: pass unchanged as target_ref; observation.actionRef also addresses raw drag destinations via to_ref. No extra token argument needed. Bare uiRef is object identity, not an action precondition. Incomplete observations have no actionRef. Guards cover 256 nodes/depth 12/8192 text characters, not business identity or geometry. Context has source rootRef and stops before peer branches. groups lists Canvas/windows (up to 32); use root_ref to narrow. view=nodes includes raw nodes. Text/context bounded to 6 strings of 160 characters. Re-query after UI changes; paths are not visual order or business identity.";
        result["referenceCapacity"] = 512; result["referenceMaxLifetimeSeconds"] = 300;
        return result;
    }
}
