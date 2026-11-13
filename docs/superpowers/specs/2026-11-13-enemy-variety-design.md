# Enemy variety: six new enemies and stage recipes

Design agreed with Kokan on 2026-11-13. Items marked **(call)** were decided by Claude and not discussed; change them freely.

## Why

Nothing about the enemies is broken, but a run lacks variety. Today:

- Every bubble follows the same fixed bounce; kinds differ only in numbers. Nothing reacts to Wick.
- Nothing new arrives after world 3. Worlds 4 to 6 only raise the budget, and the kind mix is the same from world 2 on.
- Worlds have their own ground (sand, ice, spikes) but no enemy of their own.
- The generator spends the budget at random and drops bubbles on random open spots, all visible from the first second, so stages blur together.

## Goals

1. Each world from 2 to 6 is recognisable by what attacks you there.
2. Stages inside a world differ from each other in how they are filled.
3. Variety grows through the run: later worlds mix more kinds than early ones.

## Non-goals

- Bosses (separate session). Boss stages are not touched.
- Layouts (separate session). The 25 layouts, the layout picker and WorldRules stay as they are.
- New modifiers like ceramic, and a real reward for Lucky. Lucky stays as it is.
- Upgrades and debuffs.

## Anti-goals

- A hit the player could not have seen coming. Every new danger has a visible tell or a fixed, readable rule.
- A stage that cannot be cleared, or an enemy the harpoon cannot reach.
- An enemy that only differs by numbers. Each new one must change what the player does.

## Constraints

- Same style as the existing code: no physics engine, Game ticks everything (so hit-stop, slow motion and pause freeze it), every number in Tuning, behaviour covered by EditMode tests.
- A stage is rebuilt from the run seed and the stage number only (saves store just the stage), so recipe, placement and wave contents must come from `Run.StageRng`. Wave timing may depend on play. Existing seeds will produce different stages after this change; that is accepted.
- Art is painted in code (PlaceholderArt) under the rules in `docs/design/expansion-art.md`. Bubbles have no faces: a kind is read by colour plus centre mark.

## Approaches considered

- **A. A signature enemy per world.** New behaviours tied to each world's ground. Most variety the player can feel; each enemy needs movement code, a sprite and tests.
- **B. More modifiers like ceramic.** Cheap combinations, but everything still bounces the same way.
- **C. Stage recipes.** Shapes how a stage is filled. No new art; adds no new enemy to learn.

Kokan picked **A and C together**, with B left out. Reason: A gives each world an identity, C makes stages inside a world differ and gives the new enemies a deliberate introduction.

## Part 1: stage recipes

### World shape

