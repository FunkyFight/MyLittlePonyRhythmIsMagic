using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rhythm.Note;
using Rhythm.Note.Visual;

namespace MLP_RiM.Elements.Editor;

public sealed class EditorRhythmInputVisualNote : VisualNote
{
    private readonly Texture2D _pixel;
    private readonly Vector2 _reactionOrigin;

    public EditorRhythmInputVisualNote(Note logicalNote, Texture2D pixel, Vector2 reactionOrigin) : base(logicalNote, 2, 2)
    {
        _pixel = pixel;
        _reactionOrigin = reactionOrigin;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        double startX = GLOBALS.graphicsDevice.Viewport.Width * 1.1;
        double x = UnclampedProgress <= 1
            ? ApproachThrough(startX, _reactionOrigin.X)
            : _reactionOrigin.X + PostHitSameSpeed(startX, _reactionOrigin.X);

        Rectangle body = new((int)x - 12, (int)_reactionOrigin.Y - 12, 24, 24);
        spriteBatch.Draw(_pixel, body, Color.DeepSkyBlue);
    }
}
