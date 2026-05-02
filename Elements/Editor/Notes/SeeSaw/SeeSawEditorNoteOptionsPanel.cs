using System.Collections.Generic;
using MLP_RiM.Elements.DevUI;
using Rhythm.Note;

namespace MLP_RiM.Elements.Editor;

public sealed class SeeSawEditorNoteOptionsPanel : IEditorNoteOptionsPanel
{
    public string Title => "SEE SAW";

    public IReadOnlyList<DevUiWindowRow> BuildRows(EditorNoteOptionsContext context)
    {
        return new[]
        {
            DevUiWindowRow.Category("DIRECTION"),
            DevUiWindowRow.Dropdown(
                "TARGET",
                GetDirectionNames(),
                GetDirectionIndex(context),
                index => SetDirection(context.GetCurrentNote(), GetDirection(index))),
            DevUiWindowRow.Category("BIG LEAP"),
            DevUiWindowRow.Checkbox("APPLEJACK", SeeSawAction.GetBigLeapApplejack(context.Note.AdditionnalData), () => ToggleBigLeapApplejack(context.GetCurrentNote())),
            DevUiWindowRow.Checkbox("RAINBOW DASH", SeeSawAction.GetBigLeapRainbowDash(context.Note.AdditionnalData), () => ToggleBigLeapRainbowDash(context.GetCurrentNote()))
        };
    }

    private static IReadOnlyList<string> GetDirectionNames()
    {
        return new[] { "Outer", "Inner", "Opposite" };
    }

    private static int GetDirectionIndex(EditorNoteOptionsContext context)
    {
        return SeeSawAction.GetBaseDirection(SeeSawAction.FromAdditionnalData(context.Note.AdditionnalData).Direction) switch
        {
            SeeSawDirection.Inner => 1,
            SeeSawDirection.Opposite => 2,
            _ => 0
        };
    }

    private static SeeSawDirection GetDirection(int index)
    {
        return index switch
        {
            1 => SeeSawDirection.Inner,
            2 => SeeSawDirection.Opposite,
            _ => SeeSawDirection.Outer
        };
    }

    private static void SetDirection(ChartNote note, SeeSawDirection direction)
    {
        Dictionary<string, string> data = note.AdditionnalData;
        SeeSawAction.SetDirection(data, direction);
        note.AdditionnalData = data;
    }

    private static void ToggleBigLeapApplejack(ChartNote note)
    {
        Dictionary<string, string> data = note.AdditionnalData;
        SeeSawAction.ToggleBigLeapApplejack(data);
        note.AdditionnalData = data;
    }

    private static void ToggleBigLeapRainbowDash(ChartNote note)
    {
        Dictionary<string, string> data = note.AdditionnalData;
        SeeSawAction.ToggleBigLeapRainbowDash(data);
        note.AdditionnalData = data;
    }
}
