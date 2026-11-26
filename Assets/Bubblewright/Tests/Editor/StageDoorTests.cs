using NUnit.Framework;
using UnityEngine;

namespace Bubblewright.Tests
{
    /// <summary>
    /// Leaving a cleared stage through a door: how many doors each stage has, where the exit door stands, and the
    /// shut, opening and open door art.
    /// </summary>
    public class StageDoorTests
    {
        static bool HasColour(PixelCanvas c, Color32 col)
        {
            for (int y = 0; y < c.Height; y++)
                for (int x = 0; x < c.Width; x++)
                {
                    var p = c.Get(x, y);
                    if (p.r == col.r && p.g == col.g && p.b == col.b && p.a > 0) return true;
                }
            return false;
        }

        static bool HasColourInOpening(PixelCanvas c, Color32 col)
        {
            for (int y = 0; y < c.Height; y++)
                for (int x = 0; x < c.Width; x++)
                {
                    if (!DoorArt.InOpening(x, y)) continue;
                    var p = c.Get(x, y);
                    if (p.r == col.r && p.g == col.g && p.b == col.b && p.a > 0) return true;
                }
            return false;
        }

        static bool Colour(Color32 p) =>
            p.a > 0 && (p.r != Palette.Ink.r || p.g != Palette.Ink.g || p.b != Palette.Ink.b);

        static void FillRows(StageMap map, int col, int fromRow, int toRow)
        {
            for (int r = fromRow; r <= toRow; r++) map.Set(col, r, new Cell { kind = TileKind.Solid });
        }

        [Test]
        public void OrdinaryStagesHaveOneDoorBossesTwoAndTheLastBossNone()
        {
            for (int stage = 1; stage <= Tuning.I.RunLength; stage++)
            {
                int expected = stage == Tuning.I.RunLength ? 0 : Run.IsBossStageNumber(stage) ? 2 : 1;
                Assert.AreEqual(expected, Run.DoorCount(stage), $"stage {stage}");
                Assert.AreEqual(Run.PathFollows(stage), Run.DoorCount(stage) == 2, $"stage {stage}: road doors only where the road choice is");
            }
        }

        [Test]
        public void OnAnOpenFloorTheExitDoorIsAtTheRightEdge()
        {
            var map = StageMap.Flat();
            var spot = StageValidator.ExitDoorSpot(1, map, new Vector2(-10f, 0f));
            Assert.AreEqual(StageMap.CenterX(StageMap.Cols - 1), spot.x, 0.001f);
            Assert.AreEqual(0f, spot.y, 0.001f);
        }

        [Test]
        public void AWallKeepsTheDoorOnHisSide()
        {
            // A full-height wall at column 20 of a one-storey stage: the door stands just left of it.
            var map = StageMap.Flat();
            FillRows(map, 20, 0, StageMap.StoreyRows - 1);
            var spot = StageValidator.ExitDoorSpot(1, map, new Vector2(StageMap.CenterX(4), 0f));
            Assert.AreEqual(StageMap.CenterX(19), spot.x, 0.001f);
            Assert.AreEqual(0f, spot.y, 0.001f);
        }

        [Test]
        public void TheExitDoorIsOnHisStorey()
        {
            var map = StageMap.Flat(2);
            for (int c = 0; c < StageMap.Cols; c++) map.Set(c, StageMap.StoreyRows - 1, new Cell { kind = TileKind.Solid });
            float upper = StageMap.StoreyRows; // the floor of storey 1, on top of the solid cap row
            var spot = StageValidator.ExitDoorSpot(1, map, new Vector2(0f, upper));
            Assert.AreEqual(1, map.StoreyOf(spot.y + 0.01f));
            Assert.AreEqual(upper, spot.y, 0.001f);
            Assert.AreEqual(StageMap.CenterX(StageMap.Cols - 1), spot.x, 0.001f);
        }

        [Test]
        public void ANarrowPitAgainstTheWallDoesNotTakeTheDoor()
        {
            // Lantern Court's right end: a block on columns 27 to 30 leaves a one-column pit at column 31. The wall holds
            // Wick 0.77 u in, so he cannot get down into it: the door stands on the block instead.
            var map = StageMap.Flat();
            for (int c = 27; c <= 30; c++) map.Set(c, 0, new Cell { kind = TileKind.Solid });
            Assert.IsFalse(StageValidator.WickFitsAt(map, StageMap.Cols - 1, 0f), "the pit is too narrow for him");
            var spot = StageValidator.ExitDoorSpot(1, map, new Vector2(0f, 0f));
            Assert.AreEqual(StageMap.CenterX(30), spot.x, 0.001f);
            Assert.AreEqual(1f, spot.y, 0.001f);
        }

