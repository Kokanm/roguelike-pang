using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

namespace Bubblewright
{
    /// <summary>
    /// W06 Lamp Lance: one charge. A press winds up for lanceWindUp seconds (Wick cannot move), then a beam lanceWidth
    /// wide runs from the weapon tip to the ceiling (to the floor when fired down) for lanceBeamTime seconds. Everything
    /// that touches it is removed whole, with no smaller bubbles left: every kind of bubble, steel, ceramic and faded
    /// Ghosts included, rolling balloons (steel too) and boss bullets. The boss takes lanceBossDamage once per beam.
    /// It goes through platforms, destroys every breakable block in its columns and melts Frost ice.
    /// In the air he hangs still for the whole shot. Only one can be held: a second crate stays on the floor.
    /// The charge is spent when the beam ends (or when the stage ends during the shot).
    /// </summary>
    [Preserve]
    public sealed class LampLance : WeaponSpec
    {
        public override string Id => "lance";
        public override string Name => "Lamp Lance";
        public override WeaponTier Tier => WeaponTier.VeryRare;
        public override int MaxAmmo => Mathf.Max(1, Tuning.I.lanceAmmo);
        public override int Order => 6;
        public override bool AllowSecondCopy => false;

        static readonly string[] Icon11 =
        {
            ".......R...",
            "LLLLLLRYR..",
            "SSSSSSRWYR.",
            "SDSDSSRWYR.",
            "LSSSSSRYR..",
            ".DS....R...",
            ".DS........",
            ".DD........",
        };

        static readonly string[] Hand =
        {
            ".RYYR.",
            "RYWWYR",
            "LSSSSD",
            ".LSSD.",
            ".LSSD.",
            ".LSSD.",
            ".LSSD.",
            ".LSSD.",
            ".DDDD.",
        };

        public override string[] IconRows => Icon11;
        public override string[] HeldRows => Hand;
        public override WeaponBehaviour CreateBehaviour() => new Behaviour();

        /// <summary>What one frame of the beam removed, for the effects and the tests.</summary>
        public struct BurnResult
        {
            public int bubbles;
            public int rollers;
            public int bullets;
            /// <summary>Breakable blocks destroyed.</summary>
            public int tiles;
            /// <summary>Frost ice spots melted.</summary>
            public int ice;
            /// <summary>The boss was in the beam and took its damage this frame.</summary>
            public bool boss;
        }

        /// <summary>The beam: lanceWidth wide, centred on the tip, from the tip up to the ceiling (down to the floor when down).</summary>
        public static Rect BeamRect(Vector2 tip, bool down)
        {
            var t = Tuning.I;
            float w = Mathf.Max(t.Pixel, t.lanceWidth);
            float top = down ? tip.y : t.arenaHeight;
            float bottom = down ? 0f : tip.y;
            if (top < bottom) (top, bottom) = (bottom, top);
            return new Rect(tip.x - w * 0.5f, bottom, w, top - bottom);
        }

        /// <summary>
        /// One frame of the beam. Removes whole everything overlapping it, ignoring tiles. The boss takes lanceBossDamage
        /// only while bossDone is false, and bossDone turns true when it does.
        /// </summary>
        public static BurnResult Burn(WeaponWorld world, Rect beam, ref bool bossDone, List<WeaponTarget> scratch = null)
        {
            var t = Tuning.I;
            var result = new BurnResult();
            float half = beam.width * 0.5f;
            // A thick vertical segment, trimmed by its half width so the round caps stay inside the rectangle's ends.
            float x = beam.center.x;
            float y0 = beam.yMin + Mathf.Min(half, beam.height * 0.5f), y1 = beam.yMax - Mathf.Min(half, beam.height * 0.5f);
            var found = world.AlongSegment(new Vector2(x, y0), new Vector2(x, y1), half, scratch);
            // Remove from a copy: removing changes the lists the query read from.
            var targets = found.ToArray();
            foreach (var target in targets)
            {
                switch (target.kind)
                {
                    case TargetKind.Bubble:
                        if (Vaporize(world, target.bubble)) result.bubbles++;
                        break;
                    case TargetKind.Roller:
                        if (Vaporize(world, target.roller)) result.rollers++;
                        break;
                    case TargetKind.Bullet:
                        if (world.Hit(target, 0f)) result.bullets++;
                        break;
                    case TargetKind.Boss:
                        if (bossDone || world.LiveBoss == null) break;
                        world.WeaponHitBoss(t.lanceBossDamage, target.position);
                        bossDone = true;
                        result.boss = true;
                        break;
                }
            }
            result.tiles = BreakTiles(world, beam);
            result.ice = MeltIce(world.Map, beam);
            return result;
        }

