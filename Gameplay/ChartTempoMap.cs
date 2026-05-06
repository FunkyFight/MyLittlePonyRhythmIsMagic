using System;
using System.Collections.Generic;
using System.Linq;
using MLP_RiM.Elements.Editor;
using Rhythm.Note;

public sealed class ChartTempoMap
{
    private const double SameBeatEpsilon = 0.000000001;

    private readonly List<TimedBpmEffect> _bpmEffects;
    private readonly List<TempoSegment> _segments;
    private readonly double _baseBpm;
    private readonly double _baseOffset;

    private sealed class RawBpmEffect
    {
        public RawBpmEffect(ChartEffect effect, double bpm, int order)
        {
            Effect = effect;
            Bpm = bpm;
            Order = order;
        }

        public ChartEffect Effect { get; }
        public double Bpm { get; }
        public int Order { get; }
    }

    private sealed class TimedBpmEffect
    {
        public TimedBpmEffect(ChartEffect effect, double beat, double bpm, int order)
        {
            Effect = effect;
            Beat = beat;
            Bpm = bpm;
            Order = order;
        }

        public ChartEffect Effect { get; }
        public double Beat { get; }
        public double Bpm { get; }
        public int Order { get; }
    }

    private sealed class TempoSegment
    {
        public TempoSegment(double startBeat, double startSeconds, double bpm)
        {
            StartBeat = startBeat;
            StartSeconds = startSeconds;
            Bpm = NormalizeBpm(bpm);
        }

        public double StartBeat { get; }
        public double StartSeconds { get; }
        public double Bpm { get; }
        public double SecondsPerBeat => GetSecondsPerBeatForBpm(Bpm);

        public double BeatToSeconds(double beat)
        {
            return StartSeconds + (beat - StartBeat) * SecondsPerBeat;
        }

        public double SecondsToBeat(double seconds)
        {
            return StartBeat + (seconds - StartSeconds) / SecondsPerBeat;
        }
    }

    public ChartTempoMap(Chart chart)
    {
        _baseBpm = NormalizeBpm(chart?.BPM ?? 100);
        _baseOffset = chart?.Offset ?? 0.0;

        List<RawBpmEffect> rawEffects = CreateRawBpmEffects(chart).ToList();
        Dictionary<ChartEffect, double> legacyEffectBeats = rawEffects.Any(effect => !effect.Effect.BeatPosition.HasValue)
            ? BuildLegacyEffectBeatLookup(rawEffects)
            : new Dictionary<ChartEffect, double>();

        _bpmEffects = rawEffects
            .Select(effect => CreateTimedBpmEffect(effect, legacyEffectBeats))
            .Where(effect => effect != null)
            .OrderBy(effect => effect.Beat)
            .ThenBy(effect => effect.Order)
            .ToList();

        _segments = BuildTempoSegments();
    }

    public double BeatToSeconds(double beat)
    {
        return FindSegmentByBeat(beat).BeatToSeconds(beat);
    }

    public double SecondsToBeat(double seconds)
    {
        return FindSegmentBySeconds(seconds).SecondsToBeat(seconds);
    }

    public double GetBpmAtBeat(double beat)
    {
        return FindSegmentByBeat(beat).Bpm;
    }

    public double GetBpmAtSeconds(double seconds)
    {
        return FindSegmentBySeconds(seconds).Bpm;
    }

    public double GetSecondsPerBeatAtBeat(double beat)
    {
        return FindSegmentByBeat(beat).SecondsPerBeat;
    }

    public double GetSecondsPerBeatAtSeconds(double seconds)
    {
        return FindSegmentBySeconds(seconds).SecondsPerBeat;
    }

    public double GetBeatAt(double songPosition)
    {
        return SecondsToBeat(songPosition);
    }

    public double GetSongPositionAtBeat(double beat)
    {
        return BeatToSeconds(beat);
    }

    public double GetBpmAt(double songPosition)
    {
        return GetBpmAtSeconds(songPosition);
    }

    public double GetCrotchetAt(double songPosition)
    {
        return GetSecondsPerBeatAtSeconds(songPosition);
    }

    public double GetTempoAnchorAt(double songPosition)
    {
        double beat = SecondsToBeat(songPosition);
        if (double.IsNaN(beat) || double.IsInfinity(beat))
            return _baseOffset;

        return BeatToSeconds(Math.Floor(beat));
    }

    public double GetMaxCrotchet()
    {
        double maxCrotchet = GetSecondsPerBeatForBpm(_baseBpm);
        foreach (TimedBpmEffect effect in _bpmEffects)
            maxCrotchet = Math.Max(maxCrotchet, GetSecondsPerBeatForBpm(effect.Bpm));

        return maxCrotchet;
    }

    public bool HasNonZeroSectionOffset(ChartEffect effect, double epsilon = 0.0005)
    {
        return effect?.IsBpmChange == true
            && effect.TryGetSectionOffset(out double sectionOffset)
            && Math.Abs(sectionOffset) > epsilon;
    }

    public IEnumerable<EditorTempoSegment> GetTempoSegments(double startSongPosition, double endSongPosition)
    {
        if (endSongPosition < startSongPosition)
            (startSongPosition, endSongPosition) = (endSongPosition, startSongPosition);

        double startBeat = SecondsToBeat(startSongPosition);
        double endBeat = SecondsToBeat(endSongPosition);
        foreach (EditorBeatTempoSegment segment in GetTempoSegmentsByBeat(startBeat, endBeat))
        {
            yield return new EditorTempoSegment(
                segment.StartSeconds,
                segment.StartSeconds,
                segment.StartSeconds,
                segment.EndSeconds,
                segment.Bpm,
                anchorIsRelativeToSegmentStart: false);
        }
    }

