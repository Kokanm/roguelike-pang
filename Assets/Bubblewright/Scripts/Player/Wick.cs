using UnityEngine;

namespace Bubblewright
{
    public enum Mode { Ground, Fall, Climb }

    /// <summary>
    /// The lamplighter. Stands on floors and platforms, falls off edges, climbs ladders and hops. Pivot at his feet.
    /// A jump is Mode.Fall with an upward speed and its own gravity (Tuning > Jump); it cannot reach the next platform.
    /// Rig: Wick (position) > Rig (squash, lean, hop) > Back / Front groups (body, launcher, flame; the front adds a free hand).
    /// He plays with his back to the camera and faces it only for the stage intro and the celebration.
    /// All animation is procedural, with timings from the sheet's animation table (Tuning > Wick animation).
    /// </summary>
    public class Wick : MonoBehaviour
    {
        const float HitboxBottomInset = 0.02f;
        const float FootSupportTolerance = 0.01f;
        const float ClimbCenterSpeed = 16f;
        const float ClimbBobSeconds = 0.12f;

        public Mode Mode { get; private set; }
        public float FeetY => transform.position.y;
        public float VelocityX { get; private set; }
        public int Hearts { get; private set; }
        public bool FacingFront { get; private set; }
        public bool Invulnerable => invulnerable > 0f;
        public bool Dead => Hearts <= 0;
        /// <summary>True from take-off until he lands. Mode is Fall the whole time.</summary>
        public bool Jumping { get; private set; }
        /// <summary>Upward speed in units per second (negative while falling).</summary>
        public float VelocityY => -fallSpeed;
        /// <summary>Off in debug god mode: a gap still sends him back to the edge, but costs no heart.</summary>
        public bool GapDamageEnabled { get; set; } = true;
        /// <summary>Set by the game during the stage intro: he waves his free hand while facing the camera.</summary>
        public bool Waving { get; set; }
        /// <summary>Run upgrades: multiplies top speed, and adds seconds of safety after a hit.</summary>
        public float SpeedMultiplier { get; set; } = 1f;
        public float ExtraInvulnerable { get; set; }
        /// <summary>Hard-world debuff: multiplies the whole safe time after a hit.</summary>
        public float InvulnerableScale { get; set; } = 1f;

        public bool NearLadder
        {
            get
            {
                if (Mode == Mode.Climb) return true;
                var map = Game.Map;
                if (map == null) return false;
                var p = transform.position;
                return TryFindLadder(map, p.x, p.y, 1f, Mode, out _, out _) ||
                       TryFindLadder(map, p.x, p.y, -1f, Mode, out _, out _);
            }
        }

        float? baseRunSpeed, baseInvulnerable;
        /// <summary>The character's own run speed; Wick's follows Tuning until a character sets it.</summary>
        public float BaseRunSpeed { get => baseRunSpeed ?? T.runSpeed; set => baseRunSpeed = value; }
        /// <summary>The character's own safe time after a hit, before upgrades and debuffs.</summary>
        public float BaseInvulnerable { get => baseInvulnerable ?? T.invulnerableTime; set => baseInvulnerable = value; }
        public Character Character { get; private set; } = global::Bubblewright.Character.Wick;

        float RunSpeed => BaseRunSpeed * SpeedMultiplier;

        Tuning T => Tuning.I;
        float Px => 1f / T.pixelsPerUnit;
        Transform rig, hand;
        Effects fx;
        GameObject backSet, frontSet;
        SpriteRenderer bodyBack, bodyFront, flameBack, flameFront, launcherBack, launcherFront, handFront;
        Vector3 launcherBackHome, launcherFrontHome;

        float fallSpeed;
        int ladderCol, ladderBottomRow, ladderTopRow, ladderMapVersion;
        bool climbMoving;
        float climbClock;
        float landSquash = -1f;

        float coyoteClock, jumpBufferClock, airTopSpeed, groundTopSpeed;
        float jumpRise = -1f, jumpLand = -1f;
        Vector3 safeSpot;
        float safeStandClock;
        bool gapFallPending;

        float invulnerable;
        float knockTimer, knockVelocity;
        float turnTimer = -1f;
        bool turnToFront, swapped;
        float fireKick = -1f;
        float runStart = -1f, skid = -1f;
        int runDir, skidDir, lastWantDir;
        float hurtFlash = -1f;
        float stepClock;
        float sandDustClock;
        float celebrateTimer = -1f;
        int celebrateLandings;
        float flameClock, idleClock;
        float lean;

        public static Wick Create(Transform parent, Effects fx)
        {
            var go = new GameObject("Wick");
            go.transform.SetParent(parent, false);
            var w = go.AddComponent<Wick>();
            w.fx = fx;
            w.Build();
            return w;
        }

        void Build()
        {
            var art = PlaceholderArt.I;
            var sprites = art.SpritesFor(Character);
            rig = new GameObject("Rig").transform;
            rig.SetParent(transform, false);

            backSet = new GameObject("Back");
            backSet.transform.SetParent(rig, false);
            frontSet = new GameObject("Front");
            frontSet.transform.SetParent(rig, false);

            var flamePos = new Vector2(0f, 27f * Px);
            bodyBack = MakePart(backSet.transform, "Body", sprites.Back, Vector2.zero, 0);
            launcherBack = MakePart(backSet.transform, "Launcher", sprites.Launcher, PlaceholderArt.LauncherOffsetPixels(false) * Px, 2);
            flameBack = MakePart(backSet.transform, "Flame", art.Flame, flamePos, 1);
            bodyFront = MakePart(frontSet.transform, "Body", sprites.Front, Vector2.zero, 0);
            launcherFront = MakePart(frontSet.transform, "Launcher", sprites.Launcher, PlaceholderArt.LauncherOffsetPixels(true) * Px, 2);
            flameFront = MakePart(frontSet.transform, "Flame", art.Flame, flamePos, 1);
            handFront = MakePart(frontSet.transform, "Hand", sprites.Hand, Vector2.zero, 2);
            hand = handFront.transform;
            launcherBackHome = launcherBack.transform.localPosition;
            launcherFrontHome = launcherFront.transform.localPosition;

            ResetForStage(0f, 0f, T.hearts);
        }

