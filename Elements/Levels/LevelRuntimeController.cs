using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using GameCore.Inputs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MLP_RiM.Elements.DevUI;
using MLP_RiM.Elements.Editor;
using Rhythm.Note;
using Rhythm.Note.Evaluator;

namespace MLP_RiM.Elements.Levels;

public sealed class LevelRuntimeController
{
    private const string MenuSelectAction = "MenuSelect";
    private const string ReactMainAction = "ReactMain";

    private readonly LevelDocument _document;
    private readonly BeatmapPlayer _beatmapPlayer;
    private readonly SpriteFont _dialogueFont;
    private readonly DevUiRenderer _ui;
    private readonly Action _returnToMenu;
    private readonly Action _ensureRhythmScene;
    private readonly Action<string> _switchRhythmGameScene;
    private readonly Dictionary<string, int> _trainingSuccessCounts = new();

    private RuntimeState _state = RuntimeState.Idle;
    private LevelNodeData _currentNode;
    private string _errorMessage = string.Empty;
    private bool _beatmapCompletionHandled;
    private double _trainingLoopDuration;
    private double _trainingCurrentLoopEndSongPosition;
    private double _trainingCurrentLoopEvaluationSongPosition;
    private double _trainingLoopEvaluationDelaySeconds;
    private bool _trainingSuccessExitPending;
    private double _trainingSuccessExitSongPosition;
    private IReadOnlyList<Note> _trainingCurrentLoopNotes = Array.Empty<Note>();
    private IReadOnlyList<Note> _trainingNextLoopNotes = Array.Empty<Note>();
    private KeyboardState _keyboard;
    private KeyboardState _previousKeyboard;

