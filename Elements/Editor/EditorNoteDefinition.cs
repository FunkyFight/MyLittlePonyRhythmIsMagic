using System;
using System.Collections.Generic;
using System.Linq;
using Rhythm.Note;

namespace MLP_RiM.Elements.Editor;

public enum EditorNoteKind
{
    RhythmInput,
    SeeSaw,
    SeaponyParade
}

public sealed class EditorNotePlacement
{
    public EditorNotePlacement(EditorNoteDefinition definition, ChartNote note)
    {
        Definition = definition;
        Note = note;
    }

    public EditorNoteDefinition Definition { get; }
    public ChartNote Note { get; }
}

public sealed class EditorNotePlacementContext
{
    public EditorNotePlacementContext(double crotchet, IReadOnlyList<ChartNote> existingNotes)
    {
        Crotchet = crotchet;
        ExistingNotes = existingNotes ?? Array.Empty<ChartNote>();
    }

    public double Crotchet { get; }
    public IReadOnlyList<ChartNote> ExistingNotes { get; }
}

public interface IEditorNotePlacementStrategy
{
    IReadOnlyList<EditorNotePlacement> CreatePlacements(EditorNoteDefinition definition, ChartNote sourceNote, EditorNotePlacementContext context);
}

public sealed class SingleEditorNotePlacementStrategy : IEditorNotePlacementStrategy
{
    public IReadOnlyList<EditorNotePlacement> CreatePlacements(EditorNoteDefinition definition, ChartNote sourceNote, EditorNotePlacementContext context)
    {
        if (definition == null || sourceNote == null || context == null)
            return Array.Empty<EditorNotePlacement>();

        return EditorNotePlacementData.CreateNotesFromSource(sourceNote, context.Crotchet, time => EditorNotePlacementData.CloneForPlacement(sourceNote, time))
            .Select(note => new EditorNotePlacement(definition, note))
            .ToArray();
    }
}

internal static class EditorNotePlacementData
{
    private const double InclusiveEndEpsilonSeconds = 0.000001;

    public static bool HasIntervalConfiguration(ChartNote note)
    {
        return note?.AdditionnalData != null
            && (note.AdditionnalData.ContainsKey(IntervalEditorNoteProvider.DurationBeatsKey)
                || note.AdditionnalData.ContainsKey(IntervalEditorNoteProvider.StepBeatsKey));
    }

    public static IReadOnlyList<ChartNote> CreateNotesFromSource(ChartNote sourceNote, double crotchet, Func<double, ChartNote> createNote)
    {
        if (sourceNote == null || createNote == null)
            return Array.Empty<ChartNote>();

        if (sourceNote.BeatPosition.HasValue)
            return CreateBeatBasedNotesFromSource(sourceNote, crotchet, createNote);

        double start = sourceNote.SongPosition;
        if (!HasIntervalConfiguration(sourceNote))
            return new[] { createNote(start) };

        if (crotchet <= 0)
            return Array.Empty<ChartNote>();

        double durationBeats = Math.Max(0, IntervalEditorNoteProvider.GetDurationBeats(sourceNote.AdditionnalData));
        double stepBeats = IntervalEditorNoteProvider.GetStepBeats(sourceNote.AdditionnalData);
        double end = start + durationBeats * crotchet;
        double stepSeconds = stepBeats * crotchet;

        List<ChartNote> notes = new();
        for (double time = start; time <= end + InclusiveEndEpsilonSeconds; time += stepSeconds)
            notes.Add(createNote(time));

        return notes;
    }

