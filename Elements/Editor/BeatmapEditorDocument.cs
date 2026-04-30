using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using Rhythm.Note;

namespace MLP_RiM.Elements.Editor;

public sealed class BeatmapEditorDocument
{
    private const double HitWindowEpsilonSeconds = 0.0005;

    public Chart Chart { get; private set; }
    public string SongPath { get; set; }
    public string ChartPath { get; private set; }
    public bool IsDirty { get; private set; }

    public double Crotchet => Chart.BPM > 0 ? 60.0 / Chart.BPM : 0.6;

    private BeatmapEditorDocument(Chart chart, string songPath, string chartPath)
    {
        Chart = chart;
        SongPath = songPath;
        ChartPath = chartPath;
        NormalizeChart();
    }

    public static BeatmapEditorDocument CreateNew(string songPath, string chartPath, double bpm = 100)
    {
        return new BeatmapEditorDocument(new Chart
        {
            SongName = Path.GetFileNameWithoutExtension(songPath),
            BeatmapName = Path.GetFileNameWithoutExtension(chartPath),
            Beatmapper = "Unknown",
            ArtistName = "Unknown",
            MusicName = Path.GetFileNameWithoutExtension(songPath),
            SongPath = songPath,
            BPM = bpm,
            Offset = 0.078,
            Notes = new List<ChartNote>()
        }, songPath, chartPath);
    }

    public static BeatmapEditorDocument LoadOrCreate(string songPath, string chartPath, double bpm = 100)
    {
        if (!File.Exists(chartPath) || new FileInfo(chartPath).Length == 0)
            return CreateNew(songPath, chartPath, bpm);

        Chart chart;
        try
        {
            using FileStream stream = File.OpenRead(chartPath);
            XmlSerializer serializer = new(typeof(Chart));
            chart = (Chart)serializer.Deserialize(stream);
        }
        catch (InvalidOperationException)
        {
            BackupInvalidChart(chartPath);
            return CreateNew(songPath, chartPath, bpm);
        }

        if (chart == null)
            return CreateNew(songPath, chartPath, bpm);

        if (!string.IsNullOrWhiteSpace(chart.SongPath))
            songPath = chart.SongPath;

        return new BeatmapEditorDocument(chart, songPath, chartPath);
    }

    private static void BackupInvalidChart(string chartPath)
    {
        if (!File.Exists(chartPath) || new FileInfo(chartPath).Length == 0)
            return;

        string backupPath = $"{chartPath}.invalid.{DateTime.Now:yyyyMMddHHmmss}";
        File.Copy(chartPath, backupPath, overwrite: false);
    }

