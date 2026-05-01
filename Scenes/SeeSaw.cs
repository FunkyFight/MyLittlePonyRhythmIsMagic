using System;
using GameCore.Animation;
using GameCore.GameObjects;
using GameCore.Graphics;
using GameCore.Scenes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MLP_RiM;
using MLP_RiM.Elements.Editor;
using Rhythm.Note;
using Rhythm.Note.Visual;
using TexturePackerMonoGameDefinitions;
using System.Collections.Generic;

/// <summary>
/// Runtime scene for the See-Saw rhythm game.
/// </summary>
/// <remarks>
/// The important rule in this scene is that the stable timeline and the in-flight animation
/// are separated:
///
/// - ApplyTimelineBaseState rebuilds where the actors should be after all already-hit notes.
/// - SeeSawVisualNote temporarily overrides those positions while the current note is in flight.
/// - _drivingVisualNote chooses which active visual note is allowed to mutate shared actors.
///
/// Keeping those responsibilities separate makes seeking, rewinding, and overlapping visual
/// windows predictable. When adding new See-Saw actions, update the action-to-target helpers
/// first, then add a visual construction branch in CreateVisualNote.
/// </remarks>
public class SeeSawScene : Scene
{
    private const string ActionDataKey = "action";
    private const double TimelineRewindThreshold = 0.001;
    private const double MaxVisualLookAheadBeats = 4.0;
    private const float OuterJumpHandoffProgression = 0.4375f;
    private const float BeamTiltDegrees = 10f;

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

    private Vector2 applejackOuterPos;
    private Vector2 applejackInnerPos;
    private Vector2 applejackExitPos;
    private Vector2 rainbowOuterPos;
    private Vector2 rainbowInnerPos;
    private ChartPlayer _visualChartPlayer;

    // VisualNoteManager can update several active notes in the same frame. Only this note is
    // allowed to write to Rainbow, Applejack, and the beam; other visual notes still update
    // their internal VisualNote state but their canApplyState guard blocks scene mutations.
    private Note _drivingVisualNote;
    private double _lastSongPosition = double.NaN;

    public SeeSawScene(Game1 game) : base("See Saw")
    {
        this.game = game;
    }

