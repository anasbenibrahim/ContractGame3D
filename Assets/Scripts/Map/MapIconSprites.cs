using System.Collections.Generic;
using UnityEngine;

namespace Contract.Map
{
    /// <summary>
    /// Anti-aliased map icon sprites drawn procedurally once at runtime (no external art needed).
    /// White sprites, tinted per icon type by the views.
    /// </summary>
    public static class MapIconSprites
    {
        public enum Shape { Circle, Ring, Diamond, Arrow, House, Check, Square, Dot, Exit, Outline }

        private const int Size = 64;
        private static readonly Dictionary<Shape, Sprite> cache = new Dictionary<Shape, Sprite>();

        public static Sprite Get(Shape shape)
        {
            if (cache.TryGetValue(shape, out Sprite sprite) && sprite != null) return sprite;
            Texture2D tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "MapIcon_" + shape };
            var pixels = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    // 4x supersampling for smooth edges.
                    float coverage = 0f;
                    for (int s = 0; s < 4; s++)
                    {
                        float u = (x + 0.25f + 0.5f * (s % 2)) / Size * 2f - 1f;
                        float v = (y + 0.25f + 0.5f * (s / 2)) / Size * 2f - 1f;
                        coverage += Inside(shape, u, v) ? 0.25f : 0f;
                    }

                    pixels[y * Size + x] = new Color32(255, 255, 255, (byte)(coverage * 255));
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply();
            sprite = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f);
            cache[shape] = sprite;
            return sprite;
        }

        /// <summary>Shape for an icon type / state.</summary>
        public static Shape ShapeFor(MapIconType type, bool completed) => completed ? Shape.Check : type switch
        {
            MapIconType.Player => Shape.Arrow,
            MapIconType.Objective => Shape.Diamond,
            MapIconType.MissionSubject => Shape.Ring,
            MapIconType.Safehouse => Shape.House,
            MapIconType.ExtractionPoint => Shape.Exit,
            MapIconType.Collectible => Shape.Square,
            MapIconType.PointOfInterest => Shape.Square,
            _ => Shape.Dot
        };

        public static Color ColorFor(MapIconType type, bool completed, bool highlighted)
        {
            if (completed) return new Color(0.3f, 0.83f, 0.54f);
            return type switch
            {
                MapIconType.Player => Color.white,
                MapIconType.Objective => new Color(1f, 0.23f, 0.28f),
                MapIconType.MissionSubject => highlighted ? new Color(1f, 0.23f, 0.28f) : new Color(1f, 0.55f, 0.58f),
                MapIconType.NPC => new Color(0.62f, 0.66f, 0.72f, 0.85f),
                MapIconType.Guard => highlighted ? new Color(1f, 0.25f, 0.2f) : new Color(1f, 0.69f, 0.13f),
                MapIconType.Collectible => new Color(1f, 0.83f, 0.3f),
                MapIconType.Safehouse => new Color(0.3f, 0.64f, 1f),
                MapIconType.ExtractionPoint => new Color(0.3f, 0.83f, 0.54f),
                _ => new Color(0.78f, 0.8f, 0.83f)
            };
        }

        /// <summary>Icon size in pixels at the reference resolution.</summary>
        public static float SizeFor(MapIconType type) => type switch
        {
            MapIconType.Player => 26f,
            MapIconType.NPC => 8f,
            MapIconType.Guard => 11f,
            MapIconType.PointOfInterest => 10f,
            _ => 20f
        };

        public static string LegendName(MapIconType type) => type switch
        {
            MapIconType.Player => "나 · You",
            MapIconType.Objective => "목표 · Objective",
            MapIconType.MissionSubject => "임무 인물 · Mission character",
            MapIconType.NPC => "시민 · Civilian",
            MapIconType.Guard => "경비원 · Security",
            MapIconType.Collectible => "수집품 · Collectible",
            MapIconType.Safehouse => "안가 · Safehouse",
            MapIconType.ExtractionPoint => "탈출 지점 · Extraction",
            _ => "장소 · Place"
        };

        private static bool Inside(Shape shape, float u, float v)
        {
            float r = Mathf.Sqrt(u * u + v * v);
            switch (shape)
            {
                case Shape.Circle: return r <= 0.95f;
                case Shape.Dot: return r <= 0.8f;
                case Shape.Outline: return r <= 0.99f && r >= 0.955f; // thin frame for the minimap
                case Shape.Ring: return (r <= 0.95f && r >= 0.62f) || r <= 0.32f;
                case Shape.Diamond: return Mathf.Abs(u) + Mathf.Abs(v) <= 0.92f;
                case Shape.Square: return Mathf.Abs(u) <= 0.72f && Mathf.Abs(v) <= 0.72f;
                case Shape.Arrow:
                    // Chevron pointing up (+v): triangle with a notch at the back.
                    bool inTriangle = v <= 0.95f && v >= -0.85f && Mathf.Abs(u) <= (0.95f - v) * 0.5f;
                    bool notch = v < -0.85f + (0.7f - Mathf.Abs(u) * 0.9f) && v < -0.15f;
                    return inTriangle && !notch;
                case Shape.House:
                    bool roof = v >= 0.05f && v <= 0.9f && Mathf.Abs(u) <= (0.9f - v) * 1.05f;
                    bool body = v < 0.1f && v >= -0.85f && Mathf.Abs(u) <= 0.62f;
                    bool door = v < -0.3f && Mathf.Abs(u) < 0.16f;
                    return (roof || body) && !door;
                case Shape.Check:
                    bool disc = r <= 0.95f;
                    float d1 = DistanceToSegment(u, v, -0.45f, 0f, -0.1f, -0.38f);
                    float d2 = DistanceToSegment(u, v, -0.1f, -0.38f, 0.5f, 0.38f);
                    return disc && Mathf.Min(d1, d2) > 0.13f; // disc with a check mark cut out
                case Shape.Exit:
                    bool outer = r <= 0.95f && r >= 0.7f;
                    bool arrowUp = v >= -0.5f && v <= 0.5f && Mathf.Abs(u) <= (0.5f - v) * 0.6f && v >= 0f
                                   || (Mathf.Abs(u) <= 0.12f && v < 0.05f && v >= -0.5f);
                    return outer || arrowUp;
            }

            return false;
        }

        private static float DistanceToSegment(float px, float py, float ax, float ay, float bx, float by)
        {
            float abx = bx - ax, aby = by - ay;
            float t = Mathf.Clamp01(((px - ax) * abx + (py - ay) * aby) / (abx * abx + aby * aby));
            float dx = px - (ax + abx * t), dy = py - (ay + aby * t);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }
    }
}
