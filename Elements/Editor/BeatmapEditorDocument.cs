using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using Rhythm.Note;

namespace MLP_RiM.Elements.Editor;

public sealed class BeatmapEditorDocument
{
    private const double HitWindowEpsilonSeconds = 0.0005;

    public Chart Chart { get; private set; }
    public string SongPath { get; set; }
    public string ChartPath { get; private set; }
    public bool IsDirty { get; private set; }

    public double Crotchet => Chart.BPM > 0 ? 60.0 / Chart.BPM : 0.6;

    private BeatmapEditorDocument(Chart chart, string songPath, string chartPath)
    {
        Chart = chart;
        SongPath = songPath;
        ChartPath = chartPath;
        NormalizeChart();
    }

    public static BeatmapEditorDocument CreateNew(string songPath, string chartPath, double bpm = 100)
    {
        string songName = string.IsNullOrWhiteSpace(songPath) ? "Untitled" : Path.GetFileNameWithoutExtension(songPath);
        return new BeatmapEditorDocument(new Chart
        {
            SongName = songName,
            BeatmapName = Path.GetFileNameWithoutExtension(chartPath),
            Beatmapper = "Unknown",
            ArtistName = "Unknown",
            MusicName = songName,
            SongPath = songPath,
            BPM = bpm,
            Offset = 0.078,
            Notes = new List<ChartNote>(),
            Effects = new List<ChartEffect>()
        }, songPath, chartPath);
    }

    public static BeatmapEditorDocument LoadOrCreate(string songPath, string chartPath, double bpm = 100)
    {
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
            ChartPath = chartPath;

        string directory = Path.GetDirectoryName(ChartPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        SynchronizeAllNoteDurations();
        SortNotes();
        SortEffects();

        string tempPath = ChartPath + ".tmp";
        using (FileStream stream = File.Create(tempPath))
        {
            XmlSerializer serializer = new(typeof(Chart));
            serializer.Serialize(stream, Chart);
        }

        File.Move(tempPath, ChartPath, overwrite: true);
        IsDirty = false;
    }

    public bool TryPlaceNote(EditorNoteDefinition definition, double songPosition, int variantIndex, out ChartNote placedNote, out string reason)
    {
        songPosition = Math.Max(0, songPosition);
        return TryPlaceNote(definition, definition.CreateChartNote(songPosition, GetCrotchetAt(songPosition), variantIndex), out placedNote, out reason);
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
            note.SongPosition = Math.Max(0, note.SongPosition);
            note.InputActionToPress ??= placement.Definition.InputAction;
            note.AdditionnalData ??= new Dictionary<string, string>();
            SynchronizeNoteDuration(note, placement.Definition);
            normalizedPlacements.Add(new EditorNotePlacement(placement.Definition, note));
        }

        IReadOnlyList<ChartNote> contextualNotes = CreateContextualNotes(normalizedPlacements);
        for (int i = 0; i < normalizedPlacements.Count; i++)
        {
            EditorNotePlacement placement = normalizedPlacements[i];

            if (FindBlockingNote(placement, contextualNotes) is ChartNote blocker)
            {
                EditorNoteDefinition blockerDefinition = EditorNoteDefinitions.FromChartNote(blocker);
                reason = $"Blocked by {blockerDefinition?.DisplayName ?? "note"} at {blocker.SongPosition:0.000}s";
                return false;
            }

            for (int j = 0; j < i; j++)
            {
                if (PlacementsBlockEachOther(placement, normalizedPlacements[j], contextualNotes))
                {
                    reason = $"Generated notes overlap at {placement.Note.SongPosition:0.000}s";
                    return false;
                }
            }
        }

        if (normalizedPlacements.Any(placement => placement.Definition.Kind == EditorNoteKind.SeeSaw))
        {
            ChartNote firstSeeSawNote = normalizedPlacements.First(placement => placement.Definition.Kind == EditorNoteKind.SeeSaw).Note;
            SeeSawTimeline previewTimeline = SeeSawChartCompiler.CompileContextualChartNotes(contextualNotes, GetBeatAt, GetSongPositionAtBeat);
            if (previewTimeline.Errors.Count > 0)
            {
                reason = previewTimeline.Errors[0];
                return false;
            }
        }

        List<ChartNote> notes = normalizedPlacements.Select(placement => placement.Note).ToList();
        Chart.Notes.AddRange(notes);
        SynchronizeAllNoteDurations();
        SortNotes();
        IsDirty = true;
        placedNotes = notes;
        reason = null;
        return true;
    }

