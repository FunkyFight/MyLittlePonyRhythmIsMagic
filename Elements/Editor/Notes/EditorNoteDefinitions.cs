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

    private static readonly IReadOnlyDictionary<NoteTypeId, IEditorNoteOptionsPanel> OptionsPanels = Providers
        .Where(provider => provider.OptionsPanel != null)
        .ToDictionary(provider => provider.Definition.TypeId, provider => provider.OptionsPanel);

    private static readonly IReadOnlyDictionary<string, IEditorNoteProvider> ProvidersByRhythmGameId = GameProviders
        .GroupBy(provider => provider.RhythmGameId)
        .ToDictionary(group => group.Key, group => group.First());

    private static readonly IReadOnlyDictionary<NoteTypeId, IEditorNoteProvider> ProvidersByTypeId = Providers
        .GroupBy(provider => provider.Definition.TypeId)
        .ToDictionary(group => group.Key, group => group.First());

    public static EditorNoteDefinition Get(NoteTypeId typeId)
    {
        return All.First(definition => definition.TypeId == typeId);
    }

    public static EditorNoteDefinition Get(EditorNoteKind kind)
    {
        return Get(EditorNoteKindCompatibility.ToTypeId(kind));
    }

    public static EditorNoteDefinition FromChartNote(ChartNote note)
    {
        if (note == null)
            return null;

        return All.FirstOrDefault(definition => definition.Matches(note));
    }

    public static bool TryGetOptionsPanel(NoteTypeId typeId, out IEditorNoteOptionsPanel panel)
    {
        return OptionsPanels.TryGetValue(typeId, out panel);
    }

    public static bool TryGetOptionsPanel(EditorNoteKind kind, out IEditorNoteOptionsPanel panel)
    {
        return TryGetOptionsPanel(EditorNoteKindCompatibility.ToTypeId(kind), out panel);
    }

    public static bool TryGetProvider(NoteTypeId typeId, out IEditorNoteProvider provider)
    {
        return ProvidersByTypeId.TryGetValue(typeId, out provider);
    }

    public static bool TryGetProvider(EditorNoteKind kind, out IEditorNoteProvider provider)
    {
        return TryGetProvider(EditorNoteKindCompatibility.ToTypeId(kind), out provider);
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
        if (definition != null && ProvidersByTypeId.TryGetValue(definition.TypeId, out IEditorNoteProvider provider))
            return provider.FindVariantIndex(note);

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
