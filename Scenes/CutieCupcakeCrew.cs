using System.Collections.Generic;
using GameCore.GameObjects;
using GameCore.Graphics;
using GameCore.Scenes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MLP_RiM.Elements.DevUI;
using MLP_RiM.Elements.Editor;
using Rhythm.Conductor;
using Rhythm.Note;
using Rhythm.Note.Visual;
using TexturePackerMonoGameDefinitions;

public class CutieCupcakeCrew : Scene
{
    private const float DecoScale = 17.5f;
    private const float CharacterScale = 9f;
    private const float CharacterSourceWidth = 64f;
    private const float CharacterSpacingMultiplier = 0.85f;
    private const float CharacterYRatio = 0.5f;
    private const float EntryHiddenCharacterYRatio = 1.0f;
    private const int PlayerLabelScale = 10;
    private const float PlayerLabelVerticalPadding = 20f;
    private const double BopDurationSeconds = 0.12;

    // Deco
    private GameObject _decom2;
    private GameObject _decom1;
    private GameObject _deco1;
    private GameObject _deco2;

    // Characters
    public enum Characters {PINKIE_PIE, SWEETIE_BELLE, SCOOTALOO, APPLEBLOOM}
    public enum CharacterPoses {IDLE, BOP, PRESS, FAIL}
    public enum CupcakeStates {EMPTY, FROSTED, PERSONAL_TOUCH}
    private GameObject pinkiePie;
    private GameObject sweetieBelle;
    private GameObject scootaloo;
    private GameObject appleBloom;
    private readonly Characters[] _entryCharacters =
    {
        Characters.PINKIE_PIE,
        Characters.SWEETIE_BELLE,
        Characters.SCOOTALOO,
        Characters.APPLEBLOOM
    };
    private readonly Dictionary<Characters, CharacterPoses> _characterPoses = new Dictionary<Characters, CharacterPoses>();
    private VisualRuntime _visualRuntime;
    private VisualNoteManager<CutieCupcakeCrewVisualNote> _cupcakeVisualNoteManager;
    private Conductor _beatConductor;
    private DevUiRenderer _devUIRenderer;
    private double _bopTimer;

    // Characters Sprite
    private Dictionary<Characters, Dictionary<CharacterPoses, string>> sprites = new Dictionary<Characters, Dictionary<CharacterPoses, string>>
    {
        [Characters.PINKIE_PIE] = new Dictionary<CharacterPoses, string>
        {
            [CharacterPoses.IDLE] = MainAtlas.Cutie_cupcake_crew_pinkie_pie1,
            [CharacterPoses.BOP] = MainAtlas.Cutie_cupcake_crew_pinkie_pie2,
            [CharacterPoses.PRESS] = MainAtlas.Cutie_cupcake_crew_pinkie_pie3,
        },
        [Characters.SWEETIE_BELLE] = new Dictionary<CharacterPoses, string>
        {
            [CharacterPoses.IDLE] = MainAtlas.Cutie_cupcake_crew_sweetie_belle1,
            [CharacterPoses.BOP] = MainAtlas.Cutie_cupcake_crew_sweetie_belle2,
            [CharacterPoses.PRESS] = MainAtlas.Cutie_cupcake_crew_sweetie_belle3,
        },
        [Characters.SCOOTALOO] = new Dictionary<CharacterPoses, string>
        {
            [CharacterPoses.IDLE] = MainAtlas.Cutie_cupcake_crew_scootaloo1,
            [CharacterPoses.BOP] = MainAtlas.Cutie_cupcake_crew_scootaloo2,
            [CharacterPoses.PRESS] = MainAtlas.Cutie_cupcake_crew_scootaloo3,
        },
        [Characters.APPLEBLOOM] = new Dictionary<CharacterPoses, string>
        {
            [CharacterPoses.IDLE] = MainAtlas.Cutie_cupcake_crew_applebloom1,
            [CharacterPoses.BOP] = MainAtlas.Cutie_cupcake_crew_applebloom2,
            [CharacterPoses.PRESS] = MainAtlas.Cutie_cupcake_crew_applebloom3,
            [CharacterPoses.FAIL] = MainAtlas.Cutie_cupcake_crew_applebloom4,
        }
    };

