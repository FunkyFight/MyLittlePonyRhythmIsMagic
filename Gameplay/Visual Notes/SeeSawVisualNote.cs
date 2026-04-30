using System;
using System.Numerics;
using Microsoft.Xna.Framework.Graphics;
using Rhythm.Note;
using Rhythm.Note.Visual;
using GameCore.GameObjects;

public class SeeSawVisualNote : VisualNote
{
    private GameObject _jumper;
    private float _solY;
    private float _jumpHeight = 350f;

    public SeeSawVisualNote(Note logicalNote, GameObject jumper, float solY, double approachDuration, double despawnDelay = 0) 
        : base(logicalNote, approachDuration, despawnDelay)
    {
        _jumper = jumper;
        _solY = solY;
    }

    public override void Update(double currentSongPosition)
    {
        double startTime = Note.SongPosition - ApproachDuration;
        double endTime = Note.SongPosition;

        if (currentSongPosition < startTime)
        {
            return; 
        }

        if (currentSongPosition >= endTime)
        {
            _jumper.Position = new Vector2(_jumper.Position.X, _solY);
            return;
        }

        float progression = (float)((currentSongPosition - startTime) / ApproachDuration);
        float hauteurMult = (float)Math.Sin(progression * Math.PI);

        _jumper.Position = new Vector2(_jumper.Position.X, _solY - (_jumpHeight * hauteurMult));
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
    }
}