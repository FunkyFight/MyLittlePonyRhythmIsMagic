using System;
using System.Collections.Generic;
using Rhythm.Note;

/// <summary>
/// Registre de tracks visuelles et arbitre central des écritures effectuées par les notes dirigées.
/// </summary>
/// <remarks>
/// Le runtime ne décide pas encore lui-même quelle note pilote une track : la scène appelle
/// <see cref="SetDriver"/> pour préserver la logique existante. Les mutations restent sûres : une track
/// inconnue ou pilotée par une autre note produit un no-op via <see cref="VisualContext.Mutate{T}"/>.
/// </remarks>
public sealed class VisualRuntime
{
    private readonly Dictionary<string, VisualTrack> _tracks = new(StringComparer.Ordinal);

    /// <summary>
    /// Enregistre ou remplace une track typée dans ce runtime.
    /// </summary>
    /// <typeparam name="T">Type de la ressource visuelle.</typeparam>
    /// <param name="id">Identifiant utilisé dans les timelines, par exemple <c>background</c>.</param>
    /// <param name="target">Ressource partagée que les notes pourront lire ou muter.</param>
    /// <returns>La track typée enregistrée.</returns>
    public VisualTrack<T> RegisterTrack<T>(string id, T target)
    {
        VisualTrack<T> track = new VisualTrack<T>(id, target);
        _tracks[id] = track;
        return track;
    }

    /// <summary>
    /// Récupère une track par son identifiant.
    /// </summary>
    /// <param name="id">Identifiant de track.</param>
    /// <returns>La track trouvée, ou <c>null</c> si elle n'existe pas.</returns>
    public VisualTrack Track(string id)
    {
        if(string.IsNullOrWhiteSpace(id))
            return null;

        return _tracks.TryGetValue(id, out VisualTrack track) ? track : null;
    }

    /// <summary>
    /// Indique si une note est le driver courant d'une track.
    /// </summary>
    /// <param name="trackId">Identifiant de la track cible.</param>
    /// <param name="note">Note qui demande l'écriture.</param>
    /// <param name="songPosition">Position musicale courante, réservée aux futures policies de driver.</param>
    /// <returns><c>true</c> seulement si la track existe et référence exactement cette note comme driver.</returns>
    public bool CanWrite(string trackId, Note note, double songPosition)
    {
        VisualTrack track = Track(trackId);
        return track != null && ReferenceEquals(track.DriverNote, note);
    }

    /// <summary>
    /// Essaie de lire la ressource d'une track avec un type concret.
    /// </summary>
    /// <typeparam name="T">Type attendu pour la ressource.</typeparam>
    /// <param name="trackId">Identifiant de la track.</param>
    /// <param name="target">Ressource typée si la lecture réussit.</param>
    /// <returns><c>true</c> si la track existe et contient une ressource compatible avec <typeparamref name="T"/>.</returns>
    public bool TryRead<T>(string trackId, out T target)
    {
        if(Track(trackId)?.Target is T typedTarget)
        {
            target = typedTarget;
            return true;
        }

        target = default;
        return false;
    }

    /// <summary>
    /// Définit la note conductrice d'une track pour la frame courante.
    /// </summary>
    /// <param name="trackId">Identifiant de la track à piloter.</param>
    /// <param name="driver">Note autorisée à écrire, ou <c>null</c> pour désactiver l'écriture.</param>
    public void SetDriver(string trackId, Note driver)
    {
        Track(trackId)?.SetDriver(driver);
    }

    /// <summary>
    /// Supprime tous les drivers sans désenregistrer les tracks.
    /// </summary>
    public void ClearDrivers()
    {
        foreach(VisualTrack track in _tracks.Values)
            track.SetDriver(null);
    }
}
