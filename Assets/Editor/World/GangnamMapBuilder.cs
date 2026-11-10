using System.Collections.Generic;
using System.IO;
using System.Linq;
using Contract.Core;
using Contract.Interaction;
using Contract.Map;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static Contract.Editor.World.UIKit;

namespace Contract.Editor.World
{
    /// <summary>
    /// Stage 5: stylised map geometry (Minimap layer) generated from the built district, street /
    /// landmark labels, place icons, the MapUI prefab (minimap + full map) and scene wiring.
    /// Run after the district and population builders. Idempotent.
    /// Headless: -executeMethod Contract.Editor.World.GangnamMapBuilder.Build
    /// </summary>
    public static class GangnamMapBuilder
    {
        private const string ScenePath = "Assets/Scenes/GameWorld.unity";
        private const string MapRoot = "Gangnam_Map";
        private const string MaterialFolder = "Assets/Art/KoreanEnvironment/Materials/Map";
        private const string MapUIPrefabPath = "Assets/Prefabs/UI/MapUI.prefab";
        private const string InputReaderPath = "Assets/Settings/InputReader.asset";
        private const int MapLayer = 13;
        private const float Height = 300f;

        // Dark map palette with red accents (matches the UI direction: charcoal / red / white).
        internal static readonly Color Block = new Color(0.106f, 0.122f, 0.149f);
        internal static readonly Color Road = new Color(0.17f, 0.19f, 0.225f);
        internal static readonly Color Alley = new Color(0.14f, 0.155f, 0.185f);
        internal static readonly Color Marking = new Color(0.36f, 0.39f, 0.44f);
        internal static readonly Color CentreLine = new Color(0.5f, 0.38f, 0.12f);
        internal static readonly Color BuildingFill = new Color(0.205f, 0.228f, 0.272f);
        internal static readonly Color BuildingEdge = new Color(0.31f, 0.34f, 0.4f);
        internal static readonly Color MissionFill = new Color(0.25f, 0.11f, 0.135f);
        internal static readonly Color MissionEdge = new Color(0.78f, 0.22f, 0.28f);

        private static readonly Dictionary<Color, Material> materials = new Dictionary<Color, Material>();
        private static Mesh quad;

        /// <summary>Shape / material state for map builders of any district.</summary>
        internal static void PrepareShapes()
        {
            Directory.CreateDirectory(MaterialFolder);
            materials.Clear();
            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad = temp.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(temp);
        }

        [MenuItem("CONTRACT/World/Build Gangnam Map (Minimap + Full Map)")]
        public static void Build()
        {
            Directory.CreateDirectory(MaterialFolder);
            materials.Clear();
            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad = temp.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(temp);

            GameObject mapUIPrefab = BuildMapUIPrefab();

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject old = GameObject.Find(MapRoot);
            if (old != null) Object.DestroyImmediate(old);
            Transform root = new GameObject(MapRoot).transform;

            BuildGeometry(EnvKit.Group(root, "Geometry"));
            BuildLabels(EnvKit.Group(root, "Labels"));
            AddPlaceIcons();
            WireScene(mapUIPrefab);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[GangnamMapBuilder] Done: {root.Find("Geometry").childCount} map shapes, {root.Find("Labels").childCount} labels, " +
                      $"{Object.FindObjectsByType<MapIcon>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length} map icons in the scene.");
        }

        // =====================================================================================
        // Geometry (flat unlit quads on the Minimap layer, captured once by MapManager)
        // =====================================================================================

        internal static void Shape(Transform parent, string name, Rect r, Color color, int order)
        {
            GameObject go = new GameObject(name) { layer = MapLayer };
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(new Vector3(r.center.x, Height + order * 0.01f, r.center.y), Quaternion.Euler(90f, 0f, 0f));
            go.transform.localScale = new Vector3(r.width, r.height, 1f);
            go.AddComponent<MeshFilter>().sharedMesh = quad;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = Unlit(color);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            GameObjectUtility.SetStaticEditorFlags(go, 0);
        }

        internal static Material Unlit(Color color)
        {
            if (materials.TryGetValue(color, out Material m)) return m;
            string path = $"{MaterialFolder}/Map_{ColorUtility.ToHtmlStringRGB(color)}.mat";
            m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(m, path);
            }

