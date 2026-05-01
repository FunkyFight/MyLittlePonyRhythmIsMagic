namespace MLP_RiM.Elements.Editor;

public readonly struct EditorNoteTimingContext
{
    public double SongPosition { get; }
    public double Crotchet { get; }
    public int VariantIndex { get; }
    public bool BeforeUsesOuterTiming { get; }
    public bool RainbowTargetsOuter { get; }

    public EditorNoteTimingContext(double songPosition, double crotchet, int variantIndex = 0, bool beforeUsesOuterTiming = false, bool rainbowTargetsOuter = false)
    {
        SongPosition = songPosition;
        Crotchet = crotchet;
        VariantIndex = variantIndex;
        BeforeUsesOuterTiming = beforeUsesOuterTiming;
        RainbowTargetsOuter = rainbowTargetsOuter;
    }
}
