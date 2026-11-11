using UnityEngine;

namespace Bubblewright
{
    /// <summary>Paints the stage tile grid into one transparent, pixel-perfect arena sprite.</summary>
    public class StageView : MonoBehaviour
    {
        const int TilePixels = 15;
        const int Width = StageMap.Cols * TilePixels;
        const int Height = StageMap.Rows * TilePixels;

        readonly Color32[] pixels = new Color32[Width * Height];
        SpriteRenderer spriteRenderer;
        Texture2D texture;
        Sprite sprite;
        StageMap paintedMap;
        int paintedVersion = -1;

        public static StageView Create(Transform parent)
        {
            var go = new GameObject("Stage Map");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(-StageMap.Cols * 0.5f, 0f, 0f);

            var view = go.AddComponent<StageView>();
            view.texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false)
            {
                name = "Stage Map",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            view.sprite = Sprite.Create(view.texture, new Rect(0f, 0f, Width, Height), Vector2.zero,
                TilePixels, 0, SpriteMeshType.FullRect);
            view.sprite.name = "Stage Map";
            view.spriteRenderer = go.AddComponent<SpriteRenderer>();
            view.spriteRenderer.sprite = view.sprite;
            Sorting.Apply(view.spriteRenderer, Layer.Stage);
            Game.ApplySpriteMaterial(view.spriteRenderer);
            return view;
        }

        public void Tick()
        {
            StageMap map = Game.Map;
            if (map == null || map == paintedMap && map.Version == paintedVersion) return;

            System.Array.Clear(pixels, 0, pixels.Length);
            for (int r = 0; r < StageMap.Rows; r++)
            {
                for (int c = 0; c < StageMap.Cols; c++)
                {
                    Cell cell = map.Get(c, r);
                    if (cell.kind != TileKind.Empty) PaintTile(map, c, r, cell.kind);
                    if (cell.ladder) PaintLadder(c, r);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            paintedMap = map;
            paintedVersion = map.Version;
        }

        void PaintTile(StageMap map, int c, int r, TileKind kind)
        {
            int x0 = c * TilePixels, y0 = r * TilePixels;
            Color32 fill = kind == TileKind.Breakable
                ? Palette.Mix(Palette.Dusk, Palette.Lilac, 0.35f)
                : Palette.Dusk;
            Rect(x0, y0, TilePixels, TilePixels, fill);

            if (kind == TileKind.Breakable)
            {
                for (int y = 5; y < 11; y += 5) Rect(x0, y0 + y, TilePixels, 1, Palette.Deep);
                for (int course = 0; course < 3; course++)
                {
                    int y = y0 + course * 5;
                    int joint = course % 2 == 0 ? 5 : 10;
                    for (int x = joint; x < TilePixels; x += 10)
                        Rect(x0 + x, y, 1, Mathf.Min(5, TilePixels - course * 5), Palette.Deep);
                }
            }

            Rect(x0, y0 + 12, TilePixels, 3, Palette.Lilac);
            Rect(x0, y0 + 11, TilePixels, 1, Palette.Deep);

            if (!map.IsSolid(c - 1, r)) Rect(x0, y0, 1, TilePixels, Palette.Ink);
            if (!map.IsSolid(c + 1, r)) Rect(x0 + 14, y0, 1, TilePixels, Palette.Ink);
            if (!map.IsSolid(c, r - 1)) Rect(x0, y0, TilePixels, 1, Palette.Ink);
            if (!map.IsSolid(c, r + 1)) Rect(x0, y0 + 14, TilePixels, 1, Palette.Ink);
        }

        void PaintLadder(int c, int r)
        {
            int x0 = c * TilePixels, y0 = r * TilePixels;
            Rect(x0 + 3, y0, 1, TilePixels, Palette.Steel);
            Rect(x0 + 11, y0, 1, TilePixels, Palette.Steel);
            for (int y = 0; y < TilePixels; y += 4)
                Rect(x0 + 3, y0 + y, 9, 1, Palette.SteelLight);
        }

        void Rect(int x, int y, int width, int height, Color32 color)
        {
            for (int py = y; py < y + height; py++)
                for (int px = x; px < x + width; px++)
                    pixels[py * Width + px] = color;
        }

        void OnDestroy()
        {
            if (sprite != null) Destroy(sprite);
            if (texture != null) Destroy(texture);
        }
    }
}
