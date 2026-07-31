using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using SptModSync.Shared.Json;
using SptModSync.Shared.Models;

namespace SptModSync.Client.Sync
{
    public sealed class StagedStore
    {
        private readonly string _stagedFilePath;
        private readonly Action<string> _log;

        public StagedStore(string pluginDirectory, Action<string> log)
        {
            _stagedFilePath = Path.Combine(pluginDirectory, "staged.json");
            _log = log;
        }

        public void Save(PendingOperations pending)
        {
            try
            {
                var json = JsonSerializer.Serialize(pending, JsonDefaults.FileOptions);
                var tmp = _stagedFilePath + ".tmp";
                File.WriteAllText(tmp, json);
                if (File.Exists(_stagedFilePath)) File.Delete(_stagedFilePath);
                File.Move(tmp, _stagedFilePath);

                _log($"[SptModSync] Kept {pending.Operations.Count} staged file(s) for next launch.");
            }
            catch (Exception ex)
            {
                _log($"[SptModSync] Could not record staged downloads: {ex.Message}");
            }
        }

        public PendingOperations? Load()
        {
            try
            {
                if (!File.Exists(_stagedFilePath)) return null;

                var pending = JsonSerializer.Deserialize<PendingOperations>(
                    File.ReadAllText(_stagedFilePath), JsonDefaults.Options);

                if (pending == null || pending.SchemaVersion != 2)
                {
                    Discard();
                    return null;
                }

                var usable = pending.Operations
                    .Where(op => op.Kind != PendingOpKind.CopyFromStaging
                                 || (!string.IsNullOrEmpty(op.StagedAbsolutePath) && File.Exists(op.StagedAbsolutePath)))
                    .ToList();

                if (usable.Count == 0)
                {
                    Discard();
                    return null;
                }

                pending.Operations = usable;
                return pending;
            }
            catch (Exception ex)
            {
                _log($"[SptModSync] Could not read staged downloads ({ex.Message}); they will be downloaded again.");
                Discard();
                return null;
            }
        }

        public void Discard()
        {
            try
            {
                if (File.Exists(_stagedFilePath)) File.Delete(_stagedFilePath);
            }
            catch
            {
            }
        }
    }
}
