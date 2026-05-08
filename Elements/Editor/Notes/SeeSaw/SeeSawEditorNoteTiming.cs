using System.Collections.Generic;
using Rhythm.Note;

namespace MLP_RiM.Elements.Editor;

public sealed class SeeSawEditorNoteTiming : IEditorNoteTiming
{
    public const string LeadInBeatsContextKey = "see_saw.lead_in_beats";

    private static readonly FixedEditorNoteTiming FallbackTiming = new();

    public NoteTimingResult GetTiming(NoteTimingRequest request)
    {
        if (request == null)
            return NoteTimingResult.AtBeat(0.0);

        SeeSawCompiledEventTiming timing = GetCompiledTiming(request);
        if (!timing.IsSeeSaw && !timing.IsExit)
            return FallbackTiming.GetTiming(request);

        return new NoteTimingResult(
            StartBeat: timing.PrepStartBeat,
            EndBeat: timing.EndBeat,
            HitStartBeat: timing.PrepStartBeat,
            HitEndBeat: timing.EndBeat,
            SameVariantHitStartBeat: timing.PrepStartBeat,
            SameVariantHitEndBeat: timing.EndBeat);
    }

    private static SeeSawCompiledEventTiming GetCompiledTiming(NoteTimingRequest request)
    {
        IReadOnlyList<ChartNote> contextualNotes = request.GetContextualNotes();
        double leadInBeats = GetLeadInBeats(request);

        if (request.Note != null)
            return SeeSawChartCompiler.GetTimingForChartNote(contextualNotes, request.Note, note => ChartTiming.GetNoteBeat(note, request.TempoMap), leadInBeats);

        return SeeSawChartCompiler.GetPreviewTiming(contextualNotes, request.Definition.GetVariant(request.VariantIndex).AdditionnalData, request.Beat, note => ChartTiming.GetNoteBeat(note, request.TempoMap), leadInBeats);
    }

    private static double GetLeadInBeats(NoteTimingRequest request)
    {
        return request.TryGetGameContext(LeadInBeatsContextKey, out double leadInBeats) ? leadInBeats : 0.0;
    }
}
