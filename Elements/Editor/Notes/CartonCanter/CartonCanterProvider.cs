using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace MLP_RiM.Elements.Editor;

public enum CartonCanterAction
{
    TwilightSparkle,
    Fluttershy,
    Applejack,
    PinkiePie
}

public sealed record CartonCanterNotePayload(CartonCanterAction Action) : INotePayload
{
    public string GameId => CartonCanterNoteCodec.GameId;
    public string NoteId => CartonCanterNoteCodec.NoteId;
    public int SchemaVersion => CartonCanterNoteCodec.SchemaVersion;

    public Dictionary<string, string> ToLegacyData()
    {
        return CartonCanterNoteCodec.Write(this);
    }
}

public static class CartonCanterNoteCodec
{
    public const string GameId = "carton_canter";
    public const string NoteId = "note";
    public const int SchemaVersion = 1;

    private static readonly EnumNoteCodec<CartonCanterAction> Codec = new(GameId, NoteId, schemaVersion: SchemaVersion);

    public static bool TryReadAction(IReadOnlyDictionary<string, string> data, out CartonCanterAction action)
    {
        return Codec.TryReadAction(data, out action);
    }

    public static bool IsAction(IReadOnlyDictionary<string, string> data, CartonCanterAction expected)
    {
        return Codec.IsAction(data, expected);
    }

    public static bool Matches(IReadOnlyDictionary<string, string> data)
    {
        return Codec.Matches(data);
    }

    public static Dictionary<string, string> Write(CartonCanterAction action)
    {
        return Codec.Write(action);
    }

    public static Dictionary<string, string> Write(CartonCanterNotePayload payload)
    {
        return Codec.Write(payload?.Action ?? CartonCanterAction.TwilightSparkle);
    }

    public static string GetVariantId(CartonCanterAction action)
    {
        return Codec.GetVariantId(action);
    }
}

public sealed class CartonCanterProvider : SimpleRhythmGame<CartonCanterAction>
{
    public const string GameId = CartonCanterNoteCodec.GameId;
    public const string TwilightSparkleClipId = "carton_canter.twilight_sparkle";
    public const string FluttershyClipId = "carton_canter.fluttershy";
    public const string ApplejackClipId = "carton_canter.applejack";
    public const string PinkiePieClipId = "carton_canter.pinkie_pie";
    public const double ExitBeats = 2.0;
    public static readonly NoteTypeId TypeId = new(GameId, CartonCanterNoteCodec.NoteId);

    private const string PlayerInputAction = "ReactMain";
    private static readonly double[] TwilightCueOffsets = { 1.0, 2.0, 3.0 };
    private static readonly double[] FluttershyCueOffsets = { 1.0, 3.0, 5.0 };
    private static readonly double[] ApplejackCueOffsets = { 1.0, 1.5, 2.0 };
    private static readonly double[] PinkiePieCueOffsets = { 1.0, 1.5, 2.0 };

    protected override void Build(RhythmGameBuilder<CartonCanterAction> game)
    {
        game.Id(GameId)
            .DisplayName("Carton Canter")
            .SortOrder(50)
            .Scene(() => new global::CartonCanter());

        game.RuntimeNote(CartonCanterNoteCodec.NoteId)
            .Input(PlayerInputAction)
            .Hold(0)
            .HitWindow(0, 0)
            .SameVariantHitWindow(0, 0);

        DeclarePonyClip(game, CartonCanterAction.TwilightSparkle, TwilightSparkleClipId, "Twilight Sparkle", Color.MediumPurple);
        DeclarePonyClip(game, CartonCanterAction.Fluttershy, FluttershyClipId, "Fluttershy", Color.LightGoldenrodYellow);
        DeclarePonyClip(game, CartonCanterAction.Applejack, ApplejackClipId, "Applejack", Color.SandyBrown);
        DeclarePonyClip(game, CartonCanterAction.PinkiePie, PinkiePieClipId, "Pinkie Pie", Color.HotPink);
    }

    public static IReadOnlyList<double> GetCueOffsetsBeats(CartonCanterAction action)
    {
        return action switch
        {
            CartonCanterAction.Fluttershy => FluttershyCueOffsets,
            CartonCanterAction.Applejack => ApplejackCueOffsets,
            CartonCanterAction.PinkiePie => PinkiePieCueOffsets,
            _ => TwilightCueOffsets
        };
    }

    public static double GetReactOffsetBeats(CartonCanterAction action)
    {
        return action switch
        {
            CartonCanterAction.Fluttershy => 7.0,
            CartonCanterAction.Applejack => 4.0,
            CartonCanterAction.PinkiePie => 2.5,
            _ => 4.0
        };
    }

    private static void DeclarePonyClip(RhythmGameBuilder<CartonCanterAction> game, CartonCanterAction action, string clipId, string displayName, Color color)
    {
        double reactOffset = GetReactOffsetBeats(action);
        game.Clip(action)
            .Id(clipId)
            .Name(displayName)
            .Color(color)
            .Continuous(reactOffset + ExitBeats)
            .LeadIn(reactOffset)
            .Occupies(reactOffset, ExitBeats)
            .HitWindow(0, 0)
            .SameVariantHitWindow(0, 0);
    }
}
