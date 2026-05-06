using System;
using System.Collections.Generic;
using System.Linq;
using MLP_RiM.Elements.DevUI;
using Rhythm.Note;

namespace MLP_RiM.Elements.Editor;

public sealed class SeeSawEditorNoteOptionsPanel : IEditorNoteOptionsPanel
{
    public string Title => "SEE SAW";

    public IReadOnlyList<DevUiWindowRow> BuildRows(EditorNoteOptionsContext context)
    {
        IReadOnlyList<SeeSawPatternKind?> patternOptions = GetPatternOptions();
        SeeSawCompiledEventTiming timing = GetCurrentTiming(context);
        SeeSawLogicalState state = GetCurrentState(context);
        List<DevUiWindowRow> rows = new()
        {
            DevUiWindowRow.Category("PATTERN"),
            DevUiWindowRow.Title($"CURRENT: {GetCurrentPatternName(context, timing)}"),
            DevUiWindowRow.Dropdown(
                "see_saw_pattern",
                "PATTERN",
                patternOptions.Select(GetPatternName).ToArray(),
                GetPatternIndex(context, patternOptions, timing),
                index => SetPattern(context, patternOptions[index])),
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
                DevUiWindowRow.Checkbox("APPLEJACK", SeeSawAction.GetBigLeapApplejack(context.Note.AdditionnalData), () => ToggleBigLeapApplejack(context.GetCurrentNote())),
                DevUiWindowRow.Checkbox("RAINBOW DASH", SeeSawAction.GetBigLeapRainbowDash(context.Note.AdditionnalData), () => ToggleBigLeapRainbowDash(context.GetCurrentNote()))
            });
        }

        return rows;
    }

    private static IReadOnlyList<SeeSawPatternKind?> GetPatternOptions()
    {
        return new SeeSawPatternKind?[]
        {
            SeeSawPatternKind.LongLong,
            SeeSawPatternKind.ShortShort,
            SeeSawPatternKind.ShortLong,
            SeeSawPatternKind.LongShort,
            null
        };
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

    private static int GetPatternIndex(EditorNoteOptionsContext context, IReadOnlyList<SeeSawPatternKind?> options, SeeSawCompiledEventTiming timing)
    {
        SeeSawAction action = SeeSawAction.FromAdditionnalData(context.Note.AdditionnalData);
        SeeSawPatternKind? currentPattern;
        if (SeeSawAction.TryGetPattern(context.Note.AdditionnalData, out SeeSawPatternKind storedPattern))
        {
            currentPattern = storedPattern;
        }
        else
        {
            currentPattern = SeeSawAction.GetBaseDirection(action.Direction) == SeeSawDirection.Exit || timing.IsExit
                ? null
                : timing.Pattern;
        }

        for (int i = 0; i < options.Count; i++)
        {
            if (options[i] == currentPattern)
                return i;
        }

        return 0;
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

    private static void SetPattern(EditorNoteOptionsContext context, SeeSawPatternKind? pattern)
    {
        ChartNote note = context.GetCurrentNote();
        Dictionary<string, string> data = GetData(note);

        if (pattern == null)
        {
            SeeSawAction.SetDirection(data, SeeSawDirection.Exit);
            SeeSawAction.ClearPattern(data);
            SeeSawAction.SetBigLeapApplejack(data, false);
            SeeSawAction.SetBigLeapRainbowDash(data, false);
            note.AdditionnalData = data;
            return;
        }

        SeeSawAction.SetPattern(data, pattern.Value);
        note.AdditionnalData = data;
    }

    private static void ToggleBigLeapApplejack(ChartNote note)
    {
        Dictionary<string, string> data = GetData(note);
        SeeSawAction.ToggleBigLeapApplejack(data);
        note.AdditionnalData = data;
    }

    private static void ToggleBigLeapRainbowDash(ChartNote note)
    {
        Dictionary<string, string> data = GetData(note);
        SeeSawAction.ToggleBigLeapRainbowDash(data);
        note.AdditionnalData = data;
    }

    private static Dictionary<string, string> GetData(ChartNote note)
    {
        return note.AdditionnalData ?? new Dictionary<string, string>();
    }
}
