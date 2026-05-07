using System;
using System.Collections.Generic;
using System.Linq;
using Rhythm.Note;

namespace MLP_RiM.Elements.Editor.Commands;

public sealed class PlaceNotesCommand : IEditorCommand
{
    private readonly IReadOnlyList<EditorNotePlacement> _placements;

    public PlaceNotesCommand(IEnumerable<EditorNotePlacement> placements)
    {
        _placements = placements?
            .Where(placement => placement?.Definition != null && placement.Note != null)
            .Select(placement => new EditorNotePlacement(placement.Definition, EditorCommandCloning.CloneNote(placement.Note)))
            .ToArray() ?? Array.Empty<EditorNotePlacement>();
    }

    public string Name => "Place Notes";
    public IReadOnlyList<ChartNote> PlacedNotes { get; private set; } = Array.Empty<ChartNote>();

    public void Execute(BeatmapEditorDocument document)
    {
        IReadOnlyList<EditorNotePlacement> placements = _placements
            .Select(placement => new EditorNotePlacement(placement.Definition, EditorCommandCloning.CloneNote(placement.Note)))
            .ToArray();

        if (!document.TryPlaceNotes(placements, out IReadOnlyList<ChartNote> placedNotes, out string reason))
            throw new InvalidOperationException(reason);

        PlacedNotes = placedNotes;
    }

    public void Undo(BeatmapEditorDocument document)
    {
        if (!document.RemoveNotes(PlacedNotes))
            throw new InvalidOperationException("Placed notes are not available");
    }
}

public sealed class DeleteNoteCommand : IEditorCommand
{
    private readonly ChartNote _note;
    private ChartNote _snapshot;
    private EditorNoteDefinition _definition;

    public DeleteNoteCommand(ChartNote note)
    {
        _note = note ?? throw new ArgumentNullException(nameof(note));
    }

    public string Name => "Delete Note";

    public void Execute(BeatmapEditorDocument document)
    {
        _snapshot ??= EditorCommandCloning.CloneNote(_note);
        _definition ??= EditorNoteDefinitions.FromChartNote(_note);
        if (!document.RemoveNote(_note))
            throw new InvalidOperationException("Note not found");
    }

    public void Undo(BeatmapEditorDocument document)
    {
        EditorNoteDefinition definition = _definition ?? EditorNoteDefinitions.FromChartNote(_snapshot);
        string reason = null;
        RestoreSnapshotToOriginalNote();
        if (definition == null || !document.TryPlaceNote(definition, _note, out _, out reason))
            throw new InvalidOperationException(reason ?? "Deleted note cannot be restored");
    }

    private void RestoreSnapshotToOriginalNote()
    {
        _note.SongPosition = _snapshot.SongPosition;
        _note.BeatPosition = _snapshot.BeatPosition;
        _note.HoldDuration = _snapshot.HoldDuration;
        _note.HoldBeats = _snapshot.HoldBeats;
        _note.InputActionToPress = _snapshot.InputActionToPress;
        _note.AdditionnalData = new Dictionary<string, string>(_snapshot.AdditionnalData ?? new Dictionary<string, string>());
    }
}

public sealed class MoveNoteCommand : IEditorCommand
{
    private readonly ChartNote _note;
    private readonly double _oldBeat;
    private readonly double _newBeat;

    public MoveNoteCommand(ChartNote note, double oldBeat, double newBeat)
    {
        _note = note ?? throw new ArgumentNullException(nameof(note));
        _oldBeat = oldBeat;
        _newBeat = newBeat;
    }

    public string Name => "Move Note";

    public void Execute(BeatmapEditorDocument document)
    {
        if (!document.MoveNoteToBeat(_note, _newBeat))
            throw new InvalidOperationException("Note not found");
    }

    public void Undo(BeatmapEditorDocument document)
    {
        if (!document.MoveNoteToBeat(_note, _oldBeat))
            throw new InvalidOperationException("Note not found");
    }
}

public sealed class ChangeNoteCommand : IEditorCommand
{
    private readonly ChartNote _note;
    private readonly ChartNote _oldSnapshot;
    private readonly ChartNote _newSnapshot;

    public ChangeNoteCommand(ChartNote note, ChartNote oldSnapshot, ChartNote newSnapshot)
    {
        _note = note ?? throw new ArgumentNullException(nameof(note));
        _oldSnapshot = EditorCommandCloning.CloneNote(oldSnapshot) ?? throw new ArgumentNullException(nameof(oldSnapshot));
        _newSnapshot = EditorCommandCloning.CloneNote(newSnapshot) ?? throw new ArgumentNullException(nameof(newSnapshot));
    }

