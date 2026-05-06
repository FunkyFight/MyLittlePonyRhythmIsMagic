using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rhythm.Note;
using Rhythm.Note.Visual;
using System;

namespace MLP_RiM.Elements.Editor;

public sealed class EditorRhythmInputVisualNote : VisualNote
{
    private readonly Texture2D _pixel;
    private readonly Vector2 _reactionOrigin;
    private readonly double _hitBeat;
    private readonly double _approachBeats;
    private readonly double _exitBeats;
    private readonly Func<double, double> _getBeatAt;

    public EditorRhythmInputVisualNote(Note logicalNote, double hitBeat, double approachBeats, double exitBeats, Func<double, double> getBeatAt, Texture2D pixel, Vector2 reactionOrigin)
        : base(logicalNote, Math.Max(0.001, approachBeats * 0.6), Math.Max(0.0, exitBeats * 0.6))
    {
        _pixel = pixel;
        _reactionOrigin = reactionOrigin;
        _hitBeat = hitBeat;
        _approachBeats = Math.Max(0.001, approachBeats);
        _exitBeats = Math.Max(0.0, exitBeats);
        _getBeatAt = getBeatAt;
    }

    public override void Update(double currentSongPosition)
    {
        double currentBeat = _getBeatAt?.Invoke(currentSongPosition) ?? currentSongPosition / 0.6;
        double spawnBeat = _hitBeat - _approachBeats;
        double despawnBeat = _hitBeat + _exitBeats;
        double unclampedProgress = (currentBeat - spawnBeat) / _approachBeats;
        double progress = Math.Clamp(unclampedProgress, 0.0, 1.0);
        double postHitProgress = _exitBeats <= 0 ? (currentBeat >= _hitBeat ? 1.0 : 0.0) : Math.Clamp((currentBeat - _hitBeat) / _exitBeats, 0.0, 1.0);
        SetState(new VisualNoteState(
            Note.SongPosition - currentSongPosition,
            progress,
            currentBeat >= _hitBeat ? 1.0 : 0.0,
            progress,
            postHitProgress,
            unclampedProgress,
            currentBeat >= spawnBeat && currentBeat <= despawnBeat,
            currentBeat >= spawnBeat,
            currentBeat > despawnBeat));
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        double startX = GLOBALS.graphicsDevice.Viewport.Width * 1.1;
        double x = UnclampedProgress <= 1
            ? LerpUnclamped(startX, _reactionOrigin.X, UnclampedProgress)
            : _reactionOrigin.X + (_reactionOrigin.X - startX) * (UnclampedProgress - 1.0);

        Rectangle body = new((int)x - 12, (int)_reactionOrigin.Y - 12, 24, 24);
        spriteBatch.Draw(_pixel, body, Color.DeepSkyBlue);
    }

    private static double LerpUnclamped(double start, double end, double progress)
    {
        return start + (end - start) * progress;
    }

}
