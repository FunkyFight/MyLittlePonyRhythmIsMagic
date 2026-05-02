using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace MLP_RiM.Elements.DevUI;

public sealed class DevUiDropdown
{
    private const int MaxVisibleItems = 4;

    private readonly DevUiRenderer _ui;
    private bool _isOpen;
    private int _scrollOffset;

    public DevUiDropdown(DevUiRenderer ui)
    {
        _ui = ui;
    }

    public bool IsOpen => _isOpen;

    public int SelectedIndex { get; private set; }

    public bool Update(Rectangle bounds, IReadOnlyList<string> options, int selectedIndex, MouseState mouse, bool leftClicked, int wheelDelta, bool isOpenRow)
    {
        SelectedIndex = Math.Clamp(selectedIndex, 0, Math.Max(0, options.Count - 1));
        _isOpen = isOpenRow;

        if (options.Count == 0)
        {
            _scrollOffset = 0;
            return false;
        }

        int visibleItems = GetVisibleItemCount(options);
        _scrollOffset = Math.Clamp(_scrollOffset, 0, Math.Max(0, options.Count - visibleItems));

        if (_isOpen && MouseOverList(bounds, visibleItems, mouse.Position))
            ApplyScroll(options.Count, visibleItems, wheelDelta);

        if (!leftClicked)
            return false;

        Point mousePoint = mouse.Position;
        if (bounds.Contains(mousePoint))
        {
            _isOpen = !_isOpen;
            if (_isOpen)
                EnsureSelectedVisible(visibleItems);
            return false;
        }

        if (!_isOpen)
            return false;

        for (int visibleIndex = 0; visibleIndex < visibleItems; visibleIndex++)
        {
            int optionIndex = _scrollOffset + visibleIndex;
            Rectangle itemBounds = new(bounds.X, bounds.Bottom + visibleIndex * bounds.Height, bounds.Width, bounds.Height);
            if (!itemBounds.Contains(mousePoint))
                continue;

            SelectedIndex = optionIndex;
            _isOpen = false;
            return true;
        }

        _isOpen = false;
        return false;
    }

    public void Draw(SpriteBatch spriteBatch, Rectangle bounds, IReadOnlyList<string> options, int selectedIndex, bool isOpen)
    {
        if (options.Count == 0)
            return;

        int clampedIndex = Math.Clamp(selectedIndex, 0, options.Count - 1);
        DrawItem(spriteBatch, bounds, options[clampedIndex], Color.Black * 0.85f, Color.LightGreen);
        _ui.Label(spriteBatch, isOpen ? "^" : "v", new Vector2(bounds.Right - 18, bounds.Y + 6), Color.LightGreen, 2);

        if (!isOpen)
            return;

        int visibleItems = GetVisibleItemCount(options);
        for (int visibleIndex = 0; visibleIndex < visibleItems; visibleIndex++)
        {
            int optionIndex = _scrollOffset + visibleIndex;
            Rectangle itemBounds = new(bounds.X, bounds.Bottom + visibleIndex * bounds.Height, bounds.Width, bounds.Height);
            Color background = optionIndex == clampedIndex ? new Color(42, 68, 42, 245) : new Color(8, 10, 14, 245);
            DrawItem(spriteBatch, itemBounds, options[optionIndex], background, Color.White);
        }

        if (options.Count > visibleItems)
            DrawScrollIndicator(spriteBatch, bounds, options.Count, visibleItems);
    }

    private void DrawItem(SpriteBatch spriteBatch, Rectangle bounds, string text, Color background, Color textColor)
    {
        _ui.Fill(spriteBatch, bounds, background);
        _ui.Stroke(spriteBatch, bounds, Color.LightGreen, 1);
        _ui.Label(spriteBatch, text, new Vector2(bounds.X + 8, bounds.Y + 6), textColor, 2);
    }

    private int GetVisibleItemCount(IReadOnlyList<string> options)
    {
        return Math.Min(MaxVisibleItems, options.Count);
    }

    private bool MouseOverList(Rectangle bounds, int visibleItems, Point mousePosition)
    {
        Rectangle listBounds = new(bounds.X, bounds.Bottom, bounds.Width, bounds.Height * visibleItems);
        return listBounds.Contains(mousePosition);
    }

    private void ApplyScroll(int optionCount, int visibleItems, int wheelDelta)
    {
        if (wheelDelta == 0)
            return;

        _scrollOffset -= Math.Sign(wheelDelta);
        _scrollOffset = Math.Clamp(_scrollOffset, 0, Math.Max(0, optionCount - visibleItems));
    }

    private void EnsureSelectedVisible(int visibleItems)
    {
        if (SelectedIndex < _scrollOffset)
            _scrollOffset = SelectedIndex;
        else if (SelectedIndex >= _scrollOffset + visibleItems)
            _scrollOffset = SelectedIndex - visibleItems + 1;
    }

    private void DrawScrollIndicator(SpriteBatch spriteBatch, Rectangle bounds, int optionCount, int visibleItems)
    {
        Rectangle track = new(bounds.Right - 6, bounds.Bottom, 4, bounds.Height * visibleItems);
        int thumbHeight = Math.Max(8, track.Height * visibleItems / optionCount);
        int maxOffset = optionCount - visibleItems;
        int thumbY = maxOffset == 0 ? track.Y : track.Y + (track.Height - thumbHeight) * _scrollOffset / maxOffset;
        _ui.Fill(spriteBatch, track, Color.DarkSlateGray);
        _ui.Fill(spriteBatch, new Rectangle(track.X, thumbY, track.Width, thumbHeight), Color.LightGreen);
    }
}
