namespace MLP_RiM.Elements.Editor;

public sealed class SeeSawEditorNote : EditorNoteProvider
{
    public override EditorNoteDefinition Definition { get; } = new EditorNoteDefinitionBuilder(EditorNoteKind.SeeSaw, "See Saw")
        .Occupies(beforeBeats: 4, afterBeats: 4)
        .HitWindow(beforeBeats: 0, afterBeats: 4)
        .Timing(new SeeSawEditorNoteTiming())
        .Matches(SeeSawChartNoteMatcher.Matches)
        .Variant("Default", SeeSawAction.TowardOuter.ToAdditionnalData())
        .Build();

    public override IEditorNoteOptionsPanel OptionsPanel { get; } = new SeeSawEditorNoteOptionsPanel();
}
