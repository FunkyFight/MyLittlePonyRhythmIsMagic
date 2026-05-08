using System;
using System.Collections.Generic;
using GameCore.Scenes;

namespace MLP_RiM.Elements.Editor;

/// <summary>
/// Builder declaratif utilise par <see cref="SimpleRhythmGame{TAction}"/> pour decrire un rhythm game enum-based.
/// </summary>
/// <typeparam name="TAction">Enum qui liste les actions runtime du rhythm game.</typeparam>
public sealed class RhythmGameBuilder<TAction>
    where TAction : struct, Enum
{
    private readonly SimpleRuntimeNoteConfiguration _runtimeNote = new();
    private readonly List<SimpleClipConfiguration<TAction>> _clips = new();
    private readonly Dictionary<TAction, List<string>> _legacyActionValues = new();
    private SimpleClipConfiguration<TAction> _noHitClip;

    internal RhythmGameBuilder()
    {
    }

    /// <summary>
    /// Identifiant stable configure par <see cref="Id"/>.
    /// </summary>
    public string GameId { get; private set; }

    /// <summary>
    /// Nom affiche configure par <see cref="DisplayName"/>.
    /// </summary>
    public string DisplayNameValue { get; private set; }

    /// <summary>
    /// Ordre de tri configure par <see cref="SortOrder"/>.
    /// </summary>
    public int SortOrderValue { get; private set; }

    /// <summary>
    /// Fabrique de scene runtime configuree par <see cref="Scene"/>.
    /// </summary>
    public Func<Scene> SceneFactory { get; private set; }
    internal SimpleRuntimeNoteConfiguration RuntimeNoteConfiguration => _runtimeNote;
    internal IReadOnlyList<SimpleClipConfiguration<TAction>> ClipConfigurations => _clips;
    internal SimpleClipConfiguration<TAction> NoHitClipConfiguration => _noHitClip;
    internal IReadOnlyDictionary<TAction, IReadOnlyList<string>> LegacyActionValues => CreateLegacyActionValuesSnapshot();

    /// <summary>
    /// Definit l'identifiant stable du rhythm game.
    /// </summary>
    /// <param name="gameId">Identifiant stable, generalement en snake_case.</param>
    /// <returns>Ce builder pour continuer la declaration.</returns>
    public RhythmGameBuilder<TAction> Id(string gameId)
    {
        GameId = gameId;
        return this;
    }

    /// <summary>
    /// Definit le nom affiche dans l'editeur.
    /// </summary>
    /// <param name="displayName">Nom lisible du rhythm game.</param>
    /// <returns>Ce builder pour continuer la declaration.</returns>
    public RhythmGameBuilder<TAction> DisplayName(string displayName)
    {
        DisplayNameValue = displayName;
        return this;
    }

    /// <summary>
    /// Definit l'ordre de tri du rhythm game dans les listes de l'editeur.
    /// </summary>
    /// <param name="sortOrder">Valeur de tri croissante.</param>
    /// <returns>Ce builder pour continuer la declaration.</returns>
    public RhythmGameBuilder<TAction> SortOrder(int sortOrder)
    {
        SortOrderValue = sortOrder;
        return this;
    }

    /// <summary>
    /// Definit la fabrique de scene runtime lancee pour ce rhythm game.
    /// </summary>
    /// <param name="sceneFactory">Fonction qui cree une nouvelle scene runtime.</param>
    /// <returns>Ce builder pour continuer la declaration.</returns>
    public RhythmGameBuilder<TAction> Scene(Func<Scene> sceneFactory)
    {
        SceneFactory = sceneFactory;
        return this;
    }

    /// <summary>
    /// Commence la declaration de la note runtime produite par les clips du jeu.
    /// </summary>
    /// <param name="noteId">Identifiant stable du type de note runtime.</param>
    /// <returns>Builder de la note runtime.</returns>
    public RuntimeNoteBuilder<TAction> RuntimeNote(string noteId = "note")
    {
        _runtimeNote.NoteId = string.IsNullOrWhiteSpace(noteId) ? "note" : noteId;
        return new RuntimeNoteBuilder<TAction>(_runtimeNote);
    }

    /// <summary>
    /// Commence la declaration d'un clip auteur qui produit des notes runtime pour une action.
    /// </summary>
    /// <param name="action">Action runtime produite par le clip.</param>
    /// <returns>Builder du clip auteur.</returns>
    public SimpleClipBuilder<TAction> Clip(TAction action)
    {
        SimpleClipConfiguration<TAction> configuration = new(action, isRuntime: true);
        _clips.Add(configuration);
        return new SimpleClipBuilder<TAction>(configuration);
    }

    /// <summary>
    /// Declare le clip auteur silencieux qui ne produit aucune note runtime.
    /// </summary>
    /// <param name="defaultLengthBeats">Longueur par defaut du clip en beats.</param>
    /// <returns>Builder du clip silencieux.</returns>
    public SimpleClipBuilder<TAction> NoHit(double defaultLengthBeats = 1.0)
    {
        _noHitClip = new SimpleClipConfiguration<TAction>(default, isRuntime: false)
        {
            ClipTypeId = EditorClipDefinitions.NoHit,
            DisplayName = "No Hit",
            Category = EditorClipCategory.NoHit,
            DefaultLengthBeats = Math.Max(0.0, defaultLengthBeats)
        };
        return new SimpleClipBuilder<TAction>(_noHitClip);
    }

    /// <summary>
    /// Ajoute une valeur d'action legacy acceptee pour une action enum.
    /// </summary>
    /// <param name="action">Action enum cible.</param>
    /// <param name="value">Ancienne valeur du champ <c>action</c> a accepter.</param>
    /// <returns>Ce builder pour continuer la declaration.</returns>
    public RhythmGameBuilder<TAction> LegacyActionValue(TAction action, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return this;

        if (!_legacyActionValues.TryGetValue(action, out List<string> values))
        {
            values = new List<string>();
            _legacyActionValues[action] = values;
        }

        values.Add(value);
        return this;
    }

    internal RhythmGameDefinition<TAction> BuildDefinition()
    {
        return new RhythmGameDefinition<TAction>(this);
    }

    private IReadOnlyDictionary<TAction, IReadOnlyList<string>> CreateLegacyActionValuesSnapshot()
    {
        Dictionary<TAction, IReadOnlyList<string>> values = new();
        foreach (KeyValuePair<TAction, List<string>> pair in _legacyActionValues)
            values[pair.Key] = pair.Value.ToArray();
        return values;
    }
}
