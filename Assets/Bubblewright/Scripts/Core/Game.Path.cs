using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// The left or right choice (step 14). After the upgrade pick that follows a boss (not the last one) two doors stand in
    /// front of the next world's scenery. One of them, chosen at random, is the hard road for the whole next world.
    /// Flow: Clear, upgrade pick, Path, then the next stage. Side 0 is the left door, side 1 the right one.
    /// </summary>
    public partial class Game
    {
        Run.RoadChoice road;
        int pathSelected;          // highlighted door; starts on the calm one
        int pathChosen = -1;       // -1 until a door is picked
        bool pathArrived;          // Wick reached the door and the fade has started
        float pathRunTime, pathFade;
        readonly Rect[] pathCardRects = new Rect[2]; // GUI space, written by OnGUI, read by TickPath
        readonly Rect[] pathDoorRects = new Rect[2];

        Transform pathRoot;
        readonly SpriteRenderer[] doorRenderers = new SpriteRenderer[2];

        int HardSide => road.hardIsRight ? 1 : 0;
        float DoorX(int side) => (side == 0 ? -1f : 1f) * T.pathDoorX;

        /// <summary>The pick is over: the choice follows a boss, otherwise the next stage starts.</summary>
        void FinishPick()
        {
            if (Run.PathFollows(run.Stage)) OpenPath();
            else NextStage();
        }

        void OpenPath()
        {
            road = run.RollRoad();
            pathChosen = -1;
            pathArrived = false;
            pathRunTime = pathFade = 0f;
            pathSelected = 1 - HardSide; // the calm door is lit first
            lastMoveDir = input.Move > 0f ? 1 : input.Move < 0f ? -1 : 0;
            for (int i = 0; i < 2; i++) pathCardRects[i] = pathDoorRects[i] = Rect.zero;

            // A bare floor in the next world's look: no platforms, spikes, pickups or cables left over from the boss.
            Map = StageMap.Flat();
            debugSurface = -1;
            debugTheme = -1;
            ClearSpikes();
            ClearFlames();
            ClearPickups();
            HideHarpoons();
            fx.Clear();
            var next = Themes.ForWorld(run.World + 1);
            ApplyTheme(next);
            wick.ResetForStage(0f, 0f, run.Hearts); // stands in the middle, facing the camera
            ShowPathScene(next);
            SetState(State.Path);
        }

        void ShowPathScene(Theme next)
        {
            if (pathRoot == null)
            {
                pathRoot = new GameObject("Road Choice").transform;
                pathRoot.SetParent(world, false);
                for (int i = 0; i < 2; i++)
                {
                    var go = new GameObject(i == 0 ? "Left Door" : "Right Door");
                    go.transform.SetParent(pathRoot, false);
                    doorRenderers[i] = go.AddComponent<SpriteRenderer>();
                    Sorting.Apply(doorRenderers[i], Layer.Stage, 10);
                    ApplySpriteMaterial(doorRenderers[i]);
                }
                // The usual dim, laid over the background only so the doors and Wick stay bright.
                var dimGo = new GameObject("Dim");
                dimGo.transform.SetParent(pathRoot, false);
                var dim = dimGo.AddComponent<SpriteRenderer>();
                dim.sprite = PlaceholderArt.I.Pixel;
                dim.color = WithAlpha(Palette.Ink, UiText.DimAlpha);
                Sorting.Apply(dim, Layer.Sky, 10);
                ApplySpriteMaterial(dim);
                float ppu = T.pixelsPerUnit;
                dimGo.transform.localPosition = new Vector3((ThemeBackgrounds.Left + ThemeBackgrounds.Right) * 0.5f, (ThemeBackgrounds.Bottom + ThemeBackgrounds.Top) * 0.5f, 0f);
                dimGo.transform.localScale = new Vector3((ThemeBackgrounds.Right - ThemeBackgrounds.Left) * ppu, (ThemeBackgrounds.Top - ThemeBackgrounds.Bottom) * ppu, 1f);
            }
            for (int i = 0; i < 2; i++)
            {
                doorRenderers[i].sprite = PlaceholderArt.I.DoorFor(next, i == HardSide);
                doorRenderers[i].transform.position = new Vector3(DoorX(i), 0f, 0f);
            }
            pathRoot.gameObject.SetActive(true);
        }

        void HidePathScene()
        {
            if (pathRoot != null) pathRoot.gameObject.SetActive(false);
        }

        /// <summary>Left/right moves the highlight, Enter, Space or pad A takes it, 1 and 2 pick left and right, a tap or click on a door or its card picks it.</summary>
        void TickPath(float dt)
        {
            if (pathChosen >= 0)
            {
                TickPathRun(dt);
                return;
            }
            wick.Tick(0f, 0f, dt, false);
            int dir = input.Move > 0f ? 1 : input.Move < 0f ? -1 : 0;
            bool moved = dir != 0 && dir != lastMoveDir;
            lastMoveDir = dir;
            if (stateTime < T.pathInputDelay) return; // so a tap meant for the pick does not choose a door

            if (moved) pathSelected = dir < 0 ? 0 : 1;
            if (input.DebugSpawn == 1 || input.DebugSpawn == 2) { ChoosePathDoor(input.DebugSpawn - 1); return; }
            if (input.TapPosition is Vector2 tap)
            {
                var gui = new Vector2(tap.x, Screen.height - tap.y);
                for (int i = 0; i < 2; i++)
                    if (pathCardRects[i].Contains(gui) || pathDoorRects[i].Contains(gui)) { ChoosePathDoor(i); return; }
            }
            if (input.KeyConfirmPressed) ChoosePathDoor(pathSelected);
        }

        void ChoosePathDoor(int side)
        {
            pathChosen = side;
            pathSelected = side;
            pathArrived = false;
            pathRunTime = pathFade = 0f;
            run.ChooseRoad(side == HardSide ? road.debuff : null);
            Sfx.Play(Sound.Pop, 1.6f, 0.8f);
            wick.Turn(false); // back to the camera, he runs to the door
        }

        /// <summary>Wick runs to his door, then the screen fades to the strip colour and the next stage starts.</summary>
        void TickPathRun(float dt)
        {
            pathRunTime += dt;
            float dx = DoorX(pathChosen) - wick.transform.position.x;
            if (!pathArrived && Mathf.Abs(dx) > 0.2f && pathRunTime < 4f)
            {
                wick.Tick(Mathf.Sign(dx), 0f, dt, true);
                return;
            }
            pathArrived = true;
            wick.Tick(0f, 0f, dt, false);
            pathFade += dt;
            if (pathFade >= T.pathFadeTime)
            {
                HidePathScene();
                NextStage();
            }
        }

        // ------------------------------------------------------------------ drawing

        void DrawPath(int k, bool touchUi)
        {
            Vector2 topLeft = WorldToGui(new Vector2(-T.HalfWidth, T.arenaHeight));
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

            bool canHover = !Application.isMobilePlatform;
            Vector2 mouse = Event.current.mousePosition;
            bool mouseMoved = (mouse - lastMouse).sqrMagnitude > 0.5f;
            lastMouse = mouse;
            for (int i = 0; i < 2; i++)
            {
                float cx = WorldToGui(new Vector2(DoorX(i), 0f)).x;
                pathCardRects[i] = new Rect(cx - cardW * 0.5f, cardTop, cardW, cardH);
                pathDoorRects[i] = new Rect(cx - unit, doorTop, unit * 2f, 3f * unit);
                if (canHover && pathChosen < 0 && mouseMoved && (pathCardRects[i].Contains(mouse) || pathDoorRects[i].Contains(mouse))) pathSelected = i;
            }

            float pulse = 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 5f);
            for (int i = 0; i < 2; i++)
            {
                bool hard = i == HardSide;
                bool lit = canHover && i == pathSelected;
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

            if (!touchUi)
            {
                float floorY = WorldToGui(Vector2.zero).y;
                Centered("Left/right and Enter, 1-2, or click a door", floorY + 44 * k, 8 * k, UiText.Secondary);
            }

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
            float y = WorldToGui(new Vector2(0f, T.arenaHeight)).y + 5 * k;
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
