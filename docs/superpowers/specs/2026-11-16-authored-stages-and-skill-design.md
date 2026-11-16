# Authored Stages, Puzzle Geometry, and Level-Design Agent Skill

Design agreed with Kokan on 2026-11-16. Items marked **(call)** were settled by the builder and can be changed freely.

## Why

Stages today are built by picking one of 25 shelf layouts (`StageLayouts`) and letting `StageGenerator` spend a bubble budget onto random open coordinates. That makes stages blur together:
- There is no deliberate spatial plan, symmetry, or tactical puzzle behind where walls, platforms, and balloons sit.
- Geometry is locked to platform rows `4` and `9` inside a single `32 × 14` screen, so you cannot build low crouch tunnels, enclosed bunkers with firing slots, vertical divider walls, or multi-floor climbs.
- Future stages will be authored with LLM coding agents, which need both a clean zero-conflict stage format in C# and a dedicated level-design skill (`SKILL.md` + `CLAUDE.md`) grounded in `Bubblewright`'s exact physics and fairness rules.

## Goals

1. **Replace procedural stage generation completely** with hand-crafted, self-discovering C# `StageSpec` stages organized by world (`1..6`) and difficulty tier (`1..4`).
2. **Unlock puzzle geometry in the engine:**
   - Platforms and ceilings at any height down to Wick's crouch height (`1` empty row = `1.0u`).
   - Unbreakable vertical `#` walls of any height (crossed via ladders or intentional wall-jump shafts).
   - Discrete multi-floor stages (`32 × (14 * Floors)`) with smooth vertical camera pans when climbing between floors and dormant upper floors until reached.
   - Floor- and wall-mounted patient `Clinger` spawns so puzzles like "jump over floor Clinger Pogos and shoot downward" work reliably.
3. **Enforce progression and solvability automatically** via an EditMode `StageValidator` test suite covering syntax, world unlocks, clearances, a `1.2s` fair-start simulation, and reachability with base stats and zero upgrades.
4. **Ship a 42-stage starter library** (`7` authored stages per world across Worlds `1..6`) so both Calm and Hard runs match their target difficulty curve in every slot with zero repeats inside a world and showcase the new puzzle archetypes.
5. **Create and TDD-verify the agent level-design skill** (`.claude/skills/bubblewright-level-design/SKILL.md` + repo-root `CLAUDE.md`) so future LLM agents design cohesive, symmetric/intentional, bug-free stages.

## Non-goals

- Redesigning the 6 boss stages (`StageInWorld == 6`): bosses keep their flat `32 × 14` arena and `BossSpec` table.
- Changing hero base stats (`Character.cs`), upgrades, road-choice UI, or weapon mechanics (beyond making ceiling/row checks respect `map.Rows` and `map.Height` on multi-floor maps).
- Free-scrolling arbitrary-height stages (e.g. `19` rows): verticality uses discrete `14`-row stories so every active floor frames cleanly on mobile (`480 × 270`, `15 PPU`).

## Anti-goals

- **"Random balloons on shelves" stages:** every stage must be built around an explicit spatial/combat idea (`Concept`), deliberate symmetry or structure, and purposeful enemy placement.
- **Upgrade-gated or hero-gated stages:** no stage may require `Updraft` (double jump), extra cables, crate weapons, or a fast hero. Every stage must be clearable hitless with **Moss** (`7.5 u/s` walk) and **Flint** (`16 u/s` harpoon), default Harpoon, and zero upgrades.
- **Pixel-perfect touch precision:** mobile touch controls come first; jumps, bunker slots, and safe lanes need comfortable margins rather than frame-perfect inputs.
- **Breakable soft-locks or off-screen ambushes:** breaking `B` tiles *may* be required to breach vaults or open firing lanes, but **over-breaking** (destroying every `B` tile on the map) must never trap Wick in a pit or strand a required enemy out of reach/line-of-fire (e.g. a `B` bridge must never be the sole route to a solid-sealed upper room). Likewise, enemies on unseen floors must never hit or get sniped blindly across floor boundaries.

