using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using HarmonyLib;
using Newtonsoft.Json.Linq;

namespace ModDevBridge;

// Invoked on Unity's main thread. MOD lifecycle owns callbacks/resources.
// Closing the test session does not undo a successful replacement.
public static class NativeModReload
{
#if BRIDGE_FULL
    public static bool HasHooks(IMod mod) => mod != null && Hook(mod.GetType(), "HotReloadPrepare") != null && Hook(mod.GetType(), "HotReloadActivate") != null;
    private static MethodInfo Hook(Type type, string name)
    {
        var method = type.GetMethod(name, BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
        return method != null && method.ReturnType == typeof(void) && !method.ContainsGenericParameters ? method : null;
    }
    private static void Invoke(MethodInfo method, IMod instance)
    {
        try { method.Invoke(instance, null); }
        catch (TargetInvocationException e) when (e.InnerException != null) { throw e.InnerException; }
    }
    private static JObject Describe(IMod instance, Assembly assembly, Harmony harmony) => new JObject {
        ["assembly"] = assembly?.GetName().Name, ["entry"] = instance?.GetType().FullName, ["harmonyOwner"] = harmony?.Id
    };
    private static int OwnerCount(string owner) => owner == null ? 0 : Harmony.GetAllPatchedMethods().Count(method => {
        var p = Harmony.GetPatchInfo(method);
        return p != null && p.Prefixes.Concat(p.Postfixes).Concat(p.Transpilers).Concat(p.Finalizers).Any(x => x.owner == owner);
    });
    private static JObject PatchObservation(string previous, string current, string candidate)
    {
        try { return new JObject { ["available"] = true, ["previousOwnerPatchedMethods"] = OwnerCount(previous),
            ["currentOwnerPatchedMethods"] = OwnerCount(current), ["candidateOwnerPatchedMethods"] = OwnerCount(candidate) }; }
        catch (Exception e) { return new JObject { ["available"] = false, ["error"] = BridgeFailure.Describe(e, "reload.observe") }; }
    }
    public static JObject Execute(string modId, string filename)
    {
        var report = new JObject { ["modId"] = modId, ["filename"] = filename, ["completed"] = false,
            ["candidateAssemblyLoaded"] = false, ["candidateConstructed"] = false, ["replacementRetainedOnSessionClose"] = true,
            ["assetsReloaded"] = false, ["assembliesRemainLoaded"] = true, ["effectVerification"] = "not_checked" };
        string stage = "reload.preflight";
        try
        {
            if (string.IsNullOrWhiteSpace(modId)) throw new ArgumentException("mod_id is required");
            if (string.IsNullOrEmpty(filename) || Path.GetFileName(filename) != filename || !filename.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Expected a DLL filename from moduleDirectory");
            var target = ModLoader.Mods.SingleOrDefault(m => m.Manifest.ID == modId && m.IsEnabled && m.Instance != null);
            if (target == null || target.HarmonyInstance == null) throw new ArgumentException("Enabled MOD with Harmony instance not found: " + modId);
            if (target.Assembly == typeof(NativeModReload).Assembly) throw new ArgumentException("Restart the game to update the Bridge itself");
            var old = target.Instance; var oldAssembly = target.Assembly; var oldHarmony = target.HarmonyInstance;
            report["previous"] = Describe(old, oldAssembly, oldHarmony);
            if (!HasHooks(old)) throw new ArgumentException("Running MOD must declare public void HotReloadPrepare() and HotReloadActivate(); hook presence does not verify cleanup safety");
            string path = Path.Combine(BridgePaths.Modules, filename);
            byte[] bytes = File.ReadAllBytes(path);
            using (var sha = SHA256.Create()) report["candidateSha256"] = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
            stage = "reload.load";
            string identity = AssemblyName.GetAssemblyName(path).Name;
            if (AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == identity)) throw new ArgumentException("Assembly name already loaded; rebuild with a unique assembly name");
            var assembly = Assembly.Load(bytes); report["candidateAssemblyLoaded"] = true;
            report["candidateAssembly"] = assembly.GetName().Name;
            var entries = assembly.GetTypes().Where(t => typeof(IMod).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface).ToArray();
            if (entries.Length != 1 || !entries[0].IsPublic || entries[0].GetConstructor(Type.EmptyTypes) == null) throw new ArgumentException("Candidate needs exactly one public concrete IMod with a public parameterless constructor");
            if (entries[0].FullName != old.GetType().FullName) throw new ArgumentException("Candidate entry type must match the running MOD's full name");
            var nextPrepare = Hook(entries[0], "HotReloadPrepare"); var nextActivate = Hook(entries[0], "HotReloadActivate");
            if (nextPrepare == null || nextActivate == null) throw new ArgumentException("Candidate is missing valid hot-reload hooks");
            stage = "reload.construct";
            var next = (IMod)Activator.CreateInstance(entries[0]); report["candidateConstructed"] = true;
            var nextHarmony = new Harmony("mod." + modId + ".dev." + Guid.NewGuid().ToString("N"));
            stage = "reload.switch";
            var transaction = HotReloadTransaction.Run(new HotReloadPlan {
                OldPrepare = () => Invoke(Hook(old.GetType(), "HotReloadPrepare"), old), OldDisable = old.OnDisable,
                OldUnpatch = () => oldHarmony.UnpatchAll(oldHarmony.Id), OldPatch = () => oldHarmony.PatchAll(oldAssembly),
                OldInit = () => old.Init(target.Manifest), OldEnable = old.OnEnable, OldActivate = () => Invoke(Hook(old.GetType(), "HotReloadActivate"), old),
                NextPatch = () => nextHarmony.PatchAll(assembly), NextInit = () => next.Init(target.Manifest), NextEnable = next.OnEnable,
                NextActivate = () => Invoke(nextActivate, next), NextPrepare = () => Invoke(nextPrepare, next), NextDisable = next.OnDisable,
                NextUnpatch = () => nextHarmony.UnpatchAll(nextHarmony.Id),
                Commit = () => { target.Instance = next; target.Assembly = assembly; target.HarmonyInstance = nextHarmony; }
            });
            report["transaction"] = JObject.FromObject(transaction);
            report["current"] = Describe(target.Instance, target.Assembly, target.HarmonyInstance);
            report["loaderReferencesMatchCandidate"] = ReferenceEquals(target.Instance, next) && target.Assembly == assembly && ReferenceEquals(target.HarmonyInstance, nextHarmony);
            report["patches"] = PatchObservation(oldHarmony.Id, target.HarmonyInstance?.Id, nextHarmony.Id);
            report["completed"] = transaction.success;
            if (!transaction.success) throw new BridgeFailure("reload_failed", "Reload failed; inspect stage and compensation. Gameplay effects were not verified.", transaction.failedStage, report,
                "MODs own event/resource cleanup. Inspect the previous version after compensation; restart if runtime state is uncertain.");
            return report;
        }
        catch (BridgeFailure) { throw; }
        catch (Exception e)
        {
            throw new BridgeFailure("reload_preflight_failed", e.Message, stage, report,
                "Candidate loading/constructors may have run; inspect candidateAssemblyLoaded/candidateConstructed. Assemblies remain loaded.", e);
        }
    }
#else
    public static bool HasHooks(IMod mod) => false;
    public static JObject Execute(string modId, string filename) => throw ReleaseInfo.FullRequired();
#endif
}
