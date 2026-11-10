using System.Collections.Generic;
using System.IO;
using System.Linq;
using Contract.Data;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Contract.Editor.World
{
    /// <summary>
    /// Builds the Gangnam district (Stage 3) into GameWorld.unity. Idempotent: re-running replaces
    /// District_Gangnam. Gameplay objects (player, safehouse, interactables, extraction, markers)
    /// are preserved and only repositioned where the new layout requires it.
    ///
    /// Layout (metres, +Z = north): 강남대로 boulevard along Z (x = -8..8), 테헤란로 cross road along
    /// X (z = -7..7), four raised city blocks, a pedestrian food alley (먹자골목) in the NE block,
    /// 강남역 subway stairs (NE) and an underground parking ramp (SW) that hosts the extraction.
    /// Headless: -executeMethod Contract.Editor.World.GangnamDistrictBuilder.Build
    /// </summary>
    public static class GangnamDistrictBuilder
    {
        private const string ScenePath = "Assets/Scenes/GameWorld.unity";
        public const string RootName = "District_Gangnam";
        public const string GameplayRootName = "GameplayStructures";
        public const string PrefabPath = "Assets/Prefabs/Environment/District_Gangnam.prefab";

        public const float SidewalkTop = 0.15f;
        public static readonly Rect WalkableArea = Rect.MinMaxRect(-85f, -70f, 85f, 95f);

        // Sunken areas (x/z rects) - also used by tests to allow lower ground there.
        public static readonly Rect SubwayPit = Rect.MinMaxRect(24f, 9f, 31f, 12f);
        public static readonly Rect ParkingPit = Rect.MinMaxRect(-44.5f, -47f, -38.5f, -30f);
        public const float SubwayFloor = -3.1f;
        public const float ParkingFloor = -3.2f;

        public static readonly Vector3 PlayerSpawn = new Vector3(-10.5f, 0.2f, -46.5f);
        public static readonly Vector3 ExtractionPosition = new Vector3(-41.5f, ParkingFloor, -43.5f);

        internal static EnvKit kit;
        private static KoreanModules mod;

        [MenuItem("CONTRACT/World/Build Gangnam District")]
        public static void Build()
        {
            EnvTextureFactory.GenerateAll();
            kit = new EnvKit();
            mod = new KoreanModules(kit);

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Transform gameplay = PreserveGameplayStructures(scene);

            GameObject old = GameObject.Find(RootName);
            if (old != null) Object.DestroyImmediate(old);
            Transform root = new GameObject(RootName).transform;

            BuildGround(EnvKit.Group(root, "Ground"));
            BuildMarkings(EnvKit.Group(root, "RoadMarkings"));
            BuildSubwayEntrance(EnvKit.Group(root, "Subway_GangnamStation"));
            BuildParkingRamp(EnvKit.Group(root, "UndergroundParking"));
            BuildBuildings(EnvKit.Group(root, "Buildings"));
            BuildAlley(EnvKit.Group(root, "FoodAlley"));
            BuildStreetFurniture(EnvKit.Group(root, "StreetFurniture"));
            BuildTraffic(EnvKit.Group(root, "Traffic"));
            BuildBackground(EnvKit.Group(root, "Skyline"));
            BuildBounds(scene);
            RelocateGameplay(gameplay);
            ConfigureLighting(root);
            MarkStatic(root);
            PrewarmFont(root);
            AddNeonFlicker(root);
            RenameRegion();

            // Shared district prefab: GameWorld, the main menu and mission selection all use it.
            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            PrefabUtility.SaveAsPrefabAssetAndConnect(root.gameObject, PrefabPath, InteractionMode.AutomatedAction);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            int buildings = root.Find("Buildings").childCount;
            int signs = root.GetComponentsInChildren<TextMeshPro>(true).Length;
            Debug.Log($"[GangnamDistrictBuilder] Built {RootName}: {buildings} buildings, {signs} Hangul/Latin text elements, " +
                      $"{root.GetComponentsInChildren<Light>(true).Length} lights, {root.GetComponentsInChildren<MeshRenderer>(true).Length} renderers.");
        }

        // =====================================================================================
        // Gameplay preservation
        // =====================================================================================

        private static Transform PreserveGameplayStructures(Scene scene)
        {
            GameObject existing = GameObject.Find(GameplayRootName);
            Transform gameplay = existing != null ? existing.transform : new GameObject(GameplayRootName).transform;

            GameObject environment = GameObject.Find("Environment");
            if (environment != null)
            {
                foreach (string keep in new[] { "Safehouse", "ExtractionZone", "PlayerSpawn" })
                {
                    Transform t = environment.transform.Find(keep);
                    if (t != null) t.SetParent(gameplay, true);
                }

                Object.DestroyImmediate(environment); // old primitive street
            }

            return gameplay;
        }

        private static void RelocateGameplay(Transform gameplay)
        {
            // Blocks are raised 0.15 m (kerb height): lift the safehouse and its interactables once.
            Transform safehouse = gameplay.Find("Safehouse");
            if (safehouse != null && safehouse.position.y < 0.1f)
            {
                safehouse.position += Vector3.up * SidewalkTop;
                foreach (string name in new[] { "KeycardPickup", "SafehouseTerminal", "SafehouseDoor" })
                {
                    GameObject go = GameObject.Find(name);
                    if (go != null) go.transform.position += Vector3.up * SidewalkTop;
                }
            }

            Transform extraction = gameplay.Find("ExtractionZone");
            if (extraction != null) extraction.position = ExtractionPosition;
            GameObject extractionMarker = GameObject.Find("ExtractionPoint");
            if (extractionMarker != null) extractionMarker.transform.position = ExtractionPosition + Vector3.up * 0.1f;

            Transform spawn = gameplay.Find("PlayerSpawn");
            if (spawn != null) spawn.position = PlayerSpawn;

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                player.transform.SetPositionAndRotation(PlayerSpawn, Quaternion.identity);
            }

            GameObject camera = GameObject.FindGameObjectWithTag("MainCamera");
            if (camera != null)
            {
                camera.transform.position = PlayerSpawn + new Vector3(0.5f, 2.6f, -4.5f);
            }
        }

        // =====================================================================================
        // Ground
        // =====================================================================================

        private static void BuildGround(Transform g)
        {
            Rect[] pits = { SubwayPit, ParkingPit };

            // Road surface everywhere (top y = 0), and the four raised blocks (top y = 0.15).
            Slab(g, "Road", Rect.MinMaxRect(-90f, -75f, 90f, 100f), 0f, 0.5f, "Asphalt", EnvTextureFactory.AsphaltTile, pits);
            Slab(g, "Block_NE", Rect.MinMaxRect(8f, 7f, 85f, 95f), SidewalkTop, SidewalkTop, "Pavers", EnvTextureFactory.PaverTile, pits);
            Slab(g, "Block_NW", Rect.MinMaxRect(-85f, 7f, -8f, 95f), SidewalkTop, SidewalkTop, "Pavers", EnvTextureFactory.PaverTile, pits);
            Slab(g, "Block_SE", Rect.MinMaxRect(8f, -70f, 85f, -7f), SidewalkTop, SidewalkTop, "Pavers", EnvTextureFactory.PaverTile, pits);
            Slab(g, "Block_SW", Rect.MinMaxRect(-85f, -70f, -8f, -7f), SidewalkTop, SidewalkTop, "Pavers", EnvTextureFactory.PaverTile, pits);

            // Outer ground so the edges of the world never show the void.
            kit.Block(g, "OuterGround", new Vector3(0f, -0.6f, 0f), new Vector3(500f, 0.55f, 500f), "Roof", new Vector2(8f, 8f), collider: false, castShadows: false);

            // Granite kerbs along every road edge.
            foreach (float x in new[] { -8f, 8f })
            {
                foreach ((float z0, float z1) in new[] { (7f, 95f), (-70f, -7f) })
                {
                    kit.Block(g, "Kerb", new Vector3(x - Mathf.Sign(x) * -0.12f, 0f, (z0 + z1) / 2f), new Vector3(0.25f, SidewalkTop + 0.02f, z1 - z0), "Curb", collider: false);
                }
            }

            foreach (float z in new[] { -7f, 7f })
            {
                foreach ((float x0, float x1) in new[] { (8f, 85f), (-85f, -8f) })
                {
                    kit.Block(g, "Kerb", new Vector3((x0 + x1) / 2f, 0f, z - Mathf.Sign(z) * -0.12f), new Vector3(x1 - x0, SidewalkTop + 0.02f, 0.25f), "Curb", collider: false);
                }
            }

            // Yellow tactile guide line down the middle of the boulevard pavements.
            foreach (float x in new[] { -11.5f, 11.5f })
            {
                foreach ((float z0, float z1) in new[] { (14f, 95f), (-70f, -14f) })
                {
                    kit.Block(g, "TactileGuide", new Vector3(x, SidewalkTop, (z0 + z1) / 2f), new Vector3(0.3f, 0.01f, z1 - z0), "Tactile", EnvTextureFactory.TactileTile, collider: false, castShadows: false);
                }
            }
        }

        /// <summary>Box slab covering <paramref name="r"/> minus <paramref name="holes"/> (grid decomposition).</summary>
        private static void Slab(Transform parent, string name, Rect r, float top, float thickness, string material, Vector2 tile, IEnumerable<Rect> holes)
        {
            List<Rect> cut = holes.Where(h => h.Overlaps(r)).ToList();
            List<float> xs = new List<float> { r.xMin, r.xMax };
            List<float> zs = new List<float> { r.yMin, r.yMax };
            foreach (Rect h in cut)
            {
                xs.Add(Mathf.Clamp(h.xMin, r.xMin, r.xMax)); xs.Add(Mathf.Clamp(h.xMax, r.xMin, r.xMax));
                zs.Add(Mathf.Clamp(h.yMin, r.yMin, r.yMax)); zs.Add(Mathf.Clamp(h.yMax, r.yMin, r.yMax));
            }

            xs = xs.Distinct().OrderBy(v => v).ToList();
            zs = zs.Distinct().OrderBy(v => v).ToList();
            int part = 0;
            for (int j = 0; j < zs.Count - 1; j++)
            {
                int i = 0;
                while (i < xs.Count - 1)
                {
                    if (InHole(cut, (xs[i] + xs[i + 1]) / 2f, (zs[j] + zs[j + 1]) / 2f)) { i++; continue; }
                    int k = i;
                    while (k + 1 < xs.Count - 1 && !InHole(cut, (xs[k + 1] + xs[k + 2]) / 2f, (zs[j] + zs[j + 1]) / 2f)) k++;
                    Vector3 size = new Vector3(xs[k + 1] - xs[i], thickness, zs[j + 1] - zs[j]);
                    Vector3 pos = new Vector3((xs[i] + xs[k + 1]) / 2f, top - thickness, (zs[j] + zs[j + 1]) / 2f);
                    kit.Block(parent, $"{name}_{part++}", pos, size, material, tile, castShadows: false);
                    i = k + 1;
                }
            }
        }

        private static bool InHole(List<Rect> holes, float x, float z) => holes.Any(h => h.Contains(new Vector2(x, z)));

        // =====================================================================================
        // Road markings (Korean conventions: yellow double centre line, white lane dashes,
        // zebra crossings with bars parallel to traffic, stop lines, painted Hangul text)
        // =====================================================================================

        private static void BuildMarkings(Transform m)
        {
            const float y = 0.005f;

            void Paint(string name, Vector3 center, Vector3 size, string mat = "PaintWhite") =>
                kit.Block(m, name, new Vector3(center.x, y, center.z), new Vector3(size.x, 0.01f, size.z), mat, collider: false, castShadows: false);

            // Boulevard (along Z).
            foreach ((float z0, float z1) in new[] { (14.5f, 95f), (-70f, -14.5f) })
            {
                float len = z1 - z0, mid = (z0 + z1) / 2f;
                Paint("CentreLineL", new Vector3(-0.2f, 0, mid), new Vector3(0.15f, 0, len), "PaintYellow");
                Paint("CentreLineR", new Vector3(0.2f, 0, mid), new Vector3(0.15f, 0, len), "PaintYellow");
                for (float z = z0 + 2f; z < z1 - 3f; z += 8f)
                {
                    Paint("LaneDash", new Vector3(-4f, 0, z + 1.5f), new Vector3(0.15f, 0, 3f));
                    Paint("LaneDash", new Vector3(4f, 0, z + 1.5f), new Vector3(0.15f, 0, 3f));
                }
            }

            // Cross road (along X).
            foreach ((float x0, float x1) in new[] { (14.5f, 85f), (-85f, -14.5f) })
            {
                float len = x1 - x0, mid = (x0 + x1) / 2f;
                Paint("CentreLineN", new Vector3(mid, 0, 0.2f), new Vector3(len, 0, 0.15f), "PaintYellow");
                Paint("CentreLineS", new Vector3(mid, 0, -0.2f), new Vector3(len, 0, 0.15f), "PaintYellow");
                for (float x = x0 + 2f; x < x1 - 3f; x += 8f)
                {
                    Paint("LaneDash", new Vector3(x + 1.5f, 0, 3.5f), new Vector3(3f, 0, 0.15f));
                    Paint("LaneDash", new Vector3(x + 1.5f, 0, -3.5f), new Vector3(3f, 0, 0.15f));
                }
            }

            // Zebra crossings around the intersection.
            foreach (float zc in new[] { 10.5f, -10.5f })
            {
                for (float x = -7.25f; x <= 7.3f; x += 1f) Paint("Zebra", new Vector3(x, 0, zc), new Vector3(0.5f, 0, 4f));
            }

            foreach (float xc in new[] { 10.5f, -10.5f })
            {
                for (float z = -6.25f; z <= 6.3f; z += 1f) Paint("Zebra", new Vector3(xc, 0, z), new Vector3(4f, 0, 0.5f));
            }

            // Stop lines (traffic drives on the right).
            Paint("StopLine", new Vector3(4f, 0, -13.6f), new Vector3(7.6f, 0, 0.45f));
            Paint("StopLine", new Vector3(-4f, 0, 13.6f), new Vector3(7.6f, 0, 0.45f));
            Paint("StopLine", new Vector3(-13.6f, 0, -3.5f), new Vector3(0.45f, 0, 6.6f));
            Paint("StopLine", new Vector3(13.6f, 0, 3.5f), new Vector3(0.45f, 0, 6.6f));

            // Painted road text, read by approaching drivers.
            RoadText(m, "버스전용", new Vector3(6f, y + 0.006f, -40f), 0f);
            RoadText(m, "버스전용", new Vector3(-6f, y + 0.006f, 40f), 180f);
            RoadText(m, "천천히", new Vector3(2f, y + 0.006f, -24f), 0f);
            RoadText(m, "천천히", new Vector3(-2f, y + 0.006f, 24f), 180f);
        }

        private static void RoadText(Transform parent, string text, Vector3 position, float heading)
        {
            // Laid flat, elongated along the direction of travel as real road lettering is.
            TextMeshPro tmp = kit.Text(parent, "RoadText_" + text, text, position, new Vector2(3.2f, 1.6f), new Color(0.86f, 0.86f, 0.84f), kit.PaintTextMaterial,
                new Vector3(90f, heading, 0f));
            tmp.transform.localScale = new Vector3(1f, 2.2f, 1f);
        }

        // =====================================================================================
        // 강남역 subway entrance (NE corner) - real stairs down to a closed shutter
        // =====================================================================================

        private static void BuildSubwayEntrance(Transform s)
        {
            Rect p = SubwayPit;
            float zc = p.center.y, w = p.height;
            // Steps: 13 x 0.25 m rise, 0.4 m run, descending east.
            for (int i = 0; i < 13; i++)
            {
                float top = SidewalkTop - 0.25f * (i + 1);
                float x0 = p.xMin + 0.4f + 0.4f * i;
                float x1 = i == 12 ? p.xMax : x0 + 0.4f; // each step covers only its own run; the last one is the landing
                kit.Block(s, "Step" + i, new Vector3((x0 + x1) / 2f, -3.6f, zc), new Vector3(x1 - x0, top + 3.6f, w), "Concrete", EnvTextureFactory.ConcreteTile);
            }

            kit.Block(s, "TopLanding", new Vector3(p.xMin + 0.2f, -3.6f, zc), new Vector3(0.4f, SidewalkTop + 3.6f, w), "Concrete");
            // Pit walls (below ground) and parapets (above ground) on three sides.
            kit.Block(s, "WallN", new Vector3(p.center.x, -3.6f, p.yMax + 0.15f), new Vector3(p.width, 4.75f, 0.3f), "Concrete", EnvTextureFactory.ConcreteTile);
            kit.Block(s, "WallS", new Vector3(p.center.x, -3.6f, p.yMin - 0.15f), new Vector3(p.width, 4.75f, 0.3f), "Concrete", EnvTextureFactory.ConcreteTile);
            kit.Block(s, "WallE", new Vector3(p.xMax + 0.15f, -3.6f, zc), new Vector3(0.3f, 4.75f, w + 0.6f), "Concrete", EnvTextureFactory.ConcreteTile);
            kit.Block(s, "Shutter", new Vector3(p.xMax - 0.05f, SubwayFloor, zc), new Vector3(0.08f, 2.6f, w - 0.2f), "MetalLight", collider: false);
            kit.Text(s, "ShutterText", "운행 종료", new Vector3(p.xMax - 0.1f, SubwayFloor + 1.8f, zc), new Vector2(2.4f, 0.5f), new Color(0.75f, 0.1f, 0.1f),
                kit.SignTextMaterial, new Vector3(0f, 90f, 0f));

            // Glass canopy.
            foreach (float x in new[] { p.xMin + 0.1f, p.xMax - 0.1f })
            {
                foreach (float z in new[] { p.yMin - 0.2f, p.yMax + 0.2f })
                {
                    kit.Block(s, "CanopyPost", new Vector3(x, SidewalkTop + 1.15f, z), new Vector3(0.12f, 1.75f, 0.12f), "MetalDark", collider: false);
                }
            }

            kit.Block(s, "CanopyRoof", new Vector3(p.center.x, SidewalkTop + 2.9f, zc), new Vector3(p.width + 0.6f, 0.1f, w + 0.9f), "GlassClear", collider: false);

            // Station signs: line-2 green, station name, exit number - on the street side and the stair head.
            Transform sign = EnvKit.Group(s, "StationSign", new Vector3(p.xMin - 0.15f, SidewalkTop + 3.4f, zc), -90f);
            kit.Block(sign, "Board", new Vector3(0f, -0.5f, 0f), new Vector3(w + 0.8f, 1f, 0.15f), "SignWhite", collider: false);
            kit.Primitive(sign, PrimitiveType.Cylinder, "Line2", new Vector3(-w / 2f, 0f, 0.09f), new Vector3(0.8f, 0.02f, 0.8f), "LineGreen", euler: new Vector3(90f, 0f, 0f), castShadows: false);
            kit.Text(sign, "Num", "2", new Vector3(-w / 2f, 0f, 0.11f), new Vector2(0.6f, 0.6f), Color.white, kit.SignTextMaterial);
            kit.Text(sign, "Name", "강남역", new Vector3(0.35f, 0.12f, 0.09f), new Vector2(w * 0.7f, 0.55f), new Color(0.1f, 0.1f, 0.12f), kit.SignTextMaterial);
            kit.Text(sign, "NameEn", "Gangnam  ·  Exit 10", new Vector3(0.35f, -0.3f, 0.09f), new Vector2(w * 0.7f, 0.22f), new Color(0.2f, 0.2f, 0.25f), kit.SignTextMaterial);

            Light glow = new GameObject("StairLight").AddComponent<Light>();
            glow.transform.SetParent(s, false);
            glow.transform.localPosition = new Vector3(p.xMax - 1.5f, SubwayFloor + 2.2f, zc);
            glow.type = LightType.Point;
            glow.color = new Color(0.85f, 0.92f, 1f);
            glow.intensity = 3f;
            glow.range = 7f;
        }

        // =====================================================================================
        // 지하주차장 - underground parking ramp (SW block), hosts the extraction point
        // =====================================================================================

        private static void BuildParkingRamp(Transform s)
        {
            Rect p = ParkingPit;
            float xc = p.center.x, w = p.width;
            const float rampTopZ = -30f, rampBottomZ = -40f;
            float drop = SidewalkTop - ParkingFloor;
            float run = rampTopZ - rampBottomZ;
            float angle = Mathf.Atan2(drop, run) * Mathf.Rad2Deg;
            float length = Mathf.Sqrt(run * run + drop * drop);

            // Ramp slab: pivot is bottom-centre, tilt so the south (-Z) end is lower.
            kit.Block(s, "Ramp", new Vector3(xc, (SidewalkTop + ParkingFloor) / 2f - 0.32f, (rampTopZ + rampBottomZ) / 2f), new Vector3(w, 0.3f, length + 0.2f),
                "Concrete", EnvTextureFactory.ConcreteTile, euler: new Vector3(-angle, 0f, 0f));
            kit.Block(s, "Floor", new Vector3(xc, ParkingFloor - 0.4f, (rampBottomZ + p.yMin) / 2f - 0.1f), new Vector3(w, 0.4f, rampBottomZ - p.yMin + 0.4f),
                "Concrete", EnvTextureFactory.ConcreteTile);
            for (int i = 0; i < 5; i++)
            {
                kit.Block(s, "Stripe", new Vector3(xc - 1.6f + i * 0.8f, ParkingFloor + 0.005f, -44.5f), new Vector3(0.25f, 0.01f, 3f), "PaintYellow", collider: false, castShadows: false);
            }

            // Retaining walls (below ground) + low walls above ground on the long sides.
            kit.Block(s, "WallW", new Vector3(p.xMin - 0.15f, ParkingFloor - 0.4f, p.center.y), new Vector3(0.3f, drop + 1.5f, p.height), "Concrete", EnvTextureFactory.ConcreteTile);
            kit.Block(s, "WallE", new Vector3(p.xMax + 0.15f, ParkingFloor - 0.4f, p.center.y), new Vector3(0.3f, drop + 1.5f, p.height), "Concrete", EnvTextureFactory.ConcreteTile);
            kit.Block(s, "WallS", new Vector3(xc, ParkingFloor - 0.4f, p.yMin - 0.15f), new Vector3(w + 0.6f, drop + 1.5f, 0.3f), "Concrete", EnvTextureFactory.ConcreteTile);

            // Basement door at the bottom with the classic yellow/black clearance bar.
            kit.Block(s, "Shutter", new Vector3(xc, ParkingFloor, p.yMin + 0.05f), new Vector3(w - 0.4f, 3.2f, 0.1f), "MetalLight", collider: false);
            kit.Sign(s, "Basement", new Vector3(xc, ParkingFloor + 2.6f, p.yMin + 0.15f), new Vector2(3.6f, 0.6f), "SignDark", "지하 2층 주차장", new Color(1f, 0.8f, 0.2f), depth: 0.08f);

            // Entrance portal at the ramp head.
            Transform portal = EnvKit.Group(s, "EntrancePortal", new Vector3(xc, SidewalkTop, rampTopZ));
            kit.Block(portal, "PostW", new Vector3(-w / 2f - 0.2f, 0f, 0f), new Vector3(0.3f, 3.4f, 0.3f), "MetalDark");
            kit.Block(portal, "PostE", new Vector3(w / 2f + 0.2f, 0f, 0f), new Vector3(0.3f, 3.4f, 0.3f), "MetalDark");
            kit.Block(portal, "Beam", new Vector3(0f, 3.4f, 0f), new Vector3(w + 0.8f, 0.9f, 0.4f), "SignDark", collider: false);
            kit.Text(portal, "Entrance", "지하주차장 입구", new Vector3(0f, 3.85f, 0.22f), new Vector2(w * 0.7f, 0.6f), new Color(1f, 0.85f, 0.3f), kit.NeonTextMaterial);
            kit.Sign(portal, "P", new Vector3(w / 2f - 0.1f, 3.85f, 0.25f), new Vector2(0.7f, 0.7f), "SignBlue", "P", Color.white, depth: 0.06f);
            // Height bar "높이제한 2.3m" hanging in front.
            Transform bar = EnvKit.Group(portal, "HeightBar", new Vector3(0f, 2.5f, 1.2f));
            for (int i = 0; i < 8; i++)
            {
                kit.Block(bar, "Seg" + i, new Vector3(-w / 2f + w / 16f + i * w / 8f, 0f, 0f), new Vector3(w / 8f, 0.25f, 0.12f), i % 2 == 0 ? "SignYellow" : "Black", collider: false);
            }

            kit.Text(bar, "Limit", "높이제한 2.3m", new Vector3(0f, 0.13f, 0.07f), new Vector2(w * 0.6f, 0.2f), Color.white, kit.SignTextMaterial);

            Light lamp = new GameObject("RampLight").AddComponent<Light>();
            lamp.transform.SetParent(s, false);
            lamp.transform.localPosition = new Vector3(xc, ParkingFloor + 3f, -42f);
            lamp.type = LightType.Point;
            lamp.color = new Color(0.8f, 0.95f, 1f);
            lamp.intensity = 3f;
            lamp.range = 9f;
        }

        // =====================================================================================
        // Buildings
        // =====================================================================================

        private static Biz B(string ko, string en, string board, Color text) => new Biz(ko, en, board, text);

        private static readonly Color W = Color.white, K = new Color(0.08f, 0.08f, 0.09f);
        private static readonly Color Brown = new Color(0.35f, 0.18f, 0.08f), DeepRed = new Color(0.72f, 0.08f, 0.06f),
            Navy = new Color(0.08f, 0.22f, 0.6f), Gold = new Color(1f, 0.82f, 0.35f), Plum = new Color(0.55f, 0.08f, 0.32f);

        // Fictional businesses with natural Korean names.
        private static readonly Biz Conv = B("하루 편의점 24", "HARU 24", "SignMint", K);
        private static readonly Biz Cafe = B("서울 카페", "SEOUL CAFE", "SignWhite", Brown);
        private static readonly Biz Hangang = B("한강 레스토랑", "HANGANG RESTAURANT", "SignDark", Gold);
        private static readonly Biz Market = B("서울 마켓", "SEOUL MARKET", "SignGreen", W);
        private static readonly Biz Chicken = B("강남 치킨", "GANGNAM CHICKEN", "SignYellow", DeepRed);
        private static readonly Biz Hansik = B("소담 한식당", "SODAM KOREAN TABLE", "SignWhite", new Color(0.12f, 0.3f, 0.15f));
        private static readonly Biz Gimbap = B("행복 김밥", "HAPPY GIMBAP", "SignOrange", W);
        private static readonly Biz Noraebang = B("별빛 노래방", null, "SignDark", new Color(1f, 0.35f, 0.85f));
        private static readonly Biz PcBang = B("한빛 PC방", null, "SignBlue", W);
        private static readonly Biz Pharmacy = B("푸른 약국", "PHARMACY", "SignGreen", W);
        private static readonly Biz Dental = B("새봄 치과", "DENTAL CLINIC", "SignWhite", Navy);
        private static readonly Biz Hair = B("미소 헤어", "MISO HAIR", "SignPurple", W);
        private static readonly Biz RealEstate = B("다온 부동산", null, "SignYellow", K);
        private static readonly Biz Bank = B("누리 은행", "NURI BANK", "SignBlue", W);
        private static readonly Biz Academy = B("마루 영어학원", "MARU ENGLISH", "SignWhite", DeepRed);
        private static readonly Biz Eye = B("하늘 안과", "EYE CLINIC", "SignCyan", K);
        private static readonly Biz Haejang = B("24시 해장국", null, "SignRed", W);
        private static readonly Biz Bbq = B("한우 숯불구이", null, "SignRed", Gold);
        private static readonly Biz Flower = B("오늘 꽃집", "TODAY FLOWERS", "SignWhite", Plum);
        private static readonly Biz Fitness = B("강남 피트니스", "GANGNAM FITNESS", "SignDark", new Color(1f, 0.25f, 0.3f));
        private static readonly Biz Billiards = B("청춘 당구장", null, "SignCyan", K);
        private static readonly Biz StudyCafe = B("다올 스터디카페", null, "SignWhite", K);
        private static readonly Biz Pilates = B("온새미 필라테스", null, "SignPurple", W);
        private static readonly Biz Internal = B("가온 내과", null, "SignWhite", Navy);
        private static readonly Biz Derma = B("하람 피부과", "DERMATOLOGY", "SignMint", K);
        private static readonly Biz Noodle = B("고향 칼국수", null, "SignOrange", W);

        private static void BuildBuildings(Transform b)
        {
            // ---- NE block (frontage on the boulevard and 테헤란로) ----------------------------
            Transform mirae = mod.GlassTower(b, "MiraeTower", new Vector3(26f, SidewalkTop, 25f), -90f, 20f, 20f, 72f, "SignCyan",
                B("미래 타워", "MIRAE TOWER", "SignDark", new Color(0.6f, 0.95f, 1f)), new[] { Cafe, Bank }, enterable: true);
            BuildMiraeInterior(mirae);
            mod.Commercial(b, "NE_Comm_1", new Vector3(45f, SidewalkTop, 24f), 180f, 14f, 18f, 8, Conv,
                new[] { Dental, Academy, PcBang, Internal, Hair, StudyCafe, Pilates }, PcBang);
            mod.Commercial(b, "NE_Comm_2", new Vector3(61f, SidewalkTop, 23f), 180f, 14f, 16f, 6, Chicken,
                new[] { Eye, RealEstate, Billiards, Academy, Derma }, null, Fitness);
            mod.Commercial(b, "NE_Comm_3", new Vector3(77f, SidewalkTop, 24f), 180f, 14f, 18f, 10, Haejang,
                new[] { Noraebang, Internal, StudyCafe, Dental, Pilates, Hair, Eye, PcBang }, Noraebang);

            Transform hangyeol = mod.GlassTower(b, "HangyeolGroup", new Vector3(25f, SidewalkTop, 73f), -90f, 18f, 20f, 90f, "SignBlue",
                B("한결 그룹", "HANGYEOL GROUP", "SignDark", new Color(0.65f, 0.8f, 1f)), new[] { Market, Cafe }, enterable: true);
            BuildHangyeolInterior(hangyeol);
            mod.Commercial(b, "NE_Comm_4", new Vector3(45f, SidewalkTop, 72f), 0f, 14f, 16f, 12, Pharmacy,
                new[] { Internal, Derma, Academy, Eye, StudyCafe, Dental }, null, B("누리 생명", "NURI LIFE", "SignDark", new Color(0.4f, 1f, 0.7f)));
            mod.GlassTower(b, "NuriLife", new Vector3(70f, SidewalkTop, 78f), 180f, 26f, 26f, 60f, "SignMagenta",
                B("누리 생명", "NURI LIFE", "SignDark", new Color(0.4f, 1f, 0.7f)), new[] { Hangang, Bank, Cafe });

            // ---- NW block ---------------------------------------------------------------------
            mod.Commercial(b, "NW_Comm_1", new Vector3(-26f, SidewalkTop, 23.5f), 90f, 17f, 20f, 9, Conv,
                new[] { Derma, Pilates, Billiards, Academy, Internal, Hair }, Noraebang);
            mod.Commercial(b, "NW_Comm_2", new Vector3(-45f, SidewalkTop, 22.5f), 180f, 14f, 15f, 7, Hangang,
                new[] { Billiards, StudyCafe, Dental, RealEstate }, null, Fitness);
            mod.Commercial(b, "NW_Comm_3", new Vector3(-62f, SidewalkTop, 24f), 180f, 16f, 18f, 11, Pharmacy,
                new[] { Internal, Pilates, Eye, Academy, Hair, Derma, StudyCafe }, Noraebang);
            mod.Commercial(b, "NW_Comm_4", new Vector3(-78f, SidewalkTop, 21.5f), 180f, 12f, 13f, 5, Noodle,
                new[] { RealEstate, PcBang, Billiards }, PcBang);
            mod.Hotel(b, "MiraeHotel", new Vector3(-25f, SidewalkTop, 48f), 90f, 20f, 18f, 55f, B("미래 호텔", "MIRAE HOTEL", "SignDark", Gold), new[] { Hangang, Cafe });
            mod.Commercial(b, "NW_Comm_5", new Vector3(-25f, SidewalkTop, 76f), 90f, 28f, 18f, 14, Gimbap,
                new[] { Academy, Internal, Dental, Derma, Eye, Pilates, StudyCafe, Hair }, Noraebang);
            mod.GlassTower(b, "NW_Office", new Vector3(-52f, SidewalkTop, 56f), 90f, 18f, 24f, 40f, "SignWhite",
                B("누리 증권", "NURI SECURITIES", "SignDark", new Color(1f, 0.85f, 0.5f)), new[] { Bank });
            mod.Commercial(b, "NW_Comm_6", new Vector3(-75f, SidewalkTop, 64f), 90f, 48f, 16f, 8, Market,
                new[] { PcBang, Billiards, StudyCafe, Hair }, Noraebang, Fitness);

            // ---- SE block ---------------------------------------------------------------------
            mod.GlassTower(b, "NuriSecurities", new Vector3(27f, SidewalkTop, -25.5f), -90f, 19f, 20f, 80f, "SignWhite",
                B("누리 증권", "NURI SECURITIES", "SignDark", new Color(1f, 0.85f, 0.5f)), new[] { Bank, Cafe });
            mod.Commercial(b, "SE_Comm_1", new Vector3(48f, SidewalkTop, -23.5f), 0f, 16f, 17f, 10, Market,
                new[] { Eye, Dental, Academy, Internal, Pilates, Derma }, PcBang);
            mod.Commercial(b, "SE_Comm_2", new Vector3(71f, SidewalkTop, -24.5f), 0f, 26f, 19f, 6, Gimbap,
                new[] { Billiards, RealEstate, Hair, StudyCafe }, Noraebang, Fitness);
            mod.GlassTower(b, "SE_Tower", new Vector3(28f, SidewalkTop, -55f), -90f, 22f, 24f, 50f, "SignCyan",
                B("한결 그룹", "HANGYEOL GROUP", "SignDark", new Color(0.65f, 0.8f, 1f)), new[] { Conv, Cafe });
            mod.Commercial(b, "SE_Officetel", new Vector3(64f, SidewalkTop, -55f), 0f, 38f, 22f, 15, Pharmacy,
                new[] { Internal, Derma, Dental, Eye, Academy, Pilates, StudyCafe, Hair }, Noraebang);

            // ---- SW block (the safehouse sits at x -20..-13, z -53..-46: keep clear) -----------
            mod.Commercial(b, "SW_Comm_1", new Vector3(-26f, SidewalkTop, -22.5f), 90f, 15f, 20f, 7, Haejang,
                new[] { Billiards, PcBang, Hair, RealEstate }, Noraebang);
            mod.Commercial(b, "SW_Comm_2", new Vector3(-56f, SidewalkTop, -24.5f), 0f, 14f, 19f, 9, Bbq,
                new[] { Academy, Internal, StudyCafe, Pilates, Eye }, PcBang);
            mod.Commercial(b, "SW_Comm_3", new Vector3(-75f, SidewalkTop, -25f), 0f, 18f, 20f, 6, Flower,
                new[] { Hair, RealEstate, Billiards }, null, Fitness);
            mod.GlassTower(b, "SW_ParkingTower", new Vector3(-48f, SidewalkTop, -60f), 0f, 24f, 16f, 45f, "SignBlue",
                B("미래 타워", "MIRAE TOWER", "SignDark", new Color(0.6f, 0.95f, 1f)), new[] { Market, Cafe });
            mod.Commercial(b, "SW_Comm_4", new Vector3(-76f, SidewalkTop, -55f), 90f, 24f, 16f, 5, Noodle,
                new[] { Billiards, PcBang }, Noraebang);
            mod.Commercial(b, "SW_Comm_5", new Vector3(-27f, SidewalkTop, -63f), 90f, 12f, 12f, 4, Chicken,
                new[] { RealEstate, Hair }, null);
        }

        // ---- Interiors (local to the tower, front = +Z) ---------------------------------------

        private static void Partition(Transform t, float z, float halfWidth, Vector2 door, string material = "Concrete")
        {
            float h = KoreanModules.LobbyHeight;
            float leftWidth = door.x + halfWidth, rightWidth = halfWidth - door.y;
            kit.Block(t, "PartitionL", new Vector3(-halfWidth + leftWidth / 2f, 0f, z), new Vector3(leftWidth, h, 0.25f), material, EnvTextureFactory.ConcreteTile);
            kit.Block(t, "PartitionR", new Vector3(door.y + rightWidth / 2f, 0f, z), new Vector3(rightWidth, h, 0.25f), material, EnvTextureFactory.ConcreteTile);
            kit.Block(t, "DoorHeader", new Vector3((door.x + door.y) / 2f, 2.6f, z), new Vector3(door.y - door.x, h - 2.6f, 0.25f), material, collider: false);
        }

        private static void Desk(Transform t, Vector3 at, bool monitor)
        {
            kit.Block(t, "Desk", at, new Vector3(1.6f, 0.75f, 0.8f), "Wood");
            kit.Block(t, "Chair", at + new Vector3(0f, 0f, 0.9f), new Vector3(0.55f, 0.95f, 0.55f), "Black", collider: false);
            if (monitor)
            {
                kit.Block(t, "Monitor", at + new Vector3(0f, 0.75f, -0.2f), new Vector3(0.7f, 0.45f, 0.06f), "Monitor", collider: false, castShadows: false);
            }
        }

        private static void BuildMiraeInterior(Transform tower)
        {
            Transform i = EnvKit.Group(tower, "Interior");
            Partition(i, GangnamLayout.MiraePartitionZ, 10.7f, GangnamLayout.MiraeOfficeDoor);
            float wallFront = GangnamLayout.MiraePartitionZ + 0.14f;
            kit.Sign(i, "StaffOnly", new Vector3(6.5f, 3.3f, wallFront), new Vector2(2.8f, 0.45f), "SignWhite", "관계자 외 출입금지", new Color(0.75f, 0.08f, 0.08f), "STAFF ONLY", depth: 0.04f);
            kit.Text(i, "WallLogo", "미래 타워", new Vector3(-3f, 2.9f, wallFront + 0.02f), new Vector2(5f, 1f), new Color(0.6f, 0.95f, 1f), kit.NeonTextMaterial);
            kit.Text(i, "WallLogoEn", "MIRAE TOWER", new Vector3(-3f, 2.25f, wallFront + 0.02f), new Vector2(4f, 0.35f), new Color(0.75f, 0.8f, 0.85f), kit.SignTextMaterial);
            foreach (float x in new[] { -9.4f, -7.4f })
            {
                kit.Block(i, "ElevatorDoor", new Vector3(x, 0f, wallFront), new Vector3(1.5f, 2.6f, 0.04f), "Steel", collider: false);
            }

            // Reception and speed gates.
            Vector3 r = GangnamLayout.MiraeReception;
            kit.Block(i, "Reception", r, new Vector3(5f, 1.1f, 0.9f), "Wood");
            kit.Block(i, "ReceptionTop", r + new Vector3(0f, 1.1f, 0f), new Vector3(5.2f, 0.05f, 1f), "Marble", collider: false);
            kit.Block(i, "ReceptionMonitor", r + new Vector3(1.2f, 1.15f, -0.2f), new Vector3(0.6f, 0.4f, 0.05f), "Monitor", collider: false, castShadows: false);
            for (float x = -3.6f; x <= 3.7f; x += 1.8f)
            {
                kit.Block(i, "GatePost", new Vector3(x, 0f, -1.2f), new Vector3(0.22f, 1f, 1.2f), "MetalLight");
            }

            // Lounge.
            foreach (float x in new[] { -7f, 7f })
            {
                kit.Block(i, "Sofa", new Vector3(x, 0f, 7.5f), new Vector3(3f, 0.45f, 1f), "Carpet");
                kit.Block(i, "SofaBack", new Vector3(x, 0.45f, 7.95f), new Vector3(3f, 0.45f, 0.15f), "Carpet", collider: false);
                kit.Block(i, "Table", new Vector3(x, 0f, 6.2f), new Vector3(1.2f, 0.4f, 0.7f), "Marble");
            }

            mod.Place(i, "Planter", new Vector3(-9.6f, 0f, 9.6f), 90f);
            mod.Place(i, "Planter", new Vector3(9.6f, 0f, 9.6f), 90f);

            // Staff office behind the partition.
            Desk(i, new Vector3(-7f, 0f, -8f), true);
            Desk(i, new Vector3(-2f, 0f, -8f), true);
            Desk(i, new Vector3(3f, 0f, -8f), true);
            for (float x = 6.5f; x <= 10f; x += 1f)
            {
                kit.Block(i, "Cabinet", new Vector3(x, 0f, -10.25f), new Vector3(0.9f, 1.6f, 0.6f), "MetalLight");
            }

            // Light grid matching the ceiling panels: lobby, then the staff office.
            foreach (float x in new[] { -6f, 0f, 6f })
            {
                foreach (float z in new[] { -2f, 4f, 9f }) AddLight(i, new Vector3(x, 3.9f, z), new Color(1f, 0.96f, 0.9f), 8f, 10f);
                AddLight(i, new Vector3(x, 3.7f, -8f), new Color(0.92f, 0.96f, 1f), 7f, 8f);
            }
        }

        private static void BuildHangyeolInterior(Transform tower)
        {
            Transform i = EnvKit.Group(tower, "Interior");
            Partition(i, GangnamLayout.HangyeolPartitionZ, 9.7f, GangnamLayout.HangyeolServerDoor);
            float lobbySide = GangnamLayout.HangyeolPartitionZ + 0.14f;
            kit.Sign(i, "ServerRoom", new Vector3(3.5f, 3.3f, lobbySide), new Vector2(3.2f, 0.45f), "SignWhite", "서버실 · 관계자 외 출입금지", new Color(0.75f, 0.08f, 0.08f), depth: 0.04f);
            kit.Text(i, "WallLogo", "한결 그룹", new Vector3(-4f, 2.9f, lobbySide + 0.02f), new Vector2(5f, 1f), new Color(0.65f, 0.8f, 1f), kit.NeonTextMaterial);
            kit.Block(i, "Reception", new Vector3(0f, 0f, 5f), new Vector3(4.5f, 1.1f, 0.9f), "Wood");
            kit.Block(i, "ReceptionTop", new Vector3(0f, 1.1f, 5f), new Vector3(4.7f, 0.05f, 1f), "Marble", collider: false);
            kit.Block(i, "Sofa", new Vector3(-6f, 0f, 8f), new Vector3(3f, 0.45f, 1f), "Carpet");

            // Server room: two rows of racks with blinking-style LED strips.
            foreach (float z in new[] { -4f, -7.5f })
            {
                for (float x = -8f; x <= 4f; x += 0.8f)
                {
                    kit.Block(i, "Rack", new Vector3(x, 0f, z), new Vector3(0.75f, 2.2f, 1f), "Black");
                    kit.Block(i, "Leds", new Vector3(x, 0.4f, z + 0.505f), new Vector3(0.5f, 1.4f, 0.01f), "RackLed", collider: false, castShadows: false);
                }
            }

            foreach (float x in new[] { -5f, 0f, 5f })
            {
                foreach (float z in new[] { 2f, 7f }) AddLight(i, new Vector3(x, 3.9f, z), new Color(1f, 0.96f, 0.9f), 8f, 10f);
            }

            foreach (Vector3 p in new[] { new Vector3(-6f, 3.5f, -2.5f), new Vector3(2f, 3.5f, -5.7f), new Vector3(-5f, 3.5f, -9.3f), new Vector3(7f, 3.5f, -9f) })
            {
                AddLight(i, p, new Color(0.45f, 0.7f, 1f), 5f, 8f); // cold server-room light
            }
        }

        // ---- 강남 먹자골목 (NE block, pedestrian alley z 38..46) ------------------------------

        private static void BuildAlley(Transform a)
        {
            // Two-to-five storey restaurant buildings facing the alley from the north (front = -Z).
            mod.Commercial(a, "Alley_1", new Vector3(22f, SidewalkTop, 53f), 180f, 12f, 12f, 4, Hansik, new[] { Noraebang, Hair, Billiards }, Noraebang);
            mod.Commercial(a, "Alley_2", new Vector3(35f, SidewalkTop, 52f), 180f, 10f, 10f, 3, Bbq, new[] { PcBang, RealEstate }, null);
            mod.Commercial(a, "Alley_3", new Vector3(48f, SidewalkTop, 53.5f), 180f, 12f, 13f, 5, Gimbap, new[] { Hair, Noraebang, StudyCafe, Billiards }, Noraebang);
            mod.Commercial(a, "Alley_4", new Vector3(62f, SidewalkTop, 52.5f), 180f, 12f, 11f, 3, Flower, new[] { Hangang, Pilates }, null);
            mod.Commercial(a, "Alley_5", new Vector3(77f, SidewalkTop, 53.5f), 180f, 14f, 13f, 4, Market, new[] { Haejang, Noraebang, PcBang }, PcBang);

            // Gate at the alley mouth, facing the boulevard.
            Transform gate = EnvKit.Group(a, "AlleyGate", new Vector3(15f, SidewalkTop, 42f), -90f);
            kit.Block(gate, "PostN", new Vector3(-3.7f, 0f, 0f), new Vector3(0.35f, 6f, 0.35f), "RedPaint");
            kit.Block(gate, "PostS", new Vector3(3.7f, 0f, 0f), new Vector3(0.35f, 6f, 0.35f), "RedPaint");
            kit.Sign(gate, "Gate", new Vector3(0f, 5.8f, 0f), new Vector2(7.8f, 1.5f), "SignDark", "강남 먹자골목", Gold, "GANGNAM FOOD STREET", depth: 0.3f);

            // String lights strung across the alley every 6 m.
            for (float x = 19f; x < 84f; x += 6f)
            {
                for (int i = 0; i <= 8; i++)
                {
                    float t = i / 8f;
                    float z = Mathf.Lerp(38.5f, 45.5f, t);
                    float y = 5.6f - Mathf.Sin(t * Mathf.PI) * 0.7f;
                    kit.Primitive(a, PrimitiveType.Sphere, "Bulb", new Vector3(x, y, z), Vector3.one * 0.14f, "LampWarm", castShadows: false);
                }
            }

            for (float x = 20f; x <= 82f; x += 10f)
            {
                AddLight(a, new Vector3(x, 4.8f, 42f), new Color(1f, 0.75f, 0.5f), 5f, 11f);
            }

            // South side of the alley = backs of the 테헤란로 buildings: give them lit rear shops,
            // and line it with 포장마차 tents so it reads as a lively street, not a blank wall.
            foreach ((float x, float zWall, float width, Biz biz) in new[] { (26f, 36f, 18f, Cafe), (45f, 33f, 12f, Chicken), (61f, 31f, 12f, Haejang), (77f, 33f, 12f, Bbq) })
            {
                Transform rear = EnvKit.Group(a, "RearShop_" + biz.Ko, new Vector3(x, SidewalkTop, zWall));
                kit.Block(rear, "Glass", new Vector3(0f, 0f, 0.04f), new Vector3(width - 1f, 2.8f, 0.05f), "Shopfront", collider: false);
                kit.Sign(rear, biz.Ko, new Vector3(0f, 3.4f, 0.12f), new Vector2(width * 0.8f, 0.8f), biz.Board, biz.Ko, biz.Text, biz.En);
            }

            foreach (float x in new[] { 36.5f, 53f, 69f }) // in the gaps between the rear shops
            {
                mod.Place(a, "Pojangmacha", new Vector3(x, SidewalkTop, 37.8f), 0f);
            }

            // Street life: outdoor tables, scooters, bins.
            mod.Place(a, "DeliveryScooter", new Vector3(30f, SidewalkTop, 45f), 100f);
            mod.Place(a, "DeliveryScooter", new Vector3(57f, SidewalkTop, 45.3f), 75f);
            mod.Place(a, "TrashBins", new Vector3(40f, SidewalkTop, 46.2f), 180f);
            mod.Place(a, "Planter", new Vector3(70f, SidewalkTop, 45.5f), 0f);
            for (int i = 0; i < 3; i++)
            {
                mod.Place(a, "Bench", new Vector3(20f + i * 20f, SidewalkTop, 38.6f), 0f);
            }
        }

        // =====================================================================================
        // Street furniture
        // =====================================================================================

        private static void BuildStreetFurniture(Transform f)
        {
            // Street lights: boulevard both sides, arm over the road.
            for (float z = -66f; z <= 92f; z += 24f)
            {
                if (Mathf.Abs(z) < 16f) continue;
                mod.Place(f, "StreetLight", new Vector3(8.7f, SidewalkTop, z), -90f);
                mod.Place(f, "StreetLight", new Vector3(-8.7f, SidewalkTop, z), 90f);
            }

            for (float x = 20f; x <= 84f; x += 24f)
            {
                mod.Place(f, "StreetLight", new Vector3(x, SidewalkTop, 7.7f), 180f);
                mod.Place(f, "StreetLight", new Vector3(-x, SidewalkTop, 7.7f), 180f);
                mod.Place(f, "StreetLight", new Vector3(x, SidewalkTop, -7.7f), 0f);
                mod.Place(f, "StreetLight", new Vector3(-x, SidewalkTop, -7.7f), 0f);
            }

            // Ginkgo trees between the lights.
            for (float z = -64f; z <= 94f; z += 12f)
            {
                if (Mathf.Abs(z) < 16f) continue;
                if (Mathf.Abs(z - 42f) < 5f) continue; // alley gate
                foreach (float x in new[] { -9.4f, 9.4f })
                {
                    if (x < 0 && z > -54f && z < -44f) continue; // safehouse door
                    mod.Place(f, "GinkgoTree", new Vector3(x, SidewalkTop, z));
                }
            }

            for (float x = 26f; x <= 84f; x += 12f)
            {
                foreach (float sx in new[] { -1f, 1f })
                {
                    foreach (float z in new[] { 9.6f, -9.6f })
                    {
                        float px = sx * x;
                        if (z > 0 && px > 21f && px < 34f) continue; // subway entrance
                        mod.Place(f, "GinkgoTree", new Vector3(px, SidewalkTop, z));
                    }
                }
            }

            // Bollards guard every crossing.
            foreach (float zc in new[] { 10.5f, -10.5f })
            {
                foreach (float x in new[] { -8.5f, 8.5f })
                {
                    for (int i = -1; i <= 1; i++) mod.Place(f, "Bollard", new Vector3(x, SidewalkTop, zc + i * 1.6f));
                }
            }

            foreach (float xc in new[] { 10.5f, -10.5f })
            {
                foreach (float z in new[] { -7.5f, 7.5f })
                {
                    for (int i = -1; i <= 1; i++) mod.Place(f, "Bollard", new Vector3(xc + i * 1.6f, SidewalkTop, z));
                }
            }

            // Warning tactile pads at the crossings.
            foreach ((float x, float z, bool alongX) in new[] { (8.9f, 10.5f, false), (-8.9f, 10.5f, false), (8.9f, -10.5f, false), (-8.9f, -10.5f, false),
                                                              (10.5f, 7.9f, true), (-10.5f, 7.9f, true), (10.5f, -7.9f, true), (-10.5f, -7.9f, true) })
            {
                Vector3 size = alongX ? new Vector3(3.6f, 0.01f, 0.6f) : new Vector3(0.6f, 0.01f, 3.6f);
                kit.Block(f, "TactileWarning", new Vector3(x, SidewalkTop, z), size, "Tactile", EnvTextureFactory.TactileTile, collider: false, castShadows: false);
            }

            // Bus stops, benches, bins.
            mod.Place(f, "BusStop", new Vector3(12.4f, SidewalkTop, -38f), -90f);
            mod.Place(f, "BusStop", new Vector3(-12.4f, SidewalkTop, 36f), 90f);
            mod.Place(f, "Bench", new Vector3(13.3f, SidewalkTop, 58f), -90f);
            mod.Place(f, "Bench", new Vector3(-13.3f, SidewalkTop, -30f), 90f);
            mod.Place(f, "TrashBins", new Vector3(13.4f, SidewalkTop, -22f), -90f);
            mod.Place(f, "TrashBins", new Vector3(-13.4f, SidewalkTop, 60f), 90f);
            mod.Place(f, "TrashBins", new Vector3(40f, SidewalkTop, 13.4f), 180f);
            mod.Place(f, "Planter", new Vector3(13f, SidewalkTop, 30f), 90f);
            mod.Place(f, "Planter", new Vector3(-13f, SidewalkTop, -60f), 90f);
            mod.Place(f, "DeliveryScooter", new Vector3(61f, SidewalkTop, 13.2f), 80f);

            // Road-name signs on the corners (blue Korean street signs).
            RoadNameSign(f, new Vector3(13.6f, SidewalkTop, 13.6f), 225f, "강남대로", "Gangnam-daero");
            RoadNameSign(f, new Vector3(-13.6f, SidewalkTop, -13.6f), 45f, "테헤란로", "Teheran-ro");

            // Signals: boulevard red (cars waiting), cross road green; pedestrians match.
            mod.Place(f, "SignalMast_Red", new Vector3(9f, SidewalkTop, 14f), 0f);
            mod.Place(f, "SignalMast_Red", new Vector3(-9f, SidewalkTop, -14f), 180f);
            mod.Place(f, "SignalMast_Green", new Vector3(14f, SidewalkTop, -9f), 90f);
            mod.Place(f, "SignalMast_Green", new Vector3(-14f, SidewalkTop, 9f), -90f);
            foreach (float zc in new[] { 10.5f, -10.5f })
            {
                mod.Place(f, "PedSignal_Green", new Vector3(8.6f, SidewalkTop, zc + 2.6f), 90f);
                mod.Place(f, "PedSignal_Green", new Vector3(-8.6f, SidewalkTop, zc - 2.6f), -90f);
            }

            foreach (float xc in new[] { 10.5f, -10.5f })
            {
                mod.Place(f, "PedSignal_Red", new Vector3(xc + 2.6f, SidewalkTop, 7.6f), 0f);
                mod.Place(f, "PedSignal_Red", new Vector3(xc - 2.6f, SidewalkTop, -7.6f), 180f);
            }

            // A few shopfront spill lights so storefronts light the pavement.
            foreach (Vector3 p in new[] { new Vector3(14.5f, 2.5f, 20f), new Vector3(-15f, 2.5f, 22f), new Vector3(14.5f, 2.5f, -24f),
                                           new Vector3(-15f, 2.5f, -22f), new Vector3(45f, 2.5f, 13f), new Vector3(-45f, 2.5f, 13f) })
            {
                AddLight(f, p, new Color(1f, 0.85f, 0.65f), 3f, 9f);
            }
        }

        private static void RoadNameSign(Transform parent, Vector3 position, float yaw, string ko, string en)
        {
            Transform t = EnvKit.Group(parent, "RoadName_" + ko, position, yaw);
            kit.Primitive(t, PrimitiveType.Cylinder, "Pole", new Vector3(0f, 1.6f, 0f), new Vector3(0.1f, 1.6f, 0.1f), "MetalDark", collider: true);
            kit.Sign(t, ko, new Vector3(0f, 3.1f, 0.08f), new Vector2(2.2f, 0.65f), "RoadSignBlue", ko, Color.white, en, depth: 0.06f);
        }

        // =====================================================================================
        // Traffic: cars queued at the red light on the boulevard (both directions)
        // =====================================================================================

        private static void BuildTraffic(Transform t)
        {
            mod.Place(t, "Car_White", new Vector3(2f, 0f, -17.5f), 0f);
            mod.Place(t, "Taxi", new Vector3(6f, 0f, -17f), 0f);
            mod.Place(t, "Car_Black", new Vector3(2f, 0f, -24.5f), 0f);
            mod.Place(t, "Car_Silver", new Vector3(6f, 0f, -24f), 0f);
            mod.Place(t, "Car_Blue", new Vector3(-2f, 0f, 17.5f), 180f);
            mod.Place(t, "Taxi", new Vector3(-6f, 0f, 18f), 180f);
            mod.Place(t, "Car_White", new Vector3(-2f, 0f, 24.6f), 180f);
            // Parked near the parking ramp.
            mod.Place(t, "Car_Silver", new Vector3(-48f, SidewalkTop, -38f), 0f);
            mod.Place(t, "Car_Black", new Vector3(-35f, SidewalkTop, -40f), 0f);
        }

        // =====================================================================================
        // Skyline: apartment complexes (아파트) and distant towers, outside the playable area
        // =====================================================================================

        private static void BuildBackground(Transform s)
        {
            int n = 101;
            foreach (float x in new[] { -80f, -25f, 30f, 85f })
                mod.Apartment(s, "Apt_N", new Vector3(x, 0f, 120f), 180f, 42f, 13f, 25, $"{n++}동", "미래힐스");
            n = 201;
            foreach (float x in new[] { -70f, -15f, 40f })
                mod.Apartment(s, "Apt_S", new Vector3(x, 0f, -96f), 0f, 42f, 13f, 22, $"{n++}동", "누리파크");
            n = 301;
            foreach (float z in new[] { -40f, 15f, 70f })
                mod.Apartment(s, "Apt_E", new Vector3(114f, 0f, z), -90f, 42f, 13f, 28, $"{n++}동", "미래힐스");
            n = 401;
            foreach (float z in new[] { -30f, 25f, 80f })
                mod.Apartment(s, "Apt_W", new Vector3(-114f, 0f, z), 90f, 42f, 13f, 24, $"{n++}동", "누리파크");

            mod.GlassTower(s, "Far_1", new Vector3(10f, 0f, 160f), 180f, 26f, 26f, 140f, "SignBlue", B("한결", "HANGYEOL", "SignDark", new Color(0.65f, 0.8f, 1f)), new Biz[0]);
            mod.GlassTower(s, "Far_2", new Vector3(150f, 0f, 120f), -90f, 24f, 24f, 110f, "SignMagenta", B("미래", "MIRAE", "SignDark", new Color(0.6f, 0.95f, 1f)), new Biz[0]);
            mod.GlassTower(s, "Far_3", new Vector3(-150f, 0f, -110f), 90f, 24f, 24f, 100f, "SignCyan", B("누리", "NURI", "SignDark", new Color(0.4f, 1f, 0.7f)), new Biz[0]);
        }

        // =====================================================================================
        // World bounds, lighting, statics
        // =====================================================================================

        private static void BuildBounds(Scene scene)
        {
            GameObject bounds = GameObject.Find("WorldBounds") ?? new GameObject("WorldBounds");
            for (int i = bounds.transform.childCount - 1; i >= 0; i--) Object.DestroyImmediate(bounds.transform.GetChild(i).gameObject);

            Rect a = WalkableArea;
            void Wall(string name, Vector3 center, Vector3 size)
            {
                GameObject wall = new GameObject(name);
                wall.transform.SetParent(bounds.transform, false);
                wall.transform.position = center;
                wall.AddComponent<BoxCollider>().size = size;
            }

            Wall("Boundary_East", new Vector3(a.xMax + 0.5f, 10f, a.center.y), new Vector3(1f, 30f, a.height + 2f));
            Wall("Boundary_West", new Vector3(a.xMin - 0.5f, 10f, a.center.y), new Vector3(1f, 30f, a.height + 2f));
            Wall("Boundary_North", new Vector3(a.center.x, 10f, a.yMax + 0.5f), new Vector3(a.width + 2f, 30f, 1f));
            Wall("Boundary_South", new Vector3(a.center.x, 10f, a.yMin - 0.5f), new Vector3(a.width + 2f, 30f, 1f));
        }

        private static void AddLight(Transform parent, Vector3 position, Color color, float intensity, float range)
        {
            Light light = new GameObject("Light").AddComponent<Light>();
            light.transform.SetParent(parent, false);
            light.transform.localPosition = position;
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.shadows = LightShadows.None;
        }

        /// <summary>Night lighting for any scene that shows the district (game world, menus).</summary>
        public static void ApplyNightLighting(Transform root) => ConfigureLighting(root);

        /// <summary>About one neon sign in eight flickers now and then (deterministic choice).</summary>
        internal static void AddNeonFlicker(Transform root)
        {
            var random = new System.Random(2026);
            int count = 0;
            foreach (TextMeshPro text in root.GetComponentsInChildren<TextMeshPro>(true))
            {
                if (text.fontSharedMaterial == null || !text.fontSharedMaterial.name.Contains("Neon")) continue;
                if (random.NextDouble() > 0.125) continue;
                text.gameObject.AddComponent<Contract.VFX.NeonFlicker>().Configure(random.Next(), new Vector2(3f, 12f));
                count++;
            }

            Debug.Log($"[GangnamDistrictBuilder] Neon flicker on {count} signs.");
        }

        private static void ConfigureLighting(Transform root)
        {
            // Moonlight: cool, low, soft shadows; the city's own lights do the rest.
            GameObject sun = GameObject.Find("Directional Light");
            if (sun != null)
            {
                Light light = sun.GetComponent<Light>();
                light.color = new Color(0.6f, 0.68f, 0.9f);
                light.intensity = 0.55f;
                light.shadows = LightShadows.Soft;
                sun.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            }

            RenderSettings.ambientMode = AmbientMode.Trilight;
            // Stage 9: a readable city night (silhouettes and pavements visible away from street lights).
            // Tuned by measurement (AtmosphereTests.City_IsReadableAtNight): mean street-view luminance ~0.11.
            RenderSettings.ambientSkyColor = new Color(0.54f, 0.61f, 0.88f);
            RenderSettings.ambientEquatorColor = new Color(0.42f, 0.38f, 0.48f);
            RenderSettings.ambientGroundColor = new Color(0.16f, 0.15f, 0.18f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.1f, 0.11f, 0.17f);
            RenderSettings.fogDensity = 0.0042f;

            // Exposure lift. GameWorld blends two global volumes (scene look + the effects controller's
            // profile, which has priority), so both carry the same value.
            foreach (string path in new[] { "Assets/Settings/ContractPostProcess.asset", "Assets/Settings/PostProcessingProfile.asset" })
            {
                var profile = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>(path);
                if (profile != null && profile.TryGet(out UnityEngine.Rendering.Universal.ColorAdjustments color))
                {
                    color.postExposure.overrideState = true;
                    color.postExposure.value = 0.75f;
                    EditorUtility.SetDirty(profile);
                }
            }

            Camera main = Camera.main;
            if (main != null)
            {
                main.clearFlags = CameraClearFlags.SolidColor;
                main.backgroundColor = new Color(0.07f, 0.08f, 0.13f);
                main.farClipPlane = 600f;
            }

            // One realtime reflection probe (rendered once on load) for the glass towers and wet road.
            GameObject probeObject = new GameObject("CityReflections");
            probeObject.transform.SetParent(root, false);
            probeObject.transform.position = new Vector3(0f, 12f, 12f);
            ReflectionProbe probe = probeObject.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
            probe.size = new Vector3(200f, 120f, 200f);
            probe.resolution = 128;
            probe.boxProjection = false;
            probe.farClipPlane = 400f;
            probe.cullingMask = ~(1 << 13); // never capture the flat map geometry (Minimap layer) floating above the city
        }

        internal static void MarkStatic(Transform root)
        {
            const StaticEditorFlags flags = StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic
                                            | StaticEditorFlags.ReflectionProbeStatic; // realtime-lit city: no ContributeGI / lightmaps
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                // TMP regenerates its mesh at runtime - it must not be static-batched.
                bool text = t.GetComponent<TextMeshPro>() != null;
                GameObjectUtility.SetStaticEditorFlags(t.gameObject, text ? StaticEditorFlags.OccludeeStatic : flags);
            }
        }

        internal static void PrewarmFont(Transform root)
        {
            string all = string.Concat(root.GetComponentsInChildren<TextMeshPro>(true).Select(t => t.text));
            kit.Font.TryAddCharacters(all, out string missing);
            if (!string.IsNullOrEmpty(missing)) Debug.LogWarning($"[GangnamDistrictBuilder] Glyphs missing from Noto Sans KR: '{missing}'");
            EditorUtility.SetDirty(kit.Font);
        }

        private static void RenameRegion()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:RegionData"))
            {
                RegionData region = AssetDatabase.LoadAssetAtPath<RegionData>(AssetDatabase.GUIDToAssetPath(guid));
                if (region != null && region.regionId == "region_downtown")
                {
                    region.regionName = "Gangnam 강남";
                    region.description = "Seoul's glass-and-neon business district: 강남대로 towers, 강남역 and the food alley behind it.";
                    EditorUtility.SetDirty(region);
                }
            }
        }
    }
}
