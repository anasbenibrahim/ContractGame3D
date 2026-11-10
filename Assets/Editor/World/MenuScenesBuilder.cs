using Contract.Core;
using Contract.Interaction;
using Contract.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem.UI;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static Contract.Editor.World.UIKit;

namespace Contract.Editor.World
{
    /// <summary>
    /// Stage 7: rebuilds the Main Menu, Character Selection and Mission Selection scenes and the
    /// GameWorld HUD (interaction prompt, detection meter, operative badge, controls, pause and
    /// settings) in one consistent style. Run after the district, population, map and missions
    /// builders. Idempotent (the three menu scenes are regenerated from scratch).
    /// Headless: -executeMethod Contract.Editor.World.MenuScenesBuilder.Build
    /// </summary>
    public static class MenuScenesBuilder
    {
        private const string FontPath = "Assets/UI/Fonts/NotoSansKR/NotoSansKR SDF.asset";
        private const string LiberationPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
        private const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
        private const string VolumeProfilePath = "Assets/Settings/ContractPostProcess.asset";
        private const string BaseMaterialPath = "Assets/Art/Materials/Player_Suit.mat";
        private static readonly Vector2 Centre = new Vector2(0.5f, 0.5f), TopLeft = new Vector2(0f, 1f), Top = new Vector2(0.5f, 1f);

        private static Sprite white;

        [MenuItem("CONTRACT/World/Build Menus and HUD")]
        public static void Build()
        {
            UseKoreanUIFont();
            white = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/White.png");

            BuildMainMenu();
            BuildCharacterSelection();
            if (AssetDatabase.LoadAssetAtPath<GameObject>(MyeongdongBuilder.PrefabPath) != null) BuildWorldSelection();
            else Debug.LogWarning("[MenuScenesBuilder] World Selection skipped: build Myeongdong first (BuildWorldSelectionScene).");
            BuildMissionSelection();
            BuildGameWorldHud();
            AssetDatabase.SaveAssets();
            Debug.Log("[MenuScenesBuilder] Done.");
        }

        /// <summary>One typeface for Korean and Latin UI text: Noto Sans KR, with Liberation Sans as the symbol fallback.</summary>
        private static void UseKoreanUIFont()
        {
            Object settings = AssetDatabase.LoadAssetAtPath<Object>(TmpSettingsPath);
            TMP_FontAsset noto = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
            TMP_FontAsset liberation = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(LiberationPath);
            SerializedObject so = new SerializedObject(settings);
            so.FindProperty("m_defaultFontAsset").objectReferenceValue = noto;
            SerializedProperty fallbacks = so.FindProperty("m_fallbackFontAssets");
            fallbacks.arraySize = 2;
            fallbacks.GetArrayElementAtIndex(0).objectReferenceValue = noto;
            fallbacks.GetArrayElementAtIndex(1).objectReferenceValue = liberation;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
        }

        // =====================================================================================
        // Shared scene pieces
        // =====================================================================================

        private static Scene NewScene() => EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        private static Camera MainCamera(Vector3 position, Vector3 lookAt)
        {
            GameObject go = new GameObject("Main Camera") { tag = "MainCamera" };
            go.transform.SetPositionAndRotation(position, Quaternion.LookRotation(lookAt - position));
            Camera cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.04f, 0.05f, 0.09f);
            cam.farClipPlane = 600f;
            cam.cullingMask = ~(1 << 13); // never the map layer
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            go.AddComponent<AudioListener>();
            return cam;
        }

        private static void PostProcessing()
        {
            Volume volume = new GameObject("Global Volume").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
        }

        private static void EventSystem()
        {
            GameObject go = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        private static Canvas UICanvas(string name, int order = 0)
        {
            GameObject root = new GameObject(name, typeof(RectTransform));
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand; // never smaller than 1920x1080: no overflow on 20:9 phones or 16:10 screens
            root.AddComponent<GraphicRaycaster>();
            root.AddComponent<SafeAreaFitter>(); // content kept clear of notches / cut-outs
            return canvas;
        }

        private static void DistrictBackdrop()
        {
            GameObject light = new GameObject("Directional Light");
            light.AddComponent<Light>().type = LightType.Directional;
            GameObject district = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(GangnamDistrictBuilder.PrefabPath));
            GangnamDistrictBuilder.ApplyNightLighting(district.transform);
        }

        private static Image Fill(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            RectTransform bg = Node(parent, name, anchor, anchor, pivot, position, size);
            Img(bg, PanelLight);
            Image fill = Img(Stretch(bg, "Fill"), Accent);
            fill.sprite = white;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            return fill;
        }

        private static RectTransform Card(Transform parent, string name, Vector2 size)
        {
            RectTransform card = Node(parent, name, Centre, Centre, Centre, Vector2.zero, size);
            Img(card, Panel, true);
            Node(card, "Accent", TopLeft, Vector2.one, Top, Vector2.zero, new Vector2(0f, 4f)).gameObject.AddComponent<Image>().color = Accent;
            Animate(card);
            return card;
        }

        // ---- touch-sized settings widgets (rows 64 units tall, whole row tappable) ---------------------

        /// <summary>Pause and settings draw above every HUD canvas (HUD 12, mission HUD 15, minimap / GPS strip 20).</summary>
        public const int OverlayOrder = 40;

        private const float RowHeight = 64f, ControlX = 560f, ControlWidth = 440f;

        private static Sprite TouchSprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/UI/Sprites/Touch/{name}.png");

        private static void RowLabel(Transform parent, string label, float y) =>
            Text(parent, label, label, 21f, TextColor, TextAlignmentOptions.Left, TopLeft, TopLeft, TopLeft, new Vector2(40f, y - 14f), new Vector2(500f, 36f));

