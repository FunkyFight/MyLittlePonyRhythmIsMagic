using Rhythm.Note;

namespace MLP_RiM.Elements.Editor;

public static class SeeSawChartNoteMatcher
{
    public static bool Matches(ChartNote note)
    {
        return note?.AdditionnalData != null
            && note.AdditionnalData.TryGetValue("action", out string action)
            && SeeSawAction.TryParse(action, out _);
    }
}
