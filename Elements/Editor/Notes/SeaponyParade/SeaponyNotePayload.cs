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

    private const string ActionKey = NotePayloadKeys.Action;
    private const string SwimActionValue = "seapony_parade_swim";
    private const string RollActionValue = "seapony_parade_roll";
    private const string TapTapActionValue = "seapony_parade_tap_tap";

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
        if (HasExplicitMetadata(data) && !HasMatchingMetadata(data))
        {
            payload = default;
            return false;
        }

        if (TryReadAction(data, out SeaponyAction action))
        {
            payload = new SeaponyNotePayload(action);
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
        if (data != null
            && data.TryGetValue(ActionKey, out string value)
            && TryParseAction(value, out action))
            return true;

        action = default;
        return false;
    }

    public static bool Matches(IReadOnlyDictionary<string, string> data)
    {
        return TryRead(data, out _);
    }

    public static bool IsAction(IReadOnlyDictionary<string, string> data, SeaponyAction expected)
    {
        return TryRead(data, out SeaponyNotePayload payload) && payload.Action == expected;
    }

    public static Dictionary<string, string> Write(SeaponyAction action)
    {
        return Write(new SeaponyNotePayload(action));
    }

    public static Dictionary<string, string> Write(SeaponyNotePayload payload)
    {
        return new Dictionary<string, string>
        {
            [NotePayloadKeys.Game] = GameId,
            [NotePayloadKeys.Type] = NoteId,
            [NotePayloadKeys.Version] = SchemaVersion.ToString(),
            [ActionKey] = ToLegacyActionValue(payload.Action)
        };
    }

    public static Dictionary<string, string> WithAction(IReadOnlyDictionary<string, string> data, SeaponyAction action)
    {
        Dictionary<string, string> result = new(data ?? new Dictionary<string, string>());
        result[NotePayloadKeys.Game] = GameId;
        result[NotePayloadKeys.Type] = NoteId;
        result[NotePayloadKeys.Version] = SchemaVersion.ToString();
        result[ActionKey] = ToLegacyActionValue(action);
        return result;
    }

    private static bool HasExplicitMetadata(IReadOnlyDictionary<string, string> data)
    {
        return data != null
            && (data.ContainsKey(NotePayloadKeys.Game)
                || data.ContainsKey(NotePayloadKeys.Type)
                || data.ContainsKey(NotePayloadKeys.Version));
    }

    private static bool HasMatchingMetadata(IReadOnlyDictionary<string, string> data)
    {
        return data != null
            && data.TryGetValue(NotePayloadKeys.Game, out string gameId)
            && gameId == GameId
            && data.TryGetValue(NotePayloadKeys.Type, out string noteId)
            && noteId == NoteId;
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

    private static bool TryParseAction(string value, out SeaponyAction action)
    {
        switch (value)
        {
            case SwimActionValue:
                action = SeaponyAction.Swim;
                return true;
            case RollActionValue:
                action = SeaponyAction.Roll;
                return true;
            case TapTapActionValue:
                action = SeaponyAction.TapTap;
                return true;
            default:
                action = default;
                return false;
        }
    }

    private static string ToLegacyActionValue(SeaponyAction action)
    {
        return action switch
        {
            SeaponyAction.Roll => RollActionValue,
            SeaponyAction.TapTap => TapTapActionValue,
            _ => SwimActionValue
        };
    }
}
