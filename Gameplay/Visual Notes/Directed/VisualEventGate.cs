/// <summary>
/// Centralise les déclenchements one-shot basés sur un crossing vers l'avant.
/// </summary>
/// <remarks>
/// Cette classe est volontairement sans état : elle ne mémorise pas les évènements déclenchés.
/// Un évènement se déclenche quand l'échantillon précédent est strictement avant le seuil et
/// l'échantillon courant au seuil ou après, sans rewind et sans premier sample indéterminé.
/// </remarks>
public sealed class VisualEventGate
{
    /// <summary>
    /// Teste si la position musicale a franchi un seuil temporel vers l'avant.
    /// </summary>
    /// <param name="eventId">Identifiant logique de l'évènement, conservé pour l'API et les futures extensions.</param>
    /// <param name="lastSongPosition">Position musicale de l'échantillon précédent.</param>
    /// <param name="songPosition">Position musicale courante.</param>
    /// <param name="eventSongPosition">Seuil temporel de l'évènement.</param>
    /// <param name="hasRewound">Indique si la timeline a reculé entre les deux échantillons.</param>
    /// <returns><c>true</c> uniquement lors du franchissement forward du seuil.</returns>
    public bool ForwardCrossed(string eventId, double lastSongPosition, double songPosition, double eventSongPosition, bool hasRewound)
    {
        return !hasRewound
            && !double.IsNaN(lastSongPosition)
            && lastSongPosition < eventSongPosition
            && songPosition >= eventSongPosition;
    }

    /// <summary>
    /// Teste si une progression normalisée a franchi un seuil vers l'avant.
    /// </summary>
    /// <param name="eventId">Identifiant logique de l'évènement, conservé pour l'API et les futures extensions.</param>
    /// <param name="lastProgress">Progression de l'échantillon précédent.</param>
    /// <param name="progress">Progression courante.</param>
    /// <param name="eventProgress">Seuil de progression à franchir.</param>
    /// <param name="hasRewound">Indique si la timeline a reculé entre les deux échantillons.</param>
    /// <returns><c>true</c> uniquement lors du franchissement forward du seuil.</returns>
    public bool ForwardCrossedProgress(string eventId, float lastProgress, float progress, float eventProgress, bool hasRewound)
    {
        return !hasRewound
            && !float.IsNaN(lastProgress)
            && lastProgress < eventProgress
            && progress >= eventProgress;
    }
}
