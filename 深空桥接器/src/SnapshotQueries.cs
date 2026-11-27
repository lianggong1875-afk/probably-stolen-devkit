using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
namespace ModDevBridge;

// Eight immutable short-lived observations, no persistence or event history.
internal sealed class SnapshotQueries
{
    internal static readonly string[] LegacyFields = { "slot", "day", "cash", "inventory", "contacts", "mods", "modData" };
    internal static readonly string[] AllFields = LegacyFields.Concat(new[] { "store_state","professions","perks","finance","inspection" }).ToArray();
    internal static readonly string[] DefaultFields = LegacyFields.Where(x => x != "modData").ToArray();
    private sealed class Entry { public string Id, World; public DateTime Captured; public JObject Data; }
    readonly List<Entry> entries = new List<Entry>();
    readonly Func<DateTime> clock;
    public SnapshotQueries(Func<DateTime> clock = null) { this.clock = clock ?? (() => DateTime.UtcNow); }
    public static string[] Fields(JObject args)
    {
        if (args?["fields"] == null) return DefaultFields;
        var fields = (args["fields"] as JArray ?? throw new ArgumentException("fields must be an array")).Values<string>().Distinct().ToArray();
        if (fields.Length == 0 || fields.Any(f => !AllFields.Contains(f))) throw new ArgumentException("Select at least one of: " + string.Join(", ", AllFields));
        return fields;
    }
    Entry Find(string id, string world)
    {
        var e = entries.FirstOrDefault(x => x.Id == id);
        if (e == null || clock() - e.Captured > TimeSpan.FromMinutes(10)) throw new BridgeFailure("snapshot_unavailable", "Snapshot expired, was evicted, or belongs to another process; capture a new snapshot", "snapshot.lookup");
        if (e.World != world) throw new BridgeFailure("snapshot_world_mismatch", "Snapshots from different worlds cannot be compared or reused", "snapshot.lookup");
        return e;
    }
    Entry Capture(string world, JObject data)
    {
        entries.RemoveAll(e => clock() - e.Captured > TimeSpan.FromMinutes(10));
        var e = new Entry { Id = Guid.NewGuid().ToString("N"), World = world, Captured = clock(), Data = (JObject)data.DeepClone() };
        entries.Add(e); while (entries.Count > 8) entries.RemoveAt(0); return e;
    }
    public JObject Read(string world, JObject args, Func<string[], JObject> capture)
    {
        int limit = ObservationOutput.Limit(args), offset = ObservationOutput.Offset(args); string id = (string)args["snapshot_id"];
        if (id != null && args["fields"] != null) throw new ArgumentException("Use fields when capturing; cached snapshot fields are fixed");
        return View(id == null ? Capture(world, capture(Fields(args))) : Find(id, world), offset, limit);
    }
    static JObject View(Entry e, int offset, int limit)
    {
        var data = new JObject(); var collections = new JObject(); bool complete = true;
        foreach (var p in e.Data.Properties())
        {
            if (p.Value is JObject obj)
            {
                var props = obj.Properties().OrderBy(x => x.Name, StringComparer.Ordinal).ToArray();
                data[p.Name] = new JObject(props.Skip(offset).Take(limit).Select(x => new JProperty(x.Name, x.Value.DeepClone())));
                var page = ObservationOutput.Page(props.Select(x => (JToken)new JValue(x.Name)), offset, limit); page.Remove("entries");
                collections[p.Name] = page; complete &= (bool)page["complete"];
            }
            else if (p.Value is JArray array)
            {
                var page = ObservationOutput.Page(array, offset, limit); data[p.Name] = page["entries"]; page.Remove("entries");
                collections[p.Name] = page; complete &= (bool)page["complete"];
            }
            else data[p.Name] = p.Value.DeepClone();
        }
        var selected = e.Data.Properties().Select(p => p.Name).ToArray();
        return new JObject { ["snapshotId"] = e.Id, ["worldId"] = e.World, ["capturedAt"] = e.Captured.ToString("o"),
            ["fields"] = new JArray(selected), ["omittedFields"] = new JArray(AllFields.Except(selected)), ["data"] = data,
            ["collections"] = collections, ["complete"] = complete, ["truncated"] = !complete,
            ["expiresAfterSeconds"] = 600, ["verification"] = "selected_fields_only" };
    }
    public JObject Diff(string world, JObject args, Func<string[], JObject> capture)
    {
        int limit = ObservationOutput.Limit(args), offset = ObservationOutput.Offset(args);
        var before = Find((string)args["before_id"], world);
        var fields = args["fields"] == null ? before.Data.Properties().Select(p => p.Name).ToArray() : Fields(args);
        if (fields.Any(f => before.Data.Property(f) == null)) throw new BridgeFailure("snapshot_fields_missing", "Before snapshot did not capture all requested fields", "snapshot.diff");
        var after = args["after_id"] == null ? Capture(world, capture(fields)) : Find((string)args["after_id"], world);
        if (fields.Any(f => after.Data.Property(f) == null)) throw new BridgeFailure("snapshot_fields_missing", "After snapshot did not capture all requested fields", "snapshot.diff");
        var changes = new List<JToken>(); foreach (var field in fields) Changes(before.Data[field], after.Data[field], new[] { field }, changes);
        var result = ObservationOutput.Page(changes, offset, limit); result["changes"] = result["entries"]; result.Remove("entries");
        result["beforeId"] = before.Id; result["afterId"] = after.Id; result["worldId"] = world;
        result["fields"] = new JArray(fields); result["omittedFields"] = new JArray(AllFields.Except(fields));
        result["changed"] = changes.Count != 0; result["verification"] = "selected_fields_only"; return result;
    }
    static void Changes(JToken before, JToken after, string[] path, List<JToken> changes)
    {
        if (JToken.DeepEquals(before, after)) return;
        if (before is JObject b && after is JObject a)
        {
            foreach (var key in b.Properties().Select(p => p.Name).Union(a.Properties().Select(p => p.Name)).OrderBy(k => k, StringComparer.Ordinal)) Changes(b[key], a[key], path.Concat(new[] { key }).ToArray(), changes);
            return;
        }
        changes.Add(new JObject { ["path"] = new JArray(path), ["kind"] = before == null ? "added" : after == null ? "removed" : "modified",
            ["hasBefore"] = before != null, ["hasAfter"] = after != null, ["before"] = before?.DeepClone(), ["after"] = after?.DeepClone() });
    }
    public static JObject Summary(JObject data) => new JObject(data.Properties().Select(p => new JProperty(p.Name, p.Value is JContainer c ? (JToken)new JObject { ["count"] = c.Count } : p.Value.DeepClone())));
    public static JObject SessionDiff(JObject before, JObject after, bool sameWorld)
    {
        if (!sameWorld || before == null || after == null) return new JObject { ["comparable"] = false, ["reason"] = sameWorld ? "snapshot_unavailable" : "world_changed" };
        var changes = new List<JToken>(); Changes(before, after, Array.Empty<string>(), changes);
        var page = ObservationOutput.Page(changes, 0, 50); page["changes"] = page["entries"]; page.Remove("entries");
        page["comparable"] = true; page["changed"] = changes.Count > 0; page["verification"] = "selected_fields_only"; return page;
    }
}
