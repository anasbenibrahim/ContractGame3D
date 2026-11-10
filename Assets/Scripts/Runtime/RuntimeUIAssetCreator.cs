using System.IO;
using UnityEngine;

public class RuntimeUIAssetCreator : MonoBehaviour
{
    // Called when the script starts
    private void Start()
    {
        CreateSimpleUIAssets();
    }

    private void CreateSimpleUIAssets()
    {
        // Define paths
        string iconsPath = Path.Combine(Application.dataPath, "UI/Icons");
        string spritesPath = Path.Combine(Application.dataPath, "UI/Sprites");

        // Ensure directories exist
        Directory.CreateDirectory(iconsPath);
        Directory.CreateDirectory(spritesPath);

        // Create a simple panel background texture (dark)
        CreateTexture("PanelBG.png", spritesPath, 256, 128, (x, y) =>
        {
            if (x < 2 || x > 254 || y < 2 || y > 126)
                return new Color(0.1f, 0.1f, 0.15f, 1f); // border
            return new Color(0.05f, 0.05f, 0.1f, 0.95f); // fill
        });

        // Create a simple button normal texture
        CreateTexture("ButtonNormal.png", spritesPath, 200, 50, (x, y) =>
        {
            if (x < 1 || x > 199 || y < 1 || y > 49)
                return new Color(0.3f, 0.3f, 0.4f, 1f);
            return new Color(0.15f, 0.15f, 0.2f, 1f);
        });

        // Create a simple button hover texture
        CreateTexture("ButtonHover.png", spritesPath, 200, 50, (x, y) =>
        {
            if (x < 1 || x > 199 || y < 1 || y > 49)
                return new Color(0.6f, 0.2f, 0.2f, 1f);
            return new Color(0.25f, 0.1f, 0.1f, 1f);
        });

        // Create a simple button pressed texture
        CreateTexture("ButtonPressed.png", spritesPath, 200, 50, (x, y) =>
        {
            if (x < 1 || x > 199 || y < 1 || y > 49)
                return new Color(0.8f, 0.3f, 0.3f, 1f);
            return new Color(0.3f, 0.15f, 0.15f, 1f);
        });

        // Create a simple slider fill texture
        CreateTexture("SliderFill.png", spritesPath, 200, 20, (x, y) =>
        {
            if (y < 8 || y > 12)
                return Color.clear;
            return new Color(0.8f, 0.2f, 0.2f, 1f);
        });

        Debug.Log("[RuntimeUIAssetCreator] Simple UI assets generated.");
    }

    private void CreateTexture(string fileName, string folderPath, int width, int height, System.Func<int, int, Color> pixelFunc)
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

        string fullPath = Path.Combine(folderPath, fileName);
        byte[] pngData = tex.EncodeToPNG();
        File.WriteAllBytes(fullPath, pngData);
        DestroyImmediate(tex);
    }
}