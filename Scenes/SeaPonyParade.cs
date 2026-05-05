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
using Rhythm.Note;
using Rhythm.Note.Visual;
using TexturePackerMonoGameDefinitions;

public class SeaPonyParade : Scene
{
    // Config
    private const int SCALE = 3;
    private const int SeaPonyCount = 4;
    private const string ActionDataKey = "action";
    private const string SwimAction = "seapony_parade_swim";
    private const string RollAction = "seapony_parade_roll";
    private const string TapTapAction = "seapony_parade_tap_tap";

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
        if(!TryGetSeaponyAction(note, out string action))
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
            case SwimAction:
                _successfulReactionNotes.Add(note);
                break;

            case RollAction:
                _authorizedRollNotes.Add(note);
                break;

            case TapTapAction:
                _successfulReactionNotes.Add(note);
                if(result == NoteReactionResult.PERFECT)
                {
                    _perfectReactionNotes.Add(note);
                    int clapIndex = Random.Shared.Next(1, 5);
                    SFX.Play(this, $"SFX/clap{clapIndex}.wav", 4);
                }
                break;
        }

        if(action != TapTapAction)
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
                conf.WithOffset(new Vector2(0, 2.75f * (_viewport.Height / 5)));
                conf.WithScale(3);
            })
            .AddLine(conf =>
            {
                conf.AddPrototype(new GameObject(GLOBALS.main_atlas.CreateSprite(MainAtlas.Sand3)));
                conf.WithPlacementInterval(0, 0);
                conf.WithScrollMultiplier(0.3f);
                conf.WithOffset(new Vector2(0, 3.15f * (_viewport.Height / 5)));
                conf.WithScale(4);
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
                conf.WithOffset(new Vector2(0, 3.45f * (_viewport.Height / 5)));
                conf.WithScale(3);
            })
            .AddLine(conf =>
            {
                conf.AddPrototype(new GameObject(GLOBALS.main_atlas.CreateSprite(MainAtlas.Sand2)));
                conf.WithPlacementInterval(0, 0);
                conf.WithScrollMultiplier(0.6f);
                conf.WithOffset(new Vector2(485, 3.65f * (_viewport.Height / 5)));
                conf.WithScale(4);
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
                conf.WithOffset(new Vector2(200, 3.9f * (_viewport.Height / 5)));
                conf.WithScale(3);
            })
            .AddLine(conf =>
            {
                conf.AddPrototype(new GameObject(GLOBALS.main_atlas.CreateSprite(MainAtlas.Sand1)));
                conf.WithPlacementInterval(0, 0);
                conf.WithScrollMultiplier(1);
                conf.WithOffset(new Vector2(0, 4.15f * (_viewport.Height / 5)));
                conf.WithScale(4);
            })
            .WithPixelsPerProgress(new Vector2(_viewport.Width / 4, 0))
            .Build();
    }

    private void createSeaPonies()
    {
        for(int i = 0; i < SeaPonyCount; i++)
        {
            // Create object
            GameObject seaPony = new GameObject(GLOBALS.main_atlas.CreateAnimatedSprite("template-pony-idle"));
            seaPony.Position = new Vector2(4 * _viewport.Width / 5 - ((GLOBALS.graphicsDevice.Viewport.Width / 5) * i), _viewport.Height / 2);
            _seaPonyBasePositions[i] = seaPony.Position;
            seaPony.Scale = Vector2.One * SCALE;
            seaPony.sprite.CenterOrigin();
            int seaPonyId = i;

            GameObjects.Add(seaPony);
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
        double crotchet = GLOBALS.beatmapPlayer.Conductor.Crotchet;

        for(int i = 0; i < SeaPonyCount; i++)
        {
            GameObject seaPony = _seaPonies[i];
            AnimationStateMachine animationState = _seaPoniesAnimationStates[i];
            int seaPonyId = i;

            VisualNoteManager<SeaponyVisualNote> visualNoteManager = new VisualNoteManager<SeaponyVisualNote>(chartPlayer, note =>
            {
                if(!TryGetSeaponyAction(note, out string action))
                    return null;

                switch(action)
                {
                    case SwimAction:
                        return new SeaponyVisualNote(note, crotchet, this, seaPony, animationState, seaPonyId, crotchet, despawnDelay: crotchet, hasSuccessfulReactionProvider: hasSuccessfulReaction, canApplyState: () => canSeaPonyApplyState(seaPonyId, note), baseSeaPonyPosition: _seaPonyBasePositions[seaPonyId]);
                    case RollAction:
                        return new SeaponyVisualNote(note, crotchet * 2, this, seaPony, animationState, seaPonyId, crotchet, getRollTargetRotation(note), getRollDespawnDelay(note), rollIndexInSequence: getRollIndexInSequence(note), rollsRemainingInSequence: getRollsRemainingInSequence(note), canRollProvider: canSeaPonyRoll, canApplyState: () => canSeaPonyApplyState(seaPonyId, note), baseSeaPonyPosition: _seaPonyBasePositions[seaPonyId]);
                    case TapTapAction:
                        int tapTapHitsRemaining = getTapTapHitsRemainingInSequence(note);
                        return new SeaponyVisualNote(note, crotchet * 2, this, seaPony, animationState, seaPonyId, crotchet, despawnDelay: getTapTapDespawnDelay(note), tapTapIndexInSequence: getTapTapIndexInSequence(note), tapTapHitsRemainingInSequence: tapTapHitsRemaining, hasSuccessfulReactionProvider: hasSuccessfulReaction, hasPerfectReactionProvider: hasPerfectReaction, canApplyState: () => canSeaPonyApplyState(seaPonyId, note), baseSeaPonyPosition: _seaPonyBasePositions[seaPonyId]);
                }
                return null;
            });
            
            visualNoteManager.LookBehindSeconds = crotchet * 3;
            _seaPoniesVisualNotes[i] = visualNoteManager;
            
        }




        _infiniteScrollBgVisualNotes = new VisualNoteManager<SeaponyBgVisualNote>(chartPlayer, (note) =>
        {
            if(!TryGetSeaponyAction(note, out string action))
                return null;

            return new SeaponyBgVisualNote(note, crotchet, _infiniteScrollBg, getBackgroundScrollDestinationBeat(note), getBackgroundScrollDuration(action), canApplyState: () => ReferenceEquals(_drivingBackgroundNote, note));
        });

        _infiniteScrollBgVisualNotes.LookBehindSeconds = crotchet * 2;
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
            if(!TryGetSeaponyAction(note, out string action))
                continue;

            double approachDuration = getSeaponyApproachDuration(action);
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
            if(!TryGetSeaponyAction(note, out string action))
                continue;

            double scrollEnd = note.SongPosition + getBackgroundScrollDuration(action);
            if(songPosition >= note.SongPosition && songPosition <= scrollEnd)
                drivingNote = note;

            if(note.SongPosition > songPosition)
                break;
        }

        return drivingNote;
    }

    private double getSeaponyApproachDuration(string action)
    {
        double crotchet = GLOBALS.beatmapPlayer.Conductor == null ? 0 : GLOBALS.beatmapPlayer.Conductor.Crotchet;
        return action == RollAction || action == TapTapAction ? crotchet * 2 : crotchet;
    }

    private double getSeaponyDespawnDelay(string action, Note note)
    {
        double crotchet = GLOBALS.beatmapPlayer.Conductor == null ? 0 : GLOBALS.beatmapPlayer.Conductor.Crotchet;
        if(action == RollAction)
            return getRollDespawnDelay(note);

        if(action == TapTapAction)
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

            if(TryGetSeaponyAction(note, out string action) && action == RollAction)
                completedRollCount++;
        }

        return MathHelper.ToRadians(completedRollCount % 4 * 90f);
    }

    private static bool TryGetSeaponyAction(Note note, out string action)
    {
        action = string.Empty;
        return note != null
            && note.AdditionnalData != null
            && note.AdditionnalData.TryGetValue(ActionDataKey, out action)
            && (action == SwimAction || action == RollAction || action == TapTapAction);
    }

    private float getRollTargetRotation(Note note)
    {
        int rollCount = 0;

        foreach(Note candidate in GLOBALS.beatmapPlayer.ChartPlayer.Notes)
        {
            if(candidate.AdditionnalData != null
                && candidate.AdditionnalData.TryGetValue(ActionDataKey, out string action)
                && action == RollAction)
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

            if(candidate.AdditionnalData != null
                && candidate.AdditionnalData.TryGetValue(ActionDataKey, out string action)
                && action == RollAction)
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

            if(candidate.AdditionnalData != null
                && candidate.AdditionnalData.TryGetValue(ActionDataKey, out string action)
                && action == RollAction)
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

            if(candidate.AdditionnalData != null
                && candidate.AdditionnalData.TryGetValue(ActionDataKey, out string action)
                && action == RollAction)
            {
                return candidate.SongPosition - note.SongPosition;
            }

            break;
        }

        foreach(Note candidate in GLOBALS.beatmapPlayer.ChartPlayer.Notes)
        {
            if(candidate.AdditionnalData != null
                && candidate.AdditionnalData.TryGetValue(ActionDataKey, out string action)
                && action == RollAction)
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

        return paddingBeats * GLOBALS.beatmapPlayer.Conductor.Crotchet;
    }

    private int getTapTapIndexInSequence(Note note)
    {
        int index = 0;

        foreach(Note candidate in GLOBALS.beatmapPlayer.ChartPlayer.Notes)
        {
            if(ReferenceEquals(candidate, note))
                break;

            if(IsSeaponyAction(candidate, TapTapAction))
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

            if(IsSeaponyAction(candidate, TapTapAction))
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

            if(IsSeaponyAction(candidate, TapTapAction))
                return candidate.SongPosition - note.SongPosition;

            break;
        }

        return 2 * GLOBALS.beatmapPlayer.Conductor.Crotchet;
    }

    private static bool IsSeaponyAction(Note note, string expectedAction)
    {
        return note?.AdditionnalData != null
            && note.AdditionnalData.TryGetValue(ActionDataKey, out string action)
            && action == expectedAction;
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

    private double getBackgroundScrollDuration(string action)
    {
        double crotchet = GLOBALS.beatmapPlayer.Conductor == null ? 0 : GLOBALS.beatmapPlayer.Conductor.Crotchet;
        return action switch
        {
            RollAction => crotchet * 0.5,
            TapTapAction => crotchet * 2,
            _ => crotchet
        };
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
        applyBaseStateForIdleSeaPonies(songPos);

        for(int i = 0; i < SeaPonyCount; i++)
        {
            _seaPoniesAnimationStates[i]?.Update(gameTime);
            _seaPoniesVisualNotes[i]?.Update(songPos);
        }

        _infiniteScrollBgVisualNotes?.Update(songPos);
        _lastSeaPonySongPosition = songPos;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {

        GLOBALS.graphicsDevice?.Clear(Color.DarkBlue);

        _devUIRenderer.Label(spriteBatch, "You", _seaPonies[1].Position + new Vector2(-35, -225), Color.White, 7);
        _infiniteScrollBg?.Draw(spriteBatch);

        base.Draw(spriteBatch);

    }

}


