using NUnit.Framework;
using UnityEngine;

namespace Bubblewright.Tests
{
    /// <summary>
    /// A round bubble or rolling balloon is drawn a little below its collision circle so its ink outline visibly meets the
    /// floor. Only the sprite pivot moves; the hit circle is Radius and Position, which these tests do not touch.
    /// </summary>
    public class BubbleContactTests
    {
        [Test]
        public void SinkGrowsWithSizeAndIsAtLeastOnePixel()
        {
            var t = Tuning.I;
            int previous = int.MaxValue;
            for (int tier = 0; tier < t.tiers.Length; tier++)
            {
                int d = Mathf.RoundToInt(t.tiers[tier].diameter * t.pixelsPerUnit);
                int sink = PlaceholderArt.SinkPx(d);
                Assert.GreaterOrEqual(sink, 1, $"tier {tier}");
                Assert.LessOrEqual(sink, previous, "a smaller bubble never sinks further than a bigger one");
                Assert.Less(sink, d / 8 + 1, "it is a nudge, not a dive");
                previous = sink;
            }
        }

        [Test]
        public void BubbleSpritesPivotLowAndKeepInkAtTheBottomRow()
        {
            var art = PlaceholderArt.I;
            var t = Tuning.I;
            foreach (BubbleKind kind in System.Enum.GetValues(typeof(BubbleKind)))
                for (int tier = 0; tier < t.tiers.Length; tier++)
                {
                    var s = art.Bubbles[(int)kind, tier];
                    if (s == null) continue;
                    AssertSunk(s, $"{kind} tier {tier}");
                }
        }

        [Test]
        public void RollingBalloonsPivotLowAndKeepInkAtTheBottomRow()
        {
            for (int tier = 0; tier < Tuning.I.rollerDiameters.Length; tier++)
                for (int frame = 0; frame < Roller.Frames; frame++)
                    for (int look = 0; look < PlaceholderArt.RollerLooks; look++)
                        AssertSunk(PlaceholderArt.I.RollerFrame(tier, frame, look), $"roller tier {tier} frame {frame} look {look}");
        }

        static void AssertSunk(Sprite s, string what)
        {
            var tex = s.texture;
            int d = tex.height;
            Assert.AreEqual(d * 0.5f + PlaceholderArt.SinkPx(d), s.pivot.y, 0.01f, what + " pivot");
            bool ink = false;
            for (int x = 0; x < tex.width && !ink; x++) ink = tex.GetPixel(x, 0).a > 0f;
            Assert.IsTrue(ink, what + " has a painted pixel on its bottom row, so nothing is left floating below it");
        }
    }
}
