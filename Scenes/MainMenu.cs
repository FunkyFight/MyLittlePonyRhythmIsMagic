using System;
using System.Collections.Generic;
using System.Linq;
using GameCore.Inputs;
using GameCore.Scenes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MLP_RiM.Elements.DevUI;
using MLP_RiM.Elements.Levels;

public sealed class MainMenu : Scene
{
    private const string MenuUpAction = "MenuUp";
    private const string MenuDownAction = "MenuDown";
    private const string MenuSelectAction = "MenuSelect";
    private const int PlayIndex = 0;
    private const int LevelEditorIndex = 1;
    private const int BeatmapEditorIndex = 2;
    private const int MenuWidth = 680;
    private const int MenuItemHeight = 76;
    private const int MenuItemGap = 18;

    private static readonly string[] MenuItems =
    {
        "Jouer",
        "Éditeur de niveau",
        "Éditeur de beatmap"
    };

    private readonly InputActionManager _inputActionManager;
    private readonly Action<LevelDocument> _startLevel;
    private readonly Action _openLevelEditor;
    private readonly Action _openBeatmapEditor;
    private readonly List<LevelMenuEntry> _levels = new();
    private DevUiRenderer _ui;
    private MenuMode _mode = MenuMode.Main;
    private int _selectedIndex;
    private int _selectedLevelIndex;
    private string _status = string.Empty;
    private KeyboardState _keyboard;
    private KeyboardState _previousKeyboard;

    public MainMenu(InputActionManager inputActionManager, Action<LevelDocument> startLevel, Action openLevelEditor, Action openBeatmapEditor) : base("Main Menu")
    {
        _inputActionManager = inputActionManager ?? throw new ArgumentNullException(nameof(inputActionManager));
        _startLevel = startLevel ?? throw new ArgumentNullException(nameof(startLevel));
        _openLevelEditor = openLevelEditor ?? throw new ArgumentNullException(nameof(openLevelEditor));
        _openBeatmapEditor = openBeatmapEditor ?? throw new ArgumentNullException(nameof(openBeatmapEditor));
    }

    public override void OnLoad()
    {
        _ui ??= new DevUiRenderer(GLOBALS.graphicsDevice);
    }

    public override void OnUnload()
    {
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        _previousKeyboard = _keyboard;
        _keyboard = Keyboard.GetState();

        if (_mode == MenuMode.LevelList)
            UpdateLevelList();
        else
            UpdateMainMenu();
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        Viewport viewport = GLOBALS.graphicsDevice.Viewport;
        DrawBackground(spriteBatch, viewport.Width, viewport.Height);

        if (_mode == MenuMode.LevelList)
            DrawLevelList(spriteBatch, viewport.Width, viewport.Height);
        else
        {
            DrawTitle(spriteBatch, viewport.Width);
            DrawMenu(spriteBatch, viewport.Width, viewport.Height);
        }

        base.Draw(spriteBatch);
    }

    private void UpdateMainMenu()
    {
        int movement = GetMenuMovement();
        if (movement != 0)
            _selectedIndex = WrapIndex(_selectedIndex + movement, MenuItems.Length);

        if (_inputActionManager.IsPressedOnce(MenuSelectAction))
            ActivateSelection();
    }

    private void UpdateLevelList()
    {
        if (Pressed(Keys.Escape) || Pressed(Keys.Back))
        {
            _mode = MenuMode.Main;
            _status = string.Empty;
            return;
        }

        int movement = GetMenuMovement();
        if (movement != 0 && _levels.Count > 0)
            _selectedLevelIndex = WrapIndex(_selectedLevelIndex + movement, _levels.Count);

        if (_inputActionManager.IsPressedOnce(MenuSelectAction))
            ActivateSelectedLevel();
    }

    private int GetMenuMovement()
    {
        int movement = 0;
        if (_inputActionManager.IsPressedOnce(MenuUpAction))
            movement--;
        if (_inputActionManager.IsPressedOnce(MenuDownAction))
            movement++;
        return movement;
    }

    private void ActivateSelection()
    {
        switch (_selectedIndex)
        {
            case PlayIndex:
                OpenLevelList();
                break;
            case LevelEditorIndex:
                _openLevelEditor();
                break;
            case BeatmapEditorIndex:
                _openBeatmapEditor();
                break;
        }
    }

