using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>World 5 difficulty 2. A crouch tunnel under twin vaults, with breakable plugs in its roof.</summary>
    /// <remarks>
    /// World / Difficulty / slots: W5, D2, MinStageInWorld 2.
    /// Concept: crawl the tunnel and shoot out its B plugs to fire up at the Trigloops; the Ghosts sink into the tunnel.
    /// Archetype: Crouch Duct Sniping. Structure: mirror-symmetric about x = 0.
    /// Enemies: Trigloop L over plugs 8 and 23, one per vault; Ghost M over plugs 11 and 20. The divider keeps each
    /// vault's Trigloop on its own side; the Ghosts ignore the divider and the tunnel roof alike.
    /// Pops: 13 + 13 + 3 + 3 = 32 (D2 band 20-40); shortest hard clock for slots 2-5 = 72 s, max 115.
    /// Hitless plan: from the left court, crouch into the tunnel at column 5. Under each plug, fire twice to open it,
    /// then fire up through the slot as the Trigloop crosses it. Shoot a Ghost as it sinks into the tunnel while solid,
    /// or step back out to a court. Children that roll off the deck ends land in the courts, where he stands to shoot.
    /// Over-break: broken plugs are one-tile slots; M and L never fall through, S drops slowly. Nothing strands.
    /// </remarks>
    [Preserve]
    public sealed class W5_CryptCrawl : StageSpec
    {
        public override string Id => "w5_crypt_crawl";
        public override string Name => "Crypt Crawl";
        public override int World => 5;
        public override int Difficulty => 2;
        public override int MinStageInWorld => 2;
        public override string Concept =>
            "Crawl the crypt tunnel and shoot out its plugs to fire up at the Trigloops. The tunnel roof stops them, not the Ghosts.";

        public override string[] Rows
        {
            get
            {
                // Tunnel roof on row 1 from column 5 to 26, breakable plugs at 8, 11, 20 and 23.
                // The divider on columns 15-16 rises to the ceiling and splits the deck into two vaults.
                var grid = StageSketch.Open();
                StageSketch.Box(grid, 5, 26, 1, 1, '#');
                grid[1][8] = 'B';
                grid[1][11] = 'B';
                grid[1][20] = 'B';
                grid[1][23] = 'B';
                StageSketch.Box(grid, 15, 16, 2, 13, '#');
                return StageSketch.Lines(grid);
            }
        }

        public override string Ground => SolidGround;

        protected override void Populate(StageBuilder b)
        {
            b.WickAtCol(2);
            b.BubbleAt(BubbleKind.Trigloop, StageBuilder.TierL, 8, 7f, 1);
            b.BubbleAt(BubbleKind.Trigloop, StageBuilder.TierL, 23, 7f, -1);
            b.BubbleAt(BubbleKind.Ghost, StageBuilder.TierM, 11, 9f, 1);
            b.BubbleAt(BubbleKind.Ghost, StageBuilder.TierM, 20, 9f, -1);
        }
    }
}