    public bool DeleteNearest(double songPosition, double maxDistanceSeconds, out ChartNote deletedNote)
    {
        deletedNote = FindNearest(songPosition, maxDistanceSeconds);
        if (deletedNote == null)
            return false;

        Chart.Notes.Remove(deletedNote);
        IsDirty = true;
        return true;
    }

    public bool MoveNote(ChartNote note, double songPosition)
    {
        if (note == null || !Chart.Notes.Contains(note))
            return false;

        note.SongPosition = Math.Max(0, songPosition);
        SynchronizeNoteDuration(note, EditorNoteDefinitions.FromChartNote(note));
        SortNotes();
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
        SynchronizeAllNoteDurations();
        SortEffects();
        IsDirty = true;
        placedEffect = effect;
        reason = null;
        return true;
    }

    public bool DeleteNearestEffect(double songPosition, double maxDistanceSeconds, out ChartEffect deletedEffect)
    {
        deletedEffect = FindNearestEffect(songPosition, maxDistanceSeconds);
        if (deletedEffect == null)
            return false;

        Chart.Effects.Remove(deletedEffect);
        SynchronizeAllNoteDurations();
        IsDirty = true;
        return true;
    }

    public bool MoveEffect(ChartEffect effect, double songPosition, bool sectionOffsetFollowsPosition)
    {
        if (effect == null || !Chart.Effects.Contains(effect))
            return false;

        effect.SongPosition = Math.Max(0, songPosition);
        if (sectionOffsetFollowsPosition)
            effect.SetSectionOffset(0);

        SynchronizeAllNoteDurations();
        SortEffects();
        IsDirty = true;
        return true;
    }

    public int NormalizeSeeSawNotesToGrid(double snapDivisions)
    {
        double divisions = GetEffectiveSnapDivisions(snapDivisions);
        ChartTempoMap tempoMap = CreateTempoMap();
        int normalizedCount = 0;

        foreach (ChartNote note in Chart.Notes)
        {
            EditorNoteDefinition definition = EditorNoteDefinitions.FromChartNote(note);
            if (definition?.Kind != EditorNoteKind.SeeSaw)
                continue;

            double beat = tempoMap.GetBeatAt(note.SongPosition);
            if (double.IsNaN(beat) || double.IsInfinity(beat))
                continue;

            double snappedBeat = QuantizeBeat(beat, divisions);
            double snappedSongPosition = Math.Max(0, tempoMap.GetSongPositionAtBeat(snappedBeat));
            if (Math.Abs(note.SongPosition - snappedSongPosition) <= 0.000000001)
                continue;

            note.SongPosition = snappedSongPosition;
            normalizedCount++;
        }

        if (normalizedCount > 0)
        {
            SynchronizeAllNoteDurations();
            SortNotes();
            IsDirty = true;
        }

        return normalizedCount;
    }

    public int NormalizeBpmChangesToNearestGlobalBeat()
    {
        int normalizedCount = 0;
        foreach (ChartEffect effect in Chart.Effects.Where(effect => effect?.IsBpmChange == true).ToList())
        {
            ChartTempoMap tempoMap = CreateTempoMap();
            double beat = tempoMap.GetBeatAt(effect.SongPosition);
            if (double.IsNaN(beat) || double.IsInfinity(beat))
                continue;

            double snappedBeat = Math.Round(beat, MidpointRounding.AwayFromZero);
            double snappedSongPosition = Math.Max(0, tempoMap.GetSongPositionAtBeat(snappedBeat));
            if (Math.Abs(effect.SongPosition - snappedSongPosition) <= 0.000000001)
                continue;

            effect.SongPosition = snappedSongPosition;
            effect.SetSectionOffset(0);
            normalizedCount++;
            SortEffects();
        }

        if (normalizedCount > 0)
        {
            SynchronizeAllNoteDurations();
            SortEffects();
            IsDirty = true;
        }

        return normalizedCount;
    }

