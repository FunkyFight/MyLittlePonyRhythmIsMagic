namespace MLP_RiM.Elements.Editor;

public sealed class RhythmInputEditorNote : IEditorNoteDefinitionProvider
{
    public EditorNoteDefinition Create()
    {
        return new EditorNoteDefinitionBuilder(EditorNoteKind.RhythmInput, "Rhythm Input")
            .Occupies(beforeBeats: 2, afterBeats: 0.25)
            .HitWindow(beforeBeats: 0, afterBeats: 0.25)
            .Build();
    }
}
