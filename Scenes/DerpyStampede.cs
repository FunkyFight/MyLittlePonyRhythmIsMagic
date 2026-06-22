using System;
using System.Collections.Generic;
using GameCore.Audio;
using GameCore.GameObjects;
using GameCore.Scenes;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MLP_RiM.Elements.Editor;
using Rhythm.Note;
using Rhythm.Note.Visual;
using TexturePackerMonoGameDefinitions;

public class DerpyStampede : Scene
{
    private const double DerpyReactDuration = 0.12;
    private const float SfxVolume = 1f;
    private static readonly Vector2 DerpyDefaultPositionRatio = new(0.4969f, 0.5667f);

    private VisualRuntime _visualRuntime;
    private VisualNoteManager<DerpyStampedeVisualNote> _visualNoteManager;
    private VisualNoteManager<DerpyStampedeEntryVisualNote> _entryVisualNoteManager;
    private GameObject _derpy;
    private Vector2 _derpyDefaultPosition;
    private double _derpyReactTimer;
    private uint _lastReactMainInputSerial;
    private double _lastSfxSongPosition = double.NaN;

    public DerpyStampede() : base("Derpy Stampede")
    {
    }

    public override void OnLoad()
    {
        Viewport vp = GLOBALS.graphicsDevice.Viewport;

        GameObject table = new GameObject(GLOBALS.main_atlas.CreateSprite(MainAtlas.Table));
        table.Scale = new Vector2(8, 4);
        table.Position = new Vector2(vp.Width * -0.0042f, vp.Height * 0.2389f);

        _derpyDefaultPosition = new Vector2(vp.Width * DerpyDefaultPositionRatio.X, vp.Height * DerpyDefaultPositionRatio.Y);

        _derpy = new GameObject(GLOBALS.main_atlas.CreateSprite(MainAtlas.Derpy));
        _derpy.sprite.CenterOrigin();
        _derpy.Position = _derpyDefaultPosition;
        _derpy.Scale *= 4;
        _lastReactMainInputSerial = GLOBALS.ReactMainInputSerial;
        
        GameObjects.Add(_derpy);
        GameObjects.Add(table);

        setupVisuals();
        GLOBALS.beatmapPlayer.BeatmapStarted += setupVisuals;
    }

    private void setupVisuals()
    {
        _lastReactMainInputSerial = GLOBALS.ReactMainInputSerial;
        _visualRuntime = new VisualRuntime();
        _visualRuntime.RegisterTrack("derpy", _derpy)
        .UseDriverResolver(ctx =>
        {
            return CurrentNote(ctx.SongPosition, ctx.Notes);
        });

        if(GLOBALS.beatmapPlayer.ChartPlayer == null)
        {
            _visualNoteManager = null;
            return;
        }

        _visualNoteManager = new VisualNoteManager<DerpyStampedeVisualNote>(GLOBALS.beatmapPlayer.ChartPlayer, note =>
        {
            if(!DerpyStampedeNoteCodec.TryReadAction(note?.AdditionnalData, out DerpyStampedeAction action))
                return null;

            switch(action)
            {
                case DerpyStampedeAction.Stamp:
                    return new DerpyStampedeVisualNote(note, _visualRuntime, action, new[] { note }, getCrotchetAt(note), getCrotchetAt(note));
                case DerpyStampedeAction.Triple_Stamp:
                    IReadOnlyList<Note> tripleNotes = getTripleStampNotes(note);
                    if(!ReferenceEquals(tripleNotes[0], note))
                        return null;

                    return new DerpyStampedeVisualNote(note, _visualRuntime, action, tripleNotes, getCrotchetAt(note), getTripleDespawnDelay(tripleNotes));
            }

            return null;
        });

        _visualNoteManager.LookAheadSeconds = getMaxCrotchet();
        _visualNoteManager.LookBehindSeconds = getMaxCrotchet() * 2;

        _entryVisualNoteManager = new VisualNoteManager<DerpyStampedeEntryVisualNote>(GLOBALS.beatmapPlayer.ChartPlayer, note =>
        {
            if(!DerpyStampedeNoteCodec.TryReadAction(note?.AdditionnalData, out DerpyStampedeAction action) || action != DerpyStampedeAction.Entry)
                return null;

            return new DerpyStampedeEntryVisualNote(note, _visualRuntime, _derpyDefaultPosition, getNoteDuration(note));
        });

        _entryVisualNoteManager.LookAheadSeconds = getMaxCrotchet() * 4;
        _entryVisualNoteManager.LookBehindSeconds = getMaxCrotchet();
    }

