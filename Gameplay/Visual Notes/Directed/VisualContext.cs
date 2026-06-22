using System;
using System.Collections.Generic;
using GameCore.Animation;
using GameCore.Audio;
using GameCore.Scenes;
using Rhythm.Note;
using Rhythm.Note.Visual;

/// <summary>
/// Contexte d'échantillonnage transmis aux lambdas d'une <see cref="VisualTimeline"/>.
/// </summary>
/// <remarks>
/// Le contexte expose les informations de timing de la note courante et les opérations sûres sur les tracks.
/// Une mutation doit passer par <see cref="Mutate{T}"/> : elle réussit seulement si le bloc de timeline a déclaré
/// posséder la track via <c>Owns</c> et si le <see cref="VisualRuntime"/> autorise la note courante à écrire.
/// </remarks>
public sealed class VisualContext
{
    private readonly VisualRuntime _runtime;
    private readonly VisualEventGate _eventGate;
    private HashSet<string> _ownedTrackIds = new(StringComparer.Ordinal);

    /// <summary>
    /// Crée le contexte utilisé pour sampler une timeline à une position musicale donnée.
    /// </summary>
    /// <param name="note">Note logique représentée par la visual note.</param>
    /// <param name="songPosition">Position musicale courante en secondes.</param>
    /// <param name="lastSongPosition">Position musicale observée au sample précédent, ou <see cref="double.NaN"/> au premier sample.</param>
    /// <param name="approachDuration">Durée d'approche de la visual note.</param>
    /// <param name="despawnDelay">Durée de persistance après la fin logique de la note.</param>
    /// <param name="runtime">Runtime qui contient les tracks partagées.</param>
    /// <param name="state">État déterministe calculé par <see cref="VisualNote"/> pour ce sample.</param>
    /// <param name="eventGate">Gate utilisé pour les déclenchements forward-only.</param>
    public VisualContext(
        Note note,
        double songPosition,
        double lastSongPosition,
        double approachDuration,
        double despawnDelay,
        VisualRuntime runtime,
        VisualNoteState state,
        VisualEventGate eventGate)
    {
        Note = note ?? throw new ArgumentNullException(nameof(note));
        SongPosition = songPosition;
        LastSongPosition = lastSongPosition;
        ApproachDuration = approachDuration;
        DespawnDelay = despawnDelay;
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _eventGate = eventGate ?? throw new ArgumentNullException(nameof(eventGate));

        NoteProgress = (float)state.Progress;
        UnclampedNoteProgress = (float)state.UnclampedTravelProgress;
        HoldProgress = (float)state.HoldProgress;
        PostHitProgress = (float)state.PostHitProgress;
        LastNoteProgress = calculateLastNoteProgress();
        LastHoldProgress = calculateLastHoldProgress();
        LastPostHitProgress = calculateLastPostHitProgress();
        HasRewound = RhythmVisualUtils.HasRewound(songPosition, lastSongPosition);
        IsBeforeApproach = songPosition < Note.SongPosition - approachDuration;
        IsAtOrAfterHit = songPosition >= Note.SongPosition;
    }

    /// <summary>
    /// Note logique représentée par la visual note en cours de sampling.
    /// </summary>
    public Note Note { get; }

    /// <summary>
    /// Position musicale courante en secondes.
    /// </summary>
    public double SongPosition { get; }

    /// <summary>
    /// Position musicale du sample précédent, ou <see cref="double.NaN"/> si aucun sample précédent n'existe.
    /// </summary>
    public double LastSongPosition { get; }

    /// <summary>
    /// Durée d'approche configurée sur la visual note.
    /// </summary>
    public double ApproachDuration { get; }

    /// <summary>
    /// Durée de persistance après la fin logique de la note.
    /// </summary>
    public double DespawnDelay { get; }

    /// <summary>
    /// Progression d'approche bornée entre <c>0</c> et <c>1</c>.
    /// </summary>
    public float NoteProgress { get; }

    /// <summary>
    /// Progression d'approche non bornée, utile pour les phases qui continuent après le hit.
    /// </summary>
    public float UnclampedNoteProgress { get; }

    /// <summary>
    /// Progression post-hit bornée entre la fin logique de la note et son despawn.
    /// </summary>
    public float PostHitProgress { get; }

    /// <summary>
    /// Progression tenue bornée entre le hit et le release.
    /// </summary>
    public float HoldProgress { get; }

    /// <summary>
    /// Indique si la position musicale a reculé depuis le sample précédent.
    /// </summary>
    public bool HasRewound { get; }

