# Functionality

[← Back to README](README.md)

## Configuration

### `serverConfig.json` - server side

Lives in `<mod folder>\config\`. Created on first launch. Restart the server after editing.

| Setting | What it does |
|---|---|
| `IncludePatterns` | Glob patterns for what to offer clients, relative to the SPT root |
| `ExcludePatterns` | Glob patterns carved back out of the above. Exclusions win |
| `FileHashBlacklist` | Hashes that must be deleted from any client that has them. Cannot be declined |
| `HeadlessIncludePatterns` | What Fika headless clients get, narrowed from `IncludePatterns`. Empty means they get nothing |
| `HeadlessExcludePatterns` | Carved back out of `HeadlessIncludePatterns` for headless clients only everyone else still gets these files |
| `SptRootDirectory` | Folder the patterns resolve against. Leave empty to detect automatically |
| `VerboseLogging` | Extra console detail. Leave off unless diagnosing something |
| `SptVersion` | Version string shown to connecting clients. Informational only |

Defaults:

```json
{
  "IncludePatterns": [
    "SptModSync.Updater.exe",
    "BepInEx/patchers/**/*",
    "BepInEx/plugins/**/*"
  ],
  "ExcludePatterns": [
    "**/*.log",
    "BepInEx/plugins/SAIN/**/*.json",
    "BepInEx/patchers/spt-prepatch.dll",
    "BepInEx/plugins/spt/**/*",
    "BepInEx/plugins/DynamicMaps/**/*"
  ],
  "FileHashBlacklist": [],
  "HeadlessIncludePatterns": [
    "BepInEx/patchers/**/*"
  ],
  "HeadlessExcludePatterns": [
    "BepInEx/plugins/Fika/**/*"
  ],
  "SptRootDirectory": "",
  "VerboseLogging": false,
  "SptVersion": "4.0.13"
}
```

**Patterns are relative to the SPT root and cannot escape it.** Absolute paths, drive letters, and
`..` segments are rejected at startup the mod will refuse to load rather than serve files from
outside your install.

**Excluding:** anything a mod rewrites while running. Caches generated bundles and per-user
settings will otherwise be re-offered on every launch, because their contents genuinely change.
Per-user config is the common case `SAIN/**/*.json` is excluded by default for exactly this
reason, since it holds each player's own bot tuning.

**Excluding something a client already synced doesn't delete it.** If a path drops out via
`ExcludePatterns` but the file is still on the server's disk, clients that already have it just stop
being managed for that path the file is left alone, not removed. Only a path that's genuinely gone
from the server (deleted, or never matched `IncludePatterns` in the first place) is removed from a
client that was tracking it.

### `clientConfig.json` - client side

Lives in `<game>\BepInEx\plugins\SptModSync.Client\`. Created automatically; you normally never edit
it.

| Setting | What it does |
|---|---|
| `ExcludePatterns` | Files you declined. Written automatically when you untick something |
| `TrackedFiles` | Files this client has synced, with their hashes |
| `HeadlessAutoAccept` | Force unattended syncing on this client |

`TrackedFiles` is a record, not a source of truth. Every launch re-reads what's actually on disk, so
deleting a synced mod by hand simply causes it to be offered again.

Deleting this file resets the client completely: everything is re-offered, and your declines are
forgotten.

### Plugin settings - BepInEx F12 menu

Under `com.thecrimsonfuckr.sptmodsync.client`:

| Setting | Default | What it does |
|---|---|---|
| `RelaunchTarget` | `None` | What to start after applying. See below |
| `HeadlessAutoAccept` | `false` | Force unattended syncing. Rarely needed see below |
| `ResetExclusionsOnNextLaunch` | `false` | Tick and restart to clear everything you've declined |

**`RelaunchTarget`** options:

- **`None`** the game closes and stays closed. Relaunch through the SPT launcher as normal.
  This is the default because it's the only one that reliably works.
- **`Launcher`** starts the SPT launcher for you. You still pick your profile.
- **`Game`** restarts the game directly, reusing its launch arguments. Usually fails: SPT's
  launcher issues a profile token that the server appears to accept only once, so the second process
  exits immediately.

### Fika headless clients

Headless instances are detected automatically and sync unattended no window every offer accepted and
files applied on shutdown. Nothing needs configuring in this instance by default it should all be good.

Detection uses Unity's batch-mode flag, the `-batchmode`/`-nographics` launch arguments, and the
presence of Fika's headless plugin. The reason is logged on startup:

```
[SptModSync] Headless client detected (Unity reports batch mode) - syncing unattended...
```

`HeadlessAutoAccept` only exists to force this behaviour on an instance that isn't detected as
headless. Don't set it on a client someone plays on: it would apply changes and close the game with
no prompt.

**Headless clients and reduced mod sets.** As they need only a handful of the mods players run,
and most client plugins will destabilise them. Set `HeadlessIncludePatterns` in `serverConfig.json`
and headless instances are offered only those for example:

```json
"HeadlessIncludePatterns": [
    "BepInEx/plugins/acidphantasm-botplacementsystem/**/*",
    "BepInEx/plugins/DrakiaXYZ-Waypoints/**/*",
    "BepInEx/plugins/DrakiaXYZ-BigBrain.dll",
    "BepInEx/plugins/QuestingBots/**/*",
    "BepInEx/plugins/skwizzy.LootingBots.dll"
],
"HeadlessExcludePatterns": [
  "BepInEx/plugins/Fika/**/*"
]
```

This lives on the server rather than on each headless client for two reasons: you should already know which
mods are headless-safe, and a newly installed headless instance is then safe with no configuration
of its own an unset `HeadlessIncludePatterns` offers headless clients nothing at all, rather than
defaulting to the full player mod set.

`HeadlessIncludePatterns` is an allow-list so it stays correct by itself add a mod server-side and
headless instances simply don't receive it, with nothing to update. `HeadlessExcludePatterns` exists
alongside it for the one case an allow-list can't express cleanly: a file you DO want synced to real
players but never to headless. 

Fika is that case a headless instance generates its own Fika install
rather than running the client plugin a real player does, so even though `Fika/**/*` matches
`HeadlessIncludePatterns` above (it has to, since players need it), `HeadlessExcludePatterns` carves it
back out for headless only. Regular `ExcludePatterns` can't do this: it applies to every client, so
using it to keep Fika off headless would also stop it being offered to players who need it.

Headless clients request this narrowed manifest automatically, so anything they synced previously
that is no longer offered is removed on the next sync like any other dropped file.

Patterns that match nothing fail silently, so check folder names against
`http://<host>:<port>/manifest` a misspelled folder means that mod is never delivered, with no
error anywhere.

