using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace MLP_RiM.Elements.DevUI;

public sealed class DevUiFloatingWindow
{
    private readonly DevUiRenderer _ui;
    private readonly DevUiDropdown _dropdown;
    private string _openDropdownKey;
    private int _scrollOffset;
    private MouseState _previousMouse;
    private MouseState _mouse;

    public DevUiFloatingWindow(DevUiRenderer ui)
    {
        _ui = ui;
        _dropdown = new DevUiDropdown(ui);
    }

    public bool IsOpen { get; private set; }

    public void Open()
    {
        IsOpen = true;
    }

    public void Close()
    {
        IsOpen = false;
        _scrollOffset = 0;
    }

    public bool Update(Rectangle bounds, IReadOnlyList<DevUiWindowRow> rows)
    {
        _previousMouse = _mouse;
        _mouse = Mouse.GetState();

        if (!IsOpen)
            return false;

        ApplyScroll(bounds, rows);

        bool leftClicked = LeftClicked();
        int wheelDelta = _mouse.ScrollWheelValue - _previousMouse.ScrollWheelValue;
        bool mouseOverOpenDropdown = MouseOverOpenDropdown(bounds, rows);

        if (UpdateDropdownRows(bounds, rows, leftClicked, wheelDelta))
            return true;

        if (leftClicked && !bounds.Contains(_mouse.Position) && !mouseOverOpenDropdown)
        {
            Close();
            return false;
        }

        if (!leftClicked || mouseOverOpenDropdown)
            return false;

        int y = bounds.Y + 34 - _scrollOffset;
        foreach (DevUiWindowRow row in rows)
        {
            Rectangle rowBounds = new(bounds.X + 12, y, bounds.Width - 24, GetRowHeight(row));
            if ((row.Kind == DevUiWindowRowKind.Checkbox || row.Kind == DevUiWindowRowKind.Button) && GetContentBounds(bounds).Intersects(rowBounds) && rowBounds.Contains(_mouse.Position))
            {
                row.Toggle?.Invoke();
                return true;
            }

            y += rowBounds.Height;
        }

        return false;
    }

    public void Draw(SpriteBatch spriteBatch, Rectangle bounds, string title, IReadOnlyList<DevUiWindowRow> rows)
    {
        if (!IsOpen)
            return;

        _ui.Fill(spriteBatch, bounds, new Color(4, 6, 10, 235));
        _ui.Stroke(spriteBatch, bounds, Color.LightGreen, 2);
        _ui.Fill(spriteBatch, new Rectangle(bounds.X, bounds.Y, bounds.Width, 26), new Color(18, 36, 24, 245));
        _ui.Label(spriteBatch, title, new Vector2(bounds.X + 10, bounds.Y + 8), Color.LightGreen, 2);

        Rectangle contentBounds = GetContentBounds(bounds);
        int y = bounds.Y + 34 - _scrollOffset;
        foreach (DevUiWindowRow row in rows)
        {
            Rectangle rowBounds = new(bounds.X + 12, y, bounds.Width - 24, GetRowHeight(row));
            if (contentBounds.Intersects(rowBounds))
                DrawRow(spriteBatch, rowBounds, row);

            y += rowBounds.Height;
        }

        DrawScrollIndicator(spriteBatch, bounds, rows);
    }

    private bool UpdateDropdownRows(Rectangle bounds, IReadOnlyList<DevUiWindowRow> rows, bool leftClicked, int wheelDelta)
    {
        Rectangle contentBounds = GetContentBounds(bounds);
        int y = bounds.Y + 34 - _scrollOffset;
        foreach (DevUiWindowRow row in rows)
        {
            Rectangle rowBounds = new(bounds.X + 12, y, bounds.Width - 24, GetRowHeight(row));
            if (row.Kind == DevUiWindowRowKind.Dropdown && row.Options != null && contentBounds.Intersects(rowBounds))
            {
                Rectangle dropdownBounds = GetDropdownBounds(rowBounds);
                bool isOpenRow = _openDropdownKey == row.Key;
                if (_dropdown.Update(dropdownBounds, row.Options, row.SelectedIndex, _mouse, leftClicked, wheelDelta, isOpenRow))
                {
                    row.Select?.Invoke(_dropdown.SelectedIndex);
                    _openDropdownKey = null;
                    return true;
                }

                if (leftClicked && dropdownBounds.Contains(_mouse.Position))
                    _openDropdownKey = _dropdown.IsOpen ? row.Key : null;
            }

            y += rowBounds.Height;
        }

        return false;
    }

