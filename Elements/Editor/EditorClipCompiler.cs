using System;
using System.Collections.Generic;
using System.Linq;
using Rhythm.Note;

namespace MLP_RiM.Elements.Editor;

public static class EditorClipCompiler
{
    private const double InclusiveEndEpsilonBeats = 0.000001;
    private const double SeaponySwimStepBeats = 2.0;
    private const double SeaponyRollStepBeats = 1.0;
    private const double TapTapSecondHitOffsetBeats = 0.5;
    private const double TapTapPairStepBeats = 1.5;

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

        if (string.Equals(clip.ClipTypeId, EditorClipDefinitions.NoHit, StringComparison.OrdinalIgnoreCase)
            || string.Equals(clip.ClipCategory, EditorClipCategory.NoHit.ToString(), StringComparison.OrdinalIgnoreCase))
            return Array.Empty<ChartNote>();

        Dictionary<string, string> data = CreateClipData(clip);
        if (IsSeeSawClip(clip))
            return new[] { CreateNote(clip, tempoMap, clip.StartBeat, data) };

        if (IsSeaponyClip(clip, EditorClipDefinitions.SeaponyTapTap))
            return CompileTapTap(clip, tempoMap, data);

        if (IsSeaponyClip(clip, EditorClipDefinitions.SeaponyRoll))
            return CompileRoll(clip, tempoMap, data);

        if (IsSeaponyClip(clip, EditorClipDefinitions.SeaponySwim))
            return CompileContinuous(clip, tempoMap, data, SeaponySwimStepBeats);

