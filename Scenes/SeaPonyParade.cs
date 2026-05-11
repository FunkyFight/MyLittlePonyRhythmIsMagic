using System;
using System.Collections.Generic;
using System.Linq;
using GameCore;
using GameCore.Animation;
using GameCore.Audio;
using GameCore.GameObjects;
using GameCore.Graphics;
using GameCore.Scenes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MLP_RiM.Elements.DevUI;
using MLP_RiM.Elements.Editor;
using Rhythm.Note;
using Rhythm.Note.Visual;
using TexturePackerMonoGameDefinitions;

public class SeaPonyParade : Scene
{
    // Config
    private const float SCALE = 4.5f;
    private const float CoralBackgroundScale = 4.5f;
    private const float SandBackgroundScale = 6f;
    private const int SeaPonyCount = 4;
    private static readonly int[] SeaPonyPositionOrder = { 3, 2, 1, 0 };
    private const double FirstCueLeadBeats = 2.0;
    private const double SecondCueLeadBeats = 1.0;
    private const float LeaveExitPadding = 260f;
    private const double IdleJamPoseBeatFraction = 0.5;
    private const float TapTapGroupOffsetX = 64f;
    private const float TapClapEffectScale = 3f;
    private const float TapClapEffectOffsetY = -75f;
    private const double TapClapEffectDuration = 0.22;

    // Objects
    private Viewport _viewport;
    private GameObject[] _seaPonies = new GameObject[SeaPonyCount];
    private Vector2[] _seaPonyBasePositions = new Vector2[SeaPonyCount];
    private AnimationStateMachine[] _seaPoniesAnimationStates = new AnimationStateMachine[SeaPonyCount];
    private int[] _seaPonyIdleBeatStates = Enumerable.Repeat(int.MinValue, SeaPonyCount).ToArray();
    private static readonly Dictionary<string, string>[] _seaPoniesSkins =
        new Dictionary<string, string>[]
        {
            new Dictionary<string, string>
            {
                { "idle", MainAtlas.Pinkiepie_pony_idle1 },
                { "swim_anticipation", MainAtlas.Pinkiepie_pony_swim1 },
                { "swim", MainAtlas.Pinkiepie_pony_swim2 },
                { "roll", MainAtlas.Pinkiepie_pony_star },
                { "uptap", MainAtlas.Pinkiepie_pony_tap1 },
                { "downtap", MainAtlas.Pinkiepie_pony_tap2 },
            },
            new Dictionary<string, string>
            {
                { "idle", MainAtlas.Fluttershy_pony_idle1 },
                { "swim_anticipation", MainAtlas.Fluttershy_pony_swim1 },
                { "swim", MainAtlas.Fluttershy_pony_swim2 },
                { "roll", MainAtlas.Fluttershy_pony_star },
                { "uptap", MainAtlas.Fluttershy_pony_tap1 },
                { "downtap", MainAtlas.Fluttershy_pony_tap2 }
            },
            new Dictionary<string, string>
            {
                { "idle", MainAtlas.Applejack_pony_idle1 },
                { "swim_anticipation", MainAtlas.Applejack_pony_swim1 },
                { "swim", MainAtlas.Applejack_pony_swim2 },
                { "roll", MainAtlas.Applejack_pony_star },
                { "uptap", MainAtlas.Applejack_pony_tap1 },
                { "downtap", MainAtlas.Applejack_pony_tap2 }
            },
            new Dictionary<string, string> {
                { "idle", MainAtlas.Rarity_pony_idle1 },
                { "swim_anticipation", MainAtlas.Rarity_pony_swim1 },
                { "swim", MainAtlas.Rarity_pony_swim2 },
                { "roll", MainAtlas.Rarity_pony_star },
                { "uptap", MainAtlas.Rarity_pony_tap1 },
                { "downtap", MainAtlas.Rarity_pony_tap2 },
            },
        };
    private HashSet<Note> _authorizedRollNotes = new HashSet<Note>();
    private HashSet<Note> _successfulReactionNotes = new HashSet<Note>();
    private HashSet<Note> _perfectReactionNotes = new HashSet<Note>();
    private HashSet<Note> _playerTapClapEffectNotes = new HashSet<Note>();
    private HashSet<Note> _autoTapClapEffectNotes = new HashSet<Note>();
    private List<GameObject> _tapClapEffects = new List<GameObject>();
    private VisualNoteManager<SeaponyVisualNote>[] _seaPoniesVisualNotes = new VisualNoteManager<SeaponyVisualNote>[SeaPonyCount];
    private ChartPlayer _reactionChartPlayer;
    private Note _activeGroupMoveNote;
    private double _lastSeaPonySongPosition = double.NaN;
    private Vector2 _seaPonyGroupOffset = Vector2.Zero;

    private InfiniteScrollBackground _infiniteScrollBg;
    private VisualNoteManager<SeaponyBgVisualNote> _infiniteScrollBgVisualNotes;
    /// <summary>
    /// Runtime partagé par les visual notes dirigées de la scène.
    /// </summary>
    /// <remarks>
    /// Il est recréé avec les managers dans <see cref="createVisualNotes"/> afin de survivre proprement aux
    /// redémarrages de beatmap. Pour le premier incrément il ne contient que la track <c>background</c>.
    /// </remarks>
    private VisualRuntime _visualRuntime;

    private DevUiRenderer _devUIRenderer;

    public SeaPonyParade() : base("Seapony Parade")
    {
    }

    public override void OnLoad()
    {
        _devUIRenderer = new DevUiRenderer(GLOBALS.graphicsDevice);
        _viewport = GLOBALS.graphicsDevice.Viewport;
        createSeaPonies();
        createScrollBg();
        createVisualNotes();
        createReactions();

        GLOBALS.beatmapPlayer.BeatmapStarted += createVisualNotes;
        GLOBALS.beatmapPlayer.BeatmapStarted += createReactions;
    }

