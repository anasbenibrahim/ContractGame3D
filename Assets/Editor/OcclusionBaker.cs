using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Contract.Editor
{
    /// <summary>
    /// Stage 10: bakes occlusion culling for the scenes that show the city at street level
    /// (GameWorld, MainMenu), so buildings hide what is behind them from the GPU.
    /// Run after the world builders. Headless: -executeMethod Contract.Editor.OcclusionBaker.Bake
    /// </summary>
    public static class OcclusionBaker
    {
        [MenuItem("CONTRACT/Setup/Bake Occlusion Culling")]
        public static void Bake()
        {
            foreach (string path in new[] { "Assets/Scenes/GameWorld.unity", "Assets/Scenes/Myeongdong.unity", "Assets/Scenes/MainMenu.unity" })
            {
                EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                StaticOcclusionCulling.smallestOccluder = 4f;   // shopfronts and buildings, not benches
                StaticOcclusionCulling.smallestHole = 0.3f;
                StaticOcclusionCulling.backfaceThreshold = 100f;
                float start = Time.realtimeSinceStartup;
                bool ok = StaticOcclusionCulling.Compute();
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
                string data = System.IO.Path.ChangeExtension(path, null) + "/OcclusionCullingData.asset";
                long bytes = System.IO.File.Exists(data) ? new System.IO.FileInfo(data).Length : 0;
                Debug.Log($"[OcclusionBaker] {path}: {(ok && bytes > 0 ? "baked" : "FAILED")} in {Time.realtimeSinceStartup - start:F0} s, data {bytes / 1024} KB");
            }
        }
    }
}
