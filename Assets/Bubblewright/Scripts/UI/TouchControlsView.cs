using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// Draws the thumb controls inside the ground strip, as world sprites so they stay pixel perfect.
    /// Left: move pad hint, plus a knob where the thumb landed and a dot that follows it.
    /// Right: the big mint fire button, with the round jump button 3.5 units to its left. Shown on phones, or on desktop once the mouse has been used as a finger.
    /// While Wick holds a weapon, a third disc (the weapon button) sits 3.5 units left of the jump button, with the
    /// weapon's icon and the ammo left under it.
    /// </summary>
    public class TouchControlsView : MonoBehaviour
    {
        SpriteRenderer hint, knob, dot, climbUp, climbDown, fire, jump, weapon, weaponAmmo;
        Camera cam;
        WeaponSpec heldWeapon;
        int heldAmmo;
        bool heldDimmed;
        float heldGauge = -1f;
        static readonly Color DimTint = new Color(0.5f, 0.5f, 0.55f, 1f);

        /// <summary>The weapon in the weapon slot and its ammo, or null to hide the weapon button. Set before Refresh.</summary>
        public void ShowWeapon(WeaponSpec spec, int ammo, bool dimmed = false, float gauge = -1f)
        {
            heldWeapon = spec;
            heldAmmo = ammo;
            heldDimmed = dimmed;
            heldGauge = gauge;
        }

        public static TouchControlsView Create(Transform parent, Camera cam)
        {
            var go = new GameObject("Touch Controls");
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<TouchControlsView>();
            v.cam = cam;
            var art = PlaceholderArt.I;
            v.hint = v.Part("Move Hint", art.MoveHint, 0);
            v.knob = v.Part("Move Knob", art.MoveKnob, 1);
            v.dot = v.Part("Move Dot", art.MoveDot, 2);
            v.climbUp = v.Part("Climb Up", art.ClimbUpHint, 1);
            v.climbDown = v.Part("Climb Down", art.ClimbDownHint, 1);
            v.fire = v.Part("Fire Button", art.FireButton, 0);
            v.jump = v.Part("Jump Button", art.JumpButton, 0);
            v.weapon = v.Part("Weapon Button", null, 0);
            v.weaponAmmo = v.Part("Weapon Ammo", null, 1);
            return v;
        }

        SpriteRenderer Part(string name, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            Sorting.Apply(sr, Layer.UI, order);
            Game.ApplySpriteMaterial(sr);
            return sr;
        }

        public void Refresh(GameInput input, Tuning t, bool nearLadder = false, float camPanY = 0f)
        {
            bool show = Application.isMobilePlatform || input.TouchUsed;
            float stripMid = camPanY - t.groundStrip * 0.5f - 0.2f;
            float px = 1f / t.pixelsPerUnit;

            // Anchor to the visible screen edges (inside the safe area), not the arena, so thumbs reach them.
            var safe = Screen.safeArea;
            float leftX = cam.ScreenToWorldPoint(new Vector3(safe.xMin, 0f, 0f)).x;
            float rightX = cam.ScreenToWorldPoint(new Vector3(safe.xMax, 0f, 0f)).x;
            Vector2 hintPos = new Vector2(leftX + 4f, stripMid);

            hint.enabled = show && !input.PadActive;
            hint.transform.position = Snap(hintPos, t);

            fire.enabled = show;
            fire.sprite = input.TouchFireHeld ? PlaceholderArt.I.FireButtonPressed : PlaceholderArt.I.FireButton;
            Vector2 firePos = new Vector2(rightX - 3f, stripMid);
            fire.transform.position = Snap(firePos, t);

            // Jump: same size as fire, centre 3.5 units further left. The input reads its circle in screen pixels.
            jump.enabled = show;
            jump.sprite = input.TouchJumpHeld ? PlaceholderArt.I.JumpButtonPressed : PlaceholderArt.I.JumpButton;
            Vector3 jumpPos = Snap(firePos - new Vector2(3.5f, 0f), t);
            jump.transform.position = jumpPos;
            if (show)
            {
                Vector2 centre = cam.WorldToScreenPoint(jumpPos);
                Vector2 edge = cam.WorldToScreenPoint(jumpPos + new Vector3(JumpButtonRadiusPixels * px, 0f, 0f));
                input.SetJumpButton(centre, Mathf.Abs(edge.x - centre.x));
            }
            else input.SetJumpButton(Vector2.zero, 0f);

            // Weapon: same size again, 3.5 units left of jump, only while a weapon is held. Excluded from the fire area.
            bool showWeapon = show && heldWeapon != null;
            weapon.enabled = weaponAmmo.enabled = showWeapon;
            if (showWeapon)
            {
                bool down = input.TouchWeaponHeld;
                Vector3 weaponPos = Snap(firePos - new Vector2(7f, 0f), t);
                weapon.sprite = WeaponArt.Button(heldWeapon, down);
                weapon.color = weaponAmmo.color = heldDimmed ? DimTint : Color.white;
                weapon.transform.position = weaponPos;
                weaponAmmo.sprite = heldGauge >= 0f ? GaugeBar(heldGauge) : WeaponArt.Number(heldAmmo);
                weaponAmmo.transform.position = weaponPos + new Vector3(0f, WeaponArt.ButtonNumberOffsetPixels(down) * px, 0f);
                Vector2 centre = cam.WorldToScreenPoint(weaponPos);
                Vector2 edge = cam.WorldToScreenPoint(weaponPos + new Vector3(JumpButtonRadiusPixels * px, 0f, 0f));
                input.SetWeaponButton(centre, Mathf.Abs(edge.x - centre.x));
            }
            else input.SetWeaponButton(Vector2.zero, 0f);

            knob.enabled = dot.enabled = show && input.PadActive;
            Vector2 arrowAnchor = hintPos;
            if (input.PadActive)
            {
                Vector2 o = cam.ScreenToWorldPoint(input.PadOrigin);
                Vector2 c = cam.ScreenToWorldPoint(input.PadCurrent);
                knob.transform.position = Snap(o, t);
                dot.transform.position = Snap(input.Climb != 0f ? new Vector2(o.x, c.y) : new Vector2(c.x, o.y), t);
                arrowAnchor = o;
            }

            climbUp.enabled = climbDown.enabled = show && nearLadder;
            if (nearLadder)
            {
                float arrowOffset = 12f * px;
                climbUp.transform.position = Snap(arrowAnchor + new Vector2(0f, arrowOffset), t);
                climbDown.transform.position = Snap(arrowAnchor - new Vector2(0f, arrowOffset), t);
            }
        }

        const int GaugeWidth = 15;
        static readonly Sprite[] gaugeBars = new Sprite[GaugeWidth + 1];

        /// <summary>An ink frame 17 x 5 px filled with ink for the share left, in place of the ammo number (fuel).</summary>
        static Sprite GaugeBar(float fraction)
        {
            int filled = Mathf.Clamp(Mathf.CeilToInt(fraction * GaugeWidth - 0.001f), 0, GaugeWidth);
            if (gaugeBars[filled] != null) return gaugeBars[filled];
            var c = new PixelCanvas(GaugeWidth + 2, 5);
            c.Rect(0, 0, GaugeWidth + 2, 1, Palette.Ink);
            c.Rect(0, 4, GaugeWidth + 2, 1, Palette.Ink);
            c.Rect(0, 0, 1, 5, Palette.Ink);
            c.Rect(GaugeWidth + 1, 0, 1, 5, Palette.Ink);
            c.Rect(1, 1, filled, 3, Palette.Ink);
            return gaugeBars[filled] = c.ToSprite("WeaponGauge_" + filled, new Vector2(Mathf.Floor(c.Width * 0.5f), 2f), Tuning.I.pixelsPerUnit);
        }

        /// <summary>Radius of the drawn jump (and fire) disc, in art pixels.</summary>
        const float JumpButtonRadiusPixels = 21f;

        static Vector3 Snap(Vector2 p, Tuning t)
        {
            float px = 1f / t.pixelsPerUnit;
            return new Vector3(Mathf.Round(p.x / px) * px, Mathf.Round(p.y / px) * px, 0f);
        }
    }
}
