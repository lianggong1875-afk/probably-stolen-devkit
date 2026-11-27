using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace ModDevBridge;

// The game ships its own debug action table: CustomUIDebugPanel keeps a private
// static List<Entry> where Entry is { category, name, Action }. Driving that table
// is safer than hand-rolling effects, because every entry is a call the game's own
// debug panel already makes. entries and Entry are private, so this reads them by
// reflection; nothing here writes tags or invents behaviour.
// Note for MOD authors: CustomUIDebugPanel.Add/Remove are public static, so a MOD
// can contribute or withdraw its own entries. This tool only lists and invokes.
internal static class DebugActions
{
    private const BindingFlags Statics = BindingFlags.Static | BindingFlags.NonPublic;
    private const BindingFlags EntryFields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private static List<object> Entries()
    {
        try
        {
            var field = typeof(CustomUIDebugPanel).GetField("entries", Statics);
            if (field?.GetValue(null) is IEnumerable list)
                return list.Cast<object>().Where(e => e != null).ToList();
        }
        catch { }
        return new List<object>();
    }

    private static string Text(object entry, string field)
    {
        try { return entry?.GetType().GetField(field, EntryFields)?.GetValue(entry)?.ToString(); }
        catch { return null; }
    }

    private static Action Delegate(object entry)
    {
        try { return entry?.GetType().GetField("action", EntryFields)?.GetValue(entry) as Action; }
        catch { return null; }
    }

    public static object List(JObject a)
    {
        string category = (string)a["category"];
        string contains = (string)a["contains"];
        var rows = new JArray();
        var categories = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var entry in Entries())
        {
            string cat = Text(entry, "category");
            string name = Text(entry, "name");
            if (!string.IsNullOrEmpty(category) && !string.Equals(cat, category, StringComparison.OrdinalIgnoreCase)) continue;
            if (!string.IsNullOrEmpty(contains) && (name ?? "").IndexOf(contains, StringComparison.OrdinalIgnoreCase) < 0) continue;
            if (cat != null) categories.Add(cat);
            rows.Add(new JObject { ["category"] = cat, ["name"] = name, ["invokable"] = Delegate(entry) != null });
        }
        return new JObject {
            ["count"] = rows.Count, ["source"] = "CustomUIDebugPanel.entries",
            ["categories"] = new JArray(categories.Cast<object>().ToArray()), ["actions"] = rows
        };
    }

    public static object Invoke(JObject a)
    {
        string name = (string)a["name"];
        string category = (string)a["category"];
        if (string.IsNullOrEmpty(name)) throw new ArgumentException("name is required; list them with debug_actions first");
        int seen = 0;
        foreach (var entry in Entries())
        {
            if (!string.Equals(Text(entry, "name"), name, StringComparison.OrdinalIgnoreCase)) continue;
            if (!string.IsNullOrEmpty(category) && !string.Equals(Text(entry, "category"), category, StringComparison.OrdinalIgnoreCase)) continue;
            seen++;
            var action = Delegate(entry);
            if (action == null) continue;
            action();
            return new JObject {
                ["invoked"] = name, ["category"] = Text(entry, "category"),
                ["native"] = "CustomUIDebugPanel", ["matched"] = seen
            };
        }
        throw new KeyNotFoundException(seen == 0
            ? "Debug action '" + name + "' was not found; list them with debug_actions first"
            : "Debug action '" + name + "' exists but has no callable delegate");
    }
}
