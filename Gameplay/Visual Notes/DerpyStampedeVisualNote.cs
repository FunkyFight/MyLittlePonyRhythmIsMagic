using System;
using System.Collections.Generic;
using GameCore.GameObjects;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MLP_RiM.Elements.Editor;
using Rhythm.Note;
using TexturePackerMonoGameDefinitions;

public class DerpyStampedeVisualNote : DirectedVisualNote
{
    private const float BaseScale = 1f;
    private const float ParcelScaleMultiplier = 1.18f;
    private const float HitBounceScaleAmount = 0.16f;
    private const double HitBounceDuration = 0.16;
    private const float HitLineYRatio = 0.8519f;
    private const float ParcelHitLineYRatio = 0.8038f;
    private const float OffscreenPadding = 96f;

    private readonly DerpyStampedeAction _action;
    private readonly Note[] _hitNotes;
    private readonly GameObject _gameObject;
    private readonly double _departDuration;
    private string _currentTextureId;

    public DerpyStampedeVisualNote(Note note, VisualRuntime runtime, DerpyStampedeAction action, IReadOnlyList<Note> hitNotes, double approachDuration, double despawnDelay = 0) : base(note, runtime, approachDuration, despawnDelay)
    {
        _action = action;
        _hitNotes = createHitNotes(note, hitNotes);
        _departDuration = getDepartDuration(note, _hitNotes, despawnDelay);
        _gameObject = new GameObject(null)
        {
            Scale = Vector2.One * getBaseScale()
        };

        setTexture(getIdleTextureId());
        applyApproach(0f, note.SongPosition - approachDuration);
    }

    protected override void Build(VisualTimeline timeline)
    {
        timeline.StableBefore("derpy_stampede_before")
            .Do(ctx =>
            {
                refreshTexture();
                applyApproach(0f, ctx.SongPosition);
            });

        timeline.DuringApproach("derpy_stampede_approach")
            .Do((ctx, phase) =>
            {
                refreshTexture();
                applyApproach(phase.LocalProgress, ctx.SongPosition);
            });

        timeline.AfterHitUntilDespawn("derpy_stampede_depart")
            .Do((ctx, phase) =>
            {
                refreshTexture();
                applyDepart(getDepartProgress(ctx.SongPosition), ctx.SongPosition);
            });

        timeline.StableAfter("derpy_stampede_after")
            .Do(ctx =>
            {
                refreshTexture();
                applyDepart(1f, ctx.SongPosition);
            });
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        if(!ShouldDraw)
            return;

        _gameObject.Draw(spriteBatch);
    }

    private void refreshTexture()
    {
        setTexture(getCurrentTextureId());
    }

    private string getCurrentTextureId()
    {
        int hitCount = getSuccessfulHitCount();
        if(hitCount <= 0)
            return getIdleTextureId();

        if(_action == DerpyStampedeAction.Stamp)
            return MainAtlas.Enveloppe2;

        return hitCount switch
        {
            1 => MainAtlas.Parcel2,
            2 => MainAtlas.Parcel3,
            _ => MainAtlas.Parcel4
        };
    }

    private int getSuccessfulHitCount()
    {
        int hitCount = 0;
        foreach(Note hitNote in _hitNotes)
        {
            if(hitNote is { HasReacted: true, HasBeenMissed: false })
                hitCount++;
        }

        return hitCount;
    }

    private string getIdleTextureId()
    {
        return _action == DerpyStampedeAction.Stamp
            ? MainAtlas.Enveloppe1
            : MainAtlas.Parcel1;
    }

    private void setTexture(string textureId)
    {
        if(_currentTextureId == textureId)
            return;

        _gameObject.sprite = GLOBALS.main_atlas.CreateSprite(textureId);
        _gameObject.sprite.CenterOrigin();
        _currentTextureId = textureId;
    }

    private void applyApproach(float progress, double songPosition)
    {
        float eased = easeOutCubic(progress);
        _gameObject.Position = Vector2.Lerp(getLeftPosition(), getCenterPosition(), eased);
        _gameObject.Rotation = MathHelper.Lerp(MathHelper.ToRadians(-7f), 0f, eased);
        _gameObject.Scale = Vector2.One * (getBaseScale() * getArrivalScale(eased) * getHitBounceScale(songPosition));
    }

