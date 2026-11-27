using System;
using PortraitStudio.DynamicV1;
using UnityEngine;

// Connect this class to the author's actual arrival, update and departure events.
// The two source sprites are author-owned full frames, already loaded by the mod.
public sealed class DynamicPortraitExample : IDisposable
{
    readonly DynamicBridge bridge;
    readonly DynamicDisplay display;
    readonly SpriteRenderer renderer;
    readonly Sprite frameA, frameB;
    readonly Vector3 basePosition, baseScale;
    readonly Vector2 canvasSize;
    string visit;
    public DynamicPortraitExample(string providerId, SpriteRenderer surface, Sprite original,
        Sprite a, Sprite b, Vector2 defaultCanvasWorldSize, string visitId)
    {
        renderer = surface; frameA = a; frameB = b; canvasSize = defaultCanvasWorldSize;
        basePosition = surface.transform.localPosition; baseScale = surface.transform.localScale;
        bridge = new DynamicBridge(providerId);
        try
        {
            display = bridge.RegisterDisplay("mentor-main", "counter", ApplyPolicy);
            visit = visitId; display.Show(visitId, surface, original);
        }
        catch { bridge.Dispose(); throw; }
    }
    void RestoreLayout() { if (renderer != null) { renderer.transform.localPosition = basePosition; renderer.transform.localScale = baseScale; } }
    void ApplyPolicy(DynamicPolicy policy)
    {
        // Put layer shutdown here if the original mod has mouths or overlays.
        // Do not write the main sprite in this synchronous policy callback.
        RestoreLayout();
    }
    public void Tick()
    {
        bridge.Tick();
        if (!display.ShouldWrite || renderer == null) return;
        var policy = display.Current;
        float interval = policy.CandidateId == "quick" ? .15f : .45f;
        Sprite frame = ((int)(Time.unscaledTime / interval) & 1) == 0 ? frameA : frameB;
        if (frame == null) return;
        var layout = policy.Layout;
        renderer.transform.localScale = baseScale * layout.Scale;
        renderer.transform.localPosition = basePosition + new Vector3(layout.X * canvasSize.x, layout.Y * canvasSize.y, 0);
        if (renderer.sprite != frame) renderer.sprite = frame;
    }
    public void SetStandaloneCandidate(string id) { display.SetStandaloneCandidate(id); }
    // Capture this token with asynchronous requests; compare before assigning their results.
    public string VisitToken => visit;
    public bool AcceptsAsyncResult(string token) => token == visit && display.ShouldWrite;
    public void Dispose()
    {
        visit = null;
        try { bridge.Dispose(); }
        finally { RestoreLayout(); }
        // Destroy only the textures/sprites created by the author, after disposing.
        // Shared game frames passed into this sample remain borrowed.
    }
}
