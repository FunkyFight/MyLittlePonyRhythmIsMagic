using System.Collections.Generic;
using System.Linq;

namespace MLP_RiM.Elements.Editor;

public static class EditorClipDefinitions
{
    public const string SeeSawGameId = "see_saw";
    public const string SeaponyParadeGameId = "seapony_parade";
    public const string UnknownGameId = "unknown";

    public const string SeeSawLongLong = "see_saw.long_long";
    public const string SeeSawLongShort = "see_saw.long_short";
    public const string SeeSawShortLong = "see_saw.short_long";
    public const string SeeSawShortShort = "see_saw.short_short";
    public const string SeeSawExit = "see_saw.exit";

    public const string SeaponySwim = "seapony_parade.swim";
    public const string SeaponyRoll = "seapony_parade.roll";
    public const string SeaponyTapTap = "seapony_parade.tap_tap";
    public const string NoHit = "no_hit";

    public static readonly IReadOnlyList<EditorRhythmGameDefinition> Games = new[]
    {
        new EditorRhythmGameDefinition(SeeSawGameId, "See Saw", new[]
        {
            CreateSeeSaw(SeeSawLongLong, "Long Long", SeeSawPatternKind.LongLong),
            CreateSeeSaw(SeeSawShortShort, "Short Short", SeeSawPatternKind.ShortShort),
            CreateSeeSaw(SeeSawLongShort, "Long Short", SeeSawPatternKind.LongShort),
            CreateSeeSaw(SeeSawShortLong, "Short Long", SeeSawPatternKind.ShortLong),
            new EditorClipDefinition(SeeSawGameId, SeeSawExit, "Exit", EditorClipCategory.SingleHit, 0, "ReactMain", SeeSawAction.Exit.ToAdditionnalData())
        }),
        new EditorRhythmGameDefinition(SeaponyParadeGameId, "Seapony Parade", new[]
        {
            new EditorClipDefinition(SeaponyParadeGameId, SeaponySwim, "Swim", EditorClipCategory.Continuous, 2, "ReactMain", new Dictionary<string, string> { ["action"] = "seapony_parade_swim" }),
            new EditorClipDefinition(SeaponyParadeGameId, SeaponyRoll, "Roll", EditorClipCategory.Continuous, 3, "ReactMain", new Dictionary<string, string> { ["action"] = "seapony_parade_roll" }),
            new EditorClipDefinition(SeaponyParadeGameId, SeaponyTapTap, "Tap Tap", EditorClipCategory.SingleHit, 0, "ReactMain", new Dictionary<string, string> { ["action"] = "seapony_parade_tap_tap" }),
            new EditorClipDefinition(SeaponyParadeGameId, NoHit, "No Hit", EditorClipCategory.NoHit, 1, "ReactMain")
        })
    };

    public static readonly IReadOnlyList<EditorClipDefinition> All = Games.SelectMany(game => game.Clips).ToArray();

    public static EditorClipDefinition Find(string rhythmGameId, string clipTypeId)
    {
        return All.FirstOrDefault(definition => definition.RhythmGameId == rhythmGameId && definition.ClipTypeId == clipTypeId)
            ?? All.FirstOrDefault(definition => definition.ClipTypeId == clipTypeId);
    }

    private static EditorClipDefinition CreateSeeSaw(string clipTypeId, string displayName, SeeSawPatternKind pattern)
    {
        Dictionary<string, string> data = new();
        SeeSawAction.SetPattern(data, pattern);
        return new EditorClipDefinition(SeeSawGameId, clipTypeId, displayName, EditorClipCategory.SingleHit, 0, "ReactMain", data);
    }
}
