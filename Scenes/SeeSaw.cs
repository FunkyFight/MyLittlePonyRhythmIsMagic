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
using Rhythm.Note.Visual;
using TexturePackerMonoGameDefinitions;

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

    private float applejackBaseY;
    private float rainbowBaseY;

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
        Rainbow.Position = new System.Numerics.Vector2(vp.Width / 2 + 270, vp.Height / 2 + 100);
        Applejack.Position = new System.Numerics.Vector2(vp.Width / 2 - 450, vp.Height / 2 + 300);

        applejackBaseY = Applejack.Position.Y;
        rainbowBaseY = Rainbow.Position.Y;

        SeeSaw2.Rotation = MathHelper.ToRadians(-15);
        Rainbow.Rotation = MathHelper.ToRadians(-15);

        SetupAnimations();

        GLOBALS.beatmapPlayer.BeatmapStarted += () =>
        {
            SetupVisuals();
            GLOBALS.beatmapPlayer.Conductor.BeatChanged += (_, _) => 
            {
                requestBop = [true, true];
            };
        };
    }

    private void SetupVisuals()
    {
        seeSawVisuals = new VisualNoteManager<VisualNote>(GLOBALS.beatmapPlayer.ChartPlayer, note =>
        {
            if (note.AdditionnalData == null || !note.AdditionnalData.ContainsKey("action")) 
                return null;

            string action = note.AdditionnalData["action"];

            switch(action)
            {
                case "see_saw_toward_outer":
                    if(turn == 0) 
                    {
                        turn = 1;
                        return new SeeSawVisualNote(note, Applejack, applejackBaseY, 1.0);
                    }
                    else 
                    {
                        turn = 0;
                        return new SeeSawVisualNote(note, Rainbow, rainbowBaseY, 1.0);
                    }
            }

            return null; 
        });

        seeSawVisuals.LookBehindSeconds = 0;
    }

    public override void OnUnload()
    {
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        RainbowState.Update(gameTime);
        ApplejackState?.Update(gameTime);
        seeSawVisuals.Update(GLOBALS.beatmapPlayer.Conductor.SongPosition);
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