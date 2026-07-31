using System;
using System.IO;
using System.Threading.Tasks;
using System.Text.Json;
using SPT.Common.Http;
using SptModSync.Shared.Json;
using SptModSync.Shared.Models;

namespace SptModSync.Client.Sync
{
    public sealed class SptTransport
    {
        public const string RoutePrefix = "/sptmodsync";
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

            var previousTimeout = RequestHandler.HttpClient.HttpClient.Timeout;
            RequestHandler.HttpClient.HttpClient.Timeout = TimeSpan.FromMinutes(15);

            try
            {
                await RequestHandler.HttpClient.DownloadAsync(route, stagingAbsolutePath, null).ConfigureAwait(false);
            }
            finally
            {
                RequestHandler.HttpClient.HttpClient.Timeout = previousTimeout;
            }
        }
    }
}
