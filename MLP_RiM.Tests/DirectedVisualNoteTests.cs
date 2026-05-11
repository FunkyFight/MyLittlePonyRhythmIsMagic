using System;
using System.Collections.Generic;
using GameCore.Animation;
using GameCore.GameObjects;
using Microsoft.Xna.Framework;
using MLP_RiM.Elements.Editor;
using Rhythm.Note;
using Xunit;

/// <summary>
/// Tests de non-régression pour l'infrastructure Directed Visual Notes et la migration du background SeaPony.
/// </summary>
public sealed class DirectedVisualNoteTests
{
    /// <summary>
    /// Vérifie qu'une mutation ne s'applique que si la track est possédée par le bloc courant et drivé par la note.
    /// </summary>
    [Fact]
    public void MutateRequiresOwnedTrackAndCurrentDriver()
    {
        VisualRuntime runtime = new();
        Counter owned = new();
        Counter unowned = new();
        Note note = new(1.0);
        Note otherNote = new(1.0);
        runtime.RegisterTrack("owned", owned);
        runtime.RegisterTrack("unowned", unowned);

        TestDirectedVisualNote visual = new(note, runtime, timeline =>
        {
            timeline.AfterHitUntilDespawn("post")
                .Owns("owned")
                .Do((ctx, phase) =>
                {
                    ctx.Mutate<Counter>("owned", counter => counter.Value++);
                    ctx.Mutate<Counter>("unowned", counter => counter.Value++);
                });
        });

        visual.Update(1.25);
        Assert.Equal(0, owned.Value);
        Assert.Equal(0, unowned.Value);

        runtime.SetDriver("owned", otherNote);
        visual.Update(1.3);
        Assert.Equal(0, owned.Value);

        runtime.SetDriver("owned", note);
        visual.Update(1.35);
        Assert.Equal(1, owned.Value);
        Assert.Equal(0, unowned.Value);
    }

    /// <summary>
    /// Vérifie qu'un évènement forward-only ignore le premier sample, les samples sans crossing et les rewinds.
    /// </summary>
    [Fact]
    public void ForwardCrossedIsForwardOnlyAndReusableAfterRewind()
    {
        VisualEventGate gate = new();

        Assert.False(gate.ForwardCrossed("hit", double.NaN, 1.0, 1.0, hasRewound: false));
        Assert.True(gate.ForwardCrossed("hit", 0.9, 1.0, 1.0, hasRewound: false));
        Assert.False(gate.ForwardCrossed("hit", 1.0, 1.1, 1.0, hasRewound: false));
        Assert.False(gate.ForwardCrossed("hit", 1.2, 0.8, 1.0, hasRewound: true));
        Assert.True(gate.ForwardCrossed("hit", 0.8, 1.0, 1.0, hasRewound: false));
    }

    /// <summary>
    /// Vérifie les progressions locale/globale d'une phase et ses crossings de phase.
    /// </summary>
    [Fact]
    public void PhaseContextComputesProgressAndForwardCrossings()
    {
        VisualRuntime runtime = new();
        Counter target = new();
        Note note = new(2.0);
        runtime.RegisterTrack("target", target);
        runtime.SetDriver("target", note);
        PhaseContext captured = null;

        TestDirectedVisualNote visual = new(note, runtime, timeline =>
        {
            timeline.Phase("middle", 0.25f, 0.75f)
                .Owns("target")
                .Do((ctx, phase) =>
                {
                    captured = phase;
                    if(phase.CrossedLocalForward("middle", 0.5f))
                        ctx.Mutate<Counter>("target", counter => counter.Value++);
                });
        }, approachDuration: 2.0);

        visual.Update(0.8);
        visual.Update(1.0);

        Assert.NotNull(captured);
        NearlyEqual(0.5f, captured.LocalProgress);
        NearlyEqual(0.5f, captured.UnclampedLocalProgress);
        Assert.True(captured.IsActive);
        Assert.True(captured.WasActive);
        Assert.False(captured.JustEnteredForward("enter"));
        Assert.Equal(1, target.Value);
    }

