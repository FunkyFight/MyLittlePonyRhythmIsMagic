using System.Collections.Generic;
using System.Linq;
using Rhythm.Note;

namespace MLP_RiM.Elements.Editor;

public static class EditorClipDefinitions
{
    public const string SeeSawGameId = SeeSawEditorNote.GameId;
    public const string SeaponyParadeGameId = SeaPonyParadeNoteEditor.GameId;
    public const string UnknownGameId = "unknown";

    public const string SeeSawLongLong = SeeSawEditorNote.LongLongClipId;
    public const string SeeSawSwitchGame = SeeSawEditorNote.SwitchGameClipId;
    public const string SeeSawLongShort = SeeSawEditorNote.LongShortClipId;
    public const string SeeSawShortLong = SeeSawEditorNote.ShortLongClipId;
    public const string SeeSawShortShort = SeeSawEditorNote.ShortShortClipId;
    public const string SeeSawExit = SeeSawEditorNote.ExitClipId;

    public const string SeaponySwitchGame = SeaPonyParadeNoteEditor.SwitchGameClipId;
    public const string SeaponySwim = SeaPonyParadeNoteEditor.SwimClipId;
    public const string SeaponyRoll = SeaPonyParadeNoteEditor.RollClipId;
    public const string SeaponyTapTap = SeaPonyParadeNoteEditor.TapTapClipId;
    public const string NoHit = "no_hit";

    public const string SwitchGameEventKey = "editor_event";
    public const string SwitchGameEventValue = "switch_game";
    public const string SwitchGameTargetGameKey = "target_game";

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

}
