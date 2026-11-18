using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// The starting weapon: a barbed steel tip trailing a 1 px twisted steel cable, fired straight up
    /// from the launcher tip at Tuning.harpoonSpeed (15 u/s for Wick). The cable stays where it was fired. A bubble touching the cable
    /// or the tip pops. Gone when it pops something or reaches the ceiling or the underside of a platform. One on screen at a time.
    /// Fired with Down in the air it flies straight down instead (FireDown) and stops at the floor or platform below,
    /// breaking a breakable tile it lands on.
    /// </summary>
    public class Harpoon : MonoBehaviour
    {
        public bool Active { get; private set; }
        /// <summary>True for a downward shot (fire + Down in the air).</summary>
        public bool Down { get; private set; }
        /// <summary>Run upgrades: cable speed, extra hit width, and seconds the tip stays hooked in the ceiling.</summary>
        float? baseSpeed;
        /// <summary>The character's own shot speed; Wick's follows Tuning until a character sets it.</summary>
        public float BaseSpeed { get => baseSpeed ?? T.harpoonSpeed; set => baseSpeed = value; }
        public float SpeedMultiplier { get; set; } = 1f;
        public float WidthMultiplier { get; set; } = 1f;
        public float HoldTime { get; set; }
        /// <summary>
        /// The cables fired together by a harpoon head share one volley, so the boss takes damage once per volley
        /// (BossFight.HarpoonHits). Null for a normal shot. Every Fire clears it.
        /// </summary>
        public HarpoonVolley Volley { get; set; }
        /// <summary>
        /// A Skewer Harpoon cable keeps going after a hit until its pops run out (HarpoonPierce). Null for a normal shot.
        /// Every Fire clears it.
        /// </summary>
        public HarpoonPierce Pierce { get; set; }

        /// <summary>True if this harpoon leaves the thing alone for now (a skewer and what it just hit or what was just born).</summary>
        public bool Ignores(Object thing) => Pierce != null && Pierce.Ignores(thing);

        /// <summary>
        /// Called by the hit loops when this harpoon hits thing. A normal harpoon is used up; a skewer counts one pop and
        /// stays up until none are left. ends forces it to go (steel).
        /// </summary>
        public void Spend(Object thing, bool ends = false)
        {
            if (ends || Pierce == null || !Pierce.Strike(thing)) Hide();
        }

        Tuning T => Tuning.I;
        SpriteRenderer tip, rope;
        System.Action<int, int> hitTile;
        float baseY, tipY, x;
        float topY, bottomY;   // where the tip stops (underside of the first solid tile above) and where the tail ends
        float held = -1f;
        float clock;
        int chipColumn = -1;
        int chipNextRow;

        // The cable hangs from the launcher down to just above the floor. The loose end swings when fired,
        // then settles straight. Pieces are 3 px, the length of the cable's shading pattern, so they match the rest of it.
        const int TailBottomPixels = 2;   // gap left above the floor
        const int SegmentPixels = 3;
        const float SwaySpeed = 7f;       // radians per second
        const float SettleTime = 1.8f;    // seconds until the tail hangs straight: about two full swings
        SpriteRenderer[] tail;

        public static Harpoon Create(Transform parent, System.Action<int, int> hitTile = null)
        {
            var go = new GameObject("Harpoon");
            go.transform.SetParent(parent, false);
            var h = go.AddComponent<Harpoon>();
            h.hitTile = hitTile;
            var art = PlaceholderArt.I;

            var ropeGo = new GameObject("Cable");
            ropeGo.transform.SetParent(go.transform, false);
            h.rope = ropeGo.AddComponent<SpriteRenderer>();
            h.rope.sprite = art.Rope;
            h.rope.drawMode = SpriteDrawMode.Tiled;
            h.rope.tileMode = SpriteTileMode.Continuous;
            Sorting.Apply(h.rope, Layer.Shots, 0);
            Game.ApplySpriteMaterial(h.rope);

            var tipGo = new GameObject("Tip");
            tipGo.transform.SetParent(go.transform, false);
            h.tip = tipGo.AddComponent<SpriteRenderer>();
            h.tip.sprite = art.HarpoonTip;
            Sorting.Apply(h.tip, Layer.Shots, 1);
            Game.ApplySpriteMaterial(h.tip);

            int segments = Mathf.CeilToInt((Tuning.I.launcherTip.y - 1f / Tuning.I.pixelsPerUnit * TailBottomPixels) * Tuning.I.pixelsPerUnit / SegmentPixels);
            h.tail = new SpriteRenderer[Mathf.Max(0, segments)];
            for (int i = 0; i < h.tail.Length; i++)
            {
                var seg = new GameObject("Tail " + i);
                seg.transform.SetParent(go.transform, false);
                var sr = seg.AddComponent<SpriteRenderer>();
                sr.sprite = art.Rope;
                sr.drawMode = SpriteDrawMode.Tiled;
                sr.tileMode = SpriteTileMode.Continuous;
                Sorting.Apply(sr, Layer.Shots, 0);
                Game.ApplySpriteMaterial(sr);
                h.tail[i] = sr;
            }

            h.Hide();
            return h;
        }

        public void Fire(Vector2 from, int chipColumn = -1)
        {
            // Snap to the pixel grid so the 1 px cable never straddles two columns.
            float px = 1f / T.pixelsPerUnit;
            x = Mathf.Round(from.x / px) * px;
            baseY = from.y;
            var map = Game.Map;
            topY = map != null ? map.ShotCeiling(x, from.y, out _) : T.arenaHeight;
            float tailFloor = map != null ? map.ShotFloor(x, from.y) : 0f;
            if (tailFloor < 0f) tailFloor = 0f;
            bottomY = tailFloor + TailBottomPixels * px; // the cable never hangs into a gap or the storey below
            // Fired in the air, the cable hangs only as far as his feet were, like a shot from the ground.
            bottomY = Mathf.Min(from.y, Mathf.Max(bottomY, from.y - T.launcherTip.y + TailBottomPixels * px));
            Down = false;
            Volley = null;
            Pierce = null;
            ClearAngle();
            tip.transform.localRotation = Quaternion.identity;
            this.chipColumn = chipColumn;
            chipNextRow = map != null ? Mathf.Max(0, StageMap.Row(Mathf.Max(0f, map.ShotFloor(x, from.y)))) : 0;
            tipY = from.y;
            held = -1f;
            clock = 0f;
            Active = true;
            tip.enabled = rope.enabled = true;
            foreach (var t in tail) t.enabled = true;
            UpdateVisual();
        }

        /// <summary>
        /// Fire + Down in the air: the tip flies straight down from his feet. The cable runs from where it was fired down
        /// to the tip, with no hanging tail. It stops on the floor or platform below (or the floor line over a gap).
        /// </summary>
        public void FireDown(Vector2 from)
        {
            float px = 1f / T.pixelsPerUnit;
            x = Mathf.Round(from.x / px) * px;
            baseY = from.y;
            bottomY = DownStop(Game.Map, out _);
            chipColumn = -1;
            tipY = from.y;
            held = -1f;
            clock = 0f;
            Down = true;
            Volley = null;
            Pierce = null;
            ClearAngle();
            Active = true;
            tip.transform.localRotation = Quaternion.Euler(0f, 0f, 180f);
            tip.enabled = rope.enabled = true;
            foreach (var t in tail) t.enabled = false;
            UpdateVisual();
        }

        /// <summary>Top of the first solid tile under the shot (and that tile's row, or -1 for the ground), never below the floor line.</summary>
        float DownStop(StageMap map, out int hitRow)
        {
            hitRow = -1;
            if (map == null) return 0f;
            float floor = map.ShotFloor(x, baseY);
            float baseLine = map.StoreyBase(baseY);
            if (floor <= baseLine) return Mathf.Max(0f, floor);
            hitRow = Mathf.RoundToInt(floor) - 1;
            return floor;
        }

        public void Hide()
        {
            Active = false;
            if (tip) tip.enabled = false;
            if (rope) rope.enabled = false;
            if (tail != null) foreach (var t in tail) if (t) t.enabled = false;
        }

        public void Tick(float dt)
        {
            if (!Active) return;
            clock += dt;
            Pierce?.Tick(dt);
            if (held >= 0f)
            {
                held += dt;
                if (held >= HoldTime) Hide();
                else UpdateVisual(); // keep the tail settling while hooked
                return;
            }
            // The map can change under a flying cable (breakable tiles), so look again for what is above.
            var map = Game.Map;
            if (Angled)
            {
                TickAngled(map, dt);
                return;
            }
            if (Down)
            {
                TickDown(map, dt);
                return;
            }
            int hitRow = map != null ? map.Rows : StageMap.StoreyRows;
            topY = map != null ? map.ShotCeiling(x, baseY, out hitRow) : T.arenaHeight;
            tipY += BaseSpeed * SpeedMultiplier * dt;
            ChipPassedRows(map);
            if (tipY + TipHeight >= topY)
            {
                tipY = topY - TipHeight; // never past the underside
                if (map != null && hitRow < map.Rows && map.Get(StageMap.Col(x), hitRow).kind == TileKind.Breakable)
                {
                    UpdateVisual();
                    hitTile?.Invoke(StageMap.Col(x), hitRow);
                    Hide();
                    return;
                }
                if (HoldTime <= 0f)
                {
                    UpdateVisual();
                    Hide();
                    return;
                }
                held = 0f;
            }
            UpdateVisual();
        }

        void TickDown(StageMap map, float dt)
        {
            float stop = DownStop(map, out int hitRow);
            bottomY = stop;
            tipY -= BaseSpeed * SpeedMultiplier * dt;
            if (tipY <= stop)
            {
                tipY = stop;
                UpdateVisual();
                if (map != null && hitRow >= 0 && map.Get(StageMap.Col(x), hitRow).kind == TileKind.Breakable)
                {
                    hitTile?.Invoke(StageMap.Col(x), hitRow);
                    Hide();
                    return;
                }
                if (HoldTime <= 0f || stop <= 0f && map != null && map.IsGap(StageMap.Col(x)))
                {
                    Hide();
                    return;
                }
                held = 0f;
                return;
            }
            UpdateVisual();
        }

        void ChipPassedRows(StageMap map)
        {
            if (map == null || chipColumn < 0 || chipColumn >= StageMap.Cols) return;
            int storeyTop = (map.StoreyOf(baseY) + 1) * StageMap.StoreyRows - 1;
            int last = Mathf.Min(storeyTop, StageMap.Row(tipY + TipHeight));
            while (chipNextRow <= last)
            {
                int row = chipNextRow++;
                if (map.Get(chipColumn, row).kind == TileKind.Breakable)
                    hitTile?.Invoke(chipColumn, row);
            }
        }

        // ------------------------------------------------------------------ angled cables (Vee Harpoon)

        Vector2 angleBase, angleTip, angleDir;

        /// <summary>True for a slanted cable (FireAngled): a straight segment from where it was fired to its tip.</summary>
        public bool Angled { get; private set; }
        /// <summary>Where a slanted cable was fired from.</summary>
        public Vector2 AngledBase => angleBase;
        /// <summary>The front point of a slanted cable's tip.</summary>
        public Vector2 AngledTip => angleTip;

        void ClearAngle()
        {
            Angled = false;
            if (rope) rope.transform.localRotation = Quaternion.identity;
        }

        /// <summary>
        /// A slanted cable: the tip flies along direction (pointing down counts as a down shot) and the cable is the
        /// straight segment from where it was fired to the tip. It does not sway and has no hanging tail. The tip stops at
        /// the first solid tile, wall, ceiling or the floor; a breakable tile there takes one hit and the cable goes, any
        /// other surface hooks it for HoldTime like the straight harpoon. Hit tests use the segment with the cable's width.
        /// </summary>
        public void FireAngled(Vector2 from, Vector2 direction)
        {
            float px = 1f / T.pixelsPerUnit;
            angleDir = direction.sqrMagnitude > 1e-8f ? direction.normalized : Vector2.up;
            angleBase = new Vector2(Mathf.Round(from.x / px) * px, from.y);
            angleTip = angleBase;
            x = angleBase.x;
            baseY = tipY = from.y;
            Down = angleDir.y < 0f;
            Volley = null;
            Pierce = null;
            Angled = true;
            chipColumn = -1;
            held = -1f;
            clock = 0f;
            Active = true;
            var turn = Quaternion.FromToRotation(Vector3.up, angleDir);
            tip.transform.localRotation = turn;
            rope.transform.localRotation = turn;
            tip.enabled = rope.enabled = true;
            foreach (var t in tail) t.enabled = false;
            UpdateVisual();
        }

        void TickAngled(StageMap map, float dt)
        {
            float travel = BaseSpeed * SpeedMultiplier * dt;
            float step = 0.5f / T.pixelsPerUnit;
            int steps = Mathf.Max(1, Mathf.CeilToInt(travel / step));
            Vector2 move = angleDir * (travel / steps);
            for (int i = 0; i < steps; i++)
            {
                Vector2 next = angleTip + move;
                if (AngledStop(map, next, out int col, out int row))
                {
                    if (map != null && row >= 0 && map.Get(col, row).kind == TileKind.Breakable)
                    {
                        UpdateVisual();
                        hitTile?.Invoke(col, row);
                        Hide();
                        return;
                    }
                    UpdateVisual();
                    bool overGap = map != null && next.y <= 0f && map.IsGap(StageMap.Col(next.x));
                    if (HoldTime <= 0f || overGap) Hide();
                    else held = 0f;
                    return;
                }
                angleTip = next;
            }
            UpdateVisual();
        }

        /// <summary>True when the tip at p has left the arena or entered a solid tile (that tile's column and row, or row -1).</summary>
        bool AngledStop(StageMap map, Vector2 p, out int col, out int row)
        {
            col = StageMap.Col(p.x);
            row = -1;
            float ceil = map != null ? map.StoreyCeiling(baseY) : T.arenaHeight;
            float floor = map != null ? Mathf.Max(0f, map.StoreyBase(baseY)) : 0f;
            if (p.x <= -T.HalfWidth || p.x >= T.HalfWidth || p.y <= floor || p.y >= ceil) return true;
            if (map == null) return false;
            int r = StageMap.Row(p.y);
            if (col < 0 || col >= StageMap.Cols || !map.RowInStorey(r, baseY) || !map.IsSolid(col, r)) return false;
            row = r;
            return true;
        }

        void UpdateAngledVisual(float px)
        {
            float length = Vector2.Distance(angleBase, angleTip);
            rope.transform.position = angleBase;
            rope.size = new Vector2(px, Mathf.Max(px, length - TipHeight + px));
            tip.transform.position = angleTip - angleDir * TipHeight;
        }

        static Rect AngledBounds(Vector2 a, Vector2 b, float halfWidth) =>
            Rect.MinMaxRect(Mathf.Min(a.x, b.x) - halfWidth, Mathf.Min(a.y, b.y) - halfWidth,
                Mathf.Max(a.x, b.x) + halfWidth, Mathf.Max(a.y, b.y) + halfWidth);

        /// <summary>
        /// True if a thing with this rectangle test touches the slanted cable or its tip. The cable is checked as small
        /// squares one pixel apart along the segment, so each thing keeps its own exact shape test (circles, Pogos,
        /// boss bodies). reach skips anything whose rough circle is nowhere near the segment.
        /// </summary>
        bool AngledTouches(System.Func<Rect, bool> overlaps, Vector2 centre, float reach)
        {
            float px = 1f / T.pixelsPerUnit;
            float half = px * WidthMultiplier;
            float tipHalf = 3.5f * px * WidthMultiplier;
            Vector2 closest = WeaponWorld.ClosestOnSegment(angleBase, angleTip, centre);
            if (Vector2.Distance(closest, centre) > reach + tipHalf + px) return false;

            Vector2 tipBack = angleTip - angleDir * TipHeight;
            for (int k = 0; k <= 4; k++) // the barbed tip, as wide as the straight harpoon's
            {
                Vector2 q = Vector2.Lerp(tipBack, angleTip, k / 4f);
                if (overlaps(new Rect(q.x - tipHalf, q.y - tipHalf, tipHalf * 2f, tipHalf * 2f))) return true;
            }
            float length = Vector2.Distance(angleBase, angleTip);
            int n = Mathf.Max(1, Mathf.CeilToInt(length / px));
            for (int i = 0; i <= n; i++)
            {
                Vector2 q = Vector2.Lerp(angleBase, angleTip, i / (float)n);
                if (overlaps(new Rect(q.x - half, q.y - half, half * 2f, half * 2f))) return true;
            }
            return false;
        }

        float TipHeight => (float)PlaceholderArt.HarpoonTipHeight / T.pixelsPerUnit;

        void UpdateVisual()
        {
            float px = 1f / T.pixelsPerUnit;
            if (Angled)
            {
                UpdateAngledVisual(px);
                return;
            }
            if (Down)
            {
                // Turned upside down about its pivot, the point ends at tipY.
                tip.transform.position = new Vector3(x, tipY + TipHeight, 0f);
                rope.transform.position = new Vector3(x, tipY + TipHeight, 0f);
                rope.size = new Vector2(px, Mathf.Max(px, baseY - tipY - TipHeight + px));
                return;
            }
            tip.transform.position = new Vector3(x, tipY, 0f);
            rope.transform.position = new Vector3(x, baseY, 0f);
            rope.size = new Vector2(px, Mathf.Max(px, tipY - baseY + px));
            UpdateTail(px);
        }

        /// <summary>Stacked pieces from the launcher down; the lower they hang, the further they sway (whole pixels), fading to straight.</summary>
        void UpdateTail(float px)
        {
            int n = tail.Length;
            for (int i = 0; i < n; i++)
            {
                float k = (i + 1f) / n; // 0 at the launcher, 1 at the loose end
                float settle = Mathf.Clamp01(1f - clock / SettleTime);
                float sway = Mathf.Sin(clock * SwaySpeed - k * 2.5f) * 2.5f * k * k * settle;
                float segTop = baseY - i * SegmentPixels * px;
                float segBottom = Mathf.Max(bottomY, segTop - SegmentPixels * px);
                float px_x = x + Mathf.Round(sway) * px;
                tail[i].transform.position = new Vector3(px_x, segBottom, 0f);
                tail[i].size = new Vector2(px, Mathf.Max(px, segTop - segBottom));
            }
        }

        /// <summary>The rectangle of the cable, from just above the floor up to the tip. The same one Hits uses.</summary>
        public Rect CableRect
        {
            get
            {
                float px = 1f / T.pixelsPerUnit;
                float w = WidthMultiplier;
                if (Angled) return AngledBounds(angleBase, angleTip, px * w);
                if (Down) return new Rect(x - px * w, tipY, px * 2f * w, Mathf.Max(0f, baseY - tipY));
                return new Rect(x - px * w, bottomY, px * 2f * w, tipY - bottomY);
            }
        }

        /// <summary>The rectangle of the barbed tip. The same one Hits uses.</summary>
        public Rect TipRect
        {
            get
            {
                float px = 1f / T.pixelsPerUnit;
                float w = WidthMultiplier;
                if (Angled) return AngledBounds(angleTip - angleDir * TipHeight, angleTip, 3.5f * px * w);
                return new Rect(x - 3.5f * px * w, tipY, 7f * px * w, TipHeight);
            }
        }

        /// <summary>True if the rectangle touches the cable or the tip.</summary>
        public bool Hits(Rect rect)
        {
            if (!Active) return false;
            if (Angled) return AngledTouches(rect.Overlaps, rect.center, Mathf.Max(rect.width, rect.height) * 0.75f);
            return rect.Overlaps(CableRect) || rect.Overlaps(TipRect);
        }

        /// <summary>True if the rolling balloon touches the cable or the tip.</summary>
        public bool Hits(Roller r)
        {
            if (!Active) return false;
            if (Angled) return AngledTouches(r.Overlaps, r.Position, r.Radius);
            return r.Overlaps(CableRect) || r.Overlaps(TipRect);
        }

        /// <summary>True if the bubble touches the cable or the tip.</summary>
        public bool Hits(Bubble b)
        {
            if (!Active) return false;
            if (Angled) return AngledTouches(b.Overlaps, b.Position, b.Radius * 1.5f); // a Pogo reaches past its radius
            return b.Overlaps(CableRect) || b.Overlaps(TipRect);
        }
    }

    /// <summary>Cables a harpoon head fired together. The first one to reach the boss deals BossDamage; the rest deal none.</summary>
    public class HarpoonVolley
    {
        public int BossDamage = 1;
        public bool BossHit;
    }
}
