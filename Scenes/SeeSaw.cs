using System;
using System.Numerics;
using GameCore;
using GameCore.Animation;
using GameCore.GameObjects;
using GameCore.Graphics;
using GameCore.Scenes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MLP_RiM;
using Rhythm.Note;
using Rhythm.Note.Visual;
using TexturePackerMonoGameDefinitions;
using System.Collections.Generic;
using Vector2 = System.Numerics.Vector2;

public class SeeSawScene : Scene
{
    private Game1 game;
    public GameObject SeeSaw1;
    public GameObject SeeSaw2;
    public GameObject Applejack;
    public GameObject Rainbow;

    private AnimationStateMachine RainbowState;
    private AnimationStateMachine ApplejackState;
    private VisualNoteManager<SeeSawVisualNote> seeSawVisuals;
    private Dictionary<SeeSawJumper, GameObject> seeSawJumpers;
    private Dictionary<SeeSawJumper, AnimationStateMachine> seeSawAnimationStates;
    private bool[] requestBop = [true, true];

    private int ponyScale = 3;
    private int seeSawScale = 1;

    public short turn = 0;

    private System.Numerics.Vector2 applejackOuterPos;
    private System.Numerics.Vector2 applejackInnerPos;
    private System.Numerics.Vector2 applejackExitPos;
    private System.Numerics.Vector2 rainbowOuterPos;
    private System.Numerics.Vector2 rainbowInnerPos;
    private ChartPlayer _visualChartPlayer;
    private Note _drivingVisualNote;
    private double _lastSongPosition = double.NaN;

    public SeeSawScene(Game1 game) : base("See Saw")
    {
        this.game = game;
    }

    public override void OnLoad()
    {
        Viewport vp = GLOBALS.graphicsDevice.Viewport;
        
        SeeSaw1 = new GameObject(new GameCore.Graphics.Sprite(GLOBALS.main_atlas.GetRegion(MainAtlas.Sprite_0001)));
        SeeSaw2 = new GameObject(new GameCore.Graphics.Sprite(GLOBALS.main_atlas.GetRegion(MainAtlas.Sprite_0002)));
        Applejack = new GameObject(new GameCore.Graphics.Sprite(GLOBALS.main_atlas.GetRegion(MainAtlas.Applejack_afk1)));
        Rainbow = new GameObject(new GameCore.Graphics.Sprite(GLOBALS.main_atlas.GetRegion(MainAtlas.Rainbowdash_idle1)));

        SeeSaw1.sprite.CenterOrigin();
        SeeSaw2.sprite.CenterOrigin();

        SeeSaw1.Scale = new System.Numerics.Vector2(seeSawScale, seeSawScale);
        SeeSaw2.Scale = new System.Numerics.Vector2(seeSawScale+0.5f, seeSawScale);
        Applejack.Scale = new System.Numerics.Vector2(ponyScale, ponyScale);
        Rainbow.Scale = new System.Numerics.Vector2(ponyScale, ponyScale);

        SeeSaw2.Position = new System.Numerics.Vector2(vp.Width * 0.4914f, vp.Height * 0.8008f);
        SeeSaw1.Position = new System.Numerics.Vector2(vp.Width / 2, vp.Height / 2 + (vp.Height / 5) * 1.5f);
        Rainbow.Position = new Vector2(vp.Width * 0.6351f, vp.Height * 0.4833f);
        Applejack.Position = new Vector2(vp.Width * 0.0289f, vp.Height * 0.6139f);
        seeSawJumpers = new Dictionary<SeeSawJumper, GameObject>
        {
            [SeeSawJumper.APPLEJACK] = Applejack,
            [SeeSawJumper.RAINBOW_DASH] = Rainbow
        };

        applejackExitPos = Applejack.Position;
        applejackOuterPos = new Vector2(vp.Width * 0.1984f, vp.Height * 0.5444f);
        applejackInnerPos = new Vector2(vp.Width * 0.3406f, vp.Height * 0.5042f);
        rainbowOuterPos = Rainbow.Position;
        rainbowInnerPos = new Vector2(vp.Width * 0.4953f, vp.Height * 0.4375f);

        SeeSaw2.Rotation = MathHelper.ToRadians(10);
        Rainbow.Rotation = MathHelper.ToRadians(10);

        SetupAnimations();
        seeSawAnimationStates = new Dictionary<SeeSawJumper, AnimationStateMachine>
        {
            [SeeSawJumper.APPLEJACK] = ApplejackState,
            [SeeSawJumper.RAINBOW_DASH] = RainbowState
        };

        GLOBALS.beatmapPlayer.BeatmapStarted += () =>
        {
            ResetActors();
            SetupVisuals();
            GLOBALS.beatmapPlayer.Conductor.BeatChanged += (_, _) => 
            {
                requestBop = [true, true];
            };
        };

        GameObjects.Add(Rainbow);
        GameObjects.Add(Applejack);
        GameObjects.Add(SeeSaw1);
        GameObjects.Add(SeeSaw2);
    }

