using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rhythm.Note.Visual;

public class RhythmInputVisualElement
{
    private BeatmapPlayer _beatmapPlayer;
    private Texture2D _pixel;

    public RhythmInputVisualElement(BeatmapPlayer beatmapPlayer)
    {
        _beatmapPlayer = beatmapPlayer;
        _beatmapPlayer.VisualNoteMng = RhythmInputVisualNote.SetupRhythmInputVisualNoteVisualNoteManager(_beatmapPlayer.ChartPlayer);

        _pixel = new Texture2D(GLOBALS.graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        _beatmapPlayer.VisualNoteMng?.Draw(spriteBatch);

        float reactionX = GLOBALS.graphicsDevice.Viewport.Width / 2f;
        float reactionY = GLOBALS.graphicsDevice.Viewport.Height / 2f + GLOBALS.graphicsDevice.Viewport.Height / 4f;
        float lineHeight = 100f;
        float lineWidth = 6f;

        Rectangle lineRect = new Rectangle(
            (int)(reactionX - lineWidth / 2f),
            (int)(reactionY + 25 - lineHeight / 2f),
            (int)lineWidth,
            (int)lineHeight
        );

        spriteBatch.Draw(_pixel, lineRect, Color.Red);
    }
}