    private static IReadOnlyList<ChartNote> CreateBeatBasedNotesFromSource(ChartNote sourceNote, double crotchet, Func<double, ChartNote> createNote)
    {
        double startBeat = sourceNote.BeatPosition.Value;
        if (!HasIntervalConfiguration(sourceNote))
        {
            ChartNote note = createNote(sourceNote.SongPosition);
            note.BeatPosition = startBeat;
            note.HoldBeats = sourceNote.HoldBeats;
            return new[] { note };
        }

        double durationBeats = Math.Max(0, IntervalEditorNoteProvider.GetDurationBeats(sourceNote.AdditionnalData));
        double stepBeats = IntervalEditorNoteProvider.GetStepBeats(sourceNote.AdditionnalData);
        if (stepBeats <= 0 || double.IsNaN(stepBeats) || double.IsInfinity(stepBeats))
            return Array.Empty<ChartNote>();

        double endBeat = startBeat + durationBeats;
        List<ChartNote> notes = new();
        for (double beat = startBeat; beat <= endBeat + InclusiveEndEpsilonSeconds; beat += stepBeats)
        {
            double approximatedSongPosition = sourceNote.SongPosition + (beat - startBeat) * Math.Max(crotchet, 0.0);
            ChartNote note = createNote(approximatedSongPosition);
            note.BeatPosition = beat;
            note.HoldBeats = sourceNote.HoldBeats;
            notes.Add(note);
        }

        return notes;
    }

    public static ChartNote CloneForPlacement(ChartNote sourceNote, double songPosition)
    {
        return new ChartNote
        {
            SongPosition = songPosition,
            BeatPosition = sourceNote?.BeatPosition,
            HoldDuration = sourceNote?.HoldDuration ?? 0,
            HoldBeats = sourceNote?.HoldBeats,
            InputActionToPress = sourceNote?.InputActionToPress,
            AdditionnalData = CreateStoredAdditionnalData(sourceNote)
        };
    }

    public static Dictionary<string, string> CreateStoredAdditionnalData(ChartNote note)
    {
        Dictionary<string, string> data = new(note?.AdditionnalData ?? new Dictionary<string, string>());
        data.Remove(IntervalEditorNoteProvider.DurationBeatsKey);
        data.Remove(IntervalEditorNoteProvider.StepBeatsKey);
        return data;
    }
}

public sealed class EditorNoteVariant
{
    public string DisplayName { get; }
    public IReadOnlyDictionary<string, string> AdditionnalData { get; }

    public EditorNoteVariant(string displayName, IReadOnlyDictionary<string, string> additionnalData)
    {
        DisplayName = displayName;
        AdditionnalData = additionnalData;
    }
}

public sealed class EditorNoteDefinition
{
    public EditorNoteKind Kind { get; }
    public string DisplayName { get; }
    public string InputAction { get; }
    public double HoldBeats { get; }
    public double OccupyBeforeBeats { get; }
    public double OccupyAfterBeats { get; }
    public double HitWindowBeforeBeats { get; }
    public double HitWindowAfterBeats { get; }
    public double SameVariantHitWindowBeforeBeats { get; }
    public double SameVariantHitWindowAfterBeats { get; }
    public IReadOnlyList<EditorNoteVariant> Variants { get; }
    private IEditorNoteTiming Timing { get; }
    private Func<ChartNote, bool> MatchesChartNote { get; }
    private IEditorNotePlacementStrategy PlacementStrategy { get; }

    public EditorNoteDefinition(EditorNoteKind kind, string displayName, string inputAction, double holdBeats, double occupyBeforeBeats, double occupyAfterBeats, double hitWindowBeforeBeats, double hitWindowAfterBeats, IReadOnlyList<EditorNoteVariant> variants)
        : this(kind, displayName, inputAction, holdBeats, occupyBeforeBeats, occupyAfterBeats, hitWindowBeforeBeats, hitWindowAfterBeats, null, null, variants, new FixedEditorNoteTiming(), _ => false, new SingleEditorNotePlacementStrategy())
    {
    }

