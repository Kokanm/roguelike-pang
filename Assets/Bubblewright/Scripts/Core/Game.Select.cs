using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// Character select: four cards in a row before a new run. Left and right move between open characters,
    /// Enter, Space or pad A confirms, a tap or click picks directly, Esc or pad B goes back to the title.
    /// Locked characters show a flat silhouette and how to unlock them, and cannot be chosen.
    /// </summary>
    public partial class Game
    {
        int selectedCharacter;
        readonly Rect[] characterRects = new Rect[4];

        /// <summary>Card text colours and layout reuse the upgrade card look; see DrawPick.</summary>
        void OpenSelect()
        {
            var last = Unlocks.LastCharacter();
            selectedCharacter = Unlocks.IsOpen(last) ? System.Array.IndexOf(Character.All, last) : 0;
            lastMoveDir = input.Move > 0f ? 1 : input.Move < 0f ? -1 : 0;
            for (int i = 0; i < characterRects.Length; i++) characterRects[i] = Rect.zero;
            SetState(State.Select);
        }

        void TickSelect(float dt)
        {
            wick.Tick(0f, 0f, dt, false);

            int dir = input.Move > 0f ? 1 : input.Move < 0f ? -1 : 0;
            bool moved = dir != 0 && dir != lastMoveDir;
            lastMoveDir = dir;
            if (stateTime < 0.2f) return; // so the tap that opened this screen does not also pick a card

            if (input.BackPressed)
            {
                Sfx.Play(Sound.Turn);
                OpenTitle();
                return;
            }
            if (moved) selectedCharacter = NextOpenCharacter(selectedCharacter, dir);

            if (input.TapPosition is Vector2 tap)
            {
                var gui = new Vector2(tap.x, Screen.height - tap.y);
                for (int i = 0; i < Character.All.Length; i++)
                    if (characterRects[i].Contains(gui) && Unlocks.IsOpen(Character.All[i]))
                    {
                        StartRun(Character.All[i]);
                        return;
                    }
                return;
            }
            if (input.KeyConfirmPressed && Unlocks.IsOpen(Character.All[selectedCharacter]))
                StartRun(Character.All[selectedCharacter]);
        }

        /// <summary>The next open character in a direction, wrapping around; stays put when there is no other open one.</summary>
        static int NextOpenCharacter(int from, int dir)
        {
            int n = Character.All.Length;
            for (int step = 1; step <= n; step++)
            {
                int i = ((from + dir * step) % n + n) % n;
                if (Unlocks.IsOpen(Character.All[i])) return i;
            }
            return from;
        }

        /// <summary>Called when the last stage is cleared: opens whatever this character's finished run unlocks.</summary>
        void UnlockCharactersForFinishedRun()
        {
            newUnlocks = null;
            foreach (var c in Character.All)
            {
                if (c.unlockedByFinishingWith != run.Character.id || !Unlocks.Unlock(c.id)) continue;
                newUnlocks = newUnlocks == null ? c.name : newUnlocks + ", " + c.name;
            }
        }

        /// <summary>The front view at 2x with its flame and launcher. The figure box is 46 x 76 pixels at 2x (the flame rises above the head).</summary>
        void DrawCharacterFigure(Rect box, Character c, bool locked, int k)
        {
            float s = 2 * k;
            var art = PlaceholderArt.I;
            var sprites = art.SpritesFor(c);
            float left = box.center.x - 23 * 0.5f * s, feet = box.yMax;
            if (locked)
            {
                GUI.DrawTexture(new Rect(left, feet - 30 * s, 23 * s, 30 * s), sprites.FrontLocked.texture);
                return;
            }
            GUI.DrawTexture(new Rect(left, feet - 30 * s, 23 * s, 30 * s), sprites.Front.texture);
            var launcher = sprites.Launcher.texture;
            var lo = PlaceholderArt.LauncherOffsetPixels(true);
            GUI.DrawTexture(new Rect(left + (lo.x + 11.5f) * s, feet - (lo.y + launcher.height) * s, launcher.width * s, launcher.height * s), launcher);
            var flame = art.Flame.texture;
            GUI.DrawTexture(new Rect(left + (11.5f - 4.5f) * s, feet - (27f - 1f + flame.height) * s, flame.width * s, flame.height * s), flame);
        }

        void DrawPips(float x, float y, int filled, int total, bool lit, int k)
        {
            for (int i = 0; i < total; i++)
            {
                var r = new Rect(x + i * 5 * k, y, 4 * k, 4 * k);
                if (i < filled) FillRect(r, Palette.Mint);
                else
                {
                    FillRect(r, Palette.Mist);
                    FillRect(Grow(r, -k), lit ? UiText.CardFillHover : UiText.CardFill);
                }
            }
        }

        void DrawSelect(int k)
        {
            FillRect(new Rect(0, 0, Screen.width, Screen.height), WithAlpha(Palette.Ink, UiText.DimAlpha));
            float top = Screen.height * 0.5f - 107 * k;
            Centered("CHOOSE YOUR HERO", top - 18 * k, 12 * k, UiText.Primary);

            if (cardStyle == null) cardStyle = new GUIStyle(textStyle) { wordWrap = true, fontStyle = FontStyle.Normal, alignment = TextAnchor.UpperLeft };
            cardStyle.fontSize = 8 * k;

            // Four cards fit the 480 px reference width with the same rules as the upgrade cards.
            int count = Character.All.Length;
            float gap = 6 * k;
            float w = Mathf.Min(120 * k, Mathf.Floor(((T.referenceResolution.x - 12) * k - (count - 1) * gap) / count));
            float h = 172 * k;
            float x0 = (Screen.width - (count * w + (count - 1) * gap)) * 0.5f;
            bool canHover = !Application.isMobilePlatform;

            Vector2 mouse = Event.current.mousePosition;
            bool mouseMoved = (mouse - lastMouse).sqrMagnitude > 0.5f;
            lastMouse = mouse;
            for (int i = 0; i < count; i++)
            {
                characterRects[i] = new Rect(x0 + i * (w + gap), top, w, h);
                if (canHover && mouseMoved && characterRects[i].Contains(mouse) && Unlocks.IsOpen(Character.All[i])) selectedCharacter = i;
            }

            float pulse = 0.85f + 0.15f * Mathf.Sin(Time.unscaledTime * 5f);
            for (int i = 0; i < count; i++)
            {
                var c = Character.All[i];
                bool open = Unlocks.IsOpen(c);
                bool lit = open && i == selectedCharacter && canHover;
                var r = characterRects[i];
                if (lit)
                {
                    r.y -= 2 * k;
                    for (int g = 4; g >= 1; g--)
                        FillRect(Grow(r, g * k), WithAlpha(Palette.Mint, 0.12f * pulse * (5 - g) / 4f));
                }
                int border = lit ? 2 * k : k;
                FillRect(r, lit ? Palette.Mint : Palette.Lilac);
                var inner = Grow(r, -border);
                FillRect(inner, lit ? UiText.CardFillHover : UiText.CardFill);

                float figureBottom = inner.y + 4 * k + 76 * k;
                DrawCharacterFigure(new Rect(inner.x, inner.y + 4 * k, inner.width, 76 * k), c, !open, k);

                float textX = inner.x + 6 * k, textW = inner.width - 12 * k;
                float y = figureBottom + 3 * k;
                Label(open ? c.name.ToUpperInvariant() : "???", inner.x, y, lit ? UiText.Accent : UiText.Primary, TextAnchor.UpperCenter, inner.width);
                y += 12 * k;
                SetTextColor(cardStyle, lit ? UiText.Primary : UiText.Secondary);
                if (!open)
                {
                    GUI.Label(new Rect(textX, y, textW, inner.yMax - y - 2 * k), c.UnlockHint, cardStyle);
                    continue;
                }
                GUI.Label(new Rect(textX, y, textW, 24 * k), c.blurb, cardStyle);
                y += 26 * k;

                float pipX = inner.xMax - 6 * k - 24 * k;
                void Row(string label, int filled, int total)
                {
                    Label(label, textX, y, UiText.Secondary, TextAnchor.UpperLeft, 66 * k);
                    DrawPips(pipX, y + 2 * k, filled, total, lit, k);
                    y += 10 * k;
                }
                Row("HEARTS", c.StartHearts, Mathf.Max(c.StartHearts, 4));
                Row("SPEED", c.SpeedPips, 5);
                Row("CABLES", c.StartShots, Mathf.Max(c.StartShots, 2));
                Row("SHOT SPEED", c.ShotSpeedPips, 5);
            }

            bool touchUi = Application.isMobilePlatform || input.TouchUsed;
            float below = top + h + 8 * k;
            Centered(touchUi ? "Tap a hero" : "Left/right and Enter, or click a hero    Esc: back", below, 8 * k, UiText.Secondary);
        }
    }
}
