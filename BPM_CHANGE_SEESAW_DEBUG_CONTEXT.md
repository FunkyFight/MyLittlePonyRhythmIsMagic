# Probleme BPM Change + SeeSaw

## Resume du probleme

Dans l'editeur de beatmap, ajouter un effet `bpm_change` rend tres difficile ou impossible de continuer une sequence See-Saw en placant des notes apres le changement de BPM.

Le comportement attendu est simple: a un temps donne, le BPM change, puis les notes suivantes doivent pouvoir continuer a s'enchainer musicalement sur la nouvelle grille BPM.

Le comportement actuel est ambigu parce que plusieurs systemes se croisent:

- La chart stocke les notes en secondes (`SongPosition`).
- Le gameplay See-Saw valide les enchainements en beats.
- `bpm_change` a un `section_offset` qui sert d'ancre locale de grille.
- L'editeur snappe les notes en secondes selon la grille locale.
- Le compilateur See-Saw exige des relations de beats tres exactes, avec `SameBeatEpsilon = 0.0005`.

Une premiere tentative de correction a ete faite dans `ChartTempoMap`: `GetBeatAt` retourne maintenant un beat global cumule apres les changements BPM, au lieu de repartir a zero apres chaque effet. Le probleme global d'enchainement n'est pas encore resolu de maniere fiable.

Le jeu est littéralement un Rhythm Heaven Like et doit fonctionner comme tel

## Exemple de chart actuelle

Fichier: `Beatmaps/See Saw.xml`

Parametres globaux:

```xml
<BPM>120</BPM>
<Offset>1.08</Offset>
```

Extrait autour du premier changement BPM:

```xml
<Note>
  <SongPosition>20.58</SongPosition>
  <HoldDuration>0</HoldDuration>
  <InputActionToPress>ReactMain</InputActionToPress>
  <AdditionnalData>
    <Entry Key="action" Value="see_saw_toward_outer" />
    <Entry Key="pattern" Value="ShortLong" />
  </AdditionnalData>
</Note>

<Note>
  <SongPosition>22.056847290640388</SongPosition>
  <HoldDuration>0</HoldDuration>
  <InputActionToPress>ReactMain</InputActionToPress>
  <AdditionnalData>
    <Entry Key="action" Value="see_saw_toward_outer" />
    <Entry Key="pattern" Value="LongLong" />
    <Entry Key="big_leap_rainbow_dash" Value="true" />
  </AdditionnalData>
</Note>

<Effects>
  <Effect>
    <SongPosition>21.586896551724134</SongPosition>
    <EffectType>bpm_change</EffectType>
    <Data>
      <Entry Key="bpm" Value="130" />
      <Entry Key="section_offset" Value="0" />
    </Data>
  </Effect>
  <Effect>
    <SongPosition>37.14650246305419</SongPosition>
    <EffectType>bpm_change</EffectType>
    <Data>
      <Entry Key="bpm" Value="120" />
      <Entry Key="section_offset" Value="0" />
    </Data>
  </Effect>
</Effects>
```

## Observation numerique sur la chart actuelle

Avec BPM initial 120 et offset 1.08, le premier effet BPM est a:

```text
effectBeat = (21.586896551724134 - 1.08) / 0.5 = 41.0137931034483
```

Donc le changement BPM n'est pas exactement sur un beat entier de la grille 120 BPM. Avec la tempo map actuelle a beats cumules, quelques notes apres l'effet tombent sur ces beats approximatifs:

| SongPosition | Beat global approx |
| --- | --- |
| 22.056847290640388 | 42.032020 |
| 23.89527093596059 | 46.015271 |
| 24.814482758620684 | 48.006897 |
| 26.666699507389158 | 52.020033 |
| 28.048965517241378 | 55.014943 |
| 29.43812807881773 | 58.024795 |
| 31.276551724137928 | 62.008046 |
| 33.1287684729064 | 66.021182 |
| 34.93960591133004 | 69.944663 |
| 37.14650246305418 | 74.726273 |

Point important: See-Saw valide les enchainements avec des durees de 1 ou 2 beats et une tolerance de `0.0005` beat. Des positions comme `46.015271` peuvent deja etre trop eloignees d'une grille exacte pour que les chaines passent.

## Code: format des effets BPM

Fichier: `../MonogameLibs/RhythmConductor/RhythmConductor/Note/Chart.cs`

