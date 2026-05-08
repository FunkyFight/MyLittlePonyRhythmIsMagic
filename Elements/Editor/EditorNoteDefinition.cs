using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Rhythm.Note;

namespace MLP_RiM.Elements.Editor;

public enum EditorNoteKind
{
    Unknown = -1,
    RhythmInput,
    SeeSaw,
    SeaponyParade
}

public readonly record struct NoteTypeId(string GameId, string NoteId)
{
    public bool IsEmpty => string.IsNullOrWhiteSpace(GameId) || string.IsNullOrWhiteSpace(NoteId);

    public override string ToString()
    {
        return IsEmpty ? string.Empty : $"{GameId}:{NoteId}";
    }
}

public static class EditorNoteKindCompatibility
{
    public static NoteTypeId ToTypeId(EditorNoteKind kind)
    {
        return kind switch
        {
            EditorNoteKind.RhythmInput => new NoteTypeId("core", "rhythm_input"),
            EditorNoteKind.SeeSaw => new NoteTypeId("see_saw", "jump"),
            EditorNoteKind.SeaponyParade => new NoteTypeId("seapony_parade", "note"),
            _ => default
        };
    }

    public static EditorNoteKind ToKind(NoteTypeId typeId)
    {
        if (typeId == ToTypeId(EditorNoteKind.RhythmInput))
            return EditorNoteKind.RhythmInput;

        if (typeId == ToTypeId(EditorNoteKind.SeeSaw))
            return EditorNoteKind.SeeSaw;

        if (typeId == ToTypeId(EditorNoteKind.SeaponyParade))
            return EditorNoteKind.SeaponyParade;

        return EditorNoteKind.Unknown;
    }
}

public sealed class EditorNotePlacement
{
    public EditorNotePlacement(EditorNoteDefinition definition, ChartNote note)
    {
        Definition = definition;
        Note = note;
    }

    public EditorNoteDefinition Definition { get; }
    public ChartNote Note { get; }
}

public sealed class EditorNotePlacementContext
{
    public EditorNotePlacementContext(double crotchet, IReadOnlyList<ChartNote> existingNotes)
    {
        Crotchet = crotchet;
        ExistingNotes = existingNotes ?? Array.Empty<ChartNote>();
    }

    public double Crotchet { get; }
    public IReadOnlyList<ChartNote> ExistingNotes { get; }
}

public sealed record PlacementOptions(double? RepeatDurationBeats = null, double? RepeatStepBeats = null)
{
    public static readonly PlacementOptions None = new();

    public bool HasRepeat => RepeatDurationBeats.HasValue || RepeatStepBeats.HasValue;

    public static PlacementOptions FromLegacyNote(ChartNote note)
    {
        if (!EditorNotePlacementData.HasIntervalConfiguration(note))
            return None;

        return new PlacementOptions(
            RepeatDurationBeats: Math.Max(0, IntervalEditorNoteProvider.GetDurationBeats(note.AdditionnalData)),
            RepeatStepBeats: IntervalEditorNoteProvider.GetStepBeats(note.AdditionnalData));
    }
}

public interface IEditorNotePlacementStrategy
{
    IReadOnlyList<EditorNotePlacement> CreatePlacements(EditorNoteDefinition definition, ChartNote sourceNote, EditorNotePlacementContext context, PlacementOptions placementOptions);
}

public sealed class SingleEditorNotePlacementStrategy : IEditorNotePlacementStrategy
{
    public IReadOnlyList<EditorNotePlacement> CreatePlacements(EditorNoteDefinition definition, ChartNote sourceNote, EditorNotePlacementContext context, PlacementOptions placementOptions)
    {
        if (definition == null || sourceNote == null || context == null)
            return Array.Empty<EditorNotePlacement>();

        return EditorNotePlacementData.CreateNotesFromSource(sourceNote, context.Crotchet, placementOptions, time => EditorNotePlacementData.CloneForPlacement(sourceNote, time))
            .Select(note => new EditorNotePlacement(definition, note))
            .ToArray();
    }
}

internal static class EditorNotePlacementData
{
    private const double InclusiveEndEpsilonSeconds = 0.000001;

    public static bool HasIntervalConfiguration(ChartNote note)
    {
        return note?.AdditionnalData != null
            && (note.AdditionnalData.ContainsKey(IntervalEditorNoteProvider.DurationBeatsKey)
                || note.AdditionnalData.ContainsKey(IntervalEditorNoteProvider.StepBeatsKey));
    }

