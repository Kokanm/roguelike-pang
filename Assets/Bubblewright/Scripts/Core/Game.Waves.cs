using System.Collections.Generic;
using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// Reinforcements: the waves of a recipe that drops bubbles in from the ceiling after the start. WaveDirector decides
    /// when; this file spawns the bubbles, shows the warning markers, the HUD pips and holds the clear rule.
    /// </summary>
    public partial class Game
    {
        const float MarkerOn = 0.1f, MarkerOff = 0.05f;

        WaveDirector waves;               // null on stages without waves
        float stageTimeScale = 1f;        // the plan's storm clock multiplier
        readonly List<SpriteRenderer> markerIcons = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> markerLines = new List<SpriteRenderer>();
        readonly HashSet<WaveDirector.Marker> announcedMarkers = new HashSet<WaveDirector.Marker>();
        Transform markerRoot;

        /// <summary>A stage with waves is cleared only when no enemy is left and no wave or marker is still to come.</summary>
        public static bool StageClearedWithWaves(int bubblesAlive, int rollersAlive, bool wavesDone) =>
            StageCleared(bubblesAlive, rollersAlive) && wavesDone;

        bool WavesDone => waves == null || waves.Done;

        void SetupWaves(StagePlan plan)
        {
            HideWaveMarkers();
            announcedMarkers.Clear();
            stageTimeScale = run != null && run.IsBossStage ? 1f : Mathf.Max(0.01f, plan.timeScale);
            waves = plan.waves != null && plan.waves.Count > 0 && !run.IsBossStage ? new WaveDirector(plan.waves, bubbles.Count + RequiredRollers()) : null;
        }

        /// <summary>Plain dt, not the storm-scaled one: waves keep the same rule during the storm.</summary>
        void TickWaves(float dt)
        {
            if (waves == null) return;
            int enemies = bubbles.Count + hitBubbles.Count + RequiredRollers();
            waves.Tick(dt, enemies, wick.transform.position.x, SpawnWaveBubble);

            announcedMarkers.RemoveWhere(m => !Contains(waves.Markers, m));
            foreach (var m in waves.Markers)
                if (announcedMarkers.Add(m)) Sfx.Play(Sound.Bounce, 2f, 0.4f);
            UpdateWaveMarkers();
        }

        static bool Contains(IReadOnlyList<WaveDirector.Marker> list, WaveDirector.Marker m)
        {
            for (int i = 0; i < list.Count; i++) if (list[i] == m) return true;
            return false;
        }

        void SpawnWaveBubble(SpawnSpec spec, float x)
        {
            int tier = Mathf.Clamp(spec.tier, 0, T.tiers.Length - 1);
            float radius = T.tiers[tier].diameter * 0.5f;
            var b = SpawnBubble(spec.kind, tier, new Vector2(x, T.arenaHeight - radius - 0.05f), spec.direction >= 0 ? 1 : -1, 0f, false, spec.ceramic);
            if (storm) b.OnStorm();
        }

        // ------------------------------------------------------------------ markers

        void UpdateWaveMarkers()
        {
            var list = waves.Markers;
            for (int i = 0; i < list.Count; i++)
            {
                EnsureMarkerSprites(i);
                var m = list[i];
                float elapsed = Mathf.Max(0f, T.waveWarningTime - m.timeLeft);
                bool on = elapsed % (MarkerOn + MarkerOff) < MarkerOn;
                markerIcons[i].gameObject.SetActive(on);
                markerLines[i].gameObject.SetActive(on);
                markerIcons[i].transform.position = new Vector3(m.x, T.arenaHeight, 0f);
                markerLines[i].transform.position = new Vector3(m.x, T.arenaHeight - 9f * T.Pixel, 0f);
            }
            for (int i = list.Count; i < markerIcons.Count; i++)
            {
                markerIcons[i].gameObject.SetActive(false);
                markerLines[i].gameObject.SetActive(false);
            }
        }

        void EnsureMarkerSprites(int index)
        {
            if (markerRoot == null)
            {
                var go = new GameObject("Wave Markers");
                go.transform.SetParent(world, false);
                markerRoot = go.transform;
            }
            while (markerIcons.Count <= index)
            {
                markerIcons.Add(MakeMarkerSprite("Wave Marker", PlaceholderArt.I.WaveMarker, 2));
                markerLines.Add(MakeMarkerSprite("Wave Marker Line", PlaceholderArt.I.WaveMarkerLine, 1));
            }
        }

        SpriteRenderer MakeMarkerSprite(string name, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(markerRoot, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            Sorting.Apply(sr, Layer.FX, order);
            ApplySpriteMaterial(sr);
            go.SetActive(false);
            return sr;
        }

        void HideWaveMarkers()
        {
            for (int i = 0; i < markerIcons.Count; i++)
            {
                if (markerIcons[i]) markerIcons[i].gameObject.SetActive(false);
                if (markerLines[i]) markerLines[i].gameObject.SetActive(false);
            }
        }

        // ------------------------------------------------------------------ HUD

        /// <summary>One pip per wave beside the storm timer text: filled while the wave is still to come, hollow once called.</summary>
        void DrawWavePips(Vector2 topLeft, Vector2 topRight, float pad, int k)
        {
            if (waves == null || waves.Total == 0) return;
            float centre = (topLeft.x + topRight.x) * 0.5f;
            float y = topLeft.y + pad + 3.5f * k + (boss != null ? 4 * k : 0);
            float size = 5 * k;
            for (int i = 0; i < waves.Total; i++)
            {
                var r = new Rect(Mathf.Round(centre + 22 * k + i * 9 * k), Mathf.Round(y), size, size);
                bool coming = i >= waves.Total - waves.WavesLeft;
                if (coming)
                {
                    FillRect(r, Palette.Cream);
                    continue;
                }
                FillRect(new Rect(r.x, r.y, r.width, k), Palette.Cream);
                FillRect(new Rect(r.x, r.yMax - k, r.width, k), Palette.Cream);
                FillRect(new Rect(r.x, r.y, k, r.height), Palette.Cream);
                FillRect(new Rect(r.xMax - k, r.y, k, r.height), Palette.Cream);
            }
        }
    }
}
