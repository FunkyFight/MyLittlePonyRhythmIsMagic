namespace MLP_RiM.Elements.Editor.Commands;

public interface IEditorCommand
{
    string Name { get; }
    void Execute(BeatmapEditorDocument document);
    void Undo(BeatmapEditorDocument document);
}
