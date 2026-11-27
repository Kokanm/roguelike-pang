# Bubblewright (Roguelike Pang)

A Unity 6 (6000.6.7f1, 2D URP) prototype of a roguelike inspired by Pang: Wick fires a harpoon straight up to
split bouncing bubbles. Mobile first (touch, landscape, 480 x 270 at 15 px per unit), then desktop, then console.
The README explains controls and how to open the project.

## Stages: read the skill first

**Before you create, edit, review or rebalance any stage, read `.claude/skills/bubblewright-level-design/SKILL.md`
and follow it.** It holds the design brief, the rules `StageValidator` enforces, the numbers, the worked
examples (the play-tested World 1 stages) and the check loop. If you find a gap in the skill, fix the skill.

## Layout

| Path | What |
|---|---|
| `Assets/Bubblewright/Scripts/Core/` | `Game` (partial classes, one tick order), `Tuning`, `Run`, saves, `StageReport` |
| `Assets/Bubblewright/Scripts/Stage/` | `StageMap` (tiles and collision), `StageLayout` (ASCII parser), `StageSpec` + `StageBuilder`, `StagePicker`, `StageValidator` |
| `Assets/Bubblewright/Scripts/Stage/Stages/W1..W6/` | one authored stage per file, found by `StageSpec.All` |
| `Assets/Bubblewright/Scripts/Stage/Stages/Bosses/` | one boss arena per file (`IsBossArena`, up to 5 storeys), found by `StageSpec.BossArenas`; `ArenaEvents` is what a boss does to its arena |
| `Assets/Bubblewright/Scripts/Player`, `Bubbles`, `Weapons`, `Bosses`, `FX`, `UI`, `Art` | Wick, bubble kinds and behaviours, crate weapons, bosses, effects, HUD, code-painted placeholder art |
| `Assets/Bubblewright/Editor/` | menu items (Project Setup, balance summary, Write Level-Design Reference) |
| `Assets/Bubblewright/Tests/Editor/` | EditMode NUnit tests |
| `docs/superpowers/specs`, `docs/superpowers/plans` | agreed designs and step-by-step plans |

## Invariants

* **No physics engine.** Movement and collision are our own kinematic maths on the `StageMap` grid, ticked from
  `Game.Update` in a fixed order so hit-stop, slow motion and pause freeze everything.
* **Every tunable number lives in `Tuning`** (the `Resources/Tuning.asset`; fields missing from the asset use the
  code default). Hero stats are in `Character.All`, hard-world debuffs in `Debuff.All`. Do not hard-code a
  number that belongs there.
* **No image or audio files**: art is painted in code (`PlaceholderArt`), sounds are synthesised (`Sfx`).
* **Content discovers itself**: stages (`StageSpec`) and weapons (`WeaponSpec`) are sealed `[Preserve]`
  classes found by reflection, one per file, so parallel branches never conflict.
* Every new asset or script needs its `.meta` (Unity writes it on import; commit it).
* Keep package-generated `ProjectSettings` changes (app-ui, SENTIS define) out of commits.

## Checks

```bash
python3 .claude/skills/unity-csharp-scripting/scripts/check_unity_project.py .
unity test . --mode EditMode --output Temp/editmode.xml
unity test . --mode EditMode --filter "Bubblewright.Tests.StageValidatorTests" --output Temp/validator.xml
python3 .claude/skills/bubblewright-level-design/scripts/check_stage.py Assets/Bubblewright/Scripts/Stage/Stages
```

Unity runs on Kokan's MacBook (it can run headless there). Cloud sessions have no Unity: run the Python checks,
and say plainly which Unity tests still need a run on the laptop.

After changing a `Tuning` number that the level-design skill quotes, run **Bubblewright > Write Level-Design
Reference** and commit the reference file; `LevelDesignReferenceTests` fails until you do.
