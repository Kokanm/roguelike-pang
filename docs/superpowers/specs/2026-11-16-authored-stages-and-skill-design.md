# Authored Stages, Puzzle Geometry, and Level-Design Agent Skill

Design agreed with Kokan on 2026-11-16. Items marked **(call)** were settled by the builder and can be changed freely.

Updated 2026-11-16 after review. Kokan decided: at least 12 stages per world; a hard world still opens with the signature intro stage; sand, ice and spikes stay in their own world only; up to 3 storeys (full screens) per stage, with as many shelves inside a screen as fit; bubbles never move between storeys and Wick never goes back down; wall jumps are part of the puzzle rules; guests are planned by the stage author; Lucky bubbles replace a plain bubble at random; the hard-world `+30%` bubble budget is gone and the hard-world boss is harder instead.

## Why

Stages today are built by picking one of 25 shelf layouts (`StageLayouts`) and letting `StageGenerator` spend a bubble budget onto random open coordinates. That makes stages blur together:
- There is no deliberate spatial plan, symmetry, or tactical puzzle behind where walls, platforms, and balloons sit.
- Geometry is locked to platform rows `4` and `9` inside a single `32 × 14` screen, so you cannot build low crouch tunnels, enclosed bunkers with firing slots, vertical divider walls, or multi-storey climbs.
- Future stages will be authored with LLM coding agents, which need both a clean zero-conflict stage format in C# and a dedicated level-design skill (`SKILL.md` + `CLAUDE.md`) grounded in `Bubblewright`'s exact physics and fairness rules.

## Goals

1. **Replace procedural stage generation completely** with hand-crafted, self-discovering C# `StageSpec` stages organized by world (`1..6`) and difficulty tier (`1..4`).
2. **Unlock puzzle geometry in the engine:**
   - Platforms and ceilings at any height down to Wick's crouch height (`1` empty row = `1.0u`).
   - Unbreakable vertical `#` walls of any height (crossed via ladders or intentional wall-jump shafts).
   - Discrete multi-storey stages (`32 × (14 * Storeys)`, `Storeys` in `1..3`) with smooth vertical camera pans when climbing between storeys and dormant upper storeys until reached. Enemies never cross between storeys. Wick only goes up: the way up opens when the current storey is cleared and closes behind him.
   - Floor- and wall-mounted patient `Clinger` spawns so puzzles like "jump over floor Clinger Pogos and shoot downward" work reliably.
3. **Enforce progression and solvability automatically** via an EditMode `StageValidator` test suite covering syntax, world unlocks, clearances, a `1.2s` fair-start simulation, and reachability with base stats and zero upgrades.
4. **Ship a 72-stage starter library** (`12` authored stages per world across Worlds `1..6`, the minimum Kokan set) so both Calm and Hard runs match their target difficulty curve in every slot with zero repeats inside a world, different runs see different stages, and the library showcases the new puzzle archetypes.
5. **Create and TDD-verify the agent level-design skill** (`.claude/skills/bubblewright-level-design/SKILL.md` + repo-root `CLAUDE.md`) so future LLM agents design cohesive, symmetric/intentional, bug-free stages.

## Non-goals

- Redesigning the 6 boss stages (`StageInWorld == 6`): bosses keep their flat `32 × 14` arena and `BossSpec` table. The one boss change is the hard-world scaling in Section 1.8.
- Changing hero base stats (`Character.cs`), upgrades, road-choice UI, or weapon mechanics (beyond making ceiling/row checks respect `map.Rows` and `map.Height` on multi-storey maps).
- Free-scrolling arbitrary-height stages (e.g. `19` rows): verticality uses discrete `14`-row stories so every active storey frames cleanly on mobile (`480 × 270`, `15 PPU`).

## Anti-goals

