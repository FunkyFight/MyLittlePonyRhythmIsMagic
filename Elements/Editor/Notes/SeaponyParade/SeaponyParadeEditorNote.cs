using System;
using System.Collections.Generic;
using System.Linq;
using GameCore.Scenes;
using MLP_RiM.Elements.Editor;
using Rhythm.Note;

public class SeaPonyParadeNoteEditor : EditorNoteProvider
{
    public const string GameId = "seapony_parade";
    public const string SwitchGameClipId = "seapony_parade.switch_game";
    public const string SwimClipId = "seapony_parade.swim";
    public const string RollClipId = "seapony_parade.roll";
    public const string TapTapClipId = "seapony_parade.tap_tap";

    private const string SwimAction = "seapony_parade_swim";
    private const string RollAction = "seapony_parade_roll";
    private const string TapTapAction = "seapony_parade_tap_tap";
    private const double SwimStepBeats = 2.0;
    private const double RollStepBeats = 1.0;
    private const double RollCueLeadBeats = 2.0;
    private const double TapTapCueLeadBeats = 2.0;
    private const double TapTapSecondHitOffsetBeats = 0.5;
    private const double TapTapPairStepBeats = 1.5;

    public override int SortOrder => 10;

    public override string RhythmGameId => GameId;

    public override string RhythmGameDisplayName => "Seapony Parade";

    public override EditorNoteDefinition Definition { get; } = new EditorNoteDefinitionBuilder(EditorNoteKind.SeaponyParade, "Seapony Parade")
        .HitWindow(0, 2)
        .InputAction("ReactMain")
        .Occupies(1d, 1d)
        .Matches(note => note.AdditionnalData != null
            && note.AdditionnalData.TryGetValue("action", out string action)
            && action.StartsWith("seapony_parade_"))
        .Variant("Swim", new Dictionary<string, string>(){["action"] = SwimAction})
        .Variant("Roll", new Dictionary<string, string>(){["action"] = RollAction})
        .Variant("Tap Tap", new Dictionary<string, string>(){["action"] = TapTapAction})
        .Timing(new SeaponyParadeEditorNoteTiming())
        .Placement(new SeaponyParadeEditorNotePlacementStrategy())
        .Build();

    public override IEditorNoteOptionsPanel OptionsPanel { get; } = new SeaponyParadeNoteOptionsPanel();

    public override Scene CreateScene()
    {
        return new SeaPonyParade();
    }

    protected override IReadOnlyList<EditorClipDefinition> CreateClips()
    {
        return new[]
        {
            Clip(SwimClipId, "Swim", EditorClipCategory.Continuous, 2, "ReactMain", new Dictionary<string, string> { ["action"] = SwimAction }),
            Clip(RollClipId, "Roll", EditorClipCategory.Continuous, 3, "ReactMain", new Dictionary<string, string> { ["action"] = RollAction }),
            Clip(TapTapClipId, "Tap Tap", EditorClipCategory.SingleHit, 0, "ReactMain", new Dictionary<string, string> { ["action"] = TapTapAction }),
            Clip(EditorClipDefinitions.NoHit, "No Hit", EditorClipCategory.NoHit, 1)
        };
    }

    public override IReadOnlyList<ChartNote> CompileClip(ChartEditorClip clip, ChartTempoMap tempoMap)
    {
        if (clip == null || tempoMap == null || !IsRuntimeClip(clip, out EditorClipDefinition definition))
            return Array.Empty<ChartNote>();

        Dictionary<string, string> data = CreateClipData(clip, definition);
        return clip.ClipTypeId switch
        {
            TapTapClipId => CompileTapTap(clip, tempoMap, data),
            RollClipId => CompileRoll(clip, tempoMap, data),
            SwimClipId => CompileContinuous(clip, tempoMap, data, SwimStepBeats),
            _ => base.CompileClip(clip, tempoMap)
        };
    }

    private IReadOnlyList<ChartNote> CompileRoll(ChartEditorClip clip, ChartTempoMap tempoMap, IReadOnlyDictionary<string, string> data)
    {
        double actionStartBeat = clip.StartBeat + RollCueLeadBeats;
        double length = Math.Max(0.0, clip.LengthBeats);
        List<ChartNote> notes = new();
        if (length <= InclusiveEndEpsilonBeats)
        {
            notes.Add(CreateRuntimeNote(clip, tempoMap, actionStartBeat, data));
        }
        else
        {
            double endBeat = actionStartBeat + length;
            for (double beat = actionStartBeat; beat <= endBeat + InclusiveEndEpsilonBeats; beat += RollStepBeats)
                notes.Add(CreateRuntimeNote(clip, tempoMap, beat, data));
        }

        if (notes.Count == 0)
            return notes;

        int padding = (4 - notes.Count % 4) % 4;
        double lastBeat = notes[^1].BeatPosition.GetValueOrDefault();
        for (int i = 1; i <= padding; i++)
            notes.Add(CreateRuntimeNote(clip, tempoMap, lastBeat + i * RollStepBeats, data));

        return notes;
    }

