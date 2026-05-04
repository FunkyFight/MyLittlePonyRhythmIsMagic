using System.Collections.Generic;
using MLP_RiM.Elements.DevUI;
using Rhythm.Note;

namespace MLP_RiM.Elements.Editor;

public sealed class IntervalEditorNoteOptionsPanel : IEditorNoteOptionsPanel
{
    public string Title => "INTERVAL";

    public IReadOnlyList<DevUiWindowRow> BuildRows(EditorNoteOptionsContext context)
    {
        ChartNote note = context.GetCurrentNote();
        double durationBeats = IntervalEditorNoteProvider.GetDurationBeats(note.AdditionnalData);
        double stepBeats = IntervalEditorNoteProvider.GetStepBeats(note.AdditionnalData);
        int hitCount = IntervalEditorNoteProvider.GetHitCount(note);

        return new[]
        {
            DevUiWindowRow.Title($"Generates {hitCount} hits"),
            DevUiWindowRow.Category("RANGE"),
            DevUiWindowRow.FloatInput(
                "interval_duration_beats",
                "DURATION BEATS",
                durationBeats,
                value => IntervalEditorNoteProvider.SetDurationBeats(context.GetCurrentNote(), value)),
            DevUiWindowRow.FloatInput(
                "interval_step_beats",
                "INTERVAL BEATS",
                stepBeats,
                value => IntervalEditorNoteProvider.SetStepBeats(context.GetCurrentNote(), value))
        };
    }
}
