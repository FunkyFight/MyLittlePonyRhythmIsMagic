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
/// Joue l'animation visuelle d'une note See-Saw sur les acteurs possedes par la scene.
/// </summary>
/// <remarks>
/// Cette classe ne cree pas et ne possede pas les GameObjects. La scene See-Saw possede
/// Rainbow Dash, Applejack et la poutre; chaque note visuelle ne les modifie que lorsque la
/// scene designe cette note comme conductrice. Ce guard est essentiel car le VisualNoteManager
/// peut garder plusieurs notes actives en meme temps pour gerer le look-ahead et le look-behind.
///
/// Le timing est deterministe et depend uniquement de la position musicale, jamais du delta time
/// de la frame. Cela rend le scrubbing editeur et les rewinds stables: appeler Update avec la
/// meme position musicale doit toujours replacer les acteurs au meme endroit.
/// </remarks>
public class SeeSawVisualNote : VisualNote
{
    private const string JumpState = "jump";
    private const string FallState = "fall";
    private const string LandState = "land";
    private const string FailState = "fail";

    // Le contre-sauteur joue la premiere partie de la note visuelle. Le sauteur principal
    // commence ensuite a _jumperStartProgression et atterrit exactement a Note.SongPosition.
    private const float CounterJumpEndProgression = 0.5f;

    private readonly Dictionary<SeeSawJumper, GameObject> _jumpers;
    private readonly Dictionary<SeeSawJumper, AnimationStateMachine> _animationStates;
    private readonly SeeSawJumper _jumper;
    private readonly SeeSawJumpPath _jumperPath;
    private readonly SeeSawCounterJump? _counterJump;
    private readonly SeeSawSimultaneousJump? _simultaneousJump;
    private readonly GameObject _seeSawBeam;
    private readonly Camera _sceneCamera;
    private readonly float _fromRotation;
    private readonly float _targetRotation;
    private readonly float _counterRotationProgression;
    private readonly float _jumperStartProgression;
    private readonly float _mainJumpStartProgression;
    private readonly float _counterJumpEndProgression;
    private readonly bool _counterIsBigLeap;
    private readonly bool _applyBeamRotation;
    private readonly Func<bool> _canApplyState;
    private readonly Func<bool> _shouldPreserveRainbowFeedbackAnimation;
    private bool _isBigLeap;
    private bool _counterJumpStarted;
    private bool _counterLanded;
    private bool _jumperJumpStarted;
    private bool _jumperLanded;
    private bool _rainbowSnapLandRequested;
    private bool _rainbowFailRequested;
    private bool _rainbowFeedbackStateApplied;
    private double _lastSongPosition = double.NaN;

    internal const float OuterJumpHeight = 450f;
    internal const float InnerJumpHeight = 180f;
    internal const float ExitJumpHeight = 560f;
    internal const float BigLeapJumpHeight = OuterJumpHeight * 4f;
    private const float DefaultCounterRotationProgression = 0.5f;
    private const float DefaultJumperStartProgression = 0.5f;
    private const float BigLeapCameraVerticalMargin = 160f;
    private const float LandTriggerProgression = 0.97f;
    private const float BeamRotationEaseProgression = 0.08f;
    internal const float CounterLandTriggerProgression = 0.47f;
    private const double MissWindowSeconds = 0.25;

    internal static float GetCounterLandProgression(float counterJumpEndProgression)
    {
        return counterJumpEndProgression * (CounterLandTriggerProgression / CounterJumpEndProgression);
    }




    /// <summary>
    /// Cree une note visuelle See-Saw a un seul sauteur depuis des positions brutes.
    /// </summary>
    /// <param name="logicalNote">Note rythmique logique representee par ce visuel.</param>
    /// <param name="jumpers">GameObjects des sauteurs possedes par la scene, indexes par identite.</param>
    /// <param name="animationStates">Machines d'animation possedees par la scene, indexees par identite.</param>
    /// <param name="jumper">Acteur qui effectue le saut principal et atterrit au temps de hit.</param>
    /// <param name="fromPos">Position stable de depart du sauteur principal.</param>
    /// <param name="toPos">Position stable d'atterrissage du sauteur principal.</param>
    /// <param name="crotchet">Duree d'un beat en secondes au BPM courant.</param>
    /// <param name="seeSawBeam">Poutre possedee par la scene dont la rotation est animee.</param>
    /// <param name="sceneCamera">Camera de scene disponible pour les effets visuels qui en dependent.</param>
    /// <param name="fromRotation">Rotation de la poutre avant application du visuel.</param>
    /// <param name="targetRotation">Rotation de la poutre apres l'atterrissage du sauteur principal.</param>
    /// <param name="innerPos">Reference du cote interieur pour calculer timing et hauteur de saut.</param>
    /// <param name="outerPos">Reference du cote exterieur pour calculer timing et hauteur de saut.</param>
    /// <param name="jumpHeightMultiplier">Multiplicateur applique a la hauteur verticale derivee.</param>
    /// <param name="despawnDelay">Duree de vie supplementaire apres la fin de la note.</param>
    public SeeSawVisualNote(Note logicalNote, Dictionary<SeeSawJumper, GameObject> jumpers, Dictionary<SeeSawJumper, AnimationStateMachine> animationStates, SeeSawJumper jumper, Vector2 fromPos, Vector2 toPos, double crotchet, GameObject seeSawBeam, Camera sceneCamera, float fromRotation, float targetRotation, Vector2 innerPos, Vector2 outerPos, float jumpHeightMultiplier = 1f, double despawnDelay = 0)
        : this(logicalNote, jumpers, animationStates, jumper, fromPos, toPos, crotchet, seeSawBeam, sceneCamera, fromRotation, targetRotation, innerPos, outerPos, null, Vector2.Zero, Vector2.Zero, 0, Vector2.Zero, Vector2.Zero, DefaultCounterRotationProgression, DefaultJumperStartProgression, null, despawnDelay, null, jumpHeightMultiplier)
    {
    }