```csharp
[XmlType("Effect")]
public class ChartEffect
{
    public const string BpmChangeEffectType = "bpm_change";
    public const string BpmKey = "bpm";
    public const string SectionOffsetKey = "section_offset";

    [XmlElement("SongPosition")]
    public double SongPosition { get; set; }

    [XmlElement("EffectType")]
    public string EffectType { get; set; }

    [XmlArray("Data")]
    [XmlArrayItem("Entry")]
    public List<ChartNoteAdditionnalDataEntry> SerializedData { get; set; } = new List<ChartNoteAdditionnalDataEntry>();

    [XmlIgnore]
    public Dictionary<string, string> Data
    {
        get
        {
            Dictionary<string, string> dict = new Dictionary<string, string>();
            if (SerializedData == null)
                return dict;

            foreach (ChartNoteAdditionnalDataEntry entry in SerializedData)
            {
                if (!string.IsNullOrWhiteSpace(entry.Key))
                    dict[entry.Key] = entry.Value ?? string.Empty;
            }

            return dict;
        }
        set
        {
            SerializedData = new List<ChartNoteAdditionnalDataEntry>();
            if (value == null)
                return;

            foreach (KeyValuePair<string, string> pair in value)
            {
                SerializedData.Add(new ChartNoteAdditionnalDataEntry
                {
                    Key = pair.Key,
                    Value = pair.Value
                });
            }
        }
    }

    [XmlIgnore]
    public bool IsBpmChange => string.Equals(EffectType, BpmChangeEffectType, StringComparison.OrdinalIgnoreCase);

    public bool TryGetBpm(out double bpm)
    {
        bpm = 0;
        return IsBpmChange
            && Data.TryGetValue(BpmKey, out string bpmValue)
            && double.TryParse(bpmValue, NumberStyles.Float, CultureInfo.InvariantCulture, out bpm)
            && bpm > 0;
    }

    public void SetBpm(double bpm)
    {
        if (bpm <= 0 || double.IsNaN(bpm) || double.IsInfinity(bpm))
            return;

        Dictionary<string, string> data = Data;
        data[BpmKey] = bpm.ToString("0.###", CultureInfo.InvariantCulture);
        Data = data;
    }

    public bool TryGetSectionOffset(out double sectionOffset)
    {
        sectionOffset = 0;
        return IsBpmChange
            && Data.TryGetValue(SectionOffsetKey, out string sectionOffsetValue)
            && double.TryParse(sectionOffsetValue, NumberStyles.Float, CultureInfo.InvariantCulture, out sectionOffset)
            && !double.IsNaN(sectionOffset)
            && !double.IsInfinity(sectionOffset);
    }

    public double GetSectionOffsetOrDefault(double fallback)
    {
        return TryGetSectionOffset(out double sectionOffset) ? sectionOffset : fallback;
    }

    public double GetSectionAnchorSongPosition()
    {
        return SongPosition + GetSectionOffsetOrDefault(0);
    }

    public void SetSectionOffset(double sectionOffset)
    {
        if (double.IsNaN(sectionOffset) || double.IsInfinity(sectionOffset))
            return;

        Dictionary<string, string> data = Data;
        data[SectionOffsetKey] = sectionOffset.ToString("0.######", CultureInfo.InvariantCulture);
        Data = data;
    }
}
```

## Code: creation et edition des BPM changes

Fichier: `Elements/Editor/EditorEffectDefinition.cs`

```csharp
private static ChartEffect CreateBpmChangeEffect(double songPosition, BeatmapEditorDocument document)
{
    ChartEffect effect = new()
    {
        SongPosition = songPosition,
        EffectType = ChartEffect.BpmChangeEffectType
    };

    effect.SetBpm(document?.GetBpmAt(songPosition) ?? 100);
    effect.SetSectionOffset(0);
    return effect;
}
```

```csharp
public IReadOnlyList<DevUiWindowRow> BuildRows(EditorEffectOptionsContext context)
{
    if (context == null)
        return Array.Empty<DevUiWindowRow>();

    ChartEffect effect = context.GetCurrentEffect();
    if (effect == null)
        return new[] { DevUiWindowRow.Title("Effect unavailable") };

    double bpm = GetBpm(effect, context.Document?.GetBpmAt(effect.SongPosition) ?? 100);
    double sectionOffset = effect.GetSectionOffsetOrDefault(0);

    return new[]
    {
        DevUiWindowRow.Title($"Time: {effect.SongPosition:0.000}s"),
        DevUiWindowRow.FloatInput("effect_bpm", "BPM", bpm, value => SetBpm(context.GetCurrentEffect(), value)),
        DevUiWindowRow.FloatInput("effect_section_offset", "FIRST BEAT +", sectionOffset, value => SetSectionOffset(context.GetCurrentEffect(), value))
    };
}
```

## Code: tempo map actuelle

Fichier: `Gameplay/ChartTempoMap.cs`

