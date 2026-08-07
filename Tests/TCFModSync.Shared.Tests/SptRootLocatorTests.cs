using TCFModSync.Shared.Paths;
using Xunit;

namespace TCFModSync.Shared.Tests;

public class SptRootLocatorTests : IDisposable
{
    private readonly string _temp;

    public SptRootLocatorTests()
    {
        _temp = Path.Combine(Path.GetTempPath(), "SptRootTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_temp);
    }

    private string Dir(params string[] parts)
    {
        var path = Path.Combine(new[] { _temp }.Concat(parts).ToArray());
        Directory.CreateDirectory(path);
        return path;
    }

    private void File_(params string[] parts)
    {
        var path = Path.Combine(new[] { _temp }.Concat(parts).ToArray());
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "x");
    }

    [Fact]
    public void DedicatedServer_RootIsFolderContainingBepInEx_NotTheServerAppFolder()
    {
        Dir("root", "BepInEx", "plugins");
        Dir("root", "SPT", "SPT_Data");
        var modDir = Dir("root", "SPT", "user", "mods", "TCF-ModSync.Server");

        var found = SptRootLocator.FindRoot(modDir);

        Assert.Equal(Path.Combine(_temp, "root"), found);
    }

    [Fact]
    public void SharedInstall_WhereClientAndServerLiveTogether()
    {
        Dir("root", "BepInEx", "plugins");
        File_("root", "EscapeFromTarkov.exe");
        var modDir = Dir("root", "user", "mods", "TCF-ModSync.Server");

        var found = SptRootLocator.FindRoot(modDir);

        Assert.Equal(Path.Combine(_temp, "root"), found);
    }

    [Fact]
    public void ClientPlugin_ResolvesToGameRoot()
    {
        Dir("game", "BepInEx", "plugins", "TCF-ModSync.Client");
        File_("game", "EscapeFromTarkov.exe");
        var pluginDir = Path.Combine(_temp, "game", "BepInEx", "plugins", "TCF-ModSync.Client");

        var found = SptRootLocator.FindRoot(pluginDir);

        Assert.Equal(Path.Combine(_temp, "game"), found);
    }

    [Fact]
    public void NoLandmarksAnywhere_ReturnsNull()
    {
        var orphan = Dir("nothing", "here", "at", "all");

        var found = SptRootLocator.FindRoot(orphan);

        Assert.Null(found);
    }

    public void Dispose()
    {
        try { Directory.Delete(_temp, recursive: true); } catch { }
    }
}
