using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using SptModSync.Shared.Models;

namespace SptModSync.Client.Handoff
{
    public sealed class UpdaterLauncher
    {
        private const string PendingFileName = "SptModSync.pending.json";

        private readonly string _gameRootDirectory;
        private readonly Action<string> _log;

        public UpdaterLauncher(string gameRootDirectory, Action<string> log)
        {
            _gameRootDirectory = gameRootDirectory;
            _log = log;
        }

        private string PendingFilePath => Path.Combine(_gameRootDirectory, PendingFileName);

        public void LaunchUpdaterAndPrepareForExit(PendingOperations pending)
        {
            pending.WaitForProcessId = Process.GetCurrentProcess().Id;
            var relaunchingGame = !string.IsNullOrEmpty(pending.RelaunchExecutable) &&
                                  Path.GetFileName(pending.RelaunchExecutable)
                                      .Equals("EscapeFromTarkov.exe", StringComparison.OrdinalIgnoreCase);

            if (relaunchingGame)
            {
                pending.RelaunchArguments = GetOwnArguments();
                if (string.IsNullOrWhiteSpace(pending.RelaunchArguments))
                {
                    _log("[SptModSync] WARNING: could not read this process's launch arguments. The game " +
                         "may close immediately on relaunch.");
                }
            }
            else
            {
                pending.RelaunchArguments = "";
            }

            var json = JsonSerializer.Serialize(pending, new JsonSerializerOptions { WriteIndented = true });
            var tmpPath = PendingFilePath + ".tmp";
            File.WriteAllText(tmpPath, json);
            if (File.Exists(PendingFilePath)) File.Delete(PendingFilePath);
            File.Move(tmpPath, PendingFilePath);

            var updaterExe = Path.Combine(_gameRootDirectory, "SptModSync.Updater.exe");
            if (!File.Exists(updaterExe))
            {
                _log($"[SptModSync] Cannot find updater at '{updaterExe}' - files were downloaded to staging " +
                     "but cannot be applied. Re-run the updater manually once it's back in place.");
                return;
            }

            var psi = new ProcessStartInfo
            {
                FileName = updaterExe,
                Arguments = $"--pending \"{PendingFilePath}\"",
                UseShellExecute = true,
                WorkingDirectory = _gameRootDirectory
            };

            _log($"[SptModSync] Launching updater (pid {pending.WaitForProcessId} will be awaited)...");
            Process.Start(psi);
        }

        private static string GetOwnArguments()
        {
            try
            {
                var full = Environment.CommandLine;
                if (string.IsNullOrEmpty(full)) return "";

                int endOfExe;
                if (full[0] == '"')
                {
                    endOfExe = full.IndexOf('"', 1);
                    if (endOfExe < 0) return "";
                    endOfExe++;
                }
                else
                {
                    endOfExe = full.IndexOf(' ');
                    if (endOfExe < 0) return "";
                }

                return full.Substring(endOfExe).Trim();
            }
            catch
            {
                return "";
            }
        }
    }
}
