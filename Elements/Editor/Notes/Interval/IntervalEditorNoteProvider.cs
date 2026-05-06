using System;
using System.Collections.Generic;
using System.Globalization;
using Rhythm.Note;

namespace MLP_RiM.Elements.Editor;

public abstract class IntervalEditorNoteProvider : EditorNoteProvider
{
    public const string DurationBeatsKey = "interval_duration_beats";
    public const string StepBeatsKey = "interval_step_beats";

    public const double DefaultDurationBeats = 4.0;
    public const double DefaultStepBeats = 1.0;
    private const double InclusiveEndEpsilonSeconds = 0.000001;

    protected IntervalEditorNoteProvider(EditorNoteKind kind, string displayName, string inputAction)
    {
        Definition = new EditorNoteDefinitionBuilder(kind, displayName)
            .InputAction(inputAction)
            .Matches(_ => false)
            .Placement(new IntervalEditorNotePlacementStrategy(this))
            .Variant("Default")
            .Build();
    }

    public override EditorNoteDefinition Definition { get; }

    public override IEditorNoteOptionsPanel OptionsPanel { get; } = new IntervalEditorNoteOptionsPanel();

    protected virtual EditorNoteDefinition HitDefinition => RhythmInputEditorNote.DefinitionInstance;

    internal IReadOnlyList<EditorNotePlacement> CreateIntervalPlacements(ChartNote sourceConfigNote, double crotchet)
    {
        if (sourceConfigNote == null || crotchet <= 0)
            return Array.Empty<EditorNotePlacement>();

        double start = sourceConfigNote.SongPosition;
        double durationBeats = Math.Max(0, GetDurationBeats(sourceConfigNote.AdditionnalData));
        double stepBeats = Math.Max(0.000001, GetStepBeats(sourceConfigNote.AdditionnalData));
        double end = start + durationBeats * crotchet;
        double stepSeconds = stepBeats * crotchet;

        List<EditorNotePlacement> placements = new();
        for (double time = start; time <= end + InclusiveEndEpsilonSeconds; time += stepSeconds)
        {
            ChartNote hitNote = CreateHitNote(time, crotchet, sourceConfigNote);
            placements.Add(new EditorNotePlacement(HitDefinition, hitNote));
        }

        return placements;
    }

    protected virtual ChartNote CreateHitNote(double songPosition, double crotchet, ChartNote sourceConfigNote)
    {
        return new ChartNote
        {
            SongPosition = songPosition,
            HoldDuration = 0,
            InputActionToPress = HitDefinition.InputAction,
            AdditionnalData = new Dictionary<string, string>()
        };
    }

    public static double GetDurationBeats(Dictionary<string, string> data)
    {
        return GetBeats(data, DurationBeatsKey, DefaultDurationBeats);
    }

    public static double GetStepBeats(Dictionary<string, string> data)
    {
        return Math.Max(0.000001, GetBeats(data, StepBeatsKey, DefaultStepBeats));
    }

    public static void SetDurationBeats(ChartNote note, double beats)
    {
        SetBeats(note, DurationBeatsKey, Math.Max(0, beats));
    }

    public static void SetStepBeats(ChartNote note, double beats)
    {
        SetBeats(note, StepBeatsKey, Math.Max(0.000001, beats));
    }

    public static int GetHitCount(ChartNote note)
    {
        double durationBeats = Math.Max(0, GetDurationBeats(note?.AdditionnalData));
        double stepBeats = GetStepBeats(note?.AdditionnalData);
        return (int)Math.Floor(durationBeats / stepBeats + 0.000001) + 1;
    }

    private static double GetBeats(Dictionary<string, string> data, string key, double defaultValue)
    {
        if (data != null
            && data.TryGetValue(key, out string value)
            && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double beats)
            && !double.IsNaN(beats)
            && !double.IsInfinity(beats))
        {
            return beats;
        }

        return defaultValue;
    }

    private static void SetBeats(ChartNote note, string key, double beats)
    {
        note.AdditionnalData ??= new Dictionary<string, string>();
        note.AdditionnalData[key] = beats.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private sealed class IntervalEditorNotePlacementStrategy : IEditorNotePlacementStrategy
    {
        private readonly IntervalEditorNoteProvider _provider;

        public IntervalEditorNotePlacementStrategy(IntervalEditorNoteProvider provider)
        {
            _provider = provider;
        }

        public IReadOnlyList<EditorNotePlacement> CreatePlacements(EditorNoteDefinition definition, ChartNote sourceNote, EditorNotePlacementContext context)
        {
            return _provider.CreateIntervalPlacements(sourceNote, context?.Crotchet ?? 0);
        }
    }
}
