using System;
using System.Collections.Generic;
using System.Globalization;
using GameCore;
using Microsoft.Xna.Framework;
using MLP_RiM.Elements.Editor;
using Rhythm.Conductor;
using Rhythm.Note;
using Rhythm.Note.Evaluator;
using Rhythm.Note.Visual;

public readonly record struct ViewportCameraState(Vector2 Offset, float Rotation, Vector2 Zoom)
{
    public static readonly ViewportCameraState Identity = new(Vector2.Zero, 0f, Vector2.One);
}

public class BeatmapPlayer : IDisposable
{
    private const double TimelineEventEpsilonSeconds = 0.000001;
    private const double SwitchGameBlackoutLeadBeats = 0.25;

    public Conductor Conductor {get; private set;}
    public ChartPlayer ChartPlayer {get; private set;}
    public Chart CurrentChart {get; private set;}
    public bool HasAChartLoaded { get; private set; }
    public VisualNoteManager<VisualNote> VisualNoteMng {get; set;}
    public TempoMappedRhythmClock Clock { get; private set; }
    public bool IsSwitchGameBlackoutActive { get; private set; }
    public bool IsBlackAndWhiteActive { get; private set; }
    public ViewportCameraState CameraEffectState { get; private set; } = ViewportCameraState.Identity;
    public float FlashIntensity { get; private set; }
    public float CameraSaturation { get; private set; } = 1f;
    public double GameplaySongPosition => _usesIndependentBeatmapClock ? _beatmapSongPosition : Conductor?.SongPosition ?? 0.0;
    public bool IsContinuingEmptyBeatmap => _continueGameplayWithoutMusic;
    public bool MusicPlaybackFinished => Conductor != null
        && _startupComplete
        && !_loopMusic
        && _musicWasPlaying
        && (!Conductor.isPlaying() || _musicPlaybackElapsedSeconds >= Conductor.Duration - TimelineEventEpsilonSeconds);

    public event Action BeatmapStarted;
    public event Action BeatmapLoopAppended;
    public event Action<IReadOnlyCollection<Note>> BeatmapNotesRemoved;
    public event Action<string> RhythmGameSwitchRequested;

    private double _startupDelay;
    private double _startupTimer;
    private bool _startupComplete;
    private ChartTempoMap _tempoMap;
    private readonly List<RhythmGameSwitchMarker> _switchGameMarkers = new();
    private readonly List<double> _blackAndWhiteToggleMarkers = new();
    private readonly List<SaturationMarker> _saturationMarkers = new();
    private readonly List<ViewportOffsetClip> _viewportOffsetClips = new();
    private readonly List<FlashClip> _flashClips = new();
    private int _nextSwitchGameMarkerIndex;
    private int _nextBlackAndWhiteToggleMarkerIndex;
    private string _currentSwitchGameId;
    private bool _usesIndependentBeatmapClock;
    private bool _loopMusic;
    private double _beatmapSongPosition;
    private double _musicPlaybackElapsedSeconds;
    private bool _musicWasPlaying;
    private bool _continueGameplayWithoutMusic;
    private Chart _runtimeChart;

    private sealed class RhythmGameSwitchMarker
    {
        public double SongPosition { get; init; }
        public double BlackoutStartSongPosition { get; init; }
        public string RhythmGameId { get; init; }
    }

    private sealed class SaturationMarker
    {
        public double SongPosition { get; init; }
        public float Saturation { get; init; }
    }

    private sealed class ViewportOffsetClip
    {
        public double StartSongPosition { get; init; }
        public double EndSongPosition { get; init; }
        public ViewportCameraState StartState { get; set; }
        public ViewportCameraState TargetState { get; init; }
        public bool Instant { get; init; }
        public string Interpolation { get; init; }
    }

    private sealed class FlashClip
    {
        public double StartSongPosition { get; init; }
        public double EndSongPosition { get; init; }
    }

    public BeatmapPlayer(Conductor conductor, ChartPlayer chartPlayer)
    {
        this.Conductor = conductor;
        this.ChartPlayer = chartPlayer;
    }

    public BeatmapPlayer()
    {
        this.Conductor = null;
        this.ChartPlayer = null;
    }

