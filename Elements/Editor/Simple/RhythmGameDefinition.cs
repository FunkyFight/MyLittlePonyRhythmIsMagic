using System;
using System.Collections.Generic;
using System.Linq;
using GameCore.Scenes;
using Rhythm.Note;

namespace MLP_RiM.Elements.Editor;

/// <summary>
/// Definition construite par la facade simple apres execution du builder declaratif.
/// </summary>
/// <typeparam name="TAction">Enum qui liste les actions runtime du rhythm game.</typeparam>
public sealed class RhythmGameDefinition<TAction>
    where TAction : struct, Enum
{
    private readonly IReadOnlyDictionary<TAction, int> _variantIndicesByAction;
    private readonly IReadOnlyDictionary<TAction, SimpleClipDefinition<TAction>> _clipsByAction;
    private readonly SimplePatternCompiler<TAction> _patternCompiler;
    private readonly Func<Scene> _sceneFactory;

    internal RhythmGameDefinition(RhythmGameBuilder<TAction> builder)
    {
        if (builder == null)
            throw new ArgumentNullException(nameof(builder));

        RhythmGameId = RequireId(builder.GameId, "Rhythm game id");
        RhythmGameDisplayName = string.IsNullOrWhiteSpace(builder.DisplayNameValue) ? RhythmGameId : builder.DisplayNameValue;
        SortOrder = builder.SortOrderValue;
        _sceneFactory = builder.SceneFactory;
        RuntimeNote = builder.RuntimeNoteConfiguration;
        TypeId = new NoteTypeId(RhythmGameId, RuntimeNote.NoteId);
        Codec = new EnumNoteCodec<TAction>(RhythmGameId, RuntimeNote.NoteId, builder.LegacyActionValues);

        RuntimeClips = builder.ClipConfigurations
            .Where(configuration => configuration.IsRuntime)
            .Select(configuration => new SimpleClipDefinition<TAction>(this, configuration))
            .ToArray();

        NoHitClip = builder.NoHitClipConfiguration != null
            ? new SimpleClipDefinition<TAction>(this, builder.NoHitClipConfiguration)
            : null;

        ValidateRuntimeActions(RuntimeClips);
        ValidateClipIds(RuntimeClips, NoHitClip);

        EditorClips = RuntimeClips
            .Concat(NoHitClip != null ? new[] { NoHitClip } : Array.Empty<SimpleClipDefinition<TAction>>())
            .Select(clip => clip.EditorClip)
            .ToArray();

        _variantIndicesByAction = RuntimeClips
            .Select((clip, index) => new { clip.Action, Index = index })
            .ToDictionary(item => item.Action, item => item.Index);

        _clipsByAction = RuntimeClips
            .ToDictionary(clip => clip.Action);

        EditorDefinition = CreateEditorDefinition();
        _patternCompiler = new SimplePatternCompiler<TAction>(this);
    }

    /// <summary>
    /// Ordre de tri du provider dans l'editeur.
    /// </summary>
    public int SortOrder { get; }

    /// <summary>
    /// Identifiant stable du rhythm game.
    /// </summary>
    public string RhythmGameId { get; }

    /// <summary>
    /// Nom affiche du rhythm game.
    /// </summary>
    public string RhythmGameDisplayName { get; }

    /// <summary>
    /// Identifiant extensible du type de note runtime produit par le jeu.
    /// </summary>
    public NoteTypeId TypeId { get; }

    /// <summary>
    /// Codec generique utilise pour lire et ecrire les actions enum.
    /// </summary>
    public EnumNoteCodec<TAction> Codec { get; }

    /// <summary>
    /// Definition de note exposee au noyau editeur.
    /// </summary>
    public EditorNoteDefinition EditorDefinition { get; }

    /// <summary>
    /// Clips auteur declares par le jeu, hors clip <c>Switch Game</c> automatique.
    /// </summary>
    public IReadOnlyList<EditorClipDefinition> EditorClips { get; }
    internal SimpleRuntimeNoteConfiguration RuntimeNote { get; }
    internal IReadOnlyList<SimpleClipDefinition<TAction>> RuntimeClips { get; }
    internal SimpleClipDefinition<TAction> NoHitClip { get; }

    /// <summary>
    /// Cree la scene runtime declaree par le rhythm game.
    /// </summary>
    /// <returns>Nouvelle scene runtime, ou <c>null</c> si aucune scene n'est declaree.</returns>
    public Scene CreateScene()
    {
        return _sceneFactory?.Invoke();
    }

    /// <summary>
    /// Compile un clip auteur en notes runtime avec la tempo map fournie.
    /// </summary>
    /// <param name="clip">Clip auteur a compiler.</param>
    /// <param name="editorClip">Definition editeur du clip.</param>
    /// <param name="tempoMap">Tempo map utilisee pour convertir les beats en secondes.</param>
    /// <param name="data">Donnees legacy finales du clip, incluant defaults et overrides.</param>
    /// <returns>Notes runtime generees.</returns>
    public IReadOnlyList<ChartNote> CompileClip(ChartEditorClip clip, EditorClipDefinition editorClip, ChartTempoMap tempoMap, IReadOnlyDictionary<string, string> data)
    {
        if (clip == null || editorClip == null || tempoMap == null)
            return Array.Empty<ChartNote>();

        SimpleClipDefinition<TAction> simpleClip = FindRuntimeClip(clip.ClipTypeId);
        if (simpleClip == null)
            return Array.Empty<ChartNote>();

        TAction action = Codec.TryReadAction(data, out TAction readAction) ? readAction : simpleClip.Action;
        return _patternCompiler.Compile(simpleClip, clip, data, action, new NoteCompileContext(tempoMap), PlacementOptions.None, applyLeadIn: true)
            .Select(draft => draft.ToChartNote(tempoMap))
            .ToArray();
    }

    /// <summary>
    /// Compile une creation directe de note via l'ancien flux de placement editeur.
    /// </summary>
    /// <param name="sourceNote">Note source demandee par l'editeur.</param>
    /// <param name="sourceBeat">Beat source de la note.</param>
    /// <param name="data">Donnees legacy a propager aux notes creees.</param>
    /// <param name="action">Action runtime a produire.</param>
    /// <param name="context">Contexte de compilation, notamment les notes existantes.</param>
    /// <param name="placementOptions">Options de repetition de l'editeur.</param>
    /// <returns>Brouillons de notes runtime a transformer en clones de placement.</returns>
    public IReadOnlyList<RuntimeNoteDraft> CompilePlacement(ChartNote sourceNote, double sourceBeat, IReadOnlyDictionary<string, string> data, TAction action, NoteCompileContext context, PlacementOptions placementOptions)
    {
        SimpleClipDefinition<TAction> simpleClip = FindRuntimeClip(action);
        if (sourceNote == null || simpleClip == null)
            return Array.Empty<RuntimeNoteDraft>();

        ChartEditorClip placementClip = new()
        {
            StartBeat = sourceBeat,
            LengthBeats = Math.Max(0.0, placementOptions?.RepeatDurationBeats ?? 0.0),
            RhythmGameId = RhythmGameId,
            ClipTypeId = simpleClip.ClipTypeId,
            ClipCategory = simpleClip.Category.ToString(),
            InputAction = string.IsNullOrWhiteSpace(sourceNote.InputActionToPress) ? RuntimeNote.InputAction : sourceNote.InputActionToPress,
            Data = new Dictionary<string, string>(data ?? new Dictionary<string, string>())
        };

        return _patternCompiler.Compile(simpleClip, placementClip, data, action, context, placementOptions ?? PlacementOptions.None, applyLeadIn: false);
    }

    /// <summary>
    /// Retrouve le clip auteur correspondant a une note runtime legacy.
    /// </summary>
    /// <param name="note">Note runtime legacy.</param>
    /// <returns>Identifiant de clip auteur correspondant, ou premier clip runtime disponible.</returns>
    public string GetClipTypeIdFromLegacyNote(ChartNote note)
    {
        if (Codec.TryReadAction(note?.AdditionnalData, out TAction action)
            && _clipsByAction.TryGetValue(action, out SimpleClipDefinition<TAction> clip))
            return clip.ClipTypeId;

        return RuntimeClips.FirstOrDefault()?.ClipTypeId ?? EditorClipDefinitions.NoHit;
    }

    /// <summary>
    /// Retrouve l'index de variant d'une note runtime depuis son action enum.
    /// </summary>
    /// <param name="note">Note runtime a analyser.</param>
    /// <returns>Index du variant, ou <c>0</c> par defaut.</returns>
    public int GetNoteVariantIndex(ChartNote note)
    {
        if (Codec.TryReadAction(note?.AdditionnalData, out TAction action)
            && _variantIndicesByAction.TryGetValue(action, out int index))
            return index;

        return 0;
    }

    /// <summary>
    /// Retourne le style editeur associe a une note runtime.
    /// </summary>
    /// <param name="note">Note runtime a styler.</param>
    /// <returns>Style du variant correspondant.</returns>
    public EditorVisualStyle GetEditorStyle(ChartNote note)
    {
        return EditorDefinition.GetVariant(GetNoteVariantIndex(note)).EditorStyle ?? EditorVisualStyle.Default;
    }

    internal EnumNotePayload<TAction> CreatePayload(TAction action, IReadOnlyDictionary<string, string> data = null)
    {
        return new EnumNotePayload<TAction>(Codec, action, data);
    }

    internal SimpleClipDefinition<TAction> FindRuntimeClip(TAction action)
    {
        return _clipsByAction.TryGetValue(action, out SimpleClipDefinition<TAction> clip) ? clip : null;
    }

    private SimpleClipDefinition<TAction> FindRuntimeClip(string clipTypeId)
    {
        return RuntimeClips.FirstOrDefault(clip => clip.ClipTypeId == clipTypeId);
    }

    private EditorNoteDefinition CreateEditorDefinition()
    {
        EditorNoteDefinitionBuilder builder = new EditorNoteDefinitionBuilder(TypeId, RhythmGameDisplayName)
            .InputAction(RuntimeNote.InputAction)
            .Hold(RuntimeNote.HoldBeats)
            .Occupies(RuntimeNote.OccupyBeforeBeats, RuntimeNote.OccupyAfterBeats)
            .HitWindow(RuntimeNote.HitWindowBeforeBeats, RuntimeNote.HitWindowAfterBeats)
            .Timing(RuntimeNote.Timing)
            .Matches(note => Codec.Matches(note?.AdditionnalData))
            .Placement(new SimpleEditorNotePlacementStrategy<TAction>(this));

        if (RuntimeNote.HasSameVariantHitWindow)
            builder.SameVariantHitWindow(RuntimeNote.SameVariantHitWindowBeforeBeats, RuntimeNote.SameVariantHitWindowAfterBeats);

        foreach (SimpleClipDefinition<TAction> clip in RuntimeClips)
        {
            builder.Variant(
                clip.VariantId,
                clip.DisplayName,
                CreatePayload(clip.Action),
                payload => Codec.IsPayloadAction(payload, clip.Action),
                editorStyle: clip.EditorStyle ?? EditorVisualStyle.Default);
        }

        return builder.Build();
    }

    private static string RequireId(string value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"{label} must be configured before the rhythm game definition is used.");

        return value;
    }

    private void ValidateRuntimeActions(IReadOnlyList<SimpleClipDefinition<TAction>> runtimeClips)
    {
        IGrouping<TAction, SimpleClipDefinition<TAction>> duplicateAction = runtimeClips
            .GroupBy(clip => clip.Action)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicateAction == null)
            return;

        string clipIds = string.Join(", ", duplicateAction.Select(clip => $"'{clip.ClipTypeId}'"));
        throw new InvalidOperationException($"Simple rhythm game '{RhythmGameId}' declares multiple runtime clips for action '{duplicateAction.Key}': {clipIds}. Each runtime action can only be declared once.");
    }

    private void ValidateClipIds(IReadOnlyList<SimpleClipDefinition<TAction>> runtimeClips, SimpleClipDefinition<TAction> noHitClip)
    {
        IEnumerable<SimpleClipDefinition<TAction>> clips = runtimeClips
            .Concat(noHitClip != null ? new[] { noHitClip } : Array.Empty<SimpleClipDefinition<TAction>>());
        IGrouping<string, SimpleClipDefinition<TAction>> duplicateClipId = clips
            .GroupBy(clip => clip.ClipTypeId, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicateClipId == null)
            return;

        string owners = string.Join(", ", duplicateClipId.Select(DescribeClipOwner));
        throw new InvalidOperationException($"Simple rhythm game '{RhythmGameId}' declares duplicate clip id '{duplicateClipId.Key}' for {owners}. Clip ids must be unique within a rhythm game.");
    }

    private static string DescribeClipOwner(SimpleClipDefinition<TAction> clip)
    {
        return clip.IsRuntime
            ? $"action '{clip.Action}'"
            : "No Hit clip";
    }
}

