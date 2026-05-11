using System;
using GameCore;
using GameCore.GameObjects;
using Microsoft.Xna.Framework.Graphics;
using MLP_RiM.Elements.Editor;
using Rhythm.Note;

/// <summary>
/// Visual note dirigée qui pilote le scrolling du background de SeaPony Parade après le hit.
/// </summary>
/// <remarks>
/// Cette classe est la première migration vers <see cref="DirectedVisualNote"/>. Elle ne reçoit plus de
/// <c>Func&lt;bool&gt;</c> de ownership : elle déclare la track <c>background</c> dans sa timeline et laisse
/// <see cref="VisualRuntime"/> autoriser l'écriture uniquement pour la note conductrice courante.
/// </remarks>
public class SeaponyBgVisualNote : DirectedVisualNote
{
    private readonly int _backgroundScrollDestinationBeat;

    /// <summary>
    /// Crée la visual note de background pour une note SeaPony.
    /// </summary>
    /// <param name="logicalNote">Note logique SeaPony qui déclenche le scroll.</param>
    /// <param name="runtime">Runtime contenant la track <c>background</c>.</param>
    /// <param name="approachDuration">Durée d'approche conservée pour rester compatible avec le manager.</param>
    /// <param name="backgroundScrollDestinationBeat">Index de beat visuel que le background doit atteindre.</param>
    /// <param name="despawnDelay">Durée pendant laquelle le scroll post-hit est interpolé.</param>
    public SeaponyBgVisualNote(Note logicalNote, VisualRuntime runtime, double approachDuration, int backgroundScrollDestinationBeat, double despawnDelay = 0) : base(logicalNote, runtime, approachDuration, despawnDelay)
    {
        _backgroundScrollDestinationBeat = backgroundScrollDestinationBeat;
    }

    /// <summary>
    /// Déclare la phase post-hit qui interpole la progression du background.
    /// </summary>
    /// <param name="timeline">Timeline fournie par <see cref="DirectedVisualNote"/>.</param>
    protected override void Build(VisualTimeline timeline)
    {
        timeline.AfterHitUntilDespawn("background_scroll")
            .Owns("background")
            .DoOwned<InfiniteScrollBackground>("background", (ctx, phase, background) =>
            {
                if(!SeaponyNoteCodec.TryReadAction(ctx.Note?.AdditionnalData, out _))
                    return;

                float interpolated = Interpolation.EaseOutQuart(ctx.PostHitProgress);
                background.Progression = Single.Lerp(_backgroundScrollDestinationBeat - 1, _backgroundScrollDestinationBeat, interpolated);
            });
    }

    /// <summary>
    /// Aucun rendu direct : cette visual note modifie seulement la track de background partagée.
    /// </summary>
    /// <param name="spriteBatch">Batch de rendu fourni par le manager.</param>
    public override void Draw(SpriteBatch spriteBatch)
    {
    }
}
