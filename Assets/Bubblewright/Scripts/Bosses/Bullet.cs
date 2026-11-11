using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// A boss bullet: a disc that flies in a straight line. No physics. Game ticks it, so hit-stop, slow motion
    /// and pause freeze it. Harpoons do not stop bullets and bullets do not stop harpoons.
    /// </summary>
    public class Bullet : MonoBehaviour
    {
        public Vector2 Velocity;
        public float Radius { get; private set; }
        public Vector2 Position => transform.position;
        public bool Dead { get; set; }

        public static Bullet Create(Transform parent, Vector2 position, Vector2 velocity, Theme theme)
        {
            var go = new GameObject("Bullet");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var b = go.AddComponent<Bullet>();
            b.Velocity = velocity;
            b.Radius = Tuning.I.bulletRadiusPixels / (float)Tuning.I.pixelsPerUnit;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PlaceholderArt.I.BulletFor(theme);
            Sorting.Apply(sr, Layer.Bubbles, 20); // over every bubble
            Game.ApplySpriteMaterial(sr);
            return b;
        }

        /// <summary>The velocity of a bullet that leaves from and flies toward target at speed.</summary>
        public static Vector2 Aim(Vector2 from, Vector2 target, float speed)
        {
            Vector2 d = target - from;
            if (d.sqrMagnitude < 1e-6f) d = Vector2.down;
            return d.normalized * speed;
        }

        public void Tick(float dt)
        {
            transform.position += (Vector3)(Velocity * dt);
        }

        /// <summary>True when the bullet has left the arena or reached the floor.</summary>
        public bool Out
        {
            get
            {
                var p = Position;
                float half = Tuning.I.HalfWidth;
                return p.y - Radius <= 0f || p.y - Radius > Tuning.I.arenaHeight || p.x + Radius < -half || p.x - Radius > half;
            }
        }

        public bool Overlaps(Rect rect)
        {
            var c = Position;
            float cx = Mathf.Clamp(c.x, rect.xMin, rect.xMax);
            float cy = Mathf.Clamp(c.y, rect.yMin, rect.yMax);
            float dx = c.x - cx, dy = c.y - cy;
            return dx * dx + dy * dy < Radius * Radius;
        }
    }
}
