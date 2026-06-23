using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MLP_RiM.Elements.DevUI;
using MLP_RiM.Elements.Editor;
using MLP_RiM.Elements.Levels;

namespace MLP_RiM.Elements.LevelEditor;

public sealed class LevelEditorElement
{
    private const int TopBarHeight = 48;
    private const int LeftPanelWidth = 220;
    private const int RightPanelWidth = 390;
    private const int StatusBarHeight = 38;
    private const int NodeWidth = 184;
    private const int NodeHeight = 92;
    private const int PortSize = 14;

    private readonly GraphicsDevice _graphicsDevice;
    private readonly DevUiRenderer _ui;
    private readonly BeatmapFolderExplorer _beatmapSelector;
    private readonly Action<LevelDocument> _playLevel;
    private readonly Action _closeEditor;

    private LevelDocument _document;
    private MouseState _mouse;
    private MouseState _previousMouse;
    private KeyboardState _keyboard;
    private KeyboardState _previousKeyboard;
    private string _selectedNodeId;
    private string _draggingNodeId;
    private Point _dragOffset;
    private string _wireFromNodeId;
    private string _wireFromPort;
    private bool _showOpenList;
    private bool _showUnlockLevelSelector;
    private bool _showSpeakerSelector;
    private bool _showMiniGameSelector;
    private string _beatmapSelectorNodeId;
    private IReadOnlyList<string> _openLevelFiles = Array.Empty<string>();
    private IReadOnlyList<LevelSelectorEntry> _levelSelectorEntries = Array.Empty<LevelSelectorEntry>();
    private IReadOnlyList<SpeakerSelectorEntry> _speakerSelectorEntries = Array.Empty<SpeakerSelectorEntry>();
    private IReadOnlyList<MiniGameSelectorEntry> _miniGameSelectorEntries = Array.Empty<MiniGameSelectorEntry>();
    private string _editingField;
    private string _editingBuffer = string.Empty;
    private string _status = "Ready.";
    private Vector2 _canvasPan;
    private bool _panningCanvas;
    private Point _panMouseStart;
    private Vector2 _panStart;
    private long _backspaceHoldStartMs;
    private long _backspaceLastRepeatMs;
    private LevelNodeData _clipboardNode;
    private int _clipboardPasteSerial;

    private readonly record struct LevelSelectorEntry(string FilePath, string DisplayName, string LevelId);
    private readonly record struct SpeakerSelectorEntry(LevelSpeaker Speaker, string DisplayName);
    private readonly record struct MiniGameSelectorEntry(string MiniGameId, string DisplayName);

    public LevelEditorElement(GraphicsDevice graphicsDevice, Action<LevelDocument> playLevel, Action closeEditor)
    {
        _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
        _ui = new DevUiRenderer(graphicsDevice);
        _beatmapSelector = new BeatmapFolderExplorer(_ui);
        _playLevel = playLevel ?? throw new ArgumentNullException(nameof(playLevel));
        _closeEditor = closeEditor ?? throw new ArgumentNullException(nameof(closeEditor));
        _document = LevelDocument.CreateNewPackage("New Level");
        _selectedNodeId = _document.Level.StartNodeId;
        _status = "New level created. Save to write level.xml.";
    }

    public void Update(GameTime gameTime)
    {
        _previousMouse = _mouse;
        _mouse = Mouse.GetState();
        _previousKeyboard = _keyboard;
        _keyboard = Keyboard.GetState();

        if (_beatmapSelector.IsOpen)
        {
            _beatmapSelector.Update(_mouse, _previousMouse, _keyboard, _previousKeyboard, _graphicsDevice.Viewport);
            return;
        }

        UpdateTextEditing();

        if (_showOpenList)
        {
            UpdateOpenLevelList();
            return;
        }

        if (_showUnlockLevelSelector)
        {
            UpdateUnlockLevelSelector();
            return;
        }

        if (_showSpeakerSelector)
        {
            UpdateSpeakerSelector();
            return;
        }

        if (_showMiniGameSelector)
        {
            UpdateMiniGameSelector();
            return;
        }

        if (_editingField == null && HandleClipboardShortcuts())
            return;

        if (Pressed(Keys.Escape))
        {
            if (_editingField != null)
                CancelEditing();
            else
                _closeEditor();
            return;
        }

        if (HandleTopBar())
            return;

        if (HandleInspector())
            return;

        if (HandlePalette())
            return;

        HandleCanvas();

        if (_editingField == null && (Pressed(Keys.Delete) || Pressed(Keys.Back)))
            DeleteSelectedNode();
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        Viewport viewport = _graphicsDevice.Viewport;
        Rectangle full = new(0, 0, viewport.Width, viewport.Height);
        _ui.Fill(spriteBatch, full, new Color(5, 7, 14, 245));

        DrawCanvas(spriteBatch);
        DrawTopBar(spriteBatch);
        DrawPalette(spriteBatch);
        DrawInspector(spriteBatch);
        DrawStatusBar(spriteBatch);

        if (_showOpenList)
            DrawOpenList(spriteBatch);
        if (_showUnlockLevelSelector)
            DrawUnlockLevelSelector(spriteBatch);
        if (_showSpeakerSelector)
            DrawSpeakerSelector(spriteBatch);
        if (_showMiniGameSelector)
            DrawMiniGameSelector(spriteBatch);
        _beatmapSelector.Draw(spriteBatch, _graphicsDevice.Viewport, _mouse.Position);
    }

    private bool HandleTopBar()
    {
        Rectangle topBar = GetTopBarBounds();
        if (!topBar.Contains(_mouse.Position))
            return false;

        if (!LeftPressed())
            return true;

        CommitEditing();
        int x = topBar.X + 12;
        if (TryConsumeButton(new Rectangle(x, 8, 96, 32), NewLevel))
            return true;
        x += 106;
        if (TryConsumeButton(new Rectangle(x, 8, 100, 32), OpenLevelList))
            return true;
        x += 110;
        if (TryConsumeButton(new Rectangle(x, 8, 84, 32), SaveLevel))
            return true;
        x += 94;
        if (TryConsumeButton(new Rectangle(x, 8, 92, 32), ReloadLevel))
            return true;
        x += 102;
        if (TryConsumeButton(new Rectangle(x, 8, 84, 32), PlayLevel))
            return true;
        x += 94;
        TryConsumeButton(new Rectangle(x, 8, 84, 32), _closeEditor);
        return true;
    }

    private bool HandlePalette()
    {
        Rectangle palette = GetPaletteBounds();
        if (!palette.Contains(_mouse.Position))
            return false;

        if (!LeftPressed())
            return true;

        CommitEditing();
        int y = palette.Y + 62;
        if (TryConsumeButton(new Rectangle(palette.X + 16, y, palette.Width - 32, 36), () => AddNode(LevelNodeKind.Dialogue)))
            return true;
        y += 46;
        if (TryConsumeButton(new Rectangle(palette.X + 16, y, palette.Width - 32, 36), () => AddNode(LevelNodeKind.TrainingBeatmap)))
            return true;
        y += 46;
        if (TryConsumeButton(new Rectangle(palette.X + 16, y, palette.Width - 32, 36), () => AddNode(LevelNodeKind.PlayRepresentationBeatmap)))
            return true;
        y += 46;
        if (TryConsumeButton(new Rectangle(palette.X + 16, y, palette.Width - 32, 36), () => AddNode(LevelNodeKind.SetMiniGame)))
            return true;
        y += 46;
        TryConsumeButton(new Rectangle(palette.X + 16, y, palette.Width - 32, 36), () => AddNode(LevelNodeKind.End));
        return true;
    }

