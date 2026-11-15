using System.Collections.Generic;
using UnityEngine;

namespace Bubblewright
{
    /// <summary>How a Pogo is drawn besides its kind: plain, ceramic, cracked ceramic, steel, or a faded Ghost.</summary>
    public enum PogoLook { Normal, Ceramic, Cracked, Steel, GhostFaded, GhostFlicker }

    public partial class PlaceholderArt
    {
        readonly Dictionary<int, Sprite> pogoFrames = new Dictionary<int, Sprite>();

        /// <summary>
        /// A Pogo's sprite: the soft triangle of this kind and tier at morph frame (0 points up, Tuning.pogoFrames - 1
        /// points down). arrow is the Drifter's arrow: +1 up, -1 down, 0 none. Same size and centre as the round bubble
        /// of the tier, with the base on the bottom of the circle.
        /// </summary>
        public Sprite PogoFrame(BubbleKind kind, int tier, int frame, PogoLook look = PogoLook.Normal, int arrow = 0)
        {
            var t = Tuning.I;
            int frames = Mathf.Max(2, t.pogoFrames);
            tier = Mathf.Clamp(tier, 0, t.tiers.Length - 1);
            frame = Mathf.Clamp(frame, 0, frames - 1);
            int key = ((((int)kind * 8 + tier) * 64 + frame) * 8 + (int)look) * 3 + (arrow > 0 ? 1 : arrow < 0 ? 2 : 0);
            if (pogoFrames.TryGetValue(key, out var s) && s != null && s.texture != null) return s;
            int d = Mathf.RoundToInt(t.tiers[tier].diameter * ppu);
            s = BuildPogo(kind, t.Kind(kind).color, d, frame / (float)(frames - 1), look, arrow);
            pogoFrames[key] = s;
            return s;
        }

        Sprite BuildPogo(BubbleKind kind, Color32 baseColor, int d, float k, PogoLook look, int arrow)
        {
            var c = new PixelCanvas(d, d);
            float cx = d * 0.5f;
            float w = (d - 2) * 0.5f;
            int h = Mathf.Max(3, Mathf.RoundToInt((d - 2) * PogoShape.HeightShare));
            bool steel = look == PogoLook.Steel;
            bool ceramic = look == PogoLook.Ceramic || look == PogoLook.Cracked;
            bool faded = look == PogoLook.GhostFaded || look == PogoLook.GhostFlicker;
            bool strong = look == PogoLook.GhostFlicker;
            Color32 body = steel ? Palette.Steel : ceramic ? Palette.Mix(baseColor, Palette.Cream, 0.55f) : baseColor;

            // Body, row by row from the bottom (canvas row 1, above the outline row).
            var half = new float[h];
            for (int i = 0; i < h; i++)
            {
                half[i] = Mathf.Max(0.5f, w * PogoShape.HalfWidth01((i + 0.5f) / h, k));
                for (int x = 0; x < d; x++)
                    if (Mathf.Abs(x + 0.5f - cx) <= half[i]) c.Set(x, 1 + i, body);
            }
            // Soften the corners of the widest end.
            foreach (int i in new[] { 0, h - 1 })
            {
                if (half[i] <= 2f) continue;
                for (int x = 0; x < d; x++)
                {
                    float dx = Mathf.Abs(x + 0.5f - cx);
                    if (dx <= half[i] && dx > half[i] - 1f) c.Set(x, 1 + i, Palette.Clear);
                }
            }

            if (faded)
            {
                PaintFadedGhost(c, baseColor, d, h, k, strong);
                return c.ToSprite($"Pogo_Ghost_{d}px_{Mathf.RoundToInt(k * 100)}_{(strong ? "flicker" : "faded")}", new Vector2(cx, d * 0.5f), ppu);
            }

            // Light stays put: a darker band down the right side and along the right of the base, shine inside the left.
            var shade = steel ? Palette.SteelDark : Palette.Shade(body, 0.78f);
            float band = Mathf.Max(1.5f, d * 0.1f);
            for (int i = 0; i < h; i++)
                for (int x = 0; x < d; x++)
                {
                    if (!c.Filled(x, 1 + i)) continue;
                    float rx = x + 0.5f - cx;
                    if ((rx > 0f && half[i] - rx < band) || (i == 0 && rx > 0f)) c.Set(x, 1 + i, shade);
                    if (steel && rx < 0f && half[i] + rx < 1f) c.Set(x, 1 + i, Palette.SteelLight);
                }
            if (!steel)
                for (int i = Mathf.RoundToInt(h * 0.45f); i <= Mathf.RoundToInt(h * 0.65f) && i < h; i++)
                {
                    int x = Mathf.FloorToInt(cx - half[i] + 2f);
                    if (c.Filled(x, 1 + i)) c.Set(x, 1 + i, Palette.Cream);
                }

            PaintPogoMark(c, kind, baseColor, d, h, k, steel, arrow);

            if (ceramic)
            {
                AddEdge(c, Palette.Lilac);
                if (look == PogoLook.Cracked) AddCracks(c, cx, 1 + h * 0.5f, w * 0.8f, Palette.Shade(body, 0.5f));
            }
            c.Outline(Palette.Ink);
            return c.ToSprite($"Pogo_{(steel ? "Steel" : kind.ToString())}_{d}px_{Mathf.RoundToInt(k * 100)}_{look}{(arrow != 0 ? "_" + arrow : "")}",
                new Vector2(cx, d * 0.5f), ppu);
        }