    public void MarkDirty()
    {
        SynchronizeAllNoteDurations();
        IsDirty = true;
    }

    public ChartNote FindNearest(double songPosition, double maxDistanceSeconds)
    {
        return Chart.Notes
            .Select(note => new { Note = note, Distance = Math.Abs(note.SongPosition - songPosition) })
            .Where(item => item.Distance <= maxDistanceSeconds)
            .OrderBy(item => item.Distance)
            .Select(item => item.Note)
            .FirstOrDefault();
    }

    public ChartEffect FindNearestEffect(double songPosition, double maxDistanceSeconds)
    {
        return Chart.Effects
            .Select(effect => new { Effect = effect, Distance = Math.Abs(effect.SongPosition - songPosition) })
            .Where(item => item.Distance <= maxDistanceSeconds)
            .OrderBy(item => item.Distance)
            .Select(item => item.Effect)
            .FirstOrDefault();
    }

    public bool IsOccupiedAt(double songPosition)
    {
        foreach (ChartNote note in Chart.Notes)
        {
            EditorNoteDefinition definition = EditorNoteDefinitions.FromChartNote(note);
            double start = GetContextualStart(note, definition);
            double end = GetContextualEnd(note, definition);
            if (songPosition >= start && songPosition <= end)
                return true;
        }

        return false;
    }

    public IEnumerable<ChartNote> GetNotesInWindow(double startSongPosition, double endSongPosition)
    {
        foreach (ChartNote note in Chart.Notes)
        {
            EditorNoteDefinition definition = EditorNoteDefinitions.FromChartNote(note);
            if (GetContextualHitWindowEnd(note, definition) >= startSongPosition && GetContextualStart(note, definition) <= endSongPosition)
                yield return note;
        }
    }

    public IEnumerable<ChartEffect> GetEffectsInWindow(double startSongPosition, double endSongPosition)
    {
        foreach (ChartEffect effect in Chart.Effects)
        {
            if (effect.SongPosition >= startSongPosition && effect.SongPosition <= endSongPosition)
                yield return effect;
        }
    }

    public double GetBpmAt(double songPosition)
    {
        return CreateTempoMap().GetBpmAt(songPosition);
    }

    public double GetCrotchetAt(double songPosition)
    {
        return CreateTempoMap().GetCrotchetAt(songPosition);
    }

    public double GetTempoAnchorAt(double songPosition)
    {
        return CreateTempoMap().GetTempoAnchorAt(songPosition);
    }

    public double GetBeatAt(double songPosition)
    {
        return CreateTempoMap().GetBeatAt(songPosition);
    }

    public double GetSongPositionAtBeat(double beat)
    {
        return CreateTempoMap().GetSongPositionAtBeat(beat);
    }

    public IEnumerable<EditorTempoSegment> GetTempoSegments(double startSongPosition, double endSongPosition)
    {
        return CreateTempoMap().GetTempoSegments(startSongPosition, endSongPosition);
    }

    public void SetBpm(double bpm)
    {
        if (bpm <= 0)
            return;

        Chart.BPM = bpm;
        SynchronizeAllNoteDurations();
        IsDirty = true;
    }

    public void SetOffset(double offset)
    {
        Chart.Offset = offset;
        IsDirty = true;
    }

    public void SetSongPath(string songPath)
    {
        if (string.IsNullOrWhiteSpace(songPath))
            return;

        SongPath = songPath;
        Chart.SongPath = songPath;

        if (string.IsNullOrWhiteSpace(Chart.MusicName) || Chart.MusicName == "Unknown")
            Chart.MusicName = Path.GetFileNameWithoutExtension(songPath);

        IsDirty = true;
    }

