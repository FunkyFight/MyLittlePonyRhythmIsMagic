using System;
using System.Collections.Generic;
using MLP_RiM.Elements.DevUI;
using MLP_RiM.Elements.Editor;

public class SeaponyParadeNoteOptionsPanel : IEditorNoteOptionsPanel
{
    private static readonly string[] VariantNames = { "Swim", "Star", "Tap Tap" };
    private static readonly string[] VariantActions = { "seapony_parade_swim", "seapony_parade_star", "seapony_parade_tap_tap" };

    public string Title => "Seapony Parade";

    public IReadOnlyList<DevUiWindowRow> BuildRows(EditorNoteOptionsContext context)
    {
        return new List<DevUiWindowRow>()
        {
            DevUiWindowRow.Dropdown("seapony_variant", "VARIANT", VariantNames, GetSelectedVariantIndex(context),
            selection =>
            {
                int index = Math.Clamp(selection, 0, VariantActions.Length - 1);
                context.GetCurrentNote().AdditionnalData ??= new Dictionary<string, string>();
                context.GetCurrentNote().AdditionnalData["action"] = VariantActions[index];
            })
        };
    }

    private static int GetSelectedVariantIndex(EditorNoteOptionsContext context)
    {
        if (context.Note.AdditionnalData == null || !context.Note.AdditionnalData.TryGetValue("action", out string action))
            return 0;

        for (int i = 0; i < VariantActions.Length; i++)
        {
            if (string.Equals(VariantActions[i], action, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return 0;
    }
}