    public SeeSawVisualNote(Note logicalNote, Dictionary<SeeSawJumper, GameObject> jumpers, Dictionary<SeeSawJumper, AnimationStateMachine> animationStates, SeeSawJumper jumper, Vector2 fromPos, Vector2 toPos, double crotchet, GameObject seeSawBeam, float fromRotation, float targetRotation, Vector2 innerPos, Vector2 outerPos, float jumpHeightMultiplier = 1f, double despawnDelay = 0)
        : this(logicalNote, jumpers, animationStates, jumper, fromPos, toPos, crotchet, seeSawBeam, null, fromRotation, targetRotation, innerPos, outerPos, jumpHeightMultiplier, despawnDelay)
    {
    }

    /// <summary>
    /// Cree une note visuelle See-Saw a un seul sauteur avec guard d'ownership.
    /// </summary>
    /// <param name="logicalNote">Note rythmique logique representee par ce visuel.</param>
    /// <param name="jumpers">GameObjects des sauteurs possedes par la scene, indexes par identite.</param>
    /// <param name="animationStates">Machines d'animation possedees par la scene, indexees par identite.</param>
    /// <param name="jumper">Acteur qui effectue le saut principal et atterrit au temps de hit.</param>
    /// <param name="fromPos">Position stable de depart du sauteur principal.</param>
    /// <param name="toPos">Position stable d'atterrissage du sauteur principal.</param>
    /// <param name="crotchet">Duree d'un beat en secondes au BPM courant.</param>
    /// <param name="seeSawBeam">Poutre possedee par la scene dont la rotation est animee.</param>
    /// <param name="sceneCamera">Camera de scene disponible pour les effets visuels qui en dependent.</param>
    /// <param name="fromRotation">Rotation de la poutre avant application du visuel.</param>
    /// <param name="targetRotation">Rotation de la poutre apres l'atterrissage du sauteur principal.</param>
    /// <param name="innerPos">Reference du cote interieur pour calculer timing et hauteur de saut.</param>
    /// <param name="outerPos">Reference du cote exterieur pour calculer timing et hauteur de saut.</param>
    /// <param name="canApplyState">Predicate indiquant si ce visuel peut muter les objets partages cette frame.</param>
    /// <param name="jumpHeightMultiplier">Multiplicateur applique a la hauteur verticale derivee.</param>
    /// <param name="despawnDelay">Duree de vie supplementaire apres la fin de la note.</param>
    public SeeSawVisualNote(Note logicalNote, Dictionary<SeeSawJumper, GameObject> jumpers, Dictionary<SeeSawJumper, AnimationStateMachine> animationStates, SeeSawJumper jumper, Vector2 fromPos, Vector2 toPos, double crotchet, GameObject seeSawBeam, Camera sceneCamera, float fromRotation, float targetRotation, Vector2 innerPos, Vector2 outerPos, Func<bool> canApplyState, float jumpHeightMultiplier = 1f, double despawnDelay = 0)
        : this(logicalNote, jumpers, animationStates, jumper, fromPos, toPos, crotchet, seeSawBeam, sceneCamera, fromRotation, targetRotation, innerPos, outerPos, null, Vector2.Zero, Vector2.Zero, 0, Vector2.Zero, Vector2.Zero, DefaultCounterRotationProgression, DefaultJumperStartProgression, canApplyState, despawnDelay, null, jumpHeightMultiplier)
    {
    }

    public SeeSawVisualNote(Note logicalNote, Dictionary<SeeSawJumper, GameObject> jumpers, Dictionary<SeeSawJumper, AnimationStateMachine> animationStates, SeeSawJumper jumper, Vector2 fromPos, Vector2 toPos, double crotchet, GameObject seeSawBeam, float fromRotation, float targetRotation, Vector2 innerPos, Vector2 outerPos, Func<bool> canApplyState, float jumpHeightMultiplier = 1f, double despawnDelay = 0)
        : this(logicalNote, jumpers, animationStates, jumper, fromPos, toPos, crotchet, seeSawBeam, null, fromRotation, targetRotation, innerPos, outerPos, canApplyState, jumpHeightMultiplier, despawnDelay)
    {
    }

