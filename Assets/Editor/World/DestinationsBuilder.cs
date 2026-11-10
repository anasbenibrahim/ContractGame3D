using System.IO;
using System.Linq;
using Contract.Data;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Contract.Editor.World
{
    /// <summary>
    /// South Korea destinations (DestinationData assets) for the World Selection screen. Only
    /// destinations with a built district get a preview image (rendered from that district's
    /// scene); the others show an "in development" placeholder and cannot be unlocked. Unbuilt
    /// districts without game data are listed by name only (plannedDistricts), never as fake regions.
    /// Headless: -executeMethod Contract.Editor.World.DestinationsBuilder.Build  (needs graphics for previews)
    /// </summary>
    public static class DestinationsBuilder
    {
        private const string Folder = "Assets/ScriptableObjects/Destinations";
        private const string PreviewFolder = "Assets/Art/Previews";
        private const string Regions = "Assets/ScriptableObjects/Regions/";

        [MenuItem("CONTRACT/World/Build Destinations")]
        public static void Build()
        {
            Directory.CreateDirectory(Folder);
            Directory.CreateDirectory(PreviewFolder);
            Texture2D seoulPreview = CapturePreview(MyeongdongBuilder.ScenePath, "dest_seoul",
                new Vector3(-30f, 6f, 18f), new Vector3(60f, 14f, 18f));

            Make("dest_01_seoul", "dest_seoul", "Seoul", "서울", 0, true, seoulPreview, new Color(0.9f, 0.16f, 0.22f),
                "The capital: glass towers and neon in Gangnam, street food and cosmetics shops under stacked Hangul signs in Myeongdong, " +
                "hanok alleys in Bukchon and the late-night streets of Hongdae.",
                new[] { "region_downtown", "region_industrial", "region_residential" }, new[] { "Hongdae 홍대" },
                new[]
                {
                    "명동 · Myeongdong: the street-food carts open at dusk — crowds give you cover.",
                    "강남 · Gangnam: guards watch the office lobbies; staff passes open restricted floors.",
                    "Stay out of restricted areas when guards are looking — suspicion builds fast."
                });
            Make("dest_02_daegu", "dest_daegu", "Daegu", "대구", 80000, false, null, new Color(0.95f, 0.55f, 0.2f),
                "Inland city of textile markets and summer heat: Seomun Market's night stalls and the Dongseong-ro shopping streets.",
                new string[0], new[] { "Dongseong-ro 동성로", "Seomun Market 서문시장" }, new string[0]);
            Make("dest_03_incheon", "dest_incheon", "Incheon", "인천", 100000, false, null, new Color(0.2f, 0.6f, 0.9f),
                "Port city on the Yellow Sea: the Chinatown gate and the glass high-rises of Songdo by the water.",
                new string[0], new[] { "Chinatown 차이나타운", "Songdo 송도" }, new string[0]);
            Make("dest_04_gyeongju", "dest_gyeongju", "Gyeongju", "경주", 120000, false, null, new Color(0.75f, 0.6f, 0.35f),
                "The old Silla capital: royal burial mounds, Hwangnidan-gil's hanok cafés and lantern-lit Woljeonggyo bridge.",
                new string[0], new[] { "Hwangnidan-gil 황리단길", "Woljeonggyo 월정교" }, new string[0]);
            Make("dest_05_busan", "dest_busan", "Busan", "부산", 150000, false, null, new Color(0.15f, 0.45f, 0.85f),
                "Korea's harbour city: Haeundae beach towers, the lit-up Gwangan Bridge over Gwangalli, the container docks and the painted hillside of Gamcheon.",
                new[] { "region_luxury", "region_harbor" }, new[] { "Gwangalli 광안리", "Gamcheon 감천문화마을" }, new string[0]);
            Make("dest_06_jeju", "dest_jeju", "Jeju", "제주", 200000, false, null, new Color(0.3f, 0.7f, 0.45f),
                "Volcanic island: basalt stone walls, tangerine orchards and the sunrise crater of Seongsan Ilchulbong.",
                new string[0], new[] { "Seongsan 성산", "Jeju City 제주시" }, new string[0]);

            AssetDatabase.SaveAssets();
            GameDatabaseBuilder.Build();
            Debug.Log("[DestinationsBuilder] Done.");
        }

        private static void Make(string file, string id, string name, string korean, int cost, bool byDefault, Texture2D preview, Color theme,
            string description, string[] regionIds, string[] planned, string[] tips)
        {
            string path = $"{Folder}/{file}.asset";
            DestinationData d = AssetDatabase.LoadAssetAtPath<DestinationData>(path);
            if (d == null)
            {
                d = ScriptableObject.CreateInstance<DestinationData>();
                AssetDatabase.CreateAsset(d, path);
            }

            d.destinationId = id;
            d.displayName = name;
            d.koreanName = korean;
            d.description = description;
            d.unlockCost = cost;
            d.unlockedByDefault = byDefault;
            d.preview = preview;
            d.themeColor = theme;
            d.districts = regionIds.Select(r => AssetDatabase.LoadAssetAtPath<RegionData>(Regions + r + ".asset")).Where(r => r != null).ToArray();
            d.plannedDistricts = planned;
            d.loadingTips = tips;
            EditorUtility.SetDirty(d);
        }

        /// <summary>Renders an aerial view of a built district scene into a PNG preview.</summary>
        private static Texture2D CapturePreview(string scenePath, string name, Vector3 position, Vector3 lookAt)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                Debug.LogWarning($"[DestinationsBuilder] No preview for {name}: scene missing or no graphics device.");
                return AssetDatabase.LoadAssetAtPath<Texture2D>($"{PreviewFolder}/{name}.png");
            }

            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            string file = $"{PreviewFolder}/{name}.png";
            File.WriteAllBytes(file, Render(position, lookAt));
            AssetDatabase.ImportAsset(file);
            var importer = (TextureImporter)AssetImporter.GetAtPath(file);
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 1024;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(file);
        }

        /// <summary>Renders the open scene from a viewpoint (shaders compiled synchronously so nothing renders blank).</summary>
        internal static byte[] Render(Vector3 position, Vector3 lookAt, int w = 1280, int h = 720)
        {
            bool async = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var go = new GameObject("PreviewCamera");
            try
            {
                Camera cam = go.AddComponent<Camera>();
                go.transform.SetPositionAndRotation(position, Quaternion.LookRotation(lookAt - position));
                cam.fieldOfView = 50f;
                cam.farClipPlane = 800f;
                cam.cullingMask = ~(1 << 13); // not the map layer
                cam.clearFlags = CameraClearFlags.Skybox;
                cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
                cam.targetTexture = rt;
                for (int i = 0; i < 3; i++) cam.Render(); // first frames warm up shaders and auto-exposure

                RenderTexture.active = rt;
                var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply();
                byte[] png = tex.EncodeToPNG();
                Object.DestroyImmediate(tex);
                return png;
            }
            finally
            {
                RenderTexture.active = null;
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(rt);
                ShaderUtil.allowAsyncCompilation = async;
            }
        }

        /// <summary>Scratch helper: renders candidate viewpoints of Myeongdong to a folder (-contract-out DIR).</summary>
        internal static void RenderCandidates()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            string dir = args[System.Array.IndexOf(args, "-contract-out") + 1];
            EditorSceneManager.OpenScene(MyeongdongBuilder.ScenePath, OpenSceneMode.Single);
            var views = new (Vector3 p, Vector3 l)[]
            {
                (new Vector3(-55f, 45f, -45f), new Vector3(20f, 0f, 25f)),
                (new Vector3(0f, 9f, -46f), new Vector3(0f, 6f, 30f)),
                (new Vector3(20f, 30f, -20f), new Vector3(60f, 15f, 18f)),
                (new Vector3(-5f, 60f, -110f), new Vector3(10f, 0f, 20f)),
                (new Vector3(-30f, 6f, 18f), new Vector3(60f, 14f, 18f)),
                (new Vector3(3f, 2f, -30f), new Vector3(0f, 4f, 10f))
            };
            for (int i = 0; i < views.Length; i++) File.WriteAllBytes(Path.Combine(dir, $"view{i}.png"), Render(views[i].p, views[i].l));
        }
    }
}
