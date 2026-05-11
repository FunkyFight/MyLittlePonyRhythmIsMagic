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
    /// Vérifie que le runtime peut choisir le driver d'une track via une policy, sans SetDriver manuel par frame.
    /// </summary>
    [Fact]
    public void ResolveDriversUsesTrackPolicy()
    {
        VisualRuntime runtime = new();
        Counter target = new();
        Note first = new(1.0);
        Note second = new(2.0);
        runtime.RegisterTrack("target", target)
            .UseDriverResolver(ctx => ctx.Notes[ctx.Notes.Count - 1]);

        runtime.ResolveDrivers(1.5, new[] { first, second });

        Assert.Same(second, runtime.Track("target").DriverNote);
    }

    /// <summary>
    /// Vérifie que la policy de background SeaPony choisit la dernière note dont la fenêtre de scroll est active.
    /// </summary>
    [Fact]
    public void ResolveDriversUsesSeaponyBackgroundDriverPolicy()
    {
        Note first = CreateSeaponyNote(1.0, SeaponyAction.Swim);
        Note second = CreateSeaponyNote(1.5, SeaponyAction.Roll);

        Assert.Null(ResolveSeaponyBackgroundDriver(0.9, first, second));
        Assert.Same(first, ResolveSeaponyBackgroundDriver(1.2, first, second));
        Assert.Same(second, ResolveSeaponyBackgroundDriver(1.6, first, second));
        Assert.Null(ResolveSeaponyBackgroundDriver(2.6, first, second));
    }

    /// <summary>
    /// Vérifie qu'une note SeaPony en approche peut piloter les acteurs quand aucune note post-hit ne gagne.
    /// </summary>
    [Fact]
    public void SeaponyActorDriverPolicySelectsApproachNote()
    {
        Note approach = CreateSeaponyNote(2.0, SeaponyAction.Swim);

        Assert.Same(approach, ResolveSeaponyActorDriver(1.5, approach));
    }

    /// <summary>
    /// Vérifie que la priorité legacy conserve une note post-hit devant une autre note en approche.
    /// </summary>
    [Fact]
    public void SeaponyActorDriverPolicyPrefersPostHitOverApproach()
    {
        Note postHit = CreateSeaponyNote(1.0, SeaponyAction.Swim);
        Note approach = CreateSeaponyNote(1.75, SeaponyAction.Swim);

        Assert.Same(postHit, ResolveSeaponyActorDriver(1.5, postHit, approach));
    }

    /// <summary>
    /// Vérifie l'exception legacy : une approche Roll recouvre la queue post-hit d'un TapTap.
    /// </summary>
    [Fact]
    public void SeaponyActorDriverPolicyLetsRollApproachOverrideTapTapTail()
    {
        Note tapTapTail = CreateSeaponyNote(1.0, SeaponyAction.TapTap);
        Note rollApproach = CreateSeaponyNote(1.75, SeaponyAction.Roll);

        Assert.Same(rollApproach, ResolveSeaponyActorDriver(1.5, tapTapTail, rollApproach));
    }

    /// <summary>
    /// Vérifie que les mutations ignorées exposent une raison de debug exploitable.
    /// </summary>
    [Fact]
    public void MutateReportsIgnoredReason()
    {
        VisualRuntime runtime = new();
        Counter target = new();
        Note note = new(1.0);
        VisualMutationIgnored ignored = null;
        runtime.RegisterTrack("target", target);
        runtime.MutationIgnored += info => ignored = info;

        TestDirectedVisualNote visual = new(note, runtime, timeline =>
        {
            timeline.AfterHitUntilDespawn("post")
                .Do((ctx, phase) => ctx.Mutate<Counter>("target", counter => counter.Value++));
        });

        visual.Update(1.25);

        Assert.NotNull(ignored);
        Assert.Equal("target", ignored.TrackId);
        Assert.Equal(VisualMutationIgnoredReason.TrackNotOwned, ignored.Reason);
        Assert.Equal(0, target.Value);
    }

    /// <summary>
    /// Vérifie que chaque no-op de mutation expose sa raison précise côté diagnostics.
    /// </summary>
    [Fact]
    public void MutateReportsSpecificIgnoredReasons()
    {
        VisualRuntime runtime = new();
        Note note = new(1.0);
        Note otherNote = new(1.0);
        List<VisualMutationIgnored> ignored = new();
        runtime.RegisterTrack("no_driver", new Counter());
        runtime.RegisterTrack("wrong_driver", new Counter());
        runtime.RegisterTrack("wrong_type", new Counter());
        runtime.SetDriver("wrong_driver", otherNote);
        runtime.SetDriver("wrong_type", note);
        runtime.MutationIgnored += info => ignored.Add(info);

        TestDirectedVisualNote visual = new(note, runtime, timeline =>
        {
            timeline.AfterHitUntilDespawn("post")
                .Owns("missing", "no_driver", "wrong_driver", "wrong_type")
                .Do((ctx, phase) =>
                {
                    ctx.Mutate<Counter>("missing", counter => counter.Value++);
                    ctx.Mutate<Counter>("no_driver", counter => counter.Value++);
                    ctx.Mutate<Counter>("wrong_driver", counter => counter.Value++);
                    ctx.Mutate<string>("wrong_type", value => _ = value.Length);
                });
        });

        visual.Update(1.25);

        Assert.Equal(4, ignored.Count);
        Assert.Contains(ignored, info => info.TrackId == "missing" && info.Reason == VisualMutationIgnoredReason.TrackMissing);
        Assert.Contains(ignored, info => info.TrackId == "no_driver" && info.Reason == VisualMutationIgnoredReason.NoDriver);
        Assert.Contains(ignored, info => info.TrackId == "wrong_driver" && info.Reason == VisualMutationIgnoredReason.WrongDriver);
        Assert.Contains(ignored, info => info.TrackId == "wrong_type" && info.Reason == VisualMutationIgnoredReason.WrongTargetType);
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
        string rainbowTrackId = SeeSawVisualNote.GetJumperTrackId(SeeSawJumper.RAINBOW_DASH);
        string rainbowAnimationTrackId = SeeSawVisualNote.GetJumperAnimationTrackId(SeeSawJumper.RAINBOW_DASH);
        runtime.RegisterTrack(rainbowTrackId, rainbow);
        runtime.RegisterTrack(rainbowAnimationTrackId, states[SeeSawJumper.RAINBOW_DASH]);
        runtime.RegisterTrack(SeeSawVisualNote.BeamTrackId, beam);

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

        runtime.SetDriver(rainbowTrackId, otherNote);
        runtime.SetDriver(rainbowAnimationTrackId, otherNote);
        runtime.SetDriver(SeeSawVisualNote.BeamTrackId, otherNote);
        visual.Update(0.5);
        Assert.Null(states[SeeSawJumper.RAINBOW_DASH].CurrentState);

        runtime.SetDriver(rainbowTrackId, note);
        runtime.SetDriver(rainbowAnimationTrackId, note);
        runtime.SetDriver(SeeSawVisualNote.BeamTrackId, note);
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
    /// Crée une note SeaPony logique avec l'action encodée comme dans les charts.
    /// </summary>
    private static Note CreateSeaponyNote(double songPosition, SeaponyAction action)
    {
        return new Note(songPosition, additionnalData: SeaponyNoteCodec.Write(action));
    }

    /// <summary>
    /// Résout le driver SeaPony acteur avec des durées de test déterministes.
    /// </summary>
    private static Note ResolveSeaponyActorDriver(double songPosition, params Note[] notes)
    {
        VisualRuntime runtime = new();
        runtime.RegisterTrack("actor", new Counter())
            .UseDriverPolicy(new SeaponyActorDriverPolicy(
                getApproachDuration: (_, _) => 1.0,
                getDespawnDelay: GetTestSeaponyDespawnDelay,
                getMaxApproachDuration: () => 1.0));

        runtime.ResolveDrivers(songPosition, notes);
        return runtime.Track("actor").DriverNote;
    }

    /// <summary>
    /// Résout le driver SeaPony background avec une durée de scroll fixe.
    /// </summary>
    private static Note ResolveSeaponyBackgroundDriver(double songPosition, params Note[] notes)
    {
        VisualRuntime runtime = new();
        runtime.RegisterTrack("background", new Counter())
            .UseDriverPolicy(new SeaponyBackgroundDriverPolicy((_, _) => 1.0));

        runtime.ResolveDrivers(songPosition, notes);
        return runtime.Track("background").DriverNote;
    }

    /// <summary>
    /// Donne aux TapTap une queue plus longue afin de tester le recouvrement Roll legacy.
    /// </summary>
    private static double GetTestSeaponyDespawnDelay(SeaponyAction action, Note note)
    {
        return action == SeaponyAction.TapTap ? 1.0 : 0.75;
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