**Restarting.** A headless instance simply closes after applying updates —
`FikaHeadlessManager.exe` supervises it and brings it back up on its own. `RelaunchTarget` is
ignored on headless clients.

---

## Using it

The sync window appears during startup if anything needs changing. It blocks the game's menu while
open, so you can't start a raid with mods you're about to replace.

Each entry is one of:

| Action | Meaning |
|---|---|
| **Add** | The server has it, you don't. Will download |
| **Update** | You both have it, contents differ. Will download |
| **Delete** | You have it, the server dropped it. Will be removed |
| **Blacklist** | Hash is on the server's blacklist. Forced removal, cannot be declined |
| *Adopt* | Already identical. Nothing transfers; only records update |
| *Untrack* | Already gone, declined, or no longer distributed by the server. Records only |

Adopt and Untrack aren't listed individually they're summarised, since neither touches a file.

Unticking something records it permanently, and it won't be offered again. To undo that, use the
**Offer Them Again** button in the window, or `ResetExclusionsOnNextLaunch` in the F12 menu if you've
declined so much that no window appears.

**Accept Offer** downloads to a temporary folder, showing progress. Nothing in your install has
changed at this point. When it finishes you're asked to confirm.

**Close Game And Apply** shuts the game down. The updater then waits for it to exit fully, verifies
every downloaded file's hash, and moves them into place.

**Not Now** keeps the downloads. They're reused on your next launch rather than fetched again, as
long as the server is still offering the same files.

---

## Files created while running

| File | Where | Purpose |
|---|---|---|
| `clientConfig.json` | Plugin folder | Your declines and synced-file records |
| `staged.json` | Plugin folder | Downloads kept for next launch |
| `SptModSync.pending.json` | Game root | Handoff to the updater. Deleted on success |
| `SptModSync.Updater.log` | Game root | What the updater did. **Check this first if a sync doesn't apply** |
| `SptModSync.Client.log` | Plugin folder | Client-side debug log, independent of BepInEx's shared `LogOutput.log` |
| `SptModSync.Server.log` | Server mod folder | Server-side debug log, independent of the SPT server's shared log |
| `staging_<id>\` | `%TEMP%\SptModSync\` | Downloads awaiting install |
