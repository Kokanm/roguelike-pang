# Authored Stages, Puzzle Geometry & Level-Design Skill Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace procedural stage generation with self-discovering authored `StageSpec` stages (supporting crouch tunnels, vertical divider walls, patient floor/wall Clingers, and discrete multi-storey climbs), ship a 72-stage starter library (`12` per world), make hard-world bosses harder in place of the old bubble-budget bonus, and build + TDD-test the `.claude/skills/bubblewright-level-design/` agent skill and `CLAUDE.md`.

**Architecture:** First extend `Wick`, `ClingerBehavior`, `StageMap`, `StageView`, `Game`, and weapons for crouch crawlspaces, surface-mounted patient Clingers, and `32 × (14 * Storeys)` discrete multi-storey arenas (up to `3` storeys, enemies sealed inside their storey, Wick only goes up). Next introduce `StageSpec` / `StageBuilder` / `StagePicker` / `StageValidator`, retire `StageGenerator` and `StageLayouts`, and author the 12 World 1 stages for Kokan to play-test. Then run baseline RED subagent stage-design tests, author the skill + `check_stage.py` + `CLAUDE.md`, and verify GREEN compliance with fresh subagents against `StageValidatorTests`. Last, author the other 60 stages with the skill, one world per parallel thread, and scale the hard-world bosses.

**Tech Stack:** Unity `6000.6.7f1` (2D URP), C# 9 (`.NET Standard 2.1`), NUnit EditMode Test Runner (`unity` CLI), Python 3.10+.

**Spec:** `docs/superpowers/specs/2026-11-16-authored-stages-and-skill-design.md`

## Global Constraints

- Unity `6000.6.7f1`, `480 × 270` reference resolution at `15 PPU`; custom kinematic collision (`StageMap`), deterministic `Game.Update` tick order (no `Rigidbody2D`).
- Every stage is a `sealed` `[Preserve]` subclass of `StageSpec` in its own file under `Assets/Bubblewright/Scripts/Stage/Stages/W{1..6}/`, auto-discovered by `StageSpec.All`.
- `StagePicker` must deterministically pick the exact same `StageSpec` and mirror flag from `(seed, stage, hardWorld)`. `SaveData` (`version = 2`) also stores the current world's picked stage `Id`s and mirror flags, so adding stages never changes a saved run.
- Unlock schedule: `1-1` has only `Gloop` and solid/ladder geometry (no `B`, no `_`); `1-2+` unlocks `B` and `_`; `1-3+` unlocks `Trigloop` and plain `Pogo`s; W2 unlocks `Clinger`, `Lead`, `Drifter`, `Roller`; W3 unlocks `Sinker`, `Steel`, `Ceramic` (bubbles `<= M`, big rollers); W4 unlocks `Frost`; W5 unlocks `Ghost`; W6 unlocks `Scorch`. Hazards stay in their own world: `Sand` only in W3, `Ice` only in W4, `SpikeStrip`s only in W6. `Lucky` is never authored: the picker swaps one plain starting bubble at random. Guests are placed by the author (at most one earlier signature kind per stage), never rolled.
- Every authored stage must pass `StageValidator` (normal and mirrored if `CanMirror`) and be clearable hitless with **Moss** (`7.5 u/s`) and **Flint** (`16 u/s`), default Harpoon, and `0` upgrades. The same must hold under the hard-road debuffs (Moss with Heavy Boots at about `6.4 u/s`, Flint with Frayed Cable at `12.8 u/s`). Wall jumps and chained wall jumps are a base move and may be required.
- Run `python3 .claude/skills/unity-csharp-scripting/scripts/check_unity_project.py .` and `unity test . --mode EditMode` after every C# task.
- `.claude/` is ignored by `.gitignore` (line 144), so `check_unity_project.py` exists only on Kokan's laptop until Task 6 Step 0 un-ignores it. Run these tasks on the laptop.

## Review Focus

1. **Releasing Down or pressing Jump inside a `1`-row (`1.0u`) crouch crawlspace:** Wick (`standing = 1.467u`, `crouching = 0.88u`) must stay crouched and grounded without wedging into the ceiling tile above, and stand up automatically once he shuffles into a `>= 2`-row opening.
2. **Upper-storey weapon ceiling clamps (`y = 14..28`):** Weapons (`Harpoon`, `WeaponWorld`, `Skyrocket`, `RicochetDisc`, `GasFlask`, `Longshot`, `LampLance`) fired on Storey `1` (`y >= 14`) must clamp to `(shooterStorey + 1) * 14f` (`28f`) rather than dying immediately against `Tuning.I.arenaHeight` (`14f`), while shots fired on Storey `0` still stop at `14f` so unseen Storey `1` enemies cannot be sniped blindly.
3. **One-way storeys:** Wick only goes up. A hatch stays shut until the storey below it is cleared and shuts again behind him, so he can never drop from Storey `1` to Storey `0`; `FallIntoGap` (`y < -gapFallDepth`) still only triggers below `y = 0f`. No bubble, roller, Pogo or Ghost ever crosses a storey boundary, open hatch or not.
4. **Horizontal mirroring of asymmetric placements (`CanMirror = true`):** Mirroring must flip `SpikeStrip.col` to `32 - col - width` (not `31 - col`), swap `ClingerMount.LeftWall <-> RightWall`, and negate `wickStart.x`, `SpawnSpec.position.x`/`direction`, and `RollerSpec.x`/`direction`.
5. **Over-breaking `B` tiles (`all B broken`):** Destroying every `B` tile on a stage must never drop Wick into an inescapable pit or destroy the sole bridge needed to reach/shoot a solid-sealed chamber.
6. **Every weapon respects the storey clamp:** not only the files that read `Tuning.arenaHeight`. `DoubleHarpoon`, `VeeHarpoon` and `LampPost` call `StageMap.CeilingAbove` directly, and `SkewerHarpoon`, `ArcCoil`, `LamplighterTorch`, `Rattler`, `Scattergun` and `TwinCarbine` must be checked too.
7. **HUD on upper storeys:** the HUD is placed from `WorldToGui(... T.arenaHeight)` in `Game.cs`, so it must add `camPanY` like the background and the thumb strip.

---

### Task 1: Crouch Crawlspaces & Low-Ceiling Firing (`Wick`, `Harpoon`)

**Files:**
- Modify: `Assets/Bubblewright/Scripts/Player/Wick.cs:292-310,422-515,705-715,788-842`
- Test: `Assets/Bubblewright/Tests/Editor/AirMoveTests.cs`

**Interfaces:**
- Consumes: `StageMap.CeilingAbove(float x, float y, out int hitRow) -> float`, `StageMap.OverlapsSolid(Rect r) -> bool`
- Produces:
  - `Wick.ForcedCrouch` (`bool`, true on ground when the lowest solid underside above either edge of his crouched box is less than `T.hitbox.y - 1e-4f` above his feet)
  - `Wick.BodyBox(float x, float y)` using `HitboxHeight` (`0.88u` when `Crouching`, `1.467u` standing)
  - Crouched `Wick.LauncherTip` with `y = FeetY + Mathf.Min(T.launcherTip.y * T.crouchSquash.y, HitboxHeight - T.Pixel)`

