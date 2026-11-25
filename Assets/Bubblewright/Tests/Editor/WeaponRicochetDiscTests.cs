using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Bubblewright.Tests
{
    /// <summary>W15 Ricochet Disc: slanted discs that bounce off walls, tiles, steel and the boss and pop up to three things.</summary>
    public class WeaponRicochetDiscTests
    {
        const float Dt = 1f / 60f;

        GameObject root;
        StageMap previousMap;
        List<Bubble> bubbles;
        List<Roller> rollers;
        WeaponWorld world;
        Run run;
        WeaponSpec disc;
        RicochetDisc.Behaviour behaviour;
        WeaponContext ctx;
        readonly List<Vector2Int> tileHits = new List<Vector2Int>();
        int steelHits, flashes;
        /// <summary>When set, a bubble that breaks splits into two smaller ones at its spot, the way Game.Hit does.</summary>
        bool splitOnPop;

        DiscField Field => RicochetDisc.FieldOf(world);

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("WeaponRicochetDiscTests");
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
                    if (splitOnPop && b.Tier < 3)
                    {
                        AddBubble(b.Position + new Vector2(-0.1f, 0f), b.Tier + 1);
                        AddBubble(b.Position + new Vector2(0.1f, 0f), b.Tier + 1);
                    }
                },
                OnHitRoller = r => { if (!r.AbsorbHit()) r.Swell(Tuning.I.popSwell); },
                OnHitTile = (c, r) => { tileHits.Add(new Vector2Int(c, r)); Game.Map.Damage(c, r); },
                OnSteel = _ => steelHits++,
            };

            disc = WeaponSpec.ById("disc");
            Assert.NotNull(disc, "the Ricochet Disc is in WeaponSpec.All");
            run = new Run(1);
            Assert.IsTrue(run.GiveWeapon(disc));
            behaviour = (RicochetDisc.Behaviour)disc.CreateBehaviour();
            ctx = new WeaponContext
            {
                CanFire = true,
                Muzzle = new Vector2(0.5f, 2f),
                Up = Vector2.up,
                Spec = disc,
                World = world,
                Run = run,
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

        /// <summary>One frame: the weapon reads its button, then shots and discs fly.</summary>
        void Frame(bool pressed)
        {
            ctx.Pressed = pressed;
            ctx.Held = pressed;
            behaviour.Tick(ctx, Dt);
            world.TickShots(Dt);
        }

        void Fly(int frames)
        {
            for (int i = 0; i < frames; i++) world.TickShots(Dt);
        }

        static int Frames(float seconds) => Mathf.CeilToInt(seconds / Dt);

        // ------------------------------------------------------------------ table and ammo

        [Test]
        public void TheRicochetDiscIsAnUncommonWeaponSlotWeaponThatDrops()
        {
            Assert.AreEqual("Ricochet Disc", disc.Name);
            Assert.AreEqual(WeaponSlot.Weapon, disc.Slot);
            Assert.AreEqual(WeaponTier.Uncommon, disc.Tier);
            Assert.IsTrue(disc.Drops);
            Assert.AreEqual(Tuning.I.discAmmo, disc.MaxAmmo);
            Assert.AreEqual(Tuning.I.discAmmo, run.WeaponAmmo, "a crate comes full");
        }

        [Test]
        public void APressLaunchesOneSlantedDiscAndSpendsOne()
        {
            Frame(true);
            Assert.AreEqual(disc.MaxAmmo - 1, run.WeaponAmmo);
            Assert.AreEqual(1, Field.Count);
            Assert.AreEqual(1, flashes);
            var v = Field.Discs[0].Velocity;
            Assert.AreEqual(Tuning.I.discSpeed, v.magnitude, 1e-3f);
            Assert.AreEqual(Tuning.I.discAngle, Vector2.Angle(Vector2.up, v), 0.1f, "discAngle off straight up");
            Assert.Greater(v.x, 0f, "leans right by default");
        }

        [Test]
        public void ItLeansTheWayHeLastMoved()
        {
            behaviour.Side = -1;
            Frame(true);
            Assert.Less(Field.Discs[0].Velocity.x, 0f);
            Assert.Greater(Field.Discs[0].Velocity.y, 0f);
        }

        [Test]
        public void DownInTheAirLaunchesDownwardAtTheSameSlant()
        {
            ctx.ShootDown = true;
            ctx.InAir = true;
            ctx.Up = Vector2.down;
            ctx.Muzzle = new Vector2(0.5f, 6f);
            Frame(true);
            var v = Field.Discs[0].Velocity;
            Assert.Less(v.y, 0f);
            Assert.AreEqual(Tuning.I.discAngle, Vector2.Angle(Vector2.down, v), 0.1f);
        }

        [Test]
        public void HoldingDoesNotLaunchAgain()
        {
            Frame(true);
            ctx.Pressed = false;
            ctx.Held = true;
            for (int i = 0; i < 30; i++) { behaviour.Tick(ctx, Dt); world.TickShots(Dt); }
            Assert.AreEqual(disc.MaxAmmo - 1, run.WeaponAmmo);
        }

        [Test]
        public void AtMostDiscMaxAliveFlyAndExtraPressesSpendNothing()
        {
            int max = Tuning.I.discMaxAlive;
            for (int i = 0; i < max; i++) { Frame(true); Frame(false); }
            Assert.AreEqual(max, Field.Count);
            Assert.IsTrue(behaviour.Dimmed, "dim while the most discs fly");
            Frame(true);
            Assert.AreEqual(max, Field.Count);
            Assert.AreEqual(disc.MaxAmmo - max, run.WeaponAmmo, "a blocked press spends nothing");
        }

        [Test]
        public void TheSlotEmptiesAtZeroButTheLastDiscFliesOn()
        {
            Assert.IsTrue(run.SpendAmmo(WeaponSlot.Weapon, disc.MaxAmmo - 1));
            Frame(true);
            Assert.IsNull(run.Weapon, "the slot empties with the last disc");
            behaviour.Dispose();
            Vector2 at = Field.Discs[0].Position;
            Fly(5);
            Assert.AreEqual(1, Field.Count, "the disc outlives the slot");
            Assert.AreNotEqual(at, Field.Discs[0].Position);
        }

        [Test]
        public void NothingFiresWhenTheMuzzleIsInATile()
        {
            ctx.CanFire = false;
            Frame(true);
            Assert.AreEqual(0, Field.Count);
            Assert.AreEqual(disc.MaxAmmo, run.WeaponAmmo);
        }

        // ------------------------------------------------------------------ bounces

        [Test]
        public void ItBouncesOffTheArenaWall()
        {
            var d = Field.Launch(new Vector2(14f, 3f), new Vector2(Tuning.I.discSpeed, 0f));
            Fly(10);
            Assert.Less(d.Velocity.x, 0f);
            Assert.AreEqual(1, d.Bounces);
            Assert.LessOrEqual(d.Position.x + DiscField.Radius, Tuning.I.HalfWidth + 1e-4f);
        }

        [Test]
        public void ItBouncesOffTheCeilingAndTheFloor()
        {
            float ceiling = Tuning.I.arenaHeight;
            var d = Field.Launch(new Vector2(0.5f, ceiling - 2f), new Vector2(0f, Tuning.I.discSpeed));
            Fly(10);
            Assert.Less(d.Velocity.y, 0f, "off the ceiling");
            Fly(Frames(ceiling / Tuning.I.discSpeed));
            Assert.Greater(d.Velocity.y, 0f, "off the floor line");
            Assert.AreEqual(2, d.Bounces);
        }

        [Test]
        public void ItBouncesOffTheUndersideOfAPlatform()
        {
            var map = StageMap.Flat();
            for (int c = 10; c < 22; c++) map.Set(c, 4, new Cell { kind = TileKind.Solid });
            SetMap(map);
            var above = AddBubble(new Vector2(0.5f, 6.5f), tier: 3);
            var d = Field.Launch(new Vector2(0.5f, 2f), new Vector2(0f, Tuning.I.discSpeed));
            Fly(10);
            Assert.Less(d.Velocity.y, 0f);
            Assert.AreEqual(1, d.Bounces);
            Assert.LessOrEqual(d.Position.y + DiscField.Radius, 4f);
            Assert.IsFalse(above.Popped, "nothing gets through the platform");
            Assert.AreEqual(0, tileHits.Count, "a solid tile is not hit");
        }

        [Test]
        public void ABreakableBlockTakesOneHitAndStillBouncesIt()
        {
            var map = StageMap.Flat();
            map.Set(16, 5, new Cell { kind = TileKind.Breakable, hp = 9 });
            SetMap(map);
            var d = Field.Launch(new Vector2(0.5f, 2f), new Vector2(0f, Tuning.I.discSpeed));
            Fly(10);
            Assert.AreEqual(1, tileHits.Count);
            Assert.AreEqual(new Vector2Int(16, 5), tileHits[0]);
            Assert.Less(d.Velocity.y, 0f, "bounced back down");
        }

        [Test]
        public void ItEndsAfterDiscBouncesBouncesThenFlickersAndBreaks()
        {
            // A one-tile-wide shaft: it bounces between the walls quickly.
            var map = StageMap.Flat();
            for (int r = 0; r < map.Rows; r++)
            {
                map.Set(15, r, new Cell { kind = TileKind.Solid });
                map.Set(17, r, new Cell { kind = TileKind.Solid });
            }
            SetMap(map);
            var d = Field.Launch(new Vector2(0.5f, 7f), new Vector2(Tuning.I.discSpeed, 0f));
            Fly(Frames(0.3f));
            Assert.AreEqual(Tuning.I.discBounces, d.Bounces, "flickering bounces do not count");
            Fly(Frames(Tuning.I.discFlickerTime) + 2);
            Assert.AreEqual(0, Field.Count, "broken after its flicker");
            Assert.IsFalse(d.Alive);
        }

        [Test]
        public void EveryDiscIsGoneByItsLifeTime()
        {
            Field.Launch(new Vector2(0f, 7f), new Vector2(3f, 1f)); // slow: few bounces
            Fly(Frames(Tuning.I.discLife * 0.9f));
            Assert.AreEqual(1, Field.Count, "still flying before discLife");
            Fly(Frames(Tuning.I.discLife * 0.1f + Tuning.I.discFlickerTime) + 2);
            Assert.AreEqual(0, Field.Count);
        }

        [Test]
        public void ItDropsThroughAFloorGap()
        {
            var map = StageMap.Flat();
            map.SetGap(16, true);
            SetMap(map);
            Field.Launch(new Vector2(0.5f, 2f), new Vector2(0f, -Tuning.I.discSpeed));
            Fly(20);
            Assert.AreEqual(0, Field.Count, "lost through the gap");
        }

        // ------------------------------------------------------------------ pops

        [Test]
        public void ItPassesThroughWhatItPopsUpToDiscPops()
        {
            var row = new List<Bubble>();
            for (int i = 0; i < 5; i++) row.Add(AddBubble(new Vector2(-6f + i * 2.5f, 3f), tier: 3));
            var d = Field.Launch(new Vector2(-10f, 3f), new Vector2(Tuning.I.discSpeed, 0f));
            Fly(Frames(1f));
            int popped = 0;
            foreach (var b in row) if (b.Popped) popped++;
            Assert.AreEqual(Tuning.I.discPops, popped, "pops discPops in a row, then ends");
            Assert.IsTrue(row[0].Popped && row[1].Popped && row[2].Popped);
            Assert.IsFalse(row[3].Popped, "the flickering disc hits nothing");
            Assert.AreEqual(Tuning.I.discPops, d.Pops);
        }

        [Test]
        public void CeramicTakesOneHitPerTouchAndCountsAsAPop()
        {
            var ceramic = AddBubble(new Vector2(0f, 3f), tier: 2, ceramic: true);
            var d = Field.Launch(new Vector2(-4f, 3f), new Vector2(Tuning.I.discSpeed, 0f));
            Fly(Frames(0.3f));
            Assert.IsFalse(ceramic.Popped, "one touch cracks it");
            Assert.AreEqual(1, d.Pops);
        }

        [Test]
        public void SteelBouncesItAndCountsAsABounce()
        {
            var steel = AddBubble(new Vector2(3f, 3f), tier: 2, steel: true);
            var d = Field.Launch(new Vector2(0f, 3f), new Vector2(Tuning.I.discSpeed, 0f));
            Fly(10);
            Assert.Less(d.Velocity.x, 0f, "turned round by the steel ball");
            Assert.AreEqual(1, d.Bounces);
            Assert.AreEqual(1, steelHits);
            Assert.AreEqual(0, d.Pops);
            Assert.IsFalse(steel.Popped);
        }

        [Test]
        public void ASteelRollingBallBouncesItToo()
        {
            var roller = Roller.Create(root.transform, 1, 3f, 0f, 1, steel: true);
            rollers.Add(roller);
            var d = Field.Launch(new Vector2(0f, roller.Position.y), new Vector2(Tuning.I.discSpeed, 0f));
            Fly(10);
            Assert.Less(d.Velocity.x, 0f);
            Assert.AreEqual(1, d.Bounces);
            Assert.IsFalse(roller.Popped);
        }

        [Test]
        public void ARollingBalloonPops()
        {
            var roller = Roller.Create(root.transform, 1, 3f, 0f, 1);
            rollers.Add(roller);
            var d = Field.Launch(new Vector2(0f, roller.Position.y), new Vector2(Tuning.I.discSpeed, 0f));
            Fly(10);
            Assert.IsTrue(roller.Popped);
            Assert.AreEqual(1, d.Pops);
            Assert.Greater(d.Velocity.x, 0f, "passes through");
        }

        [Test]
        public void TheHalvesOfItsOwnPopAreSparedWhileTheyOverlapIt()
        {
            splitOnPop = true;
            var big = AddBubble(new Vector2(0f, 3f), tier: 1);
            var d = Field.Launch(new Vector2(-3f, 3f), new Vector2(Tuning.I.discSpeed, 0f));
            Fly(Frames(0.25f));
            Assert.IsTrue(big.Popped);
            Assert.AreEqual(3, bubbles.Count, "the pop made two halves");
            Assert.IsFalse(bubbles[1].Popped);
            Assert.IsFalse(bubbles[2].Popped);
            Assert.AreEqual(1, d.Pops);
        }

        // ------------------------------------------------------------------ boss

        [Test]
        public void TheBossTakesDiscBossDamageOncePerDiscAndBouncesIt()
        {
            var boss = Boss.Create(root.transform, BossSpec.ForWorld(1), 1, Themes.ForWorld(1));
            var w = new WeaponWorld(bubbles, rollers, null, () => boss, root.transform);
            int total = 0;
            w.OnDamageBoss = (n, _) => total += n;
            var y = typeof(Boss).GetField("y", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(y, "Boss.y must exist.");
            y.SetValue(boss, 8f); // inside the arena, as when it has entered
            var body = boss.Body;
            var field = RicochetDisc.FieldOf(w);
            var d = field.Launch(new Vector2(body.center.x, 1f), new Vector2(0f, Tuning.I.discSpeed));
            for (int i = 0; i < Frames(1.5f); i++) w.TickShots(Dt);
            Assert.AreEqual(Mathf.FloorToInt(Tuning.I.discBossDamage + 1e-3f), total, "once per disc, however often it bounces off");
            Assert.AreEqual(1, total);
            Assert.GreaterOrEqual(d.Bounces, 2, "bounced off the boss and the floor");
            w.Destroy();
        }

        // ------------------------------------------------------------------ cleanup

        [Test]
        public void ClearingTheShotsRemovesEveryDisc()
        {
            Frame(true);
            Frame(false);
            Frame(true);
            Assert.AreEqual(2, Field.Count);
            world.ClearShots();
            Assert.AreEqual(0, Field.Count);
            Assert.AreEqual(0, root.GetComponentsInChildren<SpriteRenderer>(true).Length - CountBubbleRenderers(),
                "disc and trail objects are destroyed");
        }

        int CountBubbleRenderers()
        {
            int n = 0;
            foreach (var b in bubbles) n += b.GetComponentsInChildren<SpriteRenderer>(true).Length;
            return n;
        }
    }
}
