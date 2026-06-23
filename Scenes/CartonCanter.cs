using System;
using System.Collections.Generic;
using GameCore.Audio;
using GameCore.GameObjects;
using GameCore.Graphics;
using GameCore.Scenes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MLP_RiM.Elements.Editor;
using Rhythm.Note;
using Rhythm.Note.Visual;
using TexturePackerMonoGameDefinitions;

public class CartonCanter : Scene
{
    private const float ShadowDarkness = 0.55f;
    private const byte ShadowAlpha = 145;
    private const float BoxScale = 8.75f;
    private const float PonyScale = 6.2f;
    private const double CueDropBeats = 0.22;
    private const double PonyFallBeats = 0.38;
    private const double BoxBounceBeats = 1.0;
    private const float BoxYRatio = 0.64f;
    private const float OffscreenPadding = 90f;
    private const float PonyHiddenTopPadding = 8f;
    private const float PonyCueRevealPixels = 148f;
    private const float PonyHorizontalOffset = 24f;
    private const float CueSfxVolume = 0.45f;
    private static readonly Vector2 ShadowOffset = new(-32f, 28f);
    private const string ReactSfx = "SFX/CartonCanter/react.wav";
    private const string FailSfx = "SFX/CartonCanter/fail.wav";
    private static readonly string[] CueSfx =
    {
        "SFX/CartonCanter/squeak1.wav",
        "SFX/CartonCanter/squeak2.wav",
        "SFX/CartonCanter/squeakl3.wav"
    };
    private static readonly Dictionary<string, TextureRegion> ShadowRegions = new(StringComparer.Ordinal);

    private VisualNoteManager<CartonCanterVisualNote> _visualNoteManager;
    private ChartPlayer _reactionChartPlayer;

    public CartonCanter() : base("Carton Canter")
    {
    }

    public override void OnLoad()
    {
        SetupVisualNotes();
        SetupReactionFeedbacks();
        GLOBALS.beatmapPlayer.BeatmapStarted += SetupVisualNotes;
        GLOBALS.beatmapPlayer.BeatmapStarted += SetupReactionFeedbacks;
    }

    public override void OnUnload()
    {
        GLOBALS.beatmapPlayer.BeatmapStarted -= SetupVisualNotes;
        GLOBALS.beatmapPlayer.BeatmapStarted -= SetupReactionFeedbacks;
        UnsubscribeReactionFeedbacks();
        _visualNoteManager = null;
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        if(GLOBALS.beatmapPlayer.Conductor == null || GLOBALS.beatmapPlayer.ChartPlayer == null)
            return;

        _visualNoteManager?.Update(GLOBALS.beatmapPlayer.Conductor.SongPosition);
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        GLOBALS.graphicsDevice.Clear(new Color(190, 156, 120));
        _visualNoteManager?.Draw(spriteBatch);
    }

    private void SetupVisualNotes()
    {
        if(GLOBALS.beatmapPlayer.ChartPlayer == null)
        {
            _visualNoteManager = null;
            return;
        }

        _visualNoteManager = new VisualNoteManager<CartonCanterVisualNote>(GLOBALS.beatmapPlayer.ChartPlayer, CreateVisualNote)
        {
            LookAheadSeconds = GetMaxCrotchet() * 10.0,
            LookBehindSeconds = GetMaxCrotchet() * 3.0
        };
    }

    private void SetupReactionFeedbacks()
    {
        UnsubscribeReactionFeedbacks();
        _reactionChartPlayer = GLOBALS.beatmapPlayer.ChartPlayer;
        if(_reactionChartPlayer != null)
            _reactionChartPlayer.NoteReactedWithNote += OnNoteReacted;
    }

    private void UnsubscribeReactionFeedbacks()
    {
        if(_reactionChartPlayer == null)
            return;

        _reactionChartPlayer.NoteReactedWithNote -= OnNoteReacted;
        _reactionChartPlayer = null;
    }

    private void OnNoteReacted(NoteReactionResult result, Note note)
    {
        if(!CartonCanterNoteCodec.TryReadAction(note?.AdditionnalData, out _))
            return;

        SFX.Play(this, result == NoteReactionResult.MISS ? FailSfx : ReactSfx, 1f);
    }

