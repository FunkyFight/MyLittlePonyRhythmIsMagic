using System;
using System.Collections.Generic;
using GameCore.Audio;
using GameCore.GameObjects;
using GameCore.Scenes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MLP_RiM.Elements.DevUI;
using MLP_RiM.Elements.Editor;
using Rhythm.Note;
using Rhythm.Note.Visual;
using TexturePackerMonoGameDefinitions;

public class GemwalkGlamour : Scene
{
    public const string PickaxeTrackId = "gemwalk_glamour.pickaxe";
    public const string BagTrackId = "gemwalk_glamour.bag";
    public const float PickaxeScale = 3.1f;
    public const float OreScale = PickaxeScale;
    public const float BagScale = 2.55f;
    public const double CueLeadBeats = 2.0;
    public const double DespawnBeats = 2.0;
    public const double BagLandingBounceBeats = 0.85;
    public const double NonHoldOreLandingBeats = DespawnBeats - BagLandingBounceBeats;
    public const double PickaxeStrikeRushBeats = 0.18;
    public const double PickaxeStrikeHoldBeats = 1.45;
    public const double PickaxeStrikeReturnBeats = 0.55;
    public const double RubyReleaseWindowSeconds = 0.1;
    private const string PerfectSapphireHitSfx = "SFX/GemwalkGlammour/1.wav";
    private const float PerfectHitSfxVolume = 1f;

    private const float RarityScale = 5.0f;
    private const double AnimationCycleBeats = 2.0;
    private const double FloatCycleBeats = 1.0;
    private const float ActorBackOffsetFifths = 0.5f;
    private const float ActorGroundYRatio = 0.63f;
    private const float OreSpawnRightPadding = 160f;
    private const float SapphireApexXRatio = 0.68f;
    private const float RubyApexXRatio = 0.72f;
    private const float SapphireApexYRatio = 0.28f;
    private const float RubyApexYRatio = 0.24f;
    private const float BagFloatAmplitude = 10f;
    private const float PickaxeFloatAmplitude = 8f;
    private const float RarityOffscreenPadding = 420f;
    private const float CaveScrollProgressionPerBeat = 0.11f;
    private const float CaveRearScale = 6.2f;
    private const float CaveMidScale = 7.1f;
    private const float CaveFrontScale = 8.0f;
    private const float CaveForegroundScale = 9.2f;
    private const float CaveFloorScale = 7.8f;
    private const float CaveFloorForegroundScale = 9.0f;
    private const float CaveFloorTopRatio = 0.68f;
    private const int CaveFloorHoofOffsetPixels = -3;
    private const float FailedGemGroundSinkPixels = 30f;
    private const int RadialLightTextureWidth = 224;
    private const int RadialLightTextureHeight = 144;
    private const int RadialLightPixelBlockSize = 6;
    private const int RadialLightStepCount = 32;
    private const int BackRadialLightPulseStepCount = 16;
    private const int FloorLightTextureSize = 192;
    private const int FloorLightPixelBlockSize = 4;
    private const int FloorLightStepCount = 32;
    private const float FloorLightDiameterRatio = 0.62f;

    private static readonly string[] RarityWalkSprites =
    {
        MainAtlas.Gemwalk_glammour_rarity_walk1,
        MainAtlas.Gemwalk_glammour_rarity_walk2,
        MainAtlas.Gemwalk_glammour_rarity_walk3,
        MainAtlas.Gemwalk_glammour_rarity_walk4,
        MainAtlas.Gemwalk_glammour_rarity_walk5,
        MainAtlas.Gemwalk_glammour_rarity_walk6,
        MainAtlas.Gemwalk_glammour_rarity_walk7,
        MainAtlas.Gemwalk_glammour_rarity_walk8,
        MainAtlas.Gemwalk_glammour_rarity_walk9,
        MainAtlas.Gemwalk_glammour_rarity_walk10
    };

    private static readonly string[] BagSprites =
    {
        MainAtlas.Gemwalk_glammour_bourse1,
        MainAtlas.Gemwalk_glammour_bourse2,
        MainAtlas.Gemwalk_glammour_bourse3,
        MainAtlas.Gemwalk_glammour_bourse4,
        MainAtlas.Gemwalk_glammour_bourse5
    };

    private static readonly string[] PickaxeSprites =
    {
        MainAtlas.Gemwalk_glammour_pickaxe1,
        MainAtlas.Gemwalk_glammour_pickaxe2,
        MainAtlas.Gemwalk_glammour_pickaxe3,
        MainAtlas.Gemwalk_glammour_pickaxe4,
        MainAtlas.Gemwalk_glammour_pickaxe5
    };

    private static readonly string[] StalactiteSprites =
    {
        MainAtlas.Stalagtite2,
        MainAtlas.Stalagtite3,
        MainAtlas.Stalagtite4,
        MainAtlas.Stalagtite5,
        MainAtlas.Stalagtite6
    };

