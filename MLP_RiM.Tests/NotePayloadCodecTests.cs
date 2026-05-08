using System.Collections.Generic;
using MLP_RiM.Elements.Editor;
using Rhythm.Note;
using Xunit;

public sealed class NotePayloadCodecTests
{
    [Fact]
    public void SeaponyCodecReadsLegacyActionWithoutMetadata()
    {
        Dictionary<string, string> data = SeaponyNoteCodec.Write(SeaponyAction.Roll);
        data.Remove(NotePayloadKeys.Game);
        data.Remove(NotePayloadKeys.Type);
        data.Remove(NotePayloadKeys.Version);

        Assert.True(SeaponyNoteCodec.TryRead(data, out SeaponyNotePayload payload));
        Assert.Equal(SeaponyAction.Roll, payload.Action);
        Assert.True(SeaponyNoteCodec.IsAction(data, SeaponyAction.Roll));
    }

    [Fact]
    public void SeaponyCodecRejectsMismatchedExplicitMetadata()
    {
        Dictionary<string, string> data = SeaponyNoteCodec.Write(SeaponyAction.Roll);
        data[NotePayloadKeys.Game] = "other_game";

        Assert.False(SeaponyNoteCodec.TryRead(data, out _));
        Assert.False(SeaponyNoteCodec.IsAction(data, SeaponyAction.Roll));
    }

    [Fact]
    public void PayloadDataEqualsIgnoresExplicitMetadataForLegacyMatching()
    {
        Dictionary<string, string> legacyData = SeaponyNoteCodec.Write(SeaponyAction.Roll);
        legacyData.Remove(NotePayloadKeys.Game);
        legacyData.Remove(NotePayloadKeys.Type);
        legacyData.Remove(NotePayloadKeys.Version);
        Dictionary<string, string> typedData = SeaponyNoteCodec.Write(SeaponyAction.Roll);

        Assert.True(NotePayloadKeys.PayloadDataEquals(legacyData, typedData));
    }

    [Fact]
    public void PayloadDataEqualsStillRejectsDifferentGameplayData()
    {
        Assert.False(NotePayloadKeys.PayloadDataEquals(
            SeaponyNoteCodec.Write(SeaponyAction.Roll),
            SeaponyNoteCodec.Write(SeaponyAction.Swim)));
    }

    [Fact]
    public void RhythmInputFallbackIgnoresExplicitTypedPayloads()
    {
        ChartNote note = new()
        {
            InputActionToPress = RhythmInputEditorNote.DefinitionInstance.InputAction,
            AdditionnalData = new Dictionary<string, string>
            {
                [NotePayloadKeys.Game] = SeaponyNoteCodec.GameId,
                [NotePayloadKeys.Type] = SeaponyNoteCodec.NoteId
            }
        };

        Assert.NotEqual(RhythmInputEditorNote.TypeId, EditorNoteDefinitions.FromChartNote(note)?.TypeId);
    }

    [Fact]
    public void LegacySeaponyRuntimeNoteMigratesToMatchingAuthorClip()
    {
        Dictionary<string, string> data = SeaponyNoteCodec.Write(SeaponyAction.Roll);
        data.Remove(NotePayloadKeys.Game);
        data.Remove(NotePayloadKeys.Type);
        data.Remove(NotePayloadKeys.Version);
        ChartNote note = new()
        {
            BeatPosition = 4,
            SongPosition = 2,
            InputActionToPress = "ReactMain",
            AdditionnalData = data
        };

        ChartEditorClip clip = EditorClipCompiler.CreateClipFromLegacyNote(note, _ => 4, 0);

        Assert.Equal(SeaponyParadeEditorNoteProvider.GameId, clip.RhythmGameId);
        Assert.Equal(SeaponyParadeEditorNoteProvider.RollClipId, clip.ClipTypeId);
        Assert.Equal(3, clip.LengthBeats);
    }

    [Fact]
    public void LegacySeeSawRuntimeNoteMigratesToMatchingAuthorClip()
    {
        Dictionary<string, string> data = new();
        SeeSawAction.SetPattern(data, SeeSawPatternKind.ShortLong);
        ChartNote note = new()
        {
            BeatPosition = 4,
            SongPosition = 2,
            InputActionToPress = "ReactMain",
            AdditionnalData = data
        };

        ChartEditorClip clip = EditorClipCompiler.CreateClipFromLegacyNote(note, _ => 4, 0);

        Assert.Equal(SeeSawEditorNote.GameId, clip.RhythmGameId);
        Assert.Equal(SeeSawEditorNote.ShortLongClipId, clip.ClipTypeId);
        Assert.Equal(3, clip.LengthBeats);
    }
}
