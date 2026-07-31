using SptModSync.Shared.Diffing;
using SptModSync.Shared.Models;
using Xunit;

namespace SptModSync.Shared.Tests;

public class DiffEngineTests
{
    private static Manifest ManifestWith(params (string path, string hash)[] files) => new()
    {
        Files = files.Select(f => new ManifestEntry { RelativePath = f.path, Hash = f.hash, Size = 1 }).ToList(),
        FileHashBlacklist = new List<string>()
    };

    [Fact]
    public void NewFile_NotLocal_IsAdd()
    {
        var manifest = ManifestWith(("mod.dll", "hash1"));
        var result = DiffEngine.BuildDiff(manifest, new Dictionary<string, string>(), new ClientConfig());

        Assert.Single(result);
        Assert.Equal(FileAction.Add, result[0].Action);
    }

    [Fact]
    public void ExistingFile_HashMismatch_IsUpdate()
    {
        var manifest = ManifestWith(("mod.dll", "hash_new"));
        var local = new Dictionary<string, string> { ["mod.dll"] = "hash_old" };

        var result = DiffEngine.BuildDiff(manifest, local, new ClientConfig());

        Assert.Equal(FileAction.Update, result[0].Action);
    }

    [Fact]
    public void MatchingHash_NotYetTracked_IsAdopt()
    {
        var manifest = ManifestWith(("mod.dll", "hash1"));
        var local = new Dictionary<string, string> { ["mod.dll"] = "hash1" };

        var result = DiffEngine.BuildDiff(manifest, local, new ClientConfig());

        Assert.Equal(FileAction.Adopt, result[0].Action);
    }

    [Fact]
    public void MatchingHash_AlreadyTracked_ProducesNothing()
    {
        var manifest = ManifestWith(("mod.dll", "hash1"));
        var local = new Dictionary<string, string> { ["mod.dll"] = "hash1" };
        var config = new ClientConfig { TrackedFiles = { ["mod.dll"] = "hash1" } };

        var result = DiffEngine.BuildDiff(manifest, local, config);

        Assert.Empty(result);
    }

    [Fact]
    public void TrackedFile_NoLongerOnServer_IsDelete()
    {
        var manifest = ManifestWith();
        var config = new ClientConfig { TrackedFiles = { ["old.dll"] = "hash1" } };

        var result = DiffEngine.BuildDiff(manifest, new Dictionary<string, string>(), config);

        Assert.Equal(FileAction.Delete, result[0].Action);
    }

    [Fact]
    public void TrackedFile_GoneFromServerAndAlreadyAbsentLocally_IsUntrack_NotDelete()
    {
        var manifest = ManifestWith();
        var config = new ClientConfig { TrackedFiles = { ["removed.dll"] = "hash1" } };

        var result = DiffEngine.BuildDiff(manifest, new Dictionary<string, string>(), config);

        Assert.Equal(FileAction.Untrack, result[0].Action);
    }

    [Fact]
    public void TrackedFile_ExcludedByServer_ButStillPresentThere_IsUntrack_NotDelete()
    {
        var manifest = ManifestWith();
        manifest.ExcludedPaths.Add("BepInEx/plugins/DynamicMaps/DynamicMaps.dll");
        var local = new Dictionary<string, string> { ["BepInEx/plugins/DynamicMaps/DynamicMaps.dll"] = "hash1" };
        var config = new ClientConfig
        {
            TrackedFiles = { ["BepInEx/plugins/DynamicMaps/DynamicMaps.dll"] = "hash1" }
        };

        var result = DiffEngine.BuildDiff(manifest, local, config);

        Assert.Equal(FileAction.Untrack, result[0].Action);
    }

    [Fact]
    public void UserDeletedFile_StillOfferedByServer_IsAddedBack()
    {
        var manifest = ManifestWith(("mod.dll", "hash1"));
        var config = new ClientConfig { TrackedFiles = { ["mod.dll"] = "hash1" } };

        var result = DiffEngine.BuildDiff(manifest, new Dictionary<string, string>(), config);

        Assert.Single(result);
        Assert.Equal(FileAction.Add, result[0].Action);
    }

    [Fact]
    public void UserExcludedFile_IsUntrack_NotAdd()
    {
        var manifest = ManifestWith(("mod.dll", "hash1"));
        var config = new ClientConfig { ExcludePatterns = { "mod.dll" } };

        var result = DiffEngine.BuildDiff(manifest, new Dictionary<string, string>(), config);

        Assert.Equal(FileAction.Untrack, result[0].Action);
        Assert.True(result[0].UserCanDecline);
    }

    [Fact]
    public void AllowList_IgnoresFilesOutsideIt()
    {
        var manifest = ManifestWith(("BepInEx/plugins/SAIN/SAIN.dll", "h1"), ("BepInEx/plugins/Other/Other.dll", "h2"));
        var config = new ClientConfig { IncludePatterns = { "BepInEx/plugins/SAIN/**/*" } };

        var result = DiffEngine.BuildDiff(manifest, new Dictionary<string, string>(), config);

        Assert.Single(result);
        Assert.Equal("BepInEx/plugins/SAIN/SAIN.dll", result[0].RelativePath);
    }

    [Fact]
    public void AllowList_RemovesPreviouslySyncedFilesNowOutOfScope()
    {
        var manifest = ManifestWith(("BepInEx/plugins/Other/Other.dll", "h2"));
        var local = new Dictionary<string, string> { ["BepInEx/plugins/Other/Other.dll"] = "h2" };
        var config = new ClientConfig
        {
            IncludePatterns = { "BepInEx/plugins/SAIN/**/*" },
            TrackedFiles = { ["BepInEx/plugins/Other/Other.dll"] = "h2" }
        };

        var result = DiffEngine.BuildDiff(manifest, local, config);

        Assert.Equal(FileAction.Delete, result[0].Action);
    }

    [Fact]
    public void AllowList_LeavesUntrackedLocalFilesAlone()
    {
        var manifest = ManifestWith(("BepInEx/plugins/Other/Other.dll", "h2"));
        var local = new Dictionary<string, string> { ["BepInEx/plugins/Other/Other.dll"] = "h2" };
        var config = new ClientConfig { IncludePatterns = { "BepInEx/plugins/SAIN/**/*" } };

        var result = DiffEngine.BuildDiff(manifest, local, config);

        Assert.Empty(result);
    }

    [Fact]
    public void Blacklist_OutranksAllowList()
    {
        var manifest = ManifestWith(("BepInEx/plugins/Other/cheat.dll", "bad"));
        manifest.FileHashBlacklist.Add("bad");
        var local = new Dictionary<string, string> { ["BepInEx/plugins/Other/cheat.dll"] = "bad" };
        var config = new ClientConfig { IncludePatterns = { "BepInEx/plugins/SAIN/**/*" } };

        var result = DiffEngine.BuildDiff(manifest, local, config);

        Assert.Equal(FileAction.Blacklist, result[0].Action);
    }

    [Fact]
    public void BlacklistedHash_ExistingLocally_IsBlacklist_AndCannotBeDeclined()
    {
        var manifest = ManifestWith(("cheat.dll", "bad_hash"));
        manifest.FileHashBlacklist.Add("bad_hash");
        var local = new Dictionary<string, string> { ["cheat.dll"] = "bad_hash" };

        var result = DiffEngine.BuildDiff(manifest, local, new ClientConfig());

        Assert.Equal(FileAction.Blacklist, result[0].Action);
        Assert.False(result[0].UserCanDecline);
    }
}
