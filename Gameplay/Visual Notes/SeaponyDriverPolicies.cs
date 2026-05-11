using System;
using System.Collections.Generic;
using MLP_RiM.Elements.Editor;
using Rhythm.Note;

/// <summary>
/// Driver policy qui choisit la note SeaPony autorisée à piloter les acteurs.
/// </summary>
/// <remarks>
/// Elle reprend la priorité historique de SeaPony Parade : une note post-hit gagne sur une note en approche,
/// sauf quand une approche Roll recouvre la queue post-hit d'un TapTap.
/// </remarks>
public sealed class SeaponyActorDriverPolicy : IVisualDriverPolicy
{
    private readonly Func<SeaponyAction, Note, double> _getApproachDuration;
    private readonly Func<SeaponyAction, Note, double> _getDespawnDelay;
    private readonly Func<double> _getMaxApproachDuration;
    private IReadOnlyList<Note> _cachedNotes;
    private double _cachedSongPosition = double.NaN;
    private Note _cachedDriver;

    /// <summary>
    /// Crée une policy d'acteurs SeaPony avec les durées spécifiques à la scène.
    /// </summary>
    public SeaponyActorDriverPolicy(
        Func<SeaponyAction, Note, double> getApproachDuration,
        Func<SeaponyAction, Note, double> getDespawnDelay,
        Func<double> getMaxApproachDuration)
    {
        _getApproachDuration = getApproachDuration ?? throw new ArgumentNullException(nameof(getApproachDuration));
        _getDespawnDelay = getDespawnDelay ?? throw new ArgumentNullException(nameof(getDespawnDelay));
        _getMaxApproachDuration = getMaxApproachDuration ?? throw new ArgumentNullException(nameof(getMaxApproachDuration));
    }

    /// <inheritdoc />
    public Note ResolveDriver(VisualDriverContext context)
    {
        if(ReferenceEquals(_cachedNotes, context.Notes) && _cachedSongPosition == context.SongPosition)
            return _cachedDriver;

        _cachedNotes = context.Notes;
        _cachedSongPosition = context.SongPosition;
        _cachedDriver = findDriver(context.Notes, context.SongPosition);
        return _cachedDriver;
    }

    private Note findDriver(IReadOnlyList<Note> notes, double songPosition)
    {
        if(notes == null)
            return null;

        Note postHitNote = null;
        Note approachNote = null;
        double closestApproachTime = double.PositiveInfinity;
        double maxApproachDuration = _getMaxApproachDuration();

        foreach(Note note in notes)
        {
            if(!SeaponyNoteCodec.TryReadAction(note?.AdditionnalData, out SeaponyAction action))
                continue;

            if(note.SongPosition - maxApproachDuration > songPosition)
                break;

            double approachDuration = _getApproachDuration(action, note);
            double despawnDelay = _getDespawnDelay(action, note);
            double approachStart = note.SongPosition - approachDuration;
            double despawnEnd = note.SongPosition + despawnDelay;

            if(songPosition >= note.SongPosition && songPosition < despawnEnd)
            {
                if(postHitNote == null || note.SongPosition >= postHitNote.SongPosition)
                    postHitNote = note;

                continue;
            }

            if(songPosition >= approachStart && songPosition < note.SongPosition)
            {
                double approachTime = note.SongPosition - songPosition;
                if(approachTime < closestApproachTime)
                {
                    closestApproachTime = approachTime;
                    approachNote = note;
                }
            }
        }

        if(isRollApproachOverlappingTapTail(postHitNote, approachNote))
            return approachNote;

        return postHitNote ?? approachNote;
    }

    private static bool isRollApproachOverlappingTapTail(Note postHitNote, Note approachNote)
    {
        return SeaponyNoteCodec.IsAction(postHitNote?.AdditionnalData, SeaponyAction.TapTap)
            && SeaponyNoteCodec.IsAction(approachNote?.AdditionnalData, SeaponyAction.Roll);
    }
}

/// <summary>
/// Driver policy qui choisit la note SeaPony autorisée à piloter le background.
/// </summary>
public sealed class SeaponyBackgroundDriverPolicy : IVisualDriverPolicy
{
    private readonly Func<SeaponyAction, Note, double> _getScrollDuration;

    /// <summary>
    /// Crée une policy de background SeaPony.
    /// </summary>
    public SeaponyBackgroundDriverPolicy(Func<SeaponyAction, Note, double> getScrollDuration)
    {
        _getScrollDuration = getScrollDuration ?? throw new ArgumentNullException(nameof(getScrollDuration));
    }

    /// <inheritdoc />
    public Note ResolveDriver(VisualDriverContext context)
    {
        if(context.Notes == null)
            return null;

        Note drivingNote = null;
        foreach(Note note in context.Notes)
        {
            if(!SeaponyNoteCodec.TryReadAction(note?.AdditionnalData, out SeaponyAction action))
                continue;

            double scrollEnd = note.SongPosition + _getScrollDuration(action, note);
            if(context.SongPosition >= note.SongPosition && context.SongPosition <= scrollEnd)
                drivingNote = note;

            if(note.SongPosition > context.SongPosition)
                break;
        }

        return drivingNote;
    }
}
