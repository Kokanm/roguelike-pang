using System;
using System.Collections.Generic;
using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// One hand-built stage. Each stage is a sealed [Preserve] subclass in its own file under Stages/W1 to W6.
    /// All finds those classes itself, the same way WeaponSpec finds weapons, so two stages never share a list.
    /// Rows are 14 per storey, top row first, 32 characters each. Ground is the floor under row 0.
    /// </summary>
    public abstract class StageSpec
    {
        public abstract string Id { get; }
        public abstract string Name { get; }
        /// <summary>World 1 to 6.</summary>
        public abstract int World { get; }
        /// <summary>1 to 4. Slot 1 of a world is the difficulty 1 intro.</summary>
        public abstract int Difficulty { get; }
        public virtual int MinStageInWorld => 1;
        public virtual int MaxStageInWorld => 5;
        public virtual bool CanMirror => true;
        public virtual float TimeScale => 1f;
        public abstract string Concept { get; }
        /// <summary>1 to 3 full screens; a boss arena may have up to 5 (<see cref="StageMap.MaxStoreys"/>).</summary>
        public virtual int Storeys => 1;
        /// <summary>
        /// True for a boss arena: a stage a BossDef fights in. It is never picked for an ordinary slot and is not in
        /// <see cref="All"/>; find it with <see cref="BossArenas"/> or <see cref="ById"/>. It may have up to 5 storeys,
        /// has no required enemies, and on a multi-storey arena a hatch opens when a boss phase ends.
        /// </summary>
        public virtual bool IsBossArena => false;
        /// <summary>Most storeys an ordinary stage may have.</summary>
        public const int MaxOrdinaryStoreys = 3;
        /// <summary>Most storeys this stage may have: 3 for an ordinary stage, 5 for a boss arena.</summary>
        public int StoreyCap => IsBossArena ? StageMap.MaxStoreys : MaxOrdinaryStoreys;
        public abstract string[] Rows { get; }
        public abstract string Ground { get; }

        protected abstract void Populate(StageBuilder b);

        /// <summary>Open rows of dots, top row first. One shared string would alias every row, so each row is its own.</summary>
        protected static string[] BlankRows(int storeys = 1)
        {
            int n = StageMap.StoreyRows * Mathf.Clamp(storeys, 1, StageMap.MaxStoreys);
            var rows = new string[n];
            for (int i = 0; i < n; i++) rows[i] = new string('.', StageMap.Cols);
            return rows;
        }

        protected static string SolidGround => new string('=', StageMap.Cols);

        /// <summary>The stage as a plan. mirror flips the grid, the ground and every placed thing left to right.</summary>
        public StagePlan Build(bool mirror = false)
        {
            string[] rows = mirror ? Mirror(Rows) : Rows;
            string ground = mirror && Ground != null ? Reverse(Ground) : Ground;
            var map = StageLayout.Parse(rows, ground, World == 3 ? Surface.Sand : Surface.Normal);
            var builder = new StageBuilder(mirror);
            Populate(builder);
            return builder.ToPlan(this, map);
        }

        static string[] Mirror(string[] rows)
        {
            if (rows == null) return null;
            var copy = new string[rows.Length];
            for (int i = 0; i < rows.Length; i++)
                copy[i] = rows[i] == null ? null : Reverse(rows[i]);
            return copy;
        }

        static string Reverse(string line)
        {
            var chars = line.ToCharArray();
            Array.Reverse(chars);
            return new string(chars);
        }

        static StageSpec[] all, arenas;

        /// <summary>Every ordinary stage, sorted by world, difficulty and id. Boss arenas are in <see cref="BossArenas"/>.</summary>
        public static IReadOnlyList<StageSpec> All => all ??= Discover(false);

        /// <summary>Every boss arena (IsBossArena), sorted by world and id.</summary>
        public static IReadOnlyList<StageSpec> BossArenas => arenas ??= Discover(true);

        /// <summary>An ordinary stage or a boss arena by id, or null.</summary>
        public static StageSpec ById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var stage in All)
                if (stage.Id == id) return stage;
            foreach (var stage in BossArenas)
                if (stage.Id == id) return stage;
            return null;
        }

        static StageSpec[] Discover(bool bossArenas)
        {
            var list = new List<StageSpec>();
            foreach (var type in typeof(StageSpec).Assembly.GetTypes())
            {
                if (type.IsAbstract || !type.IsSealed || !typeof(StageSpec).IsAssignableFrom(type)) continue;
                if (type.GetConstructor(Type.EmptyTypes) == null) continue;
                var spec = (StageSpec)Activator.CreateInstance(type);
                if (spec.IsBossArena == bossArenas) list.Add(spec);
            }
            list.Sort((a, b) =>
            {
                int world = a.World.CompareTo(b.World);
                if (world != 0) return world;
                int difficulty = a.Difficulty.CompareTo(b.Difficulty);
                if (difficulty != 0) return difficulty;
                return string.CompareOrdinal(a.Id, b.Id);
            });
            return list.ToArray();
        }
    }

    /// <summary>
    /// Places Wick, bubbles, rollers, spikes and waves while a stage is built. Positions are in author space.
    /// Build(mirror: true) flips them after Populate returns.
    /// </summary>
    public sealed class StageBuilder
    {
        public const int TierXL = 0, TierL = 1, TierM = 2, TierS = 3;

        readonly bool mirror;
        Vector2 wick;
        readonly List<SpawnSpec> spawns = new List<SpawnSpec>();
        readonly List<SpawnSpec> steelPogos = new List<SpawnSpec>();
        readonly List<RollerSpec> rollers = new List<RollerSpec>();
        readonly List<StageMap.SpikeStrip> spikes = new List<StageMap.SpikeStrip>();
        readonly List<List<SpawnSpec>> waves = new List<List<SpawnSpec>>();
        int waveStorey = -1;

        internal StageBuilder(bool mirror) { this.mirror = mirror; }

        public void Wick(float x, float y = 0f) => wick = new Vector2(X(x), y);

        public void WickAtCol(int col, int row = 0) => Wick(StageMap.CenterX(col), row);

        public void Bubble(BubbleKind kind, int tier, float x, float y, int dir = 1, bool ceramic = false, bool pogo = false, bool steel = false)
        {
            var spawn = Make(kind, tier, x, y, dir, ceramic, pogo, steel, ClingerMount.Ceiling, false);
            if (steel && pogo) steelPogos.Add(spawn);
            else spawns.Add(spawn);
        }

        public void BubbleAt(BubbleKind kind, int tier, int col, float y, int dir = 1, bool ceramic = false, bool pogo = false) =>
            Bubble(kind, tier, StageMap.CenterX(col), y, dir, ceramic, pogo, false);

        public void Clinger(int tier, float x, float y, ClingerMount mount = ClingerMount.Ceiling, int dir = 1, bool pogo = false, bool ceramic = false, bool patient = true) =>
            spawns.Add(Make(BubbleKind.Clinger, tier, x, y, dir, ceramic, pogo, false, mount, patient));

        public void FloorClinger(int tier, float x, float surfaceY = 0f, int dir = 1, bool pogo = true, bool ceramic = false, bool patient = true)
        {
            float y = surfaceY + Radius(tier);
            Clinger(tier, x, y, ClingerMount.Floor, dir, pogo, ceramic, patient);
        }

        public void SteelPogo(int tier, float x, float surfaceY = 0f)
        {
            steelPogos.Add(Make(BubbleKind.Gloop, tier, x, surfaceY + Radius(tier), 1, false, true, true, ClingerMount.Ceiling, false));
        }

        public void Roller(float x, float surfaceY = 0f, int dir = 1, int tier = 0, bool ceramic = false, bool steel = false)
        {
            rollers.Add(new RollerSpec
            {
                tier = tier,
                x = X(x),
                surfaceY = surfaceY,
                direction = Dir(dir),
                ceramic = ceramic,
                steel = steel,
            });
        }

        /// <summary>col is the left column. Mirroring uses 32 - col - width, so a strip keeps its width.</summary>
        public void Spike(int col, int width, int row = -1, float? phase = null)
        {
            int placed = mirror ? StageMap.Cols - col - width : col;
            spikes.Add(new StageMap.SpikeStrip
            {
                col = placed,
                row = row,
                width = width,
                phase = phase ?? spikes.Count * Tuning.I.spikePhaseStep,
            });
        }

        public void Wave(int waveIndex, BubbleKind kind, int tier, float x, int dir = 1, bool ceramic = false, bool pogo = false)
        {
            while (waves.Count <= waveIndex) waves.Add(new List<SpawnSpec>());
            if (waveIndex >= 0)
                waves[waveIndex].Add(Make(kind, tier, x, 0f, dir, ceramic, pogo, false, ClingerMount.Ceiling, false));
        }

        public void WaveStorey(int storey) => waveStorey = storey;

        internal StagePlan ToPlan(StageSpec spec, StageMap map)
        {
            foreach (var spike in spikes) map.spikes.Add(spike);
            var plan = new StagePlan
            {
                map = map,
                spawns = spawns,
                rollers = rollers,
                steelPogos = steelPogos,
                waves = waves,
                wickPosition = wick,
                waveStorey = waveStorey,
                layoutName = spec.Name,
                stageId = spec.Id,
                difficulty = spec.Difficulty,
                concept = spec.Concept,
                timeScale = spec.TimeScale,
                guestName = GuestName(spec.World),
                isBossArena = spec.IsBossArena,
            };
            return plan;
        }

        string GuestName(int world)
        {
            string guest = null;
            foreach (var spawn in spawns)
            {
                if (!IsGuest(world, spawn.kind)) continue;
                string name = spawn.kind.ToString();
                if (guest == null) guest = name;
                else if (guest != name) return null;
            }
            return guest;
        }

        static bool IsGuest(int world, BubbleKind kind)
        {
            int home = HomeWorld(kind);
            return home > 0 && home < world;
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

        SpawnSpec Make(BubbleKind kind, int tier, float x, float y, int dir, bool ceramic, bool pogo, bool steel, ClingerMount mount, bool patient) =>
            new SpawnSpec
            {
                kind = kind,
                tier = tier,
                position = new Vector2(X(x), y),
                direction = Dir(dir),
                ceramic = ceramic,
                pogo = pogo,
                steel = steel,
                mount = MirrorMount(mount),
                patient = patient,
            };

        float X(float x) => mirror ? -x : x;
        int Dir(int dir) => mirror ? -dir : dir;

        ClingerMount MirrorMount(ClingerMount mount)
        {
            if (!mirror) return mount;
            if (mount == ClingerMount.LeftWall) return ClingerMount.RightWall;
            if (mount == ClingerMount.RightWall) return ClingerMount.LeftWall;
            return mount;
        }

        static float Radius(int tier)
        {
            var tiers = Tuning.I.tiers;
            int index = Mathf.Clamp(tier, 0, tiers.Length - 1);
            return tiers[index].diameter * 0.5f;
        }
    }
}
