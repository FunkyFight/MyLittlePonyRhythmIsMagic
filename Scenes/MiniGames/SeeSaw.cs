using System;
using System.Collections.Generic;
using GameCore.Animation;
using GameCore.GameObjects;
using GameCore.Graphics;
using GameCore.Scenes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MLP_RiM;
using Rhythm.Note;
using TexturePackerMonoGameDefinitions;

/// <summary>
/// Runtime scene for the See-Saw rhythm game.
/// </summary>
/// <remarks>
/// The scene owns sprites, viewport anchors, lifecycle, and animation state machines. See-Saw
/// choreography is compiled into a relay timeline and evaluated by <see cref="SeeSawDirector"/>.
/// </remarks>
public class SeeSawScene : Scene
{
    private const float BeamTiltDegrees = 10f;
    private const float ApplejackTiltDegrees = -15f;
    private const float BackgroundPropsScale = 8f;
    private const float TreeScale = 12f;
    private readonly Game1 game;

    public GameObject SeeSaw1;
    public GameObject SeeSaw2;
    public GameObject Applejack;
    public GameObject Rainbow;
    public GameObject Tree;
    public GameObject Fences;
    public GameObject Arbustes;

    private AnimationStateMachine RainbowState;
    private AnimationStateMachine ApplejackState;
    private AnimationStateMachine SeeSawState;
    private bool[] requestBop = [true, true];
    private readonly float ponyScale = 4.5f;
    private readonly float seeSawScale = 4.5f;

    private Vector2 applejackOuterPos;
    private Vector2 applejackInnerPos;
    private Vector2 applejackExitPos;
    private Vector2 rainbowOuterPos;
    private Vector2 rainbowInnerPos;

    private TrailGameObject rainbowTrail;
    private Texture2D groundTexture;

    private SeeSawDirector _director;
    private ChartPlayer _reactionChartPlayer;
    private int _lastTempoMapBeat = int.MinValue;

    public SeeSawScene() : this(null)
    {
    }

    public SeeSawScene(Game1 game) : base("See Saw")
    {
        this.game = game;
    }

    public override void OnLoad()
    {
        Viewport vp = GLOBALS.graphicsDevice.Viewport;

        SeeSaw1 = new GameObject(new Sprite(GLOBALS.main_atlas.GetRegion(MainAtlas.Stand)));
        SeeSaw2 = new GameObject(new Sprite(GLOBALS.main_atlas.GetRegion(MainAtlas.Planks_idle)));
        Applejack = new GameObject(new Sprite(GLOBALS.main_atlas.GetRegion(MainAtlas.Applejack_afk1)));
        Rainbow = new GameObject(new Sprite(GLOBALS.main_atlas.GetRegion(MainAtlas.Rainbowdash_idle1)));
        Tree = new GameObject(GLOBALS.main_atlas.CreateSprite(MainAtlas.Tree));
        Fences = new GameObject(GLOBALS.main_atlas.CreateSprite(MainAtlas.Fences));
        Arbustes = new GameObject(GLOBALS.main_atlas.CreateSprite(MainAtlas.Arbustes));

        SeeSaw1.sprite.CenterOrigin();
        SeeSaw2.sprite.CenterOrigin();

        SeeSaw1.Scale = new Vector2(seeSawScale, seeSawScale);
        SeeSaw2.Scale = new Vector2(seeSawScale + 0.5f, seeSawScale);
        Applejack.Scale = new Vector2(ponyScale, ponyScale);
        Rainbow.Scale = new Vector2(ponyScale, ponyScale);
        Tree.Scale = new Vector2(TreeScale, TreeScale);
        Fences.Scale = new Vector2(BackgroundPropsScale, BackgroundPropsScale);
        Arbustes.Scale = new Vector2(BackgroundPropsScale, BackgroundPropsScale);

        Tree.sprite.CenterOrigin();
        Fences.sprite.CenterOrigin();
        Arbustes.sprite.CenterOrigin();

        SeeSaw2.Position = new Vector2(vp.Width * 0.5008f, vp.Height * 0.8064f);
        SeeSaw1.Position = new Vector2(vp.Width / 2, vp.Height / 2 + (vp.Height / 5) * 1.5f);
        Rainbow.Position = new Vector2(vp.Width * 0.6305f, vp.Height * 0.55f);
        Applejack.Position = new Vector2(vp.Width * 0.0351f, vp.Height * 0.6176f);

        // Temporary placeholders: replace with final coordinates.
        Tree.Position = new Vector2(vp.Width * 1.0042f, vp.Height * 0.5537f);
        Fences.Position = new Vector2(vp.Width * 0.9146f, vp.Height * 0.8237f);
        Arbustes.Position = new Vector2(vp.Width * 0.0627f, vp.Height * 0.67f);

        applejackExitPos = Applejack.Position;
        applejackOuterPos = new Vector2(vp.Width * 0.1906f, vp.Height * 0.6708f);
        applejackInnerPos = new Vector2(vp.Width * 0.3008f, vp.Height * 0.6292f);
        rainbowOuterPos = Rainbow.Position;
        rainbowInnerPos = new Vector2(vp.Width * 0.5203f, vp.Height * 0.5069f);

        rainbowTrail = new TrailGameObject(GLOBALS.main_atlas.CreateSprite(MainAtlas.Rainbow_trail));
        rainbowTrail.sprite.CenterOrigin();
        rainbowTrail.DrawCurrentSprite = false;
        rainbowTrail.EmitTrail = false;
        rainbowTrail.MinDistance = 0.05f;
        rainbowTrail.sprite.DrawOffset += new Vector2(20, 20);

        groundTexture = new Texture2D(GLOBALS.graphicsDevice, 1, 1);
        groundTexture.SetData([Color.White]);

        ResetActors();
        SetupAnimations();

        if (GLOBALS.beatmapPlayer != null)
        {
            GLOBALS.beatmapPlayer.BeatmapStarted += OnBeatmapStarted;
            GLOBALS.beatmapPlayer.BeatmapLoopAppended += OnBeatmapLoopAppended;
            GLOBALS.beatmapPlayer.BeatmapNotesRemoved += OnBeatmapNotesRemoved;
        }

        if (GLOBALS.beatmapPlayer?.ChartPlayer != null && GLOBALS.beatmapPlayer.Conductor != null)
            OnBeatmapStarted();

        GameObjects.Add(rainbowTrail);
        GameObjects.Add(Tree);
        GameObjects.Add(Fences);
        GameObjects.Add(Arbustes);
        GameObjects.Add(Rainbow);
        GameObjects.Add(Applejack);
        GameObjects.Add(SeeSaw2);
        GameObjects.Add(SeeSaw1);
    }

