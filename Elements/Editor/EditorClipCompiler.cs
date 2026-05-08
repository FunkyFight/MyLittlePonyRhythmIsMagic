using System;
using System.Collections.Generic;
using System.Linq;
using Rhythm.Note;

namespace MLP_RiM.Elements.Editor;

public static class EditorClipCompiler
{
    public static List<ChartNote> Compile(Chart chart, ChartTempoMap tempoMap)
    {
        if (chart?.EditorClips == null || chart.EditorClips.Count == 0)
            return new List<ChartNote>();

        ChartTempoMap map = tempoMap ?? new ChartTempoMap(chart);
        List<ChartNote> notes = new();
        foreach (ChartEditorClip clip in chart.EditorClips.Where(clip => clip != null).OrderBy(clip => clip.StartBeat).ThenBy(clip => clip.TrackIndex))
            notes.AddRange(CompileClip(clip, map));

        return notes.OrderBy(note => note.BeatPosition.GetValueOrDefault()).ToList();
    }

    public static IReadOnlyList<ChartNote> CompileClip(ChartEditorClip clip, ChartTempoMap tempoMap)
    {
        if (clip == null || tempoMap == null)
            return Array.Empty<ChartNote>();

        if (EditorNoteDefinitions.TryGetProvider(clip.RhythmGameId, out IEditorNoteProvider provider))
            return provider.CompileClip(clip, tempoMap);

        return CompileFallbackClip(clip, tempoMap);
    }

    public static ChartEditorClip CreateClipFromLegacyNote(ChartNote note, Func<ChartNote, double> getNoteBeat, int index)
    {
        Dictionary<string, string> data = new(note?.AdditionnalData ?? new Dictionary<string, string>());
        EditorNoteDefinition definition = EditorNoteDefinitions.FromChartNote(note);
        IEditorNoteProvider provider = null;
        if (definition != null)
            EditorNoteDefinitions.TryGetProvider(definition.Kind, out provider);

        string rhythmGameId = provider?.RhythmGameId ?? EditorClipDefinitions.UnknownGameId;
        string clipTypeId = provider?.GetClipTypeIdFromLegacyNote(note) ?? EditorClipDefinitions.NoHit;
        EditorClipDefinition clipDefinition = EditorClipDefinitions.Find(rhythmGameId, clipTypeId);
        return new ChartEditorClip
        {
            Id = $"legacy-{index}",
            TrackIndex = 0,
            StartBeat = getNoteBeat?.Invoke(note) ?? note?.BeatPosition ?? 0,
            LengthBeats = Math.Max(0.0, note?.HoldBeats ?? clipDefinition?.DefaultLengthBeats ?? 0.0),
            RhythmGameId = rhythmGameId,
            ClipTypeId = clipTypeId,
            ClipCategory = (clipDefinition?.Category ?? EditorClipCategory.SingleHit).ToString(),
            InputAction = note?.InputActionToPress ?? clipDefinition?.InputAction ?? "ReactMain",
            Data = data
        };
    }

    private static IReadOnlyList<ChartNote> CompileFallbackClip(ChartEditorClip clip, ChartTempoMap tempoMap)
    {
        if (EditorClipDefinitions.IsSwitchGame(clip)
            || string.Equals(clip.ClipCategory, EditorClipCategory.Instant.ToString(), StringComparison.OrdinalIgnoreCase)
            || string.Equals(clip.ClipTypeId, EditorClipDefinitions.NoHit, StringComparison.OrdinalIgnoreCase)
            || string.Equals(clip.ClipCategory, EditorClipCategory.NoHit.ToString(), StringComparison.OrdinalIgnoreCase))
            return Array.Empty<ChartNote>();

        return new[] { CreateNote(clip, tempoMap, clip.StartBeat, CreateClipData(clip)) };
    }

    private static ChartNote CreateNote(ChartEditorClip clip, ChartTempoMap tempoMap, double beat, IReadOnlyDictionary<string, string> data)
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

    private static Dictionary<string, string> CreateClipData(ChartEditorClip clip)
    {
        EditorClipDefinition definition = EditorClipDefinitions.Find(clip.RhythmGameId, clip.ClipTypeId);
        Dictionary<string, string> data = new(definition?.DefaultData ?? new Dictionary<string, string>());
        foreach (KeyValuePair<string, string> pair in clip.Data ?? new Dictionary<string, string>())
            data[pair.Key] = pair.Value;

        return data;
    }
}