    private void createReactions()
    {
        unsubscribeReactionChartPlayer();
        resetSeaPonyTimelineState();

        _reactionChartPlayer = GLOBALS.beatmapPlayer.ChartPlayer;
        if(_reactionChartPlayer != null)
            _reactionChartPlayer.NoteReactedWithNote += handleNoteReacted;
    }

    private void handleNoteReacted(NoteReactionResult result, Note note)
    {
        if(!TryGetSeaponyAction(note, out SeaponyAction action))
            return;

        if(result == NoteReactionResult.MISS)
        {
            _successfulReactionNotes.Remove(note);
            _perfectReactionNotes.Remove(note);
            _authorizedRollNotes.Remove(note);
            _playerTapClapEffectNotes.Remove(note);
            return;
        }

        switch(action)
        {
            case SeaponyAction.Swim:
                _successfulReactionNotes.Add(note);
                break;

            case SeaponyAction.Roll:
                _authorizedRollNotes.Add(note);
                break;

            case SeaponyAction.TapTap:
                _successfulReactionNotes.Add(note);
                spawnPlayerTapClapEffect(note);
                if(result == NoteReactionResult.PERFECT)
                    _perfectReactionNotes.Add(note);

                int clapIndex = Random.Shared.Next(1, 5);
                SFX.Play(this, $"SFX/clap{clapIndex}.wav", 4);
                break;
        }

        if(action != SeaponyAction.TapTap)
            SFX.Play(this, "SFX/BubbleSwim.wav", GLOBALS.SfxVolume);
    }

    private void unsubscribeReactionChartPlayer()
    {
        if(_reactionChartPlayer == null)
            return;

        _reactionChartPlayer.NoteReactedWithNote -= handleNoteReacted;
        _reactionChartPlayer = null;
    }

    private void spawnPlayerTapClapEffect(Note note)
    {
        if(note == null || _playerTapClapEffectNotes.Contains(note))
            return;

        _playerTapClapEffectNotes.Add(note);
        spawnTapClapEffect(getTapClapEffectPosition(1, 0));
    }

    private void spawnAutoTapClapEffect(Note note)
    {
        if(note == null || _autoTapClapEffectNotes.Contains(note))
            return;

        _autoTapClapEffectNotes.Add(note);
        spawnTapClapEffect(getTapClapEffectPosition(3, 2));
    }

    private Vector2 getTapClapEffectPosition(int leftSeaPonyIndex, int rightSeaPonyIndex)
    {
        Vector2 pairCenter = (_seaPonyBasePositions[leftSeaPonyIndex] + _seaPonyBasePositions[rightSeaPonyIndex]) * 0.5f;
        return pairCenter + _seaPonyGroupOffset + new Vector2(TapTapGroupOffsetX, TapClapEffectOffsetY);
    }

    private void spawnTapClapEffect(Vector2 position)
    {
        TimedSpriteEffect effect = new TimedSpriteEffect(
            this,
            GLOBALS.main_atlas.CreateSprite(MainAtlas.Seapony_parade_clap_effect),
            position,
            TapClapEffectScale,
            TapClapEffectDuration,
            removeTapClapEffect);

        effect.sprite.CenterOrigin();
        _tapClapEffects.Add(effect);
        GameObjects.Add(effect);
    }

    private void removeTapClapEffect(GameObject effect)
    {
        _tapClapEffects.Remove(effect);
    }

    private void createScrollBg()
    {
        _infiniteScrollBg = new InfiniteScrollBackground.Builder()
            .AddLine(conf =>
            {
                conf.AddPrototypes(
                    new GameObject[]
                    {
                        new GameObject(GLOBALS.main_atlas.CreateSprite(MainAtlas.Coral1)),
                        new GameObject(GLOBALS.main_atlas.CreateSprite(MainAtlas.Coral2)),
                        new GameObject(GLOBALS.main_atlas.CreateSprite(MainAtlas.Coral3)),
                        new GameObject(GLOBALS.main_atlas.CreateSprite(MainAtlas.Coral4)),
                        new GameObject(GLOBALS.main_atlas.CreateSprite(MainAtlas.Coral5)),
                        new GameObject(GLOBALS.main_atlas.CreateSprite(MainAtlas.Coral6)),

                    }
                );
                conf.WithPlacementInterval(25, 600);
                conf.WithScrollMultiplier(0.1f);
                conf.WithOffset(new Vector2(0, 2.6f * (_viewport.Height / 5)));
                conf.WithScale(CoralBackgroundScale);
            })
            .AddLine(conf =>
            {
                conf.AddPrototype(new GameObject(GLOBALS.main_atlas.CreateSprite(MainAtlas.Sand3)));
                conf.WithPlacementInterval(0, 0);
                conf.WithScrollMultiplier(0.3f);
                conf.WithOffset(new Vector2(0, 3.15f * (_viewport.Height / 5)));
                conf.WithScale(SandBackgroundScale);
            })
            .AddLine(conf =>
            {
                conf.AddPrototypes(
                    new GameObject[]
                    {
                        new GameObject(GLOBALS.main_atlas.CreateSprite(MainAtlas.Coral1)),
                        new GameObject(GLOBALS.main_atlas.CreateSprite(MainAtlas.Coral2)),
                        new GameObject(GLOBALS.main_atlas.CreateSprite(MainAtlas.Coral3)),
                        new GameObject(GLOBALS.main_atlas.CreateSprite(MainAtlas.Coral4)),
                        new GameObject(GLOBALS.main_atlas.CreateSprite(MainAtlas.Coral5)),
                        new GameObject(GLOBALS.main_atlas.CreateSprite(MainAtlas.Coral6)),

                    }
                );
                conf.WithPlacementInterval(25, 600);
                conf.WithScrollMultiplier(0.4f);
                conf.WithOffset(new Vector2(0, 3.09f * (_viewport.Height / 5)));
                conf.WithScale(CoralBackgroundScale);
            })
            .AddLine(conf =>
            {
                conf.AddPrototype(new GameObject(GLOBALS.main_atlas.CreateSprite(MainAtlas.Sand2)));
                conf.WithPlacementInterval(0, 0);
                conf.WithScrollMultiplier(0.6f);
                conf.WithOffset(new Vector2(485, 3.25f * (_viewport.Height / 5)));
                conf.WithScale(SandBackgroundScale);
            })
            .AddLine(conf =>
            {
                conf.AddPrototypes(
                    new GameObject[]
                    {
                        new GameObject(GLOBALS.main_atlas.CreateSprite(MainAtlas.Coral1)),
                        new GameObject(GLOBALS.main_atlas.CreateSprite(MainAtlas.Coral2)),
                        new GameObject(GLOBALS.main_atlas.CreateSprite(MainAtlas.Coral3)),
                        new GameObject(GLOBALS.main_atlas.CreateSprite(MainAtlas.Coral4)),
                        new GameObject(GLOBALS.main_atlas.CreateSprite(MainAtlas.Coral5)),
                        new GameObject(GLOBALS.main_atlas.CreateSprite(MainAtlas.Coral6)),

                    }
                );
                conf.WithPlacementInterval(25, 600);
                conf.WithScrollMultiplier(0.7f);
                conf.WithOffset(new Vector2(200, 3.45f * (_viewport.Height / 5)));
                conf.WithScale(CoralBackgroundScale);
            })
            .AddLine(conf =>
            {
                conf.AddPrototype(new GameObject(GLOBALS.main_atlas.CreateSprite(MainAtlas.Sand1)));
                conf.WithPlacementInterval(0, 0);
                conf.WithScrollMultiplier(1);
                conf.WithOffset(new Vector2(0, 3.4f * (_viewport.Height / 5)));
                conf.WithScale(SandBackgroundScale);
            })
            .WithPixelsPerProgress(new Vector2(_viewport.Width / 4, 0))
            .Build();
    }