        SpriteRenderer MakePart(Transform parent, string name, Sprite sprite, Vector2 pos, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            Sorting.Apply(sr, Layer.Player, order);
            Game.ApplySpriteMaterial(sr);
            return sr;
        }

        public void ResetForStage(float x, int hearts) => ResetForStage(x, 0f, hearts);

        /// <summary>Hearts carry over between stages, so the run passes them in.</summary>
        public void ResetForStage(float x, float y, int hearts)
        {
            transform.position = new Vector3(x, y, 0f);
            Mode = Mode.Ground;
            VelocityX = 0f;
            fallSpeed = 0f;
            climbMoving = false;
            climbClock = 0f;
            Jumping = false;
            coyoteClock = jumpBufferClock = 0f;
            jumpRise = jumpLand = -1f;
            airTopSpeed = groundTopSpeed = RunSpeed;
            safeSpot = transform.position;
            safeStandClock = 0f;
            gapFallPending = false;
            Hearts = hearts;
            invulnerable = 0f;
            knockTimer = 0f;
            turnTimer = -1f;
            celebrateTimer = -1f;
            fireKick = runStart = skid = landSquash = hurtFlash = -1f;
            lastWantDir = 0;
            lean = 0f;
            Waving = false;
            SetFacing(true);
            rig.localScale = Vector3.one;
            rig.localRotation = Quaternion.identity;
            flameBack.transform.localScale = flameFront.transform.localScale = Vector3.one;
            ApplyFlash(false);
            SetVisible(true);
        }

        void SetFacing(bool front)
        {
            FacingFront = front;
            frontSet.SetActive(front);
            backSet.SetActive(!front);
        }

        void SetVisible(bool v)
        {
            bodyBack.enabled = bodyFront.enabled = launcherBack.enabled = launcherFront.enabled = v;
            flameBack.enabled = flameFront.enabled = v && !Dead;
        }

        void ApplyFlash(bool on)
        {
            var sprites = PlaceholderArt.I.SpritesFor(Character);
            bodyBack.sprite = on ? sprites.BackFlash : sprites.Back;
            bodyFront.sprite = on ? sprites.FrontFlash : sprites.Front;
            launcherBack.sprite = launcherFront.sprite = on ? sprites.LauncherFlash : sprites.Launcher;
        }

        /// <summary>Swaps the cloak colours. Starting numbers (speed, safe time) are set by the game from the run.</summary>
        public void SetCharacter(Character c)
        {
            Character = c ?? global::Bubblewright.Character.Wick;
            var sprites = PlaceholderArt.I.SpritesFor(Character);
            handFront.sprite = sprites.Hand;
            ApplyFlash(hurtFlash >= 0f);
        }

        /// <summary>Squash to 10% width, swap front/back at the thinnest point behind a dust puff, spring back.</summary>
        public void Turn(bool toFront)
        {
            if (FacingFront == toFront && turnTimer < 0f) return;
            turnToFront = toFront;
            turnTimer = 0f;
            swapped = false;
            Sfx.Play(Sound.Turn);
        }

        public Vector2 LauncherTip => (Vector2)transform.position + T.launcherTip;

        public Rect Hitbox
        {
            get
            {
                var p = transform.position;
                return new Rect(p.x - T.hitbox.x * 0.5f, p.y, T.hitbox.x, T.hitbox.y);
            }
        }

        public void OnFire() => fireKick = 0f;

        /// <summary>Debug mode: set hearts directly, for heals and extra hearts while playing.</summary>
        public void SetHearts(int hearts)
        {
            Hearts = Mathf.Max(0, hearts);
            SetVisible(true);
        }

        public void Heal()
        {
            Hearts++;
            SetVisible(true);
        }

        /// <summary>Normal bubbles take one heart; during the storm they take more.</summary>
        public void Hurt(float fromX, int damage = 1)
        {
            if (Invulnerable || Dead) return;
            Hearts = Mathf.Max(0, Hearts - Mathf.Max(1, damage));
            invulnerable = (BaseInvulnerable + ExtraInvulnerable) * InvulnerableScale;
            if (Mode != Mode.Climb)
            {
                knockTimer = T.knockbackTime;
                float dir = transform.position.x >= fromX ? 1f : -1f;
                knockVelocity = dir * T.knockback / Mathf.Max(0.01f, T.knockbackTime);
            }
            VelocityX = 0f;
            runStart = skid = -1f;
            // Flash now: the game freezes right after this, so Animate will not run until the freeze ends.
            hurtFlash = T.hurtFlashTime;
            ApplyFlash(true);
            SetVisible(true);
            if (Dead)
            {
                invulnerable = 0f;
                flameBack.enabled = flameFront.enabled = false;
            }
        }

        public void Celebrate()
        {
            celebrateTimer = 0f;
            celebrateLandings = 0;
        }

