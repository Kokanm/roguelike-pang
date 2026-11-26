using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>World 2 difficulty 1. The Clinger intro: patient Clingers under two boughs and on the canopy.</summary>
    /// <remarks>
    /// Stage Design Brief
    /// World / Difficulty / slots: W2, D1, MaxStageInWorld 1
    /// Concept: Two Clingers hang under the boughs and two from the canopy. Each waits for Wick to step beneath it:
    ///   stand under one and fire at once.
    /// Archetype(s): Pads (one enemy per spot), Clinger intro
    /// Structure: mirror-symmetric about x = 0. Boughs on row 7, columns 4-9 and 22-27, over a wide open floor.
    /// Enemies and why each one is where it is: an L Clinger under the middle of each bough (column 7 / 24), low enough
    ///   to reach in one quick shot; an M Clinger on the canopy at column 11 / 20, 4.5 u from Wick, so he learns the
    ///   1.5 u wake lane without being in it at the start. All patient, so nothing moves until he walks in.
    /// Pops: 7 + 7 + 3 + 3 = 20 (D1 band 10-25); hard clock for slot 1 in W2 = 51 s -> max 81 pops.
    /// Hitless plan for Moss with Heavy Boots and the default harpoon:
    ///   1. Start at x = 0; every Clinger is 4.5 u or more to the side, so none wakes.
    ///   2. Walk left until under the M at column 11 and fire as it wobbles (it hangs at 13.2, the harpoon is there in
    ///      about 0.6 s; its two S children fly off and stick; shoot each as it hangs).
    ///   3. Walk under the left bough to column 7 and fire up at the L (it hangs at 5.75, under 0.25 s away). Step out
    ///      from under the bough if a child drops, then shoot the children where they stick.
    ///   4. Mirror for the right side.
    /// Over-break check: no B tiles. Every Clinger starts over open floor in its own column.
    /// </remarks>
    [Preserve]
    public sealed class W2_CanopyAmbush : StageSpec
    {
        public override string Id => "w2_canopy_ambush";
        public override string Name => "Canopy Ambush";
        public override int World => 2;
        public override int Difficulty => 1;
        public override int MaxStageInWorld => 1;
        public override string Concept =>
            "Two Clingers hang under the boughs and two from the canopy. Each waits for Wick to step beneath it: stand under one and fire at once.";

        public override string[] Rows
        {
            get
            {
                // Two boughs on row 7. Their undersides are 7 u above the floor, so an L Clinger hangs low enough for a quick shot.
                var grid = StageSketch.Open();
                StageSketch.Box(grid, 4, 9, 7, 7, '#');
                StageSketch.Box(grid, 22, 27, 7, 7, '#');
                return StageSketch.Lines(grid);
            }
        }

        public override string Ground => SolidGround;

        protected override void Populate(StageBuilder b)
        {
            b.Wick(0f);
            // The first spawn is off centre, so RunStructureTests can tell this stage from its mirror.
            b.Clinger(StageBuilder.TierL, StageMap.CenterX(7), 5f, dir: 1);
            b.Clinger(StageBuilder.TierL, StageMap.CenterX(24), 5f, dir: -1);
            b.Clinger(StageBuilder.TierM, StageMap.CenterX(11), 12f, dir: -1);
            b.Clinger(StageBuilder.TierM, StageMap.CenterX(20), 12f, dir: 1);
        }
    }
}
