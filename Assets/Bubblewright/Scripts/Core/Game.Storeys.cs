using System.Collections.Generic;
using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// One storey at a time. The camera pans up when Wick climbs through a hatch, the HUD and the thumb strip
    /// stay on the screen, and enemies on a storey he has not reached do not move or hurt him.
    /// </summary>
    public partial class Game
    {
        const float FloorStripWidth = 64f; // ThemeBackgrounds spans x = -32 to 32

        int activeStorey;
        float camPanY;
        bool[] awakeStoreys;
        int waveStorey = -1;
        bool debugStoreyStage;
        SpriteRenderer floorStrip;

        public int ActiveStorey => activeStorey;

        /// <summary>
        /// The next storey once his feet reach its floor. Never drops, and never past the top storey.
        /// A jump that clears more than one floor advances through each of them.
        /// </summary>
        public static int ComputeActiveStorey(int current, float feetY, int storeys)
        {
            if (storeys < 1) storeys = 1;
            int next = Mathf.Clamp(current, 0, storeys - 1);
            while (next < storeys - 1 && feetY >= (next + 1) * (float)StageMap.StoreyRows)
                next++;
            return next;
        }

        /// <summary>World point of the arena's top-left corner once the camera has panned by panY.</summary>
        public static Vector2 HudAnchorWorld(float panY) =>
            new Vector2(-Tuning.I.HalfWidth, Tuning.I.arenaHeight + panY);

        /// <summary>True when enemies on this storey may move and hurt Wick. Before a stage starts, everything may.</summary>
        public bool IsStoreyAwake(int storey)
        {
            if (awakeStoreys == null) return true;
            if (storey < 0 || storey >= awakeStoreys.Length) return false;
            return awakeStoreys[storey];
        }

        /// <summary>Negative means the top storey. A one-storey stage stays on storey 0, as waves do today.</summary>
        int ResolvedWaveStorey()
        {
            int top = Map != null ? Mathf.Max(0, Map.Storeys - 1) : 0;
            return waveStorey < 0 ? top : Mathf.Clamp(waveStorey, 0, top);
        }

        float WaveCeiling() => (ResolvedWaveStorey() + 1) * (float)StageMap.StoreyRows;

        void LoadStage(StagePlan plan)
        {
            stageReport = StageReport.From(run.Stage, plan, run.IsHard);
            Map = plan.map;
            ResetFrost();
            debugSurface = -1;
            debugTheme = -1;
            ApplyTheme(Themes.ForWorld(run.World));
            HidePathScene();
            SetupSpikes();
            EnsureFlames();
            ClearFlames();
            foreach (var b in bubbles) if (b) Destroy(b.gameObject);
            bubbles.Clear();
            hitBubbles.Clear();
            ClearRollers();
            ClearPickups();
            ClearBoss();
            fx.Clear();
            ApplyRunUpgrades();
            HideHarpoons();
            wick.ResetForStage(plan.wickPosition.x, plan.wickPosition.y, run.Hearts);
            ResetWeaponsForStage();
            WickFeet = wick.transform.position;
            WickBox = wick.Hitbox;
            wick.Waving = true;
            freeze = slowMo = shake = 0f;
            tileShakeFrames = 0;
            shotTimer = 0f;
            turnedForCelebration = celebrating = false;
            stormClock = stormBanner = 0f;
            storm = false;

            foreach (var s in plan.spawns)
            {
                int tier = Mathf.Clamp(s.tier, 0, T.tiers.Length - 1);
                SpawnBubble(s.kind, tier, s.position, s.direction >= 0 ? 1 : -1, 0f, false, s.ceramic, s.pogo, s.steel, s.mount, s.patient);
            }
            foreach (var s in plan.steelPogos)
                SpawnBubble(s.kind, Mathf.Clamp(s.tier, 0, T.tiers.Length - 1), s.position, 1, 0f, false, false, true, true);
            SpawnRollers(plan);
            SetupWaves(plan);
            SetupBoss();
            waveStorey = plan.waveStorey;
            BeginStoreys(plan.wickPosition.y);
            SetState(State.Intro);
        }

        /// <summary>Snaps the camera to the storey Wick starts on and shuts every hatch that still has enemies under it.</summary>
        void BeginStoreys(float feetY)
        {
            int count = Map != null ? Mathf.Max(1, Map.Storeys) : 1;
            activeStorey = Map != null ? Mathf.Clamp(Map.StoreyOf(feetY), 0, count - 1) : 0;
            camPanY = SnapToPixel(activeStorey * (float)StageMap.StoreyRows);
            awakeStoreys = new bool[count];
            awakeStoreys[activeStorey] = true;
            if (Map != null) Map.IncludeHatches = false;
            RefreshHatches();
            ApplyStoreyCamera();
        }

        /// <summary>Title and the road choice are one screen at the ground. The pan from the stage must not stay up.</summary>
        void ResetStoreyView()
        {
            activeStorey = 0;
            camPanY = 0f;
            awakeStoreys = null;
            waveStorey = -1;
            if (Map != null)
            {
                Map.HatchRules = false;
                Map.IncludeHatches = false;
            }
            ApplyStoreyCamera();
        }

        void SyncStoreys(float feetY)
        {
            if (Map == null) return;
            int count = Mathf.Max(1, Map.Storeys);
            if (awakeStoreys == null || awakeStoreys.Length != count)
            {
                awakeStoreys = new bool[count];
                int keep = Mathf.Clamp(activeStorey, 0, count - 1);
                awakeStoreys[keep] = true;
            }
            int next = ComputeActiveStorey(activeStorey, feetY, count);
            for (int s = activeStorey; s <= next; s++) WakeStorey(s);
            activeStorey = next;
            RefreshHatches();
        }

        void WakeStorey(int storey)
        {
            if (awakeStoreys == null || storey < 0 || storey >= awakeStoreys.Length || awakeStoreys[storey]) return;
            awakeStoreys[storey] = true;
            if (!storm) return;
            for (int i = 0; i < bubbles.Count; i++)
                if (bubbles[i] != null && bubbles[i].HomeStorey == storey) bubbles[i].OnStorm();
        }

        /// <summary>
        /// A storey is clear when its own required enemies are gone and, if the waves belong to it, they have all arrived.
        /// Waves on a higher storey must not hold the hatch shut: they cannot start until he is up there.
        /// </summary>
        bool StoreySettled(int storey)
        {
            for (int i = 0; i < bubbles.Count; i++)
            {
                var b = bubbles[i];
                if (b != null && !b.Steel && b.HomeStorey == storey) return false;
            }
            for (int i = 0; i < hitBubbles.Count; i++)
            {
                var b = hitBubbles[i];
                if (b != null && !b.Steel && b.HomeStorey == storey) return false;
            }
            for (int i = 0; i < rollers.Count; i++)
            {
                var r = rollers[i];
                if (r != null && !r.Steel && r.HomeStorey == storey) return false;
            }
            for (int i = 0; i < hitRollers.Count; i++)
            {
                var r = hitRollers[i];
                if (r != null && !r.Steel && r.HomeStorey == storey) return false;
            }
            if (waves != null && ResolvedWaveStorey() == storey && !waves.Done) return false;
            return true;
        }

        /// <summary>
        /// The ladder cells on the top row of storey f-1 are the hatch into storey f. Open only once that storey is
        /// clear and Wick has not gone up yet. The rest of the row stays a ceiling, so he cannot jump the shaft.
        /// </summary>
        void RefreshHatches()
        {
            if (Map == null) return;
            Map.HatchRules = Map.Storeys > 1;
            if (!Map.HatchRules) return;
            for (int f = 1; f < Map.Storeys; f++)
            {
                int row = StageMap.StoreyRows * f - 1;
                bool open = StoreySettled(f - 1) && activeStorey < f;
                for (int c = 0; c < StageMap.Cols; c++)
                    Map.SetHatchOpen(c, row, open);
            }
        }

        void TickStoreyPan(float dt)
        {
            float pixel = T.Pixel;
            float target = activeStorey * (float)StageMap.StoreyRows;
            float next = Mathf.MoveTowards(camPanY, target, T.storeyPanSpeed * Mathf.Max(0f, dt));
            next = Mathf.Round(next / pixel) * pixel;
            if ((target - camPanY) * (target - next) <= 0f) next = target;
            camPanY = next;
        }

        static float SnapToPixel(float y)
        {
            float pixel = Tuning.I.Pixel;
            return Mathf.Round(y / pixel) * pixel;
        }

        void ApplyStoreyCamera()
        {
            if (cam != null)
            {
                var pos = cameraHome;
                pos.y += camPanY;
                cam.transform.position = pos;
            }
            if (background != null)
                background.transform.localPosition = new Vector3(0f, camPanY, 0f);
            PlaceFloorStrip();
        }

        /// <summary>
        /// Covers the thumb strip so the storey below does not show behind the controls.
        /// Same 6 px floor cap and strip fill as the stage background.
        /// </summary>
        void RebuildFloorStrip()
        {
            if (world == null) return;
            var theme = CurrentTheme;
            int ppu = T.pixelsPerUnit;
            int w = Mathf.RoundToInt(FloorStripWidth * ppu);
            int h = Mathf.RoundToInt(T.groundStrip * ppu);
            var canvas = new PixelCanvas(w, h);
            canvas.Rect(0, 0, w, h - 7, theme.strip);
            canvas.Rect(0, h - 6, w, 6, theme.tileCap);
            canvas.Rect(0, h - 1, w, 1, Palette.Shade(theme.tileCap, 0.92f));
            canvas.Rect(0, h - 6, w, 1, Palette.Shade(theme.tileCap, 0.7f));
            canvas.Rect(0, h - 7, w, 1, Palette.Ink);
            var fresh = canvas.ToSprite("Floor Strip Mask", new Vector2(w * 0.5f, 0f), ppu);
            if (floorStrip == null)
            {
                var go = new GameObject("Floor Strip Mask");
                go.transform.SetParent(world, false);
                floorStrip = go.AddComponent<SpriteRenderer>();
                floorStrip.enabled = false;
                Sorting.Apply(floorStrip, Layer.UI, -2);
                ApplySpriteMaterial(floorStrip);
            }
            else ReplaceStripSprite(fresh);
            if (floorStrip.sprite != fresh) floorStrip.sprite = fresh;
        }

        void ReplaceStripSprite(Sprite fresh)
        {
            var old = floorStrip.sprite;
            floorStrip.sprite = fresh;
            if (old == null) return;
            var tex = old.texture;
            DestroyAsset(old);
            if (tex != null && tex != fresh.texture) DestroyAsset(tex);
        }

        void PlaceFloorStrip()
        {
            if (floorStrip == null) RebuildFloorStrip();
            if (floorStrip == null) return;
            floorStrip.enabled = camPanY > 0.01f;
            floorStrip.transform.localPosition = new Vector3(0f, camPanY - T.groundStrip, 0f);
        }

        void OnDestroy()
        {
            if (floorStrip == null || floorStrip.sprite == null) return;
            var sprite = floorStrip.sprite;
            var tex = sprite.texture;
            floorStrip.sprite = null;
            DestroyAsset(sprite);
            DestroyAsset(tex);
        }

        static void DestroyAsset(UnityEngine.Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(obj);
            else UnityEngine.Object.DestroyImmediate(obj);
        }

        void ToggleDebugStoreyStage()
        {
            debugStoreyStage = !debugStoreyStage;
            if (debugStoreyStage) testLayoutIndex = -1;
            run.Hearts = wick.Hearts;
            StartStage();
        }

        /// <summary>
        /// A two-storey look at the pan, the strip, the HUD and the hatch. Not a real stage.
        /// The crouch tunnel is on the left, the hatch is the centre ladder, and the floor Clinger waits on the right.
        /// He has to pop that Clinger too: it is a required enemy, so the hatch stays shut until it is gone.
        /// </summary>
        StagePlan StoreyDebugPlan()
        {
            const int ladder = 16;
            int rows = StageMap.StoreyRows * 2;
            var lines = new string[rows];
            for (int i = 0; i < rows; i++) lines[i] = new string('.', StageMap.Cols);
            for (int row = 0; row <= 16; row++)
            {
                string line = row == 1 ? TunnelRow(ladder) : row == 13 ? CapRow(ladder) : LadderRow(ladder);
                lines[rows - 1 - row] = line;
            }
            return new StagePlan
            {
                map = StageLayout.Parse(lines),
                layoutName = "Storey test",
                wickPosition = new Vector2(StageMap.CenterX(ladder), 0f),
                spawns = new List<SpawnSpec>
                {
                    new SpawnSpec { kind = BubbleKind.Gloop, tier = 2, position = new Vector2(-14f, 9f), direction = 1 },
                    new SpawnSpec { kind = BubbleKind.Gloop, tier = 2, position = new Vector2(6f, 7f), direction = -1 },
                    new SpawnSpec { kind = BubbleKind.Clinger, tier = 2, position = new Vector2(10f, 1f), direction = -1, pogo = true, mount = ClingerMount.Floor, patient = true },
                    new SpawnSpec { kind = BubbleKind.Gloop, tier = 2, position = new Vector2(-8f, 18f), direction = 1 },
                    new SpawnSpec { kind = BubbleKind.Gloop, tier = 2, position = new Vector2(8f, 22f), direction = -1 },
                },
            };
        }

        static string LadderRow(int col)
        {
            var chars = new string('.', StageMap.Cols).ToCharArray();
            chars[col] = 'H';
            return new string(chars);
        }

        static string TunnelRow(int col)
        {
            var chars = LadderRow(col).ToCharArray();
            for (int c = 4; c <= 11; c++) chars[c] = '#';
            return new string(chars);
        }

        static string CapRow(int col)
        {
            var chars = new string('#', StageMap.Cols).ToCharArray();
            chars[col] = 'H';
            return new string(chars);
        }
    }
}
