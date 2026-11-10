using System;
using UnityEngine;
using UnityEditor;
using Object = UnityEngine.Object;

namespace Contract.EditorTools
{
    /// <summary>
    /// Generates placeholder UI sprites, icons, and font assets.
    /// Run: CONTRACT/Setup/Create UI Assets
    /// </summary>
    public static class UIAssetsGenerator
    {
        private const string IconsPath = "Assets/UI/Icons/";
        private const string SpritesPath = "Assets/UI/Sprites/";
        private const string FontsPath = "Assets/UI/Fonts/";

        [MenuItem("CONTRACT/Setup/Create UI Assets")]
        public static void CreateAllUIAssets()
        {
            CreateFolders();
            CreateIcons();
            CreateSprites();
            CreateFontSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[CONTRACT] UI assets created successfully.");
        }

        private static void CreateFolders()
        {
            string[] folders = { IconsPath, SpritesPath, FontsPath };
            foreach (string folder in folders)
            {
                if (!AssetDatabase.IsValidFolder(folder))
                {
                    string parent = System.IO.Path.GetDirectoryName(folder.TrimEnd('/'));
                    string name = System.IO.Path.GetFileName(folder.TrimEnd('/'));
                    AssetDatabase.CreateFolder(parent, name);
                }
            }
        }

        private static void CreateIcons()
        {
            // Player arrow (blue/green)
            CreateIcon(IconsPath + "PlayerArrow.png", 32, 32, (x, y) => {
                float cx = 16, cy = 16;
                float dx = x - cx, dy = y - cy;
                float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                if (dist < 14 && angle > -45 && angle < 45) return new Color(0.2f, 0.8f, 1f, 1f);
                return Color.clear;
            }, "Player Arrow");

            // Mission subject (red dot)
            CreateIcon(IconsPath + "MissionSubject.png", 24, 24, (x, y) => {
                float cx = 12, cy = 12;
                float dx = x - cx, dy = y - cy;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                if (dist < 10) return new Color(1f, 0.2f, 0.2f, 1f);
                return Color.clear;
            }, "Mission Subject");

            // Objective marker (yellow diamond)
            CreateIcon(IconsPath + "Objective.png", 24, 24, (x, y) => {
                float cx = 12, cy = 12;
                float dx = Mathf.Abs(x - cx), dy = Mathf.Abs(y - cy);
                if (dx + dy < 10) return new Color(1f, 0.9f, 0.2f, 1f);
                return Color.clear;
            }, "Objective");

            // Safehouse (green house)
            CreateIcon(IconsPath + "Safehouse.png", 24, 24, (x, y) => {
                float cx = 12, cy = 16;
                float dx = Mathf.Abs(x - cx), dy = cy - y;
                if (dx < 8 && dy > 0 && dy < 12 && dx < dy) return new Color(0.2f, 1f, 0.3f, 1f);
                if (y > 16 && Mathf.Abs(x - cx) < 4) return new Color(0.2f, 1f, 0.3f, 1f);
                return Color.clear;
            }, "Safehouse");

            // Extraction (blue helicopter)
            CreateIcon(IconsPath + "Extraction.png", 24, 24, (x, y) => {
                float cx = 12, cy = 12;
                float dx = x - cx, dy = y - cy;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                if (dist < 10) return new Color(0.2f, 0.6f, 1f, 1f);
                return Color.clear;
            }, "Extraction");

            // NPC (gray circle)
            CreateIcon(IconsPath + "NPC.png", 20, 20, (x, y) => {
                float cx = 10, cy = 10;
                float dx = x - cx, dy = y - cy;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                if (dist < 8) return new Color(0.7f, 0.7f, 0.7f, 1f);
                return Color.clear;
            }, "NPC");

            // Completed mission (green check)
            CreateIcon(IconsPath + "Completed.png", 24, 24, (x, y) => {
                float cx = 12, cy = 12;
                float dx = x - cx, dy = y - cy;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                if (dist < 10) return new Color(0.2f, 1f, 0.3f, 1f);
                return Color.clear;
            }, "Completed");

            // Interaction prompt (E key)
            CreateIcon(IconsPath + "InteractPrompt.png", 64, 32, (x, y) => {
                if (x < 10 || x > 54 || y < 4 || y > 28) return Color.clear;
                // Draw [E]
                if (x < 16 && y > 8 && y < 24) return Color.white; // [
                if (x > 48 && y > 8 && y < 24) return Color.white; // ]
                if (x >= 16 && x <= 48 && y >= 10 && y <= 22) return Color.white; // E
                return Color.clear;
            }, "Interact Prompt");

            // Settings gear
            CreateIcon(IconsPath + "Settings.png", 24, 24, (x, y) => {
                float cx = 12, cy = 12;
                float dx = x - cx, dy = y - cy;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                if (dist < 10 && dist > 6) return new Color(1f, 0.8f, 0.2f, 1f);
                return Color.clear;
            }, "Settings");

            // Pause
            CreateIcon(IconsPath + "Pause.png", 24, 24, (x, y) => {
                if (x >= 6 && x <= 10 && y >= 4 && y <= 20) return Color.white;
                if (x >= 14 && x <= 18 && y >= 4 && y <= 20) return Color.white;
                return Color.clear;
            }, "Pause");

            // Play
            CreateIcon(IconsPath + "Play.png", 24, 24, (x, y) => {
                float cx = 12, cy = 12;
                if (x >= cx - 4 && x <= cx + 4) {
                    float t = (x - (cx - 4)) / 8f;
                    if (y >= 4 + t * 8 && y <= 20 - t * 8) return Color.white;
                }
                return Color.clear;
            }, "Play");

            // Volume icons
            CreateIcon(IconsPath + "VolumeHigh.png", 24, 24, (x, y) => {
                float cx = 12, cy = 12;
                if (x >= 4 && x <= 10 && y >= 6 && y <= 18) return Color.white; // speaker
                if (x >= 12 && x <= 16 && y >= 4 && y <= 20) return Color.white; // wave 1
                if (x >= 16 && x <= 20 && y >= 2 && y <= 22) return Color.white; // wave 2
                return Color.clear;
            }, "Volume High");

            CreateIcon(IconsPath + "VolumeLow.png", 24, 24, (x, y) => {
                float cx = 12, cy = 12;
                if (x >= 4 && x <= 10 && y >= 6 && y <= 18) return Color.white; // speaker
                if (x >= 12 && x <= 16 && y >= 8 && y <= 16) return Color.white; // wave 1
                return Color.clear;
            }, "Volume Low");

            CreateIcon(IconsPath + "VolumeMute.png", 24, 24, (x, y) => {
                float cx = 12, cy = 12;
                if (x >= 4 && x <= 10 && y >= 6 && y <= 18) return Color.white; // speaker
                if (x >= 14 && x <= 20 && y >= 4 && y <= 20) return Color.white; // X
                return Color.clear;
            }, "Volume Mute");
        }

