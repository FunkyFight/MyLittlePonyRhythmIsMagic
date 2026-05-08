using Rhythm.Note;

namespace MLP_RiM.Elements.Editor;

public sealed class RhythmInputEditorNote : EditorNoteProvider
{
    public static readonly NoteTypeId TypeId = new("core", "rhythm_input");

    public static EditorNoteDefinition DefinitionInstance { get; } = new EditorNoteDefinitionBuilder(TypeId, "Rhythm Input")
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

        if (note.AdditionnalData == null)
            return true;

        if (note.AdditionnalData.ContainsKey(NotePayloadKeys.Game)
            || note.AdditionnalData.ContainsKey(NotePayloadKeys.Type))
            return false;

        return !note.AdditionnalData.ContainsKey(NotePayloadKeys.Action);
    }
}
