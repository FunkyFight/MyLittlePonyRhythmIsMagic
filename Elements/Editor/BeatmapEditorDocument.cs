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
    private const double SeeSawInnerBeforeBeats = 2.0;
    private const double SeeSawOuterBeforeBeats = 4.0;

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
        return new BeatmapEditorDocument(new Chart
        {
            SongName = Path.GetFileNameWithoutExtension(songPath),
            BeatmapName = Path.GetFileNameWithoutExtension(chartPath),
            Beatmapper = "Unknown",
            ArtistName = "Unknown",
            MusicName = Path.GetFileNameWithoutExtension(songPath),
            SongPath = songPath,
            BPM = bpm,
            Offset = 0.078,
            Notes = new List<ChartNote>()
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

        SortNotes();

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
        return TryPlaceNote(definition, definition.CreateChartNote(songPosition, Crotchet, variantIndex), out placedNote, out reason);
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

        List<ChartNote> notes = normalizedPlacements.Select(placement => placement.Note).ToList();
        Chart.Notes.AddRange(notes);
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

    public void MarkDirty()
    {
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

    public void SetBpm(double bpm)
    {
        if (bpm <= 0)
            return;

        Chart.BPM = bpm;
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

        if (definition.Kind != EditorNoteKind.SeeSaw)
            return definition.GetHitWindowEnd(note.SongPosition, Crotchet, variantIndex, getRelativeNote);

        SeeSawEditorState state = GetSeeSawStateBefore(note.SongPosition);
        SeeSawAction action = SeeSawAction.FromAdditionnalData(note.AdditionnalData);
        if (IsSeeSawOpposite(action))
            return note.SongPosition + GetOppositeAfterBeats(action, state) * Crotchet;

        bool rainbowTargetsOuter = GetRainbowTargetIsOuter(action, state);
        bool afterUsesOuterTiming = GetAfterUsesOuterTiming(action, state);
        return definition.GetHitWindowEnd(note.SongPosition, Crotchet, variantIndex, getRelativeNote, rainbowTargetsOuter, action.IsBigLeap, afterUsesOuterTiming);
    }

    public double GetContextualSameVariantHitWindowEnd(ChartNote note, EditorNoteDefinition definition)
    {
        if (definition == null)
            return note.SongPosition;

        Func<int, ChartNote> getRelativeNote = CreateRelativeNoteGetter(note);
        int variantIndex = EditorNoteDefinitions.FindVariantIndex(definition, note);

        if (definition.Kind != EditorNoteKind.SeeSaw)
            return definition.GetSameVariantHitWindowEnd(note.SongPosition, Crotchet, variantIndex, getRelativeNote);

        SeeSawEditorState state = GetSeeSawStateBefore(note.SongPosition);
        SeeSawAction action = SeeSawAction.FromAdditionnalData(note.AdditionnalData);
        if (IsSeeSawOpposite(action))
            return note.SongPosition + GetOppositeAfterBeats(action, state) * Crotchet;

        bool rainbowTargetsOuter = GetRainbowTargetIsOuter(action, state);
        bool afterUsesOuterTiming = GetAfterUsesOuterTiming(action, state);
        return definition.GetSameVariantHitWindowEnd(note.SongPosition, Crotchet, variantIndex, getRelativeNote, rainbowTargetsOuter, action.IsBigLeap, afterUsesOuterTiming);
    }

    public double GetContextualStart(ChartNote note, EditorNoteDefinition definition)
    {
        if (definition == null)
            return note.SongPosition;

        Func<int, ChartNote> getRelativeNote = CreateRelativeNoteGetter(note);
        int variantIndex = EditorNoteDefinitions.FindVariantIndex(definition, note);

        if (definition.Kind != EditorNoteKind.SeeSaw)
            return definition.GetStart(note.SongPosition, Crotchet, variantIndex, getRelativeNote, beforeUsesOuterTiming: false);

        SeeSawEditorState state = GetSeeSawStateBefore(note.SongPosition);
        SeeSawAction action = SeeSawAction.FromAdditionnalData(note.AdditionnalData);
        if (IsSeeSawOpposite(action))
            return note.SongPosition - GetOppositeApproachBeats(action, state) * Crotchet;

        bool beforeUsesOuterTiming = GetBeforeUsesOuterTiming(action, state);
        bool counterUsesOuterTiming = GetCounterUsesOuterTiming(action, state);
        return definition.GetStart(note.SongPosition, Crotchet, variantIndex, getRelativeNote, beforeUsesOuterTiming, action.IsBigLeap, counterUsesOuterTiming, action.HasBigCounterJump);
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

        if (definition.Kind != EditorNoteKind.SeeSaw)
            return definition.GetHitWindowStart(note.SongPosition, Crotchet, variantIndex, getRelativeNote, beforeUsesOuterTiming: false);

        SeeSawEditorState state = GetSeeSawStateBefore(note.SongPosition);
        SeeSawAction action = SeeSawAction.FromAdditionnalData(note.AdditionnalData);
        if (IsSeeSawOpposite(action))
            return note.SongPosition - GetOppositeApproachBeats(action, state) * Crotchet;

        bool beforeUsesOuterTiming = GetBeforeUsesOuterTiming(action, state);
        bool counterUsesOuterTiming = GetCounterUsesOuterTiming(action, state);
        return definition.GetHitWindowStart(note.SongPosition, Crotchet, variantIndex, getRelativeNote, beforeUsesOuterTiming, action.IsBigLeap, counterUsesOuterTiming, action.HasBigCounterJump);
    }

    public double GetContextualSameVariantHitWindowStart(ChartNote note, EditorNoteDefinition definition)
    {
        if (definition == null)
            return note.SongPosition;

        Func<int, ChartNote> getRelativeNote = CreateRelativeNoteGetter(note);
        int variantIndex = EditorNoteDefinitions.FindVariantIndex(definition, note);

        if (definition.Kind != EditorNoteKind.SeeSaw)
            return definition.GetSameVariantHitWindowStart(note.SongPosition, Crotchet, variantIndex, getRelativeNote, beforeUsesOuterTiming: false);

        SeeSawEditorState state = GetSeeSawStateBefore(note.SongPosition);
        SeeSawAction action = SeeSawAction.FromAdditionnalData(note.AdditionnalData);
        if (IsSeeSawOpposite(action))
            return note.SongPosition - GetOppositeApproachBeats(action, state) * Crotchet;

        bool beforeUsesOuterTiming = GetBeforeUsesOuterTiming(action, state);
        bool counterUsesOuterTiming = GetCounterUsesOuterTiming(action, state);
        return definition.GetSameVariantHitWindowStart(note.SongPosition, Crotchet, variantIndex, getRelativeNote, beforeUsesOuterTiming, action.IsBigLeap, counterUsesOuterTiming, action.HasBigCounterJump);
    }

    public double GetContextualEnd(ChartNote note, EditorNoteDefinition definition)
    {
        if (definition == null)
            return note.SongPosition;

        if (definition.Kind == EditorNoteKind.SeeSaw)
            return note.SongPosition;

        return definition.GetEnd(note.SongPosition, Crotchet, EditorNoteDefinitions.FindVariantIndex(definition, note), CreateRelativeNoteGetter(note));
    }

    public double GetContextualStart(EditorNoteDefinition definition, double songPosition, int variantIndex)
    {
        if (definition == null)
            return songPosition;

        if (definition.Kind != EditorNoteKind.SeeSaw)
            return definition.GetStart(songPosition, Crotchet);

        SeeSawEditorState state = GetSeeSawStateBefore(songPosition);
        EditorNoteVariant variant = definition.GetVariant(variantIndex);
        SeeSawAction action = SeeSawAction.FromVariant(variant);
        if (SeeSawAction.GetBaseDirection(action.Direction) == SeeSawDirection.Exit)
            return songPosition - 2 * Crotchet;

        if (IsSeeSawOpposite(action))
            return songPosition - GetOppositeApproachBeats(action, state) * Crotchet;

        bool beforeUsesOuterTiming = GetBeforeUsesOuterTiming(action, state);
        bool counterUsesOuterTiming = GetCounterUsesOuterTiming(action, state);
        return definition.GetStart(songPosition, Crotchet, variantIndex, beforeUsesOuterTiming, forceBigLeapTiming: false, afterUsesOuterTiming: counterUsesOuterTiming);
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

        return Math.Abs(placedEnd - existingEnd) <= HitWindowEpsilonSeconds
            || Math.Abs(placedStart - existingEnd) <= HitWindowEpsilonSeconds
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
            return note.SongPosition - 2 * Crotchet;

        int variantIndex = EditorNoteDefinitions.FindVariantIndex(definition, note);
        Func<int, ChartNote> getRelativeNote = CreateRelativeNoteGetter(note, contextualNotes);
        return sameVariantWindow
            ? definition.GetSameVariantHitWindowStart(note.SongPosition, Crotchet, variantIndex, getRelativeNote, beforeUsesOuterTiming: false)
            : definition.GetHitWindowStart(note.SongPosition, Crotchet, variantIndex, getRelativeNote, beforeUsesOuterTiming: false);
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
            return note.SongPosition;

        int variantIndex = EditorNoteDefinitions.FindVariantIndex(definition, note);
        Func<int, ChartNote> getRelativeNote = CreateRelativeNoteGetter(note, contextualNotes);
        return sameVariantWindow
            ? definition.GetSameVariantHitWindowEnd(note.SongPosition, Crotchet, variantIndex, getRelativeNote)
            : definition.GetHitWindowEnd(note.SongPosition, Crotchet, variantIndex, getRelativeNote);
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

    private SeeSawEditorState GetSeeSawStateBefore(double songPosition)
    {
        SeeSawEditorState state = new(rainbowIsOuter: true, applejackIsOuter: false);

        foreach (ChartNote note in Chart.Notes.Where(note => note.SongPosition < songPosition - HitWindowEpsilonSeconds).OrderBy(note => note.SongPosition))
        {
            if (note.AdditionnalData == null || !note.AdditionnalData.TryGetValue("action", out string actionValue))
                continue;

            if (SeeSawAction.TryParse(actionValue, out _))
                state = SeeSawAction.FromAdditionnalData(note.AdditionnalData).Apply(state);
        }

        return state;
    }

    private bool GetRainbowTargetIsOuter(SeeSawAction action, SeeSawEditorState state)
    {
        return action.Apply(state).RainbowIsOuter;
    }

    private bool GetBeforeUsesOuterTiming(SeeSawAction action, SeeSawEditorState state)
    {
        if (IsSeeSawOpposite(action))
            return action.OppositeMode == SeeSawOppositeMode.Applejack ? state.RainbowIsOuter : state.ApplejackIsOuter;

        return state.RainbowIsOuter;
    }

    private bool GetAfterUsesOuterTiming(SeeSawAction action, SeeSawEditorState state)
    {
        return SeeSawAction.GetBaseDirection(action.Direction) switch
        {
            SeeSawDirection.Outer => true,
            SeeSawDirection.Inner => false,
            SeeSawDirection.Opposite => action.OppositeMode switch
            {
                SeeSawOppositeMode.Applejack => !state.RainbowIsOuter,
                SeeSawOppositeMode.Both => !state.RainbowIsOuter,
                _ => !state.ApplejackIsOuter
            },
            SeeSawDirection.Exit => true,
            _ => action.Apply(state).RainbowIsOuter
        };
    }

    private bool GetCounterUsesOuterTiming(SeeSawAction action, SeeSawEditorState state)
    {
        if (IsSeeSawOpposite(action))
            return GetBeforeUsesOuterTiming(action, state);

        return state.ApplejackIsOuter;
    }

    private static bool IsSeeSawOpposite(SeeSawAction action)
    {
        return SeeSawAction.GetBaseDirection(action.Direction) == SeeSawDirection.Opposite;
    }

    private static double GetOppositeApproachBeats(SeeSawAction action, SeeSawEditorState state)
    {
        return action.OppositeMode switch
        {
            SeeSawOppositeMode.Applejack => GetActorPhaseBeats(state.ApplejackIsOuter, action.HasBigCounterJump)
                + GetActorPhaseBeats(state.RainbowIsOuter, action.IsBigLeap),
            SeeSawOppositeMode.Both => GetActorPhaseBeats(state.ApplejackIsOuter, action.HasBigCounterJump)
                + GetActorPhaseBeats(state.RainbowIsOuter, action.IsBigLeap),
            _ => GetActorPhaseBeats(state.ApplejackIsOuter, action.HasBigCounterJump)
                + GetActorPhaseBeats(state.RainbowIsOuter, action.IsBigLeap)
        };
    }

    private static double GetOppositeAfterBeats(SeeSawAction action, SeeSawEditorState state)
    {
        SeeSawEditorState targetState = action.Apply(state);
        return action.OppositeMode switch
        {
            SeeSawOppositeMode.Applejack => GetActorPhaseBeats(targetState.RainbowIsOuter, action.IsBigLeap),
            SeeSawOppositeMode.Both => GetActorPhaseBeats(targetState.RainbowIsOuter, action.IsBigLeap),
            _ => GetActorPhaseBeats(targetState.RainbowIsOuter, action.IsBigLeap)
        };
    }

    private static double GetActorApproachBeats(bool isOuter, bool isBigLeap)
    {
        return isBigLeap ? SeeSawOuterBeforeBeats / 2.0 : isOuter ? SeeSawOuterBeforeBeats : SeeSawInnerBeforeBeats;
    }

    private static double GetActorPhaseBeats(bool isOuter, bool isBigLeap)
    {
        return isBigLeap ? SeeSawOuterBeforeBeats / 2.0 : (isOuter ? SeeSawOuterBeforeBeats : SeeSawInnerBeforeBeats) / 2.0;
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

        foreach (ChartNote note in Chart.Notes)
        {
            note.InputActionToPress ??= "ReactMain";
            note.AdditionnalData ??= new Dictionary<string, string>();
        }

        SortNotes();
    }

    private void SortNotes()
    {
        Chart.Notes = Chart.Notes.OrderBy(note => note.SongPosition).ToList();
    }
}