    private void OnBeatmapStarted()
    {
        ResetActors();
        SetupTimelineAndDirector();
        _lastTempoMapBeat = int.MinValue;
        SetupReactionFeedbacks();
    }

    private void OnBeatmapLoopAppended()
    {
        SetupTimelineAndDirector();
    }

    private void OnBeatmapNotesRemoved(IReadOnlyCollection<Note> notes)
    {
        _director?.RemoveEventsForNotes(notes);
    }

    private void SetupTimelineAndDirector()
    {
        ChartPlayer chartPlayer = GLOBALS.beatmapPlayer.ChartPlayer;
        if (chartPlayer == null || GLOBALS.beatmapPlayer.Conductor == null)
        {
            _director = null;
            return;
        }

        SeeSawLayout layout = new(applejackOuterPos, applejackInnerPos, applejackExitPos, rainbowOuterPos, rainbowInnerPos);
        double currentSeconds = GLOBALS.beatmapPlayer.Clock?.SongSeconds ?? GLOBALS.beatmapPlayer.GameplaySongPosition;
        double baseCrotchet = GLOBALS.beatmapPlayer.GetCrotchetAt(currentSeconds);
        SeeSawTimeline timeline = SeeSawChartCompiler.Compile(chartPlayer.Notes, GLOBALS.beatmapPlayer.GetBeatAt, GLOBALS.beatmapPlayer.GetSongPositionAtBeat, GLOBALS.beatmapPlayer.GetCrotchetAt, ChartTiming.GetLeadInBeats(GLOBALS.beatmapPlayer.CurrentChart));
        SeeSawPathCatalog pathCatalog = new(layout);

        _director = new SeeSawDirector(
            timeline,
            new SeeSawActorController(SeeSawActor.RainbowDash, Rainbow, RainbowState),
            new SeeSawActorController(SeeSawActor.Applejack, Applejack, ApplejackState),
            new SeeSawBeamController(SeeSaw2, SeeSawState),
            rainbowTrail,
            pathCatalog,
            new SeeSawCameraController(sceneCamera),
            new SeeSawSoundScheduler(this),
            new SeeSawCameraEffectController(sceneCamera),
            GLOBALS.beatmapPlayer.GetBeatAt,
            GLOBALS.beatmapPlayer.GetCrotchetAt,
            baseCrotchet);
        _director.Reset();
        SyncDirectorToCurrentSongPosition();
    }

