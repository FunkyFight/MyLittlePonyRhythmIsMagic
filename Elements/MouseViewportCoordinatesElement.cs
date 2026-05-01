using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MLP_RiM.Elements.DevUI;
using System.Globalization;

namespace MLP_RiM.Elements;

public sealed class MouseViewportCoordinatesElement
{
    private readonly DevUiRenderer _ui;
    private string _status = "F10 COPY";

    public MouseViewportCoordinatesElement(GraphicsDevice graphicsDevice)
    {
        _ui = new DevUiRenderer(graphicsDevice);
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        Viewport viewport = GLOBALS.graphicsDevice.Viewport;
        MouseState mouse = Mouse.GetState();
        float percentX = viewport.Width > 0 ? mouse.X / (float)viewport.Width * 100f : 0f;
        float percentY = viewport.Height > 0 ? mouse.Y / (float)viewport.Height * 100f : 0f;

        Rectangle panel = new(20, 20, 360, 70);
        _ui.Fill(spriteBatch, panel, new Color(0, 0, 0, 190));
        _ui.Stroke(spriteBatch, panel, Color.White, 2);
        _ui.Label(spriteBatch, "MOUSE VIEWPORT COORDS", new Vector2(34, 34), Color.LightBlue, 2);
        _ui.Label(spriteBatch, $"PX {mouse.X},{mouse.Y}  PCT {percentX:0.00}%,{percentY:0.00}%", new Vector2(34, 58), Color.White, 2);
        _ui.Label(spriteBatch, _status, new Vector2(34, 76), Color.LightGray, 2);
    }

    public string GetViewportExpression()
    {
        Viewport viewport = GLOBALS.graphicsDevice.Viewport;
        MouseState mouse = Mouse.GetState();
        float percentX = viewport.Width > 0 ? mouse.X / (float)viewport.Width : 0f;
        float percentY = viewport.Height > 0 ? mouse.Y / (float)viewport.Height : 0f;

        return string.Create(CultureInfo.InvariantCulture, $"vp.Width * {percentX:0.####}f, vp.Height * {percentY:0.####}f");
    }

    public void SetCopiedStatus(string text)
    {
        _status = $"COPIED {text}";
    }
}
