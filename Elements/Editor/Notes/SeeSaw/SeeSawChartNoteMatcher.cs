using Rhythm.Note;

namespace MLP_RiM.Elements.Editor;

public static class SeeSawChartNoteMatcher
{
    public static bool Matches(ChartNote note)
    {
        var data = note?.AdditionnalData;
        return data != null
            && ((data.TryGetValue("action", out string action)
                    && SeeSawAction.TryParse(action, out _))
                || SeeSawAction.TryGetPattern(data, out _));
    }
}
