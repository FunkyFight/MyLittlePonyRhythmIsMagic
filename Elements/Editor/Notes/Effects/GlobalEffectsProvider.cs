using System;
using System.Collections.Generic;
using GameCore.Scenes;
using Microsoft.Xna.Framework;
using Rhythm.Note;

namespace MLP_RiM.Elements.Editor;

public sealed class GlobalEffectsProvider : IEditorNoteProvider
{
    public const string GameId = "effects";
    public const string BlackAndWhiteToggleClipId = "effects.black_and_white_toggle";
    public const string ViewportClipId = "effects.viewport";
    public const string FlashClipId = "effects.flash";
    public const string SaturationClipId = "effects.saturation";
    public const string SaturationValueKey = "saturation";
    public const string ViewportOffsetXKey = "offset_x";
    public const string ViewportOffsetYKey = "offset_y";
    public const string ViewportRotationDegreesKey = "rotation_degrees";
    public const string ViewportZoomXKey = "zoom_x";
    public const string ViewportZoomYKey = "zoom_y";
    public const string ViewportInstantKey = "instant";
    public const string ViewportInterpolationKey = "interpolation";
    public const string ViewportInterpolationLinear = "linear";
    public const string ViewportInterpolationEaseInCubic = "ease_in_cubic";
    public const string ViewportInterpolationEaseOutCubic = "ease_out_cubic";
    public const string ViewportInterpolationEaseInOutCubic = "ease_in_out_cubic";

    private static readonly IReadOnlyList<EditorClipFieldDefinition> ViewportFields = new[]
    {
        EditorClipFieldDefinition.Float(ViewportOffsetXKey, "Camera Offset X", 0, minValue: -2000, maxValue: 2000),
        EditorClipFieldDefinition.Float(ViewportOffsetYKey, "Camera Offset Y", 0, minValue: -2000, maxValue: 2000),
        EditorClipFieldDefinition.Float(ViewportRotationDegreesKey, "Camera Rotation", 0, minValue: -360, maxValue: 360),
        EditorClipFieldDefinition.Float(ViewportZoomXKey, "Camera Zoom X", 1, minValue: 0.1, maxValue: 5),
        EditorClipFieldDefinition.Float(ViewportZoomYKey, "Camera Zoom Y", 1, minValue: 0.1, maxValue: 5),
        EditorClipFieldDefinition.Bool(ViewportInstantKey, "Instant", false),
        EditorClipFieldDefinition.Enum(ViewportInterpolationKey, "Interpolation", ViewportInterpolationEaseInOutCubic, CreateInterpolationOptions())
    };

    private static readonly IReadOnlyList<EditorClipFieldDefinition> SaturationFields = new[]
    {
        EditorClipFieldDefinition.Float(SaturationValueKey, "Saturation", 1, minValue: 0, maxValue: 5)
    };

    private static readonly EditorNoteDefinition EffectsDefinition = new EditorNoteDefinitionBuilder(new NoteTypeId(GameId, "marker"), "Effects")
        .InputAction(string.Empty)
        .Matches(_ => false)
        .Build();

    private static readonly IReadOnlyList<EditorClipDefinition> EffectClips = new[]
    {
        new EditorClipDefinition(
            GameId,
            BlackAndWhiteToggleClipId,
            "Camera Effect - Black And White",
            EditorClipCategory.Instant,
            0,
            string.Empty,
            new Dictionary<string, string>
            {
                [EditorClipDefinitions.SwitchGameEventKey] = EditorClipDefinitions.BlackAndWhiteToggleEventValue
            },
            editorStyle: new EditorVisualStyle(Color.White)),
        new EditorClipDefinition(
            GameId,
            ViewportClipId,
            "Viewport",
            EditorClipCategory.Continuous,
            1.0,
            string.Empty,
            new Dictionary<string, string>
            {
                [EditorClipDefinitions.SwitchGameEventKey] = EditorClipDefinitions.ViewportOffsetEventValue,
                [ViewportOffsetXKey] = "0",
                [ViewportOffsetYKey] = "0",
                [ViewportRotationDegreesKey] = "0",
                [ViewportZoomXKey] = "1",
                [ViewportZoomYKey] = "1",
                [ViewportInstantKey] = "false",
                [ViewportInterpolationKey] = ViewportInterpolationEaseInOutCubic
            },
            ViewportFields,
            new EditorVisualStyle(Color.CornflowerBlue)),
        new EditorClipDefinition(
            GameId,
            FlashClipId,
            "Flash",
            EditorClipCategory.Continuous,
            0.5,
            string.Empty,
            new Dictionary<string, string>
            {
                [EditorClipDefinitions.SwitchGameEventKey] = EditorClipDefinitions.FlashEventValue
            },
            editorStyle: new EditorVisualStyle(Color.White)),
        new EditorClipDefinition(
            GameId,
            SaturationClipId,
            "Camera Effect - Saturation",
            EditorClipCategory.Instant,
            0,
            string.Empty,
            new Dictionary<string, string>
            {
                [EditorClipDefinitions.SwitchGameEventKey] = EditorClipDefinitions.SaturationEventValue,
                [SaturationValueKey] = "1"
            },
            SaturationFields,
            new EditorVisualStyle(Color.LightSkyBlue))
    };

