using System.Collections.Generic;

namespace MLP_RiM.Elements.Editor;

public readonly struct SeeSawAction
{
    private const string DataKey = "action";

    public static readonly SeeSawAction TowardOuter = new("see_saw_toward_outer", SeeSawDirection.Outer, isBigLeap: false);
    public static readonly SeeSawAction TowardInner = new("see_saw_toward_inner", SeeSawDirection.Inner, isBigLeap: false);
    public static readonly SeeSawAction TowardOpposite = new("see_saw_toward_opposite", SeeSawDirection.Opposite, isBigLeap: false);
    public static readonly SeeSawAction TowardOuterBigLeap = new("see_saw_toward_outer_big_leap", SeeSawDirection.Outer, isBigLeap: true);
    public static readonly SeeSawAction TowardInnerBigLeap = new("see_saw_toward_inner_big_leap", SeeSawDirection.Inner, isBigLeap: true);
    public static readonly SeeSawAction TowardOppositeBigLeap = new("see_saw_toward_opposite_big_leap", SeeSawDirection.Opposite, isBigLeap: true);

    private static readonly IReadOnlyList<SeeSawAction> KnownActions = new[]
    {
        TowardOuter,
        TowardInner,
        TowardOpposite,
        TowardOuterBigLeap,
        TowardInnerBigLeap,
        TowardOppositeBigLeap
    };

    public string Value { get; }
    public SeeSawDirection Direction { get; }
    public bool IsBigLeap { get; }

    private SeeSawAction(string value, SeeSawDirection direction, bool isBigLeap)
    {
        Value = value;
        Direction = direction;
        IsBigLeap = isBigLeap;
    }

    public static SeeSawAction FromVariant(EditorNoteVariant variant)
    {
        if (variant.AdditionnalData.TryGetValue(DataKey, out string value) && TryParse(value, out SeeSawAction action))
            return action;

        return TowardOuter;
    }

    public static bool TryParse(string value, out SeeSawAction action)
    {
        foreach (SeeSawAction knownAction in KnownActions)
        {
            if (knownAction.Value == value)
            {
                action = knownAction;
                return true;
            }
        }

        action = default;
        return false;
    }

    public IReadOnlyDictionary<string, string> ToAdditionnalData()
    {
        return new Dictionary<string, string> { [DataKey] = Value };
    }

    public SeeSawEditorState Apply(SeeSawEditorState state)
    {
        return Direction switch
        {
            SeeSawDirection.Outer => new SeeSawEditorState(rainbowIsOuter: true, applejackIsOuter: true),
            SeeSawDirection.Inner => new SeeSawEditorState(rainbowIsOuter: false, applejackIsOuter: false),
            SeeSawDirection.Opposite => new SeeSawEditorState(rainbowIsOuter: !state.ApplejackIsOuter, applejackIsOuter: state.ApplejackIsOuter),
            _ => state
        };
    }

    public bool TargetsOuterAfterHit(bool rainbowTargetsOuter)
    {
        return Direction == SeeSawDirection.Outer || (Direction == SeeSawDirection.Opposite && rainbowTargetsOuter);
    }
}
