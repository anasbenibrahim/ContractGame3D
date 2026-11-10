using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Contract.EditorTools
{
    [InitializeOnLoad]
    public static class ContractStage3Setup
    {
        private const string MarkerPath = "ProjectSettings/ContractStage3.done";
        private const string Stage1MarkerPath = "ProjectSettings/ContractStage1.done";
        private const string Stage2MarkerPath = "ProjectSettings/ContractStage2.done";
        private const string GameWorldPath = "Assets/Scenes/GameWorld.unity";
        private static readonly Vector3 SpawnPosition = new Vector3(0f, 0.1f, -49f);

        private static bool isRunning;

        private sealed class CityMaterials
        {
            public Material Ground;
            public Material Asphalt;
            public Material Concrete;
            public Material BuildingSlate;
            public Material BuildingSteel;
            public Material Metal;
            public Material Windows;
            public Material NeonCyan;
            public Material NeonMagenta;
            public Material ExtractionGreen;
            public Material WarningYellow;
            public Material Foliage;
            public Material RoadMarking;
            public Material Crate;
        }

        static ContractStage3Setup()
        {
            EditorApplication.delayCall += AutoSetup;
        }

        [MenuItem("CONTRACT/Setup/Rebuild Stage 3 City")]
        public static void Rebuild()
        {
            if (File.Exists(MarkerPath))
            {
                File.Delete(MarkerPath);
            }

            RunSetup();
        }

        private static void AutoSetup()
        {
            if (Application.isBatchMode || File.Exists(MarkerPath) || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += AutoSetup;
                return;
            }

            if (!File.Exists(Stage1MarkerPath) || !File.Exists(Stage2MarkerPath) || !File.Exists(GameWorldPath))
            {
                EditorApplication.delayCall += AutoSetup;
                return;
            }

            RunSetup();
        }

        private static void RunSetup()
        {
            if (isRunning)
            {
                return;
            }

            isRunning = true;
            try
            {
                Debug.Log("[CONTRACT] Stage 3: building downtown city environment...");
                EnsureFolder("Assets/Art/Materials");

                CityMaterials materials = CreateCityMaterials();
                if (materials == null)
                {
                    Debug.LogError("[CONTRACT] Stage 3 requires the URP Lit shader and city materials.");
                    return;
                }

                int environmentLayer = LayerMask.NameToLayer("Environment");
                if (environmentLayer < 0)
                {
                    Debug.LogError("[CONTRACT] Stage 3 requires the Environment layer. Run Stage 1 setup first.");
                    return;
                }

                Scene scene = EditorSceneManager.OpenScene(GameWorldPath, OpenSceneMode.Single);
                DestroyRoot(scene, "Environment");
                DestroyRoot(scene, "WorldMarkers");
                DestroyRoot(scene, "WorldBounds");

                GameObject environment = new GameObject("Environment");
                SceneManager.MoveGameObjectToScene(environment, scene);
                BuildGround(environment.transform, materials);
                BuildStreets(environment.transform, materials);
                BuildBuildings(environment.transform, materials);
                BuildBackgroundTowers(environment.transform, materials);
                BuildStreetlights(environment.transform, materials);
                BuildProps(environment.transform, materials);
                BuildSafehouse(environment.transform, materials);
                BuildExtractionZone(environment.transform, materials);
                GameObject spawn = BuildSpawn(environment.transform, environmentLayer);
                SetLayerRecursively(environment, environmentLayer);

                BuildWorldBounds(scene, environmentLayer);
                BuildWorldMarkers(scene, environmentLayer);

                if (!PositionPlayer(scene, spawn))
                {
                    Debug.LogError("[CONTRACT] Stage 3 could not position the existing player.");
                    return;
                }

                ConfigureLighting(scene);
                ConfigureCamera(scene);
                UpdateOverlay();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                File.WriteAllText(MarkerPath, $"Stage 3 completed {DateTime.UtcNow:O}\n");
                Debug.Log("[CONTRACT] Stage 3 city environment generated successfully.");
            }
            catch (Exception exception)
            {
                Debug.LogError($"[CONTRACT] Stage 3 setup failed: {exception}");
            }
            finally
            {
                isRunning = false;
            }
        }

        private static CityMaterials CreateCityMaterials()
        {
            Material ground = CreateMaterial(
                "Assets/Art/Materials/Ground_Dark.mat",
                new Color(0.035f, 0.04f, 0.05f),
                0.1f,
                0.3f);
            Material asphalt = CreateMaterial(
                "Assets/Art/Materials/Road_Asphalt.mat",
                new Color(0.025f, 0.028f, 0.035f),
                0.15f,
                0.25f);
            Material concrete = CreateMaterial(
                "Assets/Art/Materials/Sidewalk_Concrete.mat",
                new Color(0.17f, 0.18f, 0.2f),
                0.05f,
                0.35f);
            Material buildingSlate = CreateMaterial(
                "Assets/Art/Materials/Building_Slate.mat",
                new Color(0.055f, 0.065f, 0.09f),
                0.35f,
                0.45f);
            Material buildingSteel = CreateMaterial(
                "Assets/Art/Materials/Building_Steel.mat",
                new Color(0.09f, 0.105f, 0.13f),
                0.55f,
                0.55f);
            Material metal = CreateMaterial(
                "Assets/Art/Materials/Metal_Dark.mat",
                new Color(0.03f, 0.035f, 0.045f),
                0.8f,
                0.65f);
            Material windows = CreateMaterial(
                "Assets/Art/Materials/Window_Warm.mat",
                new Color(0.3f, 0.2f, 0.07f),
                0.2f,
                0.55f,
                new Color(2.4f, 1.5f, 0.55f));
            Material neonCyan = CreateMaterial(
                "Assets/Art/Materials/Neon_Cyan.mat",
                new Color(0.02f, 0.2f, 0.3f),
                0.1f,
                0.7f,
                new Color(0.15f, 2.8f, 4.5f));
            Material neonMagenta = CreateMaterial(
                "Assets/Art/Materials/Neon_Magenta.mat",
                new Color(0.3f, 0.02f, 0.18f),
                0.1f,
                0.7f,
                new Color(4.2f, 0.15f, 2.6f));
            Material extractionGreen = CreateMaterial(
                "Assets/Art/Materials/Extraction_Green.mat",
                new Color(0.02f, 0.3f, 0.08f),
                0.1f,
                0.7f,
                new Color(0.15f, 4.2f, 0.8f));
            Material warningYellow = CreateMaterial(
                "Assets/Art/Materials/Warning_Yellow.mat",
                new Color(0.35f, 0.25f, 0.02f),
                0.1f,
                0.55f,
                new Color(3.4f, 1.8f, 0.1f));
            Material foliage = CreateMaterial(
                "Assets/Art/Materials/Foliage_Dark.mat",
                new Color(0.02f, 0.07f, 0.035f),
                0.05f,
                0.25f);
            Material roadMarking = CreateMaterial(
                "Assets/Art/Materials/Road_Marking.mat",
                new Color(0.62f, 0.64f, 0.68f),
                0.05f,
                0.4f);
            Material crate = CreateMaterial(
                "Assets/Art/Materials/Prop_Crate.mat",
                new Color(0.18f, 0.12f, 0.06f),
                0.1f,
                0.25f);

            Material[] created =
            {
                ground, asphalt, concrete, buildingSlate, buildingSteel, metal, windows,
                neonCyan, neonMagenta, extractionGreen, warningYellow, foliage, roadMarking, crate
            };

            foreach (Material material in created)
            {
                if (material == null)
                {
                    return null;
                }
            }

            return new CityMaterials
            {
                Ground = ground,
                Asphalt = asphalt,
                Concrete = concrete,
                BuildingSlate = buildingSlate,
                BuildingSteel = buildingSteel,
                Metal = metal,
                Windows = windows,
                NeonCyan = neonCyan,
                NeonMagenta = neonMagenta,
                ExtractionGreen = extractionGreen,
                WarningYellow = warningYellow,
                Foliage = foliage,
                RoadMarking = roadMarking,
                Crate = crate
            };
        }

        private static void BuildGround(Transform parent, CityMaterials materials)
        {
            CreatePrimitive(
                PrimitiveType.Plane,
                "Ground",
                parent,
                Vector3.zero,
                new Vector3(12f, 1f, 12f),
                materials.Ground);
        }

        private static void BuildStreets(Transform parent, CityMaterials materials)
        {
            CreatePrimitive(
                PrimitiveType.Cube,
                "Road",
                parent,
                new Vector3(0f, 0.02f, 0f),
                new Vector3(16f, 0.04f, 110f),
                materials.Asphalt);
            CreatePrimitive(
                PrimitiveType.Cube,
                "Sidewalk L",
                parent,
                new Vector3(-10f, 0.09f, 0f),
                new Vector3(4f, 0.18f, 110f),
                materials.Concrete);
            CreatePrimitive(
                PrimitiveType.Cube,
                "Sidewalk R",
                parent,
                new Vector3(10f, 0.09f, 0f),
                new Vector3(4f, 0.18f, 110f),
                materials.Concrete);

            for (int i = 0; i < 18; i++)
            {
                float z = -51f + i * 6f;
                CreatePrimitive(
                    PrimitiveType.Cube,
                    $"Lane_Center_{i + 1:00}",
                    parent,
                    new Vector3(0f, 0.045f, z),
                    new Vector3(0.16f, 0.012f, 3f),
                    materials.RoadMarking,
                    false);
            }

            CreatePrimitive(
                PrimitiveType.Cube,
                "Lane_Side_L",
                parent,
                new Vector3(-5.6f, 0.045f, 0f),
                new Vector3(0.12f, 0.012f, 104f),
                materials.RoadMarking,
                false);
            CreatePrimitive(
                PrimitiveType.Cube,
                "Lane_Side_R",
                parent,
                new Vector3(5.6f, 0.045f, 0f),
                new Vector3(0.12f, 0.012f, 104f),
                materials.RoadMarking,
                false);

            BuildCrosswalk(parent, materials, -46f);
            BuildCrosswalk(parent, materials, 43f);
        }

        private static void BuildCrosswalk(Transform parent, CityMaterials materials, float z)
        {
            for (int i = -3; i <= 3; i++)
            {
                CreatePrimitive(
                    PrimitiveType.Cube,
                    $"Crosswalk_{z:0}_{i + 4}",
                    parent,
                    new Vector3(i * 1.8f, 0.045f, z),
                    new Vector3(0.9f, 0.012f, 3.2f),
                    materials.RoadMarking,
                    false);
            }
        }

        private static void BuildBuildings(Transform parent, CityMaterials materials)
        {
            float[] leftZ = { -35f, -18f, 2f, 21f, 40f };
            float[] leftHeight = { 16f, 24f, 12f, 28f, 18f };
            float[] rightZ = { -35f, -16f, 4f, 23f, 42f };
            float[] rightHeight = { 20f, 13f, 26f, 16f, 22f };

            for (int i = 0; i < leftZ.Length; i++)
            {
                CreateBuilding(
                    parent,
                    materials,
                    $"Building_Main_L_{i + 1:00}",
                    new Vector3(-19f, 0f, leftZ[i]),
                    new Vector2(10f, 14f),
                    leftHeight[i],
                    i % 2 == 0 ? materials.NeonCyan : materials.NeonMagenta,
                    i % 2 == 0);
            }

            for (int i = 0; i < rightZ.Length; i++)
            {
                CreateBuilding(
                    parent,
                    materials,
                    $"Building_Main_R_{i + 1:00}",
                    new Vector3(19f, 0f, rightZ[i]),
                    new Vector2(10f, 14f),
                    rightHeight[i],
                    i % 2 == 0 ? materials.NeonMagenta : materials.NeonCyan,
                    i % 2 == 1);
            }
        }

        private static void CreateBuilding(
            Transform parent,
            CityMaterials materials,
            string name,
            Vector3 groundPosition,
            Vector2 footprint,
            float height,
            Material neonMaterial,
            bool addRoofUnit)
        {
            CreatePrimitive(
                PrimitiveType.Cube,
                name,
                parent,
                new Vector3(groundPosition.x, height * 0.5f, groundPosition.z),
                new Vector3(footprint.x, height, footprint.y),
                materials.BuildingSlate);

            int streetSide = groundPosition.x < 0f ? 1 : -1;
            float faceX = groundPosition.x + streetSide * (footprint.x * 0.5f + 0.03f);
            int floors = Mathf.Max(3, Mathf.FloorToInt(height / 3.6f));

            for (int floor = 1; floor < floors; floor++)
            {
                float y = height * (floor / (float)floors);
                Material windowMaterial = floor % 3 == 0 ? neonMaterial : materials.Windows;
                CreatePrimitive(
                    PrimitiveType.Cube,
                    $"Window_{name}_{floor:00}",
                    parent,
                    new Vector3(faceX, y, groundPosition.z),
                    new Vector3(0.08f, 0.32f, footprint.y * 0.82f),
                    windowMaterial,
                    false);
            }

            CreatePrimitive(
                PrimitiveType.Cube,
                $"RoofLip_{name}",
                parent,
                new Vector3(groundPosition.x, height + 0.12f, groundPosition.z),
                new Vector3(footprint.x + 0.35f, 0.24f, footprint.y + 0.35f),
                materials.BuildingSteel,
                false);

            CreatePrimitive(
                PrimitiveType.Cube,
                $"Accent_{name}",
                parent,
                new Vector3(faceX, height * 0.82f, groundPosition.z),
                new Vector3(0.1f, 0.28f, footprint.y * 0.72f),
                neonMaterial,
                false);

            if (addRoofUnit)
            {
                CreatePrimitive(
                    PrimitiveType.Cube,
                    $"Prop_AC_{name}",
                    parent,
                    new Vector3(groundPosition.x - streetSide * 2f, height + 0.7f, groundPosition.z + footprint.y * 0.22f),
                    new Vector3(2.1f, 1.2f, 1.6f),
                    materials.Metal);
            }
        }

        private static void BuildBackgroundTowers(Transform parent, CityMaterials materials)
        {
            float[] zPositions = { -42f, -14f, 14f, 42f };
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < zPositions.Length; i++)
                {
                    float height = 30f + ((i + (side > 0 ? 2 : 0)) % 4) * 8f;
                    float x = side * 38f;
                    CreatePrimitive(
                        PrimitiveType.Cube,
                        $"BackgroundTower_{side}_{i + 1:00}",
                        parent,
                        new Vector3(x, height * 0.5f, zPositions[i]),
                        new Vector3(10f, height, 10f),
                        materials.BuildingSteel);

                    int streetSide = side < 0 ? 1 : -1;
                    CreatePrimitive(
                        PrimitiveType.Cube,
                        $"Window_BackgroundTower_{side}_{i + 1:00}",
                        parent,
                        new Vector3(x + streetSide * 5.03f, height * 0.62f, zPositions[i]),
                        new Vector3(0.08f, 0.3f, 7.6f),
                        i % 2 == 0 ? materials.Windows : materials.NeonCyan,
                        false);
                }
            }
        }

        private static void BuildStreetlights(Transform parent, CityMaterials materials)
        {
            float[] zPositions = { -39f, -13f, 13f, 39f };
            foreach (float z in zPositions)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    float x = side * 9.1f;
                    GameObject root = CreateChild($"Streetlight_{side}_{z:0}", parent, new Vector3(x, 0f, z));
                    CreatePrimitive(
                        PrimitiveType.Cylinder,
                        "Pole",
                        root.transform,
                        new Vector3(0f, 2.75f, 0f),
                        new Vector3(0.16f, 2.75f, 0.16f),
                        materials.Metal);
                    CreatePrimitive(
                        PrimitiveType.Cube,
                        "Arm",
                        root.transform,
                        new Vector3(-side * 0.55f, 5.45f, 0f),
                        new Vector3(1.1f, 0.12f, 0.12f),
                        materials.Metal);
                    CreatePrimitive(
                        PrimitiveType.Sphere,
                        "Bulb",
                        root.transform,
                        new Vector3(-side * 0.95f, 5.35f, 0f),
                        new Vector3(0.35f, 0.35f, 0.35f),
                        materials.NeonCyan,
                        false);
                    AddPointLight(
                        root.transform,
                        "Light",
                        new Vector3(-side * 0.95f, 5.25f, 0f),
                        new Color(0.5f, 0.78f, 1f),
                        4f,
                        16f);
                }
            }
        }

        private static void BuildProps(Transform parent, CityMaterials materials)
        {
            GameObject root = CreateChild("Props", parent, Vector3.zero);

            Vector3[] cratePositions =
            {
                new Vector3(-10.7f, 0.65f, -30f),
                new Vector3(10.7f, 0.65f, -30f),
                new Vector3(-10.7f, 0.65f, -8f),
                new Vector3(10.7f, 0.65f, -8f),
                new Vector3(-10.7f, 0.65f, 16f),
                new Vector3(10.7f, 0.65f, 16f),
                new Vector3(-10.7f, 0.65f, 34f),
                new Vector3(10.7f, 0.65f, 34f)
            };
            for (int i = 0; i < cratePositions.Length; i++)
            {
                CreatePrimitive(
                    PrimitiveType.Cube,
                    $"Prop_Crate_{i + 1:00}",
                    root.transform,
                    cratePositions[i],
                    new Vector3(0.9f, 0.9f, 0.9f),
                    materials.Crate);
            }

            Vector3[] dumpsterPositions =
            {
                new Vector3(-10.9f, 0.8f, -22f),
                new Vector3(10.9f, 0.8f, 6f),
                new Vector3(-10.9f, 0.8f, 28f)
            };
            for (int i = 0; i < dumpsterPositions.Length; i++)
            {
                CreatePrimitive(
                    PrimitiveType.Cube,
                    $"Prop_Dumpster_{i + 1:00}",
                    root.transform,
                    dumpsterPositions[i],
                    new Vector3(1.4f, 1.3f, 2.1f),
                    materials.Metal);
            }

            Vector3[] planterPositions =
            {
                new Vector3(-9.7f, 0.35f, -37f),
                new Vector3(9.7f, 0.35f, -5f),
                new Vector3(-9.7f, 0.35f, 20f)
            };
            for (int i = 0; i < planterPositions.Length; i++)
            {
                CreatePrimitive(
                    PrimitiveType.Cube,
                    $"Prop_Planter_{i + 1:00}",
                    root.transform,
                    planterPositions[i],
                    new Vector3(1.4f, 0.7f, 2.4f),
                    materials.Concrete);
                CreatePrimitive(
                    PrimitiveType.Sphere,
                    $"Prop_Planter_{i + 1:00}_Foliage",
                    root.transform,
                    planterPositions[i] + new Vector3(0f, 0.55f, 0f),
                    new Vector3(1.0f, 0.6f, 1.9f),
                    materials.Foliage,
                    false);
            }

            float[] barrierX = { -6.6f, 6.6f };
            foreach (float x in barrierX)
            {
                CreatePrimitive(
                    PrimitiveType.Cube,
                    $"Prop_Barrier_{x:0}",
                    root.transform,
                    new Vector3(x, 0.55f, 42f),
                    new Vector3(2.4f, 1.0f, 0.3f),
                    materials.WarningYellow);
            }

            float[] pipeZ = { -35f, -18f, 2f, 21f, 40f };
            for (int i = 0; i < pipeZ.Length; i++)
            {
                CreatePrimitive(
                    PrimitiveType.Cube,
                    $"Prop_Pipe_L_{i + 1:00}",
                    root.transform,
                    new Vector3(-13.7f, 4.2f, pipeZ[i]),
                    new Vector3(0.25f, 7.2f, 0.25f),
                    materials.Metal,
                    false);
                CreatePrimitive(
                    PrimitiveType.Cube,
                    $"Prop_Pipe_R_{i + 1:00}",
                    root.transform,
                    new Vector3(13.7f, 4.2f, pipeZ[i]),
                    new Vector3(0.25f, 7.2f, 0.25f),
                    materials.Metal,
                    false);
            }
        }

        private static void BuildSafehouse(Transform parent, CityMaterials materials)
        {
            GameObject safehouse = CreateChild("Safehouse", parent, new Vector3(-16.5f, 0f, -49.5f));
            Transform root = safehouse.transform;

            CreatePrimitive(
                PrimitiveType.Cube,
                "Safehouse_Floor",
                root,
                new Vector3(0f, 0.1f, 0f),
                new Vector3(7.4f, 0.2f, 7.4f),
                materials.Concrete);
            CreatePrimitive(
                PrimitiveType.Cube,
                "Safehouse_Roof",
                root,
                new Vector3(0f, 3.3f, 0f),
                new Vector3(7.6f, 0.2f, 7.6f),
                materials.BuildingSlate);
            CreatePrimitive(
                PrimitiveType.Cube,
                "Safehouse_Wall_Back",
                root,
                new Vector3(0f, 1.7f, -3.6f),
                new Vector3(7.3f, 3.4f, 0.25f),
                materials.BuildingSlate);
            CreatePrimitive(
                PrimitiveType.Cube,
                "Safehouse_Wall_Left",
                root,
                new Vector3(-3.6f, 1.7f, 0f),
                new Vector3(0.25f, 3.4f, 7.3f),
                materials.BuildingSlate);
            CreatePrimitive(
                PrimitiveType.Cube,
                "Safehouse_Wall_Right_A",
                root,
                new Vector3(3.6f, 1.7f, -2.35f),
                new Vector3(0.25f, 3.4f, 2.3f),
                materials.BuildingSlate);
            CreatePrimitive(
                PrimitiveType.Cube,
                "Safehouse_Wall_Right_B",
                root,
                new Vector3(3.6f, 1.7f, 2.35f),
                new Vector3(0.25f, 3.4f, 2.3f),
                materials.BuildingSlate);
            CreatePrimitive(
                PrimitiveType.Cube,
                "Safehouse_Sign",
                root,
                new Vector3(3.7f, 2.9f, 0f),
                new Vector3(0.1f, 0.5f, 2.2f),
                materials.ExtractionGreen,
                false);
            CreatePrimitive(
                PrimitiveType.Cube,
                "Safehouse_Desk",
                root,
                new Vector3(-2.3f, 0.6f, -2.2f),
                new Vector3(1.9f, 0.8f, 0.8f),
                materials.Metal);
            CreatePrimitive(
                PrimitiveType.Cube,
                "Safehouse_Stool",
                root,
                new Vector3(-1.4f, 0.35f, -1.2f),
                new Vector3(0.55f, 0.5f, 0.55f),
                materials.Crate);
            CreatePrimitive(
                PrimitiveType.Cube,
                "Safehouse_Locker",
                root,
                new Vector3(2.2f, 1.1f, -2.8f),
                new Vector3(1.2f, 2f, 0.6f),
                materials.Metal);
            AddPointLight(
                root,
                "Safehouse_Light",
                new Vector3(0f, 2.8f, 0f),
                new Color(0.45f, 1f, 0.55f),
                3f,
                9f);
        }

        private static void BuildExtractionZone(Transform parent, CityMaterials materials)
        {
            GameObject zone = CreateChild("ExtractionZone", parent, new Vector3(0f, 0f, 47f));
            Transform root = zone.transform;

            CreatePrimitive(
                PrimitiveType.Cylinder,
                "ExtractionPad",
                root,
                new Vector3(0f, 0.1f, 0f),
                new Vector3(3.4f, 0.04f, 3.4f),
                materials.ExtractionGreen,
                false);
            CreatePrimitive(
                PrimitiveType.Cylinder,
                "ExtractionBeacon",
                root,
                new Vector3(0f, 2f, 0f),
                new Vector3(0.7f, 2f, 0.7f),
                materials.ExtractionGreen,
                false);
            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0 ? -1f : 1f) * 2.6f;
                float z = (i < 2 ? -1f : 1f) * 2.6f;
                CreatePrimitive(
                    PrimitiveType.Cube,
                    $"ExtractionPost_{i + 1}",
                    root,
                    new Vector3(x, 1.5f, z),
                    new Vector3(0.18f, 3f, 0.18f),
                    materials.Metal);
                CreatePrimitive(
                    PrimitiveType.Sphere,
                    $"ExtractionPost_{i + 1}_Light",
                    root,
                    new Vector3(x, 3.05f, z),
                    new Vector3(0.3f, 0.3f, 0.3f),
                    materials.ExtractionGreen,
                    false);
            }

            AddPointLight(
                root,
                "ExtractionLight",
                new Vector3(0f, 2.7f, 0f),
                new Color(0.25f, 1f, 0.45f),
                4f,
                12f);
        }

        private static GameObject BuildSpawn(Transform parent, int environmentLayer)
        {
            GameObject spawn = new GameObject("PlayerSpawn");
            spawn.transform.SetParent(parent, false);
            spawn.transform.localPosition = SpawnPosition;
            spawn.layer = environmentLayer;
            return spawn;
        }

        private static void BuildWorldBounds(Scene scene, int environmentLayer)
        {
            GameObject bounds = new GameObject("WorldBounds");
            SceneManager.MoveGameObjectToScene(bounds, scene);

            CreatePrimitive(
                PrimitiveType.Cube,
                "Boundary_North",
                bounds.transform,
                new Vector3(0f, 4f, 54f),
                new Vector3(112f, 8f, 1f),
                null);
            CreatePrimitive(
                PrimitiveType.Cube,
                "Boundary_South",
                bounds.transform,
                new Vector3(0f, 4f, -56f),
                new Vector3(112f, 8f, 1f),
                null);
            CreatePrimitive(
                PrimitiveType.Cube,
                "Boundary_East",
                bounds.transform,
                new Vector3(54f, 4f, -1f),
                new Vector3(1f, 8f, 112f),
                null);
            CreatePrimitive(
                PrimitiveType.Cube,
                "Boundary_West",
                bounds.transform,
                new Vector3(-54f, 4f, -1f),
                new Vector3(1f, 8f, 112f),
                null);

            foreach (Renderer renderer in bounds.GetComponentsInChildren<Renderer>(true))
            {
                renderer.enabled = false;
            }

            SetLayerRecursively(bounds, environmentLayer);
        }

        private static void BuildWorldMarkers(Scene scene, int environmentLayer)
        {
            int npcLayer = LayerMask.NameToLayer("NPC");
            int interactableLayer = LayerMask.NameToLayer("Interactable");
            int missionLayer = LayerMask.NameToLayer("MissionSubject");

            GameObject markers = new GameObject("WorldMarkers");
            SceneManager.MoveGameObjectToScene(markers, scene);
            markers.layer = environmentLayer;

            Transform root = markers.transform;
            CreateMarker(root, "SafehouseMarker", new Vector3(-12.8f, 0.1f, -49.5f), "Interactable", interactableLayer);
            CreateMarker(root, "MissionTarget_1", new Vector3(5f, 0.1f, 18f), "MissionSubject", missionLayer);
            CreateMarker(root, "MissionTarget_2", new Vector3(-5f, 0.1f, 35f), "MissionSubject", missionLayer);
            CreateMarker(root, "NPC_Spawn_1", new Vector3(-6f, 0.1f, -30f), "NPC", npcLayer);
            CreateMarker(root, "NPC_Spawn_2", new Vector3(6f, 0.1f, -10f), "NPC", npcLayer);
            CreateMarker(root, "NPC_Spawn_3", new Vector3(-5f, 0.1f, 12f), "NPC", npcLayer);
            CreateMarker(root, "NPC_Spawn_4", new Vector3(7f, 0.1f, 32f), "NPC", npcLayer);
            CreateMarker(root, "PatrolPoint_1", new Vector3(-5f, 0.1f, -30f), null, environmentLayer);
            CreateMarker(root, "PatrolPoint_2", new Vector3(5f, 0.1f, -20f), null, environmentLayer);
            CreateMarker(root, "PatrolPoint_3", new Vector3(-4f, 0.1f, 0f), null, environmentLayer);
            CreateMarker(root, "PatrolPoint_4", new Vector3(4f, 0.1f, 12f), null, environmentLayer);
            CreateMarker(root, "PatrolPoint_5", new Vector3(-6f, 0.1f, 28f), null, environmentLayer);
            CreateMarker(root, "PatrolPoint_6", new Vector3(5f, 0.1f, 40f), null, environmentLayer);

            GameObject extraction = CreateMarker(
                root,
                "ExtractionPoint",
                new Vector3(0f, 0.1f, 47f),
                "ExtractionPoint",
                interactableLayer);
            BoxCollider extractionTrigger = extraction.AddComponent<BoxCollider>();
            extractionTrigger.isTrigger = true;
            extractionTrigger.center = new Vector3(0f, 1.5f, 0f);
            extractionTrigger.size = new Vector3(7f, 3f, 7f);
        }

        private static GameObject CreateMarker(
            Transform parent,
            string name,
            Vector3 localPosition,
            string tag,
            int layer)
        {
            var marker = new GameObject(name);
            marker.transform.SetParent(parent, false);
            marker.transform.localPosition = localPosition;
            if (!string.IsNullOrEmpty(tag))
            {
                marker.tag = tag;
            }

            marker.layer = layer;
            return marker;
        }

        private static bool PositionPlayer(Scene scene, GameObject spawn)
        {
            GameObject player = FindRoot(scene, "Player");
            if (player == null)
            {
                return false;
            }

            CharacterController characterController = player.GetComponent<CharacterController>();
            if (characterController != null)
            {
                characterController.enabled = false;
            }

            player.transform.SetPositionAndRotation(spawn.transform.position, Quaternion.identity);

            if (characterController != null)
            {
                characterController.enabled = true;
            }

            GameObject cameraObject = FindRoot(scene, "Main Camera");
            if (cameraObject != null)
            {
                cameraObject.transform.SetPositionAndRotation(
                    spawn.transform.position + new Vector3(0.45f, 2.59f, -4.46f),
                    Quaternion.Euler(14f, 0f, 0f));
            }

            return true;
        }

        private static void ConfigureLighting(Scene scene)
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.035f, 0.045f, 0.065f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.055f, 0.07f, 0.105f);
            RenderSettings.fogDensity = 0.0035f;
            RenderSettings.skybox = null;

            GameObject sunObject = FindRoot(scene, "Directional Light");
            if (sunObject == null)
            {
                sunObject = new GameObject("Directional Light");
                SceneManager.MoveGameObjectToScene(sunObject, scene);
            }

            Light sun = sunObject.GetComponent<Light>();
            if (sun == null)
            {
                sun = sunObject.AddComponent<Light>();
            }

            sun.type = LightType.Directional;
            sun.color = new Color(0.75f, 0.82f, 1f);
            sun.intensity = 1.15f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;
            sunObject.transform.rotation = Quaternion.Euler(38f, -35f, 0f);
            RenderSettings.sun = sun;
        }

        private static void ConfigureCamera(Scene scene)
        {
            GameObject cameraObject = FindRoot(scene, "Main Camera");
            if (cameraObject == null)
            {
                return;
            }

            Camera camera = cameraObject.GetComponent<Camera>();
            if (camera == null)
            {
                return;
            }

            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.008f, 0.011f, 0.018f);
            camera.fieldOfView = 60f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 450f;
        }

        private static void UpdateOverlay()
        {
            foreach (TMP_Text text in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include))
            {
                switch (text.gameObject.name)
                {
                    case "Title":
                        text.text = "DOWNTOWN CITY";
                        break;
                    case "Subtitle":
                        text.text = "SECTOR 07 // CONTRACT ZONE";
                        break;
                    case "Stage Label":
                        text.text = "WASD MOVE | SHIFT SPRINT | CTRL CROUCH | SPACE JUMP | ESC PAUSE // STAGE 3";
                        break;
                }

                EditorUtility.SetDirty(text);
            }

            EnsureObjectiveLabel();
        }

        private static void EnsureObjectiveLabel()
        {
            GameObject canvasObject = GameObject.Find("UI Canvas");
            if (canvasObject == null)
            {
                return;
            }

            Transform existing = canvasObject.transform.Find("Objective Label");
            GameObject labelObject;
            TextMeshProUGUI label;

            if (existing == null)
            {
                labelObject = new GameObject("Objective Label", typeof(RectTransform));
                labelObject.transform.SetParent(canvasObject.transform, false);
                RectTransform rect = labelObject.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(1f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(1f, 1f);
                rect.anchoredPosition = new Vector2(-80f, -70f);
                rect.sizeDelta = new Vector2(760f, 52f);
                label = labelObject.AddComponent<TextMeshProUGUI>();
                label.fontSize = 22f;
                label.alignment = TextAlignmentOptions.TopRight;
                label.color = new Color(0.45f, 0.95f, 0.55f);
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.raycastTarget = false;
                if (label.font == null && TMP_Settings.defaultFontAsset != null)
                {
                    label.font = TMP_Settings.defaultFontAsset;
                }
            }
            else
            {
                labelObject = existing.gameObject;
                label = labelObject.GetComponent<TextMeshProUGUI>();
                if (label == null)
                {
                    label = labelObject.AddComponent<TextMeshProUGUI>();
                }
            }

            label.text = "OBJECTIVE // REACH THE EXTRACTION ZONE";
            EditorUtility.SetDirty(label);
        }

        private static void AddPointLight(
            Transform parent,
            string name,
            Vector3 localPosition,
            Color color,
            float intensity,
            float range)
        {
            var lightObject = new GameObject(name);
            lightObject.transform.SetParent(parent, false);
            lightObject.transform.localPosition = localPosition;
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
        }

        private static GameObject CreateChild(string name, Transform parent, Vector3 localPosition)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            child.transform.localPosition = localPosition;
            return child;
        }

        private static GameObject CreatePrimitive(
            PrimitiveType primitiveType,
            string name,
            Transform parent,
            Vector3 localPosition,
            Vector3 localScale,
            Material material,
            bool addCollider = true,
            Quaternion? localRotation = null)
        {
            GameObject primitive = GameObject.CreatePrimitive(primitiveType);
            primitive.name = name;
            primitive.transform.SetParent(parent, false);
            primitive.transform.localPosition = localPosition;
            primitive.transform.localRotation = localRotation ?? Quaternion.identity;
            primitive.transform.localScale = localScale;
            primitive.isStatic = true;

            if (!addCollider)
            {
                Collider collider = primitive.GetComponent<Collider>();
                if (collider != null)
                {
                    Object.DestroyImmediate(collider);
                }
            }

            Renderer renderer = primitive.GetComponent<Renderer>();
            if (renderer != null && material != null)
            {
                renderer.sharedMaterial = material;
            }

            return primitive;
        }

        private static Material CreateMaterial(
            string path,
            Color baseColor,
            float metallic,
            float smoothness,
            Color? emission = null)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    Debug.LogError("[CONTRACT] URP Lit shader was not found; city materials could not be created.");
                    return null;
                }

                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetColor("_BaseColor", baseColor);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            material.enableInstancing = true;

            if (emission.HasValue)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission.Value);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                material.DisableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", Color.black);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static void DestroyRoot(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == name)
                {
                    Object.DestroyImmediate(root);
                }
            }
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == name)
                {
                    return root;
                }
            }

            return null;
        }

        private static void SetLayerRecursively(GameObject gameObject, int layer)
        {
            gameObject.layer = layer;
            foreach (Transform child in gameObject.GetComponentsInChildren<Transform>(true))
            {
                child.gameObject.layer = layer;
            }
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string parent = Path.GetDirectoryName(folder)?.Replace('\\', '/');
            string name = Path.GetFileName(folder);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
