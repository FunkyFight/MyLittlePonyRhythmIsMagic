public sealed class TempoMappedRhythmClock
{
    private readonly ChartTempoMap _tempoMap;

    public TempoMappedRhythmClock(ChartTempoMap tempoMap)
    {
        _tempoMap = tempoMap;
        Reset();
    }

    public double SongSeconds { get; private set; }
    public double Beat { get; private set; }
    public double PreviousBeat { get; private set; }
    public double Bpm { get; private set; }
    public double SecondsPerBeat { get; private set; }

    public void Reset(double songSeconds = 0.0)
    {
        SongSeconds = songSeconds;
        Beat = _tempoMap?.SecondsToBeat(songSeconds) ?? 0.0;
        PreviousBeat = Beat;
        Bpm = _tempoMap?.GetBpmAtSeconds(songSeconds) ?? 100.0;
        SecondsPerBeat = _tempoMap?.GetSecondsPerBeatAtSeconds(songSeconds) ?? 0.6;
    }

    public void Update(double songSeconds)
    {
        SongSeconds = songSeconds;
        PreviousBeat = Beat;
        Beat = _tempoMap?.SecondsToBeat(songSeconds) ?? songSeconds / 0.6;
        Bpm = _tempoMap?.GetBpmAtSeconds(songSeconds) ?? 100.0;
        SecondsPerBeat = _tempoMap?.GetSecondsPerBeatAtSeconds(songSeconds) ?? 0.6;
    }
}
