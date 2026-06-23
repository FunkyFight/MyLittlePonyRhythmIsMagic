using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MLP_RiM.Elements.DevUI;

namespace MLP_RiM.Elements.Editor;

internal sealed class BeatmapFolderExplorer
{
    private const int RowHeight = 34;
    private readonly DevUiRenderer _ui;
    private ExplorerMode _mode;
    private string _title = "BEATMAPS";
    private string _currentFolderPath = BeatmapPackagePaths.BeatmapsRoot;
    private string _selectedChartPath = string.Empty;
    private string _beatmapNameBuffer = string.Empty;
    private string _folderNameBuffer = string.Empty;
    private bool _beatmapNameFocused;
    private bool _folderNameFocused;
    private int _folderListScroll;
    private long _backspaceHoldStartMs;
    private long _backspaceLastRepeatMs;
    private Action<string> _createBeatmap;
    private Action<string> _selectBeatmap;
    private Action<string> _setStatus;
    private Action _cancel;

    private enum ExplorerMode
    {
        CreateBeatmap,
        SelectBeatmap
    }

    private readonly struct ExplorerEntry
    {
        public ExplorerEntry(string label, string path, string chartPath, bool isChart)
        {
            Label = label;
            Path = path;
            ChartPath = chartPath;
            IsChart = isChart;
        }

        public string Label { get; }
        public string Path { get; }
        public string ChartPath { get; }
        public bool IsChart { get; }
    }

    public BeatmapFolderExplorer(DevUiRenderer ui)
    {
        _ui = ui ?? throw new ArgumentNullException(nameof(ui));
    }

    public bool IsOpen { get; private set; }

    public void OpenCreateBeatmap(string title, Action<string> createBeatmap, Action cancel, Action<string> setStatus)
    {
        Directory.CreateDirectory(BeatmapPackagePaths.BeatmapsRoot);
        _mode = ExplorerMode.CreateBeatmap;
        _title = string.IsNullOrWhiteSpace(title) ? "NEW BEATMAP" : title;
        _createBeatmap = createBeatmap;
        _selectBeatmap = null;
        _setStatus = setStatus;
        _cancel = cancel;
        _currentFolderPath = BeatmapPackagePaths.BeatmapsRoot;
        _selectedChartPath = string.Empty;
        _beatmapNameBuffer = string.Empty;
        _folderNameBuffer = string.Empty;
        ResetInteractionState();
        IsOpen = true;
        SetStatus("Type a beatmap name, then create it as a package folder.");
    }

    public void OpenSelectBeatmap(string title, string selectedChartPath, Action<string> selectBeatmap, Action cancel, Action<string> setStatus)
    {
        Directory.CreateDirectory(BeatmapPackagePaths.BeatmapsRoot);
        _mode = ExplorerMode.SelectBeatmap;
        _title = string.IsNullOrWhiteSpace(title) ? "OPEN BEATMAP" : title;
        _createBeatmap = null;
        _selectBeatmap = selectBeatmap;
        _setStatus = setStatus;
        _cancel = cancel;
        _selectedChartPath = BeatmapPackagePaths.NormalizeRelativePath(selectedChartPath ?? string.Empty);
        _beatmapNameBuffer = string.Empty;
        _folderNameBuffer = string.Empty;
        _currentFolderPath = GetInitialSelectFolder(_selectedChartPath);
        ResetInteractionState();
        IsOpen = true;
        SetStatus("Select a beatmap package.");
    }

    public void Close()
    {
        IsOpen = false;
        _beatmapNameFocused = false;
        _folderNameFocused = false;
        ResetBackspaceRepeat();
    }

