using System;
using System.IO;
using Contract.Core;
using Contract.UI.Touch;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static Contract.Editor.World.UIKit;

namespace Contract.Editor.World
{
    /// <summary>
    /// Phase 8: Android touchscreen controls. Draws the control icons (anti-aliased vector shapes
    /// rendered to PNG sprites), builds the TouchControls prefab (floating joystick, look surface,
    /// Jump / Sprint / Crouch / Interact, Pause / Map) and places it in every gameplay scene with
    /// the HUD's keyboard hints wired so they hide while touch controls show.
    /// Headless: -executeMethod Contract.Editor.World.TouchControlsBuilder.Build
    /// </summary>
    public static class TouchControlsBuilder
    {
        public const string PrefabPath = "Assets/Prefabs/UI/TouchControls.prefab";
        private const string IconFolder = "Assets/UI/Sprites/Touch";
        private const string InputReaderPath = "Assets/Settings/InputReader.asset";
        private const int IconSize = 192;

        private static readonly Vector2 BottomRight = new Vector2(1f, 0f), TopRight = Vector2.one, BottomLeft = Vector2.zero, Centre = new Vector2(0.5f, 0.5f);

        [MenuItem("CONTRACT/World/Build Touch Controls")]
        public static void Build()
        {
            GenerateIcons();
            GameObject prefab = BuildPrefab();
            foreach (string scene in new[] { "Assets/Scenes/GameWorld.unity", MyeongdongBuilder.ScenePath })
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scene) != null) Place(scene, prefab);
            Debug.Log("[TouchControlsBuilder] Done.");
        }

        // =====================================================================================
        // Icons
        // =====================================================================================

        private static Sprite Icon(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{IconFolder}/{name}.png");

        private static void GenerateIcons()
        {
            Directory.CreateDirectory(IconFolder);
            const float stroke = 0.075f;
            Draw("Circle", p => Circle(p, new Vector2(0.5f, 0.5f), 0.48f));
            Draw("Ring", p => Ring(p, new Vector2(0.5f, 0.5f), 0.465f, 0.035f));
            Draw("Jump", p => Min(Polygon(p, new Vector2(0.5f, 0.84f), new Vector2(0.76f, 0.54f), new Vector2(0.24f, 0.54f)),
                Capsule(p, new Vector2(0.5f, 0.58f), new Vector2(0.5f, 0.2f), stroke)));
            Draw("Sprint", p => Min(Polyline(p, stroke, new Vector2(0.22f, 0.78f), new Vector2(0.46f, 0.5f), new Vector2(0.22f, 0.22f)),
                Polyline(p, stroke, new Vector2(0.5f, 0.78f), new Vector2(0.74f, 0.5f), new Vector2(0.5f, 0.22f))));
            Draw("Crouch", p => Min(Min(Polygon(p, new Vector2(0.5f, 0.3f), new Vector2(0.24f, 0.6f), new Vector2(0.76f, 0.6f)),
                Capsule(p, new Vector2(0.5f, 0.56f), new Vector2(0.5f, 0.86f), stroke)), Capsule(p, new Vector2(0.2f, 0.15f), new Vector2(0.8f, 0.15f), stroke)));
            Draw("Interact", p => Min(Ring(p, new Vector2(0.5f, 0.5f), 0.3f, stroke), Circle(p, new Vector2(0.5f, 0.5f), 0.12f)));
            Draw("Pause", p => Min(Capsule(p, new Vector2(0.37f, 0.25f), new Vector2(0.37f, 0.75f), 0.1f), Capsule(p, new Vector2(0.63f, 0.25f), new Vector2(0.63f, 0.75f), 0.1f)));
            Draw("Map", p => Min(Polyline(p, 0.06f, new Vector2(0.14f, 0.24f), new Vector2(0.14f, 0.8f), new Vector2(0.38f, 0.72f), new Vector2(0.62f, 0.8f),
                    new Vector2(0.86f, 0.72f), new Vector2(0.86f, 0.16f), new Vector2(0.62f, 0.24f), new Vector2(0.38f, 0.16f), new Vector2(0.14f, 0.24f)),
                Min(Capsule(p, new Vector2(0.38f, 0.72f), new Vector2(0.38f, 0.16f), 0.05f), Capsule(p, new Vector2(0.62f, 0.8f), new Vector2(0.62f, 0.24f), 0.05f))));
            AssetDatabase.Refresh();
            foreach (string file in Directory.GetFiles(IconFolder, "*.png"))
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(file.Replace('\\', '/'));
                importer.textureType = TextureImporterType.Sprite;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }
        }

        /// <summary>Renders a signed-distance shape (unit square, distance in units; negative inside) as an anti-aliased white sprite.</summary>
        private static void Draw(string name, Func<Vector2, float> sdf)
        {
            var tex = new Texture2D(IconSize, IconSize, TextureFormat.RGBA32, false);
            var pixels = new Color32[IconSize * IconSize];
            for (int y = 0; y < IconSize; y++)
            for (int x = 0; x < IconSize; x++)
            {
                Vector2 p = new Vector2((x + 0.5f) / IconSize, (y + 0.5f) / IconSize);
                float alpha = Mathf.Clamp01(0.5f - sdf(p) * IconSize);
                pixels[y * IconSize + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
            }

            tex.SetPixels32(pixels);
            File.WriteAllBytes($"{IconFolder}/{name}.png", tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }

        private static float Min(float a, float b) => Mathf.Min(a, b);
        private static float Circle(Vector2 p, Vector2 c, float r) => Vector2.Distance(p, c) - r;
        private static float Ring(Vector2 p, Vector2 c, float r, float width) => Mathf.Abs(Vector2.Distance(p, c) - r) - width / 2f;

        private static float Capsule(Vector2 p, Vector2 a, Vector2 b, float width)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
            return Vector2.Distance(p, a + ab * t) - width / 2f;
        }

        private static float Polyline(Vector2 p, float width, params Vector2[] points)
        {
            float d = float.MaxValue;
            for (int i = 0; i + 1 < points.Length; i++) d = Mathf.Min(d, Capsule(p, points[i], points[i + 1], width));
            return d;
        }

        /// <summary>Exact signed distance to a polygon (inside negative).</summary>
        private static float Polygon(Vector2 p, params Vector2[] v)
        {
            float d = Vector2.SqrMagnitude(p - v[0]);
            float sign = 1f;
            for (int i = 0, j = v.Length - 1; i < v.Length; j = i, i++)
            {
                Vector2 e = v[j] - v[i], w = p - v[i];
                Vector2 b = w - e * Mathf.Clamp01(Vector2.Dot(w, e) / Vector2.Dot(e, e));
                d = Mathf.Min(d, b.sqrMagnitude);
                bool c1 = p.y >= v[i].y, c2 = p.y < v[j].y, c3 = e.x * w.y > e.y * w.x;
                if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) sign = -sign;
            }

            return sign * Mathf.Sqrt(d);
        }

        // =====================================================================================
        // Prefab
        // =====================================================================================

        private static GameObject BuildPrefab()
        {
            var root = new GameObject("TouchControls", typeof(RectTransform));
            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20; // above the HUD (12); hidden whenever a menu, the map or pause is open
            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 1f; // phones are wider than 16:9: size controls by height so they stay thumb-sized
            root.AddComponent<GraphicRaycaster>();
            root.AddComponent<Contract.UI.SafeAreaFitter>(); // thumbs stay clear of notches and rounded corners
            CanvasGroup group = root.AddComponent<CanvasGroup>();

            // Look surface: the right side, behind the buttons.
            RectTransform look = Node(root.transform, "LookArea", new Vector2(0.42f, 0f), Vector2.one, Centre, Vector2.zero, Vector2.zero);
            Img(look, Color.clear, true);
            TouchLookArea lookArea = look.gameObject.AddComponent<TouchLookArea>();

            RectTransform movement = Stretch(root.transform, "Movement");

            // Floating joystick: the left side below the top HUD.
            RectTransform zone = Node(movement, "JoystickZone", Vector2.zero, new Vector2(0.42f, 0.78f), Centre, Vector2.zero, Vector2.zero);
            Img(zone, Color.clear, true);
            RectTransform baseRect = Node(zone, "Base", BottomLeft, BottomLeft, Centre, new Vector2(250f, 250f), new Vector2(250f, 250f));
            Image baseImage = Img(baseRect, new Color(0.07f, 0.075f, 0.09f, 0.55f));
            baseImage.sprite = Icon("Circle");
            Image baseRing = Img(Stretch(baseRect, "Ring"), new Color(1f, 1f, 1f, 0.5f));
            baseRing.sprite = Icon("Ring");
            RectTransform knob = Node(baseRect, "Knob", Centre, Centre, Centre, Vector2.zero, new Vector2(112f, 112f));
            Image knobImage = Img(knob, new Color(0.93f, 0.94f, 0.96f, 0.9f));
            knobImage.sprite = Icon("Circle");
            VirtualJoystick joystick = zone.gameObject.AddComponent<VirtualJoystick>();
            SetRefs(joystick, ("zone", zone), ("baseRect", baseRect), ("knob", knob), ("baseImage", baseImage), ("knobImage", knobImage));

            TouchButton jump = Button(movement, "Jump", "Jump", "점프", BottomRight, new Vector2(-150f, 165f), 172f, TouchButton.Mode.Tap);
            TouchButton crouch = Button(movement, "Crouch", "Crouch", "웅크리기", BottomRight, new Vector2(-350f, 110f), 128f, TouchButton.Mode.Toggle);
            TouchButton sprint = Button(movement, "Sprint", "Sprint", "질주", BottomRight, new Vector2(-135f, 375f), 128f, TouchButton.Mode.Toggle);
            TouchButton interact = Button(root.transform, "Interact", "Interact", "상호작용", BottomRight, new Vector2(-345f, 305f), 150f, TouchButton.Mode.Hold);
            Image progress = Img(Stretch(interact.transform, "HoldProgress"), new Color(1f, 1f, 1f, 0.95f));
            progress.sprite = Icon("Ring");
            progress.type = Image.Type.Filled;
            progress.fillMethod = Image.FillMethod.Radial360;
            progress.fillOrigin = (int)Image.Origin360.Top;
            progress.fillClockwise = true;
            progress.enabled = false;

            // Under the minimap and its district label (the HUD scales by width, these by height: a row
            // below the label stays clear of the minimap on every aspect ratio from 4:3 to 21:9).
            TouchButton map = Button(root.transform, "Map", "Map", null, TopRight, new Vector2(-205f, -490f), 96f, TouchButton.Mode.Tap);
            TouchButton pause = Button(root.transform, "Pause", "Pause", null, TopRight, new Vector2(-90f, -490f), 96f, TouchButton.Mode.Tap);

            TouchControls controls = root.AddComponent<TouchControls>();
            SetRefs(controls, ("inputReader", AssetDatabase.LoadAssetAtPath<InputReader>(InputReaderPath)), ("group", group), ("joystick", joystick),
                ("lookArea", lookArea), ("jumpButton", jump), ("sprintButton", sprint), ("crouchButton", crouch), ("interactButton", interact),
                ("pauseButton", pause), ("mapButton", map), ("interactProgress", progress), ("movementGroup", movement.gameObject));

            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static TouchButton Button(Transform parent, string name, string icon, string korean, Vector2 anchor, Vector2 position, float size, TouchButton.Mode mode)
        {
            RectTransform rt = Node(parent, name, anchor, anchor, Centre, position, new Vector2(size, size));
            Image background = Img(rt, new Color(0.07f, 0.075f, 0.09f, 0.62f), true);
            background.sprite = Icon("Circle");
            Image ring = Img(Stretch(rt, "Ring"), new Color(1f, 1f, 1f, 0.35f));
            ring.sprite = Icon("Ring");
            float iconSize = size * (korean != null ? 0.46f : 0.56f);
            Image glyph = Img(Node(rt, "Icon", Centre, Centre, Centre, new Vector2(0f, korean != null ? size * 0.08f : 0f), new Vector2(iconSize, iconSize)), Color.white);
            glyph.sprite = Icon(icon);
            glyph.preserveAspect = true;
            if (korean != null)
            {
                TextMeshProUGUI label = Text(rt, "Label", korean, Mathf.Max(15f, size * 0.12f), new Color(1f, 1f, 1f, 0.85f), TextAlignmentOptions.Center,
                    Centre, Centre, Centre, new Vector2(0f, -size * 0.3f), new Vector2(size, size * 0.2f), true);
                label.raycastTarget = false;
            }

            TouchButton button = rt.gameObject.AddComponent<TouchButton>();
            SetRefs(button, ("background", background), ("icon", glyph));
            var so = new SerializedObject(button);
            so.FindProperty("mode").enumValueIndex = (int)mode;
            so.ApplyModifiedPropertiesWithoutUndo();
            return button;
        }

        // =====================================================================================
        // Scenes
        // =====================================================================================

        private static void Place(string scenePath, GameObject prefab)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            GameObject old = GameObject.Find("TouchControls");
            if (old != null) UnityEngine.Object.DestroyImmediate(old);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            GameObject hints = GameObject.Find("GameHUD/Controls");
            if (hints == null) Debug.LogWarning($"[TouchControlsBuilder] {scenePath}: no GameHUD/Controls keyboard hints found.");
            var controls = instance.GetComponent<TouchControls>();
            SetRefs(controls, ("keyboardHints", hints));
            PrefabUtility.RecordPrefabInstancePropertyModifications(controls);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
    }
}
