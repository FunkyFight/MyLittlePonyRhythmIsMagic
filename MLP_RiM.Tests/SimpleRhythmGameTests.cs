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
                .SingleHit();

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
}
