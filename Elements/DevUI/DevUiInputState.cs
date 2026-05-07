using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace MLP_RiM.Elements.DevUI;

public readonly struct DevUiInputState
{
    public DevUiInputState(MouseState mouse, MouseState previousMouse, KeyboardState keyboard, KeyboardState previousKeyboard)
    {
        Mouse = mouse;
        PreviousMouse = previousMouse;
        Keyboard = keyboard;
        PreviousKeyboard = previousKeyboard;
    }

    public MouseState Mouse { get; }
    public MouseState PreviousMouse { get; }
    public KeyboardState Keyboard { get; }
    public KeyboardState PreviousKeyboard { get; }

    public Point Pointer => Mouse.Position;
    public int WheelDelta => Mouse.ScrollWheelValue - PreviousMouse.ScrollWheelValue;
    public bool LeftDown => Mouse.LeftButton == ButtonState.Pressed;
    public bool RightDown => Mouse.RightButton == ButtonState.Pressed;
    public bool LeftPressed => Mouse.LeftButton == ButtonState.Pressed && PreviousMouse.LeftButton == ButtonState.Released;
    public bool LeftReleased => Mouse.LeftButton == ButtonState.Released && PreviousMouse.LeftButton == ButtonState.Pressed;
    public bool RightPressed => Mouse.RightButton == ButtonState.Pressed && PreviousMouse.RightButton == ButtonState.Released;
    public bool RightReleased => Mouse.RightButton == ButtonState.Released && PreviousMouse.RightButton == ButtonState.Pressed;

    public bool KeyPressed(Keys key)
    {
        return Keyboard.IsKeyDown(key) && !PreviousKeyboard.IsKeyDown(key);
    }
}