    private CartonCanterVisualNote CreateVisualNote(Note note)
    {
        return CartonCanterNoteCodec.TryReadAction(note?.AdditionnalData, out CartonCanterAction action)
            ? new CartonCanterVisualNote(this, note, action)
            : null;
    }

    private double GetBeatAt(double songPosition)
    {
        double crotchet = Math.Max(0.001, GetCrotchetAt(songPosition));
        return GLOBALS.beatmapPlayer?.GetBeatAt(songPosition) ?? songPosition / crotchet;
    }

    private double GetCrotchetAt(double songPosition)
    {
        return GLOBALS.beatmapPlayer?.GetCrotchetAt(songPosition) ?? 0.6;
    }

    private double GetMaxCrotchet()
    {
        return GLOBALS.beatmapPlayer?.GetMaxCrotchet() ?? 0.6;
    }

    private sealed class CartonCanterVisualNote : VisualNote
    {
        private readonly CartonCanter _scene;
        private readonly CartonCanterAction _action;
        private readonly GameObject _box;
        private readonly GameObject _pony;
        private readonly double _hitBeat;
        private readonly double _reactOffsetBeats;
        private readonly IReadOnlyList<double> _cueOffsets;
        private readonly bool[] _cueSfxPlayed;
        private string _currentPonySprite;
        private double _lastBeat = double.NaN;

        public CartonCanterVisualNote(CartonCanter scene, Note note, CartonCanterAction action)
            : base(note, GetApproachDuration(note, action), GetExitDuration(note))
        {
            _scene = scene;
            _action = action;
            _reactOffsetBeats = CartonCanterProvider.GetReactOffsetBeats(action);
            _cueOffsets = CartonCanterProvider.GetCueOffsetsBeats(action);
            _cueSfxPlayed = new bool[_cueOffsets.Count];
            _hitBeat = _scene.GetBeatAt(note.SongPosition);

            _box = new GameObject(GLOBALS.main_atlas.CreateSprite(GetBoxSprite(action)))
            {
                Scale = Vector2.One * BoxScale
            };
            _box.sprite.CenterOrigin();

            _pony = new GameObject(null)
            {
                Scale = Vector2.One * PonyScale
            };
            SetPonySprite(GetCuePonySprite(action));
        }

        public override void Update(double currentSongPosition)
        {
            double currentBeat = _scene.GetBeatAt(currentSongPosition);
            double startBeat = GetStartBeat();
            double despawnBeat = _hitBeat + CartonCanterProvider.ExitBeats;
            double approachProgress = _reactOffsetBeats <= 0.0 ? 1.0 : Math.Clamp((currentBeat - startBeat) / _reactOffsetBeats, 0.0, 1.0);
            double unclampedProgress = _reactOffsetBeats <= 0.0 ? 1.0 : (currentBeat - startBeat) / _reactOffsetBeats;
            double postHitProgress = Math.Clamp((currentBeat - _hitBeat) / CartonCanterProvider.ExitBeats, 0.0, 1.0);

            SetState(new VisualNoteState(
                Note.SongPosition - currentSongPosition,
                approachProgress,
                currentBeat >= _hitBeat ? 1.0 : 0.0,
                approachProgress,
                postHitProgress,
                unclampedProgress,
                currentBeat >= startBeat && currentBeat <= despawnBeat,
                currentBeat >= startBeat,
                currentBeat > despawnBeat));

            PlayCueSfx(currentBeat);

            if(!ShouldDraw)
            {
                _lastBeat = currentBeat;
                return;
            }

            ApplyPose(currentBeat);
            _lastBeat = currentBeat;
        }

        public override void Draw(SpriteBatch spriteBatch)
        {
            if(!ShouldDraw)
                return;

            DrawShadow(spriteBatch, _pony, _currentPonySprite);
            DrawShadow(spriteBatch, _box, GetBoxSprite(_action));
            _pony.Draw(spriteBatch);
            _box.Draw(spriteBatch);
        }