    private InfiniteScrollBackground _rearCaveBackground;
    private InfiniteScrollBackground _frontCaveBackground;
    private InfiniteScrollBackground _floorCaveBackground;
    private Texture2D _backRadialLightTexture;
    private Texture2D _floorRadialLightTexture;
    private Texture2D _floorPixelTexture;
    private Rectangle _backRadialLightBounds;
    private int _backRadialLightPulseStep = -1;
    private GameObject _rarity;
    private GameObject _bag;
    private GameObject _pickaxe;
    private Vector2 _rarityBasePosition;
    private Vector2 _bagBasePosition;
    private Vector2 _pickaxeBasePosition;
    private string _raritySpriteRegion;
    private string _bagSpriteRegion;
    private string _pickaxeSpriteRegion;
    private DevUiRenderer _devUIRenderer;
    private VisualRuntime _visualRuntime;
    private VisualNoteManager<GemwalkGlamourOreVisualNote> _oreVisualNoteManager;
    private ChartPlayer _reactionChartPlayer;

    public GemwalkGlamour() : base("Gemwalk Glamour")
    {
    }

    public override void OnLoad()
    {
        Viewport viewport = GLOBALS.graphicsDevice.Viewport;
        _devUIRenderer = new DevUiRenderer(GLOBALS.graphicsDevice);

        _rarityBasePosition = new Vector2(viewport.Width * Fifth(2f), viewport.Height * ActorGroundYRatio);
        _bagBasePosition = new Vector2(viewport.Width * Fifth(1f), _rarityBasePosition.Y + 42f);
        _pickaxeBasePosition = new Vector2(viewport.Width * Fifth(3f), _rarityBasePosition.Y - 36f);

        CreateCaveBackgrounds(viewport);

        _bag = CreateAtlasObject(MainAtlas.Gemwalk_glammour_bourse1, _bagBasePosition, BagScale);
        _rarity = CreateAtlasObject(MainAtlas.Gemwalk_glammour_rarity_walk1, _rarityBasePosition, RarityScale);
        _pickaxe = CreateAtlasObject(MainAtlas.Gemwalk_glammour_pickaxe1, _pickaxeBasePosition, PickaxeScale);

        GameObjects.Add(_bag);
        GameObjects.Add(_rarity);
        GameObjects.Add(_pickaxe);

        SetupVisualNotes();
        GLOBALS.beatmapPlayer.BeatmapStarted += SetupVisualNotes;
        SetupReactionFeedbacks();
        GLOBALS.beatmapPlayer.BeatmapStarted += SetupReactionFeedbacks;
    }

    public override void OnUnload()
    {
        GLOBALS.beatmapPlayer.BeatmapStarted -= SetupVisualNotes;
        GLOBALS.beatmapPlayer.BeatmapStarted -= SetupReactionFeedbacks;
        UnsubscribeReactionFeedbacks();
        _visualRuntime?.ClearDrivers();
        _visualRuntime = null;
        _oreVisualNoteManager = null;
        _devUIRenderer = null;
        _reactionChartPlayer = null;
        _rearCaveBackground = null;
        _frontCaveBackground = null;
        _floorCaveBackground = null;
        _backRadialLightTexture?.Dispose();
        _backRadialLightTexture = null;
        _backRadialLightPulseStep = -1;
        _floorRadialLightTexture?.Dispose();
        _floorRadialLightTexture = null;
        _floorPixelTexture?.Dispose();
        _floorPixelTexture = null;
        _rarity = null;
        _bag = null;
        _pickaxe = null;
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        double songPosition = GLOBALS.beatmapPlayer?.GameplaySongPosition ?? 0.0;
        double beat = GLOBALS.beatmapPlayer.Conductor != null
            ? GLOBALS.beatmapPlayer.GetBeatAt(songPosition)
            : 0.0;
        UpdateCaveBackgroundProgression(beat);

        if (GLOBALS.beatmapPlayer.Conductor == null || GLOBALS.beatmapPlayer.ChartPlayer == null)
        {
            ApplyAmbientActors(0.0);
            _visualRuntime?.ClearDrivers();
            return;
        }

        IReadOnlyList<Note> notes = GLOBALS.beatmapPlayer.ChartPlayer.Notes;
        _visualRuntime?.ResolveDrivers(songPosition, notes);
        ApplyAmbientActors(beat);
        _oreVisualNoteManager?.Update(songPosition);
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        GLOBALS.graphicsDevice.Clear(new Color(39, 31, 58));

        DrawBackRadialLight(spriteBatch);
        _rearCaveBackground?.Draw(spriteBatch);
        _frontCaveBackground?.Draw(spriteBatch);
        DrawCaveFloor(spriteBatch);
        DrawFloorRadialLight(spriteBatch);
        _oreVisualNoteManager?.Draw(spriteBatch);
        _bag?.Draw(spriteBatch);
        _rarity?.Draw(spriteBatch);
        _pickaxe?.Draw(spriteBatch);
        _floorCaveBackground?.Draw(spriteBatch);

    }

