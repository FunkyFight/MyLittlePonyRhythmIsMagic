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
    private VisualNoteManager<VisualNote> seeSawVisuals;
    private bool[] requestBop = [true, true];

    private int ponyScale = 7;
    private int seeSawScale = 2;

    public short turn = 0;

    private System.Numerics.Vector2 applejackOuterPos;
    private System.Numerics.Vector2 applejackInnerPos;
    private System.Numerics.Vector2 applejackExitPos;
    private System.Numerics.Vector2 rainbowOuterPos;
    private System.Numerics.Vector2 rainbowInnerPos;
    private ChartPlayer _visualChartPlayer;

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
        SeeSaw2.Scale = new System.Numerics.Vector2(seeSawScale + 1, seeSawScale);
        Applejack.Scale = new System.Numerics.Vector2(ponyScale, ponyScale);
        Rainbow.Scale = new System.Numerics.Vector2(ponyScale, ponyScale);

        SeeSaw2.Position = new System.Numerics.Vector2(vp.Width / 2, vp.Height / 2 + vp.Height / 4);
        SeeSaw1.Position = new System.Numerics.Vector2(vp.Width / 2, vp.Height / 2 + (vp.Height / 5) * 1.5f);
        Rainbow.Position = new System.Numerics.Vector2(vp.Width / 2 + 270, vp.Height / 2 + 210);
        Applejack.Position = new System.Numerics.Vector2(vp.Width / 2 - 500, vp.Height / 2 + 300);

        applejackExitPos = Applejack.Position;
        applejackOuterPos = new System.Numerics.Vector2(vp.Width / 2 - 300, Rainbow.Position.Y);
        applejackInnerPos = new System.Numerics.Vector2(vp.Width / 2 - 50, Applejack.Position.Y);
        rainbowOuterPos = Rainbow.Position;
        rainbowInnerPos = new System.Numerics.Vector2(vp.Width / 2 + 100, Rainbow.Position.Y);

        SeeSaw2.Rotation = MathHelper.ToRadians(10);
        Rainbow.Rotation = MathHelper.ToRadians(10);

        SetupAnimations();

        GLOBALS.beatmapPlayer.BeatmapStarted += () =>
        {
            ResetActors();
            SetupVisuals();
            GLOBALS.beatmapPlayer.Conductor.BeatChanged += (_, _) => 
            {
                requestBop = [true, true];
            };
        };
    }

    private void SetupVisuals()
    {
        double crotchet = 60.0 / GLOBALS.beatmapPlayer.Conductor.BPM;

        _visualChartPlayer = new ChartPlayer(GLOBALS.beatmapPlayer.CurrentChart, Rhythm.Note.ReactionRules.RhythmHeavenLike());

        seeSawVisuals = new VisualNoteManager<VisualNote>(_visualChartPlayer, note =>
        {
            if (note.AdditionnalData == null || !note.AdditionnalData.ContainsKey("action")) 
                return null;

            string action = note.AdditionnalData["action"];
            Vector2 rainbowFromPos = GetRainbowPositionBefore(note);
            Vector2 applejackFromPos = GetApplejackPositionBefore(note);
            float fromRotation = GetBeamRotationForRainbowPosition(rainbowFromPos);

            switch(action)
            {
                case "see_saw_toward_outer":
                    return new SeeSawVisualNote(note, Rainbow, rainbowFromPos, rainbowOuterPos, crotchet, SeeSaw2, fromRotation, MathHelper.ToRadians(10), rainbowInnerPos, rainbowOuterPos, Applejack, applejackFromPos, applejackOuterPos, MathHelper.ToRadians(-10), applejackInnerPos, applejackOuterPos);
                case "see_saw_toward_inner":
                    return new SeeSawVisualNote(note, Rainbow, rainbowFromPos, rainbowInnerPos, crotchet, SeeSaw2, fromRotation, MathHelper.ToRadians(10), rainbowInnerPos, rainbowOuterPos, Applejack, applejackFromPos, applejackInnerPos, MathHelper.ToRadians(-10), applejackInnerPos, applejackOuterPos);
            }

            return null; 
        });

        seeSawVisuals.LookBehindSeconds = 0;
        seeSawVisuals.LookAheadSeconds = crotchet * 2.0;
    }

    private void ApplyTimelineBaseState(double songPosition)
    {
        Vector2 rainbowPosition = rainbowOuterPos;
        Vector2 applejackPosition = applejackExitPos;

        foreach (Note note in _visualChartPlayer.Notes)
        {
            if (note.SongPosition > songPosition)
                break;

            rainbowPosition = GetRainbowTargetPosition(note, rainbowPosition);
            applejackPosition = GetApplejackTargetPosition(note, applejackPosition);
        }

        Rainbow.Position = rainbowPosition;
        Applejack.Position = applejackPosition;
        SeeSaw2.Rotation = GetBeamRotationForRainbowPosition(rainbowPosition);
    }

    private Vector2 GetRainbowPositionBefore(Note targetNote)
    {
        Vector2 position = rainbowOuterPos;

        foreach (Note note in _visualChartPlayer.Notes)
        {
            if (note == targetNote)
                break;

            position = GetRainbowTargetPosition(note, position);
        }

        return position;
    }

    private Vector2 GetApplejackPositionBefore(Note targetNote)
    {
        Vector2 position = applejackExitPos;

        foreach (Note note in _visualChartPlayer.Notes)
        {
            if (note == targetNote)
                break;

            position = GetApplejackTargetPosition(note, position);
        }

        return position;
    }

    private Vector2 GetRainbowTargetPosition(Note note, Vector2 fallback)
    {
        if (note.AdditionnalData == null || !note.AdditionnalData.TryGetValue("action", out string action))
            return fallback;

        return action switch
        {
            "see_saw_toward_outer" => rainbowOuterPos,
            "see_saw_toward_inner" => rainbowInnerPos,
            _ => fallback
        };
    }

    private Vector2 GetApplejackTargetPosition(Note note, Vector2 fallback)
    {
        if (note.AdditionnalData == null || !note.AdditionnalData.TryGetValue("action", out string action))
            return fallback;

        return action switch
        {
            "see_saw_toward_outer" => applejackOuterPos,
            "see_saw_toward_inner" => applejackInnerPos,
            _ => fallback
        };
    }

    private float GetBeamRotationForRainbowPosition(Vector2 rainbowPosition)
    {
        return Vector2.Distance(rainbowPosition, rainbowOuterPos) < Vector2.Distance(rainbowPosition, rainbowInnerPos)
            ? MathHelper.ToRadians(10)
            : MathHelper.ToRadians(-10);
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
            _visualChartPlayer?.Seek(songPosition);
            seeSawVisuals.Reset();
            ApplyTimelineBaseState(songPosition);
            seeSawVisuals.Update(songPosition);
        }
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        base.Draw(spriteBatch);
        GLOBALS.graphicsDevice.Clear(Color.Cyan);

        SeeSaw2.Draw(spriteBatch);
        SeeSaw1.Draw(spriteBatch);
        Applejack.Draw(spriteBatch);
        Rainbow.Draw(spriteBatch);
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
            .SetGlobalUpdate(globalUpdate: (gt) =>
            {
                ((AnimatedSprite) Applejack.sprite)?.Update(gt);
                return true;
            })
            .Build();

        RainbowState.ForceState("idle");
        ApplejackState.ForceState("start_idle");
    }
}
