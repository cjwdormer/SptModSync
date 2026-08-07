using TCFModSync.Shared.Hashing;
using TCFModSync.Shared.Models;
using TCFModSync.Updater.Logging;

namespace TCFModSync.Updater.Apply;

public sealed class FileOperationApplier
{
    private readonly UpdaterLog _log;

    public FileOperationApplier(UpdaterLog log)
    {
        _log = log;
    }

    public int Apply(PendingOperations pending)
    {
        var failures = 0;

        foreach (var op in pending.Operations)
        {
            try
            {
                switch (op.Kind)
                {
                    case PendingOpKind.CopyFromStaging:
                        ApplyCopy(op);
                        break;
                    case PendingOpKind.DeleteFile:
                        ApplyDelete(op);
                        break;
                }
            }
            catch (Exception ex)
            {
                failures++;
                _log.Write($"FAILED on '{op.DestinationAbsolutePath}': {ex.Message}");
            }
        }

        return failures;
    }

    private void ApplyCopy(PendingOperation op)
    {
        if (op.StagedAbsolutePath == null || !File.Exists(op.StagedAbsolutePath))
        {
            throw new FileNotFoundException("Staged file is missing.", op.StagedAbsolutePath);
        }

        if (!string.IsNullOrEmpty(op.ExpectedHash))
        {
            var actualHash = FileHasher.HashFile(op.StagedAbsolutePath);
            if (!string.Equals(actualHash, op.ExpectedHash, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Staged file hash mismatch (expected {op.ExpectedHash}, got {actualHash}) - refusing to apply a possibly corrupt download.");
            }
        }

        var destDir = Path.GetDirectoryName(op.DestinationAbsolutePath);
        if (!string.IsNullOrEmpty(destDir)) Directory.CreateDirectory(destDir);

        if (IsOwnExecutable(op.DestinationAbsolutePath))
        {
            ApplySelfUpdate(op);
            return;
        }

        const int maxAttempts = 5;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                File.Copy(op.StagedAbsolutePath, op.DestinationAbsolutePath, overwrite: true);
                _log.Write($"Applied update: {op.DestinationAbsolutePath}");
                return;
            }
            catch (IOException) when (attempt < maxAttempts)
            {
                Thread.Sleep(1000);
            }
        }
    }

    private static bool IsOwnExecutable(string destinationPath)
    {
        var ownPath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(ownPath)) return false;

        return string.Equals(
            Path.GetFullPath(destinationPath),
            Path.GetFullPath(ownPath),
            StringComparison.OrdinalIgnoreCase);
    }

    private void ApplySelfUpdate(PendingOperation op)
    {
        var ownPath = Path.GetFullPath(op.DestinationAbsolutePath);
        var retiredPath = ownPath + ".old";

        try
        {
            if (File.Exists(retiredPath)) File.Delete(retiredPath);
        }
        catch (Exception ex)
        {
            _log.Write($"Could not remove previous '{Path.GetFileName(retiredPath)}': {ex.Message}");
            retiredPath = ownPath + $".old{DateTime.Now:HHmmss}";
        }

        File.Move(ownPath, retiredPath);
        _log.Write($"Moved running updater aside to '{Path.GetFileName(retiredPath)}'.");

        try
        {
            File.Copy(op.StagedAbsolutePath!, ownPath, overwrite: true);
            _log.Write($"Applied new updater build: {ownPath}");
        }
        catch
        {
            try { File.Move(retiredPath, ownPath); _log.Write("Copy failed - restored the previous updater."); }
            catch { }
            throw;
        }
    }

    public void CleanUpRetiredSelf()
    {
        var ownPath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(ownPath)) return;

        var directory = Path.GetDirectoryName(ownPath);
        if (string.IsNullOrEmpty(directory)) return;

        var pattern = Path.GetFileName(ownPath) + ".old*";
        foreach (var stale in Directory.EnumerateFiles(directory, pattern))
        {
            try
            {
                File.Delete(stale);
                _log.Write($"Cleaned up previous updater build '{Path.GetFileName(stale)}'.");
            }
            catch
            {
            }
        }
    }

    private void ApplyDelete(PendingOperation op)
    {
        if (!File.Exists(op.DestinationAbsolutePath))
        {
            _log.Write($"Delete skipped (already absent): {op.DestinationAbsolutePath}");
            return;
        }

        const int maxAttempts = 5;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                File.Delete(op.DestinationAbsolutePath);
                _log.Write($"Deleted: {op.DestinationAbsolutePath}");
                return;
            }
            catch (IOException) when (attempt < maxAttempts)
            {
                Thread.Sleep(1000);
            }
        }
    }
}
