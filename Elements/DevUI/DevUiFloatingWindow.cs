using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace MLP_RiM.Elements.DevUI;

public sealed class DevUiFloatingWindow
{
    private readonly DevUiRenderer _ui;
    private readonly DevUiDropdown _dropdown;
    private string _openDropdownKey;
    private string _editingFloatKey;
    private string _floatEditBuffer = "";
    private int _scrollOffset;
    private MouseState _previousMouse;
    private MouseState _mouse;
    private KeyboardState _previousKeyboard;
    private KeyboardState _keyboard;

    public DevUiFloatingWindow(DevUiRenderer ui)
    {
        _ui = ui;
        _dropdown = new DevUiDropdown(ui);
    }

    public bool IsOpen { get; private set; }
    public bool IsEditingTextInput => _editingFloatKey != null;

    public void Open()
    {
        IsOpen = true;
        ResetInputState();
    }

    public void Close()
    {
        IsOpen = false;
        _scrollOffset = 0;
        _openDropdownKey = null;
        _editingFloatKey = null;
        _floatEditBuffer = "";
    }

    public bool Update(Rectangle bounds, IReadOnlyList<DevUiWindowRow> rows)
    {
        _previousMouse = _mouse;
        _mouse = Mouse.GetState();
        _previousKeyboard = _keyboard;
        _keyboard = Keyboard.GetState();

        if (!IsOpen)
            return false;

        ApplyScroll(bounds, rows);
        EnsureOpenDropdownRowExists(bounds, rows);

        bool leftClicked = LeftPressed();
        int wheelDelta = _mouse.ScrollWheelValue - _previousMouse.ScrollWheelValue;
        bool hadOpenDropdown = _openDropdownKey != null;
        bool mouseOverOpenDropdownBeforeUpdate = MouseOverOpenDropdown(bounds, rows);

        if (_editingFloatKey != null)
        {
            if (UpdateFloatEdit(rows))
                return true;

            if (leftClicked && MouseOverEditingFloatRow(bounds, rows))
                return false;

            if (leftClicked && !TryCommitFloatEdit(rows))
                return false;
        }

        if (UpdateDropdownRows(bounds, rows, leftClicked, wheelDelta))
            return true;

        bool mouseOverOpenDropdown = MouseOverOpenDropdown(bounds, rows);

        if (leftClicked && hadOpenDropdown && !mouseOverOpenDropdownBeforeUpdate)
        {
            if (!bounds.Contains(_mouse.Position))
                Close();
            else
                _openDropdownKey = null;

            return false;
        }

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
            if (!IsClickableRow(row) || !GetContentBounds(bounds).Intersects(rowBounds) || !rowBounds.Contains(_mouse.Position))
            {
                y += rowBounds.Height;
                continue;
            }

            if (row.Kind == DevUiWindowRowKind.FloatInput)
            {
                BeginFloatEdit(row);
                return false;
            }

            if (row.Kind == DevUiWindowRowKind.Checkbox || row.Kind == DevUiWindowRowKind.Button)
            {
                row.Toggle?.Invoke();
                return true;
            }

            if (row.Kind == DevUiWindowRowKind.Slider)
            {
                UpdateSlider(row, GetDropdownBounds(rowBounds));
                return true;
            }

            if (row.Kind == DevUiWindowRowKind.Stepper)
            {
                UpdateStepper(row, GetDropdownBounds(rowBounds));
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
        DrawOpenDropdownList(spriteBatch, bounds, rows);
    }

    private bool UpdateDropdownRows(Rectangle bounds, IReadOnlyList<DevUiWindowRow> rows, bool leftClicked, int wheelDelta)
    {
        Rectangle contentBounds = GetContentBounds(bounds);
        int y = bounds.Y + 34 - _scrollOffset;
        foreach (DevUiWindowRow row in rows)
        {
            Rectangle rowBounds = new(bounds.X + 12, y, bounds.Width - 24, GetRowHeight(row));
            if (row.Kind != DevUiWindowRowKind.Dropdown || row.Options == null)
            {
                y += rowBounds.Height;
                continue;
            }

            bool isOpenRow = _openDropdownKey == row.Key;
            bool isVisibleRow = contentBounds.Intersects(rowBounds);
            bool clickedDropdown = isVisibleRow && leftClicked && GetDropdownBounds(rowBounds).Contains(_mouse.Position);

            if (!isOpenRow && !clickedDropdown)
            {
                y += rowBounds.Height;
                continue;
            }

            Rectangle dropdownBounds = GetDropdownBounds(rowBounds);
            if (_dropdown.Update(dropdownBounds, row.Options, row.SelectedIndex, _mouse, leftClicked, wheelDelta, isOpenRow && isVisibleRow))
            {
                row.Select?.Invoke(_dropdown.SelectedIndex);
                _openDropdownKey = null;
                return true;
            }

            if (isVisibleRow)
                _openDropdownKey = _dropdown.IsOpen ? row.Key : null;
            else
                _openDropdownKey = null;

            y += rowBounds.Height;
        }

        return false;
    }

    private bool MouseOverOpenDropdown(Rectangle bounds, IReadOnlyList<DevUiWindowRow> rows)
    {
        if (_openDropdownKey == null)
            return false;

        if (!TryGetOpenDropdownRow(bounds, rows, out Rectangle rowBounds, out DevUiWindowRow openDropdownRow))
            return false;

        Rectangle dropdownBounds = GetDropdownBounds(rowBounds);
        int visibleItems = Math.Min(4, openDropdownRow.Options.Count);
        Rectangle listBounds = new(dropdownBounds.X, dropdownBounds.Y, dropdownBounds.Width, dropdownBounds.Height * (visibleItems + 1));
        return listBounds.Contains(_mouse.Position);
    }

    private bool TryGetOpenDropdownRow(Rectangle bounds, IReadOnlyList<DevUiWindowRow> rows, out Rectangle rowBounds, out DevUiWindowRow row)
    {
        row = default;
        rowBounds = default;

        if (_openDropdownKey == null)
            return false;

        Rectangle contentBounds = GetContentBounds(bounds);
        int y = bounds.Y + 34 - _scrollOffset;

        foreach (DevUiWindowRow candidate in rows)
        {
            Rectangle candidateBounds = new(bounds.X + 12, y, bounds.Width - 24, GetRowHeight(candidate));
            if (candidate.Key == _openDropdownKey && candidate.Kind == DevUiWindowRowKind.Dropdown && candidate.Options != null)
            {
                if (contentBounds.Intersects(candidateBounds))
                {
                    row = candidate;
                    rowBounds = candidateBounds;
                    return true;
                }

                return false;
            }

            y += candidateBounds.Height;
        }

        return false;
    }

    private void EnsureOpenDropdownRowExists(Rectangle bounds, IReadOnlyList<DevUiWindowRow> rows)
    {
        if (_openDropdownKey == null)
            return;

        if (!TryGetOpenDropdownRow(bounds, rows, out _, out _))
            _openDropdownKey = null;
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

            case DevUiWindowRowKind.Value:
                _ui.Label(spriteBatch, row.Text, new Vector2(bounds.X, bounds.Y + 7), Color.White, 2);
                _ui.Label(spriteBatch, row.ValueText, new Vector2(GetDropdownBounds(bounds).X + 8, bounds.Y + 7), Color.LightGreen, 2);
                break;

            case DevUiWindowRowKind.Separator:
                int separatorY = bounds.Y + bounds.Height / 2;
                _ui.Line(spriteBatch, new Vector2(bounds.X, separatorY), new Vector2(bounds.Right, separatorY), Color.DarkSlateGray, 1);
                if (!string.IsNullOrWhiteSpace(row.Text))
                    _ui.Label(spriteBatch, row.Text, new Vector2(bounds.X + 8, bounds.Y + 7), Color.DarkSeaGreen, 1);
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
                _dropdown.Draw(spriteBatch, GetDropdownBounds(bounds), row.Options, row.SelectedIndex, _openDropdownKey == row.Key, drawList: false);
                break;

            case DevUiWindowRowKind.FloatInput:
                _ui.Label(spriteBatch, row.Text, new Vector2(bounds.X, bounds.Y + 7), Color.White, 2);
                DrawFloatInput(spriteBatch, GetDropdownBounds(bounds), row);
                break;

            case DevUiWindowRowKind.Button:
                _ui.Fill(spriteBatch, bounds, new Color(18, 36, 24, 245));
                _ui.Stroke(spriteBatch, bounds, Color.LightGreen, 2);
                _ui.Label(spriteBatch, row.Text, new Vector2(bounds.X + 10, bounds.Y + 8), Color.LightGreen, 2);
                break;

            case DevUiWindowRowKind.Slider:
                _ui.Label(spriteBatch, row.Text, new Vector2(bounds.X, bounds.Y + 7), Color.White, 2);
                DrawSlider(spriteBatch, GetDropdownBounds(bounds), row);
                break;

            case DevUiWindowRowKind.Stepper:
                _ui.Label(spriteBatch, row.Text, new Vector2(bounds.X, bounds.Y + 7), Color.White, 2);
                DrawStepper(spriteBatch, GetDropdownBounds(bounds), row);
                break;
        }
    }

