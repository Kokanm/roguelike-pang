using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>World 6 difficulty 3. A long one-row duct under a slotted grate; a wall on the grate splits the sky in two.</summary>
    /// <remarks>
    /// World / Difficulty / slots: W6, D3, MinStageInWorld 2, MaxStageInWorld 5.
    /// Concept: Scorches bounce on the grate and burn it, while Wick crawls underneath and snipes up through its slots.
    /// Archetype(s): Crouch Duct Sniping (W1_Crawlway). Structure: mirror-symmetric about x = 0.
    /// Enemies: an XL and an L Scorch on each half of the grate, each starting over a slot (columns 7, 11, 20, 24), so the
    ///   first shot is always there. Flames land on the grate, a row above the crawling Wick. Bubbles that bounce off the
    ///   grate's ends land on the open ground at columns 0-3 and 28-31, where Wick stands up.
    /// Pops: 15 + 7 + 15 + 7 = 44 (D3 band 35-65); shortest hard clock for slots 2-5 in W6 = 78 s -> max 124 pops.
    /// Hitless plan (Moss, Heavy Boots): from the left end, crouch into the duct and fire up through slots 7 and 11 as the
    ///   left pair crosses them; take any child that falls off the end from the open ground. Crawl under the dividing wall
    ///   to slots 20 and 24 for the right pair, and use the right end the same way. The duct is level: no step or gap.
    /// Over-break check: no B tiles.
    /// </remarks>
    [Preserve]
    public sealed class W6_CinderCrawl : StageSpec
    {
        public override string Id => "w6_cinder_crawl";
        public override string Name => "Cinder Crawl";
        public override int World => 6;
        public override int Difficulty => 3;
        public override int MinStageInWorld => 2;
        public override string Concept =>
            "Scorches burn the grate overhead while Wick crawls the duct under it and snipes up through four one-tile slots.";

        public override string[] Rows
        {
            get
            {
                // Grate on row 1 from column 4 to 27 with one-tile slots on 7, 11, 20 and 24: M and L never fall through.
                // The wall on columns 15-16 stands on the grate up to the ceiling, so the duct is the only way across.
                var grid = StageSketch.Open14();
                StageSketch.Box(grid, 4, 27, 1, 1, '#');
                grid[1][7] = '.';
                grid[1][11] = '.';
                grid[1][20] = '.';
                grid[1][24] = '.';
                StageSketch.Box(grid, 15, 16, 2, 13, '#');
                return StageSketch.Lines(grid);
            }
        }

        public override string Ground => SolidGround;

        protected override void Populate(StageBuilder b)
        {
            b.WickAtCol(1);
            b.BubbleAt(BubbleKind.Scorch, StageBuilder.TierXL, 7, 9f, 1);
            b.BubbleAt(BubbleKind.Scorch, StageBuilder.TierL, 11, 9f, 1);
            b.BubbleAt(BubbleKind.Scorch, StageBuilder.TierXL, 24, 9f, -1);
            b.BubbleAt(BubbleKind.Scorch, StageBuilder.TierL, 20, 9f, -1);
        }
    }
}
