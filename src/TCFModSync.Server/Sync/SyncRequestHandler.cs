using TCFModSync.Shared.Models;

namespace TCFModSync.Server.Sync;

public sealed class SyncRequestHandler
{
    private readonly string _sptRootDirectory;
    private readonly ServerConfig _config;
    private readonly ManifestBuilder _manifestBuilder;
    private readonly Action<string> _log;

    public SyncRequestHandler(
        string sptRootDirectory, ServerConfig config, ManifestBuilder manifestBuilder, Action<string> log)
    {
        _sptRootDirectory = Path.GetFullPath(sptRootDirectory);
        _config = config;
        _manifestBuilder = manifestBuilder;
        _log = log;
    }

    public Manifest BuildManifest(bool headless)
    {
        var manifest = _manifestBuilder.Build(_sptRootDirectory, _config, _config.SptVersion, headless);

        if (manifest.Files.Count == 0)
        {
            _log($"Manifest is EMPTY - scanned '{_sptRootDirectory}' and nothing matched the include " +
                 "patterns (or everything matched was excluded).");

            foreach (var line in _manifestBuilder.DiagnoseEmptyScan(_sptRootDirectory, _config))
            {
                _log(line);
            }
        }
        else if (_config.VerboseLogging)
        {
            var totalBytes = manifest.Files.Sum(f => f.Size);
            _log($"Manifest requested{(headless ? " (headless)" : "")}: {manifest.Files.Count} file(s), " +
                 $"{totalBytes / 1024.0 / 1024.0:F1} MB.");
        }

        return manifest;
    }

    public bool TryResolveSafePath(string relativePath, out string absolutePath)
    {
        absolutePath = "";

        if (string.IsNullOrWhiteSpace(relativePath)) return false;
        if (Path.IsPathRooted(relativePath)) return false;
        if (relativePath.Split('/', '\\').Any(segment => segment == "..")) return false;

        string candidate;
        try
        {
            candidate = Path.GetFullPath(
                Path.Combine(_sptRootDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        }
        catch
        {
            return false;
        }

        var rootWithSeparator = _sptRootDirectory.EndsWith(Path.DirectorySeparatorChar)
            ? _sptRootDirectory
            : _sptRootDirectory + Path.DirectorySeparatorChar;

        if (!candidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase)) return false;

        absolutePath = candidate;
        return true;
    }
}