    private void SyncDirectorToCurrentSongPosition()
    {
        if (_director == null || GLOBALS.beatmapPlayer?.Conductor == null)
            return;

        double songSeconds = GLOBALS.beatmapPlayer.Clock?.SongSeconds ?? GLOBALS.beatmapPlayer.GameplaySongPosition;
        double beat = GLOBALS.beatmapPlayer.Clock?.Beat ?? GLOBALS.beatmapPlayer.GetBeatAt(songSeconds);
        _director.SyncTo(beat, songSeconds);
    }

    private void SetupReactionFeedbacks()
    {
        if (_reactionChartPlayer != null)
            _reactionChartPlayer.NoteReactedWithNote -= OnNoteReacted;

        _reactionChartPlayer = GLOBALS.beatmapPlayer.ChartPlayer;
        if (_reactionChartPlayer != null)
            _reactionChartPlayer.NoteReactedWithNote += OnNoteReacted;
    }

    private void OnNoteReacted(NoteReactionResult result, Note note)
    {
        _director?.ApplyReaction(result, note);
    }

    private void ResetActors()
    {
        Rainbow.Position = rainbowOuterPos;
        Applejack.Position = applejackExitPos;
        SeeSaw2.Rotation = 0f;
        Rainbow.Rotation = MathHelper.ToRadians(BeamTiltDegrees);
        Applejack.Rotation = 0f;
        sceneCamera.Position = Vector2.Zero;
    }

    public override void OnUnload()
    {
        if (GLOBALS.beatmapPlayer != null)
        {
            GLOBALS.beatmapPlayer.BeatmapStarted -= OnBeatmapStarted;
            GLOBALS.beatmapPlayer.BeatmapLoopAppended -= OnBeatmapLoopAppended;
            GLOBALS.beatmapPlayer.BeatmapNotesRemoved -= OnBeatmapNotesRemoved;
        }

        if (_reactionChartPlayer != null)
        {
            _reactionChartPlayer.NoteReactedWithNote -= OnNoteReacted;
            _reactionChartPlayer = null;
        }

        _director?.Reset();
        _director = null;
        groundTexture?.Dispose();
        groundTexture = null;
        sceneCamera.Position = Vector2.Zero;
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        UpdateTempoMapBeatEvents();
        RainbowState?.Update(gameTime);
        ApplejackState?.Update(gameTime);
        SeeSawState?.Update(gameTime);

        bool isEmptyTail = GLOBALS.beatmapPlayer?.IsContinuingEmptyBeatmap == true;
        if (isEmptyTail)
        {
            UpdateEmptyTailAnimations();
            return;
        }

        if (GLOBALS.beatmapPlayer?.Conductor != null && _director != null)
        {
            double songSeconds = GLOBALS.beatmapPlayer.Clock?.SongSeconds ?? GLOBALS.beatmapPlayer.GameplaySongPosition;
            double beat = GLOBALS.beatmapPlayer.Clock?.Beat ?? GLOBALS.beatmapPlayer.GetBeatAt(songSeconds);
            _director.Update(beat, songSeconds, gameTime);
        }
    }

    private void UpdateEmptyTailAnimations()
    {
        SettleState(RainbowState, "idle", "jump", "fall", "land", "fail");

        bool applejackAtExit = Vector2.DistanceSquared(Applejack.Position, applejackExitPos) <= 1f;
        string applejackIdleState = applejackAtExit ? "start_idle" : "idle";
        if (SettleState(ApplejackState, applejackIdleState, "jump", "fall", "land") && !applejackAtExit)
            Applejack.Rotation = MathHelper.ToRadians(ApplejackTiltDegrees);

        string beamState = SeeSawState?.CurrentState?.Name;
        if (SeeSawState?.StateProgress < 1f)
            return;

        if (beamState == "land_left")
            SeeSawState.ForceState("idle_left");
        else if (beamState == "land_right")
            SeeSawState.ForceState("idle_right");
    }

