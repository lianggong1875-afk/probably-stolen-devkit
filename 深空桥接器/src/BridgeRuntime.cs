using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace ModDevBridge;

// Helpers for new probes. Resources must be registered when acquired, before later work can fail.
public static class DevContext
{
    internal static ResourceScope Scope;
    internal static string WorldId;
    public static void Track(string name, Action cleanup) => (Scope ?? throw new InvalidOperationException("No test session")).Track(name, cleanup);
    public static T TrackItem<T>(T item) where T : GameItem { Track("item:" + item.identifier, () => { if (item != null && (!(item is GameItemElement element) || !element.IsDestroyed())) item.Destroy(); }); return item; }
    public static void TrackHarmony(Harmony harmony) => Track("patches:" + harmony.Id, () => harmony.UnpatchAll(harmony.Id));
    public static void TrackWindow(PixelWindow window) => Track("window", () => window.Hide());
}

public sealed class BridgeRuntime
{
    public OperationQueue Queue;
    internal readonly RuntimeLogs Logs = new RuntimeLogs();
    public string WorldId { get; private set; } = Guid.NewGuid().ToString("N");
    private bool loaded;
    private PlayerStore observedStore;
    private bool wasInMenu = true;
    private string sessionId, sessionWorld;
#if BRIDGE_FULL
    private string moduleName;
    private IDevModule module;
#else
    private const string moduleName = null;
#endif
    private JObject baseline;
    private readonly SnapshotQueries snapshots = new SnapshotQueries();
    private readonly ItemQueries items = new ItemQueries();
    private readonly UiQueries ui = new UiQueries();
    private readonly CustomerTools customers = new CustomerTools();
    private ResourceScope scope;
    private static bool Menu => NewGameData.Instance == null || NewGameData.Instance.isInMainMenu;
    public bool Ready => loaded && !Menu && PlayerStore.HasInstance && EmporiumEntry.Instance != null;
    public void MarkLoaded() { observedStore = PlayerStore.HasInstance ? PlayerStore.Instance : null; wasInMenu = Menu; loaded = true; WorldId = Guid.NewGuid().ToString("N"); DevContext.WorldId = null; }
    public void ObserveWorld()
    {
        var current = PlayerStore.HasInstance ? PlayerStore.Instance : null;
        bool menu = Menu;
        if (current != observedStore || menu != wasInMenu)
        {
            WorldId = Guid.NewGuid().ToString("N");
            DevContext.WorldId = null;
            if (current != observedStore || menu) loaded = false;
            observedStore = current; wasInMenu = menu;
        }
    }
    private void NeedReady() { if (!Ready) throw new BridgeFailure("game_not_ready", "Load a game save and wait for OnGameLoadedLate", "preflight"); }
    private void NeedSession(BridgeRequest r)
    {
        if (sessionId == null || r.sessionId != sessionId) throw new BridgeFailure("session_mismatch", "A matching test session is required", "preflight");
        if (sessionWorld != WorldId) throw new BridgeFailure("session_world_changed", "Session belongs to a previous world; close it and open a new session", "preflight");
        DevContext.Scope = scope;
        DevContext.WorldId = WorldId;
    }
    public object Execute(BridgeRequest r)
    {
#if !BRIDGE_FULL
        if (r.command == "load" || r.command == "call" || r.command == "reload_mod") throw ReleaseInfo.FullRequired();
#endif
        var a = r.arguments ?? new JObject();
        switch (r.command)
        {
            case "status": return Status();
            case "development_environment": return DevelopmentQueries.EnvironmentInfo();
            case "query_probes": return DevelopmentQueries.Probes(a);
            case "reload_mod": NeedReady(); NeedSession(r); return NativeModReload.Execute((string)a["modId"], (string)a["filename"]);
            case "logs": return Logs.Read(a);
            case "inspect": return RuntimeInspector.Inspect((string)a["kind"] ?? "mods", (string)a["target"], a);
            case "query_items": NeedReady(); return items.Find(WorldId, a);
            case "query_customers": if ((string)a["scope"] != "presets") NeedReady(); return customers.Query(WorldId,a);
            case "enqueue_customer": NeedReady(); NeedSession(r); return customers.Enqueue(WorldId,a,scope,() => WorldId);
            case "item_details": NeedReady(); return items.Details(WorldId, a);
            case "inspection_state": NeedReady(); return new InspectionTools(items).State(WorldId);
            case "inspection_operate": NeedReady(); NeedSession(r); return new InspectionTools(items).Operate(WorldId,a,scope,() => WorldId);
            case "query_localization": return LocalizationQueries.Read(a);
            case "query_ui": return ui.Find(WorldId,a);
            case "ui_details": return ui.Details(WorldId,a);
            case "ui_raycast": return ui.Raycast(WorldId,a);
            case "ui_pointer": return UiActions.Run(WorldId,a,ui,Queue,() => WorldId);
            case "list_configs": return ConfigService.List();
            case "get_config": return ConfigService.Get((string)a["modId"] ?? "", (bool?)a["includeLabels"] ?? false);
            case "set_config":
                if ((bool?)a["track"] ?? true) NeedSession(r);
                return ConfigService.Set((string)a["modId"], (string)a["key"], a["value"],
                    (bool?)a["persist"] ?? false, (bool?)a["track"] ?? true, ((bool?)a["track"] ?? true) ? scope : null, () => sessionWorld == WorldId);
            case "seed_scenario":
                NeedReady();
                if ((bool?)a["track"] ?? true) NeedSession(r);
                return ScenarioSeeder.Seed(a, ((bool?)a["track"] ?? true) ? scope : null, WorldId, () => WorldId);
            case "machine_find": NeedReady(); return MachineSandbox.Find(a);
            case "machine_fast_forward": NeedReady(); NeedSession(r); return MachineSandbox.FastForward(a);
            case "machine_skip_night": NeedReady(); NeedSession(r); return MachineSandbox.SkipNight(a);
            case "machine_recharge": NeedReady(); NeedSession(r); return MachineSandbox.RechargeAll(a);
            case "debug_list": return DebugActions.List(a);
            case "debug_invoke": NeedReady(); NeedSession(r); return DebugActions.Invoke(a);
            case "snapshot": NeedReady(); return snapshots.Read(WorldId, a, Snapshot);
            case "snapshot_diff": NeedReady(); return snapshots.Diff(WorldId, a, Snapshot);
            case "save_test_slot":
                NeedReady();
                if (sessionId != null) throw new InvalidOperationException("Close and clean the test session before saving");
                int testSlot = (int?)a["testSlot"] ?? -1;
                if (testSlot < 0 || PlayerStore.Instance.saveSlotId != testSlot) throw new InvalidOperationException("Explicit testSlot must match the loaded save slot");
                PlayerStore.Instance.SaveGame();
                return new { slot = testSlot, file = SaveFiles.AbsolutePath(SaveFiles.Slot(testSlot)), attempted = true, verified = false };
            case "open_session":
                NeedReady();
                if (sessionId != null) throw new InvalidOperationException("A test session is already active: " + sessionId);
                var nextScope = new ResourceScope();
                var nextBaseline = Snapshot(SnapshotQueries.Fields(a));
                sessionId = Guid.NewGuid().ToString("N"); sessionWorld = WorldId; scope = nextScope; baseline = nextBaseline; DevContext.Scope = scope; DevContext.WorldId = WorldId;
                return new { sessionId, worldId = WorldId, baseline = (bool?)a["include_snapshot"] == true ? baseline : SnapshotQueries.Summary(baseline),
                    snapshotIncluded = (bool?)a["include_snapshot"] == true, fields = baseline.Properties().Select(p => p.Name).ToArray(),
                    omittedFields = SnapshotQueries.AllFields.Except(baseline.Properties().Select(p => p.Name)).ToArray() };
            case "close_session":
                if (sessionId == null || r.sessionId != sessionId) throw new BridgeFailure("session_mismatch", "A matching test session is required", "preflight");
                return CloseSession((bool?)a["include_snapshot"] ?? false); // Allow cleanup after a world change.
            case "load": NeedReady(); NeedSession(r); return Load((string)a["filename"]);
            case "call":
                NeedReady(); NeedSession(r);
                return Call((string)a["command"] ?? "");
            case "stop": NeedSession(r); return Stop();
            case "run_test": NeedReady(); NeedSession(r); return RunTest(a);
            default: throw new ArgumentException("Unknown bridge command: " + r.command);
        }
    }
    private object Status() => new {
        protocolVersion = 1, bridgeVersion = ReleaseInfo.Version, releaseId = ReleaseInfo.Id,
        edition = ReleaseInfo.Edition, dynamicModules = ReleaseInfo.DynamicModules, ready = Ready, mainMenu = Menu, worldId = WorldId,
        scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
        gameVersion = Application.version, gameAssembly = typeof(PlayerStore).Assembly.Location,
        unityVersion = Application.unityVersion, saveDirectory = Application.persistentDataPath,
        saveSlot = Ready ? (int?)PlayerStore.Instance.saveSlotId : null, saveFile = NewGameData.Instance?.saveFile,
        day = Ready ? (int?)StoreStation.GetDayCounter() : null,
        playerLocation = Ready ? PlayerStore.Instance.playerLocation.ToString() : null,
        module = moduleName, sessionId, sessionWorld, resources = scope?.Report(),
        loadedDevelopmentAssemblies = AppDomain.CurrentDomain.GetAssemblies().Count(x => x.GetName().Name.Contains("_dev_") || x.GetName().Name.StartsWith("NativeModReloader_"))
    };
    public static JObject Snapshot() => Snapshot(SnapshotQueries.LegacyFields);
    private static JObject Snapshot(string[] fields)
    {
        if (!PlayerStore.HasInstance || EmporiumEntry.Instance == null) throw new InvalidOperationException("No loaded save");
        var store = PlayerStore.Instance;
        var result = new JObject();
        foreach (string field in fields) switch (field)
        {
            case "slot": result[field] = store.saveSlotId; break;
            case "day": result[field] = StoreStation.GetDayCounter(); break;
            case "cash": result[field] = store.GetCash(); break;
            case "inventory": result[field] = JObject.FromObject(EmporiumEntry.Instance.GetAllOwnedItems().GroupBy(i => i.identifier).OrderBy(g => g.Key).ToDictionary(g => g.Key, g => g.Sum(i => i.unitCount))); break;
            case "contacts": result[field] = new JArray(store.PhoneClientDict.Keys.OrderBy(n => n)); break;
            case "modData": result[field] = JObject.FromObject(store.modData == null ? new Dictionary<string, string>() : new Dictionary<string, string>(store.modData)); break;
            case "mods": result[field] = JArray.FromObject(ModLoader.Mods.OrderBy(m => m.Manifest.ID, StringComparer.Ordinal).Select(m => new { id = m.Manifest.ID, enabled = m.IsEnabled, version = m.Manifest.ModVersion })); break;
            case "store_state": case "professions": case "perks": case "finance": case "inspection": result[field] = StoreStateReader.Read(field); break;
            default: throw new ArgumentException("Unknown snapshot field: " + field);
        }
        return result;
    }
    private object Load(string filename)
    {
#if BRIDGE_FULL
        if (module != null) throw new InvalidOperationException("Stop the active module first");
        if (string.IsNullOrEmpty(filename) || Path.GetFileName(filename) != filename || !filename.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Expected a DLL filename from modules/");
        var path = Path.Combine(BridgePaths.Modules, filename);
        string identity = AssemblyName.GetAssemblyName(path).Name;
        if (AppDomain.CurrentDomain.GetAssemblies().Any(x => x.GetName().Name == identity)) throw new InvalidOperationException("Assembly name already loaded; rebuild with a unique name");
        bool assemblyLoaded = false;
        string stage = "load.assembly";
        try
        {
            var assembly = Assembly.Load(File.ReadAllBytes(path)); assemblyLoaded = true;
            stage = "load.types";
            var types = assembly.GetTypes().Where(t => typeof(IDevModule).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface).ToArray();
            if (types.Length != 1) throw new InvalidOperationException("Module must contain exactly one IDevModule implementation");
            stage = "load.construct";
            var next = (IDevModule)Activator.CreateInstance(types[0]);
            module = next; moduleName = filename;
            scope.Track("module:" + filename, () => { next.Stop(); if (module == next) { module = null; moduleName = null; } });
            stage = "load.start";
            return CheckText(next.Start());
        }
        catch (Exception error)
        {
            var cleanup = scope.Cleanup();
            throw new BridgeFailure("module_load_failed", error.Message, stage,
                new { module = filename, startResult = (error as BridgeFailure)?.PartialResult, resources = cleanup,
                    cleanupStatus = scope.Status, assemblyStillLoaded = assemblyLoaded, verification = "not_checked" }, inner: error);
        }
#else
        throw ReleaseInfo.FullRequired();
#endif
    }
    private object Call(string command)
    {
#if BRIDGE_FULL
        if (module == null) throw new InvalidOperationException("No active module");
        if (module is IStructuredDevModule structured) return structured.ExecuteStructured(command);
        return CheckText(module.Execute(command));
#else
        throw ReleaseInfo.FullRequired();
#endif
    }
    private static object CheckText(string text)
    {
        string trimmed = (text ?? "").TrimStart();
        if (trimmed.StartsWith("ERROR", StringComparison.OrdinalIgnoreCase) || trimmed.StartsWith("FAIL:", StringComparison.OrdinalIgnoreCase))
            throw new BridgeFailure("probe_reported_failure", text, "probe", new { text, assertionVerified = false });
        return new { text = text ?? "", assertionVerified = false };
    }
    private object Stop()
    {
        var report = scope.Cleanup();
        var result = new { cleaned = scope.Clean, cleanupStatus = scope.Status, resources = report };
        if (!scope.Settled) throw new BridgeFailure("cleanup_incomplete", "Cleanup incomplete; retry stop or close using the same session", "cleanup", result);
        return result;
    }
    private object CloseSession(bool includeSnapshot = false)
    {
        var resources = scope.Cleanup();
        JObject after = null, snapshotError = null;
        try { if (Ready && sessionWorld == WorldId) after = Snapshot(baseline.Properties().Select(p => p.Name).ToArray()); }
        catch (Exception error) { snapshotError = BridgeFailure.Describe(error, "cleanup.snapshot"); }
        var result = new { sessionId, worldId = sessionWorld, cleaned = scope.Clean, cleanupStatus = scope.Status,
            sessionReleased = scope.Settled, resources, worldChanged = sessionWorld != WorldId,
            before = includeSnapshot ? baseline : SnapshotQueries.Summary(baseline),
            after = after == null ? null : includeSnapshot ? after : SnapshotQueries.Summary(after), snapshotIncluded = includeSnapshot,
            diff = SnapshotQueries.SessionDiff(baseline, after, sessionWorld == WorldId),
            snapshotError, verification = "not_checked" };
        if (!scope.Settled) throw new BridgeFailure("cleanup_incomplete", "Cleanup incomplete; session retained for retry", "cleanup", result);
        sessionId = null; sessionWorld = null; scope = null; baseline = null; DevContext.Scope = null; DevContext.WorldId = null;
        return result;
    }
    // One step per frame, with a bounded wait. Assertions are explicit, not inferred from prose.
    private IEnumerator RunTest(JObject args)
    {
        var steps = args["steps"] as JArray ?? throw new ArgumentException("steps array required");
        if (steps.Count > 100) throw new ArgumentException("At most 100 steps");
        var results = new JArray();
        var report = new JObject { ["passed"] = false, ["steps"] = results, ["completedSteps"] = 0, ["totalSteps"] = steps.Count };
        string testWorld = sessionWorld;
        Action checkWorld = () => {
            if (testWorld != WorldId) throw new BridgeFailure("session_world_changed", "World changed during the test; no further steps were executed", "test.checkpoint", report);
            NeedReady();
        };
        Queue.RoutineResult = report;
        try
        {
            foreach (JObject step in steps)
            {
                checkWorld();
                string kind = (string)step["kind"];
                report["completedSteps"] = results.Count;
                report["currentStepIndex"] = results.Count;
                report["currentStep"] = step.DeepClone();
                report.Remove("currentActual");
                Queue.ReportProgress(new { completedSteps = results.Count, totalSteps = steps.Count, currentStep = kind });
                if (kind == "wait_until")
                {
                    int limit = (int?)step["maxFrames"] ?? 120;
                    if (limit < 1 || limit > 600) throw new ArgumentException("maxFrames must be 1..600");
                    bool matched = false;
                    for (int i = 0; i < limit; i++)
                    {
                        checkWorld();
                        var state = JToken.FromObject(RuntimeInspector.Inspect((string)step["targetKind"] ?? "mods", (string)step["target"], step));
                        report["currentActual"] = state.SelectToken((string)step["path"] ?? "$")?.DeepClone();
                        if (JToken.DeepEquals(state.SelectToken((string)step["path"] ?? "$"), step["equals"])) { matched = true; break; }
                        yield return null;
                    }
                    if (!matched) throw new BridgeFailure("condition_timeout", "wait_until condition not met", "test.wait_until", report);
                    results.Add(new JObject { ["kind"] = kind, ["matched"] = true });
                    report["completedSteps"] = results.Count;
                    continue;
                }
                if (kind == "wait_frames")
                {
                    int frames = (int?)step["frames"] ?? 1;
                    if (frames < 1 || frames > 600) throw new ArgumentException("frames must be 1..600");
                    for (int i = 0; i < frames; i++) { checkWorld(); yield return null; }
                    results.Add(new JObject { ["kind"] = kind, ["frames"] = frames });
                    report["completedSteps"] = results.Count;
                    continue;
                }
                object output = kind == "call" ? Call((string)step["command"] ?? "") :
                    kind == "snapshot" ? Snapshot(SnapshotQueries.Fields(step)) : kind == "inspect" ? RuntimeInspector.Inspect((string)step["targetKind"] ?? "mods", (string)step["target"], step) :
                    throw new ArgumentException("Unknown test step " + kind);
                JToken data = output == null ? JValue.CreateNull() : JToken.FromObject(output);
                report["currentActual"] = data.DeepClone();
                if (step["equals"] != null)
                {
                    var actual = data.SelectToken((string)step["path"] ?? "$");
                    if (!JToken.DeepEquals(actual, step["equals"])) throw new BridgeFailure("assertion_failed", "Assertion failed: " + step["path"], "test.assert", report);
                }
                if (step["contains"] != null && !data.ToString().Contains((string)step["contains"])) throw new BridgeFailure("assertion_failed", "Expected result to contain " + step["contains"], "test.assert", report);
                results.Add(new JObject { ["kind"] = kind, ["result"] = data });
                report["completedSteps"] = results.Count;
                yield return null;
            }
            checkWorld();
            report["passed"] = true;
            report.Remove("currentStepIndex"); report.Remove("currentStep"); report.Remove("currentActual");
        }
        finally { report["resources"] = JArray.FromObject(scope.Report()); }
    }
    public void Shutdown()
    {
        if (scope != null) { scope.Cleanup(); if (!scope.Clean) Debug.LogError("[ModDevBridge] Remaining resources: " + JArray.FromObject(scope.Report())); }
        DevContext.Scope = null;
        DevContext.WorldId = null;
    }
}
