using System.Collections.Generic;
using MLP_RiM.Elements.Editor;
using Rhythm.Note;
using Xunit;

public sealed class ChartTempoMapTests
{
    [Fact]
    public void BeatAndSecondsRoundTripAcrossBpmChanges()
    {
        ChartTempoMap tempoMap = new(CreateBeatFirstChart());

        foreach (double beat in new[] { 0.0, 1.25, 3.999, 4.0, 6.5, 8.0, 11.25 })
            NearlyEqual(beat, tempoMap.SecondsToBeat(tempoMap.BeatToSeconds(beat)));

        foreach (double seconds in new[] { 1.0, 1.5, 2.999, 3.0, 5.5, 7.0, 8.25 })
            NearlyEqual(seconds, tempoMap.BeatToSeconds(tempoMap.SecondsToBeat(seconds)));
    }

    [Fact]
    public void BpmLookupUsesBeatAnchoredEffects()
    {
        ChartTempoMap tempoMap = new(CreateBeatFirstChart());

        NearlyEqual(120, tempoMap.GetBpmAtBeat(3.999));
        NearlyEqual(60, tempoMap.GetBpmAtBeat(4.0));
        NearlyEqual(60, tempoMap.GetBpmAtSeconds(6.999));
        NearlyEqual(180, tempoMap.GetBpmAtBeat(8.0));
        NearlyEqual(180, tempoMap.GetBpmAtSeconds(7.0));
    }

    [Fact]
    public void LegacyV1BpmEffectsAreConvertedFromSecondsToBeats()
    {
        Chart chart = new()
        {
            BPM = 120,
            Offset = 0,
            Effects = new List<ChartEffect>
            {
                CreateBpmEffect(songPosition: 3.0, beatPosition: null, bpm: 60)
            }
        };

        ChartTempoMap tempoMap = new(chart);

        NearlyEqual(120, tempoMap.GetBpmAtBeat(5.999));
        NearlyEqual(60, tempoMap.GetBpmAtBeat(6.0));
        NearlyEqual(5.0, tempoMap.BeatToSeconds(8.0));
    }

    [Fact]
    public void ChangingBpmDoesNotMoveLogicalBeats()
    {
        ChartEffect bpmChange = CreateBpmEffect(songPosition: 20.5, beatPosition: 41.0, bpm: 120);
        Chart chart = new()
        {
            ChartVersion = 2,
            BPM = 120,
            Offset = 0,
            Effects = new List<ChartEffect> { bpmChange },
            Notes = new List<ChartNote>
            {
                CreateNote(40.0, holdBeats: 0),
                CreateNote(42.0, holdBeats: 1),
                CreateNote(46.0, holdBeats: 2)
            }
        };

        ChartTempoMap before = new(chart);
        double[] editorXBefore = chart.Notes.ConvertAll(note => note.BeatPosition.GetValueOrDefault() * 32.0).ToArray();
        double secondsAt42Before = before.BeatToSeconds(42.0);

        bpmChange.SetBpm(180);
        ChartTempoMap after = new(chart);
        double[] editorXAfter = chart.Notes.ConvertAll(note => note.BeatPosition.GetValueOrDefault() * 32.0).ToArray();

        for (int i = 0; i < chart.Notes.Count; i++)
        {
            NearlyEqual(editorXBefore[i], editorXAfter[i]);
            NearlyEqual(i == 0 ? 40.0 : i == 1 ? 42.0 : 46.0, chart.Notes[i].BeatPosition.GetValueOrDefault());
        }

        NearlyEqual(20.0, after.BeatToSeconds(40.0));
        Assert.NotEqual(secondsAt42Before, after.BeatToSeconds(42.0));
        NearlyEqual(20.5 + (1.0 / 3.0), after.BeatToSeconds(42.0));
        NearlyEqual(1.0, chart.Notes[1].HoldBeats.GetValueOrDefault());
        NearlyEqual(2.0, chart.Notes[2].HoldBeats.GetValueOrDefault());
    }

    [Fact]
    public void LeadInBeatsDoesNotMoveAudioBeatMapping()
    {
        Chart chart = new()
        {
            BPM = 120,
            Offset = 1.0,
            LeadInBeats = 2.0
        };

        ChartTempoMap tempoMap = new(chart);

        NearlyEqual(1.0, tempoMap.BeatToSeconds(0.0));
        NearlyEqual(-2.0, tempoMap.SecondsToBeat(0.0));
    }

    [Fact]
    public void SeeSawCompilerUsesLeadInForInitialApplejackPrep()
    {
        Chart chart = new()
        {
            BPM = 120,
            Offset = 1.0,
            LeadInBeats = 4.0,
            Notes = new List<ChartNote>
            {
                new()
                {
                    BeatPosition = 0.0,
                    SongPosition = 1.0,
                    AdditionnalData = new Dictionary<string, string>(SeeSawAction.TowardOuter.ToAdditionnalData())
                }
            }
        };

        ChartTempoMap tempoMap = new(chart);

        Assert.NotEmpty(SeeSawChartCompiler.Compile(chart.Notes, note => note.BeatPosition.GetValueOrDefault(), tempoMap).Errors);
        Assert.Empty(SeeSawChartCompiler.Compile(chart.Notes, note => note.BeatPosition.GetValueOrDefault(), tempoMap, chart.LeadInBeats).Errors);
    }

    [Fact]
    public void LeadInAllowsNonSeeSawNotesBeforeAudioStart()
    {
        BeatmapEditorDocument document = BeatmapEditorDocument.CreateNew("", "lead-in-test.xml", 120);
        document.SetOffset(1.0);
        document.SetLeadInBeats(4.0);

        EditorNoteDefinition definition = EditorNoteDefinitions.Get(SeaPonyParadeNoteEditor.TypeId);

        Assert.True(document.TryPlaceNoteAtBeat(definition, -3.0, 0, out ChartNote placedNote, out string reason), reason);
        NearlyEqual(-3.0, placedNote.BeatPosition.GetValueOrDefault());
        NearlyEqual(-0.5, placedNote.SongPosition);
    }

    private static Chart CreateBeatFirstChart()
    {
        return new Chart
        {
            ChartVersion = 2,
            BPM = 120,
            Offset = 1.0,
            Effects = new List<ChartEffect>
            {
                CreateBpmEffect(songPosition: 3.0, beatPosition: 4.0, bpm: 60),
                CreateBpmEffect(songPosition: 7.0, beatPosition: 8.0, bpm: 180)
            }
        };
    }

    private static ChartEffect CreateBpmEffect(double songPosition, double? beatPosition, double bpm)
    {
        ChartEffect effect = new()
        {
            SongPosition = songPosition,
            BeatPosition = beatPosition,
            EffectType = ChartEffect.BpmChangeEffectType
        };
        effect.SetBpm(bpm);
        return effect;
    }

    private static ChartNote CreateNote(double beat, double holdBeats)
    {
        return new ChartNote
        {
            BeatPosition = beat,
            HoldBeats = holdBeats,
            SongPosition = beat * 0.5,
            HoldDuration = holdBeats * 0.5,
            InputActionToPress = "ReactMain"
        };
    }

    private static void NearlyEqual(double expected, double actual, double epsilon = 0.000001)
    {
        Assert.InRange(actual, expected - epsilon, expected + epsilon);
    }
}