    private bool HandleInspector()
    {
        Rectangle inspector = GetInspectorBounds();
        if (!inspector.Contains(_mouse.Position))
            return false;

        if (!LeftPressed())
            return true;

        LevelNodeData selectedNode = SelectedNode;
        if (selectedNode == null)
            return true;

        int y = GetInspectorPropertyStartY(inspector);
        if (selectedNode.Kind == LevelNodeKind.Start)
        {
            if (TryBeginTextField(new Rectangle(inspector.X + 14, y + 18, inspector.Width - 28, 30), "level.display"))
                return true;
            y += 60;

            Rectangle lockedBox = new(inspector.X + 14, y + 4, 18, 18);
            if (lockedBox.Contains(_mouse.Position))
            {
                CommitEditing();
                _document.Level.LockedByDefault = !_document.Level.LockedByDefault;
                _document.MarkDirty();
                return true;
            }
            y += 42;

            if (TryBeginTextField(new Rectangle(inspector.X + 14, y + 18, inspector.Width - 28, 30), "level.unlocks"))
                return true;
            y += 58;

            if (TryConsumeButton(new Rectangle(inspector.X + 14, y, inspector.Width - 28, 34), OpenUnlockLevelSelector))
                return true;
        }
        else if (selectedNode.Kind == LevelNodeKind.Dialogue)
        {
            if (TryConsumeButton(new Rectangle(inspector.X + 14, y + 18, inspector.Width - 28, 34), OpenSpeakerSelector))
                return true;
            y += 60;

            string textFieldKey = $"node.text:{selectedNode.Id}";
            if (TryBeginTextField(new Rectangle(inspector.X + 14, y + 18, inspector.Width - 28, GetTextFieldHeight(textFieldKey, inspector.Width - 28)), textFieldKey))
                return true;
        }
        else if (selectedNode.Kind == LevelNodeKind.TrainingBeatmap || selectedNode.Kind == LevelNodeKind.PlayRepresentationBeatmap)
        {
            Rectangle chartValue = new(inspector.X + 14, y + 18, inspector.Width - 110, 30);
            if (chartValue.Contains(_mouse.Position) || TryConsumeButton(new Rectangle(inspector.Right - 88, y + 18, 74, 30), () => OpenBeatmapSelector(selectedNode)))
            {
                if (chartValue.Contains(_mouse.Position))
                    OpenBeatmapSelector(selectedNode);
                return true;
            }
            y += 60;

            if (selectedNode.Kind == LevelNodeKind.TrainingBeatmap)
            {
                Rectangle minus = new(inspector.X + 14, y + 18, 34, 30);
                Rectangle plus = new(inspector.Right - 48, y + 18, 34, 30);
                if (minus.Contains(_mouse.Position))
                {
                    CommitEditing();
                    selectedNode.RequiredSuccessCount = Math.Max(1, selectedNode.RequiredSuccessCount - 1);
                    _document.MarkDirty();
                    return true;
                }
                if (plus.Contains(_mouse.Position))
                {
                    CommitEditing();
                    selectedNode.RequiredSuccessCount++;
                    _document.MarkDirty();
                    return true;
                }
            }
        }
        else if (selectedNode.Kind == LevelNodeKind.SetMiniGame)
        {
            if (TryBeginTextField(new Rectangle(inspector.X + 14, y + 18, inspector.Width - 110, 30), $"node.minigame:{selectedNode.Id}"))
                return true;

            if (TryConsumeButton(new Rectangle(inspector.Right - 88, y + 18, 74, 30), OpenMiniGameSelector))
                return true;
        }

        return true;
    }

    private void HandleCanvas()
    {
        Rectangle canvas = GetCanvasBounds();

        if (MiddlePressed() && canvas.Contains(_mouse.Position))
        {
            CommitEditing();
            _panningCanvas = true;
            _panMouseStart = _mouse.Position;
            _panStart = _canvasPan;
            _status = "Middle drag: panning canvas.";
        }

        if (_panningCanvas)
        {
            if (_mouse.MiddleButton == ButtonState.Pressed)
            {
                Point delta = _mouse.Position - _panMouseStart;
                _canvasPan = _panStart + new Vector2(delta.X, delta.Y);
            }
            else
            {
                _panningCanvas = false;
            }
        }

        if (LeftPressed())
        {
            CommitEditing();

            if (TryGetOutputPortAt(_mouse.Position, out LevelNodeData portNode, out string portName))
            {
                _wireFromNodeId = portNode.Id;
                _wireFromPort = portName;
                _selectedNodeId = portNode.Id;
                return;
            }

            LevelNodeData clickedNode = GetNodeAt(_mouse.Position);
            if (clickedNode != null)
            {
                _selectedNodeId = clickedNode.Id;
                Rectangle nodeBounds = GetNodeBounds(clickedNode);
                _draggingNodeId = clickedNode.Id;
                _dragOffset = new Point(_mouse.X - nodeBounds.X, _mouse.Y - nodeBounds.Y);
                return;
            }

            if (canvas.Contains(_mouse.Position))
                _selectedNodeId = null;
        }

        if (_draggingNodeId != null && _mouse.LeftButton == ButtonState.Pressed)
        {
            LevelNodeData node = _document.FindNode(_draggingNodeId);
            if (node != null)
            {
                node.X = (int)MathF.Round(_mouse.X - canvas.X - _canvasPan.X - _dragOffset.X);
                node.Y = (int)MathF.Round(_mouse.Y - canvas.Y - _canvasPan.Y - _dragOffset.Y);
                _document.MarkDirty();
            }
        }

        if (LeftReleased())
        {
            if (_wireFromNodeId != null)
            {
                LevelNodeData targetNode = GetNodeAt(_mouse.Position);
                if (targetNode != null)
                {
                    _document.SetConnection(_wireFromNodeId, _wireFromPort, targetNode.Id);
                    _status = $"Connected {_wireFromPort}.";
                }
            }

            _draggingNodeId = null;
            _wireFromNodeId = null;
            _wireFromPort = null;
        }
    }

    private void UpdateOpenLevelList()
    {
        if (Pressed(Keys.Escape))
        {
            _showOpenList = false;
            return;
        }

        if (!LeftPressed())
            return;

        Rectangle modal = GetOpenListBounds();
        Rectangle close = new(modal.Right - 94, modal.Y + 14, 72, 28);
        if (close.Contains(_mouse.Position))
        {
            _showOpenList = false;
            return;
        }

        int y = modal.Y + 70;
        foreach (string levelFile in _openLevelFiles.Take(12))
        {
            Rectangle row = new(modal.X + 24, y, modal.Width - 48, 34);
            if (row.Contains(_mouse.Position))
            {
                LoadLevel(levelFile);
                _showOpenList = false;
                return;
            }

            y += 42;
        }
    }

    private void UpdateUnlockLevelSelector()
    {
        if (Pressed(Keys.Escape))
        {
            _showUnlockLevelSelector = false;
            return;
        }

        if (!LeftPressed())
            return;

        Rectangle modal = GetOpenListBounds();
        Rectangle close = new(modal.Right - 94, modal.Y + 14, 72, 28);
        if (close.Contains(_mouse.Position))
        {
            _showUnlockLevelSelector = false;
            return;
        }

        int y = modal.Y + 70;
        foreach (LevelSelectorEntry entry in _levelSelectorEntries.Take(12))
        {
            Rectangle row = new(modal.X + 24, y, modal.Width - 48, 42);
            if (row.Contains(_mouse.Position))
            {
                AddUnlockLevelId(entry.LevelId);
                _showUnlockLevelSelector = false;
                return;
            }

            y += 50;
        }
    }

    private void UpdateSpeakerSelector()
    {
        if (Pressed(Keys.Escape))
        {
            _showSpeakerSelector = false;
            return;
        }

        if (!LeftPressed())
            return;

        Rectangle modal = GetOpenListBounds();
        Rectangle close = new(modal.Right - 94, modal.Y + 14, 72, 28);
        if (close.Contains(_mouse.Position))
        {
            _showSpeakerSelector = false;
            return;
        }

        LevelNodeData selectedNode = SelectedNode;
        if (selectedNode == null || selectedNode.Kind != LevelNodeKind.Dialogue)
        {
            _showSpeakerSelector = false;
            return;
        }

        int y = modal.Y + 70;
        foreach (SpeakerSelectorEntry entry in _speakerSelectorEntries)
        {
            Rectangle row = new(modal.X + 24, y, modal.Width - 48, 42);
            if (row.Contains(_mouse.Position))
            {
                selectedNode.Speaker = entry.Speaker;
                _document.MarkDirty();
                _showSpeakerSelector = false;
                _status = $"Speaker set to {entry.DisplayName}.";
                return;
            }

            y += 50;
        }
    }

    private void UpdateMiniGameSelector()
    {
        if (Pressed(Keys.Escape))
        {
            _showMiniGameSelector = false;
            return;
        }

        if (!LeftPressed())
            return;

        Rectangle modal = GetOpenListBounds();
        Rectangle close = new(modal.Right - 94, modal.Y + 14, 72, 28);
        if (close.Contains(_mouse.Position))
        {
            _showMiniGameSelector = false;
            return;
        }

        LevelNodeData selectedNode = SelectedNode;
        if (selectedNode == null || selectedNode.Kind != LevelNodeKind.SetMiniGame)
        {
            _showMiniGameSelector = false;
            return;
        }

        int y = modal.Y + 70;
        foreach (MiniGameSelectorEntry entry in _miniGameSelectorEntries)
        {
            Rectangle row = new(modal.X + 24, y, modal.Width - 48, 42);
            if (row.Contains(_mouse.Position))
            {
                selectedNode.MiniGameId = entry.MiniGameId;
                _document.MarkDirty();
                _showMiniGameSelector = false;
                _status = $"Mini-game set to {entry.DisplayName}.";
                return;
            }

            y += 50;
        }
    }

