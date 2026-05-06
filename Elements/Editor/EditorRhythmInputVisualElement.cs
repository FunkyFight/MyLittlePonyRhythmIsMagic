using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MLP_RiM.Elements.DevUI;
using Rhythm.Note;
using Rhythm.Note.Visual;

namespace MLP_RiM.Elements.Editor;

public sealed class EditorRhythmInputVisualElement
{
    private readonly BeatmapPlayer _beatmapPlayer;
    private readonly Texture2D _pixel;
    private readonly DevUiRenderer _ui;

    public EditorRhythmInputVisualElement(BeatmapPlayer beatmapPlayer, Texture2D pixel, DevUiRenderer ui)
    {
        _beatmapPlayer = beatmapPlayer;
        _pixel = pixel;
        _ui = ui;
        RebuildVisuals();
    }

    public void RebuildVisuals()
    {
        if (_beatmapPlayer.ChartPlayer == null)
            return;

        VisualNoteManager<VisualNote> visualNoteManager = new(_beatmapPlayer.ChartPlayer, note =>
            new EditorRhythmInputVisualNote(
                note,
                _beatmapPlayer.GetCrotchetAt(note.SongPosition),
                _pixel,
                new Vector2(GLOBALS.graphicsDevice.Viewport.Width / 2f, GLOBALS.graphicsDevice.Viewport.Height / 2f + GLOBALS.graphicsDevice.Viewport.Height / 4f)));

        visualNoteManager.LookBehindSeconds = _beatmapPlayer.GetMaxCrotchet() * 2;
        visualNoteManager.LookAheadSeconds = _beatmapPlayer.GetMaxCrotchet() * 4;
        _beatmapPlayer.VisualNoteMng = visualNoteManager;
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        _beatmapPlayer.VisualNoteMng?.Draw(spriteBatch);

        float reactionX = GLOBALS.graphicsDevice.Viewport.Width / 2f;
        float reactionY = GLOBALS.graphicsDevice.Viewport.Height / 2f + GLOBALS.graphicsDevice.Viewport.Height / 4f;

        _ui.Fill(spriteBatch, new Rectangle((int)reactionX - 3, (int)reactionY - 60, 6, 120), Color.Red);
        _ui.Stroke(spriteBatch, new Rectangle((int)reactionX - 24, (int)reactionY - 24, 48, 48), Color.White, 2);
    }
}
