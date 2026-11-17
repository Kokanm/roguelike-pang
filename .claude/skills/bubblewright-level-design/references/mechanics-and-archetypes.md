# Bubblewright stage mechanics and puzzle archetypes

Read with `../SKILL.md`. The tables between the GENERATED markers are written by the Unity menu item
**Bubblewright > Write Level-Design Reference** from `Tuning`, `Character.All`, `Debuff.All` and `StageValidator`.
Do not edit them by hand: `LevelDesignReferenceTests.LevelDesignReferenceIsUpToDate` fails when they drift.
Everything after the END marker is written by hand.

## Numbers

<!-- BEGIN GENERATED: Bubblewright > Write Level-Design Reference. Do not edit by hand. -->
_Written from `Tuning`, `Character.All`, `Debuff.All` and `StageValidator`. One unit is one tile: one column, one row, 15 px._

### Grid

| What | Value |
|---|---|
| Columns | 32 (`0..31`); column `c` is centred at `x = c - 15.5`, so `x = 0` is the line between columns 15 and 16 |
| Rows per storey | 14; row `0` stands on the ground, `Rows` lists the top row first |
| Storeys | 1 to 3: rows `0..13`, `14..27`, `28..41` |

### Wick's body and moves (every hero)

| What | Value |
|---|---|
| Standing height | 1.47 u: walking needs 2 clear rows |
| Crouched height | 0.88 u: crouch-walking needs 1 clear row |
| Width | 1 u |
| Jump height | 1.9 u: a standing jump climbs a step of 1 row, never 2 |
| Jump air time on flat ground | 0.56 s |
| Crouch walk | 35% of run speed |
| Ladder climb | 5 u/s |
| Wall slide | 4 u/s; a wall jump pushes 1 u off the wall |
| Harpoon launcher height | 1.67 u standing, 0.81 u crouched (above his feet) |
| Breakable `B` tile | 2 harpoon hits |

### Heroes

Heavy Boots and Frayed Cable are hard-world debuffs. Sand multiplies walking by 0.7. Jump reach is run speed times the flat air time. StageValidator walks every stage as Moss with Heavy Boots.

| Hero | Run u/s | Heavy Boots | On sand + Heavy Boots | Jump reach, Heavy Boots | Same on sand | Harpoon u/s | Frayed Cable | Cables |
|---|---|---|---|---|---|---|---|---|
| Wick | 9 | 7.65 | 5.36 | 4.28 u | 3 u | 20 | 16 | 1 |
| Ember | 10.5 | 8.93 | 6.25 | 5 u | 3.5 u | 24 | 19.2 | 1 |
| Moss | 7.5 | 6.38 | 4.46 | 3.57 u | 2.5 u | 18 | 14.4 | 1 |
| Flint | 8.5 | 7.23 | 5.06 | 4.05 u | 2.83 u | 16 | 12.8 | 2 |

### Bubble tiers

Peak is the top of the bubble above the floor it bounces on. Every bubble moves sideways at 4 u/s (Lead 5.6 u/s). A ceramic bubble takes 2 hits and adds 1 pop.

| Tier | `StageBuilder` | Diameter | Peak | Lead peak | Bounce time | Lead bounce time | Pops, Gloop | Pops, Trigloop |
|---|---|---|---|---|---|---|---|---|
| XL | `TierXL` | 3.5 | 13 | 9.1 | 3 s | 1.8 s | 15 | 40 |
| L | `TierL` | 2.5 | 11 | 7.7 | 2.6 s | 1.56 s | 7 | 13 |
| M | `TierM` | 1.6 | 8.5 | 5.95 | 2.2 s | 1.32 s | 3 | 4 |
| S | `TierS` | 0.9 | 6 | 4.2 | 1.8 s | 1.08 s | 1 | 1 |

### Bubble kinds

| Kind | Children per pop | Largest tier | Peak x | Speed x |
|---|---|---|---|---|
| Gloop | 2 | XL | 1 | 1 |
| Trigloop | 3 | XL | 1 | 1 |
| Lead | 2 | XL | 0.7 | 1.4 |
| Lucky | 2 | XL | 1 | 1 |
| Clinger | 2 | XL | 1 | 1 |
| Sinker | 2 | L | 1 | 1 |
| Frost | 2 | XL | 1 | 1 |
| Ghost | 2 | XL | 1 | 1 |
| Scorch | 2 | XL | 1 | 1 |
| Drifter | 2 | L | 1 | 1 |

