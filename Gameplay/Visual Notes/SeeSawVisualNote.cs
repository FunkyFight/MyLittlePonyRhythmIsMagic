using System;
using System.Numerics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rhythm.Note;
using Rhythm.Note.Visual;
using GameCore.GameObjects;
using System.Collections.Generic;
using GameCore.Animation;

using Vector2 = System.Numerics.Vector2;

public class SeeSawVisualNote : VisualNote
{
    private readonly Dictionary<SeeSawJumper, GameObject> _jumpers;
    private readonly Dictionary<SeeSawJumper, AnimationStateMachine> _animationStates;
    private readonly SeeSawJumper _jumper;
    private readonly SeeSawJumper? _counterJumper;
    private bool _counterJumpStarted;
    private bool _counterLanded;
    private bool _jumperJumpStarted;
    private bool _jumperLanded;
    private Vector2 _fromPos;
    private Vector2 _toPos;
    private float _jumpHeight;
    private GameObject _seeSawBeam;
    private float _fromRotation;
    private float _targetRotation;
    private Vector2 _innerPos;
    private Vector2 _outerPos;
    private Vector2 _counterFromPos;
    private Vector2 _counterToPos;
    private Vector2 _counterInnerPos;
    private Vector2 _counterOuterPos;
    private float _counterJumpHeight;
    private float _counterTargetRotation;
    private float _counterRotationProgression;
    private float _jumperStartProgression;
    private Func<bool> _canApplyState;
    private double _lastSongPosition = double.NaN;

    private const float OuterJumpHeight = 450f;
    private const float InnerJumpHeight = 180f;
    private const float DefaultCounterRotationProgression = 0.5f;
    private const float DefaultJumperStartProgression = 0.5f;

    public SeeSawVisualNote(Note logicalNote, Dictionary<SeeSawJumper, GameObject> jumpers, Dictionary<SeeSawJumper, AnimationStateMachine> animationStates, SeeSawJumper jumper, Vector2 fromPos, Vector2 toPos, double crotchet, GameObject seeSawBeam, float fromRotation, float targetRotation, Vector2 innerPos, Vector2 outerPos, double despawnDelay = 0)
        : this(logicalNote, jumpers, animationStates, jumper, fromPos, toPos, crotchet, seeSawBeam, fromRotation, targetRotation, innerPos, outerPos, null, Vector2.Zero, Vector2.Zero, 0, Vector2.Zero, Vector2.Zero, DefaultCounterRotationProgression, DefaultJumperStartProgression, null, despawnDelay)
    {
    }

    public SeeSawVisualNote(Note logicalNote, Dictionary<SeeSawJumper, GameObject> jumpers, Dictionary<SeeSawJumper, AnimationStateMachine> animationStates, SeeSawJumper jumper, Vector2 fromPos, Vector2 toPos, double crotchet, GameObject seeSawBeam, float fromRotation, float targetRotation, Vector2 innerPos, Vector2 outerPos, Func<bool> canApplyState, double despawnDelay = 0)
        : this(logicalNote, jumpers, animationStates, jumper, fromPos, toPos, crotchet, seeSawBeam, fromRotation, targetRotation, innerPos, outerPos, null, Vector2.Zero, Vector2.Zero, 0, Vector2.Zero, Vector2.Zero, DefaultCounterRotationProgression, DefaultJumperStartProgression, canApplyState, despawnDelay)
    {
    }

    public SeeSawVisualNote(Note logicalNote, Dictionary<SeeSawJumper, GameObject> jumpers, Dictionary<SeeSawJumper, AnimationStateMachine> animationStates, SeeSawJumper jumper, Vector2 fromPos, Vector2 toPos, double crotchet, GameObject seeSawBeam, float fromRotation, float targetRotation, Vector2 innerPos, Vector2 outerPos, SeeSawJumper? counterJumper, Vector2 counterFromPos, Vector2 counterToPos, float counterTargetRotation, Vector2 counterInnerPos, Vector2 counterOuterPos, float counterRotationProgression = DefaultCounterRotationProgression, float jumperStartProgression = DefaultJumperStartProgression, Func<bool> canApplyState = null, double despawnDelay = 0, double? approachDuration = null) 
        : base(logicalNote, approachDuration ?? GetApproachDuration(crotchet, fromPos, innerPos, outerPos), despawnDelay)
    {
        _jumpers = jumpers;
        _animationStates = animationStates;
        _jumper = jumper;
        _fromPos = fromPos;
        _toPos = toPos;
        _seeSawBeam = seeSawBeam;
        _fromRotation = fromRotation;
        _targetRotation = targetRotation;
        _innerPos = innerPos;
        _outerPos = outerPos;
        _counterJumper = counterJumper;
        _counterFromPos = counterFromPos;
        _counterToPos = counterToPos;
        _counterTargetRotation = counterTargetRotation;
        _counterInnerPos = counterInnerPos;
        _counterOuterPos = counterOuterPos;
        _counterRotationProgression = counterRotationProgression;
        _jumperStartProgression = jumperStartProgression;
        _canApplyState = canApplyState;

        _jumpHeight = GetJumpHeight(_fromPos, _innerPos, _outerPos);

        if (_counterJumper.HasValue)
        {
            _counterJumpHeight = GetJumpHeight(_counterFromPos, _counterInnerPos, _counterOuterPos);
        }
    }

