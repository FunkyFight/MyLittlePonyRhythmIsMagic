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
        EditorNoteTimingProfile timing = request.TimingProfile;

        return new NoteTimingResult(
            StartBeat: beat - timing.OccupyBeforeBeats,
            EndBeat: beat + Math.Max(holdBeats, timing.OccupyAfterBeats),
            HitStartBeat: beat - timing.HitWindowBeforeBeats,
            HitEndBeat: beat + Math.Max(holdBeats, timing.HitWindowAfterBeats),
            SameVariantHitStartBeat: beat - timing.SameVariantHitWindowBeforeBeats,
            SameVariantHitEndBeat: beat + Math.Max(holdBeats, timing.SameVariantHitWindowAfterBeats));
    }
}