        [Test]
        public void EveryStageHasAnExitDoorWickCanWalkInto()
        {
            var t = Tuning.I;
            foreach (var spec in StageSpec.All)
                foreach (bool mirror in new[] { false, true })
                {
                    var plan = spec.Build(mirror);
                    var spot = StageValidator.ExitDoorSpot(spec.World, plan.map, plan.wickPosition);
                    string name = $"{spec.Name}{(mirror ? " (mirrored)" : "")}";
                    int col = StageMap.Col(spot.x);
                    Assert.IsTrue(StageValidator.WickFitsAt(plan.map, col, spot.y), $"{name}: Wick cannot stand at the door (column {col}, feet {spot.y})");
                    float doorX = Game.ClampDoorX(spot.x);
                    float standX = StageValidator.WickStandX(col);
                    Assert.LessOrEqual(Mathf.Abs(standX - doorX), t.hitbox.x * 0.5f + t.doorEnterHalfWidth,
                        $"{name}: standing at the door's column does not put him in the doorway");
                }
        }

        [Test]
        public void ADoorAtTheEdgeStaysOnTheScreen()
        {
            float half = DoorArt.Width * 0.5f / Tuning.I.pixelsPerUnit;
            float right = Game.ClampDoorX(StageMap.CenterX(StageMap.Cols - 1));
            float left = Game.ClampDoorX(StageMap.CenterX(0));
            Assert.LessOrEqual(right + half, StageMap.Cols * 0.5f + 0.001f);
            Assert.GreaterOrEqual(left - half, -StageMap.Cols * 0.5f - 0.001f);
            Assert.AreEqual(3f, Game.ClampDoorX(3f), 0.001f, "a door away from the edge does not move");
        }

        [Test]
        public void AShutDoorHidesItsOpeningAndAnOpenOneShowsIt()
        {
            foreach (var theme in Themes.All)
            {
                var shut = DoorArt.Paint(theme, DoorMark.None, 0f);
                var open = DoorArt.Paint(theme, DoorMark.None, 1f);
                // Check inside the opening rather than the whole canvas: Rooftops uses Palette.Ink for skyTop,
                // which also appears on the outer 1 px outline.
                Assert.IsFalse(HasColourInOpening(shut, theme.skyTop), $"{theme.name}: the leaves cover the dark opening");
                Assert.IsTrue(HasColourInOpening(shut, Palette.Steel), $"{theme.name}: ring handles");
                Assert.IsTrue(HasColourInOpening(open, theme.skyTop), $"{theme.name}: open door shows the opening");
            }
        }

        [Test]
        public void AnUnmarkedDoorGivesNothingAway()
        {
            var theme = Themes.ForWorld(2);
            for (int f = 0; f < DoorArt.OpenFrames; f++)
            {
                var c = DoorArt.Paint(theme, DoorMark.None, f / (float)(DoorArt.OpenFrames - 1));
                Assert.IsFalse(HasColour(c, Palette.Lemon), $"frame {f}: no warning");
                Assert.IsFalse(HasColour(c, Palette.Cream), $"frame {f}: no lantern");
            }
        }

        [Test]
        public void EveryDoorFrameIsThirtyByFortyFiveAndOutlinedInInk()
        {
            foreach (var theme in Themes.All)
                foreach (DoorMark mark in System.Enum.GetValues(typeof(DoorMark)))
                    for (int f = 0; f < DoorArt.OpenFrames; f++)
                    {
                        var sprite = PlaceholderArt.I.DoorFrame(theme, mark, f);
                        Assert.AreEqual(DoorArt.Width, sprite.texture.width);
                        Assert.AreEqual(DoorArt.Height, sprite.texture.height);
                        var canvas = DoorArt.Paint(theme, mark, f / (float)(DoorArt.OpenFrames - 1));
                        for (int y = 0; y < canvas.Height; y++)
                            for (int x = 0; x < canvas.Width; x++)
                            {
                                if (canvas.Filled(x, y)) continue;
                                bool touchesColour = Colour(canvas.Get(x - 1, y)) || Colour(canvas.Get(x + 1, y))
                                    || Colour(canvas.Get(x, y - 1)) || Colour(canvas.Get(x, y + 1));
                                Assert.IsFalse(touchesColour, $"{theme.name} {mark} frame {f} has a bare edge at {x},{y}");
                            }
                    }
        }
    }
}