    public void SetChartPath(string chartPath)
    {
        if (string.IsNullOrWhiteSpace(chartPath))
            return;

        ChartPath = chartPath;

        if (string.IsNullOrWhiteSpace(Chart.BeatmapName) || Chart.BeatmapName == "Unknown")
            Chart.BeatmapName = Path.GetFileNameWithoutExtension(chartPath);

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

    public double GetContextualHitWindowEnd(ChartNote note, EditorNoteDefinition definition)
    {
        if (definition == null)
            return note.SongPosition;

        Func<int, ChartNote> getRelativeNote = CreateRelativeNoteGetter(note);
        int variantIndex = EditorNoteDefinitions.FindVariantIndex(definition, note);
        double crotchet = GetCrotchetAt(note.SongPosition);

        if (definition.Kind != EditorNoteKind.SeeSaw)
            return definition.GetHitWindowEnd(note.SongPosition, crotchet, variantIndex, getRelativeNote);

        return GetSongPositionAtBeat(GetSeeSawTiming(note).EndBeat);
    }

    public double GetContextualSameVariantHitWindowEnd(ChartNote note, EditorNoteDefinition definition)
    {
        if (definition == null)
            return note.SongPosition;

        Func<int, ChartNote> getRelativeNote = CreateRelativeNoteGetter(note);
        int variantIndex = EditorNoteDefinitions.FindVariantIndex(definition, note);
        double crotchet = GetCrotchetAt(note.SongPosition);

        if (definition.Kind != EditorNoteKind.SeeSaw)
            return definition.GetSameVariantHitWindowEnd(note.SongPosition, crotchet, variantIndex, getRelativeNote);

        return GetSongPositionAtBeat(GetSeeSawTiming(note).EndBeat);
    }

    public double GetContextualStart(ChartNote note, EditorNoteDefinition definition)
    {
        if (definition == null)
            return note.SongPosition;

        Func<int, ChartNote> getRelativeNote = CreateRelativeNoteGetter(note);
        int variantIndex = EditorNoteDefinitions.FindVariantIndex(definition, note);
        double crotchet = GetCrotchetAt(note.SongPosition);

        if (definition.Kind != EditorNoteKind.SeeSaw)
            return definition.GetStart(note.SongPosition, crotchet, variantIndex, getRelativeNote, beforeUsesOuterTiming: false);

        return GetSongPositionAtBeat(GetSeeSawTiming(note).PrepStartBeat);
    }

    public double GetBlockingStart(ChartNote note, EditorNoteDefinition definition)
    {
        if (definition == null)
            return note.SongPosition;

        if (definition.Kind == EditorNoteKind.SeeSaw)
            return GetContextualStart(note, definition);

        return GetBlockingStart(definition, note);
    }

    public double GetContextualHitWindowStart(ChartNote note, EditorNoteDefinition definition)
    {
        if (definition == null)
            return note.SongPosition;

        Func<int, ChartNote> getRelativeNote = CreateRelativeNoteGetter(note);
        int variantIndex = EditorNoteDefinitions.FindVariantIndex(definition, note);
        double crotchet = GetCrotchetAt(note.SongPosition);

        if (definition.Kind != EditorNoteKind.SeeSaw)
            return definition.GetHitWindowStart(note.SongPosition, crotchet, variantIndex, getRelativeNote, beforeUsesOuterTiming: false);

        return GetSongPositionAtBeat(GetSeeSawTiming(note).PrepStartBeat);
    }

    public double GetContextualSameVariantHitWindowStart(ChartNote note, EditorNoteDefinition definition)
    {
        if (definition == null)
            return note.SongPosition;

        Func<int, ChartNote> getRelativeNote = CreateRelativeNoteGetter(note);
        int variantIndex = EditorNoteDefinitions.FindVariantIndex(definition, note);
        double crotchet = GetCrotchetAt(note.SongPosition);

        if (definition.Kind != EditorNoteKind.SeeSaw)
            return definition.GetSameVariantHitWindowStart(note.SongPosition, crotchet, variantIndex, getRelativeNote, beforeUsesOuterTiming: false);

        return GetSongPositionAtBeat(GetSeeSawTiming(note).PrepStartBeat);
    }

    public double GetContextualEnd(ChartNote note, EditorNoteDefinition definition)
    {
        if (definition == null)
            return note.SongPosition;

        if (definition.Kind == EditorNoteKind.SeeSaw)
            return GetSongPositionAtBeat(GetSeeSawTiming(note).EndBeat);

        return definition.GetEnd(note.SongPosition, GetCrotchetAt(note.SongPosition), EditorNoteDefinitions.FindVariantIndex(definition, note), CreateRelativeNoteGetter(note));
    }

    public double GetContextualStart(EditorNoteDefinition definition, double songPosition, int variantIndex)
    {
        if (definition == null)
            return songPosition;

        if (definition.Kind != EditorNoteKind.SeeSaw)
            return definition.GetStart(songPosition, GetCrotchetAt(songPosition));

        EditorNoteVariant variant = definition.GetVariant(variantIndex);
        return GetSongPositionAtBeat(SeeSawChartCompiler.GetPreviewTiming(Chart.Notes, variant.AdditionnalData, songPosition, GetBeatAt).PrepStartBeat);
    }

    private ChartNote FindBlockingNote(EditorNotePlacement placed, IReadOnlyList<ChartNote> contextualNotes)
    {
        foreach (ChartNote note in Chart.Notes)
        {
            EditorNoteDefinition existingDefinition = EditorNoteDefinitions.FromChartNote(note);
            bool sameVariantWindow = AreSameDefinitionAndVariant(placed.Definition, placed.Note, existingDefinition, note);
            double placedStart = GetBlockingStart(placed.Definition, placed.Note, sameVariantWindow, contextualNotes);
            double placedEnd = GetBlockingEnd(placed.Definition, placed.Note, sameVariantWindow, contextualNotes);
            double existingStart = GetBlockingStart(existingDefinition, note, sameVariantWindow, contextualNotes);
            double existingEnd = GetBlockingEnd(existingDefinition, note, sameVariantWindow, contextualNotes);

            if (TouchesAllowedBoundary(placed.Definition, existingDefinition, placedStart, placedEnd, existingStart, existingEnd))
                continue;

            if (placedStart < existingEnd - HitWindowEpsilonSeconds && placedEnd > existingStart + HitWindowEpsilonSeconds)
                return note;
        }

        return null;
    }

    private bool PlacementsBlockEachOther(EditorNotePlacement placed, EditorNotePlacement existing, IReadOnlyList<ChartNote> contextualNotes)
    {
        bool sameVariantWindow = AreSameDefinitionAndVariant(placed.Definition, placed.Note, existing.Definition, existing.Note);
        double placedStart = GetBlockingStart(placed.Definition, placed.Note, sameVariantWindow, contextualNotes);
        double placedEnd = GetBlockingEnd(placed.Definition, placed.Note, sameVariantWindow, contextualNotes);
        double existingStart = GetBlockingStart(existing.Definition, existing.Note, sameVariantWindow, contextualNotes);
        double existingEnd = GetBlockingEnd(existing.Definition, existing.Note, sameVariantWindow, contextualNotes);

        if (TouchesAllowedBoundary(placed.Definition, existing.Definition, placedStart, placedEnd, existingStart, existingEnd))
            return false;

        return placedStart < existingEnd - HitWindowEpsilonSeconds && placedEnd > existingStart + HitWindowEpsilonSeconds;
    }

    private static bool AreSameDefinitionAndVariant(EditorNoteDefinition aDefinition, ChartNote aNote, EditorNoteDefinition bDefinition, ChartNote bNote)
    {
        if (aDefinition == null || bDefinition == null || aNote == null || bNote == null)
            return false;

        if (!ReferenceEquals(aDefinition, bDefinition) && aDefinition.Kind != bDefinition.Kind)
            return false;

        return EditorNoteDefinitions.FindVariantIndex(aDefinition, aNote) == EditorNoteDefinitions.FindVariantIndex(bDefinition, bNote);
    }

    private bool TouchesAllowedBoundary(EditorNoteDefinition placedDefinition, EditorNoteDefinition existingDefinition, double placedStart, double placedEnd, double existingStart, double existingEnd)
    {
        if (placedDefinition == null || existingDefinition == null)
            return false;

        if (placedDefinition.Kind != EditorNoteKind.SeeSaw || existingDefinition.Kind != EditorNoteKind.SeeSaw)
            return false;

        return Math.Abs(placedStart - existingEnd) <= HitWindowEpsilonSeconds
            || Math.Abs(existingStart - placedEnd) <= HitWindowEpsilonSeconds;
    }

    private double GetBlockingStart(EditorNoteDefinition definition, ChartNote note)
    {
        return GetBlockingStart(definition, note, sameVariantWindow: false, Chart.Notes);
    }

    private double GetBlockingStart(EditorNoteDefinition definition, ChartNote note, bool sameVariantWindow)
    {
        return GetBlockingStart(definition, note, sameVariantWindow, Chart.Notes);
    }

    private double GetBlockingStart(EditorNoteDefinition definition, ChartNote note, bool sameVariantWindow, IReadOnlyList<ChartNote> contextualNotes)
    {
        if (definition == null)
            return note.SongPosition;

        if (definition.Kind == EditorNoteKind.SeeSaw)
            return GetSongPositionAtBeat(GetSeeSawTiming(note, contextualNotes).PrepStartBeat);

        int variantIndex = EditorNoteDefinitions.FindVariantIndex(definition, note);
        Func<int, ChartNote> getRelativeNote = CreateRelativeNoteGetter(note, contextualNotes);
        double crotchet = GetCrotchetAt(note.SongPosition);
        return sameVariantWindow
            ? definition.GetSameVariantHitWindowStart(note.SongPosition, crotchet, variantIndex, getRelativeNote, beforeUsesOuterTiming: false)
            : definition.GetHitWindowStart(note.SongPosition, crotchet, variantIndex, getRelativeNote, beforeUsesOuterTiming: false);
    }

    private double GetBlockingEnd(EditorNoteDefinition definition, ChartNote note)
    {
        return GetBlockingEnd(definition, note, sameVariantWindow: false, Chart.Notes);
    }

    private double GetBlockingEnd(EditorNoteDefinition definition, ChartNote note, bool sameVariantWindow)
    {
        return GetBlockingEnd(definition, note, sameVariantWindow, Chart.Notes);
    }

    private double GetBlockingEnd(EditorNoteDefinition definition, ChartNote note, bool sameVariantWindow, IReadOnlyList<ChartNote> contextualNotes)
    {
        if (definition == null)
            return note.SongPosition;

        if (definition.Kind == EditorNoteKind.SeeSaw)
            return GetSongPositionAtBeat(GetSeeSawTiming(note, contextualNotes).EndBeat);

        int variantIndex = EditorNoteDefinitions.FindVariantIndex(definition, note);
        Func<int, ChartNote> getRelativeNote = CreateRelativeNoteGetter(note, contextualNotes);
        double crotchet = GetCrotchetAt(note.SongPosition);
        return sameVariantWindow
            ? definition.GetSameVariantHitWindowEnd(note.SongPosition, crotchet, variantIndex, getRelativeNote)
            : definition.GetHitWindowEnd(note.SongPosition, crotchet, variantIndex, getRelativeNote);
    }

    private Func<int, ChartNote> CreateRelativeNoteGetter(ChartNote note)
    {
        return CreateRelativeNoteGetter(note, Chart.Notes);
    }

    private static Func<int, ChartNote> CreateRelativeNoteGetter(ChartNote note, IReadOnlyList<ChartNote> notes)
    {
        int index = IndexOfReference(notes, note);
        return offset =>
        {
            int relativeIndex = index + offset;
            return index >= 0 && relativeIndex >= 0 && relativeIndex < notes.Count ? notes[relativeIndex] : null;
        };
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

    private static int CompareContextualNoteEntries(ContextualNoteEntry a, ContextualNoteEntry b)
    {
        int byTime = a.Note.SongPosition.CompareTo(b.Note.SongPosition);
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

    private SeeSawCompiledEventTiming GetSeeSawTiming(ChartNote note)
    {
        return GetSeeSawTiming(note, Chart.Notes);
    }

    private SeeSawCompiledEventTiming GetSeeSawTiming(ChartNote note, IReadOnlyList<ChartNote> contextualNotes)
    {
        SeeSawCompiledEventTiming timing = SeeSawChartCompiler.GetTimingForChartNote(contextualNotes ?? Chart.Notes, note, GetBeatAt);
        if (timing.IsSeeSaw)
            return timing;

        double beat = GetBeatAt(note.SongPosition);
        return new SeeSawCompiledEventTiming(
            idealPrepStartBeat: beat,
            prepStartBeat: beat,
            cueBeat: beat,
            playerHitBeat: beat,
            endBeat: beat,
            isSeeSaw: false,
            isExit: false,
            isValid: true,
            invalidReason: null,
            pattern: SeeSawPatternKind.LongLong,
            launchSide: SeeSawSide.Outer,
            targetSide: SeeSawSide.Outer,
            applejackTargetSide: SeeSawSide.Outer);
    }

    private ChartTempoMap CreateTempoMap()
    {
        return new ChartTempoMap(Chart);
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
        if (Chart?.Notes == null)
            return;

        foreach (ChartNote note in Chart.Notes)
            SynchronizeNoteDuration(note, EditorNoteDefinitions.FromChartNote(note));
    }

    private void SynchronizeNoteDuration(ChartNote note, EditorNoteDefinition definition)
    {
        if (note == null || definition == null)
            return;

        note.HoldDuration = definition.HoldBeats * GetCrotchetAt(note.SongPosition);
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
    }

    private void NormalizeChart()
    {
        if (Chart == null)
            Chart = new Chart();

        if (string.IsNullOrWhiteSpace(Chart.SongName))
            Chart.SongName = Path.GetFileNameWithoutExtension(SongPath);

        if (string.IsNullOrWhiteSpace(Chart.BeatmapName))
            Chart.BeatmapName = Path.GetFileNameWithoutExtension(ChartPath);

        if (string.IsNullOrWhiteSpace(Chart.Beatmapper))
            Chart.Beatmapper = "Unknown";

        if (string.IsNullOrWhiteSpace(Chart.ArtistName))
            Chart.ArtistName = "Unknown";

        if (string.IsNullOrWhiteSpace(Chart.MusicName))
            Chart.MusicName = Chart.SongName;

        if (string.IsNullOrWhiteSpace(Chart.SongPath))
            Chart.SongPath = SongPath;

        SongPath = Chart.SongPath;

        if (Chart.BPM <= 0)
            Chart.BPM = 100;

        Chart.Notes ??= new List<ChartNote>();
        Chart.Effects ??= new List<ChartEffect>();

        foreach (ChartNote note in Chart.Notes)
        {
            note.InputActionToPress ??= "ReactMain";
            note.AdditionnalData ??= new Dictionary<string, string>();
        }

        Chart.Effects = Chart.Effects.Where(effect => effect != null).ToList();
        foreach (ChartEffect effect in Chart.Effects)
            NormalizeEffect(effect);

        SynchronizeAllNoteDurations();
        SortNotes();
        SortEffects();
    }

    private void SortNotes()
    {
        Chart.Notes = Chart.Notes.OrderBy(note => note.SongPosition).ToList();
    }

    private void SortEffects()
    {
        Chart.Effects = Chart.Effects.OrderBy(effect => effect.SongPosition).ToList();
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