    public LevelRuntimeController(
        LevelDocument document,
        BeatmapPlayer beatmapPlayer,
        SpriteFont dialogueFont,
        GraphicsDevice graphicsDevice,
        Action returnToMenu,
        Action ensureRhythmScene,
        Action<string> switchRhythmGameScene)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _beatmapPlayer = beatmapPlayer ?? throw new ArgumentNullException(nameof(beatmapPlayer));
        _dialogueFont = dialogueFont;
        _ui = new DevUiRenderer(graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice)));
        _returnToMenu = returnToMenu ?? throw new ArgumentNullException(nameof(returnToMenu));
        _ensureRhythmScene = ensureRhythmScene;
        _switchRhythmGameScene = switchRhythmGameScene;

        Start();
    }

    public bool IsBeatmapActive => _state == RuntimeState.TrainingBeatmap || _state == RuntimeState.PlayRepresentationBeatmap;
    public bool ShouldUpdateRhythmScene => IsBeatmapActive || _beatmapPlayer.IsContinuingEmptyBeatmap;
    public bool AcceptsRhythmInput => IsBeatmapActive;

    public void Update(GameTime gameTime, InputActionManager inputActionManager)
    {
        _previousKeyboard = _keyboard;
        _keyboard = Keyboard.GetState();

        if (_state == RuntimeState.Dialogue)
        {
            if (inputActionManager.IsPressedOnce(MenuSelectAction) || inputActionManager.IsPressedOnce(ReactMainAction))
                FollowOutput("Next");
            return;
        }

        if (_state == RuntimeState.TrainingBeatmap || _state == RuntimeState.PlayRepresentationBeatmap)
        {
            if (_state == RuntimeState.TrainingBeatmap)
            {
                UpdateTrainingLoopProgress();
                return;
            }

            if (!_beatmapCompletionHandled && _beatmapPlayer.MusicPlaybackFinished)
            {
                _beatmapCompletionHandled = true;
                HandleBeatmapFinished();
            }

            return;
        }

        if (_state == RuntimeState.FailedGraph && (Pressed(Keys.Escape) || Pressed(Keys.Back) || inputActionManager.IsPressedOnce(MenuSelectAction)))
            _returnToMenu();
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        if (_state == RuntimeState.Dialogue && _currentNode != null)
        {
            DrawDialogue(spriteBatch, _currentNode);
            return;
        }

        if (_state == RuntimeState.TrainingBeatmap && _currentNode != null)
        {
            DrawTrainingInputWidget(spriteBatch);
            if (!_trainingSuccessExitPending)
                DrawTrainingOverlay(spriteBatch, _currentNode);
            return;
        }

        if (_state == RuntimeState.FailedGraph)
            DrawError(spriteBatch);
    }

    private void Start()
    {
        if (_document.Level == null)
        {
            Fail("Level data is missing.");
            return;
        }

        LevelNodeData startNode = _document.FindNode(_document.Level.StartNodeId);
        if (startNode == null)
        {
            Fail("Start node is missing.");
            return;
        }

        _currentNode = startNode;
        FollowOutput("Next");
    }

    private void EnterNode(LevelNodeData node)
    {
        _currentNode = node;
        _beatmapCompletionHandled = false;

        switch (node.Kind)
        {
            case LevelNodeKind.Start:
                FollowOutput("Next");
                break;

            case LevelNodeKind.Dialogue:
                if (!_beatmapPlayer.IsContinuingEmptyBeatmap)
                    _beatmapPlayer.StopBeatmap();
                ClearTrainingInputWidget();
                _state = RuntimeState.Dialogue;
                break;

            case LevelNodeKind.TrainingBeatmap:
                StartBeatmapNode(node, RuntimeState.TrainingBeatmap);
                break;

            case LevelNodeKind.PlayRepresentationBeatmap:
                StartBeatmapNode(node, RuntimeState.PlayRepresentationBeatmap);
                break;

            case LevelNodeKind.SetMiniGame:
                SetMiniGame(node);
                break;

            case LevelNodeKind.End:
                CompleteLevel();
                break;

            default:
                Fail($"Unsupported node kind: {node.Kind}");
                break;
        }
    }

    private void FollowOutput(string port)
    {
        if (_currentNode == null)
        {
            Fail("No current node.");
            return;
        }

        string targetNodeId = _document.GetConnectionTarget(_currentNode.Id, port);
        if (string.IsNullOrWhiteSpace(targetNodeId))
        {
            Fail($"Missing {port} connection from {_currentNode.Kind}.");
            return;
        }

        LevelNodeData targetNode = _document.FindNode(targetNodeId);
        if (targetNode == null)
        {
            Fail($"Connection target not found: {targetNodeId}");
            return;
        }

        EnterNode(targetNode);
    }

    private void StartBeatmapNode(LevelNodeData node, RuntimeState state)
    {
        if (!TryLoadBeatmap(node.ChartPath, out Chart chart, out string songPath, out string error))
        {
            Fail(error);
            return;
        }

        _state = state;
        _beatmapCompletionHandled = false;
        _trainingSuccessExitPending = false;
        _trainingSuccessExitSongPosition = 0.0;
        _ensureRhythmScene?.Invoke();
        bool training = state == RuntimeState.TrainingBeatmap;
        ReactionRules rules = ReactionRules.RhythmHeavenLike();
        _trainingLoopEvaluationDelaySeconds = training ? rules.MissInterval / 1000.0 : 0.0;
        _beatmapPlayer.StartBeatmap(songPath, chart, rules, new RhythmHeavenLikeReactionEvaluator(), independentBeatmapClock: training, loopMusic: training);
        if (training)
            EnableTrainingInputWidget();
        else
            ClearTrainingInputWidget();

        if (training)
            InitializeTrainingLoop();
    }

    private void InitializeTrainingLoop()
    {
        double chartEnd = _beatmapPlayer.RuntimeChartLoopEndSongPosition;
        if (chartEnd <= 0.0)
            chartEnd = Math.Max(1.0, _beatmapPlayer.Conductor?.Duration ?? 1.0);

        _trainingCurrentLoopNotes = _beatmapPlayer.ChartPlayer?.Notes.ToArray() ?? Array.Empty<Note>();
        _trainingCurrentLoopEndSongPosition = Math.Max(0.001, GetTrainingLoopEndSongPosition(_trainingCurrentLoopNotes, chartEnd));
        _trainingLoopDuration = Math.Max(0.001, _trainingCurrentLoopEndSongPosition);
        _trainingCurrentLoopEvaluationSongPosition = GetTrainingLoopEvaluationSongPosition(_trainingCurrentLoopNotes, _trainingCurrentLoopEndSongPosition);
        _trainingNextLoopNotes = _beatmapPlayer.AppendBeatmapLoopAt(_trainingCurrentLoopEndSongPosition, skipInitialOffset: false);
    }

    private void SetMiniGame(LevelNodeData node)
    {
        string miniGameId = node.MiniGameId?.Trim();
        if (string.IsNullOrWhiteSpace(miniGameId))
        {
            Fail("Set MiniGame node has no mini-game id.");
            return;
        }

        if (!EditorNoteDefinitions.TryCreateScene(miniGameId, out _))
        {
            Fail($"Unknown mini-game id: {miniGameId}");
            return;
        }

        _state = RuntimeState.Idle;
        _switchRhythmGameScene?.Invoke(miniGameId);
        FollowOutput("Next");
    }

    private void HandleBeatmapFinished()
    {
        if (_currentNode == null)
        {
            Fail("Beatmap finished without a current node.");
            return;
        }

        if (_state == RuntimeState.PlayRepresentationBeatmap)
        {
            CompleteLevel();
            return;
        }

        HandleTrainingLoopFinished();
    }

    private void UpdateTrainingLoopProgress()
    {
        if (_currentNode == null)
            return;

        if (_trainingSuccessExitPending)
        {
            if (_beatmapPlayer.GameplaySongPosition >= _trainingSuccessExitSongPosition)
            {
                _trainingSuccessExitPending = false;
                FollowOutput("Success");
            }

            return;
        }

        while (_state == RuntimeState.TrainingBeatmap
            && _trainingLoopDuration > 0.0
            && _beatmapPlayer.GameplaySongPosition >= _trainingCurrentLoopEvaluationSongPosition)
        {
            HandleTrainingLoopFinished();
            if (_state != RuntimeState.TrainingBeatmap || _trainingSuccessExitPending)
                return;
        }
    }

    private void HandleTrainingLoopFinished()
    {
        if (_currentNode == null)
        {
            Fail("Training loop finished without a current node.");
            return;
        }

        bool success = IsTrainingLoopSuccessful(_trainingCurrentLoopNotes);
        string nodeId = _currentNode.Id;
        int successCount = _trainingSuccessCounts.TryGetValue(nodeId, out int previousCount) ? previousCount : 0;
        int requiredSuccessCount = Math.Max(1, _currentNode.RequiredSuccessCount);

        if (success)
        {
            successCount++;
            _trainingSuccessCounts[nodeId] = successCount;
            if (successCount >= requiredSuccessCount)
            {
                BeginTrainingSuccessExitDelay();
                return;
            }

            ContinueTrainingLoop();
            return;
        }

        ContinueTrainingLoop();
    }

    private static bool IsTrainingLoopSuccessful(IReadOnlyList<Note> notes)
    {
        if (notes == null || notes.Count == 0)
            return true;

        foreach (Note note in notes)
        {
            if (!note.HasReacted || note.HasBeenMissed)
                return false;
        }

        return true;
    }

    private void BeginTrainingSuccessExitDelay()
    {
        double songPosition = _beatmapPlayer.GameplaySongPosition;
        double oneBeatSeconds = Math.Max(0.001, _beatmapPlayer.GetCrotchetAt(songPosition));
        _trainingSuccessExitPending = true;
        _trainingSuccessExitSongPosition = songPosition + oneBeatSeconds;
        _beatmapCompletionHandled = true;
        ClearTrainingInputWidget();
        _beatmapPlayer.ContinueEmptyBeatmapWithoutMusic(_trainingNextLoopNotes);
    }

    private void ContinueTrainingLoop()
    {
        _beatmapCompletionHandled = false;
        _trainingCurrentLoopEndSongPosition += _trainingLoopDuration;
        _trainingCurrentLoopNotes = _trainingNextLoopNotes;
        _trainingCurrentLoopEvaluationSongPosition = GetTrainingLoopEvaluationSongPosition(_trainingCurrentLoopNotes, _trainingCurrentLoopEndSongPosition);
        _trainingNextLoopNotes = _beatmapPlayer.AppendBeatmapLoopAt(_trainingCurrentLoopEndSongPosition, skipInitialOffset: false);
    }

    private double GetTrainingLoopEvaluationSongPosition(IReadOnlyList<Note> notes, double loopEndSongPosition)
    {
        double evaluationSongPosition = loopEndSongPosition;
        if (notes == null)
            return evaluationSongPosition;

        foreach (Note note in notes)
        {
            if (note == null)
                continue;

            evaluationSongPosition = Math.Max(evaluationSongPosition, note.SongPosition + _trainingLoopEvaluationDelaySeconds);
        }

        return evaluationSongPosition;
    }

    private double GetTrainingLoopEndSongPosition(IReadOnlyList<Note> notes, double chartEndSongPosition)
    {
        double loopEndSongPosition = chartEndSongPosition;
        if (notes == null || notes.Count == 0)
            return loopEndSongPosition;

        List<double> notePositions = new();
        foreach (Note note in notes)
        {
            if (note == null || double.IsNaN(note.SongPosition) || double.IsInfinity(note.SongPosition))
                continue;

            notePositions.Add(note.SongPosition);
        }

        if (notePositions.Count == 0)
            return loopEndSongPosition;

        notePositions.Sort();
        double lastNotePosition = notePositions[^1];
        double largestStep = 0.0;
        for (int i = 1; i < notePositions.Count; i++)
            largestStep = Math.Max(largestStep, notePositions[i] - notePositions[i - 1]);

        double tailStep = largestStep > 0.0
            ? largestStep
            : Math.Max(0.001, _beatmapPlayer.GetCrotchetAt(lastNotePosition));
        return Math.Max(loopEndSongPosition, lastNotePosition + tailStep);
    }

    private void CompleteLevel()
    {
        _state = RuntimeState.Complete;
        ClearTrainingInputWidget();
        _beatmapPlayer.StopBeatmap();

        LevelProgressSave save = LevelProgressSave.Load();
        foreach (string unlockId in _document.Level.UnlockLevelIds.Where(id => !string.IsNullOrWhiteSpace(id)))
            save.Unlock(unlockId);
        save.Save();

        _returnToMenu();
    }

    private void Fail(string message)
    {
        _state = RuntimeState.FailedGraph;
        _errorMessage = string.IsNullOrWhiteSpace(message) ? "Level runtime error." : message;
        ClearTrainingInputWidget();
        _beatmapPlayer.StopBeatmap();
    }

    private void EnableTrainingInputWidget()
    {
        if (_beatmapPlayer.ChartPlayer == null)
            return;

        GLOBALS.rhythmInputVisualElement = new RhythmInputVisualElement(_beatmapPlayer);
    }

    private void ClearTrainingInputWidget()
    {
        if (GLOBALS.rhythmInputVisualElement == null && _beatmapPlayer.VisualNoteMng == null)
            return;

        GLOBALS.rhythmInputVisualElement = null;
        _beatmapPlayer.VisualNoteMng = null;
    }

    private bool TryLoadBeatmap(string chartPathValue, out Chart chart, out string songPath, out string error)
    {
        chart = null;
        songPath = string.Empty;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(chartPathValue))
        {
            error = "Beatmap node has no chart path.";
            return false;
        }

        string chartPath = ResolveExistingPath(BeatmapPackagePaths.ResolveChartPath(chartPathValue));
        if (!File.Exists(chartPath))
        {
            error = $"Chart not found: {chartPathValue}";
            return false;
        }

        try
        {
            using FileStream stream = File.OpenRead(chartPath);
            XmlSerializer serializer = new(typeof(Chart));
            chart = (Chart)serializer.Deserialize(stream);
        }
        catch (Exception ex) when (ex is IOException || ex is InvalidOperationException || ex is UnauthorizedAccessException)
        {
            error = $"Cannot load chart: {chartPathValue}";
            return false;
        }

        if (chart == null)
        {
            error = $"Chart is empty: {chartPathValue}";
            return false;
        }

        string chartDirectory = Path.GetDirectoryName(chartPath);
        string chartSongPath = ResolveExistingPath(chart.SongPath, chartDirectory);
        if (!File.Exists(chartSongPath))
        {
            string packageSongPath = BeatmapPackagePaths.GetSongPathForPackage(BeatmapPackagePaths.GetPackagePath(chartPath));
            chartSongPath = ResolveExistingPath(packageSongPath);
        }

        if (!File.Exists(chartSongPath))
        {
            error = $"Song file not found for chart: {chartPathValue}";
            return false;
        }

        songPath = chartSongPath;
        return true;
    }

    private static string ResolveExistingPath(string path, string relativeToDirectory = null)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        string normalized = path.Replace('/', Path.DirectorySeparatorChar);
        List<string> candidates = new();

        if (Path.IsPathRooted(normalized))
        {
            candidates.Add(normalized);
        }
        else
        {
            candidates.Add(normalized);
            candidates.Add(Path.Combine(AppContext.BaseDirectory, normalized));
            if (!string.IsNullOrWhiteSpace(relativeToDirectory))
                candidates.Add(Path.Combine(relativeToDirectory, normalized));
        }

        return candidates.FirstOrDefault(File.Exists) ?? candidates[0];
    }

    private void DrawDialogue(SpriteBatch spriteBatch, LevelNodeData node)
    {
        Viewport viewport = GLOBALS.graphicsDevice.Viewport;
        Rectangle box = new(120, 70, viewport.Width - 240, 310);
        Color speakerColor = LevelSpeakerInfo.GetTextboxColor(node.Speaker);

        _ui.Fill(spriteBatch, new Rectangle(box.X + 8, box.Y + 8, box.Width, box.Height), Color.Black * 0.45f);
        _ui.Fill(spriteBatch, box, speakerColor * 0.92f);
        _ui.Stroke(spriteBatch, box, Color.White, 4);
        _ui.Stroke(spriteBatch, new Rectangle(box.X + 10, box.Y + 10, box.Width - 20, box.Height - 20), Color.Black * 0.35f, 2);

        if (_dialogueFont == null)
        {
            string text = node.Text ?? string.Empty;
            int textWidth = Math.Max(0, (text.Length * 4 - 1) * 3);
            int textHeight = 5 * 3;
            _ui.Label(spriteBatch, text, new Vector2(box.Center.X - textWidth / 2, box.Center.Y - textHeight / 2), Color.White, 3);
            return;
        }

        Rectangle textBounds = new(box.X + 48, box.Y + 36, box.Width - 96, box.Height - 72);
        List<string> lines = WrapText(_dialogueFont, node.Text ?? string.Empty, textBounds.Width).ToList();
        int lineGap = 4;
        int maxLines = Math.Max(1, (textBounds.Height + lineGap) / (_dialogueFont.LineSpacing + lineGap));
        if (lines.Count > maxLines)
            lines = lines.Take(maxLines).ToList();

        int totalTextHeight = lines.Count * _dialogueFont.LineSpacing + Math.Max(0, lines.Count - 1) * lineGap;
        float y = textBounds.Y + (textBounds.Height - totalTextHeight) / 2f;
        foreach (string line in lines)
        {
            Vector2 size = _dialogueFont.MeasureString(line);
            float x = textBounds.X + (textBounds.Width - size.X) / 2f;
            spriteBatch.DrawString(_dialogueFont, line, new Vector2(x, y), Color.White);
            y += _dialogueFont.LineSpacing + lineGap;
        }
    }

    private void DrawTrainingOverlay(SpriteBatch spriteBatch, LevelNodeData node)
    {
        int successCount = _trainingSuccessCounts.TryGetValue(node.Id, out int value) ? value : 0;
        int requiredSuccessCount = Math.Max(1, node.RequiredSuccessCount);
        int remaining = Math.Max(0, requiredSuccessCount - successCount);
        string text = remaining == 1 ? "Do it 1 more time" : $"Do it {remaining} more times";
        Viewport viewport = GLOBALS.graphicsDevice.Viewport;

        if (_dialogueFont != null)
        {
            Vector2 size = _dialogueFont.MeasureString(text);
            spriteBatch.DrawString(_dialogueFont, text, new Vector2((viewport.Width - size.X) / 2f, viewport.Height - size.Y - 44f), Color.White);
            return;
        }

        const int scale = 3;
        int textWidth = Math.Max(0, (text.Length * 4 - 1) * scale);
        _ui.Label(spriteBatch, text, new Vector2((viewport.Width - textWidth) / 2f, viewport.Height - 72), Color.White, scale);
    }

    private static void DrawTrainingInputWidget(SpriteBatch spriteBatch)
    {
        GLOBALS.rhythmInputVisualElement?.Draw(spriteBatch);
    }

    private void DrawError(SpriteBatch spriteBatch)
    {
        Viewport viewport = GLOBALS.graphicsDevice.Viewport;
        Rectangle box = new(viewport.Width / 2 - 420, viewport.Height / 2 - 110, 840, 220);
        _ui.Fill(spriteBatch, box, new Color(42, 12, 22, 235));
        _ui.Stroke(spriteBatch, ColorBox(box, 0), Color.Red, 4);
        _ui.Label(spriteBatch, "LEVEL RUNTIME ERROR", new Vector2(box.X + 28, box.Y + 28), Color.White, 4);
        _ui.Label(spriteBatch, _errorMessage, new Vector2(box.X + 28, box.Y + 92), Color.White, 3);
        _ui.Label(spriteBatch, "ENTER / ESC: return to menu", new Vector2(box.X + 28, box.Bottom - 52), Color.White * 0.75f, 2);
    }

    private static Rectangle ColorBox(Rectangle rectangle, int inset)
    {
        return new Rectangle(rectangle.X + inset, rectangle.Y + inset, rectangle.Width - inset * 2, rectangle.Height - inset * 2);
    }

    private static IReadOnlyList<string> WrapText(SpriteFont font, string text, float maxWidth)
    {
        if (font == null || string.IsNullOrWhiteSpace(text))
            return new[] { string.Empty };

        List<string> lines = new();
        foreach (string paragraph in text.Replace("\r", string.Empty).Split('\n'))
        {
            string currentLine = string.Empty;
            foreach (string word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                string candidate = string.IsNullOrEmpty(currentLine) ? word : currentLine + " " + word;
                if (font.MeasureString(candidate).X <= maxWidth)
                {
                    currentLine = candidate;
                    continue;
                }

                if (!string.IsNullOrEmpty(currentLine))
                    lines.Add(currentLine);
                currentLine = word;
            }

            lines.Add(currentLine);
        }

        return lines;
    }

    private bool Pressed(Keys key)
    {
        return _keyboard.IsKeyDown(key) && !_previousKeyboard.IsKeyDown(key);
    }

    private enum RuntimeState
    {
        Idle,
        Dialogue,
        TrainingBeatmap,
        PlayRepresentationBeatmap,
        Complete,
        FailedGraph
    }
}