    private bool MouseOverOpenDropdown(Rectangle bounds, IReadOnlyList<DevUiWindowRow> rows)
    {
        if (!_dropdown.IsOpen)
            return false;

        int y = bounds.Y + 34 - _scrollOffset;
        foreach (DevUiWindowRow row in rows)
        {
            Rectangle rowBounds = new(bounds.X + 12, y, bounds.Width - 24, GetRowHeight(row));
            if (row.Kind == DevUiWindowRowKind.Dropdown && row.Options != null)
            {
                Rectangle dropdownBounds = GetDropdownBounds(rowBounds);
                Rectangle listBounds = new(dropdownBounds.X, dropdownBounds.Y, dropdownBounds.Width, dropdownBounds.Height * (Math.Min(4, row.Options.Count) + 1));
                if (_openDropdownKey == row.Key)
                    return listBounds.Contains(_mouse.Position);
            }

            y += rowBounds.Height;
        }

        return false;
    }

    private void DrawRow(SpriteBatch spriteBatch, Rectangle bounds, DevUiWindowRow row)
    {
        switch (row.Kind)
        {
            case DevUiWindowRowKind.Category:
                _ui.Label(spriteBatch, row.Text, new Vector2(bounds.X, bounds.Y + 7), Color.LightBlue, 2);
                _ui.Line(spriteBatch, new Vector2(bounds.X, bounds.Bottom - 3), new Vector2(bounds.Right, bounds.Bottom - 3), Color.DarkSlateGray, 1);
                break;

            case DevUiWindowRowKind.Title:
                _ui.Label(spriteBatch, row.Text, new Vector2(bounds.X, bounds.Y + 5), Color.White, 2);
                break;

            case DevUiWindowRowKind.Checkbox:
                Rectangle checkBounds = new(bounds.X, bounds.Y + 6, 14, 14);
                _ui.Stroke(spriteBatch, checkBounds, Color.LightGreen, 2);
                if (row.IsChecked)
                    _ui.Fill(spriteBatch, new Rectangle(checkBounds.X + 4, checkBounds.Y + 4, 6, 6), Color.LightGreen);

                _ui.Label(spriteBatch, row.Text, new Vector2(bounds.X + 24, bounds.Y + 7), Color.White, 2);
                break;

            case DevUiWindowRowKind.Dropdown:
                _ui.Label(spriteBatch, row.Text, new Vector2(bounds.X, bounds.Y + 7), Color.White, 2);
                _dropdown.Draw(spriteBatch, GetDropdownBounds(bounds), row.Options, row.SelectedIndex, _openDropdownKey == row.Key);
                break;

            case DevUiWindowRowKind.Button:
                _ui.Fill(spriteBatch, bounds, new Color(18, 36, 24, 245));
                _ui.Stroke(spriteBatch, bounds, Color.LightGreen, 2);
                _ui.Label(spriteBatch, row.Text, new Vector2(bounds.X + 10, bounds.Y + 8), Color.LightGreen, 2);
                break;
        }
    }

    private static int GetRowHeight(DevUiWindowRow row)
    {
        return row.Kind == DevUiWindowRowKind.Category ? 26 : 28;
    }

    private void ApplyScroll(Rectangle bounds, IReadOnlyList<DevUiWindowRow> rows)
    {
        int maxScroll = GetMaxScroll(bounds, rows);
        _scrollOffset = Math.Clamp(_scrollOffset, 0, maxScroll);

        if (!bounds.Contains(_mouse.Position))
            return;

        int wheelDelta = _mouse.ScrollWheelValue - _previousMouse.ScrollWheelValue;
        if (wheelDelta == 0)
            return;

        _scrollOffset -= Math.Sign(wheelDelta) * 24;
        _scrollOffset = Math.Clamp(_scrollOffset, 0, maxScroll);
    }

