using System;
using System.Collections.Generic;
using System.IO;
using GameCore;
using GameCore.Animation;
using GameCore.Audio;
using GameCore.GameObjects;
using GameCore.Scenes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MLP_RiM.Elements.Editor;
using Rhythm.Note;

public enum SeeSawSide
{
    Inner,
    Outer,
    Exit
}

public enum SeeSawJumpLength
{
    Short = 1,
    Long = 2
}

public enum SeeSawPatternKind
{
    LongLong,
    LongShort,
    ShortLong,
    ShortShort
}

public enum SeeSawActor
{
    Applejack,
    RainbowDash
}

public enum SeeSawJudgement
{
    Pending,
    Just,
    Barely,
    Miss
}

public enum SeeSawImpactKind
{
    Cue,
    PlayerHit,
    RelayEnd,
    Exit
}

public enum SeeSawPathId
{
    RainbowOutOut,
    RainbowOutIn,
    RainbowInOut,
    RainbowInIn,
    ApplejackOutOut,
    ApplejackOutIn,
    ApplejackInOut,
    ApplejackInIn,
    ApplejackStartOut,
    ApplejackStartIn,
    ApplejackEndOut,
    ApplejackEndIn,
    RainbowHighOutOut,
    RainbowHighOutIn,
    RainbowHighInOut,
    RainbowHighInIn,
    ApplejackHighOutOut,
    ApplejackHighOutIn,
    ApplejackHighInOut,
    ApplejackHighInIn,
    ApplejackHighStartOut,
    ApplejackHighStartIn,
    ApplejackHighEndOut,
    ApplejackHighEndIn
}

public sealed class SeeSawPatternEvent
{
    public int Id { get; init; }
    public Note SourceNote { get; init; }
    public double CueBeat { get; init; }
    public double PlayerHitBeat { get; init; }
    public double EndBeat { get; init; }
    public double PrepStartBeat { get; init; }
    public double CueSongPosition { get; init; }
    public double PlayerHitSongPosition { get; init; }
    public double EndSongPosition { get; init; }
    public double PrepStartSongPosition { get; init; }
    public SeeSawPatternKind Pattern { get; init; }
    public SeeSawSide LaunchSide { get; init; }
    public SeeSawSide ApplejackCueSide { get; init; }
    public SeeSawSide TargetSide { get; init; }
    public SeeSawSide ApplejackTargetSide { get; init; }
    public bool RainbowHigh { get; init; }
    public bool ApplejackHigh { get; init; }
    public bool IsExit { get; init; }
    public SeeSawJudgement Judgement { get; set; }
}

public sealed class SeeSawJumpSegment
{
    public int Id { get; init; }
    public int EventId { get; init; }
    public SeeSawActor Actor { get; init; }
    public double StartBeat { get; init; }
    public double EndBeat { get; init; }
    public double StartSongPosition { get; init; }
    public double EndSongPosition { get; init; }
    public SeeSawSide FromSide { get; init; }
    public SeeSawSide ToSide { get; init; }
    public bool High { get; init; }
    public SeeSawPathId PathId { get; init; }
}

public sealed class SeeSawImpactEvent
{
    public int Id { get; init; }
    public int PatternEventId { get; init; }
    public SeeSawActor Actor { get; init; }
    public double Beat { get; init; }
    public double SongPosition { get; init; }
    public SeeSawSide Side { get; init; }
    public SeeSawImpactKind Kind { get; init; }
    public SeeSawJumpLength JumpLength { get; init; }
}

public readonly struct SeeSawImpactSource
{
    public SeeSawImpactSource(SeeSawSide fromSide, SeeSawSide toSide)
    {
        FromSide = fromSide;
        ToSide = toSide;
    }

    public SeeSawSide FromSide { get; }
    public SeeSawSide ToSide { get; }
    public SeeSawJumpLength JumpLength => SeeSawTiming.GetJumpLengthFromSide(FromSide);
}

public readonly struct SeeSawCompiledEventTiming
{
    public SeeSawCompiledEventTiming(double idealPrepStartBeat, double prepStartBeat, double cueBeat, double playerHitBeat, double endBeat, bool isSeeSaw, bool isExit, bool isValid, string invalidReason, SeeSawPatternKind pattern, SeeSawSide launchSide, SeeSawSide targetSide, SeeSawSide applejackTargetSide, SeeSawSide? applejackCueSide = null, SeeSawImpactSource? applejackCueSource = null, SeeSawImpactSource? rainbowSource = null, SeeSawImpactSource? applejackEndSource = null)
    {
        IdealPrepStartBeat = idealPrepStartBeat;
        PrepStartBeat = prepStartBeat;
        CueBeat = cueBeat;
        PlayerHitBeat = playerHitBeat;
        EndBeat = endBeat;
        IsSeeSaw = isSeeSaw;
        IsExit = isExit;
        IsValid = isValid;
        InvalidReason = invalidReason ?? string.Empty;
        Pattern = pattern;
        LaunchSide = launchSide;
        TargetSide = targetSide;
        ApplejackTargetSide = applejackTargetSide;
        ApplejackCueSide = applejackCueSide ?? launchSide;
        ApplejackCueSource = applejackCueSource ?? new SeeSawImpactSource(launchSide, ApplejackCueSide);
        RainbowSource = rainbowSource ?? new SeeSawImpactSource(launchSide, targetSide);
        ApplejackEndSource = applejackEndSource ?? new SeeSawImpactSource(ApplejackCueSide, applejackTargetSide);
    }

    public double IdealPrepStartBeat { get; }
    public double PrepStartBeat { get; }
    public double CueBeat { get; }
    public double PlayerHitBeat { get; }
    public double EndBeat { get; }
    public bool IsSeeSaw { get; }
    public bool IsExit { get; }
    public bool IsValid { get; }
    public string InvalidReason { get; }
    public SeeSawPatternKind Pattern { get; }
    public SeeSawSide LaunchSide { get; }
    public SeeSawSide ApplejackCueSide { get; }
    public SeeSawSide TargetSide { get; }
    public SeeSawSide ApplejackTargetSide { get; }
    public SeeSawImpactSource ApplejackCueSource { get; }
    public SeeSawImpactSource RainbowSource { get; }
    public SeeSawImpactSource ApplejackEndSource { get; }
}

public readonly struct SeeSawLogicalState
{
    public SeeSawLogicalState(SeeSawSide rainbowSide, SeeSawSide applejackSide, double applejackAvailableBeat = 0.0)
    {
        RainbowSide = rainbowSide;
        ApplejackSide = applejackSide;
        ApplejackAvailableBeat = applejackAvailableBeat;
    }

    public SeeSawSide RainbowSide { get; }
    public SeeSawSide ApplejackSide { get; }
    public double ApplejackAvailableBeat { get; }

    public static SeeSawLogicalState Initial => new(SeeSawSide.Outer, SeeSawSide.Exit);

    public static SeeSawLogicalState InitialWithLeadIn(double leadInBeats)
    {
        if (double.IsNaN(leadInBeats) || double.IsInfinity(leadInBeats) || leadInBeats <= 0.0)
            return Initial;

        return new SeeSawLogicalState(SeeSawSide.Outer, SeeSawSide.Exit, -leadInBeats);
    }
}

public static class SeeSawPatternInfo
{
    public static SeeSawJumpLength GetRainbowTargetLength(SeeSawPatternKind pattern)
    {
        return pattern is SeeSawPatternKind.LongLong or SeeSawPatternKind.ShortLong
            ? SeeSawJumpLength.Long
            : SeeSawJumpLength.Short;
    }

    public static SeeSawSide GetApplejackCueSide(SeeSawPatternKind pattern)
    {
        return pattern is SeeSawPatternKind.LongLong or SeeSawPatternKind.LongShort
            ? SeeSawSide.Outer
            : SeeSawSide.Inner;
    }

    public static SeeSawJumpLength GetApplejackCueLength(SeeSawPatternKind pattern)
    {
        return pattern is SeeSawPatternKind.LongLong or SeeSawPatternKind.LongShort
            ? SeeSawJumpLength.Long
            : SeeSawJumpLength.Short;
    }

    public static SeeSawSide GetTargetSide(SeeSawPatternKind pattern)
    {
        return GetRainbowTargetLength(pattern) == SeeSawJumpLength.Long
            ? SeeSawSide.Outer
            : SeeSawSide.Inner;
    }
}

public sealed class SeeSawTimeline
{
    private readonly Dictionary<int, SeeSawPatternEvent> _eventsById = new();

    public List<SeeSawPatternEvent> PatternEvents { get; } = new();
    public List<SeeSawJumpSegment> JumpSegments { get; } = new();
    public List<SeeSawImpactEvent> ImpactEvents { get; } = new();
    public List<string> Errors { get; } = new();
    public Dictionary<Note, int> NoteToEventId { get; } = new();

    public void FinalizeOrdering()
    {
        PatternEvents.Sort((a, b) => a.PlayerHitBeat.CompareTo(b.PlayerHitBeat));
        JumpSegments.Sort((a, b) => a.StartBeat.CompareTo(b.StartBeat));
        ImpactEvents.Sort((a, b) => a.Beat.CompareTo(b.Beat));

        _eventsById.Clear();
        foreach (SeeSawPatternEvent patternEvent in PatternEvents)
            _eventsById[patternEvent.Id] = patternEvent;
    }

    public bool TryGetEvent(int eventId, out SeeSawPatternEvent patternEvent)
    {
        return _eventsById.TryGetValue(eventId, out patternEvent);
    }

    public bool TryGetEventForNote(Note note, out SeeSawPatternEvent patternEvent)
    {
        patternEvent = null;
        return note != null
            && NoteToEventId.TryGetValue(note, out int eventId)
            && _eventsById.TryGetValue(eventId, out patternEvent);
    }

    public SeeSawJumpSegment GetActiveSegment(SeeSawActor actor, double beat)
    {
        return GetActiveSegment(actor, beat, null);
    }

    public SeeSawJumpSegment GetActiveSegment(SeeSawActor actor, double beat, double? songPosition)
    {
        SeeSawJumpSegment active = null;
        foreach (SeeSawJumpSegment segment in JumpSegments)
        {
            if (segment.Actor != actor)
                continue;

            bool isActive = segment.StartBeat <= beat && beat < segment.EndBeat;

            if (isActive)
                active = segment;

            bool isAfter = segment.StartBeat > beat;

            if (isAfter)
                break;
        }

        return active;
    }

    public SeeSawSide GetLastGroundedSide(SeeSawActor actor, double beat)
    {
        return GetLastGroundedSide(actor, beat, null);
    }

    public SeeSawSide GetLastGroundedSide(SeeSawActor actor, double beat, double? songPosition)
    {
        SeeSawSide side = actor == SeeSawActor.RainbowDash ? SeeSawSide.Outer : SeeSawSide.Exit;

        foreach (SeeSawJumpSegment segment in JumpSegments)
        {
            if (segment.Actor != actor)
                continue;

            bool isAfter = segment.EndBeat > beat;

            if (isAfter)
                break;

            side = segment.ToSide;
        }

        return side;
    }

    public SeeSawImpactEvent GetLastImpact(double beat)
    {
        return GetLastImpact(beat, null);
    }