    private void OpenLevelList()
    {
        RefreshLevels();
        _mode = MenuMode.LevelList;
        _selectedLevelIndex = 0;
        _status = _levels.Count == 0 ? "No Levels/*/level.xml files found." : string.Empty;
    }

    private void RefreshLevels()
    {
        _levels.Clear();
        LevelProgressSave save = LevelProgressSave.Load();

        foreach (string levelFile in LevelDocument.DiscoverLevelFiles())
        {
            try
            {
                LevelDocument document = LevelDocument.LoadOrCreate(levelFile);
                bool locked = !save.IsUnlocked(document.Level);
                string displayName = string.IsNullOrWhiteSpace(document.Level.DisplayName)
                    ? levelFile
                    : document.Level.DisplayName;
                _levels.Add(new LevelMenuEntry(levelFile, displayName, locked));
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is UnauthorizedAccessException)
            {
                _levels.Add(new LevelMenuEntry(levelFile, levelFile, true));
            }
        }

        _levels.Sort((left, right) => string.Compare(left.DisplayName, right.DisplayName, StringComparison.OrdinalIgnoreCase));
    }

    private void ActivateSelectedLevel()
    {
        if (_levels.Count == 0)
            return;

        LevelMenuEntry entry = _levels[Math.Clamp(_selectedLevelIndex, 0, _levels.Count - 1)];
        if (entry.Locked)
        {
            _status = "Level is locked.";
            return;
        }

        try
        {
            _startLevel(LevelDocument.LoadOrCreate(entry.FilePath));
        }
        catch (Exception ex) when (ex is InvalidOperationException || ex is UnauthorizedAccessException)
        {
            _status = "Cannot open level.";
        }
    }

    private void DrawBackground(SpriteBatch spriteBatch, int width, int height)
    {
        _ui.Fill(spriteBatch, new Rectangle(0, 0, width, height), new Color(24, 18, 48));
    }

    private void DrawTitle(SpriteBatch spriteBatch, int width)
    {
        DrawCenteredText(spriteBatch, "MY LITTLE PONY", width / 2, 150, 12, new Color(255, 196, 236));
        DrawCenteredText(spriteBatch, "RHYTHM IS MAGIC", width / 2, 235, 12, new Color(174, 228, 255));
        DrawCenteredText(spriteBatch, "PLACEHOLDER TITLE", width / 2, 330, 4, new Color(255, 255, 255, 170));
    }

    private void DrawMenu(SpriteBatch spriteBatch, int width, int height)
    {
        int totalItemsHeight = MenuItems.Length * MenuItemHeight + (MenuItems.Length - 1) * MenuItemGap;
        int menuTop = Math.Max(430, height / 2 - totalItemsHeight / 2 + 90);
        Rectangle panel = new(width / 2 - MenuWidth / 2 - 30, menuTop - 36, MenuWidth + 60, totalItemsHeight + 118);

        _ui.Fill(spriteBatch, panel, new Color(10, 8, 24, 205));
        _ui.Stroke(spriteBatch, panel, new Color(255, 196, 236), 4);

        for (int i = 0; i < MenuItems.Length; i++)
        {
            Rectangle bounds = new(width / 2 - MenuWidth / 2, menuTop + i * (MenuItemHeight + MenuItemGap), MenuWidth, MenuItemHeight);
            DrawMenuItem(spriteBatch, bounds, MenuItems[i], i == _selectedIndex, true);
        }

        DrawCenteredText(spriteBatch, "Z/S: naviguer   ENTREE: valider", width / 2, panel.Bottom - 44, 3, new Color(255, 255, 255, 180));
    }