    /// <summary>
    /// Vérifie que la phase post-hit ne sample pas avant le hit ni après le despawn.
    /// </summary>
    [Fact]
    public void AfterHitUntilDespawnSamplesOnlyPostHitWindow()
    {
        VisualRuntime runtime = new();
        Counter target = new();
        Note note = new(1.0);
        runtime.RegisterTrack("target", target);
        runtime.SetDriver("target", note);

        TestDirectedVisualNote visual = new(note, runtime, timeline =>
        {
            timeline.AfterHitUntilDespawn("post")
                .DoOwned<Counter>("target", (ctx, phase, counter) => counter.Value++);
        }, despawnDelay: 1.0);

        visual.Update(0.9);
        visual.Update(1.0);
        visual.Update(1.5);
        visual.Update(2.1);

        Assert.Equal(2, target.Value);
    }

    /// <summary>
    /// Vérifie que la note background SeaPony mute le background uniquement quand elle est driver de la track.
    /// </summary>
    [Fact]
    public void SeaponyBgVisualNoteUpdatesBackgroundOnlyWhenDrivenByNote()
    {
        VisualRuntime runtime = new();
        InfiniteScrollBackground background = CreateBackground();
        Note note = new(1.0, additionnalData: SeaponyNoteCodec.Write(SeaponyAction.Swim));
        Note otherNote = new(1.0, additionnalData: SeaponyNoteCodec.Write(SeaponyAction.Swim));
        runtime.RegisterTrack("background", background);
        SeaponyBgVisualNote visual = new(note, runtime, approachDuration: 1.0, backgroundScrollDestinationBeat: 4, despawnDelay: 1.0);

        runtime.SetDriver("background", otherNote);
        visual.Update(1.5);
        Assert.Equal(0f, background.Progression);

        runtime.SetDriver("background", note);
        visual.Update(1.5);
        Assert.InRange(background.Progression, 3.0f, 4.0f);
        Assert.NotEqual(0f, background.Progression);
    }

    /// <summary>
    /// Vérifie que <see cref="SeaponyVisualNote"/> utilise les tracks runtime pour remplacer le guard canApplyState.
    /// </summary>
    [Fact]
    public void SeaponyVisualNoteMutatesOnlyWhenRuntimeTracksAreDrivenByNote()
    {
        VisualRuntime runtime = new();
        GameObject seaPony = new(null);
        AnimationStateMachine stateMachine = CreateSeaPonyStateMachine();
        Note note = new(1.0, additionnalData: SeaponyNoteCodec.Write(SeaponyAction.Swim));
        Note otherNote = new(1.0, additionnalData: SeaponyNoteCodec.Write(SeaponyAction.Swim));
        string objectTrackId = SeaponyVisualNote.GetPonyTrackId(0);
        string animationTrackId = SeaponyVisualNote.GetPonyAnimationTrackId(0);
        runtime.RegisterTrack(objectTrackId, seaPony);
        runtime.RegisterTrack(animationTrackId, stateMachine);

        SeaponyVisualNote visual = new(
            note,
            approachDuration: 1.0,
            scene: null,
            seaPony: seaPony,
            seaPonyStateMachine: stateMachine,
            seaPonyIndex: 0,
            crotchet: 0.5,
            despawnDelay: 0.5,
            runtime: runtime,
            seaPonyTrackId: objectTrackId,
            seaPonyAnimationTrackId: animationTrackId);

        runtime.SetDriver(objectTrackId, otherNote);
        runtime.SetDriver(animationTrackId, otherNote);
        visual.Update(0.5);
        Assert.Null(stateMachine.CurrentState);

        runtime.SetDriver(objectTrackId, note);
        runtime.SetDriver(animationTrackId, note);
        visual.Update(0.5);
        Assert.Equal("swim_anticipation", stateMachine.CurrentState.Name);
    }

