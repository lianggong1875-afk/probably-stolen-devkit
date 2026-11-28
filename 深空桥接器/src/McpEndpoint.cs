using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ModDevBridge;

// Standard MCP Streamable HTTP, stateless JSON responses. Optional server SSE,
// prompts, sampling and task extensions are not advertised or implemented.
internal sealed class McpEndpoint
{
    private const string Version = "2025-11-25";
    private static readonly string[] Supported = { Version, "2025-06-18", "2025-03-26" };
    private readonly McpTools tools;
    private readonly McpResources resources;
    public McpEndpoint(OperationQueue queue, Func<bool> stopping, string documentationDirectory = null)
    { tools = new McpTools(queue, stopping); resources = new McpResources(documentationDirectory); }
    private static JObject Error(JToken id, int code, string message) => new JObject {
        ["jsonrpc"] = "2.0", ["id"] = id?.DeepClone() ?? JValue.CreateNull(), ["error"] = new JObject { ["code"] = code, ["message"] = message }
    };
    public void Handle(HttpListenerContext context)
    {
        var req = context.Request;
        if (req.HttpMethod != "POST")
        {
            context.Response.Headers["Allow"] = "POST";
            HttpTransport.Reply(context, 405, Error(null, -32000, "This endpoint uses JSON responses; no standalone SSE stream or transport session is provided")); return;
        }
        var accept = (req.Headers["Accept"] ?? "").Split(',').Select(v => v.Split(';')[0].Trim().ToLowerInvariant()).ToArray();
        if (!accept.Contains("application/json") || !accept.Contains("text/event-stream"))
        { HttpTransport.Reply(context, 406, Error(null, -32600, "Accept must include application/json and text/event-stream")); return; }
        if (!string.Equals((req.ContentType ?? "").Split(';')[0].Trim(), "application/json", StringComparison.OrdinalIgnoreCase))
        { HttpTransport.Reply(context, 415, Error(null, -32600, "Content-Type must be application/json")); return; }
        string version = req.Headers["MCP-Protocol-Version"] ?? "2025-03-26";
        if (!Supported.Contains(version)) { HttpTransport.Reply(context, 400, Error(null, -32600, "Unsupported MCP-Protocol-Version")); return; }
        JToken id = null;
        try
        {
            // Read a bounded body, including HTTP chunked requests.
            using var body = new MemoryStream();
            var buffer = new byte[4096]; int count;
            while ((count = req.InputStream.Read(buffer, 0, buffer.Length)) > 0)
            {
                if (body.Length + count > 65536) { HttpTransport.Reply(context, 413, Error(null, -32600, "MCP request exceeds 64 KiB")); return; }
                body.Write(buffer, 0, count);
            }
            JToken value;
            try
            {
                using var json = new JsonTextReader(new StringReader(new UTF8Encoding(false, true).GetString(body.ToArray()))) { MaxDepth = 64, DateParseHandling = DateParseHandling.None };
                value = JToken.ReadFrom(json);
                if (json.Read()) throw new JsonReaderException("More than one JSON value");
            }
            catch (Exception error) when (error is JsonException || error is DecoderFallbackException)
            { HttpTransport.Reply(context, 400, Error(null, -32700, "Invalid JSON")); return; }
            if (!(value is JObject message)) { HttpTransport.Reply(context, 400, Error(null, -32600, "Expected a single JSON-RPC object; batches are not supported")); return; }
            id = message["id"];
            if (message["jsonrpc"]?.Type != JTokenType.String || (string)message["jsonrpc"] != "2.0" || message["method"]?.Type != JTokenType.String ||
                id != null && id.Type != JTokenType.Integer && id.Type != JTokenType.String ||
                message["params"] != null && !(message["params"] is JObject))
            { HttpTransport.Reply(context, 400, Error(null, -32600, "Invalid JSON-RPC request")); return; }
            string method = (string)message["method"];
            if (id == null)
            {
                context.Response.StatusCode = 202; context.Response.ContentLength64 = 0; return;
            }
            var args = message["params"] as JObject ?? new JObject();
            JObject result;
            switch (method)
            {
                case "initialize":
                    if (args["protocolVersion"]?.Type != JTokenType.String || !(args["capabilities"] is JObject) || !(args["clientInfo"] is JObject))
                        throw new ArgumentException("protocolVersion, capabilities and clientInfo are required");
                    result = new JObject {
                        ["protocolVersion"] = Supported.Contains((string)args["protocolVersion"]) ? (string)args["protocolVersion"] : Version,
                        ["capabilities"] = new JObject { ["tools"] = new JObject { ["listChanged"] = false }, ["resources"] = new JObject() },
                        ["serverInfo"] = new JObject { ["name"] = "ModDevBridgeNext", ["version"] = ReleaseInfo.Version },
                        ["_meta"] = new JObject { ["releaseId"] = ReleaseInfo.Id, ["edition"] = ReleaseInfo.Edition, ["dynamicModules"] = ReleaseInfo.DynamicModules },
                        ["instructions"] = "Game-hosted MCP. Read SKILL.md beside the Bridge DLL, also available at moddevbridge://guides/skill. get_development_environment reports documentationDirectory and entry paths. Calls run on the Unity main thread. Pending/unknown retains operationId; query it before deciding on another write. Cleanup is not a complete rollback."
                    }; break;
                case "ping": result = new JObject(); break;
                case "resources/list":
                    if (args["cursor"] != null) throw new ArgumentException("No pagination cursor is issued for this bounded resource list");
                    result = resources.List(); break;
                case "resources/templates/list": result = new JObject { ["resourceTemplates"] = new JArray() }; break;
                case "resources/read":
                    if (args["uri"]?.Type != JTokenType.String) throw new ArgumentException("Resource uri must be a string");
                    result = resources.Read((string)args["uri"]);
                    if (result == null) { HttpTransport.Reply(context, 200, Error(id, -32002, "Resource not found: " + (string)args["uri"])); return; }
                    break;
                case "tools/list":
                    if (args["cursor"] != null) throw new ArgumentException("No pagination cursor is issued for this bounded tool list");
                    result = tools.List(); break;
                case "tools/call":
                    if (args["name"]?.Type != JTokenType.String) throw new ArgumentException("Tool name must be a string");
                    string name = (string)args["name"];
                    if (!tools.Contains(name)) throw new ArgumentException("Unknown tool: " + name);
                    if (args["arguments"] != null && !(args["arguments"] is JObject)) throw new ArgumentException("arguments must be an object");
                    result = tools.Call(name, args["arguments"] as JObject ?? new JObject()); break;
                default: HttpTransport.Reply(context, 200, Error(id, -32601, "Method not found")); return;
            }
            HttpTransport.Reply(context, 200, new JObject { ["jsonrpc"] = "2.0", ["id"] = id.DeepClone(), ["result"] = result });
        }
        catch (FileNotFoundException error) { HttpTransport.Reply(context, 200, Error(id, -32002, error.Message)); }
        catch (ArgumentException error) { HttpTransport.Reply(context, 200, Error(id, -32602, error.Message)); }
        catch (Exception error) { HttpTransport.Reply(context, 200, Error(id, -32603, error.Message)); }
    }
}
