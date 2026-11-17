using System;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// The number tables of the level-design skill, written from Tuning, the hero table, the debuffs and StageValidator,
    /// so the skill never quotes stale numbers. Bubblewright > Write Level-Design Reference puts them between the markers
    /// in .claude/skills/bubblewright-level-design/references/mechanics-and-archetypes.md, and
    /// LevelDesignReferenceTests fails when that file no longer matches.
    /// </summary>
    public static class LevelDesignReference
    {
        public const string RelativePath = ".claude/skills/bubblewright-level-design/references/mechanics-and-archetypes.md";
        public const string Begin = "<!-- BEGIN GENERATED: Bubblewright > Write Level-Design Reference. Do not edit by hand. -->";
        public const string End = "<!-- END GENERATED -->";

        /// <summary>The reference file under the project root.</summary>
        public static string FullPath => System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", RelativePath));

        /// <summary>file with the text between Begin and End replaced by Tables(). Null when a marker is missing.</summary>
        public static string Splice(string file)
        {
            if (file == null) return null;
            file = file.Replace("\r\n", "\n");
            int a = file.IndexOf(Begin, StringComparison.Ordinal);
            int b = file.IndexOf(End, StringComparison.Ordinal);
            if (a < 0 || b < a) return null;
            return file.Substring(0, a + Begin.Length) + "\n" + Tables() + file.Substring(b);
        }

        public static string Tables()
        {
            var t = Tuning.I;
            var sb = new StringBuilder();
            void Line(string s = "") => sb.Append(s).Append('\n');

            Line("_Written from `Tuning`, `Character.All`, `Debuff.All` and `StageValidator`. One unit is one tile: one column, one row, 15 px._");
            Line();

            Line("### Grid");
            Line();
            Line("| What | Value |");
            Line("|---|---|");
            Line($"| Columns | {StageMap.Cols} (`0..{StageMap.Cols - 1}`); column `c` is centred at `x = c - {F(t.HalfWidth - 0.5f)}`, so `x = 0` is the line between columns {StageMap.Cols / 2 - 1} and {StageMap.Cols / 2} |");
            Line($"| Rows per storey | {StageMap.StoreyRows}; row `0` stands on the ground, `Rows` lists the top row first |");
            Line($"| Storeys | 1 to 3: rows `0..{StageMap.StoreyRows - 1}`, `{StageMap.StoreyRows}..{StageMap.StoreyRows * 2 - 1}`, `{StageMap.StoreyRows * 2}..{StageMap.StoreyRows * 3 - 1}` |");
            Line();

            float stand = t.hitbox.y;
            float crouch = t.hitbox.y * t.crouchHeightScale;
            float air = 2f * t.jumpTimeToApex;
            float tipStand = t.launcherTip.y;
            float tipCrouch = Mathf.Min(t.launcherTip.y * t.crouchSquash.y, crouch - t.Pixel);
            Line("### Wick's body and moves (every hero)");
            Line();
            Line("| What | Value |");
            Line("|---|---|");
            Line($"| Standing height | {F(stand)} u: walking needs {Mathf.CeilToInt(stand)} clear rows |");
            Line($"| Crouched height | {F(crouch)} u: crouch-walking needs {Mathf.CeilToInt(crouch)} clear row |");
            Line($"| Width | {F(t.hitbox.x)} u |");
            Line($"| Jump height | {F(t.jumpHeight)} u: a standing jump climbs a step of {Mathf.FloorToInt(t.jumpHeight)} row, never {Mathf.FloorToInt(t.jumpHeight) + 1} |");
            Line($"| Jump air time on flat ground | {F(air)} s |");
            Line($"| Crouch walk | {F(t.crouchSpeedScale * 100f)}% of run speed |");
            Line($"| Ladder climb | {F(t.climbSpeed)} u/s |");
            Line($"| Wall slide | {F(t.wallSlideSpeed)} u/s; a wall jump pushes {F(t.wallJumpPush)} u off the wall |");
            Line($"| Harpoon launcher height | {F(tipStand)} u standing, {F(tipCrouch)} u crouched (above his feet) |");
            Line($"| Breakable `B` tile | {t.breakableHp} harpoon hits |");
            Line();

            var boots = Debuff.ByName("Heavy Boots");
            var cable = Debuff.ByName("Frayed Cable");
            float bootsScale = boots != null ? boots.speedScale : 1f;
            float cableScale = cable != null ? cable.harpoonSpeedScale : 1f;
            Line("### Heroes");
            Line();
            Line($"Heavy Boots and Frayed Cable are hard-world debuffs. Sand multiplies walking by {F(t.sandSpeedScale)}. Jump reach is run speed times the flat air time. StageValidator walks every stage as Moss with Heavy Boots.");
            Line();
            Line("| Hero | Run u/s | Heavy Boots | On sand + Heavy Boots | Jump reach, Heavy Boots | Same on sand | Harpoon u/s | Frayed Cable | Cables |");
            Line("|---|---|---|---|---|---|---|---|---|");
            foreach (var c in Character.All)
            {
                float run = c.BaseRunSpeed;
                float slow = run * bootsScale;
                float sand = slow * t.sandSpeedScale;
                Line($"| {c.name} | {F(run)} | {F(slow)} | {F(sand)} | {F(slow * air)} u | {F(sand * air)} u | {F(c.BaseHarpoonSpeed)} | {F(c.BaseHarpoonSpeed * cableScale)} | {c.StartShots} |");
            }
            Line();

            Line("### Bubble tiers");
            Line();
            Line($"Peak is the top of the bubble above the floor it bounces on. Every bubble moves sideways at {F(t.bubbleSpeedX)} u/s (Lead {F(t.bubbleSpeedX * t.Kind(BubbleKind.Lead).speedScale)} u/s). A ceramic bubble takes {t.ceramicHits} hits and adds {t.ceramicHits - 1} pop.");
            Line();
            Line("| Tier | `StageBuilder` | Diameter | Peak | Lead peak | Bounce time | Lead bounce time | Pops, Gloop | Pops, Trigloop |");
            Line("|---|---|---|---|---|---|---|---|---|");
            string[] constants = { "TierXL", "TierL", "TierM", "TierS" };
            var lead = t.Kind(BubbleKind.Lead);
            for (int i = 0; i < t.tiers.Length; i++)
            {
                var tier = t.tiers[i];
                string constant = i < constants.Length ? constants[i] : i.ToString(CultureInfo.InvariantCulture);
                Line($"| {tier.name} | `{constant}` | {F(tier.diameter)} | {F(tier.peakHeight)} | {F(tier.peakHeight * lead.peakScale)} | {F(tier.bounceTime)} s | {F(tier.bounceTime * lead.bounceTimeScale)} s | {StageReport.PopsNeeded(BubbleKind.Gloop, i)} | {StageReport.PopsNeeded(BubbleKind.Trigloop, i)} |");
            }
            Line();

            Line("### Bubble kinds");
            Line();
            Line("| Kind | Children per pop | Largest tier | Peak x | Speed x |");
            Line("|---|---|---|---|---|");
            foreach (var k in t.kinds)
            {
                string largest = k.largestTier >= 0 && k.largestTier < t.tiers.Length ? t.tiers[k.largestTier].name : k.largestTier.ToString(CultureInfo.InvariantCulture);
                Line($"| {k.kind} | {k.children} | {largest} | {F(k.peakScale)} | {F(k.speedScale)} |");
            }
            Line();

            Line("### Rolling balls, enemies and hazards");
            Line();
            Line("| Field | Value | Meaning |");
            Line("|---|---|---|");
            Line($"| `rollerDiameters` | {F(t.rollerDiameters[0])} / {F(t.rollerDiameters[1])} u | big and small rolling ball; a big one is 3 pops (itself and two small) |");
            Line($"| `rollerSpeeds` | {F(t.rollerSpeeds[0])} / {F(t.rollerSpeeds[1])} u/s | big and small rolling ball |");
            Line($"| `clingerTriggerX` | {F(t.clingerTriggerX)} u | a stuck Clinger lets go when Wick is this close to its lane (plus its radius for a floor Pogo) |");
            Line($"| `clingerMaxStickTime` | {F(t.clingerMaxStickTime)} s | a Clinger that is not patient lets go by itself after this |");
            Line($"| `clingerWobbleTime` | {F(t.clingerWobbleTime)} s | warning wobble before a Clinger lets go |");
            Line($"| `clingerMinClearance` | {F(t.clingerMinClearance)} u | a Clinger only sticks under a platform when Wick can still walk under it |");
            Line($"| `sinkerKeepSand` / `sinkerKeep` | {F(t.sinkerKeepSand)} / {F(t.sinkerKeep)} | share of the last bounce a Sinker keeps on sand / elsewhere |");
            Line($"| `frostIceTime` | {F(t.frostIceTime)} s | a floor tile a Frost landed on stays ice this long |");
            Line($"| `ghostSolidTime` / `ghostFadedTime` | {F(t.ghostSolidTime)} / {F(t.ghostFadedTime)} s | a Ghost can be hit and hurt only while solid; it passes through tiles |");
            Line($"| `flameLife` | {F(t.flameLife)} s | a flame burns where a Scorch landed ({t.flameMax} at most) |");
            Line($"| `drifterSpeed` | {F(t.drifterSpeed)} u/s | a Drifter flies diagonals at this speed |");
            Line($"| `spikeDownTime` / `spikeWarnTime` / `spikeUpTime` | {F(t.spikeDownTime)} / {F(t.spikeWarnTime)} / {F(t.spikeUpTime)} s | spike strip cycle; each strip starts {F(t.spikePhaseStep)} s after the one before |");
            Line($"| `waveSafeDistance` | {F(t.waveSafeDistance)} u | a wave bubble never drops in closer than this to Wick, sideways |");
            Line($"| `ceramicMaxTier` | {t.tiers[Mathf.Clamp(t.ceramicMaxTier, 0, t.tiers.Length - 1)].name} | the smallest ceramic tier: a ceramic bubble is {t.tiers[0].name} down to this, never smaller |");
            Line();

            Line("### Gaps and pace (StageValidator)");
            Line();
            Line("| Rule | Value |");
            Line("|---|---|");
            Line($"| Widest ground gap allowed | {StageValidator.MaxGapColumns} columns, {StageValidator.MaxSandGapColumns} in World 3 or next to sand |");
            var moss = Character.ById("moss");
            if (moss != null)
            {
                // The reach check jumps from column centre to column centre: a gap of n columns is n + 1 units across.
                float reach = moss.BaseRunSpeed * bootsScale * air + 0.2f;
                float sandReach = moss.BaseRunSpeed * bootsScale * t.sandSpeedScale * air + 0.2f;
                Line($"| Why | Moss with Heavy Boots jumps {F(reach)} u ({F(sandReach)} u on sand) column centre to column centre, so he crosses {Mathf.FloorToInt(reach) - 1} columns ({Mathf.FloorToInt(sandReach) - 1} on sand) |");
            }
            for (int d = 1; d <= 4; d++)
            {
                StageValidator.PopBand(d, out int min, out int max);
                Line($"| D{d} pop count | {min} to {max} |");
            }
            Line($"| Pop rate | at most {F(StageValidator.TouchPopsPerSecond)} pops per second of the shortest hard-road clock among the slots the stage can land in |");
            Line();

            Line("### Hard-road clock per slot (seconds, and the most pops that clock allows)");
            Line();
            Line("A stage's limit is the smallest one over `MinStageInWorld..MaxStageInWorld`. The calm-road clock is this divided by " + F(t.hardTimeScale) + ".");
            Line();
            Line("| World | Slot 1 | Slot 2 | Slot 3 | Slot 4 | Slot 5 |");
            Line("|---|---|---|---|---|---|");
            for (int w = 1; w <= t.worlds; w++)
            {
                var row = new StringBuilder();
                row.Append("| ").Append(w).Append(' ');
                for (int slot = 1; slot <= 5; slot++)
                {
                    int stage = (w - 1) * t.stagesPerWorld + slot;
                    float time = StagePicker.StageTime(stage, true);
                    int cap = Mathf.FloorToInt(time * StageValidator.TouchPopsPerSecond + 1e-3f);
                    row.Append("| ").Append(F(time)).Append(" s, ").Append(cap).Append(' ');
                }
                row.Append('|');
                Line(row.ToString());
            }
            return sb.ToString();
        }

        /// <summary>
        /// Two decimals, half away from zero, through whole thousandths first. A float product such as 9 * 0.85 * 0.7
        /// lands a hair either side of 5.355; rounding to thousandths first makes the result the same on every machine.
        /// </summary>
        static string F(float v)
        {
            long k = (long)Math.Round((double)v * 1000.0, MidpointRounding.AwayFromZero);
            long h = k >= 0 ? (k + 5) / 10 : -((-k + 5) / 10);
            return (h / 100m).ToString("0.##", CultureInfo.InvariantCulture);
        }
    }
}
