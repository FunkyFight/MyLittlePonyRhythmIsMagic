using System;
using System.Collections.Generic;
using System.Linq;
using GameCore.Scenes;
using Rhythm.Note;

namespace MLP_RiM.Elements.Editor;

public static class EditorNoteDefinitions
{
    public static readonly IReadOnlyList<IEditorNoteProvider> Providers = CreateProviders();

    public static readonly IReadOnlyList<IEditorNoteProvider> GameProviders = Providers
        .Where(provider => !string.IsNullOrWhiteSpace(provider.RhythmGameId))
        .ToArray();

    public static readonly IReadOnlyList<EditorNoteDefinition> All = Providers
        .Select(provider => provider.Definition)
        .ToArray();

    private static readonly IReadOnlyDictionary<EditorNoteKind, IEditorNoteOptionsPanel> OptionsPanels = Providers
        .Where(provider => provider.OptionsPanel != null)
        .ToDictionary(provider => provider.Definition.Kind, provider => provider.OptionsPanel);

    private static readonly IReadOnlyDictionary<string, IEditorNoteProvider> ProvidersByRhythmGameId = GameProviders
        .GroupBy(provider => provider.RhythmGameId)
        .ToDictionary(group => group.Key, group => group.First());

    private static readonly IReadOnlyDictionary<EditorNoteKind, IEditorNoteProvider> ProvidersByKind = Providers
        .GroupBy(provider => provider.Definition.Kind)
        .ToDictionary(group => group.Key, group => group.First());

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

    public static bool TryGetProvider(EditorNoteKind kind, out IEditorNoteProvider provider)
    {
        return ProvidersByKind.TryGetValue(kind, out provider);
    }

    public static bool TryGetProvider(string rhythmGameId, out IEditorNoteProvider provider)
    {
        provider = null;
        return !string.IsNullOrWhiteSpace(rhythmGameId)
            && ProvidersByRhythmGameId.TryGetValue(rhythmGameId, out provider);
    }

    public static bool TryCreateScene(string rhythmGameId, out Scene scene)
    {
        scene = null;
        if (string.IsNullOrWhiteSpace(rhythmGameId)
            || !ProvidersByRhythmGameId.TryGetValue(rhythmGameId, out IEditorNoteProvider provider))
            return false;

        scene = provider.CreateScene();
        return scene != null;
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

    private static IReadOnlyList<IEditorNoteProvider> CreateProviders()
    {
        Type providerType = typeof(IEditorNoteProvider);
        return typeof(EditorNoteDefinitions).Assembly.GetTypes()
            .Where(type => !type.IsAbstract
                && providerType.IsAssignableFrom(type)
                && type.GetConstructor(Type.EmptyTypes) != null)
            .Select(type => (IEditorNoteProvider)Activator.CreateInstance(type))
            .OrderBy(provider => provider.SortOrder)
            .ThenBy(provider => provider.RhythmGameDisplayName ?? provider.Definition.DisplayName)
            .ToArray();
    }
}
