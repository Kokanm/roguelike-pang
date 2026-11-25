using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>World 2 difficulty 2. Two pearl rollers circle a long central platform while Leads bounce low.</summary>
    /// <remarks>
    /// Stage Design Brief
    /// World / Difficulty / slots: W2, D2, MinStageInWorld 2, MaxStageInWorld 5
    /// Concept: Two pearl rollers circle the long central platform, over and under it. Shoot them as they pass beneath
    ///   while Leads bounce low on both sides.
    /// Archetype(s): Roller Orbit Loop
    /// Structure: mirror-symmetric about x = 0. One platform on row 5, columns 10-21: 5 rows under it for Wick, 8 over it
    ///   for the rollers, so the loop has more than a roller's diameter all round.
    /// Enemies and why each one is where it is: two big rollers start on top of the platform at x = -4 and +4, both
    ///   rolling the same way round so they chase each other and never meet. Under the platform a roller's bottom is at
    ///   3.8 u, above Wick's head even in a jump (3.37 u), so the loop is a timing target, not a threat. The threat is
    ///   the Leads: an L at columns 3 / 28 and an M at columns 7 / 24, all heading outward.
    /// Pops: 3 + 3 + 7 + 7 + 3 + 3 = 26 (D2 band 20-40); shortest hard clock for slots 2-5 in W2 = 54 s -> max 86.
    /// Hitless plan for Moss with Heavy Boots and the default harpoon:
    ///   1. Start at x = 0 under the platform: the roof covers him while the Leads bounce outward and back.
    ///   2. Step out to the side and pop each Lead as it comes in low; retreat under the platform between shots (the
    ///      L Lead's peak is 7.7 u, so under the 5 u roof its bounces are shallow and easy to read).
    ///   3. Stand under the platform and fire up as a roller rolls along the underside; or stand one column outside an
    ///      end (column 9 / 22) and fire as it rolls down the end face. Each big roller splits into two small ones that
    ///      keep circling; shoot them the same way.
    /// Over-break check: no B tiles. Every Lead starts over open floor; the rollers ride the platform and come under it.
    /// </remarks>
    [Preserve]
    public sealed class W2_RollingTrack : StageSpec
    {
        public override string Id => "w2_rolling_track";
        public override string Name => "Rolling Track";
        public override int World => 2;
        public override int Difficulty => 2;
        public override int MinStageInWorld => 2;
        public override string Concept =>
            "Two pearl rollers circle the long central platform, over and under it. Shoot them as they pass beneath while Leads bounce low on both sides.";

        public override string[] Rows
        {
            get
            {
                var grid = StageSketch.Open14();
                StageSketch.Box(grid, 10, 21, 5, 5, '#');
                return StageSketch.Lines(grid);
            }
        }

        public override string Ground => SolidGround;

        protected override void Populate(StageBuilder b)
        {
            b.Wick(0f);
            b.BubbleAt(BubbleKind.Lead, StageBuilder.TierL, 3, 5f, -1);
            b.BubbleAt(BubbleKind.Lead, StageBuilder.TierL, 28, 5f, 1);
            b.BubbleAt(BubbleKind.Lead, StageBuilder.TierM, 7, 4f, -1);
            b.BubbleAt(BubbleKind.Lead, StageBuilder.TierM, 24, 4f, 1);
            // Both clockwise, half a loop apart, so they chase each other round the platform.
            b.Roller(-4f, 6f, 1);
            b.Roller(4f, 6f, 1);
        }
    }
}