    public EditorNoteDefinition(EditorNoteKind kind, string displayName, string inputAction, double holdBeats, double occupyBeforeBeats, double occupyAfterBeats, double hitWindowBeforeBeats, double hitWindowAfterBeats, IReadOnlyList<EditorNoteVariant> variants, IEditorNoteTiming timing, Func<ChartNote, bool> matchesChartNote, IEditorNotePlacementStrategy placementStrategy)
        : this(kind, displayName, inputAction, holdBeats, occupyBeforeBeats, occupyAfterBeats, hitWindowBeforeBeats, hitWindowAfterBeats, null, null, variants, timing, matchesChartNote, placementStrategy)
    {
    }

    public EditorNoteDefinition(EditorNoteKind kind, string displayName, string inputAction, double holdBeats, double occupyBeforeBeats, double occupyAfterBeats, double hitWindowBeforeBeats, double hitWindowAfterBeats, double? sameVariantHitWindowBeforeBeats, double? sameVariantHitWindowAfterBeats, IReadOnlyList<EditorNoteVariant> variants, IEditorNoteTiming timing, Func<ChartNote, bool> matchesChartNote, IEditorNotePlacementStrategy placementStrategy)
    {
        Kind = kind;
        DisplayName = displayName;
        InputAction = inputAction;
        HoldBeats = holdBeats;
        OccupyBeforeBeats = occupyBeforeBeats;
        OccupyAfterBeats = occupyAfterBeats;
        HitWindowBeforeBeats = hitWindowBeforeBeats;
        HitWindowAfterBeats = hitWindowAfterBeats;
        SameVariantHitWindowBeforeBeats = sameVariantHitWindowBeforeBeats ?? hitWindowBeforeBeats;
        SameVariantHitWindowAfterBeats = sameVariantHitWindowAfterBeats ?? hitWindowAfterBeats;
        Variants = variants;
        Timing = timing;
        MatchesChartNote = matchesChartNote;
        PlacementStrategy = placementStrategy;
    }

    public bool Matches(ChartNote note)
    {
        return MatchesChartNote(note);
    }

    public ChartNote CreateChartNote(double songPosition, double crotchet, int variantIndex = 0)
    {
        EditorNoteVariant variant = GetVariant(variantIndex);
        return new ChartNote
        {
            SongPosition = songPosition,
            BeatPosition = null,
            HoldDuration = HoldBeats * crotchet,
            HoldBeats = HoldBeats,
            InputActionToPress = InputAction,
            AdditionnalData = variant.AdditionnalData.ToDictionary(pair => pair.Key, pair => pair.Value)
        };
    }

    public IReadOnlyList<EditorNotePlacement> CreatePlacements(ChartNote sourceNote, EditorNotePlacementContext context)
    {
        return PlacementStrategy.CreatePlacements(this, sourceNote, context);
    }

    public EditorNoteVariant GetVariant(int variantIndex)
    {
        if (Variants.Count == 0)
            return new EditorNoteVariant(DisplayName, new Dictionary<string, string>());

        return Variants[Math.Clamp(variantIndex, 0, Variants.Count - 1)];
    }

    public bool Occupies(double noteSongPosition, double crotchet, double testedSongPosition)
    {
        return testedSongPosition >= GetStart(noteSongPosition, crotchet) && testedSongPosition <= GetEnd(noteSongPosition, crotchet);
    }

    public double GetStart(double noteSongPosition, double crotchet)
    {
        return Timing.GetStart(this, new EditorNoteTimingContext(noteSongPosition, crotchet));
    }

    public double GetStart(double noteSongPosition, double crotchet, Func<int, ChartNote> getRelativeNote)
    {
        return Timing.GetStart(this, new EditorNoteTimingContext(noteSongPosition, crotchet, getRelativeNote: getRelativeNote));
    }

    public double GetEnd(double noteSongPosition, double crotchet)
    {
        return Timing.GetEnd(this, new EditorNoteTimingContext(noteSongPosition, crotchet));
    }

    public double GetEnd(double noteSongPosition, double crotchet, Func<int, ChartNote> getRelativeNote)
    {
        return Timing.GetEnd(this, new EditorNoteTimingContext(noteSongPosition, crotchet, getRelativeNote: getRelativeNote));
    }