    public SeeSawImpactEvent GetLastImpact(double beat, double? songPosition)
    {
        SeeSawImpactEvent last = null;
        foreach (SeeSawImpactEvent impact in ImpactEvents)
        {
            bool isAfter = impact.Beat > beat;

            if (isAfter)
                break;

            last = impact;
        }

        return last;
    }

    public SeeSawImpactEvent GetNextImpact(double beat)
    {
        return GetNextImpact(beat, null);
    }

    public SeeSawImpactEvent GetNextImpact(double beat, double? songPosition)
    {
        foreach (SeeSawImpactEvent impact in ImpactEvents)
        {
            bool isAfter = impact.Beat > beat;

            if (isAfter)
                return impact;
        }

        return null;
    }

    public void ResetJudgements(double fromBeat = double.NegativeInfinity)
    {
        ResetJudgements(fromBeat, null);
    }

    public void ResetJudgements(double fromBeat, double? fromSongPosition)
    {
        foreach (SeeSawPatternEvent patternEvent in PatternEvents)
        {
            bool shouldReset = patternEvent.PlayerHitBeat >= fromBeat;

            if (shouldReset)
                patternEvent.Judgement = SeeSawJudgement.Pending;
        }
    }
}

public static class SeeSawTiming
{
    public const double ShortJumpBeats = 1.0;
    public const double LongJumpBeats = 2.0;
    public const double ExitJumpBeats = 2.0;

    public static double GetJumpLengthBeats(SeeSawJumpLength length)
    {
        return length == SeeSawJumpLength.Long ? LongJumpBeats : ShortJumpBeats;
    }

    public static double GetJumpBeatsFromSide(SeeSawSide side)
    {
        if (side == SeeSawSide.Outer || side == SeeSawSide.Exit)
            return LongJumpBeats;

        return ShortJumpBeats;
    }

    public static SeeSawJumpLength GetJumpLengthFromSide(SeeSawSide side)
    {
        return GetJumpBeatsFromSide(side) >= LongJumpBeats ? SeeSawJumpLength.Long : SeeSawJumpLength.Short;
    }

    public static SeeSawPatternKind GetPattern(SeeSawJumpLength applejackPrepLength, SeeSawJumpLength rainbowLength)
    {
        return (applejackPrepLength, rainbowLength) switch
        {
            (SeeSawJumpLength.Long, SeeSawJumpLength.Short) => SeeSawPatternKind.LongShort,
            (SeeSawJumpLength.Short, SeeSawJumpLength.Long) => SeeSawPatternKind.ShortLong,
            (SeeSawJumpLength.Short, SeeSawJumpLength.Short) => SeeSawPatternKind.ShortShort,
            _ => SeeSawPatternKind.LongLong
        };
    }

    public static SeeSawSide ToggleSide(SeeSawSide side)
    {
        return side == SeeSawSide.Outer ? SeeSawSide.Inner : SeeSawSide.Outer;
    }

    public static SeeSawSide GetSideOppositeRainbow(SeeSawSide rainbowSide)
    {
        return rainbowSide == SeeSawSide.Outer ? SeeSawSide.Inner : SeeSawSide.Outer;
    }

    public static SeeSawSide GetSideOppositeApplejack(SeeSawSide applejackSide)
    {
        return applejackSide == SeeSawSide.Outer ? SeeSawSide.Inner : SeeSawSide.Outer;
    }

    public static void GetTargetSides(SeeSawAction action, SeeSawSide currentRainbowSide, SeeSawSide currentApplejackSide, out SeeSawSide rainbowTargetSide, out SeeSawSide applejackTargetSide)
    {
        switch (SeeSawAction.GetBaseDirection(action.Direction))
        {
            case SeeSawDirection.Inner:
                rainbowTargetSide = SeeSawSide.Inner;
                applejackTargetSide = SeeSawSide.Inner;
                return;

            case SeeSawDirection.Opposite:
                switch (action.OppositeMode)
                {
                    case SeeSawOppositeMode.Applejack:
                        applejackTargetSide = GetSideOppositeRainbow(currentRainbowSide);
                        rainbowTargetSide = GetSideOppositeApplejack(applejackTargetSide);
                        return;
                    case SeeSawOppositeMode.Both:
                        rainbowTargetSide = ToggleSide(currentRainbowSide);
                        applejackTargetSide = ToggleSide(currentApplejackSide);
                        return;
                    default:
                        rainbowTargetSide = GetSideOppositeApplejack(currentApplejackSide);
                        applejackTargetSide = GetSideOppositeRainbow(rainbowTargetSide);
                        return;
                }

            case SeeSawDirection.Exit:
                rainbowTargetSide = currentRainbowSide;
                applejackTargetSide = SeeSawSide.Exit;
                return;

            default:
                rainbowTargetSide = SeeSawSide.Outer;
                applejackTargetSide = SeeSawSide.Outer;
                return;
        }
    }

    public static SeeSawCompiledEventTiming CreateEventTiming(SeeSawAction action, SeeSawLogicalState state, double playerHitBeat)
    {
        return CreateScheduledEventTiming(action, state, playerHitBeat);
    }

    public static SeeSawCompiledEventTiming CreateScheduledEventTiming(SeeSawAction action, SeeSawLogicalState state, double playerHitBeat)
    {
        bool isExit = SeeSawAction.GetBaseDirection(action.Direction) == SeeSawDirection.Exit;
        if (isExit)
        {
            double startBeat = playerHitBeat - ExitJumpBeats;
            return new SeeSawCompiledEventTiming(
                startBeat,
                startBeat,
                playerHitBeat,
                playerHitBeat,
                playerHitBeat,
                isSeeSaw: true,
                isExit: true,
                isValid: true,
                invalidReason: null,
                pattern: SeeSawPatternKind.LongLong,
                launchSide: state.RainbowSide,
                targetSide: state.RainbowSide,
                applejackTargetSide: SeeSawSide.Exit);
        }

        SeeSawSide launchSide = state.RainbowSide;
        GetTargetSides(action, state.RainbowSide, state.ApplejackSide, out SeeSawSide rainbowTargetSide, out SeeSawSide applejackTargetSide);

        SeeSawJumpLength applejackPrepLength = GetJumpLengthFromSide(launchSide);
        SeeSawJumpLength rainbowTargetLength = GetJumpLengthFromSide(rainbowTargetSide);
        SeeSawJumpLength rainbowJumpLength = GetJumpLengthFromSide(launchSide);
        double cueBeat = playerHitBeat - GetJumpLengthBeats(rainbowJumpLength);
        double endBeat = playerHitBeat + GetJumpBeatsFromSide(applejackTargetSide);
        double idealPrepStartBeat = cueBeat - GetJumpLengthBeats(applejackPrepLength);
        bool hasFullPrep = state.ApplejackAvailableBeat <= idealPrepStartBeat + SeeSawChartCompiler.SameBeatEpsilon;
        bool isChainedToCue = Math.Abs(state.ApplejackAvailableBeat - cueBeat) <= SeeSawChartCompiler.SameBeatEpsilon
            && state.ApplejackSide == launchSide;
        bool isValid = hasFullPrep || isChainedToCue;
        double prepStartBeat = isChainedToCue ? cueBeat : idealPrepStartBeat;

        return new SeeSawCompiledEventTiming(
            idealPrepStartBeat,
            prepStartBeat,
            cueBeat,
            playerHitBeat,
            endBeat,
            isSeeSaw: true,
            isExit: false,
            isValid: isValid,
            invalidReason: isValid ? null : GetInvalidReason(state, launchSide, cueBeat, idealPrepStartBeat),
            pattern: GetPattern(applejackPrepLength, rainbowTargetLength),
            launchSide: launchSide,
            targetSide: rainbowTargetSide,
            applejackTargetSide: applejackTargetSide,
            applejackCueSource: new SeeSawImpactSource(state.ApplejackSide, launchSide),
            rainbowSource: new SeeSawImpactSource(launchSide, rainbowTargetSide),
            applejackEndSource: new SeeSawImpactSource(launchSide, applejackTargetSide));
    }

    public static SeeSawCompiledEventTiming CreateScheduledPatternTiming(SeeSawPatternKind pattern, SeeSawAction style, SeeSawLogicalState state, double playerHitBeat, bool exitAfterHit = false)
    {
        _ = style;

        SeeSawSide launchSide = state.RainbowSide;
        SeeSawSide applejackCueSide = SeeSawPatternInfo.GetApplejackCueSide(pattern);
        SeeSawSide targetSide = SeeSawPatternInfo.GetTargetSide(pattern);
        SeeSawJumpLength applejackPrepLength = GetJumpLengthFromSide(state.ApplejackSide);
        SeeSawJumpLength rainbowJumpLength = GetJumpLengthFromSide(launchSide);

        double cueBeat = playerHitBeat - GetJumpLengthBeats(rainbowJumpLength);
        double endBeat = exitAfterHit ? playerHitBeat + ExitJumpBeats : playerHitBeat;
        double idealPrepStartBeat = cueBeat - GetJumpLengthBeats(applejackPrepLength);
        bool hasFullPrep = state.ApplejackAvailableBeat <= idealPrepStartBeat + SeeSawChartCompiler.SameBeatEpsilon;
        bool isChainedToCue = Math.Abs(state.ApplejackAvailableBeat - cueBeat) <= SeeSawChartCompiler.SameBeatEpsilon
            && state.ApplejackSide == applejackCueSide;
        bool hasValidPrep = hasFullPrep || isChainedToCue;
        bool isValid = hasValidPrep;
        double prepStartBeat = isChainedToCue ? cueBeat : idealPrepStartBeat;

        string invalidReason = hasValidPrep ? null : GetInvalidReason(state, applejackCueSide, cueBeat, idealPrepStartBeat);

        return new SeeSawCompiledEventTiming(
            idealPrepStartBeat,
            prepStartBeat,
            cueBeat,
            playerHitBeat,
            endBeat,
            isSeeSaw: true,
            isExit: false,
            isValid: isValid,
            invalidReason: invalidReason,
            pattern: pattern,
            launchSide: launchSide,
            targetSide: targetSide,
            applejackTargetSide: exitAfterHit ? SeeSawSide.Exit : applejackCueSide,
            applejackCueSide: applejackCueSide,
            applejackCueSource: new SeeSawImpactSource(state.ApplejackSide, applejackCueSide),
            rainbowSource: new SeeSawImpactSource(launchSide, targetSide),
            applejackEndSource: new SeeSawImpactSource(applejackCueSide, exitAfterHit ? SeeSawSide.Exit : applejackCueSide));
    }

    private static string GetInvalidReason(SeeSawLogicalState state, SeeSawSide launchSide, double cueBeat, double idealPrepStartBeat)
    {
        return $"Applejack needs prep from {idealPrepStartBeat:0.###}b to {cueBeat:0.###}b, but is available at {state.ApplejackAvailableBeat:0.###}b on {state.ApplejackSide} and is not chained to {launchSide} at cue";
    }

    public static SeeSawLogicalState ApplyEvent(SeeSawAction action, SeeSawLogicalState state, double eventEndBeat)
    {
        GetTargetSides(action, state.RainbowSide, state.ApplejackSide, out SeeSawSide rainbowTargetSide, out SeeSawSide applejackTargetSide);
        return new SeeSawLogicalState(rainbowTargetSide, applejackTargetSide, eventEndBeat);
    }
}