- **"Random balloons on shelves" stages:** every stage must be built around an explicit spatial/combat idea (`Concept`), deliberate symmetry or structure, and purposeful enemy placement.
- **Upgrade-gated or hero-gated stages:** no stage may require `Updraft` (double jump), extra cables, crate weapons, or a fast hero. Every stage must be clearable hitless with **Moss** (`7.5 u/s` walk) and **Flint** (`16 u/s` harpoon), default Harpoon, and zero upgrades. This must also hold under the hard-road debuffs (Heavy Boots: Moss at about `6.4 u/s`; Frayed Cable: Flint at `12.8 u/s`), because `D4` stages only appear in hard worlds. Wall jumps and chained wall jumps are a base move, so a stage may require them.
- **Pixel-perfect touch precision:** mobile touch controls come first; jumps, bunker slots, and safe lanes need comfortable margins rather than frame-perfect inputs.
- **Breakable soft-locks or off-screen ambushes:** breaking `B` tiles *may* be required to breach vaults or open firing lanes, but **over-breaking** (destroying every `B` tile on the map) must never trap Wick in a pit or strand a required enemy out of reach/line-of-fire (e.g. a `B` bridge must never be the sole route to a solid-sealed upper room). Likewise, enemies on unseen storeys must never hit or get sniped blindly across storey boundaries.

## Constraints

- Unity `6000.6.7f1` (2D URP), `480 × 270` reference resolution at `15 PPU`, custom kinematic collision (`StageMap`), `Game.Update` deterministic tick order (so hit-stop, slow-mo, and pause freeze everything).
- Saved runs: `StagePicker` must deterministically pick the same `StageSpec` and horizontal mirror flag from `(seed, stage, hardWorld)`. So that adding stages to the library never changes a run that is already saved, `SaveData` also stores the picked stage `Id`s and mirror flags of the current world (`version` goes to `2`; a save with an unknown `Id` falls back to a fresh pick for that slot).
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

Kokan picked **Approach 1**, and chose to deliver **both** the engine/stage replacement and the agent `SKILL.md` + `CLAUDE.md` in order: **Part 1 (Engine + `StageSpec` framework + 72 starter stages)** first, followed by **Part 2 (`SKILL.md` + `CLAUDE.md`, verified with subagents against the real validator)**. Build order agreed after review: engine and framework, then the `12` World `1` stages for Kokan to play-test, then Part 2 (the skill, with those stages as its worked examples), then the other `60` stages authored with the skill.

---

## Part 1: Engine & Authored Stage Framework

### 1.1 Unlock progression across worlds

- **Stage `1-1` (`World == 1 && MaxStageInWorld == 1`):** Pure introduction to movement, jumping, ladders, walls, and `Gloop` splitting. **No** breakable blocks (`B`), **no** ground gaps (`_`), **no** other enemy kinds or modifiers.
- **Stage `1-2` onward (`World >= 2 || MinStageInWorld >= 2`):** Breakable blocks (`B`) and ground gaps (`_`) are unlocked.
- **Stage `1-3` onward (`World >= 2 || MinStageInWorld >= 3`):** `Trigloop` and plain `Pogo`s (`Gloop`, `Trigloop`, `Lead` in W2+) are unlocked.
- **World `2` (Forest) onward:** `Clinger` (signature), `Lead`, `Drifter`, `Roller` (pearl). `Lucky` is never authored: see Section 1.6.
- **World `3` (Desert) onward:** `Sinker` (signature), `Steel` (steel rollers & steel Pogos), `Ceramic` (bubbles up to tier `M`, big rollers). **World `3` only:** `Sand` surface.
- **World `4` (Winter) onward:** `Frost` (signature). **World `4` only:** `Ice` surface (a `Frost` guest in a later world still makes its own temporary ice).
- **World `5` (Catacombs) onward:** `Ghost` (signature).
- **World `6` (Lava):** `Scorch` (signature). **World `6` only:** retracting `SpikeStrip`s.
- **Hazards stay in their own world (Kokan):** `Sand` only in World `3`, `Ice` only in World `4`, `SpikeStrip`s only in World `6`. The validator rejects them anywhere else.
- **Guest rule in authored stages (guests are planned, never random):** A stage in World `W >= 2` may use classic kinds (`Gloop`, `Trigloop`, `Lead`), its world's signature enemy, `Drifter`, and at most one earlier world's signature enemy (`< W`) that the author places on purpose and names in `Concept`; stage `W-1` (`Difficulty == 1`) sticks to that world's signature enemy + classic kinds so every world still opens with a clean showcase of its own mechanic. The old random guest roll (`guestChance`) is removed.

**Words:** a *storey* is one full `32 × 14` screen of a stage. *Floor* always means the surface underfoot (`FloorBelow`, floor Clinger). A storey can hold as many shelves as fit.

