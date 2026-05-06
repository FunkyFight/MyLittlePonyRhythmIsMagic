using System;
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

    private readonly Game1 game;

    public GameObject SeeSaw1;
    public GameObject SeeSaw2;
    public GameObject Applejack;
    public GameObject Rainbow;

    private AnimationStateMachine RainbowState;
    private AnimationStateMachine ApplejackState;
    private bool[] requestBop = [true, true];
    private readonly int ponyScale = 3;
    private readonly int seeSawScale = 1;

    private Vector2 applejackOuterPos;
    private Vector2 applejackInnerPos;
    private Vector2 applejackExitPos;
    private Vector2 rainbowOuterPos;
    private Vector2 rainbowInnerPos;

    private TrailGameObject rainbowTrail;

    private SeeSawDirector _director;
    private ChartPlayer _reactionChartPlayer;
    private int _lastTempoMapBeat = int.MinValue;

    public SeeSawScene(Game1 game) : base("See Saw")
    {
        this.game = game;
    }

    public override void OnLoad()
    {
        Viewport vp = GLOBALS.graphicsDevice.Viewport;

        SeeSaw1 = new GameObject(new Sprite(GLOBALS.main_atlas.GetRegion(MainAtlas.Sprite_0001)));
        SeeSaw2 = new GameObject(new Sprite(GLOBALS.main_atlas.GetRegion(MainAtlas.Sprite_0002)));
        Applejack = new GameObject(new Sprite(GLOBALS.main_atlas.GetRegion(MainAtlas.Applejack_afk1)));
        Rainbow = new GameObject(new Sprite(GLOBALS.main_atlas.GetRegion(MainAtlas.Rainbowdash_idle1)));

        SeeSaw1.sprite.CenterOrigin();
        SeeSaw2.sprite.CenterOrigin();

        SeeSaw1.Scale = new Vector2(seeSawScale, seeSawScale);
        SeeSaw2.Scale = new Vector2(seeSawScale + 0.5f, seeSawScale);
        Applejack.Scale = new Vector2(ponyScale, ponyScale);
        Rainbow.Scale = new Vector2(ponyScale, ponyScale);

        SeeSaw2.Position = new Vector2(vp.Width * 0.4914f, vp.Height * 0.8008f);
        SeeSaw1.Position = new Vector2(vp.Width / 2, vp.Height / 2 + (vp.Height / 5) * 1.5f);
        Rainbow.Position = new Vector2(vp.Width * 0.6351f, vp.Height * 0.4902f);
        Applejack.Position = new Vector2(vp.Width * 0.0289f, vp.Height * 0.6139f);

        applejackExitPos = Applejack.Position;
        applejackOuterPos = new Vector2(vp.Width * 0.1984f, vp.Height * 0.5444f);
        applejackInnerPos = new Vector2(vp.Width * 0.3406f, vp.Height * 0.5042f);
        rainbowOuterPos = Rainbow.Position;
        rainbowInnerPos = new Vector2(vp.Width * 0.4953f, vp.Height * 0.4375f);

        rainbowTrail = new TrailGameObject(GLOBALS.main_atlas.CreateSprite(MainAtlas.Rainbow_trail));
        rainbowTrail.sprite.CenterOrigin();
        rainbowTrail.DrawCurrentSprite = false;
        rainbowTrail.EmitTrail = false;
        rainbowTrail.MinDistance = 0.05f;
        rainbowTrail.sprite.DrawOffset += new Vector2(20, 20);
        

        ResetActors();
        SetupAnimations();

        if (GLOBALS.beatmapPlayer != null)
            GLOBALS.beatmapPlayer.BeatmapStarted += OnBeatmapStarted;

        if (GLOBALS.beatmapPlayer?.ChartPlayer != null && GLOBALS.beatmapPlayer.Conductor != null)
            OnBeatmapStarted();

        GameObjects.Add(rainbowTrail);
        GameObjects.Add(Rainbow);
        GameObjects.Add(Applejack);
        GameObjects.Add(SeeSaw1);
        GameObjects.Add(SeeSaw2);
    }

    private void OnBeatmapStarted()
    {
        ResetActors();
        SetupTimelineAndDirector();
        _lastTempoMapBeat = int.MinValue;
        SetupReactionFeedbacks();
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
        double baseCrotchet = GLOBALS.beatmapPlayer.GetCrotchetAt(GLOBALS.beatmapPlayer.Conductor.SongPosition);
        SeeSawTimeline timeline = SeeSawChartCompiler.Compile(chartPlayer.Notes, GLOBALS.beatmapPlayer.GetBeatAt, GLOBALS.beatmapPlayer.GetSongPositionAtBeat, GLOBALS.beatmapPlayer.GetCrotchetAt);
        SeeSawPathCatalog pathCatalog = new(layout);

        _director = new SeeSawDirector(
            timeline,
            new SeeSawActorController(SeeSawActor.RainbowDash, Rainbow, RainbowState),
            new SeeSawActorController(SeeSawActor.Applejack, Applejack, ApplejackState),
            SeeSaw2,
            rainbowTrail,
            pathCatalog,
            new SeeSawCameraController(sceneCamera),
            new SeeSawSoundScheduler(this),
            GLOBALS.beatmapPlayer.GetBeatAt,
            GLOBALS.beatmapPlayer.GetCrotchetAt,
            baseCrotchet);
        _director.Reset();
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
        SeeSaw2.Rotation = MathHelper.ToRadians(BeamTiltDegrees);
        Rainbow.Rotation = MathHelper.ToRadians(BeamTiltDegrees);
        Applejack.Rotation = 0;
        sceneCamera.Position = Vector2.Zero;
    }

    public override void OnUnload()
    {
        if (GLOBALS.beatmapPlayer != null)
            GLOBALS.beatmapPlayer.BeatmapStarted -= OnBeatmapStarted;

        if (_reactionChartPlayer != null)
        {
            _reactionChartPlayer.NoteReactedWithNote -= OnNoteReacted;
            _reactionChartPlayer = null;
        }

        _director?.Reset();
        _director = null;
        sceneCamera.Position = Vector2.Zero;
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        UpdateTempoMapBeatEvents();
        RainbowState?.Update(gameTime);
        ApplejackState?.Update(gameTime);

        if (GLOBALS.beatmapPlayer?.Conductor != null && _director != null)
            _director.Update(GLOBALS.beatmapPlayer.Conductor.SongPosition, gameTime);
    }

    private void UpdateTempoMapBeatEvents()
    {
        if (GLOBALS.beatmapPlayer?.Conductor == null)
            return;

        double beat = GLOBALS.beatmapPlayer.GetBeatAt(GLOBALS.beatmapPlayer.Conductor.SongPosition);
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
        GLOBALS.graphicsDevice.Clear(Color.Cyan);
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
                        Rainbow.sprite.DrawOffset = new Vector2(0, 20);
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
                        Rainbow.sprite.DrawOffset = new Vector2(100, 170);
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
                        Applejack.sprite = GLOBALS.main_atlas.CreateAnimatedSprite("applejack afk");
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
                        Applejack.sprite = GLOBALS.main_atlas.CreateAnimatedSprite("applejack-land");
                        Applejack.sprite.Rotation = MathHelper.ToRadians(-10);
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
                        Applejack.sprite = GLOBALS.main_atlas.CreateSprite(MainAtlas.Applejack_true_idle);
                        Applejack.sprite.Rotation = MathHelper.ToRadians(-10);
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

        RainbowState.ForceState("idle");
        ApplejackState.ForceState("start_idle");
    }
}