## Constraints

- Unity `6000.6.7f1` (2D URP), `480 × 270` reference resolution at `15 PPU`, custom kinematic collision (`StageMap`), `Game.Update` deterministic tick order (so hit-stop, slow-mo, and pause freeze everything).
- Saved runs (`SaveData`) store only `seed` and `stage` (`1..36`), so `StagePicker` must deterministically pick the same `StageSpec` and horizontal mirror flag from `(seed, stage, hardWorld)`.
- Parallel agent branches (`claude/project-thread-*`) must be able to add stages concurrently without merge conflicts: each stage lives in its own file and is discovered automatically, matching `WeaponSpec.All`.
- Every engine change and every `StageSpec` must pass EditMode NUnit tests (`unity test`) and `.meta` checks (`check_unity_project.py`).

## Approaches considered

1. **Self-discovering C# `StageSpec` subclasses — one file per stage, like `WeaponSpec` (chosen):**
   Each stage is a `sealed` `[Preserve]` subclass under `Assets/Bubblewright/Scripts/Stage/Stages/W1/`…`W6/` with `32`-char ASCII rows, a `32`-char ground row, metadata (`World`, `Difficulty`, `Concept`), and helper calls (`WickAtCol`, `BubbleAt`, `FloorClinger`, `Roller`, `Spike`, `Wave`).
   - *Pros:* Matches `WeaponSpec`, compile-time type safety, zero merge conflicts across parallel agent branches, trivial to test in NUnit.
   - *Cons:* Editing a stage triggers a ~2–3s C# domain reload in Unity.
2. **Standalone `.txt` / `.json` stage assets in `Resources/Stages/`:**
   - *Pros:* No C# recompile on edits; one file per stage.
   - *Cons:* Invents a custom untyped DSL with no IDE/compiler checks and departs from `Bubblewright`'s code-first architecture.
3. **Six per-world C# table files (`World1Stages.cs` … `World6Stages.cs`):**
   - *Pros:* Few files on disk.
   - *Cons:* Parallel agents adding stages to the same world hit merge conflicts, and 28/42-row diagrams bloat each file past hundreds of lines.

Kokan picked **Approach 1**, and chose to deliver **both** the engine/stage replacement and the agent `SKILL.md` + `CLAUDE.md` in order: **Part 1 (Engine + `StageSpec` framework + 42 starter stages)** first, followed by **Part 2 (`SKILL.md` + `CLAUDE.md`, verified with subagents against the real validator)**.

---

## Part 1: Engine & Authored Stage Framework

### 1.1 Unlock progression across worlds

- **Stage `1-1` (`World == 1 && MaxStageInWorld == 1`):** Pure introduction to movement, jumping, ladders, walls, and `Gloop` splitting. **No** breakable blocks (`B`), **no** ground gaps (`_`), **no** other enemy kinds or modifiers.
- **Stage `1-2` onward (`World >= 2 || MinStageInWorld >= 2`):** Breakable blocks (`B`) and ground gaps (`_`) are unlocked.
- **Stage `1-3` onward (`World >= 2 || MinStageInWorld >= 3`):** `Trigloop` and plain `Pogo`s (`Gloop`, `Trigloop`, `Lead` in W2+) are unlocked.
- **World `2` (Forest) onward:** `Clinger` (signature), `Lead`, `Drifter`, `Roller` (pearl), `Lucky` **(call)**.
- **World `3` (Desert) onward:** `Sinker` (signature), `Sand` surface, `Steel` (steel rollers & steel Pogos), `Ceramic` (bubbles up to tier `M`, big rollers).
- **World `4` (Winter) onward:** `Frost` (signature), `Ice` surface.
- **World `5` (Catacombs) onward:** `Ghost` (signature).
- **World `6` (Lava) onward:** `Scorch` (signature), retracting `SpikeStrip`s.
- **Guest rule in authored stages **(call)**:** A stage in World `W >= 2` may use classic kinds (`Gloop`, `Trigloop`, `Lead`, `Lucky`), its world's signature enemy, `Drifter`, and at most one earlier world's signature enemy (`< W`) when the stage's concept calls for it; stage `W-1` (`Difficulty == 1`) sticks to that world's signature enemy + classic kinds so every world still opens with a clean showcase of its own mechanic.

