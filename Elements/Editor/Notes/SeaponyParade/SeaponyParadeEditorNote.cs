using System;
using System.Collections.Generic;
using MLP_RiM.Elements.Editor;

public class SeaPonyParadeNoteEditor : EditorNoteProvider
{
    public override EditorNoteDefinition Definition => new EditorNoteDefinitionBuilder(EditorNoteKind.SeaponyParade, "Seapony Parade")
        .HitWindow(0, 2)
        .InputAction("ReactMain")
        .Occupies(1d, 1d)
        .Matches(note => note.AdditionnalData != null
            && note.AdditionnalData.TryGetValue("action", out string action)
            && action.StartsWith("seapony_parade_"))
        .Variant("Swim", new Dictionary<string, string>(){["action"] = "seapony_parade_swim"})
        .Variant("Star", new Dictionary<string, string>(){["action"] = "seapony_parade_star"})
        .Variant("Tap Tap", new Dictionary<string, string>(){["action"] = "seapony_parade_tap_tap"})
        .Build();

    public override IEditorNoteOptionsPanel OptionsPanel => new SeaponyParadeNoteOptionsPanel();
}