```csharp
public sealed class ChartTempoMap
{
    private const double EpsilonSeconds = 0.0005;

    private readonly List<ChartEffect> _bpmEffects;
    private readonly double _baseBpm;
    private readonly double _baseOffset;

    public ChartTempoMap(Chart chart)
    {
        _baseBpm = chart?.BPM > 0 ? chart.BPM : 100;
        _baseOffset = chart?.Offset ?? 0.0;
        _bpmEffects = chart?.Effects == null
            ? new List<ChartEffect>()
            : chart.Effects
                .Where(effect => effect?.IsBpmChange == true && effect.TryGetBpm(out _))
                .OrderBy(effect => effect.SongPosition)
                .ToList();
    }

    public double GetBpmAt(double songPosition)
    {
        double bpm = _baseBpm;
        foreach (ChartEffect effect in _bpmEffects)
        {
            if (effect.SongPosition > songPosition + EpsilonSeconds)
                break;

            if (effect.TryGetBpm(out double effectBpm))
                bpm = effectBpm;
        }

        return bpm;
    }

    public double GetCrotchetAt(double songPosition)
    {
        return GetCrotchetForBpm(GetBpmAt(songPosition));
    }

    public double GetTempoAnchorAt(double songPosition)
    {
        double anchor = _baseOffset;
        foreach (ChartEffect effect in _bpmEffects)
        {
            if (effect.SongPosition > songPosition + EpsilonSeconds)
                break;

            anchor = effect.GetSectionAnchorSongPosition();
        }

        return anchor;
    }

    public double GetBeatAt(double songPosition)
    {
        GetTempoStateAt(songPosition, out double segmentStartSongPosition, out double segmentStartBeat, out double bpm);
        return GetBeatInSegment(songPosition, segmentStartSongPosition, segmentStartBeat, bpm);
    }

    public double GetSongPositionAtBeat(double beat)
    {
        double segmentStartSongPosition = _baseOffset;
        double segmentStartBeat = 0.0;
        double bpm = _baseBpm;

        foreach (ChartEffect effect in _bpmEffects)
        {
            double effectBeat = GetBeatInSegment(effect.SongPosition, segmentStartSongPosition, segmentStartBeat, bpm);
            if (beat < effectBeat - EpsilonSeconds)
                break;

            segmentStartSongPosition = effect.SongPosition;
            segmentStartBeat = effectBeat;
            if (effect.TryGetBpm(out double effectBpm))
                bpm = effectBpm;
        }

        return Math.Max(0.0, segmentStartSongPosition + (beat - segmentStartBeat) * GetCrotchetForBpm(bpm));
    }

    public IEnumerable<EditorTempoSegment> GetTempoSegments(double startSongPosition, double endSongPosition)
    {
        if (endSongPosition < startSongPosition)
            (startSongPosition, endSongPosition) = (endSongPosition, startSongPosition);

        double anchor = _baseOffset;
        double segmentStart = _baseOffset;
        double bpm = _baseBpm;
        bool anchorIsRelativeToSegmentStart = false;

        foreach (ChartEffect effect in _bpmEffects)
        {
            if (effect.SongPosition > endSongPosition)
                break;

            if (effect.SongPosition > segmentStart && effect.SongPosition >= startSongPosition)
                yield return new EditorTempoSegment(anchor, segmentStart, Math.Max(segmentStart, startSongPosition), effect.SongPosition, bpm, anchorIsRelativeToSegmentStart);

            anchor = effect.GetSectionOffsetOrDefault(0);
            segmentStart = Math.Max(segmentStart, effect.SongPosition);
            anchorIsRelativeToSegmentStart = true;
            if (effect.TryGetBpm(out double effectBpm))
                bpm = effectBpm;
        }

        if (endSongPosition > segmentStart)
            yield return new EditorTempoSegment(anchor, segmentStart, Math.Max(segmentStart, startSongPosition), endSongPosition, bpm, anchorIsRelativeToSegmentStart);
    }

    private void GetTempoStateAt(double songPosition, out double segmentStartSongPosition, out double segmentStartBeat, out double bpm)
    {
        segmentStartSongPosition = _baseOffset;
        segmentStartBeat = 0.0;
        bpm = _baseBpm;

        foreach (ChartEffect effect in _bpmEffects)
        {
            if (effect.SongPosition > songPosition + EpsilonSeconds)
                break;

            double effectBeat = GetBeatInSegment(effect.SongPosition, segmentStartSongPosition, segmentStartBeat, bpm);
            segmentStartSongPosition = effect.SongPosition;
            segmentStartBeat = effectBeat;
            if (effect.TryGetBpm(out double effectBpm))
                bpm = effectBpm;
        }
    }

    private static double GetBeatInSegment(double songPosition, double segmentStartSongPosition, double segmentStartBeat, double bpm)
    {
        return segmentStartBeat + (songPosition - segmentStartSongPosition) / GetCrotchetForBpm(bpm);
    }

    private static double GetCrotchetForBpm(double bpm)
    {
        return bpm > 0 ? 60.0 / bpm : 0.6;
    }
}
```

Note historique: avant la tentative de correction, `GetBeatAt` utilisait une ancre locale apres `bpm_change`, donc avec `section_offset = 0`, le beat repartait autour de 0 apres l'effet. Cela cassait directement See-Saw, qui attend des beats monotones.

## Code: snapping dans l'editeur

Fichier: `Elements/Editor/BeatmapEditorElement.cs`

```csharp
private double GetSteppedSeekPosition(int direction)
{
    double step = IsShiftDown()
        ? GetShiftSeekStep()
        : _document.GetCrotchetAt(CurrentSongPosition()) / _snapDivisions;

    double position = CurrentSongPosition() + direction * step;
    return IsShiftDown() ? ClampSongPosition(position) : Snap(position);
}

private double Snap(double songPosition)
{
    double step = _document.GetCrotchetAt(songPosition) / _snapDivisions;
    double anchor = _document.GetTempoAnchorAt(songPosition);
    return Math.Max(0, anchor + Math.Round((songPosition - anchor) / step) * step);
}
```

## Code: placement et validation des notes dans l'editeur

Fichier: `Elements/Editor/BeatmapEditorDocument.cs`

