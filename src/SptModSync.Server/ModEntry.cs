using System.Reflection;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using SptModSync.Server.Config;
using SptModSync.Server.Http;
using SptModSync.Server.Sync;
using SptModSync.Shared.Globbing;
using SptModSync.Shared.Logging;
using SptModSync.Shared.Paths;

namespace SptModSync.Server;

[Injectable]
public class ModEntry : IOnLoad
{
    private readonly ISptLogger<ModEntry> _logger;
    private readonly ServerConfigService _configService = new();
    private readonly ManifestBuilder _manifestBuilder = new();

    private SptModSync.Shared.Models.ServerConfig? _config;
    private string _sptRootDirectory = "";
    private FileLog? _fileLog;

    public ModEntry(ISptLogger<ModEntry> logger)
    {
        _logger = logger;
    }

    /// <summary>Logs to both the SPT server's shared log and this mod's own
    /// SptModSync.Server.log, so a sync issue can be debugged from one file without wading
    /// through everything else the server logs.</summary>
    private void LogInfo(string message)
    {
        _logger.Info(message);
        _fileLog?.Write(message);
    }

    private void LogWarning(string message)
    {
        _logger.Warning(message);
        _fileLog?.Write($"WARN: {message}");
    }

    private void LogError(string message)
    {
        _logger.Error(message);
        _fileLog?.Write($"ERROR: {message}");
    }

    private void LogSuccess(string message)
    {
        _logger.Success(message);
        _fileLog?.Write(message);
    }

    public Task OnLoad()
    {
        try
        {
            var modDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
                                ?? throw new InvalidOperationException("Could not resolve mod directory.");

            _fileLog = new FileLog(Path.Combine(modDirectory, "SptModSync.Server.log"));
            FileLog.Current = _fileLog;

            _config = _configService.LoadOrCreate(modDirectory);

            if (_config.VerboseLogging)
            {
                try
                {
                    var location = Assembly.GetExecutingAssembly().Location;
                    LogInfo(string.IsNullOrEmpty(location)
                        ? "[SptModSync] Assembly has no on-disk location; build time unknown."
                        : $"[SptModSync] Build timestamp {File.GetLastWriteTime(location):yyyy-MM-dd HH:mm:ss} ({location})");
                }
                catch (Exception ex)
                {
                    LogInfo($"[SptModSync] Could not read build timestamp: {ex.Message}");
                }
            }

            if (!string.IsNullOrWhiteSpace(_config.SptRootDirectory))
            {
                _sptRootDirectory = Path.GetFullPath(_config.SptRootDirectory);
                if (!Directory.Exists(_sptRootDirectory))
                {
                    throw new DirectoryNotFoundException(
                        $"SptRootDirectory in serverConfig.json points at '{_sptRootDirectory}', which does not exist.");
                }

                if (_config.VerboseLogging)
                    LogInfo($"[SptModSync] Root set explicitly to '{_sptRootDirectory}'.");
            }
            else
            {
                _sptRootDirectory = SptRootLocator.FindRoot(modDirectory)
                                    ?? throw new InvalidOperationException(
                                        $"Could not auto-detect the SPT root above '{modDirectory}'. " +
                                        "Set SptRootDirectory in serverConfig.json to the folder you want to serve from.");

                if (_config.VerboseLogging)
                    LogInfo($"[SptModSync] Root auto-detected as '{_sptRootDirectory}'.");
            }

            if (_config.VerboseLogging)
            {
                LogInfo($"[SptModSync] Include patterns: {string.Join(", ", _config.IncludePatterns)}");
                LogInfo($"[SptModSync] Exclude patterns: {string.Join(", ", _config.ExcludePatterns)}");
            }

            SptRouteListener.Handler = new SyncRequestHandler(
                _sptRootDirectory, _config, _manifestBuilder, msg => LogInfo($"[SptModSync] {msg}"));

            var fileCount = GlobMatcher
                .ResolveIncludedFiles(_sptRootDirectory, _config.IncludePatterns, _config.ExcludePatterns)
                .Count;

            if (fileCount == 0)
            {
                LogWarning(
                    $"[SptModSync] Manifest is EMPTY - scanned '{_sptRootDirectory}' and no file matched " +
                    "the include patterns (or everything matched was excluded).");

                foreach (var line in _manifestBuilder.DiagnoseEmptyScan(_sptRootDirectory, _config))
                {
                    LogWarning($"[SptModSync] {line}");
                }
            }

            LogSuccess($"[SptModSync] Ready - sharing {fileCount} file(s) on the SPT server's own port.");
        }
        catch (Exception ex)
        {
            LogError($"[SptModSync] Failed to start: {ex}");
        }

        return Task.CompletedTask;
    }
}
