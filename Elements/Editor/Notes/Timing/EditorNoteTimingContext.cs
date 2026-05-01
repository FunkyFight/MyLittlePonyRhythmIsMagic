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

    public EditorNoteTimingContext(double songPosition, double crotchet, int variantIndex = 0, bool beforeUsesOuterTiming = false, bool rainbowTargetsOuter = false, bool forceBigLeapTiming = false, bool afterUsesOuterTiming = false)
    {
        SongPosition = songPosition;
        Crotchet = crotchet;
        VariantIndex = variantIndex;
        BeforeUsesOuterTiming = beforeUsesOuterTiming;
        RainbowTargetsOuter = rainbowTargetsOuter;
        ForceBigLeapTiming = forceBigLeapTiming;
        AfterUsesOuterTiming = afterUsesOuterTiming;
    }
}
