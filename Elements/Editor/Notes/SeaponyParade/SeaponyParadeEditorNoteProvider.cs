using Microsoft.Xna.Framework;

namespace MLP_RiM.Elements.Editor;

public sealed class SeaponyParadeEditorNoteProvider : SimpleRhythmGame<SeaponyAction>
{
    public const string GameId = SeaponyNoteCodec.GameId;
    public const string SwimClipId = "seapony_parade.swim";
    public const string RollClipId = "seapony_parade.roll";
    public const string TapTapClipId = "seapony_parade.tap_tap";
    public const string LeaveClipId = "seapony_parade.leave";
    public const string EnterClipId = "seapony_parade.enter";
    public static readonly NoteTypeId TypeId = new(GameId, SeaponyNoteCodec.NoteId);

    protected override void Build(RhythmGameBuilder<SeaponyAction> game)
    {
        game.Id(GameId)
            .DisplayName("Seapony Parade")
            .SortOrder(10)
            .Scene(() => new SeaPonyParade());

        game.RuntimeNote(SeaponyNoteCodec.NoteId)
            .Input("ReactMain")
            .Timing(new SeaponyParadeEditorNoteTiming());

        game.Clip(SeaponyAction.Swim)
            .Id(SwimClipId)
            .Name("Swim")
            .Color(Color.CornflowerBlue)
            .Continuous(2)
            .Occupies(1, 1)
            .HitWindow(0, 2)
            .RepeatEvery(2);

        game.Clip(SeaponyAction.Roll)
            .Id(RollClipId)
            .Name("Roll")
            .Color(Color.DeepSkyBlue)
            .Continuous(3)
            .Occupies(2, 1)
            .HitWindow(0, 2)
            .SameVariantHitWindow(0, 1)
            .LeadIn(2)
            .RepeatEvery(1)
            .PadToMultipleOf(4);

        game.Clip(SeaponyAction.TapTap)
            .Id(TapTapClipId)
            .Name("Tap Tap")
            .Color(Color.LightBlue)
            .SingleHit()
            .Occupies(2, 2)
            .HitWindow(0, 2)
            .SameVariantHitWindow(0, 1)
            .LeadIn(2)
            .Pair(0.5)
            .RepeatPairsEvery(1.5);

        game.Clip(SeaponyAction.Leave)
            .Id(LeaveClipId)
            .Name("Leave")
            .Color(Color.MediumPurple)
            .Continuous(4)
            .Occupies(0, 0)
            .HitWindow(0, 0)
            .HoldForClipLength();

        game.Clip(SeaponyAction.Enter)
            .Id(EnterClipId)
            .Name("Enter")
            .Color(Color.MediumSeaGreen)
            .Continuous(4)
            .Occupies(0, 0)
            .HitWindow(0, 0)
            .HoldForClipLength();

        game.NoHit(1)
            .Color(Color.DimGray);
    }
}
