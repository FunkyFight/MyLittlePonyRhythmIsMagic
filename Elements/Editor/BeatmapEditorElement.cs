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
    private readonly Texture2D _pixel;
    private readonly string _defaultSongPath;
    private readonly string _defaultChartPath;
    private readonly double _snapDivisions;
    private readonly List<string> _availableSongs = new();
    private readonly string[] _metadataFields = { "BeatmapName", "Beatmapper", "ArtistName", "MusicName", "BPM", "Offset" };

    private BeatmapEditorDocument _document;
    private EditorRhythmInputVisualElement _rhythmVisuals;
    private KeyboardState _previousKeyboard;
    private KeyboardState _keyboard;
    private EditorNoteKind _selectedKind = EditorNoteKind.SeeSawTowardInner;
    private double _manualSongPosition;
    private double _visibleBeforeSeconds = 4;
    private double _visibleAfterSeconds = 4;
    private string _status = "Editor ready";
    private int _selectedSongIndex;
    private int _selectedMetadataField;
    private bool _isEditingText;
    private string _textBuffer = "";

    public BeatmapEditorElement(BeatmapPlayer beatmapPlayer, string songPath = "Songs/metronome.wav", string chartPath = "Beatmaps/editor_beatmap.xml", double firstBeatDelay = 0.078, double snapDivisions = 4)
    {
        _beatmapPlayer = beatmapPlayer;
        _defaultSongPath = songPath;
        _defaultChartPath = chartPath;
        _snapDivisions = snapDivisions;
        _ui = new DevUiRenderer(GLOBALS.graphicsDevice);
        _pixel = new Texture2D(GLOBALS.graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });

        Load();
    }

    public void Update(GameTime gameTime)
    {
        _previousKeyboard = _keyboard;
        _keyboard = Keyboard.GetState();

        HandleCommands(gameTime);

        if (_beatmapPlayer.Conductor != null && _beatmapPlayer.Conductor.isPlaying())
        {
            _manualSongPosition = _beatmapPlayer.Conductor.SongPosition;
        }
        else if (_beatmapPlayer.ChartPlayer != null)
        {
            _beatmapPlayer.ChartPlayer.Update(_manualSongPosition);
            _beatmapPlayer.VisualNoteMng?.Update(_manualSongPosition);
        }
    }

    public void Draw(SpriteBatch spriteBatch)
    {
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

        double seekStep = IsDown(Keys.LeftShift) || IsDown(Keys.RightShift) ? _document.Crotchet : _document.Crotchet / _snapDivisions;

        if (Pressed(Keys.Space))
            TogglePlayback();

        if (Pressed(Keys.Home))
            Seek(0);

        if (Pressed(Keys.Left))
            Seek(Snap(CurrentSongPosition() - seekStep));

        if (Pressed(Keys.Right))
            Seek(Snap(CurrentSongPosition() + seekStep));

        if (IsDown(Keys.Q))
            Seek(CurrentSongPosition() - gameTime.ElapsedGameTime.TotalSeconds * 4);

        if (IsDown(Keys.D))
            Seek(CurrentSongPosition() + gameTime.ElapsedGameTime.TotalSeconds * 4);

        if (Pressed(Keys.Up))
            SelectRelative(-1);

        if (Pressed(Keys.Down))
            SelectRelative(1);

        if (Pressed(Keys.Enter) || Pressed(Keys.Insert))
            PlaceSelectedNote();

        if (Pressed(Keys.Delete) || Pressed(Keys.Back))
            DeleteNearestNote();

        if (Pressed(Keys.S) && IsControlDown())
            Save();

        if (Pressed(Keys.L) && IsControlDown())
            Load();

        if (Pressed(Keys.R))
            RebuildPlayback(false);

        if (Pressed(Keys.Tab))
            SelectNextMetadataField();

        if (Pressed(Keys.F2))
            BeginMetadataEdit();

        if (Pressed(Keys.PageUp))
            SelectSongRelative(-1);

        if (Pressed(Keys.PageDown))
            SelectSongRelative(1);

        if (Pressed(Keys.F5))
            RefreshSongs();

        if (Pressed(Keys.OemPlus) || Pressed(Keys.Add))
            Zoom(-0.5);

        if (Pressed(Keys.OemMinus) || Pressed(Keys.Subtract))
            Zoom(0.5);
    }

    private void Load()
    {
        RefreshSongs();
        _document = BeatmapEditorDocument.LoadOrCreate(_defaultSongPath, _defaultChartPath, 100);
        SyncSelectedSongIndex();
        RebuildPlayback(false);
        _status = $"Loaded {_document.ChartPath}";
    }

    private void Save()
    {
        _document.Save();
        _status = $"Saved {_document.Chart.Notes.Count} notes";
    }

    private void RebuildPlayback(bool keepPlaying)
    {
        double position = CurrentSongPosition();
        bool shouldPlay = keepPlaying && _beatmapPlayer.Conductor != null && _beatmapPlayer.Conductor.isPlaying();

        _beatmapPlayer.StartBeatmapPaused(_document.SongPath, _document.Chart, ReactionRules.RhythmHeavenLike(), new RhythmHeavenLikeReactionEvaluator());
        _beatmapPlayer.Conductor.Seek(position);
        _manualSongPosition = position;
        _rhythmVisuals = new EditorRhythmInputVisualElement(_beatmapPlayer, _pixel, _ui);
        Seek(position, updateStatus: false);

        if (shouldPlay)
            _beatmapPlayer.Conductor.Play();
    }

    private void TogglePlayback()
    {
        if (_beatmapPlayer.Conductor == null)
            return;

        if (_beatmapPlayer.Conductor.isPlaying())
        {
            _beatmapPlayer.Conductor.Pause();
            _manualSongPosition = _beatmapPlayer.Conductor.SongPosition;
            _status = "Paused";
        }
        else
        {
            Seek(_manualSongPosition, updateStatus: false);
            _beatmapPlayer.Conductor.Play();
            _status = "Playing";
        }
    }

    private void PlaceSelectedNote()
    {
        EditorNoteDefinition definition = EditorNoteDefinitions.Get(_selectedKind);
        double position = Snap(CurrentSongPosition());

        if (_document.TryPlaceNote(definition, position, out _, out string reason))
        {
            RebuildPlayback(_beatmapPlayer.Conductor?.isPlaying() == true);
            _status = $"Placed {definition.DisplayName} at {position:0.000}s";
        }
        else
        {
            _status = reason;
        }
    }

    private void DeleteNearestNote()
    {
        double position = CurrentSongPosition();
        if (_document.DeleteNearest(position, _document.Crotchet / _snapDivisions, out ChartNote deletedNote))
        {
            RebuildPlayback(_beatmapPlayer.Conductor?.isPlaying() == true);
            _status = $"Deleted note at {deletedNote.SongPosition:0.000}s";
        }
        else
        {
            _status = "No note close enough to delete";
        }
    }

    private void Seek(double songPosition, bool updateStatus = true)
    {
        _manualSongPosition = Math.Max(0, songPosition);
        if (_beatmapPlayer.Conductor != null)
            _beatmapPlayer.Conductor.Seek(_manualSongPosition);

        _beatmapPlayer.ChartPlayer?.Seek(_manualSongPosition);
        _beatmapPlayer.VisualNoteMng?.Reset();
        _beatmapPlayer.VisualNoteMng?.Update(_manualSongPosition);

        if (updateStatus)
            _status = $"Seek {_manualSongPosition:0.000}s";
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
            RebuildPlayback(_beatmapPlayer.Conductor?.isPlaying() == true);
        }
        else if (field == "Offset" && double.TryParse(_textBuffer, out double offset))
        {
            _document.SetOffset(offset);
            RebuildPlayback(_beatmapPlayer.Conductor?.isPlaying() == true);
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
        if (_beatmapPlayer.Conductor != null && _beatmapPlayer.Conductor.isPlaying())
            return _beatmapPlayer.Conductor.SongPosition;

        return _manualSongPosition;
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

        foreach (ChartNote note in _document.GetNotesInWindow(start, end))
        {
            EditorNoteDefinition definition = EditorNoteDefinitions.FromChartNote(note);
            float noteX = TimeToX(note.SongPosition, start, end, area);
            float occupyStartX = TimeToX(definition.GetStart(note.SongPosition, _document.Crotchet), start, end, area);
            float occupyEndX = TimeToX(definition.GetEnd(note.SongPosition, _document.Crotchet), start, end, area);
            float hitStartX = TimeToX(definition.GetHitWindowStart(note.SongPosition, _document.Crotchet), start, end, area);
            float hitEndX = TimeToX(definition.GetHitWindowEnd(note.SongPosition, _document.Crotchet), start, end, area);
            Color color = GetNoteColor(definition.Kind);

            int occupyX = (int)Math.Clamp(occupyStartX, area.X, area.Right);
            int occupyRight = (int)Math.Clamp(occupyEndX, area.X, area.Right);
            int hitX = (int)Math.Clamp(hitStartX, area.X, area.Right);
            int hitRight = (int)Math.Clamp(hitEndX, area.X, area.Right);
            _ui.Fill(spriteBatch, new Rectangle(occupyX, area.Y + 96, Math.Max(2, occupyRight - occupyX), 18), color * 0.35f);
            _ui.Fill(spriteBatch, new Rectangle(hitX, area.Y + 118, Math.Max(2, hitRight - hitX), 10), Color.Red * 0.5f);
            _ui.Fill(spriteBatch, new Rectangle((int)noteX - 5, area.Y + 58, 10, 68), color);
        }

        float playheadX = TimeToX(current, start, end, area);
        _ui.Line(spriteBatch, new Vector2(playheadX, area.Y - 10), new Vector2(playheadX, area.Bottom + 10), Color.Red, 3);
    }

    private void DrawHud(SpriteBatch spriteBatch)
    {
        Viewport viewport = GLOBALS.graphicsDevice.Viewport;
        double current = CurrentSongPosition();
        EditorNoteDefinition selected = EditorNoteDefinitions.Get(_selectedKind);
        string dirty = _document.IsDirty ? "DIRTY" : "SAVED";
        string playing = _beatmapPlayer.Conductor?.isPlaying() == true ? "PLAY" : "PAUSE";
        string field = _metadataFields[_selectedMetadataField];
        string editLine = _isEditingText ? $"EDIT {field}: {_textBuffer}|" : $"META <TAB/F2> {field}: {GetMetadataValue(field)}";
        string songName = _availableSongs.Count > 0 ? Path.GetFileName(_availableSongs[_selectedSongIndex]) : Path.GetFileName(_document.SongPath);

        _ui.Fill(spriteBatch, new Rectangle(20, viewport.Height - 146, viewport.Width - 40, 126), new Color(0, 0, 0, 180));
        _ui.Label(spriteBatch, $"{playing} T:{current:0.000}s BEAT:{((current - _document.Chart.Offset) / _document.Crotchet):0.00} BPM:{_document.Chart.BPM:0.##} OFFSET:{_document.Chart.Offset:0.000} NOTES:{_document.Chart.Notes.Count} {dirty}", new Vector2(34, viewport.Height - 132), Color.White, 2);
        _ui.Label(spriteBatch, $"NOTE <UP/DOWN> {selected.DisplayName}  SONG <PGUP/PGDN> {songName}", new Vector2(34, viewport.Height - 108), Color.LightGreen, 2);
        _ui.Label(spriteBatch, editLine, new Vector2(34, viewport.Height - 84), _isEditingText ? Color.Yellow : Color.LightBlue, 2);
        _ui.Label(spriteBatch, $"STATUS: {_status}", new Vector2(34, viewport.Height - 60), Color.LightGray, 2);
        _ui.Label(spriteBatch, "SPACE PLAY/PAUSE LEFT/RIGHT SEEK Q/D SCROLL ENTER PLACE DEL DELETE CTRL+S SAVE CTRL+L LOAD +/- ZOOM", new Vector2(34, viewport.Height - 36), Color.LightGray, 2);
    }

    private float TimeToX(double songPosition, double start, double end, Rectangle area)
    {
        double t = (songPosition - start) / (end - start);
        return area.X + (float)(t * area.Width);
    }

    private Color GetNoteColor(EditorNoteKind kind)
    {
        return kind switch
        {
            EditorNoteKind.SeeSawTowardOuter => Color.Orange,
            EditorNoteKind.SeeSawTowardInner => Color.MediumPurple,
            _ => Color.DeepSkyBlue
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
