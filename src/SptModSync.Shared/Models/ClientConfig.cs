namespace SptModSync.Shared.Models;

public sealed class ClientConfig
{
    public string ConfigVersion { get; set; } = "1.0.0";

    public List<string> ExcludePatterns { get; set; } = new();

    public List<string> IncludePatterns { get; set; } = new();

    public Dictionary<string, string> TrackedFiles { get; set; } = new();

    public bool HeadlessAutoAccept { get; set; } = false;
}
