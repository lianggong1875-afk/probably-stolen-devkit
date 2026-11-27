using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ModDevBridge;

// Files here describe live processes. They do not own user projects or backups.
internal sealed class Distribution
{
    private readonly string root, instance, assemblyPath, startupSha256;
    private readonly JObject identity;
    public string InstanceFile => Path.Combine(root, "instances", instance + ".json");
    public Distribution(string runtimeRoot, string instanceId, string url, string path = null)
    {
        root = runtimeRoot; instance = instanceId;
        assemblyPath = path ?? typeof(Distribution).Assembly.Location;
        startupSha256 = Hash(assemblyPath);
        identity = new JObject {
            ["schemaVersion"] = 2, ["protocolVersion"] = 1, ["instanceId"] = instance,
            ["url"] = url, ["mcpUrl"] = url + "/mcp", ["diagnosticUrl"] = url + "/bridge/status",
            ["mcpTransport"] = "streamable-http", ["authentication"] = "none", ["bind"] = "127.0.0.1",
            ["portPolicy"] = "fixed",
            ["processId"] = JValue.CreateNull(), ["processStartedAtUtc"] = JValue.CreateNull(),
            ["processIdentityAvailability"] = "not_observed_game_scanner_restricts_Process_API",
            ["bridgeStartedAtUtc"] = DateTime.UtcNow.ToString("O"),
            ["publishedAtUtc"] = DateTime.UtcNow.ToString("O"),
            ["bridgeVersion"] = ReleaseInfo.Version, ["releaseId"] = ReleaseInfo.Id,
            ["edition"] = ReleaseInfo.Edition, ["dynamicModules"] = ReleaseInfo.DynamicModules,
            ["loadedModuleId"] = typeof(Distribution).Module.ModuleVersionId.ToString(),
            ["assemblyPath"] = assemblyPath, ["startupFileSha256"] = startupSha256,
            ["mcpProtocolVersions"] = new JArray("2025-11-25", "2025-06-18", "2025-03-26"),
            ["capabilities"] = new JObject { ["tools"] = true, ["resources"] = true },
            ["instanceFile"] = InstanceFile,
            ["notice"] = "Enumerate instances and verify diagnosticUrl/instanceId. connection.json is a legacy last-published hint, not a multi-instance selector. Transport liveness is not game readiness."
        };
    }
    internal static string Hash(string path)
    {
        using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
    }
    internal static void AtomicWrite(string path, object value)
    {
        System.IO.Directory.CreateDirectory(Path.GetDirectoryName(path));
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temp, JsonConvert.SerializeObject(value, Formatting.Indented));
            // Both operations publish a complete file. Never delete the old target first.
            for (int attempt = 0; ; attempt++)
            {
                try { if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path); break; }
                catch (IOException) when (attempt < 2) { }
            }
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    public void Publish()
    {
        WithRegistryLock(() => {
            AtomicWrite(InstanceFile, identity);
            AtomicWrite(Path.Combine(root, "connection.json"), identity);
        });
    }
    public JObject Status()
    {
        var report = (JObject)identity.DeepClone();
        report["transportAlive"] = true;
        report["gameReadiness"] = "call_get_game_status";
        var disk = new JObject { ["path"] = assemblyPath };
        try
        {
            string hash = Hash(assemblyPath), product = FileVersionInfo.GetVersionInfo(assemblyPath).ProductVersion;
            bool identified = !string.IsNullOrEmpty(product);
            disk["sha256"] = hash; disk["releaseId"] = product;
            disk["changedSinceStartup"] = hash != startupSha256;
            disk["matchesLoadedRelease"] = identified ? (JToken)(product == ReleaseInfo.Id) : JValue.CreateNull();
            disk["restartRequired"] = hash != startupSha256 || identified && product != ReleaseInfo.Id;
            disk["status"] = (bool)disk["restartRequired"] ? "restart_required" : identified ? "matches" : "release_identity_unavailable";
            disk["hashScope"] = "Current disk bytes compared with disk bytes observed at startup; releaseId is compiled into the running DLL.";
        }
        catch (Exception error) { disk["status"] = "unavailable"; disk["error"] = error.Message; }
        report["disk"] = disk;
        return report;
    }
    public void Remove()
    {
        WithRegistryLock(() => {
            RemoveOwned(InstanceFile);
            RemoveOwned(Path.Combine(root, "connection.json"));
        });
    }
    private void WithRegistryLock(Action action)
    {
        System.IO.Directory.CreateDirectory(root);
        FileStream lease = null;
        for (int attempt = 0; lease == null; attempt++)
        {
            try { lease = new FileStream(Path.Combine(root, "registry.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) when (attempt < 20) { System.Threading.Thread.Sleep(10); }
        }
        using (lease) action();
    }
    private void RemoveOwned(string path)
    {
        if (!File.Exists(path)) return;
        if ((string)JObject.Parse(File.ReadAllText(path))["instanceId"] == instance) File.Delete(path);
    }
    public static void Failure(string root, Exception error)
    {
        AtomicWrite(Path.Combine(root, "failures", Guid.NewGuid().ToString("N") + ".json"), new {
            observedAtUtc = DateTime.UtcNow.ToString("O"),
            releaseId = ReleaseInfo.Id, code = (error as BridgeFailure)?.Code ?? "bridge_startup_failed",
            error = BridgeFailure.Describe(error, "bridge.start"), details = (error as BridgeFailure)?.PartialResult,
            exceptionType = error.GetType().FullName,
            message = error.Message, hresult = error.HResult,
            next = "Check game Player.log, directory permissions, requested port and MOD enable/capability approval. Failure files are historical, not current liveness."
        });
    }
}
