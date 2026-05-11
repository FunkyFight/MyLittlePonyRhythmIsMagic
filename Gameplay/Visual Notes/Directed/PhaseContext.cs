/// <summary>
/// Contexte local d'une phase de timeline pendant son échantillonnage.
/// </summary>
/// <remarks>
/// Une phase expose deux repères : une progression globale dans la fenêtre de la note et une progression locale
/// remappée sur l'intervalle de la phase. Les helpers de crossing utilisent la progression globale précédente
/// pour rester déterministes en rewind/seek.
/// </remarks>
public sealed class PhaseContext
{
    private readonly VisualContext _context;
    private readonly VisualEventGate _eventGate;
    private readonly float _lastGlobalProgress;

    /// <summary>
    /// Crée le contexte d'une phase à partir de la progression globale courante et précédente.
    /// </summary>
    /// <param name="id">Identifiant de la phase.</param>
    /// <param name="globalStart">Début de la phase dans la progression globale.</param>
    /// <param name="globalEnd">Fin de la phase dans la progression globale.</param>
    /// <param name="globalProgress">Progression globale courante.</param>
    /// <param name="lastGlobalProgress">Progression globale du sample précédent.</param>
    /// <param name="context">Contexte visuel parent.</param>
    /// <param name="eventGate">Gate utilisé pour les crossings forward-only.</param>
    internal PhaseContext(
        string id,
        float globalStart,
        float globalEnd,
        float globalProgress,
        float lastGlobalProgress,
        VisualContext context,
        VisualEventGate eventGate)
    {
        Id = id;
        GlobalStart = globalStart;
        GlobalEnd = globalEnd;
        GlobalProgress = globalProgress;
        _lastGlobalProgress = lastGlobalProgress;
        _context = context;
        _eventGate = eventGate;

        LocalProgress = RhythmVisualUtils.GetPhaseProgress(globalProgress, globalStart, globalEnd);
        UnclampedLocalProgress = getUnclampedPhaseProgress(globalProgress, globalStart, globalEnd);
        IsActive = globalProgress >= globalStart && globalProgress <= globalEnd;
        WasActive = !float.IsNaN(lastGlobalProgress) && lastGlobalProgress >= globalStart && lastGlobalProgress <= globalEnd;
    }

    /// <summary>
    /// Identifiant lisible de la phase, utile pour nommer les évènements de crossing.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Début de la phase dans la progression globale.
    /// </summary>
    public float GlobalStart { get; }

    /// <summary>
    /// Fin de la phase dans la progression globale.
    /// </summary>
    public float GlobalEnd { get; }

    /// <summary>
    /// Progression globale courante utilisée pour sampler la phase.
    /// </summary>
    public float GlobalProgress { get; }

    /// <summary>
    /// Progression locale bornée entre <c>0</c> et <c>1</c> dans l'intervalle de la phase.
    /// </summary>
    public float LocalProgress { get; }

    /// <summary>
    /// Progression locale non bornée dans l'intervalle de la phase.
    /// </summary>
    public float UnclampedLocalProgress { get; }

    /// <summary>
    /// Indique si la progression courante est dans l'intervalle de la phase.
    /// </summary>
    public bool IsActive { get; }

    /// <summary>
    /// Indique si la progression précédente était dans l'intervalle de la phase.
    /// </summary>
    public bool WasActive { get; }

    /// <summary>
    /// Indique si la progression vient de franchir le début de la phase vers l'avant.
    /// </summary>
    /// <param name="eventId">Identifiant de l'évènement d'entrée.</param>
    /// <returns><c>true</c> uniquement lors du crossing forward du début de phase.</returns>
    public bool JustEnteredForward(string eventId)
    {
        return CrossedGlobalForward(eventId, GlobalStart);
    }

    /// <summary>
    /// Indique si la progression vient de franchir la fin de la phase vers l'avant.
    /// </summary>
    /// <param name="eventId">Identifiant de l'évènement de sortie.</param>
    /// <returns><c>true</c> uniquement lors du crossing forward de fin de phase.</returns>
    public bool JustExitedForward(string eventId)
    {
        return CrossedGlobalForward(eventId, GlobalEnd);
    }

    /// <summary>
    /// Indique si une progression locale de phase a été franchie vers l'avant.
    /// </summary>
    /// <param name="eventId">Identifiant de l'évènement.</param>
    /// <param name="localProgress">Seuil local à convertir dans l'espace global de la phase.</param>
    /// <returns><c>true</c> uniquement lors du crossing forward du seuil local.</returns>
    public bool CrossedLocalForward(string eventId, float localProgress)
    {
        float threshold = GlobalStart + (GlobalEnd - GlobalStart) * localProgress;
        return CrossedGlobalForward(eventId, threshold);
    }

    /// <summary>
    /// Indique si une progression globale a été franchie vers l'avant.
    /// </summary>
    /// <param name="eventId">Identifiant de l'évènement.</param>
    /// <param name="globalProgress">Seuil global à tester.</param>
    /// <returns><c>true</c> uniquement lors du crossing forward du seuil global.</returns>
    public bool CrossedGlobalForward(string eventId, float globalProgress)
    {
        return _eventGate.ForwardCrossedProgress(eventId, _lastGlobalProgress, GlobalProgress, globalProgress, _context.HasRewound);
    }

    private static float getUnclampedPhaseProgress(float progress, float start, float end)
    {
        if(end <= start)
            return progress >= end ? 1f : 0f;

        return (progress - start) / (end - start);
    }
}
