using System.Collections.Generic;
using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// A boss: parts (armour and cores) that move with it, and phases. It slides in from above during the stage
    /// intro, then its current phase runs. Weapons hurt it only through BossFight.Hit, which lets an open core take
    /// the damage and clinks off armour. When a phase's health segment is gone the boss staggers, then the next phase
    /// starts; the last phase ending is the defeat. It has no Update of its own; Game ticks it, so hit-stop, slow
    /// motion and pause freeze it. Everything about one boss is in its BossDef.
    ///
    /// The legacy behaviour below (the slide left and right near the ceiling with aimed, bouncy and balloon attacks
    /// from a BossSpec) is what LegacyBoss runs for a world with no new boss yet. It goes with Task 10.
    /// </summary>
    public class Boss : MonoBehaviour
    {
        const float BobAmplitude = 0.15f;  // 0.3 units from top to bottom
        const float BobPeriod = 2f;
        const float TellTime = 0.5f;       // the lower band blinks this long before each shot
        const float TellBlink = 0.125f;    // one blink phase: on, off, on, off

        public BossDef Def { get; private set; }
        /// <summary>The legacy spec; null for a boss that is not the LegacyBoss.</summary>
        public BossSpec Spec => (Def as LegacyBoss)?.Spec;
        public string Name => Def.Name;
        public Vector2 Size => Def.Size;

        /// <summary>Health left over every phase: the current segment plus every later one.</summary>
        public int Hp
        {
            get
            {
                int hp = PhaseHp;
                for (int i = PhaseIndex + 1; i < Phases.Count; i++) hp += SegmentHp(Phases[i]);
                return hp;
            }
        }
        /// <summary>Health of all the segments together (a phase won by parts counts as 1).</summary>
        public int MaxHp { get; private set; }

        public IReadOnlyList<BossPhase> Phases => phases;
        public int PhaseIndex { get; private set; }
        public BossPhase Phase => phases[PhaseIndex];
        /// <summary>Health left in the current phase's segment.</summary>
        public int PhaseHp { get; private set; }
        public bool IsLastPhase => PhaseIndex >= phases.Length - 1;
        /// <summary>True during the pause between phases: nothing moves or fires and nothing hurts it.</summary>
        public bool Staggering { get; private set; }
        public float StaggerLeft { get; private set; }

        public IReadOnlyList<BossPart> Parts => parts;

        /// <summary>The boss lost a phase and is about to stagger: Game clears bullets, changes the arena and drops a crate. Gets the old phase's index.</summary>
        public System.Action<int> OnPhaseBreak;
        /// <summary>A weapon clinked off armour or a closed core at this point.</summary>
        public System.Action<Vector2> OnClink;
        /// <summary>An open core (or a part with health) took damage: how much, and where.</summary>
        public System.Action<int, Vector2> OnHurt;
        /// <summary>A part with health of its own broke.</summary>
        public System.Action<BossPart> OnPartBroken;
        /// <summary>True for the boss of a hard world: more health, a faster walk and faster attacks.</summary>
        public bool Hard { get; private set; }
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

        /// <summary>The rectangle Wick's body touches. What a weapon can hurt is decided by the parts.</summary>
        public Rect Body => new Rect(x - Size.x * 0.5f, y - Size.y * 0.5f, Size.x, Size.y);
        public Vector2 Centre => new Vector2(x, y);
        /// <summary>Where bullets leave from: the bottom centre of the body.</summary>
        public Vector2 Muzzle => new Vector2(x, y - Size.y * 0.5f);
        /// <summary>Puts the boss at a place; phases move the boss with this.</summary>
        public void SetPosition(float newX, float newY) { x = newX; y = newY; UpdateVisual(); }

        Tuning T => Tuning.I;
        float EntranceTime => Mathf.Max(0.01f, T.introTime);

        readonly List<BossPart> parts = new List<BossPart>();
        BossPhase[] phases;
        float clinkCooldown;
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

        /// <summary>Creates a legacy boss from a spec (today's six bosses); hard is true when the run took the hard road into that world.</summary>
        public static Boss Create(Transform parent, BossSpec spec, int world, Theme theme, bool hard = false) =>
            Create(parent, new LegacyBoss(world, spec), theme, hard);

        /// <summary>Creates the boss of a def: its parts, its phases (the first one entered) and its sprites.</summary>
        public static Boss Create(Transform parent, BossDef def, Theme theme, bool hard = false)
        {
            var go = new GameObject("Boss " + def.Name);
            go.transform.SetParent(parent, false);
            var boss = go.AddComponent<Boss>();
            boss.Def = def;
            boss.Hard = hard;
            boss.phases = def.CreatePhases(hard);
            boss.MaxHp = 0;
            foreach (var p in boss.phases) boss.MaxHp += SegmentHp(p);
            boss.sprites = def.Sprites(theme);

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
            def.BuildParts(boss, hard);
            if (hard) def.ApplyHard(boss);
            boss.PhaseIndex = 0;
            boss.PhaseHp = SegmentHp(boss.phases[0]);
            boss.phases[0].Enter(boss);
            boss.UpdateVisual();
            return boss;
        }

        static int SegmentHp(BossPhase p) => Mathf.Max(1, p.hp);

        /// <summary>True below half health for a boss that enrages.</summary>
        public bool Enraged => Spec != null && Spec.enrageAtHalf && Hp > 0 && Hp < MaxHp * 0.5f;
        /// <summary>Multiplier on speed and every attack rate: bossEnrageScale while enraged, otherwise 1.</summary>
        public float RageScale => Enraged ? T.bossEnrageScale : 1f;
        /// <summary>Multiplier on the walk from the hard road: hardBossSpeedScale in a hard world, otherwise 1.</summary>
        public float HardSpeedScale => Hard ? T.hardBossSpeedScale : 1f;
        /// <summary>Multiplier on every attack rate from the hard road: hardBossRateScale in a hard world, otherwise 1.</summary>
        public float HardRateScale => Hard ? T.hardBossRateScale : 1f;
        /// <summary>How much faster than its spec every attack timer runs right now: the enrage times the hard road.</summary>
        public float AttackRateScale => RageScale * HardRateScale;
        /// <summary>Walking speed right now, before the storm: the spec's speed times the enrage and the hard road.</summary>
        public float CurrentSpeed => (Spec != null ? Spec.speed : 0f) * RageScale * HardSpeedScale;
        /// <summary>True while the visor is open after releasing a balloon.</summary>
        public bool Open => open > 0f;

        float StartY => T.arenaHeight + Size.y * 0.5f + 0.5f; // fully above the ceiling

        /// <summary>
        /// The current phase's segment takes damage (it never spills into the next one) and the boss shows a cream
        /// silhouette for bossHitFlash seconds. Weapons do not call this: they go through BossFight.Hit.
        /// </summary>
        public void Damage(int amount)
        {
            if (Hp <= 0 || amount <= 0) return;
            PhaseHp = Mathf.Max(0, PhaseHp - amount);
            flash = T.bossHitFlash;
            UpdateVisual();
        }

        /// <summary>Takes the whole of the boss's health: every phase. Used by the debug page.</summary>
        public void Kill()
        {
            PhaseIndex = phases.Length - 1;
            PhaseHp = 0;
            Staggering = false;
            UpdateVisual();
        }

        /// <summary>An open core was hit: damage, then the callback that Game uses for hit-stop, sound and sparks.</summary>
        public void Hurt(int amount, Vector2 at)
        {
            if (Hp <= 0) return;
            Damage(amount);
            OnHurt?.Invoke(amount, at);
        }

        /// <summary>An open part with health was hit: it loses health, and may break.</summary>
        public void HurtPart(BossPart part, int amount, Vector2 at)
        {
            bool broke = part.Damage(amount);
            flash = T.bossHitFlash;
            OnHurt?.Invoke(amount, at);
            if (broke) { part.Open = false; OnPartBroken?.Invoke(part); }
            UpdateVisual();
        }

        /// <summary>A weapon hit armour or a closed core: spark and a short high sound, at most once per bossClinkInterval.</summary>
        public void Clink(Vector2 at)
        {
            if (clinkCooldown > 0f) return;
            clinkCooldown = T.bossClinkInterval;
            OnClink?.Invoke(at);
        }

        /// <summary>True while a hit can land at all: not while the boss staggers, dies or is already dead.</summary>
        public bool CanBeHit => !Dying && !Staggering && Hp > 0;

        // ------------------------------------------------------------------ parts

        public void AddPart(BossPart part) { if (part != null && !parts.Contains(part)) parts.Add(part); }
        public bool RemovePart(BossPart part) => parts.Remove(part);
        public BossPart Part(string name) => parts.Find(p => p.Name == name);

        /// <summary>The first part (in the order they were added) that the area overlaps, or null.</summary>
        public BossPart FirstPartTouching(Rect area)
        {
            foreach (var p in parts)
                if (p.World(this).Overlaps(area)) return p;
            return null;
        }

        /// <summary>True when the area touches any part.</summary>
        public bool Touches(Rect area) => FirstPartTouching(area) != null;

        /// <summary>The part nearest to a point (the first one when several are as near); null for a boss with no parts.</summary>
        public BossPart NearestPart(Vector2 p)
        {
            BossPart best = null;
            float bestD = float.MaxValue;
            foreach (var part in parts)
            {
                float d = Vector2.Distance(Closest(part.World(this), p), p);
                if (d < bestD - 1e-5f) { best = part; bestD = d; }
            }
            return best;
        }

        /// <summary>The point on any part that is nearest to p. A boss with no parts uses its body.</summary>
        public Vector2 ClosestPoint(Vector2 p)
        {
            var part = NearestPart(p);
            return Closest(part != null ? part.World(this) : Body, p);
        }

        static Vector2 Closest(Rect r, Vector2 p) => new Vector2(Mathf.Clamp(p.x, r.xMin, r.xMax), Mathf.Clamp(p.y, r.yMin, r.yMax));

        /// <summary>Opens or closes every core and part with health at once (the debug page, and phases that open everything).</summary>
        public void SetCoresOpen(bool open)
        {
            foreach (var p in parts) if ((p.IsCore || p.HasHealth) && !p.Broken) p.Open = open;
        }

        /// <summary>Closes every core (and every part with health): a phase break and the stagger.</summary>
        public void CloseAllParts()
        {
            foreach (var p in parts) if (p.IsCore || p.HasHealth) p.Open = false;
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
            if (clinkCooldown > 0f) clinkCooldown -= dt;

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
                if (Staggering)
                {
                    StaggerLeft -= dt;
                    if (StaggerLeft <= 0f) EndStagger();
                }
                else
                {
                    Phase.Tick(this, dt, target, speedScale, bubblesAlive);
                    CheckPhase();
                }
            }
            else if (!Staggering) Phase.Idle(this, dt);
            UpdateVisual();
        }

        /// <summary>Ends the phase when it is done: the last one is the defeat (health 0), any other starts the stagger.</summary>
        void CheckPhase()
        {
            if (Hp <= 0 || !Phase.Done(this)) return;
            if (IsLastPhase) { PhaseHp = 0; return; }
            Phase.Exit(this);
            Staggering = true;
            StaggerLeft = T.bossStaggerTime;
            CloseAllParts();
            OnPhaseBreak?.Invoke(PhaseIndex);
        }

        void EndStagger()
        {
            Staggering = false;
            PhaseIndex++;
            PhaseHp = SegmentHp(Phase);
            Phase.Enter(this);
        }

        // ------------------------------------------------------------------ legacy behaviour (LegacyBoss runs this)

        /// <summary>
        /// The legacy walk and attacks: slides left and right, bobbing, and fires from its spec's timers.
        /// speedScale is above 1 during the storm and speeds up the walk (not the attack rhythm).
        /// </summary>
        internal void TickLegacy(float dt, Vector2 target, float speedScale, int bubblesAlive)
        {
            bob += dt;
            x += Direction * CurrentSpeed * speedScale * dt;
            float limit = T.HalfWidth - Size.x * 0.5f;
            if (x >= limit) { x = limit; Direction = -1; }
            else if (x <= -limit) { x = -limit; Direction = 1; }

            y = T.bossY + Mathf.Sin(bob / BobPeriod * Mathf.PI * 2f) * BobAmplitude;

            TickAttacks(dt * AttackRateScale, target, bubblesAlive);
        }

        /// <summary>The legacy bob when nothing else moves the boss.</summary>
        internal void IdleLegacy() => y = T.bossY + Mathf.Sin(bob / BobPeriod * Mathf.PI * 2f) * BobAmplitude;

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
                if (Dying || !Entered || Spec == null || Spec.aimedEvery <= 0f) return false;
                float remaining = aimedTimer / AttackRateScale;
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
