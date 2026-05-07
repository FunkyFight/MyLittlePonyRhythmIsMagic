using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using GameCore;
using GameCore.Graphics;
using GameCore.Inputs;
using GameCore.Scenes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MLP_RiM.Elements;
using MLP_RiM.Elements.Editor;
using Rhythm.Conductor;
using Rhythm.Note;
using Rhythm.Note.Evaluator;

namespace MLP_RiM;

public class Game1 : Core
{
    private SceneManager _sceneManager;

    private InputActionManager _inputActionManager;
    private KeyboardState _keyboard;
    private KeyboardState _previousKeyboard;
    private bool _showMouseViewportCoordinates;

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
        
        // Beatmap editor
        GLOBALS.beatmapEditorElement = new BeatmapEditorElement(GLOBALS.beatmapPlayer);

        // Scene
        _sceneManager = new SceneManager();
        _sceneManager.SetScene(new SeeSawScene(this));


        
        GLOBALS.mouseViewportCoordinatesElement = new MouseViewportCoordinatesElement(GraphicsDevice);
        GLOBALS.beatmapPlayer.BeatmapStarted += () =>
        {
            if (GLOBALS.beatmapPlayer.ChartPlayer != null)
                GLOBALS.beatmapPlayer.ChartPlayer.NoteReacted += result => Console.WriteLine(result);
        };


        DebugActivated = true;

    }

    protected override void LoadContent()
    {
        base.LoadContent();
        GLOBALS.controller_atlas = TextureAtlas.FromTexturePackerFile(Content, "atlas/xbox_controller", "atlas/xbox_controller.txt");
        GLOBALS.main_atlas = TextureAtlas.FromTexturePackerFile(Content, "atlas/main_atlas", "atlas/main_atlas.txt");
    }

    protected override void Update(GameTime gameTime)
    {
        // Non-preview editor playback owns conductor updates to avoid advancing rhythm state twice.
        bool editorOwnsBeatmapPlayback = GLOBALS.beatmapEditorElement != null && !GLOBALS.beatmapEditorElement.IsPreviewPlaying;
        GLOBALS.SfxVolume = GLOBALS.beatmapEditorElement != null
            && (GLOBALS.beatmapEditorElement.IsPreviewPlaying || GLOBALS.beatmapEditorElement.IsEditorPlaybackPlaying)
            ? 1.0f
            : 0.0f;

        if (editorOwnsBeatmapPlayback)
            GLOBALS.beatmapEditorElement.Update(gameTime);
        else
            GLOBALS.beatmapPlayer?.Update(gameTime);

        _sceneManager.Update(gameTime);

        _previousKeyboard = _keyboard;
        _keyboard = Keyboard.GetState();

        if (Pressed(Keys.F9))
            _showMouseViewportCoordinates = !_showMouseViewportCoordinates;

        if (Pressed(Keys.F10))
            CopyMouseViewportCoordinates();

        if (!editorOwnsBeatmapPlayback)
            GLOBALS.beatmapEditorElement?.Update(gameTime);

        handleInputs();

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.Black);


        _sceneManager.Viewport.Scale = new Vector2(0.5f, 0.5f);
        _sceneManager.Draw(SpriteBatch);

        SpriteBatch.Begin(samplerState: SamplerState.PointClamp);
        
        GLOBALS.beatmapEditorElement?.Draw(SpriteBatch);
        if (_showMouseViewportCoordinates)
            GLOBALS.mouseViewportCoordinatesElement?.Draw(SpriteBatch);
        
        //GLOBALS.rhythmInputVisualElement?.Draw(SpriteBatch);
        SpriteBatch.End();



        base.Draw(gameTime);
    }

    private void handleInputs()
    {
        _inputActionManager.Update();
        
        if (GLOBALS.beatmapEditorElement != null && !GLOBALS.beatmapEditorElement.IsPreviewPlaying)
            return;

        if(GLOBALS.beatmapPlayer != null && GLOBALS.beatmapPlayer.Conductor != null && GLOBALS.beatmapPlayer.Conductor.isPlaying())
        {
            if(_inputActionManager.IsPressedOnce("ReactMain")) 
            {
                GLOBALS.beatmapPlayer.ChartPlayer.React("ReactMain", GLOBALS.beatmapPlayer.Conductor.SongPosition);
            }
        }
    }

    private bool Pressed(Keys key)
    {
        return _keyboard.IsKeyDown(key) && !_previousKeyboard.IsKeyDown(key);
    }

    private void CopyMouseViewportCoordinates()
    {
        if (GLOBALS.mouseViewportCoordinatesElement == null)
            return;

        string text = GLOBALS.mouseViewportCoordinatesElement.GetViewportExpression();
        SetClipboardText(text);
        GLOBALS.mouseViewportCoordinatesElement.SetCopiedStatus(text);
    }

    private void SetClipboardText(string text)
    {
        if (!OperatingSystem.IsWindows())
            return;

        WindowsClipboard.SetText(text);
    }

    private static class WindowsClipboard
    {
        private const uint CfUnicodeText = 13;
        private const uint GmemMoveable = 0x0002;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool OpenClipboard(IntPtr hWndNewOwner);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool EmptyClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool CloseClipboard();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalLock(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalUnlock(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalFree(IntPtr hMem);

        public static void SetText(string text)
        {
            IntPtr hGlobal = IntPtr.Zero;

            try
            {
                int bytes = (text.Length + 1) * 2;
                hGlobal = GlobalAlloc(GmemMoveable, (UIntPtr)bytes);
                if (hGlobal == IntPtr.Zero)
                    return;

                IntPtr target = GlobalLock(hGlobal);
                if (target == IntPtr.Zero)
                    return;

                Marshal.Copy(text.ToCharArray(), 0, target, text.Length);
                Marshal.WriteInt16(target, text.Length * 2, 0);
                GlobalUnlock(hGlobal);

                if (!OpenClipboard(IntPtr.Zero))
                    return;

                EmptyClipboard();
                if (SetClipboardData(CfUnicodeText, hGlobal) != IntPtr.Zero)
                    hGlobal = IntPtr.Zero;
            }
            finally
            {
                CloseClipboard();

                if (hGlobal != IntPtr.Zero)
                    GlobalFree(hGlobal);
            }
        }
    }
}