    private void CreateCaveBackgrounds(Viewport viewport)
    {
        _backRadialLightTexture = CreatePixelatedRadialLightTexture(GLOBALS.graphicsDevice);
        _floorRadialLightTexture = CreatePixelatedFloorLightTexture(GLOBALS.graphicsDevice);
        _floorPixelTexture = CreatePixelTexture(GLOBALS.graphicsDevice);
        int lightWidth = (int)(viewport.Width * 1.95f);
        int lightHeight = (int)(viewport.Height * 1.55f);
        _backRadialLightBounds = new Rectangle(
            (int)(viewport.Width * 0.5f - lightWidth * 0.5f),
            (int)(viewport.Height * 0.24f - lightHeight * 0.5f),
            lightWidth,
            lightHeight);

        _rearCaveBackground = new InfiniteScrollBackground.Builder()
            .WithPixelsPerProgress(new Vector2(viewport.Width, 0f))
            .WithRandomSeed(401)
            .AddLine(line =>
            {
                line.AddPrototypes(CreateStalactites(Color.White * 0.92f));
                line.WithOffset(new Vector2(0f, -24f));
                line.WithScale(CaveRearScale);
                line.WithScrollMultiplier(0.18f);
                line.WithPlacementInterval(70f, 180f);
            })
            .AddLine(line =>
            {
                line.AddPrototypes(CreateStalactites(new Color(145, 132, 170)));
                line.WithOffset(new Vector2(0f, -18f));
                line.WithScale(CaveMidScale);
                line.WithScrollMultiplier(0.28f);
                line.WithPlacementInterval(90f, 230f);
            })
            .Build();

        _frontCaveBackground = new InfiniteScrollBackground.Builder()
            .WithPixelsPerProgress(new Vector2(viewport.Width, 0f))
            .WithRandomSeed(947)
            .AddLine(line =>
            {
                line.AddPrototypes(CreateStalactites(new Color(74, 62, 94)));
                line.WithOffset(new Vector2(0f, -14f));
                line.WithScale(CaveFrontScale);
                line.WithScrollMultiplier(0.55f);
                line.WithPlacementInterval(120f, 280f);
            })
            .AddLine(line =>
            {
                line.AddPrototypes(CreateStalactites(new Color(33, 27, 44)));
                line.WithOffset(new Vector2(0f, -10f));
                line.WithScale(CaveForegroundScale);
                line.WithScrollMultiplier(0.82f);
                line.WithPlacementInterval(150f, 340f);
            })
            .Build();

        _floorCaveBackground = new InfiniteScrollBackground.Builder()
            .WithPixelsPerProgress(new Vector2(viewport.Width, 0f))
            .WithRandomSeed(1431)
            .AddLine(line =>
            {
                line.AddPrototypes(CreateStalagmites(new Color(42, 35, 56)));
                line.WithOffset(new Vector2(0f, viewport.Height + 18f));
                line.WithScale(CaveFloorScale);
                line.WithScrollMultiplier(0.96f);
                line.WithPlacementInterval(120f, 270f);
            })
            .AddLine(line =>
            {
                line.AddPrototypes(CreateStalagmites(new Color(24, 20, 34)));
                line.WithOffset(new Vector2(0f, viewport.Height + 32f));
                line.WithScale(CaveFloorForegroundScale);
                line.WithScrollMultiplier(1.18f);
                line.WithPlacementInterval(170f, 360f);
            })
            .Build();
    }

    private void UpdateCaveBackgroundProgression(double beat)
    {
        float progression = (float)(Math.Max(0.0, beat) * CaveScrollProgressionPerBeat);
        if (_rearCaveBackground != null)
            _rearCaveBackground.Progression = progression;

        if (_frontCaveBackground != null)
            _frontCaveBackground.Progression = progression;

        if (_floorCaveBackground != null)
            _floorCaveBackground.Progression = progression;
    }

    private void DrawBackRadialLight(SpriteBatch spriteBatch)
    {
        if (_backRadialLightTexture == null)
            return;

        double songPosition = GLOBALS.beatmapPlayer?.GameplaySongPosition ?? 0.0;
        double beat = GLOBALS.beatmapPlayer.Conductor != null
            ? GLOBALS.beatmapPlayer.GetBeatAt(songPosition)
            : 0.0;
        float beatPhase = (float)(beat - Math.Floor(beat));
        float inversePhase = 1f - beatPhase;
        float pulse = inversePhase * inversePhase * inversePhase;
        int pulseStep = (int)MathF.Round(pulse * BackRadialLightPulseStepCount);
        if (pulseStep != _backRadialLightPulseStep)
        {
            _backRadialLightPulseStep = pulseStep;
            UpdatePixelatedRadialLightTexture(_backRadialLightTexture, pulseStep / (float)BackRadialLightPulseStepCount);
        }

        spriteBatch.Draw(_backRadialLightTexture, _backRadialLightBounds, Color.White);
    }

    private void DrawCaveFloor(SpriteBatch spriteBatch)
    {
        if (_floorPixelTexture == null)
            return;

        Viewport viewport = GLOBALS.graphicsDevice.Viewport;
        int floorTop = GetCaveFloorTopY(viewport);
        Rectangle floorBounds = new(0, floorTop, viewport.Width, viewport.Height - floorTop);
        spriteBatch.Draw(_floorPixelTexture, floorBounds, new Color(34, 30, 48));
    }

