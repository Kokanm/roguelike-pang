# Authored Stages, Puzzle Geometry & Level-Design Skill Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace procedural stage generation with self-discovering authored `StageSpec` stages (supporting crouch tunnels, vertical divider walls, patient floor/wall Clingers, and discrete multi-floor climbs), ship a 42-stage starter library, and build + TDD-test the `.claude/skills/bubblewright-level-design/` agent skill and `CLAUDE.md`.

**Architecture:** First extend `Wick`, `ClingerBehavior`, `StageMap`, `StageView`, `Game`, and weapons for crouch crawlspaces, surface-mounted patient Clingers, and `32 × (14 * Floors)` discrete multi-floor arenas. Next introduce `StageSpec` / `StageBuilder` / `StagePicker` / `StageValidator`, retire `StageGenerator` and `StageLayouts`, and author 42 validated stages (`7` per world). Finally run baseline RED subagent stage-design tests, author the skill + `check_stage.py` + `CLAUDE.md`, and verify GREEN compliance with fresh subagents against `StageValidatorTests`.

**Tech Stack:** Unity `6000.6.7f1` (2D URP), C# 9 (`.NET Standard 2.1`), NUnit EditMode Test Runner (`unity` CLI), Python 3.10+.

**Spec:** `docs/superpowers/specs/2026-11-16-authored-stages-and-skill-design.md`

## Global Constraints

- Unity `6000.6.7f1`, `480 × 270` reference resolution at `15 PPU`; custom kinematic collision (`StageMap`), deterministic `Game.Update` tick order (no `Rigidbody2D`).
- Every stage is a `sealed` `[Preserve]` subclass of `StageSpec` in its own file under `Assets/Bubblewright/Scripts/Stage/Stages/W{1..6}/`, auto-discovered by `StageSpec.All`.
- Saved runs (`SaveData`) store only `seed` and `stage` (`1..36`); `StagePicker` must deterministically pick the exact same `StageSpec` and mirror flag from `(seed, stage, hardWorld)`.
- Unlock schedule: `1-1` has only `Gloop` and solid/ladder geometry (no `B`, no `_`); `1-2+` unlocks `B` and `_`; `1-3+` unlocks `Trigloop` and plain `Pogo`s; W2 unlocks `Clinger`, `Lead`, `Drifter`, `Roller`, `Lucky`; W3 unlocks `Sinker`, `Sand`, `Steel`, `Ceramic` (bubbles `<= M`, big rollers); W4 unlocks `Frost`, `Ice`; W5 unlocks `Ghost`; W6 unlocks `Scorch`, `SpikeStrip`s.
- Every authored stage must pass `StageValidator` (normal and mirrored if `CanMirror`) and be clearable hitless with **Moss** (`7.5 u/s`) and **Flint** (`16 u/s`), default Harpoon, and `0` upgrades.
- Run `python3 .claude/skills/unity-csharp-scripting/scripts/check_unity_project.py .` and `unity test . --mode EditMode` after every C# task.

## Review Focus

1. **Releasing Down or pressing Jump inside a `1`-row (`1.0u`) crouch crawlspace:** Wick (`standing = 1.467u`, `crouching = 0.88u`) must stay crouched and grounded without wedging into the ceiling tile above, and stand up automatically once he shuffles into a `>= 2`-row opening.
2. **Upper-floor weapon ceiling clamps (`y = 14..28`):** Weapons (`Harpoon`, `WeaponWorld`, `Skyrocket`, `RicochetDisc`, `GasFlask`, `Longshot`, `LampLance`) fired on Floor `1` (`y >= 14`) must clamp to `(shooterFloor + 1) * 14f` (`28f`) rather than dying immediately against `Tuning.I.arenaHeight` (`14f`), while shots fired on Floor `0` still stop at `14f` so unseen Floor `1` enemies cannot be sniped blindly.
3. **Dropping between floors vs. falling into a ground gap:** Crossing downward past `y = 14f` through an open shaft or ladder pans `activeFloor` from `1 -> 0` with `0` damage, whereas `FallIntoGap` (`y < -gapFallDepth`) only triggers below `y = 0f`.
4. **Horizontal mirroring of asymmetric placements (`CanMirror = true`):** Mirroring must flip `SpikeStrip.col` to `32 - col - width` (not `31 - col`), swap `ClingerMount.LeftWall <-> RightWall`, and negate `wickStart.x`, `SpawnSpec.position.x`/`direction`, and `RollerSpec.x`/`direction`.
5. **Over-breaking `B` tiles (`all B broken`):** Destroying every `B` tile on a stage must never drop Wick into an inescapable pit or destroy the sole bridge needed to reach/shoot a solid-sealed chamber.

---

### Task 1: Crouch Crawlspaces & Low-Ceiling Firing (`Wick`, `Harpoon`)

**Files:**
- Modify: `Assets/Bubblewright/Scripts/Player/Wick.cs:292-310,422-515,705-715,788-842`
- Test: `Assets/Bubblewright/Tests/Editor/AirMoveTests.cs`

**Interfaces:**
- Consumes: `StageMap.CeilingAbove(float x, float y, out int hitRow) -> float`, `StageMap.OverlapsSolid(Rect r) -> bool`
- Produces:
  - `Wick.ForcedCrouch` (`bool`, true on ground when `HeadCeiling(map, pos.x, pos.y) - pos.y < T.hitbox.y - 1e-4f`)
  - `Wick.BodyBox(float x, float y)` using `HitboxHeight` (`0.88u` when `Crouching`, `1.467u` standing)
  - Crouched `Wick.LauncherTip` with `y = FeetY + Mathf.Min(T.launcherTip.y * T.crouchSquash.y, HitboxHeight - T.Pixel)`

- [ ] **Step 1: Add failing crouch-tunnel tests in `Assets/Bubblewright/Tests/Editor/AirMoveTests.cs`**

Add tests pinning:
- `CrouchingLetsWickShuffleThroughAOneRowTunnelAndLocksCrouchUntilClear`: row `1` has `#` across cols `14..18` (`1.0u` clearance on row `0`). Standing Wick blocked at col `13`; holding Down (`climb: -1f`) enters col `14..16`; releasing Down (`climb: 0f`) inside col `15` keeps `wick.Crouching == true`, ignores `jump: true` (`!wick.Jumping`), and exiting past col `18` restores `wick.Crouching == false`.
- `CrouchedInAOneRowTunnelCanFireUpThroughASlotOrBreakCeilingTile`: inside a `1`-row tunnel with a `B` tile at `(col 16, row 1)` and `.` at `(col 17, row 1)`, `wick.LauncherTip.y < 1f`, firing at col `16` hits `(16, 1)`, and firing at col `17` travels above `y = 2f`.

