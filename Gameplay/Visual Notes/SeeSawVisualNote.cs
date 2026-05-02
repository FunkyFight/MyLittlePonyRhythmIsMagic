using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rhythm.Note;
using Rhythm.Note.Visual;
using GameCore.GameObjects;
using System.Collections.Generic;
using GameCore.Animation;
using GameCore.Scenes;
using GameCore;

/// <summary>
/// Plays one See-Saw note animation against scene-owned actors.
/// </summary>
/// <remarks>
/// This class intentionally does not create or own GameObjects. The See-Saw scene owns
/// Rainbow Dash, Applejack, and the beam; each visual note only mutates them while the
/// scene says this note is the current driver. That guard is important because the
/// VisualNoteManager may keep several notes alive at the same time for look-ahead.
///
/// Timing is deterministic and based only on song position, not frame delta. This keeps
/// editor scrubbing and rewinds stable: calling Update with the same song position should
/// always place the actors in the same positions.
/// </remarks>
public class SeeSawVisualNote : VisualNote
{
    private const string JumpState = "jump";
    private const string FallState = "fall";
    private const string LandState = "land";

    // The counter jumper performs the first half of the visual note. The main jumper then
    // starts from _jumperStartProgression and lands exactly on Note.SongPosition.
    private const float CounterJumpEndProgression = 0.5f;

    private readonly Dictionary<SeeSawJumper, GameObject> _jumpers;
    private readonly Dictionary<SeeSawJumper, AnimationStateMachine> _animationStates;
    private readonly SeeSawJumper _jumper;
    private readonly SeeSawJumpPath _jumperPath;
    private readonly SeeSawCounterJump? _counterJump;
    private readonly GameObject _seeSawBeam;
    private readonly Camera _sceneCamera;
    private readonly float _fromRotation;
    private readonly float _targetRotation;
    private readonly float _counterRotationProgression;
    private readonly float _jumperStartProgression;
    private readonly float _counterJumpEndProgression;
    private readonly bool _counterIsBigLeap;
    private readonly Func<bool> _canApplyState;
    private bool _isBigLeap;
    private bool _counterJumpStarted;
    private bool _counterLanded;
    private bool _jumperJumpStarted;
    private bool _jumperLanded;
    private double _lastSongPosition = double.NaN;

    internal const float OuterJumpHeight = 450f;
    internal const float InnerJumpHeight = 180f;
    internal const float BigLeapJumpHeight = OuterJumpHeight * 4f;
    private const float DefaultCounterRotationProgression = 0.5f;
    private const float DefaultJumperStartProgression = 0.5f;
    private const float BigLeapCameraVerticalMargin = 160f;
    private const float LandTriggerProgression = 0.98f;




    /// <summary>
    /// Creates a single-jumper See-Saw visual note from raw position values.
    /// </summary>
    /// <param name="logicalNote">Logical rhythm note represented by this visual.</param>
    /// <param name="jumpers">Scene-owned jumper GameObjects indexed by jumper identity.</param>
    /// <param name="animationStates">Scene-owned animation state machines indexed by jumper identity.</param>
    /// <param name="jumper">The actor that performs the main jump and lands on the note hit time.</param>
    /// <param name="fromPos">Stable position where the main jumper starts.</param>
    /// <param name="toPos">Stable position where the main jumper lands.</param>
    /// <param name="crotchet">Beat duration in seconds at the current BPM.</param>
    /// <param name="seeSawBeam">Scene-owned beam object whose rotation is animated.</param>
    /// <param name="sceneCamera">Scene camera available to camera-aware visual effects.</param>
    /// <param name="fromRotation">Beam rotation before the visual applies state.</param>
    /// <param name="targetRotation">Beam rotation after the main jumper lands.</param>
    /// <param name="innerPos">Reference inner-side position for timing and jump height.</param>
    /// <param name="outerPos">Reference outer-side position for timing and jump height.</param>
    /// <param name="jumpHeightMultiplier">Multiplier applied to the derived vertical jump height.</param>
    /// <param name="despawnDelay">Extra lifetime after the note ends.</param>
    public SeeSawVisualNote(Note logicalNote, Dictionary<SeeSawJumper, GameObject> jumpers, Dictionary<SeeSawJumper, AnimationStateMachine> animationStates, SeeSawJumper jumper, Vector2 fromPos, Vector2 toPos, double crotchet, GameObject seeSawBeam, Camera sceneCamera, float fromRotation, float targetRotation, Vector2 innerPos, Vector2 outerPos, float jumpHeightMultiplier = 1f, double despawnDelay = 0)
        : this(logicalNote, jumpers, animationStates, jumper, fromPos, toPos, crotchet, seeSawBeam, sceneCamera, fromRotation, targetRotation, innerPos, outerPos, null, Vector2.Zero, Vector2.Zero, 0, Vector2.Zero, Vector2.Zero, DefaultCounterRotationProgression, DefaultJumperStartProgression, null, despawnDelay, null, jumpHeightMultiplier)
    {
    }