    public void Update(MouseState mouse, MouseState previousMouse, KeyboardState keyboard, KeyboardState previousKeyboard, Viewport viewport)
    {
        if (!IsOpen)
            return;

        if (Pressed(keyboard, previousKeyboard, Keys.Escape))
        {
            Cancel();
            return;
        }

        Rectangle modal = GetExplorerBounds(viewport);
        Rectangle sidebar = GetSidebarBounds(modal);
        Rectangle list = GetFolderListBounds(modal);
        Rectangle actions = GetActionsBounds(modal);
        IReadOnlyList<ExplorerEntry> entries = GetEntries();

        ApplyFolderListScroll(mouse, previousMouse, list, entries.Count);

        if (_mode == ExplorerMode.CreateBeatmap && (_beatmapNameFocused || _folderNameFocused))
        {
            if (Pressed(keyboard, previousKeyboard, Keys.Enter))
            {
                if (_beatmapNameFocused)
                    CreateBeatmapFromName();
                else if (!string.IsNullOrWhiteSpace(_folderNameBuffer))
                    CreateFolderFromName();
                return;
            }

            if (_beatmapNameFocused)
                UpdateTextInput(keyboard, previousKeyboard, ref _beatmapNameBuffer);
            else
                UpdateTextInput(keyboard, previousKeyboard, ref _folderNameBuffer);
        }
        else if (_mode == ExplorerMode.CreateBeatmap && Pressed(keyboard, previousKeyboard, Keys.Enter))
        {
            CreateBeatmapFromName();
            return;
        }

        if (!LeftPressed(mouse, previousMouse))
            return;

        if (GetCloseButtonBounds(modal).Contains(mouse.Position))
        {
            Cancel();
            return;
        }

        if (_mode == ExplorerMode.CreateBeatmap)
        {
            bool beatmapInputHit = GetBeatmapNameInputBounds(actions).Contains(mouse.Position);
            bool folderInputHit = GetFolderNameInputBounds(actions).Contains(mouse.Position);
            SetInputFocus(beatmapInputHit, folderInputHit);
        }

        if (GetRootButtonBounds(sidebar).Contains(mouse.Position))
        {
            NavigateToFolder(BeatmapPackagePaths.BeatmapsRoot);
            return;
        }

        Rectangle parentButton = GetParentButtonBounds(sidebar);
        if (!BeatmapPackagePaths.IsBeatmapsRoot(_currentFolderPath) && parentButton.Contains(mouse.Position))
        {
            NavigateToParentFolder();
            return;
        }

        Rectangle listContent = GetFolderListContentBounds(list);
        if (listContent.Contains(mouse.Position))
        {
            for (int i = 0; i < entries.Count; i++)
            {
                Rectangle row = GetEntryRowBounds(list, i);
                if (!listContent.Intersects(row) || !row.Contains(mouse.Position))
                    continue;

                ActivateEntry(entries[i]);
                return;
            }
        }

        if (_mode == ExplorerMode.CreateBeatmap && GetCreateFolderButtonBounds(actions).Contains(mouse.Position))
        {
            CreateFolderFromName();
            return;
        }

        if (_mode == ExplorerMode.CreateBeatmap && GetCreateBeatmapButtonBounds(actions).Contains(mouse.Position))
        {
            CreateBeatmapFromName();
            return;
        }

        if (GetCancelButtonBounds(actions).Contains(mouse.Position))
            Cancel();
    }

    public void Draw(SpriteBatch spriteBatch, Viewport viewport, Point mousePosition)
    {
        if (!IsOpen)
            return;

        Rectangle overlay = new(0, 0, viewport.Width, viewport.Height);
        Rectangle modal = GetExplorerBounds(viewport);
        _ui.Fill(spriteBatch, overlay, Color.Black * 0.66f);
        _ui.Fill(spriteBatch, modal, new Color(4, 6, 10, 245));
        _ui.Stroke(spriteBatch, modal, Color.LightGreen, 2);

        DrawHeader(spriteBatch, modal, mousePosition);
        DrawSidebar(spriteBatch, GetSidebarBounds(modal), mousePosition);
        DrawFolderList(spriteBatch, GetFolderListBounds(modal), mousePosition);
        DrawActions(spriteBatch, GetActionsBounds(modal), mousePosition);
    }

    private void ActivateEntry(ExplorerEntry entry)
    {
        if (entry.IsChart)
        {
            string chartPath = BeatmapPackagePaths.NormalizeRelativePath(entry.ChartPath);
            _selectedChartPath = chartPath;
            Close();
            _selectBeatmap?.Invoke(chartPath);
            return;
        }

        NavigateToFolder(entry.Path);
    }

