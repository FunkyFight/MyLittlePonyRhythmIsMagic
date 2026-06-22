using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.Xna.Framework;
using Rhythm.Note;

namespace MLP_RiM.Elements.Editor;

public enum GemwalkGlamourAction
{
    OneSapphire,
    ThreeSapphires,
    Ruby,
    RarityEntry,
    RarityExit
}

public sealed record GemwalkGlamourNotePayload(GemwalkGlamourAction Action) : INotePayload
{
    public string GameId => GemwalkGlamourNoteCodec.GameId;
    public string NoteId => GemwalkGlamourNoteCodec.NoteId;
    public int SchemaVersion => GemwalkGlamourNoteCodec.SchemaVersion;

    public Dictionary<string, string> ToLegacyData()
    {
        return GemwalkGlamourNoteCodec.Write(this);
    }
}

public static class GemwalkGlamourNoteCodec
{
    public const string GameId = "gemwalk_glamour";
    public const string NoteId = "note";
    public const int SchemaVersion = 1;

    private static readonly EnumNoteCodec<GemwalkGlamourAction> Codec = new(GameId, NoteId, schemaVersion: SchemaVersion);

    public static bool TryReadAction(IReadOnlyDictionary<string, string> data, out GemwalkGlamourAction action)
    {
        return Codec.TryReadAction(data, out action);
    }

    public static bool IsAction(IReadOnlyDictionary<string, string> data, GemwalkGlamourAction expected)
    {
        return Codec.IsAction(data, expected);
    }

    public static bool Matches(IReadOnlyDictionary<string, string> data)
    {
        return Codec.Matches(data);
    }

    public static Dictionary<string, string> Write(GemwalkGlamourAction action)
    {
        return Codec.Write(action);
    }

    public static Dictionary<string, string> Write(GemwalkGlamourNotePayload payload)
    {
        return Codec.Write(payload?.Action ?? GemwalkGlamourAction.OneSapphire);
    }
}

public sealed class GemwalkGlamourProvider : SimpleRhythmGame<GemwalkGlamourAction>
{
    public const string GameId = GemwalkGlamourNoteCodec.GameId;
    public const string OneSapphireClipId = "gemwalk_glamour.one_sapphire";
    public const string ThreeSapphiresClipId = "gemwalk_glamour.three_sapphires";
    public const string RubyClipId = "gemwalk_glamour.ruby";
    public const string RarityEntryClipId = "gemwalk_glamour.rarity_entry";
    public const string RarityExitClipId = "gemwalk_glamour.rarity_exit";
    public const string BeatSpacingDataKey = "beat_spacing";
    public const string CueCountDataKey = "cue_count";
    public const string SourceClipDataKey = "source_clip";
    public const double DefaultBeatSpacing = 1.0;
    public const int DefaultRubyCueCount = 4;
    public static readonly NoteTypeId TypeId = new(GameId, GemwalkGlamourNoteCodec.NoteId);

    private const string PlayerInputAction = "ReactMain";
    private const double CueLeadBeats = 2.0;
    private const double DespawnLeadBeats = 2.0;
    private const double DefaultOneSapphireLengthBeats = CueLeadBeats + DespawnLeadBeats;
    private const double DefaultThreeSapphiresLengthBeats = DefaultBeatSpacing * 3.0 + CueLeadBeats + DespawnLeadBeats;
    private const double DefaultRubyLengthBeats = CueLeadBeats + DefaultRubyCueCount * DefaultBeatSpacing + DespawnLeadBeats;
    private const double DefaultRarityMotionLengthBeats = 4.0;
    private const double LeadInEpsilonBeats = 0.000001;

