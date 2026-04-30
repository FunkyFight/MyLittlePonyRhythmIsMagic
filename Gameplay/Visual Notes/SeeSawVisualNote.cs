using System;
using System.Numerics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rhythm.Note;
using Rhythm.Note.Visual;
using GameCore.GameObjects;

using Vector2 = System.Numerics.Vector2;

public class SeeSawVisualNote : VisualNote
{
    private GameObject _jumper;
    private Vector2 _fromPos;
    private Vector2 _toPos;
    private float _jumpHeight;
    private GameObject _seeSawBeam;
    private float _fromRotation;
    private float _targetRotation;
    private Vector2 _innerPos;
    private Vector2 _outerPos;
    private GameObject _counterJumper;
    private Vector2 _counterFromPos;
    private Vector2 _counterToPos;
    private Vector2 _counterInnerPos;
    private Vector2 _counterOuterPos;
    private float _counterJumpHeight;
    private float _counterTargetRotation;

    private const float OuterJumpHeight = 450f;
    private const float InnerJumpHeight = 180f;

    public SeeSawVisualNote(Note logicalNote, GameObject jumper, Vector2 fromPos, Vector2 toPos, double crotchet, GameObject seeSawBeam, float fromRotation, float targetRotation, Vector2 innerPos, Vector2 outerPos, double despawnDelay = 0)
        : this(logicalNote, jumper, fromPos, toPos, crotchet, seeSawBeam, fromRotation, targetRotation, innerPos, outerPos, null, Vector2.Zero, Vector2.Zero, 0, Vector2.Zero, Vector2.Zero, despawnDelay)
    {
    }

    public SeeSawVisualNote(Note logicalNote, GameObject jumper, Vector2 fromPos, Vector2 toPos, double crotchet, GameObject seeSawBeam, float fromRotation, float targetRotation, Vector2 innerPos, Vector2 outerPos, GameObject counterJumper, Vector2 counterFromPos, Vector2 counterToPos, float counterTargetRotation, Vector2 counterInnerPos, Vector2 counterOuterPos, double despawnDelay = 0) 
        : base(logicalNote, GetApproachDuration(crotchet, fromPos, innerPos, outerPos), despawnDelay)
    {
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

        _jumpHeight = GetJumpHeight(_fromPos, _innerPos, _outerPos);

        if (_counterJumper != null)
        {
            _counterJumpHeight = GetJumpHeight(_counterFromPos, _counterInnerPos, _counterOuterPos);
        }
    }

    public override void Update(double currentSongPosition)
    {
        UpdateState(currentSongPosition);

        double startTime = Note.SongPosition - ApproachDuration;
        double endTime = Note.SongPosition;

        if (currentSongPosition < startTime)
        {
            ApplyBeforeState();
            return; 
        }

        float progression = (float)((currentSongPosition - startTime) / (endTime - startTime));

        if (currentSongPosition >= endTime)
        {
            if (_counterJumper != null)
                _counterJumper.Position = _counterToPos;

            _jumper.Position = _toPos;
            _seeSawBeam.Rotation = _targetRotation;
            return;
        }

        if (_counterJumper != null)
        {
            if (progression <= 0.5f)
            {
                float counterProgression = progression / 0.5f;
                ApplyJump(_counterJumper, _counterFromPos, _counterToPos, _counterJumpHeight, counterProgression);
                _seeSawBeam.Rotation = _counterTargetRotation;
            }
            else
            {
                _counterJumper.Position = _counterToPos;
                _seeSawBeam.Rotation = _counterTargetRotation;
            }
        }

        if (progression >= 0.5f)
        {
            float jumperProgression = (progression - 0.5f) / 0.5f;
            ApplyJump(_jumper, _fromPos, _toPos, _jumpHeight, jumperProgression);
            _seeSawBeam.Rotation = _targetRotation;
        }
        else
        {
            _jumper.Position = _fromPos;
        }
    }

    private void ApplyBeforeState()
    {
        _jumper.Position = _fromPos;
        _seeSawBeam.Rotation = _fromRotation;

        if (_counterJumper != null)
            _counterJumper.Position = _counterFromPos;
    }

    private static float GetJumpHeight(Vector2 fromPos, Vector2 innerPos, Vector2 outerPos)
    {
        float distToInner = Vector2.Distance(fromPos, innerPos);
        float distToOuter = Vector2.Distance(fromPos, outerPos);
        return (distToInner < distToOuter) ? InnerJumpHeight : OuterJumpHeight;
    }

    private static double GetApproachDuration(double crotchet, Vector2 fromPos, Vector2 innerPos, Vector2 outerPos)
    {
        float distToInner = Vector2.Distance(fromPos, innerPos);
        float distToOuter = Vector2.Distance(fromPos, outerPos);
        return distToInner < distToOuter ? crotchet : crotchet * 2.0;
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
