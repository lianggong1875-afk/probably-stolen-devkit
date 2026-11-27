using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace ModDevBridge;

internal static class DevelopmentQueries
{
    public static JObject Probes(JObject args)
    {
        string name = (string)args["name"], contains = (string)args["contains"], purpose = (string)args["purpose"];
        bool source = (bool?)args["include_source"] ?? false, details = source || ((bool?)args["include_details"] ?? false);
        if (source && string.IsNullOrWhiteSpace(name)) throw new ArgumentException("include_source requires an exact name");
        var all = ProbeCatalogData.Read();
        var matches = all.OfType<JObject>().Where(p =>
            (name == null || string.Equals((string)p["name"], name, StringComparison.OrdinalIgnoreCase)) &&
            (purpose == null || string.Equals((string)p["purpose"], purpose, StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrEmpty(contains) || new[] { "name", "purpose", "summary", "commands" }.Any(k =>
                (p[k]?.ToString() ?? "").IndexOf(contains, StringComparison.OrdinalIgnoreCase) >= 0))).ToArray();
        int offset = ObservationOutput.Offset(args), limit = args["limit"] == null ? 10 : ObservationOutput.Limit(args);
        var entries = new JArray();
        foreach (var item in matches.Skip(offset).Take(limit))
        {
            var row = (JObject)item.DeepClone();
            if (!source) row.Remove("source");
            if (!details) foreach (string key in new[] { "commands", "sideEffects", "cleanup", "sourceSha256" }) row.Remove(key);
            entries.Add(row);
        }
        return new JObject { ["catalogVersion"] = 1, ["scope"] = "curated_public_sources",
            ["totalAvailable"] = all.Count, ["totalMatched"] = matches.Length, ["entries"] = entries,
            ["offset"] = offset, ["nextOffset"] = offset + entries.Count < matches.Length ? (JToken)(offset + entries.Count) : JValue.CreateNull(),
            ["searchComplete"] = true, ["sourceIncluded"] = source,
            ["notice"] = "Metadata review is not runtime validation. Historical internal fixtures are not part of this public catalog; Start executes on load." };
    }
    public static JObject EnvironmentInfo()
    {
        string gameAssembly = typeof(PlayerStore).Assembly.Location, sdk = typeof(IDevModule).Assembly.Location;
        return new JObject {
            ["bridgeVersion"] = ReleaseInfo.Version, ["releaseId"] = ReleaseInfo.Id,
            ["edition"] = ReleaseInfo.Edition, ["dynamicModules"] = ReleaseInfo.DynamicModules,
            ["fullSourceProject"] = Path.Combine(Path.GetDirectoryName(sdk), "FullSource", "ModDevBridgeNext.csproj"),
            ["editionGuide"] = "Read README.md before compiling or installing Full; the player's consent and manual local Developer mode are required.",
            ["guideUri"] = "moddevbridge://guides/skill", ["gameVersion"] = Application.version, ["unityVersion"] = Application.unityVersion,
            ["managedDirectory"] = Path.GetDirectoryName(gameAssembly), ["gameAssembly"] = gameAssembly,
            ["sdkAssembly"] = sdk, ["runtimeRoot"] = BridgePaths.Root, ["moduleDirectory"] = BridgePaths.Modules,
            ["documentationDirectory"] = Path.GetDirectoryName(sdk),
            ["readmePath"] = Path.Combine(Path.GetDirectoryName(sdk), "README.md"),
            ["guidePath"] = Path.Combine(Path.GetDirectoryName(sdk), "AI_GUIDE.md"),
            ["skillPath"] = Path.Combine(Path.GetDirectoryName(sdk), "SKILL.md"),
            ["compilation"] = new JObject { ["target"] = "Unity Mono class library", ["references"] = "Use installed game Managed assemblies and sdkAssembly; target the game's Mono framework, not net8/net10 runtime libraries.",
                ["assemblyIdentity"] = "Unique assembly name per build; renaming a compiled DLL does not change its identity.",
                ["staging"] = "Copy your compiled DLL to moduleDirectory, then pass only its filename to load_probe_module. Keep project/source outside the Bridge directories." },
            ["moduleContract"] = new JObject { ["available"] = ReleaseInfo.DynamicModules,
                ["namespace"] = "ModDevBridge", ["entry"] = "Exactly one concrete public IDevModule with a public parameterless constructor",
                ["IDevModule"] = "string Start(); string Execute(string command); void Stop();",
                ["IStructuredDevModule"] = "IDevModule plus object ExecuteStructured(string command); returns JSON-serializable values",
                ["resources"] = "DevContext.Track(string name, Action cleanup); register resources immediately. Stop/cleanup must tolerate repeated calls.",
                ["lifecycle"] = "Start runs immediately on the Unity main thread. Close the owning session to attempt cleanup; inspect failed/skipped resources. Loaded assemblies remain until game restart.",
                ["example"] = "query_probes name=RuntimeExperiment include_source=true",
                ["finalMod"] = "Final MOD projects use native IMod and must not reference the Bridge SDK." }
        };
    }
}
