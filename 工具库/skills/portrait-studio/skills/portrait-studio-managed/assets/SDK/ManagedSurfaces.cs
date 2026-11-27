using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;
using PortraitStudio.StaticV1;

namespace PortraitStudio.Managed
{
    // Custom creator registration only. This class never assigns a sprite or changes transforms.
    public sealed class ManagedSurfaces : IDisposable
    {
        public const string ServiceKey = "portrait-studio-managed-surfaces/1", Channel = "static-managed";
        sealed class Row { public string id, target, visit, origin; public long epoch; public UnityEngine.Object surface; public Sprite original; public object[] value; }
        readonly ManagedCatalog catalog;
        readonly Dictionary<string, Row> rows = new Dictionary<string, Row>(StringComparer.Ordinal);
        readonly Dictionary<string, long> epochs = new Dictionary<string, long>(StringComparer.Ordinal);
        readonly Func<object[]> service;
        readonly string instance = Guid.NewGuid().ToString("N");
        long generation;
        object[] snapshot;
        bool disposed, notifying;
        public ManagedSurfaces(string providerId) : this(ManagedCatalog.Read(ModHelper.GetModFolder(providerId), providerId)) { }
        public ManagedSurfaces(ManagedCatalog declaration)
        {
            catalog = declaration ?? throw new ArgumentNullException(nameof(declaration));
            Check.That(ModHelper.Lookup<Func<object[]>>(catalog.ProviderId, ServiceKey) == null, "Managed registry already published");
            service = () => snapshot;
            Rebuild(); ModHelper.Expose(catalog.ProviderId, ServiceKey, service); Notify("changed", null, null);
        }
        JObject Header()
        {
            return new JObject { ["protocol"] = ManagedCatalog.Format, ["contract"] = ManagedCatalog.Contract, ["providerId"] = catalog.ProviderId,
                ["revision"] = catalog.Revision, ["instance"] = instance, ["renderMode"] = "static", ["generation"] = generation, ["enabled"] = !disposed };
        }
        void Rebuild()
        {
            var next = new object[rows.Count + 1]; next[0] = Header().ToString(Formatting.None); int i = 1;
            foreach (var row in rows.Values) next[i++] = row.value;
            snapshot = next;
        }
        public void Show(string targetId, string displayId, string visitId, SpriteRenderer surface, Sprite immutableOriginal, string originVisitId = null)
        { ShowCore(targetId, displayId, visitId, surface, immutableOriginal, originVisitId, "SpriteRenderer"); }
        public void Show(string targetId, string displayId, string visitId, Image surface, Sprite immutableOriginal, string originVisitId = null)
        { ShowCore(targetId, displayId, visitId, surface, immutableOriginal, originVisitId, "Image"); }
        void ShowCore(string targetId, string displayId, string visitId, UnityEngine.Object surface, Sprite original, string origin, string type)
        {
            Check.That(!disposed && !notifying && Check.Id(displayId) && Check.Id(visitId) && (origin == null || Check.Id(origin)) && surface != null && original != null, "Valid live managed surface/original/visit required");
            var target = catalog.GetTarget(targetId);
            Check.That(target.binding.type == "custom" && target.subject.owner == catalog.ProviderId, "Only the custom object's creator registers its surfaces");
            string surfaceId = catalog.ProviderId + ":" + displayId; Check.That(Check.Id(surfaceId), "Managed surface ID too long");
            Check.That(!rows.Values.Any(r => r.id != displayId && r.surface == surface), "Surface already registered under another ID");
            if (rows.TryGetValue(displayId, out var old))
            {
                if (old.target == targetId && old.visit == visitId && old.surface == surface && old.original == original && old.origin == origin) return;
                Hide(displayId, "rebound");
            }
            Check.That(rows.Count < 4096, "Managed surface limit reached");
            epochs.TryGetValue(displayId, out var epoch); epoch++; epochs[displayId] = epoch;
            var meta = new JObject { ["targetId"] = targetId, ["displayId"] = displayId, ["visitId"] = visitId,
                ["displayEpoch"] = epoch, ["surfaceId"] = surfaceId, ["surfaceType"] = type };
            if (origin != null) meta["originVisitId"] = origin;
            rows.Add(displayId, new Row { id = displayId, target = targetId, visit = visitId, origin = origin, epoch = epoch, surface = surface, original = original,
                value = new object[] { meta.ToString(Formatting.None), surface, original } });
            generation++; Rebuild(); Notify("changed", null, null);
        }
        public void Hide(string displayId, string reason = "hidden")
        {
            Check.That(!disposed && !notifying && (reason == "hidden" || reason == "rebound" || reason == "destroyed"), "Invalid hide reason/lifecycle");
            if (!rows.TryGetValue(displayId, out var row)) return;
            rows.Remove(displayId); generation++; Rebuild(); Notify("hide", row, reason);
        }
        void Notify(string op, Row row, string reason)
        {
            var message = Header(); message["op"] = op;
            if (row != null)
            {
                message["displayId"] = row.id; message["targetId"] = row.target; message["visitId"] = row.visit;
                message["displayEpoch"] = row.epoch; message["reason"] = reason;
            }
            notifying = true;
            try { ModHelper.SendMessage(catalog.ProviderId, Channel, message.ToString(Formatting.None)); }
            finally { notifying = false; }
        }
        public void Dispose()
        {
            if (disposed) return;
            Check.That(!notifying, "Do not mutate a managed registry from its notification callback");
            // A hidden notification permits the manager's synchronous final restore before reuse.
            foreach (var id in rows.Keys.ToArray()) Hide(id, "hidden");
            disposed = true; generation++; Rebuild(); Notify("changed", null, null);
            if (ReferenceEquals(ModHelper.Lookup<Func<object[]>>(catalog.ProviderId, ServiceKey), service)) ModHelper.Expose(catalog.ProviderId, ServiceKey, null);
        }
    }
}
