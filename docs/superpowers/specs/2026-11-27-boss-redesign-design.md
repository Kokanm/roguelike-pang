# Boss Redesign: Design

Approved by Kokan on 2026-11-27 in the project thread "Rethink the bosses". Visual reference: the Bubblewright Boss
Sheet artifact (https://claude.ai/artifact/QBqkbCMeTjEgMTZ1xrGXkb, v3), also kept as
`/mnt/project-files/design/bubblewright-boss-sheet.html` in the project files. Where this file and the sheet differ,
this file wins. The build plan is `docs/superpowers/plans/2026-11-27-boss-redesign.md`.

## Why

The six bosses from expansion steps 17 and 18 (Chimney King, Old Bramble, Dune Warden, Frostmaw, Bone Abbot, Cinder
Tyrant) are one machine six times: a block with a visor sliding left and right near the ceiling, differing only in
numbers (`BossSpec.ForWorld`) and outline (`BossShape`). They fight in a flat empty arena, so wall jump, double jump,
crouch, ladders and storeys do not matter, and the best plan is to stand under the boss and fire. Kokan found them too
easy, boring, not Pang-like and plain to look at, and keeps none of them.

Kokan's brief: machines and creatures (not bubble bosses); arenas with platforms and walls, some with several storeys;
phases; 90 to 180 s fights; a bullet hell where you dodge and jump while hitting; bosses that enter from the sides or
from below so you jump and shoot down; bosses that break platforms; a puzzle in every fight, not just more damage and
more balloons.

## Shared rules (every boss)

1. **Only the core hurts.** Each boss has one or more cores: a cream disc inside a 1 px ring of the world's `accent`
   (Rooftops uses `tileCap` for the ring, since its accent is cream). Everything else on the boss is armour. A shot or
   harpoon that touches armour is used up, clinks (spark, short high `Sfx`), and does no damage. A core is only open
   some of the time; opening it is the puzzle. A closed core draws as armour.
2. **Phases.** Most bosses have 3 phases; the Crucible Forge has 5. The health bar is split into one segment per
   phase. When a segment empties: the boss staggers for `bossStaggerTime` (1.5 s, nothing moves or fires), every boss
   bullet clears, the arena changes for the next phase, and a weapon crate falls (any weapon from `WeaponDrops`).
3. **Hearts.** On the calm road a heart pickup also falls at each phase break (only when Wick is below his maximum,
   like breakable-block hearts). On the hard road no heart ever drops in a boss fight.
4. **They move.** No boss sits along the top. They walk, dive, swing, charge from a side, rise from below and fly.
5. **They break the arena.** Bosses can break any tile, including solid ones, and can add short-lived tiles (ice
   blocks, sand blocks) and remove floors. Shots keep breaking only breakable blocks, as today. Bosses are the only
   non-shot thing that breaks tiles.
6. **Every attack is told first,** at least 0.5 s ahead: a glow at the screen edge, a dust column on the floor, a lit
   lane on the ceiling, or a red shadow on the target surface.
7. **Balloons only from some bosses.** Most bosses release no bubbles. Those that do (Sootjack, the Crucible Forge)
   shoot ordinary Gloops out of a pipe at the top left, top right or top middle of the screen. The pipe flashes 0.5 s
   before. `bossMaxBubbles` still caps how many are alive.
8. **Hard road** keeps today's scaling (`hardBossHpScale`, `hardBossSpeedScale`, `hardBossRateScale`) and adds one new
   move per phase (listed per boss below).
9. **Storm clock.** `bossTime` becomes 240 s. The Crucible Forge has its own 300 s. The storm still speeds up the boss
   and its bullets (`bossStormScale`).
10. **Look.** Built from the world's own palette plus steel; visors, slits and grates, never eyes. No saffron, mint or
    bubble colours. 1 px ink outline. Hit flash: the core turns white for `bossHitFlash`.
11. **Health** below is counted in core hits with the starting harpoon (1 damage per hit). Weapons keep their own boss
    damage values.

## Bullet patterns

| Pattern | What it does |
| --- | --- |
| Aimed | Single, fan or burst at Wick (today's aimed bullet, cream core, accent ring). |
| Ring with a gap | A full ring outward from a point. One gap 3 bullets wide that turns a little each time. |
| Curtain with a lane | A row of falling bullets the full width of a storey with one clear lane, lit on the ceiling 0.6 s before. |
| Spiral | 2 to 6 arms turning around the boss, slow enough to walk through. |
| Ground wave | A slam or stomp sends a 4 px wave along the surface it hit, both ways. Jump it or be on another surface. |
| Arcing shell | Lobbed under gravity; may leave something where it lands (ice block, sand block). |
| Homing missile | Slow, turns toward Wick at a capped rate, dies after a few seconds or on a tile. |
| Sweep stream | A column (lava, sand jet, freeze beam) that sweeps sideways across a storey. |

## World 1: Sootjack (Rooftops)

Machine, the teaching boss. An iron stove on stilt legs that sweeps the chimneys. About 90 s. Health 6 + 6 + 8.

- **Enters:** bursts out of the big chimney at the back and lands with a stomp.
- **Arena:** one storey. Flat roof, two chimney stacks 2 wide and 4 tall, one high platform.
- **Core:** the belly grate. It is tall enough on its stilts for Wick to walk under the body.
- **Phase 1, Stilt walk:** walks left and right. Coughs three fans of soot from its pipe, then the grate glows for 2 s.
  Every stop is a stomp: a ground wave both ways. Hurt it: get under the belly while the grate glows and shoot up.
- **Phase 2, Chimney hop:** leaps between the chimney tops and the high platform and perches, firing a curtain with a
  lane. Each chimney it jumps off loses its top tile. Its pipe at the top middle drops Gloops. Hurt it: the grate
  faces down while it perches; shoot from inside the lane.
- **Phase 3, Flue run:** dives into the chimneys. One pot puffs, then it bursts out of that one firing a ring. Between
  pops it charges along the roof from a screen edge. Hurt it: stand under the pot that puffed; jump onto a chimney or
  double jump over the charge.
- **Puzzle:** count the coughs; the arena shrinks as it breaks the chimneys.
- **Hard road:** two coughs instead of three, every stomp sends two waves, in phase 3 two pots puff (one is a fake).
- **Weapons that shine:** Scattergun and Trimmer Blade under the belly, Pop Mine where it charges.

## World 2: Burrowmaw (Forest)

Creature. A giant root grub with bark plates. About 105 s. Health 7 + 7 + 8.

- **Enters:** from below; a dust column shows where, then it breaches.
- **Arena:** one storey. Soft earth floor between two big trunks you can wall-jump, branches at two heights, ladders.
- **Core:** the glowing sap sac on its tail (comes out last). In phase 2 the head can become a second core.
- **Phase 1, Breach:** dust column, then 1 s later it leaps out of the floor in a long arc and dives back in, spraying
  a pebble fan from the top of the arc. Its whole body hurts. Hurt it: the sap sac, from a branch with a down shot or
  from the floor right after the dive.
- **Phase 2, Strangle:** coils up a trunk and along the canopy, breaking every branch it crawls over. Spits seed pods
  that burst into rings with a gap. Hurt it: three thorn nuts hang from the canopy; shoot one onto its head and the
  bark cracks open, making the head a core for 4 s.
- **Phase 3, Sinkhole:** the floor caves in and is gone (a full-width gap; falling costs a heart, as gaps do). Only
  branches and ladders remain. It circles in the pit and lunges up through the gaps; dust falls from the edge it will
  use. Hurt it: down shots into the head as it lunges, and the tail on its way back down.
- **Puzzle:** drop the thorn nuts on its head; track which branches it has not crushed yet.
- **Hard road:** two dust columns at once in phase 1 (one fake), seed rings in pairs, one fewer thorn nut.
- **Weapons that shine:** Pop Mine on the dust column, Skyrocket and Torch fired down at the lunges.

## World 3: The Sandglass (Desert)

Machine. A brass hourglass engine hanging over the dunes; it is the stage clock. About 120 s. Health 8 + 8 + 10.

- **Enters:** lowered from the top on a rod.
- **Arena:** one storey. Sand floor (slow walk), two high platforms near the walls, a counterweight at each wall on a
  chain that runs horizontally over a pulley to the top of the hourglass (shot from below).
- **Core:** the valve in its neck.
- **Phase 1, Pour:** sand pours out into a dune in the middle of the floor that grows and slows Wick more. Four
  turrets on its frame fire a slow spiral. Hurt it: cut both chains within 3 s of each other; it flips, stunned for
  5 s, the neck core opens, the dune pours back in, and the storm clock gains 20 s.
- **Phase 2, Spin:** free of the chains it spins slowly and drifts. Four sand jets turn with it; every quarter turn it
  drops a sand block that sticks as a platform and crumbles after 5 s. Hurt it: the core only faces Wick when it lies
  flat; climb the sand blocks to reach it.
- **Phase 3, Scarab:** the glass shatters and the core runs out as a brass scarab, racing around the room's edges
  (floor, wall, ceiling, wall) and dropping glass shards while on the ceiling. Platforms it runs over crack. Hurt it:
  on the floor jump it; on the ceiling it is above you.
- **Puzzle:** cut both chains in time (the Vee Harpoon cuts both at once); the dune and the clock make waiting costly.
- **Hard road:** chains within 1.5 s, the spiral has six arms, the scarab turns back once per lap.
- **Weapons that shine:** Vee Harpoon or Double Harpoon for the chains, Ricochet Disc for the scarab.

## World 4: The Whiteout (Winter)

Machine. A huge frost siege tank, about half the screen wide (sheet v3; replaces the "Icebreaker" plough engine of v1
and v2). Armoured skirts over its treads, ice ram spikes on the front, a twin cryo cannon, a missile pod, and a reactor
on the back of the turret. About 135 s. Health 8 + 10 + 10.

- **Enters:** smashes through a side wall; its sensor slit glows on that edge first.
- **Arena:** one storey. Ice floor (Wick slides), ledges at two heights, a high platform, one ladder.
- **Core:** the reactor on the back of the turret.
- **Phase 1, Assault:** crashes in through a wall, crosses the whole floor crushing any low ledge in its way, then
  comes back from either side. The turret tracks Wick and fires frost shells in pairs. Front and treads are armour.
  Hurt it: shoot down at the reactor as it passes beneath, or from behind once it has gone by.
- **Phase 2, Siege:** anchors at one side. A freeze beam sweeps the room; anything it touches becomes ice blocks (walls
  and steps that melt after 10 s). The pod fires slow homing ice missiles. Hurt it: lure the beam across its own three
  coolant pods; a frozen pod cracks, and with all three cracked the reactor overheats and opens for 4 s.
- **Phase 3, Overdrive:** races back and forth without leaving the screen, faster each pass; the turret spins and fires
  a spiral; every surface its treads touch turns to ice. The reactor stays open, but a heat blast goes straight up from
  its exhaust: shoot behind the exhaust, never over it.
- **Puzzle:** steer its freeze beam onto its own coolant pods; pick ledges it cannot crush.
- **Hard road:** two charges in a row from opposite sides, missiles in threes, frost shells in phase 3 too.
- **Weapons that shine:** Pop Mine on its track, Lamp Lance fired down as it passes.

## World 5: The Deep Bell (Catacombs)

Machine, three storeys. An enormous upturned bell on a lift, rising out of the deepest crypt. About 150 s. Health
10 + 10 + 12. One phase per storey; the hatch to the next storey opens when a phase ends.

- **Enters:** from below, breaking up through the bone-brick floor.
- **Arena:** three storeys. Bone-brick floors it breaks, ladders, wall-jump shafts, a hatch up to the next storey.
- **Core:** the clapper.
- **Phase 1, Rising (storey 1):** rises through the floor mouth up. Each toll fires a ring with a gap and lifts it one
  tile; each core hit sinks it half a tile. If it reaches the ceiling it hurts Wick and drops back. Hurt it: stand
  above the mouth and shoot down at the clapper.
- **Phase 2, Lights out (storey 2):** the lamps go out. Four wall lamps light for 8 s when shot. The bell hunts in the
  dark and can only be seen and hurt inside lamp light; its rings still toll (the bullets glow).
- **Phase 3, Belfry (storey 3):** hangs from chains and swings like a pendulum, mouth down. Each swing fires a ring and
  smashes the platforms at both ends of the arc. Hurt it: stand under the bottom of the swing and shoot the clapper as
  it passes over.
- **Puzzle:** tug of war in storey 1, lighting lamps in storey 2, timing the swing in storey 3.
- **Hard road:** tolls lift two tiles, lamps last 5 s, each swing fires two rings.
- **Weapons that shine:** Lamp Post and Lamplighter Torch light the dark, Gas Flask dropped into the mouth.

## World 6: The Crucible Forge (Lava), final boss

Machine, five storeys, five phases, the longest and hardest fight ("an epic battle"). The furnace that heats the
whole run. About 240 s, storm clock 300 s. Health: two arms of 5, then core 12 + 14 + 18 (phases 2, 4 and 5; phases 1
and 3 are won by breaking parts). Lava rises from the bottom of the arena through the whole fight and stops at the
bottom of storey 5. Touching the lava costs a heart and throws Wick up.

- **Enters:** it is already there; the back wall wakes up and the lava starts to rise.
- **Arena:** five storeys of basalt platforms, shafts, ladders and spike strips. The hatch to the next storey opens
  when a phase ends.
- **Phase 1, Hammers (storey 1):** two arms from the side walls slam down on platforms or the floor, aimed at where
  Wick stands (red shadow first). A slam breaks the platform, sends ground waves and a spark fan, and a spike strip
  rises where it was. Hurt it: each elbow glows for 2 s after a slam and has its own small bar; break both.
- **Phase 2, Pour (storey 2):** the crucible tips and pours lava streams that sweep the storey, with aimed molten drops.
  Gloops come out of the pipe at the top middle. Hurt it: three valves on the walls steer the pour; turn it into the
  cooling vat and the steam cracks the crucible open, so the core shows for 4 s.
- **Phase 3, Bellows (storey 3):** three bellows on the back wall blow fire columns up through the gaps in the floor;
  the columns follow Wick's jumps. Hurt it: shoot each bellow's grill while it is between breaths; with all three
  silent, the phase ends.
- **Phase 4, Quench (storey 4):** the core drops out and flies up the shafts, landing on platforms. Gloops drop from the
  pipes at the top left and top right, and a spiral turns between landings. Hurt it: shoot the core each time it lands;
  each landing opens it for 3 s.
- **Phase 5, Heart (storey 5):** full bullet hell: a turning spiral, aimed bursts and rings, faster as its health
  falls. The lava has reached the bottom of this storey, so the last platforms are the only ground. It is all core.
  Double jump and wall jump are needed to reach it.
- **Puzzle:** choose which platforms to lose in phase 1 (slams follow you); lure the fire columns off a gap to climb
  through; watch the lava line, which is a clock.
- **Hard road:** no hearts (rule), bellows breathe faster, both hammers slam together, phase 5 adds a second spiral
  turning the other way.
- **Weapons that shine:** Arc Coil between both elbows, Gas Flask into the cooling vat, Rattler and Twin Carbine for the
  core.

## What changes in the game's rules

- `StageMap` and the validator allow up to 5 storeys, for boss arenas only. Ordinary stages stay at 1 to 3.
- Boss stages become authored arenas (one per world), not `StagePicker.Arena("Boss")`.
- On a multi-storey boss stage the hatch opens when a phase ends, not when the storey's bubbles are cleared. Wick still
  never goes back down.
- Bosses may break solid tiles, add short-lived tiles, open floor gaps and raise lava. Shots still break only
  breakable blocks.
- Boss contact still costs `bossContactDamage`; boss bullets cost 1 heart (`stormDamage` in the storm), as today.