    public string Name => "Change Note";

    public void Execute(BeatmapEditorDocument document)
    {
        if (!document.ApplyNoteSnapshot(_note, _newSnapshot))
            throw new InvalidOperationException("Note not found");
    }

    public void Undo(BeatmapEditorDocument document)
    {
        if (!document.ApplyNoteSnapshot(_note, _oldSnapshot))
            throw new InvalidOperationException("Note not found");
    }
}

public sealed class PlaceEffectCommand : IEditorCommand
{
    private readonly ChartEffect _effect;

    public PlaceEffectCommand(ChartEffect effect)
    {
        _effect = EditorCommandCloning.CloneEffect(effect) ?? throw new ArgumentNullException(nameof(effect));
    }

    public string Name => "Place Effect";
    public ChartEffect PlacedEffect { get; private set; }

    public void Execute(BeatmapEditorDocument document)
    {
        if (!document.TryPlaceEffect(EditorCommandCloning.CloneEffect(_effect), out ChartEffect placedEffect, out string reason))
            throw new InvalidOperationException(reason);

        PlacedEffect = placedEffect;
    }

    public void Undo(BeatmapEditorDocument document)
    {
        if (!document.RemoveEffect(PlacedEffect))
            throw new InvalidOperationException("Placed effect is not available");
    }
}

public sealed class DeleteEffectCommand : IEditorCommand
{
    private readonly ChartEffect _effect;

    public DeleteEffectCommand(ChartEffect effect)
    {
        _effect = effect ?? throw new ArgumentNullException(nameof(effect));
    }

    public string Name => "Delete Effect";

    public void Execute(BeatmapEditorDocument document)
    {
        if (!document.RemoveEffect(_effect))
            throw new InvalidOperationException("Effect not found");
    }

    public void Undo(BeatmapEditorDocument document)
    {
        if (!document.TryPlaceEffect(_effect, out _, out string reason))
            throw new InvalidOperationException(reason);
    }
}

public sealed class MoveEffectCommand : IEditorCommand
{
    private readonly ChartEffect _effect;
    private readonly double _oldBeat;
    private readonly double _newBeat;
    private readonly bool _sectionOffsetFollowsPosition;

    public MoveEffectCommand(ChartEffect effect, double oldBeat, double newBeat, bool sectionOffsetFollowsPosition)
    {
        _effect = effect ?? throw new ArgumentNullException(nameof(effect));
        _oldBeat = oldBeat;
        _newBeat = newBeat;
        _sectionOffsetFollowsPosition = sectionOffsetFollowsPosition;
    }

    public string Name => "Move Effect";

    public void Execute(BeatmapEditorDocument document)
    {
        if (!document.MoveEffectToBeat(_effect, _newBeat, _sectionOffsetFollowsPosition))
            throw new InvalidOperationException("Effect not found");
    }

    public void Undo(BeatmapEditorDocument document)
    {
        if (!document.MoveEffectToBeat(_effect, _oldBeat, _sectionOffsetFollowsPosition))
            throw new InvalidOperationException("Effect not found");
    }
}

public sealed class ChangeEffectCommand : IEditorCommand
{
    private readonly ChartEffect _effect;
    private readonly ChartEffect _oldSnapshot;
    private readonly ChartEffect _newSnapshot;

    public ChangeEffectCommand(ChartEffect effect, ChartEffect oldSnapshot, ChartEffect newSnapshot)
    {
        _effect = effect ?? throw new ArgumentNullException(nameof(effect));
        _oldSnapshot = EditorCommandCloning.CloneEffect(oldSnapshot) ?? throw new ArgumentNullException(nameof(oldSnapshot));
        _newSnapshot = EditorCommandCloning.CloneEffect(newSnapshot) ?? throw new ArgumentNullException(nameof(newSnapshot));
    }

    public string Name => "Change Effect";

    public void Execute(BeatmapEditorDocument document)
    {
        if (!document.ApplyEffectSnapshot(_effect, _newSnapshot))
            throw new InvalidOperationException("Effect not found");
    }

    public void Undo(BeatmapEditorDocument document)
    {
        if (!document.ApplyEffectSnapshot(_effect, _oldSnapshot))
            throw new InvalidOperationException("Effect not found");
    }
}