        private void ApplyPose(double currentBeat)
        {
            Vector2 boxTarget = GetBoxTargetPosition();
            Vector2 boxPosition = GetBoxPosition(currentBeat, boxTarget);
            boxPosition += GetBoxBounceOffset(currentBeat);
            _box.Position = boxPosition;
            _box.Scale = GetBoxScale(currentBeat);
            _box.Rotation = 0f;

            SetPonySprite(GetCurrentPonySprite());
            _pony.Position = GetPonyPosition(currentBeat, boxTarget, boxPosition);
            _pony.Rotation = 0f;
        }

        private Vector2 GetBoxPosition(double currentBeat, Vector2 target)
        {
            double startBeat = GetStartBeat();
            Vector2 hidden = new(target.X, GLOBALS.graphicsDevice.Viewport.Height + GetBoxHalfHeight() + OffscreenPadding);
            float appearProgress = EaseOutCubic((float)((currentBeat - startBeat) / 1.0));
            Vector2 position = Vector2.Lerp(hidden, target, appearProgress);

            if(currentBeat < _hitBeat)
                return position;

            float exitProgress = EaseInCubic((float)((currentBeat - _hitBeat) / CartonCanterProvider.ExitBeats));
            Vector2 exit = new(-GetBoxHalfWidth() - OffscreenPadding, target.Y);
            return Vector2.Lerp(target, exit, exitProgress);
        }

        private Vector2 GetPonyPosition(double currentBeat, Vector2 boxTarget, Vector2 boxPosition)
        {
            Vector2 cueDropPosition = GetPonyCuePosition(boxTarget, 1f);
            Vector2 inBox = new(boxTarget.X + PonyHorizontalOffset, boxTarget.Y - GetBoxHalfHeight() * 0.82f);
            double fallStartBeat = _hitBeat - PonyFallBeats;

            if(currentBeat < fallStartBeat)
            {
                double localBeat = currentBeat - GetStartBeat();
                float revealProgress = GetCueRevealProgress(localBeat);
                return GetPonyCuePosition(boxTarget, revealProgress);
            }

            if(currentBeat < _hitBeat)
            {
                float fallProgress = EaseInCubic((float)((currentBeat - fallStartBeat) / PonyFallBeats));
                return Vector2.Lerp(cueDropPosition, inBox, fallProgress);
            }

            Vector2 boxOffset = boxPosition - boxTarget;
            return inBox + boxOffset;
        }

        private Vector2 GetBoxScale(double currentBeat)
        {
            if(currentBeat < _hitBeat || currentBeat > _hitBeat + BoxBounceBeats)
                return Vector2.One * BoxScale;

            float progress = MathHelper.Clamp((float)((currentBeat - _hitBeat) / BoxBounceBeats), 0f, 1f);
            float impact = 1f - progress;
            float rebound = MathF.Sin(progress * MathHelper.Pi) * impact;
            return new Vector2(
                BoxScale * (1f + 0.10f * impact - 0.04f * rebound),
                BoxScale * (1f - 0.08f * impact + 0.06f * rebound));
        }

        private Vector2 GetBoxBounceOffset(double currentBeat)
        {
            if(currentBeat < _hitBeat || currentBeat > _hitBeat + BoxBounceBeats)
                return Vector2.Zero;

            float progress = MathHelper.Clamp((float)((currentBeat - _hitBeat) / BoxBounceBeats), 0f, 1f);
            float impact = 1f - progress;
            float rebound = MathF.Sin(progress * MathHelper.Pi) * impact;
            return new Vector2(0f, 18f * impact - 16f * rebound);
        }

        private Vector2 GetPonyCuePosition(Vector2 boxTarget, float revealProgress)
        {
            float visiblePixels = MathHelper.Lerp(0f, PonyCueRevealPixels, MathHelper.Clamp(revealProgress, 0f, 1f));
            return new Vector2(boxTarget.X + PonyHorizontalOffset, -GetPonyHalfHeight() - PonyHiddenTopPadding + visiblePixels);
        }

        private float GetCueRevealProgress(double localBeat)
        {
            float progress = 0f;
            for(int i = 0; i < _cueOffsets.Count; i++)
            {
                double cueBeat = _cueOffsets[i];
                if(localBeat < cueBeat)
                    break;

                float cueProgress = EaseOutCubic((float)((localBeat - cueBeat) / CueDropBeats));
                progress = Math.Max(progress, (i + cueProgress) / _cueOffsets.Count);
                if(localBeat >= cueBeat + CueDropBeats)
                    progress = Math.Max(progress, (i + 1f) / _cueOffsets.Count);
            }

            return MathHelper.Clamp(progress, 0f, 1f);
        }

