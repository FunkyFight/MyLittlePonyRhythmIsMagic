# Training Loop / Empty Tail Debug Summary

## Situation

The current issue is **not solved**.

After training completion, the characters still freeze or stop moving, even though the intended behavior is for the conductor and beatmap player to keep running an empty map in the background.

Latest user feedback:

> TOUJOURS RIEN TU STAGNES LA. RESUME LE PROBLEME ET TOUTES LES INFOS DANS UN.MD

Previous specific feedback:

> non, les persos freeze et ne bougent plus comme si la beatmap etait arretee alors qu'il fallait faire tourner le conductor / beatmap player a vide

## Intended Behavior

### Training beatmap loop

- Training beatmaps loop until the required success count is reached.
- The scene state must **not** reset between loops.
- The gameplay timeline must be monotonic and must not reset to `0`.
- Each next loop should be appended after the current loop on the same timeline.
- The music should handle its own loop independently from the beatmap loop.
- The first loop is normal.
- Later loops should skip the initial non-gameplay silence, offset, or prelude.
- Loop start should be the first playable note or clip time.
- Loop end should be the latest end of the playable note or clip, not merely the position of the last tap note.

### Final training success

When the final required success is achieved:

- Do **not** immediately stop the scene.
- Do **not** reset actors.
- Do **not** teleport actors.
- Do **not** freeze actors.
- Stop or kill the audible training music, or make it silent.
- Continue running the conductor and beatmap player with an empty map.
- This empty map must keep advancing time.
- This empty map must continue even after leaving the training node.
- Wait `1` beat before officially following the training `Success` connection.
- Even after the `Success` node transition, the empty background rhythm scene/player should keep running until something else explicitly replaces or stops it.

### Representation beatmap

- Play-representation beatmaps should complete at the end of the **music**.
- They should **not** complete when `ChartPlayer.IsFinished()` becomes true.

## Reproduction / Reference Beatmaps

### `Beatmaps/tutorials/see saw/inner/chart.xml`

Relevant data:

- BPM: `100`
- Offset: `0`
- Notes:
  - Note 1 at `SongPosition=2.4`, `BeatPosition=4`
  - Note 2 at `SongPosition=3.6`, `BeatPosition=6`
- Editor clips:
  - Clip 1: `StartBeat=4`, `LengthBeats=2`, so `2.4s -> 3.6s`
  - Clip 2: `StartBeat=6`, `LengthBeats=2`, so `3.6s -> 4.8s`

Expected training loop bounds:

- Playable start: `2.4s`
- Loop end: `4.8s`
- Loop duration after the first loop: `2.4s`

Important detail: the last tap note is at `3.6s`, but the playable pattern visually ends at `4.8s` because the clip continues after the tap.

### `Beatmaps/tutorials/see saw/edges/chart.xml`

Relevant data:

- BPM: `100`
- Offset: `0`
- Notes:
  - Note 1 at `SongPosition=2.4`, `BeatPosition=4`
  - Note 2 at `SongPosition=4.8`, `BeatPosition=8`
- Editor clips:
  - Clip 1: `StartBeat=4`, `LengthBeats=4`, so `2.4s -> 4.8s`
  - Clip 2: `StartBeat=8`, `LengthBeats=4`, so `4.8s -> 7.2s`

Expected training loop bounds:

- Playable start: `2.4s`
- Loop end: `7.2s`
- Loop duration after the first loop: `4.8s`

This chart seemed closer to working because the visual end was less misleading than `inner`.

## Files Touched / Current Relevant Code

### `Elements/Levels/LevelRuntimeController.cs`

Current training logic has these fields:

- `_trainingLoopDuration`
- `_trainingCurrentLoopEndSongPosition`
- `_trainingSuccessExitPending`
- `_trainingSuccessExitSongPosition`
- `_trainingCurrentLoopNotes`
- `_trainingNextLoopNotes`

Important methods:

- `StartBeatmapNode(...)`
- `InitializeTrainingLoop()`
- `UpdateTrainingLoopProgress()`
- `HandleTrainingLoopFinished()`
- `BeginTrainingSuccessExitDelay()`
- `ContinueTrainingLoop()`

Current intended flow:

- Start training with `BeatmapPlayer.StartBeatmap(... independentBeatmapClock: true, loopMusic: true)`.
- Compute loop bounds from `BeatmapPlayer.RuntimeChartPlayableStartSongPosition` and `BeatmapPlayer.RuntimeChartLoopEndSongPosition`.
- Pre-append the next loop via `_beatmapPlayer.AppendBeatmapLoopAt(... skipInitialOffset: true)`.
- On each loop boundary, judge `_trainingCurrentLoopNotes` directly.
- On non-final success, continue looping.
- On failure or miss, continue looping without incrementing success.
- On final success, call `BeginTrainingSuccessExitDelay()`.

Current `BeginTrainingSuccessExitDelay()`:

- Sets `_trainingSuccessExitPending = true`.
- Sets `_trainingSuccessExitSongPosition = current position + 1 beat`.
- Calls `_beatmapPlayer.ContinueEmptyBeatmapWithoutMusic(_trainingNextLoopNotes)`.

Current `UpdateTrainingLoopProgress()`:

- If a success exit is pending, wait until `GameplaySongPosition >= _trainingSuccessExitSongPosition`, then call `FollowOutput("Success")`.
- Exit the loop immediately when `_trainingSuccessExitPending` becomes true to avoid an infinite `while` freeze.

Current `ShouldUpdateRhythmScene`:

```csharp
public bool ShouldUpdateRhythmScene => IsBeatmapActive || _beatmapPlayer.IsContinuingEmptyBeatmap;
```

Current `AcceptsRhythmInput`:

```csharp
public bool AcceptsRhythmInput => IsBeatmapActive;
```

Dialogue entry currently does **not** stop the beatmap if `IsContinuingEmptyBeatmap` is true.

### `Gameplay/BeatmapPlayer.cs`

Added concepts:

- `GameplaySongPosition`
- `_usesIndependentBeatmapClock`
- `_loopMusic`
- `_beatmapSongPosition`
- `_continueGameplayWithoutMusic`
- `IsContinuingEmptyBeatmap`
- `MusicPlaybackFinished`
- `RuntimeChartPlayableStartSongPosition`
- `RuntimeChartLoopEndSongPosition`
- `AppendBeatmapLoopAt(...)`
- `ContinueEmptyBeatmapWithoutMusic(...)`
- `BeatmapLoopAppended`
- `BeatmapNotesRemoved`

Current `ContinueEmptyBeatmapWithoutMusic(...)` does the following:

- Removes `_trainingNextLoopNotes` from `ChartPlayer`.
- Emits `BeatmapNotesRemoved`.
- Sets `_loopMusic = false`.
- Sets `_usesIndependentBeatmapClock = true`.
- Sets `_continueGameplayWithoutMusic = true`.
- Sets conductor music volume to `0`.
- Ensures the conductor is playing.

Important: an earlier version called `Conductor.Pause()` here. That caused obvious freezing because several systems check `Conductor.isPlaying()`. It was changed to mute instead.

Current `BeatmapPlayer.Update(...)` only advances rhythm state if:

```csharp
isMusicPlaying || _continueGameplayWithoutMusic
```

When this condition is true, it updates:

- `Clock`
- Editor timeline effects
- `ChartPlayer`
- `VisualNoteMng`
- `_beatmapSongPosition` when the independent clock is enabled

### `Game1.cs`

Important current change:

```csharp
bool levelRuntimeRhythmSceneActive = levelRuntimeActive && _levelRuntimeController.ShouldUpdateRhythmScene;
```

This is used to decide whether to update:

- `GLOBALS.beatmapPlayer.Update(gameTime)`
- `_sceneManager.Update(gameTime)`

The intent was to continue updating the rhythm scene even after the training node transitions to dialogue or another node, provided that the empty background beatmap is still active.

Current user feedback says this still did not visibly fix the freeze.

### `Scenes/MiniGames/SeeSaw.cs`

Relevant events:

- Subscribes to `BeatmapStarted`.
- Subscribes to `BeatmapLoopAppended`.
- Subscribes to `BeatmapNotesRemoved`.

Current behavior:

- `OnBeatmapStarted()` calls `ResetActors()`, `SetupTimelineAndDirector()`, and related setup.
- `OnBeatmapLoopAppended()` calls `SetupTimelineAndDirector()`.
- `OnBeatmapNotesRemoved(...)` calls `_director?.RemoveEventsForNotes(notes)`.

Important past bug:

- `ContinueEmptyBeatmapWithoutMusic(...)` previously emitted `BeatmapLoopAppended` after removing the preloaded loop.
- SeeSaw responded by rebuilding or resetting the director.
- That caused actor teleporting.
- This was changed to emit `BeatmapNotesRemoved` instead.

Current suspected issue:

- Although `BeatmapNotesRemoved` avoids a director reset, SeeSaw may still stop or override animations during the empty tail.
- `SeeSawDirector.Update(...)` continues to force actor pose/state every frame based on the current timeline.
- If no active segment exists after removing future events, `ApplyActor(...)` may force the idle or grounded state every frame.
- This may make it look as though the characters freeze or stop their ending animation.
- It remains necessary to verify whether the empty tail should update the director or only update animation state machines and scene time.

### `Gameplay/SeeSaw/SeeSawRuntime.cs`

Added:

- `SeeSawTimeline.RemoveEventsForNotes(...)`
- `SeeSawDirector.RemoveEventsForNotes(...)`

Purpose:

- Remove preloaded next-loop events from the active SeeSaw timeline without reconstructing the director and without resetting actors.

Possible issue:

- Removing future events may leave the current director with no active future segment, and `Update(...)` may keep applying a grounded or idle state immediately after the final loop boundary.

### `../../MonogameLibs/RhythmConductor/RhythmConductor/Note/ChartPlayer.cs`

Added:

- `AppendChart(...)` now returns the appended `Note` objects.
- `RemoveNotes(...)` removes note objects from `_notes` and `_activeNotes`, then resets the visible window.

Purpose:

- Preload the next loop, track which notes belong to it, then remove them on final training success so that the background map is empty.

Potential issue:

- Removing notes only affects chart and reaction data. It does not directly tell every mini-game-specific timeline compiler to preserve current ending animations.

### `../../MonogameLibs/RhythmConductor/RhythmConductor/Conductor/Conductor.cs`

Added:

- `SetMusicVolume(float musicVolume)`.
- `MusicVolume` changed to a private setter.

Purpose:

- Mute the training tail without pausing or stopping the conductor.

## Things Tried That Failed or Were Insufficient

### 1. Restart the beatmap at `0`

Initial approach reset the beatmap loop back to `0`.

Rejected because:

- The user wanted a monotonic timeline.
- Scene state must not reset.
- Music loops independently.

### 2. Append the full chart, including offset or silence

Rejected because:

- It caused a pause at the beginning of every loop.
- Later loops should begin at playable content.

### 3. Append from `Offset`

Insufficient because:

- Some charts have `Offset=0` but their first note occurs at `2.4s`.
- Example: `inner` contains silence before its first note despite having an offset of `0`.

### 4. End the loop at the last note position

Insufficient because:

- In `inner`, the last note occurs at `3.6s`, while the visual clip ends at `4.8s`.
- The end must use the latest end of playable notes or clips.

### 5. Use `missWindow` as an extra end delay

Rejected because:

- It created a visible pause.
- The loop should be seamless at the actual end of the latest stopping note or clip.

### 6. Stop the beatmap at final training success

Rejected because:

- Ending animations require the scene and player to keep running.

### 7. Pause the conductor during the empty tail

Rejected because:

- Several systems and scenes check `Conductor.isPlaying()` or otherwise depend on active conductor/player updates.
- This made characters freeze.

### 8. Emit `BeatmapLoopAppended` when clearing future notes

Rejected because:

- SeeSaw rebuilt or reset the director.
- Characters teleported.

### 9. Continue the empty beatmap only while the state is `TrainingBeatmap`

Rejected because:

- The empty background beatmap must keep running even after leaving the training node.

## Current Observed Problem

Despite the current changes, the user still reports:

- No visible change.
- Characters freeze or stop moving as though the beatmap had stopped.
- Expected behavior: the conductor and beatmap player should continue running an empty map in the background even after training ends.

## Strong Suspicions / Next Debug Points

### 1. `SeeSawDirector` may force idle every frame after future events are removed

