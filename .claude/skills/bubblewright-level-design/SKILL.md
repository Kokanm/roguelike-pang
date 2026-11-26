---
name: bubblewright-level-design
description: Use when creating, editing, reviewing or balancing a Bubblewright stage (a StageSpec under Assets/Bubblewright/Scripts/Stage/Stages), or when asked to design a level, puzzle room, bunker, tower or world's stage set for this game.
---

# Bubblewright level design

Stages are hand-built puzzles: one sealed `StageSpec` class per file, a 32 x 14 grid per storey, and a
`Populate` that places Wick and the enemies. `StageValidator` (run by `StageValidatorTests` in Unity) is the
judge of every game rule. This skill gives you the rules and numbers so you design a stage that passes the first
time, without re-deriving them from the engine code.

**Read first:** `references/mechanics-and-archetypes.md` (numbers generated from `Tuning`, what fits through
what, the unlock table, how the validator plays a stage, and 8 archetypes). Then open two or three of the World 1
stages below that are closest to your idea. They were play-tested and approved by Kokan; copy their style.

| Stage | Shows |
|---|---|
| `W1/W1_FirstPop.cs` | D1 intro: literal rows, `b.Wick(x)` |
| `W1/W1_SlottedBunker.cs` | bunker with 1-tile firing slots, `B` feet as an optional exit |
| `W1/W1_Crawlway.cs` | 1-row crouch tunnel under a full-height divider, slots in the tunnel roof |
| `W1/W1_TwinChambers.cs` | divider wall, ladders on both faces, bridge that holds big bubbles back |
| `W1/W1_WellShaft.cs` | 1-column wall-jump shaft, crouch entrance, penned bubble |
| `W1/W1_TrigloopSieve.cs` | deck with 2-tile sieve holes, ladders at the ends |
| `W1/W1_PogoSteps.cs` | 2-column ground gaps, floor Pogos on the landings, Wick starting on a dais |
| `W1/W1_BreakVault.cs` | `B` vault with bubbles sealed inside |
| `W1/W1_BellTower.cs` | 2 storeys, twin ladders and hatches, `StageSketch` loops |
| `W1/W1_ThreeRoofs.cs` | 3 storeys, one ladder through two hatches |
| `W1/W1_TerraceClimb.cs`, `W1/W1_LanternCourt.cs` | terraces and pads, one bubble per height |

All paths are under `Assets/Bubblewright/Scripts/Stage/Stages/`.

## 1. Write the Stage Design Brief first

Before any code, write this brief (in your reply, or as a comment block in the file). Every line is required.

```
World / Difficulty / slots: W2, D3, MinStageInWorld 2, MaxStageInWorld 5
Concept (1-2 sentences, under 160 characters): ...
Archetype(s): Slotted Bunker + Floor Clinger Pogo Gauntlet
Structure: mirror-symmetric about x = 0 / twin chambers / rhythmic columns / storey climb (never scattered shelves)
Enemies and why each one is where it is: ...  (guest, if any, named here and in Concept)
Pops: 15 + 15 + 7 + 1 + 1 = 39 (D3 band 35-65); shortest hard clock for slots 2-5 in W2 = 54 s -> max 86 pops
Hitless plan for Moss with Heavy Boots and the default harpoon: step by step, every chamber and storey
Over-break check: with every B gone, can Wick still leave every spot, and is every enemy still reachable or shootable?
```

Pick `Difficulty` from the run's slot targets: calm road `[1, 2, 2, 3, 3]`, hard road `[1, 3, 3, 4, 4]` for slots 1
to 5. Set the slot bounds like this:

| Difficulty | World 1 | Worlds 2-6 |
|---|---|---|
| D1 | `MaxStageInWorld = 1` (exactly one stage) | `MaxStageInWorld = 1` (exactly one stage) |
| D2 | `MinStageInWorld = 2` | `MinStageInWorld = 2` |
| D3 | `MinStageInWorld = 3` (pinned by `StageValidatorTests`) | `MinStageInWorld = 2` (the hard road needs D3 in slots 2-3) |
| D4 | never | `MinStageInWorld = 4`, or `3` when the world's plan says so |