- [ ] **Step 2: Run `AirMoveTests` to verify they fail**

Run: `unity test /Users/kokanmalenko/Documents/MyProjects/roguelike-pang --mode EditMode --filter "Bubblewright.Tests.AirMoveTests" --output Temp/airmove.xml`
Expected: FAIL (`Wick` blocked horizontally at `1`-row tunnel entrance and `LauncherTip.y > 1f`).

- [ ] **Step 3: Implement crouch-height `BodyBox`, low-ceiling crouch lock, and crouched `LauncherTip` in `Wick.cs`**

1. In `Wick.BodyBox(float x, float y)`, replace `T.hitbox.y` with `HitboxHeight`.
2. Add `bool MustCrouch(StageMap map, float x, float y)` checking `HeadCeiling(map, x, y + HitboxHeight - 0.02f) - y < T.hitbox.y - 1e-4f` (measuring from crouched top so ceiling tiles at row `+1` above feet are detected).
3. Set `Crouching = Mode == Mode.Ground && knockTimer <= 0f && (wantClimb < 0f || MustCrouch(map, pos.x, pos.y));` and skip `StartJump()` when `MustCrouch(map, pos.x, pos.y)` is true.
4. In `Wick.LauncherTip`, compute local tip height when `Crouching` as `Mathf.Min(T.launcherTip.y * T.crouchSquash.y, HitboxHeight - T.Pixel)`.

- [ ] **Step 4: Run `AirMoveTests` and project checker**

Run: `python3 .claude/skills/unity-csharp-scripting/scripts/check_unity_project.py . && unity test . --mode EditMode --filter "Bubblewright.Tests.AirMoveTests" --output Temp/airmove.xml`
Expected: PASS (`failed="0"`).

- [ ] **Step 5: Commit**

```bash
git add Assets/Bubblewright/Scripts/Player/Wick.cs Assets/Bubblewright/Tests/Editor/AirMoveTests.cs
git commit -m "Support 1-row crouch crawlspaces and low-ceiling firing"
```

---

### Task 2: Floor/Wall Clinger Mounts & Patient Mode (`Tuning`, `Bubble`, `ClingerBehavior`)

**Files:**
- Modify: `Assets/Bubblewright/Scripts/Core/Tuning.cs:573-585`
- Modify: `Assets/Bubblewright/Scripts/Bubbles/Bubble.cs:62-97`
- Modify: `Assets/Bubblewright/Scripts/Bubbles/Behaviors/ClingerBehavior.cs:20-165`
- Modify: `Assets/Bubblewright/Scripts/Core/Game.cs:235-245,286-295`
- Test: `Assets/Bubblewright/Tests/Editor/ClingerTests.cs`

**Interfaces:**
- Produces:
  - `public enum ClingerMount { Ceiling = 0, Floor = 1, LeftWall = 2, RightWall = 3 }` in `Tuning.cs`
  - `SpawnSpec.mount` (`ClingerMount`) and `SpawnSpec.patient` (`bool`)
  - `Bubble.Create(..., ClingerMount mount = ClingerMount.Ceiling, bool patient = false)`

- [ ] **Step 1: Add failing tests in `Assets/Bubblewright/Tests/Editor/ClingerTests.cs`**

Add tests pinning:
- `PatientFloorClingerPogoWaitsPastMaxStickTimeThenLeapsWhenWickApproaches`: `Bubble.Create(..., BubbleKind.Clinger, 2, new Vector2(5f, 1f), Vector2.right, child: false, pogo: true, mount: ClingerMount.Floor, patient: true)` seats at `y == b.Radius` with `Normal == Vector2.up`, stays `State.Stuck` after `clingerMaxStickTime + 3f` seconds while Wick is at `x = -10f`, transitions to `Wobbling -> Free` with `Velocity.y > 0f` when `Game.WickFeet = new Vector2(5f - 1.5f, 0f)`, and uses normal `clingerMaxStickTime` on subsequent sticks.
- `WallMountedClingersAttachToLeftAndRightSurfacesAndWaitWhenPatient`: verifies `ClingerMount.LeftWall` (`Normal == Vector2.right`) and `ClingerMount.RightWall` (`Normal == Vector2.left`) seat flush against arena/tile walls and respect `patient: true` + `OnStorm()`.

- [ ] **Step 2: Run `ClingerTests` to verify they fail**

Run: `unity test . --mode EditMode --filter "Bubblewright.Tests.ClingerTests" --output Temp/clinger.xml`
Expected: FAIL (missing `ClingerMount` and `patient` parameters).

- [ ] **Step 3: Implement `ClingerMount` and `patient` in `Tuning.cs`, `Bubble.cs`, `ClingerBehavior.cs`, and `Game.cs`**

1. Add `ClingerMount` enum and `public ClingerMount mount; public bool patient;` on `SpawnSpec` in `Tuning.cs`.
2. Store `Mount` and `Patient` on `Bubble` (set before `behavior.OnSpawn(b, child)`); pass `s.mount` and `s.patient` from `Game.SpawnBubble`.
3. In `ClingerBehavior.OnSpawn`:
   - `Ceiling`: existing behavior (probe `CeilingAbove`).
   - `Floor`: probe `FloorBelow(p.x, p.y + b.Radius)` (or `0f`), seat at `y = floor + SeatDistance(b)` with `Stick(b, Vector2.up, tile, col, row)`.
   - `LeftWall` / `RightWall`: scan horizontally from `p.x` to nearest solid tile face or `±HalfWidth`, seat at `wallX ± b.Radius` with `normal = (±1, 0)`.
   - Initialize private `bool waitForever = b.Patient;` and clear `waitForever = false;` inside `LetGo(b)`. In `State.Stuck`, check `WickInLane(b) || (!waitForever && stuckTime >= T.clingerMaxStickTime)`.

- [ ] **Step 4: Run `ClingerTests` and project checker**