        /// <summary>Removes a bubble with no children. The running game scores it as a pop; tests just take it out.</summary>
        static bool Vaporize(WeaponWorld world, Bubble b)
        {
            if (b == null || b.Popped) return false;
            if (Game.LanceRemove(b)) return true;
            world.Bubbles.Remove(b);
            b.Popped = true;
            DestroyObject(b.gameObject);
            return true;
        }

        static bool Vaporize(WeaponWorld world, Roller r)
        {
            if (r == null || r.Popped) return false;
            if (Game.LanceRemove(r)) return true;
            world.Rollers.Remove(r);
            DestroyObject(r.gameObject);
            return true;
        }

        static void DestroyObject(GameObject go)
        {
            if (go == null) return;
            if (Application.isPlaying) Object.Destroy(go);
            else Object.DestroyImmediate(go);
        }

        /// <summary>Hits every breakable block in the beam's cells until it is gone. Returns how many were destroyed.</summary>
        static int BreakTiles(WeaponWorld world, Rect beam)
        {
            var map = world.Map;
            if (map == null) return 0;
            int destroyed = 0;
            Cells(beam, out int c0, out int c1, out int r0, out int r1);
            for (int c = c0; c <= c1; c++)
            for (int r = r0; r <= r1; r++)
            {
                if (map.Get(c, r).kind != TileKind.Breakable) continue;
                // HitTile takes one hit at a time; a few is always enough for the strongest block.
                for (int k = 0; k < 16 && map.Get(c, r).kind == TileKind.Breakable; k++) world.OnHitTile?.Invoke(c, r);
                if (map.Get(c, r).kind != TileKind.Breakable) destroyed++;
                else break; // nothing is listening (no OnHitTile): do not loop forever
            }
            return destroyed;
        }

        static int MeltIce(StageMap map, Rect beam)
        {
            if (Game.Ice == null || Game.Ice.Count == 0) return 0;
            Cells(beam, out int c0, out int c1, out int r0, out int r1);
            return Game.Ice.Melt(map, c0, c1, r0, r1, beam.yMin <= 0.01f);
        }

        /// <summary>The tile cells the beam overlaps, clamped to the map.</summary>
        static void Cells(Rect beam, out int c0, out int c1, out int r0, out int r1)
        {
            const float inset = 0.001f;
            c0 = Mathf.Max(0, StageMap.Col(beam.xMin + inset));
            c1 = Mathf.Min(StageMap.Cols - 1, StageMap.Col(beam.xMax - inset));
            r0 = Mathf.Max(0, StageMap.Row(beam.yMin + inset));
            r1 = Mathf.Min(StageMap.Rows - 1, StageMap.Row(beam.yMax - inset));
        }

        public enum Phase { Ready, WindUp, Beam, Fade, Done }

        /// <summary>Frames the beam takes to thin away after lanceBeamTime.</summary>
        public const int FadeFrames = LanceView.FadeFrames;

        public sealed class Behaviour : WeaponBehaviour
        {
            public Phase Phase { get; private set; } = Phase.Ready;
            /// <summary>Seconds in the current phase.</summary>
            public float PhaseTime { get; private set; }
            /// <summary>The beam this frame (empty before it fires).</summary>
            public Rect Beam { get; private set; }
            /// <summary>Everything the beam removed so far.</summary>
            public BurnResult Total { get; private set; }
            /// <summary>True once the charge was spent.</summary>
            public bool Spent { get; private set; }

            bool down, bossDone;
            Run run;
            WeaponSpec spec;
            Wick wick;
            readonly List<WeaponTarget> scratch = new List<WeaponTarget>();
            LanceView view;

