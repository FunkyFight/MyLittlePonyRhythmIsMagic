using System;
using System.Collections.Generic;
using Rhythm.Conductor;
using Rhythm.Note;
using Rhythm.Note.Evaluator;
using Rhythm.Note.Visual;

public class BeatmapPlayer
{
    public Conductor Conductor {get; private set;}
    public ChartPlayer ChartPlayer {get; private set;}
    public VisualNoteManager<VisualNote> VisualNoteMng {get; set;}

    public event Action BeatmapStarted;


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

    public void Update()
    {
        if(Conductor != null && Conductor.isPlaying())
        {
            Conductor.Update();
            ChartPlayer?.Update(Conductor.SongPosition);
            VisualNoteMng?.Update(Conductor.SongPosition);
        }
    }
    

    public void StartMetronomeDebugMap(Dictionary<string, string> additionnalData)
    {
        Conductor = new Conductor("Songs/metronome.wav", 100, 0.078);
        ChartPlayer = new ChartPlayer(Chart.CreateMetronome(50, 200, 0, additionnalData), ReactionRules.RhythmHeavenLike(), new RhythmHeavenLikeReactionEvaluator());
        Conductor.Play();

        BeatmapStarted?.Invoke();
    }

    public void StartBeatmap(string song_path, Chart chart, ReactionRules rules, IReactionEvaluator reactionEvaluator)
    {
        Conductor = new Conductor(song_path, 100, 0.078);
        ChartPlayer = new ChartPlayer(chart, rules, reactionEvaluator);
        Conductor.Play();
        BeatmapStarted.Invoke();

    }
}