using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>
    /// World 1, the teaching boss: an iron stove on stilt legs that sweeps the chimneys (design spec, World 1). Only its
    /// belly grate hurts it, and the grate is shut most of the time.
    ///
    /// Phase 1, Stilt walk (6): it walks, and at every stop stomps (a wave each way along the roof) and coughs a fan of
    /// soot from its pipe. After three coughs (two on the hard road) the grate glows for 2 s and Wick, under the belly,
    /// shoots up. Phase 2, Chimney hop (6): it leaps between the two chimney tops and the high platform and perches
    /// leaning out over open floor, grate facing down; a curtain with a lit lane falls, a Gloop drops from the top-middle
    /// pipe, and the grate opens for Wick standing in the lane. Each chimney it jumps off loses its top tile. Phase 3,
    /// Flue run (8): it dives into the chimneys; one pot puffs, then it bursts out of that pot firing a ring with a gap
    /// straight down, hangs there with the grate open, and goes back down; between pops it charges along the roof from a
    /// screen edge (on the hard road two pots puff and one is a fake).
    /// </summary>
    [Preserve]
    public sealed class Sootjack : BossDef
    {
        public const string ArenaName = "w1_sootjack_roof";
        public const int WalkPose = 0, PerchPose = 1, ChargePose = 2;

        // The body, in units from its centre. Feet are 3.2 below the centre; the belly is 2.3 above the feet, so Wick
        // (1.47 tall) walks under it; the stove is 2.9 tall and the pipe 1.2 more.
        public const float HalfBody = 3.4f;
        public const float HalfHeight = 3.2f;
        public const float FootX = 3f;
        public const float StiltW = 0.8f;
        public const float Clearance = 2.3f;
        public const float BellyH = 0.45f;
        public const float LegH = 0.8f;
        public const float BellyLocalY = -HalfHeight + Clearance;
        const float StoveTop = 2f;
        const float PipeW = 1f, PipeH = 1.2f, PipeX = 1.6f;

        /// <summary>X of the two flue pots in the roof, where phase 3 bursts out.</summary>
        public static readonly float[] PotX = { -8f, 8f };

        /// <summary>
        /// A place it perches: one leg grips the surface at legX and the stove leans out dir (+1 right, -1 left) over open
        /// air, so the grate hangs over floor Wick can stand on. TopCells are the chimney's top tiles (column, row) that
        /// crumble when it jumps off; a platform has none.
        /// </summary>
        public sealed class PerchSpot
        {
            public readonly string name;
            public readonly float legX;
            public readonly int dir;
            readonly Vector2Int[] topCells;

            public PerchSpot(string name, float legX, int dir, params Vector2Int[] topCells)
            {
                this.name = name;
                this.legX = legX;
                this.dir = dir;
                this.topCells = topCells;
            }

            public bool IsStack => topCells.Length > 0;
            public Vector2Int[] TopCells => topCells;
            /// <summary>The stove's centre x: the leg is at the stove's back corner.</summary>
            public float CentreX => legX + dir * FootX;
            /// <summary>The boss's centre when its leg stands on a surface at this height.</summary>
            public Vector2 Centre(float surface) => new Vector2(CentreX, surface + LegH - BellyLocalY);
            /// <summary>The middle of the grate, on the belly.</summary>
            public Vector2 GrateCentre(float surface) => new Vector2(CentreX, surface + LegH + BellyH * 0.5f);
        }

        /// <summary>The perches in the order it visits them: left chimney, platform right, right chimney, platform left.</summary>
        public static readonly PerchSpot[] Perches =
        {
            new PerchSpot("left chimney", -13.4f, 1, new Vector2Int(1, 3), new Vector2Int(2, 3)),
            new PerchSpot("high platform, right", 3.6f, 1),
            new PerchSpot("right chimney", 13.4f, -1, new Vector2Int(29, 3), new Vector2Int(30, 3)),
            new PerchSpot("high platform, left", -3.6f, -1),
        };

        public override int World => 1;
        public override string Name => "Sootjack";
        public override Vector2 Size => new Vector2(HalfBody * 2f, HalfHeight * 2f);
        public override string ArenaId => ArenaName;
        public override Vector2? StartSpot => new Vector2(0f, HalfHeight);
        public override bool ContactFromParts => true;

        /// <summary>The cores that stay put: the grate over each perch and over each pot, to be shot from the floor.</summary>
        public override IReadOnlyList<Vector2> CoreSpots
        {
            get
            {
                var spots = new List<Vector2>();
                foreach (var p in Perches)
                {
                    float surface = p.IsStack ? 4f : 8f;
                    spots.Add(p.GrateCentre(surface));
                }
                foreach (float x in PotX)
                    spots.Add(new Vector2(x, Tuning.I.sootHoverFeet + HalfHeight + BellyLocalY + BellyH * 0.5f));
                return spots;
            }
        }

        public override PlaceholderArt.BossSprites Sprites(Theme theme) => SootjackArt.For(theme);

        protected override BossPhase[] BuildPhases(bool hard) => new BossPhase[]
        {
            new StiltWalk { name = "Stilt walk", hp = 6 },
            new ChimneyHop { name = "Chimney hop", hp = 6 },
            new FlueRun { name = "Flue run", hp = 8 },
        };

        public override void BuildParts(Boss boss, bool hard)
        {
            SetPose(boss, WalkPose, 1);
            for (int i = 0; i < PotX.Length; i++)
                boss.SetMarker("pot" + i, SootjackArt.Pot(boss.Theme), new Vector2(PotX[i], 0f), true);
        }

        /// <summary>It bursts out from behind the roof at the top right and lands, centred, with a stomp (the first thing phase 1 does).</summary>
        public override Vector2? EntrancePosition(Boss boss, float k)
        {
            var end = StartSpot.Value;
            const float hiddenUntil = 0.2f;
            boss.Hidden = k < hiddenUntil;
            boss.Pose = WalkPose;
            float t = Mathf.Clamp01((k - hiddenUntil) / (1f - hiddenUntil));
            var start = new Vector2(13f, 18f);
            var control = new Vector2(7f, 20f);
            float u = 1f - t;
            return u * u * start + 2f * u * t * control + t * t * end;
        }

        // ------------------------------------------------------------------ poses

        /// <summary>A part's rectangle, mirrored about the boss's centre when it faces left.</summary>
        static Rect Local(float x, float y, float w, float h, int dir) => dir >= 0 ? new Rect(x, y, w, h) : new Rect(-x - w, y, w, h);

        /// <summary>
        /// Gives the boss the parts of a pose. The grate is always the first part, so a shot that touches it and its
        /// neighbour counts as the grate; it keeps its Open state. Closed, the grate is armour.
        /// </summary>
        public static void SetPose(Boss boss, int pose, int dir)
        {
            var grate = boss.Part("grate") ?? new BossPart { Name = "grate", IsCore = true };
            foreach (var old in new List<BossPart>(boss.Parts)) boss.RemovePart(old);
            boss.Pose = pose;
            boss.Flip = dir < 0;

            if (pose == ChargePose)
            {
                float h = Tuning.I.sootChargeHeight;
                grate.Local = Local(-1.5f, h * 0.5f - BellyH, 3f, BellyH, dir);
                boss.AddPart(grate);
                boss.AddPart(new BossPart { Name = "sled", Local = Local(-HalfBody, -h * 0.5f, HalfBody * 2f, h, dir) });
                return;
            }

            grate.Local = Local(-1.5f, BellyLocalY, 3f, BellyH, dir);
            boss.AddPart(grate);
            boss.AddPart(new BossPart { Name = "skirt back", Local = Local(-HalfBody, BellyLocalY, HalfBody - 1.5f, BellyH, dir) });
            boss.AddPart(new BossPart { Name = "skirt front", Local = Local(1.5f, BellyLocalY, HalfBody - 1.5f, BellyH, dir) });
            boss.AddPart(new BossPart { Name = "body", Local = Local(-HalfBody, BellyLocalY + BellyH, HalfBody * 2f, StoveTop - BellyLocalY - BellyH, dir) });
            boss.AddPart(new BossPart { Name = "pipe", Local = Local(PipeX, StoveTop, PipeW, PipeH, dir) });
            if (pose == WalkPose)
            {
                boss.AddPart(new BossPart { Name = "stilt back", Harmless = true, Local = Local(-FootX - StiltW * 0.5f, -HalfHeight, StiltW, Clearance, dir) });
                boss.AddPart(new BossPart { Name = "stilt front", Harmless = true, Local = Local(FootX - StiltW * 0.5f, -HalfHeight, StiltW, Clearance, dir) });
            }
            else
            {
                boss.AddPart(new BossPart { Name = "leg", Harmless = true, Local = Local(-FootX - StiltW * 0.5f, BellyLocalY - LegH, StiltW, LegH, dir) });
            }
        }

        static Vector2 PipeTip(Boss boss) =>
            boss.Centre + new Vector2((boss.Flip ? -1f : 1f) * (PipeX + PipeW * 0.5f), StoveTop + PipeH);

        static void CloseGrate(Boss boss)
        {
            var grate = boss.Part("grate");
            if (grate != null) grate.Open = false;
        }

        static void OpenGrate(Boss boss)
        {
            var grate = boss.Part("grate");
            if (grate != null) grate.Open = true;
        }

        // ------------------------------------------------------------------ phase 1: stilt walk

        /// <summary>
        /// Walks, stomps and coughs; after enough coughs it tires: it sags, its shutter slides back and the grate is open
        /// while it stands panting, until the time is up or the grate has taken its hits for this opening.
        /// </summary>
        public sealed class StiltWalk : BossPhase
        {
            enum State { Stomp, Cough, Walk, Tiring, Tired }

            State state = State.Stomp;
            bool told;
            float timer, clock, smoke;
            int dir = 1;

            /// <summary>Coughs since the grate last closed.</summary>
            public int Coughs { get; private set; }
            public int Stomps { get; private set; }
            /// <summary>True from the moment it tires (shutter sliding) until the grate shuts again.</summary>
            public bool Tired => state == State.Tiring || state == State.Tired;

            static Tuning T => Tuning.I;
            int Needed(Boss boss) => boss.Hard ? T.sootCoughsToOpenHard : T.sootCoughsToOpen;

            public override void Enter(Boss boss)
            {
                // The first stop is the landing: it stomps as soon as the intro is over.
                state = State.Stomp;
                told = false;
                Coughs = 0;
                dir = 1;
                boss.Hidden = false;
                SetPose(boss, WalkPose, 1);
                CloseGrate(boss);
            }

            public override void Tick(Boss boss, float dt, Vector2 target, float speedScale, int bubblesAlive)
            {
                var stage = boss.Stage;
                if (stage == null) return;
                float k = dt * speedScale * boss.HardRateScale;
                clock += dt;
                Breathe(boss, dt);
                switch (state)
                {
                    case State.Stomp:
                        Stand(boss, 0f);
                        if (told) break;
                        told = true;
                        var feet = new Rect(boss.Centre.x - HalfBody, 0f, HalfBody * 2f, 0.8f);
                        stage.Telegraph(TellKind.DustColumn, feet, T.sootStompTell, () => DoStomp(boss));
                        break;
                    case State.Cough:
                        Stand(boss, 0f);
                        if (told) break;
                        told = true;
                        var glow = new Rect(PipeTip(boss).x - 0.5f, PipeTip(boss).y - 0.2f, 1f, 1f);
                        stage.Telegraph(TellKind.EdgeGlow, glow, T.sootCoughTell, () => DoCough(boss));
                        break;
                    case State.Walk:
                        Move(boss, dt, speedScale);
                        timer -= k;
                        if (timer <= 0f) { state = State.Stomp; told = false; }
                        break;
                    case State.Tiring:
                        // It sags and pants while the shutter slides back.
                        timer -= k;
                        Stand(boss, -0.25f * Mathf.Clamp01(1f - timer / Mathf.Max(0.01f, T.sootTiredTime)));
                        if (timer > 0f) break;
                        boss.Opening = false;
                        boss.CoreHits = 0;
                        OpenGrate(boss);
                        state = State.Tired;
                        timer = T.sootGrateGlow;
                        break;
                    case State.Tired:
                        timer -= k;
                        Stand(boss, -0.25f + Mathf.Sin(clock * 9f) * 0.06f);
                        boss.Tilt = Mathf.Sin(clock * 9f) * 1.5f;
                        if (timer <= 0f || boss.CoreHits >= T.sootHitsPerOpening) EndTired(boss);
                        break;
                }
            }

            public override void Exit(Boss boss)
            {
                CloseGrate(boss);
                boss.Opening = false;
                boss.Tilt = 0f;
            }

            /// <summary>Rocks from side to side while it stands, and puffs smoke from its pipe.</summary>
            void Breathe(Boss boss, float dt)
            {
                if (state == State.Stomp || state == State.Cough) boss.Tilt = Mathf.Sin(clock * 3f) * 3f;
                smoke -= dt;
                if (smoke > 0f) return;
                smoke = T.sootSmokeEvery;
                boss.Stage.Puff(PipeTip(boss));
            }

            void Stand(Boss boss, float sag) => boss.SetPosition(boss.Centre.x, HalfHeight + sag);

            void Move(Boss boss, float dt, float speedScale)
            {
                float speed = T.sootWalkSpeed * boss.HardSpeedScale * speedScale;
                float x = boss.Centre.x + dir * speed * dt;
                float limit = T.sootWalkLimit;
                if (x > limit) { x = limit; dir = -1; }
                else if (x < -limit) { x = -limit; dir = 1; }
                // A stride: it bobs on each step and leans into the walk.
                float step = Mathf.Sin(clock * 6f);
                boss.SetPosition(x, HalfHeight + Mathf.Abs(step) * 0.18f);
                boss.Tilt = -dir * 3f + step * 2f;
            }

            /// <summary>A stomp: a wave goes out each way along the roof from the feet; on the hard road a second pair follows.</summary>
            void DoStomp(Boss boss)
            {
                Stomps++;
                var stage = boss.Stage;
                float x = boss.Centre.x;
                var waves = new List<Launch>();
                AddWaves(waves, x, 0f);
                if (boss.Hard) AddWaves(waves, x, T.sootStompEcho);
                stage.Fire(waves.ToArray());
                stage.Impact(new Vector2(x, 0f), 1f);
                state = State.Cough;
                told = false;
            }

            static void AddWaves(List<Launch> waves, float x, float delay)
            {
                foreach (var w in Patterns.GroundWave(0f, x - FootX, -1)) { var l = w; l.delay = delay; waves.Add(l); }
                foreach (var w in Patterns.GroundWave(0f, x + FootX, 1)) { var l = w; l.delay = delay; waves.Add(l); }
            }

            /// <summary>True when Wick stands under the belly, between the stilts: the soot cannot reach him there.</summary>
            static bool Under(Boss boss, Rect wick) =>
                Mathf.Abs(wick.center.x - boss.Centre.x) < FootX - 0.2f && wick.yMax < boss.Centre.y + BellyLocalY;

            /// <summary>
            /// A cough: a fan of soot from the pipe at Wick; if he is under the belly it goes up and out instead (he is
            /// safe there). Once enough have been coughed it tires.
            /// </summary>
            void DoCough(Boss boss)
            {
                var stage = boss.Stage;
                var from = PipeTip(boss);
                var wick = stage.WickBox;
                var aim = Under(boss, wick) ? from + new Vector2(dir * 6f, 5f) : wick.center;
                stage.Fire(Patterns.Fan(from, aim, T.bulletSpeed, T.sootCoughBullets, T.sootCoughSpread));
                stage.Puff(from);
                Coughs++;
                if (Coughs >= Needed(boss))
                {
                    boss.Opening = true;
                    state = State.Tiring;
                    timer = T.sootTiredTime;
                }
                else
                {
                    state = State.Walk;
                    timer = T.sootWalkTime; // it patrols: it turns only at the ends of its walk
                }
            }

            void EndTired(Boss boss)
            {
                CloseGrate(boss);
                boss.Opening = false;
                boss.Tilt = 0f;
                Coughs = 0;
                state = State.Walk;
                timer = T.sootWalkTime;
            }
        }

        // ------------------------------------------------------------------ phase 2: chimney hop

        /// <summary>Hops between the chimney tops and the high platform; at each perch a curtain with a lane, then the grate.</summary>
        public sealed class ChimneyHop : BossPhase
        {
            enum State { HopTell, Hop, Settle, LaneTell, Falling, Tiring, Window, Rest }

            State state;
            bool told;
            float timer, life, smoke;
            int target;
            PerchSpot current;
            Vector2 from, to;
            float flight;

            /// <summary>Index into Perches of the perch it is on (or was last on); -1 before the first landing.</summary>
            public int PerchIndex { get; private set; } = -1;
            public int Landings { get; private set; }
            public int Leaves { get; private set; }

            static Tuning T => Tuning.I;

            public override void Enter(Boss boss)
            {
                state = State.HopTell;
                told = false;
                target = 0;
                current = null;
                PerchIndex = -1;
                boss.Hidden = false;
                SetPose(boss, WalkPose, 1);
                CloseGrate(boss);
            }

            public override void Tick(Boss boss, float dt, Vector2 targetPoint, float speedScale, int bubblesAlive)
            {
                var stage = boss.Stage;
                if (stage == null) return;
                float clock = dt * speedScale * boss.HardRateScale;
                life += dt;
                if (state != State.Hop) boss.Tilt = state == State.Window ? Mathf.Sin(life * 9f) * 1.5f : Mathf.Sin(life * 3f) * 2.5f;
                smoke -= dt;
                if (smoke <= 0f && !boss.Hidden) { smoke = T.sootSmokeEvery; stage.Puff(PipeTip(boss)); }
                switch (state)
                {
                    case State.HopTell:
                        if (told) break;
                        told = true;
                        var perch = Perches[target];
                        float surface = stage.Map.FloorBelow(perch.legX, 20f);
                        var shadow = new Rect(perch.legX - 1.2f, surface, 2.4f, 0.4f);
                        stage.Telegraph(TellKind.RedShadow, shadow, T.sootHopTell, () => StartHop(boss));
                        break;
                    case State.Hop:
                        flight += dt * speedScale / Mathf.Max(0.1f, T.sootHopTime);
                        float e = Mathf.Clamp01(flight);
                        var at = Vector2.Lerp(from, to, e) + Vector2.up * (T.sootHopPeak * 4f * e * (1f - e));
                        boss.SetPosition(at.x, at.y);
                        boss.Tilt = (to.x >= from.x ? -1f : 1f) * 8f * (1f - e);
                        if (flight >= 1f) Land(boss);
                        break;
                    case State.Settle:
                        timer -= clock;
                        if (timer <= 0f) { state = State.LaneTell; told = false; }
                        break;
                    case State.LaneTell:
                        if (told) break;
                        told = true;
                        LightLane(boss);
                        break;
                    case State.Falling:
                        // A perch where it is not tired yet: only the curtain, then on to the next.
                        timer -= clock;
                        if (timer <= 0f) { state = State.Rest; timer = T.sootPerchRest; }
                        break;
                    case State.Tiring:
                        timer -= clock;
                        if (timer > 0f) break;
                        boss.Opening = false;
                        boss.CoreHits = 0;
                        OpenGrate(boss);
                        state = State.Window;
                        timer = T.sootPerchOpen;
                        break;
                    case State.Window:
                        timer -= clock;
                        if (timer <= 0f || boss.CoreHits >= T.sootHitsPerOpening)
                        {
                            CloseGrate(boss);
                            state = State.Rest;
                            timer = T.sootPerchRest;
                        }
                        break;
                    case State.Rest:
                        timer -= clock;
                        if (timer <= 0f) { state = State.HopTell; told = false; }
                        break;
                }
            }

            public override void Exit(Boss boss)
            {
                CloseGrate(boss);
                boss.Opening = false;
                boss.Tilt = 0f;
            }

            /// <summary>True when this perch is one where it is tired and opens: every sootPerchesToTire-th landing.</summary>
            bool TiredHere => Landings % Mathf.Max(1, T.sootPerchesToTire) == 0;

            void StartHop(Boss boss)
            {
                var stage = boss.Stage;
                if (current != null)
                {
                    Leaves++;
                    foreach (var cell in current.TopCells)
                        stage.Arena?.CrumbleTile(cell.x, cell.y, T.sootChimneyCrumble);
                }
                var perch = Perches[target];
                float surface = stage.Map.FloorBelow(perch.legX, 20f);
                from = boss.Centre;
                to = perch.Centre(surface);
                SetPose(boss, PerchPose, perch.dir);
                CloseGrate(boss);
                flight = 0f;
                state = State.Hop;
            }

            void Land(Boss boss)
            {
                var perch = Perches[target];
                boss.SetPosition(to.x, to.y);
                Landings++;
                PerchIndex = target;
                current = perch;
                target = (target + 1) % Perches.Length;
                boss.Stage.Impact(new Vector2(perch.legX, to.y + BellyLocalY - LegH), 0.5f);
                state = State.Settle;
                timer = T.sootPerchSettle;
            }

            /// <summary>
            /// Lights the lane under the grate, tells the pipe at the top middle to drop a Gloop, and when the lane's
            /// tell ends drops a slow curtain with a gap in every few columns besides the lane. On a perch where it is
            /// tired the shutter then slides back and the grate opens.
            /// </summary>
            void LightLane(Boss boss)
            {
                var stage = boss.Stage;
                int cols = Mathf.Clamp(T.sootLaneCols, 1, StageMap.Cols);
                int laneCol = Mathf.Clamp(StageMap.Col(boss.Centre.x) - cols / 2, 0, StageMap.Cols - cols);
                var lane = new Rect(StageMap.CellRect(laneCol, 0).xMin, 0f, cols, StageMap.StoreyRows);
                stage.Telegraph(TellKind.LitLane, lane, T.bossCurtainTell, () =>
                {
                    stage.Fire(Curtain(boss.Centre.y, laneCol, cols));
                    if (TiredHere)
                    {
                        boss.Opening = true;
                        state = State.Tiring;
                        timer = T.sootTiredTime;
                    }
                    else
                    {
                        state = State.Falling;
                        timer = T.sootPerchOpen * 0.5f;
                    }
                });
                if (stage.BubblesAlive < T.bossMaxBubbles)
                    stage.Arena?.FirePipe(PipeSpot.TopMiddle, T.sootPipeTier, 0);
            }

            /// <summary>Its curtain: the kit's curtain with a lane, slower, with one more gap in every few columns (none next to the lane).</summary>
            public static Launch[] Curtain(float storeyY, int laneCol, int laneCols)
            {
                var all = Patterns.CurtainWithLane(storeyY, laneCol, laneCols);
                int every = Mathf.Max(2, T.sootCurtainGapEvery);
                var kept = new List<Launch>();
                foreach (var l in all)
                {
                    int c = StageMap.Col(l.position.x);
                    int fromLane = c < laneCol ? laneCol - c : c - (laneCol + laneCols - 1);
                    if (fromLane % every == 0) continue; // a gap every few columns, counted out from the lane
                    var slow = l;
                    slow.velocity = Vector2.down * T.sootCurtainSpeed;
                    kept.Add(slow);
                }
                return kept.ToArray();
            }
        }

        // ------------------------------------------------------------------ phase 3: flue run

        /// <summary>Dives into the chimneys; pops out of the pot that puffed, and charges along the roof between pops.</summary>
        public sealed class FlueRun : BossPhase
        {
            enum State { Under, Puff, Hover, ChargeTell, Charging }

            State state;
            bool told, nextIsPop = true;
            float timer;
            int ringCount, chargeSide = 1, moveDir;
            float life;

            /// <summary>The pots that are puffing now (on the hard road, the real one and the fake).</summary>
            public List<int> Puffed { get; } = new List<int>();
            /// <summary>The pot it will burst out of, or last did.</summary>
            public int RealPot { get; private set; } = -1;
            /// <summary>The pot it burst out of most recently; -1 before the first pop.</summary>
            public int Emerged { get; private set; } = -1;
            public int Charges { get; private set; }
            public bool Underground { get; private set; }

            static Tuning T => Tuning.I;

            public override void Enter(Boss boss)
            {
                // It dives into the nearest chimney: gone from sight until a pot puffs or the screen edge glows.
                Puffed.Clear();
                Emerged = -1;
                RealPot = -1;
                SetPose(boss, WalkPose, 1);
                CloseGrate(boss);
                GoUnder(boss);
                nextIsPop = true;
            }

            public override void Tick(Boss boss, float dt, Vector2 target, float speedScale, int bubblesAlive)
            {
                var stage = boss.Stage;
                if (stage == null) return;
                float clock = dt * speedScale * boss.HardRateScale;
                switch (state)
                {
                    case State.Under:
                        timer -= clock;
                        if (timer > 0f) break;
                        state = nextIsPop ? State.Puff : State.ChargeTell;
                        told = false;
                        break;
                    case State.Puff:
                        if (told) break;
                        told = true;
                        StartPuff(boss);
                        break;
                    case State.Hover:
                        life += dt;
                        boss.SetPosition(PotX[Emerged], T.sootHoverFeet + HalfHeight + Mathf.Sin(life * 5f) * 0.1f);
                        boss.Tilt = Mathf.Sin(life * 9f) * 1.5f;
                        timer -= clock;
                        if (timer <= 0f || boss.CoreHits >= T.sootHitsPerOpening)
                        {
                            boss.Tilt = 0f;
                            CloseGrate(boss);
                            GoUnder(boss);
                            nextIsPop = false;
                        }
                        break;
                    case State.ChargeTell:
                        if (told) break;
                        told = true;
                        StartChargeTell(boss);
                        break;
                    case State.Charging:
                        Charge(boss, dt, speedScale);
                        break;
                }
            }

            public override void Exit(Boss boss)
            {
                CloseGrate(boss);
                boss.Tilt = 0f;
                boss.Hidden = false;
                Underground = false;
            }

            void GoUnder(Boss boss)
            {
                boss.Hidden = true;
                Underground = true;
                state = State.Under;
                timer = T.sootUnderground;
            }

            static Rect Puff(int pot) => new Rect(PotX[pot] - 0.9f, 0f, 1.8f, 3.5f);

            /// <summary>One pot puffs, then the boss comes out of it. On the hard road a second, fake pot puffs a little shorter.</summary>
            void StartPuff(Boss boss)
            {
                var stage = boss.Stage;
                int real = Random.Range(0, PotX.Length);
                RealPot = real;
                Puffed.Clear();
                Puffed.Add(real);
                stage.Telegraph(TellKind.DustColumn, Puff(real), T.sootPotPuff, () => Emerge(boss, real));
                if (!boss.Hard) return;
                int fake = (real + 1) % PotX.Length;
                Puffed.Add(fake);
                stage.Telegraph(TellKind.DustColumn, Puff(fake), T.sootFakePuff, () => { });
            }

            /// <summary>It bursts out above the pot, firing a ring whose gap points straight down, and hangs with the grate open.</summary>
            void Emerge(Boss boss, int pot)
            {
                var stage = boss.Stage;
                Underground = false;
                boss.Hidden = false;
                SetPose(boss, WalkPose, 1);
                boss.SetPosition(PotX[pot], T.sootHoverFeet + HalfHeight);
                OpenGrate(boss);
                boss.CoreHits = 0;
                boss.Stage.Puff(PipeTip(boss));
                Emerged = pot;

                int slots = Mathf.Max(4, T.sootRingSlots);
                int gap = Mathf.Clamp(T.sootRingGap, 0, slots - 1);
                int turn = (ringCount % 3 == 0) ? 0 : (ringCount % 3 == 1 ? 1 : -1); // the gap turns a little each time
                ringCount++;
                int gapStart = slots * 3 / 4 - gap / 2 + turn;
                stage.Fire(Patterns.RingWithGap(boss.Centre, slots, gapStart, gap, T.bulletSpeed));
                stage.Impact(new Vector2(PotX[pot], 0f), 0.6f);
                state = State.Hover;
                timer = T.sootHover;
            }

            /// <summary>The screen edge glows where the charge will come in.</summary>
            void StartChargeTell(Boss boss)
            {
                chargeSide = -chargeSide;
                float half = Tuning.I.HalfWidth;
                var edge = chargeSide < 0 ? new Rect(-half, 0f, 1.2f, 4f) : new Rect(half - 1.2f, 0f, 1.2f, 4f);
                int side = chargeSide;
                boss.Stage.Telegraph(TellKind.EdgeGlow, edge, T.sootChargeTell, () => StartCharge(boss, side));
            }

            float OffscreenX => Tuning.I.HalfWidth + HalfBody + 0.5f;

            /// <summary>It comes in low from the edge that glowed and runs the whole roof.</summary>
            void StartCharge(Boss boss, int side)
            {
                Charges++;
                moveDir = side < 0 ? 1 : -1;
                Underground = false;
                boss.Hidden = false;
                SetPose(boss, ChargePose, moveDir);
                boss.SetPosition(-moveDir * OffscreenX, T.sootChargeHeight * 0.5f);
                state = State.Charging;
            }

            void Charge(Boss boss, float dt, float speedScale)
            {
                float x = boss.Centre.x + moveDir * T.sootChargeSpeed * boss.HardSpeedScale * speedScale * dt;
                life += dt;
                boss.SetPosition(x, T.sootChargeHeight * 0.5f + Mathf.Abs(Mathf.Sin(life * 14f)) * 0.08f);
                boss.Tilt = -4f; // leans into the charge (mirrored when it runs left)
                if (moveDir * x < OffscreenX) return;
                boss.Tilt = 0f;
                SetPose(boss, WalkPose, 1);
                GoUnder(boss);
                nextIsPop = true;
            }
        }
    }
}
