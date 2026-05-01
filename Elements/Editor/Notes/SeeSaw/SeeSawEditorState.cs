namespace MLP_RiM.Elements.Editor;

public readonly struct SeeSawEditorState
{
    public bool RainbowIsOuter { get; }
    public bool ApplejackIsOuter { get; }

    public SeeSawEditorState(bool rainbowIsOuter, bool applejackIsOuter)
    {
        RainbowIsOuter = rainbowIsOuter;
        ApplejackIsOuter = applejackIsOuter;
    }
}
