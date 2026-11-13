using System.Collections.Generic;
using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// A rolling balloon: a pearl ball that rolls at a constant speed. On the ground it turns around at walls, solid
    /// tiles, gaps and other rollers. On a platform it rolls around the whole platform: over the end, along the
    /// underside, up the other end and back on top, always touching the tiles. It turns around at the arena walls and
    /// ceiling and at other rollers. If the tile it rests on is destroyed it falls with fallGravity to the next
    /// surface and carries on. It ignores ice, sand and spikes. Wick jumps over it or shoots it; a big one splits into two
    /// small ones rolling apart. Game ticks it, so hit-stop, slow motion and pause freeze it. No physics.
    /// Pivot at the centre.
    /// </summary>
    public class Roller : MonoBehaviour
    {
        public const int Frames = 4;
        /// <summary>The bands step to the next frame every this many pixels travelled.</summary>
        public const int PixelsPerFrame = 3;

        /// <summary>0 is big, 1 is small.</summary>
        public int Tier { get; private set; }
        /// <summary>
        /// +1 rolls right on the ground and clockwise around a platform (right along its top, down its right end, left
        /// along its underside); -1 is the other way.
        /// </summary>
        public int Direction { get; set; }
        /// <summary>True while it rolls on the ground; false while it rolls around a platform.</summary>
        public bool OnGround { get; private set; }
        /// <summary>Points from what it touches to its centre: up on top of a platform, down on the underside.</summary>
        public Vector2 Normal { get; private set; } = Vector2.up;
        /// <summary>The way it is rolling right now.</summary>
        public Vector2 Tangent => OnGround ? new Vector2(Direction, 0f) : Direction * new Vector2(Normal.y, -Normal.x);
        public bool Popped { get; private set; }
        public bool Falling { get; private set; }
        /// <summary>The frame of the rolling bands, 0 to 3.</summary>
        public int Frame { get; private set; }

        Tuning T => Tuning.I;
        SpriteRenderer sr;
        float fallSpeed;
        float travel; // signed: rolling right adds, rolling left takes away, so the bands turn back with the direction

        public static int TierCount => Mathf.Max(1, Tuning.I.rollerDiameters.Length);
        static int ClampTier(int tier) => Mathf.Clamp(tier, 0, TierCount - 1);

        public static float RadiusOf(int tier) => Tuning.I.rollerDiameters[ClampTier(tier)] * 0.5f;
        public static float SpeedOf(int tier)
        {
            var s = Tuning.I.rollerSpeeds;
            return s.Length == 0 ? 0f : s[Mathf.Clamp(tier, 0, s.Length - 1)];
        }

        public static int ScoreOf(int tier)
        {
            var s = Tuning.I.rollerScores;
            return s.Length == 0 ? 0 : s[Mathf.Clamp(tier, 0, s.Length - 1)];
        }

        public float Radius => RadiusOf(Tier);
        public float Speed => SpeedOf(Tier);
        public Vector2 Position => transform.position;
        /// <summary>The surface it rests on (or the bottom of the ball while it falls).</summary>
        public float BottomY => transform.position.y - Radius;

        /// <summary>A roller resting on the surface at height surfaceY (0 is the ground, 5 is platform row A), centred on x.</summary>
        public static Roller Create(Transform parent, int tier, float x, float surfaceY, int direction)
        {
            tier = ClampTier(tier);
            var go = new GameObject(tier == 0 ? "Roller big" : "Roller small");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector2(x, surfaceY + RadiusOf(tier));
            var r = go.AddComponent<Roller>();
            r.Tier = tier;
            r.Direction = direction >= 0 ? 1 : -1;
            r.OnGround = surfaceY <= 0.01f;
            r.sr = go.AddComponent<SpriteRenderer>();
            Sorting.Apply(r.sr, Layer.Bubbles, 6 + tier);
            Game.ApplySpriteMaterial(r.sr);
            r.ApplyVisual();
            return r;
        }

        static StageMap flat;
        static StageMap MapOrFlat => Game.Map ?? (flat ?? (flat = StageMap.Flat()));

        const float MaxSubStep = 0.1f;   // a ball never moves more than this between collision checks
        const float SupportSlack = 0.05f; // further than radius + this from every tile and it has nothing to roll on

        /// <summary>One step. others is the list of every roller on the stage (it may contain this one).</summary>
        public void Tick(float dt, IList<Roller> others = null)
        {
            if (Popped || dt <= 0f) return;
            var map = MapOrFlat;
            if (Falling) TickFall(map, dt);
            else if (OnGround) TickGround(map, dt, others);
            else TickCrawl(map, dt, others);
            ApplyVisual();
        }

        // ------------------------------------------------------------------ ground

        void TickGround(StageMap map, float dt, IList<Roller> others)
        {
            float r = Radius;
            Vector2 p = transform.position;
            float bottom = p.y - r;
            float step = Speed * dt;
            if (Blocked(map, p.x, bottom, r, Direction, step, others, out Roller met))
            {
                Direction = -Direction;
                // Two rollers that meet both turn, so the second one does not chase the first.
                if (met != null && met.Direction == Direction) met.Direction = -Direction;
                if (Blocked(map, p.x, bottom, r, Direction, step, others, out _)) step = 0f; // boxed in: wait until something changes
            }
            p.x += Direction * step;
            travel += Direction * step;
            transform.position = new Vector2(p.x, bottom + r);
        }

        /// <summary>
        /// True when moving step units toward dir would take the ball into a wall or a solid tile, into a gap in the
        /// ground, or into another roller ahead of it on the ground.
        /// </summary>
        bool Blocked(StageMap map, float x, float bottom, float r, int dir, float step, IList<Roller> others, out Roller met)
        {
            met = null;
            float nx = x + dir * step;
            float lead = nx + dir * r;
            float half = StageMap.Cols * 0.5f;
            if (lead <= -half || lead >= half) return true;

            int c = StageMap.Col(lead - dir * 0.0001f);
            int rMin = StageMap.Row(bottom + 0.0001f);
            int rMax = StageMap.Row(bottom + 2f * r - 0.0001f);
            for (int row = rMin; row <= rMax; row++)
                if (map.IsSolid(c, row)) return true;
            if (!HasFloor(map, c, bottom)) return true;

            if (others != null)
            {
                for (int i = 0; i < others.Count; i++)
                {
                    var o = others[i];
                    if (o == null || o == this || o.Popped || o.Falling || !o.OnGround) continue;
                    float dx = o.Position.x - nx;
                    if (dx * dir > 0f && Mathf.Abs(dx) < r + o.Radius)
                    {
                        met = o;
                        return true;
                    }
                }
            }
            return false;
        }

        // ------------------------------------------------------------------ around a platform

        /// <summary>Distance from q to the nearest solid tile within range (infinity when none), and that tile's nearest point.</summary>
        static float Nearest(StageMap map, Vector2 q, float range, out Vector2 point)
        {
            float best = float.PositiveInfinity;
            point = q;
            int c0 = Mathf.Max(0, StageMap.Col(q.x - range)), c1 = Mathf.Min(StageMap.Cols - 1, StageMap.Col(q.x + range));
            int r0 = Mathf.Max(0, StageMap.Row(q.y - range)), r1 = Mathf.Min(StageMap.Rows - 1, StageMap.Row(q.y + range));
            for (int c = c0; c <= c1; c++)
                for (int row = r0; row <= r1; row++)
                {
                    if (!map.IsSolid(c, row)) continue;
                    Rect rect = StageMap.CellRect(c, row);
                    var near = new Vector2(Mathf.Clamp(q.x, rect.xMin, rect.xMax), Mathf.Clamp(q.y, rect.yMin, rect.yMax));
                    float d = (q - near).magnitude;
                    if (d < best)
                    {
                        best = d;
                        point = near;
                    }
                }
            return best;
        }

        /// <summary>
        /// Rolls around the tiles it touches: a step along the tangent, then back out to exactly one radius from the
        /// nearest tile. Past a convex corner that traces an arc around the corner; at a concave corner the push out of
        /// the blocking tile turns it up that wall. It turns at the arena walls and ceiling and at other rollers, and
        /// it falls if there is no tile within reach any more.
        /// </summary>
        void TickCrawl(StageMap map, float dt, IList<Roller> others)
        {
            float r = Radius;
            float half = StageMap.Cols * 0.5f;
            Vector2 p = transform.position;
            float total = Speed * dt;
            int n = Mathf.Max(1, Mathf.CeilToInt(total / MaxSubStep));
            float sub = total / n;

            for (int i = 0; i < n; i++)
            {
                if (TouchesRollerAhead(p, r, others, out Roller met))
                {
                    Direction = -Direction;
                    if (met != null && !met.OnGround && Vector2.Dot(met.Tangent, Tangent) < 0f) met.Direction = -met.Direction; // head-on: both turn
                    continue;
                }

                Vector2 q = p + Tangent * sub;
                if (q.x - r < -half || q.x + r > half || q.y + r > StageMap.Rows)
                {
                    Direction = -Direction; // the arena wall or ceiling is in the way
                    continue;
                }
                if (Nearest(map, q, r + 0.5f, out _) > r + SupportSlack)
                {
                    Falling = true; // the tile under it is gone
                    fallSpeed = 0f;
                    break;
                }

                Vector2 push = Vector2.zero;
                bool pushed = false;
                for (int pass = 0; pass < 3; pass++)
                {
                    float d = Nearest(map, q, r + 0.5f, out Vector2 np);
                    if (d >= r - 0.0001f) break;
                    Vector2 away = q - np;
                    away = away.sqrMagnitude < 1e-8f ? Normal : away.normalized;
                    q = np + away * r;
                    push = away;
                    pushed = true;
                }
                float dist = Nearest(map, q, r + 0.5f, out Vector2 near);
                if (dist < r - 0.02f)
                {
                    Direction = -Direction; // wedged in a gap too narrow for it: back out
                    continue;
                }

                Vector2 n2 = pushed ? push : (q - near).sqrMagnitude > 1e-8f ? (q - near).normalized : Normal;
                p = q;
                Normal = n2;
                travel += Direction * sub;

                if (p.y - r <= 0.01f)
                {
                    // Down the side of something onto the ground: roll on along the ground, away from it.
                    p.y = r;
                    OnGround = true;
                    Normal = Vector2.up;
                    if (Mathf.Abs(n2.x) > 0.3f) Direction = n2.x > 0f ? 1 : -1;
                    break;
                }
            }
            transform.position = p;
        }

        bool TouchesRollerAhead(Vector2 p, float r, IList<Roller> others, out Roller met)
        {
            met = null;
            if (others == null) return false;
            Vector2 tan = Tangent;
            for (int i = 0; i < others.Count; i++)
            {
                var o = others[i];
                if (o == null || o == this || o.Popped || o.Falling) continue;
                Vector2 to = o.Position - p;
                if (to.magnitude >= r + o.Radius || Vector2.Dot(to, tan) <= 0f) continue;
                met = o;
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ falling

        /// <summary>Straight down with fallGravity until it touches a tile (then it rolls around it) or the ground.</summary>
        void TickFall(StageMap map, float dt)
        {
            float r = Radius;
            Vector2 p = transform.position;
            fallSpeed = Mathf.Min(fallSpeed + T.fallGravity * dt, T.maxFallSpeed);
            float total = fallSpeed * dt;
            int n = Mathf.Max(1, Mathf.CeilToInt(total / MaxSubStep));
            float sub = total / n;
            for (int i = 0; i < n; i++)
            {
                p.y -= sub;
                if (p.y - r <= 0f)
                {
                    p.y = r;
                    Land(true, Vector2.up);
                    break;
                }
                float d = Nearest(map, p, r + 0.01f, out Vector2 np);
                if (d <= r)
                {
                    Vector2 away = p - np;
                    away = away.sqrMagnitude < 1e-8f ? Vector2.up : away.normalized;
                    p = np + away * r;
                    Land(false, away);
                    break;
                }
            }
            transform.position = p;
        }

        void Land(bool ground, Vector2 normal)
        {
            Falling = false;
            fallSpeed = 0f;
            OnGround = ground;
            Normal = normal;
        }

        /// <summary>Whether column c has something to roll on at height bottom: ground that is not a gap, or a solid tile top.</summary>
        static bool HasFloor(StageMap map, int c, float bottom)
        {
            if (c < 0 || c >= StageMap.Cols) return false;
            if (bottom <= 0.01f) return !map.IsGap(c);
            return map.IsSolid(c, StageMap.Row(bottom - 0.01f));
        }

        void ApplyVisual()
        {
            float stepUnits = PixelsPerFrame * T.Pixel;
            int f = Mathf.FloorToInt(travel / Mathf.Max(0.0001f, stepUnits));
            Frame = ((f % Frames) + Frames) % Frames;
            if (sr != null) sr.sprite = PlaceholderArt.I.RollerFrame(Tier, Frame);
            if (!Popped) transform.localScale = Vector3.one;
        }

        /// <summary>Hit: swell during the hit-stop, then the game pops it.</summary>
        public void Swell(float scale)
        {
            Popped = true;
            transform.localScale = new Vector3(scale, scale, 1f);
        }

        /// <summary>
        /// The rollers a popped roller leaves behind: a big one leaves two small ones on the same spot, rolling in
        /// opposite directions; a small one leaves none. The caller destroys the popped roller.
        /// </summary>
        public static List<Roller> Split(Roller popped, Transform parent)
        {
            var children = new List<Roller>(2);
            int next = popped.Tier + 1;
            if (next >= TierCount) return children;
            // Same spot, moved toward what the big one touched so the smaller ball still touches it.
            Vector2 centre = popped.Position - popped.Normal * (popped.Radius - RadiusOf(next));
            for (int i = 0; i < 2; i++)
            {
                var kid = Create(parent, next, centre.x, 0f, i == 0 ? -1 : 1);
                kid.transform.position = centre;
                kid.OnGround = popped.OnGround;
                kid.Normal = popped.Normal;
                kid.Falling = popped.Falling;
                children.Add(kid);
            }
            return children;
        }

        /// <summary>Circle vs axis-aligned rectangle.</summary>
        public bool Overlaps(Rect rect)
        {
            var c = Position;
            float cx = Mathf.Clamp(c.x, rect.xMin, rect.xMax);
            float cy = Mathf.Clamp(c.y, rect.yMin, rect.yMax);
            float dx = c.x - cx, dy = c.y - cy;
            float r = Radius;
            return dx * dx + dy * dy < r * r;
        }
    }
}