- [ ] **Step 1: Add failing crouch-tunnel tests in `Assets/Bubblewright/Tests/Editor/AirMoveTests.cs`**

Add tests pinning:
- `CrouchingLetsWickShuffleThroughAOneRowTunnelAndLocksCrouchUntilClear`: row `1` has `#` across cols `14..18` (`1.0u` clearance on row `0`). Standing Wick blocked at col `13`; holding Down (`climb: -1f`) enters col `14..16`; releasing Down (`climb: 0f`) inside col `15` keeps `wick.Crouching == true`, ignores `jump: true` (`!wick.Jumping`), and exiting past col `18` restores `wick.Crouching == false`.
- `CrouchedInAOneRowTunnelCanFireUpThroughASlotOrBreakCeilingTile`: inside a `1`-row tunnel with a `B` tile at `(col 16, row 1)` and `.` at `(col 17, row 1)`, `wick.LauncherTip.y < 1f`, firing at col `16` hits `(16, 1)`, and firing at col `17` travels above `y = 2f`.
- `HurtInsideAOneRowTunnelStaysCrouchedAndNeverOverlapsTheRoof`: Wick takes a hit at col `15` inside the tunnel; during the whole knockback `wick.Crouching == true` and his hitbox never overlaps a solid tile.

- [ ] **Step 2: Run `AirMoveTests` to verify they fail**

Run: `unity test /Users/kokanmalenko/Documents/MyProjects/roguelike-pang --mode EditMode --filter "Bubblewright.Tests.AirMoveTests" --output Temp/airmove.xml`
Expected: FAIL (`Wick` blocked horizontally at `1`-row tunnel entrance and `LauncherTip.y > 1f`).

- [ ] **Step 3: Implement crouch-height `BodyBox`, low-ceiling crouch lock, and crouched `LauncherTip` in `Wick.cs`**

1. In `Wick.BodyBox(float x, float y)`, replace `T.hitbox.y` with `HitboxHeight`.
2. Add `bool MustCrouch(StageMap map, float x, float y)`. Do not call `HeadCeiling` for this: it adds the standing height to the feet, and `CeilingAbove` skips any tile below the height it is given, so the roof tile one row above his feet would be missed and Wick would stand up inside the tunnel. Measure from the crouched top instead: `float top = y + T.hitbox.y * T.crouchHeightScale; float half = T.hitbox.x * 0.5f - 0.01f; float ceiling = Mathf.Min(map.CeilingAbove(x - half, top, out _), map.CeilingAbove(x + half, top, out _)); return ceiling - y < T.hitbox.y - 1e-4f;`
3. Set `Crouching = Mode == Mode.Ground && (MustCrouch(map, pos.x, pos.y) || (wantClimb < 0f && knockTimer <= 0f));` so a knockback inside a tunnel cannot stand him up, and skip `StartJump()` when `MustCrouch(map, pos.x, pos.y)` is true.
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

### Task 3a: `StageMap.Rows` Becomes Per-Map (pure refactor, no behaviour change)

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
- Modify: `Assets/Bubblewright/Tests/Editor/StageMapTests.cs`
- Modify: every test under `Assets/Bubblewright/Tests/Editor/` that reads `StageMap.Rows` or `Tuning.I.arenaHeight` as the map height (about 40 files)

**Interfaces:**
- Produces:
  - `StageMap.StoreyRows = 14`, `map.Storeys` (`int 1..3`), `map.Rows` (`14 * Storeys`), `map.Height` (`float`), `StageMap.Flat(int storeys = 1)`, `StageMap.StoreyOf(float y) -> int` (`Mathf.Clamp(Mathf.FloorToInt(y / 14f), 0, Storeys - 1)`)

- [ ] **Step 1: Make the row count a property of the map**

Change `StageMap.Rows` from `const` to `public const int StoreyRows = 14; public int Storeys { get; } public int Rows => StoreyRows * Storeys; public float Height => Rows;` with `new StageMap(int storeys = 1)` and `StageMap.Flat(int storeys = 1)`. Update all `StageMap.Rows` call sites to `map.Rows` (or `StageMap.StoreyRows` where referring to one screen). `Tuning.arenaHeight = 14f` stays as the height of one screen. No map has more than `1` storey yet, so nothing in the game changes.

- [ ] **Step 2: Run the project checker and the full EditMode suite**

Run: `python3 .claude/skills/unity-csharp-scripting/scripts/check_unity_project.py . && unity test . --mode EditMode --output Temp/editmode.xml`
Expected: every existing test passes with no change to what it asserts. This is the proof that the refactor changed no behaviour, so do not add features in this task.

- [ ] **Step 3: Commit**

```bash
git add Assets/Bubblewright/
git commit -m "Make StageMap rows and height per-map"
```

---

### Task 3b: Multi-Storey Parsing, Sealed Storeys & Shot Clamp

**Files:**
- Modify: `Assets/Bubblewright/Scripts/Stage/StageMap.cs`, `StageLayout.cs`
- Modify: `Assets/Bubblewright/Scripts/Bubbles/Bubble.cs`, `PogoShape.cs`, `Roller.cs`, `Behaviors/GhostBehavior.cs`, `Behaviors/ClingerBehavior.cs`, `Behaviors/DrifterBehavior.cs`
- Modify: every file in `Assets/Bubblewright/Scripts/Weapons/` that asks the map for a ceiling or floor (all 16 weapons and `Harpoon.cs`, `WeaponWorld.cs`)
- Create: `Assets/Bubblewright/Tests/Editor/MultiStoreyTests.cs`
- Modify: `Assets/Bubblewright/Tests/Editor/StageMapTests.cs`

**Interfaces:**
- Consumes: per-map `Rows` / `Height` (Task 3a)
- Produces:
  - `StageLayout.Parse(string[] rows, string ground = null, Surface defaultSurface = Surface.Normal) -> StageMap` (accepting `rows.Length == 14 * storeys` plus legacy `15`-row overload, with tile chars `. # N I S B H T` and ground chars `= N I S _`)
  - `StageMap.StoreyCeiling(float fromY)`, `StageMap.StoreyBase(float fromY)`, `StageMap.ShotCeiling(float x, float fromY, out int hitRow)`, `StageMap.ShotFloor(float x, float fromY)`
  - `Bubble.homeStorey`, `Roller.homeStorey`

- [ ] **Step 1: Write failing tests in `Assets/Bubblewright/Tests/Editor/MultiStoreyTests.cs`**