    private void createSeaPonies()
    {
        float spacing = _viewport.Width / SeaPonyCount;
        float startX = spacing / 2f - _viewport.Width * 0.03f;

        for(int i = 0; i < SeaPonyCount; i++)
        {
            // Create object
            GameObject seaPony = new GameObject(createSeaPonyIdleSprite(i, 0.0));
            int positionIndex = Array.IndexOf(SeaPonyPositionOrder, i);
            seaPony.Position = new Vector2(startX + spacing * positionIndex, _viewport.Height / 2);
            _seaPonyBasePositions[i] = seaPony.Position;
            seaPony.Scale = Vector2.One * SCALE;
            seaPony.sprite.CenterOrigin();
            int seaPonyId = i;

            _seaPonies[i] = seaPony;

            // Create animation state
            AnimationStateMachine animationState = new AnimationStateMachine()
                .AddState(new AnimationState(
                    "idle",
                    1,
                    isLooping: false,
                    onEnter: () =>
                    {
                        seaPony.sprite = createSeaPonyIdleSprite(seaPonyId, GLOBALS.beatmapPlayer.Conductor?.SongPosition ?? 0.0);
                        seaPony.sprite.CenterOrigin();
                        _seaPonyIdleBeatStates[seaPonyId] = int.MinValue;
                    }
                ))
                .AddState(new AnimationState(
                    "swim_anticipation",
                    1,
                    isLooping: false,
                    onEnter: () =>
                    {
                        seaPony.sprite = GLOBALS.main_atlas.CreateSprite(getSeaPonyAtlasRegion(seaPonyId, "swim_anticipation"));
                        seaPony.sprite.CenterOrigin();
                        seaPony.sprite.DrawOffset = new Vector2(80, 0);
                    })
                )
                .AddState(new AnimationState(
                    "swim",
                    1,
                    isLooping: false,
                    onEnter: () =>
                    {
                        seaPony.sprite = GLOBALS.main_atlas.CreateSprite(getSeaPonyAtlasRegion(seaPonyId, "swim"));
                        seaPony.sprite.CenterOrigin();
                    })
                )
                .AddState(new AnimationState(
                    "roll",
                    1,
                    isLooping: false,
                    onEnter: () =>
                    {
                        seaPony.sprite = GLOBALS.main_atlas.CreateSprite(getSeaPonyAtlasRegion(seaPonyId, "roll"));
                        seaPony.sprite.CenterOrigin();
                        seaPony.sprite.DrawOffset += new Vector2(15, 0);
                    }
                ))
                .AddState(new AnimationState(
                    "uptap",
                    1,
                    isLooping: false,
                    onEnter: () =>
                    {
                        seaPony.sprite = GLOBALS.main_atlas.CreateSprite(getSeaPonyAtlasRegion(seaPonyId, "uptap"));
                        seaPony.sprite.CenterOrigin();
                    }
                ))
                .AddState(new AnimationState(
                    "downtap",
                    1,
                    isLooping: false,
                    onEnter: () =>
                    {
                        seaPony.sprite = GLOBALS.main_atlas.CreateSprite(getSeaPonyAtlasRegion(seaPonyId, "downtap"));
                        seaPony.sprite.CenterOrigin();
                    }
                ))
                .SetGlobalUpdate(time =>
                {
                    if (seaPony.sprite is AnimatedSprite animatedSprite)
                        animatedSprite.Update(time);
                     
                    return true;
                })
                .Build();

                animationState.ForceState("idle");
                _seaPoniesAnimationStates[i] = animationState;
        }

        for(int i = 0; i < SeaPonyCount; i++)
            GameObjects.Add(_seaPonies[i]);
    }

    private static string getSeaPonyAtlasRegion(int seaPonyId, string state)
    {
        if(seaPonyId >= 0
            && seaPonyId < _seaPoniesSkins.Length
            && _seaPoniesSkins[seaPonyId].TryGetValue(state, out string atlasRegion)
            && !string.IsNullOrEmpty(atlasRegion))
        {
            return atlasRegion;
        }
        return MainAtlas.Pinkiepie_pony_idle1;
    }

