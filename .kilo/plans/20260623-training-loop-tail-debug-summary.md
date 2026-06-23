# Training Loop / Empty Tail Debug Summary

## Situation

The current issue is NOT solved.

User reports that after training completion the characters still freeze / stop moving, even though the intended behavior is that the conductor / beatmap player keeps running an empty map in the background.

Last user feedback:

> TOUJOURS RIEN TU STAGNES LA. RESUME LE PROBLEME ET TOUTES LES INFOS DANS UN.MD

Previous specific feedback:

> non, les persos freeze et ne bougent plus comme si la beatmap etait arretee alors qu'il fallait faire tourner le conductor / beatmap player a vide

## Intended Behavior

### Training beatmap loop

- Training beatmaps loop until the required success count is reached.
- The scene state must NOT reset between loops.
- The gameplay timeline must be monotonic, not reset to 0.
- Each next loop should be appended after the current loop on the same timeline.
- The music should handle its own loop independently from the beatmap loop.
- First loop is normal.
- Later loops should skip the initial non-gameplay silence / offset / prelude.
- Loop start should be the first playable note/clip time.
- Loop end should be the latest end of the playable note/clip, not just the last tap note.

### Final training success

- When the final required success is achieved:
- Do NOT immediately stop the scene.
- Do NOT reset actors.
- Do NOT teleport actors.
- Do NOT freeze actors.
- Stop/kill audible training music or make it silent.
- Continue running the conductor / beatmap player with an empty map.
- This empty map must keep advancing time.
- This empty map must continue even after leaving the training node.
- Wait 1 beat before officially following the training `Success` connection.
- Even after the `Success` node transition, the empty background rhythm scene/player should keep running until something else explicitly replaces/stops it.

### Representation beatmap

- Play representation beatmaps should complete at the end of the MUSIC.
- They should NOT complete when `ChartPlayer.IsFinished()` becomes true.

## Repro / Reference Beatmaps

### `Beatmaps/tutorials/see saw/inner/chart.xml`

Relevant data:

- BPM: 100
- Offset: 0
- Notes:
- Note 1 at `SongPosition=2.4`, `BeatPosition=4`
- Note 2 at `SongPosition=3.6`, `BeatPosition=6`
- Editor clips:
- Clip 1: `StartBeat=4`, `LengthBeats=2`, so `2.4s -> 3.6s`
- Clip 2: `StartBeat=6`, `LengthBeats=2`, so `3.6s -> 4.8s`

Expected training loop bounds:

- Playable start: `2.4s`
- Loop end: `4.8s`
- Loop duration after first loop: `2.4s`

Important detail: the last tap note is at `3.6s`, but the playable pattern visually ends at `4.8s` because the clip continues after the tap.

### `Beatmaps/tutorials/see saw/edges/chart.xml`

Relevant data:

- BPM: 100
- Offset: 0
- Notes:
- Note 1 at `SongPosition=2.4`, `BeatPosition=4`
- Note 2 at `SongPosition=4.8`, `BeatPosition=8`
- Editor clips:
- Clip 1: `StartBeat=4`, `LengthBeats=4`, so `2.4s -> 4.8s`
- Clip 2: `StartBeat=8`, `LengthBeats=4`, so `4.8s -> 7.2s`

Expected training loop bounds:

- Playable start: `2.4s`
- Loop end: `7.2s`
- Loop duration after first loop: `4.8s`

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
- Pre-append next loop via `_beatmapPlayer.AppendBeatmapLoopAt(... skipInitialOffset: true)`.
- On each loop boundary, judge `_trainingCurrentLoopNotes` directly.
- On non-final success, continue loop.
- On failure/miss, continue loop without incrementing success.
- On final success, call `BeginTrainingSuccessExitDelay()`.

Current `BeginTrainingSuccessExitDelay()`:

- Sets `_trainingSuccessExitPending = true`.
- Sets `_trainingSuccessExitSongPosition = current position + 1 beat`.
- Calls `_beatmapPlayer.ContinueEmptyBeatmapWithoutMusic(_trainingNextLoopNotes)`.

Current `UpdateTrainingLoopProgress()`:

- If pending success exit, waits until `GameplaySongPosition >= _trainingSuccessExitSongPosition`, then calls `FollowOutput("Success")`.
- It exits the loop immediately when `_trainingSuccessExitPending` becomes true to avoid an infinite `while` freeze.

Current `ShouldUpdateRhythmScene`:

```csharp
public bool ShouldUpdateRhythmScene => IsBeatmapActive || _beatmapPlayer.IsContinuingEmptyBeatmap;
```

Current `AcceptsRhythmInput`:

```csharp
public bool AcceptsRhythmInput => IsBeatmapActive;
```

Dialogue entry currently does NOT stop the beatmap if `IsContinuingEmptyBeatmap` is true.

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

Current `ContinueEmptyBeatmapWithoutMusic(...)` does this:

- Removes `_trainingNextLoopNotes` from `ChartPlayer`.
- Emits `BeatmapNotesRemoved`.
- Sets `_loopMusic = false`.
- Sets `_usesIndependentBeatmapClock = true`.
- Sets `_continueGameplayWithoutMusic = true`.
- Sets conductor music volume to `0`.
- Ensures conductor is playing.

Important: a previous version called `Conductor.Pause()` here. That caused obvious freezing because several systems check `Conductor.isPlaying()`. It was changed to mute instead.

Current `BeatmapPlayer.Update(...)` only advances rhythm state if:

```csharp
isMusicPlaying || _continueGameplayWithoutMusic
```

When this happens it updates:

- `Clock`
- editor timeline effects
- `ChartPlayer`
- `VisualNoteMng`
- `_beatmapSongPosition` if independent clock is enabled

### `Game1.cs`

Important current change:

```csharp
bool levelRuntimeRhythmSceneActive = levelRuntimeActive && _levelRuntimeController.ShouldUpdateRhythmScene;
```

This is used to decide whether to update:

- `GLOBALS.beatmapPlayer.Update(gameTime)`
- `_sceneManager.Update(gameTime)`

The intent was to continue updating the rhythm scene even after the training node transitions to dialogue/another node, as long as the empty background beatmap is still active.

Current user feedback says this still did not visibly fix the freeze.

### `Scenes/MiniGames/SeeSaw.cs`

Relevant events:

- Subscribes to `BeatmapStarted`.
- Subscribes to `BeatmapLoopAppended`.
- Subscribes to `BeatmapNotesRemoved`.

Current behavior:

- `OnBeatmapStarted()` calls `ResetActors()`, `SetupTimelineAndDirector()`, etc.
- `OnBeatmapLoopAppended()` calls `SetupTimelineAndDirector()`.
- `OnBeatmapNotesRemoved(...)` calls `_director?.RemoveEventsForNotes(notes)`.

Important past bug:

- `ContinueEmptyBeatmapWithoutMusic(...)` previously emitted `BeatmapLoopAppended` after removing the preloaded loop.
- SeeSaw responded by rebuilding/resetting the director.
- That caused actor teleporting.
- This was changed to emit `BeatmapNotesRemoved` instead.

Current suspected issue:

- Even though `BeatmapNotesRemoved` avoids director reset, SeeSaw may still stop/override animations during empty tail.
- `SeeSawDirector.Update(...)` continues to force actor pose/state each frame based on current timeline.
- If no active segment exists after removing future events, `ApplyActor(...)` may force idle/grounded state every frame.
- This may make it look like the characters freeze or stop their ending animation.
- Need verify whether empty-tail should update director, or only update animation state machines / scene time.

### `Gameplay/SeeSaw/SeeSawRuntime.cs`

Added:

- `SeeSawTimeline.RemoveEventsForNotes(...)`
- `SeeSawDirector.RemoveEventsForNotes(...)`

Purpose:

- Remove preloaded next-loop events from the active SeeSaw timeline without reconstructing the director and without resetting actors.

Possible issue:

- Removing future events may leave the current director with no active future segment, and `Update(...)` may keep applying grounded/idle state immediately after the final loop boundary.

### `../../MonogameLibs/RhythmConductor/RhythmConductor/Note/ChartPlayer.cs`

Added:

- `AppendChart(...)` now returns the appended `Note` objects.
- `RemoveNotes(...)` removes note objects from `_notes` and `_activeNotes`, resets visible window.

Purpose:

- Preload the next loop, track which notes belong to it, then remove them on final training success so the background map is empty.

Potential issue:

- Removing notes only affects chart/reaction data. It does not directly tell every mini-game-specific timeline compiler to preserve current ending animations.

### `../../MonogameLibs/RhythmConductor/RhythmConductor/Conductor/Conductor.cs`

Added:

- `SetMusicVolume(float musicVolume)`.
- `MusicVolume` changed to private setter.

Purpose:

- Mute training tail without pausing/stopping the conductor.

## Things That Were Tried And Failed / Were Insufficient

### 1. Restart beatmap at 0

Initial approach reset the beatmap loop back to 0.

Rejected because:

- User wanted a monotonic timeline.
- Scene state must not reset.
- Music loops independently.

### 2. Append full chart including offset/silence

Rejected because:

- It caused a pause at the start of every loop.
- User wanted subsequent loops to start at playable content.

### 3. Append from `Offset`

Insufficient because:

- Some charts have `Offset=0` but first note at `2.4s`.
- Example: `inner` has silence before first note despite offset 0.

### 4. End loop at last note position

Insufficient because:

- `inner` last note is at `3.6s`, but the visual clip ends at `4.8s`.
- Need latest end of playable notes/clips.

### 5. Use `missWindow` as extra end delay

Rejected because:

- It created a visible pause.
- User wanted seamless loop at the real end of the latest stopping note/clip.

### 6. Stop beatmap at final training success

Rejected because:

- End animations need the scene/player to keep running.

### 7. Pause conductor during empty tail

Rejected because:

- Several systems and scenes check `Conductor.isPlaying()` or depend on active conductor/player updates.
- This made characters freeze.

### 8. Emit `BeatmapLoopAppended` when clearing future notes

Rejected because:

- SeeSaw rebuilt/reset the director.
- Characters teleported.

### 9. Continue empty beatmap but only while state is `TrainingBeatmap`

Rejected because:

- User explicitly clarified that it must keep running even after exiting the training node.

## Current Observed Problem

Despite the current changes, user still reports:

- No visible change.
- Characters freeze / stop moving as if the beatmap was stopped.
- Expected: conductor / beatmap player should run empty in the background even after leaving training.

## Strong Suspicions / Next Debug Points

### 1. SeeSawDirector may force idle every frame after future events are removed

Current SeeSaw director update does:

- Get active segment for current beat.
- If none, `ApplyActor(...)` computes last grounded side and calls `controller.SetIdleForSide(...)`.

If empty tail should preserve the current landing/end animation, then continuing director update with an empty future timeline may be wrong.

Possible next test:

- During `BeatmapPlayer.IsContinuingEmptyBeatmap`, skip `_director.Update(...)` in `SeeSawScene.Update(...)` but still update `RainbowState`, `ApplejackState`, `SeeSawState`.
- This would allow current animation state machines to continue without director forcing a new pose.
- However this must be checked carefully because user said conductor/beatmap player should keep running empty, not necessarily that director should keep evaluating an empty timeline.

### 2. Game1 update gating might still not cover all draw/update cases

Current code uses `ShouldUpdateRhythmScene`, but verify at runtime:

- Is `_levelRuntimeController.ShouldUpdateRhythmScene` true after `FollowOutput("Success")`?
- Is `GLOBALS.beatmapPlayer.IsContinuingEmptyBeatmap` true after entering the next node?
- Is `_sceneManager.Update(gameTime)` still called?
- Is `GLOBALS.beatmapPlayer.Update(gameTime)` still called?

Add temporary logs/status if needed.

### 3. Entering the next node may still stop or replace the scene

Dialogue was changed not to stop the empty beatmap, but other nodes may still do things:

- `SetMiniGame` may switch scene.
- `End` / `CompleteLevel` stops beatmap.
- Any future beatmap node starts a new beatmap and disposes previous one.

Need inspect actual level graph after the training node.

### 4. Muting conductor but letting it reach song end may affect `isPlaying()`

If conductor reaches actual audio duration and stops, `_continueGameplayWithoutMusic` should still let `BeatmapPlayer.Update(...)` advance. But scenes that directly check `Conductor.isPlaying()` may still stop some logic.

Known direct check:

- `SeaPonyParade` has `if (GLOBALS.beatmapPlayer.Conductor.isPlaying())` around some logic.

SeeSaw mostly checks `Conductor != null`, not `isPlaying()`, but other mini-games may differ.

Possible next fix:

- Add `BeatmapPlayer.IsRhythmClockActive` and replace mini-game `Conductor.isPlaying()` checks with that.

### 5. `Clock` may not advance correctly if using conductor-derived clock

During training, independent clock is enabled and `_beatmapSongPosition` should advance. `Clock.Update(GameplaySongPosition)` should keep advancing.

Need verify with runtime logging:

- `GameplaySongPosition`
- `Clock.SongSeconds`
- `Clock.Beat`
- `_continueGameplayWithoutMusic`

## Commands / Validation

Build command used repeatedly:

```powershell
dotnet build -p:OutDir="C:\Users\Alexis\AppData\Local\Temp\kilo\MLP_RiM_build\"
```

Build currently succeeds with 0 errors.

Known warnings are preexisting / non-blocking:

- `ChartPlayer.BeatmapStarted` unused
- `SeeSawDirector._hasLastRainbowTrailPosition` assigned but unused
- `SeaPonyParade._elapsed` assigned but unused

## Important User Preferences / Constraints

- Do not reset scene state between training loops.
- Do not reintroduce native dialogs.
- Do not make training output depend on a `Failure` connection.
- Training overlay should be simple white bottom-center text: `Do it X more time(s)`.
- The tail after final training success should run empty in the background, including after leaving the training node.
- The representation beatmap should wait for music end.

## High-Value Next Step

Most likely useful next test/fix:

1. Add temporary logging or on-screen debug for `ShouldUpdateRhythmScene`, `IsContinuingEmptyBeatmap`, `GameplaySongPosition`, `Clock.Beat`, and whether `SeeSawScene.Update` runs after final training success.
2. If those values advance but characters still freeze, change SeeSaw empty-tail behavior so `SeeSawDirector.Update(...)` is skipped while `IsContinuingEmptyBeatmap` is true, but actor animation state machines still update.
3. If those values do not advance, fix `Game1` / `LevelRuntimeController` update gating first.
