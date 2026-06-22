using System;
using System.Runtime.CompilerServices;
using GameCore.Audio;
using GameCore.GameObjects;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MLP_RiM.Elements.Editor;
using Rhythm.Note;

public sealed class CutieCupcakeCrewVisualNote : DirectedVisualNote
{
    private const float CupcakeScale = 6.5f;
    private const float CupcakeSourceWidth = 64f;
    private const float PressDurationBeats = 0.45f;
    private const float SfxVolume = 1f;
    private const float PressSfxVolume = 0.8f;
    private const float PersonalTouchSfxVolume = 0.5f;
    private const float TogetherSfxVolume = 1f;
    private const string FrostSfx = "SFX/CutieCupcakeCrew/Frost.wav";
    private const string PersonalTouchSfx = "SFX/CutieCupcakeCrew/Personal_Touch.wav";
    private const string PressSfx = "SFX/CutieCupcakeCrew/Press.wav";
    private const string Together1Sfx = "SFX/CutieCupcakeCrew/Together1.wav";
    private const string Together2Sfx = "SFX/CutieCupcakeCrew/Together2.wav";
    private const string Together3Sfx = "SFX/CutieCupcakeCrew/Together3.wav";

    private readonly CutieCupcakeCrew _scene;
    private readonly string _sourceClipId;
    private readonly double _spacingSeconds;
    private readonly Note _firstHit;
    private readonly Note _secondHit;
    private readonly GameObject[] _cupcakes;
    private readonly CutieCupcakeCrew.CupcakeStates[] _cupcakeStates;
    private readonly string[] _cupcakeTrackIds;
    private readonly string[] _ownedTrackIds;
    private bool _clipStartSfxPlayed;
    private bool _firstHitSfxPlayed;
    private bool _secondHitSfxPlayed;

    public CutieCupcakeCrewVisualNote(CutieCupcakeCrew scene, Note note, Note secondHit, VisualRuntime runtime, string sourceClipId, double spacingSeconds)
        : base(note, runtime, GetApproachBeats(sourceClipId) * spacingSeconds, GetPostHitBeats(sourceClipId) * spacingSeconds)
    {
        _scene = scene;
        _sourceClipId = sourceClipId;
        _spacingSeconds = Math.Max(0.000001, spacingSeconds);
        _firstHit = note;
        _secondHit = secondHit;
        _cupcakes = new GameObject[4];
        _cupcakeStates = new CutieCupcakeCrew.CupcakeStates[4];
        _cupcakeTrackIds = new string[4];

        for(int i = 0; i < _cupcakes.Length; i++)
        {
            CutieCupcakeCrew.Characters character = GetCharacter(i);
            _cupcakes[i] = new GameObject(null)
            {
                Scale = Vector2.One * CupcakeScale,
                Position = GetLeftPosition(character)
            };
            SetCupcakeState(i, _cupcakes[i], CutieCupcakeCrew.CupcakeStates.EMPTY);

            _cupcakeTrackIds[i] = $"cutie_cupcake_crew.cupcake.{RuntimeHelpers.GetHashCode(this)}.{i}";
            runtime.RegisterTrack(_cupcakeTrackIds[i], _cupcakes[i]);
            runtime.SetDriver(_cupcakeTrackIds[i], note);
        }

        _ownedTrackIds = new[]
        {
            CutieCupcakeCrew.GetCharacterTrackId(CutieCupcakeCrew.Characters.PINKIE_PIE),
            CutieCupcakeCrew.GetCharacterTrackId(CutieCupcakeCrew.Characters.SWEETIE_BELLE),
            CutieCupcakeCrew.GetCharacterTrackId(CutieCupcakeCrew.Characters.SCOOTALOO),
            CutieCupcakeCrew.GetCharacterTrackId(CutieCupcakeCrew.Characters.APPLEBLOOM),
            _cupcakeTrackIds[0],
            _cupcakeTrackIds[1],
            _cupcakeTrackIds[2],
            _cupcakeTrackIds[3]
        };
    }

