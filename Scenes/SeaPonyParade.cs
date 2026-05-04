using System;
using GameCore.Animation;
using GameCore.GameObjects;
using GameCore.Graphics;
using GameCore.Scenes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MLP_RiM.Elements.DevUI;
using Rhythm.Note;
using Rhythm.Note.Visual;
using TexturePackerMonoGameDefinitions;

public class SeaPonyParade : Scene
{
    // Config
    private const int SCALE = 3;
    private const int SeaPonyCount = 4;

    // Objects
    private Viewport _viewport;
    private GameObject[] _seaPonies = new GameObject[SeaPonyCount];
    private AnimationStateMachine[] _seaPoniesAnimationStates = new AnimationStateMachine[SeaPonyCount];
    private VisualNoteManager<SeaponyVisualNote>[] _seaPoniesVisualNotes = new VisualNoteManager<SeaponyVisualNote>[SeaPonyCount];

    private InfiniteScrollBackground _infiniteScrollBg;
    private VisualNoteManager<SeaponyBgVisualNote> _infiniteScrollBgVisualNotes;

    private DevUiRenderer _devUIRenderer;

    public SeaPonyParade() : base("Seapony Parade")
    {
    }

    public override void OnLoad()
    {
        _devUIRenderer = new DevUiRenderer(GLOBALS.graphicsDevice);
        _viewport = GLOBALS.graphicsDevice.Viewport;
        createSeaPonies();
        createScrollBg();

        GLOBALS.beatmapPlayer.BeatmapStarted += createReactions;
    }

    private void createReactions()
    {
        GLOBALS.beatmapPlayer.ChartPlayer.NoteReacted += (result) =>
        {   
            Console.WriteLine(result);
            if(result != NoteReactionResult.MISS)
            {
                _seaPoniesAnimationStates[1].ForceState("swim");
            }  
        };
    }

