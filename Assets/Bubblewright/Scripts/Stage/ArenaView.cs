using UnityEngine;

namespace Bubblewright
{
    /// <summary>
    /// Draws the parts of ArenaEvents that are not tiles: the rising hazard line and the flashing balloon pipes.
    /// Tiles repaint themselves from the map. Game owns one and syncs it every tick.
    /// </summary>
    public class ArenaView : MonoBehaviour
    {
        const int Ppu = 15;
        const int PipeSlots = 6;

        SpriteRenderer lava, lavaCap;
        readonly SpriteRenderer[] pipes = new SpriteRenderer[PipeSlots];
        Sprite square, pipeSprite;

        public static ArenaView Create(Transform parent)
        {
            var go = new GameObject("Arena View");
            go.transform.SetParent(parent, false);
            return go.AddComponent<ArenaView>();
        }

        void EnsureParts()
        {
            if (lava != null) return;
            var c = new PixelCanvas(Ppu, Ppu);
            c.Rect(0, 0, Ppu, Ppu, Color.white);
            square = c.ToSprite("Arena Square", new Vector2(0f, 0f), Ppu);

            var pipe = new PixelCanvas(3 * Ppu, 2 * Ppu);
            pipe.Rect(2, 0, 3 * Ppu - 4, 2 * Ppu - 5, Palette.Steel);
            pipe.Rect(2, 0, 3 * Ppu - 4, 3, Palette.SteelDark);
            pipe.Rect(0, 2 * Ppu - 5, 3 * Ppu, 5, Palette.SteelLight);
            pipe.Rect(0, 2 * Ppu - 5, 3 * Ppu, 1, Palette.SteelDark);
            pipe.Outline(Palette.Ink);
            pipeSprite = pipe.ToSprite("Balloon Pipe", new Vector2(1.5f * Ppu, 2f * Ppu), Ppu);

            lava = Make("Hazard", square, Palette.Coral, -1);
            lavaCap = Make("Hazard Cap", square, Palette.Saffron, 0);
            for (int i = 0; i < PipeSlots; i++) pipes[i] = Make("Pipe " + i, pipeSprite, Color.white, 1);
        }

        SpriteRenderer Make(string name, Sprite sprite, Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.color = color;
            r.enabled = false;
            Sorting.Apply(r, order > 0 ? Layer.FX : Layer.Stage, 5 + order);
            Game.ApplySpriteMaterial(r);
            return r;
        }

        public void Hide()
        {
            if (lava == null) return;
            lava.enabled = lavaCap.enabled = false;
            foreach (var p in pipes) p.enabled = false;
        }

        /// <summary>Places the hazard line and a pipe for every flash in progress.</summary>
        public void Sync(ArenaEvents arena)
        {
            if (arena == null) { Hide(); return; }
            EnsureParts();
            bool lit = arena.HazardActive && arena.HazardY > 0.01f;
            lava.enabled = lavaCap.enabled = lit;
            if (lit)
            {
                float y = arena.HazardY;
                lava.transform.localPosition = new Vector3(-StageMap.Cols * 0.5f, 0f, 0f);
                lava.transform.localScale = new Vector3(StageMap.Cols, y, 1f);
                lavaCap.transform.localPosition = new Vector3(-StageMap.Cols * 0.5f, y - 1f / Ppu * 2f, 0f);
                lavaCap.transform.localScale = new Vector3(StageMap.Cols, 2f / Ppu, 1f);
            }
            foreach (var p in pipes) p.enabled = false;
            int slot = 0;
            arena.ForEachFlashingPipe((spot, storey, t) =>
            {
                if (slot >= PipeSlots) return;
                var r = pipes[slot++];
                r.enabled = true;
                r.transform.localPosition = new Vector3(ArenaEvents.PipeX(spot), (storey + 1) * (float)StageMap.StoreyRows, 0f);
                // Blink faster as the Gloop is about to come out.
                bool on = Mathf.FloorToInt(t * 8f) % 2 == 0;
                r.color = on ? Color.white : (Color)Palette.Cream;
            });
        }
    }
}
