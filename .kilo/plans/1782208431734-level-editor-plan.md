# Level Editor And Runtime Plan

## Context

- The game now starts on `Scenes/MainMenu.cs` with `Jouer`, `Éditeur de niveau`, and `Éditeur de beatmap`; currently only the beatmap editor action is wired.
- `Game1.cs` owns the `SceneManager`, global `BeatmapPlayer`, and the existing beatmap editor overlay (`GLOBALS.beatmapEditorElement`).
- Beatmaps are package folders at project root under `Beatmaps/<Name>/chart.xml` plus song assets. Levels must follow the same root-file style under `Levels/<Name>/level.xml`.
- The existing dev UI (`DevUiRenderer`, `DevUiFloatingWindow`, `DevUiFont`) is enough for tool chrome, but runtime dialogues need a real `SpriteFont` asset.
- `ChartPlayer` exposes `IsFinished()`, `PerfectCount`, `EarlyCount`, `LateCount`, and `MissCount`; training success is defined as finishing a beatmap loop with `MissCount == 0`.

## Locked Decisions

- Scope: full level editor plus playable runtime.
- Level files: `Levels/<LevelFolder>/level.xml`.
- Project output: copy `Levels/**/*` via `.csproj`; do not put root-level levels through `Content.mgcb`.
- Runtime save: AppData `MLP_RiM/save.json`, storing unlocked level UUIDs.
- Level identity: explicit UUID in every `level.xml`; unlocks reference UUIDs.
- Main menu `Jouer`: opens a simple level list. Locked levels are visible, grayed out, and not launchable.
- Level metadata: `LockedByDefault` and `UnlockLevelIds` on the level.
- Node types for v1: `Start`, `Dialogue`, `TrainingBeatmap`, `FinalBeatmap`, `End`.
- Node graph connections: drag wires from output ports to target nodes.
- Dialogue speakers: Twilight Sparkle, Applejack, Rainbow Dash, Rarity, Fluttershy, Pinkie Pie, Apple Bloom, Scootaloo, Sweetie Belle, Derpy.
- Dialogue runtime: textbox is a rectangle whose color changes by speaker; advance with Enter (`MenuSelect`) or Space (`ReactMain`).
- Beatmap reference in nodes: store a `Beatmaps/<Package>/chart.xml` path.
- Training beatmap: success counter is cumulative; failures do not reset previous successes.
- Training failure: explicit `Failure` output port. Example graph: `Training -> Failure dialogue -> Training`.
- Final beatmap: when finished, return to the main menu. Evaluation scene is out of scope.
- Editor layout: topbar, left node palette, central canvas, right inspector, bottom/status area.
- Editor test action: topbar `Play` launches the current level from `Start` and ignores progression locks.

## Data Model

Add level data classes, preferably under a new namespace such as `MLP_RiM.Elements.Levels` or `MLP_RiM.Levels`:

- `LevelDocument`
  - Owns `LevelData`, `FilePath`, `PackagePath`, `IsDirty`.
  - Supports `CreateNewPackage(name)`, `LoadOrCreate(path)`, `Save()`.
  - Generates UUIDs for new levels and nodes.

- `LevelData` XML root, e.g. `[XmlRoot("Level")]`
  - `Id` string UUID.
  - `DisplayName` string.
  - `LockedByDefault` bool.
  - `UnlockLevelIds` list of UUID strings.
  - `StartNodeId` string.
  - `Nodes` list.
  - `Connections` list, or per-node output target fields. Prefer a central `Connections` list for node editor flexibility.

- `LevelNodeData`
  - `Id` string UUID.
  - `Kind` enum/string: `Start`, `Dialogue`, `TrainingBeatmap`, `FinalBeatmap`, `End`.
  - `X`, `Y` canvas coordinates.
  - Dialogue fields: `Speaker`, `Text`.
  - Beatmap fields: `ChartPath`, `RequiredSuccessCount` for training.

- `LevelConnectionData`
  - `FromNodeId`.
  - `FromPort` string: `Next`, `Success`, `Failure`.
  - `ToNodeId`.

- `LevelSpeaker` enum plus a helper for display names and textbox colors.

- `LevelProgressSave`
  - AppData path: same base folder as `EditorSettings.GetDefaultSettingsPath()`, filename `save.json`.
  - `UnlockedLevelIds` list/set.
  - Methods: `Load()`, `Save()`, `IsUnlocked(LevelData)`, `Unlock(levelId)`.
  - Rule: levels with `LockedByDefault == false` are available even if absent from `save.json`.

