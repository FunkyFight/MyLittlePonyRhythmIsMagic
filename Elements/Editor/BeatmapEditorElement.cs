using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MLP_RiM.Elements.DevUI;
using Rhythm.Note;
using Rhythm.Note.Evaluator;

namespace MLP_RiM.Elements.Editor;

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
    private readonly IEditorNoteOptionsPanel _intervalOptionsPanel = new IntervalEditorNoteOptionsPanel();
    private readonly string[] _metadataFields = { "BeatmapName", "Beatmapper", "ArtistName", "MusicName", "BPM", "Offset" };
    private const double ShiftSeekSongDurationRatio = 0.05;

    private BeatmapEditorDocument _document;
    private EditorRhythmInputVisualElement _rhythmVisuals;
    private KeyboardState _previousKeyboard;
    private KeyboardState _keyboard;
    private EditorNoteKind _selectedKind = EditorNoteKind.SeeSaw;
    private double _manualSongPosition;
    private double _visibleBeforeSeconds = 4;
    private double _visibleAfterSeconds = 4;
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
    private bool _optionsIsCreation;
    private bool _optionsIsIntervalCreation;
    private bool _isSelectingIntervalRange;
    private double? _intervalRangeStart;
    private double _pendingIntervalDurationBeats;
    private bool _isCreatingNewBeatmap;
    private string _newBeatmapNameBuffer = "";

    public BeatmapEditorElement(BeatmapPlayer beatmapPlayer, string songPath = "Songs/metronome.wav", string chartPath = "Beatmaps/editor_beatmap.xml", double firstBeatDelay = 0.078, double snapDivisions = 4)
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

    public void Update(GameTime gameTime)
    {
        _previousKeyboard = _keyboard;
        _keyboard = Keyboard.GetState();

        if (_isPreviewPlaying)
        {
            HandlePreviewCommands();
            return;
        }

        AdvanceEditorPlayback();

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

        HandleCommands(gameTime);
        UpdateNoteOptionsWindow();
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        if (_isPreviewPlaying)
            return;

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

        if (Pressed(Keys.Space))
            TogglePlayback();

        if (Pressed(Keys.Home))
            Seek(0);

        if (Pressed(Keys.Left))
            Seek(GetSteppedSeekPosition(-1));

        if (Pressed(Keys.Right))
            Seek(GetSteppedSeekPosition(1));

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
                Seek(ClampSongPosition(CurrentSongPosition() - gameTime.ElapsedGameTime.TotalSeconds * 4));

            if (IsDown(Keys.D))
                Seek(ClampSongPosition(CurrentSongPosition() + gameTime.ElapsedGameTime.TotalSeconds * 4));
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
            PlaceSelectedNote();

        if (Pressed(Keys.I))
            ToggleIntervalRangeSelection();

        if (Pressed(Keys.Delete) || Pressed(Keys.Back))
            DeleteNearestNote();

        if (Pressed(Keys.S) && IsControlDown())
            Save();

        if (Pressed(Keys.L) && IsControlDown())
            ReloadCurrentDocument();

        if (Pressed(Keys.R))
            RebuildPlayback(false);

        if (Pressed(Keys.Tab))
            SelectNextMetadataField();

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

        if (Pressed(Keys.OemPlus) || Pressed(Keys.Add))
            Zoom(-0.5);

        if (Pressed(Keys.OemMinus) || Pressed(Keys.Subtract))
            Zoom(0.5);

        if (Pressed(Keys.P))
            StartPreview();

    }

    private void ToggleNoteOptionsWindow()
    {
        if (_noteOptionsWindow.IsOpen)
        {
            _noteOptionsWindow.Close();
            return;
        }

        OpenNoteOptionsWindow(_document.FindNearest(CurrentSongPosition(), _document.Crotchet / _snapDivisions));
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
        _optionsIsCreation = false;
        _optionsIsIntervalCreation = false;
        _noteOptionsWindow.Open();
        _status = $"Options for {definition.DisplayName} at {_optionsNote.SongPosition:0.000}s";
        return true;
    }

    private void OpenCreateNoteWindow(EditorNoteDefinition definition, double songPosition)
    {
        _optionsNote = definition.CreateChartNote(songPosition, _document.Crotchet, variantIndex: 0);
        if (_lastCreatedNoteData.TryGetValue(definition.Kind, out Dictionary<string, string> lastData))
            _optionsNote.AdditionnalData = new Dictionary<string, string>(lastData);

        _optionsDefinition = definition;
        _optionsIsCreation = true;
        _optionsIsIntervalCreation = false;
        if (!EditorNoteDefinitions.TryGetOptionsPanel(definition.Kind, out _optionsPanel))
            _optionsPanel = null;
        _noteOptionsWindow.Open();
        _status = $"Configure {definition.DisplayName} at {songPosition:0.000}s, then Create";
    }

    private void OpenCreateIntervalWindow(EditorNoteDefinition definition, double startSongPosition, double endSongPosition)
    {
        double start = Math.Min(startSongPosition, endSongPosition);
        double end = Math.Max(startSongPosition, endSongPosition);
        _optionsNote = definition.CreateChartNote(start, _document.Crotchet, variantIndex: 0);
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
        _optionsIsCreation = true;
        _optionsIsIntervalCreation = true;
        if (!EditorNoteDefinitions.TryGetOptionsPanel(definition.Kind, out _optionsPanel))
            _optionsPanel = null;
        _noteOptionsWindow.Open();
        _status = $"Configure interval {definition.DisplayName} from {start:0.000}s to {end:0.000}s, then Create";
    }

    private void ApplyNoteOption(Action<ChartNote> apply)
    {
        ChartNote note = ResolveOptionsNote();
        if (note == null)
            return;

        apply(note);
    }

    private ChartNote ResolveOptionsNote()
    {
        if (_optionsIsCreation)
            return _optionsNote;

        ChartNote note = _optionsNote;
        if (note == null || !_document.Chart.Notes.Contains(note))
            note = _document.FindNearest(CurrentSongPosition(), _document.Crotchet / _snapDivisions);

        if (note == null)
            return null;

        _optionsNote = note;
        return note;
    }

    private void UpdateNoteOptionsWindow()
    {
        if (!_noteOptionsWindow.IsOpen)
            return;

        if (_optionsNote == null || _optionsDefinition == null || (!_optionsIsCreation && !_document.Chart.Notes.Contains(_optionsNote)))
        {
            _noteOptionsWindow.Close();
            return;
        }

        bool wasCreation = _optionsIsCreation;
        if (_noteOptionsWindow.Update(GetNoteOptionsWindowBounds(), GetNoteOptionRows()))
        {
            if (wasCreation)
                return;

            _document.MarkDirty();
            RebuildPlayback(_editorPlaybackPlaying);
            _status = $"Updated options at {_optionsNote.SongPosition:0.000}s";
        }
    }

    private void StartPreview()
    {
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
        double position = _beatmapPlayer.Conductor?.SongPosition ?? _manualSongPosition;
        _isPreviewPlaying = false;
        _editorPlaybackPlaying = false;
        RebuildPlayback(false);
        Seek(position);
        _status = $"Back to editor at {position:0.000}s";
    }

    private void Load()
    {
        LoadDocument(_defaultSongPath, _defaultChartPath);
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
        _document.SetMetadata(beatmapName: beatmapName);
        _manualSongPosition = 0;
        RefreshSongs();
        RefreshCharts();
        SyncSelectedSongIndex();
        SyncSelectedChartIndex();
        RebuildPlayback(false);
        CloseNewBeatmapModal($"New beatmap {Path.GetFileName(chartPath)} ready");
    }

    private void CloseNewBeatmapModal(string status)
    {
        _isCreatingNewBeatmap = false;
        _newBeatmapWindow.Close();
        _status = status;
    }

    private string GetAvailableNewBeatmapPath(string beatmapName)
    {
        string fileName = SanitizeFileName(beatmapName);
        if (string.IsNullOrWhiteSpace(fileName))
            fileName = "New Beatmap";

        string basePath = Path.Combine("Beatmaps", fileName + ".xml");
        if (!File.Exists(basePath))
            return basePath;

        for (int i = 2; ; i++)
        {
            string candidate = Path.Combine("Beatmaps", $"{fileName}_{i}.xml");
            if (!File.Exists(candidate))
                return candidate;
        }
    }

    private static string SanitizeFileName(string value)
    {
        char[] invalidChars = Path.GetInvalidFileNameChars();
        string sanitized = new(value.Select(c => invalidChars.Contains(c) ? '_' : c).ToArray());
        return sanitized.Trim().Trim('.');
    }

    private void RebuildPlayback(bool keepPlaying)
    {
        double position = CurrentSongPosition();
        bool shouldPlay = keepPlaying && !_isPreviewPlaying;
        _editorPlaybackPlaying = false;

        _beatmapPlayer.StartBeatmapPaused(_document.SongPath, _document.Chart, ReactionRules.RhythmHeavenLike(), new RhythmHeavenLikeReactionEvaluator());
        _rhythmVisuals = new EditorRhythmInputVisualElement(_beatmapPlayer, _pixel, _ui);
        SyncPlaybackToEditorPosition(position, resetVisuals: true);

        if (shouldPlay)
        {
            _beatmapPlayer.Conductor.Play();
            _editorPlaybackPlaying = true;
        }
    }

    private void TogglePlayback()
    {
        if (_beatmapPlayer.Conductor == null)
            return;

        if (_editorPlaybackPlaying)
        {
            _editorPlaybackPlaying = false;
            _beatmapPlayer.Conductor.Pause();
            SyncPlaybackToEditorPosition(_manualSongPosition, resetVisuals: false);
            _status = "Paused";
        }
        else
        {
            SyncPlaybackToEditorPosition(_manualSongPosition, resetVisuals: false);
            _beatmapPlayer.Conductor.Play();
            _editorPlaybackPlaying = true;
            _status = "Playing";
        }
    }

    private void PlaceSelectedNote()
    {
        EditorNoteDefinition definition = EditorNoteDefinitions.Get(_selectedKind);
        double position = Snap(CurrentSongPosition());
        OpenCreateNoteWindow(definition, position);
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
        double position = Snap(CurrentSongPosition());
        if (_intervalRangeStart == null)
        {
            _intervalRangeStart = position;
            _status = $"Interval start {position:0.000}s selected; move to end and press ENTER";
            return;
        }

        EditorNoteDefinition definition = EditorNoteDefinitions.Get(_selectedKind);
        double start = _intervalRangeStart.Value;
        _isSelectingIntervalRange = false;
        _intervalRangeStart = null;
        OpenCreateIntervalWindow(definition, start, position);
    }

    private void CreatePendingNote()
    {
        if (!_optionsIsCreation || _optionsNote == null || _optionsDefinition == null)
            return;

        Dictionary<string, string> creationData = GetNoteCreationData(_optionsNote);
        if (_optionsIsIntervalCreation)
            StoreLastIntervalData(_optionsNote);

        IReadOnlyList<EditorNotePlacement> placements = _optionsIsIntervalCreation
            ? CreateIntervalPlacements(_optionsDefinition, _optionsNote)
            : _optionsDefinition.CreatePlacements(_optionsNote, _document.Crotchet);
        if (_document.TryPlaceNotes(placements, out IReadOnlyList<ChartNote> placedNotes, out string reason))
        {
            _lastCreatedNoteData[_optionsDefinition.Kind] = creationData;
            _optionsIsCreation = false;
            _optionsIsIntervalCreation = false;
            RebuildPlayback(_editorPlaybackPlaying);

            if (placedNotes.Count == 1 && EditorNoteDefinitions.FromChartNote(placedNotes[0]) is EditorNoteDefinition placedDefinition && EditorNoteDefinitions.TryGetOptionsPanel(placedDefinition.Kind, out _))
                OpenNoteOptionsWindow(placedNotes[0]);
            else
                _noteOptionsWindow.Close();

            ChartNote firstNote = placedNotes[0];
            ChartNote lastNote = placedNotes[placedNotes.Count - 1];
            string creationName = GetSelectedNoteName(_optionsDefinition);
            _status = placedNotes.Count == 1
                ? $"Created {GetSelectedNoteName(_optionsDefinition)} at {firstNote.SongPosition:0.000}s"
                : $"Created interval {creationName}: {placedNotes.Count} notes from {firstNote.SongPosition:0.000}s to {lastNote.SongPosition:0.000}s";
        }
        else
        {
            _status = reason;
        }
    }

    private IReadOnlyList<EditorNotePlacement> CreateIntervalPlacements(EditorNoteDefinition definition, ChartNote sourceNote)
    {
        double crotchet = _document.Crotchet;
        if (crotchet <= 0)
            return Array.Empty<EditorNotePlacement>();

        double start = Math.Max(0, sourceNote.SongPosition);
        double durationBeats = Math.Max(0, IntervalEditorNoteProvider.GetDurationBeats(sourceNote.AdditionnalData));
        double stepBeats = IntervalEditorNoteProvider.GetStepBeats(sourceNote.AdditionnalData);
        double end = start + durationBeats * crotchet;
        double stepSeconds = stepBeats * crotchet;

        List<EditorNotePlacement> placements = new();
        for (double time = start; time <= end + 0.000001; time += stepSeconds)
        {
            ChartNote note = CloneNoteForInterval(sourceNote, time);
            placements.Add(new EditorNotePlacement(definition, note));
        }

        return placements;
    }

    private static ChartNote CloneNoteForInterval(ChartNote sourceNote, double songPosition)
    {
        return new ChartNote
        {
            SongPosition = Math.Max(0, songPosition),
            HoldDuration = sourceNote.HoldDuration,
            InputActionToPress = sourceNote.InputActionToPress,
            AdditionnalData = GetNoteCreationData(sourceNote)
        };
    }

    private static Dictionary<string, string> GetNoteCreationData(ChartNote note)
    {
        Dictionary<string, string> data = new(note.AdditionnalData ?? new Dictionary<string, string>());
        data.Remove(IntervalEditorNoteProvider.DurationBeatsKey);
        data.Remove(IntervalEditorNoteProvider.StepBeatsKey);
        return data;
    }

    private void StoreLastIntervalData(ChartNote note)
    {
        _lastIntervalData[IntervalEditorNoteProvider.DurationBeatsKey] = IntervalEditorNoteProvider.GetDurationBeats(note.AdditionnalData).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        _lastIntervalData[IntervalEditorNoteProvider.StepBeatsKey] = IntervalEditorNoteProvider.GetStepBeats(note.AdditionnalData).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
    }

    private double GetBeatsBetween(double startSongPosition, double endSongPosition)
    {
        return _document.Crotchet > 0 ? Math.Abs(endSongPosition - startSongPosition) / _document.Crotchet : 0;
    }

    private void DeleteNearestNote()
    {
        double position = CurrentSongPosition();
        if (_document.DeleteNearest(position, _document.Crotchet / _snapDivisions, out ChartNote deletedNote))
        {
            RebuildPlayback(_editorPlaybackPlaying);
            _status = $"Deleted note at {deletedNote.SongPosition:0.000}s";
        }
        else
        {
            _status = "No note close enough to delete";
        }
    }

    private void Seek(double songPosition, bool updateStatus = true)
    {
        SyncPlaybackToEditorPosition(songPosition, resetVisuals: true);

        if (_editorPlaybackPlaying)
            _beatmapPlayer.Conductor?.Play();

        if (updateStatus)
            _status = $"Seek {_manualSongPosition:0.000}s";
    }

    private void AdvanceEditorPlayback()
    {
        if (!_editorPlaybackPlaying)
            return;

        _beatmapPlayer.Conductor?.Update();
        _manualSongPosition = Math.Max(0, _beatmapPlayer.Conductor?.SongPosition ?? _manualSongPosition);
        _beatmapPlayer.ChartPlayer?.Update(_manualSongPosition);
        _beatmapPlayer.VisualNoteMng?.Update(_manualSongPosition);
    }

    private void SyncPlaybackToEditorPosition(double songPosition, bool resetVisuals)
    {
        double targetPosition = Math.Max(0, songPosition);
        _beatmapPlayer.Conductor?.Seek(targetPosition);
        _manualSongPosition = Math.Max(0, _beatmapPlayer.Conductor?.SongPosition ?? targetPosition);
        _beatmapPlayer.ChartPlayer?.Seek(_manualSongPosition);

        if (resetVisuals)
            _beatmapPlayer.VisualNoteMng?.Reset();

        _beatmapPlayer.VisualNoteMng?.Update(_manualSongPosition);
    }

    private void Select(EditorNoteKind kind)
    {
        _selectedKind = kind;
        _status = $"Selected {EditorNoteDefinitions.Get(kind).DisplayName}";
    }

    private void SelectRelative(int delta)
    {
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

        if (_availableSongs.Count == 0)
            _availableSongs.Add(_defaultSongPath);

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
        _status = $"Chart {Path.GetFileName(chartPath)}";
    }

    private void SelectSongRelative(int delta)
    {
        if (_availableSongs.Count == 0)
            RefreshSongs();

        _selectedSongIndex = PositiveModulo(_selectedSongIndex + delta, _availableSongs.Count);
        string songPath = _availableSongs[_selectedSongIndex];
        _document.SetSongPath(songPath);
        RebuildPlayback(false);
        _status = $"Song {Path.GetFileName(songPath)}";
    }

    private void SelectNextMetadataField()
    {
        _selectedMetadataField = PositiveModulo(_selectedMetadataField + 1, _metadataFields.Length);
        _status = $"Metadata field {_metadataFields[_selectedMetadataField]}";
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
        if (field == "BPM" && double.TryParse(_textBuffer, out double bpm))
        {
            _document.SetBpm(bpm);
            _beatmapPlayer.Conductor?.SetBpm(bpm);
            RebuildPlayback(_editorPlaybackPlaying);
        }
        else if (field == "Offset" && double.TryParse(_textBuffer, out double offset))
        {
            _document.SetOffset(offset);
            RebuildPlayback(_editorPlaybackPlaying);
        }
        else if (field == "BeatmapName")
            _document.SetMetadata(beatmapName: _textBuffer);
        else if (field == "Beatmapper")
            _document.SetMetadata(beatmapper: _textBuffer);
        else if (field == "ArtistName")
            _document.SetMetadata(artistName: _textBuffer);
        else if (field == "MusicName")
            _document.SetMetadata(musicName: _textBuffer);

        _isEditingText = false;
        _status = $"Updated {field}";
    }

    private void Zoom(double delta)
    {
        _visibleBeforeSeconds = Math.Clamp(_visibleBeforeSeconds + delta, 1, 16);
        _visibleAfterSeconds = Math.Clamp(_visibleAfterSeconds + delta, 1, 16);
        _status = $"Timeline window {(_visibleBeforeSeconds + _visibleAfterSeconds):0.0}s";
    }

    private double CurrentSongPosition()
    {
        return _manualSongPosition;
    }

    private double GetSteppedSeekPosition(int direction)
    {
        double step = IsShiftDown()
            ? GetShiftSeekStep()
            : _document.Crotchet / _snapDivisions;

        double position = CurrentSongPosition() + direction * step;
        return IsShiftDown() ? ClampSongPosition(position) : Snap(position);
    }

    private double GetShiftSeekStep()
    {
        double songDuration = GetSongDurationSeconds();
        return songDuration > 0 ? songDuration * ShiftSeekSongDurationRatio : _document.Crotchet;
    }

    private double ClampSongPosition(double songPosition)
    {
        songPosition = Math.Max(0, songPosition);

        double songDuration = GetSongDurationSeconds();
        return songDuration > 0 ? Math.Min(songPosition, songDuration) : songPosition;
    }

    private double GetSongDurationSeconds()
    {
        return Math.Max(0, _beatmapPlayer.Conductor?.Duration ?? 0);
    }

    private double Snap(double songPosition)
    {
        double step = _document.Crotchet / _snapDivisions;
        return Math.Max(0, _document.Chart.Offset + Math.Round((songPosition - _document.Chart.Offset) / step) * step);
    }

    private void DrawTimeline(SpriteBatch spriteBatch)
    {
        Viewport viewport = GLOBALS.graphicsDevice.Viewport;
        Rectangle area = new(60, 40, viewport.Width - 120, 160);
        double current = CurrentSongPosition();
        double start = current - _visibleBeforeSeconds;
        double end = current + _visibleAfterSeconds;

        _ui.Fill(spriteBatch, area, new Color(12, 14, 20, 220));
        _ui.Stroke(spriteBatch, area, Color.White, 2);

        if (_document.Chart.Offset >= start && _document.Chart.Offset <= end)
        {
            float firstBeatX = TimeToX(_document.Chart.Offset, start, end, area);
            _ui.Line(spriteBatch, new Vector2(firstBeatX, area.Y - 6), new Vector2(firstBeatX, area.Bottom + 6), Color.Yellow, 4);
        }

        double firstBeat = Math.Floor((start - _document.Chart.Offset) / _document.Crotchet) * _document.Crotchet + _document.Chart.Offset;
        for (double beat = firstBeat; beat <= end; beat += _document.Crotchet)
        {
            float x = TimeToX(beat, start, end, area);
            if (x < area.X || x > area.Right)
                continue;

            _ui.Line(spriteBatch, new Vector2(x, area.Y), new Vector2(x, area.Bottom), Color.DimGray, 2);
        }

        double snapStep = _document.Crotchet / _snapDivisions;
        double firstSnap = Math.Floor((start - _document.Chart.Offset) / snapStep) * snapStep + _document.Chart.Offset;
        for (double snap = firstSnap; snap <= end; snap += snapStep)
        {
            float x = TimeToX(snap, start, end, area);
            if (x < area.X || x > area.Right)
                continue;

            _ui.Line(spriteBatch, new Vector2(x, area.Y + 110), new Vector2(x, area.Bottom), Color.DarkSlateGray, 1);
        }

        if (_isSelectingIntervalRange && _intervalRangeStart is double intervalStart)
        {
            float intervalStartX = TimeToX(intervalStart, start, end, area);
            float intervalEndX = TimeToX(Snap(current), start, end, area);
            _ui.Line(spriteBatch, new Vector2(intervalStartX, area.Y + 18), new Vector2(intervalEndX, area.Y + 18), Color.LightGreen, 3);
            _ui.Fill(spriteBatch, new Rectangle((int)intervalStartX - 4, area.Y + 10, 8, 16), Color.LightGreen);
            _ui.Fill(spriteBatch, new Rectangle((int)intervalEndX - 4, area.Y + 10, 8, 16), Color.LightGreen * 0.7f);
        }

        foreach (ChartNote note in _document.GetNotesInWindow(start, end))
        {
            EditorNoteDefinition definition = EditorNoteDefinitions.FromChartNote(note);
            if (definition == null)
                continue;

            float noteX = TimeToX(note.SongPosition, start, end, area);
            float occupyStartX = TimeToX(_document.GetContextualStart(note, definition), start, end, area);
            float occupyEndX = TimeToX(_document.GetContextualEnd(note, definition), start, end, area);
            float hitStartX = TimeToX(_document.GetContextualHitWindowStart(note, definition), start, end, area);
            float hitEndX = TimeToX(_document.GetContextualHitWindowEnd(note, definition), start, end, area);
            int variantIndex = EditorNoteDefinitions.FindVariantIndex(definition, note);
            Color color = GetNoteColor(definition.Kind, variantIndex);

            int occupyX = (int)Math.Clamp(occupyStartX, area.X, area.Right);
            int occupyRight = (int)Math.Clamp(occupyEndX, area.X, area.Right);
            int hitX = (int)Math.Clamp(hitStartX, area.X, area.Right);
            int hitRight = (int)Math.Clamp(hitEndX, area.X, area.Right);
            _ui.Fill(spriteBatch, new Rectangle(occupyX, area.Y + 96, Math.Max(2, occupyRight - occupyX), 18), color * 0.35f);
            _ui.Fill(spriteBatch, new Rectangle(hitX, area.Y + 118, Math.Max(2, hitRight - hitX), 10), Color.Red * 0.5f);
            _ui.Fill(spriteBatch, new Rectangle((int)noteX - 5, area.Y + 58, 10, 68), color);
        }

        DrawIntervalPreview(spriteBatch, start, end, area);

        float playheadX = TimeToX(current, start, end, area);
        _ui.Line(spriteBatch, new Vector2(playheadX, area.Y - 10), new Vector2(playheadX, area.Bottom + 10), Color.Red, 3);
    }

    private void DrawIntervalPreview(SpriteBatch spriteBatch, double windowStart, double windowEnd, Rectangle area)
    {
        if (!_optionsIsCreation || !_optionsIsIntervalCreation || _optionsNote == null || _optionsDefinition == null)
            return;

        IReadOnlyList<EditorNotePlacement> placements = CreateIntervalPlacements(_optionsDefinition, _optionsNote);
        foreach (EditorNotePlacement placement in placements)
        {
            ChartNote note = placement.Note;
            if (note.SongPosition < windowStart || note.SongPosition > windowEnd)
                continue;

            int variantIndex = EditorNoteDefinitions.FindVariantIndex(placement.Definition, note);
            Color color = GetNoteColor(placement.Definition.Kind, variantIndex);
            float noteX = TimeToX(note.SongPosition, windowStart, windowEnd, area);

            _ui.Line(spriteBatch, new Vector2(noteX, area.Y + 30), new Vector2(noteX, area.Bottom - 18), Color.White * 0.85f, 2);
            _ui.Stroke(spriteBatch, new Rectangle((int)noteX - 7, area.Y + 46, 14, 84), color, 2);
            _ui.Fill(spriteBatch, new Rectangle((int)noteX - 4, area.Y + 75, 8, 22), color * 0.7f);
        }
    }

    private void DrawHud(SpriteBatch spriteBatch)
    {
        Viewport viewport = GLOBALS.graphicsDevice.Viewport;
        double current = CurrentSongPosition();
        EditorNoteDefinition selected = EditorNoteDefinitions.Get(_selectedKind);
        string dirty = _document.IsDirty ? "DIRTY" : "SAVED";
        string playing = _editorPlaybackPlaying ? "PLAY" : "PAUSE";
        string field = _metadataFields[_selectedMetadataField];
        string editLine = _isEditingText ? $"EDIT {field}: {_textBuffer}|" : $"META <TAB/F2> {field}: {GetMetadataValue(field)}";
        string songName = _availableSongs.Count > 0 ? Path.GetFileName(_availableSongs[_selectedSongIndex]) : Path.GetFileName(_document.SongPath);
        string chartName = _availableCharts.Count > 0 ? Path.GetFileName(_availableCharts[_selectedChartIndex]) : Path.GetFileName(_document.ChartPath);

        _ui.Fill(spriteBatch, new Rectangle(20, viewport.Height - 146, viewport.Width - 40, 126), new Color(0, 0, 0, 180));
        _ui.Label(spriteBatch, $"{playing} T:{current:0.000}s BEAT:{((current - _document.Chart.Offset) / _document.Crotchet):0.00} BPM:{_document.Chart.BPM:0.##} OFFSET:{_document.Chart.Offset:0.000} NOTES:{_document.Chart.Notes.Count} {dirty}", new Vector2(34, viewport.Height - 132), Color.White, 2);
        _ui.Label(spriteBatch, $"NOTE <UP/DOWN> {selected.DisplayName}  SONG <PGUP/PGDN> {songName}", new Vector2(34, viewport.Height - 108), Color.LightGreen, 2);
        _ui.Label(spriteBatch, $"CHART <CTRL+PGUP/PGDN> {chartName}", new Vector2(34, viewport.Height - 96), Color.LightGreen, 2);
        _ui.Label(spriteBatch, editLine, new Vector2(34, viewport.Height - 84), _isEditingText ? Color.Yellow : Color.LightBlue, 2);
        _ui.Label(spriteBatch, $"STATUS: {_status}", new Vector2(34, viewport.Height - 60), Color.LightGray, 2);
        _ui.Label(spriteBatch, "SPACE PLAY/PAUSE LEFT/RIGHT SEEK Q/D SCROLL ENTER PLACE I INTERVAL RANGE DEL DELETE F3 OPTIONS CTRL+N NEW CTRL+S SAVE CTRL+L LOAD P PREVIEW ESC STOP +/- ZOOM", new Vector2(34, viewport.Height - 36), Color.LightGray, 2);
        _noteOptionsWindow.Draw(spriteBatch, GetNoteOptionsWindowBounds(), GetNoteOptionsTitle(), GetNoteOptionRows());
        _newBeatmapWindow.Draw(spriteBatch, GetNewBeatmapWindowBounds(), "NEW BEATMAP", GetNewBeatmapRows());
    }

    private string GetNoteOptionsTitle()
    {
        if (_optionsIsIntervalCreation)
            return $"INTERVAL {_optionsDefinition?.DisplayName?.ToUpperInvariant() ?? "NOTE"}";

        return _optionsPanel?.Title ?? "NOTE OPTIONS";
    }

    private float TimeToX(double songPosition, double start, double end, Rectangle area)
    {
        double t = (songPosition - start) / (end - start);
        return area.X + (float)(t * area.Width);
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

    private string GetSelectedNoteName(EditorNoteDefinition definition)
    {
        return definition.DisplayName;
    }

    private Rectangle GetNoteOptionsWindowBounds()
    {
        Viewport viewport = GLOBALS.graphicsDevice.Viewport;
        return new Rectangle(viewport.Width - 390, 210, 350, 230);
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

    private DevUiWindowRow WrapNoteOptionRow(DevUiWindowRow row)
    {
        return row.Kind switch
        {
            DevUiWindowRowKind.Checkbox => DevUiWindowRow.Checkbox(row.Text, row.IsChecked, () => ApplyNoteOption(_ => row.Toggle?.Invoke())),
            DevUiWindowRowKind.Dropdown => DevUiWindowRow.Dropdown(row.Key, row.Text, row.Options, row.SelectedIndex, index => ApplyNoteOption(_ => row.Select?.Invoke(index))),
            DevUiWindowRowKind.FloatInput => DevUiWindowRow.FloatInput(row.Key, row.Text, row.FloatValue, value => ApplyNoteOption(_ => row.SetFloat?.Invoke(value))),
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
