using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace ModDevBridge;

internal sealed class RuntimeLogs
{
    private long sequence;
    private sealed class Entry { public long sequence; public string level, message, stack; }
    private readonly Queue<Entry> entries = new Queue<Entry>();
    public void Capture(string text, string stack, LogType level)
    {
        entries.Enqueue(new Entry { sequence = ++sequence, level = level.ToString(), message = text, stack = stack });
        while (entries.Count > 200) entries.Dequeue();
    }
    public object Read(long after) => Read(new JObject { ["after"] = after });
    public object Read(JObject args)
    {
        long after = (long?)args["after"] ?? 0; int limit = ObservationOutput.Limit(args);
        if (after < 0 || after > sequence) throw new BridgeFailure("log_cursor_invalid", "Cursor is outside this process log; restart with after=0", "logs.read");
        string level = (string)args["level"], contains = (string)args["contains"];
        if (level != null && !new[] { "Log", "Warning", "Error", "Assert", "Exception" }.Contains(level)) throw new ArgumentException("Unknown log level");
        bool stack = (bool?)args["include_stack"] ?? false;
        var matches = entries.Where(e => e.sequence > after && (level == null || e.level == level) && (string.IsNullOrEmpty(contains) || (e.message ?? "").IndexOf(contains, StringComparison.OrdinalIgnoreCase) >= 0)).ToArray();
        var selected = matches.Take(limit).ToArray(); bool more = matches.Length > selected.Length;
        long first = entries.Count == 0 ? sequence + 1 : entries.Peek().sequence;
        var values = new JArray(selected.Select(e => {
            var v = new JObject { ["sequence"] = e.sequence, ["level"] = e.level, ["message"] = e.message, ["stackOmitted"] = !stack && !string.IsNullOrEmpty(e.stack) };
            if (stack) v["stack"] = e.stack; return v;
        }));
        return new { cursor = more ? selected.Last().sequence : sequence, latestCursor = sequence, oldestCursor = first,
            bufferOverwritten = after < first - 1, lostEntries = Math.Max(0, first - 1 - after), hasMore = more,
            matched = matches.Length, returned = selected.Length, omitted = matches.Length - selected.Length,
            truncated = more || after < first - 1, complete = !more && after >= first - 1, stackIncluded = stack, entries = values };
    }
}
