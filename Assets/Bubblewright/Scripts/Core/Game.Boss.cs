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

        void ClearBoss()
        {
            foreach (var b in bullets) if (b) Destroy(b.gameObject);
            bullets.Clear();
            if (boss != null)
            {
                boss.gameObject.SetActive(false);
                Destroy(boss.gameObject);
                boss = null;
            }
            bossBurst = false;
        }

        /// <summary>Called from StartStage after ClearBoss: a boss stage gets a boss of its world, ready to slide in.</summary>
        void SetupBoss()
        {
            if (!run.IsBossStage) return;
            boss = Boss.Create(world, BossSpec.ForWorld(run.World), run.World, CurrentTheme);
            boss.OnShoot = FireBullet;
            boss.OnBouncy = FireBouncy;
            boss.OnBalloon = ReleaseBalloon;
        }

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
            TickBullets(dt);

            BossFight.HarpoonHits(boss, harpoons, DamageBoss);

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
                b.Tick(dt * scale);
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

        /// <summary>The one way anything hurts the boss: harpoons today, other weapons later.</summary>
        void DamageBoss(int amount, Vector2 at)
        {
            if (boss == null || boss.Hp <= 0) return;
            boss.Damage(amount);
            freeze = Mathf.Max(freeze, T.bossHitStopFrames / 60f);
            Sfx.Play(Sound.Pop, 0.6f, 0.9f);
            fx.Sparkle(at, Palette.Cream);
        }

        /// <summary>Bullets vanish, leftover bubbles pop without children, the boss shakes and sheds debris.</summary>
        void BeginBossDefeat()
        {
            foreach (var b in bullets) if (b) Destroy(b.gameObject);
            bullets.Clear();
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
            FillRect(track, WithAlpha(Palette.Ink, UiText.TimerTrackAlpha));
            float frac = boss.MaxHp > 0 ? Mathf.Clamp01(boss.Hp / (float)boss.MaxHp) : 0f;
            float w = Mathf.Round(width * frac);
            if (w > 0f)
            {
                FillRect(new Rect(track.x, track.y, w, track.height), CurrentTheme.tileCap);
                FillRect(new Rect(track.x + Mathf.Max(0f, w - k), track.y, Mathf.Min(k, w), track.height), Palette.Cream);
            }
            // The name and the health sit on the row under the bar, over the sky; the timer text shares that row in the middle.
            float textY = barY + 5 * k;
            Label(boss.Spec.name, left, textY, UiText.Primary, TextAnchor.UpperLeft, 160 * k);
            Label($"{boss.Hp}/{boss.MaxHp}", left + width - 160 * k, textY, UiText.Primary, TextAnchor.UpperRight, 160 * k);
        }

        void DrawBossHitboxes()
        {
            if (boss != null) DrawWorldRect(boss.Body, Palette.Coral);
            foreach (var b in bullets)
                DrawWorldRect(new Rect(b.Position.x - b.Radius, b.Position.y - b.Radius, b.Radius * 2f, b.Radius * 2f), Palette.Cream);
        }

        bool DebugBossReady => boss != null && boss.Hp > 0 && !boss.Dying && boss.Entered && state == State.Play;

        void DebugBossAimed() { if (DebugBossReady) boss.FireAimed(WickCentre); }
        void DebugBossBouncy() { if (DebugBossReady) boss.FireBouncy(WickCentre); }
        void DebugBossBalloon() { if (DebugBossReady) boss.ReleaseBalloon(); }

        void DebugBossDamage(int amount)
        {
            if (boss == null || boss.Hp <= 0 || (state != State.Play && state != State.Intro)) return;
            boss.Damage(amount);
        }
    }
}
