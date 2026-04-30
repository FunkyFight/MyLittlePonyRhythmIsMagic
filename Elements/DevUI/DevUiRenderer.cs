using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MLP_RiM.Elements.DevUI;

public sealed class DevUiRenderer
{
    private readonly Texture2D _pixel;

    public DevUiRenderer(GraphicsDevice graphicsDevice)
    {
        _pixel = new Texture2D(graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
    }

    public void Fill(SpriteBatch spriteBatch, Rectangle rectangle, Color color)
    {
        spriteBatch.Draw(_pixel, rectangle, color);
    }

    public void Stroke(SpriteBatch spriteBatch, Rectangle rectangle, Color color, int thickness = 1)
    {
        Fill(spriteBatch, new Rectangle(rectangle.X, rectangle.Y, rectangle.Width, thickness), color);
        Fill(spriteBatch, new Rectangle(rectangle.X, rectangle.Bottom - thickness, rectangle.Width, thickness), color);
        Fill(spriteBatch, new Rectangle(rectangle.X, rectangle.Y, thickness, rectangle.Height), color);
        Fill(spriteBatch, new Rectangle(rectangle.Right - thickness, rectangle.Y, thickness, rectangle.Height), color);
    }

    public void Line(SpriteBatch spriteBatch, Vector2 from, Vector2 to, Color color, float thickness = 1f)
    {
        Vector2 delta = to - from;
        float length = delta.Length();

        if (length <= 0)
            return;

        float rotation = MathF.Atan2(delta.Y, delta.X);
        spriteBatch.Draw(_pixel, from, null, color, rotation, Vector2.Zero, new Vector2(length, thickness), SpriteEffects.None, 0f);
    }

    public void Label(SpriteBatch spriteBatch, string text, Vector2 position, Color color, int scale = 2)
    {
        DevUiFont.Draw(spriteBatch, _pixel, text, position, color, scale);
    }
}
