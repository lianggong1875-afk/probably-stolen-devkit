using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace PortraitStudio.StaticV1
{
    public struct DisplayDecision
    {
        public readonly bool shouldWrite;
        public readonly Sprite sprite;
        public DisplayDecision(bool write, Sprite value) { shouldWrite = write; sprite = value; }
    }
    public sealed class StaticDisplay
    {
        internal StaticBridge owner;
        internal Func<Sprite> read, source;
        internal Action<DisplayDecision> apply;
        internal Sprite original, lastWritten;
        internal StaticBridge.Frame local, current;
        internal StaticBridge.Transaction transaction;
        internal bool active, localReady;
        internal long epoch;
        internal string visit, originVisit;
        public readonly string TargetId, DisplayId, SurfaceId;
        internal StaticDisplay(StaticBridge bridge, string target, string display, string surface) { owner = bridge; TargetId = target; DisplayId = display; SurfaceId = surface; }
        public void Show(string visitId, Sprite immutableOriginal, string originVisitId = null) { owner.Show(this, visitId, immutableOriginal, originVisitId); }
        public void Hide(string reason = "hidden") { owner.Hide(this, reason); }
        public void SourceChanged() { owner.SourceChanged(this); }
        public DisplayDecision Resolve() => !active ? new DisplayDecision(false, null) : current == null ? new DisplayDecision(true, source()) : current.decision;
    }
    public sealed class StaticBridge : IDisposable
    {
        public const string Channel = "static-v1", Manager = "PortraitStudio", ActiveServiceKey = "portrait-studio-static-active/1";
        internal sealed class Frame { public DisplayDecision decision; public StaticResources.Lease lease; public string choiceId; }
        sealed class Choice { public string target, mode, signature; public Layout layout; public ResourceRef resource; }
        internal sealed class Transaction
        {
            public JObject fields;
            public string id, planId, reservation, phase;
            public StaticDisplay display;
            public StaticResources.Plan plan, defaultPlan;
            public Budget budget;
            public Frame prepared, previous, defaultPrepared;
            public double deadline;
        }
        readonly StaticResources resources;
        readonly Dictionary<string, StaticDisplay> displays = new Dictionary<string, StaticDisplay>(StringComparer.Ordinal);
        readonly Dictionary<string, Choice> choices = new Dictionary<string, Choice>(StringComparer.Ordinal);
        readonly Dictionary<string, string> defaults = new Dictionary<string, string>(StringComparer.Ordinal);
        Dictionary<string, ResourceRef> localResources = new Dictionary<string, ResourceRef>(StringComparer.Ordinal);
        string pendingPortraitSet;
        readonly HashSet<string> retiredTransactions = new HashSet<string>(StringComparer.Ordinal);
        sealed class Cached { public string request; public List<string> replies; public long chars; }
        readonly Dictionary<string, Cached> responses = new Dictionary<string, Cached>(StringComparer.Ordinal);
        readonly Dictionary<string, long> epochs = new Dictionary<string, long>(StringComparer.Ordinal);
        readonly Queue<string> responseOrder = new Queue<string>();
        readonly List<StaticDisplay> expired = new List<StaticDisplay>();
        readonly Dictionary<int, long> borrowed = new Dictionary<int, long>();
        readonly Dictionary<string, int> activeTargets = new Dictionary<string, int>(StringComparer.Ordinal);
        readonly Func<string> activeService;
        string activeSnapshot;
        bool activeDirty = true;
        List<string> recording;
        long eventSeq, responseChars;
        bool disposed;
        public readonly Catalog Descriptor;
        public readonly StaticSession State;
        public ResourceStats Statistics => resources.Stats;
        public StaticBridge(string providerId, long imageBudgetBytes = 128L * 1024 * 1024, long warmCacheBytes = 32L * 1024 * 1024)
        {
            resources = new StaticResources(imageBudgetBytes, warmCacheBytes);
            try
            {
                Descriptor = resources.ReadCatalog(providerId);
                Check.That(Descriptor.role != "resources", "Resource-only provider does not create a writer bridge");
                State = new StaticSession(Descriptor.targets.Select(t => t.id));
                Check.That(ModHelper.Lookup<Func<string>>(providerId, ActiveServiceKey) == null, "Static writer already published");
                activeService = ReadActiveSnapshot;
                ModHelper.OnMessage(providerId, Manager + ":" + Channel, Receive);
                try { ModHelper.Expose(providerId, ActiveServiceKey, activeService); }
                catch { ModHelper.OffMessage(providerId, Manager + ":" + Channel, Receive); throw; }
            }
            catch { resources.Dispose(); throw; }
        }
        string ReadActiveSnapshot()
        {
            if (disposed) return null;
            if (!activeDirty) return activeSnapshot;
            activeDirty = false;
            var snapshot = new JObject { ["protocol"] = Catalog.Format, ["contract"] = Catalog.Contract,
                ["renderMode"] = "static", ["providerId"] = Descriptor.providerId,
                ["instance"] = State.Instance, ["revision"] = Descriptor.revision,
                ["targets"] = new JArray(activeTargets.Keys.OrderBy(id => id, StringComparer.Ordinal)) }.ToString(Formatting.None);
            activeSnapshot = snapshot.Length <= 65536 && System.Text.Encoding.UTF8.GetByteCount(snapshot) <= 65536 ? snapshot : null;
            if (activeSnapshot == null) Debug.LogError("[" + Descriptor.providerId + "] Active static target snapshot exceeds 64 KiB; discovery unavailable until the active set changes.");
            return activeSnapshot;
        }
        void ActiveTarget(string id, int delta)
        {
            activeTargets.TryGetValue(id, out var count);
            if (count + delta == 0) { activeTargets.Remove(id); activeDirty = true; }
            else { activeTargets[id] = count + delta; if (count == 0) activeDirty = true; }
        }
        public StaticDisplay RegisterDisplay(string targetId, string displayId, string surfaceId, Func<Sprite> readCurrent, Func<Sprite> sourceDefault, Action<DisplayDecision> finalWrite)
        {
            Check.That(!disposed && Check.Id(displayId) && Check.Id(surfaceId) && !displays.ContainsKey(displayId) && readCurrent != null && sourceDefault != null && finalWrite != null, "Invalid/duplicate display");
            Descriptor.GetTarget(targetId);
            var d = new StaticDisplay(this, targetId, displayId, surfaceId) { read = readCurrent, source = sourceDefault, apply = finalWrite };
            displays.Add(displayId, d); return d;
        }
        public void UnregisterDisplay(StaticDisplay display)
        {
            Check.That(display.owner == this, "Foreign display");
            try { Hide(display, "destroyed"); } finally { displays.Remove(display.DisplayId); }
        }
        internal void Show(StaticDisplay d, string visit, Sprite original, string originVisit)
        {
            Check.That(!disposed && displays.TryGetValue(d.DisplayId, out var known) && known == d && Check.Id(visit) && (originVisit == null || Check.Id(originVisit)) && original != null, "Invalid display/immutable original");
            if (d.active && d.visit == visit && d.original == original && d.originVisit == originVisit) return;
            if (d.active) Hide(d, "rebound");
            d.visit = visit; d.originVisit = originVisit; d.original = original;
            epochs.TryGetValue(d.DisplayId, out var previousEpoch); d.epoch = previousEpoch + 1; epochs[d.DisplayId] = d.epoch; d.active = true;
            bool reportedActive = false;
            try
            {
                var target = Descriptor.GetTarget(d.TargetId);
                bool managed = State.Session != null && State.Targets.Contains(d.TargetId);
                d.localReady = !managed || target.defaultMode == "source";
                d.local = d.localReady ? MakeLocal(d) : new Frame { decision = new DisplayDecision(false, null) }; d.current = d.local;
                if (d.localReady) Apply(d, d.local, false);
                ActiveTarget(d.TargetId, 1); reportedActive = true;
                if (State.Session != null && State.Targets.Contains(d.TargetId)) ShowEvent(d);
            }
            catch { if (reportedActive) ActiveTarget(d.TargetId, -1); d.active = false; DisposeFrame(d.local); d.local = d.current = null; throw; }
        }
        Frame MakeLocal(StaticDisplay d)
            => MakeLocal(d, localResources);
        ResourceRef LocalResource(Target target) => localResources.TryGetValue(target.id, out var r) ? r : target.defaultResource;
        Frame MakeLocal(StaticDisplay d, Dictionary<string, ResourceRef> mapping)
        {
            var target = Descriptor.GetTarget(d.TargetId);
            if (target.defaultMode == "source") return new Frame { decision = new DisplayDecision(true, d.source()) };
            var reference = mapping.TryGetValue(target.id, out var selected) ? selected : target.defaultResource;
            var plan = resources.PlanResource(target, reference, Layout.Identity);
            var lease = resources.Prepare(plan, new Grant { residentDeltaBytes = plan.budget.residentDeltaBytes, peakAdditionalBytes = plan.budget.peakAdditionalBytes });
            return new Frame { decision = new DisplayDecision(true, lease.Sprite), lease = lease };
        }
        // User-selected static source defaults. No catalog mutation and no new wire operation.
        // A manager session retains its choices/budget; source changes wait until release.
        public void SetPortraitSet(string id)
        {
            Check.That(!disposed, "Bridge disposed");
            var set = Descriptor.GetPortraitSet(id);
            var mapping = new Dictionary<string, ResourceRef>(localResources, StringComparer.Ordinal);
            var targetsByIdentity = Descriptor.targets.ToDictionary(t => t.Identity, StringComparer.Ordinal);
            foreach (var r in set.entries)
            {
                var identity = Descriptor.GetSet(r.setId).Identity;
                var target = targetsByIdentity[identity];
                Check.That(target.defaultMode == "resource", "Source-default adapters must retain their own set switching");
                mapping[target.id] = r;
            }
            if (State.Session != null) { pendingPortraitSet = id; return; }
            var changed = displays.Values.Where(d => d.active && d.localReady && mapping.TryGetValue(d.TargetId, out var r)
                && (!localResources.TryGetValue(d.TargetId, out var old) ? Descriptor.GetTarget(d.TargetId).defaultResource?.Key != r.Key : old.Key != r.Key)).ToArray();
            var prepared = new Dictionary<StaticDisplay, Frame>();
            var previous = new Dictionary<StaticDisplay, Frame>();
            try
            {
                // Load only active surfaces; prepare the whole visible change before writing.
                foreach (var d in changed) { Check.That(d.transaction == null, "Display transaction active"); prepared.Add(d, MakeLocal(d, mapping)); previous.Add(d, d.local); }
                foreach (var d in changed)
                {
                    var old = d.local; d.local = prepared[d];
                    if (d.current == old) { d.current = d.local; Apply(d, d.current, true); }
                }
            }
            catch
            {
                foreach (var pair in previous)
                {
                    var d = pair.Key;
                    if (d.current == prepared[d]) { d.current = pair.Value; try { Apply(d, d.current, true); } catch { } }
                    d.local = pair.Value;
                }
                foreach (var frame in prepared.Values) DisposeFrame(frame);
                throw;
            }
            localResources = mapping; pendingPortraitSet = null;
            foreach (var frame in previous.Values) DisposeFrame(frame);
        }
        void ApplyPendingPortraitSet()
        {
            if (State.Session != null || pendingPortraitSet == null) return;
            var id = pendingPortraitSet; pendingPortraitSet = null;
            try { SetPortraitSet(id); }
            catch (Exception error) { Debug.LogError("[" + Descriptor.providerId + "] Portrait set change failed; previous defaults retained: " + error); }
        }
        internal void SourceChanged(StaticDisplay d)
        {
            Check.That(d.owner == this && d.active && d.localReady && Descriptor.GetTarget(d.TargetId).defaultMode == "source", "SourceChanged requires a prepared source default");
            if (d.transaction != null) CloseTransaction(d, true);
            var old = d.local; var next = MakeLocal(d); d.local = next; d.localReady = true;
            if (d.current == old) { d.current = next; Apply(d, next, true); DisposeFrame(old); }
            else DisposeFrame(old);
            if (State.Session != null) ShowEvent(d);
        }
        internal void Hide(StaticDisplay d, string reason)
        {
            if (!d.active) return;
            Check.That(reason == "hidden" || reason == "destroyed" || reason == "rebound", "Invalid hide reason");
            try
            {
                if (State.Session != null && State.Targets.Contains(d.TargetId)) Event("hide", IdentityFields(d, false), o => o["reason"] = reason);
                // Rebinding must not restore a sprite onto an object already assigned to another subject.
                if (reason != "rebound" && reason != "destroyed" && d.lastWritten != null && d.read() == d.lastWritten)
                    d.apply(new DisplayDecision(true, d.original));
            }
            finally { d.active = false; ActiveTarget(d.TargetId, -1); ClearFrames(d); d.original = d.lastWritten = null; }
            if (State.Session != null) ReportEvent("display-released", IdentityFields(d, false));
        }
        static void DisposeFrame(Frame f) { f?.lease?.Dispose(); }
        void ClearFrames(StaticDisplay d)
        {
            var seen = new HashSet<Frame>();
            foreach (var f in new[] { d.current, d.local, d.transaction?.prepared, d.transaction?.previous, d.transaction?.defaultPrepared }) if (f != null && seen.Add(f)) DisposeFrame(f);
            if (d.transaction != null) retiredTransactions.Add(d.transaction.id);
            d.current = d.local = null; d.transaction = null;
        }
        void Apply(StaticDisplay d, Frame frame, bool restoring)
        {
            var decision = frame.decision;
            // Restore rights but avoid overwriting a later third-party sprite.
            if (restoring && decision.shouldWrite && d.lastWritten != null && d.read() != d.lastWritten)
                decision = new DisplayDecision(false, decision.sprite);
            if (decision.shouldWrite) d.lastWritten = decision.sprite;
            d.apply(decision);
        }
        public void Announce()
        {
            if (disposed) return;
            var snapshot = Guid.NewGuid().ToString("N");
            for (int page = 0, first = 0; first < Descriptor.targets.Length; page++, first += 32)
            {
                int index = page, start = first;
                Event("hello", null, o => { o["targets"] = JArray.FromObject(Descriptor.targets.Skip(start).Take(32).Select(t => t.id)); o["memory"] = JObject.FromObject(Memory()); o["snapshotId"] = snapshot; o["page"] = index; o["last"] = start + 32 >= Descriptor.targets.Length; });
            }
        }

        public void Tick()
        {
            if (disposed) return;
            ApplyPendingPortraitSet();
            bool changed = resources.Tick();
            if (State.Session != null && Time.realtimeSinceStartup >= State.Deadline) { RestoreSession(); State.Clear(); ClearSession(); ActivateStandaloneDefaults(); return; }
            expired.Clear();
            foreach (var d in displays.Values) if (d.transaction != null && d.transaction.phase != "applied" && Time.realtimeSinceStartup >= d.transaction.deadline) expired.Add(d);
            foreach (var d in expired)
            {
                var fields = TransactionFields(d.transaction); CloseTransaction(d, false);
                if (State.Session != null) ReportEvent("transaction-expired", fields);
            }
            if (changed && State.Session != null) ReportEvent("memory-changed", null);
        }
        void Receive(string sender, object payload)
        {
            if (disposed || sender != Manager || !(payload is string json) || json.Length > 65536) return;
            JObject m = null;
            try
            {
                m = JObject.Parse(json, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error }); StaticWire.Validate(m);
                if ((string)m["op"] == "discover") { Announce(); return; }
                Check.That((string)m["providerId"] == Descriptor.providerId && (string)m["revision"] == Descriptor.revision && (string)m["instance"] == State.Instance, "Writer instance/revision mismatch", "stale-session");
                Tick();
                long seq = (long)m["seq"]; string signature = m.ToString(Formatting.None);
                string responseKey = JsonConvert.SerializeObject(new object[] { (string)m["session"], seq });
                if (responses.TryGetValue(responseKey, out var cached))
                {
                    Check.That(cached.request == signature, "Sequence reused with different request", "invalid-message");
                    foreach (var response in cached.replies) Send(response); return;
                }
                var op = (string)m["op"];
                if (op == "acquire")
                {
                    Check.That(State.Acquire(State.Instance, (string)m["session"], seq, m["targets"].ToObject<string[]>(), Time.realtimeSinceStartup), "Acquire rejected", "stale-session");
                    recording = new List<string>();
                    Reply(m, "ready", o => { o["targets"] = m["targets"].DeepClone(); o["memory"] = JObject.FromObject(Memory()); });
                    Cache(responseKey, signature); recording = null;
                    foreach (var d in displays.Values) if (d.active && State.Targets.Contains(d.TargetId)) ShowEvent(d);
                    return;
                }
                Check.That(State.Accepts(State.Instance, (string)m["session"], seq, Time.realtimeSinceStartup), "Session/sequence expired", "stale-session");
                recording = new List<string>(); Dispatch(m); State.Commit(seq, Time.realtimeSinceStartup); Cache(responseKey, signature); recording = null;
                if (op == "release") { State.Clear(); ClearSession(); }
            }
            catch (Exception e)
            {
                recording = null;
                if (m == null || m["seq"]?.Type != JTokenType.Integer || m["op"]?.Type != JTokenType.String) return;
                Reply(m, "error", o => { o["code"] = e is ContractException c ? c.Code : "invalid-message"; o["message"] = e.Message.Length > 1000 ? e.Message.Substring(0, 1000) : e.Message; });
            }
        }
        void Cache(string key, string signature)
        {
            var size = signature.Length + recording.Sum(r => (long)r.Length);
            responses[key] = new Cached { request = signature, replies = recording, chars = size }; responseChars += size; responseOrder.Enqueue(key);
            while (responseOrder.Count > 256 || responseChars > 1048576)
            {
                var oldest = responseOrder.Dequeue(); responseChars -= responses[oldest].chars; responses.Remove(oldest);
            }
        }
        void Dispatch(JObject m)
        {
            switch ((string)m["op"])
            {
                case "heartbeat": Reply(m, "ack"); return;
                case "select": Select(m); return;
                case "forget-choice":
                    var ids = m["choiceIds"].ToObject<string[]>();
                    Check.That(ids.All(id => choices.ContainsKey(id) && !Referenced(id)), "Choice still referenced");
                    foreach (var id in ids) choices.Remove(id);
                    Reply(m, "forgotten", o => o["choiceIds"] = m["choiceIds"].DeepClone()); return;
                case "plan": Plan(m); return;
                case "prepare": Prepare(m); return;
                case "commit": Commit(m); return;
                case "rollback": End(m, "rolled-back", true); return;
                case "cancel": End(m, "cancelled", false); return;
                case "finish": Finish(m); return;
                case "trim": resources.Trim((long)m["warmLimitBytes"]); ReportReply(m, "trimmed"); return;
                case "query-state": ReportReply(m, "state", true); return;
                case "release": RestoreSession(); State.Clear(); ClearSession(); ActivateStandaloneDefaults(); ReportReply(m, "released"); return;
                default: throw new ContractException("invalid-message", "Unknown operation");
            }
        }
        void Select(JObject m)
        {
            string target = (string)m["targetId"], id = (string)m["choiceId"];
            Check.That(State.Targets.Contains(target), "Target outside session");
            var layout = m["layout"] == null ? Layout.Identity : Layout.Read(m["layout"]);
            var resource = m["resource"] == null ? null : Check.Parse<ResourceRef>(m["resource"].ToString());
            string signature = JsonConvert.SerializeObject(new object[] { target, (string)m["mode"], layout, resource });
            if (choices.TryGetValue(id, out var old)) Check.That(old.signature == signature, "Choice ID is immutable");
            else choices.Add(id, new Choice { target = target, mode = (string)m["mode"], layout = layout, resource = resource, signature = signature });
            if ((bool)m["setDefault"]) defaults[target] = id;
            Reply(m, "stored", o => { o["targetId"] = target; o["choiceId"] = id; o["setDefault"] = (bool)m["setDefault"]; });
        }
        bool Referenced(string id)
        {
            if (defaults.Values.Contains(id)) return true;
            foreach (var d in displays.Values) if (d.current?.choiceId == id || d.transaction?.prepared?.choiceId == id || d.transaction?.previous?.choiceId == id || (string)d.transaction?.fields["choiceId"] == id) return true;
            return false;
        }
        StaticDisplay Matching(JObject m)
        {
            Check.That(displays.TryGetValue((string)m["displayId"], out var d) && d.active && d.TargetId == (string)m["targetId"] && d.visit == (string)m["visitId"] && d.epoch == (long)m["displayEpoch"] && d.SurfaceId == (string)m["surfaceId"] && State.Targets.Contains(d.TargetId), "Display lifetime changed", "stale-display");
            return d;
        }
        static readonly string[] association = { "targetId", "displayId", "visitId", "displayEpoch", "choiceId", "transactionId", "surfaceId", "encounterId", "groupId", "presentationRole", "presenterProviderId" };
        static JObject Associated(JObject m)
        {
            var o = new JObject(); foreach (var k in association) if (m[k] != null) o[k] = m[k].DeepClone(); return o;
        }
        void Plan(JObject m)
        {
            var d = Matching(m); string id = (string)m["transactionId"], role = (string)m["presentationRole"], presenter = (string)m["presenterProviderId"];
            Check.That(d.transaction == null && !retiredTransactions.Contains(id), "Display transaction already active/retired", "stale-plan");
            Check.That(choices.TryGetValue((string)m["choiceId"], out var choice) && choice.target == d.TargetId, "Unknown choice/target");
            Check.That(role == "presenter" && presenter == Descriptor.providerId || role == "silent" && presenter != Descriptor.providerId, "Invalid presenter role");
            StaticResources.Plan plan = null, defaultPlan = null;
            if (!d.localReady)
            {
                // Even a silent writer must retain its independently usable fallback.
                // Reserve and prepare it without making it the displayed choice.
                defaultPlan = resources.PlanResource(Descriptor.GetTarget(d.TargetId), LocalResource(Descriptor.GetTarget(d.TargetId)), Layout.Identity);
            }
            if (role == "presenter")
            {
                var target = Descriptor.GetTarget(d.TargetId);
                if (choice.mode == "selected") plan = resources.PlanResource(target, choice.resource, choice.layout);
                else if (choice.mode == "original") plan = resources.PlanBorrowed(d.original, choice.layout);
                else if (target.defaultMode == "resource") plan = resources.PlanResource(target, LocalResource(target), choice.layout);
                else plan = resources.PlanBorrowed(d.source(), choice.layout);
            }
            d.transaction = new Transaction { fields = Associated(m), id = id, planId = Guid.NewGuid().ToString("N"), phase = "planned", display = d, plan = plan, defaultPlan = defaultPlan, budget = Combine(plan?.budget, defaultPlan?.budget), deadline = Time.realtimeSinceStartup + 2 };
            Reply(m, "planned", o => { Copy(o, TransactionFields(d.transaction)); o["budget"] = JObject.FromObject(d.transaction.budget); o["memory"] = JObject.FromObject(Memory()); });
        }
        static Budget Combine(Budget a, Budget b)
        {
            var parts = new[] { a, b }.Where(p => p != null).ToArray();
            var textures = parts.SelectMany(p => p.textures).GroupBy(t => t.textureKey, StringComparer.Ordinal).Select(g => g.First()).ToArray();
            return new Budget { textures = textures, residentDeltaBytes = textures.Where(t => !t.alreadyResident).Sum(t => t.residentBytes), peakAdditionalBytes = parts.Sum(p => p.peakAdditionalBytes) };
        }
        Transaction TransactionFor(JObject m)
        {
            var d = Matching(m); var t = d.transaction;
            Check.That(t != null && association.All(k => JToken.DeepEquals(t.fields[k], m[k])) && t.planId == (string)m["planId"], "Transaction mismatch", "stale-plan");
            return t;
        }
        void Prepare(JObject m)
        {
            var t = TransactionFor(m); Check.That(t.phase == "planned", "Plan not awaiting prepare", "stale-plan");
            var grant = Check.Parse<Grant>(m["grant"].ToString());
            Check.That(grant.residentDeltaBytes >= t.budget.residentDeltaBytes && grant.peakAdditionalBytes >= t.budget.peakAdditionalBytes, "Reservation too small", "budget-exceeded");
            StaticResources.Lease lease = null, defaultLease = null;
            try
            {
                if (t.defaultPlan != null) defaultLease = resources.Prepare(t.defaultPlan, grant);
                if (t.plan != null) lease = resources.Prepare(t.plan, grant);
                t.prepared = new Frame { lease = lease, choiceId = (string)m["choiceId"], decision = new DisplayDecision(t.plan != null, lease?.Sprite) };
                if (defaultLease != null) t.defaultPrepared = new Frame { lease = defaultLease, decision = new DisplayDecision(true, defaultLease.Sprite) };
            }
            catch { lease?.Dispose(); defaultLease?.Dispose(); throw; }
            t.reservation = (string)m["reservationId"]; t.phase = "prepared";
            ReportReply(m, "prepared");
        }
        void Commit(JObject m)
        {
            var t = TransactionFor(m); Check.That(t.phase == "prepared" && t.reservation == (string)m["reservationId"], "Not prepared/reservation mismatch", "stale-plan");
            var d = t.display; t.previous = !d.localReady && d.current == d.local && t.prepared.decision.shouldWrite ? new Frame { decision = new DisplayDecision(true, d.read()) } : d.current; d.current = t.prepared;
            try { Apply(d, d.current, false); }
            catch (Exception e)
            {
                d.current = t.previous;
                try { Apply(d, t.previous, true); } catch { }
                throw new ContractException("apply-failed", e.Message);
            }
            if (t.defaultPrepared != null) { d.local = t.defaultPrepared; d.localReady = true; }
            t.phase = "applied"; ReportReply(m, "applied");
        }
        void End(JObject m, string op, bool rollback)
        {
            var t = TransactionFor(m);
            Check.That(rollback || t.phase != "applied", "Committed transaction requires rollback", "stale-plan");
            Check.That(t.reservation == null || t.reservation == (string)m["reservationId"], "Reservation mismatch", "stale-plan");
            CloseTransaction(t.display, rollback); ReportReply(m, op);
        }
        void CloseTransaction(StaticDisplay d, bool rollback)
        {
            var t = d.transaction; if (t == null) return;
            if (t.phase == "applied" && rollback)
            {
                var next = d.current; d.current = t.previous;
                try { Apply(d, d.current, true); }
                catch (Exception e) { d.current = next; throw new ContractException("apply-failed", e.Message); }
            }
            if (t.prepared != d.current && t.prepared != d.local) DisposeFrame(t.prepared);
            if (t.defaultPrepared != null && t.defaultPrepared != d.local) DisposeFrame(t.defaultPrepared);
            retiredTransactions.Add(t.id); d.transaction = null;
        }
        void Finish(JObject m)
        {
            var t = TransactionFor(m); Check.That(t.phase == "applied" && t.reservation == (string)m["reservationId"], "Not applied/reservation mismatch", "stale-plan");
            if (t.previous != t.display.local) DisposeFrame(t.previous);
            retiredTransactions.Add(t.id); t.display.transaction = null; ReportReply(m, "finished");
        }
        void RestoreSession()
        {
            foreach (var d in displays.Values) if (d.active)
            {
                if (d.transaction != null) CloseTransaction(d, true);
                if (d.localReady && d.current != d.local) { var old = d.current; d.current = d.local; Apply(d, d.local, true); DisposeFrame(old); }
            }
        }
        void ActivateStandaloneDefaults()
        {
            Check.That(State.Session == null, "Independent defaults require an ended session");
            ApplyPendingPortraitSet();
            foreach (var d in displays.Values) if (d.active && !d.localReady)
            {
                var old = d.current; var local = MakeLocal(d); d.local = d.current = local; d.localReady = true;
                Apply(d, local, false); if (old != local) DisposeFrame(old);
            }
        }
        void ClearSession() { choices.Clear(); defaults.Clear(); retiredTransactions.Clear(); }
        MemoryReport Memory()
        {
            var m = resources.Memory(); borrowed.Clear();
            foreach (var d in displays.Values) if (d.active)
            {
                AddBorrowed(d.original); if (Descriptor.GetTarget(d.TargetId).defaultMode == "source") AddBorrowed(d.local?.decision.sprite);
            }
            foreach (var n in borrowed.Values) m.borrowedBytes += n; return m;
        }
        void AddBorrowed(Sprite s)
        {
            if (s == null || s.texture == null) return; var t = s.texture; long n = 0; int w = t.width, h = t.height;
            for (int i = 0; i < t.mipmapCount; i++) { n += (long)w * h * 4; w = Math.Max(1, w / 2); h = Math.Max(1, h / 2); }
            borrowed[t.GetInstanceID()] = n;
        }
        List<HeldResource> Held()
        {
            var held = resources.Held().ToDictionary(h => h.resource.Key, StringComparer.Ordinal);
            foreach (var d in displays.Values) {
                if (d.transaction?.plan?.reference != null) StaticResources.AddHeld(held, d.transaction.plan.reference, 1);
                if (d.transaction?.defaultPlan?.reference != null) StaticResources.AddHeld(held, d.transaction.defaultPlan.reference, 1);
            }
            return held.Values.ToList();
        }
        JObject Base(string op, string session = null)
        {
            var o = new JObject { ["protocol"] = Catalog.Format, ["contract"] = Catalog.Contract, ["renderMode"] = "static", ["providerId"] = Descriptor.providerId, ["revision"] = Descriptor.revision, ["instance"] = State.Instance, ["op"] = op };
            if (session != null || State.Session != null) o["session"] = session ?? State.Session; return o;
        }
        void Send(string json) { Check.That(json.Length <= 65536, "Response too large"); ModHelper.SendMessage(Descriptor.providerId, Channel, json); }
        void SendObject(JObject o, bool response)
        {
            string json = o.ToString(Formatting.None); if (response) recording?.Add(json); Send(json);
        }
        void Reply(JObject request, string op, Action<JObject> fill = null)
        {
            var o = Base(op, (string)request["session"]); o["seq"] = request["seq"].DeepClone(); o["requestOp"] = request["op"].DeepClone();
            if (op == "error") Copy(o, Associated(request)); fill?.Invoke(o); SendObject(o, true);
        }
        void Event(string op, JObject fields, Action<JObject> fill = null)
        {
            var o = Base(op); if (op == "hello") o.Remove("session"); o["seq"] = 0; o["eventSeq"] = ++eventSeq;
            Copy(o, fields); fill?.Invoke(o); SendObject(o, false);
        }
        static void Copy(JObject dest, JObject fields) { if (fields != null) foreach (var p in fields.Properties()) dest[p.Name] = p.Value.DeepClone(); }
        static JObject IdentityFields(StaticDisplay d, bool surface)
        {
            var o = new JObject { ["targetId"] = d.TargetId, ["displayId"] = d.DisplayId, ["visitId"] = d.visit, ["displayEpoch"] = d.epoch };
            if (surface) o["surfaceId"] = d.SurfaceId; return o;
        }
        static JObject TransactionFields(Transaction t)
        {
            var o = (JObject)t.fields.DeepClone(); o["planId"] = t.planId; if (t.reservation != null) o["reservationId"] = t.reservation; return o;
        }
        void ShowEvent(StaticDisplay d)
        {
            Event("show", IdentityFields(d, true), o => { if (d.originVisit != null) o["originVisitId"] = d.originVisit; });
        }
        void ReportReply(JObject request, string op, bool state = false)
        {
            var fields = request["transactionId"] == null ? null : Associated(request);
            if (fields != null) { fields["planId"] = request["planId"].DeepClone(); if (request["reservationId"] != null) fields["reservationId"] = request["reservationId"].DeepClone(); }
            Reports((page, last, snapshot, held, rows) => Reply(request, op, o => { Copy(o, fields); o["memory"] = JObject.FromObject(Memory()); o["heldResources"] = JArray.FromObject(held); o["snapshotId"] = snapshot; o["page"] = page; o["last"] = last; if (state) o["displays"] = rows; }), state);
        }
        void ReportEvent(string op, JObject fields)
        {
            Reports((page, last, snapshot, held, rows) => Event(op, fields, o => { o["memory"] = JObject.FromObject(Memory()); o["heldResources"] = JArray.FromObject(held); o["snapshotId"] = snapshot; o["page"] = page; o["last"] = last; }), false);
        }
        void Reports(Action<int, bool, string, IEnumerable<HeldResource>, JArray> send, bool state)
        {
            var held = Held(); var rows = new List<JObject>();
            if (state) foreach (var d in displays.Values) if (d.active)
            {
                var o = IdentityFields(d, true); o["phase"] = d.transaction?.phase ?? (d.current?.choiceId == null ? "shown" : "finished");
                if (d.originVisit != null) o["originVisitId"] = d.originVisit;
                if (d.current?.choiceId != null) o["choiceId"] = d.current.choiceId; rows.Add(o);
            }
            int pages = Math.Max(1, Math.Max((held.Count + 31) / 32, (rows.Count + 31) / 32)); var snapshot = Guid.NewGuid().ToString("N");
            for (int page = 0; page < pages; page++) send(page, page + 1 == pages, snapshot, held.Skip(page * 32).Take(32), new JArray(rows.Skip(page * 32).Take(32)));
        }
        public void Dispose()
        {
            if (disposed) return;
            ModHelper.OffMessage(Descriptor.providerId, Manager + ":" + Channel, Receive);
            var errors = new List<Exception>();
            foreach (var d in displays.Values) if (d.active)
                try { Hide(d, "hidden"); } catch (Exception error) { errors.Add(error); }
            disposed = true; State.Clear(); ClearSession(); displays.Clear(); responses.Clear(); responseOrder.Clear(); responseChars = 0;
            activeSnapshot = null; activeTargets.Clear();
            if (ReferenceEquals(ModHelper.Lookup<Func<string>>(Descriptor.providerId, ActiveServiceKey), activeService))
                ModHelper.Expose(Descriptor.providerId, ActiveServiceKey, null);
            resources.Dispose();
            if (errors.Count > 0) throw new AggregateException("Static bridge cleanup failed", errors);
        }
    }
}
