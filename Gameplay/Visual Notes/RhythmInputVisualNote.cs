using System;
using GameCore.GameObjects;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rhythm.Note;
using Rhythm.Note.Visual;
using TexturePackerMonoGameDefinitions;

public class RhythmInputVisualNote : VisualNote
{
    private const double ApproachSeconds = 2.0;
    private const double DespawnSeconds = 0.6;
    private const float BaseIconScale = 0.10f;
    private const double SuccessFeedbackSeconds = 0.22;
    private const double MissFeedbackSeconds = 0.36;

    private readonly Vector2 _reactionOrigin;
    private readonly Texture2D _pixel;
    private readonly GameObject _inputIndicatorGameObject;
    private bool _wasReacted;
    private double _reactionFeedbackStartSongPosition = double.NaN;

    public RhythmInputVisualNote(Note logicalNote, Vector2 reactionOrigin, Texture2D pixel) : base(logicalNote, ApproachSeconds, DespawnSeconds)
    {
        _reactionOrigin = reactionOrigin;
        _pixel = pixel;
        _inputIndicatorGameObject = new GameObject(new GameCore.Graphics.Sprite(GLOBALS.controller_atlas.GetRegion(XboxControllerAtlasDefinitions.Digital_Buttons_ABXY_button_xbox_digital_a_2)));
        _inputIndicatorGameObject.sprite?.CenterOrigin();
        _inputIndicatorGameObject.Scale = new Vector2(BaseIconScale, BaseIconScale);
    }

    public override void Update(double currentSongPosition)
    {
        base.Update(currentSongPosition);

        if (Note.HasReacted && !_wasReacted)
            _reactionFeedbackStartSongPosition = currentSongPosition;

        if (!Note.HasReacted)
            _reactionFeedbackStartSongPosition = double.NaN;

        _wasReacted = Note.HasReacted;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!ShouldDraw)
            return;

        bool successfulHit = Note.HasReacted && !Note.HasBeenMissed;
        double feedbackProgress = GetFeedbackProgress(successfulHit ? SuccessFeedbackSeconds : MissFeedbackSeconds);
        if (successfulHit && feedbackProgress >= 1.0)
            return;

        if (Note.HasBeenMissed && feedbackProgress >= 1.0)
            return;

        float hitX = _reactionOrigin.X;
        float startX = hitX + 250f;
        float exitX = hitX - 96f;
        float x = UnclampedProgress <= 1.0
            ? MathHelper.Lerp(startX, hitX, (float)Math.Clamp(Progress, 0.0, 1.0))
            : MathHelper.Lerp(hitX, exitX, MathHelper.Clamp((float)PostHitProgress, 0f, 1f));
        Vector2 center = new(x, _reactionOrigin.Y);
        float scale = BaseIconScale;

        if (successfulHit)
        {
            DrawSuccessFeedback(spriteBatch, center, (float)feedbackProgress);
            scale *= 1f + 0.25f * (1f - (float)feedbackProgress);
        }
        else if (Note.HasBeenMissed)
        {
            float missProgress = (float)feedbackProgress;
            float shake = 5f * (1f - missProgress);
            center += new Vector2(
                MathF.Sin(missProgress * MathHelper.TwoPi * 7f) * shake,
                MathF.Cos(missProgress * MathHelper.TwoPi * 5f) * shake * 0.45f);
            scale *= MathF.Max(0f, 1f - missProgress);
        }

        DrawInputIcon(spriteBatch, center, scale);
    }

    public static VisualNoteManager<VisualNote> SetupRhythmInputVisualNoteVisualNoteManager(ChartPlayer chartPlayer, Texture2D pixel)
    {
        VisualNoteManager<VisualNote> visualNoteManager = new VisualNoteManager<VisualNote>(chartPlayer, note => 
            new RhythmInputVisualNote(
                note, 
                RhythmInputVisualElement.GetReactionOrigin(GLOBALS.graphicsDevice.Viewport),
                pixel
            )
        );

        visualNoteManager.LookAheadSeconds = 2.15;
        visualNoteManager.LookBehindSeconds = 0.8;

        return visualNoteManager;
    }

    private double GetFeedbackProgress(double durationSeconds)
    {
        if (!Note.HasReacted || double.IsNaN(_reactionFeedbackStartSongPosition) || durationSeconds <= 0.0)
            return 0.0;

        double currentSongPosition = Note.SongPosition - SecondsUntilHit;
        return Math.Clamp((currentSongPosition - _reactionFeedbackStartSongPosition) / durationSeconds, 0.0, 1.0);
    }

    private void DrawSuccessFeedback(SpriteBatch spriteBatch, Vector2 center, float progress)
    {
        float alpha = 1f - progress;
        int offset = 8 + (int)MathF.Round(progress * 12f);
        int size = 3;
        Color color = Color.White * (0.75f * alpha);

        spriteBatch.Draw(_pixel, new Rectangle((int)center.X - size / 2, (int)center.Y - offset, size, size), color);
        spriteBatch.Draw(_pixel, new Rectangle((int)center.X - size / 2, (int)center.Y + offset - size, size, size), color);
        spriteBatch.Draw(_pixel, new Rectangle((int)center.X - offset, (int)center.Y - size / 2, size, size), color);
        spriteBatch.Draw(_pixel, new Rectangle((int)center.X + offset - size, (int)center.Y - size / 2, size, size), color);
    }

    private void DrawInputIcon(SpriteBatch spriteBatch, Vector2 center, float scale)
    {
        if (scale <= 0.001f)
            return;

        _inputIndicatorGameObject.Position = center;
        _inputIndicatorGameObject.Scale = new Vector2(scale, scale);
        _inputIndicatorGameObject.Draw(spriteBatch);
    }
}
