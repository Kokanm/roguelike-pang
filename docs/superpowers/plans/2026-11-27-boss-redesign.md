# Boss Redesign Implementation Plan

**Goal:** Replace the six placeholder bosses (one `BossSpec` table, one sliding-block behaviour) with six authored
bosses that each have their own arena, phases, cores and puzzle, as approved in
`docs/superpowers/specs/2026-11-27-boss-redesign-design.md` (read it first; it is the source of truth).

**Architecture:** A shared boss kit first (one file per boss found by reflection, phases, cores and armour, a
segmented bar, phase breaks, bullet patterns and tells, authored boss arenas with up to 5 storeys, arena events,
balloon pipes). A legacy adapter keeps today's six bosses running until each world's new boss lands, so every task
ships a playable game and Tasks 5 to 9 can run in parallel threads. Sootjack (World 1) is built first and play-tested
by Kokan before the other five start.

**Tech stack:** Unity `6000.6.7f1` (2D URP), C# 9, NUnit EditMode tests, the `unity` CLI on Kokan's MacBook.

## Global constraints

- Follow `CLAUDE.md`: no physics engine, every number in `Tuning` (a `Bosses` header, plus one header per boss, for
  example `[Header("Boss: Sootjack")]`), no image or audio files (`PlaceholderArt`, `Sfx`), content discovers itself,
  commit every `.meta`, keep package-generated `ProjectSettings` changes out of commits.
- Boss arenas are `StageSpec`s and must pass `StageValidator`. Read `.claude/skills/bubblewright-level-design/SKILL.md`
  before writing one, and add the boss-arena rules to the skill in Task 3.
- Hit-stop, slow motion and pause must freeze bosses, bullets, tells and arena events: everything ticks from
  `Game.Update` in the existing order, never from its own `Update`.
- Art follows the spec's look rules and the Boss Sheet: world palette plus steel, cores are cream with an accent ring,
  no eyes, 1 px ink outline. Draw at real size; the sheet's pixel art is the reference, not a pixel-exact target.
- Tests: add EditMode tests for every rule that does not need a running scene (pure functions on boss state, patterns,
  arena events, the validator). Cloud threads have no Unity: run the Python checks there and say which Unity tests
  still need a run on the laptop.
- Checks after every C# task:
  `python3 .claude/skills/unity-csharp-scripting/scripts/check_unity_project.py .`,
  `unity test . --mode EditMode --output Temp/editmode.xml`, and for arenas
  `python3 .claude/skills/bubblewright-level-design/scripts/check_stage.py Assets/Bubblewright/Scripts/Stage/Stages`.
- One PR per task, on its own branch.

## Order

1. Task 1 (boss kit core) → Task 2 (patterns and tells) and Task 3 (arena kit), which can run in parallel →
   Task 4 (Sootjack) → **Kokan play-tests Sootjack** → Tasks 5 to 9 in parallel, one thread per boss → Task 10 (cleanup).

---

### Task 1: Boss kit core

**Files:** `Scripts/Bosses/` (new `BossDef.cs`, `BossPart.cs`, `BossPhase.cs`, `LegacyBoss.cs`; rework `Boss.cs`,
`BossFight.cs`), `Scripts/Core/Game.Boss.cs`, `Scripts/Core/Tuning.cs`, weapon files that damage the boss
(`ArcCoil`, `DoubleHarpoon`, `VeeHarpoon`, `SkewerHarpoon`, `Shot`, `GasFlask`, `Skyrocket`, `LampLance`, `LampPost`,
`LamplighterTorch`, `PopMine`, `RicochetDisc`, `TrimmerBlade`, `WeaponWorld`), `Tests/Editor/BossTests.cs`.

