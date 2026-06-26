using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rhythm.Note.Visual;

public class RhythmInputVisualElement
{
    private const int PanelWidth = 540;
    private const int PanelHeight = 64;
    private const int PanelBottomMargin = 112;
    private const int ReactionYOffset = 32;

    private readonly BeatmapPlayer _beatmapPlayer;
    private readonly Texture2D _pixel;

    public RhythmInputVisualElement(BeatmapPlayer beatmapPlayer)
    {
        _beatmapPlayer = beatmapPlayer;
        _pixel = new Texture2D(GLOBALS.graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });

        _beatmapPlayer.VisualNoteMng = RhythmInputVisualNote.SetupRhythmInputVisualNoteVisualNoteManager(_beatmapPlayer.ChartPlayer, _pixel);
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        Viewport viewport = GLOBALS.graphicsDevice.Viewport;
        Rectangle panel = GetPanelBounds(viewport);
        Vector2 reactionOrigin = GetReactionOrigin(viewport);

        DrawPanel(spriteBatch, panel, reactionOrigin);
        _beatmapPlayer.VisualNoteMng?.Draw(spriteBatch);
        DrawReactionTarget(spriteBatch, reactionOrigin);
    }

    public static Vector2 GetReactionOrigin(Viewport viewport)
    {
        Rectangle panel = GetPanelBounds(viewport);
        return new Vector2(panel.Center.X, panel.Y + ReactionYOffset);
    }

    private static Rectangle GetPanelBounds(Viewport viewport)
    {
        int width = Math.Min(PanelWidth, viewport.Width - 48);
        return new Rectangle((viewport.Width - width) / 2, viewport.Height - PanelBottomMargin - PanelHeight, width, PanelHeight);
    }

    private void DrawPanel(SpriteBatch spriteBatch, Rectangle panel, Vector2 reactionOrigin)
    {
        spriteBatch.Draw(_pixel, panel, Color.Black * 0.68f);
        Stroke(spriteBatch, panel, Color.White * 0.55f, 2);

        int railY = (int)reactionOrigin.Y;
        spriteBatch.Draw(_pixel, new Rectangle(panel.X + 22, railY, panel.Width - 44, 2), Color.White * 0.62f);
    }

    private void DrawReactionTarget(SpriteBatch spriteBatch, Vector2 reactionOrigin)
    {
        int x = (int)MathF.Round(reactionOrigin.X);
        int y = (int)MathF.Round(reactionOrigin.Y);

        spriteBatch.Draw(_pixel, new Rectangle(x - 1, y - 17, 2, 34), Color.White);
        Stroke(spriteBatch, new Rectangle(x - 10, y - 10, 20, 20), Color.White * 0.85f, 2);
    }

    private void Stroke(SpriteBatch spriteBatch, Rectangle rectangle, Color color, int thickness)
    {
        spriteBatch.Draw(_pixel, new Rectangle(rectangle.X, rectangle.Y, rectangle.Width, thickness), color);
        spriteBatch.Draw(_pixel, new Rectangle(rectangle.X, rectangle.Bottom - thickness, rectangle.Width, thickness), color);
        spriteBatch.Draw(_pixel, new Rectangle(rectangle.X, rectangle.Y, thickness, rectangle.Height), color);
        spriteBatch.Draw(_pixel, new Rectangle(rectangle.Right - thickness, rectangle.Y, thickness, rectangle.Height), color);
    }
}
