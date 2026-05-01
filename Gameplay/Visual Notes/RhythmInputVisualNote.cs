using GameCore.GameObjects;
using GameCore.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rhythm.Note;
using Rhythm.Note.Visual;
using TexturePackerMonoGameDefinitions;

public class RhythmInputVisualNote : VisualNote
{

    public Vector2 reactionOrigin;
    public GameObject inputIndicatorGameObject;

    public RhythmInputVisualNote(Note logicalNote, Vector2 reactionOrigin) : base(logicalNote, 2, 2)
    {
        this.reactionOrigin = reactionOrigin;
        this.inputIndicatorGameObject = new GameObject(new GameCore.Graphics.Sprite(GLOBALS.controller_atlas.GetRegion(XboxControllerAtlasDefinitions.Digital_Buttons_ABXY_button_xbox_digital_a_2)));
        inputIndicatorGameObject.Scale = new Vector2(0.15f, 0.15f);
        
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        if(!Note.HasBeenMissed && Note.HasReacted) return;

        double lerpedX;

        if(UnclampedProgress <= 1)
        {
            lerpedX = ApproachThrough(GLOBALS.graphicsDevice.Viewport.Width * 1.1, reactionOrigin.X);
        } else
        {
            lerpedX = reactionOrigin.X + PostHitSameSpeed(GLOBALS.graphicsDevice.Viewport.Width * 1.1, reactionOrigin.X);
        }
        inputIndicatorGameObject.Position = new Vector2((float)lerpedX, reactionOrigin.Y);
        inputIndicatorGameObject.Draw(spriteBatch);
    }

    public static VisualNoteManager<VisualNote> SetupRhythmInputVisualNoteVisualNoteManager(ChartPlayer chartPlayer)
    {
        VisualNoteManager<VisualNote> visualNoteManager = new VisualNoteManager<VisualNote>(chartPlayer, note => 
            new RhythmInputVisualNote(
                note, 
                new Vector2(GLOBALS.graphicsDevice.Viewport.Width / 2, GLOBALS.graphicsDevice.Viewport.Height / 2 + GLOBALS.graphicsDevice.Viewport.Height / 4)
            )
        );

        visualNoteManager.LookBehindSeconds = 1.5;

        return visualNoteManager;
    }
}
