using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameCore.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MLP_RiM.Elements.DevUI;
using MLP_RiM.Elements.Editor.Commands;
using Rhythm.Note;
using Rhythm.Note.Evaluator;

namespace MLP_RiM.Elements.Editor;

internal enum EditorPlacementMode
{
    Note,
    Effect
}

internal enum EditorTimelineDragKind
{
    None,
    Note,
    Effect,
    ClipCreate,
    ClipMove,
    ClipResizeStart,
    ClipResizeEnd
}

public sealed class BeatmapEditorElement
{
    private readonly BeatmapPlayer _beatmapPlayer;
    private readonly DevUiRenderer _ui;
    private readonly DevUiFloatingWindow _noteOptionsWindow;
    private readonly DevUiFloatingWindow _newBeatmapWindow;
    private readonly Texture2D _pixel;
    private readonly string _defaultSongPath;
    private readonly string _defaultChartPath;
    private readonly double _snapDivisions;
    private readonly List<string> _availableSongs = new();
    private readonly List<string> _availableCharts = new();
    private readonly Dictionary<EditorNoteKind, Dictionary<string, string>> _lastCreatedNoteData = new();
    private readonly Dictionary<string, string> _lastIntervalData = new();
    private readonly EditorCommandStack _commandStack = new();
    private readonly IEditorNoteOptionsPanel _intervalOptionsPanel = new IntervalEditorNoteOptionsPanel();
    private readonly string[] _metadataFields = { "BeatmapName", "Beatmapper", "ArtistName", "MusicName", "BPM", "Offset", "LeadInBeats" };
    private const float ScenePreviewScale = 0.5f;
    private const int TimelineTrackCount = 10;
    private const int TimelineTrackLabelWidth = 180;
    private const int TimelineHeaderHeight = 28;
    private const double ShiftSeekSongDurationRatio = 0.05;
    private const double HeldArrowSeekInitialDelaySeconds = 0.25;
    private const double HeldArrowSeekRepeatSeconds = 0.075;

    private BeatmapEditorDocument _document;
    private EditorRhythmInputVisualElement _rhythmVisuals;
    private KeyboardState _previousKeyboard;
    private KeyboardState _keyboard;
    private MouseState _previousMouse;
    private MouseState _mouse;
    private EditorPlacementMode _placementMode = EditorPlacementMode.Note;
    private EditorNoteKind _selectedKind = EditorNoteKind.SeeSaw;
    private EditorEffectKind _selectedEffectKind = EditorEffectKind.BpmChange;
    private EditorTimelineDragKind _timelineDragKind;
    private double _manualBeatPosition;
    private double _visibleBeforeBeats = 8;
    private double _visibleAfterBeats = 8;
    private string _status = "Editor ready";
    private int _selectedSongIndex;
    private int _selectedChartIndex;
    private int _selectedMetadataField;
    private bool _isEditingText;
    private bool _isPreviewPlaying;
    private bool _editorPlaybackPlaying;
    private string _textBuffer = "";
    private ChartNote _optionsNote;
    private EditorNoteDefinition _optionsDefinition;
    private IEditorNoteOptionsPanel _optionsPanel;
    private ChartEffect _optionsEffect;
    private EditorEffectDefinition _optionsEffectDefinition;
    private IEditorEffectOptionsPanel _effectOptionsPanel;
    private bool _optionsIsEffect;
    private bool _optionsIsCreation;
    private bool _optionsIsIntervalCreation;
    private bool _isSelectingIntervalRange;
    private double? _intervalRangeStart;
    private double _pendingIntervalDurationBeats;
    private bool _isCreatingNewBeatmap;
    private string _newBeatmapNameBuffer = "";
    private ChartNote _draggedNote;
    private ChartEffect _draggedEffect;
    private ChartEditorClip _draggedClip;
    private EditorClipDefinition _draggedClipDefinition;
    private double _dragPointerOffsetBeats;
    private double _dragStartBeat;
    private double _dragEndBeat;
    private double _dragStartLengthBeats;
    private double _dragPreviewStartBeat;
    private double _dragPreviewLengthBeats;
    private int _dragStartTrackIndex;
    private int _dragPreviewTrackIndex;
    private bool _draggedEffectOffsetFollowedPosition;
    private bool _dragMoved;
    private double _heldLeftSeekSeconds;
    private double _heldRightSeekSeconds;
    private bool _leftSeekRepeated;
    private bool _rightSeekRepeated;

    public BeatmapEditorElement(BeatmapPlayer beatmapPlayer, string songPath = "", string chartPath = "Beatmaps/editor_beatmap/chart.xml", double firstBeatDelay = 0.078, double snapDivisions = 4)
    {
        _beatmapPlayer = beatmapPlayer;
        _defaultSongPath = songPath;
        _defaultChartPath = chartPath;
        _snapDivisions = snapDivisions;
        _ui = new DevUiRenderer(GLOBALS.graphicsDevice);
        _noteOptionsWindow = new DevUiFloatingWindow(_ui);
        _newBeatmapWindow = new DevUiFloatingWindow(_ui);
        _pixel = new Texture2D(GLOBALS.graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });

        Load();
    }

    public bool IsPreviewPlaying => _isPreviewPlaying;
    public bool IsEditorPlaybackPlaying => _editorPlaybackPlaying;

    public void ConfigureSceneViewport(RenderViewport viewport)
    {
        if (viewport == null)
            return;

        if (_isPreviewPlaying)
        {
            ConfigureSceneViewportFullscreen(viewport);
            return;
        }

        Rectangle bounds = GetEditorLayout().ScenePreviewPanel;
        viewport.UseBackBufferSize();
        viewport.Position = new Vector2(bounds.X, bounds.Y);
        viewport.Origin = Vector2.Zero;
        viewport.Rotation = 0f;
        viewport.Scale = new Vector2(ScenePreviewScale, ScenePreviewScale);
        viewport.Color = Color.White;
        viewport.ClearBackBufferBeforePresent = true;
    }

    public static void ConfigureSceneViewportFullscreen(RenderViewport viewport)
    {
        if (viewport == null)
            return;

        viewport.UseBackBufferSize();
        viewport.Position = Vector2.Zero;
        viewport.Origin = Vector2.Zero;
        viewport.Rotation = 0f;
        viewport.Scale = Vector2.One;
        viewport.Color = Color.White;
        viewport.ClearBackBufferBeforePresent = true;
    }

    public void Update(GameTime gameTime)
    {
        _previousKeyboard = _keyboard;
        _keyboard = Keyboard.GetState();
        _previousMouse = _mouse;
        _mouse = Mouse.GetState();

        if (_isPreviewPlaying)
        {
            HandlePreviewCommands();
            return;
        }

        AdvanceEditorPlayback(gameTime);

        if (_isCreatingNewBeatmap)
        {
            HandleNewBeatmapModal();
            return;
        }

        if (_noteOptionsWindow.IsEditingTextInput)
        {
            UpdateNoteOptionsWindow();
            return;
        }

        if (HandleTimelineDrag())
        {
            UpdateNoteOptionsWindow();
            return;
        }

        HandleCommands(gameTime);
        UpdateNoteOptionsWindow();
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        if (_isPreviewPlaying)
            return;

        DrawEditorShell(spriteBatch);
        DrawTimeline(spriteBatch);
        _rhythmVisuals?.Draw(spriteBatch);
        DrawHud(spriteBatch);
    }

    private void HandleCommands(GameTime gameTime)
    {
        if (_isEditingText)
        {
            HandleTextEdit();
            return;
        }

        if (Pressed(Keys.N) && IsControlDown())
        {
            OpenNewBeatmapModal();
            return;
        }

        if (Pressed(Keys.E))
            TogglePlacementMode();

        if (Pressed(Keys.Escape) && TryCancelPendingCreation())
            return;

        if (Pressed(Keys.Space))
            TogglePlayback();

        if (Pressed(Keys.Home))
            Seek(0);

        if (Pressed(Keys.Left))
            Seek(GetSteppedSeekPosition(-1));

        if (Pressed(Keys.Right))
            Seek(GetSteppedSeekPosition(1));

        HandleHeldArrowSeek(gameTime, Keys.Left, -1, ref _heldLeftSeekSeconds, ref _leftSeekRepeated);
        HandleHeldArrowSeek(gameTime, Keys.Right, 1, ref _heldRightSeekSeconds, ref _rightSeekRepeated);

        if (IsShiftDown())
        {
            if (Pressed(Keys.Q))
                Seek(GetSteppedSeekPosition(-1));

            if (Pressed(Keys.D))
                Seek(GetSteppedSeekPosition(1));
        }
        else
        {
            if (IsDown(Keys.Q))
                SeekBeat(ClampBeat(CurrentBeatPosition() - gameTime.ElapsedGameTime.TotalSeconds * 4));

            if (IsDown(Keys.D))
                SeekBeat(ClampBeat(CurrentBeatPosition() + gameTime.ElapsedGameTime.TotalSeconds * 4));
        }

        if (Pressed(Keys.Up))
            SelectRelative(-1);

        if (Pressed(Keys.Down))
            SelectRelative(1);

        if ((Pressed(Keys.Enter) || Pressed(Keys.Insert)) && _isSelectingIntervalRange)
        {
            SelectIntervalRangePoint();
            return;
        }

        if (Pressed(Keys.Enter) && _optionsIsCreation && _noteOptionsWindow.IsOpen)
            CreatePendingNote();
        else if (Pressed(Keys.Enter) || Pressed(Keys.Insert))
            PlaceSelected();

        if (Pressed(Keys.I) && _placementMode == EditorPlacementMode.Note)
            ToggleIntervalRangeSelection();

        if (Pressed(Keys.Delete) || Pressed(Keys.Back))
            DeleteNearestSelected();

        if (Pressed(Keys.S) && IsControlDown())
            Save();

        if (Pressed(Keys.Z) && IsControlDown())
        {
            UndoCommand();
            return;
        }

        if (Pressed(Keys.Y) && IsControlDown())
        {
            RedoCommand();
            return;
        }

        if (Pressed(Keys.L) && IsControlDown())
            ReloadCurrentDocument();

        if (Pressed(Keys.R))
            RebuildPlayback(false);

        if (Pressed(Keys.Tab))
        {
            if (!TrySelectNextFirstOptionsDropdownValue())
                SelectNextMetadataField();
        }

        if (Pressed(Keys.F2))
            BeginMetadataEdit();

        if (Pressed(Keys.F3))
            ToggleNoteOptionsWindow();

        if (Pressed(Keys.PageUp))
            SelectSongRelative(-1);

        if (Pressed(Keys.PageDown))
            SelectSongRelative(1);

        if (Pressed(Keys.PageUp) && IsControlDown())
            SelectChartRelative(-1);

        if (Pressed(Keys.PageDown) && IsControlDown())
            SelectChartRelative(1);

        if (Pressed(Keys.F5))
            RefreshSongs();

        if (Pressed(Keys.F6))
        {
            if (IsShiftDown())
                NormalizeBpmChangesToGlobalBeats();
            else
                NormalizeSeeSawNotesToGrid();
        }

        if (Pressed(Keys.OemPlus) || Pressed(Keys.Add))
            Zoom(-0.5);

        if (Pressed(Keys.OemMinus) || Pressed(Keys.Subtract))
            Zoom(0.5);

        if (Pressed(Keys.P))
            StartPreview();

    }

    private bool HandleTimelineDrag()
    {
        if (_document == null || _isCreatingNewBeatmap || _noteOptionsWindow.IsEditingTextInput)
            return false;

        Rectangle area = GetTimelineArea();
        GetTimelineWindow(out double windowStart, out double windowEnd);

        if (_timelineDragKind != EditorTimelineDragKind.None)
        {
            if (_mouse.LeftButton == ButtonState.Released)
            {
                FinishTimelineDrag();
                return true;
            }

            UpdateTimelineDrag(area, windowStart, windowEnd);
            return true;
        }

        if (!LeftPressed() || _optionsIsCreation || MouseOverOpenWindow())
            return false;

        if (TryStartPaletteClipDrag())
            return true;

        if (TrySelectRhythmGameFromList())
            return true;

        return TryStartTimelineDrag(area, windowStart, windowEnd);
    }

    private bool TryStartPaletteClipDrag()
    {
        if (!TryHitPaletteClip(out EditorClipDefinition clipDefinition))
            return false;

        _timelineDragKind = EditorTimelineDragKind.ClipCreate;
        _draggedClip = null;
        _draggedClipDefinition = clipDefinition;
        _dragStartBeat = 0;
        _dragEndBeat = 0;
        _dragStartLengthBeats = Math.Max(0.0, clipDefinition.DefaultLengthBeats);
        _dragPreviewLengthBeats = _dragStartLengthBeats;
        _dragPreviewStartBeat = CurrentBeatPosition();
        _dragStartTrackIndex = 0;
        _dragPreviewTrackIndex = 0;
        _dragMoved = false;
        _status = $"Drag {clipDefinition.DisplayName} to a track";
        return true;
    }

    private bool TrySelectRhythmGameFromList()
    {
        BeatmapEditorLayout layout = GetEditorLayout();
        int gameY = layout.RhythmGameListPanel.Y + 30;
        Rectangle seeSawBounds = new(layout.RhythmGameListPanel.X, gameY - 4, layout.RhythmGameListPanel.Width, 22);
        Rectangle seaponyBounds = new(layout.RhythmGameListPanel.X, gameY + 18, layout.RhythmGameListPanel.Width, 22);

        if (seeSawBounds.Contains(_mouse.Position))
        {
            Select(EditorNoteKind.SeeSaw);
            _status = "Rhythm game See Saw";
            return true;
        }

        if (seaponyBounds.Contains(_mouse.Position))
        {
            Select(EditorNoteKind.SeaponyParade);
            _status = "Rhythm game Seapony Parade";
            return true;
        }

        return false;
    }

    private bool TryStartTimelineDrag(Rectangle area, double windowStart, double windowEnd)
    {
        if (!area.Contains(_mouse.Position))
            return false;

        double mouseBeat = XToBeat(_mouse.X, windowStart, windowEnd, area);

        if (TryHitTimelineClip(area, windowStart, windowEnd, out ChartEditorClip clip, out EditorTimelineDragKind clipDragKind))
        {
            _timelineDragKind = clipDragKind;
            _draggedClip = clip;
            _draggedClipDefinition = EditorClipDefinitions.Find(clip.RhythmGameId, clip.ClipTypeId);
            _dragStartBeat = clip.StartBeat;
            _dragEndBeat = clip.StartBeat;
            _dragStartLengthBeats = clip.LengthBeats;
            _dragPreviewStartBeat = clip.StartBeat;
            _dragPreviewLengthBeats = clip.LengthBeats;
            _dragStartTrackIndex = clip.TrackIndex;
            _dragPreviewTrackIndex = clip.TrackIndex;
            _dragPointerOffsetBeats = clip.StartBeat - mouseBeat;
            _dragMoved = false;
            _status = $"Dragging {_draggedClipDefinition?.DisplayName ?? "clip"}";
            return true;
        }

        if (TryHitTimelineEffect(area, windowStart, windowEnd, out ChartEffect effect))
        {
            _timelineDragKind = EditorTimelineDragKind.Effect;
            _draggedEffect = effect;
            _dragStartBeat = _document.GetEffectBeat(effect);
            _dragEndBeat = _dragStartBeat;
            _dragPointerOffsetBeats = _dragStartBeat - mouseBeat;
            _draggedEffectOffsetFollowedPosition = !effect.TryGetSectionOffset(out double sectionOffset)
                || Math.Abs(sectionOffset) <= 0.0005;
            _dragMoved = false;
            _status = $"Dragging {GetEffectLabel(effect, EditorEffectDefinitions.FromChartEffect(effect))}";
            return true;
        }

        if (TryHitTimelineNote(area, windowStart, windowEnd, out ChartNote note))
        {
            _timelineDragKind = EditorTimelineDragKind.Note;
            _draggedNote = note;
            _dragStartBeat = _document.GetNoteBeat(note);
            _dragEndBeat = _dragStartBeat;
            _dragPointerOffsetBeats = _dragStartBeat - mouseBeat;
            _dragMoved = false;

            EditorNoteDefinition definition = EditorNoteDefinitions.FromChartNote(note);
            _status = $"Dragging {definition?.DisplayName ?? "note"} at {note.SongPosition:0.000}s";
            return true;
        }

        return false;
    }

    private void UpdateTimelineDrag(Rectangle area, double windowStart, double windowEnd)
    {
        double pointerBeat = XToBeat(_mouse.X, windowStart, windowEnd, area) + _dragPointerOffsetBeats;

        if (_timelineDragKind == EditorTimelineDragKind.ClipCreate && _draggedClipDefinition != null)
        {
            UpdateClipCreatePreview(area, windowStart, windowEnd);
            return;
        }

        if ((_timelineDragKind == EditorTimelineDragKind.ClipMove
                || _timelineDragKind == EditorTimelineDragKind.ClipResizeStart
                || _timelineDragKind == EditorTimelineDragKind.ClipResizeEnd)
            && _draggedClip != null)
        {
            UpdateClipDragPreview(area, pointerBeat);
            return;
        }

        if (_timelineDragKind == EditorTimelineDragKind.Note && _draggedNote != null)
        {
            double beat = SnapPlacementBeat(pointerBeat);
            if (_document.MoveNoteToBeat(_draggedNote, beat))
            {
                _dragMoved = true;
                _dragEndBeat = _document.GetNoteBeat(_draggedNote);
                EditorNoteDefinition definition = EditorNoteDefinitions.FromChartNote(_draggedNote);
                _status = $"Dragging {definition?.DisplayName ?? "note"}: {_document.GetNoteBeat(_draggedNote):0.###}b";
            }

            return;
        }

        if (_timelineDragKind == EditorTimelineDragKind.Effect && _draggedEffect != null)
        {
            double beat = ClampEffectPlacementBeat(pointerBeat);
            if (_document.MoveEffectToBeat(_draggedEffect, beat, _draggedEffectOffsetFollowedPosition))
            {
                _dragMoved = true;
                _dragEndBeat = _document.GetEffectBeat(_draggedEffect);
                EditorEffectDefinition definition = EditorEffectDefinitions.FromChartEffect(_draggedEffect);
                _status = $"Dragging {definition?.DisplayName ?? "effect"}: {_document.GetEffectBeat(_draggedEffect):0.###}b";
            }
        }
    }

    private void UpdateClipCreatePreview(Rectangle area, double windowStart, double windowEnd)
    {
        if (!area.Contains(_mouse.Position))
        {
            _dragMoved = false;
            _status = $"Drag {_draggedClipDefinition.DisplayName} to a track";
            return;
        }

        _dragPreviewStartBeat = SnapPlacementBeat(XToBeat(_mouse.X, windowStart, windowEnd, area));
        _dragPreviewTrackIndex = GetTrackIndexAtY(_mouse.Y, area);
        _dragMoved = true;
        _status = $"Create {_draggedClipDefinition.DisplayName}: {_dragPreviewStartBeat:0.###}b on Track {_dragPreviewTrackIndex + 1}";
    }

    private void UpdateClipDragPreview(Rectangle area, double pointerBeat)
    {
        double minLength = 0.0;
        double originalEnd = _dragStartBeat + Math.Max(0.0, _dragStartLengthBeats);

        if (_timelineDragKind == EditorTimelineDragKind.ClipMove)
        {
            _dragPreviewStartBeat = SnapPlacementBeat(pointerBeat);
            _dragPreviewLengthBeats = _dragStartLengthBeats;
            _dragPreviewTrackIndex = GetTrackIndexAtY(_mouse.Y, area);
        }
        else if (_timelineDragKind == EditorTimelineDragKind.ClipResizeStart)
        {
            double newStart = Math.Min(SnapPlacementBeat(pointerBeat), originalEnd - minLength);
            _dragPreviewStartBeat = newStart;
            _dragPreviewLengthBeats = Math.Max(minLength, originalEnd - newStart);
            _dragPreviewTrackIndex = _dragStartTrackIndex;
        }
        else if (_timelineDragKind == EditorTimelineDragKind.ClipResizeEnd)
        {
            double newEnd = Math.Max(SnapPlacementBeat(pointerBeat), _dragStartBeat + minLength);
            _dragPreviewStartBeat = _dragStartBeat;
            _dragPreviewLengthBeats = Math.Max(minLength, newEnd - _dragStartBeat);
            _dragPreviewTrackIndex = _dragStartTrackIndex;
        }

        _dragEndBeat = _dragPreviewStartBeat;
        _dragMoved = true;
        _status = $"{_draggedClipDefinition?.DisplayName ?? "Clip"}: {_dragPreviewStartBeat:0.###}b len {_dragPreviewLengthBeats:0.###} on Track {_dragPreviewTrackIndex + 1}";
    }

    private void FinishTimelineDrag()
    {
        EditorTimelineDragKind kind = _timelineDragKind;
        ChartNote draggedNote = _draggedNote;
        ChartEffect draggedEffect = _draggedEffect;
        ChartEditorClip draggedClip = _draggedClip;
        EditorClipDefinition draggedClipDefinition = _draggedClipDefinition;
        bool dragged = _dragMoved;
        double startBeat = _dragStartBeat;
        double endBeat = _dragEndBeat;
        double previewStartBeat = _dragPreviewStartBeat;
        double previewLengthBeats = _dragPreviewLengthBeats;
        int startTrackIndex = _dragStartTrackIndex;
        int previewTrackIndex = _dragPreviewTrackIndex;
        bool effectOffsetFollowedPosition = _draggedEffectOffsetFollowedPosition;

        _timelineDragKind = EditorTimelineDragKind.None;
        _draggedNote = null;
        _draggedEffect = null;
        _draggedClip = null;
        _draggedClipDefinition = null;
        _dragPointerOffsetBeats = 0;
        _dragStartBeat = 0;
        _dragEndBeat = 0;
        _dragStartLengthBeats = 0;
        _dragPreviewStartBeat = 0;
        _dragPreviewLengthBeats = 0;
        _dragStartTrackIndex = 0;
        _dragPreviewTrackIndex = 0;
        _draggedEffectOffsetFollowedPosition = false;
        _dragMoved = false;

        if (kind == EditorTimelineDragKind.ClipCreate)
        {
            if (dragged && draggedClipDefinition != null && CreateDroppedClip(draggedClipDefinition, previewStartBeat, previewLengthBeats, previewTrackIndex))
                _status = $"Created {draggedClipDefinition.DisplayName} at {previewStartBeat:0.###}b on Track {previewTrackIndex + 1}";
            else if (draggedClipDefinition != null)
                _status = $"Cancelled {draggedClipDefinition.DisplayName}";

            return;
        }

        if (!dragged)
            return;

        if (kind == EditorTimelineDragKind.ClipMove && draggedClip != null)
            ExecuteCommand(new MoveClipCommand(draggedClip.Id, previewStartBeat, previewTrackIndex));
        else if (kind == EditorTimelineDragKind.ClipResizeEnd && draggedClip != null)
            ExecuteCommand(new ResizeClipCommand(draggedClip.Id, previewLengthBeats));
        else if (kind == EditorTimelineDragKind.ClipResizeStart && draggedClip != null)
            ExecuteCommand(new CompositeEditorCommand("Resize Clip", new IEditorCommand[]
            {
                new MoveClipCommand(draggedClip.Id, previewStartBeat, startTrackIndex),
                new ResizeClipCommand(draggedClip.Id, previewLengthBeats)
            }));
        else if (kind == EditorTimelineDragKind.Note && draggedNote != null)
            ExecuteCommand(new MoveNoteCommand(draggedNote, startBeat, endBeat));
        else if (kind == EditorTimelineDragKind.Effect && draggedEffect != null)
            ExecuteCommand(new MoveEffectCommand(draggedEffect, startBeat, endBeat, effectOffsetFollowedPosition));
        else
            RebuildPlayback(_editorPlaybackPlaying);

        if ((kind == EditorTimelineDragKind.ClipMove || kind == EditorTimelineDragKind.ClipResizeStart || kind == EditorTimelineDragKind.ClipResizeEnd) && draggedClip != null)
        {
            _status = $"Updated {draggedClipDefinition?.DisplayName ?? "clip"} at {previewStartBeat:0.###}b len {previewLengthBeats:0.###}";
        }
        else if (kind == EditorTimelineDragKind.Note && draggedNote != null)
        {
            EditorNoteDefinition definition = EditorNoteDefinitions.FromChartNote(draggedNote);
            _status = $"Moved {definition?.DisplayName ?? "note"} to {_document.GetNoteBeat(draggedNote):0.###}b";
        }
        else if (kind == EditorTimelineDragKind.Effect && draggedEffect != null)
        {
            EditorEffectDefinition definition = EditorEffectDefinitions.FromChartEffect(draggedEffect);
            _status = $"Moved {definition?.DisplayName ?? "effect"} to {_document.GetEffectBeat(draggedEffect):0.###}b";
        }
    }

    private bool TryHitTimelineEffect(Rectangle area, double windowStart, double windowEnd, out ChartEffect effect)
    {
        effect = null;
        GetSongWindowForBeatWindow(windowStart, windowEnd, out double songStart, out double songEnd);
        foreach (ChartEffect candidate in _document.GetEffectsInWindow(songStart, songEnd).Reverse())
        {
            if (EditorEffectDefinitions.FromChartEffect(candidate) == null)
                continue;

            Rectangle bounds = GetEffectMarkerBounds(candidate, windowStart, windowEnd, area);
            bounds.Inflate(6, 4);
            if (!bounds.Contains(_mouse.Position))
                continue;

            effect = candidate;
            return true;
        }

        return false;
    }

    private bool TryHitTimelineNote(Rectangle area, double windowStart, double windowEnd, out ChartNote note)
    {
        note = null;
        GetSongWindowForBeatWindow(windowStart, windowEnd, out double songStart, out double songEnd);
        foreach (ChartNote candidate in _document.GetNotesInWindow(songStart, songEnd).Reverse())
        {
            if (EditorNoteDefinitions.FromChartNote(candidate) == null)
                continue;

            Rectangle bounds = GetNoteMarkerBounds(candidate, windowStart, windowEnd, area);
            bounds.Inflate(6, 4);
            if (!bounds.Contains(_mouse.Position))
                continue;

            note = candidate;
            return true;
        }

        return false;
    }

    private bool MouseOverOpenWindow()
    {
        return (_noteOptionsWindow.IsOpen && GetNoteOptionsWindowBounds().Contains(_mouse.Position))
            || (_newBeatmapWindow.IsOpen && GetNewBeatmapWindowBounds().Contains(_mouse.Position));
    }

    private void ToggleNoteOptionsWindow()
    {
        if (_noteOptionsWindow.IsOpen)
        {
            _noteOptionsWindow.Close();
            return;
        }

        if (_placementMode == EditorPlacementMode.Effect)
            OpenEffectOptionsWindow(_document.FindNearestEffectAtBeat(CurrentBeatPosition(), GetSelectionDistance()));
        else
            OpenNoteOptionsWindow(_document.FindNearestAtBeat(CurrentBeatPosition(), GetSelectionDistance()));
    }

    private void TogglePlacementMode()
    {
        if (_isSelectingIntervalRange)
        {
            _isSelectingIntervalRange = false;
            _intervalRangeStart = null;
        }

        _placementMode = _placementMode == EditorPlacementMode.Note ? EditorPlacementMode.Effect : EditorPlacementMode.Note;
        _noteOptionsWindow.Close();
        ClearPendingOptions();
        _status = _placementMode == EditorPlacementMode.Note
            ? $"Mode notes: {EditorNoteDefinitions.Get(_selectedKind).DisplayName}"
            : $"Mode effects: {EditorEffectDefinitions.Get(_selectedEffectKind).DisplayName}";
    }

    private bool OpenNoteOptionsWindow(ChartNote note)
    {
        if (note == null)
        {
            _status = "No configurable note close enough";
            return false;
        }

        _optionsNote = note;
        EditorNoteDefinition definition = EditorNoteDefinitions.FromChartNote(_optionsNote);
        if (definition == null || !EditorNoteDefinitions.TryGetOptionsPanel(definition.Kind, out _optionsPanel))
        {
            _status = "No configurable note close enough";
            return false;
        }

        _optionsDefinition = definition;
        _optionsEffect = null;
        _optionsEffectDefinition = null;
        _effectOptionsPanel = null;
        _optionsIsEffect = false;
        _optionsIsCreation = false;
        _optionsIsIntervalCreation = false;
        _noteOptionsWindow.Open();
        _status = $"Options for {definition.DisplayName} at {_optionsNote.SongPosition:0.000}s";
        return true;
    }

    private void OpenCreateNoteWindow(EditorNoteDefinition definition, double songPosition)
    {
        double beat = _document.GetBeatAt(songPosition);
        _optionsNote = definition.CreateChartNote(songPosition, _document.GetSecondsPerBeatAtBeat(beat), variantIndex: 0);
        ChartTiming.SetNoteBeat(_optionsNote, beat);
        ChartTiming.SetNoteHoldBeats(_optionsNote, definition.HoldBeats);
        if (_lastCreatedNoteData.TryGetValue(definition.Kind, out Dictionary<string, string> lastData))
            _optionsNote.AdditionnalData = new Dictionary<string, string>(lastData);

        _optionsDefinition = definition;
        _optionsEffect = null;
        _optionsEffectDefinition = null;
        _effectOptionsPanel = null;
        _optionsIsEffect = false;
        _optionsIsCreation = true;
        _optionsIsIntervalCreation = false;
        if (!EditorNoteDefinitions.TryGetOptionsPanel(definition.Kind, out _optionsPanel))
            _optionsPanel = null;
        _noteOptionsWindow.Open();
        _status = $"Configure {definition.DisplayName} at {beat:0.###}b, then Create";
    }

    private void OpenCreateIntervalWindow(EditorNoteDefinition definition, double startSongPosition, double endSongPosition)
    {
        double start = Math.Min(startSongPosition, endSongPosition);
        double end = Math.Max(startSongPosition, endSongPosition);
        double startBeat = _document.GetBeatAt(start);
        _optionsNote = definition.CreateChartNote(start, _document.GetSecondsPerBeatAtBeat(startBeat), variantIndex: 0);
        ChartTiming.SetNoteBeat(_optionsNote, startBeat);
        ChartTiming.SetNoteHoldBeats(_optionsNote, definition.HoldBeats);
        if (_lastCreatedNoteData.TryGetValue(definition.Kind, out Dictionary<string, string> lastData))
            _optionsNote.AdditionnalData = new Dictionary<string, string>(lastData);

        _pendingIntervalDurationBeats = GetBeatsBetween(start, end);
        foreach (KeyValuePair<string, string> pair in _lastIntervalData)
        {
            if (pair.Key != IntervalEditorNoteProvider.DurationBeatsKey)
                _optionsNote.AdditionnalData[pair.Key] = pair.Value;
        }

        IntervalEditorNoteProvider.SetDurationBeats(_optionsNote, _pendingIntervalDurationBeats);

        _optionsDefinition = definition;
        _optionsEffect = null;
        _optionsEffectDefinition = null;
        _effectOptionsPanel = null;
        _optionsIsEffect = false;
        _optionsIsCreation = true;
        _optionsIsIntervalCreation = true;
        if (!EditorNoteDefinitions.TryGetOptionsPanel(definition.Kind, out _optionsPanel))
            _optionsPanel = null;
        _noteOptionsWindow.Open();
        _status = $"Configure interval {definition.DisplayName} from {startBeat:0.###}b to {_document.GetBeatAt(end):0.###}b, then Create";
    }

    private bool OpenEffectOptionsWindow(ChartEffect effect)
    {
        if (effect == null)
        {
            _status = "No configurable effect close enough";
            return false;
        }

        _optionsEffect = effect;
        EditorEffectDefinition definition = EditorEffectDefinitions.FromChartEffect(_optionsEffect);
        if (definition == null || !EditorEffectDefinitions.TryGetOptionsPanel(definition.Kind, out _effectOptionsPanel))
        {
            _status = "No configurable effect close enough";
            return false;
        }

        _optionsNote = null;
        _optionsDefinition = null;
        _optionsPanel = null;
        _optionsEffectDefinition = definition;
        _optionsIsEffect = true;
        _optionsIsCreation = false;
        _optionsIsIntervalCreation = false;
        _noteOptionsWindow.Open();
        _status = $"Options for {definition.DisplayName} at {_optionsEffect.SongPosition:0.000}s";
        return true;
    }

    private void OpenCreateEffectWindow(EditorEffectDefinition definition, double songPosition)
    {
        _optionsNote = null;
        _optionsDefinition = null;
        _optionsPanel = null;
        _optionsEffect = definition.CreateChartEffect(songPosition, _document);
        _optionsEffectDefinition = definition;
        _optionsIsEffect = true;
        _optionsIsCreation = true;
        _optionsIsIntervalCreation = false;
        if (!EditorEffectDefinitions.TryGetOptionsPanel(definition.Kind, out _effectOptionsPanel))
            _effectOptionsPanel = null;
        _noteOptionsWindow.Open();
        _status = $"Configure {definition.DisplayName} at {_document.GetEffectBeat(_optionsEffect):0.###}b, then Create";
    }

    private void ApplyNoteOption(Action<ChartNote> apply)
    {
        ChartNote note = ResolveOptionsNote();
        if (note == null)
            return;

        apply(note);
    }

    private void ApplyEffectOption(Action<ChartEffect> apply)
    {
        ChartEffect effect = ResolveOptionsEffect();
        if (effect == null)
            return;

        apply(effect);
    }

    private ChartNote ResolveOptionsNote()
    {
        if (_optionsIsCreation)
            return _optionsNote;

        ChartNote note = _optionsNote;
        if (note == null || !_document.Chart.Notes.Contains(note))
            note = _document.FindNearestAtBeat(CurrentBeatPosition(), GetSelectionDistance());

        if (note == null)
            return null;

        _optionsNote = note;
        return note;
    }

    private ChartEffect ResolveOptionsEffect()
    {
        if (_optionsIsCreation)
            return _optionsEffect;

        ChartEffect effect = _optionsEffect;
        if (effect == null || !_document.Chart.Effects.Contains(effect))
            effect = _document.FindNearestEffectAtBeat(CurrentBeatPosition(), GetSelectionDistance());

        if (effect == null)
            return null;

        _optionsEffect = effect;
        return effect;
    }

    private void UpdateNoteOptionsWindow()
    {
        if (!_noteOptionsWindow.IsOpen)
            return;

        if (_optionsIsEffect)
        {
            UpdateEffectOptionsWindow();
            return;
        }

        if (_optionsNote == null || _optionsDefinition == null || (!_optionsIsCreation && !_document.Chart.Notes.Contains(_optionsNote)))
        {
            _noteOptionsWindow.Close();
            return;
        }

        bool wasCreation = _optionsIsCreation;
        ChartNote oldSnapshot = wasCreation ? null : EditorCommandCloning.CloneNote(_optionsNote);
        if (_noteOptionsWindow.Update(GetNoteOptionsWindowBounds(), GetNoteOptionRows()))
        {
            if (wasCreation)
                return;

            ChartNote newSnapshot = EditorCommandCloning.CloneNote(_optionsNote);
            if (!NotesEqual(oldSnapshot, newSnapshot))
                ExecuteCommand(new ChangeNoteCommand(_optionsNote, oldSnapshot, newSnapshot));

            _status = $"Updated options at {_optionsNote.SongPosition:0.000}s";
        }
    }

    private void UpdateEffectOptionsWindow()
    {
        if (_optionsEffect == null || _optionsEffectDefinition == null || (!_optionsIsCreation && !_document.Chart.Effects.Contains(_optionsEffect)))
        {
            _noteOptionsWindow.Close();
            return;
        }

        bool wasCreation = _optionsIsCreation;
        ChartEffect oldSnapshot = wasCreation ? null : EditorCommandCloning.CloneEffect(_optionsEffect);
        if (_noteOptionsWindow.Update(GetNoteOptionsWindowBounds(), GetNoteOptionRows()))
        {
            if (wasCreation)
                return;

            ChartEffect newSnapshot = EditorCommandCloning.CloneEffect(_optionsEffect);
            if (!EffectsEqual(oldSnapshot, newSnapshot))
                ExecuteCommand(new ChangeEffectCommand(_optionsEffect, oldSnapshot, newSnapshot));

            _status = $"Updated effect at {_optionsEffect.SongPosition:0.000}s";
        }
    }

    private void StartPreview()
    {
        if (!HasPlayableSong())
        {
            _status = "No music loaded";
            return;
        }

        double position = CurrentSongPosition();
        _editorPlaybackPlaying = false;
        _beatmapPlayer.StartBeatmapPaused(_document.SongPath, _document.Chart, ReactionRules.RhythmHeavenLike(), new RhythmHeavenLikeReactionEvaluator());
        SyncPlaybackToEditorPosition(position, resetVisuals: true);
        _beatmapPlayer.Conductor.Play();
        _isPreviewPlaying = true;
        _status = "Preview playing — ESC to stop";
    }

    private void HandlePreviewCommands()
    {
        if (Pressed(Keys.Escape))
            StopPreview();
    }

    private void StopPreview()
    {
        double position = _beatmapPlayer.Conductor?.SongPosition ?? CurrentSongPosition();
        double beat = _document.GetBeatAt(position);
        _isPreviewPlaying = false;
        _editorPlaybackPlaying = false;
        RebuildPlayback(false);
        SeekBeat(beat);
        _status = $"Back to editor at {beat:0.###}b / {position:0.000}s";
    }

    private void Load()
    {
        RefreshSongs();
        RefreshCharts();

        string songPath = !string.IsNullOrWhiteSpace(_defaultSongPath)
            ? _defaultSongPath
            : _availableSongs.FirstOrDefault() ?? "";
        string chartPath = !string.IsNullOrWhiteSpace(_defaultChartPath) && _availableCharts.Contains(_defaultChartPath)
            ? _defaultChartPath
            : _availableCharts.FirstOrDefault() ?? _defaultChartPath;

        LoadDocument(songPath, chartPath);
    }

    private void ReloadCurrentDocument()
    {
        string songPath = _document?.SongPath ?? _defaultSongPath;
        string chartPath = _document?.ChartPath ?? _defaultChartPath;
        LoadDocument(songPath, chartPath);
    }

    private void LoadDocument(string songPath, string chartPath)
    {
        RefreshSongs();
        RefreshCharts();
        _document = BeatmapEditorDocument.LoadOrCreate(songPath, chartPath, 100);
        _commandStack.Clear();
        SyncSelectedSongIndex();
        SyncSelectedChartIndex();
        RebuildPlayback(false);
        _status = $"Loaded {_document.ChartPath}";
    }

    private void Save()
    {
        _document.Save();
        RefreshCharts();
        SyncSelectedChartIndex();
        _status = $"Saved {_document.Chart.Notes.Count} notes";
    }

    private bool ExecuteCommand(IEditorCommand command, bool rebuildPlayback = true)
    {
        try
        {
            double position = CurrentSongPosition();
            _commandStack.Execute(command, _document);
            if (rebuildPlayback)
                RebuildPlaybackAtPosition(position, _editorPlaybackPlaying);

            return true;
        }
        catch (Exception ex)
        {
            _status = ex.Message;
            return false;
        }
    }

    private void UndoCommand()
    {
        if (!_commandStack.CanUndo)
        {
            _status = "Nothing to undo";
            return;
        }

        string name = _commandStack.NextUndoName;
        double position = CurrentSongPosition();
        try
        {
            if (_commandStack.TryUndo(_document))
            {
                RebuildPlaybackAtPosition(position, _editorPlaybackPlaying);
                _status = $"Undid {name}";
            }
        }
        catch (Exception ex)
        {
            _status = ex.Message;
        }
    }

    private void RedoCommand()
    {
        if (!_commandStack.CanRedo)
        {
            _status = "Nothing to redo";
            return;
        }

        string name = _commandStack.NextRedoName;
        double position = CurrentSongPosition();
        try
        {
            if (_commandStack.TryRedo(_document))
            {
                RebuildPlaybackAtPosition(position, _editorPlaybackPlaying);
                _status = $"Redid {name}";
            }
        }
        catch (Exception ex)
        {
            _status = ex.Message;
        }
    }

    private void NormalizeSeeSawNotesToGrid()
    {
        int normalizedCount = _document.NormalizeSeeSawNotesToGrid(GetEffectiveSnapDivisions());
        if (normalizedCount <= 0)
        {
            _status = "No See-Saw notes needed grid normalization";
            return;
        }

        RebuildPlayback(_editorPlaybackPlaying);
        _status = $"Normalized {normalizedCount} See-Saw notes to global beat grid";
    }

    private void NormalizeBpmChangesToGlobalBeats()
    {
        int normalizedCount = _document.NormalizeBpmChangesToNearestGlobalBeat();
        if (normalizedCount <= 0)
        {
            _status = "No BPM changes needed beat normalization";
            return;
        }

        RebuildPlayback(_editorPlaybackPlaying);
        _status = $"Normalized {normalizedCount} BPM changes to nearest global beat";
    }

    private void OpenNewBeatmapModal()
    {
        _newBeatmapNameBuffer = "New Beatmap";
        _isCreatingNewBeatmap = true;
        _newBeatmapWindow.Open();
        _status = "Creating new beatmap";
    }

    private void HandleNewBeatmapModal()
    {
        if (Pressed(Keys.Enter))
        {
            CreateNewBeatmap();
            return;
        }

        if (Pressed(Keys.Escape))
        {
            CloseNewBeatmapModal("New beatmap cancelled");
            return;
        }

        if (Pressed(Keys.Back) && _newBeatmapNameBuffer.Length > 0)
            _newBeatmapNameBuffer = _newBeatmapNameBuffer[..^1];

        foreach (Keys key in _keyboard.GetPressedKeys())
        {
            if (_previousKeyboard.IsKeyDown(key))
                continue;

            if (TryKeyToChar(key, out char c))
                _newBeatmapNameBuffer += c;
        }

        if (_newBeatmapWindow.Update(GetNewBeatmapWindowBounds(), GetNewBeatmapRows()) && !_newBeatmapWindow.IsOpen)
            _isCreatingNewBeatmap = false;
    }

    private void CreateNewBeatmap()
    {
        string beatmapName = string.IsNullOrWhiteSpace(_newBeatmapNameBuffer) ? "New Beatmap" : _newBeatmapNameBuffer.Trim();
        string chartPath = GetAvailableNewBeatmapPath(beatmapName);
        string songPath = _document?.SongPath ?? _defaultSongPath;
        double bpm = _document?.Chart?.BPM > 0 ? _document.Chart.BPM : 100;

        _document = BeatmapEditorDocument.CreateNew(songPath, chartPath, bpm);
        _commandStack.Clear();
        _document.SetMetadata(beatmapName: beatmapName);
        _manualBeatPosition = 0;
        RefreshSongs();
        RefreshCharts();
        SyncSelectedSongIndex();
        SyncSelectedChartIndex();
        RebuildPlayback(false);
        CloseNewBeatmapModal($"New beatmap {GetChartDisplayName(chartPath)} ready");
    }

    private void CloseNewBeatmapModal(string status)
    {
        _isCreatingNewBeatmap = false;
        _newBeatmapWindow.Close();
        _status = status;
    }

    private string GetAvailableNewBeatmapPath(string beatmapName)
    {
        return BeatmapPackagePaths.GetAvailablePackageChartPath(beatmapName);
    }

    private static string SanitizeFileName(string value)
    {
        char[] invalidChars = Path.GetInvalidFileNameChars();
        string sanitized = new(value.Select(c => invalidChars.Contains(c) ? '_' : c).ToArray());
        return sanitized.Trim().Trim('.');
    }

    private static string GetChartDisplayName(string chartPath)
    {
        if (BeatmapPackagePaths.IsPackageChartPath(chartPath))
            return Path.GetFileName(Path.GetDirectoryName(chartPath));

        return Path.GetFileName(chartPath);
    }

    private void RebuildPlayback(bool keepPlaying)
    {
        RebuildPlaybackAtPosition(CurrentSongPosition(), keepPlaying);
    }

    private void RebuildPlaybackAtPosition(double position, bool keepPlaying)
    {
        bool shouldPlay = keepPlaying && !_isPreviewPlaying;
        _editorPlaybackPlaying = false;

        if (!HasPlayableSong())
        {
            _rhythmVisuals = null;
            _beatmapPlayer.Dispose();
            _manualBeatPosition = _document.GetBeatAt(position);
            _status = "No music loaded";
            return;
        }

        _beatmapPlayer.StartBeatmapPaused(_document.SongPath, _document.Chart, ReactionRules.RhythmHeavenLike(), new RhythmHeavenLikeReactionEvaluator());
        _rhythmVisuals = new EditorRhythmInputVisualElement(_beatmapPlayer, _pixel, _ui);
        SyncPlaybackToEditorPosition(position, resetVisuals: true);

        if (shouldPlay)
        {
            StartEditorAudioIfAtOrAfterSongStart();
            _editorPlaybackPlaying = true;
        }
    }

    private void TogglePlayback()
    {
        if (!HasPlayableSong())
        {
            _status = "No music loaded";
            return;
        }

        if (_beatmapPlayer.Conductor == null)
            return;

        if (_editorPlaybackPlaying)
        {
            _editorPlaybackPlaying = false;
            _beatmapPlayer.Conductor.Pause();
            SyncPlaybackToEditorPosition(CurrentSongPosition(), resetVisuals: false);
            _status = "Paused";
        }
        else
        {
            SyncPlaybackToEditorPosition(CurrentSongPosition(), resetVisuals: false);
            StartEditorAudioIfAtOrAfterSongStart();
            _editorPlaybackPlaying = true;
            _status = "Playing";
        }
    }

    private void PlaceSelected()
    {
        if (_placementMode == EditorPlacementMode.Effect)
            PlaceSelectedEffect();
        else
            PlaceSelectedNote();
    }

    private void PlaceSelectedNote()
    {
        EditorNoteDefinition definition = EditorNoteDefinitions.Get(_selectedKind);
        double position = _document.GetSongPositionAtBeat(SnapPlacementBeat(CurrentBeatPosition()));
        OpenCreateNoteWindow(definition, position);
    }

    private void PlaceSelectedEffect()
    {
        EditorEffectDefinition definition = EditorEffectDefinitions.Get(_selectedEffectKind);
        double position = _document.GetSongPositionAtBeat(SnapEffectPlacementBeat(definition, CurrentBeatPosition()));
        OpenCreateEffectWindow(definition, position);
    }

    private void ToggleIntervalRangeSelection()
    {
        if (_isSelectingIntervalRange)
        {
            _isSelectingIntervalRange = false;
            _intervalRangeStart = null;
            _status = "Interval cancelled";
            return;
        }

        _noteOptionsWindow.Close();
        _optionsIsCreation = false;
        _optionsIsIntervalCreation = false;
        _isSelectingIntervalRange = true;
        _intervalRangeStart = null;
        _status = $"Interval {EditorNoteDefinitions.Get(_selectedKind).DisplayName}: ENTER start, ENTER end";
    }

    private void SelectIntervalRangePoint()
    {
        double position = _document.GetSongPositionAtBeat(SnapPlacementBeat(CurrentBeatPosition()));
        if (_intervalRangeStart == null)
        {
            _intervalRangeStart = _document.GetBeatAt(position);
            _status = $"Interval start {_intervalRangeStart.Value:0.###}b selected; move to end and press ENTER";
            return;
        }

        EditorNoteDefinition definition = EditorNoteDefinitions.Get(_selectedKind);
        double start = _document.GetSongPositionAtBeat(_intervalRangeStart.Value);
        _isSelectingIntervalRange = false;
        _intervalRangeStart = null;
        OpenCreateIntervalWindow(definition, start, position);
    }

    private void CreatePendingNote()
    {
        if (_optionsIsEffect)
        {
            CreatePendingEffect();
            return;
        }

        if (!_optionsIsCreation || _optionsNote == null || _optionsDefinition == null)
            return;

        Dictionary<string, string> creationData = GetNoteCreationData(_optionsNote);
        bool wasIntervalCreation = _optionsIsIntervalCreation;
        if (_optionsIsIntervalCreation)
            StoreLastIntervalData(_optionsNote);

        IReadOnlyList<EditorNotePlacement> placements = _optionsDefinition.CreatePlacements(_optionsNote, CreatePlacementContext());
        PlaceNotesCommand command = new(placements);
        if (ExecuteCommand(command))
        {
            IReadOnlyList<ChartNote> placedNotes = command.PlacedNotes;
            _lastCreatedNoteData[_optionsDefinition.Kind] = creationData;
            _optionsIsCreation = false;
            _optionsIsIntervalCreation = false;

            if (placedNotes.Count == 1 && EditorNoteDefinitions.FromChartNote(placedNotes[0]) is EditorNoteDefinition placedDefinition && EditorNoteDefinitions.TryGetOptionsPanel(placedDefinition.Kind, out _))
                OpenNoteOptionsWindow(placedNotes[0]);
            else
                _noteOptionsWindow.Close();

            ChartNote firstNote = placedNotes[0];
            ChartNote lastNote = placedNotes[placedNotes.Count - 1];
            string creationName = GetSelectedNoteName(_optionsDefinition);
            if (placedNotes.Count == 1)
                _status = $"Created {creationName} at {firstNote.SongPosition:0.000}s";
            else if (wasIntervalCreation)
                _status = $"Created interval {creationName}: {placedNotes.Count} notes from {firstNote.SongPosition:0.000}s to {lastNote.SongPosition:0.000}s";
            else
                _status = $"Created {creationName}: {placedNotes.Count} notes from {firstNote.SongPosition:0.000}s to {lastNote.SongPosition:0.000}s";
        }
    }

    private void CreatePendingEffect()
    {
        if (!_optionsIsCreation || _optionsEffect == null || _optionsEffectDefinition == null)
            return;

        PlaceEffectCommand command = new(_optionsEffect);
        if (ExecuteCommand(command))
        {
            ChartEffect placedEffect = command.PlacedEffect;
            _optionsIsCreation = false;

            if (EditorEffectDefinitions.FromChartEffect(placedEffect) is EditorEffectDefinition placedDefinition && EditorEffectDefinitions.TryGetOptionsPanel(placedDefinition.Kind, out _))
                OpenEffectOptionsWindow(placedEffect);
            else
                _noteOptionsWindow.Close();

            _status = $"Created {_optionsEffectDefinition.DisplayName} at {placedEffect.SongPosition:0.000}s";
        }
    }

    private bool TryCancelPendingCreation()
    {
        if (_isSelectingIntervalRange)
        {
            _isSelectingIntervalRange = false;
            _intervalRangeStart = null;
            _status = "Interval cancelled";
            return true;
        }

        if (!_optionsIsCreation)
            return false;

        string cancelledName = _optionsIsEffect
            ? _optionsEffectDefinition?.DisplayName ?? "Effect"
            : _optionsIsIntervalCreation ? "Interval" : _optionsDefinition?.DisplayName ?? "Note";
        ClearPendingOptions();
        _noteOptionsWindow.Close();
        _status = $"{cancelledName} creation cancelled";
        return true;
    }

    private EditorNotePlacementContext CreatePlacementContext()
    {
        double songPosition = _optionsNote?.SongPosition ?? CurrentSongPosition();
        return new EditorNotePlacementContext(_document.GetCrotchetAt(songPosition), _document.Chart.Notes);
    }

    private void ClearPendingOptions()
    {
        _optionsNote = null;
        _optionsDefinition = null;
        _optionsPanel = null;
        _optionsEffect = null;
        _optionsEffectDefinition = null;
        _effectOptionsPanel = null;
        _optionsIsEffect = false;
        _optionsIsCreation = false;
        _optionsIsIntervalCreation = false;
    }

    private static Dictionary<string, string> GetNoteCreationData(ChartNote note)
    {
        return EditorNotePlacementData.CreateStoredAdditionnalData(note);
    }

    private static bool NotesEqual(ChartNote a, ChartNote b)
    {
        return a == null && b == null
            || a != null && b != null
            && NearlyEqual(a.SongPosition, b.SongPosition)
            && NullableNearlyEqual(a.BeatPosition, b.BeatPosition)
            && NearlyEqual(a.HoldDuration, b.HoldDuration)
            && NullableNearlyEqual(a.HoldBeats, b.HoldBeats)
            && a.InputActionToPress == b.InputActionToPress
            && DictionariesEqual(a.AdditionnalData, b.AdditionnalData);
    }

    private static bool EffectsEqual(ChartEffect a, ChartEffect b)
    {
        return a == null && b == null
            || a != null && b != null
            && NearlyEqual(a.SongPosition, b.SongPosition)
            && NullableNearlyEqual(a.BeatPosition, b.BeatPosition)
            && a.EffectType == b.EffectType
            && DictionariesEqual(a.Data, b.Data);
    }

    private static bool NearlyEqual(double a, double b)
    {
        return Math.Abs(a - b) <= 0.000000001;
    }

    private static bool NullableNearlyEqual(double? a, double? b)
    {
        if (!a.HasValue || !b.HasValue)
            return a.HasValue == b.HasValue;

        return NearlyEqual(a.Value, b.Value);
    }

    private static bool DictionariesEqual(Dictionary<string, string> a, Dictionary<string, string> b)
    {
        a ??= new Dictionary<string, string>();
        b ??= new Dictionary<string, string>();
        return a.Count == b.Count && a.All(pair => b.TryGetValue(pair.Key, out string value) && value == pair.Value);
    }

    private void StoreLastIntervalData(ChartNote note)
    {
        _lastIntervalData[IntervalEditorNoteProvider.DurationBeatsKey] = IntervalEditorNoteProvider.GetDurationBeats(note.AdditionnalData).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        _lastIntervalData[IntervalEditorNoteProvider.StepBeatsKey] = IntervalEditorNoteProvider.GetStepBeats(note.AdditionnalData).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
    }

    private double GetBeatsBetween(double startSongPosition, double endSongPosition)
    {
        return Math.Abs(_document.GetBeatAt(endSongPosition) - _document.GetBeatAt(startSongPosition));
    }

    private void DeleteNearestSelected()
    {
        if (_placementMode == EditorPlacementMode.Effect)
            DeleteNearestEffect();
        else
            DeleteNearestNote();
    }

    private void DeleteNearestNote()
    {
        double beat = CurrentBeatPosition();
        ChartNote deletedNote = _document.FindNearestAtBeat(beat, GetSelectionDistance());
        if (deletedNote == null)
        {
            _status = "No note close enough to delete";
        }
        else if (ExecuteCommand(new DeleteNoteCommand(deletedNote)))
        {
            _status = $"Deleted note at {deletedNote.SongPosition:0.000}s";
        }
    }

    private void DeleteNearestEffect()
    {
        double beat = CurrentBeatPosition();
        ChartEffect deletedEffect = _document.FindNearestEffectAtBeat(beat, GetSelectionDistance());
        if (deletedEffect == null)
        {
            _status = "No effect close enough to delete";
        }
        else if (ExecuteCommand(new DeleteEffectCommand(deletedEffect)))
        {
            if (ReferenceEquals(_optionsEffect, deletedEffect))
            {
                _noteOptionsWindow.Close();
                ClearPendingOptions();
            }

            _status = $"Deleted effect at {deletedEffect.SongPosition:0.000}s";
        }
    }

    private void Seek(double songPosition, bool updateStatus = true)
    {
        SeekBeat(_document.GetBeatAt(songPosition), updateStatus);
    }

    private void SeekBeat(double beat, bool updateStatus = true)
    {
        SyncPlaybackToEditorBeatPosition(beat, resetVisuals: true);
        UpdatePendingCreationPosition();

        if (_editorPlaybackPlaying)
            StartEditorAudioIfAtOrAfterSongStart();

        if (updateStatus)
            _status = $"Seek {_manualBeatPosition:0.###}b / {CurrentSongPosition():0.000}s";
    }

    private void UpdatePendingCreationPosition()
    {
        if (!_optionsIsCreation || _optionsIsIntervalCreation)
            return;

        if (_optionsIsEffect && _optionsEffect != null)
        {
            bool offsetFollowedPosition = !_optionsEffect.TryGetSectionOffset(out double sectionOffset)
                || Math.Abs(sectionOffset) <= 0.0005;

            double beat = SnapEffectPlacementBeat(_optionsEffectDefinition, CurrentBeatPosition());
            ChartTiming.SetEffectBeat(_optionsEffect, beat);
            _optionsEffect.SongPosition = _document.GetSongPositionAtBeat(beat);
            if (offsetFollowedPosition)
                _optionsEffect.SetSectionOffset(0);
        }
        else if (_optionsNote != null)
        {
            double beat = SnapPlacementBeat(CurrentBeatPosition());
            ChartTiming.SetNoteBeat(_optionsNote, beat);
            _optionsNote.SongPosition = _document.GetSongPositionAtBeat(beat);
        }
    }

    private void AdvanceEditorPlayback(GameTime gameTime)
    {
        if (!_editorPlaybackPlaying)
            return;

        double virtualSongPosition = CurrentSongPosition();
        if (virtualSongPosition < 0.0)
        {
            _beatmapPlayer.Conductor?.Pause();
            double nextVirtualSongPosition = virtualSongPosition + gameTime.ElapsedGameTime.TotalSeconds;
            if (nextVirtualSongPosition >= 0.0)
            {
                _beatmapPlayer.Conductor?.Seek(nextVirtualSongPosition);
                _beatmapPlayer.Conductor?.Play();
            }

            _manualBeatPosition = ClampBeat(_document.GetBeatAt(nextVirtualSongPosition));
            UpdateEditorPlaybackSystems(nextVirtualSongPosition, seekChartPlayer: false, resetVisuals: false);
            return;
        }

        _beatmapPlayer.Conductor?.Update();
        double songPosition = Math.Max(0, _beatmapPlayer.Conductor?.SongPosition ?? virtualSongPosition);
        _manualBeatPosition = ClampBeat(_document.GetBeatAt(songPosition));
        UpdateEditorPlaybackSystems(songPosition, seekChartPlayer: false, resetVisuals: false);
    }

    private void SyncPlaybackToEditorPosition(double songPosition, bool resetVisuals)
    {
        SyncPlaybackToEditorBeatPosition(_document.GetBeatAt(songPosition), resetVisuals);
    }

    private void SyncPlaybackToEditorBeatPosition(double beat, bool resetVisuals)
    {
        _manualBeatPosition = ClampBeat(beat);
        double targetPosition = _document.GetSongPositionAtBeat(_manualBeatPosition);
        double audioPosition = Math.Max(0, targetPosition);
        _beatmapPlayer.Conductor?.Seek(audioPosition);

        if (targetPosition < 0.0)
            _beatmapPlayer.Conductor?.Pause();

        double syncedPosition = targetPosition < 0.0
            ? targetPosition
            : Math.Max(0, _beatmapPlayer.Conductor?.SongPosition ?? audioPosition);
        _manualBeatPosition = ClampBeat(_document.GetBeatAt(syncedPosition));
        UpdateEditorPlaybackSystems(syncedPosition, seekChartPlayer: true, resetVisuals: resetVisuals);
    }

    private void StartEditorAudioIfAtOrAfterSongStart()
    {
        if (CurrentSongPosition() >= 0.0)
            _beatmapPlayer.Conductor?.Play();
        else
            _beatmapPlayer.Conductor?.Pause();
    }

    private void UpdateEditorPlaybackSystems(double songPosition, bool seekChartPlayer, bool resetVisuals)
    {
        _beatmapPlayer.ApplyChartEffectsAt(songPosition);

        if (seekChartPlayer)
            _beatmapPlayer.ChartPlayer?.Seek(songPosition);
        else
            _beatmapPlayer.ChartPlayer?.Update(songPosition);

        if (resetVisuals)
            _beatmapPlayer.VisualNoteMng?.Reset();

        _beatmapPlayer.VisualNoteMng?.Update(songPosition);
    }

    private void Select(EditorNoteKind kind)
    {
        _selectedKind = kind;
        _status = $"Selected {EditorNoteDefinitions.Get(kind).DisplayName}";
    }

    private void Select(EditorEffectKind kind)
    {
        _selectedEffectKind = kind;
        _status = $"Selected effect {EditorEffectDefinitions.Get(kind).DisplayName}";
    }

    private void SelectRelative(int delta)
    {
        if (_placementMode == EditorPlacementMode.Effect)
        {
            IReadOnlyList<EditorEffectDefinition> effects = EditorEffectDefinitions.All;
            int currentEffectIndex = effects.ToList().FindIndex(definition => definition.Kind == _selectedEffectKind);
            int nextEffectIndex = PositiveModulo(currentEffectIndex + delta, effects.Count);
            Select(effects[nextEffectIndex].Kind);
            return;
        }

        IReadOnlyList<EditorNoteDefinition> definitions = EditorNoteDefinitions.All;
        int currentIndex = definitions.ToList().FindIndex(definition => definition.Kind == _selectedKind);
        int nextIndex = PositiveModulo(currentIndex + delta, definitions.Count);
        Select(definitions[nextIndex].Kind);
    }

    private void RefreshSongs()
    {
        _availableSongs.Clear();
        if (Directory.Exists("Songs"))
        {
            _availableSongs.AddRange(Directory.GetFiles("Songs", "*.*", SearchOption.AllDirectories)
                .Where(path => path.EndsWith(".wav", StringComparison.OrdinalIgnoreCase)
                    || path.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase)
                    || path.EndsWith(".ogg", StringComparison.OrdinalIgnoreCase))
                .OrderBy(path => path));
        }

        SyncSelectedSongIndex();
    }

    private void SyncSelectedSongIndex()
    {
        if (_document == null || _availableSongs.Count == 0)
        {
            _selectedSongIndex = 0;
            return;
        }

        int index = _availableSongs.FindIndex(path => string.Equals(NormalizePath(path), NormalizePath(_document.SongPath), StringComparison.OrdinalIgnoreCase));
        _selectedSongIndex = index >= 0 ? index : 0;
    }

    private void RefreshCharts()
    {
        _availableCharts.Clear();
        if (Directory.Exists("Beatmaps"))
        {
            _availableCharts.AddRange(Directory.GetFiles("Beatmaps", "*.xml", SearchOption.AllDirectories)
                .OrderBy(path => path));
        }

        if (_availableCharts.Count == 0)
            _availableCharts.Add(_defaultChartPath);

        SyncSelectedChartIndex();
    }

    private void SyncSelectedChartIndex()
    {
        if (_document == null || _availableCharts.Count == 0)
        {
            _selectedChartIndex = 0;
            return;
        }

        int index = _availableCharts.FindIndex(path => string.Equals(NormalizePath(path), NormalizePath(_document.ChartPath), StringComparison.OrdinalIgnoreCase));
        _selectedChartIndex = index >= 0 ? index : 0;
    }

    private void SelectChartRelative(int delta)
    {
        if (_availableCharts.Count == 0)
            RefreshCharts();

        _selectedChartIndex = PositiveModulo(_selectedChartIndex + delta, _availableCharts.Count);
        string chartPath = _availableCharts[_selectedChartIndex];
        _document.SetChartPath(chartPath);
        _status = $"Chart {GetChartDisplayName(chartPath)}";
    }

    private void SelectSongRelative(int delta)
    {
        if (_availableSongs.Count == 0)
            RefreshSongs();

        if (_availableSongs.Count == 0)
        {
            _status = "No songs found";
            return;
        }

        _selectedSongIndex = PositiveModulo(_selectedSongIndex + delta, _availableSongs.Count);
        string songPath = _availableSongs[_selectedSongIndex];
        if (ExecuteCommand(new SetSongPathCommand(songPath)))
            _status = $"Song {Path.GetFileName(songPath)}";
    }

    private void SelectNextMetadataField()
    {
        _selectedMetadataField = PositiveModulo(_selectedMetadataField + 1, _metadataFields.Length);
        _status = $"Metadata field {_metadataFields[_selectedMetadataField]}";
    }

    private bool TrySelectNextFirstOptionsDropdownValue()
    {
        if (!_noteOptionsWindow.IsOpen)
            return false;

        foreach (DevUiWindowRow row in GetNoteOptionRows())
        {
            if (row.Kind != DevUiWindowRowKind.Dropdown || row.Options == null || row.Options.Count == 0)
                continue;

            int nextIndex = PositiveModulo(row.SelectedIndex + 1, row.Options.Count);
            row.Select?.Invoke(nextIndex);
            _status = $"{row.Text}: {row.Options[nextIndex]}";
            return true;
        }

        return false;
    }

    private void BeginMetadataEdit()
    {
        _textBuffer = GetMetadataValue(_metadataFields[_selectedMetadataField]);
        _isEditingText = true;
        _status = $"Editing {_metadataFields[_selectedMetadataField]}";
    }

    private void HandleTextEdit()
    {
        if (Pressed(Keys.Enter))
        {
            CommitMetadataEdit();
            return;
        }

        if (Pressed(Keys.Escape))
        {
            _isEditingText = false;
            _status = "Edit cancelled";
            return;
        }

        if (Pressed(Keys.Back) && _textBuffer.Length > 0)
            _textBuffer = _textBuffer[..^1];

        foreach (Keys key in _keyboard.GetPressedKeys())
        {
            if (_previousKeyboard.IsKeyDown(key))
                continue;

            if (TryKeyToChar(key, out char c))
                _textBuffer += c;
        }
    }

    private void CommitMetadataEdit()
    {
        string field = _metadataFields[_selectedMetadataField];
        bool updated = false;
        if (field == "BPM" && double.TryParse(_textBuffer, out double bpm))
            updated = ExecuteCommand(new SetBpmCommand(bpm));
        else if (field == "Offset" && double.TryParse(_textBuffer, out double offset))
            updated = ExecuteCommand(new SetOffsetCommand(offset));
        else if (field == "LeadInBeats" && double.TryParse(_textBuffer, out double leadInBeats))
            updated = ExecuteCommand(new SetLeadInBeatsCommand(leadInBeats));
        else if (field == "BeatmapName")
            updated = ExecuteCommand(new SetMetadataCommand(EditorMetadataField.BeatmapName, _textBuffer), rebuildPlayback: false);
        else if (field == "Beatmapper")
            updated = ExecuteCommand(new SetMetadataCommand(EditorMetadataField.Beatmapper, _textBuffer), rebuildPlayback: false);
        else if (field == "ArtistName")
            updated = ExecuteCommand(new SetMetadataCommand(EditorMetadataField.ArtistName, _textBuffer), rebuildPlayback: false);
        else if (field == "MusicName")
            updated = ExecuteCommand(new SetMetadataCommand(EditorMetadataField.MusicName, _textBuffer), rebuildPlayback: false);

        _isEditingText = false;
        if (updated)
            _status = $"Updated {field}";
    }

    private void Zoom(double delta)
    {
        _visibleBeforeBeats = Math.Clamp(_visibleBeforeBeats + delta, 1, 32);
        _visibleAfterBeats = Math.Clamp(_visibleAfterBeats + delta, 1, 32);
        _status = $"Timeline window {(_visibleBeforeBeats + _visibleAfterBeats):0.0}b";
    }

    private double CurrentSongPosition()
    {
        return _document == null ? 0 : _document.GetSongPositionAtBeat(_manualBeatPosition);
    }

    private double CurrentBeatPosition()
    {
        return _manualBeatPosition;
    }

    private bool HasPlayableSong()
    {
        return !string.IsNullOrWhiteSpace(_document?.SongPath) && File.Exists(_document.SongPath);
    }

    private double GetSelectionDistance()
    {
        return 1.0 / GetEffectiveSnapDivisions();
    }

    private double GetSteppedSeekPosition(int direction)
    {
        if (IsShiftDown())
        {
            double beat = CurrentBeatPosition() + direction * GetShiftSeekStepBeats();
            return _document.GetSongPositionAtBeat(ClampBeat(beat));
        }

        double divisions = GetEffectiveSnapDivisions();
        double currentBeat = CurrentBeatPosition();
        if (double.IsNaN(currentBeat) || double.IsInfinity(currentBeat))
            return ClampSongPosition(CurrentSongPosition());

        double snappedCurrentBeat = QuantizeBeat(currentBeat, divisions);
        double targetBeat = snappedCurrentBeat + direction * (1.0 / divisions);
        return ClampSongPosition(_document.GetSongPositionAtBeat(targetBeat));
    }

    private void HandleHeldArrowSeek(GameTime gameTime, Keys key, int direction, ref double heldSeconds, ref bool repeated)
    {
        if (!IsDown(key))
        {
            heldSeconds = 0;
            repeated = false;
            return;
        }

        if (Pressed(key))
        {
            heldSeconds = 0;
            repeated = false;
            return;
        }

        heldSeconds += gameTime.ElapsedGameTime.TotalSeconds;
        double threshold = repeated ? HeldArrowSeekRepeatSeconds : HeldArrowSeekInitialDelaySeconds;
        if (heldSeconds < threshold)
            return;

        heldSeconds -= threshold;
        repeated = true;
        Seek(GetSteppedSeekPosition(direction));
    }

    private double GetShiftSeekStep()
    {
        double songDuration = GetSongDurationSeconds();
        return songDuration > 0 ? songDuration * ShiftSeekSongDurationRatio : _document.GetCrotchetAt(CurrentSongPosition());
    }

    private double GetShiftSeekStepBeats()
    {
        double songDuration = GetSongDurationSeconds();
        if (songDuration <= 0)
            return 1.0;

        double durationBeats = _document.GetBeatAt(songDuration);
        return Math.Max(1.0, durationBeats * ShiftSeekSongDurationRatio);
    }

    private double ClampSongPosition(double songPosition)
    {
        songPosition = Math.Max(GetMinimumNavigableSongPosition(), songPosition);

        double songDuration = GetSongDurationSeconds();
        return songDuration > 0 ? Math.Min(songPosition, songDuration) : songPosition;
    }

    private double ClampBeat(double beat)
    {
        beat = Math.Max(GetMinimumNavigableBeat(), beat);
        double songDuration = GetSongDurationSeconds();
        if (songDuration <= 0)
            return beat;

        return Math.Min(beat, _document.GetBeatAt(songDuration));
    }

    private double ClampNotePlacementBeat(double beat)
    {
        return ClampBeat(beat);
    }

    private double ClampEffectPlacementBeat(double beat)
    {
        return Math.Max(0.0, ClampBeat(beat));
    }

    private double GetMinimumNavigableBeat()
    {
        if (_document == null)
            return 0.0;

        double songStartBeat = _document.GetBeatAt(0.0);
        if (double.IsNaN(songStartBeat) || double.IsInfinity(songStartBeat))
            return 0.0;

        double leadInStartBeat = -ChartTiming.GetLeadInBeats(_document.Chart);
        return Math.Min(Math.Min(0.0, songStartBeat), leadInStartBeat);
    }

    private double GetMinimumNavigableSongPosition()
    {
        return _document == null ? 0.0 : _document.GetSongPositionAtBeat(GetMinimumNavigableBeat());
    }

    private double GetSongDurationSeconds()
    {
        return Math.Max(0, _beatmapPlayer.Conductor?.Duration ?? 0);
    }

    private double Snap(double songPosition)
    {
        return _document.GetSongPositionAtBeat(SnapBeat(_document.GetBeatAt(songPosition)));
    }

    private double SnapBeat(double beat)
    {
        if (!HasValidSnapDivisions() || double.IsNaN(beat) || double.IsInfinity(beat))
            return ClampBeat(beat);

        return ClampBeat(QuantizeBeat(beat, GetEffectiveSnapDivisions()));
    }

    private double SnapPlacementBeat(double beat)
    {
        if (!HasValidSnapDivisions() || double.IsNaN(beat) || double.IsInfinity(beat))
            return ClampNotePlacementBeat(beat);

        double divisions = GetEffectiveSnapDivisions();
        double quantizedBeat = QuantizeBeat(beat, divisions);
        if (beat < 0.0 && quantizedBeat >= 0.0)
        {
            double previousGridBeat = Math.Floor(beat / GetSnapStep(divisions)) * GetSnapStep(divisions);
            if (previousGridBeat >= GetMinimumNavigableBeat())
                return ClampNotePlacementBeat(previousGridBeat);

            return ClampNotePlacementBeat(beat);
        }

        return ClampNotePlacementBeat(quantizedBeat);
    }

    private double SnapEffectPlacementPosition(EditorEffectDefinition definition, double songPosition)
    {
        return _document.GetSongPositionAtBeat(SnapEffectPlacementBeat(definition, _document.GetBeatAt(songPosition)));
    }

    private double SnapEffectPlacementBeat(EditorEffectDefinition definition, double beat)
    {
        if (definition?.Kind != EditorEffectKind.BpmChange || double.IsNaN(beat) || double.IsInfinity(beat))
            return ClampEffectPlacementBeat(beat);

        return ClampEffectPlacementBeat(Math.Round(beat, MidpointRounding.AwayFromZero));
    }

    private bool HasValidSnapDivisions()
    {
        return !double.IsNaN(_snapDivisions) && !double.IsInfinity(_snapDivisions) && _snapDivisions > 0;
    }

    private double GetEffectiveSnapDivisions()
    {
        return HasValidSnapDivisions() ? _snapDivisions : 1.0;
    }

    private static double QuantizeBeat(double beat, double divisions)
    {
        double step = GetSnapStep(divisions);
        return Math.Round(beat / step, MidpointRounding.AwayFromZero) * step;
    }

    private static double GetSnapStep(double divisions)
    {
        return 1.0 / Math.Max(1.0, divisions);
    }

    private void DrawEditorShell(SpriteBatch spriteBatch)
    {
        BeatmapEditorLayout layout = GetEditorLayout();
        string dirty = _document.IsDirty ? "DIRTY" : "SAVED";
        string playing = _editorPlaybackPlaying ? "PLAY" : "PAUSE";

        _ui.Fill(spriteBatch, layout.TopBar, new Color(4, 6, 10, 245));
        _ui.Stroke(spriteBatch, layout.TopBar, Color.DarkSlateGray, 1);
        _ui.Label(spriteBatch, "FILE", new Vector2(6, 9), Color.LightGreen, 2);
        _ui.Label(spriteBatch, "ACTIONS", new Vector2(58, 9), Color.LightGreen, 2);
        _ui.Label(spriteBatch, "DATA", new Vector2(138, 9), Color.LightGreen, 2);
        _ui.Label(spriteBatch, "TOOLS", new Vector2(194, 9), Color.LightGreen, 2);
        _ui.Label(spriteBatch, $"{playing} {dirty} BEAT:{CurrentBeatPosition():0.00} BPM:{_document.GetBpmAtBeat(CurrentBeatPosition()):0.##} {_status}", new Vector2(270, 9), Color.White, 2);

        DrawPanel(spriteBatch, layout.RhythmGameListPanel, "RHYTHM GAMES");
        DrawPanel(spriteBatch, layout.PalettePanel, _noteOptionsWindow.IsOpen ? "OPTIONS" : "CLIP PALETTE");

        int gameY = layout.RhythmGameListPanel.Y + 30;
        _ui.Label(spriteBatch, "SEE SAW", new Vector2(layout.RhythmGameListPanel.X + 6, gameY), _selectedKind == EditorNoteKind.SeeSaw ? Color.LightGreen : Color.White, 2);
        _ui.Label(spriteBatch, "SEAPONY PARADE", new Vector2(layout.RhythmGameListPanel.X + 6, gameY + 22), _selectedKind == EditorNoteKind.SeaponyParade ? Color.LightGreen : Color.White, 2);

        IReadOnlyList<EditorClipDefinition> paletteClips = GetPaletteClipDefinitions();
        for (int i = 0; i < paletteClips.Count; i++)
        {
            EditorClipDefinition clipDefinition = paletteClips[i];
            Color color = GetClipColor(clipDefinition);
            Rectangle rowBounds = GetPaletteClipBounds(i);
            Rectangle clipPreview = new(rowBounds.X, rowBounds.Y + 4, 46, 24);
            _ui.Fill(spriteBatch, clipPreview, color * 0.78f);
            _ui.Fill(spriteBatch, new Rectangle(clipPreview.X, clipPreview.Y, 6, clipPreview.Height), color);
            _ui.Stroke(spriteBatch, clipPreview, Color.White, 1);
            _ui.Label(spriteBatch, clipDefinition.DisplayName, new Vector2(rowBounds.X + 56, rowBounds.Y + 11), Color.White, 2);
        }
    }

    private void DrawPanel(SpriteBatch spriteBatch, Rectangle bounds, string title)
    {
        _ui.Fill(spriteBatch, bounds, new Color(8, 10, 14, 180));
        _ui.Stroke(spriteBatch, bounds, Color.DarkSlateGray, 1);
        _ui.Fill(spriteBatch, new Rectangle(bounds.X, bounds.Y, bounds.Width, 26), new Color(18, 36, 24, 200));
        _ui.Label(spriteBatch, title, new Vector2(bounds.X + 6, bounds.Y + 8), Color.LightGreen, 2);
    }

    private IReadOnlyList<EditorClipDefinition> GetPaletteClipDefinitions()
    {
        string gameId = _selectedKind == EditorNoteKind.SeeSaw
            ? EditorClipDefinitions.SeeSawGameId
            : EditorClipDefinitions.SeaponyParadeGameId;

        return EditorClipDefinitions.Games
            .FirstOrDefault(game => game.Id == gameId)?.Clips
            ?? Array.Empty<EditorClipDefinition>();
    }

    private Rectangle GetPaletteClipBounds(int index)
    {
        Rectangle panel = GetEditorLayout().PalettePanel;
        return new Rectangle(panel.X + 8, panel.Y + 30 + index * 34, Math.Max(1, panel.Width - 16), 32);
    }

    private bool TryHitPaletteClip(out EditorClipDefinition clipDefinition)
    {
        IReadOnlyList<EditorClipDefinition> clips = GetPaletteClipDefinitions();
        for (int i = 0; i < clips.Count; i++)
        {
            if (!GetPaletteClipBounds(i).Contains(_mouse.Position))
                continue;

            clipDefinition = clips[i];
            return true;
        }

        clipDefinition = null;
        return false;
    }

    private void DrawTimeline(SpriteBatch spriteBatch)
    {
        Rectangle panel = GetTimelinePanelArea();
        Rectangle area = GetTimelineArea();
        double current = CurrentBeatPosition();
        GetTimelineWindow(out double start, out double end);
        GetSongWindowForBeatWindow(start, end, out double songStart, out double songEnd);

        _ui.Fill(spriteBatch, panel, new Color(12, 14, 20, 235));
        _ui.Stroke(spriteBatch, panel, Color.White, 1);
        DrawTimelineTracks(spriteBatch, panel, area);

        DrawTempoGrid(spriteBatch, start, end, area);
        DrawEditorClips(spriteBatch, start, end, area);
        DrawDraggingClipPreview(spriteBatch, start, end, area);

        if (_isSelectingIntervalRange && _intervalRangeStart is double intervalStart)
        {
            float intervalStartX = BeatToX(intervalStart, start, end, area);
            float intervalEndX = BeatToX(SnapPlacementBeat(current), start, end, area);
            _ui.Line(spriteBatch, new Vector2(intervalStartX, area.Y + 18), new Vector2(intervalEndX, area.Y + 18), Color.LightGreen, 3);
            _ui.Fill(spriteBatch, new Rectangle((int)intervalStartX - 4, area.Y + 10, 8, 16), Color.LightGreen);
            _ui.Fill(spriteBatch, new Rectangle((int)intervalEndX - 4, area.Y + 10, 8, 16), Color.LightGreen * 0.7f);
        }

        DrawEffects(spriteBatch, start, end, area);
        DrawIntervalPreview(spriteBatch, start, end, area);

        float playheadX = BeatToX(current, start, end, area);
        _ui.Line(spriteBatch, new Vector2(playheadX, area.Y - 10), new Vector2(playheadX, area.Bottom + 10), Color.Red, 3);
    }

    private void DrawTimelineTracks(SpriteBatch spriteBatch, Rectangle panel, Rectangle lanesArea)
    {
        Rectangle corner = new(panel.X, panel.Y, TimelineTrackLabelWidth, TimelineHeaderHeight);
        Rectangle header = new(lanesArea.X, panel.Y, lanesArea.Width, TimelineHeaderHeight);
        _ui.Fill(spriteBatch, corner, new Color(18, 36, 24, 245));
        _ui.Fill(spriteBatch, header, new Color(14, 18, 26, 245));
        _ui.Stroke(spriteBatch, corner, Color.DarkSlateGray, 1);
        _ui.Stroke(spriteBatch, header, Color.DarkSlateGray, 1);
        _ui.Label(spriteBatch, "TRACKS", new Vector2(corner.X + 4, corner.Y + 8), Color.LightGreen, 2);

        int laneHeight = GetTrackLaneHeight(lanesArea);
        IReadOnlyList<ChartEditorTrack> tracks = _document.EditorTracks;
        for (int i = 0; i < TimelineTrackCount; i++)
        {
            int y = lanesArea.Y + i * laneHeight;
            int height = i == TimelineTrackCount - 1 ? lanesArea.Bottom - y : laneHeight;
            Rectangle labelBounds = new(panel.X, y, TimelineTrackLabelWidth, height);
            Rectangle laneBounds = new(lanesArea.X, y, lanesArea.Width, height);
            Color laneColor = i % 2 == 0 ? new Color(16, 19, 28, 230) : new Color(11, 14, 22, 230);
            _ui.Fill(spriteBatch, labelBounds, new Color(8, 10, 14, 245));
            _ui.Fill(spriteBatch, laneBounds, laneColor);
            _ui.Stroke(spriteBatch, labelBounds, Color.DarkSlateGray, 1);
            _ui.Line(spriteBatch, new Vector2(lanesArea.X, laneBounds.Bottom - 1), new Vector2(lanesArea.Right, laneBounds.Bottom - 1), Color.DarkSlateGray, 1);

            string trackName = i < tracks.Count && !string.IsNullOrWhiteSpace(tracks[i].Name)
                ? tracks[i].Name
                : $"Track {i + 1}";
            _ui.Label(spriteBatch, trackName, new Vector2(labelBounds.X + 8, labelBounds.Y + Math.Max(4, height / 2 - 6)), Color.White, 2);
        }
    }

    private void DrawEditorClips(SpriteBatch spriteBatch, double windowStart, double windowEnd, Rectangle area)
    {
        if (_document?.EditorClips == null || _document.EditorClips.Count == 0)
            return;

        foreach (ChartEditorClip clip in _document.EditorClips)
        {
            if (clip == null)
                continue;

            Rectangle bounds = GetClipBounds(clip, windowStart, windowEnd, area);
            if (bounds.Right < area.X || bounds.X > area.Right)
                continue;

            EditorClipDefinition definition = EditorClipDefinitions.Find(clip.RhythmGameId, clip.ClipTypeId);
            Color color = GetClipColor(definition);
            _ui.Fill(spriteBatch, bounds, color * 0.78f);
            _ui.Stroke(spriteBatch, bounds, Color.White, 1);
            _ui.Fill(spriteBatch, new Rectangle(bounds.X, bounds.Y, Math.Min(5, bounds.Width), bounds.Height), color);
            DrawClipResizeHandles(spriteBatch, bounds, Color.White * 0.8f);

            if (bounds.Width > 58)
                _ui.Label(spriteBatch, definition?.DisplayName ?? clip.ClipTypeId ?? "Clip", new Vector2(bounds.X + 8, bounds.Y + Math.Max(4, bounds.Height / 2 - 6)), Color.White, 2);
        }
    }

    private void DrawDraggingClipPreview(SpriteBatch spriteBatch, double windowStart, double windowEnd, Rectangle area)
    {
        if (_timelineDragKind != EditorTimelineDragKind.ClipCreate
            && _timelineDragKind != EditorTimelineDragKind.ClipMove
            && _timelineDragKind != EditorTimelineDragKind.ClipResizeStart
            && _timelineDragKind != EditorTimelineDragKind.ClipResizeEnd)
            return;

        EditorClipDefinition definition = _draggedClipDefinition;
        if (definition == null)
            return;

        Rectangle bounds = GetClipBounds(_dragPreviewStartBeat, _dragPreviewLengthBeats, _dragPreviewTrackIndex, windowStart, windowEnd, area);
        if (bounds.Right < area.X || bounds.X > area.Right)
            return;

        Color color = GetClipColor(definition);
        _ui.Fill(spriteBatch, bounds, color * 0.45f);
        _ui.Stroke(spriteBatch, bounds, Color.Yellow, 2);
        _ui.Fill(spriteBatch, new Rectangle(bounds.X, bounds.Y, Math.Min(5, bounds.Width), bounds.Height), color);
        DrawClipResizeHandles(spriteBatch, bounds, Color.Yellow);
        if (bounds.Width > 58)
            _ui.Label(spriteBatch, definition.DisplayName, new Vector2(bounds.X + 8, bounds.Y + Math.Max(4, bounds.Height / 2 - 6)), Color.White, 2);
    }

    private void DrawClipResizeHandles(SpriteBatch spriteBatch, Rectangle bounds, Color color)
    {
        int handleWidth = Math.Min(6, Math.Max(2, bounds.Width / 4));
        _ui.Fill(spriteBatch, new Rectangle(bounds.X, bounds.Y, handleWidth, bounds.Height), color * 0.5f);
        _ui.Fill(spriteBatch, new Rectangle(bounds.Right - handleWidth, bounds.Y, handleWidth, bounds.Height), color * 0.5f);
    }

    private Rectangle GetClipBounds(ChartEditorClip clip, double windowStart, double windowEnd, Rectangle area)
    {
        if (clip == null)
            return Rectangle.Empty;

        return GetClipBounds(clip.StartBeat, clip.LengthBeats, clip.TrackIndex, windowStart, windowEnd, area);
    }

    private Rectangle GetClipBounds(double startBeat, double lengthBeats, int trackIndex, double windowStart, double windowEnd, Rectangle area)
    {
        double clipEnd = startBeat + Math.Max(lengthBeats, 1.0);
        int laneHeight = GetTrackLaneHeight(area);
        int clampedTrack = Math.Clamp(trackIndex, 0, TimelineTrackCount - 1);
        float startX = BeatToX(startBeat, windowStart, windowEnd, area);
        float endX = BeatToX(clipEnd, windowStart, windowEnd, area);
        int x = (int)Math.Clamp(Math.Min(startX, endX), area.X, area.Right);
        int right = (int)Math.Clamp(Math.Max(startX, endX), area.X, area.Right);
        int laneY = area.Y + clampedTrack * laneHeight;
        int laneBottom = clampedTrack == TimelineTrackCount - 1 ? area.Bottom : laneY + laneHeight;
        return new Rectangle(x, laneY + 3, Math.Max(10, right - x), Math.Max(8, laneBottom - laneY - 6));
    }

    private bool TryHitTimelineClip(Rectangle area, double windowStart, double windowEnd, out ChartEditorClip clip, out EditorTimelineDragKind dragKind)
    {
        foreach (ChartEditorClip candidate in _document.EditorClips.Reverse())
        {
            if (candidate == null)
                continue;

            Rectangle bounds = GetClipBounds(candidate, windowStart, windowEnd, area);
            if (!bounds.Contains(_mouse.Position))
                continue;

            int handleWidth = Math.Min(10, Math.Max(6, bounds.Width / 5));
            if (_mouse.X <= bounds.X + handleWidth)
                dragKind = EditorTimelineDragKind.ClipResizeStart;
            else if (_mouse.X >= bounds.Right - handleWidth)
                dragKind = EditorTimelineDragKind.ClipResizeEnd;
            else
                dragKind = EditorTimelineDragKind.ClipMove;

            clip = candidate;
            return true;
        }

        clip = null;
        dragKind = EditorTimelineDragKind.None;
        return false;
    }

    private int GetTrackIndexAtY(int y, Rectangle area)
    {
        int laneHeight = GetTrackLaneHeight(area);
        return Math.Clamp((y - area.Y) / Math.Max(1, laneHeight), 0, TimelineTrackCount - 1);
    }

    private bool CreateDroppedClip(EditorClipDefinition definition, double startBeat, double lengthBeats, int trackIndex)
    {
        if (definition == null)
            return false;

        ChartEditorClip clip = new()
        {
            Id = Guid.NewGuid().ToString("N"),
            TrackIndex = Math.Clamp(trackIndex, 0, TimelineTrackCount - 1),
            StartBeat = startBeat,
            LengthBeats = Math.Max(0.0, lengthBeats),
            RhythmGameId = definition.RhythmGameId,
            ClipTypeId = definition.ClipTypeId,
            ClipCategory = definition.Category.ToString(),
            InputAction = definition.InputAction,
            Data = new Dictionary<string, string>(definition.DefaultData ?? new Dictionary<string, string>())
        };

        return ExecuteCommand(new CreateClipCommand(clip));
    }

    private static int GetTrackLaneHeight(Rectangle area)
    {
        return Math.Max(1, area.Height / TimelineTrackCount);
    }

    private void DrawTempoGrid(SpriteBatch spriteBatch, double windowStart, double windowEnd, Rectangle area)
    {
        double divisions = GetEffectiveSnapDivisions();
        double step = 1.0 / divisions;
        double first = Math.Ceiling(windowStart / step) * step;

        for (double beat = first; beat <= windowEnd; beat += step)
        {
            bool wholeBeat = Math.Abs(beat - Math.Round(beat)) <= 0.000001;
            float x = BeatToX(beat, windowStart, windowEnd, area);
            _ui.Line(spriteBatch, new Vector2(x, area.Y), new Vector2(x, area.Bottom), wholeBeat ? Color.DimGray : Color.DarkSlateGray, wholeBeat ? 2 : 1);
        }

        DrawTempoAnchor(spriteBatch, windowStart, windowEnd, area, 0.0);

        GetSongWindowForBeatWindow(windowStart, windowEnd, out double songStart, out double songEnd);
        foreach (ChartEffect effect in _document.GetEffectsInWindow(songStart, songEnd))
            DrawTempoAnchor(spriteBatch, windowStart, windowEnd, area, _document.GetEffectBeat(effect));
    }

    private void DrawTempoAnchor(SpriteBatch spriteBatch, double windowStart, double windowEnd, Rectangle area, double beat)
    {
        if (beat < windowStart || beat > windowEnd)
            return;

        float x = BeatToX(beat, windowStart, windowEnd, area);
        _ui.Line(spriteBatch, new Vector2(x, area.Y - 6), new Vector2(x, area.Bottom + 6), Color.Yellow, 4);
    }

    private void DrawVisualSectionGridLines(SpriteBatch spriteBatch, double windowStart, double windowEnd, Rectangle area, EditorTempoSegment segment, double stepSeconds, Color color, int yStart, int yEnd, int thickness)
    {
        if (stepSeconds <= 0 || double.IsNaN(stepSeconds) || double.IsInfinity(stepSeconds))
            return;

        double start = Math.Max(windowStart, segment.StartSongPosition);
        double end = Math.Min(windowEnd, segment.EndSongPosition);
        double anchor = segment.AnchorSongPosition;
        double first = anchor + Math.Ceiling((start - anchor) / stepSeconds) * stepSeconds;

        for (double songPosition = first; songPosition <= end; songPosition += stepSeconds)
        {
            float x = TimeToX(songPosition, windowStart, windowEnd, area);
            if (x < area.X || x > area.Right)
                continue;

            _ui.Line(spriteBatch, new Vector2(x, yStart), new Vector2(x, yEnd), color, thickness);
        }
    }

    private void DrawEffects(SpriteBatch spriteBatch, double windowStart, double windowEnd, Rectangle area)
    {
        GetSongWindowForBeatWindow(windowStart, windowEnd, out double songStart, out double songEnd);
        foreach (ChartEffect effect in _document.GetEffectsInWindow(songStart, songEnd))
        {
            EditorEffectDefinition definition = EditorEffectDefinitions.FromChartEffect(effect);
            if (definition == null)
                continue;

            float effectX = BeatToX(_document.GetEffectBeat(effect), windowStart, windowEnd, area);
            Color color = GetEffectColor(definition.Kind);
            Rectangle marker = GetEffectMarkerBounds(effect, windowStart, windowEnd, area);
            _ui.Line(spriteBatch, new Vector2(effectX, area.Y + 4), new Vector2(effectX, area.Bottom - 10), color * 0.35f, 2);
            _ui.Fill(spriteBatch, marker, color * 0.9f);
            _ui.Stroke(spriteBatch, marker, Color.White, 1);
            _ui.Label(spriteBatch, GetEffectLabel(effect, definition), new Vector2(effectX + 10, area.Y + 24), color, 1);
        }

        if (_optionsIsEffect && _optionsIsCreation && _optionsEffect != null && _document.GetEffectBeat(_optionsEffect) >= windowStart && _document.GetEffectBeat(_optionsEffect) <= windowEnd)
        {
            EditorEffectDefinition definition = _optionsEffectDefinition ?? EditorEffectDefinitions.FromChartEffect(_optionsEffect);
            if (definition == null)
                return;

            float effectX = BeatToX(_document.GetEffectBeat(_optionsEffect), windowStart, windowEnd, area);
            Color color = GetEffectColor(definition.Kind);
            Rectangle marker = new((int)effectX - 8, area.Y + 18, 16, 34);
            _ui.Line(spriteBatch, new Vector2(effectX, area.Y + 4), new Vector2(effectX, area.Bottom - 10), Color.White * 0.45f, 2);
            _ui.Stroke(spriteBatch, marker, color, 2);
            _ui.Label(spriteBatch, GetEffectLabel(_optionsEffect, definition), new Vector2(effectX + 10, area.Y + 24), Color.White, 1);
        }
    }

    private void DrawIntervalPreview(SpriteBatch spriteBatch, double windowStart, double windowEnd, Rectangle area)
    {
        if (!_optionsIsCreation || !_optionsIsIntervalCreation || _optionsNote == null || _optionsDefinition == null)
            return;

        IReadOnlyList<EditorNotePlacement> placements = _optionsDefinition.CreatePlacements(_optionsNote, CreatePlacementContext());
        foreach (EditorNotePlacement placement in placements)
        {
            ChartNote note = placement.Note;
            double noteBeat = _document.GetNoteBeat(note);
            if (noteBeat < windowStart || noteBeat > windowEnd)
                continue;

            int variantIndex = EditorNoteDefinitions.FindVariantIndex(placement.Definition, note);
            Color color = GetNoteColor(placement.Definition.Kind, variantIndex);
            double noteEndBeat = Math.Max(noteBeat + 1.0, noteBeat + Math.Max(0.0, ChartTiming.GetNoteHoldBeats(note, placement.Definition, _document.TempoMap)));
            float noteX = BeatToX(noteBeat, windowStart, windowEnd, area);
            float noteEndX = BeatToX(noteEndBeat, windowStart, windowEnd, area);
            int laneHeight = GetTrackLaneHeight(area);
            int x = (int)Math.Clamp(Math.Min(noteX, noteEndX), area.X, area.Right);
            int right = (int)Math.Clamp(Math.Max(noteX, noteEndX), area.X, area.Right);
            Rectangle previewBounds = new(x, area.Y + 3, Math.Max(10, right - x), Math.Max(8, laneHeight - 6));
            _ui.Fill(spriteBatch, previewBounds, color * 0.45f);
            _ui.Stroke(spriteBatch, previewBounds, Color.White, 2);
        }
    }

    private void DrawHud(SpriteBatch spriteBatch)
    {
        _noteOptionsWindow.Draw(spriteBatch, GetNoteOptionsWindowBounds(), GetNoteOptionsTitle(), GetNoteOptionRows());
        _newBeatmapWindow.Draw(spriteBatch, GetNewBeatmapWindowBounds(), "NEW BEATMAP", GetNewBeatmapRows());
    }

    private string GetNoteOptionsTitle()
    {
        if (_optionsIsEffect)
            return _effectOptionsPanel?.Title ?? "EFFECT OPTIONS";

        if (_optionsIsIntervalCreation)
            return $"INTERVAL {_optionsDefinition?.DisplayName?.ToUpperInvariant() ?? "NOTE"}";

        return _optionsPanel?.Title ?? "NOTE OPTIONS";
    }

    private Rectangle GetTimelineArea()
    {
        Rectangle panel = GetTimelinePanelArea();
        return new Rectangle(
            panel.X + TimelineTrackLabelWidth,
            panel.Y + TimelineHeaderHeight,
            Math.Max(1, panel.Width - TimelineTrackLabelWidth),
            Math.Max(1, panel.Height - TimelineHeaderHeight));
    }

    private Rectangle GetTimelinePanelArea()
    {
        return GetEditorLayout().Timeline;
    }

    private BeatmapEditorLayout GetEditorLayout()
    {
        return new BeatmapEditorLayout(GLOBALS.graphicsDevice.Viewport);
    }

    private void GetTimelineWindow(out double start, out double end)
    {
        double current = CurrentBeatPosition();
        start = current - _visibleBeforeBeats;
        end = current + _visibleAfterBeats;
    }

    private void GetSongWindowForBeatWindow(double startBeat, double endBeat, out double startSongPosition, out double endSongPosition)
    {
        startSongPosition = _document.GetSongPositionAtBeat(startBeat);
        endSongPosition = _document.GetSongPositionAtBeat(endBeat);
        if (endSongPosition < startSongPosition)
            (startSongPosition, endSongPosition) = (endSongPosition, startSongPosition);
    }

    private float TimeToX(double songPosition, double start, double end, Rectangle area)
    {
        return BeatToX(_document.GetBeatAt(songPosition), start, end, area);
    }

    private float BeatToX(double beat, double start, double end, Rectangle area)
    {
        double t = (beat - start) / (end - start);
        return area.X + (float)(t * area.Width);
    }

    private double XToSongPosition(int x, double start, double end, Rectangle area)
    {
        return _document.GetSongPositionAtBeat(XToBeat(x, start, end, area));
    }

    private double XToBeat(int x, double start, double end, Rectangle area)
    {
        double clampedX = Math.Clamp(x, area.X, area.Right);
        double t = (clampedX - area.X) / area.Width;
        return start + t * (end - start);
    }

    private Rectangle GetNoteMarkerBounds(ChartNote note, double start, double end, Rectangle area)
    {
        EditorNoteDefinition definition = EditorNoteDefinitions.FromChartNote(note);
        double noteBeat = _document.GetNoteBeat(note);
        double noteEndBeat = Math.Max(noteBeat + 1.0, _document.GetContextualEndBeat(note, definition));
        float noteX = BeatToX(noteBeat, start, end, area);
        float noteEndX = BeatToX(noteEndBeat, start, end, area);
        int laneHeight = GetTrackLaneHeight(area);
        int x = (int)Math.Clamp(Math.Min(noteX, noteEndX), area.X, area.Right);
        int right = (int)Math.Clamp(Math.Max(noteX, noteEndX), area.X, area.Right);
        return new Rectangle(x, area.Y + 3, Math.Max(10, right - x), Math.Max(8, laneHeight - 6));
    }

    private Rectangle GetEffectMarkerBounds(ChartEffect effect, double start, double end, Rectangle area)
    {
        float effectX = BeatToX(_document.GetEffectBeat(effect), start, end, area);
        return new Rectangle((int)effectX - 7, area.Y + 20, 14, 30);
    }

    private Color GetNoteColor(EditorNoteKind kind, int variantIndex)
    {
        if (kind == EditorNoteKind.SeeSaw)
        {
            return variantIndex switch
            {
                0 or 3 => Color.Orange,
                1 or 4 => Color.MediumPurple,
                2 or 5 => Color.Gold,
                _ => Color.Orange
            };
        }

        return Color.DeepSkyBlue;
    }

    private Color GetEffectColor(EditorEffectKind kind)
    {
        return kind switch
        {
            EditorEffectKind.BpmChange => Color.Cyan,
            _ => Color.Cyan
        };
    }

    private Color GetClipColor(EditorClipDefinition definition)
    {
        if (definition == null)
            return Color.Gray;

        if (definition.RhythmGameId == EditorClipDefinitions.SeeSawGameId)
        {
            return definition.ClipTypeId switch
            {
                EditorClipDefinitions.SeeSawLongLong => Color.Orange,
                EditorClipDefinitions.SeeSawLongShort => Color.Gold,
                EditorClipDefinitions.SeeSawShortLong => Color.MediumPurple,
                EditorClipDefinitions.SeeSawShortShort => Color.LightSalmon,
                _ => Color.OrangeRed
            };
        }

        if (definition.ClipTypeId == EditorClipDefinitions.NoHit)
            return Color.DimGray;

        return definition.ClipTypeId switch
        {
            EditorClipDefinitions.SeaponyRoll => Color.DeepSkyBlue,
            EditorClipDefinitions.SeaponyTapTap => Color.LightBlue,
            _ => Color.CornflowerBlue
        };
    }

    private string GetEffectLabel(ChartEffect effect, EditorEffectDefinition definition)
    {
        if (definition.Kind == EditorEffectKind.BpmChange && effect.TryGetBpm(out double bpm))
            return $"BPM {bpm:0.##}";

        return definition.DisplayName;
    }

    private string GetSelectedNoteName(EditorNoteDefinition definition)
    {
        return definition.DisplayName;
    }

    private Rectangle GetNoteOptionsWindowBounds()
    {
        return GetEditorLayout().OptionsWindow;
    }

    private Rectangle GetNewBeatmapWindowBounds()
    {
        Viewport viewport = GLOBALS.graphicsDevice.Viewport;
        const int width = 420;
        const int height = 150;
        return new Rectangle((viewport.Width - width) / 2, (viewport.Height - height) / 2, width, height);
    }

    private IReadOnlyList<DevUiWindowRow> GetNewBeatmapRows()
    {
        return new[]
        {
            DevUiWindowRow.Title("Name: " + _newBeatmapNameBuffer + "|"),
            DevUiWindowRow.Title("ENTER or CREATE to confirm, ESC to cancel"),
            DevUiWindowRow.Button("CREATE", CreateNewBeatmap)
        };
    }

    private IReadOnlyList<DevUiWindowRow> GetNoteOptionRows()
    {
        if (_optionsIsEffect)
            return GetEffectOptionRows();

        if (_optionsNote == null)
            return Array.Empty<DevUiWindowRow>();

        EditorNoteOptionsContext context = new(_optionsNote, _document, GetNoteOptionsWindowBounds(), ResolveOptionsNote);
        List<DevUiWindowRow> rows = new();
        if (_optionsPanel != null)
            rows.AddRange(_optionsPanel.BuildRows(context));

        if (_optionsIsIntervalCreation)
            rows.AddRange(_intervalOptionsPanel.BuildRows(context));

        List<DevUiWindowRow> wrappedRows = new();

        if (_optionsIsCreation)
            wrappedRows.Add(DevUiWindowRow.Button(_optionsIsIntervalCreation ? "CREATE INTERVAL" : "CREATE", CreatePendingNote));

        for (int i = 0; i < rows.Count; i++)
            wrappedRows.Add(WrapNoteOptionRow(rows[i]));

        return wrappedRows;
    }

    private IReadOnlyList<DevUiWindowRow> GetEffectOptionRows()
    {
        if (_optionsEffect == null)
            return Array.Empty<DevUiWindowRow>();

        if (!_optionsIsCreation && !_document.Chart.Effects.Contains(_optionsEffect) && ResolveOptionsEffect() == null)
        {
            _noteOptionsWindow.Close();
            ClearPendingOptions();
            return Array.Empty<DevUiWindowRow>();
        }

        EditorEffectOptionsContext context = new(_optionsEffect, _document, GetNoteOptionsWindowBounds(), ResolveOptionsEffect);
        List<DevUiWindowRow> rows = new();
        if (_effectOptionsPanel != null)
            rows.AddRange(_effectOptionsPanel.BuildRows(context));

        List<DevUiWindowRow> wrappedRows = new();
        if (_optionsIsCreation)
            wrappedRows.Add(DevUiWindowRow.Button("CREATE EFFECT", CreatePendingNote));

        for (int i = 0; i < rows.Count; i++)
            wrappedRows.Add(WrapEffectOptionRow(rows[i]));

        return wrappedRows;
    }

    private DevUiWindowRow WrapNoteOptionRow(DevUiWindowRow row)
    {
        return row.Kind switch
        {
            DevUiWindowRowKind.Checkbox => DevUiWindowRow.Checkbox(row.Text, row.IsChecked, () => ApplyNoteOption(_ => row.Toggle?.Invoke())),
            DevUiWindowRowKind.Dropdown => DevUiWindowRow.Dropdown(row.Key, row.Text, row.Options, row.SelectedIndex, index => ApplyNoteOption(_ => row.Select?.Invoke(index))),
            DevUiWindowRowKind.FloatInput => DevUiWindowRow.FloatInput(row.Key, row.Text, row.FloatValue, value => ApplyNoteOption(_ => row.SetFloat?.Invoke(value))),
            DevUiWindowRowKind.Slider => DevUiWindowRow.Slider(row.Key, row.Text, row.FloatValue, row.MinValue, row.MaxValue, value => ApplyNoteOption(_ => row.SetFloat?.Invoke(value))),
            DevUiWindowRowKind.Stepper => DevUiWindowRow.Stepper(row.Key, row.Text, row.FloatValue, row.StepValue, value => ApplyNoteOption(_ => row.SetFloat?.Invoke(value))),
            _ => row
        };
    }

    private DevUiWindowRow WrapEffectOptionRow(DevUiWindowRow row)
    {
        return row.Kind switch
        {
            DevUiWindowRowKind.Checkbox => DevUiWindowRow.Checkbox(row.Text, row.IsChecked, () => ApplyEffectOption(_ => row.Toggle?.Invoke())),
            DevUiWindowRowKind.Dropdown => DevUiWindowRow.Dropdown(row.Key, row.Text, row.Options, row.SelectedIndex, index => ApplyEffectOption(_ => row.Select?.Invoke(index))),
            DevUiWindowRowKind.FloatInput => DevUiWindowRow.FloatInput(row.Key, row.Text, row.FloatValue, value => ApplyEffectOption(_ => row.SetFloat?.Invoke(value))),
            DevUiWindowRowKind.Slider => DevUiWindowRow.Slider(row.Key, row.Text, row.FloatValue, row.MinValue, row.MaxValue, value => ApplyEffectOption(_ => row.SetFloat?.Invoke(value))),
            DevUiWindowRowKind.Stepper => DevUiWindowRow.Stepper(row.Key, row.Text, row.FloatValue, row.StepValue, value => ApplyEffectOption(_ => row.SetFloat?.Invoke(value))),
            _ => row
        };
    }

    private bool Pressed(Keys key)
    {
        return _keyboard.IsKeyDown(key) && !_previousKeyboard.IsKeyDown(key);
    }

    private bool IsDown(Keys key)
    {
        return _keyboard.IsKeyDown(key);
    }

    private bool LeftPressed()
    {
        return _mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton == ButtonState.Released;
    }

    private bool IsControlDown()
    {
        return IsDown(Keys.LeftControl) || IsDown(Keys.RightControl);
    }

    private bool IsShiftDown()
    {
        return IsDown(Keys.LeftShift) || IsDown(Keys.RightShift);
    }

    private int PositiveModulo(int value, int count)
    {
        return ((value % count) + count) % count;
    }

    private string NormalizePath(string path)
    {
        return path.Replace('\\', '/');
    }

    private string GetMetadataValue(string field)
    {
        return field switch
        {
            "BeatmapName" => _document.Chart.BeatmapName ?? "",
            "Beatmapper" => _document.Chart.Beatmapper ?? "",
            "ArtistName" => _document.Chart.ArtistName ?? "",
            "MusicName" => _document.Chart.MusicName ?? "",
            "BPM" => _document.Chart.BPM.ToString("0.###"),
            "Offset" => _document.Chart.Offset.ToString("0.###"),
            "LeadInBeats" => _document.Chart.LeadInBeats.ToString("0.###"),
            _ => ""
        };
    }

    private bool TryKeyToChar(Keys key, out char c)
    {
        bool shift = IsDown(Keys.LeftShift) || IsDown(Keys.RightShift);
        c = '\0';

        if (key >= Keys.A && key <= Keys.Z)
        {
            c = (char)('a' + (key - Keys.A));
            if (shift)
                c = char.ToUpperInvariant(c);
            return true;
        }

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
            Keys.Space => ' ',
            Keys.OemPeriod or Keys.Decimal => '.',
            Keys.OemComma => ',',
            Keys.OemMinus or Keys.Subtract => '-',
            Keys.OemPlus or Keys.Add => '+',
            Keys.OemQuestion => '/',
            Keys.OemBackslash => '\\',
            Keys.OemSemicolon => ';',
            _ => '\0'
        };

        return c != '\0';
    }
}
