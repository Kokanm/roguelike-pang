using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>World 3 difficulty 3. A sand funnel over a low chamber, joined by a breakable plug in the neck.</summary>
    /// <remarks>
    /// World / Difficulty / slots: W3, D3, MinStageInWorld 2, MaxStageInWorld 5.
    /// Concept: Sinkers start on the high shelves of a sand funnel and lose height as they bounce inward toward a brick
    ///   plug in its neck. Break the plug to drop them to you, or climb up and meet them.
    /// Archetype(s): Breakable Vault (the plug) + Stepped terraces (the funnel), as an hourglass.
    /// Structure: mirror-symmetric about x = 0. A waist deck on row 5 splits a 5-row lower chamber from the funnel. The
    ///   funnel steps up in 5-wide shelves (feet 7 on columns 5-9, feet 8 on columns 0-4) from a 12-wide floor at feet 6.
    ///   The neck is a B plug on columns 14-17. Ladders on columns 10 and 21 (T on row 5) join the chamber to the funnel.
    ///   Every funnel surface is at least 4 wide and Wick can stand on it, so a rolling Sinker is never out of reach.
    /// Enemies and why each one is where it is: a Sinker L on each high shelf, heading inward (they bounce lower and
    ///   lower down the funnel). Two ceramic Sinker L at the bottom of the funnel, either side of the plug. A Trigloop L
    ///   in the lower chamber so the chamber is a fight of its own, heading away from Wick. No guest.
    /// Pops: 7 + 7 + 8 + 8 + 13 = 43 (D3 band 35-65); shortest hard clock for slots 2-5 in W3 = 69 s -> max 110 pops.
    /// Hitless plan for Moss with Heavy Boots and the default harpoon: start in the lower chamber at column 3. Pop the
    ///   Trigloop L; its family fits under the 5-row roof without touching a standing Wick. Then either shoot the plug
    ///   twice per brick from below and let the funnel's bubbles fall into the chamber (the plug is 4 wide, so L comes
    ///   through), or climb a ladder to the funnel floor and fight there: shoot the ceramic Sinkers as they cross, step
    ///   up the shelves to reach the high ones, and finish rollers by firing in front of them and stepping up a shelf.
    /// Over-break check: the only B tiles are the plug. With it gone the funnel floor has a 4-wide hole Wick can drop
    ///   through, and both ladders still join the two halves, so nothing is stranded and no spot is a pit.
    /// </remarks>
    [Preserve]
    public sealed class W3_Hourglass : StageSpec
    {
        public override string Id => "w3_hourglass";
        public override string Name => "Hourglass";
        public override int World => 3;
        public override int Difficulty => 3;
        public override int MinStageInWorld => 2;
        public override string Concept =>
            "Sinkers bounce down a sand funnel toward a brick plug in its neck. Break the plug to drop them to you, or climb up to meet them.";

        public override string[] Rows
        {
            get
            {
                var grid = StageSketch.Open();
                // Waist deck with the plug and the two ladder tops.
                StageSketch.Box(grid, 0, 31, 5, 5, '#');
                StageSketch.Box(grid, 14, 17, 5, 5, 'B');
                StageSketch.Box(grid, 10, 10, 0, 4, 'H');
                StageSketch.Box(grid, 21, 21, 0, 4, 'H');
                grid[5][10] = 'T';
                grid[5][21] = 'T';
                // Funnel shelves.
                StageSketch.Box(grid, 0, 9, 6, 6, '#');
                StageSketch.Box(grid, 22, 31, 6, 6, '#');
                StageSketch.Box(grid, 0, 4, 7, 7, '#');
                StageSketch.Box(grid, 27, 31, 7, 7, '#');
                return StageSketch.Lines(grid);
            }
        }

        public override string Ground => SolidGround;

        protected override void Populate(StageBuilder b)
        {
            b.WickAtCol(3);
            b.BubbleAt(BubbleKind.Trigloop, StageBuilder.TierL, 20, 2.5f, 1);
            b.BubbleAt(BubbleKind.Sinker, StageBuilder.TierL, 2, 11f, 1);
            b.BubbleAt(BubbleKind.Sinker, StageBuilder.TierL, 29, 11f, -1);
            b.BubbleAt(BubbleKind.Sinker, StageBuilder.TierL, 12, 10f, -1, ceramic: true);
            b.BubbleAt(BubbleKind.Sinker, StageBuilder.TierL, 19, 10f, 1, ceramic: true);
        }
    }
}
