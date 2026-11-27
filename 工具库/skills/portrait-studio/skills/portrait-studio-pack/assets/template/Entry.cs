using System;
using PortraitStudio.StaticV1;
using UnityEngine;

public sealed class PortraitPackEntry : IMod
{
    string id;
    StaticBridge bridge;
    StaticBindings bindings;
    StandaloneHighResolution highResolution;
    ConfigEntry<bool> highResolutionOption;
    StandalonePortraitSets portraitSets;
    GameObject host;
    bool startupAttempted;
    public void Init(ModManifest manifest) { id = manifest.ID; }
    public void OnEnable()
    {
        if (host != null) return;
        startupAttempted = false;
        host = new GameObject("StaticPortraitLease/" + id);
        UnityEngine.Object.DontDestroyOnLoad(host);
        host.AddComponent<StaticPortraitTick>().owner = this;
    }
    internal void Tick()
    {
        if (host == null) return;
        if (!startupAttempted)
        {
            startupAttempted = true;
            try { StartDisplay(); }
            catch (Exception error)
            {
                try { OnDisable(); }
                finally { Debug.LogError("[" + id + "] Static portrait initialization failed: " + error); }
                return;
            }
        }
        portraitSets?.Tick(); bindings?.Tick(); bridge?.Tick();
        highResolution?.Tick(highResolutionOption.Value && bindings.HasVisibleReplacement);
    }
    void StartDisplay()
    {
        // The loader registers our folder after OnEnable returns, before the first frame.
        bridge = new StaticBridge(id);
        portraitSets = new StandalonePortraitSets(bridge);
        bindings = new StaticBindings(bridge);
        highResolutionOption = ModConfig.For(id).Define("highResolution", true, "画面",
            () => "去像素化（影响场景画面）", () => "本包实际显示替换图时关闭游戏的场景像素化处理，背景也会变化，可能增加渲染开销。关闭此项保留游戏和其他模组的画面设置。管理器协作按实际显示决策释放控制。");
        highResolution = new StandaloneHighResolution();
        bridge.Announce();
    }
    public void OnDisable()
    {
        if (host != null)
        {
            host.GetComponent<StaticPortraitTick>().owner = null;
            UnityEngine.Object.Destroy(host); host = null;
        }
        var oldBindings = bindings; var oldBridge = bridge;
        bindings = null; bridge = null;
        portraitSets?.Dispose(); portraitSets = null;
        try { highResolution?.Dispose(); }
        finally
        {
            highResolution = null;
            try { oldBindings?.Dispose(); }
            finally { oldBridge?.Dispose(); }
        }
    }
}
public sealed class StaticPortraitTick : MonoBehaviour
{
    internal PortraitPackEntry owner;
    void LateUpdate() { owner?.Tick(); }
}
