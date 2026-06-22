using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using GameCore.Audio;
using GameCore.GameObjects;
using GameCore.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MLP_RiM.Elements.Editor;
using Rhythm.Note;
using TexturePackerMonoGameDefinitions;

public sealed class GemwalkGlamourOreVisualNote : DirectedVisualNote
{
    private const string OneSapphirePreCueSfx = "SFX/GemwalkGlammour/pre1.wav";
    private const string ThreeSapphiresCueSfx = "SFX/GemwalkGlammour/3.wav";
    private const string RubyCueSfx = "SFX/GemwalkGlammour/Ruby.wav";
    private const string RubyEnd1CueSfx = "SFX/GemwalkGlammour/Ruby_End1.wav";
    private const string RubyEnd2CueSfx = "SFX/GemwalkGlammour/Ruby_End2.wav";
    private const float CueSfxVolume = 2.5f;
    private const float RubyEndCueSfxVolume = 5f;
    private const float PropScrollPixelsPerBeat = 72f;
    private const float ApproachSpriteStartScaleMultiplier = 2.4f;
    private const float ApproachSpriteAlpha = 0.65f;
    private const double SapphirePickaxeStrikeRushBeats = 0.42;
    private const double OreHitBounceBeats = 0.45;
    private const float OreHitBounceScale = 0.26f;
    private const float OreHitBounceHeight = 34f;
    private const float BagLandingBounceAmplitude = 34f;
    private const float BagLandingScalePulse = 0.16f;
    private const double FailBounceBeats = 0.75;
    private const float FailBounceHeight = 34f;
    private static readonly Vector2 PickaxeStrikeOffset = new(-24f, 58f);
    private static readonly Vector2 SapphirePickaxeStrikeOffset = new(-52f, 96f);
    private static readonly Vector2 SapphirePickaxeWindupOffset = new(-94f, -78f);
    private static readonly Vector2 RubyPickaxeWindupOffset = new(-36f, -28f);
    private const int OreShardCount = 18;
    private const double OreShardLifetimeBeats = 1.05;
    private const float OreShardGravityPixelsPerBeat = 290f;
    private static readonly Dictionary<string, TextureRegion> WhiteApproachRegions = new();

    private readonly GemwalkGlamour _scene;
    private readonly GemwalkGlamourAction _action;
    private readonly GameObject _approachOre;
    private readonly GameObject _ore;
    private readonly string _approachOreTrackId;
    private readonly string _oreTrackId;
    private readonly string _pickaxeTrackId;
    private readonly string _bagTrackId;
    private readonly Vector2 _spawnPosition;
    private readonly Vector2 _baseApexPosition;
    private Vector2 _apexPosition;
    private readonly float _scale;
    private readonly double _crotchet;
    private readonly double _pickaxeStrikeRushBeats;
    private readonly double _pickaxeStrikeHoldBeats;
    private readonly double _pickaxeStrikeReturnBeats;
    private readonly TextureRegion _oreShardSourceRegion;
    private string _currentApproachOreSprite;
    private string _currentOreSprite;
    private OreShard[] _oreShards = Array.Empty<OreShard>();
    private Vector2 _oreShardOrigin;
    private double _activeOreShardImpactSongPosition = double.NaN;
    private double _lastSongPosition = double.NaN;
    private bool _hasPickaxeStrikeStart;
    private bool _rerolledForCurrentRewind;
    private bool _rubyReleaseEvaluated;
    private bool _rubyReleaseSuccessful;
    private bool _rubyReleaseFailed;
    private double _rubyReleaseSongPosition = double.NaN;
    private uint _observedReactMainReleaseSerial;
    private Vector2 _pickaxeStrikeStartPosition;
    private float _pickaxeStrikeStartRotation;

    public GemwalkGlamourOreVisualNote(
        GemwalkGlamour scene,
        Note note,
        VisualRuntime runtime,
        GemwalkGlamourAction action,
        Vector2 spawnPosition,
        Vector2 apexPosition,
        string pickaxeTrackId,
        string bagTrackId,
        float scale,
        double crotchet,
        double pickaxeStrikeRushBeats,
        double pickaxeStrikeHoldBeats,
        double pickaxeStrikeReturnBeats)
        : base(note, runtime, GemwalkGlamour.CueLeadBeats * Math.Max(0.001, crotchet), GetDespawnDelay(scene, note, scale, Math.Max(0.001, crotchet)))
    {
        _scene = scene;
        _action = action;
        _spawnPosition = spawnPosition;
        _baseApexPosition = apexPosition;
        _apexPosition = GetRandomApexPosition(_baseApexPosition);
        _pickaxeTrackId = pickaxeTrackId;
        _bagTrackId = bagTrackId;
        _scale = scale;
        _crotchet = Math.Max(0.001, crotchet);
        _pickaxeStrikeRushBeats = Math.Max(0.001, pickaxeStrikeRushBeats);
        _pickaxeStrikeHoldBeats = Math.Max(_pickaxeStrikeRushBeats, pickaxeStrikeHoldBeats);
        _pickaxeStrikeReturnBeats = Math.Max(0.001, pickaxeStrikeReturnBeats);
        _observedReactMainReleaseSerial = GLOBALS.ReactMainReleaseSerial;

        _approachOre = new GameObject(null)
        {
            Position = _spawnPosition,
            Scale = Vector2.One * (_scale * ApproachSpriteStartScaleMultiplier)
        };
        SetApproachOreSprite();
        SetVisible(_approachOre, false, 0f);

        _ore = new GameObject(null)
        {
            Position = _spawnPosition,
            Scale = Vector2.One * _scale
        };
        SetOreSprite(mined: false);
        SetVisible(_ore, false, 0f);
        _oreShardSourceRegion = GLOBALS.main_atlas.GetRegion(GetOreSprite(mined: false));

        int instanceId = RuntimeHelpers.GetHashCode(this);
        _approachOreTrackId = $"gemwalk_glamour.approach_ore.{instanceId}";
        _oreTrackId = $"gemwalk_glamour.ore.{instanceId}";
        runtime.RegisterTrack(_approachOreTrackId, _approachOre);
        runtime.SetDriver(_approachOreTrackId, note);
        runtime.RegisterTrack(_oreTrackId, _ore);
        runtime.SetDriver(_oreTrackId, note);
    }