    public static IReadOnlyList<ChartNote> CreateNotesFromSource(ChartNote sourceNote, double crotchet, Func<double, ChartNote> createNote)
    {
        return CreateNotesFromSource(sourceNote, crotchet, PlacementOptions.FromLegacyNote(sourceNote), createNote);
    }

    public static IReadOnlyList<ChartNote> CreateNotesFromSource(ChartNote sourceNote, double crotchet, PlacementOptions placementOptions, Func<double, ChartNote> createNote)
    {
        if (sourceNote == null || createNote == null)
            return Array.Empty<ChartNote>();

        if (sourceNote.BeatPosition.HasValue)
            return CreateBeatBasedNotesFromSource(sourceNote, crotchet, placementOptions, createNote);

        double start = sourceNote.SongPosition;
        if (placementOptions?.HasRepeat != true)
            return new[] { createNote(start) };

        if (crotchet <= 0)
            return Array.Empty<ChartNote>();

        double durationBeats = Math.Max(0, placementOptions.RepeatDurationBeats ?? 0);
        double stepBeats = Math.Max(0.000001, placementOptions.RepeatStepBeats ?? IntervalEditorNoteProvider.DefaultStepBeats);
        double end = start + durationBeats * crotchet;
        double stepSeconds = stepBeats * crotchet;

        List<ChartNote> notes = new();
        for (double time = start; time <= end + InclusiveEndEpsilonSeconds; time += stepSeconds)
            notes.Add(createNote(time));

        return notes;
    }

    private static IReadOnlyList<ChartNote> CreateBeatBasedNotesFromSource(ChartNote sourceNote, double crotchet, PlacementOptions placementOptions, Func<double, ChartNote> createNote)
    {
        double startBeat = sourceNote.BeatPosition.Value;
        if (placementOptions?.HasRepeat != true)
        {
            ChartNote note = createNote(sourceNote.SongPosition);
            note.BeatPosition = startBeat;
            note.HoldBeats = sourceNote.HoldBeats;
            return new[] { note };
        }

        double durationBeats = Math.Max(0, placementOptions.RepeatDurationBeats ?? 0);
        double stepBeats = Math.Max(0.000001, placementOptions.RepeatStepBeats ?? IntervalEditorNoteProvider.DefaultStepBeats);
        if (stepBeats <= 0 || double.IsNaN(stepBeats) || double.IsInfinity(stepBeats))
            return Array.Empty<ChartNote>();

        double endBeat = startBeat + durationBeats;
        List<ChartNote> notes = new();
        for (double beat = startBeat; beat <= endBeat + InclusiveEndEpsilonSeconds; beat += stepBeats)
        {
            double approximatedSongPosition = sourceNote.SongPosition + (beat - startBeat) * Math.Max(crotchet, 0.0);
            ChartNote note = createNote(approximatedSongPosition);
            note.BeatPosition = beat;
            note.HoldBeats = sourceNote.HoldBeats;
            notes.Add(note);
        }

        return notes;
    }

    public static ChartNote CloneForPlacement(ChartNote sourceNote, double songPosition)
    {
        return new ChartNote
        {
            SongPosition = songPosition,
            BeatPosition = sourceNote?.BeatPosition,
            HoldDuration = sourceNote?.HoldDuration ?? 0,
            HoldBeats = sourceNote?.HoldBeats,
            InputActionToPress = sourceNote?.InputActionToPress,
            AdditionnalData = CreateStoredAdditionnalData(sourceNote)
        };
    }

    public static Dictionary<string, string> CreateStoredAdditionnalData(ChartNote note)
    {
        Dictionary<string, string> data = new(note?.AdditionnalData ?? new Dictionary<string, string>());
        data.Remove(IntervalEditorNoteProvider.DurationBeatsKey);
        data.Remove(IntervalEditorNoteProvider.StepBeatsKey);
        return data;
    }
}

public sealed record NoteTimingPreset(string Id)
{
    public static readonly NoteTimingPreset Default = new("default");
}

public sealed record EditorVisualStyle(Color Color)
{
    public static readonly EditorVisualStyle Default = new(Color.DeepSkyBlue);
}

public sealed class EditorNoteVariant
{
    private readonly IReadOnlyDictionary<string, string> _legacyData;

    public EditorNoteVariant(string displayName, IReadOnlyDictionary<string, string> additionnalData)
        : this(CreateId(displayName), displayName, new LegacyNotePayload(string.Empty, string.Empty, 0, additionnalData), _ => true, NoteTimingPreset.Default, EditorVisualStyle.Default)
    {
        _legacyData = additionnalData ?? new Dictionary<string, string>();
    }

