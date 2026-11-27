# Native engineering experience

Use installed assemblies and get_development_environment for current game and Unity versions. Recheck API signatures and behavior after game updates.

## Updated capability rules

The observed scanner v4 deprecates dynamic-code, reflection-external, native and threads. Workshop Compatible compiles out runtime assembly loading and computed-type hot-reload reflection using BRIDGE_FULL; Full retains them for the player's local Developer mode. See README.md and FullSource/BUILD.md before switching editions.

Use ModHelper.RunInBackground(modId, Action<CancellationToken>, onFinished) for owned background work. Honor cancellation and close blocking listeners/streams; replacing Thread.Start with the helper alone does not stop a blocking read. Prefer native ModReflect for scoped game/MOD access or reflection with a statically known game type. The observed ConfigEntry setter is internal ApplyBoxed(object), replacing the former ApplyValue name; recheck the installed build before invoking internals.

Local placement alone does not skip scanning. Developer mode requires an approved local MOD and a manual player toggle; first approve a Compatible local copy before replacing it with Full. Blocklists, stopped records and global pauses remain separate. Workshop file/network verification requirements are also separate from deprecated-capability checks.

## Native MOD lifecycle

Use the game's public IMod contract (Init(ModManifest), OnEnable(), OnDisable()) and manifest assembly declaration. Verify required capabilities in the current loader. Subscribe to game/mod lifecycle events at appropriate timing and unsubscribe exact handlers in OnDisable. Init/OnEnable can precede asset/localization loading; gameplay singleton access can require a loaded save. Inspect existing mod IDs and patch owners before choosing identifiers or patch targets.

For new items, inspect the current native item directory registration and factory call chain (including ModHook.OnModItemDirectoryInit). Creating an item factory is only one part: verify acquisition, display, behavior, save persistence and handling when its MOD is absent. Use native ModHelper persistence rather than unrelated global save keys. Native registration signatures and missing-item behavior must be read from the current game, not guessed from this guide.

## Localization

Native ModHelper.GetLocalized(modId, key, params object[]) uses current-language entry, then English entry, then the key with a missing warning. This MOD localization mechanism differs from query_localization's Unity StringDatabase tables. Read the installed game's locale list before preparing translations.

Final MOD localization files normally live under Localization/<locale>.csv. The observed parser splits at the first comma, accepts quoted values with doubled quotes, ignores empty/comment lines and converts literal backslash-n to line breaks; it does not support physical multiline fields. Check current parser before generating CSV. Preserve placeholders across languages.

Use deferred Func<string> labels/descriptions so text resolves after resources load and can follow locale changes. A string section title may need registration after OnModAssetsLoaded and a restart to update. Avoid caching translations during Init. Choose supported languages according to the player's requirements; keep keys and placeholders consistent across those languages.

## Compatibility and UI

Prefer native registration, inventory, payment, events and UI components. Preserve an existing delegate's behavior when extending it; do not replace callbacks without accounting for native rewards, state and other MOD handlers. Scope Harmony patches to intended callers/instances. Patch order alone cannot fix conflicting business semantics.

UI text, layout and click handlers can be on different GameObjects/components. Labels/nearby text help discovery; visible=true or active flags do not prove correct rendering or clickability. Use raycast diagnostics for obstruction and inspect results after interaction. Rebind closures on existing objects when hot switching; unpatching Harmony does not remove event subscriptions or instance delegates. Stop/Disable/Prepare should tolerate partial initialization and repeated cleanup.

This MOD uses runtime instance/world-bound references. Refresh them after a game restart, save switch or expiration. Verify final MOD cold start and persistence independently of development hot reload. Keep permanent MOD assets and localization with the final MOD; the single-DLL constraint applies to this MOD itself.
