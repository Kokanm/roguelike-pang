using System;
using System.Collections.Generic;
using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// Retracting spike strips for one stage. Game creates them at stage start and ticks them with game time,
    /// so pause, hit-stop and slow motion freeze the cycle. Raised strips hurt Wick. Bubbles ignore them.
    /// </summary>
    public class Spikes : MonoBehaviour
    {
        const float RiseTime = 0.08f;
        const float WobbleStep = 0.06f;
        const float WarnPitch = 1.9f, WarnVolume = 0.22f;
        const float RisePitch = 1.45f, RiseVolume = 0.28f;

        enum SpikePhase { Down, Warn, Up }

        sealed class StripView
        {
            public SpriteRenderer renderer;
            public SpikePhase phase;
        }

        readonly List<StripView> views = new List<StripView>();
        StageMap map;
        float clock;

        public static Spikes Create(Transform parent, StageMap stageMap)
        {
            var go = new GameObject("Spikes");
            go.transform.SetParent(parent, false);
            var spikes = go.AddComponent<Spikes>();
            spikes.map = stageMap;
            spikes.BuildViews();
            return spikes;
        }

        public void Tick(float dt)
        {
            if (dt > 0f) clock += dt;
            Refresh(true);
        }

        /// <summary>Swap the strips after the map's list changes (the debug toggle). The cycle clock keeps running.</summary>
        public void Rebuild(StageMap stageMap)
        {
            map = stageMap;
            for (int i = transform.childCount - 1; i >= 0; i--)
                DestroyImmediate(transform.GetChild(i).gameObject);
            views.Clear();
            BuildViews();
        }

        /// <summary>True when a raised strip's hurt box overlaps Wick. centreX is that strip's centre.</summary>
        public bool Hurts(Rect wickHitbox, out float centreX)
        {
            centreX = 0f;
            if (map == null) return false;
            foreach (var strip in map.spikes)
            {
                if (PhaseAt(CycleTime(strip.phase)) != SpikePhase.Up) continue;
                Rect box = HurtBox(strip);
                if (!box.Overlaps(wickHitbox)) continue;
                centreX = box.center.x;
                return true;
            }
            return false;
        }

        public void ForEachRaisedHurtBox(Action<Rect> draw)
        {
            if (map == null || draw == null) return;
            foreach (var strip in map.spikes)
                if (PhaseAt(CycleTime(strip.phase)) == SpikePhase.Up)
                    draw(HurtBox(strip));
        }

        void BuildViews()
        {
            if (map == null) return;
            for (int i = 0; i < map.spikes.Count; i++)
            {
                views.Add(new StripView
                {
                    renderer = MakeRenderer(transform),
                    phase = SpikePhase.Down,
                });
            }
            Refresh(false);
        }

        static SpriteRenderer MakeRenderer(Transform parent)
        {
            var go = new GameObject("Spike Strip");
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            Sorting.Apply(sr, Layer.Stage, 1);
            Game.ApplySpriteMaterial(sr);
            return sr;
        }

        void Refresh(bool sounds)
        {
            if (map == null) return;
            int count = Mathf.Min(views.Count, map.spikes.Count);
            for (int i = 0; i < count; i++)
                RefreshOne(views[i], map.spikes[i], sounds);
        }

        void RefreshOne(StripView view, StageMap.SpikeStrip strip, bool sounds)
        {
            float time = CycleTime(strip.phase);
            SpikePhase phase = PhaseAt(time);
            if (sounds && phase != view.phase && OnScreen(strip))
            {
                // A large step can skip the warning. Play the tick as well as the rise.
                if (phase == SpikePhase.Warn || (phase == SpikePhase.Up && view.phase == SpikePhase.Down))
                    Sfx.Play(Sound.Bounce, WarnPitch, WarnVolume);
                if (phase == SpikePhase.Up)
                    Sfx.Play(Sound.Fire, RisePitch, RiseVolume);
            }
            view.phase = phase;

            var art = PlaceholderArt.I;
            Theme theme = Game.CurrentTheme;
            Sprite sprite = phase == SpikePhase.Up ? art.SpikeRaisedFor(theme)
                : phase == SpikePhase.Warn ? art.SpikeWarnFor(theme)
                : art.SpikeHiddenFor(theme);
            SpriteRenderer sr = view.renderer;
            sr.sprite = sprite;
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.tileMode = SpriteTileMode.Continuous;

            float scale = phase == SpikePhase.Up ? RiseScale(time) : 1f;
            float native = sprite.bounds.size.y;
            sr.size = new Vector2(strip.width, Mathf.Max(0.0001f, native * scale));
            sr.enabled = phase != SpikePhase.Up || scale > 0.001f;

            float shift = phase == SpikePhase.Warn ? WarnShift(time) : 0f;
            float pixel = Tuning.I.Pixel;
            float left = StageMap.CellRect(strip.col, 0).xMin;
            float surface = strip.row < 0 ? 0f : strip.row + 1f;
            sr.transform.localPosition = new Vector3(
                Mathf.Round((left + shift) / pixel) * pixel,
                Mathf.Round(surface / pixel) * pixel,
                0f);
        }

        float CycleTime(float phase)
        {
            var tuning = Tuning.I;
            float period = tuning.spikeDownTime + tuning.spikeWarnTime + tuning.spikeUpTime;
            if (period <= 0.0001f) return 0f;
            return Mathf.Repeat(clock + phase, period);
        }

        static SpikePhase PhaseAt(float time)
        {
            var tuning = Tuning.I;
            if (time < tuning.spikeDownTime) return SpikePhase.Down;
            if (time < tuning.spikeDownTime + tuning.spikeWarnTime) return SpikePhase.Warn;
            return SpikePhase.Up;
        }

        static float RiseScale(float time)
        {
            var tuning = Tuning.I;
            float along = time - tuning.spikeDownTime - tuning.spikeWarnTime;
            float scale = 1f;
            if (along < RiseTime) scale = along / RiseTime;
            else if (along > tuning.spikeUpTime - RiseTime) scale = (tuning.spikeUpTime - along) / RiseTime;
            return Mathf.Clamp01(scale);
        }

        static float WarnShift(float time)
        {
            float warnT = time - Tuning.I.spikeDownTime;
            int step = Mathf.FloorToInt(warnT / WobbleStep);
            if (step < 0) step = 0;
            return ((step & 1) == 0 ? -1f : 1f) * Tuning.I.Pixel;
        }

        /// <summary>The strip's width, 2 px in from each end, from the surface up spikeHeightPixels.</summary>
        static Rect HurtBox(StageMap.SpikeStrip strip)
        {
            float pixel = Tuning.I.Pixel;
            float inset = 2f * pixel;
            float left = StageMap.CellRect(strip.col, 0).xMin + inset;
            float surface = strip.row < 0 ? 0f : strip.row + 1f;
            return new Rect(left, surface, Mathf.Max(0f, strip.width - inset * 2f), Tuning.I.spikeHeightPixels * pixel);
        }

        static bool OnScreen(StageMap.SpikeStrip strip)
        {
            Camera cam = Camera.main;
            if (cam == null || !cam.orthographic) return false;
            float halfH = cam.orthographicSize;
            float halfW = halfH * cam.aspect;
            if (halfW < 1f) return true;
            Vector3 p = cam.transform.position;
            float left = StageMap.CellRect(strip.col, 0).xMin;
            float right = left + strip.width;
            float y = strip.row < 0 ? 0f : strip.row + 1f;
            return right > p.x - halfW && left < p.x + halfW && y < p.y + halfH && y + 1f > p.y - halfH;
        }
    }
}