    private void CreateBeatmapFromName()
    {
        string packageName = BeatmapPackagePaths.SanitizeFileName(_beatmapNameBuffer);
        if (string.IsNullOrWhiteSpace(packageName))
            packageName = "New Beatmap";

        string packagePath = Path.Combine(_currentFolderPath, packageName);
        packagePath = BeatmapPackagePaths.GetBeatmapsFolderStoragePath(packagePath);
        Close();
        _createBeatmap?.Invoke(packagePath);
    }

    private void CreateFolderFromName()
    {
        string folderName = BeatmapPackagePaths.SanitizeFileName(_folderNameBuffer);
        if (string.IsNullOrWhiteSpace(folderName))
            folderName = "New Folder";

        string folderPath = GetAvailableSubfolderPath(_currentFolderPath, folderName);
        Directory.CreateDirectory(folderPath);
        _folderNameBuffer = string.Empty;
        NavigateToFolder(folderPath);
        SetInputFocus(false, false);
    }

    private void NavigateToFolder(string folderPath)
    {
        if (!Directory.Exists(folderPath) || !BeatmapPackagePaths.IsInsideBeatmapsRoot(folderPath))
        {
            SetStatus("Folder is not inside Beatmaps.");
            return;
        }

        if (_mode == ExplorerMode.CreateBeatmap && !BeatmapPackagePaths.IsBeatmapsRoot(folderPath) && File.Exists(BeatmapPackagePaths.GetChartPathForPackage(folderPath)))
        {
            SetStatus("Beatmap packages are hidden while creating a new beatmap.");
            return;
        }

        _currentFolderPath = BeatmapPackagePaths.GetBeatmapsFolderStoragePath(folderPath);
        _folderListScroll = 0;
        SetInputFocus(false, false);
        SetStatus("Folder: " + BeatmapPackagePaths.GetBeatmapsFolderDisplayPath(_currentFolderPath));
    }

    private void NavigateToParentFolder()
    {
        string current = Path.GetFullPath(_currentFolderPath);
        string root = Path.GetFullPath(BeatmapPackagePaths.BeatmapsRoot);
        if (string.Equals(TrimPathEnd(current), TrimPathEnd(root), StringComparison.OrdinalIgnoreCase))
            return;

        string parent = Path.GetDirectoryName(current);
        if (!string.IsNullOrWhiteSpace(parent))
            NavigateToFolder(parent);
    }

