using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// Draws the thumb controls inside the ground strip, as world sprites so they stay pixel perfect.
    /// Left: move pad hint, plus a knob where the thumb landed and a dot that follows it.
    /// Right: the big mint fire button, with the round jump button 3.5 units to its left. Shown on phones, or on desktop once the mouse has been used as a finger.
    /// </summary>
    public class TouchControlsView : MonoBehaviour
    {
        SpriteRenderer hint, knob, dot, climbUp, climbDown, fire, jump;
        Camera cam;

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

        public void Refresh(GameInput input, Tuning t, bool nearLadder = false)
        {
            bool show = Application.isMobilePlatform || input.TouchUsed;
            float stripMid = -t.groundStrip * 0.5f - 0.2f;
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

        /// <summary>Radius of the drawn jump (and fire) disc, in art pixels.</summary>
        const float JumpButtonRadiusPixels = 21f;

        static Vector3 Snap(Vector2 p, Tuning t)
        {
            float px = 1f / t.pixelsPerUnit;
            return new Vector3(Mathf.Round(p.x / px) * px, Mathf.Round(p.y / px) * px, 0f);
        }
    }
}