    private void DrawScrollIndicator(SpriteBatch spriteBatch, Rectangle bounds, IReadOnlyList<DevUiWindowRow> rows)
    {
        int contentHeight = GetContentHeight(rows);
        Rectangle contentBounds = GetContentBounds(bounds);
        if (contentHeight <= contentBounds.Height)
            return;

        Rectangle track = new(bounds.Right - 8, contentBounds.Y, 4, contentBounds.Height);
        int thumbHeight = Math.Max(12, track.Height * contentBounds.Height / contentHeight);
        int maxScroll = GetMaxScroll(bounds, rows);
        int thumbY = maxScroll == 0 ? track.Y : track.Y + (track.Height - thumbHeight) * _scrollOffset / maxScroll;
        _ui.Fill(spriteBatch, track, Color.DarkSlateGray);
        _ui.Fill(spriteBatch, new Rectangle(track.X, thumbY, track.Width, thumbHeight), Color.LightGreen);
    }

    private static Rectangle GetContentBounds(Rectangle bounds)
    {
        return new Rectangle(bounds.X, bounds.Y + 30, bounds.Width, bounds.Height - 34);
    }

    private static int GetMaxScroll(Rectangle bounds, IReadOnlyList<DevUiWindowRow> rows)
    {
        return Math.Max(0, GetContentHeight(rows) - GetContentBounds(bounds).Height);
    }

    private static int GetContentHeight(IReadOnlyList<DevUiWindowRow> rows)
    {
        int height = 4;
        foreach (DevUiWindowRow row in rows)
            height += GetRowHeight(row);

        return height;
    }

    private static Rectangle GetDropdownBounds(Rectangle rowBounds)
    {
        return new Rectangle(rowBounds.Right - 150, rowBounds.Y + 2, 150, 24);
    }

    private bool LeftClicked()
    {
        return _mouse.LeftButton == ButtonState.Released && _previousMouse.LeftButton == ButtonState.Pressed;
    }
}

public readonly struct DevUiWindowRow
{
    public DevUiWindowRowKind Kind { get; }
    public string Text { get; }
    public bool IsChecked { get; }
    public Action Toggle { get; }
    public IReadOnlyList<string> Options { get; }
    public int SelectedIndex { get; }
    public Action<int> Select { get; }
    public string Key { get; }

    private DevUiWindowRow(DevUiWindowRowKind kind, string text, bool isChecked = false, Action toggle = null, IReadOnlyList<string> options = null, int selectedIndex = 0, Action<int> select = null, string key = null)
    {
        Kind = kind;
        Text = text;
        IsChecked = isChecked;
        Toggle = toggle;
        Options = options;
        SelectedIndex = selectedIndex;
        Select = select;
        Key = key ?? text;
    }

    public static DevUiWindowRow Category(string text)
    {
        return new DevUiWindowRow(DevUiWindowRowKind.Category, text);
    }

    public static DevUiWindowRow Title(string text)
    {
        return new DevUiWindowRow(DevUiWindowRowKind.Title, text);
    }

    public static DevUiWindowRow Checkbox(string text, bool isChecked, Action toggle)
    {
        return new DevUiWindowRow(DevUiWindowRowKind.Checkbox, text, isChecked, toggle);
    }

    public static DevUiWindowRow Dropdown(string text, IReadOnlyList<string> options, int selectedIndex, Action<int> select)
    {
        return Dropdown(text, text, options, selectedIndex, select);
    }

    public static DevUiWindowRow Dropdown(string key, string text, IReadOnlyList<string> options, int selectedIndex, Action<int> select)
    {
        return new DevUiWindowRow(DevUiWindowRowKind.Dropdown, text, options: options, selectedIndex: selectedIndex, select: select, key: key);
    }

    public static DevUiWindowRow Button(string text, Action click)
    {
        return new DevUiWindowRow(DevUiWindowRowKind.Button, text, toggle: click);
    }

}

public enum DevUiWindowRowKind
{
    Category,
    Title,
    Checkbox,
    Dropdown,
    Button
}
