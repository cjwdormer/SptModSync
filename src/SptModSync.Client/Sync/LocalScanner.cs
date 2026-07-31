using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SptModSync.Shared.Hashing;
using SptModSync.Shared.Models;

namespace SptModSync.Client.Sync
{
    public sealed class LocalScanner
    {
        private readonly string _gameRootDirectory;

        public LocalScanner(string gameRootDirectory)
        {
            _gameRootDirectory = gameRootDirectory;
        }

        public async Task<Dictionary<string, string>> HashKnownPathsAsync(
            Manifest manifest, ClientConfig clientConfig, Action<string>? log = null, CancellationToken ct = default)
        {
            var result = new Dictionary<string, string>();
            var candidates = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var f in manifest.Files) candidates.Add(f.RelativePath);
            foreach (var p in clientConfig.TrackedFiles.Keys) candidates.Add(p);

            var existing = candidates
                .Select(rel => (rel, abs: Path.Combine(_gameRootDirectory, rel.Replace('/', Path.DirectorySeparatorChar))))
                .Where(x => File.Exists(x.abs))
                .ToList();

            var totalBytes = existing.Sum(x => new FileInfo(x.abs).Length);
            log?.Invoke($"[SptModSync] Hashing {existing.Count} local file(s), {totalBytes / 1024.0 / 1024.0:F0} MB total...");

            var done = 0;
            var lastReport = DateTime.UtcNow;
            foreach (var (relativePath, absolutePath) in existing)
            {
                var hash = await FileHasher.HashFileAsync(absolutePath, ct).ConfigureAwait(false);
                result[relativePath] = hash;
                done++;

                if ((DateTime.UtcNow - lastReport).TotalSeconds >= 5)
                {
                    log?.Invoke($"[SptModSync] Hashed {done}/{existing.Count} local file(s)...");
                    lastReport = DateTime.UtcNow;
                }
            }

            log?.Invoke($"[SptModSync] Local scan complete ({existing.Count} file(s) hashed).");
            return result;
        }
    }
}