    // Cupcakes
    private Dictionary<Characters, Dictionary<CupcakeStates, string>> cupcake_sprites = new Dictionary<Characters, Dictionary<CupcakeStates, string>>
    {
        [Characters.PINKIE_PIE] = new Dictionary<CupcakeStates, string>
        {
            [CupcakeStates.EMPTY] = MainAtlas.Cutie_cupcake_crew_cupcake_pinkie_pie1,
            [CupcakeStates.FROSTED] = MainAtlas.Cutie_cupcake_crew_cupcake_pinkie_pie2,
            [CupcakeStates.PERSONAL_TOUCH] = MainAtlas.Cutie_cupcake_crew_cupcake_pinkie_pie3,
        },
        [Characters.SWEETIE_BELLE] = new Dictionary<CupcakeStates, string>
        {
            [CupcakeStates.EMPTY] = MainAtlas.Cutie_cupcake_crew_cupcake_sweetie_belle1,
            [CupcakeStates.FROSTED] = MainAtlas.Cutie_cupcake_crew_cupcake_sweetie_belle2,
            [CupcakeStates.PERSONAL_TOUCH] = MainAtlas.Cutie_cupcake_crew_cupcake_sweetie_belle3,
        },
        [Characters.SCOOTALOO] = new Dictionary<CupcakeStates, string>
        {
            [CupcakeStates.EMPTY] = MainAtlas.Cutie_cupcake_crew_cupcake_scootaloo1,
            [CupcakeStates.FROSTED] = MainAtlas.Cutie_cupcake_crew_cupcake_scootaloo2,
            [CupcakeStates.PERSONAL_TOUCH] = MainAtlas.Cutie_cupcake_crew_cupcake_scootaloo3,
        },
        [Characters.APPLEBLOOM] = new Dictionary<CupcakeStates, string>
        {
            [CupcakeStates.EMPTY] = MainAtlas.Cutie_cupcake_crew_cupcake_applebloom1,
            [CupcakeStates.FROSTED] = MainAtlas.Cutie_cupcake_crew_cupcake_applebloom2,
            [CupcakeStates.PERSONAL_TOUCH] = MainAtlas.Cutie_cupcake_crew_cupcake_apple_bloom3,
        }
    };

    public CutieCupcakeCrew() : base("Cutie Cupcake Crew")
    {
    }

    public override void OnLoad()
    {
        Viewport vp = GLOBALS.graphicsDevice.Viewport;
        TextureAtlas atlas = GLOBALS.main_atlas;
        _devUIRenderer = new DevUiRenderer(GLOBALS.graphicsDevice);

        _decom2 = CreateFullscreenDeco(atlas, MainAtlas.Deco_m2, vp);
        _decom1 = CreateFullscreenDeco(atlas, MainAtlas.Deco_m1, vp);
        _deco1 = CreateFullscreenDeco(atlas, MainAtlas.Deco1, vp);
        _deco2 = CreateFullscreenDeco(atlas, MainAtlas.Deco2, vp);

        GameObjects.Add(_decom2);
        GameObjects.Add(_decom1);
        GameObjects.Add(_deco1);

        pinkiePie = CreateCharacter(atlas, Characters.PINKIE_PIE, 0, vp);
        sweetieBelle = CreateCharacter(atlas, Characters.SWEETIE_BELLE, 1, vp);
        scootaloo = CreateCharacter(atlas, Characters.SCOOTALOO, 2, vp);
        appleBloom = CreateCharacter(atlas, Characters.APPLEBLOOM, 3, vp);

        GameObjects.Add(pinkiePie);
        GameObjects.Add(sweetieBelle);
        GameObjects.Add(scootaloo);
        GameObjects.Add(appleBloom);

        GameObjects.Add(_deco2);

        SetupVisualNotes();
        GLOBALS.beatmapPlayer.BeatmapStarted += SetupVisualNotes;
    }

    private static GameObject CreateFullscreenDeco(TextureAtlas atlas, string regionName, Viewport viewport)
    {
        GameObject deco = new GameObject(atlas.CreateSprite(regionName));
        deco.sprite.CenterOrigin();
        deco.Position = new Vector2(viewport.Width / 2f, viewport.Height / 2f);
        deco.Scale = Vector2.One * DecoScale;
        return deco;
    }

    private GameObject CreateCharacter(TextureAtlas atlas, Characters character, int index, Viewport viewport)
    {
        GameObject gameObject = new GameObject(atlas.CreateSprite(sprites[character][CharacterPoses.IDLE]));
        gameObject.sprite.CenterOrigin();
        gameObject.Position = GetCharacterPosition(index, viewport);
        gameObject.Scale = Vector2.One * CharacterScale;
        _characterPoses[character] = CharacterPoses.IDLE;
        return gameObject;
    }

