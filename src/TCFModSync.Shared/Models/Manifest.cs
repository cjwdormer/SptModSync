namespace TCFModSync.Shared.Models;

public sealed class ManifestEntry
{
    public string RelativePath { get; set; } = "";

    public long Size { get; set; }

    public string Hash { get; set; } = "";
}

public sealed class Manifest
{
    public string ServerConfigVersion { get; set; } = "";
    public List<ManifestEntry> Files { get; set; } = new();
    public List<string> FileHashBlacklist { get; set; } = new();

    public List<string> ExcludedPaths { get; set; } = new();

    public string SptVersion { get; set; } = "";
}