        private void PlayCueSfx(double currentBeat)
        {
            ResetSfxOnRewind(currentBeat);

            if(double.IsNaN(_lastBeat))
                return;

            double startBeat = GetStartBeat();
            for(int i = 0; i < _cueOffsets.Count; i++)
            {
                if(_cueSfxPlayed[i])
                    continue;

                double cueBeat = startBeat + _cueOffsets[i];
                if(!CrossedBeat(_lastBeat, currentBeat, cueBeat))
                    continue;

                _cueSfxPlayed[i] = true;
                SFX.Play(_scene, CueSfx[Math.Min(i, CueSfx.Length - 1)], CueSfxVolume);
            }
        }

        private void ResetSfxOnRewind(double currentBeat)
        {
            if(double.IsNaN(_lastBeat) || currentBeat >= _lastBeat - 0.001)
                return;

            double startBeat = GetStartBeat();
            for(int i = 0; i < _cueSfxPlayed.Length; i++)
                _cueSfxPlayed[i] = currentBeat > startBeat + _cueOffsets[i];
        }

        private static bool CrossedBeat(double previousBeat, double currentBeat, double eventBeat)
        {
            return previousBeat < eventBeat && currentBeat >= eventBeat;
        }

        private string GetCurrentPonySprite()
        {
            if(Note.HasBeenMissed)
                return GetMissPonySprite(_action);

            return Note.HasReacted ? GetSuccessPonySprite(_action) : GetCuePonySprite(_action);
        }

        private void SetPonySprite(string regionName)
        {
            if(_currentPonySprite == regionName)
                return;

            _pony.sprite = GLOBALS.main_atlas.CreateSprite(regionName);
            _pony.sprite.CenterOrigin();
            _currentPonySprite = regionName;
        }

        private static void DrawShadow(SpriteBatch spriteBatch, GameObject gameObject, string regionName)
        {
            if(gameObject?.sprite?.Region == null || string.IsNullOrEmpty(regionName))
                return;

            GetShadowRegion(regionName).Draw(
                spriteBatch,
                gameObject.Position + ShadowOffset + gameObject.sprite.DrawOffset,
                Color.White,
                gameObject.Rotation,
                gameObject.sprite.Origin,
                gameObject.Scale,
                gameObject.sprite.Effects,
                gameObject.sprite.LayerDepth);
        }

        private static TextureRegion GetShadowRegion(string regionName)
        {
            if(ShadowRegions.TryGetValue(regionName, out TextureRegion region))
                return region;

            region = CreateShadowRegion(regionName);
            ShadowRegions[regionName] = region;
            return region;
        }

        private static TextureRegion CreateShadowRegion(string regionName)
        {
            var region = GLOBALS.main_atlas.GetRegion(regionName);
            Rectangle source = region.SourceRectangle;
            Color[] pixels = new Color[source.Width * source.Height];
            region.Texture.GetData(0, source, pixels, 0, pixels.Length);

            long alphaSum = 0;
            long redSum = 0;
            long greenSum = 0;
            long blueSum = 0;
            foreach(Color pixel in pixels)
            {
                if(pixel.A <= 8)
                    continue;

                alphaSum += pixel.A;
                redSum += pixel.R * pixel.A;
                greenSum += pixel.G * pixel.A;
                blueSum += pixel.B * pixel.A;
            }

            byte r = 0;
            byte g = 0;
            byte b = 0;
            if(alphaSum > 0)
            {
                r = ToShadowByte(redSum / (double)alphaSum);
                g = ToShadowByte(greenSum / (double)alphaSum);
                b = ToShadowByte(blueSum / (double)alphaSum);
            }

            for(int i = 0; i < pixels.Length; i++)
            {
                byte alpha = (byte)Math.Clamp(pixels[i].A * ShadowAlpha / 255, 0, 255);
                if(alpha == 0)
                {
                    pixels[i] = Color.Transparent;
                    continue;
                }

                pixels[i] = new Color(
                    (byte)(r * alpha / 255),
                    (byte)(g * alpha / 255),
                    (byte)(b * alpha / 255),
                    alpha);
            }

            Texture2D shadowTexture = new(GLOBALS.graphicsDevice, source.Width, source.Height);
            shadowTexture.SetData(pixels);

            return new TextureRegion(shadowTexture, 0, 0, source.Width, source.Height)
            {
                OriginalWidth = region.OriginalWidth,
                OriginalHeight = region.OriginalHeight,
                TrimOffset = region.TrimOffset,
                Pivot = region.Pivot,
                IsRotated = region.IsRotated,
                AtlasRotationDegrees = region.AtlasRotationDegrees
            };
        }

