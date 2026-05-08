namespace MLP_RiM.Elements.Editor;

/// <summary>
/// Builder declaratif de la note runtime produite par un <see cref="SimpleRhythmGame{TAction}"/>.
/// </summary>
/// <typeparam name="TAction">Enum qui liste les actions runtime du rhythm game.</typeparam>
public sealed class RuntimeNoteBuilder<TAction>
    where TAction : struct, System.Enum
{
    private readonly SimpleRuntimeNoteConfiguration _configuration;

    internal RuntimeNoteBuilder(SimpleRuntimeNoteConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// Definit l'action d'input demandee au joueur pour reussir la note runtime.
    /// </summary>
    /// <param name="inputAction">Nom de l'action d'input. <c>ReactMain</c> est utilise si la valeur est vide.</param>
    /// <returns>Ce builder pour continuer la declaration.</returns>
    public RuntimeNoteBuilder<TAction> Input(string inputAction)
    {
        _configuration.InputAction = string.IsNullOrWhiteSpace(inputAction) ? "ReactMain" : inputAction;
        return this;
    }

    /// <summary>
    /// Definit la duree tenue par defaut de la note runtime.
    /// </summary>
    /// <param name="beats">Duree en beats.</param>
    /// <returns>Ce builder pour continuer la declaration.</returns>
    public RuntimeNoteBuilder<TAction> Hold(double beats)
    {
        _configuration.HoldBeats = System.Math.Max(0.0, beats);
        return this;
    }

    /// <summary>
    /// Definit l'espace occupe par la note sur la timeline de l'editeur.
    /// </summary>
    /// <param name="beforeBeats">Nombre de beats occupes avant le beat de la note.</param>
    /// <param name="afterBeats">Nombre de beats occupes apres le beat de la note.</param>
    /// <returns>Ce builder pour continuer la declaration.</returns>
    public RuntimeNoteBuilder<TAction> Occupies(double beforeBeats, double afterBeats)
    {
        _configuration.OccupyBeforeBeats = System.Math.Max(0.0, beforeBeats);
        _configuration.OccupyAfterBeats = System.Math.Max(0.0, afterBeats);
        return this;
    }

    /// <summary>
    /// Definit la fenetre de hit et de conflit generale de la note.
    /// </summary>
    /// <param name="beforeBeats">Nombre de beats avant le beat de la note.</param>
    /// <param name="afterBeats">Nombre de beats apres le beat de la note.</param>
    /// <returns>Ce builder pour continuer la declaration.</returns>
    public RuntimeNoteBuilder<TAction> HitWindow(double beforeBeats, double afterBeats)
    {
        _configuration.HitWindowBeforeBeats = System.Math.Max(0.0, beforeBeats);
        _configuration.HitWindowAfterBeats = System.Math.Max(0.0, afterBeats);
        return this;
    }

    /// <summary>
    /// Definit la fenetre de conflit entre notes de meme variant.
    /// </summary>
    /// <param name="beforeBeats">Nombre de beats avant le beat de la note.</param>
    /// <param name="afterBeats">Nombre de beats apres le beat de la note.</param>
    /// <returns>Ce builder pour continuer la declaration.</returns>
    public RuntimeNoteBuilder<TAction> SameVariantHitWindow(double beforeBeats, double afterBeats)
    {
        _configuration.HasSameVariantHitWindow = true;
        _configuration.SameVariantHitWindowBeforeBeats = System.Math.Max(0.0, beforeBeats);
        _configuration.SameVariantHitWindowAfterBeats = System.Math.Max(0.0, afterBeats);
        return this;
    }

    /// <summary>
    /// Remplace le calcul de timing par defaut de la note runtime.
    /// </summary>
    /// <param name="timing">Strategie de timing a utiliser.</param>
    /// <returns>Ce builder pour continuer la declaration.</returns>
    public RuntimeNoteBuilder<TAction> Timing(IEditorNoteTiming timing)
    {
        _configuration.Timing = timing ?? new FixedEditorNoteTiming();
        return this;
    }
}

internal sealed class SimpleRuntimeNoteConfiguration
{
    public string NoteId { get; set; } = "note";
    public string InputAction { get; set; } = "ReactMain";
    public double HoldBeats { get; set; }
    public double OccupyBeforeBeats { get; set; }
    public double OccupyAfterBeats { get; set; }
    public double HitWindowBeforeBeats { get; set; }
    public double HitWindowAfterBeats { get; set; }
    public bool HasSameVariantHitWindow { get; set; }
    public double SameVariantHitWindowBeforeBeats { get; set; }
    public double SameVariantHitWindowAfterBeats { get; set; }
    public IEditorNoteTiming Timing { get; set; } = new FixedEditorNoteTiming();
}