        public void Tick(float move, float dt, bool controllable) => Tick(move, 0f, false, dt, controllable);

        public void Tick(float move, float climb, float dt, bool controllable) => Tick(move, climb, false, dt, controllable);

        /// <summary>
        /// True once after he fell into a gap and came back at the edge and lost a heart this way.
        /// The game uses it for the hurt freeze, sound and game over.
        /// </summary>
        public bool ConsumeGapFall()
        {
            bool fell = gapFallPending;
            gapFallPending = false;
            return fell;
        }

        /// <summary>Physics and procedural animation. move and climb are -1..1; jump is "pressed this frame". dt is game time.</summary>
        public void Tick(float move, float climb, bool jump, float dt, bool controllable)
        {
            var map = Game.Map ?? StageMap.Flat();
            float wantMove = controllable && !Dead ? Mathf.Clamp(move, -1f, 1f) : 0f;
            float wantClimb = controllable && !Dead ? Mathf.Clamp(climb, -1f, 1f) : 0f;
            var pos = transform.position;
            climbMoving = false;

            // A press is remembered for jumpBuffer seconds, so pressing just before landing still jumps.
            jumpBufferClock = Mathf.Max(0f, jumpBufferClock - dt);
            if (jump && controllable && !Dead) jumpBufferClock = Mathf.Max(T.jumpBuffer, 1e-4f);
            coyoteClock = Mathf.Max(0f, coyoteClock - dt);

            // No ladder grabs in the middle of a jump.
            if (Mode != Mode.Climb && !Jumping && knockTimer <= 0f && wantClimb != 0f &&
                TryFindLadder(map, pos.x, pos.y, wantClimb, Mode, out int grabCol, out int grabRow))
            {
                StartClimb(map, grabCol, grabRow);
            }

            if (Mode == Mode.Climb)
            {
                RefreshClimbSpan(map, pos.y);
                if (Mode == Mode.Climb && wantClimb == 0f && wantMove != 0f)
                {
                    Mode = Mode.Fall;
                    fallSpeed = 0f;
                    coyoteClock = 0f; // stepping off a ladder is not walking off an edge
                }
            }

            if (Mode == Mode.Climb)
            {
                TickClimb(map, wantClimb, dt, ref pos);
                transform.position = pos;
            }
            else
            {
                float prevX = pos.x;
                if (knockTimer > 0f)
                {
                    knockTimer -= dt;
                    pos.x += knockVelocity * dt;
                    lastWantDir = 0;
                }
                else
                {
                    // The floor under his feet sets how he speeds up and slows down (only while on the ground).
                    Surface surface = Mode == Mode.Ground ? map.SurfaceUnder(pos.x, FeetY) : Surface.Normal;
                    float topSpeed = surface == Surface.Sand ? RunSpeed * T.sandSpeedScale : RunSpeed;
                    if (Mode == Mode.Ground) groundTopSpeed = topSpeed;
                    else if (Jumping) topSpeed = airTopSpeed; // a jump keeps the top speed of the floor it left (shorter on sand)
                    float accelTime = surface == Surface.Ice ? T.iceAccelTime : surface == Surface.Sand ? T.sandAccelTime : T.accelTime;
                    float stopTime = surface == Surface.Ice ? T.iceStopTime : T.stopTime;
                    float target = wantMove * topSpeed;
                    transform.position = pos;
                    if (Mode == Mode.Ground) DetectRunEdges(target, surface != Surface.Ice);
                    else lastWantDir = target > 0f ? 1 : target < 0f ? -1 : 0;
                    bool stopping = target == 0f || (VelocityX != 0f && Mathf.Sign(target) != Mathf.Sign(VelocityX));
                    float rate = topSpeed / Mathf.Max(0.001f, stopping ? stopTime : accelTime);
                    VelocityX = Mathf.MoveTowards(VelocityX, target, rate * dt);
                    pos.x += VelocityX * dt;
                    TickSandDust(surface, dt);
                }

                ResolveHorizontal(map, prevX, ref pos);
                KeepInsideGap(map, ref pos);

                bool walkedOff = false;
                if (Mode == Mode.Ground)
                {
                    if (!IsSupported(map, pos.x, pos.y))
                    {
                        walkedOff = true;
                        Mode = Mode.Fall;
                        fallSpeed = 0f;
                        coyoteClock = T.coyoteTime; // walked off an edge: a jump still works for a moment
                    }
                    transform.position = pos;
                }

                // Jump from the ground, or just after walking off an edge. Never from a ladder or during knockback.
                if (controllable && !Dead && jumpBufferClock > 0f && knockTimer <= 0f &&
                    (Mode == Mode.Ground || Mode == Mode.Fall && !Jumping && coyoteClock > 0f))
                {
                    StartJump();
                    walkedOff = false;
                }

                // Walking off an edge starts falling next frame, as before; a jump moves this frame.
                if (Mode == Mode.Fall && !walkedOff)
                {
                    float previousFeetY = pos.y;
                    float gravity = Jumping ? T.JumpGravity : T.fallGravity;
                    float newSpeed = Mathf.Min(T.maxFallSpeed, fallSpeed + gravity * dt);
                    // A jump integrates exactly (average speed over the frame) so its height does not depend on the frame rate.
                    pos.y -= (Jumping ? (fallSpeed + newSpeed) * 0.5f : newSpeed) * dt;
                    bool wasRising = fallSpeed < 0f;
                    fallSpeed = newSpeed;
                    if (wasRising && fallSpeed >= 0f) jumpRise = -1f; // the top: back to 1 x 1

                    // Head bump: the top of his hitbox stops under any solid tile.
                    if (pos.y > previousFeetY)
                    {
                        float ceiling = HeadCeiling(map, pos.x, previousFeetY);
                        if (pos.y + T.hitbox.y >= ceiling)
                        {
                            pos.y = Mathf.Max(previousFeetY, ceiling - T.hitbox.y);
                            fallSpeed = 0f;
                            jumpRise = -1f;
                        }
                    }

                    float halfFeet = T.feetWidth * 0.5f;
                    // Once below the floor line he is inside a gap: nothing to land on until he comes back at the edge.
                    float floor = previousFeetY < -1e-4f ? float.NegativeInfinity : Mathf.Max(
                        map.FloorBelow(pos.x - halfFeet, previousFeetY),
                        map.FloorBelow(pos.x + halfFeet, previousFeetY));
                    if (fallSpeed >= 0f && pos.y <= floor)
                    {
                        pos.y = floor;
                        fallSpeed = 0f;
                        Mode = Mode.Ground;
                        coyoteClock = 0f;
                        if (Jumping)
                        {
                            Jumping = false;
                            jumpRise = -1f;
                            jumpLand = 0f;
                            landSquash = -1f;
                        }
                        else landSquash = 0f;
                        transform.position = pos;
                        if (fx != null)
                        {
                            fx.Dust(Feet(-4), -1f);
                            fx.Dust(Feet(4), 1f);
                        }
                        // A buffered press jumps again straight away.
                        if (controllable && !Dead && jumpBufferClock > 0f && knockTimer <= 0f) StartJump();
                    }
                    else if (pos.y < -T.gapFallDepth)
                    {
                        FallIntoGap(map, ref pos);
                    }
                    else
                    {
                        transform.position = pos;
                    }
                }
            }

            TrackSafeSpot(map, dt);

            if (invulnerable > 0f) invulnerable -= dt;
            Animate(dt);
        }