    public override void OnUnload()
    {
        GLOBALS.beatmapPlayer.BeatmapStarted -= setupVisuals;
        _visualRuntime?.ClearDrivers();
        _visualNoteManager = null;
        _entryVisualNoteManager = null;
        _lastSfxSongPosition = double.NaN;
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        GLOBALS.graphicsDevice.Clear(Color.LightBlue);

        base.Draw(spriteBatch);

        _visualNoteManager?.Draw(spriteBatch);
    }

    public override void Update(GameTime gameTime)
    {
        base.Update(gameTime);

        if(GLOBALS.beatmapPlayer.Conductor == null || GLOBALS.beatmapPlayer.ChartPlayer == null)
            return;

        updateDerpyReaction(gameTime);
        triggerDerpyReactionOnPlayerInput();

        double songPosition = GLOBALS.beatmapPlayer.Conductor.SongPosition;
        playCueSfx(songPosition);
        _visualRuntime?.ResolveDrivers(songPosition, GLOBALS.beatmapPlayer.ChartPlayer.Notes);
        _entryVisualNoteManager?.Update(songPosition);
        _visualNoteManager?.Update(songPosition);
    }

    private void triggerDerpyReactionOnPlayerInput()
    {
        if(_lastReactMainInputSerial == GLOBALS.ReactMainInputSerial)
            return;

        _lastReactMainInputSerial = GLOBALS.ReactMainInputSerial;
        SFX.Play(this, "SFX/DerpyStampede/Stamp.wav", SfxVolume);
        _derpy.sprite = GLOBALS.main_atlas.CreateSprite(MainAtlas.Derpy2);
        _derpy.sprite.CenterOrigin();
        _derpyReactTimer = DerpyReactDuration;
    }

    private void updateDerpyReaction(GameTime gameTime)
    {
        if(_derpyReactTimer <= 0)
            return;

        _derpyReactTimer -= gameTime.ElapsedGameTime.TotalSeconds;
        if(_derpyReactTimer > 0)
            return;

        _derpy.sprite = GLOBALS.main_atlas.CreateSprite(MainAtlas.Derpy);
        _derpy.sprite.CenterOrigin();
        _derpyReactTimer = 0;
    }

    private void playCueSfx(double songPosition)
    {
        double previousSongPosition = double.IsNaN(_lastSfxSongPosition)
            ? songPosition
            : _lastSfxSongPosition;

        if(previousSongPosition > songPosition)
            previousSongPosition = songPosition;

        foreach(Note note in GLOBALS.beatmapPlayer.ChartPlayer.Notes)
        {
            if(!DerpyStampedeNoteCodec.TryReadAction(note?.AdditionnalData, out DerpyStampedeAction action))
                continue;

            if(action == DerpyStampedeAction.Stamp)
            {
                double cueSongPosition = note.SongPosition - getCrotchetAt(note);
                if(previousSongPosition <= cueSongPosition && songPosition >= cueSongPosition)
                    SFX.Play(this, "SFX/DerpyStampede/Enveloppe.wav", SfxVolume);
            }
            else if(action == DerpyStampedeAction.Triple_Stamp && ReferenceEquals(getTripleStampNotes(note)[0], note))
            {
                double crotchet = getCrotchetAt(note);
                double startCueSongPosition = note.SongPosition - crotchet * 2.0;
                double secondCueSongPosition = note.SongPosition - crotchet;
                if(previousSongPosition <= startCueSongPosition && songPosition >= startCueSongPosition)
                    SFX.Play(this, "SFX/DerpyStampede/Cardboard.wav", SfxVolume);
                if(previousSongPosition <= secondCueSongPosition && songPosition >= secondCueSongPosition)
                    SFX.Play(this, "SFX/DerpyStampede/Cardboard.wav", SfxVolume);
            }
        }

        foreach(Note note in GLOBALS.beatmapPlayer.ChartPlayer.Notes)
        {
            if(!DerpyStampedeNoteCodec.TryReadAction(note?.AdditionnalData, out DerpyStampedeAction action))
                continue;

            if(previousSongPosition > note.SongPosition || songPosition < note.SongPosition)
                continue;

            if(action == DerpyStampedeAction.Triple_Stamp && TryGetTripleStampHitIndex(note, out int hitIndex))
                SFX.Play(this, $"SFX/DerpyStampede/CardboardHit{hitIndex + 1}.wav", SfxVolume);
        }

        _lastSfxSongPosition = songPosition;
    }

