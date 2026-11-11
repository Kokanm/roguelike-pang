# Bubblewright expansion art (approved 2026-11-11)

Source of truth for the look of worlds, hazards, doors, characters, bosses and the rolling balloon. Prompts for steps 10 to 12, 14, 16 to 18 and 20 read this file; where it differs from a prompt, this file wins. Visual reference: the "Bubblewright Expansion Sheet" artifact (https://claude.ai/artifact/NCXiCq87VRnmovtmZKxBBM).

Rules for all art: 480 x 270 game pixels, 15 px per tile, 1 px ink outline (`1A1633`) on the outside of sprites, flat colours and hard edges only, every world is a night scene. Scenery never uses saffron, mint or the four bubble colours. Wick plays with his back to the camera.

## Theme palettes (hex)

| World | skyTop | skyBottom | far | near | tileFill | tileCap | tileShade | tileLine | breakFill | accent | strip |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 1 Rooftops | 1A1633 | 433D85 | 2B2757 | 1A1633 | 433D85 | 8F88D1 | 2B2757 | 1A1633 | 6E67B0 | FFF3D6 | 1A1633 |
| 2 Forest | 0C1C1A | 1E3F36 | 16302A | 0A1614 | 3B5A3A | 6FA35A | 24382A | 0A1614 | 5E7F52 | D8F27A | 0A1614 |
| 3 Desert | 1E1430 | 5A2E4A | 5C3644 | 2C1A2C | 8C6A55 | **B08A72** | 5E4238 | 20121F | A88468 | F2D9B0 | 20121F |
| 4 Winter | 101A33 | 1F3358 | 294168 | 16264A | 51658F | DCEBFA | 34456B | 0C1428 | 7388B0 | F2FBFF | 0C1428 |
| 5 Catacombs | 120F1A | 2A2233 | 332A3F | 1A1522 | 6E6478 | A79FB0 | 463E50 | 120F1A | 8C8296 | 9FD4FF | 120F1A |
| 6 Lava | 17090E | 4A1620 | 5C1F26 | 240C12 | 4A3A44 | 8E7885 | 2E222A | 17090E | 66535E | C22A1F | 17090E |

`breakLine` = `tileShade` in every theme. Desert `tileCap` is B08A72 (the plan had C9A27A, which is the sand colour). Sky is drawn as 6 flat bands from skyTop to skyBottom.

## Surfaces (same in every world)

- Ice: cap `9FDCFF` (3 px), glint `F2FBFF` 2 px long at x = 3 and x = 10 of each tile, row under the cap `5FA8E0`. (Plan had BFE6FF and 7FB4E0, too close to Winter snow.)
- Sand: cap `C9A27A`, wavy top (alternate columns 1 px lower, drawn in tileLine), grains `8C6A55` at 4 fixed spots per tile.
- Ladder: two 1 px rails `9AA4BC` at x = 3 and x = 11, 1 px rungs `DCE2EE` every 4 px. Steel in every world.
- Breakable block: no cap, `breakFill` with a lighter top row, brick joints in `tileShade`, `tileLine` outline. Cracked state: jagged 1 px ink line top to bottom. Heart drop: the flame sprite in a 1 px mint ring.
- Spikes: hidden = five 1 px ink holes; warning = five 2 px steel tips; raised = five 7 px tall, 3 px wide steel triangles (`9AA4BC`, left edge `DCE2EE`), ink outline. In Lava tint steel 25% toward `far`.
- Gap in the ground: darker pit in the ground strip with a 1 px lit edge (`tileShade`) on each side.

## Per-world scenery and tile detail

- Rooftops: unchanged from the current game.
- Forest: pale moon (14 px disc, accent mixed 50% with skyBottom) in the upper left behind a trunk; far pine silhouettes 30 to 70 px; big near trunks at the screen edges only with stubby branches; bushes along the floor; about 18 fireflies (accent pixel plus a dimmer pixel) in the lower half. Tiles: moss cap with 1 px drips 1 to 3 px long at 3 fixed spots per tile.
- Desert: dusk. Half-set sun (40 px disc, far mixed 30% toward accent, two 1 px skyBottom bands cut across it) behind the dunes; far dunes with a 1 px crest line (far mixed 25% toward accent); near dunes, 3 to 4 cactus, one ruined column; about 30 stars in the top third (accent 60% toward sky). Tiles: sandstone with a 1 px tileShade joint down the middle of every second tile and a horizontal one 8 px down.
- Winter: three thin aurora ribbons in the top third (accent mixed 80% toward skyTop, 2 px tall, wavy); far jagged mountains with snow caps (top 6 px far mixed 50% toward accent); near snowy hills and 5 to 6 small pines with a snow line on each tier; about 40 still snow dots. Tiles: lumpy snow cap (alternate 2 and 3 px) with two 2 px icicles per tile.
- Catacombs: no sky, stone wall in 6 bands; tall pointed arches (about 60 x 110 px) with skyTop inside, separated by 12 px pillars; shelves of small dim skulls (5 x 5 px, far mixed 30% toward tileCap, skyTop eye dots) in the lower third; 6 pale blue wall lamps on pillars; a few 1 px cracks. Tiles: bone-stone with a 1 px tileShade chip off every third tile's top corner and a 3 x 3 skull mark on every fifth.
- Lava: notched volcano slightly right of centre; smoke column of 6 to 10 px blobs drifting left; black near rock spires at both sides; lava band in the bottom fifth (accent shaded to 60%, 1 px accent line on top, small 2 px bubbles); about 20 ember pixels. Glow stays red and in the bottom fifth. Tiles: basalt with two 1 px vertical tileShade cracks and a faint red (accent shaded to 70%) line along the bottom of platform tiles.

## Doors (step 14), 30 x 45 px each

Arched frame in the next world's `tileCap` with `tileFill` stone and `tileShade` joints, ink outline, opening in `skyTop`. Calm door: small cream lantern above the arch. Hard door: 9 x 8 px lemon warning triangle with an ink exclamation mark above the arch, and three small steel spikes along the top of the frame. Two doors at x = -7 and x = +7, Wick between them facing the camera.

## Characters (step 16), 23 x 30 px, same hitbox for all

| Id | Name | body | bodyDark | bodyLine | Shape tell |
| --- | --- | --- | --- | --- | --- |
| wick | Wick | FFA53A | D9782A | B85A1C | Plain round hood |
| ember | Ember | FF8FB3 | D9648C | A8456A | Slimmer cloak, scarf tails flying left |
| moss | Moss | 8FCB6B | 62A04A | 3F7A33 | Wider cloak, broad hat brim |
| flint | Flint | B9C2E0 | 8892B8 | 5F688F | Goggle strap, a launcher in each hand (two cables) |

Flame on the head, mint cloak clasp and the launcher steel are the same for everyone. Saffron is Wick's colour only.

## Bosses (steps 17 and 18)

Each boss is built from its world's colours: `tileFill` body, `tileCap` band across the top third, `skyTop` visor slit (60% of the width), three rivets in `tileShade`, ink outline. No eyes, no saffron, mint or bubble colours.

| World | Name | Size (tiles) | Health | Shape |
| --- | --- | --- | --- | --- |
| 1 | Chimney King | 4 x 3 | 12 | Block with a brick chimney (crown rim of three notches) on top |
| 2 | Old Bramble | 5 x 2.5 | 16 | Low wide rounded body, five triangular thorns along the top |
| 3 | Dune Warden | 3.5 x 3.5 | 20 | Round disc with a flat band and the visor across the middle |
| 4 | Frostmaw | 4.5 x 3 | 24 | Block with six downward icicle teeth |
| 5 | Bone Abbot | 3 x 4 | 28 | Tall narrow body, pointed hood, rib lines |
| 6 | Cinder Tyrant | 5 x 3.5 | 32 | Block with a three-spike crown; visor has a 1 px accent (red) glow line |

Opening to release a balloon: the visor slit doubles in height for 0.4 s. Hit flash: whole boss turns cream.

Bullets: aimed = 7 px disc, **cream core** with a 1 px ring in the theme `accent` and ink outline. Bouncy = 9 px disc in `tileCap` with an `accent` ring inside the outline. (Plan had a plain accent bullet, hard to see in Lava.)

## Rolling balloon (step 20)

Pearl `EDE6F7`, shade `B7A9D6`, 1 px ink outline, no face. Two thin crossing bands in `PearlShade` darkened, 4 frames rotated 22.5 degrees each, one frame per 3 px travelled, reversing with direction. Big 18 px, small 12 px. A white 1 px highlight near the top left.