    /// <summary>
    /// Cree une note visuelle See-Saw avec un contre-sauteur optionnel depuis des positions brutes.
    /// </summary>
    /// <param name="logicalNote">Note rythmique logique representee par ce visuel.</param>
    /// <param name="jumpers">GameObjects des sauteurs possedes par la scene, indexes par identite.</param>
    /// <param name="animationStates">Machines d'animation possedees par la scene, indexees par identite.</param>
    /// <param name="jumper">Acteur qui effectue le saut principal et atterrit au temps de hit.</param>
    /// <param name="fromPos">Position stable de depart du sauteur principal.</param>
    /// <param name="toPos">Position stable d'atterrissage du sauteur principal.</param>
    /// <param name="crotchet">Duree d'un beat en secondes au BPM courant.</param>
    /// <param name="seeSawBeam">Poutre possedee par la scene dont la rotation est animee.</param>
    /// <param name="sceneCamera">Camera de scene disponible pour les effets visuels qui en dependent.</param>
    /// <param name="fromRotation">Rotation de la poutre avant application du visuel.</param>
    /// <param name="targetRotation">Rotation de la poutre apres l'atterrissage du sauteur principal.</param>
    /// <param name="innerPos">Reference cote interieur du sauteur principal.</param>
    /// <param name="outerPos">Reference cote exterieur du sauteur principal.</param>
    /// <param name="counterJumper">Acteur optionnel qui joue le contre-saut de premiere moitie.</param>
    /// <param name="counterFromPos">Position stable de depart du contre-sauteur.</param>
    /// <param name="counterToPos">Position stable d'atterrissage du contre-sauteur.</param>
    /// <param name="counterTargetRotation">Rotation de la poutre apres l'atterrissage du contre-sauteur.</param>
    /// <param name="counterInnerPos">Reference cote interieur du contre-sauteur.</param>
    /// <param name="counterOuterPos">Reference cote exterieur du contre-sauteur.</param>
    /// <param name="counterRotationProgression">Progression normalisee ou la rotation de poutre du contre-sauteur s'applique.</param>
    /// <param name="jumperStartProgression">Progression normalisee ou le sauteur principal commence a bouger.</param>
    /// <param name="canApplyState">Predicate indiquant si ce visuel peut muter les objets partages cette frame.</param>
    /// <param name="despawnDelay">Duree de vie supplementaire apres la fin de la note.</param>
    /// <param name="approachDuration">Override optionnel de la duree d'approche, en secondes.</param>
    /// <param name="jumpHeightMultiplier">Multiplicateur applique aux hauteurs verticales derivees.</param>
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
    /// Cree une note visuelle See-Saw depuis des objets de chemin de saut explicites.
    /// </summary>
    /// <param name="logicalNote">Note rythmique logique representee par ce visuel.</param>
    /// <param name="jumpers">GameObjects des sauteurs possedes par la scene, indexes par identite.</param>
    /// <param name="animationStates">Machines d'animation possedees par la scene, indexees par identite.</param>
    /// <param name="jumper">Acteur qui effectue le saut principal et atterrit au temps de hit.</param>
    /// <param name="jumperPath">Chemin du sauteur principal.</param>
    /// <param name="crotchet">Duree d'un beat en secondes au BPM courant.</param>
    /// <param name="seeSawBeam">Poutre possedee par la scene dont la rotation est animee.</param>
    /// <param name="sceneCamera">Camera de scene disponible pour les effets visuels qui en dependent.</param>
    /// <param name="fromRotation">Rotation de la poutre avant application du visuel.</param>
    /// <param name="targetRotation">Rotation de la poutre apres l'atterrissage du sauteur principal.</param>
    /// <param name="counterJump">Contre-saut optionnel de premiere moitie.</param>
    /// <param name="counterRotationProgression">Progression normalisee ou la rotation de poutre du contre-sauteur s'applique.</param>
    /// <param name="jumperStartProgression">Progression normalisee ou le sauteur principal commence a bouger.</param>
    /// <param name="canApplyState">Predicate indiquant si ce visuel peut muter les objets partages cette frame.</param>
    /// <param name="despawnDelay">Duree de vie supplementaire apres la fin de la note.</param>
    /// <param name="approachDuration">Override optionnel de la duree d'approche, en secondes.</param>
    public SeeSawVisualNote(Note logicalNote, Dictionary<SeeSawJumper, GameObject> jumpers, Dictionary<SeeSawJumper, AnimationStateMachine> animationStates, SeeSawJumper jumper, SeeSawJumpPath jumperPath, double crotchet, GameObject seeSawBeam, Camera sceneCamera, float fromRotation, float targetRotation, SeeSawCounterJump? counterJump = null, float counterRotationProgression = DefaultCounterRotationProgression, float jumperStartProgression = DefaultJumperStartProgression, Func<bool> canApplyState = null, double despawnDelay = 0, double? approachDuration = null, bool isBigLeap = false, float jumpMultiplier = 1, float counterJumpEndProgression = CounterJumpEndProgression, bool counterIsBigLeap = false, SeeSawSimultaneousJump? simultaneousJump = null, float? mainJumpStartProgression = null, Func<bool> shouldPreserveRainbowFeedbackAnimation = null)
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
        _simultaneousJump = simultaneousJump;
        _counterRotationProgression = counterRotationProgression;
        _jumperStartProgression = jumperStartProgression;
        _mainJumpStartProgression = mainJumpStartProgression ?? jumperStartProgression;
        _counterJumpEndProgression = counterJumpEndProgression;
        _counterIsBigLeap = counterIsBigLeap;
        _applyBeamRotation = seeSawBeam != null;
        _canApplyState = canApplyState;
        _shouldPreserveRainbowFeedbackAnimation = shouldPreserveRainbowFeedbackAnimation;
        _isBigLeap = isBigLeap;
    }

    public SeeSawVisualNote(Note logicalNote, Dictionary<SeeSawJumper, GameObject> jumpers, Dictionary<SeeSawJumper, AnimationStateMachine> animationStates, SeeSawJumper jumper, SeeSawJumpPath jumperPath, double crotchet, GameObject seeSawBeam, float fromRotation, float targetRotation, SeeSawCounterJump? counterJump = null, float counterRotationProgression = DefaultCounterRotationProgression, float jumperStartProgression = DefaultJumperStartProgression, Func<bool> canApplyState = null, double despawnDelay = 0, double? approachDuration = null, bool isBigLeap = false)
        : this(logicalNote, jumpers, animationStates, jumper, jumperPath, crotchet, seeSawBeam, null, fromRotation, targetRotation, counterJump, counterRotationProgression, jumperStartProgression, canApplyState, despawnDelay, approachDuration, isBigLeap)
    {
    }

    /// <summary>
    /// Camera transmise par la scene proprietaire pour les effets visuels qui en dependent.
    /// </summary>
    public Camera SceneCamera => _sceneCamera;

    public void SnapLandRainbow(bool correct)
    {
        if (correct)
        {
            _rainbowSnapLandRequested = true;
            _rainbowFeedbackStateApplied = false;
            return;
        }

        _rainbowFailRequested = true;
        _rainbowFeedbackStateApplied = false;
    }

    public void ApplyRainbowFeedbackState(bool correct)
    {
        SnapLandRainbow(correct);

        if (_jumper == SeeSawJumper.RAINBOW_DASH)
        {
            ApplyRainbowFeedbackAnimationState(correct);
            return;
        }

        if (!correct)
            RhythmVisualUtils.ForceAnimationState(_animationStates, SeeSawJumper.RAINBOW_DASH, FailState);
    }

    /// <summary>
    /// Met a jour les positions des acteurs, la rotation de poutre et les animations pour un temps musical donne.
    /// </summary>
    /// <param name="currentSongPosition">Position musicale courante, en secondes.</param>
    public override void Update(double currentSongPosition)
    {
        UpdateState(currentSongPosition);

        // Un rewind peut replacer cette meme note avant un trigger jump/land deja joue.
        // Les flags one-shot doivent etre remis a zero pour rejouer correctement les animations.
        if (RhythmVisualUtils.HasRewound(currentSongPosition, _lastSongPosition))
            ResetAnimationTriggers();

        _lastSongPosition = currentSongPosition;

        // Plusieurs notes visuelles peuvent etre vivantes en meme temps. La scene decide laquelle
        // possede les acteurs partages sur cette frame pour eviter des mutations concurrentes.
        if (!RhythmVisualUtils.CanApplyState(_canApplyState))
            return;

        if (RhythmVisualUtils.IsBeforeApproach(currentSongPosition, Note.SongPosition, ApproachDuration))
        {
            // Si le manager met a jour cette note avant sa fenetre d'approche, aucun ancien
            // trigger one-shot issu d'un seek precedent ne doit rester actif.
            ResetAnimationTriggers();
            return; 
        }

        float progression = RhythmVisualUtils.GetApproachProgress(currentSongPosition, Note.SongPosition, ApproachDuration);

        if (RhythmVisualUtils.IsAtOrAfterHit(currentSongPosition, Note.SongPosition))
        {
            if (_jumper == SeeSawJumper.RAINBOW_DASH && !_rainbowSnapLandRequested && !_rainbowFailRequested)
            {
                ApplyRainbowPostHitMissWindow(currentSongPosition);
                return;
            }

            ApplyCompletedState();
            return;
        }

        ApplyCounterJump(progression);
        ApplyMainJump(progression);
        ApplySimultaneousJump(progression);
        ApplyCameraJump(progression);
        ApplyBeamRotation(progression);
    }

    


    /// <summary>
    /// Replace tous les acteurs controles et la poutre dans leur etat final exact au hit.
    /// </summary>
    private void ApplyCompletedState()
    {
        ResetBigLeapCamera();

        // L'etat final est snap plutot qu'interpole. Cela evite de minuscules offsets flottants
        // au temps de hit, important car la scene reconstruit son etat stable chaque frame.
        if (_counterJump.HasValue)
        {
            SeeSawCounterJump counterJump = _counterJump.Value;
            if (!IsStationaryPath(counterJump.Path))
                Land(counterJump.Jumper);

            GetJumper(counterJump.Jumper).Position = counterJump.Path.To;
        }

        if (_simultaneousJump.HasValue)
        {
            SeeSawSimultaneousJump simultaneousJump = _simultaneousJump.Value;
            if (!IsStationaryPath(simultaneousJump.Path))
                Land(simultaneousJump.Jumper);

            GetJumper(simultaneousJump.Jumper).Position = simultaneousJump.Path.To;
        }

        if (_jumper == SeeSawJumper.RAINBOW_DASH)
        {
            if (_rainbowSnapLandRequested)
            {
                ApplyRainbowCompletedFeedbackState(correct: true);
            }
            else if (_rainbowFailRequested)
            {
                ApplyRainbowCompletedFeedbackState(correct: false);
            }
        }
        else if (!IsStationaryPath(_jumperPath))
        {
            Land(_jumper);
            GetJumper(_jumper).Position = _jumperPath.To;
        }

        if (_applyBeamRotation)
            _seeSawBeam.Rotation = _targetRotation;
    }

    private void ApplyRainbowCompletedFeedbackState(bool correct)
    {
        if (!_rainbowFeedbackStateApplied)
            ApplyRainbowFeedbackAnimationState(correct);

        GetJumper(SeeSawJumper.RAINBOW_DASH).Position = _jumperPath.To;

        if (_applyBeamRotation)
            _seeSawBeam.Rotation = _targetRotation;
    }

    private void ApplyRainbowFeedbackAnimationState(bool correct)
    {
        RhythmVisualUtils.ForceAnimationState(_animationStates, SeeSawJumper.RAINBOW_DASH, correct ? LandState : FailState, correct ? JumpState : null);
        _rainbowFeedbackStateApplied = true;
    }

    private void ApplyRainbowPostHitMissWindow(double currentSongPosition)
    {
        ResetBigLeapCamera();

        float unreactedProgression = GetRainbowUnreactedJumpProgression(
            RhythmVisualUtils.GetUnclampedApproachProgress(currentSongPosition, Note.SongPosition, ApproachDuration));

        StartJump(_jumper);
        ApplyJumpAnimation(_jumper, unreactedProgression);
        ApplyJump(GetJumper(_jumper), _jumperPath, unreactedProgression);

        if (_applyBeamRotation)
            _seeSawBeam.Rotation = GetBeamRotationBeforeRainbowLanding();
    }

    private float GetRainbowJumpProgression(float noteProgression)
    {
        return _rainbowSnapLandRequested || _rainbowFailRequested
            ? RhythmVisualUtils.GetPhaseProgress(noteProgression, _mainJumpStartProgression, 1f)
            : GetRainbowUnreactedJumpProgression(noteProgression);
    }

    private float GetRainbowUnreactedJumpProgression(double approachProgression)
    {
        double missProgressionExtension = ApproachDuration <= 0.0 ? 0.0 : MissWindowSeconds / ApproachDuration;
        double phaseEnd = 1.0 + missProgressionExtension;

        if (approachProgression <= _mainJumpStartProgression)
            return 0f;

        if (phaseEnd <= _mainJumpStartProgression)
            return 1f;

        return (float)((approachProgression - _mainJumpStartProgression) / (phaseEnd - _mainJumpStartProgression));
    }

    private float GetBeamRotationBeforeRainbowLanding()
    {
        return _counterJump.HasValue ? _counterJump.Value.TargetRotation : _fromRotation;
    }

    

    /// <summary>
    /// Applique le contre-saut optionnel de premiere moitie.
    /// </summary>
    /// <param name="noteProgression">Progression normalisee entre le debut d'approche et le hit.</param>
    private void ApplyCounterJump(float noteProgression)
    {
        // Le mouvement d'Applejack precede celui de Rainbow pour les notes normales.
        // Pour les notes opposees, le chemin peut etre stationnaire mais conserve le meme timing
        // et le meme comportement de passage de relais de la poutre.
        if (!_counterJump.HasValue)
            return;

        SeeSawCounterJump counterJump = _counterJump.Value;

        if (IsStationaryPath(counterJump.Path))
        {
            GetJumper(counterJump.Jumper).Position = counterJump.Path.To;
            return;
        }

        if (noteProgression < _jumperStartProgression)
        {
            StartJump(counterJump.Jumper);
            float jumpProgression = RhythmVisualUtils.GetPhaseProgress(noteProgression, 0f, _counterJumpEndProgression);

            if (noteProgression >= GetCounterLandProgression(_counterJumpEndProgression))
            {
                Land(counterJump.Jumper);
                GetJumper(counterJump.Jumper).Position = counterJump.Path.To;
                return;
            }

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
        // La camera ne participe qu'aux grands sauts. Certains constructeurs autorisent une camera nulle.
        if(!_isBigLeap || _sceneCamera == null) return;

        if(progression < _jumperStartProgression)
        {
            _sceneCamera.Position = Vector2.Zero;
            return;
        }

        progression = RhythmVisualUtils.GetPhaseProgress(progression, _jumperStartProgression, 1f);

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

    private void ResetBigLeapCamera()
    {
        if (_isBigLeap && _sceneCamera != null)
            _sceneCamera.Position = Vector2.Zero;
    }

    /// <summary>
    /// Applique l'arc du sauteur principal lorsque son point de relais est atteint.
    /// </summary>
    /// <param name="noteProgression">Progression normalisee entre le debut d'approche et le hit.</param>
    private void ApplyMainJump(float noteProgression)
    {
        // Le sauteur principal reste fixe sur sa position stable precedente jusqu'au relais.
        // Cela preserve la choregraphie ou Applejack atterrit avant Rainbow.
        if (noteProgression < _mainJumpStartProgression)
        {
            GetJumper(_jumper).Position = _jumperPath.From;
            return;
        }

        if (IsStationaryPath(_jumperPath))
        {
            GetJumper(_jumper).Position = _jumperPath.To;
            return;
        }

        if(!_isBigLeap)
        {
            float jumpProgression = _jumper == SeeSawJumper.RAINBOW_DASH
                ? GetRainbowJumpProgression(noteProgression)
                : RhythmVisualUtils.GetPhaseProgress(noteProgression, _mainJumpStartProgression, 1f);
            if (_jumper == SeeSawJumper.RAINBOW_DASH)
            {
                StartJump(_jumper);
                ApplyJumpAnimation(_jumper, jumpProgression);
                ApplyJump(GetJumper(_jumper), _jumperPath, jumpProgression);
                return;
            }

            if (_jumper != SeeSawJumper.RAINBOW_DASH && jumpProgression >= LandTriggerProgression)
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
            float bigLeapProgression = _jumper == SeeSawJumper.RAINBOW_DASH
                ? GetRainbowJumpProgression(noteProgression)
                : RhythmVisualUtils.GetPhaseProgress(noteProgression, _mainJumpStartProgression, 1f);
            if (_jumper != SeeSawJumper.RAINBOW_DASH && bigLeapProgression >= LandTriggerProgression)
            {
                Land(_jumper);
                GetJumper(_jumper).Position = _jumperPath.To;
                return;
            }

            ApplyJump(GetJumper(_jumper), _jumperPath, bigLeapProgression);
            StartJump(_jumper);
        }
    }

    /// <summary>
    /// Applique un second saut qui partage exactement la phase du sauteur principal.
    /// </summary>
    /// <param name="noteProgression">Progression normalisee entre le debut d'approche et le hit.</param>
    private void ApplySimultaneousJump(float noteProgression)
    {
        if (!_simultaneousJump.HasValue)
            return;

        SeeSawSimultaneousJump simultaneousJump = _simultaneousJump.Value;

        if (noteProgression < _mainJumpStartProgression)
        {
            GetJumper(simultaneousJump.Jumper).Position = simultaneousJump.Path.From;
            return;
        }

        if (IsStationaryPath(simultaneousJump.Path))
        {
            GetJumper(simultaneousJump.Jumper).Position = simultaneousJump.Path.To;
            return;
        }

        float jumpProgression = RhythmVisualUtils.GetPhaseProgress(noteProgression, _mainJumpStartProgression, 1f);
        if (jumpProgression >= LandTriggerProgression)
        {
            Land(simultaneousJump.Jumper);
            GetJumper(simultaneousJump.Jumper).Position = simultaneousJump.Path.To;
            return;
        }

        StartJump(simultaneousJump.Jumper);

        if (!simultaneousJump.IsBigLeap)
            ApplyJumpAnimation(simultaneousJump.Jumper, jumpProgression);

        ApplyJump(GetJumper(simultaneousJump.Jumper), simultaneousJump.Path, jumpProgression);
    }

    /// <summary>
    /// Applique la rotation de poutre pendant le vol, avant l'atterrissage du sauteur principal.
    /// </summary>
    /// <param name="noteProgression">Progression normalisee entre le debut d'approche et le hit.</param>
    private void ApplyBeamRotation(float noteProgression)
    {
        if (!_applyBeamRotation)
            return;

        if (_jumper == SeeSawJumper.RAINBOW_DASH && !_rainbowSnapLandRequested && !_rainbowFailRequested)
        {
            if (_counterJump.HasValue)
                _seeSawBeam.Rotation = EaseBeamRotation(_fromRotation, _counterJump.Value.TargetRotation, noteProgression, GetCounterLandProgression(_counterJumpEndProgression));
            else
                _seeSawBeam.Rotation = _fromRotation;

            return;
        }

        // La poutre snap en meme temps que les atterrissages anticipes des sauteurs.
        if (!_counterJump.HasValue)
        {
            float jumpProgression = RhythmVisualUtils.GetPhaseProgress(noteProgression, _mainJumpStartProgression, 1f);
            _seeSawBeam.Rotation = EaseBeamRotation(_fromRotation, _targetRotation, jumpProgression, LandTriggerProgression);
            return;
        }

        float counterLandProgression = GetCounterLandProgression(_counterJumpEndProgression);
        if (noteProgression < _jumperStartProgression)
        {
            _seeSawBeam.Rotation = EaseBeamRotation(_fromRotation, _counterJump.Value.TargetRotation, noteProgression, counterLandProgression);
            return;
        }

        if (noteProgression >= _jumperStartProgression)
        {
            float mainJumpProgression = RhythmVisualUtils.GetPhaseProgress(noteProgression, _jumperStartProgression, 1f);
            _seeSawBeam.Rotation = EaseBeamRotation(_counterJump.Value.TargetRotation, _targetRotation, mainJumpProgression, LandTriggerProgression);
            return;
        }

        _seeSawBeam.Rotation = _fromRotation;
    }

    private static float EaseBeamRotation(float fromRotation, float targetRotation, float progression, float targetProgression)
    {
        if (progression >= targetProgression)
            return targetRotation;

        float easeStart = Math.Max(0f, targetProgression - BeamRotationEaseProgression);
        if (progression <= easeStart)
            return fromRotation;

        float t = RhythmVisualUtils.GetPhaseProgress(progression, easeStart, targetProgression);
        t = t * t * (3f - 2f * t);
        return MathHelper.Lerp(fromRotation, targetRotation, t);
    }

    /// <summary>
    /// Reinitialise les flags de triggers jump/land pour rejouer les animations apres un seek.
    /// </summary>
    private void ResetAnimationTriggers()
    {
        _counterJumpStarted = false;
        _counterLanded = false;
        _jumperJumpStarted = false;
        _jumperLanded = false;
        _rainbowFeedbackStateApplied = false;
    }


    /// <summary>
    /// Recupere le GameObject possede par la scene pour une identite de sauteur.
    /// </summary>
    /// <param name="jumper">Identite du sauteur a resoudre.</param>
    /// <returns>GameObject correspondant possede par la scene.</returns>
    private GameObject GetJumper(SeeSawJumper jumper)
    {
        return _jumpers[jumper];
    }

    /// <summary>
    /// Declenche l'animation de saut d'un sauteur une seule fois par passage vers l'avant.
    /// </summary>
    /// <param name="jumper">Sauteur dont l'animation de saut doit demarrer.</param>
    private void StartJump(SeeSawJumper jumper)
    {
        // Jump et land sont des changements d'animation de type evenement. La position reste
        // deterministe, mais la state machine ne doit recevoir chaque trigger qu'une fois par passe.
        if (jumper == _jumper)
        {
            if (!RhythmVisualUtils.TrySetTrigger(ref _jumperJumpStarted))
                return;
        }
        else
        {
            if (!RhythmVisualUtils.TrySetTrigger(ref _counterJumpStarted))
                return;
        }

        RhythmVisualUtils.ForceAnimationState(_animationStates, jumper, JumpState);
    }
    

    /// <summary>
    /// Declenche l'animation d'atterrissage d'un sauteur une seule fois par passage vers l'avant.
    /// </summary>
    /// <param name="jumper">Sauteur dont l'animation d'atterrissage doit etre jouee.</param>
    private void Land(SeeSawJumper jumper)
    {
        if (jumper == _jumper)
        {
            if (!RhythmVisualUtils.TrySetTrigger(ref _jumperLanded))
                return;
        }
        else
        {
            if (!RhythmVisualUtils.TrySetTrigger(ref _counterLanded))
                return;
        }

        RhythmVisualUtils.ForceAnimationState(_animationStates, jumper, LandState, JumpState);
    }

    /// <summary>
    /// Selectionne l'etat d'animation jump ou fall selon la progression courante de l'arc.
    /// </summary>
    /// <param name="jumper">Sauteur anime.</param>
    /// <param name="jumpProgression">Progression normalisee dans l'arc de ce sauteur.</param>
    private void ApplyJumpAnimation(SeeSawJumper jumper, float jumpProgression)
    {
        if (jumper == SeeSawJumper.RAINBOW_DASH && _shouldPreserveRainbowFeedbackAnimation?.Invoke() == true)
            return;

        RhythmVisualUtils.ForceAnimationState(_animationStates, jumper, jumpProgression < 0.5f ? JumpState : FallState);
    }

    /// <summary>
    /// Calcule la duree d'approche depuis une position de depart et les references de cote.
    /// </summary>
    /// <param name="crotchet">Duree d'un beat en secondes au BPM courant.</param>
    /// <param name="fromPos">Position stable de depart.</param>
    /// <param name="innerPos">Reference du cote interieur.</param>
    /// <param name="outerPos">Reference du cote exterieur.</param>
    /// <returns>Deux beats si le depart est proche du cote interieur; quatre beats cote exterieur.</returns>
    public static double GetApproachDuration(double crotchet, Vector2 fromPos, Vector2 innerPos, Vector2 outerPos)
    {
        return new SeeSawJumpPath(fromPos, fromPos, innerPos, outerPos).GetApproachDuration(crotchet);
    }

    /// <summary>
    /// Applique une position de saut en arc sinusoidal a un GameObject.
    /// </summary>
    /// <param name="jumper">GameObject a deplacer.</param>
    /// <param name="path">Chemin contenant depart, arrivee et hauteur d'arc.</param>
    /// <param name="progression">Progression normalisee dans l'arc de saut.</param>
    private static void ApplyJump(GameObject jumper, SeeSawJumpPath path, float progression)
    {
        // Le mouvement horizontal est lineaire; le mouvement vertical suit un arc sinusoidal
        // qui commence et finit au sol. Le chemin porte la hauteur derivee du cote de depart.
        RhythmVisualUtils.ApplySineArc(jumper, path.From, path.To, path.JumpHeight, progression);
    }

    private static bool IsStationaryPath(SeeSawJumpPath path)
    {
        return path.From == path.To && path.JumpHeight == 0f;
    }

    /// <summary>
    /// Dessine cette note visuelle.
    /// </summary>
    /// <param name="spriteBatch">SpriteBatch utilise pour le rendu.</param>
    /// <remarks>
    /// Les notes visuelles See-Saw mutent directement des acteurs possedes par la scene; il n'y a
    /// donc pas de sprite independant propre a chaque note a dessiner ici.
    /// </remarks>
    public override void Draw(SpriteBatch spriteBatch)
    {
    }
}

/// <summary>
/// Decrit un arc de saut deterministe entre deux positions stables du See-Saw.
/// </summary>
/// <remarks>
/// Les references interieure/exterieure ne sont pas forcement les extremites du chemin. Elles
/// servent a determiner si l'acteur part du cote interieur ou exterieur, ce qui controle a la fois
/// la hauteur de l'arc et la duree d'approche. Cette regle doit rester alignee avec l'editeur.
/// </remarks>
public readonly struct SeeSawJumpPath
{
    private const double InnerApproachBeats = 2.0;
    private const double OuterApproachBeats = 4.0;

    /// <summary>
    /// Cree un chemin de saut et derive sa hauteur d'arc depuis le cote de depart.
    /// </summary>
    /// <param name="from">Position stable de depart.</param>
    /// <param name="to">Position stable d'atterrissage.</param>
    /// <param name="innerReference">Reference du cote interieur pour cet acteur.</param>
    /// <param name="outerReference">Reference du cote exterieur pour cet acteur.</param>
    /// <param name="jumpHeightMultiplier">Multiplicateur applique a la hauteur verticale derivee.</param>
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

    /// <summary>
    /// Position stable de depart du saut.
    /// </summary>
    public Vector2 From { get; }

    /// <summary>
    /// Position stable d'atterrissage du saut.
    /// </summary>
    public Vector2 To { get; }

    /// <summary>
    /// Position de reference representant le cote interieur de l'acteur.
    /// </summary>
    public Vector2 InnerReference { get; }

    /// <summary>
    /// Position de reference representant le cote exterieur de l'acteur.
    /// </summary>
    public Vector2 OuterReference { get; }

    /// <summary>
    /// Multiplicateur applique lors du calcul automatique de la hauteur de saut.
    /// </summary>
    public float JumpHeightMultiplier { get; }

    /// <summary>
    /// Hauteur maximale de l'arc de saut, en pixels.
    /// </summary>
    public float JumpHeight { get; }

    // Un sauteur partant du cote interieur a un lead-in plus court qu'un depart cote exterieur.
    // Le timing editeur utilise la meme regle.
    /// <summary>
    /// Calcule la duree d'approche de ce chemin a partir de son cote de depart.
    /// </summary>
    /// <param name="crotchet">Duree d'un beat en secondes au BPM courant.</param>
    /// <returns>Deux beats si le depart est proche de l'interieur; quatre beats s'il est proche de l'exterieur.</returns>
    public double GetApproachDuration(double crotchet)
    {
        return RhythmVisualUtils.ApproachDurationByNearestReference(crotchet, From, InnerReference, OuterReference, InnerApproachBeats, OuterApproachBeats);
    }

    /// <summary>
    /// Selectionne la hauteur d'arc verticale selon le cote de depart.
    /// </summary>
    /// <param name="fromPos">Position stable de depart.</param>
    /// <param name="innerPos">Reference du cote interieur.</param>
    /// <param name="outerPos">Reference du cote exterieur.</param>
    /// <returns>Hauteur de saut interieure ou exterieure, en pixels.</returns>
    private static float GetJumpHeight(Vector2 fromPos, Vector2 innerPos, Vector2 outerPos)
    {
        return RhythmVisualUtils.IsNearer(fromPos, innerPos, outerPos) ? SeeSawVisualNote.InnerJumpHeight : SeeSawVisualNote.OuterJumpHeight;
    }
}

/// <summary>
/// Saut optionnel qui se deroule en meme temps que le sauteur principal.
/// </summary>
public readonly struct SeeSawSimultaneousJump
{
    public SeeSawSimultaneousJump(SeeSawJumper jumper, SeeSawJumpPath path, bool isBigLeap)
    {
        Jumper = jumper;
        Path = path;
        IsBigLeap = isBigLeap;
    }

    public SeeSawJumper Jumper { get; }
    public SeeSawJumpPath Path { get; }
    public bool IsBigLeap { get; }
}

/// <summary>
/// Contre-saut optionnel effectue par l'autre acteur avant l'atterrissage du sauteur principal.
/// </summary>
public readonly struct SeeSawCounterJump
{
    /// <summary>
    /// Cree une configuration de contre-saut.
    /// </summary>
    /// <param name="jumper">Acteur qui effectue le contre-saut.</param>
    /// <param name="path">Chemin suivi par le contre-sauteur.</param>
    /// <param name="targetRotation">Rotation de poutre apres l'atterrissage de ce contre-sauteur.</param>
    public SeeSawCounterJump(SeeSawJumper jumper, SeeSawJumpPath path, float targetRotation)
    {
        Jumper = jumper;
        Path = path;
        TargetRotation = targetRotation;
    }

    /// <summary>
    /// Identite de l'acteur qui effectue le contre-saut.
    /// </summary>
    public SeeSawJumper Jumper { get; }

    /// <summary>
    /// Chemin d'arc suivi par le contre-sauteur.
    /// </summary>
    public SeeSawJumpPath Path { get; }

    /// <summary>
    /// Rotation de poutre appliquee lorsque le contre-sauteur a atterri.
    /// </summary>
    public float TargetRotation { get; }
}

/// <summary>
/// Identifie les acteurs pouvant participer a une choregraphie See-Saw.
/// </summary>
public enum SeeSawJumper
{
    /// <summary>
    /// Applejack, utilisee comme contre-sauteuse dans les choregraphies actuelles.
    /// </summary>
    APPLEJACK,

    /// <summary>
    /// Rainbow Dash, utilisee comme sauteuse principale dans les actions de chart actuelles.
    /// </summary>
    RAINBOW_DASH
}
