using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using MLP_RiM.Elements.DevUI;
using Rhythm.Note;

namespace MLP_RiM.Elements.Editor;

public enum EditorEffectKind
{
    BpmChange
}

public sealed class EditorEffectDefinition
{
    private readonly Func<ChartEffect, bool> _matchesChartEffect;
    private readonly Func<double, BeatmapEditorDocument, ChartEffect> _createChartEffect;

    public EditorEffectDefinition(EditorEffectKind kind, string displayName, Func<ChartEffect, bool> matchesChartEffect, Func<double, BeatmapEditorDocument, ChartEffect> createChartEffect)
    {
        Kind = kind;
        DisplayName = displayName;
        _matchesChartEffect = matchesChartEffect;
        _createChartEffect = createChartEffect;
    }

    public EditorEffectKind Kind { get; }
    public string DisplayName { get; }

    public bool Matches(ChartEffect effect)
    {
        return _matchesChartEffect(effect);
    }

    public ChartEffect CreateChartEffect(double songPosition, BeatmapEditorDocument document)
    {
        return _createChartEffect(Math.Max(0, songPosition), document);
    }
}

public sealed class EditorEffectOptionsContext
{
    public EditorEffectOptionsContext(ChartEffect effect, BeatmapEditorDocument document, Rectangle bounds, Func<ChartEffect> getCurrentEffect = null)
    {
        Effect = effect;
        Document = document;
        Bounds = bounds;
        GetCurrentEffect = getCurrentEffect ?? (() => Effect);
    }

    public ChartEffect Effect { get; }
    public BeatmapEditorDocument Document { get; }
    public Rectangle Bounds { get; }
    public Func<ChartEffect> GetCurrentEffect { get; }
}

public interface IEditorEffectOptionsPanel
{
    string Title { get; }
    IReadOnlyList<DevUiWindowRow> BuildRows(EditorEffectOptionsContext context);
}

public static class EditorEffectDefinitions
{
    public static readonly EditorEffectDefinition BpmChange = new(
        EditorEffectKind.BpmChange,
        "BPM Change",
        effect => effect?.IsBpmChange == true,
        CreateBpmChangeEffect);

    public static readonly IReadOnlyList<EditorEffectDefinition> All = new[]
    {
        BpmChange
    };

    private static readonly IReadOnlyDictionary<EditorEffectKind, IEditorEffectOptionsPanel> OptionsPanels = new Dictionary<EditorEffectKind, IEditorEffectOptionsPanel>
    {
        [EditorEffectKind.BpmChange] = new BpmChangeEditorEffectOptionsPanel()
    };

    public static EditorEffectDefinition Get(EditorEffectKind kind)
    {
        return All.First(definition => definition.Kind == kind);
    }

    public static EditorEffectDefinition FromChartEffect(ChartEffect effect)
    {
        if (effect == null)
            return null;

        return All.FirstOrDefault(definition => definition.Matches(effect));
    }

    public static bool TryGetOptionsPanel(EditorEffectKind kind, out IEditorEffectOptionsPanel panel)
    {
        return OptionsPanels.TryGetValue(kind, out panel);
    }

    private static ChartEffect CreateBpmChangeEffect(double songPosition, BeatmapEditorDocument document)
    {
        double placedSongPosition = SnapBpmChangeToNearestBeat(songPosition, document);

        ChartEffect effect = new()
        {
            SongPosition = placedSongPosition,
            EffectType = ChartEffect.BpmChangeEffectType
        };

        effect.SetBpm(document?.GetBpmAt(placedSongPosition) ?? 100);
        effect.SetSectionOffset(0);
        return effect;
    }

    private static double SnapBpmChangeToNearestBeat(double songPosition, BeatmapEditorDocument document)
    {
        double placedSongPosition = songPosition;
        if (document != null)
        {
            double beat = document.GetBeatAt(songPosition);
            if (!double.IsNaN(beat) && !double.IsInfinity(beat))
            {
                double snappedBeat = Math.Round(beat, MidpointRounding.AwayFromZero);
                placedSongPosition = document.GetSongPositionAtBeat(snappedBeat);
            }
        }

        return Math.Max(0, placedSongPosition);
    }
}

public sealed class BpmChangeEditorEffectOptionsPanel : IEditorEffectOptionsPanel
{
    public string Title => "BPM CHANGE";

    public IReadOnlyList<DevUiWindowRow> BuildRows(EditorEffectOptionsContext context)
    {
        if (context == null)
            return Array.Empty<DevUiWindowRow>();

        ChartEffect effect = context.GetCurrentEffect();
        if (effect == null)
            return new[] { DevUiWindowRow.Title("Effect unavailable") };

        double bpm = GetBpm(effect, context.Document?.GetBpmAt(effect.SongPosition) ?? 100);
        double sectionOffset = effect.GetSectionOffsetOrDefault(0);
        double globalBeat = context.Document?.GetBeatAt(effect.SongPosition) ?? double.NaN;
        double offGridBeat = double.IsNaN(globalBeat) || double.IsInfinity(globalBeat)
            ? double.NaN
            : globalBeat - Math.Round(globalBeat, MidpointRounding.AwayFromZero);

        return new[]
        {
            DevUiWindowRow.Title($"Time: {effect.SongPosition:0.000}s"),
            DevUiWindowRow.Title(double.IsNaN(globalBeat) || double.IsInfinity(globalBeat) ? "Global beat: ?" : $"Global beat: {globalBeat:0.######}"),
            DevUiWindowRow.Title(double.IsNaN(offGridBeat) || double.IsInfinity(offGridBeat) ? "Off grid: ?" : $"Off grid: {FormatSignedBeat(offGridBeat)}b"),
            DevUiWindowRow.FloatInput("effect_bpm", "BPM", bpm, value => SetBpm(context.GetCurrentEffect(), value)),
            DevUiWindowRow.FloatInput("effect_section_offset", "FIRST BEAT +", sectionOffset, value => SetSectionOffset(context.GetCurrentEffect(), value)),
            DevUiWindowRow.Button("SNAP TO GLOBAL BEAT", () => SnapToNearestGlobalBeat(context.GetCurrentEffect(), context.Document))
        };
    }

    private static string FormatSignedBeat(double value)
    {
        return value >= 0 ? $"+{value:0.######}" : value.ToString("0.######");
    }

    private static double GetBpm(ChartEffect effect, double fallback)
    {
        return effect != null && effect.TryGetBpm(out double bpm) ? bpm : fallback;
    }

    private static void SetBpm(ChartEffect effect, double bpm)
    {
        effect?.SetBpm(bpm);
    }

    private static void SetSectionOffset(ChartEffect effect, double sectionOffset)
    {
        effect?.SetSectionOffset(sectionOffset);
    }

    private static void SnapToNearestGlobalBeat(ChartEffect effect, BeatmapEditorDocument document)
    {
        if (effect == null || document == null)
            return;

        double beat = document.GetBeatAt(effect.SongPosition);
        if (double.IsNaN(beat) || double.IsInfinity(beat))
            return;

        double snappedBeat = Math.Round(beat, MidpointRounding.AwayFromZero);
        effect.SongPosition = Math.Max(0, document.GetSongPositionAtBeat(snappedBeat));
        effect.SetSectionOffset(0);
    }
}