### Rolling balls, enemies and hazards

| Field | Value | Meaning |
|---|---|---|
| `rollerDiameters` | 1.2 / 0.8 u | big and small rolling ball; a big one is 3 pops (itself and two small) |
| `rollerSpeeds` | 3.5 / 4.5 u/s | big and small rolling ball |
| `clingerTriggerX` | 1.5 u | a stuck Clinger lets go when Wick is this close to its lane (plus its radius for a floor Pogo) |
| `clingerMaxStickTime` | 4 s | a Clinger that is not patient lets go by itself after this |
| `clingerWobbleTime` | 0.4 s | warning wobble before a Clinger lets go |
| `clingerMinClearance` | 2 u | a Clinger only sticks under a platform when Wick can still walk under it |
| `sinkerKeepSand` / `sinkerKeep` | 0.75 / 0.9 | share of the last bounce a Sinker keeps on sand / elsewhere |
| `frostIceTime` | 4 s | a floor tile a Frost landed on stays ice this long |
| `ghostSolidTime` / `ghostFadedTime` | 2.5 / 1.5 s | a Ghost can be hit and hurt only while solid; it passes through tiles |
| `flameLife` | 1.5 s | a flame burns where a Scorch landed (6 at most) |
| `drifterSpeed` | 5 u/s | a Drifter flies diagonals at this speed |
| `spikeDownTime` / `spikeWarnTime` / `spikeUpTime` | 2.6 / 0.5 / 1.8 s | spike strip cycle; each strip starts 0.8 s after the one before |
| `waveSafeDistance` | 4 u | a wave bubble never drops in closer than this to Wick, sideways |
| `ceramicMaxTier` | M | the smallest ceramic tier: a ceramic bubble is XL down to this, never smaller |

### Gaps and pace (StageValidator)

| Rule | Value |
|---|---|
| Widest ground gap allowed | 3 columns, 2 in World 3 or next to sand |
| Widest gap Wick must jump across | 2 columns, 1 in World 3 (Moss with Heavy Boots reaches 3.77 u, 2.7 u on sand, centre to centre) |
| D1 pop count | 10 to 25 |
| D2 pop count | 20 to 40 |
| D3 pop count | 35 to 65 |
| D4 pop count | 55 to 90 |
| Pop rate | at most 1.6 pops per second of the shortest hard-road clock among the slots the stage can land in |

### Hard-road clock per slot (seconds, and the most pops that clock allows)

A stage's limit is the smallest one over `MinStageInWorld..MaxStageInWorld`. The calm-road clock is this divided by 0.75.

| World | Slot 1 | Slot 2 | Slot 3 | Slot 4 | Slot 5 |
|---|---|---|---|---|---|
| 1 | 45 s, 72 | 48 s, 76 | 51 s, 81 | 54 s, 86 | 57 s, 91 |
| 2 | 51 s, 81 | 54 s, 86 | 57 s, 91 | 60 s, 96 | 63 s, 100 |
| 3 | 65.55 s, 104 | 69 s, 110 | 72.45 s, 115 | 75.9 s, 121 | 79.35 s, 126 |
| 4 | 63 s, 100 | 66 s, 105 | 69 s, 110 | 72 s, 115 | 75 s, 120 |
| 5 | 69 s, 110 | 72 s, 115 | 75 s, 120 | 78 s, 124 | 81 s, 129 |
| 6 | 75 s, 120 | 78 s, 124 | 81 s, 129 | 84 s, 134 | 87 s, 139 |
<!-- END GENERATED -->

## What fits through what

Clearances are whole rows and columns, because tiles are 1 unit.