    private static double GetDespawnDelay(GemwalkGlamour scene, Note note, float scale, double crotchet)
    {
        double defaultDelayBeats = GemwalkGlamour.DespawnBeats;
        double failedGroundDelayBeats = GetFailedGroundDespawnDelayBeats(scene, note, scale);
        return Math.Max(defaultDelayBeats, failedGroundDelayBeats) * crotchet;
    }

    private static double GetFailedGroundDespawnDelayBeats(GemwalkGlamour scene, Note note, float scale)
    {
        double landingDelayBeats = note?.HoldDuration > 0.0 ? 0.0 : GemwalkGlamour.NonHoldOreLandingBeats;
        float landingCenterX = scene?.GetFailedGemGroundTarget().X ?? 0f;
        float oreHalfWidth = GetOreHalfWidth(scale) * 1.25f;
        double offscreenBeats = Math.Max(0.0, landingCenterX + oreHalfWidth) / PropScrollPixelsPerBeat;
        return landingDelayBeats + offscreenBeats;
    }

    private static float GetOreHalfWidth(float scale)
    {
        TextureRegion region = GLOBALS.main_atlas?.GetRegion(MainAtlas.Gemwalk_glammour_ore_sapphire1);
        return (region?.DrawWidth ?? 64) * scale * 0.5f;
    }

    protected override void Build(VisualTimeline timeline)
    {
        timeline.StableBefore("gemwalk_ore_before")
            .Owns(_approachOreTrackId, _oreTrackId)
            .Do(ctx =>
            {
                PlayCueSfx(ctx);
                HideOre(ctx);
            });

        timeline.DuringApproach("gemwalk_ore_approach")
            .Owns(_approachOreTrackId, _oreTrackId)
            .Do((ctx, phase) =>
            {
                PlayCueSfx(ctx);
                ApplyApproach(ctx, phase.LocalProgress);
            });

        timeline.DuringHold("gemwalk_ore_hold")
            .Owns(_approachOreTrackId, _oreTrackId, _pickaxeTrackId)
            .Do((ctx, phase) =>
            {
                PlayCueSfx(ctx);
                ApplyHold(ctx, phase.LocalProgress);
            });

        timeline.AfterHitUntilDespawn("gemwalk_ore_after_hit")
            .Owns(_approachOreTrackId, _oreTrackId, _pickaxeTrackId, _bagTrackId)
            .Do((ctx, phase) =>
            {
                PlayCueSfx(ctx);
                ApplyAfterHit(ctx, phase.LocalProgress);
            });

        timeline.StableAfter("gemwalk_ore_after")
            .Owns(_approachOreTrackId, _oreTrackId)
            .Do(HideOre);
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        if(ShouldDraw)
        {
            _approachOre.Draw(spriteBatch);
            _ore.Draw(spriteBatch);
            DrawOreShards(spriteBatch);
        }
    }

    private void HideOre(VisualContext context)
    {
        context.Mutate<GameObject>(_approachOreTrackId, ore => SetVisible(ore, false, 0f));
        context.Mutate<GameObject>(_oreTrackId, ore => SetVisible(ore, false, 0f));
    }

    private void PlayCueSfx(VisualContext context)
    {
        RerollApexOnRewind(context);

        if(_action == GemwalkGlamourAction.Ruby)
        {
            PlayRubyCueSfx(context);
            return;
        }

        if(!IsThreeSapphiresNote())
        {
            PlaySfx(context, "one_sapphire_cue_2", Note.SongPosition - GemwalkGlamour.CueLeadBeats * _crotchet, OneSapphirePreCueSfx);
            return;
        }

        if(IsFirstThreeSapphiresHit())
            PlaySfx(context, "three_sapphires_cue_2", Note.SongPosition - GemwalkGlamour.CueLeadBeats * _crotchet, ThreeSapphiresCueSfx);
    }

    private void PlayRubyCueSfx(VisualContext context)
    {
        PlaySfx(context, "ruby_initial_cue", Note.SongPosition - GemwalkGlamour.CueLeadBeats * _crotchet, RubyCueSfx);

        double spacing = GemwalkGlamourProvider.GetBeatSpacing(Note.AdditionnalData) * _crotchet;
        int cueCount = GemwalkGlamourProvider.GetRubyCueCount(Note.AdditionnalData);
        for(int i = 0; i < cueCount; i++)
        {
            string path = GetRubyHoldCueSfx(i, cueCount);
            PlaySfx(context, $"ruby_hold_cue_{i}", Note.SongPosition + i * spacing, path);
        }
        PlaySfx(context, "ruby_release_cue", Note.EndSongPosition, RubyEnd2CueSfx);
    }

    private void PlaySfx(VisualContext context, string eventId, double songPosition, string path)
    {
        float volume = path is RubyEnd1CueSfx or RubyEnd2CueSfx ? RubyEndCueSfxVolume : CueSfxVolume;
        context.PlaySfxOnForwardCross($"gemwalk_glamour.{RuntimeHelpers.GetHashCode(this)}.{eventId}", songPosition, _scene, path, volume);
    }

    private void ApplyApproach(VisualContext context, float progress)
    {
        float fullProgress = MathHelper.Clamp(progress, 0f, 1f) * 0.5f;
        Vector2 position = GetTravelPosition(context, fullProgress, failed: false);
        context.Mutate<GameObject>(_approachOreTrackId, approachOre =>
        {
            SetApproachOreSprite();
            approachOre.Position = position;
            approachOre.Rotation = GetOreRotation(fullProgress);
            float approachScale = _scale * MathHelper.Lerp(ApproachSpriteStartScaleMultiplier, 1f, MathHelper.Clamp(progress, 0f, 1f));
            approachOre.Scale = Vector2.One * approachScale;
            SetVisible(approachOre, true, ApproachSpriteAlpha);
        });

        context.Mutate<GameObject>(_oreTrackId, ore =>
        {
            SetOreSprite(mined: false);
            ore.Position = position;
            ore.Rotation = GetOreRotation(fullProgress);
            ore.Scale = Vector2.One * _scale;
            SetVisible(ore, true, 1f);
        });
    }