    private static bool SettleState(AnimationStateMachine stateMachine, string idleState, params string[] eventStates)
    {
        if (stateMachine?.CurrentState == null)
            return false;

        string currentState = stateMachine.CurrentState.Name;
        bool waitForCompletion = currentState == "land" || currentState == "fail";
        if (waitForCompletion && stateMachine.StateProgress < 1f)
            return false;

        foreach (string eventState in eventStates)
        {
            if (currentState != eventState)
                continue;

            stateMachine.ForceState(idleState);
            return true;
        }

        return false;
    }

    private void UpdateTempoMapBeatEvents()
    {
        if (GLOBALS.beatmapPlayer?.Conductor == null)
            return;

        double songSeconds = GLOBALS.beatmapPlayer.Clock?.SongSeconds ?? GLOBALS.beatmapPlayer.GameplaySongPosition;
        double beat = GLOBALS.beatmapPlayer.Clock?.Beat ?? GLOBALS.beatmapPlayer.GetBeatAt(songSeconds);
        if (double.IsNaN(beat) || double.IsInfinity(beat))
            return;

        int flooredBeat = (int)Math.Floor(beat);
        if (flooredBeat == _lastTempoMapBeat)
            return;

        _lastTempoMapBeat = flooredBeat;
        requestBop = [true, true];
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        GLOBALS.graphicsDevice.Clear(new Color(190, 220, 235));
        Viewport vp = GLOBALS.graphicsDevice.Viewport;
        spriteBatch.Draw(groundTexture, new Rectangle(0, (int)(vp.Height * 0.905f), vp.Width, (int)(vp.Height * 0.15f)), new Color(147, 176, 160, 255));
        base.Draw(spriteBatch);
    }