    private IReadOnlyList<ChartNote> CompileTapTap(ChartEditorClip clip, ChartTempoMap tempoMap, IReadOnlyDictionary<string, string> data)
    {
        double actionStartBeat = clip.StartBeat + TapTapCueLeadBeats;
        double length = Math.Max(0.0, clip.LengthBeats);
        if (length <= InclusiveEndEpsilonBeats)
        {
            return new[]
            {
                CreateRuntimeNote(clip, tempoMap, actionStartBeat, data),
                CreateRuntimeNote(clip, tempoMap, actionStartBeat + TapTapSecondHitOffsetBeats, data)
            };
        }

        List<ChartNote> notes = new();
        double endBeat = actionStartBeat + length;
        for (double pairStart = actionStartBeat; pairStart <= endBeat + InclusiveEndEpsilonBeats; pairStart += TapTapPairStepBeats)
        {
            notes.Add(CreateRuntimeNote(clip, tempoMap, pairStart, data));
            double secondHit = pairStart + TapTapSecondHitOffsetBeats;
            if (secondHit <= endBeat + InclusiveEndEpsilonBeats)
                notes.Add(CreateRuntimeNote(clip, tempoMap, secondHit, data));
        }

        return notes;
    }
}

public sealed class SeaponyParadeEditorNotePlacementStrategy : IEditorNotePlacementStrategy
{
    private const string ActionKey = "action";
    private const string SwimAction = "seapony_parade_swim";
    private const string RollAction = "seapony_parade_roll";
    private const string TapTapAction = "seapony_parade_tap_tap";
    private const double TapTapSecondHitOffsetBeats = 0.5;
    private const double TapTapPairStepBeats = 1.5;

    public IReadOnlyList<EditorNotePlacement> CreatePlacements(EditorNoteDefinition definition, ChartNote sourceNote, EditorNotePlacementContext context)
    {
        if (definition == null || sourceNote == null || context == null)
            return Array.Empty<EditorNotePlacement>();

        string action = GetAction(sourceNote);
        if (action == TapTapAction)
            return CreateTapTapPlacements(definition, sourceNote, context);

        if (action != RollAction)
        {
            ChartNote note = EditorNotePlacementData.CloneForPlacement(sourceNote, sourceNote.SongPosition);
            return new[] { new EditorNotePlacement(definition, note) };
        }

        List<ChartNote> rollNotes = EditorNotePlacementData.CreateNotesFromSource(sourceNote, context.Crotchet, time => CreateRollNote(sourceNote, time)).ToList();
        if (rollNotes.Count == 0)
            return Array.Empty<EditorNotePlacement>();

        RollSeriesInfo series = GetCombinedRollSeries(rollNotes, context.ExistingNotes);
        int padding = (4 - series.Count % 4) % 4;
        double stepSeconds = GetPaddingStepBeats(sourceNote) * context.Crotchet;
        if (stepSeconds > 0)
        {
            for (int i = 1; i <= padding; i++)
                rollNotes.Add(CreateRollNote(sourceNote, series.LastSongPosition + i * stepSeconds));
        }

        return rollNotes
            .OrderBy(note => note.SongPosition)
            .Select(note => new EditorNotePlacement(definition, note))
            .ToArray();
    }

    private static IReadOnlyList<EditorNotePlacement> CreateTapTapPlacements(EditorNoteDefinition definition, ChartNote sourceNote, EditorNotePlacementContext context)
    {
        if (context.Crotchet <= 0)
            return Array.Empty<EditorNotePlacement>();

        if (!EditorNotePlacementData.HasIntervalConfiguration(sourceNote))
        {
            return new[]
            {
                new EditorNotePlacement(definition, CreateTapTapNote(sourceNote, sourceNote.SongPosition)),
                new EditorNotePlacement(definition, CreateTapTapNote(sourceNote, sourceNote.SongPosition + TapTapSecondHitOffsetBeats * context.Crotchet))
            };
        }

        double start = sourceNote.SongPosition;
        double end = start + Math.Max(0, IntervalEditorNoteProvider.GetDurationBeats(sourceNote.AdditionnalData)) * context.Crotchet;
        double secondHitOffset = TapTapSecondHitOffsetBeats * context.Crotchet;
        double pairStep = TapTapPairStepBeats * context.Crotchet;
        const double inclusiveEndEpsilonSeconds = 0.000001;

        List<ChartNote> tapTapNotes = new();
        for (double pairStart = start; pairStart <= end + inclusiveEndEpsilonSeconds; pairStart += pairStep)
        {
            tapTapNotes.Add(CreateTapTapNote(sourceNote, pairStart));

            double secondHit = pairStart + secondHitOffset;
            if (secondHit <= end + inclusiveEndEpsilonSeconds)
                tapTapNotes.Add(CreateTapTapNote(sourceNote, secondHit));
        }

        return tapTapNotes
            .OrderBy(note => note.SongPosition)
            .Select(note => new EditorNotePlacement(definition, note))
            .ToArray();
    }

