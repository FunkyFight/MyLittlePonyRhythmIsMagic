using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using MLP_RiM.Elements.Editor;
using MLP_RiM.Elements.Editor.Commands;
using Rhythm.Note;
using Xunit;

public sealed class BeatmapPackageTests
{
    [Fact]
    public void ChartMetadataTracksAndClipsRoundTripThroughXml()
    {
        Chart chart = new()
        {
            SongName = "Song",
            BeatmapName = "Map",
            Beatmapper = "Mapper",
            ArtistName = "Artist",
            MusicName = "Music",
            SongPath = "Songs/song.ogg",
            Description = "Description",
            FlashingEffectsWarning = true,
            Tags = new List<string> { "tag-a", "tag-b" },
            LevelIconPath = "assets/icon.png",
            RatingHeader = "Rank",
            RatingTryAgainMessage = "Try again",
            RatingTryAgainImagePath = "assets/ratings/try_again.png",
            RatingOkMessage = "OK",
            RatingOkImagePath = "assets/ratings/ok.png",
            RatingSuperbMessage = "Superb",
            RatingSuperbImagePath = "assets/ratings/superb.png",
            BPM = 120,
            Offset = 0,
            ChartVersion = 2,
            EditorTracks = new List<ChartEditorTrack>
            {
                new() { Id = "track-1", Name = "Track 1", Index = 0, Color = "#ff00ff" }
            },
            EditorTracksSpecified = true,
            EditorClips = new List<ChartEditorClip>
            {
                new()
                {
                    Id = "clip-1",
                    TrackIndex = 0,
                    StartBeat = 4,
                    LengthBeats = 2,
                    RhythmGameId = EditorClipDefinitions.SeaponyParadeGameId,
                    ClipTypeId = EditorClipDefinitions.SeaponySwim,
                    ClipCategory = EditorClipCategory.Continuous.ToString(),
                    InputAction = "ReactMain",
                    Data = new Dictionary<string, string> { ["action"] = "seapony_parade_swim" }
                }
            },
            EditorClipsSpecified = true
        };

        Chart roundTripped = RoundTrip(chart);

        Assert.Equal("Description", roundTripped.Description);
        Assert.True(roundTripped.FlashingEffectsWarning);
        Assert.Equal(new[] { "tag-a", "tag-b" }, roundTripped.Tags);
        Assert.Equal("assets/icon.png", roundTripped.LevelIconPath);
        Assert.Single(roundTripped.EditorTracks);
        Assert.Single(roundTripped.EditorClips);
        Assert.Equal("clip-1", roundTripped.EditorClips[0].Id);
        Assert.Equal("seapony_parade_swim", roundTripped.EditorClips[0].Data["action"]);
    }

    [Fact]
    public void CreateNewPackageCreatesDefaultTracksAndAssetFolder()
    {
        using TempWorkspace workspace = new();
        string chartPath = Path.Combine(workspace.Root, "Beatmaps", "My Beatmap", "chart.xml");

        BeatmapEditorDocument document = BeatmapEditorDocument.CreateNew("Songs/song.ogg", chartPath, 120);

        Assert.Equal(chartPath, document.ChartPath);
        Assert.Equal(Path.Combine(workspace.Root, "Beatmaps", "My Beatmap"), document.PackagePath);
        Assert.Equal(Path.Combine(document.PackagePath, "assets"), document.AssetsPath);
        Assert.True(Directory.Exists(document.PackagePath));
        Assert.True(Directory.Exists(document.AssetsPath));
        Assert.Equal(10, document.EditorTracks.Count);
        Assert.True(document.Chart.EditorTracksSpecified);
        Assert.True(document.Chart.EditorClipsSpecified);
    }

