using System;
using System.Collections.Generic;
using UnityEngine;

namespace Bubblewright
{
    public enum TargetKind { Bubble, Roller, Boss, Bullet }

    /// <summary>One thing a weapon query found. Exactly one of bubble, roller, boss or bullet is set.</summary>
    public struct WeaponTarget
    {
        public TargetKind kind;
        public Bubble bubble;
        public Roller roller;
        public Boss boss;
        public Bullet bullet;
        /// <summary>Its centre (the closest point of the body, for the boss).</summary>
        public Vector2 position;
        /// <summary>Distance from the query's origin. Query results are sorted by it, nearest first.</summary>
        public float distance;
    }

    /// <summary>
    /// The shared rules every weapon must use, so steel, ceramic, Ghosts, bosses and tiles behave the same for all of
    /// them: the hit helpers, the boss damage that adds up fractions, the area queries and the live Shots.
    /// Game builds one and hands it its lists and its hit methods; tests build one with their own.
    /// </summary>
    public class WeaponWorld
    {
        public readonly List<Bubble> Bubbles;
        public readonly List<Roller> Rollers;
        public readonly List<Bullet> Bullets;
        readonly Func<Boss> boss;

        /// <summary>A bubble that can be hit was hit: Game.Hit (cracks ceramic, or swells and pops it).</summary>
        public Action<Bubble> OnHitBubble;
        /// <summary>A rolling balloon that is not steel was hit: Game.HitRoller.</summary>
        public Action<Roller> OnHitRoller;
        /// <summary>Whole boss damage: Game.DamageBoss.</summary>
        public Action<int, Vector2> OnDamageBoss;
        /// <summary>A breakable tile was hit once: Game.HitTile.</summary>
        public Action<int, int> OnHitTile;
        /// <summary>Something steel was hit: a sparkle and the bounce sound, nothing else.</summary>
        public Action<Vector2> OnSteel;

        readonly Transform shotRoot;
        readonly List<Shot> shots = new List<Shot>();
        readonly Stack<Shot> pool = new Stack<Shot>();
        float bossCarry;

        public WeaponWorld(List<Bubble> bubbles, List<Roller> rollers, List<Bullet> bullets, Func<Boss> boss, Transform shotRoot)
        {
            Bubbles = bubbles ?? new List<Bubble>();
            Rollers = rollers ?? new List<Roller>();
            Bullets = bullets ?? new List<Bullet>();
            this.boss = boss;
            this.shotRoot = shotRoot;
        }

        public Boss Boss => boss?.Invoke();
        /// <summary>A boss that can still take damage.</summary>
        public Boss LiveBoss
        {
            get
            {
                var b = Boss;
                return b != null && b.Hp > 0 && !b.Dying ? b : null;
            }
        }
        public StageMap Map => Game.Map;
        public IReadOnlyList<Shot> Shots => shots;
        /// <summary>Boss damage waiting to add up to a whole hit.</summary>
        public float BossCarry => bossCarry;

        // ------------------------------------------------------------------ hit helpers

        /// <summary>
        /// Steel sparkles and returns false. A bubble that cannot be hit (popped, or a faded Ghost) returns false
        /// silently. Anything else takes one hit (a ceramic one cracks) and returns true.
        /// </summary>
        public bool WeaponHit(Bubble b)
        {
            if (b == null || b.Popped) return false;
            if (b.Steel)
            {
                OnSteel?.Invoke(b.Position);
                return false;
            }
            if (!b.CanBeHit) return false;
            OnHitBubble?.Invoke(b);
            return true;
        }

        /// <summary>The same rules for a rolling balloon: steel sparkles, ceramic cracks, the rest pop.</summary>
        public bool WeaponHit(Roller r)
        {
            if (r == null || r.Popped) return false;
            if (r.Steel)
            {
                OnSteel?.Invoke(r.Position);
                return false;
            }
            OnHitRoller?.Invoke(r);
            return true;
        }

        /// <summary>Adds boss damage, fractions included, and passes on every whole point as it adds up.</summary>
        public void WeaponHitBoss(float amount, Vector2 at)
        {
            if (amount <= 0f || LiveBoss == null) return;
            bossCarry += amount;
            int whole = Mathf.FloorToInt(bossCarry + 0.0001f);
            if (whole <= 0) return;
            bossCarry = Mathf.Max(0f, bossCarry - whole);
            OnDamageBoss?.Invoke(whole, at);
        }