### 1.2 Multi-floor `StageMap` (`32 × (14 * Floors)`)

- `StageMap.Cols = 32` and `StageMap.FloorRows = 14` remain constants. `StageMap` gains instance properties `Floors` (`>= 1`, default `1`), `Rows` (`14 * Floors`), and `Height` (`(float)Rows`). `StageMap.Flat(int floors = 1)` defaults to `1` floor (`14` rows).
- All tile, ceiling, bounce, harpoon, and weapon bounds checks replace hardcoded `StageMap.Rows` / `Tuning.arenaHeight` with `map.Rows` / `map.Height` (keeping `Tuning.arenaHeight = 14f` as the height of one screen/floor).
- `StageView` resizes its arena `Texture2D` / `Sprite` dynamically to `480 × (210 * map.Floors)` whenever `map.Floors` changes.

### 1.3 Discrete floor camera pan, UI strip & floor wake-up

- **Active floor tracking (`Game`):**
  - Floor `f` spans world `y` in `[14f * f, 14f * (f + 1))`.
  - Going up, `activeFloor` advances to `f` once `wick.FeetY >= 14f * f`. Going down, `activeFloor` drops below `f` once `wick.FeetY < 14f * f - 0.15f` **(call: small hysteresis so standing on a `y = 14` ledge never oscillates)**.
  - Camera target offset `targetPanY = activeFloor * 14f`. `camPanY` moves toward `targetPanY` at `56 u/s` (~`0.25s` per floor) using unscaled/play `dt`, snapped to `1 / 15u` pixels in `UpdateCamera`. On `StartStage`, `activeFloor` and `camPanY` snap immediately to Wick's starting floor.
- **Background & thumb-control strip on upper floors **(call)**:**
  - `Stage Background` moves vertically with `camPanY` so the world's sky and side-wall pillars always frame the current `32 × 14` view.
  - `TouchControlsView` offsets its `stripMid` by `camPanY` so thumb controls stay anchored to the bottom `4u` strip of the screen on every floor.
  - When `camPanY > 0f`, a `FloorStripMask` sprite (`Layer.UI`, order `-2`, `y = camPanY - 4f .. camPanY`) paints the `6 px` floor cap and `theme.strip` fill over the lower floor's top rows so thumb controls never overlap visible tiles below.
- **Dormant floors & cross-floor boundaries **(call)**:**
  - `bool[] awakeFloors` (`Length = Map.Floors`): Wick's starting floor starts awake; any floor `f` wakes permanently the moment `activeFloor == f` or any awake bubble/roller crosses into floor `f`. While `awakeFloors[f]` is `false`, bubbles and rollers on floor `f` render in place at their starting positions without ticking (so Sinkers don't flatten out, Scorches don't litter flames, and non-patient Clingers don't time out before Wick arrives).
  - Upward harpoons and ranged weapon shots clamp their max ceiling to `Mathf.Min(tileCeiling, (shooterFloor + 1) * 14f)` so shots through open ladder shafts cannot snipe dormant enemies on unseen floors above; downward air shots clamp their floor to `Mathf.Max(tileFloor, shooterFloor * 14f)` unless hitting a tile at `shooterFloor * 14f - 1`.
  - `Ghost` bubbles (`IgnoresTiles = true`) clamp their vertical bounds to `[homeFloor * 14f, (homeFloor + 1) * 14f]` instead of `[0, map.Height]` so they stay on their floor rather than phasing through inter-floor ceilings.
  - Ceiling wave markers (`Game.Waves`) spawn at the top of the wave's designated floor (`waveFloor`, defaulting to `Map.Floors - 1`) and wake that floor when spawned.

