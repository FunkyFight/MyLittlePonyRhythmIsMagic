using GameCore.Graphics;
using Microsoft.Xna.Framework.Graphics;
using MLP_RiM.Elements;
using MLP_RiM.Elements.Editor;

/// <summary>
/// Defined on the go by Game1.cs so everyone can access it.
/// </summary>
public static class GLOBALS
{
    public static TextureAtlas controller_atlas;
    public static TextureAtlas main_atlas;


    public static GraphicsDevice graphicsDevice;
    public static BeatmapPlayer beatmapPlayer;
    public static RhythmInputVisualElement rhythmInputVisualElement;
    public static MouseViewportCoordinatesElement mouseViewportCoordinatesElement;
    public static BeatmapEditorElement beatmapEditorElement;

    public static float SfxVolume { get; set; } = 0.0f;
}
