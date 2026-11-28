using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ModDevBridge;

// A small, typed MCP surface over the existing operation queue. No game API is
// called from the HTTP thread. Building projects remains the coding agent's job.
internal sealed class McpTools
{
    private sealed class Tool
    {
        public string Name, Command, Description;
        public JObject Properties;
        public string[] Required;
        public bool ReadOnly;
        public JObject Definition() => new JObject {
            ["name"] = Name, ["description"] = Description,
            ["inputSchema"] = new JObject { ["type"] = "object", ["properties"] = Properties, ["required"] = new JArray(Required), ["additionalProperties"] = false },
            ["annotations"] = new JObject { ["readOnlyHint"] = ReadOnly }
        };
    }
    private readonly OperationQueue queue;
    private readonly Func<bool> stopping;
    private readonly Dictionary<string, Tool> tools = new Dictionary<string, Tool>();
    private static JObject Schema(string type) => new JObject { ["type"] = type };
    private static JObject Number(int min, int max) => new JObject { ["type"] = "integer", ["minimum"] = min, ["maximum"] = max };
    private static JObject Enum(params string[] values) => new JObject { ["type"] = "string", ["enum"] = new JArray(values) };
    private static JObject PageArgs(JObject fields = null)
    {
        fields = fields ?? new JObject(); fields["offset"] = Number(0, int.MaxValue); fields["limit"] = Number(1, 200); return fields;
    }
    private static JObject Fields() => new JObject { ["type"] = "array", ["maxItems"] = 12,
        ["items"] = Enum("slot", "day", "cash", "inventory", "contacts", "mods", "modData", "store_state","professions","perks","finance","inspection") };
    public McpTools(OperationQueue queue, Func<bool> stopping)
    {
        this.queue = queue; this.stopping = stopping;
#if BRIDGE_FULL
        Add("reload_mod", "reload_mod", "Switch an enabled MOD to an agent-compiled, uniquely named DLL staged in moduleDirectory. Requires session/world and matching entry class plus public void HotReloadPrepare/HotReloadActivate on both versions. Hooks declare support, not verified safety. Returns stages, actual loader references and patch-owner observations; gameplay effect remains not_checked. Compensation is limited. Session close retains replacement; assemblies remain until restart. MOD callbacks/resources/assets are not automatically migrated.", false,
            new JObject { ["mod_id"] = Schema("string"), ["filename"] = Schema("string"), ["session_id"] = Schema("string"), ["world_id"] = Schema("string") }, "mod_id", "filename", "session_id", "world_id");
#endif
        Add("get_development_environment", "development_environment", "Discover actual game Managed path, Bridge SDK path, owned module staging directory and minimal C# interfaces. Available in menus. Compile in your own directory; no game-side compiler or SDK extraction.", true);
        Add("query_probes", "query_probes", "Discover curated reusable C# sources by keyword/purpose. Default short summaries; exact name plus include_details reveals side effects, cleanup and historical validation. include_source requires an exact name and returns source embedded in the DLL, without extracting files. Start runs on load. Long source is bounded; read retained original via get_operation result_path=$.entries[0].source with offset/limit.", true,
            PageArgs(new JObject { ["name"] = Schema("string"), ["contains"] = Schema("string"), ["purpose"] = Enum("self_test","example","hot_reload"),
                ["include_details"] = Schema("boolean"), ["include_source"] = Schema("boolean") }));
        Add("get_game_status", "status", "Read actual readiness, save slot, world and session. No gameplay state is changed.", true);
        Add("get_inspection", "inspection_state", "Read native inspection target reference and panel active flags. Active is not visibility or correct rendering. Does not invoke item/UI callbacks.",true);
        Add("operate_inspection", "inspection_operate", "Open an item_ref with native OnInspectItem, or close only that current target. Session/world required. replace=true explicitly switches another open item. Open resources close only their own still-current target; previous UI and native initialization side effects are not restored. Read completed and actual after state, not only invocation success.",false,
            new JObject { ["world_id"] = Schema("string"),["session_id"] = Schema("string"),["item_ref"] = Schema("string"),["action"] = Enum("open","close"),["replace"] = Schema("boolean") },"world_id","session_id","item_ref","action");
        Add("query_localization", "query_localization", "Read native Unity StringDatabase values, raw entries and resolved locale for up to 50 keys in one table. Typical tables: UI, Item, Dialogue, ClientName, MainMenu, NewGame, Mechanic, PerkTable. Optional locale reads another installed language without changing selected locale; arguments apply to every key. Missing/fallback is based on native table resolution, not text-language guesses. Native formatting errors retain raw values and per-key diagnostics. Item-cached descriptions use get_item_details include_text.",true,
            new JObject { ["table"] = Schema("string"),["keys"] = new JObject { ["type"] = "array",["maxItems"] = 50,["items"] = Schema("string") },
                ["locale"] = Schema("string"),["arguments"] = new JObject { ["type"] = "array",["maxItems"] = 16,["items"] = new JObject { ["type"] = new JArray("string","integer","number","boolean") } } },"table","keys");
        Add("query_customers", "query_customers", "Read current customer and/or native queue, or scope=presets to discover supported generators (also in menus). Optional customer_ref follows the same object from queue to counter. not_observed is not completed. Default 20 results; bounded live scan, check searchComplete/nextOffset. Refs: 512, 5 minutes, world-bound. Does not invoke dialogue or business-completion checks.", true,
            PageArgs(new JObject { ["world_id"] = Schema("string"),["scope"] = Enum("all","current","queue","presets"),
                ["customer_ref"] = Schema("string"),["customer_id"] = Schema("string"),["name_contains"] = Schema("string"),["intent"] = Schema("string"),
                ["include_details"] = Schema("boolean"),["max_customers"] = Number(1,2000) }));
        Add("enqueue_customer", "enqueue_customer", "Create one supported native customer at the queue front; does not force arrival or dismiss the current customer. Discover presets with query_customers scope=presets. Requires session/world. Read queued/completed separately from invocation success; cleanup removes only this object still queued, skips current/absent/old-world objects. Native RNG/cooldown trackers/hooks and interaction effects are not restored.", false,
            new JObject { ["world_id"] = Schema("string"),["session_id"] = Schema("string"),["preset"] = Enum("appraisal_hand","aug_med"),["tier"] = Number(1,4) },"world_id","session_id","preset");
        Add("operate_ui", "ui_pointer", "Click/drag using Unity left-pointer events. Pass fresh query_ui targetRef unchanged as target_ref; its content guard is embedded. For to_ref use destination observation.actionRef (view=nodes/details supports drop-only nodes). Bare uiRef is for reads, not actions. Changed content rejects or releases without click/drop. Geometry may move. Coordinate-only input is unguarded. Re-raycasts and world checks apply. Always releases. Dispatch is not effect verification.", false,
            new JObject { ["world_id"] = Schema("string"),["action"] = Enum("click","drag"),
                ["x"] = new JObject { ["type"] = "number",["minimum"] = 0 },["y"] = new JObject { ["type"] = "number",["minimum"] = 0 },
                ["to_x"] = new JObject { ["type"] = "number",["minimum"] = 0 },["to_y"] = new JObject { ["type"] = "number",["minimum"] = 0 },
                ["frames"] = Number(1,120),["target_ref"] = Schema("string"),["to_ref"] = Schema("string") },"world_id","action");
        Add("query_ui", "query_ui", "Start here to discover current UI, including menus; world_id is optional and returned. Default overview lists controls with descendant labels, standalone text and nearby context for repeated labels. groups separately lists Canvas/windows with uiRefs to narrow root_ref. Unlabeled custom click/down handlers are suppressed unless narrowed by name/component/path/root_ref; see excludedUnlabeledHandlers. text_contains searches control labels too. targetRef embeds its observation guard; pass it unchanged to operate_ui; context.rootRef names its source container. Text nodes link targetRef to their observed click/Selectable owner. view=nodes exposes all raw nodes; include_details adds paths/components. Active does not prove visible/clickable. Bounded scan/live pages; inspect incompleteReasons and nextOffset.", true,
            PageArgs(new JObject { ["world_id"] = Schema("string"), ["scope"] = Enum("canvases","windows"), ["root_ref"] = Schema("string"),
                ["name_contains"] = Schema("string"), ["path_contains"] = Schema("string"), ["text_contains"] = Schema("string"), ["component"] = Schema("string"),
                ["view"] = Enum("overview","nodes"), ["include_details"] = Schema("boolean"),
                ["include_inactive"] = Schema("boolean"), ["max_nodes"] = Number(1,10000), ["max_depth"] = Number(0,64) }));
        Add("get_ui_details", "ui_details", "Read native UI state: active flags, component enabled, interactability, text/input/toggle/slider, Rect, Canvas order and ancestor groups. Screen bounds do not prove visibility or clickability. No callbacks invoked.", true,
            new JObject { ["world_id"] = Schema("string"), ["ui_ref"] = Schema("string") }, "world_id", "ui_ref");
        Add("raycast_ui", "ui_raycast", "Read Unity EventSystem candidate order at a primary-display pixel point (origin bottom-left), optionally compare with target_ref. No pointer events or clicks dispatched; hit order is not successful interaction verification.", true,
            PageArgs(new JObject { ["world_id"] = Schema("string"), ["x"] = new JObject { ["type"] = "number", ["minimum"] = 0 },
                ["y"] = new JObject { ["type"] = "number", ["minimum"] = 0 }, ["target_ref"] = Schema("string") }), "world_id", "x", "y");
        Add("inspect_runtime", "inspect", "Inspect native mods, patches, events, config or item factories.", true,
            PageArgs(new JObject { ["kind"] = Enum("mods", "patches", "events", "config", "item"), ["target"] = Schema("string"), ["contains"] = Schema("string"), ["include_details"] = Schema("boolean") }), "kind");
        Add("query_items", "query_items", "Find live instances by ID/name/root/current UI and optional owned/stolen/contraband/level/concealment predicates. Filters include semantic details; unknown predicates exclude uncertain matches and mark searchComplete=false. in_smuggler_bay is the native IsRootedInSmugglerBay predicate, concealed is native IsConcealedFromInspection; neither proves an inspection outcome. Counts distinguish instances/units. Bounded live scan, no creation. Refs: 512, 5 minutes, world-bound.", true,
            PageArgs(new JObject { ["scope"] = Enum("store", "inspected", "selected"),
                ["root"] = Enum("inv", "back", "back_counter", "front", "doc", "showcase", "trash", "vending_fountain", "vending_machine", "afterhours", "hidden", "trashcan"),
                ["item_id"] = Schema("string"), ["name_contains"] = Schema("string"), ["owned"] = Schema("boolean"),
                ["stolen"] = Schema("boolean"),["contraband"] = Schema("boolean"),["contraband_level_min"] = Number(0,4),["in_smuggler_bay"] = Schema("boolean"),["concealed"] = Schema("boolean"),["include_semantics"] = Schema("boolean"),
                ["recursive"] = Schema("boolean"), ["max_nodes"] = Number(1,10000), ["max_depth"] = Number(0,64) }));
        Add("get_item_details", "item_details", "Read a world-bound item_ref without allocating native IDs. Optional include_semantics reads safe native predicates (unknown/errors explicit), include_location adds bounded parent containers, include_text reads current cached description/flavor. Tags and selected callback metadata are opt-in; delegates are not invoked. Expired/destroyed/old-world refs fail.", true,
            PageArgs(new JObject { ["world_id"] = Schema("string"), ["item_ref"] = Schema("string"), ["include_tags"] = Schema("boolean"),
                ["include_semantics"] = Schema("boolean"),["include_location"] = Schema("boolean"),["include_text"] = Schema("boolean"),
                ["tags"] = new JObject { ["type"] = "array", ["maxItems"] = 50, ["items"] = Schema("string") },
                ["callbacks"] = new JObject { ["type"] = "array", ["maxItems"] = 12, ["items"] = Schema("string") } }), "world_id", "item_ref");
        Add("get_game_logs", "logs", "Read filtered logs, default 50 without stacks. Continue with returned cursor and identical filters; bufferOverwritten reports lost records.", true,
            new JObject { ["after"] = new JObject { ["type"] = "integer", ["minimum"] = 0 }, ["limit"] = Number(1,200),
                ["level"] = Enum("Log","Warning","Error","Assert","Exception"), ["contains"] = Schema("string"), ["include_stack"] = Schema("boolean") });
        Add("get_snapshot", "snapshot", "Capture selected state; modData and extended groups are opt-in. Optional fields: store_state, professions (native start type/appraisal introduction), perks, finance, inspection (schedule). Each group reports available/errors. Cache: 8 snapshots/10 minutes. Page immutable data using snapshot_id; inspect collections and omittedFields.", true,
            PageArgs(new JObject { ["fields"] = Fields(), ["snapshot_id"] = Schema("string") }));
        Add("diff_snapshots", "snapshot_diff", "Compare captured fields in the same world. Omit after_id to capture current state. Page changes using the returned beforeId and afterId. Equality is not complete rollback verification.", true,
            PageArgs(new JObject { ["before_id"] = Schema("string"), ["after_id"] = Schema("string"), ["fields"] = Fields() }), "before_id");
        Add("open_test_session", "open_session", "Acquire the single resource-owning session. Default baseline is a summary; select fields and optionally include_snapshot.", false,
            new JObject { ["fields"] = Fields(), ["include_snapshot"] = Schema("boolean") });
        Add("close_test_session", "close_session", "Attempt cleanup; return summary and bounded diff by default. Inspect cleanupStatus, sessionReleased and skipped resources.", false,
            new JObject { ["session_id"] = Schema("string"), ["include_snapshot"] = Schema("boolean") }, "session_id");
#if BRIDGE_FULL
        Add("load_probe_module", "load", "Load a uniquely named compiled probe from the owned module directory returned by get_development_environment. Compile in your own project and copy the DLL there first. Start executes immediately; assemblies remain loaded. No build tooling or repository layout required.", false,
            new JObject { ["session_id"] = Schema("string"), ["world_id"] = Schema("string"), ["filename"] = Schema("string") }, "session_id", "world_id", "filename");
        Add("call_probe", "call", "Execute the active C# probe on the Unity main thread. Pending/unknown means query the original operation ID.", false,
            new JObject { ["session_id"] = Schema("string"), ["world_id"] = Schema("string"), ["command"] = Schema("string") }, "session_id", "world_id", "command");
#endif
        Add("run_test", "run_test", "Run at most 100 explicit steps. Failure/cancellation retains completed steps and actual values.", false,
            new JObject { ["session_id"] = Schema("string"), ["world_id"] = Schema("string"), ["steps"] = new JObject { ["type"] = "array", ["maxItems"] = 100, ["items"] = Schema("object") } }, "session_id", "world_id", "steps");
        Add("list_configs", "list_configs", "List existing native ModConfig IDs.", true);
        Add("get_config", "get_config", "Read a native config without creating missing entries.", true,
            new JObject { ["mod_id"] = Schema("string"), ["include_labels"] = Schema("boolean") }, "mod_id");
        Add("set_config", "set_config", "Apply native config. track defaults true and requires session_id. persist verifies the serialized key by file readback, even if native Save swallows errors. Memory application, persistence and notification are reported separately. Cleanup persists the before-memory value, not original file bytes/existence; later changes can skip cleanup.", false,
            new JObject { ["mod_id"] = Schema("string"), ["key"] = Schema("string"), ["value"] = new JObject { ["type"] = new JArray("string", "integer", "number", "boolean") },
                ["persist"] = Schema("boolean"), ["track"] = Schema("boolean"), ["session_id"] = Schema("string") }, "mod_id", "key", "value");
        Add("seed_scenario", "seed_scenario", "Seed cash (absolute) OR cash_delta (increment), item units or modData. All factory IDs validated before mutation. count means units (max 1000 total), using whole native factory batches. track defaults true; tracked placement refuses merges/splits before placement. Strict is not transactional. Inspect acceptedUnits, observedUnitChange, completed and cleanup scope; factory counters/callbacks are not restored.", false,
            new JObject { ["cash"] = Schema("integer"), ["cash_delta"] = Schema("integer"), ["items"] = new JObject { ["type"] = "array", ["maxItems"] = 100, ["items"] = new JObject {
                ["type"] = "object", ["properties"] = new JObject { ["id"] = Schema("string"), ["count"] = Number(1,999), ["where"] = Enum("auto","inv","back","front","trash","hidden","showcase") }, ["required"] = new JArray("id"), ["additionalProperties"] = false } },
                ["mod_data"] = new JObject { ["type"] = "object", ["additionalProperties"] = new JObject { ["type"] = new JArray("string", "null") } },
                ["track"] = Schema("boolean"), ["strict"] = Schema("boolean"), ["session_id"] = Schema("string") });
        Add("machine_sandbox", "machine_find", "Read every machine the shop physically holds, with progress/target/state and whether it carries a cell. Scans PlayerStore.FindAllItem(false), so machines are found by type/tag rather than by knowing their IDs. Read-only: writes no tag and fires no cycle callback, unlike inspect_runtime which reports registrations.", true,
            new JObject { ["target"] = Schema("string") });
        Add("fast_forward_machines", "machine_fast_forward", "Advance machines without waiting for real nights. mode=one_cycle runs one night's three cycle callbacks (early/main/late); mode=nights repeats that 1..30 times; mode=complete pins the progress tag to the machine's own target then runs one pass so the native completion path produces output. auto_recharge tops each cell first. Reported values are observed progress, not verified production; inspect the output item yourself.", false,
            new JObject { ["target"] = Schema("string"), ["mode"] = Enum("one_cycle", "complete", "nights"), ["nights"] = Number(1, 30), ["auto_recharge"] = Schema("boolean"), ["session_id"] = Schema("string"), ["world_id"] = Schema("string") }, "session_id", "world_id");
        Add("skip_night_machines", "machine_skip_night", "Run the game's own DebugPanel.SkipNightMachine(): early pass, cold-marker reset, main pass, late pass, cold-feature expiry, in the shipped order. Writes no progress tag directly, so it is the safest way to simulate one night for machines. Optional auto_recharge tops each cell first. Reports progress before/after per machine.", false,
            new JObject { ["target"] = Schema("string"), ["auto_recharge"] = Schema("boolean"), ["session_id"] = Schema("string"), ["world_id"] = Schema("string") }, "session_id", "world_id");
        Add("recharge_all_machines", "machine_recharge", "Top every machine's internal cell to its declared max through PowerHelper.SetPowerSourceAt, which also refreshes the native energy sprite so tooltips agree with the written value. Reports energy before/after per battery.", false,
            new JObject { ["target"] = Schema("string"), ["session_id"] = Schema("string"), ["world_id"] = Schema("string") }, "session_id", "world_id");
        Add("debug_actions", "debug_list", "List the game's own debug action table (CustomUIDebugPanel.entries: category, name, invokable). These are the exact calls the shipped debug panel makes, so invoking one is not a hand-rolled effect. Filter by category or name substring. Read-only.", true,
            new JObject { ["category"] = Schema("string"), ["contains"] = Schema("string") });
        Add("invoke_debug_action", "debug_invoke", "Invoke one entry of the game's own debug action table by name, optionally disambiguated by category. The delegate runs on the main thread. Side effects are whatever that native action does — they are not bounded or rolled back by the test session, so check the action's category before calling it on a save you care about.", false,
            new JObject { ["name"] = Schema("string"), ["category"] = Schema("string"), ["session_id"] = Schema("string"), ["world_id"] = Schema("string") }, "session_id", "world_id");
        Add("get_operation", null, "Read an operation, including pending or failed results. Query success does not mean the original operation succeeded.", true,
            PageArgs(new JObject { ["operation_id"] = Schema("string"), ["include_details"] = Schema("boolean"),
                ["result_path"] = new JObject { ["type"] = "string", ["description"] = "Optional single JSONPath relative to the retained original result, e.g. $.text or $.entries[0].stack. With a path, offset/limit page arrays, object properties, or UTF-16 text characters." } }), "operation_id");
        Add("cancel_operation", null, "Request cancellation at a main-thread checkpoint; cancellation does not clean up a test session.", false,
            new JObject { ["operation_id"] = Schema("string") }, "operation_id");
    }
    private void Add(string name, string command, string description, bool readOnly = false, JObject properties = null, params string[] required)
    {
        properties = properties ?? new JObject();
        if (command != null)
        {
            properties["timeout"] = Number(0,300); properties["timeout"]["description"] = "Client wait seconds; default 15. A timeout does not cancel or repeat the operation.";
            properties["queue_timeout"] = Number(1,300); properties["queue_timeout"]["description"] = "Expiry seconds for unstarted work; default 30.";
            properties["operation_id"] = Schema("string"); properties["operation_id"]["description"] = "Optional UUID for deduplication and recovery if the HTTP response is lost.";
        }
        tools.Add(name, new Tool { Name = name, Command = command, Description = description, Properties = properties, Required = required, ReadOnly = readOnly });
    }
    public JObject List() => new JObject { ["tools"] = new JArray(tools.Values.Select(t => t.Definition())) };
    public bool Contains(string name) => name != null && tools.ContainsKey(name);
    // Validation covers the JSON Schema keywords used by these concrete schemas;
    // this is not a general schema or reflection service.
    private static void Validate(JToken value, JObject schema, string path)
    {
        var declared = schema["type"];
        string[] types = declared is JArray array ? array.Values<string>().ToArray() : new[] { (string)declared };
        bool match = types.Any(t => t == "string" && value.Type == JTokenType.String || t == "boolean" && value.Type == JTokenType.Boolean ||
            t == "integer" && value.Type == JTokenType.Integer || t == "number" && (value.Type == JTokenType.Integer || value.Type == JTokenType.Float) ||
            t == "null" && value.Type == JTokenType.Null || t == "object" && value is JObject || t == "array" && value is JArray);
        if (!match) throw new ArgumentException(path + " has an invalid type");
        if (schema["enum"] is JArray choices && !choices.Any(v => JToken.DeepEquals(v, value))) throw new ArgumentException(path + " is not an allowed value");
        if (value.Type == JTokenType.Integer || value.Type == JTokenType.Float)
        {
            double n = value.Value<double>();
            if (double.IsNaN(n) || double.IsInfinity(n) || schema["minimum"] != null && n < (double)schema["minimum"] || schema["maximum"] != null && n > (double)schema["maximum"])
                throw new ArgumentException(path + " is outside its range");
        }
        if (value is JObject obj)
        {
            var properties = schema["properties"] as JObject;
            foreach (var key in (schema["required"] as JArray ?? new JArray()).Values<string>()) if (obj[key] == null) throw new ArgumentException(path + "." + key + " is required");
            foreach (var property in obj.Properties())
            {
                if (properties?[property.Name] is JObject field) Validate(property.Value, field, path + "." + property.Name);
                else if (schema["additionalProperties"] is JObject extra) Validate(property.Value, extra, path + "." + property.Name);
                else if (schema["additionalProperties"]?.Type == JTokenType.Boolean && !(bool)schema["additionalProperties"]) throw new ArgumentException("Unknown argument " + path + "." + property.Name);
            }
        }
        if (value is JArray list)
        {
            if (schema["maxItems"] != null && list.Count > (int)schema["maxItems"]) throw new ArgumentException(path + " has too many entries");
            if (schema["items"] is JObject item) for (int i = 0; i < list.Count; i++) Validate(list[i], item, path + "[" + i + "]");
        }
    }
    public JObject Call(string name, JObject arguments)
    {
        var tool = tools[name];
        Validate(arguments, (JObject)tool.Definition()["inputSchema"], "arguments");
        JObject op;
        if (tool.Command == null)
        {
            string id = (string)arguments["operation_id"];
            if (!Guid.TryParse(id, out _)) throw new ArgumentException("operation_id must be a UUID");
            op = name == "get_operation" ? queue.Get(id, (bool?)arguments["include_details"] ?? false) : queue.Cancel(id);
            if (op == null) return Result(Envelope(false, null, null, new JObject { ["code"] = "unknown_operation", ["message"] = "Unknown operation; it may belong to a previous game process" }, "failed"));
            if (name == "get_operation" && arguments["result_path"] != null)
            {
                string path = (string)arguments["result_path"];
                var selected = op["result"]?.SelectToken(path);
                op["result"] = new JObject { ["path"] = path, ["found"] = selected != null,
                    ["value"] = ObservationOutput.Slice(selected, ObservationOutput.Offset(arguments), ObservationOutput.Limit(arguments)) };
            }
            return Result(Envelope(true, op, op, null, (string)op["state"]));
        }
        var args = (JObject)arguments.DeepClone();
        string sid = (string)args["session_id"], world = (string)args["world_id"];
        string operationId = (string)args["operation_id"] ?? Guid.NewGuid().ToString();
        if (!Guid.TryParse(operationId, out _)) throw new ArgumentException("operation_id must be a UUID");
        int timeout = (int?)args["timeout"] ?? 15, queueTimeout = (int?)args["queue_timeout"] ?? 30;
        foreach (string key in new[] { "session_id", "world_id", "operation_id", "timeout", "queue_timeout" }) args.Remove(key);
        foreach (var pair in new[] { new[] { "mod_id", "modId" }, new[] { "mod_data", "modData" }, new[] { "include_labels", "includeLabels" } })
            if (args[pair[0]] != null) { args[pair[1]] = args[pair[0]]; args.Remove(pair[0]); }
        op = queue.Submit(new BridgeRequest { id = operationId, command = tool.Command, arguments = args, sessionId = sid, worldId = world,
            expiresAt = DateTimeOffset.UtcNow.AddSeconds(queueTimeout).ToUnixTimeMilliseconds() });
        var elapsed = Stopwatch.StartNew();
        while ((string)op["state"] == "queued" || (string)op["state"] == "running")
        {
            if (elapsed.Elapsed.TotalSeconds >= timeout || stopping())
            {
                var pending = Envelope(null, op["result"], op, null, (string)op["state"]);
                pending["nextAction"] = "query_operation"; pending["hint"] = "Query the original operationId; do not repeat a write under a new ID.";
                return Result(pending);
            }
            Thread.Sleep(10); op = queue.Get(operationId);
        }
        return Result(Envelope((string)op["state"] == "succeeded", op["result"], op, op["error"], (string)op["state"]));
    }
    private static JToken Field(JObject value, string key) => value?[key]?.Type == JTokenType.Null ? null : value?[key];
    private static JObject Envelope(bool? ok, JToken payload, JObject op, JToken error, string state) => new JObject {
        ["ok"] = ok.HasValue ? new JValue(ok.Value) : JValue.CreateNull(), ["result"] = payload?.DeepClone(),
        ["operationId"] = op?["id"], ["sessionId"] = Field(op, "sessionId") ?? Field(payload as JObject, "sessionId"),
        ["worldId"] = Field(op, "worldId") ?? Field(payload as JObject, "worldId"), ["executionState"] = state, ["error"] = error?.DeepClone()
    };
    private static JObject Result(JObject payload)
    {
        payload = ObservationOutput.Bound(payload);
        return new JObject {
        ["content"] = new JArray(new JObject { ["type"] = "text", ["text"] = payload.ToString(Formatting.None) }),
        ["structuredContent"] = payload, ["isError"] = payload["ok"].Type == JTokenType.Boolean && !(bool)payload["ok"]
        };
    }
}
