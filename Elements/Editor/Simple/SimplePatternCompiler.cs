using System;
using System.Collections.Generic;
using System.Linq;
using Rhythm.Note;

namespace MLP_RiM.Elements.Editor;

/// <summary>
/// Contexte transmis au compiler custom d'un clip simple.
/// </summary>
/// <typeparam name="TAction">Enum qui liste les actions runtime du rhythm game.</typeparam>
public sealed class SimpleClipCompileContext<TAction>
    where TAction : struct, Enum
{
    internal SimpleClipCompileContext(ChartEditorClip clip, EditorClipDefinition clipDefinition, NoteAuthoringIntent intent, ChartTempoMap tempoMap, IReadOnlyList<ChartNote> existingNotes, IReadOnlyDictionary<string, string> data, TAction action, string inputAction, PlacementOptions placementOptions)
    {
        Clip = clip;
        ClipDefinition = clipDefinition;
        Intent = intent;
        TempoMap = tempoMap;
        ExistingNotes = existingNotes ?? Array.Empty<ChartNote>();
        Data = data ?? new Dictionary<string, string>();
        Action = action;
        InputAction = inputAction;
        PlacementOptions = placementOptions ?? PlacementOptions.None;
    }

    /// <summary>
    /// Clip auteur en cours de compilation.
    /// </summary>
    public ChartEditorClip Clip { get; }

    /// <summary>
    /// Definition editeur du clip en cours de compilation.
    /// </summary>
    public EditorClipDefinition ClipDefinition { get; }

    /// <summary>
    /// Intention d'auteur normalisee pour le compiler de pattern.
    /// </summary>
    public NoteAuthoringIntent Intent { get; }

    /// <summary>
    /// Tempo map disponible pendant la compilation de clip, ou <c>null</c> pendant certains placements directs.
    /// </summary>
    public ChartTempoMap TempoMap { get; }

    /// <summary>
    /// Notes deja presentes dans la chart, utiles pour les patterns dependants du voisinage.
    /// </summary>
    public IReadOnlyList<ChartNote> ExistingNotes { get; }

    /// <summary>
    /// Donnees legacy finales du clip, incluant defaults et overrides.
    /// </summary>
    public IReadOnlyDictionary<string, string> Data { get; }

    /// <summary>
    /// Action runtime principale du clip.
    /// </summary>
    public TAction Action { get; }

    /// <summary>
    /// Action d'input a ecrire sur les notes runtime generees.
    /// </summary>
    public string InputAction { get; }

    /// <summary>
    /// Options de repetition fournies par le flux de placement editeur.
    /// </summary>
    public PlacementOptions PlacementOptions { get; }

    /// <summary>
    /// Beat runtime de depart du pattern, apres application du lead-in si necessaire.
    /// </summary>
    public double StartBeat => Intent.StartBeat;

    /// <summary>
    /// Longueur positive du pattern en beats.
    /// </summary>
    public double LengthBeats => Intent.LengthBeats;
}

/// <summary>
/// Emetteur mis a disposition d'un compiler custom de clip simple.
/// </summary>
/// <typeparam name="TAction">Enum qui liste les actions runtime du rhythm game.</typeparam>
public sealed class SimpleRuntimeNoteEmitter<TAction>
    where TAction : struct, Enum
{
    private readonly RhythmGameDefinition<TAction> _game;
    private readonly SimpleClipCompileContext<TAction> _context;
    private readonly List<RuntimeNoteDraft> _drafts = new();

    internal SimpleRuntimeNoteEmitter(RhythmGameDefinition<TAction> game, SimpleClipCompileContext<TAction> context)
    {
        _game = game;
        _context = context;
    }

    /// <summary>
    /// Brouillons de notes runtime emis par le compiler custom.
    /// </summary>
    public IReadOnlyList<RuntimeNoteDraft> Drafts => _drafts;

    /// <summary>
    /// Emet une note runtime avec l'action principale du clip.
    /// </summary>
    /// <param name="offsetBeats">Offset en beats depuis le debut runtime du pattern.</param>
    /// <param name="holdBeats">Duree tenue de la note en beats.</param>
    public void Emit(double offsetBeats = 0.0, double holdBeats = 0.0)
    {
        Emit(_context.Action, offsetBeats, holdBeats);
    }

    /// <summary>
    /// Emet une note runtime avec une action explicite.
    /// </summary>
    /// <param name="action">Action runtime a ecrire dans la note.</param>
    /// <param name="offsetBeats">Offset en beats depuis le debut runtime du pattern.</param>
    /// <param name="holdBeats">Duree tenue de la note en beats.</param>
    public void Emit(TAction action, double offsetBeats = 0.0, double holdBeats = 0.0)
    {
        _drafts.Add(new RuntimeNoteDraft(
            _context.StartBeat + offsetBeats,
            _game.CreatePayload(action, _context.Data),
            Math.Max(0.0, holdBeats),
            _context.InputAction));
    }
}

