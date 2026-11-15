using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Bubblewright.Tests
{
    /// <summary>W04 Longshot: it aims by itself at the nearest thing in sight, then the shot goes through the whole line.</summary>
    public class WeaponLongshotTests
    {
        const float Dt = 1f / 60f;
        static readonly Vector2 Tip = new Vector2(0f, 2f);

        GameObject root;
        StageMap previousMap, map;
        List<Bubble> bubbles;
        List<Roller> rollers;
        WeaponWorld world;
        Run run;
        WeaponSpec longshot;
        Longshot.Behaviour behaviour;
        WeaponContext ctx;
        readonly List<Bubble> bubbleHits = new List<Bubble>();
        readonly List<Roller> rollerHits = new List<Roller>();
        readonly List<Vector2Int> tileHits = new List<Vector2Int>();
        int steelHits, flashes;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("WeaponLongshotTests");
            previousMap = Game.Map;
            map = StageMap.Flat();
            SetMap(map);
            bubbles = new List<Bubble>();
            rollers = new List<Roller>();
            bubbleHits.Clear();
            rollerHits.Clear();
            tileHits.Clear();
            steelHits = flashes = 0;
            world = MakeWorld(null);

            longshot = WeaponSpec.ById("longshot");
            Assert.NotNull(longshot, "the Longshot is in WeaponSpec.All");
            run = new Run(1);
            Assert.IsTrue(run.GiveWeapon(longshot));
            behaviour = (Longshot.Behaviour)longshot.CreateBehaviour();
            ctx = new WeaponContext
            {
                CanFire = true,
                Muzzle = Tip,
                Up = Vector2.up,
                Spec = longshot,
                World = world,
                Run = run,
                Rng = new System.Random(1),
            };
            SetInternal("spend", (System.Func<int, bool>)(n => run.SpendAmmo(WeaponSlot.Weapon, n)));
            SetInternal("flash", (System.Action)(() => flashes++));
        }

        WeaponWorld MakeWorld(Boss boss)
        {
            return new WeaponWorld(bubbles, rollers, null, boss == null ? (System.Func<Boss>)null : () => boss, root.transform)
            {
                OnHitBubble = b => { bubbleHits.Add(b); if (!b.AbsorbHit()) b.Swell(Tuning.I.popSwell); },
                OnHitRoller = r => { rollerHits.Add(r); if (!r.AbsorbHit()) r.Swell(Tuning.I.popSwell); },
                OnHitTile = (c, r) => { tileHits.Add(new Vector2Int(c, r)); Game.Map.Damage(c, r); },
                OnSteel = _ => steelHits++,
            };
        }

        [TearDown]
        public void TearDown()
        {
            behaviour?.Dispose();
            world.Destroy();
            SetMap(previousMap);
            Object.DestroyImmediate(root);
        }

        static void SetMap(StageMap m)
        {
            var property = typeof(Game).GetProperty("Map", BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(property, "Game.Map must exist.");
            property.SetValue(null, m);
        }

        void SetInternal(string field, object value)
        {
            var f = typeof(WeaponContext).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(f, $"WeaponContext.{field} must exist.");
            f.SetValue(ctx, value);
        }

        Bubble AddBubble(Vector2 at, int tier = 3, bool ceramic = false, bool steel = false, BubbleKind kind = BubbleKind.Gloop)
        {
            var b = Bubble.Create(root.transform, kind, tier, at, Vector2.zero, false, ceramic, steel, steel);
            bubbles.Add(b);
            return b;
        }

        Roller AddRoller(float x, bool steel = false)
        {
            var r = Roller.Create(root.transform, 0, x, 0f, 1, steel);
            rollers.Add(r);
            return r;
        }

        void Frame(bool pressed)
        {
            ctx.Pressed = pressed;
            ctx.Held = pressed;
            behaviour.Tick(ctx, Dt);
        }

        void Frames(int n)
        {
            for (int i = 0; i < n; i++) Frame(false);
        }

        static int AimFrames => Mathf.CeilToInt(Tuning.I.longshotAim / Dt) + 2;
        static int CooldownFrames => Mathf.CeilToInt(Tuning.I.longshotCooldown / Dt) + 2;

        /// <summary>Press once and wait out the aim.</summary>
        void Shoot()
        {
            Frame(true);
            Frames(AimFrames);
        }

        int Fired => longshot.MaxAmmo - run.WeaponAmmo;

        // ------------------------------------------------------------------ table and ammo

        [Test]
        public void TheLongshotIsARareWeaponSlotGunThatDrops()
        {
            Assert.AreEqual("Longshot", longshot.Name);
            Assert.AreEqual(WeaponSlot.Weapon, longshot.Slot);
            Assert.AreEqual(WeaponTier.Rare, longshot.Tier);
            Assert.IsTrue(longshot.Drops);
            Assert.AreEqual(Tuning.I.longshotAmmo, longshot.MaxAmmo, "a full crate comes from Tuning");
            Assert.AreEqual(Tuning.I.longshotAmmo, run.WeaponAmmo);
            Assert.LessOrEqual(WeaponArt.WidthOf(longshot.IconRows), 11);
            Assert.NotNull(WeaponArt.Held(longshot));
        }

        [Test]
        public void OnePressSpendsOneRoundAfterTheAimTime()
        {
            AddBubble(new Vector2(0f, 6f));
            Frame(true);
            Assert.AreEqual(Longshot.Phase.Aim, behaviour.Phase);
            Assert.AreEqual(0, Fired, "nothing is spent while aiming");
            Frames(Mathf.FloorToInt(Tuning.I.longshotAim / Dt) - 3);
            Assert.AreEqual(0, Fired, "still aiming before longshotAim");
            Frames(5);
            Assert.AreEqual(1, Fired);
            Assert.AreEqual(1, flashes, "the kick and flash");
        }

        [Test]
        public void TheSlotEmptiesAfterTheLastRound()
        {
            for (int i = 0; i < longshot.MaxAmmo; i++)
            {
                AddBubble(new Vector2(0f, 6f)); // a hit bubble swells and is gone, so a fresh one each round
                Shoot();
                Frames(CooldownFrames);
            }
            Assert.AreEqual(0, run.WeaponAmmo);
            Assert.IsNull(run.Weapon, "the slot empties at zero");
        }

        [Test]
        public void WithNothingInSightAPressDoesNothingAndTheButtonIsDim()
        {
            Frame(false);
            Assert.IsTrue(behaviour.Dimmed, "dim with nothing to shoot at");
            Shoot();
            Assert.AreEqual(0, Fired, "no round is spent");
            Assert.AreEqual(Longshot.Phase.Ready, behaviour.Phase);

            AddBubble(new Vector2(3f, 7f));
            Frame(false);
            Assert.IsFalse(behaviour.Dimmed, "lit once something is in sight");
        }

        [Test]
        public void ThePressWaitsOutTheCooldown()
        {
            AddBubble(new Vector2(0f, 6f));
            Shoot();
            Assert.AreEqual(1, Fired);
            AddBubble(new Vector2(2f, 6f));
            Shoot();
            Assert.AreEqual(1, Fired, "a press during longshotCooldown does nothing");
            Frames(CooldownFrames);
            Shoot();
            Assert.AreEqual(2, Fired);
        }

        // ------------------------------------------------------------------ targeting

        [Test]
        public void ItLocksOnTheNearestBubbleItCanSee()
        {
            var far = AddBubble(new Vector2(-6f, 9f));
            var near = AddBubble(new Vector2(4f, 5f));
            Frame(true);
            Assert.AreEqual(near, behaviour.Target.bubble);
            Frames(AimFrames);
            CollectionAssert.Contains(bubbleHits, near);
            CollectionAssert.DoesNotContain(bubbleHits, far);
        }

        [Test]
        public void TheSightLineFollowsAMovingTarget()
        {
            var b = AddBubble(new Vector2(-4f, 6f));
            Frame(true);
            for (int i = 0; i < AimFrames; i++)
            {
                b.transform.position = new Vector3(-4f + i * 0.2f, 6f, 0f);
                Frame(false);
            }
            CollectionAssert.Contains(bubbleHits, b, "the shot goes where the target moved to");
            Assert.Greater(behaviour.LastShot.end.x, 0f, "aimed to the right of where it started");
        }

        [Test]
        public void RollingBalloonsAreTargetsToo()
        {
            var r = AddRoller(3f);
            Shoot();
            CollectionAssert.Contains(rollerHits, r);
        }

        [Test]
        public void TheShotGoesThroughEverythingOnTheLine()
        {
            var a = AddBubble(new Vector2(0f, 5f));
            var b = AddBubble(new Vector2(0f, 8f));
            var c = AddBubble(new Vector2(0f, 11f));
            var off = AddBubble(new Vector2(3f, 11f));
            Shoot();
            CollectionAssert.Contains(bubbleHits, a);
            CollectionAssert.Contains(bubbleHits, b);
            CollectionAssert.Contains(bubbleHits, c);
            CollectionAssert.DoesNotContain(bubbleHits, off);
            Assert.AreEqual(Longshot.StopKind.Wall, behaviour.LastShot.stop);
            Assert.AreEqual(Tuning.I.arenaHeight, behaviour.LastShot.end.y, 1e-3f, "reaches the ceiling");
        }

        // ------------------------------------------------------------------ steel

        [Test]
        public void SteelIsNeverTargeted()
        {
            AddBubble(new Vector2(0f, 6f), 2, steel: true);
            AddRoller(3f, steel: true);
            Shoot();
            Assert.AreEqual(0, Fired);
            Assert.IsTrue(behaviour.Dimmed);
        }

        [Test]
        public void SteelInTheWayHidesWhatIsBehindIt()
        {
            AddBubble(new Vector2(0f, 6f), 2, steel: true);
            var behind = AddBubble(new Vector2(0f, 10f));
            var seen = AddBubble(new Vector2(6f, 9f));
            Shoot();
            CollectionAssert.Contains(bubbleHits, seen);
            CollectionAssert.DoesNotContain(bubbleHits, behind);
        }

        [Test]
        public void ASteelBallBeyondTheTargetStopsTheShot()
        {
            var target = AddBubble(new Vector2(0f, 5f));
            AddBubble(new Vector2(0f, 8f), 2, steel: true);
            var beyond = AddBubble(new Vector2(0f, 11.5f));
            Shoot();
            CollectionAssert.Contains(bubbleHits, target);
            CollectionAssert.DoesNotContain(bubbleHits, beyond);
            Assert.AreEqual(Longshot.StopKind.Steel, behaviour.LastShot.stop);
            Assert.AreEqual(1, steelHits, "the steel ball sparks");
        }

        // ------------------------------------------------------------------ ceramic and Ghosts

        [Test]
        public void CeramicBreaksInOneShot()
        {
            var b = AddBubble(new Vector2(0f, 6f), ceramic: true);
            Assert.Greater(b.Armor, 1);
            Shoot();
            Assert.AreEqual(1, Fired);
            Assert.AreEqual(1, b.Armor, "every armour hit is taken");
            Assert.AreEqual(Mathf.Max(2, Tuning.I.ceramicHits), bubbleHits.FindAll(x => x == b).Count, "and the last hit pops it");
        }

        [Test]
        public void AFadedGhostIsNeverTargetedAndTheShotPassesThroughIt()
        {
            var g = Bubble.Create(root.transform, BubbleKind.Ghost, 3, new Vector2(0f, 6f), Vector2.zero, false);
            bubbles.Add(g);
            for (int i = 0; i < 400 && g.CanBeHit; i++) g.Tick(Dt);
            g.transform.position = new Vector3(0f, 6f, 0f);
            Assert.IsFalse(g.CanBeHit, "the Ghost has faded");

            Shoot();
            Assert.AreEqual(0, Fired, "a faded Ghost alone is not a target");

            var beyond = AddBubble(new Vector2(0f, 10f));
            Shoot();
            CollectionAssert.Contains(bubbleHits, beyond);
            CollectionAssert.DoesNotContain(bubbleHits, g);
        }

        // ------------------------------------------------------------------ tiles

        [Test]
        public void ASolidTileHidesABubbleAndTheNearestVisibleOneIsChosen()
        {
            map.Set(StageMap.Col(0f), 4, new Cell { kind = TileKind.Solid });
            var hidden = AddBubble(new Vector2(0f, 6f));
            var seen = AddBubble(new Vector2(-6f, 3f));
            Shoot();
            CollectionAssert.Contains(bubbleHits, seen);
            CollectionAssert.DoesNotContain(bubbleHits, hidden);
        }

        [Test]
        public void ASolidTileBeyondTheTargetStopsTheShotUnharmed()
        {
            int c = StageMap.Col(0f);
            map.Set(c, 8, new Cell { kind = TileKind.Solid });
            var target = AddBubble(new Vector2(0f, 5f));
            var beyond = AddBubble(new Vector2(0f, 11f));
            Shoot();
            CollectionAssert.Contains(bubbleHits, target);
            CollectionAssert.DoesNotContain(bubbleHits, beyond);
            Assert.AreEqual(Longshot.StopKind.Tile, behaviour.LastShot.stop);
            Assert.AreEqual(8f, behaviour.LastShot.end.y, 0.06f);
            Assert.AreEqual(0, tileHits.Count);
            Assert.AreEqual(TileKind.Solid, map.Get(c, 8).kind);
        }

        [Test]
        public void ABreakableBlockInTheWayIsDestroyedAndStopsTheShot()
        {
            int c = StageMap.Col(0f);
            map.Set(c, 8, new Cell { kind = TileKind.Breakable, hp = 3 });
            var target = AddBubble(new Vector2(0f, 5f));
            var beyond = AddBubble(new Vector2(0f, 11f));
            Shoot();
            CollectionAssert.Contains(bubbleHits, target);
            CollectionAssert.DoesNotContain(bubbleHits, beyond);
            Assert.IsTrue(behaviour.LastShot.brokeTile);
            Assert.AreEqual(TileKind.Empty, map.Get(c, 8).kind);
        }

        // ------------------------------------------------------------------ boss

        [Test]
        public void OnABossStageWithNoBubblesItAimsAtTheBoss()
        {
            var boss = Boss.Create(root.transform, BossSpec.ForWorld(1), 1, Themes.ForWorld(1));
            world.Destroy();
            world = MakeWorld(boss);
            ctx.World = world;
            int total = 0;
            world.OnDamageBoss = (n, _) => total += n;
            var y = typeof(Boss).GetField("y", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(y, "Boss.y must exist.");
            y.SetValue(boss, 9f);

            Frame(true);
            Assert.AreEqual(TargetKind.Boss, behaviour.Target.kind);
            Frames(AimFrames);
            Assert.IsTrue(behaviour.LastShot.boss);
            Assert.AreEqual(Mathf.FloorToInt(Tuning.I.longshotBossDamage + 1e-3f), total);
        }

        [Test]
        public void ABubbleIsChosenBeforeTheBoss()
        {
            var boss = Boss.Create(root.transform, BossSpec.ForWorld(1), 1, Themes.ForWorld(1));
            world.Destroy();
            world = MakeWorld(boss);
            ctx.World = world;
            var b = AddBubble(new Vector2(-10f, 12f));
            Frame(true);
            Assert.AreEqual(TargetKind.Bubble, behaviour.Target.kind);
            Assert.AreEqual(b, behaviour.Target.bubble);
        }

        [Test]
        public void AFullCrateTakesAroundTwelveBossHealth()
        {
            int crate = Mathf.FloorToInt(Tuning.I.longshotAmmo * Tuning.I.longshotBossDamage + 1e-3f);
            Assert.That(crate, Is.InRange(8, 12), "a full crate takes about 8 to 12 boss health");
        }

        // ------------------------------------------------------------------ air and losing the target

        [Test]
        public void DownDoesNothingItStillPicksItsOwnAngle()
        {
            ctx.ShootDown = true;
            ctx.InAir = true;
            ctx.Up = Vector2.down;
            var b = AddBubble(new Vector2(5f, 9f));
            Shoot();
            Assert.AreEqual(1, Fired);
            CollectionAssert.Contains(bubbleHits, b);
        }

        [Test]
        public void LosingTheTargetWithNothingElseInSightSpendsNothing()
        {
            var b = AddBubble(new Vector2(0f, 6f));
            Frame(true);
            Assert.AreEqual(Longshot.Phase.Aim, behaviour.Phase);
            bubbles.Remove(b);
            b.Popped = true;
            Frames(AimFrames);
            Assert.AreEqual(0, Fired);
            Assert.AreEqual(Longshot.Phase.Ready, behaviour.Phase);
        }

        [Test]
        public void LosingTheTargetMovesTheSightToTheNextOne()
        {
            var first = AddBubble(new Vector2(0f, 6f));
            var next = AddBubble(new Vector2(-7f, 10f));
            Frame(true);
            Assert.AreEqual(first, behaviour.Target.bubble);
            bubbles.Remove(first);
            first.Popped = true;
            Frames(AimFrames);
            Assert.AreEqual(1, Fired);
            CollectionAssert.Contains(bubbleHits, next);
        }
    }
}
