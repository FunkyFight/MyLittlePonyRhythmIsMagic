using GameCore;
using GameCore.Scenes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MLP_RiM;
using TexturePackerMonoGameDefinitions;

public class MetronomeDebugScene : Scene
{
    private Game1 game;

    public MetronomeDebugScene(Game1 game) : base("MetronomeDebugScene")
    {
        this.game = game;
    }

    public override void OnLoad()
    {
    }

    public override void OnUnload()
    {
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        base.Draw(spriteBatch);

        GLOBALS.rhythmInputVisualElement.Draw(spriteBatch);
    }
}