        void StartJump()
        {
            Mode = Mode.Fall;
            Jumping = true;
            fallSpeed = -T.JumpSpeed;
            airTopSpeed = groundTopSpeed;
            jumpBufferClock = 0f;
            coyoteClock = 0f;
            jumpRise = 0f;
            jumpLand = landSquash = runStart = skid = -1f;
            if (fx != null) fx.Dust(Feet(0), 0f);
            Sfx.Play(Sound.Turn, 1.9f, 0.7f);
        }

        /// <summary>Lowest underside of a solid tile above either edge of his hitbox (or the arena ceiling).</summary>
        float HeadCeiling(StageMap map, float x, float feetY)
        {
            float half = T.hitbox.x * 0.5f - 0.01f;
            float top = feetY + T.hitbox.y;
            return Mathf.Min(
                map.CeilingAbove(x - half, top, out _),
                map.CeilingAbove(x + half, top, out _));
        }

        /// <summary>Below the floor line, the sides of a gap are walls: his feet stay over the gap's columns.</summary>
        void KeepInsideGap(StageMap map, ref Vector3 pos)
        {
            if (pos.y >= 0f) return;
            int c = Mathf.Clamp(StageMap.Col(pos.x), 0, StageMap.Cols - 1);
            if (!map.IsGap(c)) return;
            int left = c, right = c;
            while (left > 0 && map.IsGap(left - 1)) left--;
            while (right < StageMap.Cols - 1 && map.IsGap(right + 1)) right++;
            float halfFeet = T.feetWidth * 0.5f;
            float minX = StageMap.CellRect(left, 0).xMin + halfFeet;
            float maxX = StageMap.CellRect(right, 0).xMax - halfFeet;
            if (pos.x < minX) { pos.x = minX; VelocityX = 0f; }
            if (pos.x > maxX) { pos.x = maxX; VelocityX = 0f; }
        }

        bool StandsOnSolid(StageMap map, float x, float y)
        {
            float halfFeet = T.feetWidth * 0.5f;
            return Mathf.Abs(map.FloorBelow(x - halfFeet, y + FootSupportTolerance) - y) <= FootSupportTolerance &&
                   Mathf.Abs(map.FloorBelow(x + halfFeet, y + FootSupportTolerance) - y) <= FootSupportTolerance;
        }

        /// <summary>Remembers where he last stood with both feet on solid ground for gapSafeStandTime seconds.</summary>
        void TrackSafeSpot(StageMap map, float dt)
        {
            var p = transform.position;
            if (Mode == Mode.Ground && knockTimer <= 0f && StandsOnSolid(map, p.x, p.y))
            {
                safeStandClock += dt;
                if (safeStandClock >= T.gapSafeStandTime) safeSpot = p;
            }
            else safeStandClock = 0f;
        }

