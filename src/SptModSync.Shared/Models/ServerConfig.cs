namespace SptModSync.Shared.Models;

public sealed class ServerConfig
{
    public string ConfigVersion { get; set; } = "1.0.0";

    public List<string> IncludePatterns { get; set; } = new()
    {
        "SptModSync.Updater.exe",
        "BepInEx/patchers/**/*",
        "BepInEx/plugins/**/*"
    };

    public List<string> ExcludePatterns { get; set; } = new()
    {
        "**/*.log",
        "BepInEx/plugins/SAIN/**/*.json",
        "BepInEx/patchers/spt-prepatch.dll",
        "BepInEx/plugins/spt/**/*",
        "BepInEx/plugins/DynamicMaps/**/*"
    };

    public List<string> FileHashBlacklist { get; set; } = new();

    public List<string> HeadlessIncludePatterns { get; set; } = new();

    public List<string> HeadlessExcludePatterns { get; set; } = new();

    public string SptRootDirectory { get; set; } = "";

    public bool VerboseLogging { get; set; } = false;

    public string SptVersion { get; set; } = "4.0.13";
}
