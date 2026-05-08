using System;
using System.Collections.Generic;

namespace MLP_RiM.Elements.Editor;

public enum EditorClipCategory
{
    SingleHit,
    Continuous,
    Instant,
    NoHit,
    TempoChange
}

public enum EditorClipFieldKind
{
    Bool,
    Float,
    Enum
}

public sealed record EditorClipFieldOption(string Value, string DisplayName);

public sealed class EditorClipFieldDefinition
{
    private EditorClipFieldDefinition(string key, string displayName, EditorClipFieldKind kind, string defaultValue, IReadOnlyList<EditorClipFieldOption> options = null, double minValue = 0, double maxValue = 1)
    {
        Key = key;
        DisplayName = displayName;
        Kind = kind;
        DefaultValue = defaultValue ?? string.Empty;
        Options = options ?? Array.Empty<EditorClipFieldOption>();
        MinValue = minValue;
        MaxValue = maxValue;
    }

    public string Key { get; }
    public string DisplayName { get; }
    public EditorClipFieldKind Kind { get; }
    public string DefaultValue { get; }
    public IReadOnlyList<EditorClipFieldOption> Options { get; }
    public double MinValue { get; }
    public double MaxValue { get; }

    public static EditorClipFieldDefinition Bool(string key, string displayName, bool defaultValue = false)
    {
        return new EditorClipFieldDefinition(key, displayName, EditorClipFieldKind.Bool, defaultValue ? "true" : "false");
    }

    public static EditorClipFieldDefinition Float(string key, string displayName, double defaultValue = 0, double minValue = 0, double maxValue = 1)
    {
        return new EditorClipFieldDefinition(key, displayName, EditorClipFieldKind.Float, defaultValue.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture), minValue: minValue, maxValue: maxValue);
    }

    public static EditorClipFieldDefinition Enum(string key, string displayName, string defaultValue, IReadOnlyList<EditorClipFieldOption> options)
    {
        return new EditorClipFieldDefinition(key, displayName, EditorClipFieldKind.Enum, defaultValue, options);
    }
}

public sealed class EditorClipDefinition
{
    public EditorClipDefinition(string rhythmGameId, string clipTypeId, string displayName, EditorClipCategory category, double defaultLengthBeats, string inputAction, IReadOnlyDictionary<string, string> defaultData = null, IReadOnlyList<EditorClipFieldDefinition> fields = null)
    {
        RhythmGameId = rhythmGameId;
        ClipTypeId = clipTypeId;
        DisplayName = displayName;
        Category = category;
        DefaultLengthBeats = defaultLengthBeats;
        InputAction = inputAction;
        DefaultData = defaultData ?? new Dictionary<string, string>();
        Fields = fields ?? Array.Empty<EditorClipFieldDefinition>();
    }

    public string RhythmGameId { get; }
    public string ClipTypeId { get; }
    public string DisplayName { get; }
    public EditorClipCategory Category { get; }
    public double DefaultLengthBeats { get; }
    public string InputAction { get; }
    public IReadOnlyDictionary<string, string> DefaultData { get; }
    public IReadOnlyList<EditorClipFieldDefinition> Fields { get; }
}

public sealed class EditorRhythmGameDefinition
{
    public EditorRhythmGameDefinition(string id, string displayName, IReadOnlyList<EditorClipDefinition> clips)
    {
        Id = id;
        DisplayName = displayName;
        Clips = clips ?? Array.Empty<EditorClipDefinition>();
    }

    public string Id { get; }
    public string DisplayName { get; }
    public IReadOnlyList<EditorClipDefinition> Clips { get; }
}
