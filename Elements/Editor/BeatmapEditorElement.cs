using System;
using System.Collections.Generic;
using System.Globalization;
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

internal enum EditorToolMode
{
    Selection,
    BpmModification
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

internal enum EditorTopBarMenu
{
    None,
    File,
    Actions,
    Data,
    Tools
}

internal readonly struct EditorTopBarMenuItem
{
    public EditorTopBarMenuItem(string label, Action action)
    {
        Label = label;
        Action = action;
    }

    public string Label { get; }
    public Action Action { get; }
}

internal readonly struct ClipActivationRange
{
    public ClipActivationRange(double startBeat, double endBeat)
    {
        StartBeat = startBeat;
        EndBeat = endBeat;
    }

    public double StartBeat { get; }
    public double EndBeat { get; }
}

public sealed class BeatmapEditorElement
{
    private readonly BeatmapPlayer _beatmapPlayer;
    private readonly DevUiRenderer _ui;
    private readonly DevUiFloatingWindow _noteOptionsWindow;
    private readonly DevUiFloatingWindow _openBeatmapWindow;
    private readonly DevUiFloatingWindow _metadataWindow;
    private readonly BeatmapFolderExplorer _newBeatmapExplorer;
    private readonly BeatmapFolderExplorer _openBeatmapExplorer;
    private readonly Texture2D _pixel;
    private readonly string _defaultSongPath;
    private readonly string _defaultChartPath;
    private readonly EditorSettings _settings;
    private readonly double _snapDivisions;
    private readonly List<string> _availableSongs = new();
    private readonly List<string> _availableCharts = new();
    private readonly Dictionary<NoteTypeId, Dictionary<string, string>> _lastCreatedNoteData = new();
    private readonly Dictionary<string, ClipActivationRange> _clipActivationRangeCache = new();
    private PlacementOptions _lastIntervalPlacementOptions = new(IntervalEditorNoteProvider.DefaultDurationBeats, IntervalEditorNoteProvider.DefaultStepBeats);
    private readonly EditorCommandStack _commandStack = new();
    private readonly IEditorNoteOptionsPanel _intervalOptionsPanel = new IntervalEditorNoteOptionsPanel();
    private readonly string[] _metadataFields = { "BeatmapName", "Beatmapper", "ArtistName", "MusicName", "BPM", "Offset", "LeadInBeats", "MusicVolume" };
    private const float ScenePreviewScale = 0.5f;
    private const int TimelineTrackCount = 10;
    private const int TimelineTrackLabelWidth = 180;
    private const int TimelineHeaderHeight = 28;
    private const double ShiftSeekSongDurationRatio = 0.05;
    private const double HeldArrowSeekInitialDelaySeconds = 0.25;
    private const double HeldArrowSeekRepeatSeconds = 0.075;
    private const double ClipWindowCullPaddingBeats = 8.0;
    private const double PreviewAutoplayEpsilonSeconds = 0.000001;

    private BeatmapEditorDocument _document;
    private EditorRhythmInputVisualElement _rhythmVisuals;
    private KeyboardState _previousKeyboard;
    private KeyboardState _keyboard;
    private MouseState _previousMouse;
    private MouseState _mouse;
    private EditorPlacementMode _placementMode = EditorPlacementMode.Note;
    private EditorToolMode _toolMode = EditorToolMode.Selection;
    private NoteTypeId _selectedNoteTypeId;
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
    private bool _previewAutoplayEnabled;
    private int _previewAutoplayNextNoteIndex;
    private double _previewAutoplayLastSongPosition = double.NaN;
    private bool _editorPlaybackPlaying;
    private string _textBuffer = "";
    private ChartNote _optionsNote;
    private EditorNoteDefinition _optionsDefinition;
    private IEditorNoteOptionsPanel _optionsPanel;
    private ChartEditorClip _optionsClip;
    private EditorClipDefinition _optionsClipDefinition;
    private ChartEffect _optionsEffect;
    private EditorEffectDefinition _optionsEffectDefinition;
    private IEditorEffectOptionsPanel _effectOptionsPanel;
    private bool _optionsIsClip;
    private bool _optionsIsEffect;
    private bool _optionsIsCreation;
    private bool _optionsIsIntervalCreation;
    private bool _isSelectingIntervalRange;
    private double? _intervalRangeStart;
    private PlacementOptions _pendingPlacementOptions = PlacementOptions.None;
    private bool _isCreatingNewBeatmap;
    private string _newBeatmapNameBuffer = "";
    private string _newBeatmapFolderPath = BeatmapPackagePaths.BeatmapsRoot;
    private bool _newBeatmapFolderNameFocused;
    private int _newBeatmapFolderListScroll;
    private long _customTextBackspaceHoldStartMs;
    private long _customTextBackspaceLastRepeatMs;
    private ChartNote _draggedNote;
    private ChartEffect _draggedEffect;
    private ChartEditorClip _draggedClip;
    private EditorClipDefinition _draggedClipDefinition;
    private ChartNote _selectedTimelineNote;
    private ChartEffect _selectedTimelineEffect;
    private string _selectedTimelineClipId;
    private ChartEditorClip _clipboardClip;
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
    private EditorTopBarMenu _openTopBarMenu;
    private double _heldLeftSeekSeconds;
    private double _heldRightSeekSeconds;
    private bool _leftSeekRepeated;
    private bool _rightSeekRepeated;
    private bool _optionsOpenedFromTimelineContext;

    public BeatmapEditorElement(BeatmapPlayer beatmapPlayer, string songPath = "", string chartPath = "Beatmaps/editor_beatmap/chart.xml", double firstBeatDelay = 0.078, double snapDivisions = 4)
    {
        _beatmapPlayer = beatmapPlayer;
        _defaultSongPath = songPath;
        _defaultChartPath = chartPath;
        _snapDivisions = snapDivisions;
        _settings = EditorSettings.Load();
        _ui = new DevUiRenderer(GLOBALS.graphicsDevice);
        _noteOptionsWindow = new DevUiFloatingWindow(_ui);
        _openBeatmapWindow = new DevUiFloatingWindow(_ui);
        _metadataWindow = new DevUiFloatingWindow(_ui);
        _newBeatmapExplorer = new BeatmapFolderExplorer(_ui);
        _openBeatmapExplorer = new BeatmapFolderExplorer(_ui);
        _pixel = new Texture2D(GLOBALS.graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _selectedNoteTypeId = GetDefaultNoteTypeId();

        Load();
    }

    private static NoteTypeId GetDefaultNoteTypeId()
    {
        return EditorNoteDefinitions.GameProviders.FirstOrDefault()?.Definition.TypeId
            ?? EditorNoteDefinitions.All.FirstOrDefault()?.TypeId
            ?? default;
    }

    public bool IsPreviewPlaying => _isPreviewPlaying;
    public bool IsPreviewAutoplayEnabled => _previewAutoplayEnabled;
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
            ApplyPreviewAutoplay();
            HandlePreviewCommands();
            return;
        }

        AdvanceEditorPlayback(gameTime);

        if (_isCreatingNewBeatmap)
        {
            HandleNewBeatmapModal();
            return;
        }

        if (_metadataWindow.IsEditingTextInput)
        {
            UpdateMetadataWindow();
            UpdateNoteOptionsWindow();
            return;
        }

        if (_noteOptionsWindow.IsEditingTextInput)
        {
            UpdateNoteOptionsWindow();
            return;
        }

        if (_openBeatmapExplorer.IsOpen)
        {
            if (Pressed(Keys.Escape))
                CloseOpenBeatmapWindow("Open beatmap cancelled");
            else
                UpdateOpenBeatmapWindow();

            UpdateNoteOptionsWindow();
            return;
        }

        if (HandleTopBarMouse())
        {
            UpdateNoteOptionsWindow();
            return;
        }

        if (HandleTimelineContextOptions())
        {
            UpdateNoteOptionsWindow();
            return;
        }