    public double GetEnd(double noteSongPosition, double crotchet, int variantIndex, Func<int, ChartNote> getRelativeNote)
    {
        return Timing.GetEnd(this, new EditorNoteTimingContext(noteSongPosition, crotchet, variantIndex, getRelativeNote));
    }

    public double GetHitWindowStart(double noteSongPosition, double crotchet)
    {
        return Timing.GetHitWindowStart(this, new EditorNoteTimingContext(noteSongPosition, crotchet));
    }

    public double GetHitWindowStart(double noteSongPosition, double crotchet, Func<int, ChartNote> getRelativeNote)
    {
        return Timing.GetHitWindowStart(this, new EditorNoteTimingContext(noteSongPosition, crotchet, getRelativeNote: getRelativeNote));
    }

    public double GetHitWindowEnd(double noteSongPosition, double crotchet)
    {
        return Timing.GetHitWindowEnd(this, new EditorNoteTimingContext(noteSongPosition, crotchet));
    }

    public double GetHitWindowEnd(double noteSongPosition, double crotchet, Func<int, ChartNote> getRelativeNote)
    {
        return Timing.GetHitWindowEnd(this, new EditorNoteTimingContext(noteSongPosition, crotchet, getRelativeNote: getRelativeNote));
    }

    public double GetHitWindowEnd(double noteSongPosition, double crotchet, int variantIndex, Func<int, ChartNote> getRelativeNote)
    {
        return Timing.GetHitWindowEnd(this, new EditorNoteTimingContext(noteSongPosition, crotchet, variantIndex, getRelativeNote));
    }

    public double GetSameVariantHitWindowStart(double noteSongPosition, double crotchet)
    {
        return Timing.GetSameVariantHitWindowStart(this, new EditorNoteTimingContext(noteSongPosition, crotchet));
    }

    public double GetSameVariantHitWindowStart(double noteSongPosition, double crotchet, int variantIndex, Func<int, ChartNote> getRelativeNote)
    {
        return Timing.GetSameVariantHitWindowStart(this, new EditorNoteTimingContext(noteSongPosition, crotchet, variantIndex, getRelativeNote));
    }

    public double GetSameVariantHitWindowEnd(double noteSongPosition, double crotchet)
    {
        return Timing.GetSameVariantHitWindowEnd(this, new EditorNoteTimingContext(noteSongPosition, crotchet));
    }

    public double GetSameVariantHitWindowEnd(double noteSongPosition, double crotchet, int variantIndex, Func<int, ChartNote> getRelativeNote)
    {
        return Timing.GetSameVariantHitWindowEnd(this, new EditorNoteTimingContext(noteSongPosition, crotchet, variantIndex, getRelativeNote));
    }

    public double GetStart(double noteSongPosition, double crotchet, int variantIndex, bool beforeUsesOuterTiming, bool forceBigLeapTiming = false, bool afterUsesOuterTiming = false, bool counterBigLeapTiming = false)
    {
        return Timing.GetStart(this, new EditorNoteTimingContext(noteSongPosition, crotchet, variantIndex, beforeUsesOuterTiming: beforeUsesOuterTiming, forceBigLeapTiming: forceBigLeapTiming, afterUsesOuterTiming: afterUsesOuterTiming, counterBigLeapTiming: counterBigLeapTiming));
    }

    public double GetStart(double noteSongPosition, double crotchet, int variantIndex, Func<int, ChartNote> getRelativeNote, bool beforeUsesOuterTiming, bool forceBigLeapTiming = false, bool afterUsesOuterTiming = false, bool counterBigLeapTiming = false)
    {
        return Timing.GetStart(this, new EditorNoteTimingContext(noteSongPosition, crotchet, variantIndex, getRelativeNote, beforeUsesOuterTiming: beforeUsesOuterTiming, forceBigLeapTiming: forceBigLeapTiming, afterUsesOuterTiming: afterUsesOuterTiming, counterBigLeapTiming: counterBigLeapTiming));
    }