        private static void CreateSprites()
        {
            // Panel background (dark with border)
            CreateSprite(SpritesPath + "PanelBG.png", 256, 128, (x, y) => {
                if (x < 2 || x > 254 || y < 2 || y > 126) return new Color(0.1f, 0.1f, 0.15f, 1f); // border
                return new Color(0.05f, 0.05f, 0.1f, 0.95f); // fill
            }, "Panel Background");

            // Button normal
            CreateSprite(SpritesPath + "ButtonNormal.png", 200, 50, (x, y) => {
                if (x < 1 || x > 199 || y < 1 || y > 49) return new Color(0.3f, 0.3f, 0.4f, 1f);
                return new Color(0.15f, 0.15f, 0.2f, 1f);
            }, "Button Normal");

            // Button hover
            CreateSprite(SpritesPath + "ButtonHover.png", 200, 50, (x, y) => {
                if (x < 1 || x > 199 || y < 1 || y > 49) return new Color(0.6f, 0.2f, 0.2f, 1f);
                return new Color(0.25f, 0.1f, 0.1f, 1f);
            }, "Button Hover");

            // Button pressed
            CreateSprite(SpritesPath + "ButtonPressed.png", 200, 50, (x, y) => {
                if (x < 1 || x > 199 || y < 1 || y > 49) return new Color(0.8f, 0.3f, 0.3f, 1f);
                return new Color(0.3f, 0.15f, 0.15f, 1f);
            }, "Button Pressed");

            // Slider background
            CreateSprite(SpritesPath + "SliderBG.png", 200, 20, (x, y) => {
                if (y < 8 || y > 12) return Color.clear;
                return new Color(0.2f, 0.2f, 0.25f, 1f);
            }, "Slider Background");

            // Slider fill
            CreateSprite(SpritesPath + "SliderFill.png", 200, 20, (x, y) => {
                if (y < 8 || y > 12) return Color.clear;
                return new Color(0.8f, 0.2f, 0.2f, 1f);
            }, "Slider Fill");

            // Slider handle
            CreateSprite(SpritesPath + "SliderHandle.png", 20, 20, (x, y) => {
                float cx = 10, cy = 10;
                float dx = x - cx, dy = y - cy;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                if (dist < 10) return new Color(0.9f, 0.3f, 0.3f, 1f);
                return Color.clear;
            }, "Slider Handle");

            // Progress bar background
            CreateSprite(SpritesPath + "ProgressBG.png", 300, 30, (x, y) => {
                if (x < 2 || x > 298 || y < 2 || y > 28) return new Color(0.2f, 0.2f, 0.25f, 1f);
                return new Color(0.1f, 0.1f, 0.15f, 1f);
            }, "Progress Background");

            // Progress bar fill
            CreateSprite(SpritesPath + "ProgressFill.png", 300, 30, (x, y) => {
                if (x < 2 || x > 298 || y < 2 || y > 28) return Color.clear;
                return new Color(0.8f, 0.2f, 0.2f, 1f);
            }, "Progress Fill");

            // Tooltip background
            CreateSprite(SpritesPath + "TooltipBG.png", 300, 80, (x, y) => {
                if (x < 2 || x > 298 || y < 2 || y > 78) return new Color(0.1f, 0.1f, 0.15f, 1f);
                return new Color(0.05f, 0.05f, 0.1f, 0.95f);
            }, "Tooltip Background");

            // Notification background
            CreateSprite(SpritesPath + "NotificationBG.png", 400, 100, (x, y) => {
                if (x < 2 || x > 398 || y < 2 || y > 98) return new Color(0.8f, 0.2f, 0.2f, 1f);
                return new Color(0.1f, 0.05f, 0.05f, 0.95f);
            }, "Notification Background");

            // Minimap background
            CreateSprite(SpritesPath + "MinimapBG.png", 256, 256, (x, y) => {
                float cx = 128, cy = 128;
                float dx = x - cx, dy = y - cy;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                if (dist > 128) return Color.clear;
                return new Color(0.02f, 0.02f, 0.03f, 1f);
            }, "Minimap Background");

            // Map marker ring
            CreateSprite(SpritesPath + "MarkerRing.png", 32, 32, (x, y) => {
                float cx = 16, cy = 16;
                float dx = x - cx, dy = y - cy;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                if (dist > 14 && dist < 16) return Color.white;
                return Color.clear;
            }, "Marker Ring");
        }

