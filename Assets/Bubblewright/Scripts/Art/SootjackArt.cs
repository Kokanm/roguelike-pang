using System.Collections.Generic;
using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// Sootjack, painted in code from the world's palette plus steel (docs/superpowers/specs/2026-11-27-boss-redesign-design.md,
    /// the Boss Sheet): an iron stove on stilt legs with a pipe, a visor slit, rivets and a belly grate. The grate is the
    /// core: closed it is dark steel like the rest of the belly, open it is a cream disc inside a ring of the world's accent
    /// (Rooftops' accent is cream, so its ring is tileCap). No eyes. 1 px ink outline. Three poses share one canvas whose
    /// pivot is the boss's centre, so the sprite lines up with the parts: 0 walking on stilts, 1 perched with one short
    /// leg and the stove leaning out, 2 charging low.
    /// </summary>
    public static class SootjackArt
    {
        const int Ppu = 15;
        const int W = 120, H = 104;
        const int OriginX = 60, OriginY = 52; // the boss's centre, in canvas pixels

        static readonly Dictionary<string, PlaceholderArt.BossSprites> cache = new Dictionary<string, PlaceholderArt.BossSprites>();
        static readonly Dictionary<string, Sprite> potCache = new Dictionary<string, Sprite>();

        public static PlaceholderArt.BossSprites For(Theme theme)
        {
            if (cache.TryGetValue(theme.name, out var cached) && cached.body != null && cached.body.texture != null) return cached;
            var walk = Pose(theme, Sootjack.WalkPose);
            walk.poses = new[] { walk, Pose(theme, Sootjack.PerchPose), Pose(theme, Sootjack.ChargePose) };
            cache[theme.name] = walk;
            return walk;
        }

        static PlaceholderArt.BossSprites Pose(Theme theme, int pose)
        {
            var closed = Paint(theme, pose, false);
            var opened = Paint(theme, pose, true);
            closed.Outline(Palette.Ink);
            opened.Outline(Palette.Ink);
            var pivot = new Vector2(OriginX, OriginY);
            string key = $"Sootjack_{theme.name}_{pose}";
            return new PlaceholderArt.BossSprites
            {
                body = closed.ToSprite(key, pivot, Ppu),
                open = opened.ToSprite(key + "_Open", pivot, Ppu),
                flash = closed.Silhouette(Palette.Cream).ToSprite(key + "_Flash", pivot, Ppu),
                tell = new PixelCanvas(1, 1).ToSprite(key + "_Tell", Vector2.zero, Ppu),
            };
        }

        // Local units (relative to the boss's centre) to canvas pixels.
        static int Px(float x) => OriginX + Mathf.RoundToInt(x * Ppu);
        static int Py(float y) => OriginY + Mathf.RoundToInt(y * Ppu);
        static int Len(float v) => Mathf.Max(1, Mathf.RoundToInt(v * Ppu));

        static void Fill(PixelCanvas c, float x, float y, float w, float h, Color32 col) => c.Rect(Px(x), Py(y), Len(w), Len(h), col);

        static Color32 RingColour(Theme theme)
        {
            var a = theme.accent;
            bool cream = a.r == Palette.Cream.r && a.g == Palette.Cream.g && a.b == Palette.Cream.b;
            return cream ? theme.tileCap : a;
        }

        static PixelCanvas Paint(Theme theme, int pose, bool open)
        {
            var c = new PixelCanvas(W, H);
            float stoveBottom = -0.9f, stoveTop = 2f;
            if (pose == Sootjack.ChargePose)
            {
                float h = Tuning.I.sootChargeHeight;
                stoveBottom = -h * 0.5f;
                stoveTop = h * 0.5f;
                PaintSled(c, theme, stoveBottom, stoveTop, open);
                return c;
            }

            if (pose == Sootjack.WalkPose)
            {
                Stilt(c, -Sootjack.FootX - Sootjack.StiltW * 0.5f, -Sootjack.HalfHeight, Sootjack.Clearance);
                Stilt(c, Sootjack.FootX - Sootjack.StiltW * 0.5f, -Sootjack.HalfHeight, Sootjack.Clearance);
            }
            else
            {
                // Perched: one short leg at the back corner grips the rim; the stove leans out over the edge.
                float legBottom = Sootjack.BellyLocalY - Sootjack.LegH;
                Stilt(c, -Sootjack.FootX - Sootjack.StiltW * 0.5f, legBottom, Sootjack.LegH);
            }

            // Belly: the grate in the middle, steel skirts either side.
            Fill(c, -Sootjack.HalfBody, Sootjack.BellyLocalY, Sootjack.HalfBody * 2f, Sootjack.BellyH, Palette.SteelDark);
            PaintGrate(c, theme, open);

            // The stove: body, light band across the top, shade band low down, visor slit, rivets.
            float bodyH = stoveTop - (Sootjack.BellyLocalY + Sootjack.BellyH);
            Fill(c, -Sootjack.HalfBody, Sootjack.BellyLocalY + Sootjack.BellyH, Sootjack.HalfBody * 2f, bodyH, theme.tileFill);
            Fill(c, -Sootjack.HalfBody, stoveTop - 0.8f, Sootjack.HalfBody * 2f, 0.8f, theme.tileCap);
            Fill(c, -Sootjack.HalfBody, Sootjack.BellyLocalY + Sootjack.BellyH, Sootjack.HalfBody * 2f, 0.3f, theme.tileShade);
            Fill(c, -2f, 0.35f, 4f, 0.33f, theme.skyTop); // the visor slit, never an eye
            for (int i = -2; i <= 2; i += 2) Fill(c, i * 1.2f - 0.1f, 1.15f, 0.2f, 0.2f, theme.tileShade); // rivets
            for (int i = 0; i < 5; i++) Fill(c, -3.1f + i * 0.2f, 0.0f - 0.05f, 0.08f, 0.5f, theme.tileShade); // vent slats at the side

            // The pipe, on the right, with a lighter rim.
            Fill(c, 1.6f, stoveTop, 1f, 1.2f, theme.tileShade);
            Fill(c, 1.5f, stoveTop + 1.0f, 1.2f, 0.2f, theme.tileCap);
            return c;
        }

        static void Stilt(PixelCanvas c, float x, float bottom, float height)
        {
            Fill(c, x, bottom, Sootjack.StiltW, height, Palette.Steel);
            Fill(c, x, bottom, 0.1f, height, Palette.SteelDark);
            Fill(c, x - 0.3f, bottom, Sootjack.StiltW + 0.6f, 0.3f, Palette.SteelDark); // the foot
        }

        /// <summary>Closed it is dark slats like the armour; open it is a cream disc inside a ring.</summary>
        static void PaintGrate(PixelCanvas c, Theme theme, bool open)
        {
            float x = -1.5f, y = Sootjack.BellyLocalY, w = 3f, h = Sootjack.BellyH;
            if (!open)
            {
                Fill(c, x, y, w, h, Palette.SteelDark);
                for (int i = 0; i < 9; i++) Fill(c, x + 0.15f + i * 0.33f, y + 0.06f, 0.12f, h - 0.12f, Palette.Steel);
                return;
            }
            Fill(c, x, y, w, h, RingColour(theme));
            c.Rect(Px(x) + 1, Py(y) + 1, Len(w) - 2, Len(h) - 2, Palette.Cream);
            for (int i = 0; i < 4; i++) Fill(c, x + 0.45f + i * 0.7f, y + 0.12f, 0.08f, h - 0.24f, Palette.SteelLight);
        }

        /// <summary>The low charging body: a flat iron sled with a closed grate on its roof and a stubby pipe.</summary>
        static void PaintSled(PixelCanvas c, Theme theme, float bottom, float top, bool open)
        {
            float h = top - bottom;
            Fill(c, -Sootjack.HalfBody, bottom, Sootjack.HalfBody * 2f, h, theme.tileFill);
            Fill(c, -Sootjack.HalfBody, top - 0.55f, Sootjack.HalfBody * 2f, 0.55f, theme.tileCap);
            Fill(c, -Sootjack.HalfBody, bottom, Sootjack.HalfBody * 2f, 0.3f, Palette.SteelDark); // runners
            Fill(c, -2f, bottom + h * 0.4f, 4f, 0.3f, theme.skyTop);
            for (int i = -2; i <= 2; i += 2) Fill(c, i * 1.2f - 0.1f, bottom + 0.45f, 0.2f, 0.2f, theme.tileShade);
            // The grate lies on the roof, closed: armour while it charges.
            Fill(c, -1.5f, top - Sootjack.BellyH, 3f, Sootjack.BellyH, Palette.SteelDark);
            for (int i = 0; i < 9; i++) Fill(c, -1.35f + i * 0.33f, top - Sootjack.BellyH + 0.06f, 0.12f, Sootjack.BellyH - 0.12f, Palette.Steel);
            Fill(c, 1.9f, top, 0.8f, 0.6f, theme.tileShade);
            Fill(c, 1.8f, top + 0.45f, 1f, 0.15f, theme.tileCap);
        }

        /// <summary>One flue pot: a short iron pot with a dark mouth, standing on the roof; its pivot is the middle of its foot.</summary>
        public static Sprite Pot(Theme theme)
        {
            if (potCache.TryGetValue(theme.name, out var cached) && cached != null && cached.texture != null) return cached;
            var c = new PixelCanvas(26, 16);
            c.Rect(3, 0, 20, 11, theme.tileShade);
            c.Rect(1, 10, 24, 4, theme.tileCap);
            c.Rect(4, 11, 18, 2, theme.skyTop); // the mouth
            c.Rect(3, 0, 2, 10, theme.tileFill);
            c.Outline(Palette.Ink);
            var sprite = c.ToSprite("Sootjack_Pot_" + theme.name, new Vector2(13f, 1f), Ppu);
            potCache[theme.name] = sprite;
            return sprite;
        }
    }
}
