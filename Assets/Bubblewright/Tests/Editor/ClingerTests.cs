using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Bubblewright.Tests
{
    /// <summary>The Clinger hangs from a ceiling or platform, wobbles when Wick is below, then drops and bounces like a Gloop.</summary>
    public class ClingerTests
    {
        const float Dt = 1f / 60f;

        GameObject root;
        StageMap previousMap;
        Vector2 previousFeet;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("ClingerTests");
            previousMap = Game.Map;
            previousFeet = Game.WickFeet;
            SetMap(StageMap.Flat());
            Game.WickFeet = new Vector2(-10f, 0f);
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

        static StageMap PlatformMap(TileKind kind = TileKind.Solid)
        {
            var map = StageMap.Flat();
            for (int c = 19; c <= 23; c++) map.Set(c, 9, new Cell { kind = kind, hp = 1 });
            return map;
        }

        Bubble Spawn(int tier, Vector2 at, bool child = false)
        {
            return Bubble.Create(root.transform, BubbleKind.Clinger, tier, at, new Vector2(1f, 0f), child);
        }

        static ClingerBehavior Of(Bubble b) => (ClingerBehavior)b.Behavior;

        static void Run(Bubble b, float seconds)
        {
            int n = Mathf.RoundToInt(seconds / Dt);
            for (int i = 0; i < n; i++) b.Tick(Dt);
        }

        [Test]
        public void AttachesToTheArenaCeilingAndStaysStillWhenWickIsFarAway()
        {
            var b = Spawn(2, new Vector2(5f, 9f));
            Assert.AreEqual(14f - b.Radius, b.Position.y, 0.001f);
            Assert.AreEqual(ClingerBehavior.State.Hanging, Of(b).Current);
            var start = b.Position;
            Run(b, 5f);
            Assert.AreEqual(start.x, b.Position.x, 0.0001f);
            Assert.AreEqual(start.y, b.Position.y, 0.0001f);
        }

        [Test]
        public void AttachesUnderAPlatformUnlessItIsXl()
        {
            SetMap(PlatformMap());
            var m = Spawn(2, new Vector2(5.5f, 7f));
            Assert.AreEqual(9f - m.Radius, m.Position.y, 0.001f);
            var xl = Spawn(0, new Vector2(5.5f, 7f));
            Assert.AreEqual(14f - xl.Radius, xl.Position.y, 0.001f);
        }

        [Test]
        public void WobblesFor0Point4SecondsThenDropsSidewaysAtFullSpeed()
        {
            SetMap(PlatformMap());
            var b = Spawn(2, new Vector2(5.5f, 7f));
            float hangY = b.Position.y;
            Game.WickFeet = new Vector2(5.5f + 1.4f, 0f);
            b.Tick(Dt);
            Assert.AreEqual(ClingerBehavior.State.Wobbling, Of(b).Current);
            Run(b, 0.38f);
            Assert.AreEqual(ClingerBehavior.State.Wobbling, Of(b).Current);
            Assert.AreEqual(hangY, b.Position.y, 0.0001f, "it must not move while it wobbles");
            Run(b, 0.1f);
            Assert.AreEqual(ClingerBehavior.State.Free, Of(b).Current);
            Assert.AreEqual(b.SpeedX, b.Velocity.x, 0.001f);
            Run(b, 0.2f);
            Assert.Less(b.Position.y, hangY, "it falls once dropped");
        }

        [Test]
        public void DoesNotTriggerWhenWickIsTooFarSidewaysOrAbove()
        {
            SetMap(PlatformMap());
            var b = Spawn(2, new Vector2(5.5f, 7f));
            Game.WickFeet = new Vector2(5.5f + 1.6f, 0f);
            Run(b, 1f);
            Assert.AreEqual(ClingerBehavior.State.Hanging, Of(b).Current);
            Game.WickFeet = new Vector2(5.5f, b.Position.y + 1f); // standing above its bottom
            Run(b, 1f);
            Assert.AreEqual(ClingerBehavior.State.Hanging, Of(b).Current);
        }

        [Test]
        public void BouncesLikeAGloopAfterDropping()
        {
            var b = Spawn(2, new Vector2(5f, 9f));
            Game.WickFeet = new Vector2(5f, 0f);
            float peak = Tuning.I.tiers[2].peakHeight;
            float highest = 0f;
            bool bounced = false;
            for (int i = 0; i < 20 * 60; i++)
            {
                b.Tick(Dt);
                if (b.Velocity.y > 0f && Of(b).Current == ClingerBehavior.State.Free) bounced = true;
                if (bounced) highest = Mathf.Max(highest, b.Position.y + b.Radius);
            }
            Assert.IsTrue(bounced, "it must have landed and bounced");
            Assert.LessOrEqual(highest, peak + 0.05f);
            Assert.GreaterOrEqual(highest, peak - 0.2f);
        }

        [Test]
        public void TheStormDropsAHangingClingerAtOnce()
        {
            var b = Spawn(1, new Vector2(5f, 9f));
            Assert.AreEqual(ClingerBehavior.State.Hanging, Of(b).Current);
            b.OnStorm();
            Assert.AreEqual(ClingerBehavior.State.Free, Of(b).Current);
        }

        [Test]
        public void AChildIsFreeFromTheStartAndIsNotMovedToACeiling()
        {
            var b = Spawn(2, new Vector2(5f, 4f), child: true);
            Assert.AreEqual(ClingerBehavior.State.Free, Of(b).Current);
            Assert.AreEqual(4f, b.Position.y, 0.0001f);
        }

        [Test]
        public void AHangingClingerCanBeShotAndHurts()
        {
            var b = Spawn(2, new Vector2(5f, 9f));
            Assert.IsTrue(b.CanBeHit);
            Assert.IsTrue(b.CanHurt);
        }

        [Test]
        public void DropsWhenItsBreakableTileIsShotAway()
        {
            var map = PlatformMap(TileKind.Breakable);
            SetMap(map);
            var b = Spawn(2, new Vector2(5.5f, 7f));
            b.Tick(Dt);
            Assert.AreEqual(ClingerBehavior.State.Hanging, Of(b).Current);
            map.Set(21, 9, new Cell { kind = TileKind.Empty });
            b.Tick(Dt);
            Assert.AreEqual(ClingerBehavior.State.Free, Of(b).Current);
        }
    }
}