    public void Update(GameTime gameTime)
    {
        if(Conductor == null) return;

        double elapsedSeconds = gameTime.ElapsedGameTime.TotalSeconds;
        bool isMusicPlaying = Conductor.isPlaying();

        if (_loopMusic && !isMusicPlaying)
        {
            Conductor.Seek(0);
            Conductor.Play();
            _musicPlaybackElapsedSeconds = 0.0;
            isMusicPlaying = Conductor.isPlaying();
        }

        if(!_startupComplete)
        {
            _startupTimer += elapsedSeconds;
            if(_startupTimer >= _startupDelay)
            {
                _startupComplete = true;
                Conductor.Play();
                isMusicPlaying = Conductor.isPlaying();
            }
        }

        if(isMusicPlaying)
        {
            Conductor.Update();
            _musicWasPlaying = true;
            _musicPlaybackElapsedSeconds += elapsedSeconds;
            if (_loopMusic && Conductor.SongPosition >= Conductor.Duration - TimelineEventEpsilonSeconds)
            {
                Conductor.Seek(0);
                Conductor.Play();
                _musicPlaybackElapsedSeconds = 0.0;
            }
        }

        if(isMusicPlaying || _continueGameplayWithoutMusic)
        {
            double gameplaySongPosition = GameplaySongPosition;
            Clock?.Update(gameplaySongPosition);
            ApplyEditorTimelineEventsAt(gameplaySongPosition, seek: false);
            ChartPlayer?.Update(gameplaySongPosition);
            VisualNoteMng?.Update(gameplaySongPosition);

            if (_usesIndependentBeatmapClock)
                _beatmapSongPosition += elapsedSeconds;
        }
    }
    

    public void StartMetronomeDebugMap(Dictionary<string, string> additionnalData, double startupDelaySeconds = 5.0)
    {
        DisposeConductor();

        _startupDelay = startupDelaySeconds;
        _startupTimer = 0;
        _startupComplete = false;
        _usesIndependentBeatmapClock = false;
        _loopMusic = false;
        _beatmapSongPosition = 0.0;
        _musicPlaybackElapsedSeconds = 0.0;
        _musicWasPlaying = false;
        _continueGameplayWithoutMusic = false;

        int bpm = 100;

        Conductor = new Conductor("Songs/metronome.wav", bpm, 0.078);
        CurrentChart = Chart.CreateMetronome(bpm, 200, startupDelaySeconds, additionnalData);
        _tempoMap = new ChartTempoMap(CurrentChart);
        RebuildSwitchGameMarkers();
        RebuildBlackAndWhiteToggleMarkers();
        RebuildSaturationMarkers();
        RebuildViewportOffsetClips();
        RebuildFlashClips();
        Clock = new TempoMappedRhythmClock(_tempoMap);
        HasAChartLoaded = true;
        _runtimeChart = RuntimeChartProjector.Project(CurrentChart, _tempoMap);
        ChartPlayer = new ChartPlayer(_runtimeChart, ReactionRules.RhythmHeavenLike(), new RhythmHeavenLikeReactionEvaluator());

        BeatmapStarted?.Invoke();
    }

    public void StartSeeSawDebugMap(Dictionary<string, string> additionnalData, double startupDelaySeconds = 5.0)
    {
        DisposeConductor();

        _startupDelay = startupDelaySeconds;
        _startupTimer = 0;
        _startupComplete = false;
        _usesIndependentBeatmapClock = false;
        _loopMusic = false;
        _beatmapSongPosition = 0.0;
        _musicPlaybackElapsedSeconds = 0.0;
        _musicWasPlaying = false;
        _continueGameplayWithoutMusic = false;

        int bpm = 100;
        double crotchet = 60.0 / bpm;
        Chart chart = new Chart
        {
            SongName = "See Saw Debug",
            BPM = bpm,
            Offset = startupDelaySeconds,
            ChartVersion = 2
        };

        for (int i = 0; i < 200; i++)
        {
            chart.Notes.Add(new ChartNote
            {
                SongPosition = startupDelaySeconds + i * crotchet * 2.0,
                BeatPosition = i * 2.0,
                HoldDuration = 0,
                HoldBeats = 0,
                InputActionToPress = "ReactMain",
                AdditionnalData = additionnalData
            });
        }

        Conductor = new Conductor("Songs/metronome.wav", bpm, 0.078);
        CurrentChart = chart;
        _tempoMap = new ChartTempoMap(CurrentChart);
        RebuildSwitchGameMarkers();
        RebuildBlackAndWhiteToggleMarkers();
        RebuildSaturationMarkers();
        RebuildViewportOffsetClips();
        RebuildFlashClips();
        Clock = new TempoMappedRhythmClock(_tempoMap);
        HasAChartLoaded = true;
        _runtimeChart = RuntimeChartProjector.Project(chart, _tempoMap);
        ChartPlayer = new ChartPlayer(_runtimeChart, ReactionRules.RhythmHeavenLike(), new RhythmHeavenLikeReactionEvaluator());

        BeatmapStarted?.Invoke();
    }