public static class SeeSawChartCompiler
{
    internal const double SameBeatEpsilon = 0.0005;

    private readonly struct SeeSawCommand
    {
        public SeeSawCommand(SeeSawAction action, SeeSawPatternKind? pattern)
        {
            Action = action;
            Pattern = pattern;
        }

        public SeeSawAction Action { get; }
        public SeeSawPatternKind? Pattern { get; }
    }

    private readonly struct SeeSawCommandEntry
    {
        public SeeSawCommandEntry(Note sourceNote, double songPosition, SeeSawCommand command, int sortedNoteIndex)
        {
            SourceNote = sourceNote;
            SongPosition = songPosition;
            Command = command;
            SortedNoteIndex = sortedNoteIndex;
        }

        public Note SourceNote { get; }
        public double SongPosition { get; }
        public SeeSawCommand Command { get; }
        public int SortedNoteIndex { get; }
    }

    private readonly struct SeeSawChartCommandEntry
    {
        public SeeSawChartCommandEntry(ChartNote note, SeeSawCommand command, int sortedNoteIndex)
        {
            Note = note;
            Command = command;
            SortedNoteIndex = sortedNoteIndex;
        }

        public ChartNote Note { get; }
        public SeeSawCommand Command { get; }
        public int SortedNoteIndex { get; }
    }

    public static SeeSawTimeline Compile(IReadOnlyList<Note> notes, double crotchet)
    {
        return crotchet > 0.0
            ? Compile(notes, _ => crotchet)
            : new SeeSawTimeline();
    }

    public static SeeSawTimeline Compile(IReadOnlyList<Note> notes, Func<double, double> getCrotchetAt)
    {
        return Compile(notes, songPosition => songPosition / getCrotchetAt(songPosition), beat => beat * getCrotchetAt(0.0), getCrotchetAt);
    }

    public static SeeSawTimeline Compile(IReadOnlyList<Note> notes, Func<double, double> getBeatAt, Func<double, double> getSongPositionAtBeat, Func<double, double> getCrotchetAt, double leadInBeats = 0.0)
    {
        SeeSawTimeline timeline = new();
        if (notes == null || getBeatAt == null || getSongPositionAtBeat == null || getCrotchetAt == null)
            return timeline;

        List<Note> sortedNotes = new(notes);
        sortedNotes.Sort((a, b) => a.SongPosition.CompareTo(b.SongPosition));

        List<SeeSawCommandEntry> commandEntries = CreateCommandEntries(sortedNotes);
        SeeSawLogicalState state = SeeSawLogicalState.InitialWithLeadIn(leadInBeats);
        int eventId = 0;
        int segmentId = 0;
        int impactId = 0;

        for (int i = 0; i < commandEntries.Count; i++)
        {
            SeeSawCommandEntry entry = commandEntries[i];
            double hitBeat = getBeatAt(entry.SongPosition);
            if (double.IsNaN(hitBeat) || double.IsInfinity(hitBeat))
                continue;

            CompileNote(timeline, entry.SourceNote, entry.SongPosition, entry.Command, ShouldAutoExitAfterHit(commandEntries, i, sortedNotes), hitBeat, getSongPositionAtBeat, ref state, ref eventId, ref segmentId, ref impactId);
        }

        timeline.FinalizeOrdering();
        return timeline;
    }

    public static SeeSawTimeline Compile(IReadOnlyList<ChartNote> notes, Func<ChartNote, double> getNoteBeat, ChartTempoMap tempoMap, double leadInBeats = 0.0)
    {
        SeeSawTimeline timeline = new();
        if (notes == null || getNoteBeat == null || tempoMap == null)
            return timeline;

        List<ChartNote> sortedNotes = CreateSortedChartNotes(notes, getNoteBeat);
        List<SeeSawChartCommandEntry> commandEntries = CreateChartCommandEntries(sortedNotes);
        SeeSawLogicalState state = SeeSawLogicalState.InitialWithLeadIn(leadInBeats);
        int eventId = 0;
        int segmentId = 0;
        int impactId = 0;

        for (int i = 0; i < commandEntries.Count; i++)
        {
            SeeSawChartCommandEntry entry = commandEntries[i];
            double hitBeat = getNoteBeat(entry.Note);
            if (double.IsNaN(hitBeat) || double.IsInfinity(hitBeat))
                continue;

            double songPosition = tempoMap.BeatToSeconds(hitBeat);
            CompileNote(timeline, null, songPosition, entry.Command, ShouldAutoExitAfterHit(commandEntries, i, sortedNotes), hitBeat, tempoMap.BeatToSeconds, ref state, ref eventId, ref segmentId, ref impactId);
        }

        timeline.FinalizeOrdering();
        return timeline;
    }

    public static SeeSawTimeline CompileContextualChartNotes(IReadOnlyList<ChartNote> notes, double crotchet)
    {
        return crotchet > 0.0
            ? CompileContextualChartNotes(notes, songPosition => songPosition / crotchet, beat => beat * crotchet)
            : new SeeSawTimeline();
    }

    public static SeeSawTimeline CompileContextualChartNotes(IReadOnlyList<ChartNote> notes, Func<double, double> getBeatAt, Func<double, double> getSongPositionAtBeat)
    {
        SeeSawTimeline timeline = new();
        if (notes == null || getBeatAt == null || getSongPositionAtBeat == null)
            return timeline;

        List<ChartNote> sortedNotes = CreateSortedChartNotes(notes);

        List<SeeSawCommandEntry> commandEntries = CreateCommandEntries(sortedNotes);
        SeeSawLogicalState state = SeeSawLogicalState.Initial;
        int eventId = 0;
        int segmentId = 0;
        int impactId = 0;

        for (int i = 0; i < commandEntries.Count; i++)
        {
            SeeSawCommandEntry entry = commandEntries[i];
            CompileNote(timeline, null, entry.SongPosition, entry.Command, ShouldAutoExitAfterHit(commandEntries, i, sortedNotes), getBeatAt(entry.SongPosition), getSongPositionAtBeat, ref state, ref eventId, ref segmentId, ref impactId);
        }

        timeline.FinalizeOrdering();
        return timeline;
    }

    public static bool TryGetSeeSawAction(IReadOnlyDictionary<string, string> additionnalData, out SeeSawAction action)
    {
        if (additionnalData != null
            && additionnalData.TryGetValue(SeeSawAction.DataKey, out string value)
            && SeeSawAction.TryParse(value, out _))
        {
            action = SeeSawAction.FromAdditionnalData(additionnalData);
            return true;
        }

        action = default;
        return false;
    }

    private static bool TryGetSeeSawCommand(IReadOnlyDictionary<string, string> additionnalData, out SeeSawCommand command)
    {
        bool hasAction = TryGetSeeSawAction(additionnalData, out SeeSawAction action);
        bool hasPattern = SeeSawAction.TryGetPattern(additionnalData, out SeeSawPatternKind storedPattern);
        if (!hasAction && !hasPattern)
        {
            command = default;
            return false;
        }

        if (!hasAction)
            action = SeeSawAction.FromAdditionnalData(additionnalData);

        SeeSawPatternKind? pattern = hasPattern ? storedPattern : null;
        command = new SeeSawCommand(action, pattern);
        return true;
    }

    private static SeeSawCompiledEventTiming CreateTiming(SeeSawCommand command, SeeSawLogicalState state, double hitBeat, bool exitAfterHit = false)
    {
        return command.Pattern.HasValue
            ? SeeSawTiming.CreateScheduledPatternTiming(command.Pattern.Value, command.Action, state, hitBeat, exitAfterHit)
            : SeeSawTiming.CreateScheduledEventTiming(command.Action, state, hitBeat);
    }

    private static List<SeeSawCommandEntry> CreateCommandEntries(IReadOnlyList<Note> sortedNotes)
    {
        List<SeeSawCommandEntry> entries = new();
        for (int i = 0; i < sortedNotes.Count; i++)
        {
            Note note = sortedNotes[i];
            if (TryGetSeeSawCommand(note.AdditionnalData, out SeeSawCommand command))
                entries.Add(new SeeSawCommandEntry(note, note.SongPosition, command, i));
        }

        return entries;
    }

    private static List<SeeSawCommandEntry> CreateCommandEntries(IReadOnlyList<ChartNote> sortedNotes)
    {
        List<SeeSawCommandEntry> entries = new();
        for (int i = 0; i < sortedNotes.Count; i++)
        {
            ChartNote note = sortedNotes[i];
            if (TryGetSeeSawCommand(note.AdditionnalData, out SeeSawCommand command))
                entries.Add(new SeeSawCommandEntry(null, note.SongPosition, command, i));
        }

        return entries;
    }

    private static List<SeeSawChartCommandEntry> CreateChartCommandEntries(IReadOnlyList<ChartNote> sortedNotes)
    {
        List<SeeSawChartCommandEntry> entries = new();
        for (int i = 0; i < sortedNotes.Count; i++)
        {
            ChartNote note = sortedNotes[i];
            if (TryGetSeeSawCommand(note.AdditionnalData, out SeeSawCommand command))
                entries.Add(new SeeSawChartCommandEntry(note, command, i));
        }

        return entries;
    }

    private static bool ShouldAutoExitAfterHit(IReadOnlyList<SeeSawCommandEntry> commandEntries, int index, IReadOnlyList<Note> sortedNotes)
    {
        if (index < 0 || index >= commandEntries.Count || IsExit(commandEntries[index].Command))
            return false;

        int nextNoteIndex = commandEntries[index].SortedNoteIndex + 1;
        return nextNoteIndex < 0
            || nextNoteIndex >= (sortedNotes?.Count ?? 0)
            || !TryGetSeeSawCommand(sortedNotes[nextNoteIndex].AdditionnalData, out _);
    }

    private static bool ShouldAutoExitAfterHit(IReadOnlyList<SeeSawCommandEntry> commandEntries, int index, IReadOnlyList<ChartNote> sortedNotes)
    {
        if (index < 0 || index >= commandEntries.Count || IsExit(commandEntries[index].Command))
            return false;

        int nextNoteIndex = commandEntries[index].SortedNoteIndex + 1;
        return nextNoteIndex < 0
            || nextNoteIndex >= (sortedNotes?.Count ?? 0)
            || !TryGetSeeSawCommand(sortedNotes[nextNoteIndex].AdditionnalData, out _);
    }

    private static bool ShouldAutoExitAfterHit(IReadOnlyList<SeeSawChartCommandEntry> commandEntries, int index, IReadOnlyList<ChartNote> sortedNotes)
    {
        if (index < 0 || index >= commandEntries.Count || IsExit(commandEntries[index].Command))
            return false;

        int nextNoteIndex = commandEntries[index].SortedNoteIndex + 1;
        return nextNoteIndex < 0
            || nextNoteIndex >= (sortedNotes?.Count ?? 0)
            || !TryGetSeeSawCommand(sortedNotes[nextNoteIndex].AdditionnalData, out _);
    }

    private static bool IsExit(SeeSawCommand command)
    {
        return SeeSawAction.GetBaseDirection(command.Action.Direction) == SeeSawDirection.Exit;
    }

    public static SeeSawCompiledEventTiming GetTimingForChartNote(IReadOnlyList<ChartNote> notes, ChartNote targetNote, double crotchet)
    {
        return crotchet > 0.0
            ? GetTimingForChartNote(notes, targetNote, songPosition => songPosition / crotchet)
            : default;
    }

