using System;
using System.Collections.Generic;
using System.Linq;
using MLP_RiM.Elements.Editor;
using Rhythm.Note;

public sealed class ChartTempoMap
{
    private readonly List<ChartEffect> _bpmEffects;
    private readonly List<TempoSegment> _segments;
    private readonly double _baseBpm;
    private readonly double _baseOffset;

    private sealed class TempoSegment
    {
        public TempoSegment(double startSongPosition, double startBeat, double bpm)
        {
            StartSongPosition = startSongPosition;
            StartBeat = startBeat;
            Bpm = bpm;
        }

        public double StartSongPosition { get; }
        public double StartBeat { get; }
        public double Bpm { get; }
        public double Crotchet => Bpm > 0 ? 60.0 / Bpm : 0.6;

        public double GetBeatAt(double songPosition)
        {
            return StartBeat + (songPosition - StartSongPosition) / Crotchet;
        }

        public double GetSongPositionAtBeat(double beat)
        {
            return StartSongPosition + (beat - StartBeat) * Crotchet;
        }
    }

    public ChartTempoMap(Chart chart)
    {
        _baseBpm = chart?.BPM > 0 ? chart.BPM : 100;
        _baseOffset = chart?.Offset ?? 0.0;
        _bpmEffects = chart?.Effects == null
            ? new List<ChartEffect>()
            : chart.Effects
                .Where(effect => effect?.IsBpmChange == true && effect.TryGetBpm(out _))
                .OrderBy(effect => effect.SongPosition)
                .ToList();
        _segments = BuildTempoSegments();
    }

    public double GetBpmAt(double songPosition)
    {
        return FindSegmentBySongPosition(songPosition).Bpm;
    }

    public double GetCrotchetAt(double songPosition)
    {
        return GetCrotchetForBpm(GetBpmAt(songPosition));
    }

    public double GetTempoAnchorAt(double songPosition)
    {
        double anchor = _baseOffset;
        foreach (ChartEffect effect in _bpmEffects)
        {
            if (effect.SongPosition > songPosition)
                break;

            anchor = effect.GetSectionAnchorSongPosition();
        }

        return anchor;
    }

    public double GetBeatAt(double songPosition)
    {
        return FindSegmentBySongPosition(songPosition).GetBeatAt(songPosition);
    }

    public double GetSongPositionAtBeat(double beat)
    {
        return FindSegmentByBeat(beat).GetSongPositionAtBeat(beat);
    }

    public double GetMaxCrotchet()
    {
        double maxCrotchet = GetCrotchetForBpm(_baseBpm);
        foreach (ChartEffect effect in _bpmEffects)
        {
            if (effect.TryGetBpm(out double bpm) && bpm > 0)
                maxCrotchet = Math.Max(maxCrotchet, GetCrotchetForBpm(bpm));
        }

        return maxCrotchet;
    }

    public IEnumerable<EditorTempoSegment> GetTempoSegments(double startSongPosition, double endSongPosition)
    {
        if (endSongPosition < startSongPosition)
            (startSongPosition, endSongPosition) = (endSongPosition, startSongPosition);

        double anchor = _baseOffset;
        double segmentStart = _baseOffset;
        double bpm = _baseBpm;
        bool anchorIsRelativeToSegmentStart = false;

        foreach (ChartEffect effect in _bpmEffects)
        {
            if (effect.SongPosition > endSongPosition)
                break;

            if (effect.SongPosition > segmentStart && effect.SongPosition >= startSongPosition)
                yield return new EditorTempoSegment(anchor, segmentStart, Math.Max(segmentStart, startSongPosition), effect.SongPosition, bpm, anchorIsRelativeToSegmentStart);

            anchor = effect.GetSectionOffsetOrDefault(0);
            segmentStart = Math.Max(segmentStart, effect.SongPosition);
            anchorIsRelativeToSegmentStart = true;
            if (effect.TryGetBpm(out double effectBpm))
                bpm = effectBpm;
        }

        if (endSongPosition > segmentStart)
            yield return new EditorTempoSegment(anchor, segmentStart, Math.Max(segmentStart, startSongPosition), endSongPosition, bpm, anchorIsRelativeToSegmentStart);
    }

    private List<TempoSegment> BuildTempoSegments()
    {
        List<TempoSegment> segments = new()
        {
            new TempoSegment(_baseOffset, 0.0, _baseBpm)
        };

        double segmentStartSongPosition = _baseOffset;
        double segmentStartBeat = 0.0;
        double bpm = _baseBpm;

        foreach (ChartEffect effect in _bpmEffects)
        {
            double effectBeat = GetBeatInSegment(effect.SongPosition, segmentStartSongPosition, segmentStartBeat, bpm);
            if (!effect.TryGetBpm(out double effectBpm))
                continue;

            segmentStartSongPosition = effect.SongPosition;
            segmentStartBeat = effectBeat;
            bpm = effectBpm;
            segments.Add(new TempoSegment(segmentStartSongPosition, segmentStartBeat, bpm));
        }

        return segments;
    }

    private TempoSegment FindSegmentBySongPosition(double songPosition)
    {
        TempoSegment segment = _segments[0];
        for (int i = 1; i < _segments.Count; i++)
        {
            if (_segments[i].StartSongPosition > songPosition)
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

    private static double GetBeatInSegment(double songPosition, double segmentStartSongPosition, double segmentStartBeat, double bpm)
    {
        return segmentStartBeat + (songPosition - segmentStartSongPosition) / GetCrotchetForBpm(bpm);
    }

    private static double GetCrotchetForBpm(double bpm)
    {
        return bpm > 0 ? 60.0 / bpm : 0.6;
    }
}