    public void StartBeatmap(string song_path, Chart chart, ReactionRules rules, IReactionEvaluator reactionEvaluator, bool independentBeatmapClock = false, bool loopMusic = false)
    {
        DisposeConductor();

        _startupDelay = 0;
        _startupTimer = 0;
        _startupComplete = true;
        _usesIndependentBeatmapClock = independentBeatmapClock;
        _loopMusic = loopMusic;
        _beatmapSongPosition = 0.0;
        _musicPlaybackElapsedSeconds = 0.0;
        _musicWasPlaying = false;
        _continueGameplayWithoutMusic = false;

        Conductor = new Conductor(song_path, chart.BPM, chart.Offset, musicVolume: GetMusicVolume(chart));
        CurrentChart = chart;
        _tempoMap = new ChartTempoMap(CurrentChart);
        RebuildSwitchGameMarkers();
        RebuildBlackAndWhiteToggleMarkers();
        RebuildSaturationMarkers();
        RebuildViewportOffsetClips();
        RebuildFlashClips();
        Clock = new TempoMappedRhythmClock(_tempoMap);
        HasAChartLoaded = true;
        _runtimeChart = RuntimeChartProjector.Project(chart, _tempoMap);
        ChartPlayer = new ChartPlayer(_runtimeChart, rules, reactionEvaluator);
        Clock.Update(GameplaySongPosition);
        Conductor.Play();
        BeatmapStarted?.Invoke();

    }

    public void StartBeatmapPaused(string songPath, Chart chart, ReactionRules rules, IReactionEvaluator reactionEvaluator, double firstBeatDelay = double.NaN)
    {
        DisposeConductor();

        _startupDelay = 0;
        _startupTimer = 0;
        _startupComplete = true;
        _usesIndependentBeatmapClock = false;
        _loopMusic = false;
        _beatmapSongPosition = 0.0;
        _musicPlaybackElapsedSeconds = 0.0;
        _musicWasPlaying = false;
        _continueGameplayWithoutMusic = false;

        double beatDelay = double.IsNaN(firstBeatDelay) ? chart.Offset : firstBeatDelay;
        Conductor = new Conductor(songPath, chart.BPM, beatDelay, musicVolume: GetMusicVolume(chart));
        CurrentChart = chart;
        _tempoMap = new ChartTempoMap(CurrentChart);
        RebuildSwitchGameMarkers();
        RebuildBlackAndWhiteToggleMarkers();
        RebuildSaturationMarkers();
        RebuildViewportOffsetClips();
        RebuildFlashClips();
        Clock = new TempoMappedRhythmClock(_tempoMap);
        HasAChartLoaded = true;
        _runtimeChart = RuntimeChartProjector.Project(chart, _tempoMap);
        ChartPlayer = new ChartPlayer(_runtimeChart, rules, reactionEvaluator);
        Clock.Update(GameplaySongPosition);
        BeatmapStarted?.Invoke();
    }

    public IReadOnlyList<Note> AppendBeatmapLoop(bool skipInitialOffset)
    {
        if (ChartPlayer == null || CurrentChart == null || _runtimeChart == null)
            return Array.Empty<Note>();

        _usesIndependentBeatmapClock = true;
        double sourceStart = skipInitialOffset ? RuntimeChartPlayableStartSongPosition : 0.0;
        double loopStart = Math.Max(0.0, GameplaySongPosition);
        IReadOnlyList<Note> appendedNotes = ChartPlayer.AppendChart(_runtimeChart, loopStart, sourceStart);
        BeatmapLoopAppended?.Invoke();
        return appendedNotes;
    }

    public double RuntimeChartPlayableStartSongPosition => GetChartPlayableStartSongPosition();
    public double RuntimeChartLoopEndSongPosition => GetChartLoopEndSongPosition();