    private AnimatedSprite createSeaPonyIdleSprite(int seaPonyId, double songPosition)
    {
        GameCore.Graphics.Animation animation = GLOBALS.main_atlas.GetAnimation(getSeaPonyIdleAnimationName(seaPonyId));
        List<TextureRegion> frames = animation.Frames.Count > 1
            ? new List<TextureRegion> { animation.Frames[1], animation.Frames[0] }
            : animation.Frames.ToList();
        AnimatedSprite sprite = new AnimatedSprite(new GameCore.Graphics.Animation(frames, TimeSpan.FromSeconds(getIdleJamFrameDuration(songPosition))));
        sprite.IsLooping = false;
        return sprite;
    }

    private static string getSeaPonyIdleAnimationName(int seaPonyId)
    {
        string idleRegion = getSeaPonyAtlasRegion(seaPonyId, "idle");
        return idleRegion.EndsWith("1") ? idleRegion.Substring(0, idleRegion.Length - 1) : idleRegion;
    }

    /// <summary>
    /// Reconstruit les managers de visual notes et le runtime de tracks associé.
    /// </summary>
    /// <remarks>
    /// Les visual notes SeaPony existantes gardent leur logique legacy. Le background passe par
    /// <see cref="VisualRuntime"/> : la track <c>background</c> référence <see cref="_infiniteScrollBg"/>
    /// et <see cref="SeaponyBgVisualNote"/> la mute seulement si son note driver est actif.
    /// </remarks>
    private void createVisualNotes()
    {
        if(GLOBALS.beatmapPlayer.ChartPlayer == null || GLOBALS.beatmapPlayer.Conductor == null)
            return;

        clearVisualRuntimeDrivers();
        ChartPlayer chartPlayer = GLOBALS.beatmapPlayer.ChartPlayer;
        _visualRuntime = new VisualRuntime();
        SeaponyActorDriverPolicy seaPonyDriverPolicy = new(getSeaponyApproachDuration, getSeaponyDespawnDelay, () => getMaxCrotchet() * 2);
        _visualRuntime.RegisterTrack("background", _infiniteScrollBg)
            .UseDriverPolicy(new SeaponyBackgroundDriverPolicy(getBackgroundScrollDuration));
        registerSeaPonyVisualTracks(seaPonyDriverPolicy);

        for(int i = 0; i < SeaPonyCount; i++)
        {
            GameObject seaPony = _seaPonies[i];
            AnimationStateMachine animationState = _seaPoniesAnimationStates[i];
            int seaPonyId = i;

            VisualNoteManager<SeaponyVisualNote> visualNoteManager = new VisualNoteManager<SeaponyVisualNote>(chartPlayer, note =>
            {
                if(!TryGetSeaponyAction(note, out SeaponyAction action))
                    return null;

                double crotchet = getCrotchetAt(note);
                switch(action)
                {
                    case SeaponyAction.Swim:
                        return new SeaponyVisualNote(note, crotchet, this, seaPony, animationState, seaPonyId, crotchet, despawnDelay: crotchet, hasSuccessfulReactionProvider: hasSuccessfulReaction, baseSeaPonyPosition: _seaPonyBasePositions[seaPonyId], runtime: _visualRuntime, seaPonyTrackId: SeaponyVisualNote.GetPonyTrackId(seaPonyId), seaPonyAnimationTrackId: SeaponyVisualNote.GetPonyAnimationTrackId(seaPonyId));
                    case SeaponyAction.Roll:
                        return new SeaponyVisualNote(note, crotchet * 2, this, seaPony, animationState, seaPonyId, crotchet, getRollTargetRotation(note), getRollDespawnDelay(note), rollIndexInSequence: getRollIndexInSequence(note), rollsRemainingInSequence: getRollsRemainingInSequence(note), canRollProvider: canSeaPonyRoll, baseSeaPonyPosition: _seaPonyBasePositions[seaPonyId], runtime: _visualRuntime, seaPonyTrackId: SeaponyVisualNote.GetPonyTrackId(seaPonyId), seaPonyAnimationTrackId: SeaponyVisualNote.GetPonyAnimationTrackId(seaPonyId));
                    case SeaponyAction.TapTap:
                        int tapTapHitsRemaining = getTapTapHitsRemainingInSequence(note);
                        return new SeaponyVisualNote(note, crotchet * 2, this, seaPony, animationState, seaPonyId, crotchet, despawnDelay: getTapTapDespawnDelay(note), tapTapIndexInSequence: getTapTapIndexInSequence(note), tapTapHitsRemainingInSequence: tapTapHitsRemaining, hasSuccessfulReactionProvider: hasSuccessfulReaction, hasPerfectReactionProvider: hasPerfectReaction, baseSeaPonyPosition: _seaPonyBasePositions[seaPonyId], runtime: _visualRuntime, seaPonyTrackId: SeaponyVisualNote.GetPonyTrackId(seaPonyId), seaPonyAnimationTrackId: SeaponyVisualNote.GetPonyAnimationTrackId(seaPonyId));
                    case SeaponyAction.Leave:
                    case SeaponyAction.Enter:
                        return null;
                }
                return null;
            });
            
            visualNoteManager.LookAheadSeconds = getMaxCrotchet() * 2;
            visualNoteManager.LookBehindSeconds = getMaxCrotchet() * 3;
            _seaPoniesVisualNotes[i] = visualNoteManager;
            
        }




        _infiniteScrollBgVisualNotes = new VisualNoteManager<SeaponyBgVisualNote>(chartPlayer, (note) =>
        {
            if(!TryGetSeaponyAction(note, out SeaponyAction action))
                return null;

            double crotchet = getCrotchetAt(note);
            return new SeaponyBgVisualNote(note, _visualRuntime, crotchet, getBackgroundScrollDestinationBeat(note), getBackgroundScrollDuration(action, note));
        });

        _infiniteScrollBgVisualNotes.LookAheadSeconds = getMaxCrotchet() * 2;
        _infiniteScrollBgVisualNotes.LookBehindSeconds = getMaxCrotchet() * 2;
    }

