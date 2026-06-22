using System.Collections.Generic;
using System.Linq;
using Rhythm.Note;

namespace MLP_RiM.Elements.Editor;

public static class EditorClipDefinitions
{
    public const string UnknownGameId = "unknown";
    public const string NoHit = "no_hit";

    public const string SwitchGameEventKey = "editor_event";
    public const string SwitchGameEventValue = "switch_game";
    public const string SwitchGameTargetGameKey = "target_game";
    public const string BlackAndWhiteToggleEventValue = "black_and_white_toggle";
    public const string ViewportOffsetEventValue = "viewport_offset";
    public const string FlashEventValue = "flash";
    public const string SaturationEventValue = "saturation";

    public static readonly IReadOnlyList<EditorRhythmGameDefinition> Games = EditorNoteDefinitions.GameProviders
        .Select(provider => new EditorRhythmGameDefinition(provider.RhythmGameId, provider.RhythmGameDisplayName, provider.Clips))
        .ToArray();

    public static readonly IReadOnlyList<EditorClipDefinition> All = Games.SelectMany(game => game.Clips).ToArray();

    public static EditorClipDefinition Find(string rhythmGameId, string clipTypeId)
    {
        return All.FirstOrDefault(definition => definition.RhythmGameId == rhythmGameId && definition.ClipTypeId == clipTypeId)
            ?? All.FirstOrDefault(definition => definition.ClipTypeId == clipTypeId);
    }

    public static bool IsSwitchGame(EditorClipDefinition definition)
    {
        return definition != null
            && definition.Category == EditorClipCategory.Instant
            && definition.DefaultData.TryGetValue(SwitchGameEventKey, out string editorEvent)
            && editorEvent == SwitchGameEventValue;
    }

    public static bool IsSwitchGame(ChartEditorClip clip)
    {
        if (clip == null)
            return false;

        EditorClipDefinition definition = Find(clip.RhythmGameId, clip.ClipTypeId);
        return IsSwitchGame(definition)
            || clip.Data.TryGetValue(SwitchGameEventKey, out string editorEvent)
            && editorEvent == SwitchGameEventValue;
    }

    public static string GetSwitchGameTargetGameId(ChartEditorClip clip)
    {
        if (clip == null)
            return null;

        Dictionary<string, string> data = clip.Data;
        if (data.TryGetValue(SwitchGameTargetGameKey, out string targetGame) && !string.IsNullOrWhiteSpace(targetGame))
            return targetGame;

        EditorClipDefinition definition = Find(clip.RhythmGameId, clip.ClipTypeId);
        return definition?.RhythmGameId ?? clip.RhythmGameId;
    }

    public static bool IsBlackAndWhiteToggle(ChartEditorClip clip)
    {
        if (clip == null)
            return false;

        EditorClipDefinition definition = Find(clip.RhythmGameId, clip.ClipTypeId);
        return IsBlackAndWhiteToggle(definition)
            || clip.Data.TryGetValue(SwitchGameEventKey, out string editorEvent)
            && editorEvent == BlackAndWhiteToggleEventValue;
    }

    public static bool IsBlackAndWhiteToggle(EditorClipDefinition definition)
    {
        return definition != null
            && definition.Category == EditorClipCategory.Instant
            && definition.DefaultData.TryGetValue(SwitchGameEventKey, out string editorEvent)
            && editorEvent == BlackAndWhiteToggleEventValue;
    }

    public static bool IsViewportOffset(ChartEditorClip clip)
    {
        if (clip == null)
            return false;

        EditorClipDefinition definition = Find(clip.RhythmGameId, clip.ClipTypeId);
        return IsViewportOffset(definition)
            || clip.Data.TryGetValue(SwitchGameEventKey, out string editorEvent)
            && editorEvent == ViewportOffsetEventValue;
    }

    public static bool IsViewportOffset(EditorClipDefinition definition)
    {
        return definition != null
            && definition.DefaultData.TryGetValue(SwitchGameEventKey, out string editorEvent)
            && editorEvent == ViewportOffsetEventValue;
    }

    public static bool IsFlash(ChartEditorClip clip)
    {
        if (clip == null)
            return false;

        EditorClipDefinition definition = Find(clip.RhythmGameId, clip.ClipTypeId);
        return IsFlash(definition)
            || clip.Data.TryGetValue(SwitchGameEventKey, out string editorEvent)
            && editorEvent == FlashEventValue;
    }

    public static bool IsFlash(EditorClipDefinition definition)
    {
        return definition != null
            && definition.DefaultData.TryGetValue(SwitchGameEventKey, out string editorEvent)
            && editorEvent == FlashEventValue;
    }

    public static bool IsSaturation(ChartEditorClip clip)
    {
        if (clip == null)
            return false;

        EditorClipDefinition definition = Find(clip.RhythmGameId, clip.ClipTypeId);
        return IsSaturation(definition)
            || clip.Data.TryGetValue(SwitchGameEventKey, out string editorEvent)
            && editorEvent == SaturationEventValue;
    }

    public static bool IsSaturation(EditorClipDefinition definition)
    {
        return definition != null
            && definition.Category == EditorClipCategory.Instant
            && definition.DefaultData.TryGetValue(SwitchGameEventKey, out string editorEvent)
            && editorEvent == SaturationEventValue;
    }

}