    private void createScrollBg()
    {
        _infiniteScrollBg = new InfiniteScrollBackground.Builder()
            .AddLine(conf =>
            {
                conf.AddPrototypes(
                    new GameObject[]
                    {
                        new GameObject(GLOBALS.main_atlas.CreateSprite(MainAtlas.Coral1)),
                        new GameObject(GLOBALS.main_atlas.CreateSprite(MainAtlas.Coral2)),
                        new GameObject(GLOBALS.main_atlas.CreateSprite(MainAtlas.Coral3)),
                        new GameObject(GLOBALS.main_atlas.CreateSprite(MainAtlas.Coral4)),
                        new GameObject(GLOBALS.main_atlas.CreateSprite(MainAtlas.Coral5)),
                        new GameObject(GLOBALS.main_atlas.CreateSprite(MainAtlas.Coral6)),

                    }
                );
                conf.WithPlacementInterval(25, 600);
                conf.WithScrollMultiplier(0.1f);
                conf.WithOffset(new Vector2(0, 2.75f * (_viewport.Height / 5)));
                conf.WithScale(3);
            })
            .AddLine(conf =>
            {
                conf.AddPrototype(new GameObject(GLOBALS.main_atlas.CreateSprite(MainAtlas.Sand3)));
                conf.WithPlacementInterval(0, 0);
                conf.WithScrollMultiplier(0.3f);
                conf.WithOffset(new Vector2(0, 3.15f * (_viewport.Height / 5)));
                conf.WithScale(4);
            })
            .AddLine(conf =>
            {
                conf.AddPrototypes(
                    new GameObject[]
                    {
                        new GameObject(GLOBALS.main_atlas.CreateSprite(MainAtlas.Coral1)),
                        new GameObject(GLOBALS.main_atlas.CreateSprite(MainAtlas.Coral2)),
                        new GameObject(GLOBALS.main_atlas.CreateSprite(MainAtlas.Coral3)),
                        new GameObject(GLOBALS.main_atlas.CreateSprite(MainAtlas.Coral4)),
                        new GameObject(GLOBALS.main_atlas.CreateSprite(MainAtlas.Coral5)),
                        new GameObject(GLOBALS.main_atlas.CreateSprite(MainAtlas.Coral6)),

                    }
                );
                conf.WithPlacementInterval(25, 600);
                conf.WithScrollMultiplier(0.4f);
                conf.WithOffset(new Vector2(0, 3.45f * (_viewport.Height / 5)));
                conf.WithScale(3);
            })
            .AddLine(conf =>
            {
                conf.AddPrototype(new GameObject(GLOBALS.main_atlas.CreateSprite(MainAtlas.Sand2)));
                conf.WithPlacementInterval(0, 0);
                conf.WithScrollMultiplier(0.6f);
                conf.WithOffset(new Vector2(485, 3.65f * (_viewport.Height / 5)));
                conf.WithScale(4);
            })
            .AddLine(conf =>
            {
                conf.AddPrototypes(
                    new GameObject[]
                    {
                        new GameObject(GLOBALS.main_atlas.CreateSprite(MainAtlas.Coral1)),
                        new GameObject(GLOBALS.main_atlas.CreateSprite(MainAtlas.Coral2)),
                        new GameObject(GLOBALS.main_atlas.CreateSprite(MainAtlas.Coral3)),
                        new GameObject(GLOBALS.main_atlas.CreateSprite(MainAtlas.Coral4)),
                        new GameObject(GLOBALS.main_atlas.CreateSprite(MainAtlas.Coral5)),
                        new GameObject(GLOBALS.main_atlas.CreateSprite(MainAtlas.Coral6)),

                    }
                );
                conf.WithPlacementInterval(25, 600);
                conf.WithScrollMultiplier(0.7f);
                conf.WithOffset(new Vector2(200, 3.9f * (_viewport.Height / 5)));
                conf.WithScale(3);
            })
            .AddLine(conf =>
            {
                conf.AddPrototype(new GameObject(GLOBALS.main_atlas.CreateSprite(MainAtlas.Sand1)));
                conf.WithPlacementInterval(0, 0);
                conf.WithScrollMultiplier(1);
                conf.WithOffset(new Vector2(0, 4.15f * (_viewport.Height / 5)));
                conf.WithScale(4);
            })
            .WithPixelsPerProgress(new Vector2(_viewport.Width / 4, 0))
            .Build();
    }

    private void createSeaPonies()
    {
        for(int i = 0; i < SeaPonyCount; i++)
        {
            // Create object
            GameObject seaPony = new GameObject(GLOBALS.main_atlas.CreateAnimatedSprite("template-pony-idle"));
            seaPony.Position = new Vector2(4 * _viewport.Width / 5 - ((GLOBALS.graphicsDevice.Viewport.Width / 5) * i), _viewport.Height / 2);
            seaPony.Scale = Vector2.One * SCALE;
            seaPony.sprite.CenterOrigin();

            GameObjects.Add(seaPony);
            _seaPonies[i] = seaPony;

            // Create animation state
            AnimationStateMachine animationState = new AnimationStateMachine()
                .AddState(new AnimationState(
                    "idle",
                    1,
                    isLooping: false,
                    onEnter: () =>
                    {
                        seaPony.sprite = GLOBALS.main_atlas.CreateSprite(MainAtlas.Template_pony_idle1);
                        seaPony.sprite.CenterOrigin();
                    }
                ))
                .AddState(new AnimationState(
                    "swim_anticipation",
                    1,
                    isLooping: false,
                    onEnter: () =>
                    {
                        seaPony.sprite = GLOBALS.main_atlas.CreateSprite(MainAtlas.Template_pony_swim1);
                        seaPony.sprite.CenterOrigin();
                        seaPony.sprite.DrawOffset = new Vector2(80, 0);
                    })
                )
                .AddState(new AnimationState(
                    "swim",
                    1,
                    isLooping: false,
                    onEnter: () =>
                    {
                        seaPony.sprite = GLOBALS.main_atlas.CreateSprite(MainAtlas.Template_pony_swim2);
                        seaPony.sprite.CenterOrigin();
                    })
                )
                .SetGlobalUpdate(time =>
                {
                    if (seaPony.sprite is AnimatedSprite animatedSprite)
                        animatedSprite.Update(time);
                     
                    return true;
                })
                .Build();

                animationState.ForceState("idle");
                _seaPoniesAnimationStates[i] = animationState;
        }

        createVisualNotes();
        GLOBALS.beatmapPlayer.BeatmapStarted += createVisualNotes;
    }

