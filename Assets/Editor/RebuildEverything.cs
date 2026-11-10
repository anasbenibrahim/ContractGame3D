using System.Diagnostics;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace Contract.Editor
{
    /// <summary>
    /// Regenerates all generated content in dependency order: data, upgrades, audio, the Gangnam
    /// district, its population and navmesh, the map, the mission / terminal UI, the menu scenes
    /// and HUD, quality tiers and occlusion culling. Takes several minutes.
    /// Headless: -executeMethod Contract.Editor.RebuildEverything.Run
    /// </summary>
    public static class RebuildEverything
    {
        [MenuItem("CONTRACT/Rebuild Everything", priority = 0)]
        public static void Run()
        {
            Step("Game database", GameDatabaseBuilder.Build);
            Step("Upgrade catalogue", UpgradeCatalogBuilder.Build);
            Step("Audio", AudioSynthBuilder.Build);
            Step("Activity config", ActivityAssetsBuilder.Build);
            Step("Gangnam district", World.GangnamDistrictBuilder.Build);
            Step("Population + navmesh", World.GangnamPopulationBuilder.Build);
            Step("GPS walking route", World.GpsRouteBuilder.Build);
            Step("Map", World.GangnamMapBuilder.Build);
            Step("Mission + terminal UI", World.MissionUIBuilder.Build);
            Step("Menus + HUD", World.MenuScenesBuilder.Build);
            Step("GPS activity UI", World.ActivityUIBuilder.Build);
            Step("Touch controls", World.TouchControlsBuilder.Build);
            Step("Myeongdong district", World.MyeongdongBuilder.Build);   // copies the finished GameWorld
            Step("Destinations", World.DestinationsBuilder.Build);
            Step("World selection", World.MenuScenesBuilder.BuildWorldSelectionScene);
            Step("Quality tiers", QualityTierSetup.Apply);
            Step("Android presets + LOD culling", AndroidOptimizationSetup.Apply);
            Step("Occlusion culling", OcclusionBaker.Bake);
            Debug.Log("[RebuildEverything] Done.");
        }

        private static void Step(string name, System.Action action)
        {
            var watch = Stopwatch.StartNew();
            EditorUtility.DisplayProgressBar("CONTRACT: Rebuild Everything", name, 0f);
            try
            {
                action();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            Debug.Log($"[RebuildEverything] {name}: {watch.Elapsed.TotalSeconds:F0} s");
        }
    }
}
