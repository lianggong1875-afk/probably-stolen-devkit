# Publishing a MOD through the logged-in Steam client

An Agent can create a temporary C# publishing utility in its own workspace and call Steamworks through the user's running, logged-in Steam client. The Steam session supplies authentication; no Steam password, cookie export, API key or MOD dependency is needed for this route. Use the player's intended account and verify game ownership before changing Workshop content.

This document describes the publishing mechanism. Prepare and review the target, package, page text, images and visibility with the user, and operate within their publishing authorization. A request to prepare a release does not by itself authorize making it public.

## Environment and temporary utility

- Discover Steam and the actual game library through the Steam registry entry, libraryfolders.vdf and the game's appmanifest. Use the installed game's Managed and native-plugin directories; do not assume a drive or an author's workspace.
- Compile against the installed Steamworks.NET binding, commonly `com.rlabrecque.steamworks.net.dll`. Locate the matching native `steam_api64.dll` for the process architecture. Check actual filenames and binding signatures in the installation.
- Use a compatible SDK/runtime for this separate utility. Keep its executable, dependencies, receipts and logs outside the final MOD package. Its runtime is independent of the game's Unity Mono target.
- Set `SteamAppId` / `SteamGameId` for the utility process to the intended game's app ID before Steam initialization. Probably Stolen's app ID is `4348910`.
- Resolve the binding's native Steam API import to the discovered native library, for example with `NativeLibrary.SetDllImportResolver` on a compatible .NET runtime. Call `SteamAPI.InitEx` (or the installed binding's equivalent), check the result, and verify `SteamUtils.GetAppID()` and `SteamApps.BIsSubscribedApp(...)`. Call `SteamAPI.Shutdown()` when finished.

Use the session's current Steam user as the owner identity. Do not infer ownership from a display name or try to edit another publisher's item.

## Create or reuse the correct item

Keep the app ID, target MOD ID, published-file ID and item URL in a project-owned metadata/receipt file. If an item ID already exists, query it and confirm ownership and consumer app before updating it.

For an explicitly authorized new item, use `SteamUGC.CreateItem(appId, EWorkshopFileType.k_EWorkshopFileTypeCommunity)`. Register the `CreateItemResult_t` callback, call `SteamAPI.RunCallbacks()` until it completes, and inspect the I/O failure flag and `m_eResult`. Save `m_nPublishedFileId` immediately when returned, before subsequent updates. Keep `m_bUserNeedsToAcceptWorkshopLegalAgreement`; if set, direct the user to accept Steam's agreement through Steam.

An empty private placeholder can reserve the item's ID before final packaging. Set and verify private visibility explicitly. Creating a placeholder does not validate a release package.

If creation times out or its result is unknown, inspect the saved receipt and the account's published Workshop items before retrying. Do not create duplicates merely because the response was lost. Later releases reuse the same published-file ID.

## Upload and update

Use `SteamUGC.StartItemUpdate(appId, publishedFileId)` and set the intended properties:

| API | Meaning |
|---|---|
| `SetItemTitle` / `SetItemDescription` | Page title and body |
| `SetItemUpdateLanguage` | Language of the metadata being updated |
| `SetItemContent` | Absolute path to a clean, final release directory |
| `SetItemPreview` | Cover image, within the current Steam limits |
| `SetItemVisibility` | Explicitly selected private, friends, unlisted or public visibility |
| `SetItemTags` | Tags appropriate to the game, if needed |
| `SubmitItemUpdate` | Submit with the intended change note |

Check the boolean result of each setter. Keep the `SubmitItemUpdateResult_t` callback alive, pump `SteamAPI.RunCallbacks()`, and check I/O failure, `m_eResult` and any agreement requirement. `GetItemUpdateProgress` can report progress while waiting. A submitted request or progress counter is not proof of a completed upload.

The content directory should contain the MOD's declared DLL and required assets, localization and documentation. Verify its manifest version and assembly list. Exclude SDKs, decompilation output, temporary publishing tools, caches, saves and credentials. A final player MOD must not reference or bundle this development tool.

For localized pages, update one language at a time and record which completed. Set content and visibility deliberately; do not accidentally switch a private test item to public during a text update. A failure after one language succeeds is a partial update, not a total failure.

## Verify the server and downloaded package

Query the item using `CreateQueryUGCDetailsRequest`, `SendQueryUGCRequest` and `GetQueryUGCResult`. Set query language and request the long description as needed; use a fresh response when verifying an update. Check the consumer app, owner, title/body, visibility, banned status and other properties relevant to the release. Release query handles afterward.

For content verification, use `DownloadItem` for the intended item, wait for its download result and inspect `GetItemState` / `GetItemInstallInfo`. Compare the actual downloaded file list and each file's SHA256 with the prepared release directory. Server-reported size alone does not establish identical content. Do not change the user's subscriptions solely to run this verification unless that action is authorized.

Report the item URL, published-file ID, visibility, uploaded version, completed languages and what was actually verified. Keep incomplete or unknown results explicit. Enabling an updated MOD may require a normal game restart.

References: [Steam UGC API](https://partner.steamgames.com/doc/api/ISteamUGC), [Steam Workshop implementation guide](https://partner.steamgames.com/doc/features/workshop/implementation), [Steamworks.NET](https://steamworks.github.io/).