    /// <summary>
    /// Creates the scene GameObjects, places them from the viewport, registers animation state
    /// machines, and subscribes to beatmap start events.
    /// </summary>
    public override void OnLoad()
    {
        Viewport vp = GLOBALS.graphicsDevice.Viewport;
        
        SeeSaw1 = new GameObject(new GameCore.Graphics.Sprite(GLOBALS.main_atlas.GetRegion(MainAtlas.Sprite_0001)));
        SeeSaw2 = new GameObject(new GameCore.Graphics.Sprite(GLOBALS.main_atlas.GetRegion(MainAtlas.Sprite_0002)));
        Applejack = new GameObject(new GameCore.Graphics.Sprite(GLOBALS.main_atlas.GetRegion(MainAtlas.Applejack_afk1)));
        Rainbow = new GameObject(new GameCore.Graphics.Sprite(GLOBALS.main_atlas.GetRegion(MainAtlas.Rainbowdash_idle1)));

        SeeSaw1.sprite.CenterOrigin();
        SeeSaw2.sprite.CenterOrigin();

        SeeSaw1.Scale = new Vector2(seeSawScale, seeSawScale);
        SeeSaw2.Scale = new Vector2(seeSawScale+0.5f, seeSawScale);
        Applejack.Scale = new Vector2(ponyScale, ponyScale);
        Rainbow.Scale = new Vector2(ponyScale, ponyScale);

        SeeSaw2.Position = new Vector2(vp.Width * 0.4914f, vp.Height * 0.8008f);
        SeeSaw1.Position = new Vector2(vp.Width / 2, vp.Height / 2 + (vp.Height / 5) * 1.5f);
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

        SeeSaw2.Rotation = MathHelper.ToRadians(BeamTiltDegrees);
        Rainbow.Rotation = MathHelper.ToRadians(BeamTiltDegrees);

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

    /// <summary>
    /// Rebuilds the visual note manager for the currently loaded beatmap.
    /// </summary>
    /// <remarks>
    /// This is called when a beatmap starts because the visual timeline depends on the current
    /// chart and BPM. A separate <see cref="ChartPlayer"/> is used so visual look-ahead does not
    /// affect gameplay input/reaction state.
    /// </remarks>
    private void SetupVisuals()
    {
        double crotchet = 60.0 / GLOBALS.beatmapPlayer.Conductor.BPM;

        // Visuals use their own ChartPlayer so input/reaction state stays owned by the beatmap player.
        _visualChartPlayer = new ChartPlayer(GLOBALS.beatmapPlayer.CurrentChart, Rhythm.Note.ReactionRules.RhythmHeavenLike());
        seeSawVisuals = new VisualNoteManager<SeeSawVisualNote>(_visualChartPlayer, note => CreateVisualNote(note, crotchet));

        seeSawVisuals.LookBehindSeconds = 0;
        seeSawVisuals.LookAheadSeconds = crotchet * MaxVisualLookAheadBeats;
        _lastSongPosition = double.NaN;
    }

    /// <summary>
    /// Converts a rhythm note into the See-Saw visual animation that should represent it.
    /// </summary>
    /// <param name="note">The runtime chart note to convert.</param>
    /// <param name="crotchet">Beat duration in seconds at the current BPM.</param>
    /// <returns>
    /// A configured visual note for known See-Saw actions, or <c>null</c> when the note is not a
    /// See-Saw note and should be ignored by the visual manager.
    /// </returns>
    private SeeSawVisualNote CreateVisualNote(Note note, double crotchet)
    {
        // Notes without a known See-Saw action are intentionally skipped. VisualNoteManager
        // remembers skipped notes until Reset, so action parsing must stay deterministic.
        if (!TryGetSeeSawAction(note, out SeeSawAction action))
            return null;

        // These values are computed from the timeline, not from current GameObject positions.
        // Current positions may already include an in-flight visual override from another note.
        Vector2 rainbowFromPos = GetRainbowPositionBefore(note);
        Vector2 applejackFromPos = GetApplejackPositionBefore(note);
        float fromRotation = GetBeamRotationBefore(note);
        float counterJumpMultiplier = action.IsBigLeap && WasPreviousSeeSawActionBigLeap(note) ? 4f : 1f;
        bool previousWasBigLeap = action.IsBigLeap && WasPreviousSeeSawActionBigLeap(note);

        return action.Direction switch
        {
            SeeSawDirection.Outer => CreatePairedJumpVisual(
                note,
                crotchet,
                rainbowFromPos,
                rainbowOuterPos,
                applejackFromPos,
                applejackOuterPos,
                fromRotation,
                counterRotationProgression: OuterJumpHandoffProgression,
                jumperStartProgression: OuterJumpHandoffProgression),

            SeeSawDirection.Inner => CreatePairedJumpVisual(
                note,
                crotchet,
                rainbowFromPos,
                rainbowInnerPos,
                applejackFromPos,
                applejackInnerPos,
                fromRotation),

            SeeSawDirection.Opposite => CreateOppositeJumpVisual(note, crotchet, rainbowFromPos, applejackFromPos, fromRotation),

            SeeSawDirection.OuterBigLeap => CreatePairedJumpVisual(
                note,
                crotchet,
                rainbowFromPos,
                rainbowOuterPos,
                applejackFromPos,
                applejackOuterPos,
                fromRotation,
                approachDuration: crotchet * 4.0,
                isBigLeap: true,
                jumpMultiplier: 4,
                counterJumpMultiplier: counterJumpMultiplier,
                counterJumpDuration: previousWasBigLeap ? crotchet * 4.0 : (double?)null),

            SeeSawDirection.InnerBigLeap => CreatePairedJumpVisual(
                note,
                crotchet,
                rainbowFromPos,
                rainbowInnerPos,
                applejackFromPos,
                applejackInnerPos,
                fromRotation,
                approachDuration: crotchet * 4.0,
                isBigLeap: true,
                jumpMultiplier: 4,
                counterJumpMultiplier: counterJumpMultiplier,
                counterJumpDuration: previousWasBigLeap ? crotchet * 4.0 : (double?)null),

            SeeSawDirection.OppositeBigLeap => CreateOppositeJumpVisual(note, crotchet, rainbowFromPos, applejackFromPos, fromRotation, isBigLeap: true, jumpMultiplier: 4, counterJumpMultiplier: counterJumpMultiplier, counterJumpDuration: previousWasBigLeap ? crotchet * 4.0 : (double?)null),
            _ => null
        };
    }

    /// <summary>
    /// Creates a standard two-actor jump where Applejack moves first and Rainbow Dash lands on
    /// the note hit time.
    /// </summary>
    /// <param name="note">The chart note driving this visual.</param>
    /// <param name="crotchet">Beat duration in seconds at the current BPM.</param>
    /// <param name="rainbowFromPos">Rainbow Dash stable position before the note.</param>
    /// <param name="rainbowToPos">Rainbow Dash stable target after the note.</param>
    /// <param name="applejackFromPos">Applejack stable position before the note.</param>
    /// <param name="applejackToPos">Applejack stable target after the note.</param>
    /// <param name="fromRotation">Beam rotation before the visual starts applying state.</param>
    /// <param name="counterRotationProgression">Normalized note progress where Applejack's beam tilt applies.</param>
    /// <param name="jumperStartProgression">Normalized note progress where Rainbow Dash starts jumping.</param>
    /// <returns>A visual note configured for the paired jump choreography.</returns>
    private SeeSawVisualNote CreatePairedJumpVisual(Note note, double crotchet, Vector2 rainbowFromPos, Vector2 rainbowToPos, Vector2 applejackFromPos, Vector2 applejackToPos, float fromRotation, float counterRotationProgression = 0.5f, float jumperStartProgression = 0.5f, double? approachDuration = null, bool isBigLeap = false, float jumpMultiplier = 1f, float counterJumpMultiplier = 1f, double? counterJumpDuration = null)
    {
        // Paired jumps are the standard choreography: Applejack jumps first, the beam tilts
        // toward her landing side, then Rainbow jumps and owns the final hit timing.
        SeeSawJumpPath rainbowPath = new(rainbowFromPos, rainbowToPos, rainbowInnerPos, rainbowOuterPos);
        SeeSawCounterJump applejackCounterJump = new(
            SeeSawJumper.APPLEJACK,
            new SeeSawJumpPath(applejackFromPos, applejackToPos, applejackInnerPos, applejackOuterPos, counterJumpMultiplier),
            GetBeamRotationToward(SeeSawJumper.APPLEJACK));

        double visualApproachDuration = approachDuration ?? rainbowPath.GetApproachDuration(crotchet);
        float counterJumpEndProgression = counterJumpDuration.HasValue
            ? (float)(counterJumpDuration.Value / visualApproachDuration)
            : counterRotationProgression;

        return CreateRainbowDrivenVisual(note, crotchet, rainbowPath, applejackCounterJump, fromRotation, counterRotationProgression, jumperStartProgression, approachDuration: visualApproachDuration, isBigLeap: isBigLeap, jumpMultiplier: jumpMultiplier, counterJumpEndProgression: counterJumpEndProgression);
    }

    /// <summary>
    /// Creates the visual for an opposite-side action.
    /// </summary>
    /// <param name="note">The chart note driving this visual.</param>
    /// <param name="crotchet">Beat duration in seconds at the current BPM.</param>
    /// <param name="rainbowFromPos">Rainbow Dash stable position before the note.</param>
    /// <param name="applejackFromPos">Applejack stable position before the note.</param>
    /// <param name="fromRotation">Beam rotation before the visual starts applying state.</param>
    /// <returns>A visual note where Rainbow Dash targets the side opposite Applejack.</returns>
    private SeeSawVisualNote CreateOppositeJumpVisual(Note note, double crotchet, Vector2 rainbowFromPos, Vector2 applejackFromPos, float fromRotation, bool isBigLeap = false, float jumpMultiplier = 1f, float counterJumpMultiplier = 1f, double? counterJumpDuration = null)
    {
        // Opposite means Rainbow lands on the side opposite Applejack's current stable side.
        // Applejack does not move for this action.
        Vector2 rainbowTargetPos = GetOppositeRainbowPosition(applejackFromPos);
        SeeSawJumpPath rainbowPath = new(rainbowFromPos, rainbowTargetPos, rainbowInnerPos, rainbowOuterPos);
        SeeSawJumpPath applejackStationaryPath = new(applejackFromPos, applejackFromPos, applejackInnerPos, applejackOuterPos);
        SeeSawCounterJump applejackCounterJump = new(
            SeeSawJumper.APPLEJACK,
            new SeeSawJumpPath(applejackFromPos, applejackFromPos, applejackInnerPos, applejackOuterPos, counterJumpMultiplier),
            GetBeamRotationToward(SeeSawJumper.APPLEJACK));

        double approachDuration = isBigLeap ? crotchet * 4.0 : applejackStationaryPath.GetApproachDuration(crotchet);
        return CreateRainbowDrivenVisual(note, crotchet, rainbowPath, applejackCounterJump, fromRotation, approachDuration: approachDuration, isBigLeap: isBigLeap, jumpMultiplier: jumpMultiplier);
    }

    /// <summary>
    /// Creates the common visual-note object for all See-Saw actions where Rainbow Dash owns the
    /// final hit timing.
    /// </summary>
    /// <param name="note">The chart note driving this visual.</param>
    /// <param name="crotchet">Beat duration in seconds at the current BPM.</param>
    /// <param name="rainbowPath">Rainbow Dash jump path.</param>
    /// <param name="applejackCounterJump">Applejack's first-half counter jump configuration.</param>
    /// <param name="fromRotation">Beam rotation before the visual starts applying state.</param>
    /// <param name="counterRotationProgression">Normalized note progress where Applejack's beam tilt applies.</param>
    /// <param name="jumperStartProgression">Normalized note progress where Rainbow Dash starts jumping.</param>
    /// <param name="approachDuration">Optional duration override for actions whose cue timing is not based on Rainbow Dash.</param>
    /// <returns>A configured <see cref="SeeSawVisualNote"/>.</returns>
    private SeeSawVisualNote CreateRainbowDrivenVisual(Note note, double crotchet, SeeSawJumpPath rainbowPath, SeeSawCounterJump? applejackCounterJump, float fromRotation, float counterRotationProgression = 0.5f, float jumperStartProgression = 0.5f, double? approachDuration = null, bool isBigLeap = false, float jumpMultiplier = 1f, float counterJumpEndProgression = 0.5f)
    {
        // Rainbow is always the main jumper for these chart actions. The final beam rotation
        // is therefore the Rainbow side, while Applejack's optional counter jump controls the
        // first handoff during the approach.
        return new SeeSawVisualNote(
            note,
            seeSawJumpers,
            seeSawAnimationStates,
            SeeSawJumper.RAINBOW_DASH,
            rainbowPath,
            crotchet,
            SeeSaw2,
            sceneCamera,
            fromRotation,
            GetBeamRotationToward(SeeSawJumper.RAINBOW_DASH),
            applejackCounterJump,
            counterRotationProgression,
            jumperStartProgression,
            canApplyState: () => ReferenceEquals(_drivingVisualNote, note),
            approachDuration: approachDuration, 
            isBigLeap: isBigLeap,
            jumpMultiplier: jumpMultiplier,
            counterJumpEndProgression: counterJumpEndProgression);
    }

    /// <summary>
    /// Applies the stable, post-hit actor positions and beam rotation for a song position.
    /// </summary>
    /// <param name="songPosition">Current song position in seconds.</param>
    /// <remarks>
    /// This method ignores in-flight arcs. It replays completed chart actions from the start of
    /// the chart to know where each actor should stand after all notes at or before the current
    /// time have resolved.
    /// </remarks>
    private void ApplyTimelineBaseState(double songPosition)
    {
        Vector2 rainbowPosition = rainbowOuterPos;
        Vector2 applejackPosition = applejackExitPos;
        float beamRotation = MathHelper.ToRadians(BeamTiltDegrees);

        // Rebuild the stable state from the note timeline every frame. This keeps editor seeking
        // and rewinds deterministic, then the currently active visual note adds in-flight motion.
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

    /// <summary>
    /// Finds the active note that is allowed to mutate the shared scene actors this frame.
    /// </summary>
    /// <param name="songPosition">Current song position in seconds.</param>
    /// <returns>The driving note for the current approach window, or <c>null</c> if none is active.</returns>
    private Note FindDrivingVisualNote(double songPosition)
    {
        // Only the latest note whose active window contains the cursor may drive shared objects.
        // Older overlapping visuals stay alive, but their canApplyState guard prevents mutations.
        Note drivingNote = null;

        foreach (Note note in _visualChartPlayer.Notes)
        {
            if (!IsSeeSawNote(note))
                continue;

            double start = note.SongPosition - GetApproachDuration(note);
            if (songPosition >= start && songPosition <= note.SongPosition)
                drivingNote = note;

            if (note.SongPosition > songPosition)
                break;
        }

        return drivingNote;
    }

    /// <summary>
    /// Computes how far before the hit time a See-Saw visual note should become active.
    /// </summary>
    /// <param name="note">The note whose approach duration should be computed.</param>
    /// <returns>Approach duration in seconds.</returns>
    private double GetApproachDuration(Note note)
    {
        double crotchet = 60.0 / GLOBALS.beatmapPlayer.Conductor.BPM;

        // Opposite actions are cued from Applejack's side: Rainbow chooses the opposite target,
        // but the lead-in length follows Applejack's current stable position.
        if (TryGetSeeSawAction(note, out SeeSawAction action))
        {
            if (action.IsBigLeap)
                return crotchet * 4.0;

            if (GetBaseDirection(action.Direction) == SeeSawDirection.Opposite)
            {
                Vector2 applejackFromPosition = GetApplejackPositionBefore(note);
                return SeeSawVisualNote.GetApproachDuration(crotchet, applejackFromPosition, applejackInnerPos, applejackOuterPos);
            }
        }

        Vector2 rainbowFromPosition = GetRainbowPositionBefore(note);
        return SeeSawVisualNote.GetApproachDuration(crotchet, rainbowFromPosition, rainbowInnerPos, rainbowOuterPos);
    }

    /// <summary>
    /// Checks whether a note contains a recognized See-Saw action.
    /// </summary>
    /// <param name="note">The note to inspect.</param>
    /// <returns><c>true</c> when the note is a known See-Saw action; otherwise <c>false</c>.</returns>
    private bool IsSeeSawNote(Note note)
    {
        return TryGetSeeSawAction(note, out _);
    }

    /// <summary>
    /// Gets Rainbow Dash's stable position immediately before a target note resolves.
    /// </summary>
    /// <param name="targetNote">The note whose pre-state should be evaluated.</param>
    /// <returns>Rainbow Dash's stable pre-note position.</returns>
    private Vector2 GetRainbowPositionBefore(Note targetNote)
    {
        // Replay previous chart actions instead of reading Rainbow.Position. The GameObject may
        // currently be mid-air, while this method needs the stable pre-note target.
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

    /// <summary>
    /// Gets Applejack's stable position immediately before a target note resolves.
    /// </summary>
    /// <param name="targetNote">The note whose pre-state should be evaluated.</param>
    /// <returns>Applejack's stable pre-note position.</returns>
    private Vector2 GetApplejackPositionBefore(Note targetNote)
    {
        // Applejack starts outside the See-Saw. From there, only Outer and Inner actions move
        // her stable target; Opposite leaves her on the same side.
        Vector2 position = applejackExitPos;

        foreach (Note note in _visualChartPlayer.Notes)
        {
            if (note == targetNote || Math.Abs(note.SongPosition - targetNote.SongPosition) <= 0.0005)
                break;

            position = GetApplejackTargetPosition(note, position);
        }

        return position;
    }

    /// <summary>
    /// Gets Rainbow Dash's stable target position after a note resolves.
    /// </summary>
    /// <param name="note">The note to apply.</param>
    /// <param name="fallback">Position to keep when the note is not a recognized See-Saw action.</param>
    /// <param name="currentApplejackPosition">Applejack's current stable position, used for opposite-side targeting.</param>
    /// <returns>Rainbow Dash's stable target position after the note.</returns>
    private Vector2 GetRainbowTargetPosition(Note note, Vector2 fallback, Vector2 currentApplejackPosition)
    {
        // This is the stable post-hit position, not the animated position during the jump.
        if (!TryGetSeeSawAction(note, out SeeSawAction action))
            return fallback;

        return GetBaseDirection(action.Direction) switch
        {
            SeeSawDirection.Outer => rainbowOuterPos,
            SeeSawDirection.Inner => rainbowInnerPos,
            SeeSawDirection.Opposite => GetOppositeRainbowPosition(currentApplejackPosition),
            _ => fallback
        };
    }

    /// <summary>
    /// Gets Applejack's stable target position after a note resolves.
    /// </summary>
    /// <param name="note">The note to apply.</param>
    /// <param name="fallback">Position to keep when the note does not move Applejack.</param>
    /// <returns>Applejack's stable target position after the note.</returns>
    private Vector2 GetApplejackTargetPosition(Note note, Vector2 fallback)
    {
        // Applejack mirrors Rainbow for inner/outer notes. Opposite notes are Rainbow-only
        // target changes, so Applejack keeps her current stable side.
        if (!TryGetSeeSawAction(note, out SeeSawAction action))
            return fallback;

        return GetBaseDirection(action.Direction) switch
        {
            SeeSawDirection.Outer => applejackOuterPos,
            SeeSawDirection.Inner => applejackInnerPos,
            SeeSawDirection.Opposite => fallback,
            _ => fallback
        };
    }

    private static SeeSawDirection GetBaseDirection(SeeSawDirection direction)
    {
        return direction switch
        {
            SeeSawDirection.OuterBigLeap => SeeSawDirection.Outer,
            SeeSawDirection.InnerBigLeap => SeeSawDirection.Inner,
            SeeSawDirection.OppositeBigLeap => SeeSawDirection.Opposite,
            _ => direction
        };
    }

    private bool WasPreviousSeeSawActionBigLeap(Note targetNote)
    {
        SeeSawAction previousAction = default;
        bool hasPreviousAction = false;

        foreach (Note note in _visualChartPlayer.Notes)
        {
            if (note == targetNote || Math.Abs(note.SongPosition - targetNote.SongPosition) <= 0.0005)
                break;

            if (TryGetSeeSawAction(note, out SeeSawAction action))
            {
                previousAction = action;
                hasPreviousAction = true;
            }
        }

        return hasPreviousAction && previousAction.IsBigLeap;
    }

    /// <summary>
    /// Gets the Rainbow Dash side opposite Applejack's stable side.
    /// </summary>
    /// <param name="currentApplejackPosition">Applejack's stable position.</param>
    /// <returns>Rainbow Dash inner position when Applejack is outer; otherwise Rainbow Dash outer position.</returns>
    private Vector2 GetOppositeRainbowPosition(Vector2 currentApplejackPosition)
    {
        return currentApplejackPosition == applejackOuterPos ? rainbowInnerPos : rainbowOuterPos;
    }

    /// <summary>
    /// Gets the stable beam rotation after a note resolves.
    /// </summary>
    /// <param name="note">The note to apply.</param>
    /// <param name="fallback">Rotation to keep when the note is not a recognized See-Saw action.</param>
    /// <returns>The post-note beam rotation in radians.</returns>
    private float GetBeamTargetRotation(Note note, float fallback)
    {
        return TryGetSeeSawAction(note, out _)
            ? GetBeamRotationToward(SeeSawJumper.RAINBOW_DASH)
            : fallback;
    }

    /// <summary>
    /// Reads and parses the See-Saw action stored in a note's additional data.
    /// </summary>
    /// <param name="note">The note to inspect.</param>
    /// <param name="action">Parsed action when this method returns <c>true</c>.</param>
    /// <returns><c>true</c> when the note contains a known action string; otherwise <c>false</c>.</returns>
    private bool TryGetSeeSawAction(Note note, out SeeSawAction action)
    {
        // Use the editor's action model as the single source of truth for recognized action
        // strings. This avoids runtime/editor drift when new variants are added.
        if (note.AdditionnalData != null
            && note.AdditionnalData.TryGetValue(ActionDataKey, out string actionValue)
            && SeeSawAction.TryParse(actionValue, out action))
        {
            return true;
        }

        action = default;
        return false;
    }

    /// <summary>
    /// Gets the stable beam rotation immediately before a target note resolves.
    /// </summary>
    /// <param name="targetNote">The note whose pre-state should be evaluated.</param>
    /// <returns>Beam rotation in radians before the target note.</returns>
    private float GetBeamRotationBefore(Note targetNote)
    {
        float rotation = MathHelper.ToRadians(BeamTiltDegrees);

        foreach (Note note in _visualChartPlayer.Notes)
        {
            if (note == targetNote)
                break;

            rotation = GetBeamTargetRotation(note, rotation);
        }

        return rotation;
    }

    // The beam tilts toward the side of the last jumper that landed.
    /// <summary>
    /// Gets the beam tilt caused by the last jumper that landed.
    /// </summary>
    /// <param name="lander">The jumper whose landing side should receive the beam tilt.</param>
    /// <returns>Beam rotation in radians.</returns>
    private float GetBeamRotationToward(SeeSawJumper lander)
    {
        return lander == SeeSawJumper.RAINBOW_DASH
            ? MathHelper.ToRadians(BeamTiltDegrees)
            : MathHelper.ToRadians(-BeamTiltDegrees);
    }

    /// <summary>
    /// Resets active visual notes and animation states after timeline seeking or rewinding.
    /// </summary>
    private void ResetAnimationStateForTimeline()
    {
        // Rewinding invalidates one-shot animation triggers inside active visual notes. Reset
        // the manager and choose an idle state that matches Applejack's stable timeline position.
        seeSawVisuals?.Reset();
        RainbowState.ForceState("idle");

        if (Vector2.Distance(Applejack.Position, applejackExitPos) < 0.01f)
            ApplejackState.ForceState("start_idle");
        else
            ApplejackState.ForceState("idle");
    }

    /// <summary>
    /// Restores the actors and beam to the initial scene state before chart playback starts.
    /// </summary>
    private void ResetActors()
    {
        Rainbow.Position = rainbowOuterPos;
        Applejack.Position = applejackExitPos;
        SeeSaw2.Rotation = MathHelper.ToRadians(BeamTiltDegrees);
        Rainbow.Rotation = MathHelper.ToRadians(BeamTiltDegrees);
        Applejack.Rotation = 0;
    }

    public override void OnUnload()
    {
    }

    /// <summary>
    /// Updates actor animation state machines and applies the current deterministic visual state.
    /// </summary>
    /// <param name="gameTime">MonoGame frame timing information.</param>
    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        RainbowState.Update(gameTime);
        ApplejackState?.Update(gameTime);
        if (GLOBALS.beatmapPlayer.Conductor != null && seeSawVisuals != null)
        {
            double songPosition = GLOBALS.beatmapPlayer.Conductor.SongPosition;
            bool rewound = !double.IsNaN(_lastSongPosition) && songPosition < _lastSongPosition - TimelineRewindThreshold;

            // Order matters:
            // 1. Restore stable post-hit state for this song position.
            // 2. Reset animation one-shots if the cursor moved backwards.
            // 3. Select the note that owns shared actor mutations.
            // 4. Let visual notes apply the in-flight override for that owner.
            ApplyTimelineBaseState(songPosition);

            if (rewound)
                ResetAnimationStateForTimeline();

            _drivingVisualNote = FindDrivingVisualNote(songPosition);
            seeSawVisuals.Update(songPosition);
            _lastSongPosition = songPosition;
        }
    }

    /// <summary>
    /// Draws the See-Saw scene in the required back-to-front order.
    /// </summary>
    /// <param name="spriteBatch">Sprite batch used for drawing.</param>
    public override void Draw(SpriteBatch spriteBatch)
    {
        GLOBALS.graphicsDevice.Clear(Color.Cyan);

        base.Draw(spriteBatch);
    }

    /// <summary>
    /// Builds animation state machines for Rainbow Dash and Applejack.
    /// </summary>
    /// <remarks>
    /// The state names are used by <see cref="SeeSawVisualNote"/>. Renaming states here requires
    /// updating the constants in that class as well.
    /// </remarks>
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