The pop-rate limit uses the shortest hard clock among `MinStageInWorld..MaxStageInWorld`, so a lower
`MinStageInWorld` means a tighter limit. Keep the per-world mix (World 1: 1 D1, 5 D2, 6 D3; Worlds 2-6: 1 D1,
3 D2, 4 D3, 4 D4) and check what that world already has. Put the brief in the stage file as a `<remarks>` block
under the class `<summary>`, so a reviewer can read it next to the grid.

## 2. Hard rules (the validator rejects these)

* **Unlocks:** follow the unlock table in the reference. 1-1 is Gloop only. `B` and `_` from 1-2, Trigloop and
  Pogos from 1-3. Sand (`S`) **only** in World 3, ice (`I`) **only** in World 4, `b.Spike` **only** in World 6.
  Never author `Lucky`. At most one guest kind (an earlier world's signature), never in D1. D1 in Worlds 2-6:
  signature + Gloop/Trigloop/Lead only, no rollers, ceramic, steel or Drifter. Sinker and Drifter are at most L.
  Ceramic is XL, L or M (never S) and from World 3.
* **Every storey** has at least one required enemy (steel things do not count).
* **Pop count** inside the band: D1 10-25, D2 20-40, D3 35-65, D4 55-90. Gloop XL = 15, L = 7, M = 3, S = 1;
  Trigloop XL = 40, L = 13, M = 4; ceramic adds 1; a big roller is 3. And pops / shortest hard clock <= 1.6.
* **Ground gaps: 2 columns at most, 1 in World 3** (all of World 3 is sand) or next to sand. Kokan set this cap
  on 2026-11-17; it is what Moss with Heavy Boots jumps, column centre to column centre.
* **Wick's start:** on solid footing, not on ice, not inside a tile, and nothing touches him in the first 1.2 s
  standing still (a roof between him and a bubble counts as safe). Any plain Gloop/Trigloop/Lead may become a
  Lucky, so it must be fair either way.
* **Reach, judged at the start position:** for every required enemy, Moss with Heavy Boots must reach a spot in
  **the enemy's starting column** from which the enemy is inside his headroom or straight above with no solid or
  unbroken `B` tile between (breakable ones count as broken once he can shoot them). So a bubble that starts over
  a solid roof or a tunnel roof fails, even if it would bounce into the open later: start every required enemy
  over open floor, a slot, or a ledge Wick can stand on. Moves: walk; crouch through 1-row gaps; jump up one row
  (1.9 u, never two rows) or across within reach; drop; ladders; jump off a wall he stands against; chained wall
  jumps up a 1-column shaft. A crouched spot (1 row of headroom) cannot jump, so never put a gap or a step inside
  a crouch tunnel. No double jump, no crate weapons, no fast hero.
* **No unshootable pockets:** any empty space a bubble or its children can get into is space Wick can reach or
  fire into. Sealed `#` rooms with a bubble inside fail.
* **Over-break:** breaking every `B` must not strand anything that was reachable or leave Wick in a pit he
  cannot leave. Never make a `B` bridge or floor the only way to a room sealed by `#`.
* **Storeys:** the top row of a storey (row `13`, and `27`) is its **cap**: an invisible ceiling for Wick across
  the whole width, except at a **hatch**, which is an `H` ladder cell on the cap row. The hatch is shut while the
  storey has enemies, opens when it is clear and shuts behind him. Draw the storey above's floor on row `14` (and
  `28`) as solid with a `T` over each hatch, and run `H` from the hatch down to the floor below, as
  `W1_BellTower` does: `for (int y = 0; y <= 14; y++) grid[y][c] = y == 14 ? 'T' : 'H';`. So Wick has 13 usable rows
  on a lower storey. He only goes up; bubbles never cross a storey line. No enemy within 3 u of where Wick climbs
  out, and the hatch arrival gets its own 1.2 s fair start.
* **Placed things:** bubbles start clear of tiles and of each other; Clingers have a surface (`mount`) to hold;
  rollers rest on solid ground with a diameter of headroom and never over a gap; spikes sit on open floor, away
  from ladders and from each other. Every Pogo, floor Clinger Pogos included, sits over ground (not a gap), in a
  column with no ladder in any row of any storey, and at least `pogoMinSpacing` (2 u) from every other Pogo
  (`PogoTests.PogosFollowTheirStageRules`).

## 3. Design rules (the validator cannot see these, Kokan can)

