using System;
using System.Collections.Generic;
using System.Linq;

namespace MLP_RiM.Elements.Editor;

public static class EditorNoteOptionsPanels
{
    private static readonly IReadOnlyDictionary<EditorNoteKind, IEditorNoteOptionsPanel> Panels = new Dictionary<EditorNoteKind, IEditorNoteOptionsPanel>
    {
        [EditorNoteKind.SeeSaw] = new SeeSawEditorNoteOptionsPanel()
    };

    public static bool TryGet(EditorNoteKind kind, out IEditorNoteOptionsPanel panel)
    {
        return Panels.TryGetValue(kind, out panel);
    }
}
