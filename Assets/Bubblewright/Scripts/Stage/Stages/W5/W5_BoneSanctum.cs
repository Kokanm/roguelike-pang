using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>World 5 difficulty 4. A sanctum with two Ghost Pogo pillars, a steel roller, Sinker guests and two waves.</summary>
    /// <remarks>
    /// World / Difficulty / slots: W5, D4, MinStageInWorld 4.
    /// Concept: two Ghost Pogos stand like pillars and a steel roller sweeps the floor; the Sinker guests settle and
    /// roll, and two waves follow.
    /// Archetype: open arena with rhythmic hazards (two hanging bone pillars frame the aisles). Guest: Sinker (World 3),
    /// named in Concept; its job is pacing: a Sinker loses height and rolls, blocking the floor until shot.
    /// Structure: mirror-symmetric about x = 0.
    /// Enemies: Ghost Pogo L at columns 9 and 22, bouncing in place from the floor to near the ceiling, hittable only
    /// while solid; Sinker L at columns 3 and 28; steel roller (not required, cannot be popped) on the floor at x -11.
    /// Waves: Ghost L pair, then Trigloop L pair, each called when few enemies are left.
    /// Pops: 7 + 7 + 7 + 7 + wave 7 + 7 + wave 13 + 13 = 68 (D4 band 55-90); slot 4-5 hard clock 78 s allows 124.
    /// Hitless plan: no platforms, so every enemy is over open floor. Jump the steel roller when it comes (it is 1.2 u
    /// tall). Shoot a Ghost Pogo from beside it while it is solid; stay a step away while it is faded. Let the Sinkers
    /// settle and shoot them low. Step away from the wave markers before they drop.
    /// Over-break: no B tiles.
    /// </remarks>
    [Preserve]
    public sealed class W5_BoneSanctum : StageSpec
    {
        public override string Id => "w5_bone_sanctum";
        public override string Name => "Bone Sanctum";
        public override int World => 5;
        public override int Difficulty => 4;
        public override int MinStageInWorld => 4;
        public override string Concept =>
            "Two Ghost Pogos stand like pillars and a steel roller sweeps the floor. Sinker guests settle and roll, then two waves follow.";

        public override string[] Rows
        {
            get
            {
                // Two bone pillars hang from the ceiling on columns 6 and 25, rows 8-13.
                var grid = StageSketch.Open14();
                StageSketch.Box(grid, 6, 6, 8, 13, '#');
                StageSketch.Box(grid, 25, 25, 8, 13, '#');
                return StageSketch.Lines(grid);
            }
        }

        public override string Ground => SolidGround;

        protected override void Populate(StageBuilder b)
        {
            b.Wick(0f);
            b.Bubble(BubbleKind.Ghost, StageBuilder.TierL, StageMap.CenterX(9), 1.3f, 1, pogo: true);
            b.Bubble(BubbleKind.Ghost, StageBuilder.TierL, StageMap.CenterX(22), 1.3f, 1, pogo: true);
            b.BubbleAt(BubbleKind.Sinker, StageBuilder.TierL, 3, 6f, -1);
            b.BubbleAt(BubbleKind.Sinker, StageBuilder.TierL, 28, 6f, 1);
            b.Roller(-11f, 0f, 1, 0, steel: true);

            b.Wave(0, BubbleKind.Ghost, StageBuilder.TierL, -10f, 1);
            b.Wave(0, BubbleKind.Ghost, StageBuilder.TierL, 10f, -1);
            b.Wave(1, BubbleKind.Trigloop, StageBuilder.TierL, -6f, -1);
            b.Wave(1, BubbleKind.Trigloop, StageBuilder.TierL, 6f, 1);
        }
    }
}
