using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// The Clinger sticks to any surface it touches: floor, ceiling, wall, or any face of a platform. It waits there
    /// until Wick crosses its lane, the strip straight out from that surface, or until it has held on for
    /// Tuning.clingerMaxStickTime. Then it wobbles, lets go away from the
    /// surface, and sticks again to the next surface it touches. Under a ceiling Wick crosses by passing below it, on a
    /// floor by being above it (a jump or a platform), on a right wall by being to its left, and so on.
    /// </summary>
    public class ClingerBehavior : BubbleBehavior
    {
        public enum State { Stuck, Wobbling, Free }

        const float FlipTime = 0.05f;

        State state = State.Free;
        float timer;
        float stuckTime;  // how long it has held the current surface
        float restick;    // counts down after letting go; no sticking while above 0
        bool stormFree;   // after the storm starts it never sticks again
        int dir;          // sideways direction it travels in after letting go of a floor or ceiling
        Vector2 normal;   // points out of the surface it holds, into the arena
        Vector2 anchor;   // the point of the surface it holds
        bool onTile;
        int tileCol, tileRow;
        GameObject pad;

        public State Current => state;
        /// <summary>Points out of the surface it holds: (0,-1) ceiling, (0,1) floor, (-1,0) right wall, (1,0) left wall.</summary>
        public Vector2 Normal => normal;
        public bool OnTile => onTile;

        Tuning T => Tuning.I;

        public override void OnSpawn(Bubble b, bool child)
        {
            if (b.Velocity.x != 0f) dir = b.Velocity.x < 0f ? -1 : 1;
            if (child) return; // children fly out of the pop and stick to the first surface they touch

            // A placed Clinger starts on the surface above its spawn point, or the arena ceiling.
            var p = b.Position;
            float surface = T.arenaHeight;
            bool tile = false;
            int col = 0, row = 0;
            if (Game.Map != null && b.Tier > 0)
            {
                float s = Game.Map.CeilingAbove(p.x, p.y, out int hitRow);
                if (hitRow < StageMap.Rows && ClearUnder(b, p.x, s))
                {
                    surface = s;
                    tile = true;
                    col = StageMap.Col(p.x);
                    row = hitRow;
                }
            }
            b.transform.position = new Vector3(p.x, surface - b.Radius, b.transform.position.z);
            Stick(b, new Vector2(0f, -1f), tile, col, row);
        }

        public override bool Tick(Bubble b, float dt)
        {
            // A Clinger born during the storm (a pop's children, a debug spawn) never sticks either.
            if (!stormFree && Game.StormOn) OnStorm(b);
            switch (state)
            {
                case State.Free:
                    MoveFree(b, dt);
                    return true;

                case State.Stuck:
                    // The game gives a placed bubble its direction after Create, so read it on the first tick.
                    if (dir == 0) dir = b.Velocity.x < 0f ? -1 : 1;
                    b.Velocity = Vector2.zero;
                    Seat(b);
                    if (onTile && Game.Map != null && !Game.Map.IsSolid(tileCol, tileRow))
                    {
                        LetGo(b);
                        return true;
                    }
                    stuckTime += dt;
                    if (WickInLane(b) || stuckTime >= T.clingerMaxStickTime)
                    {
                        state = State.Wobbling;
                        timer = 0f;
                        Game.Sparkle(b.Position, Palette.Cream);
                    }
                    return true;

                default: // Wobbling
                    b.Velocity = Vector2.zero;
                    Seat(b);
                    timer += dt;
                    if (timer >= T.clingerWobbleTime)
                    {
                        LetGo(b);
                        return true;
                    }
                    bool wide = (int)(timer / FlipTime) % 2 == 0;
                    b.VisualScale = wide ? new Vector2(1.1f, 0.92f) : new Vector2(0.92f, 1.1f);
                    return true;
            }
        }

        public override void OnStorm(Bubble b)
        {
            stormFree = true;
            if (state != State.Free) LetGo(b);
        }

        /// <summary>True when Wick is wholly past the face of the Clinger, inside the lane straight out from its surface.</summary>
        bool WickInLane(Bubble b)
        {
            var feet = Game.WickFeet;
            var box = new Rect(feet.x - T.hitbox.x * 0.5f, feet.y, T.hitbox.x, T.hitbox.y);
            var p = b.Position;
            float r = b.Radius;
            float reach = T.clingerTriggerX;
            if (normal.y < 0f) return box.yMax < p.y - r && Mathf.Abs(box.center.x - p.x) <= reach; // ceiling: Wick below
            if (normal.y > 0f) return box.yMin > p.y + r && Mathf.Abs(box.center.x - p.x) <= reach; // floor: Wick above
            float off = Mathf.Max(0f, Mathf.Max(box.yMin - p.y, p.y - box.yMax));
            if (off > reach) return false;
            return normal.x < 0f ? box.xMax < p.x - r  // right wall: Wick to its left
                                 : box.xMin > p.x + r; // left wall: Wick to its right
        }

        void LetGo(Bubble b)
        {
            state = State.Free;
            restick = T.clingerRestickDelay;
            if (dir == 0) dir = 1;
            b.VisualScale = Vector2.one;
            float fast = b.SpeedX * T.clingerLetGoSpeedScale; // it springs off faster than a Gloop moves
            if (normal.y < 0f) b.Velocity = new Vector2(dir * fast, -T.clingerDropSpeed);              // drop
            else if (normal.y > 0f) b.Velocity = new Vector2(dir * fast, T.BounceSpeed(b.Kind, b.Tier)); // a Gloop's bounce height
            else b.Velocity = new Vector2(normal.x * fast, 0f);                                        // push off the wall
            if (normal.x != 0f) dir = normal.x < 0f ? -1 : 1;
            ReleasePad();
        }

        /// <summary>The same ballistic step as a plain bubble, but it sticks to the first surface it touches.</summary>
        void MoveFree(Bubble b, float dt)
        {
            if (restick > 0f) restick -= dt;
            float g = T.Gravity(b.Kind, b.Tier);
            var p = b.Position;
            var v = b.Velocity;
            var displacement = new Vector2(v.x * dt, v.y * dt - 0.5f * g * dt * dt);
            int steps = Mathf.Max(1, Mathf.CeilToInt(displacement.magnitude / 0.4f));
            float stepDt = dt / steps;
            for (int i = 0; i < steps; i++)
            {
                p.x += v.x * stepDt;
                p.y += v.y * stepDt - 0.5f * g * stepDt * stepDt;
                v.y -= g * stepDt;
                if (v.x != 0f) dir = v.x < 0f ? -1 : 1;

                Contact contact = Game.Map != null ? Game.Map.ResolveCircle(ref p, b.Radius) : default;
                if (!contact.landed && !contact.bumped && contact.side == 0) continue;

                if (!stormFree && restick <= 0f)
                {
                    b.transform.position = p;
                    if (TryStick(b, p, contact))
                    {
                        b.Velocity = Vector2.zero;
                        b.StartSquash();
                        return;
                    }
                }
                if (contact.landed)
                {
                    v.y = T.BounceSpeed(b.Kind, b.Tier);
                    b.StartSquash();
                    Sfx.Play(Sound.Bounce, 1f + b.Tier * 0.25f, 0.5f - b.Tier * 0.1f);
                }
                if (contact.bumped) v.y = -Mathf.Abs(v.y);
                if (contact.side != 0) v.x = contact.side * Mathf.Abs(v.x);
            }
            b.transform.position = p;
            b.Velocity = v;
        }

        bool TryStick(Bubble b, Vector2 p, Contact contact)
        {
            Vector2 n = contact.landed ? Vector2.up
                      : contact.bumped ? Vector2.down
                      : new Vector2(contact.side, 0f);
            if (!FindSupport(p, b.Radius, n, out bool tile, out int col, out int row)) return false;
            if (tile && n.y < 0f && (b.Tier == 0 || !ClearUnder(b, p.x, p.y + b.Radius))) return false;
            Stick(b, n, tile, col, row);
            return true;
        }

        /// <summary>Under a platform, its bottom must leave room for Wick. An XL never fits, so it only hangs from the arena ceiling.</summary>
        bool ClearUnder(Bubble b, float x, float surface)
        {
            float bottom = surface - 2f * b.Radius;
            float floor = Game.Map != null ? Game.Map.FloorBelow(x, bottom) : 0f;
            return bottom - Mathf.Max(0f, floor) >= T.clingerMinClearance;
        }

        /// <summary>
        /// Finds what it touches on the far side of normal: a solid tile (to watch, in case it is shot away) or the arena edge.
        /// False when the contact was only a glancing corner it cannot hold on to.
        /// </summary>
        static bool FindSupport(Vector2 p, float r, Vector2 n, out bool tile, out int col, out int row)
        {
            tile = false;
            col = row = 0;
            var side = new Vector2(-n.y, n.x);
            float[] offsets = { 0f, -0.7f, 0.7f };
            for (int i = 0; i < offsets.Length; i++)
            {
                var probe = p - n * (r + 0.05f) + side * (offsets[i] * r);
                if (probe.x < -Tuning.I.HalfWidth || probe.x > Tuning.I.HalfWidth || probe.y < 0f || probe.y > StageMap.Rows)
                    return i == 0; // the arena itself: floor, ceiling or a side wall
                int c = StageMap.Col(probe.x), rr = StageMap.Row(probe.y);
                if (Game.Map != null && Game.Map.IsSolid(c, rr))
                {
                    tile = true;
                    col = c;
                    row = rr;
                    return true;
                }
            }
            return false;
        }

        void Stick(Bubble b, Vector2 n, bool tile, int col, int row)
        {
            state = State.Stuck;
            stuckTime = 0f;
            normal = n;
            anchor = b.Position - n * b.Radius;
            onTile = tile;
            tileCol = col;
            tileRow = row;
            b.VisualScale = Vector2.one;
            MakePad(b);
        }

        /// <summary>Keeps it resting on the surface while a freshly split child grows to full size.</summary>
        void Seat(Bubble b)
        {
            var at = anchor + normal * b.Radius;
            b.transform.position = new Vector3(at.x, at.y, b.transform.position.z);
        }

        /// <summary>
        /// Edit-mode tests have no play-mode destroy queue, and Object.Destroy there logs an error that fails the test.
        /// </summary>
        void ReleasePad()
        {
            if (pad == null) return;
            if (Application.isPlaying) Object.Destroy(pad);
            else Object.DestroyImmediate(pad);
            pad = null;
        }

        void MakePad(Bubble b)
        {
            ReleasePad();
            var sprite = PlaceholderArt.I.ClingerPad(b.Tier);
            pad = new GameObject("Clinger pad");
            pad.transform.SetParent(b.transform, false);
            // The pad's top faces the surface it holds and overlaps the edge of the bubble there.
            float angle = Mathf.Atan2(-normal.y, -normal.x) * Mathf.Rad2Deg - 90f;
            pad.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            var at = b.Position - normal * (b.Radius - sprite.bounds.extents.y);
            pad.transform.position = new Vector3(at.x, at.y, b.transform.position.z);
            var sr = pad.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            Sorting.Apply(sr, Layer.Bubbles, 3 - b.Tier + 1);
            Game.ApplySpriteMaterial(sr);
        }
    }
}