    public IReadOnlyList<Note> AppendBeatmapLoopAt(double loopStartSongPosition, bool skipInitialOffset = false)
    {
        if (ChartPlayer == null || _runtimeChart == null)
            return Array.Empty<Note>();

        _usesIndependentBeatmapClock = true;
        double sourceStart = skipInitialOffset ? RuntimeChartPlayableStartSongPosition : 0.0;
        IReadOnlyList<Note> appendedNotes = ChartPlayer.AppendChart(_runtimeChart, Math.Max(0.0, loopStartSongPosition), sourceStart);
        BeatmapLoopAppended?.Invoke();
        return appendedNotes;
    }

    public void ContinueEmptyBeatmapWithoutMusic(IReadOnlyCollection<Note> notesToRemove)
    {
        ChartPlayer?.RemoveNotes(notesToRemove);
        BeatmapNotesRemoved?.Invoke(notesToRemove);
        _loopMusic = false;
        _usesIndependentBeatmapClock = true;
        _continueGameplayWithoutMusic = true;
        if (Conductor != null)
        {
            Conductor.SetMusicVolume(0f);
            if (!Conductor.isPlaying())
                Conductor.Play();
        }
    }

    public void ApplyChartEffectsAt(double songPosition)
    {
        Clock?.Update(songPosition);
    }

    public void StopBeatmap()
    {
        DisposeConductor();
    }

    public void ApplyEditorTimelineEventsAt(double songPosition, bool seek)
    {
        UpdateSwitchGameBlackout(songPosition);
        UpdateCameraSaturation(songPosition);
        UpdateViewportCameraOffset(songPosition);
        UpdateFlashIntensity(songPosition);

        if (seek)
        {
            ApplySwitchGameMarkerForSeek(songPosition);
            ApplyBlackAndWhiteToggleMarkersForSeek(songPosition);
            return;
        }

        while (_nextSwitchGameMarkerIndex < _switchGameMarkers.Count
            && _switchGameMarkers[_nextSwitchGameMarkerIndex].SongPosition <= songPosition + TimelineEventEpsilonSeconds)
        {
            RequestRhythmGameSwitch(_switchGameMarkers[_nextSwitchGameMarkerIndex].RhythmGameId);
            _nextSwitchGameMarkerIndex++;
        }

        while (_nextBlackAndWhiteToggleMarkerIndex < _blackAndWhiteToggleMarkers.Count
            && _blackAndWhiteToggleMarkers[_nextBlackAndWhiteToggleMarkerIndex] <= songPosition + TimelineEventEpsilonSeconds)
        {
            IsBlackAndWhiteActive = !IsBlackAndWhiteActive;
            _nextBlackAndWhiteToggleMarkerIndex++;
        }
    }

    private void UpdateSwitchGameBlackout(double songPosition)
    {
        IsSwitchGameBlackoutActive = false;

        for (int i = 1; i < _switchGameMarkers.Count; i++)
        {
            RhythmGameSwitchMarker marker = _switchGameMarkers[i];
            if (songPosition + TimelineEventEpsilonSeconds < marker.BlackoutStartSongPosition)
                break;

            if (songPosition + TimelineEventEpsilonSeconds < marker.SongPosition)
            {
                IsSwitchGameBlackoutActive = true;
                return;
            }
        }
    }

    private void ApplySwitchGameMarkerForSeek(double songPosition)
    {
        int markerIndex = -1;
        for (int i = 0; i < _switchGameMarkers.Count; i++)
        {
            if (_switchGameMarkers[i].SongPosition > songPosition + TimelineEventEpsilonSeconds)
                break;

            markerIndex = i;
        }

        _nextSwitchGameMarkerIndex = markerIndex + 1;
        if (markerIndex >= 0)
            RequestRhythmGameSwitch(_switchGameMarkers[markerIndex].RhythmGameId);
    }

    private void ApplyBlackAndWhiteToggleMarkersForSeek(double songPosition)
    {
        int togglesBeforePosition = 0;
        for (int i = 0; i < _blackAndWhiteToggleMarkers.Count; i++)
        {
            if (_blackAndWhiteToggleMarkers[i] > songPosition + TimelineEventEpsilonSeconds)
                break;

            togglesBeforePosition++;
        }

        _nextBlackAndWhiteToggleMarkerIndex = togglesBeforePosition;
        IsBlackAndWhiteActive = togglesBeforePosition % 2 == 1;
    }

    private void RequestRhythmGameSwitch(string rhythmGameId)
    {
        if (string.IsNullOrWhiteSpace(rhythmGameId) || rhythmGameId == _currentSwitchGameId)
            return;

        _currentSwitchGameId = rhythmGameId;
        RhythmGameSwitchRequested?.Invoke(rhythmGameId);
    }

