using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MLP_RiM.Elements.Editor;

/// <summary>
/// Codec generique pour les notes runtime dont le payload est une action enum.
/// </summary>
/// <typeparam name="TAction">Enum qui liste les actions runtime du rhythm game.</typeparam>
public sealed class EnumNoteCodec<TAction>
    where TAction : struct, Enum
{
    private readonly IReadOnlyDictionary<TAction, string> _primaryActionValues;
    private readonly IReadOnlyDictionary<string, TAction> _acceptedActionValues;
    private readonly TAction _defaultAction;

    /// <summary>
    /// Cree un codec pour un rhythm game et un type de note donne.
    /// </summary>
    /// <param name="gameId">Identifiant stable du rhythm game.</param>
    /// <param name="noteId">Identifiant stable du type de note runtime.</param>
    /// <param name="legacyActionValues">Aliases legacy acceptes pour chaque action. Le premier alias devient la valeur ecrite.</param>
    /// <param name="schemaVersion">Version de schema ecrite dans les donnees legacy.</param>
    public EnumNoteCodec(string gameId, string noteId, IReadOnlyDictionary<TAction, IReadOnlyList<string>> legacyActionValues = null, int schemaVersion = 1)
    {
        GameId = gameId;
        NoteId = noteId;
        SchemaVersion = schemaVersion;

        TAction[] actions = Enum.GetValues<TAction>();
        _defaultAction = actions.Length > 0 ? actions[0] : default;

        Dictionary<TAction, string> primaryActionValues = new();
        Dictionary<string, TAction> acceptedActionValues = new(StringComparer.Ordinal);
        foreach (TAction action in actions)
        {
            string generatedValue = CreateLegacyActionValue(gameId, action);
            IReadOnlyList<string> aliases = legacyActionValues != null && legacyActionValues.TryGetValue(action, out IReadOnlyList<string> values)
                ? values
                : Array.Empty<string>();
            string primaryValue = aliases.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? generatedValue;

            primaryActionValues[action] = primaryValue;
            acceptedActionValues[generatedValue] = action;
            foreach (string alias in aliases.Where(value => !string.IsNullOrWhiteSpace(value)))
                acceptedActionValues[alias] = action;
        }

        _primaryActionValues = primaryActionValues;
        _acceptedActionValues = acceptedActionValues;
    }

    /// <summary>
    /// Identifiant stable du rhythm game.
    /// </summary>
    public string GameId { get; }

    /// <summary>
    /// Identifiant stable du type de note runtime.
    /// </summary>
    public string NoteId { get; }

    /// <summary>
    /// Version du schema de payload ecrite dans les donnees legacy.
    /// </summary>
    public int SchemaVersion { get; }

    /// <summary>
    /// Actions enum connues par ce codec.
    /// </summary>
    public IReadOnlyList<TAction> Actions => _primaryActionValues.Keys.ToArray();

    /// <summary>
    /// Lit un payload depuis des donnees legacy, ou retourne l'action enum par defaut si la lecture echoue.
    /// </summary>
    /// <param name="data">Donnees legacy de la note.</param>
    /// <returns>Payload lu, ou payload de l'action par defaut.</returns>
    public EnumNotePayload<TAction> Read(IReadOnlyDictionary<string, string> data)
    {
        return TryRead(data, out EnumNotePayload<TAction> payload)
            ? payload
            : new EnumNotePayload<TAction>(this, _defaultAction);
    }

    /// <summary>
    /// Essaie de lire un payload depuis des donnees legacy.
    /// </summary>
    /// <param name="data">Donnees legacy de la note.</param>
    /// <param name="payload">Payload lu si l'action est reconnue et les metadata correspondent.</param>
    /// <returns><c>true</c> si le payload a ete lu.</returns>
    public bool TryRead(IReadOnlyDictionary<string, string> data, out EnumNotePayload<TAction> payload)
    {
        if (HasExplicitMetadata(data) && !HasMatchingMetadata(data))
        {
            payload = default;
            return false;
        }

        if (TryReadActionValue(data, out TAction action))
        {
            payload = new EnumNotePayload<TAction>(this, action, data);
            return true;
        }

        payload = default;
        return false;
    }

    /// <summary>
    /// Lit l'action enum depuis des donnees legacy, ou retourne l'action par defaut si la lecture echoue.
    /// </summary>
    /// <param name="data">Donnees legacy de la note.</param>
    /// <returns>Action lue, ou premiere valeur de l'enum.</returns>
    public TAction ReadAction(IReadOnlyDictionary<string, string> data)
    {
        return TryReadAction(data, out TAction action) ? action : _defaultAction;
    }

    /// <summary>
    /// Essaie de lire l'action enum depuis des donnees legacy.
    /// </summary>
    /// <param name="data">Donnees legacy de la note.</param>
    /// <param name="action">Action lue si elle est reconnue et les metadata correspondent.</param>
    /// <returns><c>true</c> si l'action a ete lue.</returns>
    public bool TryReadAction(IReadOnlyDictionary<string, string> data, out TAction action)
    {
        if (HasExplicitMetadata(data) && !HasMatchingMetadata(data))
        {
            action = default;
            return false;
        }

        return TryReadActionValue(data, out action);
    }

    /// <summary>
    /// Indique si des donnees legacy appartiennent a ce codec.
    /// </summary>
    /// <param name="data">Donnees legacy a tester.</param>
    /// <returns><c>true</c> si une action reconnue est presente et les metadata explicites correspondent.</returns>
    public bool Matches(IReadOnlyDictionary<string, string> data)
    {
        return TryRead(data, out _);
    }

    /// <summary>
    /// Indique si des donnees legacy representent l'action attendue.
    /// </summary>
    /// <param name="data">Donnees legacy a tester.</param>
    /// <param name="expected">Action attendue.</param>
    /// <returns><c>true</c> si l'action lue correspond.</returns>
    public bool IsAction(IReadOnlyDictionary<string, string> data, TAction expected)
    {
        return TryReadAction(data, out TAction action) && EqualityComparer<TAction>.Default.Equals(action, expected);
    }

    /// <summary>
    /// Indique si un payload type par ce codec porte l'action attendue.
    /// </summary>
    /// <param name="payload">Payload a tester.</param>
    /// <param name="expected">Action attendue.</param>
    /// <returns><c>true</c> si le payload appartient a ce codec et porte l'action attendue.</returns>
    public bool IsPayloadAction(INotePayload payload, TAction expected)
    {
        return payload is EnumNotePayload<TAction> enumPayload
            && ReferenceEquals(enumPayload.Codec, this)
            && EqualityComparer<TAction>.Default.Equals(enumPayload.Action, expected);
    }

    /// <summary>
    /// Ecrit une action enum dans un dictionnaire legacy neuf.
    /// </summary>
    /// <param name="action">Action a ecrire.</param>
    /// <returns>Dictionnaire legacy contenant metadata et action.</returns>
    public Dictionary<string, string> Write(TAction action)
    {
        return new EnumNotePayload<TAction>(this, action).ToLegacyData();
    }

    /// <summary>
    /// Ecrit un payload enum dans un dictionnaire legacy.
    /// </summary>
    /// <param name="payload">Payload a ecrire.</param>
    /// <returns>Dictionnaire legacy contenant metadata et action.</returns>
    public Dictionary<string, string> Write(EnumNotePayload<TAction> payload)
    {
        return payload?.ToLegacyData() ?? new Dictionary<string, string>();
    }

    /// <summary>
    /// Copie des donnees legacy et force l'action enum de ce codec.
    /// </summary>
    /// <param name="data">Donnees de base a conserver.</param>
    /// <param name="action">Action a ecrire.</param>
    /// <returns>Nouveau dictionnaire avec metadata et action mises a jour.</returns>
    public Dictionary<string, string> WithAction(IReadOnlyDictionary<string, string> data, TAction action)
    {
        return new EnumNotePayload<TAction>(this, action, data).ToLegacyData();
    }

    /// <summary>
    /// Convertit une action enum vers sa valeur legacy stockee dans le champ <c>action</c>.
    /// </summary>
    /// <param name="action">Action a convertir.</param>
    /// <returns>Valeur legacy stable de l'action.</returns>
    public string ToLegacyActionValue(TAction action)
    {
        return _primaryActionValues.TryGetValue(action, out string value)
            ? value
            : CreateLegacyActionValue(GameId, action);
    }

    /// <summary>
    /// Retourne l'id de clip par convention pour une action.
    /// </summary>
    /// <param name="action">Action du clip.</param>
    /// <returns>Id de clip au format <c>{gameId}.{action_id}</c>.</returns>
    public string GetClipTypeId(TAction action)
    {
        return $"{GameId}.{ToActionId(action)}";
    }

    /// <summary>
    /// Retourne l'id de variant par convention pour une action.
    /// </summary>
    /// <param name="action">Action du variant.</param>
    /// <returns>Id de variant en snake_case.</returns>
    public string GetVariantId(TAction action)
    {
        return ToActionId(action);
    }

    /// <summary>
    /// Convertit une valeur enum en identifiant snake_case stable.
    /// </summary>
    /// <param name="action">Action a convertir.</param>
    /// <returns>Identifiant snake_case de l'action.</returns>
    public static string ToActionId(TAction action)
    {
        return ToSnakeCase(action.ToString());
    }

    /// <summary>
    /// Convertit une valeur enum en nom lisible par l'utilisateur.
    /// </summary>
    /// <param name="action">Action a convertir.</param>
    /// <returns>Nom lisible derive de l'id snake_case.</returns>
    public static string ToDisplayName(TAction action)
    {
        return string.Join(" ", ToActionId(action)
            .Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(word => word.Length == 0 ? word : char.ToUpperInvariant(word[0]) + word.Substring(1)));
    }

    private static string CreateLegacyActionValue(string gameId, TAction action)
    {
        return $"{gameId}_{ToActionId(action)}";
    }

    private bool TryReadActionValue(IReadOnlyDictionary<string, string> data, out TAction action)
    {
        if (data != null
            && data.TryGetValue(NotePayloadKeys.Action, out string value)
            && _acceptedActionValues.TryGetValue(value, out action))
            return true;

        action = default;
        return false;
    }

    private bool HasExplicitMetadata(IReadOnlyDictionary<string, string> data)
    {
        return data != null
            && (data.ContainsKey(NotePayloadKeys.Game)
                || data.ContainsKey(NotePayloadKeys.Type)
                || data.ContainsKey(NotePayloadKeys.Version));
    }

    private bool HasMatchingMetadata(IReadOnlyDictionary<string, string> data)
    {
        return data != null
            && data.TryGetValue(NotePayloadKeys.Game, out string gameId)
            && gameId == GameId
            && data.TryGetValue(NotePayloadKeys.Type, out string noteId)
            && noteId == NoteId;
    }

    private static string ToSnakeCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        StringBuilder builder = new();
        for (int i = 0; i < value.Length; i++)
        {
            char current = value[i];
            if (char.IsUpper(current))
            {
                bool hasPrevious = i > 0;
                bool previousIsSeparator = hasPrevious && builder.Length > 0 && builder[builder.Length - 1] == '_';
                bool previousIsLowerOrDigit = hasPrevious && (char.IsLower(value[i - 1]) || char.IsDigit(value[i - 1]));
                bool nextIsLower = i + 1 < value.Length && char.IsLower(value[i + 1]);
                if (hasPrevious && !previousIsSeparator && (previousIsLowerOrDigit || nextIsLower))
                    builder.Append('_');

                builder.Append(char.ToLowerInvariant(current));
                continue;
            }

            if (current == '-' || char.IsWhiteSpace(current))
            {
                if (builder.Length > 0 && builder[builder.Length - 1] != '_')
                    builder.Append('_');
                continue;
            }

            builder.Append(char.ToLowerInvariant(current));
        }

        return builder.ToString().Trim('_');
    }
}
