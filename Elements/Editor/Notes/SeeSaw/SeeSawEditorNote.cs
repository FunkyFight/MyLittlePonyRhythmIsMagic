using System.Collections.Generic;
using GameCore.Scenes;
using Microsoft.Xna.Framework;
using Rhythm.Note;

namespace MLP_RiM.Elements.Editor;

public sealed class SeeSawEditorNote : EditorNoteProvider
{
    public const string GameId = "see_saw";
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
        .Variant("Long Long", CreatePatternData(SeeSawPatternKind.LongLong))
        .Variant("Short Short", CreatePatternData(SeeSawPatternKind.ShortShort))
        .Variant("Long Short", CreatePatternData(SeeSawPatternKind.LongShort))
        .Variant("Short Long", CreatePatternData(SeeSawPatternKind.ShortLong))
        .Variant("Exit", SeeSawAction.Exit.ToAdditionnalData())
        .Build();

    public override IEditorNoteOptionsPanel OptionsPanel { get; } = new SeeSawEditorNoteOptionsPanel();

    public override Scene CreateScene()
    {
        return new SeeSawScene();
    }

    public override int GetNoteVariantIndex(ChartNote note)
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

    public override EditorVisualStyle GetEditorStyle(ChartNote note)
    {
        Color color = GetNoteVariantIndex(note) switch
        {
            1 => Color.LightSalmon,
            2 => Color.Gold,
            3 => Color.MediumPurple,
            4 => Color.OrangeRed,
            _ => Color.Orange
        };

        return new EditorVisualStyle(color);
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
            CreateSeeSawClip(LongLongClipId, "Long Long", SeeSawPatternKind.LongLong, Color.Orange),
            CreateSeeSawClip(ShortShortClipId, "Short Short", SeeSawPatternKind.ShortShort, Color.LightSalmon),
            CreateSeeSawClip(LongShortClipId, "Long Short", SeeSawPatternKind.LongShort, Color.Gold),
            CreateSeeSawClip(ShortLongClipId, "Short Long", SeeSawPatternKind.ShortLong, Color.MediumPurple),
            Clip(ExitClipId, "Exit", EditorClipCategory.SingleHit, 0, "ReactMain", SeeSawAction.Exit.ToAdditionnalData(), editorStyle: new EditorVisualStyle(Color.OrangeRed))
        };
    }

    private static IReadOnlyDictionary<string, string> CreatePatternData(SeeSawPatternKind pattern)
    {
        Dictionary<string, string> data = new();
        SeeSawAction.SetPattern(data, pattern);
        return data;
    }

    private EditorClipDefinition CreateSeeSawClip(string clipTypeId, string displayName, SeeSawPatternKind pattern, Color color)
    {
        return Clip(clipTypeId, displayName, EditorClipCategory.SingleHit, GetPatternLengthBeats(pattern), "ReactMain", CreatePatternData(pattern), JumpClipFields, new EditorVisualStyle(color));
    }

    private static double GetPatternLengthBeats(SeeSawPatternKind pattern)
    {
        return SeeSawTiming.GetJumpLengthBeats(SeeSawPatternInfo.GetApplejackCueLength(pattern))
            + SeeSawTiming.GetJumpLengthBeats(SeeSawPatternInfo.GetRainbowTargetLength(pattern));
    }
}
