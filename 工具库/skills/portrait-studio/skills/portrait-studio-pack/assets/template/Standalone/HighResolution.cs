using System;
using UnityEngine;

// Shared scene-local leases use ordinary Unity objects, so separately compiled packs
// cooperate without reflection or depending on one another's private runtime types.
internal sealed class StandaloneHighResolution : IDisposable
{
    const string LeaseRoot = "PortraitStudio.AuthorHD.Leases";
    GameObject token;
    Transform root;
    Behaviour camera;
    bool disposed;

    public void Tick(bool requested)
    {
        if (disposed) return;
        Behaviour next = EmporiumEntry.Instance == null ? null : EmporiumEntry.Instance.pixelPerfectCamera;
        if (camera != next || !requested) Release();
        if (!requested || next == null) return;
        if (token == null)
        {
            var shared = next.transform.Find(LeaseRoot);
            // A camera already disabled by the game or another mod needs no lease.
            if (shared == null && !next.enabled) return;
            if (shared == null)
            {
                var host = new GameObject(LeaseRoot);
                shared = host.transform; shared.SetParent(next.transform, false);
            }
            root = shared; camera = next;
            token = new GameObject("portrait-pack-lease");
            token.transform.SetParent(root, false);
        }
        // The game can reapply its display settings after focus or resolution changes.
        if (camera.enabled) camera.enabled = false;
    }

    void Release()
    {
        if (token != null)
        {
            token.transform.SetParent(null, false);
            UnityEngine.Object.Destroy(token);
        }
        token = null;
        if (root != null && root.childCount == 0)
        {
            // Remove the shared name immediately; Destroy completes at frame end.
            root.gameObject.name = LeaseRoot + ".released";
            UnityEngine.Object.Destroy(root.gameObject);
            if (camera != null && !camera.enabled)
            {
                // Restore the current game setting, not an outdated saved resolution.
                if (EmporiumEntry.Instance != null && EmporiumEntry.Instance.pixelPerfectCamera == camera && EmporiumOption.Instance != null) EmporiumOption.Instance.ApplyPixelPerfectCamera();
                else camera.enabled = true;
            }
        }
        root = null; camera = null;
    }

    public void Dispose() { if (disposed) return; Release(); disposed = true; }
}
