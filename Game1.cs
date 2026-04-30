using System;
using System.Collections.Generic;
using GameCore;
using GameCore.Graphics;
using GameCore.Inputs;
using GameCore.Scenes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rhythm.Conductor;
using Rhythm.Note;
using Rhythm.Note.Evaluator;

namespace MLP_RiM;

public class Game1 : Core
{
    private SceneManager _sceneManager;

    private InputActionManager _inputActionManager;

    public Game1() : base("My Little Pony: Rhythm Is Magic", 1280, 720, false)
    {
    }

    protected override void Initialize()
    {
        base.Initialize();

        GLOBALS.graphicsDevice = GraphicsDevice;

        // Controls
        _inputActionManager = new InputActionManager();
        _inputActionManager.LoadFromXml(Content, "Content/InputActions.xml");

        // Beatmap
        GLOBALS.beatmapPlayer = new BeatmapPlayer();
        

        // Scene
        _sceneManager = new SceneManager();
        _sceneManager.SetScene(new SeeSawScene(this));


        // Beatmap
        // Debug
        Dictionary<string, string> data = new Dictionary<string, string>();
        data.Add("action", "see_saw_toward_outer");
        //
        GLOBALS.beatmapPlayer.StartMetronomeDebugMap(data);
        GLOBALS.rhythmInputVisualElement = new RhythmInputVisualElement(GLOBALS.beatmapPlayer);
        GLOBALS.beatmapPlayer.ChartPlayer.NoteReacted += (result) =>
        {
            Console.WriteLine(result);
        };

    }

    protected override void LoadContent()
    {
        base.LoadContent();
        GLOBALS.controller_atlas = TextureAtlas.FromTexturePackerFile(Content, "atlas/xbox_controller", "atlas/xbox_controller.txt");
        GLOBALS.main_atlas = TextureAtlas.FromTexturePackerFile(Content, "atlas/main_atlas", "atlas/main_atlas.txt");
    }

    protected override void Update(GameTime gameTime)
    {
        if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
            Exit();


        GLOBALS.beatmapPlayer?.Update();
        _sceneManager.Update(gameTime);
        handleInputs();

        

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.Black);

        SpriteBatch.Begin(samplerState: SamplerState.PointClamp);
        _sceneManager.Draw(SpriteBatch);
        SpriteBatch.End();

        base.Draw(gameTime);
    }

    private void handleInputs()
    {
        _inputActionManager.Update();
        
        // Rhythm Game
        if(GLOBALS.beatmapPlayer != null && GLOBALS.beatmapPlayer.Conductor != null && GLOBALS.beatmapPlayer.Conductor.isPlaying())
        {
            if(_inputActionManager.IsPressedOnce("ReactMain")) GLOBALS.beatmapPlayer.ChartPlayer.React("ReactMain", GLOBALS.beatmapPlayer.Conductor.SongPosition);
        }

    }
}
