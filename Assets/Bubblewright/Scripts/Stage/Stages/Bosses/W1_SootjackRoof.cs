using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>
    /// World 1 boss arena: Sootjack's roof. One storey of flat roof under one screen, a chimney stack at each wall and a
    /// high platform in the middle.
    /// </summary>
    /// <remarks>
    /// Brief (boss arena):
    /// Storeys: 1. No hatch, so no phase opens one.
    /// Pipes: top middle (phase 2 drops a Gloop there at every perch).
    /// What the boss breaks or adds: phase 2 crumbles the top row (2 tiles) of a chimney each time it jumps off it, so the
    ///   stacks drop from 4 tall to 3. The ladder tile beside each top never crumbles and the floor is never touched, so
    ///   Wick can always stand on the roof, and climb a ladder to a chimney top.
    /// Walk: phase 1 keeps its centre within 7 units of the middle (stilts clear of both chimneys), on the open roof under
    ///   the platform (stove top 6.4, platform underside 7).
    /// Perches (all lean out over open floor, grate faces down, nothing solid between the floor and the grate): left
    ///   chimney top, right chimney top, and the two ends of the high platform.
    /// Hitless route: phase 1, stand between the stilts and follow the grate while it glows, jump the waves at each stop;
    ///   phase 2, stand in the lit lane under the grate; phase 3, stand under the pot that puffed (the ring's gap points
    ///   straight down) and climb a chimney ladder when the screen edge glows, since the charge is 2 tall.
    /// </remarks>
    [Preserve]
    public sealed class W1_SootjackRoof : StageSpec
    {
        public override string Id => Sootjack.ArenaName;
        public override string Name => "Sootjack's Roof";
        public override int World => 1;
        public override int Difficulty => 4;
        public override bool IsBossArena => true;
        public override bool CanMirror => false;
        public override string Concept => "A flat roof with a chimney at each wall and a high platform in the middle, for the stove-on-stilts boss.";

        public override string[] Rows
        {
            get
            {
                // A chimney at each wall: two columns of brick four tall, with a ladder up the inner face to its top.
                // The high platform spans columns 12 to 19 with its top 8 up, over the middle of the roof.
                var grid = StageSketch.Open();
                StageSketch.Box(grid, 1, 2, 0, 3, '#');
                StageSketch.Box(grid, 3, 3, 0, 2, 'H');
                grid[3][3] = 'T';
                StageSketch.Box(grid, 29, 30, 0, 3, '#');
                StageSketch.Box(grid, 28, 28, 0, 2, 'H');
                grid[3][28] = 'T';
                StageSketch.Box(grid, 12, 19, 7, 7, '#');
                return StageSketch.Lines(grid);
            }
        }

        public override string Ground => SolidGround;

        protected override void Populate(StageBuilder b)
        {
            b.Wick(-9f);
        }
    }
}
