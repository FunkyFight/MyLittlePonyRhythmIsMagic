using System;

namespace MLP_RiM.Elements.Editor;

public sealed class FixedEditorNoteTiming : IEditorNoteTiming
{
    public double GetStart(EditorNoteDefinition definition, EditorNoteTimingContext context)
    {
        return context.SongPosition - definition.OccupyBeforeBeats * context.Crotchet;
    }

    public double GetEnd(EditorNoteDefinition definition, EditorNoteTimingContext context)
    {
        return context.SongPosition + Math.Max(definition.HoldBeats, definition.OccupyAfterBeats) * context.Crotchet;
    }

    public double GetHitWindowStart(EditorNoteDefinition definition, EditorNoteTimingContext context)
    {
        return context.SongPosition - definition.HitWindowBeforeBeats * context.Crotchet;
    }

    public double GetHitWindowEnd(EditorNoteDefinition definition, EditorNoteTimingContext context)
    {
        return context.SongPosition + Math.Max(definition.HoldBeats, definition.HitWindowAfterBeats) * context.Crotchet;
    }

    public double GetSameVariantHitWindowStart(EditorNoteDefinition definition, EditorNoteTimingContext context)
    {
        return context.SongPosition - definition.SameVariantHitWindowBeforeBeats * context.Crotchet;
    }

    public double GetSameVariantHitWindowEnd(EditorNoteDefinition definition, EditorNoteTimingContext context)
    {
        return context.SongPosition + Math.Max(definition.HoldBeats, definition.SameVariantHitWindowAfterBeats) * context.Crotchet;
    }
}
