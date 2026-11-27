using System;
using System.Collections.Generic;
using System.Linq;

namespace PortraitStudio.StaticV1
{
    // Pure state machine: no game APIs, wall clock, filesystem or background thread.
    public sealed class StaticSession
    {
        public const double LeaseSeconds = 10;
        public readonly string Instance = Guid.NewGuid().ToString("N");
        public string Session { get; private set; }
        public long Sequence { get; private set; }
        public double Deadline { get; private set; }
        readonly HashSet<string> allowed;
        readonly HashSet<string> retired = new HashSet<string>(StringComparer.Ordinal);
        public readonly HashSet<string> Targets = new HashSet<string>(StringComparer.Ordinal);
        public StaticSession(IEnumerable<string> allowedTargets) { allowed = new HashSet<string>(allowedTargets, StringComparer.Ordinal); }
        public bool Acquire(string instance, string session, long seq, string[] targets, double now)
        {
            Expire(now);
            if (instance != Instance || string.IsNullOrWhiteSpace(session) || retired.Contains(session) || Session != null || seq <= 0 || targets == null || targets.Length == 0 || targets.Distinct(StringComparer.Ordinal).Count() != targets.Length || targets.Any(t => !allowed.Contains(t))) return false;
            Session = session; Sequence = seq; Deadline = now + LeaseSeconds;
            Targets.UnionWith(targets);
            return true;
        }
        public bool Accepts(string instance, string session, long seq, double now)
        {
            return Session != null && now < Deadline && instance == Instance && session == Session && seq > Sequence;
        }
        public void Commit(long seq, double now) { Sequence = seq; Deadline = now + LeaseSeconds; }
        public bool Expire(double now) { if (Session == null || now < Deadline) return false; Clear(); return true; }
        public void Clear()
        {
            if (Session != null) retired.Add(Session);
            Session = null; Targets.Clear(); Deadline = 0;
        }
    }
}