    private void ApplyHold(VisualContext context, float progress)
    {
        UpdateRubyReleaseState(context);
        UpdateOreShardState(context);
        context.Mutate<GameObject>(_approachOreTrackId, ore => SetVisible(ore, false, 0f));

        float fullProgress = 0.5f + MathHelper.Clamp(progress, 0f, 1f) * 0.5f;
        bool mined = IsMined();
        bool failed = IsFailed();
        Vector2 position = GetTravelPosition(context, fullProgress, failed);
        position += GetOreHitBounceOffset(context);

        context.Mutate<GameObject>(_oreTrackId, ore =>
        {
            SetOreSprite(mined);
            ore.Position = position;
            ore.Rotation = GetOreRotation(fullProgress);
            ore.Scale = Vector2.One * (_scale * GetBreakBounceScale(context));
            SetVisible(ore, true, 1f);
        });

        if (IsRubyHoldFollowActive(context))
            ApplyRubyHoldPickaxe(context, position);
        else if (_action != GemwalkGlamourAction.Ruby)
            ApplyPickaxeStrike(context, mined);
    }

    private void ApplyAfterHit(VisualContext context, float progress)
    {
        UpdateRubyReleaseState(context);
        UpdateOreShardState(context);
        context.Mutate<GameObject>(_approachOreTrackId, ore => SetVisible(ore, false, 0f));

        float landingProgress = GetLandingProgress(context);
        float fullProgress = Note.HoldDuration > 0.0
            ? 1f
            : 0.5f + landingProgress * 0.5f;
        bool mined = IsMined();
        bool failed = IsFailed();
        Vector2 position = failed && HasLanded(context)
            ? GetFailedLandingPosition()
            : GetTravelPosition(context, fullProgress, failed);
        position += GetOreHitBounceOffset(context);
        position += GetGroundedPropOffset(context, failed);
        float failBounceProgress = GetFailBounceProgress(context, failed);
        bool showOre = failed || !HasLanded(context) || IsRubyPendingRelease(context) || IsRubyReleaseSuccessVisible(context);

        context.Mutate<GameObject>(_oreTrackId, ore =>
        {
            SetOreSprite(mined);
            ore.Position = position;
            ore.Rotation = GetOreRotation(fullProgress) + GetFailBounceRotation(failBounceProgress);
            ore.Scale = Vector2.One * (_scale * GetOreScale(context, mined, failBounceProgress));
            SetVisible(ore, showOre, 1f);
        });

        if (IsRubyHoldFollowActive(context))
            ApplyRubyHoldPickaxe(context, position);
        else if (_action != GemwalkGlamourAction.Ruby)
            ApplyPickaxeStrike(context, mined);
        ApplyBagLandingBounce(context, mined);
    }

    private bool IsMined()
    {
        return _action == GemwalkGlamourAction.Ruby
            ? _rubyReleaseSuccessful
            : GemwalkGlamour.IsSuccessful(Note);
    }

    private bool IsFailed()
    {
        return _action == GemwalkGlamourAction.Ruby
            ? GemwalkGlamour.IsMissed(Note) || _rubyReleaseFailed
            : GemwalkGlamour.IsMissed(Note);
    }

    private void UpdateRubyReleaseState(VisualContext context)
    {
        if (_action != GemwalkGlamourAction.Ruby)
            return;

        if (context.HasRewound && context.SongPosition <= Note.SongPosition)
            ResetRubyReleaseState();

        if (GLOBALS.ReactMainReleaseSerial != _observedReactMainReleaseSerial)
        {
            _observedReactMainReleaseSerial = GLOBALS.ReactMainReleaseSerial;
            double releaseSongPosition = GLOBALS.ReactMainReleaseSongPosition;
            if (!_rubyReleaseEvaluated
                && Note.HasReacted
                && !Note.HasBeenMissed
                && !double.IsNaN(releaseSongPosition)
                && releaseSongPosition >= Note.SongPosition - GemwalkGlamour.RubyReleaseWindowSeconds)
            {
                MarkRubyRelease(releaseSongPosition, IsRubyReleaseOnTime(releaseSongPosition));
            }
        }

        if (_rubyReleaseEvaluated)
            return;

        if (Note.HasBeenMissed)
        {
            MarkRubyRelease(context.SongPosition, success: false);
            return;
        }

        if (!Note.HasReacted)
            return;

        if (IsRubyAutoplayHold(context.SongPosition) && context.SongPosition >= Note.EndSongPosition)
        {
            MarkRubyRelease(Note.EndSongPosition, success: true);
            return;
        }

        if (context.SongPosition > Note.EndSongPosition + GemwalkGlamour.RubyReleaseWindowSeconds)
            MarkRubyRelease(context.SongPosition, success: false);
    }

    private void ResetRubyReleaseState()
    {
        _rubyReleaseEvaluated = false;
        _rubyReleaseSuccessful = false;
        _rubyReleaseFailed = false;
        _rubyReleaseSongPosition = double.NaN;
        _observedReactMainReleaseSerial = GLOBALS.ReactMainReleaseSerial;
    }

    private void MarkRubyRelease(double releaseSongPosition, bool success)
    {
        _rubyReleaseEvaluated = true;
        _rubyReleaseSuccessful = success;
        _rubyReleaseFailed = !success;
        _rubyReleaseSongPosition = releaseSongPosition;
    }

    private bool IsRubyReleaseOnTime(double releaseSongPosition)
    {
        return Math.Abs(releaseSongPosition - Note.EndSongPosition) <= GemwalkGlamour.RubyReleaseWindowSeconds;
    }

    private bool IsRubyHoldFollowActive(VisualContext context)
    {
        if (_action != GemwalkGlamourAction.Ruby
            || _rubyReleaseEvaluated
            || !Note.HasReacted
            || Note.HasBeenMissed)
            return false;

        if (IsRubyAutoplayHold(context.SongPosition))
            return true;

        return GLOBALS.ReactMainIsPressed
            && context.SongPosition <= Note.EndSongPosition + GemwalkGlamour.RubyReleaseWindowSeconds;
    }

    private bool IsRubyPendingRelease(VisualContext context)
    {
        return _action == GemwalkGlamourAction.Ruby
            && Note.HasReacted
            && !Note.HasBeenMissed
            && !_rubyReleaseEvaluated
            && context.SongPosition <= Note.EndSongPosition + GemwalkGlamour.RubyReleaseWindowSeconds;
    }

