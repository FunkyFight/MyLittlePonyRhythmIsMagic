using System;

namespace MLP_RiM.Elements.Editor;

public sealed class SeeSawEditorNoteTiming : IEditorNoteTiming
{
    public double GetStart(EditorNoteDefinition definition, EditorNoteTimingContext context)
    {
        return context.SongPosition - GetBeforeBeats(definition, context) * context.Crotchet;
    }

    public double GetEnd(EditorNoteDefinition definition, EditorNoteTimingContext context)
    {
        return context.SongPosition + Math.Max(definition.HoldBeats, definition.OccupyAfterBeats) * context.Crotchet;
    }

    public double GetHitWindowStart(EditorNoteDefinition definition, EditorNoteTimingContext context)
    {
        return context.SongPosition - GetBeforeBeats(definition, context) * context.Crotchet;
    }

    public double GetHitWindowEnd(EditorNoteDefinition definition, EditorNoteTimingContext context)
    {
        SeeSawAction action = SeeSawAction.FromVariant(definition.GetVariant(context.VariantIndex));
        if (GetBaseDirection(action.Direction) == SeeSawDirection.Exit)
            return context.SongPosition;

        return context.SongPosition + GetPhaseBeats(context.AfterUsesOuterTiming) * context.Crotchet;
    }

    public double GetSameVariantHitWindowStart(EditorNoteDefinition definition, EditorNoteTimingContext context)
    {
        return GetHitWindowStart(definition, context);
    }

    public double GetSameVariantHitWindowEnd(EditorNoteDefinition definition, EditorNoteTimingContext context)
    {
        return GetHitWindowEnd(definition, context);
    }

    private static double GetBeforeBeats(EditorNoteDefinition definition, EditorNoteTimingContext context)
    {
        SeeSawAction action = SeeSawAction.FromVariant(definition.GetVariant(context.VariantIndex));
        if (GetBaseDirection(action.Direction) == SeeSawDirection.Exit)
            return global::SeeSawTiming.ExitJumpBeats;

        return GetPhaseBeats(context.BeforeUsesOuterTiming) + GetPhaseBeats(context.AfterUsesOuterTiming);
    }

    private static SeeSawDirection GetBaseDirection(SeeSawDirection direction)
    {
        return direction switch
        {
            SeeSawDirection.OuterBigLeap => SeeSawDirection.Outer,
            SeeSawDirection.InnerBigLeap => SeeSawDirection.Inner,
            SeeSawDirection.OppositeBigLeap => SeeSawDirection.Opposite,
            _ => direction
        };
    }

    private static double GetPhaseBeats(bool usesOuterTiming)
    {
        return usesOuterTiming ? global::SeeSawTiming.LongJumpBeats : global::SeeSawTiming.ShortJumpBeats;
    }
}