        private static void CreateFontSettings()
        {
            // Create a font settings asset for TMP
            string fontSettingsPath = FontsPath + "TMP_FontSettings.asset";
            if (!AssetDatabase.IsValidFolder(FontsPath))
            {
                AssetDatabase.CreateFolder("Assets/UI", "Fonts");
            }
            
            // This creates a reference to the default LiberationSans SDF font
            // Actual font assets would be imported separately
            Debug.Log("[CONTRACT] Font settings placeholder created. Import TMP fonts via Window > TextMeshPro > Import TMP Essential Resources.");
        }

        private static void CreateIcon(string path, int width, int height, Func<int, int, Color> pixelFunc, string name)
        {
            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            
            Color[] pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    pixels[y * width + x] = pixelFunc(x, y);
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();

            string folder = System.IO.Path.GetDirectoryName(path);
            if (!AssetDatabase.IsValidFolder(folder))
            {
                string[] parts = folder.Split('/');
                string current = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    string next = current + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(next))
                    {
                        AssetDatabase.CreateFolder(current, parts[i]);
                    }
                    current = next;
                }
            }

            byte[] pngData = tex.EncodeToPNG();
            System.IO.File.WriteAllBytes(path, pngData);
            AssetDatabase.ImportAsset(path);
            
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.filterMode = FilterMode.Bilinear;
                importer.spritePixelsPerUnit = 100;
                importer.mipmapEnabled = false;
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
            
            Object.DestroyImmediate(tex);
        }

        private static void CreateSprite(string path, int width, int height, Func<int, int, Color> pixelFunc, string name)
        {
            CreateIcon(path, width, height, pixelFunc, name);
        }
    }
}