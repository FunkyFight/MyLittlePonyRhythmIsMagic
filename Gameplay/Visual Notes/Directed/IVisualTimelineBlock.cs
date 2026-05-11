/// <summary>
/// Contrat interne commun aux blocs échantillonnables d'une <see cref="VisualTimeline"/>.
/// </summary>
internal interface IVisualTimelineBlock
{
    /// <summary>
    /// Échantillonne le bloc pour le contexte visuel courant.
    /// </summary>
    /// <param name="context">Contexte contenant la note, le timing et l'accès aux tracks.</param>
    void Sample(VisualContext context);
}