    public SeeSawVisualNote(Note logicalNote, Dictionary<SeeSawJumper, GameObject> jumpers, Dictionary<SeeSawJumper, AnimationStateMachine> animationStates, SeeSawJumper jumper, Vector2 fromPos, Vector2 toPos, double crotchet, GameObject seeSawBeam, float fromRotation, float targetRotation, Vector2 innerPos, Vector2 outerPos, float jumpHeightMultiplier = 1f, double despawnDelay = 0)
        : this(logicalNote, jumpers, animationStates, jumper, fromPos, toPos, crotchet, seeSawBeam, null, fromRotation, targetRotation, innerPos, outerPos, jumpHeightMultiplier, despawnDelay)
    {
    }

    /// <summary>
    /// Creates a guarded single-jumper See-Saw visual note from raw position values.
    /// </summary>
    /// <param name="logicalNote">Logical rhythm note represented by this visual.</param>
    /// <param name="jumpers">Scene-owned jumper GameObjects indexed by jumper identity.</param>
    /// <param name="animationStates">Scene-owned animation state machines indexed by jumper identity.</param>
    /// <param name="jumper">The actor that performs the main jump and lands on the note hit time.</param>
    /// <param name="fromPos">Stable position where the main jumper starts.</param>
    /// <param name="toPos">Stable position where the main jumper lands.</param>
    /// <param name="crotchet">Beat duration in seconds at the current BPM.</param>
    /// <param name="seeSawBeam">Scene-owned beam object whose rotation is animated.</param>
    /// <param name="sceneCamera">Scene camera available to camera-aware visual effects.</param>
    /// <param name="fromRotation">Beam rotation before the visual applies state.</param>
    /// <param name="targetRotation">Beam rotation after the main jumper lands.</param>
    /// <param name="innerPos">Reference inner-side position for timing and jump height.</param>
    /// <param name="outerPos">Reference outer-side position for timing and jump height.</param>
    /// <param name="canApplyState">Predicate that decides whether this visual may mutate shared scene objects this frame.</param>
    /// <param name="jumpHeightMultiplier">Multiplier applied to the derived vertical jump height.</param>
    /// <param name="despawnDelay">Extra lifetime after the note ends.</param>
    public SeeSawVisualNote(Note logicalNote, Dictionary<SeeSawJumper, GameObject> jumpers, Dictionary<SeeSawJumper, AnimationStateMachine> animationStates, SeeSawJumper jumper, Vector2 fromPos, Vector2 toPos, double crotchet, GameObject seeSawBeam, Camera sceneCamera, float fromRotation, float targetRotation, Vector2 innerPos, Vector2 outerPos, Func<bool> canApplyState, float jumpHeightMultiplier = 1f, double despawnDelay = 0)
        : this(logicalNote, jumpers, animationStates, jumper, fromPos, toPos, crotchet, seeSawBeam, sceneCamera, fromRotation, targetRotation, innerPos, outerPos, null, Vector2.Zero, Vector2.Zero, 0, Vector2.Zero, Vector2.Zero, DefaultCounterRotationProgression, DefaultJumperStartProgression, canApplyState, despawnDelay, null, jumpHeightMultiplier)
    {
    }

    public SeeSawVisualNote(Note logicalNote, Dictionary<SeeSawJumper, GameObject> jumpers, Dictionary<SeeSawJumper, AnimationStateMachine> animationStates, SeeSawJumper jumper, Vector2 fromPos, Vector2 toPos, double crotchet, GameObject seeSawBeam, float fromRotation, float targetRotation, Vector2 innerPos, Vector2 outerPos, Func<bool> canApplyState, float jumpHeightMultiplier = 1f, double despawnDelay = 0)
        : this(logicalNote, jumpers, animationStates, jumper, fromPos, toPos, crotchet, seeSawBeam, null, fromRotation, targetRotation, innerPos, outerPos, canApplyState, jumpHeightMultiplier, despawnDelay)
    {
    }

