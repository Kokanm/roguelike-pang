using System.Collections.Generic;
using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// A rolling balloon: a pearl ball that rolls along the ground or a platform at a constant speed. It never leaves
    /// its surface by itself: it turns around at walls and solid tiles, at the end of a platform, at a gap in the
    /// ground and at another roller. If the tile under it is destroyed it falls with fallGravity to the next surface
    /// and carries on. It ignores ice, sand and spikes. Wick jumps over it or shoots it; a big one splits into two
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
        /// <summary>+1 rolls right, -1 rolls left.</summary>
        public int Direction { get; set; }
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
            r.sr = go.AddComponent<SpriteRenderer>();
            Sorting.Apply(r.sr, Layer.Bubbles, 6 + tier);
            Game.ApplySpriteMaterial(r.sr);
            r.ApplyVisual();
            return r;
        }

        static StageMap flat;
        static StageMap MapOrFlat => Game.Map ?? (flat ?? (flat = StageMap.Flat()));

        /// <summary>One step. others is the list of every roller on the stage (it may contain this one).</summary>
        public void Tick(float dt, IList<Roller> others = null)
        {
            if (Popped || dt <= 0f) return;
            var map = MapOrFlat;
            float r = Radius;
            Vector2 p = transform.position;
            float bottom = p.y - r;

            if (!Falling && !SupportedUnderCentre(map, p.x, bottom))
            {
                Falling = true;
                fallSpeed = 0f;
            }

            if (Falling)
            {
                // Straight down to the next surface, then roll on in the same direction.
                fallSpeed = Mathf.Min(fallSpeed + T.fallGravity * dt, T.maxFallSpeed);
                float floor = Mathf.Max(0f, map.FloorBelow(p.x, bottom + 0.0001f));
                bottom -= fallSpeed * dt;
                if (bottom <= floor)
                {
                    bottom = floor;
                    Falling = false;
                    fallSpeed = 0f;
                }
                transform.position = new Vector2(p.x, bottom + r);
                ApplyVisual();
                return;
            }

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
            ApplyVisual();
        }

        /// <summary>
        /// True when moving step units toward dir would take the ball into a wall or a solid tile, past the end of
        /// the surface it rests on (its leading edge would hang over nothing), or into another roller ahead of it.
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
                    if (o == null || o == this || o.Popped || o.Falling) continue;
                    if (Mathf.Abs(o.BottomY - bottom) > 0.5f) continue; // on another surface
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

        /// <summary>Whether column c has something to roll on at height bottom: ground that is not a gap, or a solid tile top.</summary>
        static bool HasFloor(StageMap map, int c, float bottom)
        {
            if (c < 0 || c >= StageMap.Cols) return false;
            if (bottom <= 0.01f) return !map.IsGap(c);
            return map.IsSolid(c, StageMap.Row(bottom - 0.01f));
        }

        /// <summary>The ground always holds it (bubbles also bounce at y = 0 over a gap); a platform only while the tile under its centre stands.</summary>
        static bool SupportedUnderCentre(StageMap map, float x, float bottom)
        {
            if (bottom <= 0.01f) return true;
            return map.IsSolid(StageMap.Col(x), StageMap.Row(bottom - 0.01f));
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
            float x = popped.Position.x, surface = popped.BottomY;
            children.Add(Create(parent, next, x, surface, -1));
            children.Add(Create(parent, next, x, surface, 1));
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