## Content And Project File Updates

- Add a SpriteFont asset under `Content/Fonts/Dialogue.spritefont` or equivalent.
- Update `Content/Content.mgcb` to build/copy the SpriteFont.
- Update `MLP_RiM.csproj` to copy root `Levels/**/*` to output with `CopyToOutputDirectory=PreserveNewest`.
- Keep root `Levels` separate from `Content`; `Content.mgcb` should only cover content assets such as the SpriteFont.

## Main Menu Changes

- Extend `MainMenu` callbacks:
  - `OpenLevelList` for `Jouer`.
  - `OpenLevelEditor` for `Éditeur de niveau`.
  - Existing `OpenBeatmapEditor` for `Éditeur de beatmap`.
- Add a simple level-list mode in `MainMenu`:
  - Scan `Levels/*/level.xml`.
  - Load metadata only where possible.
  - Sort by display name or folder name.
  - Show locked levels grayed out with `LOCKED`.
  - Enter launches only unlocked levels.
  - Escape returns to the main menu list if an Escape action exists; otherwise Backspace/Escape can be handled directly in `MainMenu` for now.
- When a level finishes successfully, runtime returns to `MainMenu`; progress save is updated before returning.

## Game1 Integration

- Avoid having beatmap editor, level editor, and level runtime active at the same time.
- Add fields or globals for:
  - `LevelEditorElement` current editor overlay.
  - `LevelRuntimeController` or `LevelRuntimeElement` current playable level.
- Add methods in `Game1`:
  - `OpenLevelEditor()`.
  - `OpenLevelList()` if menu list is external, or callback from `MainMenu`.
  - `StartLevel(LevelDocument/LevelData, bool ignoreLocks)`.
  - `ReturnToMainMenu()`.
  - `ClearEditorsAndRuntime()`.
- Draw/update order:
  - Main menu: fullscreen scene, no editor overlay.
  - Level editor: fullscreen UI overlay, no beatmap editor overlay.
  - Level runtime dialogue: draw current scene/background if needed, then dialogue overlay.
  - Level runtime beatmap: use existing `BeatmapPlayer` and rhythm game scenes; draw dialogue overlay only when not in beatmap mode.
- Ensure rhythm input still reaches `BeatmapPlayer.ChartPlayer.React("ReactMain", ...)` during runtime beatmaps.
- During non-beatmap dialogue, Space should advance dialogue, not trigger rhythm reactions.

## Level Editor UI Plan

- New `LevelEditorElement` similar in style to `BeatmapEditorElement`, but smaller and modular.
- Layout:
  - Topbar: `FILE`, `ACTIONS`, status text.
  - Left palette: buttons for `Dialogue`, `Training`, `Final`, `End`; `Start` exists by default and should not be duplicated.
  - Canvas: pan/drag nodes, draw node rectangles, ports, wires, temporary wire while dragging.
  - Inspector: properties for selected node and selected level metadata.
  - Status line: save/load/errors.
- File actions:
  - `New Level` creates `Levels/<Name>/level.xml` with a UUID, `Start` node, and default metadata.
  - `Open Level` can initially be a simple in-editor list/window of discovered `Levels/*/level.xml`.
  - `Save` writes XML.
  - `Reload` reloads current XML.
- Actions:
  - `Play` saves or serializes current state, then launches runtime from `Start` with `ignoreLocks=true`.
- Node interactions:
  - Left-click select node.
  - Drag node body to move.
  - Drag from output port to another node to create/update that output connection.
  - Delete selected node except `Start` with `Delete`/`Backspace`.
  - Draw output ports according to node type:
    - `Start`: `Next`.
    - `Dialogue`: `Next`.
    - `TrainingBeatmap`: `Success`, `Failure`.
    - `FinalBeatmap`: no graph output needed; final completion returns menu.
    - `End`: no output.
- Inspector fields:
  - Level metadata: display name, locked by default, unlock UUID list.
  - Dialogue: speaker dropdown, text input.
  - Training beatmap: chart path selector/list, required success count, success/failure connection display.
  - Final beatmap: chart path selector/list.
- Use existing `DevUiFloatingWindow`/`DevUiRenderer` patterns where practical; avoid copying all of `BeatmapEditorElement` wholesale.

## Runtime Plan

- Add `LevelRuntimeController` or `LevelRuntimeElement` with states:
  - `Idle`.
  - `Dialogue`.
  - `TrainingBeatmap`.
  - `FinalBeatmap`.
  - `Complete`.
  - `FailedGraph`/error state for broken connections.
