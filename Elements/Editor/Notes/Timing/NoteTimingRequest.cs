using System;
using System.Collections.Generic;
using Rhythm.Note;

namespace MLP_RiM.Elements.Editor;

public sealed record NoteTimingRequest
{
    public NoteTimingRequest(ChartNote note, EditorNoteDefinition definition, int noteVariantIndex, double beat, ChartTempoMap tempoMap, IReadOnlyList<ChartNote> previousNotes, IReadOnlyList<ChartNote> nextNotes, IReadOnlyDictionary<string, object> gameContext)
    {
        Note = note;
        Definition = definition;
        NoteVariantIndex = noteVariantIndex;
        Beat = beat;
        TempoMap = tempoMap;
        PreviousNotes = previousNotes ?? Array.Empty<ChartNote>();
        NextNotes = nextNotes ?? Array.Empty<ChartNote>();
        GameContext = gameContext ?? EmptyGameContext;
    }

    private static readonly IReadOnlyDictionary<string, object> EmptyGameContext = new Dictionary<string, object>();

    public ChartNote Note { get; init; }
    public EditorNoteDefinition Definition { get; init; }
    public NoteTypeId NoteType => Definition?.TypeId ?? default;
    public NoteTypeId NoteTypeId => NoteType;
    public int NoteVariantIndex { get; init; }
    public EditorNoteVariant Variant => Definition?.GetVariant(NoteVariantIndex);
    public EditorNoteTimingProfile TimingProfile => Definition?.GetTimingProfile(NoteVariantIndex) ?? EditorNoteTimingProfile.Zero;
    public double Beat { get; init; }
    public ChartTempoMap TempoMap { get; init; }
    public IReadOnlyList<ChartNote> PreviousNotes { get; init; }
    public IReadOnlyList<ChartNote> NextNotes { get; init; }
    public IReadOnlyDictionary<string, object> GameContext { get; init; }

    public ChartNote GetPreviousNote(int offset)
    {
        if (offset <= 0)
            return null;

        int index = PreviousNotes.Count - offset;
        return index >= 0 && index < PreviousNotes.Count ? PreviousNotes[index] : null;
    }

    public ChartNote GetNextNote(int offset)
    {
        if (offset <= 0)
            return null;

        int index = offset - 1;
        return index >= 0 && index < NextNotes.Count ? NextNotes[index] : null;
    }

    public double GetBeat(ChartNote note)
    {
        return note != null ? ChartTiming.GetNoteBeat(note, TempoMap) : Beat;
    }

    public double GetSecondsPerBeat()
    {
        return TempoMap?.GetSecondsPerBeatAtBeat(Beat) ?? 0.0;
    }

    public IReadOnlyList<ChartNote> GetContextualNotes()
    {
        List<ChartNote> notes = new(PreviousNotes.Count + NextNotes.Count + (Note != null ? 1 : 0));
        notes.AddRange(PreviousNotes);
        if (Note != null)
            notes.Add(Note);

        notes.AddRange(NextNotes);
        return notes;
    }

    public bool TryGetGameContext<T>(string key, out T value)
    {
        if (!string.IsNullOrWhiteSpace(key)
            && GameContext.TryGetValue(key, out object rawValue)
            && rawValue is T typedValue)
        {
            value = typedValue;
            return true;
        }

        value = default;
        return false;
    }
}

public sealed record NoteTimingResult(double StartBeat, double EndBeat, double HitStartBeat, double HitEndBeat, double SameVariantHitStartBeat, double SameVariantHitEndBeat)
{
    public static NoteTimingResult AtBeat(double beat)
    {
        return new NoteTimingResult(beat, beat, beat, beat, beat, beat);
    }
}