        /// <summary>Hits whatever a query returned with the helpers above. True when it did damage.</summary>
        public bool Hit(WeaponTarget t, float bossDamage)
        {
            switch (t.kind)
            {
                case TargetKind.Bubble: return WeaponHit(t.bubble);
                case TargetKind.Roller: return WeaponHit(t.roller);
                case TargetKind.Boss:
                    if (LiveBoss == null) return false;
                    WeaponHitBoss(bossDamage, t.position);
                    return true;
                case TargetKind.Bullet:
                    if (t.bullet == null || t.bullet.Dead) return false;
                    t.bullet.Dead = true;
                    return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ area queries
        // Every query lists everything inside it, steel and faded Ghosts too, nearest first. Popped things are left out.

        public List<WeaponTarget> InCircle(Vector2 centre, float radius, List<WeaponTarget> into = null)
        {
            return Collect(centre, into, (p, r) =>
            {
                float d = Vector2.Distance(p, centre);
                return d <= radius + r ? Mathf.Max(0f, d - r) : -1f;
            }, body =>
            {
                Vector2 q = Closest(body, centre);
                float d = Vector2.Distance(q, centre);
                return d <= radius ? d : -1f;
            });
        }

        /// <summary>A slice of a circle: radius around centre, arcDegrees wide, centred on the facing direction.</summary>
        public List<WeaponTarget> InSector(Vector2 centre, float radius, Vector2 facing, float arcDegrees, List<WeaponTarget> into = null)
        {
            float half = arcDegrees * 0.5f;
            if (facing.sqrMagnitude < 1e-6f) facing = Vector2.up;
            return Collect(centre, into, (p, r) =>
            {
                Vector2 to = p - centre;
                float d = to.magnitude;
                if (d > radius + r) return -1f;
                if (d <= r) return 0f;
                float slack = Mathf.Asin(Mathf.Clamp01(r / d)) * Mathf.Rad2Deg;
                return Vector2.Angle(facing, to) <= half + slack ? d - r : -1f;
            }, body =>
            {
                if (body.Contains(centre)) return 0f;
                Vector2 q = Closest(body, centre);
                float d = Vector2.Distance(q, centre);
                if (d > radius) return -1f;
                // Inside the arc if its closest point or any corner within reach is.
                if (Vector2.Angle(facing, q - centre) <= half) return d;
                foreach (var c in Corners(body))
                    if (Vector2.Distance(c, centre) <= radius && Vector2.Angle(facing, c - centre) <= half) return d;
                return -1f;
            });
        }

        /// <summary>A thick line from a to b, halfWidth either side. Distances are measured from a.</summary>
        public List<WeaponTarget> AlongSegment(Vector2 a, Vector2 b, float halfWidth, List<WeaponTarget> into = null)
        {
            return Collect(a, into, (p, r) =>
            {
                Vector2 q = ClosestOnSegment(a, b, p);
                return Vector2.Distance(q, p) <= halfWidth + r ? Vector2.Distance(a, q) : -1f;
            }, body =>
            {
                var grown = new Rect(body.xMin - halfWidth, body.yMin - halfWidth, body.width + 2f * halfWidth, body.height + 2f * halfWidth);
                return SegmentEntry(a, b, grown, out float t) ? t * Vector2.Distance(a, b) : -1f;
            });
        }

        /// <summary>circle(p, r) returns the distance when inside, or below 0 when not; rect(body) the same for the boss.</summary>
        List<WeaponTarget> Collect(Vector2 origin, List<WeaponTarget> into, Func<Vector2, float, float> circle, Func<Rect, float> rect)
        {
            into ??= new List<WeaponTarget>();
            into.Clear();
            foreach (var b in Bubbles)
            {
                if (b == null || b.Popped) continue;
                float d = circle(b.Position, b.Radius);
                if (d >= 0f) into.Add(new WeaponTarget { kind = TargetKind.Bubble, bubble = b, position = b.Position, distance = d });
            }
            foreach (var r in Rollers)
            {
                if (r == null || r.Popped) continue;
                float d = circle(r.Position, r.Radius);
                if (d >= 0f) into.Add(new WeaponTarget { kind = TargetKind.Roller, roller = r, position = r.Position, distance = d });
            }
            foreach (var bl in Bullets)
            {
                if (bl == null || bl.Dead) continue;
                float d = circle(bl.Position, bl.Radius);
                if (d >= 0f) into.Add(new WeaponTarget { kind = TargetKind.Bullet, bullet = bl, position = bl.Position, distance = d });
            }
            var live = LiveBoss;
            if (live != null)
            {
                float d = rect(live.Body);
                if (d >= 0f) into.Add(new WeaponTarget { kind = TargetKind.Boss, boss = live, position = Closest(live.Body, origin), distance = d });
            }
            into.Sort((x, y) => x.distance.CompareTo(y.distance));
            return into;
        }

        static Vector2 Closest(Rect r, Vector2 p) => new Vector2(Mathf.Clamp(p.x, r.xMin, r.xMax), Mathf.Clamp(p.y, r.yMin, r.yMax));

        static IEnumerable<Vector2> Corners(Rect r)
        {
            yield return new Vector2(r.xMin, r.yMin);
            yield return new Vector2(r.xMax, r.yMin);
            yield return new Vector2(r.xMin, r.yMax);
            yield return new Vector2(r.xMax, r.yMax);
        }

        public static Vector2 ClosestOnSegment(Vector2 a, Vector2 b, Vector2 p)
        {
            Vector2 ab = b - a;
            float len2 = ab.sqrMagnitude;
            if (len2 < 1e-8f) return a;
            return a + ab * Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
        }

        /// <summary>Where (0 to 1 along a to b) the segment first enters the rectangle, if it does.</summary>
        static bool SegmentEntry(Vector2 a, Vector2 b, Rect r, out float tEnter)
        {
            tEnter = 0f;
            float tExit = 1f;
            Vector2 d = b - a;
            for (int axis = 0; axis < 2; axis++)
            {
                float p = axis == 0 ? a.x : a.y, dir = axis == 0 ? d.x : d.y;
                float min = axis == 0 ? r.xMin : r.yMin, max = axis == 0 ? r.xMax : r.yMax;
                if (Mathf.Abs(dir) < 1e-8f)
                {
                    if (p < min || p > max) return false;
                    continue;
                }
                float t0 = (min - p) / dir, t1 = (max - p) / dir;
                if (t0 > t1) (t0, t1) = (t1, t0);
                tEnter = Mathf.Max(tEnter, t0);
                tExit = Mathf.Min(tExit, t1);
                if (tEnter > tExit) return false;
            }
            return true;
        }

        // ------------------------------------------------------------------ shots

        /// <summary>Fires a shared spark bullet. Velocity in units per second; range in units (infinite by default).</summary>
        public Shot FireShot(Vector2 from, Vector2 velocity, float bossDamage, float range = float.PositiveInfinity)
        {
            var s = pool.Count > 0 ? pool.Pop() : NewShot();
            s.Position = from;
            s.Velocity = velocity;
            s.BossDamage = bossDamage;
            s.Range = range;
            s.Travelled = 0f;
            s.Ignores = null;
            s.Alive = true;
            shots.Add(s);
            s.Show();
            return s;
        }

        Shot NewShot()
        {
            var s = new Shot();
            s.go = new GameObject("Shot");
            if (shotRoot != null) s.go.transform.SetParent(shotRoot, false);
            s.sr = s.go.AddComponent<SpriteRenderer>();
            s.sr.sprite = WeaponArt.ShotSprite;
            Sorting.Apply(s.sr, Layer.Shots, 1);
            Game.ApplySpriteMaterial(s.sr);
            return s;
        }

        /// <summary>Moves every live shot in small steps so it never skips over a small bubble or a tile.</summary>
        public void TickShots(float dt)
        {
            var t = Tuning.I;
            for (int i = shots.Count - 1; i >= 0; i--)
            {
                var s = shots[i];
                float dist = s.Velocity.magnitude * dt;
                int steps = Mathf.Max(1, Mathf.CeilToInt(dist / 0.2f));
                Vector2 step = s.Velocity * (dt / steps);
                for (int k = 0; k < steps && s.Alive; k++)
                {
                    s.Position += step;
                    s.Travelled += step.magnitude;
                    if (s.Travelled > s.Range || s.Position.x < -t.HalfWidth || s.Position.x > t.HalfWidth ||
                        s.Position.y > t.arenaHeight || s.Position.y < 0f)
                    {
                        s.Alive = false;
                        break;
                    }
                    if (StopsAtTile(s) || HitsSomething(s)) s.Alive = false;
                }
                s.Show();
                if (!s.Alive)
                {
                    shots.RemoveAt(i);
                    pool.Push(s);
                }
            }
        }

        bool StopsAtTile(Shot s)
        {
            var map = Map;
            if (map == null) return false;
            int c = StageMap.Col(s.Position.x), r = StageMap.Row(s.Position.y);
            if (!map.IsSolid(c, r)) return false;
            if (map.Get(c, r).kind == TileKind.Breakable) OnHitTile?.Invoke(c, r);
            return true;
        }

        /// <summary>The first bubble, rolling balloon or boss it touches. Steel stops it; a faded Ghost lets it through.</summary>
        bool HitsSomething(Shot s)
        {
            Rect box = s.Box;
            for (int i = 0; i < Bubbles.Count; i++)
            {
                var b = Bubbles[i];
                if (b == null || b.Popped || !b.Overlaps(box)) continue;
                if (s.Ignores != null && s.Ignores(b)) continue;
                if (b.Steel) { WeaponHit(b); return true; }
                if (!b.CanBeHit) continue;
                WeaponHit(b);
                return true;
            }
            for (int i = 0; i < Rollers.Count; i++)
            {
                var r = Rollers[i];
                if (r == null || r.Popped || !r.Overlaps(box)) continue;
                if (s.Ignores != null && s.Ignores(r)) continue;
                WeaponHit(r);
                return true;
            }
            var live = LiveBoss;
            if (live != null && live.Body.Overlaps(box))
            {
                WeaponHitBoss(s.BossDamage, s.Position);
                return true;
            }
            return false;
        }

        /// <summary>Removes every live shot (stage start, stage clear). Boss damage still adding up is dropped too.</summary>
        public void ClearShots()
        {
            foreach (var s in shots)
            {
                s.Alive = false;
                s.Show();
                pool.Push(s);
            }
            shots.Clear();
            bossCarry = 0f;
        }

        /// <summary>Destroys every shot object, pooled ones too (tests).</summary>
        public void Destroy()
        {
            ClearShots();
            while (pool.Count > 0)
            {
                var s = pool.Pop();
                if (s.go == null) continue;
                if (Application.isPlaying) UnityEngine.Object.Destroy(s.go);
                else UnityEngine.Object.DestroyImmediate(s.go);
            }
        }
    }
}
