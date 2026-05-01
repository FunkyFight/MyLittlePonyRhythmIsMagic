namespace MLP_RiM.Elements.Editor;

public sealed class SeeSawEditorNote : IEditorNoteDefinitionProvider
{
    public EditorNoteDefinition Create()
    {
        return new EditorNoteDefinitionBuilder(EditorNoteKind.SeeSaw, "See Saw")
            .Occupies(beforeBeats: 3, afterBeats: 3)
            .HitWindow(beforeBeats: 0, afterBeats: 3)
            .Timing(new SeeSawEditorNoteTiming())
            .Matches(SeeSawChartNoteMatcher.Matches)
            .Variant("SeeSawTowardOuter", SeeSawAction.TowardOuter.ToAdditionnalData())
            .Variant("SeeSawTowardInner", SeeSawAction.TowardInner.ToAdditionnalData())
            .Variant("SeeSawTowardOpposite", SeeSawAction.TowardOpposite.ToAdditionnalData())
            .Variant("SeeSawTowardOuterBigLeap", SeeSawAction.TowardOuterBigLeap.ToAdditionnalData())
            .Variant("SeeSawTowardInnerBigLeap", SeeSawAction.TowardInnerBigLeap.ToAdditionnalData())
            .Variant("SeeSawTowardOppositeBigLeap", SeeSawAction.TowardOppositeBigLeap.ToAdditionnalData())
            .Build();
    }
}