    /// <summary>
    /// Creates a See-Saw visual note with an optional counter jumper from raw position values.
    /// </summary>
    /// <param name="logicalNote">Logical rhythm note represented by this visual.</param>
    /// <param name="jumpers">Scene-owned jumper GameObjects indexed by jumper identity.</param>
    /// <param name="animationStates">Scene-owned animation state machines indexed by jumper identity.</param>
    /// <param name="jumper">The actor that performs the main jump and lands on the note hit time.</param>
    /// <param name="fromPos">Stable position where the main jumper starts.</param>
    /// <param name="toPos">Stable position where the main jumper lands.</param>
    /// <param name="crotchet">Beat duration in seconds at the current BPM.</param>
    /// <param name="seeSawBeam">Scene-owned beam object whose rotation is animated.</param>
    /// <param name="sceneCamera">Scene camera available to camera-aware visual effects.</param>
    /// <param name="fromRotation">Beam rotation before the visual applies state.</param>
    /// <param name="targetRotation">Beam rotation after the main jumper lands.</param>
    /// <param name="innerPos">Main jumper inner-side reference position.</param>
    /// <param name="outerPos">Main jumper outer-side reference position.</param>
    /// <param name="counterJumper">Optional actor that performs the first-half counter jump.</param>
    /// <param name="counterFromPos">Stable position where the counter jumper starts.</param>
    /// <param name="counterToPos">Stable position where the counter jumper lands.</param>
    /// <param name="counterTargetRotation">Beam rotation after the counter jumper lands.</param>
    /// <param name="counterInnerPos">Counter jumper inner-side reference position.</param>
    /// <param name="counterOuterPos">Counter jumper outer-side reference position.</param>
    /// <param name="counterRotationProgression">Normalized note progress where the counter beam rotation applies.</param>
    /// <param name="jumperStartProgression">Normalized note progress where the main jumper starts moving.</param>
    /// <param name="canApplyState">Predicate that decides whether this visual may mutate shared scene objects this frame.</param>
    /// <param name="despawnDelay">Extra lifetime after the note ends.</param>
    /// <param name="approachDuration">Optional approach duration override in seconds.</param>
    /// <param name="jumpHeightMultiplier">Multiplier applied to both derived vertical jump heights.</param>
    public SeeSawVisualNote(Note logicalNote, Dictionary<SeeSawJumper, GameObject> jumpers, Dictionary<SeeSawJumper, AnimationStateMachine> animationStates, SeeSawJumper jumper, Vector2 fromPos, Vector2 toPos, double crotchet, GameObject seeSawBeam, Camera sceneCamera, float fromRotation, float targetRotation, Vector2 innerPos, Vector2 outerPos, SeeSawJumper? counterJumper, Vector2 counterFromPos, Vector2 counterToPos, float counterTargetRotation, Vector2 counterInnerPos, Vector2 counterOuterPos, float counterRotationProgression = DefaultCounterRotationProgression, float jumperStartProgression = DefaultJumperStartProgression, Func<bool> canApplyState = null, double despawnDelay = 0, double? approachDuration = null, float jumpHeightMultiplier = 1f, bool isBigLeap = false) 
        : this(
            logicalNote,
            jumpers,
            animationStates,
            jumper,
            new SeeSawJumpPath(fromPos, toPos, innerPos, outerPos, jumpHeightMultiplier),
            crotchet,
            seeSawBeam,
            sceneCamera,
            fromRotation,
            targetRotation,
            counterJumper.HasValue
                ? new SeeSawCounterJump(counterJumper.Value, new SeeSawJumpPath(counterFromPos, counterToPos, counterInnerPos, counterOuterPos, jumpHeightMultiplier), counterTargetRotation)
                : null,
            counterRotationProgression,
            jumperStartProgression,
            canApplyState,
            despawnDelay,
            approachDuration,
            isBigLeap
            )
    {
    }