- Start from `StartNodeId`, follow its `Next` output.
- Dialogue node:
  - Draw textbox using `SpriteFont`.
  - Box color comes from `LevelSpeaker` helper.
  - Show speaker name and text.
  - On Enter or Space, follow `Next`.
- Training beatmap node:
  - Load chart from `ChartPath` using existing `ChartLoader.LoadFromFile` or `BeatmapEditorDocument.LoadOrCreate` if song path/package resolution is needed.
  - Start the beatmap with `GLOBALS.beatmapPlayer.StartBeatmap(...)` and `ReactionRules.RhythmHeavenLike()` / `RhythmHeavenLikeReactionEvaluator`.
  - Track loop completion with `ChartPlayer.IsFinished()`.
  - On loop finish:
    - success if `MissCount == 0`.
    - if success, increment node success counter.
    - if counter >= `RequiredSuccessCount`, follow `Success` output.
    - if failure, follow `Failure` output without resetting the counter.
    - if the graph routes back to the same training node, restart the beatmap and keep the counter value for that node.
- Final beatmap node:
  - Load and start beatmap.
  - When `ChartPlayer.IsFinished()`, mark level complete, unlock `UnlockLevelIds` in `save.json`, and return to main menu.
- End node:
  - Mark level complete, unlock `UnlockLevelIds`, and return to main menu.
- Broken graph handling:
  - Missing target or missing chart should stop runtime, display a clear error overlay/status, and allow returning to menu.

## Beatmap Loading Notes

- Existing beatmap packages store both `chart.xml` and `song.mp3`.
- `Chart.SongPath` may exist but should be normalized carefully. Prefer:
  - If chart has `SongPath`, use it when valid.
  - Else fallback to `BeatmapPackagePaths.GetSongPathForPackage(Path.GetDirectoryName(chartPath))`.
- Use `EditorNoteDefinitions`/existing chart effects to switch rhythm game scenes as beatmaps request them.
- If a beatmap starts without an immediate rhythm-game switch marker, ensure a valid first rhythm game scene is selected, reusing `SwitchFirstAvailableRhythmGameScene()` behavior or chart metadata if available.

## Validation Plan

- Build with temporary output if the normal Debug binary is locked:
  - `dotnet build -p:OutDir="C:\Users\Alexis\AppData\Local\Temp\kilo\MLP_RiM_build\"`
- Add or manually create at least one sample level in `Levels/<Name>/level.xml`:
  - `Start -> Dialogue -> TrainingBeatmap`.
  - Training `Success -> Dialogue -> FinalBeatmap`.
  - Training `Failure -> Dialogue -> TrainingBeatmap`.
  - Final returns to menu and unlocks another dummy level UUID.
- Manual checks:
  - Main menu opens level list from `Jouer`.
  - Locked levels are visible but not launchable.
  - Level editor opens from `Éditeur de niveau`.
  - Nodes can be created, moved, selected, edited, connected via drag wires, saved, loaded.
  - Dialogue advances with Enter and Space.
  - Training loops until cumulative success count is reached.
  - A failed loop follows `Failure` and does not reset previous successful loops.
  - Final beatmap returns to menu and updates AppData `save.json`.
  - Beatmap editor still opens and works from `Éditeur de beatmap`.

## Risks And Constraints

- The existing `Game1` rendering/update flow assumes one global beatmap editor overlay. Implementation must explicitly clear/suspend incompatible modes when switching between menu, level editor, beatmap editor, and runtime.
- `ChartPlayer.IsFinished()` only describes note completion; depending on chart/song duration, a beatmap may be considered finished before audio reaches the end. For this tranche, use `IsFinished()` as the completion trigger.
- The current input action XML has no Escape/back action. It is acceptable to use direct `Keys.Escape` checks for editor/menu back behavior initially, but add a named input action later if this grows.
- The existing text UI font is limited; runtime dialogue must use the new SpriteFont to support French text reliably.
- Workspace may contain pre-existing scene file moves under `Scenes/MiniGames`; implementation should not revert unrelated changes.

## Out Of Scope

- Evaluation scene after final beatmap.
- Rich dialogue portraits or character art.
- Branch conditions beyond training success/failure.
- Runtime save slots or multiple profiles.
- Advanced graph features such as copy/paste, zoom, comments, minimap, reroute nodes, undo/redo, or transition configuration.
- A polished level selection screen beyond a simple list.