        /// <summary>The kind's mark, upright, on the shape's weight (lower third pointing up, upper third pointing down).</summary>
        void PaintPogoMark(PixelCanvas c, BubbleKind kind, Color32 color, int d, int h, float k, bool steel, int arrow)
        {
            float cx = d * 0.5f, cy = 1f + h * PogoShape.MarkHeight01(k);
            float r = d * 0.5f * 0.6f;
            var mark = steel ? Palette.SteelDark : Palette.Shade(color, 0.68f);
            if (steel)
            {
                float off = Mathf.Max(2f, r * 0.4f), dot = Mathf.Max(0.8f, r * 0.14f);
                DiscOnFilled(c, cx - off, cy, dot, mark);
                DiscOnFilled(c, cx + off, cy, dot, mark);
                return;
            }
            switch (kind)
            {
                case BubbleKind.Trigloop:
                    {
                        float dot = Mathf.Max(0.8f, r * 0.13f), ring = r * 0.4f;
                        for (int i = 0; i < 3; i++)
                        {
                            float a = Mathf.PI * 0.5f + i * Mathf.PI * 2f / 3f;
                            DiscOnFilled(c, cx + Mathf.Cos(a) * ring, cy + Mathf.Sin(a) * ring, dot, mark);
                        }
                        break;
                    }
                case BubbleKind.Lead:
                    {
                        int th = Mathf.Max(1, Mathf.RoundToInt(r * 0.25f));
                        c.RectOnFilled(0, Mathf.RoundToInt(cy - th * 0.5f), d, th, mark);
                        break;
                    }
                case BubbleKind.Lucky:
                    {
                        int len = Mathf.Max(1, Mathf.RoundToInt(r * 0.35f));
                        int mx = Mathf.FloorToInt(cx), my = Mathf.FloorToInt(cy);
                        for (int i = -len; i <= len; i++)
                        {
                            if (c.Filled(mx + i, my)) c.Set(mx + i, my, Palette.Cream);
                            if (c.Filled(mx, my + i)) c.Set(mx, my + i, Palette.Cream);
                        }
                        break;
                    }
                case BubbleKind.Clinger:
                    {
                        int a = Mathf.Max(2, Mathf.RoundToInt(r * 0.4f));
                        int thick = d >= 24 ? 2 : 1;
                        float lift = a / 2f;
                        for (int i = -a; i <= a; i++)
                        {
                            int x = Mathf.RoundToInt(cx + i - 0.5f);
                            int y = Mathf.RoundToInt(cy - (a - Mathf.Abs(i)) + lift);
                            for (int t = 0; t < thick; t++)
                                if (c.Filled(x, y + t)) c.Set(x, y + t, mark);
                        }
                        break;
                    }
                case BubbleKind.Sinker:
                    {
                        float line = 1f + h * 0.24f;
                        var foam = Palette.Mix(color, Palette.Cream, 0.35f);
                        for (int y = 0; y < d; y++)
                            for (int x = 0; x < d; x++)
                            {
                                if (!c.Filled(x, y)) continue;
                                float py = y + 0.5f;
                                if (py < line) c.Set(x, y, mark);
                                else if (py < line + 1f) c.Set(x, y, foam);
                            }
                        break;
                    }
                case BubbleKind.Frost:
                    {
                        int a = Mathf.Max(1, Mathf.RoundToInt(r * 0.33f));
                        int mx = Mathf.RoundToInt(cx - 0.5f), my = Mathf.RoundToInt(cy - 0.5f);
                        for (int i = -a; i <= a; i++)
                            for (int j = -a; j <= a; j++)
                                if (Mathf.Abs(i) + Mathf.Abs(j) <= a && c.Filled(mx + i, my + j)) c.Set(mx + i, my + j, Palette.IceGlint);
                        break;
                    }
                case BubbleKind.Ghost:
                    {
                        float outer = Mathf.Max(4, Mathf.RoundToInt(r * 0.9f)) * 0.5f;
                        DiscOnFilled(c, cx, cy, outer, mark);
                        DiscOnFilled(c, cx, cy, Mathf.Max(0.8f, outer - (d >= 24 ? 1.4f : 1f)), Palette.Mix(color, Palette.Cream, 0.6f));
                        break;
                    }
                case BubbleKind.Scorch:
                    {
                        float outer = Mathf.Max(5, Mathf.RoundToInt(r)) * 0.5f;
                        DiscOnFilled(c, cx, cy, outer, Palette.Mix(color, Palette.Cream, 0.5f));
                        DiscOnFilled(c, cx, cy, Mathf.Max(0.8f, outer - (d >= 24 ? 1.6f : 1f)), Palette.Cream);
                        break;
                    }
                case BubbleKind.Drifter:
                    {
                        // An arrowhead pointing the way it moves: up or down.
                        int a = Mathf.Max(2, Mathf.RoundToInt(r * 0.45f));
                        int dir = arrow < 0 ? -1 : 1;
                        int mx = Mathf.RoundToInt(cx - 0.5f), my = Mathf.RoundToInt(cy - 0.5f);
                        for (int v = 0; v <= a; v++)
                        {
                            int wRow = a - v;
                            for (int u = -wRow; u <= wRow; u++)
                            {
                                if (v < a - Mathf.Abs(u) - 1) continue; // a chevron two pixels thick
                                int x = mx + u, y = my + dir * (v - a / 2);
                                if (c.Filled(x, y)) c.Set(x, y, mark);
                            }
                        }
                        break;
                    }
            }
        }

