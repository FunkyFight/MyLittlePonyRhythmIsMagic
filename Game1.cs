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
    private string _currentRhythmGameSceneId;
    private Texture2D _blackoutPixel;
    private Effect _saturationEffect;

    private InputActionManager _inputActionManager;
    private KeyboardState _keyboard;
    private KeyboardState _previousKeyboard;
    private bool _showMouseViewportCoordinates;

    public Game1() : base("My Little Pony: Rhythm Is Magic", 1920, 1080, true)
    {
    }

    protected override void Initialize()
    {
        base.Initialize();

        GLOBALS.graphicsDevice = GraphicsDevice;
        _blackoutPixel = new Texture2D(GraphicsDevice, 1, 1);
        _blackoutPixel.SetData(new[] { Color.White });

        // Controls
        _inputActionManager = new InputActionManager();
        _inputActionManager.LoadFromXml(Content, "Content/InputActions.xml");

        // Beatmap
        GLOBALS.beatmapPlayer = new BeatmapPlayer();

        // Scene
        _sceneManager = new SceneManager();
        _sceneManager.Viewport.SamplerState = SamplerState.PointClamp;
        GLOBALS.beatmapPlayer.RhythmGameSwitchRequested += SwitchRhythmGameScene;

        // Beatmap editor
        GLOBALS.beatmapEditorElement = new BeatmapEditorElement(GLOBALS.beatmapPlayer);
        if (_currentRhythmGameSceneId == null)
            SwitchFirstAvailableRhythmGameScene();


        
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
        _saturationEffect = Content.Load<Effect>("Effects/Saturation");
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


        if (GLOBALS.beatmapEditorElement != null)
            GLOBALS.beatmapEditorElement.ConfigureSceneViewport(_sceneManager.Viewport);
        else
            BeatmapEditorElement.ConfigureSceneViewportFullscreen(_sceneManager.Viewport);

        DrawSceneWithCameraEffects(SpriteBatch);
        DrawSceneSaturationOverlay(SpriteBatch);
        DrawSceneFlashOverlay(SpriteBatch);

        SpriteBatch.Begin(samplerState: SamplerState.PointClamp);
        DrawSwitchGameBlackout(SpriteBatch);

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
        GLOBALS.ReactMainIsPressed = _inputActionManager.IsPressed("ReactMain");
        if (_inputActionManager.IsReleasedOnce("ReactMain"))
        {
            GLOBALS.ReactMainReleaseSerial++;
            GLOBALS.ReactMainReleaseSongPosition = GLOBALS.beatmapPlayer?.Conductor?.SongPosition ?? double.NaN;
        }
        
        if (GLOBALS.beatmapEditorElement != null && !GLOBALS.beatmapEditorElement.IsPreviewPlaying)
            return;

        if(GLOBALS.beatmapPlayer != null && GLOBALS.beatmapPlayer.Conductor != null && GLOBALS.beatmapPlayer.Conductor.isPlaying())
        {
            if(_inputActionManager.IsPressedOnce("ReactMain")) 
            {
                GLOBALS.ReactMainInputSerial++;
                GLOBALS.beatmapPlayer.ChartPlayer.React("ReactMain", GLOBALS.beatmapPlayer.Conductor.SongPosition);
            }
        }
    }

    private bool Pressed(Keys key)
    {
        return _keyboard.IsKeyDown(key) && !_previousKeyboard.IsKeyDown(key);
    }

    private void SwitchRhythmGameScene(string rhythmGameId)
    {
        if (_sceneManager == null || string.IsNullOrWhiteSpace(rhythmGameId) || rhythmGameId == _currentRhythmGameSceneId)
            return;

        if (!EditorNoteDefinitions.TryCreateScene(rhythmGameId, out Scene scene))
            return;

        _currentRhythmGameSceneId = rhythmGameId;
        _sceneManager.SetScene(scene);
    }

    private void SwitchFirstAvailableRhythmGameScene()
    {
        foreach (IEditorNoteProvider provider in EditorNoteDefinitions.GameProviders)
        {
            if (provider == null || string.IsNullOrWhiteSpace(provider.RhythmGameId))
                continue;

            if (!EditorNoteDefinitions.TryCreateScene(provider.RhythmGameId, out Scene scene))
                continue;

            _currentRhythmGameSceneId = provider.RhythmGameId;
            _sceneManager.SetScene(scene);
            return;
        }
    }

    private void DrawSceneWithCameraEffects(SpriteBatch spriteBatch)
    {
        Scene scene = _sceneManager.CurrentScene;
        if (scene == null)
            return;

        ViewportCameraState cameraState = GLOBALS.beatmapPlayer?.CameraEffectState ?? ViewportCameraState.Identity;
        Vector2 originalCameraPosition = scene.sceneCamera.Position;
        float originalCameraRotation = scene.sceneCamera.Rotation;
        Vector2 originalCameraZoom = scene.sceneCamera.Zoom;
        scene.sceneCamera.Position = originalCameraPosition + cameraState.Offset;
        scene.sceneCamera.Rotation = originalCameraRotation + cameraState.Rotation;
        scene.sceneCamera.Zoom = cameraState.Zoom;

        try
        {
            _sceneManager.Draw(spriteBatch);
        }
        finally
        {
            scene.sceneCamera.Position = originalCameraPosition;
            scene.sceneCamera.Rotation = originalCameraRotation;
            scene.sceneCamera.Zoom = originalCameraZoom;
        }
    }

    private void DrawSceneSaturationOverlay(SpriteBatch spriteBatch)
    {
        if (GLOBALS.beatmapPlayer == null || _saturationEffect == null)
            return;

        float saturation = GLOBALS.beatmapPlayer.IsBlackAndWhiteActive
            ? 0f
            : GLOBALS.beatmapPlayer.CameraSaturation;
        if (Math.Abs(saturation - 1f) <= 0.0001f)
            return;

        RenderViewport viewport = _sceneManager.Viewport;
        RenderTarget2D renderTarget = viewport.RenderTarget;
        if (renderTarget == null || renderTarget.IsDisposed)
            return;

        _saturationEffect.Parameters["Saturation"]?.SetValue(saturation);
        spriteBatch.Begin(blendState: viewport.BlendState, samplerState: viewport.PresentationSamplerState, effect: _saturationEffect);
        spriteBatch.Draw(renderTarget, viewport.Position, null, Color.White, viewport.Rotation, viewport.Origin, viewport.Scale, SpriteEffects.None, 0.0f);
        spriteBatch.End();
    }

    private void DrawSceneFlashOverlay(SpriteBatch spriteBatch)
    {
        float intensity = GLOBALS.beatmapPlayer?.FlashIntensity ?? 0f;
        if (intensity <= 0f || _blackoutPixel == null)
            return;

        RenderViewport viewport = _sceneManager.Viewport;
        Rectangle bounds = new Rectangle(
            (int)MathF.Round(viewport.Position.X - viewport.Origin.X * viewport.Scale.X),
            (int)MathF.Round(viewport.Position.Y - viewport.Origin.Y * viewport.Scale.Y),
            (int)MathF.Round(viewport.Width * viewport.Scale.X),
            (int)MathF.Round(viewport.Height * viewport.Scale.Y));

        spriteBatch.Begin(blendState: BlendState.AlphaBlend, samplerState: viewport.PresentationSamplerState);
        spriteBatch.Draw(_blackoutPixel, bounds, Color.White * intensity);
        spriteBatch.End();
    }

    private void DrawSwitchGameBlackout(SpriteBatch spriteBatch)
    {
        if (GLOBALS.beatmapPlayer?.IsSwitchGameBlackoutActive != true || _blackoutPixel == null)
            return;

        RenderViewport viewport = _sceneManager.Viewport;
        Rectangle bounds = new Rectangle(
            (int)MathF.Round(viewport.Position.X - viewport.Origin.X * viewport.Scale.X),
            (int)MathF.Round(viewport.Position.Y - viewport.Origin.Y * viewport.Scale.Y),
            (int)MathF.Round(viewport.Width * viewport.Scale.X),
            (int)MathF.Round(viewport.Height * viewport.Scale.Y));

        spriteBatch.Draw(_blackoutPixel, bounds, Color.Black);
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