    private void DrawFloorRadialLight(SpriteBatch spriteBatch)
    {
        if (_floorRadialLightTexture == null || _rarity == null)
            return;

        Viewport viewport = GLOBALS.graphicsDevice.Viewport;
        double songPosition = GLOBALS.beatmapPlayer?.GameplaySongPosition ?? 0.0;
        double beat = GLOBALS.beatmapPlayer.Conductor != null
            ? GLOBALS.beatmapPlayer.GetBeatAt(songPosition)
            : 0.0;
        float blink = 0.72f + 0.28f * (0.5f + 0.5f * MathF.Sin((float)beat * MathHelper.TwoPi));
        int floorTop = GetCaveFloorTopY(viewport);
        int diameter = (int)(viewport.Height * FloorLightDiameterRatio);
        Vector2 center = new(_rarity.Position.X, floorTop);
        Rectangle source = new(
            0,
            FloorLightTextureSize / 2,
            FloorLightTextureSize,
            FloorLightTextureSize / 2);
        Rectangle bounds = new(
            (int)(center.X - diameter * 0.5f),
            (int)center.Y,
            diameter,
            diameter / 2);

        spriteBatch.Draw(_floorRadialLightTexture, bounds, source, Color.White * blink);
    }

    private int GetCaveFloorTopY(Viewport viewport)
    {
        if (_rarity != null && _rarity.Height > 0f)
            return Math.Clamp((int)(_rarity.Position.Y + _rarity.Height * 0.5f) + CaveFloorHoofOffsetPixels, 0, viewport.Height - 1);

        return (int)(viewport.Height * CaveFloorTopRatio);
    }

    private float GetFailedGemGroundCenterY()
    {
        var region = GLOBALS.main_atlas?.GetRegion(MainAtlas.Gemwalk_glammour_ore_sapphire1);
        float oreHalfHeight = (region?.DrawHeight ?? 64) * OreScale * 0.5f;
        return GetCaveFloorTopY(GLOBALS.graphicsDevice.Viewport) - oreHalfHeight + FailedGemGroundSinkPixels;
    }

    public Vector2 GetBagGemTarget(double songPosition)
    {
        return GetBagPosition(songPosition) + new Vector2(-4f, -34f);
    }

    public Vector2 GetFailedGemTarget(double songPosition)
    {
        return new Vector2(GetBagPosition(songPosition).X + 96f, GetFailedGemGroundCenterY());
    }

    public Vector2 GetFailedGemGroundTarget()
    {
        return new Vector2(_bagBasePosition.X + 96f, GetFailedGemGroundCenterY());
    }

    public Vector2 GetBagIdlePosition(double songPosition)
    {
        return GetBagPosition(songPosition);
    }

    public Vector2 GetPickaxeIdlePosition(double songPosition)
    {
        double beat = GLOBALS.beatmapPlayer?.Conductor != null
            ? GLOBALS.beatmapPlayer.GetBeatAt(songPosition)
            : 0.0;
        return GetPickaxeIdlePositionAtBeat(beat);
    }

    public static bool IsSuccessful(Note note)
    {
        return note is { HasReacted: true, HasBeenMissed: false };
    }

    public static bool IsMissed(Note note)
    {
        return note is { HasBeenMissed: true };
    }

    private void SetupVisualNotes()
    {
        _visualRuntime?.ClearDrivers();
        _visualRuntime = new VisualRuntime();
        _visualRuntime.RegisterTrack(PickaxeTrackId, _pickaxe)
            .UseDriverResolver(ctx => FindCurrentPickaxeDriver(ctx.SongPosition, ctx.Notes));
        _visualRuntime.RegisterTrack(BagTrackId, _bag)
            .UseDriverResolver(ctx => FindCurrentBagBounceDriver(ctx.SongPosition, ctx.Notes));

        if (GLOBALS.beatmapPlayer.ChartPlayer == null)
        {
            _oreVisualNoteManager = null;
            return;
        }

        _oreVisualNoteManager = new VisualNoteManager<GemwalkGlamourOreVisualNote>(GLOBALS.beatmapPlayer.ChartPlayer, CreateOreVisualNote)
        {
            LookAheadSeconds = GLOBALS.beatmapPlayer.GetMaxCrotchet() * 24.0,
            LookBehindSeconds = GLOBALS.beatmapPlayer.GetMaxCrotchet() * 12.0
        };
    }

    private void SetupReactionFeedbacks()
    {
        UnsubscribeReactionFeedbacks();
        _reactionChartPlayer = GLOBALS.beatmapPlayer.ChartPlayer;
        if (_reactionChartPlayer != null)
            _reactionChartPlayer.NoteReactedWithNote += OnNoteReacted;
    }

    private void UnsubscribeReactionFeedbacks()
    {
        if (_reactionChartPlayer == null)
            return;

        _reactionChartPlayer.NoteReactedWithNote -= OnNoteReacted;
        _reactionChartPlayer = null;
    }