| Opening | Who gets through |
|---|---|
| 1 row high | Wick crouched (shuffles at crouch speed, cannot jump, fires from 0.81 u). No bubble but S (0.9 u) fits. |
| 2 rows high | Wick standing, and M and S bubbles. L (2.5 u) and XL (3.5 u) are held. |
| 3 rows high | L too. Only 4 rows let an XL pass. |
| 1-tile hole in a deck or roof | A **firing slot**. M and L never fall through; S does, slowly (one slot: 7 of 24 drops in 60 s; three slots: all, median about 26 s). |
| 2-tile hole | The **M/S sieve**: holds L and XL, lets M and S rain through (three holes: M median about 14 s). |
| 3-tile hole | Lets L through (median about 5 s), so it filters only XL. |
| 1-column shaft with walls both sides | Wick climbs it with chained wall jumps, one row per jump, no ladder needed. |
| Step 1 row up | A standing jump. 2 rows up needs a ladder, a wall to kick off, or a shaft. |
| Ground gap Wick must cross | **2 columns at most, 1 in World 3.** The gap check allows 3 (2 on sand), but the reach check jumps centre to centre with Moss and Heavy Boots, so a wider gap he has to cross fails as "cannot reach". A wider gap is fine only where a ladder or another route goes round it. |

Filter numbers are from `FilterGapTests` (Task 5). `MossGetsPastATriggeredFloorPogo` logs how Moss with Heavy Boots
gets past a triggered floor Pogo; by arithmetic he clears a triggered M Pogo by only about 0.2 u, so a stage that
makes him jump over a floor Pogo uses S Pogos or gives a long run-up.

## Unlocks (StageValidator enforces all of this)

| From | What unlocks |
|---|---|
| 1-1 (`World 1`, `MaxStageInWorld = 1`) | Gloop only. No `B`, no `_` gaps, no Pogos, no rollers. |
| 1-2 (`MinStageInWorld >= 2`) | `B` breakable tiles, `_` ground gaps |
| 1-3 (`MinStageInWorld >= 3`) | Trigloop, plain Pogos (`pogo: true` on Gloop, Trigloop, later Lead) |
| World 2, Forest | Clinger (signature), Lead, Drifter (not in D1, largest L), pearl `Roller` |
| World 3, Desert | Sinker (signature, largest L), steel (`steel: true`, `SteelPogo`, steel `Roller`), ceramic (`ceramic: true` on XL, L or M bubbles, never S; big rollers). `S` sand tiles and ground **only in World 3**; `#` and `=` are sand there by default. |
| World 4, Winter | Frost (signature). `I` ice **only in World 4**; Wick must start on normal ground (`N`). |
| World 5, Catacombs | Ghost (signature; passes through tiles, bounded by its storey) |
| World 6, Lava | Scorch (signature). `b.Spike(...)` strips **only in World 6**. |

* Lucky is never authored. The run turns one plain Gloop, Trigloop or Lead into a Lucky at random, and the
  validator re-runs the fair start with each possible swap, so a plain bubble near Wick must be fair as a Lucky too.
* Difficulty 1 in World 2..6 uses only the world's signature kind and classic kinds (Gloop, Trigloop, Lead):
  no guest, no Drifter, no rollers, no ceramic or steel.
* A guest is an earlier world's signature kind. At most one guest kind per stage, placed on purpose and named in `Concept`.
* Every storey needs at least one required enemy. Steel things are not required (they cannot be popped).

## How StageValidator plays a stage

It is the judge; `check_stage.py` only checks the shape. Reading this saves re-deriving it from the code.

* **Hero:** Moss with Heavy Boots (and sand speed in World 3), default harpoon, no upgrades.
* **Standing spots:** every column/height where Wick's feet can rest with at least crouch headroom.
  Spots with 2 rows of headroom can jump.
* **Moves between spots:** walk to a neighbouring column at the same height; climb a ladder (`H` or `T` in every
  row between); jump up to 1.9 u higher, or level or lower, within jump reach (Moss with Heavy Boots: 3.57 u + 0.2 u,
  2.5 u + 0.2 u on sand, measured column centre to column centre) when the path is clear; drop to a lower spot up to 1.2 u sideways; jump away from a wall he stands against; climb
  one-column shafts a row at a time. Spots on different storeys only connect by a ladder through the hatch.
* **Breaching:** starting with every `B` intact, it breaks each `B` that Wick can shoot (straight up, down in
  the air, or chip sideways from beside it) and grows the reachable set until nothing changes.