        if (_metadataWindow.IsOpen)
        {
            if (Pressed(Keys.Escape))
                CloseMetadataWindow();
            else
                UpdateMetadataWindow();

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

        if (Pressed(Keys.C) && IsControlDown())
        {
            CopySelectedClip();
            return;
        }

        if (Pressed(Keys.V) && IsControlDown())
        {
            PasteCopiedClip();
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

        if (Pressed(Keys.F5))
            RefreshSongs();

        if (Pressed(Keys.F6) && IsShiftDown())
            NormalizeBpmChangesToGlobalBeats();

        if (Pressed(Keys.OemPlus) || Pressed(Keys.Add))
            Zoom(-0.5);

        if (Pressed(Keys.OemMinus) || Pressed(Keys.Subtract))
            Zoom(0.5);

        if (Pressed(Keys.P))
            StartPreview();

    }

    private bool HandleTopBarMouse()
    {
        if (!LeftPressed())
            return false;

        if (TryGetTopBarMenuAt(_mouse.Position, out EditorTopBarMenu menu))
        {
            _openTopBarMenu = _openTopBarMenu == menu ? EditorTopBarMenu.None : menu;
            return true;
        }

        if (_openTopBarMenu == EditorTopBarMenu.None)
            return false;

        if (TryActivateTopBarMenuItem(_mouse.Position))
            _openTopBarMenu = EditorTopBarMenu.None;
        else
            _openTopBarMenu = EditorTopBarMenu.None;

        return true;
    }

    private bool TryActivateTopBarMenuItem(Point position)
    {
        IReadOnlyList<EditorTopBarMenuItem> items = GetTopBarMenuItems(_openTopBarMenu);
        for (int i = 0; i < items.Count; i++)
        {
            if (!GetTopBarMenuItemBounds(_openTopBarMenu, i, items.Count).Contains(position))
                continue;

            items[i].Action?.Invoke();
            return true;
        }

        return false;
    }

    private bool HandleTimelineDrag()
    {
        if (_document == null || _isCreatingNewBeatmap || _noteOptionsWindow.IsEditingTextInput)
            return false;

        Rectangle area = GetTimelineArea();
        Rectangle markerArea = GetTimelineMarkerLaneArea();
        GetTimelineWindow(out double windowStart, out double windowEnd);

        if (_timelineDragKind != EditorTimelineDragKind.None)
        {
            if (_mouse.LeftButton == ButtonState.Released)
            {
                FinishTimelineDrag();
                return true;
            }

            UpdateTimelineDrag(area, markerArea, windowStart, windowEnd);
            return true;
        }

        if (!LeftPressed() || _optionsIsCreation || MouseOverOpenWindow())
            return false;

        if (_toolMode == EditorToolMode.BpmModification)
            return TryHandleBpmToolTimelineClick(area, markerArea, windowStart, windowEnd);

        if (TryStartPaletteClipDrag())
            return true;

        if (TrySelectRhythmGameFromList())
            return true;

        return TryStartTimelineDrag(area, markerArea, windowStart, windowEnd);
    }

    private bool TryHandleBpmToolTimelineClick(Rectangle area, Rectangle markerArea, double windowStart, double windowEnd)
    {
        if (!markerArea.Contains(_mouse.Position))
            return false;

        if (TryHitTimelineClip(area, markerArea, windowStart, windowEnd, out ChartEditorClip clip, out _))
        {
            SelectTimelineClip(clip);
            return true;
        }

        if (TryStartTimelineEffectDrag(area, markerArea, windowStart, windowEnd))
            return true;

        return CreateBpmMarkerAtPointer(markerArea, windowStart, windowEnd);
    }

    private bool CreateBpmMarkerAtPointer(Rectangle markerArea, double windowStart, double windowEnd)
    {
        EditorEffectDefinition definition = EditorEffectDefinitions.Get(EditorEffectKind.BpmChange);
        double beat = SnapEffectPlacementBeat(definition, XToBeat(_mouse.X, windowStart, windowEnd, markerArea));
        double position = _document.GetSongPositionAtBeat(beat);
        ChartEffect effect = definition.CreateChartEffect(position, _document);
        ChartTiming.SetEffectBeat(effect, beat);
        effect.SongPosition = position;
        effect.SetSectionOffset(0);

        PlaceEffectCommand command = new(effect);
        if (ExecuteCommand(command))
        {
            SelectTimelineEffect(command.PlacedEffect);
            OpenEffectOptionsWindow(command.PlacedEffect);
            _status = $"Created BPM marker at {beat:0.###}b";
        }

        return true;
    }

    private bool HandleTimelineContextOptions()
    {
        if (_document == null || _isCreatingNewBeatmap || _noteOptionsWindow.IsEditingTextInput)
            return false;

        if (RightPressed())
            return TryOpenTimelineContextOptions();

        if (LeftPressed() && _optionsOpenedFromTimelineContext && !MouseOverOpenWindow())
        {
            CloseTimelineContextOptions();
            return false;
        }

        return false;
    }

    private bool TryOpenTimelineContextOptions()
    {
        Rectangle area = GetTimelineArea();
        Rectangle markerArea = GetTimelineMarkerLaneArea();
        GetTimelineWindow(out double windowStart, out double windowEnd);

        if (!area.Contains(_mouse.Position) && !markerArea.Contains(_mouse.Position))
            return false;

        if (TryHitTimelineClip(area, markerArea, windowStart, windowEnd, out ChartEditorClip clip, out _))
        {
            SelectTimelineClip(clip);
            bool opened = OpenClipOptionsWindow(clip);
            _optionsOpenedFromTimelineContext = opened;
            return opened;
        }

        if (TryHitTimelineNote(area, windowStart, windowEnd, out ChartNote note))
        {
            SelectTimelineNote(note);
            bool opened = OpenNoteOptionsWindow(note);
            _optionsOpenedFromTimelineContext = opened;
            return opened;
        }

        if (TryHitTimelineEffect(markerArea, windowStart, windowEnd, out ChartEffect effect))
        {
            SelectTimelineEffect(effect);
            bool opened = OpenEffectOptionsWindow(effect);
            _optionsOpenedFromTimelineContext = opened;
            return opened;
        }

        CloseTimelineContextOptions();
        return true;
    }

    private void CloseTimelineContextOptions()
    {
        _optionsOpenedFromTimelineContext = false;
        ClearPendingOptions();
        _noteOptionsWindow.Close();
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
        _status = IsInstantClip(clipDefinition)
            ? $"Drag {clipDefinition.DisplayName} to the marker lane"
            : $"Drag {clipDefinition.DisplayName} to a track";
        return true;
    }

    private bool TrySelectRhythmGameFromList()
    {
        IReadOnlyList<IEditorNoteProvider> providers = EditorNoteDefinitions.GameProviders;
        for (int i = 0; i < providers.Count; i++)
        {
            if (!GetRhythmGameRowBounds(i).Contains(_mouse.Position))
                continue;

            Select(providers[i].Definition.TypeId);
            _status = $"Rhythm game {providers[i].RhythmGameDisplayName ?? providers[i].Definition.DisplayName}";
            return true;
        }

        return false;
    }

    private bool TryStartTimelineDrag(Rectangle area, Rectangle markerArea, double windowStart, double windowEnd)
    {
        if (!area.Contains(_mouse.Position) && !markerArea.Contains(_mouse.Position))
            return false;

        double mouseBeat = XToBeat(_mouse.X, windowStart, windowEnd, area);

        if (TryHitTimelineClip(area, markerArea, windowStart, windowEnd, out ChartEditorClip clip, out EditorTimelineDragKind clipDragKind))
        {
            SelectTimelineClip(clip);
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
            double clipEndBeat = clip.StartBeat + Math.Max(0.0, clip.LengthBeats);
            _dragPointerOffsetBeats = clipDragKind == EditorTimelineDragKind.ClipResizeEnd
                ? clipEndBeat - mouseBeat
                : clip.StartBeat - mouseBeat;
            _dragMoved = false;
            _status = $"Dragging {_draggedClipDefinition?.DisplayName ?? "clip"}";
            return true;
        }

        if (TryStartTimelineEffectDrag(area, markerArea, windowStart, windowEnd))
            return true;

        if (TryHitTimelineNote(area, windowStart, windowEnd, out ChartNote note))
        {
            SelectTimelineNote(note);
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

        ClearTimelineSelection();
        return false;
    }

    private bool TryStartTimelineEffectDrag(Rectangle area, Rectangle markerArea, double windowStart, double windowEnd)
    {
        if (!TryHitTimelineEffect(markerArea, windowStart, windowEnd, out ChartEffect effect))
            return false;

        double mouseBeat = XToBeat(_mouse.X, windowStart, windowEnd, area);
        SelectTimelineEffect(effect);
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

    private void UpdateTimelineDrag(Rectangle area, Rectangle markerArea, double windowStart, double windowEnd)
    {
        double pointerBeat = XToBeat(_mouse.X, windowStart, windowEnd, area) + _dragPointerOffsetBeats;

        if (_timelineDragKind == EditorTimelineDragKind.ClipCreate && _draggedClipDefinition != null)
        {
            UpdateClipCreatePreview(area, markerArea, windowStart, windowEnd);
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
                InvalidateClipActivationCache();
                _dragMoved = true;
                _dragEndBeat = _document.GetNoteBeat(_draggedNote);
                EditorNoteDefinition definition = EditorNoteDefinitions.FromChartNote(_draggedNote);
                _status = $"Dragging {definition?.DisplayName ?? "note"}: {_document.GetNoteBeat(_draggedNote):0.###}b";
            }

            return;
        }

        if (_timelineDragKind == EditorTimelineDragKind.Effect && _draggedEffect != null)
        {
            EditorEffectDefinition definition = EditorEffectDefinitions.FromChartEffect(_draggedEffect);
            double beat = SnapEffectPlacementBeat(definition, pointerBeat);
            if (_document.MoveEffectToBeat(_draggedEffect, beat, _draggedEffectOffsetFollowedPosition))
            {
                InvalidateClipActivationCache();
                _dragMoved = true;
                _dragEndBeat = _document.GetEffectBeat(_draggedEffect);
                _status = $"Dragging {definition?.DisplayName ?? "effect"}: {_document.GetEffectBeat(_draggedEffect):0.###}b";
            }
        }
    }

    private void UpdateClipCreatePreview(Rectangle area, Rectangle markerArea, double windowStart, double windowEnd)
    {
        bool isInstant = IsInstantClip(_draggedClipDefinition);
        Rectangle dropArea = isInstant ? markerArea : area;
        if (!dropArea.Contains(_mouse.Position))
        {
            _dragMoved = false;
            _status = isInstant
                ? $"Drag {_draggedClipDefinition.DisplayName} to the marker lane"
                : $"Drag {_draggedClipDefinition.DisplayName} to a track";
            return;
        }

        _dragPreviewStartBeat = SnapPlacementBeat(XToBeat(_mouse.X, windowStart, windowEnd, area));
        _dragPreviewTrackIndex = isInstant ? 0 : GetTrackIndexAtY(_mouse.Y, area);
        _dragPreviewLengthBeats = isInstant ? 0.0 : _dragPreviewLengthBeats;
        _dragMoved = true;
        _status = isInstant
            ? $"Create {_draggedClipDefinition.DisplayName}: {_dragPreviewStartBeat:0.###}b on marker lane"
            : $"Create {_draggedClipDefinition.DisplayName}: {_dragPreviewStartBeat:0.###}b on Track {_dragPreviewTrackIndex + 1}";
    }

    private void UpdateClipDragPreview(Rectangle area, double pointerBeat)
    {
        double minLength = 0.0;
        double originalEnd = _dragStartBeat + Math.Max(0.0, _dragStartLengthBeats);

        if (_timelineDragKind == EditorTimelineDragKind.ClipMove)
        {
            _dragPreviewStartBeat = SnapPlacementBeat(pointerBeat);
            _dragPreviewLengthBeats = IsInstantClip(_draggedClipDefinition) ? 0.0 : _dragStartLengthBeats;
            _dragPreviewTrackIndex = IsInstantClip(_draggedClipDefinition) ? 0 : GetTrackIndexAtY(_mouse.Y, area);
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
        _status = IsInstantClip(_draggedClipDefinition)
            ? $"{_draggedClipDefinition?.DisplayName ?? "Clip"}: {_dragPreviewStartBeat:0.###}b on marker lane"
            : $"{_draggedClipDefinition?.DisplayName ?? "Clip"}: {_dragPreviewStartBeat:0.###}b len {_dragPreviewLengthBeats:0.###} on Track {_dragPreviewTrackIndex + 1}";
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
                _status = IsInstantClip(draggedClipDefinition)
                    ? $"Created {draggedClipDefinition.DisplayName} at {previewStartBeat:0.###}b on marker lane"
                    : $"Created {draggedClipDefinition.DisplayName} at {previewStartBeat:0.###}b on Track {previewTrackIndex + 1}";
            else if (draggedClipDefinition != null)
                _status = $"Cancelled {draggedClipDefinition.DisplayName}";

            return;
        }

        if (!dragged)
        {
            if (kind == EditorTimelineDragKind.Effect && draggedEffect != null && _toolMode == EditorToolMode.BpmModification)
                OpenEffectOptionsWindow(draggedEffect);

            return;
        }

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
            _status = IsInstantClip(draggedClipDefinition)
                ? $"Updated {draggedClipDefinition?.DisplayName ?? "clip"} at {previewStartBeat:0.###}b on marker lane"
                : $"Updated {draggedClipDefinition?.DisplayName ?? "clip"} at {previewStartBeat:0.###}b len {previewLengthBeats:0.###}";
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

    private bool TryHitTimelineEffect(Rectangle markerArea, double windowStart, double windowEnd, out ChartEffect effect)
    {
        effect = null;
        GetSongWindowForBeatWindow(windowStart, windowEnd, out double songStart, out double songEnd);
        foreach (ChartEffect candidate in _document.GetEffectsInWindow(songStart, songEnd).Reverse())
        {
            if (EditorEffectDefinitions.FromChartEffect(candidate) == null)
                continue;

            Rectangle bounds = GetEffectMarkerBounds(candidate, windowStart, windowEnd, markerArea);
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

    private void SelectTimelineClip(ChartEditorClip clip)
    {
        _selectedTimelineClipId = clip?.Id;
        _selectedTimelineNote = null;
        _selectedTimelineEffect = null;
    }

    private void SelectTimelineNote(ChartNote note)
    {
        _selectedTimelineNote = note;
        _selectedTimelineEffect = null;
        _selectedTimelineClipId = null;
    }

    private void SelectTimelineEffect(ChartEffect effect)
    {
        _selectedTimelineEffect = effect;
        _selectedTimelineNote = null;
        _selectedTimelineClipId = null;
    }

    private void ClearTimelineSelection()
    {
        _selectedTimelineNote = null;
        _selectedTimelineEffect = null;
        _selectedTimelineClipId = null;
    }

    private bool IsTimelineClipSelected(ChartEditorClip clip)
    {
        return clip != null
            && !string.IsNullOrWhiteSpace(_selectedTimelineClipId)
            && string.Equals(clip.Id, _selectedTimelineClipId, StringComparison.Ordinal);
    }

    private bool IsTimelineNoteSelected(ChartNote note)
    {
        return ReferenceEquals(note, _selectedTimelineNote);
    }

    private bool IsTimelineEffectSelected(ChartEffect effect)
    {
        return ReferenceEquals(effect, _selectedTimelineEffect);
    }

    private bool MouseOverOpenWindow()
    {
        return (_noteOptionsWindow.IsOpen && GetNoteOptionsWindowBounds().Contains(_mouse.Position))
            || _newBeatmapExplorer.IsOpen
            || _openBeatmapExplorer.IsOpen
            || (_openBeatmapWindow.IsOpen && GetOpenBeatmapWindowBounds().Contains(_mouse.Position))
            || (_metadataWindow.IsOpen && GetMetadataWindowBounds().Contains(_mouse.Position));
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
        else if (!TryOpenSelectedClipOptionsWindow())
            OpenNoteOptionsWindow(_document.FindNearestAtBeat(CurrentBeatPosition(), GetSelectionDistance()));
    }

    private bool TryOpenSelectedClipOptionsWindow()
    {
        ChartEditorClip selectedClip = !string.IsNullOrWhiteSpace(_selectedTimelineClipId)
            ? _document.FindEditorClip(_selectedTimelineClipId)
            : null;

        return OpenClipOptionsWindow(selectedClip ?? FindNearestEditorClipAtBeat(CurrentBeatPosition(), GetSelectionDistance()), showStatusWhenMissing: false);
    }

    private void CopySelectedClip()
    {
        ChartEditorClip clip = ResolveSelectedOrNearbyClip();
        if (clip == null)
        {
            _status = "No clip selected to copy";
            return;
        }

        _clipboardClip = EditorCommandCloning.CloneClip(clip);
        EditorClipDefinition definition = EditorClipDefinitions.Find(clip.RhythmGameId, clip.ClipTypeId);
        _status = $"Copied {definition?.DisplayName ?? clip.ClipTypeId ?? "clip"}";
    }

    private void PasteCopiedClip()
    {
        if (_clipboardClip == null)
        {
            _status = "No copied clip to paste";
            return;
        }

        EditorClipDefinition definition = EditorClipDefinitions.Find(_clipboardClip.RhythmGameId, _clipboardClip.ClipTypeId);
        bool isInstant = IsInstantClip(definition, _clipboardClip);
        ChartEditorClip pastedClip = EditorCommandCloning.CloneClip(_clipboardClip);
        pastedClip.Id = Guid.NewGuid().ToString("N");
        pastedClip.StartBeat = SnapPlacementBeat(CurrentBeatPosition());
        pastedClip.TrackIndex = isInstant ? 0 : Math.Clamp(pastedClip.TrackIndex, 0, TimelineTrackCount - 1);
        pastedClip.LengthBeats = isInstant ? 0.0 : Math.Max(0.0, pastedClip.LengthBeats);
        pastedClip.ClipCategory = definition?.Category.ToString() ?? pastedClip.ClipCategory;
        pastedClip.InputAction = definition?.InputAction ?? pastedClip.InputAction;

        if (!ExecuteCommand(new CreateClipCommand(pastedClip)))
            return;

        _selectedTimelineClipId = pastedClip.Id;
        _selectedTimelineNote = null;
        _selectedTimelineEffect = null;
        _status = $"Pasted {definition?.DisplayName ?? pastedClip.ClipTypeId ?? "clip"} at {pastedClip.StartBeat:0.###}b";
    }

    private ChartEditorClip ResolveSelectedOrNearbyClip()
    {
        if (!string.IsNullOrWhiteSpace(_selectedTimelineClipId))
        {
            ChartEditorClip selected = _document.FindEditorClip(_selectedTimelineClipId);
            if (selected != null)
                return selected;

            _selectedTimelineClipId = null;
        }

        if (_optionsIsClip && _optionsClip != null)
        {
            ChartEditorClip optionsClip = _document.FindEditorClip(_optionsClip.Id);
            if (optionsClip != null)
                return optionsClip;
        }

        return FindNearestEditorClipAtBeat(CurrentBeatPosition(), GetSelectionDistance());
    }

    private bool OpenClipOptionsWindow(ChartEditorClip clip, bool showStatusWhenMissing = true)
    {
        if (clip == null)
        {
            if (showStatusWhenMissing)
                _status = "No configurable clip close enough";
            return false;
        }

        EditorClipDefinition definition = EditorClipDefinitions.Find(clip.RhythmGameId, clip.ClipTypeId);
        if (definition == null)
        {
            if (showStatusWhenMissing)
                _status = "No configurable clip close enough";
            return false;
        }

        _optionsClip = clip;
        _optionsClipDefinition = definition;
        _optionsNote = null;
        _optionsDefinition = null;
        _optionsPanel = null;
        _optionsEffect = null;
        _optionsEffectDefinition = null;
        _effectOptionsPanel = null;
        _optionsIsClip = true;
        _optionsIsEffect = false;
        _optionsIsCreation = false;
        _optionsIsIntervalCreation = false;
        _noteOptionsWindow.Open();
        _status = $"Options for {definition.DisplayName} at {clip.StartBeat:0.###}b";
        return true;
    }

    private ChartEditorClip FindNearestEditorClipAtBeat(double beat, double maxDistanceBeats)
    {
        return _document.EditorClips
            .Where(clip => clip != null)
            .Select(clip => new { Clip = clip, Distance = GetClipDistanceFromBeat(clip, beat) })
            .Where(item => item.Distance <= maxDistanceBeats)
            .OrderBy(item => item.Distance)
            .Select(item => item.Clip)
            .FirstOrDefault();
    }

    private static double GetClipDistanceFromBeat(ChartEditorClip clip, double beat)
    {
        double start = clip.StartBeat;
        double end = start + Math.Max(0.0, clip.LengthBeats);
        if (beat >= start && beat <= end)
            return 0.0;

        return Math.Min(Math.Abs(beat - start), Math.Abs(beat - end));
    }

    private void TogglePlacementMode()
    {
        if (_toolMode == EditorToolMode.BpmModification)
            EnterSelectionToolMode();
        else
            EnterBpmModificationToolMode();
    }

    private void EnterSelectionToolMode()
    {
        SetToolMode(EditorToolMode.Selection);
    }

    private void EnterBpmModificationToolMode()
    {
        SetToolMode(EditorToolMode.BpmModification);
    }

    private void SetToolMode(EditorToolMode toolMode)
    {
        if (_isSelectingIntervalRange)
        {
            _isSelectingIntervalRange = false;
            _intervalRangeStart = null;
        }

        if (_optionsIsCreation)
        {
            _noteOptionsWindow.Close();
            ClearPendingOptions();
        }

        _toolMode = toolMode;
        if (_toolMode == EditorToolMode.BpmModification)
        {
            _placementMode = EditorPlacementMode.Effect;
            _selectedEffectKind = EditorEffectKind.BpmChange;
            _status = "BPM edit mode: click the MARKERS ruler to place BPM markers";
            return;
        }

        _placementMode = EditorPlacementMode.Note;
        _status = "Selection mode: normal timeline actions enabled";
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
        if (definition == null || !EditorNoteDefinitions.TryGetOptionsPanel(definition.TypeId, out _optionsPanel))
        {
            _status = "No configurable note close enough";
            return false;
        }

        _optionsDefinition = definition;
        _optionsClip = null;
        _optionsClipDefinition = null;
        _optionsEffect = null;
        _optionsEffectDefinition = null;
        _effectOptionsPanel = null;
        _optionsIsClip = false;
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
        if (_lastCreatedNoteData.TryGetValue(definition.TypeId, out Dictionary<string, string> lastData))
            _optionsNote.AdditionnalData = new Dictionary<string, string>(lastData);

        _optionsDefinition = definition;
        _optionsClip = null;
        _optionsClipDefinition = null;
        _optionsEffect = null;
        _optionsEffectDefinition = null;
        _effectOptionsPanel = null;
        _optionsIsClip = false;
        _optionsIsEffect = false;
        _optionsIsCreation = true;
        _optionsIsIntervalCreation = false;
        if (!EditorNoteDefinitions.TryGetOptionsPanel(definition.TypeId, out _optionsPanel))
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
        if (_lastCreatedNoteData.TryGetValue(definition.TypeId, out Dictionary<string, string> lastData))
            _optionsNote.AdditionnalData = new Dictionary<string, string>(lastData);

        _pendingPlacementOptions = new PlacementOptions(
            RepeatDurationBeats: GetBeatsBetween(start, end),
            RepeatStepBeats: _lastIntervalPlacementOptions.RepeatStepBeats ?? IntervalEditorNoteProvider.DefaultStepBeats);

        _optionsDefinition = definition;
        _optionsClip = null;
        _optionsClipDefinition = null;
        _optionsEffect = null;
        _optionsEffectDefinition = null;
        _effectOptionsPanel = null;
        _optionsIsClip = false;
        _optionsIsEffect = false;
        _optionsIsCreation = true;
        _optionsIsIntervalCreation = true;
        if (!EditorNoteDefinitions.TryGetOptionsPanel(definition.TypeId, out _optionsPanel))
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
        _optionsClip = null;
        _optionsClipDefinition = null;
        _optionsEffectDefinition = definition;
        _optionsIsClip = false;
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
        _optionsClip = null;
        _optionsClipDefinition = null;
        _optionsEffect = definition.CreateChartEffect(songPosition, _document);
        _optionsEffectDefinition = definition;
        _optionsIsClip = false;
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

    private void ApplyNotePatch(NotePatch patch)
    {
        if (patch == null)
            return;

        ChartNote note = ResolveOptionsNote();
        if (note == null)
            return;

        if (_optionsIsCreation)
        {
            patch.ApplyTo(note);
            return;
        }

        _document.ApplyNotePatch(note, patch);
    }

    private void ApplyPlacementOptions(PlacementOptions placementOptions)
    {
        _pendingPlacementOptions = placementOptions ?? PlacementOptions.None;
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

        if (_optionsIsClip)
        {
            UpdateClipOptionsWindow();
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

    private void UpdateClipOptionsWindow()
    {
        if (_optionsClip == null || _optionsClipDefinition == null || _document.FindEditorClip(_optionsClip.Id) == null)
        {
            _noteOptionsWindow.Close();
            return;
        }

        if (_noteOptionsWindow.Update(GetNoteOptionsWindowBounds(), GetNoteOptionRows()))
            _status = $"Updated clip {_optionsClipDefinition.DisplayName} at {_optionsClip.StartBeat:0.###}b";
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
        ResetPreviewAutoplayCursor(position);
        _beatmapPlayer.Conductor.Play();
        _isPreviewPlaying = true;
        _status = _previewAutoplayEnabled ? "Preview playing with autoplay — ESC to stop" : "Preview playing — ESC to stop";
    }

    private void TogglePreviewAutoplay()
    {
        _previewAutoplayEnabled = !_previewAutoplayEnabled;
        ResetPreviewAutoplayCursor(CurrentSongPosition());
        _status = _previewAutoplayEnabled ? "Preview autoplay enabled" : "Preview autoplay disabled";
    }

    private void ResetPreviewAutoplayCursor(double songPosition)
    {
        _previewAutoplayNextNoteIndex = 0;
        _previewAutoplayLastSongPosition = songPosition;

        IReadOnlyList<Note> notes = _beatmapPlayer.ChartPlayer?.Notes;
        if (notes == null)
            return;

        while (_previewAutoplayNextNoteIndex < notes.Count
            && notes[_previewAutoplayNextNoteIndex].SongPosition < songPosition - PreviewAutoplayEpsilonSeconds)
        {
            _previewAutoplayNextNoteIndex++;
        }
    }

    private void ApplyPreviewAutoplay()
    {
        if (!_previewAutoplayEnabled || _beatmapPlayer.Conductor == null || _beatmapPlayer.ChartPlayer == null)
            return;

        double songPosition = _beatmapPlayer.Conductor.SongPosition;
        if (double.IsNaN(_previewAutoplayLastSongPosition) || songPosition < _previewAutoplayLastSongPosition - PreviewAutoplayEpsilonSeconds)
            ResetPreviewAutoplayCursor(songPosition);

        IReadOnlyList<Note> notes = _beatmapPlayer.ChartPlayer.Notes;
        while (_previewAutoplayNextNoteIndex < notes.Count)
        {
            Note note = notes[_previewAutoplayNextNoteIndex];
            if (note.SongPosition > songPosition + PreviewAutoplayEpsilonSeconds)
                break;

            _previewAutoplayNextNoteIndex++;
            if (note.HasReacted || string.IsNullOrWhiteSpace(note.InputActionToPress))
                continue;

            _beatmapPlayer.ChartPlayer.React(note.InputActionToPress, note.SongPosition);
            if (note.InputActionToPress == "ReactMain")
                GLOBALS.ReactMainInputSerial++;
        }

        _previewAutoplayLastSongPosition = songPosition;
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
        string chartPath = GetInitialChartPath();

        LoadDocument(songPath, chartPath);
    }

    private string GetInitialChartPath()
    {
        string lastChartPath = FindAvailableChartPath(_settings.LastChartPath);
        if (!string.IsNullOrWhiteSpace(lastChartPath))
            return lastChartPath;

        string defaultChartPath = FindAvailableChartPath(_defaultChartPath);
        if (!string.IsNullOrWhiteSpace(defaultChartPath))
            return defaultChartPath;

        return _availableCharts.FirstOrDefault() ?? _defaultChartPath;
    }

    private string FindAvailableChartPath(string chartPath)
    {
        if (string.IsNullOrWhiteSpace(chartPath))
            return null;

        string resolvedChartPath = BeatmapPackagePaths.ResolveChartPath(chartPath);
        return _availableCharts.FirstOrDefault(path => PathEquals(path, resolvedChartPath));
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
        ClearTimelineSelection();
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
        _isCreatingNewBeatmap = true;
        _newBeatmapExplorer.OpenCreateBeatmap(
            "NEW BEATMAP",
            CreateNewBeatmapAtPackagePath,
            () => CloseNewBeatmapModal("New beatmap cancelled"),
            status => _status = status);
    }

    private void HandleNewBeatmapModal()
    {
        if (!_newBeatmapExplorer.IsOpen)
        {
            CloseNewBeatmapModal("New beatmap cancelled");
            return;
        }

        _newBeatmapExplorer.Update(_mouse, _previousMouse, _keyboard, _previousKeyboard, GLOBALS.graphicsDevice.Viewport);
    }

    private void HandleNewBeatmapExplorerMouse()
    {
        Rectangle modal = GetNewBeatmapExplorerBounds();
        Rectangle sidebar = GetNewBeatmapSidebarBounds(modal);
        Rectangle list = GetNewBeatmapFolderListBounds(modal);
        Rectangle actions = GetNewBeatmapActionsBounds(modal);
        IReadOnlyList<string> folders = GetNewBeatmapChildFolders();

        ApplyNewBeatmapFolderListScroll(list, folders.Count);

        if (!LeftPressed())
            return;

        if (GetNewBeatmapHeaderCloseButtonBounds(modal).Contains(_mouse.Position))
        {
            CloseNewBeatmapModal("New beatmap cancelled");
            return;
        }

        Rectangle input = GetNewBeatmapFolderNameInputBounds(actions);
        SetNewBeatmapFolderNameFocused(input.Contains(_mouse.Position));

        if (GetNewBeatmapSidebarRootButtonBounds(sidebar).Contains(_mouse.Position))
        {
            NavigateToNewBeatmapFolder(BeatmapPackagePaths.BeatmapsRoot);
            return;
        }

        Rectangle parentButton = GetNewBeatmapSidebarParentButtonBounds(sidebar);
        if (!IsBeatmapsRoot(_newBeatmapFolderPath) && parentButton.Contains(_mouse.Position))
        {
            NavigateToParentNewBeatmapFolder();
            return;
        }

        Rectangle listContent = GetNewBeatmapFolderListContentBounds(list);
        if (listContent.Contains(_mouse.Position))
        {
            for (int i = 0; i < folders.Count; i++)
            {
                Rectangle row = GetNewBeatmapFolderRowBounds(list, i);
                if (!listContent.Intersects(row) || !row.Contains(_mouse.Position))
                    continue;

                NavigateToNewBeatmapFolder(folders[i]);
                return;
            }
        }

        if (GetNewBeatmapCreateFolderButtonBounds(actions).Contains(_mouse.Position))
        {
            CreateNewBeatmapSubfolder();
            return;
        }

        if (GetNewBeatmapCreateBeatmapButtonBounds(actions).Contains(_mouse.Position))
        {
            CreateNewBeatmap();
            return;
        }

        if (GetNewBeatmapActionCancelButtonBounds(actions).Contains(_mouse.Position))
            CloseNewBeatmapModal("New beatmap cancelled");
    }

    private void ApplyNewBeatmapFolderListScroll(Rectangle list, int folderCount)
    {
        int maxScroll = GetNewBeatmapFolderListMaxScroll(list, folderCount);
        _newBeatmapFolderListScroll = Math.Clamp(_newBeatmapFolderListScroll, 0, maxScroll);

        if (!GetNewBeatmapFolderListContentBounds(list).Contains(_mouse.Position))
            return;

        int wheelDelta = _mouse.ScrollWheelValue - _previousMouse.ScrollWheelValue;
        if (wheelDelta == 0)
            return;

        _newBeatmapFolderListScroll -= Math.Sign(wheelDelta) * 34;
        _newBeatmapFolderListScroll = Math.Clamp(_newBeatmapFolderListScroll, 0, maxScroll);
    }

    private void UpdateNewBeatmapFolderNameInput()
    {
        if (DevUiTextInput.ShouldBackspace(_keyboard, _previousKeyboard, ref _customTextBackspaceHoldStartMs, ref _customTextBackspaceLastRepeatMs) && _newBeatmapNameBuffer.Length > 0)
            _newBeatmapNameBuffer = _newBeatmapNameBuffer[..^1];

        foreach (Keys key in _keyboard.GetPressedKeys())
        {
            if (_previousKeyboard.IsKeyDown(key) || key == Keys.Back)
                continue;

            if (DevUiTextInput.TryGetTypedChar(key, _keyboard, out char c) && _newBeatmapNameBuffer.Length < 80)
                _newBeatmapNameBuffer += c;
        }
    }

    private void SetNewBeatmapFolderNameFocused(bool focused)
    {
        if (_newBeatmapFolderNameFocused == focused)
            return;

        _newBeatmapFolderNameFocused = focused;
        ResetCustomTextBackspaceRepeat();
    }

    private void CreateNewBeatmap()
    {
        CreateNewBeatmapAtPackagePath(_newBeatmapFolderPath);
    }

    private void CreateNewBeatmapAtPackagePath(string packagePath)
    {
        string chartPath = GetAvailableNewBeatmapPath(packagePath);
        string beatmapName = GetChartLeafDisplayName(chartPath);
        string songPath = _document?.SongPath ?? _defaultSongPath;
        double bpm = _document?.Chart?.BPM > 0 ? _document.Chart.BPM : 100;

        _document = BeatmapEditorDocument.CreateNew(songPath, chartPath, bpm);
        _commandStack.Clear();
        ClearTimelineSelection();
        _document.SetMetadata(beatmapName: beatmapName);
        _manualBeatPosition = 0;
        RefreshSongs();
        RefreshCharts();
        SyncSelectedSongIndex();
        SyncSelectedChartIndex();
        RebuildPlayback(false);
        RememberCurrentBeatmap();
        CloseNewBeatmapModal($"New beatmap {GetChartDisplayName(chartPath)} ready");
    }

    private void RememberCurrentBeatmap()
    {
        if (_document == null || string.IsNullOrWhiteSpace(_document.ChartPath))
            return;

        _settings.LastChartPath = _document.ChartPath;
        _settings.Save();
    }

    private void CloseNewBeatmapModal(string status)
    {
        _isCreatingNewBeatmap = false;
        _newBeatmapExplorer.Close();
        _newBeatmapFolderNameFocused = false;
        ResetCustomTextBackspaceRepeat();
        _status = status;
    }

    private string GetAvailableNewBeatmapPath(string selectedFolderPath)
    {
        string folderPath = string.IsNullOrWhiteSpace(selectedFolderPath)
            ? BeatmapPackagePaths.GetPackagePathFromUserInput("New Beatmap")
            : selectedFolderPath;

        if (IsBeatmapsRoot(folderPath))
            return BeatmapPackagePaths.GetAvailablePackageChartPath("New Beatmap");

        return BeatmapPackagePaths.GetAvailablePackageChartPathForPackagePath(folderPath);
    }

    private void NavigateToNewBeatmapFolder(string folderPath)
    {
        if (!Directory.Exists(folderPath) || !IsInsideBeatmapsRoot(folderPath))
        {
            _status = "Folder is not inside Beatmaps.";
            return;
        }

        if (!IsBeatmapsRoot(folderPath) && File.Exists(BeatmapPackagePaths.GetChartPathForPackage(folderPath)))
        {
            _status = "Beatmap packages are hidden in New Beatmap.";
            return;
        }

        string storedFolderPath = GetBeatmapsFolderStoragePath(folderPath);
        _newBeatmapFolderPath = storedFolderPath;
        _newBeatmapFolderListScroll = 0;
        SetNewBeatmapFolderNameFocused(false);
        _status = "Folder: " + GetBeatmapsFolderDisplayPath(storedFolderPath);
    }

    private void NavigateToParentNewBeatmapFolder()
    {
        string current = Path.GetFullPath(_newBeatmapFolderPath);
        string root = Path.GetFullPath(BeatmapPackagePaths.BeatmapsRoot);
        if (string.Equals(current.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
            return;

        string parent = Path.GetDirectoryName(current);
        if (!string.IsNullOrWhiteSpace(parent))
            NavigateToNewBeatmapFolder(parent);
    }

    private void CreateNewBeatmapSubfolder()
    {
        string folderName = BeatmapPackagePaths.SanitizeFileName(_newBeatmapNameBuffer);
        if (string.IsNullOrWhiteSpace(folderName))
            folderName = "New Folder";

        string folderPath = GetAvailableSubfolderPath(_newBeatmapFolderPath, folderName);
        Directory.CreateDirectory(folderPath);
        _newBeatmapNameBuffer = string.Empty;
        NavigateToNewBeatmapFolder(folderPath);
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

    private static bool IsInsideBeatmapsRoot(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
            return false;

        string root = Path.GetFullPath(BeatmapPackagePaths.BeatmapsRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string folder = Path.GetFullPath(folderPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.Equals(folder, root, StringComparison.OrdinalIgnoreCase)
            || folder.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || folder.StartsWith(root + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBeatmapsRoot(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
            return false;

        string root = Path.GetFullPath(BeatmapPackagePaths.BeatmapsRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string folder = Path.GetFullPath(folderPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.Equals(folder, root, StringComparison.OrdinalIgnoreCase);
    }

    private IReadOnlyList<string> GetNewBeatmapChildFolders()
    {
        try
        {
            if (!Directory.Exists(_newBeatmapFolderPath) || !IsInsideBeatmapsRoot(_newBeatmapFolderPath))
                return Array.Empty<string>();

            return Directory.GetDirectories(_newBeatmapFolderPath)
                .Where(IsInsideBeatmapsRoot)
                .Where(path => !File.Exists(BeatmapPackagePaths.GetChartPathForPackage(path)))
                .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException)
        {
            return Array.Empty<string>();
        }
    }

    private static string GetChartDisplayName(string chartPath)
    {
        if (BeatmapPackagePaths.IsPackageChartPath(chartPath))
        {
            string packagePath = Path.GetDirectoryName(chartPath);
            string relativePackagePath = GetBeatmapsRelativePath(packagePath);
            return string.IsNullOrWhiteSpace(relativePackagePath)
                ? Path.GetFileName(packagePath)
                : relativePackagePath;
        }

        return Path.GetFileName(chartPath);
    }

    private static string GetChartLeafDisplayName(string chartPath)
    {
        if (BeatmapPackagePaths.IsPackageChartPath(chartPath))
            return Path.GetFileName(Path.GetDirectoryName(chartPath));

        return Path.GetFileNameWithoutExtension(chartPath);
    }

    private static string GetBeatmapsFolderDisplayPath(string path)
    {
        string relativePath = GetBeatmapsRelativePath(path);
        return string.IsNullOrWhiteSpace(relativePath) ? BeatmapPackagePaths.BeatmapsRoot : relativePath;
    }

    private static string GetBeatmapsFolderStoragePath(string path)
    {
        string relativePath = GetBeatmapsRelativePath(path);
        return string.IsNullOrWhiteSpace(relativePath)
            ? BeatmapPackagePaths.BeatmapsRoot
            : Path.Combine(BeatmapPackagePaths.BeatmapsRoot, relativePath);
    }

    private static string GetNewBeatmapExplorerDisplayPath(string path)
    {
        string relativePath = GetBeatmapsRelativePath(path).Replace('\\', '/');
        return string.IsNullOrWhiteSpace(relativePath)
            ? BeatmapPackagePaths.BeatmapsRoot
            : BeatmapPackagePaths.BeatmapsRoot + " / " + relativePath.Replace("/", " / ");
    }

    private static string GetBeatmapsRelativePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        string normalizedPath = NormalizePath(GetFullPathOrOriginal(path)).TrimEnd('/');
        string root = NormalizePath(GetFullPathOrOriginal(BeatmapPackagePaths.BeatmapsRoot)).TrimEnd('/');
        if (string.Equals(normalizedPath, root, StringComparison.OrdinalIgnoreCase))
            return string.Empty;

        string prefix = root + "/";
        if (normalizedPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return normalizedPath[prefix.Length..];

        normalizedPath = NormalizePath(path);
        root = NormalizePath(BeatmapPackagePaths.BeatmapsRoot);
        if (string.Equals(normalizedPath, root, StringComparison.OrdinalIgnoreCase))
            return string.Empty;

        prefix = root + "/";
        return normalizedPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? normalizedPath[prefix.Length..]
            : normalizedPath;
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

    private void RebuildPlayback(bool keepPlaying)
    {
        RebuildPlaybackAtPosition(CurrentSongPosition(), keepPlaying);
    }

    private void RebuildPlaybackAtPosition(double position, bool keepPlaying)
    {
        InvalidateClipActivationCache();
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

    private void InvalidateClipActivationCache()
    {
        _clipActivationRangeCache.Clear();
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
        EditorNoteDefinition definition = EditorNoteDefinitions.Get(_selectedNoteTypeId);
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
        if (_toolMode == EditorToolMode.BpmModification)
        {
            _status = "Switch to Selection mode for interval range";
            return;
        }

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
        _status = $"Interval {EditorNoteDefinitions.Get(_selectedNoteTypeId).DisplayName}: ENTER start, ENTER end";
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

        EditorNoteDefinition definition = EditorNoteDefinitions.Get(_selectedNoteTypeId);
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
            StoreLastIntervalData();

        PlacementOptions placementOptions = _optionsIsIntervalCreation
            ? _pendingPlacementOptions
            : PlacementOptions.None;
        IReadOnlyList<EditorNotePlacement> placements = _optionsDefinition.CreatePlacements(_optionsNote, CreatePlacementContext(), placementOptions);
        PlaceNotesCommand command = new(placements);
        if (ExecuteCommand(command))
        {
            IReadOnlyList<ChartNote> placedNotes = command.PlacedNotes;
            _lastCreatedNoteData[_optionsDefinition.TypeId] = creationData;
            _optionsIsCreation = false;
            _optionsIsIntervalCreation = false;

            if (placedNotes.Count == 1 && EditorNoteDefinitions.FromChartNote(placedNotes[0]) is EditorNoteDefinition placedDefinition && EditorNoteDefinitions.TryGetOptionsPanel(placedDefinition.TypeId, out _))
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
        _optionsClip = null;
        _optionsClipDefinition = null;
        _optionsEffect = null;
        _optionsEffectDefinition = null;
        _effectOptionsPanel = null;
        _optionsIsClip = false;
        _optionsIsEffect = false;
        _optionsIsCreation = false;
        _optionsIsIntervalCreation = false;
        _pendingPlacementOptions = PlacementOptions.None;
        _optionsOpenedFromTimelineContext = false;
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

    private void StoreLastIntervalData()
    {
        _lastIntervalPlacementOptions = _pendingPlacementOptions ?? PlacementOptions.None;
    }

    private double GetBeatsBetween(double startSongPosition, double endSongPosition)
    {
        return Math.Abs(_document.GetBeatAt(endSongPosition) - _document.GetBeatAt(startSongPosition));
    }

    private void DeleteNearestSelected()
    {
        if (DeleteSelectedTimelineItem())
            return;

        if (_placementMode == EditorPlacementMode.Effect)
            DeleteNearestEffect();
        else
            DeleteNearestNote();
    }

    private bool DeleteSelectedTimelineItem()
    {
        if (!string.IsNullOrWhiteSpace(_selectedTimelineClipId))
        {
            ChartEditorClip clip = _document.FindEditorClip(_selectedTimelineClipId);
            if (clip == null)
            {
                _selectedTimelineClipId = null;
                return false;
            }

            EditorClipDefinition definition = EditorClipDefinitions.Find(clip.RhythmGameId, clip.ClipTypeId);
            string clipId = clip.Id;
            string clipName = definition?.DisplayName ?? clip.ClipTypeId ?? "clip";
            if (ExecuteCommand(new DeleteClipCommand(clipId)))
            {
                ClearTimelineSelection();
                _status = $"Deleted {clipName}";
                return true;
            }

            return true;
        }

        if (_selectedTimelineNote != null)
        {
            if (!_document.Chart.Notes.Contains(_selectedTimelineNote))
            {
                _selectedTimelineNote = null;
                return false;
            }

            ChartNote note = _selectedTimelineNote;
            if (ExecuteCommand(new DeleteNoteCommand(note)))
            {
                if (ReferenceEquals(_optionsNote, note))
                {
                    _noteOptionsWindow.Close();
                    ClearPendingOptions();
                }

                ClearTimelineSelection();
                _status = $"Deleted note at {note.SongPosition:0.000}s";
                return true;
            }

            return true;
        }

        if (_selectedTimelineEffect != null)
        {
            if (!_document.Chart.Effects.Contains(_selectedTimelineEffect))
            {
                _selectedTimelineEffect = null;
                return false;
            }

            ChartEffect effect = _selectedTimelineEffect;
            if (ExecuteCommand(new DeleteEffectCommand(effect)))
            {
                if (ReferenceEquals(_optionsEffect, effect))
                {
                    _noteOptionsWindow.Close();
                    ClearPendingOptions();
                }

                ClearTimelineSelection();
                _status = $"Deleted effect at {effect.SongPosition:0.000}s";
                return true;
            }

            return true;
        }

        return false;
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
            ClearTimelineSelection();
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
            ClearTimelineSelection();
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
        _beatmapPlayer.ApplyEditorTimelineEventsAt(songPosition, seekChartPlayer);

        if (seekChartPlayer)
            _beatmapPlayer.ChartPlayer?.Seek(songPosition);
        else
            _beatmapPlayer.ChartPlayer?.Update(songPosition);

        if (resetVisuals)
            _beatmapPlayer.VisualNoteMng?.Reset();

        _beatmapPlayer.VisualNoteMng?.Update(songPosition);
    }

    private void Select(NoteTypeId typeId)
    {
        _selectedNoteTypeId = typeId;
        _status = $"Selected {EditorNoteDefinitions.Get(typeId).DisplayName}";
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
        int currentIndex = definitions.ToList().FindIndex(definition => definition.TypeId == _selectedNoteTypeId);
        int nextIndex = PositiveModulo(currentIndex + delta, definitions.Count);
        Select(definitions[nextIndex].TypeId);
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
                .Where(BeatmapPackagePaths.IsDiscoverableBeatmapChart)
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

    private void OpenBeatmapBrowser()
    {
        RefreshCharts();
        _noteOptionsWindow.Close();
        _metadataWindow.Close();
        ClearPendingOptions();
        _openBeatmapWindow.Close();
        _openBeatmapExplorer.OpenSelectBeatmap(
            "OPEN BEATMAP",
            _document?.ChartPath ?? string.Empty,
            OpenBeatmap,
            () => CloseOpenBeatmapWindow("Open beatmap cancelled"),
            status => _status = status);
    }

    private bool UpdateOpenBeatmapWindow()
    {
        if (!_openBeatmapExplorer.IsOpen)
            return false;

        _openBeatmapExplorer.Update(_mouse, _previousMouse, _keyboard, _previousKeyboard, GLOBALS.graphicsDevice.Viewport);
        return true;
    }

    private void OpenBeatmap(string chartPath)
    {
        if (string.IsNullOrWhiteSpace(chartPath))
            return;

        if (_document?.IsDirty == true)
            Save();

        string songPath = _document?.SongPath ?? _defaultSongPath;
        LoadDocument(songPath, chartPath);
        RememberCurrentBeatmap();
        _openBeatmapWindow.Close();
        _openBeatmapExplorer.Close();
        _status = $"Opened {GetChartDisplayName(chartPath)}";
    }

    private void CloseOpenBeatmapWindow(string status = "Open beatmap closed")
    {
        _openBeatmapWindow.Close();
        _openBeatmapExplorer.Close();
        _status = status;
    }

    private void SetPackageSong()
    {
        if (_document == null)
        {
            _status = "No beatmap loaded";
            return;
        }

        if (!EditorMusicFileDialog.TrySelectMp3(out string sourcePath, out string dialogError))
        {
            _status = string.IsNullOrWhiteSpace(dialogError) ? "Set song cancelled" : dialogError;
            return;
        }

        if (!string.Equals(Path.GetExtension(sourcePath), ".mp3", StringComparison.OrdinalIgnoreCase))
        {
            _status = "Selected song must be an MP3";
            return;
        }

        try
        {
            _document.Save();
            string targetPath = _document.PackageSongPath;
            Directory.CreateDirectory(_document.PackagePath);

            if (!string.Equals(NormalizePath(sourcePath), NormalizePath(targetPath), StringComparison.OrdinalIgnoreCase))
                File.Copy(sourcePath, targetPath, overwrite: true);

            _document.SetSongPath(targetPath);
            _document.Save();
            RefreshCharts();
            SyncSelectedChartIndex();
            RebuildPlayback(false);
            _status = $"Song set to {BeatmapPackagePaths.SongFileName}";
        }
        catch (Exception ex)
        {
            _status = $"Set song failed: {ex.Message}";
        }
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
        _isEditingText = false;
        _noteOptionsWindow.Close();
        ClearPendingOptions();
        _metadataWindow.Open();
        _status = "Editing beatmap metadata";
    }

    private bool UpdateMetadataWindow()
    {
        if (!_metadataWindow.IsOpen)
            return false;

        return _metadataWindow.Update(GetMetadataWindowBounds(), GetMetadataRows());
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
            ResetCustomTextBackspaceRepeat();
            _status = "Edit cancelled";
            return;
        }

        if (DevUiTextInput.ShouldBackspace(_keyboard, _previousKeyboard, ref _customTextBackspaceHoldStartMs, ref _customTextBackspaceLastRepeatMs) && _textBuffer.Length > 0)
            _textBuffer = _textBuffer[..^1];

        foreach (Keys key in _keyboard.GetPressedKeys())
        {
            if (_previousKeyboard.IsKeyDown(key))
                continue;

            if (key == Keys.Back)
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
        else if (field == "MusicVolume" && double.TryParse(_textBuffer, out double musicVolume))
            updated = ExecuteCommand(new SetMusicVolumeCommand(musicVolume));
        else if (field == "BeatmapName")
            updated = ExecuteCommand(new SetMetadataCommand(EditorMetadataField.BeatmapName, _textBuffer), rebuildPlayback: false);
        else if (field == "Beatmapper")
            updated = ExecuteCommand(new SetMetadataCommand(EditorMetadataField.Beatmapper, _textBuffer), rebuildPlayback: false);
        else if (field == "ArtistName")
            updated = ExecuteCommand(new SetMetadataCommand(EditorMetadataField.ArtistName, _textBuffer), rebuildPlayback: false);
        else if (field == "MusicName")
            updated = ExecuteCommand(new SetMetadataCommand(EditorMetadataField.MusicName, _textBuffer), rebuildPlayback: false);

        _isEditingText = false;
        ResetCustomTextBackspaceRepeat();
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

    private string GetToolModeLabel()
    {
        return _toolMode == EditorToolMode.BpmModification ? "BPM" : "SELECT";
    }

    private string GetToolModeMenuLabel(EditorToolMode toolMode, string label)
    {
        return _toolMode == toolMode ? $"> {label}" : label;
    }

    private void DrawEditorShell(SpriteBatch spriteBatch)
    {
        BeatmapEditorLayout layout = GetEditorLayout();
        string dirty = _document.IsDirty ? "DIRTY" : "SAVED";
        string playing = _editorPlaybackPlaying ? "PLAY" : "PAUSE";

        _ui.Fill(spriteBatch, layout.TopBar, new Color(4, 6, 10, 245));
        _ui.Stroke(spriteBatch, layout.TopBar, Color.DarkSlateGray, 1);
        DrawTopBarMenuButton(spriteBatch, EditorTopBarMenu.File, "FILE");
        DrawTopBarMenuButton(spriteBatch, EditorTopBarMenu.Actions, "ACTIONS");
        DrawTopBarMenuButton(spriteBatch, EditorTopBarMenu.Data, "DATA");
        DrawTopBarMenuButton(spriteBatch, EditorTopBarMenu.Tools, "TOOLS");
        _ui.Label(spriteBatch, $"{playing} {dirty} TOOL:{GetToolModeLabel()} BEAT:{CurrentBeatPosition():0.00} BPM:{_document.GetBpmAtBeat(CurrentBeatPosition()):0.##} {_status}", new Vector2(300, 9), Color.White, 2);

        DrawPanel(spriteBatch, layout.RhythmGameListPanel, "RHYTHM GAMES");
        DrawPanel(spriteBatch, layout.PalettePanel, _noteOptionsWindow.IsOpen ? "OPTIONS" : "CLIP PALETTE");

        IReadOnlyList<IEditorNoteProvider> gameProviders = EditorNoteDefinitions.GameProviders;
        for (int i = 0; i < gameProviders.Count; i++)
        {
            IEditorNoteProvider provider = gameProviders[i];
            Rectangle rowBounds = GetRhythmGameRowBounds(i);
            if (provider.Definition.TypeId == _selectedNoteTypeId)
                _ui.Fill(spriteBatch, rowBounds, new Color(24, 58, 36, 180));

            string displayName = provider.RhythmGameDisplayName ?? provider.Definition.DisplayName;
            _ui.Label(spriteBatch, displayName.ToUpperInvariant(), new Vector2(rowBounds.X + 6, rowBounds.Y + 4), provider.Definition.TypeId == _selectedNoteTypeId ? Color.LightGreen : Color.White, 2);
        }

        if (_noteOptionsWindow.IsOpen)
        {
            DrawOpenTopBarMenu(spriteBatch);
            return;
        }

        IReadOnlyList<EditorClipDefinition> paletteClips = GetPaletteClipDefinitions();
        for (int i = 0; i < paletteClips.Count; i++)
        {
            EditorClipDefinition clipDefinition = paletteClips[i];
            Color color = GetClipColor(clipDefinition);
            Rectangle rowBounds = GetPaletteClipBounds(i);
            Rectangle clipPreview = new(rowBounds.X, rowBounds.Y + 4, 46, 24);
            if (IsInstantClip(clipDefinition))
                DrawInstantClipMarker(spriteBatch, new Rectangle(clipPreview.X + 16, clipPreview.Y, 14, clipPreview.Height), color, Color.White);
            else
            {
                _ui.Fill(spriteBatch, clipPreview, color * 0.78f);
                _ui.Fill(spriteBatch, new Rectangle(clipPreview.X, clipPreview.Y, 6, clipPreview.Height), color);
                _ui.Stroke(spriteBatch, clipPreview, Color.White, 1);
            }
            _ui.Label(spriteBatch, clipDefinition.DisplayName, new Vector2(rowBounds.X + 56, rowBounds.Y + 11), Color.White, 2);
        }

        DrawOpenTopBarMenu(spriteBatch);
    }

    private void DrawTopBarMenuButton(SpriteBatch spriteBatch, EditorTopBarMenu menu, string label)
    {
        Rectangle bounds = GetTopBarMenuButtonBounds(menu);
        bool hovered = bounds.Contains(_mouse.Position);
        bool open = _openTopBarMenu == menu;
        Color background = open ? new Color(24, 58, 36, 245) : hovered ? new Color(18, 36, 24, 220) : new Color(4, 6, 10, 0);
        _ui.Fill(spriteBatch, bounds, background);
        _ui.Stroke(spriteBatch, bounds, open ? Color.LightGreen : Color.DarkSlateGray, 1);
        _ui.Label(spriteBatch, label + " v", new Vector2(bounds.X + 6, bounds.Y + 9), open || hovered ? Color.LightGreen : Color.White, 2);
    }

    private void DrawOpenTopBarMenu(SpriteBatch spriteBatch)
    {
        if (_openTopBarMenu == EditorTopBarMenu.None)
            return;

        IReadOnlyList<EditorTopBarMenuItem> items = GetTopBarMenuItems(_openTopBarMenu);
        if (items.Count == 0)
            return;

        Rectangle bounds = GetTopBarDropdownBounds(_openTopBarMenu, items.Count);
        _ui.Fill(spriteBatch, bounds, new Color(8, 10, 14, 245));
        _ui.Stroke(spriteBatch, bounds, Color.LightGreen, 1);

        for (int i = 0; i < items.Count; i++)
        {
            Rectangle rowBounds = GetTopBarMenuItemBounds(_openTopBarMenu, i, items.Count);
            bool hovered = rowBounds.Contains(_mouse.Position);
            _ui.Fill(spriteBatch, rowBounds, hovered ? new Color(24, 58, 36, 245) : new Color(8, 10, 14, 0));
            _ui.Label(spriteBatch, items[i].Label, new Vector2(rowBounds.X + 8, rowBounds.Y + 7), hovered ? Color.LightGreen : Color.White, 2);
        }
    }

    private bool TryGetTopBarMenuAt(Point position, out EditorTopBarMenu menu)
    {
        EditorTopBarMenu[] menus = { EditorTopBarMenu.File, EditorTopBarMenu.Actions, EditorTopBarMenu.Data, EditorTopBarMenu.Tools };
        foreach (EditorTopBarMenu candidate in menus)
        {
            if (!GetTopBarMenuButtonBounds(candidate).Contains(position))
                continue;

            menu = candidate;
            return true;
        }

        menu = EditorTopBarMenu.None;
        return false;
    }

    private Rectangle GetTopBarMenuButtonBounds(EditorTopBarMenu menu)
    {
        return menu switch
        {
            EditorTopBarMenu.File => new Rectangle(0, 0, 58, TimelineHeaderHeight + 2),
            EditorTopBarMenu.Actions => new Rectangle(58, 0, 96, TimelineHeaderHeight + 2),
            EditorTopBarMenu.Data => new Rectangle(154, 0, 66, TimelineHeaderHeight + 2),
            EditorTopBarMenu.Tools => new Rectangle(220, 0, 74, TimelineHeaderHeight + 2),
            _ => Rectangle.Empty
        };
    }

    private Rectangle GetTopBarDropdownBounds(EditorTopBarMenu menu, int itemCount)
    {
        Rectangle button = GetTopBarMenuButtonBounds(menu);
        const int width = 190;
        const int rowHeight = 24;
        return new Rectangle(button.X, button.Bottom, width, Math.Max(1, itemCount) * rowHeight);
    }

    private Rectangle GetTopBarMenuItemBounds(EditorTopBarMenu menu, int index, int itemCount)
    {
        Rectangle dropdown = GetTopBarDropdownBounds(menu, itemCount);
        const int rowHeight = 24;
        return new Rectangle(dropdown.X, dropdown.Y + index * rowHeight, dropdown.Width, rowHeight);
    }

    private IReadOnlyList<EditorTopBarMenuItem> GetTopBarMenuItems(EditorTopBarMenu menu)
    {
        return menu switch
        {
            EditorTopBarMenu.File => new[]
            {
                new EditorTopBarMenuItem("New Beatmap", OpenNewBeatmapModal),
                new EditorTopBarMenuItem("Open Beatmap", OpenBeatmapBrowser),
                new EditorTopBarMenuItem("Save", Save),
                new EditorTopBarMenuItem("Reload", ReloadCurrentDocument),
                new EditorTopBarMenuItem("Refresh Lists", RefreshSongAndChartLists)
            },
            EditorTopBarMenu.Actions => new[]
            {
                new EditorTopBarMenuItem(_editorPlaybackPlaying ? "Pause" : "Play", TogglePlayback),
                new EditorTopBarMenuItem("Preview", StartPreview),
                new EditorTopBarMenuItem(_previewAutoplayEnabled ? "Autoplay: ON" : "Autoplay: OFF", TogglePreviewAutoplay),
                new EditorTopBarMenuItem("Rebuild Playback", () => RebuildPlayback(false)),
                new EditorTopBarMenuItem("Undo", UndoCommand),
                new EditorTopBarMenuItem("Redo", RedoCommand)
            },
            EditorTopBarMenu.Data => new[]
            {
                new EditorTopBarMenuItem("Edit Metadata", BeginMetadataEdit),
                new EditorTopBarMenuItem("Set Song", SetPackageSong),
                new EditorTopBarMenuItem("Open Beatmap", OpenBeatmapBrowser)
            },
            EditorTopBarMenu.Tools => new[]
            {
                new EditorTopBarMenuItem(GetToolModeMenuLabel(EditorToolMode.Selection, "Selection Mode"), EnterSelectionToolMode),
                new EditorTopBarMenuItem(GetToolModeMenuLabel(EditorToolMode.BpmModification, "BPM Edit Mode"), EnterBpmModificationToolMode),
                new EditorTopBarMenuItem("Interval Range", ToggleIntervalRangeSelection),
                new EditorTopBarMenuItem("Options Window", ToggleNoteOptionsWindow)
            },
            _ => Array.Empty<EditorTopBarMenuItem>()
        };
    }

    private void RefreshSongAndChartLists()
    {
        RefreshSongs();
        RefreshCharts();
        _status = "Refreshed songs and charts";
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
        return EditorNoteDefinitions.TryGetProvider(_selectedNoteTypeId, out IEditorNoteProvider provider)
            ? provider.Clips
            : Array.Empty<EditorClipDefinition>();
    }

    private Rectangle GetRhythmGameRowBounds(int index)
    {
        Rectangle panel = GetEditorLayout().RhythmGameListPanel;
        return new Rectangle(panel.X, panel.Y + 26 + index * 24, panel.Width, 24);
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
        Rectangle markerArea = GetTimelineMarkerLaneArea();
        double current = CurrentBeatPosition();
        GetTimelineWindow(out double start, out double end);
        GetSongWindowForBeatWindow(start, end, out double songStart, out double songEnd);

        _ui.Fill(spriteBatch, panel, new Color(12, 14, 20, 235));
        _ui.Stroke(spriteBatch, panel, Color.White, 1);
        DrawTimelineTracks(spriteBatch, panel, area);

        DrawMarkerBeatRuler(spriteBatch, start, end, markerArea);
        DrawTempoGrid(spriteBatch, start, end, area);
        DrawEditorClipMarkers(spriteBatch, start, end, markerArea);
        DrawEditorClips(spriteBatch, start, end, area);
        DrawDraggingClipPreview(spriteBatch, start, end, area, markerArea);

        if (_isSelectingIntervalRange && _intervalRangeStart is double intervalStart)
        {
            float intervalStartX = BeatToX(intervalStart, start, end, area);
            float intervalEndX = BeatToX(SnapPlacementBeat(current), start, end, area);
            _ui.Line(spriteBatch, new Vector2(intervalStartX, area.Y + 18), new Vector2(intervalEndX, area.Y + 18), Color.LightGreen, 3);
            _ui.Fill(spriteBatch, new Rectangle((int)intervalStartX - 4, area.Y + 10, 8, 16), Color.LightGreen);
            _ui.Fill(spriteBatch, new Rectangle((int)intervalEndX - 4, area.Y + 10, 8, 16), Color.LightGreen * 0.7f);
        }

        DrawEffects(spriteBatch, start, end, markerArea);
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
        _ui.Label(spriteBatch, "MARKERS", new Vector2(corner.X + 4, corner.Y + 8), Color.LightGreen, 2);

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

    private void DrawMarkerBeatRuler(SpriteBatch spriteBatch, double windowStart, double windowEnd, Rectangle markerArea)
    {
        if (windowEnd <= windowStart || markerArea.Width <= 0)
            return;

        double divisions = GetEffectiveSnapDivisions();
        double step = GetSnapStep(divisions);
        double first = Math.Ceiling(windowStart / step) * step;
        double pixelsPerBeat = markerArea.Width / (windowEnd - windowStart);
        int labelEveryBeats = GetRulerLabelBeatStep(pixelsPerBeat);

        for (double beat = first; beat <= windowEnd + 0.000001; beat += step)
        {
            bool wholeBeat = Math.Abs(beat - Math.Round(beat)) <= 0.000001;
            float x = BeatToX(beat, windowStart, windowEnd, markerArea);
            if (x < markerArea.X || x > markerArea.Right)
                continue;

            int tickHeight = wholeBeat ? markerArea.Height - 5 : Math.Max(5, markerArea.Height / 3);
            Color color = wholeBeat ? Color.DimGray : Color.DarkSlateGray;
            _ui.Line(spriteBatch, new Vector2(x, markerArea.Bottom - tickHeight), new Vector2(x, markerArea.Bottom), color, wholeBeat ? 2 : 1);

            if (!wholeBeat)
                continue;

            int roundedBeat = (int)Math.Round(beat);
            if (roundedBeat % labelEveryBeats != 0)
                continue;

            _ui.Label(spriteBatch, roundedBeat.ToString(), new Vector2(x + 4, markerArea.Y + 3), Color.LightGray, 1);
        }
    }

    private static int GetRulerLabelBeatStep(double pixelsPerBeat)
    {
        if (pixelsPerBeat >= 44)
            return 1;

        if (pixelsPerBeat >= 24)
            return 2;

        if (pixelsPerBeat >= 12)
            return 4;

        return 8;
    }

    private void DrawEditorClips(SpriteBatch spriteBatch, double windowStart, double windowEnd, Rectangle area)
    {
        if (_document?.EditorClips == null || _document.EditorClips.Count == 0)
            return;

        foreach (ChartEditorClip clip in _document.EditorClips)
        {
            if (clip == null)
                continue;

            EditorClipDefinition definition = EditorClipDefinitions.Find(clip.RhythmGameId, clip.ClipTypeId);
            if (IsInstantClip(definition, clip))
                continue;

            if (!ClipMayOverlapWindow(clip, windowStart, windowEnd))
                continue;

            Rectangle bounds = GetClipBounds(clip, windowStart, windowEnd, area);
            if (bounds.Right < area.X || bounds.X > area.Right)
                continue;

            Color color = GetClipColor(definition);
            _ui.Fill(spriteBatch, bounds, color * 0.78f);
            _ui.Stroke(spriteBatch, bounds, Color.White, 1);
            if (IsTimelineClipSelected(clip))
                _ui.Stroke(spriteBatch, bounds, Color.Yellow, 2);

            _ui.Fill(spriteBatch, new Rectangle(bounds.X, bounds.Y, Math.Min(5, bounds.Width), bounds.Height), color);
            DrawClipHitMarkers(spriteBatch, clip, definition, bounds, windowStart, windowEnd, area, color);
            DrawClipResizeHandles(spriteBatch, bounds, Color.White * 0.8f);

            if (bounds.Width > 58)
                _ui.Label(spriteBatch, definition?.DisplayName ?? clip.ClipTypeId ?? "Clip", new Vector2(bounds.X + 8, bounds.Y + Math.Max(4, bounds.Height / 2 - 6)), Color.White, 2);
        }
    }

    private void DrawEditorClipMarkers(SpriteBatch spriteBatch, double windowStart, double windowEnd, Rectangle markerArea)
    {
        if (_document?.EditorClips == null || _document.EditorClips.Count == 0)
            return;

        foreach (ChartEditorClip clip in _document.EditorClips)
        {
            if (clip == null)
                continue;

            EditorClipDefinition definition = EditorClipDefinitions.Find(clip.RhythmGameId, clip.ClipTypeId);
            if (!IsInstantClip(definition, clip))
                continue;

            Rectangle bounds = GetInstantClipMarkerBounds(clip.StartBeat, windowStart, windowEnd, markerArea);
            if (bounds.Right < markerArea.X || bounds.X > markerArea.Right)
                continue;

            Color color = GetClipColor(definition);
            DrawInstantClipMarker(spriteBatch, bounds, color, IsTimelineClipSelected(clip) ? Color.Yellow : Color.White);
            if (markerArea.Right - bounds.Right > 84)
                _ui.Label(spriteBatch, definition?.DisplayName ?? clip.ClipTypeId ?? "Marker", new Vector2(bounds.Right + 4, markerArea.Y + 8), Color.White, 1);
        }
    }

    private void DrawDraggingClipPreview(SpriteBatch spriteBatch, double windowStart, double windowEnd, Rectangle area, Rectangle markerArea)
    {
        if (_timelineDragKind != EditorTimelineDragKind.ClipCreate
            && _timelineDragKind != EditorTimelineDragKind.ClipMove
            && _timelineDragKind != EditorTimelineDragKind.ClipResizeStart
            && _timelineDragKind != EditorTimelineDragKind.ClipResizeEnd)
            return;

        EditorClipDefinition definition = _draggedClipDefinition;
        if (definition == null)
            return;

        if (IsInstantClip(definition, _draggedClip))
        {
            if (_timelineDragKind == EditorTimelineDragKind.ClipCreate && !_dragMoved)
                return;

            Rectangle markerBounds = GetInstantClipMarkerBounds(_dragPreviewStartBeat, windowStart, windowEnd, markerArea);
            if (markerBounds.Right < markerArea.X || markerBounds.X > markerArea.Right)
                return;

            Color markerColor = GetClipColor(definition);
            DrawInstantClipMarker(spriteBatch, markerBounds, markerColor, Color.Yellow);
            if (markerArea.Right - markerBounds.Right > 84)
                _ui.Label(spriteBatch, definition.DisplayName, new Vector2(markerBounds.Right + 4, markerArea.Y + 8), Color.White, 1);
            return;
        }

        ChartEditorClip previewClip = CreateDragPreviewClip(definition);
        Rectangle bounds = GetClipBounds(previewClip, windowStart, windowEnd, area, CreateDragPreviewRuntimeContext(previewClip));
        if (bounds.Right < area.X || bounds.X > area.Right)
            return;

        Color color = GetClipColor(definition);
        _ui.Fill(spriteBatch, bounds, color * 0.45f);
        _ui.Stroke(spriteBatch, bounds, Color.Yellow, 2);
        _ui.Fill(spriteBatch, new Rectangle(bounds.X, bounds.Y, Math.Min(5, bounds.Width), bounds.Height), color);
        DrawClipHitMarkers(spriteBatch, previewClip, definition, bounds, windowStart, windowEnd, area, color);
        DrawClipResizeHandles(spriteBatch, bounds, Color.Yellow);
        if (bounds.Width > 58)
            _ui.Label(spriteBatch, definition.DisplayName, new Vector2(bounds.X + 8, bounds.Y + Math.Max(4, bounds.Height / 2 - 6)), Color.White, 2);
    }

    private ChartEditorClip CreateDragPreviewClip(EditorClipDefinition definition)
    {
        return new ChartEditorClip
        {
            Id = _draggedClip?.Id ?? "preview",
            TrackIndex = _dragPreviewTrackIndex,
            StartBeat = _dragPreviewStartBeat,
            LengthBeats = Math.Max(0.0, _dragPreviewLengthBeats),
            RhythmGameId = definition.RhythmGameId,
            ClipTypeId = definition.ClipTypeId,
            ClipCategory = definition.Category.ToString(),
            InputAction = string.IsNullOrWhiteSpace(_draggedClip?.InputAction) ? definition.InputAction : _draggedClip.InputAction,
            Data = CreateMergedClipData(definition, _draggedClip?.Data ?? definition.DefaultData)
        };
    }

    private IReadOnlyList<ChartNote> CreateDragPreviewRuntimeContext(ChartEditorClip previewClip)
    {
        if (previewClip == null || _document?.EditorClips == null)
            return null;

        List<ChartEditorClip> clips = new();
        bool replacedDraggedClip = false;
        foreach (ChartEditorClip clip in _document.EditorClips)
        {
            if (clip == null)
                continue;

            if (_draggedClip != null && string.Equals(clip.Id, _draggedClip.Id, StringComparison.Ordinal))
            {
                clips.Add(previewClip);
                replacedDraggedClip = true;
            }
            else
            {
                clips.Add(clip);
            }
        }

        if (!replacedDraggedClip)
            clips.Add(previewClip);

        return EditorClipCompiler.Compile(new Chart { EditorClips = clips }, _document.TempoMap);
    }

    private void DrawClipResizeHandles(SpriteBatch spriteBatch, Rectangle bounds, Color color)
    {
        int handleWidth = Math.Min(6, Math.Max(2, bounds.Width / 4));
        _ui.Fill(spriteBatch, new Rectangle(bounds.X, bounds.Y, handleWidth, bounds.Height), color * 0.5f);
        _ui.Fill(spriteBatch, new Rectangle(bounds.Right - handleWidth, bounds.Y, handleWidth, bounds.Height), color * 0.5f);
    }

    private void DrawClipHitMarkers(SpriteBatch spriteBatch, ChartEditorClip clip, EditorClipDefinition definition, Rectangle bounds, double windowStart, double windowEnd, Rectangle area, Color clipColor)
    {
        if (clip == null || definition == null || definition.Category == EditorClipCategory.NoHit)
            return;

        IReadOnlyList<ChartNote> notes = EditorClipCompiler.CompileClip(clip, _document.TempoMap);
        if (notes.Count == 0)
            return;

        Color markerColor = GetOppositeColor(clipColor);
        foreach (ChartNote note in notes)
        {
            double beat = ChartTiming.GetNoteBeat(note, _document.TempoMap);
            bool isRubyHold = TryGetGemwalkRubyHoldBeats(note, out double rubyHoldBeats);
            if (isRubyHold)
                DrawGemwalkRubyHoldMarker(spriteBatch, beat, rubyHoldBeats, bounds, windowStart, windowEnd, area, markerColor);

            if (beat < windowStart || beat > windowEnd)
                continue;

            int x = (int)MathF.Round(BeatToX(beat, windowStart, windowEnd, area));
            if (x < bounds.X || x > bounds.Right)
                continue;

            _ui.Line(spriteBatch, new Vector2(x, bounds.Y + 2), new Vector2(x, bounds.Bottom - 2), markerColor, isRubyHold ? 3 : 2);
        }
    }

    private bool TryGetGemwalkRubyHoldBeats(ChartNote note, out double holdBeats)
    {
        holdBeats = 0.0;
        if (!GemwalkGlamourNoteCodec.TryReadAction(note?.AdditionnalData, out GemwalkGlamourAction action)
            || action != GemwalkGlamourAction.Ruby)
            return false;

        holdBeats = ChartTiming.GetNoteHoldBeats(note, EditorNoteDefinitions.FromChartNote(note), _document?.TempoMap);
        return holdBeats > 0.0;
    }

    private void DrawGemwalkRubyHoldMarker(SpriteBatch spriteBatch, double hitBeat, double holdBeats, Rectangle bounds, double windowStart, double windowEnd, Rectangle area, Color markerColor)
    {
        double releaseBeat = hitBeat + holdBeats;
        if (releaseBeat < windowStart || hitBeat > windowEnd)
            return;

        float hitX = BeatToX(hitBeat, windowStart, windowEnd, area);
        float releaseX = BeatToX(releaseBeat, windowStart, windowEnd, area);
        float leftX = Math.Clamp(Math.Min(hitX, releaseX), bounds.X, bounds.Right);
        float rightX = Math.Clamp(Math.Max(hitX, releaseX), bounds.X, bounds.Right);
        float centerY = bounds.Y + bounds.Height / 2f;

        if (rightX - leftX > 1f)
        {
            _ui.Line(spriteBatch, new Vector2(leftX, centerY), new Vector2(rightX, centerY), markerColor * 0.35f, 7f);
            _ui.Line(spriteBatch, new Vector2(leftX, centerY), new Vector2(rightX, centerY), markerColor * 0.9f, 2f);
        }

        if (releaseBeat < windowStart || releaseBeat > windowEnd || releaseX < bounds.X || releaseX > bounds.Right)
            return;

        const float capHalfWidth = 5f;
        Color releaseColor = Color.White;
        _ui.Line(spriteBatch, new Vector2(releaseX, bounds.Y + 2), new Vector2(releaseX, bounds.Bottom - 2), releaseColor, 3f);
        _ui.Line(spriteBatch, new Vector2(releaseX - capHalfWidth, bounds.Y + 4), new Vector2(releaseX + capHalfWidth, bounds.Y + 4), releaseColor, 2f);
        _ui.Line(spriteBatch, new Vector2(releaseX - capHalfWidth, bounds.Bottom - 4), new Vector2(releaseX + capHalfWidth, bounds.Bottom - 4), releaseColor, 2f);
    }

    private void DrawInstantClipMarker(SpriteBatch spriteBatch, Rectangle bounds, Color fillColor, Color strokeColor)
    {
        int centerX = bounds.X + bounds.Width / 2;
        _ui.Line(spriteBatch, new Vector2(centerX, bounds.Y), new Vector2(centerX, bounds.Bottom), fillColor, 2);
        Rectangle head = new(bounds.X, bounds.Y + 3, bounds.Width, Math.Max(8, bounds.Height - 6));
        _ui.Fill(spriteBatch, head, fillColor * 0.82f);
        _ui.Stroke(spriteBatch, head, strokeColor, 1);
    }

    private Rectangle GetInstantClipMarkerBounds(double beat, double windowStart, double windowEnd, Rectangle markerArea)
    {
        float markerX = BeatToX(beat, windowStart, windowEnd, markerArea);
        const int markerWidth = 14;
        int markerHeight = Math.Max(12, markerArea.Height - 4);
        int y = markerArea.Y + Math.Max(1, (markerArea.Height - markerHeight) / 2);
        return new Rectangle((int)MathF.Round(markerX) - markerWidth / 2, y, markerWidth, markerHeight);
    }

    private Rectangle GetClipBounds(ChartEditorClip clip, double windowStart, double windowEnd, Rectangle area)
    {
        if (clip == null)
            return Rectangle.Empty;

        EditorClipDefinition definition = EditorClipDefinitions.Find(clip.RhythmGameId, clip.ClipTypeId);
        return GetClipBounds(clip, windowStart, windowEnd, area, _document?.Chart?.Notes);
    }

    private Rectangle GetClipBounds(ChartEditorClip clip, double windowStart, double windowEnd, Rectangle area, IReadOnlyList<ChartNote> contextualNotes)
    {
        if (clip == null)
            return Rectangle.Empty;

        EditorClipDefinition definition = EditorClipDefinitions.Find(clip.RhythmGameId, clip.ClipTypeId);
        ClipActivationRange activationRange = GetClipActivationBeatRange(clip, definition, contextualNotes);
        int laneHeight = GetTrackLaneHeight(area);
        int clampedTrack = Math.Clamp(clip.TrackIndex, 0, TimelineTrackCount - 1);
        float startX = BeatToX(activationRange.StartBeat, windowStart, windowEnd, area);
        float endX = BeatToX(activationRange.EndBeat, windowStart, windowEnd, area);
        int x = (int)Math.Clamp(Math.Min(startX, endX), area.X, area.Right);
        int right = (int)Math.Clamp(Math.Max(startX, endX), area.X, area.Right);
        int laneY = area.Y + clampedTrack * laneHeight;
        int laneBottom = clampedTrack == TimelineTrackCount - 1 ? area.Bottom : laneY + laneHeight;
        return new Rectangle(x, laneY + 3, GetTimelineBlockWidth(right - x), Math.Max(8, laneBottom - laneY - 6));
    }

    private ClipActivationRange GetClipActivationBeatRange(ChartEditorClip clip, EditorClipDefinition definition, IReadOnlyList<ChartNote> contextualNotes)
    {
        ClipActivationRange fallbackRange = new(clip?.StartBeat ?? 0.0, (clip?.StartBeat ?? 0.0) + Math.Max(0.0, clip?.LengthBeats ?? 0.0));

        if (clip == null || definition == null)
            return fallbackRange;

        if (TryGetCachedClipActivationRange(clip, contextualNotes, out ClipActivationRange cachedRange))
            return cachedRange;

        ChartEditorClip previewClip = new()
        {
            Id = clip.Id,
            TrackIndex = clip.TrackIndex,
            StartBeat = clip.StartBeat,
            LengthBeats = Math.Max(0.0, clip.LengthBeats),
            RhythmGameId = definition.RhythmGameId,
            ClipTypeId = definition.ClipTypeId,
            ClipCategory = definition.Category.ToString(),
            InputAction = definition.InputAction,
            Data = CreateMergedClipData(definition, clip.Data)
        };

        IReadOnlyList<ChartNote> generatedNotes = EditorClipCompiler.CompileClip(previewClip, _document.TempoMap);
        if (generatedNotes.Count == 0)
            return CacheClipActivationRange(clip, contextualNotes, fallbackRange);

        double activationStartBeat = double.PositiveInfinity;
        double activationEndBeat = double.NegativeInfinity;
        foreach (ChartNote note in generatedNotes)
        {
            EditorNoteDefinition noteDefinition = EditorNoteDefinitions.FromChartNote(note);
            if (noteDefinition == null)
                continue;

            ChartNote timingNote = FindContextualRuntimeNote(note, noteDefinition, contextualNotes) ?? note;
            IReadOnlyList<ChartNote> timingContext = ReferenceEquals(timingNote, note)
                ? generatedNotes
                : contextualNotes;
            double noteBeat = ChartTiming.GetNoteBeat(timingNote, _document.TempoMap);
            GetNoteActivationBeatRange(timingNote, noteDefinition, noteBeat, timingContext, out double noteStartBeat, out double noteEndBeat);
            activationStartBeat = Math.Min(activationStartBeat, noteStartBeat);
            activationEndBeat = Math.Max(activationEndBeat, noteEndBeat);
        }

        if (double.IsInfinity(activationStartBeat) || double.IsInfinity(activationEndBeat))
        {
            return CacheClipActivationRange(clip, contextualNotes, fallbackRange);
        }

        return CacheClipActivationRange(clip, contextualNotes, new ClipActivationRange(activationStartBeat, activationEndBeat));
    }

    private bool TryGetCachedClipActivationRange(ChartEditorClip clip, IReadOnlyList<ChartNote> contextualNotes, out ClipActivationRange range)
    {
        range = default;
        return CanCacheClipActivationRange(clip, contextualNotes)
            && _clipActivationRangeCache.TryGetValue(clip.Id, out range);
    }

    private ClipActivationRange CacheClipActivationRange(ChartEditorClip clip, IReadOnlyList<ChartNote> contextualNotes, ClipActivationRange range)
    {
        if (CanCacheClipActivationRange(clip, contextualNotes))
            _clipActivationRangeCache[clip.Id] = range;

        return range;
    }

    private bool CanCacheClipActivationRange(ChartEditorClip clip, IReadOnlyList<ChartNote> contextualNotes)
    {
        return clip != null
            && !string.IsNullOrWhiteSpace(clip.Id)
            && ReferenceEquals(contextualNotes, _document?.Chart?.Notes);
    }

    private static bool ClipMayOverlapWindow(ChartEditorClip clip, double windowStart, double windowEnd)
    {
        if (clip == null)
            return false;

        double startBeat = clip.StartBeat - ClipWindowCullPaddingBeats;
        double endBeat = clip.StartBeat + Math.Max(0.0, clip.LengthBeats) + ClipWindowCullPaddingBeats;
        return endBeat >= windowStart && startBeat <= windowEnd;
    }

    private ChartNote FindContextualRuntimeNote(ChartNote generatedNote, EditorNoteDefinition definition, IReadOnlyList<ChartNote> contextualNotes)
    {
        contextualNotes ??= _document?.Chart?.Notes;
        if (generatedNote == null || definition == null || contextualNotes == null)
            return null;

        double generatedBeat = ChartTiming.GetNoteBeat(generatedNote, _document.TempoMap);
        foreach (ChartNote candidate in contextualNotes)
        {
            EditorNoteDefinition candidateDefinition = EditorNoteDefinitions.FromChartNote(candidate);
            if (candidateDefinition == null || candidateDefinition.TypeId != definition.TypeId)
                continue;

            double candidateBeat = ChartTiming.GetNoteBeat(candidate, _document.TempoMap);
            if (Math.Abs(candidateBeat - generatedBeat) > 0.0005)
                continue;

            if (NotePayloadKeys.PayloadDataEquals(candidate.AdditionnalData, generatedNote.AdditionnalData))
                return candidate;
        }

        return null;
    }

    private void GetNoteActivationBeatRange(ChartNote note, EditorNoteDefinition definition, double noteBeat, IReadOnlyList<ChartNote> contextualNotes, out double startBeat, out double endBeat)
    {
        if (definition == null)
        {
            double fallbackHoldBeats = ChartTiming.GetNoteHoldBeats(note, null, _document.TempoMap);
            startBeat = noteBeat;
            endBeat = noteBeat + fallbackHoldBeats;
            return;
        }

        NoteTimingResult timing = _document.GetNoteTiming(note, definition, contextualNotes);
        startBeat = timing.StartBeat;
        endBeat = timing.EndBeat;
    }

    private static Dictionary<string, string> CreateMergedClipData(EditorClipDefinition definition, IReadOnlyDictionary<string, string> data)
    {
        Dictionary<string, string> merged = new(definition?.DefaultData ?? new Dictionary<string, string>());
        if (data == null)
            return merged;

        foreach (KeyValuePair<string, string> pair in data)
            merged[pair.Key] = pair.Value;

        return merged;
    }

    private bool TryHitTimelineClip(Rectangle area, Rectangle markerArea, double windowStart, double windowEnd, out ChartEditorClip clip, out EditorTimelineDragKind dragKind)
    {
        foreach (ChartEditorClip candidate in _document.EditorClips.Reverse())
        {
            if (candidate == null)
                continue;

            EditorClipDefinition definition = EditorClipDefinitions.Find(candidate.RhythmGameId, candidate.ClipTypeId);
            Rectangle bounds = IsInstantClip(definition, candidate)
                ? GetInstantClipMarkerBounds(candidate.StartBeat, windowStart, windowEnd, markerArea)
                : GetClipBounds(candidate, windowStart, windowEnd, area);
            if (!bounds.Contains(_mouse.Position))
                continue;

            if (IsInstantClip(definition, candidate))
            {
                dragKind = EditorTimelineDragKind.ClipMove;
                clip = candidate;
                return true;
            }

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

        bool isInstant = IsInstantClip(definition);

        ChartEditorClip clip = new()
        {
            Id = Guid.NewGuid().ToString("N"),
            TrackIndex = isInstant ? 0 : Math.Clamp(trackIndex, 0, TimelineTrackCount - 1),
            StartBeat = startBeat,
            LengthBeats = isInstant ? 0.0 : Math.Max(0.0, lengthBeats),
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

    private void DrawEffects(SpriteBatch spriteBatch, double windowStart, double windowEnd, Rectangle markerArea)
    {
        GetSongWindowForBeatWindow(windowStart, windowEnd, out double songStart, out double songEnd);
        foreach (ChartEffect effect in _document.GetEffectsInWindow(songStart, songEnd))
        {
            EditorEffectDefinition definition = EditorEffectDefinitions.FromChartEffect(effect);
            if (definition == null)
                continue;

            Color color = GetEffectColor(definition.Kind);
            Rectangle marker = GetEffectMarkerBounds(effect, windowStart, windowEnd, markerArea);
            DrawInstantClipMarker(spriteBatch, marker, color, IsTimelineEffectSelected(effect) ? Color.Yellow : Color.White);
            if (markerArea.Right - marker.Right > 84)
                _ui.Label(spriteBatch, GetEffectLabel(effect, definition), new Vector2(marker.Right + 4, markerArea.Y + 8), color, 1);
        }

        if (_optionsIsEffect && _optionsIsCreation && _optionsEffect != null && _document.GetEffectBeat(_optionsEffect) >= windowStart && _document.GetEffectBeat(_optionsEffect) <= windowEnd)
        {
            EditorEffectDefinition definition = _optionsEffectDefinition ?? EditorEffectDefinitions.FromChartEffect(_optionsEffect);
            if (definition == null)
                return;

            Color color = GetEffectColor(definition.Kind);
            Rectangle marker = GetEffectMarkerBounds(_optionsEffect, windowStart, windowEnd, markerArea);
            DrawInstantClipMarker(spriteBatch, marker, color * 0.65f, Color.Yellow);
            if (markerArea.Right - marker.Right > 84)
                _ui.Label(spriteBatch, GetEffectLabel(_optionsEffect, definition), new Vector2(marker.Right + 4, markerArea.Y + 8), Color.White, 1);
        }
    }

    private void DrawIntervalPreview(SpriteBatch spriteBatch, double windowStart, double windowEnd, Rectangle area)
    {
        if (!_optionsIsCreation || !_optionsIsIntervalCreation || _optionsNote == null || _optionsDefinition == null)
            return;

        IReadOnlyList<EditorNotePlacement> placements = _optionsDefinition.CreatePlacements(_optionsNote, CreatePlacementContext(), _pendingPlacementOptions);
        IReadOnlyList<ChartNote> placementNotes = placements.Select(item => item.Note).ToList();
        foreach (EditorNotePlacement placement in placements)
        {
            ChartNote note = placement.Note;
            if (note == null || placement.Definition == null)
                continue;

            double noteBeat = _document.GetNoteBeat(note);
            GetNoteActivationBeatRange(note, placement.Definition, noteBeat, placementNotes, out double noteStartBeat, out double noteEndBeat);
            if (noteEndBeat < windowStart || noteStartBeat > windowEnd)
                continue;

            Color color = GetNoteColor(placement.Definition, note);
            float noteX = BeatToX(noteStartBeat, windowStart, windowEnd, area);
            float noteEndX = BeatToX(noteEndBeat, windowStart, windowEnd, area);
            int laneHeight = GetTrackLaneHeight(area);
            int x = (int)Math.Clamp(Math.Min(noteX, noteEndX), area.X, area.Right);
            int right = (int)Math.Clamp(Math.Max(noteX, noteEndX), area.X, area.Right);
            Rectangle previewBounds = new(x, area.Y + 3, GetTimelineBlockWidth(right - x), Math.Max(8, laneHeight - 6));
            _ui.Fill(spriteBatch, previewBounds, color * 0.45f);
            _ui.Stroke(spriteBatch, previewBounds, Color.White, 2);
        }
    }

    private void DrawHud(SpriteBatch spriteBatch)
    {
        _noteOptionsWindow.Draw(spriteBatch, GetNoteOptionsWindowBounds(), GetNoteOptionsTitle(), GetNoteOptionRows());
        _metadataWindow.Draw(spriteBatch, GetMetadataWindowBounds(), "BEATMAP METADATA", GetMetadataRows());

        _openBeatmapExplorer.Draw(spriteBatch, GLOBALS.graphicsDevice.Viewport, _mouse.Position);
        _newBeatmapExplorer.Draw(spriteBatch, GLOBALS.graphicsDevice.Viewport, _mouse.Position);
    }

    private void DrawNewBeatmapExplorer(SpriteBatch spriteBatch)
    {
        Viewport viewport = GLOBALS.graphicsDevice.Viewport;
        Rectangle overlay = new(0, 0, viewport.Width, viewport.Height);
        Rectangle modal = GetNewBeatmapExplorerBounds();

        _ui.Fill(spriteBatch, overlay, new Color(0, 0, 0, 170));
        _ui.Fill(spriteBatch, modal, new Color(4, 6, 10, 245));
        _ui.Stroke(spriteBatch, modal, Color.LightGreen, 2);

        DrawNewBeatmapExplorerHeader(spriteBatch, modal);
        DrawNewBeatmapSidebar(spriteBatch, GetNewBeatmapSidebarBounds(modal));
        DrawNewBeatmapFolderList(spriteBatch, GetNewBeatmapFolderListBounds(modal));
        DrawNewBeatmapActions(spriteBatch, GetNewBeatmapActionsBounds(modal));
    }

    private void DrawNewBeatmapExplorerHeader(SpriteBatch spriteBatch, Rectangle modal)
    {
        Rectangle header = new(modal.X, modal.Y, modal.Width, 54);
        Rectangle closeButton = GetNewBeatmapHeaderCloseButtonBounds(modal);
        _ui.Fill(spriteBatch, header, new Color(18, 36, 24, 245));
        _ui.Line(spriteBatch, new Vector2(modal.X, header.Bottom), new Vector2(modal.Right, header.Bottom), Color.DarkSlateGray, 1);
        _ui.Label(spriteBatch, "NEW BEATMAP", new Vector2(modal.X + 18, modal.Y + 14), Color.LightGreen, 2);

        string path = GetNewBeatmapExplorerDisplayPath(_newBeatmapFolderPath);
        int pathWidth = Math.Max(80, closeButton.X - modal.X - 210);
        DrawExplorerFittedLabel(spriteBatch, path, new Vector2(modal.X + 190, modal.Y + 15), Color.White, 2, pathWidth);
        DrawExplorerButton(spriteBatch, closeButton, "X");
    }

    private void DrawNewBeatmapSidebar(SpriteBatch spriteBatch, Rectangle sidebar)
    {
        _ui.Fill(spriteBatch, sidebar, new Color(8, 10, 14, 235));
        _ui.Stroke(spriteBatch, sidebar, Color.DarkSlateGray, 1);
        _ui.Label(spriteBatch, "LOCATIONS", new Vector2(sidebar.X + 12, sidebar.Y + 12), Color.LightBlue, 2);

        DrawExplorerButton(spriteBatch, GetNewBeatmapSidebarRootButtonBounds(sidebar), "BEATMAPS", IsBeatmapsRoot(_newBeatmapFolderPath));

        if (!IsBeatmapsRoot(_newBeatmapFolderPath))
            DrawExplorerButton(spriteBatch, GetNewBeatmapSidebarParentButtonBounds(sidebar), "PARENT");

        Rectangle note = new(sidebar.X + 12, sidebar.Bottom - 94, sidebar.Width - 24, 82);
        _ui.Fill(spriteBatch, note, new Color(4, 6, 10, 210));
        _ui.Stroke(spriteBatch, note, Color.DarkSlateGray, 1);
        _ui.Label(spriteBatch, "PACKAGES ARE HIDDEN\nHERE. USE OPEN\nBEATMAP TO LOAD\nEXISTING CHARTS.", new Vector2(note.X + 8, note.Y + 10), Color.DarkSeaGreen, 1);
    }

    private void DrawNewBeatmapFolderList(SpriteBatch spriteBatch, Rectangle list)
    {
        IReadOnlyList<string> folders = GetNewBeatmapChildFolders();
        Rectangle content = GetNewBeatmapFolderListContentBounds(list);

        _ui.Fill(spriteBatch, list, new Color(8, 10, 14, 235));
        _ui.Stroke(spriteBatch, list, Color.DarkSlateGray, 1);
        _ui.Label(spriteBatch, "FOLDERS", new Vector2(list.X + 12, list.Y + 12), Color.LightBlue, 2);
        _ui.Line(spriteBatch, new Vector2(list.X + 10, content.Y - 8), new Vector2(list.Right - 10, content.Y - 8), Color.DarkSlateGray, 1);

        if (folders.Count == 0)
        {
            DrawExplorerFittedLabel(spriteBatch, "No subfolders. Create one or create the beatmap here.", new Vector2(content.X + 8, content.Y + 10), Color.DarkSeaGreen, 2, content.Width - 16);
            return;
        }

        for (int i = 0; i < folders.Count; i++)
        {
            Rectangle row = GetNewBeatmapFolderRowBounds(list, i);
            if (!content.Intersects(row))
                continue;

            bool hovered = row.Contains(_mouse.Position);
            _ui.Fill(spriteBatch, row, hovered ? new Color(24, 58, 36, 235) : new Color(4, 6, 10, 160));
            _ui.Stroke(spriteBatch, row, hovered ? Color.LightGreen : Color.DarkSlateGray, 1);
            DrawFolderIcon(spriteBatch, new Rectangle(row.X + 10, row.Y + 8, 20, 16), hovered ? Color.LightGreen : Color.Goldenrod);

            string label = Path.GetFileName(folders[i]);
            DrawExplorerFittedLabel(spriteBatch, label, new Vector2(row.X + 42, row.Y + 10), hovered ? Color.LightGreen : Color.White, 2, row.Width - 54);
        }

        DrawNewBeatmapFolderListScrollbar(spriteBatch, list, folders.Count);
    }

    private void DrawNewBeatmapActions(SpriteBatch spriteBatch, Rectangle actions)
    {
        _ui.Fill(spriteBatch, actions, new Color(8, 10, 14, 235));
        _ui.Stroke(spriteBatch, actions, Color.DarkSlateGray, 1);
        _ui.Label(spriteBatch, "CURRENT FOLDER", new Vector2(actions.X + 14, actions.Y + 14), Color.LightBlue, 2);

        Rectangle currentBox = new(actions.X + 14, actions.Y + 44, actions.Width - 28, 34);
        DrawExplorerValueBox(spriteBatch, currentBox, GetNewBeatmapExplorerDisplayPath(_newBeatmapFolderPath), Color.White);

        _ui.Label(spriteBatch, "WILL CREATE", new Vector2(actions.X + 14, actions.Y + 92), Color.LightBlue, 2);
        Rectangle createBox = new(actions.X + 14, actions.Y + 122, actions.Width - 28, 34);
        DrawExplorerValueBox(spriteBatch, createBox, GetChartDisplayName(GetAvailableNewBeatmapPath(_newBeatmapFolderPath)), Color.LightGreen);

        _ui.Label(spriteBatch, "NEW SUBFOLDER NAME", new Vector2(actions.X + 14, actions.Y + 170), Color.LightBlue, 2);
        DrawExplorerTextInput(spriteBatch, GetNewBeatmapFolderNameInputBounds(actions));
        DrawExplorerButton(spriteBatch, GetNewBeatmapCreateFolderButtonBounds(actions), "CREATE FOLDER");
        DrawExplorerButton(spriteBatch, GetNewBeatmapCreateBeatmapButtonBounds(actions), "CREATE BEATMAP HERE", primary: true);
        DrawExplorerButton(spriteBatch, GetNewBeatmapActionCancelButtonBounds(actions), "CANCEL");

        Rectangle createFolderButton = GetNewBeatmapCreateFolderButtonBounds(actions);
        _ui.Label(spriteBatch, "ENTER CREATES HERE\nESC CANCELS", new Vector2(actions.X + 14, createFolderButton.Bottom + 10), Color.DarkSeaGreen, 1);
    }

    private void DrawExplorerButton(SpriteBatch spriteBatch, Rectangle bounds, string text, bool selected = false, bool primary = false)
    {
        bool hovered = bounds.Contains(_mouse.Position);
        Color fill = primary
            ? hovered ? new Color(34, 84, 52, 245) : new Color(24, 58, 36, 245)
            : selected ? new Color(24, 58, 36, 245) : hovered ? new Color(18, 36, 24, 245) : new Color(4, 6, 10, 230);
        Color stroke = primary || hovered || selected ? Color.LightGreen : Color.DarkSlateGray;
        Color label = primary || hovered || selected ? Color.LightGreen : Color.White;

        _ui.Fill(spriteBatch, bounds, fill);
        _ui.Stroke(spriteBatch, bounds, stroke, primary ? 2 : 1);
        DrawExplorerFittedLabel(spriteBatch, text, new Vector2(bounds.X + 10, bounds.Y + 9), label, 2, bounds.Width - 20);
    }

    private void DrawExplorerTextInput(SpriteBatch spriteBatch, Rectangle bounds)
    {
        bool empty = string.IsNullOrEmpty(_newBeatmapNameBuffer);
        string text = empty && !_newBeatmapFolderNameFocused ? "<folder name>" : _newBeatmapNameBuffer;
        if (_newBeatmapFolderNameFocused)
            text += "|";

        Color stroke = _newBeatmapFolderNameFocused ? Color.Yellow : Color.LightGreen;
        Color label = empty && !_newBeatmapFolderNameFocused ? Color.DarkSeaGreen : _newBeatmapFolderNameFocused ? Color.Yellow : Color.White;
        _ui.Fill(spriteBatch, bounds, Color.Black * 0.85f);
        _ui.Stroke(spriteBatch, bounds, stroke, 1);
        DrawExplorerFittedLabel(spriteBatch, text, new Vector2(bounds.X + 8, bounds.Y + 8), label, 2, bounds.Width - 16);
    }

    private void DrawExplorerValueBox(SpriteBatch spriteBatch, Rectangle bounds, string text, Color color)
    {
        _ui.Fill(spriteBatch, bounds, Color.Black * 0.75f);
        _ui.Stroke(spriteBatch, bounds, Color.DarkSlateGray, 1);
        DrawExplorerFittedLabel(spriteBatch, text, new Vector2(bounds.X + 8, bounds.Y + 10), color, 2, bounds.Width - 16);
    }

    private void DrawFolderIcon(SpriteBatch spriteBatch, Rectangle bounds, Color color)
    {
        Rectangle tab = new(bounds.X + 2, bounds.Y, Math.Max(6, bounds.Width / 2), 5);
        Rectangle body = new(bounds.X, bounds.Y + 4, bounds.Width, bounds.Height - 4);
        _ui.Fill(spriteBatch, tab, color * 0.75f);
        _ui.Fill(spriteBatch, body, color * 0.65f);
        _ui.Stroke(spriteBatch, body, color, 1);
    }

    private void DrawNewBeatmapFolderListScrollbar(SpriteBatch spriteBatch, Rectangle list, int folderCount)
    {
        Rectangle content = GetNewBeatmapFolderListContentBounds(list);
        int contentHeight = folderCount * 34;
        if (contentHeight <= content.Height)
            return;

        Rectangle track = new(list.Right - 12, content.Y, 4, content.Height);
        int thumbHeight = Math.Max(18, track.Height * content.Height / contentHeight);
        int maxScroll = GetNewBeatmapFolderListMaxScroll(list, folderCount);
        int thumbY = maxScroll == 0 ? track.Y : track.Y + (track.Height - thumbHeight) * _newBeatmapFolderListScroll / maxScroll;
        _ui.Fill(spriteBatch, track, Color.DarkSlateGray);
        _ui.Fill(spriteBatch, new Rectangle(track.X, thumbY, track.Width, thumbHeight), Color.LightGreen);
    }

    private void DrawExplorerFittedLabel(SpriteBatch spriteBatch, string text, Vector2 position, Color color, int scale, int maxWidth)
    {
        _ui.Label(spriteBatch, FitDevUiText(text, maxWidth, scale), position, color, scale);
    }

    private string GetNoteOptionsTitle()
    {
        if (_optionsIsEffect)
            return _effectOptionsPanel?.Title ?? "EFFECT OPTIONS";

        if (_optionsIsClip)
            return $"CLIP {_optionsClipDefinition?.DisplayName?.ToUpperInvariant() ?? "OPTIONS"}";

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

    private Rectangle GetTimelineMarkerLaneArea()
    {
        Rectangle panel = GetTimelinePanelArea();
        return new Rectangle(
            panel.X + TimelineTrackLabelWidth,
            panel.Y,
            Math.Max(1, panel.Width - TimelineTrackLabelWidth),
            TimelineHeaderHeight);
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
        GetNoteActivationBeatRange(note, definition, noteBeat, _document.Chart.Notes, out double noteStartBeat, out double noteEndBeat);
        float noteX = BeatToX(noteStartBeat, start, end, area);
        float noteEndX = BeatToX(noteEndBeat, start, end, area);
        int laneHeight = GetTrackLaneHeight(area);
        int x = (int)Math.Clamp(Math.Min(noteX, noteEndX), area.X, area.Right);
        int right = (int)Math.Clamp(Math.Max(noteX, noteEndX), area.X, area.Right);
        return new Rectangle(x, area.Y + 3, GetTimelineBlockWidth(right - x), Math.Max(8, laneHeight - 6));
    }

    private static int GetTimelineBlockWidth(int projectedWidth)
    {
        return Math.Max(1, projectedWidth);
    }

    private Rectangle GetEffectMarkerBounds(ChartEffect effect, double start, double end, Rectangle area)
    {
        float effectX = BeatToX(_document.GetEffectBeat(effect), start, end, area);
        const int markerWidth = 14;
        int markerHeight = Math.Max(12, area.Height - 4);
        int y = area.Y + Math.Max(1, (area.Height - markerHeight) / 2);
        return new Rectangle((int)MathF.Round(effectX) - markerWidth / 2, y, markerWidth, markerHeight);
    }

    private Color GetNoteColor(EditorNoteDefinition definition, ChartNote note)
    {
        return EditorNoteDefinitions.GetEditorStyle(definition, note).Color;
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

        if (definition.Category == EditorClipCategory.NoHit || definition.ClipTypeId == EditorClipDefinitions.NoHit)
            return Color.DimGray;

        if (EditorClipDefinitions.IsSwitchGame(definition))
            return definition.EditorStyle?.Color ?? Color.LightGreen;

        return definition.EditorStyle?.Color ?? Color.CornflowerBlue;
    }

    private static Color GetOppositeColor(Color color)
    {
        return new Color(255 - color.R, 255 - color.G, 255 - color.B, color.A);
    }

    private static bool IsInstantClip(EditorClipDefinition definition)
    {
        return definition?.Category == EditorClipCategory.Instant;
    }

    private static bool IsInstantClip(EditorClipDefinition definition, ChartEditorClip clip)
    {
        return IsInstantClip(definition)
            || EditorClipDefinitions.IsSwitchGame(clip)
            || string.Equals(clip?.ClipCategory, EditorClipCategory.Instant.ToString(), StringComparison.OrdinalIgnoreCase);
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

    private Rectangle GetNewBeatmapExplorerBounds()
    {
        Viewport viewport = GLOBALS.graphicsDevice.Viewport;
        int width = Math.Clamp(viewport.Width - 100, 760, 1120);
        int height = Math.Clamp(viewport.Height - 120, 480, 720);
        return new Rectangle((viewport.Width - width) / 2, (viewport.Height - height) / 2, width, height);
    }

    private static Rectangle GetNewBeatmapSidebarBounds(Rectangle modal)
    {
        return new Rectangle(modal.X + 16, modal.Y + 70, 170, modal.Height - 86);
    }

    private static Rectangle GetNewBeatmapActionsBounds(Rectangle modal)
    {
        const int width = 276;
        return new Rectangle(modal.Right - width - 16, modal.Y + 70, width, modal.Height - 86);
    }

    private static Rectangle GetNewBeatmapFolderListBounds(Rectangle modal)
    {
        Rectangle sidebar = GetNewBeatmapSidebarBounds(modal);
        Rectangle actions = GetNewBeatmapActionsBounds(modal);
        int x = sidebar.Right + 14;
        return new Rectangle(x, modal.Y + 70, Math.Max(1, actions.X - x - 14), modal.Height - 86);
    }

    private static Rectangle GetNewBeatmapFolderNameInputBounds(Rectangle actions)
    {
        return new Rectangle(actions.X + 14, actions.Y + 198, actions.Width - 28, 30);
    }

    private static Rectangle GetNewBeatmapHeaderCloseButtonBounds(Rectangle modal)
    {
        return new Rectangle(modal.Right - 48, modal.Y + 12, 32, 30);
    }

    private static Rectangle GetNewBeatmapSidebarRootButtonBounds(Rectangle sidebar)
    {
        return new Rectangle(sidebar.X + 12, sidebar.Y + 46, sidebar.Width - 24, 34);
    }

    private static Rectangle GetNewBeatmapSidebarParentButtonBounds(Rectangle sidebar)
    {
        return new Rectangle(sidebar.X + 12, sidebar.Y + 88, sidebar.Width - 24, 34);
    }

    private static Rectangle GetNewBeatmapFolderListContentBounds(Rectangle list)
    {
        return new Rectangle(list.X + 10, list.Y + 44, list.Width - 20, list.Height - 54);
    }

    private Rectangle GetNewBeatmapFolderRowBounds(Rectangle list, int index)
    {
        Rectangle content = GetNewBeatmapFolderListContentBounds(list);
        return new Rectangle(content.X, content.Y + index * 34 - _newBeatmapFolderListScroll, content.Width - 8, 32);
    }

    private static int GetNewBeatmapFolderListMaxScroll(Rectangle list, int folderCount)
    {
        Rectangle content = GetNewBeatmapFolderListContentBounds(list);
        return Math.Max(0, folderCount * 34 - content.Height);
    }

    private static Rectangle GetNewBeatmapCreateFolderButtonBounds(Rectangle actions)
    {
        Rectangle input = GetNewBeatmapFolderNameInputBounds(actions);
        return new Rectangle(actions.X + 14, input.Bottom + 10, actions.Width - 28, 34);
    }

    private static Rectangle GetNewBeatmapCreateBeatmapButtonBounds(Rectangle actions)
    {
        return new Rectangle(actions.X + 14, actions.Bottom - 94, actions.Width - 28, 42);
    }

    private static Rectangle GetNewBeatmapActionCancelButtonBounds(Rectangle actions)
    {
        return new Rectangle(actions.X + 14, actions.Bottom - 44, actions.Width - 28, 32);
    }

    private Rectangle GetOpenBeatmapWindowBounds()
    {
        Viewport viewport = GLOBALS.graphicsDevice.Viewport;
        int width = Math.Clamp(viewport.Width - 100, 520, 780);
        int height = Math.Clamp(viewport.Height - 120, 320, 560);
        return new Rectangle((viewport.Width - width) / 2, (viewport.Height - height) / 2, width, height);
    }

    private Rectangle GetMetadataWindowBounds()
    {
        Viewport viewport = GLOBALS.graphicsDevice.Viewport;
        int width = Math.Clamp(viewport.Width - 80, 480, 820);
        int height = Math.Clamp(viewport.Height - 80, 360, 640);
        return new Rectangle((viewport.Width - width) / 2, (viewport.Height - height) / 2, width, height);
    }

    private IReadOnlyList<DevUiWindowRow> GetOpenBeatmapRows()
    {
        List<DevUiWindowRow> rows = new()
        {
            DevUiWindowRow.Value("Current", _document?.ChartPath ?? "<none>"),
            DevUiWindowRow.Separator("Beatmaps")
        };

        if (_availableCharts.Count == 0)
            rows.Add(DevUiWindowRow.Title("No beatmaps found"));

        for (int i = 0; i < _availableCharts.Count; i++)
        {
            string chartPath = _availableCharts[i];
            string label = GetOpenBeatmapLabel(chartPath);
            string selectedChartPath = chartPath;
            rows.Add(DevUiWindowRow.Button(label, () => OpenBeatmap(selectedChartPath)));
        }

        rows.Add(DevUiWindowRow.Separator("Actions"));
        rows.Add(DevUiWindowRow.Button("REFRESH", RefreshOpenBeatmapList));
        rows.Add(DevUiWindowRow.Button("CLOSE", () => CloseOpenBeatmapWindow()));
        return rows;
    }

    private string GetOpenBeatmapLabel(string chartPath)
    {
        string displayName = GetChartDisplayName(chartPath);
        bool current = string.Equals(NormalizePath(chartPath), NormalizePath(_document?.ChartPath ?? string.Empty), StringComparison.OrdinalIgnoreCase);
        return current ? $"> {displayName}" : displayName;
    }

    private void RefreshOpenBeatmapList()
    {
        RefreshCharts();
        _status = "Beatmap list refreshed";
    }

    private IReadOnlyList<DevUiWindowRow> GetMetadataRows()
    {
        if (_document == null)
            return Array.Empty<DevUiWindowRow>();

        return new[]
        {
            DevUiWindowRow.Category("Core"),
            MetadataTextInput(EditorMetadataField.BeatmapName, "Beatmap Name"),
            MetadataTextInput(EditorMetadataField.Beatmapper, "Beatmapper"),
            MetadataTextInput(EditorMetadataField.SongName, "Song Name"),
            MetadataTextInput(EditorMetadataField.ArtistName, "Artist"),
            MetadataTextInput(EditorMetadataField.MusicName, "Music Name"),
            MetadataTextInput(EditorMetadataField.Description, "Description"),
            MetadataTextInput(EditorMetadataField.Tags, "Tags (comma)"),
            DevUiWindowRow.Checkbox("Flashing Effects Warning", _document.GetFlashingEffectsWarning(), ToggleMetadataFlashWarning),

            DevUiWindowRow.Category("Timing"),
            DevUiWindowRow.FloatInput("metadata.bpm", "BPM", _document.Chart.BPM, SetMetadataBpm),
            DevUiWindowRow.FloatInput("metadata.offset", "Offset", _document.Chart.Offset, SetMetadataOffset),
            DevUiWindowRow.FloatInput("metadata.leadIn", "Lead-In Beats", _document.Chart.LeadInBeats, SetMetadataLeadInBeats),

            DevUiWindowRow.Category("Audio"),
            DevUiWindowRow.FloatInput("metadata.musicVolume", "Music Volume", _document.Chart.MusicVolume, SetMetadataMusicVolume),

            DevUiWindowRow.Category("Package"),
            DevUiWindowRow.Value("Chart", _document.ChartPath ?? string.Empty),
            DevUiWindowRow.Value("Song", string.IsNullOrWhiteSpace(_document.SongPath) ? "<none>" : _document.SongPath),
            DevUiWindowRow.Button("SET SONG", SetPackageSong),

            DevUiWindowRow.Category("Images"),
            MetadataTextInput(EditorMetadataField.LevelIconPath, "Level Icon Path"),
            DevUiWindowRow.Button("IMPORT LEVEL ICON", () => ImportMetadataImage(EditorMetadataField.LevelIconPath, "icons")),

            DevUiWindowRow.Category("Rating"),
            MetadataTextInput(EditorMetadataField.RatingHeader, "Rating Header"),
            MetadataTextInput(EditorMetadataField.RatingTryAgainMessage, "Try Again Message"),
            MetadataTextInput(EditorMetadataField.RatingTryAgainImagePath, "Try Again Image"),
            DevUiWindowRow.Button("IMPORT TRY AGAIN IMAGE", () => ImportMetadataImage(EditorMetadataField.RatingTryAgainImagePath, "ratings")),
            MetadataTextInput(EditorMetadataField.RatingOkMessage, "OK Message"),
            MetadataTextInput(EditorMetadataField.RatingOkImagePath, "OK Image"),
            DevUiWindowRow.Button("IMPORT OK IMAGE", () => ImportMetadataImage(EditorMetadataField.RatingOkImagePath, "ratings")),
            MetadataTextInput(EditorMetadataField.RatingSuperbMessage, "Superb Message"),
            MetadataTextInput(EditorMetadataField.RatingSuperbImagePath, "Superb Image"),
            DevUiWindowRow.Button("IMPORT SUPERB IMAGE", () => ImportMetadataImage(EditorMetadataField.RatingSuperbImagePath, "ratings")),

            DevUiWindowRow.Separator("Actions"),
            DevUiWindowRow.Button("SAVE CHART", Save),
            DevUiWindowRow.Button("CLOSE", CloseMetadataWindow)
        };
    }

    private DevUiWindowRow MetadataTextInput(EditorMetadataField field, string label)
    {
        return DevUiWindowRow.TextInput($"metadata.{field}", label, _document.GetMetadataField(field), value => SetMetadataText(field, label, value));
    }

    private void SetMetadataText(EditorMetadataField field, string label, string value)
    {
        if (ExecuteCommand(new SetMetadataCommand(field, value), rebuildPlayback: false))
            _status = $"Updated {label}";
    }

    private void SetMetadataBpm(double bpm)
    {
        if (bpm <= 0)
        {
            _status = "BPM must be greater than 0";
            return;
        }

        if (ExecuteCommand(new SetBpmCommand(bpm)))
            _status = $"Updated BPM to {bpm.ToString("0.###", CultureInfo.InvariantCulture)}";
    }

    private void SetMetadataOffset(double offset)
    {
        if (ExecuteCommand(new SetOffsetCommand(offset)))
            _status = $"Updated offset to {offset.ToString("0.###", CultureInfo.InvariantCulture)}";
    }

    private void SetMetadataLeadInBeats(double leadInBeats)
    {
        if (ExecuteCommand(new SetLeadInBeatsCommand(Math.Max(0.0, leadInBeats))))
            _status = $"Updated lead-in to {Math.Max(0.0, leadInBeats).ToString("0.###", CultureInfo.InvariantCulture)} beats";
    }

    private void SetMetadataMusicVolume(double musicVolume)
    {
        double normalizedVolume = double.IsNaN(musicVolume) || double.IsInfinity(musicVolume)
            ? Chart.DefaultMusicVolume
            : Math.Clamp(musicVolume, 0.0, Chart.MaxMusicVolume);

        if (ExecuteCommand(new SetMusicVolumeCommand(normalizedVolume)))
            _status = $"Updated music volume to {normalizedVolume.ToString("0.###", CultureInfo.InvariantCulture)}";
    }

    private void ToggleMetadataFlashWarning()
    {
        bool nextValue = !_document.GetFlashingEffectsWarning();
        if (ExecuteCommand(new SetFlashingEffectsWarningCommand(nextValue), rebuildPlayback: false))
            _status = nextValue ? "Enabled flashing effects warning" : "Disabled flashing effects warning";
    }

    private void ImportMetadataImage(EditorMetadataField field, string targetSubfolder)
    {
        if (!EditorMusicFileDialog.TrySelectImage(out string sourcePath, out string dialogError))
        {
            _status = string.IsNullOrWhiteSpace(dialogError) ? "Image import cancelled" : dialogError;
            return;
        }

        string extension = Path.GetExtension(sourcePath);
        if (!string.Equals(extension, ".png", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(extension, ".jpg", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase))
        {
            _status = "Selected image must be PNG or JPG";
            return;
        }

        string label = GetMetadataFieldLabel(field);
        if (ExecuteCommand(new ImportBeatmapAssetCommand(sourcePath, targetSubfolder, field), rebuildPlayback: false))
            _status = $"Imported {label}";
    }

    private void CloseMetadataWindow()
    {
        _metadataWindow.Close();
        _status = "Metadata closed";
    }

    private static string GetMetadataFieldLabel(EditorMetadataField field)
    {
        return field switch
        {
            EditorMetadataField.BeatmapName => "Beatmap Name",
            EditorMetadataField.Beatmapper => "Beatmapper",
            EditorMetadataField.Description => "Description",
            EditorMetadataField.Tags => "Tags",
            EditorMetadataField.SongName => "Song Name",
            EditorMetadataField.ArtistName => "Artist",
            EditorMetadataField.MusicName => "Music Name",
            EditorMetadataField.LevelIconPath => "Level Icon",
            EditorMetadataField.RatingHeader => "Rating Header",
            EditorMetadataField.RatingTryAgainMessage => "Try Again Message",
            EditorMetadataField.RatingTryAgainImagePath => "Try Again Image",
            EditorMetadataField.RatingOkMessage => "OK Message",
            EditorMetadataField.RatingOkImagePath => "OK Image",
            EditorMetadataField.RatingSuperbMessage => "Superb Message",
            EditorMetadataField.RatingSuperbImagePath => "Superb Image",
            _ => field.ToString()
        };
    }

    private IReadOnlyList<DevUiWindowRow> GetNoteOptionRows()
    {
        if (_optionsIsEffect)
            return GetEffectOptionRows();

        if (_optionsIsClip)
            return GetClipOptionRows();

        if (_optionsNote == null)
            return Array.Empty<DevUiWindowRow>();

        EditorNoteOptionsContext context = new(
            _optionsNote,
            _document,
            GetNoteOptionsWindowBounds(),
            ResolveOptionsNote,
            _optionsDefinition,
            ApplyNotePatch,
            _pendingPlacementOptions,
            ApplyPlacementOptions);
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

    private IReadOnlyList<DevUiWindowRow> GetClipOptionRows()
    {
        ChartEditorClip clip = ResolveOptionsClip();
        if (clip == null || _optionsClipDefinition == null)
            return Array.Empty<DevUiWindowRow>();

        List<DevUiWindowRow> rows = new()
        {
            DevUiWindowRow.Value("TYPE", _optionsClipDefinition.DisplayName),
            DevUiWindowRow.Value("CATEGORY", _optionsClipDefinition.Category.ToString()),
            DevUiWindowRow.Value("START", $"{clip.StartBeat:0.###}b"),
            DevUiWindowRow.Value("LENGTH", $"{clip.LengthBeats:0.###}b")
        };

        if (_optionsClipDefinition.Fields.Count == 0)
        {
            rows.Add(DevUiWindowRow.Title("No editable clip fields"));
            return rows;
        }

        rows.Add(DevUiWindowRow.Category("FIELDS"));
        foreach (EditorClipFieldDefinition field in _optionsClipDefinition.Fields)
        {
            string value = GetClipFieldValue(clip, _optionsClipDefinition, field);
            rows.Add(CreateClipFieldRow(field, value));
        }

        return rows;
    }

    private ChartEditorClip ResolveOptionsClip()
    {
        if (_optionsClip == null)
            return null;

        ChartEditorClip clip = _document.FindEditorClip(_optionsClip.Id);
        if (clip == null)
            return null;

        _optionsClip = clip;
        _optionsClipDefinition = EditorClipDefinitions.Find(clip.RhythmGameId, clip.ClipTypeId) ?? _optionsClipDefinition;
        return clip;
    }

    private DevUiWindowRow CreateClipFieldRow(EditorClipFieldDefinition field, string value)
    {
        return field.Kind switch
        {
            EditorClipFieldKind.Bool => DevUiWindowRow.Checkbox(field.DisplayName, ParseClipFieldBool(value), () => ToggleClipField(field)),
            EditorClipFieldKind.Enum when field.Options.Count > 0 => DevUiWindowRow.Dropdown(field.Key, field.DisplayName, field.Options.Select(option => option.DisplayName).ToArray(), GetClipFieldOptionIndex(field, value), index => SetClipField(field, field.Options[Math.Clamp(index, 0, field.Options.Count - 1)].Value)),
            EditorClipFieldKind.Float => DevUiWindowRow.FloatInput(field.Key, field.DisplayName, ParseClipFieldDouble(value), number => SetClipField(field, number.ToString("0.###", CultureInfo.InvariantCulture))),
            _ => DevUiWindowRow.Title(field.DisplayName)
        };
    }

    private void ToggleClipField(EditorClipFieldDefinition field)
    {
        bool current = ParseClipFieldBool(GetClipFieldValue(ResolveOptionsClip(), _optionsClipDefinition, field));
        SetClipField(field, current ? "false" : "true");
    }

    private void SetClipField(EditorClipFieldDefinition field, string value)
    {
        ChartEditorClip clip = ResolveOptionsClip();
        if (clip == null || field == null)
            return;

        Dictionary<string, string> data = CreateMergedClipData(_optionsClipDefinition, clip.Data);
        data[field.Key] = value ?? string.Empty;
        if (ExecuteCommand(new ChangeClipDataCommand(clip.Id, data)))
            _status = $"Updated {_optionsClipDefinition.DisplayName} {field.DisplayName}";
    }

    private static string GetClipFieldValue(ChartEditorClip clip, EditorClipDefinition definition, EditorClipFieldDefinition field)
    {
        Dictionary<string, string> data = CreateMergedClipData(definition, clip?.Data);
        return data.TryGetValue(field.Key, out string value) ? value : field.DefaultValue;
    }

    private static bool ParseClipFieldBool(string value)
    {
        return bool.TryParse(value, out bool parsed) && parsed
            || string.Equals(value, "1", StringComparison.OrdinalIgnoreCase);
    }

    private static double ParseClipFieldDouble(string value)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)
            ? parsed
            : 0.0;
    }

    private static int GetClipFieldOptionIndex(EditorClipFieldDefinition field, string value)
    {
        for (int i = 0; i < field.Options.Count; i++)
        {
            if (field.Options[i].Value == value)
                return i;
        }

        return 0;
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

    private bool RightPressed()
    {
        return _mouse.RightButton == ButtonState.Pressed && _previousMouse.RightButton == ButtonState.Released;
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

    private static bool PathEquals(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
            return false;

        if (string.Equals(NormalizePath(left), NormalizePath(right), StringComparison.OrdinalIgnoreCase))
            return true;

        return string.Equals(NormalizePath(GetFullPathOrOriginal(left)), NormalizePath(GetFullPathOrOriginal(right)), StringComparison.OrdinalIgnoreCase);
    }

    private static string GetFullPathOrOriginal(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException)
        {
            return path;
        }
    }

    private static string NormalizePath(string path)
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
        return DevUiTextInput.TryGetTypedChar(key, _keyboard, out c);
    }

    private void ResetCustomTextBackspaceRepeat()
    {
        _customTextBackspaceHoldStartMs = 0;
        _customTextBackspaceLastRepeatMs = 0;
    }
}
