using SptModSync.Shared.Globbing;
using SptModSync.Shared.Models;

namespace SptModSync.Shared.Diffing;

public sealed class DiffResult
{
    public string RelativePath { get; set; } = "";
    public FileAction Action { get; set; }
    public string? ServerHash { get; set; }
    public long? Size { get; set; }

    public bool UserCanDecline => Action != FileAction.Blacklist;
}

public static class DiffEngine
{
    public static List<DiffResult> BuildDiff(
        Manifest manifest,
        IReadOnlyDictionary<string, string> localFileHashes,
        ClientConfig clientConfig)
    {
        var results = new List<DiffResult>();
        var serverPaths = new HashSet<string>(manifest.Files.Select(f => f.RelativePath), StringComparer.OrdinalIgnoreCase);
        var serverExcludedPaths = new HashSet<string>(manifest.ExcludedPaths, StringComparer.OrdinalIgnoreCase);

        var hasAllowList = clientConfig.IncludePatterns.Count > 0;
        var excludeMatcher = new PatternMatcher(clientConfig.ExcludePatterns);
        var includeMatcher = new PatternMatcher(clientConfig.IncludePatterns);

        foreach (var entry in manifest.Files)
        {
            var isBlacklisted = manifest.FileHashBlacklist.Contains(entry.Hash, StringComparer.OrdinalIgnoreCase);
            var isUserExcluded = excludeMatcher.Matches(entry.RelativePath);
            var existsLocally = localFileHashes.TryGetValue(entry.RelativePath, out var localHash);

            FileAction action;

            var isAllowed = !hasAllowList || includeMatcher.Matches(entry.RelativePath);

            if (isBlacklisted && existsLocally)
            {
                action = FileAction.Blacklist;
            }
            else if (!isAllowed)
            {
                if (!clientConfig.TrackedFiles.ContainsKey(entry.RelativePath)) continue;

                action = existsLocally ? FileAction.Delete : FileAction.Untrack;
            }
            else if (isUserExcluded)
            {
                action = FileAction.Untrack;
            }
            else if (!existsLocally)
            {
                action = FileAction.Add;
            }
            else if (!string.Equals(localHash, entry.Hash, StringComparison.OrdinalIgnoreCase))
            {
                action = FileAction.Update;
            }
            else if (!clientConfig.TrackedFiles.ContainsKey(entry.RelativePath))
            {
                action = FileAction.Adopt;
            }
            else
            {
                continue;
            }

            results.Add(new DiffResult
            {
                RelativePath = entry.RelativePath,
                Action = action,
                ServerHash = entry.Hash,
                Size = entry.Size
            });
        }

        foreach (var trackedPath in clientConfig.TrackedFiles.Keys)
        {
            if (serverPaths.Contains(trackedPath)) continue;

            var isUserExcluded = excludeMatcher.Matches(trackedPath);
            if (isUserExcluded) continue;

            if (serverExcludedPaths.Contains(trackedPath))
            {
                results.Add(new DiffResult { RelativePath = trackedPath, Action = FileAction.Untrack });
                continue;
            }

            var existsLocally = localFileHashes.ContainsKey(trackedPath);

            results.Add(new DiffResult
            {
                RelativePath = trackedPath,
                Action = existsLocally ? FileAction.Delete : FileAction.Untrack
            });
        }

        return results.OrderBy(r => r.RelativePath, StringComparer.OrdinalIgnoreCase).ToList();
    }
}
