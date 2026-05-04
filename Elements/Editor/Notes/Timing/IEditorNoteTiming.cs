namespace MLP_RiM.Elements.Editor;

public interface IEditorNoteTiming
{
    double GetStart(EditorNoteDefinition definition, EditorNoteTimingContext context);
    double GetEnd(EditorNoteDefinition definition, EditorNoteTimingContext context);
    double GetHitWindowStart(EditorNoteDefinition definition, EditorNoteTimingContext context);
    double GetHitWindowEnd(EditorNoteDefinition definition, EditorNoteTimingContext context);
    double GetSameVariantHitWindowStart(EditorNoteDefinition definition, EditorNoteTimingContext context);
    double GetSameVariantHitWindowEnd(EditorNoteDefinition definition, EditorNoteTimingContext context);
}
