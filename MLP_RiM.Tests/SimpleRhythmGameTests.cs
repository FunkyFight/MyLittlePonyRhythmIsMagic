using System;
using System.Collections.Generic;
using System.Linq;
using MLP_RiM.Elements.Editor;
using Rhythm.Note;
using Xunit;

public sealed class SimpleRhythmGameTests
{
    [Fact]
    public void EnumCodecWritesMetadataReadsLegacyActionAndRejectsOtherGame()
    {
        EnumNoteCodec<SimpleTestAction> codec = new("my_game", "note");

        Dictionary<string, string> data = codec.Write(SimpleTestAction.DoubleTap);
        Dictionary<string, string> legacyData = new()
        {
            [NotePayloadKeys.Action] = "my_game_double_tap"
        };
        Dictionary<string, string> mismatchedData = new(data)
        {
            [NotePayloadKeys.Game] = "other_game"
        };

        Assert.Equal("my_game", data[NotePayloadKeys.Game]);
        Assert.Equal("note", data[NotePayloadKeys.Type]);
        Assert.Equal("1", data[NotePayloadKeys.Version]);
        Assert.Equal("my_game_double_tap", data[NotePayloadKeys.Action]);
        Assert.True(codec.TryRead(legacyData, out EnumNotePayload<SimpleTestAction> payload));
        Assert.Equal(SimpleTestAction.DoubleTap, payload.Action);
        Assert.False(codec.TryRead(mismatchedData, out _));
    }

    [Fact]
    public void EnumCodecMapsPascalCaseActionIds()
    {
        EnumNoteCodec<SimpleTestAction> codec = new("my_game", "note");

        Assert.Equal("double_tap", EnumNoteCodec<SimpleTestAction>.ToActionId(SimpleTestAction.DoubleTap));
        Assert.Equal("my_game_double_tap", codec.ToLegacyActionValue(SimpleTestAction.DoubleTap));
        Assert.Equal("my_game.double_tap", codec.GetClipTypeId(SimpleTestAction.DoubleTap));
        Assert.Equal("double_tap", codec.GetVariantId(SimpleTestAction.DoubleTap));
    }

    [Fact]
    public void SimpleClipCompilesSingleHitAtStartBeat()
    {
        SimpleTestRhythmGame provider = new();
        ChartEditorClip clip = CreateClip(provider, "my_game.basic", startBeat: 4.0, lengthBeats: 0.0);

        IReadOnlyList<ChartNote> notes = provider.CompileClip(clip, CreateTempoMap());

        Assert.Equal(new[] { 4.0 }, notes.Select(note => note.BeatPosition.GetValueOrDefault()).ToArray());
    }

    [Fact]
    public void SimpleClipCompilesDoubleEmitOffsets()
    {
        SimpleTestRhythmGame provider = new();
        ChartEditorClip clip = CreateClip(provider, "my_game.double_tap", startBeat: 8.0, lengthBeats: 0.0);

        IReadOnlyList<ChartNote> notes = provider.CompileClip(clip, CreateTempoMap());

        Assert.Equal(new[] { 8.0, 8.5 }, notes.Select(note => note.BeatPosition.GetValueOrDefault()).ToArray());
    }

    [Fact]
    public void SimpleClipCompilesLeadInAndInclusiveRepeat()
    {
        SimpleTestRhythmGame provider = new();
        ChartEditorClip clip = CreateClip(provider, "my_game.repeat", startBeat: 1.0, lengthBeats: 4.0);

        IReadOnlyList<ChartNote> notes = provider.CompileClip(clip, CreateTempoMap());

        Assert.Equal(new[] { 3.0, 5.0, 7.0 }, notes.Select(note => note.BeatPosition.GetValueOrDefault()).ToArray());
    }

    [Fact]
    public void SimpleClipHoldForLengthConvertsThroughTempoMap()
    {
        SimpleTestRhythmGame provider = new();
        ChartEditorClip clip = CreateClip(provider, "my_game.hold", startBeat: 4.0, lengthBeats: 2.0);

        ChartNote note = Assert.Single(provider.CompileClip(clip, CreateTempoMap()));

        NearlyEqual(2.0, note.HoldBeats.GetValueOrDefault());
        NearlyEqual(1.0, note.HoldDuration);
    }

    [Fact]
    public void SimpleClipDeclaresTimingIndependentlyFromRuntimeNoteDefaults()
    {
        SimpleTestRhythmGame provider = new();
        EditorClipDefinition clip = provider.Clips.First(definition => definition.ClipTypeId == "my_game.basic");
        EditorNoteDefinition noteDefinition = provider.Definition;
        ChartNote note = noteDefinition.CreateChartNote(0, 0.5, variantIndex: 0);
        ChartTiming.SetNoteBeat(note, 10.0);

        NoteTimingResult timing = noteDefinition.GetTiming(new NoteTimingRequest(
            note,
            noteDefinition,
            noteVariantIndex: 0,
            beat: 10.0,
            tempoMap: CreateTempoMap(),
            previousNotes: Array.Empty<ChartNote>(),
            nextNotes: Array.Empty<ChartNote>(),
            gameContext: null));

        NearlyEqual(2.0, clip.TimingProfile.OccupyBeforeBeats);
        NearlyEqual(3.0, clip.TimingProfile.OccupyAfterBeats);
        NearlyEqual(0.25, clip.TimingProfile.HitWindowBeforeBeats);
        NearlyEqual(0.75, clip.TimingProfile.HitWindowAfterBeats);
        NearlyEqual(8.0, timing.StartBeat);
        NearlyEqual(13.0, timing.EndBeat);
        NearlyEqual(9.75, timing.HitStartBeat);
        NearlyEqual(10.75, timing.HitEndBeat);
        NearlyEqual(10.0, timing.SameVariantHitStartBeat);
        NearlyEqual(10.5, timing.SameVariantHitEndBeat);
    }

