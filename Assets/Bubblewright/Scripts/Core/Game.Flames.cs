using UnityEngine;

namespace Bubblewright
{
    public partial class Game
    {
        /// <summary>The flames Scorch bubbles leave behind. Null until the game has started.</summary>
        public static Flames Flames { get; private set; }

        void EnsureFlames()
        {
            if (Flames == null && world != null) Flames = Flames.Create(world);
        }

        void ClearFlames() => Flames?.Clear();

        void DrawFlameHurtBox(Rect box) => DrawWorldRect(box, Palette.Scorch);
    }
}
