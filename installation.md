# Installation

[← Back to README](README.md)

### Server

Copy the **whole `SptModSync.Server` folder** into your server's mods directory:

```
<SPT server>\SPT\user\mods\SptModSync.Server\
    SptModSync.Server.dll
    SptModSync.Shared.dll
    Microsoft.Extensions.FileSystemGlobbing.dll
    System.IO.Hashing.dll
    SptModSync.Server.deps.json
    config\
        serverConfig.default.json
```

Then put a copy of the updater in the **server root** not the mods folder:

```
<SPT server>\SptModSync.Updater.exe
```

This is so the server distributes the updater to clients, keeping everyone on the same build. It
should be the same file you install on clients.

Start the server once. It creates `config\serverConfig.json` next to the default, and prints:

```
[SptModSync] Ready - sharing 289 file(s) on the SPT server's own port.
```

### Client

Copy the **whole `SptModSync.Client` folder** into BepInEx's plugins directory:

```
<game>\BepInEx\plugins\SptModSync.Client\
    SptModSync.Client.dll
    SptModSync.Shared.dll
    Microsoft.Extensions.FileSystemGlobbing.dll
    Microsoft.Bcl.AsyncInterfaces.dll
    System.IO.Hashing.dll
    System.IO.Pipelines.dll
    System.Text.Json.dll
    System.Text.Encodings.Web.dll
    System.Buffers.dll
    System.Memory.dll
    System.Numerics.Vectors.dll
    System.Runtime.CompilerServices.Unsafe.dll
    System.Threading.Tasks.Extensions.dll
    System.ValueTuple.dll
```

Then put the updater in the **game root**, next to `EscapeFromTarkov.exe`:

```
<game>\SptModSync.Updater.exe
```

**The updater must be installed manually the first time.** The server distributes it after that, but
it can't install itself it's the thing that applies downloads, so it has to already be present for
the first sync to complete.
