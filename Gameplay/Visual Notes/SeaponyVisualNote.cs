using System;
using GameCore.Animation;
using GameCore.GameObjects;
using Microsoft.Xna.Framework.Graphics;
using Rhythm.Note;
using Rhythm.Note.Visual;

public class SeaponyVisualNote : VisualNote
{

    private GameObject _seaPony;
    private AnimationStateMachine _seaPonyStateMachine;
    private int _seaPonyIndex;

    private int _backgroundScrollDestinationBeat;

    private bool _wasControlling = false;

    public SeaponyVisualNote(Note logicalNote, double approachDuration, GameObject seaPony, AnimationStateMachine seaPonyStateMachine, int seaPonyIndex, double despawnDelay = 0) : base(logicalNote, approachDuration, despawnDelay)
    {
        this._seaPony = seaPony;
        this._seaPonyIndex = seaPonyIndex;
        this._seaPonyStateMachine = seaPonyStateMachine;
    }

    public override void Update(double currentSongPosition)
    {
        base.Update(currentSongPosition);

        bool inTimeWindow = RhythmVisualUtils.IsInTimeWindow(currentSongPosition, Note.SongPosition, ApproachDuration, DespawnDelay);

        switch(Note.AdditionnalData["action"])
        {
            case "seapony_parade_swim":
                handleSwim(inTimeWindow);
                break;
        }
    }

    private void handleSwim(bool inTimeWindow)
    {
        if(!inTimeWindow)
        {
            if(_wasControlling)
            {
                _seaPonyStateMachine.ForceState("idle");
                _wasControlling = false;
            }
        }


        if(Progress <= double.Epsilon)
        {
            return;
        }

        if(Progress < 1 && !Note.HasReacted)
        {
            _seaPonyStateMachine.ForceState("swim_anticipation");
            _wasControlling = true;
            return;
        }
        
        if(Progress >= 1 && !State.HasDespawned)
        {
            if(_seaPonyIndex == 1) return;
            _seaPonyStateMachine.ForceState("swim");
            _wasControlling = true;
            return;
        }
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
    }
}