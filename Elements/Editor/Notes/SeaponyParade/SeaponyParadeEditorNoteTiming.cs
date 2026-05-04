using MLP_RiM.Elements.Editor;
using Rhythm.Note;

public class SeaponyParadeEditorNoteTiming : IEditorNoteTiming
{
    private const string SwimAction = "seapony_parade_swim";
    private const string RollAction = "seapony_parade_roll";

    public double GetEnd(EditorNoteDefinition definition, EditorNoteTimingContext context)
    {
        switch(GetSelectedAction(definition, context))
        {
            case SwimAction:
                return context.SongPosition + context.Crotchet;

            case RollAction:
                return IsRollAfter(context)
                    ? context.GetNoteAfter(1).SongPosition
                    : context.SongPosition + GetRollStopCueBeats(context) * context.Crotchet;
        }

        return context.SongPosition;
    }

    public double GetHitWindowEnd(EditorNoteDefinition definition, EditorNoteTimingContext context)
    {
        switch(GetSelectedAction(definition, context))
        {
            case SwimAction:
                return context.SongPosition + 2 * context.Crotchet;

            case RollAction:
                return IsRollAfter(context)
                    ? context.GetNoteAfter(1).SongPosition
                    : GetEnd(definition, context) + context.Crotchet;
        }

        return context.SongPosition;
    }

    public double GetHitWindowStart(EditorNoteDefinition definition, EditorNoteTimingContext context)
    {
        switch(GetSelectedAction(definition, context))
        {
            case SwimAction:
            case RollAction:
                return context.SongPosition;
        }

        return context.SongPosition;
    }

    public double GetStart(EditorNoteDefinition definition, EditorNoteTimingContext context)
    {
        switch(GetSelectedAction(definition, context))
        {
            case SwimAction:
                return context.SongPosition - context.Crotchet;

            case RollAction:
                return IsRollBefore(context)
                    ? context.SongPosition
                    : context.SongPosition - 2 * context.Crotchet;
        }

        return context.SongPosition;
    }

    public double GetSameVariantHitWindowStart(EditorNoteDefinition definition, EditorNoteTimingContext context)
    {
        if(GetSelectedAction(definition, context) == RollAction)
            return context.SongPosition;

        return GetHitWindowStart(definition, context);
    }

    public double GetSameVariantHitWindowEnd(EditorNoteDefinition definition, EditorNoteTimingContext context)
    {
        if(GetSelectedAction(definition, context) == RollAction)
            return context.SongPosition + context.Crotchet;

        return GetHitWindowEnd(definition, context);
    }

    private static string GetSelectedAction(EditorNoteDefinition definition, EditorNoteTimingContext context)
    {
        return definition.GetVariant(context.VariantIndex).AdditionnalData.TryGetValue("action", out string action)
            ? action
            : SwimAction;
    }

    private static bool IsRollBefore(EditorNoteTimingContext context)
    {
        return IsRoll(context.GetNoteBefore(1));
    }

    private static bool IsRollAfter(EditorNoteTimingContext context)
    {
        return IsRoll(context.GetNoteAfter(1));
    }

    private static bool IsRoll(ChartNote note)
    {
        return note?.AdditionnalData != null
            && note.AdditionnalData.TryGetValue("action", out string action)
            && action == RollAction;
    }

    private static int GetRollStopCueBeats(EditorNoteTimingContext context)
    {
        int rollCount = 1;
        for (int offset = 1; IsRoll(context.GetNoteBefore(offset)); offset++)
            rollCount++;

        int paddingBeats = (4 - rollCount % 4) % 4;
        if(paddingBeats == 0)
            paddingBeats = 1;

        return paddingBeats;
    }
}
