using System;
using System.Collections.Generic;
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

    // Objects
    private Viewport _viewport;
    private GameObject[] _seaPonies = new GameObject[SeaPonyCount];
    private Vector2[] _seaPonyBasePositions = new Vector2[SeaPonyCount];
    private AnimationStateMachine[] _seaPoniesAnimationStates = new AnimationStateMachine[SeaPonyCount];
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
            new Dictionary<string, string>(),
            new Dictionary<string, string>
            {
                { "idle", MainAtlas.Applejack_pony_idle1 },
                { "swim_anticipation", MainAtlas.Applejack_pony_swim1 },
                { "swim", MainAtlas.Applejack_pony_swim2 },
                { "roll", MainAtlas.Applejack_pony_star },
                { "uptap", MainAtlas.Applejack_pony_tap1 },
                { "downtap", MainAtlas.Applejack_pony_tap2 }
            },
            new Dictionary<string, string>(),
        };
    private HashSet<Note> _authorizedRollNotes = new HashSet<Note>();
    private HashSet<Note> _successfulReactionNotes = new HashSet<Note>();
    private HashSet<Note> _perfectReactionNotes = new HashSet<Note>();
    private Note[] _drivingSeaPonyNotes = new Note[SeaPonyCount];
    private VisualNoteManager<SeaponyVisualNote>[] _seaPoniesVisualNotes = new VisualNoteManager<SeaponyVisualNote>[SeaPonyCount];
    private ChartPlayer _reactionChartPlayer;
    private Note _drivingBackgroundNote;
    private double _lastSeaPonySongPosition = double.NaN;

    private InfiniteScrollBackground _infiniteScrollBg;
    private VisualNoteManager<SeaponyBgVisualNote> _infiniteScrollBgVisualNotes;

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
                if(result == NoteReactionResult.PERFECT)
                {
                    _perfectReactionNotes.Add(note);
                    int clapIndex = Random.Shared.Next(1, 5);
                    SFX.Play(this, $"SFX/clap{clapIndex}.wav", 4);
                }
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
            GameObject seaPony = new GameObject(GLOBALS.main_atlas.CreateAnimatedSprite("template-pony-idle"));
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
                        seaPony.sprite = GLOBALS.main_atlas.CreateSprite(getSeaPonyAtlasRegion(seaPonyId, "idle"));
                        seaPony.sprite.CenterOrigin();
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

        switch(state)
        {
            case "idle":
                return MainAtlas.Template_pony_idle1;
            case "swim_anticipation":
                return MainAtlas.Template_pony_swim1;
            case "swim":
                return MainAtlas.Template_pony_swim2;
            case "roll":
                return MainAtlas.Template_pony_star;
            case "uptap":
                return MainAtlas.Template_pony_tap1;
            case "downtap":
                return MainAtlas.Template_pony_tap2;
            default:
                return MainAtlas.Template_pony_idle1;
        }
    }

    private void createVisualNotes()
    {
        if(GLOBALS.beatmapPlayer.ChartPlayer == null || GLOBALS.beatmapPlayer.Conductor == null)
            return;

        resetDrivingSeaPonyNotes();
        ChartPlayer chartPlayer = GLOBALS.beatmapPlayer.ChartPlayer;

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
                        return new SeaponyVisualNote(note, crotchet, this, seaPony, animationState, seaPonyId, crotchet, despawnDelay: crotchet, hasSuccessfulReactionProvider: hasSuccessfulReaction, canApplyState: () => canSeaPonyApplyState(seaPonyId, note), baseSeaPonyPosition: _seaPonyBasePositions[seaPonyId]);
                    case SeaponyAction.Roll:
                        return new SeaponyVisualNote(note, crotchet * 2, this, seaPony, animationState, seaPonyId, crotchet, getRollTargetRotation(note), getRollDespawnDelay(note), rollIndexInSequence: getRollIndexInSequence(note), rollsRemainingInSequence: getRollsRemainingInSequence(note), canRollProvider: canSeaPonyRoll, canApplyState: () => canSeaPonyApplyState(seaPonyId, note), baseSeaPonyPosition: _seaPonyBasePositions[seaPonyId]);
                    case SeaponyAction.TapTap:
                        int tapTapHitsRemaining = getTapTapHitsRemainingInSequence(note);
                        return new SeaponyVisualNote(note, crotchet * 2, this, seaPony, animationState, seaPonyId, crotchet, despawnDelay: getTapTapDespawnDelay(note), tapTapIndexInSequence: getTapTapIndexInSequence(note), tapTapHitsRemainingInSequence: tapTapHitsRemaining, hasSuccessfulReactionProvider: hasSuccessfulReaction, hasPerfectReactionProvider: hasPerfectReaction, canApplyState: () => canSeaPonyApplyState(seaPonyId, note), baseSeaPonyPosition: _seaPonyBasePositions[seaPonyId]);
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
            return new SeaponyBgVisualNote(note, crotchet, _infiniteScrollBg, getBackgroundScrollDestinationBeat(note), getBackgroundScrollDuration(action, note), canApplyState: () => ReferenceEquals(_drivingBackgroundNote, note));
        });

        _infiniteScrollBgVisualNotes.LookAheadSeconds = getMaxCrotchet() * 2;
        _infiniteScrollBgVisualNotes.LookBehindSeconds = getMaxCrotchet() * 2;
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

    private bool canSeaPonyApplyState(int seaPonyIndex, Note note)
    {
        return seaPonyIndex >= 0
            && seaPonyIndex < _drivingSeaPonyNotes.Length
            && ReferenceEquals(_drivingSeaPonyNotes[seaPonyIndex], note);
    }

    private void updateDrivingSeaPonyNotes(double songPosition)
    {
        for(int i = 0; i < SeaPonyCount; i++)
            _drivingSeaPonyNotes[i] = findDrivingSeaPonyNote(songPosition);
    }

    private Note findDrivingSeaPonyNote(double songPosition)
    {
        ChartPlayer chartPlayer = GLOBALS.beatmapPlayer.ChartPlayer;
        if(chartPlayer == null || GLOBALS.beatmapPlayer.Conductor == null)
            return null;

        Note postHitNote = null;
        Note approachNote = null;
        double closestApproachTime = double.PositiveInfinity;

        foreach(Note note in chartPlayer.Notes)
        {
            if(!TryGetSeaponyAction(note, out SeaponyAction action))
                continue;

            double approachDuration = getSeaponyApproachDuration(action, note);
            double despawnDelay = getSeaponyDespawnDelay(action, note);
            double approachStart = note.SongPosition - approachDuration;
            double despawnEnd = note.SongPosition + despawnDelay;

            if(songPosition >= note.SongPosition && songPosition < despawnEnd)
            {
                if(postHitNote == null || note.SongPosition >= postHitNote.SongPosition)
                    postHitNote = note;

                continue;
            }

            if(songPosition >= approachStart && songPosition < note.SongPosition)
            {
                double approachTime = note.SongPosition - songPosition;
                if(approachTime < closestApproachTime)
                {
                    closestApproachTime = approachTime;
                    approachNote = note;
                }
            }
        }

        return postHitNote ?? approachNote;
    }

    private Note findDrivingBackgroundNote(double songPosition)
    {
        ChartPlayer chartPlayer = GLOBALS.beatmapPlayer.ChartPlayer;
        if(chartPlayer == null || GLOBALS.beatmapPlayer.Conductor == null)
            return null;

        Note drivingNote = null;
        foreach(Note note in chartPlayer.Notes)
        {
            if(!TryGetSeaponyAction(note, out SeaponyAction action))
                continue;

            double scrollEnd = note.SongPosition + getBackgroundScrollDuration(action, note);
            if(songPosition >= note.SongPosition && songPosition <= scrollEnd)
                drivingNote = note;

            if(note.SongPosition > songPosition)
                break;
        }

        return drivingNote;
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

        return crotchet;
    }

    private void resetDrivingSeaPonyNotes()
    {
        Array.Clear(_drivingSeaPonyNotes, 0, _drivingSeaPonyNotes.Length);
    }

    private void resetSeaPonyTimelineState()
    {
        _authorizedRollNotes.Clear();
        _successfulReactionNotes.Clear();
        _perfectReactionNotes.Clear();
        resetDrivingSeaPonyNotes();
        _drivingBackgroundNote = null;
        _lastSeaPonySongPosition = double.NaN;

        for(int i = 0; i < SeaPonyCount; i++)
        {
            if(_seaPonies[i] != null)
            {
                _seaPonies[i].Position = _seaPonyBasePositions[i];
                _seaPonies[i].Rotation = 0;
                _seaPonies[i].Scale = Vector2.One * SCALE;
                if(_seaPonies[i].sprite != null)
                    _seaPonies[i].sprite.Effects = SpriteEffects.None;
            }

            RhythmVisualUtils.ForceAnimationState(_seaPoniesAnimationStates[i], "idle");
        }
    }

    private void applyBaseStateForIdleSeaPonies(double songPosition)
    {
        for(int i = 0; i < SeaPonyCount; i++)
        {
            if(_drivingSeaPonyNotes[i] != null)
                continue;

            RhythmVisualUtils.ForceAnimationState(_seaPoniesAnimationStates[i], "idle");

            if(_seaPonies[i] != null)
            {
                _seaPonies[i].Position = _seaPonyBasePositions[i];
                _seaPonies[i].Scale = Vector2.One * SCALE;
                if(_seaPonies[i].sprite != null)
                    _seaPonies[i].sprite.Effects = SpriteEffects.None;

                _seaPonies[i].Rotation = getStableSeaPonyRotation(i, songPosition);
            }
        }
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
            _ => crotchet
        };
    }

    private double getCrotchetAt(Note note)
    {
        if(note == null)
            return getMaxCrotchet();

        return GLOBALS.beatmapPlayer?.GetCrotchetAt(note.SongPosition) ?? 0.6;
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
    }

    private float _elapsed = 0; 
    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        updateSeaPonies(gameTime);
        _infiniteScrollBg?.Update(gameTime);
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
        }

        updateDrivingSeaPonyNotes(songPos);
        _drivingBackgroundNote = findDrivingBackgroundNote(songPos);
        playStartCues(songPos, gameTime.ElapsedGameTime.TotalSeconds);
        applyBaseStateForIdleSeaPonies(songPos);

        for(int i = 0; i < SeaPonyCount; i++)
        {
            _seaPoniesAnimationStates[i]?.Update(gameTime);
            _seaPoniesVisualNotes[i]?.Update(songPos);
        }

        _infiniteScrollBgVisualNotes?.Update(songPos);
        _lastSeaPonySongPosition = songPos;
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

        foreach(Note note in GLOBALS.beatmapPlayer.ChartPlayer.Notes)
        {
            double crotchet = getCrotchetAt(note);
            if(isFirstActionInSequence(note, SeaponyAction.Roll))
            {
                playSfxOnForwardCross(previousSongPosition, note.SongPosition - FirstCueLeadBeats * crotchet, songPosition, "SFX/BubbleHeavy.wav");
                playSfxOnForwardCross(previousSongPosition, note.SongPosition - SecondCueLeadBeats * crotchet, songPosition, "SFX/BubbleHeavy.wav");
            }
            else if(isFirstActionInSequence(note, SeaponyAction.TapTap))
            {
                playSfxOnForwardCross(previousSongPosition, note.SongPosition - FirstCueLeadBeats * crotchet, songPosition, "SFX/seapony_parade_roll.wav");
                playSfxOnForwardCross(previousSongPosition, note.SongPosition - SecondCueLeadBeats * crotchet, songPosition, "SFX/seapony_parade_roll.wav");
            }
        }
    }

    private bool isFirstActionInSequence(Note note, SeaponyAction expectedAction)
    {
        if(!IsSeaponyAction(note, expectedAction) || GLOBALS.beatmapPlayer.ChartPlayer == null)
            return false;

        Note previousSeaponyNote = null;
        foreach(Note candidate in GLOBALS.beatmapPlayer.ChartPlayer.Notes)
        {
            if(ReferenceEquals(candidate, note))
                break;

            if(TryGetSeaponyAction(candidate, out _))
                previousSeaponyNote = candidate;
        }

        return !IsSeaponyAction(previousSeaponyNote, expectedAction);
    }

    private void playSfxOnForwardCross(double previousSongPosition, double cuePosition, double currentSongPosition, string filePath)
    {
        if(previousSongPosition < cuePosition && currentSongPosition >= cuePosition)
            SFX.Play(this, filePath, 4);
    }

    public override void Draw(SpriteBatch spriteBatch)
    {

        GLOBALS.graphicsDevice?.Clear(Color.DarkBlue);

        _devUIRenderer.Label(spriteBatch, "You", _seaPonies[1].Position + new Vector2(-25, -225), Color.White, 7);
        _infiniteScrollBg?.Draw(spriteBatch);

        base.Draw(spriteBatch);

    }

}


