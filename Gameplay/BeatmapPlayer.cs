using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Rhythm.Conductor;
using Rhythm.Note;
using Rhythm.Note.Evaluator;
using Rhythm.Note.Visual;

public class BeatmapPlayer : IDisposable
{
    public Conductor Conductor {get; private set;}
    public ChartPlayer ChartPlayer {get; private set;}
    public Chart CurrentChart {get; private set;}
    public bool HasAChartLoaded { get; private set; }
    public VisualNoteManager<VisualNote> VisualNoteMng {get; set;}

    public event Action BeatmapStarted;

    private double _startupDelay;
    private double _startupTimer;
    private bool _startupComplete;
    private ChartTempoMap _tempoMap;

    public BeatmapPlayer(Conductor conductor, ChartPlayer chartPlayer)
    {
        this.Conductor = conductor;
        this.ChartPlayer = chartPlayer;
    }

    public BeatmapPlayer()
    {
        this.Conductor = null;
        this.ChartPlayer = null;
    }

    public void Update(GameTime gameTime)
    {
        if(Conductor == null) return;

        if(!_startupComplete)
        {
            _startupTimer += (double)gameTime.ElapsedGameTime.TotalSeconds;
            if(_startupTimer >= _startupDelay)
            {
                _startupComplete = true;
                Conductor.Play();
            }
            return;
        }

        if(Conductor.isPlaying())
        {
            Conductor.Update();
            ApplyChartEffectsAt(Conductor.SongPosition);
            ChartPlayer?.Update(Conductor.SongPosition);
            VisualNoteMng?.Update(Conductor.SongPosition);
        }
    }
    

    public void StartMetronomeDebugMap(Dictionary<string, string> additionnalData, double startupDelaySeconds = 5.0)
    {
        DisposeConductor();

        _startupDelay = startupDelaySeconds;
        _startupTimer = 0;
        _startupComplete = false;

        int bpm = 100;

        Conductor = new Conductor("Songs/metronome.wav", bpm, 0.078);
        CurrentChart = Chart.CreateMetronome(bpm, 200, startupDelaySeconds, additionnalData);
        _tempoMap = new ChartTempoMap(CurrentChart);
        HasAChartLoaded = true;
        ChartPlayer = new ChartPlayer(CurrentChart, ReactionRules.RhythmHeavenLike(), new RhythmHeavenLikeReactionEvaluator());

        BeatmapStarted?.Invoke();
    }

    public void StartSeeSawDebugMap(Dictionary<string, string> additionnalData, double startupDelaySeconds = 5.0)
    {
        DisposeConductor();

        _startupDelay = startupDelaySeconds;
        _startupTimer = 0;
        _startupComplete = false;

        int bpm = 100;
        double crotchet = 60.0 / bpm;
        Chart chart = new Chart
        {
            SongName = "See Saw Debug",
            BPM = bpm
        };

        for (int i = 0; i < 200; i++)
        {
            chart.Notes.Add(new ChartNote
            {
                SongPosition = startupDelaySeconds + i * crotchet * 2.0,
                HoldDuration = 0,
                InputActionToPress = "ReactMain",
                AdditionnalData = additionnalData
            });
        }

        Conductor = new Conductor("Songs/metronome.wav", bpm, 0.078);
        CurrentChart = chart;
        _tempoMap = new ChartTempoMap(CurrentChart);
        HasAChartLoaded = true;
        ChartPlayer = new ChartPlayer(chart, ReactionRules.RhythmHeavenLike(), new RhythmHeavenLikeReactionEvaluator());

        BeatmapStarted?.Invoke();
    }

    public void StartBeatmap(string song_path, Chart chart, ReactionRules rules, IReactionEvaluator reactionEvaluator)
    {
        DisposeConductor();

        _startupDelay = 0;
        _startupTimer = 0;
        _startupComplete = true;

        Conductor = new Conductor(song_path, chart.BPM, chart.Offset);
        CurrentChart = chart;
        _tempoMap = new ChartTempoMap(CurrentChart);
        HasAChartLoaded = true;
        ChartPlayer = new ChartPlayer(chart, rules, reactionEvaluator);
        ApplyChartEffectsAt(0);
        Conductor.Play();
        BeatmapStarted?.Invoke();

    }

    public void StartBeatmapPaused(string songPath, Chart chart, ReactionRules rules, IReactionEvaluator reactionEvaluator, double firstBeatDelay = double.NaN)
    {
        DisposeConductor();

        _startupDelay = 0;
        _startupTimer = 0;
        _startupComplete = true;

        double beatDelay = double.IsNaN(firstBeatDelay) ? chart.Offset : firstBeatDelay;
        Conductor = new Conductor(songPath, chart.BPM, beatDelay);
        CurrentChart = chart;
        _tempoMap = new ChartTempoMap(CurrentChart);
        HasAChartLoaded = true;
        ChartPlayer = new ChartPlayer(chart, rules, reactionEvaluator);
        ApplyChartEffectsAt(0);
        BeatmapStarted?.Invoke();
    }

    public void ApplyChartEffectsAt(double songPosition)
    {
        if (Conductor == null || CurrentChart == null)
            return;

        double bpm = GetBpmAt(songPosition);
        if (Math.Abs(Conductor.BPM - bpm) > 0.0005)
            Conductor.SetBpm(bpm);
    }

    public double GetBpmAt(double songPosition)
    {
        if (CurrentChart == null)
            return Conductor?.BPM ?? 100;

        return GetTempoMap().GetBpmAt(songPosition);
    }

    public double GetCrotchetAt(double songPosition)
    {
        return CurrentChart == null
            ? Conductor?.Crotchet ?? 0.6
            : GetTempoMap().GetCrotchetAt(songPosition);
    }

    public double GetBeatAt(double songPosition)
    {
        return CurrentChart == null
            ? songPosition / (Conductor?.Crotchet ?? 0.6)
            : GetTempoMap().GetBeatAt(songPosition);
    }

    public double GetSongPositionAtBeat(double beat)
    {
        return CurrentChart == null
            ? beat * (Conductor?.Crotchet ?? 0.6)
            : GetTempoMap().GetSongPositionAtBeat(beat);
    }

    public double GetMaxCrotchet()
    {
        return CurrentChart == null
            ? Conductor?.Crotchet ?? 0.6
            : GetTempoMap().GetMaxCrotchet();
    }

    public void Dispose()
    {
        DisposeConductor();
        GC.SuppressFinalize(this);
    }

    private void DisposeConductor()
    {
        Conductor?.Dispose();
        Conductor = null;
        CurrentChart = null;
        _tempoMap = null;
        HasAChartLoaded = false;
        ChartPlayer = null;
        VisualNoteMng = null;
    }

    private ChartTempoMap GetTempoMap()
    {
        if (_tempoMap == null)
            _tempoMap = new ChartTempoMap(CurrentChart);

        return _tempoMap;
    }
}