    public override void Update(double currentSongPosition)
    {
        UpdateState(currentSongPosition);

        if (!double.IsNaN(_lastSongPosition) && currentSongPosition < _lastSongPosition - 0.001)
            ResetAnimationTriggers();

        _lastSongPosition = currentSongPosition;

        if (_canApplyState != null && !_canApplyState())
            return;

        double startTime = Note.SongPosition - ApproachDuration;
        double endTime = Note.SongPosition;

        if (currentSongPosition < startTime)
        {
            ResetAnimationTriggers();
            return; 
        }

        float progression = (float)((currentSongPosition - startTime) / (endTime - startTime));

        if (currentSongPosition >= endTime)
        {
            if (_counterJumper.HasValue)
            {
                Land(_counterJumper.Value);
                GetJumper(_counterJumper.Value).Position = _counterToPos;
            }

            Land(_jumper);
            GetJumper(_jumper).Position = _toPos;
            _seeSawBeam.Rotation = _targetRotation;
            return;
        }

        if (_counterJumper.HasValue)
        {
            if (progression < 0.5f)
            {
                StartJump(_counterJumper.Value);
                float counterProgression = progression / 0.5f;
                ApplyJumpAnimation(_counterJumper.Value, counterProgression);
                ApplyJump(GetJumper(_counterJumper.Value), _counterFromPos, _counterToPos, _counterJumpHeight, counterProgression);
            }
            else
            {
                Land(_counterJumper.Value);
                GetJumper(_counterJumper.Value).Position = _counterToPos;
            }
        }

        if (progression >= _jumperStartProgression)
        {
            StartJump(_jumper);
            float jumperProgression = (progression - _jumperStartProgression) / (1f - _jumperStartProgression);
            ApplyJumpAnimation(_jumper, jumperProgression);
            ApplyJump(GetJumper(_jumper), _fromPos, _toPos, _jumpHeight, jumperProgression);
        }
        else
        {
            GetJumper(_jumper).Position = _fromPos;
        }

        if (_counterJumper.HasValue && progression >= _counterRotationProgression)
            _seeSawBeam.Rotation = _counterTargetRotation;
        else
            _seeSawBeam.Rotation = _fromRotation;
    }

    private void ApplyBeforeState()
    {
        GetJumper(_jumper).Position = _fromPos;

        if (_counterJumper.HasValue)
            GetJumper(_counterJumper.Value).Position = _counterFromPos;
    }

    private void ResetAnimationTriggers()
    {
        _counterJumpStarted = false;
        _counterLanded = false;
        _jumperJumpStarted = false;
        _jumperLanded = false;
    }

    private GameObject GetJumper(SeeSawJumper jumper)
    {
        return _jumpers[jumper];
    }

    private void StartJump(SeeSawJumper jumper)
    {
        if (jumper == _jumper)
        {
            if (_jumperJumpStarted)
                return;

            _jumperJumpStarted = true;
        }
        else
        {
            if (_counterJumpStarted)
                return;

            _counterJumpStarted = true;
        }

        ForceAnimationState(jumper, "jump");
    }

    private void Land(SeeSawJumper jumper)
    {
        if (jumper == _jumper)
        {
            if (_jumperLanded)
                return;

            _jumperLanded = true;
        }
        else
        {
            if (_counterLanded)
                return;

            _counterLanded = true;
        }

        ForceAnimationState(jumper, "land");
    }

    private void ApplyJumpAnimation(SeeSawJumper jumper, float jumpProgression)
    {
        ForceAnimationState(jumper, jumpProgression < 0.5f ? "jump" : "fall");
    }

    private void ForceAnimationState(SeeSawJumper jumper, string stateName)
    {
        if (jumper != SeeSawJumper.APPLEJACK && stateName == "fall")
            return;

        if (_animationStates == null || !_animationStates.TryGetValue(jumper, out AnimationStateMachine stateMachine))
            return;

        if (stateMachine.CurrentState?.Name == stateName && stateName != "land")
            return;

        if (stateMachine.CurrentState?.Name == stateName)
            stateMachine.ForceState("jump");

        stateMachine.ForceState(stateName);
    }

    private static float GetJumpHeight(Vector2 fromPos, Vector2 innerPos, Vector2 outerPos)
    {
        float distToInner = Vector2.Distance(fromPos, innerPos);
        float distToOuter = Vector2.Distance(fromPos, outerPos);
        return (distToInner < distToOuter) ? InnerJumpHeight : OuterJumpHeight;
    }

    public static double GetApproachDuration(double crotchet, Vector2 fromPos, Vector2 innerPos, Vector2 outerPos)
    {
        float distToInner = Vector2.Distance(fromPos, innerPos);
        float distToOuter = Vector2.Distance(fromPos, outerPos);
        return distToInner < distToOuter ? crotchet * 2.0 : crotchet * 3.0;
    }

    private static void ApplyJump(GameObject jumper, Vector2 fromPos, Vector2 toPos, float jumpHeight, float progression)
    {
        float hauteurMult = (float)Math.Sin(progression * Math.PI);

        Vector2 basePos = Vector2.Lerp(fromPos, toPos, progression);
        jumper.Position = new Vector2(basePos.X, basePos.Y - (jumpHeight * hauteurMult));
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
    }
}

public enum SeeSawJumper
{
    APPLEJACK,
    RAINBOW_DASH
}
