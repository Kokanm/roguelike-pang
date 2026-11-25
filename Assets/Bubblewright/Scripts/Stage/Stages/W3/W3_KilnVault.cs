using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>World 3 difficulty 2. A brick kiln over Wick's corridor holds two ceramic Gloops; he chips his own firing slots.</summary>
    /// <remarks>
    /// World / Difficulty / slots: W3, D2, MinStageInWorld 2, MaxStageInWorld 5.
    /// Concept: a kiln with a breakable brick floor hangs over the corridor; Wick chips open whichever firing slots he wants.
    /// Archetype(s): Breakable Vault + Slotted Bunker (the slots are made by the player).
    /// Structure: mirror-symmetric about x = 0. Kiln walls on columns 9 and 22 from row 5 to the ceiling, a B floor on
    ///   row 5 from 10 to 21. Under it a 5-row corridor joins the two side courts.
    /// Enemies and why each one is where it is: two ceramic Gloop L sealed in the kiln (the puzzle). One Sinker L in
    ///   each side court, heading for its wall, to keep Wick moving while he works on the kiln. The kiln walls reach
    ///   the ceiling, so a Sinker can never settle on a roof Wick cannot shoot. No guest.
    /// Pops: 8 + 8 + 7 + 7 = 30 (D2 band 20-40); shortest hard clock for slots 2-5 in W3 = 69 s -> max 110 pops.
    /// Hitless plan for Moss with Heavy Boots and the default harpoon: start under the kiln. Walk out to a court and pop
    ///   its Sinker while it bounces, then the other; finish rolling children by firing in front of them and stepping
    ///   back. Then stand under the kiln and shoot one brick twice to open a slot under a ceramic Gloop's path, and a
    ///   second or third slot if he wants them faster. The ceramic L and M never fit through a 1-tile slot, so he fires
    ///   up through his slots as they cross; S children trickle down slowly and are popped in the corridor.
    /// Over-break check: every B is the kiln floor. With all of them gone the kiln is an open room over the corridor,
    ///   the bubbles drop into the corridor, and nothing is stranded or sealed.
    /// </remarks>
    [Preserve]
    public sealed class W3_KilnVault : StageSpec
    {
        public override string Id => "w3_kiln_vault";
        public override string Name => "Kiln Vault";
        public override int World => 3;
        public override int Difficulty => 2;
        public override int MinStageInWorld => 2;
        public override string Concept =>
            "Two ceramic Gloops are sealed in a kiln with a brick floor. Shoot out any bricks you like to make firing slots.";

        public override string[] Rows
        {
            get
            {
                var grid = StageSketch.Open14();
                StageSketch.Box(grid, 9, 9, 5, 13, '#');
                StageSketch.Box(grid, 22, 22, 5, 13, '#');
                StageSketch.Box(grid, 10, 21, 5, 5, 'B');
                return StageSketch.Lines(grid);
            }
        }

        public override string Ground => SolidGround;

        protected override void Populate(StageBuilder b)
        {
            b.Wick(0f);
            b.BubbleAt(BubbleKind.Gloop, StageBuilder.TierL, 13, 9f, -1, ceramic: true);
            b.BubbleAt(BubbleKind.Gloop, StageBuilder.TierL, 18, 9f, 1, ceramic: true);
            b.BubbleAt(BubbleKind.Sinker, StageBuilder.TierL, 4, 9f, -1);
            b.BubbleAt(BubbleKind.Sinker, StageBuilder.TierL, 27, 9f, 1);
        }
    }
}
