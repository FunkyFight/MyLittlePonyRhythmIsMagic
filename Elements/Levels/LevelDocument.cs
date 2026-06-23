using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace MLP_RiM.Elements.Levels;

public sealed class LevelDocument
{
    public const string LevelsRoot = "Levels";
    public const string LevelFileName = "level.xml";

    public LevelData Level { get; private set; }
    public string FilePath { get; private set; }
    public string PackagePath => Path.GetDirectoryName(FilePath) ?? string.Empty;
    public bool IsDirty { get; private set; }

    private LevelDocument(LevelData level, string filePath, bool isDirty)
    {
        Level = level ?? CreateDefaultData("New Level");
        FilePath = ResolveLevelPath(filePath);
        IsDirty = isDirty;
        NormalizeLevel();
    }

    public static LevelDocument CreateNewPackage(string name)
    {
        string displayName = string.IsNullOrWhiteSpace(name) ? "New Level" : name.Trim();
        return new LevelDocument(CreateDefaultData(displayName), GetAvailablePackageLevelPath(displayName), true);
    }

    public static LevelDocument LoadOrCreate(string path)
    {
        string levelPath = ResolveLevelPath(path);
        if (!File.Exists(levelPath) || new FileInfo(levelPath).Length == 0)
            return new LevelDocument(CreateDefaultData(GetDefaultDisplayName(levelPath)), levelPath, true);

        try
        {
            using TextReader stream = OpenMigratingReader(levelPath, out bool migratedLegacyNodeKind);
            XmlSerializer serializer = new(typeof(LevelData));
            LevelData level = (LevelData)serializer.Deserialize(stream);
            return new LevelDocument(level, levelPath, migratedLegacyNodeKind);
        }
        catch (InvalidOperationException)
        {
            BackupInvalidLevel(levelPath);
            return new LevelDocument(CreateDefaultData(GetDefaultDisplayName(levelPath)), levelPath, true);
        }
    }

    private static TextReader OpenMigratingReader(string levelPath, out bool migratedLegacyNodeKind)
    {
        string xml = File.ReadAllText(levelPath);
        migratedLegacyNodeKind = xml.Contains("Kind=\"FinalBeatmap\"", StringComparison.Ordinal);
        if (migratedLegacyNodeKind)
            xml = xml.Replace("Kind=\"FinalBeatmap\"", "Kind=\"PlayRepresentationBeatmap\"", StringComparison.Ordinal);
        return new StringReader(xml);
    }

