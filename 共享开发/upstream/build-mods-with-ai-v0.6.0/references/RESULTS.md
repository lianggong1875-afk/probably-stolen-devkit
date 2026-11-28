# Results and experiment ownership

Tools return structured content with ok, operationId, worldId, result and error. ok=true reports completed tool execution; read the domain result for actual changes and effect verification. ok=false reports a failure that can include partial changes. ok=null means pending/unknown: query the original operationId before deciding on another write. A timeout is not proof that nothing happened. Old-instance operations cannot be continued in a new game process and must not be blindly resubmitted.

Collection counts, searchComplete, omitted/truncated fields and nextOffset describe specific search/output limits. Equal selected snapshots do not establish equality of unobserved state. get_operation supports retained-result projections/slices; consult its current schema for result_path and paging.

Mutating tracked experiments require explicit session/world identity. Inspect an existing active session rather than silently taking it over. Resources are released in reverse ownership order; cleanup reports completed, failed and skipped. A skip is not restoration. Failed cleanup can retain the session for retry. Cleanup must not overwrite legitimate later player changes.

Native factory calls, RNG/cooldown state, third-party callbacks, configuration persistence and save writes are not generally reversible by session cleanup. Config tools distinguish memory, notification and persisted-file readback. Test-session close does not undo a successful MOD replacement. Mono assemblies remain loaded until game exit.

Use the authorization and backup rules in [README.md](../README.md). Observe logs, selected state and intended effects separately; restart the game when runtime state cannot be confirmed.
