using System.Collections.Generic;
using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// Boss stages: the boss, its bullets and balloons, damage to the boss, hurt to Wick, the defeat sequence and the
    /// health bar. A boss stage is one flat floor with no generated bubbles; the boss may release ordinary Gloops,
    /// which behave and score like any other bubble. It is cleared when the boss's health reaches 0.
    /// </summary>
    public partial class Game
    {
        Boss boss;
        readonly List<Bullet> bullets = new List<Bullet>();
        bool bossBurst;   // the final burst has happened and the clear flow is next
        Tells tells;      // boss tells and delayed shots; ticks with the boss so hit-stop, slow motion and pause freeze it

        void ClearBoss()
        {
            foreach (var b in bullets) if (b) Destroy(b.gameObject);
            bullets.Clear();
            tells?.Clear();
            tells = null;
            if (boss != null)
            {
                boss.gameObject.SetActive(false);
                Destroy(boss.gameObject);
                boss = null;
            }
            bossBurst = false;
        }

        /// <summary>
        /// Called from StartStage after ClearBoss: a boss stage gets a boss of its world, ready to slide in. On the hard
        /// road it is the harder boss (more health, faster walk, faster attacks).
        /// </summary>
        void SetupBoss()
        {
            if (!run.IsBossStage) return;
            boss = Boss.Create(world, BossDef.ForWorld(run.World), CurrentTheme, run.IsHard);
            boss.OnHurt = BossHurt;
            boss.OnClink = BossClink;
            boss.OnPhaseBreak = BossPhaseBreak;
            boss.OnShoot = FireBullet;
            boss.OnBouncy = FireBouncy;
            boss.OnBalloon = ReleaseBalloon;
            tells = new Tells(world);
        }

        /// <summary>Fires one pattern launch now, or after its delay (the later shots of a burst).</summary>
        void FireLaunch(Launch launch)
        {
            if (launch.delay > 0f && tells != null)
            {
                float delay = launch.delay;
                launch.delay = 0f;
                tells.After(delay, () => FireLaunch(launch));
                return;
            }
            bullets.Add(Bullet.Create(world, launch, CurrentTheme));
        }

        void FirePattern(Launch[] launches)
        {
            foreach (var l in launches) FireLaunch(l);
        }

        /// <summary>
        /// A telegraphed attack: shows the tell over area now and runs attack when it ends, at least bossTellMin
        /// seconds later. Bosses call this for every pattern that has a tell.
        /// </summary>
        float Telegraph(TellKind kind, Rect area, float lead, System.Action attack) =>
            tells != null ? tells.Add(kind, area, lead, attack) : 0f;

        void FireBouncy(Vector2 from, int dirX, int bounces)
        {
            bullets.Add(Bullet.CreateBouncy(world, from, dirX, bounces, CurrentTheme));
        }

        void ReleaseBalloon(Vector2 at, int dir, int tier)
        {
            SpawnBubble(BubbleKind.Gloop, tier, at, dir, 0f, false);
            Sfx.Play(Sound.Bounce, 0.7f, 0.6f);
        }

        void FireBullet(Vector2 from, Vector2 direction)
        {
            bullets.Add(Bullet.Create(world, from, direction * T.bulletSpeed, CurrentTheme));
        }

        float BossSpeedScale => storm ? T.bossStormScale : 1f;

        Vector2 WickCentre => wick.Hitbox.center;

        /// <summary>The boss slides down during the intro; it does not move sideways or shoot yet.</summary>
        void TickBossIntro(float dt)
        {
            boss?.Tick(dt, WickCentre, false);
        }

        /// <summary>One tick of a boss fight in the Play state. Returns true when the stage is cleared and Play should end.</summary>
        bool TickBossPlay(float dt)
        {
            if (boss.Dying)
            {
                TickBossDefeat(dt);
                return bossBurst;
            }

            boss.Tick(dt, WickCentre, true, BossSpeedScale, bubbles.Count);
            tells?.Tick(dt * BossSpeedScale);
            TickBullets(dt);

            BossFight.HarpoonHits(boss, harpoons);

            if (BossFight.StageCleared(boss, bubbles.Count))
            {
                BeginBossDefeat();
                return false;
            }

            if (!debug.god && !wick.Invulnerable && !wick.Dead)
            {
                if (wick.Hitbox.Overlaps(boss.Body))
                    HurtWick(boss.Centre.x, T.bossContactDamage);
            }
            return false;
        }

        void TickBullets(float dt)
        {
            float scale = BossSpeedScale;
            for (int i = bullets.Count - 1; i >= 0; i--)
            {
                var b = bullets[i];
                b.Tick(dt * scale, WickCentre);
                if (!b.Dead && !wick.Dead && b.CanHurt && b.Overlaps(wick.Hitbox))
                {
                    b.Dead = true;
                    if (!debug.god && !wick.Invulnerable)
                        HurtWick(b.Position.x, storm ? T.stormDamage : 1);
                }
                if (b.Dead || b.Out)
                {
                    Destroy(b.gameObject);
                    bullets.RemoveAt(i);
                }
            }
        }

        /// <summary>An open core took damage (BossFight.Hit calls this through Boss.OnHurt): hit-stop, sound and a sparkle.</summary>
        void BossHurt(int amount, Vector2 at)
        {
            freeze = Mathf.Max(freeze, T.bossHitStopFrames / 60f);
            Sfx.Play(Sound.Pop, 0.6f, 0.9f);
            fx.Sparkle(at, Palette.Cream);
        }

        /// <summary>A shot or harpoon clinked off armour or a closed core: a steel spark, a short high sound, a tiny freeze.</summary>
        void BossClink(Vector2 at)
        {
            freeze = Mathf.Max(freeze, T.bossClinkStopFrames / 60f);
            Sfx.Play(Sound.Bounce, 2.2f, 0.5f);
            fx.Sparkle(at, Palette.SteelLight);
        }

        /// <summary>
        /// A phase ended and the boss is about to stagger: every boss bullet clears, the phase makes its arena change,
        /// a weapon crate falls, and on the calm road a heart does too when Wick is below his maximum.
        /// </summary>
        void BossPhaseBreak(int phaseIndex)
        {
            tells?.Clear();
            var result = BossFight.PhaseBreak(bullets, b => Destroy(b.gameObject), run.IsHard, wick.Hearts, run.MaxHearts);
            boss.Phases[phaseIndex].OnBreak(boss);
            if (arena != null)
            {
                arena.OpenHatchOf(boss.Phases[phaseIndex]);
                RefreshHatches();
            }
            var drop = new Vector2(Mathf.Clamp(boss.Centre.x, -T.HalfWidth + 2f, T.HalfWidth - 2f), T.arenaHeight - 2f);
            if (result.dropCrate && dropper != null)
            {
                var spec = dropper.RollGuaranteed();
                if (spec != null) SpawnCrate(spec, drop);
            }
            if (result.dropHeart) pickups.Add(Pickup.Create(world, drop + Vector2.right * 2f));
            hitBubbles.Clear();
            shake = Mathf.Max(shake, T.shakeTime);
            Sfx.Play(Sound.Hurt, 0.5f, 0.8f);
        }

        /// <summary>Bullets vanish, leftover bubbles pop without children, the boss shakes and sheds debris.</summary>
        void BeginBossDefeat()
        {
            BossFight.ClearBullets(bullets, b => Destroy(b.gameObject));
            tells?.Clear();
            HideHarpoons();

            // A bubble still swelling in the hit-stop is in the bubble list too, so it pops without children as well.
            BossFight.PopRemaining(bubbles, b => Pop(b, false));
            hitBubbles.Clear();

            run.AddScore(BossFight.ScorePerWorld * run.World);
            boss.BeginDefeat();
            bossDebrisClock = 0f;
            Sfx.Play(Sound.Hurt, 0.5f, 0.8f);
            shake = Mathf.Max(shake, T.shakeTime);
        }

        float bossDebrisClock;

        void TickBossDefeat(float dt)
        {
            boss.Tick(dt, WickCentre, false);
            bossDebrisClock += dt;
            if (bossDebrisClock >= 0.1f && boss.DyingTime < T.bossDeathShake)
            {
                bossDebrisClock = 0f;
                var body = boss.Body;
                var at = new Vector2(Random.Range(body.xMin, body.xMax), Random.Range(body.yMin, body.yMax));
                fx.Debris(at, CurrentTheme.tileFill, CurrentTheme.tileCap, 3);
                Sfx.Play(Sound.Bounce, 1.6f, 0.5f);
            }
            if (!bossBurst && boss.DyingTime >= T.bossDeathShake)
            {
                bossBurst = true;
                fx.Debris(boss.Centre, CurrentTheme.tileFill, CurrentTheme.tileCap, 30);
                boss.Hide();
                shake = T.shakeTime * 4f;
                Sfx.Play(Sound.Pop, 0.5f, 1f);
            }
        }

        // ------------------------------------------------------------------ HUD

        /// <summary>
        /// The boss's health bar under the storm timer: same width, 4 px tall. The track is the timer's track colour,
        /// the fill the theme's tileCap with a 1 px cream leading edge, shrinking from the right. Name at the left end, hp at the right.
        /// </summary>
        void DrawBossBar(Vector2 topLeft, Vector2 topRight, float pad, int k)
        {
            float left = topLeft.x + pad, width = topRight.x - topLeft.x - 2 * pad;
            float barY = topLeft.y + 5 * k; // just under the timer's 2 px track
            var track = new Rect(left, barY, width, 4 * k);
            // One segment per phase with a 2 px gap; the current one drains, spent ones are empty.
            foreach (var seg in BossFight.BarSegments(boss, width, 2 * k))
            {
                float sx = Mathf.Round(left + seg.x), sw = Mathf.Round(seg.width);
                FillRect(new Rect(sx, track.y, sw, track.height), WithAlpha(Palette.Ink, UiText.TimerTrackAlpha));
                float w = Mathf.Round(sw * seg.fill);
                if (w <= 0f) continue;
                FillRect(new Rect(sx, track.y, w, track.height), CurrentTheme.tileCap);
                FillRect(new Rect(sx + Mathf.Max(0f, w - k), track.y, Mathf.Min(k, w), track.height), Palette.Cream);
            }
            DrawBossPartBars(k);
            // The name and the health sit on the row under the bar, over the sky; the timer text shares that row in the middle.
            float textY = barY + 5 * k;
            Label(boss.Name, left, textY, UiText.Primary, TextAnchor.UpperLeft, 160 * k);
            Label($"{boss.Hp}/{boss.MaxHp}", left + width - 160 * k, textY, UiText.Primary, TextAnchor.UpperRight, 160 * k);
        }

        /// <summary>A small bar over each part that has health of its own (arms, bellows, valves).</summary>
        void DrawBossPartBars(int k)
        {
            foreach (var part in boss.Parts)
            {
                if (!part.HasHealth || part.Broken) continue;
                var r = part.World(boss);
                var at = WorldToGui(new Vector2(r.center.x, r.yMax));
                float w = 16 * k;
                var track = new Rect(Mathf.Round(at.x - w * 0.5f), Mathf.Round(at.y - 5 * k), w, 2 * k);
                FillRect(track, WithAlpha(Palette.Ink, UiText.TimerTrackAlpha));
                FillRect(new Rect(track.x, track.y, Mathf.Round(w * part.Hp / part.MaxHp), track.height), CurrentTheme.tileCap);
            }
        }

        void DrawBossHitboxes()
        {
            if (boss != null)
            {
                DrawWorldRect(boss.Body, Palette.Coral);
                foreach (var part in boss.Parts) DrawWorldRect(part.World(boss), part.IsCore ? Palette.Cream : Palette.SteelLight);
            }
            foreach (var b in bullets)
                DrawWorldRect(new Rect(b.Position.x - b.Radius, b.Position.y - b.Radius, b.Radius * 2f, b.Radius * 2f), Palette.Cream);
        }

        bool DebugBossReady => boss != null && boss.Hp > 0 && !boss.Dying && boss.Entered && state == State.Play;

        void DebugBossAimed() { if (DebugBossReady) boss.FireAimed(WickCentre); }
        void DebugBossBouncy() { if (DebugBossReady) boss.FireBouncy(WickCentre); }
        void DebugBossBalloon() { if (DebugBossReady) boss.ReleaseBalloon(); }

        void DebugBossNextPhase()
        {
            if (DebugBossReady && !boss.Staggering) boss.Damage(boss.PhaseHp);
        }

        bool debugCoresOpen;
        void DebugBossOpenCores()
        {
            if (!DebugBossReady) return;
            debugCoresOpen = !debugCoresOpen;
            boss.SetCoresOpen(debugCoresOpen);
        }

        void DebugBossDamage(int amount)
        {
            if (boss == null || boss.Hp <= 0 || (state != State.Play && state != State.Intro)) return;
            boss.Damage(amount);
        }
    }
}