    private void SetupVisuals()
    {
        double crotchet = 60.0 / GLOBALS.beatmapPlayer.Conductor.BPM;

        _visualChartPlayer = new ChartPlayer(GLOBALS.beatmapPlayer.CurrentChart, Rhythm.Note.ReactionRules.RhythmHeavenLike());

        seeSawVisuals = new VisualNoteManager<SeeSawVisualNote>(_visualChartPlayer, note =>
        {
            if (note.AdditionnalData == null || !note.AdditionnalData.ContainsKey("action")) 
                return null;

            string action = note.AdditionnalData["action"];
            Vector2 rainbowFromPos = GetRainbowPositionBefore(note);
            Vector2 applejackFromPos = GetApplejackPositionBefore(note);
            float fromRotation = GetBeamRotationBefore(note);

            switch(action)
            {
                case "see_saw_toward_outer":
                case "see_saw_toward_outer_big_leap":
                {
                    float counterTarget = GetBeamRotationToward(SeeSawJumper.APPLEJACK);      // Applejack atterrit -> beam penche a gauche
                    float target = GetBeamRotationToward(SeeSawJumper.RAINBOW_DASH);          // Rainbow atterrit -> beam penche a droite
                    return new SeeSawVisualNote(note, seeSawJumpers, seeSawAnimationStates, SeeSawJumper.RAINBOW_DASH, rainbowFromPos, rainbowOuterPos, crotchet, SeeSaw2, fromRotation, target, rainbowInnerPos, rainbowOuterPos, SeeSawJumper.APPLEJACK, applejackFromPos, applejackOuterPos, counterTarget, applejackInnerPos, applejackOuterPos, 0.4375f, 0.4375f, () => ReferenceEquals(_drivingVisualNote, note));
                }
                case "see_saw_toward_inner":
                case "see_saw_toward_inner_big_leap":
                {
                    float counterTarget = GetBeamRotationToward(SeeSawJumper.APPLEJACK);      // Applejack atterrit -> beam penche a gauche
                    float target = GetBeamRotationToward(SeeSawJumper.RAINBOW_DASH);          // Rainbow atterrit -> beam penche a droite
                    return new SeeSawVisualNote(note, seeSawJumpers, seeSawAnimationStates, SeeSawJumper.RAINBOW_DASH, rainbowFromPos, rainbowInnerPos, crotchet, SeeSaw2, fromRotation, target, rainbowInnerPos, rainbowOuterPos, SeeSawJumper.APPLEJACK, applejackFromPos, applejackInnerPos, counterTarget, applejackInnerPos, applejackOuterPos, canApplyState: () => ReferenceEquals(_drivingVisualNote, note));
                }
                case "see_saw_toward_opposite":
                case "see_saw_toward_opposite_big_leap":
                {
                    float counterTarget = GetBeamRotationToward(SeeSawJumper.APPLEJACK);
                    float target = GetBeamRotationToward(SeeSawJumper.RAINBOW_DASH);

                    Vector2 rainbowTargetPos = GetOppositeRainbowPosition(applejackFromPos);

                    double approachDuration = SeeSawVisualNote.GetApproachDuration(crotchet, applejackFromPos, applejackInnerPos, applejackOuterPos);
                    return new SeeSawVisualNote(note, seeSawJumpers, seeSawAnimationStates, SeeSawJumper.RAINBOW_DASH, rainbowFromPos, rainbowTargetPos, crotchet, SeeSaw2, fromRotation, target, rainbowInnerPos, rainbowOuterPos, SeeSawJumper.APPLEJACK, applejackFromPos, applejackFromPos, counterTarget, applejackInnerPos, applejackOuterPos, canApplyState: () => ReferenceEquals(_drivingVisualNote, note), approachDuration: approachDuration);
                }
            }

            return null; 
        });

        seeSawVisuals.LookBehindSeconds = 0;
        seeSawVisuals.LookAheadSeconds = crotchet * 3.0;
        _lastSongPosition = double.NaN;
    }

