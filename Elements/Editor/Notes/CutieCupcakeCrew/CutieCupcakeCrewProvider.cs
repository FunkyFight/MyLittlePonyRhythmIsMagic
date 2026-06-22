using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using GameCore.Scenes;
using Microsoft.Xna.Framework;
using Rhythm.Note;

namespace MLP_RiM.Elements.Editor;

public enum CutieCupcakeCrewAction
{
    PinkiePieFrost,
    PinkiePiePersonalTouch,
    SweetieBelleFrost,
    SweetieBellePersonalTouch,
    ScootalooFrost,
    ScootalooPersonalTouch,
    AppleBloomFrostHit,
    AppleBloomPersonalTouchHit
}

public sealed record CutieCupcakeCrewNotePayload(CutieCupcakeCrewAction Action) : INotePayload
{
    public string GameId => CutieCupcakeCrewNoteCodec.GameId;
    public string NoteId => CutieCupcakeCrewNoteCodec.NoteId;
    public int SchemaVersion => CutieCupcakeCrewNoteCodec.SchemaVersion;

    public Dictionary<string, string> ToLegacyData()
    {
        return CutieCupcakeCrewNoteCodec.Write(this);
    }
}

public static class CutieCupcakeCrewNoteCodec
{
    public const string GameId = "cutie_cupcake_crew";
    public const string NoteId = "note";
    public const int SchemaVersion = 1;

    private static readonly EnumNoteCodec<CutieCupcakeCrewAction> Codec = new(GameId, NoteId, schemaVersion: SchemaVersion);

    public static bool TryReadAction(IReadOnlyDictionary<string, string> data, out CutieCupcakeCrewAction action)
    {
        return Codec.TryReadAction(data, out action);
    }

    public static bool IsAction(IReadOnlyDictionary<string, string> data, CutieCupcakeCrewAction expected)
    {
        return Codec.IsAction(data, expected);
    }

    public static bool Matches(IReadOnlyDictionary<string, string> data)
    {
        return Codec.Matches(data);
    }

    public static Dictionary<string, string> Write(CutieCupcakeCrewAction action)
    {
        return Codec.Write(action);
    }

    public static Dictionary<string, string> Write(CutieCupcakeCrewNotePayload payload)
    {
        return Codec.Write(payload?.Action ?? CutieCupcakeCrewAction.PinkiePieFrost);
    }

    public static string GetVariantId(CutieCupcakeCrewAction action)
    {
        return Codec.GetVariantId(action);
    }
}

public sealed class CutieCupcakeCrewProvider : EditorNoteProvider
{
    public const string GameId = CutieCupcakeCrewNoteCodec.GameId;
    public const string EntryClipId = "cutie_cupcake_crew.entry";
    public const string FrostClipId = "cutie_cupcake_crew.frost";
    public const string TogetherFrostClipId = "cutie_cupcake_crew.together_frost";
    public const string PersonalTouchClipId = "cutie_cupcake_crew.personal_touch";
    public const string BeatSpacingDataKey = "beat_spacing";
    public const string SourceClipDataKey = "source_clip";
    public const double DefaultBeatSpacing = 1.0;
    public static readonly NoteTypeId TypeId = new(GameId, CutieCupcakeCrewNoteCodec.NoteId);

    private const string PlayerInputAction = "ReactMain";

    private static readonly IReadOnlyList<EditorClipFieldDefinition> TimingFields = new[]
    {
        EditorClipFieldDefinition.Float(BeatSpacingDataKey, "x beats", DefaultBeatSpacing, minValue: 0.001, maxValue: 16)
    };

    private static readonly IReadOnlyDictionary<string, string> DefaultClipData = new Dictionary<string, string>
    {
        [BeatSpacingDataKey] = DefaultBeatSpacing.ToString("0.###", CultureInfo.InvariantCulture)
    };

