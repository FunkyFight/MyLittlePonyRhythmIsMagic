using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace MLP_RiM.Elements.Editor;

internal readonly struct BeatmapEditorLayout
{
    public BeatmapEditorLayout(Viewport viewport)
    {
        TopBar = new Rectangle(0, 0, viewport.Width, 30);

        int upperY = TopBar.Bottom;
        int upperHeight = viewport.Height / 2;
        UpperContainer = new Rectangle(0, upperY, viewport.Width, upperHeight);

        int sceneWidth = viewport.Width / 2;
        ScenePreviewPanel = new Rectangle(UpperContainer.X, UpperContainer.Y, sceneWidth, UpperContainer.Height);
        RhythmGameListPanel = new Rectangle(ScenePreviewPanel.Right, UpperContainer.Y, 190, UpperContainer.Height);
        PalettePanel = new Rectangle(RhythmGameListPanel.Right, UpperContainer.Y, UpperContainer.Right - RhythmGameListPanel.Right, UpperContainer.Height);

        int timelineY = UpperContainer.Bottom;
        Timeline = new Rectangle(0, timelineY, viewport.Width, System.Math.Max(180, viewport.Height - timelineY));
        OptionsWindow = PalettePanel.Width >= 350
            ? new Rectangle(PalettePanel.Right - 350, PalettePanel.Y, 350, PalettePanel.Height)
            : new Rectangle(viewport.Width - 350, TopBar.Bottom, 350, upperHeight);
    }

    public Rectangle TopBar { get; }
    public Rectangle UpperContainer { get; }
    public Rectangle ScenePreviewPanel { get; }
    public Rectangle RhythmGameListPanel { get; }
    public Rectangle PalettePanel { get; }
    public Rectangle Timeline { get; }
    public Rectangle OptionsWindow { get; }
}