    public EditorNoteVariant(string id, string displayName, INotePayload defaultPayload, Func<INotePayload, bool> matches = null, NoteTimingPreset timingPreset = null, EditorVisualStyle editorStyle = null)
    {
        Id = string.IsNullOrWhiteSpace(id) ? CreateId(displayName) : id;
        DisplayName = displayName;
        DefaultPayload = defaultPayload;
        Matches = matches ?? (_ => false);
        TimingPreset = timingPreset ?? NoteTimingPreset.Default;
        EditorStyle = editorStyle ?? EditorVisualStyle.Default;
        _legacyData = defaultPayload?.ToLegacyData() ?? new Dictionary<string, string>();
    }

    public string Id { get; }
    public string DisplayName { get; }
    public INotePayload DefaultPayload { get; }
    public Func<INotePayload, bool> Matches { get; }
    public NoteTimingPreset TimingPreset { get; }
    public EditorVisualStyle EditorStyle { get; }
    public IReadOnlyDictionary<string, string> AdditionnalData => DefaultPayload?.ToLegacyData() ?? _legacyData;

    public bool MatchesPayload(INotePayload payload)
    {
        return Matches(payload);
    }

    private static string CreateId(string displayName)
    {
        return string.IsNullOrWhiteSpace(displayName)
            ? "default"
            : displayName.Trim().ToLowerInvariant().Replace(' ', '_');
    }
}

public sealed class EditorNoteDefinition
{
    public NoteTypeId TypeId { get; }
    public EditorNoteKind Kind => EditorNoteKindCompatibility.ToKind(TypeId);
    public string DisplayName { get; }
    public string InputAction { get; }
    public double HoldBeats { get; }
    public double OccupyBeforeBeats { get; }
    public double OccupyAfterBeats { get; }
    public double HitWindowBeforeBeats { get; }
    public double HitWindowAfterBeats { get; }
    public double SameVariantHitWindowBeforeBeats { get; }
    public double SameVariantHitWindowAfterBeats { get; }
    public IReadOnlyList<EditorNoteVariant> Variants { get; }
    private IEditorNoteTiming Timing { get; }
    private Func<ChartNote, bool> MatchesChartNote { get; }
    private IEditorNotePlacementStrategy PlacementStrategy { get; }

    public EditorNoteDefinition(EditorNoteKind kind, string displayName, string inputAction, double holdBeats, double occupyBeforeBeats, double occupyAfterBeats, double hitWindowBeforeBeats, double hitWindowAfterBeats, IReadOnlyList<EditorNoteVariant> variants)
        : this(EditorNoteKindCompatibility.ToTypeId(kind), displayName, inputAction, holdBeats, occupyBeforeBeats, occupyAfterBeats, hitWindowBeforeBeats, hitWindowAfterBeats, variants)
    {
    }

    public EditorNoteDefinition(NoteTypeId typeId, string displayName, string inputAction, double holdBeats, double occupyBeforeBeats, double occupyAfterBeats, double hitWindowBeforeBeats, double hitWindowAfterBeats, IReadOnlyList<EditorNoteVariant> variants)
        : this(typeId, displayName, inputAction, holdBeats, occupyBeforeBeats, occupyAfterBeats, hitWindowBeforeBeats, hitWindowAfterBeats, null, null, variants, new FixedEditorNoteTiming(), _ => false, new SingleEditorNotePlacementStrategy())
    {
    }

    public EditorNoteDefinition(EditorNoteKind kind, string displayName, string inputAction, double holdBeats, double occupyBeforeBeats, double occupyAfterBeats, double hitWindowBeforeBeats, double hitWindowAfterBeats, IReadOnlyList<EditorNoteVariant> variants, IEditorNoteTiming timing, Func<ChartNote, bool> matchesChartNote, IEditorNotePlacementStrategy placementStrategy)
        : this(EditorNoteKindCompatibility.ToTypeId(kind), displayName, inputAction, holdBeats, occupyBeforeBeats, occupyAfterBeats, hitWindowBeforeBeats, hitWindowAfterBeats, variants, timing, matchesChartNote, placementStrategy)
    {
    }

    public EditorNoteDefinition(NoteTypeId typeId, string displayName, string inputAction, double holdBeats, double occupyBeforeBeats, double occupyAfterBeats, double hitWindowBeforeBeats, double hitWindowAfterBeats, IReadOnlyList<EditorNoteVariant> variants, IEditorNoteTiming timing, Func<ChartNote, bool> matchesChartNote, IEditorNotePlacementStrategy placementStrategy)
        : this(typeId, displayName, inputAction, holdBeats, occupyBeforeBeats, occupyAfterBeats, hitWindowBeforeBeats, hitWindowAfterBeats, null, null, variants, timing, matchesChartNote, placementStrategy)
    {
    }