        return new[] { CreateNote(clip, tempoMap, clip.StartBeat, data) };
    }

    public static ChartEditorClip CreateClipFromLegacyNote(ChartNote note, Func<ChartNote, double> getNoteBeat, int index)
    {
        Dictionary<string, string> data = new(note?.AdditionnalData ?? new Dictionary<string, string>());
        EditorNoteDefinition definition = EditorNoteDefinitions.FromChartNote(note);
        string rhythmGameId = EditorClipDefinitions.UnknownGameId;
        string clipTypeId = EditorClipDefinitions.NoHit;

        if (definition?.Kind == EditorNoteKind.SeeSaw)
        {
            rhythmGameId = EditorClipDefinitions.SeeSawGameId;
            clipTypeId = GetSeeSawClipType(data);
        }
        else if (definition?.Kind == EditorNoteKind.SeaponyParade)
        {
            rhythmGameId = EditorClipDefinitions.SeaponyParadeGameId;
            clipTypeId = GetSeaponyClipType(data);
        }

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

    private static IReadOnlyList<ChartNote> CompileContinuous(ChartEditorClip clip, ChartTempoMap tempoMap, Dictionary<string, string> data, double stepBeats)
    {
        double length = Math.Max(0.0, clip.LengthBeats);
        if (length <= InclusiveEndEpsilonBeats)
            return new[] { CreateNote(clip, tempoMap, clip.StartBeat, data) };

        List<ChartNote> notes = new();
        double endBeat = clip.StartBeat + length;
        for (double beat = clip.StartBeat; beat <= endBeat + InclusiveEndEpsilonBeats; beat += stepBeats)
            notes.Add(CreateNote(clip, tempoMap, beat, data));

        return notes;
    }

    private static IReadOnlyList<ChartNote> CompileRoll(ChartEditorClip clip, ChartTempoMap tempoMap, Dictionary<string, string> data)
    {
        List<ChartNote> notes = CompileContinuous(clip, tempoMap, data, SeaponyRollStepBeats).ToList();
        if (notes.Count == 0)
            return notes;

        int padding = (4 - notes.Count % 4) % 4;
        double lastBeat = notes[^1].BeatPosition.GetValueOrDefault();
        for (int i = 1; i <= padding; i++)
            notes.Add(CreateNote(clip, tempoMap, lastBeat + i * SeaponyRollStepBeats, data));

        return notes;
    }

    private static IReadOnlyList<ChartNote> CompileTapTap(ChartEditorClip clip, ChartTempoMap tempoMap, Dictionary<string, string> data)
    {
        double length = Math.Max(0.0, clip.LengthBeats);
        if (length <= InclusiveEndEpsilonBeats)
        {
            return new[]
            {
                CreateNote(clip, tempoMap, clip.StartBeat, data),
                CreateNote(clip, tempoMap, clip.StartBeat + TapTapSecondHitOffsetBeats, data)
            };
        }

        List<ChartNote> notes = new();
        double endBeat = clip.StartBeat + length;
        for (double pairStart = clip.StartBeat; pairStart <= endBeat + InclusiveEndEpsilonBeats; pairStart += TapTapPairStepBeats)
        {
            notes.Add(CreateNote(clip, tempoMap, pairStart, data));
            double secondHit = pairStart + TapTapSecondHitOffsetBeats;
            if (secondHit <= endBeat + InclusiveEndEpsilonBeats)
                notes.Add(CreateNote(clip, tempoMap, secondHit, data));
        }

        return notes;
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

        if (IsSeeSawClip(clip) && !data.ContainsKey(SeeSawAction.DataKey))
            ApplySeeSawDefaults(clip.ClipTypeId, data);

        return data;
    }

    private static void ApplySeeSawDefaults(string clipTypeId, Dictionary<string, string> data)
    {
        switch (clipTypeId)
        {
            case EditorClipDefinitions.SeeSawLongShort:
                SeeSawAction.SetPattern(data, SeeSawPatternKind.LongShort);
                break;
            case EditorClipDefinitions.SeeSawShortLong:
                SeeSawAction.SetPattern(data, SeeSawPatternKind.ShortLong);
                break;
            case EditorClipDefinitions.SeeSawShortShort:
                SeeSawAction.SetPattern(data, SeeSawPatternKind.ShortShort);
                break;
            case EditorClipDefinitions.SeeSawExit:
                data[SeeSawAction.DataKey] = SeeSawAction.Exit.Value;
                break;
            default:
                SeeSawAction.SetPattern(data, SeeSawPatternKind.LongLong);
                break;
        }
    }

    private static string GetSeeSawClipType(IReadOnlyDictionary<string, string> data)
    {
        if (data != null && data.TryGetValue(SeeSawAction.DataKey, out string action) && action == SeeSawAction.Exit.Value)
            return EditorClipDefinitions.SeeSawExit;

        if (SeeSawAction.TryGetPattern(data, out SeeSawPatternKind pattern))
        {
            return pattern switch
            {
                SeeSawPatternKind.LongShort => EditorClipDefinitions.SeeSawLongShort,
                SeeSawPatternKind.ShortLong => EditorClipDefinitions.SeeSawShortLong,
                SeeSawPatternKind.ShortShort => EditorClipDefinitions.SeeSawShortShort,
                _ => EditorClipDefinitions.SeeSawLongLong
            };
        }

        return EditorClipDefinitions.SeeSawLongLong;
    }

    private static string GetSeaponyClipType(IReadOnlyDictionary<string, string> data)
    {
        if (data != null && data.TryGetValue("action", out string action))
        {
            return action switch
            {
                "seapony_parade_roll" => EditorClipDefinitions.SeaponyRoll,
                "seapony_parade_tap_tap" => EditorClipDefinitions.SeaponyTapTap,
                _ => EditorClipDefinitions.SeaponySwim
            };
        }

        return EditorClipDefinitions.SeaponySwim;
    }

    private static bool IsSeeSawClip(ChartEditorClip clip)
    {
        return string.Equals(clip.RhythmGameId, EditorClipDefinitions.SeeSawGameId, StringComparison.OrdinalIgnoreCase)
            || (clip.ClipTypeId?.StartsWith("see_saw.", StringComparison.OrdinalIgnoreCase) == true);
    }

    private static bool IsSeaponyClip(ChartEditorClip clip, string clipTypeId)
    {
        return string.Equals(clip.RhythmGameId, EditorClipDefinitions.SeaponyParadeGameId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(clip.ClipTypeId, clipTypeId, StringComparison.OrdinalIgnoreCase);
    }
}
