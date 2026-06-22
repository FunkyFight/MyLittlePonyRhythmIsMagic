using System;
using System.Collections.Generic;

/// <summary>
/// Bloc de timeline actif sur un intervalle de progression.
/// </summary>
/// <remarks>
/// Une phase déclare les tracks qu'elle possède avec <see cref="Owns"/> puis enregistre une ou plusieurs lambdas.
/// Le sampling construit un <see cref="PhaseContext"/> qui fournit les progressions locale/globale et les helpers
/// de crossing forward-only.
/// </remarks>
public sealed class VisualPhase : IVisualTimelineBlock
{
    private readonly string _id;
    private readonly float _startProgress;
    private readonly float _endProgress;
    private readonly VisualPhaseKind _kind;
    private readonly VisualEventGate _eventGate;
    private readonly List<string> _ownedTrackIds = new();
    private readonly List<Action<VisualContext, PhaseContext>> _actions = new();

    /// <summary>
    /// Crée une phase interne pour une timeline.
    /// </summary>
    /// <param name="id">Identifiant de phase.</param>
    /// <param name="startProgress">Début de l'intervalle de progression.</param>
    /// <param name="endProgress">Fin de l'intervalle de progression.</param>
    /// <param name="kind">Type de progression à utiliser.</param>
    /// <param name="eventGate">Gate partagé par la timeline pour les crossings.</param>
    internal VisualPhase(string id, float startProgress, float endProgress, VisualPhaseKind kind, VisualEventGate eventGate)
    {
        _id = id;
        _startProgress = startProgress;
        _endProgress = endProgress;
        _kind = kind;
        _eventGate = eventGate;
    }

    /// <summary>
    /// Déclare les tracks que cette phase est autorisée à muter.
    /// </summary>
    /// <param name="trackIds">Identifiants de tracks possédées pendant l'exécution des actions de la phase.</param>
    /// <returns>Cette phase pour continuer la déclaration fluent.</returns>
    public VisualPhase Owns(params string[] trackIds)
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
    /// Ajoute une action à exécuter quand la phase est active.
    /// </summary>
    /// <param name="action">Lambda recevant le contexte global et le contexte de phase.</param>
    /// <returns>Cette phase pour continuer la déclaration fluent.</returns>
    public VisualPhase Do(Action<VisualContext, PhaseContext> action)
    {
        if(action != null)
            _actions.Add(action);

        return this;
    }

    /// <summary>
    /// Ajoute une action typée qui mute une track possédée si le driver courant l'autorise.
    /// </summary>
    /// <typeparam name="T">Type attendu de la ressource stockée dans la track.</typeparam>
    /// <param name="trackId">Identifiant de la track à posséder et muter.</param>
    /// <param name="action">Lambda appelée avec la ressource typée lorsque la mutation est autorisée.</param>
    /// <returns>Cette phase pour continuer la déclaration fluent.</returns>
    public VisualPhase DoOwned<T>(string trackId, Action<VisualContext, PhaseContext, T> action)
    {
        if(action == null)
            return this;

        Owns(trackId);
        return Do((context, phase) => context.Mutate<T>(trackId, target => action(context, phase, target)));
    }

    /// <summary>
    /// Échantillonne la phase si la position musicale courante est dans sa fenêtre.
    /// </summary>
    /// <param name="context">Contexte visuel courant.</param>
    void IVisualTimelineBlock.Sample(VisualContext context)
    {
        if(!isInWindow(context))
            return;

        float globalProgress = getGlobalProgress(context);
        float lastGlobalProgress = getLastGlobalProgress(context);
        PhaseContext phase = new(_id, _startProgress, _endProgress, globalProgress, lastGlobalProgress, context, _eventGate);

        if(!phase.IsActive)
            return;

        using(context.UseOwnedTracks(_ownedTrackIds))
        {
            foreach(Action<VisualContext, PhaseContext> action in _actions)
                action(context, phase);
        }
    }

    private bool isInWindow(VisualContext context)
    {
        if(_kind == VisualPhaseKind.Hold)
            return context.Note.HoldDuration > 0
                && context.SongPosition >= context.Note.SongPosition
                && context.SongPosition <= context.Note.EndSongPosition;

        if(_kind == VisualPhaseKind.PostHit)
            return context.SongPosition >= context.Note.EndSongPosition
                && context.SongPosition <= context.Note.EndSongPosition + context.DespawnDelay;

        return context.SongPosition >= context.Note.SongPosition - context.ApproachDuration
            && context.SongPosition <= context.Note.SongPosition;
    }

    private float getGlobalProgress(VisualContext context)
    {
        return _kind switch
        {
            VisualPhaseKind.Hold => context.HoldProgress,
            VisualPhaseKind.PostHit => context.PostHitProgress,
            _ => context.NoteProgress
        };
    }

    private float getLastGlobalProgress(VisualContext context)
    {
        return _kind switch
        {
            VisualPhaseKind.Hold => context.LastHoldProgress,
            VisualPhaseKind.PostHit => context.LastPostHitProgress,
            _ => context.LastNoteProgress
        };
    }
}