* **One idea per stage**, stated in `Concept`, and the layout exists to serve it. No random shelves.
* **Structure:** prefer mirror symmetry about `x = 0` (column 15 | 16), twin chambers, or a rhythm of repeated
  pieces. Asymmetry is fine when it is the point (terraces, a tower), never by accident. The game mirrors stages
  at random (`CanMirror`), so a lopsided idea still reads both ways.
* **Touch controls come first:** leave a comfortable margin for every jump, slot and safe lane. No pixel-precise
  moves. A gap at the reach limit is a bad gap; give Moss slack.
* **Puzzle Clingers are patient** (`patient: true` is the default in `b.Clinger` and `b.FloorClinger`); pass
  `patient: false` only when the 4 s timeout is the point. Floor Pogos Wick must jump over should be S (he clears
  a triggered M by only about 0.2 u). Keep 3 u, measured from the Pogo's centre to Wick's centre, between a floor
  Pogo and anywhere he must stand still (it wakes at 1.95 u for an S, 2.3 u for an M).
* **How each Clinger behaves** (patient ones wait for the trigger, the storm, or their tile breaking):

  Every round Clinger re-sticks after letting go (not patiently: the 4 s timeout applies from then on).

  | Clinger | Wakes when | Then | Wick kills it by |
  |---|---|---|---|
  | Ceiling | Wick is below it, centre within 1.5 u sideways | wobbles 0.4 s, then a Pogo drops straight down; a round one drops moving sideways at 1.75x Gloop speed and sticks to the next surface it touches | shooting it while it hangs, or as it drops |
  | Floor Pogo (`b.FloorClinger`) | Wick's centre is within 1.5 u + its radius sideways, at any height | wobbles 0.4 s, leaps straight up to the first surface above and hangs there as a ceiling Clinger | jumping over it before it wakes and shooting down, or waking it, stepping back, and shooting it as it hangs or drops |
  | Floor, not Pogo | Wick is above it, within 1.5 u sideways | jumps a Gloop's bounce height moving sideways fast, sticks to the next surface | shooting down from a ledge, or as it flies |
  | Left / right wall | Wick is out in front of it, within 1.5 u of its height | pushes straight off the wall, fast, and sticks to the next surface | shooting it while it holds the wall or as it crosses |

  A Clinger only sticks under a platform with at least 2 u of floor clearance below, so a leaping floor Pogo
  under a low roof goes on up to the next surface that allows that.
* **Sinkers** keep 75% of each bounce on sand (90% elsewhere), then roll along their surface, turning at walls,
  gap edges and platform ends, so they never fall off by themselves. A rolling Sinker is low and easy to shoot
  but blocks a corridor; use it to pace a stage. A Sinker Pogo rests, wobbles and springs back to full height.
  Sinkers can be ceramic (World 3 on, L or M).
* **Scorch flames and spikes (World 6):** every landing of an XL, L or M Scorch lights a flame on the one column under
  it for 1.5 s (6 at most; an S lights none, and nothing lights over a ground gap). Flames on a roof or grate above
  Wick are harmless, so a roof is how a lava stage gives him shelter. A Scorch Pogo relights its column every bounce:
  keep it off a column Wick must cross. Spike strips: `b.Spike(col, width, row, phase)`; leave one empty column
  between strips on the same surface, keep every strip column at least two columns from **any** ladder column on any
  storey, and keep Wick's start off every strip. Default phases rise 0.8 s apart in the order you place them; pass
  `phase:` (a cycle is 4.9 s, so 2.4 is about half) when two strips should beat together or opposite.
* **Where a Sinker can settle** (found while building World 3): a rolling Sinker stops at the edge of every slot,
  hole and step, so its edge never crosses a tile boundary, and a cable fired from the next column misses it by
  half a tile. Wick can only shoot it from the surface it rolls on: he fires in front of it and hops off. So every
  top a Sinker can land on must be one Wick can stand on and at least 4 wide (5 is comfortable for an L), or `B`
  he can shoot out from below. Never put Sinkers over a roof, tunnel roof, narrow terrace or 1-wide lip that Wick
  cannot stand on; run walls to the ceiling instead of leaving a top, and use Gloops where a roof is unreachable.
* **Guests are planned:** name the guest in `Concept` and give it a job in the puzzle.
* **Readability:** a stage reads at a glance. Use `#` for structure, `B` only where breaking is a choice or the
  puzzle, ladders where the route is not obvious.
