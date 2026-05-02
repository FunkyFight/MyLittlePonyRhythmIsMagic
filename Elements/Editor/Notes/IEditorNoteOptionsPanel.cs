using System.Collections.Generic;
using System;
using Microsoft.Xna.Framework;
using MLP_RiM.Elements.DevUI;
using Rhythm.Note;

namespace MLP_RiM.Elements.Editor;

public sealed class EditorNoteOptionsContext
{
    public EditorNoteOptionsContext(ChartNote note, BeatmapEditorDocument document, Rectangle bounds, Func<ChartNote> getCurrentNote = null)
    {
        Note = note;
        Document = document;
        Bounds = bounds;
        GetCurrentNote = getCurrentNote ?? (() => Note);
    }

    public ChartNote Note { get; }
    public BeatmapEditorDocument Document { get; }
    public Rectangle Bounds { get; }
    public Func<ChartNote> GetCurrentNote { get; }
}

public interface IEditorNoteOptionsPanel
{
    string Title { get; }
    IReadOnlyList<DevUiWindowRow> BuildRows(EditorNoteOptionsContext context);
}