    private void ApplyTimelineBaseState(double songPosition)
    {
        Vector2 rainbowPosition = rainbowOuterPos;
        Vector2 applejackPosition = applejackExitPos;
        float beamRotation = MathHelper.ToRadians(10);

        foreach (Note note in _visualChartPlayer.Notes)
        {
            if (note.SongPosition > songPosition)
                break;

            Vector2 nextRainbowPosition = GetRainbowTargetPosition(note, rainbowPosition, applejackPosition);
            applejackPosition = GetApplejackTargetPosition(note, applejackPosition);
            rainbowPosition = nextRainbowPosition;
            beamRotation = GetBeamTargetRotation(note, beamRotation);
        }

        Rainbow.Position = rainbowPosition;
        Applejack.Position = applejackPosition;
        SeeSaw2.Rotation = beamRotation;
    }

    private Note FindDrivingVisualNote(double songPosition)
    {
        foreach (Note note in _visualChartPlayer.Notes)
        {
            if (!IsSeeSawNote(note))
                continue;

            double start = note.SongPosition - GetApproachDuration(note);
            if (songPosition >= start && songPosition <= note.SongPosition)
                return note;

            if (note.SongPosition > songPosition)
                break;
        }

        return null;
    }

    private double GetApproachDuration(Note note)
    {
        double crotchet = 60.0 / GLOBALS.beatmapPlayer.Conductor.BPM;

        if (note.AdditionnalData != null
            && note.AdditionnalData.TryGetValue("action", out string action)
            && IsTowardOpposite(action))
        {
            Vector2 applejackFromPosition = GetApplejackPositionBefore(note);
            return SeeSawVisualNote.GetApproachDuration(crotchet, applejackFromPosition, applejackInnerPos, applejackOuterPos);
        }

        Vector2 rainbowFromPosition = GetRainbowPositionBefore(note);
        return SeeSawVisualNote.GetApproachDuration(crotchet, rainbowFromPosition, rainbowInnerPos, rainbowOuterPos);
    }

    private bool IsSeeSawNote(Note note)
    {
        return note.AdditionnalData != null
            && note.AdditionnalData.TryGetValue("action", out string action)
            && IsSeeSawAction(action);
    }

    private Vector2 GetRainbowPositionBefore(Note targetNote)
    {
        Vector2 position = rainbowOuterPos;
        Vector2 applejackPosition = applejackExitPos;

        foreach (Note note in _visualChartPlayer.Notes)
        {
            if (note == targetNote || Math.Abs(note.SongPosition - targetNote.SongPosition) <= 0.0005)
                break;

            position = GetRainbowTargetPosition(note, position, applejackPosition);
            applejackPosition = GetApplejackTargetPosition(note, applejackPosition);
        }

        return position;
    }