    private IReadOnlyList<ExplorerEntry> GetEntries()
    {
        try
        {
            if (!Directory.Exists(_currentFolderPath) || !BeatmapPackagePaths.IsInsideBeatmapsRoot(_currentFolderPath))
                return Array.Empty<ExplorerEntry>();

            List<ExplorerEntry> entries = new();
            foreach (string directory in Directory.GetDirectories(_currentFolderPath).Where(BeatmapPackagePaths.IsInsideBeatmapsRoot).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
            {
                string chartPath = BeatmapPackagePaths.GetChartPathForPackage(directory);
                bool isPackage = File.Exists(chartPath);
                if (_mode == ExplorerMode.CreateBeatmap && isPackage)
                    continue;

                string label = Path.GetFileName(directory);
                entries.Add(new ExplorerEntry(label, directory, chartPath, isPackage));
            }

            if (_mode == ExplorerMode.SelectBeatmap)
            {
                foreach (string chartPath in Directory.GetFiles(_currentFolderPath, "*.xml", SearchOption.TopDirectoryOnly).Where(BeatmapPackagePaths.IsDiscoverableBeatmapChart).OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
                {
                    if (BeatmapPackagePaths.IsPackageChartPath(chartPath))
                        continue;

                    entries.Add(new ExplorerEntry(Path.GetFileNameWithoutExtension(chartPath), chartPath, chartPath, isChart: true));
                }
            }

            return entries;
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException)
        {
            return Array.Empty<ExplorerEntry>();
        }
    }

    private void ApplyFolderListScroll(MouseState mouse, MouseState previousMouse, Rectangle list, int entryCount)
    {
        int maxScroll = GetFolderListMaxScroll(list, entryCount);
        _folderListScroll = Math.Clamp(_folderListScroll, 0, maxScroll);

        if (!GetFolderListContentBounds(list).Contains(mouse.Position))
            return;

        int wheelDelta = mouse.ScrollWheelValue - previousMouse.ScrollWheelValue;
        if (wheelDelta == 0)
            return;

        _folderListScroll -= Math.Sign(wheelDelta) * RowHeight;
        _folderListScroll = Math.Clamp(_folderListScroll, 0, maxScroll);
    }

    private void UpdateTextInput(KeyboardState keyboard, KeyboardState previousKeyboard, ref string buffer)
    {
        if (DevUiTextInput.ShouldBackspace(keyboard, previousKeyboard, ref _backspaceHoldStartMs, ref _backspaceLastRepeatMs) && buffer.Length > 0)
            buffer = buffer[..^1];

        foreach (Keys key in keyboard.GetPressedKeys())
        {
            if (previousKeyboard.IsKeyDown(key) || key == Keys.Back)
                continue;

            if (DevUiTextInput.TryGetTypedChar(key, keyboard, out char c) && buffer.Length < 80)
                buffer += c;
        }
    }

    private void SetInputFocus(bool beatmapNameFocused, bool folderNameFocused)
    {
        if (_beatmapNameFocused == beatmapNameFocused && _folderNameFocused == folderNameFocused)
            return;

        _beatmapNameFocused = beatmapNameFocused;
        _folderNameFocused = folderNameFocused;
        ResetBackspaceRepeat();
    }

    private void Cancel()
    {
        Close();
        _cancel?.Invoke();
    }

    private void SetStatus(string status)
    {
        _setStatus?.Invoke(status);
    }

    private void DrawHeader(SpriteBatch spriteBatch, Rectangle modal, Point mousePosition)
    {
        Rectangle header = new(modal.X, modal.Y, modal.Width, 54);
        Rectangle closeButton = GetCloseButtonBounds(modal);
        _ui.Fill(spriteBatch, header, new Color(18, 36, 24, 245));
        _ui.Line(spriteBatch, new Vector2(modal.X, header.Bottom), new Vector2(modal.Right, header.Bottom), Color.DarkSlateGray, 1);
        _ui.Label(spriteBatch, _title, new Vector2(modal.X + 18, modal.Y + 14), Color.LightGreen, 2);

        string path = BeatmapPackagePaths.GetBeatmapsExplorerDisplayPath(_currentFolderPath);
        int pathWidth = Math.Max(80, closeButton.X - modal.X - 210);
        DrawFittedLabel(spriteBatch, path, new Vector2(modal.X + 190, modal.Y + 15), Color.White, 2, pathWidth);
        DrawButton(spriteBatch, closeButton, "X", mousePosition);
    }

    private void DrawSidebar(SpriteBatch spriteBatch, Rectangle sidebar, Point mousePosition)
    {
        _ui.Fill(spriteBatch, sidebar, new Color(8, 10, 14, 235));
        _ui.Stroke(spriteBatch, sidebar, Color.DarkSlateGray, 1);
        _ui.Label(spriteBatch, "LOCATIONS", new Vector2(sidebar.X + 12, sidebar.Y + 12), Color.LightBlue, 2);

        DrawButton(spriteBatch, GetRootButtonBounds(sidebar), "BEATMAPS", mousePosition, selected: BeatmapPackagePaths.IsBeatmapsRoot(_currentFolderPath));
        if (!BeatmapPackagePaths.IsBeatmapsRoot(_currentFolderPath))
            DrawButton(spriteBatch, GetParentButtonBounds(sidebar), "PARENT", mousePosition);

        Rectangle note = new(sidebar.X + 12, sidebar.Bottom - 94, sidebar.Width - 24, 82);
        _ui.Fill(spriteBatch, note, new Color(4, 6, 10, 210));
        _ui.Stroke(spriteBatch, note, Color.DarkSlateGray, 1);
        string text = _mode == ExplorerMode.CreateBeatmap
            ? "TYPE A BEATMAP\nNAME TO CREATE\nA PACKAGE FOLDER."
            : "BEATMAP PACKAGES\nARE SELECTABLE\nHERE.";
        _ui.Label(spriteBatch, text, new Vector2(note.X + 8, note.Y + 10), Color.DarkSeaGreen, 1);
    }

    private void DrawFolderList(SpriteBatch spriteBatch, Rectangle list, Point mousePosition)
    {
        IReadOnlyList<ExplorerEntry> entries = GetEntries();
        Rectangle content = GetFolderListContentBounds(list);

        _ui.Fill(spriteBatch, list, new Color(8, 10, 14, 235));
        _ui.Stroke(spriteBatch, list, Color.DarkSlateGray, 1);
        _ui.Label(spriteBatch, _mode == ExplorerMode.CreateBeatmap ? "FOLDERS" : "FOLDERS / BEATMAPS", new Vector2(list.X + 12, list.Y + 12), Color.LightBlue, 2);
        _ui.Line(spriteBatch, new Vector2(list.X + 10, content.Y - 8), new Vector2(list.Right - 10, content.Y - 8), Color.DarkSlateGray, 1);

        if (entries.Count == 0)
        {
            string emptyText = _mode == ExplorerMode.CreateBeatmap
                ? "No subfolders. Type a beatmap name or create a folder."
                : "No beatmaps or subfolders here.";
            DrawFittedLabel(spriteBatch, emptyText, new Vector2(content.X + 8, content.Y + 10), Color.DarkSeaGreen, 2, content.Width - 16);
            return;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            ExplorerEntry entry = entries[i];
            Rectangle row = GetEntryRowBounds(list, i);
            if (!content.Intersects(row))
                continue;

            bool selected = entry.IsChart && PathEquals(entry.ChartPath, _selectedChartPath);
            bool hovered = row.Contains(mousePosition);
            _ui.Fill(spriteBatch, row, selected ? new Color(30, 52, 36, 235) : hovered ? new Color(24, 58, 36, 235) : new Color(4, 6, 10, 160));
            _ui.Stroke(spriteBatch, row, selected || hovered ? Color.LightGreen : Color.DarkSlateGray, selected ? 2 : 1);

            if (entry.IsChart)
                DrawChartIcon(spriteBatch, new Rectangle(row.X + 12, row.Y + 7, 18, 20), selected || hovered ? Color.LightGreen : Color.CornflowerBlue);
            else
                DrawFolderIcon(spriteBatch, new Rectangle(row.X + 10, row.Y + 8, 20, 16), hovered ? Color.LightGreen : Color.Goldenrod);

            string label = entry.IsChart ? BeatmapPackagePaths.GetChartDisplayName(entry.ChartPath) : entry.Label;
            DrawFittedLabel(spriteBatch, label, new Vector2(row.X + 42, row.Y + 10), selected || hovered ? Color.LightGreen : Color.White, 2, row.Width - 54);
        }

        DrawScrollbar(spriteBatch, list, entries.Count);
    }

    private void DrawActions(SpriteBatch spriteBatch, Rectangle actions, Point mousePosition)
    {
        _ui.Fill(spriteBatch, actions, new Color(8, 10, 14, 235));
        _ui.Stroke(spriteBatch, actions, Color.DarkSlateGray, 1);
        _ui.Label(spriteBatch, "CURRENT FOLDER", new Vector2(actions.X + 14, actions.Y + 14), Color.LightBlue, 2);
        DrawValueBox(spriteBatch, new Rectangle(actions.X + 14, actions.Y + 44, actions.Width - 28, 34), BeatmapPackagePaths.GetBeatmapsExplorerDisplayPath(_currentFolderPath), Color.White);

        if (_mode == ExplorerMode.CreateBeatmap)
        {
            _ui.Label(spriteBatch, "BEATMAP NAME", new Vector2(actions.X + 14, actions.Y + 92), Color.LightBlue, 2);
            DrawTextInput(spriteBatch, GetBeatmapNameInputBounds(actions), _beatmapNameBuffer, _beatmapNameFocused, "<beatmap name>");
            DrawValueBox(spriteBatch, new Rectangle(actions.X + 14, actions.Y + 164, actions.Width - 28, 34), BeatmapPackagePaths.GetChartDisplayName(GetPreviewCreateChartPath()), Color.LightGreen);

            _ui.Label(spriteBatch, "FOLDER NAME", new Vector2(actions.X + 14, actions.Y + 212), Color.LightBlue, 2);
            DrawTextInput(spriteBatch, GetFolderNameInputBounds(actions), _folderNameBuffer, _folderNameFocused, "<folder name>");
            DrawButton(spriteBatch, GetCreateFolderButtonBounds(actions), "CREATE FOLDER", mousePosition);
            DrawButton(spriteBatch, GetCreateBeatmapButtonBounds(actions), "CREATE BEATMAP", mousePosition, primary: true);
        }
        else
        {
            _ui.Label(spriteBatch, "SELECTED", new Vector2(actions.X + 14, actions.Y + 92), Color.LightBlue, 2);
            string selectedText = string.IsNullOrWhiteSpace(_selectedChartPath) ? "<none>" : BeatmapPackagePaths.GetChartDisplayName(_selectedChartPath);
            DrawValueBox(spriteBatch, new Rectangle(actions.X + 14, actions.Y + 122, actions.Width - 28, 34), selectedText, Color.LightGreen);
            _ui.Label(spriteBatch, "CLICK A BEATMAP\nPACKAGE TO OPEN\nOR SELECT IT.", new Vector2(actions.X + 14, actions.Y + 178), Color.DarkSeaGreen, 1);
        }

        DrawButton(spriteBatch, GetCancelButtonBounds(actions), "CANCEL", mousePosition);
    }

    private string GetPreviewCreateChartPath()
    {
        string packageName = BeatmapPackagePaths.SanitizeFileName(_beatmapNameBuffer);
        if (string.IsNullOrWhiteSpace(packageName))
            packageName = "New Beatmap";

        return BeatmapPackagePaths.GetAvailablePackageChartPathForPackagePath(Path.Combine(_currentFolderPath, packageName));
    }

    private void DrawButton(SpriteBatch spriteBatch, Rectangle bounds, string text, Point mousePosition, bool selected = false, bool primary = false)
    {
        bool hovered = bounds.Contains(mousePosition);
        Color fill = primary
            ? hovered ? new Color(34, 84, 52, 245) : new Color(24, 58, 36, 245)
            : selected ? new Color(24, 58, 36, 245) : hovered ? new Color(18, 36, 24, 245) : new Color(4, 6, 10, 230);
        Color stroke = primary || hovered || selected ? Color.LightGreen : Color.DarkSlateGray;
        Color label = primary || hovered || selected ? Color.LightGreen : Color.White;
        _ui.Fill(spriteBatch, bounds, fill);
        _ui.Stroke(spriteBatch, bounds, stroke, primary ? 2 : 1);
        DrawFittedLabel(spriteBatch, text, new Vector2(bounds.X + 10, bounds.Y + 9), label, 2, bounds.Width - 20);
    }

    private void DrawTextInput(SpriteBatch spriteBatch, Rectangle bounds, string value, bool focused, string placeholder)
    {
        bool empty = string.IsNullOrEmpty(value);
        string text = empty && !focused ? placeholder : value;
        if (focused)
            text += "|";

        Color stroke = focused ? Color.Yellow : Color.LightGreen;
        Color label = empty && !focused ? Color.DarkSeaGreen : focused ? Color.Yellow : Color.White;
        _ui.Fill(spriteBatch, bounds, Color.Black * 0.85f);
        _ui.Stroke(spriteBatch, bounds, stroke, 1);
        DrawFittedLabel(spriteBatch, text, new Vector2(bounds.X + 8, bounds.Y + 8), label, 2, bounds.Width - 16);
    }

    private void DrawValueBox(SpriteBatch spriteBatch, Rectangle bounds, string text, Color color)
    {
        _ui.Fill(spriteBatch, bounds, Color.Black * 0.75f);
        _ui.Stroke(spriteBatch, bounds, Color.DarkSlateGray, 1);
        DrawFittedLabel(spriteBatch, text, new Vector2(bounds.X + 8, bounds.Y + 10), color, 2, bounds.Width - 16);
    }

    private void DrawFolderIcon(SpriteBatch spriteBatch, Rectangle bounds, Color color)
    {
        Rectangle tab = new(bounds.X + 2, bounds.Y, Math.Max(6, bounds.Width / 2), 5);
        Rectangle body = new(bounds.X, bounds.Y + 4, bounds.Width, bounds.Height - 4);
        _ui.Fill(spriteBatch, tab, color * 0.75f);
        _ui.Fill(spriteBatch, body, color * 0.65f);
        _ui.Stroke(spriteBatch, body, color, 1);
    }

    private void DrawChartIcon(SpriteBatch spriteBatch, Rectangle bounds, Color color)
    {
        _ui.Fill(spriteBatch, bounds, color * 0.35f);
        _ui.Stroke(spriteBatch, bounds, color, 1);
        _ui.Line(spriteBatch, new Vector2(bounds.X + 4, bounds.Y + 6), new Vector2(bounds.Right - 4, bounds.Y + 6), color, 1);
        _ui.Line(spriteBatch, new Vector2(bounds.X + 4, bounds.Y + 11), new Vector2(bounds.Right - 4, bounds.Y + 11), color, 1);
    }

    private void DrawScrollbar(SpriteBatch spriteBatch, Rectangle list, int entryCount)
    {
        Rectangle content = GetFolderListContentBounds(list);
        int contentHeight = entryCount * RowHeight;
        if (contentHeight <= content.Height)
            return;

        Rectangle track = new(list.Right - 12, content.Y, 4, content.Height);
        int thumbHeight = Math.Max(18, track.Height * content.Height / contentHeight);
        int maxScroll = GetFolderListMaxScroll(list, entryCount);
        int thumbY = maxScroll == 0 ? track.Y : track.Y + (track.Height - thumbHeight) * _folderListScroll / maxScroll;
        _ui.Fill(spriteBatch, track, Color.DarkSlateGray);
        _ui.Fill(spriteBatch, new Rectangle(track.X, thumbY, track.Width, thumbHeight), Color.LightGreen);
    }

    private void DrawFittedLabel(SpriteBatch spriteBatch, string text, Vector2 position, Color color, int scale, int maxWidth)
    {
        _ui.Label(spriteBatch, FitDevUiText(text, maxWidth, scale), position, color, scale);
    }

    private Rectangle GetEntryRowBounds(Rectangle list, int index)
    {
        Rectangle content = GetFolderListContentBounds(list);
        return new Rectangle(content.X, content.Y + index * RowHeight - _folderListScroll, content.Width - 8, RowHeight - 2);
    }

    private static Rectangle GetExplorerBounds(Viewport viewport)
    {
        int width = Math.Clamp(viewport.Width - 100, 760, 1120);
        int height = Math.Clamp(viewport.Height - 120, 480, 720);
        return new Rectangle((viewport.Width - width) / 2, (viewport.Height - height) / 2, width, height);
    }

    private static Rectangle GetSidebarBounds(Rectangle modal)
    {
        return new Rectangle(modal.X + 16, modal.Y + 70, 170, modal.Height - 86);
    }

    private static Rectangle GetActionsBounds(Rectangle modal)
    {
        const int width = 276;
        return new Rectangle(modal.Right - width - 16, modal.Y + 70, width, modal.Height - 86);
    }

    private static Rectangle GetFolderListBounds(Rectangle modal)
    {
        Rectangle sidebar = GetSidebarBounds(modal);
        Rectangle actions = GetActionsBounds(modal);
        int x = sidebar.Right + 14;
        return new Rectangle(x, modal.Y + 70, Math.Max(1, actions.X - x - 14), modal.Height - 86);
    }

    private static Rectangle GetCloseButtonBounds(Rectangle modal)
    {
        return new Rectangle(modal.Right - 48, modal.Y + 12, 32, 30);
    }

    private static Rectangle GetRootButtonBounds(Rectangle sidebar)
    {
        return new Rectangle(sidebar.X + 12, sidebar.Y + 46, sidebar.Width - 24, 34);
    }

    private static Rectangle GetParentButtonBounds(Rectangle sidebar)
    {
        return new Rectangle(sidebar.X + 12, sidebar.Y + 88, sidebar.Width - 24, 34);
    }

    private static Rectangle GetFolderListContentBounds(Rectangle list)
    {
        return new Rectangle(list.X + 10, list.Y + 44, list.Width - 20, list.Height - 54);
    }

    private static int GetFolderListMaxScroll(Rectangle list, int entryCount)
    {
        Rectangle content = GetFolderListContentBounds(list);
        return Math.Max(0, entryCount * RowHeight - content.Height);
    }

    private static Rectangle GetBeatmapNameInputBounds(Rectangle actions)
    {
        return new Rectangle(actions.X + 14, actions.Y + 120, actions.Width - 28, 30);
    }

    private static Rectangle GetFolderNameInputBounds(Rectangle actions)
    {
        return new Rectangle(actions.X + 14, actions.Y + 240, actions.Width - 28, 30);
    }

    private static Rectangle GetCreateFolderButtonBounds(Rectangle actions)
    {
        Rectangle input = GetFolderNameInputBounds(actions);
        return new Rectangle(actions.X + 14, input.Bottom + 10, actions.Width - 28, 34);
    }

    private static Rectangle GetCreateBeatmapButtonBounds(Rectangle actions)
    {
        return new Rectangle(actions.X + 14, actions.Bottom - 94, actions.Width - 28, 42);
    }

    private static Rectangle GetCancelButtonBounds(Rectangle actions)
    {
        return new Rectangle(actions.X + 14, actions.Bottom - 44, actions.Width - 28, 32);
    }

    private static string GetInitialSelectFolder(string selectedChartPath)
    {
        string packagePath = BeatmapPackagePaths.GetPackagePath(BeatmapPackagePaths.ResolveChartPath(selectedChartPath));
        string folder = !string.IsNullOrWhiteSpace(packagePath) ? Path.GetDirectoryName(packagePath) : null;
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder) || !BeatmapPackagePaths.IsInsideBeatmapsRoot(folder))
            return BeatmapPackagePaths.BeatmapsRoot;

        return BeatmapPackagePaths.GetBeatmapsFolderStoragePath(folder);
    }

