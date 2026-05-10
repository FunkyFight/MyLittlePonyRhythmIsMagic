using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using Rhythm.Note;

namespace MLP_RiM.Elements.Editor;

public sealed class BeatmapEditorDocument
{
    private const double BeatEpsilon = 0.0005;

    public Chart Chart { get; private set; }
    public string SongPath { get; set; }
    public string ChartPath { get; private set; }
    public string PackagePath { get; private set; }
    public string AssetsPath => BeatmapPackagePaths.GetAssetsPath(PackagePath);
    public string PackageSongPath => BeatmapPackagePaths.GetSongPathForPackage(PackagePath);
    public bool IsLegacyChart { get; private set; }
    public bool IsDirty { get; private set; }
    public ChartTempoMap TempoMap => _tempoMap ??= new ChartTempoMap(Chart);
    public IReadOnlyList<ChartEditorTrack> EditorTracks => Chart?.EditorTracks != null ? Chart.EditorTracks : Array.Empty<ChartEditorTrack>();
    public IReadOnlyList<ChartEditorClip> EditorClips => Chart?.EditorClips != null ? Chart.EditorClips : Array.Empty<ChartEditorClip>();

    public double Crotchet => Chart.BPM > 0 ? 60.0 / Chart.BPM : 0.6;

    private ChartTempoMap _tempoMap;
    private bool _needsV1Backup;
    private string _v1BackupSourcePath;
    private bool _editorClipsAreRuntimeSource;
    private IReadOnlyList<ChartNote> _sortedChartNotesCache;
    private readonly Dictionary<ChartNote, NoteTimingResult> _chartNoteTimingCache = new();
    private readonly Dictionary<NoteTypeId, IReadOnlyDictionary<string, object>> _timingContextCache = new();

    private BeatmapEditorDocument(Chart chart, string songPath, string chartPath)
    {
        Chart = chart;
        SongPath = songPath;
        ChartPath = BeatmapPackagePaths.ResolveChartPath(chartPath);
        PackagePath = BeatmapPackagePaths.GetPackagePath(ChartPath);
        IsLegacyChart = BeatmapPackagePaths.IsLegacyXmlChartPath(ChartPath);
        NormalizeChart();
    }

    public static BeatmapEditorDocument CreateNew(string songPath, string chartPath, double bpm = 100)
    {
        chartPath = BeatmapPackagePaths.ResolveChartPath(chartPath);
        string songName = string.IsNullOrWhiteSpace(songPath) ? "Untitled" : Path.GetFileNameWithoutExtension(songPath);
        Chart chart = new()
        {
            SongName = songName,
            BeatmapName = GetDefaultBeatmapName(chartPath),
            Beatmapper = "Unknown",
            ArtistName = "Unknown",
            MusicName = songName,
            SongPath = songPath,
            BPM = bpm,
            Offset = 0.078,
            LeadInBeats = 0,
            ChartVersion = 2,
            Notes = new List<ChartNote>(),
            Effects = new List<ChartEffect>(),
            Tags = new List<string>(),
            EditorTracks = CreateDefaultTracks(),
            EditorTracksSpecified = true,
            EditorClips = new List<ChartEditorClip>(),
            EditorClipsSpecified = true
        };

        BeatmapEditorDocument document = new(chart, songPath, chartPath)
        {
            _editorClipsAreRuntimeSource = true
        };
        document.EnsurePackageDirectoriesIfNeeded();
        return document;
    }

    public static BeatmapEditorDocument CreateNewPackage(string songPath, string beatmapName, double bpm = 100)
    {
        return CreateNew(songPath, BeatmapPackagePaths.GetAvailablePackageChartPath(beatmapName), bpm);
    }

    public static BeatmapEditorDocument LoadOrCreate(string songPath, string chartPath, double bpm = 100)
    {
        chartPath = BeatmapPackagePaths.ResolveChartPath(chartPath);
        if (!File.Exists(chartPath) || new FileInfo(chartPath).Length == 0)
            return CreateNew(songPath, chartPath, bpm);

        Chart chart;
        try
        {
            using FileStream stream = File.OpenRead(chartPath);
            XmlSerializer serializer = new(typeof(Chart));
            chart = (Chart)serializer.Deserialize(stream);
        }
        catch (InvalidOperationException)
        {
            BackupInvalidChart(chartPath);
            return CreateNew(songPath, chartPath, bpm);
        }

        if (chart == null)
            return CreateNew(songPath, chartPath, bpm);

        if (!string.IsNullOrWhiteSpace(chart.SongPath))
            songPath = chart.SongPath;

        return new BeatmapEditorDocument(chart, songPath, chartPath);
    }

    private static void BackupInvalidChart(string chartPath)
    {
        if (!File.Exists(chartPath) || new FileInfo(chartPath).Length == 0)
            return;

        string backupPath = $"{chartPath}.invalid.{DateTime.Now:yyyyMMddHHmmss}";
        File.Copy(chartPath, backupPath, overwrite: false);
    }

