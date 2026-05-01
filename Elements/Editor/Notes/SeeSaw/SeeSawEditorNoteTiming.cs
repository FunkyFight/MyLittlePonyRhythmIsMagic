using System;

namespace MLP_RiM.Elements.Editor;

public sealed class SeeSawEditorNoteTiming : IEditorNoteTiming
{
    public double GetStart(EditorNoteDefinition definition, EditorNoteTimingContext context)
    {
        return context.SongPosition - GetBeforeBeats(context.BeforeUsesOuterTiming) * context.Crotchet;
    }

    public double GetEnd(EditorNoteDefinition definition, EditorNoteTimingContext context)
    {
        return context.SongPosition + Math.Max(definition.HoldBeats, definition.OccupyAfterBeats) * context.Crotchet;
    }

    public double GetHitWindowStart(EditorNoteDefinition definition, EditorNoteTimingContext context)
    {
        return context.SongPosition - GetBeforeBeats(context.BeforeUsesOuterTiming) * context.Crotchet;
    }

    public double GetHitWindowEnd(EditorNoteDefinition definition, EditorNoteTimingContext context)
    {
        EditorNoteVariant variant = definition.GetVariant(context.VariantIndex);
        double afterBeats = SeeSawAction.FromVariant(variant).TargetsOuterAfterHit(context.RainbowTargetsOuter)
            ? 3
            : 2;

        return context.SongPosition + afterBeats * context.Crotchet;
    }

    private static double GetBeforeBeats(bool beforeUsesOuterTiming)
    {
        return beforeUsesOuterTiming ? 3 : 2;
    }
}
