using System;
using System.Collections.Generic;
using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// Short-lived flames left by Scorch bubbles where they land. Game owns one and ticks it with game time,
    /// so pause, hit-stop and slow motion freeze the burn. A flame covers one tile and hurts Wick like a spike.
    /// </summary>
    public class Flames : MonoBehaviour
    {
        const float FrameTime = 0.12f;

        sealed class Flame
        {
            public int col;
            public float surfaceY;
            public float timeLeft;
            public float age;
            public SpriteRenderer renderer;
        }

        readonly List<Flame> flames = new List<Flame>(); // oldest first
        readonly Stack<SpriteRenderer> pool = new Stack<SpriteRenderer>();

        public int Count => flames.Count;

        public static Flames Create(Transform parent)
        {
            var go = new GameObject("Flames");
            go.transform.SetParent(parent, false);
            return go.AddComponent<Flames>();
        }

        /// <summary>Light a flame on column col with its base at surfaceY. Does nothing outside the grid or over a ground gap.</summary>
        public void Add(StageMap map, int col, float surfaceY)
        {
            if (map == null || col < 0 || col >= StageMap.Cols || surfaceY < -0.05f || surfaceY > StageMap.Rows) return;
            if (surfaceY < 0.05f && map.IsGap(col)) return;

            foreach (var f in flames)
            {
                if (f.col != col || Mathf.Abs(f.surfaceY - surfaceY) > 0.05f) continue;
                f.timeLeft = Tuning.I.flameLife;
                return;
            }
            while (flames.Count >= Mathf.Max(1, Tuning.I.flameMax)) RemoveAt(0);

            var flame = new Flame { col = col, surfaceY = surfaceY, timeLeft = Tuning.I.flameLife, renderer = Rent() };
            flames.Add(flame);
            Place(flame);
        }

        /// <summary>Light a flame on whatever a bubble of this radius is resting on.</summary>
        public void AddUnder(StageMap map, Vector2 centre, float radius)
        {
            float surface = centre.y - radius;
            float snapped = Mathf.Round(surface);
            if (Mathf.Abs(surface - snapped) <= 0.05f) surface = snapped;
            Add(map, StageMap.Col(centre.x), surface);
        }

        public void Tick(StageMap map, float dt)
        {
            if (dt <= 0f) return;
            for (int i = flames.Count - 1; i >= 0; i--)
            {
                Flame f = flames[i];
                f.timeLeft -= dt;
                f.age += dt;
                bool gone = f.timeLeft <= 0f;
                if (!gone && f.surfaceY > 0.05f && map != null)
                    gone = !map.IsSolid(f.col, Mathf.RoundToInt(f.surfaceY) - 1);
                if (gone) { RemoveAt(i); continue; }
                Place(f);
            }
        }

        /// <summary>True when a flame's hurt box overlaps Wick. centreX is that flame's centre.</summary>
        public bool Hurts(Rect wickHitbox, out float centreX)
        {
            centreX = 0f;
            foreach (var f in flames)
            {
                Rect box = HurtBox(f);
                if (!box.Overlaps(wickHitbox)) continue;
                centreX = box.center.x;
                return true;
            }
            return false;
        }

        public void ForEachHurtBox(Action<Rect> draw)
        {
            if (draw == null) return;
            foreach (var f in flames) draw(HurtBox(f));
        }

        public void Clear()
        {
            for (int i = flames.Count - 1; i >= 0; i--) RemoveAt(i);
        }

        static Rect HurtBox(Flame f)
        {
            var t = Tuning.I;
            float left = StageMap.CellRect(f.col, 0).xMin + t.flameHurtInset;
            return new Rect(left, f.surfaceY, Mathf.Max(0f, 1f - t.flameHurtInset * 2f), t.flameHurtHeight);
        }

        void Place(Flame f)
        {
            var art = PlaceholderArt.I;
            f.renderer.sprite = ((int)(f.age / FrameTime) & 1) == 0 ? art.ScorchFlameA : art.ScorchFlameB;
            float pixel = Tuning.I.Pixel;
            f.renderer.transform.localPosition = new Vector3(
                Mathf.Round(StageMap.CenterX(f.col) / pixel) * pixel,
                Mathf.Round(f.surfaceY / pixel) * pixel, 0f);
        }

        SpriteRenderer Rent()
        {
            if (pool.Count > 0)
            {
                var sr = pool.Pop();
                sr.gameObject.SetActive(true);
                return sr;
            }
            var go = new GameObject("Flame");
            go.transform.SetParent(transform, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            Sorting.Apply(renderer, Layer.FX, 0);
            Game.ApplySpriteMaterial(renderer);
            return renderer;
        }

        void RemoveAt(int index)
        {
            Flame f = flames[index];
            flames.RemoveAt(index);
            if (f.renderer == null) return;
            f.renderer.gameObject.SetActive(false);
            pool.Push(f.renderer);
        }
    }
}
