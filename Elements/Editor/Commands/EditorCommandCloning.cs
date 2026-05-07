using System.Collections.Generic;
using Rhythm.Note;

namespace MLP_RiM.Elements.Editor.Commands;

internal static class EditorCommandCloning
{
    public static ChartNote CloneNote(ChartNote note)
    {
        if (note == null)
            return null;

        return new ChartNote
        {
            SongPosition = note.SongPosition,
            BeatPosition = note.BeatPosition,
            HoldDuration = note.HoldDuration,
            HoldBeats = note.HoldBeats,
            InputActionToPress = note.InputActionToPress,
            AdditionnalData = new Dictionary<string, string>(note.AdditionnalData ?? new Dictionary<string, string>())
        };
    }

    public static ChartEditorClip CloneClip(ChartEditorClip clip)
    {
        if (clip == null)
            return null;

        return new ChartEditorClip
        {
            Id = clip.Id,
            TrackIndex = clip.TrackIndex,
            StartBeat = clip.StartBeat,
            LengthBeats = clip.LengthBeats,
            RhythmGameId = clip.RhythmGameId,
            ClipTypeId = clip.ClipTypeId,
            ClipCategory = clip.ClipCategory,
            InputAction = clip.InputAction,
            Data = new Dictionary<string, string>(clip.Data ?? new Dictionary<string, string>())
        };
    }

    public static ChartEffect CloneEffect(ChartEffect effect)
    {
        if (effect == null)
            return null;

        return new ChartEffect
        {
            SongPosition = effect.SongPosition,
            BeatPosition = effect.BeatPosition,
            EffectType = effect.EffectType,
            Data = new Dictionary<string, string>(effect.Data ?? new Dictionary<string, string>())
        };
    }
}
