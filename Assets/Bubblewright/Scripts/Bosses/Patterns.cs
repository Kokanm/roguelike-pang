using System;
using UnityEngine;

namespace Bubblewright
{
    public enum BulletKind { Aimed, Bouncy, Wave, Shell, Missile, Stream }

    /// <summary>
    /// One bullet a pattern wants to fire: where, how fast, what kind, and after how many seconds (a burst). A launch
    /// is plain data so patterns can be tested without a scene; Game turns launches into Bullets.
    /// </summary>
    public struct Launch
    {
        public Vector2 position, velocity;
        public BulletKind kind;
        /// <summary>Seconds after the pattern starts that this bullet leaves.</summary>
        public float delay;
        /// <summary>Missile and stream: seconds it lives. Bouncy: floor bounces.</summary>
        public float life;
        /// <summary>Missile: degrees per second it may turn.</summary>
        public float turnRate;
        /// <summary>Ground wave: the y of the surface it rides.</summary>
        public float surfaceY;
        /// <summary>Arcing shell: runs where it lands.</summary>
        public Action<Vector2> onLand;
    }

    /// <summary>
    /// The boss bullet patterns of the spec, as pure functions that return launches. They never touch a scene; the
    /// numbers come from Tuning. Angles are in degrees, 0 pointing right and 90 up.
    /// </summary>
    public static class Patterns
    {
        static Vector2 Dir(float degrees)
        {
            float a = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        }

        /// <summary>A single bullet from from toward target.</summary>
        public static Launch Aimed(Vector2 from, Vector2 target, float speed) => new Launch
        {
            kind = BulletKind.Aimed,
            position = from,
            velocity = Bullet.Aim(from, target, speed),
        };

        /// <summary>count bullets toward target, spreadDegrees apart, the middle one (or the pair around it) on the aim.</summary>
        public static Launch[] Fan(Vector2 from, Vector2 target, float speed, int count, float spreadDegrees)
        {
            count = Mathf.Max(1, count);
            var aim = Bullet.Aim(from, target, speed);
            float centre = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
            var result = new Launch[count];
            for (int i = 0; i < count; i++)
            {
                float angle = centre + (i - (count - 1) * 0.5f) * -spreadDegrees; // first bullet on the high side
                result[i] = new Launch { kind = BulletKind.Aimed, position = from, velocity = Dir(angle) * speed };
            }
            return result;
        }

        /// <summary>count bullets one after another at the same aim, gap seconds apart.</summary>
        public static Launch[] Burst(Vector2 from, Vector2 target, float speed, int count, float gap)
        {
            count = Mathf.Max(1, count);
            var result = new Launch[count];
            for (int i = 0; i < count; i++)
            {
                result[i] = Aimed(from, target, speed);
                result[i].delay = i * gap;
            }
            return result;
        }

        /// <summary>
        /// A full ring of count slots outward from centre, with gapWidth neighbouring slots left empty from slot
        /// gapStart on. Slot i points at 360 * i / count degrees. The caller turns the gap a little each time.
        /// </summary>
        public static Launch[] RingWithGap(Vector2 centre, int count, int gapStart, int gapWidth, float speed)
        {
            count = Mathf.Max(1, count);
            gapWidth = Mathf.Clamp(gapWidth, 0, count);
            var result = new Launch[count - gapWidth];
            int n = 0;
            for (int i = 0; i < count; i++)
            {
                int fromGap = ((i - gapStart) % count + count) % count;
                if (fromGap < gapWidth) continue;
                result[n++] = new Launch { kind = BulletKind.Aimed, position = centre, velocity = Dir(360f * i / count) * speed };
            }
            return result;
        }

