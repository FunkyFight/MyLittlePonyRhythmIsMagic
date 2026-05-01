using System;
using System.Collections.Generic;
using System.Linq;
using Rhythm.Note;

namespace MLP_RiM.Elements.Editor;

public enum EditorNoteKind
{
    RhythmInput,
    SeeSaw
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
    public IReadOnlyList<EditorNoteVariant> Variants { get; }
    private IEditorNoteTiming Timing { get; }
    private Func<ChartNote, bool> MatchesChartNote { get; }

    public EditorNoteDefinition(EditorNoteKind kind, string displayName, string inputAction, double holdBeats, double occupyBeforeBeats, double occupyAfterBeats, double hitWindowBeforeBeats, double hitWindowAfterBeats, IReadOnlyList<EditorNoteVariant> variants)
        : this(kind, displayName, inputAction, holdBeats, occupyBeforeBeats, occupyAfterBeats, hitWindowBeforeBeats, hitWindowAfterBeats, variants, new FixedEditorNoteTiming(), _ => false)
    {
    }

    public EditorNoteDefinition(EditorNoteKind kind, string displayName, string inputAction, double holdBeats, double occupyBeforeBeats, double occupyAfterBeats, double hitWindowBeforeBeats, double hitWindowAfterBeats, IReadOnlyList<EditorNoteVariant> variants, IEditorNoteTiming timing, Func<ChartNote, bool> matchesChartNote)
    {
        Kind = kind;
        DisplayName = displayName;
        InputAction = inputAction;
        HoldBeats = holdBeats;
        OccupyBeforeBeats = occupyBeforeBeats;
        OccupyAfterBeats = occupyAfterBeats;
        HitWindowBeforeBeats = hitWindowBeforeBeats;
        HitWindowAfterBeats = hitWindowAfterBeats;
        Variants = variants;
        Timing = timing;
        MatchesChartNote = matchesChartNote;
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
            HoldDuration = HoldBeats * crotchet,
            InputActionToPress = InputAction,
            AdditionnalData = variant.AdditionnalData.ToDictionary(pair => pair.Key, pair => pair.Value)
        };
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

    public double GetEnd(double noteSongPosition, double crotchet)
    {
        return Timing.GetEnd(this, new EditorNoteTimingContext(noteSongPosition, crotchet));
    }

    public double GetHitWindowStart(double noteSongPosition, double crotchet)
    {
        return Timing.GetHitWindowStart(this, new EditorNoteTimingContext(noteSongPosition, crotchet));
    }

    public double GetHitWindowEnd(double noteSongPosition, double crotchet)
    {
        return Timing.GetHitWindowEnd(this, new EditorNoteTimingContext(noteSongPosition, crotchet));
    }

    public double GetStart(double noteSongPosition, double crotchet, int variantIndex, bool beforeUsesOuterTiming, bool forceBigLeapTiming = false, bool afterUsesOuterTiming = false)
    {
        return Timing.GetStart(this, new EditorNoteTimingContext(noteSongPosition, crotchet, variantIndex, beforeUsesOuterTiming, forceBigLeapTiming: forceBigLeapTiming, afterUsesOuterTiming: afterUsesOuterTiming));
    }

    public double GetHitWindowStart(double noteSongPosition, double crotchet, int variantIndex, bool beforeUsesOuterTiming, bool forceBigLeapTiming = false, bool afterUsesOuterTiming = false)
    {
        return Timing.GetHitWindowStart(this, new EditorNoteTimingContext(noteSongPosition, crotchet, variantIndex, beforeUsesOuterTiming, forceBigLeapTiming: forceBigLeapTiming, afterUsesOuterTiming: afterUsesOuterTiming));
    }

    public double GetHitWindowEnd(double noteSongPosition, double crotchet, int variantIndex, bool rainbowTargetsOuter, bool forceBigLeapTiming = false, bool afterUsesOuterTiming = false)
    {
        return Timing.GetHitWindowEnd(this, new EditorNoteTimingContext(noteSongPosition, crotchet, variantIndex, rainbowTargetsOuter: rainbowTargetsOuter, forceBigLeapTiming: forceBigLeapTiming, afterUsesOuterTiming: afterUsesOuterTiming));
    }
}
