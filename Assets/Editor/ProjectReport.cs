using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Contract.Editor
{
    /// <summary>Prints the active build target, quality levels and render pipeline (headless sanity check).</summary>
    public static class ProjectReport
    {
        [MenuItem("CONTRACT/Tools/Print Project Report")]
        public static void Print()
        {
            Debug.Log($"[ProjectReport] Active target: {EditorUserBuildSettings.activeBuildTarget}; " +
                      $"quality levels: {string.Join(", ", QualitySettings.names)}; current: {QualitySettings.names[QualitySettings.GetQualityLevel()]}; " +
                      $"pipeline: {(GraphicsSettings.currentRenderPipeline != null ? GraphicsSettings.currentRenderPipeline.name : "none")}; " +
                      $"color space: {PlayerSettings.colorSpace}");
        }
    }
}
