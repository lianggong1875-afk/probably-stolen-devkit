using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ModDevBridge;

public sealed class HttpTransport : IDisposable
{
    private HttpListener listener;
    private readonly OperationQueue queue;
    public string InstanceId { get; } = Guid.NewGuid().ToString("N");
    public string Url { get; private set; }
    private readonly int port;
    public Func<JObject> DistributionStatus;
    private volatile bool stopped;
    private readonly Func<Action<CancellationToken>, bool> runInBackground;
    private readonly object gate = new object();
    private readonly HashSet<HttpListenerContext> contexts = new HashSet<HttpListenerContext>();
    private readonly McpEndpoint mcp;
    public HttpTransport(OperationQueue queue, int port, Func<Action<CancellationToken>, bool> runInBackground, string documentationDirectory = null)
    {
        this.queue = queue;
        this.runInBackground = runInBackground ?? throw new ArgumentNullException(nameof(runInBackground));
        mcp = new McpEndpoint(queue, () => stopped, documentationDirectory);
        if (port < 1 || port > 65535) throw new ArgumentOutOfRangeException(nameof(port));
        this.port = port;
    }
    public void Start()
    {
        Url = "http://127.0.0.1:" + port;
        listener = new HttpListener();
        listener.Prefixes.Add(Url + "/");
        try { listener.Start(); }
        catch (HttpListenerException error)
        {
            listener.Close();
            throw new BridgeFailure("http_bind_failed", "Could not listen at " + Url + ": " + error.Message,
                "http.start", new { requestedUrl=Url, port, nativeErrorCode=error.NativeErrorCode },
                "The configured endpoint was not changed. Check a live Bridge at this URL, game startup logs, port occupancy and permissions. Choose an explicit port only if needed, then update the host once.", error);
        }
        try
        {
            if (!runInBackground(Listen)) throw new InvalidOperationException("The game refused background work for this MOD");
        }
        catch { StopListening(); throw; }
    }
    private void Listen(CancellationToken cancellation)
    {
        using var registration = cancellation.Register(StopListening);
        while (!stopped && !cancellation.IsCancellationRequested)
        {
            try
            {
                var context = listener.GetContext();
                lock (gate)
                {
                    if (stopped) { context.Response.Abort(); return; }
                    contexts.Add(context);
                }
                try
                {
                    if (!runInBackground(token => {
                        using var cancelled = token.Register(() => Abort(context));
                        if (!stopped && !token.IsCancellationRequested) Handle(context);
                        else Close(context);
                    })) Close(context);
                }
                catch { Close(context); if (!stopped) throw; }
            }
            catch (HttpListenerException) { if (!stopped) Thread.Sleep(50); }
            catch (ObjectDisposedException) { return; }
        }
    }
    private void Handle(HttpListenerContext context)
    {
        try
        {
            var req = context.Request;
            if (!IPAddress.IsLoopback(req.RemoteEndPoint.Address) || req.Url.Host != "127.0.0.1" || req.Headers["Origin"] != null)
            { Reply(context, 403, new { error = "Forbidden" }); return; }
            string path = req.Url.AbsolutePath;
            if (path == "/bridge/status" && req.HttpMethod == "GET")
            {
                Reply(context, 200, DistributionStatus?.Invoke() ?? new JObject {
                    ["instanceId"] = InstanceId, ["releaseId"] = ReleaseInfo.Id, ["bridgeVersion"] = ReleaseInfo.Version,
                    ["transportAlive"] = true, ["gameReadiness"] = "call_get_game_status"
                }); return;
            }
            if (path == "/mcp") { mcp.Handle(context); return; }
            if (req.Headers["X-Bridge-Instance"] != InstanceId)
            { Reply(context, 409, new { error = "Game process changed; reconnect before submitting commands" }); return; }
            if (req.HttpMethod == "GET" && path == "/v1/health")
            { Reply(context, 200, new { protocolVersion = 1, instanceId = InstanceId, transport = "http", operations = queue.Recent() }); return; }
            if (req.HttpMethod == "POST" && path == "/v1/operations")
            {
                if (req.ContentLength64 < 0 || req.ContentLength64 > 65536) throw new ArgumentException("A body up to 64 KiB with Content-Length is required");
                using (var reader = new StreamReader(req.InputStream, Encoding.UTF8))
                    Reply(context, 202, queue.Submit(JsonConvert.DeserializeObject<BridgeRequest>(reader.ReadToEnd())));
                return;
            }
            const string prefix = "/v1/operations/";
            if (path.StartsWith(prefix, StringComparison.Ordinal))
            {
                string id = path.Substring(prefix.Length);
                if (req.HttpMethod == "GET") { var op = queue.Get(id, req.QueryString["includeDetails"] == "true"); Reply(context, op == null ? 404 : 200, (object)op ?? new { error = "Unknown operation" }); return; }
                if (req.HttpMethod == "DELETE") { Reply(context, 200, queue.Cancel(id)); return; }
            }
            Reply(context, 404, new { error = "Unknown route" });
        }
        catch (Exception e) { try { Reply(context, 400, new { error = e.Message }); } catch { } }
        finally { Close(context); }
    }
    private void Close(HttpListenerContext context)
    {
        lock (gate) contexts.Remove(context);
        try { context.Response.Close(); } catch { }
    }
    private void Abort(HttpListenerContext context)
    {
        lock (gate) contexts.Remove(context);
        try { context.Response.Abort(); } catch { }
        try { context.Request.InputStream.Close(); } catch { }
    }
    private void StopListening()
    {
        HttpListenerContext[] active;
        lock (gate)
        {
            stopped = true;
            active = new HttpListenerContext[contexts.Count]; contexts.CopyTo(active); contexts.Clear();
        }
        try { listener?.Close(); } catch { }
        foreach (var context in active) Abort(context);
    }
    internal static void Reply(HttpListenerContext context, int code, object data)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(data));
        context.Response.StatusCode = code;
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.ContentLength64 = bytes.Length;
        context.Response.OutputStream.Write(bytes, 0, bytes.Length);
    }
    public void Dispose()
    {
        StopListening();
    }
}