Add tests pinning:
- `ParseBuildsTwoStoreyMapWithInlineSurfacesAndLaddersAcrossBoundary`: `28` rows + ground row with `I`, `S`, `N`, `H`, `T` across rows `12..15` produces `map.Storeys == 2`, `map.Rows == 28`, `map.Height == 28f`, and Wick climbs smoothly from `y = 10f` to `y = 15f`.
- `HarpoonAndWeaponShotsClampToCurrentStoreyCeilingAndStopAtStoreyBoundary`: upward `Harpoon` and `WeaponWorld` shot fired from `y = 2f` on a 2-storey map with an open shaft at col `16` stop at `y = 14f` (cannot hit a bubble at `y = 16f`), while shots fired from `y = 16f` reach up to `y = 28f`; firing upward from `y = 12f` into a `B` tile at `(col, row 13)` still damages `(col, 13)`.
- `NoEnemyLeavesItsHomeStorey`: on a 2-storey map with an open hatch at col `16`, a `Ghost` spawned at `y = 18f` and an `XL` Gloop, a `Drifter` and a roller spawned on each storey never cross `y = 14f` in `30s`, and children keep their parent's home storey.
- `EveryWeaponStopsAtItsStoreyCeiling`: `[TestCaseSource]` over `WeaponSpec.All`, each fired from `y = 2f` under an open shaft on a 2-storey map: nothing it creates damages a bubble or tile at `y >= 14f`.

- [ ] **Step 2: Run `MultiStoreyTests` to verify they fail**

Run: `unity test . --mode EditMode --filter "Bubblewright.Tests.MultiStoreyTests" --output Temp/multistorey.xml`
Expected: FAIL (compile errors on the `StageLayout.Parse` overload, `ShotCeiling`, `homeStorey`).

- [ ] **Step 3: Implement**