    private void OnNoteReacted(NoteReactionResult result, Note note)
    {
        if (result == NoteReactionResult.MISS || !IsPickaxeReactionNote(note))
            return;

        SFX.Play(this, PerfectSapphireHitSfx, PerfectHitSfxVolume);
    }

    private GemwalkGlamourOreVisualNote CreateOreVisualNote(Note note)
    {
        if (!GemwalkGlamourNoteCodec.TryReadAction(note?.AdditionnalData, out GemwalkGlamourAction action)
            || action is not (GemwalkGlamourAction.OneSapphire or GemwalkGlamourAction.Ruby))
            return null;

        double crotchet = GLOBALS.beatmapPlayer.GetCrotchetAt(note.SongPosition);
        return new GemwalkGlamourOreVisualNote(
            this,
            note,
            _visualRuntime,
            action,
            GetOreSpawnPosition(action),
            GetOreApexPosition(action),
            PickaxeTrackId,
            BagTrackId,
            OreScale,
            crotchet,
            PickaxeStrikeRushBeats,
            PickaxeStrikeHoldBeats,
            PickaxeStrikeReturnBeats);
    }

    private Note FindCurrentPickaxeDriver(double songPosition, IReadOnlyList<Note> notes)
    {
        double beat = GLOBALS.beatmapPlayer.GetBeatAt(songPosition);
        Note driver = null;
        double driverBeat = double.NegativeInfinity;

        foreach (Note note in notes ?? Array.Empty<Note>())
        {
            if (IsRubyHoldPickaxeDriver(note, songPosition))
            {
                double rubyHitBeat = GetNoteBeat(note);
                if (rubyHitBeat >= driverBeat)
                {
                    driver = note;
                    driverBeat = rubyHitBeat;
                }

                continue;
            }

            if (!GemwalkGlamourNoteCodec.IsAction(note?.AdditionnalData, GemwalkGlamourAction.OneSapphire) || !IsSuccessful(note))
                continue;

            double hitBeat = GetNoteBeat(note);
            double strikeEndBeat = hitBeat + PickaxeStrikeHoldBeats + PickaxeStrikeReturnBeats;
            if (beat < hitBeat || beat > strikeEndBeat || hitBeat < driverBeat)
                continue;

            driver = note;
            driverBeat = hitBeat;
        }

        return driver;
    }

    private static bool IsRubyHoldPickaxeDriver(Note note, double songPosition)
    {
        if (!GemwalkGlamourNoteCodec.IsAction(note?.AdditionnalData, GemwalkGlamourAction.Ruby)
            || note is not { HasReacted: true, HasBeenMissed: false })
            return false;

        bool isAutoplayHold = GLOBALS.beatmapEditorElement?.IsPreviewAutoplayEnabled == true
            && songPosition <= note.EndSongPosition;
        if (!GLOBALS.ReactMainIsPressed && !isAutoplayHold)
            return false;

        return songPosition >= note.SongPosition
            && songPosition <= note.EndSongPosition + RubyReleaseWindowSeconds;
    }

    private Note FindCurrentBagBounceDriver(double songPosition, IReadOnlyList<Note> notes)
    {
        double beat = GLOBALS.beatmapPlayer.GetBeatAt(songPosition);
        Note driver = null;
        double landingBeat = double.NegativeInfinity;

        foreach (Note note in notes ?? Array.Empty<Note>())
        {
            if (!IsPickaxeReactionNote(note) || !IsSuccessful(note))
                continue;

            double holdBeats = GetHoldBeats(note);
            double noteLandingBeat = GetNoteBeat(note) + (holdBeats > 0.0 ? holdBeats : NonHoldOreLandingBeats);
            if (beat < noteLandingBeat || beat > noteLandingBeat + BagLandingBounceBeats || noteLandingBeat < landingBeat)
                continue;

            driver = note;
            landingBeat = noteLandingBeat;
        }

        return driver;
    }

    private void ApplyAmbientActors(double beat)
    {
        UpdateRarityWalk(beat);
        if (_visualRuntime?.Track(BagTrackId)?.DriverNote == null)
            UpdateBag(beat);

        UpdatePickaxe(beat, updateTransform: _visualRuntime?.Track(PickaxeTrackId)?.DriverNote == null);
    }

    private void UpdatePickaxe(double beat, bool updateTransform)
    {
        SetSprite(_pickaxe, ref _pickaxeSpriteRegion, PickaxeSprites[GetLoopFrame(beat, PickaxeSprites.Length)]);
        if (!updateTransform)
            return;

        _pickaxe.Position = GetPickaxeIdlePositionAtBeat(beat);
        _pickaxe.Rotation = MathHelper.ToRadians(-6f);
        _pickaxe.Scale = Vector2.One * PickaxeScale;
    }

    private void UpdateBag(double beat)
    {
        SetSprite(_bag, ref _bagSpriteRegion, BagSprites[GetLoopFrame(beat, BagSprites.Length)]);
        _bag.Position = GetBagPositionAtBeat(beat);
        _bag.Scale = Vector2.One * BagScale;
    }