    private void DrawOpenDropdownList(SpriteBatch spriteBatch, Rectangle bounds, IReadOnlyList<DevUiWindowRow> rows)
    {
        if (_openDropdownKey == null)
            return;

        if (!TryGetOpenDropdownRow(bounds, rows, out _, out _))
            return;

        Rectangle contentBounds = GetContentBounds(bounds);
        int y = bounds.Y + 34 - _scrollOffset;
        foreach (DevUiWindowRow row in rows)
        {
            Rectangle rowBounds = new(bounds.X + 12, y, bounds.Width - 24, GetRowHeight(row));
            if (row.Kind == DevUiWindowRowKind.Dropdown && row.Options != null && _openDropdownKey == row.Key && contentBounds.Intersects(rowBounds))
            {
                _dropdown.DrawList(spriteBatch, GetDropdownBounds(rowBounds), row.Options, row.SelectedIndex);
                return;
            }

            y += rowBounds.Height;
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

    private void ResetInputState()
    {
        _mouse = Mouse.GetState();
        _previousMouse = _mouse;
        _keyboard = Keyboard.GetState();
        _previousKeyboard = _keyboard;
    }

    private static bool IsClickableRow(DevUiWindowRow row)
    {
        return row.Kind == DevUiWindowRowKind.Button
            || row.Kind == DevUiWindowRowKind.Checkbox
            || row.Kind == DevUiWindowRowKind.FloatInput
            || row.Kind == DevUiWindowRowKind.Slider
            || row.Kind == DevUiWindowRowKind.Stepper;
    }

    private void UpdateSlider(DevUiWindowRow row, Rectangle bounds)
    {
        if (!bounds.Contains(_mouse.Position))
            return;

        double t = Math.Clamp((double)(_mouse.X - bounds.X) / Math.Max(1, bounds.Width), 0.0, 1.0);
        row.SetFloat?.Invoke(row.MinValue + (row.MaxValue - row.MinValue) * t);
    }

    private void UpdateStepper(DevUiWindowRow row, Rectangle bounds)
    {
        int buttonWidth = 24;
        Rectangle minusBounds = new(bounds.X, bounds.Y, buttonWidth, bounds.Height);
        Rectangle plusBounds = new(bounds.Right - buttonWidth, bounds.Y, buttonWidth, bounds.Height);

        if (minusBounds.Contains(_mouse.Position))
            row.SetFloat?.Invoke(row.FloatValue - row.StepValue);
        else if (plusBounds.Contains(_mouse.Position))
            row.SetFloat?.Invoke(row.FloatValue + row.StepValue);
    }

    private void BeginFloatEdit(DevUiWindowRow row)
    {
        _openDropdownKey = null;
        _editingFloatKey = row.Key;
        _floatEditBuffer = row.FloatValue.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private bool UpdateFloatEdit(IReadOnlyList<DevUiWindowRow> rows)
    {
        if (Pressed(Keys.Enter))
            return TryCommitFloatEdit(rows);

        if (Pressed(Keys.Escape))
        {
            _editingFloatKey = null;
            return false;
        }

        if (Pressed(Keys.Back) && _floatEditBuffer.Length > 0)
            _floatEditBuffer = _floatEditBuffer[..^1];

        foreach (Keys key in _keyboard.GetPressedKeys())
        {
            if (_previousKeyboard.IsKeyDown(key))
                continue;

            if (TryKeyToChar(key, out char c))
                _floatEditBuffer += c;
        }

        return false;
    }

    private bool TryCommitFloatEdit(IReadOnlyList<DevUiWindowRow> rows)
    {
        if (!TryGetEditingFloatRow(rows, out DevUiWindowRow row))
        {
            _editingFloatKey = null;
            return true;
        }

        if (!double.TryParse(_floatEditBuffer, NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
            || double.IsNaN(value)
            || double.IsInfinity(value))
        {
            return false;
        }

        row.SetFloat?.Invoke(value);
        _editingFloatKey = null;
        return true;
    }

    private bool TryGetEditingFloatRow(IReadOnlyList<DevUiWindowRow> rows, out DevUiWindowRow row)
    {
        foreach (DevUiWindowRow candidate in rows)
        {
            if (candidate.Kind == DevUiWindowRowKind.FloatInput && candidate.Key == _editingFloatKey)
            {
                row = candidate;
                return true;
            }
        }

        row = default;
        return false;
    }

    private bool MouseOverEditingFloatRow(Rectangle bounds, IReadOnlyList<DevUiWindowRow> rows)
    {
        int y = bounds.Y + 34 - _scrollOffset;
        foreach (DevUiWindowRow row in rows)
        {
            Rectangle rowBounds = new(bounds.X + 12, y, bounds.Width - 24, GetRowHeight(row));
            if (row.Kind == DevUiWindowRowKind.FloatInput
                && row.Key == _editingFloatKey
                && GetContentBounds(bounds).Intersects(rowBounds)
                && rowBounds.Contains(_mouse.Position))
            {
                return true;
            }

            y += rowBounds.Height;
        }

        return false;
    }

    private void DrawFloatInput(SpriteBatch spriteBatch, Rectangle bounds, DevUiWindowRow row)
    {
        bool isEditing = _editingFloatKey == row.Key;
        string text = isEditing ? _floatEditBuffer + "|" : row.FloatValue.ToString("0.###", CultureInfo.InvariantCulture);
        Color border = isEditing ? Color.Yellow : Color.LightGreen;
        Color textColor = isEditing ? Color.Yellow : Color.LightGreen;

        _ui.Fill(spriteBatch, bounds, Color.Black * 0.85f);
        _ui.Stroke(spriteBatch, bounds, border, 1);
        _ui.Label(spriteBatch, text, new Vector2(bounds.X + 8, bounds.Y + 6), textColor, 2);
    }

    private void DrawSlider(SpriteBatch spriteBatch, Rectangle bounds, DevUiWindowRow row)
    {
        double range = row.MaxValue - row.MinValue;
        double t = Math.Abs(range) <= double.Epsilon ? 0.0 : Math.Clamp((row.FloatValue - row.MinValue) / range, 0.0, 1.0);
        int fillWidth = (int)Math.Round(bounds.Width * t);
        _ui.Fill(spriteBatch, bounds, Color.Black * 0.85f);
        _ui.Fill(spriteBatch, new Rectangle(bounds.X, bounds.Y, fillWidth, bounds.Height), Color.LightGreen * 0.5f);
        _ui.Stroke(spriteBatch, bounds, Color.LightGreen, 1);
        _ui.Label(spriteBatch, row.FloatValue.ToString("0.###", CultureInfo.InvariantCulture), new Vector2(bounds.X + 8, bounds.Y + 6), Color.LightGreen, 2);
    }

    private void DrawStepper(SpriteBatch spriteBatch, Rectangle bounds, DevUiWindowRow row)
    {
        int buttonWidth = 24;
        Rectangle minusBounds = new(bounds.X, bounds.Y, buttonWidth, bounds.Height);
        Rectangle valueBounds = new(bounds.X + buttonWidth, bounds.Y, bounds.Width - buttonWidth * 2, bounds.Height);
        Rectangle plusBounds = new(bounds.Right - buttonWidth, bounds.Y, buttonWidth, bounds.Height);

        _ui.Fill(spriteBatch, bounds, Color.Black * 0.85f);
        _ui.Stroke(spriteBatch, bounds, Color.LightGreen, 1);
        _ui.Fill(spriteBatch, minusBounds, new Color(18, 36, 24, 245));
        _ui.Fill(spriteBatch, plusBounds, new Color(18, 36, 24, 245));
        _ui.Label(spriteBatch, "-", new Vector2(minusBounds.X + 9, minusBounds.Y + 6), Color.LightGreen, 2);
        _ui.Label(spriteBatch, row.FloatValue.ToString("0.###", CultureInfo.InvariantCulture), new Vector2(valueBounds.X + 8, valueBounds.Y + 6), Color.LightGreen, 2);
        _ui.Label(spriteBatch, "+", new Vector2(plusBounds.X + 8, plusBounds.Y + 6), Color.LightGreen, 2);
    }

    private bool Pressed(Keys key)
    {
        return _keyboard.IsKeyDown(key) && !_previousKeyboard.IsKeyDown(key);
    }

    private bool TryKeyToChar(Keys key, out char c)
    {
        c = '\0';

        if (key >= Keys.D0 && key <= Keys.D9)
        {
            c = (char)('0' + (key - Keys.D0));
            return true;
        }

        if (key >= Keys.NumPad0 && key <= Keys.NumPad9)
        {
            c = (char)('0' + (key - Keys.NumPad0));
            return true;
        }

        c = key switch
        {
            Keys.OemPeriod or Keys.Decimal => '.',
            Keys.OemComma => '.',
            Keys.OemMinus or Keys.Subtract => '-',
            _ => '\0'
        };

        return c != '\0';
    }

    private bool LeftPressed()
    {
        return _mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton == ButtonState.Released;
    }

}

public readonly struct DevUiWindowRow
{
    public DevUiWindowRowKind Kind { get; }
    public string Text { get; }
    public string ValueText { get; }
    public bool IsChecked { get; }
    public Action Toggle { get; }
    public IReadOnlyList<string> Options { get; }
    public int SelectedIndex { get; }
    public Action<int> Select { get; }
    public string Key { get; }
    public double FloatValue { get; }
    public double MinValue { get; }
    public double MaxValue { get; }
    public double StepValue { get; }
    public Action<double> SetFloat { get; }

    private DevUiWindowRow(DevUiWindowRowKind kind, string text, bool isChecked = false, Action toggle = null, IReadOnlyList<string> options = null, int selectedIndex = 0, Action<int> select = null, string key = null, double floatValue = 0, Action<double> setFloat = null, string valueText = null, double minValue = 0, double maxValue = 1, double stepValue = 1)
    {
        Kind = kind;
        Text = text;
        ValueText = valueText ?? string.Empty;
        IsChecked = isChecked;
        Toggle = toggle;
        Options = options;
        SelectedIndex = selectedIndex;
        Select = select;
        Key = key ?? text;
        FloatValue = floatValue;
        MinValue = minValue;
        MaxValue = maxValue;
        StepValue = stepValue;
        SetFloat = setFloat;
    }

    public static DevUiWindowRow Category(string text)
    {
        return new DevUiWindowRow(DevUiWindowRowKind.Category, text);
    }

    public static DevUiWindowRow Title(string text)
    {
        return new DevUiWindowRow(DevUiWindowRowKind.Title, text);
    }

    public static DevUiWindowRow Value(string text, string value)
    {
        return new DevUiWindowRow(DevUiWindowRowKind.Value, text, valueText: value);
    }

    public static DevUiWindowRow Separator(string text = null)
    {
        return new DevUiWindowRow(DevUiWindowRowKind.Separator, text ?? string.Empty);
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

    public static DevUiWindowRow FloatInput(string key, string text, double value, Action<double> setFloat)
    {
        return new DevUiWindowRow(DevUiWindowRowKind.FloatInput, text, key: key, floatValue: value, setFloat: setFloat);
    }

    public static DevUiWindowRow Slider(string key, string text, double value, double min, double max, Action<double> setFloat)
    {
        return new DevUiWindowRow(DevUiWindowRowKind.Slider, text, key: key, floatValue: value, setFloat: setFloat, minValue: min, maxValue: max);
    }

    public static DevUiWindowRow Stepper(string key, string text, double value, double step, Action<double> setFloat)
    {
        return new DevUiWindowRow(DevUiWindowRowKind.Stepper, text, key: key, floatValue: value, setFloat: setFloat, stepValue: step);
    }

}

public enum DevUiWindowRowKind
{
    Category,
    Title,
    Checkbox,
    Dropdown,
    FloatInput,
    Button,
    Value,
    Separator,
    Slider,
    Stepper
}