    private bool IsRubyReleaseSuccessVisible(VisualContext context)
    {
        return _action == GemwalkGlamourAction.Ruby
            && _rubyReleaseSuccessful
            && !double.IsNaN(_rubyReleaseSongPosition)
            && context.SongPosition <= _rubyReleaseSongPosition + GemwalkGlamour.DespawnBeats * _crotchet;
    }

    private static bool IsRubyAutoplayHold(double songPosition)
    {
        return GLOBALS.beatmapEditorElement?.IsPreviewAutoplayEnabled == true;
    }

    private void ApplyRubyHoldPickaxe(VisualContext context, Vector2 rubyPosition)
    {
        double elapsedBeats = (context.SongPosition - Note.SongPosition) / _crotchet;
        if (elapsedBeats < 0.0)
            return;

        double spacingBeats = Math.Max(0.001, GemwalkGlamourProvider.GetBeatSpacing(Note.AdditionnalData));
        Vector2 impactPosition = rubyPosition + PickaxeStrikeOffset + GetPickaxeStrikeFloat(context);
        Vector2 windupPosition = impactPosition + RubyPickaxeWindupOffset;

        context.Mutate<GameObject>(_pickaxeTrackId, pickaxe =>
        {
            if (!_hasPickaxeStrikeStart || context.HasRewound)
            {
                _pickaxeStrikeStartPosition = pickaxe.Position;
                _pickaxeStrikeStartRotation = pickaxe.Rotation;
                _hasPickaxeStrikeStart = true;
            }

            GetRubyPickaxePose(elapsedBeats, spacingBeats, windupPosition, impactPosition, out Vector2 position, out float rotation);
            pickaxe.Position = position;
            pickaxe.Rotation = rotation;
            pickaxe.Scale = Vector2.One * GemwalkGlamour.PickaxeScale;
        });
    }

    private void GetRubyPickaxePose(double elapsedBeats, double spacingBeats, Vector2 windupPosition, Vector2 impactPosition, out Vector2 position, out float rotation)
    {
        const float WindupRotation = -24f;
        const float ImpactRotation = 66f;
        double firstRushBeats = GetRubyFirstStrikeRushBeats();
        float windupRotation = MathHelper.ToRadians(WindupRotation);
        float impactRotation = MathHelper.ToRadians(ImpactRotation);

        if(elapsedBeats < firstRushBeats)
        {
            float progress = MathHelper.Clamp((float)(elapsedBeats / firstRushBeats), 0f, 1f);
            if(progress < 0.45f)
            {
                float windupProgress = EaseOutCubic(progress / 0.45f);
                position = Vector2.Lerp(_pickaxeStrikeStartPosition, windupPosition, windupProgress);
                rotation = MathHelper.Lerp(_pickaxeStrikeStartRotation, windupRotation, windupProgress);
                return;
            }

            float impactProgress = EaseOutCubic((progress - 0.45f) / 0.55f);
            position = Vector2.Lerp(windupPosition, impactPosition, impactProgress);
            rotation = MathHelper.Lerp(windupRotation, impactRotation, impactProgress);
            return;
        }

        float impact = GetRubyRepeatedStrikeImpact(elapsedBeats - firstRushBeats, spacingBeats);
        position = impactPosition + RubyPickaxeWindupOffset * (1f - impact);
        rotation = MathHelper.Lerp(windupRotation, impactRotation, impact);
    }

    private double GetRubyFirstStrikeRushBeats()
    {
        return Math.Max(_pickaxeStrikeRushBeats, SapphirePickaxeStrikeRushBeats);
    }

    private float GetRubyRepeatedStrikeImpact(double elapsedAfterFirstImpactBeats, double spacingBeats)
    {
        double cycleBeats = elapsedAfterFirstImpactBeats % spacingBeats;
        if (cycleBeats < 0.0)
            cycleBeats += spacingBeats;

        double settleBeats = Math.Min(0.10, spacingBeats * 0.20);
        if (cycleBeats <= settleBeats)
            return 1f;

        double returnBeats = Math.Min(_pickaxeStrikeReturnBeats, spacingBeats * 0.32);
        if(cycleBeats <= settleBeats + returnBeats)
            return 1f - EaseOutCubic((float)((cycleBeats - settleBeats) / returnBeats));

        double windupLeadBeats = Math.Min(_pickaxeStrikeRushBeats, spacingBeats * 0.45);
        double timeToNextStrikeBeats = spacingBeats - cycleBeats;
        if (timeToNextStrikeBeats <= windupLeadBeats)
            return EaseOutCubic((float)((windupLeadBeats - timeToNextStrikeBeats) / windupLeadBeats));

        return 0f;
    }

    private void ApplyPickaxeStrike(VisualContext context, bool mined)
    {
        if(!mined)
            return;

        double elapsedBeats = (context.SongPosition - Note.SongPosition) / _crotchet;
        double rushBeats = Math.Max(_pickaxeStrikeRushBeats, SapphirePickaxeStrikeRushBeats);
        double holdBeats = Math.Max(_pickaxeStrikeHoldBeats, rushBeats + 0.18);
        double strikeEndBeats = holdBeats + _pickaxeStrikeReturnBeats;
        if(elapsedBeats < 0.0 || elapsedBeats > strikeEndBeats)
            return;

        Vector2 idlePosition = _scene.GetPickaxeIdlePosition(context.SongPosition);
        Vector2 impactPosition = GetTravelPositionAt(context, Note.SongPosition, 0.5f, failed: false)
            + SapphirePickaxeStrikeOffset
            + GetPickaxeStrikeFloat(context);
        Vector2 windupPosition = impactPosition + SapphirePickaxeWindupOffset;

        context.Mutate<GameObject>(_pickaxeTrackId, pickaxe =>
        {
            if (!_hasPickaxeStrikeStart || context.HasRewound)
            {
                _pickaxeStrikeStartPosition = pickaxe.Position;
                _pickaxeStrikeStartRotation = pickaxe.Rotation;
                _hasPickaxeStrikeStart = true;
            }

            float impactPulse = GetSapphireImpactPulse(elapsedBeats, rushBeats);
            pickaxe.Position = GetSapphirePickaxeStrikePosition(elapsedBeats, rushBeats, holdBeats, _pickaxeStrikeReturnBeats, _pickaxeStrikeStartPosition, windupPosition, impactPosition, idlePosition)
                + new Vector2(10f, -8f) * impactPulse;
            pickaxe.Rotation = GetSapphirePickaxeStrikeRotation(elapsedBeats, rushBeats, holdBeats, _pickaxeStrikeReturnBeats, _pickaxeStrikeStartRotation);
            pickaxe.Scale = Vector2.One * (GemwalkGlamour.PickaxeScale * (1f + impactPulse * 0.12f));
        });
    }