    private static Vector2 GetCharacterPosition(int index, Viewport viewport)
    {
        float centerX = viewport.Width / 2f;
        float spacing = CharacterSourceWidth * CharacterScale * CharacterSpacingMultiplier;
        return new Vector2(centerX + (index - 1.5f) * spacing, viewport.Height * CharacterYRatio);
    }

    public override void OnUnload()
    {
        GLOBALS.beatmapPlayer.BeatmapStarted -= SetupVisualNotes;
        UnsubscribeBeatChanged();
        _visualRuntime?.ClearDrivers();
        _visualRuntime = null;
        _cupcakeVisualNoteManager = null;
    }

    public static string GetCharacterTrackId(Characters character)
    {
        return $"cutie_cupcake_crew.character.{character}";
    }

    public GameObject GetCharacter(Characters character)
    {
        return character switch
        {
            Characters.SWEETIE_BELLE => sweetieBelle,
            Characters.SCOOTALOO => scootaloo,
            Characters.APPLEBLOOM => appleBloom,
            _ => pinkiePie
        };
    }

    public Vector2 GetCupcakeTargetPosition(Characters character)
    {
        Viewport viewport = GLOBALS.graphicsDevice.Viewport;
        return new Vector2(GetCharacter(character).Position.X, viewport.Height * 0.5741f);
    }

    public string GetCupcakeSprite(Characters character, CupcakeStates state)
    {
        return cupcake_sprites[character][state];
    }

    public void ApplyCharacterPose(GameObject characterObject, Characters character, CharacterPoses pose)
    {
        if(characterObject == null)
            return;

        if(_characterPoses.TryGetValue(character, out CharacterPoses currentPose) && currentPose == pose)
            return;

        characterObject.sprite = GLOBALS.main_atlas.CreateSprite(sprites[character][pose]);
        characterObject.sprite.CenterOrigin();
        _characterPoses[character] = pose;
    }

    public void ResetCharacterPoses()
    {
        CharacterPoses ambientPose = GetAmbientPose();
        ApplyCharacterPose(pinkiePie, Characters.PINKIE_PIE, ambientPose);
        ApplyCharacterPose(sweetieBelle, Characters.SWEETIE_BELLE, ambientPose);
        ApplyCharacterPose(scootaloo, Characters.SCOOTALOO, ambientPose);
        ApplyCharacterPose(appleBloom, Characters.APPLEBLOOM, ambientPose);
    }

    public CharacterPoses GetAmbientPose()
    {
        return _bopTimer > 0.0 ? CharacterPoses.BOP : CharacterPoses.IDLE;
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);
        UpdateBopTimer(gameTime);

        if(GLOBALS.beatmapPlayer.Conductor == null || GLOBALS.beatmapPlayer.ChartPlayer == null)
        {
            ResetCharacterPoses();
            return;
        }

        double songPosition = GLOBALS.beatmapPlayer.Conductor.SongPosition;
        ApplyEntryClipPositions(songPosition);
        IReadOnlyList<Note> notes = GLOBALS.beatmapPlayer.ChartPlayer.Notes;
        _visualRuntime?.ResolveDrivers(songPosition, notes);
        if(FindCurrentCupcakeDriver(songPosition, notes) == null)
            ResetCharacterPoses();

