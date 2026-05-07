using System;
using System.IO;
using System.Linq;

namespace MLP_RiM.Elements.Editor;

public static class BeatmapPackagePaths
{
    public const string BeatmapsRoot = "Beatmaps";
    public const string ChartFileName = "chart.xml";
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

    public static string GetAvailablePackageChartPath(string beatmapName)
    {
        string packageName = SanitizeFileName(beatmapName);
        if (string.IsNullOrWhiteSpace(packageName))
            packageName = "New Beatmap";

        return GetAvailablePackageChartPath(BeatmapsRoot, packageName);
    }

    public static string GetAvailablePackageChartPath(string root, string packageName)
    {
        string sanitizedRoot = string.IsNullOrWhiteSpace(root) ? BeatmapsRoot : root;
        string sanitizedName = SanitizeFileName(packageName);
        if (string.IsNullOrWhiteSpace(sanitizedName))
            sanitizedName = "New Beatmap";

        string firstPackage = Path.Combine(sanitizedRoot, sanitizedName);
        string firstChart = GetChartPathForPackage(firstPackage);
        if (!Directory.Exists(firstPackage) && !File.Exists(firstChart))
            return firstChart;

        for (int i = 2; ; i++)
        {
            string candidatePackage = Path.Combine(sanitizedRoot, $"{sanitizedName}_{i}");
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
}
