using System;
using Rhythm.Note;

/// <summary>
/// Représente une ressource visuelle partagée qu'une ou plusieurs notes visuelles peuvent vouloir modifier.
/// </summary>
/// <remarks>
/// Une track sert de point d'arbitrage entre les notes qui coexistent dans les fenêtres de look-ahead/look-behind.
/// Le <see cref="DriverNote"/> indique la note actuellement autorisée par la scène à écrire sur cette ressource.
/// </remarks>
public class VisualTrack
{
    /// <summary>
    /// Crée une track non typée autour d'une ressource cible.
    /// </summary>
    /// <param name="id">Identifiant stable utilisé par les timelines et le runtime.</param>
    /// <param name="target">Objet partagé à piloter, par exemple un background, un sprite ou une machine d'animation.</param>
    public VisualTrack(string id, object target)
    {
        if(string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Track id cannot be empty.", nameof(id));

        Id = id;
        Target = target ?? throw new ArgumentNullException(nameof(target));
    }

    /// <summary>
    /// Identifiant unique de la track dans son <see cref="VisualRuntime"/>.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Ressource visuelle partagée stockée par la track.
    /// </summary>
    public object Target { get; }

    /// <summary>
    /// Note courante autorisée à écrire sur cette track, ou <c>null</c> si aucune note ne la pilote.
    /// </summary>
    public Note DriverNote { get; private set; }

    /// <summary>
    /// Définit la note autorisée à muter cette track pour la frame courante.
    /// </summary>
    /// <param name="note">Note conductrice, ou <c>null</c> pour rendre la track non pilotée.</param>
    public void SetDriver(Note note)
    {
        DriverNote = note;
    }
}

/// <summary>
/// Version typée de <see cref="VisualTrack"/> qui évite les casts côté code appelant.
/// </summary>
/// <typeparam name="T">Type de la ressource visuelle stockée.</typeparam>
public sealed class VisualTrack<T> : VisualTrack
{
    /// <summary>
    /// Crée une track typée autour d'une ressource cible.
    /// </summary>
    /// <param name="id">Identifiant stable utilisé par les timelines et le runtime.</param>
    /// <param name="target">Ressource visuelle typée à piloter.</param>
    public VisualTrack(string id, T target)
        : base(id, target)
    {
        Target = target;
    }

    /// <summary>
    /// Ressource visuelle partagée avec son type concret.
    /// </summary>
    public new T Target { get; }
}
