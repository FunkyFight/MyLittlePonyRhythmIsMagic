using System.Collections.Generic;
using GameCore.Scenes;

namespace MLP_RiM.Elements.Editor;

public sealed class SeeSawEditorNote : EditorNoteProvider
{
    public const string GameId = "see_saw";
    public const string SwitchGameClipId = "see_saw.switch_game";
    public const string LongLongClipId = "see_saw.long_long";
    public const string LongShortClipId = "see_saw.long_short";
    public const string ShortLongClipId = "see_saw.short_long";
    public const string ShortShortClipId = "see_saw.short_short";
    public const string ExitClipId = "see_saw.exit";

    public override int SortOrder => 0;

    public override string RhythmGameId => GameId;

    public override string RhythmGameDisplayName => "See Saw";

    public override EditorNoteDefinition Definition { get; } = new EditorNoteDefinitionBuilder(EditorNoteKind.SeeSaw, "See Saw")
        .Occupies(beforeBeats: 4, afterBeats: 4)
        .HitWindow(beforeBeats: 0, afterBeats: 4)
        .Timing(new SeeSawEditorNoteTiming())
        .Matches(SeeSawChartNoteMatcher.Matches)
        .Variant("Default", CreateDefaultData())
        .Build();

    public override IEditorNoteOptionsPanel OptionsPanel { get; } = new SeeSawEditorNoteOptionsPanel();

    public override Scene CreateScene()
    {
        return new SeeSawScene();
    }

    protected override IReadOnlyList<EditorClipDefinition> CreateClips()
    {
        return new[]
        {
            CreateSeeSawClip(LongLongClipId, "Long Long", SeeSawPatternKind.LongLong),
            CreateSeeSawClip(ShortShortClipId, "Short Short", SeeSawPatternKind.ShortShort),
            CreateSeeSawClip(LongShortClipId, "Long Short", SeeSawPatternKind.LongShort),
            CreateSeeSawClip(ShortLongClipId, "Short Long", SeeSawPatternKind.ShortLong),
            Clip(ExitClipId, "Exit", EditorClipCategory.SingleHit, 0, "ReactMain", SeeSawAction.Exit.ToAdditionnalData())
        };
    }

    private static IReadOnlyDictionary<string, string> CreateDefaultData()
    {
        Dictionary<string, string> data = new();
        SeeSawAction.SetPattern(data, SeeSawPatternKind.LongLong);
        return data;
    }

    private EditorClipDefinition CreateSeeSawClip(string clipTypeId, string displayName, SeeSawPatternKind pattern)
    {
        Dictionary<string, string> data = new();
        SeeSawAction.SetPattern(data, pattern);
        return Clip(clipTypeId, displayName, EditorClipCategory.SingleHit, 0, "ReactMain", data);
    }
}
