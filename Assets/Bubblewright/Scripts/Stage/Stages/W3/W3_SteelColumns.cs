using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>World 3 difficulty 3. A crouch bunker with two firing slots, guarded by two steel Pogos at its mouths.</summary>
    /// <remarks>
    /// World / Difficulty / slots: W3, D3, MinStageInWorld 3, MaxStageInWorld 5.
    /// Concept: Wick shelters in a crouch bunker with two firing slots. Steel Pogos bounce like gates at both mouths, and
    ///   ceramic Sinkers wait in the courts beyond.
    /// Archetype(s): Crouch Duct Sniping (W1_Crawlway) + steel Pogos as timed gates.
    /// Structure: mirror-symmetric about x = 0. A 1-row tunnel under a B roof on row 1, columns 11-20, with firing slots
    ///   on 13 and 18. The roof is breakable so a Sinker that settles on it can always be shot down from inside.
    ///   Steel Pogos M on columns 8 and 23, just outside the mouths.
    /// Enemies and why each one is where it is: a Trigloop L over each slot (Wick's first targets from the bunker); a
    ///   ceramic Sinker L in each court past the gates. The steel Pogos are not required. No guest.
    /// Pops: 13 + 13 + 8 + 8 = 42 (D3 band 35-65); shortest hard clock for slots 3-5 in W3 = 72.45 s -> max 115 pops.
    /// Hitless plan for Moss with Heavy Boots and the default harpoon: start crouched in the tunnel at column 15. Shuffle
    ///   to a slot and fire up through it at the Trigloops as they cross; their M and S children bounce on the roof or
    ///   fall in the courts, and he fires through the slots again or chips a roof brick under a roller. Leave the
    ///   tunnel at column 10 while the steel Pogo on column 8 is high, walk under it into the court and pop the
    ///   ceramic Sinker (two hits) as it bounces; rolling children: fire in front, step back. Same on the right.
    /// Over-break check: every B is the tunnel roof. With all of it gone the floor is flat and open.
    /// </remarks>
    [Preserve]
    public sealed class W3_SteelColumns : StageSpec
    {
        public override string Id => "w3_steel_columns";
        public override string Name => "Steel Columns";
        public override int World => 3;
        public override int Difficulty => 3;
        public override int MinStageInWorld => 3;
        public override string Concept =>
            "Wick shelters in a crouch bunker with two firing slots. Steel Pogos bounce like gates at both mouths, and ceramic Sinkers wait beyond.";

        public override string[] Rows
        {
            get
            {
                var grid = StageSketch.Open();
                StageSketch.Box(grid, 11, 20, 1, 1, 'B');
                grid[1][13] = '.';
                grid[1][18] = '.';
                return StageSketch.Lines(grid);
            }
        }

        public override string Ground => SolidGround;

        protected override void Populate(StageBuilder b)
        {
            b.WickAtCol(15);
            b.BubbleAt(BubbleKind.Trigloop, StageBuilder.TierL, 13, 9f, -1);
            b.BubbleAt(BubbleKind.Trigloop, StageBuilder.TierL, 18, 9f, 1);
            b.BubbleAt(BubbleKind.Sinker, StageBuilder.TierL, 3, 9f, -1, ceramic: true);
            b.BubbleAt(BubbleKind.Sinker, StageBuilder.TierL, 28, 9f, 1, ceramic: true);
            b.SteelPogo(StageBuilder.TierM, StageMap.CenterX(8));
            b.SteelPogo(StageBuilder.TierM, StageMap.CenterX(23));
        }
    }
}