        /// <summary>
        /// Fell below the floor through a gap: lose gapDamage hearts (ignoring the safe time) and come back standing at the
        /// last safe spot, moved further from the gap, with the normal safe time and blink.
        /// </summary>
        void FallIntoGap(StageMap map, ref Vector3 pos)
        {
            float fallX = pos.x;
            Vector3 p = safeSpot;
            float dir = p.x >= fallX ? 1f : -1f;
            float wallX = T.HalfWidth - 11.5f * Px;
            Vector3 candidate = p;
            candidate.x = Mathf.Clamp(p.x + dir * T.gapRespawnInset, -wallX, wallX);
            for (int i = 0; i < 40 && !StandsOnSolid(map, candidate.x, candidate.y); i++)
                candidate.x = Mathf.Clamp(candidate.x + dir * 0.1f, -wallX, wallX);
            if (StandsOnSolid(map, candidate.x, candidate.y)) p = candidate;

            pos = p;
            transform.position = pos;
            Mode = Mode.Ground;
            Jumping = false;
            fallSpeed = VelocityX = 0f;
            knockTimer = 0f;
            coyoteClock = jumpBufferClock = 0f;
            jumpRise = jumpLand = landSquash = runStart = skid = -1f;
            lastWantDir = 0;
            safeStandClock = 0f;
            if (fx != null) fx.Dust(Feet(0), 0f);

            if (!GapDamageEnabled || Dead) return;
            Hearts = Mathf.Max(0, Hearts - Mathf.Max(1, T.gapDamage));
            invulnerable = Dead ? 0f : (BaseInvulnerable + ExtraInvulnerable) * InvulnerableScale;
            hurtFlash = T.hurtFlashTime;
            ApplyFlash(true);
            SetVisible(true);
            if (Dead) flameBack.enabled = flameFront.enabled = false;
            gapFallPending = true;
        }

        Rect BodyBox(float x, float y) =>
            new Rect(x - T.hitbox.x * 0.5f, y + HitboxBottomInset, T.hitbox.x, Mathf.Max(0.01f, T.hitbox.y - HitboxBottomInset));

        bool IsSupported(StageMap map, float x, float y)
        {
            float halfFeet = T.feetWidth * 0.5f;
            float leftFloor = map.FloorBelow(x - halfFeet, y + FootSupportTolerance);
            float rightFloor = map.FloorBelow(x + halfFeet, y + FootSupportTolerance);
            return Mathf.Abs(leftFloor - y) <= FootSupportTolerance || Mathf.Abs(rightFloor - y) <= FootSupportTolerance;
        }

        void ResolveHorizontal(StageMap map, float prevX, ref Vector3 pos)
        {
            float half = T.HalfWidth - 11.5f * Px; // keep the drawn body inside the walls
            if (pos.x < -half) { pos.x = -half; VelocityX = 0f; knockTimer = 0f; }
            if (pos.x > half) { pos.x = half; VelocityX = 0f; knockTimer = 0f; }

            float halfBox = T.hitbox.x * 0.5f;
            var box = BodyBox(pos.x, pos.y);
            if (!map.OverlapsSolid(box)) return;

            int cMin = Mathf.Max(0, StageMap.Col(box.xMin));
            int cMax = Mathf.Min(StageMap.Cols - 1, StageMap.Col(box.xMax));
            int rMin = Mathf.Max(0, StageMap.Row(box.yMin));
            int rMax = Mathf.Min(StageMap.Rows - 1, StageMap.Row(box.yMax));
            for (int c = cMin; c <= cMax; c++)
            {
                for (int r = rMin; r <= rMax; r++)
                {
                    if (!map.IsSolid(c, r)) continue;
                    Rect rect = StageMap.CellRect(c, r);
                    if (!box.Overlaps(rect)) continue;
                    if (Mode == Mode.Fall && map.Get(c, r).ladder && prevX > rect.xMin - halfBox && prevX < rect.xMax + halfBox)
                        continue;

                    if (prevX <= rect.center.x && !map.IsSolid(c - 1, r))
                    {
                        pos.x = Mathf.Min(pos.x, rect.xMin - halfBox);
                        VelocityX = 0f;
                        knockTimer = 0f;
                    }
                    else if (prevX >= rect.center.x && !map.IsSolid(c + 1, r))
                    {
                        pos.x = Mathf.Max(pos.x, rect.xMax + halfBox);
                        VelocityX = 0f;
                        knockTimer = 0f;
                    }
                }
            }

            if (pos.x < -half) { pos.x = -half; VelocityX = 0f; knockTimer = 0f; }
            if (pos.x > half) { pos.x = half; VelocityX = 0f; knockTimer = 0f; }
        }

        bool TryFindLadder(StageMap map, float x, float y, float climbDir, Mode currentMode, out int foundCol, out int foundRow)
        {
            foundCol = -1;
            foundRow = -1;
            float bestDist = float.PositiveInfinity;
            int cMin = Mathf.Max(0, StageMap.Col(x - T.ladderGrabWidth));
            int cMax = Mathf.Min(StageMap.Cols - 1, StageMap.Col(x + T.ladderGrabWidth));

            for (int c = cMin; c <= cMax; c++)
            {
                float dist = Mathf.Abs(x - StageMap.CenterX(c));
                if (dist > T.ladderGrabWidth + 1e-5f || dist >= bestDist) continue;

                int r = -1;
                if (climbDir > 0f)
                {
                    int feetRow = StageMap.Row(y);
                    if (map.Get(c, feetRow).ladder) r = feetRow;
                    else if (map.Get(c, feetRow + 1).ladder) r = feetRow + 1;
                    if (r >= 0)
                    {
                        GetLadderSpan(map, c, r, out _, out int topRow);
                        float topStop = LadderTopStop(map, c, topRow);
                        if (y >= topStop - 1e-4f) r = -1;
                    }
                }
                else if (climbDir < 0f)
                {
                    int belowRow = StageMap.Row(y - 0.05f);
                    if (currentMode == Mode.Ground)
                    {
                        if (map.Get(c, belowRow).ladder) r = belowRow;
                    }
                    else
                    {
                        int feetRow = StageMap.Row(y);
                        if (map.Get(c, feetRow).ladder) r = feetRow;
                        else if (map.Get(c, belowRow).ladder) r = belowRow;
                        if (r >= 0)
                        {
                            GetLadderSpan(map, c, r, out int bottomRow, out _);
                            float bottomStop = LadderBottomStop(map, c, bottomRow, out _);
                            if (y <= bottomStop + 1e-4f) r = -1;
                        }
                    }
                }

                if (r >= 0)
                {
                    bestDist = dist;
                    foundCol = c;
                    foundRow = r;
                }
            }

            return foundCol >= 0;
        }

