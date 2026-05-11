using System;
using GameCore;
using GameCore.Animation;
using GameCore.Audio;
using GameCore.GameObjects;
using GameCore.Scenes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MLP_RiM.Elements.Editor;
using Rhythm.Note;

public class SeaponyVisualNote : DirectedVisualNote
{
    private const string SeaPonyTrackPrefix = "seapony";
    private const string IdleState = "idle";
    private const string SwimAnticipationState = "swim_anticipation";
    private const string SwimState = "swim";
    private const string RollState = "roll";
    private const string UptapState = "uptap";
    private const string DowntapState = "downtap";
    private const float TapTapGroupOffsetX = 64f;
    private const float TapTapPairOffsetX = 42f;
    private const float TapTapRightFacingDowntapBackOffsetX = TapTapPairOffsetX;

    public float RollTargetRotation { get; private set; }

    private readonly float _rollTargetRotation;
    private readonly double _crotchet;
    private readonly int _rollIndexInSequence;
    private readonly int _rollsRemainingInSequence;
    private readonly int _tapTapIndexInSequence;
    private readonly int _tapTapHitsRemainingInSequence;
    private readonly Func<int, Note, bool> _canRollProvider;
    private readonly Func<Note, bool> _hasSuccessfulReactionProvider;
    private readonly Func<Note, bool> _hasPerfectReactionProvider;
    private readonly Func<bool> _canApplyState;
    private readonly bool _usesRuntimeOwnership;
    private readonly string _seaPonyTrackId;
    private readonly string _seaPonyAnimationTrackId;

    private Scene _scene;
    private GameObject _seaPony;
    private AnimationStateMachine _seaPonyStateMachine;
    private int _seaPonyIndex;
    private readonly Vector2 _baseSeaPonyPosition;
    private VisualContext _activeContext;

    private bool _wasControlling = false;
    private int _lastTapTapHitsPassed = int.MinValue;
    private bool _lastTapTapReactionState = false;

    public bool _canRoll = true;

    /// <summary>
    /// Retourne l'identifiant de track runtime qui représente le GameObject d'un sea pony.
    /// </summary>
    /// <param name="seaPonyIndex">Index logique du sea pony dans la scène.</param>
    /// <returns>Identifiant stable de track objet.</returns>
    public static string GetPonyTrackId(int seaPonyIndex)
    {
        return $"{SeaPonyTrackPrefix}.{seaPonyIndex}.object";
    }

    /// <summary>
    /// Retourne l'identifiant de track runtime qui représente la machine d'animation d'un sea pony.
    /// </summary>
    /// <param name="seaPonyIndex">Index logique du sea pony dans la scène.</param>
    /// <returns>Identifiant stable de track animation.</returns>
    public static string GetPonyAnimationTrackId(int seaPonyIndex)
    {
        return $"{SeaPonyTrackPrefix}.{seaPonyIndex}.animation";
    }

    public SeaponyVisualNote(Note logicalNote, double approachDuration, Scene scene, GameObject seaPony, AnimationStateMachine seaPonyStateMachine, int seaPonyIndex, double crotchet, float rollTargetRotation = 0f, double despawnDelay = 0, bool canRoll = true, int rollIndexInSequence = 0, int rollsRemainingInSequence = 0, Func<int, Note, bool> canRollProvider = null, Func<Note, bool> hasSuccessfulReactionProvider = null, Func<Note, bool> hasPerfectReactionProvider = null, Func<bool> canApplyState = null, int tapTapIndexInSequence = 0, int tapTapHitsRemainingInSequence = 1, Vector2? baseSeaPonyPosition = null, VisualRuntime runtime = null, string seaPonyTrackId = null, string seaPonyAnimationTrackId = null) : base(logicalNote, runtime ?? new VisualRuntime(), approachDuration, despawnDelay)
    {
        this._scene = scene;
        this._seaPony = seaPony;
        this._seaPonyIndex = seaPonyIndex;
        this._seaPonyStateMachine = seaPonyStateMachine;
        _baseSeaPonyPosition = baseSeaPonyPosition ?? seaPony?.Position ?? Vector2.Zero;
        _rollTargetRotation = rollTargetRotation;
        _crotchet = crotchet;
        _rollIndexInSequence = rollIndexInSequence;
        _rollsRemainingInSequence = rollsRemainingInSequence;
        _tapTapIndexInSequence = tapTapIndexInSequence;
        _tapTapHitsRemainingInSequence = tapTapHitsRemainingInSequence;
        _canRollProvider = canRollProvider;
        _hasSuccessfulReactionProvider = hasSuccessfulReactionProvider;
        _hasPerfectReactionProvider = hasPerfectReactionProvider;
        _canApplyState = canApplyState;
        _usesRuntimeOwnership = runtime != null;
        _seaPonyTrackId = seaPonyTrackId ?? GetPonyTrackId(seaPonyIndex);
        _seaPonyAnimationTrackId = seaPonyAnimationTrackId ?? GetPonyAnimationTrackId(seaPonyIndex);
        this.RollTargetRotation = rollTargetRotation;
        this._canRoll = canRoll;
    }