    private void RebuildSwitchGameMarkers()
    {
        _switchGameMarkers.Clear();
        _nextSwitchGameMarkerIndex = 0;
        _currentSwitchGameId = null;
        IsSwitchGameBlackoutActive = false;

        if (CurrentChart?.EditorClips == null || _tempoMap == null)
            return;

        foreach (ChartEditorClip clip in CurrentChart.EditorClips)
        {
            if (!EditorClipDefinitions.IsSwitchGame(clip))
                continue;

            string targetGameId = EditorClipDefinitions.GetSwitchGameTargetGameId(clip);
            if (string.IsNullOrWhiteSpace(targetGameId))
                continue;

            _switchGameMarkers.Add(new RhythmGameSwitchMarker
            {
                SongPosition = _tempoMap.BeatToSeconds(clip.StartBeat),
                BlackoutStartSongPosition = _tempoMap.BeatToSeconds(clip.StartBeat - SwitchGameBlackoutLeadBeats),
                RhythmGameId = targetGameId
            });
        }

        _switchGameMarkers.Sort((a, b) => a.SongPosition.CompareTo(b.SongPosition));
    }

    private void RebuildBlackAndWhiteToggleMarkers()
    {
        _blackAndWhiteToggleMarkers.Clear();
        _nextBlackAndWhiteToggleMarkerIndex = 0;
        IsBlackAndWhiteActive = false;

        if (CurrentChart?.EditorClips == null || _tempoMap == null)
            return;

        foreach (ChartEditorClip clip in CurrentChart.EditorClips)
        {
            if (EditorClipDefinitions.IsBlackAndWhiteToggle(clip))
                _blackAndWhiteToggleMarkers.Add(_tempoMap.BeatToSeconds(clip.StartBeat));
        }

        _blackAndWhiteToggleMarkers.Sort();
    }

    private void RebuildSaturationMarkers()
    {
        _saturationMarkers.Clear();
        CameraSaturation = 1f;

        if (CurrentChart?.EditorClips == null || _tempoMap == null)
            return;

        foreach (ChartEditorClip clip in CurrentChart.EditorClips)
        {
            if (!EditorClipDefinitions.IsSaturation(clip))
                continue;

            Dictionary<string, string> data = CreateMergedClipData(clip);
            _saturationMarkers.Add(new SaturationMarker
            {
                SongPosition = _tempoMap.BeatToSeconds(clip.StartBeat),
                Saturation = Math.Max(0f, ParseFloat(data, GlobalEffectsProvider.SaturationValueKey, 1f))
            });
        }

        _saturationMarkers.Sort((a, b) => a.SongPosition.CompareTo(b.SongPosition));
    }

    private void RebuildViewportOffsetClips()
    {
        _viewportOffsetClips.Clear();
        CameraEffectState = ViewportCameraState.Identity;

        if (CurrentChart?.EditorClips == null || _tempoMap == null)
            return;

        foreach (ChartEditorClip clip in CurrentChart.EditorClips)
        {
            if (!EditorClipDefinitions.IsViewportOffset(clip))
                continue;

            Dictionary<string, string> data = CreateMergedClipData(clip);
            double startBeat = clip.StartBeat;
            double endBeat = startBeat + Math.Max(0.0, clip.LengthBeats);
            _viewportOffsetClips.Add(new ViewportOffsetClip
            {
                StartSongPosition = _tempoMap.BeatToSeconds(startBeat),
                EndSongPosition = _tempoMap.BeatToSeconds(endBeat),
                TargetState = new ViewportCameraState(
                    new Vector2(
                        ParseFloat(data, GlobalEffectsProvider.ViewportOffsetXKey),
                        ParseFloat(data, GlobalEffectsProvider.ViewportOffsetYKey)),
                    MathHelper.ToRadians(ParseFloat(data, GlobalEffectsProvider.ViewportRotationDegreesKey)),
                    new Vector2(
                        Math.Max(0.001f, ParseFloat(data, GlobalEffectsProvider.ViewportZoomXKey, 1f)),
                        Math.Max(0.001f, ParseFloat(data, GlobalEffectsProvider.ViewportZoomYKey, 1f)))),
                Instant = ParseBool(data, GlobalEffectsProvider.ViewportInstantKey),
                Interpolation = data.TryGetValue(GlobalEffectsProvider.ViewportInterpolationKey, out string interpolation)
                    ? interpolation
                    : GlobalEffectsProvider.ViewportInterpolationEaseInOutCubic
            });
        }

        _viewportOffsetClips.Sort((a, b) => a.StartSongPosition.CompareTo(b.StartSongPosition));
        for (int i = 0; i < _viewportOffsetClips.Count; i++)
            _viewportOffsetClips[i].StartState = EvaluateViewportCameraStateAt(_viewportOffsetClips[i].StartSongPosition, i);
    }

