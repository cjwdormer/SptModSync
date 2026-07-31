using System.Text.Json;
using SptModSync.Shared.Json;
using SptModSync.Shared.Models;
using Xunit;

namespace SptModSync.Shared.Tests;

public class ManifestSerializationTests
{
    private const string CamelCasePayload = """
        {
          "serverConfigVersion": "1.0.0",
          "sptVersion": "4.0.13",
          "fileHashBlacklist": ["deadbeef"],
          "files": [
            { "relativePath": "BepInEx/plugins/SomeMod.dll", "size": 2048, "hash": "abc123" }
          ]
        }
        """;

    [Fact]
    public void CamelCaseManifest_BindsCorrectly()
    {
        var manifest = JsonSerializer.Deserialize<Manifest>(CamelCasePayload, JsonDefaults.Options);

        Assert.NotNull(manifest);
        Assert.Single(manifest!.Files);
        Assert.Equal("BepInEx/plugins/SomeMod.dll", manifest.Files[0].RelativePath);
        Assert.Equal(2048, manifest.Files[0].Size);
        Assert.Equal("abc123", manifest.Files[0].Hash);
        Assert.Equal("4.0.13", manifest.SptVersion);
        Assert.Single(manifest.FileHashBlacklist);
    }

    [Fact]
    public void PascalCaseManifest_AlsoBinds()
    {
        var pascal = CamelCasePayload
            .Replace("\"files\"", "\"Files\"")
            .Replace("\"relativePath\"", "\"RelativePath\"")
            .Replace("\"size\"", "\"Size\"")
            .Replace("\"hash\"", "\"Hash\"");

        var manifest = JsonSerializer.Deserialize<Manifest>(pascal, JsonDefaults.Options);

        Assert.Single(manifest!.Files);
        Assert.Equal("abc123", manifest.Files[0].Hash);
    }

    [Fact]
    public void RoundTrip_PreservesEveryFile()
    {
        var original = new Manifest
        {
            ServerConfigVersion = "1.0.0",
            SptVersion = "4.0.13",
            Files =
            {
                new ManifestEntry { RelativePath = "a/b.dll", Size = 1, Hash = "h1" },
                new ManifestEntry { RelativePath = "c/d.dll", Size = 2, Hash = "h2" }
            }
        };

        var json = JsonSerializer.Serialize(original, JsonDefaults.Options);
        var restored = JsonSerializer.Deserialize<Manifest>(json, JsonDefaults.Options);

        Assert.Equal(2, restored!.Files.Count);
        Assert.Equal("h2", restored.Files[1].Hash);
    }

    [Fact]
    public void DefaultOptions_WouldHaveMissedThis()
    {
        var manifest = JsonSerializer.Deserialize<Manifest>(CamelCasePayload);

        Assert.NotNull(manifest);
        Assert.Empty(manifest!.Files);
    }
}
