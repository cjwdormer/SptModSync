using System.Diagnostics;
using System.Text.Json;
using SptModSync.Shared.Models;
using SptModSync.Updater.Apply;
using SptModSync.Updater.Logging;

var exeDir = AppContext.BaseDirectory;
using var log = new UpdaterLog(Path.Combine(exeDir, "SptModSync.Updater.log"));

try
{
    var pendingPath = ParsePendingArg(args)
                       ?? FindLeftoverPendingFile(exeDir);

    if (pendingPath == null)
    {
        log.Write("No pending operations file found and none was specified via --pending. Nothing to do; exiting.");
        return 0;
    }

    if (!File.Exists(pendingPath))
    {
        log.Write($"Pending file '{pendingPath}' does not exist. Nothing to do; exiting.");
        return 0;
    }

    var pending = JsonSerializer.Deserialize<PendingOperations>(File.ReadAllText(pendingPath));
    if (pending == null)
    {
        log.Write("Pending file could not be parsed. Aborting without touching any files.");
        return 1;
    }

    if (pending.SchemaVersion != 2)
    {
        log.Write($"Pending file schema version {pending.SchemaVersion} is not supported by this updater build. Aborting.");
        return 1;
    }

    log.Write($"Loaded {pending.Operations.Count} operation(s). Waiting for game process {pending.WaitForProcessId} to exit...");
    WaitForProcessExit(pending.WaitForProcessId, log);

    Thread.Sleep(1000);

    var applier = new FileOperationApplier(log);

    applier.CleanUpRetiredSelf();

    var failures = applier.Apply(pending);

    if (failures > 0)
    {
        log.Write($"{failures} operation(s) failed. NOT deleting the pending file, so a re-run can retry. Not relaunching automatically.");
        return 1;
    }

    log.Write("All operations applied successfully.");

    CleanUp(pending, pendingPath, log);

    if (!string.IsNullOrEmpty(pending.RelaunchExecutable) && File.Exists(pending.RelaunchExecutable))
    {
        log.Write($"Relaunching '{pending.RelaunchExecutable}'" +
                  (string.IsNullOrWhiteSpace(pending.RelaunchArguments)
                      ? " with no arguments - it may exit immediately if it expects launcher arguments."
                      : " with the arguments it was originally started with."));

        var extension = Path.GetExtension(pending.RelaunchExecutable).ToLowerInvariant();
        var startInfo = extension == ".ps1"
            ? new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-ExecutionPolicy Bypass -File \"{pending.RelaunchExecutable}\" {pending.RelaunchArguments}".TrimEnd(),
                WorkingDirectory = Path.GetDirectoryName(pending.RelaunchExecutable),
                UseShellExecute = true
            }
            : new ProcessStartInfo
            {
                FileName = pending.RelaunchExecutable,
                Arguments = pending.RelaunchArguments,
                WorkingDirectory = Path.GetDirectoryName(pending.RelaunchExecutable),
                UseShellExecute = true
            };

        Process.Start(startInfo);
    }
    else
    {
        log.Write("No relaunch executable configured (or it's missing) - leaving the game closed.");
    }

    return 0;
}
catch (Exception ex)
{
    log.Write($"FATAL: {ex}");
    return 1;
}

static string? ParsePendingArg(string[] args)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (args[i] == "--pending") return args[i + 1];
    }
    return null;
}

static string? FindLeftoverPendingFile(string exeDir)
{
    var candidate = Path.Combine(exeDir, "SptModSync.pending.json");
    return File.Exists(candidate) ? candidate : null;
}

static void WaitForProcessExit(int pid, UpdaterLog log)
{
    if (pid <= 0)
    {
        log.Write("No valid PID given - assuming the game is already closed.");
        return;
    }

    while (true)
    {
        try
        {
            using var proc = Process.GetProcessById(pid);
            if (proc.HasExited) break;
            Thread.Sleep(500);
        }
        catch (ArgumentException)
        {
            break;
        }
    }

    log.Write("Game process has exited.");
}

static void CleanUp(PendingOperations pending, string pendingPath, UpdaterLog log)
{
    try
    {
        foreach (var stagingDirectory in pending.StagingDirectories)
        {
            if (!string.IsNullOrEmpty(stagingDirectory) && Directory.Exists(stagingDirectory))
            {
                Directory.Delete(stagingDirectory, recursive: true);
            }
        }
    }
    catch (Exception ex)
    {
        log.Write($"Could not clean up staging directory: {ex.Message}");
    }

    try
    {
        File.Delete(pendingPath);
    }
    catch (Exception ex)
    {
        log.Write($"Could not delete pending file: {ex.Message}");
    }
}
