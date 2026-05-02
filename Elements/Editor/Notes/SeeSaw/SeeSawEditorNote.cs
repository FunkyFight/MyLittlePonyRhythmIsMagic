namespace MLP_RiM.Elements.Editor;

public sealed class SeeSawEditorNote : IEditorNoteDefinitionProvider
{
    public EditorNoteDefinition Create()
    {
        return new EditorNoteDefinitionBuilder(EditorNoteKind.SeeSaw, "See Saw")
            .Occupies(beforeBeats: 4, afterBeats: 4)
            .HitWindow(beforeBeats: 0, afterBeats: 4)
            .Timing(new SeeSawEditorNoteTiming())
            .Matches(SeeSawChartNoteMatcher.Matches)
            .Variant("Default", SeeSawAction.TowardOuter.ToAdditionnalData())
            .Build();
    }
}
