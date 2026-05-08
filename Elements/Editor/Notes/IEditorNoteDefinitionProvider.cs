using System;
using System.Collections.Generic;
using System.Linq;
using GameCore.Scenes;
using Microsoft.Xna.Framework;
using Rhythm.Note;

namespace MLP_RiM.Elements.Editor;

public interface IEditorNoteProvider
{
    int SortOrder { get; }

    string RhythmGameId { get; }

    string RhythmGameDisplayName { get; }

    EditorNoteDefinition Definition { get; }

    IReadOnlyList<EditorClipDefinition> Clips { get; }

    IEditorNoteOptionsPanel OptionsPanel { get; }

    Scene CreateScene();

    IReadOnlyList<ChartNote> CompileClip(ChartEditorClip clip, ChartTempoMap tempoMap);

    string GetClipTypeIdFromLegacyNote(ChartNote note);

    int FindVariantIndex(ChartNote note);

    IReadOnlyDictionary<string, object> CreateTimingContext(Chart chart, ChartTempoMap tempoMap);

    bool TryValidateNotes(EditorNoteValidationContext context, out string reason);

    bool AllowsBoundaryTouch(EditorNoteDefinition otherDefinition);

    Color GetEditorColor(int variantIndex);
}

public sealed class EditorNoteValidationContext
{
    public EditorNoteValidationContext(Chart chart, IReadOnlyList<ChartNote> notes, IReadOnlyList<ChartNote> changedNotes, ChartTempoMap tempoMap, Func<ChartNote, double> getNoteBeat)
    {
        Chart = chart;
        Notes = notes ?? Array.Empty<ChartNote>();
        ChangedNotes = changedNotes ?? Array.Empty<ChartNote>();
        TempoMap = tempoMap;
        GetNoteBeat = getNoteBeat;
    }

    public Chart Chart { get; }
    public IReadOnlyList<ChartNote> Notes { get; }
    public IReadOnlyList<ChartNote> ChangedNotes { get; }
    public ChartTempoMap TempoMap { get; }
    public Func<ChartNote, double> GetNoteBeat { get; }
}

public abstract class EditorNoteProvider : IEditorNoteProvider
{
    protected const double InclusiveEndEpsilonBeats = 0.000001;

    private IReadOnlyList<EditorClipDefinition> _clips;

    public virtual int SortOrder => 0;

    public virtual string RhythmGameId => null;

    public virtual string RhythmGameDisplayName => Definition?.DisplayName;

    public abstract EditorNoteDefinition Definition { get; }

    public IReadOnlyList<EditorClipDefinition> Clips => _clips ??= CreateDeclaredClips();

    public virtual IEditorNoteOptionsPanel OptionsPanel => null;

    public virtual Scene CreateScene() => null;

    public virtual IReadOnlyList<ChartNote> CompileClip(ChartEditorClip clip, ChartTempoMap tempoMap)
    {
        if (clip == null || tempoMap == null || !IsRuntimeClip(clip, out EditorClipDefinition definition))
            return Array.Empty<ChartNote>();

        Dictionary<string, string> data = CreateClipData(clip, definition);
        return new[] { CreateRuntimeNote(clip, tempoMap, clip.StartBeat, data) };
    }

    public virtual string GetClipTypeIdFromLegacyNote(ChartNote note)
    {
        Dictionary<string, string> data = new(note?.AdditionnalData ?? new Dictionary<string, string>());
        EditorClipDefinition matchingClip = Clips.FirstOrDefault(clip => IsRuntimeClip(clip)
            && clip.DefaultData.Count > 0
            && clip.DefaultData.All(pair => data.TryGetValue(pair.Key, out string value) && value == pair.Value));

        return matchingClip?.ClipTypeId
            ?? Clips.FirstOrDefault(IsRuntimeClip)?.ClipTypeId
            ?? EditorClipDefinitions.NoHit;
    }

    public virtual int FindVariantIndex(ChartNote note)
    {
        return FindVariantIndexByExactData(Definition, note);
    }

    public virtual IReadOnlyDictionary<string, object> CreateTimingContext(Chart chart, ChartTempoMap tempoMap)
    {
        return new Dictionary<string, object>();
    }

    public virtual bool TryValidateNotes(EditorNoteValidationContext context, out string reason)
    {
        reason = null;
        return true;
    }

    public virtual bool AllowsBoundaryTouch(EditorNoteDefinition otherDefinition)
    {
        return false;
    }

    public virtual Color GetEditorColor(int variantIndex)
    {
        return Color.DeepSkyBlue;
    }

    protected virtual IReadOnlyList<EditorClipDefinition> CreateClips() => Array.Empty<EditorClipDefinition>();

