using System;
using System.Collections.Generic;
using System.Linq;
using GameCore.Scenes;
using Microsoft.Xna.Framework;
using Rhythm.Note;

namespace MLP_RiM.Elements.Editor;

public sealed class SeaponyParadeEditorNoteProvider : EditorNoteProvider
{
    public const string GameId = SeaponyNoteCodec.GameId;
    public const string SwimClipId = "seapony_parade.swim";
    public const string RollClipId = "seapony_parade.roll";
    public const string TapTapClipId = "seapony_parade.tap_tap";
    public static readonly NoteTypeId TypeId = new(GameId, SeaponyNoteCodec.NoteId);

    private static readonly SeaponyParadePatternCompiler PatternCompiler = new();

    public override int SortOrder => 10;

    public override string RhythmGameId => GameId;

    public override string RhythmGameDisplayName => "Seapony Parade";

    public override EditorNoteDefinition Definition { get; } = new EditorNoteDefinitionBuilder(TypeId, "Seapony Parade")
        .HitWindow(0, 2)
        .InputAction("ReactMain")
        .Occupies(1d, 1d)
        .Matches(note => SeaponyNoteCodec.Matches(note?.AdditionnalData))
        .Variant("swim", "Swim", new SeaponyNotePayload(SeaponyAction.Swim), MatchesAction(SeaponyAction.Swim), editorStyle: new EditorVisualStyle(Color.DeepSkyBlue))
        .Variant("roll", "Roll", new SeaponyNotePayload(SeaponyAction.Roll), MatchesAction(SeaponyAction.Roll), editorStyle: new EditorVisualStyle(Color.MediumPurple))
        .Variant("tap_tap", "Tap Tap", new SeaponyNotePayload(SeaponyAction.TapTap), MatchesAction(SeaponyAction.TapTap), editorStyle: new EditorVisualStyle(Color.Gold))
        .Timing(new SeaponyParadeEditorNoteTiming())
        .Placement(new SeaponyParadeEditorNotePlacementStrategy())
        .Build();

    public override int GetNoteVariantIndex(ChartNote note)
    {
        return SeaponyNoteCodec.ReadAction(note?.AdditionnalData) switch
        {
            SeaponyAction.Roll => 1,
            SeaponyAction.TapTap => 2,
            _ => 0
        };
    }

    public override string GetClipTypeIdFromLegacyNote(ChartNote note)
    {
        return SeaponyNoteCodec.ReadAction(note?.AdditionnalData) switch
        {
            SeaponyAction.Roll => RollClipId,
            SeaponyAction.TapTap => TapTapClipId,
            _ => SwimClipId
        };
    }

    public override Scene CreateScene()
    {
        return new SeaPonyParade();
    }

    protected override IReadOnlyList<EditorClipDefinition> CreateClips()
    {
        return new[]
        {
            Clip(SwimClipId, "Swim", EditorClipCategory.Continuous, 2, "ReactMain", SeaponyNoteCodec.Write(SeaponyAction.Swim), editorStyle: new EditorVisualStyle(Color.CornflowerBlue)),
            Clip(RollClipId, "Roll", EditorClipCategory.Continuous, 3, "ReactMain", SeaponyNoteCodec.Write(SeaponyAction.Roll), editorStyle: new EditorVisualStyle(Color.DeepSkyBlue)),
            Clip(TapTapClipId, "Tap Tap", EditorClipCategory.SingleHit, 0, "ReactMain", SeaponyNoteCodec.Write(SeaponyAction.TapTap), editorStyle: new EditorVisualStyle(Color.LightBlue)),
            Clip(EditorClipDefinitions.NoHit, "No Hit", EditorClipCategory.NoHit, 1, editorStyle: new EditorVisualStyle(Color.DimGray))
        };
    }

    public override IReadOnlyList<ChartNote> CompileClip(ChartEditorClip clip, ChartTempoMap tempoMap)
    {
        if (clip == null || tempoMap == null || !IsRuntimeClip(clip, out EditorClipDefinition definition))
            return Array.Empty<ChartNote>();

        Dictionary<string, string> data = CreateClipData(clip, definition);
        SeaponyNotePayload payload = SeaponyNoteCodec.Read(data);
        double startBeat = payload.Action switch
        {
            SeaponyAction.Roll => clip.StartBeat + SeaponyParadePatternCompiler.RollCueLeadBeats,
            SeaponyAction.TapTap => clip.StartBeat + SeaponyParadePatternCompiler.TapTapCueLeadBeats,
            _ => clip.StartBeat
        };

        NoteAuthoringIntent intent = new(GameId, payload.Action.ToString(), startBeat, Math.Max(0.0, clip.LengthBeats), payload);
        return PatternCompiler.Compile(intent, new NoteCompileContext(tempoMap))
            .Select(draft => draft.ToChartNote(tempoMap))
            .ToArray();
    }

    private static Func<INotePayload, bool> MatchesAction(SeaponyAction action)
    {
        return payload => payload is SeaponyNotePayload seaponyPayload && seaponyPayload.Action == action;
    }
}

public sealed class SeaponyParadeEditorNotePlacementStrategy : IEditorNotePlacementStrategy
{
    private static readonly SeaponyParadePatternCompiler PatternCompiler = new();

    public IReadOnlyList<EditorNotePlacement> CreatePlacements(EditorNoteDefinition definition, ChartNote sourceNote, EditorNotePlacementContext context, PlacementOptions placementOptions)
    {
        if (definition == null || sourceNote == null || context == null)
            return Array.Empty<EditorNotePlacement>();

        SeaponyNotePayload payload = SeaponyNoteCodec.Read(sourceNote.AdditionnalData);
        double startBeat = GetSourceBeat(sourceNote, context.Crotchet);
        NoteAuthoringIntent intent = new(SeaponyParadeEditorNoteProvider.GameId, payload.Action.ToString(), startBeat, placementOptions?.RepeatDurationBeats ?? 0.0, payload, placementOptions ?? PlacementOptions.None);
        return PatternCompiler.Compile(intent, new NoteCompileContext(null, context.ExistingNotes))
            .Select(draft => new EditorNotePlacement(definition, CreateNoteFromDraft(sourceNote, draft, startBeat, context.Crotchet)))
            .ToArray();
    }

    private static ChartNote CreateNoteFromDraft(ChartNote sourceNote, RuntimeNoteDraft draft, double sourceBeat, double crotchet)
    {
        double songPosition = sourceNote.SongPosition + (draft.Beat - sourceBeat) * Math.Max(0.0, crotchet);
        ChartNote note = EditorNotePlacementData.CloneForPlacement(sourceNote, songPosition);
        note.BeatPosition = draft.Beat;
        note.HoldBeats = draft.HoldBeats;
        note.HoldDuration = draft.HoldBeats > 0.0 && crotchet > 0.0 ? draft.HoldBeats * crotchet : 0.0;
        note.InputActionToPress = draft.InputAction;
        note.AdditionnalData = draft.Payload?.ToLegacyData() ?? new Dictionary<string, string>();
        return note;
    }

    private static double GetSourceBeat(ChartNote sourceNote, double crotchet)
    {
        if (sourceNote.BeatPosition.HasValue)
            return sourceNote.BeatPosition.Value;

        return crotchet > 0.0 ? sourceNote.SongPosition / crotchet : sourceNote.SongPosition;
    }
}
