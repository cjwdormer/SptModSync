namespace SptModSync.Shared.Logging;

/// <summary>
/// A small, dependency-free file logger each side of SptModSync (Client, Server) uses to keep its
/// own debug log independent of the host's shared log (BepInEx's LogOutput.log on the client, the
/// SPT server's own log on the server side). That way a sync problem can be diagnosed from one file
/// without digging through everything else those hosts log.
/// </summary>
public sealed class FileLog : IDisposable
{
    private readonly object _lock = new();
    private readonly StreamWriter? _writer;

    public FileLog(string logPath)
    {
        try
        {
            var directory = Path.GetDirectoryName(logPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            _writer = new StreamWriter(logPath, append: true) { AutoFlush = true };
            WriteLine($"--- run started {DateTime.Now:O} ---");
        }
        catch
        {
            _writer = null;
        }
    }

    public static FileLog? Current { get; set; }

    public void Write(string message) => WriteLine($"[{DateTime.Now:HH:mm:ss}] {message}");

    private void WriteLine(string line)
    {
        lock (_lock)
        {
            _writer?.WriteLine(line);
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _writer?.Dispose();
        }
    }
}
