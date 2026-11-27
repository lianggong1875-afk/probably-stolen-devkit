using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json;
using UnityEngine;

namespace PortraitStudio.StaticV1
{
    public sealed class ResourceStats { public int CatalogReads, ImageDecodes, CacheHits; }
    public sealed class MemoryReport { public long residentBytes, pinnedBytes, warmBytes, pendingDestroyBytes, borrowedBytes, decodeReservedBytes; }
    public sealed class TextureCost { public string textureKey; public long residentBytes; public bool alreadyResident; }
    public sealed class Budget { public long residentDeltaBytes, peakAdditionalBytes; public TextureCost[] textures = Array.Empty<TextureCost>(); }
    public sealed class Grant { [JsonRequired] public long residentDeltaBytes, peakAdditionalBytes; }
    public sealed class HeldResource { public ResourceRef resource; public int refCount; }

    // Main-thread pool. Texture and Sprite lifetimes are separate; layout never duplicates pixels.
    internal sealed class StaticResources : IDisposable
    {
        sealed class CatalogEntry { public Catalog catalog; public string root; public long length, ticks, age; }
        internal sealed class TextureEntry
        {
            public Texture2D texture;
            public string key;
            public long bytes, age;
            public int pins;
            public readonly Dictionary<string, ResourceRef> refs = new Dictionary<string, ResourceRef>(StringComparer.Ordinal);
        }
        sealed class Dying { public long bytes; public int frame; public ResourceRef resource; }
        internal sealed class Plan
        {
            public ResourceRef reference;
            public Asset asset;
            public Sprite borrowed;
            public Layout layout;
            public string key, root, fingerprint;
            public bool photo;
            public Budget budget;
        }
        internal sealed class Lease : IDisposable
        {
            readonly StaticResources pool;
            internal readonly TextureEntry entry;
            readonly bool ownsSprite;
            public readonly Sprite Sprite;
            bool released;
            internal Lease(StaticResources pool, TextureEntry entry, Sprite sprite, bool own) { this.pool = pool; this.entry = entry; Sprite = sprite; ownsSprite = own; }
            public void Dispose()
            {
                if (released) return; released = true;
                if (ownsSprite && Sprite != null) UnityEngine.Object.Destroy(Sprite);
                if (entry != null) { entry.pins--; entry.age = ++pool.clock; pool.Trim(pool.warmLimit); }
            }
        }
        readonly Dictionary<string, CatalogEntry> catalogs = new Dictionary<string, CatalogEntry>(StringComparer.Ordinal);
        readonly Dictionary<string, TextureEntry> textures = new Dictionary<string, TextureEntry>(StringComparer.Ordinal);
        readonly Dictionary<string, string> immutable = new Dictionary<string, string>(StringComparer.Ordinal);
        readonly List<Dying> dying = new List<Dying>();
        sealed class FileStamp { public long length, ticks, age; public string hash; }
        readonly Dictionary<string, FileStamp> inspected = new Dictionary<string, FileStamp>(StringComparer.Ordinal);
        static readonly uint[] crcTable = MakeCrcTable();
        static uint[] MakeCrcTable()
        {
            var table = new uint[256];
            for (uint i = 0; i < 256; i++) { uint n = i; for (int bit = 0; bit < 8; bit++) n = (n >> 1) ^ ((n & 1) == 0 ? 0u : 0xedb88320u); table[i] = n; }
            return table;
        }
        readonly long limit, warmLimit;
        long clock, decoding;
        bool disposed;
        public readonly ResourceStats Stats = new ResourceStats();
        public StaticResources(long limit, long warmLimit)
        {
            if (limit <= 0 || warmLimit < 0 || warmLimit > limit) throw new ArgumentOutOfRangeException(nameof(limit));
            this.limit = limit; this.warmLimit = warmLimit;
        }
        internal static string Hash(byte[] data)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(data)).Replace("-", "").ToLowerInvariant();
        }
        internal static string HashText(string s) => Hash(System.Text.Encoding.UTF8.GetBytes(s));
        public Catalog ReadCatalog(string id)
        {
            Check.That(Check.Id(id), "Invalid provider ID");
            var root = ModHelper.GetModFolder(id); var path = Catalog.SafePath(root, Catalog.FileName); var info = new FileInfo(path);
            Check.That(info.Exists && info.Length <= 1048576, "Catalog missing/oversized", "resource-missing");
            if (catalogs.TryGetValue(id, out var e) && e.root == root && e.length == info.Length && e.ticks == info.LastWriteTimeUtc.Ticks) { e.age = ++clock; return e.catalog; }
            var c = Catalog.Read(root, id); Stats.CatalogReads++;
            if (catalogs.Count >= 16 && !catalogs.ContainsKey(id)) catalogs.Remove(catalogs.OrderBy(p => p.Value.age).First().Key);
            catalogs[id] = new CatalogEntry { catalog = c, root = root, length = info.Length, ticks = info.LastWriteTimeUtc.Ticks, age = ++clock }; return c;
        }
        public Plan PlanResource(Target target, ResourceRef reference, Layout layout)
        {
            reference.Validate(); layout.Validate();
            var c = ReadCatalog(reference.providerId); var set = c.GetSet(reference.setId);
            Check.That(set.Identity == target.Identity, "Resource display identity mismatch", "identity-mismatch");
            var asset = set.GetAsset(reference.id, reference.revision);
            var fingerprint = JsonConvert.SerializeObject(asset);
            if (immutable.TryGetValue(reference.Key, out var old)) Check.That(old == fingerprint, "Resource revision mutated", "resource-changed");
            var root = ModHelper.GetModFolder(reference.providerId);
            long encoded = InspectFiles(root, asset);
            if (target.binding.type == "scene-key") Check.That((long)asset.geometry.rect.width * 9 == (long)asset.geometry.rect.height * 16, "Scene rect must be 16:9", "unsupported-geometry");
            bool photo = target.kind == "wanted" && target.slot == "photo";
            var key = "texture/" + HashText(JsonConvert.SerializeObject(new object[] { root, asset.path, asset.texture, photo, photo ? asset.geometry.rect : null }));
            bool resident = textures.TryGetValue(key, out var cached);
            long gpu = PixelBytes(asset.texture), temporaryGpu = TemporaryBytes(asset.texture);
            var costs = new List<TextureCost> { new TextureCost { textureKey = key, residentBytes = gpu, alreadyResident = resident } };
            if (!resident) costs.Add(new TextureCost { textureKey = "decode/" + HashText(key), residentBytes = temporaryGpu, alreadyResident = false });
            var plan = new Plan { reference = reference, asset = asset, layout = layout, photo = photo, root = root, key = key, fingerprint = fingerprint,
                budget = new Budget { residentDeltaBytes = resident ? 0 : checked(gpu + temporaryGpu), peakAdditionalBytes = resident ? 0 : checked(gpu * 4 + temporaryGpu * 3 + encoded), textures = costs.ToArray() } };
            immutable[reference.Key] = fingerprint; return plan;
        }
        public Plan PlanBorrowed(Sprite sprite, Layout layout)
        {
            Check.That(sprite != null && sprite.texture != null && sprite.rect.width > 0 && sprite.rect.height > 0 && Check.Finite(sprite.pixelsPerUnit) && sprite.pixelsPerUnit > 0 && sprite.associatedAlphaSplitTexture == null, "Missing/unsupported source Sprite", "unsupported-geometry");
            // Caller has audited FullRect, material and local side effects before advertising this target.
            layout.Validate(); return new Plan { borrowed = sprite, layout = layout, budget = new Budget() };
        }
        public Lease Prepare(Plan p, Grant grant)
        {
            Check.That(!disposed, "Pool disposed");
            Check.That(grant != null && grant.residentDeltaBytes >= p.budget.residentDeltaBytes && grant.peakAdditionalBytes >= p.budget.peakAdditionalBytes, "Reservation too small", "budget-exceeded");
            if (p.borrowed != null) return MakeLease(null, p.borrowed, p.layout, false);
            // An idle entry can disappear between plan and prepare; never allocate against a zero grant.
            if (textures.TryGetValue(p.key, out var cached))
            {
                InspectFiles(p.root, p.asset); Stats.CacheHits++; cached.refs[p.reference.Key] = p.reference;
                return MakeResourceLease(cached, p.asset.geometry, p.layout);
            }
            Check.That(p.budget.residentDeltaBytes > 0, "Planned resident texture changed", "resource-changed");
            Check.That(Memory().residentBytes <= limit - p.budget.peakAdditionalBytes, "Local memory budget exceeded", "budget-exceeded");
            InspectFiles(p.root, p.asset);
            decoding = p.budget.peakAdditionalBytes;
            Texture2D texture = null;
            try
            {
                var t = p.asset.texture;
                texture = new Texture2D(t.width, t.height, TextureFormat.RGBA32, t.mipmaps.mode != "none", t.colorSpace == "linear");
                var basePixels = Decode(p.root, p.asset.path, t.width, t.height, t.colorSpace == "linear", p.reference, t.contentHash);
                if (p.photo) Mask(basePixels, t.width, t.height, p.asset.geometry.rect, 0);
                texture.SetPixels32(basePixels, 0);
                if (t.mipmaps.mode == "provided") foreach (var m in t.mipmaps.levels)
                {
                    var pixels = Decode(p.root, m.path, m.width, m.height, t.colorSpace == "linear", p.reference, m.sha256);
                    if (p.photo) Mask(pixels, m.width, m.height, p.asset.geometry.rect, m.level);
                    texture.SetPixels32(pixels, m.level);
                }
                texture.filterMode = (FilterMode)Enum.Parse(typeof(FilterMode), t.filter);
                texture.wrapModeU = (TextureWrapMode)Enum.Parse(typeof(TextureWrapMode), t.wrapU);
                texture.wrapModeV = (TextureWrapMode)Enum.Parse(typeof(TextureWrapMode), t.wrapV);
                texture.wrapModeW = (TextureWrapMode)Enum.Parse(typeof(TextureWrapMode), t.wrapW);
                texture.anisoLevel = t.anisoLevel; texture.mipMapBias = t.mipMapBias;
                texture.Apply(t.mipmaps.mode == "generate", true);
                UnityEngine.Object.DontDestroyOnLoad(texture);
                cached = new TextureEntry { texture = texture, key = p.key, bytes = PixelBytes(t), age = ++clock };
                cached.refs.Add(p.reference.Key, p.reference); textures.Add(p.key, cached); texture = null;
                return MakeResourceLease(cached, p.asset.geometry, p.layout);
            }
            finally
            {
                decoding = 0;
                if (texture != null) DestroyTexture(texture, PixelBytes(p.asset.texture), p.reference);
            }
        }
        Color32[] Decode(string root, string path, int w, int h, bool linear, ResourceRef reference, string expectedHash)
        {
            var bytes = File.ReadAllBytes(Catalog.PngPath(root, path));
            Check.That(bytes.Length <= 33554432 && Hash(bytes) == expectedHash, "PNG changed during preparation", "resource-changed");
            var temp = new Texture2D(2, 2, TextureFormat.RGBA32, false, linear);
            try
            {
                Check.That(ImageConversion.LoadImage(temp, bytes, false) && temp.width == w && temp.height == h, "PNG decode rejected", "resource-changed");
                Stats.ImageDecodes++; return temp.GetPixels32();
            }
            finally { DestroyTexture(temp, checked((long)w * h * 4), reference); }
        }
        static void Mask(Color32[] pixels, int w, int h, PixelRect rect, int level)
        {
            // Original photo rule in the Sprite rect, propagated to each supplied mip.
            double f = Math.Pow(2, level);
            int x0 = Math.Max(0, (int)Math.Ceiling((rect.x + rect.width * 94.0 / 150) / f));
            int x1 = Math.Min(w, (int)Math.Ceiling((rect.x + rect.width) / f));
            int y0 = Math.Max(0, (int)Math.Floor((rect.y + rect.height * (1 - 30.0 / 166)) / f));
            int y1 = Math.Min(h, (int)Math.Ceiling((rect.y + rect.height) / f));
            for (int y = y0; y < y1; y++) for (int x = x0; x < x1; x++) pixels[y * w + x] = new Color32(0, 0, 0, 0);
        }
        Lease MakeResourceLease(TextureEntry entry, Geometry g, Layout layout)
        {
            var r = g.rect; var b = g.border;
            var sprite = Sprite.Create(entry.texture, new Rect(r.x, r.y, r.width, r.height), new Vector2(g.pivotX - layout.x / layout.scale, g.pivotY - layout.y / layout.scale), g.pixelsPerUnit / layout.scale, 0, SpriteMeshType.FullRect, new Vector4(b.left, b.bottom, b.right, b.top));
            UnityEngine.Object.DontDestroyOnLoad(sprite);
            entry.pins++; entry.age = ++clock;
            return new Lease(this, entry, sprite, true);
        }
        Lease MakeLease(TextureEntry entry, Sprite sprite, Layout layout, bool own)
        {
            if (!layout.IsIdentity)
            {
                var r = sprite.rect;
                sprite = Sprite.Create(sprite.texture, r, new Vector2(sprite.pivot.x / r.width - layout.x / layout.scale, sprite.pivot.y / r.height - layout.y / layout.scale), sprite.pixelsPerUnit / layout.scale, 0, SpriteMeshType.FullRect, sprite.border);
                UnityEngine.Object.DontDestroyOnLoad(sprite);
                own = true;
            }
            return new Lease(this, entry, sprite, own);
        }
        public MemoryReport Memory()
        {
            var m = new MemoryReport { decodeReservedBytes = decoding };
            foreach (var e in textures.Values) if (e.pins > 0) m.pinnedBytes += e.bytes; else m.warmBytes += e.bytes;
            foreach (var d in dying) m.pendingDestroyBytes += d.bytes;
            m.residentBytes = m.pinnedBytes + m.warmBytes + m.pendingDestroyBytes; return m;
        }
        public List<HeldResource> Held()
        {
            var refs = new Dictionary<string, HeldResource>(StringComparer.Ordinal);
            foreach (var e in textures.Values) foreach (var r in e.refs.Values) AddHeld(refs, r, Math.Max(1, e.pins));
            foreach (var d in dying) if (d.resource != null) AddHeld(refs, d.resource, 1);
            return refs.Values.ToList();
        }
        internal static void AddHeld(Dictionary<string, HeldResource> refs, ResourceRef r, int n)
        {
            if (refs.TryGetValue(r.Key, out var h)) h.refCount += n; else refs.Add(r.Key, new HeldResource { resource = r, refCount = n });
        }
        public bool Tick()
        {
            bool changed = false;
            for (int i = dying.Count - 1; i >= 0; i--) if (Time.frameCount >= dying[i].frame) { dying.RemoveAt(i); changed = true; }
            return changed;
        }
        void DestroyTexture(Texture2D t, long bytes, ResourceRef r)
        {
            UnityEngine.Object.Destroy(t); dying.Add(new Dying { bytes = bytes, frame = Time.frameCount + 2, resource = r });
        }
        public void Trim(long warmBytes)
        {
            Check.That(warmBytes >= 0, "Invalid warm limit");
            for (;;)
            {
                long warm = 0; int idle = 0; TextureEntry oldest = null;
                foreach (var t in textures.Values) if (t.pins == 0) { warm += t.bytes; idle++; if (oldest == null || t.age < oldest.age) oldest = t; }
                if (oldest == null || warm <= warmBytes && idle <= 128) break;
                textures.Remove(oldest.key);
                // Retain every revision protection until the deferred destruction grace passes.
                var refs = oldest.refs.Values.ToArray(); DestroyTexture(oldest.texture, oldest.bytes, refs.FirstOrDefault());
                foreach (var r in refs.Skip(1)) dying.Add(new Dying { frame = Time.frameCount + 2, resource = r });
            }
        }
        static long PixelBytes(TextureInfo t)
        {
            long n = 0; int w = t.width, h = t.height;
            for (;;) { n += (long)w * h * 4; if (t.mipmaps.mode == "none" || w == 1 && h == 1) return n; w = Math.Max(1, w / 2); h = Math.Max(1, h / 2); }
        }
        static long TemporaryBytes(TextureInfo t) => t.mipmaps.mode == "provided" ? PixelBytes(t) : (long)t.width * t.height * 4;
        long InspectFiles(string root, Asset a)
        {
            long bytes = Inspect(root, a.path, a.texture.width, a.texture.height, a.texture.contentHash);
            if (a.texture.mipmaps.mode == "provided") foreach (var m in a.texture.mipmaps.levels) bytes += Inspect(root, m.path, m.width, m.height, m.sha256);
            return bytes;
        }
        long Inspect(string root, string relative, int w, int h, string hash)
        {
            var path = Catalog.PngPath(root, relative); var f = new FileInfo(path);
            Check.That(f.Exists && f.Length <= 33554432, "PNG missing/oversized", "resource-missing");
            if (inspected.TryGetValue(path, out var stamp) && stamp.length == f.Length && stamp.ticks == f.LastWriteTimeUtc.Ticks && stamp.hash == hash) { stamp.age = ++clock; return f.Length; }
            var b = File.ReadAllBytes(path); Check.That(Hash(b) == hash, "PNG revision changed", "resource-changed");
            Check.That(b.Length >= 45 && b.Take(8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) && U32(b, 16) == w && U32(b, 20) == h, "PNG dimensions/header changed", "resource-changed");
            bool header = false, data = false, end = false;
            for (long p = 8; p < b.Length;)
            {
                Check.That(p + 12 <= b.Length, "Truncated PNG", "resource-changed"); int pos = (int)p; long length = U32(b, pos);
                Check.That(p + length + 12 <= b.Length, "Truncated PNG chunk", "resource-changed");
                var kind = System.Text.Encoding.ASCII.GetString(b, pos + 4, 4);
                Check.That(kind != "acTL" && kind != "fcTL" && kind != "fdAT", "APNG rejected", "resource-changed");
                uint crc = 0xffffffff;
                for (int i = pos + 4; i < pos + 8 + length; i++) crc = (crc >> 8) ^ crcTable[(crc ^ b[i]) & 255];
                Check.That((crc ^ 0xffffffff) == U32(b, (int)(pos + 8 + length)), "PNG CRC mismatch", "resource-changed");
                if (!header) { Check.That(kind == "IHDR" && length == 13, "Invalid PNG header", "resource-changed"); header = true; }
                else Check.That(kind != "IHDR", "Duplicate PNG header", "resource-changed");
                if (kind == "IDAT") data = true;
                p += length + 12;
                if (kind == "IEND") { Check.That(length == 0 && p == b.Length, "PNG trailing data", "resource-changed"); end = true; break; }
            }
            Check.That(header && data && end, "Incomplete PNG", "resource-changed");
            if (inspected.Count >= 1024 && !inspected.ContainsKey(path)) inspected.Remove(inspected.OrderBy(p => p.Value.age).First().Key);
            inspected[path] = new FileStamp { length = f.Length, ticks = f.LastWriteTimeUtc.Ticks, hash = hash, age = ++clock }; return b.Length;
        }
        static long U32(byte[] b, int p) => (long)b[p] << 24 | (long)b[p + 1] << 16 | (long)b[p + 2] << 8 | b[p + 3];
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            foreach (var e in textures.Values) DestroyTexture(e.texture, e.bytes, e.refs.Values.FirstOrDefault());
            textures.Clear(); catalogs.Clear(); immutable.Clear(); inspected.Clear();
        }
    }
}
