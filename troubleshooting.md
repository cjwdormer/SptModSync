# Troubleshooting

[← Back to README](README.md)

**Nothing appears on launch.** Either there's genuinely nothing to sync, or the client can't reach
the server. Check `BepInEx\LogOutput.log` (or the client's own `TCFModSync.Client.log`, in the
plugin folder) for `[TCF-ModSync]` lines it logs the manifest count it received and the number of
actions it worked out.

**"Sync check failed".** The client syncs over the same connection it already uses to play, so if
you can reach the SPT server at all, syncing should work too. The log names the underlying reason
the request failed. The manifest request is retried a few times before giving up, so a brief blip
won't cost you the sync.

**"Manifest is EMPTY" on the server.** The include patterns matched nothing. The server logs each
pattern with the exact folder it looked in, which is usually enough to spot it. Most often
`SptRootDirectory` is pointing somewhere without a `BepInEx` folder.

**Client says 0 files, server says more.** They aren't talking to the same server. Open
`http://<host>:<port>/manifest` in a browser to see what the server is actually offering.

**Sync completed but nothing changed.** Read `TCFModSync.Updater.log`. It records every operation and
why any failed. A file that's still locked, or a hash that no longer matches, is refused rather than
applied the log names it.

**The game closed and didn't come back.** That's the default behaviour. Relaunch through the SPT
launcher.

**A mod keeps re-downloading every launch.** It's rewriting its own files, so the contents genuinely
change each time. Exclude the paths it regenerates.

**Server won't start after editing the config.** The console names the file, line, and column.
Trailing commas and `//` comments are both accepted, so it's usually a missing quote or bracket.
