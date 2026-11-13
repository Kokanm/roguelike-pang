using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// The Clinger starts stuck to the underside of a platform or the arena ceiling and does not move. When Wick stands
    /// below it and close, it wobbles for a moment, drops, and from then on bounces like a Gloop of its size.
    /// </summary>
    public class ClingerBehavior : BubbleBehavior
    {
        public enum State { Hanging, Wobbling, Free }

        const float FlipTime = 0.05f;

        State state = State.Free;
        float timer;
        int dir;
        bool onTile;
        int tileCol, tileRow;
        GameObject pad;

        public State Current => state;
        public bool HangsFromTile => onTile;

        public override void OnSpawn(Bubble b, bool child)
        {
            if (child) return; // children of a popped Clinger bounce normally from birth
            state = State.Hanging;
            if (b.Velocity.x != 0f) dir = b.Velocity.x < 0f ? -1 : 1;
            b.Velocity = Vector2.zero;

            var p = b.Position;
            float surface;
            if (b.Tier == 0 || Game.Map == null)
            {
                // An XL would reach Wick's head under a platform, so it only hangs from the arena ceiling.
                surface = Tuning.I.arenaHeight;
                onTile = false;
            }
            else
            {
                surface = Game.Map.CeilingAbove(p.x, p.y, out int hitRow);
                onTile = hitRow < StageMap.Rows;
                if (onTile)
                {
                    tileCol = StageMap.Col(p.x);
                    tileRow = hitRow;
                }
            }
            b.transform.position = new Vector3(p.x, surface - b.Radius, b.transform.position.z);
            MakePad(b, surface);
        }

        public override bool Tick(Bubble b, float dt)
        {
            switch (state)
            {
                case State.Free:
                    return false;

                case State.Hanging:
                    // The game gives a bubble its direction after Create, so read it on the first tick.
                    if (dir == 0) dir = b.Velocity.x < 0f ? -1 : 1;
                    b.Velocity = Vector2.zero;
                    if (onTile && Game.Map != null && !Game.Map.IsSolid(tileCol, tileRow))
                    {
                        Drop(b);
                        return false;
                    }
                    var feet = Game.WickFeet;
                    if (feet.y < b.Position.y - b.Radius && Mathf.Abs(feet.x - b.Position.x) <= Tuning.I.clingerTriggerX)
                    {
                        state = State.Wobbling;
                        timer = 0f;
                        Game.Sparkle(b.Position, Palette.Cream);
                    }
                    return true;

                default: // Wobbling
                    timer += dt;
                    if (timer >= Tuning.I.clingerWobbleTime)
                    {
                        Drop(b);
                        return false;
                    }
                    bool wide = (int)(timer / FlipTime) % 2 == 0;
                    b.VisualScale = wide ? new Vector2(1.1f, 0.92f) : new Vector2(0.92f, 1.1f);
                    return true;
            }
        }

        public override void OnStorm(Bubble b)
        {
            if (state != State.Free) Drop(b);
        }

        void Drop(Bubble b)
        {
            state = State.Free;
            if (dir == 0) dir = 1;
            b.VisualScale = Vector2.one;
            b.Velocity = new Vector2(dir * b.SpeedX, 0f);
            if (pad != null) Object.Destroy(pad);
            pad = null;
        }

        void MakePad(Bubble b, float surface)
        {
            var sprite = PlaceholderArt.I.ClingerPad(b.Tier);
            pad = new GameObject("Clinger pad");
            pad.transform.SetParent(b.transform, false);
            // The pad's top touches the surface and it overlaps the top of the bubble.
            pad.transform.position = new Vector3(b.Position.x, surface - sprite.bounds.extents.y, b.transform.position.z);
            var sr = pad.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            Sorting.Apply(sr, Layer.Bubbles, 3 - b.Tier + 1);
            Game.ApplySpriteMaterial(sr);
        }
    }
}