    public static SeeSawCompiledEventTiming GetTimingForChartNote(IReadOnlyList<ChartNote> notes, ChartNote targetNote, Func<double, double> getBeatAt, double leadInBeats = 0.0)
    {
        if (targetNote == null || notes == null || getBeatAt == null)
            return default;

        List<ChartNote> sortedNotes = CreateSortedChartNotes(notes);
        List<SeeSawChartCommandEntry> commandEntries = CreateChartCommandEntries(sortedNotes);

        SeeSawLogicalState state = SeeSawLogicalState.InitialWithLeadIn(leadInBeats);
        for (int i = 0; i < commandEntries.Count; i++)
        {
            SeeSawChartCommandEntry entry = commandEntries[i];
            ChartNote note = entry.Note;

            double hitBeat = getBeatAt(note.SongPosition);
            SeeSawCompiledEventTiming timing = CreateTiming(entry.Command, state, hitBeat, ShouldAutoExitAfterHit(commandEntries, i, sortedNotes));
            if (ReferenceEquals(note, targetNote))
                return timing;

            if (note.SongPosition < targetNote.SongPosition - SameBeatEpsilon && timing.IsValid)
                state = ApplyTiming(timing);
        }

        return default;
    }

    public static SeeSawCompiledEventTiming GetTimingForChartNote(IReadOnlyList<ChartNote> notes, ChartNote targetNote, Func<ChartNote, double> getNoteBeat, double leadInBeats = 0.0)
    {
        return GetTimingForChartNote(notes, targetNote, getNoteBeat, leadInBeats, includeAutoExit: true);
    }

    public static SeeSawCompiledEventTiming GetEditorTimingForChartNote(IReadOnlyList<ChartNote> notes, ChartNote targetNote, Func<ChartNote, double> getNoteBeat, double leadInBeats = 0.0)
    {
        return GetTimingForChartNote(notes, targetNote, getNoteBeat, leadInBeats, includeAutoExit: false);
    }

    private static SeeSawCompiledEventTiming GetTimingForChartNote(IReadOnlyList<ChartNote> notes, ChartNote targetNote, Func<ChartNote, double> getNoteBeat, double leadInBeats, bool includeAutoExit)
    {
        if (targetNote == null || notes == null || getNoteBeat == null)
            return default;

        List<ChartNote> sortedNotes = CreateSortedChartNotes(notes, getNoteBeat);
        List<SeeSawChartCommandEntry> commandEntries = CreateChartCommandEntries(sortedNotes);

        SeeSawLogicalState state = SeeSawLogicalState.InitialWithLeadIn(leadInBeats);
        double targetBeat = getNoteBeat(targetNote);
        BeatTick targetTick = BeatTick.FromBeat(targetBeat);
        for (int i = 0; i < commandEntries.Count; i++)
        {
            SeeSawChartCommandEntry entry = commandEntries[i];
            ChartNote note = entry.Note;

            double hitBeat = getNoteBeat(note);
            bool exitAfterHit = includeAutoExit && ShouldAutoExitAfterHit(commandEntries, i, sortedNotes);
            SeeSawCompiledEventTiming timing = CreateTiming(entry.Command, state, hitBeat, exitAfterHit);
            if (ReferenceEquals(note, targetNote))
                return timing;

            if (BeatTick.FromBeat(hitBeat) < targetTick && timing.IsValid)
                state = ApplyTiming(timing);
        }

        return default;
    }

    public static SeeSawCompiledEventTiming GetPreviewTiming(IReadOnlyList<ChartNote> notes, SeeSawAction action, double songPosition, double crotchet)
    {
        return crotchet > 0.0
            ? GetPreviewTiming(notes, action, songPosition, position => position / crotchet)
            : default;
    }

    public static SeeSawCompiledEventTiming GetPreviewTiming(IReadOnlyList<ChartNote> notes, SeeSawAction action, double songPosition, Func<double, double> getBeatAt, double leadInBeats = 0.0)
    {
        SeeSawLogicalState state = GetStateBefore(notes, songPosition, getBeatAt, leadInBeats);
        return CreateTiming(new SeeSawCommand(action, null), state, getBeatAt(songPosition));
    }

    public static SeeSawCompiledEventTiming GetPreviewTiming(IReadOnlyList<ChartNote> notes, SeeSawAction action, double beat, Func<ChartNote, double> getNoteBeat, double leadInBeats = 0.0)
    {
        SeeSawLogicalState state = GetStateBeforeBeat(notes, beat, getNoteBeat, leadInBeats);
        return CreateTiming(new SeeSawCommand(action, null), state, beat);
    }

    public static SeeSawCompiledEventTiming GetPreviewTiming(IReadOnlyList<ChartNote> notes, IReadOnlyDictionary<string, string> additionnalData, double songPosition, double crotchet)
    {
        return crotchet > 0.0
            ? GetPreviewTiming(notes, additionnalData, songPosition, position => position / crotchet)
            : default;
    }

    public static SeeSawCompiledEventTiming GetPreviewTiming(IReadOnlyList<ChartNote> notes, IReadOnlyDictionary<string, string> additionnalData, double songPosition, Func<double, double> getBeatAt, double leadInBeats = 0.0)
    {
        if (!TryGetSeeSawCommand(additionnalData, out SeeSawCommand command))
            return default;

        SeeSawLogicalState state = GetStateBefore(notes, songPosition, getBeatAt, leadInBeats);
        return CreateTiming(command, state, getBeatAt(songPosition));
    }

    public static SeeSawCompiledEventTiming GetPreviewTiming(IReadOnlyList<ChartNote> notes, IReadOnlyDictionary<string, string> additionnalData, double beat, Func<ChartNote, double> getNoteBeat, double leadInBeats = 0.0)
    {
        if (!TryGetSeeSawCommand(additionnalData, out SeeSawCommand command))
            return default;

        SeeSawLogicalState state = GetStateBeforeBeat(notes, beat, getNoteBeat, leadInBeats);
        return CreateTiming(command, state, beat);
    }

    public static SeeSawLogicalState GetStateBefore(IReadOnlyList<ChartNote> notes, double songPosition, double crotchet)
    {
        return crotchet > 0.0
            ? GetStateBefore(notes, songPosition, position => position / crotchet)
            : SeeSawLogicalState.Initial;
    }

    public static SeeSawLogicalState GetStateBefore(IReadOnlyList<ChartNote> notes, double songPosition, Func<double, double> getBeatAt, double leadInBeats = 0.0)
    {
        if (notes == null || getBeatAt == null)
            return SeeSawLogicalState.InitialWithLeadIn(leadInBeats);

        List<ChartNote> sortedNotes = CreateSortedChartNotes(notes);

        SeeSawLogicalState state = SeeSawLogicalState.InitialWithLeadIn(leadInBeats);
        foreach (ChartNote note in sortedNotes)
        {
            if (note.SongPosition >= songPosition - SameBeatEpsilon)
                break;

            if (!TryGetSeeSawCommand(note.AdditionnalData, out SeeSawCommand command))
                continue;

            SeeSawCompiledEventTiming timing = CreateTiming(command, state, getBeatAt(note.SongPosition));
            if (timing.IsValid)
                state = ApplyTiming(timing);
        }

        return state;
    }

    public static SeeSawLogicalState GetStateBeforeBeat(IReadOnlyList<ChartNote> notes, double beat, Func<ChartNote, double> getNoteBeat, double leadInBeats = 0.0)
    {
        if (notes == null || getNoteBeat == null)
            return SeeSawLogicalState.InitialWithLeadIn(leadInBeats);

        List<ChartNote> sortedNotes = CreateSortedChartNotes(notes, getNoteBeat);
        SeeSawLogicalState state = SeeSawLogicalState.InitialWithLeadIn(leadInBeats);
        BeatTick targetTick = BeatTick.FromBeat(beat);
        foreach (ChartNote note in sortedNotes)
        {
            double noteBeat = getNoteBeat(note);
            if (BeatTick.FromBeat(noteBeat) >= targetTick)
                break;

            if (!TryGetSeeSawCommand(note.AdditionnalData, out SeeSawCommand command))
                continue;

            SeeSawCompiledEventTiming timing = CreateTiming(command, state, noteBeat);
            if (timing.IsValid)
                state = ApplyTiming(timing);
        }

        return state;
    }

    private static void CompileNote(SeeSawTimeline timeline, Note sourceNote, double songPosition, SeeSawCommand command, bool exitAfterHit, double hitBeat, Func<double, double> getSongPositionAtBeat, ref SeeSawLogicalState state, ref int eventId, ref int segmentId, ref int impactId)
    {
        SeeSawAction action = command.Action;
        SeeSawCompiledEventTiming timing = CreateTiming(command, state, hitBeat, exitAfterHit);
        if (!timing.IsValid)
        {
            timeline.Errors.Add(CreateInvalidTimingError(songPosition, hitBeat, timing));
            return;
        }

        int currentEventId = eventId++;

        SeeSawPatternEvent patternEvent = new()
        {
            Id = currentEventId,
            SourceNote = sourceNote,
            CueBeat = timing.CueBeat,
            PlayerHitBeat = timing.PlayerHitBeat,
            EndBeat = timing.EndBeat,
            PrepStartBeat = timing.PrepStartBeat,
            CueSongPosition = getSongPositionAtBeat(timing.CueBeat),
            PlayerHitSongPosition = songPosition,
            EndSongPosition = getSongPositionAtBeat(timing.EndBeat),
            PrepStartSongPosition = getSongPositionAtBeat(timing.PrepStartBeat),
            Pattern = timing.Pattern,
            LaunchSide = timing.LaunchSide,
            ApplejackCueSide = timing.ApplejackCueSide,
            TargetSide = timing.TargetSide,
            ApplejackTargetSide = timing.ApplejackTargetSide,
            RainbowHigh = action.IsBigLeap,
            ApplejackHigh = action.HasBigCounterJump,
            IsExit = timing.IsExit,
            Judgement = SeeSawJudgement.Pending
        };
        timeline.PatternEvents.Add(patternEvent);

        if (timing.IsExit)
        {
            AddSegment(timeline, ref segmentId, currentEventId, SeeSawActor.Applejack, timing.PrepStartBeat, timing.PlayerHitBeat, getSongPositionAtBeat, state.ApplejackSide, SeeSawSide.Exit, high: false);
            AddImpact(timeline, ref impactId, currentEventId, SeeSawActor.Applejack, timing.PlayerHitBeat, getSongPositionAtBeat, SeeSawSide.Exit, SeeSawImpactKind.Exit, timing.ApplejackEndSource.JumpLength);
            state = ApplyTiming(timing);
            return;
        }

        if (timing.PrepStartBeat < timing.CueBeat)
            AddSegment(timeline, ref segmentId, currentEventId, SeeSawActor.Applejack, timing.PrepStartBeat, timing.CueBeat, getSongPositionAtBeat, state.ApplejackSide, timing.ApplejackCueSide, action.HasBigCounterJump);

        AddImpact(timeline, ref impactId, currentEventId, SeeSawActor.Applejack, timing.CueBeat, getSongPositionAtBeat, timing.ApplejackCueSide, SeeSawImpactKind.Cue, timing.ApplejackCueSource.JumpLength);
        AddSegment(timeline, ref segmentId, currentEventId, SeeSawActor.RainbowDash, timing.CueBeat, timing.PlayerHitBeat, getSongPositionAtBeat, timing.LaunchSide, timing.TargetSide, action.IsBigLeap);
        AddImpact(timeline, ref impactId, currentEventId, SeeSawActor.RainbowDash, timing.PlayerHitBeat, getSongPositionAtBeat, timing.TargetSide, SeeSawImpactKind.PlayerHit, timing.RainbowSource.JumpLength);

        if (timing.EndBeat > timing.PlayerHitBeat)
        {
            SeeSawImpactKind impactKind = timing.ApplejackTargetSide == SeeSawSide.Exit
                ? SeeSawImpactKind.Exit
                : SeeSawImpactKind.RelayEnd;
            bool high = timing.ApplejackTargetSide != SeeSawSide.Exit && action.HasBigCounterJump;
            AddSegment(timeline, ref segmentId, currentEventId, SeeSawActor.Applejack, timing.PlayerHitBeat, timing.EndBeat, getSongPositionAtBeat, timing.ApplejackCueSide, timing.ApplejackTargetSide, high);
            AddImpact(timeline, ref impactId, currentEventId, SeeSawActor.Applejack, timing.EndBeat, getSongPositionAtBeat, timing.ApplejackTargetSide, impactKind, timing.ApplejackEndSource.JumpLength);
        }

        if (sourceNote != null)
            timeline.NoteToEventId[sourceNote] = currentEventId;

        state = ApplyTiming(timing);
    }