- World 1, stages 1 and 2: unchanged (flat layout, today's generator), so the opening stays a gentle tutorial. **(call)**
- Worlds 2 to 6, stage 1: **Introduction**. The world's signature enemy almost alone, on about 60% of the normal budget, no rollers, no guests, no waves.
- Every other normal stage: one recipe drawn from the pool of recipes allowed in that world, never repeating inside a world.
- Stage 6: boss, untouched.

### Recipes

| Recipe | From world | What it does |
| --- | --- | --- |
| Mixed | 1 | Today's random spend |
| Giants | 1 | One or two XL bubbles take the whole budget; nothing else at the start |
| Swarm | 1 | Only M and S bubbles; start cap raised (about double) |
| Rolling floor | 2 | Most of the budget goes to rollers, up to 4; two or three bubbles overhead. Only when the layout has roller spots, otherwise redraw |
| Home turf | 2 | The signature enemy takes about 70% of the picks, Gloops the rest |
| Hard shell | 3 | About 70% of eligible bubbles and all rollers are ceramic |
| Reinforcements | 2 | Half the budget at the start, the rest in two later waves |

In recipes other than Introduction and Home turf, the signature enemy still takes roughly a third of the picks in its own world, so it always dominates there. **(call)**

Notes **(calls)**: Giants uses only Gloop, Trigloop and Lead, since several new kinds have no XL. Rolling floor overrides `rollerMaxPerStage` for that stage. Steel rollers and the free Lucky bubble are added on top of any recipe by today's rules, except on Introduction stages.

Recipes are rows of data (name, first world, allowed tiers, start cap, roller share, signature share, ceramic chance, waves, placement patterns), in the same spirit as `BossSpec` and `LayoutDef`. Hard worlds scale the budget for every recipe as today. The balance page and LOG RUN show the recipe and the waves.

### Placement patterns

Each recipe names the patterns it may use; one is drawn per stage.

- **Scattered**: today's random open spots.
- **Mirrored pair**: bubbles in pairs at -x and +x, moving apart.
- **Top row**: evenly spaced across the top of the arena.
- **One per floor**: one group in each open band of the layout (ground, above row 5, above row 10).

Clingers are placed by their own rule whatever the pattern: attached to the underside of a platform or to the ceiling, at least 5 units sideways from Wick's start. On a flat layout they all hang from the ceiling.

All keep today's rules: clear of tiles, at least 5 units from Wick's start. A pattern that cannot place a bubble falls back to Scattered for that bubble.

### Reinforcements (later arrivals)

- Half the budget is on screen at the start. The other half is split into two waves, fixed by the seed.
- A wave is called when 2 or fewer enemies are left, or 20 s after the previous group arrived, whichever comes first. **(call)**
- Each arriving bubble shows a warning marker at the ceiling for 1.2 s at its entry point, then drops in from above. Entry points are at least 4 units from Wick at the moment the marker appears.
- The HUD shows how many waves are still to come (small pips by the storm timer).
- The stage clears only when no enemies are left and no waves remain. The last-pop slow motion plays only on the true last pop.
- The storm clock gets 15% more time on this recipe. If the storm starts, waves keep following the same rule. **(call)**
- At most once per world (the no-repeat rule already ensures this).

## Part 2: six new enemies

All six are bubbles: the harpoon pops them, they split into two smaller copies of the same kind, they score by tier and cost budget like other kinds. Numbers are starting values for Tuning.

| Kind | Home | Behaviour | Cost scale |
| --- | --- | --- | --- |
| **Clinger** | 2 Forest | Starts stuck to the underside of a platform or the ceiling, not moving. When Wick is below it and within 1.5 units sideways, it wobbles for 0.4 s, drops, then bounces like a Gloop of its tier for good. Can be shot while hanging, and hurts on touch while hanging. Children bounce normally. | 1 |
| **Sinker** | 3 Desert | Each landing lowers its bounce: to 75% on sand, 90% on any other surface. Once the rise is under 0.3 units it stops bouncing and rolls along the surface at its sideways speed, turning at walls and gap edges. Children start with a full bounce. | 1.25 |
| **Frost bubble** | 4 Winter | The ground column or platform tile it lands on becomes ice for 4 s, then returns to what it was. | 1.25 |
| **Ghost** | 5 Catacombs | Ignores platform tiles (still bounces on the ground and off walls and ceiling). Alternates solid 2.5 s and faded 1.5 s, flickering for the last 0.3 s of the fade. Faded: cannot be hit, cannot hurt. It does not turn solid while overlapping Wick. | 1.5 |
| **Ember** | 6 Lava | Each landing leaves a flame one tile wide for 1.5 s on that surface. The flame costs one heart, like spikes. S-tier Embers leave no flame. | 1.5 |
| **Drifter** | none | No gravity. Moves at a constant 5 u/s on a diagonal and reflects off walls, ground, ceiling and platforms. Children leave on mirrored diagonals. | 1.25 |

Rules that keep them fair **(calls)**:

- Clinger: under a platform only L or smaller hangs (an XL would reach Wick's head); XL only on the ceiling. When the storm starts, every hanging Clinger drops.
- Sinker: largest tier L, so a rolling one is never a wall that needs two pops to get past.
- Frost: never freezes the columns Wick starts on during the first 2 s of a stage.
- Ember: flames never appear over a gap; at most 6 flames alive at once (oldest goes out).
- Drifter: largest tier L; never starts on a line that passes within 3 units of Wick's start in its first second.
- Rooftops has no signature enemy. It stays the world for learning Gloops and Trigloops.

### Where they appear

- **Home world**: the signature enemy is introduced on stage 1 and dominates stages 2 to 5 as described above.
- **Guests**: from the world after its home, a kind can return as a guest. A normal stage has a guest with a 30% chance (40% in a hard world); at most one guest kind per stage; the guest takes at most a quarter of the budget; never on an Introduction stage.
- **Drifter** has no home. It is a guest in any world from world 2 on, and gets its own short introduction: the first time it appears in a run, it appears alone among Gloops. **(call)**
- Gloop, Trigloop, Lead, rollers, steel and ceramic keep their current rules and fill the rest.

### Look

Each kind needs a colour and a centre mark that reads at S size, plus: a faded Ghost sprite, the Ember flame, the wave warning marker and the wave pips. Six new bubble colours must stay clear of saffron, mint, pearl, the four existing bubble colours and each world's scenery. **Open: the colours and marks are Kokan's to approve.** Proposed as the first step of the enemy work: a small sheet of the six kinds at all tiers on every world's background, then `expansion-art.md` is updated with the approved values.

## Left to the builder

- How kinds get their behaviour in code. Suggested: keep one `Bubble` class and `BubbleKind`, with a small per-kind behaviour object that Bubble calls on tick, landing and pop, so popping, scoring, splitting, ceramic and debug spawning keep working through the existing paths. Flames follow the pattern of `Spikes`.
- How the Ghost skips platforms (a collision check against the arena bounds and ground only, in place of `StageMap.ResolveCircle`).
- How temporary ice is stored and redrawn (StageMap surface plus a timer list is enough).
- Exact tuning names and the test list. Minimum: one movement test per kind; generator tests over many seeds that every stage respects its budget, its recipe's rules, the fairness rules above and the no-repeat rule; a wave test that a stage does not clear while waves remain.
- The debug panel gets a spawn button per new kind (it already lists kinds) and a button to force each recipe.

## Suggested build order

Each step leaves the game playable.

1. Recipes and placement patterns with today's enemies (no waves yet).
2. Reinforcements: waves, marker, pips, clear rule.
3. Colour and mark sheet for the six kinds; Kokan approves.
4. Per-kind behaviour hook, then Drifter as the first user of it.
5. Clinger, Sinker, Frost bubble, Ghost, Ember, one at a time, each with its Introduction stage and Home turf share.
6. Guests.
7. Balance pass with LOG RUN and play on the phone: cost scales, shares, wave timing.
