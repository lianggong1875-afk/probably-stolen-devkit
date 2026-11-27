# Connection diagnostics

Read this reference when discovery, host configuration or a request fails. Normal setup is in [AI_GUIDE.md](../AI_GUIDE.md).

## Verify endpoint candidates

Default data root: %LOCALAPPDATA%\ModDevBridgeNext. Canonical descriptors: .runtime\instances\*.json. connection.json identifies the last-published instance; enumerate all descriptors when multiple games run. MODDEVBRIDGE_NEXT_ROOT overrides the data root. Default port 18774 is fixed; MODDEVBRIDGE_NEXT_PORT explicitly selects another fixed port. Binding failure stops startup with http_bind_failed, requestedUrl and nativeErrorCode; no random fallback occurs.

This read-only PowerShell example lists and verifies candidates; it changes no configuration:

```powershell
$bridgeRoot = if ($env:MODDEVBRIDGE_NEXT_ROOT) { $env:MODDEVBRIDGE_NEXT_ROOT } else { Join-Path $env:LOCALAPPDATA 'ModDevBridgeNext' }
$bridgeRuntime = Join-Path $bridgeRoot '.runtime'
$bridgeFiles = @(Get-ChildItem -LiteralPath (Join-Path $bridgeRuntime 'instances') -Filter '*.json' -File -ErrorAction SilentlyContinue)
if (!$bridgeFiles.Count) {
    $bridgeHint = Join-Path $bridgeRuntime 'connection.json'
    if (Test-Path -LiteralPath $bridgeHint) { $bridgeFiles = @(Get-Item -LiteralPath $bridgeHint) }
}
if (!$bridgeFiles.Count) { Write-Output 'NO_DESCRIPTOR: check game, MOD enable/approval/restart, data-root override and Player.log' }
foreach ($bridgeFile in $bridgeFiles) {
    $bridgeResult = [ordered]@{ descriptor = $bridgeFile.FullName; state = 'invalid_descriptor' }
    try {
        $bridgeDescription = Get-Content -LiteralPath $bridgeFile.FullName -Raw | ConvertFrom-Json
        $bridgeUrl = [uri]$bridgeDescription.mcpUrl
        if (!$bridgeUrl.IsAbsoluteUri -or $bridgeUrl.Scheme -ne 'http' -or $bridgeUrl.Host -ne '127.0.0.1' -or $bridgeUrl.AbsolutePath -ne '/mcp' -or $bridgeUrl.UserInfo -or $bridgeUrl.Query -or $bridgeUrl.Fragment) { throw 'Invalid loopback MCP URL' }
        $bridgeResult.state = 'unreachable_or_stale'
        $bridgeLive = Invoke-RestMethod -Uri ($bridgeUrl.GetLeftPart([System.UriPartial]::Authority) + '/bridge/status') -TimeoutSec 3
        if ($bridgeLive.instanceId -ne $bridgeDescription.instanceId -or $bridgeLive.releaseId -ne $bridgeDescription.releaseId -or $bridgeLive.bridgeStartedAtUtc -ne $bridgeDescription.bridgeStartedAtUtc) { throw 'Endpoint instance identity mismatch; rediscover' }
        $bridgeResult.state = 'live'; $bridgeResult.mcpUrl = $bridgeDescription.mcpUrl
        $bridgeResult.instanceId = $bridgeLive.instanceId; $bridgeResult.releaseId = $bridgeLive.releaseId
        $bridgeResult.assemblyPath = $bridgeLive.assemblyPath; $bridgeResult.disk = $bridgeLive.disk
    } catch { $bridgeResult.error = $_.Exception.Message }
    [pscustomobject]$bridgeResult | ConvertTo-Json -Depth 6 -Compress
}
```

Only live candidates qualify. If several are live, select explicitly using installation, bridge start time and the player's intended game. Keep the selected instanceId with experiment state. Re-read descriptors after restart: ports can be reused. Do not replay old-instance operations into a new process.

GET /bridge/status checks transport/identity and current disk release without Unity calls. It does not establish save readiness. disk.restartRequired means the file changed or its release differs: restart, then rediscover. Startup failures, when writable, appear under .runtime/failures/ as historical records, not liveness evidence. Inspect Unity Player.log for loading, capability and access errors; locate it under %USERPROFILE%\AppData\LocalLow when Bridge cannot start. Service binds only 127.0.0.1 and rejects browser Origin requests.

## Raw MCP requests

POST mcpUrl with Content-Type application/json and Accept application/json, text/event-stream. initialize takes protocolVersion 2025-11-25, capabilities {} and clientInfo name/version; send notifications/initialized, then MCP-Protocol-Version on subsequent requests. Also supported: 2025-06-18 and 2025-03-26. Responses use JSON; standalone SSE GET and DELETE return 405 by design.

## Recover a host that remains failed

Check the game's diagnosticUrl first. If it does not answer, diagnose game startup rather than reloading host configuration. An initial connection attempted before the game started may remain failed and need an explicit connect action. An already connected host may keep working after a game restart because Bridge uses stateless HTTP requests: first try get_game_status directly and verify the new instance/world. Reconnect only if the actual call fails or the host retains stale state. The Agent triggers recovery when needed.

OpenCode exposes an explicit connection action through its MCP management UI (`/mcps` where supported). An Agent with access to the existing OpenCode server can use its management API instead, avoiding manual configuration edits:

