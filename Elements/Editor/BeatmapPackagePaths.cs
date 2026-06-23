using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MLP_RiM.Elements.Editor;

public static class BeatmapPackagePaths
{
    public const string BeatmapsRoot = "Beatmaps";
    public const string ChartFileName = "chart.xml";
    public const string SongFileName = "song.mp3";
    public const string AssetsDirectoryName = "assets";

    public static string ResolveChartPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return Path.Combine(BeatmapsRoot, "Untitled", ChartFileName);

        return HasXmlExtension(path) ? path : Path.Combine(path, ChartFileName);
    }

    public static bool IsPackageChartPath(string chartPath)
    {
        return !string.IsNullOrWhiteSpace(chartPath)
            && string.Equals(Path.GetFileName(chartPath), ChartFileName, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(Path.GetDirectoryName(chartPath));
    }

    public static bool IsLegacyXmlChartPath(string chartPath)
    {
        return HasXmlExtension(chartPath) && !IsPackageChartPath(chartPath);
    }

    public static string GetPackagePath(string chartPath)
    {
        if (string.IsNullOrWhiteSpace(chartPath))
            return Path.Combine(BeatmapsRoot, "Untitled");

        if (IsPackageChartPath(chartPath))
            return Path.GetDirectoryName(chartPath);

        if (HasXmlExtension(chartPath))
        {
            string directory = Path.GetDirectoryName(chartPath);
            string name = Path.GetFileNameWithoutExtension(chartPath);
            return string.IsNullOrWhiteSpace(directory) ? name : Path.Combine(directory, name);
        }

        return chartPath;
    }

    public static string GetAssetsPath(string packagePath)
    {
        return Path.Combine(packagePath, AssetsDirectoryName);
    }

    public static string GetChartPathForPackage(string packagePath)
    {
        return Path.Combine(packagePath, ChartFileName);
    }

    public static string GetSongPathForPackage(string packagePath)
    {
        return Path.Combine(packagePath, SongFileName);
    }

    public static string GetAvailablePackageChartPath(string beatmapName)
    {
        return GetAvailablePackageChartPathForPackagePath(GetPackagePathFromUserInput(beatmapName));
    }

    public static string GetAvailablePackageChartPath(string root, string packageName)
    {
        string sanitizedRoot = string.IsNullOrWhiteSpace(root) ? BeatmapsRoot : root;
        string relativePackagePath = SanitizeRelativePackagePath(packageName);
        if (string.IsNullOrWhiteSpace(relativePackagePath))
            relativePackagePath = "New Beatmap";

        return GetAvailablePackageChartPathForPackagePath(Path.Combine(sanitizedRoot, relativePackagePath));
    }

    public static string GetPackagePathFromUserInput(string beatmapPath)
    {
        string relativePackagePath = SanitizeRelativePackagePath(StripBeatmapsRootPrefix(beatmapPath));
        if (string.IsNullOrWhiteSpace(relativePackagePath))
            relativePackagePath = "New Beatmap";

        return Path.Combine(BeatmapsRoot, relativePackagePath);
    }

    public static string SanitizeRelativePackagePath(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        string[] segments = value
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(SanitizeFileName)
            .Where(segment => !string.IsNullOrWhiteSpace(segment))
            .ToArray();

        return segments.Length == 0 ? string.Empty : Path.Combine(segments);
    }

    public static string GetAvailablePackageChartPathForPackagePath(string packagePath)
    {
        if (string.IsNullOrWhiteSpace(packagePath))
            packagePath = Path.Combine(BeatmapsRoot, "New Beatmap");

        string firstPackage = packagePath;
        string firstChart = GetChartPathForPackage(firstPackage);
        if (!File.Exists(firstChart))
            return firstChart;

        string parent = Path.GetDirectoryName(firstPackage);
        if (string.IsNullOrWhiteSpace(parent))
            parent = BeatmapsRoot;

        string packageName = Path.GetFileName(firstPackage);
        if (string.IsNullOrWhiteSpace(packageName))
            packageName = "New Beatmap";

        for (int i = 2; ; i++)
        {
            string candidatePackage = Path.Combine(parent, $"{packageName}_{i}");
            string candidateChart = GetChartPathForPackage(candidatePackage);
            if (!Directory.Exists(candidatePackage) && !File.Exists(candidateChart))
                return candidateChart;
        }
    }

    public static string GetAvailableMigratedChartPath(string legacyChartPath)
    {
        string directory = Path.GetDirectoryName(legacyChartPath);
        string root = string.IsNullOrWhiteSpace(directory) ? BeatmapsRoot : directory;
        string name = Path.GetFileNameWithoutExtension(legacyChartPath);
        return GetAvailablePackageChartPath(root, name);
    }

    public static string NormalizeRelativePath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return string.Empty;

        string normalized = relativePath.Replace('\\', '/').Trim();
        while (normalized.StartsWith("/", StringComparison.Ordinal))
            normalized = normalized[1..];

        return normalized;
    }

    public static IReadOnlyList<string> DiscoverBeatmapCharts()
    {
        if (!Directory.Exists(BeatmapsRoot))
            return Array.Empty<string>();

        return Directory.GetFiles(BeatmapsRoot, "*.xml", SearchOption.AllDirectories)
            .Where(IsDiscoverableBeatmapChart)
            .Select(NormalizeRelativePath)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static bool IsDiscoverableBeatmapChart(string chartPath)
    {
        if (IsPackageChartPath(chartPath))
            return true;

        string directory = Path.GetDirectoryName(chartPath);
        return string.Equals(NormalizePath(directory), NormalizePath(BeatmapsRoot), StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsInsideBeatmapsRoot(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
            return false;

        string root = GetFullPathOrOriginal(BeatmapsRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string folder = GetFullPathOrOriginal(folderPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.Equals(folder, root, StringComparison.OrdinalIgnoreCase)
            || folder.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || folder.StartsWith(root + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsBeatmapsRoot(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
            return false;

        string root = GetFullPathOrOriginal(BeatmapsRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string folder = GetFullPathOrOriginal(folderPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.Equals(folder, root, StringComparison.OrdinalIgnoreCase);
    }

    public static string GetBeatmapsFolderStoragePath(string path)
    {
        string relativePath = GetBeatmapsRelativePath(path);
        return string.IsNullOrWhiteSpace(relativePath)
            ? BeatmapsRoot
            : Path.Combine(BeatmapsRoot, relativePath);
    }

    public static string GetBeatmapsFolderDisplayPath(string path)
    {
        string relativePath = GetBeatmapsRelativePath(path);
        return string.IsNullOrWhiteSpace(relativePath) ? BeatmapsRoot : relativePath;
    }

    public static string GetBeatmapsExplorerDisplayPath(string path)
    {
        string relativePath = GetBeatmapsRelativePath(path).Replace('\\', '/');
        return string.IsNullOrWhiteSpace(relativePath)
            ? BeatmapsRoot
            : BeatmapsRoot + " / " + relativePath.Replace("/", " / ");
    }

    public static string GetBeatmapsRelativePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        string normalizedPath = NormalizePath(GetFullPathOrOriginal(path)).TrimEnd('/');
        string root = NormalizePath(GetFullPathOrOriginal(BeatmapsRoot)).TrimEnd('/');
        if (string.Equals(normalizedPath, root, StringComparison.OrdinalIgnoreCase))
            return string.Empty;

        string prefix = root + "/";
        if (normalizedPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return normalizedPath[prefix.Length..];

        normalizedPath = NormalizePath(path);
        root = NormalizePath(BeatmapsRoot);
        if (string.Equals(normalizedPath, root, StringComparison.OrdinalIgnoreCase))
            return string.Empty;

        prefix = root + "/";
        return normalizedPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? normalizedPath[prefix.Length..]
            : normalizedPath;
    }

    public static string GetChartDisplayName(string chartPath)
    {
        if (IsPackageChartPath(chartPath))
        {
            string packagePath = Path.GetDirectoryName(chartPath);
            string relativePackagePath = GetBeatmapsRelativePath(packagePath);
            return string.IsNullOrWhiteSpace(relativePackagePath)
                ? Path.GetFileName(packagePath)
                : relativePackagePath;
        }

        return Path.GetFileName(chartPath);
    }

    public static string GetChartLeafDisplayName(string chartPath)
    {
        if (IsPackageChartPath(chartPath))
            return Path.GetFileName(Path.GetDirectoryName(chartPath));

        return Path.GetFileNameWithoutExtension(chartPath);
    }

    private static string StripBeatmapsRootPrefix(string path)
    {
        string normalized = NormalizeRelativePath(path);
        string root = NormalizeRelativePath(BeatmapsRoot);
        if (string.Equals(normalized, root, StringComparison.OrdinalIgnoreCase))
            return string.Empty;

        string prefix = root + "/";
        return normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? normalized[prefix.Length..]
            : normalized;
    }

    public static string SanitizeFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        char[] invalidChars = Path.GetInvalidFileNameChars();
        string sanitized = new(value.Select(c => invalidChars.Contains(c) ? '_' : c).ToArray());
        return sanitized.Trim().Trim('.');
    }

    private static bool HasXmlExtension(string path)
    {
        return !string.IsNullOrWhiteSpace(path)
            && string.Equals(Path.GetExtension(path), ".xml", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePath(string path)
    {
        return (path ?? string.Empty).Replace('\\', '/');
    }

    private static string GetFullPathOrOriginal(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException)
        {
            return path ?? string.Empty;
        }
    }
}
