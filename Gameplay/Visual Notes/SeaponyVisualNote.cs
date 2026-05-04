using System;
using GameCore;
using GameCore.Animation;
using GameCore.Audio;
using GameCore.GameObjects;
using GameCore.Scenes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rhythm.Note;
using Rhythm.Note.Visual;

public class SeaponyVisualNote : VisualNote
{
    private const string ActionDataKey = "action";
    private const string SwimAction = "seapony_parade_swim";
    private const string RollAction = "seapony_parade_roll";
    private const string IdleState = "idle";
    private const string SwimAnticipationState = "swim_anticipation";
    private const string SwimState = "swim";
    private const string RollState = "roll";

    public float RollTargetRotation { get; private set; }

    private readonly float _rollTargetRotation;
    private readonly double _crotchet;
    private readonly int _rollIndexInSequence;
    private readonly int _rollsRemainingInSequence;
    private readonly Func<int, Note, bool> _canRollProvider;
    private readonly Func<Note, bool> _hasSuccessfulReactionProvider;
    private readonly Func<bool> _canApplyState;

    private Scene _scene;
    private GameObject _seaPony;
    private AnimationStateMachine _seaPonyStateMachine;
    private int _seaPonyIndex;

    private int _backgroundScrollDestinationBeat;

    private bool _wasControlling = false;
    private double _lastSongPosition = double.NaN;

    public bool _canRoll = true;

    public SeaponyVisualNote(Note logicalNote, double approachDuration, Scene scene, GameObject seaPony, AnimationStateMachine seaPonyStateMachine, int seaPonyIndex, double crotchet, float rollTargetRotation = 0f, double despawnDelay = 0, bool canRoll = true, int rollIndexInSequence = 0, int rollsRemainingInSequence = 0, Func<int, Note, bool> canRollProvider = null, Func<Note, bool> hasSuccessfulReactionProvider = null, Func<bool> canApplyState = null) : base(logicalNote, approachDuration, despawnDelay)
    {
        this._scene = scene;
        this._seaPony = seaPony;
        this._seaPonyIndex = seaPonyIndex;
        this._seaPonyStateMachine = seaPonyStateMachine;
        _rollTargetRotation = rollTargetRotation;
        _crotchet = crotchet;
        _rollIndexInSequence = rollIndexInSequence;
        _rollsRemainingInSequence = rollsRemainingInSequence;
        _canRollProvider = canRollProvider;
        _hasSuccessfulReactionProvider = hasSuccessfulReactionProvider;
        _canApplyState = canApplyState;
        this.RollTargetRotation = rollTargetRotation;
        this._canRoll = canRoll;
    }

    public override void Update(double currentSongPosition)
    {
        base.Update(currentSongPosition);

        if(RhythmVisualUtils.HasRewound(currentSongPosition, _lastSongPosition))
            _wasControlling = false;

        if(!tryGetAction(out string action))
        {
            _lastSongPosition = currentSongPosition;
            return;
        }

        bool inTimeWindow = RhythmVisualUtils.IsInTimeWindow(currentSongPosition, Note.SongPosition, ApproachDuration, DespawnDelay);

        if(!RhythmVisualUtils.CanApplyState(_canApplyState))
        {
            _wasControlling = false;
            _lastSongPosition = currentSongPosition;
            return;
        }

        switch(action)
        {
            case SwimAction:
                handleSwim(inTimeWindow, currentSongPosition);
                break;

            case RollAction:
                handleRoll(inTimeWindow, currentSongPosition);
                break;

            default:
                break;
        }

        _lastSongPosition = currentSongPosition;
    }