    private void SetupAnimations()
    {
        RainbowState = new AnimationStateMachine()
            .AddState(
                new AnimationState(
                    "idle",
                    5,
                    onEnter: () =>
                    {
                        Rainbow.sprite = GLOBALS.main_atlas.CreateAnimatedSprite("rainbowdash-idle");
                    },
                    onUpdate: gt =>
                    {
                        if (requestBop[1])
                        {
                            requestBop[1] = false;
                            ((AnimatedSprite)Rainbow.sprite).Restart();
                        }
                    }
                )
            )
            .AddState(
                new AnimationState(
                    name: "jump",
                    duration: 1,
                    onEnter: () =>
                    {
                        Rainbow.sprite = GLOBALS.main_atlas.CreateSprite(MainAtlas.Rainbowdash_jump1);
                    },
                    isLooping: true
                )
            )
            .AddState(
                new AnimationState(
                    name: "land",
                    duration: 1f,
                    onEnter: () =>
                    {
                        Rainbow.Rotation = MathHelper.ToRadians(BeamTiltDegrees);
                        Rainbow.sprite = GLOBALS.main_atlas.CreateAnimatedSprite("rainbowdash-land");
                        Rainbow.sprite.DrawOffset = new Vector2(0, 55);
                        ((AnimatedSprite)Rainbow.sprite).Restart();
                    },
                    isLooping: false
                )
            )
            .AddState(
                new AnimationState(
                    name: "fall",
                    duration: 1,
                    onEnter: () =>
                    {
                        Rainbow.sprite = GLOBALS.main_atlas.CreateSprite(MainAtlas.Rainbowdash_jump2);
                        Rainbow.sprite.DrawOffset = new Vector2(0, 100);
                        Rainbow.sprite.CenterOrigin();
                    },
                    onExit: () => { },
                    isLooping: false
                )
            )
            .AddState(
                new AnimationState(
                    name: "fail",
                    duration: 0.5f,
                    onEnter: () =>
                    {
                        Rainbow.sprite = GLOBALS.main_atlas.CreateSprite(MainAtlas.Rainbowdash_fail);
                        Rainbow.sprite.DrawOffset = new Vector2(150, 240);
                        Rainbow.sprite.CenterOrigin();
                    },
                    isLooping: false
                )
            )
            .SetGlobalUpdate(globalUpdate: gt =>
            {
                if (Rainbow.sprite is AnimatedSprite animatedSprite)
                    animatedSprite.Update(gt);

                return true;
            })
            .Build();

        ApplejackState = new AnimationStateMachine()
            .AddState(
                new AnimationState(
                    "start_idle",
                    5,
                    onEnter: () =>
                    {
                        Applejack.Rotation = 0f;
                        Applejack.sprite = GLOBALS.main_atlas.CreateAnimatedSprite("applejack afk");
                        Applejack.sprite.DrawOffset = Vector2.Zero;
                    },
                    onUpdate: gt =>
                    {
                        if (requestBop[0])
                        {
                            requestBop[0] = false;
                            ((AnimatedSprite)Applejack.sprite).Restart();
                        }
                    }
                )
            )
            .AddState(
                new AnimationState(
                    name: "jump",
                    duration: 1,
                    onEnter: () =>
                    {
                        Applejack.Rotation = MathHelper.ToRadians(ApplejackTiltDegrees);
                        Applejack.sprite = GLOBALS.main_atlas.CreateSprite(MainAtlas.Applejack_jump1);
                    },
                    isLooping: false
                )
            )
            .AddState(
                new AnimationState(
                    name: "fall",
                    duration: 1,
                    onEnter: () =>
                    {
                        Applejack.Rotation = MathHelper.ToRadians(ApplejackTiltDegrees);
                        Applejack.sprite = GLOBALS.main_atlas.CreateSprite(MainAtlas.Applejack_jump2);
                    },
                    isLooping: false
                )
            )
            .AddState(
                new AnimationState(
                    name: "land",
                    duration: 1,
                    onEnter: () =>
                    {
                        Applejack.Rotation = MathHelper.ToRadians(ApplejackTiltDegrees);
                        Applejack.sprite = GLOBALS.main_atlas.CreateAnimatedSprite("applejack-land");
                        ((AnimatedSprite)Applejack.sprite).IsLooping = false;
                        ((AnimatedSprite)Applejack.sprite).Restart();
                    },
                    isLooping: false
                )
            )
            .AddState(
                new AnimationState(
                    name: "idle",
                    duration: 1,
                    onEnter: () =>
                    {
                        Applejack.Rotation = 0f;
                        Applejack.sprite = GLOBALS.main_atlas.CreateSprite(MainAtlas.Applejack_true_idle);
                    },
                    isLooping: false
                )
            )
            .SetGlobalUpdate(globalUpdate: gt =>
            {
                if (Applejack.sprite is AnimatedSprite animatedSprite)
                    animatedSprite.Update(gt);

                return true;
            })
            .Build();

        SeeSawState = new AnimationStateMachine()
            .AddState(
                new AnimationState(
                    name: "idle_right",
                    duration: 1,
                    onEnter: () =>
                    {
                        SeeSaw2.sprite = GLOBALS.main_atlas.CreateSprite(MainAtlas.Planks_left4);
                        SeeSaw2.sprite.Effects = SpriteEffects.None;
                        SeeSaw2.sprite.CenterOrigin();
                    },
                    isLooping: false
                )
            )
            .AddState(
                new AnimationState(
                    name: "idle_left",
                    duration: 1,
                    onEnter: () =>
                    {
                        SeeSaw2.sprite = GLOBALS.main_atlas.CreateSprite(MainAtlas.Planks_left4);
                        SeeSaw2.sprite.Effects = SpriteEffects.FlipHorizontally;
                        SeeSaw2.sprite.CenterOrigin();
                    },
                    isLooping: false
                )
            )
            .AddState(
                new AnimationState(
                    name: "land_right",
                    duration: 1,
                    onEnter: () =>
                    {
                        SeeSaw2.sprite = GLOBALS.main_atlas.CreateAnimatedSprite("planks_left");
                        SeeSaw2.sprite.Effects = SpriteEffects.None;
                        SeeSaw2.sprite.CenterOrigin();
                        ((AnimatedSprite)SeeSaw2.sprite).IsLooping = false;
                        ((AnimatedSprite)SeeSaw2.sprite).Restart();
                    },
                    isLooping: false
                )
            )
            .AddState(
                new AnimationState(
                    name: "land_left",
                    duration: 1,
                    onEnter: () =>
                    {
                        SeeSaw2.sprite = GLOBALS.main_atlas.CreateAnimatedSprite("planks_left");
                        SeeSaw2.sprite.Effects = SpriteEffects.FlipHorizontally;
                        SeeSaw2.sprite.CenterOrigin();
                        ((AnimatedSprite)SeeSaw2.sprite).IsLooping = false;
                        ((AnimatedSprite)SeeSaw2.sprite).Restart();
                    },
                    isLooping: false
                )
            )
            .SetGlobalUpdate(globalUpdate: gt =>
            {
                if (SeeSaw2.sprite is AnimatedSprite animatedSprite)
                    animatedSprite.Update(gt);

                return true;
            })
            .Build();

        RainbowState.ForceState("idle");
        ApplejackState.ForceState("start_idle");
        SeeSawState.ForceState("idle_right");
    }
}
