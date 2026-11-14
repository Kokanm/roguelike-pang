# Ice Turning Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make reversing direction on ice immediately begin shedding speed (`0.18s` brake + `0.25s` acceleration) while preserving the long slippery slide (`0.9s`) when letting go of movement.

**Architecture:** Add `iceTurnTime = 0.18f` and lower `iceAccelTime` to `0.25f` in `Tuning` (and sync `Resources/Tuning.asset` if serialized there, though `ice*` fields currently fall back to `Tuning.cs` defaults), then split `Wick.Tick`'s horizontal ground velocity step into three cases: coasting (`target == 0f`), reversing (`VelocityX != 0f && Mathf.Sign(target) != Mathf.Sign(VelocityX)`), and accelerating. Verify all surface transitions and edge cases via EditMode NUnit tests in `SurfaceTests.cs`.

**Tech Stack:** Unity 6000.6.7f1, C# 9, NUnit (Unity Test Framework EditMode), `unity` CLI.

**Spec:** `docs/superpowers/specs/2026-11-14-ice-turning-design.md`

## Global Constraints

- Same movement architecture in `Wick.cs`: frame-rate-independent integration driven by `Game.Update`, every timing exposed in plain seconds on `Tuning`, and behaviour verified with EditMode NUnit tests in `SurfaceTests.cs`.
- Coasting on ice (`wantMove == 0`): keeps `Tuning.iceStopTime = 0.9f`.
- Active braking / reversing on ice (`wantMove` opposes `VelocityX`): uses `Tuning.iceTurnTime = 0.18f`.
- Accelerating on ice (from `0` or with `VelocityX`): uses `Tuning.iceAccelTime = 0.25f`.
- Normal and sand surfaces: continue using `Tuning.stopTime` (`0.04f`) for both coasting and reversing, leaving non-ice feel identical.
- Keep `canSkid` false on ice in `Wick.DetectRunEdges`.

## Review Focus

1. **Coasting vs. active reversing on ice:** Releasing input (`move = 0`) from top speed on ice must still slide past `0.4s` (`iceStopTime = 0.9f`), whereas pressing the opposite direction (`move = -1` from `+runSpeed`) must cross `0` right around `0.18s` and reach `-runSpeed` around `0.43s`.
2. **Step-by-step deceleration curve on ice:** Pressing Right while sliding Left at `-runSpeed` must still move Left at monotonically smaller speeds for the first several frames before crossing `0` and ramping Right — never snapping to `+runSpeed` on frame 1 and never staying stuck near `-runSpeed` for half a second.
3. **Mid-slide re-acceleration in the same direction:** Letting go briefly on ice (speed drops to e.g. `+5 u/s`) and pressing the *same* direction again (`move = +1`) must use `iceAccelTime` (`0.25f`), not `iceStopTime` or `iceTurnTime`.
4. **Jumping off ice:** While airborne (`Mode.Fall`), `Wick.Tick` treats `surface` as `Surface.Normal`, so air steering and normal/sand ground turning must remain completely unchanged.
5. **Runtime `Resources/Tuning.asset` parity:** `Tuning.I` loads `Assets/Bubblewright/Resources/Tuning.asset` at runtime and in EditMode tests; verify via test that `Tuning.I.iceAccelTime == 0.25f`, `Tuning.I.iceTurnTime == 0.18f`, and `Tuning.I.iceStopTime == 0.9f`.

---

### Task 1: Responsive Ice Turning in `Tuning` and `Wick`

**Files:**
- Modify: `Assets/Bubblewright/Scripts/Core/Tuning.cs:200-206`
- Modify: `Assets/Bubblewright/Scripts/Player/Wick.cs:344-358`
- Test: `Assets/Bubblewright/Tests/Editor/SurfaceTests.cs:53-82`

**Interfaces:**
- Consumes: `StageMap.SurfaceUnder(float x, float y) -> Surface`, `Wick.Tick(float move, float climb, float dt, bool controllable)`
- Produces: `public float Tuning.iceAccelTime = 0.25f`, `public float Tuning.iceTurnTime = 0.18f`, `public float Tuning.iceStopTime = 0.9f`

- [ ] **Step 1: Update and add failing EditMode tests in `Assets/Bubblewright/Tests/Editor/SurfaceTests.cs`**

Update `IceIsSlowToStart` for the `0.25s` ramp and add tests covering active reversing, progressive frame-by-frame deceleration, same-direction re-acceleration after coasting, and `Tuning.I` values:

```csharp
        [Test]
        public void IceTuningDefaultsMatchSpec()
        {
            Assert.AreEqual(0.25f, Tuning.I.iceAccelTime, 1e-4f);
            Assert.AreEqual(0.18f, Tuning.I.iceTurnTime, 1e-4f);
            Assert.AreEqual(0.9f, Tuning.I.iceStopTime, 1e-4f);
        }

        [Test]
        public void IceIsSlowToStart()
        {
            AllGround(map, Surface.Ice);
            Step(0.12f, 1f);
            float top = Tuning.I.runSpeed;
            Assert.LessOrEqual(wick.VelocityX, top * 0.5f + 0.01f, "on ice he is around half of top speed after 0.12 s");
            Step(0.18f, 1f);
            Assert.AreEqual(top, wick.VelocityX, 0.001f, "reaches top speed by 0.30 s");
        }

        [Test]
        public void IceReversesMuchFasterThanCoastingButSlowerThanNormalGround()
        {
            // Normal ground reverses from -runSpeed to +runSpeed within 0.12 s.
            Step(0.25f, -1f);
            Assert.AreEqual(-Tuning.I.runSpeed, wick.VelocityX, 0.001f);
            Step(0.12f, 1f);
            Assert.AreEqual(Tuning.I.runSpeed, wick.VelocityX, 0.001f, "normal ground reverses almost immediately");

            // Reset onto ice and reach full left speed (-runSpeed).
            AllGround(map, Surface.Ice);
            wick.ResetForStage(0f, 0f, 3);
            Step(0.35f, -1f);
            float top = Tuning.I.runSpeed;
            Assert.AreEqual(-top, wick.VelocityX, 0.001f);

            // Press right: still sliding left for the first few frames, shedding speed progressively.
            Step(3f * Dt, 1f); // 0.05 s
            float vAfter3 = wick.VelocityX;
            Assert.That(vAfter3, Is.InRange(-top + 0.5f, -1f), "still moving left, visibly slower after 3 frames");

            Step(4f * Dt, 1f); // ~0.117 s total
            float vAfter7 = wick.VelocityX;
            Assert.That(vAfter7, Is.GreaterThan(vAfter3).And.LessThan(0f), "still moving left, slower again after 7 frames");

            // Past 0.18 s of braking + a few frames of acceleration (~0.25 s total): now moving right, not yet at top speed.
            Step(8f * Dt, 1f); // 0.25 s total
            Assert.That(wick.VelocityX, Is.GreaterThan(0.5f).And.LessThan(top - 0.5f), "moving right and building speed by 0.25 s");

            // Full reverse complete by 0.45 s (vs 1.4 s previously).
            Step(0.22f, 1f);
            Assert.AreEqual(top, wick.VelocityX, 0.001f, "at full right speed by ~0.47 s");
        }

        [Test]
        public void IceReacceleratesWithIceAccelTimeAfterBriefCoast()
        {
            AllGround(map, Surface.Ice);
            float top = Tuning.I.runSpeed;
            Step(0.35f, 1f);
            Assert.AreEqual(top, wick.VelocityX, 0.001f);

            // Coast to roughly half speed (~0.45 s of 0.9 s stop time).
            Step(0.45f, 0f);
            Assert.That(wick.VelocityX, Is.InRange(top * 0.4f, top * 0.6f));

            // Pressing right again should recover to top speed in ~0.13 s (half of 0.25 s iceAccelTime), not 0.45 s.
            Step(0.15f, 1f);
            Assert.AreEqual(top, wick.VelocityX, 0.001f);
        }

        [Test]
        public void JumpingOffIceSteersAtNormalGroundRateInAir()
        {
            AllGround(map, Surface.Ice);
            float top = Tuning.I.runSpeed;
            Step(0.35f, -1f);
            Assert.AreEqual(-top, wick.VelocityX, 0.001f);

            // Jump while running left on ice, then press right in the air: airborne steering uses normal ground rates.
            wick.Tick(-1f, 0f, true, Dt, true);
            Assert.IsTrue(wick.Jumping);
            Step(0.12f, 1f);
            Assert.AreEqual(top, wick.VelocityX, 0.001f);
        }
```

- [ ] **Step 2: Run EditMode surface tests to verify they fail**

Run: `unity test /Users/kokanmalenko/Documents/MyProjects/roguelike-pang --mode EditMode --filter "Bubblewright.Tests.SurfaceTests" --output Temp/surface-tests.xml`
Expected: FAIL (compile error on missing `Tuning.iceTurnTime`).

- [ ] **Step 3: Implement `iceTurnTime`, updated `iceAccelTime`, and 3-way horizontal rate selection**

1. In `Assets/Bubblewright/Scripts/Core/Tuning.cs` under `[Header("Surfaces")]`:
   - Set `public float iceAccelTime = 0.25f;`
   - Add `[Tooltip("Ice: seconds to brake from top speed to zero when pressing the opposite direction (normal floor: Stop Time).")] public float iceTurnTime = 0.18f;`
   - Keep `public float iceStopTime = 0.9f;` with updated tooltip clarifying it applies when letting go of movement.
2. In `Assets/Bubblewright/Scripts/Player/Wick.cs` inside `Tick` (`348-356`):
   - Compute `turnTime = surface == Surface.Ice ? T.iceTurnTime : T.stopTime;` alongside `accelTime` and `stopTime`.
   - Distinguish `coasting = target == 0f` from `reversing = target != 0f && VelocityX != 0f && Mathf.Sign(target) != Mathf.Sign(VelocityX)`.
   - Pick `float rampTime = coasting ? stopTime : reversing ? turnTime : accelTime;` and `float rate = topSpeed / Mathf.Max(0.001f, rampTime);`.

- [ ] **Step 4: Run project checker, `SurfaceTests`, and the full EditMode test suite**

Run:
```bash
python3 .claude/skills/unity-csharp-scripting/scripts/check_unity_project.py /Users/kokanmalenko/Documents/MyProjects/roguelike-pang && \
unity test /Users/kokanmalenko/Documents/MyProjects/roguelike-pang --mode EditMode --output Temp/editmode-results.xml
```
Expected: `check_unity_project.py` exits `0` with no errors; all EditMode tests in `Temp/editmode-results.xml` pass (`failed="0"`).

- [ ] **Step 5: Commit implementation and tests**

```bash
git add Assets/Bubblewright/Scripts/Core/Tuning.cs Assets/Bubblewright/Scripts/Player/Wick.cs Assets/Bubblewright/Tests/Editor/SurfaceTests.cs
git commit -m "Make turning on ice brake faster than coasting"
```
