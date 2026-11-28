using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// Leaving a cleared stage through a door. An ordinary stage shows one exit door once it is clear, on the right of
    /// Wick's storey (the right-most spot he can reach). The boss of worlds 1 to 5 shows the two road doors (step 14) in
    /// the next world's stone through the whole fight, shut and unmarked; when the boss falls they slide open and show
    /// which one is the hard road for the whole next world, and its debuff. The final boss has no doors: the run is complete.
    /// Flow: Play, Clear (the doors open during the celebration), upgrade pick, Path (Wick walks through a door), next stage.
    /// The run is saved when the next stage starts, as before. Side 0 is the left road door, side 1 the right one.
    /// </summary>
    public partial class Game
    {
        Run.RoadChoice road;
        int doorCount;             // 0, 1 (exit door) or 2 (road doors), from Run.DoorCount
        bool RoadDoors => doorCount == 2;
        readonly Vector2[] doorFeet = new Vector2[2];
        readonly bool[] doorArmed = new bool[2]; // a road door takes him only after he stepped out of it, so standing in one when the boss fell picks nothing
        int exitStorey;            // the storey the exit door was placed on
        float doorOpenClock = -1f; // < 0 while shut; counts up while it slides open
        bool doorsShown;           // the exit door appears only when the stage is clear
        int pathSelected;          // the road door nearest Wick, lit
        int pathChosen = -1;       // -1 until he goes through a door, or a tap sends him to one
        bool pathArrived;          // he reached the door and the fade has started
        float pathRunTime, pathFade;
        readonly Rect[] pathCardRects = new Rect[2]; // GUI space, written by OnGUI, read by TickPath
        readonly Rect[] pathDoorRects = new Rect[2];

        Transform pathRoot;
        readonly SpriteRenderer[] doorRenderers = new SpriteRenderer[2];

        int HardSide => road.hardIsRight ? 1 : 0;
        float DoorX(int side) => doorFeet[side].x;
        bool DoorsOpen => doorOpenClock >= 0f;

        /// <summary>Keeps a door's 30 px sprite on the 32-unit screen: its centre stays a door's half width in from each side.</summary>
        public static float ClampDoorX(float x)
        {
            float half = DoorArt.Width * 0.5f / Tuning.I.pixelsPerUnit;
            float edge = StageMap.Cols * 0.5f - half;
            return Mathf.Clamp(x, -edge, edge);
        }

        /// <summary>Called from LoadStage: the road doors stand shut through a boss fight; an exit door waits for the clear.</summary>
        void SetupDoors()
        {
            EnsureDoorRenderers();
            doorCount = Run.DoorCount(run.Stage);
            doorOpenClock = -1f;
            doorsShown = false;
            pathChosen = -1;
            pathArrived = false;
            pathRunTime = pathFade = 0f;
            for (int i = 0; i < 2; i++) pathCardRects[i] = pathDoorRects[i] = Rect.zero;
            if (RoadDoors)
            {
                road = run.RollRoad(); // seed and stage only, so a resumed boss shows the same doors
                pathSelected = 1 - HardSide;
                doorFeet[0] = new Vector2(-T.pathDoorX, 0f);
                doorFeet[1] = new Vector2(T.pathDoorX, 0f);
                doorsShown = true;
            }
            RefreshDoorSprites();
        }

        void EnsureDoorRenderers()
        {
            if (pathRoot != null) return;
            pathRoot = new GameObject("Doors").transform;
            pathRoot.SetParent(world, false);
            for (int i = 0; i < 2; i++)
            {
                var go = new GameObject(i == 0 ? "Door 0" : "Door 1");
                go.transform.SetParent(pathRoot, false);
                doorRenderers[i] = go.AddComponent<SpriteRenderer>();
                Sorting.Apply(doorRenderers[i], Layer.Stage, -5); // over the sky, behind tiles, bubbles and Wick
                ApplySpriteMaterial(doorRenderers[i]);
            }
        }

        /// <summary>Raised when a door starts to open and each time its leaves move on a frame: a sound can be plugged in here
        /// (frame is 0..OpenFrames - 1, progress 0..1). Nothing is subscribed yet; a placeholder creak plays meanwhile.</summary>
        public static event System.Action<int, float> DoorCreak;

        int DoorFrameIndex => !DoorsOpen ? 0 : DoorAnim.Frame(DoorAnim.OpenAmount(doorOpenClock, T.doorFadeInTime, T.doorOpenTime));
        bool DoorsFullyOpen => DoorsOpen && DoorAnim.IsOpen(doorOpenClock, T.doorFadeInTime, T.doorOpenTime);

        /// <summary>Road doors are dim and see-through through the fight (they sit behind platforms), an exit door starts invisible.</summary>
        float DoorStartAlpha => RoadDoors ? T.doorFightAlpha : 0f;

        void OnDoorCreak(int frame)
        {
            float progress = frame / (float)(DoorArt.OpenFrames - 1);
            DoorCreak?.Invoke(frame, progress);
            Sfx.Play(Sound.Skid, 0.45f, 0.35f + 0.1f * progress); // placeholder creak: slow low scrape
        }

        void RefreshDoorSprites()
        {
            if (pathRoot == null) return;
            pathRoot.gameObject.SetActive(doorsShown && doorCount > 0);
            if (!doorsShown) return;
            int frame = DoorFrameIndex;
            var theme = RoadDoors ? Themes.ForWorld(Mathf.Min(run.World + 1, T.worlds)) : Themes.ForWorld(run.World);
            for (int i = 0; i < 2; i++)
            {
                bool used = i < doorCount;
                doorRenderers[i].enabled = used;
                if (!used) continue;
                DoorMark mark = !RoadDoors ? DoorMark.Lantern : !DoorsOpen ? DoorMark.None : i == HardSide ? DoorMark.Hard : DoorMark.Lantern;
                doorRenderers[i].sprite = PlaceholderArt.I.DoorFrame(theme, mark, frame);
                doorRenderers[i].transform.position = new Vector3(doorFeet[i].x, doorFeet[i].y, 0f);
                float alpha = DoorAnim.Alpha(doorOpenClock, DoorStartAlpha, T.doorFadeInTime);
                float shade = Mathf.Lerp(T.doorFightShade, 1f, alpha >= 1f ? 1f : Mathf.InverseLerp(DoorStartAlpha, 1f, alpha));
                doorRenderers[i].color = new Color(shade, shade, shade, alpha);
            }
        }

        void HidePathScene()
        {
            doorsShown = false;
            doorCount = 0;
            if (pathRoot != null) pathRoot.gameObject.SetActive(false);
        }

        /// <summary>The stage is clear: the exit door appears beside Wick's reach, or the road doors show their marks, and they slide open.</summary>
        void OpenDoors()
        {
            if (doorCount == 0 || DoorsOpen) return;
            if (!RoadDoors) PlaceExitDoor();
            doorsShown = true;
            doorOpenClock = 0f;
            PopLeftovers();
            RefreshDoorSprites();
        }

        /// <summary>The right-most spot Wick can reach on his storey; moved again if he climbs to a higher storey before leaving.</summary>
        void PlaceExitDoor()
        {
            Vector2 feet = wick.transform.position;
            var spot = StageValidator.ExitDoorSpot(run.World, Map, feet);
            doorFeet[0] = new Vector2(ClampDoorX(spot.x), spot.y);
            exitStorey = Map != null ? Map.StoreyOf(spot.y + 0.01f) : 0;
        }

        /// <summary>Steel bubbles and steel rolling balls never needed popping; they burst when the doors open so the way out is clear.</summary>
        void PopLeftovers()
        {
            for (int i = bubbles.Count - 1; i >= 0; i--)
            {
                var b = bubbles[i];
                if (b == null) continue;
                fx.Pop(b.Position, b.Tier, b.Steel ? (Color)Palette.Steel : T.Kind(b.Kind).color, T.popSwell);
                Destroy(b.gameObject);
            }
            bubbles.Clear();
            hitBubbles.Clear();
            foreach (var r in rollers) if (r) fx.Pop(r.Position, r.Tier, Palette.Steel, T.popSwell);
            ClearRollers();
        }

        void TickDoors(float dt)
        {
            if (!DoorsOpen || doorOpenClock > DoorAnim.TotalTime(T.doorFadeInTime, T.doorOpenTime) + 0.1f) return;
            int before = DoorFrameIndex;
            float fadeTime = T.doorFadeInTime;
            bool wasFading = doorOpenClock < fadeTime;
            doorOpenClock += dt;
            if (wasFading && doorOpenClock >= fadeTime) OnDoorCreak(0); // the leaves start to move
            int after = DoorFrameIndex;
            if (after != before) OnDoorCreak(after);
            if (after == DoorArt.OpenFrames - 1 && before != after) // wide open: a sparkle
                for (int i = 0; i < doorCount; i++)
                    fx.Sparkle(doorFeet[i] + new Vector2(0f, DoorArt.Height * 0.85f / T.pixelsPerUnit), i == HardSide && RoadDoors ? Palette.Lemon : Palette.Cream);
            RefreshDoorSprites();
        }

        /// <summary>The pick is over: Wick walks to a door. Without doors (only after the final boss, which goes to the title) the next stage starts.</summary>
        void FinishPick()
        {
            if (doorCount == 0)
            {
                NextStage();
                return;
            }
            OpenDoors();
            pathChosen = -1;
            pathArrived = false;
            pathRunTime = pathFade = 0f;
            lastMoveDir = input.Move > 0f ? 1 : input.Move < 0f ? -1 : 0;
            wick.Waving = false;
            wick.Turn(false); // back to the camera, facing the doors
            wick.GapDamageEnabled = false; // the stage is won: a gap only sends him back to the edge
            for (int i = 0; i < 2; i++) doorArmed[i] = i < doorCount && (!RoadDoors || !AtDoor(i));
            SetState(State.Path);
        }

        /// <summary>
        /// Wick walks, climbs and jumps as in play, but cannot fire and nothing hurts him. He goes through a door by
        /// standing in it and pressing UP, once the door has opened. A road door can also be picked by a tap or click on it or its card, or with 1 and 2; he then
        /// runs to it by himself.
        /// </summary>
        void TickPath(float dt)
        {
            TickDoors(dt);
            if (pathArrived)
            {
                wick.Tick(0f, 0f, dt, false);
                pathFade += dt;
                if (pathFade >= T.pathFadeTime) LeaveThroughDoor();
                return;
            }
            if (pathChosen >= 0)
            {
                TickPathRun(dt);
                return;
            }

            if (Map != null) SyncStoreys(wick.FeetY);
            wick.Tick(input.Move, input.Climb, input.JumpPressed, dt, true);
            if (Map != null)
            {
                SyncStoreys(wick.FeetY);
                Map.IncludeHatches = false;
            }
            // He climbed to a higher storey before leaving: he cannot go back down, so the door moves up with him.
            if (!RoadDoors && Map != null && activeStorey != exitStorey)
            {
                PlaceExitDoor();
                RefreshDoorSprites();
            }
            for (int i = 0; i < doorCount; i++) if (!doorArmed[i] && !AtDoor(i)) doorArmed[i] = true;
            if (RoadDoors) pathSelected = Mathf.Abs(wick.transform.position.x - DoorX(0)) <= Mathf.Abs(wick.transform.position.x - DoorX(1)) ? 0 : 1;

            if (stateTime < T.pathInputDelay) return; // so a tap meant for the pick does not choose a door
            if (RoadDoors)
            {
                if (input.DebugSpawn == 1 || input.DebugSpawn == 2) { SendToDoor(input.DebugSpawn - 1); return; }
                if (input.TapPosition is Vector2 tap)
                {
                    var gui = new Vector2(tap.x, Screen.height - tap.y);
                    for (int i = 0; i < 2; i++)
                        if (pathCardRects[i].Contains(gui) || pathDoorRects[i].Contains(gui)) { SendToDoor(i); return; }
                }
            }
            if (!DoorsFullyOpen || input.Climb < T.doorEnterClimb) return; // a shut door stays shut, and he must press UP at the door
            for (int i = 0; i < doorCount; i++)
                if (doorArmed[i] && AtDoor(i)) { GoThrough(i); return; }
        }

        /// <summary>Standing on the door's floor with his hitbox over the middle of the doorway.</summary>
        bool AtDoor(int i)
        {
            if (wick.Mode != Mode.Ground) return false;
            if (Mathf.Abs(wick.FeetY - doorFeet[i].y) > 0.3f) return false;
            var box = wick.Hitbox;
            float half = T.doorEnterHalfWidth;
            return box.xMax >= doorFeet[i].x - half && box.xMin <= doorFeet[i].x + half;
        }

        void SendToDoor(int side)
        {
            pathChosen = side;
            pathSelected = side;
            pathRunTime = 0f;
            Sfx.Play(Sound.Pop, 1.6f, 0.8f);
        }

        /// <summary>A tapped road door: he runs to it along the arena floor, then goes through.</summary>
        void TickPathRun(float dt)
        {
            pathRunTime += dt;
            float dx = DoorX(pathChosen) - wick.transform.position.x;
            if (Mathf.Abs(dx) > 0.2f && pathRunTime < 4f)
            {
                wick.Tick(Mathf.Sign(dx), 0f, dt, true);
                return;
            }
            if (!DoorsFullyOpen) return; // he waits at the door until it has creaked open
            GoThrough(pathChosen);
        }

        void GoThrough(int side)
        {
            pathChosen = side;
            pathSelected = side;
            pathArrived = true;
            pathFade = 0f;
            if (RoadDoors) run.ChooseRoad(side == HardSide ? road.debuff : null);
            Sfx.Play(Sound.Pop, 1.6f, 0.8f);
        }

        /// <summary>The screen has faded to the strip colour: the next stage starts, and with it the save.</summary>
        void LeaveThroughDoor()
        {
            wick.GapDamageEnabled = true;
            HidePathScene();
            NextStage();
        }

        // ------------------------------------------------------------------ drawing

        /// <summary>The exit door needs no words beyond a hint; the road doors get their title, the world strip and a card each.</summary>
        void DrawPath(int k, bool touchUi)
        {
            if (!RoadDoors)
            {
                if (!touchUi && !pathArrived)
                    Centered("Press UP at the door", WorldToGui(new Vector2(0f, camPanY)).y + 44 * k, 8 * k, UiText.Secondary);
                DrawPathFade();
                return;
            }
            Vector2 topLeft = WorldToGui(HudAnchorWorld(camPanY));
            int nextWorld = Mathf.Min(run.World + 1, T.worlds);
            DrawRoadStrip(k, nextWorld);

            float y0 = topLeft.y + 24 * k;
            Plate(y0 - 5 * k, 36 * k, k);
            Centered("CHOOSE YOUR ROAD", y0, 12 * k, UiText.Primary);
            Centered($"NEXT: WORLD {nextWorld} {Run.WorldNameOf(nextWorld).ToUpperInvariant()}", y0 + 17 * k, 8 * k, UiText.Accent);

            if (cardStyle == null) cardStyle = new GUIStyle(textStyle) { wordWrap = true, fontStyle = FontStyle.Normal, alignment = TextAnchor.UpperLeft };
            cardStyle.fontSize = 8 * k;

            // Doors and cards are placed from world positions, so they stay over the doors on any screen shape.
            float unit = WorldToGui(new Vector2(1f, 0f)).x - WorldToGui(Vector2.zero).x;
            float doorTop = WorldToGui(new Vector2(0f, 3f)).y;
            float cardW = 170 * k, inner = cardW - 12 * k, line = 10 * k;

            string hardLine1 = Run.HardRoadNumbers();
            string hardLine2 = $"{road.debuff.Name}: {road.debuff.Text}";
            string hardLine3 = Run.HardRoadReward();
            float hardH = 6 * k + line + 4 * k + Wrapped(hardLine1, inner) + 2 * k + Wrapped(hardLine2, inner) + 2 * k + Wrapped(hardLine3, inner) + 6 * k;
            const string calmLine = "Nothing changes.";
            float calmH = 6 * k + line + 4 * k + Wrapped(calmLine, inner) + 6 * k;
            float cardH = Mathf.Max(hardH, calmH);
            float cardTop = doorTop - 8 * k - cardH;

            for (int i = 0; i < 2; i++)
            {
                float cx = WorldToGui(new Vector2(DoorX(i), 0f)).x;
                pathCardRects[i] = new Rect(cx - cardW * 0.5f, cardTop, cardW, cardH);
                pathDoorRects[i] = new Rect(cx - unit, doorTop, unit * 2f, 3f * unit);
            }

            // The card of the door nearest Wick is lit, so walking towards a door reads as choosing it.
            float pulse = 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 5f);
            for (int i = 0; i < 2; i++)
            {
                bool hard = i == HardSide;
                bool lit = i == pathSelected;
                var r = pathCardRects[i];
                if (lit)
                {
                    r.y -= 2 * k;
                    for (int g = 4; g >= 1; g--)
                        FillRect(Grow(r, g * k), WithAlpha(Palette.Mint, 0.12f * pulse * (5 - g) / 4f));
                }
                int border = lit ? 2 * k : k;
                FillRect(r, lit ? Palette.Mint : hard ? Palette.Lemon : Palette.Lilac);
                var box = Grow(r, -border);
                FillRect(box, lit ? UiText.CardFillHover : UiText.CardFill);

                float x = box.x + 6 * k - (border - k), w = inner;
                float y = box.y + 6 * k - (border - k);
                Label(hard ? "HARD ROAD" : "CALM ROAD", x, y, hard ? UiText.Warning : lit ? UiText.Accent : UiText.Primary, TextAnchor.UpperLeft, w);
                y += line + 4 * k;
                Color body = lit ? UiText.Primary : UiText.Secondary;
                if (hard)
                {
                    y = CardText(hardLine1, x, y, w, body) + 2 * k;
                    y = CardText(hardLine2, x, y, w, body) + 2 * k;
                    CardText(hardLine3, x, y, w, UiText.Accent);
                }
                else CardText(calmLine, x, y, w, body);
                if (!touchUi) Label($"{i + 1}", box.xMax - 10 * k, box.yMax - 14 * k, UiText.Secondary, TextAnchor.UpperLeft, 10 * k);
            }

            if (!touchUi && !pathArrived)
            {
                float floorY = WorldToGui(Vector2.zero).y;
                Centered("Walk to a door and press UP, press 1-2, or click one", floorY + 44 * k, 8 * k, UiText.Secondary);
            }
            DrawPathFade();
        }

        void DrawPathFade()
        {
            if (pathArrived)
                FillRect(new Rect(0, 0, Screen.width, Screen.height), WithAlpha(CurrentTheme.strip, Mathf.Clamp01(pathFade / Mathf.Max(0.01f, T.pathFadeTime))));
        }

        float Wrapped(string text, float width) => cardStyle.CalcHeight(new GUIContent(text), width);

        /// <summary>One wrapped paragraph of card text; returns the y below it.</summary>
        float CardText(string text, float x, float y, float width, Color color)
        {
            float h = Wrapped(text, width);
            SetTextColor(cardStyle, color);
            GUI.Label(new Rect(x, y, width, h), text, cardStyle);
            return y + h;
        }

        /// <summary>
        /// Six small squares at the top centre, one per world: mint for a calm world played, lemon for a hard one, outline only
        /// for worlds not reached yet, and a cream bracket around the world in question. Shown on the Path and pause screens.
        /// </summary>
        void DrawRoadStrip(int k, int bracketWorld)
        {
            float y = WorldToGui(new Vector2(0f, HudAnchorWorld(camPanY).y)).y + 5 * k;
            int n = T.worlds;
            float size = 6 * k, gap = 5 * k;
            float x0 = (Screen.width - (n * size + (n - 1) * gap)) * 0.5f;
            for (int w = 1; w <= n; w++)
            {
                var r = new Rect(x0 + (w - 1) * (size + gap), y, size, size);
                bool reached = w <= run.World;
                if (reached) FillRect(r, Palette.Mist);
                else
                {
                    FillRect(new Rect(r.x, r.y, r.width, k), Palette.Mist);
                    FillRect(new Rect(r.x, r.yMax - k, r.width, k), Palette.Mist);
                    FillRect(new Rect(r.x, r.y, k, r.height), Palette.Mist);
                    FillRect(new Rect(r.xMax - k, r.y, k, r.height), Palette.Mist);
                }
                if (reached) FillRect(Grow(r, -k), run.hardWorld[w] ? Palette.Lemon : Palette.Mint);
                if (w == bracketWorld)
                {
                    var b = Grow(r, 2 * k);
                    FillRect(new Rect(b.x, b.y, k, b.height), Palette.Cream);
                    FillRect(new Rect(b.xMax - k, b.y, k, b.height), Palette.Cream);
                    FillRect(new Rect(b.x, b.y, 2 * k, k), Palette.Cream);
                    FillRect(new Rect(b.x, b.yMax - k, 2 * k, k), Palette.Cream);
                    FillRect(new Rect(b.xMax - 2 * k, b.y, 2 * k, k), Palette.Cream);
                    FillRect(new Rect(b.xMax - 2 * k, b.yMax - k, 2 * k, k), Palette.Cream);
                }
            }
        }
    }
}
