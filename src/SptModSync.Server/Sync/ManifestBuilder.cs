using SptModSync.Shared.Globbing;
using SptModSync.Shared.Hashing;
using SptModSync.Shared.Models;

namespace SptModSync.Server.Sync;

public sealed class ManifestBuilder
{
    private sealed class CachedHash
    {
        public long Size;
        public DateTime LastWriteUtc;
        public string Hash = "";
    }

    private readonly Dictionary<string, CachedHash> _hashCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _cacheLock = new();

    private string GetHash(string relativePath, string absolutePath, FileInfo info)
    {
        lock (_cacheLock)
        {
            if (_hashCache.TryGetValue(relativePath, out var cached)
                && cached.Size == info.Length
                && cached.LastWriteUtc == info.LastWriteTimeUtc)
            {
                return cached.Hash;
            }
        }

        var hash = FileHasher.HashFile(absolutePath);

        lock (_cacheLock)
        {
            _hashCache[relativePath] = new CachedHash
            {
                Size = info.Length,
                LastWriteUtc = info.LastWriteTimeUtc,
                Hash = hash
            };
        }

        return hash;
    }

    public List<string> DiagnoseEmptyScan(string sptRootDirectory, ServerConfig config)
    {
        var lines = new List<string>();

        foreach (var pattern in config.IncludePatterns)
        {
            var normalized = pattern.Replace('\\', '/');
            var wildcardAt = normalized.IndexOfAny(new[] { '*', '?' });

            if (wildcardAt < 0)
            {
                var literalPath = Path.Combine(sptRootDirectory, normalized.Replace('/', Path.DirectorySeparatorChar));
                lines.Add(File.Exists(literalPath)
                    ? $"  '{pattern}' -> file exists"
                    : $"  '{pattern}' -> FILE NOT FOUND at {literalPath}");
                continue;
            }

            var lastSlash = normalized.LastIndexOf('/', Math.Max(wildcardAt - 1, 0));
            var prefix = lastSlash > 0 ? normalized.Substring(0, lastSlash) : "";
            var prefixPath = string.IsNullOrEmpty(prefix)
                ? sptRootDirectory
                : Path.Combine(sptRootDirectory, prefix.Replace('/', Path.DirectorySeparatorChar));

            if (!Directory.Exists(prefixPath))
            {
                lines.Add($"  '{pattern}' -> DIRECTORY DOES NOT EXIST: {prefixPath}");
                continue;
            }

            var fileCount = Directory.EnumerateFiles(prefixPath, "*", SearchOption.AllDirectories).Count();
            lines.Add(fileCount == 0
                ? $"  '{pattern}' -> directory exists but is EMPTY: {prefixPath}"
                : $"  '{pattern}' -> directory contains {fileCount} file(s); all were excluded or unmatched: {prefixPath}");
        }

        return lines;
    }

    public Manifest Build(string sptRootDirectory, ServerConfig config, string sptVersion, bool headless = false)
    {
        var candidatePaths = GlobMatcher.ResolveIncludedFiles(
            sptRootDirectory, config.IncludePatterns, Enumerable.Empty<string>());

        var relativePaths = GlobMatcher.FilterOutExcluded(candidatePaths, config.ExcludePatterns);

        var excludedPaths = candidatePaths
            .Except(relativePaths, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (headless)
        {
            var headlessIncludeMatcher = new PatternMatcher(config.HeadlessIncludePatterns);
            var headlessExcludeMatcher = new PatternMatcher(config.HeadlessExcludePatterns);

            var beforeHeadlessFilter = relativePaths;
            relativePaths = beforeHeadlessFilter
                .Where(path => headlessIncludeMatcher.Matches(path))
                .Where(path => !headlessExcludeMatcher.Matches(path))
                .ToList();

            var droppedByHeadlessFilter = beforeHeadlessFilter
                .Except(relativePaths, StringComparer.OrdinalIgnoreCase);

            excludedPaths = excludedPaths
                .Concat(droppedByHeadlessFilter)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        var files = new List<ManifestEntry>(relativePaths.Count);
        foreach (var relativePath in relativePaths)
        {
            var absolutePath = Path.Combine(sptRootDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
            var info = new FileInfo(absolutePath);
            if (!info.Exists) continue;

            files.Add(new ManifestEntry
            {
                RelativePath = relativePath,
                Size = info.Length,
                Hash = GetHash(relativePath, absolutePath, info)
            });
        }

        return new Manifest
        {
            ServerConfigVersion = config.ConfigVersion,
            Files = files,
            FileHashBlacklist = config.FileHashBlacklist,
            ExcludedPaths = excludedPaths,
            SptVersion = sptVersion
        };
    }
}