    /// <summary>
    /// Déclare les blocs de timeline qui recouvrent toute la durée de vie de la note SeaPony.
    /// </summary>
    /// <param name="timeline">Timeline déclarative fournie par <see cref="DirectedVisualNote"/>.</param>
    protected override void Build(VisualTimeline timeline)
    {
        timeline.StableBefore("seapony_before_approach")
            .Owns(_seaPonyTrackId, _seaPonyAnimationTrackId)
            .Do(sample);

        timeline.DuringApproach("seapony_approach")
            .Owns(_seaPonyTrackId, _seaPonyAnimationTrackId)
            .Do((ctx, phase) =>
            {
                if(ctx.IsAtOrAfterHit)
                    return;

                sample(ctx);
            });

        timeline.AfterHitUntilDespawn("seapony_after_hit")
            .Owns(_seaPonyTrackId, _seaPonyAnimationTrackId)
            .Do((ctx, phase) => sample(ctx));

        timeline.StableAfter("seapony_after_despawn")
            .Owns(_seaPonyTrackId, _seaPonyAnimationTrackId)
            .Do(sample);
    }

    private void sample(VisualContext ctx)
    {
        _activeContext = ctx;
        try
        {
            sampleCore(ctx);
        }
        finally
        {
            _activeContext = null;
        }
    }

    private void sampleCore(VisualContext ctx)
    {
        double currentSongPosition = ctx.SongPosition;

        if(ctx.HasRewound)
        {
            resetTapTapScale();
            _wasControlling = false;
            _lastTapTapHitsPassed = int.MinValue;
            _lastTapTapReactionState = false;
        }

        if(!tryGetAction(out SeaponyAction action))
            return;

        bool inTimeWindow = RhythmVisualUtils.IsInTimeWindow(currentSongPosition, Note.SongPosition, ApproachDuration, DespawnDelay);

        if(action == SeaponyAction.TapTap)
            handleTapTapSfx(ctx);

        if(!canApplyState(ctx))
        {
            if(action == SeaponyAction.TapTap && _wasControlling && !hasNextTapTapTakenOver(currentSongPosition))
                resetTapTapScale();

            _wasControlling = false;
            return;
        }

        switch(action)
        {
            case SeaponyAction.Swim:
                handleSwim(inTimeWindow, currentSongPosition, ctx);
                break;

            case SeaponyAction.Roll:
                handleRoll(inTimeWindow, currentSongPosition, ctx);
                break;

            case SeaponyAction.TapTap:
                handleTapTap(inTimeWindow, currentSongPosition);
                break;

            case SeaponyAction.Leave:
                handleLeave(inTimeWindow, currentSongPosition);
                break;

            default:
                break;
        }
    }