    private void DrawTopBar(SpriteBatch spriteBatch)
    {
        Rectangle topBar = GetTopBarBounds();
        _ui.Fill(spriteBatch, topBar, new Color(12, 20, 32, 255));
        _ui.Stroke(spriteBatch, topBar, new Color(74, 108, 158), 2);

        int x = topBar.X + 12;
        DrawButton(spriteBatch, new Rectangle(x, 8, 96, 32), "NEW", Color.LightBlue);
        x += 106;
        DrawButton(spriteBatch, new Rectangle(x, 8, 100, 32), "OPEN", Color.LightBlue);
        x += 110;
        DrawButton(spriteBatch, new Rectangle(x, 8, 84, 32), "SAVE", Color.LightGreen);
        x += 94;
        DrawButton(spriteBatch, new Rectangle(x, 8, 92, 32), "RELOAD", Color.LightBlue);
        x += 102;
        DrawButton(spriteBatch, new Rectangle(x, 8, 84, 32), "PLAY", Color.LightGreen);
        x += 94;
        DrawButton(spriteBatch, new Rectangle(x, 8, 84, 32), "MENU", Color.Orange);

        string dirty = _document.IsDirty ? "*" : string.Empty;
        _ui.Label(spriteBatch, $"LEVEL EDITOR {dirty}", new Vector2(topBar.Right - 330, 15), Color.White, 3);
    }

    private void DrawPalette(SpriteBatch spriteBatch)
    {
        Rectangle palette = GetPaletteBounds();
        _ui.Fill(spriteBatch, palette, new Color(10, 12, 24, 255));
        _ui.Stroke(spriteBatch, palette, new Color(74, 108, 158), 2);
        _ui.Label(spriteBatch, "NODE PALETTE", new Vector2(palette.X + 16, palette.Y + 18), Color.LightBlue, 3);

        int y = palette.Y + 62;
        DrawButton(spriteBatch, new Rectangle(palette.X + 16, y, palette.Width - 32, 36), "TEXTBOX", GetNodeColor(LevelNodeKind.Dialogue));
        y += 46;
        DrawButton(spriteBatch, new Rectangle(palette.X + 16, y, palette.Width - 32, 36), "TRAINING BEATMAP", GetNodeColor(LevelNodeKind.TrainingBeatmap));
        y += 46;
        DrawButton(spriteBatch, new Rectangle(palette.X + 16, y, palette.Width - 32, 36), "PLAY REPRESENT.", GetNodeColor(LevelNodeKind.PlayRepresentationBeatmap));
        y += 46;
        DrawButton(spriteBatch, new Rectangle(palette.X + 16, y, palette.Width - 32, 36), "SET MINIGAME", GetNodeColor(LevelNodeKind.SetMiniGame));
        y += 46;
        DrawButton(spriteBatch, new Rectangle(palette.X + 16, y, palette.Width - 32, 36), "END LEVEL", GetNodeColor(LevelNodeKind.End));

        _ui.Label(spriteBatch, "Drag from output", new Vector2(palette.X + 16, palette.Bottom - 96), Color.White * 0.7f, 2);
        _ui.Label(spriteBatch, "ports to connect.", new Vector2(palette.X + 16, palette.Bottom - 70), Color.White * 0.7f, 2);
        _ui.Label(spriteBatch, "Middle drag pans.", new Vector2(palette.X + 16, palette.Bottom - 44), Color.White * 0.7f, 2);
    }

    private void DrawCanvas(SpriteBatch spriteBatch)
    {
        Rectangle canvas = GetCanvasBounds();
        _ui.Fill(spriteBatch, canvas, new Color(16, 18, 30, 255));
        DrawGrid(spriteBatch, canvas);
        _ui.Label(spriteBatch, $"PAN {MathF.Round(_canvasPan.X)}, {MathF.Round(_canvasPan.Y)}", new Vector2(canvas.X + 12, canvas.Y + 12), Color.White * 0.45f, 2);

        foreach (LevelConnectionData connection in _document.Level.Connections)
            DrawConnection(spriteBatch, connection);

        foreach (LevelNodeData node in _document.Level.Nodes)
            DrawNode(spriteBatch, node);

        if (_wireFromNodeId != null)
        {
            LevelNodeData node = _document.FindNode(_wireFromNodeId);
            if (node != null)
            {
                Vector2 from = GetOutputPortCenter(node, _wireFromPort);
                _ui.Line(spriteBatch, from, _mouse.Position.ToVector2(), Color.White, 3f);
            }
        }
    }

    private void DrawInspector(SpriteBatch spriteBatch)
    {
        Rectangle inspector = GetInspectorBounds();
        _ui.Fill(spriteBatch, inspector, new Color(10, 12, 24, 255));
        _ui.Stroke(spriteBatch, inspector, new Color(74, 108, 158), 2);
        _ui.Label(spriteBatch, "INSPECTOR", new Vector2(inspector.X + 14, inspector.Y + 18), Color.LightBlue, 3);

        LevelNodeData selectedNode = SelectedNode;
        if (selectedNode == null)
        {
            _ui.Label(spriteBatch, "NO NODE SELECTED", new Vector2(inspector.X + 14, inspector.Y + 80), Color.White * 0.7f, 2);
            return;
        }

        int titleY = inspector.Y + 62;
        _ui.Label(spriteBatch, GetNodeTitle(selectedNode.Kind), new Vector2(inspector.X + 14, titleY), GetNodeColor(selectedNode.Kind), 3);
        _ui.Label(spriteBatch, $"ID {ShortId(selectedNode.Id)}", new Vector2(inspector.X + 14, titleY + 32), Color.White * 0.55f, 2);
        int y = GetInspectorPropertyStartY(inspector);

        if (selectedNode.Kind == LevelNodeKind.Start)
        {
            DrawLabel(spriteBatch, "LEVEL DISPLAY NAME", inspector.X + 14, y);
            DrawTextField(spriteBatch, new Rectangle(inspector.X + 14, y + 18, inspector.Width - 28, 30), "level.display");
            y += 60;

            Rectangle lockedBox = new(inspector.X + 14, y + 4, 18, 18);
            _ui.Stroke(spriteBatch, lockedBox, Color.LightGreen, 2);
            if (_document.Level.LockedByDefault)
                _ui.Fill(spriteBatch, new Rectangle(lockedBox.X + 5, lockedBox.Y + 5, 8, 8), Color.LightGreen);
            _ui.Label(spriteBatch, "LOCKED BY DEFAULT", new Vector2(inspector.X + 42, y + 6), Color.White, 2);
            y += 42;

            DrawLabel(spriteBatch, "UNLOCK LEVEL UUIDS", inspector.X + 14, y);
            DrawTextField(spriteBatch, new Rectangle(inspector.X + 14, y + 18, inspector.Width - 28, 30), "level.unlocks");
            y += 58;
            DrawButton(spriteBatch, new Rectangle(inspector.X + 14, y, inspector.Width - 28, 34), "SELECT LEVEL UUID", Color.LightBlue);
            y += 48;
        }
        else if (selectedNode.Kind == LevelNodeKind.Dialogue)
        {
            DrawLabel(spriteBatch, "SPEAKER", inspector.X + 14, y);
            Rectangle speakerValue = new(inspector.X + 14, y + 18, inspector.Width - 110, 34);
            _ui.Fill(spriteBatch, speakerValue, LevelSpeakerInfo.GetTextboxColor(selectedNode.Speaker) * 0.65f);
            _ui.Stroke(spriteBatch, speakerValue, Color.White * 0.6f, 1);
            _ui.Label(spriteBatch, Truncate(LevelSpeakerInfo.GetDisplayName(selectedNode.Speaker), 24), new Vector2(speakerValue.X + 8, speakerValue.Y + 10), Color.White, 2);
            DrawButton(spriteBatch, new Rectangle(inspector.Right - 88, y + 18, 74, 34), "SELECT", Color.LightBlue);
            y += 60;

            DrawLabel(spriteBatch, "TEXTBOX TEXT", inspector.X + 14, y);
            string textFieldKey = $"node.text:{selectedNode.Id}";
            int textFieldHeight = GetTextFieldHeight(textFieldKey, inspector.Width - 28);
            DrawTextField(spriteBatch, new Rectangle(inspector.X + 14, y + 18, inspector.Width - 28, textFieldHeight), textFieldKey);
            y += textFieldHeight + 34;
            if (y + 116 <= inspector.Bottom - 12)
                DrawDialoguePreview(spriteBatch, inspector, selectedNode, y);
        }
        else if (selectedNode.Kind == LevelNodeKind.TrainingBeatmap || selectedNode.Kind == LevelNodeKind.PlayRepresentationBeatmap)
        {
            DrawLabel(spriteBatch, "CHART PATH", inspector.X + 14, y);
            DrawChartPathValue(spriteBatch, new Rectangle(inspector.X + 14, y + 18, inspector.Width - 110, 30), selectedNode.ChartPath);
            DrawButton(spriteBatch, new Rectangle(inspector.Right - 88, y + 18, 74, 30), "SELECT", Color.LightBlue);
            y += 60;

            if (selectedNode.Kind == LevelNodeKind.TrainingBeatmap)
            {
                DrawLabel(spriteBatch, "REQUIRED SUCCESSES", inspector.X + 14, y);
                DrawButton(spriteBatch, new Rectangle(inspector.X + 14, y + 18, 34, 30), "-", Color.LightBlue);
                DrawButton(spriteBatch, new Rectangle(inspector.Right - 48, y + 18, 34, 30), "+", Color.LightBlue);
                Rectangle value = new(inspector.X + 58, y + 18, inspector.Width - 120, 30);
                _ui.Fill(spriteBatch, value, new Color(22, 28, 44));
                _ui.Stroke(spriteBatch, value, Color.White * 0.45f, 1);
                _ui.Label(spriteBatch, selectedNode.RequiredSuccessCount.ToString(), new Vector2(value.X + 10, value.Y + 8), Color.White, 2);
                y += 60;
            }
        }
        else if (selectedNode.Kind == LevelNodeKind.SetMiniGame)
        {
            DrawLabel(spriteBatch, "MINIGAME ID", inspector.X + 14, y);
            DrawTextField(spriteBatch, new Rectangle(inspector.X + 14, y + 18, inspector.Width - 110, 30), $"node.minigame:{selectedNode.Id}");
            DrawButton(spriteBatch, new Rectangle(inspector.Right - 88, y + 18, 74, 30), "SELECT", Color.LightBlue);
            y += 60;
        }
        else
        {
            _ui.Label(spriteBatch, "No editable properties.", new Vector2(inspector.X + 14, y + 10), Color.White * 0.65f, 2);
            y += 48;
        }

        DrawConnectionSummary(spriteBatch, inspector, selectedNode, y + 14);
    }