```csharp
public bool TryPlaceNotes(IReadOnlyList<EditorNotePlacement> placements, out IReadOnlyList<ChartNote> placedNotes, out string reason)
{
    placedNotes = Array.Empty<ChartNote>();
    if (placements == null || placements.Count == 0)
    {
        reason = "No notes to place";
        return false;
    }

    List<EditorNotePlacement> normalizedPlacements = new();
    foreach (EditorNotePlacement placement in placements)
    {
        if (placement?.Definition == null || placement.Note == null)
        {
            reason = "Invalid note placement";
            return false;
        }

        ChartNote note = placement.Note;
        note.SongPosition = Math.Max(0, note.SongPosition);
        note.InputActionToPress ??= placement.Definition.InputAction;
        note.AdditionnalData ??= new Dictionary<string, string>();
        SynchronizeNoteDuration(note, placement.Definition);
        normalizedPlacements.Add(new EditorNotePlacement(placement.Definition, note));
    }

    IReadOnlyList<ChartNote> contextualNotes = CreateContextualNotes(normalizedPlacements);
    for (int i = 0; i < normalizedPlacements.Count; i++)
    {
        EditorNotePlacement placement = normalizedPlacements[i];

        if (FindBlockingNote(placement, contextualNotes) is ChartNote blocker)
        {
            EditorNoteDefinition blockerDefinition = EditorNoteDefinitions.FromChartNote(blocker);
            reason = $"Blocked by {blockerDefinition?.DisplayName ?? "note"} at {blocker.SongPosition:0.000}s";
            return false;
        }

        for (int j = 0; j < i; j++)
        {
            if (PlacementsBlockEachOther(placement, normalizedPlacements[j], contextualNotes))
            {
                reason = $"Generated notes overlap at {placement.Note.SongPosition:0.000}s";
                return false;
            }
        }
    }

    if (normalizedPlacements.Any(placement => placement.Definition.Kind == EditorNoteKind.SeeSaw))
    {
        SeeSawTimeline previewTimeline = SeeSawChartCompiler.CompileContextualChartNotes(contextualNotes, GetBeatAt, GetSongPositionAtBeat);
        if (previewTimeline.Errors.Count > 0)
        {
            reason = previewTimeline.Errors[0];
            return false;
        }
    }

    List<ChartNote> notes = normalizedPlacements.Select(placement => placement.Note).ToList();
    Chart.Notes.AddRange(notes);
    SynchronizeAllNoteDurations();
    SortNotes();
    IsDirty = true;
    placedNotes = notes;
    reason = null;
    return true;
}
```

Wrappers tempo dans le document:

```csharp
public double GetBpmAt(double songPosition) => CreateTempoMap().GetBpmAt(songPosition);
public double GetCrotchetAt(double songPosition) => CreateTempoMap().GetCrotchetAt(songPosition);
public double GetTempoAnchorAt(double songPosition) => CreateTempoMap().GetTempoAnchorAt(songPosition);
public double GetBeatAt(double songPosition) => CreateTempoMap().GetBeatAt(songPosition);
public double GetSongPositionAtBeat(double beat) => CreateTempoMap().GetSongPositionAtBeat(beat);
```

Conversion des fenetres See-Saw pour le blocking:

```csharp
private double GetBlockingStart(EditorNoteDefinition definition, ChartNote note, bool sameVariantWindow, IReadOnlyList<ChartNote> contextualNotes)
{
    if (definition == null)
        return note.SongPosition;

    if (definition.Kind == EditorNoteKind.SeeSaw)
        return GetSongPositionAtBeat(GetSeeSawTiming(note, contextualNotes).PrepStartBeat);

    int variantIndex = EditorNoteDefinitions.FindVariantIndex(definition, note);
    Func<int, ChartNote> getRelativeNote = CreateRelativeNoteGetter(note, contextualNotes);
    double crotchet = GetCrotchetAt(note.SongPosition);
    return sameVariantWindow
        ? definition.GetSameVariantHitWindowStart(note.SongPosition, crotchet, variantIndex, getRelativeNote, beforeUsesOuterTiming: false)
        : definition.GetHitWindowStart(note.SongPosition, crotchet, variantIndex, getRelativeNote, beforeUsesOuterTiming: false);
}

private double GetBlockingEnd(EditorNoteDefinition definition, ChartNote note, bool sameVariantWindow, IReadOnlyList<ChartNote> contextualNotes)
{
    if (definition == null)
        return note.SongPosition;

    if (definition.Kind == EditorNoteKind.SeeSaw)
        return GetSongPositionAtBeat(GetSeeSawTiming(note, contextualNotes).EndBeat);

    int variantIndex = EditorNoteDefinitions.FindVariantIndex(definition, note);
    Func<int, ChartNote> getRelativeNote = CreateRelativeNoteGetter(note, contextualNotes);
    double crotchet = GetCrotchetAt(note.SongPosition);
    return sameVariantWindow
        ? definition.GetSameVariantHitWindowEnd(note.SongPosition, crotchet, variantIndex, getRelativeNote)
        : definition.GetHitWindowEnd(note.SongPosition, crotchet, variantIndex, getRelativeNote);
}
```

Timing See-Saw depuis le document:

