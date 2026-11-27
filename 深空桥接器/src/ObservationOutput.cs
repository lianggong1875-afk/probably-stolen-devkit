using System;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
namespace ModDevBridge;

// Limits apply to responses; assertions and retained operations use original data.
internal static class ObservationOutput
{
    public static int Limit(JObject a) { int n = (int?)a?["limit"] ?? 50; if (n < 1 || n > 200) throw new ArgumentException("limit must be 1..200"); return n; }
    public static int Offset(JObject a) { int n = (int?)a?["offset"] ?? 0; if (n < 0) throw new ArgumentException("offset must be nonnegative"); return n; }
    public static JObject Page(IEnumerable<JToken> source, int offset, int limit)
    {
        var all = source.ToArray(); var entries = new JArray(all.Skip(offset).Take(limit).Select(v => v.DeepClone()));
        return new JObject { ["entries"] = entries, ["total"] = all.Length, ["offset"] = offset, ["limit"] = limit,
            ["returned"] = entries.Count, ["omitted"] = all.Length - entries.Count,
            ["nextOffset"] = (long)offset + entries.Count < all.Length ? new JValue(offset + entries.Count) : JValue.CreateNull(),
            ["truncated"] = entries.Count < all.Length, ["complete"] = entries.Count == all.Length };
    }
    public static JToken Slice(JToken value, int offset, int limit)
    {
        if (value is JArray a) return Page(a, offset, limit);
        if (value is JObject o) return Page(o.Properties().Select(p => (JToken)new JObject { ["key"] = p.Name, ["value"] = p.Value.DeepClone() }), offset, limit);
        if (value?.Type == JTokenType.String)
        {
            string text = (string)value; int start = Math.Min(offset, text.Length), count = Math.Min(limit, text.Length - start);
            return new JObject { ["text"] = text.Substring(start, count), ["totalCharacters"] = text.Length,
                ["offset"] = offset, ["nextOffset"] = start + count < text.Length ? new JValue(start + count) : JValue.CreateNull(), ["truncated"] = count < text.Length };
        }
        return value?.DeepClone() ?? JValue.CreateNull();
    }
    public static JObject Bound(JObject payload)
    {
        var b = new Budget(); var result = (JObject)payload.DeepClone();
        foreach (string key in new[] { "error", "result" }) if (result[key] != null) result[key] = b.Copy(result[key], key, 0);
        result["output"] = new JObject { ["truncated"] = b.Omissions > 0, ["omissionCount"] = b.Omissions,
            ["details"] = b.Details, ["detailsOmitted"] = Math.Max(0, b.Omissions - b.Details.Count),
            ["limits"] = new JObject { ["textCharacters"] = 2000, ["collectionEntries"] = 200, ["depth"] = 12, ["nodes"] = 4000, ["characters"] = 24000 },
            ["hint"] = "Use filters, snapshot pages, or get_operation result_path with offset/limit for omitted data. Truncated observations are incomplete." };
        return result;
    }
    private sealed class Budget
    {
        int nodes = 4000, characters = 24000;
        public int Omissions;
        public readonly JArray Details = new JArray();
        void Omit(string path, string reason, int omitted)
        {
            Omissions++; if (Details.Count < 32) Details.Add(new JObject { ["path"] = path.Length > 256 ? path.Substring(0, 256) : path, ["reason"] = reason, ["omitted"] = omitted });
        }
        public JToken Copy(JToken token, string path, int depth)
        {
            if (nodes-- <= 0 || characters <= 0 || depth > 12) { Omit(path, "response_budget", 1); return JValue.CreateNull(); }
            characters -= 8;
            if (token.Type == JTokenType.String)
            {
                string text = (string)token;
                if (text == null) return JValue.CreateNull();
                int take = Math.Min(text.Length, Math.Max(0, Math.Min(2000, characters)));
                if (take > 0 && take < text.Length && char.IsHighSurrogate(text[take - 1])) take--;
                characters -= take; if (take < text.Length) Omit(path, "text", text.Length - take);
                return new JValue(text.Substring(0, take));
            }
            if (token is JObject obj)
            {
                var copy = new JObject(); int count = 0;
                foreach (var p in obj.Properties())
                {
                    if (count >= 200 || nodes <= 0 || characters <= p.Name.Length) { Omit(path, "properties", obj.Count - count); break; }
                    if (p.Name.Length > 2000) { Omit(path, "property_name", 1); count++; continue; }
                    characters -= p.Name.Length; count++;
                    copy[p.Name] = Copy(p.Value, path + "/" + p.Name.Replace("~", "~0").Replace("/", "~1"), depth + 1);
                }
                return copy;
            }
            if (token is JArray array)
            {
                var copy = new JArray();
                foreach (var child in array)
                {
                    if (copy.Count >= 200 || nodes <= 0 || characters <= 0) { Omit(path, "entries", array.Count - copy.Count); break; }
                    copy.Add(Copy(child, path + "/" + copy.Count, depth + 1));
                }
                return copy;
            }
            return token.DeepClone();
        }
    }
}