    private void DrawStatusBar(SpriteBatch spriteBatch)
    {
        Rectangle status = GetStatusBarBounds();
        _ui.Fill(spriteBatch, status, new Color(12, 20, 32, 255));
        _ui.Stroke(spriteBatch, status, new Color(74, 108, 158), 2);
        _ui.Label(spriteBatch, Truncate(_status, 70), new Vector2(status.X + 14, status.Y + 12), Color.LightGreen, 2);
        _ui.Label(spriteBatch, Truncate(_document.FilePath, 72), new Vector2(status.Right - 620, status.Y + 12), Color.White * 0.65f, 2);
    }

    private void DrawOpenList(SpriteBatch spriteBatch)
    {
        Viewport viewport = _graphicsDevice.Viewport;
        _ui.Fill(spriteBatch, new Rectangle(0, 0, viewport.Width, viewport.Height), Color.Black * 0.55f);
        Rectangle modal = GetOpenListBounds();
        _ui.Fill(spriteBatch, modal, new Color(8, 12, 22, 245));
        _ui.Stroke(spriteBatch, modal, Color.LightBlue, 3);
        _ui.Label(spriteBatch, "OPEN LEVEL", new Vector2(modal.X + 24, modal.Y + 22), Color.LightBlue, 3);
        DrawButton(spriteBatch, new Rectangle(modal.Right - 94, modal.Y + 14, 72, 28), "CLOSE", Color.Orange);

        if (_openLevelFiles.Count == 0)
        {
            _ui.Label(spriteBatch, "NO LEVELS FOUND", new Vector2(modal.X + 24, modal.Y + 84), Color.White, 3);
            return;
        }

        int y = modal.Y + 70;
        foreach (string levelFile in _openLevelFiles.Take(12))
        {
            Rectangle row = new(modal.X + 24, y, modal.Width - 48, 34);
            _ui.Fill(spriteBatch, row, row.Contains(_mouse.Position) ? new Color(38, 54, 78) : new Color(22, 28, 44));
            _ui.Stroke(spriteBatch, row, Color.White * 0.3f, 1);
            _ui.Label(spriteBatch, Truncate(levelFile, 62), new Vector2(row.X + 10, row.Y + 10), Color.White, 2);
            y += 42;
        }
    }

    private void DrawUnlockLevelSelector(SpriteBatch spriteBatch)
    {
        Viewport viewport = _graphicsDevice.Viewport;
        _ui.Fill(spriteBatch, new Rectangle(0, 0, viewport.Width, viewport.Height), Color.Black * 0.55f);
        Rectangle modal = GetOpenListBounds();
        _ui.Fill(spriteBatch, modal, new Color(8, 12, 22, 245));
        _ui.Stroke(spriteBatch, modal, Color.LightBlue, 3);
        _ui.Label(spriteBatch, "SELECT LEVEL UUID", new Vector2(modal.X + 24, modal.Y + 22), Color.LightBlue, 3);
        DrawButton(spriteBatch, new Rectangle(modal.Right - 94, modal.Y + 14, 72, 28), "CLOSE", Color.Orange);

        if (_levelSelectorEntries.Count == 0)
        {
            _ui.Label(spriteBatch, "NO LEVELS FOUND", new Vector2(modal.X + 24, modal.Y + 84), Color.White, 3);
            return;
        }

        int y = modal.Y + 70;
        foreach (LevelSelectorEntry entry in _levelSelectorEntries.Take(12))
        {
            bool alreadySelected = _document.Level.UnlockLevelIds.Any(id => string.Equals(id, entry.LevelId, StringComparison.OrdinalIgnoreCase));
            Rectangle row = new(modal.X + 24, y, modal.Width - 48, 42);
            Color fill = alreadySelected ? new Color(30, 52, 36) : new Color(22, 28, 44);
            _ui.Fill(spriteBatch, row, row.Contains(_mouse.Position) ? new Color(38, 54, 78) : fill);
            _ui.Stroke(spriteBatch, row, alreadySelected ? Color.LightGreen : Color.White * 0.3f, 1);
            _ui.Label(spriteBatch, Truncate(entry.DisplayName, 34), new Vector2(row.X + 10, row.Y + 8), Color.White, 2);
            _ui.Label(spriteBatch, ShortId(entry.LevelId), new Vector2(row.Right - 108, row.Y + 8), alreadySelected ? Color.LightGreen : Color.White * 0.55f, 2);
            y += 50;
        }
    }

    private void DrawSpeakerSelector(SpriteBatch spriteBatch)
    {
        Viewport viewport = _graphicsDevice.Viewport;
        _ui.Fill(spriteBatch, new Rectangle(0, 0, viewport.Width, viewport.Height), Color.Black * 0.55f);
        Rectangle modal = GetOpenListBounds();
        _ui.Fill(spriteBatch, modal, new Color(8, 12, 22, 245));
        _ui.Stroke(spriteBatch, modal, Color.LightBlue, 3);
        _ui.Label(spriteBatch, "SELECT SPEAKER", new Vector2(modal.X + 24, modal.Y + 22), Color.LightBlue, 3);
        DrawButton(spriteBatch, new Rectangle(modal.Right - 94, modal.Y + 14, 72, 28), "CLOSE", Color.Orange);

        LevelSpeaker currentSpeaker = SelectedNode?.Speaker ?? LevelSpeaker.TwilightSparkle;
        int y = modal.Y + 70;
        foreach (SpeakerSelectorEntry entry in _speakerSelectorEntries)
        {
            bool selected = entry.Speaker == currentSpeaker;
            Rectangle row = new(modal.X + 24, y, modal.Width - 48, 42);
            Color color = LevelSpeakerInfo.GetTextboxColor(entry.Speaker);
            _ui.Fill(spriteBatch, row, row.Contains(_mouse.Position) ? new Color(38, 54, 78) : color * 0.35f);
            _ui.Stroke(spriteBatch, row, selected ? Color.LightGreen : color, selected ? 3 : 1);
            _ui.Fill(spriteBatch, new Rectangle(row.X + 10, row.Y + 8, 26, 26), color);
            _ui.Label(spriteBatch, entry.DisplayName, new Vector2(row.X + 48, row.Y + 12), Color.White, 2);
            if (selected)
                _ui.Label(spriteBatch, "SELECTED", new Vector2(row.Right - 120, row.Y + 12), Color.LightGreen, 2);
            y += 50;
        }
    }

    private void DrawMiniGameSelector(SpriteBatch spriteBatch)
    {
        Viewport viewport = _graphicsDevice.Viewport;
        _ui.Fill(spriteBatch, new Rectangle(0, 0, viewport.Width, viewport.Height), Color.Black * 0.55f);
        Rectangle modal = GetOpenListBounds();
        _ui.Fill(spriteBatch, modal, new Color(8, 12, 22, 245));
        _ui.Stroke(spriteBatch, modal, Color.LightBlue, 3);
        _ui.Label(spriteBatch, "SELECT MINIGAME", new Vector2(modal.X + 24, modal.Y + 22), Color.LightBlue, 3);
        DrawButton(spriteBatch, new Rectangle(modal.Right - 94, modal.Y + 14, 72, 28), "CLOSE", Color.Orange);

        if (_miniGameSelectorEntries.Count == 0)
        {
            _ui.Label(spriteBatch, "NO MINIGAMES FOUND", new Vector2(modal.X + 24, modal.Y + 84), Color.White, 3);
            return;
        }

        string currentMiniGameId = SelectedNode?.MiniGameId ?? string.Empty;
        int y = modal.Y + 70;
        foreach (MiniGameSelectorEntry entry in _miniGameSelectorEntries)
        {
            bool selected = string.Equals(entry.MiniGameId, currentMiniGameId, StringComparison.OrdinalIgnoreCase);
            Rectangle row = new(modal.X + 24, y, modal.Width - 48, 42);
            _ui.Fill(spriteBatch, row, row.Contains(_mouse.Position) ? new Color(38, 54, 78) : new Color(22, 28, 44));
            _ui.Stroke(spriteBatch, row, selected ? Color.LightGreen : Color.White * 0.3f, selected ? 3 : 1);
            _ui.Label(spriteBatch, Truncate(entry.DisplayName, 42), new Vector2(row.X + 10, row.Y + 8), Color.White, 2);
            _ui.Label(spriteBatch, entry.MiniGameId, new Vector2(row.Right - 220, row.Y + 8), selected ? Color.LightGreen : Color.White * 0.55f, 2);
            y += 50;
        }
    }