```csharp
private SeeSawCompiledEventTiming GetSeeSawTiming(ChartNote note, IReadOnlyList<ChartNote> contextualNotes)
{
    SeeSawCompiledEventTiming timing = SeeSawChartCompiler.GetTimingForChartNote(contextualNotes ?? Chart.Notes, note, GetBeatAt);
    if (timing.IsSeeSaw)
        return timing;

    double beat = GetBeatAt(note.SongPosition);
    return new SeeSawCompiledEventTiming(
        idealPrepStartBeat: beat,
        prepStartBeat: beat,
        cueBeat: beat,
        playerHitBeat: beat,
        endBeat: beat,
        isSeeSaw: false,
        isExit: false,
        isValid: true,
        invalidReason: null,
        pattern: SeeSawPatternKind.LongLong,
        launchSide: SeeSawSide.Outer,
        targetSide: SeeSawSide.Outer,
        applejackTargetSide: SeeSawSide.Outer);
}
```

## Code: runtime BPM et tempo map

Fichier: `Gameplay/BeatmapPlayer.cs`

```csharp
public void StartBeatmap(string song_path, Chart chart, ReactionRules rules, IReactionEvaluator reactionEvaluator)
{
    DisposeConductor();

    _startupDelay = 0;
    _startupTimer = 0;
    _startupComplete = true;

    Conductor = new Conductor(song_path, chart.BPM, chart.Offset);
    CurrentChart = chart;
    _tempoMap = new ChartTempoMap(CurrentChart);
    HasAChartLoaded = true;
    ChartPlayer = new ChartPlayer(chart, rules, reactionEvaluator);
    ApplyChartEffectsAt(0);
    Conductor.Play();
    BeatmapStarted?.Invoke();
}

public void ApplyChartEffectsAt(double songPosition)
{
    if (Conductor == null || CurrentChart == null)
        return;

    double bpm = GetBpmAt(songPosition);
    if (Math.Abs(Conductor.BPM - bpm) > 0.0005)
        Conductor.SetBpm(bpm);
}

public double GetBpmAt(double songPosition)
{
    if (CurrentChart == null)
        return Conductor?.BPM ?? 100;

    return GetTempoMap().GetBpmAt(songPosition);
}

public double GetCrotchetAt(double songPosition)
{
    return CurrentChart == null
        ? Conductor?.Crotchet ?? 0.6
        : GetTempoMap().GetCrotchetAt(songPosition);
}

public double GetBeatAt(double songPosition)
{
    return CurrentChart == null
        ? songPosition / (Conductor?.Crotchet ?? 0.6)
        : GetTempoMap().GetBeatAt(songPosition);
}

public double GetSongPositionAtBeat(double beat)
{
    return CurrentChart == null
        ? beat * (Conductor?.Crotchet ?? 0.6)
        : GetTempoMap().GetSongPositionAtBeat(beat);
}
```

Fichier: `../MonogameLibs/RhythmConductor/RhythmConductor/Conductor/Conductor.cs`

```csharp
public void SetBpm(double bpm)
{
    ThrowIfDisposed();

    if (bpm <= 0)
        throw new ArgumentOutOfRangeException(nameof(bpm), "BPM must be greater than zero.");

    BPM = bpm;
    UpdateSongPosition(SongPosition);
    ResetBeatCursor();
}

private void UpdateSongPosition(double songPosition)
{
    SongPosition = songPosition;
    SongPositionInBeats = (int)Math.Floor((SongPosition - FirstBeatDelay) / Crotchet);
    SongPositionInBeatsExact = (SongPosition - FirstBeatDelay) / Crotchet;
}
```

Note: le `Conductor` lui-meme ne connait pas `ChartTempoMap`. Il recalcule ses beats avec le BPM courant et `FirstBeatDelay`. Le gameplay See-Saw utilise plutot `BeatmapPlayer.GetBeatAt`, mais les events `BeatChanged` du conductor peuvent quand meme ne pas correspondre a la tempo map globale.

## Code: branchement See-Saw runtime

Fichier: `Scenes/SeeSaw.cs`

```csharp
private void SetupTimelineAndDirector()
{
    ChartPlayer chartPlayer = GLOBALS.beatmapPlayer.ChartPlayer;
    if (chartPlayer == null || GLOBALS.beatmapPlayer.Conductor == null)
    {
        _director = null;
        return;
    }

    SeeSawLayout layout = new(applejackOuterPos, applejackInnerPos, applejackExitPos, rainbowOuterPos, rainbowInnerPos);
    double baseCrotchet = GLOBALS.beatmapPlayer.GetCrotchetAt(GLOBALS.beatmapPlayer.Conductor.SongPosition);
    SeeSawTimeline timeline = SeeSawChartCompiler.Compile(chartPlayer.Notes, GLOBALS.beatmapPlayer.GetBeatAt, GLOBALS.beatmapPlayer.GetSongPositionAtBeat, GLOBALS.beatmapPlayer.GetCrotchetAt);
    SeeSawPathCatalog pathCatalog = new(layout);

    _director = new SeeSawDirector(
        timeline,
        new SeeSawActorController(SeeSawActor.RainbowDash, Rainbow, RainbowState),
        new SeeSawActorController(SeeSawActor.Applejack, Applejack, ApplejackState),
        SeeSaw2,
        rainbowTrail,
        pathCatalog,
        new SeeSawCameraController(sceneCamera),
        new SeeSawSoundScheduler(this),
        GLOBALS.beatmapPlayer.GetBeatAt,
        GLOBALS.beatmapPlayer.GetCrotchetAt,
        baseCrotchet);
    _director.Reset();
}
```

## Code: constantes et validation See-Saw

Fichier: `Gameplay/SeeSaw/SeeSawRuntime.cs`

