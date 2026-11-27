using System.Collections.Generic;
using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// Temporary weapons in play: the held weapon's behaviour and the harpoon head's, the shared WeaponWorld (hit
    /// helpers and Shots), crates that pops drop, the weapon HUD and banner, and the debug give and crate keys.
    /// The weapons themselves live in Scripts/Weapons/, one file each.
    /// </summary>
    public partial class Game
    {
        WeaponWorld weaponWorld;
        Transform weaponRoot;
        readonly WeaponContext weaponContext = new WeaponContext();
        WeaponBehaviour weaponBehaviour, headBehaviour;
        WeaponSpec weaponBehaviourSpec, headBehaviourSpec;
        readonly List<WeaponCrate> crates = new List<WeaponCrate>();
        WeaponDropper dropper;
        System.Random weaponRng;
        string weaponBanner;
        float weaponBannerTime;
        int debugGiveIndex, debugCrateIndex;

        /// <summary>Built on first use, so it survives a script reload too.</summary>
        WeaponWorld Weapons
        {
            get
            {
                if (weaponWorld != null) return weaponWorld;
                if (weaponRoot == null)
                {
                    weaponRoot = new GameObject("Weapons").transform;
                    weaponRoot.SetParent(world, false);
                }
                weaponWorld = new WeaponWorld(bubbles, rollers, bullets, () => boss, weaponRoot)
                {
                    OnHitBubble = Hit,
                    OnHitRoller = HitRoller,
                    OnHitTile = HitTile,
                    OnSteel = at =>
                    {
                        fx.Sparkle(at, Palette.SteelLight);
                        Sfx.Play(Sound.Bounce, 1.8f, 0.5f);
                    },
                };
                return weaponWorld;
            }
        }

        /// <summary>Called from StartStage: no shots or crates carry over; the held weapons do, with their ammo.</summary>
        void ResetWeaponsForStage()
        {
            Weapons.ClearShots();
            ClearGas();
            ClearCrates();
            ClearMines();
            ClearLampPosts();
            DisposeWeaponBehaviours();
            dropper = new WeaponDropper(run.Seed, run.Stage);
            weaponRng = run.StageRngFor(run.Stage, 0x3EA);
            weaponBanner = null;
            weaponBannerTime = 0f;
            SyncHeldWeapons();
        }

        void DisposeWeaponBehaviours()
        {
            weaponBehaviour?.Dispose();
            headBehaviour?.Dispose();
            weaponBehaviour = headBehaviour = null;
            weaponBehaviourSpec = headBehaviourSpec = null;
        }

        /// <summary>A slot that changed gets a fresh behaviour, and Wick's hand shows the weapon slot's weapon.</summary>
        void SyncHeldWeapons()
        {
            if (run == null) return;
            if (weaponBehaviourSpec != run.Weapon)
            {
                weaponBehaviour?.Dispose();
                weaponBehaviourSpec = run.Weapon;
                weaponBehaviour = run.Weapon?.CreateBehaviour();
            }
            if (headBehaviourSpec != run.Head)
            {
                headBehaviour?.Dispose();
                headBehaviourSpec = run.Head;
                headBehaviour = run.Head?.CreateBehaviour();
            }
            // A gun in hand wins; otherwise a harpoon head may have its own in-hand art (Skewer Harpoon).
            var inHand = run.Weapon?.HeldRows != null ? run.Weapon : run.Head;
            wick.SetHeld(WeaponArt.Held(inHand), WeaponArt.Held(inHand, true));
            touchView.ShowWeapon(run.Weapon, run.WeaponAmmo, weaponBehaviour != null && weaponBehaviour.Dimmed, weaponBehaviour?.Gauge ?? -1f);
        }

        void FillWeaponContext(WeaponSpec spec, WeaponSlot slot, bool pressed, bool held)
        {
            var c = weaponContext;
            c.ShootDown = wick.Mode == Mode.Fall && input.MoveY < 0f;
            c.InAir = wick.Mode == Mode.Fall;
            c.Muzzle = c.ShootDown ? wick.DownShotStart : wick.LauncherTip;
            c.Up = c.ShootDown ? Vector2.down : Vector2.up;
            c.CanFire = Map == null || !Map.IsSolid(StageMap.Col(c.Muzzle.x), StageMap.Row(c.Muzzle.y));
            c.Pressed = pressed;
            c.Held = held;
            c.Spec = spec;
            c.World = Weapons;
            c.Wick = wick;
            c.Run = run;
            c.Rng = weaponRng;
            c.spend = n => run.SpendAmmo(slot, n);
            c.flash = WeaponFlash;
            c.harpoons = HeadHarpoons;
        }

        /// <summary>The harpoon list, grown to count. New ones get the same upgrades as the first.</summary>
        IReadOnlyList<Harpoon> HeadHarpoons(int count)
        {
            while (harpoons.Count < count)
            {
                var h = Harpoon.Create(world, HitTile);
                if (harpoons.Count > 0)
                {
                    var first = harpoons[0];
                    h.BaseSpeed = first.BaseSpeed;
                    h.SpeedMultiplier = first.SpeedMultiplier;
                    h.WidthMultiplier = first.WidthMultiplier;
                    h.HoldTime = first.HoldTime;
                }
                harpoons.Add(h);
            }
            return harpoons;
        }

        /// <summary>
        /// Called from the harpoon firing block: may fire fire now? Without a head, when a normal harpoon is free.
        /// A held head decides for itself (the Double Harpoon waits until its cables are gone).
        /// </summary>
        bool HarpoonReady(bool harpoonFree)
        {
            SyncHeldWeapons();
            if (headBehaviour == null || run.Head == null) return harpoonFree;
            FillWeaponContext(run.Head, WeaponSlot.Harpoon, input.FirePressed, input.FireHeld);
            weaponContext.HarpoonFree = harpoonFree;
            return headBehaviour.VolleyReady(weaponContext);
        }

        void WeaponFlash()
        {
            wick.OnFire();
            fx.MuzzleFlash(weaponContext.Muzzle);
            Sfx.Play(Sound.Fire, 1.35f, 0.9f);
        }

        /// <summary>
        /// Called from the harpoon firing block when fire is pressed and a harpoon is free. A held harpoon head fires its
        /// own volley instead and spends one head ammo. False lets the normal harpoon fire.
        /// </summary>
        bool FireHeadVolley()
        {
            SyncHeldWeapons();
            if (headBehaviour == null || run.Head == null) return false;
            bool harpoonFree = weaponContext.HarpoonFree;
            FillWeaponContext(run.Head, WeaponSlot.Harpoon, true, input.FireHeld);
            weaponContext.HarpoonFree = harpoonFree;
            if (!weaponContext.CanFire || !headBehaviour.FireVolley(weaponContext)) return false;
            run.SpendAmmo(WeaponSlot.Harpoon);
            return true;
        }

        /// <summary>Called from TickPlay right after the harpoon firing block.</summary>
        void TickWeapons(float dt)
        {
            SyncHeldWeapons();
            if (weaponBehaviour != null)
            {
                FillWeaponContext(run.Weapon, WeaponSlot.Weapon, input.WeaponPressed, input.WeaponHeld);
                weaponBehaviour.Tick(weaponContext, dt);
            }
            if (headBehaviour != null)
            {
                FillWeaponContext(run.Head, WeaponSlot.Harpoon, false, false);
                headBehaviour.Tick(weaponContext, dt);
            }
            SyncHeldWeapons(); // the last shot may have emptied the slot
            Weapons.TickShots(dt);
            TickGas(dt);
            TickMines(dt);
            TickLampPosts(dt);
            TickCrates(dt);
            if (weaponBannerTime > 0f) weaponBannerTime -= dt;
        }

        /// <summary>The stage was cleared: shots vanish, crates stay put until the next stage clears them.</summary>
        void ClearWeaponShots()
        {
            Weapons.ClearShots();
            ClearGas();
            PopMinesHarmlessly();
            PutOutLampPosts();
            FizzleRocket();
        }

        // ------------------------------------------------------------------ crates

        /// <summary>A pop or a broken block may drop a crate where it happened (bubbles a boss releases too).</summary>
        void MaybeDropCrate(Vector2 at, float chance)
        {
            if (run == null || dropper == null || state != State.Play || (boss != null && boss.Dying)) return;
            var spec = dropper.Roll(chance, crates.Count > 0);
            if (spec != null) SpawnCrate(spec, at);
        }

        void SpawnCrate(WeaponSpec spec, Vector2 at)
        {
            at.y = Mathf.Clamp(at.y, 0f, T.arenaHeight - WeaponCrate.SizePixels * T.Pixel);
            crates.Add(WeaponCrate.Create(weaponRoot != null ? weaponRoot : world, at, spec));
        }

        void TickCrates(float dt)
        {
            for (int i = crates.Count - 1; i >= 0; i--)
            {
                var crate = crates[i];
                bool touching = crate.Tick(dt, Map, wick.Dead ? new Rect(1000f, 1000f, 0f, 0f) : wick.Hitbox);
                bool taken = touching && run.GiveWeapon(crate.Spec);
                if (taken)
                {
                    weaponBanner = crate.Spec.Name.ToUpperInvariant();
                    weaponBannerTime = T.weaponBannerTime;
                    fx.Sparkle(crate.Position + new Vector2(0f, 0.5f), Palette.Mint);
                    Sfx.Play(Sound.Clear, 1.9f, 0.35f);
                    SyncHeldWeapons();
                }
                if (taken || !crate.Active)
                {
                    Destroy(crate.gameObject);
                    crates.RemoveAt(i);
                }
            }
        }

        void ClearCrates()
        {
            foreach (var c in crates) if (c) Destroy(c.gameObject);
            crates.Clear();
        }

        // ------------------------------------------------------------------ HUD

        /// <summary>
        /// Right of the hearts: the weapon icon and "x ammo", then the head icon and its count. Returns the width used,
        /// so the stage label moves right of it.
        /// </summary>
        float DrawWeaponHud(float x, float y, int k)
        {
            if (run == null) return 0f;
            float start = x;
            x = DrawSlotHud(run.Weapon, run.WeaponAmmo, x, y, k);
            x = DrawSlotHud(run.Head, run.HeadAmmo, x, y, k);
            return x - start;
        }

        float DrawSlotHud(WeaponSpec spec, int ammo, float x, float y, int k)
        {
            if (spec == null) return x;
            var tex = spec.IconTexture;
            if (tex == null) return x;
            x += 4 * k;
            GUI.DrawTexture(new Rect(x, y, tex.width * k, tex.height * k), tex);
            x += tex.width * k + 2 * k;
            string text = $"x{ammo}";
            Label(text, x, y + Mathf.Max(0, tex.height - 9) * 0.5f * k, UiText.Primary, TextAnchor.UpperLeft, 40 * k);
            return x + textStyle.CalcSize(new GUIContent(text)).x;
        }

        /// <summary>The weapon's name, small, for a second after its crate is taken.</summary>
        void DrawWeaponBanner(int k)
        {
            if (weaponBannerTime <= 0f || string.IsNullOrEmpty(weaponBanner) || state != State.Play) return;
            Plate(Screen.height * 0.22f - 4 * k, 18 * k, k);
            Centered(weaponBanner, Screen.height * 0.22f, 10 * k, UiText.Accent);
        }

        // ------------------------------------------------------------------ debug

        string DebugGiveLabel()
        {
            var all = WeaponSpec.All;
            return all.Count == 0 ? "GIVE WEAPON" : $"GIVE WEAPON: {all[debugGiveIndex % all.Count].Name.ToUpperInvariant()}";
        }

        /// <summary>GIVE WEAPON: gives the next weapon of WeaponSpec.All (the label shows which).</summary>
        void DebugGiveWeapon()
        {
            var all = WeaponSpec.All;
            if (run == null || all.Count == 0) return;
            var spec = all[debugGiveIndex % all.Count];
            debugGiveIndex = (debugGiveIndex + 1) % all.Count;
            if (run.GiveWeapon(spec))
            {
                weaponBanner = spec.Name.ToUpperInvariant();
                weaponBannerTime = T.weaponBannerTime;
            }
            SyncHeldWeapons();
        }

        /// <summary>Key 9: a crate of the next weapon in WeaponSpec.All drops from above Wick.</summary>
        void DebugDropCrate()
        {
            var all = WeaponSpec.All;
            if (run == null || all.Count == 0 || (state != State.Play && state != State.Intro)) return;
            var spec = all[debugCrateIndex % all.Count];
            debugCrateIndex = (debugCrateIndex + 1) % all.Count;
            SpawnCrate(spec, new Vector2(wick.transform.position.x, wick.transform.position.y + 4f));
        }
    }
}
