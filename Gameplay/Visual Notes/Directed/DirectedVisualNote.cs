using System;
using Microsoft.Xna.Framework.Graphics;
using Rhythm.Note;
using Rhythm.Note.Visual;

/// <summary>
/// Base de visual note qui décrit sa chorégraphie avec une <see cref="VisualTimeline"/>.
/// </summary>
/// <remarks>
/// Cette couche reste compatible avec <see cref="VisualNoteManager{T}"/> : elle dérive de <see cref="VisualNote"/>,
/// appelle le calcul d'état de la classe de base, construit un <see cref="VisualContext"/> puis sample la timeline.
/// La timeline est construite paresseusement au premier <see cref="Update"/> pour éviter d'appeler du code virtuel
/// avant que le constructeur de la classe dérivée ait initialisé ses champs.
/// </remarks>
public abstract class DirectedVisualNote : VisualNote
{
    private readonly VisualEventGate _eventGate = new();
    private double _lastSongPosition = double.NaN;
    private bool _timelineBuilt;

    /// <summary>
    /// Crée une visual note dirigée avec un runtime de tracks partagé.
    /// </summary>
    /// <param name="note">Note logique représentée par cette visual note.</param>
    /// <param name="runtime">Runtime contenant les tracks que la timeline peut lire ou muter.</param>
    /// <param name="approachDuration">Durée d'approche transmise à <see cref="VisualNote"/>.</param>
    /// <param name="despawnDelay">Durée post-hit transmise à <see cref="VisualNote"/>.</param>
    protected DirectedVisualNote(Note note, VisualRuntime runtime, double approachDuration, double despawnDelay = 0)
        : base(note, approachDuration, despawnDelay)
    {
        Runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        Timeline = new VisualTimeline();
    }

    /// <summary>
    /// Runtime qui arbitre les tracks utilisées par cette visual note.
    /// </summary>
    protected VisualRuntime Runtime { get; }

    /// <summary>
    /// Timeline déclarative samplée à chaque update.
    /// </summary>
    protected VisualTimeline Timeline { get; }

    /// <summary>
    /// Déclare les phases et blocs stables de la visual note.
    /// </summary>
    /// <param name="timeline">Timeline à remplir avec des lambdas de chorégraphie.</param>
    protected abstract void Build(VisualTimeline timeline);

    /// <summary>
    /// Met à jour l'état déterministe de la note puis sample sa timeline.
    /// </summary>
    /// <param name="currentSongPosition">Position musicale courante en secondes.</param>
    public sealed override void Update(double currentSongPosition)
    {
        ensureTimelineBuilt();
        base.Update(currentSongPosition);

        VisualContext context = new(
            Note,
            currentSongPosition,
            _lastSongPosition,
            ApproachDuration,
            DespawnDelay,
            Runtime,
            State,
            _eventGate);

        Timeline.Sample(context);
        _lastSongPosition = currentSongPosition;
    }

    /// <summary>
    /// Dessin par défaut vide : les visual notes dirigées peuvent piloter des ressources externes sans dessiner elles-mêmes.
    /// </summary>
    /// <param name="spriteBatch">Batch de rendu MonoGame fourni par le manager.</param>
    public override void Draw(SpriteBatch spriteBatch)
    {
    }

    private void ensureTimelineBuilt()
    {
        if(_timelineBuilt)
            return;

        Build(Timeline);
        _timelineBuilt = true;
    }
}