        _cupcakeVisualNoteManager?.Update(songPosition);
    }


    public override void Draw(SpriteBatch spriteBatch)
    {
        GLOBALS.graphicsDevice.Clear(Color.HotPink);

        foreach(GameObject gameObject in GameObjects)
        {
            if(ReferenceEquals(gameObject, _deco2))
                _cupcakeVisualNoteManager?.Draw(spriteBatch);

            gameObject.Draw(spriteBatch);
        }

        DrawPlayerLabel(spriteBatch);
        DrawDebugOverlay(spriteBatch);
    }

    private void DrawPlayerLabel(SpriteBatch spriteBatch)
    {
        if(_devUIRenderer == null || appleBloom == null)
            return;

        const string label = "You";
        float labelWidth = label.Length * 4f * PlayerLabelScale;
        Vector2 position = appleBloom.Position + new Vector2(
            -labelWidth * 0.5f,
            -CharacterSourceWidth * CharacterScale * 0.5f - PlayerLabelVerticalPadding);
        _devUIRenderer.Label(spriteBatch, label, position, Color.White, PlayerLabelScale);
    }

    private void SetupVisualNotes()
    {
        _visualRuntime = new VisualRuntime();
        RegisterCharacterTrack(Characters.PINKIE_PIE);
        RegisterCharacterTrack(Characters.SWEETIE_BELLE);
        RegisterCharacterTrack(Characters.SCOOTALOO);
        RegisterCharacterTrack(Characters.APPLEBLOOM);

        if(GLOBALS.beatmapPlayer.ChartPlayer == null)
        {
            _cupcakeVisualNoteManager = null;
            return;
        }

        _cupcakeVisualNoteManager = new VisualNoteManager<CutieCupcakeCrewVisualNote>(GLOBALS.beatmapPlayer.ChartPlayer, CreateCupcakeVisualNote)
        {
            LookAheadSeconds = GLOBALS.beatmapPlayer.GetMaxCrotchet() * 112.0,
            LookBehindSeconds = GLOBALS.beatmapPlayer.GetMaxCrotchet() * 48.0
        };

        SubscribeBeatChanged();
    }

    private void SubscribeBeatChanged()
    {
        Conductor conductor = GLOBALS.beatmapPlayer.Conductor;
        if(ReferenceEquals(_beatConductor, conductor))
            return;

        UnsubscribeBeatChanged();
        _beatConductor = conductor;
        if(_beatConductor != null)
            _beatConductor.BeatChanged += OnBeatChanged;
    }

    private void UnsubscribeBeatChanged()
    {
        if(_beatConductor != null)
            _beatConductor.BeatChanged -= OnBeatChanged;

        _beatConductor = null;
    }

    private void OnBeatChanged(object sender, BeatChangedEventArgs e)
    {
        _bopTimer = BopDurationSeconds;
        ResetCharacterPoses();
    }

    private void UpdateBopTimer(GameTime gameTime)
    {
        if(_bopTimer <= 0.0)
            return;

        _bopTimer = System.Math.Max(0.0, _bopTimer - gameTime.ElapsedGameTime.TotalSeconds);
    }

    private void RegisterCharacterTrack(Characters character)
    {
        _visualRuntime.RegisterTrack(GetCharacterTrackId(character), GetCharacter(character))
            .UseDriverResolver(ctx => FindCurrentCupcakeDriver(ctx.SongPosition, ctx.Notes));
    }

    private void ApplyEntryClipPositions(double songPosition)
    {
        double beat = GLOBALS.beatmapPlayer.GetBeatAt(songPosition);
        ChartEditorClip entryClip = FindActiveEntryClip(beat);

        for(int i = 0; i < _entryCharacters.Length; i++)
        {
            Characters character = _entryCharacters[i];
            GameObject gameObject = GetCharacter(character);
            if(gameObject == null)
                continue;

            Vector2 target = GetCharacterPosition(i, GLOBALS.graphicsDevice.Viewport);
            gameObject.Position = entryClip == null
                ? target
                : GetEntryClipPosition(target, entryClip, beat, i);
        }
    }

    private ChartEditorClip FindActiveEntryClip(double beat)
    {
        ChartEditorClip activeClip = null;
        double activeStartBeat = double.NegativeInfinity;
        IReadOnlyList<ChartEditorClip> clips = GLOBALS.beatmapPlayer.CurrentChart?.EditorClips;
        if(clips == null)
            return null;

        foreach(ChartEditorClip clip in clips)
        {
            if(clip?.ClipTypeId != CutieCupcakeCrewProvider.EntryClipId)
                continue;

            double start = clip.StartBeat;
            double end = start + System.Math.Max(0.001, clip.LengthBeats);
            if(beat < start || beat > end || start < activeStartBeat)
                continue;

            activeClip = clip;
            activeStartBeat = start;
        }

        return activeClip;
    }

    private static Vector2 GetEntryClipPosition(Vector2 target, ChartEditorClip clip, double beat, int characterIndex)
    {
        Viewport viewport = GLOBALS.graphicsDevice.Viewport;
        double clipStart = clip.StartBeat;
        double clipEnd = clip.StartBeat + System.Math.Max(0.001, clip.LengthBeats);
        double startOffsetBeats = System.Math.Max(1.0, 4.0 - characterIndex);
        double characterStart = System.Math.Max(clipStart, clipEnd - startOffsetBeats);
        double duration = System.Math.Max(0.001, clipEnd - characterStart);
        float progress = (float)System.Math.Clamp((beat - characterStart) / duration, 0.0, 1.0);
        Vector2 hidden = new(target.X, viewport.Height * EntryHiddenCharacterYRatio + CharacterSourceWidth * CharacterScale);
        return Vector2.Lerp(hidden, target, EaseOutCubic(progress));
    }

    private static float EaseOutCubic(float value)
    {
        value = MathHelper.Clamp(value, 0f, 1f);
        float inverse = 1f - value;
        return 1f - inverse * inverse * inverse;
    }

    private CutieCupcakeCrewVisualNote CreateCupcakeVisualNote(Note note)
    {
        if(!IsCupcakeVisualDriverNote(note))
            return null;

        string sourceClipId = CutieCupcakeCrewProvider.GetSourceClipId(note.AdditionnalData);
        double spacingBeats = CutieCupcakeCrewProvider.GetBeatSpacing(note.AdditionnalData);
        double spacingSeconds = spacingBeats * GetCrotchetAt(note);
        Note secondHit = sourceClipId == CutieCupcakeCrewProvider.PersonalTouchClipId
            ? FindPersonalTouchSecondHit(note, spacingSeconds)
            : null;

        return new CutieCupcakeCrewVisualNote(this, note, secondHit, _visualRuntime, sourceClipId, spacingSeconds);
    }

    private Note FindCurrentCupcakeDriver(double songPosition, IReadOnlyList<Note> notes)
    {
        Note driver = null;
        double driverStart = double.NegativeInfinity;

        foreach(Note note in notes ?? System.Array.Empty<Note>())
        {
            if(!IsCupcakeVisualDriverNote(note))
                continue;

            string sourceClipId = CutieCupcakeCrewProvider.GetSourceClipId(note.AdditionnalData);
            double spacingSeconds = CutieCupcakeCrewProvider.GetBeatSpacing(note.AdditionnalData) * GetCrotchetAt(note);
            double start = note.SongPosition - GetApproachBeats(sourceClipId) * spacingSeconds;
            double end = note.SongPosition + GetPostHitBeats(sourceClipId) * spacingSeconds;
            if(songPosition < start || songPosition > end || start < driverStart)
                continue;

            driver = note;
            driverStart = start;
        }

        return driver;
    }

    private Note FindPersonalTouchSecondHit(Note firstHit, double spacingSeconds)
    {
        foreach(Note note in GLOBALS.beatmapPlayer.ChartPlayer?.Notes ?? System.Array.Empty<Note>())
        {
            if(note.SongPosition <= firstHit.SongPosition)
                continue;

            if(note.SongPosition > firstHit.SongPosition + spacingSeconds * 1.5)
                return null;

            if(CutieCupcakeCrewProvider.GetSourceClipId(note.AdditionnalData) == CutieCupcakeCrewProvider.PersonalTouchClipId
                && CutieCupcakeCrewNoteCodec.IsAction(note.AdditionnalData, CutieCupcakeCrewAction.AppleBloomPersonalTouchHit))
                return note;
        }

        return null;
    }

    private double GetCrotchetAt(Note note)
    {
        return GLOBALS.beatmapPlayer.GetCrotchetAt(note?.SongPosition ?? 0.0);
    }

    private static bool IsCupcakeVisualDriverNote(Note note)
    {
        string sourceClipId = CutieCupcakeCrewProvider.GetSourceClipId(note?.AdditionnalData);
        return (sourceClipId == CutieCupcakeCrewProvider.FrostClipId
                || sourceClipId == CutieCupcakeCrewProvider.TogetherFrostClipId
                || sourceClipId == CutieCupcakeCrewProvider.PersonalTouchClipId)
            && CutieCupcakeCrewNoteCodec.IsAction(note?.AdditionnalData, CutieCupcakeCrewAction.AppleBloomFrostHit);
    }

    private static double GetApproachBeats(string sourceClipId)
    {
        if(sourceClipId == CutieCupcakeCrewProvider.TogetherFrostClipId)
            return 3.0;

        return sourceClipId == CutieCupcakeCrewProvider.PersonalTouchClipId ? 7.0 : 4.0;
    }

    private static double GetPostHitBeats(string sourceClipId)
    {
        return sourceClipId == CutieCupcakeCrewProvider.PersonalTouchClipId ? 3.0 : 2.0;
    }
}
