using System.Collections.Generic;
using GameCore.Scenes;
using Microsoft.Xna.Framework;
using Rhythm.Note;

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
    public static readonly NoteTypeId TypeId = new(GameId, "jump");
    private static readonly IReadOnlyList<EditorClipFieldDefinition> JumpClipFields = new[]
    {
        EditorClipFieldDefinition.Bool(SeeSawAction.BigLeapApplejackDataKey, "Applejack Big Leap"),
        EditorClipFieldDefinition.Bool(SeeSawAction.BigLeapRainbowDashDataKey, "Rainbow Dash Big Leap")
    };

    public override int SortOrder => 0;

    public override string RhythmGameId => GameId;

    public override string RhythmGameDisplayName => "See Saw";

    public override EditorNoteDefinition Definition { get; } = new EditorNoteDefinitionBuilder(TypeId, "See Saw")
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

    public override int FindVariantIndex(ChartNote note)
    {
        if (SeeSawAction.TryGetPattern(note?.AdditionnalData, out SeeSawPatternKind pattern))
        {
            return pattern switch
            {
                SeeSawPatternKind.ShortShort => 1,
                SeeSawPatternKind.LongShort => 2,
                SeeSawPatternKind.ShortLong => 3,
                _ => 0
            };
        }

        SeeSawAction action = SeeSawAction.FromAdditionnalData(note?.AdditionnalData);
        return SeeSawAction.GetBaseDirection(action.Direction) == SeeSawDirection.Exit ? 4 : 0;
    }

    public override IReadOnlyDictionary<string, object> CreateTimingContext(Chart chart, ChartTempoMap tempoMap)
    {
        return new Dictionary<string, object>
        {
            [SeeSawEditorNoteTiming.LeadInBeatsContextKey] = ChartTiming.GetLeadInBeats(chart)
        };
    }

    public override bool TryValidateNotes(EditorNoteValidationContext context, out string reason)
    {
        SeeSawTimeline previewTimeline = SeeSawChartCompiler.Compile(context?.Notes, context?.GetNoteBeat, context?.TempoMap, ChartTiming.GetLeadInBeats(context?.Chart));
        if (previewTimeline.Errors.Count > 0)
        {
            reason = previewTimeline.Errors[0];
            return false;
        }

        reason = null;
        return true;
    }

    public override bool AllowsBoundaryTouch(EditorNoteDefinition otherDefinition)
    {
        return otherDefinition != null && otherDefinition.TypeId == Definition.TypeId;
    }

    public override Color GetEditorColor(int variantIndex)
    {
        return variantIndex switch
        {
            1 => Color.LightSalmon,
            2 => Color.Gold,
            3 => Color.MediumPurple,
            4 => Color.OrangeRed,
            _ => Color.Orange
        };
    }

    public override string GetClipTypeIdFromLegacyNote(ChartNote note)
    {
        if (SeeSawAction.TryGetPattern(note?.AdditionnalData, out SeeSawPatternKind pattern))
        {
            return pattern switch
            {
                SeeSawPatternKind.ShortShort => ShortShortClipId,
                SeeSawPatternKind.LongShort => LongShortClipId,
                SeeSawPatternKind.ShortLong => ShortLongClipId,
                _ => LongLongClipId
            };
        }

        SeeSawAction action = SeeSawAction.FromAdditionnalData(note?.AdditionnalData);
        return SeeSawAction.GetBaseDirection(action.Direction) == SeeSawDirection.Exit
            ? ExitClipId
            : LongLongClipId;
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
        return Clip(clipTypeId, displayName, EditorClipCategory.SingleHit, GetPatternLengthBeats(pattern), "ReactMain", data, JumpClipFields);
    }

    private static double GetPatternLengthBeats(SeeSawPatternKind pattern)
    {
        return SeeSawTiming.GetJumpLengthBeats(SeeSawPatternInfo.GetApplejackCueLength(pattern))
            + SeeSawTiming.GetJumpLengthBeats(SeeSawPatternInfo.GetRainbowTargetLength(pattern));
    }
}