    private void UpdateRarityWalk(double beat)
    {
        int frame = GetLoopFrame(beat, RarityWalkSprites.Length);
        SetSprite(_rarity, ref _raritySpriteRegion, RarityWalkSprites[frame]);
        _rarity.Scale = Vector2.One * RarityScale;
        _rarity.Position = GetRarityClipPosition(beat);
    }

    private Vector2 GetRarityClipPosition(double beat)
    {
        if (TryFindActiveRarityMotionClip(beat, out ChartEditorClip activeClip, out bool activeIsEntry))
            return GetRarityMotionClipPosition(activeClip, activeIsEntry, beat);

        if (TryFindLatestCompletedRarityMotionClip(beat, out _, out bool completedIsEntry) && !completedIsEntry)
            return GetRarityOffscreenRightPosition();

        return _rarityBasePosition;
    }

    private Vector2 GetActorClipOffset(double beat)
    {
        return GetRarityClipPosition(beat) - _rarityBasePosition;
    }

    private Vector2 GetBagPositionAtBeat(double beat)
    {
        return _bagBasePosition
            + GetActorClipOffset(beat)
            + GetBeatFloatOffset(beat, BagFloatAmplitude, phaseBeats: 0.0);
    }

    private Vector2 GetPickaxeIdlePositionAtBeat(double beat)
    {
        return _pickaxeBasePosition
            + GetActorClipOffset(beat)
            + GetBeatFloatOffset(beat, PickaxeFloatAmplitude, phaseBeats: 0.35);
    }

    private bool TryFindActiveRarityMotionClip(double beat, out ChartEditorClip activeClip, out bool isEntry)
    {
        activeClip = null;
        isEntry = false;
        double activeStartBeat = double.NegativeInfinity;
        IReadOnlyList<ChartEditorClip> clips = GLOBALS.beatmapPlayer.CurrentChart?.EditorClips;
        if (clips == null)
            return false;

        foreach (ChartEditorClip clip in clips)
        {
            if (!TryGetRarityMotionClipKind(clip, out bool clipIsEntry))
                continue;

            double start = clip.StartBeat;
            double end = start + Math.Max(0.001, clip.LengthBeats);
            if (beat < start || beat > end || start < activeStartBeat)
                continue;

            activeClip = clip;
            isEntry = clipIsEntry;
            activeStartBeat = start;
        }

        return activeClip != null;
    }

    private bool TryFindLatestCompletedRarityMotionClip(double beat, out ChartEditorClip completedClip, out bool isEntry)
    {
        completedClip = null;
        isEntry = false;
        double latestEndBeat = double.NegativeInfinity;
        IReadOnlyList<ChartEditorClip> clips = GLOBALS.beatmapPlayer.CurrentChart?.EditorClips;
        if (clips == null)
            return false;

        foreach (ChartEditorClip clip in clips)
        {
            if (!TryGetRarityMotionClipKind(clip, out bool clipIsEntry))
                continue;

            double end = clip.StartBeat + Math.Max(0.001, clip.LengthBeats);
            if (beat <= end || end < latestEndBeat)
                continue;

            completedClip = clip;
            isEntry = clipIsEntry;
            latestEndBeat = end;
        }

        return completedClip != null;
    }

    private static bool TryGetRarityMotionClipKind(ChartEditorClip clip, out bool isEntry)
    {
        isEntry = false;
        if (clip?.RhythmGameId != GemwalkGlamourProvider.GameId)
            return false;

        if (clip.ClipTypeId == GemwalkGlamourProvider.RarityEntryClipId)
        {
            isEntry = true;
            return true;
        }

        return clip.ClipTypeId == GemwalkGlamourProvider.RarityExitClipId;
    }

    private Vector2 GetRarityMotionClipPosition(ChartEditorClip clip, bool isEntry, double beat)
    {
        double duration = Math.Max(0.001, clip.LengthBeats);
        float progress = MathHelper.Clamp((float)((beat - clip.StartBeat) / duration), 0f, 1f);
        progress = EaseInOutCubic(progress);

        Vector2 hidden = isEntry ? GetRarityOffscreenLeftPosition() : GetRarityOffscreenRightPosition();
        return isEntry
            ? Vector2.Lerp(hidden, _rarityBasePosition, progress)
            : Vector2.Lerp(_rarityBasePosition, hidden, progress);
    }

    private Vector2 GetRarityOffscreenLeftPosition()
    {
        float halfWidth = (_rarity?.Width ?? 0f) * 0.5f;
        return new Vector2(-halfWidth - RarityOffscreenPadding, _rarityBasePosition.Y);
    }

    private Vector2 GetRarityOffscreenRightPosition()
    {
        Viewport viewport = GLOBALS.graphicsDevice.Viewport;
        float halfWidth = (_rarity?.Width ?? 0f) * 0.5f;
        return new Vector2(viewport.Width + halfWidth + RarityOffscreenPadding, _rarityBasePosition.Y);
    }

