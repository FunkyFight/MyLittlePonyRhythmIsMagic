using System.Collections.Generic;

namespace MLP_RiM.Elements.Editor;

public sealed class SeeSawEditorNote : EditorNoteProvider
{
    public override EditorNoteDefinition Definition { get; } = new EditorNoteDefinitionBuilder(EditorNoteKind.SeeSaw, "See Saw")
        .Occupies(beforeBeats: 4, afterBeats: 4)
        .HitWindow(beforeBeats: 0, afterBeats: 4)
        .Timing(new SeeSawEditorNoteTiming())
        .Matches(SeeSawChartNoteMatcher.Matches)
        .Variant("Default", CreateDefaultData())
        .Build();

    public override IEditorNoteOptionsPanel OptionsPanel { get; } = new SeeSawEditorNoteOptionsPanel();

    private static IReadOnlyDictionary<string, string> CreateDefaultData()
    {
        Dictionary<string, string> data = new();
        SeeSawAction.SetPattern(data, SeeSawPatternKind.LongLong);
        return data;
    }
}