    protected override void Build(VisualTimeline timeline)
    {
        timeline.StableBefore("cutie_cupcake_crew_before")
            .Owns(_ownedTrackIds)
            .Do(Apply);

        timeline.DuringApproach("cutie_cupcake_crew_approach")
            .Owns(_ownedTrackIds)
            .Do((ctx, phase) => Apply(ctx));

        timeline.AfterHitUntilDespawn("cutie_cupcake_crew_after_hit")
            .Owns(_ownedTrackIds)
            .Do((ctx, phase) => Apply(ctx));

        timeline.StableAfter("cutie_cupcake_crew_after")
            .Owns(_ownedTrackIds)
            .Do(Apply);
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        if(!ShouldDraw)
            return;

        foreach(GameObject cupcake in _cupcakes)
            cupcake.Draw(spriteBatch);
    }

    private void Apply(VisualContext context)
    {
        ResetHitSfxOnRewind(context);
        double localBeat = GetLocalBeat(context.SongPosition);
        PlayClipStartSfx(context, localBeat);
        PlayTogetherCueSfx(context);
        PlayAutomaticPressSfx(context);
        PlayPlayerHitSfx();
        ApplyCupcakes(context, localBeat);
        ApplyCharacters(context, localBeat);
    }

    private void PlayClipStartSfx(VisualContext context, double localBeat)
    {
        if(context.HasRewound || localBeat < 0.0)
            _clipStartSfxPlayed = false;

        if(_clipStartSfxPlayed)
            return;

        double startSongPosition = GetSongPositionAtLocalBeat(0.0);
        bool crossedStart = context.ForwardCrossed("clip_start_sfx", startSongPosition);
        bool firstSampleAtStart = double.IsNaN(context.LastSongPosition) && localBeat >= 0.0 && localBeat < 0.25;
        if(!crossedStart && !firstSampleAtStart)
            return;

        _clipStartSfxPlayed = true;
        string path = IsTogetherFrost ? Together1Sfx : IsPersonalTouch ? PersonalTouchSfx : FrostSfx;
        float volume = IsTogetherFrost ? TogetherSfxVolume : IsPersonalTouch ? PersonalTouchSfxVolume : SfxVolume;
        SFX.Play(_scene, path, volume);
    }

    private void PlayTogetherCueSfx(VisualContext context)
    {
        if(!IsTogetherFrost)
            return;

        PlaySfxAtBeat(context, "together_2", 1.0, Together2Sfx, TogetherSfxVolume);
        PlaySfxAtBeat(context, "together_3", 2.0, Together3Sfx, TogetherSfxVolume);
    }

    private void PlayAutomaticPressSfx(VisualContext context)
    {
        if(IsTogetherFrost)
            return;

        if(IsPersonalTouch)
        {
            PlaySfxAtBeat(context, "pinkie_press_1", 1.0, PressSfx, PressSfxVolume);
            PlaySfxAtBeat(context, "pinkie_press_2", 2.0, PressSfx, PressSfxVolume);
            PlaySfxAtBeat(context, "sweetie_press_1", 3.0, PressSfx, PressSfxVolume);
            PlaySfxAtBeat(context, "sweetie_press_2", 4.0, PressSfx, PressSfxVolume);
            PlaySfxAtBeat(context, "scootaloo_press_1", 5.0, PressSfx, PressSfxVolume);
            PlaySfxAtBeat(context, "scootaloo_press_2", 6.0, PressSfx, PressSfxVolume);
            return;
        }

        PlaySfxAtBeat(context, "pinkie_press", 1.0, PressSfx, PressSfxVolume);
        PlaySfxAtBeat(context, "sweetie_press", 2.0, PressSfx, PressSfxVolume);
        PlaySfxAtBeat(context, "scootaloo_press", 3.0, PressSfx, PressSfxVolume);
    }

    private void PlayPlayerHitSfx()
    {
        if(!_firstHitSfxPlayed && IsSuccessful(_firstHit))
        {
            _firstHitSfxPlayed = true;
            SFX.Play(_scene, PressSfx, PressSfxVolume);
        }

        if(!_secondHitSfxPlayed && IsSuccessful(_secondHit))
        {
            _secondHitSfxPlayed = true;
            SFX.Play(_scene, PressSfx, PressSfxVolume);
        }
    }