    /// <summary>
    /// Enregistre les tracks runtime qui représentent les sea ponies et leurs machines d'animation.
    /// </summary>
    /// <remarks>
    /// Ces tracks remplacent le guard legacy <c>Func&lt;bool&gt; canApplyState</c> pour <see cref="SeaponyVisualNote"/>.
    /// La policy attachée choisit automatiquement la note conductrice pendant <see cref="VisualRuntime.ResolveDrivers"/>.
    /// </remarks>
    private void registerSeaPonyVisualTracks(SeaponyActorDriverPolicy driverPolicy)
    {
        for(int i = 0; i < SeaPonyCount; i++)
        {
            _visualRuntime.RegisterTrack(SeaponyVisualNote.GetPonyTrackId(i), _seaPonies[i])
                .UseDriverPolicy(driverPolicy);
            _visualRuntime.RegisterTrack(SeaponyVisualNote.GetPonyAnimationTrackId(i), _seaPoniesAnimationStates[i])
                .UseDriverPolicy(driverPolicy);
        }
    }

    private bool canSeaPonyRoll(int seaPonyIndex, Note note)
    {
        return seaPonyIndex != 1 || (_authorizedRollNotes.Contains(note) && note.HasReacted);
    }

    private bool hasSuccessfulReaction(Note note)
    {
        return note != null && note.HasReacted && _successfulReactionNotes.Contains(note);
    }

    private bool hasPerfectReaction(Note note)
    {
        return note != null && note.HasReacted && _perfectReactionNotes.Contains(note);
    }

    private double getSeaponyApproachDuration(SeaponyAction action, Note note)
    {
        double crotchet = getCrotchetAt(note);
        return action == SeaponyAction.Roll || action == SeaponyAction.TapTap ? crotchet * 2 : crotchet;
    }

    private double getSeaponyDespawnDelay(SeaponyAction action, Note note)
    {
        double crotchet = getCrotchetAt(note);
        if(action == SeaponyAction.Roll)
            return getRollDespawnDelay(note);

        if(action == SeaponyAction.TapTap)
            return getTapTapDespawnDelay(note);

        if(IsGroupMoveAction(action))
            return getGroupMoveVisualDuration(note);

        return crotchet;
    }

    private void clearVisualRuntimeDrivers()
    {
        _visualRuntime?.ClearDrivers();
    }

    private void resetSeaPonyTimelineState()
    {
        _authorizedRollNotes.Clear();
        _successfulReactionNotes.Clear();
        _perfectReactionNotes.Clear();
        _playerTapClapEffectNotes.Clear();
        _autoTapClapEffectNotes.Clear();
        clearTapClapEffects();
        clearVisualRuntimeDrivers();
        _activeGroupMoveNote = null;
        _lastSeaPonySongPosition = double.NaN;
        _seaPonyGroupOffset = Vector2.Zero;

        for(int i = 0; i < SeaPonyCount; i++)
        {
            if(_seaPonies[i] != null)
            {
                _seaPonies[i].Position = _seaPonyBasePositions[i] + _seaPonyGroupOffset;
                _seaPonies[i].Rotation = 0;
                _seaPonies[i].Scale = Vector2.One * SCALE;
                if(_seaPonies[i].sprite != null)
                    _seaPonies[i].sprite.Effects = SpriteEffects.None;
            }

            RhythmVisualUtils.ForceAnimationState(_seaPoniesAnimationStates[i], "idle");
            _seaPonyIdleBeatStates[i] = int.MinValue;
            restartIdleJamAnimationOnBeat(i, 0.0);
        }
    }

    private void clearTapClapEffects()
    {
        foreach(GameObject effect in _tapClapEffects)
            RemoveGameObject(effect);

        _tapClapEffects.Clear();
    }

    private void applyBaseStateForIdleSeaPonies(double songPosition)
    {
        for(int i = 0; i < SeaPonyCount; i++)
        {
            if(_activeGroupMoveNote != null)
            {
                applyGroupMoveBaseState(i);
                continue;
            }

            if(hasSeaPonyVisualDriver(i))
                continue;

            RhythmVisualUtils.ForceAnimationState(_seaPoniesAnimationStates[i], "idle");
            restartIdleJamAnimationOnBeat(i, songPosition);

            if(_seaPonies[i] != null)
            {
                _seaPonies[i].Position = _seaPonyBasePositions[i] + _seaPonyGroupOffset;
                _seaPonies[i].Scale = Vector2.One * SCALE;
                if(_seaPonies[i].sprite != null)
                    _seaPonies[i].sprite.Effects = SpriteEffects.None;

                _seaPonies[i].Rotation = getStableSeaPonyRotation(i, songPosition);
            }
        }
    }

    private void restartIdleJamAnimationOnBeat(int seaPonyIndex, double songPosition)
    {
        if(seaPonyIndex < 0 || seaPonyIndex >= SeaPonyCount || _seaPonies[seaPonyIndex] == null)
            return;

        int beat = (int)Math.Floor(getBeatAt(songPosition));
        if(_seaPonyIdleBeatStates[seaPonyIndex] == beat)
            return;

        _seaPonies[seaPonyIndex].sprite = createSeaPonyIdleSprite(seaPonyIndex, songPosition);
        _seaPonies[seaPonyIndex].sprite.CenterOrigin();
        _seaPonyIdleBeatStates[seaPonyIndex] = beat;
    }

    private double getIdleJamFrameDuration(double songPosition)
    {
        double crotchet = Math.Max(0.001, GLOBALS.beatmapPlayer?.GetCrotchetAt(songPosition) ?? 0.6);
        return crotchet * IdleJamPoseBeatFraction;
    }

    private void applyGroupMoveBaseState(int seaPonyIndex)
    {
        RhythmVisualUtils.ForceAnimationState(_seaPoniesAnimationStates[seaPonyIndex], "swim");

        if(_seaPonies[seaPonyIndex] == null)
            return;

        _seaPonies[seaPonyIndex].Position = _seaPonyBasePositions[seaPonyIndex] + _seaPonyGroupOffset;
        _seaPonies[seaPonyIndex].Rotation = 0;
        _seaPonies[seaPonyIndex].Scale = Vector2.One * SCALE;
        if(_seaPonies[seaPonyIndex].sprite != null)
            _seaPonies[seaPonyIndex].sprite.Effects = SpriteEffects.None;
    }