**Build:**
- `BossDef`: abstract class, one sealed `[Preserve]` subclass per boss in `Scripts/Bosses/Defs/W1..W6/`, found by
  reflection like `StageSpec` and `WeaponSpec`. It names the world, the boss name, the arena stage id, the phase list,
  health per phase, the fight's clock, and builds the boss's parts and behaviour. `BossDef.ForWorld(world)` returns the
  new boss for a world, or the `LegacyBoss` adapter (today's `BossSpec` behaviour) when the world has none yet.
- `BossPart`: a named rectangle on the boss, either armour or a core, with `Open` (cores) and its own optional health
  (arms, bellows, valves). Parts move with the boss and can be added or removed per phase.
- Damage goes through one call, `BossFight.Hit(boss, Rect area, int amount, Vector2 at)`: the first part the area
  touches decides it. A core that is open takes the damage; armour or a closed core clinks (spark `fx`, a short
  high `Sfx`) and the shot or harpoon is used up. Every weapon's boss hit goes through it instead of `Boss.Body`.
- Phases: `BossPhase` has `Enter`, `Tick`, `Exit`. When a phase's health segment (or its parts) is gone: stagger for
  `bossStaggerTime`, clear every boss bullet, run the phase's arena change, drop a weapon crate (`SpawnCrate`), and on
  the calm road drop a heart `Pickup` when Wick is below his maximum. Never a heart on the hard road.
- HUD: `DrawBossBar` draws one segment per phase with a 2 px gap; the current segment drains; part bars (arms) show
  as small bars over the parts.
- Hard road: keep `hardBossHpScale`, `hardBossSpeedScale`, `hardBossRateScale`; add `BossDef.Hard` so each boss can add
  its extra moves.
- Tuning: `bossTime` 240, `bossStaggerTime` 1.5, `bossClinkStopFrames`, per-boss clock override (the Crucible Forge
  uses 300).
- Debug page: keep AIMED, BOUNCY, BALLOON for the legacy boss; add NEXT PHASE and OPEN CORES.

**Tests:** a closed core and armour take no damage and use the shot up; an open core takes it; a phase ends when its
segment is empty and the next one starts after the stagger; bullets clear on a phase break; a heart drops on the calm
road and never on the hard road; the bar has one segment per phase; `BossDef.ForWorld` returns the legacy boss for a
world with no new boss; every discovered `BossDef` has a unique world.

**Done when:** the game plays exactly as today with the legacy bosses (now with a segmented bar of 1 segment), and all
tests pass on the laptop.

**Prompt:**

```
Read CLAUDE.md, docs/superpowers/specs/2026-11-27-boss-redesign-design.md and Task 1 of
docs/superpowers/plans/2026-11-27-boss-redesign.md. Build Task 1 (the boss kit core) exactly as the plan says, on a new
branch boss/01-kit-core. Keep today's six bosses working through the LegacyBoss adapter so the game is unchanged until
each new boss lands. Route every weapon's boss damage through BossFight.Hit. Put every number in Tuning. Add the
EditMode tests listed in the task first, watch them fail, then make them pass. Run the checks from CLAUDE.md and open a
PR to main that lists what changed and which tests ran.
```

---

### Task 2: Bullet patterns and tells

**Files:** `Scripts/Bosses/Bullet.cs`, new `Scripts/Bosses/Patterns.cs`, `Scripts/Bosses/Tells.cs`,
`Scripts/Art/PlaceholderArt.cs` (bullet, wave, missile, shell sprites), `Scripts/Core/Game.Boss.cs`,
`Tests/Editor/BossPatternTests.cs`.

**Build:** the patterns in the spec's table, as pure functions that return bullet launches (position, velocity,
kind) so they can be tested without a scene: `Aimed`, `Fan`, `Burst`, `RingWithGap(centre, count, gapStart,
gapWidth)`, `CurtainWithLane(storeyY, laneCol, laneWidth)`, `Spiral(arms, step)`, `GroundWave(surface, x, dir)`,
`ArcingShell(from, target, onLand)`, `HomingMissile(from, turnRate, life)`, `SweepStream(x0, x1, speed)`. New bullet
kinds: ground wave (rides a surface, stops at its edge or a wall), arcing shell (gravity, runs an `onLand` callback),
homing missile, stream column. Tells: edge glow, dust column, lit lane, red shadow, each at least `bossTellMin` (0.5 s)
before its attack, all ticking from `Game.Update`.

**Tests:** a ring with a gap leaves exactly the gap empty; a curtain never covers its lane; a spiral's arms are evenly
spaced; a ground wave stops at a platform's edge and at a wall; a homing missile never turns faster than its rate; a
tell always starts at least 0.5 s before its attack.

**Prompt:**

```
Read CLAUDE.md, docs/superpowers/specs/2026-11-27-boss-redesign-design.md and Task 2 of
docs/superpowers/plans/2026-11-27-boss-redesign.md. Task 1 is merged. Build Task 2 (bullet patterns and tells) on a new
branch boss/02-patterns. Patterns are pure functions returning launches so they can be unit tested; bullets and tells
tick from Game.Update. Draw the new bullet sprites in PlaceholderArt following the Boss Sheet
(https://claude.ai/artifact/QBqkbCMeTjEgMTZ1xrGXkb). Tests first, then code. Run the checks from CLAUDE.md and open a PR.
```

