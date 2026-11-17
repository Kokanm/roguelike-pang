using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>World 2 difficulty 4. A slotted bunker under a roller-circled branch, with floor Clinger Pogos in both yards.</summary>
    /// <remarks>
    /// Stage Design Brief
    /// World / Difficulty / slots: W2, D4, MinStageInWorld 4, MaxStageInWorld 5
    /// Concept: Wick holds a slotted bramble bunker. Trigloops bounce on its roof under a roller-circled branch, big
    ///   Leads rake the yards, and floor Pogos guard both.
    /// Archetype(s): Slotted Bunker + Roller Orbit Loop + Floor Clinger Pogo Gauntlet (the hard-road finale of the forest)
    /// Structure: mirror-symmetric about x = 0. Bunker roof on row 4, columns 9-22, 1-tile slots on 11, 14, 17 and 20;
    ///   walls on 9 and 22 (rows 2-3) leave 2-row doors. A branch on row 9, columns 13-18, sits over the inner slots with
    ///   4 rows of air between it and the roof, so a roller can circle it.
    /// Enemies and why each one is where it is: L Trigloops over the outer slots (11 / 20), heading outward, so the first
    ///   shots are through a slot. A big roller circles the branch; on the underside (bottom 7.8 u) it crosses over the
    ///   inner slots, where Wick shoots it from inside. XL Leads start over the yards (columns 4 / 27), heading outward.
    ///   Floor Clinger Pogos: an S 4 u out from each door (column 5 / 26), an M in each corner (column 1 / 30).
    /// Pops: 13 + 13 + 15 + 15 + 3 + 1 + 1 + 3 + 3 = 67 (D4 band 55-90); shortest hard clock for slots 4-5 in W2 = 60 s
    ///   -> max 96.
    /// Hitless plan for Moss with Heavy Boots and the default harpoon:
    ///   1. Start at x = 0 under solid roof (the slots are on 14 and 17): nothing reaches him in the first seconds.
    ///   2. Step under slot 11 / 20 and fire as a Trigloop crosses; step back under solid roof between shots. M and S
    ///      children that come in through a door are shot inside.
    ///   3. Fire up through slot 14 / 17 when the roller is on the branch's underside. Its halves keep circling: same.
    ///   4. Shoot the XL Leads and their children through the outer slots or from a door as they cross the yard.
    ///   5. Step out of a door toward the S Pogo; it wakes at about 2 u: step back into the door and shoot it on the
    ///      ceiling or as it drops. The corner M the same way, from 3 u. Mirror for the other yard.
    /// Over-break check: no B tiles. Both doors stay open; every enemy starts over a slot or open yard floor.
    /// </remarks>
    [Preserve]
    public sealed class W2_BrambleFortress : StageSpec
    {
        public override string Id => "w2_bramble_fortress";
        public override string Name => "Bramble Fortress";
        public override int World => 2;
        public override int Difficulty => 4;
        public override int MinStageInWorld => 4;
        public override string Concept =>
            "Wick holds a slotted bramble bunker. Trigloops bounce on its roof under a roller-circled branch, big Leads rake the yards, and floor Pogos guard both.";

        public override string[] Rows
        {
            get
            {
                var grid = StageSketch.Open();
                StageSketch.Box(grid, 9, 22, 4, 4, '#');   // bunker roof
                grid[4][11] = '.';                          // firing slots
                grid[4][14] = '.';
                grid[4][17] = '.';
                grid[4][20] = '.';
                StageSketch.Box(grid, 9, 9, 2, 3, '#');     // walls over 2-row doors
                StageSketch.Box(grid, 22, 22, 2, 3, '#');
                StageSketch.Box(grid, 13, 18, 9, 9, '#');   // the branch the roller circles
                return StageSketch.Lines(grid);
            }
        }

        public override string Ground => SolidGround;

        protected override void Populate(StageBuilder b)
        {
            b.Wick(0f);
            b.BubbleAt(BubbleKind.Trigloop, StageBuilder.TierL, 11, 7f, -1);
            b.BubbleAt(BubbleKind.Trigloop, StageBuilder.TierL, 20, 7f, 1);
            b.BubbleAt(BubbleKind.Lead, StageBuilder.TierXL, 4, 9f, -1);
            b.BubbleAt(BubbleKind.Lead, StageBuilder.TierXL, 27, 9f, 1);
            b.Roller(0f, 10f, 1);
            b.FloorClinger(StageBuilder.TierS, StageMap.CenterX(5), dir: -1);
            b.FloorClinger(StageBuilder.TierS, StageMap.CenterX(26), dir: 1);
            b.FloorClinger(StageBuilder.TierM, StageMap.CenterX(1), dir: 1);
            b.FloorClinger(StageBuilder.TierM, StageMap.CenterX(30), dir: -1);
        }
    }
}