    private void RebuildFlashClips()
    {
        _flashClips.Clear();
        FlashIntensity = 0f;

        if (CurrentChart?.EditorClips == null || _tempoMap == null)
            return;

        foreach (ChartEditorClip clip in CurrentChart.EditorClips)
        {
            if (!EditorClipDefinitions.IsFlash(clip))
                continue;

            double startBeat = clip.StartBeat;
            double endBeat = startBeat + Math.Max(0.000001, clip.LengthBeats);
            _flashClips.Add(new FlashClip
            {
                StartSongPosition = _tempoMap.BeatToSeconds(startBeat),
                EndSongPosition = _tempoMap.BeatToSeconds(endBeat)
            });
        }

        _flashClips.Sort((a, b) => a.StartSongPosition.CompareTo(b.StartSongPosition));
    }

    private void UpdateFlashIntensity(double songPosition)
    {
        float intensity = 0f;
        foreach (FlashClip clip in _flashClips)
        {
            if (songPosition + TimelineEventEpsilonSeconds < clip.StartSongPosition)
                break;

            if (songPosition > clip.EndSongPosition + TimelineEventEpsilonSeconds)
                continue;

            double duration = Math.Max(TimelineEventEpsilonSeconds, clip.EndSongPosition - clip.StartSongPosition);
            float progress = MathHelper.Clamp((float)((songPosition - clip.StartSongPosition) / duration), 0f, 1f);
            intensity = Math.Max(intensity, 1f - Interpolation.EaseOutQuad(progress));
        }

        FlashIntensity = MathHelper.Clamp(intensity, 0f, 1f);
    }

    private void UpdateCameraSaturation(double songPosition)
    {
        float saturation = 1f;
        foreach (SaturationMarker marker in _saturationMarkers)
        {
            if (marker.SongPosition > songPosition + TimelineEventEpsilonSeconds)
                break;

            saturation = marker.Saturation;
        }

        CameraSaturation = saturation;
    }

    private void UpdateViewportCameraOffset(double songPosition)
    {
        CameraEffectState = EvaluateViewportCameraStateAt(songPosition, _viewportOffsetClips.Count);
    }

    private ViewportCameraState EvaluateViewportCameraStateAt(double songPosition, int clipCount)
    {
        ViewportCameraState state = ViewportCameraState.Identity;
        int count = Math.Min(clipCount, _viewportOffsetClips.Count);
        for (int i = 0; i < count; i++)
        {
            ViewportOffsetClip clip = _viewportOffsetClips[i];
            if (songPosition + TimelineEventEpsilonSeconds < clip.StartSongPosition)
                break;

            float progress = GetViewportOffsetProgress(clip, songPosition);
            state = LerpViewportCameraState(clip.StartState, clip.TargetState, progress);
        }

        return state;
    }

    private static ViewportCameraState LerpViewportCameraState(ViewportCameraState start, ViewportCameraState end, float progress)
    {
        return new ViewportCameraState(
            Vector2.Lerp(start.Offset, end.Offset, progress),
            MathHelper.Lerp(start.Rotation, end.Rotation, progress),
            Vector2.Lerp(start.Zoom, end.Zoom, progress));
    }

    private static float GetViewportOffsetProgress(ViewportOffsetClip clip, double songPosition)
    {
        if (clip.Instant)
            return 1f;

        double duration = Math.Max(TimelineEventEpsilonSeconds, clip.EndSongPosition - clip.StartSongPosition);
        float progress = MathHelper.Clamp((float)((songPosition - clip.StartSongPosition) / duration), 0f, 1f);
        return ApplyViewportInterpolation(progress, clip.Interpolation);
    }