    public EditorNoteDefinition(EditorNoteKind kind, string displayName, string inputAction, double holdBeats, double occupyBeforeBeats, double occupyAfterBeats, double hitWindowBeforeBeats, double hitWindowAfterBeats, double? sameVariantHitWindowBeforeBeats, double? sameVariantHitWindowAfterBeats, IReadOnlyList<EditorNoteVariant> variants, IEditorNoteTiming timing, Func<ChartNote, bool> matchesChartNote, IEditorNotePlacementStrategy placementStrategy)
        : this(EditorNoteKindCompatibility.ToTypeId(kind), displayName, inputAction, holdBeats, occupyBeforeBeats, occupyAfterBeats, hitWindowBeforeBeats, hitWindowAfterBeats, sameVariantHitWindowBeforeBeats, sameVariantHitWindowAfterBeats, variants, timing, matchesChartNote, placementStrategy)
    {
    }

    public EditorNoteDefinition(NoteTypeId typeId, string displayName, string inputAction, double holdBeats, double occupyBeforeBeats, double occupyAfterBeats, double hitWindowBeforeBeats, double hitWindowAfterBeats, double? sameVariantHitWindowBeforeBeats, double? sameVariantHitWindowAfterBeats, IReadOnlyList<EditorNoteVariant> variants, IEditorNoteTiming timing, Func<ChartNote, bool> matchesChartNote, IEditorNotePlacementStrategy placementStrategy)
    {
        TypeId = typeId;
        DisplayName = displayName;
        InputAction = inputAction;
        HoldBeats = holdBeats;
        OccupyBeforeBeats = occupyBeforeBeats;
        OccupyAfterBeats = occupyAfterBeats;
        HitWindowBeforeBeats = hitWindowBeforeBeats;
        HitWindowAfterBeats = hitWindowAfterBeats;
        SameVariantHitWindowBeforeBeats = sameVariantHitWindowBeforeBeats ?? hitWindowBeforeBeats;
        SameVariantHitWindowAfterBeats = sameVariantHitWindowAfterBeats ?? hitWindowAfterBeats;
        Variants = variants;
        Timing = timing;
        MatchesChartNote = matchesChartNote;
        PlacementStrategy = placementStrategy;
    }

    public bool Matches(ChartNote note)
    {
        return MatchesChartNote(note);
    }

    public ChartNote CreateChartNote(double songPosition, double crotchet, int variantIndex = 0)
    {
        EditorNoteVariant variant = GetVariant(variantIndex);
        return new ChartNote
        {
            SongPosition = songPosition,
            BeatPosition = null,
            HoldDuration = HoldBeats * crotchet,
            HoldBeats = HoldBeats,
            InputActionToPress = InputAction,
            AdditionnalData = variant.AdditionnalData.ToDictionary(pair => pair.Key, pair => pair.Value)
        };
    }

    public IReadOnlyList<EditorNotePlacement> CreatePlacements(ChartNote sourceNote, EditorNotePlacementContext context)
    {
        return CreatePlacements(sourceNote, context, PlacementOptions.FromLegacyNote(sourceNote));
    }

    public IReadOnlyList<EditorNotePlacement> CreatePlacements(ChartNote sourceNote, EditorNotePlacementContext context, PlacementOptions placementOptions)
    {
        return PlacementStrategy.CreatePlacements(this, sourceNote, context, placementOptions ?? PlacementOptions.None);
    }

    public EditorNoteVariant GetVariant(int variantIndex)
    {
        if (Variants.Count == 0)
            return new EditorNoteVariant(DisplayName, new Dictionary<string, string>());

        return Variants[Math.Clamp(variantIndex, 0, Variants.Count - 1)];
    }

    public bool Occupies(double noteSongPosition, double crotchet, double testedSongPosition)
    {
        double end = noteSongPosition + Math.Max(HoldBeats, OccupyAfterBeats) * crotchet;
        return testedSongPosition >= noteSongPosition - OccupyBeforeBeats * crotchet && testedSongPosition <= end;
    }

    public NoteTimingResult GetTiming(NoteTimingRequest request)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));

        if (request.Definition == null || !ReferenceEquals(request.Definition, this))
            request = request with { Definition = this };

        return Timing.GetTiming(request);
    }
}