    private void DrawNode(SpriteBatch spriteBatch, LevelNodeData node)
    {
        Rectangle bounds = GetNodeBounds(node);
        Color nodeColor = GetNodeColor(node.Kind);
        bool selected = string.Equals(node.Id, _selectedNodeId, StringComparison.Ordinal);

        _ui.Fill(spriteBatch, new Rectangle(bounds.X + 5, bounds.Y + 5, bounds.Width, bounds.Height), Color.Black * 0.35f);
        _ui.Fill(spriteBatch, bounds, new Color(18, 24, 38, 245));
        _ui.Fill(spriteBatch, new Rectangle(bounds.X, bounds.Y, bounds.Width, 24), nodeColor * 0.9f);
        _ui.Stroke(spriteBatch, bounds, selected ? Color.White : nodeColor, selected ? 3 : 2);
        _ui.Label(spriteBatch, GetNodeTitle(node.Kind), new Vector2(bounds.X + 10, bounds.Y + 8), Color.White, 2);

        string detail = GetNodeDetail(node);
        if (!string.IsNullOrWhiteSpace(detail))
            _ui.Label(spriteBatch, Truncate(detail, 22), new Vector2(bounds.X + 10, bounds.Y + 42), Color.White * 0.75f, 2);

        DrawInputPort(spriteBatch, node);
        foreach (string port in LevelDocument.GetOutputPorts(node.Kind))
            DrawOutputPort(spriteBatch, node, port);
    }

    private void DrawInputPort(SpriteBatch spriteBatch, LevelNodeData node)
    {
        if (node.Kind == LevelNodeKind.Start)
            return;

        Rectangle port = GetInputPortBounds(node);
        _ui.Fill(spriteBatch, port, Color.Black);
        _ui.Stroke(spriteBatch, port, Color.White, 2);
    }

    private void DrawOutputPort(SpriteBatch spriteBatch, LevelNodeData node, string portName)
    {
        Rectangle port = GetOutputPortBounds(node, portName);
        _ui.Fill(spriteBatch, port, GetPortColor(portName));
        _ui.Stroke(spriteBatch, port, Color.White, 1);
        _ui.Label(spriteBatch, portName.ToUpperInvariant(), new Vector2(port.Right + 5, port.Y + 2), Color.White * 0.7f, 1);
    }

    private void DrawConnection(SpriteBatch spriteBatch, LevelConnectionData connection)
    {
        LevelNodeData from = _document.FindNode(connection.FromNodeId);
        LevelNodeData to = _document.FindNode(connection.ToNodeId);
        if (from == null || to == null)
            return;

        Vector2 start = GetOutputPortCenter(from, connection.FromPort);
        Vector2 end = GetInputPortCenter(to);
        Color color = GetPortColor(connection.FromPort);
        _ui.Line(spriteBatch, start, new Vector2(start.X + 40, start.Y), color, 3f);
        _ui.Line(spriteBatch, new Vector2(start.X + 40, start.Y), new Vector2(end.X - 40, end.Y), color, 3f);
        _ui.Line(spriteBatch, new Vector2(end.X - 40, end.Y), end, color, 3f);
    }

    private void DrawGrid(SpriteBatch spriteBatch, Rectangle canvas)
    {
        Color gridColor = new Color(255, 255, 255, 18);
        int xOffset = PositiveModulo((int)MathF.Round(_canvasPan.X), 32);
        int yOffset = PositiveModulo((int)MathF.Round(_canvasPan.Y), 32);
        for (int x = canvas.X + xOffset; x < canvas.Right; x += 32)
            _ui.Line(spriteBatch, new Vector2(x, canvas.Y), new Vector2(x, canvas.Bottom), gridColor, 1f);
        for (int y = canvas.Y + yOffset; y < canvas.Bottom; y += 32)
            _ui.Line(spriteBatch, new Vector2(canvas.X, y), new Vector2(canvas.Right, y), gridColor, 1f);
    }

    private void DrawDialoguePreview(SpriteBatch spriteBatch, Rectangle inspector, LevelNodeData node, int top)
    {
        Rectangle preview = new(inspector.X + 14, top, inspector.Width - 28, 116);
        Color speakerColor = LevelSpeakerInfo.GetTextboxColor(node.Speaker);
        _ui.Fill(spriteBatch, preview, speakerColor * 0.85f);
        _ui.Stroke(spriteBatch, preview, Color.White, 2);
        _ui.Label(spriteBatch, "PREVIEW", new Vector2(preview.X + 12, preview.Y + 12), Color.White, 2);
        _ui.Label(spriteBatch, Truncate(LevelSpeakerInfo.GetDisplayName(node.Speaker), 24), new Vector2(preview.X + 12, preview.Y + 40), Color.White, 2);
        int y = preview.Y + 70;
        foreach (string line in WrapDevUiText(node.Text, preview.Width - 24))
        {
            _ui.Label(spriteBatch, line, new Vector2(preview.X + 12, y), Color.White, 2);
            y += 20;
            if (y > preview.Bottom - 18)
                break;
        }
    }

    private void DrawConnectionSummary(SpriteBatch spriteBatch, Rectangle inspector, LevelNodeData node, int y)
    {
        _ui.Line(spriteBatch, new Vector2(inspector.X + 14, y), new Vector2(inspector.Right - 14, y), Color.DarkSlateGray, 2);
        y += 14;
        _ui.Label(spriteBatch, "OUTPUTS", new Vector2(inspector.X + 14, y), Color.LightBlue, 2);
        y += 28;

        IReadOnlyList<string> ports = LevelDocument.GetOutputPorts(node.Kind);
        if (ports.Count == 0)
        {
            _ui.Label(spriteBatch, "No graph outputs.", new Vector2(inspector.X + 14, y), Color.White * 0.65f, 2);
            return;
        }

        foreach (string port in ports)
        {
            string targetId = _document.GetConnectionTarget(node.Id, port);
            LevelNodeData target = _document.FindNode(targetId);
            string targetText = target == null ? "not connected" : $"{GetNodeTitle(target.Kind)} {ShortId(target.Id)}";
            _ui.Label(spriteBatch, $"{port}: {targetText}", new Vector2(inspector.X + 14, y), Color.White * 0.8f, 2);
            y += 26;
        }
    }

    private void DrawLabel(SpriteBatch spriteBatch, string text, int x, int y)
    {
        _ui.Label(spriteBatch, text, new Vector2(x, y), Color.LightBlue, 1);
    }

    private void DrawTextField(SpriteBatch spriteBatch, Rectangle bounds, string fieldKey)
    {
        bool editing = string.Equals(_editingField, fieldKey, StringComparison.Ordinal);
        string value = editing ? _editingBuffer + "_" : GetFieldValue(fieldKey);
        if (!editing && string.IsNullOrWhiteSpace(value))
            value = "<click to edit>";
        _ui.Fill(spriteBatch, bounds, editing ? new Color(28, 42, 58) : new Color(22, 28, 44));
        _ui.Stroke(spriteBatch, bounds, editing ? Color.LightGreen : Color.White * 0.45f, editing ? 2 : 1);

        Color textColor = editing || value != "<click to edit>" ? Color.White : Color.White * 0.45f;
        if (bounds.Height <= 34)
        {
            _ui.Label(spriteBatch, Truncate(value, Math.Max(4, bounds.Width / 9)), new Vector2(bounds.X + 8, bounds.Y + 8), textColor, 2);
            return;
        }

        int y = bounds.Y + 8;
        foreach (string line in WrapDevUiText(value, bounds.Width - 16))
        {
            _ui.Label(spriteBatch, line, new Vector2(bounds.X + 8, y), textColor, 2);
            y += 20;
        }
    }

    private void DrawChartPathValue(SpriteBatch spriteBatch, Rectangle bounds, string chartPath)
    {
        string value = string.IsNullOrWhiteSpace(chartPath) ? "<select beatmap>" : BeatmapPackagePaths.GetChartDisplayName(chartPath);
        bool empty = string.IsNullOrWhiteSpace(chartPath);
        _ui.Fill(spriteBatch, bounds, new Color(22, 28, 44));
        _ui.Stroke(spriteBatch, bounds, bounds.Contains(_mouse.Position) ? Color.LightGreen : Color.White * 0.45f, bounds.Contains(_mouse.Position) ? 2 : 1);
        _ui.Label(spriteBatch, Truncate(value, Math.Max(4, bounds.Width / 9)), new Vector2(bounds.X + 8, bounds.Y + 8), empty ? Color.White * 0.45f : Color.White, 2);
    }

