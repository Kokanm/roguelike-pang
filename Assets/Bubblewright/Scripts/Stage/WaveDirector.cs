using System;
using System.Collections.Generic;
using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// Decides when the bubbles of a Reinforcements stage arrive. A wave is called when few enemies are left, or when the
    /// previous group arrived long enough ago. Each bubble of a called wave first gets a warning marker at the ceiling;
    /// when the marker runs out the bubble is spawned there. Plain data and maths: no GameObjects, so tests can run it.
    /// </summary>
    public class WaveDirector
    {
        public class Marker
        {
            public SpawnSpec spec;
            public float x;
            public float timeLeft;
        }

        readonly List<List<SpawnSpec>> waves;
        readonly List<Marker> markers = new List<Marker>();
        int nextWave;
        float sinceLastArrival;
        bool callNow;

        public WaveDirector(List<List<SpawnSpec>> waves)
        {
            this.waves = waves ?? new List<List<SpawnSpec>>();
        }

        /// <summary>How many waves the stage has in all.</summary>
        public int Total => waves.Count;
        /// <summary>Waves not yet called.</summary>
        public int WavesLeft => waves.Count - nextWave;
        /// <summary>No waves left to call and no marker still counting down.</summary>
        public bool Done => WavesLeft <= 0 && markers.Count == 0;
        public IReadOnlyList<Marker> Markers => markers;

        /// <summary>Calls the next wave on the next Tick, whatever the counts (debug).</summary>
        public void CallNow() => callNow = true;

        /// <summary>
        /// Advances markers, spawns the ones that ran out through spawn(spec, x), and calls the next wave when its rule is
        /// met. enemiesAlive counts bubbles and rolling balloons that must be popped.
        /// </summary>
        public void Tick(float dt, int enemiesAlive, float wickX, Action<SpawnSpec, float> spawn)
        {
            var t = Tuning.I;
            sinceLastArrival += dt;

            bool spawnedAny = false;
            for (int i = 0; i < markers.Count; i++)
            {
                markers[i].timeLeft -= dt;
                if (markers[i].timeLeft > 0f) continue;
                var m = markers[i];
                markers.RemoveAt(i);
                i--;
                spawn?.Invoke(m.spec, m.x);
                spawnedAny = true;
            }
            if (spawnedAny && markers.Count == 0) sinceLastArrival = 0f;
            // The enemy count was taken before these bubbles joined it, so wait one tick before judging it.
            if (spawnedAny) return;

            if (markers.Count > 0 || WavesLeft <= 0) return;
            if (!callNow && enemiesAlive > t.waveEnemiesLeft && sinceLastArrival < t.waveMaxWait) return;
            callNow = false;
            CallWave(waves[nextWave], wickX);
            nextWave++;
        }

        void CallWave(List<SpawnSpec> wave, float wickX)
        {
            var t = Tuning.I;
            foreach (var spec in wave)
            {
                float radius = RadiusOf(spec.tier);
                float x = EntryX(spec.position.x, radius, wickX, markers);
                markers.Add(new Marker { spec = spec, x = x, timeLeft = t.waveWarningTime });
            }
        }

        static float RadiusOf(int tier)
        {
            var tiers = Tuning.I.tiers;
            return tiers[Mathf.Clamp(tier, 0, tiers.Length - 1)].diameter * 0.5f;
        }

        /// <summary>
        /// Where a bubble of this radius enters: the wished x, moved out of waveSafeDistance of Wick (to the side the wish
        /// was on when that fits inside the walls), then stepped sideways in 0.5 unit steps, away from Wick, until it is
        /// a diameter (the larger of the two) from every marker already placed, without leaving the walls or the safe zone.
        /// </summary>
        public static float EntryX(float wished, float radius, float wickX, IReadOnlyList<Marker> placed)
        {
            var t = Tuning.I;
            float safe = t.waveSafeDistance;
            float minX = -t.HalfWidth + radius, maxX = t.HalfWidth - radius;
            float x = Mathf.Clamp(wished, minX, maxX);

            if (Mathf.Abs(x - wickX) < safe)
            {
                float right = wickX + safe, left = wickX - safe;
                bool wantRight = x >= wickX;
                bool rightFits = right <= maxX, leftFits = left >= minX;
                if (wantRight && rightFits) x = right;
                else if (!wantRight && leftFits) x = left;
                else if (rightFits) x = right;
                else if (leftFits) x = left;
                else x = Mathf.Clamp(x, minX, maxX);
            }

            if (!Free(x, radius, placed))
            {
                float away = x >= wickX ? 1f : -1f;
                if (!Search(ref x, away, radius, wickX, minX, maxX, placed))
                {
                    float back = x;
                    Search(ref back, -away, radius, wickX, minX, maxX, placed);
                    if (Free(back, radius, placed)) x = back;
                }
            }
            return x;
        }

        static bool Search(ref float x, float dir, float radius, float wickX, float minX, float maxX, IReadOnlyList<Marker> placed)
        {
            float candidate = x;
            while (true)
            {
                candidate += dir * 0.5f;
                if (candidate < minX - 0.001f || candidate > maxX + 0.001f) return false;
                if (Mathf.Abs(candidate - wickX) < Tuning.I.waveSafeDistance - 0.001f) continue;
                if (!Free(candidate, radius, placed)) continue;
                x = candidate;
                return true;
            }
        }

        static bool Free(float x, float radius, IReadOnlyList<Marker> placed)
        {
            for (int i = 0; i < placed.Count; i++)
            {
                float need = Mathf.Max(radius, RadiusOf(placed[i].spec.tier)) * 2f;
                if (Mathf.Abs(placed[i].x - x) < need - 0.001f) return false;
            }
            return true;
        }
    }
}
