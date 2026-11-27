# Connect the MOD's MCP

Complete [README.md](README.md) first: prepare tools, resolve the user's risk-review choice and enable the in-game MOD. This guide covers connection. Windows / Probably Stolen only; the game-hosted service uses standard MCP Streamable HTTP on 127.0.0.1 without a token.

## Find the package and game

If the user supplied a release-folder path, use that folder directly. Do not require a Workshop download to exist for this mode. Confirm the folder contains the DLL, manifest and guides. For a local installation, use the game's native Mods directory, back up an existing MOD installation and copy the complete package including documentation; exit the game normally before replacing its DLL.

For a Steam subscription, this MOD's workshop item is [3835953432](https://steamcommunity.com/sharedfiles/filedetails/?id=3835953432); the game app ID is 4348910. Look under `steamapps/workshop/content/4348910/3835953432/` in the Steam libraries. Read SteamPath from HKCU\Software\Valve\Steam and enumerate steamapps/libraryfolders.vdf; use appmanifest_4348910.acf for the game's installdir. Decode escaped VDF backslashes and use the actual library. The live descriptor's assemblyPath identifies the MOD installation actually loaded by the game.

The Workshop package contains one Compatible DLL, manifest.xml, guides/references and FullSource (readable C# sources, project and build guide). Check edition in /bridge/status or get_development_environment. To enable probes/hot reload, follow README.md and FullSource/BUILD.md only after explaining the risks and obtaining consent. Keep projects, compiler outputs and backups outside the downloaded package.

## Discover the active endpoint

The game must be running with this MOD enabled and native capability prompts resolved; restart when the game requests it. If that state is already confirmed, continue directly.

Read `%LOCALAPPDATA%/ModDevBridgeNext/.runtime/instances/*.json`. Verify a candidate's diagnosticUrl returns the same instanceId, releaseId and bridgeStartedAtUtc, then use its mcpUrl. Choose explicitly if several instances are live. Default port 18774 is fixed across restarts; a bind failure reports http_bind_failed without selecting another port. An explicit MODDEVBRIDGE_NEXT_PORT setting selects a different fixed port. Rediscover the instance after restart and keep old-instance operations separate.

Use an actual HTTP/MCP response as liveness evidence. Missing netstat LISTENING output alone does not prove a stopped server. For missing descriptors, scan rejection, bind failures or a discovery example, read [connection diagnostics](references/CONNECTION_DIAGNOSTICS.md).

## Configure the host

Merge one named HTTP MCP entry using the selected mcpUrl, preserving other services. Update an existing matching entry rather than creating duplicates. Examples below use the fixed default port; substitute an explicitly configured URL if needed.

Codex config.toml:

```toml
[mcp_servers.probably_stolen_dev]
url = "http://127.0.0.1:18774/mcp"
```

OpenCode V2 (check the installed major version):

```json
{"mcp":{"servers":{"probably_stolen_dev":{"type":"remote","url":"http://127.0.0.1:18774/mcp","oauth":false}}}}
```

Use a host supporting Streamable HTTP. An initial configuration may require one configuration/session reload. If the first connection failed while the game was offline and remains failed after the endpoint starts, invoke the host's reconnect action. For an already connected host after a game restart, first call get_game_status directly; reconnect only on failure/stale state. For OpenCode, see [host reconnect](references/CONNECTION_DIAGNOSTICS.md#recover-a-host-that-remains-failed). Keep unchanged endpoint configuration. Configuration references: [Codex](https://developers.openai.com/codex/mcp), [OpenCode](https://opencode.ai/docs/mcp-servers/).

After a MOD upgrade that changes tool arguments, refresh the host's MCP tool catalog if it still shows the old schema; some hosts expose this only through reconnect. This refresh is separate from transport recovery and is not required for every game restart.

## Verify and begin

Verify the actual host exposes Tools/list and can call get_game_status. ready=false in the main menu means connected but no save is loaded. Before gameplay experiments, follow [SKILL.md](SKILL.md) to identify the player's test save, load it through query_ui/operate_ui and verify the actual slot and readiness.

Read [SKILL.md](SKILL.md), also available at moddevbridge://guides/skill. get_development_environment reports current versions, Managed/SDK paths and documentationDirectory. Runtime observations and subsequent development depend on the player's request.

If tools are unavailable, a call fails or manual JSON-RPC is needed, read [connection diagnostics](references/CONNECTION_DIAGNOSTICS.md).