    private static readonly IReadOnlyList<CutieCupcakeCrewAction> Actions = new[]
    {
        CutieCupcakeCrewAction.PinkiePieFrost,
        CutieCupcakeCrewAction.PinkiePiePersonalTouch,
        CutieCupcakeCrewAction.SweetieBelleFrost,
        CutieCupcakeCrewAction.SweetieBellePersonalTouch,
        CutieCupcakeCrewAction.ScootalooFrost,
        CutieCupcakeCrewAction.ScootalooPersonalTouch,
        CutieCupcakeCrewAction.AppleBloomFrostHit,
        CutieCupcakeCrewAction.AppleBloomPersonalTouchHit
    };

    public override int SortOrder => 30;

    public override string RhythmGameId => GameId;

    public override string RhythmGameDisplayName => "Cutie Cupcake Crew";

    public override EditorNoteDefinition Definition { get; } = CreateDefinition();

    public override Scene CreateScene()
    {
        return new global::CutieCupcakeCrew();
    }

    public override IReadOnlyList<ChartNote> CompileClip(ChartEditorClip clip, ChartTempoMap tempoMap)
    {
        if (clip == null || tempoMap == null)
            return Array.Empty<ChartNote>();

        Dictionary<string, string> data = CreateClipData(clip, FindClipDefinition(clip));
        double spacing = GetBeatSpacing(data);
        return clip.ClipTypeId switch
        {
            FrostClipId => CompileFrostClip(clip, tempoMap, spacing),
            TogetherFrostClipId => CompileTogetherFrostClip(clip, tempoMap, spacing),
            PersonalTouchClipId => CompilePersonalTouchClip(clip, tempoMap, spacing),
            _ => Array.Empty<ChartNote>()
        };
    }

    public override string GetClipTypeIdFromLegacyNote(ChartNote note)
    {
        string sourceClipId = GetSourceClipId(note?.AdditionnalData);
        if (sourceClipId == FrostClipId || sourceClipId == TogetherFrostClipId || sourceClipId == PersonalTouchClipId)
            return sourceClipId;

        if (!CutieCupcakeCrewNoteCodec.TryReadAction(note?.AdditionnalData, out CutieCupcakeCrewAction action))
            return FrostClipId;

        return action is CutieCupcakeCrewAction.PinkiePiePersonalTouch
            or CutieCupcakeCrewAction.SweetieBellePersonalTouch
            or CutieCupcakeCrewAction.ScootalooPersonalTouch
            or CutieCupcakeCrewAction.AppleBloomPersonalTouchHit
            ? PersonalTouchClipId
            : FrostClipId;
    }

    public override int GetNoteVariantIndex(ChartNote note)
    {
        if (!CutieCupcakeCrewNoteCodec.TryReadAction(note?.AdditionnalData, out CutieCupcakeCrewAction action))
            return 0;

        int index = Actions.ToList().IndexOf(action);
        return index >= 0 ? index : 0;
    }

    public override EditorVisualStyle GetEditorStyle(ChartNote note)
    {
        return new EditorVisualStyle(GetActionColor(GetAction(note)));
    }

    protected override IReadOnlyList<EditorClipDefinition> CreateClips()
    {
        return new[]
        {
            Clip(EntryClipId, "Entry", EditorClipCategory.NoHit, 4 * DefaultBeatSpacing, string.Empty, DefaultClipData, TimingFields, new EditorVisualStyle(Color.LightPink)),
            Clip(FrostClipId, "Frost", EditorClipCategory.SingleHit, 6 * DefaultBeatSpacing, PlayerInputAction, DefaultClipData, TimingFields, new EditorVisualStyle(Color.LightSkyBlue)),
            Clip(TogetherFrostClipId, "Together Frost", EditorClipCategory.SingleHit, 5 * DefaultBeatSpacing, PlayerInputAction, DefaultClipData, TimingFields, new EditorVisualStyle(Color.LightCyan)),
            Clip(PersonalTouchClipId, "Personal Touch", EditorClipCategory.SingleHit, 10 * DefaultBeatSpacing, PlayerInputAction, DefaultClipData, TimingFields, new EditorVisualStyle(Color.HotPink))
        };
    }