    private static SeeSawLogicalState ApplyTiming(SeeSawCompiledEventTiming timing)
    {
        double applejackAvailableBeat = timing.IsExit || timing.EndBeat > timing.PlayerHitBeat + SameBeatEpsilon
            ? timing.EndBeat
            : timing.CueBeat;
        return new SeeSawLogicalState(timing.TargetSide, timing.ApplejackTargetSide, applejackAvailableBeat);
    }

    private static string CreateInvalidTimingError(double songPosition, double hitBeat, SeeSawCompiledEventTiming timing)
    {
        string reason = string.IsNullOrWhiteSpace(timing.InvalidReason) ? "Invalid timing" : timing.InvalidReason;
        return $"Invalid See-Saw timing at {songPosition:0.###}s ({hitBeat:0.###}b): {reason}";
    }

    private static List<ChartNote> CreateSortedChartNotes(IReadOnlyList<ChartNote> notes)
    {
        List<ChartNoteSortEntry> entries = new();
        for (int i = 0; i < notes.Count; i++)
            entries.Add(new ChartNoteSortEntry(notes[i], i));

        entries.Sort(CompareChartNoteSortEntries);

        List<ChartNote> sortedNotes = new(entries.Count);
        foreach (ChartNoteSortEntry entry in entries)
            sortedNotes.Add(entry.Note);

        return sortedNotes;
    }

    private static List<ChartNote> CreateSortedChartNotes(IReadOnlyList<ChartNote> notes, Func<ChartNote, double> getNoteBeat)
    {
        List<ChartNoteSortEntry> entries = new();
        for (int i = 0; i < notes.Count; i++)
            entries.Add(new ChartNoteSortEntry(notes[i], i));

        entries.Sort((a, b) => CompareChartNoteSortEntries(a, b, getNoteBeat));

        List<ChartNote> sortedNotes = new(entries.Count);
        foreach (ChartNoteSortEntry entry in entries)
            sortedNotes.Add(entry.Note);

        return sortedNotes;
    }

    private static int CompareChartNoteSortEntries(ChartNoteSortEntry a, ChartNoteSortEntry b)
    {
        int byTime = a.Note.SongPosition.CompareTo(b.Note.SongPosition);
        return byTime != 0 ? byTime : a.Order.CompareTo(b.Order);
    }

    private static int CompareChartNoteSortEntries(ChartNoteSortEntry a, ChartNoteSortEntry b, Func<ChartNote, double> getNoteBeat)
    {
        int byBeat = BeatTick.FromBeat(getNoteBeat(a.Note)).CompareTo(BeatTick.FromBeat(getNoteBeat(b.Note)));
        return byBeat != 0 ? byBeat : a.Order.CompareTo(b.Order);
    }

    private readonly struct ChartNoteSortEntry
    {
        public ChartNoteSortEntry(ChartNote note, int order)
        {
            Note = note;
            Order = order;
        }

        public ChartNote Note { get; }
        public int Order { get; }
    }

    private static void AddSegment(SeeSawTimeline timeline, ref int segmentId, int eventId, SeeSawActor actor, double startBeat, double endBeat, Func<double, double> getSongPositionAtBeat, SeeSawSide fromSide, SeeSawSide toSide, bool high)
    {
        if (endBeat <= startBeat)
            return;

        timeline.JumpSegments.Add(new SeeSawJumpSegment
        {
            Id = segmentId++,
            EventId = eventId,
            Actor = actor,
            StartBeat = startBeat,
            EndBeat = endBeat,
            StartSongPosition = getSongPositionAtBeat(startBeat),
            EndSongPosition = getSongPositionAtBeat(endBeat),
            FromSide = fromSide,
            ToSide = toSide,
            High = high,
            PathId = GetPathId(actor, fromSide, toSide, high)
        });
    }

    private static void AddImpact(SeeSawTimeline timeline, ref int impactId, int eventId, SeeSawActor actor, double beat, Func<double, double> getSongPositionAtBeat, SeeSawSide side, SeeSawImpactKind kind, SeeSawJumpLength jumpLength)
    {
        timeline.ImpactEvents.Add(new SeeSawImpactEvent
        {
            Id = impactId++,
            PatternEventId = eventId,
            Actor = actor,
            Beat = beat,
            SongPosition = getSongPositionAtBeat(beat),
            Side = side,
            Kind = kind,
            JumpLength = jumpLength
        });
    }

    private static SeeSawPathId GetPathId(SeeSawActor actor, SeeSawSide fromSide, SeeSawSide toSide, bool high)
    {
        if (actor == SeeSawActor.RainbowDash)
        {
            return (fromSide, toSide, high) switch
            {
                (SeeSawSide.Outer, SeeSawSide.Inner, true) => SeeSawPathId.RainbowHighOutIn,
                (SeeSawSide.Inner, SeeSawSide.Outer, true) => SeeSawPathId.RainbowHighInOut,
                (SeeSawSide.Inner, SeeSawSide.Inner, true) => SeeSawPathId.RainbowHighInIn,
                (SeeSawSide.Outer, SeeSawSide.Inner, false) => SeeSawPathId.RainbowOutIn,
                (SeeSawSide.Inner, SeeSawSide.Outer, false) => SeeSawPathId.RainbowInOut,
                (SeeSawSide.Inner, SeeSawSide.Inner, false) => SeeSawPathId.RainbowInIn,
                (_, _, true) => SeeSawPathId.RainbowHighOutOut,
                _ => SeeSawPathId.RainbowOutOut
            };
        }

        if (fromSide == SeeSawSide.Exit)
        {
            if (high)
                return toSide == SeeSawSide.Inner ? SeeSawPathId.ApplejackHighStartIn : SeeSawPathId.ApplejackHighStartOut;

            return toSide == SeeSawSide.Inner ? SeeSawPathId.ApplejackStartIn : SeeSawPathId.ApplejackStartOut;
        }

        if (toSide == SeeSawSide.Exit)
        {
            if (high)
                return fromSide == SeeSawSide.Inner ? SeeSawPathId.ApplejackHighEndIn : SeeSawPathId.ApplejackHighEndOut;

            return fromSide == SeeSawSide.Inner ? SeeSawPathId.ApplejackEndIn : SeeSawPathId.ApplejackEndOut;
        }

        return (fromSide, toSide, high) switch
        {
            (SeeSawSide.Outer, SeeSawSide.Inner, true) => SeeSawPathId.ApplejackHighOutIn,
            (SeeSawSide.Inner, SeeSawSide.Outer, true) => SeeSawPathId.ApplejackHighInOut,
            (SeeSawSide.Inner, SeeSawSide.Inner, true) => SeeSawPathId.ApplejackHighInIn,
            (SeeSawSide.Outer, SeeSawSide.Inner, false) => SeeSawPathId.ApplejackOutIn,
            (SeeSawSide.Inner, SeeSawSide.Outer, false) => SeeSawPathId.ApplejackInOut,
            (SeeSawSide.Inner, SeeSawSide.Inner, false) => SeeSawPathId.ApplejackInIn,
            (_, _, true) => SeeSawPathId.ApplejackHighOutOut,
            _ => SeeSawPathId.ApplejackOutOut
        };
    }
}

public readonly struct SeeSawLayout
{
    public SeeSawLayout(Vector2 applejackOuter, Vector2 applejackInner, Vector2 applejackExit, Vector2 rainbowOuter, Vector2 rainbowInner)
    {
        ApplejackOuter = applejackOuter;
        ApplejackInner = applejackInner;
        ApplejackExit = applejackExit;
        RainbowOuter = rainbowOuter;
        RainbowInner = rainbowInner;
    }

    public Vector2 ApplejackOuter { get; }
    public Vector2 ApplejackInner { get; }
    public Vector2 ApplejackExit { get; }
    public Vector2 RainbowOuter { get; }
    public Vector2 RainbowInner { get; }

    public Vector2 GetPosition(SeeSawActor actor, SeeSawSide side)
    {
        if (actor == SeeSawActor.RainbowDash)
            return side == SeeSawSide.Inner ? RainbowInner : RainbowOuter;

        return side switch
        {
            SeeSawSide.Inner => ApplejackInner,
            SeeSawSide.Exit => ApplejackExit,
            _ => ApplejackOuter
        };
    }
}

public readonly struct SeeSawPose
{
    public SeeSawPose(Vector2 position, float progression, float visualProgression = -1f, bool isVisualApexHold = false)
    {
        Position = position;
        Progression = progression;
        VisualProgression = visualProgression < 0f ? progression : visualProgression;
        IsVisualApexHold = isVisualApexHold;
    }

    public Vector2 Position { get; }
    public float Progression { get; }
    public float VisualProgression { get; }
    public bool IsVisualApexHold { get; }
}

public sealed class SeeSawPathCatalog
{
    private const float OuterJumpHeight = 787.5f;
    private const float InnerJumpHeight = 315f;
    private const float ExitJumpHeight = 980f;
    private const float BigLeapJumpHeight = 3200f;
    private const float RainbowHighLaunchEnd = 0.22f;
    private const float RainbowHighHangEnd = 0.46f;
    private const float RainbowHighHangStartProgression = 0.47f;
    private const float RainbowHighHangEndProgression = 0.55f;

    private readonly SeeSawLayout _layout;

    public SeeSawPathCatalog(SeeSawLayout layout)
    {
        _layout = layout;
    }