    private static Vector2 GetSapphirePickaxeStrikePosition(double elapsedBeats, double rushBeats, double holdBeats, double returnBeats, Vector2 startPosition, Vector2 windupPosition, Vector2 impactPosition, Vector2 idlePosition)
    {
        if(elapsedBeats < rushBeats)
        {
            float progress = MathHelper.Clamp((float)(elapsedBeats / rushBeats), 0f, 1f);
            if(progress < 0.45f)
                return Vector2.Lerp(startPosition, windupPosition, EaseOutCubic(progress / 0.45f));

            return Vector2.Lerp(windupPosition, impactPosition, EaseOutCubic((progress - 0.45f) / 0.55f));
        }

        if(elapsedBeats < holdBeats)
            return impactPosition;

        float returnProgress = (float)((elapsedBeats - holdBeats) / returnBeats);
        return Vector2.Lerp(impactPosition, idlePosition, EaseInOutCubic(returnProgress));
    }

    private static float GetSapphireImpactPulse(double elapsedBeats, double rushBeats)
    {
        double pulseBeats = 0.20;
        double elapsedAfterImpact = elapsedBeats - rushBeats;
        if(elapsedAfterImpact < 0.0 || elapsedAfterImpact > pulseBeats)
            return 0f;

        float progress = (float)(elapsedAfterImpact / pulseBeats);
        return MathF.Sin(progress * MathHelper.Pi);
    }

    private double GetMinedImpactSongPosition()
    {
        return _action == GemwalkGlamourAction.Ruby
            && _rubyReleaseSuccessful
            && !double.IsNaN(_rubyReleaseSongPosition)
            ? _rubyReleaseSongPosition
            : Note.SongPosition;
    }

    private void UpdateOreShardState(VisualContext context)
    {
        _lastSongPosition = context.SongPosition;

        if(context.HasRewound && context.SongPosition <= Note.SongPosition)
        {
            _activeOreShardImpactSongPosition = double.NaN;
            _oreShards = Array.Empty<OreShard>();
        }

        if(!ShouldEmitOreShards(context))
            return;

        double impactSongPosition = GetOreHitBounceSongPosition(context);
        double elapsedBeats = (context.SongPosition - impactSongPosition) / _crotchet;
        if(elapsedBeats < 0.0 || elapsedBeats > OreShardLifetimeBeats)
            return;

        if(!double.IsNaN(_activeOreShardImpactSongPosition)
            && Math.Abs(_activeOreShardImpactSongPosition - impactSongPosition) <= 0.000001
            && !context.HasRewound)
            return;

        _activeOreShardImpactSongPosition = impactSongPosition;
        _oreShardOrigin = GetOreShardOrigin(context, impactSongPosition);
        _oreShards = CreateOreShards(_oreShardSourceRegion, CreateOreShardSeed(impactSongPosition));
    }

    private bool ShouldEmitOreShards(VisualContext context)
    {
        return _action == GemwalkGlamourAction.Ruby
            ? _rubyReleaseSuccessful || IsRubyHoldFollowActive(context)
            : GemwalkGlamour.IsSuccessful(Note);
    }

    private Vector2 GetOreShardOrigin(VisualContext context, double impactSongPosition)
    {
        return GetTravelPositionAt(context, impactSongPosition, GetOreShardTravelProgress(impactSongPosition), failed: false);
    }

    private float GetOreShardTravelProgress(double impactSongPosition)
    {
        if(_action != GemwalkGlamourAction.Ruby || Note.HoldDuration <= 0.0)
            return 0.5f;

        double holdSongDuration = Math.Max(0.001, Note.EndSongPosition - Note.SongPosition);
        float holdProgress = MathHelper.Clamp((float)((impactSongPosition - Note.SongPosition) / holdSongDuration), 0f, 1f);
        return 0.5f + holdProgress * 0.5f;
    }

    private int CreateOreShardSeed(double impactSongPosition)
    {
        unchecked
        {
            int seed = RuntimeHelpers.GetHashCode(Note);
            seed = seed * 397 ^ (int)_action;
            seed = seed * 397 ^ (int)Math.Round(impactSongPosition * 1000.0);
            return seed;
        }
    }

    private void DrawOreShards(SpriteBatch spriteBatch)
    {
        if(_oreShardSourceRegion == null
            || _oreShards.Length == 0
            || double.IsNaN(_activeOreShardImpactSongPosition)
            || double.IsNaN(_lastSongPosition))
            return;

        double elapsedBeats = (_lastSongPosition - _activeOreShardImpactSongPosition) / _crotchet;
        if(elapsedBeats < 0.0 || elapsedBeats > OreShardLifetimeBeats)
            return;

        for(int i = 0; i < _oreShards.Length; i++)
        {
            OreShard shard = _oreShards[i];
            double localBeats = elapsedBeats - shard.DelayBeats;
            if(localBeats < 0.0 || localBeats > shard.LifetimeBeats)
                continue;

            float progress = MathHelper.Clamp((float)(localBeats / shard.LifetimeBeats), 0f, 1f);
            float local = (float)localBeats;
            Vector2 position = _oreShardOrigin
                + shard.Offset
                + shard.Velocity * local
                + new Vector2(0f, OreShardGravityPixelsPerBeat * local * local);
            float alpha = MathHelper.Clamp(MathF.Pow(1f - progress, 0.82f) * 1.18f, 0f, 1f);
            float scale = _scale * shard.Scale * MathHelper.Lerp(1.18f, 0.82f, progress);

            spriteBatch.Draw(
                _oreShardSourceRegion.Texture,
                position,
                shard.SourceRectangle,
                Color.White * alpha,
                shard.Rotation + shard.AngularVelocity * local,
                shard.Origin,
                scale,
                SpriteEffects.None,
                0f);
        }
    }