    private void ResetHitSfxOnRewind(VisualContext context)
    {
        if(!_firstHit.HasReacted || context.HasRewound && context.SongPosition < _firstHit.SongPosition)
            _firstHitSfxPlayed = false;

        if(_secondHit != null && (!_secondHit.HasReacted || context.HasRewound && context.SongPosition < _secondHit.SongPosition))
            _secondHitSfxPlayed = false;
    }

    private void PlaySfxAtBeat(VisualContext context, string eventId, double localBeat, string path)
    {
        PlaySfxAtBeat(context, eventId, localBeat, path, SfxVolume);
    }

    private void PlaySfxAtBeat(VisualContext context, string eventId, double localBeat, string path, float volume)
    {
        context.PlaySfxOnForwardCross(eventId, GetSongPositionAtLocalBeat(localBeat), _scene, path, volume);
    }

    private void ApplyCupcakes(VisualContext context, double localBeat)
    {
        for(int i = 0; i < _cupcakes.Length; i++)
        {
            CutieCupcakeCrew.Characters character = GetCharacter(i);
            CutieCupcakeCrew.CupcakeStates state = GetCupcakeState(character, localBeat);
            int cupcakeIndex = i;
            context.Mutate<GameObject>(_cupcakeTrackIds[cupcakeIndex], cupcake =>
            {
                SetCupcakeState(cupcakeIndex, cupcake, state);
                cupcake.Position = GetCupcakePosition(character, localBeat);
            });
        }
    }

    private void ApplyCharacters(VisualContext context, double localBeat)
    {
        if(IsTogetherFrost)
        {
            ApplyCharacterPose(context, CutieCupcakeCrew.Characters.PINKIE_PIE, GetAutomaticPose(localBeat, 3.0));
            ApplyCharacterPose(context, CutieCupcakeCrew.Characters.SWEETIE_BELLE, GetAutomaticPose(localBeat, 3.0));
            ApplyCharacterPose(context, CutieCupcakeCrew.Characters.SCOOTALOO, GetAutomaticPose(localBeat, 3.0));
            ApplyCharacterPose(context, CutieCupcakeCrew.Characters.APPLEBLOOM, GetAppleBloomPose(localBeat, 3.0, _firstHit));
            return;
        }

        ApplyCharacterPose(context, CutieCupcakeCrew.Characters.PINKIE_PIE, GetAutomaticPose(localBeat, 1.0));

        if(IsPersonalTouch)
        {
            ApplyCharacterPose(context, CutieCupcakeCrew.Characters.SWEETIE_BELLE, GetAutomaticPose(localBeat, 3.0));
            ApplyCharacterPose(context, CutieCupcakeCrew.Characters.SCOOTALOO, GetAutomaticPose(localBeat, 5.0));
            ApplyCharacterPose(context, CutieCupcakeCrew.Characters.APPLEBLOOM, GetAppleBloomPose(localBeat, 7.0, _firstHit, 8.0, _secondHit));
            return;
        }

        ApplyCharacterPose(context, CutieCupcakeCrew.Characters.SWEETIE_BELLE, GetAutomaticPose(localBeat, 2.0));
        ApplyCharacterPose(context, CutieCupcakeCrew.Characters.SCOOTALOO, GetAutomaticPose(localBeat, 3.0));
        ApplyCharacterPose(context, CutieCupcakeCrew.Characters.APPLEBLOOM, GetAppleBloomPose(localBeat, 4.0, _firstHit));
    }

    private void ApplyCharacterPose(VisualContext context, CutieCupcakeCrew.Characters character, CutieCupcakeCrew.CharacterPoses pose)
    {
        string trackId = CutieCupcakeCrew.GetCharacterTrackId(character);
        context.Mutate<GameObject>(trackId, gameObject => _scene.ApplyCharacterPose(gameObject, character, pose));
    }

