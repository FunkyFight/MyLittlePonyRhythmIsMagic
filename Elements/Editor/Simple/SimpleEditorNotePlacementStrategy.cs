using System;
using System.Collections.Generic;
using System.Linq;
using Rhythm.Note;

namespace MLP_RiM.Elements.Editor;

/// <summary>
/// Strategie de placement direct utilisee par les jeux declares avec <see cref="SimpleRhythmGame{TAction}"/>.
/// </summary>
/// <typeparam name="TAction">Enum qui liste les actions runtime du rhythm game.</typeparam>
public sealed class SimpleEditorNotePlacementStrategy<TAction> : IEditorNotePlacementStrategy
    where TAction : struct, Enum
{
    private readonly RhythmGameDefinition<TAction> _game;

    internal SimpleEditorNotePlacementStrategy(RhythmGameDefinition<TAction> game)
    {
        _game = game;
    }

    /// <summary>
    /// Cree les placements reels a partir d'une note source et des options de repetition de l'editeur.
    /// </summary>
    /// <param name="definition">Definition editeur de la note simple.</param>
    /// <param name="sourceNote">Note source creee par l'interface.</param>
    /// <param name="context">Contexte de placement.</param>
    /// <param name="placementOptions">Options de repetition.</param>
    /// <returns>Placements a ajouter a la chart.</returns>
    public IReadOnlyList<EditorNotePlacement> CreatePlacements(EditorNoteDefinition definition, ChartNote sourceNote, EditorNotePlacementContext context, PlacementOptions placementOptions)
    {
        if (definition == null || sourceNote == null || context == null)
            return Array.Empty<EditorNotePlacement>();

        if (!_game.Codec.TryReadAction(sourceNote.AdditionnalData, out TAction action))
            action = _game.Codec.ReadAction(sourceNote.AdditionnalData);

        double sourceBeat = GetSourceBeat(sourceNote, context.Crotchet);
        Dictionary<string, string> data = EditorNotePlacementData.CreateStoredAdditionnalData(sourceNote);
        data = _game.Codec.WithAction(data, action);
        IReadOnlyList<RuntimeNoteDraft> drafts = _game.CompilePlacement(
            sourceNote,
            sourceBeat,
            data,
            action,
            new NoteCompileContext(null, context.ExistingNotes),
            placementOptions ?? PlacementOptions.None);

        return drafts
            .Select(draft => new EditorNotePlacement(definition, CreateNoteFromDraft(sourceNote, draft, sourceBeat, context.Crotchet)))
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