    public SeeSawVisualNote(Note logicalNote, Dictionary<SeeSawJumper, GameObject> jumpers, Dictionary<SeeSawJumper, AnimationStateMachine> animationStates, SeeSawJumper jumper, Vector2 fromPos, Vector2 toPos, double crotchet, GameObject seeSawBeam, float fromRotation, float targetRotation, Vector2 innerPos, Vector2 outerPos, SeeSawJumper? counterJumper, Vector2 counterFromPos, Vector2 counterToPos, float counterTargetRotation, Vector2 counterInnerPos, Vector2 counterOuterPos, float counterRotationProgression = DefaultCounterRotationProgression, float jumperStartProgression = DefaultJumperStartProgression, Func<bool> canApplyState = null, double despawnDelay = 0, double? approachDuration = null, float jumpHeightMultiplier = 1f)
        : this(logicalNote, jumpers, animationStates, jumper, fromPos, toPos, crotchet, seeSawBeam, null, fromRotation, targetRotation, innerPos, outerPos, counterJumper, counterFromPos, counterToPos, counterTargetRotation, counterInnerPos, counterOuterPos, counterRotationProgression, jumperStartProgression, canApplyState, despawnDelay, approachDuration, jumpHeightMultiplier)
    {
    }

    /// <summary>
    /// Creates a See-Saw visual note from explicit jump path objects.
    /// </summary>
    /// <param name="logicalNote">Logical rhythm note represented by this visual.</param>
    /// <param name="jumpers">Scene-owned jumper GameObjects indexed by jumper identity.</param>
    /// <param name="animationStates">Scene-owned animation state machines indexed by jumper identity.</param>
    /// <param name="jumper">The actor that performs the main jump and lands on the note hit time.</param>
    /// <param name="jumperPath">Main jumper path.</param>
    /// <param name="crotchet">Beat duration in seconds at the current BPM.</param>
    /// <param name="seeSawBeam">Scene-owned beam object whose rotation is animated.</param>
    /// <param name="sceneCamera">Scene camera available to camera-aware visual effects.</param>
    /// <param name="fromRotation">Beam rotation before the visual applies state.</param>
    /// <param name="targetRotation">Beam rotation after the main jumper lands.</param>
    /// <param name="counterJump">Optional first-half counter jump.</param>
    /// <param name="counterRotationProgression">Normalized note progress where the counter beam rotation applies.</param>
    /// <param name="jumperStartProgression">Normalized note progress where the main jumper starts moving.</param>
    /// <param name="canApplyState">Predicate that decides whether this visual may mutate shared scene objects this frame.</param>
    /// <param name="despawnDelay">Extra lifetime after the note ends.</param>
    /// <param name="approachDuration">Optional approach duration override in seconds.</param>
    public SeeSawVisualNote(Note logicalNote, Dictionary<SeeSawJumper, GameObject> jumpers, Dictionary<SeeSawJumper, AnimationStateMachine> animationStates, SeeSawJumper jumper, SeeSawJumpPath jumperPath, double crotchet, GameObject seeSawBeam, Camera sceneCamera, float fromRotation, float targetRotation, SeeSawCounterJump? counterJump = null, float counterRotationProgression = DefaultCounterRotationProgression, float jumperStartProgression = DefaultJumperStartProgression, Func<bool> canApplyState = null, double despawnDelay = 0, double? approachDuration = null, bool isBigLeap = false, float jumpMultiplier = 1, float counterJumpEndProgression = CounterJumpEndProgression, bool counterIsBigLeap = false)
        : base(logicalNote, approachDuration ?? jumperPath.GetApproachDuration(crotchet), despawnDelay)
    {
        _jumpers = jumpers;
        _animationStates = animationStates;
        _jumper = jumper;
        _jumperPath = jumpMultiplier > 1f
            ? SeeSawJumpPath.WithJumpHeight(jumperPath.From, jumperPath.To, jumperPath.InnerReference, jumperPath.OuterReference, BigLeapJumpHeight)
            : jumperPath;
        _seeSawBeam = seeSawBeam;
        _sceneCamera = sceneCamera;
        _fromRotation = fromRotation;
        _targetRotation = targetRotation;
        _counterJump = counterJump;
        _counterRotationProgression = counterRotationProgression;
        _jumperStartProgression = jumperStartProgression;
        _counterJumpEndProgression = counterJumpEndProgression;
        _counterIsBigLeap = counterIsBigLeap;
        _canApplyState = canApplyState;
        _isBigLeap = isBigLeap;
    }