internal sealed class SimplePatternCompiler<TAction>
    where TAction : struct, Enum
{
    private const double InclusiveEndEpsilonBeats = 0.000001;

    private readonly RhythmGameDefinition<TAction> _game;

    public SimplePatternCompiler(RhythmGameDefinition<TAction> game)
    {
        _game = game;
    }

    public IReadOnlyList<RuntimeNoteDraft> Compile(SimpleClipDefinition<TAction> definition, ChartEditorClip clip, IReadOnlyDictionary<string, string> data, TAction action, NoteCompileContext context, PlacementOptions placementOptions, bool applyLeadIn)
    {
        if (definition == null || clip == null || !definition.IsRuntime)
            return Array.Empty<RuntimeNoteDraft>();

        double length = Math.Max(0.0, placementOptions?.RepeatDurationBeats ?? clip.LengthBeats);
        double startBeat = clip.StartBeat + (applyLeadIn ? definition.LeadInBeats : 0.0);
        string inputAction = string.IsNullOrWhiteSpace(clip.InputAction) ? definition.InputAction : clip.InputAction;
        EnumNotePayload<TAction> payload = _game.CreatePayload(action, data);
        NoteAuthoringIntent intent = new(_game.RhythmGameId, definition.VariantId, startBeat, length, payload, placementOptions ?? PlacementOptions.None);
        SimpleClipCompileContext<TAction> simpleContext = new(clip, definition.EditorClip, intent, context?.TempoMap, context?.ExistingNotes, data, action, inputAction, placementOptions);

        if (definition.CustomCompiler != null)
        {
            SimpleRuntimeNoteEmitter<TAction> emitter = new(_game, simpleContext);
            definition.CustomCompiler(simpleContext, emitter);
            return emitter.Drafts.ToArray();
        }

        List<RuntimeNoteDraft> notes = CompileStandard(definition, simpleContext, payload);
        AddPadding(definition, notes, action, simpleContext, payload);
        return notes;
    }

    private static List<RuntimeNoteDraft> CompileStandard(SimpleClipDefinition<TAction> definition, SimpleClipCompileContext<TAction> context, EnumNotePayload<TAction> payload)
    {
        IReadOnlyList<SimpleClipEmit> emits = definition.Emits.Count > 0
            ? definition.Emits
            : new[] { new SimpleClipEmit(0.0, 0.0, hasExplicitHoldBeats: false) };
        double? repeatStep = GetRepeatStep(definition, context.PlacementOptions);
        double length = Math.Max(0.0, context.LengthBeats);
        List<RuntimeNoteDraft> notes = new();

        if (repeatStep.HasValue && length > InclusiveEndEpsilonBeats)
        {
            for (double repeatOffset = 0.0; repeatOffset <= length + InclusiveEndEpsilonBeats; repeatOffset += repeatStep.Value)
                AddPatternEmits(definition, context, payload, emits, notes, repeatOffset, length, filterByLength: true);
        }
        else
        {
            AddPatternEmits(definition, context, payload, emits, notes, repeatOffset: 0.0, length, filterByLength: length > InclusiveEndEpsilonBeats);
        }

        return notes;
    }

    private static void AddPatternEmits(SimpleClipDefinition<TAction> definition, SimpleClipCompileContext<TAction> context, EnumNotePayload<TAction> payload, IReadOnlyList<SimpleClipEmit> emits, List<RuntimeNoteDraft> notes, double repeatOffset, double length, bool filterByLength)
    {
        foreach (SimpleClipEmit emit in emits)
        {
            double relativeBeat = repeatOffset + emit.OffsetBeats;
            if (filterByLength && relativeBeat > length + InclusiveEndEpsilonBeats)
                continue;

            double holdBeats = emit.HasExplicitHoldBeats
                ? emit.HoldBeats
                : definition.HoldForClipLength ? length : emit.HoldBeats;
            notes.Add(new RuntimeNoteDraft(context.StartBeat + relativeBeat, payload, holdBeats, context.InputAction));
        }
    }

    private void AddPadding(SimpleClipDefinition<TAction> definition, List<RuntimeNoteDraft> notes, TAction action, SimpleClipCompileContext<TAction> context, EnumNotePayload<TAction> payload)
    {
        if (definition.PadToMultiple <= 1 || notes.Count == 0)
            return;

        double stepBeats = GetRepeatStep(definition, context.PlacementOptions) ?? 1.0;
        if (stepBeats <= 0.0 || double.IsNaN(stepBeats) || double.IsInfinity(stepBeats))
            stepBeats = 1.0;

        SeriesInfo series = GetCombinedSeries(notes, action, context.ExistingNotes, context.TempoMap);
        int padding = (definition.PadToMultiple - series.Count % definition.PadToMultiple) % definition.PadToMultiple;
        for (int i = 1; i <= padding; i++)
            notes.Add(new RuntimeNoteDraft(series.LastBeat + i * stepBeats, payload, 0.0, context.InputAction));
    }

    private SeriesInfo GetCombinedSeries(IReadOnlyList<RuntimeNoteDraft> generatedNotes, TAction action, IReadOnlyList<ChartNote> existingNotes, ChartTempoMap tempoMap)
    {
        List<SeriesEntry> entries = new();
        int order = 0;
        foreach (ChartNote note in existingNotes ?? Array.Empty<ChartNote>())
        {
            if (note != null)
                entries.Add(new SeriesEntry(ChartTiming.GetNoteBeat(note, tempoMap), isGenerated: false, isSameAction: _game.Codec.IsAction(note.AdditionnalData, action), order++));
        }

        foreach (RuntimeNoteDraft note in generatedNotes)
            entries.Add(new SeriesEntry(note.Beat, isGenerated: true, isSameAction: true, order++));

        entries.Sort(CompareSeriesEntries);
        int anchorIndex = entries.FindLastIndex(entry => entry.IsGenerated);
        if (anchorIndex < 0)
            return new SeriesInfo(generatedNotes.Count, generatedNotes[generatedNotes.Count - 1].Beat);

        int startIndex = anchorIndex;
        while (startIndex > 0 && entries[startIndex - 1].IsSameAction)
            startIndex--;

        int endIndex = anchorIndex;
        while (endIndex + 1 < entries.Count && entries[endIndex + 1].IsSameAction)
            endIndex++;

        return new SeriesInfo(endIndex - startIndex + 1, entries[endIndex].Beat);
    }

    private static double? GetRepeatStep(SimpleClipDefinition<TAction> definition, PlacementOptions placementOptions)
    {
        double? step = placementOptions?.HasRepeat == true
            ? placementOptions.RepeatStepBeats ?? definition.RepeatEveryBeats ?? IntervalEditorNoteProvider.DefaultStepBeats
            : definition.RepeatEveryBeats;
        return step.HasValue && step.Value > 0.0 ? step.Value : null;
    }

    private static int CompareSeriesEntries(SeriesEntry a, SeriesEntry b)
    {
        int byBeat = a.Beat.CompareTo(b.Beat);
        if (byBeat != 0)
            return byBeat;

        int byGenerated = a.IsGenerated.CompareTo(b.IsGenerated);
        return byGenerated != 0 ? byGenerated : a.Order.CompareTo(b.Order);
    }

    private readonly struct SeriesEntry
    {
        public SeriesEntry(double beat, bool isGenerated, bool isSameAction, int order)
        {
            Beat = beat;
            IsGenerated = isGenerated;
            IsSameAction = isSameAction;
            Order = order;
        }

        public double Beat { get; }
        public bool IsGenerated { get; }
        public bool IsSameAction { get; }
        public int Order { get; }
    }

    private readonly struct SeriesInfo
    {
        public SeriesInfo(int count, double lastBeat)
        {
            Count = count;
            LastBeat = lastBeat;
        }

        public int Count { get; }
        public double LastBeat { get; }
    }
}