            public override void Tick(WeaponContext ctx, float dt)
            {
                run = ctx.Run;
                spec = ctx.Spec;
                wick = ctx.Wick;
                var t = Tuning.I;
                PhaseTime += dt;
                switch (Phase)
                {
                    case Phase.Ready:
                        PhaseTime = 0f;
                        if (!ctx.Pressed || !ctx.CanFire) return;
                        if (run != null && run.WeaponAmmo < 1) return;
                        down = ctx.ShootDown;
                        Enter(Phase.WindUp);
                        Hold(true);
                        if (ctx.Wick != null) view ??= LanceView.Create(ctx.Wick.transform.parent);
                        Sfx.Play(Sound.Turn, 0.6f, 0.7f); // placeholder charge-up hum
                        break;

                    case Phase.WindUp:
                        Hold(true);
                        view?.WindUp(Muzzle(ctx), PhaseTime / Mathf.Max(0.01f, t.lanceWindUp));
                        if (PhaseTime < t.lanceWindUp) return;
                        Enter(Phase.Beam);
                        ctx.Wick?.OnFire();
                        Sfx.Play(Sound.Fire, 0.45f, 1f); // placeholder beam blast
                        Game.LanceShake(t.lanceBeamTime);
                        Fire(ctx);
                        break;

                    case Phase.Beam:
                        // On the ground he may walk once the beam is out (the beam stays in his hand); in the air he hangs still.
                        Hold(ctx.Wick != null && ctx.Wick.Mode == Mode.Fall);
                        if (PhaseTime >= t.lanceBeamTime)
                        {
                            Enter(Phase.Fade);
                            Hold(false);
                            view?.Fade(Beam, down, 0);
                            return;
                        }
                        Fire(ctx);
                        break;

                    case Phase.Fade:
                        int frame = Mathf.FloorToInt(PhaseTime / LanceView.FadeFrameTime);
                        if (frame < LanceView.FadeFrames)
                        {
                            view?.Fade(Beam, down, frame);
                            return;
                        }
                        Enter(Phase.Done);
                        view?.Hide();
                        Spend(ctx);
                        break;
                }
            }

            void Enter(Phase p)
            {
                Phase = p;
                PhaseTime = 0f;
            }

            Vector2 Muzzle(WeaponContext ctx)
            {
                if (ctx.Wick == null) return ctx.Muzzle;
                return down ? ctx.Wick.DownShotStart : ctx.Wick.LauncherTip;
            }

            void Fire(WeaponContext ctx)
            {
                Beam = BeamRect(Muzzle(ctx), down);
                var r = Burn(ctx.World, Beam, ref bossDone, scratch);
                var total = Total;
                total.bubbles += r.bubbles;
                total.rollers += r.rollers;
                total.bullets += r.bullets;
                total.tiles += r.tiles;
                total.ice += r.ice;
                total.boss |= r.boss;
                Total = total;
                view?.Beam(Beam, down);
            }

            void Hold(bool on)
            {
                if (wick != null) wick.HoldStill = on;
            }

            void Spend(WeaponContext ctx)
            {
                if (Spent) return;
                Spent = true;
                ctx.Spend();
            }

            /// <summary>
            /// Lets go of Wick and removes the beam. A shot that already started is spent even when the stage ended
            /// before the beam did, so a charge can never be fired twice.
            /// </summary>
            public override void Dispose()
            {
                Hold(false);
                if (!Spent && Phase != Phase.Ready && run != null && run.Weapon == spec)
                {
                    Spent = true;
                    run.SpendAmmo(WeaponSlot.Weapon);
                }
                view?.Destroy();
                view = null;
            }
        }
    }

    /// <summary>
    /// The Lamp Lance's look: a white dot growing on the lens during the wind-up, the screen dimmed, then the beam
    /// (red edges that flicker by a pixel, a yellow body and a white core) and its thinning away.
    /// </summary>
    sealed class LanceView
    {
        public const int FadeFrames = 4;
        public const float FadeFrameTime = 1f / 60f;
        const float DimAlpha = 0.15f;

