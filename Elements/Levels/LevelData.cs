using System;
using System.Collections.Generic;
using System.Xml.Serialization;
using Microsoft.Xna.Framework;

namespace MLP_RiM.Elements.Levels;

[XmlRoot("Level")]
public sealed class LevelData
{
    [XmlAttribute]
    public string Id { get; set; } = string.Empty;

    public string DisplayName { get; set; } = "New Level";
    public bool LockedByDefault { get; set; }

    [XmlArray("UnlockLevelIds")]
    [XmlArrayItem("LevelId")]
    public List<string> UnlockLevelIds { get; set; } = new();

    public string StartNodeId { get; set; } = string.Empty;

    [XmlArray("Nodes")]
    [XmlArrayItem("Node")]
    public List<LevelNodeData> Nodes { get; set; } = new();

    [XmlArray("Connections")]
    [XmlArrayItem("Connection")]
    public List<LevelConnectionData> Connections { get; set; } = new();
}

public sealed class LevelNodeData
{
    [XmlAttribute]
    public string Id { get; set; } = string.Empty;

    [XmlAttribute]
    public LevelNodeKind Kind { get; set; }

    [XmlAttribute]
    public int X { get; set; }

    [XmlAttribute]
    public int Y { get; set; }

    public LevelSpeaker Speaker { get; set; } = LevelSpeaker.TwilightSparkle;
    public string Text { get; set; } = string.Empty;
    public string ChartPath { get; set; } = string.Empty;
    public string MiniGameId { get; set; } = string.Empty;
    public int RequiredSuccessCount { get; set; } = 1;
}

public sealed class LevelConnectionData
{
    [XmlAttribute]
    public string FromNodeId { get; set; } = string.Empty;

    [XmlAttribute]
    public string FromPort { get; set; } = string.Empty;

    [XmlAttribute]
    public string ToNodeId { get; set; } = string.Empty;
}

public enum LevelNodeKind
{
    Start,
    Dialogue,
    TrainingBeatmap,
    PlayRepresentationBeatmap,
    SetMiniGame,
    End
}

public enum LevelSpeaker
{
    TwilightSparkle,
    Applejack,
    RainbowDash,
    Rarity,
    Fluttershy,
    PinkiePie,
    AppleBloom,
    Scootaloo,
    SweetieBelle,
    Derpy
}

public static class LevelSpeakerInfo
{
    public static readonly IReadOnlyList<LevelSpeaker> All = Enum.GetValues<LevelSpeaker>();

    public static string GetDisplayName(LevelSpeaker speaker)
    {
        return speaker switch
        {
            LevelSpeaker.TwilightSparkle => "Twilight Sparkle",
            LevelSpeaker.RainbowDash => "Rainbow Dash",
            LevelSpeaker.PinkiePie => "Pinkie Pie",
            LevelSpeaker.AppleBloom => "Apple Bloom",
            LevelSpeaker.SweetieBelle => "Sweetie Belle",
            _ => speaker.ToString()
        };
    }

    public static Color GetTextboxColor(LevelSpeaker speaker)
    {
        return speaker switch
        {
            LevelSpeaker.TwilightSparkle => new Color(118, 82, 196),
            LevelSpeaker.Applejack => new Color(225, 139, 50),
            LevelSpeaker.RainbowDash => new Color(58, 164, 223),
            LevelSpeaker.Rarity => new Color(188, 92, 205),
            LevelSpeaker.Fluttershy => new Color(236, 190, 86),
            LevelSpeaker.PinkiePie => new Color(232, 91, 164),
            LevelSpeaker.AppleBloom => new Color(215, 64, 75),
            LevelSpeaker.Scootaloo => new Color(226, 112, 54),
            LevelSpeaker.SweetieBelle => new Color(202, 144, 225),
            LevelSpeaker.Derpy => new Color(150, 158, 185),
            _ => new Color(80, 92, 128)
        };
    }
}
