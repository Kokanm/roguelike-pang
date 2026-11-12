using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// A boss: slides in from above during the stage intro, then moves left and right near the ceiling. Its spec
    /// picks from three attacks with their own timers: aimed bullets (single, fan or burst), bouncy bullets, and
    /// released balloons (ordinary Gloops). It has no Update of its own; Game ticks it, so hit-stop, slow motion and pause freeze it.
    /// Everything about one boss is in its BossSpec. Any harpoon touch hurts it through Damage.
    /// </summary>
    public class Boss : MonoBehaviour
    {
        const float BobAmplitude = 0.15f;  // 0.3 units from top to bottom
        const float BobPeriod = 2f;
        const float TellTime = 0.5f;       // the lower band blinks this long before each shot
        const float TellBlink = 0.125f;    // one blink phase: on, off, on, off

        public BossSpec Spec { get; private set; }
        public int Hp { get; private set; }
        public int MaxHp { get; private set; }
        /// <summary>Called when the boss fires: where from, and the direction (unit length) of each bullet.</summary>
        public System.Action<Vector2, Vector2> OnShoot;
        /// <summary>Called when the boss drops a bouncy bullet: where from, which way it heads (+1 right, -1 left), and its bounces.</summary>
        public System.Action<Vector2, int, int> OnBouncy;
        /// <summary>Called when the boss releases a balloon: where its centre starts, which way it moves, and its tier.</summary>
        public System.Action<Vector2, int, int> OnBalloon;

        public bool Dying { get; private set; }
        public float DyingTime { get; private set; }
        /// <summary>Which way it moves: +1 right, -1 left.</summary>
        public int Direction { get; private set; } = 1;
        public bool Entered => entrance >= EntranceTime;

        /// <summary>The rectangle harpoons, bullets and Wick test against.</summary>
        public Rect Body => new Rect(x - Spec.size.x * 0.5f, y - Spec.size.y * 0.5f, Spec.size.x, Spec.size.y);
        public Vector2 Centre => new Vector2(x, y);
        /// <summary>Where bullets leave from: the bottom centre of the body.</summary>
        public Vector2 Muzzle => new Vector2(x, y - Spec.size.y * 0.5f);

        Tuning T => Tuning.I;
        float EntranceTime => Mathf.Max(0.01f, T.introTime);

        PlaceholderArt.BossSprites sprites;
        SpriteRenderer body, tell;
        float x, y;
        float entrance, bob;
        float aimedTimer, bouncyTimer, balloonTimer; // seconds until each attack next fires
        int burstLeft;                                // aimed shots still to fire in the current burst
        float burstTimer;
        float open;                                   // seconds the visor stays open after releasing a balloon
        float flash;
        Vector2 shakeOffset;

        public static Boss Create(Transform parent, BossSpec spec, int world, Theme theme)
        {
            var go = new GameObject("Boss " + spec.name);
            go.transform.SetParent(parent, false);
            var boss = go.AddComponent<Boss>();
            boss.Spec = spec;
            boss.MaxHp = boss.Hp = BossSpec.HpForWorld(world);
            boss.sprites = PlaceholderArt.I.BossFor(theme, spec);

            boss.body = go.AddComponent<SpriteRenderer>();
            boss.body.sprite = boss.sprites.body;
            Sorting.Apply(boss.body, Layer.Bubbles, 50); // over every bubble and bullet
            Game.ApplySpriteMaterial(boss.body);

            var tellGo = new GameObject("Tell");
            tellGo.transform.SetParent(go.transform, false);
            boss.tell = tellGo.AddComponent<SpriteRenderer>();
            boss.tell.sprite = boss.sprites.tell;
            Sorting.Apply(boss.tell, Layer.Bubbles, 51);
            Game.ApplySpriteMaterial(boss.tell);
            boss.tell.enabled = false;

            boss.aimedTimer = BossSpec.AimedStart;
            boss.bouncyTimer = BossSpec.BouncyStart;
            boss.balloonTimer = BossSpec.BalloonStart;
            boss.x = 0f;
            boss.y = boss.StartY;
            boss.Direction = 1;
            boss.UpdateVisual();
            return boss;
        }

        /// <summary>True below half health for a boss that enrages.</summary>
        public bool Enraged => Spec.enrageAtHalf && Hp > 0 && Hp < MaxHp * 0.5f;
        /// <summary>Multiplier on speed and every attack rate: bossEnrageScale while enraged, otherwise 1.</summary>
        public float RageScale => Enraged ? T.bossEnrageScale : 1f;
        /// <summary>Walking speed right now, before the storm.</summary>
        public float CurrentSpeed => Spec.speed * RageScale;
        /// <summary>True while the visor is open after releasing a balloon.</summary>
        public bool Open => open > 0f;

        float StartY => T.arenaHeight + Spec.size.y * 0.5f + 0.5f; // fully above the ceiling

        /// <summary>The boss takes damage and shows a cream silhouette for bossHitFlash seconds.</summary>
        public void Damage(int amount)
        {
            if (Hp <= 0 || amount <= 0) return;
            Hp = Mathf.Max(0, Hp - amount);
            flash = T.bossHitFlash;
            UpdateVisual();
        }

        /// <summary>Hp reached 0: it stops moving and shooting, and shakes and flashes until Hide.</summary>
        public void BeginDefeat()
        {
            if (Dying) return;
            Dying = true;
            DyingTime = 0f;
            tell.enabled = false;
        }

        public void Hide()
        {
            body.enabled = tell.enabled = false;
        }

        /// <summary>
        /// Advances the boss. It only slides in until the intro is over, then moves and shoots. target is the centre
        /// of Wick's hitbox; speedScale is above 1 during the storm and speeds up the walk (not the attack rhythm).
        /// bubblesAlive is how many bubbles are in play: no balloon is released while bossMaxBubbles are alive.
        /// </summary>
        public void Tick(float dt, Vector2 target, bool playing, float speedScale = 1f, int bubblesAlive = 0)
        {
            if (flash > 0f) flash -= dt;
            if (open > 0f) open -= dt;

            if (Dying)
            {
                DyingTime += dt;
                float px = 1f / T.pixelsPerUnit;
                shakeOffset = new Vector2(Random.Range(-1, 2), Random.Range(-1, 2)) * px;
                UpdateVisual();
                return;
            }

            if (!Entered)
            {
                entrance = Mathf.Min(EntranceTime, entrance + dt);
                float k = Mathf.Clamp01(entrance / EntranceTime);
                float ease = 1f - (1f - k) * (1f - k);
                y = Mathf.Lerp(StartY, T.bossY, ease);
                if (Entered) y = T.bossY;
                UpdateVisual();
                return; // no sideways move and no shots until it has slid in
            }

            if (playing && Hp > 0)
            {
                bob += dt;
                x += Direction * CurrentSpeed * speedScale * dt;
                float limit = T.HalfWidth - Spec.size.x * 0.5f;
                if (x >= limit) { x = limit; Direction = -1; }
                else if (x <= -limit) { x = -limit; Direction = 1; }

                y = T.bossY + Mathf.Sin(bob / BobPeriod * Mathf.PI * 2f) * BobAmplitude;

                TickAttacks(dt * RageScale, target, bubblesAlive);
            }
            else y = T.bossY + Mathf.Sin(bob / BobPeriod * Mathf.PI * 2f) * BobAmplitude;
            UpdateVisual();
        }

        void TickAttacks(float dt, Vector2 target, int bubblesAlive)
        {
            if (Spec.aimedEvery > 0f)
            {
                aimedTimer -= dt;
                if (aimedTimer <= 0f)
                {
                    aimedTimer += Spec.aimedEvery;
                    FireAimed(target);
                    burstLeft = Mathf.Max(1, Spec.aimedBurst) - 1;
                    burstTimer = BossSpec.BurstGap;
                }
            }
            if (burstLeft > 0)
            {
                burstTimer -= dt;
                if (burstTimer <= 0f)
                {
                    burstTimer += BossSpec.BurstGap;
                    burstLeft--;
                    FireAimed(target);
                }
            }

            if (Spec.bouncyEvery > 0f)
            {
                bouncyTimer -= dt;
                if (bouncyTimer <= 0f)
                {
                    bouncyTimer += Spec.bouncyEvery;
                    FireBouncy(target);
                }
            }

            if (Spec.balloonEvery > 0f)
            {
                balloonTimer -= dt;
                if (balloonTimer <= 0f)
                {
                    balloonTimer += Spec.balloonEvery;
                    if (bubblesAlive < T.bossMaxBubbles) ReleaseBalloon();
                }
            }
        }

        /// <summary>One aimed shot at target (all the bullets of a fan). The debug panel calls it too.</summary>
        public void FireAimed(Vector2 target) => Shoot(target);

        /// <summary>One bouncy bullet from the muzzle, heading toward the side target is on.</summary>
        public void FireBouncy(Vector2 target)
        {
            int dir = target.x >= x ? 1 : -1;
            OnBouncy?.Invoke(Muzzle, dir, Spec.bouncyBounces);
        }

        /// <summary>The visor opens and one Gloop of balloonTier drops from the bottom centre, toward a random side.</summary>
        public void ReleaseBalloon()
        {
            open = 0.4f;
            int tier = Mathf.Clamp(Spec.balloonTier, 0, T.tiers.Length - 1);
            float r = T.tiers[tier].diameter * 0.5f;
            int dir = Random.value < 0.5f ? -1 : 1;
            OnBalloon?.Invoke(Muzzle + Vector2.down * r, dir, tier);
            UpdateVisual();
        }

        void Shoot(Vector2 target)
        {
            var from = Muzzle;
            foreach (var dir in Directions(from, target, Spec.aimedCount, Spec.aimedSpread))
                OnShoot?.Invoke(from, dir);
        }

        /// <summary>Unit directions from from toward target; with count above 1 they fan out evenly across spread degrees.</summary>
        public static Vector2[] Directions(Vector2 from, Vector2 target, int count, float spreadDegrees)
        {
            count = Mathf.Max(1, count);
            var center = Bullet.Aim(from, target, 1f);
            var result = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                float offset = count == 1 ? 0f : (i / (float)(count - 1) - 0.5f) * spreadDegrees;
                result[i] = Quaternion.Euler(0f, 0f, offset) * center;
            }
            return result;
        }

        /// <summary>True during the blink that warns of the next shot.</summary>
        public bool TellOn
        {
            get
            {
                if (Dying || !Entered || Spec.aimedEvery <= 0f) return false;
                float remaining = aimedTimer / RageScale;
                if (remaining > TellTime) return false;
                float t = TellTime - remaining;
                return t < TellBlink || (t >= TellBlink * 2f && t < TellBlink * 3f);
            }
        }

        void UpdateVisual()
        {
            float px = 1f / T.pixelsPerUnit;
            var p = new Vector2(Mathf.Round((x + shakeOffset.x) / px) * px, Mathf.Round((y + shakeOffset.y) / px) * px);
            transform.position = new Vector3(p.x, p.y, 0f);
            bool flashing = flash > 0f || (Dying && Mathf.FloorToInt(DyingTime * 16f) % 2 == 0);
            body.sprite = flashing ? sprites.flash : Open ? sprites.open : sprites.body;
            tell.enabled = !flashing && TellOn;
        }
    }
}