    public SeeSawPose GetPose(SeeSawJumpSegment segment, double beat)
    {
        float progression = segment.EndBeat <= segment.StartBeat
            ? 1f
            : (float)RhythmVisualUtils.GetProgression(segment.StartBeat, segment.EndBeat, beat);
        Vector2 from = _layout.GetPosition(segment.Actor, segment.FromSide);
        Vector2 to = _layout.GetPosition(segment.Actor, segment.ToSide);
        float height = GetJumpHeight(segment);
        float visualProgression = GetVisualProgression(segment, progression);
        bool isVisualApexHold = IsRainbowHighJumpApexHold(segment, progression);

        Vector2 basePosition = Vector2.Lerp(from, to, progression);
        Vector2 position = new(basePosition.X, basePosition.Y - height * RhythmVisualUtils.SineArcHeight(visualProgression));
        return new SeeSawPose(position, progression, visualProgression, isVisualApexHold);
    }

    public SeeSawPose GetGroundedPose(SeeSawActor actor, SeeSawSide side)
    {
        return new SeeSawPose(_layout.GetPosition(actor, side), 1f);
    }

    private static float GetJumpHeight(SeeSawJumpSegment segment)
    {
        if (segment.High)
            return BigLeapJumpHeight;

        if (segment.FromSide == SeeSawSide.Exit && segment.ToSide == SeeSawSide.Exit)
            return 0f;

        if (segment.FromSide == SeeSawSide.Exit || segment.ToSide == SeeSawSide.Exit)
            return ExitJumpHeight;

        return segment.FromSide == SeeSawSide.Inner ? InnerJumpHeight : OuterJumpHeight;
    }

    private static float GetVisualProgression(SeeSawJumpSegment segment, float progression)
    {
        if (segment?.Actor != SeeSawActor.RainbowDash || !segment.High)
            return progression;

        return GetRainbowHighJumpVisualProgression(progression);
    }

    private static float GetRainbowHighJumpVisualProgression(float progression)
    {
        progression = Math.Clamp(progression, 0f, 1f);
        if (progression <= RainbowHighLaunchEnd)
        {
            float launchProgress = progression / RainbowHighLaunchEnd;
            return MathHelper.Lerp(0f, RainbowHighHangStartProgression, Interpolation.EaseOutQuart(launchProgress));
        }

        if (progression <= RainbowHighHangEnd)
        {
            float hangProgress = (progression - RainbowHighLaunchEnd) / (RainbowHighHangEnd - RainbowHighLaunchEnd);
            return MathHelper.Lerp(RainbowHighHangStartProgression, RainbowHighHangEndProgression, Interpolation.EaseInOutSine(hangProgress));
        }

        float descentProgress = (progression - RainbowHighHangEnd) / (1f - RainbowHighHangEnd);
        return MathHelper.Lerp(RainbowHighHangEndProgression, 1f, Interpolation.EaseInQuart(descentProgress));
    }

    private static bool IsRainbowHighJumpApexHold(SeeSawJumpSegment segment, float progression)
    {
        return segment?.Actor == SeeSawActor.RainbowDash
            && segment.High
            && progression > RainbowHighLaunchEnd
            && progression <= RainbowHighHangEnd;
    }
}

public sealed class SeeSawActorController
{
    private const float ApplejackTiltDegrees = -15f;
    private const float RainbowTiltDegrees = 10f;
    private const string JumpState = "jump";
    private const string FallState = "fall";
    private const string LandState = "land";
    private const string FailState = "fail";
    private const string IdleState = "idle";
    private const string ApplejackStartIdleState = "start_idle";

    private readonly SeeSawActor _actor;
    private readonly GameObject _gameObject;
    private readonly AnimationStateMachine _stateMachine;

    public SeeSawActorController(SeeSawActor actor, GameObject gameObject, AnimationStateMachine stateMachine)
    {
        _actor = actor;
        _gameObject = gameObject;
        _stateMachine = stateMachine;
    }

    public GameObject GameObject => _gameObject;

    public void ApplyPose(SeeSawPose pose)
    {
        if (_gameObject != null)
            _gameObject.Position = pose.Position;
    }

    public void StartJump(SeeSawJumpSegment segment)
    {
        RhythmVisualUtils.ForceAnimationState(_stateMachine, JumpState);
    }

    public void SetAirPhase(SeeSawJumpSegment segment, float t, bool force = false)
    {
        if (!force && IsFeedbackStateActive())
            return;

        RhythmVisualUtils.ForceAnimationState(_stateMachine, t < 0.5f ? JumpState : FallState);
    }

    public void Land(SeeSawImpactEvent impact)
    {
        if (_actor == SeeSawActor.Applejack && _gameObject != null)
        {
            _gameObject.Rotation = impact.Side == SeeSawSide.Exit ? 0f : MathHelper.ToRadians(ApplejackTiltDegrees);
            if (_gameObject.sprite != null && impact.Side == SeeSawSide.Exit)
                _gameObject.sprite.DrawOffset = Vector2.Zero;

            if (impact.Side == SeeSawSide.Exit)
            {
                RhythmVisualUtils.ForceAnimationState(_stateMachine, ApplejackStartIdleState);
                return;
            }
        }

        RhythmVisualUtils.ForceAnimationState(_stateMachine, LandState, JumpState);
    }

    public void ApplyJudgement(SeeSawJudgement judgement)
    {
        if (_actor != SeeSawActor.RainbowDash)
            return;

        switch (judgement)
        {
            case SeeSawJudgement.Just:
            case SeeSawJudgement.Barely:
                RhythmVisualUtils.ForceAnimationState(_stateMachine, LandState, JumpState);
                break;
            case SeeSawJudgement.Miss:
                RhythmVisualUtils.ForceAnimationState(_stateMachine, FailState);
                break;
        }
    }

    public void SetIdleForSide(SeeSawSide _, bool force = false)
    {
        if (!force && IsEventStatePlaying())
            return;

        string state = GetIdleStateForSide(_);
        if (force)
            _stateMachine?.ForceState(state);
        else
            RhythmVisualUtils.ForceAnimationState(_stateMachine, state);

        ApplyGroundedRotation(_);
    }

    public void ResetToIdle(SeeSawSide _)
    {
        string state = GetIdleStateForSide(_);
        _stateMachine?.ForceState(state);
        ApplyGroundedRotation(_);
    }

    private void ApplyGroundedRotation(SeeSawSide side)
    {
        if (_gameObject == null)
            return;

        _gameObject.Rotation = _actor switch
        {
            SeeSawActor.Applejack => side == SeeSawSide.Exit ? 0f : MathHelper.ToRadians(ApplejackTiltDegrees),
            SeeSawActor.RainbowDash => MathHelper.ToRadians(RainbowTiltDegrees),
            _ => _gameObject.Rotation
        };
    }

    private string GetIdleStateForSide(SeeSawSide side)
    {
        return _actor == SeeSawActor.Applejack && side == SeeSawSide.Exit ? ApplejackStartIdleState : IdleState;
    }

    private bool IsFeedbackStateActive()
    {
        string stateName = _stateMachine?.CurrentState?.Name;
        return _actor == SeeSawActor.RainbowDash
            && (stateName == LandState || stateName == FailState)
            && _stateMachine.StateProgress < 1f;
    }

    private bool IsEventStatePlaying()
    {
        string stateName = _stateMachine?.CurrentState?.Name;
        return (stateName == LandState || stateName == FailState) && _stateMachine.StateProgress < 1f;
    }
}

public sealed class SeeSawBeamController
{
    private const string IdleLeftState = "idle_left";
    private const string IdleRightState = "idle_right";
    private const string LandLeftState = "land_left";
    private const string LandRightState = "land_right";

    private readonly GameObject _gameObject;
    private readonly AnimationStateMachine _stateMachine;

    public SeeSawBeamController(GameObject gameObject, AnimationStateMachine stateMachine)
    {
        _gameObject = gameObject;
        _stateMachine = stateMachine;
    }

    public void Reset()
    {
        if (_gameObject != null)
            _gameObject.Rotation = 0f;

        _stateMachine?.ForceState(IdleRightState);
    }

    public void LandToward(SeeSawActor actor)
    {
        RhythmVisualUtils.ForceAnimationState(_stateMachine, actor == SeeSawActor.RainbowDash ? LandRightState : LandLeftState);
    }

    public void SetIdleToward(SeeSawActor actor, bool force = false)
    {
        if (!force && IsLandStatePlaying())
            return;

        string state = actor == SeeSawActor.RainbowDash ? IdleRightState : IdleLeftState;
        if (force)
            _stateMachine?.ForceState(state);
        else
            RhythmVisualUtils.ForceAnimationState(_stateMachine, state);
    }

    private bool IsLandStatePlaying()
    {
        string stateName = _stateMachine?.CurrentState?.Name;
        return (stateName == LandLeftState || stateName == LandRightState) && _stateMachine.StateProgress < 1f;
    }
}

public sealed class SeeSawCameraController
{
    private const float BigLeapCameraTargetViewportY = 220f;

    private readonly Camera _camera;
    private readonly Viewport _viewport;

    public SeeSawCameraController(Camera camera)
    {
        _camera = camera;
        _viewport = GLOBALS.graphicsDevice.Viewport;
    }

    public void Apply(SeeSawJumpSegment rainbowSegment, SeeSawPose rainbowPose, SeeSawJumpSegment applejackSegment, SeeSawPose applejackPose)
    {
        if (_camera == null)
            return;

        if (rainbowSegment != null && rainbowSegment.High)
        {
            ApplyHighJump(rainbowPose);
            return;
        }

        _camera.Position = Vector2.Zero;
    }

    public void Reset()
    {
        if (_camera != null)
            _camera.Position = Vector2.Zero;
    }

    private void ApplyHighJump(SeeSawPose pose)
    {
        float t = RhythmVisualUtils.SineArcHeight(pose.VisualProgression);
        float targetY = pose.Position.Y - BigLeapCameraTargetViewportY;
        _camera.Position = new Vector2(0f, MathHelper.Lerp(0f, targetY, t));
    }
}

public sealed class SeeSawSoundScheduler
{
    private const float SeeSawSfxVolume = 0.75f;
    private const string ApplejackHiHouSfx = "SFX/SeeSaw/applejack-hihou.wav";
    private const string ApplejackShortSfx = "SFX/SeeSaw/applejack-short.wav";
    private const string RainbowDashAHouhSfx = "SFX/SeeSaw/rainbowdash-ahouh.wav";
    private const string RainbowDashShortSfx = "SFX/SeeSaw/rainbowdash-short.wav";
    private const string RainbowDashHighSfx = "SFX/SeeSaw/rainbowdash-high.wav";
    private const string RainbowDashHighLandSfx = "SFX/SeeSaw/rainbowdash-high-land.wav";
    private const string SeeSawLongSwingSfx = "SFX/SeeSaw/see_saw_long.wav";
    private const string SeeSawShortSwingSfx = "SFX/SeeSaw/see_saw_short.wav";

    private readonly Scene _scene;
    private readonly Dictionary<string, string> _slots = new()
    {
        ["other_jump"] = ApplejackHiHouSfx,
        ["other_land"] = ApplejackHiHouSfx,
        ["applejack_short"] = ApplejackShortSfx,
        ["rainbowdash_short"] = RainbowDashShortSfx,
        ["rainbow_high"] = RainbowDashHighSfx,
        ["rainbow_high_land"] = RainbowDashHighLandSfx,
        ["see_saw_long"] = SeeSawLongSwingSfx,
        ["see_saw_short"] = SeeSawShortSwingSfx,
        ["just"] = RainbowDashAHouhSfx
    };

