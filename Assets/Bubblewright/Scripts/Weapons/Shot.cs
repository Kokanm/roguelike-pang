using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// The shared spark bullet: 3 x 6 px, red with a yellow core and an ink outline. It flies in a straight line, is
    /// removed at the walls, ceiling and floor, stops at a solid tile (chipping a breakable one once) and stops at the
    /// first thing it touches. WeaponWorld owns, ticks and pools every Shot; weapons only call WeaponWorld.FireShot.
    /// </summary>
    public class Shot
    {
        public const int WidthPixels = 3, HeightPixels = 6;

        public Vector2 Position;
        public Vector2 Velocity;
        /// <summary>Where it was fired from. The storey clamp uses this, so a shot that crosses y = 14 still belongs to the storey below.</summary>
        public float OriginY;
        /// <summary>Boss health this shot takes when it hits. Fractions add up in WeaponWorld.</summary>
        public float BossDamage;
        /// <summary>Units it may travel before it fades (pellets). Infinity for a normal bullet.</summary>
        public float Range = float.PositiveInfinity;
        public float Travelled;
        /// <summary>
        /// Things this shot passes through as if they were not there (a bubble or rolling balloon). The Scattergun sets it
        /// so a blast's pellets never hit the halves its own pops made. Cleared by FireShot.
        /// </summary>
        public System.Predicate<object> Ignores;
        public bool Alive;

        internal GameObject go;
        internal SpriteRenderer sr;

        /// <summary>The hit box: 3 x 6 px centred on Position (6 x 3 when it flies sideways).</summary>
        public Rect Box
        {
            get
            {
                float px = Tuning.I.Pixel;
                bool sideways = Mathf.Abs(Velocity.x) > Mathf.Abs(Velocity.y);
                float w = (sideways ? HeightPixels : WidthPixels) * px, h = (sideways ? WidthPixels : HeightPixels) * px;
                return new Rect(Position.x - w * 0.5f, Position.y - h * 0.5f, w, h);
            }
        }

        internal void Show()
        {
            if (go == null) return;
            go.SetActive(Alive);
            if (!Alive) return;
            float px = Tuning.I.Pixel;
            go.transform.position = new Vector3(Mathf.Round(Position.x / px) * px, Mathf.Round(Position.y / px) * px, 0f);
            float angle = Velocity.sqrMagnitude > 0f ? Mathf.Atan2(Velocity.y, Velocity.x) * Mathf.Rad2Deg - 90f : 0f;
            go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Round(angle / 90f) * 90f); // stays on the pixel grid
        }
    }
}
