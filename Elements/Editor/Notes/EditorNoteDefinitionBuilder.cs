using System;
using System.Collections.Generic;
using Rhythm.Note;

namespace MLP_RiM.Elements.Editor;

public sealed class EditorNoteDefinitionBuilder
{
    private readonly EditorNoteKind _kind;
    private readonly string _displayName;
    private string _inputAction = "ReactMain";
    private double _holdBeats;
    private double _occupyBeforeBeats;
    private double _occupyAfterBeats;
    private double _hitWindowBeforeBeats;
    private double _hitWindowAfterBeats;
    private readonly List<EditorNoteVariant> _variants = new();
    private IEditorNoteTiming _timing = new FixedEditorNoteTiming();
    private Func<ChartNote, bool> _matchesChartNote = _ => false;

    public EditorNoteDefinitionBuilder(EditorNoteKind kind, string displayName)
    {
        _kind = kind;
        _displayName = displayName;
    }

    public EditorNoteDefinitionBuilder InputAction(string inputAction)
    {
        _inputAction = inputAction;
        return this;
    }

    public EditorNoteDefinitionBuilder Hold(double beats)
    {
        _holdBeats = beats;
        return this;
    }

    public EditorNoteDefinitionBuilder Occupies(double beforeBeats, double afterBeats)
    {
        _occupyBeforeBeats = beforeBeats;
        _occupyAfterBeats = afterBeats;
        return this;
    }

    public EditorNoteDefinitionBuilder HitWindow(double beforeBeats, double afterBeats)
    {
        _hitWindowBeforeBeats = beforeBeats;
        _hitWindowAfterBeats = afterBeats;
        return this;
    }

    public EditorNoteDefinitionBuilder Variant(string displayName)
    {
        return Variant(displayName, new Dictionary<string, string>());
    }

    public EditorNoteDefinitionBuilder Variant(string displayName, IReadOnlyDictionary<string, string> additionnalData)
    {
        _variants.Add(new EditorNoteVariant(displayName, additionnalData));
        return this;
    }

    public EditorNoteDefinitionBuilder Timing(IEditorNoteTiming timing)
    {
        _timing = timing;
        return this;
    }

    public EditorNoteDefinitionBuilder Matches(Func<ChartNote, bool> matchesChartNote)
    {
        _matchesChartNote = matchesChartNote;
        return this;
    }

    public EditorNoteDefinition Build()
    {
        IReadOnlyList<EditorNoteVariant> variants = _variants.Count > 0
            ? _variants.ToArray()
            : new[] { new EditorNoteVariant("Default", new Dictionary<string, string>()) };

        return new EditorNoteDefinition(_kind, _displayName, _inputAction, _holdBeats, _occupyBeforeBeats, _occupyAfterBeats, _hitWindowBeforeBeats, _hitWindowAfterBeats, variants, _timing, _matchesChartNote);
    }
}
