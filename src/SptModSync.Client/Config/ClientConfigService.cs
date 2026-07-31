using System;
using System.IO;
using System.Text.Json;
using SptModSync.Shared.Models;

namespace SptModSync.Client.Config
{
    public sealed class ClientConfigService
    {
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions { WriteIndented = true };

        private static readonly JsonSerializerOptions ReadOptions = new JsonSerializerOptions
        {
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };

        public ClientConfig LoadOrCreate(string configPath)
        {
            var dir = Path.GetDirectoryName(configPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            if (!File.Exists(configPath))
            {
                Save(configPath, new ClientConfig());
            }

            var json = File.ReadAllText(configPath);
            return JsonSerializer.Deserialize<ClientConfig>(json, ReadOptions) ?? new ClientConfig();
        }

        public void Save(string configPath, ClientConfig config)
        {
            var json = JsonSerializer.Serialize(config, JsonOptions);
            var tmpPath = configPath + ".tmp";
            File.WriteAllText(tmpPath, json);
            if (File.Exists(configPath)) File.Delete(configPath);
            File.Move(tmpPath, configPath);
        }
    }
}