---

### Task 3: Arena kit (authored boss arenas, 5 storeys, arena events, balloon pipes)

**Files:** `Scripts/Stage/StageSpec.cs`, `StageMap.cs`, `StageLayout.cs`, `StageValidator.cs`, `StagePicker.cs`,
`Scripts/Core/Game.Storeys.cs`, `Game.Boss.cs`, new `Scripts/Core/Game.Arena.cs`,
`Scripts/Stage/Stages/Bosses/` (one placeholder arena per world: today's flat floor), the level-design skill
(`SKILL.md`, `check_stage.py`), `Tests/Editor/` (`BossArenaTests.cs`, `StageValidatorTests.cs`).

**Build:**
- `StageSpec.IsBossArena`: boss arenas are never picked for ordinary slots; `StagePicker` uses the arena named by the
  world's `BossDef` instead of `Arena("Boss")`.
- Up to 5 storeys for boss arenas only (`StageMap`, `StageSpec.BlankRows`, `StageValidator`; ordinary stages stay at 1
  to 3).
- On a boss stage a hatch opens when the boss's phase ends (`BossDef` says which phase opens which hatch), not when the
  storey's bubbles are cleared. Wick still never goes back down.
- Arena events, all through `Game.Arena.cs` and all undone at the next stage: `BossBreakTile(c, r)` (any tile, debris
  `fx`), `AddTempTile(c, r, kind, surface, life)` (ice or sand blocks that crumble or melt), `OpenFloorGap(c0, c1)`,
  `RisingHazard` (a lava line that rises at a set speed and stops at a set height; touching it hurts and throws Wick
  up), `SetSurface(c, r, Surface.Ice)`.
- Balloon pipes: `PipeSpot.TopLeft`, `TopRight`, `TopMiddle`; a short pipe drawn at the top of the active storey, a
  0.5 s flash, then a Gloop of the given tier.
- Validator rules for boss arenas: the boss's start spot is inside the arena; Wick can reach every core position that
  the def lists as reachable from the floor or a platform with the base moves; every hatch is reachable.
- Skill: add a "Boss arenas" section (what differs from stages, the 5-storey cap, hatches opened by phases).

**Tests:** a 5-storey boss arena parses and validates; a 4-storey ordinary stage still fails validation; a hatch on a
boss stage stays shut until its phase ends; `BossBreakTile` breaks a solid tile and a shot still cannot; a temp tile
disappears after its life; the rising hazard stops at its stop height; a pipe Gloop appears after its flash.

**Prompt:**

```
Read CLAUDE.md, .claude/skills/bubblewright-level-design/SKILL.md,
docs/superpowers/specs/2026-11-27-boss-redesign-design.md and Task 3 of
docs/superpowers/plans/2026-11-27-boss-redesign.md. Task 1 is merged (Task 2 may run in parallel). Build Task 3 (arena
kit) on a new branch boss/03-arena-kit. Boss arenas are StageSpecs flagged IsBossArena and may have up to 5 storeys;
ordinary stages stay at 1 to 3. Hatches on boss stages open when a phase ends. Add the arena events and balloon pipes,
update the level-design skill with a Boss arenas section, tests first. Run the checks from CLAUDE.md and open a PR.
```

---

### Task 4: Sootjack (World 1), then Kokan's play-test

**Files:** `Scripts/Bosses/Defs/W1/Sootjack.cs`, `Scripts/Stage/Stages/Bosses/W1_SootjackRoof.cs`,
`Scripts/Art/` (Sootjack painter), `Tuning` (`[Header("Boss: Sootjack")]`), `Tests/Editor/SootjackTests.cs`.

**Build:** everything in the spec's World 1 section: the stilt walk with the belly grate core and stomp waves, the
chimney hop with crumbling chimney tops, the soot curtain and the top-middle Gloop pipe, the flue run with puffing pots
and edge charges, the hard-road moves, and its arena. About 90 s with the starting harpoon and no upgrades.

**Tests:** the grate is only open after three coughs (two on the hard road); a stomp sends waves both ways; a chimney
loses its top tile when Sootjack leaves it; in phase 3 Sootjack comes out of the pot that puffed (on the hard road one of
two puffing pots is a fake); the arena validates.

**Gate:** Kokan plays it on the laptop (debug page, TEST STAGE on the World 1 boss) and says what to change. Tasks 5
to 9 start only after that, so the feel of the shared kit is settled first.

**Prompt:**

```
Read CLAUDE.md, .claude/skills/bubblewright-level-design/SKILL.md,
docs/superpowers/specs/2026-11-27-boss-redesign-design.md (World 1 and the shared rules) and Task 4 of
docs/superpowers/plans/2026-11-27-boss-redesign.md. Tasks 1 to 3 are merged. Build Sootjack and its arena on a new branch
boss/04-sootjack, using the kit only (add to the kit if something is missing, and say so in the PR). Match the Boss
Sheet (https://claude.ai/artifact/QBqkbCMeTjEgMTZ1xrGXkb). Tests first. Run the checks from CLAUDE.md, open a PR, and ask
Kokan to play-test it before the other bosses start.
```

---

### Tasks 5 to 9: the other five bosses (parallel, one thread each, after the Sootjack play-test)

Each task builds one boss from its section of the spec: its `BossDef` in `Scripts/Bosses/Defs/W<n>/`, its arena in
`Scripts/Stage/Stages/Bosses/`, its painter, its `Tuning` header, its hard-road moves, and its own test file. Each
file stays inside its own folder, so the five branches do not conflict; shared kit changes go in a separate small PR
first.

| Task | Boss | World | Branch | Tests to add (at least) |
| --- | --- | --- | --- | --- |
| 5 | Burrowmaw | 2 Forest | `boss/05-burrowmaw` | a breach follows its dust column by 1 s; a thorn nut that hits the head opens it for 4 s; the phase 3 floor is a full gap and falling costs a heart; branches it crawls over break |
| 6 | The Sandglass | 3 Desert | `boss/06-sandglass` | cutting both chains within 3 s (1.5 s hard) flips it and adds 20 s to the clock, one chain alone does not; sand blocks crumble after 5 s; the scarab's lap visits floor, wall, ceiling, wall |
| 7 | The Whiteout | 4 Winter | `boss/07-whiteout` | a charge crushes low ledges and not high ones; the freeze beam makes ice blocks that melt after 10 s; three frozen coolant pods open the reactor for 4 s; the phase 3 exhaust blast hurts directly above it only |
| 8 | The Deep Bell | 5 Catacombs | `boss/08-deep-bell` | a toll lifts it one tile (two hard) and a core hit sinks it half a tile; reaching the ceiling hurts Wick and drops it; the bell can only be hit inside lamp light; each phase opens the hatch to the next storey (3 storeys) |
| 9 | The Crucible Forge | 6 Lava | `boss/09-crucible-forge` | 5 storeys and 5 phases; the lava rises and stops at the bottom of storey 5; a slam breaks the platform and raises a spike strip; steering the pour into the vat opens the core for 4 s; the clock is 300 s |

**Prompt (fill in the boss, world, task number and branch):**

```
Read CLAUDE.md, .claude/skills/bubblewright-level-design/SKILL.md,
docs/superpowers/specs/2026-11-27-boss-redesign-design.md (the shared rules and the <BOSS> section) and Task <N> of
docs/superpowers/plans/2026-11-27-boss-redesign.md. The kit and Sootjack are merged and Kokan has play-tested Sootjack.
Build <BOSS> (World <W>) and its arena on a new branch <BRANCH>, using Sootjack as the worked example. Keep your files in
your boss's own folders; if the kit needs a change, make it a separate small PR first. Match the Boss Sheet
(https://claude.ai/artifact/QBqkbCMeTjEgMTZ1xrGXkb). Write the tests listed for Task <N> first. Run the checks from
CLAUDE.md and open a PR.
```

---

### Task 10: Cleanup

**Files:** `Scripts/Bosses/BossSpec.cs`, `LegacyBoss.cs`, `PlaceholderArt` (`BossShape` painters), the debug page,
`docs/design/expansion-art.md`, the balance summary menu, `README.md`, `CLAUDE.md` (Layout table).

**Build:** once all six new bosses are merged, delete `BossSpec`, `BossShape` and `LegacyBoss` and their tests and
debug buttons; point the "Bosses" section of `docs/design/expansion-art.md` at the spec and the Boss Sheet; add the
bosses to the balance summary (health per phase, clock, hard-road changes); add `Scripts/Bosses/Defs/` to the
`CLAUDE.md` layout table.

**Prompt:**

```
Read CLAUDE.md, docs/superpowers/specs/2026-11-27-boss-redesign-design.md and Task 10 of
docs/superpowers/plans/2026-11-27-boss-redesign.md. All six new bosses are merged. On a new branch boss/10-cleanup,
remove the legacy boss code and its tests, update the docs and the balance summary as listed, run the checks from
CLAUDE.md and open a PR.
```
