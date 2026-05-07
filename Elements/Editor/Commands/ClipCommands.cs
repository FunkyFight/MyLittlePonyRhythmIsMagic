using System;
using System.Collections.Generic;
using Rhythm.Note;

namespace MLP_RiM.Elements.Editor.Commands;

public sealed class CreateClipCommand : IEditorCommand
{
    private readonly ChartEditorClip _clip;

    public CreateClipCommand(ChartEditorClip clip)
    {
        _clip = EditorCommandCloning.CloneClip(clip) ?? throw new ArgumentNullException(nameof(clip));
        _clip.Id = string.IsNullOrWhiteSpace(_clip.Id) ? Guid.NewGuid().ToString("N") : _clip.Id;
    }

    public string Name => "Create Clip";

    public void Execute(BeatmapEditorDocument document)
    {
        if (!document.AddEditorClip(EditorCommandCloning.CloneClip(_clip), out string reason))
            throw new InvalidOperationException(reason);
    }

    public void Undo(BeatmapEditorDocument document)
    {
        document.RemoveEditorClip(_clip.Id);
    }
}

public sealed class DeleteClipCommand : IEditorCommand
{
    private readonly string _clipId;
    private ChartEditorClip _deletedClip;

    public DeleteClipCommand(string clipId)
    {
        _clipId = clipId;
    }

    public string Name => "Delete Clip";

    public void Execute(BeatmapEditorDocument document)
    {
        ChartEditorClip deleted = document.RemoveEditorClip(_clipId);
        if (deleted == null)
            throw new InvalidOperationException("Clip not found");

        _deletedClip = EditorCommandCloning.CloneClip(deleted);
    }

    public void Undo(BeatmapEditorDocument document)
    {
        string reason = null;
        if (_deletedClip == null || !document.AddEditorClip(EditorCommandCloning.CloneClip(_deletedClip), out reason))
            throw new InvalidOperationException(reason ?? "Deleted clip is not available");
    }
}

public sealed class MoveClipCommand : IEditorCommand
{
    private readonly string _clipId;
    private readonly double _newStartBeat;
    private readonly int _newTrackIndex;
    private bool _hasOldValues;
    private double _oldStartBeat;
    private int _oldTrackIndex;

    public MoveClipCommand(string clipId, double newStartBeat, int newTrackIndex)
    {
        _clipId = clipId;
        _newStartBeat = newStartBeat;
        _newTrackIndex = newTrackIndex;
    }

    public string Name => "Move Clip";

    public void Execute(BeatmapEditorDocument document)
    {
        ChartEditorClip clip = document.FindEditorClip(_clipId) ?? throw new InvalidOperationException("Clip not found");
        if (!_hasOldValues)
        {
            _oldStartBeat = clip.StartBeat;
            _oldTrackIndex = clip.TrackIndex;
            _hasOldValues = true;
        }

        if (!document.MoveEditorClip(_clipId, _newStartBeat, _newTrackIndex, out string reason))
            throw new InvalidOperationException(reason);
    }

    public void Undo(BeatmapEditorDocument document)
    {
        if (!document.MoveEditorClip(_clipId, _oldStartBeat, _oldTrackIndex, out string reason))
            throw new InvalidOperationException(reason);
    }
}

public sealed class ResizeClipCommand : IEditorCommand
{
    private readonly string _clipId;
    private readonly double _newLengthBeats;
    private bool _hasOldValue;
    private double _oldLengthBeats;

    public ResizeClipCommand(string clipId, double newLengthBeats)
    {
        _clipId = clipId;
        _newLengthBeats = newLengthBeats;
    }

    public string Name => "Resize Clip";

    public void Execute(BeatmapEditorDocument document)
    {
        ChartEditorClip clip = document.FindEditorClip(_clipId) ?? throw new InvalidOperationException("Clip not found");
        if (!_hasOldValue)
        {
            _oldLengthBeats = clip.LengthBeats;
            _hasOldValue = true;
        }

        if (!document.ResizeEditorClip(_clipId, _newLengthBeats, out string reason))
            throw new InvalidOperationException(reason);
    }

    public void Undo(BeatmapEditorDocument document)
    {
        if (!document.ResizeEditorClip(_clipId, _oldLengthBeats, out string reason))
            throw new InvalidOperationException(reason);
    }
}

public sealed class ChangeClipDataCommand : IEditorCommand
{
    private readonly string _clipId;
    private readonly Dictionary<string, string> _newData;
    private Dictionary<string, string> _oldData;

    public ChangeClipDataCommand(string clipId, IDictionary<string, string> newData)
    {
        _clipId = clipId;
        _newData = new Dictionary<string, string>(newData ?? new Dictionary<string, string>());
    }

    public string Name => "Change Clip Data";

    public void Execute(BeatmapEditorDocument document)
    {
        ChartEditorClip clip = document.FindEditorClip(_clipId) ?? throw new InvalidOperationException("Clip not found");
        _oldData ??= new Dictionary<string, string>(clip.Data ?? new Dictionary<string, string>());
        if (!document.ChangeEditorClipData(_clipId, _newData, out string reason))
            throw new InvalidOperationException(reason);
    }

    public void Undo(BeatmapEditorDocument document)
    {
        if (!document.ChangeEditorClipData(_clipId, _oldData, out string reason))
            throw new InvalidOperationException(reason);
    }
}