    public void Save(string chartPath = null)
    {
        if (!string.IsNullOrWhiteSpace(chartPath))
            ChartPath = chartPath;

        string directory = Path.GetDirectoryName(ChartPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        SortNotes();

        using FileStream stream = File.Create(ChartPath);
        XmlSerializer serializer = new(typeof(Chart));
        serializer.Serialize(stream, Chart);
        IsDirty = false;
    }

    public bool TryPlaceNote(EditorNoteDefinition definition, double songPosition, out ChartNote placedNote, out string reason)
    {
        placedNote = null;
        songPosition = Math.Max(0, songPosition);

        if (FindBlockingNote(definition, songPosition) is ChartNote blocker)
        {
            reason = $"Blocked by {EditorNoteDefinitions.FromChartNote(blocker).DisplayName} at {blocker.SongPosition:0.000}s";
            return false;
        }

        placedNote = definition.CreateChartNote(songPosition, Crotchet);
        Chart.Notes.Add(placedNote);
        SortNotes();
        IsDirty = true;
        reason = null;
        return true;
    }

    public bool DeleteNearest(double songPosition, double maxDistanceSeconds, out ChartNote deletedNote)
    {
        deletedNote = FindNearest(songPosition, maxDistanceSeconds);
        if (deletedNote == null)
            return false;

        Chart.Notes.Remove(deletedNote);
        IsDirty = true;
        return true;
    }

    public ChartNote FindNearest(double songPosition, double maxDistanceSeconds)
    {
        return Chart.Notes
            .Select(note => new { Note = note, Distance = Math.Abs(note.SongPosition - songPosition) })
            .Where(item => item.Distance <= maxDistanceSeconds)
            .OrderBy(item => item.Distance)
            .Select(item => item.Note)
            .FirstOrDefault();
    }

    public bool IsOccupiedAt(double songPosition)
    {
        foreach (ChartNote note in Chart.Notes)
        {
            EditorNoteDefinition definition = EditorNoteDefinitions.FromChartNote(note);
            if (definition.Occupies(note.SongPosition, Crotchet, songPosition))
                return true;
        }

        return false;
    }

    public IEnumerable<ChartNote> GetNotesInWindow(double startSongPosition, double endSongPosition)
    {
        foreach (ChartNote note in Chart.Notes)
        {
            EditorNoteDefinition definition = EditorNoteDefinitions.FromChartNote(note);
            if (definition.GetEnd(note.SongPosition, Crotchet) >= startSongPosition && definition.GetStart(note.SongPosition, Crotchet) <= endSongPosition)
                yield return note;
        }
    }

    public void SetBpm(double bpm)
    {
        if (bpm <= 0)
            return;

        Chart.BPM = bpm;
        IsDirty = true;
    }

    public void SetOffset(double offset)
    {
        Chart.Offset = offset;
        IsDirty = true;
    }

    public void SetSongPath(string songPath)
    {
        if (string.IsNullOrWhiteSpace(songPath))
            return;

        SongPath = songPath;
        Chart.SongPath = songPath;

        if (string.IsNullOrWhiteSpace(Chart.MusicName) || Chart.MusicName == "Unknown")
            Chart.MusicName = Path.GetFileNameWithoutExtension(songPath);

        IsDirty = true;
    }

    public void SetMetadata(string beatmapName = null, string beatmapper = null, string artistName = null, string musicName = null)
    {
        if (beatmapName != null)
            Chart.BeatmapName = beatmapName;

        if (beatmapper != null)
            Chart.Beatmapper = beatmapper;

        if (artistName != null)
            Chart.ArtistName = artistName;

        if (musicName != null)
            Chart.MusicName = musicName;

        Chart.SongName = Chart.MusicName;
        IsDirty = true;
    }

    private ChartNote FindBlockingNote(EditorNoteDefinition placedDefinition, double songPosition)
    {
        double placedStart = placedDefinition.GetHitWindowStart(songPosition, Crotchet);
        double placedEnd = placedDefinition.GetHitWindowEnd(songPosition, Crotchet);

        foreach (ChartNote note in Chart.Notes)
        {
            EditorNoteDefinition existingDefinition = EditorNoteDefinitions.FromChartNote(note);
            double existingStart = existingDefinition.GetHitWindowStart(note.SongPosition, Crotchet);
            double existingEnd = existingDefinition.GetHitWindowEnd(note.SongPosition, Crotchet);

            if (placedStart < existingEnd - HitWindowEpsilonSeconds && placedEnd > existingStart + HitWindowEpsilonSeconds)
                return note;
        }

        return null;
    }

    private void NormalizeChart()
    {
        if (Chart == null)
            Chart = new Chart();

        if (string.IsNullOrWhiteSpace(Chart.SongName))
            Chart.SongName = Path.GetFileNameWithoutExtension(SongPath);

        if (string.IsNullOrWhiteSpace(Chart.BeatmapName))
            Chart.BeatmapName = Path.GetFileNameWithoutExtension(ChartPath);

        if (string.IsNullOrWhiteSpace(Chart.Beatmapper))
            Chart.Beatmapper = "Unknown";

        if (string.IsNullOrWhiteSpace(Chart.ArtistName))
            Chart.ArtistName = "Unknown";

        if (string.IsNullOrWhiteSpace(Chart.MusicName))
            Chart.MusicName = Chart.SongName;

        if (string.IsNullOrWhiteSpace(Chart.SongPath))
            Chart.SongPath = SongPath;

        SongPath = Chart.SongPath;

        if (Chart.BPM <= 0)
            Chart.BPM = 100;

        Chart.Notes ??= new List<ChartNote>();

        foreach (ChartNote note in Chart.Notes)
        {
            note.InputActionToPress ??= "ReactMain";
            note.AdditionnalData ??= new Dictionary<string, string>();
        }

        SortNotes();
    }

    private void SortNotes()
    {
        Chart.Notes = Chart.Notes.OrderBy(note => note.SongPosition).ToList();
    }
}