### 1.2 Multi-storey `StageMap` (`32 × (14 * Storeys)`)

- `StageMap.Cols = 32` and `StageMap.StoreyRows = 14` remain constants. `StageMap` gains instance properties `Storeys` (`>= 1`, default `1`), `Rows` (`14 * Storeys`), and `Height` (`(float)Rows`). `StageMap.Flat(int storeys = 1)` defaults to `1` storey (`14` rows). `Storeys` is at most `3`. Inside one storey a stage may have as many shelves as fit.
- All tile, ceiling, bounce, harpoon, and weapon bounds checks replace hardcoded `StageMap.Rows` / `Tuning.arenaHeight` with `map.Rows` / `map.Height` (keeping `Tuning.arenaHeight = 14f` as the height of one screen/storey).
- `StageView` resizes its arena `Texture2D` / `Sprite` dynamically to `480 × (210 * map.Storeys)` whenever `map.Storeys` changes.

### 1.3 Discrete storey camera pan, UI strip & storey wake-up

- **Active storey tracking (`Game`):**
  - Storey `f` spans world `y` in `[14f * f, 14f * (f + 1))`.
  - Wick only goes up. `activeStorey` advances to `f` once `wick.FeetY >= 14f * f` and never drops. Each storey boundary is solid for Wick except its ladder hatch(es). A hatch is shut (solid, drawn closed) while any required enemy of the storey below is alive, opens when that storey is cleared, and shuts again behind Wick once `activeStorey` advances, so he cannot fall or climb back down **(call: the hatch is how "cannot go back down" is enforced)**.
  - Camera target offset `targetPanY = activeStorey * 14f`. `camPanY` moves toward `targetPanY` at `56 u/s` (~`0.25s` per storey) using unscaled/play `dt`, snapped to `1 / 15u` pixels in `UpdateCamera`. On `StartStage`, `activeStorey` and `camPanY` snap immediately to Wick's starting storey.
- **Background & thumb-control strip on upper storeys **(call)**:**
  - `Stage Background` moves vertically with `camPanY` so the world's sky and side-wall pillars always frame the current `32 × 14` view.
  - `TouchControlsView` offsets its `stripMid` by `camPanY` so thumb controls stay anchored to the bottom `4u` strip of the screen on every storey.
  - The HUD is drawn from the arena's top corners in world coordinates (`Game.cs`, `WorldToGui(... T.arenaHeight)`), so it adds `camPanY` too and stays at the top of the screen on every storey.
  - When `camPanY > 0f`, a `FloorStripMask` sprite (`Layer.UI`, order `-2`, `y = camPanY - 4f .. camPanY`) paints the `6 px` floor cap and `theme.strip` fill over the lower storey's top rows so thumb controls never overlap visible tiles below.
- **Dormant storeys & cross-storey boundaries **(call)**:**
  - `bool[] awakeStoreys` (`Length = Map.Storeys`): Wick's starting storey starts awake; storey `f` wakes permanently the moment `activeStorey == f`. While `awakeStoreys[f]` is `false`, bubbles and rollers on storey `f` render in place at their starting positions without ticking and cannot hurt Wick (so Sinkers don't flatten out, Scorches don't litter flames, and non-patient Clingers don't time out before Wick arrives). The storm clock running out wakes nothing: it only affects awake storeys.
  - Upward harpoons and ranged weapon shots clamp their max ceiling to `Mathf.Min(tileCeiling, (shooterStorey + 1) * 14f)` so shots through open ladder shafts cannot snipe dormant enemies on unseen storeys above; downward air shots clamp their floor to `Mathf.Max(tileFloor, shooterStorey * 14f)` unless hitting a tile at `shooterStorey * 14f - 1`.
  - **No enemy crosses a storey boundary (Kokan).** Every bubble, roller and Pogo treats the top and bottom of its home storey (`[homeStorey * 14f, (homeStorey + 1) * 14f]`) as a solid ceiling and floor, open hatch or not. `Ghost` bubbles (`IgnoresTiles = true`) use the same clamp instead of `[0, map.Height]`. Children inherit their parent's `homeStorey`.
  - Ceiling wave markers (`Game.Waves`) belong to one storey (`waveStorey`, defaulting to `Map.Storeys - 1`), spawn at the top of that storey, and are only called while it is the active storey. A storey counts as cleared only when its waves have arrived and been popped.

