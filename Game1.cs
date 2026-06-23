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
using MLP_RiM.Elements.LevelEditor;
using MLP_RiM.Elements.Levels;
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
    private SpriteFont _dialogueFont;
    private AppMode _appMode = AppMode.MainMenu;
    private LevelEditorElement _levelEditorElement;
    private LevelRuntimeController _levelRuntimeController;

    private InputActionManager _inputActionManager;
    private KeyboardState _keyboard;
    private KeyboardState _previousKeyboard;
    private bool _showMouseViewportCoordinates;

    private enum AppMode
    {
        MainMenu,
        BeatmapEditor,
        LevelEditor,
        LevelRuntime
    }

    public Game1() : base("My Little Pony: Rhythm Is Magic", 1920, 1080, false)
    {
        ConfigureBorderlessFullscreen();
    }

    private void ConfigureBorderlessFullscreen()
    {
        DisplayMode displayMode = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
        Graphics.IsFullScreen = false;
        Graphics.PreferredBackBufferWidth = displayMode.Width;
        Graphics.PreferredBackBufferHeight = displayMode.Height;
        Window.IsBorderless = true;
        Window.Position = Point.Zero;
        Graphics.ApplyChanges();
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

        // Scene starts on the main menu. Editors and level runtime are created only when selected.
        _sceneManager.SetScene(CreateMainMenu());


        
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
        _dialogueFont = Content.Load<SpriteFont>("Fonts/Dialogue");
    }

    protected override void Update(GameTime gameTime)
    {
        _previousKeyboard = _keyboard;
        _keyboard = Keyboard.GetState();
        _inputActionManager.Update();

        bool beatmapEditorActive = _appMode == AppMode.BeatmapEditor && GLOBALS.beatmapEditorElement != null;
        bool levelEditorActive = _appMode == AppMode.LevelEditor && _levelEditorElement != null;
        bool levelRuntimeActive = _appMode == AppMode.LevelRuntime && _levelRuntimeController != null;
        bool levelRuntimeRhythmClockActive = levelRuntimeActive && _levelRuntimeController.ShouldUpdateRhythmScene;

        // Non-preview editor playback owns conductor updates to avoid advancing rhythm state twice.
        bool editorOwnsBeatmapPlayback = beatmapEditorActive && !GLOBALS.beatmapEditorElement.IsPreviewPlaying;
        GLOBALS.SfxVolume = beatmapEditorActive
            && (GLOBALS.beatmapEditorElement.IsPreviewPlaying || GLOBALS.beatmapEditorElement.IsEditorPlaybackPlaying)
            || levelRuntimeRhythmClockActive
            ? 1.0f
            : 0.0f;

        if (levelEditorActive)
            _levelEditorElement.Update(gameTime);

        if (editorOwnsBeatmapPlayback)
            GLOBALS.beatmapEditorElement.Update(gameTime);
        else if (!levelEditorActive
            && (!levelRuntimeActive
                || levelRuntimeRhythmClockActive
                || GLOBALS.beatmapPlayer?.HasAChartLoaded == true))
            GLOBALS.beatmapPlayer?.Update(gameTime);

        if (Pressed(Keys.F9))
            _showMouseViewportCoordinates = !_showMouseViewportCoordinates;

        if (Pressed(Keys.F10))
            CopyMouseViewportCoordinates();

        if (beatmapEditorActive && !editorOwnsBeatmapPlayback)
            GLOBALS.beatmapEditorElement?.Update(gameTime);

        handleInputs();

        // Run graph transitions before the scene update. Starting the empty tail can remove notes
        // and change the active node; the rhythm scene must see that new state in the same frame.
        if (levelRuntimeActive)
            _levelRuntimeController.Update(gameTime, _inputActionManager);

        // Dialogue overlays must never pause the underlying rhythm scene. In particular, the
        // post-training empty tail keeps actor state machines alive after leaving the training node.
        if (!levelEditorActive)
            _sceneManager.Update(gameTime);

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(Color.Black);

        bool beatmapEditorActive = _appMode == AppMode.BeatmapEditor && GLOBALS.beatmapEditorElement != null;
        bool levelEditorActive = _appMode == AppMode.LevelEditor && _levelEditorElement != null;
        bool levelRuntimeActive = _appMode == AppMode.LevelRuntime && _levelRuntimeController != null;

        if (beatmapEditorActive)
            GLOBALS.beatmapEditorElement.ConfigureSceneViewport(_sceneManager.Viewport);
        else
            BeatmapEditorElement.ConfigureSceneViewportFullscreen(_sceneManager.Viewport);

        DrawSceneWithCameraEffects(SpriteBatch);
        DrawSceneSaturationOverlay(SpriteBatch);
        DrawSceneFlashOverlay(SpriteBatch);

        SpriteBatch.Begin(samplerState: SamplerState.PointClamp);
        DrawSwitchGameBlackout(SpriteBatch);

        if (beatmapEditorActive)
            GLOBALS.beatmapEditorElement?.Draw(SpriteBatch);
        if (levelEditorActive)
            _levelEditorElement?.Draw(SpriteBatch);
        if (levelRuntimeActive)
            _levelRuntimeController?.Draw(SpriteBatch);
        if (_showMouseViewportCoordinates)
            GLOBALS.mouseViewportCoordinatesElement?.Draw(SpriteBatch);
        
        //GLOBALS.rhythmInputVisualElement?.Draw(SpriteBatch);
        SpriteBatch.End();



        base.Draw(gameTime);
    }

    private void handleInputs()
    {
        GLOBALS.ReactMainIsPressed = _inputActionManager.IsPressed("ReactMain");
        if (_inputActionManager.IsReleasedOnce("ReactMain"))
        {
            GLOBALS.ReactMainReleaseSerial++;
            GLOBALS.ReactMainReleaseSongPosition = GLOBALS.beatmapPlayer?.GameplaySongPosition ?? double.NaN;
        }
        
        bool beatmapEditorActive = _appMode == AppMode.BeatmapEditor && GLOBALS.beatmapEditorElement != null;
        if (beatmapEditorActive && !GLOBALS.beatmapEditorElement.IsPreviewPlaying)
            return;

        if (_appMode == AppMode.LevelEditor)
            return;

        if (_appMode == AppMode.LevelRuntime && _levelRuntimeController?.AcceptsRhythmInput != true)
            return;

        if(GLOBALS.beatmapPlayer != null && GLOBALS.beatmapPlayer.Conductor != null && GLOBALS.beatmapPlayer.Conductor.isPlaying())
        {
            if(_inputActionManager.IsPressedOnce("ReactMain")) 
            {
                GLOBALS.ReactMainInputSerial++;
                GLOBALS.beatmapPlayer.ChartPlayer.React("ReactMain", GLOBALS.beatmapPlayer.GameplaySongPosition);
            }
        }
    }

    private void OpenBeatmapEditor()
    {
        ClearEditorsAndRuntime();
        _appMode = AppMode.BeatmapEditor;
        GLOBALS.beatmapEditorElement = new BeatmapEditorElement(GLOBALS.beatmapPlayer);
        SwitchFirstAvailableRhythmGameScene();
    }

    private void OpenLevelEditor()
    {
        ClearEditorsAndRuntime();
        _appMode = AppMode.LevelEditor;
        _sceneManager.SetScene(CreateMainMenu());
        _levelEditorElement = new LevelEditorElement(GraphicsDevice, document => StartLevel(document, ignoreLocks: true), ReturnToMainMenu);
    }

    private void StartLevel(LevelDocument document, bool ignoreLocks)
    {
        if (document == null)
            return;

        if (!ignoreLocks && !LevelProgressSave.Load().IsUnlocked(document.Level))
            return;

        ClearEditorsAndRuntime();
        _appMode = AppMode.LevelRuntime;
        _levelRuntimeController = new LevelRuntimeController(
            document,
            GLOBALS.beatmapPlayer,
            _dialogueFont,
            GraphicsDevice,
            ReturnToMainMenu,
            SwitchFirstAvailableRhythmGameScene,
            SwitchRhythmGameScene);
    }

    private void ReturnToMainMenu()
    {
        ClearEditorsAndRuntime();
        _appMode = AppMode.MainMenu;
        _sceneManager.SetScene(CreateMainMenu());
    }

    private MainMenu CreateMainMenu()
    {
        return new MainMenu(_inputActionManager, document => StartLevel(document, ignoreLocks: false), OpenLevelEditor, OpenBeatmapEditor);
    }

    private void ClearEditorsAndRuntime()
    {
        GLOBALS.beatmapEditorElement = null;
        _levelEditorElement = null;
        _levelRuntimeController = null;
        GLOBALS.beatmapPlayer?.StopBeatmap();
        GLOBALS.SfxVolume = 0.0f;
        _currentRhythmGameSceneId = null;
    }

    private bool Pressed(Keys key)
    {
        return _keyboard.IsKeyDown(key) && !_previousKeyboard.IsKeyDown(key);
    }

    private void SwitchRhythmGameScene(string rhythmGameId)
    {
        if (_appMode != AppMode.BeatmapEditor && _appMode != AppMode.LevelRuntime)
            return;

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
