using System;
using System.Collections.Generic;

namespace MLP_RiM.Elements.Editor.Commands;

public sealed class EditorCommandStack
{
    private readonly Stack<IEditorCommand> _undo = new();
    private readonly Stack<IEditorCommand> _redo = new();

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public string NextUndoName => CanUndo ? _undo.Peek().Name : string.Empty;
    public string NextRedoName => CanRedo ? _redo.Peek().Name : string.Empty;

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }

    public void Execute(IEditorCommand command, BeatmapEditorDocument document)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(document);

        command.Execute(document);
        _undo.Push(command);
        _redo.Clear();
    }

    public bool TryUndo(BeatmapEditorDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!CanUndo)
            return false;

        IEditorCommand command = _undo.Pop();
        command.Undo(document);
        _redo.Push(command);
        return true;
    }

    public bool TryRedo(BeatmapEditorDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (!CanRedo)
            return false;

        IEditorCommand command = _redo.Pop();
        command.Execute(document);
        _undo.Push(command);
        return true;
    }
}
