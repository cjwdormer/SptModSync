using System;
using System.IO;
using System.Threading.Tasks;
using System.Text.Json;
using SPT.Common.Http;
using TCFModSync.Shared.Json;
using TCFModSync.Shared.Models;

namespace TCFModSync.Client.Sync
{
    public sealed class SptTransport
    {
        public const string RoutePrefix = "/tcfmodsync";
        public const string RouteManifest = "/manifest";
        public const string RouteFile = "/file";

        private readonly Action<string> _log;

        public SptTransport(Action<string> log)
        {
            _log = log;
        }

        public async Task<Manifest> GetManifestAsync(bool headless)
        {
            var route = $"{RoutePrefix}{RouteManifest}" + (headless ? "?headless=true" : "");
            var json = await RequestHandler.GetJsonAsync(route).ConfigureAwait(false);

            if (string.IsNullOrWhiteSpace(json))
                throw new InvalidOperationException($"Empty response from {route}.");

            return JsonSerializer.Deserialize<Manifest>(json, JsonDefaults.Options)
                   ?? throw new InvalidOperationException($"Could not parse the manifest from {route}.");
        }

        public async Task DownloadToAsync(string relativePath, string stagingAbsolutePath)
        {
            var directory = Path.GetDirectoryName(stagingAbsolutePath);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            var route = $"{RoutePrefix}{RouteFile}/" + Uri.EscapeDataString(relativePath);

            await RequestHandler.HttpClient.DownloadAsync(route, stagingAbsolutePath, null).ConfigureAwait(false);
        }

        public static IDisposable ExtendedDownloadTimeout(TimeSpan? timeout = null)
        {
            var client = RequestHandler.HttpClient.HttpClient;
            var previous = client.Timeout;
            client.Timeout = timeout ?? TimeSpan.FromMinutes(15);
            return new TimeoutScope(client, previous);
        }

        private sealed class TimeoutScope : IDisposable
        {
            private readonly System.Net.Http.HttpClient _client;
            private readonly TimeSpan _previous;

            public TimeoutScope(System.Net.Http.HttpClient client, TimeSpan previous)
            {
                _client = client;
                _previous = previous;
            }

            public void Dispose() => _client.Timeout = _previous;
        }
    }
}
