using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace MLP_RiM.Elements.Editor;

public enum DerpyStampedeAction
{
    Entry,
    Exit,
    Stamp,
    Triple_Stamp
}

public sealed record DerpyStampedeNotePayload(DerpyStampedeAction Action) : INotePayload
{
    public string GameId => DerpyStampedeNoteCodec.GameId;
    public string NoteId => DerpyStampedeNoteCodec.NoteId;
    public int SchemaVersion => DerpyStampedeNoteCodec.SchemaVersion;

    public Dictionary<string, string> ToLegacyData()
    {
        return DerpyStampedeNoteCodec.Write(this);
    }
}

public static class DerpyStampedeNoteCodec
{
    public const string GameId = "derpy_stampede";
    public const string NoteId = "derpy_stampede_note";
    public const int SchemaVersion = 1;

    private static readonly EnumNoteCodec<DerpyStampedeAction> Codec = new(GameId, NoteId, schemaVersion: SchemaVersion);

    public static bool TryReadAction(IReadOnlyDictionary<string, string> data, out DerpyStampedeAction action)
    {
        return Codec.TryReadAction(data, out action);
    }

    public static bool IsAction(IReadOnlyDictionary<string, string> data, DerpyStampedeAction expected)
    {
        return Codec.IsAction(data, expected);
    }

    public static Dictionary<string, string> Write(DerpyStampedeAction action)
    {
        return Codec.Write(action);
    }

    public static Dictionary<string, string> Write(DerpyStampedeNotePayload payload)
    {
        return Codec.Write(payload?.Action ?? DerpyStampedeAction.Stamp);
    }
}

public sealed class DerpyStampedeProvider : SimpleRhythmGame<DerpyStampedeAction>
{
    public const string GameId = DerpyStampedeNoteCodec.GameId;
    public const string EntryCinematicClipId = "derpy_stampede.entry";
    public const string ExitCinematicClipId = "derpy_stampede.exit";
    public const string StampClipId = "derpy_stampede.stamp";
    public const string TripleStampClipId = "derpy_stampede.triple_stamp";
    public static readonly NoteTypeId TypeId = new(GameId, DerpyStampedeNoteCodec.NoteId);

    protected override void Build(RhythmGameBuilder<DerpyStampedeAction> game)
    {
        game.Id(GameId)
            .DisplayName("Derpy Stampede")
            .SortOrder(20)
            .Scene(() => new DerpyStampede());

        game.RuntimeNote(DerpyStampedeNoteCodec.NoteId)
            .Input("ReactMain")
            .Hold(0);

        game.NoHit(4)
            .Id(EntryCinematicClipId)
            .Name("Entry")
            .Color(Color.GreenYellow);

        game.Clip(DerpyStampedeAction.Stamp)
            .Id(StampClipId)
            .Name("Stamp")
            .Color(Color.SandyBrown)
            .SingleHit()
            .Occupies(1, 1)
            .HitWindow(0, 0)
            .SameVariantHitWindow(0, 0);

        game.Clip(DerpyStampedeAction.Triple_Stamp)
            .Id(TripleStampClipId)
            .Name("Triple Stamp")
            .Color(Color.Goldenrod)
            .Occupies(2, 1)
            .HitWindow(0, 0)
            .SameVariantHitWindow(0, 0)
            .Emit(0)
            .Emit(0.5)
            .Emit(1.0);
    }
}
