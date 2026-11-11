using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// Title screen: Rooftops background with Wick standing in the middle facing the camera and waving,
    /// CONTINUE when a saved run exists, and NEW RUN (asking once before replacing a saved run).
    /// </summary>
    public partial class Game
    {
        static readonly string[] TitleWithSaveItems = { "CONTINUE", "NEW RUN" };
        static readonly string[] TitleNoSaveItems = { "NEW RUN" };
        static readonly string[] TitleConfirmItems = { "START NEW", "BACK" };

        Run savedRun;
        bool confirmingNewRun;
        int titleSelected;
        readonly Rect[] titleRects = new Rect[2];

        public static string SavedPlace(Run saved) =>
            $"WORLD {saved.World}-{saved.StageInWorld}, {saved.Hearts} {(saved.Hearts == 1 ? "HEART" : "HEARTS")}";

        string[] CurrentTitleItems =>
            confirmingNewRun ? TitleConfirmItems : savedRun != null ? TitleWithSaveItems : TitleNoSaveItems;

        int TitleMoveDir => input.Move > 0f || input.MoveY < 0f ? 1 : input.Move < 0f || input.MoveY > 0f ? -1 : 0;

        void OpenTitle()
        {
            paused = debugPanel = false;
            Map = StageMap.Flat();
            ClearSpikes();
            foreach (var b in bubbles) if (b) Destroy(b.gameObject);
            bubbles.Clear();
            hitBubbles.Clear();
            fx.Clear();
            HideHarpoons();
            wick.ResetForStage(0f, 0f, T.hearts);
            wick.Waving = true;
            freeze = slowMo = shake = 0f;
            shotTimer = 0f;
            turnedForCelebration = celebrating = false;
            stormClock = stormBanner = 0f;
            storm = false;
            touchView.gameObject.SetActive(false);

            savedRun = SaveGame.Load();
            confirmingNewRun = false;
            titleSelected = 0;
            lastMoveDir = TitleMoveDir;
            ClearTitleRects();
            SetState(State.Title);
        }

        void ClearTitleRects()
        {
            for (int i = 0; i < titleRects.Length; i++) titleRects[i] = Rect.zero;
        }

        void TickTitle(float dt)
        {
            wick.Tick(0f, 0f, dt, false);

            var items = CurrentTitleItems;
            int dir = TitleMoveDir;
            if (dir != 0 && dir != lastMoveDir && items.Length > 1)
                titleSelected = (titleSelected + dir + items.Length) % items.Length;
            lastMoveDir = dir;

            if (stateTime < 0.15f) return;

            if (confirmingNewRun && input.PausePressed)
            {
                CloseNewRunConfirm();
                return;
            }

            if (input.TapPosition is Vector2 tap)
            {
                var gui = new Vector2(tap.x, Screen.height - tap.y);
                for (int i = 0; i < items.Length; i++)
                    if (titleRects[i].Contains(gui)) { ActivateTitle(i); return; }
                return;
            }

            if (input.KeyConfirmPressed) ActivateTitle(titleSelected);
        }

        void ActivateTitle(int item)
        {
            if (confirmingNewRun)
            {
                if (item == 0)
                {
                    SaveGame.Delete();
                    savedRun = null;
                    confirmingNewRun = false;
                    StartRun();
                }
                else CloseNewRunConfirm();
                return;
            }

            if (savedRun != null)
            {
                if (item == 0) ContinueRun();
                else
                {
                    confirmingNewRun = true;
                    titleSelected = 1;
                    ClearTitleRects();
                    Sfx.Play(Sound.Turn);
                }
            }
            else StartRun();
        }

        void CloseNewRunConfirm()
        {
            confirmingNewRun = false;
            titleSelected = 0;
            ClearTitleRects();
            Sfx.Play(Sound.Turn);
        }

        void ContinueRun()
        {
            var loaded = SaveGame.Load() ?? savedRun;
            savedRun = null;
            if (loaded == null)
            {
                StartRun();
                return;
            }
            run = loaded;
            newBest = false;
            StartStage();
        }

        void DrawTitle(int k)
        {
            float top = Mathf.Round(Screen.height * 0.14f);
            Plate(top - 6 * k, (savedRun != null ? 90 : 72) * k, k);

            Centered("BUBBLEWRIGHT", top, 18 * k, Palette.Cream);
            Centered($"BEST {bestScore}", top + 24 * k, 8 * k, UiText.Accent);

            var items = CurrentTitleItems;
            float w = 96 * k, h = 22 * k, gap = 12 * k;
            float x0 = (Screen.width - (items.Length * w + (items.Length - 1) * gap)) * 0.5f;
            float buttonY = top + (confirmingNewRun ? 56 : 42) * k;

            if (confirmingNewRun)
                Centered("This replaces your saved run.", top + 40 * k, 8 * k, UiText.Primary);

            bool canHover = !Application.isMobilePlatform;
            Vector2 mouse = Event.current.mousePosition;
            bool mouseMoved = (mouse - lastMouse).sqrMagnitude > 0.5f;
            lastMouse = mouse;
            for (int i = 0; i < items.Length; i++)
            {
                titleRects[i] = new Rect(x0 + i * (w + gap), buttonY, w, h);
                if (canHover && mouseMoved && titleRects[i].Contains(mouse)) titleSelected = i;
            }
            for (int i = 0; i < items.Length; i++)
                DrawMenuButton(titleRects[i], items[i], canHover && i == titleSelected, k);

            if (!confirmingNewRun && savedRun != null)
                Label(SavedPlace(savedRun), titleRects[0].center.x - 90 * k, buttonY + h + 4 * k, UiText.Secondary, TextAnchor.UpperCenter, 180 * k);
        }
    }
}
