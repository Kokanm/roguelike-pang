using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Bubblewright.Tests
{
    /// <summary>
    /// A map of two or three storeys. Enemies stay on the storey they were born on, and a shot stops at that storey's ceiling.
    /// </summary>
    public class MultiStoreyTests
    {
        const float Dt = 1f / 60f;
        const int Shaft = 16;

        GameObject root;
        StageMap previousMap;
        Rect previousBox;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("MultiStoreyTests");
            previousMap = Game.Map;
            previousBox = Game.WickBox;
            Game.WickBox = new Rect(100f, 100f, 1f, 1f);
        }

        [TearDown]
        public void TearDown()
        {
            SetMap(previousMap);
            Game.WickBox = previousBox;
            UnityEngine.Object.DestroyImmediate(root);
        }

        static void SetMap(StageMap map)
        {
            var property = typeof(Game).GetProperty("Map", BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(property, "Game.Map must exist.");
            property.SetValue(null, map);
        }

        static float ShaftX => StageMap.CenterX(Shaft);

        static string[] Blank(int count)
        {
            var rows = new string[count];
            for (int i = 0; i < count; i++) rows[i] = new string('.', StageMap.Cols);
            return rows;
        }

        /// <summary>Paints one cell. rows is top-first and holds only tile rows.</summary>
        static void Paint(string[] rows, int row, int col, char ch)
        {
            int index = rows.Length - 1 - row;
            var chars = rows[index].ToCharArray();
            chars[col] = ch;
            rows[index] = new string(chars);
        }

        static string[] TwoStoreys()
        {
            var rows = Blank(28);
            // Surfaces and a ladder that crosses the storey line (rows 12..15).
            Paint(rows, 12, 4, 'N');
            Paint(rows, 13, 4, 'I');
            Paint(rows, 14, 4, 'S');
            Paint(rows, 15, 4, 'T');
            Paint(rows, 0, 8, '#');
            Paint(rows, 2, 9, 'B');
            for (int row = 8; row <= 16; row++) Paint(rows, row, Shaft, 'H');
            return rows;
        }

        static string GroundRow()
        {
            var chars = new char[StageMap.Cols];
            for (int i = 0; i < chars.Length; i++) chars[i] = '=';
            chars[1] = 'N';
            chars[2] = 'I';
            chars[3] = 'S';
            chars[4] = '_';
            return new string(chars);
        }

        [Test]
        public void ParseBuildsTwoStoreyMapWithInlineSurfacesAndLaddersAcrossBoundary()
        {
            var map = StageLayout.Parse(TwoStoreys(), GroundRow(), Surface.Sand);
            SetMap(map);

            Assert.AreEqual(2, map.Storeys);
            Assert.AreEqual(28, map.Rows);
            Assert.AreEqual(28f, map.Height, 1e-5f);

            Assert.AreEqual(TileKind.Solid, map.Get(4, 12).kind);
            Assert.AreEqual(Surface.Normal, map.Get(4, 12).surface, "N stays normal when the default is sand");
            Assert.AreEqual(Surface.Ice, map.Get(4, 13).surface);
            Assert.AreEqual(Surface.Sand, map.Get(4, 14).surface);
            Assert.AreEqual(TileKind.Solid, map.Get(4, 15).kind);
            Assert.IsTrue(map.Get(4, 15).ladder, "T is a solid ladder");
            Assert.AreEqual(Surface.Sand, map.Get(4, 15).surface, "T uses the default surface");
            Assert.AreEqual(Surface.Sand, map.Get(8, 0).surface, "# uses the default surface");
            Assert.AreEqual(TileKind.Breakable, map.Get(9, 2).kind);
            Assert.AreEqual(Tuning.I.breakableHp, map.Get(9, 2).hp);

            for (int row = 12; row <= 15; row++)
                Assert.IsTrue(map.Get(Shaft, row).ladder, $"ladder crosses the boundary at row {row}");
            Assert.AreEqual(TileKind.Empty, map.Get(Shaft, 13).kind);
            Assert.AreEqual(TileKind.Empty, map.Get(Shaft, 14).kind);

            Assert.AreEqual(Surface.Sand, map.GetGround(0), "= uses the default surface");
            Assert.AreEqual(Surface.Normal, map.GetGround(1));
            Assert.AreEqual(Surface.Ice, map.GetGround(2));
            Assert.AreEqual(Surface.Sand, map.GetGround(3));
            Assert.IsTrue(map.IsGap(4));
            Assert.IsFalse(map.IsGap(0));

            var fx = Effects.Create(root.transform);
            var wick = Wick.Create(root.transform, fx);
            wick.ResetForStage(ShaftX, 10f, 3);
            float prev = wick.FeetY;
            bool crossed = false;
            for (int i = 0; i < 180 && wick.FeetY < 15f; i++)
            {
                wick.Tick(0f, 1f, false, Dt, true);
                float y = wick.FeetY;
                Assert.Greater(y, prev + 0.04f, $"frame {i}: climb stalled at y {prev}");
                if (prev < 14f && y >= 14f) crossed = true;
                prev = y;
            }
            Assert.IsTrue(crossed, "he climbs through y = 14");
            Assert.GreaterOrEqual(wick.FeetY, 15f);
            Assert.AreEqual(Mode.Climb, wick.Mode, "the ladder keeps going, so he is still climbing at y = 15");
        }

        [Test]
        public void WrongRowWidthAndWrongRowCountThrow()
        {
            var wide = Blank(28);
            wide[4] = new string('.', 31); // top-first index 4 is map row 23
            var width = Assert.Throws<ArgumentException>(() => StageLayout.Parse(wide));
            StringAssert.Contains("row 23", width.Message);

            var count = Assert.Throws<ArgumentException>(() => StageLayout.Parse(Blank(27)));
            StringAssert.Contains("row 26", count.Message);
        }

        [Test]
        public void HarpoonAndWeaponShotsClampToCurrentStoreyCeilingAndStopAtStoreyBoundary()
        {
            var rows = Blank(28);
            var map = StageLayout.Parse(rows);
            SetMap(map);

            float open = map.ShotCeiling(ShaftX, 2f, out int openRow);
            Assert.AreEqual(14f, open, 1e-4f, "an open shaft stops at the storey ceiling");
            Assert.GreaterOrEqual(openRow, map.Rows, "the storey edge is not a tile on the next storey");
            Assert.AreEqual(14f, map.StoreyCeiling(2f), 1e-4f);
            Assert.AreEqual(0f, map.StoreyBase(2f), 1e-4f);
            Assert.AreEqual(28f, map.ShotCeiling(ShaftX, 16f, out _), 1e-4f);
            Assert.AreEqual(14f, map.ShotFloor(ShaftX, 16f), 1e-4f, "a downward shot stops at the storey base");
            Assert.AreEqual(14f, map.StoreyBase(16f), 1e-4f);
            Assert.AreEqual(28f, map.StoreyCeiling(16f), 1e-4f);

            var upper = Bubble.Create(root.transform, BubbleKind.Gloop, 2, new Vector2(ShaftX, 16f), Vector2.zero, false);
            var bubbles = new List<Bubble> { upper };
            var world = new WeaponWorld(bubbles, new List<Roller>(), null, null, root.transform)
            {
                OnHitBubble = b => { if (!b.AbsorbHit()) b.Swell(Tuning.I.popSwell); },
            };

            var harpoon = Harpoon.Create(root.transform);
            harpoon.HoldTime = 0f;
            float top = RunUp(harpoon, new Vector2(ShaftX, 2f), upper);
            Assert.AreEqual(14f, top, 0.05f, "fired from storey 0");
            Assert.IsFalse(upper.Popped, "the cable cannot reach the bubble on storey 1");

            harpoon.HoldTime = 0f;
            float high = RunUp(harpoon, new Vector2(ShaftX, 16f), null);
            Assert.AreEqual(28f, high, 0.05f, "fired from storey 1");

            float shotTop = RunShot(world, new Vector2(ShaftX, 2f));
            Assert.LessOrEqual(shotTop, 14.001f, "a WeaponWorld shot from storey 0 stops at y = 14");
            Assert.IsFalse(upper.Popped);
            float shotHigh = RunShot(world, new Vector2(StageMap.CenterX(20), 16f));
            Assert.GreaterOrEqual(shotHigh, 27.5f, "a WeaponWorld shot from storey 1 reaches the top");
            Assert.LessOrEqual(shotHigh, 28.001f);

            int col = 8;
            map.Set(col, 13, new Cell { kind = TileKind.Breakable, hp = (byte)Tuning.I.breakableHp });
            float tileCeil = map.ShotCeiling(StageMap.CenterX(col), 12f, out int hitRow);
            Assert.AreEqual(13f, tileCeil, 1e-4f);
            Assert.AreEqual(13, hitRow, "the top row of the storey still counts");
            var tileHits = new List<Vector2Int>();
            var chipper = Harpoon.Create(root.transform, (c, r) =>
            {
                tileHits.Add(new Vector2Int(c, r));
                map.Damage(c, r);
            });
            chipper.HoldTime = 0f;
            chipper.Fire(new Vector2(StageMap.CenterX(col), 12f));
            for (int i = 0; i < 120 && chipper.Active; i++) chipper.Tick(Dt);
            Assert.AreEqual(Tuning.I.breakableHp - 1, map.Get(col, 13).hp, "the harpoon still damages row 13");

            int shotCol = 10;
            map.Set(shotCol, 13, new Cell { kind = TileKind.Breakable, hp = (byte)Tuning.I.breakableHp });
            world.OnHitTile = (c, r) => map.Damage(c, r);
            RunShot(world, new Vector2(StageMap.CenterX(shotCol), 12f));
            Assert.AreEqual(Tuning.I.breakableHp - 1, map.Get(shotCol, 13).hp, "a WeaponWorld shot still damages row 13");

            map.Set(Shaft, 13, new Cell { kind = TileKind.Breakable, hp = (byte)Tuning.I.breakableHp });
            var down = Harpoon.Create(root.transform, (c, r) => map.Damage(c, r));
            down.HoldTime = 1f;
            down.FireDown(new Vector2(ShaftX, 16f));
            for (int i = 0; i < 180 && down.CableRect.yMin > 14.05f; i++) down.Tick(Dt);
            Assert.AreEqual(14f, down.CableRect.yMin, 0.05f, "a downward shot stops at the storey base");
            Assert.AreEqual(Tuning.I.breakableHp, map.Get(Shaft, 13).hp, "it does not chip the storey below");

            world.Destroy();
        }

        static float RunUp(Harpoon harpoon, Vector2 from, Bubble avoid)
        {
            float top = from.y;
            harpoon.Fire(from);
            for (int i = 0; i < 180 && harpoon.Active; i++)
            {
                if (avoid != null) Assert.IsFalse(harpoon.Hits(avoid), "the cable reached the other storey");
                harpoon.Tick(Dt);
                var tip = harpoon.transform.Find("Tip");
                top = Mathf.Max(top, tip.position.y + (float)PlaceholderArt.HarpoonTipHeight / Tuning.I.pixelsPerUnit);
            }
            return top;
        }

        static float RunShot(WeaponWorld world, Vector2 from)
        {
            var shot = world.FireShot(from, Vector2.up * Tuning.I.weaponShotSpeed, 1f);
            float top = from.y;
            for (int i = 0; i < 180 && shot.Alive; i++)
            {
                world.TickShots(Dt);
                if (shot.Alive) top = Mathf.Max(top, shot.Position.y);
                else top = Mathf.Max(top, shot.Position.y);
            }
            return top;
        }

        [Test]
        public void NoEnemyLeavesItsHomeStorey()
        {
            var map = StageLayout.Parse(Blank(28));
            SetMap(map);

            var ghost = Bubble.Create(root.transform, BubbleKind.Ghost, 2, new Vector2(0f, 18f), Vector2.zero, false);
            var lowerGloop = Bubble.Create(root.transform, BubbleKind.Gloop, 0, new Vector2(-4f, 5f), Vector2.zero, false);
            var upperGloop = Bubble.Create(root.transform, BubbleKind.Gloop, 0, new Vector2(4f, 20f), Vector2.zero, false);
            var lowerDrift = Bubble.Create(root.transform, BubbleKind.Drifter, 1, new Vector2(-8f, 6f), Vector2.right, false);
            var upperDrift = Bubble.Create(root.transform, BubbleKind.Drifter, 1, new Vector2(8f, 20f), Vector2.left, false);
            var bubbles = new[] { ghost, lowerGloop, upperGloop, lowerDrift, upperDrift };

            Assert.AreEqual(1, ghost.HomeStorey);
            Assert.AreEqual(0, lowerGloop.HomeStorey);
            Assert.AreEqual(1, upperGloop.HomeStorey);

            var lowerRoll = Roller.Create(root.transform, 0, -2f, 0f, 1);
            var upperRoll = Roller.Create(root.transform, 0, 2f, 14f, -1);
            var rollers = new List<Roller> { lowerRoll, upperRoll };
            Assert.AreEqual(0, lowerRoll.HomeStorey);
            Assert.AreEqual(1, upperRoll.HomeStorey);

            var bornLow = Bubble.Create(root.transform, BubbleKind.Gloop, 1, new Vector2(0f, 3f), Vector2.zero, true);
            Assert.AreEqual(0, bornLow.HomeStorey, "a child takes its position's storey until it inherits");
            bornLow.InheritHomeStorey(upperGloop);
            Assert.AreEqual(upperGloop.HomeStorey, bornLow.HomeStorey);

            var kids = Roller.Split(upperRoll, root.transform);
            Assert.AreEqual(2, kids.Count);
            Assert.AreEqual(upperRoll.HomeStorey, kids[0].HomeStorey);
            Assert.AreEqual(upperRoll.HomeStorey, kids[1].HomeStorey);
            rollers.AddRange(kids);

            var all = new List<Bubble>(bubbles) { bornLow };
            for (int i = 0; i < 30 * 60; i++)
            {
                for (int b = 0; b < all.Count; b++)
                {
                    var bubble = all[b];
                    bubble.Tick(Dt);
                    AssertStorey(bubble.HomeStorey, bubble.Position.y, bubble.Radius, $"{bubble.Kind} tick {i}");
                }
                for (int r = 0; r < rollers.Count; r++)
                {
                    var roller = rollers[r];
                    roller.Tick(Dt, rollers);
                    AssertStorey(roller.HomeStorey, roller.Position.y, roller.Radius, $"roller tick {i}");
                }
            }
        }

        static void AssertStorey(int storey, float y, float radius, string where)
        {
            float line = 14f;
            if (storey <= 0)
                Assert.LessOrEqual(y + radius, line + 0.02f, where);
            else
                Assert.GreaterOrEqual(y - radius, line - 0.02f, where);
        }

        public static IEnumerable<TestCaseData> EveryWeapon()
        {
            foreach (var spec in WeaponSpec.All)
                yield return new TestCaseData(spec.Id).SetName("EveryWeaponStopsAtItsStoreyCeiling_" + spec.Id);
        }

        [TestCaseSource(nameof(EveryWeapon))]
        public void EveryWeaponStopsAtItsStoreyCeiling(string id)
        {
            var rows = Blank(28);
            var map = StageLayout.Parse(rows);
            int col = Shaft;
            map.Set(col, 14, new Cell { kind = TileKind.Breakable, hp = (byte)Tuning.I.breakableHp });
            map.Set(col, 20, new Cell { kind = TileKind.Breakable, hp = (byte)Tuning.I.breakableHp });
            SetMap(map);

            var upper = Bubble.Create(root.transform, BubbleKind.Gloop, 2, new Vector2(ShaftX, 16f), Vector2.zero, false);
            int armor = upper.Armor;
            var bubbles = new List<Bubble> { upper };
            var rollers = new List<Roller>();
            var tileHits = new List<Vector2Int>();
            var world = new WeaponWorld(bubbles, rollers, null, null, root.transform)
            {
                OnHitBubble = b => { if (!b.AbsorbHit()) b.Swell(Tuning.I.popSwell); },
                OnHitRoller = r => { if (!r.AbsorbHit()) r.Swell(Tuning.I.popSwell); },
                OnHitTile = (c, r) =>
                {
                    tileHits.Add(new Vector2Int(c, r));
                    map.Damage(c, r);
                },
            };

            var spec = WeaponSpec.ById(id);
            Assert.NotNull(spec, id);
            var run = new Run(1);
            Assert.IsTrue(run.GiveWeapon(spec));
            var behaviour = spec.CreateBehaviour();
            var harpoons = new List<Harpoon>();
            var ctx = new WeaponContext
            {
                CanFire = true,
                InAir = false,
                ShootDown = false,
                Muzzle = new Vector2(ShaftX, 2f),
                Up = Vector2.up,
                Spec = spec,
                World = world,
                Run = run,
                Rng = new System.Random(1),
            };
            var slot = spec.Slot;
            SetField(ctx, "spend", (Func<int, bool>)(n => run.SpendAmmo(slot, n)));
            SetField(ctx, "flash", (Action)(() => { }));
            SetField(ctx, "harpoons", (Func<int, IReadOnlyList<Harpoon>>)(n =>
            {
                while (harpoons.Count < n)
                    harpoons.Add(Harpoon.Create(root.transform, (c, r) =>
                    {
                        tileHits.Add(new Vector2Int(c, r));
                        map.Damage(c, r);
                    }));
                return harpoons;
            }));

            GasField gas = null;
            if (behaviour is GasFlask.Behaviour flask)
            {
                gas = new GasField(world, root.transform);
                flask.Field = gas;
            }

            bool head = spec.Slot == WeaponSlot.Harpoon;
            bool firedHead = false;
            for (int i = 0; i < 4 * 60; i++)
            {
                ctx.Pressed = true;
                ctx.Held = true;
                if (head)
                {
                    if (!firedHead && behaviour.VolleyReady(ctx) && behaviour.FireVolley(ctx))
                    {
                        run.SpendAmmo(WeaponSlot.Harpoon);
                        firedHead = true;
                    }
                }
                else behaviour.Tick(ctx, Dt);

                world.TickShots(Dt);
                gas?.Tick(Dt);
                for (int h = 0; h < harpoons.Count; h++)
                {
                    var cable = harpoons[h];
                    if (cable.Active) Assert.IsFalse(cable.Hits(upper), id + " cable hit the upper bubble");
                    cable.Tick(Dt);
                }
            }

            Assert.IsFalse(upper.Popped, id);
            Assert.AreEqual(armor, upper.Armor, id);
            Assert.AreEqual(Tuning.I.breakableHp, map.Get(col, 14).hp, id + " row 14");
            Assert.AreEqual(Tuning.I.breakableHp, map.Get(col, 20).hp, id + " row 20");
            foreach (var hit in tileHits)
                Assert.Less(hit.y, 14, id + " damaged row " + hit.y);

            behaviour.Dispose();
            gas?.Clear();
            world.Destroy();
        }

        static void SetField(object target, string field, object value)
        {
            var f = target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(f, field);
            f.SetValue(target, value);
        }
    }
}
