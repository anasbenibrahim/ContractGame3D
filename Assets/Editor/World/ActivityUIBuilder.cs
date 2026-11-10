using Contract.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static Contract.Editor.World.UIKit;

namespace Contract.Editor.World
{
    /// <summary>
    /// GPS Activity UI: the REAL GPS ACTIVITY dashboard, the GPS access / permission screen and the
    /// city HUD strip, saved as Assets/Prefabs/UI/ActivityUI.prefab and placed in the main menu
    /// (opened by GPS ACTIVITY) and in GameWorld. Run after MenuScenesBuilder.
    /// Headless: -executeMethod Contract.Editor.World.ActivityUIBuilder.Build
    /// </summary>
    public static class ActivityUIBuilder
    {
        public const string PrefabPath = "Assets/Prefabs/UI/ActivityUI.prefab";
        private static readonly Vector2 TopLeft = new Vector2(0f, 1f), Top = new Vector2(0.5f, 1f), Centre = new Vector2(0.5f, 0.5f);

        [MenuItem("CONTRACT/World/Build Activity UI")]
        public static void Build()
        {
            GameObject prefab = BuildPrefab();
            Place("Assets/Scenes/MainMenu.unity", prefab, mainMenu: true);
            Place("Assets/Scenes/GameWorld.unity", prefab, mainMenu: false);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MyeongdongBuilder.ScenePath) != null) Place(MyeongdongBuilder.ScenePath, prefab, mainMenu: false);
            Debug.Log("[ActivityUIBuilder] Done.");
        }

        // =====================================================================================

        private static GameObject BuildPrefab()
        {
            Sprite white = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/White.png");
            var root = new GameObject("ActivityUI", typeof(RectTransform));
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

            // ---- HUD strip (city) -----------------------------------------------------------------
            // Bottom centre: the top centre belongs to mission toasts and the detection meter.
            Vector2 bottom = new Vector2(0.5f, 0f);
            RectTransform strip = Node(root.transform, "HudStrip", bottom, bottom, bottom, new Vector2(0f, 28f), new Vector2(620f, 64f));
            Image stripBg = Img(strip, new Color(0.04f, 0.045f, 0.055f, 0.85f), true);
            Button stripButton = strip.gameObject.AddComponent<Button>();
            stripButton.targetGraphic = stripBg;
            Image stripLight = Img(Node(strip, "Light", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(14f, 14f)), Muted);
            TextMeshProUGUI stripLabel = Text(strip, "Label", "", 20f, TextColor, TextAlignmentOptions.Left, Vector2.zero, Vector2.one, Centre, new Vector2(24f, 0f), new Vector2(-64f, 0f));

            // ---- Dashboard -------------------------------------------------------------------------
            RectTransform shade = Stretch(root.transform, "Dashboard");
            Img(shade, new Color(0.01f, 0.012f, 0.018f, 0.9f), true);
            RectTransform card = Node(shade, "Card", Centre, Centre, Centre, Vector2.zero, new Vector2(1800f, 1000f));
            Img(card, Panel, true);
            Node(card, "Accent", TopLeft, Vector2.one, Top, Vector2.zero, new Vector2(0f, 5f)).gameObject.AddComponent<Image>().color = Accent;
            Animate(card);
            Text(card, "Title", "실제 GPS 활동  <size=55%><color=#9AA3AE>REAL GPS ACTIVITY</color></size>", 40f, TextColor, TextAlignmentOptions.Left, TopLeft, TopLeft, TopLeft, new Vector2(48f, -28f), new Vector2(1200f, 60f), true);
            Button close = ButtonAt(card, "Close", "닫기 · Close", Vector2.one, Vector2.one, new Vector2(-40f, -24f), new Vector2(240f, 80f));

            // Left column: mode, status, stats, coordinates, controls.
            TextMeshProUGUI modeLabel = Text(card, "Mode", "", 26f, TextColor, TextAlignmentOptions.Left, TopLeft, TopLeft, TopLeft, new Vector2(48f, -112f), new Vector2(420f, 44f), true);
            Button modeButton = ButtonAt(card, "ModeSwitch", "", TopLeft, TopLeft, new Vector2(480f, -104f), new Vector2(380f, 80f));
            TextMeshProUGUI modeHint = Wrap(Text(card, "ModeHint", "", 17f, Muted, TextAlignmentOptions.TopLeft, TopLeft, TopLeft, TopLeft, new Vector2(48f, -196f), new Vector2(820f, 50f)));
            Image light = Img(Node(card, "StatusLight", TopLeft, TopLeft, TopLeft, new Vector2(48f, -268f), new Vector2(18f, 18f)), Muted);
            TextMeshProUGUI status = Text(card, "Status", "", 22f, TextColor, TextAlignmentOptions.Left, TopLeft, TopLeft, TopLeft, new Vector2(80f, -256f), new Vector2(790f, 40f), true);

            TextMeshProUGUI Tile(string name, string label, int column, int row)
            {
                RectTransform tile = Node(card, name, TopLeft, TopLeft, TopLeft, new Vector2(48f + column * 278f, -320f - row * 150f), new Vector2(264f, 136f));
                Img(tile, PanelLight);
                Text(tile, "Label", label, 16f, Muted, TextAlignmentOptions.Left, TopLeft, TopLeft, TopLeft, new Vector2(16f, -12f), new Vector2(240f, 26f));
                return Text(tile, "Value", "", 40f, TextColor, TextAlignmentOptions.Left, TopLeft, TopLeft, TopLeft, new Vector2(16f, -48f), new Vector2(240f, 72f), true);
            }

            TextMeshProUGUI real = Tile("RealDistance", "실제 거리 · Real distance", 0, 0);
            TextMeshProUGUI virt = Tile("VirtualDistance", "가상 거리 · Virtual distance", 1, 0);
            TextMeshProUGUI steps = Tile("Steps", "걸음 · Steps", 2, 0);
            TextMeshProUGUI time = Tile("SessionTime", "시간 · Session time", 0, 1);
            TextMeshProUGUI speed = Tile("Speed", "속도 · Speed", 1, 1);
            TextMeshProUGUI xp = Tile("Xp", "경험치 · XP earned", 2, 1);
            TextMeshProUGUI coords = Text(card, "Coordinates", "", 17f, Muted, TextAlignmentOptions.Left, TopLeft, TopLeft, TopLeft, new Vector2(48f, -632f), new Vector2(520f, 30f));
            Button coordsButton = ButtonAt(card, "ShowPosition", "위치 보기 · Show / hide", TopLeft, TopLeft, new Vector2(580f, -624f), new Vector2(290f, 56f));

            Button primary = ButtonAt(card, "Primary", "", Vector2.zero, Vector2.zero, new Vector2(48f, 120f), new Vector2(420f, 110f), true);
            Button stop = ButtonAt(card, "Stop", "종료 · STOP", Vector2.zero, Vector2.zero, new Vector2(484f, 120f), new Vector2(200f, 110f));
            Button mark = ButtonAt(card, "MarkSpot", "지점 표시 · MARK SPOT", Vector2.zero, Vector2.zero, new Vector2(700f, 120f), new Vector2(300f, 110f));

            // Right column: mission progress, mission list, checkpoint, collectibles.
            float rx = 960f;
            Text(card, "MissionTitle", "현재 GPS 임무  <size=75%><color=#9AA3AE>CURRENT GPS MISSION</color></size>", 18f, Accent, TextAlignmentOptions.Left, TopLeft, TopLeft, TopLeft, new Vector2(rx, -112f), new Vector2(780f, 30f), true);
            TextMeshProUGUI mission = Text(card, "Mission", "", 24f, TextColor, TextAlignmentOptions.Left, TopLeft, TopLeft, TopLeft, new Vector2(rx, -150f), new Vector2(790f, 40f), true);
            RectTransform bar = Node(card, "MissionBar", TopLeft, TopLeft, TopLeft, new Vector2(rx, -204f), new Vector2(790f, 16f));
            Img(bar, PanelLight);
            Image fill = Img(Stretch(bar, "Fill"), Accent);
            fill.sprite = white;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            TextMeshProUGUI list = Text(card, "MissionList", "", 19f, TextColor, TextAlignmentOptions.TopLeft, TopLeft, TopLeft, TopLeft, new Vector2(rx, -244f), new Vector2(790f, 230f));
            list.lineSpacing = 10f;
            TextMeshProUGUI checkpoint = Wrap(Text(card, "Checkpoint", "", 19f, TextColor, TextAlignmentOptions.TopLeft, TopLeft, TopLeft, TopLeft, new Vector2(rx, -500f), new Vector2(790f, 80f)));
            TextMeshProUGUI collectibles = Text(card, "Collectibles", "", 19f, TextColor, TextAlignmentOptions.Left, TopLeft, TopLeft, TopLeft, new Vector2(rx, -600f), new Vector2(470f, 40f));
            Button collect = ButtonAt(card, "Collect", "수집 · COLLECT", TopLeft, TopLeft, new Vector2(rx + 480f, -592f), new Vector2(310f, 80f), true);

            Button map = ButtonAt(card, "ViewMap", "", Vector2.right, Vector2.right, new Vector2(-380f, 120f), new Vector2(320f, 110f));
            Button setup = ButtonAt(card, "GpsSetup", "GPS 설정 · GPS SETUP", Vector2.right, Vector2.right, new Vector2(-40f, 120f), new Vector2(320f, 110f));
            TextMeshProUGUI banner = Wrap(Text(card, "Banner", "", 17f, new Color(1f, 0.82f, 0.4f), TextAlignmentOptions.Left, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(48f, 36f), new Vector2(1700f, 56f)));

            // ---- GPS access screen (permissions, device state, live check) -------------------------------
            LocationAccessView access = AccessPanel(root.transform, setup, shade.gameObject);

            var dashboard = root.AddComponent<ActivityDashboardView>();
            SetRefs(dashboard, ("panel", shade.gameObject), ("closeButton", close), ("modeLabel", modeLabel), ("modeButton", modeButton),
                ("modeButtonLabel", modeButton.GetComponentInChildren<TextMeshProUGUI>()), ("modeHint", modeHint), ("statusLight", light), ("statusText", status),
                ("bannerText", banner), ("realDistance", real), ("virtualDistance", virt), ("steps", steps), ("sessionTime", time), ("speed", speed),
                ("xpEarned", xp), ("coordinates", coords), ("missionText", mission), ("missionFill", fill), ("checkpointText", checkpoint),
                ("collectiblesText", collectibles), ("missionList", list), ("primaryButton", primary), ("primaryLabel", primary.GetComponentInChildren<TextMeshProUGUI>()),
                ("stopButton", stop), ("markButton", mark), ("collectButton", collect), ("mapButton", map), ("mapLabel", map.GetComponentInChildren<TextMeshProUGUI>()),
                ("setupButton", setup), ("coordinatesButton", coordsButton), ("access", access));

            var hud = strip.gameObject.AddComponent<ActivityHudStrip>();
            SetRefs(hud, ("button", stripButton), ("label", stripLabel), ("statusLight", stripLight), ("dashboard", dashboard));

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return saved;
        }

        private static TextMeshProUGUI Wrap(TextMeshProUGUI t)
        {
            t.textWrappingMode = TextWrappingModes.Normal;
            return t;
        }

        /// <summary>GPS access screen: permissions explained, device states handled, live GPS check.</summary>
        private static LocationAccessView AccessPanel(Transform canvas, Button openButton, GameObject returnTo)
        {
            RectTransform shade = Stretch(canvas, "GpsAccess");
            Img(shade, new Color(0f, 0f, 0f, 0.72f), true);
            DrawAbove(shade, MenuScenesBuilder.OverlayOrder + 10); // opened from Settings: above it
            RectTransform card = Node(shade, "Card", Centre, Centre, Centre, Vector2.zero, new Vector2(980f, 760f));
            Img(card, Panel, true);
            Node(card, "Accent", TopLeft, Vector2.one, Top, Vector2.zero, new Vector2(0f, 4f)).gameObject.AddComponent<Image>().color = Accent;
            Animate(card);
            Text(card, "Title", "GPS 설정  <size=60%><color=#9AA3AE>GPS SETUP</color></size>", 38f, TextColor, TextAlignmentOptions.Left, TopLeft, TopLeft, TopLeft, new Vector2(40f, -28f), new Vector2(880f, 54f), true);
            Text(card, "Intro", "Walk in the real world to progress in Seoul. Location access first:", 18f, Muted, TextAlignmentOptions.Left, TopLeft, TopLeft, TopLeft, new Vector2(40f, -88f), new Vector2(880f, 28f));
            Image light = Img(Node(card, "StatusLight", TopLeft, TopLeft, TopLeft, new Vector2(40f, -146f), new Vector2(18f, 18f)), Muted);
            TextMeshProUGUI title = Text(card, "StatusTitle", "", 26f, TextColor, TextAlignmentOptions.Left, TopLeft, TopLeft, TopLeft, new Vector2(72f, -134f), new Vector2(860f, 40f), true);
            TextMeshProUGUI body = Wrap(Text(card, "StatusBody", "", 20f, TextColor, TextAlignmentOptions.TopLeft, TopLeft, TopLeft, TopLeft, new Vector2(40f, -192f), new Vector2(900f, 290f)));
            Node(card, "Divider", TopLeft, TopLeft, TopLeft, new Vector2(40f, -500f), new Vector2(900f, 2f)).gameObject.AddComponent<Image>().color = PanelLight;
            TextMeshProUGUI activity = Wrap(Text(card, "Activity", "", 19f, TextColor, TextAlignmentOptions.Left, TopLeft, TopLeft, TopLeft, new Vector2(40f, -522f), new Vector2(560f, 64f)));
            Button activityButton = ButtonAt(card, "AllowActivity", "", TopLeft, TopLeft, new Vector2(620f, -520f), new Vector2(320f, 72f));
            Button close = ButtonAt(card, "Back", "뒤로 · Back", Vector2.zero, Vector2.zero, new Vector2(40f, 36f), new Vector2(240f, 84f));
            Button stop = ButtonAt(card, "Stop", "중지 · Stop", Vector2.zero, Vector2.zero, new Vector2(300f, 36f), new Vector2(240f, 84f));
            Button primary = ButtonAt(card, "Primary", "", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 36f), new Vector2(380f, 84f), true);

            LocationAccessView view = shade.gameObject.AddComponent<LocationAccessView>();
            SetRefs(view, ("panel", shade.gameObject), ("openButton", openButton), ("returnTo", returnTo), ("statusTitle", title), ("statusBody", body),
                ("statusLight", light), ("primaryButton", primary), ("primaryLabel", primary.GetComponentInChildren<TextMeshProUGUI>()), ("stopButton", stop),
                ("activityText", activity), ("activityButton", activityButton), ("activityLabel", activityButton.GetComponentInChildren<TextMeshProUGUI>()),
                ("closeButton", close));
            return view;
        }

        // =====================================================================================

        private static void Place(string scenePath, GameObject prefab, bool mainMenu)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            GameObject old = GameObject.Find("ActivityUI");
            if (old != null) Object.DestroyImmediate(old);
            // Older builds placed the access screen directly in the main menu.
            GameObject legacyAccess = GameObject.Find("Main Menu UI/GpsAccess");
            if (legacyAccess != null) Object.DestroyImmediate(legacyAccess);

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            var dashboard = instance.GetComponent<ActivityDashboardView>();
            if (mainMenu)
            {
                Button open = GameObject.Find("Main Menu UI/MainPanel/GpsActivity").GetComponent<Button>();
                GameObject mainPanel = GameObject.Find("Main Menu UI/MainPanel");
                SetRefs(dashboard, ("returnTo", mainPanel));
                var so = new SerializedObject(dashboard);
                SerializedProperty buttons = so.FindProperty("openButtons");
                buttons.arraySize = 1;
                buttons.GetArrayElementAtIndex(0).objectReferenceValue = open;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.RecordPrefabInstancePropertyModifications(dashboard);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
