using Microsoft.Xna.Framework;

namespace MLP_RiM.Elements.Editor;

public sealed class SeaponyParadeEditorNoteProvider : SimpleRhythmGame<SeaponyAction>
{
    public const string GameId = SeaponyNoteCodec.GameId;
    public const string SwimClipId = "seapony_parade.swim";
    public const string RollClipId = "seapony_parade.roll";
    public const string TapTapClipId = "seapony_parade.tap_tap";
    public static readonly NoteTypeId TypeId = new(GameId, SeaponyNoteCodec.NoteId);

    protected override void Build(RhythmGameBuilder<SeaponyAction> game)
    {
        game.Id(GameId)
            .DisplayName("Seapony Parade")
            .SortOrder(10)
            .Scene(() => new SeaPonyParade());

        game.RuntimeNote(SeaponyNoteCodec.NoteId)
            .Input("ReactMain")
            .Occupies(1, 1)
            .HitWindow(0, 2)
            .Timing(new SeaponyParadeEditorNoteTiming());

        game.Clip(SeaponyAction.Swim)
            .Id(SwimClipId)
            .Name("Swim")
            .Color(Color.CornflowerBlue)
            .Continuous(2)
            .RepeatEvery(2);

        game.Clip(SeaponyAction.Roll)
            .Id(RollClipId)
            .Name("Roll")
            .Color(Color.DeepSkyBlue)
            .Continuous(3)
            .LeadIn(2)
            .RepeatEvery(1)
            .PadToMultipleOf(4);

        game.Clip(SeaponyAction.TapTap)
            .Id(TapTapClipId)
            .Name("Tap Tap")
            .Color(Color.LightBlue)
            .SingleHit()
            .LeadIn(2)
            .Pair(0.5)
            .RepeatPairsEvery(1.5);

        game.NoHit(1)
            .Color(Color.DimGray);
    }
}