    private static float ApplyViewportInterpolation(float progress, string interpolation)
    {
        progress = MathHelper.Clamp(progress, 0f, 1f);
        return interpolation switch
        {
            GlobalEffectsProvider.ViewportInterpolationLinear => Interpolation.Linear(progress),
            "ease_in_sine" => Interpolation.EaseInSine(progress),
            "ease_out_sine" => Interpolation.EaseOutSine(progress),
            "ease_in_out_sine" => Interpolation.EaseInOutSine(progress),
            "ease_in_quad" => Interpolation.EaseInQuad(progress),
            "ease_out_quad" => Interpolation.EaseOutQuad(progress),
            "ease_in_out_quad" => Interpolation.EaseInOutQuad(progress),
            GlobalEffectsProvider.ViewportInterpolationEaseInCubic or "ease_in" => Interpolation.EaseInCubic(progress),
            GlobalEffectsProvider.ViewportInterpolationEaseOutCubic or "ease_out" => Interpolation.EaseOutCubic(progress),
            GlobalEffectsProvider.ViewportInterpolationEaseInOutCubic or "ease_in_out" => Interpolation.EaseInOutCubic(progress),
            "ease_in_quart" => Interpolation.EaseInQuart(progress),
            "ease_out_quart" => Interpolation.EaseOutQuart(progress),
            "ease_in_out_quart" => Interpolation.EaseInOutQuart(progress),
            "ease_in_quint" => Interpolation.EaseInQuint(progress),
            "ease_out_quint" => Interpolation.EaseOutQuint(progress),
            "ease_in_out_quint" => Interpolation.EaseInOutQuint(progress),
            "ease_in_expo" => Interpolation.EaseInExpo(progress),
            "ease_out_expo" => Interpolation.EaseOutExpo(progress),
            "ease_in_out_expo" => Interpolation.EaseInOutExpo(progress),
            "ease_in_circ" => Interpolation.EaseInCirc(progress),
            "ease_out_circ" => Interpolation.EaseOutCirc(progress),
            "ease_in_out_circ" => Interpolation.EaseInOutCirc(progress),
            "ease_in_back" => Interpolation.EaseInBack(progress),
            "ease_out_back" => Interpolation.EaseOutBack(progress),
            "ease_in_out_back" => Interpolation.EaseInOutBack(progress),
            "ease_in_elastic" => Interpolation.EaseInElastic(progress),
            "ease_out_elastic" => Interpolation.EaseOutElastic(progress),
            "ease_in_out_elastic" => Interpolation.EaseInOutElastic(progress),
            "ease_in_bounce" => Interpolation.EaseInBounce(progress),
            "ease_out_bounce" => Interpolation.EaseOutBounce(progress),
            "ease_in_out_bounce" => Interpolation.EaseInOutBounce(progress),
            "smooth_step" => Interpolation.SmoothStep(progress),
            "smoother_step" => Interpolation.SmootherStep(progress),
            _ => Interpolation.EaseInOutCubic(progress)
        };
    }

    private static Dictionary<string, string> CreateMergedClipData(ChartEditorClip clip)
    {
        EditorClipDefinition definition = EditorClipDefinitions.Find(clip.RhythmGameId, clip.ClipTypeId);
        Dictionary<string, string> data = new(definition?.DefaultData ?? new Dictionary<string, string>());
        foreach (KeyValuePair<string, string> pair in clip.Data ?? new Dictionary<string, string>())
            data[pair.Key] = pair.Value;

        return data;
    }

    private static float GetMusicVolume(Chart chart)
    {
        if (chart == null || double.IsNaN(chart.MusicVolume) || double.IsInfinity(chart.MusicVolume))
            return (float)Chart.DefaultMusicVolume;

        return (float)Math.Clamp(chart.MusicVolume, 0.0, Chart.MaxMusicVolume);
    }