    public void Save()
    {
        NormalizeLevel();
        FilePath = ResolveLevelPath(FilePath);

        if (Directory.Exists(FilePath))
            FilePath = Path.Combine(FilePath, LevelFileName);

        string directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        string tempPath = FilePath + ".tmp";

        DeleteFileIfExists(tempPath);
        using (FileStream stream = new(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            XmlSerializer serializer = new(typeof(LevelData));
            serializer.Serialize(stream, Level);
        }

        ReplaceSavedFile(tempPath, FilePath);
        IsDirty = false;
    }

    private static void ReplaceSavedFile(string tempPath, string targetPath)
    {
        if (Directory.Exists(targetPath))
            throw new IOException($"Cannot save level because the target path is a directory: {Path.GetFullPath(targetPath)}");

        DeleteFileIfExists(targetPath);
        File.Move(tempPath, targetPath);
    }

    private static void DeleteFileIfExists(string path)
    {
        if (!File.Exists(path))
            return;

        FileAttributes attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.ReadOnly) != 0)
            File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);

        File.Delete(path);
    }

    public void Reload()
    {
        LevelDocument reloaded = LoadOrCreate(FilePath);
        Level = reloaded.Level;
        FilePath = reloaded.FilePath;
        IsDirty = reloaded.IsDirty;
    }

    public void MarkDirty()
    {
        IsDirty = true;
    }

    public LevelNodeData AddNode(LevelNodeKind kind, int x, int y)
    {
        LevelNodeData node = new()
        {
            Id = NewId(),
            Kind = kind,
            X = x,
            Y = y,
            Text = kind == LevelNodeKind.Dialogue ? "New dialogue." : string.Empty,
            RequiredSuccessCount = 1
        };

        Level.Nodes.Add(node);
        IsDirty = true;
        return node;
    }

    public bool DeleteNode(string nodeId)
    {
        LevelNodeData node = FindNode(nodeId);
        if (node == null || node.Kind == LevelNodeKind.Start)
            return false;

        Level.Nodes.Remove(node);
        Level.Connections.RemoveAll(connection => connection.FromNodeId == nodeId || connection.ToNodeId == nodeId);
        IsDirty = true;
        return true;
    }

    public LevelNodeData FindNode(string nodeId)
    {
        return Level.Nodes.FirstOrDefault(node => string.Equals(node.Id, nodeId, StringComparison.Ordinal));
    }

    public LevelConnectionData GetConnection(string fromNodeId, string fromPort)
    {
        return Level.Connections.FirstOrDefault(connection =>
            string.Equals(connection.FromNodeId, fromNodeId, StringComparison.Ordinal)
            && string.Equals(connection.FromPort, fromPort, StringComparison.Ordinal));
    }

    public string GetConnectionTarget(string fromNodeId, string fromPort)
    {
        return GetConnection(fromNodeId, fromPort)?.ToNodeId;
    }

    public void SetConnection(string fromNodeId, string fromPort, string toNodeId)
    {
        if (string.IsNullOrWhiteSpace(fromNodeId)
            || string.IsNullOrWhiteSpace(fromPort)
            || string.IsNullOrWhiteSpace(toNodeId))
            return;

        Level.Connections.RemoveAll(connection =>
            string.Equals(connection.FromNodeId, fromNodeId, StringComparison.Ordinal)
            && string.Equals(connection.FromPort, fromPort, StringComparison.Ordinal));

        Level.Connections.Add(new LevelConnectionData
        {
            FromNodeId = fromNodeId,
            FromPort = fromPort,
            ToNodeId = toNodeId
        });
        IsDirty = true;
    }

    public static IReadOnlyList<string> GetOutputPorts(LevelNodeKind kind)
    {
        return kind switch
        {
            LevelNodeKind.Start => new[] { "Next" },
            LevelNodeKind.Dialogue => new[] { "Next" },
            LevelNodeKind.SetMiniGame => new[] { "Next" },
            LevelNodeKind.TrainingBeatmap => new[] { "Success" },
            _ => Array.Empty<string>()
        };
    }

    public static string ResolveLevelPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return GetAvailablePackageLevelPath("New Level");

        return string.Equals(Path.GetExtension(path), ".xml", StringComparison.OrdinalIgnoreCase)
            ? path
            : Path.Combine(path, LevelFileName);
    }

    public static IReadOnlyList<string> DiscoverLevelFiles()
    {
        if (!Directory.Exists(LevelsRoot))
            return Array.Empty<string>();

        return Directory.GetFiles(LevelsRoot, LevelFileName, SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static string SanitizeFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        char[] invalidChars = Path.GetInvalidFileNameChars();
        string sanitized = new(value.Select(c => invalidChars.Contains(c) ? '_' : c).ToArray());
        return sanitized.Trim().Trim('.');
    }

    private static LevelData CreateDefaultData(string displayName)
    {
        string levelId = NewId();
        string startId = NewId();
        return new LevelData
        {
            Id = levelId,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? "New Level" : displayName,
            LockedByDefault = false,
            StartNodeId = startId,
            Nodes = new List<LevelNodeData>
            {
                new()
                {
                    Id = startId,
                    Kind = LevelNodeKind.Start,
                    X = 60,
                    Y = 120
                }
            },
            Connections = new List<LevelConnectionData>(),
            UnlockLevelIds = new List<string>()
        };
    }

    private void NormalizeLevel()
    {
        Level.Id = string.IsNullOrWhiteSpace(Level.Id) ? NewId() : Level.Id.Trim();
        Level.DisplayName = string.IsNullOrWhiteSpace(Level.DisplayName) ? GetDefaultDisplayName(FilePath) : Level.DisplayName.Trim();
        Level.UnlockLevelIds ??= new List<string>();
        Level.Nodes ??= new List<LevelNodeData>();
        Level.Connections ??= new List<LevelConnectionData>();

        foreach (LevelNodeData node in Level.Nodes)
        {
            node.Id = string.IsNullOrWhiteSpace(node.Id) ? NewId() : node.Id.Trim();
            node.Text ??= string.Empty;
            node.ChartPath ??= string.Empty;
            node.MiniGameId ??= string.Empty;
            node.RequiredSuccessCount = Math.Max(1, node.RequiredSuccessCount);
        }

        LevelNodeData startNode = Level.Nodes.FirstOrDefault(node => node.Kind == LevelNodeKind.Start);
        if (startNode == null)
        {
            startNode = new LevelNodeData
            {
                Id = NewId(),
                Kind = LevelNodeKind.Start,
                X = 60,
                Y = 120
            };
            Level.Nodes.Insert(0, startNode);
            IsDirty = true;
        }

        if (string.IsNullOrWhiteSpace(Level.StartNodeId) || FindNode(Level.StartNodeId) == null)
        {
            Level.StartNodeId = startNode.Id;
            IsDirty = true;
        }

        Level.Connections.RemoveAll(connection =>
            string.IsNullOrWhiteSpace(connection.FromNodeId)
            || string.IsNullOrWhiteSpace(connection.FromPort)
            || string.IsNullOrWhiteSpace(connection.ToNodeId)
            || FindNode(connection.FromNodeId) == null
            || FindNode(connection.ToNodeId) == null
            || !GetOutputPorts(FindNode(connection.FromNodeId).Kind).Contains(connection.FromPort));
    }

    private static string GetAvailablePackageLevelPath(string levelName)
    {
        string sanitizedName = SanitizeFileName(levelName);
        if (string.IsNullOrWhiteSpace(sanitizedName))
            sanitizedName = "New Level";

        string firstPackage = Path.Combine(LevelsRoot, sanitizedName);
        string firstLevel = Path.Combine(firstPackage, LevelFileName);
        if (!Directory.Exists(firstPackage) && !File.Exists(firstLevel))
            return firstLevel;

        for (int i = 2; ; i++)
        {
            string candidatePackage = Path.Combine(LevelsRoot, $"{sanitizedName}_{i}");
            string candidateLevel = Path.Combine(candidatePackage, LevelFileName);
            if (!Directory.Exists(candidatePackage) && !File.Exists(candidateLevel))
                return candidateLevel;
        }
    }

    private static string GetDefaultDisplayName(string levelPath)
    {
        string directory = Path.GetDirectoryName(levelPath);
        string name = string.IsNullOrWhiteSpace(directory)
            ? Path.GetFileNameWithoutExtension(levelPath)
            : Path.GetFileName(directory);
        return string.IsNullOrWhiteSpace(name) ? "New Level" : name;
    }

    private static void BackupInvalidLevel(string levelPath)
    {
        if (!File.Exists(levelPath) || new FileInfo(levelPath).Length == 0)
            return;

        string backupPath = $"{levelPath}.invalid.{DateTime.Now:yyyyMMddHHmmss}";
        File.Copy(levelPath, backupPath, overwrite: false);
    }

    private static string NewId()
    {
        return Guid.NewGuid().ToString("D");
    }
}