### 1.4 Crouch crawlspaces & vertical divider walls (`Wick.cs`)

- **Dimensions:** Standing Wick is `1.0u` wide × `1.467u` (`22 px`) tall -> needs **2 empty rows (`2.0u`)** to walk upright. Crouching Wick (`crouchHeightScale = 0.6f`) is **`0.88u` (`13.2 px`) tall** -> fits inside **1 empty row (`1.0u`)**.
- **Horizontal collision fix:** `Wick.BodyBox(x, y)` uses `HitboxHeight` (`0.88u` when `Crouching`, `1.467u` when standing) so Wick can crouch-shuffle (`35%` run speed) into and through `1`-row crawlspaces.
- **Low-ceiling crouch lock:** When `Mode == Mode.Ground` and `HeadCeiling(map, pos.x, pos.y) - pos.y < T.hitbox.y - 1e-4f`, `Crouching` remains `true` even if the player releases Down, and jumping is disabled until `HeadCeiling - pos.y >= T.hitbox.y`.
- **Firing from a crouch crawlspace **(call)**:** While `Crouching`, `LauncherTip.y` clamps to `Mathf.Min(T.launcherTip.y * T.crouchSquash.y, HitboxHeight - T.Pixel)` (`~0.81u`, inside row `0` relative to Wick's feet). Under a solid `#` ceiling at row `+1`, upward harpoons immediately stop at `y = feetY + 1f`; under a `1`-tile gap `.` or breakable `B` at row `+1`, Wick can fire straight up through the slot or chip the `B` block overhead.
- **Vertical unbreakable walls & ladders:** `#` tiles may form vertical walls at any column and height, including full-floor dividers (`rows 0..13`) or bunkers with roofs. `H` (empty ladder) and `T` (solid + ladder) may span any contiguous vertical range `>= 2` rows, including across floor boundaries (`r = 13..14`).

### 1.5 Floor- & wall-mounted patient Clingers (`ClingerBehavior`)

- `ClingerMount` enum added to `SpawnSpec`: `Ceiling = 0` (default: attaches to first solid underside above or floor ceiling), `Floor = 1` (attaches with `normal = Vector2.up` to surface below), `LeftWall = 2` (`normal = Vector2.right` on solid/arena wall to the left), `RightWall = 3` (`normal = Vector2.left` on solid/arena wall to the right).
- `bool patient` added to `SpawnSpec`: when `true`, the Clinger ignores `Tuning.clingerMaxStickTime` (`4s`) during its initial mount and waits until `WickInLane(b)` triggers, its support tile breaks, or `OnStorm` fires. Once it detaches the first time, normal rules apply. Existing `ClingerTests` (where `patient == false`) keep their exact `4s` behavior.

### 1.6 `StageSpec` API, ASCII grammar & deterministic run selection

Each authored stage is a `sealed` `[Preserve]` subclass of `StageSpec` in `Assets/Bubblewright/Scripts/Stage/Stages/W{1..6}/<StageClassName>.cs`:

```csharp
[Preserve]
public sealed class W1_SlottedBunker : StageSpec
{
    public override string Id => "w1_slotted_bunker";
    public override string Name => "Slotted Bunker";
    public override int World => 1;
    public override int Difficulty => 2;
    public override int MinStageInWorld => 2;
    public override string Concept =>
        "Wick starts inside a roofed bunker with three 1-tile firing slots; XL/L/M Gloops bounce overhead until split to S, which can drop in.";

    // Col:  01234567890123456789012345678901
    public override string[] Rows => new[]
    {
        "................................", // r13
        "................................", // r12
        "................................", // r11
        "................................", // r10
        "................................", // r9
        "................................", // r8
        "................................", // r7
        "................................", // r6
        "................................", // r5
        ".........###.###.###.###........", // r4: roof with three 1u slots at cols 12, 16, 20
        ".........#.............#........", // r3
        ".........#.............#........", // r2
        ".........B.............B........", // r1: optional 2-row side exits behind breakables
        ".........B.............B........", // r0
    };

    public override string Ground =>
        "================================";

    protected override void Populate(StageBuilder b)
    {
        b.Wick(0f, 0f);
        b.BubbleAt(BubbleKind.Gloop, TierXL, col: 6, y: 8f, dir: 1);
        b.BubbleAt(BubbleKind.Gloop, TierL, col: 25, y: 8f, dir: -1);
    }
}
```

- **Tile grammar (`Rows`, `14 * Floors` strings of `32` chars, top row first):**
  - `.` Empty
  - `#` Solid (`DefaultSurface`: `Surface.Sand` when `World == 3`, `Surface.Normal` otherwise **(call)**)
  - `N` Solid with `Surface.Normal`
  - `I` Solid with `Surface.Ice`
  - `S` Solid with `Surface.Sand`
  - `B` Breakable (`hp = Tuning.I.breakableHp`)
  - `H` Empty + ladder
  - `T` Solid (`DefaultSurface`) + ladder
- **Ground grammar (`Ground`, `32` chars):**
  - `=` Solid ground (`DefaultSurface`)
  - `N` Normal ground, `I` Ice ground, `S` Sand ground, `_` Ground gap (pit)
- **Horizontal mirroring (`CanMirror`, default `true`):**
  - Mirrors `Rows` and `Ground` left-to-right, negates `wickStart.x`, spawn `position.x`, `direction`, swaps `LeftWall <-> RightWall` Clinger mounts, and flips `SpikeStrip.col` to `32 - col - width`.
  - **Run selection (`StagePicker` replacing `StageGenerator`):**
    - Boss stages (`StageInWorld == 6`): flat `32 × 14` arena, no stage enemies.
    - Normal stages (`StageInWorld == 1..5`):
      - **Calm road target difficulties** for slots `1..5`: `[1, 2, 2, 3, 3]`.
      - **Hard road target difficulties** for slots `1..5` (Worlds `2..6`, plus World `1` if toggled via debug `HARD`): `[2, 3, 3, 4, 4]` (combined with existing `0.75x` storm clock, world debuff, `2x` score, and `2-of-4` boss reward).
      - Per world, eligible `StageSpec`s for that `World` are deterministically shuffled using `(run.Seed, world, hard)` and 5 distinct stages are chosen without repeating any `StageSpec.Id` within the world: each slot picks an unused stage where `MinStageInWorld <= slot <= MaxStageInWorld` with minimal `|stage.Difficulty - targetDifficulty|` (breaking ties `<= target` on calm road and `>= target` on hard road). Slots `2..5` are then ordered by non-decreasing `Difficulty` (while respecting `MinStageInWorld` / `MaxStageInWorld`) so a world's difficulty never drops on `W-5` even if a tier has fewer stages **(call)**. Each slot applies `Mirror` with 50% probability from `run.StageRng(stage)` when `CanMirror` is `true`.
  - **Retirement of legacy procedural files **(call)**:** Delete `StageLayouts.cs`, `StageRecipe.cs`, `PlacementPatterns.cs`, procedural `WorldRules` placement (keeping `WorldRules.PaintAll` for the debug `SURFACE` cycle and `WorldRules.PlaceSpikes` for the debug `SPIKES` toggle), and procedural generator tests (`StageLayoutTests`, `RecipeTests`, `SignatureTests`, `GuestTests`, `PlacementPatternTests`). Update `StageReport`, `Game.Balance`, `Game.Menus` (`F3` cycles `StageSpec.All`), `RunStructureTests`, `BalanceTests`, `SaveTests`, `WaveTests`, `RollerTests`, `PogoTests`, `SteelCeramicTests`, `SpikeTests`, `BossTests`, and `SurfaceTests`.

### 1.7 `StageValidator` & 42-stage starter library

`StageValidator.Validate(StageSpec spec, bool mirrored)` runs in EditMode tests across every `StageSpec` in `StageSpec.All` (and its mirror when `CanMirror` is `true`):
1. **Metadata & dimensions:** Unique `Id` and `Name`, non-empty `Concept`, `World` in `1..6`, `Difficulty` in `1..4`, `Rows.Length == 14 * Floors`, every row and `Ground` is `32` valid characters.
2. **Unlock matrix:** Enforces Section 1.1 (`1-1` pure, `1-2+` `B`/`_`, `1-3+` Trigloop/Pogo, World `2..6` enemies/surfaces/modifiers, `LargestTier` limits for `Sinker`/`Drifter`).
3. **Wick start & `1.2s` headless fair-start simulation:** Wick starts supported on solid footing (standing or crouched in an intentional crawlspace), clear of solid tiles, and a real `72`-tick (`1.2s` at `60 Hz`) simulation of the stage's awake floor with Wick stationary at his start point confirms zero bubble, roller, or spike overlaps with Wick's hitbox (allowing bubbles directly overhead on a bunker roof while catching any unobstructed early hit).
4. **Entity clearances & counts:** At least `1` required enemy; non-Clinger bubbles start clear of solid tiles/walls and non-overlapping; Clingers have valid mount surfaces; Rollers rest on valid surfaces with diameter headroom; Spikes sit on valid surfaces clear of ladders and other spikes; wave spawns fit inside the arena.
5. **Ladder, gap & baseline reachability (Moss, 0 upgrades, iterative `B` breaching + `all B broken` safety) **(call)**:**
   - Every ladder `H`/`T` span connects two navigable levels cleanly.
   - Ground gaps are `<= 2` columns when bordered by Sand, `<= 3` columns otherwise, never trapping Wick at arena edges, so **Moss** (`7.5 u/s`) can jump them without upgrades (or bypass via ladder).
   - **Iterative breach solvability (starting from `all B intact`):** Using base movement (walk `>= 2` empty rows, crouch-shuffle `1` empty row, base `1.9u` jump, drop, and ladder climb—no `Updraft`, no mandatory wall-jump chains unless an alternate ladder exists) and iteratively breaking any `B` tile reachable by Wick's upward harpoon, downward air harpoon, or adjacent `ChipColumn()` side-chipping, Wick can reach or vertically harpoon every required enemy and every floor with required enemies (supporting sealed `B` vaults and `B`-plugged firing slots).
   - **Over-break safety (`all B broken`):** With every `B` tile removed, no reachable cell traps Wick in an inescapable pit/dead-end, and every required enemy chamber remains reachable or vertically shootable (preventing soft-locks from destroying a `B` bridge that was the sole route to a solid-sealed upper chamber).

**Starter library (`42` stages = `7` per world) **(call)**:**
- **World `1` (always Calm in normal runs):** `1× D1` (`1-1` pure intro), `3× D2`, `3× D3` so all 7 World `1` stages enter normal run rotation (`[1, 2, 2, 3, 3]`) with seed variety.
- **Worlds `2..6`:** `1× D1` (`W-1` signature intro), `2× D2`, `2× D3`, `2× D4` so Calm (`[1, 2, 2, 3, 3]`) and Hard (`[2, 3, 3, 4, 4]`) roads both hit their exact difficulty targets in every slot without repeats.
- Includes Kokan's two specified examples (*Slotted Bunker* and *Clinger Pogo Floor Gauntlet*), crouch crawlspace stages, vertical divider-wall stages, breakable-seal vaults, and 2-floor ladder climbs.

---

## Part 2: Level-Design Agent Skill & `CLAUDE.md`

### 2.1 File layout

```text
CLAUDE.md                                               # Repo-wide rules + pointer to stage-design skill
.claude/skills/bubblewright-level-design/
├── SKILL.md                                            # Core workflow, design pillars, StageSpec recipe & checklist
├── references/
│   └── mechanics-and-archetypes.md                     # Exact unit/bounce/jump math, unlock matrix, 8 puzzle archetypes
└── scripts/
    └── check_stage.py                                  # Fast standalone structural checker (32-col alignment, unlocks, ladders)
```

*(Note: `.claude/skills/` is already registered in both Claude Code and Cursor's `<available_skills>` for this workspace.)*

### 2.2 What `SKILL.md` teaches and enforces

1. **Concept-first design contract:** Before writing ASCII rows, the agent must state:
   - Target `World` (`1..6`), `Difficulty` (`1..4`), and `MinStageInWorld`
   - **Core spatial/puzzle question** (1 sentence stored in `Concept`: e.g., *"Shoot upward through 1-tile bunker slots until XL splits to S and enters"* or *"Hop over patient floor Clinger Pogos and down-shoot them before they leap"*)
   - **Symmetry / visual structure** (bilateral symmetry, mirrored twin chambers, rhythmic columns, or deliberate 2-floor ascent—never random scattered shelves)
   - **Baseline hitless plan for Moss + default Harpoon** (how the slowest hero clears every chamber without upgrades).
2. **Exact spatial grammar & numbers inline:**
   - `32`-char column ruler (`0..31`, center between `15` and `16`), row heights, `2`-row walk clearance vs `1`-row crouch crawlspace, `1`-tile (`1.0u`) S-only filter gaps vs `2`-tile (`2.0u`) M/S gaps vs `3`-tile (`3.0u`) L/M/S gaps.
   - Bubble diameters & bounce peaks (`XL: 3.5u / 13u`, `L: 2.5u / 11u`, `M: 1.6u / 8.5u`, `S: 0.9u / 6u`; `Lead: 0.7x` peak), Wick jump (`1.9u` high, clears `S` and `M` Pogos and big `1.2u` Rollers; max gap `3` cols normal/ice, `2` cols sand).
   - Pacing & pop-count targets per difficulty (`D1` ~10–25 pops, `D2` ~20–40 pops, `D3` ~35–65 pops, `D4` ~55–90 pops, matched to `StageTime` so `pops / second` stays comfortable on touch controls) **(call)**.
3. **Mandatory validation loop:**
   - Run `python3 .claude/skills/bubblewright-level-design/scripts/check_stage.py <StageFile.cs>` to catch row-length/alignment/unlock/ladder mistakes immediately.
   - Run `python3 .claude/skills/unity-csharp-scripting/scripts/check_unity_project.py .` and `unity test . --mode EditMode` (running `StageValidatorTests`) when Unity CLI is available, fixing every failure before finishing.

### 2.3 TDD skill verification (`writing-skills` RED-GREEN-REFACTOR)

- **RED (baseline):** Dispatch fresh subagents *without* the skill on realistic stage-design prompts (e.g., a World 2 floor-Clinger Pogo puzzle and a World 4 2-floor ice/crouch bunker stage) and record exact failure modes (misaligned 31/33-char strings, unlock violations, impossible jumps on sand/ice, breakable soft-locks, random clutter).
- **GREEN & REFACTOR:** Write `SKILL.md` + `references/mechanics-and-archetypes.md` + `scripts/check_stage.py` + `CLAUDE.md` targeting those exact failure modes, re-run fresh subagents *with* the skill, run `StageValidatorTests` on their generated stages, and tighten any loopholes until subagents pass cleanly.

## Left to the builder

- Exact helper overloads on `StageBuilder` (world-unit vs column/row convenience methods) and exact stage names/concepts for the remaining starter stages beyond Kokan's two specified examples, provided all 42 pass `StageValidatorTests` and cover the puzzle archetypes.
- Exact hysteresis/pan speed tuning constants on `Tuning` (`floorPanSpeed = 56f`) and HUD floor indicator label (e.g. `FLOOR 1/2` when `Map.Floors > 1`).