    private Vector2 GetApplejackPositionBefore(Note targetNote)
    {
        Vector2 position = applejackExitPos;

        foreach (Note note in _visualChartPlayer.Notes)
        {
            if (note == targetNote || Math.Abs(note.SongPosition - targetNote.SongPosition) <= 0.0005)
                break;

            position = GetApplejackTargetPosition(note, position);
        }

        return position;
    }

    private Vector2 GetRainbowTargetPosition(Note note, Vector2 fallback, Vector2 currentApplejackPosition)
    {
        if (note.AdditionnalData == null || !note.AdditionnalData.TryGetValue("action", out string action))
            return fallback;

        if (IsTowardOuter(action))
            return rainbowOuterPos;

        if (IsTowardInner(action))
            return rainbowInnerPos;

        if (IsTowardOpposite(action))
            return GetOppositeRainbowPosition(currentApplejackPosition);

        return fallback;
    }

    private Vector2 GetApplejackTargetPosition(Note note, Vector2 fallback)
    {
        if (note.AdditionnalData == null || !note.AdditionnalData.TryGetValue("action", out string action))
            return fallback;

        if (IsTowardOuter(action))
            return applejackOuterPos;

        if (IsTowardInner(action))
            return applejackInnerPos;

        if (IsTowardOpposite(action))
            return fallback;

        return fallback;
    }

    private Vector2 GetOppositeRainbowPosition(Vector2 currentApplejackPosition)
    {
        return currentApplejackPosition == applejackOuterPos ? rainbowInnerPos : rainbowOuterPos;
    }

    private float GetBeamTargetRotation(Note note, float fallback)
    {
        if (note.AdditionnalData == null || !note.AdditionnalData.TryGetValue("action", out string action))
            return fallback;

        return IsSeeSawAction(action)
            ? GetBeamRotationToward(SeeSawJumper.RAINBOW_DASH)
            : fallback;
    }

    private bool IsSeeSawAction(string action)
    {
        return IsTowardOuter(action) || IsTowardInner(action) || IsTowardOpposite(action);
    }

    private bool IsTowardOuter(string action)
    {
        return action is "see_saw_toward_outer" or "see_saw_toward_outer_big_leap";
    }

    private bool IsTowardInner(string action)
    {
        return action is "see_saw_toward_inner" or "see_saw_toward_inner_big_leap";
    }

    private bool IsTowardOpposite(string action)
    {
        return action is "see_saw_toward_opposite" or "see_saw_toward_opposite_big_leap";
    }

    private float GetBeamRotationBefore(Note targetNote)
    {
        float rotation = MathHelper.ToRadians(10);

        foreach (Note note in _visualChartPlayer.Notes)
        {
            if (note == targetNote)
                break;

            rotation = GetBeamTargetRotation(note, rotation);
        }

        return rotation;
    }

    // La beam penche vers le cote logique du dernier qui a atterri.
    private float GetBeamRotationToward(SeeSawJumper lander)
    {
        return lander == SeeSawJumper.RAINBOW_DASH
            ? MathHelper.ToRadians(10)
            : MathHelper.ToRadians(-10);
    }

    private void ResetAnimationStateForTimeline()
    {
        seeSawVisuals?.Reset();
        RainbowState.ForceState("idle");

        if (Vector2.Distance(Applejack.Position, applejackExitPos) < 0.01f)
            ApplejackState.ForceState("start_idle");
        else
            ApplejackState.ForceState("idle");
    }

    private void ResetActors()
    {
        Rainbow.Position = rainbowOuterPos;
        Applejack.Position = applejackExitPos;
        SeeSaw2.Rotation = MathHelper.ToRadians(10);
        Rainbow.Rotation = MathHelper.ToRadians(10);
        Applejack.Rotation = 0;
    }

