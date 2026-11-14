using UnityEngine;

namespace Bubblewright
{
    public partial class Game
    {
        static Game active;

        /// <summary>True while the running game is in its storm. False when no game is running (tests).</summary>
        public static bool StormOn => active != null && active.storm;

        /// <summary>A sparkle from code that has no Game reference (bubble behaviours). Does nothing when no game is running.</summary>
        public static void Sparkle(Vector2 at, Color32 color)
        {
            if (active != null && active.fx != null) active.fx.Sparkle(at, color);
        }
    }
}
