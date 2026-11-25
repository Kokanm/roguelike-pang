using System;
using System.Collections.Generic;
using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// Checks one stage, and its mirror, against the unlock rules and a slow clear: Moss with Heavy Boots,
    /// sand speed in the desert, and no upgrades. An empty list means the stage is valid.
    /// </summary>
    public static class StageValidator
    {
        const int FairTicks = 72;
        const float FairDt = 1f / 60f;
        /// <summary>Fastest pop rate a stage may ask for on touch controls: pops over the shortest hard-road clock.</summary>
        public const float TouchPopsPerSecond = 1.6f;
        /// <summary>
        /// Widest ground gap, in columns: what Moss with Heavy Boots jumps from column centre to column centre (3.77 u,
        /// 2.7 u on sand). Kokan set the cap on 2026-11-17: 2 columns, 1 in World 3 or next to sand.
        /// </summary>
        public const int MaxGapColumns = 2, MaxSandGapColumns = 1;
        const float FootInset = 0.02f;

        struct Node
        {
            public int Col;
            public float Feet;
            public bool CanJump;
            public int Storey;
        }

        public static List<string> Validate(StageSpec spec, bool mirror)
        {
            var errors = new List<string>();
            if (spec == null)
            {
                errors.Add("Stage is missing.");
                return errors;
            }

            CheckMeta(spec, errors);
            if (!RowsArePlayable(spec, errors)) return errors;

            StagePlan plan;
            try { plan = spec.Build(mirror); }
            catch (Exception e)
            {
                errors.Add("Stage did not build: " + e.Message);
                return errors;
            }
            if (plan == null || plan.map == null)
            {
                errors.Add("Stage built no map.");
                return errors;
            }

            CheckUnlocks(spec, plan, errors);
            CheckClearance(spec, plan, errors);
            CheckGaps(spec, plan, errors);
            CheckLadders(plan, errors);

            var intact = Reach(spec.World, plan, EmptyBroken());
            var opened = Breach(spec.World, plan);
            var allBroken = Reach(spec.World, plan, EveryBreakable(plan.map));
            CheckSolvable(plan, intact, opened, allBroken, errors);
            CheckHatches(plan, opened.nodes, opened.reach, errors);
            CheckPockets(plan, opened.broken, opened.nodes, opened.reach, errors);
            CheckPops(spec, plan, errors);
            CheckFairStart(plan, errors);
            return errors;
        }

        static void CheckMeta(StageSpec spec, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(spec.Id)) errors.Add("Id is empty.");
            if (string.IsNullOrWhiteSpace(spec.Name)) errors.Add("Name is empty.");
            if (string.IsNullOrWhiteSpace(spec.Concept)) errors.Add("Concept is empty.");
            if (spec.World < 1 || spec.World > 6) errors.Add($"World {spec.World} is outside 1 to 6.");
            if (spec.Difficulty < 1 || spec.Difficulty > 4) errors.Add($"Difficulty {spec.Difficulty} is outside 1 to 4.");
            if (spec.Storeys < 1 || spec.Storeys > 3) errors.Add($"Storeys {spec.Storeys} is outside 1 to 3.");
            if (spec.MinStageInWorld < 1 || spec.MaxStageInWorld > 5 || spec.MinStageInWorld > spec.MaxStageInWorld)
                errors.Add($"Stage slot bounds {spec.MinStageInWorld} to {spec.MaxStageInWorld} are not inside 1 to 5.");

            foreach (var other in StageSpec.All)
            {
                if (ReferenceEquals(other, spec)) continue;
                if (other.Id == spec.Id) errors.Add($"Id '{spec.Id}' is used by more than one stage.");
                if (other.Name == spec.Name) errors.Add($"Name '{spec.Name}' is used by more than one stage.");
            }
        }

        static bool RowsArePlayable(StageSpec spec, List<string> errors)
        {
            int want = StageMap.StoreyRows * spec.Storeys;
            if (spec.Storeys < 1 || spec.Storeys > 3) return false;
            // A legacy stage (14 rows per storey) is judged as the game plays it: padded to 18 rows.
            var tileRows = spec.TileRows;
            if (tileRows == null || tileRows.Length != want)
            {
                errors.Add($"Rows must be {want} lines of {StageMap.Cols} characters ({StageMap.StoreyRows} per storey, top row first).");
                return false;
            }
            bool ok = true;
            for (int i = 0; i < tileRows.Length; i++)
            {
                string line = tileRows[i];
                int row = tileRows.Length - 1 - i;
                if (line == null || line.Length != StageMap.Cols)
                {
                    errors.Add($"Row {row} must be {StageMap.Cols} characters.");
                    ok = false;
                    continue;
                }
                for (int c = 0; c < line.Length; c++)
                    if (".#NISBHT".IndexOf(line[c]) < 0)
                    {
                        errors.Add($"Unknown stage character '{line[c]}' at column {c}, row {row}.");
                        ok = false;
                    }
            }
            if (spec.Ground == null || spec.Ground.Length != StageMap.Cols)
            {
                errors.Add($"Ground must be {StageMap.Cols} characters.");
                return false;
            }
            for (int c = 0; c < spec.Ground.Length; c++)
                if ("=NIS_".IndexOf(spec.Ground[c]) < 0)
                {
                    errors.Add($"Unknown ground character '{spec.Ground[c]}' at column {c}.");
                    ok = false;
                }
            return ok;
        }

        static void CheckUnlocks(StageSpec spec, StagePlan plan, List<string> errors)
        {
            bool intro = spec.World == 1 && spec.MaxStageInWorld == 1;
            bool gapsAndBreakables = spec.World >= 2 || spec.MinStageInWorld >= 2;
            bool trigloopAndPogo = spec.World >= 2 || spec.MinStageInWorld >= 3;
            var guests = new HashSet<BubbleKind>();

            if (spec.Rows != null)
                foreach (var line in spec.TileRows)
                {
                    if (line == null) continue;
                    if (line.IndexOf('B') >= 0 && !gapsAndBreakables)
                        errors.Add("Stage 1-1 cannot use breakable tiles.");
                    if (line.IndexOf('I') >= 0 && spec.World != 4)
                        errors.Add("Ice is only allowed in World 4.");
                    if (line.IndexOf('S') >= 0 && spec.World != 3)
                        errors.Add("Sand is only allowed in World 3.");
                }
            if (spec.Ground != null)
            {
                if (spec.Ground.IndexOf('_') >= 0 && !gapsAndBreakables)
                    errors.Add("Stage 1-1 cannot use ground gaps.");
                if (spec.Ground.IndexOf('I') >= 0 && spec.World != 4)
                    errors.Add("Ice is only allowed in World 4.");
                if (spec.Ground.IndexOf('S') >= 0 && spec.World != 3)
                    errors.Add("Sand is only allowed in World 3.");
            }
            if (plan.map.spikes.Count > 0 && spec.World != 6)
                errors.Add("Spike strips are only allowed in World 6.");

            void One(SpawnSpec spawn, string where)
            {
                if (spawn.kind == BubbleKind.Lucky)
                    errors.Add($"{where} authors a Lucky. Lucky is placed at random and is never authored.");
                if (intro && spawn.kind != BubbleKind.Gloop)
                    errors.Add($"Stage 1-1 can only use Gloop, not {spawn.kind}.");
                if (intro && (spawn.pogo || spawn.ceramic || spawn.steel))
                    errors.Add("Stage 1-1 cannot use Pogos, ceramic or steel.");
                if (spawn.kind == BubbleKind.Trigloop && !trigloopAndPogo)
                    errors.Add("Trigloop is locked until stage 1-3.");
                if (spawn.pogo && !trigloopAndPogo)
                    errors.Add("Pogos are locked until stage 1-3.");
                if (spawn.kind == BubbleKind.Lead && spec.World < Tuning.I.leadFromWorld)
                    errors.Add("Lead is locked until World 2.");
                if (spawn.kind == BubbleKind.Drifter && (spec.World < 2 || spec.Difficulty == 1))
                    errors.Add("Drifter is locked until World 2, and a difficulty 1 stage does not use it.");
                int home = HomeWorld(spawn.kind);
                if (home > spec.World)
                    errors.Add($"{spawn.kind} is locked until World {home}.");
                if (home > 0 && home < spec.World) guests.Add(spawn.kind);
                if (spec.Difficulty == 1 && !ClassicOrSignature(spec.World, spawn.kind))
                    errors.Add($"Difficulty 1 sticks to the signature and classic kinds, not {spawn.kind}.");
                int largest = Tuning.I.Kind(spawn.kind).largestTier;
                if (spawn.tier < largest)
                    errors.Add($"{spawn.kind} is larger than L.");
                if (spawn.ceramic && spec.World < Tuning.I.ceramicFromWorld)
                    errors.Add("Ceramic is locked until World 3.");
                if (spawn.ceramic && spawn.tier > Tuning.I.ceramicMaxTier)
                    errors.Add("Ceramic bubbles stop at M.");
                if (spawn.steel && spec.World < Tuning.I.steelFromWorld)
                    errors.Add("Steel is locked until World 3.");
                if (spec.Difficulty == 1 && (spawn.ceramic || spawn.steel))
                    errors.Add("Difficulty 1 does not use ceramic or steel.");
            }

            if (plan.spawns != null)
                for (int i = 0; i < plan.spawns.Count; i++) One(plan.spawns[i], $"Spawn {i}");
            if (plan.waves != null)
                foreach (var wave in plan.waves)
                    foreach (var spawn in wave) One(spawn, "A wave");
            if (plan.steelPogos != null)
                foreach (var spawn in plan.steelPogos) One(spawn, "A steel Pogo");
            if (intro && plan.rollers != null && plan.rollers.Count > 0)
                errors.Add("Stage 1-1 cannot use rollers.");
            if (plan.rollers != null && plan.rollers.Count > 0 && spec.World < Tuning.I.rollerFromWorld)
                errors.Add("Rollers are locked until World 2.");
            if (spec.Difficulty == 1 && plan.rollers != null && plan.rollers.Count > 0)
                errors.Add("Difficulty 1 sticks to the signature and classic kinds, not a roller.");
            if (guests.Count > 1)
                errors.Add("A stage can have at most one guest kind.");
            if (spec.Difficulty == 1 && guests.Count > 0)
                errors.Add("Difficulty 1 does not bring a guest.");

            var counts = new int[plan.map.Storeys];
            CountEnemies(plan, counts);
            for (int s = 0; s < counts.Length; s++)
                if (counts[s] < 1) errors.Add($"Storey {s + 1} has no required enemy.");
        }

        static void CheckClearance(StageSpec spec, StagePlan plan, List<string> errors)
        {
            var map = plan.map;
            if (!Supported(map, plan.wickPosition, out string why))
                errors.Add("Wick does not start on solid footing. " + why);
            else if (map.OverlapsSolid(Body(plan.wickPosition, Standing(map, plan.wickPosition))))
                errors.Add("Wick starts inside a solid tile.");
            if (spec.World == 4 && plan.wickPosition.y <= 0.05f)
            {
                int c0 = StageMap.Col(plan.wickPosition.x - Tuning.I.hitbox.x * 0.5f + 0.001f);
                int c1 = StageMap.Col(plan.wickPosition.x + Tuning.I.hitbox.x * 0.5f - 0.001f);
                for (int c = c0; c <= c1; c++)
                    if (map.GetGround(c) == Surface.Ice)
                        errors.Add("Wick starts on ice. World 4 keeps normal ground under his feet.");
            }

            var bodies = new List<SpawnSpec>();
            if (plan.spawns != null) bodies.AddRange(plan.spawns);
            for (int i = 0; i < bodies.Count; i++)
            {
                var spawn = bodies[i];
                if (spawn.kind == BubbleKind.Clinger)
                {
                    if (!MountOk(map, spawn))
                        errors.Add($"Clinger {i} has no {spawn.mount} surface to stick to.");
                    continue;
                }
                if (spawn.kind == BubbleKind.Ghost) continue;
                float radius = Radius(spawn.tier);
                var p = spawn.position;
                var resolved = p;
                map.ResolveCircle(ref resolved, radius);
                if ((resolved - p).sqrMagnitude > 0.0001f)
                    errors.Add($"Bubble {i} ({spawn.kind}) starts inside a tile or a wall.");
                for (int j = i + 1; j < bodies.Count; j++)
                {
                    if (bodies[j].kind == BubbleKind.Clinger) continue;
                    float need = radius + Radius(bodies[j].tier);
                    if ((spawn.position - bodies[j].position).sqrMagnitude < need * need - 0.01f)
                        errors.Add($"Bubbles {i} and {j} start overlapping.");
                }
            }

            if (plan.rollers != null)
                foreach (var roller in plan.rollers)
                {
                    float radius = Roller.RadiusOf(roller.tier);
                    int col = StageMap.Col(roller.x);
                    if (roller.surfaceY <= 0.05f)
                    {
                        if (map.IsGap(col)) errors.Add($"Roller at x {roller.x:0.##} starts over a ground gap.");
                    }
                    else
                    {
                        int row = Mathf.FloorToInt(roller.surfaceY) - 1;
                        if (!map.IsSolid(col, row))
                            errors.Add($"Roller at x {roller.x:0.##} does not rest on a solid tile.");
                    }
                    if (Headroom(map, col, roller.surfaceY, EmptyBroken()) < radius * 2f - 0.05f)
                        errors.Add($"Roller at x {roller.x:0.##} does not have a diameter of headroom.");
                    foreach (var spike in map.spikes)
                        if (SameSurface(spike.row, roller.surfaceY) && col >= spike.col && col < spike.col + spike.width)
                            errors.Add($"Roller at x {roller.x:0.##} starts on a spike strip.");
                }

            for (int i = 0; i < map.spikes.Count; i++)
            {
                var spike = map.spikes[i];
                if (spike.width < 1 || spike.col < 0 || spike.col + spike.width > StageMap.Cols)
                {
                    errors.Add($"Spike strip {i} does not fit the arena.");
                    continue;
                }
                for (int c = spike.col; c < spike.col + spike.width; c++)
                {
                    if (NearLadder(map, c))
                        errors.Add($"Spike strip {i} sits on or next to a ladder.");
                    if (spike.row < 0)
                    {
                        if (map.IsGap(c) || map.IsSolid(c, 0))
                            errors.Add($"Spike strip {i} is not on open ground.");
                    }
                    else if (!map.IsSolid(c, spike.row) || map.IsSolid(c, spike.row + 1))
                        errors.Add($"Spike strip {i} is not on a platform top.");
                }
                for (int j = i + 1; j < map.spikes.Count; j++)
                {
                    var other = map.spikes[j];
                    if (other.row == spike.row && spike.col <= other.col + other.width && other.col <= spike.col + spike.width)
                        errors.Add($"Spike strips {i} and {j} touch.");
                }
            }

            if (plan.waves != null)
            {
                int storey = plan.waveStorey < 0 ? plan.map.Storeys - 1 : plan.waveStorey;
                if (storey < 0 || storey >= plan.map.Storeys)
                    errors.Add("Wave storey is outside the stage.");
                foreach (var wave in plan.waves)
                    foreach (var spawn in wave)
                        if (Mathf.Abs(spawn.position.x) + Radius(spawn.tier) > Tuning.I.HalfWidth + 0.01f)
                            errors.Add("A wave bubble does not fit in the arena.");
            }
        }

        static void CheckGaps(StageSpec spec, StagePlan plan, List<string> errors)
        {
            var map = plan.map;
            int run = 0;
            for (int c = 0; c <= StageMap.Cols; c++)
            {
                bool gap = c < StageMap.Cols && map.IsGap(c);
                if (gap) { run++; continue; }
                if (run == 0) continue;
                int start = c - run;
                bool sand = spec.World == 3
                    || (start > 0 && map.GetGround(start - 1) == Surface.Sand)
                    || (c < StageMap.Cols && map.GetGround(c) == Surface.Sand);
                int limit = sand ? MaxSandGapColumns : MaxGapColumns;
                if (run > limit)
                    errors.Add(sand
                        ? $"Ground gap of {run} columns is wider than sand allows ({limit})."
                        : $"Ground gap of {run} columns is wider than a jump allows ({limit}).");
                run = 0;
            }
        }

        static void CheckLadders(StagePlan plan, List<string> errors)
        {
            var map = plan.map;
            for (int c = 0; c < StageMap.Cols; c++)
            {
                int r = 0;
                while (r < map.Rows)
                {
                    if (!map.Get(c, r).ladder) { r++; continue; }
                    int start = r;
                    while (r < map.Rows && map.Get(c, r).ladder) r++;
                    int length = r - start;
                    bool hatch = length == 1 && map.IsStoreyCap(start);
                    if (length < 2 && !hatch)
                        errors.Add($"Ladder at column {c}, row {start} does not connect two levels.");
                }
            }
        }

        static void CheckSolvable(StagePlan plan, ReachSet intact, ReachSet opened, ReachSet allBroken, List<string> errors)
        {
            if (intact.start < 0 || intact.nodes.Count == 0 || !intact.reach[intact.start])
            {
                errors.Add("Wick has no reachable footing.");
                return;
            }
            foreach (var enemy in Enemies(plan))
            {
                bool before = CanHit(plan.map, intact, enemy, EmptyBroken());
                bool afterOpen = CanHit(plan.map, opened, enemy, opened.broken);
                bool afterAll = CanHit(plan.map, allBroken, enemy, allBroken.broken);
                if (before && !afterAll)
                    errors.Add($"Breaking every B tile strands a required enemy ({enemy.kind}) that was reachable before.");
                else if (!before && !afterOpen)
                    errors.Add($"Wick cannot reach or shoot a required enemy ({enemy.kind}).");
            }
            for (int i = 0; i < allBroken.nodes.Count; i++)
            {
                if (!allBroken.reach[i]) continue;
                if (!allBroken.returns[i])
                {
                    errors.Add("Breaking every B tile traps Wick somewhere he cannot leave.");
                    break;
                }
            }
        }

        static void CheckHatches(StagePlan plan, List<Node> nodes, bool[] reach, List<string> errors)
        {
            var map = plan.map;
            if (map.Storeys < 2) return;
            for (int storey = 0; storey < map.Storeys - 1; storey++)
            {
                int cap = (storey + 1) * StageMap.StoreyRows - 1;
                var hatches = new List<int>();
                for (int c = 0; c < StageMap.Cols; c++)
                {
                    var cell = map.Get(c, cap);
                    if (cell.kind == TileKind.Empty && cell.ladder) hatches.Add(c);
                }
                if (hatches.Count == 0)
                {
                    errors.Add($"Storey {storey + 1} has no hatch Wick can climb.");
                    continue;
                }
                bool any = false;
                foreach (int col in hatches)
                {
                    bool canReach = false;
                    for (int i = 0; i < nodes.Count; i++)
                        if (reach[i] && nodes[i].Col == col && nodes[i].Storey == storey) canReach = true;
                    if (!canReach) continue;
                    any = true;
                    var arrival = new Vector2(StageMap.CenterX(col), (storey + 1) * (float)StageMap.StoreyRows);
                    if (EnemyNear(plan, arrival, 3f))
                        errors.Add($"An enemy starts within 3 units of the hatch on storey {storey + 1}.");
                    string hit = Simulate(plan, arrival, storey + 1);
                    if (hit != null) errors.Add($"Hatch arrival on storey {storey + 2}: {hit}");
                }
                if (!any) errors.Add($"Wick cannot reach a hatch on storey {storey + 1}.");
            }
        }

        static void CheckPockets(StagePlan plan, HashSet<int> broken, List<Node> nodes, bool[] reach, List<string> errors)
        {
            var map = plan.map;
            var seen = new bool[StageMap.Cols, map.Rows];
            var stack = new List<Vector2Int>();
            for (int r = 0; r < map.Rows; r++)
                for (int c = 0; c < StageMap.Cols; c++)
                {
                    if (seen[c, r] || Blocks(map, c, r, broken)) continue;
                    var cells = new List<Vector2Int>();
                    stack.Clear();
                    stack.Add(new Vector2Int(c, r));
                    seen[c, r] = true;
                    bool shootable = false;
                    while (stack.Count > 0)
                    {
                        var cell = stack[stack.Count - 1];
                        stack.RemoveAt(stack.Count - 1);
                        cells.Add(cell);
                        if (CanShootCell(map, nodes, reach, broken, cell.x, cell.y)) shootable = true;
                        Try(cell.x + 1, cell.y);
                        Try(cell.x - 1, cell.y);
                        Try(cell.x, cell.y + 1);
                        Try(cell.x, cell.y - 1);
                    }
                    if (shootable || !BubbleCouldEnter(plan, cells)) continue;
                    errors.Add("Unshootable pocket: a bubble or its children can sit in empty space Wick cannot shoot into.");
                    return;

                    void Try(int x, int y)
                    {
                        if (x < 0 || y < 0 || x >= StageMap.Cols || y >= map.Rows || seen[x, y]) return;
                        if (Blocks(map, x, y, broken)) return;
                        seen[x, y] = true;
                        stack.Add(new Vector2Int(x, y));
                    }
                }
        }

        /// <summary>The pop count a stage of this difficulty must land in (Section 2.2 of the authored-stages spec).</summary>
        public static void PopBand(int difficulty, out int min, out int max)
        {
            min = difficulty == 1 ? 10 : difficulty == 2 ? 20 : difficulty == 3 ? 35 : 55;
            max = difficulty == 1 ? 25 : difficulty == 2 ? 40 : difficulty == 3 ? 65 : 90;
        }

        static void CheckPops(StageSpec spec, StagePlan plan, List<string> errors)
        {
            int pops = StageReport.PopsNeeded(plan);
            PopBand(spec.Difficulty, out int min, out int max);
            if (pops < min || pops > max)
                errors.Add($"Pop count {pops} is outside the D{spec.Difficulty} band ({min} to {max}).");

            float shortest = float.MaxValue;
            bool any = false;
            for (int slot = spec.MinStageInWorld; slot <= spec.MaxStageInWorld; slot++)
            {
                int stage = (spec.World - 1) * Tuning.I.stagesPerWorld + slot;
                shortest = Mathf.Min(shortest, StagePicker.StageTime(stage, true));
                any = true;
            }
            if (!any) shortest = StagePicker.StageTime((spec.World - 1) * Tuning.I.stagesPerWorld + 1, true);
            float rate = pops / Mathf.Max(0.01f, shortest);
            if (rate > TouchPopsPerSecond)
                errors.Add($"Pop rate {rate:0.##} per second is faster than touch controls allow ({TouchPopsPerSecond:0.##}).");
        }

        static void CheckFairStart(StagePlan plan, List<string> errors)
        {
            int storey = plan.map.StoreyOf(plan.wickPosition.y);
            string hit = Simulate(plan, plan.wickPosition, storey);
            if (hit != null) errors.Add(hit);

            if (plan.spawns == null) return;
            for (int i = 0; i < plan.spawns.Count; i++)
            {
                var spawn = plan.spawns[i];
                if (spawn.pogo || spawn.ceramic || spawn.steel) continue;
                if (spawn.kind != BubbleKind.Gloop && spawn.kind != BubbleKind.Trigloop && spawn.kind != BubbleKind.Lead) continue;
                var swapped = new List<SpawnSpec>(plan.spawns);
                var lucky = swapped[i];
                lucky.kind = BubbleKind.Lucky;
                swapped[i] = lucky;
                var trial = Copy(plan, swapped);
                string luckyHit = Simulate(trial, trial.wickPosition, storey);
                if (luckyHit != null) errors.Add("With a Lucky in place of bubble " + i + ": " + luckyHit);
            }
        }

        static string Simulate(StagePlan plan, Vector2 feet, int storey)
        {
            var previousMap = Game.Map;
            var previousFeet = Game.WickFeet;
            var previousBox = Game.WickBox;
            var root = new GameObject("StageValidator");
            try
            {
                Game.InstallMap(plan.map);
                bool standing = Standing(plan.map, feet);
                var box = Body(feet, standing);
                Game.WickFeet = feet;
                Game.WickBox = box;

                var bubbles = new List<Bubble>();
                if (plan.spawns != null)
                    foreach (var spawn in plan.spawns)
                    {
                        if (plan.map.StoreyOf(spawn.position.y) != storey) continue;
                        int dir = spawn.direction < 0 ? -1 : 1;
                        float vx = dir * Tuning.I.bubbleSpeedX * Tuning.I.Kind(spawn.kind).speedScale;
                        bubbles.Add(Bubble.Create(root.transform, spawn.kind, Mathf.Clamp(spawn.tier, 0, Tuning.I.tiers.Length - 1),
                            spawn.position, new Vector2(vx, 0f), false, spawn.ceramic, spawn.pogo, spawn.steel, spawn.mount, spawn.patient));
                    }
                var rollers = new List<Roller>();
                if (plan.rollers != null)
                    foreach (var spec in plan.rollers)
                    {
                        if (plan.map.StoreyOf(spec.surfaceY + 0.05f) != storey) continue;
                        rollers.Add(Roller.Create(root.transform, spec.tier, spec.x, spec.surfaceY, spec.direction, spec.steel, spec.ceramic));
                    }
                Spikes spikes = null;
                bool spikesHere = false;
                foreach (var strip in plan.map.spikes)
                {
                    float y = strip.row < 0 ? 0f : strip.row + 1f;
                    if (plan.map.StoreyOf(y) == storey) spikesHere = true;
                }
                if (spikesHere) spikes = Spikes.Create(root.transform, plan.map);

                string HitNow()
                {
                    foreach (var bubble in bubbles)
                    {
                        if (bubble == null || bubble.Popped || !bubble.CanHurt) continue;
                        if (!CircleHits(bubble.Position, bubble.Radius, box)) continue;
                        var chest = new Vector2(feet.x, box.center.y);
                        if (RoofBetween(plan.map, chest, bubble.Position)) continue;
                        return "A bubble hits Wick within 1.2 s of the start.";
                    }
                    foreach (var roller in rollers)
                    {
                        if (roller == null || roller.Popped) continue;
                        if (CircleHits(roller.Position, roller.Radius, box))
                            return "A roller hits Wick within 1.2 s of the start.";
                    }
                    if (spikes != null && spikes.Hurts(box, out _))
                        return "A spike hits Wick within 1.2 s of the start.";
                    return null;
                }

                string immediate = HitNow();
                if (immediate != null) return immediate;
                for (int tick = 0; tick < FairTicks; tick++)
                {
                    if (spikes != null) spikes.Tick(FairDt);
                    for (int i = 0; i < rollers.Count; i++) rollers[i].Tick(FairDt, rollers);
                    for (int i = 0; i < bubbles.Count; i++) bubbles[i].Tick(FairDt);
                    string hit = HitNow();
                    if (hit != null) return hit;
                }
                return null;
            }
            catch (Exception e)
            {
                return "Fair-start simulation failed: " + e.GetType().Name + " " + e.Message;
            }
            finally
            {
                Game.InstallMap(previousMap);
                Game.WickFeet = previousFeet;
                Game.WickBox = previousBox;
                if (root != null) UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>
        /// Where the exit door of a cleared stage stands: the right-most spot on Wick's storey that he can walk, climb or
        /// jump to from his feet, with the same slow-clear reach the checks use (Moss in Heavy Boots). Spots he can stand
        /// up in beat crouch-only ones, then the right-most, then the lowest. Tiles already broken are gone from the map.
        /// Falls back to his own feet when nothing is reachable, so the door can never be out of reach.
        /// </summary>
        public static Vector2 ExitDoorSpot(int world, StageMap map, Vector2 wickFeet)
        {
            if (map == null) return wickFeet;
            int storey = map.StoreyOf(wickFeet.y + 0.01f);
            var plan = new StagePlan { map = map, wickPosition = wickFeet, spawns = new List<SpawnSpec>() };
            var set = Reach(world, plan, EmptyBroken(), storey);
            int best = -1;
            for (int i = 0; i < set.nodes.Count; i++)
            {
                if (!set.reach[i]) continue;
                if (best < 0 || BetterExit(set.nodes[i], set.nodes[best])) best = i;
            }
            if (best < 0) return wickFeet;
            var node = set.nodes[best];
            return new Vector2(StageMap.CenterX(node.Col), node.Feet);
        }

        static bool BetterExit(Node a, Node b)
        {
            if (a.CanJump != b.CanJump) return a.CanJump;
            if (a.Col != b.Col) return a.Col > b.Col;
            return a.Feet < b.Feet;
        }

        struct ReachSet
        {
            public List<Node> nodes;
            public bool[] reach;
            public bool[] returns;
            public HashSet<int> broken;
            public int start;
        }

        static ReachSet Breach(int world, StagePlan plan)
        {
            var broken = EmptyBroken();
            var reach = Reach(world, plan, broken);
            for (int guard = 0; guard < 64; guard++)
            {
                var more = new List<int>();
                for (int i = 0; i < reach.nodes.Count; i++)
                {
                    if (!reach.reach[i]) continue;
                    var node = reach.nodes[i];
                    int support = Mathf.RoundToInt(node.Feet) - 1;
                    float origin = node.Feet + Tip(node.CanJump);
                    MarkUp(plan.map, node.Col, origin, broken, more);
                    MarkDown(plan.map, node.Col, node.Feet, support, broken, more);
                    MarkChip(plan.map, node, broken, more);
                }
                if (more.Count == 0) break;
                foreach (int key in more) broken.Add(key);
                reach = Reach(world, plan, broken);
            }
            return reach;
        }

        static void MarkUp(StageMap map, int col, float origin, HashSet<int> broken, List<int> more)
        {
            int storeyTop = (map.StoreyOf(origin) + 1) * StageMap.StoreyRows;
            for (int r = Mathf.FloorToInt(origin); r < storeyTop; r++)
            {
                if (r < 0) continue;
                if (!Blocks(map, col, r, broken)) continue;
                if (map.Get(col, r).kind == TileKind.Breakable) Add(more, broken, col, r);
                break;
            }
        }

        static void MarkDown(StageMap map, int col, float feet, int support, HashSet<int> broken, List<int> more)
        {
            int floor = map.StoreyOf(feet) * StageMap.StoreyRows;
            for (int r = Mathf.FloorToInt(feet) - 1; r >= floor; r--)
            {
                if (r == support) continue;
                if (!Blocks(map, col, r, broken)) continue;
                if (map.Get(col, r).kind == TileKind.Breakable) Add(more, broken, col, r);
                break;
            }
        }

        static void MarkChip(StageMap map, Node node, HashSet<int> broken, List<int> more)
        {
            int row = Mathf.FloorToInt(node.Feet + 0.2f);
            int top = Mathf.FloorToInt(node.Feet + (node.CanJump ? Tuning.I.hitbox.y : CrouchHeight()) - 0.05f);
            for (int r = row; r <= top; r++)
            {
                TryChip(map, node.Col - 1, r, broken, more);
                TryChip(map, node.Col + 1, r, broken, more);
            }
        }

        static void TryChip(StageMap map, int c, int r, HashSet<int> broken, List<int> more)
        {
            if (c < 0 || c >= StageMap.Cols || r < 0 || r >= map.Rows) return;
            if (map.Get(c, r).kind == TileKind.Breakable && !broken.Contains(Key(c, r)))
                more.Add(Key(c, r));
        }

        static void Add(List<int> more, HashSet<int> broken, int c, int r)
        {
            int key = Key(c, r);
            if (!broken.Contains(key)) more.Add(key);
        }

        /// <summary>onlyStorey >= 0 keeps that storey's spots only and starts from the spot nearest Wick at any distance.</summary>
        static ReachSet Reach(int world, StagePlan plan, HashSet<int> broken, int onlyStorey = -1)
        {
            var nodes = Stands(plan.map, broken);
            if (onlyStorey >= 0) nodes.RemoveAll(other => other.Storey != onlyStorey);
            int n = nodes.Count;
            var edges = new List<int>[n];
            for (int i = 0; i < n; i++) edges[i] = new List<int>();
            float jumpH = Tuning.I.jumpHeight;
            float reach = WalkSpeed(world) * Tuning.I.jumpTimeToApex * 2f;

            for (int i = 0; i < n; i++)
                for (int j = i + 1; j < n; j++)
                {
                    var a = nodes[i];
                    var b = nodes[j];
                    if (a.Storey != b.Storey && !LadderBetween(plan.map, a, b)) continue;
                    float dy = b.Feet - a.Feet;
                    float dx = Mathf.Abs(StageMap.CenterX(a.Col) - StageMap.CenterX(b.Col));
                    bool same = Mathf.Abs(dy) <= 0.05f && Mathf.Abs(a.Col - b.Col) == 1;
                    bool ladder = LadderBetween(plan.map, a, b);
                    // A jump goes up to jumpH higher, or level or lower: a level jump clears a ground gap or a hole in a deck.
                    bool jump = a.CanJump && dy <= jumpH + 0.05f && dx <= reach + 0.2f && dx >= 0.4f && Clear(plan.map, a, b, broken);
                    bool jumpBack = b.CanJump && -dy <= jumpH + 0.05f && dx <= reach + 0.2f && dx >= 0.4f && Clear(plan.map, a, b, broken);
                    bool drop = dy < -0.2f && dx <= 1.2f && Clear(plan.map, a, b, broken);
                    bool dropBack = -dy < -0.2f && dx <= 1.2f && Clear(plan.map, a, b, broken);
                    if (WallAway(plan.map, a, b, broken) && a.CanJump && dy >= -0.05f && dy <= jumpH + 0.05f && dx <= reach + 0.2f)
                        jump = true;
                    if (WallAway(plan.map, b, a, broken) && b.CanJump && -dy >= -0.05f && -dy <= jumpH + 0.05f && dx <= reach + 0.2f)
                        jumpBack = true;
                    // Chained wall jumps climb a one-column shaft a row at a time. The lower end may be the floor at its foot.
                    bool shaft = a.Col == b.Col && Mathf.Abs(a.Col) > 0 && (ShaftCell(plan.map, a.Col, a.Feet, broken) || ShaftCell(plan.map, b.Col, b.Feet, broken)) && Mathf.Abs(dy) <= 1.05f && Mathf.Abs(dy) > 0.2f;
                    if (same || ladder || jump || drop || shaft) edges[i].Add(j);
                    if (same || ladder || jumpBack || dropBack || shaft) edges[j].Add(i);
                }

            int start = Closest(nodes, plan.wickPosition, onlyStorey >= 0 ? float.MaxValue : 1.5f);
            var forward = new bool[n];
            var backward = new bool[n];
            if (start >= 0)
            {
                Flood(edges, start, forward);
                var reverse = new List<int>[n];
                for (int i = 0; i < n; i++) reverse[i] = new List<int>();
                for (int i = 0; i < n; i++)
                    foreach (int j in edges[i]) reverse[j].Add(i);
                Flood(reverse, start, backward);
            }
            return new ReachSet { nodes = nodes, reach = forward, returns = backward, broken = broken, start = start };
        }

        static bool ShaftCell(StageMap map, int col, float feet, HashSet<int> broken)
        {
            int r = Mathf.FloorToInt(feet + 0.05f);
            return col > 0 && col < StageMap.Cols - 1 && !Blocks(map, col, r, broken)
                && Blocks(map, col - 1, r, broken) && Blocks(map, col + 1, r, broken);
        }

        static List<Node> Stands(StageMap map, HashSet<int> broken)
        {
            var nodes = new List<Node>();
            var seen = new HashSet<int>();
            void Add(int col, float feet)
            {
                if (col < 0 || col >= StageMap.Cols) return;
                float room = Headroom(map, col, feet, broken);
                if (room < CrouchHeight() - 0.02f) return;
                int key = col + Mathf.RoundToInt(feet * 4f) * 64;
                if (!seen.Add(key)) return;
                nodes.Add(new Node
                {
                    Col = col,
                    Feet = feet,
                    CanJump = room >= Tuning.I.hitbox.y - 0.05f,
                    Storey = map.StoreyOf(feet + 0.01f),
                });
            }

            for (int c = 0; c < StageMap.Cols; c++)
            {
                if (!map.IsGap(c) && !Blocks(map, c, 0, broken)) Add(c, 0f);
                for (int r = 0; r < map.Rows; r++)
                {
                    if (!Blocks(map, c, r, broken))
                    {
                        if (c > 0 && c < StageMap.Cols - 1 && Blocks(map, c - 1, r, broken) && Blocks(map, c + 1, r, broken))
                            Add(c, r);
                        continue;
                    }
                    if (r + 1 < map.Rows && Blocks(map, c, r + 1, broken)) continue;
                    Add(c, r + 1f);
                }
            }
            return nodes;
        }

        static bool CanHit(StageMap map, ReachSet set, SpawnSpec enemy, HashSet<int> broken)
        {
            int col = StageMap.Col(enemy.position.x);
            for (int i = 0; i < set.nodes.Count; i++)
            {
                if (!set.reach[i] || set.nodes[i].Col != col) continue;
                var node = set.nodes[i];
                float room = Headroom(map, node.Col, node.Feet, broken);
                if (enemy.position.y >= node.Feet && enemy.position.y <= node.Feet + room) return true;
                float origin = node.Feet + Tip(node.CanJump);
                if (enemy.position.y > origin && ClearRows(map, col, origin, enemy.position.y, broken)) return true;
            }
            return false;
        }

        static bool CanShootCell(StageMap map, List<Node> nodes, bool[] reach, HashSet<int> broken, int col, int row)
        {
            float mid = row + 0.5f;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (!reach[i] || nodes[i].Col != col) continue;
                var node = nodes[i];
                float room = Headroom(map, node.Col, node.Feet, broken);
                if (mid >= node.Feet && mid <= node.Feet + room) return true;
                float origin = node.Feet + Tip(node.CanJump);
                if (mid > origin && ClearRows(map, col, origin, mid, broken)) return true;
            }
            return false;
        }

        static bool BubbleCouldEnter(StagePlan plan, List<Vector2Int> cells)
        {
            float child = Radius(3);
            foreach (var spawn in Enemies(plan))
            {
                float radius = spawn.kind == BubbleKind.Clinger ? child : Mathf.Min(Radius(spawn.tier), child);
                foreach (var cell in cells)
                {
                    var rect = StageMap.CellRect(cell.x, cell.y);
                    if (CircleHits(spawn.position, radius, rect)) return true;
                }
            }
            return false;
        }

        static bool ClearRows(StageMap map, int col, float fromY, float toY, HashSet<int> broken)
        {
            int lo = Mathf.FloorToInt(Mathf.Min(fromY, toY));
            int hi = Mathf.FloorToInt(Mathf.Max(fromY, toY));
            for (int r = lo; r <= hi; r++)
                if (Blocks(map, col, r, broken) && r + 1f > fromY + 0.05f && r < toY - 0.05f) return false;
            return true;
        }

        static bool Clear(StageMap map, Node a, Node b, HashSet<int> broken)
        {
            int lo = Mathf.Min(a.Col, b.Col);
            int hi = Mathf.Max(a.Col, b.Col);
            int row = Mathf.FloorToInt(Mathf.Max(a.Feet, b.Feet) + 0.2f);
            for (int c = lo + 1; c < hi; c++)
                if (Blocks(map, c, row, broken) || Blocks(map, c, row + 1, broken)) return false;
            return true;
        }

        static bool WallAway(StageMap map, Node from, Node to, HashSet<int> broken)
        {
            int row = Mathf.FloorToInt(from.Feet + 0.2f);
            bool left = Blocks(map, from.Col - 1, row, broken) || from.Col == 0;
            bool right = Blocks(map, from.Col + 1, row, broken) || from.Col == StageMap.Cols - 1;
            if (left && to.Col > from.Col) return true;
            if (right && to.Col < from.Col) return true;
            return false;
        }

        static bool LadderBetween(StageMap map, Node a, Node b)
        {
            if (a.Col != b.Col || Mathf.Abs(a.Feet - b.Feet) < 0.2f) return false;
            int lo = Mathf.FloorToInt(Mathf.Min(a.Feet, b.Feet));
            int hi = Mathf.FloorToInt(Mathf.Max(a.Feet, b.Feet));
            if (hi <= lo) return false;
            for (int r = lo; r < hi && r < map.Rows; r++)
                if (r >= 0 && !map.Get(a.Col, r).ladder) return false;
            return true;
        }

        static void Flood(List<int>[] edges, int start, bool[] seen)
        {
            var stack = new Stack<int>();
            stack.Push(start);
            seen[start] = true;
            while (stack.Count > 0)
            {
                int node = stack.Pop();
                if (node < 0 || node >= edges.Length) continue;
                foreach (int next in edges[node])
                {
                    if (next < 0 || next >= seen.Length || seen[next]) continue;
                    seen[next] = true;
                    stack.Push(next);
                }
            }
        }

        static int Closest(List<Node> nodes, Vector2 wick, float maxSqrDistance = 1.5f)
        {
            int best = -1;
            float bestD = float.MaxValue;
            for (int i = 0; i < nodes.Count; i++)
            {
                float dx = StageMap.CenterX(nodes[i].Col) - wick.x;
                float dy = nodes[i].Feet - wick.y;
                float d = dx * dx + dy * dy;
                if (d < bestD) { bestD = d; best = i; }
            }
            return bestD <= maxSqrDistance ? best : -1;
        }

        static bool Supported(StageMap map, Vector2 feet, out string why)
        {
            why = null;
            if (feet.y <= 0.05f)
            {
                int c0 = StageMap.Col(feet.x - Tuning.I.hitbox.x * 0.5f + 0.001f);
                int c1 = StageMap.Col(feet.x + Tuning.I.hitbox.x * 0.5f - 0.001f);
                for (int c = c0; c <= c1; c++)
                    if (map.IsGap(c)) { why = "His feet are over a gap."; return false; }
                return true;
            }
            int col = StageMap.Col(feet.x);
            int row = Mathf.FloorToInt(feet.y) - 1;
            if (map.IsSolid(col, row) || map.Get(col, row).kind == TileKind.Breakable) return true;
            why = "Nothing is under his feet.";
            return false;
        }

        static bool Standing(StageMap map, Vector2 feet) =>
            Headroom(map, StageMap.Col(feet.x), feet.y, EmptyBroken()) >= Tuning.I.hitbox.y - 0.05f;

        static Rect Body(Vector2 feet, bool standing)
        {
            float height = standing ? Tuning.I.hitbox.y : CrouchHeight();
            return new Rect(feet.x - Tuning.I.hitbox.x * 0.5f, feet.y + FootInset, Tuning.I.hitbox.x, Mathf.Max(0.01f, height - FootInset));
        }

        static float CrouchHeight() => Tuning.I.hitbox.y * Tuning.I.crouchHeightScale;

        static float Tip(bool standing)
        {
            float up = Tuning.I.launcherTip.y;
            if (standing) return up;
            return Mathf.Min(up * Tuning.I.crouchSquash.y, CrouchHeight() - Tuning.I.Pixel);
        }

        static float WalkSpeed(int world)
        {
            float speed = Character.ById("moss").BaseRunSpeed * Debuff.ByName("Heavy Boots").speedScale;
            if (world == 3) speed *= Tuning.I.sandSpeedScale;
            return speed;
        }

        static float Headroom(StageMap map, int col, float feet, HashSet<int> broken)
        {
            int storey = map.StoreyOf(Mathf.Max(0f, feet));
            float ceil = (storey + 1) * (float)StageMap.StoreyRows;
            int start = Mathf.Approximately(feet, Mathf.Round(feet)) ? Mathf.RoundToInt(feet) : Mathf.FloorToInt(feet) + 1;
            for (int r = start; r < (storey + 1) * StageMap.StoreyRows; r++)
                if (Blocks(map, col, r, broken)) return r - feet;
            return ceil - feet;
        }

        static bool Blocks(StageMap map, int c, int r, HashSet<int> broken)
        {
            if (c < 0 || c >= StageMap.Cols) return true;
            if (r < 0 || r >= map.Rows) return false;
            var cell = map.Get(c, r);
            if (cell.kind == TileKind.Solid) return true;
            if (cell.kind == TileKind.Breakable) return !broken.Contains(Key(c, r));
            return false;
        }

        static bool MountOk(StageMap map, SpawnSpec spawn)
        {
            if (spawn.mount == ClingerMount.Ceiling) return spawn.position.y < map.StoreyCeiling(spawn.position.y);
            if (spawn.mount == ClingerMount.Floor) return spawn.position.y >= map.StoreyBase(spawn.position.y);
            return true;
        }

        static bool NearLadder(StageMap map, int c)
        {
            for (int col = c - 1; col <= c + 1; col++)
            {
                if (col < 0 || col >= StageMap.Cols) continue;
                for (int r = 0; r < map.Rows; r++)
                    if (map.Get(col, r).ladder) return true;
            }
            return false;
        }

        static bool SameSurface(int spikeRow, float surfaceY) =>
            spikeRow < 0 ? surfaceY <= 0.05f : Mathf.Abs(surfaceY - (spikeRow + 1f)) < 0.2f;

        static bool EnemyNear(StagePlan plan, Vector2 point, float distance)
        {
            float limit = distance * distance;
            foreach (var enemy in Enemies(plan))
                if ((enemy.position - point).sqrMagnitude < limit) return true;
            if (plan.rollers == null) return false;
            foreach (var roller in plan.rollers)
                if (!roller.steel && (new Vector2(roller.x, roller.surfaceY) - point).sqrMagnitude < limit) return true;
            return false;
        }

        static void CountEnemies(StagePlan plan, int[] counts)
        {
            foreach (var enemy in Enemies(plan))
            {
                int storey = plan.map.StoreyOf(enemy.position.y);
                if (storey >= 0 && storey < counts.Length) counts[storey]++;
            }
            if (plan.rollers != null)
                foreach (var roller in plan.rollers)
                {
                    if (roller.steel) continue;
                    int storey = plan.map.StoreyOf(roller.surfaceY + 0.05f);
                    if (storey >= 0 && storey < counts.Length) counts[storey]++;
                }
            if (plan.waves == null) return;
            int waveStorey = plan.waveStorey < 0 ? plan.map.Storeys - 1 : plan.waveStorey;
            foreach (var wave in plan.waves)
                foreach (var spawn in wave)
                    if (!spawn.steel && waveStorey >= 0 && waveStorey < counts.Length) counts[waveStorey]++;
        }

        static IEnumerable<SpawnSpec> Enemies(StagePlan plan)
        {
            if (plan.spawns != null)
                foreach (var spawn in plan.spawns)
                    if (!spawn.steel) yield return spawn;
        }

        static bool ClassicOrSignature(int world, BubbleKind kind)
        {
            if (kind == BubbleKind.Gloop || kind == BubbleKind.Trigloop || kind == BubbleKind.Lead) return true;
            return HomeWorld(kind) == world;
        }

        static int HomeWorld(BubbleKind kind)
        {
            switch (kind)
            {
                case BubbleKind.Clinger: return 2;
                case BubbleKind.Sinker: return 3;
                case BubbleKind.Frost: return 4;
                case BubbleKind.Ghost: return 5;
                case BubbleKind.Scorch: return 6;
                default: return 0;
            }
        }

        static StagePlan Copy(StagePlan plan, List<SpawnSpec> spawns) => new StagePlan
        {
            map = plan.map,
            spawns = spawns,
            rollers = plan.rollers,
            wickPosition = plan.wickPosition,
            waves = plan.waves,
            waveStorey = plan.waveStorey,
        };

        static bool CircleHits(Vector2 center, float radius, Rect box)
        {
            float x = Mathf.Clamp(center.x, box.xMin, box.xMax);
            float y = Mathf.Clamp(center.y, box.yMin, box.yMax);
            float dx = center.x - x, dy = center.y - y;
            return dx * dx + dy * dy <= radius * radius + 1e-4f;
        }

        static bool RoofBetween(StageMap map, Vector2 from, Vector2 to)
        {
            for (int i = 1; i < 8; i++)
            {
                var p = Vector2.Lerp(from, to, i / 8f);
                int c = StageMap.Col(p.x);
                int r = StageMap.Row(p.y);
                if (c < 0 || c >= StageMap.Cols || r < 0 || r >= map.Rows) continue;
                if (map.IsSolid(c, r)) return true;
            }
            return false;
        }

        static float Radius(int tier)
        {
            var tiers = Tuning.I.tiers;
            return tiers[Mathf.Clamp(tier, 0, tiers.Length - 1)].diameter * 0.5f;
        }

        static HashSet<int> EmptyBroken() => new HashSet<int>();

        static HashSet<int> EveryBreakable(StageMap map)
        {
            var broken = new HashSet<int>();
            for (int r = 0; r < map.Rows; r++)
                for (int c = 0; c < StageMap.Cols; c++)
                    if (map.Get(c, r).kind == TileKind.Breakable) broken.Add(Key(c, r));
            return broken;
        }

        static int Key(int c, int r) => r * StageMap.Cols + c;
    }
}
