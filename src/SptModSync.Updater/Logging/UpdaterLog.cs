namespace SptModSync.Updater.Logging;

public sealed class UpdaterLog : IDisposable
{
    private readonly StreamWriter _writer;

    public UpdaterLog(string logPath)
    {
        _writer = new StreamWriter(logPath, append: true) { AutoFlush = true };
        Write($"--- run started {DateTime.Now:O} ---");
    }

    public void Write(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {message}";
        Console.WriteLine(line);
        _writer.WriteLine(line);
    }

    public void Dispose() => _writer.Dispose();
}