* **Required:** judged at each enemy's start position and column: Wick reaches a spot in that column with the
  enemy inside his headroom, or straight above with a clear line for the harpoon. With **all** `B` broken, nothing that was reachable is stranded and no reachable spot is a pit
  he cannot leave.
* **Pockets:** any empty region a bubble or its children can enter must be one Wick can reach or shoot into.
* **Fair start:** 1.2 s (72 ticks) with Wick standing still at his start, and again at every hatch arrival.
  Nothing may touch him. A bubble with a roof between it and Wick is fine. No enemy within 3 u of a hatch.
* **Pops and pace:** pop count in the difficulty band, and at most 1.6 pops per second of the shortest
  hard-road clock among the slots the stage can be drawn for (tables above).
* **Shape:** unique `Id` and `Name`, `Concept` not empty, rows and ground 32 characters, ladders that connect
  two levels, gaps no wider than the table above, rollers on solid ground with a diameter of headroom, spikes on
  open floor away from ladders and each other, Clingers with a surface to hold.

## Eight puzzle archetypes

Each names the World 1 stage that shows it, where one exists. Combine at most two per stage; one idea, clearly.

1. **Slotted Bunker** (`W1_SlottedBunker`). Wick starts under a roof with 1-tile firing slots; big bubbles
   bounce on the roof and can only be hit through the slots. `B` feet on the walls are an optional way out.
   Make the slots line up with where the bubbles cross, and keep the roof at row 4 or higher so Wick stands.
2. **Floor Clinger Pogo Gauntlet** (World 2 on). A long floor with patient floor Clinger Pogos
   (`b.FloorClinger(TierS, x)`); Wick jumps over them and fires down in the air, or walks up to trigger the leap and
   shoots from beside. Leave at least 3 u between a Pogo and where Wick must stand still. Prefer S Pogos where
   he must jump over one.
3. **Crouch Duct Sniping** (`W1_Crawlway`). A 1-row tunnel (a roof one row above the floor) under a dividing
   wall; slots in the tunnel roof let Wick fire up from a crouch. The tunnel is the only crossing, so both
   ends must be reachable from either side.
4. **Divided Twin Chambers** (`W1_TwinChambers`). A wall to the ceiling (or to a bridge) splits the screen;
   ladders on each face and a bridge over the top. Two rows over the bridge let Wick and M/S through, not L/XL,
   so each chamber keeps its own big bubble.
5. **Breakable Vault** (`W1_BreakVault`). Bubbles start sealed in a `B` box. Wick chips a wall or the roof to get
   in. Never let a `B` be the only floor or bridge to a room that is otherwise sealed by `#`: breaking it all must
   not strand anything.
6. **Sieve Ceiling** (`W1_TrigloopSieve`). A deck with 2-tile holes over Wick's court: big bubbles bounce on the
   deck, their M and S children rain through. Ladders up to the deck so Wick can reach the big ones.
7. **Roller Orbit Loop** (World 2 on). A raised block that `Roller`s circle (they roll around whole platforms,
   top and underside) while bubbles bounce over it. Give the loop a diameter of headroom all round, and keep
   Wick's start out of the orbit.
8. **Storey Tower Climb** (`W1_BellTower`, `W1_ThreeRoofs`). 2 or 3 storeys; each storey is its own fight. Row
   `13` (and `27`) is the storey's cap, an invisible ceiling for Wick except at a hatch: an `H` ladder cell on that
   row. Row `14` (and `28`) is the floor of the storey above, drawn solid with a `T` over each hatch, and `H` runs
   from the hatch down to the floor below. The hatch opens when the storey is clear and shuts behind him. No enemy
   within 3 u of where he climbs out.

Also proven in World 1: **Wall-jump well** (`W1_WellShaft`: a 1-column shaft with a crouch entrance leads to a
penned bubble), **Stepped terraces** (`W1_TerraceClimb`: each landing has a bubble only shootable from that
height), **Pads** (`W1_LanternCourt`: low raised pads, one bubble over each), **Gaps and Pogos**
(`W1_PogoSteps`: 2-column gaps with Pogos guarding the landings).
