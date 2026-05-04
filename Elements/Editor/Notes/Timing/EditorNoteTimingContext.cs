using System;
using Rhythm.Note;

namespace MLP_RiM.Elements.Editor;

public readonly struct EditorNoteTimingContext
{
    public double SongPosition { get; }
    public double Crotchet { get; }
    public int VariantIndex { get; }
    public bool BeforeUsesOuterTiming { get; }
    public bool RainbowTargetsOuter { get; }
    public bool ForceBigLeapTiming { get; }
    public bool AfterUsesOuterTiming { get; }
    public bool CounterBigLeapTiming { get; }
    private Func<int, ChartNote> GetRelativeNote { get; }

    public EditorNoteTimingContext(double songPosition, double crotchet, int variantIndex = 0, Func<int, ChartNote> getRelativeNote = null, bool beforeUsesOuterTiming = false, bool rainbowTargetsOuter = false, bool forceBigLeapTiming = false, bool afterUsesOuterTiming = false, bool counterBigLeapTiming = false)
    {
        SongPosition = songPosition;
        Crotchet = crotchet;
        VariantIndex = variantIndex;
        GetRelativeNote = getRelativeNote;
        BeforeUsesOuterTiming = beforeUsesOuterTiming;
        RainbowTargetsOuter = rainbowTargetsOuter;
        ForceBigLeapTiming = forceBigLeapTiming;
        AfterUsesOuterTiming = afterUsesOuterTiming;
        CounterBigLeapTiming = counterBigLeapTiming;
    }

    public ChartNote GetNoteBefore(int offset)
    {
        return offset > 0 ? GetRelativeNote?.Invoke(-offset) : null;
    }

    public ChartNote GetNoteAfter(int offset)
    {
        return offset > 0 ? GetRelativeNote?.Invoke(offset) : null;
    }
}
