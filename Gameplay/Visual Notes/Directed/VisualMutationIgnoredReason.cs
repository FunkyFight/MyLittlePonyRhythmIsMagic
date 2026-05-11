/// <summary>
/// Raison pour laquelle une mutation de track a été ignorée.
/// </summary>
public enum VisualMutationIgnoredReason
{
    /// <summary>
    /// Le bloc courant n'a pas déclaré la track dans <c>Owns(...)</c>.
    /// </summary>
    TrackNotOwned,

    /// <summary>
    /// Aucune track avec cet identifiant n'est enregistrée dans le runtime.
    /// </summary>
    TrackMissing,

    /// <summary>
    /// La track existe mais n'a aucun driver courant.
    /// </summary>
    NoDriver,

    /// <summary>
    /// La track est drivé par une autre note que la note appelante.
    /// </summary>
    WrongDriver,

    /// <summary>
    /// La ressource de la track n'est pas compatible avec le type demandé.
    /// </summary>
    WrongTargetType
}