    private CutieCupcakeCrew.CupcakeStates GetCupcakeState(CutieCupcakeCrew.Characters character, double localBeat)
    {
        return character switch
        {
            CutieCupcakeCrew.Characters.PINKIE_PIE when IsTogetherFrost => GetAutoCupcakeState(localBeat, 3.0, null),
            CutieCupcakeCrew.Characters.SWEETIE_BELLE when IsTogetherFrost => GetAutoCupcakeState(localBeat, 3.0, null),
            CutieCupcakeCrew.Characters.SCOOTALOO when IsTogetherFrost => GetAutoCupcakeState(localBeat, 3.0, null),
            CutieCupcakeCrew.Characters.APPLEBLOOM when IsTogetherFrost => GetPlayerCupcakeState(localBeat, 3.0, _firstHit, null, null),
            CutieCupcakeCrew.Characters.PINKIE_PIE => GetAutoCupcakeState(localBeat, 1.0, IsPersonalTouch ? 2.0 : null),
            CutieCupcakeCrew.Characters.SWEETIE_BELLE => IsPersonalTouch ? GetAutoCupcakeState(localBeat, 3.0, 4.0) : GetAutoCupcakeState(localBeat, 2.0, null),
            CutieCupcakeCrew.Characters.SCOOTALOO => IsPersonalTouch ? GetAutoCupcakeState(localBeat, 5.0, 6.0) : GetAutoCupcakeState(localBeat, 3.0, null),
            CutieCupcakeCrew.Characters.APPLEBLOOM => IsPersonalTouch ? GetPlayerCupcakeState(localBeat, 7.0, _firstHit, 8.0, _secondHit) : GetPlayerCupcakeState(localBeat, 4.0, _firstHit, null, null),
            _ => CutieCupcakeCrew.CupcakeStates.EMPTY
        };
    }

    private CutieCupcakeCrew.CupcakeStates GetAutoCupcakeState(double localBeat, double frostBeat, double? personalTouchBeat)
    {
        if(personalTouchBeat.HasValue && localBeat >= personalTouchBeat.Value)
            return CutieCupcakeCrew.CupcakeStates.PERSONAL_TOUCH;

        return localBeat >= frostBeat
            ? CutieCupcakeCrew.CupcakeStates.FROSTED
            : CutieCupcakeCrew.CupcakeStates.EMPTY;
    }

    private CutieCupcakeCrew.CupcakeStates GetPlayerCupcakeState(double localBeat, double frostBeat, Note frostNote, double? personalTouchBeat, Note personalTouchNote)
    {
        if(personalTouchBeat.HasValue && localBeat >= personalTouchBeat.Value && IsSuccessful(personalTouchNote))
            return CutieCupcakeCrew.CupcakeStates.PERSONAL_TOUCH;

        return localBeat >= frostBeat && IsSuccessful(frostNote)
            ? CutieCupcakeCrew.CupcakeStates.FROSTED
            : CutieCupcakeCrew.CupcakeStates.EMPTY;
    }

    private CutieCupcakeCrew.CharacterPoses GetAutomaticPose(double localBeat, double firstBeat)
    {
        if(IsBeatActive(localBeat, firstBeat) || IsPersonalTouch && IsBeatActive(localBeat, firstBeat + 1.0))
            return CutieCupcakeCrew.CharacterPoses.PRESS;

        return _scene.GetAmbientPose();
    }

    private CutieCupcakeCrew.CharacterPoses GetAppleBloomPose(double localBeat, double firstBeat, Note firstNote, double? secondBeat = null, Note secondNote = null)
    {
        if(IsInDespawnDelay(localBeat) && (IsMissed(firstNote) || IsMissed(secondNote)))
            return CutieCupcakeCrew.CharacterPoses.FAIL;

        if(secondBeat.HasValue && IsBeatActive(localBeat, secondBeat.Value))
            return GetPlayerPose(secondNote);

        if(IsBeatActive(localBeat, firstBeat))
            return GetPlayerPose(firstNote);

        return _scene.GetAmbientPose();
    }

    private CutieCupcakeCrew.CharacterPoses GetPlayerPose(Note note)
    {
        if(IsSuccessful(note))
            return CutieCupcakeCrew.CharacterPoses.PRESS;

        if(note is { HasBeenMissed: true })
            return CutieCupcakeCrew.CharacterPoses.FAIL;

        return _scene.GetAmbientPose();
    }

