using System;
using Rhythm.Note;

/// <summary>
/// Diagnostic émis quand une mutation de track est ignorée par sécurité.
/// </summary>
public sealed class VisualMutationIgnored
{
    /// <summary>
    /// Crée un diagnostic d'ignorance de mutation.
    /// </summary>
    public VisualMutationIgnored(string trackId, Note callerNote, Note driverNote, Type expectedType, double songPosition, VisualMutationIgnoredReason reason)
    {
        TrackId = trackId;
        CallerNote = callerNote;
        DriverNote = driverNote;
        ExpectedType = expectedType;
        SongPosition = songPosition;
        Reason = reason;
    }

    /// <summary>
    /// Track ciblée par la mutation.
    /// </summary>
    public string TrackId { get; }

    /// <summary>
    /// Note qui a tenté la mutation.
    /// </summary>
    public Note CallerNote { get; }

    /// <summary>
    /// Driver courant de la track, si elle existe.
    /// </summary>
    public Note DriverNote { get; }

    /// <summary>
    /// Type demandé par la mutation.
    /// </summary>
    public Type ExpectedType { get; }

    /// <summary>
    /// Position musicale au moment du no-op.
    /// </summary>
    public double SongPosition { get; }

    /// <summary>
    /// Raison précise du no-op.
    /// </summary>
    public VisualMutationIgnoredReason Reason { get; }
}