    [Fact]
    public void SavingLegacyXmlMigratesToPackageWithoutDeletingLegacyFile()
    {
        using TempWorkspace workspace = new();
        string beatmaps = Path.Combine(workspace.Root, "Beatmaps");
        Directory.CreateDirectory(beatmaps);
        string legacyPath = Path.Combine(beatmaps, "legacy.xml");
        WriteChart(legacyPath, new Chart
        {
            SongName = "Legacy",
            BeatmapName = "Legacy",
            BPM = 120,
            ChartVersion = 2,
            Notes = new List<ChartNote>
            {
                new()
                {
                    BeatPosition = 1,
                    HoldBeats = 0,
                    SongPosition = 0.5,
                    HoldDuration = 0,
                    InputActionToPress = "ReactMain",
                    AdditionnalData = new Dictionary<string, string> { ["action"] = "seapony_parade_swim" }
                }
            }
        });

        BeatmapEditorDocument document = BeatmapEditorDocument.LoadOrCreate("", legacyPath, 120);
        Assert.True(document.IsLegacyChart);

        document.Save();

        string packageChartPath = Path.Combine(beatmaps, "legacy", "chart.xml");
        Assert.True(File.Exists(legacyPath));
        Assert.True(File.Exists(packageChartPath));
        Assert.Equal(packageChartPath, document.ChartPath);
        Assert.False(document.IsLegacyChart);
    }

    [Fact]
    public void AssetImportUsesPackageRelativeForwardSlashPathsAndAvoidsCollisions()
    {
        using TempWorkspace workspace = new();
        string chartPath = Path.Combine(workspace.Root, "Beatmaps", "Assets", "chart.xml");
        BeatmapEditorDocument document = BeatmapEditorDocument.CreateNew("", chartPath, 120);
        string source = Path.Combine(workspace.Root, "Icon File.PNG");
        File.WriteAllBytes(source, new byte[] { 1, 2, 3 });

        string first = document.ImportAsset(source, "ratings");
        string second = document.ImportAsset(source, "ratings");

        Assert.Equal("assets/ratings/Icon File.png", first);
        Assert.Equal("assets/ratings/Icon File_2.png", second);
        Assert.True(File.Exists(Path.Combine(document.PackagePath, "assets", "ratings", "Icon File.png")));
        Assert.True(File.Exists(Path.Combine(document.PackagePath, "assets", "ratings", "Icon File_2.png")));
    }

    [Fact]
    public void ClipCompilerGeneratesContinuousTapTapAndNoHitRuntimeNotes()
    {
        Chart chart = new()
        {
            BPM = 120,
            Offset = 0,
            ChartVersion = 2,
            EditorClips = new List<ChartEditorClip>
            {
                CreateClip("swim", EditorClipDefinitions.SeaponySwim, EditorClipCategory.Continuous, startBeat: 0, lengthBeats: 4),
                CreateClip("tap", EditorClipDefinitions.SeaponyTapTap, EditorClipCategory.SingleHit, startBeat: 8, lengthBeats: 0),
                CreateClip("nohit", EditorClipDefinitions.NoHit, EditorClipCategory.NoHit, startBeat: 12, lengthBeats: 4)
            },
            EditorClipsSpecified = true
        };

        List<ChartNote> notes = EditorClipCompiler.Compile(chart, new ChartTempoMap(chart));

        Assert.Equal(new[] { 0.0, 2.0, 4.0, 8.0, 8.5 }, notes.Select(note => note.BeatPosition.GetValueOrDefault()).ToArray());
    }

    [Fact]
    public void CommandStackUndoRedoRestoresClipGeneratedNotes()
    {
        using TempWorkspace workspace = new();
        BeatmapEditorDocument document = BeatmapEditorDocument.CreateNew("", Path.Combine(workspace.Root, "Beatmaps", "Commands", "chart.xml"), 120);
        EditorCommandStack stack = new();
        ChartEditorClip clip = CreateClip("clip-1", EditorClipDefinitions.SeaponySwim, EditorClipCategory.Continuous, startBeat: 0, lengthBeats: 4);

        stack.Execute(new CreateClipCommand(clip), document);
        Assert.Equal(new[] { 0.0, 2.0, 4.0 }, document.Chart.Notes.Select(note => note.BeatPosition.GetValueOrDefault()).ToArray());

        stack.Execute(new MoveClipCommand("clip-1", 1, 2), document);
        Assert.Equal(new[] { 1.0, 3.0, 5.0 }, document.Chart.Notes.Select(note => note.BeatPosition.GetValueOrDefault()).ToArray());

        Assert.True(stack.TryUndo(document));
        Assert.Equal(new[] { 0.0, 2.0, 4.0 }, document.Chart.Notes.Select(note => note.BeatPosition.GetValueOrDefault()).ToArray());

        Assert.True(stack.TryUndo(document));
        Assert.Empty(document.Chart.Notes);

        Assert.True(stack.TryRedo(document));
        Assert.Equal(new[] { 0.0, 2.0, 4.0 }, document.Chart.Notes.Select(note => note.BeatPosition.GetValueOrDefault()).ToArray());
    }

