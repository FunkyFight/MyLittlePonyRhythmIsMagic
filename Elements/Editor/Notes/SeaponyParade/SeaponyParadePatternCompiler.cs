using System;
using System.Collections.Generic;
using System.Linq;
using Rhythm.Note;

namespace MLP_RiM.Elements.Editor;

public sealed class SeaponyParadePatternCompiler : INotePatternCompiler
{
    public const double SwimStepBeats = 2.0;
    public const double RollStepBeats = 1.0;
    public const double RollCueLeadBeats = 2.0;
    public const double TapTapCueLeadBeats = 2.0;
    public const double TapTapSecondHitOffsetBeats = 0.5;
    public const double TapTapPairStepBeats = 1.5;

    private const double InclusiveEndEpsilonBeats = 0.000001;

    public IReadOnlyList<RuntimeNoteDraft> Compile(NoteAuthoringIntent intent, NoteCompileContext context)
    {
        if (intent?.Payload is not SeaponyNotePayload payload)
            return Array.Empty<RuntimeNoteDraft>();

        return payload.Action switch
        {
            SeaponyAction.Roll => CompileRoll(intent, context, payload),
            SeaponyAction.TapTap => CompileTapTap(intent, payload),
            _ => CompileSwim(intent, payload)
        };
    }

    private static IReadOnlyList<RuntimeNoteDraft> CompileSwim(NoteAuthoringIntent intent, SeaponyNotePayload payload)
    {
        double length = Math.Max(0.0, intent.LengthBeats);
        if (length <= InclusiveEndEpsilonBeats)
            return new[] { CreateDraft(intent.StartBeat, payload) };

        List<RuntimeNoteDraft> notes = new();
        double endBeat = intent.StartBeat + length;
        for (double beat = intent.StartBeat; beat <= endBeat + InclusiveEndEpsilonBeats; beat += SwimStepBeats)
            notes.Add(CreateDraft(beat, payload));

        return notes;
    }

    private static IReadOnlyList<RuntimeNoteDraft> CompileRoll(NoteAuthoringIntent intent, NoteCompileContext context, SeaponyNotePayload payload)
    {
        double length = Math.Max(0.0, intent.PlacementOptions?.RepeatDurationBeats ?? intent.LengthBeats);
        double stepBeats = Math.Max(0.000001, intent.PlacementOptions?.RepeatStepBeats ?? RollStepBeats);
        List<RuntimeNoteDraft> notes = new();

        if (length <= InclusiveEndEpsilonBeats)
        {
            notes.Add(CreateDraft(intent.StartBeat, payload));
        }
        else
        {
            double endBeat = intent.StartBeat + length;
            for (double beat = intent.StartBeat; beat <= endBeat + InclusiveEndEpsilonBeats; beat += stepBeats)
                notes.Add(CreateDraft(beat, payload));
        }

        if (notes.Count == 0)
            return notes;

        RollSeriesInfo series = GetCombinedRollSeries(notes, context?.ExistingNotes, context?.TempoMap);
        int padding = (4 - series.Count % 4) % 4;
        for (int i = 1; i <= padding; i++)
            notes.Add(CreateDraft(series.LastBeat + i * stepBeats, payload));

        return notes;
    }

    private static IReadOnlyList<RuntimeNoteDraft> CompileTapTap(NoteAuthoringIntent intent, SeaponyNotePayload payload)
    {
        double length = Math.Max(0.0, intent.PlacementOptions?.RepeatDurationBeats ?? intent.LengthBeats);
        if (length <= InclusiveEndEpsilonBeats)
        {
            return new[]
            {
                CreateDraft(intent.StartBeat, payload),
                CreateDraft(intent.StartBeat + TapTapSecondHitOffsetBeats, payload)
            };
        }

        List<RuntimeNoteDraft> notes = new();
        double endBeat = intent.StartBeat + length;
        for (double pairStart = intent.StartBeat; pairStart <= endBeat + InclusiveEndEpsilonBeats; pairStart += TapTapPairStepBeats)
        {
            notes.Add(CreateDraft(pairStart, payload));
            double secondHit = pairStart + TapTapSecondHitOffsetBeats;
            if (secondHit <= endBeat + InclusiveEndEpsilonBeats)
                notes.Add(CreateDraft(secondHit, payload));
        }

        return notes;
    }

    private static RuntimeNoteDraft CreateDraft(double beat, SeaponyNotePayload payload)
    {
        return new RuntimeNoteDraft(beat, payload);
    }

    private static RollSeriesInfo GetCombinedRollSeries(IReadOnlyList<RuntimeNoteDraft> generatedRolls, IReadOnlyList<ChartNote> existingNotes, ChartTempoMap tempoMap)
    {
        List<RollSeriesEntry> entries = new();
        int order = 0;

        foreach (ChartNote note in existingNotes ?? Array.Empty<ChartNote>())
        {
            if (note != null)
                entries.Add(new RollSeriesEntry(GetNoteBeat(note, tempoMap), isGenerated: false, isRoll: SeaponyNoteCodec.IsAction(note.AdditionnalData, SeaponyAction.Roll), order++));
        }

        foreach (RuntimeNoteDraft note in generatedRolls)
            entries.Add(new RollSeriesEntry(note.Beat, isGenerated: true, isRoll: true, order++));

        entries.Sort(CompareRollSeriesEntries);

        int anchorIndex = entries.FindLastIndex(entry => entry.IsGenerated);
        if (anchorIndex < 0)
            return new RollSeriesInfo(generatedRolls.Count, generatedRolls[generatedRolls.Count - 1].Beat);

        int startIndex = anchorIndex;
        while (startIndex > 0 && entries[startIndex - 1].IsRoll)
            startIndex--;

        int endIndex = anchorIndex;
        while (endIndex + 1 < entries.Count && entries[endIndex + 1].IsRoll)
            endIndex++;

        return new RollSeriesInfo(endIndex - startIndex + 1, entries[endIndex].Beat);
    }

    private static double GetNoteBeat(ChartNote note, ChartTempoMap tempoMap)
    {
        return ChartTiming.GetNoteBeat(note, tempoMap);
    }

    private static int CompareRollSeriesEntries(RollSeriesEntry a, RollSeriesEntry b)
    {
        int byBeat = a.Beat.CompareTo(b.Beat);
        if (byBeat != 0)
            return byBeat;

        int byGenerated = a.IsGenerated.CompareTo(b.IsGenerated);
        return byGenerated != 0 ? byGenerated : a.Order.CompareTo(b.Order);
    }

    private readonly struct RollSeriesEntry
    {
        public RollSeriesEntry(double beat, bool isGenerated, bool isRoll, int order)
        {
            Beat = beat;
            IsGenerated = isGenerated;
            IsRoll = isRoll;
            Order = order;
        }

        public double Beat { get; }
        public bool IsGenerated { get; }
        public bool IsRoll { get; }
        public int Order { get; }
    }

    private readonly struct RollSeriesInfo
    {
        public RollSeriesInfo(int count, double lastBeat)
        {
            Count = count;
            LastBeat = lastBeat;
        }

        public int Count { get; }
        public double LastBeat { get; }
    }
}
