using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TCFModSync.Shared.Hashing;
using TCFModSync.Shared.Models;

namespace TCFModSync.Client.Sync
{
    public sealed class LocalScanner
    {
        private const int MaxConcurrentHashes = 4;

        private readonly string _gameRootDirectory;

        public LocalScanner(string gameRootDirectory)
        {
            _gameRootDirectory = gameRootDirectory;
        }

        public async Task<Dictionary<string, string>> HashKnownPathsAsync(
            Manifest manifest, ClientConfig clientConfig, Action<string>? log = null, CancellationToken ct = default)
        {
            var candidates = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var f in manifest.Files) candidates.Add(f.RelativePath);
            foreach (var p in clientConfig.TrackedFiles.Keys) candidates.Add(p);

            var existing = candidates
                .Select(rel => (rel, abs: Path.Combine(_gameRootDirectory, rel.Replace('/', Path.DirectorySeparatorChar))))
                .Where(x => File.Exists(x.abs))
                .ToList();

            var totalBytes = existing.Sum(x => new FileInfo(x.abs).Length);
            log?.Invoke($"[TCF-ModSync] Hashing {existing.Count} local file(s), {totalBytes / 1024.0 / 1024.0:F0} MB total...");

            var result = new ConcurrentDictionary<string, string>();
            var done = 0;
            var reportLock = new object();
            var lastReport = DateTime.UtcNow;

            using var throttle = new SemaphoreSlim(MaxConcurrentHashes);

            var hashTasks = existing.Select(async item =>
            {
                await throttle.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    var hash = await FileHasher.HashFileAsync(item.abs, ct).ConfigureAwait(false);
                    result[item.rel] = hash;

                    var current = Interlocked.Increment(ref done);
                    lock (reportLock)
                    {
                        if ((DateTime.UtcNow - lastReport).TotalSeconds >= 5)
                        {
                            log?.Invoke($"[TCF-ModSync] Hashed {current}/{existing.Count} local file(s)...");
                            lastReport = DateTime.UtcNow;
                        }
                    }
                }
                finally
                {
                    throttle.Release();
                }
            });

            await Task.WhenAll(hashTasks).ConfigureAwait(false);

            log?.Invoke($"[TCF-ModSync] Local scan complete ({existing.Count} file(s) hashed).");
            return new Dictionary<string, string>(result);
        }
    }
}
