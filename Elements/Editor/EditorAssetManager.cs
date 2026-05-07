using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MLP_RiM.Elements.Editor;

public sealed class EditorAssetManager
{
    private static readonly HashSet<string> SupportedImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png",
        ".jpg",
        ".jpeg"
    };

    public EditorAssetManager(string packagePath)
    {
        if (string.IsNullOrWhiteSpace(packagePath))
            throw new ArgumentException("Package path is required", nameof(packagePath));

        PackagePath = packagePath;
        AssetsPath = BeatmapPackagePaths.GetAssetsPath(packagePath);
    }

    public string PackagePath { get; }
    public string AssetsPath { get; }

    public string ImportImage(string sourcePath, string targetSubfolder = null)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
            throw new ArgumentException("Source path is required", nameof(sourcePath));

        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("Asset source file was not found", sourcePath);

        string extension = Path.GetExtension(sourcePath);
        if (!SupportedImageExtensions.Contains(extension))
            throw new InvalidOperationException($"Unsupported image extension '{extension}'");

        string targetDirectory = GetTargetDirectory(targetSubfolder);
        Directory.CreateDirectory(targetDirectory);

        string baseName = BeatmapPackagePaths.SanitizeFileName(Path.GetFileNameWithoutExtension(sourcePath));
        if (string.IsNullOrWhiteSpace(baseName))
            baseName = "asset";

        string targetPath = GetAvailableTargetPath(targetDirectory, baseName, extension.ToLowerInvariant());
        File.Copy(sourcePath, targetPath, overwrite: false);
        return BeatmapPackagePaths.NormalizeRelativePath(Path.GetRelativePath(PackagePath, targetPath));
    }

    private string GetTargetDirectory(string targetSubfolder)
    {
        if (string.IsNullOrWhiteSpace(targetSubfolder))
            return AssetsPath;

        string normalized = BeatmapPackagePaths.NormalizeRelativePath(targetSubfolder);
        string[] parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(BeatmapPackagePaths.SanitizeFileName)
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToArray();

        return parts.Length == 0 ? AssetsPath : Path.Combine(new[] { AssetsPath }.Concat(parts).ToArray());
    }

    private static string GetAvailableTargetPath(string targetDirectory, string baseName, string extension)
    {
        string first = Path.Combine(targetDirectory, baseName + extension);
        if (!File.Exists(first))
            return first;

        for (int i = 2; ; i++)
        {
            string candidate = Path.Combine(targetDirectory, $"{baseName}_{i}{extension}");
            if (!File.Exists(candidate))
                return candidate;
        }
    }
}
