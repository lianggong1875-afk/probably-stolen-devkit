using System;
using PortraitStudio.StaticV1;
using UnityEngine;

// This example owns one isolated static surface. Keep the author's other behavior intact.
public sealed class AdapterLifecycle : IDisposable
{
    readonly StaticBridge bridge;
    readonly StaticDisplay display;
    readonly SpriteRenderer renderer;
    readonly Func<Sprite> authorDefault;
    bool mayWrite = true;
    // Construct on the first frame or a later object-creation event, after OnEnable returns.
    public AdapterLifecycle(string modId, string targetId, SpriteRenderer ownedRenderer, Sprite immutableOriginal, Func<Sprite> currentStaticDefault, string visitId, string creatorSurfaceId)
    {
        renderer = ownedRenderer; authorDefault = currentStaticDefault;
        bridge = new StaticBridge(modId);
        try
        {
            display = bridge.RegisterDisplay(targetId, "author-display", creatorSurfaceId,
            () => renderer.sprite, authorDefault, decision =>
            {
                mayWrite = decision.shouldWrite;
                if (mayWrite && renderer != null && renderer.sprite != decision.sprite) renderer.sprite = decision.sprite;
            });
            display.Show(visitId, immutableOriginal);
            bridge.Announce();
        }
        catch { bridge.Dispose(); throw; }
    }
    // Call at ALL final-write paths, including overlays. Silent must skip every surface write.
    public void Refresh()
    {
        var decision = display.Resolve();
        mayWrite = decision.shouldWrite;
        if (mayWrite && renderer != null && renderer.sprite != decision.sprite) renderer.sprite = decision.sprite;
    }
    public void Show(string visitId, Sprite immutableOriginal) { display.Show(visitId, immutableOriginal); }
    public void Hide() { display.Hide(); }
    public void SourceChanged() { display.SourceChanged(); }
    public void Tick() { bridge.Tick(); }
    public void Dispose()
    {
        try { display.Hide(); }
        finally { try { bridge.UnregisterDisplay(display); } finally { bridge.Dispose(); } }
    }
}