    public SeeSawVisualNote(Note logicalNote, Dictionary<SeeSawJumper, GameObject> jumpers, Dictionary<SeeSawJumper, AnimationStateMachine> animationStates, SeeSawJumper jumper, SeeSawJumpPath jumperPath, double crotchet, GameObject seeSawBeam, float fromRotation, float targetRotation, SeeSawCounterJump? counterJump = null, float counterRotationProgression = DefaultCounterRotationProgression, float jumperStartProgression = DefaultJumperStartProgression, Func<bool> canApplyState = null, double despawnDelay = 0, double? approachDuration = null, bool isBigLeap = false)
        : this(logicalNote, jumpers, animationStates, jumper, jumperPath, crotchet, seeSawBeam, null, fromRotation, targetRotation, counterJump, counterRotationProgression, jumperStartProgression, canApplyState, despawnDelay, approachDuration, isBigLeap)
    {
    }

    /// <summary>
    /// Scene camera passed by the owning scene for camera-aware visual effects.
    /// </summary>
    public Camera SceneCamera => _sceneCamera;

    /// <summary>
    /// Updates actor positions, beam rotation, and animation states for the given song time.
    /// </summary>
    /// <param name="currentSongPosition">Current song position in seconds.</param>
    public override void Update(double currentSongPosition)
    {
        UpdateState(currentSongPosition);

        // Rewinding can put the same visual note before a jump/land trigger that already
        // fired. Reset the one-shot flags so animations can be replayed correctly.
        if (!double.IsNaN(_lastSongPosition) && currentSongPosition < _lastSongPosition - 0.001)
            ResetAnimationTriggers();

        _lastSongPosition = currentSongPosition;

        // Several visual notes can be alive at once. The scene decides which one owns
        // the shared actors for this frame so overlapping notes do not fight each other.
        if (!CanApplyState())
            return;

        double startTime = Note.SongPosition - ApproachDuration;
        double endTime = Note.SongPosition;

        if (currentSongPosition < startTime)
        {
            // If the visual manager updates this note before its approach window, make sure
            // no stale one-shot animation state leaks from a previous seek position.
            ResetAnimationTriggers();
            return; 
        }

        float progression = (float)((currentSongPosition - startTime) / (endTime - startTime));

        if (currentSongPosition >= endTime)
        {
            ApplyCompletedState();
            return;
        }

        ApplyCounterJump(progression);
        ApplyMainJump(progression);
        ApplyCameraJump(progression);
        ApplyBeamRotation(progression);
    }

    


    /// <summary>
    /// Checks whether this visual note currently owns the shared scene objects.
    /// </summary>
    /// <returns><c>true</c> if this visual may mutate actors and beam state; otherwise <c>false</c>.</returns>
    private bool CanApplyState()
    {
        return _canApplyState == null || _canApplyState();
    }

    /// <summary>
    /// Snaps all controlled actors and the beam to the exact final hit-time state.
    /// </summary>
    private void ApplyCompletedState()
    {
        // Completion is snapped instead of interpolated. This avoids tiny floating point
        // offsets at the exact hit time, which matters because the scene rebuilds its base
        // state from note targets every frame.
        if (_counterJump.HasValue)
        {
            SeeSawCounterJump counterJump = _counterJump.Value;
            Land(counterJump.Jumper);
            GetJumper(counterJump.Jumper).Position = counterJump.Path.To;
        }

        Land(_jumper);
        GetJumper(_jumper).Position = _jumperPath.To;
        _seeSawBeam.Rotation = _targetRotation;
    }

    

    /// <summary>
    /// Applies the optional first-half counter jump.
    /// </summary>
    /// <param name="noteProgression">Normalized progress from approach start to hit time.</param>
    private void ApplyCounterJump(float noteProgression)
    {
        // Applejack's counter movement happens before Rainbow's movement for normal notes.
        // For opposite notes this can be a stationary path, but it still triggers the same
        // timing and beam handoff behavior.
        if (!_counterJump.HasValue)
            return;

        SeeSawCounterJump counterJump = _counterJump.Value;

        if (noteProgression < _counterJumpEndProgression)
        {
            StartJump(counterJump.Jumper);
            float jumpProgression = noteProgression / _counterJumpEndProgression;
            if (!_counterIsBigLeap)
                ApplyJumpAnimation(counterJump.Jumper, jumpProgression);

            ApplyJump(GetJumper(counterJump.Jumper), counterJump.Path, jumpProgression);
            return;
        }

        Land(counterJump.Jumper);
        GetJumper(counterJump.Jumper).Position = counterJump.Path.To;
    }