    private void DrawLevelList(SpriteBatch spriteBatch, int width, int height)
    {
        DrawCenteredText(spriteBatch, "LEVELS", width / 2, 145, 12, new Color(174, 228, 255));

        Rectangle panel = new(width / 2 - 460, 260, 920, Math.Min(650, height - 330));
        _ui.Fill(spriteBatch, panel, new Color(10, 8, 24, 220));
        _ui.Stroke(spriteBatch, panel, new Color(255, 196, 236), 4);

        if (_levels.Count == 0)
        {
            DrawCenteredText(spriteBatch, "AUCUN NIVEAU", width / 2, panel.Y + 165, 6, Color.White * 0.8f);
        }
        else
        {
            int visibleCount = Math.Min(_levels.Count, 8);
            int start = Math.Clamp(_selectedLevelIndex - visibleCount / 2, 0, Math.Max(0, _levels.Count - visibleCount));
            int y = panel.Y + 40;
            for (int i = 0; i < visibleCount; i++)
            {
                int levelIndex = start + i;
                LevelMenuEntry entry = _levels[levelIndex];
                Rectangle row = new(panel.X + 46, y, panel.Width - 92, 62);
                DrawLevelRow(spriteBatch, row, entry, levelIndex == _selectedLevelIndex);
                y += 76;
            }
        }

        if (!string.IsNullOrWhiteSpace(_status))
            DrawCenteredText(spriteBatch, _status, width / 2, panel.Bottom - 78, 2, Color.IndianRed);
        DrawCenteredText(spriteBatch, "Z/S: naviguer   ENTREE: lancer   ESC: retour", width / 2, panel.Bottom - 42, 3, new Color(255, 255, 255, 180));
    }

    private void DrawMenuItem(SpriteBatch spriteBatch, Rectangle bounds, string label, bool selected, bool active)
    {
        Color fill = selected ? new Color(91, 58, 128, 245) : new Color(28, 24, 54, 235);
        Color stroke = selected ? new Color(174, 228, 255) : new Color(94, 79, 128);
        Color text = active ? Color.White : new Color(190, 180, 210);

        _ui.Fill(spriteBatch, bounds, fill);
        _ui.Stroke(spriteBatch, bounds, stroke, selected ? 4 : 2);

        if (selected)
        {
            _ui.Fill(spriteBatch, new Rectangle(bounds.X + 12, bounds.Y + 12, 12, bounds.Height - 24), new Color(255, 196, 236));
            _ui.Fill(spriteBatch, new Rectangle(bounds.Right - 24, bounds.Y + 12, 12, bounds.Height - 24), new Color(255, 196, 236));
        }

        int labelScale = 6;
        int labelX = bounds.Center.X - GetTextWidth(label, labelScale) / 2;
        int labelY = bounds.Center.Y - GetTextHeight(labelScale) / 2;
        _ui.Label(spriteBatch, label, new Vector2(labelX, labelY), text, labelScale);
    }

    private void DrawLevelRow(SpriteBatch spriteBatch, Rectangle bounds, LevelMenuEntry entry, bool selected)
    {
        Color fill = entry.Locked ? new Color(32, 32, 42, 230) : new Color(28, 24, 54, 235);
        Color stroke = selected ? new Color(174, 228, 255) : new Color(94, 79, 128);
        Color text = entry.Locked ? Color.Gray : Color.White;

        _ui.Fill(spriteBatch, bounds, selected ? new Color(91, 58, 128, 245) : fill);
        _ui.Stroke(spriteBatch, bounds, stroke, selected ? 4 : 2);
        _ui.Label(spriteBatch, Truncate(entry.DisplayName, 34), new Vector2(bounds.X + 24, bounds.Y + 20), text, 4);

        if (entry.Locked)
            _ui.Label(spriteBatch, "LOCKED", new Vector2(bounds.Right - 150, bounds.Y + 22), Color.Gray, 3);
    }

    private void DrawCenteredText(SpriteBatch spriteBatch, string text, int centerX, int y, int scale, Color color)
    {
        _ui.Label(spriteBatch, text, new Vector2(centerX - GetTextWidth(text, scale) / 2, y), color, scale);
    }

    private bool Pressed(Keys key)
    {
        return _keyboard.IsKeyDown(key) && !_previousKeyboard.IsKeyDown(key);
    }

    private static int WrapIndex(int index, int count)
    {
        return count <= 0 ? 0 : (index % count + count) % count;
    }

    private static string Truncate(string value, int maxLength)
    {
        value ??= string.Empty;
        return value.Length <= maxLength ? value : value[..Math.Max(0, maxLength - 3)] + "...";
    }

    private static int GetTextWidth(string text, int scale)
    {
        return string.IsNullOrEmpty(text) ? 0 : (text.Length * 4 - 1) * scale;
    }

    private static int GetTextHeight(int scale)
    {
        return 5 * scale;
    }

    private enum MenuMode
    {
        Main,
        LevelList
    }

    private readonly record struct LevelMenuEntry(string FilePath, string DisplayName, bool Locked);
}