        static void GetLadderSpan(StageMap map, int c, int seedRow, out int bottomRow, out int topRow)
        {
            bottomRow = seedRow;
            while (bottomRow > 0 && map.Get(c, bottomRow - 1).ladder) bottomRow--;
            topRow = seedRow;
            while (topRow + 1 < StageMap.Rows && map.Get(c, topRow + 1).ladder) topRow++;
        }

        float LadderTopStop(StageMap map, int c, int topRow)
        {
            if (map.IsSolid(c, topRow)) return topRow + 1f;
            float ceil = map.CeilingAbove(StageMap.CenterX(c), topRow + 0.5f, out _);
            return Mathf.Min(topRow + 1f, ceil - T.hitbox.y);
        }

        static float LadderBottomStop(StageMap map, int c, int bottomRow, out bool hasSurface)
        {
            float surface = map.FloorBelow(StageMap.CenterX(c), bottomRow + FootSupportTolerance);
            hasSurface = Mathf.Abs(surface - bottomRow) <= FootSupportTolerance;
            return hasSurface ? surface : bottomRow;
        }

        void StartClimb(StageMap map, int c, int seedRow)
        {
            Mode = Mode.Climb;
            Jumping = false;
            coyoteClock = 0f;
            ladderCol = c;
            GetLadderSpan(map, c, seedRow, out ladderBottomRow, out ladderTopRow);
            ladderMapVersion = map.Version;
            VelocityX = 0f;
            fallSpeed = 0f;
            runStart = skid = landSquash = -1f;
            lastWantDir = 0;
        }

        void RefreshClimbSpan(StageMap map, float y)
        {
            if (map.Version == ladderMapVersion) return;
            ladderMapVersion = map.Version;
            int r = Mathf.Clamp(StageMap.Row(y), ladderBottomRow, ladderTopRow);
            if (!map.Get(ladderCol, r).ladder && map.Get(ladderCol, r + 1).ladder) r++;
            if (!map.Get(ladderCol, r).ladder && map.Get(ladderCol, r - 1).ladder) r--;
            if (!map.Get(ladderCol, r).ladder)
            {
                Mode = Mode.Fall;
                fallSpeed = 0f;
                return;
            }
            GetLadderSpan(map, ladderCol, r, out ladderBottomRow, out ladderTopRow);
        }

        void TickClimb(StageMap map, float wantClimb, float dt, ref Vector3 pos)
        {
            float centerX = StageMap.CenterX(ladderCol);
            pos.x = Mathf.MoveTowards(pos.x, centerX, ClimbCenterSpeed * dt);
            VelocityX = 0f;

            float vy = wantClimb * T.climbSpeed;
            float prevY = pos.y;
            pos.y += vy * dt;

            if (wantClimb > 0f)
            {
                bool hasTopTile = map.IsSolid(ladderCol, ladderTopRow);
                float topStop = LadderTopStop(map, ladderCol, ladderTopRow);
                if (pos.y >= topStop)
                {
                    pos.y = topStop;
                    if (hasTopTile)
                    {
                        Mode = Mode.Ground;
                        climbMoving = false;
                        ResolveClimbHorizontal(map, ref pos);
                        return;
                    }
                }
            }
            else if (wantClimb < 0f)
            {
                float bottomStop = LadderBottomStop(map, ladderCol, ladderBottomRow, out bool hasSurface);
                if (pos.y <= bottomStop)
                {
                    pos.y = bottomStop;
                    if (hasSurface)
                    {
                        Mode = Mode.Ground;
                        climbMoving = false;
                        ResolveClimbHorizontal(map, ref pos);
                        return;
                    }
                }
            }

            climbMoving = Mathf.Abs(pos.y - prevY) > 1e-5f;
            ResolveClimbHorizontal(map, ref pos);
        }

        void ResolveClimbHorizontal(StageMap map, ref Vector3 pos)
        {
            float half = T.HalfWidth - 11.5f * Px;
            pos.x = Mathf.Clamp(pos.x, -half, half);

            float halfBox = T.hitbox.x * 0.5f;
            var box = BodyBox(pos.x, pos.y);
            int cMin = Mathf.Max(0, StageMap.Col(box.xMin));
            int cMax = Mathf.Min(StageMap.Cols - 1, StageMap.Col(box.xMax));
            int rMin = Mathf.Max(0, StageMap.Row(box.yMin));
            int rMax = Mathf.Min(StageMap.Rows - 1, StageMap.Row(box.yMax));
            for (int c = cMin; c <= cMax; c++)
            {
                if (c == ladderCol) continue;
                for (int r = rMin; r <= rMax; r++)
                {
                    if (!map.IsSolid(c, r)) continue;
                    Rect rect = StageMap.CellRect(c, r);
                    if (!box.Overlaps(rect)) continue;
                    if (c < ladderCol) pos.x = Mathf.Max(pos.x, rect.xMax + halfBox);
                    else pos.x = Mathf.Min(pos.x, rect.xMin - halfBox);
                }
            }
        }