* **Concept:** one or two plain sentences, under about 160 characters (it is shown on one debug-panel line).

## 4. Write the file

File: `Assets/Bubblewright/Scripts/Stage/Stages/W<world>/W<world>_<PascalName>.cs`. Nothing else to register:
`StageSpec.All` finds every sealed subclass. Commit a `.meta` with it: copy a neighbouring stage's `.meta` and put a
fresh random 32-hex `guid` in it (Unity would make one on import, but the commit must carry it).

Write rows one of two ways. **Literal rows** (good for one storey; keep the ruler and row comments aligned):

```csharp
using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>World 2 difficulty 2. One line on what the stage is.</summary>
    [Preserve]
    public sealed class W2_Example : StageSpec
    {
        public override string Id => "w2_example";          // unique, snake_case, w<world>_ prefix
        public override string Name => "Example";           // unique
        public override int World => 2;
        public override int Difficulty => 2;
        public override int MinStageInWorld => 2;
        public override string Concept => "One idea, one or two sentences.";

        //                                   01234567890123456789012345678901
        public override string[] Rows => new[]
        {
            "................................", // r13 (top)
            // ... 14 strings per storey, top row first, each exactly 32 characters ...
            "................................", // r0 (stands on the ground)
        };

        public override string Ground => SolidGround;       // or a 32-char string of = N I S _

        protected override void Populate(StageBuilder b)
        {
            b.WickAtCol(16);
            b.BubbleAt(BubbleKind.Gloop, StageBuilder.TierXL, 5, 9f, -1);
            b.BubbleAt(BubbleKind.Gloop, StageBuilder.TierXL, 26, 9f, 1);
        }
    }
}
```

**`StageSketch`** (best for storeys, walls and anything long; `grid[y][c]`, y = 0 is the ground row, and
`Box(grid, c0, c1, y0, y1, ch)` fills a rectangle, low ends first):

```csharp
public override int Storeys => 2;
public override string[] Rows
{
    get
    {
        var grid = StageSketch.Open(2);
        StageSketch.Box(grid, 0, 31, 14, 14, '#');                  // floor of storey 2
        for (int y = 0; y <= 14; y++) grid[y][14] = y == 14 ? 'T' : 'H'; // ladder and hatch
        StageSketch.Box(grid, 8, 8, 1, 4, '#');
        return StageSketch.Lines(grid);
    }
}
```

`check_stage.py` evaluates both forms (loops, ternaries, `Box`, `grid[y][c] = ...`). Keep `Rows` to these forms;
it rejects code it cannot follow.

Tiles: `.` empty, `#` solid (sand in World 3), `N` normal solid, `I` ice (W4), `S` sand (W3), `B` breakable
(2 hits), `H` ladder in empty space, `T` ladder in a solid tile (a ladder top you stand on). Ground: `=` solid,
`N`, `I`, `S`, `_` gap (a pit that costs a heart).