            m.SetColor("_BaseColor", color);
            EditorUtility.SetDirty(m);
            materials[color] = m;
            return m;
        }

        private static void BuildGeometry(Transform g)
        {
            // City blocks (pavements + plazas) and the road cross.
            Shape(g, "Roads", Rect.MinMaxRect(-90f, -75f, 90f, 100f), Road, 0);
            foreach (Rect block in new[] { Rect.MinMaxRect(8f, 7f, 85f, 95f), Rect.MinMaxRect(-85f, 7f, -8f, 95f), Rect.MinMaxRect(8f, -70f, 85f, -7f), Rect.MinMaxRect(-85f, -70f, -8f, -7f) })
            {
                Shape(g, "Block", block, Block, 1);
            }

            Shape(g, "FoodAlley", Rect.MinMaxRect(15f, 38f, 85f, 46f), Alley, 2);

            // Road markings: yellow centre lines and zebra crossings (simplified for legibility).
            Shape(g, "CentreBoulevardN", Rect.MinMaxRect(-0.35f, 14.5f, 0.35f, 95f), CentreLine, 2);
            Shape(g, "CentreBoulevardS", Rect.MinMaxRect(-0.35f, -70f, 0.35f, -14.5f), CentreLine, 2);
            Shape(g, "CentreCrossE", Rect.MinMaxRect(14.5f, -0.35f, 85f, 0.35f), CentreLine, 2);
            Shape(g, "CentreCrossW", Rect.MinMaxRect(-85f, -0.35f, -14.5f, 0.35f), CentreLine, 2);
            foreach (float zc in new[] { 10.5f, -10.5f })
                for (float x = -7.25f; x <= 7.3f; x += 1.5f) Shape(g, "Zebra", new Rect(x - 0.4f, zc - 2f, 0.8f, 4f), Marking, 2);
            foreach (float xc in new[] { 10.5f, -10.5f })
                for (float z = -6.25f; z <= 6.3f; z += 1.5f) Shape(g, "Zebra", new Rect(xc - 2f, z - 0.4f, 4f, 0.8f), Marking, 2);

            // Sunken entrances.
            Shape(g, "Subway", Rect.MinMaxRect(24f, 9f, 31f, 12f), new Color(0.1f, 0.35f, 0.2f), 3);
            Shape(g, "Parking", Rect.MinMaxRect(-44.5f, -47f, -38.5f, -30f), new Color(0.12f, 0.22f, 0.36f), 3);

            // Building footprints from the real colliders (outline + fill). Mission buildings in red.
            var missionBuildings = new HashSet<string> { "MiraeTower", "HangyeolGroup" };
            foreach (Transform building in Children("District_Gangnam/Buildings").Concat(Children("District_Gangnam/FoodAlley")))
            {
                Bounds? footprint = Footprint(building);
                if (footprint == null) continue;
                Bounds b = footprint.Value;
                Rect r = Rect.MinMaxRect(b.min.x, b.min.z, b.max.x, b.max.z);
                bool mission = missionBuildings.Contains(building.name);
                Shape(g, building.name + "_Edge", Inflate(r, 0.6f), mission ? MissionEdge : BuildingEdge, 4);
                Shape(g, building.name, r, mission ? MissionFill : BuildingFill, 5);
            }
        }

        private static IEnumerable<Transform> Children(string path)
        {
            GameObject go = GameObject.Find(path);
            return go == null ? Enumerable.Empty<Transform>() : go.transform.Cast<Transform>();
        }

        internal static Bounds? Footprint(Transform building)
        {
            Bounds? result = null;
            foreach (BoxCollider box in building.GetComponentsInChildren<BoxCollider>())
            {
                Bounds b = box.bounds;
                if (b.size.y < 2f || b.size.x * b.size.z < 6f) continue; // walls / bodies only, not furniture
                if (result == null) result = b;
                else
                {
                    Bounds merged = result.Value;
                    merged.Encapsulate(b);
                    result = merged;
                }
            }

            return result;
        }

        internal static Rect Inflate(Rect r, float by) => Rect.MinMaxRect(r.xMin - by, r.yMin - by, r.xMax + by, r.yMax + by);

        // =====================================================================================
        // Labels and place icons
        // =====================================================================================

        internal static void Label(Transform parent, string ko, string en, Vector3 position, int detail, float angle, float size)
        {
            GameObject go = new GameObject("Label_" + en);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            go.AddComponent<MapLabel>().Configure(ko, en, detail, angle, size);
        }

        private static void BuildLabels(Transform l)
        {
            Label(l, "강남", "GANGNAM", new Vector3(50f, 0f, -48f), 0, 0f, 3.2f);
            Label(l, "강남대로", "Gangnam-daero", new Vector3(0f, 0f, 62f), 0, 90f, 1.1f);
            Label(l, "강남대로", "Gangnam-daero", new Vector3(0f, 0f, -45f), 0, 90f, 1.1f);
            Label(l, "테헤란로", "Teheran-ro", new Vector3(52f, 0f, 0f), 0, 0f, 1.1f);
            Label(l, "테헤란로", "Teheran-ro", new Vector3(-52f, 0f, 0f), 0, 0f, 1.1f);
            Label(l, "강남 먹자골목", "Food Street", new Vector3(50f, 0f, 42f), 1, 0f, 0.9f);
            Label(l, "강남역", "Gangnam Stn · Exit 10", new Vector3(27.5f, 0f, 14.5f), 1, 0f, 0.8f);
            Label(l, "미래 타워", "Mirae Tower", new Vector3(26f, 0f, 25f), 1, 0f, 0.85f);
            Label(l, "한결 그룹", "Hangyeol Group", new Vector3(25f, 0f, 73f), 1, 0f, 0.85f);
            Label(l, "미래 호텔", "Mirae Hotel", new Vector3(-25f, 0f, 48f), 1, 0f, 0.85f);
            Label(l, "지하주차장", "Underground parking", new Vector3(-41.5f, 0f, -27f), 2, 0f, 0.7f);
            Label(l, "누리 증권", "Nuri Securities", new Vector3(27f, 0f, -25.5f), 2, 0f, 0.7f);
            Label(l, "누리 생명", "Nuri Life", new Vector3(70f, 0f, 78f), 2, 0f, 0.7f);
        }

        private static void AddPlaceIcons()
        {
            void Icon(GameObject go, string id, MapIconType type, string label, bool clamp = false)
            {
                if (go == null) return;
                MapIcon icon = go.GetComponent<MapIcon>();
                if (icon == null) icon = go.AddComponent<MapIcon>();
                icon.Configure(id, type, label, null, null, clamp);
            }

            Icon(GameObject.Find("GameplayStructures/Safehouse"), "safehouse_downtown", MapIconType.Safehouse, "안가 · Safehouse", true);
            Icon(Object.FindAnyObjectByType<ExtractionInteractable>()?.gameObject, "extraction_downtown", MapIconType.ExtractionPoint, "탈출 지점 · Extraction (지하주차장)", true);
            Icon(GameObject.Find("KeycardPickup"), "keycard", MapIconType.Collectible, "키카드 · Keycard");
            Icon(GameObject.Find("EvidencePickup"), "evidence", MapIconType.Collectible, "블랙박스 · Black box");

            Transform places = EnvKit.Group(GameObject.Find(MapRoot).transform, "Places");
            void Place(string id, string label, Vector3 position)
            {
                GameObject go = new GameObject("Place_" + id);
                go.transform.SetParent(places, false);
                go.transform.position = position;
                go.AddComponent<MapIcon>().Configure(id, MapIconType.PointOfInterest, label);
            }

            Place("subway_gangnam", "강남역 2호선 · Gangnam Station", new Vector3(23f, 0.15f, 10.5f));
            Place("bus_stop_e", "정류장 · Bus stop", new Vector3(12.4f, 0.15f, -38f));
            Place("bus_stop_w", "정류장 · Bus stop", new Vector3(-12.4f, 0.15f, 36f));
            Place("food_alley", "강남 먹자골목 · Food Street", new Vector3(16f, 0.15f, 42f));
        }

        // =====================================================================================
        // MapUI prefab: circular minimap (top-right) + full-screen map
        // =====================================================================================

        private static GameObject BuildMapUIPrefab()
        {
            InputReader input = AssetDatabase.LoadAssetAtPath<InputReader>(InputReaderPath);
            GameObject root = new GameObject("MapUI", typeof(RectTransform));
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand; // never smaller than 1920x1080: no overflow on 20:9 phones or 16:10 screens
            root.AddComponent<GraphicRaycaster>();
            root.AddComponent<Contract.UI.SafeAreaFitter>();

            // ---- Minimap -----------------------------------------------------------------------
            RectTransform mini = Node(root.transform, "Minimap", Vector2.one, Vector2.one, Vector2.one, new Vector2(-40f, -40f), new Vector2(290f, 290f));
            RectTransform backing = Node(mini, "Backing", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300f, 300f));
            Image backingImage = Img(backing, new Color(0f, 0f, 0f, 0.6f));
            RectTransform viewport = Stretch(mini, "Viewport");
            Img(viewport, Color.white);
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            RectTransform miniMap = Node(viewport, "MapImage", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(100f, 100f));
            RawImage miniRaw = miniMap.gameObject.AddComponent<RawImage>();
            miniRaw.raycastTarget = false;
            RectTransform miniMarkers = Stretch(viewport, "Markers");
            RectTransform ring = Node(mini, "Ring", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(304f, 304f));
            Image ringImage = Img(ring, new Color(1f, 1f, 1f, 0.85f));
            RectTransform arrow = Node(mini, "PlayerArrow", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(24f, 24f));
            Image arrowImage = Img(arrow, Color.white);
            TextMeshProUGUI north = Text(mini, "North", "N", 18f, Accent, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(24f, 24f), true);
            TextMeshProUGUI region = Text(mini, "Region", "강남 · GANGNAM", 17f, TextColor, TextAlignmentOptions.Center, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -14f), new Vector2(290f, 26f), true);

            MinimapView minimap = mini.gameObject.AddComponent<MinimapView>();
            SetRefs(minimap, ("viewport", viewport), ("mapImage", miniRaw), ("markerLayer", miniMarkers), ("playerArrow", arrowImage),
                ("northMarker", north.rectTransform), ("regionLabel", region), ("ring", ringImage), ("backing", backingImage));

            // ---- Full map ------------------------------------------------------------------------
            RectTransform panel = Stretch(root.transform, "FullMap");
            Img(panel, new Color(0.025f, 0.028f, 0.035f, 0.97f), true); // blocks clicks to the HUD underneath
            RectTransform header = Node(panel, "Header", new Vector2(0f, 1f), Vector2.one, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 84f));
            Img(header, Panel);
            Node(header, "AccentLine", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 3f)).gameObject.AddComponent<Image>().color = Accent;
            Text(header, "Title", "지도  <size=60%><color=#9AA3AE>MAP</color></size>", 34f, TextColor, TextAlignmentOptions.Left, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(40f, 0f), new Vector2(300f, 60f), true);
            TextMeshProUGUI regionTitle = Text(header, "RegionTitle", "강남", 26f, Accent, TextAlignmentOptions.Left, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(240f, 0f), new Vector2(500f, 60f), true);
            Button close = ButtonAt(header, "CloseButton", "닫기 · Close   [M / ESC]", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-40f, 0f), new Vector2(300f, 46f), true);

            RectTransform area = Stretch(panel, "MapArea", 40f, 400f, 110f, 70f);
            Img(area, new Color(0.055f, 0.063f, 0.08f, 1f), true);
            area.gameObject.AddComponent<RectMask2D>();
            RectTransform fullMapImage = Node(area, "MapImage", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(100f, 100f));
            RawImage fullRaw = fullMapImage.gameObject.AddComponent<RawImage>();
            fullRaw.raycastTarget = false;
            RectTransform labels = Stretch(area, "Labels");
            RectTransform markers = Stretch(area, "Markers");
            RectTransform player = Node(markers, "Player", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30f, 30f));
            Image playerImage = Img(player, Color.white);
            TextMeshProUGUI tooltip = Text(markers, "Tooltip", "", 16f, TextColor, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(420f, 26f), true);
            tooltip.gameObject.SetActive(false);
            TextMeshProUGUI hints = Text(panel, "Hints", "드래그 · WASD  이동      휠 · D-pad  확대/축소      E  내 위치      M / ESC  닫기",
                15f, Muted, TextAlignmentOptions.Left, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(40f, 24f), new Vector2(1100f, 26f));

            // Side panel: legend, filters, zoom, recenter, coordinates.
            RectTransform side = Node(panel, "SidePanel", new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(-40f, -20f), new Vector2(330f, -180f));
            Img(side, Panel);
            VerticalLayoutGroup v = side.gameObject.AddComponent<VerticalLayoutGroup>();
            v.padding = new RectOffset(20, 20, 18, 18);
            v.spacing = 6f;
            v.childControlWidth = true;
            v.childControlHeight = false;
            v.childForceExpandHeight = false;

            TextMeshProUGUI Section(string ko, string en)
            {
                TextMeshProUGUI t = Text(side, "Title_" + en, $"{ko}  <size=70%><color=#9AA3AE>{en}</color></size>", 19f, Accent, TextAlignmentOptions.Left,
                    new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(290f, 30f), true);
                t.gameObject.AddComponent<LayoutElement>().preferredHeight = 32f;
                return t;
            }

            Section("범례", "LEGEND");
            RectTransform legend = Node(side, "Legend", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(290f, 250f));
            legend.gameObject.AddComponent<LayoutElement>().preferredHeight = 252f;
            VerticalLayoutGroup lv = legend.gameObject.AddComponent<VerticalLayoutGroup>();
            lv.spacing = 1f;
            lv.childControlHeight = false;
            lv.childControlWidth = false;
            lv.childForceExpandHeight = false;

            Section("필터", "FILTERS");
            Toggle[] filters =
            {
                ToggleRow(side, "Filter_Objectives", "목표 · Objectives"),
                ToggleRow(side, "Filter_Characters", "임무 인물 · Mission characters"),
                ToggleRow(side, "Filter_Civilians", "시민 · Civilians"),
                ToggleRow(side, "Filter_Security", "경비원 · Security"),
                ToggleRow(side, "Filter_Collectibles", "수집품 · Collectibles"),
                ToggleRow(side, "Filter_Places", "장소 · Places")
            };
            Toggle activeOnly = ToggleRow(side, "Filter_ActiveMission", "진행 중인 임무만 · Active mission only");
            activeOnly.isOn = false;

            Section("확대", "ZOOM");
            RectTransform zoomRow = Node(side, "ZoomRow", new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(290f, 40f));
            zoomRow.gameObject.AddComponent<LayoutElement>().preferredHeight = 40f;
            Button zoomOut = ButtonAt(zoomRow, "ZoomOut", "−", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(40f, 36f));
            Button zoomIn = ButtonAt(zoomRow, "ZoomIn", "+", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(208f, 0f), new Vector2(40f, 36f));
            Slider slider = BuildSlider(zoomRow);
            TextMeshProUGUI zoomLabel = Text(zoomRow, "ZoomLabel", "x2.0", 16f, Muted, TextAlignmentOptions.Right, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(40f, 30f));

            Button recenter = ButtonAt(side, "Recenter", "내 위치 · Recenter   [E]", new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(290f, 44f), true);
            recenter.gameObject.AddComponent<LayoutElement>().preferredHeight = 46f;
            TextMeshProUGUI coords = Text(side, "Coordinates", "X 0   Z 0", 16f, Muted, TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero, new Vector2(290f, 26f));
            coords.gameObject.AddComponent<LayoutElement>().preferredHeight = 28f;

            FullMapView full = root.AddComponent<FullMapView>();
            SetRefs(full, ("panel", panel.gameObject), ("mapArea", area), ("mapImage", fullRaw), ("labelLayer", labels), ("markerLayer", markers),
                ("playerMarker", playerImage), ("regionTitle", regionTitle), ("coordinates", coords), ("tooltip", tooltip), ("zoomLabel", zoomLabel),
                ("legendContainer", legend), ("closeButton", close), ("recenterButton", recenter), ("zoomInButton", zoomIn), ("zoomOutButton", zoomOut),
                ("zoomSlider", slider), ("activeMissionOnly", activeOnly), ("inputReader", input));
            SerializedObject so = new SerializedObject(full);
            SerializedProperty toggles = so.FindProperty("filterToggles");
            toggles.arraySize = filters.Length;
            for (int i = 0; i < filters.Length; i++) toggles.GetArrayElementAtIndex(i).objectReferenceValue = filters[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            Directory.CreateDirectory(Path.GetDirectoryName(MapUIPrefabPath));
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, MapUIPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // =====================================================================================
        // Scene wiring and cleanup of the old map
        // =====================================================================================

        private static void WireScene(GameObject mapUIPrefab)
        {
            // Old, non-functional map pieces.
            foreach (string path in new[] { "UI Canvas/Full Map Panel", "UI Canvas/HUD Panel/Minimap Panel", "[MapManager]/FullMapCamera", "[MapManager]/MinimapCamera" })
            {
                GameObject old = GameObject.Find(path);
                if (old != null) Object.DestroyImmediate(old);
            }

            GameObject existingUI = GameObject.Find("MapUI");
            if (existingUI != null) Object.DestroyImmediate(existingUI);
            PrefabUtility.InstantiatePrefab(mapUIPrefab);

            MapManager manager = Object.FindAnyObjectByType<MapManager>();
            if (manager == null) manager = new GameObject("[MapManager]").AddComponent<MapManager>();
            // A component from an earlier stage whose script was deleted ("referenced script is missing" in the player log).
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(manager.gameObject);
            SetRefs(manager, ("inputReader", AssetDatabase.LoadAssetAtPath<InputReader>(InputReaderPath)));

            // Main camera never draws the map geometry floating above the city.
            Camera main = Camera.main;
            if (main != null) main.cullingMask &= ~(1 << MapLayer);
            foreach (ReflectionProbe probe in Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None))
            {
                probe.cullingMask &= ~(1 << MapLayer);
            }
        }
    }
}
