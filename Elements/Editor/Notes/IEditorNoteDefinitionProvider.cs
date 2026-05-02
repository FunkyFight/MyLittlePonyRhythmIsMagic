namespace MLP_RiM.Elements.Editor;

public interface IEditorNoteProvider
{
    EditorNoteDefinition Definition { get; }

    IEditorNoteOptionsPanel OptionsPanel { get; }
}

public abstract class EditorNoteProvider : IEditorNoteProvider
{
    public abstract EditorNoteDefinition Definition { get; }

    public virtual IEditorNoteOptionsPanel OptionsPanel => null;
}