    public SeeSawSoundScheduler(Scene scene)
    {
        _scene = scene;
    }

    public void OnImpact(SeeSawImpactEvent impact, SeeSawPatternEvent patternEvent)
    {
        if (impact.Kind != SeeSawImpactKind.Exit)
            PlaySlot(impact.JumpLength == SeeSawJumpLength.Long ? "see_saw_long" : "see_saw_short", 1.25f);

        string slot = impact.Kind switch
        {
            SeeSawImpactKind.PlayerHit => null,
            SeeSawImpactKind.Exit => "exit",
            _ => null
        };

        PlaySlot(slot);
    }

    public void OnJumpStart(SeeSawJumpSegment segment)
    {
        if (segment == null || segment.ToSide == SeeSawSide.Exit)
            return;

        PlaySlot(GetActorSlot(segment.Actor, SeeSawTiming.GetJumpLengthFromSide(segment.FromSide)));
    }

    private static string GetActorSlot(SeeSawActor actor, SeeSawJumpLength jumpLength)
    {
        if (jumpLength == SeeSawJumpLength.Short)
            return actor == SeeSawActor.RainbowDash ? "rainbowdash_short" : "applejack_short";

        return actor == SeeSawActor.RainbowDash ? "just" : "other_land";
    }

    public void OnJudgement(SeeSawJudgement judgement)
    {
        PlaySlot(judgement == SeeSawJudgement.Miss ? "miss" : "just");
    }

    public void OnRainbowHighApex()
    {
        PlaySlot("rainbow_high", 4f);
    }

    public void OnRainbowHighLand()
    {
        PlaySlot("rainbow_high_land");
    }

    public void OnRainbowLand(SeeSawJumpLength jumpLength)
    {
        PlaySlot(GetActorSlot(SeeSawActor.RainbowDash, jumpLength));
    }

    private void PlaySlot(string slot, float volumeMultiplier = 1f)
    {
        if (_scene == null || string.IsNullOrWhiteSpace(slot) || !_slots.TryGetValue(slot, out string filePath) || string.IsNullOrWhiteSpace(filePath))
            return;

        string resolvedPath = Path.IsPathRooted(filePath)
            ? filePath
            : Path.Combine(GameCore.Core.Content.RootDirectory, filePath);

        if (!File.Exists(resolvedPath))
            return;

        try
        {
            SFX.Play(_scene, filePath, MathF.Min(SeeSawSfxVolume, GLOBALS.SfxVolume * volumeMultiplier * SeeSawSfxVolume));
        }
        catch (InvalidOperationException)
        {
        }
    }
}

public sealed class SeeSawCameraEffectController
{
    private const float BigLeapSpeedLineSpeed = 3200.0f;
    private const float BigLeapSpeedLineAreaHeightRatio = 0.55f;

    private readonly Camera _camera;
    private readonly Viewport _viewport;
    private SpeedLinesCameraEffect _bigLeapEffect;

    public SeeSawCameraEffectController(Camera camera)
    {
        _camera = camera;
        _viewport = GLOBALS.graphicsDevice.Viewport;
    }

    public void ApplyRainbowHighJump(SeeSawJumpSegment rainbowSegment, SeeSawPose rainbowPose)
    {
        bool shouldShow = rainbowSegment?.High == true;
        if (shouldShow)
        {
            EnsureBigLeapEffect();
            if (_bigLeapEffect != null)
            {
                _bigLeapEffect.Speed = rainbowPose.VisualProgression < 0.5f ? BigLeapSpeedLineSpeed : -BigLeapSpeedLineSpeed;
                _bigLeapEffect.Area = GetSpeedLineArea(rainbowPose);
            }

            return;
        }

        ClearBigLeapEffect();
    }

    public void Reset()
    {
        ClearBigLeapEffect();
    }

    private void ClearBigLeapEffect()
    {
        _camera?.RemoveEffect(_bigLeapEffect);
        _bigLeapEffect = null;
    }

    private void EnsureBigLeapEffect()
    {
        if (_camera == null || _bigLeapEffect != null)
            return;

        _bigLeapEffect = new SpeedLinesCameraEffect
        {
            Orientation = SpeedLineOrientation.Vertical,
            LineCount = 55,
            MinLength = 180.0f,
            MaxLength = 520.0f,
            MinThickness = 6.0f,
            MaxThickness = 16.0f,
            Color = Color.Black * 0.85f,
            Speed = BigLeapSpeedLineSpeed,
            CenterGapRatio = 0.0f,
            RandomSeed = 123
        };

        _camera.AddEffect(_bigLeapEffect);
    }

    private Rectangle GetSpeedLineArea(SeeSawPose rainbowPose)
    {
        int areaHeight = Math.Max(1, (int)MathF.Round(_viewport.Height * BigLeapSpeedLineAreaHeightRatio));
        float rawTop;

        if (rainbowPose.VisualProgression < 0.5f)
        {
            float ascentProgress = MathHelper.Clamp(rainbowPose.VisualProgression / 0.5f, 0f, 1f);
            rawTop = MathHelper.Lerp(0f, _viewport.Height, ascentProgress);
        }
        else
        {
            float descentProgress = MathHelper.Clamp((rainbowPose.VisualProgression - 0.5f) / 0.5f, 0f, 1f);
            rawTop = MathHelper.Lerp(-areaHeight, 0f, descentProgress);
        }

        return new Rectangle(0, (int)MathF.Round(rawTop), _viewport.Width, areaHeight);
    }
}

public sealed class SeeSawDirector
{
    private const double RewindThresholdBeats = 0.001;
    private const double MissWindowSeconds = 0.25;

    private readonly SeeSawTimeline _timeline;
    private readonly SeeSawActorController _rainbow;
    private readonly SeeSawActorController _applejack;
    private readonly SeeSawBeamController _beam;
    private readonly TrailGameObject _rainbowTrail;
    private readonly SeeSawPathCatalog _pathCatalog;
    private readonly SeeSawCameraController _cameraController;
    private readonly SeeSawSoundScheduler _soundScheduler;
    private readonly SeeSawCameraEffectController _cameraEffectController;
    private readonly Func<double, double> _getBeatAt;
    private readonly Func<double, double> _getCrotchetAt;
    private readonly double _fallbackCrotchet;
    private readonly HashSet<int> _startedSegmentIds = new();
    private readonly HashSet<int> _triggeredImpactIds = new();
    private readonly HashSet<int> _triggeredRainbowHighApexSegmentIds = new();
    private double _lastSongPosition = double.NaN;
    private double _lastBeat = double.NaN;
    private double _currentBeat = double.NaN;
    private double _currentSongPosition;
    private Vector2 _lastRainbowTrailPosition;
    private bool _hasLastRainbowTrailPosition;

    public SeeSawDirector(SeeSawTimeline timeline, SeeSawActorController rainbow, SeeSawActorController applejack, SeeSawBeamController beam, TrailGameObject rainbowTrail, SeeSawPathCatalog pathCatalog, SeeSawCameraController cameraController, SeeSawSoundScheduler soundScheduler, double crotchet)
        : this(timeline, rainbow, applejack, beam, rainbowTrail, pathCatalog, cameraController, soundScheduler, null, null, _ => crotchet, crotchet)
    {
    }

    public SeeSawDirector(SeeSawTimeline timeline, SeeSawActorController rainbow, SeeSawActorController applejack, SeeSawBeamController beam, TrailGameObject rainbowTrail, SeeSawPathCatalog pathCatalog, SeeSawCameraController cameraController, SeeSawSoundScheduler soundScheduler, Func<double, double> getCrotchetAt, double fallbackCrotchet = 0.6)
        : this(timeline, rainbow, applejack, beam, rainbowTrail, pathCatalog, cameraController, soundScheduler, null, null, getCrotchetAt, fallbackCrotchet)
    {
    }

    public SeeSawDirector(SeeSawTimeline timeline, SeeSawActorController rainbow, SeeSawActorController applejack, SeeSawBeamController beam, TrailGameObject rainbowTrail, SeeSawPathCatalog pathCatalog, SeeSawCameraController cameraController, SeeSawSoundScheduler soundScheduler, SeeSawCameraEffectController cameraEffectController, Func<double, double> getBeatAt, Func<double, double> getCrotchetAt, double fallbackCrotchet = 0.6)
    {
        _timeline = timeline;
        _rainbow = rainbow;
        _applejack = applejack;
        _beam = beam;
        _rainbowTrail = rainbowTrail;
        _pathCatalog = pathCatalog;
        _cameraController = cameraController;
        _soundScheduler = soundScheduler;
        _cameraEffectController = cameraEffectController;
        _getBeatAt = getBeatAt;
        _getCrotchetAt = getCrotchetAt;
        _fallbackCrotchet = fallbackCrotchet > 0.0 ? fallbackCrotchet : 0.6;
    }

    public void Reset()
    {
        _lastSongPosition = double.NaN;
        _lastBeat = double.NaN;
        _currentBeat = double.NaN;
        _startedSegmentIds.Clear();
        _triggeredImpactIds.Clear();
        _triggeredRainbowHighApexSegmentIds.Clear();
        _timeline?.ResetJudgements();
        _cameraController?.Reset();
        _cameraEffectController?.Reset();
        _hasLastRainbowTrailPosition = false;
        ResetRainbowTrail();
        _beam?.Reset();
        _rainbow.ApplyPose(_pathCatalog.GetGroundedPose(SeeSawActor.RainbowDash, SeeSawSide.Outer));
        _applejack.ApplyPose(_pathCatalog.GetGroundedPose(SeeSawActor.Applejack, SeeSawSide.Exit));
        _rainbow.ResetToIdle(SeeSawSide.Outer);
        _applejack.ResetToIdle(SeeSawSide.Exit);
    }

    public void Update(double songPosition, GameTime gameTime)
    {
        double beat = _getBeatAt != null ? _getBeatAt(songPosition) : _currentBeat;
        Update(beat, songPosition, gameTime);
    }

    public void Update(double beat, double songPosition, GameTime gameTime)
    {
        if (_timeline == null)
            return;

        _currentSongPosition = songPosition;
        _currentBeat = beat;

        if (HasRewound(beat, songPosition))
        {
            _startedSegmentIds.Clear();
            _triggeredImpactIds.Clear();
            _triggeredRainbowHighApexSegmentIds.Clear();
            _timeline.ResetJudgements(beat);
        }

        FireCrossedEvents(beat, songPosition);

        SeeSawJumpSegment rainbowSegment = _timeline.GetActiveSegment(SeeSawActor.RainbowDash, beat);
        SeeSawJumpSegment applejackSegment = _timeline.GetActiveSegment(SeeSawActor.Applejack, beat);
        SeeSawPose rainbowPose = ApplyActor(SeeSawActor.RainbowDash, _rainbow, rainbowSegment, beat, songPosition);
        SeeSawPose applejackPose = ApplyActor(SeeSawActor.Applejack, _applejack, applejackSegment, beat, songPosition);

        ApplyRainbowTrail(rainbowSegment, rainbowPose);
        ApplyRainbowHighApexSfx(rainbowSegment, rainbowPose);
        _cameraEffectController?.ApplyRainbowHighJump(rainbowSegment, rainbowPose);
        ApplyBeamIdle(beat, songPosition);
        _cameraController?.Apply(rainbowSegment, rainbowPose, applejackSegment, applejackPose);

        _lastSongPosition = songPosition;
        _lastBeat = beat;
    }

