using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// The shape of a Pogo, shared by its sprites and its hitbox. A Pogo is a soft triangle: morph 0 points up (resting
    /// on its base), morph 1 points down. In between its weight flows through a rounded blob, so it never goes flat.
    /// The shape is described row by row: the half width at a height f (0 bottom, 1 top) as a share of the widest.
    /// </summary>
    public static class PogoShape
    {
        /// <summary>Height of the shape as a share of its width (an equilateral triangle).</summary>
        public const float HeightShare = 0.866f;

        /// <summary>Half width at height f (0 bottom, 1 top), as a share of the full half width, for morph k (0 up, 1 down).</summary>
        public static float HalfWidth01(float f, float k)
        {
            k = Mathf.Clamp01(k);
            f = Mathf.Clamp01(f);
            float blob = Mathf.Sin(Mathf.PI * k) * 0.6f;
            float tri = (1f - k) * (1f - f) + k * f;
            float e = 2f * f - 1f;
            float ell = 0.62f * Mathf.Sqrt(Mathf.Max(0f, 1f - e * e));
            return Mathf.Clamp01((1f - blob) * tri + blob * ell);
        }

        /// <summary>Height of the kind's mark, as a share of the shape's height: where the weight sits.</summary>
        public static float MarkHeight01(float k) => 1f / 3f + Mathf.Clamp01(k) / 3f;

        /// <summary>The morph frame (0 .. frames - 1) for a morph value.</summary>
        public static int Frame(float k, int frames)
        {
            frames = Mathf.Max(2, frames);
            return Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(k) * (frames - 1)), 0, frames - 1);
        }

        /// <summary>
        /// For a Pogo that travels from one surface to another (a Drifter, a climbing Clinger): 0 resting on the surface
        /// below, 1 touching the one above.
        /// </summary>
        public static float BetweenSurfaces(Bubble b)
        {
            var map = Game.Map;
            float bottom = b.Position.y - b.Radius, top = b.Position.y + b.Radius;
            float homeFloor = b.HomeStorey * (float)StageMap.StoreyRows;
            float homeCeil = homeFloor + StageMap.StoreyRows;
            float floor = homeFloor;
            float ceiling = homeCeil;
            if (map != null)
            {
                float floorProbe = Mathf.Clamp(bottom + 0.01f, homeFloor, homeCeil - 0.001f);
                float ceilProbe = Mathf.Clamp(top - 0.01f, homeFloor, homeCeil - 0.001f);
                floor = Mathf.Max(homeFloor, map.ShotFloor(b.Position.x, floorProbe));
                ceiling = Mathf.Min(homeCeil, map.ShotCeiling(b.Position.x, ceilProbe, out _));
            }
            float span = ceiling - floor - 2f * b.Radius;
            return span > 0.05f ? Mathf.Clamp01((bottom - floor) / span) : 0f;
        }

        /// <summary>
        /// True when the shape touches the rectangle. The shape's base sits on the bottom of the bubble's circle
        /// (centre.y - radius) and it is as wide as the circle, less the outline pixel.
        /// </summary>
        public static bool Overlaps(Vector2 centre, float radius, float k, Rect rect)
        {
            float px = Tuning.I.Pixel;
            float halfW = Mathf.Max(px, radius - px);
            float height = 2f * halfW * HeightShare;
            float bottom = centre.y - radius + px;
            float y0 = Mathf.Max(rect.yMin, bottom), y1 = Mathf.Min(rect.yMax, bottom + height);
            if (y1 < y0) return false;
            float dx = Mathf.Max(0f, Mathf.Max(rect.xMin - centre.x, centre.x - rect.xMax));
            if (dx >= halfW) return false;
            const int samples = 12;
            for (int i = 0; i <= samples; i++)
            {
                float y = Mathf.Lerp(y0, y1, i / (float)samples);
                float f = (y - bottom) / height;
                if (dx < halfW * HalfWidth01(f, k)) return true;
            }
            return false;
        }
    }
}
