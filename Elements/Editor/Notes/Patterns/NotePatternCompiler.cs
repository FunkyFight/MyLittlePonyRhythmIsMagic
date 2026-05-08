using System.Collections.Generic;
using Rhythm.Note;

namespace MLP_RiM.Elements.Editor;

public sealed record NoteAuthoringIntent(string GameId, string PatternId, double StartBeat, double LengthBeats, INotePayload Payload, PlacementOptions PlacementOptions = null);

public sealed record NoteCompileContext(ChartTempoMap TempoMap, IReadOnlyList<ChartNote> ExistingNotes = null);

public sealed record RuntimeNoteDraft(double Beat, INotePayload Payload, double HoldBeats = 0.0, string InputAction = "ReactMain")
{
    public ChartNote ToChartNote(ChartTempoMap tempoMap)
    {
        double songPosition = tempoMap.BeatToSeconds(Beat);
        return new ChartNote
        {
            SongPosition = songPosition,
            BeatPosition = Beat,
            HoldDuration = 0,
            HoldBeats = HoldBeats,
            InputActionToPress = InputAction,
            AdditionnalData = Payload?.ToLegacyData() ?? new Dictionary<string, string>()
        };
    }
}

public interface INotePatternCompiler
{
    IReadOnlyList<RuntimeNoteDraft> Compile(NoteAuthoringIntent intent, NoteCompileContext context);
}
