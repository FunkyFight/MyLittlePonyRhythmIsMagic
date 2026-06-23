using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using MLP_RiM.Elements.Editor;

namespace MLP_RiM.Elements.Levels;

public sealed class LevelProgressSave
{
    private const string SaveFileName = "save.json";

    public List<string> UnlockedLevelIds { get; set; } = new();

    public bool IsUnlocked(LevelData level)
    {
        if (level == null)
            return false;

        return !level.LockedByDefault || UnlockedLevelIds.Any(id => string.Equals(id, level.Id, StringComparison.OrdinalIgnoreCase));
    }

    public bool Unlock(string levelId)
    {
        if (string.IsNullOrWhiteSpace(levelId)
            || UnlockedLevelIds.Any(id => string.Equals(id, levelId, StringComparison.OrdinalIgnoreCase)))
            return false;

        UnlockedLevelIds.Add(levelId.Trim());
        return true;
    }

    public static LevelProgressSave Load(string savePath = null)
    {
        savePath ??= GetDefaultSavePath();
        try
        {
            if (!File.Exists(savePath))
                return new LevelProgressSave();

            using FileStream stream = File.OpenRead(savePath);
            LevelProgressSave save = JsonSerializer.Deserialize<LevelProgressSave>(stream) ?? new LevelProgressSave();
            save.UnlockedLevelIds ??= new List<string>();
            return save;
        }
        catch (Exception ex) when (IsRecoverableSaveException(ex))
        {
            return new LevelProgressSave();
        }
    }

    public void Save(string savePath = null)
    {
        savePath ??= GetDefaultSavePath();
        try
        {
            string directory = Path.GetDirectoryName(savePath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            using FileStream stream = File.Create(savePath);
            JsonSerializer.Serialize(stream, this, new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex) when (IsRecoverableSaveException(ex))
        {
        }
    }

    public static string GetDefaultSavePath()
    {
        string settingsPath = EditorSettings.GetDefaultSettingsPath();
        string directory = Path.GetDirectoryName(settingsPath);
        return Path.Combine(string.IsNullOrWhiteSpace(directory) ? Path.GetTempPath() : directory, SaveFileName);
    }

    private static bool IsRecoverableSaveException(Exception ex)
    {
        return ex is IOException
            || ex is UnauthorizedAccessException
            || ex is JsonException
            || ex is NotSupportedException
            || ex is ArgumentException;
    }
}
