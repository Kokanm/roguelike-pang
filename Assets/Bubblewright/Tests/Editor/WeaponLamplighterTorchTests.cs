using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Bubblewright.Tests
{
    /// <summary>W13 Lamplighter Torch: fuel in seconds, a flame cone cut at tiles, a hit per torchTick, Scorch and steel unharmed, Frost ice melts.</summary>
    public class WeaponLamplighterTorchTests
    {
        const float Dt = 1f / 60f;
        static readonly Vector2 Tip = new Vector2(0.5f, 2f);

        GameObject root;
        StageMap previousMap;
        List<Bubble> bubbles;
        List<Roller> rollers;
        WeaponWorld world;
        Run run;
        WeaponSpec torch;
        LamplighterTorch.Behaviour behaviour;
        WeaponContext ctx;
        Effects fx;
        Wick wick;
        int steelHits;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("WeaponLamplighterTorchTests");
            previousMap = Game.Map;
            SetMap(StageMap.Flat());
            Game.Ice.Clear();
            bubbles = new List<Bubble>();
            rollers = new List<Roller>();
            steelHits = 0;
            world = MakeWorld(null);

            fx = Effects.Create(root.transform);
            wick = Wick.Create(root.transform, fx);
            wick.ResetForStage(0f, 0f, 3);

            torch = WeaponSpec.ById("torch");
            Assert.NotNull(torch, "the Lamplighter Torch is in WeaponSpec.All");
            run = new Run(1);
            Assert.IsTrue(run.GiveWeapon(torch));
            behaviour = (LamplighterTorch.Behaviour)torch.CreateBehaviour();
            ctx = new WeaponContext
            {
                CanFire = true,
                Muzzle = Tip,
                Up = Vector2.up,
                Spec = torch,
                World = world,
                Run = run,
                Wick = wick,
                Rng = new System.Random(1),
            };
            var spend = typeof(WeaponContext).GetField("spend", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(spend, "WeaponContext.spend must exist.");
            spend.SetValue(ctx, (System.Func<int, bool>)(n => run.SpendAmmo(WeaponSlot.Weapon, n)));
        }

        [TearDown]
        public void TearDown()
        {
            behaviour?.Dispose();
            world.Destroy();
            Game.Ice.Clear();
            SetMap(previousMap);
            Object.DestroyImmediate(root);
        }

        WeaponWorld MakeWorld(System.Func<Boss> boss) => new WeaponWorld(bubbles, rollers, null, boss, root.transform)
        {
            OnHitBubble = b => { if (!b.AbsorbHit()) b.Swell(Tuning.I.popSwell); },
            OnHitRoller = r => { if (!r.AbsorbHit()) r.Swell(Tuning.I.popSwell); },
            OnHitTile = (c, r) => Game.Map.Damage(c, r),
            OnSteel = _ => steelHits++,
        };

        static void SetMap(StageMap map)
        {
            var property = typeof(Game).GetProperty("Map", BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(property, "Game.Map must exist.");
            property.SetValue(null, map);
        }

        Bubble AddBubble(Vector2 at, BubbleKind kind = BubbleKind.Gloop, int tier = 3, bool ceramic = false, bool steel = false)
        {
            var b = Bubble.Create(root.transform, kind, tier, at, Vector2.zero, false, ceramic, steel, steel);
            bubbles.Add(b);
            return b;
        }

        void Frame(bool held)
        {
            ctx.Pressed = false;
            ctx.Held = held;
            behaviour.Tick(ctx, Dt);
        }

        void Hold(float seconds)
        {
            int frames = Mathf.RoundToInt(seconds / Dt);
            for (int i = 0; i < frames; i++) Frame(true);
        }

        // ------------------------------------------------------------------ fuel

        [Test]
        public void AFullCrateIsTorchFuelSecondsOfAmmo()
        {
            Assert.AreEqual(Tuning.I.torchFuel, torch.MaxAmmo);
            Assert.AreEqual(Tuning.I.torchFuel, run.WeaponAmmo);
            Assert.AreEqual(WeaponSlot.Weapon, torch.Slot);
        }

        [Test]
        public void HoldingEmptiesTheFuelInTorchFuelSeconds()
        {
            Hold(1f);
            Assert.AreEqual(Tuning.I.torchFuel - 1, run.WeaponAmmo, "one second of flame spends one second of fuel");
            Hold(Tuning.I.torchFuel - 1.1f);
            Assert.AreSame(torch, run.Weapon, "a part-burned last second is still there");
            Hold(0.2f);
            Assert.IsNull(run.Weapon, "the slot empties when the fuel runs out");
        }

        [Test]
        public void APartBurnedSecondIsNotSpentAndTheGaugeShowsIt()
        {
            Assert.Less(behaviour.Gauge, 0f, "no gauge before the first frame");
            Frame(false);
            Assert.AreEqual(1f, behaviour.Gauge, 1e-4f, "a full crate is a full bar");
            Hold(0.5f);
            Assert.AreEqual(Tuning.I.torchFuel, run.WeaponAmmo, "the saved run rounds the fuel up");
            Assert.AreEqual((Tuning.I.torchFuel - 0.5f) / Tuning.I.torchFuel, behaviour.Gauge, 0.01f, "the bar shrinks smoothly");
        }

        [Test]
        public void ReleasingStopsTheFlameAndTheFuel()
        {
            Hold(0.5f);
            for (int i = 0; i < 120; i++) Frame(false);
            Assert.IsFalse(behaviour.Burning);
            Assert.AreEqual(0.5f, behaviour.Burned, 0.02f, "no fuel burns while the button is up");
        }

        [Test]
        public void NothingBurnsWhenTheTipIsInsideATile()
        {
            var b = AddBubble(Tip + Vector2.up * 1.5f);
            ctx.CanFire = false;
            Hold(0.5f);
            Assert.IsFalse(b.Popped);
            Assert.AreEqual(Tuning.I.torchFuel, run.WeaponAmmo);
            Assert.AreEqual(0f, behaviour.Burned);
        }

        // ------------------------------------------------------------------ the flame

        [Test]
        public void TheFlamePopsWhatIsInItsConeAndNothingPastIt()
        {
            var near = AddBubble(Tip + Vector2.up * 2f);
            var far = AddBubble(Tip + Vector2.up * (Tuning.I.torchLength + 1.5f));
            var side = AddBubble(Tip + new Vector2(3f, 0.5f));
            Frame(true);
            Assert.IsTrue(near.Popped, "inside the flame");
            Assert.IsFalse(far.Popped, "past the end of the flame");
            Assert.IsFalse(side.Popped, "outside the cone");
        }

        [Test]
        public void CeramicTakesAHitEveryTorchTick()
        {
            var c = AddBubble(Tip + Vector2.up * 2f, ceramic: true);
            Frame(true);
            Assert.IsFalse(c.Popped, "the first touch cracks it");
            Hold(Tuning.I.torchTick * 0.5f);
            Assert.IsFalse(c.Popped, "safe until torchTick has passed");
            Hold(Tuning.I.torchTick * 0.6f);
            Assert.IsTrue(c.Popped, "the second touch breaks it");
        }

        [Test]
        public void SteelIsLeftAloneWithoutASpark()
        {
            var s = AddBubble(Tip + Vector2.up * 2f, steel: true);
            Hold(1f);
            Assert.IsFalse(s.Popped);
            Assert.AreEqual(0, steelHits, "no spark and no clang every tick");
        }

        [Test]
        public void ScorchBubblesAreImmune()
        {
            var s = AddBubble(Tip + Vector2.up * 2f, BubbleKind.Scorch);
            Hold(1f);
            Assert.IsFalse(s.Popped);
        }

        [Test]
        public void ASolidTileCutsTheFlameShort()
        {
            var map = StageMap.Flat();
            int col = StageMap.Col(Tip.x);
            map.Set(col, 3, new Cell { kind = TileKind.Solid });
            SetMap(map);
            var above = AddBubble(new Vector2(Tip.x, 4.6f));
            Hold(0.5f);
            Assert.IsFalse(above.Popped, "the platform shields what is above it");
            Assert.Less(behaviour.Last.length, 1.01f, "the flame stops under the tile");
            Assert.AreEqual(TileKind.Solid, map.Get(col, 3).kind, "the flame does not harm blocks");
        }

        [Test]
        public void TheFlameDoesNotHarmBreakableBlocks()
        {
            var map = StageMap.Flat();
            int col = StageMap.Col(Tip.x);
            map.Set(col, 4, new Cell { kind = TileKind.Breakable, hp = 1 });
            SetMap(map);
            Hold(1f);
            Assert.AreEqual(TileKind.Breakable, map.Get(col, 4).kind);
        }

        [Test]
        public void ABubbleThatAppearsInTheFlameIsSafeForATick()
        {
            Frame(true);
            var born = AddBubble(Tip + Vector2.up * 2f);
            Frame(true);
            Assert.IsFalse(born.Popped, "a bubble born from a pop gets a moment to fly clear");
            Hold(Tuning.I.torchTick + 0.05f);
            Assert.IsTrue(born.Popped, "then it burns like any other");
        }

        [Test]
        public void TheBossLosesAboutOneHealthEveryPointSixSeconds()
        {
            var boss = Boss.Create(root.transform, BossSpec.ForWorld(1), 1, Themes.ForWorld(1));
            world.Destroy();
            world = MakeWorld(() => boss);
            ctx.World = world;
            int total = 0;
            world.OnDamageBoss = (n, _) => total += n;
            var y = typeof(Boss).GetField("y", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(y, "Boss.y must exist.");
            y.SetValue(boss, 8f);
            var body = boss.Body;
            ctx.Muzzle = new Vector2(body.center.x, body.yMin - 1f);
            Hold(1.2f);
            Assert.AreEqual(2, total, "1 health per 0.6 s in the flame");
            Hold(Tuning.I.torchFuel - 1.2f);
            Assert.That(total, Is.InRange(6, 7), "a full crate of fuel on the boss");
        }

        // ------------------------------------------------------------------ air, walking

        [Test]
        public void HeWalksSlowerWhileItBurnsAndNormallyAfter()
        {
            Frame(true);
            Assert.AreEqual(Tuning.I.torchWalkScale, wick.WeaponWalkScale, 1e-6f);
            Assert.AreEqual(1f, wick.WeaponFallScale, 1e-6f, "pointing up does not slow a fall");
            Frame(false);
            Assert.AreEqual(1f, wick.WeaponWalkScale, 1e-6f);
        }

        [Test]
        public void FiredDownItBurnsBelowAndSlowsHisFall()
        {
            ctx.ShootDown = true;
            ctx.InAir = true;
            ctx.Up = Vector2.down;
            ctx.Muzzle = new Vector2(0.5f, 6f);
            var below = AddBubble(new Vector2(0.5f, 4f));
            var above = AddBubble(new Vector2(0.5f, 8f));
            Frame(true);
            Assert.IsTrue(below.Popped);
            Assert.IsFalse(above.Popped);
            Assert.AreEqual(Tuning.I.torchDownFallScale, wick.WeaponFallScale, 1e-6f);
            behaviour.Dispose();
            Assert.AreEqual(1f, wick.WeaponFallScale, 1e-6f, "reset when the torch leaves the slot");
        }

        // ------------------------------------------------------------------ ice

        [Test]
        public void ADownwardFlameMeltsFrostIceUnderIt()
        {
            var map = StageMap.Flat();
            SetMap(map);
            int col = StageMap.Col(0.5f);
            Game.Ice.FreezeGround(map, col);
            Game.Ice.FreezeGround(map, col + 6);
            Assume.That(map.GetGround(col), Is.EqualTo(Surface.Ice));
            ctx.ShootDown = true;
            ctx.Up = Vector2.down;
            ctx.Muzzle = new Vector2(0.5f, 2.5f);
            Frame(true);
            Assert.AreEqual(Surface.Normal, map.GetGround(col), "melted back to what it was");
            Assert.AreEqual(Surface.Ice, map.GetGround(col + 6), "ice outside the flame stays");
            Assert.AreEqual(1, Game.Ice.Count);
        }

        [Test]
        public void ItMeltsIceOnTheTileItLandsOn()
        {
            var map = StageMap.Flat();
            int col = StageMap.Col(0.5f);
            map.Set(col, 3, new Cell { kind = TileKind.Solid, surface = Surface.Normal });
            SetMap(map);
            Game.Ice.FreezeTile(map, col, 3);
            Assume.That(map.Get(col, 3).surface, Is.EqualTo(Surface.Ice));
            ctx.ShootDown = true;
            ctx.Up = Vector2.down;
            ctx.Muzzle = new Vector2(0.5f, 6f);
            Frame(true);
            Assert.AreEqual(Surface.Normal, map.Get(col, 3).surface);
        }

        [Test]
        public void AFrostBubblePoppedByTheFlameLeavesNoIce()
        {
            var f = AddBubble(Tip + Vector2.up * 2f, BubbleKind.Frost);
            Frame(true);
            Assert.IsTrue(f.Popped);
            Assert.AreEqual(0, Game.Ice.Count);
        }
    }
}
