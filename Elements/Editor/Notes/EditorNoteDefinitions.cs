using System.Collections.Generic;
using System.Linq;
using Rhythm.Note;

namespace MLP_RiM.Elements.Editor;

public static class EditorNoteDefinitions
{
    private static readonly IReadOnlyList<IEditorNoteProvider> Providers = new IEditorNoteProvider[]
    {
        new SeeSawEditorNote()
    };

    public static readonly IReadOnlyList<EditorNoteDefinition> All = Providers
        .Select(provider => provider.Definition)
        .ToArray();

    private static readonly IReadOnlyDictionary<EditorNoteKind, IEditorNoteOptionsPanel> OptionsPanels = Providers
        .Where(provider => provider.OptionsPanel != null)
        .ToDictionary(provider => provider.Definition.Kind, provider => provider.OptionsPanel);

    public static EditorNoteDefinition Get(EditorNoteKind kind)
    {
        return All.First(definition => definition.Kind == kind);
    }

    public static EditorNoteDefinition FromChartNote(ChartNote note)
    {
        if (note == null)
            return null;

        return All.FirstOrDefault(definition => definition.Matches(note));
    }

    public static bool TryGetOptionsPanel(EditorNoteKind kind, out IEditorNoteOptionsPanel panel)
    {
        return OptionsPanels.TryGetValue(kind, out panel);
    }

    public static int FindVariantIndex(EditorNoteDefinition definition, ChartNote note)
    {
        if (note.AdditionnalData == null)
            return 0;

        for (int i = 0; i < definition.Variants.Count; i++)
        {
            EditorNoteVariant variant = definition.Variants[i];
            if (definition.Kind == EditorNoteKind.SeeSaw
                && variant.AdditionnalData.TryGetValue("action", out string variantAction)
                && note.AdditionnalData.TryGetValue("action", out string noteAction)
                && SeeSawAction.TryParse(noteAction, out SeeSawAction parsedAction)
                && SeeSawAction.TryParse(variantAction, out SeeSawAction parsedVariantAction)
                && SeeSawAction.GetBaseDirection(parsedAction.Direction) == SeeSawAction.GetBaseDirection(parsedVariantAction.Direction))
            {
                return i;
            }

            if (variant.AdditionnalData.All(pair => note.AdditionnalData.TryGetValue(pair.Key, out string value) && value == pair.Value))
                return i;
        }

        return 0;
    }
}
