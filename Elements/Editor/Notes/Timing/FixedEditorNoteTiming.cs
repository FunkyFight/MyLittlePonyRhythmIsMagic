using System;

namespace MLP_RiM.Elements.Editor;

public sealed class FixedEditorNoteTiming : IEditorNoteTiming
{
    public NoteTimingResult GetTiming(NoteTimingRequest request)
    {
        EditorNoteDefinition definition = request?.Definition;
        if (definition == null)
            return NoteTimingResult.AtBeat(request?.Beat ?? 0.0);

        double beat = request.Beat;
        double holdBeats = ChartTiming.GetNoteHoldBeats(request.Note, definition, request.TempoMap);

        return new NoteTimingResult(
            StartBeat: beat - definition.OccupyBeforeBeats,
            EndBeat: beat + Math.Max(holdBeats, definition.OccupyAfterBeats),
            HitStartBeat: beat - definition.HitWindowBeforeBeats,
            HitEndBeat: beat + Math.Max(holdBeats, definition.HitWindowAfterBeats),
            SameVariantHitStartBeat: beat - definition.SameVariantHitWindowBeforeBeats,
            SameVariantHitEndBeat: beat + Math.Max(holdBeats, definition.SameVariantHitWindowAfterBeats));
    }
}