        /// <summary>Starts the run-start stretch and the skid when the wanted direction changes.</summary>
        void DetectRunEdges(float target, bool canSkid = true)
        {
            int want = target > 0f ? 1 : target < 0f ? -1 : 0;
            if (want == lastWantDir) return;
            int moving = VelocityX > 0f ? 1 : VelocityX < 0f ? -1 : 0;
            if (canSkid && moving != 0 && want != moving && Mathf.Abs(VelocityX) >= T.skidMinSpeed * RunSpeed)
            {
                skid = 0f;
                skidDir = moving;
                if (fx != null) fx.Dust(Feet(moving * 5), moving);
                Sfx.Play(Sound.Skid);
            }
            if (want != 0)
            {
                runStart = 0f;
                runDir = want;
                if (fx != null)
                {
                    fx.Dust(Feet(-3), -want);
                    fx.Dust(Feet(3), -want);
                }
            }
            lastWantDir = want;
        }

        /// <summary>One dust puff at his feet every sandDustInterval seconds while he walks on sand.</summary>
        void TickSandDust(Surface surface, float dt)
        {
            if (surface != Surface.Sand || Mathf.Abs(VelocityX) < 0.1f)
            {
                sandDustClock = 0f;
                return;
            }
            sandDustClock += dt;
            if (sandDustClock < T.sandDustInterval) return;
            sandDustClock = 0f;
            if (fx != null) fx.Dust(Feet(0), -Mathf.Sign(VelocityX));
        }

        Vector2 Feet(int pixelsX) => new Vector2(transform.position.x + pixelsX * Px, FeetY);

        void Animate(float dt)
        {
            Vector3 scale = Vector3.one;
            float offsetY = 0f;
            float speed01 = RunSpeed > 0f ? VelocityX / RunSpeed : 0f;
            float leanRate = T.runLean / Mathf.Max(0.01f, T.runStartTime);

            // Run: lean into the direction, bob 1 px per step at 8 steps/s. Climb: bob 1 px every 0.12 s while moving, no lean. Idle: slow 1.6 s breathing squash.
            if (Mode == Mode.Climb)
            {
                stepClock = 0f;
                if (climbMoving)
                {
                    climbClock += dt;
                    offsetY = (Mathf.FloorToInt(climbClock / ClimbBobSeconds) % 2) * Px;
                }
                else
                {
                    climbClock = 0f;
                }
            }
            else if (Mode == Mode.Fall)
            {
                stepClock = 0f;
                climbClock = 0f;
            }
            else if (Mathf.Abs(speed01) > 0.1f)
            {
                climbClock = 0f;
                stepClock += dt * 8f;
                offsetY = (Mathf.FloorToInt(stepClock) % 2) * Px;
            }
            else
            {
                climbClock = 0f;
                stepClock = 0f;
                idleClock += dt;
                float breath = Mathf.Sin(idleClock * Mathf.PI * 2f / 1.6f);
                scale = new Vector3(1f + breath * 0.02f, 1f - breath * 0.02f, 1f);
            }

            // Run start: stretch 0.9 x 1.1 that eases back to round
            if (runStart >= 0f)
            {
                runStart += dt;
                float k = runStart / Mathf.Max(0.001f, T.runStartTime);
                if (k >= 1f) runStart = -1f;
                else scale = Vector3.Scale(scale, Squash(T.runStartStretch, EaseOutQuad(k)));
            }

            // Skid: hold a lean back for the first half, then settle with a 1.05 x 0.95 squash
            bool holdSkidLean = false;
            if (skid >= 0f)
            {
                skid += dt;
                float k = skid / Mathf.Max(0.001f, T.skidTime);
                if (k >= 1f) skid = -1f;
                else if (k < 0.5f) holdSkidLean = true;
                else scale = Vector3.Scale(scale, Squash(T.skidSettle, (k - 0.5f) * 2f));
            }
            // Jump: stretch at take-off easing to 1 x 1 at the top, then a short squash on landing.
            if (jumpRise >= 0f)
            {
                jumpRise += dt;
                float k = jumpRise / Mathf.Max(0.001f, T.jumpTimeToApex);
                if (k >= 1f) jumpRise = -1f;
                else scale = Vector3.Scale(scale, Squash(T.jumpStretch, k));
            }
            if (jumpLand >= 0f)
            {
                jumpLand += dt;
                float k = jumpLand / Mathf.Max(0.001f, T.jumpLandTime);
                if (k >= 1f) jumpLand = -1f;
                else scale = Vector3.Scale(scale, Squash(T.jumpLandSquash, k));
            }
            if (landSquash >= 0f)
            {
                landSquash += dt;
                float k = landSquash / Mathf.Max(0.001f, T.skidTime);
                if (k >= 1f) landSquash = -1f;
                else scale = Vector3.Scale(scale, Squash(T.skidSettle, k));
            }
            if (Mode == Mode.Climb) lean = 0f;
            else if (holdSkidLean) lean = skidDir * T.skidLean;
            else lean = Mathf.MoveTowards(lean, -speed01 * T.runLean, leanRate * dt);

            // Fire: launcher kicks down 2 px, body squashes 1.1 x 0.9
            float kick = 0f;
            if (fireKick >= 0f)
            {
                fireKick += dt;
                if (fireKick < T.fireTime)
                {
                    scale = Vector3.Scale(scale, new Vector3(T.fireSquash.x, T.fireSquash.y, 1f));
                    kick = -T.fireKickPixels * Px;
                }
                else fireKick = -1f;
            }

            // Free hand (front view only): waves in the intro, goes up with the launcher on the second celebration hop
            bool handUp = false;
            float launcherLift = 0f;
            Vector2 handPixels = new Vector2(19f, 15f);

            if (celebrateTimer >= 0f)
            {
                Celebrating(dt, ref scale, ref offsetY, ref handUp, ref launcherLift, ref handPixels);
                lean = 0f;
            }
            else if (Waving && FacingFront)
            {
                handUp = true;
                handPixels = new Vector2(19f + (Mathf.FloorToInt(Time.time * 6f) % 2), 21f);
                lean = Mathf.Sin(Time.time * Mathf.PI * 2f * 1.5f) * 3f; // a little rock while he waves
            }

            // Turn: squash to 10% width (and a touch taller), swap at the thinnest point, spring back with overshoot
            if (turnTimer >= 0f)
            {
                turnTimer += dt;
                float half = T.turnTime * 0.5f;
                if (turnTimer < half)
                {
                    float k = turnTimer / half;
                    scale.x *= Mathf.Lerp(1f, 0.1f, k);
                    scale.y *= Mathf.Lerp(1f, T.turnStretchY, k);
                }
                else
                {
                    if (!swapped)
                    {
                        SetFacing(turnToFront);
                        swapped = true;
                        fx.Dust(Feet(0), 0f); // hides the swap
                    }
                    float k = Mathf.Clamp01((turnTimer - half) / half);
                    scale.x *= Mathf.Lerp(0.1f, 1f, EaseOutBack(k));
                    scale.y *= Mathf.Lerp(T.turnStretchY, 1f, k);
                    if (turnTimer >= T.turnTime) turnTimer = -1f;
                }
            }

            if (Dead)
            {
                scale = new Vector3(1.08f, 0.85f, 1f); // slump, stays turned away
                lean = 0f;
            }

            rig.localScale = scale;
            rig.localPosition = new Vector3(0f, Mathf.Round(offsetY * T.pixelsPerUnit) * Px, 0f);
            rig.localRotation = Quaternion.Euler(0f, 0f, lean);
            launcherBack.transform.localPosition = launcherBackHome + new Vector3(0f, kick, 0f);
            launcherFront.transform.localPosition = launcherFrontHome + new Vector3(0f, kick + launcherLift, 0f);
            handFront.enabled = handUp && bodyFront.enabled;
            hand.localPosition = new Vector3((handPixels.x - 11.5f) * Px, handPixels.y * Px, 0f);

            // Flame = health: full at 3, small at 2, flickering at 1
            if (!Dead && celebrateTimer < 0f)
            {
                flameClock += dt;
                float s = Hearts >= 3 ? 1f : Hearts == 2 ? 0.75f : 0.55f;
                if (Hearts <= 1 && Mathf.FloorToInt(flameClock * 14f) % 3 == 0) s *= 0.7f;
                else if (Mathf.FloorToInt(flameClock * 10f) % 2 == 0) s *= 0.93f; // two-step flicker
                flameBack.transform.localScale = flameFront.transform.localScale = new Vector3(s, s, 1f);
            }

            // Hurt flash, then blink at 10 Hz while invulnerable
            if (hurtFlash >= 0f)
            {
                hurtFlash -= dt;
                if (hurtFlash < 0f) ApplyFlash(false);
            }
            bool visible = hurtFlash >= 0f || invulnerable <= 0f || Mathf.FloorToInt(invulnerable * 20f) % 2 == 0;
            SetVisible(visible);
        }

