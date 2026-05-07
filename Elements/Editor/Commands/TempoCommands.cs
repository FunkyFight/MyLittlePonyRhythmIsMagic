using System;
using Rhythm.Note;

namespace MLP_RiM.Elements.Editor.Commands;

public sealed class CreateTempoChangeCommand : IEditorCommand
{
    private readonly double _beat;
    private readonly double _bpm;
    private ChartEffect _effect;

    public CreateTempoChangeCommand(double beat, double bpm)
    {
        _beat = beat;
        _bpm = bpm;
    }

    public string Name => "Create Tempo Change";

    public void Execute(BeatmapEditorDocument document)
    {
        _effect ??= CreateEffect(_beat, _bpm, document);
        if (!document.TryPlaceEffect(_effect, out ChartEffect placedEffect, out string reason))
            throw new InvalidOperationException(reason);

        _effect = placedEffect;
    }

    public void Undo(BeatmapEditorDocument document)
    {
        document.RemoveEffect(_effect);
    }

    private static ChartEffect CreateEffect(double beat, double bpm, BeatmapEditorDocument document)
    {
        ChartEffect effect = new()
        {
            BeatPosition = beat,
            SongPosition = document.GetSongPositionAtBeat(beat),
            EffectType = ChartEffect.BpmChangeEffectType
        };
        effect.SetBpm(bpm);
        effect.SetSectionOffset(0);
        return effect;
    }
}

public sealed class MoveTempoChangeCommand : IEditorCommand
{
    private readonly ChartEffect _effect;
    private readonly double _newBeat;
    private bool _hasOldBeat;
    private double _oldBeat;

    public MoveTempoChangeCommand(ChartEffect effect, double newBeat)
    {
        _effect = effect ?? throw new ArgumentNullException(nameof(effect));
        _newBeat = newBeat;
    }

    public string Name => "Move Tempo Change";

    public void Execute(BeatmapEditorDocument document)
    {
        if (!_hasOldBeat)
        {
            _oldBeat = document.GetEffectBeat(_effect);
            _hasOldBeat = true;
        }

        if (!document.MoveEffectToBeat(_effect, _newBeat))
            throw new InvalidOperationException("Tempo change not found");
    }

    public void Undo(BeatmapEditorDocument document)
    {
        if (!document.MoveEffectToBeat(_effect, _oldBeat))
            throw new InvalidOperationException("Tempo change not found");
    }
}

public sealed class DeleteTempoChangeCommand : IEditorCommand
{
    private readonly ChartEffect _effect;

    public DeleteTempoChangeCommand(ChartEffect effect)
    {
        _effect = effect ?? throw new ArgumentNullException(nameof(effect));
    }

    public string Name => "Delete Tempo Change";

    public void Execute(BeatmapEditorDocument document)
    {
        if (!document.RemoveEffect(_effect))
            throw new InvalidOperationException("Tempo change not found");
    }

    public void Undo(BeatmapEditorDocument document)
    {
        if (!document.TryPlaceEffect(_effect, out _, out string reason))
            throw new InvalidOperationException(reason);
    }
}