### 1.4 Crouch crawlspaces & vertical divider walls (`Wick.cs`)

- **Dimensions:** Standing Wick is `1.0u` wide × `1.467u` (`22 px`) tall -> needs **2 empty rows (`2.0u`)** to walk upright. Crouching Wick (`crouchHeightScale = 0.6f`) is **`0.88u` (`13.2 px`) tall** -> fits inside **1 empty row (`1.0u`)**.
- **Horizontal collision fix:** `Wick.BodyBox(x, y)` uses `HitboxHeight` (`0.88u` when `Crouching`, `1.467u` when standing) so Wick can crouch-shuffle (`35%` run speed) into and through `1`-row crawlspaces.
- **Low-ceiling crouch lock:** When `Mode == Mode.Ground` and the lowest solid underside above either edge of his crouched box (`map.CeilingAbove(x ± half, pos.y + T.hitbox.y * T.crouchHeightScale, out _)`, measured from the crouched top because `CeilingAbove` skips tiles below the height it is given) is less than `T.hitbox.y - 1e-4f` above his feet, `Crouching` remains `true` even if the player releases Down, and jumping is disabled until that clearance is at least `T.hitbox.y`. The lock also holds while he is knocked back (`knockTimer > 0f`), so a hit inside a tunnel never stands him up into the roof.
- **Firing from a crouch crawlspace **(call)**:** While `Crouching`, `LauncherTip.y` clamps to `Mathf.Min(T.launcherTip.y * T.crouchSquash.y, HitboxHeight - T.Pixel)` (`~0.81u`, inside row `0` relative to Wick's feet). Under a solid `#` ceiling at row `+1`, upward harpoons immediately stop at `y = feetY + 1f`; under a `1`-tile gap `.` or breakable `B` at row `+1`, Wick can fire straight up through the slot or chip the `B` block overhead.
- **Vertical unbreakable walls & ladders:** `#` tiles may form vertical walls at any column and height, including full-height dividers (`rows 0..13`) or bunkers with roofs. `H` (empty ladder) and `T` (solid + ladder) may span any contiguous vertical range `>= 2` rows, including across storey boundaries (`r = 13..14`). Wick can chain wall jumps up any shaft with two facing walls and wall-jump off a single wall onto a ledge, so a wall is only a real divider when it reaches a ceiling, or when it is meant to be climbed.

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

- **Tile grammar (`Rows`, `14 * Storeys` strings of `32` chars, top row first):**
  - `.` Empty
  - `#` Solid (`DefaultSurface`: `Surface.Sand` when `World == 3`, `Surface.Normal` otherwise **(call)**)
  - `N` Solid with `Surface.Normal`
  - `I` Solid with `Surface.Ice`
  - `S` Solid with `Surface.Sand`
  - `I` is valid only in World `4` and `S` only in World `3` (hazards stay in their own world).
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
      - **Hard road target difficulties** for slots `1..5` (Worlds `2..6`, plus World `1` if toggled via debug `HARD`): `[1, 3, 3, 4, 4]`. Slot `1` is still the world's `D1` signature intro, on both roads (Kokan). The hard road keeps the existing `0.75x` storm clock, world debuff, `2x` score, and `2-of-4` boss reward, and gets a harder boss (Section 1.8). The `+30%` bubble budget (`hardBudgetScale`) is removed.
      - Per world, eligible `StageSpec`s for that `World` are deterministically shuffled using `(run.Seed, world, hard)` and 5 distinct stages are chosen without repeating any `StageSpec.Id` within the world: each slot picks an unused stage where `MinStageInWorld <= slot <= MaxStageInWorld` with minimal `|stage.Difficulty - targetDifficulty|` (breaking ties `<= target` on calm road and `>= target` on hard road). Slots `2..5` are then ordered by non-decreasing `Difficulty` (while respecting `MinStageInWorld` / `MaxStageInWorld`) so a world's difficulty never drops on `W-5` even if a tier has fewer stages **(call)**. Each slot applies `Mirror` with 50% probability from `run.StageRng(stage)` when `CanMirror` is `true`. If no unused stage fits a slot's `MinStageInWorld`/`MaxStageInWorld`, the slot takes the unused stage of that world with the nearest `Difficulty` regardless of those bounds; a library test asserts this fallback is never needed for the shipped stages.
      - **Lucky bubbles (Kokan):** never authored. From stage `2` of the run, with chance `Tuning.luckyChance` drawn from `run.StageRng(stage)`, one starting bubble that is a plain `Gloop`, `Trigloop` or `Lead` (not a Pogo, not ceramic, not a Clinger or other special kind) becomes a `Lucky` of the same tier, position and direction **(call: which bubble is picked at random among the eligible ones; a stage with none gets no Lucky)**. The validator's fair-start simulation also runs with each eligible bubble swapped.
  - **Retirement of legacy procedural files **(call)**:** Delete `StageLayouts.cs`, `StageRecipe.cs`, `PlacementPatterns.cs`, procedural `WorldRules` placement (keeping `WorldRules.PaintAll` for the debug `SURFACE` cycle and `WorldRules.PlaceSpikes` for the debug `SPIKES` toggle), and procedural generator tests (`StageLayoutTests`, `RecipeTests`, `SignatureTests`, `GuestTests`, `PlacementPatternTests`). Update `StageReport`, `Game.Balance`, `Game.Menus` (`F3` cycles `StageSpec.All`), `RunStructureTests`, `BalanceTests`, `SaveTests`, `WaveTests`, `RollerTests`, `PogoTests`, `SteelCeramicTests`, `SpikeTests`, `BossTests`, and `SurfaceTests`. Also touches `HardWorldTests`, `KindFoundationTests` and `DebugPanelLayout.cs`, which use the generator today. Removes `hardBudgetScale`, `guestChance`, `guestChanceHard`, `guestBudgetShare` and `guestMaxBubbles` from `Tuning`; `luckyChance` stays.

### 1.7 `StageValidator` & 72-stage starter library

`StageValidator.Validate(StageSpec spec, bool mirrored)` runs in EditMode tests across every `StageSpec` in `StageSpec.All` (and its mirror when `CanMirror` is `true`):
1. **Metadata & dimensions:** Unique `Id` and `Name`, non-empty `Concept`, `World` in `1..6`, `Difficulty` in `1..4`, `Rows.Length == 14 * Storeys`, every row and `Ground` is `32` valid characters.
2. **Unlock matrix:** Enforces Section 1.1 (`1-1` pure, `1-2+` `B`/`_`, `1-3+` Trigloop/Pogo, World `2..6` enemies/modifiers, `Sand` only in World `3`, `Ice` only in World `4`, `SpikeStrip`s only in World `6`, no authored `Lucky`, at most one planned guest, `LargestTier` limits for `Sinker`/`Drifter`).
3. **Wick start & `1.2s` headless fair-start simulation:** Wick starts supported on solid footing (standing or crouched in an intentional crawlspace), clear of solid tiles, and a real `72`-tick (`1.2s` at `60 Hz`) simulation of the stage's awake storey with Wick stationary at his start point confirms zero bubble, roller, or spike overlaps with Wick's hitbox (allowing bubbles directly overhead on a bunker roof while catching any unobstructed early hit).
4. **Entity clearances & counts:** At least `1` required enemy; non-Clinger bubbles start clear of solid tiles/walls and non-overlapping; Clingers have valid mount surfaces; Rollers rest on valid surfaces with diameter headroom; Spikes sit on valid surfaces clear of ladders and other spikes; wave spawns fit inside the arena.
5. **Ladder, gap & baseline reachability (Moss, 0 upgrades, iterative `B` breaching + `all B broken` safety) **(call)**:**
   - Every ladder `H`/`T` span connects two navigable levels cleanly.
   - Ground gaps are `<= 1` column when bordered by Sand (all of World `3`), `<= 2` columns otherwise, never trapping Wick at arena edges, so **Moss** with Heavy Boots can jump them without upgrades (or bypass via ladder). Kokan set this cap on 2026-11-17: the reach check jumps column centre to column centre (`3.77u`, `2.7u` on sand), so the earlier `3` / `2` could never be crossed.
   - **Iterative breach solvability (starting from `all B intact`):** Using base movement (walk `>= 2` empty rows, crouch-shuffle `1` empty row, base `1.9u` jump, drop, and ladder climb—wall jump and chained wall jumps as tuned in `Tuning`; no `Updraft`) and iteratively breaking any `B` tile reachable by Wick's upward harpoon, downward air harpoon, or adjacent `ChipColumn()` side-chipping, Wick can reach or vertically harpoon every required enemy and every storey with required enemies (supporting sealed `B` vaults and `B`-plugged firing slots).
   - **Over-break safety (`all B broken`):** With every `B` tile removed, no reachable cell traps Wick in an inescapable pit/dead-end, and every required enemy chamber remains reachable or vertically shootable (preventing soft-locks from destroying a `B` bridge that was the sole route to a solid-sealed upper chamber).
6. **Storeys:** `Storeys` in `1..3`; every storey has at least `1` required enemy; every storey below the top has a hatch Wick can reach once the storey is cleared; no enemy starts within `3u` of where Wick arrives through a hatch, and the `1.2s` fair-start simulation is repeated for each arrival point.
7. **No unreachable pockets:** every empty region that a bubble or one of its children can enter is one Wick can reach or shoot into, so a stage can never be left with an enemy nobody can hit.
8. **Difficulty and pace:** the stage's pop count sits in its tier's band (Section 2.2) and `pops / StageTime` stays under the touch-control limit, so `Difficulty` is checked rather than only declared.
9. **Slowest baseline:** checks 3 to 7 use Moss with Heavy Boots (and sand speed in World `3`), so hard-world debuffs cannot make a stage unclearable.

**Starter library (`72` stages = `12` per world; `12` is the minimum Kokan set) **(call: the split per tier)**:**
- **World `1` (always Calm in normal runs):** `1× D1` (`1-1` pure intro), `5× D2`, `6× D3`, so each run draws `2` of `5` and `2` of `6`.
- **Worlds `2..6`:** `1× D1` (`W-1` signature intro, played on both roads), `3× D2`, `4× D3`, `4× D4`. Calm (`[1, 2, 2, 3, 3]`) draws `2` of `3` `D2` and `2` of `4` `D3`; Hard (`[1, 3, 3, 4, 4]`) draws `2` of `4` `D3` and `2` of `4` `D4`.
- Includes Kokan's two specified examples (*Slotted Bunker* and *Clinger Pogo Floor Gauntlet*), crouch crawlspace stages, vertical divider-wall stages, wall-jump shafts, breakable-seal vaults, 2-storey ladder climbs, and at least one 3-storey climb.

### 1.8 Harder hard-world bosses

The hard road no longer adds bubble budget. Instead the boss of a hard world is harder (Kokan): more health, faster movement, faster attacks. Three new `Tuning` fields **(call: the values)**: `hardBossHpScale = 1.5f`, `hardBossSpeedScale = 1.2f`, `hardBossRateScale = 1.25f`. They multiply the boss's health (`bossBaseHp + bossHpPerWorld * (world - 1)`, rounded up), its movement speed, and every attack rate, and stack with `bossEnrageScale` and `bossStormScale`. `BossTests` and `HardWorldTests` pin the three effects and that a calm-world boss is unchanged.

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
    └── check_stage.py                                  # Fast structure-only checker (32 columns, row count, valid characters)
```

*(Note: `.claude/skills/` is already registered in both Claude Code and Cursor's `<available_skills>` for this workspace.)* `.claude/` is ignored by `.gitignore` today, so the skill, and `unity-csharp-scripting/scripts/check_unity_project.py` that every task runs, exist only on Kokan's laptop. Part 2 adds exceptions to `.gitignore` for both so cloud threads and other machines get them.

The number tables in `references/mechanics-and-archetypes.md` are generated from `Tuning` by an editor menu item (`Bubblewright > Write Level-Design Reference`), and an EditMode test fails when the file is out of date, so the skill never quotes stale numbers.

### 2.2 What `SKILL.md` teaches and enforces

1. **Concept-first design contract:** Before writing ASCII rows, the agent must state:
   - Target `World` (`1..6`), `Difficulty` (`1..4`), and `MinStageInWorld`
   - **Core spatial/puzzle question** (1 sentence stored in `Concept`: e.g., *"Shoot upward through 1-tile bunker slots until XL splits to S and enters"* or *"Hop over patient floor Clinger Pogos and down-shoot them before they leap"*)
   - **Symmetry / visual structure** (bilateral symmetry, mirrored twin chambers, rhythmic columns, or deliberate 2-storey ascent—never random scattered shelves)
   - **Baseline hitless plan for Moss + default Harpoon** (how the slowest hero clears every chamber without upgrades).
2. **Exact spatial grammar & numbers inline:**
   - `32`-char column ruler (`0..31`, center between `15` and `16`), row heights, `2`-row walk clearance vs `1`-row crouch crawlspace, `1`-tile (`1.0u`) S-only filter gaps vs `2`-tile (`2.0u`) M/S gaps vs `3`-tile (`3.0u`) L/M/S gaps.
   - **Filter gaps, measured (Task 5, `FilterGapTests`).** Gloops dropped onto a roof from 24 start points for 60 s each (offline model of the same bubble maths; Kokan's EditMode run logs the in-engine numbers): a `1`-tile slot never lets `M` or `L` through and lets `S` through only slowly (one slot: 7 of 24 in 60 s; three slots: 24 of 24, median about 26 s), so a `1`-tile slot is a **firing slot first and a drop slot second**. A `2`-tile hole never lets `L` through and lets `M` and `S` through (three holes: median about 14 s for `M`), so `2` tiles is the **M/S sieve**. A `3`-tile hole lets `L` through (median about 5 s with three holes), so it filters nothing above `L`. *Slotted Bunker* keeps its `1`-tile slots as firing slots; *Trigloop Sieve* uses two `2`-tile holes. The floor `M` Clinger Pogo hop with Moss is logged by `MossGetsPastATriggeredFloorPogo`; World 2 stages pick `S` Pogos or a wider approach from those numbers.
   - Bubble diameters & bounce peaks (`XL: 3.5u / 13u`, `L: 2.5u / 11u`, `M: 1.6u / 8.5u`, `S: 0.9u / 6u`; `Lead: 0.7x` peak), Wick jump (`1.9u` high, clears `S` and `M` Pogos and big `1.2u` Rollers; max gap `2` cols normal/ice, `1` col sand, Kokan 2026-11-17).
   - Pacing & pop-count targets per difficulty (`D1` ~10–25 pops, `D2` ~20–40 pops, `D3` ~35–65 pops, `D4` ~55–90 pops, matched to `StageTime` so `pops / second` stays comfortable on touch controls) **(call)**.
3. **Mandatory validation loop:**
   - Run `python3 .claude/skills/bubblewright-level-design/scripts/check_stage.py <StageFile.cs>` to catch row-length, row-count and character mistakes immediately. It checks structure only: every game rule (unlocks, gaps, ladders, reachability) lives in `StageValidator`, so the rules exist in one place.
   - Run `python3 .claude/skills/unity-csharp-scripting/scripts/check_unity_project.py .` and `unity test . --mode EditMode` (running `StageValidatorTests`) when Unity CLI is available, fixing every failure before finishing.

### 2.3 TDD skill verification (`writing-skills` RED-GREEN-REFACTOR)

- **RED (baseline):** Dispatch fresh subagents *without* the skill on realistic stage-design prompts (e.g., a World 2 floor-Clinger Pogo puzzle and a World 4 2-storey ice/crouch bunker stage) and record exact failure modes (misaligned 31/33-char strings, unlock violations, impossible jumps on sand/ice, breakable soft-locks, random clutter).
- **GREEN & REFACTOR:** Write `SKILL.md` + `references/mechanics-and-archetypes.md` + `scripts/check_stage.py` + `CLAUDE.md` targeting those exact failure modes, re-run fresh subagents *with* the skill, run `StageValidatorTests` on their generated stages, and tighten any loopholes until subagents pass cleanly.

## Left to the builder

- Exact helper overloads on `StageBuilder` (world-unit vs column/row convenience methods) and exact stage names/concepts for the remaining starter stages beyond Kokan's two specified examples, provided all 72 pass `StageValidatorTests` and cover the puzzle archetypes.
- Exact hysteresis/pan speed tuning constants on `Tuning` (`storeyPanSpeed = 56f`) and HUD floor indicator label (e.g. `FLOOR 1/2` when `Map.Storeys > 1`).
