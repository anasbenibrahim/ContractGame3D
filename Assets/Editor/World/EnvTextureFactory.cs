using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Contract.Editor.World
{
    /// <summary>
    /// Generates the tileable environment textures for the Korean districts (no external assets).
    /// Every texture documents the real-world size of one tile so meshes can use metric UVs.
    /// </summary>
    public static class EnvTextureFactory
    {
        public const string Folder = "Assets/Art/KoreanEnvironment/Textures";

        // Real-world size (metres) covered by one texture tile.
        public static readonly Vector2 GlassTile = new Vector2(14.4f, 28.8f);      // 8 panels x 8 floors
        public static readonly Vector2 CommercialTile = new Vector2(12f, 14f);     // 4 bays x 4 floors
        public static readonly Vector2 ApartmentTile = new Vector2(16f, 22.4f);    // 4 units x 8 floors
        public static readonly Vector2 PaverTile = new Vector2(2f, 2f);
        public static readonly Vector2 TactileTile = new Vector2(0.3f, 0.3f);
        public static readonly Vector2 AsphaltTile = new Vector2(6f, 6f);
        public static readonly Vector2 ConcreteTile = new Vector2(4f, 4f);

        public static void GenerateAll()
        {
            Directory.CreateDirectory(Folder);
            var rng = new System.Random(7);

            Save("T_Glass_Albedo", 512, (x, y) => GlassAlbedo(x, y));
            Save("T_Glass_Emission", 512, GlassEmission(rng));
            Save("T_Commercial_Albedo", 512, (x, y) => CommercialAlbedo(x, y));
            Save("T_Commercial_Emission", 512, CommercialEmission(rng));
            Save("T_Apartment_Albedo", 512, (x, y) => ApartmentAlbedo(x, y));
            Save("T_Apartment_Emission", 512, ApartmentEmission(rng));
            Save("T_Pavers_Albedo", 512, (x, y) => Pavers(x, y));
            Save("T_Tactile_Albedo", 128, (x, y) => Tactile(x, y));
            Save("T_Asphalt_Albedo", 512, (x, y) => Asphalt(x, y));
            Save("T_Concrete_Albedo", 256, (x, y) => Concrete(x, y, 0.47f));
            Save("T_Roof_Albedo", 256, (x, y) => Concrete(x, y, 0.3f));

            AssetDatabase.Refresh();
            foreach (string file in Directory.GetFiles(Folder, "*.png"))
            {
                ConfigureImporter(file.Replace('\\', '/'));
            }
        }

        // ---- facades ------------------------------------------------------------------------

        private static Color GlassAlbedo(int x, int y)
        {
            const int cell = 64; // 8 x 8 cells per 512 px
            int cx = x % cell, cy = y % cell;
            bool mullion = cx < 3 || cy < 3;
            bool spandrel = cy < 10; // opaque band at each slab edge
            if (mullion) return new Color(0.16f, 0.17f, 0.19f);
            if (spandrel) return new Color(0.07f, 0.08f, 0.1f);
            float t = cy / (float)cell; // sky reflection gradient inside each pane
            return Color.Lerp(new Color(0.06f, 0.09f, 0.14f), new Color(0.15f, 0.2f, 0.28f), t);
        }

        private static Func<int, int, Color> GlassEmission(System.Random rng)
        {
            Color[] lit = new Color[64];
            for (int i = 0; i < lit.Length; i++)
            {
                double roll = rng.NextDouble();
                float level = 0.35f + (float)rng.NextDouble() * 0.65f; // not every office is equally lit
                lit[i] = roll < 0.12 ? new Color(1f, 0.86f, 0.62f) * level   // warm office light
                       : roll < 0.26 ? new Color(0.78f, 0.88f, 1f) * level   // cool LED
                       : Color.black;
            }

            return (x, y) =>
            {
                const int cell = 64;
                int cx = x % cell, cy = y % cell;
                if (cx < 3 || cy < 10) return Color.black;
                Color c = lit[(y / cell) * 8 + x / cell];
                float edge = Mathf.Clamp01(Mathf.Min(cx - 3, cell - cx, cy - 10, cell - cy) / 8f);
                return c * (0.55f + 0.45f * edge);
            };
        }

        private static Color CommercialAlbedo(int x, int y)
        {
            const int bay = 128, floor = 128; // 4 bays x 4 floors
            int bx = x % bay, fy = y % floor;
            bool windowBand = fy > 22 && fy < 100;
            bool mullion = bx < 4 || bx % 42 < 3;
            if (windowBand && !mullion) return Color.Lerp(new Color(0.08f, 0.1f, 0.12f), new Color(0.16f, 0.19f, 0.22f), (fy - 22) / 78f);
            if (windowBand) return new Color(0.22f, 0.23f, 0.24f);
            float n = Mathf.PerlinNoise(x * 0.08f, y * 0.08f) * 0.06f;
            return new Color(0.56f + n, 0.53f + n, 0.49f + n); // beige stone cladding
        }

        private static Func<int, int, Color> CommercialEmission(System.Random rng)
        {
            Color[] lit = new Color[16 * 3];
            for (int i = 0; i < lit.Length; i++)
            {
                double roll = rng.NextDouble();
                float level = 0.4f + (float)rng.NextDouble() * 0.6f;
                lit[i] = roll < 0.22 ? new Color(0.88f, 0.94f, 1f) * level    // fluorescent offices / academies
                       : roll < 0.33 ? new Color(1f, 0.82f, 0.58f) * level
                       : Color.black;
            }

            return (x, y) =>
            {
                int bx = x % 128, fy = y % 128;
                if (fy <= 22 || fy >= 100 || bx < 4 || bx % 42 < 3) return Color.black;
                return lit[(y / 128) * 12 + (x / 128) * 3 + Mathf.Min(2, bx / 42)] * 0.9f;
            };
        }

        private static Color ApartmentAlbedo(int x, int y)
        {
            const int unit = 128, floor = 64; // 4 units x 8 floors
            int ux = x % unit, fy = y % floor;
            bool slab = fy < 8;
            bool window = fy > 16 && fy < 54 && ux > 10 && ux < 118 && (ux < 62 || ux > 66);
            bool rail = fy > 16 && fy < 22;
            if (slab) return new Color(0.78f, 0.77f, 0.74f);
            if (window && !rail) return new Color(0.16f, 0.18f, 0.21f);
            if (rail) return new Color(0.62f, 0.63f, 0.64f);
            return new Color(0.88f, 0.87f, 0.83f);
        }

        private static Func<int, int, Color> ApartmentEmission(System.Random rng)
        {
            Color[] lit = new Color[32 * 2];
            for (int i = 0; i < lit.Length; i++)
            {
                double roll = rng.NextDouble();
                float level = 0.4f + (float)rng.NextDouble() * 0.6f;
                lit[i] = roll < 0.24 ? new Color(1f, 0.83f, 0.6f) * level : roll < 0.3 ? new Color(0.85f, 0.9f, 1f) * level : Color.black;
            }

            return (x, y) =>
            {
                int ux = x % 128, fy = y % 64;
                bool window = fy > 22 && fy < 54 && ux > 10 && ux < 118 && (ux < 62 || ux > 66);
                if (!window) return Color.black;
                return lit[(y / 64) * 8 + (x / 128) * 2 + (ux < 64 ? 0 : 1)] * 0.8f;
            };
        }

        // ---- ground -------------------------------------------------------------------------

        private static Color Pavers(int x, int y)
        {
            // 2 m tile, 20 x 10 cm bricks in running bond, two-tone grey / terracotta like Seoul sidewalks.
            const float pxPerMetre = 256f;
            int brickW = (int)(0.2f * pxPerMetre), brickH = (int)(0.1f * pxPerMetre);
            int row = y / brickH;
            int offset = row % 2 == 0 ? 0 : brickW / 2;
            int col = (x + offset) / brickW;
            int bx = (x + offset) % brickW, by = y % brickH;
            if (bx < 2 || by < 2) return new Color(0.2f, 0.2f, 0.2f);

            int hash = (col * 73856093) ^ (row * 19349663);
            float tone = ((hash >> 3) & 15) / 15f;
            bool red = ((hash >> 8) & 7) == 0;
            float n = Mathf.PerlinNoise(x * 0.15f, y * 0.15f) * 0.05f;
            Color baseColor = red ? new Color(0.5f, 0.36f, 0.32f) : new Color(0.5f, 0.5f, 0.5f);
            return baseColor * (0.88f + tone * 0.16f) + new Color(n, n, n);
        }

        private static Color Tactile(int x, int y)
        {
            // 0.3 m tile with a 5 x 5 grid of raised dots (warning blocks at crossings).
            const int cell = 128 / 5;
            float dx = x % cell - cell / 2f, dy = y % cell - cell / 2f;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            Color yellow = new Color(0.93f, 0.72f, 0.08f);
            if (x < 2 || y < 2) return yellow * 0.6f;
            if (d < cell * 0.32f) return yellow * (1.05f - d / cell * 0.5f);
            return yellow * 0.86f;
        }

        private static Color Asphalt(int x, int y)
        {
            float n = Mathf.PerlinNoise(x * 0.02f, y * 0.02f) * 0.03f + Mathf.PerlinNoise(x * 0.4f, y * 0.4f) * 0.04f;
            float speck = (x * 7919 + y * 104729) % 97 == 0 ? 0.08f : 0f;
            float v = 0.11f + n + speck;
            return new Color(v, v, v * 1.04f);
        }

        private static Color Concrete(int x, int y, float tone)
        {
            float n = Mathf.PerlinNoise(x * 0.05f, y * 0.05f) * 0.06f + Mathf.PerlinNoise(x * 0.5f, y * 0.5f) * 0.03f;
            float v = tone + n;
            return new Color(v, v * 0.99f, v * 0.97f);
        }

        // ---- io -----------------------------------------------------------------------------

        private static void Save(string name, int size, Func<int, int, Color> pixel)
        {
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGB24, false);
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    pixels[y * size + x] = pixel(x, y);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(Folder, name + ".png"), tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        private static void ConfigureImporter(string path)
        {
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null) return;
            importer.textureType = TextureImporterType.Default;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = true;
            importer.anisoLevel = 4;
            importer.sRGBTexture = true;
            importer.maxTextureSize = 512;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.SaveAndReimport();
        }

        public static Texture2D Load(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{Folder}/{name}.png");
    }
}