    public void SyncTo(double beat, double songPosition)
    {
        if (_timeline == null)
            return;

        _currentSongPosition = songPosition;
        _currentBeat = beat;
        _lastSongPosition = double.NaN;
        _lastBeat = double.NaN;
        _startedSegmentIds.Clear();
        _triggeredImpactIds.Clear();
        _triggeredRainbowHighApexSegmentIds.Clear();
        _timeline.ResetJudgements();
        RestoreJudgementsFromNotes(beat);

        foreach (SeeSawJumpSegment segment in _timeline.JumpSegments)
        {
            if (segment.StartBeat <= beat)
                _startedSegmentIds.Add(segment.Id);
            else
                break;
        }

        foreach (SeeSawImpactEvent impact in _timeline.ImpactEvents)
        {
            if (impact.Beat > beat)
                break;

            _triggeredImpactIds.Add(impact.Id);
            ApplyImpact(impact, playSound: false);
        }

        SeeSawJumpSegment rainbowSegment = _timeline.GetActiveSegment(SeeSawActor.RainbowDash, beat);
        SeeSawJumpSegment applejackSegment = _timeline.GetActiveSegment(SeeSawActor.Applejack, beat);
        SeeSawPose rainbowPose = ApplyActor(SeeSawActor.RainbowDash, _rainbow, rainbowSegment, beat, songPosition, forceVisualState: true);
        SeeSawPose applejackPose = ApplyActor(SeeSawActor.Applejack, _applejack, applejackSegment, beat, songPosition, forceVisualState: true);

        ApplyRainbowTrail(rainbowSegment, rainbowPose);
        _cameraEffectController?.ApplyRainbowHighJump(rainbowSegment, rainbowPose);
        ApplyBeamIdle(beat, songPosition, force: true);
        _cameraController?.Apply(rainbowSegment, rainbowPose, applejackSegment, applejackPose);

        _lastSongPosition = songPosition;
        _lastBeat = beat;
    }

    public void ApplyReaction(NoteReactionResult result, Note note)
    {
        SeeSawPatternEvent patternEvent = null;
        if (note != null)
            _timeline.TryGetEventForNote(note, out patternEvent);

        if (patternEvent == null && result == NoteReactionResult.MISS)
            patternEvent = FindCurrentPendingEventInMissWindow();

        if (patternEvent == null || patternEvent.IsExit)
            return;

        SeeSawJudgement judgement = ToJudgement(result);
        patternEvent.Judgement = judgement;
        _rainbow.ApplyJudgement(judgement);

        if (patternEvent.RainbowHigh && judgement != SeeSawJudgement.Miss)
        {
            _soundScheduler?.OnRainbowHighLand();
        }
        else if (judgement == SeeSawJudgement.Miss)
        {
            _soundScheduler?.OnJudgement(judgement);
        }
    }

    private SeeSawPose ApplyActor(SeeSawActor actor, SeeSawActorController controller, SeeSawJumpSegment segment, double beat, double songPosition, bool forceVisualState = false)
    {
        if (segment != null)
        {
            double segmentBeat = GetSegmentBeat(segment, beat, songPosition);
            SeeSawPose pose = _pathCatalog.GetPose(segment, segmentBeat);
            controller.ApplyPose(pose);
            controller.SetAirPhase(segment, pose.Progression, forceVisualState);
            return pose;
        }

        SeeSawSide side = _timeline.GetLastGroundedSide(actor, beat);
        SeeSawPose groundedPose = _pathCatalog.GetGroundedPose(actor, side);
        controller.ApplyPose(groundedPose);
        controller.SetIdleForSide(side, forceVisualState);
        return groundedPose;
    }

    private void FireCrossedEvents(double beat, double songPosition)
    {
        if (double.IsNaN(_lastBeat) || beat < _lastBeat)
            return;

        foreach (SeeSawJumpSegment segment in _timeline.JumpSegments)
        {
            if (_startedSegmentIds.Contains(segment.Id) || !Crossed(segment.StartBeat, beat, segment.StartSongPosition, songPosition))
                continue;

            _startedSegmentIds.Add(segment.Id);
            GetController(segment.Actor).StartJump(segment);
            _soundScheduler?.OnJumpStart(segment);
        }

        foreach (SeeSawImpactEvent impact in _timeline.ImpactEvents)
        {
            if (_triggeredImpactIds.Contains(impact.Id) || !Crossed(impact.Beat, beat, impact.SongPosition, songPosition))
                continue;

            _triggeredImpactIds.Add(impact.Id);
            ApplyImpact(impact);
        }
    }

    private void ApplyImpact(SeeSawImpactEvent impact, bool playSound = true)
    {
        _timeline.TryGetEvent(impact.PatternEventId, out SeeSawPatternEvent patternEvent);

        if (impact.Kind == SeeSawImpactKind.PlayerHit)
        {
            if (patternEvent?.Judgement == SeeSawJudgement.Just || patternEvent?.Judgement == SeeSawJudgement.Barely)
                _rainbow.Land(impact);
        }
        else
        {
            GetController(impact.Actor).Land(impact);
        }

        if (impact.Kind != SeeSawImpactKind.Exit)
            _beam?.LandToward(impact.Actor);

        if (playSound)
            _soundScheduler?.OnImpact(impact, patternEvent);
    }

    private void ApplyRainbowTrail(SeeSawJumpSegment rainbowSegment, SeeSawPose rainbowPose)
    {
        if (_rainbowTrail == null)
            return;

        Vector2 trailPosition = GetRainbowTrailPosition(rainbowPose);
        _rainbowTrail.Position = trailPosition;
        _rainbowTrail.Scale = Vector2.One;
        _rainbowTrail.Rotation = 0f;

        bool shouldEmit = rainbowSegment?.High == true && !rainbowPose.IsVisualApexHold;
        _lastRainbowTrailPosition = trailPosition;
        _hasLastRainbowTrailPosition = true;

        if (_rainbowTrail.EmitTrail == shouldEmit)
            return;

        _rainbowTrail.EmitTrail = shouldEmit;
        if (shouldEmit)
            _rainbowTrail.ClearTrail();
    }

    private void ApplyRainbowHighApexSfx(SeeSawJumpSegment rainbowSegment, SeeSawPose rainbowPose)
    {
        if (rainbowSegment?.High != true || !rainbowPose.IsVisualApexHold || !_triggeredRainbowHighApexSegmentIds.Add(rainbowSegment.Id))
            return;

        _soundScheduler?.OnRainbowHighApex();
    }

    private void ResetRainbowTrail()
    {
        if (_rainbowTrail == null)
            return;

        _rainbowTrail.EmitTrail = false;
        _rainbowTrail.Position = GetRainbowTrailPosition(_rainbow.GameObject?.Position ?? Vector2.Zero, _rainbow?.GameObject?.sprite);
        _rainbowTrail.Scale = Vector2.One;
        _hasLastRainbowTrailPosition = false;
        _rainbowTrail.ClearTrail();
    }

    private Vector2 GetRainbowTrailPosition(SeeSawPose rainbowPose)
    {
        return GetRainbowTrailPosition(rainbowPose.Position, _rainbow?.GameObject?.sprite);
    }

    private static Vector2 GetRainbowTrailPosition(Vector2 rainbowPosition, GameCore.Graphics.Sprite rainbowSprite)
    {
        if (rainbowSprite == null || rainbowSprite.Region == null)
            return rainbowPosition;

        return rainbowPosition;
    }

    private void ApplyBeamIdle(double beat, double songPosition, bool force = false)
    {
        if (_beam == null)
            return;

        SeeSawImpactEvent lastImpact = GetLastBeamImpact(beat, songPosition);
        _beam.SetIdleToward(lastImpact?.Actor ?? SeeSawActor.RainbowDash, force);
    }

    private void RestoreJudgementsFromNotes(double beat)
    {
        foreach (SeeSawPatternEvent patternEvent in _timeline.PatternEvents)
        {
            if (patternEvent.PlayerHitBeat > beat)
                break;

            if (patternEvent.IsExit || patternEvent.SourceNote == null)
                continue;

            if (patternEvent.SourceNote.HasBeenMissed)
                patternEvent.Judgement = SeeSawJudgement.Miss;
            else if (patternEvent.SourceNote.HasReacted)
                patternEvent.Judgement = SeeSawJudgement.Just;
        }
    }

    private SeeSawImpactEvent GetLastBeamImpact(double beat, double songPosition)
    {
        SeeSawImpactEvent lastImpact = null;
        foreach (SeeSawImpactEvent impact in _timeline.ImpactEvents)
        {
            bool isAfter = impact.Beat > beat;

            if (isAfter)
                break;

            if (impact.Kind != SeeSawImpactKind.Exit)
                lastImpact = impact;
        }

        return lastImpact;
    }

    private SeeSawActorController GetController(SeeSawActor actor)
    {
        return actor == SeeSawActor.RainbowDash ? _rainbow : _applejack;
    }

    private bool HasRewound(double beat, double songPosition)
    {
        return !double.IsNaN(_lastBeat)
            ? beat < _lastBeat - RewindThresholdBeats
            : !double.IsNaN(_lastSongPosition) && songPosition < _lastSongPosition - RewindThresholdBeats * GetCrotchetAt(_lastSongPosition);
    }

    private bool Crossed(double eventBeat, double currentBeat, double eventSongPosition, double currentSongPosition)
    {
        BeatTick eventTick = BeatTick.FromBeat(eventBeat);
        return eventTick > BeatTick.FromBeat(_lastBeat) && eventTick <= BeatTick.FromBeat(currentBeat);
    }

    private static double GetSegmentBeat(SeeSawJumpSegment segment, double fallbackBeat, double songPosition)
    {
        if (segment == null || segment.EndBeat <= segment.StartBeat)
            return fallbackBeat;

        return Math.Clamp(fallbackBeat, segment.StartBeat, segment.EndBeat);
    }

    private SeeSawPatternEvent FindCurrentPendingEventInMissWindow()
    {
        if (double.IsNaN(_currentBeat))
            return null;

        SeeSawPatternEvent candidate = null;
        foreach (SeeSawPatternEvent patternEvent in _timeline.PatternEvents)
        {
            if (patternEvent.IsExit || patternEvent.Judgement != SeeSawJudgement.Pending)
                continue;

            if (_currentSongPosition >= patternEvent.PlayerHitSongPosition && _currentSongPosition <= patternEvent.PlayerHitSongPosition + MissWindowSeconds)
                candidate = patternEvent;
        }

        return candidate;
    }

    private double GetCrotchetAt(double songPosition)
    {
        if (_getCrotchetAt == null)
            return _fallbackCrotchet;

        double crotchet = _getCrotchetAt(songPosition);
        return crotchet > 0.0 ? crotchet : _fallbackCrotchet;
    }

    private static SeeSawJudgement ToJudgement(NoteReactionResult result)
    {
        return result switch
        {
            NoteReactionResult.PERFECT => SeeSawJudgement.Just,
            NoteReactionResult.EARLY or NoteReactionResult.LATE => SeeSawJudgement.Barely,
            _ => SeeSawJudgement.Miss
        };
    }

}