    private void DrawButton(SpriteBatch spriteBatch, Rectangle bounds, string label, Color color)
    {
        bool hover = bounds.Contains(_mouse.Position);
        _ui.Fill(spriteBatch, bounds, hover ? color * 0.42f : color * 0.24f);
        _ui.Stroke(spriteBatch, bounds, color, hover ? 2 : 1);
        int labelX = bounds.Center.X - GetTextWidth(label, 2) / 2;
        _ui.Label(spriteBatch, label, new Vector2(labelX, bounds.Y + 10), Color.White, 2);
    }

    private bool TryConsumeButton(Rectangle bounds, Action action)
    {
        if (!bounds.Contains(_mouse.Position))
            return false;

        action?.Invoke();
        return true;
    }

    private bool TryBeginTextField(Rectangle bounds, string fieldKey)
    {
        if (!bounds.Contains(_mouse.Position))
            return false;

        BeginTextEdit(fieldKey);
        return true;
    }

    private void NewLevel()
    {
        _document = LevelDocument.CreateNewPackage("New Level");
        _selectedNodeId = _document.Level.StartNodeId;
        _status = "New level created. Save to write level.xml.";
    }

    private void OpenLevelList()
    {
        _openLevelFiles = LevelDocument.DiscoverLevelFiles();
        _showOpenList = true;
    }

    private void OpenUnlockLevelSelector()
    {
        _levelSelectorEntries = DiscoverLevelSelectorEntries();
        _showUnlockLevelSelector = true;
        _status = _levelSelectorEntries.Count == 0
            ? "No Levels/*/level.xml files found."
            : "Select a level to append its UUID.";
    }

    private void OpenSpeakerSelector()
    {
        CommitEditing();
        _speakerSelectorEntries = LevelSpeakerInfo.All
            .Select(speaker => new SpeakerSelectorEntry(speaker, LevelSpeakerInfo.GetDisplayName(speaker)))
            .ToArray();
        _showSpeakerSelector = true;
        _status = "Select a textbox speaker.";
    }

    private void OpenMiniGameSelector()
    {
        CommitEditing();
        _miniGameSelectorEntries = DiscoverMiniGameEntries();
        _showMiniGameSelector = true;
        _status = _miniGameSelectorEntries.Count == 0
            ? "No mini-game scenes found."
            : "Select a mini-game scene.";
    }

    private void OpenBeatmapSelector(LevelNodeData node)
    {
        if (node == null)
            return;

        CommitEditing();
        _beatmapSelectorNodeId = node.Id;
        _beatmapSelector.OpenSelectBeatmap(
            node.Kind == LevelNodeKind.TrainingBeatmap ? "SELECT TRAINING BEATMAP" : "SELECT FINAL BEATMAP",
            node.ChartPath,
            SelectBeatmapForNode,
            () => _status = "Beatmap selection cancelled.",
            status => _status = status);
    }

    private void SelectBeatmapForNode(string chartPath)
    {
        LevelNodeData node = _document.FindNode(_beatmapSelectorNodeId);
        if (node == null)
            return;

        node.ChartPath = NormalizePathForDisplay(chartPath);
        _document.MarkDirty();
        _status = $"Beatmap set to {BeatmapPackagePaths.GetChartDisplayName(node.ChartPath)}.";
    }

    private void AddUnlockLevelId(string levelId)
    {
        if (string.IsNullOrWhiteSpace(levelId))
            return;

        _document.Level.UnlockLevelIds ??= new List<string>();
        if (_document.Level.UnlockLevelIds.Any(id => string.Equals(id, levelId, StringComparison.OrdinalIgnoreCase)))
        {
            _status = "Level UUID already present.";
            return;
        }

        _document.Level.UnlockLevelIds.Add(levelId);
        _document.MarkDirty();
        _status = $"Added unlock UUID {ShortId(levelId)}.";
    }

    private void LoadLevel(string path)
    {
        _document = LevelDocument.LoadOrCreate(path);
        _selectedNodeId = _document.Level.StartNodeId;
        _status = $"Loaded {path}.";
    }

    private void SaveLevel()
    {
        try
        {
            _document.Save();
            _status = "Saved.";
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is InvalidOperationException)
        {
            _status = "Save failed: " + ex.Message + " | " + Path.GetFullPath(_document.FilePath);
        }
    }

    private void ReloadLevel()
    {
        try
        {
            _document.Reload();
            _selectedNodeId = _document.Level.StartNodeId;
            _status = "Reloaded.";
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is InvalidOperationException)
        {
            _status = "Reload failed: " + ex.Message;
        }
    }

    private void PlayLevel()
    {
        try
        {
            _document.Save();
            _playLevel(_document);
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is InvalidOperationException)
        {
            _status = "Play failed: " + ex.Message + " | " + Path.GetFullPath(_document.FilePath);
        }
    }

    private void AddNode(LevelNodeKind kind)
    {
        Rectangle canvas = GetCanvasBounds();
        int count = _document.Level.Nodes.Count;
        Vector2 worldCenter = ScreenToCanvas(new Point(canvas.Center.X, canvas.Center.Y));
        LevelNodeData node = _document.AddNode(
            kind,
            (int)MathF.Round(worldCenter.X - NodeWidth / 2f + count * 24),
            (int)MathF.Round(worldCenter.Y - NodeHeight / 2f + count * 24));
        if (kind == LevelNodeKind.TrainingBeatmap || kind == LevelNodeKind.PlayRepresentationBeatmap)
            node.ChartPath = BeatmapPackagePaths.DiscoverBeatmapCharts().FirstOrDefault() ?? string.Empty;
        if (kind == LevelNodeKind.SetMiniGame)
            node.MiniGameId = DiscoverMiniGameIds().FirstOrDefault() ?? string.Empty;
        _selectedNodeId = node.Id;
        _status = $"Added {GetNodeTitle(kind)} node.";
    }

    private bool HandleClipboardShortcuts()
    {
        if (!IsControlDown())
            return false;

        if (Pressed(Keys.C))
        {
            CopySelectedNode();
            return true;
        }

        if (Pressed(Keys.X))
        {
            CutSelectedNode();
            return true;
        }

        if (Pressed(Keys.V))
        {
            PasteClipboardNode();
            return true;
        }

        return false;
    }

    private bool CopySelectedNode()
    {
        LevelNodeData selectedNode = SelectedNode;
        if (selectedNode == null)
        {
            _status = "No node selected to copy.";
            return false;
        }

        if (selectedNode.Kind == LevelNodeKind.Start)
        {
            _status = "Start node cannot be copied.";
            return false;
        }

        _clipboardNode = CloneNodeForClipboard(selectedNode);
        _clipboardPasteSerial = 0;
        _status = $"Copied {GetNodeTitle(selectedNode.Kind)}.";
        return true;
    }

    private void CutSelectedNode()
    {
        LevelNodeData selectedNode = SelectedNode;
        if (selectedNode == null)
        {
            _status = "No node selected to cut.";
            return;
        }

        if (selectedNode.Kind == LevelNodeKind.Start)
        {
            _status = "Start node cannot be cut.";
            return;
        }

        if (!CopySelectedNode())
            return;

        if (_document.DeleteNode(selectedNode.Id))
        {
            _selectedNodeId = _document.Level.StartNodeId;
            _status = $"Cut {GetNodeTitle(selectedNode.Kind)}.";
        }
    }

    private void PasteClipboardNode()
    {
        if (_clipboardNode == null)
        {
            _status = "No copied node to paste.";
            return;
        }

        _clipboardPasteSerial++;
        Vector2 position = GetPastePosition(_clipboardNode, _clipboardPasteSerial);
        LevelNodeData pastedNode = _document.AddNode(
            _clipboardNode.Kind,
            (int)MathF.Round(position.X),
            (int)MathF.Round(position.Y));
        CopyNodeProperties(_clipboardNode, pastedNode);
        _selectedNodeId = pastedNode.Id;
        _status = $"Pasted {GetNodeTitle(pastedNode.Kind)}.";
    }

    private Vector2 GetPastePosition(LevelNodeData sourceNode, int pasteSerial)
    {
        Rectangle canvas = GetCanvasBounds();
        if (canvas.Contains(_mouse.Position))
        {
            Vector2 mouseWorld = ScreenToCanvas(_mouse.Position);
            return new Vector2(mouseWorld.X - NodeWidth / 2f, mouseWorld.Y - NodeHeight / 2f);
        }

        int offset = 42 * pasteSerial;
        return new Vector2(sourceNode.X + offset, sourceNode.Y + offset);
    }

    private static LevelNodeData CloneNodeForClipboard(LevelNodeData sourceNode)
    {
        LevelNodeData clone = new()
        {
            Kind = sourceNode.Kind,
            X = sourceNode.X,
            Y = sourceNode.Y
        };
        CopyNodeProperties(sourceNode, clone);
        return clone;
    }