    private Vector2 GetBagPosition(double songPosition)
    {
        double beat = GLOBALS.beatmapPlayer?.Conductor != null
            ? GLOBALS.beatmapPlayer.GetBeatAt(songPosition)
            : 0.0;
        return GetBagPositionAtBeat(beat);
    }

    private static Vector2 GetOreSpawnPosition(GemwalkGlamourAction action)
    {
        Viewport viewport = GLOBALS.graphicsDevice.Viewport;
        float y = action == GemwalkGlamourAction.Ruby ? viewport.Height * 0.58f : viewport.Height * 0.62f;
        return new Vector2(viewport.Width + OreSpawnRightPadding, y);
    }

    private static Vector2 GetOreApexPosition(GemwalkGlamourAction action)
    {
        Viewport viewport = GLOBALS.graphicsDevice.Viewport;
        float xRatio = action == GemwalkGlamourAction.Ruby ? RubyApexXRatio : SapphireApexXRatio;
        float yRatio = action == GemwalkGlamourAction.Ruby ? RubyApexYRatio : SapphireApexYRatio;
        return new Vector2(viewport.Width * xRatio, viewport.Height * yRatio);
    }

    private static bool IsPickaxeReactionNote(Note note)
    {
        return GemwalkGlamourNoteCodec.TryReadAction(note?.AdditionnalData, out GemwalkGlamourAction action)
            && action is GemwalkGlamourAction.OneSapphire or GemwalkGlamourAction.Ruby;
    }

    private static double GetNoteBeat(Note note)
    {
        return GLOBALS.beatmapPlayer.GetBeatAt(note?.SongPosition ?? 0.0);
    }

    private static double GetHoldBeats(Note note)
    {
        if (note == null || note.HoldDuration <= 0.0)
            return 0.0;

        return GLOBALS.beatmapPlayer.GetBeatAt(note.EndSongPosition) - GetNoteBeat(note);
    }

    private static float Fifth(float index)
    {
        return (index - ActorBackOffsetFifths) / 5f;
    }

    private static GameObject CreateAtlasObject(string regionName, Vector2 position, float scale)
    {
        GameObject gameObject = new(GLOBALS.main_atlas.CreateSprite(regionName));
        gameObject.sprite.CenterOrigin();
        gameObject.Position = position;
        gameObject.Scale = Vector2.One * scale;
        return gameObject;
    }

    private static GameObject[] CreateStalactites(Color color)
    {
        GameObject[] stalactites = new GameObject[StalactiteSprites.Length];
        for (int i = 0; i < StalactiteSprites.Length; i++)
        {
            GameObject stalactite = new(GLOBALS.main_atlas.CreateSprite(StalactiteSprites[i]));
            stalactite.sprite.Origin = new Vector2(stalactite.sprite.Region.DrawWidth * 0.5f, 0f);
            stalactite.sprite.Color = color;
            stalactites[i] = stalactite;
        }

        return stalactites;
    }

    private static GameObject[] CreateStalagmites(Color color)
    {
        GameObject[] stalagmites = new GameObject[StalactiteSprites.Length];
        for (int i = 0; i < StalactiteSprites.Length; i++)
        {
            GameObject stalagmite = new(GLOBALS.main_atlas.CreateSprite(StalactiteSprites[i]));
            stalagmite.sprite.Origin = new Vector2(stalagmite.sprite.Region.DrawWidth * 0.5f, stalagmite.sprite.Region.DrawHeight);
            stalagmite.sprite.Effects = SpriteEffects.FlipVertically;
            stalagmite.sprite.Color = color;
            stalagmites[i] = stalagmite;
        }

        return stalagmites;
    }

    private static Texture2D CreatePixelTexture(GraphicsDevice graphicsDevice)
    {
        Texture2D texture = new(graphicsDevice, 1, 1);
        texture.SetData(new[] { Color.White });
        return texture;
    }

    private static Texture2D CreatePixelatedRadialLightTexture(GraphicsDevice graphicsDevice)
    {
        Texture2D texture = new(graphicsDevice, RadialLightTextureWidth, RadialLightTextureHeight);
        UpdatePixelatedRadialLightTexture(texture, 0f);
        return texture;
    }