    public IEnumerable<EditorBeatTempoSegment> GetTempoSegmentsByBeat(double startBeat, double endBeat)
    {
        if (endBeat < startBeat)
            (startBeat, endBeat) = (endBeat, startBeat);

        for (int i = 0; i < _segments.Count; i++)
        {
            TempoSegment segment = _segments[i];
            double segmentEndBeat = i + 1 < _segments.Count ? _segments[i + 1].StartBeat : endBeat;
            double clippedStartBeat = Math.Max(startBeat, segment.StartBeat);
            double clippedEndBeat = Math.Min(endBeat, segmentEndBeat);

            if (clippedEndBeat < clippedStartBeat)
                continue;

            yield return new EditorBeatTempoSegment(
                clippedStartBeat,
                clippedEndBeat,
                BeatToSeconds(clippedStartBeat),
                BeatToSeconds(clippedEndBeat),
                segment.Bpm);
        }
    }

    private static IEnumerable<RawBpmEffect> CreateRawBpmEffects(Chart chart)
    {
        if (chart?.Effects == null)
            yield break;

        int order = 0;
        foreach (ChartEffect effect in chart.Effects)
        {
            if (effect?.IsBpmChange == true && effect.TryGetBpm(out double bpm))
                yield return new RawBpmEffect(effect, NormalizeBpm(bpm), order);

            order++;
        }
    }

    private Dictionary<ChartEffect, double> BuildLegacyEffectBeatLookup(IReadOnlyList<RawBpmEffect> rawEffects)
    {
        Dictionary<ChartEffect, double> beats = new();
        double segmentStartSeconds = _baseOffset;
        double segmentStartBeat = 0.0;
        double bpm = _baseBpm;

        foreach (RawBpmEffect rawEffect in rawEffects.OrderBy(effect => effect.Effect.SongPosition).ThenBy(effect => effect.Order))
        {
            double effectSeconds = rawEffect.Effect.SongPosition;
            double effectBeat = segmentStartBeat + (effectSeconds - segmentStartSeconds) / GetSecondsPerBeatForBpm(bpm);
            beats[rawEffect.Effect] = effectBeat;

            segmentStartSeconds = effectSeconds;
            segmentStartBeat = effectBeat;
            bpm = rawEffect.Bpm;
        }

        return beats;
    }

    private static TimedBpmEffect CreateTimedBpmEffect(RawBpmEffect rawEffect, IReadOnlyDictionary<ChartEffect, double> legacyEffectBeats)
    {
        double beat = rawEffect.Effect.BeatPosition ?? (legacyEffectBeats.TryGetValue(rawEffect.Effect, out double legacyBeat) ? legacyBeat : double.NaN);
        if (double.IsNaN(beat) || double.IsInfinity(beat))
            return null;

        return new TimedBpmEffect(rawEffect.Effect, beat, rawEffect.Bpm, rawEffect.Order);
    }

    private List<TempoSegment> BuildTempoSegments()
    {
        List<TempoSegment> segments = new()
        {
            new TempoSegment(0.0, _baseOffset, _baseBpm)
        };

        foreach (TimedBpmEffect effect in _bpmEffects)
        {
            TempoSegment previous = segments[^1];
            double startSeconds = previous.BeatToSeconds(effect.Beat);
            TempoSegment next = new(effect.Beat, startSeconds, effect.Bpm);

            if (Math.Abs(previous.StartBeat - effect.Beat) <= SameBeatEpsilon)
                segments[^1] = next;
            else
                segments.Add(next);
        }

        return segments;
    }

    private TempoSegment FindSegmentBySeconds(double seconds)
    {
        TempoSegment segment = _segments[0];
        for (int i = 1; i < _segments.Count; i++)
        {
            if (_segments[i].StartSeconds > seconds)
                break;

            segment = _segments[i];
        }

        return segment;
    }

    private TempoSegment FindSegmentByBeat(double beat)
    {
        TempoSegment segment = _segments[0];
        for (int i = 1; i < _segments.Count; i++)
        {
            if (_segments[i].StartBeat > beat)
                break;

            segment = _segments[i];
        }

        return segment;
    }

    private static double NormalizeBpm(double bpm)
    {
        return bpm > 0 && !double.IsNaN(bpm) && !double.IsInfinity(bpm) ? bpm : 100;
    }

    private static double GetSecondsPerBeatForBpm(double bpm)
    {
        return bpm > 0 && !double.IsNaN(bpm) && !double.IsInfinity(bpm) ? 60.0 / bpm : 0.6;
    }
}

public readonly struct EditorBeatTempoSegment
{
    public EditorBeatTempoSegment(double startBeat, double endBeat, double startSeconds, double endSeconds, double bpm)
    {
        StartBeat = startBeat;
        EndBeat = endBeat;
        StartSeconds = startSeconds;
        EndSeconds = endSeconds;
        Bpm = bpm;
    }

    public double StartBeat { get; }
    public double EndBeat { get; }
    public double StartSeconds { get; }
    public double EndSeconds { get; }
    public double Bpm { get; }
    public double SecondsPerBeat => Bpm > 0 ? 60.0 / Bpm : 0.6;
}
