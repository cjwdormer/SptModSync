namespace TCFModSync.Shared.Models;

public enum PendingOpKind
{
    CopyFromStaging,
    DeleteFile
}

public sealed class PendingOperation
{
    public PendingOpKind Kind { get; set; }

    public string RelativePath { get; set; } = "";

    public string? StagedAbsolutePath { get; set; }

    public string DestinationAbsolutePath { get; set; } = "";

    public string? ExpectedHash { get; set; }
}

public sealed class PendingOperations
{
    public int SchemaVersion { get; set; } = 2;

    public int WaitForProcessId { get; set; }

    public string RelaunchExecutable { get; set; } = "";

    public string RelaunchArguments { get; set; } = "";

    public List<PendingOperation> Operations { get; set; } = new();

    public List<string> StagingDirectories { get; set; } = new();

    public DateTime WrittenAtUtc { get; set; } = DateTime.UtcNow;
}