        private static Slider TouchSlider(Transform parent, string label, float y, out TextMeshProUGUI value)
        {
            RowLabel(parent, label, y);
            RectTransform rt = Node(parent, label + "_Slider", TopLeft, TopLeft, TopLeft, new Vector2(ControlX, y - 8f), new Vector2(ControlWidth - 90f, 48f));
            Img(rt, Color.clear, true); // the whole 48-unit-tall strip takes the finger
            Img(Node(rt, "Track", new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), Centre, Vector2.zero, new Vector2(0f, 10f)), PanelLight);
            RectTransform fillArea = Node(rt, "FillArea", new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), Centre, Vector2.zero, new Vector2(0f, 10f));
            RectTransform fill = Stretch(fillArea, "Fill");
            Img(fill, Accent);
            RectTransform handleArea = Stretch(rt, "HandleArea", 20f, 20f, 4f, 4f); // 40 units tall: a round 40x40 knob
            RectTransform handle = Node(handleArea, "Handle", new Vector2(0f, 0f), new Vector2(0f, 1f), Centre, Vector2.zero, new Vector2(40f, 0f)); // height from the 40-unit area
            Image handleImage = Img(handle, Color.white, true);
            handleImage.sprite = TouchSprite("Circle");
            Slider slider = rt.gameObject.AddComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImage;
            slider.direction = Slider.Direction.LeftToRight;
            value = Text(parent, label + "_Value", "", 19f, Muted, TextAlignmentOptions.Right, TopLeft, TopLeft, TopLeft,
                new Vector2(ControlX + ControlWidth - 80f, y - 14f), new Vector2(80f, 36f));
            return slider;
        }

        private static Slider TouchSlider(Transform parent, string label, float y) => TouchSlider(parent, label, y, out _);

        private static Toggle TouchToggle(Transform parent, string label, float y)
        {
            // SettingsController hides desktop-only rows through the toggle's parent: each toggle gets its own holder.
            RectTransform holder = Node(parent, label + "_Holder", TopLeft, TopLeft, TopLeft, Vector2.zero, Vector2.zero);
            RectTransform row = Node(holder, label + "_Row", TopLeft, TopLeft, TopLeft, new Vector2(24f, y), new Vector2(ControlX + ControlWidth - 24f, RowHeight - 8f));
            Img(row, Color.clear, true); // tap anywhere on the row
            Text(row, "Label", label, 21f, TextColor, TextAlignmentOptions.Left, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(500f, 36f));
            RectTransform box = Node(row, "Box", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-40f, 0f), new Vector2(44f, 44f));
            Image boxImage = Img(box, PanelLight, true);
            Image check = Img(Node(box, "Check", Centre, Centre, Centre, Vector2.zero, new Vector2(26f, 26f)), Accent);
            Toggle toggle = row.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = boxImage;
            toggle.graphic = check;
            toggle.isOn = false;
            return toggle;
        }

        private static TextMeshProUGUI Stepper(Transform parent, string label, float y, out Button previous, out Button next)
        {
            RowLabel(parent, label, y);
            previous = ButtonAt(parent, label + "_Prev", "◀", TopLeft, TopLeft, new Vector2(ControlX, y - 4f), new Vector2(64f, 52f));
            TextMeshProUGUI value = Text(parent, label + "_Value", "", 20f, TextColor, TextAlignmentOptions.Center, TopLeft, TopLeft, TopLeft,
                new Vector2(ControlX + 70f, y - 12f), new Vector2(ControlWidth - 140f, 36f), true);
            next = ButtonAt(parent, label + "_Next", "▶", TopLeft, TopLeft, new Vector2(ControlX + ControlWidth - 64f, y - 4f), new Vector2(64f, 52f));
            return value;
        }

        private static TextMeshProUGUI StatusRow(Transform parent, string label, float y)
        {
            RowLabel(parent, label, y);
            TextMeshProUGUI value = Text(parent, label + "_Status", "", 17f, TextColor, TextAlignmentOptions.Left, TopLeft, TopLeft, TopLeft,
                new Vector2(ControlX - 120f, y - 6f), new Vector2(ControlWidth + 120f, 52f));
            value.textWrappingMode = TextWrappingModes.Normal;
            return value;
        }

        private static float RowY(int i) => -20f - i * RowHeight;

        /// <summary>Tabbed settings card (hidden until opened) shared by the main menu and the pause menu.</summary>
        private static SettingsController SettingsPanel(Transform canvas)
        {
            RectTransform shade = Stretch(canvas, "Settings");
            Img(shade, new Color(0f, 0f, 0f, 0.7f), true);
            DrawAbove(shade, OverlayOrder);
            RectTransform card = Card(shade, "Card", new Vector2(1100f, 860f));
            Text(card, "Title", "설정  <size=60%><color=#9AA3AE>SETTINGS</color></size>", 38f, TextColor, TextAlignmentOptions.Left, TopLeft, TopLeft, TopLeft, new Vector2(40f, -24f), new Vector2(700f, 54f), true);

            string[] tabNames = { "오디오 · AUDIO", "조작 · CONTROLS", "터치 · TOUCH", "그래픽 · GRAPHICS", "GPS" };
            var tabs = new Button[tabNames.Length];
            var pages = new GameObject[tabNames.Length];
            float tabWidth = (1100f - 80f - 4f * 8f) / tabNames.Length;
            for (int i = 0; i < tabNames.Length; i++)
            {
                tabs[i] = ButtonAt(card, "Tab" + i, tabNames[i], TopLeft, TopLeft, new Vector2(40f + i * (tabWidth + 8f), -96f), new Vector2(tabWidth, 60f));
                RectTransform page = Node(card, "Page" + i, TopLeft, TopLeft, TopLeft, new Vector2(0f, -170f), new Vector2(1100f, 600f));
                pages[i] = page.gameObject;
            }

            Transform audio = pages[0].transform, controls = pages[1].transform, touch = pages[2].transform, graphics = pages[3].transform, gps = pages[4].transform;
            Slider master = TouchSlider(audio, "전체 볼륨 · Master volume", RowY(0));
            Slider music = TouchSlider(audio, "음악 · Music", RowY(1));
            Slider sfx = TouchSlider(audio, "효과음 · Effects", RowY(2));

            Slider mouse = TouchSlider(controls, "마우스 감도 · Mouse sensitivity", RowY(0));
            Slider pad = TouchSlider(controls, "패드 감도 · Gamepad sensitivity", RowY(1));
            Slider distance = TouchSlider(controls, "카메라 거리 · Camera distance", RowY(2));
            Toggle invert = TouchToggle(controls, "카메라 상하 반전 · Invert camera Y", RowY(3));

            TextMeshProUGUI touchMode = Stepper(touch, "터치 조작 · Touch controls", RowY(0), out Button touchPrev, out Button touchNext);
            Slider lookX = TouchSlider(touch, "카메라 감도 (좌우) · Camera X", RowY(1));
            Slider lookY = TouchSlider(touch, "카메라 감도 (상하) · Camera Y", RowY(2));
            Slider stickSize = TouchSlider(touch, "조이스틱 크기 · Joystick size", RowY(3));
            Slider deadZone = TouchSlider(touch, "조이스틱 데드존 · Dead zone", RowY(4));
            Slider opacity = TouchSlider(touch, "버튼 투명도 · Button opacity", RowY(5));
            Toggle haptics = TouchToggle(touch, "진동 피드백 · Haptic feedback", RowY(6));

            TextMeshProUGUI quality = Stepper(graphics, "품질 · Quality", RowY(0), out Button prev, out Button next);
            TextMeshProUGUI fps = Stepper(graphics, "프레임 제한 · FPS target", RowY(1), out Button fpsPrev, out Button fpsNext);
            Toggle fullscreen = TouchToggle(graphics, "전체 화면 · Fullscreen", RowY(2));
            Toggle vsync = TouchToggle(graphics, "수직 동기화 · V-Sync", RowY(3));

            Toggle gpsMode = TouchToggle(gps, "GPS 모드 사용 · GPS mode", RowY(0));
            Slider ratio = TouchSlider(gps, "이동 비율 · Movement ratio", RowY(1), out TextMeshProUGUI ratioValue);
            Slider accuracy = TouchSlider(gps, "정확도 기준 · Accuracy threshold", RowY(2), out TextMeshProUGUI accuracyValue);
            Toggle notifications = TouchToggle(gps, "활동 알림 · Activity notifications", RowY(3));
            TextMeshProUGUI steps = StatusRow(gps, "걸음 센서 · Step counter", RowY(4));
            TextMeshProUGUI location = StatusRow(gps, "위치 권한 · Location access", RowY(5));
            Button locationButton = ButtonAt(gps, "LocationSetup", "GPS 설정 · GPS setup", TopLeft, TopLeft, new Vector2(ControlX, RowY(6) - 4f), new Vector2(ControlWidth, 56f));
            Text(gps, "Privacy", "위치는 기기 안에서만 사용되며 좌표는 표시·저장·전송되지 않습니다 · Location stays on this device: no coordinates are shown, stored or sent.",
                15f, Muted, TextAlignmentOptions.Left, TopLeft, TopLeft, TopLeft, new Vector2(40f, RowY(7) - 14f), new Vector2(1020f, 30f));

            Button defaults = ButtonAt(card, "Defaults", "기본값 · Defaults", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(40f, 32f), new Vector2(300f, 64f));
            Button back = ButtonAt(card, "Back", "뒤로 · Back   [ESC]", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 32f), new Vector2(300f, 64f), true);

            SettingsController settings = shade.gameObject.AddComponent<SettingsController>();
            SetRefs(settings, ("panel", shade.gameObject), ("masterVolumeSlider", master), ("musicVolumeSlider", music), ("sfxVolumeSlider", sfx),
                ("mouseSensitivitySlider", mouse), ("gamepadSensitivitySlider", pad), ("cameraDistanceSlider", distance), ("invertYToggle", invert),
                ("touchModePrevious", touchPrev), ("touchModeNext", touchNext), ("touchModeLabel", touchMode), ("touchLookXSlider", lookX), ("touchLookYSlider", lookY),
                ("joystickSizeSlider", stickSize), ("joystickDeadZoneSlider", deadZone), ("buttonOpacitySlider", opacity), ("hapticsToggle", haptics),
                ("qualityPrevious", prev), ("qualityNext", next), ("qualityLabel", quality), ("fpsPrevious", fpsPrev), ("fpsNext", fpsNext), ("fpsLabel", fps),
                ("fullscreenToggle", fullscreen), ("vSyncToggle", vsync),
                ("gpsModeToggle", gpsMode), ("gpsRatioSlider", ratio), ("gpsRatioValue", ratioValue), ("gpsAccuracySlider", accuracy), ("gpsAccuracyValue", accuracyValue),
                ("notificationsToggle", notifications), ("stepStatusText", steps), ("locationStatusText", location), ("locationButton", locationButton),
                ("backButton", back), ("defaultsButton", defaults));
            SetArray(settings, "tabButtons", tabs);
            SetArray(settings, "pages", pages);
            return settings;
        }

        private static void SetArray(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            SerializedProperty array = so.FindProperty(field);
            array.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // =====================================================================================
        // Main menu
        // =====================================================================================

        private static void BuildMainMenu()
        {
            Scene scene = NewScene();
            DistrictBackdrop();
            PostProcessing();
            Camera cam = MainCamera(new Vector3(-4f, 2.2f, -60f), new Vector3(2f, 6f, 0f));
            cam.gameObject.AddComponent<MenuCameraRig>().Configure(new[]
            {
                new MenuCameraRig.Shot { position = new Vector3(-4f, 2.2f, -62f), lookAt = new Vector3(2f, 7f, 0f) },
                new MenuCameraRig.Shot { position = new Vector3(4f, 3f, -22f), lookAt = new Vector3(22f, 18f, 30f) },
                new MenuCameraRig.Shot { position = new Vector3(13f, 2f, 42f), lookAt = new Vector3(60f, 5f, 42f) },
                new MenuCameraRig.Shot { position = new Vector3(-10f, 28f, 66f), lookAt = new Vector3(22f, 30f, 20f) },
                new MenuCameraRig.Shot { position = new Vector3(-30f, 9f, 6f), lookAt = new Vector3(10f, 12f, -10f) }
            }, 13f);
            EventSystem();

            Canvas canvas = UICanvas("Main Menu UI");
            // Left-side shade so the menu reads over the moving city.
            RectTransform shade = Node(canvas.transform, "Shade", Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(820f, 0f));
            Img(shade, new Color(0.02f, 0.025f, 0.035f, 0.82f));

            Text(canvas.transform, "Title", "CONTRACT", 104f, TextColor, TextAlignmentOptions.Left, TopLeft, TopLeft, TopLeft, new Vector2(90f, -120f), new Vector2(900f, 120f), true);
            Text(canvas.transform, "Subtitle", "서울의 그림자  <color=#FF4D5A>·</color>  SEOUL SHADOWS", 34f, Accent, TextAlignmentOptions.Left, TopLeft, TopLeft, TopLeft, new Vector2(96f, -240f), new Vector2(900f, 50f), true);
            Node(canvas.transform, "Rule", TopLeft, TopLeft, TopLeft, new Vector2(96f, -300f), new Vector2(120f, 4f)).gameObject.AddComponent<Image>().color = Accent;

            RectTransform main = Node(canvas.transform, "MainPanel", TopLeft, TopLeft, TopLeft, new Vector2(96f, -350f), new Vector2(520f, 540f));
            Button Item(string name, string label, float y, bool accent = false) =>
                ButtonAt(main, name, label, TopLeft, TopLeft, new Vector2(0f, y), new Vector2(460f, 62f), accent);
            Button play = Item("NewGame", "새 게임  ·  NEW GAME", 0f, true);
            Button resume = Item("Continue", "이어하기  ·  CONTINUE", -76f);
            TextMeshProUGUI resumeDetail = Text(main, "ContinueDetail", "", 15f, Muted, TextAlignmentOptions.Left, TopLeft, TopLeft, TopLeft, new Vector2(470f, -96f), new Vector2(400f, 24f));
            Button gpsButton = Item("GpsActivity", "GPS 활동  ·  GPS ACTIVITY", -152f);
            Button characters = Item("Characters", "요원  ·  OPERATIVES", -228f);
            Button settingsButton = Item("Settings", "설정  ·  SETTINGS", -304f);
            Button credits = Item("Credits", "제작진  ·  CREDITS", -380f);
            Button quit = Item("Quit", "종료  ·  QUIT", -456f);
            Text(canvas.transform, "Footer", "Gangnam district · Unity 6 URP · v0.7", 15f, Muted, TextAlignmentOptions.Left, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(96f, 30f), new Vector2(700f, 24f));

            RectTransform creditsPanel = Card(canvas.transform, "CreditsPanel", new Vector2(760f, 560f));
            Text(creditsPanel, "Title", "제작진  <size=60%><color=#9AA3AE>CREDITS</color></size>", 38f, TextColor, TextAlignmentOptions.Left, TopLeft, TopLeft, TopLeft, new Vector2(40f, -30f), new Vector2(680f, 54f), true);
            TextMeshProUGUI body = Text(creditsPanel, "Body",
                "CONTRACT: SEOUL SHADOWS\nA fictional stealth-contract game set in a stylised Seoul.\n\n" +
                "Built with Unity 6 · Universal Render Pipeline · Input System · AI Navigation\n" +
                "Korean typeface: Noto Sans KR (SIL Open Font License 1.1)\n" +
                "All characters, companies and events are fictional. No real people or organisations are depicted.",
                19f, TextColor, TextAlignmentOptions.TopLeft, TopLeft, TopLeft, TopLeft, new Vector2(40f, -110f), new Vector2(680f, 320f));
            body.textWrappingMode = TextWrappingModes.Normal;
            Button creditsBack = ButtonAt(creditsPanel, "Back", "뒤로 · Back   [ESC]", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 36f), new Vector2(260f, 56f), true);

            RectTransform confirm = Card(canvas.transform, "ConfirmPanel", new Vector2(700f, 320f));
            TextMeshProUGUI warn = Text(confirm, "Text", "새 게임을 시작할까요?\n<size=70%><color=#9AA3AE>Start a new career? Your saved progress will be replaced.</color></size>",
                28f, TextColor, TextAlignmentOptions.TopLeft, TopLeft, TopLeft, TopLeft, new Vector2(40f, -40f), new Vector2(620f, 150f), true);
            warn.textWrappingMode = TextWrappingModes.Normal;
            Button yes = ButtonAt(confirm, "Yes", "새 게임 · New game", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 36f), new Vector2(260f, 56f), true);
            Button no = ButtonAt(confirm, "No", "취소 · Cancel", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(40f, 36f), new Vector2(260f, 56f));

            SettingsController settings = SettingsPanel(canvas.transform);
            // GPS ACTIVITY opens the activity dashboard (ActivityUIBuilder adds it and wires this button).

            MainMenuController menu = new GameObject("[MainMenu]").AddComponent<MainMenuController>();
            SetRefs(menu, ("playButton", play), ("continueButton", resume), ("characterSelectionButton", characters), ("settingsButton", settingsButton),
                ("creditsButton", credits), ("quitButton", quit), ("mainPanel", main.gameObject), ("creditsPanel", creditsPanel.gameObject),
                ("creditsBackButton", creditsBack), ("settings", settings), ("confirmPanel", confirm.gameObject), ("confirmYes", yes), ("confirmNo", no),
                ("continueDetail", resumeDetail));

            EditorSceneManager.SaveScene(scene, "Assets/Scenes/MainMenu.unity");
        }

        // =====================================================================================
        // Character selection
        // =====================================================================================

        private static void BuildCharacterSelection()
        {
            Scene scene = NewScene();
            EnvKit kit = new EnvKit();
            PostProcessing();
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.08f, 0.085f, 0.1f);
            RenderSettings.fog = false;

            Transform stage = new GameObject("Stage").transform;
            kit.Primitive(stage, PrimitiveType.Cylinder, "Floor", new Vector3(0f, -0.05f, 0f), new Vector3(30f, 0.05f, 30f), "Black");
            kit.Primitive(stage, PrimitiveType.Cylinder, "Turntable", new Vector3(0f, 0f, 0f), new Vector3(2.4f, 0.04f, 2.4f), "MetalDark");
            kit.Primitive(stage, PrimitiveType.Cylinder, "TurntableRing", new Vector3(0f, 0f, 0f), new Vector3(2.55f, 0.03f, 2.55f), "RedPaint", castShadows: false); // top below the disc: no z-fighting
            kit.Block(stage, "Backdrop", new Vector3(0f, 0f, -6f), new Vector3(20f, 9f, 0.4f), "Concrete", new Vector2(4f, 4f), collider: false);
            // Neon sign behind the operative's head (the camera frames x = 0; up to y ~ 3.7 at the backdrop).
            kit.Text(stage, "Neon", "요원 선택", new Vector3(0f, 3.35f, -5.75f), new Vector2(4.5f, 0.7f), new Color(0.75f, 0.16f, 0.22f), kit.NeonTextMaterial);
            kit.Text(stage, "NeonEn", "SELECT  OPERATIVE", new Vector3(0f, 2.8f, -5.75f), new Vector2(4.5f, 0.28f), new Color(0.6f, 0.62f, 0.66f), kit.NeonTextMaterial);

            void Spot(string name, Vector3 position, Color color, float intensity, float angle)
            {
                Light light = new GameObject(name).AddComponent<Light>();
                light.type = LightType.Spot;
                light.transform.position = position;
                light.transform.LookAt(new Vector3(0f, 1f, 0f));
                light.color = color;
                light.intensity = intensity;
                light.range = 14f;
                light.spotAngle = angle;
                light.shadows = LightShadows.Soft;
            }

            Spot("KeyLight", new Vector3(-2.5f, 4f, 4f), new Color(1f, 0.95f, 0.9f), 30f, 45f);
            Spot("RimRed", new Vector3(3f, 3.5f, -2.5f), new Color(1f, 0.2f, 0.25f), 40f, 50f);
            Spot("RimCool", new Vector3(-3f, 3f, -2.5f), new Color(0.5f, 0.7f, 1f), 25f, 50f);

            Transform turntable = new GameObject("Turntable").transform;
            turntable.position = new Vector3(0f, 0.04f, 0f);
            // Camera looks down -Z, so world -X is screen-right: x = +0.15 puts the operative just left of
            // centre, in the gap between the card list (left) and the details panel (right).
            Camera cam = MainCamera(new Vector3(0.15f, 1.35f, 4.1f), new Vector3(0.15f, 1f, 0f));
            cam.fieldOfView = 40f;
            cam.backgroundColor = new Color(0.02f, 0.02f, 0.025f);
            CharacterPreviewStage preview = turntable.gameObject.AddComponent<CharacterPreviewStage>();
            SetRefs(preview, ("turntable", turntable), ("baseMaterial", AssetDatabase.LoadAssetAtPath<Material>(BaseMaterialPath)), ("stageCamera", cam));
            EventSystem();

            Canvas canvas = UICanvas("Character Selection UI");
            Text(canvas.transform, "Title", "요원 선택  <size=55%><color=#9AA3AE>SELECT OPERATIVE</color></size>", 44f, TextColor, TextAlignmentOptions.Left,
                TopLeft, TopLeft, TopLeft, new Vector2(60f, -40f), new Vector2(1000f, 64f), true);
            RectTransform cards = Node(canvas.transform, "Cards", TopLeft, TopLeft, TopLeft, new Vector2(60f, -140f), new Vector2(420f, 330f));
            VerticalLayoutGroup layout = cards.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 12f;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandHeight = false;

            RectTransform details = Node(canvas.transform, "Details", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-60f, -140f), new Vector2(560f, 760f));
            Img(details, new Color(0.06f, 0.065f, 0.08f, 0.9f));
            Node(details, "Accent", TopLeft, Vector2.one, Top, Vector2.zero, new Vector2(0f, 4f)).gameObject.AddComponent<Image>().color = Accent;
            TextMeshProUGUI nameText = Text(details, "Name", "", 40f, TextColor, TextAlignmentOptions.Left, TopLeft, TopLeft, TopLeft, new Vector2(32f, -28f), new Vector2(500f, 54f), true);
            TextMeshProUGUI specialty = Text(details, "Specialty", "", 18f, Accent, TextAlignmentOptions.Left, TopLeft, TopLeft, TopLeft, new Vector2(32f, -84f), new Vector2(500f, 28f), true);
            TextMeshProUGUI bio = Text(details, "Bio", "", 18f, TextColor, TextAlignmentOptions.TopLeft, TopLeft, TopLeft, TopLeft, new Vector2(32f, -126f), new Vector2(500f, 120f));
            bio.textWrappingMode = TextWrappingModes.Normal;
            Image StatBar(string label, float y)
            {
                Text(details, label, label, 17f, Muted, TextAlignmentOptions.Left, TopLeft, TopLeft, TopLeft, new Vector2(32f, y), new Vector2(240f, 26f));
                return Fill(details, label + "_Bar", TopLeft, TopLeft, new Vector2(270f, y - 9f), new Vector2(250f, 8f));
            }

            Image speed = StatBar("속도 · Speed", -262f);
            Image stealth = StatBar("은신 · Stealth", -298f);
            Image intelligence = StatBar("지능 · Intelligence", -334f);
            Text(details, "EffectsTitle", "게임 효과  <size=75%><color=#9AA3AE>IN-GAME EFFECTS</color></size>", 17f, Accent, TextAlignmentOptions.Left, TopLeft, TopLeft, TopLeft, new Vector2(32f, -384f), new Vector2(500f, 26f), true);
            TextMeshProUGUI effects = Text(details, "Effects", "", 17f, TextColor, TextAlignmentOptions.TopLeft, TopLeft, TopLeft, TopLeft, new Vector2(32f, -418f), new Vector2(500f, 160f));
            effects.lineSpacing = 12f;
            TextMeshProUGUI lockText = Text(details, "Lock", "", 17f, new Color(1f, 0.69f, 0.13f), TextAlignmentOptions.Left, TopLeft, TopLeft, TopLeft, new Vector2(32f, -566f), new Vector2(500f, 26f));
            Button unlock = ButtonAt(details, "Unlock", "", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(32f, 104f), new Vector2(496f, 52f));
            Button confirm = ButtonAt(details, "Confirm", "", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(32f, 32f), new Vector2(496f, 60f), true);

            Button back = ButtonAt(canvas.transform, "Back", "← 뒤로 · Back   [ESC]", Vector2.zero, Vector2.zero, new Vector2(60f, 40f), new Vector2(280f, 56f));
            TextMeshProUGUI balance = Text(canvas.transform, "Balance", "", 20f, TextColor, TextAlignmentOptions.Left, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(360f, 54f), new Vector2(420f, 30f), true);
            TextMeshProUGUI rotateHint = Text(canvas.transform, "Hint", "우클릭 드래그 · 회전   Right-drag to rotate", 15f, Muted, TextAlignmentOptions.Center, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-80f, 40f), new Vector2(600f, 24f));
            rotateHint.gameObject.AddComponent<TouchHintText>().SetTouchText("드래그 · 회전   Drag to rotate");

            CharacterSelectionController controller = new GameObject("[CharacterSelection]").AddComponent<CharacterSelectionController>();
            SetRefs(controller, ("cardContainer", cards), ("characterNameText", nameText), ("specialtyText", specialty), ("descriptionText", bio),
                ("speedBar", speed), ("stealthBar", stealth), ("intelligenceBar", intelligence), ("effectsText", effects), ("balanceText", balance),
                ("lockText", lockText), ("selectButton", confirm), ("selectLabel", confirm.GetComponentInChildren<TextMeshProUGUI>()),
                ("unlockButton", unlock), ("unlockLabel", unlock.GetComponentInChildren<TextMeshProUGUI>()), ("backButton", back), ("stage", preview));

            EditorSceneManager.SaveScene(scene, "Assets/Scenes/CharacterSelection.unity");
        }

        // =====================================================================================
        // Mission selection
        // =====================================================================================

        // =====================================================================================
        // World selection (South Korea destinations)
        // =====================================================================================

        [MenuItem("CONTRACT/World/Build World Selection")]
        public static void BuildWorldSelectionScene()
        {
            UseKoreanUIFont();
            white = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/Sprites/White.png");
            BuildWorldSelection();
            AssetDatabase.SaveAssets();
        }

        private static void BuildWorldSelection()
        {
            Scene scene = NewScene();
            GameObject light = new GameObject("Directional Light");
            light.AddComponent<Light>().type = LightType.Directional;
            GameObject district = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(MyeongdongBuilder.PrefabPath));
            GangnamDistrictBuilder.ApplyNightLighting(district.transform);
            PostProcessing();
            Camera cam = MainCamera(new Vector3(-60f, 70f, -95f), new Vector3(20f, 0f, 10f));
            cam.gameObject.AddComponent<MenuCameraRig>().Configure(new[]
            {
                new MenuCameraRig.Shot { position = new Vector3(-60f, 70f, -95f), lookAt = new Vector3(20f, 0f, 10f) },
                new MenuCameraRig.Shot { position = new Vector3(110f, 60f, -40f), lookAt = new Vector3(40f, 10f, 18f) },
                new MenuCameraRig.Shot { position = new Vector3(20f, 75f, 110f), lookAt = new Vector3(0f, 0f, 0f) }
            }, 20f);
            EventSystem();

            Canvas canvas = UICanvas("World Selection UI");
            Img(Stretch(canvas.transform, "Shade"), new Color(0.02f, 0.025f, 0.035f, 0.72f));
            Text(canvas.transform, "Title", "목적지 선택  <size=60%><color=#9AA3AE>DESTINATIONS · SOUTH KOREA</color></size>", 44f, TextColor, TextAlignmentOptions.Left,
                TopLeft, TopLeft, TopLeft, new Vector2(60f, -50f), new Vector2(1200f, 64f), true);
            Node(canvas.transform, "Rule", TopLeft, TopLeft, TopLeft, new Vector2(62f, -120f), new Vector2(120f, 4f)).gameObject.AddComponent<Image>().color = Accent;
            TextMeshProUGUI balance = Text(canvas.transform, "Balance", "", 24f, TextColor, TextAlignmentOptions.Right, Vector2.one, Vector2.one, Vector2.one,
                new Vector2(-60f, -60f), new Vector2(600f, 40f), true);

            RectTransform cards = Node(canvas.transform, "Destinations", TopLeft, TopLeft, TopLeft, new Vector2(60f, -160f), new Vector2(520f, 620f));
            VerticalLayoutGroup layout = cards.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10f;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandHeight = false;

            RectTransform detail = Node(canvas.transform, "Detail", TopLeft, TopLeft, TopLeft, new Vector2(620f, -160f), new Vector2(1240f, 820f));
            Img(detail, Panel);
            Node(detail, "Accent", TopLeft, Vector2.one, Top, Vector2.zero, new Vector2(0f, 4f)).gameObject.AddComponent<Image>().color = Accent;
            RectTransform previewRect = Node(detail, "Preview", TopLeft, TopLeft, TopLeft, new Vector2(30f, -30f), new Vector2(640f, 360f));
            RawImage preview = previewRect.gameObject.AddComponent<RawImage>();
            preview.raycastTarget = false;
            RectTransform placeholder = Node(detail, "Placeholder", TopLeft, TopLeft, TopLeft, new Vector2(30f, -30f), new Vector2(640f, 360f));
            Img(placeholder, PanelLight);
            TextMeshProUGUI placeholderText = Text(placeholder, "Text", "", 64f, Muted, TextAlignmentOptions.Center, Vector2.zero, Vector2.one, Centre, Vector2.zero, Vector2.zero, true);

            TextMeshProUGUI title = Text(detail, "Name", "", 46f, TextColor, TextAlignmentOptions.Left, TopLeft, TopLeft, TopLeft, new Vector2(700f, -30f), new Vector2(510f, 60f), true);
            TextMeshProUGUI status = Text(detail, "Status", "", 19f, TextColor, TextAlignmentOptions.TopLeft, TopLeft, TopLeft, TopLeft, new Vector2(700f, -104f), new Vector2(510f, 84f));
            status.textWrappingMode = TextWrappingModes.Normal;
            TextMeshProUGUI progressText = Text(detail, "Progress", "", 18f, Muted, TextAlignmentOptions.Left, TopLeft, TopLeft, TopLeft, new Vector2(700f, -200f), new Vector2(510f, 28f));
            Image progressFill = Fill(detail, "ProgressBar", TopLeft, TopLeft, new Vector2(700f, -236f), new Vector2(510f, 10f));
            TextMeshProUGUI districts = Text(detail, "Districts", "", 19f, TextColor, TextAlignmentOptions.TopLeft, TopLeft, TopLeft, TopLeft, new Vector2(700f, -266f), new Vector2(510f, 130f));
            districts.textWrappingMode = TextWrappingModes.Normal;
            TextMeshProUGUI description = Text(detail, "Description", "", 21f, TextColor, TextAlignmentOptions.TopLeft, TopLeft, TopLeft, TopLeft, new Vector2(30f, -414f), new Vector2(1180f, 200f));
            description.textWrappingMode = TextWrappingModes.Normal;

            Vector2 bottomRight = new Vector2(1f, 0f), bottomLeft = Vector2.zero;
            Button enter = ButtonAt(detail, "Enter", "", bottomRight, bottomRight, new Vector2(-30f, 30f), new Vector2(420f, 64f), true);
            Button unlock = ButtonAt(detail, "Unlock", "", bottomRight, bottomRight, new Vector2(-30f, 30f), new Vector2(420f, 64f), true);
            Button districtUnlock = ButtonAt(detail, "UnlockDistrict", "", bottomRight, bottomRight, new Vector2(-470f, 30f), new Vector2(420f, 64f));
            Button back = ButtonAt(canvas.transform, "Back", "← 요원 선택 · Back   [ESC]", bottomLeft, bottomLeft, new Vector2(60f, 60f), new Vector2(360f, 60f));

            WorldSelectionView view = new GameObject("[WorldSelection]").AddComponent<WorldSelectionView>();
            SetRefs(view, ("cardContainer", cards), ("preview", preview), ("previewPlaceholder", placeholder.gameObject), ("placeholderText", placeholderText),
                ("title", title), ("description", description), ("status", status), ("districts", districts), ("progressText", progressText),
                ("progressFill", progressFill), ("balance", balance), ("unlockButton", unlock), ("unlockLabel", unlock.GetComponentInChildren<TextMeshProUGUI>()),
                ("districtButton", districtUnlock), ("districtLabel", districtUnlock.GetComponentInChildren<TextMeshProUGUI>()), ("enterButton", enter),
                ("enterLabel", enter.GetComponentInChildren<TextMeshProUGUI>()), ("backButton", back));

            EditorSceneManager.SaveScene(scene, "Assets/Scenes/WorldSelection.unity");
            MyeongdongBuilder.EnsureInBuild("Assets/Scenes/WorldSelection.unity");
        }

        private static void BuildMissionSelection()
        {
            Scene scene = NewScene();
            DistrictBackdrop();
            PostProcessing();
            Camera cam = MainCamera(new Vector3(-70f, 90f, -80f), new Vector3(10f, 0f, 20f));
            cam.gameObject.AddComponent<MenuCameraRig>().Configure(new[]
            {
                new MenuCameraRig.Shot { position = new Vector3(-70f, 90f, -80f), lookAt = new Vector3(10f, 0f, 20f) },
                new MenuCameraRig.Shot { position = new Vector3(80f, 95f, -70f), lookAt = new Vector3(0f, 0f, 25f) },
                new MenuCameraRig.Shot { position = new Vector3(70f, 85f, 110f), lookAt = new Vector3(0f, 0f, 10f) },
                new MenuCameraRig.Shot { position = new Vector3(-80f, 90f, 100f), lookAt = new Vector3(10f, 0f, 10f) }
            }, 20f);
            EventSystem();

            GameObject board = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(MissionUIBuilder.BoardPrefabPath));
            ContractBoardView view = board.GetComponent<ContractBoardView>();
            SerializedObject so = new SerializedObject(view);
            so.FindProperty("sceneMode").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            // The board backdrop is semi-transparent here so the aerial city shows through.
            Image backdrop = board.transform.Find("ContractBoard").GetComponent<Image>();
            backdrop.color = new Color(0.02f, 0.025f, 0.035f, 0.82f);
            TextMeshProUGUI closeLabel = board.transform.Find("ContractBoard/Header/Close/Label").GetComponent<TextMeshProUGUI>();
            closeLabel.text = "← 목적지 · Destinations   [ESC]";
            TextMeshProUGUI title = board.transform.Find("ContractBoard/Header/Title").GetComponent<TextMeshProUGUI>();
            title.text = "임무 선택  <size=60%><color=#9AA3AE>MISSIONS</color></size>";
            PrefabUtility.RecordPrefabInstancePropertyModifications(backdrop);
            PrefabUtility.RecordPrefabInstancePropertyModifications(closeLabel);
            PrefabUtility.RecordPrefabInstancePropertyModifications(title);

            EditorSceneManager.SaveScene(scene, "Assets/Scenes/MissionSelection.unity");
        }

        // =====================================================================================
        // GameWorld HUD
        // =====================================================================================

        private static void BuildGameWorldHud()
        {
            Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/GameWorld.unity", OpenSceneMode.Single);
            GameObject old = GameObject.Find("GameHUD");
            if (old != null) Object.DestroyImmediate(old);

            Canvas canvas = UICanvas("GameHUD", 12);
            canvas.gameObject.AddComponent<MobileCanvasScale>(); // larger HUD with touch controls
            // Older canvases kept in the scene (screen fader, prototype UI): same no-overflow scaling.
            foreach (CanvasScaler other in Object.FindObjectsByType<CanvasScaler>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (other.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize && other.GetComponent<Contract.UI.Touch.TouchControls>() == null)
                    other.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

            // Interaction prompt (text written by the player's InteractionController).
            RectTransform prompt = Node(canvas.transform, "InteractionPrompt", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 150f), new Vector2(720f, 84f));
            Img(prompt, new Color(0.04f, 0.045f, 0.055f, 0.88f));
            Node(prompt, "Accent", Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(4f, 0f)).gameObject.AddComponent<Image>().color = Accent;
            TextMeshProUGUI promptText = Text(prompt, "Text", "", 22f, TextColor, TextAlignmentOptions.Center, Vector2.zero, Vector2.one, Centre, new Vector2(0f, 6f), new Vector2(-40f, -20f), true);
            promptText.textWrappingMode = TextWrappingModes.Normal;
            RectTransform holdBar = Node(prompt, "HoldBar", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(600f, 6f));
            Img(holdBar, PanelLight);
            Image holdFill = Img(Stretch(holdBar, "Fill"), Accent);
            holdFill.sprite = white;
            holdFill.type = Image.Type.Filled;
            holdFill.fillMethod = Image.FillMethod.Horizontal;
            InteractionPromptView promptView = prompt.gameObject.AddComponent<InteractionPromptView>();
            SetRefs(promptView, ("holdFill", holdFill), ("holdBar", holdBar.gameObject));
            prompt.gameObject.SetActive(false);

            // Detection meter.
            RectTransform detection = Node(canvas.transform, "Detection", Top, Top, Top, new Vector2(0f, -130f), new Vector2(420f, 52f));
            CanvasGroup detectionGroup = detection.gameObject.AddComponent<CanvasGroup>();
            detectionGroup.blocksRaycasts = false;
            TextMeshProUGUI detectionLabel = Text(detection, "Label", "", 17f, TextColor, TextAlignmentOptions.Center, Top, Top, Top, Vector2.zero, new Vector2(420f, 26f), true);
            Image detectionFill = Fill(detection, "Bar", Top, Top, new Vector2(0f, -32f), new Vector2(380f, 8f));
            DetectionMeterView detectionView = detection.gameObject.AddComponent<DetectionMeterView>();
            SetRefs(detectionView, ("group", detectionGroup), ("fill", detectionFill), ("label", detectionLabel));

            // Operative badge and controls.
            RectTransform badge = Node(canvas.transform, "Operative", Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(40f, 32f), new Vector2(360f, 64f));
            Image swatch = Img(Node(badge, "Swatch", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(6f, 56f)), Accent);
            TextMeshProUGUI badgeName = Text(badge, "Name", "", 22f, TextColor, TextAlignmentOptions.Left, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(340f, 60f), true);
            CharacterBadgeView badgeView = badge.gameObject.AddComponent<CharacterBadgeView>();
            SetRefs(badgeView, ("nameText", badgeName), ("swatch", swatch));
            Text(canvas.transform, "Controls",
                "<color=#FFFFFF>WASD</color> 이동   <color=#FFFFFF>Shift</color> 질주   <color=#FFFFFF>Ctrl</color> 웅크리기   <color=#FFFFFF>Space</color> 점프   " +
                "<color=#FFFFFF>E</color> 상호작용   <color=#FFFFFF>M</color> 지도   <color=#FFFFFF>Esc</color> 일시정지",
                17f, new Color(0.78f, 0.81f, 0.86f), TextAlignmentOptions.Right, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-40f, 30f), new Vector2(1100f, 24f));

            // Pause + settings.
            RectTransform pauseShade = Stretch(canvas.transform, "Pause");
            DrawAbove(pauseShade, OverlayOrder);
            Img(pauseShade, new Color(0f, 0f, 0f, 0.65f), true);
            RectTransform pauseCard = Card(pauseShade, "Card", new Vector2(560f, 640f));
            Text(pauseCard, "Title", "일시정지  <size=55%><color=#9AA3AE>PAUSED</color></size>", 40f, TextColor, TextAlignmentOptions.Left, TopLeft, TopLeft, TopLeft, new Vector2(40f, -30f), new Vector2(480f, 56f), true);
            Button resume = ButtonAt(pauseCard, "Resume", "계속하기 · Resume", TopLeft, TopLeft, new Vector2(40f, -120f), new Vector2(480f, 62f), true);
            Button settingsButton = ButtonAt(pauseCard, "Settings", "설정 · Settings", TopLeft, TopLeft, new Vector2(40f, -200f), new Vector2(480f, 62f));
            Button save = ButtonAt(pauseCard, "Save", "저장 · Save game", TopLeft, TopLeft, new Vector2(40f, -280f), new Vector2(480f, 62f));
            Button mainMenu = ButtonAt(pauseCard, "MainMenu", "메인 메뉴 · Main menu", TopLeft, TopLeft, new Vector2(40f, -360f), new Vector2(480f, 62f));
            Button quit = ButtonAt(pauseCard, "Quit", "게임 종료 · Quit game", TopLeft, TopLeft, new Vector2(40f, -440f), new Vector2(480f, 62f));
            Text(pauseCard, "SaveNote", "자동 저장: 1분마다, 의뢰 완료 및 구매 시 · Auto-saves every minute, after contracts and purchases", 14f, Muted,
                TextAlignmentOptions.Left, TopLeft, TopLeft, TopLeft, new Vector2(40f, -530f), new Vector2(480f, 60f)).textWrappingMode = TextWrappingModes.Normal;
            SettingsController settings = SettingsPanel(canvas.transform);
            PauseMenuController pause = canvas.gameObject.AddComponent<PauseMenuController>();
            SetRefs(pause, ("pausePanel", pauseShade.gameObject), ("resumeButton", resume), ("settingsButton", settingsButton),
                ("mainMenuButton", mainMenu), ("quitButton", quit), ("settings", settings), ("saveButton", save));

            // "Saved" badge (auto-saves and manual saves).
            RectTransform saved = Node(canvas.transform, "SaveIndicator", Vector2.one, Vector2.one, Vector2.one, new Vector2(-40f, -470f), new Vector2(260f, 40f));
            Img(saved, new Color(0.04f, 0.045f, 0.055f, 0.85f));
            CanvasGroup savedGroup = saved.gameObject.AddComponent<CanvasGroup>();
            savedGroup.blocksRaycasts = false;
            TextMeshProUGUI savedLabel = Text(saved, "Label", "", 17f, TextColor, TextAlignmentOptions.Center, Vector2.zero, Vector2.one, Centre, Vector2.zero, Vector2.zero, true);
            SetRefs(saved.gameObject.AddComponent<SaveIndicatorView>(), ("group", savedGroup), ("label", savedLabel));

            // The player's prompt now writes into the new HUD.
            InteractionController interaction = Object.FindAnyObjectByType<InteractionController>();
            SerializedObject ic = new SerializedObject(interaction);
            ic.FindProperty("promptText").objectReferenceValue = promptText;
            ic.FindProperty("promptRoot").objectReferenceValue = prompt.gameObject;
            ic.ApplyModifiedPropertiesWithoutUndo();

            // Superseded prototype UI: old pause / settings controllers and panels, old HUD widgets.
            foreach (string path in new[] { "[PauseMenu]", "[GameSettings]" })
            {
                GameObject legacy = GameObject.Find(path);
                if (legacy != null) Object.DestroyImmediate(legacy);
            }

            foreach (string path in new[] { "UI Canvas/Pause Panel", "UI Canvas/Game Settings Panel", "UI Canvas/Interaction Prompt Panel", "UI Canvas/HUD Panel" })
            {
                GameObject legacy = GameObject.Find(path);
                if (legacy != null) legacy.SetActive(false);
            }

            // UIManager re-shows its HUD panel on every ShowHUD; the old panel (suspicion slider,
            // inventory) is replaced by the widgets above, so unhook it.
            UIManager uiManager = Object.FindAnyObjectByType<UIManager>();
            if (uiManager != null) SetRefs(uiManager, ("hudPanel", null));

            // Prototype shop / inventory UI: replaced by the safehouse terminal's Upgrades page.
            foreach (string path in new[] { "[UpgradeShop]", "UI Canvas/Upgrade Shop Panel", "UI Canvas/Shop Open Button" })
            {
                GameObject legacy = GameObject.Find(path);
                if (legacy != null) Object.DestroyImmediate(legacy);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
