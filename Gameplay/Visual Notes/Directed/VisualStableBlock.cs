using System;
using System.Collections.Generic;

/// <summary>
/// Bloc de timeline qui applique un état déterministe hors des phases animées.
/// </summary>
/// <remarks>
/// Les blocs stables servent à réappliquer un état après seek ou avant l'entrée d'une note sans dépendre
/// d'un évènement one-shot. Ils utilisent le même système d'ownership que <see cref="VisualPhase"/>.
/// </remarks>
public sealed class VisualStableBlock : IVisualTimelineBlock
{
    private readonly string _id;
    private readonly VisualStableKind _kind;
    private readonly List<string> _ownedTrackIds = new();
    private readonly List<Action<VisualContext>> _actions = new();

    /// <summary>
    /// Crée un bloc stable interne pour une timeline.
    /// </summary>
    /// <param name="id">Identifiant du bloc.</param>
    /// <param name="kind">Position du bloc par rapport à la fenêtre visuelle.</param>
    internal VisualStableBlock(string id, VisualStableKind kind)
    {
        _id = id;
        _kind = kind;
    }

    /// <summary>
    /// Déclare les tracks que ce bloc stable est autorisé à muter.
    /// </summary>
    /// <param name="trackIds">Identifiants de tracks possédées pendant l'exécution des actions.</param>
    /// <returns>Ce bloc pour continuer la déclaration fluent.</returns>
    public VisualStableBlock Owns(params string[] trackIds)
    {
        if(trackIds == null)
            return this;

        foreach(string trackId in trackIds)
        {
            if(!string.IsNullOrWhiteSpace(trackId) && !_ownedTrackIds.Contains(trackId))
                _ownedTrackIds.Add(trackId);
        }

        return this;
    }

    /// <summary>
    /// Ajoute une action déterministe à exécuter quand le bloc stable est actif.
    /// </summary>
    /// <param name="action">Lambda recevant le contexte visuel courant.</param>
    /// <returns>Ce bloc pour continuer la déclaration fluent.</returns>
    public VisualStableBlock Do(Action<VisualContext> action)
    {
        if(action != null)
            _actions.Add(action);

        return this;
    }

    /// <summary>
    /// Échantillonne le bloc stable s'il correspond à la position courante de la note.
    /// </summary>
    /// <param name="context">Contexte visuel courant.</param>
    void IVisualTimelineBlock.Sample(VisualContext context)
    {
        bool active = _kind == VisualStableKind.Before
            ? context.IsBeforeApproach
            : context.SongPosition > context.Note.EndSongPosition + context.DespawnDelay;

        if(!active)
            return;

        using(context.UseOwnedTracks(_ownedTrackIds))
        {
            foreach(Action<VisualContext> action in _actions)
                action(context);
        }
    }
}
