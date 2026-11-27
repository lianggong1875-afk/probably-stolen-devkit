using System;
using System.Collections.Generic;
using System.Linq;

namespace ModDevBridge;

// A skipped restoration is terminal, but is never evidence of restored state.
public sealed class CleanupSkippedException : Exception
{
    public CleanupSkippedException(string reason) : base(reason) { }
}

public sealed class ResourceScope
{
    private sealed class Entry { public string Name; public Action Cleanup; public object Error; public bool Complete, Skipped; }
    private readonly List<Entry> entries = new List<Entry>();
    public void Track(string name, Action cleanup)
    {
        if (cleanup == null) throw new ArgumentNullException(nameof(cleanup));
        entries.Add(new Entry { Name = name, Cleanup = cleanup });
    }
    public object[] Cleanup()
    {
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            var e = entries[i]; if (e.Complete || e.Skipped) continue;
            try { e.Cleanup(); e.Complete = true; e.Error = null; }
            catch (CleanupSkippedException error) { e.Skipped = true; e.Error = new { code = "cleanup_skipped", message = error.Message }; }
            catch (Exception error) { e.Error = new { code = "cleanup_failed", message = error.Message, exceptionType = error.GetType().FullName }; }
        }
        return Report();
    }
    public bool Clean => entries.All(e => e.Complete);
    public bool Settled => entries.All(e => e.Complete || e.Skipped);
    public string Status => Clean ? "completed" : !Settled ? "partial" : "skipped";
    public object[] Report() => entries.Select(e => (object)new { name = e.Name, cleaned = e.Complete,
        state = e.Complete ? "completed" : e.Skipped ? "skipped" : e.Error != null ? "failed" : "pending", error = e.Error }).ToArray();
}