```csharp
public enum SeeSawPatternKind
{
    LongLong,
    LongShort,
    ShortLong,
    ShortShort
}

public static class SeeSawTiming
{
    public const double ShortJumpBeats = 1.0;
    public const double LongJumpBeats = 2.0;
    public const double ExitJumpBeats = 2.0;

    public static double GetJumpLengthBeats(SeeSawJumpLength length)
    {
        return length == SeeSawJumpLength.Long ? LongJumpBeats : ShortJumpBeats;
    }

    public static double GetJumpBeatsFromSide(SeeSawSide side)
    {
        if (side == SeeSawSide.Outer || side == SeeSawSide.Exit)
            return LongJumpBeats;

        return ShortJumpBeats;
    }
}
```

Validation d'un timing programme:

```csharp
public static SeeSawCompiledEventTiming CreateScheduledPatternTiming(SeeSawPatternKind pattern, SeeSawAction style, SeeSawLogicalState state, double playerHitBeat, bool exitAfterHit = false)
{
    _ = style;

    SeeSawSide launchSide = state.RainbowSide;
    SeeSawSide applejackCueSide = SeeSawPatternInfo.GetApplejackCueSide(pattern);
    SeeSawSide targetSide = SeeSawPatternInfo.GetTargetSide(pattern);
    SeeSawJumpLength applejackPrepLength = GetJumpLengthFromSide(state.ApplejackSide);
    SeeSawJumpLength rainbowJumpLength = GetJumpLengthFromSide(launchSide);

    double cueBeat = playerHitBeat - GetJumpLengthBeats(rainbowJumpLength);
    double endBeat = exitAfterHit ? playerHitBeat + ExitJumpBeats : playerHitBeat;
    double idealPrepStartBeat = cueBeat - GetJumpLengthBeats(applejackPrepLength);
    bool hasFullPrep = state.ApplejackAvailableBeat <= idealPrepStartBeat + SeeSawChartCompiler.SameBeatEpsilon;
    bool isChainedToCue = Math.Abs(state.ApplejackAvailableBeat - cueBeat) <= SeeSawChartCompiler.SameBeatEpsilon
        && state.ApplejackSide == applejackCueSide;
    bool hasValidPrep = hasFullPrep || isChainedToCue;
    bool isValid = hasValidPrep;
    double prepStartBeat = isChainedToCue ? cueBeat : idealPrepStartBeat;

    string invalidReason = hasValidPrep ? null : GetInvalidReason(state, applejackCueSide, cueBeat, idealPrepStartBeat);

    return new SeeSawCompiledEventTiming(
        idealPrepStartBeat,
        prepStartBeat,
        cueBeat,
        playerHitBeat,
        endBeat,
        isSeeSaw: true,
        isExit: false,
        isValid: isValid,
        invalidReason: invalidReason,
        pattern: pattern,
        launchSide: launchSide,
        targetSide: targetSide,
        applejackTargetSide: exitAfterHit ? SeeSawSide.Exit : applejackCueSide,
        applejackCueSide: applejackCueSide,
        applejackCueSource: new SeeSawImpactSource(state.ApplejackSide, applejackCueSide),
        rainbowSource: new SeeSawImpactSource(launchSide, targetSide),
        applejackEndSource: new SeeSawImpactSource(applejackCueSide, exitAfterHit ? SeeSawSide.Exit : applejackCueSide));
}

private static string GetInvalidReason(SeeSawLogicalState state, SeeSawSide launchSide, double cueBeat, double idealPrepStartBeat)
{
    return $"Applejack needs prep from {idealPrepStartBeat:0.###}b to {cueBeat:0.###}b, but is available at {state.ApplejackAvailableBeat:0.###}b on {state.ApplejackSide} and is not chained to {launchSide} at cue";
}
```

## Code: compilation See-Saw

Fichier: `Gameplay/SeeSaw/SeeSawRuntime.cs`

```csharp
public static class SeeSawChartCompiler
{
    internal const double SameBeatEpsilon = 0.0005;

    public static SeeSawTimeline Compile(IReadOnlyList<Note> notes, Func<double, double> getBeatAt, Func<double, double> getSongPositionAtBeat, Func<double, double> getCrotchetAt)
    {
        SeeSawTimeline timeline = new();
        if (notes == null || getBeatAt == null || getSongPositionAtBeat == null || getCrotchetAt == null)
            return timeline;

        List<Note> sortedNotes = new(notes);
        sortedNotes.Sort((a, b) => a.SongPosition.CompareTo(b.SongPosition));

        List<SeeSawCommandEntry> commandEntries = CreateCommandEntries(sortedNotes);
        SeeSawLogicalState state = SeeSawLogicalState.Initial;
        int eventId = 0;
        int segmentId = 0;
        int impactId = 0;

        for (int i = 0; i < commandEntries.Count; i++)
        {
            SeeSawCommandEntry entry = commandEntries[i];
            double hitBeat = getBeatAt(entry.SongPosition);
            if (double.IsNaN(hitBeat) || double.IsInfinity(hitBeat))
                continue;

            CompileNote(timeline, entry.SourceNote, entry.SongPosition, entry.Command, ShouldAutoExitAfterHit(commandEntries, i), hitBeat, getSongPositionAtBeat, ref state, ref eventId, ref segmentId, ref impactId);
        }

        timeline.FinalizeOrdering();
        return timeline;
    }

    public static SeeSawTimeline CompileContextualChartNotes(IReadOnlyList<ChartNote> notes, Func<double, double> getBeatAt, Func<double, double> getSongPositionAtBeat)
    {
        SeeSawTimeline timeline = new();
        if (notes == null || getBeatAt == null || getSongPositionAtBeat == null)
            return timeline;

        List<ChartNote> sortedNotes = CreateSortedChartNotes(notes);

        List<SeeSawCommandEntry> commandEntries = CreateCommandEntries(sortedNotes);
        SeeSawLogicalState state = SeeSawLogicalState.Initial;
        int eventId = 0;
        int segmentId = 0;
        int impactId = 0;

        for (int i = 0; i < commandEntries.Count; i++)
        {
            SeeSawCommandEntry entry = commandEntries[i];
            CompileNote(timeline, null, entry.SongPosition, entry.Command, ShouldAutoExitAfterHit(commandEntries, i), getBeatAt(entry.SongPosition), getSongPositionAtBeat, ref state, ref eventId, ref segmentId, ref impactId);
        }

        timeline.FinalizeOrdering();
        return timeline;
    }
}
```

