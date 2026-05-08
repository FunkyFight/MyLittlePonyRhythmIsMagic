using System.Collections.Generic;

namespace MLP_RiM.Elements.Editor;

public interface INotePayload
{
    string GameId { get; }
    string NoteId { get; }
    int SchemaVersion { get; }
    Dictionary<string, string> ToLegacyData();
}

public static class NotePayloadKeys
{
    public const string Game = "_game";
    public const string Type = "_type";
    public const string Version = "_version";
    public const string Action = "action";

    public static bool IsMetadataKey(string key)
    {
        return key == Game || key == Type || key == Version;
    }

    public static bool PayloadDataEquals(IReadOnlyDictionary<string, string> a, IReadOnlyDictionary<string, string> b)
    {
        foreach (KeyValuePair<string, string> pair in a ?? EmptyData)
        {
            if (IsMetadataKey(pair.Key))
                continue;

            if (b == null || !b.TryGetValue(pair.Key, out string value) || value != pair.Value)
                return false;
        }

        foreach (KeyValuePair<string, string> pair in b ?? EmptyData)
        {
            if (IsMetadataKey(pair.Key))
                continue;

            if (a == null || !a.TryGetValue(pair.Key, out string value) || value != pair.Value)
                return false;
        }

        return true;
    }

    private static readonly IReadOnlyDictionary<string, string> EmptyData = new Dictionary<string, string>();
}

public sealed record LegacyNotePayload(string GameId, string NoteId, int SchemaVersion, IReadOnlyDictionary<string, string> Data) : INotePayload
{
    public Dictionary<string, string> ToLegacyData()
    {
        return new Dictionary<string, string>(Data ?? new Dictionary<string, string>());
    }
}