        /// <summary>
        /// A row of bullets falling from the ceiling of the storey that contains storeyY, one per column across the full
        /// width, except the laneWidth columns from laneCol on. The lane is where Wick is safe.
        /// </summary>
        public static Launch[] CurtainWithLane(float storeyY, int laneCol, int laneWidth)
        {
            float ceiling = (Mathf.FloorToInt(storeyY / StageMap.StoreyRows) + 1) * (float)StageMap.StoreyRows;
            float radius = Tuning.I.bulletRadiusPixels / (float)Tuning.I.pixelsPerUnit;
            laneWidth = Mathf.Clamp(laneWidth, 0, StageMap.Cols);
            var result = new Launch[StageMap.Cols - laneWidth];
            int n = 0;
            for (int c = 0; c < StageMap.Cols; c++)
            {
                if (c >= laneCol && c < laneCol + laneWidth) continue;
                result[n++] = new Launch
                {
                    kind = BulletKind.Aimed,
                    position = new Vector2(StageMap.CenterX(c), ceiling - radius - 0.05f),
                    velocity = Vector2.down * Tuning.I.bossCurtainSpeed,
                };
            }
            return result;
        }

        /// <summary>arms bullets evenly spaced round the boss, the first at startAngle. The caller adds a step each call to turn it.</summary>
        public static Launch[] Spiral(Vector2 centre, int arms, float startAngle, float speed)
        {
            arms = Mathf.Max(1, arms);
            var result = new Launch[arms];
            for (int i = 0; i < arms; i++)
                result[i] = new Launch { kind = BulletKind.Aimed, position = centre, velocity = Dir(startAngle + 360f * i / arms) * speed };
            return result;
        }

        /// <summary>A wave along the surface at surfaceY from x. dir is 1 (right), -1 (left) or 0 (both ways).</summary>
        public static Launch[] GroundWave(float surfaceY, float x, int dir)
        {
            float speed = Tuning.I.bossWaveSpeed;
            Launch Make(int d) => new Launch
            {
                kind = BulletKind.Wave,
                position = new Vector2(x, surfaceY),
                surfaceY = surfaceY,
                velocity = new Vector2(d * speed, 0f),
            };
            return dir == 0 ? new[] { Make(-1), Make(1) } : new[] { Make(dir > 0 ? 1 : -1) };
        }

        /// <summary>A shell lobbed from from so that it comes down on target; onLand runs where it lands.</summary>
        public static Launch ArcingShell(Vector2 from, Vector2 target, Action<Vector2> onLand)
        {
            var t = Tuning.I;
            float time = Mathf.Max(t.bossShellMinTime, Mathf.Abs(target.x - from.x) / Mathf.Max(0.01f, t.bossShellSpeedX));
            float vx = (target.x - from.x) / time;
            float vy = (target.y - from.y + 0.5f * t.bossShellGravity * time * time) / time;
            return new Launch { kind = BulletKind.Shell, position = from, velocity = new Vector2(vx, vy), onLand = onLand };
        }

        /// <summary>A slow missile that leaves along direction, turns toward Wick at most turnRate degrees a second and dies after life seconds.</summary>
        public static Launch HomingMissile(Vector2 from, Vector2 direction, float turnRate, float life)
        {
            if (direction.sqrMagnitude < 1e-6f) direction = Vector2.down;
            return new Launch
            {
                kind = BulletKind.Missile,
                position = from,
                velocity = direction.normalized * Tuning.I.bossMissileSpeed,
                turnRate = turnRate,
                life = life,
            };
        }

        /// <summary>A column that sweeps sideways from x0 to x1 at speed, standing on baseY (the floor of its storey).</summary>
        public static Launch SweepStream(float x0, float x1, float speed, float baseY)
        {
            float distance = Mathf.Abs(x1 - x0);
            float dir = x1 >= x0 ? 1f : -1f;
            return new Launch
            {
                kind = BulletKind.Stream,
                position = new Vector2(x0, baseY),
                velocity = new Vector2(dir * speed, 0f),
                life = distance / Mathf.Max(0.01f, speed),
            };
        }
    }
}
