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

public sealed class EditorClipDefinition
{
    public EditorClipDefinition(string rhythmGameId, string clipTypeId, string displayName, EditorClipCategory category, double defaultLengthBeats, string inputAction, IReadOnlyDictionary<string, string> defaultData = null)
    {
        RhythmGameId = rhythmGameId;
        ClipTypeId = clipTypeId;
        DisplayName = displayName;
        Category = category;
        DefaultLengthBeats = defaultLengthBeats;
        InputAction = inputAction;
        DefaultData = defaultData ?? new Dictionary<string, string>();
    }

    public string RhythmGameId { get; }
    public string ClipTypeId { get; }
    public string DisplayName { get; }
    public EditorClipCategory Category { get; }
    public double DefaultLengthBeats { get; }
    public string InputAction { get; }
    public IReadOnlyDictionary<string, string> DefaultData { get; }
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