    [Fact]
    public void CommandStackUndoRedoWorksForLegacyNotePlacementAndDeletion()
    {
        using TempWorkspace workspace = new();
        BeatmapEditorDocument document = BeatmapEditorDocument.CreateNew("", Path.Combine(workspace.Root, "Beatmaps", "LegacyCommands", "chart.xml"), 120);
        EditorCommandStack stack = new();
        EditorNoteDefinition definition = EditorNoteDefinitions.Get(EditorNoteKind.SeaponyParade);
        ChartNote note = definition.CreateChartNote(0, document.Crotchet, variantIndex: 0);
        ChartTiming.SetNoteBeat(note, 0);

        stack.Execute(new PlaceNotesCommand(new[] { new EditorNotePlacement(definition, note) }), document);
        Assert.Single(document.Chart.Notes);

        ChartNote placedNote = document.Chart.Notes[0];
        stack.Execute(new DeleteNoteCommand(placedNote), document);
        Assert.Empty(document.Chart.Notes);

        Assert.True(stack.TryUndo(document));
        Assert.Single(document.Chart.Notes);

        Assert.True(stack.TryRedo(document));
        Assert.Empty(document.Chart.Notes);

        Assert.True(stack.TryUndo(document));
        Assert.Single(document.Chart.Notes);

        Assert.True(stack.TryUndo(document));
        Assert.Empty(document.Chart.Notes);
    }

    [Fact]
    public void TimingMetadataCommandsUndoAndRedoDerivedTiming()
    {
        using TempWorkspace workspace = new();
        BeatmapEditorDocument document = BeatmapEditorDocument.CreateNew("", Path.Combine(workspace.Root, "Beatmaps", "MetadataCommands", "chart.xml"), 120);
        EditorCommandStack stack = new();

        stack.Execute(new SetBpmCommand(180), document);
        Assert.Equal(180, document.Chart.BPM);
        Assert.Equal(180, document.GetBpmAtBeat(0));

        Assert.True(stack.TryUndo(document));
        Assert.Equal(120, document.Chart.BPM);
        Assert.Equal(120, document.GetBpmAtBeat(0));

        Assert.True(stack.TryRedo(document));
        Assert.Equal(180, document.Chart.BPM);
    }

    private static ChartEditorClip CreateClip(string id, string clipTypeId, EditorClipCategory category, double startBeat, double lengthBeats)
    {
        EditorClipDefinition definition = EditorClipDefinitions.Find(EditorClipDefinitions.SeaponyParadeGameId, clipTypeId);
        return new ChartEditorClip
        {
            Id = id,
            TrackIndex = 0,
            StartBeat = startBeat,
            LengthBeats = lengthBeats,
            RhythmGameId = EditorClipDefinitions.SeaponyParadeGameId,
            ClipTypeId = clipTypeId,
            ClipCategory = category.ToString(),
            InputAction = "ReactMain",
            Data = new Dictionary<string, string>(definition?.DefaultData ?? new Dictionary<string, string>())
        };
    }

    private static Chart RoundTrip(Chart chart)
    {
        XmlSerializer serializer = new(typeof(Chart));
        using StringWriter writer = new();
        serializer.Serialize(writer, chart);
        using StringReader reader = new(writer.ToString());
        return (Chart)serializer.Deserialize(reader);
    }

    private static void WriteChart(string path, Chart chart)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        XmlSerializer serializer = new(typeof(Chart));
        using FileStream stream = File.Create(path);
        serializer.Serialize(stream, chart);
    }

    private sealed class TempWorkspace : IDisposable
    {
        public TempWorkspace()
        {
            Root = Path.Combine(Path.GetTempPath(), "MLP_RiM_tests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }
    }
}