    private Vector2 getSeaPonyGroupOffset(double songPosition)
    {
        Note groupMoveNote = findStartedGroupMoveNote(songPosition);
        if(groupMoveNote == null || !TryGetSeaponyAction(groupMoveNote, out SeaponyAction action))
            return Vector2.Zero;

        double duration = getGroupMoveVisualDuration(groupMoveNote);
        double end = groupMoveNote.SongPosition + duration;
        float progress = (float)RhythmVisualUtils.GetProgression(groupMoveNote.SongPosition, end, songPosition);
        float offsetProgress = action == SeaponyAction.Enter
            ? -(1f - Interpolation.EaseOutBack(progress))
            : Interpolation.EaseInBack(progress);
        float swimBob = _activeGroupMoveNote != null
            ? MathF.Sin((float)((songPosition - groupMoveNote.SongPosition) / Math.Max(0.001, getCrotchetAt(groupMoveNote)) * MathHelper.TwoPi)) * 12f
            : 0f;

        return new Vector2(getGroupMoveDistance(action) * offsetProgress, swimBob);
    }

    private Note findStartedGroupMoveNote(double songPosition)
    {
        ChartPlayer chartPlayer = GLOBALS.beatmapPlayer.ChartPlayer;
        if(chartPlayer == null)
            return null;

        Note startedNote = null;
        foreach(Note note in chartPlayer.Notes)
        {
            if(note.SongPosition > songPosition)
                break;

            if(IsGroupMoveNote(note))
                startedNote = note;
        }

        return startedNote;
    }

    private Note findActiveGroupMoveNote(double songPosition)
    {
        ChartPlayer chartPlayer = GLOBALS.beatmapPlayer.ChartPlayer;
        if(chartPlayer == null)
            return null;

        Note activeNote = null;
        foreach(Note note in chartPlayer.Notes)
        {
            if(note.SongPosition > songPosition)
                break;

            if(!IsGroupMoveNote(note))
                continue;

            if(songPosition <= note.SongPosition + getGroupMoveVisualDuration(note))
                activeNote = note;
        }

        return activeNote;
    }

    private double getGroupMoveVisualDuration(Note note)
    {
        if(note?.HoldDuration > 0.0)
            return Math.Max(0.001, note.HoldDuration);

        double defaultLengthBeats = IsSeaponyAction(note, SeaponyAction.Enter)
            ? SeaponyParadePatternCompiler.EnterDefaultLengthBeats
            : SeaponyParadePatternCompiler.LeaveDefaultLengthBeats;
        return Math.Max(0.001, defaultLengthBeats * getCrotchetAt(note));
    }

    private float getGroupMoveDistance(SeaponyAction action)
    {
        if(action == SeaponyAction.Enter)
        {
            float rightmostBaseX = _seaPonyBasePositions.Max(position => position.X);
            return Math.Max(LeaveExitPadding, rightmostBaseX + LeaveExitPadding);
        }

        float leftmostBaseX = _seaPonyBasePositions.Min(position => position.X);
        return Math.Max(LeaveExitPadding, _viewport.Width - leftmostBaseX + LeaveExitPadding);
    }

    private static bool IsGroupMoveAction(SeaponyAction action)
    {
        return action == SeaponyAction.Leave || action == SeaponyAction.Enter;
    }

    private static bool IsGroupMoveNote(Note note)
    {
        return TryGetSeaponyAction(note, out SeaponyAction action) && IsGroupMoveAction(action);
    }

    private float getStableSeaPonyRotation(int seaPonyIndex, double songPosition)
    {
        if(seaPonyIndex == 1)
            return 0f;

        ChartPlayer chartPlayer = GLOBALS.beatmapPlayer.ChartPlayer;
        if(chartPlayer == null)
            return 0f;

        int completedRollCount = 0;
        foreach(Note note in chartPlayer.Notes)
        {
            if(note.SongPosition > songPosition)
                break;

            if(TryGetSeaponyAction(note, out SeaponyAction action) && action == SeaponyAction.Roll)
                completedRollCount++;
        }

        return MathHelper.ToRadians(completedRollCount % 4 * 90f);
    }

    private static bool TryGetSeaponyAction(Note note, out SeaponyAction action)
    {
        return SeaponyNoteCodec.TryReadAction(note?.AdditionnalData, out action);
    }

    private float getRollTargetRotation(Note note)
    {
        int rollCount = 0;

        foreach(Note candidate in GLOBALS.beatmapPlayer.ChartPlayer.Notes)
        {
            if(IsSeaponyAction(candidate, SeaponyAction.Roll))
            {
                rollCount++;
            }

            if(ReferenceEquals(candidate, note))
            {
                break;
            }
        }

        return rollCount % 4 * 90f;
    }

    private int getRollIndexInSequence(Note note)
    {
        int index = 0;

        foreach(Note candidate in GLOBALS.beatmapPlayer.ChartPlayer.Notes)
        {
            if(ReferenceEquals(candidate, note))
                break;

            if(IsSeaponyAction(candidate, SeaponyAction.Roll))
            {
                index++;
                continue;
            }

            index = 0;
        }

        return index;
    }

    private int getRollsRemainingInSequence(Note note)
    {
        int remaining = 0;
        bool foundNote = false;

        foreach(Note candidate in GLOBALS.beatmapPlayer.ChartPlayer.Notes)
        {
            if(ReferenceEquals(candidate, note))
                foundNote = true;

            if(!foundNote)
                continue;

            if(IsSeaponyAction(candidate, SeaponyAction.Roll))
            {
                remaining++;
                continue;
            }

            break;
        }

        return remaining;
    }

