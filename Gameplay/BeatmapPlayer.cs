using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Rhythm.Conductor;
using Rhythm.Note;
using Rhythm.Note.Evaluator;
using Rhythm.Note.Visual;

public class BeatmapPlayer
{
    public Conductor Conductor {get; private set;}
    public ChartPlayer ChartPlayer {get; private set;}
    public Chart CurrentChart {get; private set;}
    public VisualNoteManager<VisualNote> VisualNoteMng {get; set;}

    public event Action BeatmapStarted;

    private double _startupDelay;
    private double _startupTimer;
    private bool _startupComplete;

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
            ChartPlayer?.Update(Conductor.SongPosition);
            VisualNoteMng?.Update(Conductor.SongPosition);
        }
    }
    

    public void StartMetronomeDebugMap(Dictionary<string, string> additionnalData, double startupDelaySeconds = 5.0)
    {
        _startupDelay = startupDelaySeconds;
        _startupTimer = 0;
        _startupComplete = false;

        int bpm = 100;

        Conductor = new Conductor("Songs/metronome.wav", bpm, 0.078);
        CurrentChart = Chart.CreateMetronome(bpm, 200, startupDelaySeconds, additionnalData);
        ChartPlayer = new ChartPlayer(CurrentChart, ReactionRules.RhythmHeavenLike(), new RhythmHeavenLikeReactionEvaluator());

        BeatmapStarted?.Invoke();
    }

    public void StartSeeSawDebugMap(Dictionary<string, string> additionnalData, double startupDelaySeconds = 5.0)
    {
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
        ChartPlayer = new ChartPlayer(chart, ReactionRules.RhythmHeavenLike(), new RhythmHeavenLikeReactionEvaluator());

        BeatmapStarted?.Invoke();
    }

    public void StartBeatmap(string song_path, Chart chart, ReactionRules rules, IReactionEvaluator reactionEvaluator)
    {
        _startupDelay = 0;
        _startupTimer = 0;
        _startupComplete = true;

        Conductor = new Conductor(song_path, chart.BPM, chart.Offset);
        CurrentChart = chart;
        ChartPlayer = new ChartPlayer(chart, rules, reactionEvaluator);
        Conductor.Play();
        BeatmapStarted?.Invoke();

    }

    public void StartBeatmapPaused(string songPath, Chart chart, ReactionRules rules, IReactionEvaluator reactionEvaluator, double firstBeatDelay = double.NaN)
    {
        _startupDelay = 0;
        _startupTimer = 0;
        _startupComplete = true;

        double beatDelay = double.IsNaN(firstBeatDelay) ? chart.Offset : firstBeatDelay;
        Conductor = new Conductor(songPath, chart.BPM, beatDelay);
        CurrentChart = chart;
        ChartPlayer = new ChartPlayer(chart, rules, reactionEvaluator);
        BeatmapStarted?.Invoke();
    }
}