    private bool ShouldPlayOreHitBounce()
    {
        return GemwalkGlamour.IsSuccessful(Note) || _rubyReleaseSuccessful;
    }

    private float GetOreHitBounceProgress(VisualContext context)
    {
        if(!ShouldPlayOreHitBounce())
            return -1f;

        double elapsedBeats = (context.SongPosition - GetOreHitBounceSongPosition(context)) / _crotchet;
        if(elapsedBeats < 0.0 || elapsedBeats > OreHitBounceBeats)
            return -1f;

        return MathHelper.Clamp((float)(elapsedBeats / OreHitBounceBeats), 0f, 1f);
    }

    private double GetOreHitBounceSongPosition(VisualContext context)
    {
        if(_action == GemwalkGlamourAction.Ruby
            && !_rubyReleaseEvaluated
            && Note.HasReacted
            && !Note.HasBeenMissed
            && context.SongPosition >= Note.SongPosition
            && context.SongPosition <= Note.EndSongPosition + GemwalkGlamour.RubyReleaseWindowSeconds)
        {
            double spacingBeats = Math.Max(0.001, GemwalkGlamourProvider.GetBeatSpacing(Note.AdditionnalData));
            double firstImpactSongPosition = Note.SongPosition + GetRubyFirstStrikeRushBeats() * _crotchet;
            if(context.SongPosition < firstImpactSongPosition)
                return firstImpactSongPosition;

            double elapsedBeats = (context.SongPosition - firstImpactSongPosition) / _crotchet;
            double strikeIndex = Math.Floor(Math.Max(0.0, elapsedBeats) / spacingBeats);
            return firstImpactSongPosition + strikeIndex * spacingBeats * _crotchet;
        }

        return GetMinedImpactSongPosition();
    }

    private float GetBreakBounceScale(VisualContext context)
    {
        float progress = GetOreHitBounceProgress(context);
        if(progress < 0f)
            return 1f;

        return 1f + MathF.Sin(progress * MathHelper.Pi) * OreHitBounceScale;
    }

    private Vector2 GetOreHitBounceOffset(VisualContext context)
    {
        float progress = GetOreHitBounceProgress(context);
        if(progress < 0f)
            return Vector2.Zero;

        float bounce = MathF.Sin(progress * MathHelper.Pi);
        return new Vector2(0f, -bounce * OreHitBounceHeight);
    }

    private float GetOreScale(VisualContext context, bool mined, float failBounceProgress)
    {
        float scale = GetBreakBounceScale(context);
        if(failBounceProgress >= 0f)
            scale *= 1f + MathF.Sin(failBounceProgress * MathHelper.Pi * 2f) * (1f - failBounceProgress) * 0.12f;

        return scale;
    }

    private float GetFailBounceProgress(VisualContext context, bool failed)
    {
        if(!failed)
            return -1f;

        double landingSongPosition = GetLandingSongPosition();
        double elapsedBeats = (context.SongPosition - landingSongPosition) / _crotchet;
        if(elapsedBeats < 0.0 || elapsedBeats > FailBounceBeats)
            return -1f;

        return MathHelper.Clamp((float)(elapsedBeats / FailBounceBeats), 0f, 1f);
    }

    private Vector2 GetGroundedPropOffset(VisualContext context, bool failed)
    {
        if(!failed)
            return Vector2.Zero;

        double elapsedBeats = (context.SongPosition - GetLandingSongPosition()) / _crotchet;
        if(elapsedBeats <= 0.0)
            return Vector2.Zero;

        float failBounceProgress = GetFailBounceProgress(context, failed);
        Vector2 offset = new(-(float)elapsedBeats * PropScrollPixelsPerBeat, 0f);
        if(failBounceProgress >= 0f)
            offset += GetFailBounceOffset(failBounceProgress);

        return offset;
    }

    private Vector2 GetFailBounceOffset(float progress)
    {
        if(progress < 0f)
            return Vector2.Zero;

        float decay = 1f - progress;
        float bounce = MathF.Abs(MathF.Sin(progress * MathHelper.Pi * 2f));
        return new Vector2(0f, -bounce * FailBounceHeight * decay);
    }

    private float GetFailBounceRotation(float progress)
    {
        if(progress < 0f)
            return 0f;

        return MathHelper.ToRadians(34f) * progress;
    }

    private void ApplyBagLandingBounce(VisualContext context, bool mined)
    {
        if(!mined)
            return;

        double landingSongPosition = GetBagBounceSongPosition();
        double elapsedBeats = (context.SongPosition - landingSongPosition) / _crotchet;
        if(elapsedBeats < 0.0 || elapsedBeats > GemwalkGlamour.BagLandingBounceBeats)
            return;

        float progress = MathHelper.Clamp((float)(elapsedBeats / GemwalkGlamour.BagLandingBounceBeats), 0f, 1f);
        float decay = 1f - progress;
        float bounce = MathF.Sin(progress * MathHelper.TwoPi * 1.5f) * decay;
        float squash = Math.Max(0f, bounce) * BagLandingScalePulse;
        float stretch = Math.Max(0f, -bounce) * BagLandingScalePulse * 0.55f;
        context.Mutate<GameObject>(_bagTrackId, bag =>
        {
            bag.Position = _scene.GetBagIdlePosition(context.SongPosition) + new Vector2(0f, bounce * BagLandingBounceAmplitude);
            bag.Scale = new Vector2(
                GemwalkGlamour.BagScale * (1f + squash - stretch * 0.35f),
                GemwalkGlamour.BagScale * (1f - squash * 0.55f + stretch));
        });
    }

    private float GetLandingProgress(VisualContext context)
    {
        double elapsedAfterEndBeats = (context.SongPosition - Note.EndSongPosition) / _crotchet;
        double landingBeats = Note.HoldDuration > 0.0
            ? GemwalkGlamour.DespawnBeats
            : GemwalkGlamour.NonHoldOreLandingBeats;
        return MathHelper.Clamp((float)(elapsedAfterEndBeats / landingBeats), 0f, 1f);
    }