1. Use the host's existing server URL, project location and authentication. Obtain them from that host's connection information, runtime environment or launch parameters. Do not assume port 4096 or start a second service as a substitute for the actual host.
2. Read the existing server's `/openapi.json` and inspect its MCP operations; versions differ. The API tested with OpenCode Desktop 2.2.5 / bundled CLI 1.4.4 provides `GET /api/mcp` and `POST /api/experimental/mcp/{server}/connect`. Project scope uses the `location[directory]` query parameter. Match the configured server name and the current project, not another project's MCP.
   Put this deepObject parameter directly in the URL, encoding its name and directory value, e.g. `/api/mcp?location%5Bdirectory%5D=G%3A%5CModProjects%5CDemo`. Use the same query on connect/disconnect URLs. In a reported installed CLI, `opencode api post ... --param "location[directory]=..."` left the request in the default directory and produced McpServerNotFoundError. Do not rely on that shorthand; check the location returned by GET before acting.
3. If Bridge answers but that server remains failed, invoke its connect action, wait for connected status, then verify resources/tools and an actual get_game_status call. These management actions are asynchronous; request acceptance is not the final state.
4. After a game restart, keep a working connection. Only on an actual call failure or stale state, use the matching disconnect action then connect again. The tested disconnect route is `POST /api/experimental/mcp/{server}/disconnect`. A changed instanceId alone is not a reconnect requirement.

The tested management API uses the host's own authentication; keep its credentials local. This authentication belongs to OpenCode, not the token-free Bridge MCP endpoint. Experimental paths must be checked against the installed host's schema. If no accessible management action is available, explain the one required reconnect interaction instead of repeatedly editing the URL or pretending the current Agent's tools are available. A configuration reload is for actual configuration changes.

References: [OpenCode MCP management](https://opencode.ai/docs/mcp-servers/#management), [server and API discovery](https://opencode.ai/docs/server/).

## Refresh a cached tool contract after an upgrade

A successful get_game_status call proves connectivity, not that the host has refreshed its tool schemas. If its tools differ from the current Bridge Tools/list, use the matching project-scoped disconnect/connect actions above, wait for connected, then refresh the host/session tool catalog and verify the actual argument list. A client can cache the model-facing schema separately from its MCP connection. If it remains old, reload that host session once; keep the endpoint/configuration unchanged. Do not silently drop new parameters or weaken an operation's preconditions to force a call through.

The first-release operate_ui contract uses only the returned targetRef string (observation.actionRef for drag destinations). Observation checks travel in this opaque reference, so no independent observation_token or to_observation_token argument is needed. Copy the reference unchanged; a bare uiRef is for reads. Direct JSON-RPC is a diagnostic fallback, not the normal UI workflow.

## Diagnose before Bridge startup

If a release directory was provided by the user, inspect and use it directly; a missing Workshop download directory does not invalidate that local package. For a subscription installation, check the actual Steam library and download state.

When the endpoint is absent, check the game's MOD manager and the current Unity Player.log, under the game's persistent-data directory. Search for the Bridge ID and nearby `scan refused`, scan findings, `running=False`, `trust=Changed`, missing assemblies or undeclared capabilities. Player-prev.log belongs to the previous run; keep timestamps and loaded version separate.

A changed package hash triggers native trust/scan checks, not automatic rejection. `trust=Updated` with `running=True` can be a successful update; `Changed` needs its specific note/findings interpreted. If the loader refuses the DLL, Bridge code has not started, so no Bridge failure file is expected. Fix the identified package/code/capability issue and use the game's native approval flow when required. Do not diagnose this as a host URL error solely because MCP is absent.

If Bridge itself started but failed to bind, inspect its latest `.runtime/failures/*.json` and Player.log for http_bind_failed, requestedUrl and the native error. Check whether another live Bridge owns the same fixed endpoint before changing anything. Preserve a working instance; resolve the specific conflict or permission issue. For an intentional second instance, choose a distinct explicit port and configure its host URL once.

## Evidence for network availability

Use an actual `/bridge/status` response and MCP initialize/ping response with a fresh request ID to establish endpoint identity and responsiveness. Then check the host's connection state separately. netstat/Get-NetTCPConnection are supplementary: no LISTENING row alone is insufficient to declare the service down. If a socket request is denied by the Agent's execution environment while normal local execution succeeds, report that boundary rather than changing the game's listener or firewall settings.

## Diagnose observed failure

| Observation | Next check |
|---|---|
| No package | Prefer a user-provided release directory; otherwise check subscription/download state, Steam library and item id |
| No descriptor | Game process, MOD enable/capabilities/restart, native scan findings, root override, Player.log and directory permissions |
| Invalid JSON or endpoint mismatch | Ignore invalid/stale descriptor; rediscover canonical instances before writes |
| Unreachable endpoint | Exit/crash or stale file; verify a matching live endpoint after restart |
| Several live instances | Select explicitly; do not take over another instance/session |
| Transport alive, tool pending | Unity loading/blocked; retain operationId and query, do not retry writes |
| Disk changed | Steam updated during play; normal restart and rediscovery |
| Endpoint alive but host failed | Use the existing host's explicit reconnect action, then verify actual tools; keep unchanged configuration |
| http_bind_failed | Inspect requestedUrl/native error and a possible existing instance; endpoint did not move |
| No LISTENING row | Verify actual HTTP/MCP responses before concluding the service is unavailable |
| No host tools | Configuration scope/syntax/version, HTTP support and connection state; initial configuration may need a host reload |
| Access denied | Identify the failing operation/directory and narrow permission fix; avoid global administrator setup |
| No compiler | Only development needs it; inspect SDK and installed Managed references, install needed tooling |

## Update, disable and unsubscribe

Steam owns the DLL, manifest and packaged guides. Runtime descriptors/modules live in the current user's data root. Crashes can leave stale descriptors; normal shutdown removes only this process's descriptors. Projects, caches, reports and save backups remain outside the workshop package. Disabling/unsubscribing stops Bridge after the game's required restart; it neither deletes player projects nor undoes gameplay changes. Final MODs are deployed independently and must run with Bridge disabled.
