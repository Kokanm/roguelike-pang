using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// A falling weapon crate, like the heart pickup: it falls, rests on the surface below (never over a gap), lasts
    /// Tuning.weaponCrateLife seconds and blinks for the last weaponCrateBlink. Game ticks it, so pause, slow motion and
    /// hit-stop affect it, and decides what touching it does (Run.GiveWeapon; a refused crate stays).
    /// </summary>
    public class WeaponCrate : MonoBehaviour
    {
        public const int SizePixels = 17;

        public WeaponSpec Spec { get; private set; }
        public bool Active { get; private set; }
        public bool Resting { get; private set; }
        public float Age { get; private set; }
        public Vector2 Position => transform.position;

        SpriteRenderer sr;
        float fallSpeed;

        public static WeaponCrate Create(Transform parent, Vector2 at, WeaponSpec spec)
        {
            var go = new GameObject("Weapon Crate " + spec?.Id);
            go.transform.SetParent(parent, false);
            var map = Game.Map;
            if (map != null && map.FloorBelow(at.x, at.y) < 0f) at.x = map.NearestSolidGroundX(at.x);
            float half = SizePixels * 0.5f * Tuning.I.Pixel;
            at.x = Mathf.Clamp(at.x, -Tuning.I.HalfWidth + half, Tuning.I.HalfWidth - half);
            go.transform.position = at;

            var crate = go.AddComponent<WeaponCrate>();
            crate.Spec = spec;
            crate.sr = go.AddComponent<SpriteRenderer>();
            crate.sr.sprite = WeaponArt.Crate(spec);
            Sorting.Apply(crate.sr, Layer.FX, 0);
            Game.ApplySpriteMaterial(crate.sr);
            crate.Active = true;
            return crate;
        }

        /// <summary>Its box: 17 x 17 px standing on its position.</summary>
        public Rect Box
        {
            get
            {
                float s = SizePixels * Tuning.I.Pixel;
                return new Rect(transform.position.x - s * 0.5f, transform.position.y, s, s);
            }
        }

        /// <summary>Falls and ages. Returns true while it touches Wick. A crate that ran out of time turns inactive.</summary>
        public bool Tick(float dt, StageMap map, Rect wickHitbox)
        {
            if (!Active) return false;
            var t = Tuning.I;
            Age += dt;
            if (Age >= t.weaponCrateLife)
            {
                Active = false;
                sr.enabled = false;
                return false;
            }

            if (!Resting)
            {
                float y = transform.position.y;
                fallSpeed = Mathf.Min(t.maxFallSpeed, fallSpeed + t.fallGravity * dt);
                float next = y - fallSpeed * dt;
                float floor = map != null ? map.FloorBelow(transform.position.x, y) : 0f;
                if (next <= floor)
                {
                    next = floor;
                    fallSpeed = 0f;
                    Resting = true;
                }
                transform.position = new Vector3(transform.position.x, next, 0f);
            }

            float left = t.weaponCrateLife - Age;
            sr.enabled = left > t.weaponCrateBlink || Mathf.FloorToInt(left * 8f) % 2 == 0;
            return Box.Overlaps(wickHitbox);
        }
    }
}