    /// <summary>
    /// Indique si le sample est avant le début de la fenêtre d'approche.
    /// </summary>
    public bool IsBeforeApproach { get; }

    /// <summary>
    /// Indique si le sample est au hit ou après le hit de la note.
    /// </summary>
    public bool IsAtOrAfterHit { get; }

    /// <summary>
    /// Progression d'approche du sample précédent, ou <see cref="float.NaN"/> au premier sample.
    /// </summary>
    public float LastNoteProgress { get; }

    /// <summary>
    /// Progression post-hit du sample précédent, ou <see cref="float.NaN"/> au premier sample.
    /// </summary>
    public float LastPostHitProgress { get; }

    /// <summary>
    /// Progression tenue du sample précédent, ou <see cref="float.NaN"/> au premier sample.
    /// </summary>
    public float LastHoldProgress { get; }

    /// <summary>
    /// Indique si la lambda courante peut écrire sur une track.
    /// </summary>
    /// <param name="trackId">Identifiant de la track cible.</param>
    /// <returns><c>true</c> si le bloc courant possède la track et si la note courante est son driver.</returns>
    public bool CanWrite(string trackId)
    {
        return _ownedTrackIds.Contains(trackId) && _runtime.CanWrite(trackId, Note, SongPosition);
    }

    /// <summary>
    /// Essaie de lire une ressource de track sans vérifier l'ownership d'écriture.
    /// </summary>
    /// <typeparam name="T">Type attendu de la ressource.</typeparam>
    /// <param name="trackId">Identifiant de la track.</param>
    /// <param name="target">Ressource typée si la lecture réussit.</param>
    /// <returns><c>true</c> si la track existe et contient une ressource compatible.</returns>
    public bool TryRead<T>(string trackId, out T target)
    {
        return _runtime.TryRead(trackId, out target);
    }

    /// <summary>
    /// Lit une ressource de track et échoue explicitement si la track est absente ou d'un autre type.
    /// </summary>
    /// <typeparam name="T">Type attendu de la ressource.</typeparam>
    /// <param name="trackId">Identifiant de la track.</param>
    /// <returns>Ressource typée de la track.</returns>
    public T Read<T>(string trackId)
    {
        if(TryRead(trackId, out T target))
            return target;

        throw new InvalidOperationException($"Visual track '{trackId}' was not found or is not a {typeof(T).Name}.");
    }

    /// <summary>
    /// Applique une mutation safe sur une ressource de track.
    /// </summary>
    /// <typeparam name="T">Type attendu de la ressource à muter.</typeparam>
    /// <param name="trackId">Identifiant de la track cible.</param>
    /// <param name="mutation">Action à exécuter si l'ownership et le driver autorisent l'écriture.</param>
    /// <remarks>
    /// Cette méthode no-op si la mutation est nulle, si la track n'est pas possédée par le bloc courant,
    /// si la note n'est pas le driver, ou si la ressource n'a pas le type attendu.
    /// </remarks>
    public void Mutate<T>(string trackId, Action<T> mutation)
    {
        if(mutation == null)
            return;

        if(!_ownedTrackIds.Contains(trackId))
        {
            _runtime.ReportIgnoredMutation(trackId, Note, typeof(T), SongPosition, VisualMutationIgnoredReason.TrackNotOwned);
            return;
        }

        if(!_runtime.TryGetWritableTrack(trackId, Note, SongPosition, out VisualTrack track, out VisualMutationIgnoredReason reason))
        {
            _runtime.ReportIgnoredMutation(trackId, Note, typeof(T), SongPosition, reason);
            return;
        }

        if(track.Target is not T target)
        {
            _runtime.ReportIgnoredMutation(trackId, Note, typeof(T), SongPosition, VisualMutationIgnoredReason.WrongTargetType);
            return;
        }

        mutation(target);
    }

    /// <summary>
    /// Indique si un seuil temporel a été franchi vers l'avant depuis le sample précédent.
    /// </summary>
    /// <param name="eventId">Identifiant logique de l'évènement.</param>
    /// <param name="songTime">Seuil temporel en secondes.</param>
    /// <returns><c>true</c> seulement sur un crossing forward valide.</returns>
    public bool ForwardCrossed(string eventId, double songTime)
    {
        return _eventGate.ForwardCrossed(eventId, LastSongPosition, SongPosition, songTime, HasRewound);
    }

    /// <summary>
    /// Indique si une progression de note a franchi un seuil vers l'avant depuis le sample précédent.
    /// </summary>
    /// <param name="eventId">Identifiant logique de l'évènement.</param>
    /// <param name="noteProgress">Seuil de progression d'approche.</param>
    /// <returns><c>true</c> seulement sur un crossing forward valide.</returns>
    public bool ForwardCrossedProgress(string eventId, float noteProgress)
    {
        return _eventGate.ForwardCrossedProgress(eventId, LastNoteProgress, NoteProgress, noteProgress, HasRewound);
    }

