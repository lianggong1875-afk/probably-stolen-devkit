using System;
using System.Linq;
using PortraitStudio.StaticV1;
using UnityEngine;

// Settings belong to the standalone template, not the protocol reader or manager.
public sealed class StandalonePortraitSets : IDisposable
{
    readonly StaticBridge bridge;
    readonly string provider;
    readonly PortraitSet[] sets;
    readonly ConfigEntry<string> option;
    string pending;
    public StandalonePortraitSets(StaticBridge owner)
    {
        bridge = owner; provider = owner.Descriptor.providerId;
        sets = owner.Descriptor.portraitSets ?? Array.Empty<PortraitSet>();
        if (sets.Length == 0) return;
        option = ModConfig.For(provider).Define("portraitSet", sets[0].id, "立绘", () => "立绘套装",
            tooltip: () => "独立使用时切换本包套装，缺图保留原选择。管理器协作期间请使用管理器的套装选择；此处设置在协作结束后应用。");
        if (!sets.Any(s => s.id == option.Value)) option.Set(sets[0].id);
        // Store stable IDs; display names may change without invalidating the saved selection.
        ModHelper.AddGameOptionSection(provider, "portraits", () => "立绘");
        ModHelper.AddGameOptionDropdown(provider, "portraits", "portraitSet", () => "立绘套装",
            () => sets.Select(s => s.name).ToList(), () => Array.FindIndex(sets, s => s.id == option.Value),
            i => { if (i >= 0 && i < sets.Length) option.Set(sets[i].id); }, option.Tooltip);
        option.OnChanged += Changed;
        bridge.SetPortraitSet(option.Value);
    }
    void Changed(string id) { pending = id; }
    public void Tick()
    {
        if (pending == null) return;
        var id = pending; pending = null;
        try { bridge.SetPortraitSet(id); }
        catch (Exception error) { Debug.LogError("[" + provider + "] 套装切换失败，已保留当前图片：" + error); }
    }
    public void Dispose()
    {
        if (option == null) return;
        option.OnChanged -= Changed; pending = null;
        ModHelper.RemoveGameOption(provider, "portraitSet");
    }
}
