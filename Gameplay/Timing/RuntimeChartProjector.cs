using System.Collections.Generic;
using MLP_RiM.Elements.Editor;
using Rhythm.Note;

public static class RuntimeChartProjector
{
    public static Chart Project(Chart source, ChartTempoMap tempoMap)
    {
        if (source == null)
            return null;

        ChartTempoMap map = tempoMap ?? new ChartTempoMap(source);
        Chart runtimeChart = new()
        {
            SongName = source.SongName,
            BeatmapName = source.BeatmapName,
            Beatmapper = source.Beatmapper,
            ArtistName = source.ArtistName,
            MusicName = source.MusicName,
            SongPath = source.SongPath,
            BPM = source.BPM,
            Offset = source.Offset,
            LeadInBeats = source.LeadInBeats,
            ChartVersion = source.ChartVersion
        };

        if (source.Notes != null)
        {
            foreach (ChartNote sourceNote in source.Notes)
            {
                EditorNoteDefinition definition = EditorNoteDefinitions.FromChartNote(sourceNote);
                double beat = ChartTiming.GetNoteBeat(sourceNote, map);
                double holdBeats = ChartTiming.GetNoteHoldBeats(sourceNote, definition, map);
                double songPosition = map.BeatToSeconds(beat);

                runtimeChart.Notes.Add(new ChartNote
                {
                    SongPosition = songPosition,
                    BeatPosition = beat,
                    HoldDuration = System.Math.Max(0.0, map.BeatToSeconds(beat + holdBeats) - songPosition),
                    HoldBeats = holdBeats,
                    InputActionToPress = sourceNote.InputActionToPress,
                    AdditionnalData = Copy(sourceNote.AdditionnalData)
                });
            }
        }

        if (source.Effects != null)
        {
            foreach (ChartEffect sourceEffect in source.Effects)
            {
                double beat = ChartTiming.GetEffectBeat(sourceEffect, map);
                runtimeChart.Effects.Add(new ChartEffect
                {
                    SongPosition = map.BeatToSeconds(beat),
                    BeatPosition = beat,
                    EffectType = sourceEffect.EffectType,
                    Data = Copy(sourceEffect.Data)
                });
            }
        }

        return runtimeChart;
    }

    private static Dictionary<string, string> Copy(Dictionary<string, string> source)
    {
        return source == null ? new Dictionary<string, string>() : new Dictionary<string, string>(source);
    }
}