    public double GetHitWindowStart(double noteSongPosition, double crotchet, int variantIndex, bool beforeUsesOuterTiming, bool forceBigLeapTiming = false, bool afterUsesOuterTiming = false, bool counterBigLeapTiming = false)
    {
        return Timing.GetHitWindowStart(this, new EditorNoteTimingContext(noteSongPosition, crotchet, variantIndex, beforeUsesOuterTiming: beforeUsesOuterTiming, forceBigLeapTiming: forceBigLeapTiming, afterUsesOuterTiming: afterUsesOuterTiming, counterBigLeapTiming: counterBigLeapTiming));
    }

    public double GetHitWindowStart(double noteSongPosition, double crotchet, int variantIndex, Func<int, ChartNote> getRelativeNote, bool beforeUsesOuterTiming, bool forceBigLeapTiming = false, bool afterUsesOuterTiming = false, bool counterBigLeapTiming = false)
    {
        return Timing.GetHitWindowStart(this, new EditorNoteTimingContext(noteSongPosition, crotchet, variantIndex, getRelativeNote, beforeUsesOuterTiming: beforeUsesOuterTiming, forceBigLeapTiming: forceBigLeapTiming, afterUsesOuterTiming: afterUsesOuterTiming, counterBigLeapTiming: counterBigLeapTiming));
    }

    public double GetSameVariantHitWindowStart(double noteSongPosition, double crotchet, int variantIndex, Func<int, ChartNote> getRelativeNote, bool beforeUsesOuterTiming, bool forceBigLeapTiming = false, bool afterUsesOuterTiming = false, bool counterBigLeapTiming = false)
    {
        return Timing.GetSameVariantHitWindowStart(this, new EditorNoteTimingContext(noteSongPosition, crotchet, variantIndex, getRelativeNote, beforeUsesOuterTiming: beforeUsesOuterTiming, forceBigLeapTiming: forceBigLeapTiming, afterUsesOuterTiming: afterUsesOuterTiming, counterBigLeapTiming: counterBigLeapTiming));
    }

    public double GetHitWindowEnd(double noteSongPosition, double crotchet, int variantIndex, bool rainbowTargetsOuter, bool forceBigLeapTiming = false, bool afterUsesOuterTiming = false)
    {
        return Timing.GetHitWindowEnd(this, new EditorNoteTimingContext(noteSongPosition, crotchet, variantIndex, rainbowTargetsOuter: rainbowTargetsOuter, forceBigLeapTiming: forceBigLeapTiming, afterUsesOuterTiming: afterUsesOuterTiming));
    }

    public double GetHitWindowEnd(double noteSongPosition, double crotchet, int variantIndex, Func<int, ChartNote> getRelativeNote, bool rainbowTargetsOuter, bool forceBigLeapTiming = false, bool afterUsesOuterTiming = false)
    {
        return Timing.GetHitWindowEnd(this, new EditorNoteTimingContext(noteSongPosition, crotchet, variantIndex, getRelativeNote, rainbowTargetsOuter: rainbowTargetsOuter, forceBigLeapTiming: forceBigLeapTiming, afterUsesOuterTiming: afterUsesOuterTiming));
    }

    public double GetSameVariantHitWindowEnd(double noteSongPosition, double crotchet, int variantIndex, Func<int, ChartNote> getRelativeNote, bool rainbowTargetsOuter, bool forceBigLeapTiming = false, bool afterUsesOuterTiming = false)
    {
        return Timing.GetSameVariantHitWindowEnd(this, new EditorNoteTimingContext(noteSongPosition, crotchet, variantIndex, getRelativeNote, rainbowTargetsOuter: rainbowTargetsOuter, forceBigLeapTiming: forceBigLeapTiming, afterUsesOuterTiming: afterUsesOuterTiming));
    }
}