    private void handleRoll(bool inTimeWindow, double currentSongPosition, VisualContext ctx)
    {
        float startRoll = _rollTargetRotation - 90;

        if(currentSongPosition < Note.SongPosition - ApproachDuration)
        {
            if(_wasControlling)
            {
                forceSeaPonyAnimation(IdleState);
                _wasControlling = false;
                mutateSeaPony(pony => pony.Rotation = 0);
                resetTapTapScale();
            }

            return;
        }

        if(!inTimeWindow)
        {
            if(_wasControlling)
            {
                if(_seaPonyIndex != 1)
                {
                    forceSeaPonyAnimation(IdleState);
                    mutateSeaPony(pony => pony.Rotation = MathHelper.ToRadians(_rollTargetRotation));
                    resetTapTapScale();
                }
                else
                {
                    forceSeaPonyAnimation(IdleState);
                    mutateSeaPony(pony => pony.Rotation = 0);
                    resetTapTapScale();
                }

                _wasControlling = false;
            }

            return;
        }

        resetTapTapScale();

        if(currentSongPosition < Note.SongPosition)
        {
            if(isRollApproachOverlappingPreviousTapTail(currentSongPosition))
            {
                forceSeaPonyAnimation(RollState);
                mutateSeaPony(pony => pony.Rotation = _seaPonyIndex == 1 ? 0f : MathHelper.ToRadians(startRoll));
                _wasControlling = true;
            }

            return;
        }

        if(Progress <= double.Epsilon || currentSongPosition < Note.SongPosition)
        {
            return;
        }

        if(_rollsRemainingInSequence == 1)
            playSfxOnForwardCross(Note.SongPosition, "SFX/AndStop.wav", ctx);

        double rollDuration = DespawnDelay <= double.Epsilon ? ApproachDuration : DespawnDelay;
        double rollStartSongPosition = Note.SongPosition;
        double rollEndSongPosition = Note.SongPosition + rollDuration;

        if(_seaPonyIndex == 1)
        {
            if(!canRoll())
            {
                if(!Note.HasReacted && _rollIndexInSequence > 0)
                {
                    forceSeaPonyAnimation(RollState);
                    mutateSeaPony(pony => pony.Rotation = MathHelper.ToRadians(startRoll));
                    _wasControlling = true;
                    return;
                }

                forceSeaPonyAnimation(IdleState);
                mutateSeaPony(pony => pony.Rotation = 0);
                _wasControlling = true;
                return;
            }

            forceSeaPonyAnimation(RollState);

            float authorizedRollProgress = (float)RhythmVisualUtils.GetProgression(rollStartSongPosition, rollEndSongPosition, currentSongPosition);
            float authorizedInterpolated = Interpolation.EaseOutQuint(authorizedRollProgress);
            float authorizedCurrentRoll = Single.Lerp(startRoll, _rollTargetRotation, authorizedInterpolated) % 360;

            mutateSeaPony(pony => pony.Rotation = MathHelper.ToRadians(authorizedCurrentRoll));
            _wasControlling = true;
            return;
        }

        if(!_canRoll) return;

        forceSeaPonyAnimation(RollState);

        float rollProgress = (float)RhythmVisualUtils.GetProgression(rollStartSongPosition, rollEndSongPosition, currentSongPosition);
        float interpolated = Interpolation.EaseOutQuint(rollProgress);
        float currentRoll = Single.Lerp(startRoll, _rollTargetRotation, interpolated) % 360;

        mutateSeaPony(pony => pony.Rotation = MathHelper.ToRadians(currentRoll));
        _wasControlling = true;
    }

    private bool isRollApproachOverlappingPreviousTapTail(double currentSongPosition)
    {
        return IsTapTapNote(PreviousNote)
            && currentSongPosition < PreviousNote.SongPosition + _crotchet * 2;
    }

    private void handleSwim(bool inTimeWindow, double currentSongPosition, VisualContext ctx)
    {
        if(!inTimeWindow)
        {
            if(_wasControlling)
            {
                forceSeaPonyAnimation(IdleState);
                resetTapTapScale();
                _wasControlling = false;
            }

            return;
        }

        resetTapTapScale();


        if(Progress <= double.Epsilon)
        {
            return;
        }

        playSfxOnForwardCross(Note.SongPosition - ApproachDuration, "SFX/Bubble.wav", ctx);

        if(currentSongPosition < Note.SongPosition)
        {
            if(_seaPonyIndex == 1 && Note.HasReacted && !hasSuccessfulReaction())
            {
                forceSeaPonyAnimation(IdleState);
                _wasControlling = true;
                return;
            }

            forceSeaPonyAnimation(SwimAnticipationState);
            _wasControlling = true;
            return;
        }
        
        if(!State.HasDespawned)
        {
            if(_seaPonyIndex == 1)
            {
                forceSeaPonyAnimation(hasSuccessfulReaction() ? SwimState : IdleState);
                _wasControlling = true;
                return;
            }

            forceSeaPonyAnimation(SwimState);
            _wasControlling = true;
            return;
        }
    }

