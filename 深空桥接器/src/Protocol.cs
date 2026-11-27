using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace ModDevBridge;

public sealed class BridgeFailure : Exception
{
    public readonly string Code, Stage, Hint;
    public readonly object PartialResult;
    public BridgeFailure(string code, string message, string stage = null, object result = null, string hint = null, Exception inner = null)
        : base(message, inner) { Code = code; Stage = stage; PartialResult = result; Hint = hint; }
    public static JObject Describe(Exception error, string stage = null)
    {
        var known = error as BridgeFailure;
        return JObject.FromObject(new {
            code = known?.Code ?? (error is ArgumentException ? "invalid_argument" : error is TimeoutException ? "condition_timeout" : "execution_failed"),
            message = error.Message, stage = known?.Stage ?? stage, hint = known?.Hint ?? "Inspect the original operation and partial result before deciding on a new request.",
            exceptionType = (error.InnerException ?? error).GetType().FullName
        });
    }
}

public sealed class BridgeRequest
{
    public string id;
    public string command;
    public string sessionId;
    public string worldId;
    public long expiresAt;
    public JObject arguments = new JObject();
}

public sealed class BridgeOperation
{
    public string id;
    public string command;
    public string state = "queued";
    public object result;
    public object progress;
    public JObject error;
    public long submittedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public long? startedAt, completedAt;
    public bool cancelRequested;
    internal BridgeRequest Request;
    internal string Fingerprint;
    internal string Diagnostics;
    public bool Terminal => state == "succeeded" || state == "failed" || state == "cancelled";
    public JObject Snapshot(bool includeDetails = false)
    {
        var value = JObject.FromObject(new {
            id, command, state, result, progress, error, submittedAt, startedAt, completedAt, cancelRequested,
            sessionId = Request?.sessionId, worldId = Request?.worldId
        });
        if (includeDetails) value["diagnostics"] = Diagnostics;
        return value;
    }
}

// Only queue bookkeeping runs on HTTP threads. All handlers and routines run in Tick.
public sealed class OperationQueue
{
    private readonly object gate = new object();
    private readonly Queue<BridgeOperation> pending = new Queue<BridgeOperation>();
    private readonly Dictionary<string, BridgeOperation> operations = new Dictionary<string, BridgeOperation>();
    private readonly Func<BridgeRequest, object> execute;
    private readonly Func<string> world;
    private BridgeOperation active;
    private IEnumerator routine;
    public object RoutineResult { get; set; }
    public OperationQueue(Func<BridgeRequest, object> execute, Func<string> world) { this.execute = execute; this.world = world; }

    public JObject Submit(BridgeRequest request)
    {
        if (request == null || string.IsNullOrEmpty(request.id) || !Guid.TryParse(request.id, out _) || string.IsNullOrEmpty(request.command))
            throw new ArgumentException("id must be a UUID and command must be specified");
        string fingerprint = JObject.FromObject(new { request.command, request.sessionId, request.worldId, request.arguments }).ToString();
        lock (gate)
        {
            if (operations.TryGetValue(request.id, out var previous))
            {
                if (previous.Fingerprint != fingerprint) throw new InvalidOperationException("Request ID reused with different arguments");
                return previous.Snapshot();
            }
            // Do not evict IDs and inadvertently permit an old write to execute again.
            if (operations.Count >= 4096 || pending.Count >= 128) throw new InvalidOperationException("Bridge operation capacity reached; finish work and restart the bridge");
            var op = new BridgeOperation { id = request.id, command = request.command, Request = request, Fingerprint = fingerprint };
            operations.Add(op.id, op); pending.Enqueue(op);
            return op.Snapshot();
        }
    }
    public JObject Get(string id, bool includeDetails = false) { lock (gate) return operations.TryGetValue(id, out var op) ? op.Snapshot(includeDetails) : null; }
    public JObject Cancel(string id)
    {
        lock (gate)
        {
            if (!operations.TryGetValue(id, out var op)) throw new ArgumentException("Unknown operation");
            if (!op.Terminal) op.cancelRequested = true;
            return op.Snapshot();
        }
    }
    public JObject[] Recent(int limit = 30) { lock (gate) return operations.Values.OrderByDescending(o => o.submittedAt).Take(limit).Select(o => o.Snapshot()).ToArray(); }
    public void ReportProgress(object value) { lock (gate) { if (active != null) active.progress = value; } }
    public void Tick()
    {
        lock (gate)
        {
            if (active == null)
            {
                if (pending.Count == 0) return;
                active = pending.Dequeue();
                RoutineResult = null;
            }
        }
        try
        {
            bool cancelled;
            lock (gate) cancelled = active.cancelRequested;
            if (cancelled) { Finish("cancelled", RoutineResult, BridgeFailure.Describe(new BridgeFailure("operation_cancelled", "Cancelled at a main-thread checkpoint; inspect session cleanup separately", "checkpoint"))); return; }
            if (!string.IsNullOrEmpty(active.Request.worldId) && active.Request.worldId != world())
                throw new BridgeFailure("world_changed", "World changed; inspect status and open a new test session", "checkpoint");
            if (active.state == "queued")
            {
                if (active.Request.expiresAt <= DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()) throw new BridgeFailure("queue_expired", "Request expired before execution; no handler was invoked", "queue");
                lock (gate) { active.state = "running"; active.startedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(); }
                RoutineResult = null;
                var value = execute(active.Request);
                routine = value as IEnumerator;
                if (routine == null) { Finish("succeeded", value, null); return; }
            }
            if (!routine.MoveNext()) Finish("succeeded", RoutineResult, null);
        }
        catch (Exception error)
        {
            object partial = (error as BridgeFailure)?.PartialResult;
            object result = RoutineResult ?? partial;
            if (RoutineResult != null && partial != null && !ReferenceEquals(RoutineResult, partial))
            {
                if (RoutineResult is JObject report) report["failureResult"] = JToken.FromObject(partial);
                else result = new { completed = RoutineResult, failureResult = partial };
            }
            Finish("failed", result, BridgeFailure.Describe(error, "execute"), error.ToString());
        }
    }
    private void Finish(string state, object result, JObject error, string diagnostics = null)
    {
        try { (routine as IDisposable)?.Dispose(); }
        catch (Exception e) {
            state = "failed"; error = error ?? BridgeFailure.Describe(new BridgeFailure("routine_cleanup_failed", "Routine disposal failed", "dispose"));
            error["cleanupError"] = BridgeFailure.Describe(e, "dispose"); diagnostics = (diagnostics ?? "") + "\nRoutine disposal: " + e;
        }
        lock (gate)
        {
            active.state = state; active.result = result; active.error = error; active.Diagnostics = diagnostics;
            active.completedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            routine = null; active = null;
        }
    }
    public void Shutdown()
    {
        lock (gate)
        {
            if (active != null) Finish("cancelled", RoutineResult, BridgeFailure.Describe(new BridgeFailure("bridge_shutdown", "Bridge shutting down", "shutdown")));
            while (pending.Count > 0) { var p = pending.Dequeue(); p.state = "cancelled"; p.error = BridgeFailure.Describe(new BridgeFailure("bridge_shutdown", "Bridge shutting down before execution", "queue")); p.completedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(); }
        }
    }
}
