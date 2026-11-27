using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PortraitStudio.DynamicV1
{
    public sealed class DynamicCandidate
    {
        public readonly string Id, Label;
        internal DynamicCandidate(string id, string label) { Id = id; Label = label; }
    }
    public sealed class DynamicTarget
    {
        public readonly string Id, Kind, Owner, Key, Slot, Binding, BindingKey, DefaultCandidate;
        public readonly IReadOnlyList<DynamicCandidate> Candidates;
        internal DynamicTarget(JObject row)
        {
            DynamicCheck.Fields(row, "id", "kind", "subject", "slot", "renderMode", "binding", "layoutOwner", "defaultCandidate", "candidates");
            Id = DynamicCheck.Id(row, "id"); Kind = DynamicCheck.Id(row, "kind"); Slot = DynamicCheck.Id(row, "slot");
            DynamicCheck.Require((string)row["renderMode"] == "dynamic" && (string)row["layoutOwner"] == "provider", "Explicit dynamic/provider ownership required");
            var subject = row["subject"] as JObject; DynamicCheck.Fields(subject, "owner", "key");
            Owner = DynamicCheck.Id(subject, "owner"); Key = DynamicCheck.Id(subject, "key");
            var binding = row["binding"] as JObject; DynamicCheck.Fields(binding, "type", "key");
            Binding = DynamicCheck.Id(binding, "type"); BindingKey = DynamicCheck.Id(binding, "key");
            DynamicCheck.Require(BindingKey == Key, "Binding/subject key mismatch");
            DynamicCheck.Require(Kind == "character" && Slot == "main" && new[] { "sprite-key", "client-role", "custom" }.Contains(Binding)
                || Kind == "wanted" && Slot == "photo" && new[] { "wanted-key", "custom" }.Contains(Binding)
                || Kind == "scene" && Slot == "background" && new[] { "scene-key", "custom" }.Contains(Binding), "Invalid kind/slot/binding");
            DynamicCheck.Require((Owner == "game") == (Binding != "custom"), "Custom subjects need their creator's identity");
            DefaultCandidate = DynamicCheck.Id(row, "defaultCandidate");
            var candidates = row["candidates"] as JArray;
            DynamicCheck.Require(candidates != null && candidates.Count > 0 && candidates.Count <= 256, "Expected 1..256 dynamic candidates");
            var values = new List<DynamicCandidate>(); var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var token in candidates)
            {
                var candidate = token as JObject; DynamicCheck.Fields(candidate, "id", "label");
                string id = DynamicCheck.Id(candidate, "id"), label = DynamicCheck.Id(candidate, "label");
                DynamicCheck.Require(ids.Add(id), "Duplicate dynamic candidate"); values.Add(new DynamicCandidate(id, label));
            }
            DynamicCheck.Require(ids.Contains(DefaultCandidate), "Default must be a candidate"); Candidates = values.AsReadOnly();
        }
        public bool HasCandidate(string id) => Candidates.Any(c => c.Id == id);
        public string Identity => JsonConvert.SerializeObject(new[] { Kind, Owner, Key, Slot });
    }
    public sealed class DynamicCatalog
    {
        public const string Format = "portrait-studio-dynamic/1", Contract = "dynamic-display-control/1";
        public const string FileName = "portrait-studio-dynamic.json";
        public readonly string ProviderId, Revision;
        public readonly IReadOnlyList<DynamicTarget> Targets;
        internal DynamicCatalog(JObject root, string expectedId)
        {
            DynamicCheck.Fields(root, "format", "contract", "providerId", "revision", "renderMode", "targets");
            ProviderId = DynamicCheck.Id(root, "providerId"); Revision = DynamicCheck.Id(root, "revision");
            DynamicCheck.Require(ProviderId == expectedId && (string)root["format"] == Format && (string)root["contract"] == Contract
                && (string)root["renderMode"] == "dynamic", "Dynamic protocol/provider mismatch");
            var rows = root["targets"] as JArray;
            DynamicCheck.Require(rows != null && rows.Count > 0 && rows.Count <= 512, "Expected 1..512 targets");
            var values = new List<DynamicTarget>(); var ids = new HashSet<string>(StringComparer.Ordinal); var identities = new HashSet<string>(StringComparer.Ordinal);
            int total = 0;
            foreach (var token in rows)
            {
                var target = new DynamicTarget(token as JObject);
                DynamicCheck.Require(ids.Add(target.Id) && identities.Add(target.Identity), "Duplicate target or identity");
                DynamicCheck.Require(target.Owner == "game" || target.Owner == ProviderId, "Custom surfaces must be declared by their creator");
                total += target.Candidates.Count; DynamicCheck.Require(total <= 4096, "Too many candidates"); values.Add(target);
            }
            Targets = values.AsReadOnly();
        }
        public DynamicTarget Target(string id) => Targets.FirstOrDefault(t => t.Id == id) ?? throw new ArgumentException("Unknown dynamic target");
        public static DynamicCatalog Parse(string json, string expectedId)
        {
            DynamicCheck.Require(json != null && System.Text.Encoding.UTF8.GetByteCount(json) <= 1048576, "Catalog exceeds 1 MiB");
            return new DynamicCatalog(DynamicCheck.Parse(json), expectedId);
        }
        public static DynamicCatalog Read(string root, string expectedId)
        {
            var path = Path.Combine(root, FileName);
            DynamicCheck.Require(new FileInfo(path).Length <= 1048576, "Catalog exceeds 1 MiB");
            return Parse(File.ReadAllText(path), expectedId);
        }
    }
    public sealed class DynamicLayout
    {
        public readonly float Scale, X, Y;
        public static readonly DynamicLayout Default = new DynamicLayout(1, 0, 0);
        public DynamicLayout(float scale, float x, float y)
        {
            DynamicCheck.Require(!float.IsNaN(scale) && !float.IsNaN(x) && !float.IsNaN(y)
                && scale >= .25f && scale <= 3 && x >= -1 && x <= 1 && y >= -1 && y <= 1, "Invalid layout");
            Scale = scale; X = x; Y = y;
        }
        internal JObject Json() => new JObject { ["scale"] = Scale, ["x"] = X, ["y"] = Y };
        internal static DynamicLayout Read(JObject o)
        {
            DynamicCheck.Fields(o, "scale", "x", "y");
            foreach (var p in o.Properties()) DynamicCheck.Require(p.Value.Type == JTokenType.Integer || p.Value.Type == JTokenType.Float, "Numeric layout required");
            return new DynamicLayout((float)o["scale"], (float)o["x"], (float)o["y"]);
        }
    }
    public sealed class DynamicPolicy
    {
        public readonly string Mode, CandidateId;
        public readonly DynamicLayout Layout;
        public readonly bool Managed;
        internal DynamicPolicy(string mode, string candidate, DynamicLayout layout, bool managed)
        { Mode = mode; CandidateId = candidate; Layout = layout; Managed = managed; }
    }
    internal static class DynamicCheck
    {
        internal static void Require(bool condition, string message) { if (!condition) throw new ArgumentException(message); }
        internal static JObject Parse(string json) => JObject.Parse(json, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
        internal static void Fields(JObject o, params string[] fields)
        { Require(o != null && o.Properties().Count() == fields.Length && fields.All(k => o[k] != null), "Missing/unexpected fields"); }
        internal static string Id(JObject o, string key)
        {
            var t = o?[key]; Require(t?.Type == JTokenType.String, "String required: " + key);
            string value = (string)t;
            Require(!string.IsNullOrWhiteSpace(value) && value.Length <= 160 && !value.Any(char.IsControl), "Invalid ID: " + key); return value;
        }
        internal static long Sequence(JObject o, string key)
        {
            Require(o?[key]?.Type == JTokenType.Integer, "Integer required: " + key);
            long value = (long)o[key]; Require(value > 0 && value <= 9007199254740991, "Invalid sequence/epoch"); return value;
        }
    }
}