    /// <summary>
    /// Force l'état d'une machine d'animation stockée dans une track.
    /// </summary>
    /// <param name="animationTrackId">Identifiant de la track contenant une <see cref="AnimationStateMachine"/>.</param>
    /// <param name="stateName">Nom de l'état à forcer.</param>
    /// <param name="reenterViaState">État intermédiaire optionnel pour forcer une ré-entrée dans le même état.</param>
    public void ForceAnimation(string animationTrackId, string stateName, string reenterViaState = null)
    {
        Mutate<AnimationStateMachine>(animationTrackId, state => RhythmVisualUtils.ForceAnimationState(state, stateName, reenterViaState));
    }

    /// <summary>
    /// Joue un son uniquement si un seuil temporel vient d'être franchi vers l'avant.
    /// </summary>
    /// <param name="eventId">Identifiant logique de l'évènement sonore.</param>
    /// <param name="songTime">Seuil temporel à franchir.</param>
    /// <param name="scene">Scène propriétaire utilisée par le système SFX.</param>
    /// <param name="path">Chemin du fichier sonore.</param>
    /// <param name="volume">Volume du son.</param>
    public void PlaySfxOnForwardCross(string eventId, double songTime, Scene scene, string path, float volume)
    {
        if(scene == null || !ForwardCrossed(eventId, songTime))
            return;

        SFX.Play(scene, path, volume);
    }

    /// <summary>
    /// Exécute une action de spawn uniquement si un seuil temporel vient d'être franchi vers l'avant.
    /// </summary>
    /// <param name="eventId">Identifiant logique de l'évènement de spawn.</param>
    /// <param name="songTime">Seuil temporel à franchir.</param>
    /// <param name="spawn">Action de création à exécuter.</param>
    public void SpawnOnForwardCross(string eventId, double songTime, Action spawn)
    {
        if(spawn != null && ForwardCrossed(eventId, songTime))
            spawn();
    }

    /// <summary>
    /// Active temporairement la liste de tracks possédées par le bloc de timeline en cours.
    /// </summary>
    /// <param name="trackIds">Tracks déclarées par <c>Owns</c> sur le bloc courant.</param>
    /// <returns>Scope qui restaure la liste précédente à la fin du bloc.</returns>
    internal IDisposable UseOwnedTracks(IReadOnlyCollection<string> trackIds)
    {
        HashSet<string> previous = _ownedTrackIds;
        _ownedTrackIds = trackIds == null
            ? new HashSet<string>(StringComparer.Ordinal)
            : new HashSet<string>(trackIds, StringComparer.Ordinal);

        return new OwnedTrackScope(this, previous);
    }

    private float calculateLastNoteProgress()
    {
        if(double.IsNaN(LastSongPosition))
            return float.NaN;

        return RhythmVisualUtils.GetApproachProgress(LastSongPosition, Note.SongPosition, ApproachDuration);
    }

    private float calculateLastPostHitProgress()
    {
        if(double.IsNaN(LastSongPosition))
            return float.NaN;

        return (float)RhythmVisualUtils.GetProgression(Note.EndSongPosition, Note.EndSongPosition + DespawnDelay, LastSongPosition);
    }

    private float calculateLastHoldProgress()
    {
        if(double.IsNaN(LastSongPosition))
            return float.NaN;

        return (float)RhythmVisualUtils.GetProgression(Note.SongPosition, Note.EndSongPosition, LastSongPosition);
    }

    /// <summary>
    /// Scope interne qui restaure l'ownership de tracks après l'exécution d'un bloc de timeline.
    /// </summary>
    private sealed class OwnedTrackScope : IDisposable
    {
        private readonly VisualContext _context;
        private readonly HashSet<string> _previous;
        private bool _disposed;

        /// <summary>
        /// Crée un scope de restauration pour l'ownership courant.
        /// </summary>
        /// <param name="context">Contexte à restaurer.</param>
        /// <param name="previous">Liste précédente de tracks possédées.</param>
        public OwnedTrackScope(VisualContext context, HashSet<string> previous)
        {
            _context = context;
            _previous = previous;
        }

        /// <summary>
        /// Restaure l'ownership précédent une seule fois.
        /// </summary>
        public void Dispose()
        {
            if(_disposed)
                return;

            _context._ownedTrackIds = _previous;
            _disposed = true;
        }
    }
}