    public override void OnUnload()
    {
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        RainbowState.Update(gameTime);
        ApplejackState?.Update(gameTime);
        if (GLOBALS.beatmapPlayer.Conductor != null && seeSawVisuals != null)
        {
            double songPosition = GLOBALS.beatmapPlayer.Conductor.SongPosition;
            bool rewound = !double.IsNaN(_lastSongPosition) && songPosition < _lastSongPosition - 0.001;

            ApplyTimelineBaseState(songPosition);

            if (rewound)
                ResetAnimationStateForTimeline();

            _drivingVisualNote = FindDrivingVisualNote(songPosition);
            seeSawVisuals.Update(songPosition);
            _lastSongPosition = songPosition;
        }
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        GLOBALS.graphicsDevice.Clear(Color.Cyan);

        SeeSaw2.Draw(spriteBatch);
        SeeSaw1.Draw(spriteBatch);
        Applejack.Draw(spriteBatch);
        Rainbow.Draw(spriteBatch);
        base.Draw(spriteBatch);
    }

    private void SetupAnimations()
    {
        RainbowState = new AnimationStateMachine()
            .AddState(
                new AnimationState(
                    "idle",
                    5,
                    onEnter : () =>
                    {
                        Rainbow.sprite = GLOBALS.main_atlas.CreateAnimatedSprite("rainbowdash-idle");
                    },
                    onUpdate: (gt) =>
                    {
                        if(requestBop[1])
                        {
                            requestBop[1] = false;
                            ((AnimatedSprite) Rainbow.sprite).Restart();
                        }
                    }
                )
            )
            .AddState(
                new AnimationState(
                    name: "jump",
                    duration: 1,
                    onEnter : () =>
                    {
                        Rainbow.sprite = GLOBALS.main_atlas.CreateAnimatedSprite("rainbowdash-idle");
                    },
                    isLooping: true
                )
            )
            .AddState(
                new AnimationState(
                    name: "land",
                    duration: 0.25f,
                    onEnter : () =>
                    {
                        Rainbow.sprite = GLOBALS.main_atlas.CreateAnimatedSprite("rainbowdash-idle");
                    },
                    isLooping: false
                )
            )
            .AddState(
                new AnimationState(
                    name: "fall",
                    duration: 1,
                    onEnter : () =>
                    {
                        Rainbow.sprite = GLOBALS.main_atlas.CreateAnimatedSprite("rainbowdash-idle");
                    },
                    isLooping: true
                )
            )
            .SetGlobalUpdate(globalUpdate: (gt) =>
            {
                ((AnimatedSprite) Rainbow.sprite)?.Update(gt);
                return true;
            })
            .Build();

        ApplejackState = new AnimationStateMachine()
            .AddState(
                new AnimationState(
                    "start_idle",
                    5,
                    onEnter : () =>
                    {
                        Applejack.sprite = GLOBALS.main_atlas.CreateAnimatedSprite("applejack afk");
                    },
                    onUpdate: (gt) =>
                    {
                        if(requestBop[0])
                        {
                            requestBop[0] = false;
                            ((AnimatedSprite) Applejack.sprite).Restart();
                        }
                    }
                )
            )
            .AddState(
                new AnimationState(
                    name: "jump",
                    duration: 1,
                    onEnter : () =>
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
                    onEnter : () =>
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
                    onEnter : () =>
                    {
                        Applejack.sprite = GLOBALS.main_atlas.CreateAnimatedSprite("applejack-land");
                        ((AnimatedSprite) Applejack.sprite).IsLooping = false;
                        ((AnimatedSprite) Applejack.sprite).Restart();
                    },
                    isLooping: false
                )
            )
            .AddState(
                new AnimationState(
                    name: "idle",
                    duration: 1,
                    onEnter : () =>
                    {
                        Applejack.sprite = GLOBALS.main_atlas.CreateSprite(MainAtlas.Applejack_true_idle);
                    },
                    isLooping: false
                )
            )
            .AddTransition("land", "idle", () => ApplejackState.StateProgress >= 1f)
            .SetGlobalUpdate(globalUpdate: (gt) =>
            {
                if(Applejack.sprite is AnimatedSprite)((AnimatedSprite) Applejack.sprite)?.Update(gt);
                return true;
            })
            .Build();

        RainbowState.ForceState("idle");
        ApplejackState.ForceState("start_idle");
    }
}
