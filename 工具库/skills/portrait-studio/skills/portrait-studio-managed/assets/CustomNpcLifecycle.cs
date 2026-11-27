using System;
using PortraitStudio.Managed;
using UnityEngine;

// Owned by the NPC creator. Keep one registry per mod, shared by all NPC instances.
// This example never loads a portrait, assigns a sprite or changes layout.
public sealed class CustomNpcLifecycle : IDisposable
{
    readonly ManagedSurfaces surfaces;
    // Construct on the first frame or a later NPC-creation event, after OnEnable returns.
    public CustomNpcLifecycle(string creatorModId) { surfaces = new ManagedSurfaces(creatorModId); }
    public void Appeared(string targetId, string displayId, string visitId, SpriteRenderer renderer, Sprite trustedOriginal)
    {
        surfaces.Show(targetId, displayId, visitId, renderer, trustedOriginal, visitId);
    }
    public void Hidden(string displayId) { surfaces.Hide(displayId); }
    public void BeforeReuse(string displayId) { surfaces.Hide(displayId, "rebound"); }
    public void BeforeDestroy(string displayId) { surfaces.Hide(displayId, "destroyed"); }
    public void Dispose() { surfaces.Dispose(); }
}
