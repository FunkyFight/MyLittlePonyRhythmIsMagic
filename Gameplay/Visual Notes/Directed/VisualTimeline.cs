using System.Collections.Generic;

/// <summary>
/// Déclaration lambda-first d'une chorégraphie de visual note.
/// </summary>
/// <remarks>
/// Une timeline contient des phases bornées et des blocs stables. Elle ne possède pas de state de scène :
/// elle sample seulement des actions avec un <see cref="VisualContext"/> construit par <see cref="DirectedVisualNote"/>.
/// </remarks>
public sealed class VisualTimeline
{
    private readonly List<IVisualTimelineBlock> _blocks = new();
    private readonly VisualEventGate _eventGate = new();

    /// <summary>
    /// Ajoute une phase sur la progression d'approche globale de la note.
    /// </summary>
    /// <param name="id">Identifiant de phase utilisé par le <see cref="PhaseContext"/>.</param>
    /// <param name="startProgress">Début de la phase dans la progression d'approche.</param>
    /// <param name="endProgress">Fin de la phase dans la progression d'approche.</param>
    /// <returns>La phase créée pour chaîner <c>Owns</c> et <c>Do</c>.</returns>
    public VisualPhase Phase(string id, float startProgress, float endProgress)
    {
        VisualPhase phase = new(id, startProgress, endProgress, VisualPhaseKind.Approach, _eventGate);
        _blocks.Add(phase);
        return phase;
    }

    /// <summary>
    /// Ajoute une phase couvrant toute la fenêtre d'approche, de spawn à hit.
    /// </summary>
    /// <param name="id">Identifiant de phase.</param>
    /// <returns>La phase créée.</returns>
    public VisualPhase DuringApproach(string id)
    {
        return Phase(id, 0f, 1f);
    }

    /// <summary>
    /// Ajoute une phase couvrant toute la fenêtre post-hit de la note.
    /// </summary>
    /// <param name="id">Identifiant de phase.</param>
    /// <returns>La phase créée.</returns>
    public VisualPhase AfterHit(string id)
    {
        VisualPhase phase = new(id, 0f, 1f, VisualPhaseKind.PostHit, _eventGate);
        _blocks.Add(phase);
        return phase;
    }

    /// <summary>
    /// Ajoute une phase post-hit active jusqu'au despawn de la visual note.
    /// </summary>
    /// <param name="id">Identifiant de phase.</param>
    /// <returns>La phase créée.</returns>
    public VisualPhase AfterHitUntilDespawn(string id)
    {
        return AfterHit(id);
    }

    /// <summary>
    /// Ajoute un bloc stable exécuté avant le début de l'approche.
    /// </summary>
    /// <param name="id">Identifiant du bloc stable.</param>
    /// <returns>Le bloc créé pour chaîner <c>Owns</c> et <c>Do</c>.</returns>
    public VisualStableBlock StableBefore(string id = "stable_before")
    {
        VisualStableBlock block = new(id, VisualStableKind.Before);
        _blocks.Add(block);
        return block;
    }

    /// <summary>
    /// Ajoute un bloc stable exécuté après la fenêtre visuelle de la note.
    /// </summary>
    /// <param name="id">Identifiant du bloc stable.</param>
    /// <returns>Le bloc créé pour chaîner <c>Owns</c> et <c>Do</c>.</returns>
    public VisualStableBlock StableAfter(string id = "stable_after")
    {
        VisualStableBlock block = new(id, VisualStableKind.After);
        _blocks.Add(block);
        return block;
    }

    /// <summary>
    /// Échantillonne tous les blocs déclarés avec le contexte courant.
    /// </summary>
    /// <param name="context">Contexte de sampling construit par la visual note.</param>
    public void Sample(VisualContext context)
    {
        foreach(IVisualTimelineBlock block in _blocks)
            block.Sample(context);
    }
}
