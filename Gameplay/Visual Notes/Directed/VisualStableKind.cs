/// <summary>
/// Position temporelle d'un bloc stable par rapport à la fenêtre visuelle de la note.
/// </summary>
internal enum VisualStableKind
{
    /// <summary>
    /// Bloc exécuté avant le début de l'approche.
    /// </summary>
    Before,

    /// <summary>
    /// Bloc exécuté après la fin de la fenêtre post-hit/despawn.
    /// </summary>
    After
}
