using System;
using System.Collections.Generic;
using System.Linq;

namespace MLP_RiM.Elements.Editor;

public enum SeaponyAction
{
    Swim,
    Roll,
    TapTap
}

public sealed record SeaponyNotePayload(SeaponyAction Action) : INotePayload
{
    public string GameId => SeaponyNoteCodec.GameId;
    public string NoteId => SeaponyNoteCodec.NoteId;
    public int SchemaVersion => SeaponyNoteCodec.SchemaVersion;

    public Dictionary<string, string> ToLegacyData()
    {
        return SeaponyNoteCodec.Write(this);
    }
}

public static class SeaponyNoteCodec
{
    public const string GameId = "seapony_parade";
    public const string NoteId = "note";
    public const int SchemaVersion = 1;

    private static readonly EnumNoteCodec<SeaponyAction> Codec = new(GameId, NoteId, schemaVersion: SchemaVersion);

    public static readonly IReadOnlyList<SeaponyAction> EditorActions = new[]
    {
        SeaponyAction.Swim,
        SeaponyAction.Roll,
        SeaponyAction.TapTap
    };

    public static SeaponyNotePayload Read(IReadOnlyDictionary<string, string> data)
    {
        return TryRead(data, out SeaponyNotePayload payload)
            ? payload
            : new SeaponyNotePayload(SeaponyAction.Swim);
    }

    public static bool TryRead(IReadOnlyDictionary<string, string> data, out SeaponyNotePayload payload)
    {
        if (Codec.TryRead(data, out EnumNotePayload<SeaponyAction> enumPayload))
        {
            payload = new SeaponyNotePayload(enumPayload.Action);
            return true;
        }

        payload = default;
        return false;
    }

    public static SeaponyAction ReadAction(IReadOnlyDictionary<string, string> data)
    {
        return Read(data).Action;
    }

    public static bool TryReadAction(IReadOnlyDictionary<string, string> data, out SeaponyAction action)
    {
        return Codec.TryReadAction(data, out action);
    }

    public static bool Matches(IReadOnlyDictionary<string, string> data)
    {
        return TryRead(data, out _);
    }

    public static bool IsAction(IReadOnlyDictionary<string, string> data, SeaponyAction expected)
    {
        return Codec.IsAction(data, expected);
    }

    public static Dictionary<string, string> Write(SeaponyAction action)
    {
        return Codec.Write(action);
    }

    public static Dictionary<string, string> Write(SeaponyNotePayload payload)
    {
        return Codec.Write(payload?.Action ?? SeaponyAction.Swim);
    }

    public static Dictionary<string, string> WithAction(IReadOnlyDictionary<string, string> data, SeaponyAction action)
    {
        return Codec.WithAction(data, action);
    }

    public static string GetDisplayName(SeaponyAction action)
    {
        return action switch
        {
            SeaponyAction.Roll => "Roll",
            SeaponyAction.TapTap => "Tap Tap",
            _ => "Swim"
        };
    }

    public static int GetEditorActionIndex(SeaponyAction action)
    {
        int index = EditorActions.ToList().IndexOf(action);
        return index >= 0 ? index : 0;
    }

    public static SeaponyAction GetEditorActionAt(int index)
    {
        return EditorActions[Math.Clamp(index, 0, EditorActions.Count - 1)];
    }
}