    private static EditorNoteDefinition CreateDefinition()
    {
        EditorNoteDefinitionBuilder builder = new EditorNoteDefinitionBuilder(TypeId, "Cutie Cupcake Crew")
            .InputAction(PlayerInputAction)
            .Occupies(0, 0)
            .HitWindow(0, 0)
            .Timing(new CutieCupcakeCrewEditorNoteTiming())
            .Matches(note => CutieCupcakeCrewNoteCodec.Matches(note?.AdditionnalData));

        foreach (CutieCupcakeCrewAction action in Actions)
        {
            builder.Variant(
                CutieCupcakeCrewNoteCodec.GetVariantId(action),
                GetActionDisplayName(action),
                new CutieCupcakeCrewNotePayload(action),
                payload => payload is CutieCupcakeCrewNotePayload cutiePayload && cutiePayload.Action == action,
                editorStyle: new EditorVisualStyle(GetActionColor(action)));
        }

        return builder.Build();
    }

    private static IReadOnlyList<ChartNote> CompileFrostClip(ChartEditorClip clip, ChartTempoMap tempoMap, double spacing)
    {
        return new[]
        {
            CreateNote(clip, tempoMap, 4 * spacing, CutieCupcakeCrewAction.AppleBloomFrostHit, PlayerInputAction, spacing, FrostClipId)
        };
    }

    private static IReadOnlyList<ChartNote> CompilePersonalTouchClip(ChartEditorClip clip, ChartTempoMap tempoMap, double spacing)
    {
        return new[]
        {
            CreateNote(clip, tempoMap, 7 * spacing, CutieCupcakeCrewAction.AppleBloomFrostHit, PlayerInputAction, spacing, PersonalTouchClipId),
            CreateNote(clip, tempoMap, 8 * spacing, CutieCupcakeCrewAction.AppleBloomPersonalTouchHit, PlayerInputAction, spacing, PersonalTouchClipId)
        };
    }

    private static IReadOnlyList<ChartNote> CompileTogetherFrostClip(ChartEditorClip clip, ChartTempoMap tempoMap, double spacing)
    {
        return new[]
        {
            CreateNote(clip, tempoMap, 3 * spacing, CutieCupcakeCrewAction.AppleBloomFrostHit, PlayerInputAction, spacing, TogetherFrostClipId)
        };
    }

    private static ChartNote CreateNote(ChartEditorClip clip, ChartTempoMap tempoMap, double offsetBeats, CutieCupcakeCrewAction action, string inputAction, double spacing, string sourceClipId)
    {
        double beat = clip.StartBeat + offsetBeats;
        Dictionary<string, string> data = CutieCupcakeCrewNoteCodec.Write(action);
        data[BeatSpacingDataKey] = spacing.ToString("0.###", CultureInfo.InvariantCulture);
        data[SourceClipDataKey] = sourceClipId;

        return new ChartNote
        {
            SongPosition = tempoMap.BeatToSeconds(beat),
            BeatPosition = beat,
            HoldDuration = 0,
            HoldBeats = 0,
            InputActionToPress = inputAction,
            AdditionnalData = data
        };
    }

    public static double GetBeatSpacing(IReadOnlyDictionary<string, string> data)
    {
        if (data != null
            && data.TryGetValue(BeatSpacingDataKey, out string rawValue)
            && double.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out double spacing)
            && spacing > 0.0)
            return spacing;