    private Vector2 GetCupcakePosition(CutieCupcakeCrew.Characters character, double localBeat)
    {
        Vector2 target = _scene.GetCupcakeTargetPosition(character);
        if(localBeat <= 1.0)
            return Vector2.Lerp(GetLeftPosition(character), target, EaseOutCubic((float)localBeat));

        double departStartBeat = GetDepartStartBeat(_sourceClipId);
        if(localBeat < departStartBeat)
            return target;

        float departProgress = (float)((localBeat - departStartBeat) / 1.0);
        return Vector2.Lerp(target, GetRightPosition(character), EaseInCubic(departProgress));
    }

    private Vector2 GetLeftPosition(CutieCupcakeCrew.Characters character)
    {
        Viewport viewport = GLOBALS.graphicsDevice.Viewport;
        Vector2 target = _scene.GetCupcakeTargetPosition(character);
        return new Vector2(target.X - viewport.Width - GetOffscreenPadding(), target.Y);
    }

    private Vector2 GetRightPosition(CutieCupcakeCrew.Characters character)
    {
        Viewport viewport = GLOBALS.graphicsDevice.Viewport;
        Vector2 target = _scene.GetCupcakeTargetPosition(character);
        return new Vector2(target.X + viewport.Width + GetOffscreenPadding(), target.Y);
    }

    private void SetCupcakeState(int index, GameObject cupcake, CutieCupcakeCrew.CupcakeStates state)
    {
        if(cupcake.sprite != null && _cupcakeStates[index] == state)
            return;

        CutieCupcakeCrew.Characters character = GetCharacter(index);
        cupcake.sprite = GLOBALS.main_atlas.CreateSprite(_scene.GetCupcakeSprite(character, state));
        cupcake.sprite.CenterOrigin();
        _cupcakeStates[index] = state;
    }

    private double GetLocalBeat(double songPosition)
    {
        return GetApproachBeats(_sourceClipId) + (songPosition - Note.SongPosition) / _spacingSeconds;
    }

    private double GetSongPositionAtLocalBeat(double localBeat)
    {
        return Note.SongPosition + (localBeat - GetApproachBeats(_sourceClipId)) * _spacingSeconds;
    }

    private bool IsBeatActive(double localBeat, double beat)
    {
        return localBeat >= beat && localBeat < beat + PressDurationBeats;
    }

    private bool IsInDespawnDelay(double localBeat)
    {
        double departStartBeat = GetDepartStartBeat(_sourceClipId);
        return localBeat >= departStartBeat && localBeat < departStartBeat + 1.0;
    }

    private bool IsPersonalTouch => _sourceClipId == CutieCupcakeCrewProvider.PersonalTouchClipId;

    private bool IsTogetherFrost => _sourceClipId == CutieCupcakeCrewProvider.TogetherFrostClipId;

    private static bool IsSuccessful(Note note)
    {
        return note is { HasReacted: true, HasBeenMissed: false };
    }

    private static bool IsMissed(Note note)
    {
        return note is { HasBeenMissed: true };
    }

    private static float GetOffscreenPadding()
    {
        return CupcakeSourceWidth * CupcakeScale * 0.5f;
    }

    private static CutieCupcakeCrew.Characters GetCharacter(int index)
    {
        return index switch
        {
            1 => CutieCupcakeCrew.Characters.SWEETIE_BELLE,
            2 => CutieCupcakeCrew.Characters.SCOOTALOO,
            3 => CutieCupcakeCrew.Characters.APPLEBLOOM,
            _ => CutieCupcakeCrew.Characters.PINKIE_PIE
        };
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

    private static double GetDepartStartBeat(string sourceClipId)
    {
        if(sourceClipId == CutieCupcakeCrewProvider.TogetherFrostClipId)
            return 4.0;

        return sourceClipId == CutieCupcakeCrewProvider.PersonalTouchClipId ? 9.0 : 5.0;
    }

    private static float EaseOutCubic(float value)
    {
        value = MathHelper.Clamp(value, 0f, 1f);
        float inverse = 1f - value;
        return 1f - inverse * inverse * inverse;
    }

    private static float EaseInCubic(float value)
    {
        value = MathHelper.Clamp(value, 0f, 1f);
        return value * value * value;
    }
}