    private void handleTapTap(bool inTimeWindow, double currentSongPosition)
    {
        if(!inTimeWindow)
        {
            if(_wasControlling)
            {
                forceSeaPonyAnimation(IdleState);
                resetTapTapScale();
                _wasControlling = false;
            }

            return;
        }

        int poseIndex = getTapTapPoseIndex(currentSongPosition);
        bool reactionState = hasSuccessfulReaction();
        if(_wasControlling && _lastTapTapHitsPassed == poseIndex && _lastTapTapReactionState == reactionState)
        {
            applyTapTapOrientation(poseIndex);
            return;
        }

        forceSeaPonyAnimation(getTapTapState(poseIndex));
        applyTapTapOrientation(poseIndex);
        _lastTapTapHitsPassed = poseIndex;
        _lastTapTapReactionState = reactionState;
        _wasControlling = true;
    }

    private void handleLeave(bool inTimeWindow, double currentSongPosition)
    {
        if(!inTimeWindow)
        {
            if(_wasControlling)
            {
                forceSeaPonyAnimation(IdleState);
                resetTapTapScale();
                _wasControlling = false;
            }

            return;
        }

        resetTapTapScale();
        forceSeaPonyAnimation(SwimState);
        mutateSeaPony(pony => pony.Rotation = 0f);
        _wasControlling = true;
    }

    private int getTapTapHitsPassed(double currentSongPosition)
    {
        return currentSongPosition >= Note.SongPosition ? _tapTapIndexInSequence + 1 : _tapTapIndexInSequence;
    }

    private int getTapTapPoseIndex(double currentSongPosition)
    {
        if(isPlayerTapTapPony())
            return _tapTapIndexInSequence + (hasSuccessfulReaction() ? 1 : 0);

        return getTapTapHitsPassed(currentSongPosition);
    }

    private void handleTapTapSfx(VisualContext ctx)
    {
        playSfxOnForwardCross(Note.SongPosition, "SFX/seapony_parade_roll.wav", ctx);
        if(_tapTapHitsRemainingInSequence == 1)
            playSfxOnForwardCross(Note.SongPosition, "SFX/AndStop.wav", ctx);
    }

    private void applyTapTapOrientation(int poseIndex)
    {
        if(_seaPony == null)
            return;

        Vector2 positiveScale = getPositiveSeaPonyScale();
        mutateSeaPony(pony =>
        {
            pony.Scale = positiveScale;
            if(pony.sprite != null)
                pony.sprite.Effects = isTapTapLeftFacingPony() ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
        });

        applyTapTapPosition(poseIndex);
        mutateSeaPony(pony => pony.Rotation = 0f);
    }

    private void applyTapTapPosition(int poseIndex)
    {
        mutateSeaPony(pony => pony.Position = _baseSeaPonyPosition + new Vector2(getTapTapOffsetX(poseIndex), 0f));
    }

    private float getTapTapOffsetX(int poseIndex)
    {
        int visualSeaPonyIndex = getVisualSeaPonyIndex();
        float pairOffset = visualSeaPonyIndex % 2 == 0 ? TapTapPairOffsetX : -TapTapPairOffsetX;

        if(isRightFacingPonyRecedingOnDowntap(visualSeaPonyIndex, poseIndex))
            pairOffset -= TapTapRightFacingDowntapBackOffsetX;

        return TapTapGroupOffsetX + pairOffset;
    }

    private void resetTapTapScale()
    {
        if(_seaPony == null)
            return;

        Vector2 positiveScale = getPositiveSeaPonyScale();
        mutateSeaPony(pony =>
        {
            pony.Scale = positiveScale;
            if(pony.sprite != null)
                pony.sprite.Effects = SpriteEffects.None;

            pony.Position = _baseSeaPonyPosition;
        });
        _lastTapTapHitsPassed = int.MinValue;
        _lastTapTapReactionState = false;
    }

