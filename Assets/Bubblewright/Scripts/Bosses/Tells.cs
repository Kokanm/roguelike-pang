using System;
using System.Collections.Generic;
using UnityEngine;

namespace Bubblewright
{
    public enum TellKind { EdgeGlow, DustColumn, LitLane, RedShadow }

    /// <summary>
    /// Every boss attack is shown before it lands: an edge glows, dust falls in a column, a lane is lit on the ceiling,
    /// a red shadow grows where a shell will come down. A tell starts the moment it is added and the attack fires when
    /// it ends, never sooner than bossTellMin seconds later. Ticked from Game.Update (so hit-stop, slow motion and
    /// pause freeze it). Also runs delayed actions (the shots of a burst). Pass a parent to draw the tells; null for none.
    /// </summary>
    public class Tells
    {
        class Entry
        {
            public TellKind kind;
            public Rect area;
            public float total, left;
            public Action fire;
            public bool isTell;
            public SpriteRenderer view;
        }

        readonly List<Entry> entries = new List<Entry>();
        readonly Transform parent;

        public Tells(Transform parent) { this.parent = parent; }

        /// <summary>Tells showing right now.</summary>
        public int Active
        {
            get
            {
                int n = 0;
                foreach (var e in entries) if (e.isTell) n++;
                return n;
            }
        }

        /// <summary>The time a tell shows for when a pattern asks for requested seconds: never under bossTellMin.</summary>
        public static float Lead(float requested) => Mathf.Max(requested, Tuning.I.bossTellMin);

        /// <summary>Shows a tell over area and runs fire when it ends. Returns the lead actually used.</summary>
        public float Add(TellKind kind, Rect area, float requestedLead, Action fire)
        {
            float lead = Lead(requestedLead);
            var e = new Entry { kind = kind, area = area, total = lead, left = lead, fire = fire, isTell = true };
            if (parent != null) e.view = MakeView(kind, area);
            entries.Add(e);
            return lead;
        }

        /// <summary>Runs fire after delay seconds, with no tell (the later shots of a burst).</summary>
        public void After(float delay, Action fire) =>
            entries.Add(new Entry { total = delay, left = delay, fire = fire });

        public void Tick(float dt)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                e.left -= dt;
                if (e.view != null) Pulse(e);
            }
            // Fire in the order they were due, then drop them (an attack may add more entries while it runs).
            var due = new List<Entry>();
            foreach (var e in entries) if (e.left <= 0f) due.Add(e);
            due.Sort((a, b) => a.left.CompareTo(b.left));
            foreach (var e in due)
            {
                entries.Remove(e);
                if (e.view) UnityEngine.Object.Destroy(e.view.gameObject);
                e.fire?.Invoke();
            }
        }

        /// <summary>Drops every tell and pending attack (phase break, boss defeat, new stage).</summary>
        public void Clear()
        {
            foreach (var e in entries) if (e.view) UnityEngine.Object.Destroy(e.view.gameObject);
            entries.Clear();
        }

        SpriteRenderer MakeView(TellKind kind, Rect area)
        {
            var go = new GameObject("Tell " + kind);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PlaceholderArt.I.Pixel;
            sr.color = Colour(kind);
            go.transform.position = area.center;
            go.transform.localScale = new Vector3(area.width * Tuning.I.pixelsPerUnit, area.height * Tuning.I.pixelsPerUnit, 1f);
            Sorting.Apply(sr, Layer.FX, 5);
            Game.ApplySpriteMaterial(sr);
            return sr;
        }

        static Color Colour(TellKind kind)
        {
            switch (kind)
            {
                case TellKind.EdgeGlow: return Palette.Lemon;
                case TellKind.DustColumn: return Palette.Cream;
                case TellKind.LitLane: return Palette.Lemon;
                default: return Palette.Coral;
            }
        }

        /// <summary>Fades in and flickers faster as the attack nears.</summary>
        static void Pulse(Entry e)
        {
            float progress = 1f - Mathf.Clamp01(e.left / e.total);
            float flicker = 0.5f + 0.5f * Mathf.Sin(progress * progress * 40f);
            var c = e.view.color;
            c.a = Mathf.Lerp(0.15f, 0.55f, progress) * Mathf.Lerp(0.6f, 1f, flicker);
            e.view.color = c;
        }
    }
}