    private void handleRoll(bool inTimeWindow, double currentSongPosition)
    {
        float startRoll = _rollTargetRotation - 90;

        if(currentSongPosition < Note.SongPosition - ApproachDuration)
        {
            if(_wasControlling)
            {
                RhythmVisualUtils.ForceAnimationState(_seaPonyStateMachine, IdleState);
                _wasControlling = false;
                _seaPony.Rotation = 0;
            }

            return;
        }

        if(!inTimeWindow)
        {
            if(_wasControlling)
            {
                if(_seaPonyIndex != 1)
                {
                    RhythmVisualUtils.ForceAnimationState(_seaPonyStateMachine, IdleState);
                    _seaPony.Rotation = MathHelper.ToRadians(_rollTargetRotation);
                }
                else
                {
                    RhythmVisualUtils.ForceAnimationState(_seaPonyStateMachine, IdleState);
                    _seaPony.Rotation = 0;
                }

                _wasControlling = false;
            }

            return;
        }

        if(_rollIndexInSequence == 0)
        {
            playSfxOnForwardCross(Note.SongPosition - 2 * _crotchet, currentSongPosition, "SFX/BubbleHeavy.wav");
            playSfxOnForwardCross(Note.SongPosition - _crotchet, currentSongPosition, "SFX/BubbleHeavy.wav");
        }

        if(Progress <= double.Epsilon || currentSongPosition < Note.SongPosition)
        {
            return;
        }

        if(_rollsRemainingInSequence <= 2)
            playSfxOnForwardCross(Note.SongPosition, currentSongPosition, "SFX/BubbleHeavy.wav");

        double rollDuration = DespawnDelay <= double.Epsilon ? ApproachDuration : DespawnDelay;
        double rollStartSongPosition = Note.SongPosition;
        double rollEndSongPosition = Note.SongPosition + rollDuration;

        if(_seaPonyIndex == 1)
        {
            if(!canRoll())
            {
                RhythmVisualUtils.ForceAnimationState(_seaPonyStateMachine, IdleState);
                _seaPony.Rotation = 0;
                _wasControlling = true;
                return;
            }

            RhythmVisualUtils.ForceAnimationState(_seaPonyStateMachine, RollState);

            float authorizedRollProgress = (float)RhythmVisualUtils.GetProgression(rollStartSongPosition, rollEndSongPosition, currentSongPosition);
            float authorizedInterpolated = Interpolation.EaseOutQuint(authorizedRollProgress);
            float authorizedCurrentRoll = Single.Lerp(startRoll, _rollTargetRotation, authorizedInterpolated) % 360;

            _seaPony.Rotation = MathHelper.ToRadians(authorizedCurrentRoll);
            _wasControlling = true;
            return;
        }

        if(!_canRoll) return;

        RhythmVisualUtils.ForceAnimationState(_seaPonyStateMachine, RollState);

        float rollProgress = (float)RhythmVisualUtils.GetProgression(rollStartSongPosition, rollEndSongPosition, currentSongPosition);
        float interpolated = Interpolation.EaseOutQuint(rollProgress);
        float currentRoll = Single.Lerp(startRoll, _rollTargetRotation, interpolated) % 360;

        _seaPony.Rotation = MathHelper.ToRadians(currentRoll);
        _wasControlling = true;
    }

    private void handleSwim(bool inTimeWindow, double currentSongPosition)
    {
        if(!inTimeWindow)
        {
            if(_wasControlling)
            {
                RhythmVisualUtils.ForceAnimationState(_seaPonyStateMachine, IdleState);
                _wasControlling = false;
            }

            return;
        }


        if(Progress <= double.Epsilon)
        {
            return;
        }

        playSfxOnForwardCross(Note.SongPosition - ApproachDuration, currentSongPosition, "SFX/Bubble.wav");

        if(currentSongPosition < Note.SongPosition)
        {
            if(_seaPonyIndex == 1 && Note.HasReacted && !hasSuccessfulReaction())
            {
                RhythmVisualUtils.ForceAnimationState(_seaPonyStateMachine, IdleState);
                _wasControlling = true;
                return;
            }

            RhythmVisualUtils.ForceAnimationState(_seaPonyStateMachine, SwimAnticipationState);
            _wasControlling = true;
            return;
        }
        
        if(!State.HasDespawned)
        {
            if(_seaPonyIndex == 1)
            {
                RhythmVisualUtils.ForceAnimationState(_seaPonyStateMachine, hasSuccessfulReaction() ? SwimState : IdleState);
                _wasControlling = true;
                return;
            }

            RhythmVisualUtils.ForceAnimationState(_seaPonyStateMachine, SwimState);
            _wasControlling = true;
            return;
        }
    }

    private void playSfxOnForwardCross(double cuePosition, double currentSongPosition, string filePath)
    {
        if(_seaPonyIndex != 0)
            return;

        if(double.IsNaN(_lastSongPosition))
            return;

        if(_lastSongPosition < cuePosition && currentSongPosition >= cuePosition && GLOBALS.SfxVolume > 0)
            SFX.Play(_scene, filePath, GLOBALS.SfxVolume);
    }

    private bool canRoll()
    {
        return _canRoll && (_canRollProvider?.Invoke(_seaPonyIndex, Note) ?? true);
    }

    private bool hasSuccessfulReaction()
    {
        return _hasSuccessfulReactionProvider?.Invoke(Note) == true;
    }

    private bool tryGetAction(out string action)
    {
        action = string.Empty;
        return Note.AdditionnalData != null
            && Note.AdditionnalData.TryGetValue(ActionDataKey, out action)
            && (action == SwimAction || action == RollAction);
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
    }
}
