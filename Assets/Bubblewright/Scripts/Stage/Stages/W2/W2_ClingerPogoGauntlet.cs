using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>World 2 difficulty 2. Kokan's floor-Pogo puzzle: patient floor Clinger Pogos spaced along a long floor.</summary>
    /// <remarks>
    /// Stage Design Brief
    /// World / Difficulty / slots: W2, D2, MinStageInWorld 2, MaxStageInWorld 5
    /// Concept: Patient floor Clinger Pogos line the long floor. Hop each one and fire down in mid-air, or wake it,
    ///   step back and shoot it on the ceiling.
    /// Archetype(s): Floor Clinger Pogo Gauntlet
    /// Structure: mirror-symmetric about x = 0. Flat floor, nothing else: three Pogos each way, 5 u apart (S, M, S going
    ///   out from the centre), so the runs between them read as a rhythm.
    /// Enemies and why each one is where it is: S Pogos at columns 11 / 20 (4.5 u from Wick) and 1 / 30, M Pogos at
    ///   columns 6 / 25. The ones he meets first are S, the size he can hop with margin; the M sits where waking it and
    ///   stepping back is the easy answer. Two L Gloops start at columns 8 / 23 heading outward, to keep him moving.
    /// Pops: 7 + 7 + 1 + 1 + 3 + 3 + 1 + 1 = 24 (D2 band 20-40); shortest hard clock for slots 2-5 in W2 = 54 s -> max 86.
    /// Hitless plan for Moss with Heavy Boots and the default harpoon:
    ///   1. Start at x = 0: the nearest Pogo is 4.5 u away (an S wakes at 1.95 u), the Gloops are 7.5 u away going out.
    ///   2. Pop the L Gloops and their children from the middle as they come back; the floor between the inner S Pogos
    ///      is 9 u of safe standing room.
    ///   3. Walk toward the S at column 11 until it wobbles (about 2 u off), step back, and shoot it as it hangs from the
    ///      ceiling or as it drops. Or run, jump it (S is 0.9 u, the jump is 1.9 u) and fire down in mid-air.
    ///   4. The M at column 6 is 5 u further: wake it from 2.3 u, step back to 3 u and shoot it on the ceiling. Then the
    ///      outer S at column 1 the same way. Mirror on the right.
    /// Over-break check: no B tiles. Every enemy starts over open floor.
    /// </remarks>
    [Preserve]
    public sealed class W2_ClingerPogoGauntlet : StageSpec
    {
        public override string Id => "w2_clinger_pogo_gauntlet";
        public override string Name => "Clinger Pogo Gauntlet";
        public override int World => 2;
        public override int Difficulty => 2;
        public override int MinStageInWorld => 2;
        public override string Concept =>
            "Patient floor Clinger Pogos line the long floor. Hop each one and fire down in mid-air, or wake it, step back and shoot it on the ceiling.";

        public override string[] Rows => BlankRows();

        public override string Ground => SolidGround;

        protected override void Populate(StageBuilder b)
        {
            b.Wick(0f);
            b.BubbleAt(BubbleKind.Gloop, StageBuilder.TierL, 8, 9f, -1);
            b.BubbleAt(BubbleKind.Gloop, StageBuilder.TierL, 23, 9f, 1);
            // S, M, S going out from the centre, 5 u apart.
            b.FloorClinger(StageBuilder.TierS, StageMap.CenterX(11), dir: -1);
            b.FloorClinger(StageBuilder.TierS, StageMap.CenterX(20), dir: 1);
            b.FloorClinger(StageBuilder.TierM, StageMap.CenterX(6), dir: -1);
            b.FloorClinger(StageBuilder.TierM, StageMap.CenterX(25), dir: 1);
            b.FloorClinger(StageBuilder.TierS, StageMap.CenterX(1), dir: 1);
            b.FloorClinger(StageBuilder.TierS, StageMap.CenterX(30), dir: -1);
        }
    }
}
