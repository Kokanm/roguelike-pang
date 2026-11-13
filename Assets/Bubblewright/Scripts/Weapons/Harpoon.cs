using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// The starting weapon: a barbed steel tip trailing a 1 px twisted steel cable, fired straight up
    /// from the launcher tip at 20 u/s. The cable stays where it was fired. A bubble touching the cable
    /// or the tip pops. Gone when it pops something or reaches the ceiling or the underside of a platform. One on screen at a time.
    /// </summary>
    public class Harpoon : MonoBehaviour
    {
        public bool Active { get; private set; }
        /// <summary>Run upgrades: cable speed, extra hit width, and seconds the tip stays hooked in the ceiling.</summary>
        float? baseSpeed;
        /// <summary>The character's own shot speed; Wick's follows Tuning until a character sets it.</summary>
        public float BaseSpeed { get => baseSpeed ?? T.harpoonSpeed; set => baseSpeed = value; }
        public float SpeedMultiplier { get; set; } = 1f;
        public float WidthMultiplier { get; set; } = 1f;
        public float HoldTime { get; set; }

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
            topY = map != null ? map.CeilingAbove(x, from.y, out _) : T.arenaHeight;
            bottomY = (map != null ? Mathf.Max(0f, map.FloorBelow(x, from.y)) : 0f) + TailBottomPixels * px; // the cable never hangs into a gap
            this.chipColumn = chipColumn;
            chipNextRow = map != null ? Mathf.Max(0, StageMap.Row(map.FloorBelow(x, from.y))) : 0;
            tipY = from.y;
            held = -1f;
            clock = 0f;
            Active = true;
            tip.enabled = rope.enabled = true;
            foreach (var t in tail) t.enabled = true;
            UpdateVisual();
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
            if (held >= 0f)
            {
                held += dt;
                if (held >= HoldTime) Hide();
                else UpdateVisual(); // keep the tail settling while hooked
                return;
            }
            // The map can change under a flying cable (breakable tiles), so look again for what is above.
            var map = Game.Map;
            int hitRow = StageMap.Rows;
            topY = map != null ? map.CeilingAbove(x, baseY, out hitRow) : T.arenaHeight;
            tipY += BaseSpeed * SpeedMultiplier * dt;
            ChipPassedRows(map);
            if (tipY + TipHeight >= topY)
            {
                tipY = topY - TipHeight; // never past the underside
                if (map != null && hitRow < StageMap.Rows && map.Get(StageMap.Col(x), hitRow).kind == TileKind.Breakable)
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

        void ChipPassedRows(StageMap map)
        {
            if (map == null || chipColumn < 0 || chipColumn >= StageMap.Cols) return;
            int last = Mathf.Min(StageMap.Rows - 1, StageMap.Row(tipY + TipHeight));
            while (chipNextRow <= last)
            {
                int row = chipNextRow++;
                if (map.Get(chipColumn, row).kind == TileKind.Breakable)
                    hitTile?.Invoke(chipColumn, row);
            }
        }

        float TipHeight => (float)PlaceholderArt.HarpoonTipHeight / T.pixelsPerUnit;

        void UpdateVisual()
        {
            float px = 1f / T.pixelsPerUnit;
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
                return new Rect(x - 3.5f * px * w, tipY, 7f * px * w, TipHeight);
            }
        }

        /// <summary>True if the rectangle touches the cable or the tip.</summary>
        public bool Hits(Rect rect) => Active && (rect.Overlaps(CableRect) || rect.Overlaps(TipRect));

        /// <summary>True if the rolling balloon touches the cable or the tip.</summary>
        public bool Hits(Roller r)
        {
            if (!Active) return false;
            return r.Overlaps(CableRect) || r.Overlaps(TipRect);
        }

        /// <summary>True if the bubble touches the cable or the tip.</summary>
        public bool Hits(Bubble b)
        {
            if (!Active) return false;
            return b.Overlaps(CableRect) || b.Overlaps(TipRect);
        }
    }
}