    private double GetLandingSongPosition()
    {
        return Note.HoldDuration > 0.0
            ? Note.EndSongPosition
            : Note.SongPosition + GemwalkGlamour.NonHoldOreLandingBeats * _crotchet;
    }

    private double GetBagBounceSongPosition()
    {
        return _action == GemwalkGlamourAction.Ruby
            && _rubyReleaseSuccessful
            && !double.IsNaN(_rubyReleaseSongPosition)
            ? _rubyReleaseSongPosition
            : GetLandingSongPosition();
    }

    private bool HasLanded(VisualContext context)
    {
        return context.SongPosition >= GetLandingSongPosition() - 0.000001;
    }

    private Vector2 GetFailedLandingPosition()
    {
        return _scene.GetFailedGemGroundTarget();
    }

    private float GetPickaxeStrikeProgress(double elapsedBeats)
    {
        if(elapsedBeats < _pickaxeStrikeRushBeats)
            return EaseOutCubic((float)(elapsedBeats / _pickaxeStrikeRushBeats));

        if(elapsedBeats < _pickaxeStrikeHoldBeats)
            return 1f;

        float returnProgress = (float)((elapsedBeats - _pickaxeStrikeHoldBeats) / _pickaxeStrikeReturnBeats);
        return 1f - EaseInOutCubic(returnProgress);
    }

    private Vector2 GetPickaxeStrikeFloat(VisualContext context)
    {
        float beat = (float)(context.SongPosition / _crotchet);
        return new Vector2(0f, MathF.Sin((beat + 0.35f) * MathHelper.TwoPi) * 8f);
    }

    private static float GetSapphirePickaxeStrikeRotation(double elapsedBeats, double rushBeats, double holdBeats, double returnBeats, float startRotation)
    {
        float idleRotation = MathHelper.ToRadians(-6f);
        float windupRotation = startRotation - MathHelper.ToRadians(72f);
        float impactRotation = MathHelper.ToRadians(104f);
        float holdRotation = MathHelper.ToRadians(58f);

        if(elapsedBeats < rushBeats)
        {
            float progress = MathHelper.Clamp((float)(elapsedBeats / rushBeats), 0f, 1f);
            if(progress < 0.45f)
                return MathHelper.Lerp(startRotation, windupRotation, EaseOutCubic(progress / 0.45f));

            return MathHelper.Lerp(windupRotation, impactRotation, EaseOutCubic((progress - 0.45f) / 0.55f));
        }

        double settleBeats = 0.22;
        if(elapsedBeats < rushBeats + settleBeats)
        {
            float settleProgress = (float)((elapsedBeats - rushBeats) / settleBeats);
            return MathHelper.Lerp(impactRotation, holdRotation, EaseOutCubic(settleProgress));
        }

        if(elapsedBeats < holdBeats)
            return holdRotation;

        float returnProgress = (float)((elapsedBeats - holdBeats) / returnBeats);
        return MathHelper.Lerp(holdRotation, idleRotation, EaseInOutCubic(returnProgress));
    }

    private void SetOreSprite(bool mined)
    {
        string sprite = GetOreSprite(mined);
        if(_currentOreSprite == sprite)
            return;

        Color color = _ore.sprite?.Color ?? Color.White;
        _ore.sprite = GLOBALS.main_atlas.CreateSprite(sprite);
        _ore.sprite.CenterOrigin();
        _ore.sprite.Color = color;
        _currentOreSprite = sprite;
    }

    private void SetApproachOreSprite()
    {
        string sprite = GetOreSprite(mined: false);
        if(_currentApproachOreSprite == sprite)
            return;

        Color color = _approachOre.sprite?.Color ?? Color.White;
        _approachOre.sprite = CreateWhiteApproachSprite(sprite);
        _approachOre.sprite.CenterOrigin();
        _approachOre.sprite.Color = color;
        _currentApproachOreSprite = sprite;
    }

    private static Sprite CreateWhiteApproachSprite(string regionName)
    {
        if(!WhiteApproachRegions.TryGetValue(regionName, out TextureRegion whiteRegion))
        {
            TextureRegion source = GLOBALS.main_atlas.GetRegion(regionName);
            Color[] sourcePixels = new Color[source.SourceRectangle.Width * source.SourceRectangle.Height];
            source.Texture.GetData(0, source.SourceRectangle, sourcePixels, 0, sourcePixels.Length);

            for(int i = 0; i < sourcePixels.Length; i++)
            {
                byte alpha = sourcePixels[i].A;
                sourcePixels[i] = new Color(alpha, alpha, alpha, alpha);
            }

            Texture2D whiteTexture = new(GLOBALS.graphicsDevice, source.SourceRectangle.Width, source.SourceRectangle.Height);
            whiteTexture.SetData(sourcePixels);

            whiteRegion = new TextureRegion(whiteTexture, 0, 0, source.SourceRectangle.Width, source.SourceRectangle.Height)
            {
                OriginalWidth = source.OriginalWidth,
                OriginalHeight = source.OriginalHeight,
                TrimOffset = source.TrimOffset,
                Pivot = source.Pivot,
                IsRotated = source.IsRotated,
                AtlasRotationDegrees = source.AtlasRotationDegrees
            };
            WhiteApproachRegions[regionName] = whiteRegion;
        }

        return new Sprite(whiteRegion);
    }

    private static OreShard[] CreateOreShards(TextureRegion sourceRegion, int seed)
    {
        if(sourceRegion == null)
            return Array.Empty<OreShard>();

        Random random = new(seed);
        OreShard[] shards = new OreShard[OreShardCount];
        Rectangle source = sourceRegion.SourceRectangle;
        for(int i = 0; i < shards.Length; i++)
        {
            int width = random.Next(10, 23);
            int height = random.Next(10, 23);
            width = Math.Min(width, Math.Max(1, source.Width));
            height = Math.Min(height, Math.Max(1, source.Height));
            int x = source.X + random.Next(0, Math.Max(1, source.Width - width + 1));
            int y = source.Y + random.Next(0, Math.Max(1, source.Height - height + 1));

            float angle = MathHelper.Lerp(-MathHelper.Pi + 0.18f, -0.18f, (float)random.NextDouble());
            float speed = MathHelper.Lerp(74f, 214f, (float)random.NextDouble());
            Vector2 velocity = new(MathF.Cos(angle) * speed, MathF.Sin(angle) * speed - 28f);
            Vector2 offset = new(
                MathHelper.Lerp(-30f, 30f, (float)random.NextDouble()),
                MathHelper.Lerp(-24f, 18f, (float)random.NextDouble()));

            shards[i] = new OreShard(
                new Rectangle(x, y, width, height),
                new Vector2(width * 0.5f, height * 0.5f),
                offset,
                velocity,
                MathHelper.Lerp(-MathHelper.Pi, MathHelper.Pi, (float)random.NextDouble()),
                MathHelper.Lerp(-5.2f, 5.2f, (float)random.NextDouble()),
                MathHelper.Lerp(0.68f, 1.18f, (float)random.NextDouble()),
                MathHelper.Lerp(0.0f, 0.08f, (float)random.NextDouble()),
                MathHelper.Lerp(0.46f, (float)OreShardLifetimeBeats, (float)random.NextDouble()));
        }

        return shards;
    }

