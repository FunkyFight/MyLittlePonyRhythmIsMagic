using MLP_RiM.Elements.Editor;
using Rhythm.Note;

public static class ChartTiming
{
    private const double FallbackSecondsPerBeat = 0.6;

    public static double GetLeadInBeats(Chart chart)
    {
        double leadInBeats = chart?.LeadInBeats ?? 0.0;
        return !double.IsNaN(leadInBeats) && !double.IsInfinity(leadInBeats) && leadInBeats > 0.0
            ? leadInBeats
            : 0.0;
    }

    public static double GetNoteBeat(ChartNote note, ChartTempoMap legacyTempoMap)
    {
        if (note == null)
            return 0.0;

        if (note.BeatPosition.HasValue)
            return note.BeatPosition.Value;

        return legacyTempoMap != null
            ? legacyTempoMap.SecondsToBeat(note.SongPosition)
            : note.SongPosition / FallbackSecondsPerBeat;
    }

    public static void SetNoteBeat(ChartNote note, double beat)
    {
        if (note == null || double.IsNaN(beat) || double.IsInfinity(beat))
            return;

        note.BeatPosition = beat;
    }

    public static double GetNoteHoldBeats(ChartNote note, EditorNoteDefinition definition, ChartTempoMap tempoMap)
    {
        if (note == null)
            return 0.0;

        if (note.HoldBeats.HasValue)
            return System.Math.Max(0.0, note.HoldBeats.Value);

        if (definition != null)
            return System.Math.Max(0.0, definition.HoldBeats);

        if (tempoMap == null)
            return System.Math.Max(0.0, note.HoldDuration / FallbackSecondsPerBeat);

        double startBeat = GetNoteBeat(note, tempoMap);
        double endBeat = tempoMap.SecondsToBeat(note.SongPosition + System.Math.Max(0.0, note.HoldDuration));
        return System.Math.Max(0.0, endBeat - startBeat);
    }

    public static void SetNoteHoldBeats(ChartNote note, double holdBeats)
    {
        if (note == null || double.IsNaN(holdBeats) || double.IsInfinity(holdBeats))
            return;

        note.HoldBeats = System.Math.Max(0.0, holdBeats);
    }

    public static double GetEffectBeat(ChartEffect effect, ChartTempoMap legacyTempoMap)
    {
        if (effect == null)
            return 0.0;

        if (effect.BeatPosition.HasValue)
            return effect.BeatPosition.Value;

        return legacyTempoMap != null
            ? legacyTempoMap.SecondsToBeat(effect.SongPosition)
            : effect.SongPosition / FallbackSecondsPerBeat;
    }

    public static void SetEffectBeat(ChartEffect effect, double beat)
    {
        if (effect == null || double.IsNaN(beat) || double.IsInfinity(beat))
            return;

        effect.BeatPosition = System.Math.Max(0.0, beat);
    }
}