    private static void CopyNodeProperties(LevelNodeData sourceNode, LevelNodeData targetNode)
    {
        targetNode.Speaker = sourceNode.Speaker;
        targetNode.Text = sourceNode.Text ?? string.Empty;
        targetNode.ChartPath = sourceNode.ChartPath ?? string.Empty;
        targetNode.MiniGameId = sourceNode.MiniGameId ?? string.Empty;
        targetNode.RequiredSuccessCount = Math.Max(1, sourceNode.RequiredSuccessCount);
    }

    private void DeleteSelectedNode()
    {
        if (_document.DeleteNode(_selectedNodeId))
        {
            _selectedNodeId = _document.Level.StartNodeId;
            _status = "Node deleted.";
        }
    }

    private void CycleSpeaker(LevelNodeData node, int direction)
    {
        IReadOnlyList<LevelSpeaker> speakers = LevelSpeakerInfo.All;
        int index = 0;
        for (int i = 0; i < speakers.Count; i++)
        {
            if (speakers[i] == node.Speaker)
            {
                index = i;
                break;
            }
        }

        node.Speaker = speakers[(index + direction + speakers.Count) % speakers.Count];
        _document.MarkDirty();
    }

    private void CycleChartPath(LevelNodeData node)
    {
        IReadOnlyList<string> charts = BeatmapPackagePaths.DiscoverBeatmapCharts();
        if (charts.Count == 0)
        {
            _status = "No Beatmaps/*/chart.xml files found.";
            return;
        }

        string current = NormalizePathForDisplay(node.ChartPath);
        int index = -1;
        for (int i = 0; i < charts.Count; i++)
        {
            if (string.Equals(charts[i], current, StringComparison.OrdinalIgnoreCase))
            {
                index = i;
                break;
            }
        }

        node.ChartPath = charts[(index + 1 + charts.Count) % charts.Count];
        _document.MarkDirty();
    }

    private void CycleMiniGameId(LevelNodeData node)
    {
        IReadOnlyList<string> miniGameIds = DiscoverMiniGameIds();
        if (miniGameIds.Count == 0)
        {
            _status = "No mini-game providers found.";
            return;
        }

        int index = -1;
        for (int i = 0; i < miniGameIds.Count; i++)
        {
            if (string.Equals(miniGameIds[i], node.MiniGameId, StringComparison.OrdinalIgnoreCase))
            {
                index = i;
                break;
            }
        }

        node.MiniGameId = miniGameIds[(index + 1 + miniGameIds.Count) % miniGameIds.Count];
        _document.MarkDirty();
    }

    private void BeginTextEdit(string fieldKey)
    {
        if (string.Equals(_editingField, fieldKey, StringComparison.Ordinal))
            return;

        CommitEditing();
        _editingField = fieldKey;
        _editingBuffer = GetFieldValue(fieldKey);
        ResetBackspaceRepeat();
        _status = "Editing field. Enter validates, Esc cancels.";
    }

    private void CommitEditing()
    {
        if (_editingField == null)
            return;

        ApplyFieldValue(_editingField, _editingBuffer);
        _editingField = null;
        _editingBuffer = string.Empty;
        ResetBackspaceRepeat();
    }

    private void CancelEditing()
    {
        _editingField = null;
        _editingBuffer = string.Empty;
        ResetBackspaceRepeat();
    }

    private void UpdateTextEditing()
    {
        if (_editingField == null)
            return;

        foreach (Keys key in _keyboard.GetPressedKeys())
        {
            if (_previousKeyboard.IsKeyDown(key))
                continue;

            if (key == Keys.Enter)
            {
                CommitEditing();
                return;
            }

            if (key == Keys.Escape)
            {
                CancelEditing();
                return;
            }

            if (key == Keys.Back)
                continue;

            if (_editingBuffer.Length < 320 && DevUiTextInput.TryGetTypedChar(key, _keyboard, out char c))
                _editingBuffer += c;
        }

        if (DevUiTextInput.ShouldBackspace(_keyboard, _previousKeyboard, ref _backspaceHoldStartMs, ref _backspaceLastRepeatMs) && _editingBuffer.Length > 0)
            _editingBuffer = _editingBuffer[..^1];
    }

    private string GetFieldValue(string fieldKey)
    {
        if (fieldKey == "level.display")
            return _document.Level.DisplayName ?? string.Empty;
        if (fieldKey == "level.unlocks")
            return string.Join(", ", _document.Level.UnlockLevelIds ?? new List<string>());
        if (fieldKey.StartsWith("node.text:", StringComparison.Ordinal))
            return _document.FindNode(fieldKey[10..])?.Text ?? string.Empty;
        if (fieldKey.StartsWith("node.chart:", StringComparison.Ordinal))
            return _document.FindNode(fieldKey[11..])?.ChartPath ?? string.Empty;
        if (fieldKey.StartsWith("node.minigame:", StringComparison.Ordinal))
            return _document.FindNode(fieldKey[14..])?.MiniGameId ?? string.Empty;
        return string.Empty;
    }

