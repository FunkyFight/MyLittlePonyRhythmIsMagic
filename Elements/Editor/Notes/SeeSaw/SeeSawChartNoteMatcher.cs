using Rhythm.Note;

namespace MLP_RiM.Elements.Editor;

public static class SeeSawChartNoteMatcher
{
    public static bool Matches(ChartNote note)
    {
        var data = note?.AdditionnalData;
        if (data != null
            && data.TryGetValue(NotePayloadKeys.Game, out string gameId)
            && gameId != SeeSawAction.GameId)
            return false;

        return data != null
            && ((data.TryGetValue(SeeSawAction.DataKey, out string action)
                    && SeeSawAction.TryParse(action, out _))
                || SeeSawAction.TryGetPattern(data, out _));
    }
}
