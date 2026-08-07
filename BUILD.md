# Environment Setup & Build Guide

## 1. Install the .NET 9 SDK

Download and run the installer from https://dotnet.microsoft.com/en-us/download/dotnet/9.0
(get the **SDK**, not just the runtime). Verify it worked:

```
dotnet --version
```

should print something starting with `9.`.

## 2. Install an IDE

Either works fine:

- **Visual Studio 2022 Community** (free) - https://visualstudio.microsoft.com/vs/community/
  During install, check the **".NET desktop development"** workload. You don't need the
  "Game development with Unity" workload for this project - we're not touching Unity APIs beyond
  `MonoBehaviour`/`Application`, which come from small reference DLLs instead (see step 4).
- **VS Code** + the **C# Dev Kit** extension - lighter weight, works fine for everything here.

## 3. Open the solution

Open `TCFModSync.sln`. Your IDE should restore NuGet packages automatically on first load. If it
doesn't, run:

```
dotnet restore
```

from the repo root.

### If restore fails with NU1101 on `BepInEx.Core`

BepInEx does not publish to nuget.org — it runs its own feed. `build/NuGet.config` declares it, and
`build/Directory.Build.props` points restore at that file via `RestoreConfigFile`, so opening
`TCFModSync.sln` or running `dotnet restore` from the repo root should just work with no extra
flags. If you still hit NU1101:

- In Visual Studio, check **Tools > NuGet Package Manager > Package Manager Settings > Package
  Sources** and confirm `https://nuget.bepinex.dev/v3/index.json` is listed and ticked.
- Corporate networks and some DNS filters block the BepInEx feed. If that's you, use Option B below.

**Option B — skip NuGet for BepInEx entirely.** Since you already have BepInEx installed in your
game folder, you can reference its DLL directly - the project already does this via `SptInstallDir`
(see step 4), so with that set up, no csproj edits are needed here at all.

## 4. Point the Client project at your game's managed DLLs

`TCFModSync.Client` needs several DLLs from your own SPT install that can't be redistributed or
fetched via NuGet - they ship with the game itself (BepInEx core, UnityEngine modules, spt-common).
The project finds them via an MSBuild property, `SptInstallDir`, rather than a hardcoded path, so
your personal install location never ends up in a file you'd commit.

Set it one of two ways:

- **Local props file (recommended):** copy `build/Directory.Build.local.props.example` to
  `build/Directory.Build.local.props` and edit the path inside. That file is gitignored, so it stays
  on your machine.
- **Environment variable:** set `SPT_INSTALL_DIR` to your SPT folder, e.g.
  `E:\Single Player Tarkov`.

If `SptInstallDir` isn't set either way, the build fails fast with a message telling you to do one
of the above, instead of a confusing "file not found" from the compiler.

## 5. Build

Set the configuration to **Release** and build the whole solution (Build > Rebuild Solution, or
`Ctrl+Shift+B` in VS / `Ctrl+Shift+F9` in Rider). From the command line:

```
dotnet build -c Release
```

## 6. Expect (and fix) a few compile errors in TCFModSync.Server

I wrote `ModEntry.cs` and `ModMetadataInfo.cs` against SPTarkov's documented mod-authoring pattern
(`IOnLoad`, `AbstractModMetadata`, `ISptLogger<T>`, `[Injectable]`), but couldn't verify the exact
namespaces/member names against the real 4.0.13 package since I had no network access to pull it
down and compile against it myself. If the build reports errors here:

1. Open the file the error points to.
2. Hover the red squiggle - IntelliSense will usually show you the actual expected signature or a
   "did you mean...?" suggestion.
3. The [`sp-tarkov/server-mod-examples`](https://github.com/sp-tarkov/server-mod-examples) repo has
   working reference code for this exact pattern if you want a side-by-side comparison - clone it
   and open `2EditDatabase/EditDatabaseValues.cs` for a minimal `ModMetadata` + entry point example.

`TCFModSync.Client`, `TCFModSync.Shared`, and `TCFModSync.Updater` don't depend on any SPT-specific
packages (just BepInEx, which is stable and well-documented) so those should build clean.

## 7. Deploy layout

After a successful Release build:

```
<SPT server>/SPT/user/mods/TCF-ModSync.Server/
  TCFModSync.Server.dll  (+ its dependencies, from src/TCFModSync.Server/bin/Release/TCFModSync.Server/)
  config/serverConfig.json

<SPT client>/
  TCFModSync.Updater.exe  (from src/TCFModSync.Updater/bin/Release/ - use `dotnet publish` for the
                            self-contained single-file version, see below)
  BepInEx/plugins/TCF-ModSync.Client/
    TCFModSync.Client.dll  (+ its dependencies, from src/TCFModSync.Client/bin/Release/TCFModSync.Client/)
```

For the updater, publish a self-contained single-file build so end users don't need .NET 9 installed
separately:

```
dotnet publish src/TCFModSync.Updater -c Release -r win-x64 --self-contained true
```

The output `.exe` (in `src/TCFModSync.Updater/bin/Release/net9.0/win-x64/publish/`) goes directly
next to `EscapeFromTarkov.exe`.

## 8. Testing the full loop

1. Start the SPT server with the mod installed - check the server console for the
   `[TCF-ModSync] Ready - sharing ... file(s) on the SPT server's own port.` log line.
2. Launch the game with the client plugin installed. Add or change a file under one of your
   `IncludePatterns` on the server side and restart the server to re-scan.
3. On next client launch you should see the sync window. Accept it, confirm the game closes, and
   watch `TCFModSync.Updater.log` (written next to the updater exe) to see each operation applied
   and the game relaunch.