        static readonly Dictionary<int, Sprite> rows = new Dictionary<int, Sprite>();
        static Sprite[] dots;
        static Sprite dimSprite;

        GameObject root;
        SpriteRenderer beam, dot, dim;
        int flicker;

        public static LanceView Create(Transform parent)
        {
            var v = new LanceView { root = new GameObject("LampLance") };
            if (parent != null) v.root.transform.SetParent(parent, false);
            v.dim = v.Renderer("Dim", 0);
            v.beam = v.Renderer("Beam", 3);
            v.dot = v.Renderer("Lens", 4);
            return v;
        }

        SpriteRenderer Renderer(string name, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            Sorting.Apply(sr, Layer.Shots, order);
            Game.ApplySpriteMaterial(sr);
            sr.enabled = false;
            return sr;
        }

        public void WindUp(Vector2 lens, float progress)
        {
            if (root == null) return;
            var d = Dots();
            int i = Mathf.Clamp(Mathf.FloorToInt(progress * d.Length), 0, d.Length - 1);
            dot.sprite = d[i];
            dot.transform.position = lens;
            dot.enabled = true;
            ShowDim(true);
        }

        public void Beam(Rect r, bool down)
        {
            if (root == null) return;
            dot.enabled = false;
            ShowDim(true);
            int w = Mathf.Max(1, Mathf.RoundToInt(r.width * Tuning.I.pixelsPerUnit));
            flicker++;
            // The red edges flicker by a pixel either side.
            Draw(r, down, (flicker / 2) % 2 == 0 ? w : w + 2);
        }

        /// <summary>Frame 0 to FadeFrames - 1: the beam thins down to one pixel.</summary>
        public void Fade(Rect r, bool down, int frame)
        {
            if (root == null) return;
            dot.enabled = false;
            ShowDim(false);
            int w = Mathf.Max(1, Mathf.RoundToInt(r.width * Tuning.I.pixelsPerUnit));
            float k = 1f - (frame + 1f) / FadeFrames;
            Draw(r, down, Mathf.Max(1, Mathf.RoundToInt(w * k)));
        }

        void Draw(Rect r, bool down, int widthPixels)
        {
            beam.sprite = Row(widthPixels);
            float px = Tuning.I.Pixel;
            // A one pixel high row stretched up the column: every row of the beam is the same.
            beam.transform.position = new Vector3(r.center.x, r.yMin, 0f);
            beam.transform.localScale = new Vector3(1f, Mathf.Max(px, r.height) / px, 1f);
            beam.enabled = true;
        }

        void ShowDim(bool on)
        {
            if (!on) { dim.enabled = false; return; }
            var t = Tuning.I;
            dim.sprite = DimSprite();
            dim.color = new Color(1f, 1f, 1f, DimAlpha);
            dim.transform.position = new Vector3(0f, t.arenaHeight * 0.5f, 0f);
            dim.transform.localScale = new Vector3(t.arenaWidth + 4f, t.arenaHeight + 4f, 1f);
            dim.enabled = true;
        }

        public void Hide()
        {
            if (root == null) return;
            beam.enabled = dot.enabled = dim.enabled = false;
        }

        public void Destroy()
        {
            if (root == null) return;
            if (Application.isPlaying) Object.Destroy(root);
            else Object.DestroyImmediate(root);
            root = null;
        }

        /// <summary>One row of the beam, w pixels wide, pivot at the bottom centre.</summary>
        static Sprite Row(int w)
        {
            if (rows.TryGetValue(w, out var s) && s != null) return s;
            var c = new PixelCanvas(w, 1);
            int edge = w >= 9 ? 2 : w >= 5 ? 1 : 0;
            int core = w >= 9 ? 3 : 1;
            var white = new Color32(255, 255, 255, 255);
            for (int x = 0; x < w; x++)
            {
                Color32 col = x < edge || x >= w - edge ? Palette.Spark : Palette.SparkCore;
                if (Mathf.Abs(x - (w - 1) * 0.5f) < core * 0.5f) col = white;
                c.Set(x, 0, col);
            }
            s = c.ToSprite("LanceBeam" + w, new Vector2(w * 0.5f, 0f), Tuning.I.pixelsPerUnit);
            rows[w] = s;
            return s;
        }

