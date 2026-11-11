using System.Collections.Generic;
using UnityEngine;

namespace Bubblewright
{
    public enum TileKind { Empty, Solid, Breakable }

    public enum Surface { Normal, Ice, Sand }

    public struct Cell
    {
        public TileKind kind;
        public Surface surface;
        public bool ladder;
        public byte hp;
    }

    public struct Contact
    {
        public bool landed;
        public bool bumped;
        public int side;
    }

    /// <summary>
    /// The 32 x 14 arena tile grid (columns cover x from -16 to +16, rows cover y from 0 to 14)
    /// and plain-maths queries against it.
    /// </summary>
    public class StageMap
    {
        public const int Cols = 32, Rows = 14;
        const float HalfWidth = Cols * 0.5f;

        readonly Cell[,] cells = new Cell[Cols, Rows];
        readonly Surface[] ground = new Surface[Cols];
        readonly List<(int c, int r, float distSq)> hits = new List<(int, int, float)>(16);

        public int Version { get; private set; }

        public static StageMap Flat() => new StageMap();

        public Cell Get(int c, int r)
        {
            if (c < 0 || c >= Cols || r < 0 || r >= Rows)
                return new Cell { kind = TileKind.Solid };
            return cells[c, r];
        }

        public void Set(int c, int r, Cell cell)
        {
            if (c < 0 || c >= Cols || r < 0 || r >= Rows) return;
            cells[c, r] = cell;
            Version++;
        }

        public Surface GetGround(int c) => c >= 0 && c < Cols ? ground[c] : Surface.Normal;

        public void SetGround(int c, Surface s)
        {
            if (c < 0 || c >= Cols) return;
            ground[c] = s;
            Version++;
        }

        public static int Col(float x) => Mathf.FloorToInt(x + HalfWidth);
        public static int Row(float y) => Mathf.FloorToInt(y);
        public static Rect CellRect(int c, int r) => new Rect(-HalfWidth + c, r, 1f, 1f);
        public static float CenterX(int c) => -HalfWidth + c + 0.5f;

        public bool IsSolid(int c, int r)
        {
            var k = Get(c, r).kind;
            return k == TileKind.Solid || k == TileKind.Breakable;
        }

        /// <summary>Damages a breakable cell once and returns true only when that hit destroys it.</summary>
        public bool Damage(int c, int r)
        {
            Cell cell = Get(c, r);
            if (cell.kind != TileKind.Breakable) return false;

            if (cell.hp > 1)
            {
                cell.hp--;
                Set(c, r, cell);
                return false;
            }

            Set(c, r, new Cell { kind = TileKind.Empty, ladder = false });
            return true;
        }

        public float FloorBelow(float x, float y)
        {
            int c = Col(x);
            if (c < 0 || c >= Cols) return 0f;
            for (int r = Mathf.Min(Rows - 1, Row(y)); r >= 0; r--)
                if (r + 1f <= y && IsSolid(c, r)) return r + 1f;
            return 0f;
        }

        public float CeilingAbove(float x, float y, out int hitRow)
        {
            int c = Col(x);
            if (c >= 0 && c < Cols)
            {
                for (int r = Mathf.Max(0, Row(y)); r < Rows; r++)
                {
                    if (r >= y && IsSolid(c, r))
                    {
                        hitRow = r;
                        return r;
                    }
                }
            }
            hitRow = Rows;
            return Rows;
        }

        public bool OverlapsSolid(Rect r)
        {
            if (r.xMin < -HalfWidth || r.xMax > HalfWidth || r.yMin < 0f || r.yMax > Rows)
                return true;

            int cMin = Mathf.Max(0, Col(r.xMin));
            int cMax = Mathf.Min(Cols - 1, Col(r.xMax));
            int rMin = Mathf.Max(0, Row(r.yMin));
            int rMax = Mathf.Min(Rows - 1, Row(r.yMax));
            for (int c = cMin; c <= cMax; c++)
                for (int row = rMin; row <= rMax; row++)
                    if (IsSolid(c, row) && r.Overlaps(CellRect(c, row)))
                        return true;
            return false;
        }

        public Surface SurfaceUnder(float x, float y)
        {
            int c = Col(x);
            if (Mathf.Abs(y) <= 0.01f) return GetGround(c);
            return Get(c, Row(y - 0.01f)).surface;
        }

