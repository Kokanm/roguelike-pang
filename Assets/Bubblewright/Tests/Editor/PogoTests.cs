using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Bubblewright.Tests
{
    /// <summary>
    /// Pogos: soft triangles that bounce in place, point up on the floor and down at the top of the bounce, hop once
    /// after a pop, and carry their kind's behaviour without ever moving sideways.
    /// </summary>
    public class PogoTests
    {
        const float Dt = 1f / 60f;

        GameObject root;
        StageMap previousMap;
        Vector2 previousFeet;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("PogoTests");
            previousMap = Game.Map;
            previousFeet = Game.WickFeet;
            SetMap(StageMap.Flat());
            Game.WickFeet = new Vector2(-12f, 0f);
        }

        [TearDown]
        public void TearDown()
        {
            SetMap(previousMap);
            Game.WickFeet = previousFeet;
            Object.DestroyImmediate(root);
        }

        static void SetMap(StageMap map)
        {
            var property = typeof(Game).GetProperty("Map", BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(property, "Game.Map must exist.");
            property.SetValue(null, map);
        }

        Bubble Pogo(BubbleKind kind, int tier, Vector2 at, Vector2 velocity, bool child = false, bool steel = false) =>
            Bubble.Create(root.transform, kind, tier, at, velocity, child, false, true, steel);

        static void Run(Bubble b, float seconds)
        {
            int n = Mathf.RoundToInt(seconds / Dt);
            for (int i = 0; i < n; i++) b.Tick(Dt);
        }

        /// <summary>Ticks until the bubble has landed this many times (vy turned from falling to rising).</summary>
        static void RunToLanding(Bubble b, int landings, float maxSeconds = 30f)
        {
            float prev = b.Velocity.y;
            for (int i = 0; i < Mathf.RoundToInt(maxSeconds / Dt) && landings > 0; i++)
            {
                b.Tick(Dt);
                if (prev <= 0f && b.Velocity.y > 0f) landings--;
                prev = b.Velocity.y;
            }
            Assert.AreEqual(0, landings, "it should have landed");
        }

        // ------------------------------------------------------------------ bounce in place

        [Test]
        public void APogoNeverMovesSidewaysWhateverSpeedItIsGiven()
        {
            var b = Pogo(BubbleKind.Gloop, 1, new Vector2(4f, 6f), new Vector2(3f, 0f));
            RunToLanding(b, 3);
            Run(b, 1.3f);
            Assert.AreEqual(4f, b.Position.x, 1e-4f);
            Assert.AreEqual(0f, b.Velocity.x);
        }

        [Test]
        public void ItBouncesAsHighAsARoundBubbleOfItsKindAndTier()
        {
            var b = Pogo(BubbleKind.Gloop, 2, new Vector2(0f, 3f), Vector2.zero);
            RunToLanding(b, 1);
            float top = 0f;
            for (int i = 0; i < 4 * 60; i++)
            {
                b.Tick(Dt);
                top = Mathf.Max(top, b.Position.y + b.Radius);
            }
            Assert.AreEqual(Tuning.I.tiers[2].peakHeight, top, 0.15f);
        }

        [Test]
        public void ItPointsUpOnTheFloorAndDownAtTheTopOfTheBounce()
        {
            var b = Pogo(BubbleKind.Gloop, 1, new Vector2(0f, 4f), Vector2.zero);
            RunToLanding(b, 1);
            Assert.Less(b.Morph, 0.1f, "just landed: pointing up");
            float most = 0f;
            float atTop = -1f;
            float prevVy = b.Velocity.y;
            for (int i = 0; i < 3 * 60; i++)
            {
                b.Tick(Dt);
                most = Mathf.Max(most, b.Morph);
                if (prevVy > 0f && b.Velocity.y <= 0f) atTop = b.Morph;
                prevVy = b.Velocity.y;
            }
            Assert.Greater(atTop, 0.95f, "at the top of the bounce it points down");
            Assert.Greater(most, 0.95f);
        }

        [Test]
        public void TheMorphChangesSmoothlyWithHeightNoWobble()
        {
            var b = Pogo(BubbleKind.Gloop, 1, new Vector2(0f, 4f), Vector2.zero);
            RunToLanding(b, 1);
            float prev = b.Morph;
            bool rising = true;
            for (int i = 0; i < 2 * 60; i++)
            {
                b.Tick(Dt);
                if (b.Velocity.y <= 0f) rising = false;
                if (b.Position.y - b.Radius < 0.02f) break;
                if (rising) Assert.GreaterOrEqual(b.Morph, prev - 1e-3f, $"tick {i}: going up it only turns toward pointing down");
                else Assert.LessOrEqual(b.Morph, prev + 1e-3f, $"tick {i}: coming down it only turns toward pointing up");
                prev = b.Morph;
            }
        }

        [Test]
        public void AChildHopsSidewaysOnceThenStaysWhereItLanded()
        {
            var t = Tuning.I;
            var b = Pogo(BubbleKind.Gloop, 2, new Vector2(0f, 3f), Vector2.zero, child: true);
            float distance = t.pogoHopWidths * t.tiers[2].diameter;
            b.StartHop(3f, distance);
            b.Velocity.y = 4f;
            Assert.IsTrue(b.Hopping);
            RunToLanding(b, 1);
            Assert.IsFalse(b.Hopping);
            float landedX = b.Position.x;
            Assert.Greater(landedX, 0.5f);
            RunToLanding(b, 2);
            Assert.AreEqual(landedX, b.Position.x, 1e-4f);
        }

        [Test]
        public void AHopStopsAtTheWall()
        {
            var b = Pogo(BubbleKind.Gloop, 2, new Vector2(Tuning.I.HalfWidth - 1f, 3f), Vector2.zero, child: true);
            b.StartHop(6f, 5f);
            b.Velocity.y = 4f;
            RunToLanding(b, 2);
            Assert.LessOrEqual(b.Position.x, Tuning.I.HalfWidth - b.Radius + 1e-3f);
            Assert.AreEqual(0f, b.Velocity.x);
        }

        // ------------------------------------------------------------------ shape and hitbox

        [Test]
        public void TheShapePointsUpAtMorphZeroAndDownAtMorphOne()
        {
            Assert.Greater(PogoShape.HalfWidth01(0.05f, 0f), 0.9f);
            Assert.Less(PogoShape.HalfWidth01(0.95f, 0f), 0.1f);
            Assert.Less(PogoShape.HalfWidth01(0.05f, 1f), 0.1f);
            Assert.Greater(PogoShape.HalfWidth01(0.95f, 1f), 0.9f);
            // Half way it is a rounded blob, never flat or empty.
            Assert.Greater(PogoShape.HalfWidth01(0.5f, 0.5f), 0.4f);
        }

        [Test]
        public void TheEmptyCornersBesideTheTipDoNotCount()
        {
            var centre = new Vector2(0f, 5f);
            float r = 1.25f;
            var upperLeft = new Rect(-r * 0.85f, 5f + r * 0.3f, 0.2f, 0.2f); // inside the circle, beside the tip
            var base_ = new Rect(-0.1f, 5f - r + 0.1f, 0.2f, 0.2f);
            Assert.IsFalse(PogoShape.Overlaps(centre, r, 0f, upperLeft), "pointing up: the upper corner is empty");
            Assert.IsTrue(PogoShape.Overlaps(centre, r, 0f, base_));
            var lowerLeft = new Rect(-r * 0.8f, 5f - r + 0.05f, 0.2f, 0.2f);
            Assert.IsTrue(PogoShape.Overlaps(centre, r, 0f, lowerLeft), "pointing up: the base is wide");
            Assert.IsFalse(PogoShape.Overlaps(centre, r, 1f, lowerLeft), "pointing down: the lower corner is empty");
        }

        [Test]
        public void TheBubbleUsesTheTriangleForHits()
        {
            var b = Pogo(BubbleKind.Gloop, 1, new Vector2(0f, 6f), Vector2.zero);
            var round = Bubble.Create(root.transform, BubbleKind.Gloop, 1, new Vector2(0f, 6f), Vector2.zero, false);
            // Freshly placed in the air it points down: the circle's lower left is empty for the Pogo.
            float r = b.Radius;
            var lowerLeft = new Rect(-r * 0.65f, 6f - r * 0.65f, 0.1f, 0.1f);
            Assert.IsTrue(round.Overlaps(lowerLeft));
            Assert.IsFalse(b.Overlaps(lowerLeft));
        }

        [Test]
        public void SpritesArePaintedForEveryFrameAndLook()
        {
            var t = Tuning.I;
            int last = t.pogoFrames - 1;
            foreach (BubbleKind kind in System.Enum.GetValues(typeof(BubbleKind)))
                for (int tier = 0; tier < t.tiers.Length; tier++)
                {
                    var up = PlaceholderArt.I.PogoFrame(kind, tier, 0);
                    var down = PlaceholderArt.I.PogoFrame(kind, tier, last);
                    Assert.NotNull(up);
                    Assert.NotNull(down);
                    int d = Mathf.RoundToInt(t.tiers[tier].diameter * t.pixelsPerUnit);
                    Assert.AreEqual(d, up.texture.width, $"{kind} {tier}: same size as the round bubble");
                    // Up: the lower rows are wider than the upper rows. Down: the other way round.
                    Assert.Greater(RowWidth(up.texture, 2), RowWidth(up.texture, d * 3 / 4), $"{kind} {tier} up");
                    Assert.Greater(RowWidth(down.texture, d * 3 / 4), RowWidth(down.texture, 2), $"{kind} {tier} down");
                }
            foreach (PogoLook look in System.Enum.GetValues(typeof(PogoLook)))
                Assert.NotNull(PlaceholderArt.I.PogoFrame(BubbleKind.Ghost, 1, 4, look));
            Assert.AreNotSame(PlaceholderArt.I.PogoFrame(BubbleKind.Drifter, 1, 0, PogoLook.Normal, 1),
                PlaceholderArt.I.PogoFrame(BubbleKind.Drifter, 1, 0, PogoLook.Normal, -1));
        }

        static int RowWidth(Texture2D tex, int y)
        {
            int n = 0;
            for (int x = 0; x < tex.width; x++) if (tex.GetPixel(x, y).a > 0f) n++;
            return n;
        }

        // ------------------------------------------------------------------ kinds

        [Test]
        public void ASinkerPogoRestsWhenItsBounceRunsOutThenSpringsBackToFullHeight()
        {
            var t = Tuning.I;
            var b = Pogo(BubbleKind.Sinker, 2, new Vector2(3f, 2f), new Vector2(2f, 0f));
            var sinker = (SinkerBehavior)b.Behavior;
            sinker.Keep = 0.01f;
            for (int i = 0; i < 10 * 60 && !sinker.Resting; i++) b.Tick(Dt);
            Assert.IsTrue(sinker.Resting, "it rests instead of rolling");
            Assert.IsFalse(sinker.Rolling);
            Run(b, t.pogoSinkerRest - 0.2f);
            Assert.IsTrue(sinker.Resting);
            Assert.AreEqual(3f, b.Position.x, 1e-4f);
            Run(b, 0.3f);
            Assert.IsFalse(sinker.Resting);
            Assert.Greater(b.Velocity.y, 0f);
            float top = 0f;
            for (int i = 0; i < 3 * 60; i++)
            {
                b.Tick(Dt);
                top = Mathf.Max(top, b.Position.y + b.Radius);
            }
            Assert.AreEqual(t.tiers[2].peakHeight, top, 0.3f, "back to a full bounce");
            Assert.AreEqual(3f, b.Position.x, 1e-4f);
        }

        [Test]
        public void AClingerPogoHangsPointDownWithItsFlatSideOnTheCeiling()
        {
            var b = Pogo(BubbleKind.Clinger, 2, new Vector2(5f, 9f), new Vector2(1f, 0f));
            b.Tick(Dt);
            var clinger = (ClingerBehavior)b.Behavior;
            Assert.AreEqual(ClingerBehavior.State.Stuck, clinger.Current);
            Assert.AreEqual(1f, b.Morph, 1e-4f);
            float px = Tuning.I.Pixel;
            float shapeTop = b.Position.y - b.Radius + px + 2f * (b.Radius - px) * PogoShape.HeightShare;
            Assert.AreEqual(Tuning.I.arenaHeight, shapeTop, 0.02f, "its flat top touches the ceiling");
        }

        [Test]
        public void AClingerPogoDropsStraightDownAndLeapsStraightBackUp()
        {
            var b = Pogo(BubbleKind.Clinger, 2, new Vector2(5f, 9f), new Vector2(1f, 0f));
            var clinger = (ClingerBehavior)b.Behavior;
            Game.WickFeet = new Vector2(5f, 0f);
            Run(b, 0.5f);
            Assert.AreEqual(ClingerBehavior.State.Free, clinger.Current);
            Game.WickFeet = new Vector2(-12f, 0f);
            for (int i = 0; i < 4 * 60 && clinger.Current != ClingerBehavior.State.Stuck; i++) b.Tick(Dt);
            Assert.AreEqual(ClingerBehavior.State.Stuck, clinger.Current);
            Assert.AreEqual(Vector2.up, clinger.Normal, "stands on the floor");
            Assert.AreEqual(0f, b.Morph, 1e-4f, "pointing up");
            Assert.AreEqual(5f, b.Position.x, 1e-4f);

            // Wick walks up beside it: it wobbles and leaps up to the ceiling again.
            Game.WickFeet = new Vector2(5f + b.Radius + 1f, 0f);
            b.Tick(Dt);
            Assert.AreEqual(ClingerBehavior.State.Wobbling, clinger.Current);
            Game.WickFeet = new Vector2(-12f, 0f);
            Run(b, Tuning.I.clingerWobbleTime + 0.05f);
            Assert.AreEqual(ClingerBehavior.State.Free, clinger.Current);
            for (int i = 0; i < 4 * 60 && clinger.Current != ClingerBehavior.State.Stuck; i++) b.Tick(Dt);
            Assert.AreEqual(ClingerBehavior.State.Stuck, clinger.Current);
            Assert.AreEqual(Vector2.down, clinger.Normal, "back on the ceiling");
            Assert.AreEqual(5f, b.Position.x, 1e-4f);
        }

        [Test]
        public void ADrifterPogoFliesStraightUpAndDownAtItsSpeed()
        {
            var b = Pogo(BubbleKind.Drifter, 1, new Vector2(-6f, 4f), new Vector2(1f, 0f));
            Assert.AreEqual(0f, b.Velocity.x);
            Assert.AreEqual(Tuning.I.drifterSpeed, Mathf.Abs(b.Velocity.y), 1e-4f);
            bool sawUp = false, sawDown = false;
            float lowMorph = 1f, highMorph = 0f;
            for (int i = 0; i < 8 * 60; i++)
            {
                b.Tick(Dt);
                if (b.Velocity.y > 0f) sawUp = true; else sawDown = true;
                if (b.Position.y - b.Radius < 0.3f) lowMorph = Mathf.Min(lowMorph, b.Morph);
                if (b.Position.y + b.Radius > Tuning.I.arenaHeight - 0.3f) highMorph = Mathf.Max(highMorph, b.Morph);
                Assert.AreEqual(-6f, b.Position.x, 1e-4f);
            }
            Assert.IsTrue(sawUp && sawDown);
            Assert.Less(lowMorph, 0.1f);
            Assert.Greater(highMorph, 0.9f);
        }

        [Test]
        public void AGhostPogoRisesThroughPlatformsAndFades()
        {
            var map = StageMap.Flat();
            for (int c = 14; c <= 18; c++) map.Set(c, 4, new Cell { kind = TileKind.Solid, hp = 1 });
            SetMap(map);
            var b = Pogo(BubbleKind.Ghost, 2, new Vector2(StageMap.CenterX(16), 2f), Vector2.zero);
            float top = 0f;
            for (int i = 0; i < 3 * 60; i++)
            {
                b.Tick(Dt);
                top = Mathf.Max(top, b.Position.y);
            }
            Assert.Greater(top, 6f, "it rose through the platform");
            bool faded = false;
            for (int i = 0; i < 5 * 60 && !faded; i++)
            {
                b.Tick(Dt);
                faded = !b.CanBeHit;
            }
            Assert.IsTrue(faded, "it fades within a cycle");
            Assert.AreNotEqual(PogoLook.Normal, b.GhostLook, "drawn faded");
        }

        [Test]
        public void ASteelPogoCannotBeHitButHurts()
        {
            var b = Pogo(BubbleKind.Gloop, 2, new Vector2(6f, 1f), Vector2.zero, steel: true);
            Assert.IsTrue(b.Steel);
            Assert.IsFalse(b.CanBeHit);
            Assert.IsTrue(b.CanHurt);
            Assert.IsNull(b.Behavior);
            RunToLanding(b, 2);
            Assert.AreEqual(6f, b.Position.x, 1e-4f);
        }

        [Test]
        public void SteelIsOnlyForPogos()
        {
            var b = Bubble.Create(root.transform, BubbleKind.Gloop, 2, new Vector2(0f, 4f), Vector2.zero, false, false, false, true);
            Assert.IsFalse(b.Steel);
            Assert.IsTrue(b.CanBeHit);
        }

        // ------------------------------------------------------------------ where they appear

        [Test]
        public void PogosFollowTheirStageRules()
        {
            var t = Tuning.I;
            int plainPogos = 0, specialPogos = 0, steel = 0;
            for (int seed = 1; seed <= 40; seed++)
            {
                var run = new Bubblewright.Run(seed);
                for (int stage = 1; stage <= t.RunLength; stage++)
                {
                    if (Bubblewright.Run.IsBossStageNumber(stage)) continue;
                    var plan = StageGenerator.GenerateFor(run, stage);
                    string where = $"seed {seed} stage {stage}";
                    bool quiet = plan.recipeName == "Introduction" || plan.recipeName == StageGenerator.DebutRecipeName;
                    var all = new List<SpawnSpec>(plan.spawns);
                    foreach (var wave in plan.waves) all.AddRange(wave);
                    var xs = new List<float>();
                    foreach (var s in plan.spawns)
                    {
                        if (!s.pogo) continue;
                        xs.Add(s.position.x);
                    }
                    foreach (var s in all)
                    {
                        if (!s.pogo) continue;
                        Assert.IsFalse(quiet, $"{where}: no Pogos on {plan.recipeName}");
                        Assert.AreNotEqual(BubbleKind.Lucky, s.kind, $"{where}: Lucky is never a Pogo");
                        Assert.IsFalse(s.steel, $"{where}: steel Pogos are kept apart");
                        if (StageGenerator.IsPlainPogoKind(s.kind))
                        {
                            Assert.GreaterOrEqual(stage, t.pogoFromStage, $"{where}: plain Pogo too early");
                            plainPogos++;
                        }
                        else specialPogos++;
                        int col = StageMap.Col(s.position.x);
                        Assert.IsFalse(plan.map.IsGap(col), $"{where}: Pogo over a gap");
                        for (int r = 0; r < StageMap.Rows; r++) Assert.IsFalse(plan.map.Get(col, r).ladder, $"{where}: Pogo over a ladder");
                    }
                    for (int i = 0; i < xs.Count; i++)
                        for (int j = i + 1; j < xs.Count; j++)
                            Assert.GreaterOrEqual(Mathf.Abs(xs[i] - xs[j]), t.pogoMinSpacing - 1e-4f, $"{where}: Pogos too close");

                    Assert.LessOrEqual(plan.steelPogos.Count, 1, $"{where}: one steel Pogo at most");
                    foreach (var s in plan.steelPogos)
                    {
                        steel++;
                        Assert.IsFalse(quiet, where);
                        Assert.GreaterOrEqual(Bubblewright.Run.WorldOf(stage), t.pogoSteelFromWorld, $"{where}: steel too early");
                        Assert.IsTrue(s.pogo && s.steel);
                        Assert.GreaterOrEqual(Mathf.Abs(s.position.x), 5f, $"{where}: steel Pogo near Wick's start");
                        Assert.IsFalse(plan.map.IsGap(StageMap.Col(s.position.x)), where);
                        foreach (float x in xs)
                            Assert.GreaterOrEqual(Mathf.Abs(x - s.position.x), t.pogoMinSpacing - 1e-4f, $"{where}: steel Pogo too close to a Pogo");
                    }
                }
            }
            Assert.Greater(plainPogos, 0, "plain Pogos should appear");
            Assert.Greater(specialPogos, 0, "special Pogos should appear");
            Assert.Greater(steel, 0, "steel Pogos should appear");
        }

        [Test]
        public void TheSameSeedBuildsTheSamePogos()
        {
            var a = new Bubblewright.Run(7);
            var b = new Bubblewright.Run(7);
            for (int stage = 1; stage <= Tuning.I.RunLength; stage++)
            {
                var pa = StageGenerator.GenerateFor(a, stage);
                var pb = StageGenerator.GenerateFor(b, stage);
                Assert.AreEqual(pa.spawns.Count, pb.spawns.Count);
                for (int i = 0; i < pa.spawns.Count; i++) Assert.AreEqual(pa.spawns[i].pogo, pb.spawns[i].pogo, $"stage {stage} spawn {i}");
                Assert.AreEqual(pa.steelPogos.Count, pb.steelPogos.Count, $"stage {stage}");
            }
        }
    }
}
