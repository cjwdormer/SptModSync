using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;

namespace SptModSync.Shared.Globbing;

public static class GlobMatcher
{
    public static List<string> ResolveIncludedFiles(string rootDirectory, IEnumerable<string> includePatterns,
        IEnumerable<string> excludePatterns)
    {
        var matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
        foreach (var pattern in includePatterns)
            matcher.AddInclude(pattern);
        foreach (var pattern in excludePatterns)
            matcher.AddExclude(pattern);

        var result = matcher.Execute(new DirectoryInfoWrapper(new DirectoryInfo(rootDirectory)));

        return result.Files
            .Select(f => f.Path.Replace('\\', '/'))
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static List<string> FilterOutExcluded(IEnumerable<string> relativePaths, IEnumerable<string> excludePatterns)
    {
        var excludeList = excludePatterns.ToList();
        var paths = relativePaths.ToList();
        if (excludeList.Count == 0) return paths;

        var matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
        matcher.AddInclude("**/*");
        foreach (var pattern in excludeList)
            matcher.AddExclude(pattern);

        return matcher.Match(paths).Files
            .Select(f => f.Path)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static bool MatchesAny(string relativePath, IEnumerable<string> patterns)
    {
        var patternList = patterns.ToList();
        if (patternList.Count == 0) return false;

        var matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
        foreach (var pattern in patternList)
            matcher.AddInclude(pattern);

        return matcher.Match(relativePath.Replace('\\', '/')).HasMatches;
    }

    public static bool IsSafeRelativePattern(string pattern)
    {
        if (string.IsNullOrWhiteSpace(pattern)) return false;

        if (pattern.Split('/', '\\').Any(segment => segment == "..")) return false;
        if (Path.IsPathRooted(pattern)) return false;
        if (pattern.Length >= 2 && pattern[1] == ':') return false;
        if (pattern.StartsWith("/") || pattern.StartsWith("\\")) return false;
        return true;
    }
}

public sealed class PatternMatcher
{
    private readonly Matcher? _matcher;

    public PatternMatcher(IEnumerable<string> patterns)
    {
        var patternList = patterns.ToList();
        if (patternList.Count == 0) return;

        _matcher = new Matcher(StringComparison.OrdinalIgnoreCase);
        foreach (var pattern in patternList)
            _matcher.AddInclude(pattern);
    }

    public bool Matches(string relativePath)
        => _matcher != null && _matcher.Match(relativePath.Replace('\\', '/')).HasMatches;
}
