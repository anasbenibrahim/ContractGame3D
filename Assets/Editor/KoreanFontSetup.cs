using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Contract.Editor
{
    /// <summary>
    /// Creates a dynamic TextMeshPro font asset from Noto Sans KR (SIL OFL 1.1) and registers it
    /// as a global TMP fallback, so Hangul renders in every existing TMP text without per-label changes.
    /// Headless: -executeMethod Contract.Editor.KoreanFontSetup.Setup
    /// </summary>
    public static class KoreanFontSetup
    {
        private const string SourceFontPath = "Assets/UI/Fonts/NotoSansKR/NotoSansKR-Variable.ttf";
        private const string FontAssetPath = "Assets/UI/Fonts/NotoSansKR/NotoSansKR SDF.asset";
        private const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

        [MenuItem("CONTRACT/Setup/Korean Font Fallback")]
        public static void Setup()
        {
            Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
            if (sourceFont == null)
            {
                Debug.LogError($"[KoreanFontSetup] Source font not found at {SourceFontPath}.");
                return;
            }

            TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            if (fontAsset == null)
            {
                // Dynamic atlas: glyphs are rasterised on demand, so the full Hangul set
                // does not need to be baked up front. Multi-atlas lets it grow if needed.
                fontAsset = TMP_FontAsset.CreateFontAsset(
                    sourceFont, 64, 6, GlyphRenderMode.SDFAA, 1024, 1024,
                    AtlasPopulationMode.Dynamic, true);
                fontAsset.name = "NotoSansKR SDF";

                AssetDatabase.CreateAsset(fontAsset, FontAssetPath);

                Texture2D atlas = fontAsset.atlasTexture;
                atlas.name = "NotoSansKR SDF Atlas";
                AssetDatabase.AddObjectToAsset(atlas, fontAsset);

                Material material = fontAsset.material;
                material.name = "NotoSansKR SDF Material";
                AssetDatabase.AddObjectToAsset(material, fontAsset);
            }

            RegisterGlobalFallback(fontAsset);
            AssetDatabase.SaveAssets();
            Debug.Log($"[KoreanFontSetup] Korean fallback ready: {FontAssetPath} (test glyph '서' present: {fontAsset.HasCharacter('서', false, true)}).");
        }

        private static void RegisterGlobalFallback(TMP_FontAsset fontAsset)
        {
            Object settings = AssetDatabase.LoadAssetAtPath<Object>(TmpSettingsPath);
            if (settings == null)
            {
                Debug.LogError($"[KoreanFontSetup] TMP Settings not found at {TmpSettingsPath}.");
                return;
            }

            SerializedObject serialized = new SerializedObject(settings);
            SerializedProperty fallbacks = serialized.FindProperty("m_fallbackFontAssets");
            for (int i = 0; i < fallbacks.arraySize; i++)
            {
                if (fallbacks.GetArrayElementAtIndex(i).objectReferenceValue == fontAsset)
                {
                    return;
                }
            }

            fallbacks.InsertArrayElementAtIndex(fallbacks.arraySize);
            fallbacks.GetArrayElementAtIndex(fallbacks.arraySize - 1).objectReferenceValue = fontAsset;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
        }
    }
}
