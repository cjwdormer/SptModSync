using SptModSync.Shared.Globbing;
using Xunit;

namespace SptModSync.Shared.Tests;

public class GlobMatcherTests : IDisposable
{
    private readonly string _root;

    public GlobMatcherTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "SptModSyncGlobTest_" + Guid.NewGuid().ToString("N"));

        CreateFile("BepInEx/plugins/SomeMod/SomeMod.dll");
        CreateFile("BepInEx/plugins/AnotherMod.dll");
        CreateFile("BepInEx/plugins/spt/SptPlugin.dll");
        CreateFile("BepInEx/patchers/SomePatcher.dll");
        CreateFile("BepInEx/plugins/SomeMod/debug.log");
        CreateFile("SptModSync.Updater.exe");
        CreateFile("EscapeFromTarkov.exe");
        CreateFile("BepInEx/plugins/SAIN/SAIN.dll");
        CreateFile("BepInEx/plugins/SAIN/Presets/custom.json");
        CreateFile("BepInEx/patchers/spt-prepatch.dll");
        CreateFile("BepInEx/plugins/WTT/MAIN_...and survived_prod.webm");
    }

    private void CreateFile(string relativePath)
    {
        var full = Path.Combine(_root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, "test");
    }

    private static readonly string[] DefaultIncludes =
    {
        "SptModSync.Updater.exe",
        "BepInEx/patchers/**/*",
        "BepInEx/plugins/**/*"
    };

    private static readonly string[] DefaultExcludes =
    {
        "**/*.log",
        "BepInEx/plugins/SAIN/**/*.json",
        "BepInEx/patchers/spt-prepatch.dll",
        "BepInEx/plugins/spt/**/*"
    };

    [Fact]
    public void DefaultPatterns_FindModsInPluginsFolder()
    {
        var files = GlobMatcher.ResolveIncludedFiles(_root, DefaultIncludes, DefaultExcludes);

        Assert.Contains("BepInEx/plugins/SomeMod/SomeMod.dll", files);
        Assert.Contains("BepInEx/patchers/SomePatcher.dll", files);
        Assert.Contains("SptModSync.Updater.exe", files);
    }

    [Fact]
    public void DoubleStar_AlsoMatchesFilesDirectlyInTheFolder()
    {
        var files = GlobMatcher.ResolveIncludedFiles(_root, DefaultIncludes, DefaultExcludes);

        Assert.Contains("BepInEx/plugins/AnotherMod.dll", files);
    }

    [Fact]
    public void ExcludedPaths_AreNotReturned()
    {
        var files = GlobMatcher.ResolveIncludedFiles(_root, DefaultIncludes, DefaultExcludes);

        Assert.DoesNotContain("BepInEx/plugins/spt/SptPlugin.dll", files);
        Assert.DoesNotContain("BepInEx/plugins/SomeMod/debug.log", files);
    }

    [Fact]
    public void SainJsonExcluded_ButSainDllStillSyncs()
    {
        var files = GlobMatcher.ResolveIncludedFiles(_root, DefaultIncludes, DefaultExcludes);

        Assert.DoesNotContain("BepInEx/plugins/SAIN/Presets/custom.json", files);
        Assert.Contains("BepInEx/plugins/SAIN/SAIN.dll", files);
    }

    [Fact]
    public void SptPrepatcher_IsExcluded_ButOtherPatchersSync()
    {
        var files = GlobMatcher.ResolveIncludedFiles(_root, DefaultIncludes, DefaultExcludes);

        Assert.DoesNotContain("BepInEx/patchers/spt-prepatch.dll", files);
        Assert.Contains("BepInEx/patchers/SomePatcher.dll", files);
    }

    [Fact]
    public void FilesOutsideIncludePatterns_AreNotReturned()
    {
        var files = GlobMatcher.ResolveIncludedFiles(_root, DefaultIncludes, DefaultExcludes);

        Assert.DoesNotContain("EscapeFromTarkov.exe", files);
    }

    [Fact]
    public void FilenamesContainingConsecutiveDots_AreStillIncluded()
    {
        var files = GlobMatcher.ResolveIncludedFiles(_root, DefaultIncludes, DefaultExcludes);

        Assert.Contains("BepInEx/plugins/WTT/MAIN_...and survived_prod.webm", files);
    }

    [Fact]
    public void ParentDirectorySegments_AreRejected_ButDottedNamesAreNot()
    {
        Assert.False(GlobMatcher.IsSafeRelativePattern("BepInEx/../../secrets/**/*"));
        Assert.False(GlobMatcher.IsSafeRelativePattern("../outside/**/*"));
        Assert.True(GlobMatcher.IsSafeRelativePattern("BepInEx/plugins/MAIN_...and survived.webm"));
        Assert.True(GlobMatcher.IsSafeRelativePattern("BepInEx/plugins/**/*"));
    }

    [Fact]
    public void ReturnedPaths_UseForwardSlashes()
    {
        var files = GlobMatcher.ResolveIncludedFiles(_root, DefaultIncludes, DefaultExcludes);

        Assert.All(files, f => Assert.DoesNotContain('\\', f));
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { }
    }
}