    private void ApplyCameraJump(float progression)
    {
        // Only if it's a big leap, the camera must jump
        if(!_isBigLeap) return;

        if(progression < _jumperStartProgression)
        {
            _sceneCamera.Position = Vector2.Zero;
            return;
        }

        progression = (progression - _jumperStartProgression) / (1f - _jumperStartProgression);

        if(progression < 1f)
        {
            float camT = (float)Math.Sin(progression * Math.PI);

            _sceneCamera.Position = new Vector2(0, Single.Lerp(0, GetBigLeapCameraTargetY(), camT));
        }
        else
        {
            _sceneCamera.Position = Vector2.Zero;
        }
    }

    private float GetBigLeapCameraTargetY()
    {
        return GetJumper(_jumper).Position.Y - BigLeapCameraVerticalMargin;
    }

    /// <summary>
    /// Applies the main jumper arc once its configured handoff point has been reached.
    /// </summary>
    /// <param name="noteProgression">Normalized progress from approach start to hit time.</param>
    private void ApplyMainJump(float noteProgression)
    {
        // Keep the main jumper pinned to its previous stable position until its configured
        // handoff point. This preserves the original choreography of Applejack landing first.
        if (noteProgression < _jumperStartProgression)
        {
            GetJumper(_jumper).Position = _jumperPath.From;
            return;
        }

        if(!_isBigLeap)
        {
            float jumpProgression = (noteProgression - _jumperStartProgression) / (1f - _jumperStartProgression);
            if (jumpProgression >= LandTriggerProgression)
            {
                Land(_jumper);
                GetJumper(_jumper).Position = _jumperPath.To;
                return;
            }

            StartJump(_jumper);
            ApplyJumpAnimation(_jumper, jumpProgression);
            ApplyJump(GetJumper(_jumper), _jumperPath, jumpProgression);
        } else
        {
            float bigLeapProgression = (noteProgression - _jumperStartProgression) / (1f - _jumperStartProgression);
            if (bigLeapProgression >= LandTriggerProgression)
            {
                Land(_jumper);
                GetJumper(_jumper).Position = _jumperPath.To;
                return;
            }

            // 4. On lance les actions (écrit une seule fois, c'est plus propre !)
            ApplyJump(GetJumper(_jumper), _jumperPath, bigLeapProgression);
            StartJump(_jumper);
        }
    }

    private static float EaseOutCubic(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        float inverse = 1f - t;
        return 1f - inverse * inverse * inverse;
    }