        return DefaultBeatSpacing;
    }

    public static string GetSourceClipId(IReadOnlyDictionary<string, string> data)
    {
        return data != null && data.TryGetValue(SourceClipDataKey, out string sourceClipId)
            ? sourceClipId
            : null;
    }

    private static CutieCupcakeCrewAction GetAction(ChartNote note)
    {
        return CutieCupcakeCrewNoteCodec.TryReadAction(note?.AdditionnalData, out CutieCupcakeCrewAction action)
            ? action
            : CutieCupcakeCrewAction.PinkiePieFrost;
    }

    private static string GetActionDisplayName(CutieCupcakeCrewAction action)
    {
        return action switch
        {
            CutieCupcakeCrewAction.PinkiePiePersonalTouch => "Pinkie Pie Personal Touch",
            CutieCupcakeCrewAction.SweetieBelleFrost => "Sweetie Belle Frost",
            CutieCupcakeCrewAction.SweetieBellePersonalTouch => "Sweetie Belle Personal Touch",
            CutieCupcakeCrewAction.ScootalooFrost => "Scootaloo Frost",
            CutieCupcakeCrewAction.ScootalooPersonalTouch => "Scootaloo Personal Touch",
            CutieCupcakeCrewAction.AppleBloomFrostHit => "Apple Bloom Frost Hit",
            CutieCupcakeCrewAction.AppleBloomPersonalTouchHit => "Apple Bloom Personal Touch Hit",
            _ => "Pinkie Pie Frost"
        };
    }

    private static Color GetActionColor(CutieCupcakeCrewAction action)
    {
        return action switch
        {
            CutieCupcakeCrewAction.PinkiePiePersonalTouch => Color.DeepPink,
            CutieCupcakeCrewAction.SweetieBelleFrost => Color.Plum,
            CutieCupcakeCrewAction.SweetieBellePersonalTouch => Color.MediumPurple,
            CutieCupcakeCrewAction.ScootalooFrost => Color.Orange,
            CutieCupcakeCrewAction.ScootalooPersonalTouch => Color.DarkOrange,
            CutieCupcakeCrewAction.AppleBloomFrostHit => Color.LightGreen,
            CutieCupcakeCrewAction.AppleBloomPersonalTouchHit => Color.LimeGreen,
            _ => Color.LightPink
        };
    }
}

public sealed class CutieCupcakeCrewEditorNoteTiming : IEditorNoteTiming
{
    public NoteTimingResult GetTiming(NoteTimingRequest request)
    {
        double beat = request?.Beat ?? 0.0;
        IReadOnlyDictionary<string, string> data = request?.Note?.AdditionnalData ?? request?.Variant?.AdditionnalData;
        double spacing = CutieCupcakeCrewProvider.GetBeatSpacing(data);
        string sourceClipId = CutieCupcakeCrewProvider.GetSourceClipId(data);

        if (sourceClipId == CutieCupcakeCrewProvider.TogetherFrostClipId)
            return GetTogetherFrostTiming(beat, spacing);

        if (sourceClipId == CutieCupcakeCrewProvider.PersonalTouchClipId
            || IsPersonalTouchSecondHit(data))
            return GetPersonalTouchTiming(beat, spacing, IsPersonalTouchSecondHit(data));

        return GetFrostTiming(beat, spacing);
    }

    private static NoteTimingResult GetFrostTiming(double beat, double spacing)
    {
        double hitEndBeat = beat + spacing;
        return new NoteTimingResult(
            StartBeat: beat - 4 * spacing,
            EndBeat: hitEndBeat + spacing,
            HitStartBeat: beat,
            HitEndBeat: hitEndBeat,
            SameVariantHitStartBeat: beat,
            SameVariantHitEndBeat: hitEndBeat);
    }

    private static NoteTimingResult GetTogetherFrostTiming(double beat, double spacing)
    {
        double hitEndBeat = beat + spacing;
        return new NoteTimingResult(
            StartBeat: beat - 3 * spacing,
            EndBeat: hitEndBeat + spacing,
            HitStartBeat: beat,
            HitEndBeat: hitEndBeat,
            SameVariantHitStartBeat: beat,
            SameVariantHitEndBeat: hitEndBeat);
    }

    private static NoteTimingResult GetPersonalTouchTiming(double beat, double spacing, bool isSecondHit)
    {
        double approachBeats = isSecondHit ? 8 * spacing : 7 * spacing;
        double remainingHitAndDespawnBeats = isSecondHit ? 2 * spacing : 3 * spacing;
        double hitEndBeat = beat + spacing;

        return new NoteTimingResult(
            StartBeat: beat - approachBeats,
            EndBeat: beat + remainingHitAndDespawnBeats,
            HitStartBeat: beat,
            HitEndBeat: hitEndBeat,
            SameVariantHitStartBeat: beat,
            SameVariantHitEndBeat: hitEndBeat);
    }

    private static bool IsPersonalTouchSecondHit(IReadOnlyDictionary<string, string> data)
    {
        return CutieCupcakeCrewNoteCodec.IsAction(data, CutieCupcakeCrewAction.AppleBloomPersonalTouchHit);
    }
}
