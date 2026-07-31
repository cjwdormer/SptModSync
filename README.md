# SPT Mod Sync

Keeps SPT clients in step with the mods on a server. On launch the client compares what it has
against what the server is offering, it shows you exactly what would change and applies the changes
once you agree.

Built for **SPT 4.0.13**. By TheCrimsonFuckr.

---

## What it does

- Adds mods the server has and you don't
- Updates mods whose files differ from the server's
- Removes mods the server has dropped
- Leaves your own mods alone anything the server has never offered is never touched
- Lets you decline individual files and remembers that decision
- Never modifies a running install: everything downloads to a temporary folder first and files are
  only moved into place after the game has closed

---

## The three parts

| Part | Where it runs | What it does |
|---|---|---|
| **SptModSync.Server** | SPT server | Scans the server's mod folders and offers them over HTTP |
| **SptModSync.Client** | Game client (BepInEx plugin) | Compares, shows the sync window, downloads to a staging folder |
| **SptModSync.Updater** | Standalone `.exe` | Waits for the game to close, then moves files into place |

The updater is separate on purpose. A plugin can't replace files that the game itself has open, so
the work has to happen after the game exits by a process that outlives it.

---

## Documentation

- **[installation.md](installation.md)** setting up the server, client, and updater
- **[functionality.md](functionality.md)** configuration, the sync window, headless clients, files it creates
- **[troubleshooting.md](troubleshooting.md)** diagnosing a sync that isn't behaving
- **[BUILD.md](BUILD.md)** building from source

---

## Performance

Sync and manifest generation are built to hold up on large modpacks:

- Client downloads run up to 4 at a time instead of one at a time.
- Local file hashing the "checking for updates" pass that runs on every launch, not just when
  something changed is likewise bounded to 4 concurrent hashes.
- The server resolves include/exclude patterns with a single filesystem walk per manifest request
  instead of two.
- Glob patterns are compiled once per request and reused across every file checked, instead of being
  rebuilt from scratch for each one.

---

## Licence

MIT.