Compilation d'une note:

```csharp
private static void CompileNote(SeeSawTimeline timeline, Note sourceNote, double songPosition, SeeSawCommand command, bool exitAfterHit, double hitBeat, Func<double, double> getSongPositionAtBeat, ref SeeSawLogicalState state, ref int eventId, ref int segmentId, ref int impactId)
{
    SeeSawAction action = command.Action;
    SeeSawCompiledEventTiming timing = CreateTiming(command, state, hitBeat, exitAfterHit);
    if (!timing.IsValid)
    {
        timeline.Errors.Add(CreateInvalidTimingError(songPosition, hitBeat, timing));
        return;
    }

    int currentEventId = eventId++;

    SeeSawPatternEvent patternEvent = new()
    {
        Id = currentEventId,
        SourceNote = sourceNote,
        CueBeat = timing.CueBeat,
        PlayerHitBeat = timing.PlayerHitBeat,
        EndBeat = timing.EndBeat,
        PrepStartBeat = timing.PrepStartBeat,
        CueSongPosition = getSongPositionAtBeat(timing.CueBeat),
        PlayerHitSongPosition = songPosition,
        EndSongPosition = getSongPositionAtBeat(timing.EndBeat),
        PrepStartSongPosition = getSongPositionAtBeat(timing.PrepStartBeat),
        Pattern = timing.Pattern,
        LaunchSide = timing.LaunchSide,
        ApplejackCueSide = timing.ApplejackCueSide,
        TargetSide = timing.TargetSide,
        ApplejackTargetSide = timing.ApplejackTargetSide,
        RainbowHigh = action.IsBigLeap,
        ApplejackHigh = action.HasBigCounterJump,
        IsExit = timing.IsExit,
        Judgement = SeeSawJudgement.Pending
    };
    timeline.PatternEvents.Add(patternEvent);

    if (timing.PrepStartBeat < timing.CueBeat)
        AddSegment(timeline, ref segmentId, currentEventId, SeeSawActor.Applejack, timing.PrepStartBeat, timing.CueBeat, getSongPositionAtBeat, state.ApplejackSide, timing.ApplejackCueSide, action.HasBigCounterJump);

    AddImpact(timeline, ref impactId, currentEventId, SeeSawActor.Applejack, timing.CueBeat, getSongPositionAtBeat, timing.ApplejackCueSide, SeeSawImpactKind.Cue, timing.ApplejackCueSource.JumpLength);
    AddSegment(timeline, ref segmentId, currentEventId, SeeSawActor.RainbowDash, timing.CueBeat, timing.PlayerHitBeat, getSongPositionAtBeat, timing.LaunchSide, timing.TargetSide, action.IsBigLeap);
    AddImpact(timeline, ref impactId, currentEventId, SeeSawActor.RainbowDash, timing.PlayerHitBeat, getSongPositionAtBeat, timing.TargetSide, SeeSawImpactKind.PlayerHit, timing.RainbowSource.JumpLength);

    if (timing.EndBeat > timing.PlayerHitBeat)
    {
        SeeSawImpactKind impactKind = timing.ApplejackTargetSide == SeeSawSide.Exit
            ? SeeSawImpactKind.Exit
            : SeeSawImpactKind.RelayEnd;
        bool high = timing.ApplejackTargetSide != SeeSawSide.Exit && action.HasBigCounterJump;
        AddSegment(timeline, ref segmentId, currentEventId, SeeSawActor.Applejack, timing.PlayerHitBeat, timing.EndBeat, getSongPositionAtBeat, timing.ApplejackCueSide, timing.ApplejackTargetSide, high);
        AddImpact(timeline, ref impactId, currentEventId, SeeSawActor.Applejack, timing.EndBeat, getSongPositionAtBeat, timing.ApplejackTargetSide, impactKind, timing.ApplejackEndSource.JumpLength);
    }

    if (sourceNote != null)
        timeline.NoteToEventId[sourceNote] = currentEventId;

    state = ApplyTiming(timing);
}
```

Conversion des beats compiles en secondes:

```csharp
private static void AddSegment(SeeSawTimeline timeline, ref int segmentId, int eventId, SeeSawActor actor, double startBeat, double endBeat, Func<double, double> getSongPositionAtBeat, SeeSawSide fromSide, SeeSawSide toSide, bool high)
{
    if (endBeat <= startBeat)
        return;

    timeline.JumpSegments.Add(new SeeSawJumpSegment
    {
        Id = segmentId++,
        EventId = eventId,
        Actor = actor,
        StartBeat = startBeat,
        EndBeat = endBeat,
        StartSongPosition = getSongPositionAtBeat(startBeat),
        EndSongPosition = getSongPositionAtBeat(endBeat),
        FromSide = fromSide,
        ToSide = toSide,
        High = high,
        PathId = GetPathId(actor, fromSide, toSide, high)
    });
}

private static void AddImpact(SeeSawTimeline timeline, ref int impactId, int eventId, SeeSawActor actor, double beat, Func<double, double> getSongPositionAtBeat, SeeSawSide side, SeeSawImpactKind kind, SeeSawJumpLength jumpLength)
{
    timeline.ImpactEvents.Add(new SeeSawImpactEvent
    {
        Id = impactId++,
        PatternEventId = eventId,
        Actor = actor,
        Beat = beat,
        SongPosition = getSongPositionAtBeat(beat),
        Side = side,
        Kind = kind,
        JumpLength = jumpLength
    });
}
```

## Code: definition editor See-Saw

Fichier: `Elements/Editor/Notes/SeeSaw/SeeSawEditorNote.cs`

```csharp
public sealed class SeeSawEditorNote : EditorNoteProvider
{
    public override EditorNoteDefinition Definition { get; } = new EditorNoteDefinitionBuilder(EditorNoteKind.SeeSaw, "See Saw")
        .Occupies(beforeBeats: 4, afterBeats: 4)
        .HitWindow(beforeBeats: 0, afterBeats: 4)
        .Timing(new SeeSawEditorNoteTiming())
        .Matches(SeeSawChartNoteMatcher.Matches)
        .Variant("Default", CreateDefaultData())
        .Build();
}
```

Fichier: `Elements/Editor/Notes/SeeSaw/SeeSawEditorNoteTiming.cs`

```csharp
public sealed class SeeSawEditorNoteTiming : IEditorNoteTiming
{
    public double GetStart(EditorNoteDefinition definition, EditorNoteTimingContext context)
    {
        return context.SongPosition - GetBeforeBeats(definition, context) * context.Crotchet;
    }

    public double GetEnd(EditorNoteDefinition definition, EditorNoteTimingContext context)
    {
        return context.SongPosition + Math.Max(definition.HoldBeats, definition.OccupyAfterBeats) * context.Crotchet;
    }

    private static double GetBeforeBeats(EditorNoteDefinition definition, EditorNoteTimingContext context)
    {
        SeeSawAction action = SeeSawAction.FromVariant(definition.GetVariant(context.VariantIndex));
        if (GetBaseDirection(action.Direction) == SeeSawDirection.Exit)
            return global::SeeSawTiming.ExitJumpBeats;

        return GetPhaseBeats(context.BeforeUsesOuterTiming) + GetPhaseBeats(context.AfterUsesOuterTiming);
    }

    private static double GetPhaseBeats(bool usesOuterTiming)
    {
        return usesOuterTiming ? global::SeeSawTiming.LongJumpBeats : global::SeeSawTiming.ShortJumpBeats;
    }
}
```

## Points suspects a investiguer

1. `section_offset = 0` signifie actuellement que la nouvelle grille est ancree exactement sur `effect.SongPosition`. Si l'effet n'est pas exactement sur un beat musical voulu, toute la grille apres BPM est decalee.
2. Les notes apres BPM dans la chart actuelle ne tombent pas sur des beats entiers ou des offsets exacts selon la tempo map actuelle. See-Saw a une tolerance de seulement `0.0005` beat.
3. `Snap(songPosition)` utilise `GetTempoAnchorAt`, mais `GetBeatAt` utilise une autre logique depuis la tentative de correction. Il faut verifier si ces deux concepts doivent etre separes ou unifies.
4. `Conductor.UpdateSongPosition` calcule ses beats avec le BPM courant uniquement, pas avec la tempo map complete. Cela peut creer des differences entre l'affichage/BeatChanged et la logique See-Saw.
5. La validation See-Saw refuse les placements si Applejack n'est pas disponible avant `idealPrepStartBeat` ou exactement chainable au cue. Un decalage minime de beat peut suffire a rendre une note invalide.
6. Il faut clarifier la semantique voulue: BPM change sur un beat global continu, ou nouveau segment local qui redemarre sa propre grille a l'effet.

## Question de fond pour une solution externe

Quel modele de tempo faut-il adopter pour que:

1. Les notes restent stockees en secondes.
2. L'editeur puisse snapper proprement apres un `bpm_change`.
3. `GetBeatAt(songPosition)` reste monotone et musicalement coherent.
4. `GetSongPositionAtBeat(beat)` soit l'inverse fiable de `GetBeatAt`.
5. Les validations See-Saw en beats exacts continuent de fonctionner a travers les changements BPM.
6. `section_offset` ait une semantique claire et non contradictoire avec les beats globaux.