`StageBuilder` (x is in units with 0 in the middle: column `c` is `x = c - 15.5`, or use `StageMap.CenterX(c)`;
y is the bubble's centre height in units above the ground; `dir` is -1 left, +1 right):

| Call | Use |
|---|---|
| `b.WickAtCol(col, row = 0)` / `b.Wick(x, y = 0)` | Wick's start (feet). Exactly one. |
| `b.BubbleAt(kind, tier, col, y, dir, ceramic, pogo)` / `b.Bubble(kind, tier, x, y, dir, ceramic, pogo, steel)` | a bubble; `pogo: true` bounces in place (y = floor + radius + a hair: `W1_PogoSteps` uses 0.85 for an M) |
| `b.Clinger(tier, x, y, mount, dir, pogo, ceramic, patient = true)` | Clinger on `ClingerMount.Ceiling` (first surface above y), `Floor`, `LeftWall`, `RightWall` |
| `b.FloorClinger(tier, x, surfaceY = 0, dir, pogo = true, ceramic, patient = true)` | patient floor Clinger Pogo sitting on `surfaceY` |
| `b.SteelPogo(tier, x, surfaceY = 0)` | unbreakable Pogo, not required (W3+) |
| `b.Roller(x, surfaceY = 0, dir, tier = 0, ceramic, steel)` | rolling ball (W2+); tier 0 big, 1 small |
| `b.Spike(col, width, row = -1, phase)` | spike strip (W6 only); row -1 is the ground, else on top of that row |
| `b.Wave(index, kind, tier, x, dir, ceramic, pogo)` / `b.WaveStorey(s)` | reinforcements dropped from the ceiling of the top storey (or `s`) |

Tiers: `StageBuilder.TierXL` (3.5 u), `TierL` (2.5), `TierM` (1.6), `TierS` (0.9). A bubble's start must clear
tiles by its radius; bigger bubbles bounce higher (peaks in the reference), so give them the room.

## 5. Check, then hand over

Run, from the repository root, and fix everything before you finish:

```bash
python3 .claude/skills/bubblewright-level-design/scripts/check_stage.py Assets/Bubblewright/Scripts/Stage/Stages/W2/W2_Example.cs --show
python3 .claude/skills/unity-csharp-scripting/scripts/check_unity_project.py .
unity test . --mode EditMode --filter "Bubblewright.Tests.StageValidatorTests" --output Temp/validator.xml
```

* `check_stage.py` checks only the shape (32 columns, 14/28/42 rows, characters, `[Preserve]`, sealed,
  `Concept`, writes inside the grid, placements inside the arena). `--show` draws the stage on a ruler with Wick
  (`@`), bubbles (`X L M S`, Clingers lower case) and rollers (`r`). **Look at the drawing** and walk the hitless
  plan on it: it is the fastest way to catch a wrong row or a bubble inside a wall.
* `StageValidatorTests` runs every game rule above on the stage and its mirror. Its messages name the rule.
* `check_unity_project.py` comes from the `unity-csharp-scripting` skill (listed in `skills-lock.json`). If that
  folder is missing in your checkout, say so and skip it; it only checks `.meta` files and script references.
* **No Unity (cloud sessions):** you cannot run the validator. Do not port it to Python; reason with the
  reference tables, walk the brief's hitless plan on the `--show` drawing, and say plainly in your hand-over that
  `StageValidatorTests` (and the full EditMode suite) still need a run on Kokan's laptop.
* A world's stage count is pinned in `StageValidatorTests` (`W1` has `{D1: 1, D2: 5, D3: 6}`). Add or change
  a world's count assertion only when that world's twelve stages are done (the plan's per-world task says so);
  a single new stage needs no test change.
* The reach check looks only at each enemy's **starting column** (see Hard rules). Start bubbles over open
  floor or a slot, as `W1_SlottedBunker` does with its XLs over slots 12 and 20.

When you finish, report: the brief, the file path, the `--show` drawing, and which checks ran and which did not.
Kokan play-tests every world; whatever he corrects becomes a rule here, so fix the skill, not just the stage.

## Common mistakes

| Mistake | Instead |
|---|---|
| A row of 31 or 33 characters | Use `StageSketch`, or keep the ruler comment above literal rows; run `check_stage.py` |
| Row 0 written first | `Rows` is top row first; with `StageSketch`, y = 0 is the ground row and `Lines` flips it |
| 3-column gap, or 2-column gap in World 3 | 2 columns at most, 1 in World 3 or next to sand |
| Platform 2 rows above where Wick stands, no ladder | One row per jump; add a ladder, a wall-jump wall or a 1-column shaft |
| Sealed `#` room with a bubble in it | Give it a slot, a `B` wall, or a ladder route |
| `B` floor that is the only way into a `#` room | Make the `B` a wall Wick chips from outside, or add a second route |
| No `H` on row 13, or a `T` there | The hatch is the `H` on the cap row 13; the `T` sits above it in row 14 |
| Bubble starting over a roof or a tunnel | Start it over open floor, a slot or a reachable ledge (the reach check uses its start column) |
| Gap inside a crouch tunnel | A crouched Wick cannot jump; keep gaps where he stands upright |
| Bubble placed within a radius of a tile | Check y against the drawing; a bubble's centre needs radius clearance |
| Long Concept or a list of mechanics | One idea in one or two sentences |
| Porting the validator to check a stage | Use the tables and the drawing; leave the real check to `StageValidatorTests` |
| Random shelves "for variety" | Pick an archetype and a structure in the brief |
| Sinker over a roof, lip or narrow ledge Wick cannot stand on | It will settle and roll there out of reach; see "Where a Sinker can settle" |