    private double getRollDespawnDelay(Note note)
    {
        int rollCount = 0;
        bool foundNote = false;

        foreach(Note candidate in GLOBALS.beatmapPlayer.ChartPlayer.Notes)
        {
            if(ReferenceEquals(candidate, note))
            {
                foundNote = true;
                continue;
            }

            if(!foundNote)
            {
                continue;
            }

            if(IsSeaponyAction(candidate, SeaponyAction.Roll))
            {
                return candidate.SongPosition - note.SongPosition;
            }

            break;
        }

        foreach(Note candidate in GLOBALS.beatmapPlayer.ChartPlayer.Notes)
        {
            if(IsSeaponyAction(candidate, SeaponyAction.Roll))
            {
                rollCount++;
            }

            if(ReferenceEquals(candidate, note))
            {
                break;
            }
        }

        int paddingBeats = (4 - rollCount % 4) % 4;
        if(paddingBeats == 0)
        {
            paddingBeats = 1;
        }

        return paddingBeats * getCrotchetAt(note);
    }

    private int getTapTapIndexInSequence(Note note)
    {
        int index = 0;

        foreach(Note candidate in GLOBALS.beatmapPlayer.ChartPlayer.Notes)
        {
            if(ReferenceEquals(candidate, note))
                break;

            if(IsSeaponyAction(candidate, SeaponyAction.TapTap))
            {
                index++;
                continue;
            }

            index = 0;
        }

        return index;
    }

    private int getTapTapHitsRemainingInSequence(Note note)
    {
        int remaining = 0;
        bool foundNote = false;

        foreach(Note candidate in GLOBALS.beatmapPlayer.ChartPlayer.Notes)
        {
            if(ReferenceEquals(candidate, note))
                foundNote = true;

            if(!foundNote)
                continue;

            if(IsSeaponyAction(candidate, SeaponyAction.TapTap))
            {
                remaining++;
                continue;
            }

            break;
        }

        return remaining;
    }

    private double getTapTapDespawnDelay(Note note)
    {
        bool foundNote = false;

        foreach(Note candidate in GLOBALS.beatmapPlayer.ChartPlayer.Notes)
        {
            if(ReferenceEquals(candidate, note))
            {
                foundNote = true;
                continue;
            }

            if(!foundNote)
                continue;

            if(IsSeaponyAction(candidate, SeaponyAction.TapTap))
                return candidate.SongPosition - note.SongPosition;

            break;
        }

        return 2 * getCrotchetAt(note);
    }

    private double getGroupMoveDespawnDelay(Note note)
    {
        return getGroupMoveVisualDuration(note);
    }

    private static bool IsSeaponyAction(Note note, SeaponyAction expectedAction)
    {
        return SeaponyNoteCodec.IsAction(note?.AdditionnalData, expectedAction);
    }

    private int getBackgroundScrollDestinationBeat(Note note)
    {
        int seaponyNoteIndex = 0;

        foreach(Note candidate in GLOBALS.beatmapPlayer.ChartPlayer.Notes)
        {
            if(TryGetSeaponyAction(candidate, out _))
            {
                seaponyNoteIndex++;
            }

            if(ReferenceEquals(candidate, note)) return seaponyNoteIndex;
        }

        return seaponyNoteIndex;
    }

    private double getBackgroundScrollDuration(SeaponyAction action, Note note)
    {
        double crotchet = getCrotchetAt(note);
        return action switch
        {
            SeaponyAction.Roll => crotchet * 0.5,
            SeaponyAction.TapTap => crotchet * 2,
            SeaponyAction.Leave or SeaponyAction.Enter => getGroupMoveDespawnDelay(note),
            _ => crotchet
        };
    }

    private double getCrotchetAt(Note note)
    {
        if(note == null)
            return getMaxCrotchet();

        return GLOBALS.beatmapPlayer?.GetCrotchetAt(note.SongPosition) ?? 0.6;
    }

    private double getBeatAt(double songPosition)
    {
        double crotchet = Math.Max(0.001, GLOBALS.beatmapPlayer?.GetCrotchetAt(songPosition) ?? 0.6);
        return GLOBALS.beatmapPlayer?.GetBeatAt(songPosition) ?? songPosition / crotchet;
    }

    private double getMaxCrotchet()
    {
        return GLOBALS.beatmapPlayer?.GetMaxCrotchet() ?? 0.6;
    }

    public override void OnUnload()
    {
        GLOBALS.beatmapPlayer.BeatmapStarted -= createVisualNotes;
        GLOBALS.beatmapPlayer.BeatmapStarted -= createReactions;
        unsubscribeReactionChartPlayer();
        clearTapClapEffects();
    }

    private bool hasSeaPonyVisualDriver(int seaPonyIndex)
    {
        return _visualRuntime?.Track(SeaponyVisualNote.GetPonyTrackId(seaPonyIndex))?.DriverNote != null;
    }

    private float _elapsed = 0; 
    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        if(!shouldUpdateSceneState())
            return;