    private static float ParseFloat(IReadOnlyDictionary<string, string> data, string key, float fallback = 0f)
    {
        return data != null
            && data.TryGetValue(key, out string value)
            && float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed)
            ? parsed
            : fallback;
    }

    private static bool ParseBool(IReadOnlyDictionary<string, string> data, string key)
    {
        return data != null
            && data.TryGetValue(key, out string value)
            && (bool.TryParse(value, out bool parsed) && parsed
                || string.Equals(value, "1", StringComparison.OrdinalIgnoreCase));
    }

    public double GetBpmAt(double songPosition)
    {
        if (CurrentChart == null)
            return Conductor?.BPM ?? 100;

        return GetTempoMap().GetBpmAtSeconds(songPosition);
    }

    public double GetCrotchetAt(double songPosition)
    {
        return CurrentChart == null
            ? Conductor?.Crotchet ?? 0.6
            : GetTempoMap().GetCrotchetAt(songPosition);
    }

    public double GetBeatAt(double songPosition)
    {
        return CurrentChart == null
            ? songPosition / (Conductor?.Crotchet ?? 0.6)
            : GetTempoMap().SecondsToBeat(songPosition);
    }

    public double GetSongPositionAtBeat(double beat)
    {
        return CurrentChart == null
            ? beat * (Conductor?.Crotchet ?? 0.6)
            : GetTempoMap().BeatToSeconds(beat);
    }

    public double GetBpmAtBeat(double beat)
    {
        return CurrentChart == null
            ? Conductor?.BPM ?? 100
            : GetTempoMap().GetBpmAtBeat(beat);
    }

    public double GetSecondsPerBeatAtBeat(double beat)
    {
        return CurrentChart == null
            ? Conductor?.Crotchet ?? 0.6
            : GetTempoMap().GetSecondsPerBeatAtBeat(beat);
    }

    public double GetMaxCrotchet()
    {
        return CurrentChart == null
            ? Conductor?.Crotchet ?? 0.6
            : GetTempoMap().GetMaxCrotchet();
    }

    public void Dispose()
    {
        DisposeConductor();
        GC.SuppressFinalize(this);
    }

    private void DisposeConductor()
    {
        Conductor?.Dispose();
        Conductor = null;
        CurrentChart = null;
        _runtimeChart = null;
        _tempoMap = null;
        Clock = null;
        HasAChartLoaded = false;
        ChartPlayer = null;
        VisualNoteMng = null;
        _switchGameMarkers.Clear();
        _blackAndWhiteToggleMarkers.Clear();
        _saturationMarkers.Clear();
        _viewportOffsetClips.Clear();
        _flashClips.Clear();
        _nextSwitchGameMarkerIndex = 0;
        _nextBlackAndWhiteToggleMarkerIndex = 0;
        _currentSwitchGameId = null;
        IsSwitchGameBlackoutActive = false;
        IsBlackAndWhiteActive = false;
        CameraSaturation = 1f;
        CameraEffectState = ViewportCameraState.Identity;
        FlashIntensity = 0f;
        _usesIndependentBeatmapClock = false;
        _loopMusic = false;
        _beatmapSongPosition = 0.0;
        _musicPlaybackElapsedSeconds = 0.0;
        _musicWasPlaying = false;
        _continueGameplayWithoutMusic = false;
    }

    private ChartTempoMap GetTempoMap()
    {
        if (_tempoMap == null)
            _tempoMap = new ChartTempoMap(CurrentChart);

        return _tempoMap;
    }

    private double GetChartPlayableStartSongPosition()
    {
        double start = double.PositiveInfinity;

        if (_runtimeChart?.Notes != null)
        {
            foreach (ChartNote note in _runtimeChart.Notes)
                start = Math.Min(start, note.SongPosition);
        }

        if (CurrentChart?.EditorClips != null && _tempoMap != null)
        {
            foreach (ChartEditorClip clip in CurrentChart.EditorClips)
            {
                if (!IsPlayableEditorClip(clip))
                    continue;

                start = Math.Min(start, _tempoMap.BeatToSeconds(clip.StartBeat));
            }
        }

        if (double.IsPositiveInfinity(start))
            return Math.Max(0.0, _runtimeChart?.Offset ?? CurrentChart?.Offset ?? 0.0);

        return Math.Max(0.0, start);
    }

    private double GetChartLoopEndSongPosition()
    {
        double end = GetChartEndSongPosition(_runtimeChart);

        if (CurrentChart?.EditorClips != null && _tempoMap != null)
        {
            foreach (ChartEditorClip clip in CurrentChart.EditorClips)
            {
                if (!IsPlayableEditorClip(clip))
                    continue;

                double clipEndBeat = clip.StartBeat + Math.Max(0.0, clip.LengthBeats);
                end = Math.Max(end, _tempoMap.BeatToSeconds(clipEndBeat));
            }
        }

        return Math.Max(0.0, end);
    }

    private static bool IsPlayableEditorClip(ChartEditorClip clip)
    {
        return clip != null
            && clip.LengthBeats > 0.0
            && !string.IsNullOrWhiteSpace(clip.InputAction);
    }

    private static double GetChartEndSongPosition(Chart chart)
    {
        if (chart?.Notes == null || chart.Notes.Count == 0)
            return 0.0;

        double end = 0.0;
        foreach (ChartNote note in chart.Notes)
            end = Math.Max(end, note.SongPosition + Math.Max(0.0, note.HoldDuration));

        return end;
    }
}