    /// <summary>
    /// Vérifie que <see cref="SeeSawVisualNote"/> peut être gate par une track runtime sans utiliser son Func legacy.
    /// </summary>
    [Fact]
    public void SeeSawVisualNoteMutatesOnlyWhenRuntimeTrackIsDrivenByNote()
    {
        VisualRuntime runtime = new();
        Note note = new(1.0);
        Note otherNote = new(1.0);
        GameObject rainbow = new(null);
        GameObject applejack = new(null);
        GameObject beam = new(null);
        Dictionary<SeeSawJumper, GameObject> jumpers = new()
        {
            [SeeSawJumper.RAINBOW_DASH] = rainbow,
            [SeeSawJumper.APPLEJACK] = applejack
        };
        Dictionary<SeeSawJumper, AnimationStateMachine> states = new()
        {
            [SeeSawJumper.RAINBOW_DASH] = CreateSeeSawStateMachine(),
            [SeeSawJumper.APPLEJACK] = CreateSeeSawStateMachine()
        };
        runtime.RegisterTrack(SeeSawVisualNote.DefaultRuntimeTrackId, beam);

        SeeSawVisualNote visual = new(
            note,
            jumpers,
            states,
            SeeSawJumper.RAINBOW_DASH,
            new SeeSawJumpPath(Vector2.Zero, new Vector2(10f, 0f), Vector2.Zero, new Vector2(100f, 0f)),
            crotchet: 0.5,
            seeSawBeam: beam,
            sceneCamera: null,
            fromRotation: 0f,
            targetRotation: 1f,
            runtime: runtime);

        runtime.SetDriver(SeeSawVisualNote.DefaultRuntimeTrackId, otherNote);
        visual.Update(0.5);
        Assert.Null(states[SeeSawJumper.RAINBOW_DASH].CurrentState);

        runtime.SetDriver(SeeSawVisualNote.DefaultRuntimeTrackId, note);
        visual.Update(0.5);
        Assert.Equal("jump", states[SeeSawJumper.RAINBOW_DASH].CurrentState.Name);
    }

    /// <summary>
    /// Crée un background minimal sans dépendance contenu/graphics pour les tests de mutation.
    /// </summary>
    private static InfiniteScrollBackground CreateBackground()
    {
        return new InfiniteScrollBackground.Builder()
            .AddLine(line =>
            {
                line.AddPrototype(new GameObject(null));
                line.WithPlacementInterval(0, 0);
                line.WithMaxVisibleObjects(1);
            })
            .WithPixelsPerProgress(Vector2.One)
            .Build();
    }

    /// <summary>
    /// Crée une machine d'animation minimale contenant les états SeaPony utilisés par les tests.
    /// </summary>
    private static AnimationStateMachine CreateSeaPonyStateMachine()
    {
        return new AnimationStateMachine()
            .AddState(new AnimationState("idle", 1f))
            .AddState(new AnimationState("swim_anticipation", 1f))
            .AddState(new AnimationState("swim", 1f))
            .AddState(new AnimationState("roll", 1f))
            .AddState(new AnimationState("uptap", 1f))
            .AddState(new AnimationState("downtap", 1f));
    }

    /// <summary>
    /// Crée une machine d'animation minimale contenant les états See-Saw utilisés par les tests.
    /// </summary>
    private static AnimationStateMachine CreateSeeSawStateMachine()
    {
        return new AnimationStateMachine()
            .AddState(new AnimationState("jump", 1f))
            .AddState(new AnimationState("fall", 1f))
            .AddState(new AnimationState("land", 1f))
            .AddState(new AnimationState("fail", 1f));
    }

    /// <summary>
    /// Compare deux valeurs flottantes avec une tolérance explicite.
    /// </summary>
    private static void NearlyEqual(float expected, float actual, float epsilon = 0.000001f)
    {
        Assert.InRange(actual, expected - epsilon, expected + epsilon);
    }

    /// <summary>
    /// Cible mutable minimale utilisée pour tester les tracks génériques.
    /// </summary>
    private sealed class Counter
    {
        public int Value { get; set; }
    }

    /// <summary>
    /// Visual note de test qui expose une timeline construite par lambda.
    /// </summary>
    private sealed class TestDirectedVisualNote : DirectedVisualNote
    {
        private readonly Action<VisualTimeline> _build;

        /// <summary>
        /// Crée une visual note dirigée de test.
        /// </summary>
        public TestDirectedVisualNote(Note note, VisualRuntime runtime, Action<VisualTimeline> build, double approachDuration = 1.0, double despawnDelay = 1.0)
            : base(note, runtime, approachDuration, despawnDelay)
        {
            _build = build;
        }

        /// <summary>
        /// Délègue la déclaration de timeline à la lambda fournie par le test.
        /// </summary>
        protected override void Build(VisualTimeline timeline)
        {
            _build(timeline);
        }
    }
}
