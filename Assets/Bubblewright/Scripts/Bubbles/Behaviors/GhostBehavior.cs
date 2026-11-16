using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// The Ghost ignores platform tiles (it still bounces on the floor and ceiling of its home storey, and off the walls) and alternates
    /// between solid and faded. While faded the harpoon passes through it and it cannot hurt Wick. It flickers at the end
    /// of the fade, and it will not turn solid while it still overlaps Wick.
    /// </summary>
    public class GhostBehavior : BubbleBehavior
    {
        const float FlickerStep = 0.05f;

        float clock;
        bool faded;
        int shownFrame = -1; // -1 plain faded sprite, 0 / 1 flicker frames

        public bool Faded => faded;

        public override void OnSpawn(Bubble b, bool child)
        {
            b.IgnoresTiles = true;
            clock = 0f;
            if (!child)
            {
                // Ghosts placed in a stage start out of step with each other, by a time taken from where they sit.
                float x = Mathf.Abs(b.Position.x) * 0.37f;
                clock = (x - Mathf.Floor(x)) * Tuning.I.ghostSolidTime;
            }
        }

        public override bool Tick(Bubble b, float dt)
        {
            var t = Tuning.I;
            clock += dt;
            if (!faded)
            {
                if (clock >= t.ghostSolidTime)
                {
                    faded = true;
                    clock = 0f;
                    shownFrame = -1;
                    ShowFaded(b, false);
                }
            }
            else if (clock >= t.ghostFadedTime && !b.Overlaps(Game.WickBox))
            {
                faded = false;
                clock = 0f;
                b.RestoreSprite();
            }
            else
            {
                bool flickering = clock >= t.ghostFadedTime - t.ghostFlickerTime;
                int frame = flickering ? ((int)((clock - (t.ghostFadedTime - t.ghostFlickerTime)) / FlickerStep) % 2 == 0 ? 1 : 0) : -1;
                if (frame != shownFrame)
                {
                    shownFrame = frame;
                    ShowFaded(b, frame == 1);
                }
            }
            return false;
        }

        /// <summary>A round Ghost swaps to its faded sprite; a Ghost Pogo keeps its shape and is drawn faded.</summary>
        static void ShowFaded(Bubble b, bool strong)
        {
            if (b.Pogo) b.GhostLook = strong ? PogoLook.GhostFlicker : PogoLook.GhostFaded;
            else b.SetSprite(PlaceholderArt.I.GhostFaded(b.Tier, strong));
        }

        public override bool CanBeHit(Bubble b) => !faded;
        public override bool CanHurt(Bubble b) => !faded;
    }
}