internal sealed class SimpleClipDefinition<TAction>
    where TAction : struct, Enum
{
    public SimpleClipDefinition(RhythmGameDefinition<TAction> game, SimpleClipConfiguration<TAction> configuration)
    {
        IsRuntime = configuration.IsRuntime;
        Action = configuration.Action;
        VariantId = IsRuntime ? game.Codec.GetVariantId(Action) : EditorClipDefinitions.NoHit;
        ClipTypeId = IsRuntime
            ? string.IsNullOrWhiteSpace(configuration.ClipTypeId) ? game.Codec.GetClipTypeId(Action) : configuration.ClipTypeId
            : string.IsNullOrWhiteSpace(configuration.ClipTypeId) ? EditorClipDefinitions.NoHit : configuration.ClipTypeId;
        DisplayName = string.IsNullOrWhiteSpace(configuration.DisplayName)
            ? IsRuntime ? EnumNoteCodec<TAction>.ToDisplayName(Action) : "No Hit"
            : configuration.DisplayName;
        Category = configuration.Category;
        DefaultLengthBeats = Math.Max(0.0, configuration.DefaultLengthBeats);
        InputAction = game.RuntimeNote.InputAction;
        EditorStyle = configuration.EditorStyle;
        LeadInBeats = configuration.LeadInBeats;
        Emits = configuration.Emits.ToArray();
        RepeatEveryBeats = configuration.RepeatEveryBeats;
        PadToMultiple = configuration.PadToMultiple;
        HoldForClipLength = configuration.HoldForClipLength;
        CustomCompiler = configuration.CustomCompiler;

        IReadOnlyDictionary<string, string> defaultData = IsRuntime
            ? game.CreatePayload(Action, configuration.Data).ToLegacyData()
            : new Dictionary<string, string>(configuration.Data);

        EditorClip = new EditorClipDefinition(
            game.RhythmGameId,
            ClipTypeId,
            DisplayName,
            Category,
            DefaultLengthBeats,
            InputAction,
            defaultData,
            configuration.Fields.ToArray(),
            EditorStyle);
    }

    public bool IsRuntime { get; }
    public TAction Action { get; }
    public string VariantId { get; }
    public string ClipTypeId { get; }
    public string DisplayName { get; }
    public EditorClipCategory Category { get; }
    public double DefaultLengthBeats { get; }
    public string InputAction { get; }
    public EditorVisualStyle EditorStyle { get; }
    public double LeadInBeats { get; }
    public IReadOnlyList<SimpleClipEmit> Emits { get; }
    public double? RepeatEveryBeats { get; }
    public int PadToMultiple { get; }
    public bool HoldForClipLength { get; }
    public Action<SimpleClipCompileContext<TAction>, SimpleRuntimeNoteEmitter<TAction>> CustomCompiler { get; }
    public EditorClipDefinition EditorClip { get; }
}
