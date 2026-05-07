using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MLP_RiM.Elements.DevUI;

public readonly struct DevUiButtonResult
{
    public DevUiButtonResult(bool hovered, bool clicked)
    {
        Hovered = hovered;
        Clicked = clicked;
    }

    public bool Hovered { get; }
    public bool Clicked { get; }
}

public readonly struct DevUiTabResult
{
    public DevUiTabResult(int selectedIndex, bool changed)
    {
        SelectedIndex = selectedIndex;
        Changed = changed;
    }

    public int SelectedIndex { get; }
    public bool Changed { get; }
}

public sealed class DevUiControls
{
    private readonly DevUiRenderer _ui;

    public DevUiControls(DevUiRenderer ui)
    {
        _ui = ui;
    }

    public DevUiButtonResult Button(SpriteBatch spriteBatch, Rectangle bounds, string text, DevUiInputState input)
    {
        bool hovered = bounds.Contains(input.Pointer);
        bool clicked = hovered && input.LeftPressed;
        Color background = hovered ? new Color(24, 58, 36, 245) : new Color(18, 36, 24, 245);

        _ui.Fill(spriteBatch, bounds, background);
        _ui.Stroke(spriteBatch, bounds, clicked ? Color.White : Color.LightGreen, 1);
        _ui.Label(spriteBatch, text, new Vector2(bounds.X + 8, bounds.Y + 8), clicked ? Color.White : Color.LightGreen, 2);
        return new DevUiButtonResult(hovered, clicked);
    }

    public void Panel(SpriteBatch spriteBatch, Rectangle bounds, string header = null)
    {
        _ui.Fill(spriteBatch, bounds, new Color(8, 10, 14, 225));
        _ui.Stroke(spriteBatch, bounds, Color.DarkSlateGray, 1);
        if (string.IsNullOrWhiteSpace(header))
            return;

        Rectangle headerBounds = new(bounds.X, bounds.Y, bounds.Width, 26);
        _ui.Fill(spriteBatch, headerBounds, new Color(18, 36, 24, 245));
        _ui.Label(spriteBatch, header, new Vector2(bounds.X + 8, bounds.Y + 8), Color.LightGreen, 2);
    }

    public DevUiTabResult Tabs(SpriteBatch spriteBatch, Rectangle bounds, IReadOnlyList<string> labels, int selectedIndex, DevUiInputState input)
    {
        if (labels == null || labels.Count == 0)
            return new DevUiTabResult(0, false);

        int clampedSelection = Math.Clamp(selectedIndex, 0, labels.Count - 1);
        int tabWidth = Math.Max(1, bounds.Width / labels.Count);
        int newSelection = clampedSelection;

        for (int i = 0; i < labels.Count; i++)
        {
            Rectangle tabBounds = new(bounds.X + i * tabWidth, bounds.Y, i == labels.Count - 1 ? bounds.Right - (bounds.X + i * tabWidth) : tabWidth, bounds.Height);
            bool hovered = tabBounds.Contains(input.Pointer);
            bool selected = i == clampedSelection;
            Color background = selected ? new Color(42, 68, 42, 245) : hovered ? new Color(24, 58, 36, 245) : new Color(8, 10, 14, 245);
            _ui.Fill(spriteBatch, tabBounds, background);
            _ui.Stroke(spriteBatch, tabBounds, selected ? Color.LightGreen : Color.DarkSlateGray, 1);
            _ui.Label(spriteBatch, labels[i], new Vector2(tabBounds.X + 8, tabBounds.Y + 8), selected ? Color.LightGreen : Color.White, 2);

            if (hovered && input.LeftPressed)
                newSelection = i;
        }

        return new DevUiTabResult(newSelection, newSelection != clampedSelection);
    }
}