    public int SortOrder => int.MinValue;

    public string RhythmGameId => GameId;

    public string RhythmGameDisplayName => "Effects";

    public EditorNoteDefinition Definition => EffectsDefinition;

    public IReadOnlyList<EditorClipDefinition> Clips => EffectClips;

    public IEditorNoteOptionsPanel OptionsPanel => null;

    public Scene CreateScene() => null;

    public IReadOnlyList<ChartNote> CompileClip(ChartEditorClip clip, ChartTempoMap tempoMap)
    {
        return Array.Empty<ChartNote>();
    }

    public string GetClipTypeIdFromLegacyNote(ChartNote note)
    {
        return EditorClipDefinitions.NoHit;
    }

    public int GetNoteVariantIndex(ChartNote note)
    {
        return 0;
    }

    public EditorVisualStyle GetEditorStyle(ChartNote note)
    {
        return EditorVisualStyle.Default;
    }

    public IReadOnlyDictionary<string, object> CreateTimingContext(Chart chart, ChartTempoMap tempoMap)
    {
        return new Dictionary<string, object>();
    }

    public bool TryValidateNotes(EditorNoteValidationContext context, out string reason)
    {
        reason = null;
        return true;
    }

    public bool AllowsBoundaryTouch(EditorNoteDefinition otherDefinition)
    {
        return false;
    }

    private static IReadOnlyList<EditorClipFieldOption> CreateInterpolationOptions()
    {
        return new[]
        {
            new EditorClipFieldOption(ViewportInterpolationLinear, "Linear"),
            new EditorClipFieldOption("ease_in_sine", "Ease In Sine"),
            new EditorClipFieldOption("ease_out_sine", "Ease Out Sine"),
            new EditorClipFieldOption("ease_in_out_sine", "Ease In Out Sine"),
            new EditorClipFieldOption("ease_in_quad", "Ease In Quad"),
            new EditorClipFieldOption("ease_out_quad", "Ease Out Quad"),
            new EditorClipFieldOption("ease_in_out_quad", "Ease In Out Quad"),
            new EditorClipFieldOption(ViewportInterpolationEaseInCubic, "Ease In Cubic"),
            new EditorClipFieldOption(ViewportInterpolationEaseOutCubic, "Ease Out Cubic"),
            new EditorClipFieldOption(ViewportInterpolationEaseInOutCubic, "Ease In Out Cubic"),
            new EditorClipFieldOption("ease_in_quart", "Ease In Quart"),
            new EditorClipFieldOption("ease_out_quart", "Ease Out Quart"),
            new EditorClipFieldOption("ease_in_out_quart", "Ease In Out Quart"),
            new EditorClipFieldOption("ease_in_quint", "Ease In Quint"),
            new EditorClipFieldOption("ease_out_quint", "Ease Out Quint"),
            new EditorClipFieldOption("ease_in_out_quint", "Ease In Out Quint"),
            new EditorClipFieldOption("ease_in_expo", "Ease In Expo"),
            new EditorClipFieldOption("ease_out_expo", "Ease Out Expo"),
            new EditorClipFieldOption("ease_in_out_expo", "Ease In Out Expo"),
            new EditorClipFieldOption("ease_in_circ", "Ease In Circ"),
            new EditorClipFieldOption("ease_out_circ", "Ease Out Circ"),
            new EditorClipFieldOption("ease_in_out_circ", "Ease In Out Circ"),
            new EditorClipFieldOption("ease_in_back", "Ease In Back"),
            new EditorClipFieldOption("ease_out_back", "Ease Out Back"),
            new EditorClipFieldOption("ease_in_out_back", "Ease In Out Back"),
            new EditorClipFieldOption("ease_in_elastic", "Ease In Elastic"),
            new EditorClipFieldOption("ease_out_elastic", "Ease Out Elastic"),
            new EditorClipFieldOption("ease_in_out_elastic", "Ease In Out Elastic"),
            new EditorClipFieldOption("ease_in_bounce", "Ease In Bounce"),
            new EditorClipFieldOption("ease_out_bounce", "Ease Out Bounce"),
            new EditorClipFieldOption("ease_in_out_bounce", "Ease In Out Bounce"),
            new EditorClipFieldOption("smooth_step", "Smooth Step"),
            new EditorClipFieldOption("smoother_step", "Smoother Step")
        };
    }
}