1. Extend `StageLayout.Parse` to support `14 * storeys` rows, optional `ground` parameter (keeping legacy `15`-element array support), `defaultSurface`, tile chars `N`/`I`/`S`, and ground chars `N`/`I`/`S`.
2. **Storey bounds on enemies and weapons:**
   - Add `StageMap.StoreyCeiling(float fromY)` (`(StoreyOf(fromY) + 1) * StoreyRows`) and `StageMap.StoreyBase(float fromY)` (`StoreyOf(fromY) * StoreyRows`).
   - In `Harpoon` (`Fire`, `Tick`, `DownStop`, `AngledStop`) and `WeaponWorld` / `Skyrocket` / `RicochetDisc` / `PopMine` / `GasFlask` / `Longshot` / `LampLance`: clamp upward travel to `Mathf.Min(tileCeil, map.StoreyCeiling(originY))` (still allowing `hitRow == (StoreyOf(originY) + 1) * 14 - 1` `B` tiles on row `13` to take damage) and downward travel to `Mathf.Max(tileFloor, map.StoreyBase(originY))`.
   - Home storey for every enemy: `Bubble` and `Roller` record `int homeStorey` on spawn (`map.StoreyOf(position.y)`, children copy the parent's) and resolve their vertical bounds against `[homeStorey * 14f, (homeStorey + 1) * 14f]` in `Bubble`, `PogoShape`, `Roller`, `GhostBehavior`, `ClingerBehavior` and `DrifterBehavior`, whether a hatch is open or not.
   - Put the clamp in one place: add `StageMap.ShotCeiling(float x, float fromY, out int hitRow)` and `StageMap.ShotFloor(float x, float fromY)` (tile query limited to the shooter's storey) and route every weapon through them, including `DoubleHarpoon`, `VeeHarpoon`, `LampPost`, `SkewerHarpoon`, `ArcCoil`, `LamplighterTorch`, `Rattler`, `Scattergun` and `TwinCarbine`, which are not in the file list above.

- [ ] **Step 4: Run the project checker and the full EditMode suite**

Run: `python3 .claude/skills/unity-csharp-scripting/scripts/check_unity_project.py . && unity test . --mode EditMode --output Temp/editmode.xml`
Expected: PASS (`failed="0"` across all existing and new tests).

- [ ] **Step 5: Commit**

```bash
git add Assets/Bubblewright/
git commit -m "Parse multi-storey maps, seal enemies in their storey, clamp shots"
```

---

### Task 3c: Camera Pan, HUD, Hatches & Dormant Storeys (ends with a look by Kokan)

**Files:**
- Modify: `Assets/Bubblewright/Scripts/Stage/StageView.cs`, `Scripts/Core/Tuning.cs`, `Scripts/Core/Game.cs`, `Scripts/Core/Game.Stage.cs`, `Scripts/Core/Game.Waves.cs`, `Scripts/Core/Game.Rollers.cs`, `Scripts/Core/Game.Menus.cs`, `Scripts/UI/TouchControlsView.cs`
- Modify: `Assets/Bubblewright/Tests/Editor/MultiStoreyTests.cs`

**Interfaces:**
- Consumes: multi-storey maps, `homeStorey`, shot clamp (Task 3b)
- Produces:
  - `Tuning.storeyPanSpeed = 56f`
  - `StagePlan.wickPosition` (`Vector2`, replacing `wickX` while keeping `wickX` property alias until Task 4), `StagePlan.waveStorey` (`int`)
  - `Game.ComputeActiveStorey(int current, float feetY, int storeys) -> int`, hatches, `awakeStoreys`

- [ ] **Step 1: Add failing tests to `MultiStoreyTests.cs`**

- `StageViewResizesTextureForTwoStoreyMap`: `StageView` builds a `480 × 420` texture for a 2-storey map and `480 × 210` when switched back to a 1-storey map.
- `HatchOpensWhenTheStoreyIsClearedAndShutsBehindWick`: with a required bubble alive on Storey `0` the hatch cell is solid and Wick cannot climb past `y = 14f`; after the last required enemy pops it opens; once `wick.FeetY >= 14f`, `activeStorey == 1`, Storey `1` wakes, the hatch is solid again and `activeStorey` never returns to `0`. Dormant Storey `1` enemies do not tick and do not hurt Wick before that.
- `HudAndTouchStripFollowTheCameraPan`: with `camPanY = 14f` the HUD anchor and the touch strip sit `14u` higher than on Storey `0`.

- [ ] **Step 2: Run `MultiStoreyTests` to verify the new tests fail**

- [ ] **Step 3: Implement**

1. **`StageView`:** Rebuild `pixels`, `pixelCanvas`, `texture`, and `sprite` when `map.Rows != currentRows`.
2. **Camera pan, HUD, thumb strip, hatches and dormant storeys (`Game`, `TouchControlsView`):**
   - Add `public float storeyPanSpeed = 56f;` to `Tuning`.
   - Add `public static int ComputeActiveStorey(int current, float feetY, int storeys)` (advances when `feetY >= (current + 1) * 14f`, never drops). Add hatches: the ladder cell(s) on row `14 * f - 1` are solid for Wick while a required enemy of storey `f - 1` is alive (waves included), open when it is cleared, and solid again once `activeStorey == f`. `StageView` draws a shut hatch.
   - Track `int activeStorey`, `float camPanY`, and `bool[] awakeStoreys` in `Game`. Snap both on `StartStage` to `Map.StoreyOf(plan.wickPosition.y)`. Pan `camPanY` toward `activeStorey * 14f` at `T.storeyPanSpeed * dt`. Offset `cam.transform.position.y`, `background.transform.position.y`, `TouchControlsView` (`camPanY` parameter), and a `FloorStripMask` sprite (`y = camPanY - 4f .. camPanY`, enabled when `camPanY > 0.01f`) by `camPanY`.
   - Tick a bubble or roller only when `awakeStoreys[homeStorey]` is `true`, and skip its hit test against Wick otherwise; wake `awakeStoreys[activeStorey] = true` on `TickPlay`. Offset the HUD anchors in `Game.cs` (`WorldToGui(... T.arenaHeight)`) by `camPanY`. Show `FLOOR {activeStorey + 1}/{Map.Storeys}` in `StageLabel()` when `Map.Storeys > 1`.
3. **Debug stage:** add a hand-written 2-storey map behind the debug panel's `TEST STAGE` button (a few bubbles on each storey, one hatch, one crouch tunnel from Task 1, one floor Clinger from Task 2), so the pan, the strip mask, the HUD and the hatch can be looked at before any real stage exists.

- [ ] **Step 4: Run the project checker and the full EditMode suite**

Run: `python3 .claude/skills/unity-csharp-scripting/scripts/check_unity_project.py . && unity test . --mode EditMode --output Temp/editmode.xml`
Expected: PASS (`failed="0"`).

- [ ] **Step 5: Commit**

```bash
git add Assets/Bubblewright/
git commit -m "Add storey camera pan, HUD offset, hatches, and dormant storeys"
```

- [ ] **Step 6: Stop for Kokan**

Kokan plays the debug stage in the editor and on a phone build and confirms the pan, the thumb strip, the HUD and the hatch look right. None of this can be checked by EditMode tests. Do not start Task 4 before he says so.

---

### Task 4: `StageSpec` Framework, `StagePicker`, `StageValidator` & Procedural Cutover

**Files:**
- Create: `Assets/Bubblewright/Scripts/Stage/StageSpec.cs`
- Create: `Assets/Bubblewright/Scripts/Stage/StagePicker.cs`
- Create: `Assets/Bubblewright/Scripts/Stage/StageValidator.cs`
- Create: `Assets/Bubblewright/Scripts/Stage/Stages/W1/W1_FirstPop.cs` *(1 minimal bootstrap stage per world `W1..W6` so `StagePicker` tests run immediately; expanded to all 72 in Tasks 5 and 7)*
- Create: `Assets/Bubblewright/Tests/Editor/StageValidatorTests.cs`
- Delete: `Assets/Bubblewright/Scripts/Stage/StageLayouts.cs` (+ `.meta`), `StageRecipe.cs` (+ `.meta`), `PlacementPatterns.cs` (+ `.meta`)
- Delete: `Assets/Bubblewright/Tests/Editor/StageLayoutTests.cs` (+ `.meta`), `RecipeTests.cs` (+ `.meta`), `SignatureTests.cs` (+ `.meta`), `GuestTests.cs` (+ `.meta`), `PlacementPatternTests.cs` (+ `.meta`)
- Modify: `Assets/Bubblewright/Scripts/Stage/WorldRules.cs`, `Scripts/Core/Run.cs`, `Scripts/Core/Tuning.cs`, `Scripts/Core/Game.cs`, `Scripts/Core/Game.Stage.cs`, `Scripts/Core/Game.Menus.cs`, `Scripts/Core/Game.Balance.cs`, `Scripts/Core/StageReport.cs`, `Scripts/Core/SaveData.cs`, `Scripts/Core/DebugPanelLayout.cs`
- Modify: `Assets/Bubblewright/Tests/Editor/RunStructureTests.cs`, `BalanceTests.cs`, `SaveTests.cs`, `WaveTests.cs`, `RollerTests.cs`, `PogoTests.cs`, `SteelCeramicTests.cs`, `SpikeTests.cs`, `BossTests.cs`, `SurfaceTests.cs`, `HardWorldTests.cs`, `KindFoundationTests.cs`

**Interfaces:**
- Produces:
  - `public abstract class StageSpec` (`Id`, `Name`, `World`, `Difficulty`, `MinStageInWorld => 1`, `MaxStageInWorld => 5`, `CanMirror => true`, `TimeScale => 1f`, `Concept`, `Storeys => 1` (`1..3`), `Rows`, `Ground`, `Populate(StageBuilder b)`, `Build(bool mirror = false) -> StagePlan`, `public static IReadOnlyList<StageSpec> All`, `ById(string id)`)
  - `public sealed class StageBuilder` with constants `TierXL = 0, TierL = 1, TierM = 2, TierS = 3` and helpers:
    - `Wick(float x, float y = 0f)` / `WickAtCol(int col, int row = 0)`
    - `Bubble(BubbleKind kind, int tier, float x, float y, int dir = 1, bool ceramic = false, bool pogo = false, bool steel = false)` / `BubbleAt(BubbleKind kind, int tier, int col, float y, int dir = 1, bool ceramic = false, bool pogo = false)`
    - `Clinger(int tier, float x, float y, ClingerMount mount = ClingerMount.Ceiling, int dir = 1, bool pogo = false, bool ceramic = false, bool patient = true)` / `FloorClinger(int tier, float x, float surfaceY = 0f, int dir = 1, bool pogo = true, bool ceramic = false, bool patient = true)`
    - `SteelPogo(int tier, float x, float surfaceY = 0f)`
    - `Roller(float x, float surfaceY = 0f, int dir = 1, int tier = 0, bool ceramic = false, bool steel = false)`
    - `Spike(int col, int width, int row = -1, float? phase = null)`
    - `Wave(int waveIndex, BubbleKind kind, int tier, float x, int dir = 1, bool ceramic = false, bool pogo = false)` + `WaveStorey(int storey)`
  - `public static class StagePicker`:
    - `PickForWorld(int seed, int world, bool hard) -> StageSpec[]` (length `5`, no duplicate `Id`s when pool `>= 5`, Calm targets `[1,2,2,3,3]`, Hard targets `[1,3,3,4,4]` so slot `1` is the `D1` intro on both roads, slots `2..5` ordered by non-decreasing `Difficulty` respecting `MinStageInWorld`/`MaxStageInWorld`, nearest-`Difficulty` fallback when no stage fits a slot)
    - `ApplyLucky(StagePlan plan, Run run, int stage)`: from stage `2` of the run, with chance `Tuning.luckyChance` from `run.StageRng(stage)`, one random starting plain `Gloop`/`Trigloop`/`Lead` (not Pogo, not ceramic) becomes `Lucky` with the same tier, position and direction
    - `SaveData.version = 2` with `string[] stageIds` and `bool[] stageMirrors` for the current world; `Run.FromSave` uses them and falls back to a fresh pick for an unknown `Id`
    - `Build(Run run, int stage, int debugOverrideIndex = -1) -> StagePlan`
    - `StageTime(int stage, bool hard = false) -> float`, `BossTime(bool hard = false) -> float`, `BubbleCost(BubbleKind kind, int tier) -> float`
  - `public static class StageValidator`:
    - `Validate(StageSpec spec, bool mirror) -> List<string>` (returns empty list when valid; checks metadata, dimensions, unlock matrix in Spec §1.1, Wick footing + `72`-tick `1.2s` headless simulation for zero early hits, entity/roller/spike clearances, ladder completeness, sand `<= 1` / normal `<= 2` jumpable ground gaps (cap set by Kokan on 2026-11-17), hazards only in their own world, no authored `Lucky`, at most one planned guest, iterative `B`-breach reachability for Moss with Heavy Boots and `0` upgrades (walk, crouch, jump, drop, ladder, wall jump and chained wall jumps), `all B broken` trap/soft-lock check, storeys `1..3` with a reachable hatch and a fair arrival on each, no bubble-reachable pocket Wick cannot shoot into, and pop count inside the `Difficulty` band).

- [ ] **Step 1: Write `StageValidatorTests.cs` covering `StageSpec` mirroring, `StagePicker` curves, and `StageValidator` positive + negative cases**

Include tests pinning:
- `MirrorFlipsGridGroundSpawnsWallClingersAndSpikesExact`: verifies `Spike(col: 4, width: 3)` mirrors to `col == 25, width == 3`, `LeftWall` mirrors to `RightWall`, and `wickPosition.x` negates.
- `ValidatorCatchesUnlockViolationsEarlyHitsTrapPitsAndOverBreakSoftLocks`: synthetic invalid `StageSpec`s fail with descriptive messages (`B` in `1-1`, bubble hitting Wick within `1.2s`, `3`-col gap on Sand, `B` bridge that strands a solid-roofed upper chamber when broken).
- `EveryDiscoveredStageAndMirrorPassesValidation`: `[TestCaseSource]` over `StageSpec.All` (+ mirror when `CanMirror`) asserts `StageValidator.Validate(spec, mirror)` is empty.
- `StagePickerNeverRepeatsInAWorldAndMatchesCalmAndHardCurves`: across 100 seeds and both roads, slot `1` of Worlds `2..6` is the `D1` intro, slots `2..5` never decrease in difficulty, no `Id` repeats when pool `>= 5`, and the fallback is never used for the shipped library.
- `ValidatorRejectsHazardsOutsideTheirWorldAuthoredLuckyAndUnshootablePockets`: `I` tiles in World `6`, `S` tiles in World `4`, a `SpikeStrip` in World `5`, an authored `Lucky`, two guest kinds, and an `S`-sized sealed pocket each fail with a clear message.
- `LuckyReplacesOnePlainBubbleAtRandomAndNeverASpecialOne` and `SavedRunKeepsItsStagesWhenTheLibraryGrows` (a version `2` save resumes the stored `Id`s even after a new `StageSpec` joins the pool).

- [ ] **Step 2: Implement `StageSpec.cs`, `StagePicker.cs`, `StageValidator.cs`, retire `StageGenerator` / `StageLayouts`, and update callers & tests**

1. Create `StageSpec.cs`, `StagePicker.cs`, and `StageValidator.cs` matching the interfaces above, plus 1 clean bootstrap stage in each `Stages/W{1..6}/` folder so every world has an initial valid stage while wiring cutover.
2. Delete `StageLayouts.cs`, `StageRecipe.cs`, `PlacementPatterns.cs`, and their 5 procedural test files (with `.meta` files). Remove `StageGenerator` from `Run.cs` and unused recipe/guest fields and `hardBudgetScale` from `Tuning.cs` (keep `luckyChance`). Keep `WorldRules.PaintAll` and `WorldRules.PlaceSpikes` for the debug panel buttons.
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

### Task 5: Starter Library — World 1 (`12` Stages) & Play-Test Gate

**Files:**
- Create/Update `12` files in `Assets/Bubblewright/Scripts/Stage/Stages/W1/` (`1× D1`, `5× D2`, `6× D3`)
- Create: `Assets/Bubblewright/Tests/Editor/FilterGapTests.cs`
- Modify: `Assets/Bubblewright/Tests/Editor/StageValidatorTests.cs`

**Interfaces:**
- Consumes: `StageSpec`, `StageBuilder`, `StageValidator` (Task 4)
- Produces: 12 validated World 1 `StageSpec` classes, which become the worked examples of the skill in Task 6

- [ ] **Step 1: Simulate the filter gaps and the floor-Pogo hop before building on them**

Add `FilterGapTests` (EditMode): drop `S`, `M` and `L` Gloops for `60s` over a roof with `1`-, `2`- and `3`-tile slots and count how many pass; and run Moss with Heavy Boots jumping a triggered floor `M` and `S` Clinger Pogo across take-off points `±0.5u`. By arithmetic an `S` bubble has only `0.05u` each side of a `1`-tile slot and Moss clears a triggered `M` Pogo by about `0.2u`. If the simulation agrees, widen the slots (or shrink the Pogos) in *Slotted Bunker*, *Trigloop Sieve* and *Clinger Pogo Gauntlet* and correct the widths in the spec's Section 2.2 before authoring.

- [ ] **Step 2: Add the World 1 pool-count assertion in `StageValidatorTests.cs`**

Assert `W1` has `{D1: 1, D2: 5, D3: 6}` (`12` total, including `w1_first_pop` with `MaxStageInWorld == 1` and `w1_slotted_bunker`).

- [ ] **Step 3: Author the 12 World 1 `StageSpec` classes**

Design distinct, symmetrical/structured combat-puzzles (no random shelf scatter). The `7` stages below are fixed; author `5` more (`+2× D2`, `+3× D3`), each with its own `Concept`, including at least one wall-jump shaft stage and one `3`-storey climb.
- **World 1 (Rooftops — `Gloop`, `Trigloop` from `1-3`, `B` & `_` from `1-2`):**
  1. `W1_FirstPop` (`D1`, `MaxStageInWorld = 1`): Twin symmetric side steps + central open arena, two `L` Gloops splitting cleanly.
  2. `W1_SlottedBunker` (`D2`, `MinStageInWorld = 2`): Kokan's bunker puzzle — Wick starts inside a roofed bunker with three `1`-tile (`1.0u`) slots; `XL`/`L` Gloops bounce across the roof until split to `S`, which can drop inside; `B` side seals let Wick break out if desired.
  3. `W1_Crawlway` (`D2`, `MinStageInWorld = 2`): `1`-row (`1.0u`) central crouch tunnel with overhead firing slots connecting left and right courts divided by a `#` wall + ladder.
  4. `W1_TwinChambers` (`D2`, `MinStageInWorld = 2`): Full-height vertical `#` divider wall down cols `15..16` capped by a ladder bridge; Wick clears one chamber, climbs over, and clears the twin.
  5. `W1_PogoSteps` (`D3`, `MinStageInWorld = 3`): Symmetric stepped terraces guarded by plain `M` Gloop Pogos over solid landings and `2`-col ground gaps.
  6. `W1_TrigloopSieve` (`D3`, `MinStageInWorld = 3`): Upper deck with `2`-tile (`2.0u`) filter gaps that hold `XL`/`L` Trigloops above while letting `M` and `S` children rain into the lower court.
  7. `W1_BellTower` (`D3`, `MinStageInWorld = 3`, `Storeys = 2`): 2-storey Rooftops climb — clear lower belfry, climb central twin ladders to the upper bell-chamber.

- [ ] **Step 4: Run `check_unity_project.py` and `StageValidatorTests`**

Run: `python3 .claude/skills/unity-csharp-scripting/scripts/check_unity_project.py . && unity test . --mode EditMode --filter "Bubblewright.Tests.StageValidatorTests" --output Temp/validator-w1.xml`
Expected: PASS (`failed="0"` for all 12 stages and their mirrors).

- [ ] **Step 5: Commit**

```bash
git add Assets/Bubblewright/Scripts/Stage/Stages/W1/ Assets/Bubblewright/Tests/Editor/
git commit -m "Add 12 authored puzzle stages for World 1"
```

- [ ] **Step 6: Stop for Kokan's play-test**

Kokan plays all 12 World 1 stages (debug `F3` cycles `StageSpec.All`) with Wick and with Moss. Fix what he reports, in the stages or in the engine, before going on. Whatever he corrects here is what the skill in Task 6 must teach, so do not start Task 6 or Task 7 before he says the World 1 stages are right.

---

### Task 6: Author & TDD-Verify `.claude/skills/bubblewright-level-design/` and `CLAUDE.md`

**Files:**
- Create: `.claude/skills/bubblewright-level-design/SKILL.md`
- Create: `.claude/skills/bubblewright-level-design/references/mechanics-and-archetypes.md`
- Create: `.claude/skills/bubblewright-level-design/scripts/check_stage.py`
- Create: `CLAUDE.md`

**Interfaces:**
- Consumes: `StageSpec`, `StageBuilder`, `StageValidator`, `StageValidatorTests` (Tasks 4–5; the 12 play-tested World 1 stages are the worked examples the skill points to)
- Produces:
  - `python3 .claude/skills/bubblewright-level-design/scripts/check_stage.py <path-or-dir>` (structure only: exits `0` on well-formed `StageSpec` file(s), non-zero with exact line/column diagnostics on `!= 32`-char rows, a row count that is not `14`, `28` or `42`, characters outside the tile and ground grammar, or missing `Concept`/`[Preserve]`; it knows no game rules, which all live in `StageValidator`)
  - `.claude/skills/bubblewright-level-design/SKILL.md` (`< 350` lines, frontmatter `name: bubblewright-level-design`, `description: Use when...`)
  - `.claude/skills/bubblewright-level-design/references/mechanics-and-archetypes.md` (exact unit/bounce/jump/harpoon tables, world unlock matrix, and 8 worked puzzle archetypes)
  - `CLAUDE.md` (concise project overview, architecture invariants, test commands, and mandatory pointer to `.claude/skills/bubblewright-level-design/SKILL.md` whenever creating, editing, or reviewing stages)

- [ ] **Step 0: Let git track the skills this plan depends on**

`.gitignore` line 144 ignores all of `.claude/`. Replace it with `.claude/*`, `!.claude/skills/`, `.claude/skills/*`, `!.claude/skills/bubblewright-level-design/`, `!.claude/skills/unity-csharp-scripting/`, then confirm with `git check-ignore -v .claude/skills/bubblewright-level-design/SKILL.md` (no output) and commit `unity-csharp-scripting` so cloud threads can run `check_unity_project.py`.

- [ ] **Step 1: RED Phase — Run 2 baseline subagent stage-design scenarios WITHOUT the skill**

Dispatch 2 fresh `generalPurpose` subagents (in a scratch path or temporary stage files reverted right after recording) with realistic user prompts and **no** skill:
1. *Scenario A (Puzzle + Clinger mechanics):* "Design a Difficulty 3 stage for World 2 where Wick has to deal with floor Clinger Pogos and a central bunker, and add it to the game."
2. *Scenario B (Multi-storey + Surface/Breakable rules):* "Design a 2-storey Difficulty 4 stage for World 3 (Desert) with sand gaps, a crouch passage, and a sealed breakable chamber."

Record verbatim in test notes (`Temp/skill-red-baseline.md`): row-length/alignment mistakes, misunderstood `Clinger` mount/patient flags, illegal `3`-col sand gaps, over-break soft-locks, random/asymmetric bubble placement, or skipped validation commands. Clean up the scratch stage files before Step 2.

- [ ] **Step 2: GREEN Phase — Write `scripts/check_stage.py`, `references/mechanics-and-archetypes.md`, `SKILL.md`, and `CLAUDE.md`**

1. **`scripts/check_stage.py`:** Standalone Python 3 script (stdlib only) that parses one or more `StageSpec` `.cs` files, prints an annotated `32`-col ruler view on error, and checks structure only: `32`-char row widths, a row count of `14`, `28` or `42`, valid tile/ground characters, `[Preserve]`, and non-empty `Concept`. It must not re-implement unlocks, ladders, gap widths or any other rule: those are checked by `StageValidatorTests`, so they cannot drift apart. Verify it exits `0` on `Assets/Bubblewright/Scripts/Stage/Stages/` and non-zero on synthetic broken inputs.
2. **`references/mechanics-and-archetypes.md`:** Complete reference tables (bubble diameters & bounce peaks per kind, Wick standing/crouch/jump/gap metrics for Moss & Flint, narrow-gap filter widths `1u`/`2u`/`3u`, world unlock table, pop-count & `pops/s` targets for `D1..D4`, and 8 puzzle archetypes: *Slotted Bunker*, *Floor Clinger Pogo Gauntlet*, *Crouch Duct Sniping*, *Divided Twin Chambers*, *Breakable Kiln/Vault*, *Sieve Ceiling*, *Roller Orbit Loop*, *2-Storey Tower Climb*). The number tables are not typed by hand: add the editor menu item `Bubblewright > Write Level-Design Reference` that writes them from `Tuning`, and an EditMode test (`LevelDesignReferenceIsUpToDate`) that fails when the file no longer matches.
3. **`SKILL.md`:** Positive concept-first recipe addressing every RED baseline failure:
   - Required **Stage Design Brief** before writing code (`World` + `Difficulty` + `MinStageInWorld`, 1-sentence `Concept`, symmetry/visual plan, Moss + default Harpoon hitless route, over-break check).
   - `32`-column ruler template (`// 01234567890123456789012345678901`) and `StageSpec` + `StageBuilder` recipe.
   - Inline quick-reference numbers + hard fairness rules (no `Updraft`-only jumps, `<= 1`-col sand gaps and `<= 2`-col gaps elsewhere, `patient: true` for puzzle Clingers, no over-break traps).
   - Mandatory validation loop (`python3 .claude/skills/bubblewright-level-design/scripts/check_stage.py <file>`, `check_unity_project.py`, `unity test . --mode EditMode --filter StageValidatorTests`).
4. **`CLAUDE.md`:** Concise repo guide (code layout, `Tuning` rules, no physics engine, EditMode test commands, and explicit rule to read `.claude/skills/bubblewright-level-design/SKILL.md` before designing or modifying stages).

- [ ] **Step 3: GREEN Verification & REFACTOR — Re-run the 2 subagent scenarios WITH the skill**

Dispatch 2 fresh `generalPurpose` subagents on the exact same Scenario A and Scenario B prompts, pointing them to `CLAUDE.md` / `.claude/skills/bubblewright-level-design/SKILL.md`:
- Verify both subagents produce stages with clear symmetry/concepts that pass `python3 .claude/skills/bubblewright-level-design/scripts/check_stage.py` and `unity test . --mode EditMode --filter StageValidatorTests` on the first run. A stage that passes may be kept as one of the `60` stages of Task 7.
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
git add .gitignore CLAUDE.md .claude/skills/bubblewright-level-design/ .claude/skills/unity-csharp-scripting/ docs/superpowers/
git commit -m "Add bubblewright-level-design agent skill and CLAUDE.md"
```

---

### Task 7: Starter Library — Worlds 2 to 6 (`60` Stages), Authored With the Skill

Run one world per parallel thread: each stage is its own file, so the five threads cannot conflict. Every thread follows `.claude/skills/bubblewright-level-design/SKILL.md` from Task 6. A weakness a thread finds in the skill is fixed in the skill, not worked around.

**Files:**
- Create/Update `12` files in each of `Assets/Bubblewright/Scripts/Stage/Stages/W2/` … `W6/` (`1× D1`, `3× D2`, `4× D3`, `4× D4` per world)
- Modify: `Assets/Bubblewright/Tests/Editor/StageValidatorTests.cs`, `README.md`

**Interfaces:**
- Consumes: `StageSpec`, `StageBuilder`, `StageValidator` (Task 4), the level-design skill (Task 6)
- Produces: Complete 72-stage library (`12` per world across Worlds `1..6`)

- [ ] **Step 1: Expand pool-count assertions in `StageValidatorTests.cs` to all 6 worlds (`72` stages total)**

Assert `StageSpec.All.Count == 72`, `W1` has `{1, 5, 6, 0}`, `W2..W6` each have `{1, 3, 4, 4}` (including `w2_clinger_pogo_gauntlet`), and `StageReport.Summary(20)` + `StageReport.LogRun(new Run(42))` run cleanly across all 36 run slots on both Calm and Hard roads. Each world's thread adds only its own world's assertion; the total is asserted when all five are merged.

- [ ] **Step 2: Author the 60 `StageSpec` classes**

The `7` stages listed per world are fixed; author `5` more per world (`+1× D2`, `+2× D3`, `+2× D4`), each with its own `Concept` and at least one wall-jump shaft stage per world. No `Sand` outside W3, no `Ice` outside W4, no `SpikeStrip`s outside W6, no authored `Lucky`, guests placed on purpose and named in `Concept`. A `Frost` guest outside W4 makes only its own temporary ice.
- **World 2 (Forest — `Clinger`, `Roller`, `Lead`, `Drifter`):**
  1. `W2_CanopyAmbush` (`D1`, `MaxStageInWorld = 1`): Clean `Clinger` intro — symmetric ceiling and under-bough patient Clingers over a wide safe floor.
  2. `W2_ClingerPogoGauntlet` (`D2`, `MinStageInWorld = 2`): Kokan's floor-Pogo puzzle — row of patient floor `M`/`S` Clinger Pogos spaced across long runs; Wick jumps over them and fires `Down + Fire` in mid-air (or triggers their upward leap and shoots from beside them).
  3. `W2_RollingTrack` (`D2`, `MinStageInWorld = 2`): Central elevated loop where two pearl `Roller`s orbit the platform top and underside while `Lead` bubbles bounce low.
  4. `W2_HollowTrunk` (`D3`, `MinStageInWorld = 2`): Vertical `#` trunk walls with patient `LeftWall`/`RightWall` Clingers guarding ladder shafts and a `B`-sealed root vault.
  5. `W2_DrifterPrism` (`D3`, `MinStageInWorld = 2`): Angled `#` pillar diamond deflecting `Drifter` diagonals around a sheltered crouch pillbox.
  6. `W2_TreetopAscent` (`D4`, `MinStageInWorld = 3`, `Storeys = 2`): 2-storey Forest canopy climb with rollers on Storey `0` and ceiling Clingers + reinforcement wave on Storey `1`.
  7. `W2_BrambleFortress` (`D4`, `MinStageInWorld = 4`): Hard-road fortress combining a slotted bunker, orbiting platform roller, and patient floor Clinger Pogos on the flanks.
- **World 3 (Desert — `Sinker` + `Sand` + `Steel` + `Ceramic`, sand gaps `1` column wide at most; Kokan 2026-11-17):**
  1. `W3_SinkingDunes` (`D1`, `MaxStageInWorld = 1`): Clean `Sinker` on sand intro — twin `L` Sinkers quickly losing bounce into ground rollers between stepped dunes.
  2. `W3_KilnVault` (`D2`, `MinStageInWorld = 2`): `B`-brick sealed upper kiln holding `Ceramic` Gloops; Wick chips open whichever firing slot he wants.
  3. `W3_PendulumPit` (`D2`, `MinStageInWorld = 2`): Indestructible `Steel` roller patrolling a central sunken sand basin while Wick works from twin side balconies (`1`-col sand gaps).
  4. `W3_Hourglass` (`D3`, `MinStageInWorld = 2`): Symmetric hourglass funnel — `Sinker`s start on high shelves, lose height, and roll inward toward central `B` plugs.
  5. `W3_SteelColumns` (`D3`, `MinStageInWorld = 3`): Two `Steel` Pogos acting as rhythmic vertical gates flanking a central crouch bunker with `Ceramic` Sinkers outside.
  6. `W3_MirageZiggurat` (`D4`, `MinStageInWorld = 3`, `Storeys = 2`): 2-storey stepped pyramid climb with sand gaps (`1` col), ceramic rollers on Storey `0`, and `Sinker` + `Clinger` guests on Storey `1`.
  7. `W3_SandstormBunker` (`D4`, `MinStageInWorld = 4`): Slotted sandstone bunker with `Ceramic` Trigloops overhead, `Steel` roller sweeping the roof, and `1` reinforcement wave.
- **World 4 (Winter — `Frost` + authored `Ice` stretches, normal grip at Wick's start):**
  1. `W4_FirstFreeze` (`D1`, `MaxStageInWorld = 1`): `Frost` intro on alternating `N`/`I` ground bands with safe normal central footing.
  2. `W4_IcePillbox` (`D2`): Normal-grip central slotted bunker flanked by wide `I` ice skating lanes where `Frost` bubbles bounce.
  3. `W4_GlacierWall` (`D2`): Vertical `#` ice-capped divider wall with twin ladders and `Ceramic` Frost bubbles in symmetric chambers.
  4. `W4_FrozenSlide` (`D3`): Crouch-height `1`-row ice slide under a low shelf leading into flanking harpoon chimneys, with floor `Clinger` Pogo guests.
  5. `W4_RinkAndRollers` (`D3`): Upper `I` deck with orbiting pearl rollers and `Steel` Pogo gatekeeper below.
  6. `W4_IceSpire` (`D4`, `Storeys = 2`): 2-storey glacier tower climb with `B` ice seals between storeys and `Frost` + `Drifter` guest on Storey `1`.
  7. `W4_WhiteoutCitadel` (`D4`, `MinStageInWorld = 4`): Multi-chamber ice citadel with slotted ceilings, `Ceramic` Frost XL, and a late wave.
- **World 5 (Catacombs — `Ghost` phasing through tiles + `B` crypt masonry):**
  1. `W5_HauntedNave` (`D1`, `MaxStageInWorld = 1`): `Ghost` intro — raised shelves Wick stands under/on while `Ghost`s phase through platforms and flicker on a predictable beat.
  2. `W5_SealedOssuary` (`D2`): `B`-walled central reliquary where `Gloop`/`Ceramic` bubbles stay trapped inside while `Ghost`s pass freely through its walls toward Wick.
  3. `W5_CryptCrawl` (`D2`): Symmetric `1`-row crouch catacomb tunnels with `B` ceiling plugs under twin `Ghost` + `Trigloop` vaults.
  4. `W5_DividedTombs` (`D3`): Three vertical `#` tomb chambers linked by upper ladder galleries and `B` side doors.
  5. `W5_PhantomBunker` (`D3`): Slotted `#` bunker that blocks `XL` Trigloops on the roof while `L` `Ghost`s ignore the roof and force Wick to time shots to their solid phase.
  6. `W5_DeepCatacomb` (`D4`, `Storeys = 2`): 2-storey crypt ascent (`Ghost`s bounded per storey) with `Ceramic` rollers on Storey `0` and sealed `B` sarcophagus on Storey `1`.
  7. `W5_BoneSanctum` (`D4`, `MinStageInWorld = 4`): Hard-road sanctum with `Steel` roller, `Ghost` Pogos, `Sinker` guest, and `2` waves.
- **World 6 (Lava — `Scorch` flames + phased `SpikeStrip`s):**
  1. `W6_EmberCourt` (`D1`, `MaxStageInWorld = 1`): `Scorch` + `2` phased ground `SpikeStrip`s intro with wide safe central stonework.
  2. `W6_SlagBunker` (`D2`): Elevated slotted bunker keeping Wick above `Scorch` floor flames, with phased `SpikeStrip`s guarding the outer wings.
  3. `W6_FurnaceGates` (`D2`): Vertical `#` furnace walls dividing three chambers, each with alternating-phase `SpikeStrip`s and patient `Clinger` Pogo guests.
  4. `W6_CinderCrawl` (`D3`): `1`-row crouch duct beneath a `Scorch`-patrolled grate (`1`-tile slots let Wick snipe upward while `Scorch` flames burn harmlessly on the grate above).
  5. `W6_AnvilBridge` (`D3`): Long central bridge with staggered `SpikeStrip`s, `Steel` roller below, and `Ceramic` Scorch bubbles above.
  6. `W6_CalderaClimb` (`D4`, `Storeys = 2`): 2-storey volcano shaft climb with Storey `0` lava gaps + spikes and Storey `1` `Scorch` + `Ghost` guest arena.
  7. `W6_Crucible` (`D4`, `MinStageInWorld = 4`): Finale pre-boss gauntlet combining a slotted bunker, `4` phased `SpikeStrip`s, `Ceramic` Scorch + Trigloop, and `2` reinforcement waves.

Update `README.md`'s stage/debug description briefly to reflect authored `StageSpec` stages and multi-storey/crouch geometry.

- [ ] **Step 3: Run `check_stage.py`, `check_unity_project.py` and the full EditMode test suite**

Run: `python3 .claude/skills/bubblewright-level-design/scripts/check_stage.py Assets/Bubblewright/Scripts/Stage/Stages && python3 .claude/skills/unity-csharp-scripting/scripts/check_unity_project.py . && unity test . --mode EditMode --output Temp/editmode-all.xml`
Expected: all exit `0`; all EditMode tests pass (`failed="0"`).

- [ ] **Step 4: Commit (one commit per world)**

```bash
git add Assets/Bubblewright/ README.md
git commit -m "Add 12 authored puzzle stages for World N"
```

- [ ] **Step 5: Kokan play-tests each world as it lands**

---

### Task 8: Harder Hard-World Bosses (replaces the `+30%` bubble budget)

Independent of Tasks 5 to 7: it can run in its own thread any time after Task 4.

**Files:**
- Modify: `Assets/Bubblewright/Scripts/Core/Tuning.cs`, `Scripts/Bosses/Boss.cs`, `Scripts/Bosses/BossSpec.cs`, `Scripts/Bosses/BossFight.cs`, `Scripts/Core/Game.Boss.cs`
- Test: `Assets/Bubblewright/Tests/Editor/BossTests.cs`, `HardWorldTests.cs`

**Interfaces:**
- Produces: `Tuning.hardBossHpScale = 1.5f`, `Tuning.hardBossSpeedScale = 1.2f`, `Tuning.hardBossRateScale = 1.25f` (values are a call; Kokan tunes them)

- [ ] **Step 1: Add failing tests**

`HardWorldBossHasMoreHealthMovesFasterAndAttacksFaster`: for each of the 6 bosses, a hard-world fight has `Mathf.CeilToInt(hp * hardBossHpScale)` health, moves `hardBossSpeedScale` times faster and fires every attack `hardBossRateScale` times as often; the scales stack with `bossEnrageScale` and `bossStormScale`. `CalmWorldBossIsUnchanged`. `HardWorldNoLongerAddsBubbleBudget` (no reference to `hardBudgetScale` remains).

- [ ] **Step 2: Run `BossTests` and `HardWorldTests` to verify they fail**

- [ ] **Step 3: Implement the three scales where the boss reads its health, speed and attack timers, and delete `hardBudgetScale`**

- [ ] **Step 4: Run the project checker and the full EditMode suite, then commit**

```bash
git add Assets/Bubblewright/
git commit -m "Make hard-world bosses harder in place of the bubble-budget bonus"
```
