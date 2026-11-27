using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PortraitStudio.StaticV1
{
    // Fixed game fields through the official accessor. Never read another mod's assembly.
    internal static class NativeOriginals
    {
        static SpriteDict owner;
        static bool invalidated;
        static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        static readonly HashSet<Sprite> trusted = new HashSet<Sprite>();
        static readonly string[] fields = { "spriteEntries", "scavMaleEntries", "scavFemaleEntries", "wantedEntries", "secMaleEntries", "secFemaleEntries", "medMaleEntries", "medFemaleEntries", "engMaleEntries", "engFemaleEntries", "secCorpoMaleEntries", "secCorpoFemaleEntries", "lowerMaleEntries", "lowerFemaleEntries", "revMaleEntries", "revFemaleEntries", "bmMaleEntries", "bmFemaleEntries", "upperMaleEntries", "upperFemaleEntries", "chefMaleEntries", "chefFemaleEntries", "farmerMaleEntries", "farmerFemaleEntries", "mercMaleEntries", "mercFemaleEntries", "sciMaleEntries", "sciFemaleEntries", "secOffMaleEntries", "secOffFemaleEntries", "specialEntries", "augLowerMaleEntries", "augLowerFemaleEntries", "augBmMaleEntries", "augBmFemaleEntries", "augUpperMaleEntries", "augUpperFemaleEntries" };
        public static Sprite Get(string key)
        {
            if (invalidated || !ReferenceEquals(owner, SpriteDict.Instance))
            {
                invalidated = false; owner = SpriteDict.Instance; sprites.Clear(); trusted.Clear();
                if (owner != null) foreach (var field in fields)
                {
                    var list = ModReflect.Get<List<SpriteDict.SpriteEntry>>(owner, field);
                    if (list != null) foreach (var e in list) if (e != null && !string.IsNullOrEmpty(e.key) && e.sprite != null && !sprites.ContainsKey(e.key)) { sprites.Add(e.key, e.sprite); trusted.Add(e.sprite); }
                }
            }
            return key != null && sprites.TryGetValue(key, out var s) ? s : null;
        }
        public static bool IsTrusted(Sprite sprite) { Get(null); return sprite != null && trusted.Contains(sprite); }
        public static void Invalidate() { invalidated = true; }
    }
    internal static class NativeSnapshot
    {
        static int frame = -1, users;
        static SpriteRenderer[] renderers;
        static Image[] images;
        static WantedElement[] wanted;
        static Sprite[] sprites;
        static void CheckFrame() { if (frame != Time.frameCount) { Invalidate(); frame = Time.frameCount; } }
        public static void Retain() { users++; }
        public static void Release() { if (--users == 0) Invalidate(); }
        public static void Invalidate() { frame = -1; renderers = null; images = null; wanted = null; sprites = null; }
        public static SpriteRenderer[] Renderers() { CheckFrame(); return renderers ?? (renderers = Resources.FindObjectsOfTypeAll<SpriteRenderer>()); }
        public static Image[] Images() { CheckFrame(); return images ?? (images = Resources.FindObjectsOfTypeAll<Image>()); }
        public static WantedElement[] Wanted() { CheckFrame(); return wanted ?? (wanted = Resources.FindObjectsOfTypeAll<WantedElement>()); }
        public static Sprite[] Sprites() { CheckFrame(); return sprites ?? (sprites = Resources.FindObjectsOfTypeAll<Sprite>()); }
    }
    public sealed class StaticBindings : IDisposable
    {
        sealed class Bound
        {
            public UnityEngine.Object owner;
            public Func<Sprite> get;
            public Action<Sprite> set;
            public Func<bool> active;
            public StaticDisplay display;
            public string target, visit, originVisit, surface;
            public Sprite original;
            public bool shown, native;
        }
        readonly StaticBridge bridge;
        readonly Dictionary<UnityEngine.Object, Bound> owners = new Dictionary<UnityEngine.Object, Bound>();
        readonly Dictionary<string, Target> characters = new Dictionary<string, Target>(StringComparer.Ordinal);
        readonly Dictionary<string, Target> wanted = new Dictionary<string, Target>(StringComparer.Ordinal);
        readonly Dictionary<string, Target> scenes = new Dictionary<string, Target>(StringComparer.Ordinal);
        readonly Dictionary<Sprite, Target> byOriginal = new Dictionary<Sprite, Target>();
        readonly Dictionary<string, Sprite> sceneOriginals = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        readonly HashSet<string> ambiguousScenes = new HashSet<string>(StringComparer.Ordinal);
        readonly List<UnityEngine.Object> dead = new List<UnityEngine.Object>();
        StoreClientInstance client;
        StoreClient clientBlueprint;
        SpriteRenderer clientRenderer;
        string clientKey, clientVisit, clientIdentifier, clientSource, clientImage;
        bool clientAug;
        int batch;
        bool disposed;
        // Only known visible surfaces that still show this writer's replacement.
        // Used by the standalone template, not by adapter or manager presentation.
        public bool HasVisibleReplacement
        {
            get
            {
                if (disposed) return false;
                foreach (var b in owners.Values)
                {
                    if (b.owner == null || !b.shown || !b.active()) continue;
                    var decision = b.display.Resolve();
                    if (decision.shouldWrite && decision.sprite != null && decision.sprite != b.original && b.get() == decision.sprite) return true;
                }
                return false;
            }
        }
        public StaticBindings(StaticBridge b)
        {
            bridge = b;
            foreach (var t in bridge.Descriptor.targets)
            {
                if (t.binding.type == "sprite-key") characters.Add(t.binding.key, t);
                if (t.binding.type == "wanted-photo") wanted.Add(t.binding.key, t);
                if (t.binding.type == "scene-key") scenes.Add(t.binding.key, t);
            }
            NativeSnapshot.Retain();
            ModHook.OnGameLoadedLate += Refresh; ModHook.OnShutterOpenedLate += Refresh;
            ModHook.OnShutterClosedLate += Refresh; ModHook.OnReturningStoreLate += Refresh;
            ModHook.OnPaperOpened += Paper; ModHook.OnAfterhourArriveLate += Arrive; ModHook.OnAfterhourLeaveLate += Arrive;
            try { Refresh(); } catch { Dispose(); throw; }
        }
        void Paper(string id, PaperUI ui) { Refresh(); }
        void Arrive(string id) { Refresh(); }
        public void RefreshNewObjects() { NativeSnapshot.Invalidate(); Refresh(); }
        public void BeginBatch() { batch++; }
        public void EndBatch() { Check.That(batch > 0, "No batch open"); if (--batch == 0) Tick(); }
        public void BindSprite(string targetId, SpriteRenderer r, Sprite original, string visitId, string originDisplayId = null, string originVisitId = null)
        {
            if (r == null) return;
            var target = bridge.Descriptor.GetTarget(targetId);
            string surface = target.binding.type == "custom" ? CustomSurface(target, originDisplayId) : "game:SpriteRenderer:" + r.GetInstanceID();
            Bind(targetId, r, () => r.sprite, s => r.sprite = s, () => r != null && r.gameObject.activeInHierarchy && r.enabled, original, visitId, surface, originVisitId);
        }
        public void BindImage(string targetId, Image image, Sprite original, string visitId, string originDisplayId = null, string originVisitId = null)
        {
            if (image == null) return;
            var target = bridge.Descriptor.GetTarget(targetId);
            string surface = target.binding.type == "custom" ? CustomSurface(target, originDisplayId) : "game:Image:" + image.GetInstanceID();
            // Image.overrideSprite returns the effective image, including the base sprite.
            bool useOverride = image.overrideSprite != image.sprite;
            Bind(targetId, image, () => image.overrideSprite, s => { if (useOverride) image.overrideSprite = s; else image.sprite = s; }, () => image != null && image.gameObject.activeInHierarchy && image.enabled, original, visitId, surface, originVisitId);
        }
        static string CustomSurface(Target target, string origin)
        {
            Check.That(Check.Id(origin) && Check.Id(target.subject.owner + ":" + origin), "Custom creator must supply stable originDisplayId"); return target.subject.owner + ":" + origin;
        }
        void Bind(string target, UnityEngine.Object owner, Func<Sprite> get, Action<Sprite> set, Func<bool> active, Sprite original, string visit, string surface, string originVisit)
        {
            Check.That(!disposed && original != null && Check.Id(visit), "Trusted original and visit required");
            if (owners.TryGetValue(owner, out var old))
            {
                if (old.target == target && old.visit == visit && old.original == original && old.originVisit == originVisit) return;
                Remove(old, "rebound");
            }
            var b = new Bound { owner = owner, target = target, get = get, set = set, active = active, original = original, visit = visit, surface = surface, originVisit = originVisit, native = bridge.Descriptor.GetTarget(target).binding.type != "custom" };
            b.display = bridge.RegisterDisplay(target, "object/" + owner.GetInstanceID(), surface, get, () => original, decision => { if (decision.shouldWrite && owner != null && get() != decision.sprite) set(decision.sprite); });
            owners[owner] = b;
            if (batch == 0) Update(b);
        }
        public void Unbind(UnityEngine.Object owner) { if (owners.TryGetValue(owner, out var b)) Remove(b, "hidden"); }
        void Remove(Bound b, string reason)
        {
            try { b.display.Hide(reason); }
            finally { try { bridge.UnregisterDisplay(b.display); } finally { owners.Remove(b.owner); } }
        }
        void Update(Bound b)
        {
            bool active = b.owner != null && b.active();
            if (active && !b.shown) { b.display.Show(b.visit, b.original, b.originVisit); b.shown = true; }
            else if (!active && b.shown) { b.display.Hide("hidden"); b.shown = false; }
            else if (active && b.shown && b.get() == b.original)
            {
                // The game may reset a known surface after an event. Repair only its
                // trusted original; never fight a later third-party or silent writer.
                var decision = b.display.Resolve();
                if (decision.shouldWrite && decision.sprite != b.original) b.set(decision.sprite);
            }
        }
        void CurrentClient()
        {
            if (characters.Count == 0) return;
            var r = StoreClientMono.Instance == null ? null : StoreClientMono.Instance.image;
            // Instance is a lazy constructor. Menu startup must never create a store.
            var next = PlayerStore.HasInstance ? PlayerStore.Instance.currentClientInstance : null;
            var blueprint = next == null ? null : next.GetClientBlueprint();
            if (r != clientRenderer)
            {
                if (clientRenderer != null && owners.TryGetValue(clientRenderer, out var old) && old.native) Remove(old, "destroyed");
                clientRenderer = r; client = null; clientBlueprint = null; clientVisit = clientKey = null;
            }
            // The game clears currentClientInstance before the leaving renderer disappears.
            bool newVisit = next != null && (!ReferenceEquals(next, client) || !ReferenceEquals(blueprint, clientBlueprint)
                || blueprint?.identifier != clientIdentifier || blueprint?.sourceModId != clientSource || (blueprint != null && blueprint.isAug) != clientAug);
            if (newVisit)
            {
                client = next; clientBlueprint = blueprint;
                clientIdentifier = blueprint?.identifier; clientSource = blueprint?.sourceModId; clientAug = blueprint != null && blueprint.isAug;
                clientKey = clientImage = null; clientVisit = Guid.NewGuid().ToString("N");
                if (r != null && owners.TryGetValue(r, out var old) && old.native) Remove(old, "rebound");
            }
            if (next != null && r != null)
            {
                // spriteName is the game's already chosen body, never a new random draw.
                string imageKey = blueprint?.spriteName;
                string roleKey = blueprint == null ? null : NativeClientRoles.SelectionKey(blueprint.identifier, blueprint.sourceModId, blueprint.isAug);
                Target target = null;
                if (blueprint != null && string.IsNullOrEmpty(blueprint.sourceModId))
                {
                    if (roleKey != null) characters.TryGetValue(roleKey, out target);
                    if (target == null && imageKey != null) characters.TryGetValue(imageKey, out target);
                }
                string key = target?.binding.key;
                var original = target == null ? null : NativeOriginals.Get(imageKey);
                owners.TryGetValue(r, out var bound);
                // Some game customers supply a non-resource spriteName (e.g. lotteryRep).
                // Accept only the actual serialized fallback, never a preview or a mod image.
                if (original == null && target != null && roleKey != null)
                {
                    if (NativeOriginals.IsTrusted(r.sprite)) original = r.sprite;
                    else if (clientKey == key && clientImage == imageKey && bound != null && NativeOriginals.IsTrusted(bound.original)) original = bound.original;
                }
                if (bound != null && bound.native && (clientKey != key || bound.original != original)) { Remove(bound, "rebound"); bound = null; }
                clientKey = key; clientImage = imageKey;
                // Custom surfaces are registered by their creator; automatic native binding cannot own them.
                if (target != null && original != null && bound == null) BindSprite(target.id, r, original, clientVisit);
            }
            if (next == null && (r == null || !r.gameObject.activeInHierarchy || !r.enabled)) { client = null; clientBlueprint = null; clientKey = clientVisit = null; }
        }
        // Poll only known objects and the one shared customer renderer. No filesystem or scene search.
        public void Tick()
        {
            if (disposed || batch > 0) return;
            CurrentClient(); dead.Clear();
            foreach (var pair in owners)
            {
                var b = pair.Value;
                if (pair.Key == null || b.original == null) { dead.Add(pair.Key); continue; }
                if (b.native && b.display.Resolve().shouldWrite && pair.Key != clientRenderer && b.shown && b.get() != b.original && b.get() != b.display.Resolve().sprite)
                {
                    // An unrecognized subject replaced this object; stop the previous writer immediately.
                    dead.Add(pair.Key); continue;
                }
                Update(b);
            }
            foreach (var owner in dead) Remove(owners[owner], "rebound");
        }
        public void Refresh()
        {
            if (disposed) return;
            BeginBatch();
            try
            {
                NativeOriginals.Invalidate();
                // Scene assets can unload and be recreated with the same key.
                sceneOriginals.Clear(); ambiguousScenes.Clear();
                byOriginal.Clear();
                foreach (var t in characters.Values) { if (NativeClientRoles.IsRoleKey(t.binding.key)) continue; var original = NativeOriginals.Get(t.binding.key); if (original != null && !byOriginal.ContainsKey(original)) byOriginal.Add(original, t); }
                if (scenes.Count > 0)
                    foreach (var s in NativeSnapshot.Sprites())
                        if (s != null && s.texture != null && s.name == s.texture.name && scenes.ContainsKey(s.name) && !ambiguousScenes.Contains(s.name))
                        {
                            if (sceneOriginals.TryGetValue(s.name, out var old) && old != s) { sceneOriginals.Remove(s.name); ambiguousScenes.Add(s.name); }
                            else if (old == null) sceneOriginals[s.name] = s;
                        }
                if (characters.Count > 0 || scenes.Count > 0) foreach (var r in NativeSnapshot.Renderers())
                {
                    if (r == null || !r.gameObject.scene.IsValid() || r == (StoreClientMono.Instance == null ? null : StoreClientMono.Instance.image)) continue;
                    if (owners.ContainsKey(r)) continue;
                    if (r.sprite != null && byOriginal.TryGetValue(r.sprite, out var t)) BindSprite(t.id, r, NativeOriginals.Get(t.binding.key), Guid.NewGuid().ToString("N"));
                    else foreach (var scene in scenes.Values)
                        if (scene.binding.key != "drjackson" && sceneOriginals.TryGetValue(scene.binding.key, out var original) && (r.sprite == original || scene.binding.key == "drjackson2" && r.name == "inventor_scene")) { BindSprite(scene.id, r, original, Guid.NewGuid().ToString("N")); break; }
                }
                if (wanted.Count > 0) foreach (var cell in NativeSnapshot.Wanted())
                {
                    if (cell == null || !cell.gameObject.scene.IsValid()) continue;
                    var photo = cell.transform.Find("Portait"); var image = photo == null ? null : photo.GetComponent<Image>();
                    if (image == null) continue;
                    if (wanted.TryGetValue(cell.identifier, out var t))
                    {
                        var original = NativeOriginals.Get(cell.identifier);
                        if (original != null) BindImage(t.id, image, original, "wanted/" + cell.identifier + "/" + cell.GetInstanceID());
                    }
                    else if (owners.TryGetValue(image, out var old)) Remove(old, "rebound");
                }
                if (scenes.TryGetValue("drjackson", out var travel) && sceneOriginals.TryGetValue("drjackson", out var card))
                    foreach (var image in NativeSnapshot.Images())
                        if (image != null && image.gameObject.scene.IsValid() && image.name == "Picture" && image.transform.parent != null && image.transform.parent.name == "Image" && image.transform.parent.parent != null && image.transform.parent.parent.name == "Card_dr_jackson") BindImage(travel.id, image, card, "travel/" + image.GetInstanceID());
            }
            finally { EndBatch(); }
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            ModHook.OnGameLoadedLate -= Refresh; ModHook.OnShutterOpenedLate -= Refresh; ModHook.OnShutterClosedLate -= Refresh;
            ModHook.OnReturningStoreLate -= Refresh; ModHook.OnPaperOpened -= Paper; ModHook.OnAfterhourArriveLate -= Arrive; ModHook.OnAfterhourLeaveLate -= Arrive;
            var errors = new List<Exception>();
            foreach (var b in new List<Bound>(owners.Values))
                try { Remove(b, b.owner == null ? "destroyed" : "hidden"); } catch (Exception error) { errors.Add(error); }
            owners.Clear(); sceneOriginals.Clear(); NativeSnapshot.Release();
            if (errors.Count > 0) throw new AggregateException("Static binding cleanup failed", errors);
        }
    }
}