Run: `python3 .claude/skills/unity-csharp-scripting/scripts/check_unity_project.py . && unity test . --mode EditMode --filter "Bubblewright.Tests.ClingerTests" --output Temp/clinger.xml`
Expected: PASS (`failed="0"`).

- [ ] **Step 5: Commit**

```bash
git add Assets/Bubblewright/Scripts/Core/Tuning.cs Assets/Bubblewright/Scripts/Bubbles/Bubble.cs Assets/Bubblewright/Scripts/Bubbles/Behaviors/ClingerBehavior.cs Assets/Bubblewright/Scripts/Core/Game.cs Assets/Bubblewright/Tests/Editor/ClingerTests.cs
git commit -m "Add floor/wall Clinger mounts and patient puzzle mode"
```

---

### Task 3: Multi-Floor `StageMap`, Camera Pan, Floor Wake-Up & Weapon Bounds

**Files:**
- Modify: `Assets/Bubblewright/Scripts/Stage/StageMap.cs`
- Modify: `Assets/Bubblewright/Scripts/Stage/StageLayout.cs`
- Modify: `Assets/Bubblewright/Scripts/Stage/StageView.cs`
- Modify: `Assets/Bubblewright/Scripts/Core/Tuning.cs:19-24`
- Modify: `Assets/Bubblewright/Scripts/Core/Game.cs`
- Modify: `Assets/Bubblewright/Scripts/Core/Game.Stage.cs`
- Modify: `Assets/Bubblewright/Scripts/Core/Game.Waves.cs`
- Modify: `Assets/Bubblewright/Scripts/Core/Game.Rollers.cs`
- Modify: `Assets/Bubblewright/Scripts/UI/TouchControlsView.cs`
- Modify: `Assets/Bubblewright/Scripts/Bubbles/Bubble.cs`, `PogoShape.cs`, `Roller.cs`, `Behaviors/GhostBehavior.cs`, `Behaviors/ClingerBehavior.cs`
- Modify: `Assets/Bubblewright/Scripts/Stage/TempIce.cs`, `Flames.cs`, `WorldRules.cs`, `Scripts/Core/StageReport.cs`, `Scripts/Core/Game.Breakables.cs`
- Modify: `Assets/Bubblewright/Scripts/Weapons/Harpoon.cs`, `WeaponWorld.cs`, `Skyrocket.cs`, `RicochetDisc.cs`, `PopMine.cs`, `GasFlask.cs`, `Longshot.cs`, `TrimmerBlade.cs`, `LampLance.cs`
- Create: `Assets/Bubblewright/Tests/Editor/MultiFloorTests.cs`
- Modify: `Assets/Bubblewright/Tests/Editor/StageMapTests.cs`

**Interfaces:**
- Produces:
  - `StageMap.FloorRows = 14`, `map.Floors` (`int >= 1`), `map.Rows` (`14 * Floors`), `map.Height` (`float`), `StageMap.Flat(int floors = 1)`, `StageMap.FloorOf(float y) -> int` (`Mathf.Clamp(Mathf.FloorToInt(y / 14f), 0, Floors - 1)`)
  - `StageLayout.Parse(string[] rows, string ground = null, Surface defaultSurface = Surface.Normal) -> StageMap` (accepting `rows.Length == 14 * floors` plus legacy `15`-row overload, with tile chars `. # N I S B H T` and ground chars `= N I S _`)
  - `Tuning.floorPanSpeed = 56f`
  - `StagePlan.wickPosition` (`Vector2`, replacing `wickX` while keeping `wickX` property alias until Task 4), `StagePlan.waveFloor` (`int`)

- [ ] **Step 1: Write failing multi-floor tests in `Assets/Bubblewright/Tests/Editor/MultiFloorTests.cs`**

Add tests pinning:
- `ParseBuildsTwoFloorMapWithInlineSurfacesAndLaddersAcrossBoundary`: `28` rows + ground row with `I`, `S`, `N`, `H`, `T` across rows `12..15` produces `map.Floors == 2`, `map.Rows == 28`, `map.Height == 28f`, and Wick climbs smoothly from `y = 10f` to `y = 15f`.
- `HarpoonAndWeaponShotsClampToCurrentFloorCeilingAndStopAtFloorBoundary`: upward `Harpoon` and `WeaponWorld` shot fired from `y = 2f` on a 2-floor map with an open shaft at col `16` stop at `y = 14f` (cannot hit a bubble at `y = 16f`), while shots fired from `y = 16f` reach up to `y = 28f`; firing upward from `y = 12f` into a `B` tile at `(col, row 13)` still damages `(col, 13)`.
- `GhostStaysWithinItsHomeFloorBounds`: a `Ghost` spawned at `y = 18f` on a 2-floor map bounces between `y = 14f` and `y = 28f`, never crossing below `14f`.
- `StageViewResizesTextureForTwoFloorMap`: `StageView` builds a `480 × 420` texture for a 2-floor map and `480 × 210` when switched back to a 1-floor map.
- `ActiveFloorUsesDownwardHysteresisAndWakesUpperFloorOnArrival`: helper `Game.ComputeActiveFloor(currentFloor, feetY, floors)` advances `0 -> 1` at `feetY == 14.0f`, stays `1` at `feetY == 13.9f`, and drops `1 -> 0` at `feetY == 13.8f`.

- [ ] **Step 2: Run `MultiFloorTests` to verify they fail**

Run: `unity test . --mode EditMode --filter "Bubblewright.Tests.MultiFloorTests" --output Temp/multifloor.xml`
Expected: FAIL (compile errors on `StageMap.Floors`, `StageLayout.Parse` overload, `ComputeActiveFloor`).

- [ ] **Step 3: Implement multi-floor `StageMap`, `StageLayout`, `StageView`, floor bounds, camera pan, and dormant floors**