    private static ChartNote CreateRollNote(ChartNote sourceNote, double songPosition)
    {
        ChartNote note = EditorNotePlacementData.CloneForPlacement(sourceNote, songPosition);
        note.AdditionnalData ??= new Dictionary<string, string>();
        note.AdditionnalData[ActionKey] = RollAction;
        return note;
    }

    private static ChartNote CreateTapTapNote(ChartNote sourceNote, double songPosition)
    {
        ChartNote note = EditorNotePlacementData.CloneForPlacement(sourceNote, songPosition);
        note.AdditionnalData ??= new Dictionary<string, string>();
        note.AdditionnalData[ActionKey] = TapTapAction;
        return note;
    }

    private static RollSeriesInfo GetCombinedRollSeries(IReadOnlyList<ChartNote> generatedRolls, IReadOnlyList<ChartNote> existingNotes)
    {
        List<RollSeriesEntry> entries = new();
        int order = 0;

        if (existingNotes != null)
        {
            foreach (ChartNote note in existingNotes)
            {
                if (note != null)
                    entries.Add(new RollSeriesEntry(note, isGenerated: false, order++));
            }
        }

        foreach (ChartNote note in generatedRolls)
        {
            if (note != null)
                entries.Add(new RollSeriesEntry(note, isGenerated: true, order++));
        }

        entries.Sort(CompareRollSeriesEntries);

        int anchorIndex = entries.FindLastIndex(entry => entry.IsGenerated);
        if (anchorIndex < 0)
            return new RollSeriesInfo(generatedRolls.Count, generatedRolls[generatedRolls.Count - 1].SongPosition);

        int startIndex = anchorIndex;
        while (startIndex > 0 && IsRoll(entries[startIndex - 1].Note))
            startIndex--;

        int endIndex = anchorIndex;
        while (endIndex + 1 < entries.Count && IsRoll(entries[endIndex + 1].Note))
            endIndex++;

        return new RollSeriesInfo(endIndex - startIndex + 1, entries[endIndex].Note.SongPosition);
    }

    private static int CompareRollSeriesEntries(RollSeriesEntry a, RollSeriesEntry b)
    {
        int byTime = a.Note.SongPosition.CompareTo(b.Note.SongPosition);
        if (byTime != 0)
            return byTime;

        int byGenerated = a.IsGenerated.CompareTo(b.IsGenerated);
        return byGenerated != 0 ? byGenerated : a.Order.CompareTo(b.Order);
    }

    private static double GetPaddingStepBeats(ChartNote sourceNote)
    {
        return EditorNotePlacementData.HasIntervalConfiguration(sourceNote)
            ? IntervalEditorNoteProvider.GetStepBeats(sourceNote.AdditionnalData)
            : 1.0;
    }

    private static bool IsRoll(ChartNote note)
    {
        return note?.AdditionnalData != null
            && note.AdditionnalData.TryGetValue(ActionKey, out string action)
            && action == RollAction;
    }

    private static string GetAction(ChartNote note)
    {
        return note?.AdditionnalData != null && note.AdditionnalData.TryGetValue(ActionKey, out string action)
            ? action
            : SwimAction;
    }

    private readonly struct RollSeriesEntry
    {
        public RollSeriesEntry(ChartNote note, bool isGenerated, int order)
        {
            Note = note;
            IsGenerated = isGenerated;
            Order = order;
        }

        public ChartNote Note { get; }
        public bool IsGenerated { get; }
        public int Order { get; }
    }

    private readonly struct RollSeriesInfo
    {
        public RollSeriesInfo(int count, double lastSongPosition)
        {
            Count = count;
            LastSongPosition = lastSongPosition;
        }

        public int Count { get; }
        public double LastSongPosition { get; }
    }
}
