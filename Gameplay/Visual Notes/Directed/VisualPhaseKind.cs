/// <summary>
/// Type de progression globale utilisée par une <see cref="VisualPhase"/>.
/// </summary>
internal enum VisualPhaseKind
{
    /// <summary>
    /// Phase basée sur la progression d'approche de la note.
    /// </summary>
    Approach,

    /// <summary>
    /// Phase basée sur la progression entre la fin logique de la note et son despawn.
    /// </summary>
    PostHit
}
