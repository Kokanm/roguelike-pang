using UnityEngine;

namespace Bubblewright
{
    public partial class Game
    {
        static Game active;

        /// <summary>A sparkle from code that has no Game reference (bubble behaviours). Does nothing when no game is running.</summary>
        public static void Sparkle(Vector2 at, Color32 color)
        {
            if (active != null && active.fx != null) active.fx.Sparkle(at, color);
        }
    }
}
