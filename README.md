# Roguelike Pang (working title: Bubblewright)

A prototype of a roguelike game inspired by Pang (bubble-splitting arcade action), built in **Unity 2D (URP)**.

Platforms, in priority order: Mobile, then Desktop, then Console.

## Goal of this prototype

Nail the design, animations and movement before building the polished game in a separate project.

Plan:
1. Visual direction and design sheet (done: [design sheet](https://claude.ai/artifact/1gTaySmyLvPqnkRrDUxyZ5))
2. Repo and movement prototype (this)
3. Animation and game-feel pass
4. Thin roguelike loop (procedural stages, permadeath, upgrades between levels, meta-progression)

## What step 2 contains

One stage of World 1 (Rooftops) built entirely from code, using the numbers from the design sheet:

- **Wick** runs, climbs ladders and hops about 1.9 units high (0.56 s in the air; one jump never reaches the next platform). In the air he can fire up, or hold Down and fire straight down (the cable stops on the floor or platform below and breaks a breakable tile it lands on). Touching the arena wall or the side of a block in the air, a jump kicks him up and away (wall jump); pushing into the wall while falling slows him to a slide. Sliding, and for a moment as he kicks off, he turns to the wall with one hand and one boot flat against it. Chained wall jumps can get him onto platforms. The rare **Updraft** card adds one double jump, given back on landing, on a ladder or by a wall jump. Holding Down on the ground (with no ladder below) crouches: 60% height, a lower launcher and a slow shuffle. Numbers are in Tuning > Jump. Updraft is one of the 9 upgrade cards. From world 2 on some stages have gaps in the ground: falling in costs a heart and puts him back at the edge. Running: 9 u/s top speed, reached in 0.06 s, stops in 0.04 s. He plays with his back to the camera, faces it at stage start, turns with a 0.2 s squash, and turns back to celebrate after the last pop. His candle flame shows his 3 hearts.
- **Harpoon**: one shot at a time, straight up from the launcher tip (+0.35 u right of centre) at 20 u/s. A 1 px twisted steel cable stays where it was fired; a bubble touching the cable or the barbed tip pops. The tip stops under the first solid tile above it (a platform or the ceiling) and the tail hangs down to the first surface below.
- **Bubbles**: kinematic, with our own bounce maths (no physics engine). Constant 4 u/s sideways. Each tier always peaks at its height from the sheet (XL 13, L 11, M 8.5, S 6 u). They split into the next size (Trigloops into 3), kicked up and out. No eyes: type reads from colour plus a centre mark.
- **Screen**: renders at 480 x 270 with the URP Pixel Perfect Camera (15 px per unit, upscale render texture). The arena is always 32 x 14 u with a 4 u ground strip; wider or taller screens show extra scenery. Phones are locked to landscape.
- **Controls**: touch first, plus keyboard and gamepad (below).
- Placeholder pixel art is painted in code from the sheet's shapes and palette (`PlaceholderArt.cs`), so there are no image files yet.

Not in step 2 on purpose: ledges, the storm timer, other weapons, upgrades, audio, and most of the animation list (that is step 3).

## What step 3 adds (game feel, in progress)

Timings come from the sheet's animation table and live in `Tuning.asset` under **Feel**, **Wick animation** and **Sound**.

- **Wick**: run start stretches 0.9 x 1.1 and leans 8° with two dust puffs; stopping or reversing from speed skids (lean back 6°, one puff, 1.05 x 0.95 settle). Firing kicks the launcher down 2 px, squashes him and shows a one-frame mint muzzle flash. The launcher is now its own sprite part.
- **Pops**: a hit bubble swells to 115% during the 40 ms hit-stop, then one cream flash frame, the ring, droplets and the children.
- **Hurt**: Wick shows as a cream silhouette through the 120 ms freeze, then blinks.
- **Intro and clear**: he waves and rocks while facing you, the turn swap hides behind a dust puff, the celebration is two hops (stretch up, squash and dust on landing, arms up on the second) with confetti, and STAGE CLEAR drops in with a bounce.
- **Sound**: placeholder chiptune blips synthesised in code (`Scripts/FX/Sfx.cs`): fire, pop (higher for smaller bubbles), bounce, hurt, skid, turn, stage clear, game over. Volume and mute are in the tuning asset.

## Open it on desktop

1. Install **Unity Hub**, then **Unity 6000.6.7f1**. Add the modules **Android Build Support** (with OpenJDK and SDK/NDK) and, on a Mac, **iOS Build Support**.
2. Install Git LFS once per machine: `git lfs install`. Then clone this repo.
3. In Unity Hub: **Add > Add project from disk**, pick the repo folder, open it. If Hub offers a slightly different version, accept it. The pinned editor is 6000.6.7f1.
4. On first open:
   - If Unity asks to **enable the new Input System backends and restart**, click **Yes**. (Either answer works; the code supports both.)
   - The project setup runs by itself: it creates the URP 2D renderer, the tuning asset and `Assets/Scenes/Prototype.unity`. The Console says `[Bubblewright] Project setup done`. You can re-run it any time from the menu **Bubblewright > Run Project Setup**.
5. Open `Assets/Scenes/Prototype.unity` (or **Bubblewright > Open Prototype Scene**) and press **Play**.
6. In the Game view, pick a landscape size such as **1920x1080** or **16:9**. For phone shapes, switch the Game view tab to **Simulator** and pick a phone; it simulates touch too.
7. After the first open, commit what Unity generated (`Packages/packages-lock.json`, `ProjectSettings/*`, `Assets/Bubblewright/Settings`, `Assets/Bubblewright/Resources`, `Assets/Scenes`) so the project is reproducible.

### Controls

| Action | Touch | Keyboard | Gamepad |
| --- | --- | --- | --- |
| Move | Drag left or right anywhere in the left half (relative to where your thumb lands) | A / D or Left / Right | Left stick or d-pad left / right |
| Climb (Down on the ground with no ladder: crouch) | Drag up or down anywhere in the left half | W / S or Up / Down | Left stick or d-pad up / down |
| Fire (hold to keep firing) | Hold anywhere in the right half (the mint button) | Space, J or K | A / Cross or X / Square |
| Weapon (only while Wick holds one; Down in the air fires down) | Tap the third mint button, left of the jump button, with the weapon icon and ammo | I or U | RT or RB |
| Jump (also off walls; fire works in the air, Down + fire shoots down) | Tap the round arrow button left of the fire button | L, Left Shift or Right Shift | B / Circle or Y / Triangle |
| Menu / continue / pick a card or a door | Tap (doors: walk in, or tap one) | Arrows or WASD, then Enter, Space or R (cards: 1-3); doors: walk in, or 1-2 | Left stick or d-pad, then A or Start; doors: walk in |
| Back (character select) | Tap a card to start; no back button | Esc | B / Circle |
| Pause | Top-right pause button | Esc or P (F2 opens debug) | Start |
| Toggle test layout (debug) | Debug panel | F3 or debug panel | Debug panel |

The mouse acts as one finger, so you can try the touch controls on desktop: click and drag in the left half, click in the right half.

**Characters.** NEW RUN opens a select screen with four heroes: Wick (3 hearts, the all-rounder), Ember (2 hearts, fast), Moss (4 hearts, slow) and Flint (two cables, slow shots). Wick and Ember are open from the start; finishing the whole run with Wick unlocks Moss, and with Ember unlocks Flint. All stats live in one table in `Scripts/Player/Character.cs`; Wick's row follows `Tuning`. The debug panel has UNLOCK ALL (this session only) and RESET UNLOCKS.

Debug keys on desktop: **1-4** spawn an XL Gloop, Trigloop, Lead or Lucky bubble; **5** spawns a big rolling balloon on the ground, **6** a steel one, **7** a ceramic Gloop, **8** a ceramic rolling balloon; **9** drops a weapon crate of the next weapon above Wick; **F1** shows hitboxes; **F3** cycles through all 14 test layouts and shows the current layout name in the HUD. The debug panel's **SURFACE** button turns the whole stage normal, all ice, then all sand, to tune the feel in any world (numbers in Tuning > Surfaces). **SPIKES** adds Lava's retracting strips to the current stage in any world. The **THEME** button cycles the look of the current stage (sky, hills, tiles, ground strip) through the six world themes without changing the world.

### Rolling balloons

From world 2 on, about half of the normal stages also start with a pearl **rolling balloon** (one, or two from world 4). On the ground it rolls back and forth, turning at walls, solid tiles, gaps and other rolling balloons. On a platform it rolls around the whole platform: along the top, over the end, along the underside, up the other end and back, always touching the tiles; it turns at the arena walls and ceiling and at other rolling balloons. If the tile it rests on is broken it drops to the next surface (or the ground) and carries on. It ignores ice, sand and spikes. Touching it costs a heart (two in the storm, which also speeds it up). Jump over it, or shoot it: a big one (18 px) splits into two small ones (12 px) rolling apart, a small one is gone. A stage is cleared only when the bubbles and the rolling balloons are all gone. Boss stages never have them. Each big one costs 3 points of the stage's bubble budget. Numbers are in Tuning > Rolling balloons.

**Steel and ceramic.** From world 3 on, some stages also have a **steel rolling ball** (dull grey with dark bands): it cannot be destroyed, a harpoon just rings off it, it costs a heart on touch, and it does *not* have to be removed to clear the stage. Also from world 3, some bubbles and some big rolling balls are **ceramic** (the usual colour washed toward cream, with a lilac rim): they take 2 harpoon hits, cracking on the first, and the pieces they leave are ordinary. A ceramic one costs 1.5 times the budget. Debug keys: **6** steel roller, **7** ceramic Gloop, **8** ceramic roller. Numbers are in Tuning > Steel and ceramic.

### Balancing

The debug panel (F2) has a **BALANCE** page: world and stage, layout, calm or hard, the bubble budget and how it was spent (bubbles per kind and size, rolling balloons), the time limit, spike strips, gaps and breakable tiles. **LOG RUN** prints the same numbers for all 36 stages of the current seed to the Console as tab-separated text, ready to paste into a spreadsheet. **WIN STAGE** clears the stage at once and **NEXT LAYOUT** re-rolls only the layout. Since jumping arrived, every bubble budget is multiplied by Tuning > Balance > Jump Budget Bonus (1.1), spikes stay down 2.6 s and up 1.8 s, and boss bouncy bullets hop 2.6 units high.

### Boss stages

The last stage of each world is a boss stage: one flat floor, no generated bubbles, and that world's boss. It slides in from above during the intro (the banner shows its name), then walks left and right near the ceiling and attacks. Any harpoon touch (tip or cable) takes 1 health; health is 12 in world 1 and 4 more in each later world, and the bar at the top shows the boss's name. Bullets and touching the body each cost a heart (bullets cost more in the storm). The storm clock runs on boss stages too (120 s), and the storm speeds up the boss and its bullets. When its health reaches 0 any leftover bubbles pop without children, all bullets vanish, the boss shakes and bursts, and the world is cleared.

A boss mixes three attacks, each on its own timer (the first aimed shot comes 1.0 s after it slides in, the first bouncy bullet at 1.8 s, the first balloon at 3.0 s):

- **Aimed bullets** fly straight at where Wick was: one, a fan of three across 30 degrees, or a burst of three 0.25 s apart. The boss's lower band blinks cream half a second before each aimed attack.
- **Bouncy bullets** drop from the boss, hop along the floor toward Wick's side a set number of times (the top of each hop is only 2.6 units up, so Wick has to jump it or outrun it), bounce off the walls, then fall through the floor.
- **Balloons**: the visor opens and an ordinary Gloop (L or M) drops out. It behaves, splits and scores like any bubble. No balloon comes while six bubbles are alive.

| World | Boss | Size | Speed | Aimed | Bouncy | Balloons |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | CHIMNEY KING | 4 x 3 | 3.0 | 1 every 2.0 s | none | M every 8 s |
| 2 | OLD BRAMBLE | 5 x 2.5 | 3.5 | none | every 2.2 s, 2 bounces | M every 7 s |
| 3 | DUNE WARDEN | 3.5 x 3.5 | 4.0 | fan of 3 every 2.5 s | none | L every 9 s |
| 4 | FROSTMAW | 4.5 x 3 | 4.0 | 1 every 3.5 s | every 1.8 s, 3 bounces | M every 8 s |
| 5 | BONE ABBOT | 3 x 4 | 4.5 | burst of 3 every 3.0 s | every 2.5 s, 1 bounce | L every 8 s |
| 6 | CINDER TYRANT | 5 x 3.5 | 5.0 | fan of 3 every 2.2 s | every 2.0 s, 4 bounces | L every 6 s |

CINDER TYRANT enrages below half health: its speed and every attack rate go up by 1.3 times. These six are placeholders; the whole table is `BossSpec.ForWorld` in `Scripts/Bosses/BossSpec.cs`, and the shared numbers are in Tuning > Bosses. On a boss stage the debug panel has **AIMED**, **BOUNCY** and **BALLOON** buttons that fire one attack, and **BOSS -5 HP** takes off five health.

### Exit doors and the left or right road

Every ordinary stage ends at a door: when the last bubble pops, a door appears on the right of Wick's storey (the right-most spot he can reach) fades in and creaks open slowly during the celebration. After the upgrade pick he walks, climbs or jumps to it (nothing can hurt him now, and he cannot fire), stands in the doorway and presses UP to go through; the screen fades and the next stage starts, which is when the run is saved. The boss of each world except the last (stages 6, 12, 18, 24 and 30) has two doors instead, standing shut, unmarked, dimmed and see-through in the next world's stone behind the whole fight (so a platform in front of one does not hide anything). When the boss falls they fade in and creak open and show their marks, and after the pick each gets a card. One of them, chosen at random from the run seed, is the hard road for the whole next world: it carries a random debuff (never the one the finished world had), and its card spells out the numbers and the reward. The other door changes nothing. Stand at the one you want and press UP, or press 1 (left) or 2 (right), or tap or click a door or its card and Wick runs to it. The final boss has no doors: the run is complete. A row of six squares at the top shows the worlds so far (mint calm, lemon hard, outline not reached, cream bracket on the world in question); it appears on this screen and on the pause screen. The numbers live in `Tuning` under **Doors** and **Hard worlds**. The code is in `Scripts/Core/Game.Path.cs`, the door art in `Scripts/Art/DoorArt.cs`.

### Weapons (framework)

A popped bubble (5%) or a broken block (10%) can drop a **weapon crate**: a mint-ringed box with the weapon's icon. It falls to the floor, stays 8 s and blinks for the last 2. Walking over it takes it. At most 2 crates a stage, never while one is on screen, none in stages 1-1 and 1-2; uncommon weapons from world 2, rare and very rare from world 3. Wick has two slots: the **weapon slot** (guns and gadgets, fired with the weapon button; the harpoon keeps working on fire) and the **harpoon slot** (heads that change what fire shoots). The same weapon again tops its ammo up to 1.5 loads; a different one replaces it. Ammo carries over between stages and is saved at the start of each stage. The HUD shows the icon and ammo right of the hearts. Numbers are in Tuning > Weapons.

Pause lists every item you have picked in full, with its icon, name, stack count or ammo, and on desktop the keyboard and pad controls.

No real weapon is in yet, so nothing drops in normal play. The debug panel's **GIVE WEAPON** button and key **9** give or drop the debug **Test Shot** (20 spark bullets, one per press). Every weapon follows the shared hit rules in `Scripts/Weapons/WeaponWorld.cs`: steel sparks and stops a shot, ceramic takes one hit of two, a faded Ghost lets it through, a breakable block takes one hit, and boss damage adds up in fractions.

**Adding a weapon** touches no shared list: add a sealed `[Preserve]` subclass of `WeaponSpec` in its own file under `Scripts/Weapons/` (copy `TestWeapon.cs`), with its pixel maps, a behaviour inside it, and its numbers in a `partial class Tuning` under a "Weapon: Name" header. `WeaponSpec.All` finds it by itself, and its icon, crate, button and in-hand sprite are painted from its pixel maps.

**Trimmer Blade** (W05, common, weapon slot, 30 swings): a round melee swing from his left side, over his head and down his right side, 2.2 units from his hand, so it reaches the floor on both sides. The blade sweeps across that arc in 0.24 s, and a balloon is hit when the blade reaches it. Tap or hold (one swing every 0.45 s); the next swing comes back the other way. Solid tiles shield what is behind them, steel clangs and a steel rolling ball is turned away, boss bullets the blade meets are cut, and a boss the blade reaches takes 1 health per swing. Falling with Down held, the arc covers both sides and below him. Numbers in Tuning > Weapon: Trimmer Blade; code in `Scripts/Weapons/TrimmerBlade.cs`.

**Lamp Lance** (W06, very rare, weapon slot, 1 charge): one tap winds up for 0.35 s (he cannot move), then a beam one tile wide runs from the lance to the ceiling for 0.5 s (down to the floor with Down held in the air). Everything that touches it is removed whole with no smaller bubbles: steel, ceramic, Ghosts, rolling balloons and boss bullets too. It passes through platforms, destroys every breakable block in the column, melts Frost ice, and takes 6 boss health once per beam. In the air he hangs still for the whole shot. Only one can be held: a second crate stays on the floor. Numbers in Tuning > Weapon: Lamp Lance; code in `Scripts/Weapons/LampLance.cs`.

**Twin Carbine** (W01, common, weapon slot, 30 bullets): one spark bullet per press from two barrels in turn, with no limit on bullets in the air. Tapping fires up to 8 a second, holding fires 4 a second. Three bullets take 1 boss health. Numbers in Tuning > Weapon: Twin Carbine; code in `Scripts/Weapons/TwinCarbine.cs`.

**Double Harpoon** (W10, common, harpoon slot, 12 volleys): the normal fire button launches two harpoons at once, 1.6 units apart and centred on Wick (Flint gets a third in the middle). The next volley waits until all its cables are gone; each Twin Cable pick allows one more volley in the air. The cables are ordinary harpoons, so upgrades, steel, ceramic, tiles and down shots work as usual, and a volley takes at most 1 boss health. Numbers in Tuning > Weapon: Double Harpoon; code in `Scripts/Weapons/DoubleHarpoon.cs`.

### Tuning

Every number lives in `Assets/Bubblewright/Resources/Tuning.asset`. Select it and edit in the Inspector, even while playing. Bubble gravity is derived per tier from its peak height and a bounce time, so it always reaches the sheet's peak height.

## Test it on a phone

**Android** (easiest):
1. On the phone: enable Developer options and **USB debugging**, then plug it in.
2. In Unity: **File > Build Profiles**, select **Android**, click **Switch Platform**.
3. Make sure `Assets/Scenes/Prototype.unity` is in the scene list, then **Build And Run**. Pick a file name for the APK; Unity installs and launches it.

**iPhone** (needs a Mac with Xcode):
1. **File > Build Profiles**, select **iOS**, **Switch Platform**, then **Build**.
2. Open the generated Xcode project, choose your Apple ID team under Signing, plug in the phone and press Run.

Hold the phone sideways: left thumb drags to move, right thumb holds the mint button to fire.

## Project layout

```
Assets/Bubblewright/
  Scripts/Core/      Game (bootstrap and loop), Tuning (all numbers), GameInput (touch/keys/pad), Palette and sorting layers
  Scripts/Player/    Wick: movement and all his procedural animation (run, skid, fire, hurt, turn, wave, celebrate)
  Scripts/Bubbles/   Bubble: own bounce maths, split children
  Scripts/Weapons/   Harpoon: cable and barbed tip; temporary weapons: WeaponSpec (the table), WeaponWorld (shared hits and Shots), one file per weapon
  Scripts/FX/        Effects (pop, dust, muzzle flash, confetti) and Sfx (placeholder sounds made in code)
  Scripts/UI/        Thumb controls drawn in the ground strip
  Scripts/Art/       PixelCanvas and PlaceholderArt: sprites painted in code at 15 px per unit
  Editor/            ProjectSetup: URP 2D renderer, tuning asset, player settings, scene
ProjectSettings/TagManager.asset   Sorting layers: Sky, Stage, Bubbles, Shots, Player, FX, UI
```

Units: 1 unit = 15 art pixels. Floor at y = 0, ceiling at y = 14, walls at x = -16 and +16. The ground strip is y = -4 to 0.

## Troubleshooting

- **Package errors on open** (a version in `Packages/manifest.json` not found): open **Window > Package Manager**, install **Universal RP** and **Input System** at the versions it offers, then run **Bubblewright > Run Project Setup**.
- **Sprites look blurry or the screen is not pixel perfect**: check that **Edit > Project Settings > Graphics** uses `Assets/Bubblewright/Settings/URP-2D.asset`, and that the Main Camera has a Pixel Perfect Camera set to 15 PPU, 480 x 270, Grid Snapping = Upscale Render Texture. Re-running the setup fixes both.
- **Nothing in the Game view before pressing Play**: expected. Everything is built when the game starts.

## Setup notes

- Unity's `.gitignore` is in place. Git LFS rules for images, audio, fonts, models, video and archives are in `.gitattributes`.
- Progress tracker: `docs/prototype-tracker.html`. Live version: https://claude.ai/artifact/1v9Jo761zKs3VKdJ1wcpE3
