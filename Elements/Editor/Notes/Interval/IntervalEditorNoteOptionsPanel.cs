using System.Collections.Generic;
using MLP_RiM.Elements.DevUI;

namespace MLP_RiM.Elements.Editor;

public sealed class IntervalEditorNoteOptionsPanel : IEditorNoteOptionsPanel
{
    public string Title => "INTERVAL";

    public IReadOnlyList<DevUiWindowRow> BuildRows(EditorNoteOptionsContext context)
    {
        double durationBeats = System.Math.Max(0, context.PlacementOptions.RepeatDurationBeats ?? IntervalEditorNoteProvider.DefaultDurationBeats);
        double stepBeats = System.Math.Max(0.000001, context.PlacementOptions.RepeatStepBeats ?? IntervalEditorNoteProvider.DefaultStepBeats);
        int hitCount = (int)System.Math.Floor(durationBeats / stepBeats + 0.000001) + 1;

        return new[]
        {
            DevUiWindowRow.Title($"Generates {hitCount} hits"),
            DevUiWindowRow.Category("RANGE"),
            DevUiWindowRow.FloatInput(
                "interval_duration_beats",
                "DURATION BEATS",
                durationBeats,
                value => context.ApplyPlacementOptions(new PlacementOptions(
                    RepeatDurationBeats: System.Math.Max(0, value),
                    RepeatStepBeats: stepBeats))),
            DevUiWindowRow.FloatInput(
                "interval_step_beats",
                "INTERVAL BEATS",
                stepBeats,
                value => context.ApplyPlacementOptions(new PlacementOptions(
                    RepeatDurationBeats: durationBeats,
                    RepeatStepBeats: System.Math.Max(0.000001, value))))
        };
    }
}
