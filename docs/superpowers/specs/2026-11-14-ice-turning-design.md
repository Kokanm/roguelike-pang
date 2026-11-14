# Ice turning: responsive active braking on icy surfaces

Short design agreed with Kokan on 2026-11-14. Items marked **(call)** were decided by the builder and can be changed freely.

## Why

Walking on ice feels clunky and unresponsive because reversing direction uses the same `0.9s` (`54` frames) slide rate as letting go of the controls, followed by `0.5s` (`30` frames) to reach top speed (`1.4s` total). Pressing the opposite direction should immediately start shedding speed over a handful of frames and smoothly build speed the other way, while letting go of input keeps its slippery glide.

## Goals, non-goals, anti-goals

- **Goals:** Pressing the opposite direction on ice visibly cuts speed over ~18 frames (`0.30s`) and ramps up in the new direction over ~21 frames (`0.35s`), while releasing input still glides to a stop over `0.9s`.
- **Non-goals:** Changing normal ground, sand, air control, Frost bubble timers, or Winter ice placement.
- **Anti-goals:** Instant normal-ground snapping on ice (ice should still feel slippery), or a multi-second slide that ignores opposite-direction input.

## Constraints

- Same movement architecture in `Wick.cs`: frame-rate-independent integration driven by `Game.Update`, every timing exposed in plain seconds on `Tuning`, and behaviour verified with EditMode NUnit tests in `SurfaceTests.cs`.

## Approaches considered

- **A. Separate linear `iceTurnTime` alongside `iceAccelTime` and `iceStopTime` (chosen):** Split `Wick.Tick` into coasting (`wantMove == 0`), reversing (`wantMove` opposes `VelocityX`), and accelerating, using `Mathf.MoveTowards`. Keeps plain-seconds Inspector tuning and crosses `0` briskly.
- **B. Exponential drag / `Lerp` curve on ice:** Sheds high speed fast, but lingers near `0` right at the direction flip, needs snap thresholds, and departs from `Tuning`'s plain-seconds convention.
- **Dropped — quantized speed tiers:** Literal stepped speeds would feel jerky in an otherwise continuous controller.

Kokan picked **A**.

## Decisions

- **Coasting on ice** (`wantMove == 0`): keeps `Tuning.iceStopTime = 0.9f`.
- **Active braking / reversing on ice** (`wantMove` opposes `VelocityX`): uses a new `Tuning.iceTurnTime = 0.30f` (~18 frames from full speed to `0`).
- **Accelerating on ice** (from `0` or with `VelocityX`): lowers `Tuning.iceAccelTime` from `0.5f` to `0.35f` (~21 frames from `0` to top speed; full reverse in ~`0.65s` instead of `1.4s`).
- **Normal and sand surfaces:** continue using `Tuning.stopTime` (`0.04f`) for both coasting and reversing, leaving non-ice feel identical.

## Builder calls

- Keep `canSkid` false on ice in `Wick.DetectRunEdges` so ice turns stay smooth rather than kicking up dirt dust and dry-ground skid SFX, while body lean naturally follows `VelocityX` across `0` in ~18 frames. **(call)**
- Update `SurfaceTests.IceIsSlowToStart` for `iceAccelTime = 0.35f` (checked mid-ramp at `0.17s` and at top speed by `0.37s`), keep `IceIsSlowToStop` intact, and add dedicated tests proving active reversing on ice is much faster than passive coasting yet slower than normal ground. **(call)**
