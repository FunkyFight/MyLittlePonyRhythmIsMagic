using System;
using System.Collections.Generic;
using MLP_RiM.Elements.DevUI;

namespace MLP_RiM.Elements.Editor;

public sealed class SeeSawEditorNoteOptionsPanel : IEditorNoteOptionsPanel
{
    public string Title => "SEE SAW";

    public IReadOnlyList<DevUiWindowRow> BuildRows(EditorNoteOptionsContext context)
    {
        SeeSawCompiledEventTiming timing = GetCurrentTiming(context);
        SeeSawLogicalState state = GetCurrentState(context);
        List<DevUiWindowRow> rows = new()
        {
            DevUiWindowRow.Category("AUTHOR CLIP"),
            DevUiWindowRow.Title($"Pattern: {GetCurrentPatternName(context, timing)}"),
            DevUiWindowRow.Title("Change pattern by editing the See Saw clip type"),
            DevUiWindowRow.Category("STATE / TIMING"),
            DevUiWindowRow.Title($"Rainbow starts: {state.RainbowSide}"),
            DevUiWindowRow.Title($"Applejack starts: {state.ApplejackSide} / available {FormatBeat(state.ApplejackAvailableBeat)}"),
            DevUiWindowRow.Title($"Prep: {FormatPrep(timing)}"),
            DevUiWindowRow.Title($"Cue: {FormatBeat(timing.CueBeat)}"),
            DevUiWindowRow.Title($"Hit: {FormatBeat(timing.PlayerHitBeat)}"),
            DevUiWindowRow.Title($"End: {FormatBeat(timing.EndBeat)}"),
            DevUiWindowRow.Title($"AJ: {FormatApplejackPrep(state, timing)}"),
            DevUiWindowRow.Title($"RD: {FormatPhaseLength(timing.LaunchSide)} ({timing.LaunchSide} -> {timing.TargetSide})"),
            DevUiWindowRow.Title($"After: {FormatAfterHit(timing)}")
        };

        if (!timing.IsValid)
            rows.Add(DevUiWindowRow.Title($"INVALID: {timing.InvalidReason}"));

        if (!timing.IsExit)
        {
            rows.AddRange(new[]
            {
                DevUiWindowRow.Category("VISUAL STYLE"),
                DevUiWindowRow.Checkbox("APPLEJACK", SeeSawAction.GetBigLeapApplejack(context.Note.AdditionnalData), () => ToggleBigLeapApplejack(context)),
                DevUiWindowRow.Checkbox("RAINBOW DASH", SeeSawAction.GetBigLeapRainbowDash(context.Note.AdditionnalData), () => ToggleBigLeapRainbowDash(context))
            });
        }

        return rows;
    }

    private static string GetPatternName(SeeSawPatternKind? pattern)
    {
        return pattern switch
        {
            SeeSawPatternKind.LongLong => "LongLong",
            SeeSawPatternKind.LongShort => "LongShort",
            SeeSawPatternKind.ShortLong => "ShortLong",
            SeeSawPatternKind.ShortShort => "ShortShort",
            null => "Exit",
            _ => "Unknown"
        };
    }

    private static string GetCurrentPatternName(EditorNoteOptionsContext context, SeeSawCompiledEventTiming timing)
    {
        if (SeeSawAction.TryGetPattern(context.Note.AdditionnalData, out SeeSawPatternKind storedPattern))
            return GetPatternName(storedPattern);

        SeeSawAction action = SeeSawAction.FromAdditionnalData(context.Note.AdditionnalData);
        if (SeeSawAction.GetBaseDirection(action.Direction) == SeeSawDirection.Exit || timing.IsExit)
            return "Exit";

        return GetPatternName(timing.Pattern);
    }

    private static SeeSawCompiledEventTiming GetCurrentTiming(EditorNoteOptionsContext context)
    {
        double leadInBeats = ChartTiming.GetLeadInBeats(context.Document.Chart);
        SeeSawCompiledEventTiming timing = SeeSawChartCompiler.GetTimingForChartNote(context.Document.Chart.Notes, context.Note, context.Document.GetBeatAt, leadInBeats);
        if (timing.IsSeeSaw)
            return timing;

        return SeeSawChartCompiler.GetPreviewTiming(context.Document.Chart.Notes, context.Note.AdditionnalData, context.Note.SongPosition, context.Document.GetBeatAt, leadInBeats);
    }

    private static SeeSawLogicalState GetCurrentState(EditorNoteOptionsContext context)
    {
        return SeeSawChartCompiler.GetStateBefore(context.Document.Chart.Notes, context.Note.SongPosition, context.Document.GetBeatAt, ChartTiming.GetLeadInBeats(context.Document.Chart));
    }

    private static string FormatBeat(double beat)
    {
        return $"{beat:0.###}b";
    }

    private static string FormatPrep(SeeSawCompiledEventTiming timing)
    {
        string prep = FormatBeat(timing.PrepStartBeat);
        if (Math.Abs(timing.PrepStartBeat - timing.IdealPrepStartBeat) <= 0.0005)
            return prep;

        return $"{prep} (ideal {FormatBeat(timing.IdealPrepStartBeat)})";
    }

    private static string FormatPhaseLength(SeeSawSide side)
    {
        if (side == SeeSawSide.Exit)
            return "Exit";

        return SeeSawTiming.GetJumpBeatsFromSide(side) >= SeeSawTiming.LongJumpBeats
            ? "Long"
            : "Short";
    }

    private static string FormatApplejackPrep(SeeSawLogicalState state, SeeSawCompiledEventTiming timing)
    {
        if (timing.IsExit)
            return $"Exit ({state.ApplejackSide} -> Exit)";

        bool chainedFromPrevious = Math.Abs(timing.PrepStartBeat - timing.CueBeat) <= 0.0005
            && timing.IdealPrepStartBeat < timing.CueBeat;

        if (chainedFromPrevious)
            return $"Previous relay -> {timing.ApplejackCueSide}";

        return $"{FormatPhaseLength(state.ApplejackSide)} ({state.ApplejackSide} -> {timing.ApplejackCueSide})";
    }

    private static string FormatAfterHit(SeeSawCompiledEventTiming timing)
    {
        if (timing.IsExit)
            return "None";

        if (timing.ApplejackTargetSide == SeeSawSide.Exit && timing.EndBeat > timing.PlayerHitBeat)
            return $"AJ exit ({timing.ApplejackCueSide} -> Exit)";

        return "Next note AJ";
    }

    private static void ToggleBigLeapApplejack(EditorNoteOptionsContext context)
    {
        Dictionary<string, string> data = GetData(context);
        SeeSawAction.ToggleBigLeapApplejack(data);
        context.ApplyPatch(NotePatch.ReplaceAdditionnalData(data));
    }

    private static void ToggleBigLeapRainbowDash(EditorNoteOptionsContext context)
    {
        Dictionary<string, string> data = GetData(context);
        SeeSawAction.ToggleBigLeapRainbowDash(data);
        context.ApplyPatch(NotePatch.ReplaceAdditionnalData(data));
    }

    private static Dictionary<string, string> GetData(EditorNoteOptionsContext context)
    {
        return new Dictionary<string, string>(context.GetCurrentNote()?.AdditionnalData ?? new Dictionary<string, string>());
    }
}