The current SeeSaw director update does the following:

- Gets the active segment for the current beat.
- If there is no active segment, `ApplyActor(...)` computes the last grounded side and calls `controller.SetIdleForSide(...)`.

If the empty tail should preserve the current landing or ending animation, continuing to update the director against an empty future timeline may be wrong.

Possible next test:

- While `BeatmapPlayer.IsContinuingEmptyBeatmap` is true, skip `_director.Update(...)` in `SeeSawScene.Update(...)`.
- Continue updating `RainbowState`, `ApplejackState`, and `SeeSawState`.
- This should let current animation state machines continue without the director forcing a new pose.
- Check this carefully: the conductor and beatmap player must continue running empty, but the director does not necessarily need to keep evaluating an empty timeline.

### 2. `Game1` update gating may still not cover all update/draw paths

Verify at runtime after final training success:

- Is `_levelRuntimeController.ShouldUpdateRhythmScene` still true after `FollowOutput("Success")`?
- Is `GLOBALS.beatmapPlayer.IsContinuingEmptyBeatmap` still true after entering the next node?
- Is `_sceneManager.Update(gameTime)` still called?
- Is `GLOBALS.beatmapPlayer.Update(gameTime)` still called?

Temporary logging or an on-screen status display may be necessary.

### 3. Entering the next node may still stop or replace the scene

Dialogue was changed not to stop the empty beatmap, but other nodes may still do so:

- `SetMiniGame` may switch scenes.
- `End` or `CompleteLevel` stops the beatmap.
- A later beatmap node may start a new beatmap and dispose of the previous one.

Inspect the actual level graph after the training node.

### 4. Muting the conductor while allowing it to reach song end may affect `isPlaying()`

If the conductor reaches the actual audio duration and stops, `_continueGameplayWithoutMusic` should still let `BeatmapPlayer.Update(...)` advance. However, scenes that directly check `Conductor.isPlaying()` may still stop some logic.

Known direct check:

- `SeaPonyParade` wraps some logic in `if (GLOBALS.beatmapPlayer.Conductor.isPlaying())`.

SeeSaw mostly checks whether `Conductor != null`, rather than `isPlaying()`, but other mini-games may differ.

Possible next fix:

- Add `BeatmapPlayer.IsRhythmClockActive`.
- Replace mini-game checks of `Conductor.isPlaying()` with the new rhythm-clock-active condition where appropriate.

### 5. `Clock` may not advance correctly if it remains conductor-derived

During training, the independent clock is enabled and `_beatmapSongPosition` should advance. `Clock.Update(GameplaySongPosition)` should therefore continue advancing.

Verify through runtime logging:

- `GameplaySongPosition`
- `Clock.SongSeconds`
- `Clock.Beat`
- `_continueGameplayWithoutMusic`

## Commands / Validation

Build command used repeatedly:

```powershell
dotnet build -p:OutDir="C:\Users\Alexis\AppData\Local\Temp\kilo\MLP_RiM_build\"
```

The build currently succeeds with `0` errors.

Known warnings are pre-existing or non-blocking:

- `ChartPlayer.BeatmapStarted` unused
- `SeeSawDirector._hasLastRainbowTrailPosition` assigned but unused
- `SeaPonyParade._elapsed` assigned but unused

## Important User Preferences / Constraints

- Do not reset scene state between training loops.
- Do not reintroduce native dialogs.
- Do not make training output depend on a `Failure` connection.
- The training overlay should be simple white bottom-center text: `Do it X more time(s)`.
- The tail after final training success should run empty in the background, including after leaving the training node.
- The representation beatmap should wait for music end.

## Highest-Value Next Step

1. Add temporary logging or an on-screen debug display for `ShouldUpdateRhythmScene`, `IsContinuingEmptyBeatmap`, `GameplaySongPosition`, `Clock.Beat`, and whether `SeeSawScene.Update` runs after final training success.
2. If those values advance but the characters still freeze, change SeeSaw empty-tail behavior so that `SeeSawDirector.Update(...)` is skipped while `IsContinuingEmptyBeatmap` is true, while actor animation state machines continue updating.
3. If those values do not advance, fix the `Game1` / `LevelRuntimeController` update gating first.
