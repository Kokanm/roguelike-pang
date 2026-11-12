using UnityEngine;

namespace Bubblewright
{
    /// <summary>A falling heart flame. Game ticks it so pause, slow motion and hit-stop affect it.</summary>
    public class Pickup : MonoBehaviour
    {
        const int WidthPixels = 13;
        const int HeightPixels = 16;

        public bool Active { get; private set; }
        public Vector2 Position => transform.position;

        SpriteRenderer ring;
        SpriteRenderer flame;
        float age;
        float fallSpeed;
        bool resting;

        public static Pickup Create(Transform parent, Vector2 at)
        {
            var go = new GameObject("Heart Pickup");
            go.transform.SetParent(parent, false);
            // Never come to rest over a gap in the ground: drop onto the nearest solid ground column instead.
            var map = Game.Map;
            if (map != null && map.FloorBelow(at.x, at.y) < 0f) at.x = map.NearestSolidGroundX(at.x);
            go.transform.position = at;

            var pickup = go.AddComponent<Pickup>();
            pickup.ring = MakePart(go.transform, "Ring", PlaceholderArt.I.PickupRing, 0);
            pickup.flame = MakePart(go.transform, "Flame", PlaceholderArt.I.Flame, 1);
            pickup.Active = true;
            return pickup;
        }

        static SpriteRenderer MakePart(Transform parent, string name, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            Sorting.Apply(sr, Layer.FX, order);
            Game.ApplySpriteMaterial(sr);
            return sr;
        }

        /// <summary>Returns true when Wick collects it. Expired pickups simply become inactive.</summary>
        public bool Tick(float dt, StageMap map, Rect wickHitbox)
        {
            if (!Active) return false;

            age += dt;
            if (age >= Tuning.I.heartPickupLife)
            {
                Hide();
                return false;
            }

            if (!resting)
            {
                float previousY = transform.position.y;
                fallSpeed = Mathf.Min(Tuning.I.maxFallSpeed, fallSpeed + Tuning.I.fallGravity * dt);
                float nextY = previousY - fallSpeed * dt;
                float floor = map != null ? map.FloorBelow(transform.position.x, previousY) : 0f;
                if (nextY <= floor)
                {
                    nextY = floor;
                    fallSpeed = 0f;
                    resting = true;
                }
                transform.position = new Vector3(transform.position.x, nextY, 0f);
            }

            float px = Tuning.I.Pixel;
            var box = new Rect(transform.position.x - WidthPixels * px * 0.5f,
                transform.position.y, WidthPixels * px, HeightPixels * px);
            if (box.Overlaps(wickHitbox))
            {
                Hide();
                return true;
            }

            float remaining = Tuning.I.heartPickupLife - age;
            SetVisible(remaining > 2f || Mathf.FloorToInt(remaining * 8f) % 2 == 0);
            return false;
        }

        void Hide()
        {
            Active = false;
            SetVisible(false);
        }

        void SetVisible(bool visible)
        {
            ring.enabled = visible;
            flame.enabled = visible;
        }
    }
}