        public Contact ResolveCircle(ref Vector2 position, float radius)
        {
            var contact = default(Contact);
            ClampToArena(ref position, radius, ref contact);

            float rSq = radius * radius;
            for (int pass = 0; pass < 3; pass++)
            {
                hits.Clear();
                int cMin = Mathf.Max(0, Col(position.x - radius));
                int cMax = Mathf.Min(Cols - 1, Col(position.x + radius));
                int rMin = Mathf.Max(0, Row(position.y - radius));
                int rMax = Mathf.Min(Rows - 1, Row(position.y + radius));

                for (int c = cMin; c <= cMax; c++)
                {
                    for (int r = rMin; r <= rMax; r++)
                    {
                        if (!IsSolid(c, r)) continue;
                        Rect rect = CellRect(c, r);
                        float cx = Mathf.Clamp(position.x, rect.xMin, rect.xMax);
                        float cy = Mathf.Clamp(position.y, rect.yMin, rect.yMax);
                        float dx = position.x - cx, dy = position.y - cy;
                        float dSq = dx * dx + dy * dy;
                        if (dSq < rSq) hits.Add((c, r, dSq));
                    }
                }

                if (hits.Count == 0) break;
                hits.Sort((a, b) => a.distSq.CompareTo(b.distSq));

                bool moved = false;
                for (int i = 0; i < hits.Count; i++)
                {
                    var (c, r, _) = hits[i];
                    Rect rect = CellRect(c, r);
                    if (position.x >= rect.xMin && position.x <= rect.xMax &&
                        position.y >= rect.yMin && position.y <= rect.yMax)
                    {
                        PushCentreOutOfTile(c, r, rect, radius, ref position, ref contact);
                        moved = true;
                    }
                    else
                    {
                        float cx = Mathf.Clamp(position.x, rect.xMin, rect.xMax);
                        float cy = Mathf.Clamp(position.y, rect.yMin, rect.yMax);
                        float dx = position.x - cx, dy = position.y - cy;
                        float dSq = dx * dx + dy * dy;
                        if (dSq < rSq)
                        {
                            float dist = Mathf.Sqrt(dSq);
                            var dir = new Vector2(dx / dist, dy / dist);
                            position = new Vector2(cx + dir.x * radius, cy + dir.y * radius);
                            RecordPush(ref contact, dir);
                            moved = true;
                        }
                    }
                }

                ClampToArena(ref position, radius, ref contact);
                if (!moved) break;
            }

            return contact;
        }

        void PushCentreOutOfTile(int c, int r, Rect rect, float radius, ref Vector2 position, ref Contact contact)
        {
            float rawLeft = position.x - rect.xMin;
            float rawRight = rect.xMax - position.x;
            float rawBottom = position.y - rect.yMin;
            float rawTop = rect.yMax - position.y;

            float dLeft = IsSolid(c - 1, r) ? float.PositiveInfinity : rawLeft;
            float dRight = IsSolid(c + 1, r) ? float.PositiveInfinity : rawRight;
            float dBottom = IsSolid(c, r - 1) ? float.PositiveInfinity : rawBottom;
            float dTop = IsSolid(c, r + 1) ? float.PositiveInfinity : rawTop;

            if (float.IsPositiveInfinity(dLeft) && float.IsPositiveInfinity(dRight) &&
                float.IsPositiveInfinity(dBottom) && float.IsPositiveInfinity(dTop))
            {
                dLeft = rawLeft;
                dRight = rawRight;
                dBottom = rawBottom;
                dTop = rawTop;
            }

            float minX = Mathf.Min(dLeft, dRight);
            float minY = Mathf.Min(dBottom, dTop);
            if (minY <= minX)
            {
                if (dTop <= dBottom)
                {
                    position.y = rect.yMax + radius;
                    RecordPush(ref contact, Vector2.up);
                }
                else
                {
                    position.y = rect.yMin - radius;
                    RecordPush(ref contact, Vector2.down);
                }
            }
            else
            {
                if (dRight <= dLeft)
                {
                    position.x = rect.xMax + radius;
                    RecordPush(ref contact, Vector2.right);
                }
                else
                {
                    position.x = rect.xMin - radius;
                    RecordPush(ref contact, Vector2.left);
                }
            }
        }

        static void ClampToArena(ref Vector2 position, float radius, ref Contact contact)
        {
            if (position.x < -HalfWidth + radius)
            {
                position.x = -HalfWidth + radius;
                contact.side = 1;
            }
            else if (position.x > HalfWidth - radius)
            {
                position.x = HalfWidth - radius;
                contact.side = -1;
            }

            if (position.y < radius)
            {
                position.y = radius;
                contact.landed = true;
            }
            else if (position.y > Rows - radius)
            {
                position.y = Rows - radius;
                contact.bumped = true;
            }
        }

        static void RecordPush(ref Contact contact, Vector2 dir)
        {
            if (Mathf.Abs(dir.y) >= Mathf.Abs(dir.x))
            {
                if (dir.y > 0f) contact.landed = true;
                else if (dir.y < 0f) contact.bumped = true;
            }
            else
            {
                contact.side = dir.x > 0f ? 1 : -1;
            }
        }
    }
}