    private void applyDepart(float progress, double songPosition)
    {
        float eased = easeInCubic(progress);
        _gameObject.Position = Vector2.Lerp(getCenterPosition(), getRightPosition(), eased);
        _gameObject.Rotation = MathHelper.Lerp(0f, MathHelper.ToRadians(7f), eased);
        _gameObject.Scale = Vector2.One * (getBaseScale() * getHitBounceScale(songPosition));
    }

    private float getBaseScale()
    {
        return _action == DerpyStampedeAction.Triple_Stamp
            ? BaseScale * ParcelScaleMultiplier
            : BaseScale;
    }

    private float getDepartProgress(double songPosition)
    {
        double departStart = getDepartStartSongPosition();
        if(songPosition <= departStart || _departDuration <= 0)
            return 0f;

        return (float)((songPosition - departStart) / _departDuration);
    }

    private float getHitBounceScale(double songPosition)
    {
        float bounce = 0f;
        foreach(Note hitNote in _hitNotes)
        {
            if(hitNote is not { HasReacted: true, HasBeenMissed: false })
                continue;

            double elapsed = songPosition - hitNote.SongPosition;
            if(elapsed < 0 || elapsed > HitBounceDuration)
                continue;

            float progress = (float)(elapsed / HitBounceDuration);
            bounce = MathF.Max(bounce, MathF.Sin(progress * MathF.PI));
        }

        return 1f + bounce * HitBounceScaleAmount;
    }

    private double getDepartStartSongPosition()
    {
        return _hitNotes.Length > 0
            ? _hitNotes[_hitNotes.Length - 1].SongPosition
            : Note.SongPosition;
    }

    private Vector2 getLeftPosition()
    {
        Viewport viewport = GLOBALS.graphicsDevice.Viewport;
        return new Vector2(-getHorizontalMargin(), viewport.Height * getHitLineYRatio());
    }

    private Vector2 getCenterPosition()
    {
        Viewport viewport = GLOBALS.graphicsDevice.Viewport;
        return new Vector2(viewport.Width * 0.5f, viewport.Height * getHitLineYRatio());
    }

    private Vector2 getRightPosition()
    {
        Viewport viewport = GLOBALS.graphicsDevice.Viewport;
        return new Vector2(viewport.Width + getHorizontalMargin(), viewport.Height * getHitLineYRatio());
    }

    private float getHitLineYRatio()
    {
        return _action == DerpyStampedeAction.Triple_Stamp
            ? ParcelHitLineYRatio
            : HitLineYRatio;
    }

    private float getHorizontalMargin()
    {
        return MathF.Max(_gameObject.Width * 0.5f, OffscreenPadding);
    }

    private static float getArrivalScale(float progress)
    {
        return 1f + MathF.Sin(progress * MathF.PI) * 0.06f;
    }

    private static float easeOutCubic(float value)
    {
        value = MathHelper.Clamp(value, 0f, 1f);
        float inverse = 1f - value;
        return 1f - inverse * inverse * inverse;
    }

    private static float easeInCubic(float value)
    {
        value = MathHelper.Clamp(value, 0f, 1f);
        return value * value * value;
    }

    private static Note[] createHitNotes(Note note, IReadOnlyList<Note> hitNotes)
    {
        if(hitNotes == null || hitNotes.Count == 0)
            return new[] { note };

        Note[] notes = new Note[hitNotes.Count];
        for(int i = 0; i < hitNotes.Count; i++)
            notes[i] = hitNotes[i];

        return notes;
    }

    private static double getDepartDuration(Note note, Note[] hitNotes, double despawnDelay)
    {
        double departStart = hitNotes is { Length: > 0 }
            ? hitNotes[hitNotes.Length - 1].SongPosition
            : note.SongPosition;

        double despawnEnd = note.SongPosition + Math.Max(0.0, despawnDelay);
        return Math.Max(0.0, despawnEnd - departStart);
    }
}
