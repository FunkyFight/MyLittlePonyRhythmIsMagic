using GameCore.GameObjects;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rhythm.Note;

public sealed class DerpyStampedeEntryVisualNote : DirectedVisualNote
{
    public const string DerpyTrackId = "derpy";

    private readonly Vector2 _startPosition;
    private readonly Vector2 _targetPosition;

    public DerpyStampedeEntryVisualNote(Note note, VisualRuntime runtime, Vector2 targetPosition, double approachDuration) : base(note, runtime, approachDuration)
    {
        _targetPosition = targetPosition;
        _startPosition = getBottomPosition(targetPosition);
    }

    protected override void Build(VisualTimeline timeline)
    {
        timeline.StableBefore("derpy_stampede_entry_before")
            .Owns(DerpyTrackId)
            .Do(ctx => setDerpyPosition(ctx, _startPosition));

        timeline.DuringApproach("derpy_stampede_entry")
            .Owns(DerpyTrackId)
            .Do((ctx, phase) => setDerpyPosition(ctx, Vector2.Lerp(_startPosition, _targetPosition, easeOutCubic(phase.LocalProgress))));

        timeline.AfterHit("derpy_stampede_entry_after")
            .Owns(DerpyTrackId)
            .Do((ctx, phase) => setDerpyPosition(ctx, _targetPosition));

        timeline.StableAfter("derpy_stampede_entry_stable_after")
            .Owns(DerpyTrackId)
            .Do(ctx => setDerpyPosition(ctx, _targetPosition));
    }

    private static void setDerpyPosition(VisualContext context, Vector2 position)
    {
        context.Mutate<GameObject>(DerpyTrackId, derpy => derpy.Position = position);
    }

    private static Vector2 getBottomPosition(Vector2 targetPosition)
    {
        Viewport viewport = GLOBALS.graphicsDevice.Viewport;
        return new Vector2(targetPosition.X, viewport.Height + 160f);
    }

    private static float easeOutCubic(float value)
    {
        value = MathHelper.Clamp(value, 0f, 1f);
        float inverse = 1f - value;
        return 1f - inverse * inverse * inverse;
    }
}
