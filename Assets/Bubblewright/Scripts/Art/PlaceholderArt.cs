using System.Collections.Generic;
using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// Placeholder sprites painted in code from the design sheet's shapes and palette, at exactly
    /// 15 px per unit with a 1 px ink outline on the outer edge. Real art (SVG exported to PNG) replaces
    /// these later without touching gameplay code: swap the sprites, keep the pivots.
    /// </summary>
    public class PlaceholderArt
    {
        public Sprite WickBack, WickFront, Flame, PickupRing;
        public Sprite WickBackFlash, WickFrontFlash; // cream silhouettes for the hurt flash
        public Sprite Launcher, LauncherFlash, Hand;
        public Sprite[,] Bubbles; // [kind, tier]
        public Sprite[] BubbleFlash; // cream silhouette per tier, for the pop flash frame
        public Sprite[] PopRings; // per tier
        public Sprite HarpoonTip, Rope;
        public Sprite FireButton, FireButtonPressed, MoveHint, MoveKnob, MoveDot, ClimbUpHint, ClimbDownHint;
        public Sprite Droplet, Pixel, Dust, MuzzleFlash;
        public Sprite SpikeHidden, SpikeWarn, SpikeRaised;

        /// <summary>Height of the harpoon tip sprite above its pivot, in pixels.</summary>
        public const int HarpoonTipHeight = 8;

        /// <summary>Launcher column (left edge) in Wick's 23 px sprite: back view raised in his right hand, front view on the viewer's left.</summary>
        public const int LauncherBackX = 15, LauncherFrontX = 6;

        /// <summary>
        /// Where the launcher sprite sits relative to Wick's feet, in pixels. Its bottom-left pixel is
        /// 2 px left of the launcher column (room for the hand and outline) and 13 px up.
        /// </summary>
        public static Vector2 LauncherOffsetPixels(bool front) =>
            new Vector2((front ? LauncherFrontX : LauncherBackX) - 2 - 11.5f, 13f);

        bool Alive => WickBack != null && WickBack.texture != null && ClimbUpHint != null && ClimbUpHint.texture != null;

        static PlaceholderArt instance;
        public static PlaceholderArt I => instance != null && instance.Alive ? instance : (instance = new PlaceholderArt(Tuning.I));

        readonly int ppu;

        PlaceholderArt(Tuning t)
        {
            ppu = t.pixelsPerUnit;
            var wick = SpritesFor(Character.Wick);
            WickBack = wick.Back;
            WickFront = wick.Front;
            WickBackFlash = wick.BackFlash;
            WickFrontFlash = wick.FrontFlash;
            Launcher = wick.Launcher;
            LauncherFlash = wick.LauncherFlash;
            Hand = wick.Hand;
            Flame = BuildFlame();
            PickupRing = BuildPickupRing();

            int kindCount = System.Enum.GetValues(typeof(BubbleKind)).Length;
            Bubbles = new Sprite[kindCount, t.tiers.Length];
            BubbleFlash = new Sprite[t.tiers.Length];
            PopRings = new Sprite[t.tiers.Length];
            for (int tier = 0; tier < t.tiers.Length; tier++)
            {
                int d = Mathf.RoundToInt(t.tiers[tier].diameter * ppu);
                for (int k = 0; k < kindCount; k++)
                    Bubbles[k, tier] = BuildBubble((BubbleKind)k, t.Kind((BubbleKind)k).color, d);
                var disc = new PixelCanvas(d, d);
                disc.Circle(d * 0.5f, d * 0.5f, d * 0.5f, Palette.Cream);
                BubbleFlash[tier] = disc.ToSprite($"BubbleFlash_{d}px", new Vector2(d * 0.5f, d * 0.5f), ppu);
                PopRings[tier] = BuildRing(Mathf.RoundToInt(d * 1.35f));
            }

            HarpoonTip = BuildHarpoonTip();
            Rope = BuildRope();
            FireButton = BuildFireButton(false);
            FireButtonPressed = BuildFireButton(true);
            MoveHint = BuildMoveHint();
            MoveKnob = BuildDisc(14, Palette.Lilac, "MoveKnob");
            MoveDot = BuildDisc(8, Palette.Cream, "MoveDot");
            ClimbUpHint = BuildClimbArrow(true);
            ClimbDownHint = BuildClimbArrow(false);
            SpikeHidden = BuildSpikeHidden();
            SpikeWarn = BuildSpikeWarn();
            SpikeRaised = BuildSpikeRaised();

            var drop = new PixelCanvas(2, 2);
            drop.Rect(0, 0, 2, 2, Palette.Cream);
            Droplet = drop.ToSprite("Droplet", new Vector2(1, 1), ppu);

            var px = new PixelCanvas(1, 1);
            px.Set(0, 0, new Color32(255, 255, 255, 255));
            Pixel = px.ToSprite("Pixel", new Vector2(0.5f, 0.5f), ppu);

            // Dust: a soft 4 px puff with no outline, so it never reads as something you can hit.
            var dust = new PixelCanvas(5, 5);
            dust.Circle(2.5f, 2.5f, 2.3f, Palette.Mix(Palette.Lilac, Palette.Cream, 0.55f));
            dust.Set(1, 3, Palette.Cream);
            Dust = dust.ToSprite("Dust", new Vector2(2.5f, 1f), ppu);

            // Muzzle flash: a mint star at the launcher tip, shown for one frame.
            var mf = new PixelCanvas(7, 7);
            mf.Rect(3, 0, 1, 7, Palette.Mint);
            mf.Rect(0, 3, 7, 1, Palette.Mint);
            mf.Rect(2, 2, 3, 3, Palette.Mint);
            mf.Set(3, 3, Palette.Cream);
            MuzzleFlash = mf.ToSprite("MuzzleFlash", new Vector2(3.5f, 1f), ppu);
        }

        Sprite BuildPickupRing()
        {
            var c = new PixelCanvas(13, 16);
            c.Ellipse(6.5f, 8f, 6f, 7.5f, Palette.Ink);
            c.Ellipse(6.5f, 8f, 5f, 6.5f, Palette.Mint);
            c.Ellipse(6.5f, 8f, 4f, 5.5f, Palette.Clear);
            return c.ToSprite("PickupRing", new Vector2(6.5f, 0f), ppu);
        }

        // ---------------------------------------------------------------- Characters (23 x 30 px, pivot at feet)

        /// <summary>Everything about a hero that carries the cloak colours. The flame and hearts are shared.</summary>
        public class CharacterSprites
        {
            public Sprite Back, Front, BackFlash, FrontFlash, Hand, Launcher, LauncherFlash;
            /// <summary>Flat Palette.Dusk shape of the front view, for locked cards.</summary>
            public Sprite FrontLocked;
        }

        readonly System.Collections.Generic.Dictionary<string, CharacterSprites> characterSprites =
            new System.Collections.Generic.Dictionary<string, CharacterSprites>();

        /// <summary>Builds on first use and keeps the result.</summary>
        public CharacterSprites SpritesFor(Character c)
        {
            c = c ?? Character.Wick;
            if (characterSprites.TryGetValue(c.id, out var cached) && cached.Back != null && cached.Back.texture != null) return cached;
            var set = new CharacterSprites
            {
                Back = BuildWick(false, c.body, c.bodyDark, c.bodyLine, out Sprite backFlash, out _),
                BackFlash = backFlash,
            };
            set.Front = BuildWick(true, c.body, c.bodyDark, c.bodyLine, out Sprite frontFlash, out Sprite locked);
            set.FrontFlash = frontFlash;
            set.FrontLocked = locked;
            set.Launcher = BuildLauncher(c.body, out Sprite launcherFlash);
            set.LauncherFlash = launcherFlash;
            set.Hand = BuildHand(c.body);
            characterSprites[c.id] = set;
            return set;
        }

        Sprite BuildWick(bool front, Color32 body, Color32 bodyDark, Color32 bodyLine, out Sprite flash, out Sprite locked)
        {
            var c = new PixelCanvas(23, 30);
            // Cloak: round body
            c.Ellipse(11.5f, 11f, 8.5f, 8f, body);
            c.EllipseOnFilled(11.5f, 2.5f, 10f, 3.5f, bodyDark);
            // Belt: interior line in darker orange, not ink
            c.RectOnFilled(3, 8, 17, 1, bodyLine);
            // Boots (shared by both views), drawn over the cloak hem
            c.Rect(6, 1, 5, 3, Palette.Ink);
            c.Rect(12, 1, 5, 3, Palette.Ink);
            c.Rect(7, 3, 3, 1, Palette.Deep);
            c.Rect(13, 3, 3, 1, Palette.Deep);
            // Hood
            c.Ellipse(11.5f, 19.5f, 6.5f, 5.5f, body);
            c.Ellipse(11.5f, 24f, 2f, 2.2f, body);
            c.RectOnFilled(6, 14, 12, 1, bodyDark);
            // Candle wick on top (the flame is its own sprite)
            c.Rect(11, 26, 1, 2, Palette.Ink);

            if (front)
            {
                // Face in the hood opening, cream eyes. Only the front view has eyes.
                c.Ellipse(11.5f, 19f, 4f, 3.2f, Palette.Ink);
                c.Rect(9, 19, 1, 2, Palette.Cream);
                c.Rect(14, 19, 1, 2, Palette.Cream);
                c.RectOnFilled(11, 4, 1, 9, bodyLine); // cloak opening
            }
            else
            {
                c.RectOnFilled(11, 15, 1, 9, bodyLine); // hood seam
            }
            // The launcher is its own part (BuildLauncher) so it can kick and rise on its own.

            c.Outline(Palette.Ink);
            string name = front ? "Wick_Front" : "Wick_Back";
            var pivot = new Vector2(11.5f, 0f);
            flash = c.Silhouette(Palette.Cream).ToSprite(name + "_Flash", pivot, ppu);
            locked = c.Silhouette(Palette.Dusk).ToSprite(name + "_Locked", pivot, ppu);
            return c.ToSprite(name, pivot, ppu);
        }

        /// <summary>Steel launcher and the hand gripping it. Pivot at the bottom-left; see LauncherOffsetPixels.</summary>
        Sprite BuildLauncher(Color32 hand, out Sprite flash)
        {
            var c = new PixelCanvas(6, 13);
            const int x = 2, y = 1;
            c.Rect(x, y, 3, 11, Palette.Steel);
            c.Rect(x, y, 1, 11, Palette.SteelLight);
            c.Rect(x + 2, y, 1, 11, Palette.SteelDark);
            c.Rect(x, y + 10, 3, 1, Palette.SteelLight);
            c.Rect(x - 1, y + 1, 2, 2, hand);
            c.Outline(Palette.Ink);
            flash = c.Silhouette(Palette.Cream).ToSprite("Launcher_Flash", Vector2.zero, ppu);
            return c.ToSprite("Launcher", Vector2.zero, ppu);
        }

        /// <summary>His free hand, shown only when he waves or cheers. Pivot at the centre.</summary>
        Sprite BuildHand(Color32 body)
        {
            var c = new PixelCanvas(5, 5);
            c.Rect(1, 1, 3, 3, body);
            c.Outline(Palette.Ink);
            return c.ToSprite("Hand", new Vector2(2.5f, 2.5f), ppu);
        }

        // ---------------------------------------------------------------- Upgrade icons (18 x 18, drawn on the pick cards)

        readonly System.Collections.Generic.Dictionary<string, Texture2D> upgradeIcons = new System.Collections.Generic.Dictionary<string, Texture2D>();

        /// <summary>Icon for an upgrade card, by upgrade name. Built once, then cached.</summary>
        public Texture2D UpgradeIcon(string name)
        {
            if (!upgradeIcons.TryGetValue(name, out var tex) || tex == null)
            {
                tex = BuildUpgradeIcon(name).ToTexture("Icon " + name);
                upgradeIcons[name] = tex;
            }
            return tex;
        }

        public const int UpgradeIconSize = 18;

        static PixelCanvas BuildUpgradeIcon(string name)
        {
            var c = new PixelCanvas(UpgradeIconSize, UpgradeIconSize);
            // Arrowhead pointing up: rows of width 2w+1, 2w-1, ... centred on cx.
            void Head(int cx, int y, int w, Color32 col)
            {
                for (int i = 0; i <= w; i++) c.Rect(cx - (w - i), y + i, 2 * (w - i) + 1, 1, col);
            }
            void Flame(float cx, float y, Color32 core)
            {
                c.Ellipse(cx, y + 3.5f, 3.2f, 3.4f, Palette.Saffron);
                c.Ellipse(cx, y + 7f, 1.9f, 3.2f, Palette.Saffron);
                c.Ellipse(cx, y + 3.5f, 1.6f, 2.1f, core);
            }

            switch (name)
            {
                case "Twin Cable":
                    foreach (int x in new[] { 5, 12 })
                    {
                        c.Rect(x, 2, 1, 10, Palette.Steel);
                        Head(x, 11, 2, Palette.SteelLight);
                    }
                    break;
                case "Anchor Tip":
                    c.Rect(8, 3, 2, 10, Palette.Steel);
                    c.Rect(5, 11, 8, 2, Palette.Steel);
                    c.Circle(9f, 15f, 2.2f, Palette.SteelLight);
                    c.Set(8, 15, Palette.Clear);
                    c.Set(9, 15, Palette.Clear);
                    c.Rect(3, 2, 12, 2, Palette.Steel);
                    c.Rect(2, 3, 2, 4, Palette.Steel);
                    c.Rect(14, 3, 2, 4, Palette.Steel);
                    Head(2, 6, 1, Palette.SteelLight);
                    Head(15, 6, 1, Palette.SteelLight);
                    break;
                case "Quick Winch":
                    c.Rect(8, 1, 2, 9, Palette.Mint);
                    for (int i = 0; i < 4; i++) c.Rect(5 + i, 9 + i, 8 - 2 * i, 1, Palette.Mint);
                    c.Rect(2, 4, 4, 1, Palette.Mist);
                    c.Rect(3, 7, 3, 1, Palette.Mist);
                    c.Rect(12, 5, 4, 1, Palette.Mist);
                    break;
                case "Wide Barbs":
                    c.Rect(8, 1, 2, 7, Palette.Steel);
                    for (int i = 0; i < 5; i++) c.Rect(2 + i * 2, 7 + i * 2, 14 - i * 4, 2, Palette.SteelLight);
                    break;
                case "Swift Boots":
                    c.Rect(5, 2, 9, 1, Palette.SaffronLine);
                    c.Rect(5, 3, 8, 3, Palette.Saffron);
                    c.Rect(5, 5, 4, 8, Palette.Saffron);
                    c.Rect(9, 9, 6, 1, Palette.Mint);
                    c.Rect(10, 11, 6, 1, Palette.Mint);
                    c.Rect(11, 13, 5, 1, Palette.Mint);
                    break;
                case "Long Glow":
                    c.Circle(9f, 9f, 7.6f, Palette.Mist);
                    c.Circle(9f, 9f, 6.4f, Palette.Clear);
                    Flame(9f, 4f, Palette.Cream);
                    break;
                case "Spare Wick":
                    Flame(7f, 2f, Palette.Cream);
                    c.Rect(11, 11, 6, 2, Palette.Mint);
                    c.Rect(13, 9, 2, 6, Palette.Mint);
                    break;
                case "Relight":
                    Flame(9f, 3f, Palette.Lemon);
                    c.Rect(3, 3, 1, 1, Palette.Cream);
                    c.Rect(14, 6, 1, 1, Palette.Cream);
                    c.Rect(4, 11, 1, 1, Palette.Cream);
                    break;
                default:
                    c.Circle(9f, 9f, 5f, Palette.Mist);
                    break;
            }
            c.Outline(Palette.Ink);
            return c;
        }

        Sprite BuildFlame()
        {
            var c = new PixelCanvas(9, 12);
            c.Ellipse(4.5f, 4f, 3f, 3.2f, Palette.Saffron);
            c.Ellipse(4.5f, 7f, 1.8f, 3f, Palette.Saffron);
            c.Ellipse(4.5f, 4f, 1.5f, 2f, Palette.Cream);
            c.Outline(Palette.Ink);
            return c.ToSprite("Flame", new Vector2(4.5f, 1f), ppu);
        }

        // ---------------------------------------------------------------- Bubbles (no eyes: colour + centre mark)

        Sprite BuildBubble(BubbleKind kind, Color32 color, int d)
        {
            var c = new PixelCanvas(d, d);
            float cx = d * 0.5f, cy = d * 0.5f, r = d * 0.5f - 1f;
            c.Circle(cx, cy, r, color);

            // Darker crescent on the lower right
            var shade = Palette.Shade(color, 0.78f);
            for (int y = 0; y < d; y++)
                for (int x = 0; x < d; x++)
                {
                    if (!c.Filled(x, y)) continue;
                    float dx = x + 0.5f - (cx - r * 0.18f), dy = y + 0.5f - (cy + r * 0.18f);
                    if (dx * dx + dy * dy > r * r * 0.97f) c.Set(x, y, shade);
                }

            // Centre mark per type (colour-blind backup)
            var mark = Palette.Shade(color, 0.68f);
            switch (kind)
            {
                case BubbleKind.Trigloop:
                    {
                        float dr = Mathf.Max(0.8f, r * 0.11f), ring = r * 0.32f;
                        for (int i = 0; i < 3; i++)
                        {
                            float a = Mathf.PI * 0.5f + i * Mathf.PI * 2f / 3f;
                            c.Circle(cx + Mathf.Cos(a) * ring, cy + Mathf.Sin(a) * ring - r * 0.05f, dr, mark);
                        }
                        break;
                    }
                case BubbleKind.Lead:
                    {
                        int h = Mathf.Max(1, Mathf.RoundToInt(r * 0.22f));
                        c.RectOnFilled(0, Mathf.RoundToInt(cy - h * 0.5f), d, h, mark);
                        break;
                    }
                case BubbleKind.Lucky:
                    {
                        int len = Mathf.Max(1, Mathf.RoundToInt(r * 0.35f));
                        int mx = Mathf.FloorToInt(cx), my = Mathf.FloorToInt(cy);
                        c.Rect(mx - len, my, len * 2 + 1, 1, Palette.Cream);
                        c.Rect(mx, my - len, 1, len * 2 + 1, Palette.Cream);
                        break;
                    }
            }

            // Shine stays high on the body
            c.Ellipse(cx - r * 0.42f, cy + r * 0.42f, Mathf.Max(1f, r * 0.22f), Mathf.Max(0.8f, r * 0.14f), Palette.Cream);
            if (d >= 24) c.Circle(cx - r * 0.1f, cy + r * 0.66f, Mathf.Max(0.8f, r * 0.07f), Palette.Cream);

            c.Outline(Palette.Ink);
            return c.ToSprite($"Bubble_{kind}_{d}px", new Vector2(cx, cy), ppu);
        }

        Sprite BuildRing(int d)
        {
            var c = new PixelCanvas(d, d);
            float cx = d * 0.5f, cy = d * 0.5f, r = d * 0.5f - 1f;
            for (int y = 0; y < d; y++)
                for (int x = 0; x < d; x++)
                {
                    float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    if (Mathf.Abs(dist - r) > 0.6f) continue;
                    float a = Mathf.Atan2(dy, dx);
                    if (Mathf.FloorToInt((a + Mathf.PI) / (Mathf.PI * 2f) * 16f) % 2 == 0)
                        c.Set(x, y, Palette.Cream);
                }
            return c.ToSprite($"PopRing_{d}px", new Vector2(cx, cy), ppu);
        }

        // ---------------------------------------------------------------- Harpoon: barbed steel tip + 1 px twisted cable

        Sprite BuildHarpoonTip()
        {
            var c = new PixelCanvas(9, 11);
            c.Rect(4, 1, 1, 3, Palette.Steel);        // short shaft into the cable
            c.Rect(2, 3, 5, 1, Palette.SteelDark);    // barb bar
            c.Set(2, 2, Palette.SteelDark);           // barbs hook down
            c.Set(6, 2, Palette.SteelDark);
            c.Rect(3, 4, 3, 2, Palette.Steel);
            c.Rect(3, 4, 1, 2, Palette.SteelLight);
            c.Rect(4, 6, 1, 3, Palette.SteelLight);   // point
            c.Outline(Palette.Ink);
            return c.ToSprite("HarpoonTip", new Vector2(4.5f, 1f), ppu);
        }

        Sprite BuildRope()
        {
            var c = new PixelCanvas(1, 3);
            c.Set(0, 0, Palette.SteelDark);
            c.Set(0, 1, Palette.Steel);
            c.Set(0, 2, Palette.SteelLight);
            return c.ToSprite("Rope", new Vector2(0.5f, 0f), ppu);
        }

        // ---------------------------------------------------------------- Stage backgrounds (one per theme)

        readonly Sprite[] backgrounds = new Sprite[Themes.All.Length];

        /// <summary>The theme's background sprite: built on first use, then cached.</summary>
        public Sprite BackgroundFor(Theme theme)
        {
            int i = System.Array.IndexOf(Themes.All, theme);
            if (i < 0) i = 0;
            if (backgrounds[i] == null || backgrounds[i].texture == null)
                backgrounds[i] = BuildBackground(Themes.All[i], i + 1);
            return backgrounds[i];
        }

        Sprite BuildBackground(Theme theme, int world)
        {
            int w = Mathf.RoundToInt((ThemeBackgrounds.Right - ThemeBackgrounds.Left) * ppu);
            int h = Mathf.RoundToInt((ThemeBackgrounds.Top - ThemeBackgrounds.Bottom) * ppu);
            var c = new PixelCanvas(w, h);
            theme.paintBackground(c, theme, new System.Random(world)); // seeded by world, so a theme always looks the same
            return c.ToSprite("Background " + theme.name, new Vector2(-ThemeBackgrounds.Left * ppu, -ThemeBackgrounds.Bottom * ppu), ppu);
        }

        // ---------------------------------------------------------------- Touch controls (live in the ground strip)

        Sprite BuildFireButton(bool pressed)
        {
            var c = new PixelCanvas(46, 46);
            var face = pressed ? Palette.Shade(Palette.Mint, 0.8f) : Palette.Mint;
            c.Circle(23f, 23f, 21f, Palette.Shade(Palette.Mint, 0.62f));
            c.Circle(23f, pressed ? 22f : 24f, 20f, face);
            // Harpoon icon pointing up
            int ix = 23, iy = pressed ? 10 : 12;
            c.Rect(ix - 1, iy, 2, 16, Palette.Ink);
            for (int i = 0; i < 6; i++) c.Rect(ix - 6 + i, iy + 15 + i, 12 - 2 * i, 1, Palette.Ink);
            c.Outline(Palette.Ink);
            return c.ToSprite(pressed ? "FireButton_Pressed" : "FireButton", new Vector2(23f, 23f), ppu);
        }

        Sprite BuildMoveHint()
        {
            var c = new PixelCanvas(60, 13);
            var col = Palette.Mix(Palette.Lilac, Palette.Ink, 0.35f);
            for (int i = 0; i < 6; i++)
            {
                c.Rect(1 + i, 6 - i, 1, 1 + i * 2, col);       // left arrow
                c.Rect(58 - i, 6 - i, 1, 1 + i * 2, col);      // right arrow
            }
            for (int x = 12; x < 48; x += 4) c.Rect(x, 6, 2, 1, col);
            return c.ToSprite("MoveHint", new Vector2(30f, 6.5f), ppu);
        }

        Sprite BuildDisc(int d, Color32 color, string name)
        {
            var c = new PixelCanvas(d, d);
            c.Circle(d * 0.5f, d * 0.5f, d * 0.5f - 1f, color);
            c.Outline(Palette.Ink);
            return c.ToSprite(name, new Vector2(d * 0.5f, d * 0.5f), ppu);
        }

        Sprite BuildClimbArrow(bool up)
        {
            var c = new PixelCanvas(9, 6);
            for (int i = 0; i < 4; i++)
            {
                int y = up ? 1 + i : 4 - i;
                c.Rect(1 + i, y, 7 - 2 * i, 1, Palette.Mist);
            }
            c.Outline(Palette.Ink);
            return c.ToSprite(up ? "ClimbUpHint" : "ClimbDownHint", new Vector2(4.5f, 3f), ppu);
        }

        // ---------------------------------------------------------------- Road choice doors (painted in DoorArt)

        readonly Sprite[,] doors = new Sprite[Themes.All.Length, 2];

        /// <summary>The calm or hard door in a theme's stone, 30 x 45 px, pivot bottom centre. Built on first use, then cached.</summary>
        public Sprite DoorFor(Theme theme, bool hard)
        {
            int i = System.Array.IndexOf(Themes.All, theme);
            if (i < 0) i = 0;
            int h = hard ? 1 : 0;
            if (doors[i, h] == null || doors[i, h].texture == null)
                doors[i, h] = DoorArt.Paint(Themes.All[i], hard).ToSprite($"Door {Themes.All[i].name}{(hard ? " hard" : " calm")}",
                    new Vector2(DoorArt.Width * 0.5f, 0f), ppu);
            return doors[i, h];
        }

        // ---------------------------------------------------------------- Lava spikes (one tile wide, tiled along a strip)

        /// <summary>Five 1 px holes, one per spike, in the centre of each 3 px slot.</summary>
        Sprite BuildSpikeHidden()
        {
            var c = new PixelCanvas(15, 1);
            for (int i = 0; i < 5; i++) c.Set(i * 3 + 1, 0, Palette.Ink);
            return SpikeSprite(c, "SpikeHidden");
        }

        static readonly string[] SpikeWarnRows = { "ILI", "ISI" };
        static readonly string[] SpikeRaisedRows =
        {
            ".I.",
            ".L.",
            "ILI",
            "ILS",
            "ILS",
            "ISS",
            "ISI",
        };

        /// <summary>Five steel tips, 2 px tall, ink around the metal.</summary>
        Sprite BuildSpikeWarn() => PaintSpikes("SpikeWarn", Palette.Steel, SpikeWarnRows);

        /// <summary>
        /// Five steel triangles, 7 px tall and 3 px wide at the base. SteelLight is the left edge of the
        /// metal, Steel is the fill, and the ink pixels are the outline. Drawn by hand so five fit in one tile.
        /// Rows are top first.
        /// </summary>
        Sprite BuildSpikeRaised() => PaintSpikes("SpikeRaised", Palette.Steel, SpikeRaisedRows);

        // In the Lava theme the spikes are iron: the Steel fill is tinted 25% toward the theme's far colour.
        const float LavaSpikeTint = 0.25f;
        Sprite[] lavaSpikes; // hidden, warn, raised

        /// <summary>The fill colour of a spike strip in this theme. Only Lava tints it.</summary>
        public static Color32 SpikeFill(Theme theme) =>
            theme == Themes.ForWorld(6) ? Palette.Mix(Palette.Steel, theme.far, LavaSpikeTint) : Palette.Steel;

        public Sprite SpikeHiddenFor(Theme theme) => TintedSpike(theme, 0) ?? SpikeHidden;
        public Sprite SpikeWarnFor(Theme theme) => TintedSpike(theme, 1) ?? SpikeWarn;
        public Sprite SpikeRaisedFor(Theme theme) => TintedSpike(theme, 2) ?? SpikeRaised;

        /// <summary>The theme's tinted spike sprite (built on first use), or null when the theme uses the plain steel ones.</summary>
        Sprite TintedSpike(Theme theme, int which)
        {
            if (theme != Themes.ForWorld(6)) return null;
            if (lavaSpikes == null || lavaSpikes[which] == null || lavaSpikes[which].texture == null)
            {
                Color32 fill = SpikeFill(theme);
                lavaSpikes = new[]
                {
                    BuildSpikeHidden(),
                    PaintSpikes("SpikeWarnLava", fill, SpikeWarnRows),
                    PaintSpikes("SpikeRaisedLava", fill, SpikeRaisedRows),
                };
            }
            return lavaSpikes[which];
        }

        Sprite PaintSpikes(string name, Color32 fill, params string[] rowsTopFirst)
        {
            int height = rowsTopFirst.Length;
            var c = new PixelCanvas(15, height);
            for (int i = 0; i < 5; i++)
            {
                int x0 = i * 3;
                for (int r = 0; r < height; r++)
                {
                    string row = rowsTopFirst[r];
                    int y = height - 1 - r;
                    for (int x = 0; x < row.Length; x++)
                        if (row[x] != '.') c.Set(x0 + x, y, SpikeInk(row[x], fill));
                }
            }
            return SpikeSprite(c, name);
        }

        static Color32 SpikeInk(char ch, Color32 fill)
        {
            switch (ch)
            {
                case 'I': return Palette.Ink;
                case 'L': return Palette.SteelLight;
                case 'S': return fill;
                default: return Palette.Clear;
            }
        }

        Sprite SpikeSprite(PixelCanvas canvas, string name)
        {
            Sprite sprite = canvas.ToSprite(name, Vector2.zero, ppu);
            sprite.texture.wrapMode = TextureWrapMode.Repeat;
            return sprite;
        }

        // ------------------------------------------------------------------ bosses

        /// <summary>
        /// A boss body, the same body with the visor open (releasing a balloon), its cream hit-flash silhouette, and the
        /// cream lower band that blinks before a shot.
        /// </summary>
        public class BossSprites
        {
            public Sprite body, open, flash, tell;
        }

        readonly Dictionary<string, BossSprites> bossCache = new Dictionary<string, BossSprites>();
        readonly Dictionary<string, Sprite> bulletCache = new Dictionary<string, Sprite>();
        readonly Dictionary<string, Sprite> bouncyCache = new Dictionary<string, Sprite>();

        /// <summary>Height in pixels of the band at the bottom of a boss that blinks as the tell.</summary>
        public const int BossTellBand = 4;

        /// <summary>Pixels the shape's detail adds above and below the body rectangle (chimney, thorns, hood, spikes, teeth).</summary>
        public static (int top, int bottom) BossExtra(BossShape shape)
        {
            switch (shape)
            {
                case BossShape.Block: return (15, 0); // a 22 px chimney, 7 px of it sunk into the body
                case BossShape.Wide: return (8, 0);   // thorns
                case BossShape.Tall: return (10, 0);  // hood point
                case BossShape.Crown: return (12, 0); // spikes
                case BossShape.Jaw: return (0, 6);    // teeth
                default: return (0, 0);
            }
        }

        /// <summary>
        /// Placeholder boss painted from its spec and the world's colours (docs/design/expansion-art.md): a tileFill body
        /// in the spec's shape, a tileCap band across the top third (the middle for the round one), a skyTop visor slit 60%
        /// of the width, three tileShade rivets and a 1 px ink outline. The sprite is the body plus the shape's extra detail
        /// and a 1 px margin for the outline, with the pivot at the centre of the body rectangle.
        /// </summary>
        public BossSprites BossFor(Theme theme, BossSpec spec)
        {
            int w = Mathf.RoundToInt(spec.size.x * ppu), h = Mathf.RoundToInt(spec.size.y * ppu);
            string key = $"{theme.name}|{w}x{h}|{spec.shape}";
            if (bossCache.TryGetValue(key, out var cached) && cached.body != null && cached.body.texture != null) return cached;

            var (top, bottom) = BossExtra(spec.shape);
            int cw = w + 2, ch = h + top + bottom + 2;
            int ox = 1, oy = 1 + bottom;

            var closed = new PixelCanvas(cw, ch);
            PaintBoss(closed, theme, spec.shape, ox, oy, w, h, false);
            var opened = new PixelCanvas(cw, ch);
            PaintBoss(opened, theme, spec.shape, ox, oy, w, h, true);

            var tell = new PixelCanvas(cw, ch);
            for (int y = oy; y < oy + BossTellBand; y++)
                for (int x = ox; x < ox + w; x++)
                    if (closed.Filled(x, y)) tell.Set(x, y, Palette.Cream);

            closed.Outline(Palette.Ink);
            opened.Outline(Palette.Ink);
            var pivot = new Vector2(ox + w * 0.5f, oy + h * 0.5f);
            var sprites = new BossSprites
            {
                body = closed.ToSprite($"Boss_{key}", pivot, ppu),
                open = opened.ToSprite($"Boss_{key}_Open", pivot, ppu),
                flash = closed.Silhouette(Palette.Cream).ToSprite($"Boss_{key}_Flash", pivot, ppu),
                tell = tell.ToSprite($"Boss_{key}_Tell", pivot, ppu),
            };
            bossCache[key] = sprites;
            return sprites;
        }

        static void PaintBoss(PixelCanvas c, Theme theme, BossShape shape, int ox, int oy, int w, int h, bool open)
        {
            // Body.
            switch (shape)
            {
                case BossShape.Round:
                    c.Circle(ox + w * 0.5f, oy + h * 0.5f, Mathf.Min(w, h) * 0.5f, theme.tileFill);
                    break;
                case BossShape.Wide:
                    RoundedRect(c, ox, oy, w, h, 8, theme.tileFill);
                    break;
                default:
                    RoundedRect(c, ox, oy, w, h, 2, theme.tileFill);
                    break;
            }

            // The light band: across the top third, or across the middle of the round one.
            if (shape == BossShape.Round) c.RectOnFilled(ox, oy + h / 2 - h / 8, w, h / 4, theme.tileCap);
            else c.RectOnFilled(ox, oy + h - h / 3, w, h / 3, theme.tileCap);
            c.RectOnFilled(ox, oy, w, BossTellBand, theme.tileShade); // the lower band that blinks before a shot

            // Detail that grows past the body rectangle.
            switch (shape)
            {
                case BossShape.Block:
                {
                    // A brick chimney, 1 x 1.5 units, its foot sunk 7 px into the top band; a rim of four merlons and three notches.
                    int chW = 15, chH = 22, chX = ox + Mathf.RoundToInt(w * 0.68f) - chW / 2, chY = oy + h - 7;
                    c.Rect(chX, chY, chW, chH, theme.tileFill);
                    for (int row = 0; row < chH - 4; row += 4)
                    {
                        c.Rect(chX, chY + row + 3, chW, 1, theme.tileShade); // mortar line
                        int off = (row / 4) % 2 == 0 ? 4 : 0;
                        for (int bx = chX + off; bx < chX + chW; bx += 8) c.Rect(bx, chY + row, 1, 3, theme.tileShade);
                    }
                    int rimX = chX - 1, rimY = chY + chH - 4;
                    c.Rect(rimX, rimY, chW + 2, 4, theme.tileCap);
                    for (int n = 0; n < 3; n++) c.Rect(rimX + 2 + n * 5, rimY + 2, 3, 2, Palette.Clear);
                    break;
                }
                case BossShape.Wide:
                    // Five triangular thorns along the top.
                    for (int i = 0; i < 5; i++)
                    {
                        int cx = ox + w * (2 * i + 1) / 10;
                        Triangle(c, cx, oy + h - 1, 9, 8, true, theme.tileShade);
                    }
                    break;
                case BossShape.Tall:
                    // A pointed hood that rises from the top band, and rib lines on the lower body.
                    Triangle(c, ox + w / 2, oy + h - 1, w, 10, true, theme.tileCap);
                    for (int i = 0; i < 3; i++)
                    {
                        int ribW = Mathf.RoundToInt(w * 0.5f);
                        c.RectOnFilled(ox + (w - ribW) / 2, oy + BossTellBand + 8 + i * 4, ribW, 1, theme.tileShade);
                    }
                    break;
                case BossShape.Crown:
                    // Three tall spikes on top.
                    for (int i = 1; i <= 3; i++)
                        Triangle(c, ox + w * i / 4, oy + h - 1, 9, 12, true, theme.tileCap);
                    break;
                case BossShape.Jaw:
                    // Six downward teeth hanging under the body.
                    for (int i = 0; i < 6; i++)
                    {
                        int cx = ox + w * (2 * i + 1) / 12;
                        Triangle(c, cx, oy, Mathf.Max(3, w / 6 - 1), 6, false, theme.tileCap);
                    }
                    break;
            }

            // The visor slit, doubled in height while open.
            int visorW = Mathf.RoundToInt(w * 0.6f);
            int visorH = open ? 10 : 5;
            int visorX = ox + (w - visorW) / 2, visorY = oy + h / 2 - visorH / 2;
            c.Rect(visorX, visorY, visorW, visorH, theme.skyTop);
            if (shape == BossShape.Crown) c.Rect(visorX + 1, visorY + visorH / 2, visorW - 2, 1, theme.accent); // glow line

            if (shape != BossShape.Tall)
                for (int i = 1; i <= 3; i++)
                    c.RectOnFilled(ox + w * i / 4 - 1, oy + BossTellBand + 3, 3, 3, theme.tileShade); // rivets
        }

        /// <summary>A filled rectangle with corners rounded by radius r pixels.</summary>
        static void RoundedRect(PixelCanvas c, int x, int y, int w, int h, int r, Color32 col)
        {
            r = Mathf.Clamp(r, 0, Mathf.Min(w, h) / 2);
            for (int j = 0; j < h; j++)
                for (int i = 0; i < w; i++)
                {
                    float dx = i < r ? r - (i + 0.5f) : i >= w - r ? (i + 0.5f) - (w - r) : 0f;
                    float dy = j < r ? r - (j + 0.5f) : j >= h - r ? (j + 0.5f) - (h - r) : 0f;
                    if (dx * dx + dy * dy <= r * r) c.Set(x + i, y + j, col);
                }
        }

        /// <summary>
        /// An isosceles triangle standing on (or hanging from) the row baseY, centred on cx. up = true points up from
        /// baseY + 1; up = false points down from baseY - 1.
        /// </summary>
        static void Triangle(PixelCanvas c, int cx, int baseY, int baseW, int height, bool up, Color32 col)
        {
            for (int k = 0; k < height; k++)
            {
                float half = baseW * 0.5f * (1f - k / (float)height);
                int from = Mathf.RoundToInt(cx - half), to = Mathf.RoundToInt(cx + half);
                if (to <= from) to = from + 1;
                int y = up ? baseY + 1 + k : baseY - 1 - k;
                for (int x = from; x < to; x++) c.Set(x, y, col);
            }
        }

        /// <summary>A 9 px bouncy bullet: a tileCap disc with a 1 px ring in the theme's accent inside the ink outline.</summary>
        public Sprite BouncyBulletFor(Theme theme)
        {
            if (bouncyCache.TryGetValue(theme.name, out var cached) && cached != null && cached.texture != null) return cached;
            var c = new PixelCanvas(9, 9);
            c.Circle(4.5f, 4.5f, 3.5f, theme.accent);
            c.Circle(4.5f, 4.5f, 2.5f, theme.tileCap);
            c.Outline(Palette.Ink);
            var sprite = c.ToSprite($"BouncyBullet_{theme.name}", new Vector2(4.5f, 4.5f), ppu);
            bouncyCache[theme.name] = sprite;
            return sprite;
        }

        /// <summary>A 7 px aimed bullet: cream core, a 1 px ring in the theme's accent, 1 px ink outline (docs/design/expansion-art.md).</summary>
        public Sprite BulletFor(Theme theme)
        {
            if (bulletCache.TryGetValue(theme.name, out var cached) && cached != null && cached.texture != null) return cached;
            var c = new PixelCanvas(7, 7);
            c.Circle(3.5f, 3.5f, 2.5f, theme.accent);
            c.Circle(3.5f, 3.5f, 1.5f, Palette.Cream);
            c.Outline(Palette.Ink);
            var sprite = c.ToSprite($"Bullet_{theme.name}", new Vector2(3.5f, 3.5f), ppu);
            bulletCache[theme.name] = sprite;
            return sprite;
        }
    }
}
