using System.Collections.Generic;
using System.Linq;
using Rhythm.Note;

namespace MLP_RiM.Elements.Editor;

public static class EditorNoteDefinitions
{
    private static readonly IReadOnlyList<IEditorNoteDefinitionProvider> Providers = new IEditorNoteDefinitionProvider[]
    {
        new SeeSawEditorNote()
    };

    public static readonly IReadOnlyList<EditorNoteDefinition> All = Providers
        .Select(provider => provider.Create())
        .ToArray();

    public static EditorNoteDefinition Get(EditorNoteKind kind)
    {
        return All.First(definition => definition.Kind == kind);
    }

    public static EditorNoteDefinition FromChartNote(ChartNote note)
    {
        return All.FirstOrDefault(definition => definition.Matches(note));
    }

    public static int FindVariantIndex(EditorNoteDefinition definition, ChartNote note)
    {
        if (note.AdditionnalData == null)
            return 0;

        for (int i = 0; i < definition.Variants.Count; i++)
        {
            EditorNoteVariant variant = definition.Variants[i];
            if (variant.AdditionnalData.All(pair => note.AdditionnalData.TryGetValue(pair.Key, out string value) && value == pair.Value))
                return i;
        }

        return 0;
    }
}
