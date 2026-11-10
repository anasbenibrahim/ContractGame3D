using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Contract.Editor.World
{
    /// <summary>
    /// Shared building blocks for the procedural Korean districts: URP materials, metric-UV box
    /// meshes (textures keep real-world scale on any size), part placement and Hangul signage.
    /// All generated assets live under Assets/Art/KoreanEnvironment and are rebuilt idempotently.
    /// </summary>
    public sealed class EnvKit
    {
        public const string Root = "Assets/Art/KoreanEnvironment";
        public const string MaterialFolder = Root + "/Materials";
        public const string MeshFolder = Root + "/Meshes";
        private const string FontPath = "Assets/UI/Fonts/NotoSansKR/NotoSansKR SDF.asset";

        private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        private static readonly int EmissionMap = Shader.PropertyToID("_EmissionMap");

        private readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        private readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();
        private readonly Shader lit;

        public TMP_FontAsset Font { get; }
        public Material SignTextMaterial { get; private set; }
        public Material NeonTextMaterial { get; private set; }
        public Material PaintTextMaterial { get; private set; }
        public Material SteamMaterial { get; private set; }

        public EnvKit()
        {
            Directory.CreateDirectory(MaterialFolder);
            Directory.CreateDirectory(MeshFolder);
            lit = Shader.Find("Universal Render Pipeline/Lit");
            Font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            CreateMaterials();
            CreateTextMaterials();
            SteamMaterial = CreateSteamMaterial();
        }

        /// <summary>Soft round puff (generated texture) on the URP unlit particle shader, alpha blended.</summary>
        private Material CreateSteamMaterial()
        {
            string texturePath = $"{MaterialFolder}/T_SoftPuff.png";
            if (!File.Exists(texturePath))
            {
                var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
                for (int y = 0; y < 64; y++)
                for (int x = 0; x < 64; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f)) / 32f;
                    float a = Mathf.Clamp01(1f - d);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, a * a * (3f - 2f * a)));
                }

                File.WriteAllBytes(texturePath, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(texturePath);
            }

            string path = $"{MaterialFolder}/M_Steam.mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")) { name = "M_Steam" };
                AssetDatabase.CreateAsset(m, path);
            }

            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
            m.SetColor("_BaseColor", new Color(0.85f, 0.8f, 0.75f, 0.16f));
            m.SetFloat("_Surface", 1f); // transparent
            m.SetFloat("_Blend", 0f);   // alpha
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            return m;
        }

        // ---- materials -----------------------------------------------------------------------

        public Material this[string name] => materials[name];

        private void CreateMaterials()
        {
            Textured("Glass", "T_Glass_Albedo", "T_Glass_Emission", 0.8f, 0.9f, 0.25f);
            Textured("Commercial", "T_Commercial_Albedo", "T_Commercial_Emission", 0.75f, 0.35f, 0f);
            Textured("Apartment", "T_Apartment_Albedo", "T_Apartment_Emission", 0.6f, 0.2f, 0f);
            Textured("Pavers", "T_Pavers_Albedo", null, 0f, 0.42f, 0f); // damp after rain: catches the neon
            Textured("Tactile", "T_Tactile_Albedo", null, 0f, 0.35f, 0f);
            Textured("Asphalt", "T_Asphalt_Albedo", null, 0f, 0.72f, 0f); // wet road: reflects signs and headlights
            Textured("Concrete", "T_Concrete_Albedo", null, 0f, 0.2f, 0f);
            Textured("Roof", "T_Roof_Albedo", null, 0f, 0.15f, 0f);

            // Real-world CC0 PBR textures (Poly Haven, see Assets/Art/CC0/PolyHaven/LICENSE.md).
            PolyHaven("PH_Brick", "red_brick_03", 0.15f);
            PolyHaven("PH_RoofTiles", "grey_roof_tiles", 0.25f);
            PolyHaven("PH_Paving", "rectangular_paving", 0.55f);   // damp at night
            PolyHaven("PH_Granite", "granite_tile", 0.45f);
            PolyHaven("PH_Facade", "concrete_tile_facade", 0.3f);

            Flat("PaintWhite", new Color(0.86f, 0.86f, 0.84f), 0.3f, emission: 0.12f);
            Flat("PaintYellow", new Color(0.95f, 0.72f, 0.08f), 0.3f, emission: 0.1f);
            Flat("PaintBlue", new Color(0.12f, 0.35f, 0.85f), 0.3f, emission: 0.1f);
            Flat("Curb", new Color(0.6f, 0.6f, 0.58f), 0.2f);
            Flat("MetalDark", new Color(0.13f, 0.14f, 0.15f), 0.55f, metallic: 0.7f);
            Flat("MetalLight", new Color(0.62f, 0.64f, 0.66f), 0.6f, metallic: 0.8f);
            Flat("Steel", new Color(0.8f, 0.82f, 0.84f), 0.85f, metallic: 1f);
            Flat("Black", new Color(0.03f, 0.03f, 0.035f), 0.4f);
            Flat("Void", Color.black, 0f);
            Flat("Wood", new Color(0.42f, 0.27f, 0.16f), 0.3f);
            Flat("Bark", new Color(0.2f, 0.15f, 0.11f), 0.1f);
            Flat("Ginkgo", new Color(0.36f, 0.5f, 0.16f), 0.15f);
            Flat("GinkgoLight", new Color(0.52f, 0.62f, 0.2f), 0.15f);
            Flat("Planter", new Color(0.35f, 0.36f, 0.37f), 0.2f);
            Flat("Marble", new Color(0.78f, 0.77f, 0.74f), 0.8f);
            Flat("Carpet", new Color(0.16f, 0.17f, 0.2f), 0.05f);
            Flat("Monitor", new Color(0.02f, 0.03f, 0.05f), 0.9f, emission: 0.9f, emissionColor: new Color(0.25f, 0.55f, 1f));
            Flat("RackLed", Color.black, 0.5f, emission: 2.2f, emissionColor: new Color(0.2f, 1f, 0.6f));
            Flat("Tarp", new Color(0.9f, 0.4f, 0.1f), 0.3f, emission: 0.75f, emissionColor: new Color(1f, 0.45f, 0.12f));
            Flat("PlasticBlue", new Color(0.1f, 0.3f, 0.75f), 0.5f);
            Flat("PlasticRed", new Color(0.75f, 0.1f, 0.1f), 0.5f);
            Flat("GlassClear", new Color(0.55f, 0.68f, 0.75f), 0.95f);
            Flat("Shopfront", new Color(0.12f, 0.1f, 0.08f), 0.92f, emission: 0.4f, emissionColor: new Color(1f, 0.8f, 0.55f));
            Flat("ShopfrontCool", new Color(0.1f, 0.12f, 0.14f), 0.92f, emission: 0.36f, emissionColor: new Color(0.8f, 0.92f, 1f));
            Flat("LampWarm", Color.white, 0.5f, emission: 4f, emissionColor: new Color(1f, 0.86f, 0.66f));
            Flat("LampCool", Color.white, 0.5f, emission: 4f, emissionColor: new Color(0.85f, 0.92f, 1f));
            Flat("SignalRed", new Color(0.3f, 0.02f, 0.02f), 0.6f, emission: 4f, emissionColor: new Color(1f, 0.1f, 0.06f));
            Flat("SignalGreen", new Color(0.02f, 0.3f, 0.1f), 0.6f, emission: 4f, emissionColor: new Color(0.1f, 1f, 0.55f));
            Flat("SignalOff", new Color(0.05f, 0.05f, 0.05f), 0.6f);
            Flat("TailLight", new Color(0.4f, 0.02f, 0.02f), 0.6f, emission: 2.5f, emissionColor: new Color(1f, 0.05f, 0.03f));
            Flat("HeadLight", Color.white, 0.6f, emission: 3f, emissionColor: new Color(1f, 0.95f, 0.85f));
            Flat("Rubber", new Color(0.04f, 0.04f, 0.04f), 0.2f);
            Flat("CarWhite", new Color(0.86f, 0.87f, 0.88f), 0.85f, metallic: 0.3f);
            Flat("CarBlack", new Color(0.03f, 0.03f, 0.04f), 0.9f, metallic: 0.4f);
            Flat("CarSilver", new Color(0.55f, 0.57f, 0.6f), 0.85f, metallic: 0.7f);
            Flat("CarBlue", new Color(0.05f, 0.12f, 0.3f), 0.85f, metallic: 0.4f);
            Flat("TaxiOrange", new Color(0.95f, 0.45f, 0.08f), 0.8f, metallic: 0.2f);
            Flat("CarGlass", new Color(0.04f, 0.05f, 0.07f), 0.95f);
            Flat("RedPaint", new Color(0.7f, 0.06f, 0.06f), 0.5f);
            Flat("Hanok", new Color(0.3f, 0.32f, 0.34f), 0.3f);
            Flat("LineGreen", new Color(0.0f, 0.55f, 0.25f), 0.4f, emission: 1.6f, emissionColor: new Color(0.1f, 0.8f, 0.35f));
            Flat("SignDark", new Color(0.04f, 0.04f, 0.05f), 0.5f);
            Flat("RoadSignBlue", new Color(0.05f, 0.22f, 0.6f), 0.4f, emission: 0.6f, emissionColor: new Color(0.1f, 0.3f, 0.8f));

            // Neon / LED sign backings (bloom above threshold 1.05).
            SignColor("Red", new Color(0.95f, 0.12f, 0.16f));
            SignColor("White", new Color(1f, 0.98f, 0.94f));
            SignColor("Blue", new Color(0.15f, 0.45f, 1f));
            SignColor("Green", new Color(0.1f, 0.85f, 0.45f));
            SignColor("Yellow", new Color(1f, 0.8f, 0.15f));
            SignColor("Magenta", new Color(1f, 0.2f, 0.75f));
            SignColor("Cyan", new Color(0.2f, 0.9f, 1f));
            SignColor("Orange", new Color(1f, 0.5f, 0.12f));
            SignColor("Mint", new Color(0.35f, 1f, 0.8f));
            SignColor("Purple", new Color(0.6f, 0.3f, 1f));
        }

        private void SignColor(string name, Color color)
        {
            Flat("Sign" + name, color * 0.5f, 0.6f, emission: 1.12f, emissionColor: color);
        }

        private Material Textured(string name, string albedo, string emission, float emissionIntensity, float smoothness, float metallic)
        {
            Material m = GetOrCreate(name);
            m.SetTexture(BaseMap, EnvTextureFactory.Load(albedo));
            m.SetColor(BaseColor, Color.white);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            if (emission != null)
            {
                m.EnableKeyword("_EMISSION");
                m.SetTexture(EmissionMap, EnvTextureFactory.Load(emission));
                m.SetColor(EmissionColor, Color.white * emissionIntensity);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }

            EditorUtility.SetDirty(m);
            return m;
        }

        private const string PolyHavenFolder = "Assets/Art/CC0/PolyHaven";

        /// <summary>URP Lit material from a downloaded Poly Haven texture set (albedo + OpenGL normal map).</summary>
        private Material PolyHaven(string name, string id, float smoothness)
        {
            string albedoPath = $"{PolyHavenFolder}/{id}/{id}_diff_1k.jpg", normalPath = $"{PolyHavenFolder}/{id}/{id}_nor_gl_1k.jpg";
            Import(albedoPath, normal: false);
            Import(normalPath, normal: true);
            Material m = GetOrCreate(name);
            m.SetTexture(BaseMap, AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath));
            m.SetColor(BaseColor, Color.white);
            m.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));
            m.SetFloat("_BumpScale", 1f);
            m.EnableKeyword("_NORMALMAP");
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(m);
            return m;
        }

        private static void Import(string path, bool normal)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            TextureImporterType type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            if (importer.textureType == type && importer.maxTextureSize == 1024) return;
            importer.textureType = type;
            importer.sRGBTexture = !normal;
            importer.maxTextureSize = 1024;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
        }

        private Material Flat(string name, Color color, float smoothness, float metallic = 0f, float emission = 0f, Color? emissionColor = null)
        {
            Material m = GetOrCreate(name);
            m.SetColor(BaseColor, color);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            if (emission > 0f)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor(EmissionColor, (emissionColor ?? color) * emission);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }

            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        private Material GetOrCreate(string name)
        {
            string path = $"{MaterialFolder}/M_{name}.mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(lit) { name = "M_" + name };
                AssetDatabase.CreateAsset(m, path);
            }

            materials[name] = m;
            return m;
        }

        private void CreateTextMaterials()
        {
            SignTextMaterial = TextMaterial("NotoSansKR SDF Sign", Color.white, 1f);
            NeonTextMaterial = TextMaterial("NotoSansKR SDF Neon", Color.white, 2.6f); // HDR face -> bloom
            PaintTextMaterial = TextMaterial("NotoSansKR SDF RoadPaint", new Color(0.86f, 0.86f, 0.84f), 1f);
        }

        private Material TextMaterial(string name, Color face, float intensity)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Font.material) { name = name };
                AssetDatabase.CreateAsset(m, path);
            }

            m.shader = Font.material.shader;
            m.SetTexture("_MainTex", Font.atlasTexture);
            m.SetFloat("_TextureWidth", Font.atlasWidth);
            m.SetFloat("_TextureHeight", Font.atlasHeight);
            m.SetFloat("_GradientScale", Font.atlasPadding + 1);
            m.SetColor("_FaceColor", face * intensity);
            EditorUtility.SetDirty(m);
            return m;
        }

        // ---- meshes -------------------------------------------------------------------------

        /// <summary>
        /// Box with its pivot at the bottom centre and UVs in metres / tile, so a facade texture
        /// keeps its real scale (one floor stays one floor) whatever the building size.
        /// </summary>
        public Mesh Box(Vector3 size, Vector2 tile)
        {
            string key = $"box_{Mathf.RoundToInt(size.x * 100)}x{Mathf.RoundToInt(size.y * 100)}x{Mathf.RoundToInt(size.z * 100)}_t{Mathf.RoundToInt(tile.x * 100)}x{Mathf.RoundToInt(tile.y * 100)}";
            if (meshes.TryGetValue(key, out Mesh cached)) return cached;

            string path = $"{MeshFolder}/{key}.asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = BuildBox(size, tile);
                mesh.name = key;
                AssetDatabase.CreateAsset(mesh, path);
            }

            meshes[key] = mesh;
            return mesh;
        }

        private static Mesh BuildBox(Vector3 s, Vector2 tile)
        {
            float hx = s.x / 2f, hz = s.z / 2f, h = s.y;
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var tri = new List<int>();

            void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal, float uLen, float vLen)
            {
                int i = v.Count;
                v.AddRange(new[] { a, b, c, d });
                n.AddRange(new[] { normal, normal, normal, normal });
                uv.AddRange(new[] { new Vector2(0, 0), new Vector2(uLen, 0), new Vector2(uLen, vLen), new Vector2(0, vLen) });
                tri.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 }); // clockwise seen from outside = front face in Unity
            }

            float ux = s.x / tile.x, uz = s.z / tile.x, vy = h / tile.y;
            // Sides (a-b along the bottom edge, counter-clockwise seen from outside).
            Face(new Vector3(-hx, 0, hz), new Vector3(hx, 0, hz), new Vector3(hx, h, hz), new Vector3(-hx, h, hz), Vector3.forward, ux, vy);
            Face(new Vector3(hx, 0, -hz), new Vector3(-hx, 0, -hz), new Vector3(-hx, h, -hz), new Vector3(hx, h, -hz), Vector3.back, ux, vy);
            Face(new Vector3(hx, 0, hz), new Vector3(hx, 0, -hz), new Vector3(hx, h, -hz), new Vector3(hx, h, hz), Vector3.right, uz, vy);
            Face(new Vector3(-hx, 0, -hz), new Vector3(-hx, 0, hz), new Vector3(-hx, h, hz), new Vector3(-hx, h, -hz), Vector3.left, uz, vy);
            // Top and bottom use the horizontal tile in both directions.
            Face(new Vector3(-hx, h, hz), new Vector3(hx, h, hz), new Vector3(hx, h, -hz), new Vector3(-hx, h, -hz), Vector3.up, ux, s.z / tile.x);
            Face(new Vector3(-hx, 0, -hz), new Vector3(hx, 0, -hz), new Vector3(hx, 0, hz), new Vector3(-hx, 0, hz), Vector3.down, ux, s.z / tile.x);

            Mesh mesh = new Mesh();
            mesh.SetVertices(v);
            mesh.SetNormals(n);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(tri, 0);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        // ---- placement ----------------------------------------------------------------------

        public static Transform Group(Transform parent, string name, Vector3 localPosition = default, float yaw = 0f)
        {
            Transform t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPosition;
            t.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return t;
        }

        /// <summary>Box part with bottom-centre pivot at <paramref name="position"/>.</summary>
        public GameObject Block(Transform parent, string name, Vector3 position, Vector3 size, string material,
            Vector2? tile = null, bool collider = true, Vector3 euler = default, bool castShadows = true)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.AddComponent<MeshFilter>().sharedMesh = Box(size, tile ?? new Vector2(Mathf.Max(size.x, size.z, 1f), Mathf.Max(size.y, 1f)));
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = materials[material];
            renderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            if (collider)
            {
                BoxCollider box = go.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, size.y / 2f, 0f);
                box.size = size;
            }

            return go;
        }

        /// <summary>Unity primitive (cylinder / sphere / capsule) without collider unless requested.</summary>
        public GameObject Primitive(Transform parent, PrimitiveType type, string name, Vector3 position, Vector3 scale, string material,
            bool collider = false, Vector3 euler = default, bool castShadows = true)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = materials[material];
            renderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            return go;
        }

        // ---- Hangul signage -----------------------------------------------------------------

        /// <summary>
        /// A sign board facing local +Z: emissive/dark backing plus TMP text. Pass "\n" between
        /// characters for a vertical (세로) sign. <paramref name="sub"/> is an optional small
        /// second line (English).
        /// </summary>
        public Transform Sign(Transform parent, string name, Vector3 position, Vector2 size, string backing, string text,
            Color textColor, string sub = null, bool neonText = false, float yaw = 0f, float depth = 0.15f)
        {
            Transform sign = Group(parent, "Sign_" + name, position, yaw);
            Block(sign, "Board", new Vector3(0f, -size.y / 2f, 0f), new Vector3(size.x, size.y, depth), backing, collider: false, castShadows: false);

            float mainHeight = sub == null ? size.y : size.y * 0.68f;
            float mainY = sub == null ? 0f : size.y * 0.14f;
            bool glow = neonText || backing == "SignDark";
            Text(sign, "Text", text, new Vector3(0f, mainY, depth / 2f + 0.012f), new Vector2(size.x * 0.9f, mainHeight * 0.86f), textColor,
                glow ? NeonTextMaterial : SignTextMaterial);
            if (sub != null)
            {
                Text(sign, "Sub", sub, new Vector3(0f, -size.y * 0.32f, depth / 2f + 0.012f), new Vector2(size.x * 0.85f, size.y * 0.24f),
                    textColor, SignTextMaterial);
            }

            return sign;
        }

        /// <summary>World-space TMP text centred at <paramref name="position"/>, readable from local +Z.</summary>
        public TextMeshPro Text(Transform parent, string name, string text, Vector3 position, Vector2 box, Color color, Material material,
            Vector3? euler = null)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = Quaternion.Euler(euler ?? new Vector3(0f, 180f, 0f)); // TMP faces -Z; flip to face +Z
            TextMeshPro tmp = go.AddComponent<TextMeshPro>();
            tmp.font = Font;
            tmp.fontSharedMaterial = material;
            tmp.text = text;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 0.1f;
            tmp.fontSizeMax = 60f;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.lineSpacing = -20f;
            tmp.rectTransform.sizeDelta = box;
            tmp.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            return tmp;
        }
    }
}
