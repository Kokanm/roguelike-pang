using System.Collections.Generic;
using UnityEngine;

namespace Bubblewright
{
    public partial class Game
    {
        readonly List<Pickup> pickups = new List<Pickup>();
        int tileShakeFrames;

        public void HitTile(int c, int r)
        {
            if (Map == null || Map.Get(c, r).kind != TileKind.Breakable) return;

            Vector2 centre = StageMap.CellRect(c, r).center;
            bool destroyed = Map.Damage(c, r);
            if (!destroyed)
            {
                fx.Debris(centre, Palette.CatacombBrick, Palette.CatacombBrickDark, 3);
                Sfx.Play(Sound.Bounce, 1.7f, 0.65f);
                return;
            }

            fx.Debris(centre, Palette.CatacombBrick, Palette.CatacombBrickDark, 8);
            Sfx.Play(Sound.Pop, 0.7f, 0.8f);
            tileShakeFrames = Mathf.Max(tileShakeFrames, 2);

            if (wick.Hearts < run.MaxHearts && Random.value < T.breakableHeartChance)
                pickups.Add(Pickup.Create(world, centre));
        }

        int ChipColumn()
        {
            Rect body = wick.Hitbox;
            float reach = T.Pixel * 2f;
            int left = StageMap.Col(body.xMin - reach);
            int right = StageMap.Col(body.xMax + reach);

            bool leftBreakable = BreakableAtBodyHeight(left, body) &&
                                 body.xMin - StageMap.CellRect(left, 0).xMax <= reach + 0.0001f;
            bool rightBreakable = BreakableAtBodyHeight(right, body) &&
                                  StageMap.CellRect(right, 0).xMin - body.xMax <= reach + 0.0001f;
            if (leftBreakable && rightBreakable)
            {
                float leftGap = Mathf.Abs(body.xMin - StageMap.CellRect(left, 0).xMax);
                float rightGap = Mathf.Abs(StageMap.CellRect(right, 0).xMin - body.xMax);
                return leftGap <= rightGap ? left : right;
            }
            if (leftBreakable) return left;
            return rightBreakable ? right : -1;
        }

        bool BreakableAtBodyHeight(int c, Rect body)
        {
            if (c < 0 || c >= StageMap.Cols) return false;
            int rMin = Mathf.Max(0, StageMap.Row(body.yMin));
            int rMax = Mathf.Min(StageMap.Rows - 1, StageMap.Row(body.yMax - 0.0001f));
            for (int r = rMin; r <= rMax; r++)
                if (Map.Get(c, r).kind == TileKind.Breakable)
                    return true;
            return false;
        }

        void TickPickups(float dt)
        {
            Rect collector = wick.Hearts < run.MaxHearts
                ? wick.Hitbox
                : new Rect(1000f, 1000f, 0f, 0f);

            for (int i = pickups.Count - 1; i >= 0; i--)
            {
                Pickup pickup = pickups[i];
                bool collected = pickup.Tick(dt, Map, collector);
                if (collected)
                {
                    wick.Heal();
                    run.Hearts = wick.Hearts;
                    SaveGame.UpdateHearts(run.Hearts);
                    fx.Sparkle(pickup.Position, Palette.Mint);
                    Sfx.Play(Sound.Clear, 1.7f, 0.35f);
                }
                if (collected || !pickup.Active)
                {
                    Destroy(pickup.gameObject);
                    pickups.RemoveAt(i);
                }
            }
        }

        void ClearPickups()
        {
            foreach (var pickup in pickups)
                if (pickup) Destroy(pickup.gameObject);
            pickups.Clear();
        }
    }
}
