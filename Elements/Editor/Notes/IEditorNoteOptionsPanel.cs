using System.Collections.Generic;
using System;
using Microsoft.Xna.Framework;
using MLP_RiM.Elements.DevUI;
using Rhythm.Note;

namespace MLP_RiM.Elements.Editor;

public sealed class EditorNoteOptionsContext
{
    public EditorNoteOptionsContext(ChartNote note, BeatmapEditorDocument document, Rectangle bounds, Func<ChartNote> getCurrentNote = null, EditorNoteDefinition definition = null, Action<NotePatch> applyPatch = null, PlacementOptions placementOptions = null, Action<PlacementOptions> applyPlacementOptions = null)
    {
        Note = note;
        Document = document;
        Bounds = bounds;
        GetCurrentNote = getCurrentNote ?? (() => Note);
        Definition = definition;
        _applyPatch = applyPatch;
        PlacementOptions = placementOptions ?? PlacementOptions.None;
        _applyPlacementOptions = applyPlacementOptions;
    }

    private readonly Action<NotePatch> _applyPatch;
    private readonly Action<PlacementOptions> _applyPlacementOptions;

    public ChartNote Note { get; }
    public BeatmapEditorDocument Document { get; }
    public Rectangle Bounds { get; }
    public Func<ChartNote> GetCurrentNote { get; }
    public EditorNoteDefinition Definition { get; }
    public PlacementOptions PlacementOptions { get; }

    public void ApplyPatch(NotePatch patch)
    {
        if (patch == null)
            return;

        if (_applyPatch != null)
        {
            _applyPatch.Invoke(patch);
            return;
        }

        patch.ApplyTo(GetCurrentNote());
    }

    public void ApplyPlacementOptions(PlacementOptions placementOptions)
    {
        _applyPlacementOptions?.Invoke(placementOptions ?? PlacementOptions.None);
    }
}

public sealed class NotePatch
{
    private readonly Dictionary<string, string> _setData = new();
    private readonly HashSet<string> _removeData = new();
    private Dictionary<string, string> _replaceData;

    public static NotePatch ReplaceAdditionnalData(IReadOnlyDictionary<string, string> data)
    {
        NotePatch patch = new();
        patch._replaceData = data != null
            ? new Dictionary<string, string>(data)
            : new Dictionary<string, string>();
        return patch;
    }

    public NotePatch SetData(string key, string value)
    {
        if (string.IsNullOrEmpty(key))
            return this;

        _setData[key] = value ?? string.Empty;
        _removeData.Remove(key);
        return this;
    }

    public NotePatch RemoveData(string key)
    {
        if (string.IsNullOrEmpty(key))
            return this;

        _setData.Remove(key);
        _removeData.Add(key);
        return this;
    }

    public void ApplyTo(ChartNote note)
    {
        if (note == null)
            return;

        Dictionary<string, string> data = _replaceData != null
            ? new Dictionary<string, string>(_replaceData)
            : new Dictionary<string, string>(note.AdditionnalData ?? new Dictionary<string, string>());

        foreach (string key in _removeData)
            data.Remove(key);

        foreach (KeyValuePair<string, string> pair in _setData)
            data[pair.Key] = pair.Value;

        note.AdditionnalData = data;
    }
}

public interface IEditorNoteOptionsPanel
{
    string Title { get; }
    IReadOnlyList<DevUiWindowRow> BuildRows(EditorNoteOptionsContext context);
}