    [Fact]
    public void SimpleProviderMapsLegacyRuntimeNoteToMatchingClipAndVariant()
    {
        SimpleTestRhythmGame provider = new();
        ChartNote note = new()
        {
            AdditionnalData = new Dictionary<string, string>
            {
                [NotePayloadKeys.Action] = "my_game_double_tap"
            }
        };

        Assert.Equal("my_game.double_tap", provider.GetClipTypeIdFromLegacyNote(note));
        Assert.Equal(1, provider.GetNoteVariantIndex(note));
    }

    [Fact]
    public void SimpleProviderRejectsDuplicateRuntimeActions()
    {
        DuplicateRuntimeActionRhythmGame provider = new();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => _ = provider.Clips);

        Assert.Contains("Simple rhythm game 'my_game'", exception.Message);
        Assert.Contains("multiple runtime clips for action 'Basic'", exception.Message);
        Assert.Contains("'my_game.basic_a'", exception.Message);
        Assert.Contains("'my_game.basic_b'", exception.Message);
    }

    [Fact]
    public void SimpleProviderRejectsDuplicateClipIds()
    {
        DuplicateClipIdRhythmGame provider = new();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => _ = provider.Clips);

        Assert.Contains("Simple rhythm game 'my_game'", exception.Message);
        Assert.Contains("duplicate clip id 'my_game.duplicate'", exception.Message);
        Assert.Contains("action 'Basic'", exception.Message);
        Assert.Contains("action 'DoubleTap'", exception.Message);
    }

    private static ChartEditorClip CreateClip(SimpleTestRhythmGame provider, string clipTypeId, double startBeat, double lengthBeats)
    {
        EditorClipDefinition definition = provider.Clips.First(clip => clip.ClipTypeId == clipTypeId);
        return new ChartEditorClip
        {
            Id = clipTypeId,
            TrackIndex = 0,
            StartBeat = startBeat,
            LengthBeats = lengthBeats,
            RhythmGameId = provider.RhythmGameId,
            ClipTypeId = clipTypeId,
            ClipCategory = definition.Category.ToString(),
            InputAction = definition.InputAction,
            Data = new Dictionary<string, string>(definition.DefaultData)
        };
    }

    private static ChartTempoMap CreateTempoMap()
    {
        return new ChartTempoMap(new Chart
        {
            BPM = 120,
            Offset = 0,
            ChartVersion = 2
        });
    }

    private static void NearlyEqual(double expected, double actual, double epsilon = 0.000001)
    {
        Assert.InRange(actual, expected - epsilon, expected + epsilon);
    }

    private enum SimpleTestAction
    {
        Basic,
        DoubleTap,
        Repeat,
        Hold
    }

    private sealed class SimpleTestRhythmGame : SimpleRhythmGame<SimpleTestAction>
    {
        protected override void Build(RhythmGameBuilder<SimpleTestAction> game)
        {
            game.Id("my_game")
                .DisplayName("My Game")
                .SortOrder(100);

            game.RuntimeNote()
                .Input("ReactMain")
                .Occupies(0, 1)
                .HitWindow(0, 1);

            game.Clip(SimpleTestAction.Basic)
                .SingleHit()
                .Occupies(2, 3)
                .HitWindow(0.25, 0.75)
                .SameVariantHitWindow(0, 0.5);

            game.Clip(SimpleTestAction.DoubleTap)
                .SingleHit()
                .Pair(0.5);

            game.Clip(SimpleTestAction.Repeat)
                .Continuous(4)
                .LeadIn(2)
                .RepeatEvery(2);

            game.Clip(SimpleTestAction.Hold)
                .Continuous(2)
                .HoldForClipLength();

            game.NoHit(1);
        }
    }

    private sealed class DuplicateRuntimeActionRhythmGame : SimpleRhythmGame<SimpleTestAction>
    {
        protected override void Build(RhythmGameBuilder<SimpleTestAction> game)
        {
            game.Id("my_game")
                .DisplayName("My Game");

            game.RuntimeNote();

            game.Clip(SimpleTestAction.Basic)
                .Id("my_game.basic_a")
                .SingleHit();

            game.Clip(SimpleTestAction.Basic)
                .Id("my_game.basic_b")
                .SingleHit();
        }
    }

    private sealed class DuplicateClipIdRhythmGame : SimpleRhythmGame<SimpleTestAction>
    {
        protected override void Build(RhythmGameBuilder<SimpleTestAction> game)
        {
            game.Id("my_game")
                .DisplayName("My Game");

            game.RuntimeNote();

            game.Clip(SimpleTestAction.Basic)
                .Id("my_game.duplicate")
                .SingleHit();

            game.Clip(SimpleTestAction.DoubleTap)
                .Id("my_game.duplicate")
                .SingleHit();
        }
    }
}
