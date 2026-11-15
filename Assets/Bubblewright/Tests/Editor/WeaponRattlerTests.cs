using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Bubblewright.Tests
{
    /// <summary>W02 Rattler: hold for a stream after a spin-up, a narrow random fan, Wick slowed while firing, shared hit rules.</summary>
    public class WeaponRattlerTests
    {
        const float Dt = 1f / 60f;

        GameObject root;
        StageMap previousMap;
        List<Bubble> bubbles;
        List<Roller> rollers;
        WeaponWorld world;
        Run run;
        WeaponSpec rattler;
        WeaponBehaviour behaviour;
        WeaponContext ctx;
        Effects fx;
        Wick wick;
        readonly List<Vector2Int> tileHits = new List<Vector2Int>();
        int steelHits, flashes;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("WeaponRattlerTests");
            previousMap = Game.Map;
            SetMap(StageMap.Flat());
            bubbles = new List<Bubble>();
            rollers = new List<Roller>();
            tileHits.Clear();
            steelHits = flashes = 0;
            world = new WeaponWorld(bubbles, rollers, null, null, root.transform)
            {
                OnHitBubble = b => { if (!b.AbsorbHit()) b.Swell(Tuning.I.popSwell); },
                OnHitRoller = r => { if (!r.AbsorbHit()) r.Swell(Tuning.I.popSwell); },
                OnHitTile = (c, r) => { tileHits.Add(new Vector2Int(c, r)); Game.Map.Damage(c, r); },
                OnSteel = _ => steelHits++,
            };

            fx = Effects.Create(root.transform);
            wick = Wick.Create(root.transform, fx);
            wick.ResetForStage(0f, 0f, 3);

            rattler = WeaponSpec.ById("rattler");
            Assert.NotNull(rattler, "the Rattler is in WeaponSpec.All");
            run = new Run(1);
            Assert.IsTrue(run.GiveWeapon(rattler));
            behaviour = rattler.CreateBehaviour();
            ctx = new WeaponContext
            {
                CanFire = true,
                Muzzle = new Vector2(0.5f, 2f), // mid-column, so the whole fan stays in one tile column
                Up = Vector2.up,
                Spec = rattler,
                World = world,
                Run = run,
                Wick = wick,
                Rng = new System.Random(1),
            };
            SetInternal("spend", (System.Func<int, bool>)(n => run.SpendAmmo(WeaponSlot.Weapon, n)));
            SetInternal("flash", (System.Action)(() => flashes++));
        }

        [TearDown]
        public void TearDown()
        {
            behaviour?.Dispose();
            world.Destroy();
            SetMap(previousMap);
            Object.DestroyImmediate(root);
        }

        static void SetMap(StageMap map)
        {
            var property = typeof(Game).GetProperty("Map", BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(property, "Game.Map must exist.");
            property.SetValue(null, map);
        }

        void SetInternal(string field, object value)
        {
            var f = typeof(WeaponContext).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(f, $"WeaponContext.{field} must exist.");
            f.SetValue(ctx, value);
        }

        Bubble AddBubble(Vector2 at, int tier = 2, bool ceramic = false, bool steel = false)
        {
            var b = Bubble.Create(root.transform, BubbleKind.Gloop, tier, at, Vector2.zero, false, ceramic, steel, steel);
            bubbles.Add(b);
            return b;
        }

        /// <summary>One frame: the weapon reads its button, then the live bullets fly.</summary>
        void Frame(bool held)
        {
            ctx.Pressed = false;
            ctx.Held = held;
            behaviour.Tick(ctx, Dt);
            world.TickShots(Dt);
        }

        void Hold(float seconds)
        {
            int frames = Mathf.RoundToInt(seconds / Dt);
            for (int i = 0; i < frames; i++) Frame(true);
        }

        void Idle(int frames)
        {
            for (int i = 0; i < frames; i++) Frame(false);
        }

        /// <summary>Hold until the first bullet leaves (the spin-up), then stop holding.</summary>
        void FireOne()
        {
            int before = Fired;
            for (int i = 0; i < 120 && Fired == before; i++) Frame(true);
            Frame(false);
        }

        int Fired => rattler.MaxAmmo - run.WeaponAmmo;

        // ------------------------------------------------------------------ table and ammo

        [Test]
        public void TheRattlerIsAnUncommonWeaponSlotGunThatDrops()
        {
            Assert.AreEqual("Rattler", rattler.Name);
            Assert.AreEqual(WeaponSlot.Weapon, rattler.Slot);
            Assert.AreEqual(WeaponTier.Uncommon, rattler.Tier);
            Assert.IsTrue(rattler.Drops);
            Assert.AreEqual(Tuning.I.rattlerAmmo, rattler.MaxAmmo, "a full crate comes from Tuning");
            Assert.AreEqual(Tuning.I.rattlerAmmo, run.WeaponAmmo);
            Assert.LessOrEqual(WeaponArt.WidthOf(rattler.IconRows), 11);
            Assert.NotNull(WeaponArt.Held(rattler));
        }

        [Test]
        public void NothingLeavesDuringTheSpinUp()
        {
            int spinFrames = Mathf.FloorToInt(Tuning.I.rattlerSpinUp / Dt) - 1;
            for (int i = 0; i < spinFrames; i++) Frame(true);
            Assert.AreEqual(0, Fired);
            Hold(Tuning.I.rattlerSpinUp);
            Assert.Greater(Fired, 0, "bullets leave once it has spun up");
            Assert.AreEqual(Fired, flashes, "one flash per bullet");
        }

        [Test]
        public void HoldingFiresAtTheRattlerRate()
        {
            Hold(Tuning.I.rattlerSpinUp + 1f);
            int expected = Mathf.FloorToInt(Tuning.I.rattlerRate) + 1; // the first bullet leaves at the end of the spin-up
            Assert.AreEqual(expected, Fired, 1, $"about {Tuning.I.rattlerRate} a second while held");
        }

        [Test]
        public void AFullCrateLastsAboutFiveSecondsOfHolding()
        {
            float held = 0f;
            while (run.Weapon != null && held < 30f)
            {
                Frame(true);
                held += Dt;
            }
            Assert.IsNull(run.Weapon, "the slot empties at zero");
            Assert.AreEqual(0, run.WeaponAmmo);
            float expected = Tuning.I.rattlerSpinUp + (Tuning.I.rattlerAmmo - 1) / Tuning.I.rattlerRate;
            Assert.AreEqual(expected, held, 0.1f, "spin-up plus one bullet every 1 / rate seconds");
            Assert.AreEqual(5f, held, 1f, "about five seconds with the sheet's numbers");
        }

        [Test]
        public void ReleasingStopsAtOnceAndTheNextHoldSpinsUpAgain()
        {
            Hold(Tuning.I.rattlerSpinUp + 0.5f);
            int fired = Fired;
            Idle(30);
            Assert.AreEqual(fired, Fired, "nothing leaves after release");

            int spinFrames = Mathf.FloorToInt(Tuning.I.rattlerSpinUp / Dt) - 1;
            for (int i = 0; i < spinFrames; i++) Frame(true);
            Assert.AreEqual(fired, Fired, "a new hold spins up again first");
        }

        [Test]
        public void ThePressAloneDoesNothing()
        {
            ctx.Pressed = true;
            ctx.Held = false;
            behaviour.Tick(ctx, Dt);
            Assert.AreEqual(0, Fired, "it fires only while the button is held");
        }

        [Test]
        public void NothingFiresOrIsSpentWhenTheMuzzleIsInATile()
        {
            ctx.CanFire = false;
            Hold(1f);
            Assert.AreEqual(0, Fired);
            Assert.AreEqual(0, world.Shots.Count);
        }

        // ------------------------------------------------------------------ spread

        [Test]
        public void BulletsStayInsideTheSpreadAndFanOut()
        {
            ctx.Muzzle = new Vector2(0.5f, 0.5f);
            float maxDeg = 0f;
            var xs = new HashSet<float>();
            for (int i = 0; i < 40; i++)
            {
                Frame(true);
                foreach (var s in world.Shots)
                {
                    float deg = Vector2.Angle(Vector2.up, s.Velocity);
                    maxDeg = Mathf.Max(maxDeg, deg);
                    xs.Add(Mathf.Round(s.Velocity.x * 1000f));
                    Assert.AreEqual(Tuning.I.rattlerSpeed, s.Velocity.magnitude, 1e-3f);
                    Assert.AreEqual(Tuning.I.rattlerBossDamage, s.BossDamage, 1e-6f);
                }
            }
            Assert.LessOrEqual(maxDeg, Tuning.I.rattlerSpread + 1e-3f, "never wider than rattlerSpread either side");
            Assert.Greater(xs.Count, 1, "the stream is a narrow wedge, not a line");
        }

        [Test]
        public void TheSameSeedFiresTheSameStream()
        {
            List<float> Angles(int seed)
            {
                var w = new WeaponWorld(new List<Bubble>(), new List<Roller>(), null, null, root.transform);
                var r = new Run(1);
                r.GiveWeapon(rattler);
                var b = rattler.CreateBehaviour();
                var c = new WeaponContext { CanFire = true, Muzzle = new Vector2(0f, 0.5f), Up = Vector2.up, Spec = rattler, World = w, Run = r, Rng = new System.Random(seed) };
                typeof(WeaponContext).GetField("spend", BindingFlags.NonPublic | BindingFlags.Instance)
                    .SetValue(c, (System.Func<int, bool>)(n => r.SpendAmmo(WeaponSlot.Weapon, n)));
                var list = new List<float>();
                for (int i = 0; i < 40; i++)
                {
                    c.Held = true;
                    int before = w.Shots.Count;
                    b.Tick(c, Dt);
                    for (int k = before; k < w.Shots.Count; k++) list.Add(w.Shots[k].Velocity.x);
                }
                w.Destroy();
                return list;
            }
            CollectionAssert.AreEqual(Angles(7), Angles(7));
        }

        [Test]
        public void SpreadDirectionCoversBothSides()
        {
            float spread = Tuning.I.rattlerSpread;
            Assert.AreEqual(spread, Vector2.SignedAngle(Vector2.up, Rattler.SpreadDirection(Vector2.up, 1f)), 1e-3f);
            Assert.AreEqual(-spread, Vector2.SignedAngle(Vector2.up, Rattler.SpreadDirection(Vector2.up, 0f)), 1e-3f);
            Assert.AreEqual(0f, Vector2.SignedAngle(Vector2.up, Rattler.SpreadDirection(Vector2.up, 0.5f)), 1e-3f);
        }

        // ------------------------------------------------------------------ Wick slowed

        [Test]
        public void HeWalksSlowerWhileHoldingAndNormalOnRelease()
        {
            Frame(true);
            Assert.AreEqual(Tuning.I.rattlerWalkScale, wick.WeaponWalkScale, 1e-6f, "slowed from the first frame of the hold, spin-up included");
            Hold(0.5f);
            Assert.AreEqual(Tuning.I.rattlerWalkScale, wick.WeaponWalkScale, 1e-6f);
            Frame(false);
            Assert.AreEqual(1f, wick.WeaponWalkScale, 1e-6f, "back to normal on release");
        }

        [Test]
        public void TheWalkScaleSlowsHisRun()
        {
            float Distance(float scale)
            {
                wick.ResetForStage(0f, 0f, 3);
                wick.WeaponWalkScale = scale;
                for (int i = 0; i < 30; i++) wick.Tick(1f, 0f, Dt, true);
                float x = wick.transform.position.x;
                wick.WeaponWalkScale = 1f;
                return x;
            }
            float normal = Distance(1f), slowed = Distance(Tuning.I.rattlerWalkScale);
            Assert.Greater(normal, 0.5f);
            Assert.AreEqual(normal * Tuning.I.rattlerWalkScale, slowed, normal * 0.1f, "his walk is scaled by rattlerWalkScale");
        }

        [Test]
        public void EmptyingTheSlotGivesHimHisSpeedBack()
        {
            while (run.Weapon != null) Frame(true);
            behaviour.Dispose(); // the game disposes the behaviour when the slot empties
            Assert.AreEqual(1f, wick.WeaponWalkScale, 1e-6f);
            Assert.AreEqual(1f, wick.WeaponFallScale, 1e-6f);
        }

        // ------------------------------------------------------------------ air and down shots

        [Test]
        public void ItFiresUpInTheAirWithoutSlowingTheFall()
        {
            ctx.InAir = true;
            Hold(Tuning.I.rattlerSpinUp + 0.1f);
            Assert.Greater(world.Shots[0].Velocity.y, 0f);
            Assert.AreEqual(1f, wick.WeaponFallScale, 1e-6f, "only a down shot slows his fall");
        }

        [Test]
        public void FallingWithDownHeldFiresDownAndSlowsTheFall()
        {
            ctx.Muzzle = new Vector2(0.5f, 8f);
            ctx.Up = Vector2.down;
            ctx.ShootDown = true;
            ctx.InAir = true;
            var below = AddBubble(new Vector2(0f, 3f), tier: 3);
            Hold(Tuning.I.rattlerSpinUp + 0.05f);
            Assert.Less(world.Shots[0].Velocity.y, 0f, "the bullets go down");
            Assert.AreEqual(Tuning.I.rattlerDownFallScale, wick.WeaponFallScale, 1e-6f);
            Idle(30);
            Assert.IsTrue(below.Popped);
            Assert.AreEqual(1f, wick.WeaponFallScale, 1e-6f, "released: normal fall again");
        }

        [Test]
        public void TheFallScaleSlowsHisFallByAThird()
        {
            float Drop(float scale)
            {
                wick.ResetForStage(0f, 12f, 3); // in the air over the open floor
                wick.WeaponFallScale = scale;
                float start = wick.FeetY;
                for (int i = 0; i < 20; i++) wick.Tick(0f, 0f, Dt, true);
                float drop = start - wick.FeetY;
                wick.WeaponFallScale = 1f;
                return drop;
            }
            float normal = Drop(1f), slowed = Drop(Tuning.I.rattlerDownFallScale);
            Assert.Greater(normal, 0.1f, "he falls");
            Assert.AreEqual(normal * Tuning.I.rattlerDownFallScale, slowed, normal * 0.05f);
        }

        // ------------------------------------------------------------------ rules

        [Test]
        public void SteelSparksTheBulletsOff()
        {
            var steel = AddBubble(new Vector2(ctx.Muzzle.x, 7f), tier: 3, steel: true);
            FireOne();
            Idle(30);
            Assert.IsFalse(steel.Popped);
            Assert.AreEqual(1, steelHits);
            Assert.AreEqual(0, world.Shots.Count, "the bullet is spent");
        }

        [Test]
        public void TwoBulletsBreakCeramic()
        {
            var c = AddBubble(new Vector2(ctx.Muzzle.x, 7f), tier: 3, ceramic: true);
            FireOne();
            Idle(30);
            Assert.IsFalse(c.Popped, "one bullet is one hit");
            FireOne();
            Idle(30);
            Assert.IsTrue(c.Popped, "each bullet is a hit, so two break it");
        }

        [Test]
        public void AFullCrateTakesAboutTwelveBossHealth()
        {
            var boss = Boss.Create(root.transform, BossSpec.ForWorld(1), 1, Themes.ForWorld(1));
            var w = new WeaponWorld(bubbles, rollers, null, () => boss, root.transform);
            int total = 0;
            w.OnDamageBoss = (n, _) => total += n;
            for (int i = 0; i < Tuning.I.rattlerAmmo; i++) w.WeaponHitBoss(Tuning.I.rattlerBossDamage, Vector2.zero);
            Assert.AreEqual(Mathf.FloorToInt(Tuning.I.rattlerAmmo * Tuning.I.rattlerBossDamage + 1e-3f), total);
            Assert.That(total, Is.InRange(8, 12), "a full crate takes 8 to 12 boss health");
            w.Destroy();
        }

        [Test]
        public void BulletsStopUnderAPlatform()
        {
            var map = StageMap.Flat();
            int col = StageMap.Col(ctx.Muzzle.x);
            map.Set(col, 5, new Cell { kind = TileKind.Solid });
            SetMap(map);
            var above = AddBubble(new Vector2(StageMap.CenterX(col), 9f), tier: 3);
            Hold(0.6f);
            Idle(30);
            Assert.AreEqual(0, world.Shots.Count);
            Assert.IsFalse(above.Popped, "the platform stopped every bullet");
            Assert.AreEqual(0, tileHits.Count);
        }

        [Test]
        public void TheStreamChewsThroughABreakableBlock()
        {
            var map = StageMap.Flat();
            int col = StageMap.Col(ctx.Muzzle.x);
            map.Set(col, 5, new Cell { kind = TileKind.Breakable, hp = (byte)Tuning.I.breakableHp });
            SetMap(map);
            Hold(Tuning.I.rattlerSpinUp + 1f);
            Idle(30);
            Assert.GreaterOrEqual(tileHits.Count, Tuning.I.breakableHp, "every bullet chips it");
            Assert.IsFalse(map.IsSolid(col, 5), "a second of fire breaks the block");
        }
    }
}