1. **`StageMap` & `StageLayout`:** Change `StageMap.Rows` from `const` to `public const int FloorRows = 14; public int Floors { get; } public int Rows => FloorRows * Floors; public float Height => Rows;` with `new StageMap(int floors = 1)` and `StageMap.Flat(int floors = 1)`. Update all `StageMap.Rows` call sites to `map.Rows` (or `StageMap.FloorRows` where referring to one screen). Extend `StageLayout.Parse` to support `14 * floors` rows, optional `ground` parameter (keeping legacy `15`-element array support), `defaultSurface`, tile chars `N`/`I`/`S`, and ground chars `N`/`I`/`S`.
2. **Floor ceiling/floor bounds on entities & weapons:**
   - Add `StageMap.FloorCeiling(float fromY)` (`(FloorOf(fromY) + 1) * FloorRows`) and `StageMap.FloorBase(float fromY)` (`FloorOf(fromY) * FloorRows`).
   - In `Harpoon` (`Fire`, `Tick`, `DownStop`, `AngledStop`) and `WeaponWorld` / `Skyrocket` / `RicochetDisc` / `PopMine` / `GasFlask` / `Longshot` / `LampLance`: clamp upward travel to `Mathf.Min(tileCeil, map.FloorCeiling(originY))` (still allowing `hitRow == (FloorOf(originY) + 1) * 14 - 1` `B` tiles on row `13` to take damage) and downward travel to `Mathf.Max(tileFloor, map.FloorBase(originY))`.
   - In `GhostBehavior`: record `int homeFloor` on spawn (`map.FloorOf(b.Position.y)`) and resolve bounds against `[homeFloor * 14f, (homeFloor + 1) * 14f]`.
3. **`StageView`:** Rebuild `pixels`, `pixelCanvas`, `texture`, and `sprite` when `map.Rows != currentRows`.
4. **Camera pan, UI strip & dormant floors (`Game`, `TouchControlsView`):**
   - Add `public float floorPanSpeed = 56f;` to `Tuning`.
   - Add `public static int ComputeActiveFloor(int current, float feetY, int floors)` (advances when `feetY >= (f + 1) * 14f`, drops when `feetY < f * 14f - 0.15f`).
   - Track `int activeFloor`, `float camPanY`, and `bool[] awakeFloors` in `Game`. Snap both on `StartStage` to `Map.FloorOf(plan.wickPosition.y)`. Pan `camPanY` toward `activeFloor * 14f` at `T.floorPanSpeed * dt`. Offset `cam.transform.position.y`, `background.transform.position.y`, `TouchControlsView` (`camPanY` parameter), and a `FloorStripMask` sprite (`y = camPanY - 4f .. camPanY`, enabled when `camPanY > 0.01f`) by `camPanY`.
   - Tick a bubble or roller only when `awakeFloors[Map.FloorOf(pos.y)]` is `true`; wake `awakeFloors[activeFloor] = true` on `TickPlay` and wake `awakeFloors[Map.FloorOf(childPos.y)] = true` whenever an awake entity enters another floor. Show `FLOOR {activeFloor + 1}/{Map.Floors}` in `StageLabel()` when `Map.Floors > 1`.

- [ ] **Step 4: Run `MultiFloorTests`, `StageMapTests`, and the full EditMode suite**

Run: `python3 .claude/skills/unity-csharp-scripting/scripts/check_unity_project.py . && unity test . --mode EditMode --output Temp/editmode.xml`
Expected: PASS (`failed="0"` across all existing and new tests).

- [ ] **Step 5: Commit**

```bash
git add Assets/Bubblewright/
git commit -m "Add discrete multi-floor StageMap, camera pan, and floor wake-up"
```

---

### Task 4: `StageSpec` Framework, `StagePicker`, `StageValidator` & Procedural Cutover

**Files:**
- Create: `Assets/Bubblewright/Scripts/Stage/StageSpec.cs`
- Create: `Assets/Bubblewright/Scripts/Stage/StagePicker.cs`
- Create: `Assets/Bubblewright/Scripts/Stage/StageValidator.cs`
- Create: `Assets/Bubblewright/Scripts/Stage/Stages/W1/W1_FirstPop.cs` *(1 minimal bootstrap stage per world `W1..W6` so `StagePicker` tests run immediately; expanded to all 42 in Tasks 5–6)*
- Create: `Assets/Bubblewright/Tests/Editor/StageValidatorTests.cs`
- Delete: `Assets/Bubblewright/Scripts/Stage/StageLayouts.cs` (+ `.meta`), `StageRecipe.cs` (+ `.meta`), `PlacementPatterns.cs` (+ `.meta`)
- Delete: `Assets/Bubblewright/Tests/Editor/StageLayoutTests.cs` (+ `.meta`), `RecipeTests.cs` (+ `.meta`), `SignatureTests.cs` (+ `.meta`), `GuestTests.cs` (+ `.meta`), `PlacementPatternTests.cs` (+ `.meta`)
- Modify: `Assets/Bubblewright/Scripts/Stage/WorldRules.cs`, `Scripts/Core/Run.cs`, `Scripts/Core/Tuning.cs`, `Scripts/Core/Game.cs`, `Scripts/Core/Game.Stage.cs`, `Scripts/Core/Game.Menus.cs`, `Scripts/Core/Game.Balance.cs`, `Scripts/Core/StageReport.cs`
- Modify: `Assets/Bubblewright/Tests/Editor/RunStructureTests.cs`, `BalanceTests.cs`, `SaveTests.cs`, `WaveTests.cs`, `RollerTests.cs`, `PogoTests.cs`, `SteelCeramicTests.cs`, `SpikeTests.cs`, `BossTests.cs`, `SurfaceTests.cs`

