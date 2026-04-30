using System;
using System.Collections.Generic;
using System.Linq;
using Rhythm.Note;

namespace MLP_RiM.Elements.Editor;

public enum EditorNoteKind
{
    RhythmInput,
    SeeSawTowardOuter,
    SeeSawTowardInner
}

public sealed class EditorNoteDefinition
{
    public EditorNoteKind Kind { get; }
    public string DisplayName { get; }
    public string InputAction { get; }
    public double HoldBeats { get; }
    public double OccupyBeforeBeats { get; }
    public double OccupyAfterBeats { get; }
    public double HitWindowBeforeBeats { get; }
    public double HitWindowAfterBeats { get; }
    public IReadOnlyDictionary<string, string> AdditionnalData { get; }

    public EditorNoteDefinition(EditorNoteKind kind, string displayName, string inputAction, double holdBeats, double occupyBeforeBeats, double occupyAfterBeats, double hitWindowBeforeBeats, double hitWindowAfterBeats, IReadOnlyDictionary<string, string> additionnalData)
    {
        Kind = kind;
        DisplayName = displayName;
        InputAction = inputAction;
        HoldBeats = holdBeats;
        OccupyBeforeBeats = occupyBeforeBeats;
        OccupyAfterBeats = occupyAfterBeats;
        HitWindowBeforeBeats = hitWindowBeforeBeats;
        HitWindowAfterBeats = hitWindowAfterBeats;
        AdditionnalData = additionnalData;
    }

    public ChartNote CreateChartNote(double songPosition, double crotchet)
    {
        return new ChartNote
        {
            SongPosition = songPosition,
            HoldDuration = HoldBeats * crotchet,
            InputActionToPress = InputAction,
            AdditionnalData = AdditionnalData.ToDictionary(pair => pair.Key, pair => pair.Value)
        };
    }

    public bool Occupies(double noteSongPosition, double crotchet, double testedSongPosition)
    {
        return testedSongPosition >= GetStart(noteSongPosition, crotchet) && testedSongPosition <= GetEnd(noteSongPosition, crotchet);
    }

    public double GetStart(double noteSongPosition, double crotchet)
    {
        return noteSongPosition - OccupyBeforeBeats * crotchet;
    }

    public double GetEnd(double noteSongPosition, double crotchet)
    {
        return noteSongPosition + Math.Max(HoldBeats, OccupyAfterBeats) * crotchet;
    }

    public double GetHitWindowStart(double noteSongPosition, double crotchet)
    {
        return noteSongPosition - HitWindowBeforeBeats * crotchet;
    }

    public double GetHitWindowEnd(double noteSongPosition, double crotchet)
    {
        return noteSongPosition + Math.Max(HoldBeats, HitWindowAfterBeats) * crotchet;
    }
}

public static class EditorNoteDefinitions
{
    public static readonly EditorNoteDefinition RhythmInput = new(
        EditorNoteKind.RhythmInput,
        "Rhythm Input",
        "ReactMain",
        0,
        2,
        0.25,
        0,
        0.25,
        new Dictionary<string, string>());

    public static readonly EditorNoteDefinition SeeSawTowardOuter = new(
        EditorNoteKind.SeeSawTowardOuter,
        "See Saw Outer",
        "ReactMain",
        0,
        2,
        2,
        0,
        2,
        new Dictionary<string, string> { ["action"] = "see_saw_toward_outer" });

    public static readonly EditorNoteDefinition SeeSawTowardInner = new(
        EditorNoteKind.SeeSawTowardInner,
        "See Saw Inner",
        "ReactMain",
        0,
        1,
        1,
        0,
        1,
        new Dictionary<string, string> { ["action"] = "see_saw_toward_inner" });

    public static readonly IReadOnlyList<EditorNoteDefinition> All = new[]
    {
        RhythmInput,
        SeeSawTowardOuter,
        SeeSawTowardInner
    };

    public static EditorNoteDefinition Get(EditorNoteKind kind)
    {
        return All.First(definition => definition.Kind == kind);
    }

    public static EditorNoteDefinition FromChartNote(ChartNote note)
    {
        if (note.AdditionnalData != null && note.AdditionnalData.TryGetValue("action", out string action))
        {
            if (action == "see_saw_toward_outer")
                return SeeSawTowardOuter;

            if (action == "see_saw_toward_inner")
                return SeeSawTowardInner;
        }

        return RhythmInput;
    }
}
