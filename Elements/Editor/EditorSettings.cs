using System;
using System.IO;
using System.Text.Json;

namespace MLP_RiM.Elements.Editor;

public sealed class EditorSettings
{
    private const string AppFolderName = "MLP_RiM";
    private const string SettingsFileName = "editor-settings.json";

    public string LastChartPath { get; set; } = "";

    public static EditorSettings Load(string settingsPath = null)
    {
        settingsPath ??= GetDefaultSettingsPath();

        try
        {
            if (!File.Exists(settingsPath))
                return new EditorSettings();

            using FileStream stream = File.OpenRead(settingsPath);
            EditorSettings settings = JsonSerializer.Deserialize<EditorSettings>(stream) ?? new EditorSettings();
            settings.LastChartPath ??= "";
            return settings;
        }
        catch (Exception ex) when (IsRecoverableSettingsException(ex))
        {
            return new EditorSettings();
        }
    }

    public void Save(string settingsPath = null)
    {
        settingsPath ??= GetDefaultSettingsPath();

        try
        {
            string directory = Path.GetDirectoryName(settingsPath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            using FileStream stream = File.Create(settingsPath);
            JsonSerializer.Serialize(stream, this, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex) when (IsRecoverableSettingsException(ex))
        {
        }
    }

    public static string GetDefaultSettingsPath()
    {
        string basePath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrWhiteSpace(basePath))
            basePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(basePath))
            basePath = Path.GetTempPath();

        return Path.Combine(basePath, AppFolderName, SettingsFileName);
    }

    private static bool IsRecoverableSettingsException(Exception ex)
    {
        return ex is IOException
            || ex is UnauthorizedAccessException
            || ex is JsonException
            || ex is NotSupportedException
            || ex is ArgumentException;
    }
}
