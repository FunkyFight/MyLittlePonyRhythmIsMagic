using Rhythm.Note;

namespace MLP_RiM.Elements.Editor;

public sealed class RhythmInputEditorNote : EditorNoteProvider
{
    public static EditorNoteDefinition DefinitionInstance { get; } = new EditorNoteDefinitionBuilder(EditorNoteKind.RhythmInput, "Rhythm Input")
        .InputAction("ReactMain")
        .Matches(MatchesRhythmInput)
        .Variant("Default")
        .Build();

    public override EditorNoteDefinition Definition => DefinitionInstance;

    private static bool MatchesRhythmInput(ChartNote note)
    {
        if (note == null)
            return false;

        if (!string.IsNullOrWhiteSpace(note.InputActionToPress) && note.InputActionToPress != "ReactMain")
            return false;

        return note.AdditionnalData == null || !note.AdditionnalData.ContainsKey("action");
    }
}