    protected EditorClipDefinition Clip(string clipTypeId, string displayName, EditorClipCategory category, double defaultLengthBeats, string inputAction = "ReactMain", IReadOnlyDictionary<string, string> defaultData = null, IReadOnlyList<EditorClipFieldDefinition> fields = null)
    {
        return new EditorClipDefinition(RhythmGameId, clipTypeId, displayName, category, defaultLengthBeats, inputAction, defaultData, fields);
    }

    protected IReadOnlyList<ChartNote> CompileContinuous(ChartEditorClip clip, ChartTempoMap tempoMap, IReadOnlyDictionary<string, string> data, double stepBeats)
    {
        double length = Math.Max(0.0, clip.LengthBeats);
        if (length <= InclusiveEndEpsilonBeats || stepBeats <= 0)
            return new[] { CreateRuntimeNote(clip, tempoMap, clip.StartBeat, data) };

        List<ChartNote> notes = new();
        double endBeat = clip.StartBeat + length;
        for (double beat = clip.StartBeat; beat <= endBeat + InclusiveEndEpsilonBeats; beat += stepBeats)
            notes.Add(CreateRuntimeNote(clip, tempoMap, beat, data));

        return notes;
    }

    protected ChartNote CreateRuntimeNote(ChartEditorClip clip, ChartTempoMap tempoMap, double beat, IReadOnlyDictionary<string, string> data)
    {
        double songPosition = tempoMap.BeatToSeconds(beat);
        return new ChartNote
        {
            SongPosition = songPosition,
            BeatPosition = beat,
            HoldDuration = 0,
            HoldBeats = 0,
            InputActionToPress = string.IsNullOrWhiteSpace(clip.InputAction) ? "ReactMain" : clip.InputAction,
            AdditionnalData = new Dictionary<string, string>(data ?? new Dictionary<string, string>())
        };
    }

    protected Dictionary<string, string> CreateClipData(ChartEditorClip clip, EditorClipDefinition definition)
    {
        Dictionary<string, string> data = new(definition?.DefaultData ?? new Dictionary<string, string>());
        foreach (KeyValuePair<string, string> pair in clip?.Data ?? new Dictionary<string, string>())
            data[pair.Key] = pair.Value;

        return data;
    }

    protected bool IsRuntimeClip(ChartEditorClip clip, out EditorClipDefinition definition)
    {
        definition = FindClipDefinition(clip);
        return definition != null
            && IsRuntimeClip(definition)
            && !string.Equals(clip?.ClipCategory, EditorClipCategory.Instant.ToString(), StringComparison.OrdinalIgnoreCase)
            && !string.Equals(clip?.ClipCategory, EditorClipCategory.NoHit.ToString(), StringComparison.OrdinalIgnoreCase)
            && !string.Equals(clip?.ClipTypeId, EditorClipDefinitions.NoHit, StringComparison.OrdinalIgnoreCase);
    }

    protected static bool IsRuntimeClip(EditorClipDefinition clip)
    {
        return clip != null
            && clip.Category != EditorClipCategory.Instant
            && clip.Category != EditorClipCategory.NoHit;
    }

    protected EditorClipDefinition FindClipDefinition(ChartEditorClip clip)
    {
        return Clips.FirstOrDefault(definition => definition.ClipTypeId == clip?.ClipTypeId)
            ?? EditorClipDefinitions.Find(clip?.RhythmGameId, clip?.ClipTypeId);
    }

    protected static int FindVariantIndexByExactData(EditorNoteDefinition definition, ChartNote note)
    {
        if (definition == null || note?.AdditionnalData == null)
            return 0;

        for (int i = 0; i < definition.Variants.Count; i++)
        {
            EditorNoteVariant variant = definition.Variants[i];
            if (variant.AdditionnalData.All(pair => note.AdditionnalData.TryGetValue(pair.Key, out string value) && value == pair.Value))
                return i;
        }

        return 0;
    }

    private IReadOnlyList<EditorClipDefinition> CreateDeclaredClips()
    {
        if (string.IsNullOrWhiteSpace(RhythmGameId))
            return Array.Empty<EditorClipDefinition>();

        List<EditorClipDefinition> clips = new()
        {
            CreateSwitchGameClip()
        };
        clips.AddRange(CreateClips());
        return clips;
    }

    private EditorClipDefinition CreateSwitchGameClip()
    {
        return Clip($"{RhythmGameId}.switch_game", "Switch Game", EditorClipCategory.Instant, 0, string.Empty, new Dictionary<string, string>
        {
            [EditorClipDefinitions.SwitchGameEventKey] = EditorClipDefinitions.SwitchGameEventValue,
            [EditorClipDefinitions.SwitchGameTargetGameKey] = RhythmGameId
        });
    }
}
