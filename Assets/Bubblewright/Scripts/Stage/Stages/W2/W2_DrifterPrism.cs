using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>World 2 difficulty 3. Drifters ricochet off a stepped diamond; Wick shelters under a low slotted slab.</summary>
    /// <remarks>
    /// Stage Design Brief
    /// World / Difficulty / slots: W2, D3, MinStageInWorld 2, MaxStageInWorld 5
    /// Concept: Drifters ricochet off a stepped diamond in mid-air. Wick shelters crouched under a low slab and fires
    ///   up through its two slots, or steps out to chase.
    /// Archetype(s): Slotted Bunker (a crouch-height one) + Drifter deflector
    /// Structure: mirror-symmetric about x = 0. A diamond of # on rows 6-12 (2, 4, 6, 8, 6, 4, 2 wide, centred on x = 0)
    ///   turns every diagonal that meets it. Under it, a slab on row 1, columns 11-20, with 1-tile slots on 13 and 18:
    ///   one row under it, so Wick crouch-walks there and nothing but an S fits in.
    /// Enemies and why each one is where it is: L Drifters start high in the outer lanes (columns 4 / 27, y = 11) and dive
    ///   toward the diamond; M Drifters start low (columns 7 / 24, y = 4) and climb outward, so the four cross the lanes at
    ///   different heights. Two XL Gloops (columns 8 / 23) give slow, big targets between the fast ones.
    /// Pops: 7 + 7 + 3 + 3 + 15 + 15 = 50 (D3 band 35-65); shortest hard clock for slots 2-5 in W2 = 54 s -> max 86.
    /// Hitless plan for Moss with Heavy Boots and the default harpoon:
    ///   1. Start crouched under the slab at x = 0: the slab is a roof against every Drifter and Gloop that is not an S.
    ///   2. Crouch-walk under slot 13 or 18 and fire up as a Drifter or Gloop crosses it (the crouched launcher fires from
    ///      0.81 u, straight through the 1-tile slot). Big Gloops bounce on the slab and are easy to time.
    ///   3. When the air thins, crouch out of an end (column 10 / 21 stands up) and pop the rest in the open lanes,
    ///      ducking back under the slab when a Drifter dives. An S Drifter that comes under the slab is shot from the
    ///      crouch beside it.
    /// Over-break check: no B tiles. Every enemy starts over open floor in its own column.
    /// </remarks>
    [Preserve]
    public sealed class W2_DrifterPrism : StageSpec
    {
        public override string Id => "w2_drifter_prism";
        public override string Name => "Drifter Prism";
        public override int World => 2;
        public override int Difficulty => 3;
        public override int MinStageInWorld => 2;
        public override string Concept =>
            "Drifters ricochet off a stepped diamond in mid-air. Wick shelters crouched under a low slab and fires up through its two slots, or steps out to chase.";

        public override string[] Rows
        {
            get
            {
                var grid = StageSketch.Open14();
                // The diamond: half-width 1, 2, 3, 4, 3, 2, 1 on rows 6 to 12, centred on columns 15 and 16.
                for (int y = 6; y <= 12; y++)
                {
                    int half = y <= 9 ? y - 5 : 13 - y;
                    StageSketch.Box(grid, 16 - half, 15 + half, y, y, '#');
                }
                // The crouch slab with two firing slots.
                StageSketch.Box(grid, 11, 20, 1, 1, '#');
                grid[1][13] = '.';
                grid[1][18] = '.';
                return StageSketch.Lines(grid);
            }
        }

        public override string Ground => SolidGround;

        protected override void Populate(StageBuilder b)
        {
            b.Wick(0f);
            b.BubbleAt(BubbleKind.Drifter, StageBuilder.TierL, 4, 11f, 1);
            b.BubbleAt(BubbleKind.Drifter, StageBuilder.TierL, 27, 11f, -1);
            b.BubbleAt(BubbleKind.Drifter, StageBuilder.TierM, 7, 4f, -1);
            b.BubbleAt(BubbleKind.Drifter, StageBuilder.TierM, 24, 4f, 1);
            b.BubbleAt(BubbleKind.Gloop, StageBuilder.TierXL, 8, 10f, -1);
            b.BubbleAt(BubbleKind.Gloop, StageBuilder.TierXL, 23, 10f, 1);
        }
    }
}