    private static string GetAvailableSubfolderPath(string parentFolderPath, string folderName)
    {
        string candidate = Path.Combine(parentFolderPath, folderName);
        if (!Directory.Exists(candidate) && !File.Exists(candidate))
            return candidate;

        for (int i = 2; ; i++)
        {
            candidate = Path.Combine(parentFolderPath, $"{folderName}_{i}");
            if (!Directory.Exists(candidate) && !File.Exists(candidate))
                return candidate;
        }
    }

    private static string FitDevUiText(string text, int maxWidth, int scale)
    {
        if (string.IsNullOrEmpty(text) || maxWidth <= 0)
            return string.Empty;

        int maxChars = Math.Max(1, maxWidth / Math.Max(1, scale * 4));
        if (text.Length <= maxChars)
            return text;

        if (maxChars <= 3)
            return text[..maxChars];

        return text[..(maxChars - 3)] + "...";
    }

    private static string TrimPathEnd(string path)
    {
        return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    private static bool PathEquals(string left, string right)
    {
        return string.Equals(BeatmapPackagePaths.NormalizeRelativePath(left), BeatmapPackagePaths.NormalizeRelativePath(right), StringComparison.OrdinalIgnoreCase);
    }

    private static bool Pressed(KeyboardState keyboard, KeyboardState previousKeyboard, Keys key)
    {
        return keyboard.IsKeyDown(key) && !previousKeyboard.IsKeyDown(key);
    }

    private static bool LeftPressed(MouseState mouse, MouseState previousMouse)
    {
        return mouse.LeftButton == ButtonState.Pressed && previousMouse.LeftButton == ButtonState.Released;
    }

    private void ResetInteractionState()
    {
        _beatmapNameFocused = false;
        _folderNameFocused = false;
        _folderListScroll = 0;
        ResetBackspaceRepeat();
    }

    private void ResetBackspaceRepeat()
    {
        _backspaceHoldStartMs = 0;
        _backspaceLastRepeatMs = 0;
    }
}
