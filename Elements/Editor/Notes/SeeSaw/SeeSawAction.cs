using System.Collections.Generic;

namespace MLP_RiM.Elements.Editor;

public enum SeeSawOppositeMode
{
    RainbowDash,
    Applejack,
    Both
}

public readonly struct SeeSawAction
{
    public const string DataKey = "action";
    private const string BigLeapRainbowDashDataKey = "big_leap_rainbow_dash";
    private const string BigLeapApplejackDataKey = "big_leap_applejack";
    private const string OppositeJumperDataKey = "opposite_jumper";
    private const string ApplejackOppositeJumperValue = "applejack";
    private const string RainbowDashOppositeJumperValue = "rainbow_dash";
    private const string BothOppositeJumperValue = "both";

    public static readonly SeeSawAction TowardOuter = new("see_saw_toward_outer", SeeSawDirection.Outer, isBigLeap: false, hasBigCounterJump: false);
    public static readonly SeeSawAction TowardInner = new("see_saw_toward_inner", SeeSawDirection.Inner, isBigLeap: false, hasBigCounterJump: false);
    public static readonly SeeSawAction TowardOpposite = new("see_saw_toward_opposite", SeeSawDirection.Opposite, isBigLeap: false, hasBigCounterJump: false);
    public static readonly SeeSawAction TowardOuterBigLeap = new("see_saw_toward_outer_big_leap", SeeSawDirection.OuterBigLeap, isBigLeap: true, hasBigCounterJump: false);
    public static readonly SeeSawAction TowardInnerBigLeap = new("see_saw_toward_inner_big_leap", SeeSawDirection.InnerBigLeap, isBigLeap: true, hasBigCounterJump: false);
    public static readonly SeeSawAction TowardOppositeBigLeap = new("see_saw_toward_opposite_big_leap", SeeSawDirection.OppositeBigLeap, isBigLeap: true, hasBigCounterJump: false);
    public static readonly SeeSawAction TowardOuterBigLeapWithBigCounter = new("see_saw_toward_outer_big_leap_counter_big_leap", SeeSawDirection.OuterBigLeap, isBigLeap: true, hasBigCounterJump: true);
    public static readonly SeeSawAction TowardInnerBigLeapWithBigCounter = new("see_saw_toward_inner_big_leap_counter_big_leap", SeeSawDirection.InnerBigLeap, isBigLeap: true, hasBigCounterJump: true);
    public static readonly SeeSawAction TowardOppositeBigLeapWithBigCounter = new("see_saw_toward_opposite_big_leap_counter_big_leap", SeeSawDirection.OppositeBigLeap, isBigLeap: true, hasBigCounterJump: true);

    private static readonly IReadOnlyList<SeeSawAction> KnownActions = new[]
    {
        TowardOuter,
        TowardInner,
        TowardOpposite,
        TowardOuterBigLeap,
        TowardInnerBigLeap,
        TowardOppositeBigLeap,
        TowardOuterBigLeapWithBigCounter,
        TowardInnerBigLeapWithBigCounter,
        TowardOppositeBigLeapWithBigCounter
    };

    public string Value { get; }
    public SeeSawDirection Direction { get; }
    public bool IsBigLeap { get; }
    public bool HasBigCounterJump { get; }
    public SeeSawOppositeMode OppositeMode { get; }
    public SeeSawJumper OppositeJumper => OppositeMode == SeeSawOppositeMode.Applejack ? SeeSawJumper.APPLEJACK : SeeSawJumper.RAINBOW_DASH;

    private SeeSawAction(string value, SeeSawDirection direction, bool isBigLeap, bool hasBigCounterJump, SeeSawOppositeMode oppositeMode = SeeSawOppositeMode.RainbowDash)
    {
        Value = value;
        Direction = direction;
        IsBigLeap = isBigLeap;
        HasBigCounterJump = hasBigCounterJump;
        OppositeMode = oppositeMode;
    }

    public static SeeSawAction FromVariant(EditorNoteVariant variant)
    {
        if (variant.AdditionnalData.TryGetValue(DataKey, out string value) && TryParse(value, out SeeSawAction action))
            return action;

        return TowardOuter;
    }

    public static SeeSawAction FromAdditionnalData(IReadOnlyDictionary<string, string> additionnalData)
    {
        if (additionnalData != null && additionnalData.TryGetValue(DataKey, out string value) && TryParse(value, out SeeSawAction action))
        {
            bool rainbowBigLeap = GetBigLeapRainbowDash(additionnalData) || action.IsBigLeap;
            bool applejackBigLeap = GetBigLeapApplejack(additionnalData) || action.HasBigCounterJump;
            return action.WithBigLeapOptions(rainbowBigLeap, applejackBigLeap).WithOppositeMode(GetOppositeMode(additionnalData));
        }

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

    public static bool GetBigLeapRainbowDash(IReadOnlyDictionary<string, string> additionnalData)
    {
        return GetBool(additionnalData, BigLeapRainbowDashDataKey);
    }

    public static bool GetBigLeapApplejack(IReadOnlyDictionary<string, string> additionnalData)
    {
        return GetBool(additionnalData, BigLeapApplejackDataKey);
    }

    public static SeeSawJumper GetOppositeJumper(IReadOnlyDictionary<string, string> additionnalData)
    {
        return GetOppositeMode(additionnalData) == SeeSawOppositeMode.Applejack
            ? SeeSawJumper.APPLEJACK
            : SeeSawJumper.RAINBOW_DASH;
    }

    public static SeeSawOppositeMode GetOppositeMode(IReadOnlyDictionary<string, string> additionnalData)
    {
        if (additionnalData != null
            && additionnalData.TryGetValue(OppositeJumperDataKey, out string value)
            && value == ApplejackOppositeJumperValue)
            return SeeSawOppositeMode.Applejack;

        if (additionnalData != null
            && additionnalData.TryGetValue(OppositeJumperDataKey, out value)
            && value == BothOppositeJumperValue)
            return SeeSawOppositeMode.Both;

        return SeeSawOppositeMode.RainbowDash;
    }

    public static SeeSawDirection GetBaseDirection(SeeSawDirection direction)
    {
        return direction switch
        {
            SeeSawDirection.OuterBigLeap => SeeSawDirection.Outer,
            SeeSawDirection.InnerBigLeap => SeeSawDirection.Inner,
            SeeSawDirection.OppositeBigLeap => SeeSawDirection.Opposite,
            _ => direction
        };
    }

    public static string GetDirectionDisplayName(SeeSawDirection direction)
    {
        return GetBaseDirection(direction) switch
        {
            SeeSawDirection.Inner => "Inner",
            SeeSawDirection.Opposite => "Opposite",
            _ => "Outer"
        };
    }

    public static void SetDirection(IDictionary<string, string> additionnalData, SeeSawDirection direction)
    {
        additionnalData[DataKey] = GetActionValue(GetBaseDirection(direction));

        if (GetBaseDirection(direction) != SeeSawDirection.Opposite)
            additionnalData.Remove(OppositeJumperDataKey);
    }

    public static void SetOppositeJumper(IDictionary<string, string> additionnalData, SeeSawJumper jumper)
    {
        SetOppositeMode(additionnalData, jumper == SeeSawJumper.APPLEJACK ? SeeSawOppositeMode.Applejack : SeeSawOppositeMode.RainbowDash);
    }

    public static void SetOppositeMode(IDictionary<string, string> additionnalData, SeeSawOppositeMode mode)
    {
        switch (mode)
        {
            case SeeSawOppositeMode.Applejack:
                additionnalData[OppositeJumperDataKey] = ApplejackOppositeJumperValue;
                break;
            case SeeSawOppositeMode.Both:
                additionnalData[OppositeJumperDataKey] = BothOppositeJumperValue;
                break;
            default:
                additionnalData.Remove(OppositeJumperDataKey);
                break;
        }
    }

    public static void ToggleBigLeapRainbowDash(IDictionary<string, string> additionnalData)
    {
        SetBool(additionnalData, BigLeapRainbowDashDataKey, !GetBigLeapRainbowDash((IReadOnlyDictionary<string, string>)additionnalData));
    }

    public static void ToggleBigLeapApplejack(IDictionary<string, string> additionnalData)
    {
        SetBool(additionnalData, BigLeapApplejackDataKey, !GetBigLeapApplejack((IReadOnlyDictionary<string, string>)additionnalData));
    }

    private SeeSawAction WithBigLeapOptions(bool rainbowBigLeap, bool applejackBigLeap)
    {
        return new SeeSawAction(Value, GetDirectionWithBigLeap(Direction, rainbowBigLeap), rainbowBigLeap, applejackBigLeap, OppositeMode);
    }

    private SeeSawAction WithOppositeMode(SeeSawOppositeMode oppositeMode)
    {
        return new SeeSawAction(Value, Direction, IsBigLeap, HasBigCounterJump, oppositeMode);
    }

    private static SeeSawDirection GetDirectionWithBigLeap(SeeSawDirection direction, bool isBigLeap)
    {
        SeeSawDirection baseDirection = GetBaseDirection(direction);

        if (!isBigLeap)
            return baseDirection;

        return baseDirection switch
        {
            SeeSawDirection.Outer => SeeSawDirection.OuterBigLeap,
            SeeSawDirection.Inner => SeeSawDirection.InnerBigLeap,
            SeeSawDirection.Opposite => SeeSawDirection.OppositeBigLeap,
            _ => baseDirection
        };
    }

    private static bool GetBool(IReadOnlyDictionary<string, string> additionnalData, string key)
    {
        return additionnalData != null
            && additionnalData.TryGetValue(key, out string value)
            && bool.TryParse(value, out bool result)
            && result;
    }

    private static void SetBool(IDictionary<string, string> additionnalData, string key, bool value)
    {
        if (value)
            additionnalData[key] = "true";
        else
            additionnalData.Remove(key);
    }

    private static string GetActionValue(SeeSawDirection direction)
    {
        return GetBaseDirection(direction) switch
        {
            SeeSawDirection.Inner => TowardInner.Value,
            SeeSawDirection.Opposite => TowardOpposite.Value,
            _ => TowardOuter.Value
        };
    }

    public SeeSawEditorState Apply(SeeSawEditorState state)
    {
        return Direction switch
        {
            SeeSawDirection.Outer or SeeSawDirection.OuterBigLeap => new SeeSawEditorState(rainbowIsOuter: true, applejackIsOuter: true),
            SeeSawDirection.Inner or SeeSawDirection.InnerBigLeap => new SeeSawEditorState(rainbowIsOuter: false, applejackIsOuter: false),
            SeeSawDirection.Opposite or SeeSawDirection.OppositeBigLeap => OppositeMode switch
            {
                SeeSawOppositeMode.Applejack => new SeeSawEditorState(rainbowIsOuter: state.RainbowIsOuter, applejackIsOuter: !state.RainbowIsOuter),
                SeeSawOppositeMode.Both => new SeeSawEditorState(rainbowIsOuter: !state.RainbowIsOuter, applejackIsOuter: !state.ApplejackIsOuter),
                _ => new SeeSawEditorState(rainbowIsOuter: !state.ApplejackIsOuter, applejackIsOuter: state.ApplejackIsOuter)
            },
            _ => state
        };
    }

    public bool TargetsOuterAfterHit(bool rainbowTargetsOuter)
    {
        return Direction == SeeSawDirection.Outer
            || Direction == SeeSawDirection.OuterBigLeap
            || ((Direction == SeeSawDirection.Opposite || Direction == SeeSawDirection.OppositeBigLeap) && rainbowTargetsOuter);
    }
}