        updateSeaPonies(gameTime);
        _infiniteScrollBg?.Update(gameTime);
    }

    private bool shouldUpdateSceneState()
    {
        if(GLOBALS.beatmapPlayer.Conductor == null)
            return false;

        if(GLOBALS.beatmapPlayer.Conductor.isPlaying())
            return true;

        double songPosition = GLOBALS.beatmapPlayer.Conductor.SongPosition;
        return double.IsNaN(_lastSeaPonySongPosition)
            || RhythmVisualUtils.HasRewound(songPosition, _lastSeaPonySongPosition)
            || Math.Abs(songPosition - _lastSeaPonySongPosition) > 0.000001;
    }

    private void updateSeaPonies(GameTime gameTime)
    {
        if(GLOBALS.beatmapPlayer.Conductor == null)
            return;

        double songPos = GLOBALS.beatmapPlayer.Conductor.SongPosition;
        if(RhythmVisualUtils.HasRewound(songPos, _lastSeaPonySongPosition))
        {
            _authorizedRollNotes.Clear();
            _successfulReactionNotes.Clear();
            _perfectReactionNotes.Clear();
            _playerTapClapEffectNotes.Clear();
            _autoTapClapEffectNotes.Clear();
            clearTapClapEffects();
            _seaPonyGroupOffset = Vector2.Zero;
            _activeGroupMoveNote = null;
        }

        _activeGroupMoveNote = findActiveGroupMoveNote(songPos);
        _seaPonyGroupOffset = getSeaPonyGroupOffset(songPos);
        resolveVisualDrivers(songPos);
        playStartCues(songPos, gameTime.ElapsedGameTime.TotalSeconds);
        applyBaseStateForIdleSeaPonies(songPos);

        for(int i = 0; i < SeaPonyCount; i++)
        {
            _seaPoniesAnimationStates[i]?.Update(gameTime);
            _seaPoniesVisualNotes[i]?.Update(songPos);
        }

        spawnTapClapEffectsOnForwardCross(songPos, gameTime.ElapsedGameTime.TotalSeconds);
        _infiniteScrollBgVisualNotes?.Update(songPos);
        _lastSeaPonySongPosition = songPos;
    }

    /// <summary>
    /// Résout tous les drivers de tracks visuelles pour la frame courante.
    /// </summary>
    /// <param name="songPosition">Position musicale courante.</param>
    private void resolveVisualDrivers(double songPosition)
    {
        if(GLOBALS.beatmapPlayer.ChartPlayer == null)
        {
            _visualRuntime?.ClearDrivers();
            return;
        }

        _visualRuntime?.ResolveDrivers(songPosition, GLOBALS.beatmapPlayer.ChartPlayer.Notes);
    }

    private void spawnTapClapEffectsOnForwardCross(double songPosition, double elapsedSeconds)
    {
        if(GLOBALS.beatmapPlayer.ChartPlayer == null)
            return;

        double previousSongPosition = double.IsNaN(_lastSeaPonySongPosition)
            ? songPosition - Math.Max(0.0, elapsedSeconds)
            : _lastSeaPonySongPosition;

        if(previousSongPosition > songPosition)
            return;

        foreach(Note note in GLOBALS.beatmapPlayer.ChartPlayer.Notes)
        {
            if(note.SongPosition > songPosition)
                break;

            if(previousSongPosition < note.SongPosition
                && songPosition >= note.SongPosition
                && IsSeaponyAction(note, SeaponyAction.TapTap))
            {
                spawnAutoTapClapEffect(note);
            }
        }
    }

    private void playStartCues(double songPosition, double elapsedSeconds)
    {
        if(GLOBALS.SfxVolume <= 0 || GLOBALS.beatmapPlayer.ChartPlayer == null)
            return;

        double previousSongPosition = double.IsNaN(_lastSeaPonySongPosition)
            ? songPosition - Math.Max(0.0, elapsedSeconds)
            : _lastSeaPonySongPosition;

        if(previousSongPosition > songPosition)
            return;

        SeaponyAction? previousSeaponyAction = null;
        foreach(Note note in GLOBALS.beatmapPlayer.ChartPlayer.Notes)
        {
            if(!TryGetSeaponyAction(note, out SeaponyAction action))
                continue;

            double crotchet = getCrotchetAt(note);
            if(action == SeaponyAction.Roll && previousSeaponyAction != SeaponyAction.Roll)
            {
                playSfxOnForwardCross(previousSongPosition, note.SongPosition - FirstCueLeadBeats * crotchet, songPosition, "SFX/BubbleHeavy.wav");
                playSfxOnForwardCross(previousSongPosition, note.SongPosition - SecondCueLeadBeats * crotchet, songPosition, "SFX/BubbleHeavy.wav");
            }
            else if(action == SeaponyAction.TapTap && previousSeaponyAction != SeaponyAction.TapTap)
            {
                playSfxOnForwardCross(previousSongPosition, note.SongPosition - FirstCueLeadBeats * crotchet, songPosition, "SFX/seapony_parade_roll.wav");
                playSfxOnForwardCross(previousSongPosition, note.SongPosition - SecondCueLeadBeats * crotchet, songPosition, "SFX/seapony_parade_roll.wav");
            }

            previousSeaponyAction = action;
        }
    }

    private void playSfxOnForwardCross(double previousSongPosition, double cuePosition, double currentSongPosition, string filePath)
    {
        if(previousSongPosition < cuePosition && currentSongPosition >= cuePosition)
            SFX.Play(this, filePath, 4);
    }

    public override void Draw(SpriteBatch spriteBatch)
    {

        GLOBALS.graphicsDevice?.Clear(Color.DarkBlue);

        _devUIRenderer.Label(spriteBatch, "You", _seaPonies[1].Position + new Vector2(-25, -245), Color.White, 7);
        _infiniteScrollBg?.Draw(spriteBatch);

        base.Draw(spriteBatch);

    }

    private sealed class TimedSpriteEffect : GameObject
    {
        private readonly Scene _scene;
        private readonly double _duration;
        private readonly float _baseScale;
        private readonly Action<GameObject> _onFinished;
        private double _elapsed;

        public TimedSpriteEffect(Scene scene, Sprite sprite, Vector2 position, float baseScale, double duration, Action<GameObject> onFinished)
            : base(sprite)
        {
            _scene = scene;
            _duration = Math.Max(0.001, duration);
            _baseScale = baseScale;
            _onFinished = onFinished;
            Position = position;
            Scale = Vector2.One * baseScale;
        }

        public override void Update(GameTime gameTime)
        {
            _elapsed += gameTime.ElapsedGameTime.TotalSeconds;
            float progress = (float)Math.Clamp(_elapsed / _duration, 0.0, 1.0);

            Scale = Vector2.One * (_baseScale * MathHelper.Lerp(0.85f, 1.2f, progress));
            if(sprite != null)
                sprite.Color = Color.White * (1f - progress);

            if(_elapsed < _duration)
                return;

            _onFinished?.Invoke(this);
            _scene.RemoveGameObject(this);
        }
    }

}