**Interfaces:**
- Produces:
  - `public abstract class StageSpec` (`Id`, `Name`, `World`, `Difficulty`, `MinStageInWorld => 1`, `MaxStageInWorld => 5`, `CanMirror => true`, `TimeScale => 1f`, `Concept`, `Rows`, `Ground`, `Populate(StageBuilder b)`, `Build(bool mirror = false) -> StagePlan`, `public static IReadOnlyList<StageSpec> All`, `ById(string id)`)
  - `public sealed class StageBuilder` with constants `TierXL = 0, TierL = 1, TierM = 2, TierS = 3` and helpers:
    - `Wick(float x, float y = 0f)` / `WickAtCol(int col, int row = 0)`
    - `Bubble(BubbleKind kind, int tier, float x, float y, int dir = 1, bool ceramic = false, bool pogo = false, bool steel = false)` / `BubbleAt(BubbleKind kind, int tier, int col, float y, int dir = 1, bool ceramic = false, bool pogo = false)`
    - `Clinger(int tier, float x, float y, ClingerMount mount = ClingerMount.Ceiling, int dir = 1, bool pogo = false, bool ceramic = false, bool patient = true)` / `FloorClinger(int tier, float x, float surfaceY = 0f, int dir = 1, bool pogo = true, bool ceramic = false, bool patient = true)`
    - `SteelPogo(int tier, float x, float surfaceY = 0f)`
    - `Roller(float x, float surfaceY = 0f, int dir = 1, int tier = 0, bool ceramic = false, bool steel = false)`
    - `Spike(int col, int width, int row = -1, float? phase = null)`
    - `Wave(int waveIndex, BubbleKind kind, int tier, float x, int dir = 1, bool ceramic = false, bool pogo = false)` + `WaveFloor(int floor)`
  - `public static class StagePicker`:
    - `PickForWorld(int seed, int world, bool hard) -> StageSpec[]` (length `5`, no duplicate `Id`s when pool `>= 5`, Calm targets `[1,2,2,3,3]`, Hard targets `[2,3,3,4,4]`, slots `2..5` ordered by non-decreasing `Difficulty` respecting `MinStageInWorld`/`MaxStageInWorld`)
    - `Build(Run run, int stage, int debugOverrideIndex = -1) -> StagePlan`
    - `StageTime(int stage, bool hard = false) -> float`, `BossTime(bool hard = false) -> float`, `BubbleCost(BubbleKind kind, int tier) -> float`
  - `public static class StageValidator`:
    - `Validate(StageSpec spec, bool mirror) -> List<string>` (returns empty list when valid; checks metadata, dimensions, unlock matrix in Spec §1.1, Wick footing + `72`-tick `1.2s` headless simulation for zero early hits, entity/roller/spike clearances, ladder completeness, sand `<= 2` / normal `<= 3` jumpable ground gaps, iterative `B`-breach reachability for Moss `7.5 u/s` with `0` upgrades, and `all B broken` trap/soft-lock check).

- [ ] **Step 1: Write `StageValidatorTests.cs` covering `StageSpec` mirroring, `StagePicker` curves, and `StageValidator` positive + negative cases**

Include tests pinning:
- `MirrorFlipsGridGroundSpawnsWallClingersAndSpikesExact`: verifies `Spike(col: 4, width: 3)` mirrors to `col == 25, width == 3`, `LeftWall` mirrors to `RightWall`, and `wickPosition.x` negates.
- `ValidatorCatchesUnlockViolationsEarlyHitsTrapPitsAndOverBreakSoftLocks`: synthetic invalid `StageSpec`s fail with descriptive messages (`B` in `1-1`, bubble hitting Wick within `1.2s`, `3`-col gap on Sand, `B` bridge that strands a solid-roofed upper chamber when broken).
- `EveryDiscoveredStageAndMirrorPassesValidation`: `[TestCaseSource]` over `StageSpec.All` (+ mirror when `CanMirror`) asserts `StageValidator.Validate(spec, mirror)` is empty.
- `StagePickerNeverRepeatsInAWorldAndMatchesCalmAndHardCurves`: across 100 seeds, Calm slots `1..5` never decrease in difficulty on `2..5` and never repeat an `Id` when pool `>= 5`.

- [ ] **Step 2: Implement `StageSpec.cs`, `StagePicker.cs`, `StageValidator.cs`, retire `StageGenerator` / `StageLayouts`, and update callers & tests**