        /// <summary>The growing white dot on the lens: 1, 3 and 5 pixels with a yellow rim.</summary>
        static Sprite[] Dots()
        {
            if (dots != null) return dots;
            var list = new List<Sprite>();
            var white = new Color32(255, 255, 255, 255);
            foreach (int size in new[] { 1, 3, 5 })
            {
                var c = new PixelCanvas(size + 2, size + 2);
                float mid = (size + 1) * 0.5f;
                for (int y = 0; y < c.Height; y++)
                for (int x = 0; x < c.Width; x++)
                {
                    float d = Mathf.Max(Mathf.Abs(x - mid), Mathf.Abs(y - mid));
                    float round = Mathf.Abs(x - mid) + Mathf.Abs(y - mid);
                    if (round > size * 0.5f + 1f) continue;
                    c.Set(x, y, d <= size * 0.5f - 0.5f ? white : Palette.SparkCore);
                }
                list.Add(c.ToSprite("LanceLens" + size, new Vector2(c.Width * 0.5f, c.Height * 0.5f), Tuning.I.pixelsPerUnit));
            }
            return dots = list.ToArray();
        }

        static Sprite DimSprite()
        {
            if (dimSprite != null) return dimSprite;
            var c = new PixelCanvas(1, 1);
            c.Set(0, 0, Palette.Ink);
            // One pixel per unit, scaled to cover the arena.
            dimSprite = c.ToSprite("LanceDim", new Vector2(0.5f, 0.5f), 1);
            return dimSprite;
        }
    }

    public partial class Game
    {
        /// <summary>
        /// Lamp Lance: removes a bubble whole, with no children, scored and shown as a pop with red sparks.
        /// False when no game is running or the bubble is not in it (tests).
        /// </summary>
        public static bool LanceRemove(Bubble b)
        {
            var g = active;
            if (g == null || b == null || !g.bubbles.Contains(b)) return false;
            g.hitBubbles.Remove(b);
            var at = b.Position;
            g.Pop(b, false);
            g.fx.Sparkle(at, Palette.Spark);
            return true;
        }

        /// <summary>Lamp Lance: removes a rolling balloon (steel too) without splitting it, scored as a pop.</summary>
        public static bool LanceRemove(Roller r)
        {
            var g = active;
            if (g == null || r == null || !g.rollers.Contains(r)) return false;
            g.hitRollers.Remove(r);
            g.rollers.Remove(r);
            g.run.AddScore(Roller.ScoreOf(r.Tier));
            int fxTier = Mathf.Clamp(g.T.tiers.Length - 2 + r.Tier, 0, g.T.tiers.Length - 1);
            g.fx.Pop(r.Position, fxTier, g.T.rollerColor, g.T.popSwell);
            g.fx.Sparkle(r.Position, Palette.Spark);
            Sfx.Play(Sound.Pop, 1.3f + r.Tier * 0.2f, 0.8f);
            Destroy(r.gameObject);
            return true;
        }

        /// <summary>Lamp Lance: the screen shakes for this long (no game running: nothing).</summary>
        public static void LanceShake(float seconds)
        {
            if (active != null) active.shake = Mathf.Max(active.shake, seconds);
        }
    }

    public partial class Tuning
    {
        [Header("Weapon: Lamp Lance")]
        [Tooltip("Charges in a Lamp Lance crate. He never holds more than one crate's worth (a second crate stays on the floor).")]
        public int lanceAmmo = 1;
        [Tooltip("Seconds of wind-up before the beam. He cannot move, and bubbles can still hurt him.")]
        public float lanceWindUp = 0.35f;
        [Tooltip("Seconds the beam lasts. Everything that touches it during this time is removed whole.")]
        public float lanceBeamTime = 0.5f;
        [Tooltip("Beam width in units (1 = one tile).")]
        public float lanceWidth = 1f;
        [Tooltip("Boss health one beam takes, once however long the boss stays in it.")]
        public float lanceBossDamage = 6f;
    }
}