    public void Save(string chartPath = null)
    {
        if (!string.IsNullOrWhiteSpace(chartPath))
            SetChartPath(chartPath, markDirty: false);

        EnsurePackagePathForSave();

        if (_editorClipsAreRuntimeSource || HasEditorAuthoredClips())
        {
            _editorClipsAreRuntimeSource = true;
            CompileClipsToRuntimeNotes();
        }
        else
        {
            SyncEditorClipsFromRuntimeNotes();
        }

        string directory = Path.GetDirectoryName(ChartPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        EnsurePackageDirectoriesIfNeeded();

        SynchronizeAllDerivedTiming();
        SortNotes();
        SortEffects();

        if (_needsV1Backup)
            BackupV1ChartIfNeeded();

        string tempPath = ChartPath + ".tmp";
        using (FileStream stream = File.Create(tempPath))
        {
            XmlSerializer serializer = new(typeof(Chart));
            serializer.Serialize(stream, Chart);
        }

        File.Move(tempPath, ChartPath, overwrite: true);
        _needsV1Backup = false;
        IsDirty = false;
    }

    private void BackupV1ChartIfNeeded()
    {
        string sourcePath = !string.IsNullOrWhiteSpace(_v1BackupSourcePath) ? _v1BackupSourcePath : ChartPath;
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            return;

        string backupPath = sourcePath + ".v1backup";
        if (!File.Exists(backupPath))
            File.Copy(sourcePath, backupPath, overwrite: false);
    }

    public bool TryPlaceNote(EditorNoteDefinition definition, double songPosition, int variantIndex, out ChartNote placedNote, out string reason)
    {
        return TryPlaceNoteAtBeat(definition, TempoMap.SecondsToBeat(songPosition), variantIndex, out placedNote, out reason);
    }

    public bool TryPlaceNoteAtBeat(EditorNoteDefinition definition, double beat, int variantIndex, out ChartNote placedNote, out string reason)
    {
        if (definition == null)
        {
            placedNote = null;
            reason = "Invalid note definition";
            return false;
        }

        beat = ClampNoteBeatToSongStart(beat);
        double songPosition = TempoMap.BeatToSeconds(beat);
        ChartNote note = definition.CreateChartNote(songPosition, TempoMap.GetSecondsPerBeatAtBeat(beat), variantIndex);
        ChartTiming.SetNoteBeat(note, beat);
        ChartTiming.SetNoteHoldBeats(note, definition.HoldBeats);
        SynchronizeNoteDuration(note, definition);
        return TryPlaceNote(definition, note, out placedNote, out reason);
    }

    public bool TryPlaceNote(EditorNoteDefinition definition, ChartNote note, out ChartNote placedNote, out string reason)
    {
        if (TryPlaceNotes(new[] { new EditorNotePlacement(definition, note) }, out IReadOnlyList<ChartNote> placedNotes, out reason))
        {
            placedNote = placedNotes[0];
            return true;
        }

        placedNote = null;
        return false;
    }

    public bool TryPlaceNotes(IReadOnlyList<EditorNotePlacement> placements, out IReadOnlyList<ChartNote> placedNotes, out string reason)
    {
        placedNotes = Array.Empty<ChartNote>();
        if (placements == null || placements.Count == 0)
        {
            reason = "No notes to place";
            return false;
        }

        List<EditorNotePlacement> normalizedPlacements = new();
        foreach (EditorNotePlacement placement in placements)
        {
            if (placement?.Definition == null || placement.Note == null)
            {
                reason = "Invalid note placement";
                return false;
            }

            ChartNote note = placement.Note;
            EnsureNoteBeat(note, TempoMap);
            note.InputActionToPress ??= placement.Definition.InputAction;
            note.AdditionnalData ??= new Dictionary<string, string>();
            SynchronizeNoteDuration(note, placement.Definition);
            normalizedPlacements.Add(new EditorNotePlacement(placement.Definition, note));
        }

        IReadOnlyList<ChartNote> contextualNotes = CreateContextualNotes(normalizedPlacements);
        IReadOnlyList<ChartNote> changedNotes = normalizedPlacements.Select(placement => placement.Note).ToArray();
        if (!TryValidateNotes(GetProvidersForPlacements(normalizedPlacements), contextualNotes, changedNotes, out reason))
            return false;

        List<ChartNote> notes = changedNotes.ToList();
        Chart.Notes.AddRange(notes);
        SynchronizeAllDerivedTiming();
        SortNotes();
        UseRuntimeNotesAsEditorClipSource();
        IsDirty = true;
        placedNotes = notes;
        reason = null;
        return true;
    }

    public bool DeleteNearest(double songPosition, double maxDistanceSeconds, out ChartNote deletedNote)
    {
        double beat = TempoMap.SecondsToBeat(songPosition);
        double maxDistanceBeats = SecondsDistanceToBeats(songPosition, maxDistanceSeconds);
        return DeleteNearestAtBeat(beat, maxDistanceBeats, out deletedNote);
    }

    public bool DeleteNearestAtBeat(double beat, double maxDistanceBeats, out ChartNote deletedNote)
    {
        deletedNote = FindNearestAtBeat(beat, maxDistanceBeats);
        if (deletedNote == null)
            return false;

        return RemoveNote(deletedNote);
    }

    public bool RemoveNote(ChartNote note)
    {
        if (note == null || !Chart.Notes.Remove(note))
            return false;

        SynchronizeAllDerivedTiming();
        SortNotes();
        UseRuntimeNotesAsEditorClipSource();
        IsDirty = true;
        return true;
    }

    public bool RemoveNotes(IEnumerable<ChartNote> notes)
    {
        if (notes == null)
            return false;

        bool removedAny = false;
        foreach (ChartNote note in notes.ToArray())
            removedAny |= note != null && Chart.Notes.Remove(note);

        if (!removedAny)
            return false;

        SynchronizeAllDerivedTiming();
        SortNotes();
        UseRuntimeNotesAsEditorClipSource();
        IsDirty = true;
        return true;
    }

    public bool ApplyNoteSnapshot(ChartNote target, ChartNote snapshot)
    {
        if (target == null || snapshot == null || !Chart.Notes.Contains(target))
            return false;

        target.SongPosition = snapshot.SongPosition;
        target.BeatPosition = snapshot.BeatPosition;
        target.HoldDuration = snapshot.HoldDuration;
        target.HoldBeats = snapshot.HoldBeats;
        target.InputActionToPress = snapshot.InputActionToPress;
        target.AdditionnalData = new Dictionary<string, string>(snapshot.AdditionnalData ?? new Dictionary<string, string>());
        SynchronizeAllDerivedTiming();
        SortNotes();
        UseRuntimeNotesAsEditorClipSource();
        IsDirty = true;
        return true;
    }

    public bool ApplyNotePatch(ChartNote target, NotePatch patch)
    {
        if (target == null || patch == null || !Chart.Notes.Contains(target))
            return false;

        patch.ApplyTo(target);
        SynchronizeAllDerivedTiming();
        SortNotes();
        UseRuntimeNotesAsEditorClipSource();
        IsDirty = true;
        return true;
    }

    public bool MoveNote(ChartNote note, double songPosition)
    {
        return MoveNoteToBeat(note, TempoMap.SecondsToBeat(songPosition));
    }

    public bool MoveNoteToBeat(ChartNote note, double beat)
    {
        if (note == null || !Chart.Notes.Contains(note))
            return false;

        ChartTiming.SetNoteBeat(note, ClampNoteBeatToSongStart(beat));
        SynchronizeNoteDuration(note, EditorNoteDefinitions.FromChartNote(note));
        SortNotes();
        UseRuntimeNotesAsEditorClipSource();
        IsDirty = true;
        return true;
    }

    public bool TryPlaceEffect(ChartEffect effect, out ChartEffect placedEffect, out string reason)
    {
        placedEffect = null;
        if (effect == null)
        {
            reason = "Invalid effect";
            return false;
        }

        NormalizeEffect(effect);
        Chart.Effects.Add(effect);
        SynchronizeAllDerivedTiming();
        SortEffects();
        IsDirty = true;
        placedEffect = effect;
        reason = null;
        return true;
    }

    public bool DeleteNearestEffect(double songPosition, double maxDistanceSeconds, out ChartEffect deletedEffect)
    {
        double beat = TempoMap.SecondsToBeat(songPosition);
        double maxDistanceBeats = SecondsDistanceToBeats(songPosition, maxDistanceSeconds);
        return DeleteNearestEffectAtBeat(beat, maxDistanceBeats, out deletedEffect);
    }

    public bool DeleteNearestEffectAtBeat(double beat, double maxDistanceBeats, out ChartEffect deletedEffect)
    {
        deletedEffect = FindNearestEffectAtBeat(beat, maxDistanceBeats);
        if (deletedEffect == null)
            return false;

        Chart.Effects.Remove(deletedEffect);
        SynchronizeAllDerivedTiming();
        IsDirty = true;
        return true;
    }

    public bool MoveEffect(ChartEffect effect, double songPosition, bool sectionOffsetFollowsPosition)
    {
        return MoveEffectToBeat(effect, TempoMap.SecondsToBeat(Math.Max(0, songPosition)), sectionOffsetFollowsPosition);
    }

    public bool MoveEffectToBeat(ChartEffect effect, double beat, bool sectionOffsetFollowsPosition = true)
    {
        if (effect == null || !Chart.Effects.Contains(effect))
            return false;

        ChartTiming.SetEffectBeat(effect, beat);
        if (sectionOffsetFollowsPosition)
            effect.SetSectionOffset(0);

        SynchronizeAllDerivedTiming();
        SortEffects();
        IsDirty = true;
        return true;
    }

    public bool RemoveEffect(ChartEffect effect)
    {
        if (effect == null || !Chart.Effects.Remove(effect))
            return false;

        SynchronizeAllDerivedTiming();
        SortEffects();
        IsDirty = true;
        return true;
    }

    public bool ApplyEffectSnapshot(ChartEffect target, ChartEffect snapshot)
    {
        if (target == null || snapshot == null || !Chart.Effects.Contains(target))
            return false;

        target.SongPosition = snapshot.SongPosition;
        target.BeatPosition = snapshot.BeatPosition;
        target.EffectType = snapshot.EffectType;
        target.Data = new Dictionary<string, string>(snapshot.Data ?? new Dictionary<string, string>());
        SynchronizeAllDerivedTiming();
        SortEffects();
        IsDirty = true;
        return true;
    }

    public bool UpdateTempoChange(ChartEffect effect, double beat, double bpm, bool sectionOffsetFollowsPosition = true)
    {
        if (effect == null || !effect.IsBpmChange || !Chart.Effects.Contains(effect) || bpm <= 0)
            return false;

        ChartTiming.SetEffectBeat(effect, beat);
        effect.SetBpm(bpm);
        if (sectionOffsetFollowsPosition)
            effect.SetSectionOffset(0);

        SynchronizeAllDerivedTiming();
        SortEffects();
        IsDirty = true;
        return true;
    }

    public int NormalizeBpmChangesToNearestGlobalBeat()
    {
        int normalizedCount = 0;
        foreach (ChartEffect effect in Chart.Effects.Where(effect => effect?.IsBpmChange == true).ToList())
        {
            double beat = GetEffectBeat(effect);
            if (double.IsNaN(beat) || double.IsInfinity(beat))
                continue;

            double snappedBeat = Math.Round(beat, MidpointRounding.AwayFromZero);
            if (Math.Abs(beat - snappedBeat) <= 0.000000001)
                continue;

            ChartTiming.SetEffectBeat(effect, snappedBeat);
            effect.SetSectionOffset(0);
            normalizedCount++;
            SortEffects();
        }

        if (normalizedCount > 0)
        {
            SynchronizeAllDerivedTiming();
            SortEffects();
            IsDirty = true;
        }

        return normalizedCount;
    }

    public void MarkDirty()
    {
        SynchronizeAllDerivedTiming();
        UseRuntimeNotesAsEditorClipSource();
        IsDirty = true;
    }

    public ChartNote FindNearest(double songPosition, double maxDistanceSeconds)
    {
        double beat = TempoMap.SecondsToBeat(songPosition);
        return FindNearestAtBeat(beat, SecondsDistanceToBeats(songPosition, maxDistanceSeconds));
    }

    public ChartNote FindNearestAtBeat(double beat, double maxDistanceBeats)
    {
        return Chart.Notes
            .Select(note => new { Note = note, Distance = Math.Abs(GetNoteBeat(note) - beat) })
            .Where(item => item.Distance <= maxDistanceBeats)
            .OrderBy(item => item.Distance)
            .Select(item => item.Note)
            .FirstOrDefault();
    }

    public ChartEffect FindNearestEffect(double songPosition, double maxDistanceSeconds)
    {
        double beat = TempoMap.SecondsToBeat(songPosition);
        return FindNearestEffectAtBeat(beat, SecondsDistanceToBeats(songPosition, maxDistanceSeconds));
    }

    public ChartEffect FindNearestEffectAtBeat(double beat, double maxDistanceBeats)
    {
        return Chart.Effects
            .Select(effect => new { Effect = effect, Distance = Math.Abs(GetEffectBeat(effect) - beat) })
            .Where(item => item.Distance <= maxDistanceBeats)
            .OrderBy(item => item.Distance)
            .Select(item => item.Effect)
            .FirstOrDefault();
    }

    public bool IsOccupiedAt(double songPosition)
    {
        double beat = TempoMap.SecondsToBeat(songPosition);
        foreach (ChartNote note in Chart.Notes)
        {
            EditorNoteDefinition definition = EditorNoteDefinitions.FromChartNote(note);
            double start = GetContextualStartBeat(note, definition);
            double end = GetContextualEndBeat(note, definition);
            if (beat >= start && beat <= end)
                return true;
        }

        return false;
    }

    public IEnumerable<ChartNote> GetNotesInWindow(double startSongPosition, double endSongPosition)
    {
        double startBeat = TempoMap.SecondsToBeat(startSongPosition);
        double endBeat = TempoMap.SecondsToBeat(endSongPosition);
        if (endBeat < startBeat)
            (startBeat, endBeat) = (endBeat, startBeat);

        foreach (ChartNote note in Chart.Notes)
        {
            EditorNoteDefinition definition = EditorNoteDefinitions.FromChartNote(note);
            if (GetContextualEndBeat(note, definition) >= startBeat && GetContextualStartBeat(note, definition) <= endBeat)
                yield return note;
        }
    }

    public IEnumerable<ChartEffect> GetEffectsInWindow(double startSongPosition, double endSongPosition)
    {
        double startBeat = TempoMap.SecondsToBeat(startSongPosition);
        double endBeat = TempoMap.SecondsToBeat(endSongPosition);
        if (endBeat < startBeat)
            (startBeat, endBeat) = (endBeat, startBeat);

        foreach (ChartEffect effect in Chart.Effects)
        {
            double beat = GetEffectBeat(effect);
            if (beat >= startBeat && beat <= endBeat)
                yield return effect;
        }
    }

    public double GetBpmAt(double songPosition)
    {
        return TempoMap.GetBpmAtSeconds(songPosition);
    }

    public double GetBpmAtBeat(double beat)
    {
        return TempoMap.GetBpmAtBeat(beat);
    }

    public double GetCrotchetAt(double songPosition)
    {
        return TempoMap.GetCrotchetAt(songPosition);
    }

    public double GetSecondsPerBeatAtBeat(double beat)
    {
        return TempoMap.GetSecondsPerBeatAtBeat(beat);
    }

    public double GetTempoAnchorAt(double songPosition)
    {
        return TempoMap.GetTempoAnchorAt(songPosition);
    }

    public double GetBeatAt(double songPosition)
    {
        return TempoMap.SecondsToBeat(songPosition);
    }

    public double GetSongPositionAtBeat(double beat)
    {
        return TempoMap.BeatToSeconds(beat);
    }

    public double GetNoteBeat(ChartNote note)
    {
        return ChartTiming.GetNoteBeat(note, TempoMap);
    }

    public double GetEffectBeat(ChartEffect effect)
    {
        return ChartTiming.GetEffectBeat(effect, TempoMap);
    }

    public double GetNoteSeconds(ChartNote note)
    {
        return TempoMap.BeatToSeconds(GetNoteBeat(note));
    }

    public double GetEffectSeconds(ChartEffect effect)
    {
        return TempoMap.BeatToSeconds(GetEffectBeat(effect));
    }

    public IEnumerable<EditorTempoSegment> GetTempoSegments(double startSongPosition, double endSongPosition)
    {
        return TempoMap.GetTempoSegments(startSongPosition, endSongPosition);
    }

    public IEnumerable<EditorBeatTempoSegment> GetTempoSegmentsByBeat(double startBeat, double endBeat)
    {
        return TempoMap.GetTempoSegmentsByBeat(startBeat, endBeat);
    }

    public void SetBpm(double bpm)
    {
        if (bpm <= 0)
            return;

        Chart.BPM = bpm;
        SynchronizeAllDerivedTiming();
        IsDirty = true;
    }

    public void SetOffset(double offset)
    {
        Chart.Offset = offset;
        SynchronizeAllDerivedTiming();
        IsDirty = true;
    }

    public void SetLeadInBeats(double leadInBeats)
    {
        Chart.LeadInBeats = Math.Max(0.0, leadInBeats);
        SynchronizeAllDerivedTiming();
        IsDirty = true;
    }

    public void SetSongPath(string songPath)
    {
        SetSongPath(songPath, allowEmpty: false);
    }

    public void SetSongPath(string songPath, bool allowEmpty)
    {
        if (string.IsNullOrWhiteSpace(songPath))
        {
            if (!allowEmpty)
                return;

            songPath = string.Empty;
        }

        SongPath = songPath;
        Chart.SongPath = songPath;

        if (!string.IsNullOrWhiteSpace(songPath) && (string.IsNullOrWhiteSpace(Chart.MusicName) || Chart.MusicName == "Unknown"))
            Chart.MusicName = Path.GetFileNameWithoutExtension(songPath);

        IsDirty = true;
    }

    public void SetChartPath(string chartPath)
    {
        SetChartPath(chartPath, markDirty: true);
    }

    public void SetChartPath(string chartPath, bool markDirty)
    {
        if (string.IsNullOrWhiteSpace(chartPath))
            return;

        ChartPath = BeatmapPackagePaths.ResolveChartPath(chartPath);
        PackagePath = BeatmapPackagePaths.GetPackagePath(ChartPath);
        IsLegacyChart = BeatmapPackagePaths.IsLegacyXmlChartPath(ChartPath);

        if (string.IsNullOrWhiteSpace(Chart.BeatmapName) || Chart.BeatmapName == "Unknown")
            Chart.BeatmapName = GetDefaultBeatmapName(ChartPath);

        if (markDirty)
            IsDirty = true;
    }

    public void SetMetadata(string beatmapName = null, string beatmapper = null, string artistName = null, string musicName = null)
    {
        if (beatmapName != null)
            Chart.BeatmapName = beatmapName;

        if (beatmapper != null)
            Chart.Beatmapper = beatmapper;

        if (artistName != null)
            Chart.ArtistName = artistName;

        if (musicName != null)
            Chart.MusicName = musicName;

        Chart.SongName = Chart.MusicName;
        IsDirty = true;
    }

    public string GetMetadataField(EditorMetadataField field)
    {
        return field switch
        {
            EditorMetadataField.BeatmapName => Chart.BeatmapName,
            EditorMetadataField.Beatmapper => Chart.Beatmapper,
            EditorMetadataField.Description => Chart.Description,
            EditorMetadataField.Tags => string.Join(", ", Chart.Tags ?? new List<string>()),
            EditorMetadataField.SongName => Chart.SongName,
            EditorMetadataField.ArtistName => Chart.ArtistName,
            EditorMetadataField.MusicName => Chart.MusicName,
            EditorMetadataField.LevelIconPath => Chart.LevelIconPath,
            EditorMetadataField.RatingHeader => Chart.RatingHeader,
            EditorMetadataField.RatingTryAgainMessage => Chart.RatingTryAgainMessage,
            EditorMetadataField.RatingTryAgainImagePath => Chart.RatingTryAgainImagePath,
            EditorMetadataField.RatingOkMessage => Chart.RatingOkMessage,
            EditorMetadataField.RatingOkImagePath => Chart.RatingOkImagePath,
            EditorMetadataField.RatingSuperbMessage => Chart.RatingSuperbMessage,
            EditorMetadataField.RatingSuperbImagePath => Chart.RatingSuperbImagePath,
            _ => string.Empty
        } ?? string.Empty;
    }

    public void SetMetadataField(EditorMetadataField field, string value)
    {
        value ??= string.Empty;
        switch (field)
        {
            case EditorMetadataField.BeatmapName:
                Chart.BeatmapName = value;
                break;
            case EditorMetadataField.Beatmapper:
                Chart.Beatmapper = value;
                break;
            case EditorMetadataField.Description:
                Chart.Description = value;
                break;
            case EditorMetadataField.Tags:
                Chart.Tags = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
                break;
            case EditorMetadataField.SongName:
                Chart.SongName = value;
                break;
            case EditorMetadataField.ArtistName:
                Chart.ArtistName = value;
                break;
            case EditorMetadataField.MusicName:
                Chart.MusicName = value;
                Chart.SongName = value;
                break;
            case EditorMetadataField.LevelIconPath:
                Chart.LevelIconPath = BeatmapPackagePaths.NormalizeRelativePath(value);
                break;
            case EditorMetadataField.RatingHeader:
                Chart.RatingHeader = value;
                break;
            case EditorMetadataField.RatingTryAgainMessage:
                Chart.RatingTryAgainMessage = value;
                break;
            case EditorMetadataField.RatingTryAgainImagePath:
                Chart.RatingTryAgainImagePath = BeatmapPackagePaths.NormalizeRelativePath(value);
                break;
            case EditorMetadataField.RatingOkMessage:
                Chart.RatingOkMessage = value;
                break;
            case EditorMetadataField.RatingOkImagePath:
                Chart.RatingOkImagePath = BeatmapPackagePaths.NormalizeRelativePath(value);
                break;
            case EditorMetadataField.RatingSuperbMessage:
                Chart.RatingSuperbMessage = value;
                break;
            case EditorMetadataField.RatingSuperbImagePath:
                Chart.RatingSuperbImagePath = BeatmapPackagePaths.NormalizeRelativePath(value);
                break;
        }

        IsDirty = true;
    }

    public bool GetFlashingEffectsWarning()
    {
        return Chart.FlashingEffectsWarning == true;
    }

    public void SetFlashingEffectsWarning(bool value)
    {
        Chart.FlashingEffectsWarning = value;
        IsDirty = true;
    }

    public string ImportAsset(string sourcePath, string targetSubfolder = null)
    {
        EnsurePackagePathForSave();
        EnsurePackageDirectoriesIfNeeded();
        return new EditorAssetManager(PackagePath).ImportImage(sourcePath, targetSubfolder);
    }

    public bool AddEditorClip(ChartEditorClip clip, out string reason)
    {
        reason = null;
        if (clip == null)
        {
            reason = "Invalid clip";
            return false;
        }

        NormalizeClip(clip);
        Chart.EditorClips.Add(clip);
        Chart.EditorClipsSpecified = true;
        _editorClipsAreRuntimeSource = true;
        if (!TryCompileClipsToRuntimeNotes(out reason))
        {
            Chart.EditorClips.Remove(clip);
            _editorClipsAreRuntimeSource = Chart.EditorClips.Count > 0;
            return false;
        }

        IsDirty = true;
        return true;
    }

    public ChartEditorClip RemoveEditorClip(string clipId)
    {
        ChartEditorClip clip = FindEditorClip(clipId);
        if (clip == null)
            return null;

        Chart.EditorClips.Remove(clip);
        Chart.EditorClipsSpecified = true;
        _editorClipsAreRuntimeSource = true;
        CompileClipsToRuntimeNotes();
        IsDirty = true;
        return clip;
    }

    public bool MoveEditorClip(string clipId, double startBeat, int trackIndex, out string reason)
    {
        ChartEditorClip clip = FindEditorClip(clipId);
        if (clip == null)
        {
            reason = "Clip not found";
            return false;
        }

        double oldBeat = clip.StartBeat;
        int oldTrack = clip.TrackIndex;
        clip.StartBeat = startBeat;
        clip.TrackIndex = Math.Max(0, trackIndex);
        _editorClipsAreRuntimeSource = true;
        if (!TryCompileClipsToRuntimeNotes(out reason))
        {
            clip.StartBeat = oldBeat;
            clip.TrackIndex = oldTrack;
            CompileClipsToRuntimeNotes();
            return false;
        }

        IsDirty = true;
        return true;
    }

    public bool ResizeEditorClip(string clipId, double lengthBeats, out string reason)
    {
        ChartEditorClip clip = FindEditorClip(clipId);
        if (clip == null)
        {
            reason = "Clip not found";
            return false;
        }

        double oldLength = clip.LengthBeats;
        clip.LengthBeats = Math.Max(0.0, lengthBeats);
        _editorClipsAreRuntimeSource = true;
        if (!TryCompileClipsToRuntimeNotes(out reason))
        {
            clip.LengthBeats = oldLength;
            CompileClipsToRuntimeNotes();
            return false;
        }

        IsDirty = true;
        return true;
    }

    public bool ChangeEditorClipData(string clipId, IDictionary<string, string> data, out string reason)
    {
        ChartEditorClip clip = FindEditorClip(clipId);
        if (clip == null)
        {
            reason = "Clip not found";
            return false;
        }

        Dictionary<string, string> oldData = clip.Data;
        clip.Data = data == null ? new Dictionary<string, string>() : new Dictionary<string, string>(data);
        _editorClipsAreRuntimeSource = true;
        if (!TryCompileClipsToRuntimeNotes(out reason))
        {
            clip.Data = oldData;
            CompileClipsToRuntimeNotes();
            return false;
        }

        IsDirty = true;
        return true;
    }

    public ChartEditorClip FindEditorClip(string clipId)
    {
        return Chart.EditorClips?.FirstOrDefault(clip => string.Equals(clip?.Id, clipId, StringComparison.Ordinal));
    }

    public void CompileClipsToRuntimeNotes()
    {
        if (!TryCompileClipsToRuntimeNotes(out string reason))
            throw new InvalidOperationException(reason ?? "Unable to compile editor clips");
    }

    public bool TryCompileClipsToRuntimeNotes(out string reason)
    {
        reason = null;
        Chart.EditorClips ??= new List<ChartEditorClip>();
        List<ChartNote> oldNotes = Chart.Notes;
        List<ChartNote> generatedNotes = EditorClipCompiler.Compile(Chart, TempoMap);

        if (!TryValidateNotes(GetProvidersForNotes(generatedNotes), generatedNotes, generatedNotes, out reason))
        {
            Chart.Notes = oldNotes;
            return false;
        }

        Chart.Notes = generatedNotes;
        SynchronizeAllDerivedTiming();
        SortNotes();
        return true;
    }

    private bool TryValidateNotes(IReadOnlyList<IEditorNoteProvider> providers, IReadOnlyList<ChartNote> notes, IReadOnlyList<ChartNote> changedNotes, out string reason)
    {
        reason = null;
        if (providers == null || providers.Count == 0)
            return true;

        EditorNoteValidationContext context = new(Chart, notes, changedNotes, TempoMap, GetNoteBeat);
        foreach (IEditorNoteProvider provider in providers)
        {
            if (provider != null && !provider.TryValidateNotes(context, out reason))
                return false;
        }

        return true;
    }

    private static IReadOnlyList<IEditorNoteProvider> GetProvidersForPlacements(IReadOnlyList<EditorNotePlacement> placements)
    {
        List<IEditorNoteProvider> providers = new();
        foreach (EditorNotePlacement placement in placements ?? Array.Empty<EditorNotePlacement>())
        {
            if (placement?.Definition != null
                && EditorNoteDefinitions.TryGetProvider(placement.Definition.TypeId, out IEditorNoteProvider provider)
                && !providers.Contains(provider))
            {
                providers.Add(provider);
            }
        }

        return providers;
    }

    private static IReadOnlyList<IEditorNoteProvider> GetProvidersForNotes(IReadOnlyList<ChartNote> notes)
    {
        List<IEditorNoteProvider> providers = new();
        foreach (ChartNote note in notes ?? Array.Empty<ChartNote>())
        {
            EditorNoteDefinition definition = EditorNoteDefinitions.FromChartNote(note);
            if (definition != null
                && EditorNoteDefinitions.TryGetProvider(definition.TypeId, out IEditorNoteProvider provider)
                && !providers.Contains(provider))
            {
                providers.Add(provider);
            }
        }

        return providers;
    }

    public double GetContextualHitWindowEnd(ChartNote note, EditorNoteDefinition definition)
    {
        return TempoMap.BeatToSeconds(GetContextualHitWindowEndBeat(note, definition));
    }

    public double GetContextualHitWindowEndBeat(ChartNote note, EditorNoteDefinition definition)
    {
        return GetNoteTiming(note, definition).HitEndBeat;
    }

    public double GetContextualSameVariantHitWindowEnd(ChartNote note, EditorNoteDefinition definition)
    {
        return TempoMap.BeatToSeconds(GetContextualSameVariantHitWindowEndBeat(note, definition));
    }

    public double GetContextualSameVariantHitWindowEndBeat(ChartNote note, EditorNoteDefinition definition)
    {
        return GetNoteTiming(note, definition).SameVariantHitEndBeat;
    }

    public double GetContextualStart(ChartNote note, EditorNoteDefinition definition)
    {
        return TempoMap.BeatToSeconds(GetContextualStartBeat(note, definition));
    }

    public double GetContextualStartBeat(ChartNote note, EditorNoteDefinition definition)
    {
        return GetNoteTiming(note, definition).StartBeat;
    }

    public double GetBlockingStart(ChartNote note, EditorNoteDefinition definition)
    {
        return TempoMap.BeatToSeconds(GetBlockingStartBeat(note, definition));
    }

    public double GetContextualHitWindowStart(ChartNote note, EditorNoteDefinition definition)
    {
        return TempoMap.BeatToSeconds(GetContextualHitWindowStartBeat(note, definition));
    }

    public double GetContextualHitWindowStartBeat(ChartNote note, EditorNoteDefinition definition)
    {
        return GetNoteTiming(note, definition).HitStartBeat;
    }

    public double GetContextualSameVariantHitWindowStart(ChartNote note, EditorNoteDefinition definition)
    {
        return TempoMap.BeatToSeconds(GetContextualSameVariantHitWindowStartBeat(note, definition));
    }

    public double GetContextualSameVariantHitWindowStartBeat(ChartNote note, EditorNoteDefinition definition)
    {
        return GetNoteTiming(note, definition).SameVariantHitStartBeat;
    }

    public double GetContextualEnd(ChartNote note, EditorNoteDefinition definition)
    {
        return TempoMap.BeatToSeconds(GetContextualEndBeat(note, definition));
    }

    public double GetContextualEndBeat(ChartNote note, EditorNoteDefinition definition)
    {
        return GetNoteTiming(note, definition).EndBeat;
    }

    public double GetContextualStart(EditorNoteDefinition definition, double songPosition, int variantIndex)
    {
        return TempoMap.BeatToSeconds(GetContextualStartBeat(definition, TempoMap.SecondsToBeat(songPosition), variantIndex));
    }

    public double GetContextualStartBeat(EditorNoteDefinition definition, double beat, int variantIndex)
    {
        if (definition == null)
            return beat;

        double songPosition = TempoMap.BeatToSeconds(beat);
        ChartNote note = definition.CreateChartNote(songPosition, TempoMap.GetSecondsPerBeatAtBeat(beat), variantIndex);
        ChartTiming.SetNoteBeat(note, beat);
        ChartTiming.SetNoteHoldBeats(note, definition.HoldBeats);
        IReadOnlyList<ChartNote> contextualNotes = CreateContextualNotes(new[] { new EditorNotePlacement(definition, note) });
        return GetNoteTiming(note, definition, contextualNotes).StartBeat;
    }

    public NoteTimingResult GetNoteTiming(ChartNote note, EditorNoteDefinition definition, IReadOnlyList<ChartNote> contextualNotes = null)
    {
        if (definition == null)
            return NoteTimingResult.AtBeat(GetNoteBeat(note));

        bool useChartCache = UsesDefaultTimingContext(contextualNotes) && note != null;
        if (useChartCache && _chartNoteTimingCache.TryGetValue(note, out NoteTimingResult cachedTiming))
            return cachedTiming;

        NoteTimingResult timing = definition.GetTiming(CreateNoteTimingRequest(note, definition, contextualNotes));
        if (useChartCache)
            _chartNoteTimingCache[note] = timing;

        return timing;
    }

    private NoteTimingRequest CreateNoteTimingRequest(ChartNote note, EditorNoteDefinition definition, IReadOnlyList<ChartNote> contextualNotes)
    {
        double beat = GetNoteBeat(note);
        int noteVariantIndex = EditorNoteDefinitions.GetNoteVariantIndex(definition, note);
        SplitContextualNotes(note, beat, contextualNotes ?? Chart.Notes, out IReadOnlyList<ChartNote> previousNotes, out IReadOnlyList<ChartNote> nextNotes);
        return new NoteTimingRequest(note, definition, noteVariantIndex, beat, TempoMap, previousNotes, nextNotes, CreateTimingContext(definition));
    }

    private IReadOnlyDictionary<string, object> CreateTimingContext(EditorNoteDefinition definition)
    {
        if (definition == null)
            return new Dictionary<string, object>();

        if (_timingContextCache.TryGetValue(definition.TypeId, out IReadOnlyDictionary<string, object> cachedContext))
            return cachedContext;

        IReadOnlyDictionary<string, object> context = EditorNoteDefinitions.TryGetProvider(definition.TypeId, out IEditorNoteProvider provider)
            ? provider.CreateTimingContext(Chart, TempoMap)
            : new Dictionary<string, object>();
        _timingContextCache[definition.TypeId] = context;
        return context;
    }

    private void SplitContextualNotes(ChartNote note, double beat, IReadOnlyList<ChartNote> contextualNotes, out IReadOnlyList<ChartNote> previousNotes, out IReadOnlyList<ChartNote> nextNotes)
    {
        IReadOnlyList<ChartNote> sortedNotes = SortContextualNotes(contextualNotes);
        int index = IndexOfReference(sortedNotes, note);
        if (index >= 0)
        {
            previousNotes = sortedNotes.Take(index).ToArray();
            nextNotes = sortedNotes.Skip(index + 1).ToArray();
            return;
        }

        previousNotes = sortedNotes.Where(item => GetNoteBeat(item) < beat).ToArray();
        nextNotes = sortedNotes.Where(item => GetNoteBeat(item) >= beat).ToArray();
    }

    private IReadOnlyList<ChartNote> SortContextualNotes(IReadOnlyList<ChartNote> notes)
    {
        if (ReferenceEquals(notes, Chart?.Notes))
            return _sortedChartNotesCache ??= Chart.Notes
                .Where(note => note != null)
                .OrderBy(GetNoteBeat)
                .ToArray();

        return (notes ?? Array.Empty<ChartNote>())
            .Where(note => note != null)
            .OrderBy(GetNoteBeat)
            .ToArray();
    }

    private bool UsesDefaultTimingContext(IReadOnlyList<ChartNote> contextualNotes)
    {
        return contextualNotes == null || ReferenceEquals(contextualNotes, Chart?.Notes);
    }

    private ChartNote FindBlockingNote(EditorNotePlacement placed, IReadOnlyList<ChartNote> contextualNotes)
    {
        foreach (ChartNote note in Chart.Notes)
        {
            EditorNoteDefinition existingDefinition = EditorNoteDefinitions.FromChartNote(note);
            bool sameVariantWindow = AreSameDefinitionAndVariant(placed.Definition, placed.Note, existingDefinition, note);
            double placedStart = GetBlockingStartBeat(placed.Definition, placed.Note, sameVariantWindow, contextualNotes);
            double placedEnd = GetBlockingEndBeat(placed.Definition, placed.Note, sameVariantWindow, contextualNotes);
            double existingStart = GetBlockingStartBeat(existingDefinition, note, sameVariantWindow, contextualNotes);
            double existingEnd = GetBlockingEndBeat(existingDefinition, note, sameVariantWindow, contextualNotes);

            if (TouchesAllowedBoundary(placed.Definition, existingDefinition, placedStart, placedEnd, existingStart, existingEnd))
                continue;

            if (placedStart < existingEnd - BeatEpsilon && placedEnd > existingStart + BeatEpsilon)
                return note;
        }

        return null;
    }

    private bool PlacementsBlockEachOther(EditorNotePlacement placed, EditorNotePlacement existing, IReadOnlyList<ChartNote> contextualNotes)
    {
        bool sameVariantWindow = AreSameDefinitionAndVariant(placed.Definition, placed.Note, existing.Definition, existing.Note);
        double placedStart = GetBlockingStartBeat(placed.Definition, placed.Note, sameVariantWindow, contextualNotes);
        double placedEnd = GetBlockingEndBeat(placed.Definition, placed.Note, sameVariantWindow, contextualNotes);
        double existingStart = GetBlockingStartBeat(existing.Definition, existing.Note, sameVariantWindow, contextualNotes);
        double existingEnd = GetBlockingEndBeat(existing.Definition, existing.Note, sameVariantWindow, contextualNotes);

        if (TouchesAllowedBoundary(placed.Definition, existing.Definition, placedStart, placedEnd, existingStart, existingEnd))
            return false;

        return placedStart < existingEnd - BeatEpsilon && placedEnd > existingStart + BeatEpsilon;
    }

    private static bool AreSameDefinitionAndVariant(EditorNoteDefinition aDefinition, ChartNote aNote, EditorNoteDefinition bDefinition, ChartNote bNote)
    {
        if (aDefinition == null || bDefinition == null || aNote == null || bNote == null)
            return false;

        if (!ReferenceEquals(aDefinition, bDefinition) && aDefinition.TypeId != bDefinition.TypeId)
            return false;

        return EditorNoteDefinitions.GetNoteVariantIndex(aDefinition, aNote) == EditorNoteDefinitions.GetNoteVariantIndex(bDefinition, bNote);
    }

    private bool TouchesAllowedBoundary(EditorNoteDefinition placedDefinition, EditorNoteDefinition existingDefinition, double placedStart, double placedEnd, double existingStart, double existingEnd)
    {
        if (placedDefinition == null || existingDefinition == null)
            return false;

        bool allowsBoundaryTouch = EditorNoteDefinitions.TryGetProvider(placedDefinition.TypeId, out IEditorNoteProvider placedProvider)
            && placedProvider.AllowsBoundaryTouch(existingDefinition);
        if (!allowsBoundaryTouch)
            return false;

        return Math.Abs(placedStart - existingEnd) <= BeatEpsilon
            || Math.Abs(existingStart - placedEnd) <= BeatEpsilon;
    }

    private double GetBlockingStart(EditorNoteDefinition definition, ChartNote note)
    {
        return TempoMap.BeatToSeconds(GetBlockingStartBeat(definition, note, sameVariantWindow: false, Chart.Notes));
    }

    private double GetBlockingStart(EditorNoteDefinition definition, ChartNote note, bool sameVariantWindow)
    {
        return TempoMap.BeatToSeconds(GetBlockingStartBeat(definition, note, sameVariantWindow, Chart.Notes));
    }

    private double GetBlockingStartBeat(ChartNote note, EditorNoteDefinition definition)
    {
        return GetBlockingStartBeat(definition, note, sameVariantWindow: false, Chart.Notes);
    }

    private double GetBlockingStartBeat(EditorNoteDefinition definition, ChartNote note, bool sameVariantWindow, IReadOnlyList<ChartNote> contextualNotes)
    {
        NoteTimingResult timing = GetNoteTiming(note, definition, contextualNotes);
        return sameVariantWindow ? timing.SameVariantHitStartBeat : timing.HitStartBeat;
    }

    private double GetBlockingEnd(EditorNoteDefinition definition, ChartNote note)
    {
        return TempoMap.BeatToSeconds(GetBlockingEndBeat(definition, note, sameVariantWindow: false, Chart.Notes));
    }

    private double GetBlockingEnd(EditorNoteDefinition definition, ChartNote note, bool sameVariantWindow)
    {
        return TempoMap.BeatToSeconds(GetBlockingEndBeat(definition, note, sameVariantWindow, Chart.Notes));
    }

    private double GetBlockingEndBeat(EditorNoteDefinition definition, ChartNote note, bool sameVariantWindow, IReadOnlyList<ChartNote> contextualNotes)
    {
        NoteTimingResult timing = GetNoteTiming(note, definition, contextualNotes);
        return sameVariantWindow ? timing.SameVariantHitEndBeat : timing.HitEndBeat;
    }

    private IReadOnlyList<ChartNote> CreateContextualNotes(IReadOnlyList<EditorNotePlacement> placements)
    {
        List<ContextualNoteEntry> entries = new();
        int order = 0;

        foreach (ChartNote note in Chart.Notes)
            entries.Add(new ContextualNoteEntry(note, isGenerated: false, order++));

        foreach (EditorNotePlacement placement in placements)
            entries.Add(new ContextualNoteEntry(placement.Note, isGenerated: true, order++));

        entries.Sort(CompareContextualNoteEntries);
        return entries.Select(entry => entry.Note).ToList();
    }

    private int CompareContextualNoteEntries(ContextualNoteEntry a, ContextualNoteEntry b)
    {
        int byTime = GetNoteBeat(a.Note).CompareTo(GetNoteBeat(b.Note));
        if (byTime != 0)
            return byTime;

        int byGenerated = a.IsGenerated.CompareTo(b.IsGenerated);
        return byGenerated != 0 ? byGenerated : a.Order.CompareTo(b.Order);
    }

    private static int IndexOfReference(IReadOnlyList<ChartNote> notes, ChartNote note)
    {
        if (notes == null || note == null)
            return -1;

        for (int i = 0; i < notes.Count; i++)
        {
            if (ReferenceEquals(notes[i], note))
                return i;
        }

        return -1;
    }

    private readonly struct ContextualNoteEntry
    {
        public ContextualNoteEntry(ChartNote note, bool isGenerated, int order)
        {
            Note = note;
            IsGenerated = isGenerated;
            Order = order;
        }

        public ChartNote Note { get; }
        public bool IsGenerated { get; }
        public int Order { get; }
    }

    private ChartTempoMap CreateTempoMap()
    {
        return TempoMap;
    }

    private double GetLeadInBeats()
    {
        return ChartTiming.GetLeadInBeats(Chart);
    }

    private void InvalidateTempoMap()
    {
        _tempoMap = null;
        InvalidateTimingCaches();
    }

    private void InvalidateTimingCaches()
    {
        _sortedChartNotesCache = null;
        _chartNoteTimingCache.Clear();
        _timingContextCache.Clear();
    }

    private static double GetEffectiveSnapDivisions(double snapDivisions)
    {
        return !double.IsNaN(snapDivisions) && !double.IsInfinity(snapDivisions) && snapDivisions > 0
            ? snapDivisions
            : 1.0;
    }

    private static double QuantizeBeat(double beat, double divisions)
    {
        double step = 1.0 / Math.Max(1.0, divisions);
        return Math.Round(beat / step, MidpointRounding.AwayFromZero) * step;
    }

    private void SynchronizeAllNoteDurations()
    {
        SynchronizeAllDerivedTiming();
    }

    private void SynchronizeAllDerivedTiming()
    {
        if (Chart?.Notes == null)
            return;

        InvalidateTempoMap();

        if (Chart.Effects != null)
        {
            foreach (ChartEffect effect in Chart.Effects)
                SynchronizeEffectSongPosition(effect);
        }

        foreach (ChartNote note in Chart.Notes)
            SynchronizeNoteDuration(note, EditorNoteDefinitions.FromChartNote(note));
    }

    private void SynchronizeNoteDuration(ChartNote note, EditorNoteDefinition definition)
    {
        if (note == null)
            return;

        double noteBeat = ClampNoteBeatToMinimum(ChartTiming.GetNoteBeat(note, TempoMap));
        double holdBeats = ChartTiming.GetNoteHoldBeats(note, definition, TempoMap);
        ChartTiming.SetNoteBeat(note, noteBeat);
        ChartTiming.SetNoteHoldBeats(note, holdBeats);
        note.SongPosition = TempoMap.BeatToSeconds(noteBeat);
        note.HoldDuration = Math.Max(0, TempoMap.BeatToSeconds(noteBeat + holdBeats) - note.SongPosition);
    }

    private void SynchronizeEffectSongPosition(ChartEffect effect)
    {
        if (effect == null)
            return;

        double beat = ChartTiming.GetEffectBeat(effect, TempoMap);
        ChartTiming.SetEffectBeat(effect, beat);
        effect.SongPosition = Math.Max(0, TempoMap.BeatToSeconds(beat));
    }

    private void NormalizeEffect(ChartEffect effect)
    {
        effect.SongPosition = Math.Max(0, effect.SongPosition);
        if (string.IsNullOrWhiteSpace(effect.EffectType))
            effect.EffectType = ChartEffect.BpmChangeEffectType;

        effect.Data ??= new Dictionary<string, string>();
        if (effect.IsBpmChange && !effect.TryGetBpm(out _))
            effect.SetBpm(GetBpmAt(effect.SongPosition));

        if (effect.IsBpmChange && !effect.TryGetSectionOffset(out _))
            effect.SetSectionOffset(0);

        if (!effect.BeatPosition.HasValue)
            ChartTiming.SetEffectBeat(effect, TempoMap.SecondsToBeat(effect.SongPosition));
    }

    private void EnsurePackagePathForSave()
    {
        if (!IsLegacyChart)
        {
            PackagePath = BeatmapPackagePaths.GetPackagePath(ChartPath);
            return;
        }

        if (_needsV1Backup && string.IsNullOrWhiteSpace(_v1BackupSourcePath))
            _v1BackupSourcePath = ChartPath;

        SetChartPath(BeatmapPackagePaths.GetAvailableMigratedChartPath(ChartPath), markDirty: false);
        IsLegacyChart = false;
    }

    private void EnsurePackageDirectoriesIfNeeded()
    {
        if (IsLegacyChart || string.IsNullOrWhiteSpace(PackagePath))
            return;

        Directory.CreateDirectory(PackagePath);
        Directory.CreateDirectory(AssetsPath);
    }

    private void UseRuntimeNotesAsEditorClipSource()
    {
        if (HasEditorAuthoredClips())
        {
            _editorClipsAreRuntimeSource = true;
            TryCompileClipsToRuntimeNotes(out _);
            return;
        }

        SyncEditorClipsFromRuntimeNotes();
        _editorClipsAreRuntimeSource = false;
    }

    private bool HasEditorAuthoredClips()
    {
        return Chart?.EditorClips != null && Chart.EditorClips.Any(IsEditorAuthoredClip);
    }

    private static bool IsEditorAuthoredClip(ChartEditorClip clip)
    {
        return clip != null
            && !string.IsNullOrWhiteSpace(clip.Id)
            && !clip.Id.StartsWith("legacy-", StringComparison.Ordinal);
    }

    private void SyncEditorClipsFromRuntimeNotes()
    {
        Chart.EditorTracks ??= CreateDefaultTracks();
        if (Chart.EditorTracks.Count == 0)
            Chart.EditorTracks = CreateDefaultTracks();

        Chart.EditorTracksSpecified = true;
        Chart.EditorClips = Chart.Notes
            .Where(note => note != null)
            .Select((note, index) => EditorClipCompiler.CreateClipFromLegacyNote(note, GetNoteBeat, index))
            .ToList();
        Chart.EditorClipsSpecified = true;
    }

    private static void NormalizeClip(ChartEditorClip clip)
    {
        clip.Id = string.IsNullOrWhiteSpace(clip.Id) ? Guid.NewGuid().ToString("N") : clip.Id;
        clip.TrackIndex = Math.Max(0, clip.TrackIndex);
        if (double.IsNaN(clip.StartBeat) || double.IsInfinity(clip.StartBeat))
            clip.StartBeat = 0;

        EditorClipDefinition definition = EditorClipDefinitions.Find(clip.RhythmGameId, clip.ClipTypeId);
        if (double.IsNaN(clip.LengthBeats) || double.IsInfinity(clip.LengthBeats) || clip.LengthBeats <= 0)
            clip.LengthBeats = Math.Max(0.0, definition?.DefaultLengthBeats ?? 0.0);

        clip.RhythmGameId = string.IsNullOrWhiteSpace(clip.RhythmGameId) ? definition?.RhythmGameId ?? EditorClipDefinitions.UnknownGameId : clip.RhythmGameId;
        clip.ClipTypeId = string.IsNullOrWhiteSpace(clip.ClipTypeId) ? definition?.ClipTypeId ?? EditorClipDefinitions.NoHit : clip.ClipTypeId;
        clip.ClipCategory = string.IsNullOrWhiteSpace(clip.ClipCategory) ? (definition?.Category ?? EditorClipCategory.SingleHit).ToString() : clip.ClipCategory;
        clip.InputAction = string.IsNullOrWhiteSpace(clip.InputAction) ? definition?.InputAction ?? "ReactMain" : clip.InputAction;
        clip.Data ??= new Dictionary<string, string>();
    }

    private static List<ChartEditorTrack> CreateDefaultTracks()
    {
        List<ChartEditorTrack> tracks = new();
        for (int i = 0; i < 10; i++)
        {
            tracks.Add(new ChartEditorTrack
            {
                Id = $"track-{i + 1}",
                Name = $"Track {i + 1}",
                Index = i
            });
        }

        return tracks;
    }

    private static string GetDefaultBeatmapName(string chartPath)
    {
        if (BeatmapPackagePaths.IsPackageChartPath(chartPath))
            return Path.GetFileName(Path.GetDirectoryName(chartPath));

        return Path.GetFileNameWithoutExtension(chartPath);
    }

    private void NormalizeChart()
    {
        if (Chart == null)
            Chart = new Chart();

        if (string.IsNullOrWhiteSpace(Chart.SongName))
            Chart.SongName = Path.GetFileNameWithoutExtension(SongPath);

        if (string.IsNullOrWhiteSpace(Chart.BeatmapName))
            Chart.BeatmapName = GetDefaultBeatmapName(ChartPath);

        if (string.IsNullOrWhiteSpace(Chart.Beatmapper))
            Chart.Beatmapper = "Unknown";

        if (string.IsNullOrWhiteSpace(Chart.ArtistName))
            Chart.ArtistName = "Unknown";

        if (string.IsNullOrWhiteSpace(Chart.MusicName))
            Chart.MusicName = Chart.SongName;

        if (File.Exists(PackageSongPath))
            Chart.SongPath = PackageSongPath;
        else if (string.IsNullOrWhiteSpace(Chart.SongPath))
            Chart.SongPath = SongPath;

        SongPath = Chart.SongPath;

        if (Chart.BPM <= 0)
            Chart.BPM = 100;

        if (double.IsNaN(Chart.LeadInBeats) || double.IsInfinity(Chart.LeadInBeats) || Chart.LeadInBeats < 0.0)
            Chart.LeadInBeats = 0.0;

        Chart.Notes ??= new List<ChartNote>();
        Chart.Effects ??= new List<ChartEffect>();
        Chart.Tags ??= new List<string>();
        Chart.EditorTracks ??= new List<ChartEditorTrack>();
        Chart.EditorClips ??= new List<ChartEditorClip>();
        Chart.Notes = Chart.Notes.Where(note => note != null).ToList();

        bool migratedFromV1 = Chart.ChartVersion < 2
            || Chart.Notes.Any(note => note?.BeatPosition == null || !note.HoldBeats.HasValue)
            || Chart.Effects.Any(effect => effect?.IsBpmChange == true && !effect.BeatPosition.HasValue);

        ChartTempoMap legacyTempoMap = new(Chart);

        foreach (ChartNote note in Chart.Notes)
        {
            note.InputActionToPress ??= "ReactMain";
            note.AdditionnalData ??= new Dictionary<string, string>();
            EnsureNoteBeat(note, legacyTempoMap);
        }

        Chart.Effects = Chart.Effects.Where(effect => effect != null).ToList();
        foreach (ChartEffect effect in Chart.Effects)
        {
            if (!effect.BeatPosition.HasValue)
                ChartTiming.SetEffectBeat(effect, legacyTempoMap.SecondsToBeat(Math.Max(0, effect.SongPosition)));

            NormalizeEffect(effect);
        }

        bool hasPersistedEditorClips = Chart.EditorClipsSpecified || Chart.EditorClips.Count > 0;
        if (Chart.EditorTracks.Count == 0)
            Chart.EditorTracks = CreateDefaultTracks();

        for (int i = 0; i < Chart.EditorTracks.Count; i++)
        {
            ChartEditorTrack track = Chart.EditorTracks[i];
            track.Id = string.IsNullOrWhiteSpace(track.Id) ? $"track-{i + 1}" : track.Id;
            track.Name = string.IsNullOrWhiteSpace(track.Name) ? $"Track {i + 1}" : track.Name;
            track.Index = i;
        }

        Chart.EditorTracksSpecified = true;

        if (hasPersistedEditorClips)
        {
            Chart.EditorClips = Chart.EditorClips.Where(clip => clip != null).ToList();
            foreach (ChartEditorClip clip in Chart.EditorClips)
                NormalizeClip(clip);

            Chart.EditorClipsSpecified = true;
            _editorClipsAreRuntimeSource = true;
        }
        else
        {
            SyncEditorClipsFromRuntimeNotes();
            _editorClipsAreRuntimeSource = false;
        }

        Chart.ChartVersion = 2;
        _needsV1Backup = migratedFromV1 && !string.IsNullOrWhiteSpace(ChartPath) && File.Exists(ChartPath);

        if (_editorClipsAreRuntimeSource)
            TryCompileClipsToRuntimeNotes(out _);
        else
        {
            SynchronizeAllDerivedTiming();
            SortNotes();
        }

        SortEffects();
    }

    private void EnsureNoteBeat(ChartNote note, ChartTempoMap legacyTempoMap)
    {
        if (note == null)
            return;

        if (!note.BeatPosition.HasValue)
            ChartTiming.SetNoteBeat(note, legacyTempoMap.SecondsToBeat(note.SongPosition));

        if (!note.HoldBeats.HasValue)
        {
            EditorNoteDefinition definition = EditorNoteDefinitions.FromChartNote(note);
            ChartTiming.SetNoteHoldBeats(note, ChartTiming.GetNoteHoldBeats(note, definition, legacyTempoMap));
        }
    }

    private double ClampNoteBeatToSongStart(double beat)
    {
        return ClampNoteBeatToMinimum(beat);
    }

    private double ClampNoteBeatToMinimum(double beat)
    {
        double songStartBeat = TempoMap.SecondsToBeat(0.0);
        if (double.IsNaN(songStartBeat) || double.IsInfinity(songStartBeat))
            songStartBeat = 0.0;

        double leadInStartBeat = -ChartTiming.GetLeadInBeats(Chart);
        return Math.Max(Math.Min(songStartBeat, leadInStartBeat), beat);
    }

    private void SortNotes()
    {
        InvalidateTimingCaches();
        Chart.Notes = Chart.Notes.OrderBy(GetNoteBeat).ToList();
    }

    private void SortEffects()
    {
        Chart.Effects = Chart.Effects.OrderBy(GetEffectBeat).ToList();
    }

    private double SecondsDistanceToBeats(double songPosition, double distanceSeconds)
    {
        if (distanceSeconds <= 0 || double.IsNaN(distanceSeconds) || double.IsInfinity(distanceSeconds))
            return 0.0;

        double centerBeat = TempoMap.SecondsToBeat(songPosition);
        double afterBeat = TempoMap.SecondsToBeat(songPosition + distanceSeconds);
        return Math.Abs(afterBeat - centerBeat);
    }
}

public readonly struct EditorTempoSegment
{
    public EditorTempoSegment(double anchor, double sourceSongPosition, double startSongPosition, double endSongPosition, double bpm, bool anchorIsRelativeToSegmentStart)
    {
        Anchor = anchor;
        SourceSongPosition = sourceSongPosition;
        StartSongPosition = startSongPosition;
        EndSongPosition = endSongPosition;
        Bpm = bpm;
        AnchorIsRelativeToSegmentStart = anchorIsRelativeToSegmentStart;
    }

    public double Anchor { get; }
    public double SourceSongPosition { get; }
    public double StartSongPosition { get; }
    public double EndSongPosition { get; }
    public double Bpm { get; }
    public bool AnchorIsRelativeToSegmentStart { get; }
    public double AnchorSongPosition => AnchorIsRelativeToSegmentStart ? SourceSongPosition + Anchor : Anchor;
    public double Crotchet => Bpm > 0 ? 60.0 / Bpm : 0.6;
}
