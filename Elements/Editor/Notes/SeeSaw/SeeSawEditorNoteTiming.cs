using System;

namespace MLP_RiM.Elements.Editor;

public sealed class SeeSawEditorNoteTiming : IEditorNoteTiming
{
    private const double InnerBeforeBeats = 2.0;
    private const double OuterBeforeBeats = 4.0;

    public double GetStart(EditorNoteDefinition definition, EditorNoteTimingContext context)
    {
        return context.SongPosition - GetBeforeBeats(context) * context.Crotchet;
    }

    public double GetEnd(EditorNoteDefinition definition, EditorNoteTimingContext context)
    {
        return context.SongPosition + Math.Max(definition.HoldBeats, definition.OccupyAfterBeats) * context.Crotchet;
    }

    public double GetHitWindowStart(EditorNoteDefinition definition, EditorNoteTimingContext context)
    {
        return context.SongPosition - GetBeforeBeats(context) * context.Crotchet;
    }

    public double GetHitWindowEnd(EditorNoteDefinition definition, EditorNoteTimingContext context)
    {
        double afterBeats = context.ForceBigLeapTiming
            ? OuterBeforeBeats
            : GetBeforeBeats(context.AfterUsesOuterTiming);

        return context.SongPosition + afterBeats * context.Crotchet;
    }

    private static double GetBeforeBeats(bool beforeUsesOuterTiming)
    {
        return beforeUsesOuterTiming ? OuterBeforeBeats : InnerBeforeBeats;
    }

    private static double GetBeforeBeats(EditorNoteTimingContext context)
    {
        return context.ForceBigLeapTiming ? OuterBeforeBeats : GetBeforeBeats(context.BeforeUsesOuterTiming);
    }
}