        /// <summary>The faded Ghost Pogo: see-through body, rim on every second segment (all of it while flickering), ring mark, no outline.</summary>
        void PaintFadedGhost(PixelCanvas c, Color32 color, int d, int h, float k, bool strong)
        {
            var rim = Alpha(color, strong ? 0.95f : 0.8f);
            var body = Alpha(color, strong ? 0.45f : 0.2f);
            var ring = Alpha(color, strong ? 0.8f : 0.45f);
            float cx = d * 0.5f, cy = 1f + h * PogoShape.MarkHeight01(k);
            float ringRadius = Mathf.Max(4, Mathf.RoundToInt(d * 0.3f * 0.9f)) * 0.5f;
            int segments = d >= 24 ? 16 : 10;
            var filled = new bool[d, d];
            for (int y = 0; y < d; y++)
                for (int x = 0; x < d; x++)
                    filled[x, y] = c.Filled(x, y);
            for (int y = 0; y < d; y++)
                for (int x = 0; x < d; x++)
                {
                    if (!filled[x, y]) continue;
                    bool edge = x == 0 || y == 0 || x == d - 1 || y == d - 1 ||
                                !filled[x - 1, y] || !filled[x + 1, y] || !filled[x, y - 1] || !filled[x, y + 1];
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    if (edge)
                    {
                        float turn = Mathf.Atan2(dy, dx) / (2f * Mathf.PI);
                        int seg = Mathf.FloorToInt((turn < 0f ? turn + 1f : turn) * segments);
                        c.Set(x, y, strong || seg % 2 == 0 ? rim : Palette.Clear);
                    }
                    else if (Mathf.Abs(dist - ringRadius) <= 0.5f) c.Set(x, y, ring);
                    else c.Set(x, y, body);
                }
        }

        static void DiscOnFilled(PixelCanvas c, float mx, float my, float radius, Color32 col)
        {
            for (int y = 0; y < c.Height; y++)
                for (int x = 0; x < c.Width; x++)
                {
                    float dx = x + 0.5f - mx, dy = y + 0.5f - my;
                    if (dx * dx + dy * dy <= radius * radius && c.Filled(x, y)) c.Set(x, y, col);
                }
        }

        /// <summary>Recolours the filled pixels that touch an empty one (the ceramic glaze rim).</summary>
        static void AddEdge(PixelCanvas c, Color32 col)
        {
            var edge = new List<Vector2Int>();
            for (int y = 0; y < c.Height; y++)
                for (int x = 0; x < c.Width; x++)
                    if (c.Filled(x, y) && (!c.Filled(x - 1, y) || !c.Filled(x + 1, y) || !c.Filled(x, y - 1) || !c.Filled(x, y + 1)))
                        edge.Add(new Vector2Int(x, y));
            foreach (var p in edge) c.Set(p.x, p.y, col);
        }
    }
}