    private string GetOreSprite(bool mined)
    {
        return _action == GemwalkGlamourAction.Ruby
            ? mined ? MainAtlas.Gemwalk_glammour_ore_ruby2 : MainAtlas.Gemwalk_glammour_ore_ruby1
            : mined ? MainAtlas.Gemwalk_glammour_ore_sapphire2 : MainAtlas.Gemwalk_glammour_ore_sapphire1;
    }

    private void RerollApexOnRewind(VisualContext context)
    {
        if(!context.HasRewound)
        {
            _rerolledForCurrentRewind = false;
            return;
        }

        if(_rerolledForCurrentRewind || context.SongPosition > Note.SongPosition)
            return;

        _rerolledForCurrentRewind = true;

        _apexPosition = GetRandomApexPosition(_baseApexPosition);
        _hasPickaxeStrikeStart = false;
        ResetRubyReleaseState();
    }

    private bool IsThreeSapphiresNote()
    {
        return GemwalkGlamourProvider.GetSourceClipId(Note.AdditionnalData) == GemwalkGlamourProvider.ThreeSapphiresClipId;
    }

    private bool IsFirstThreeSapphiresHit()
    {
        if(!IsThreeSapphiresNote())
            return false;

        if(GemwalkGlamourProvider.GetSourceClipId(PreviousNote?.AdditionnalData) != GemwalkGlamourProvider.ThreeSapphiresClipId)
            return true;

        double expectedSpacing = GemwalkGlamourProvider.GetBeatSpacing(Note.AdditionnalData) * _crotchet;
        return Note.SongPosition - PreviousNote.SongPosition > expectedSpacing + 0.001;
    }

    private static string GetRubyHoldCueSfx(int cueIndex, int cueCount)
    {
        if(cueCount > 1 && cueIndex == cueCount - 2)
            return RubyEnd1CueSfx;

        return RubyCueSfx;
    }

    private static Vector2 GetRandomApexPosition(Vector2 apexPosition)
    {
        float verticalOffset = Random.Shared.NextSingle() * 300f - 180f;
        return apexPosition + new Vector2(0f, verticalOffset);
    }

    private static void SetVisible(GameObject gameObject, bool visible, float alpha)
    {
        if(gameObject?.sprite == null)
            return;

        gameObject.sprite.Color = visible ? Color.White * MathHelper.Clamp(alpha, 0f, 1f) : Color.Transparent;
    }

    private Vector2 GetTravelPosition(VisualContext context, float fullProgress, bool failed)
    {
        return GetTravelPositionAt(context, context.SongPosition, fullProgress, failed);
    }

    private Vector2 GetTravelPositionAt(VisualContext context, double songPosition, float fullProgress, bool failed)
    {
        Vector2 target = failed ? _scene.GetFailedGemTarget(songPosition) : _scene.GetBagGemTarget(songPosition);
        fullProgress = MathHelper.Clamp(fullProgress, 0f, 1f);

        if(fullProgress <= 0.5f)
        {
            float progress = fullProgress / 0.5f;
            float falloff = 1f - progress;
            float approachY = _apexPosition.Y + (_spawnPosition.Y - _apexPosition.Y) * falloff * falloff;
            return new Vector2(MathHelper.Lerp(_spawnPosition.X, _apexPosition.X, progress), approachY);
        }

        float descentProgress = (fullProgress - 0.5f) / 0.5f;
        float x = MathHelper.Lerp(_apexPosition.X, target.X, descentProgress);
        float yDescent = descentProgress * descentProgress;
        float descentY = MathHelper.Lerp(_apexPosition.Y, target.Y, yDescent);
        return new Vector2(x, descentY);
    }

    private float GetOreRotation(float fullProgress)
    {
        float direction = _action == GemwalkGlamourAction.Ruby ? -1f : 1f;
        return MathHelper.ToRadians(12f) + MathHelper.Clamp(fullProgress, 0f, 1f) * MathHelper.TwoPi * 1.65f * direction;
    }

    private static float EaseOutCubic(float value)
    {
        value = MathHelper.Clamp(value, 0f, 1f);
        float inverse = 1f - value;
        return 1f - inverse * inverse * inverse;
    }

    private static float EaseInOutCubic(float value)
    {
        value = MathHelper.Clamp(value, 0f, 1f);
        return value < 0.5f
            ? 4f * value * value * value
            : 1f - MathF.Pow(-2f * value + 2f, 3f) * 0.5f;
    }

    private readonly struct OreShard
    {
        public OreShard(Rectangle sourceRectangle, Vector2 origin, Vector2 offset, Vector2 velocity, float rotation, float angularVelocity, float scale, double delayBeats, double lifetimeBeats)
        {
            SourceRectangle = sourceRectangle;
            Origin = origin;
            Offset = offset;
            Velocity = velocity;
            Rotation = rotation;
            AngularVelocity = angularVelocity;
            Scale = scale;
            DelayBeats = delayBeats;
            LifetimeBeats = lifetimeBeats;
        }

        public Rectangle SourceRectangle { get; }
        public Vector2 Origin { get; }
        public Vector2 Offset { get; }
        public Vector2 Velocity { get; }
        public float Rotation { get; }
        public float AngularVelocity { get; }
        public float Scale { get; }
        public double DelayBeats { get; }
        public double LifetimeBeats { get; }
    }
}