    private static float SmoothStep(float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    /// <summary>
    /// Applies the in-flight beam rotation before the main jumper lands.
    /// </summary>
    /// <param name="noteProgression">Normalized progress from approach start to hit time.</param>
    private void ApplyBeamRotation(float noteProgression)
    {
        // The beam snaps to the counter lander's side at the handoff, then snaps to the main
        // lander's side in ApplyCompletedState when the note reaches the hit time.
        if (!_counterJump.HasValue)
        {
            _seeSawBeam.Rotation = _fromRotation;
            return;
        }

        _seeSawBeam.Rotation = noteProgression >= _counterRotationProgression
            ? _counterJump.Value.TargetRotation
            : _fromRotation;
    }

    /// <summary>
    /// Clears one-shot jump and land trigger flags so animations can be replayed after a seek.
    /// </summary>
    private void ResetAnimationTriggers()
    {
        _counterJumpStarted = false;
        _counterLanded = false;
        _jumperJumpStarted = false;
        _jumperLanded = false;
    }


    /// <summary>
    /// Gets the scene-owned GameObject for a jumper identity.
    /// </summary>
    /// <param name="jumper">The jumper identity to resolve.</param>
    /// <returns>The matching scene-owned GameObject.</returns>
    private GameObject GetJumper(SeeSawJumper jumper)
    {
        return _jumpers[jumper];
    }

    /// <summary>
    /// Triggers the jump animation for a jumper once per forward pass.
    /// </summary>
    /// <param name="jumper">The jumper whose jump animation should start.</param>
    private void StartJump(SeeSawJumper jumper)
    {
        // Jump and land are event-like animation changes. Position remains deterministic,
        // but the animation state machine should only receive each trigger once per pass.
        if (jumper == _jumper)
        {
            if (_jumperJumpStarted)
                return;

            _jumperJumpStarted = true;
        }
        else
        {
            if (_counterJumpStarted)
                return;

            _counterJumpStarted = true;
        }

        ForceAnimationState(jumper, JumpState);
    }
    

    /// <summary>
    /// Triggers the land animation for a jumper once per forward pass.
    /// </summary>
    /// <param name="jumper">The jumper whose land animation should play.</param>
    private void Land(SeeSawJumper jumper)
    {
        if (jumper == _jumper)
        {
            if (_jumperLanded)
                return;

            _jumperLanded = true;
        }
        else
        {
            if (_counterLanded)
                return;

            _counterLanded = true;
        }

        ForceAnimationState(jumper, LandState);
    }

    /// <summary>
    /// Selects the jump or fall animation state from the current arc progress.
    /// </summary>
    /// <param name="jumper">The jumper being animated.</param>
    /// <param name="jumpProgression">Normalized progress through that jumper's arc.</param>
    private void ApplyJumpAnimation(SeeSawJumper jumper, float jumpProgression)
    {
        ForceAnimationState(jumper, jumpProgression < 0.5f ? JumpState : FallState);
    }

    /// <summary>
    /// Forces a jumper animation state if the corresponding state machine exists.
    /// </summary>
    /// <param name="jumper">The jumper whose animation state should change.</param>
    /// <param name="stateName">Animation state name to force.</param>
    private void ForceAnimationState(SeeSawJumper jumper, string stateName)
    {

        if (_animationStates == null || !_animationStates.TryGetValue(jumper, out AnimationStateMachine stateMachine))
            return;

        if (stateMachine.CurrentState?.Name == stateName && stateName != LandState)
            return;

        // Re-entering the land state is needed to restart Applejack's landing animation when
        // two notes land in close succession. Force through jump first because ForceState on
        // the current state would otherwise be ignored by some state machine implementations.
        if (stateMachine.CurrentState?.Name == stateName)
            stateMachine.ForceState(JumpState);

        stateMachine.ForceState(stateName);
    }

    /// <summary>
    /// Computes the approach duration from a starting position and side references.
    /// </summary>
    /// <param name="crotchet">Beat duration in seconds at the current BPM.</param>
    /// <param name="fromPos">Stable starting position.</param>
    /// <param name="innerPos">Reference inner-side position.</param>
    /// <param name="outerPos">Reference outer-side position.</param>
    /// <returns>Two beats when starting from the inner side; three beats when starting from the outer side.</returns>
    public static double GetApproachDuration(double crotchet, Vector2 fromPos, Vector2 innerPos, Vector2 outerPos)
    {
        return new SeeSawJumpPath(fromPos, fromPos, innerPos, outerPos).GetApproachDuration(crotchet);
    }

    /// <summary>
    /// Applies a sine-arc jump position to a GameObject.
    /// </summary>
    /// <param name="jumper">GameObject to move.</param>
    /// <param name="path">Path containing start, end, and arc height.</param>
    /// <param name="progression">Normalized progress through the jump arc.</param>
    private static void ApplyJump(GameObject jumper, SeeSawJumpPath path, float progression)
    {
        // Horizontal movement is linear; vertical movement is a sine arc that starts and ends
        // at ground level. The path decides the arc height from the jumper's starting side.
        float heightMultiplier = (float)Math.Sin(progression * Math.PI);

        Vector2 basePos = Vector2.Lerp(path.From, path.To, progression);
        jumper.Position = new Vector2(basePos.X, basePos.Y - (path.JumpHeight * heightMultiplier));
    }

    /// <summary>
    /// Draws this visual note.
    /// </summary>
    /// <param name="spriteBatch">Sprite batch used for drawing.</param>
    /// <remarks>
    /// See-Saw visual notes mutate scene-owned actors directly, so there is no independent
    /// per-note sprite to draw here.
    /// </remarks>
    public override void Draw(SpriteBatch spriteBatch)
    {
    }
}

/// <summary>
/// Describes one deterministic jump arc between two stable See-Saw positions.
/// </summary>
/// <remarks>
/// The inner/outer references are not necessarily the path endpoints. They are used to infer
/// whether the actor starts from the inner or outer side, which controls both arc height and
/// approach duration. This mirrors the editor timing rules.
/// </remarks>
public readonly struct SeeSawJumpPath
{
    private const double InnerApproachBeats = 2.0;
    private const double OuterApproachBeats = 4.0;

    /// <summary>
    /// Creates a jump path and derives its arc height from the starting side.
    /// </summary>
    /// <param name="from">Stable starting position.</param>
    /// <param name="to">Stable landing position.</param>
    /// <param name="innerReference">Reference inner-side position for this actor.</param>
    /// <param name="outerReference">Reference outer-side position for this actor.</param>
    /// <param name="jumpHeightMultiplier">Multiplier applied to the derived vertical jump height.</param>
    public SeeSawJumpPath(Vector2 from, Vector2 to, Vector2 innerReference, Vector2 outerReference, float jumpHeightMultiplier = 1f)
    {
        From = from;
        To = to;
        InnerReference = innerReference;
        OuterReference = outerReference;
        JumpHeightMultiplier = jumpHeightMultiplier;
        JumpHeight = GetJumpHeight(from, innerReference, outerReference) * jumpHeightMultiplier;
    }

    private SeeSawJumpPath(Vector2 from, Vector2 to, Vector2 innerReference, Vector2 outerReference, float jumpHeight, bool useExactJumpHeight)
    {
        From = from;
        To = to;
        InnerReference = innerReference;
        OuterReference = outerReference;
        JumpHeightMultiplier = 1f;
        JumpHeight = jumpHeight;
    }

    public static SeeSawJumpPath WithJumpHeight(Vector2 from, Vector2 to, Vector2 innerReference, Vector2 outerReference, float jumpHeight)
    {
        return new SeeSawJumpPath(from, to, innerReference, outerReference, jumpHeight, useExactJumpHeight: true);
    }

    public Vector2 From { get; }
    public Vector2 To { get; }
    public Vector2 InnerReference { get; }
    public Vector2 OuterReference { get; }
    public float JumpHeightMultiplier { get; }
    public float JumpHeight { get; }

    // A jumper starting from the inner side takes a shorter lead-in than one
    // starting from the outer side. Editor timing uses the same rule.
    /// <summary>
    /// Computes the approach duration for this path from its starting side.
    /// </summary>
    /// <param name="crotchet">Beat duration in seconds at the current BPM.</param>
    /// <returns>Two beats when starting near inner; four beats when starting near outer.</returns>
    public double GetApproachDuration(double crotchet)
    {
        return crotchet * (StartsNearInner ? InnerApproachBeats : OuterApproachBeats);
    }

    private bool StartsNearInner => Vector2.Distance(From, InnerReference) < Vector2.Distance(From, OuterReference);

    /// <summary>
    /// Selects the vertical arc height from the starting side.
    /// </summary>
    /// <param name="fromPos">Stable starting position.</param>
    /// <param name="innerPos">Reference inner-side position.</param>
    /// <param name="outerPos">Reference outer-side position.</param>
    /// <returns>Inner or outer jump height in pixels.</returns>
    private static float GetJumpHeight(Vector2 fromPos, Vector2 innerPos, Vector2 outerPos)
    {
        float distToInner = Vector2.Distance(fromPos, innerPos);
        float distToOuter = Vector2.Distance(fromPos, outerPos);
        return (distToInner < distToOuter) ? SeeSawVisualNote.InnerJumpHeight : SeeSawVisualNote.OuterJumpHeight;
    }
}

/// <summary>
/// Optional first-half jump performed by the other actor before the main jumper lands.
/// </summary>
public readonly struct SeeSawCounterJump
{
    /// <summary>
    /// Creates a counter jump configuration.
    /// </summary>
    /// <param name="jumper">Actor that performs the counter jump.</param>
    /// <param name="path">Path followed by the counter jumper.</param>
    /// <param name="targetRotation">Beam rotation after this counter jumper lands.</param>
    public SeeSawCounterJump(SeeSawJumper jumper, SeeSawJumpPath path, float targetRotation)
    {
        Jumper = jumper;
        Path = path;
        TargetRotation = targetRotation;
    }

    public SeeSawJumper Jumper { get; }
    public SeeSawJumpPath Path { get; }
    public float TargetRotation { get; }
}

public enum SeeSawJumper
{
    APPLEJACK,
    RAINBOW_DASH
}
