using System.Collections.Generic;

namespace MLP_RiM.Elements.Editor;

/// <summary>
/// Payload standard pour une note runtime dont le gameplay est porte par une action enum.
/// </summary>
/// <typeparam name="TAction">Enum qui liste les actions runtime du rhythm game.</typeparam>
/// <param name="Codec">Codec utilise pour convertir l'action vers les donnees legacy.</param>
/// <param name="Action">Action runtime stockee dans la note.</param>
/// <param name="LegacyData">Donnees supplementaires a conserver lors de l'ecriture legacy.</param>
public sealed record EnumNotePayload<TAction>(EnumNoteCodec<TAction> Codec, TAction Action, IReadOnlyDictionary<string, string> LegacyData = null) : INotePayload
    where TAction : struct, System.Enum
{
    /// <summary>
    /// Identifiant stable du rhythm game.
    /// </summary>
    public string GameId => Codec.GameId;

    /// <summary>
    /// Identifiant stable du type de note runtime dans le rhythm game.
    /// </summary>
    public string NoteId => Codec.NoteId;

    /// <summary>
    /// Version du schema de payload ecrite dans les donnees legacy.
    /// </summary>
    public int SchemaVersion => Codec.SchemaVersion;

    /// <summary>
    /// Convertit ce payload vers le dictionnaire sauvegarde dans <c>ChartNote.AdditionnalData</c>.
    /// </summary>
    /// <returns>Un nouveau dictionnaire contenant les metadata et l'action legacy.</returns>
    public Dictionary<string, string> ToLegacyData()
    {
        Dictionary<string, string> data = new(LegacyData ?? new Dictionary<string, string>());
        data[NotePayloadKeys.Game] = GameId;
        data[NotePayloadKeys.Type] = NoteId;
        data[NotePayloadKeys.Version] = SchemaVersion.ToString();
        data[NotePayloadKeys.Action] = Codec.ToLegacyActionValue(Action);
        return data;
    }
}
