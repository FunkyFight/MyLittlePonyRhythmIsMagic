using System;
using GameCore;
using GameCore.Animation;
using GameCore.GameObjects;
using Microsoft.Xna.Framework.Graphics;
using Rhythm.Note;
using Rhythm.Note.Visual;

public class SeaponyBgVisualNote : VisualNote
{
    private const string ActionDataKey = "action";
    private const string SwimAction = "seapony_parade_swim";
    private const string RollAction = "seapony_parade_roll";
    private const string TapTapAction = "seapony_parade_tap_tap";

    private InfiniteScrollBackground _background;
    private int _backgroundScrollDestinationBeat;
    private readonly Func<bool> _canApplyState;

    public SeaponyBgVisualNote(Note logicalNote, double approachDuration, InfiniteScrollBackground background, int backgroundScrollDestinationBeat, double despawnDelay = 0, Func<bool> canApplyState = null) : base(logicalNote, approachDuration, despawnDelay)
    {
        this._background = background;
        this._backgroundScrollDestinationBeat = backgroundScrollDestinationBeat;
        _canApplyState = canApplyState;
    }

    public override void Update(double currentSongPosition)
    {
        base.Update(currentSongPosition);

        bool inTimeWindow = RhythmVisualUtils.IsInTimeWindow(currentSongPosition, Note.SongPosition, ApproachDuration, DespawnDelay, true);
        bool inAtOrAfterHit = RhythmVisualUtils.IsAtOrAfterHit(currentSongPosition, Note.SongPosition);

        if(!inTimeWindow || !inAtOrAfterHit) return;
        if(!RhythmVisualUtils.CanApplyState(_canApplyState)) return;

        switch(Note.AdditionnalData[ActionDataKey])
        {
            case SwimAction:
            case RollAction:
            case TapTapAction:
                handleScroll(currentSongPosition);
                break;
        }
    }

    private void handleScroll(double currentSongPosition)
    {
        float despawnDelayProgression = (float) RhythmVisualUtils.GetProgression(Note.SongPosition, Note.SongPosition + DespawnDelay, currentSongPosition);
        float interpolated = Interpolation.EaseOutQuart(despawnDelayProgression);
        float backgroundScrollBeat = Single.Lerp(_backgroundScrollDestinationBeat - 1, _backgroundScrollDestinationBeat, interpolated);

        _background.Progression = backgroundScrollBeat;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
    }
}