        /// <summary>Two hops: stretch on take-off, squash on landing with dust, arms up on the second hop, flame at 130%.</summary>
        void Celebrating(float dt, ref Vector3 scale, ref float offsetY, ref bool handUp, ref float launcherLift, ref Vector2 handPixels)
        {
            celebrateTimer += dt;
            float hopTime = T.celebrateTime * 0.5f;
            const float air = 0.8f; // fraction of each hop in the air; the rest is the landing squash
            if (celebrateTimer < T.celebrateTime)
            {
                int hop = Mathf.FloorToInt(celebrateTimer / hopTime);
                float u = (celebrateTimer - hop * hopTime) / hopTime;
                if (u < air)
                {
                    float a = u / air;
                    offsetY = Mathf.Sin(a * Mathf.PI) * T.celebrateHop;
                    scale = a < 0.25f ? new Vector3(0.9f, 1.12f, 1f) : Vector3.one;
                }
                else
                {
                    if (celebrateLandings <= hop)
                    {
                        celebrateLandings = hop + 1;
                        fx.Dust(Feet(-4), -1f);
                        fx.Dust(Feet(4), 1f);
                    }
                    float k = (u - air) / (1f - air);
                    scale = Squash(new Vector2(1.12f, 0.88f), k);
                }
                if (hop >= 1)
                {
                    handUp = true;
                    handPixels = new Vector2(20f, 25f);
                    launcherLift = 4f * Px;
                }
            }
            else
            {
                // Done: stand proud, arms still up.
                handUp = true;
                handPixels = new Vector2(20f, 25f);
                launcherLift = 4f * Px;
            }
            float s = T.celebrateFlameScale;
            flameBack.transform.localScale = flameFront.transform.localScale = new Vector3(s, s, 1f);
        }

        /// <summary>Scale (x, y) at k = 0, easing to round at k = 1.</summary>
        static Vector3 Squash(Vector2 s, float k) =>
            new Vector3(Mathf.Lerp(s.x, 1f, k), Mathf.Lerp(s.y, 1f, k), 1f);

        static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);

        static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
        }
    }
}
