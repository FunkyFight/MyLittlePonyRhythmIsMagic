# See Saw Empty Tail Exit Plan

## Goal

After a training beatmap reaches its final required success, See Saw must keep the rhythm clock/player running silently and visibly let Applejack return to her base `Exit` position while the level transitions to the `Success` node/dialogue. Non-final training loops must stay seamless and must not reset scene state.

## Findings

- Recent fixes already keep `BeatmapPlayer` and `_sceneManager` updating during dialogue/empty tail.
- The level graph after the tutorial training nodes goes to dialogue, not directly to `End`, so `CompleteLevel()` is not the immediate freeze cause for `edges`/`inner`.
- Current `SeeSawScene.OnBeatmapNotesRemoved()` removes the preloaded next-loop events, updates the director once, then sets `_emptyTailDirectorDrained = true`.
- The preloaded next-loop notes made `SeeSawChartCompiler.ShouldAutoExitAfterHit(...)` return false for the last note of the current loop. Removing those future notes later does not regenerate the missing auto-exit event.
- Result: after final success, the director has no future event that moves Applejack to `Exit`; the empty-tail code mostly settles animation states and can make the characters look frozen.

## Decisions

- Do not reset actors, rebuild the whole scene, or emit `BeatmapLoopAppended` when entering empty tail.
- Keep removing the preloaded future loop notes from `ChartPlayer` so gameplay input/notes are empty.
- Add a See Saw-specific synthetic exit/drain event with no source note/input when empty tail starts.
- Let `SeeSawDirector.Update(...)` keep running during empty tail until that synthetic exit is complete, then settle idle animations.
- Leave `BeatmapPlayer`, `LevelRuntimeController`, and `Game1` empty-clock behavior mostly unchanged unless validation shows the clock is not advancing.

## Implementation Steps

1. Update `Gameplay/SeeSaw/SeeSawRuntime.cs` to support a synthetic empty-tail exit.

2. In `SeeSawDirector`, add state for the synthetic tail exit, for example `_emptyTailExitEventId`, `_emptyTailExitEndBeat`, and `_emptyTailExitScheduled`.

3. Add a director method such as `BeginEmptyTailExit(double beat, double songPosition)`:
- If no timeline exists, return immediately.
- If an empty-tail exit is already scheduled, return without duplicating it.
- Determine Applejack's current grounded side with the existing timeline state, normally `_timeline.GetLastGroundedSide(SeeSawActor.Applejack, beat)`.
- If Applejack is already on `SeeSawSide.Exit` and has no active segment, mark the tail as complete.
- Otherwise add a synthetic `SeeSawPatternEvent`, `SeeSawJumpSegment`, and `SeeSawImpactEvent` from the current side to `SeeSawSide.Exit`.
- Use `SeeSawTiming.ExitJumpBeats` for the visible return duration unless a concrete existing duration helper is more appropriate.
- Generate IDs from current max IDs in `PatternEvents`, `JumpSegments`, and `ImpactEvents` plus one.
- Use `SourceNote = null`, `IsExit = true`, and `SeeSawImpactKind.Exit` so no input or judgement is involved.
- Call `FinalizeOrdering()` after adding the synthetic entries.

4. Pass song-position mapping into `SeeSawDirector` if needed.
- Prefer adding a `Func<double, double> getSongPositionAtBeat` constructor parameter and field, because synthetic segment/impact entries need `StartSongPosition` and `EndSongPosition`.
- Update the `SeeSawScene.SetupTimelineAndDirector()` constructor call to pass `GLOBALS.beatmapPlayer.GetSongPositionAtBeat`.
- Preserve existing constructor overloads by defaulting older overloads to a simple crotchet-based conversion.

5. Add a director method such as `IsEmptyTailExitComplete(double beat)`:
- Return true only after the synthetic exit is scheduled/completed and `beat >= _emptyTailExitEndBeat`.
- Treat “already at Exit and no active segment” as complete.
- Do not rely only on animation state names, because an idle state before the synthetic segment starts can falsely mark the tail drained.

6. Update `Scenes/MiniGames/SeeSaw.cs` `OnBeatmapNotesRemoved(...)`:
- Keep `_director?.RemoveEventsForNotes(notes)`.
- Remove the synchronous one-frame `_director.Update(...)` drain and the immediate `_emptyTailDirectorDrained = true` assignment.
- Compute current `songSeconds` and `beat` from `GLOBALS.beatmapPlayer.GameplaySongPosition` / `GetBeatAt(...)`.
- Call the new `_director.BeginEmptyTailExit(beat, songSeconds)`.
- Set `_emptyTailDirectorDrained = false`.

7. Update `SeeSawScene.DrainDirectorToFinalGroundedPose(...)`:
- Always call `UpdateDirector(gameTime)` while empty tail is active and not drained.
- After updating, compute the current beat and set `_emptyTailDirectorDrained` only when `_director.IsEmptyTailExitComplete(beat)` is true and actors are not in `jump`/`fall`.
- Once drained, allow `UpdateEmptyTailAnimations()` to settle `land` into `start_idle`/`idle`.

8. Keep `UpdateEmptyTailAnimations()` conservative.
- Do not force `jump`/`fall` directly into idle.
- Let the director move Applejack and trigger the exit impact first.
- Only settle completed `land`/`fail` style states after the synthetic exit is complete.

9. Do not change training loop bounds unless validation reveals a separate loop-bound regression.
- Existing intended bounds remain: `inner` loop end `4.8s`, `edges` loop end `7.2s`.
- Non-final loops should still append the next loop and suppress auto-exit for seamless repetition.

## Validation

1. Build with:

```powershell
dotnet build -p:OutDir="C:\Users\Alexis\AppData\Local\Temp\kilo\MLP_RiM_build\"
```

2. Manual test `Levels/New Level/level.xml`, first training node `Beatmaps/tutorials/see saw/edges/chart.xml`:
- Non-final successes loop seamlessly without scene reset.
- On the final required success, music becomes silent/muted.
- Applejack visibly returns to the base `Exit` position during the empty tail.
- Dialogue appears after the existing one-beat delay without stopping the See Saw scene animation.

3. Manual test `Beatmaps/tutorials/see saw/inner/chart.xml`:
- Non-final loops remain seamless.
- On final success, Applejack does not freeze on the inner/outer position; she returns to `Exit` while the empty clock continues.

4. Regression checks:
- No actor teleport on empty-tail start.
- `OnBeatmapStarted()` still resets actors only for real beatmap starts.
- Representation beatmap completion still depends on `MusicPlaybackFinished`, not `ChartPlayer.IsFinished()`.
- Starting a later beatmap or switching mini-game can still replace/stop the empty tail intentionally.

## Risks

- If a future level connects `Success` immediately to `End`, `CompleteLevel()` still stops the beatmap and returns to menu by design; that is a separate product decision.
- If a `SetMiniGame` or chart switch follows immediately, the See Saw scene will be replaced and the See Saw tail will not be visible, which matches “until something else explicitly replaces/stops it.”
- Variable-tempo charts require the synthetic exit to use `GetSongPositionAtBeat`; avoid approximating song positions with a fixed crotchet if that function is available.
