using Rhythm.Note;
using System;

namespace MLP_RiM.Elements.Editor;

public sealed class SeaponyParadeEditorNoteTiming : IEditorNoteTiming
{
    public NoteTimingResult GetTiming(NoteTimingRequest request)
    {
        return new NoteTimingResult(
            StartBeat: GetStartBeat(request),
            EndBeat: GetEndBeat(request),
            HitStartBeat: GetHitWindowStartBeat(request),
            HitEndBeat: GetHitWindowEndBeat(request),
            SameVariantHitStartBeat: GetSameVariantHitWindowStartBeat(request),
            SameVariantHitEndBeat: GetSameVariantHitWindowEndBeat(request));
    }

    private static double GetEndBeat(NoteTimingRequest request)
    {
        switch(GetSelectedAction(request))
        {
            case SeaponyAction.Swim:
                return request.Beat + 1;

            case SeaponyAction.Roll:
                return IsRollAfter(request)
                    ? request.GetBeat(request.GetNextNote(1))
                    : request.Beat + GetRollStopCueBeats(request);

            case SeaponyAction.TapTap:
                return request.Beat + 2;

            case SeaponyAction.Leave:
            case SeaponyAction.Enter:
                return request.Beat + GetGroupMoveHoldBeats(request);
        }

        return request.Beat;
    }

    private static double GetHitWindowEndBeat(NoteTimingRequest request)
    {
        switch(GetSelectedAction(request))
        {
            case SeaponyAction.Swim:
                return request.Beat + 2;

            case SeaponyAction.Roll:
                return IsRollAfter(request)
                    ? request.GetBeat(request.GetNextNote(1))
                    : GetEndBeat(request) + 1;

            case SeaponyAction.TapTap:
                return request.Beat + 2;

            case SeaponyAction.Leave:
            case SeaponyAction.Enter:
                return GetEndBeat(request);
        }

        return request.Beat;
    }

    private static double GetHitWindowStartBeat(NoteTimingRequest request)
    {
        switch(GetSelectedAction(request))
        {
            case SeaponyAction.Swim:
            case SeaponyAction.Roll:
            case SeaponyAction.TapTap:
            case SeaponyAction.Leave:
            case SeaponyAction.Enter:
                return request.Beat;
        }

        return request.Beat;
    }

    private static double GetStartBeat(NoteTimingRequest request)
    {
        switch(GetSelectedAction(request))
        {
            case SeaponyAction.Swim:
                return request.Beat - 1;

            case SeaponyAction.Roll:
                return IsRollBefore(request)
                    ? request.Beat
                    : request.Beat - 2;

            case SeaponyAction.TapTap:
                return IsTapTapBefore(request)
                    ? request.Beat
                    : request.Beat - 2;

            case SeaponyAction.Leave:
            case SeaponyAction.Enter:
                return request.Beat;
        }

        return request.Beat;
    }

    private static double GetSameVariantHitWindowStartBeat(NoteTimingRequest request)
    {
        SeaponyAction action = GetSelectedAction(request);
        if(action == SeaponyAction.Roll || action == SeaponyAction.TapTap)
            return request.Beat;

        return GetHitWindowStartBeat(request);
    }

    private static double GetSameVariantHitWindowEndBeat(NoteTimingRequest request)
    {
        SeaponyAction action = GetSelectedAction(request);
        if(action == SeaponyAction.Roll)
            return request.Beat + 1;

        if(action == SeaponyAction.TapTap)
            return IsNextTapTapInSamePair(request)
                ? request.Beat + 0.5
                : request.Beat + 1;

        return GetHitWindowEndBeat(request);
    }

    private static SeaponyAction GetSelectedAction(NoteTimingRequest request)
    {
        if (request.Note != null)
            return SeaponyNoteCodec.ReadAction(request.Note.AdditionnalData);

        return SeaponyNoteCodec.ReadAction(request.Definition.GetVariant(request.NoteVariantIndex).AdditionnalData);
    }

    private static bool IsRollBefore(NoteTimingRequest request)
    {
        return IsRoll(request.GetPreviousNote(1));
    }

    private static bool IsRollAfter(NoteTimingRequest request)
    {
        return IsRoll(request.GetNextNote(1));
    }

    private static bool IsRoll(ChartNote note)
    {
        return SeaponyNoteCodec.IsAction(note?.AdditionnalData, SeaponyAction.Roll);
    }

    private static bool IsTapTapBefore(NoteTimingRequest request)
    {
        return IsTapTap(request.GetPreviousNote(1));
    }

    private static bool IsTapTapAfter(NoteTimingRequest request)
    {
        return IsTapTap(request.GetNextNote(1));
    }

    private static bool IsNextTapTapInSamePair(NoteTimingRequest request)
    {
        ChartNote nextNote = request.GetNextNote(1);
        return nextNote != null
            && IsTapTap(nextNote)
            && request.GetBeat(nextNote) <= request.Beat + 0.5 + 0.000001;
    }

    private static bool IsTapTap(ChartNote note)
    {
        return SeaponyNoteCodec.IsAction(note?.AdditionnalData, SeaponyAction.TapTap);
    }

    private static int GetRollStopCueBeats(NoteTimingRequest request)
    {
        int rollCount = 1;
        for (int offset = 1; IsRoll(request.GetPreviousNote(offset)); offset++)
            rollCount++;

        int paddingBeats = (4 - rollCount % 4) % 4;
        if(paddingBeats == 0)
            paddingBeats = 1;

        return paddingBeats;
    }

    private static double GetGroupMoveHoldBeats(NoteTimingRequest request)
    {
        if(request.Note != null)
            return Math.Max(0.0, ChartTiming.GetNoteHoldBeats(request.Note, request.Definition, request.TempoMap));

        return GetSelectedAction(request) == SeaponyAction.Enter
            ? SeaponyParadePatternCompiler.EnterDefaultLengthBeats
            : SeaponyParadePatternCompiler.LeaveDefaultLengthBeats;
    }
}