    private void createVisualNotes()
    {
        for(int i = 0; i < SeaPonyCount; i++)
        {
            GameObject seaPony = _seaPonies[i];
            AnimationStateMachine animationState = _seaPoniesAnimationStates[i];
            int seaPonyId = i;

            VisualNoteManager<SeaponyVisualNote> visualNoteManager = new VisualNoteManager<SeaponyVisualNote>(GLOBALS.beatmapPlayer.ChartPlayer, note =>
            {
                if(note.AdditionnalData == null || !note.AdditionnalData.TryGetValue("action", out string action)) return null;
                if(action == "seapony_parade_swim") return new SeaponyVisualNote(note, GLOBALS.beatmapPlayer.Conductor.Crotchet, seaPony, animationState, seaPonyId, GLOBALS.beatmapPlayer.Conductor.Crotchet);
                if(action == "seapony_parade_star") return null;
                if(action == "seapony_parade_tap_tap") return null;
                return null;
            });

            _seaPoniesVisualNotes[i] = visualNoteManager;
        }


        _infiniteScrollBgVisualNotes = new VisualNoteManager<SeaponyBgVisualNote>(GLOBALS.beatmapPlayer.ChartPlayer, (note) =>
        {
            if(note.AdditionnalData["action"] == "seapony_parade_swim") return new SeaponyBgVisualNote(note, GLOBALS.beatmapPlayer.Conductor.Crotchet, _infiniteScrollBg, getBackgroundScrollDestinationBeat(note), GLOBALS.beatmapPlayer.Conductor.Crotchet);
            if(note.AdditionnalData["action"] == "seapony_parade_star") return null;
            if(note.AdditionnalData["action"] == "seapony_parade_tap_tap") return null;
            return null;
        });

        _infiniteScrollBgVisualNotes.LookBehindSeconds = GLOBALS.beatmapPlayer.Conductor.Crotchet * 2;
    }

    private int getBackgroundScrollDestinationBeat(Note note)
    {
        int swimNoteIndex = 0;

        foreach(Note candidate in GLOBALS.beatmapPlayer.ChartPlayer.Notes)
        {
            if(candidate.AdditionnalData != null
                && candidate.AdditionnalData.TryGetValue("action", out string action)
                && action == "seapony_parade_swim")
            {
                swimNoteIndex++;
            }

            if(ReferenceEquals(candidate, note)) return swimNoteIndex;
        }

        return swimNoteIndex;
    }

    public override void OnUnload()
    {
    }

    private float _elapsed = 0; 
    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        updateSeaPonies(gameTime);
        _infiniteScrollBg?.Update(gameTime);
    }

    private void updateSeaPonies(GameTime gameTime)
    {

        double songPos = GLOBALS.beatmapPlayer.Conductor.SongPosition;
        for(int i = 0; i < SeaPonyCount; i++)
        {
            _seaPoniesAnimationStates[i]?.Update(gameTime);
            _seaPoniesVisualNotes[i]?.Update(songPos);
        }

        _infiniteScrollBgVisualNotes.Update(songPos);
    }

    public override void Draw(SpriteBatch spriteBatch)
    {

        GLOBALS.graphicsDevice?.Clear(Color.DarkBlue);

        _devUIRenderer.Label(spriteBatch, "You", _seaPonies[1].Position + new Vector2(-35, -225), Color.White, 7);
        _infiniteScrollBg?.Draw(spriteBatch);

        base.Draw(spriteBatch);

    }

}
