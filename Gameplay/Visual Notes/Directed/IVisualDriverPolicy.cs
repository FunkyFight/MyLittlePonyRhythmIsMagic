using Rhythm.Note;

/// <summary>
/// Règle capable de choisir la note qui pilote une track visuelle à une position musicale donnée.
/// </summary>
public interface IVisualDriverPolicy
{
    /// <summary>
    /// Résout la note conductrice d'une track pour le sample courant.
    /// </summary>
    /// <param name="context">Contexte contenant le runtime, la track, les notes du chart et la position musicale.</param>
    /// <returns>Note autorisée à écrire sur la track, ou <c>null</c> si aucune note ne doit la piloter.</returns>
    Note ResolveDriver(VisualDriverContext context);
}
