using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Bubblewright.Tests
{
    /// <summary>W03 Scattergun: a fan of short-range pellets per press, a pump wait, the down-shot hop, shared hit rules.</summary>
    public class WeaponScattergunTests
    {
        const float Dt = 1f / 60f;

        GameObject root;
        StageMap previousMap;
        List<Bubble> bubbles;
        List<Roller> rollers;
        WeaponWorld world;
        Run run;
        WeaponSpec scatter;
        WeaponBehaviour behaviour;
        WeaponContext ctx;
        Effects fx;
        Wick wick;
        readonly List<Vector2Int> tileHits = new List<Vector2Int>();
        int steelHits, flashes;
        /// <summary>When set, a bubble that breaks splits into two halves at its spot, the way Game.Hit does.</summary>
        bool splitOnPop;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("WeaponScattergunTests");
            previousMap = Game.Map;
            SetMap(StageMap.Flat());
            bubbles = new List<Bubble>();
            rollers = new List<Roller>();
            tileHits.Clear();
            steelHits = flashes = 0;
            splitOnPop = false;
            world = new WeaponWorld(bubbles, rollers, null, null, root.transform)
            {
                OnHitBubble = b =>
                {
                    if (b.AbsorbHit()) return;
                    b.Swell(Tuning.I.popSwell);
                    if (splitOnPop && b.Tier > 0)
                    {
                        AddBubble(b.Position + new Vector2(-0.1f, 0f), b.Tier - 1);
                        AddBubble(b.Position + new Vector2(0.1f, 0f), b.Tier - 1);
                    }
                },
                OnHitRoller = r => { if (!r.AbsorbHit()) r.Swell(Tuning.I.popSwell); },
                OnHitTile = (c, r) => { tileHits.Add(new Vector2Int(c, r)); Game.Map.Damage(c, r); },
                OnSteel = _ => steelHits++,
            };

            fx = Effects.Create(root.transform);
            wick = Wick.Create(root.transform, fx);
            wick.ResetForStage(0f, 0f, 3);

            scatter = WeaponSpec.ById("scatter");
            Assert.NotNull(scatter, "the Scattergun is in WeaponSpec.All");
            run = new Run(1);
            Assert.IsTrue(run.GiveWeapon(scatter));
            behaviour = scatter.CreateBehaviour();
            ctx = new WeaponContext
            {
                CanFire = true,
                Muzzle = new Vector2(0.5f, 2f),
                Up = Vector2.up,
                Spec = scatter,
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

        /// <summary>One frame: the weapon reads its button, then the live pellets fly.</summary>
        void Frame(bool pressed)
        {
            ctx.Pressed = pressed;
            ctx.Held = pressed;
            behaviour.Tick(ctx, Dt);
            world.TickShots(Dt);
        }

        void Idle(int frames)
        {
            for (int i = 0; i < frames; i++) Frame(false);
        }

        /// <summary>Press once, then let the pellets finish and the pump wait end.</summary>
        void FireAndWait()
        {
            Frame(true);
            Idle(Mathf.CeilToInt(Tuning.I.scatterCooldown / Dt) + 2);
        }

        int Fired => scatter.MaxAmmo - run.WeaponAmmo;

        // ------------------------------------------------------------------ table and ammo

        [Test]
        public void TheScattergunIsAnUncommonWeaponSlotGunThatDrops()
        {
            Assert.AreEqual("Scattergun", scatter.Name);
            Assert.AreEqual(WeaponSlot.Weapon, scatter.Slot);
            Assert.AreEqual(WeaponTier.Uncommon, scatter.Tier);
            Assert.IsTrue(scatter.Drops);
            Assert.AreEqual(Tuning.I.scatterAmmo, scatter.MaxAmmo, "a full crate comes from Tuning");
            Assert.AreEqual(Tuning.I.scatterAmmo, run.WeaponAmmo);
            Assert.LessOrEqual(WeaponArt.WidthOf(scatter.IconRows), 11);
            Assert.NotNull(WeaponArt.Held(scatter));
        }

        [Test]
        public void APressFiresOneShellOfPellets()
        {
            Frame(true);
            Assert.AreEqual(1, Fired, "one shell per press");
            Assert.AreEqual(Tuning.I.scatterPellets, world.Shots.Count, "scatterPellets pellets");
            Assert.AreEqual(1, flashes);
            foreach (var s in world.Shots)
            {
                Assert.AreEqual(Tuning.I.scatterSpeed, s.Velocity.magnitude, 1e-3f);
                Assert.AreEqual(Tuning.I.scatterRange, s.Range, 1e-6f);
                Assert.AreEqual(Tuning.I.scatterBossDamage, s.BossDamage, 1e-6f);
            }
        }

        [Test]
        public void HoldingDoesNotFireAgain()
        {
            Frame(true);
            ctx.Pressed = false;
            ctx.Held = true;
            for (int i = 0; i < 120; i++) { behaviour.Tick(ctx, Dt); world.TickShots(Dt); }
            Assert.AreEqual(1, Fired, "one shell per press, holding fires nothing more");
        }

        [Test]
        public void ThePumpWaitBlocksPressesAndDimsTheButton()
        {
            Frame(true);
            Assert.IsTrue(behaviour.Dimmed, "dim while he pumps it");
            int waitFrames = Mathf.FloorToInt(Tuning.I.scatterCooldown / Dt) - 2;
            for (int i = 0; i < waitFrames; i++) Frame(true);
            Assert.AreEqual(1, Fired, "presses during the pump do nothing");
            Idle(4);
            Assert.IsFalse(behaviour.Dimmed, "lit again once pumped");
            Frame(true);
            Assert.AreEqual(2, Fired);
        }

        [Test]
        public void TheSlotEmptiesAfterTheLastShell()
        {
            for (int i = 0; i < Tuning.I.scatterAmmo; i++) FireAndWait();
            Assert.IsNull(run.Weapon, "the slot empties at zero");
            Assert.AreEqual(0, run.WeaponAmmo);
        }

        [Test]
        public void NothingFiresOrIsSpentWhenTheMuzzleIsInATile()
        {
            ctx.CanFire = false;
            Frame(true);
            Assert.AreEqual(0, Fired);
            Assert.AreEqual(0, world.Shots.Count);
            Assert.IsFalse(behaviour.Dimmed, "no pump wait for a shell never fired");
        }

        // ------------------------------------------------------------------ fan and range

        [Test]
        public void PelletsSpreadEvenlyAcrossTheFan()
        {
            int n = Tuning.I.scatterPellets;
            float fan = Tuning.I.scatterFan;
            Assert.AreEqual(-fan * 0.5f, Vector2.SignedAngle(Vector2.up, Scattergun.PelletDirection(Vector2.up, 0, n)), 1e-3f);
            Assert.AreEqual(fan * 0.5f, Vector2.SignedAngle(Vector2.up, Scattergun.PelletDirection(Vector2.up, n - 1, n)), 1e-3f);
            float step = fan / (n - 1);
            for (int i = 1; i < n; i++)
            {
                float a = Vector2.SignedAngle(Scattergun.PelletDirection(Vector2.up, i - 1, n), Scattergun.PelletDirection(Vector2.up, i, n));
                Assert.AreEqual(step, a, 1e-3f, "even gaps");
            }
            Assert.AreEqual(0f, Vector2.SignedAngle(Vector2.up, Scattergun.PelletDirection(Vector2.up, 0, 1)), 1e-3f, "one pellet goes straight");
        }

        [Test]
        public void PelletsFadeAtTheEndOfTheirRange()
        {
            var far = AddBubble(new Vector2(0.5f, 2f + Tuning.I.scatterRange + 2f), tier: 3);
            Frame(true);
            Idle(60);
            Assert.AreEqual(0, world.Shots.Count, "every pellet is gone");
            Assert.IsFalse(far.Popped, "a bubble beyond the range is never reached");
        }

        [Test]
        public void ABlastFromTheFloorNeverReachesTheCeiling()
        {
            ctx.Muzzle = new Vector2(0.5f, 2f); // about where his launcher tip is when standing
            var high = AddBubble(new Vector2(0.5f, Tuning.I.arenaHeight - 1.5f), tier: 2);
            Frame(true);
            float highest = 0f;
            for (int i = 0; i < 60; i++)
            {
                foreach (var s in world.Shots) highest = Mathf.Max(highest, s.Position.y);
                Frame(false);
            }
            Assert.IsFalse(high.Popped);
            Assert.Less(highest, Tuning.I.arenaHeight - 3f, "pellets fade well under the ceiling");
        }

        [Test]
        public void ABlastUnderTwoHalvesPopsBoth()
        {
            // The two halves of a bubble that just split, side by side above him.
            var left = AddBubble(new Vector2(-0.6f, 4f), tier: 1);
            var right = AddBubble(new Vector2(1.6f, 4f), tier: 1);
            Frame(true);
            Idle(30);
            Assert.IsTrue(left.Popped, "the left half");
            Assert.IsTrue(right.Popped, "the right half");
        }

        [Test]
        public void HalvesBornFromABlastAreSafeFromThatBlast()
        {
            splitOnPop = true;
            var big = AddBubble(new Vector2(0.5f, 3.2f), tier: 3);
            Frame(true);
            Idle(30);
            Assert.IsTrue(big.Popped);
            Assert.Greater(bubbles.Count, 1, "it split");
            for (int i = 1; i < bubbles.Count; i++)
                Assert.IsFalse(bubbles[i].Popped, "no pellet of the same blast hits a half it made");

            Idle(Mathf.CeilToInt(Tuning.I.scatterCooldown / Dt));
            Frame(true);
            Idle(30);
            bool anyHit = false;
            for (int i = 1; i < bubbles.Count; i++) anyHit |= bubbles[i].Popped;
            Assert.IsTrue(anyHit, "the next blast can hit them");
        }

        // ------------------------------------------------------------------ shared rules

        [Test]
        public void PelletsSparkOffSteel()
        {
            var steel = AddBubble(new Vector2(0.5f, 4f), tier: 3, steel: true);
            Frame(true);
            Idle(30);
            Assert.IsFalse(steel.Popped, "steel never pops");
            Assert.Greater(steelHits, 0, "it sparkles");
            Assert.AreEqual(0, world.Shots.Count, "the pellets that hit it are spent, the rest faded");
        }

        [Test]
        public void OneCloseBlastBreaksCeramic()
        {
            var c = AddBubble(new Vector2(0.5f, 3.5f), tier: 3, ceramic: true);
            Frame(true);
            Idle(30);
            Assert.IsTrue(c.Popped, "each pellet is a hit, so one close blast breaks it");
        }

        [Test]
        public void ABlastRightUnderTheBossTakesTwoHealth()
        {
            var boss = Boss.Create(root.transform, BossSpec.ForWorld(1), 1, Themes.ForWorld(1));
            var w = new WeaponWorld(bubbles, rollers, null, () => boss, root.transform);
            int total = 0;
            w.OnDamageBoss = (n, _) => total += n;
            var y = typeof(Boss).GetField("y", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(y, "Boss.y must exist.");
            y.SetValue(boss, 8f); // inside the arena, as when it has entered
            var body = boss.Body;
            Scattergun.Blast(w, new Vector2(body.center.x, body.yMin - 0.5f), Vector2.up);
            for (int i = 0; i < 60; i++) w.TickShots(Dt);
            int expected = Mathf.FloorToInt(Tuning.I.scatterPellets * Tuning.I.scatterBossDamage + 1e-3f);
            Assert.AreEqual(expected, total, "every pellet lands");
            Assert.AreEqual(2, total, "up to 2 health per shell with the sheet's numbers");
            w.Destroy();
        }

        [Test]
        public void PelletsStopUnderAPlatform()
        {
            var map = StageMap.Flat();
            for (int c = 10; c < 22; c++) map.Set(c, 4, new Cell { kind = TileKind.Solid });
            SetMap(map);
            var above = AddBubble(new Vector2(0.5f, 6.5f), tier: 2);
            Frame(true);
            Idle(30);
            Assert.IsFalse(above.Popped, "the platform stopped every pellet");
            Assert.AreEqual(0, tileHits.Count, "a solid tile is not hit");
        }

        [Test]
        public void ACloseBlastChipsABreakableBlockOncePerPellet()
        {
            var map = StageMap.Flat();
            int col = StageMap.Col(ctx.Muzzle.x);
            map.Set(col, 3, new Cell { kind = TileKind.Breakable, hp = 9 });
            SetMap(map);
            Frame(true);
            Idle(30);
            Assert.Greater(tileHits.Count, 1, "several pellets reach a block right above him");
            Assert.LessOrEqual(tileHits.Count, Tuning.I.scatterPellets, "at most one hit per pellet");
        }

        // ------------------------------------------------------------------ air and down shots

        [Test]
        public void ItFiresUpInTheAirWithoutAHop()
        {
            ctx.InAir = true;
            Frame(true);
            foreach (var s in world.Shots) Assert.Greater(s.Velocity.y, 0f);
            Assert.GreaterOrEqual(wick.VelocityY, -0.01f, "only a down shot hops him");
            Assert.LessOrEqual(wick.VelocityY, 0.01f);
        }

        void Airborne(float y)
        {
            wick.ResetForStage(0f, y, 3);
            for (int i = 0; i < 3; i++) wick.Tick(0f, 0f, Dt, true); // walks off into the air
            Assert.AreEqual(Mode.Fall, wick.Mode, "he is in the air");
            ctx.InAir = true;
            ctx.ShootDown = true;
            ctx.Up = Vector2.down;
            ctx.Muzzle = new Vector2(0.5f, y);
        }

        [Test]
        public void FiredDownInTheAirThePelletsGoDownAndHeHops()
        {
            Airborne(8f);
            float startY = wick.transform.position.y;
            Frame(true);
            foreach (var s in world.Shots) Assert.Less(s.Velocity.y, 0f, "the pellets go down");
            Assert.Greater(wick.VelocityY, 0f, "he is kicked upward");

            float top = startY;
            for (int i = 0; i < 90; i++)
            {
                wick.Tick(0f, 0f, Dt, true);
                top = Mathf.Max(top, wick.transform.position.y);
            }
            Assert.AreEqual(Tuning.I.scatterHop, top - startY, 0.25f, "he rises about scatterHop units");
        }

        [Test]
        public void TheHopComesOncePerTimeInTheAir()
        {
            Airborne(10f);
            Frame(true);
            Idle(Mathf.CeilToInt(Tuning.I.scatterCooldown / Dt) + 1);
            wick.Tick(0f, 0f, Dt, true);
            float before = wick.VelocityY;
            Frame(true);
            Assert.AreEqual(2, Fired, "the second down shot still fires");
            Assert.AreEqual(before, wick.VelocityY, 1e-4f, "but gives no second hop");

            ctx.InAir = false; // he lands
            Frame(false);
            Airborne(10f);
            Idle(Mathf.CeilToInt(Tuning.I.scatterCooldown / Dt) + 1);
            Frame(true);
            Assert.Greater(wick.VelocityY, 0f, "a new time in the air gives the hop back");
        }

        [Test]
        public void HopDoesNothingOnTheGround()
        {
            wick.ResetForStage(0f, 0f, 3);
            wick.Hop(Tuning.I.scatterHop);
            Assert.AreEqual(Mode.Ground, wick.Mode);
            Assert.AreEqual(0f, wick.VelocityY, 1e-6f);
        }
    }
}
