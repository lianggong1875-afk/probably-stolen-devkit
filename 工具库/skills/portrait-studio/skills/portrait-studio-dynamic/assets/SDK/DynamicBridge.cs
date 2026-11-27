using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace PortraitStudio.DynamicV1
{
    public sealed class DynamicDisplay
    {
        internal readonly DynamicBridge Owner;
        internal readonly Action<DynamicPolicy> Apply;
        internal UnityEngine.Object Surface;
        internal Sprite Original;
        internal UnityEngine.Object[] Layers;
        internal string Visit, Origin, Claim, Session, LastRequest, LastReply, LocalCandidate;
        internal long Epoch, Sequence;
        internal double Deadline, RetryAt;
        internal bool Active, Fault, Transitioning;
        internal DynamicPolicy Policy;
        public readonly string TargetId, DisplayId;
        internal DynamicDisplay(DynamicBridge owner, string target, string display, Action<DynamicPolicy> apply)
        { Owner = owner; TargetId = target; DisplayId = display; Apply = apply; LocalCandidate = owner.Catalog.Target(target).DefaultCandidate; Policy = new DynamicPolicy("source", LocalCandidate, DynamicLayout.Default, false); }
        // Query before animation work and at every final write, including layers.
        public bool ShouldWrite { get { Owner.Expire(this); return Active && Surface != null && !Fault && !Transitioning && Policy.Mode == "source"; } }
        public DynamicPolicy Current => Policy;
        public void Show(string visitId, UnityEngine.Object surface, Sprite immutableOriginal, UnityEngine.Object[] layers = null, string originVisitId = null)
        { Owner.Show(this, visitId, surface, immutableOriginal, layers, originVisitId); }
        public void Hide(string reason = "hidden") { Owner.Hide(this, reason); }
        public void SetStandaloneCandidate(string candidateId) { Owner.LocalChoice(this, candidateId); }
    }
    // This bridge does not generate frames, copy textures, assign sprites, or change transforms.
    public sealed class DynamicBridge : IDisposable
    {
        public const string Manager = "PortraitStudio", Channel = "dynamic-v1";
        public const string ActiveService = "portrait-studio-dynamic-active/1", SurfaceService = "portrait-studio-dynamic-surface/1";
        public const int LeaseSeconds = 4;
        readonly Dictionary<string, DynamicDisplay> displays = new Dictionary<string, DynamicDisplay>(StringComparer.Ordinal);
        readonly Func<double> now;
        readonly Func<string> activeService;
        readonly Func<string, object[]> surfaceService;
        readonly string instance = Guid.NewGuid().ToString("N");
        long generation;
        string snapshot;
        bool dirty = true, disposed, busy;
        public readonly DynamicCatalog Catalog;
        public string Instance => instance;
        public DynamicBridge(string providerId) : this(DynamicCatalog.Read(ModHelper.GetModFolder(providerId), providerId)) { }
        public DynamicBridge(DynamicCatalog catalog, Func<double> clock = null)
        {
            Catalog = catalog ?? throw new ArgumentNullException(nameof(catalog)); now = clock ?? (() => Time.realtimeSinceStartup);
            DynamicCheck.Require(ModHelper.Lookup<Func<string>>(Catalog.ProviderId, ActiveService) == null
                && ModHelper.Lookup<Func<string, object[]>>(Catalog.ProviderId, SurfaceService) == null, "Dynamic bridge already published");
            activeService = Snapshot; surfaceService = Surface;
            ModHelper.OnMessage(Catalog.ProviderId, Manager + ":" + Channel, Receive);
            try { ModHelper.Expose(Catalog.ProviderId, ActiveService, activeService); ModHelper.Expose(Catalog.ProviderId, SurfaceService, surfaceService); }
            catch
            {
                ModHelper.OffMessage(Catalog.ProviderId, Manager + ":" + Channel, Receive);
                if (ReferenceEquals(ModHelper.Lookup<Func<string>>(Catalog.ProviderId, ActiveService), activeService)) ModHelper.Expose(Catalog.ProviderId, ActiveService, null);
                throw;
            }
        }
        void Mutable() { DynamicCheck.Require(!disposed && !busy, "Bridge disposed or reentrant mutation"); }
        public DynamicDisplay RegisterDisplay(string targetId, string displayId, Action<DynamicPolicy> apply)
        {
            Mutable(); Catalog.Target(targetId); DynamicCheck.Id(new JObject { ["id"] = displayId }, "id");
            DynamicCheck.Require(apply != null && displays.Count < 32 && !displays.ContainsKey(displayId), "Invalid/duplicate display; maximum 32");
            var d = new DynamicDisplay(this, targetId, displayId, apply); displays.Add(displayId, d); return d;
        }
        void Own(DynamicDisplay d) { DynamicCheck.Require(d != null && d.Owner == this && displays.TryGetValue(d.DisplayId, out var found) && found == d, "Foreign/unregistered display"); }
        static bool ValidSurface(UnityEngine.Object value) => value is SpriteRenderer || value is Image;
        internal void Show(DynamicDisplay d, string visit, UnityEngine.Object surface, Sprite original, UnityEngine.Object[] layers, string origin)
        {
            Mutable(); Own(d); DynamicCheck.Id(new JObject { ["id"] = visit }, "id");
            if (origin != null) DynamicCheck.Id(new JObject { ["id"] = origin }, "id");
            var group = layers == null ? Array.Empty<UnityEngine.Object>() : (UnityEngine.Object[])layers.Clone();
            DynamicCheck.Require(surface != null && ValidSurface(surface) && original != null && group.Length <= 16
                && group.All(v => v != null && ValidSurface(v)) && !group.Contains(surface) && group.Distinct().Count() == group.Length, "Invalid surface/original/layers");
            foreach (var other in displays.Values.Where(v => v.Active && v != d))
                DynamicCheck.Require(other.Surface != surface && !other.Layers.Contains(surface) && group.All(v => v != other.Surface && !other.Layers.Contains(v)), "Surface belongs to another display");
            if (d.Active && d.Visit == visit && d.Origin == origin && d.Surface == surface && d.Original == original && d.Layers.SequenceEqual(group)) return;
            if (d.Active) Hide(d, "rebound");
            d.Surface = surface; d.Original = original; d.Layers = group; d.Visit = visit; d.Origin = origin;
            d.Epoch++; d.Claim = Guid.NewGuid().ToString("N"); d.Active = true;
            Reset(d); Changed("show", d);
        }
        internal void Hide(DynamicDisplay d, string reason)
        {
            Mutable(); Own(d); DynamicCheck.Require(new[] { "hidden", "rebound", "destroyed" }.Contains(reason), "Invalid hide reason");
            if (!d.Active) return;
            d.Active = false; d.Transitioning = true;
            try { Invoke(d, new DynamicPolicy("silent", d.Policy.CandidateId, DynamicLayout.Default, false)); }
            finally
            {
                Changed("hide", d, reason); d.Surface = null; d.Original = null; d.Layers = null;
                d.Visit = d.Origin = d.Session = null; d.LastRequest = d.LastReply = null; d.Sequence = 0; d.Transitioning = false;
            }
        }
        public void UnregisterDisplay(DynamicDisplay d) { Mutable(); Own(d); Hide(d, "destroyed"); displays.Remove(d.DisplayId); }
        internal void LocalChoice(DynamicDisplay d, string candidate)
        {
            Mutable(); Own(d); DynamicCheck.Require(Catalog.Target(d.TargetId).HasCandidate(candidate), "Unknown candidate");
            Expire(d); d.LocalCandidate = candidate;
            if (d.Active && d.Session == null) { Reset(d); Changed("state", d); }
        }
        void Invoke(DynamicDisplay d, DynamicPolicy policy)
        {
            d.Transitioning = true; busy = true;
            try { d.Apply(policy); d.Policy = policy; d.Fault = false; }
            catch { d.Fault = true; throw; }
            finally { busy = false; d.Transitioning = false; }
        }
        void Reset(DynamicDisplay d)
        {
            d.Session = null; d.Sequence = 0; d.LastRequest = d.LastReply = null; d.Claim = Guid.NewGuid().ToString("N");
            try { Invoke(d, new DynamicPolicy("source", d.LocalCandidate, DynamicLayout.Default, false)); }
            catch { d.RetryAt = now() + .5; }
        }
        internal void Expire(DynamicDisplay d)
        {
            if (disposed || busy || !d.Active) return;
            if (d.Session != null && now() >= d.Deadline || d.Session == null && d.Fault && now() >= d.RetryAt)
            { Reset(d); Changed("state", d); }
        }
        public void Tick()
        {
            if (disposed || busy) return;
            foreach (var d in displays.Values) Expire(d);
        }
        JObject Header() => new JObject { ["protocol"] = DynamicCatalog.Format, ["contract"] = DynamicCatalog.Contract,
            ["providerId"] = Catalog.ProviderId, ["revision"] = Catalog.Revision, ["instance"] = instance, ["renderMode"] = "dynamic" };
        JObject Row(DynamicDisplay d)
        {
            var row = new JObject { ["targetId"] = d.TargetId, ["displayId"] = d.DisplayId, ["visitId"] = d.Visit,
                ["displayEpoch"] = d.Epoch, ["claim"] = d.Claim, ["mode"] = d.Fault ? "fault" : d.Policy.Mode,
                ["candidateId"] = d.Policy.CandidateId, ["layout"] = d.Policy.Layout.Json(),
                ["surfaceType"] = d.Surface is Image ? "Image" : "SpriteRenderer", ["layerCount"] = d.Layers?.Length ?? 0 };
            if (d.Origin != null) row["originVisitId"] = d.Origin;
            if (d.Session != null) row["session"] = d.Session;
            return row;
        }
        string Snapshot()
        {
            if (!dirty) return snapshot;
            var root = Header(); root["enabled"] = !disposed; root["generation"] = generation;
            root["displays"] = new JArray(displays.Values.Where(d => d.Active).Select(Row));
            snapshot = root.ToString(Formatting.None); dirty = false; return snapshot;
        }
        object[] Surface(string displayId)
        {
            if (disposed || !displays.TryGetValue(displayId, out var d) || !d.Active) return null;
            var meta = Header(); meta["display"] = Row(d);
            return new object[] { meta.ToString(Formatting.None), d.Surface, d.Original, (UnityEngine.Object[])d.Layers.Clone() };
        }
        void Changed(string op, DynamicDisplay d = null, string reason = null)
        {
            generation++; dirty = true;
            var message = Header(); message["op"] = op; message["generation"] = generation;
            if (d != null) { message["display"] = Row(d); if (reason != null) message["reason"] = reason; }
            Send(message.ToString(Formatting.None));
        }
        void Send(string json)
        {
            bool previous = busy; busy = true;
            try { ModHelper.SendMessage(Catalog.ProviderId, Channel, json); }
            catch (Exception error) { Debug.LogWarning("[" + Catalog.ProviderId + "] Dynamic notification: " + error.Message); }
            finally { busy = previous; }
        }
        void Receive(string sender, object payload)
        {
            if (disposed || busy || sender != Manager || !(payload is string json) || json.Length > 65536
                || System.Text.Encoding.UTF8.GetByteCount(json) > 65536) return;
            JObject request = null; DynamicDisplay d = null;
            try
            {
                request = DynamicCheck.Parse(json); string op = DynamicCheck.Id(request, "op");
                string[] common = { "protocol", "contract", "providerId", "revision", "instance", "renderMode", "op", "targetId", "displayId", "visitId", "displayEpoch", "claim", "session", "seq" };
                DynamicCheck.Require(op == "select" || op == "renew" || op == "release", "Unknown operation");
                DynamicCheck.Fields(request, op == "select" ? common.Concat(new[] { "mode", "candidateId", "layout" }).ToArray() : common);
                foreach (var field in new[] { "protocol", "contract", "providerId", "revision", "instance", "renderMode" })
                    DynamicCheck.Require(request[field]?.Type == JTokenType.String && (string)request[field] == (string)Header()[field], "Header mismatch");
                string display = DynamicCheck.Id(request, "displayId");
                DynamicCheck.Require(displays.TryGetValue(display, out d) && d.Active, "Inactive display");
                Expire(d);
                DynamicCheck.Require(DynamicCheck.Id(request, "targetId") == d.TargetId && DynamicCheck.Id(request, "visitId") == d.Visit
                    && DynamicCheck.Sequence(request, "displayEpoch") == d.Epoch && DynamicCheck.Id(request, "claim") == d.Claim, "Stale display/claim");
                string session = DynamicCheck.Id(request, "session"); long seq = DynamicCheck.Sequence(request, "seq");
                string signature = request.ToString(Formatting.None);
                DynamicCheck.Require(d.Session == session || op == "select" && d.Session == null, "Session mismatch");
                if (seq == d.Sequence && d.LastRequest == signature) { Send(d.LastReply); return; }
                DynamicCheck.Require(seq > d.Sequence, "Stale sequence");
                if (op == "select")
                {
                    string mode = DynamicCheck.Id(request, "mode"), candidate = DynamicCheck.Id(request, "candidateId");
                    DynamicCheck.Require((mode == "source" || mode == "silent") && Catalog.Target(d.TargetId).HasCandidate(candidate), "Unknown mode/candidate");
                    var layout = DynamicLayout.Read(request["layout"] as JObject);
                    DynamicCheck.Require(mode != "silent" || layout.Scale == 1 && layout.X == 0 && layout.Y == 0, "Silent uses neutral provider layout");
                    d.Session = session; d.Deadline = now() + LeaseSeconds;
                    // A failing author callback still consumes its sequence: an
                    // older delayed command must never undo the failed switch.
                    d.Sequence = seq; d.LastRequest = d.LastReply = null;
                    Invoke(d, new DynamicPolicy(mode, candidate, layout, true));
                }
                else if (op == "renew") DynamicCheck.Require(!d.Fault, "Cannot renew faulted display");
                else { Reset(d); DynamicCheck.Require(!d.Fault, "Standalone recovery failed"); }
                d.Sequence = op == "release" ? 0 : seq;
                if (op != "release") d.Deadline = now() + LeaseSeconds;
                var reply = Header(); reply["op"] = "ack"; reply["requestOp"] = op; reply["seq"] = seq;
                reply["session"] = session; reply["display"] = Row(d); reply["leaseSeconds"] = op == "release" ? 0 : LeaseSeconds;
                d.LastRequest = op == "release" ? null : signature; d.LastReply = reply.ToString(Formatting.None);
                if (op != "renew") Changed("state", d);
                Send(d.LastReply);
            }
            catch (Exception error)
            {
                dirty = true;
                var reply = Header(); reply["op"] = "error"; reply["message"] = error.Message.Length <= 512 ? error.Message : error.Message.Substring(0, 512);
                if (request != null)
                {
                    foreach (var key in new[] { "displayId", "session" })
                        if (request[key]?.Type == JTokenType.String && ((string)request[key]).Length > 0 && ((string)request[key]).Length <= 160
                            && !((string)request[key]).Any(char.IsControl)) reply[key] = request[key].DeepClone();
                    if (request["seq"]?.Type == JTokenType.Integer && long.TryParse(request["seq"].ToString(), out long errorSeq) && errorSeq > 0 && errorSeq <= 9007199254740991)
                        reply["seq"] = request["seq"].DeepClone();
                }
                if (d?.Active == true) reply["display"] = Row(d);
                Send(reply.ToString(Formatting.None));
            }
        }
        public void Dispose()
        {
            if (disposed) return; Mutable();
            foreach (var d in displays.Values.ToArray())
                try { Hide(d, "hidden"); } catch (Exception error) { Debug.LogWarning("Dynamic hide: " + error.Message); }
            disposed = true; Changed("disabled");
            ModHelper.OffMessage(Catalog.ProviderId, Manager + ":" + Channel, Receive);
            if (ReferenceEquals(ModHelper.Lookup<Func<string>>(Catalog.ProviderId, ActiveService), activeService)) ModHelper.Expose(Catalog.ProviderId, ActiveService, null);
            if (ReferenceEquals(ModHelper.Lookup<Func<string, object[]>>(Catalog.ProviderId, SurfaceService), surfaceService)) ModHelper.Expose(Catalog.ProviderId, SurfaceService, null);
        }
    }
}