        private static byte ToShadowByte(double value)
        {
            return (byte)Math.Clamp((int)Math.Round(value * ShadowDarkness), 0, 255);
        }

        private double GetStartBeat()
        {
            return _hitBeat - _reactOffsetBeats;
        }

        private Vector2 GetBoxTargetPosition()
        {
            Viewport viewport = GLOBALS.graphicsDevice.Viewport;
            return new Vector2(viewport.Width * 0.5f, viewport.Height * BoxYRatio);
        }

        private float GetBoxHalfWidth()
        {
            return Math.Max(32f * BoxScale, _box.Width * 0.5f);
        }

        private float GetBoxHalfHeight()
        {
            return Math.Max(32f * BoxScale, _box.Height * 0.5f);
        }

        private float GetPonyHalfHeight()
        {
            return Math.Max(38f * PonyScale, _pony.Height * 0.5f);
        }

        private static double GetApproachDuration(Note note, CartonCanterAction action)
        {
            return CartonCanterProvider.GetReactOffsetBeats(action) * GetCrotchetAt(note);
        }

        private static double GetExitDuration(Note note)
        {
            return CartonCanterProvider.ExitBeats * GetCrotchetAt(note);
        }

        private static double GetCrotchetAt(Note note)
        {
            return Math.Max(0.001, GLOBALS.beatmapPlayer?.GetCrotchetAt(note?.SongPosition ?? 0.0) ?? 0.6);
        }

        private static string GetBoxSprite(CartonCanterAction action)
        {
            return action switch
            {
                CartonCanterAction.Fluttershy => MainAtlas.Box_fluttershy,
                CartonCanterAction.Applejack => MainAtlas.Box_applejack,
                CartonCanterAction.PinkiePie => MainAtlas.Box_pinkiepie,
                _ => MainAtlas.Box_twilight
            };
        }

        private static string GetCuePonySprite(CartonCanterAction action)
        {
            return action switch
            {
                CartonCanterAction.Fluttershy => MainAtlas.Carton_canter_fluttershy1,
                CartonCanterAction.Applejack => MainAtlas.Carton_canter_applejack1,
                CartonCanterAction.PinkiePie => MainAtlas.Carton_canter_pinkie_pie1,
                _ => MainAtlas.Carton_canter_twilight1
            };
        }

        private static string GetSuccessPonySprite(CartonCanterAction action)
        {
            return action switch
            {
                CartonCanterAction.Fluttershy => MainAtlas.Carton_canter_fluttershy2,
                CartonCanterAction.Applejack => MainAtlas.Carton_canter_applejack2,
                CartonCanterAction.PinkiePie => MainAtlas.Carton_canter_pinkie_pie2,
                _ => MainAtlas.Carton_canter_twilight2
            };
        }

        private static string GetMissPonySprite(CartonCanterAction action)
        {
            return action switch
            {
                CartonCanterAction.Fluttershy => MainAtlas.Carton_canter_fluttershy3,
                CartonCanterAction.Applejack => MainAtlas.Carton_canter_applejack3,
                CartonCanterAction.PinkiePie => MainAtlas.Carton_canter_pinkie_pie3,
                _ => MainAtlas.Carton_canter_twilight3
            };
        }

        private static float EaseOutCubic(float value)
        {
            value = MathHelper.Clamp(value, 0f, 1f);
            float inverse = 1f - value;
            return 1f - inverse * inverse * inverse;
        }

        private static float EaseInCubic(float value)
        {
            value = MathHelper.Clamp(value, 0f, 1f);
            return value * value * value;
        }
    }
}