    private static void UpdatePixelatedRadialLightTexture(Texture2D texture, float whiteExpansion)
    {
        Color[] pixels = new Color[RadialLightTextureWidth * RadialLightTextureHeight];
        Color edgeColor = new((byte)18, (byte)15, (byte)31, (byte)255);
        Color haloColor = new((byte)109, (byte)71, (byte)142, (byte)255);
        Color centerColor = new((byte)255, (byte)242, (byte)202, (byte)255);
        whiteExpansion = MathHelper.Clamp(whiteExpansion, 0f, 1f);
        float centerRadius = MathHelper.Lerp(0.12f, 0.32f, whiteExpansion);
        float falloffRadius = MathHelper.Lerp(0.90f, 0.78f, whiteExpansion);
        float haloThreshold = MathHelper.Lerp(0.58f, 0.42f, whiteExpansion);

        for (int blockY = 0; blockY < RadialLightTextureHeight; blockY += RadialLightPixelBlockSize)
        {
            for (int blockX = 0; blockX < RadialLightTextureWidth; blockX += RadialLightPixelBlockSize)
            {
                int blockWidth = Math.Min(RadialLightPixelBlockSize, RadialLightTextureWidth - blockX);
                int blockHeight = Math.Min(RadialLightPixelBlockSize, RadialLightTextureHeight - blockY);
                float x = ((blockX + blockWidth * 0.5f) / RadialLightTextureWidth - 0.5f) * 2f;
                float y = ((blockY + blockHeight * 0.5f) / RadialLightTextureHeight - 0.5f) * 2f;
                float distance = MathF.Sqrt(x * x + y * y * 1.5f);
                float falloff = distance <= centerRadius
                    ? 1f
                    : MathHelper.Clamp(1f - (distance - centerRadius) / falloffRadius, 0f, 1f);
                falloff = falloff * falloff * (3f - 2f * falloff);
                float stepped = MathF.Floor(falloff * RadialLightStepCount) / RadialLightStepCount;
                Color color = stepped < haloThreshold
                    ? Color.Lerp(edgeColor, haloColor, stepped / haloThreshold)
                    : Color.Lerp(haloColor, centerColor, (stepped - haloThreshold) / (1f - haloThreshold));

                for (int yy = blockY; yy < blockY + blockHeight; yy++)
                {
                    int row = yy * RadialLightTextureWidth;
                    for (int xx = blockX; xx < blockX + blockWidth; xx++)
                        pixels[row + xx] = color;
                }
            }
        }

        texture.SetData(pixels);
    }

    private static Texture2D CreatePixelatedFloorLightTexture(GraphicsDevice graphicsDevice)
    {
        Texture2D texture = new(graphicsDevice, FloorLightTextureSize, FloorLightTextureSize);
        Color[] pixels = new Color[FloorLightTextureSize * FloorLightTextureSize];

        for (int blockY = 0; blockY < FloorLightTextureSize; blockY += FloorLightPixelBlockSize)
        {
            for (int blockX = 0; blockX < FloorLightTextureSize; blockX += FloorLightPixelBlockSize)
            {
                int blockWidth = Math.Min(FloorLightPixelBlockSize, FloorLightTextureSize - blockX);
                int blockHeight = Math.Min(FloorLightPixelBlockSize, FloorLightTextureSize - blockY);
                float x = ((blockX + blockWidth * 0.5f) / FloorLightTextureSize - 0.5f) * 2f;
                float y = ((blockY + blockHeight * 0.5f) / FloorLightTextureSize - 0.5f) * 2f;
                float distance = MathF.Sqrt(x * x + y * y);
                float falloff = distance <= 0.18f
                    ? 1f
                    : MathHelper.Clamp(1f - (distance - 0.18f) / 0.82f, 0f, 1f);
                falloff = falloff * falloff * (3f - 2f * falloff);
                float stepped = MathF.Floor(falloff * FloorLightStepCount) / FloorLightStepCount;
                byte alpha = (byte)(stepped * 235f);
                float alphaScale = alpha / 255f;
                Color color = new(
                    (byte)(130 * alphaScale),
                    (byte)(193 * alphaScale),
                    (byte)(220 * alphaScale),
                    alpha);

                for (int yy = blockY; yy < blockY + blockHeight; yy++)
                {
                    int row = yy * FloorLightTextureSize;
                    for (int xx = blockX; xx < blockX + blockWidth; xx++)
                        pixels[row + xx] = color;
                }
            }
        }

        texture.SetData(pixels);
        return texture;
    }

    private static void SetSprite(GameObject gameObject, ref string currentRegion, string regionName)
    {
        if (gameObject == null || string.IsNullOrEmpty(regionName) || currentRegion == regionName)
            return;

        Color color = gameObject.sprite?.Color ?? Color.White;
        gameObject.sprite = GLOBALS.main_atlas.CreateSprite(regionName);
        gameObject.sprite.CenterOrigin();
        gameObject.sprite.Color = color;
        currentRegion = regionName;
    }

    private static int GetLoopFrame(double beat, int frameCount)
    {
        if (frameCount <= 1)
            return 0;

        double normalizedBeat = Math.Max(0.0, beat) / AnimationCycleBeats;
        double progress = normalizedBeat - Math.Floor(normalizedBeat);
        int frame = (int)Math.Floor(progress * frameCount);
        return Math.Clamp(frame, 0, frameCount - 1);
    }

    private static Vector2 GetBeatFloatOffset(double beat, float amplitude, double phaseBeats)
    {
        float angle = (float)((beat + phaseBeats) / FloatCycleBeats * MathHelper.TwoPi);
        return new Vector2(0f, MathF.Sin(angle) * amplitude);
    }

    private static float EaseInOutCubic(float value)
    {
        value = MathHelper.Clamp(value, 0f, 1f);
        return value < 0.5f
            ? 4f * value * value * value
            : 1f - MathF.Pow(-2f * value + 2f, 3f) * 0.5f;
    }
}
