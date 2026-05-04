using System;
using System.Collections.Generic;
using MLP_RiM.Elements.DevUI;
using MLP_RiM.Elements.Editor;
using Rhythm.Note;

public class SeaponyParadeNoteOptionsPanel : IEditorNoteOptionsPanel
{
    private static readonly string[] VariantNames = { "Swim", "Roll", "Tap Tap" };
    private static readonly string[] VariantActions = { "seapony_parade_swim", "seapony_parade_roll", "seapony_parade_tap_tap" };

    public string Title => "Seapony Parade";

    public IReadOnlyList<DevUiWindowRow> BuildRows(EditorNoteOptionsContext context)
    {
        int selectedVariantIndex = GetSelectedVariantIndex(context);
        List<DevUiWindowRow> rows = new()
        {
            DevUiWindowRow.Dropdown("seapony_variant", "VARIANT", VariantNames, selectedVariantIndex,
            selection =>
            {
                int index = Math.Clamp(selection, 0, VariantActions.Length - 1);
                ChartNote note = context.GetCurrentNote();
                Dictionary<string, string> data = note.AdditionnalData ?? new Dictionary<string, string>();
                data["action"] = VariantActions[index];
                note.AdditionnalData = data;
            })
        };

        if (selectedVariantIndex == 1)
            rows.Add(DevUiWindowRow.Title("Last Roll completes the series to a multiple of 4"));

        return rows;
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
