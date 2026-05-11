using System.Collections.Generic;
using Rhythm.Note;

/// <summary>
/// Contexte transmis aux policies de driver pendant <see cref="VisualRuntime.ResolveDrivers"/>.
/// </summary>
public sealed class VisualDriverContext
{
    /// <summary>
    /// Crée un contexte de résolution pour une track.
    /// </summary>
    public VisualDriverContext(VisualRuntime runtime, VisualTrack track, double songPosition, IReadOnlyList<Note> notes)
    {
        Runtime = runtime;
        Track = track;
        TrackId = track?.Id;
        SongPosition = songPosition;
        Notes = notes;
    }

    /// <summary>
    /// Runtime qui orchestre la résolution.
    /// </summary>
    public VisualRuntime Runtime { get; }

    /// <summary>
    /// Track pour laquelle un driver est résolu.
    /// </summary>
    public VisualTrack Track { get; }

    /// <summary>
    /// Identifiant de la track courante.
    /// </summary>
    public string TrackId { get; }

    /// <summary>
    /// Position musicale courante en secondes.
    /// </summary>
    public double SongPosition { get; }

    /// <summary>
    /// Notes du chart utilisables par la policy.
    /// </summary>
    public IReadOnlyList<Note> Notes { get; }
}
