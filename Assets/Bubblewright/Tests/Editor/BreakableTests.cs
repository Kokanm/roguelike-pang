using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Bubblewright.Tests
{
    public class BreakableTests
    {
        GameObject root;
        StageMap previousMap;

        [SetUp]
        public void SetUp()
        {
            previousMap = Game.Map;
            root = new GameObject("BreakableTests");
        }

        [TearDown]
        public void TearDown()
        {
            SetMap(previousMap);
            Object.DestroyImmediate(root);
        }

        [Test]
        public void DamageNeedsTwoHitsToDestroyBreakableAndIgnoresSolid()
        {
            var map = StageMap.Flat();
            map.Set(3, 4, new Cell { kind = TileKind.Breakable, hp = 2 });
            map.Set(4, 4, new Cell { kind = TileKind.Solid });

            Assert.IsFalse(map.Damage(3, 4));
            Assert.AreEqual(TileKind.Breakable, map.Get(3, 4).kind);
            Assert.AreEqual(1, map.Get(3, 4).hp);

            Assert.IsTrue(map.Damage(3, 4));
            Assert.AreEqual(TileKind.Empty, map.Get(3, 4).kind);
            Assert.IsFalse(map.Get(3, 4).ladder);

            Assert.IsFalse(map.Damage(4, 4));
            Assert.AreEqual(TileKind.Solid, map.Get(4, 4).kind);
        }

        [Test]
        public void HarpoonDestroysBreakableAboveInTwoShotsThenPassesThrough()
        {
            var map = StageMap.Flat();
            int c = StageMap.Col(0f);
            map.Set(c, 4, new Cell { kind = TileKind.Breakable, hp = 2 });
            map.Set(c, 9, new Cell { kind = TileKind.Solid });
            SetMap(map);

            var harpoon = Harpoon.Create(root.transform, (hitC, hitR) => map.Damage(hitC, hitR));
            float firstTop = FireToEnd(harpoon, new Vector2(0f, 1.67f));
            Assert.AreEqual(1, map.Get(c, 4).hp);
            Assert.LessOrEqual(firstTop, 4.001f);

            FireToEnd(harpoon, new Vector2(0f, 1.67f));
            Assert.AreEqual(TileKind.Empty, map.Get(c, 4).kind);

            float thirdTop = FireToEnd(harpoon, new Vector2(0f, 1.67f));
            Assert.Greater(thirdTop, 4.5f);
            Assert.LessOrEqual(thirdTop, 9.001f);
        }

        [Test]
        public void ChipColumnDamagesEveryBreakableOnceAndIgnoresSolid()
        {
            var map = StageMap.Flat();
            int shotColumn = StageMap.Col(0f);
            int chipColumn = shotColumn + 1;
            map.Set(chipColumn, 0, new Cell { kind = TileKind.Breakable, hp = 2 });
            map.Set(chipColumn, 1, new Cell { kind = TileKind.Solid });
            map.Set(chipColumn, 2, new Cell { kind = TileKind.Breakable, hp = 2 });
            map.Set(chipColumn, 3, new Cell { kind = TileKind.Breakable, hp = 2 });
            SetMap(map);

            var harpoon = Harpoon.Create(root.transform, (hitC, hitR) => map.Damage(hitC, hitR));
            FireToEnd(harpoon, new Vector2(0f, 1.67f), chipColumn);

            Assert.AreEqual(1, map.Get(chipColumn, 0).hp);
            Assert.AreEqual(TileKind.Solid, map.Get(chipColumn, 1).kind);
            Assert.AreEqual(1, map.Get(chipColumn, 2).hp);
            Assert.AreEqual(1, map.Get(chipColumn, 3).hp);
        }

        [Test]
        public void WickFallsSafelyWhenBreakableFootingIsDestroyed()
        {
            var map = StageMap.Flat();
            int c = StageMap.Col(0f);
            map.Set(c, 4, new Cell { kind = TileKind.Breakable, hp = 2 });
            SetMap(map);

            var wick = Wick.Create(root.transform, null);
            wick.ResetForStage(0f, 5f, 2);
            map.Damage(c, 4);
            map.Damage(c, 4);

            for (int i = 0; i < 60; i++) wick.Tick(0f, 0f, 1f / 60f, true);

            Assert.AreEqual(0f, wick.FeetY, 0.001f);
            Assert.AreEqual(2, wick.Hearts);
        }

        [Test]
        public void BreakableTuningUsesSpecifiedDefaults()
        {
            Assert.AreEqual(2, Tuning.I.breakableHp);
            Assert.AreEqual(0.03f, Tuning.I.breakableHeartChance, 0.0001f);
            Assert.AreEqual(8f, Tuning.I.heartPickupLife, 0.0001f);
        }

        [Test]
        public void PickupFallsToSurfaceCollectsAndHealsWick()
        {
            var map = StageMap.Flat();
            var wick = Wick.Create(root.transform, null);
            wick.ResetForStage(0f, 0f, 2);
            var pickup = Pickup.Create(root.transform, new Vector2(0f, 4.5f));

            for (int i = 0; i < 60 && pickup.Active; i++)
            {
                if (pickup.Tick(1f / 60f, map, wick.Hitbox))
                    wick.Heal();
            }

            Assert.IsFalse(pickup.Active);
            Assert.AreEqual(3, wick.Hearts);
        }

        [Test]
        public void PickupExpiresAfterConfiguredLifetime()
        {
            var pickup = Pickup.Create(root.transform, new Vector2(8f, 0f));
            var farAway = new Rect(-1f, 0f, 1f, 1f);

            for (int i = 0; i < Mathf.CeilToInt(Tuning.I.heartPickupLife * 60f) + 1; i++)
                pickup.Tick(1f / 60f, StageMap.Flat(), farAway);

            Assert.IsFalse(pickup.Active);
        }

        static float FireToEnd(Harpoon harpoon, Vector2 from, int chipColumn = -1)
        {
            harpoon.Fire(from, chipColumn);
            float highest = from.y;
            for (int i = 0; i < 180 && harpoon.Active; i++)
            {
                harpoon.Tick(1f / 60f);
                Transform tip = harpoon.transform.Find("Tip");
                highest = Mathf.Max(highest,
                    tip.position.y + (float)PlaceholderArt.HarpoonTipHeight / Tuning.I.pixelsPerUnit);
            }
            Assert.IsFalse(harpoon.Active, "Harpoon finished within three seconds.");
            return highest;
        }

        static void SetMap(StageMap map)
        {
            var property = typeof(Game).GetProperty("Map", BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(property);
            property.SetValue(null, map);
        }
    }
}