    private void ApplyFieldValue(string fieldKey, string value)
    {
        value ??= string.Empty;
        if (fieldKey == "level.display")
        {
            _document.Level.DisplayName = string.IsNullOrWhiteSpace(value) ? "New Level" : value.Trim();
            _document.MarkDirty();
            return;
        }

        if (fieldKey == "level.unlocks")
        {
            _document.Level.UnlockLevelIds = value
                .Split(new[] { ',', ';', ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(id => id.Trim())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            _document.MarkDirty();
            return;
        }

        if (fieldKey.StartsWith("node.text:", StringComparison.Ordinal))
        {
            LevelNodeData node = _document.FindNode(fieldKey[10..]);
            if (node != null)
            {
                node.Text = value;
                _document.MarkDirty();
            }
            return;
        }

        if (fieldKey.StartsWith("node.chart:", StringComparison.Ordinal))
        {
            LevelNodeData node = _document.FindNode(fieldKey[11..]);
            if (node != null)
            {
                node.ChartPath = NormalizePathForDisplay(value);
                _document.MarkDirty();
            }
            return;
        }

        if (fieldKey.StartsWith("node.minigame:", StringComparison.Ordinal))
        {
            LevelNodeData node = _document.FindNode(fieldKey[14..]);
            if (node != null)
            {
                node.MiniGameId = value.Trim();
                _document.MarkDirty();
            }
        }
    }

    private LevelNodeData SelectedNode => _document.FindNode(_selectedNodeId);

    private LevelNodeData GetNodeAt(Point point)
    {
        for (int i = _document.Level.Nodes.Count - 1; i >= 0; i--)
        {
            LevelNodeData node = _document.Level.Nodes[i];
            if (GetNodeBounds(node).Contains(point))
                return node;
        }

        return null;
    }

    private bool TryGetOutputPortAt(Point point, out LevelNodeData node, out string portName)
    {
        for (int i = _document.Level.Nodes.Count - 1; i >= 0; i--)
        {
            node = _document.Level.Nodes[i];
            foreach (string port in LevelDocument.GetOutputPorts(node.Kind))
            {
                if (GetOutputPortBounds(node, port).Contains(point))
                {
                    portName = port;
                    return true;
                }
            }
        }

        node = null;
        portName = null;
        return false;
    }

    private Rectangle GetTopBarBounds()
    {
        return new Rectangle(0, 0, _graphicsDevice.Viewport.Width, TopBarHeight);
    }

    private Rectangle GetPaletteBounds()
    {
        return new Rectangle(0, TopBarHeight, LeftPanelWidth, _graphicsDevice.Viewport.Height - TopBarHeight - StatusBarHeight);
    }

    private Rectangle GetInspectorBounds()
    {
        return new Rectangle(_graphicsDevice.Viewport.Width - RightPanelWidth, TopBarHeight, RightPanelWidth, _graphicsDevice.Viewport.Height - TopBarHeight - StatusBarHeight);
    }

    private Rectangle GetCanvasBounds()
    {
        return new Rectangle(LeftPanelWidth, TopBarHeight, _graphicsDevice.Viewport.Width - LeftPanelWidth - RightPanelWidth, _graphicsDevice.Viewport.Height - TopBarHeight - StatusBarHeight);
    }

    private Rectangle GetStatusBarBounds()
    {
        return new Rectangle(0, _graphicsDevice.Viewport.Height - StatusBarHeight, _graphicsDevice.Viewport.Width, StatusBarHeight);
    }

    private Rectangle GetOpenListBounds()
    {
        Viewport viewport = _graphicsDevice.Viewport;
        return new Rectangle(viewport.Width / 2 - 380, viewport.Height / 2 - 300, 760, 600);
    }

    private static int GetInspectorPropertyStartY(Rectangle inspector)
    {
        return inspector.Y + 128;
    }

    private Rectangle GetNodeBounds(LevelNodeData node)
    {
        Rectangle canvas = GetCanvasBounds();
        return new Rectangle(
            (int)MathF.Round(canvas.X + _canvasPan.X + node.X),
            (int)MathF.Round(canvas.Y + _canvasPan.Y + node.Y),
            NodeWidth,
            NodeHeight);
    }

    private Vector2 ScreenToCanvas(Point screenPoint)
    {
        Rectangle canvas = GetCanvasBounds();
        return new Vector2(screenPoint.X - canvas.X - _canvasPan.X, screenPoint.Y - canvas.Y - _canvasPan.Y);
    }

    private Rectangle GetInputPortBounds(LevelNodeData node)
    {
        Rectangle bounds = GetNodeBounds(node);
        return new Rectangle(bounds.X - PortSize / 2, bounds.Center.Y - PortSize / 2, PortSize, PortSize);
    }

    private Rectangle GetOutputPortBounds(LevelNodeData node, string portName)
    {
        Rectangle bounds = GetNodeBounds(node);
        IReadOnlyList<string> ports = LevelDocument.GetOutputPorts(node.Kind);
        int index = 0;
        for (int i = 0; i < ports.Count; i++)
        {
            if (string.Equals(ports[i], portName, StringComparison.Ordinal))
            {
                index = i;
                break;
            }
        }

        int spacing = 28;
        int totalHeight = Math.Max(0, (ports.Count - 1) * spacing);
        int y = bounds.Center.Y - totalHeight / 2 + index * spacing;
        return new Rectangle(bounds.Right - PortSize / 2, y - PortSize / 2, PortSize, PortSize);
    }

    private Vector2 GetInputPortCenter(LevelNodeData node)
    {
        Rectangle port = GetInputPortBounds(node);
        return new Vector2(port.Center.X, port.Center.Y);
    }

    private Vector2 GetOutputPortCenter(LevelNodeData node, string portName)
    {
        Rectangle port = GetOutputPortBounds(node, portName);
        return new Vector2(port.Center.X, port.Center.Y);
    }

    private static Color GetNodeColor(LevelNodeKind kind)
    {
        return kind switch
        {
            LevelNodeKind.Start => Color.LightGreen,
            LevelNodeKind.Dialogue => new Color(202, 144, 225),
            LevelNodeKind.TrainingBeatmap => new Color(255, 184, 83),
            LevelNodeKind.PlayRepresentationBeatmap => new Color(255, 105, 113),
            LevelNodeKind.SetMiniGame => new Color(108, 206, 189),
            LevelNodeKind.End => new Color(120, 190, 255),
            _ => Color.White
        };
    }

    private static Color GetPortColor(string port)
    {
        return port switch
        {
            "Success" => Color.LightGreen,
            _ => Color.LightBlue
        };
    }

    private static string GetNodeTitle(LevelNodeKind kind)
    {
        return kind switch
        {
            LevelNodeKind.Start => "START LEVEL",
            LevelNodeKind.Dialogue => "TEXTBOX",
            LevelNodeKind.TrainingBeatmap => "TRAINING BEATMAP",
            LevelNodeKind.PlayRepresentationBeatmap => "PLAY REPRESENTATION BEATMAP",
            LevelNodeKind.SetMiniGame => "SET MINIGAME",
            LevelNodeKind.End => "END LEVEL",
            _ => kind.ToString().ToUpperInvariant()
        };
    }

    private static string GetNodeDetail(LevelNodeData node)
    {
        return node.Kind switch
        {
            LevelNodeKind.Dialogue => LevelSpeakerInfo.GetDisplayName(node.Speaker),
            LevelNodeKind.TrainingBeatmap => Path.GetFileName(Path.GetDirectoryName(node.ChartPath) ?? node.ChartPath),
            LevelNodeKind.PlayRepresentationBeatmap => Path.GetFileName(Path.GetDirectoryName(node.ChartPath) ?? node.ChartPath),
            LevelNodeKind.SetMiniGame => GetMiniGameDisplayName(node.MiniGameId),
            _ => string.Empty
        };
    }

    private static string GetMiniGameDisplayName(string miniGameId)
    {
        if (string.IsNullOrWhiteSpace(miniGameId))
            return string.Empty;

        IEditorNoteProvider provider = EditorNoteDefinitions.GameProviders
            .FirstOrDefault(candidate => string.Equals(candidate.RhythmGameId, miniGameId, StringComparison.OrdinalIgnoreCase));
        return provider?.RhythmGameDisplayName ?? miniGameId;
    }

    private static string ShortId(string id)
    {
        return string.IsNullOrWhiteSpace(id) ? "-" : id[..Math.Min(8, id.Length)];
    }

    private int GetTextFieldHeight(string fieldKey, int width)
    {
        if (!fieldKey.StartsWith("node.text:", StringComparison.Ordinal))
            return 30;

        string value = string.Equals(_editingField, fieldKey, StringComparison.Ordinal)
            ? _editingBuffer
            : GetFieldValue(fieldKey);
        int lineCount = WrapDevUiText(string.IsNullOrEmpty(value) ? "<click to edit>" : value, width - 16).Count;
        return Math.Max(30, lineCount * 20 + 16);
    }

    private static IReadOnlyList<string> WrapDevUiText(string value, int width)
    {
        value ??= string.Empty;
        int maxChars = Math.Max(1, width / 8);
        List<string> lines = new();

        foreach (string paragraph in value.Replace("\r", string.Empty).Split('\n'))
        {
            if (paragraph.Length == 0)
            {
                lines.Add(string.Empty);
                continue;
            }

            string current = string.Empty;
            foreach (string word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                string remainingWord = word;
                while (remainingWord.Length > maxChars)
                {
                    if (!string.IsNullOrEmpty(current))
                    {
                        lines.Add(current);
                        current = string.Empty;
                    }

                    lines.Add(remainingWord[..maxChars]);
                    remainingWord = remainingWord[maxChars..];
                }

                string candidate = string.IsNullOrEmpty(current) ? remainingWord : current + " " + remainingWord;
                if (candidate.Length <= maxChars)
                {
                    current = candidate;
                    continue;
                }

                if (!string.IsNullOrEmpty(current))
                    lines.Add(current);
                current = remainingWord;
            }

            if (!string.IsNullOrEmpty(current))
                lines.Add(current);
        }

        if (lines.Count == 0)
            lines.Add(string.Empty);

        return lines;
    }

    private static string Truncate(string value, int maxLength)
    {
        value ??= string.Empty;
        if (value.Length <= maxLength)
            return value;
        return maxLength <= 3 ? value[..maxLength] : value[..(maxLength - 3)] + "...";
    }

    private static int GetTextWidth(string text, int scale)
    {
        return string.IsNullOrEmpty(text) ? 0 : (text.Length * 4 - 1) * scale;
    }

    private static int PositiveModulo(int value, int divisor)
    {
        return (value % divisor + divisor) % divisor;
    }

    private static IReadOnlyList<string> DiscoverMiniGameIds()
    {
        return DiscoverMiniGameEntries()
            .Select(entry => entry.MiniGameId)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(id => GetMiniGameDisplayName(id), StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<MiniGameSelectorEntry> DiscoverMiniGameEntries()
    {
        return EditorNoteDefinitions.GameProviders
            .Where(IsSceneMiniGameProvider)
            .Select(provider => new MiniGameSelectorEntry(provider.RhythmGameId, provider.RhythmGameDisplayName ?? provider.RhythmGameId))
            .GroupBy(entry => entry.MiniGameId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool IsSceneMiniGameProvider(IEditorNoteProvider provider)
    {
        if (provider == null || string.IsNullOrWhiteSpace(provider.RhythmGameId))
            return false;

        try
        {
            return provider.CreateScene() != null;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static IReadOnlyList<LevelSelectorEntry> DiscoverLevelSelectorEntries()
    {
        List<LevelSelectorEntry> entries = new();
        foreach (string levelFile in LevelDocument.DiscoverLevelFiles())
        {
            LevelDocument document;
            try
            {
                document = LevelDocument.LoadOrCreate(levelFile);
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is UnauthorizedAccessException || ex is IOException)
            {
                continue;
            }

            if (document?.Level == null || string.IsNullOrWhiteSpace(document.Level.Id))
                continue;

            string displayName = string.IsNullOrWhiteSpace(document.Level.DisplayName)
                ? levelFile
                : document.Level.DisplayName;
            entries.Add(new LevelSelectorEntry(levelFile, displayName, document.Level.Id));
        }

        return entries
            .OrderBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string NormalizePathForDisplay(string path)
    {
        return BeatmapPackagePaths.NormalizeRelativePath(path ?? string.Empty);
    }

    private bool LeftPressed()
    {
        return _mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton == ButtonState.Released;
    }

    private bool LeftReleased()
    {
        return _mouse.LeftButton == ButtonState.Released && _previousMouse.LeftButton == ButtonState.Pressed;
    }

    private bool MiddlePressed()
    {
        return _mouse.MiddleButton == ButtonState.Pressed && _previousMouse.MiddleButton == ButtonState.Released;
    }

    private bool Pressed(Keys key)
    {
        return _keyboard.IsKeyDown(key) && !_previousKeyboard.IsKeyDown(key);
    }

    private bool IsControlDown()
    {
        return _keyboard.IsKeyDown(Keys.LeftControl) || _keyboard.IsKeyDown(Keys.RightControl);
    }

    private void ResetBackspaceRepeat()
    {
        _backspaceHoldStartMs = 0;
        _backspaceLastRepeatMs = 0;
    }
}
