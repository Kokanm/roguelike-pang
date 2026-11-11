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
        const int CapRows = 3;

        readonly Color32[] groundPixels = new Color32[Width * CapRows];
        SpriteRenderer spriteRenderer;
        Texture2D texture, groundTexture;
        Sprite sprite, groundSprite;
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

            // The floor at y = 0 is not a tile: a 3 px strip just below the floor line shows ice or sand there.
            var groundGo = new GameObject("Ground Surface");
            groundGo.transform.SetParent(go.transform, false);
            groundGo.transform.localPosition = new Vector3(0f, -CapRows * Tuning.I.Pixel, 0f);
            view.groundTexture = new Texture2D(Width, CapRows, TextureFormat.RGBA32, false)
            {
                name = "Ground Surface",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };
            view.groundSprite = Sprite.Create(view.groundTexture, new Rect(0f, 0f, Width, CapRows), Vector2.zero,
                TilePixels, 0, SpriteMeshType.FullRect);
            view.groundSprite.name = "Ground Surface";
            var groundRenderer = groundGo.AddComponent<SpriteRenderer>();
            groundRenderer.sprite = view.groundSprite;
            Sorting.Apply(groundRenderer, Layer.Stage);
            Game.ApplySpriteMaterial(groundRenderer);
            return view;
        }

        public void Tick()
        {
            StageMap map = Game.Map;
            if (map == null || map == paintedMap && map.Version == paintedVersion) return;

            System.Array.Clear(pixels, 0, pixels.Length);
            System.Array.Clear(groundPixels, 0, groundPixels.Length);
            for (int r = 0; r < StageMap.Rows; r++)
            {
                for (int c = 0; c < StageMap.Cols; c++)
                {
                    Cell cell = map.Get(c, r);
                    if (cell.kind != TileKind.Empty) PaintTile(map, c, r, cell.kind);
                    if (cell.ladder) PaintLadder(c, r);
                }
            }

            for (int c = 0; c < StageMap.Cols; c++)
                if (map.GetGround(c) != Surface.Normal) PaintGroundCap(c, map.GetGround(c));

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            groundTexture.SetPixels32(groundPixels);
            groundTexture.Apply(false, false);
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

            Surface surface = map.Get(c, r).surface;
            if (surface != Surface.Normal) PaintCap(pixels, Width, x0, y0 + 12, surface);

            if (!map.IsSolid(c - 1, r)) Rect(x0, y0, 1, TilePixels, Palette.Ink);
            if (!map.IsSolid(c + 1, r)) Rect(x0 + 14, y0, 1, TilePixels, Palette.Ink);
            if (!map.IsSolid(c, r - 1)) Rect(x0, y0, TilePixels, 1, Palette.Ink);
            if (!map.IsSolid(c, r + 1))
            {
                Rect(x0, y0 + 14, TilePixels, 1, Palette.Ink);
                // Sand has a wavy top: every other column sits 1 px lower.
                if (surface == Surface.Sand)
                {
                    for (int x = 1; x < TilePixels - 1; x += 2)
                    {
                        pixels[(y0 + 14) * Width + x0 + x] = Palette.Clear;
                        pixels[(y0 + 13) * Width + x0 + x] = Palette.Ink;
                    }
                }
            }
        }

        /// <summary>The 3 px cap of a ground column, drawn just below the floor line (same art as a tile cap).</summary>
        void PaintGroundCap(int c, Surface surface)
        {
            PaintCap(groundPixels, Width, c * TilePixels, 0, surface);
            if (surface != Surface.Sand) return;
            for (int x = 1; x < TilePixels; x += 2)
                groundPixels[2 * Width + c * TilePixels + x] = Palette.Clear;
        }

        /// <summary>
        /// Paints a 15 x 3 cap into a pixel buffer, with its bottom row at capY. Ice: pale cap, 2 px glints at x = 3 and 10,
        /// and a darker row under it on a tile. Sand: tan cap with four darker grains.
        /// </summary>
        static void PaintCap(Color32[] buffer, int bufferWidth, int x0, int capY, Surface surface)
        {
            Color32 cap = surface == Surface.Ice ? Palette.Ice : Palette.Sand;
            for (int y = 0; y < CapRows; y++)
                for (int x = 0; x < TilePixels; x++)
                    buffer[(capY + y) * bufferWidth + x0 + x] = cap;

            if (surface == Surface.Ice)
            {
                // The row under the cap is only part of a tile; the ground strip has no row below it.
                if (capY > 0)
                    for (int x = 0; x < TilePixels; x++)
                        buffer[(capY - 1) * bufferWidth + x0 + x] = Palette.IceShade;
                for (int g = 0; g < 2; g++)
                {
                    int gx = g == 0 ? 3 : 10;
                    buffer[(capY + 1) * bufferWidth + x0 + gx] = Palette.IceGlint;
                    buffer[(capY + 1) * bufferWidth + x0 + gx + 1] = Palette.IceGlint;
                }
            }
            else
            {
                buffer[(capY + 0) * bufferWidth + x0 + 2] = Palette.SandGrain;
                buffer[(capY + 1) * bufferWidth + x0 + 6] = Palette.SandGrain;
                buffer[(capY + 0) * bufferWidth + x0 + 9] = Palette.SandGrain;
                buffer[(capY + 1) * bufferWidth + x0 + 12] = Palette.SandGrain;
            }
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
            if (groundSprite != null) Destroy(groundSprite);
            if (groundTexture != null) Destroy(groundTexture);
        }
    }
}
