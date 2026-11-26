using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// The chosen items on the pause screen: every item with its full icon and name, the weapons with their ammo,
    /// and the controls. Positions come from ItemsLayout, which the fit test checks.
    /// </summary>
    public partial class Game
    {
        /// <summary>Under the pause buttons: the ITEMS title, a grid of every item, and the controls at the foot.</summary>
        void DrawPauseItems(int k, bool touchUi)
        {
            var items = run.BuildItems();
            int weapons = (run.Weapon != null ? 1 : 0) + (run.Head != null ? 1 : 0);
            int count = items.Count + weapons;

            Centered("ITEMS", ItemsLayout.PauseItemsTitleY(Screen.height, k), 8 * k, UiText.Accent);
            if (count == 0)
                Centered("None yet. Pick upgrades after each stage, and break crates for weapons.",
                    ItemsLayout.PauseGridTop(Screen.height, k), 8 * k, UiText.Secondary);
            else
            {
                var rects = ItemsLayout.PauseGrid(Screen.width, ItemsLayout.PauseGridTop(Screen.height, k), count, k);
                int i = 0;
                foreach (var (upgrade, n) in items)
                    DrawPauseItem(rects[i++], PlaceholderArt.I.UpgradeIcon(upgrade.Name), upgrade.Name, n > 1 ? $"x{n}" : null, k);
                if (run.Weapon != null) DrawPauseItem(rects[i++], run.Weapon.IconTexture, run.Weapon.Name, $"ammo {run.WeaponAmmo}", k);
                if (run.Head != null) DrawPauseItem(rects[i++], run.Head.IconTexture, run.Head.Name, $"ammo {run.HeadAmmo}", k);
            }

            if (touchUi) return;
            float y = ItemsLayout.PauseControlsY(Screen.height, k);
            foreach (var line in ItemsLayout.ControlLines)
            {
                Centered(line, y, 8 * k, UiText.Secondary);
                y += 11 * k;
            }
        }

        /// <summary>Icon box on the left; the name beside it, with the stack count or ammo on a second line.</summary>
        void DrawPauseItem(Rect cell, Texture2D icon, string label, string detail, int k)
        {
            float box = PlaceholderArt.UpgradeIconSize * k + 2 * k;
            var iconBox = new Rect(cell.x, cell.y + (cell.height - box) * 0.5f, box, box);
            FillRect(iconBox, Palette.Ink);
            if (icon != null)
            {
                float w = icon.width * k, h = icon.height * k;
                GUI.DrawTexture(new Rect(Mathf.Round(iconBox.center.x - w * 0.5f), Mathf.Round(iconBox.center.y - h * 0.5f), w, h), icon);
            }
            float x = iconBox.xMax + 4 * k, w2 = cell.xMax - x;
            if (detail == null)
                Label(label, x, cell.y + (cell.height - 10 * k) * 0.5f, UiText.Primary, TextAnchor.UpperLeft, w2);
            else
            {
                Label(label, x, cell.y, UiText.Primary, TextAnchor.UpperLeft, w2);
                Label(detail, x, cell.y + 10 * k, UiText.Secondary, TextAnchor.UpperLeft, w2);
            }
        }
    }
}