1. Create `StageSpec.cs`, `StagePicker.cs`, and `StageValidator.cs` matching the interfaces above, plus 1 clean bootstrap stage in each `Stages/W{1..6}/` folder so every world has an initial valid stage while wiring cutover.
2. Delete `StageLayouts.cs`, `StageRecipe.cs`, `PlacementPatterns.cs`, and their 5 procedural test files (with `.meta` files). Remove `StageGenerator` from `Run.cs` and unused recipe/guest fields from `Tuning.cs`. Keep `WorldRules.PaintAll` and `WorldRules.PlaceSpikes` for the debug panel buttons.
3. Update `Game.cs`, `Game.Stage.cs`, `Game.Menus.cs` (`F3` / `TEST STAGE` cycles `StageSpec.All`), `Game.Balance.cs` (`NEXT STAGE` cycles current world's stages; shows `Id`, `Difficulty`, `Concept`, `PopsNeeded`, `TimeLimit`), `StageReport.cs`, and remaining EditMode tests (`RunStructureTests`, `BalanceTests`, `SaveTests`, `WaveTests`, `RollerTests`, `PogoTests`, `SteelCeramicTests`, `SpikeTests`, `BossTests`, `SurfaceTests`) to call `StagePicker`.

- [ ] **Step 3: Run project checker and full EditMode test suite**

Run: `python3 .claude/skills/unity-csharp-scripting/scripts/check_unity_project.py . && unity test . --mode EditMode --output Temp/editmode.xml`
Expected: `check_unity_project.py` exits `0`; all EditMode tests pass (`failed="0"`).

- [ ] **Step 4: Commit**

```bash
git add -A Assets/Bubblewright/
git commit -m "Replace procedural StageGenerator with authored StageSpec, StagePicker, and StageValidator"
```

---

### Task 5: Authored Starter Library — Worlds 1 to 3 (`21` Stages)

**Files:**
- Create/Update `7` files in `Assets/Bubblewright/Scripts/Stage/Stages/W1/` (`1× D1`, `3× D2`, `3× D3`)
- Create/Update `7` files in `Assets/Bubblewright/Scripts/Stage/Stages/W2/` (`1× D1`, `2× D2`, `2× D3`, `2× D4`)
- Create/Update `7` files in `Assets/Bubblewright/Scripts/Stage/Stages/W3/` (`1× D1`, `2× D2`, `2× D3`, `2× D4`)
- Modify: `Assets/Bubblewright/Tests/Editor/StageValidatorTests.cs`

**Interfaces:**
- Consumes: `StageSpec`, `StageBuilder`, `StageValidator` (Task 4)
- Produces: 21 validated `StageSpec` classes across Worlds 1–3 (`Rooftops`, `Forest`, `Desert`)

- [ ] **Step 1: Add pool-count assertions for Worlds 1–3 in `StageValidatorTests.cs`**

Assert `W1` has `{D1: 1, D2: 3, D3: 3}` (`7` total, including `w1_first_pop` with `MaxStageInWorld == 1` and `w1_slotted_bunker`), and `W2` and `W3` each have `{D1: 1, D2: 2, D3: 2, D4: 2}` (`7` total each, including `w2_clinger_pogo_gauntlet`).

- [ ] **Step 2: Author the 21 `StageSpec` classes across `W1/`, `W2/`, and `W3/`**

Design distinct, symmetrical/structured combat-puzzles (no random shelf scatter):
- **World 1 (Rooftops — `Gloop`, `Trigloop` from `1-3`, `B` & `_` from `1-2`):**
  1. `W1_FirstPop` (`D1`, `MaxStageInWorld = 1`): Twin symmetric side steps + central open arena, two `L` Gloops splitting cleanly.
  2. `W1_SlottedBunker` (`D2`, `MinStageInWorld = 2`): Kokan's bunker puzzle — Wick starts inside a roofed bunker with three `1`-tile (`1.0u`) slots; `XL`/`L` Gloops bounce across the roof until split to `S`, which can drop inside; `B` side seals let Wick break out if desired.
  3. `W1_Crawlway` (`D2`, `MinStageInWorld = 2`): `1`-row (`1.0u`) central crouch tunnel with overhead firing slots connecting left and right courts divided by a `#` wall + ladder.
  4. `W1_TwinChambers` (`D2`, `MinStageInWorld = 2`): Full-height vertical `#` divider wall down cols `15..16` capped by a ladder bridge; Wick clears one chamber, climbs over, and clears the twin.
  5. `W1_PogoSteps` (`D3`, `MinStageInWorld = 3`): Symmetric stepped terraces guarded by plain `M` Gloop Pogos over solid landings and `2`-col ground gaps.
  6. `W1_TrigloopSieve` (`D3`, `MinStageInWorld = 3`): Upper deck with `2`-tile (`2.0u`) filter gaps that hold `XL`/`L` Trigloops above while letting `M` and `S` children rain into the lower court.
  7. `W1_BellTower` (`D3`, `MinStageInWorld = 3`, `Floors = 2`): 2-floor Rooftops climb — clear lower belfry, climb central twin ladders to the upper bell-chamber.
- **World 2 (Forest — `Clinger`, `Roller`, `Lead`, `Drifter`):**
  1. `W2_CanopyAmbush` (`D1`, `MaxStageInWorld = 1`): Clean `Clinger` intro — symmetric ceiling and under-bough patient Clingers over a wide safe floor.
  2. `W2_ClingerPogoGauntlet` (`D2`, `MinStageInWorld = 2`): Kokan's floor-Pogo puzzle — row of patient floor `M`/`S` Clinger Pogos spaced across long runs; Wick jumps over them and fires `Down + Fire` in mid-air (or triggers their upward leap and shoots from beside them).
  3. `W2_RollingTrack` (`D2`, `MinStageInWorld = 2`): Central elevated loop where two pearl `Roller`s orbit the platform top and underside while `Lead` bubbles bounce low.
  4. `W2_HollowTrunk` (`D3`, `MinStageInWorld = 2`): Vertical `#` trunk walls with patient `LeftWall`/`RightWall` Clingers guarding ladder shafts and a `B`-sealed root vault.
  5. `W2_DrifterPrism` (`D3`, `MinStageInWorld = 2`): Angled `#` pillar diamond deflecting `Drifter` diagonals around a sheltered crouch pillbox.
  6. `W2_TreetopAscent` (`D4`, `MinStageInWorld = 3`, `Floors = 2`): 2-floor Forest canopy climb with rollers on Floor `0` and ceiling Clingers + reinforcement wave on Floor `1`.
  7. `W2_BrambleFortress` (`D4`, `MinStageInWorld = 4`): Hard-road fortress combining a slotted bunker, orbiting platform roller, and patient floor Clinger Pogos on the flanks.
- **World 3 (Desert — `Sinker` + `Sand` + `Steel` + `Ceramic`, max `2`-col sand gaps):**
  1. `W3_SinkingDunes` (`D1`, `MaxStageInWorld = 1`): Clean `Sinker` on sand intro — twin `L` Sinkers quickly losing bounce into ground rollers between stepped dunes.
  2. `W3_KilnVault` (`D2`, `MinStageInWorld = 2`): `B`-brick sealed upper kiln holding `Ceramic` Gloops; Wick chips open whichever firing slot he wants.
  3. `W3_PendulumPit` (`D2`, `MinStageInWorld = 2`): Indestructible `Steel` roller patrolling a central sunken sand basin while Wick works from twin side balconies (`2`-col sand gaps).
  4. `W3_Hourglass` (`D3`, `MinStageInWorld = 2`): Symmetric hourglass funnel — `Sinker`s start on high shelves, lose height, and roll inward toward central `B` plugs.
  5. `W3_SteelColumns` (`D3`, `MinStageInWorld = 3`): Two `Steel` Pogos acting as rhythmic vertical gates flanking a central crouch bunker with `Ceramic` Sinkers outside.
  6. `W3_MirageZiggurat` (`D4`, `MinStageInWorld = 3`, `Floors = 2`): 2-floor stepped pyramid climb with sand gaps (`<= 2` cols), ceramic rollers on Floor `0`, and `Sinker` + `Clinger` guests on Floor `1`.
  7. `W3_SandstormBunker` (`D4`, `MinStageInWorld = 4`): Slotted sandstone bunker with `Ceramic` Trigloops overhead, `Steel` roller sweeping the roof, and `1` reinforcement wave.

- [ ] **Step 3: Run `check_unity_project.py` and `StageValidatorTests`**

Run: `python3 .claude/skills/unity-csharp-scripting/scripts/check_unity_project.py . && unity test . --mode EditMode --filter "Bubblewright.Tests.StageValidatorTests" --output Temp/validator-w1-3.xml`
Expected: PASS (`failed="0"` for all 21 stages and their mirrors).

- [ ] **Step 4: Commit**

```bash
git add Assets/Bubblewright/Scripts/Stage/Stages/W1/ Assets/Bubblewright/Scripts/Stage/Stages/W2/ Assets/Bubblewright/Scripts/Stage/Stages/W3/ Assets/Bubblewright/Tests/Editor/StageValidatorTests.cs
git commit -m "Add 21 authored puzzle stages for Worlds 1 to 3"
```

---

### Task 6: Authored Starter Library — Worlds 4 to 6 (`21` Stages)

**Files:**
- Create/Update `7` files in `Assets/Bubblewright/Scripts/Stage/Stages/W4/` (`1× D1`, `2× D2`, `2× D3`, `2× D4`)
- Create/Update `7` files in `Assets/Bubblewright/Scripts/Stage/Stages/W5/` (`1× D1`, `2× D2`, `2× D3`, `2× D4`)
- Create/Update `7` files in `Assets/Bubblewright/Scripts/Stage/Stages/W6/` (`1× D1`, `2× D2`, `2× D3`, `2× D4`)
- Modify: `Assets/Bubblewright/Tests/Editor/StageValidatorTests.cs`, `README.md`

**Interfaces:**
- Consumes: `StageSpec`, `StageBuilder`, `StageValidator` (Task 4)
- Produces: Complete 42-stage library (`7` per world across Worlds `1..6`)

- [ ] **Step 1: Expand pool-count assertions in `StageValidatorTests.cs` to all 6 worlds (`42` stages total)**

Assert `StageSpec.All.Count == 42`, `W1` has `{1, 3, 3, 0}`, `W2..W6` each have `{1, 2, 2, 2}`, and `StageReport.Summary(20)` + `StageReport.LogRun(new Run(42))` run cleanly across all 36 run slots on both Calm and Hard roads.

- [ ] **Step 2: Author the 21 `StageSpec` classes across `W4/`, `W5/`, and `W6/`**

- **World 4 (Winter — `Frost` + authored `Ice` stretches, normal grip at Wick's start):**
  1. `W4_FirstFreeze` (`D1`, `MaxStageInWorld = 1`): `Frost` intro on alternating `N`/`I` ground bands with safe normal central footing.
  2. `W4_IcePillbox` (`D2`): Normal-grip central slotted bunker flanked by wide `I` ice skating lanes where `Frost` bubbles bounce.
  3. `W4_GlacierWall` (`D2`): Vertical `#` ice-capped divider wall with twin ladders and `Ceramic` Frost bubbles in symmetric chambers.
  4. `W4_FrozenSlide` (`D3`): Crouch-height `1`-row ice slide under a low shelf leading into flanking harpoon chimneys, with floor `Clinger` Pogo guests.
  5. `W4_RinkAndRollers` (`D3`): Upper `I` deck with orbiting pearl rollers and `Steel` Pogo gatekeeper below.
  6. `W4_IceSpire` (`D4`, `Floors = 2`): 2-floor glacier tower climb with `B` ice seals between floors and `Frost` + `Drifter` guest on Floor `1`.
  7. `W4_WhiteoutCitadel` (`D4`, `MinStageInWorld = 4`): Multi-chamber ice citadel with slotted ceilings, `Ceramic` Frost XL, and a late wave.
- **World 5 (Catacombs — `Ghost` phasing through tiles + `B` crypt masonry):**
  1. `W5_HauntedNave` (`D1`, `MaxStageInWorld = 1`): `Ghost` intro — raised shelves Wick stands under/on while `Ghost`s phase through platforms and flicker on a predictable beat.
  2. `W5_SealedOssuary` (`D2`): `B`-walled central reliquary where `Gloop`/`Ceramic` bubbles stay trapped inside while `Ghost`s pass freely through its walls toward Wick.
  3. `W5_CryptCrawl` (`D2`): Symmetric `1`-row crouch catacomb tunnels with `B` ceiling plugs under twin `Ghost` + `Trigloop` vaults.
  4. `W5_DividedTombs` (`D3`): Three vertical `#` tomb chambers linked by upper ladder galleries and `B` side doors.
  5. `W5_PhantomBunker` (`D3`): Slotted `#` bunker that blocks `XL` Trigloops on the roof while `L` `Ghost`s ignore the roof and force Wick to time shots to their solid phase.
  6. `W5_DeepCatacomb` (`D4`, `Floors = 2`): 2-floor crypt descent/ascent (`Ghost`s bounded per floor) with `Ceramic` rollers on Floor `0` and sealed `B` sarcophagus on Floor `1`.
  7. `W5_BoneSanctum` (`D4`, `MinStageInWorld = 4`): Hard-road sanctum with `Steel` roller, `Ghost` Pogos, `Sinker` guest, and `2` waves.
- **World 6 (Lava — `Scorch` flames + phased `SpikeStrip`s):**
  1. `W6_EmberCourt` (`D1`, `MaxStageInWorld = 1`): `Scorch` + `2` phased ground `SpikeStrip`s intro with wide safe central stonework.
  2. `W6_SlagBunker` (`D2`): Elevated slotted bunker keeping Wick above `Scorch` floor flames, with phased `SpikeStrip`s guarding the outer wings.
  3. `W6_FurnaceGates` (`D2`): Vertical `#` furnace walls dividing three chambers, each with alternating-phase `SpikeStrip`s and patient `Clinger` Pogo guests.
  4. `W6_CinderCrawl` (`D3`): `1`-row crouch duct beneath a `Scorch`-patrolled grate (`1`-tile slots let Wick snipe upward while `Scorch` flames burn harmlessly on the grate above).
  5. `W6_AnvilBridge` (`D3`): Long central bridge with staggered `SpikeStrip`s, `Steel` roller below, and `Ceramic` Scorch bubbles above.
  6. `W6_CalderaClimb` (`D4`, `Floors = 2`): 2-floor volcano shaft climb with Floor `0` lava gaps + spikes and Floor `1` `Scorch` + `Ghost` guest arena.
  7. `W6_Crucible` (`D4`, `MinStageInWorld = 4`): Finale pre-boss gauntlet combining a slotted bunker, `4` phased `SpikeStrip`s, `Ceramic` Scorch + Trigloop, and `2` reinforcement waves.

Update `README.md`'s stage/debug description briefly to reflect authored `StageSpec` stages and multi-floor/crouch geometry.

- [ ] **Step 3: Run `check_unity_project.py` and the full EditMode test suite**

Run: `python3 .claude/skills/unity-csharp-scripting/scripts/check_unity_project.py . && unity test . --mode EditMode --output Temp/editmode-all.xml`
Expected: `check_unity_project.py` exits `0`; all EditMode tests pass (`failed="0"`).

- [ ] **Step 4: Commit**

```bash
git add Assets/Bubblewright/ README.md
git commit -m "Complete 42-stage authored library across Worlds 4 to 6"
```

---

### Task 7: Author & TDD-Verify `.claude/skills/bubblewright-level-design/` and `CLAUDE.md`

**Files:**
- Create: `.claude/skills/bubblewright-level-design/SKILL.md`
- Create: `.claude/skills/bubblewright-level-design/references/mechanics-and-archetypes.md`
- Create: `.claude/skills/bubblewright-level-design/scripts/check_stage.py`
- Create: `CLAUDE.md`

**Interfaces:**
- Consumes: `StageSpec`, `StageBuilder`, `StageValidator`, `StageValidatorTests` (Tasks 4–6)
- Produces:
  - `python3 .claude/skills/bubblewright-level-design/scripts/check_stage.py <path-or-dir>` (exits `0` on valid `StageSpec` file(s), non-zero with exact line/column/rule diagnostics on `!= 32`-char rows, non-`14*k` row count, incomplete ladders, unlock-schedule violations, sand gap `> 2` / normal gap `> 3`, or missing `Concept`/`[Preserve]`)
  - `.claude/skills/bubblewright-level-design/SKILL.md` (`< 350` lines, frontmatter `name: bubblewright-level-design`, `description: Use when...`)
  - `.claude/skills/bubblewright-level-design/references/mechanics-and-archetypes.md` (exact unit/bounce/jump/harpoon tables, world unlock matrix, and 8 worked puzzle archetypes)
  - `CLAUDE.md` (concise project overview, architecture invariants, test commands, and mandatory pointer to `.claude/skills/bubblewright-level-design/SKILL.md` whenever creating, editing, or reviewing stages)

- [ ] **Step 1: RED Phase — Run 2 baseline subagent stage-design scenarios WITHOUT the skill**

Dispatch 2 fresh `generalPurpose` subagents (in a scratch path or temporary stage files reverted right after recording) with realistic user prompts and **no** skill:
1. *Scenario A (Puzzle + Clinger mechanics):* "Design a Difficulty 3 stage for World 2 where Wick has to deal with floor Clinger Pogos and a central bunker, and add it to the game."
2. *Scenario B (Multi-floor + Surface/Breakable rules):* "Design a 2-floor Difficulty 4 stage for World 3 (Desert) with sand gaps, a crouch passage, and a sealed breakable chamber."

Record verbatim in test notes (`Temp/skill-red-baseline.md`): row-length/alignment mistakes, misunderstood `Clinger` mount/patient flags, illegal `3`-col sand gaps, over-break soft-locks, random/asymmetric bubble placement, or skipped validation commands. Clean up the scratch stage files before Step 2.

- [ ] **Step 2: GREEN Phase — Write `scripts/check_stage.py`, `references/mechanics-and-archetypes.md`, `SKILL.md`, and `CLAUDE.md`**

1. **`scripts/check_stage.py`:** Standalone Python 3 script (stdlib only) that parses one or more `StageSpec` `.cs` files, prints an annotated `32`-col ruler view on error, and checks `32`-char row widths, `14 * Floors` row count, valid tile/ground characters, ladder completeness, gap widths (`<= 2` on sand, `<= 3` normal/ice), world unlock rules, `[Preserve]`, and non-empty `Concept`. Verify it exits `0` on `Assets/Bubblewright/Scripts/Stage/Stages/` and non-zero on synthetic broken inputs.
2. **`references/mechanics-and-archetypes.md`:** Complete reference tables (bubble diameters & bounce peaks per kind, Wick standing/crouch/jump/gap metrics for Moss & Flint, narrow-gap filter widths `1u`/`2u`/`3u`, world unlock table, pop-count & `pops/s` targets for `D1..D4`, and 8 puzzle archetypes: *Slotted Bunker*, *Floor Clinger Pogo Gauntlet*, *Crouch Duct Sniping*, *Divided Twin Chambers*, *Breakable Kiln/Vault*, *Sieve Ceiling*, *Roller Orbit Loop*, *2-Floor Tower Climb*).
3. **`SKILL.md`:** Positive concept-first recipe addressing every RED baseline failure:
   - Required **Stage Design Brief** before writing code (`World` + `Difficulty` + `MinStageInWorld`, 1-sentence `Concept`, symmetry/visual plan, Moss + default Harpoon hitless route, over-break check).
   - `32`-column ruler template (`// 01234567890123456789012345678901`) and `StageSpec` + `StageBuilder` recipe.
   - Inline quick-reference numbers + hard fairness rules (no `Updraft`-only jumps, `<= 2`-col sand gaps, `patient: true` for puzzle Clingers, no over-break traps).
   - Mandatory validation loop (`python3 .claude/skills/bubblewright-level-design/scripts/check_stage.py <file>`, `check_unity_project.py`, `unity test . --mode EditMode --filter StageValidatorTests`).
4. **`CLAUDE.md`:** Concise repo guide (code layout, `Tuning` rules, no physics engine, EditMode test commands, and explicit rule to read `.claude/skills/bubblewright-level-design/SKILL.md` before designing or modifying stages).

- [ ] **Step 3: GREEN Verification & REFACTOR — Re-run the 2 subagent scenarios WITH the skill**

Dispatch 2 fresh `generalPurpose` subagents on the exact same Scenario A and Scenario B prompts, pointing them to `CLAUDE.md` / `.claude/skills/bubblewright-level-design/SKILL.md`:
- Verify both subagents produce stages with clear symmetry/concepts that pass `python3 .claude/skills/bubblewright-level-design/scripts/check_stage.py` and `unity test . --mode EditMode --filter StageValidatorTests` on the first run.
- If either subagent hits a loophole or validation failure, tighten `SKILL.md` / `check_stage.py` and re-test until clean. Remove any temporary test stage files (or keep only if part of the validated library) and remove `Temp/skill-red-baseline.md`.

- [ ] **Step 4: Final verification & commit**

Run:
```bash
python3 .claude/skills/bubblewright-level-design/scripts/check_stage.py Assets/Bubblewright/Scripts/Stage/Stages && \
python3 .claude/skills/unity-csharp-scripting/scripts/check_unity_project.py . && \
unity test . --mode EditMode --output Temp/editmode-final.xml
```
Expected: All three commands exit `0`, `failed="0"`.

```bash
git add CLAUDE.md .claude/skills/bubblewright-level-design/ docs/superpowers/
git commit -m "Add bubblewright-level-design agent skill and CLAUDE.md"
```