    private Note CurrentNote(double songPosition, IReadOnlyList<Note> notes)
    {
        if(notes == null)
            return null;

        Note currentNote = null;
        foreach(Note note in notes)
        {
            if(note.SongPosition > songPosition)
                break;

            if(IsDerpyRuntimeVisualAction(note))
                currentNote = note;
        }

        return currentNote;
    }

    private IReadOnlyList<Note> getTripleStampNotes(Note note)
    {
        IReadOnlyList<Note> notes = GLOBALS.beatmapPlayer.ChartPlayer?.Notes;
        if(notes == null)
            return new[] { note };

        List<Note> run = new();
        foreach(Note candidate in notes)
        {
            if(IsDerpyAction(candidate, DerpyStampedeAction.Triple_Stamp))
            {
                run.Add(candidate);
                continue;
            }

            IReadOnlyList<Note> chunk = getTripleStampChunk(run, note);
            if(chunk.Count > 0)
                return chunk;

            run.Clear();
        }

        IReadOnlyList<Note> finalChunk = getTripleStampChunk(run, note);
        return finalChunk.Count > 0 ? finalChunk : new[] { note };
    }

    private static IReadOnlyList<Note> getTripleStampChunk(List<Note> run, Note note)
    {
        if(run == null || run.Count == 0)
            return Array.Empty<Note>();

        for(int i = 0; i < run.Count; i++)
        {
            if(!ReferenceEquals(run[i], note))
                continue;

            int chunkStart = i / 3 * 3;
            int chunkLength = Math.Min(3, run.Count - chunkStart);
            return run.GetRange(chunkStart, chunkLength);
        }

        return Array.Empty<Note>();
    }

    private bool TryGetTripleStampHitIndex(Note note, out int hitIndex)
    {
        IReadOnlyList<Note> tripleNotes = getTripleStampNotes(note);
        for(int i = 0; i < tripleNotes.Count; i++)
        {
            if(ReferenceEquals(tripleNotes[i], note))
            {
                hitIndex = i;
                return hitIndex < 3;
            }
        }

        hitIndex = -1;
        return false;
    }

    private double getTripleDespawnDelay(IReadOnlyList<Note> tripleNotes)
    {
        if(tripleNotes == null || tripleNotes.Count == 0)
            return getMaxCrotchet();

        Note firstNote = tripleNotes[0];
        Note lastNote = tripleNotes[tripleNotes.Count - 1];
        return Math.Max(getCrotchetAt(firstNote), lastNote.SongPosition - firstNote.SongPosition + getCrotchetAt(lastNote));
    }

    private double getCrotchetAt(Note note)
    {
        return GLOBALS.beatmapPlayer.GetCrotchetAt(note?.SongPosition ?? 0);
    }

    private double getMaxCrotchet()
    {
        return GLOBALS.beatmapPlayer.GetMaxCrotchet();
    }

    private double getNoteDuration(Note note)
    {
        if(note == null)
            return getMaxCrotchet();

        return Math.Max(getCrotchetAt(note), note.EndSongPosition - note.SongPosition);
    }

    private static bool IsDerpyRuntimeVisualAction(Note note)
    {
        return IsDerpyAction(note, DerpyStampedeAction.Entry)
            || IsDerpyAction(note, DerpyStampedeAction.Stamp)
            || IsDerpyAction(note, DerpyStampedeAction.Triple_Stamp);
    }

    private static bool IsDerpyAction(Note note, DerpyStampedeAction expectedAction)
    {
        return DerpyStampedeNoteCodec.IsAction(note?.AdditionnalData, expectedAction);
    }
}