    protected override void Build(RhythmGameBuilder<GemwalkGlamourAction> game)
    {
        game.Id(GameId)
            .DisplayName("Gemwalk Glamour")
            .SortOrder(40)
            .Scene(() => new GemwalkGlamour());

        game.RuntimeNote(GemwalkGlamourNoteCodec.NoteId)
            .Input(PlayerInputAction)
            .Hold(0)
            .Occupies(CueLeadBeats, DespawnLeadBeats)
            .HitWindow(0, 0)
            .SameVariantHitWindow(0, 0);

        game.Clip(GemwalkGlamourAction.OneSapphire)
            .Id(OneSapphireClipId)
            .Name("One Sapphire")
            .Color(Color.DeepSkyBlue)
            .Continuous(DefaultOneSapphireLengthBeats)
            .LeadIn(CueLeadBeats)
            .Data(SourceClipDataKey, OneSapphireClipId);

        game.Clip(GemwalkGlamourAction.ThreeSapphires)
            .Id(ThreeSapphiresClipId)
            .Name("Three Sapphires")
            .Color(Color.CornflowerBlue)
            .Continuous(DefaultThreeSapphiresLengthBeats)
            .LeadIn(CueLeadBeats)
            .Data(SourceClipDataKey, ThreeSapphiresClipId)
            .Data(BeatSpacingDataKey, Format(DefaultBeatSpacing))
            .Field(EditorClipFieldDefinition.Float(BeatSpacingDataKey, "x beats", DefaultBeatSpacing, minValue: 0.001, maxValue: 16))
            .Compile(CompileThreeSapphires);

        game.Clip(GemwalkGlamourAction.Ruby)
            .Id(RubyClipId)
            .Name("Ruby")
            .Color(Color.IndianRed)
            .Continuous(DefaultRubyLengthBeats)
            .LeadIn(CueLeadBeats)
            .Data(SourceClipDataKey, RubyClipId)
            .Data(BeatSpacingDataKey, Format(DefaultBeatSpacing))
            .Data(CueCountDataKey, DefaultRubyCueCount.ToString(CultureInfo.InvariantCulture))
            .Fields(
                EditorClipFieldDefinition.Float(BeatSpacingDataKey, "x beats", DefaultBeatSpacing, minValue: 0.001, maxValue: 16),
                EditorClipFieldDefinition.Float(CueCountDataKey, "i cues", DefaultRubyCueCount, minValue: 1, maxValue: 32))
            .Compile(CompileRuby);

        game.Clip(GemwalkGlamourAction.RarityEntry)
            .Id(RarityEntryClipId)
            .Name("Entry")
            .Color(Color.LightGoldenrodYellow)
            .Continuous(DefaultRarityMotionLengthBeats)
            .Data(SourceClipDataKey, RarityEntryClipId)
            .Compile(CompileNoNotes);

        game.Clip(GemwalkGlamourAction.RarityExit)
            .Id(RarityExitClipId)
            .Name("Exit")
            .Color(Color.Plum)
            .Continuous(DefaultRarityMotionLengthBeats)
            .Data(SourceClipDataKey, RarityExitClipId)
            .Compile(CompileNoNotes);
    }

    public override string GetClipTypeIdFromLegacyNote(ChartNote note)
    {
        string sourceClipId = GetSourceClipId(note?.AdditionnalData);
        if (sourceClipId == OneSapphireClipId
            || sourceClipId == ThreeSapphiresClipId
            || sourceClipId == RubyClipId
            || sourceClipId == RarityEntryClipId
            || sourceClipId == RarityExitClipId)
            return sourceClipId;

        return base.GetClipTypeIdFromLegacyNote(note);
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

    public static int GetRubyCueCount(IReadOnlyDictionary<string, string> data)
    {
        if (data != null
            && data.TryGetValue(CueCountDataKey, out string rawValue)
            && double.TryParse(rawValue, NumberStyles.Float, CultureInfo.InvariantCulture, out double count)
            && count > 0.0)
            return Math.Max(1, (int)Math.Round(count));

        return DefaultRubyCueCount;
    }

    public static double GetRubyHoldBeats(IReadOnlyDictionary<string, string> data)
    {
        return Math.Max(0.001, GetRubyCueCount(data) * GetBeatSpacing(data));
    }

    public static string GetSourceClipId(IReadOnlyDictionary<string, string> data)
    {
        return data != null && data.TryGetValue(SourceClipDataKey, out string sourceClipId)
            ? sourceClipId
            : null;
    }

    private static void CompileThreeSapphires(SimpleClipCompileContext<GemwalkGlamourAction> context, SimpleRuntimeNoteEmitter<GemwalkGlamourAction> emit)
    {
        double spacing = GetBeatSpacing(context.Data);
        double firstHitOffset = IsClipLeadInApplied(context) ? spacing : 0.0;

        emit.Emit(GemwalkGlamourAction.OneSapphire, firstHitOffset);
        emit.Emit(GemwalkGlamourAction.OneSapphire, firstHitOffset + spacing);
        emit.Emit(GemwalkGlamourAction.OneSapphire, firstHitOffset + spacing * 2.0);
    }

    private static void CompileRuby(SimpleClipCompileContext<GemwalkGlamourAction> context, SimpleRuntimeNoteEmitter<GemwalkGlamourAction> emit)
    {
        emit.Emit(GemwalkGlamourAction.Ruby, 0.0, GetRubyHoldBeats(context.Data));
    }

    private static void CompileNoNotes(SimpleClipCompileContext<GemwalkGlamourAction> context, SimpleRuntimeNoteEmitter<GemwalkGlamourAction> emit)
    {
    }

    private static bool IsClipLeadInApplied(SimpleClipCompileContext<GemwalkGlamourAction> context)
    {
        return context != null && context.StartBeat > context.Clip.StartBeat + LeadInEpsilonBeats;
    }

    private static string Format(double value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
