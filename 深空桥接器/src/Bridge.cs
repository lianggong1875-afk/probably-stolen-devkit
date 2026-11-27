using System;
using System.IO;
using System.Reflection;
using Newtonsoft.Json;
using UnityEngine;

namespace ModDevBridge;

public interface IDevModule
{
    string Start();
    string Execute(string command);
    void Stop();
}

public interface IStructuredDevModule : IDevModule
{
    object ExecuteStructured(string command);
}

public static class BridgePaths
{
    public static string Root => Path.GetFullPath(Environment.GetEnvironmentVariable("MODDEVBRIDGE_NEXT_ROOT") ??
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ModDevBridgeNext"));
    public static string Modules => Path.Combine(Root, "modules");
    public static string Runtime => Path.Combine(Root, ".runtime");
}

public sealed class Core : IMod
{
    private GameObject host;
    private string modId;
    public void Init(ModManifest manifest) { modId = manifest.ID; }
    public void OnEnable()
    {
        if (host != null) return;
        host = new GameObject("ModDevBridge");
        UnityEngine.Object.DontDestroyOnLoad(host);
        host.AddComponent<BridgeRunner>().ModId = modId;
    }
    public void OnDisable()
    {
        if (host != null) { host.GetComponent<BridgeRunner>()?.Shutdown(); UnityEngine.Object.Destroy(host); }
        host = null;
    }
}

public sealed class BridgeRunner : MonoBehaviour
{
    internal string ModId;
    private HttpTransport transport;
    private OperationQueue queue;
    private BridgeRuntime runtime;
    private Distribution distribution;
    private void Start()
    {
        try
        {
            System.IO.Directory.CreateDirectory(BridgePaths.Modules); System.IO.Directory.CreateDirectory(BridgePaths.Runtime);
            runtime = new BridgeRuntime();
            Application.logMessageReceived += runtime.Logs.Capture;
            queue = new OperationQueue(runtime.Execute, () => runtime.WorldId);
            runtime.Queue = queue;
            string setting = Environment.GetEnvironmentVariable("MODDEVBRIDGE_NEXT_PORT");
            int port = 18774;
            if (!string.IsNullOrEmpty(setting) && !int.TryParse(setting, out port)) throw new ArgumentException("MODDEVBRIDGE_NEXT_PORT must be an integer from 1 to 65535");
            transport = new HttpTransport(queue, port, work => ModHelper.RunInBackground(ModId, work));
            transport.Start();
            distribution = new Distribution(BridgePaths.Runtime, transport.InstanceId, transport.Url);
            transport.DistributionStatus = distribution.Status;
            distribution.Publish();
            ModHook.OnGameLoadedLate += Loaded;
            Debug.Log("[ModDevBridge] HTTP ready: " + transport.Url);
        }
        catch (Exception e) {
            Debug.LogError("[ModDevBridge] Startup failed: " + e);
            try { Distribution.Failure(BridgePaths.Runtime, e); } catch (Exception reportError) { Debug.LogWarning("[ModDevBridge] Cannot write startup diagnosis: " + reportError.Message); }
            Shutdown(); enabled = false;
        }
    }
    private void Loaded() => runtime?.MarkLoaded();
    private void Update() { runtime?.ObserveWorld(); queue?.Tick(); }
    public void Shutdown()
    {
        ModHook.OnGameLoadedLate -= Loaded;
        if (runtime != null) Application.logMessageReceived -= runtime.Logs.Capture;
        transport?.Dispose();
        queue?.Shutdown();
        try { runtime?.Shutdown(); } catch (Exception e) { Debug.LogError("[ModDevBridge] Cleanup: " + e); }
        try { distribution?.Remove(); } catch (Exception e) { Debug.LogWarning("[ModDevBridge] Descriptor cleanup: " + e.Message); }
        distribution = null;
        transport = null; queue = null; runtime = null;
    }
    private void OnDestroy() => Shutdown();
}