    private Vector2 getPositiveSeaPonyScale()
    {
        if(_seaPony == null)
            return Vector2.One;

        float scaleX = Math.Abs(_seaPony.Scale.X);
        float scaleY = Math.Abs(_seaPony.Scale.Y);
        if(scaleX <= float.Epsilon)
            scaleX = scaleY > float.Epsilon ? scaleY : 1f;
        if(scaleY <= float.Epsilon)
            scaleY = scaleX;

        return new Vector2(scaleX, scaleY);
    }

    private bool isTapTapLeftFacingPony()
    {
        return getVisualSeaPonyIndex() % 2 != 0;
    }

    private bool isPerfectLockedTapTapPony()
    {
        return getVisualSeaPonyIndex() == 2;
    }

    private bool isRightFacingPonyRecedingOnDowntap(int visualSeaPonyIndex, int poseIndex)
    {
        return (visualSeaPonyIndex == 0 || visualSeaPonyIndex == 2)
            && getTapTapState(poseIndex) == DowntapState;
    }

    private bool hasNextTapTapTakenOver(double currentSongPosition)
    {
        return IsTapTapNote(NextNote) && currentSongPosition >= NextNote.SongPosition;
    }

    private static bool IsTapTapNote(Note note)
    {
        return SeaponyNoteCodec.IsAction(note?.AdditionnalData, SeaponyAction.TapTap);
    }

    private string getTapTapState(int poseIndex)
    {
        bool invertBasePose = poseIndex % 2 != 0;
        bool useDowntap = isTapTapLeftFacingPony() != invertBasePose;
        return useDowntap ? DowntapState : UptapState;
    }

    private bool isPlayerTapTapPony()
    {
        int visualSeaPonyIndex = getVisualSeaPonyIndex();
        return visualSeaPonyIndex == 2 || visualSeaPonyIndex == 3;
    }

    private int getVisualSeaPonyIndex()
    {
        return _seaPonyIndex switch
        {
            3 => 0,
            2 => 1,
            1 => 2,
            0 => 3,
            _ => _seaPonyIndex
        };
    }

    private void mutateSeaPony(Action<GameObject> mutation)
    {
        if(mutation == null)
            return;

        if(_usesRuntimeOwnership && _activeContext != null)
        {
            _activeContext.Mutate(_seaPonyTrackId, mutation);
            return;
        }

        if(_seaPony != null)
            mutation(_seaPony);
    }

    private void forceSeaPonyAnimation(string stateName, string reenterViaState = null)
    {
        if(_usesRuntimeOwnership && _activeContext != null)
        {
            _activeContext.ForceAnimation(_seaPonyAnimationTrackId, stateName, reenterViaState);
            return;
        }

        RhythmVisualUtils.ForceAnimationState(_seaPonyStateMachine, stateName, reenterViaState);
    }

    private void playSfxOnForwardCross(double cuePosition, string filePath, VisualContext ctx)
    {
        if(_seaPonyIndex != 0)
            return;

        if(ctx.ForwardCrossed(filePath, cuePosition) && GLOBALS.SfxVolume > 0)
            SFX.Play(_scene, filePath, 4);
    }

    private bool canRoll()
    {
        return _canRoll && (_canRollProvider?.Invoke(_seaPonyIndex, Note) ?? true);
    }

    /// <summary>
    /// Indique si cette visual note peut muter le sea pony partagé pendant la frame courante.
    /// </summary>
    /// <param name="currentSongPosition">Position musicale courante, réservée au runtime pour ses policies.</param>
    /// <returns><c>true</c> si les tracks runtime sont drivées par cette note, ou si le guard legacy l'autorise.</returns>
    private bool canApplyState(VisualContext ctx)
    {
        if(_usesRuntimeOwnership)
            return ctx.CanWrite(_seaPonyTrackId) && ctx.CanWrite(_seaPonyAnimationTrackId);

        return RhythmVisualUtils.CanApplyState(_canApplyState);
    }

    private bool hasSuccessfulReaction()
    {
        return _hasSuccessfulReactionProvider?.Invoke(Note) == true;
    }

    private bool hasPerfectReaction()
    {
        return _hasPerfectReactionProvider?.Invoke(Note) == true;
    }

    private bool tryGetAction(out SeaponyAction action)
    {
        return SeaponyNoteCodec.TryReadAction(Note?.AdditionnalData, out action);
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
    }
}


