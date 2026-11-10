using System.Collections.Generic;
using System.Linq;
using Contract.Data;
using Contract.Interaction;
using Contract.Map;
using Contract.NPCs;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using P = Contract.Editor.World.GangnamPopulationBuilder;

namespace Contract.Editor.World
{
    /// <summary>
    /// Seoul · Myeongdong (명동), the second playable district: pedestrian shopping streets lined
    /// with cosmetics, fashion and money-exchange shops under stacked Hangul signs, street-food
    /// carts down the middle of 명동8길, a Gothic red-brick cathedral on its plaza (inspired by
    /// Myeongdong Cathedral, CC0 Poly Haven brick / roof / paving textures), the arterial road
    /// with Myeongdong Station's exit. Contains both Myeongdong contracts (a cosmetics stockroom
    /// and a loading dock), a guesthouse safehouse, crowd, navmesh, map and GPS walking route.
    /// The scene is generated from GameWorld (player, HUD, managers, UI) with Gangnam removed.
    /// Headless: -executeMethod Contract.Editor.World.MyeongdongBuilder.Build
    /// </summary>
    public static class MyeongdongBuilder
    {
        public const string ScenePath = "Assets/Scenes/Myeongdong.unity";
        public const string RootName = "District_Myeongdong";
        public const string PrefabPath = "Assets/Prefabs/Environment/District_Myeongdong.prefab";
        private const string NavMeshAsset = "Assets/Scenes/Myeongdong/NavMesh_Myeongdong.asset";
        private const float Top = 0.15f;  // pedestrian paving top (roads at 0)
        private const float GF = KoreanModules.GroundFloorHeight;

        public static readonly Vector3 PlayerSpawn = new Vector3(-35f, Top + 0.05f, 19f);
        public static readonly Vector3 Extraction = new Vector3(-12f, Top, -46f);

        // Interiors used by the contracts (world space).
        private static readonly Rect Stockroom = Rect.MinMaxRect(23f, -44f, 31f, -28f);
        private static readonly Rect DockYard = Rect.MinMaxRect(-40f, -44f, -24f, -24f);
        private static readonly Rect Guesthouse = Rect.MinMaxRect(-42f, 24f, -30f, 34f);

        private static EnvKit kit;
        private static KoreanModules mod;
        private static System.Random rng;

        private static readonly Color W = Color.white, K = new Color(0.08f, 0.08f, 0.1f), Pink = new Color(1f, 0.55f, 0.75f), Gold = new Color(1f, 0.82f, 0.4f);
        private static Biz B(string ko, string en, string board, Color text) => new Biz(ko, en, board, text);

        private static readonly Biz Haneul = B("하늘 코스메틱", "HANEUL COSMETICS", "SignWhite", new Color(0.2f, 0.45f, 0.9f));
        private static readonly Biz Dalbit = B("달빛 뷰티", "DALBIT BEAUTY", "SignMagenta", W);
        private static readonly Biz SkinLab = B("명동 스킨랩", "SKIN LAB", "SignMint", K);
        private static readonly Biz Bomnal = B("봄날 화장품", "BOMNAL", "SignWhite", new Color(0.85f, 0.2f, 0.45f));
        private static readonly Biz Sale = B("수분크림 1+1", "SALE", "SignYellow", new Color(0.7f, 0.05f, 0.1f));
        private static readonly Biz Exchange = B("환전소", "MONEY EXCHANGE", "SignGreen", W);
        private static readonly Biz Youth = B("청춘 패션", "YOUTH FASHION", "SignDark", Pink);
        private static readonly Biz Shoes = B("명동 슈즈", "SHOES", "SignWhite", K);
        private static readonly Biz Kalguksu = B("명동 칼국수", "NOODLES", "SignOrange", W);
        private static readonly Biz Mandu = B("왕만두", "DUMPLINGS", "SignRed", W);
        private static readonly Biz Cafe = B("카페 명동", "CAFE", "SignWhite", new Color(0.4f, 0.25f, 0.12f));
        private static readonly Biz Conv = B("하루 편의점 24", "HARU 24", "SignMint", K);
        private static readonly Biz Optical = B("밝은 안경", null, "SignBlue", W);
        private static readonly Biz Nail = B("네일샵", null, "SignMagenta", W);
        private static readonly Biz Derma = B("맑은 피부과", null, "SignWhite", new Color(0.1f, 0.4f, 0.4f));
        private static readonly Biz Noraebang = B("별빛 노래방", null, "SignDark", new Color(1f, 0.35f, 0.85f));
        private static readonly Biz Hostel = B("명동 게스트하우스", "GUESTHOUSE", "SignDark", Gold);
        private static readonly Biz Photo = B("인생네컷", "PHOTO BOOTH", "SignPurple", W);
        private static readonly Biz Chicken = B("명동 치킨", "CHICKEN", "SignYellow", new Color(0.6f, 0.08f, 0.05f));
        private static readonly Biz Bank = B("한빛 은행", "HANBIT BANK", "SignBlue", W);
        private static readonly Biz Dept = B("명동 백화점", "MYEONGDONG DEPT. STORE", "SignDark", Gold);

        [MenuItem("CONTRACT/World/Build Myeongdong District")]
        public static void Build()
        {
            EnvTextureFactory.GenerateAll();
            kit = new EnvKit();
            mod = new KoreanModules(kit);
            rng = new System.Random(2027);
            P.kit = kit;
            GangnamDistrictBuilder.kit = kit;
            P.baseMaterial = AssetDatabase.LoadAssetAtPath<Material>(P.BaseMaterialPath);

            Scene scene = CreateSceneFromGameWorld();
            Transform keep = StripGangnam();

            Transform root = new GameObject(RootName).transform;
            BuildGround(EnvKit.Group(root, "Ground"));
            BuildBuildings(EnvKit.Group(root, "Buildings"));
            BuildInteriors(EnvKit.Group(root, "Interiors"));
            BuildCathedral(EnvKit.Group(root, "Cathedral", new Vector3(58f, Top, 18f)));
            BuildStreetLife(EnvKit.Group(root, "StreetLife"));
            BuildBackground(EnvKit.Group(root, "Skyline"));
            BuildBounds(root);
            GangnamDistrictBuilder.ApplyNightLighting(root);
            GangnamDistrictBuilder.MarkStatic(root);
            GangnamDistrictBuilder.PrewarmFont(root);
            GangnamDistrictBuilder.AddNeonFlicker(root);
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PrefabPath));
            PrefabUtility.SaveAsPrefabAssetAndConnect(root.gameObject, PrefabPath, InteractionMode.AutomatedAction);

            PlaceGameplay(keep);
            Transform population = new GameObject("Myeongdong_Population").transform;
            Transform missions = new GameObject("Myeongdong_MissionContent").transform;
            BuildPointsOfInterest(EnvKit.Group(population, "PointsOfInterest"));
            BuildMissionContent(missions, population);
            P.ConfigureNpcManager(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/NPCs/Civilian.prefab"));
            P.BakeNavMesh("NavMesh_Myeongdong", NavMeshAsset, new[]
            {
                Rect.MinMaxRect(-80f, -75f, -4f, -50f), Rect.MinMaxRect(4f, -75f, 100f, -50f),  // 을지로, zebra crossing left open
                Rect.MinMaxRect(-80f, -50f, -58f, 90f)                                          // 남대문로
            });
            BuildMap();
            GpsRouteBuilder.BuildInOpenScene(new[]
            {
                new Vector3(-3f, Top, -44f), new Vector3(-3f, Top, 72f), new Vector3(3f, Top, 72f),
                new Vector3(52f, Top, 4f), new Vector3(40f, Top, 18f), new Vector3(-52f, Top, 18f), new Vector3(3f, Top, -44f)
            });

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            RegisterDistrict();
            Debug.Log($"[MyeongdongBuilder] Done (bake occlusion next: OcclusionBaker.Bake): {root.Find("Buildings").childCount} buildings, " +
                      $"{root.GetComponentsInChildren<TMPro.TextMeshPro>(true).Length} text elements, " +
                      $"{population.GetComponentsInChildren<NpcController>(true).Length} placed NPCs.");
        }

        // =====================================================================================
        // Scene
        // =====================================================================================

        private static Scene CreateSceneFromGameWorld()
        {
            // Overwrite the existing scene file in place so its GUID (in the .meta) stays the same across
            // rebuilds: deleting and copying gave Myeongdong a new GUID every time.
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                System.IO.File.Copy("Assets/Scenes/GameWorld.unity", ScenePath, true);
                AssetDatabase.ImportAsset(ScenePath, ImportAssetOptions.ForceUpdate);
            }
            else
            {
                AssetDatabase.CopyAsset("Assets/Scenes/GameWorld.unity", ScenePath);
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            // The copy still points at Gangnam's baked occlusion data, which would cull Myeongdong's
            // geometry wrongly. Clear it; OcclusionBaker bakes Myeongdong's own data.
            StaticOcclusionCulling.Clear();
            return scene;
        }

        /// <summary>Removes Gangnam content; returns a holder with the extraction, safehouse terminal and spawn.</summary>
        private static Transform StripGangnam()
        {
            Transform keep = new GameObject("Myeongdong_Gameplay").transform;
            ExtractionInteractable extraction = Object.FindAnyObjectByType<ExtractionInteractable>();
            ContractBoardInteractable board = Object.FindAnyObjectByType<ContractBoardInteractable>();
            GameObject spawn = GameObject.Find("PlayerSpawn");
            foreach (Component c in new Component[] { extraction, board })
                if (c != null) c.transform.SetParent(keep, true);
            if (spawn != null) spawn.transform.SetParent(keep, true);
            GameObject extractionZone = GameObject.Find("ExtractionZone");
            if (extractionZone != null) extractionZone.transform.SetParent(keep, true);

            foreach (string name in new[] { "District_Gangnam", "Gangnam_Population", "Gangnam_MissionContent", "NavMesh_Gangnam", "Gangnam_Map",
                         "GpsRoute", "WorldBounds", "WorldMarkers", "GameplayStructures", "Interactables" })
            {
                GameObject go = GameObject.Find(name);
                if (go != null) Object.DestroyImmediate(go);
            }

            return keep;
        }

        // =====================================================================================
        // Ground: roads (을지로 south, 남대문로 west), paved pedestrian streets, cathedral plaza
        // =====================================================================================

        private static void BuildGround(Transform g)
        {
            // Surrounding city ground so the distant skyline stands on something.
            kit.Block(g, "OuterGround", new Vector3(10f, -0.3f, -20f), new Vector3(700f, 0.1f, 700f), "Asphalt", new Vector2(40f, 40f), collider: false);
            kit.Block(g, "Road_Euljiro", new Vector3(10f, -0.2f, -62.5f), new Vector3(180f, 0.2f, 25f), "Asphalt", new Vector2(6f, 6f));
            kit.Block(g, "Road_Namdaemun", new Vector3(-69f, -0.2f, 20f), new Vector3(22f, 0.2f, 140f), "Asphalt", new Vector2(6f, 6f));
            kit.Block(g, "Paving", new Vector3(-5f, 0f, 16f), new Vector3(106f, Top, 132f), "PH_Paving", new Vector2(2f, 2f));
            kit.Block(g, "Plaza", new Vector3(71f, 0f, 16f), new Vector3(46f, Top, 132f), "PH_Granite", new Vector2(2.5f, 2.5f));
            kit.Block(g, "Kerb_S", new Vector3(16f, 0f, -50.15f), new Vector3(148f, Top, 0.3f), "Curb", collider: false);

            // Zebra crossing across 을지로 at the end of 명동8길, lane lines and the centre line.
            for (float x = -3.5f; x <= 3.6f; x += 1.4f) kit.Block(g, "Zebra", new Vector3(x, 0.005f, -62f), new Vector3(0.7f, 0.01f, 22f), "PaintWhite", collider: false, castShadows: false);
            kit.Block(g, "CentreLine", new Vector3(10f, 0.006f, -62.5f), new Vector3(180f, 0.01f, 0.3f), "PaintYellow", collider: false, castShadows: false);
            foreach (float z in new[] { -56.5f, -68.5f })
                for (float x = -75f; x < 95f; x += 6f)
                    if (x < -6f || x > 6f) kit.Block(g, "Lane", new Vector3(x, 0.006f, z), new Vector3(3f, 0.01f, 0.15f), "PaintWhite", collider: false, castShadows: false);
            kit.Text(g, "RoadText", "을지로", new Vector3(30f, 0.012f, -58f), new Vector2(6f, 2f), new Color(0.86f, 0.86f, 0.84f), kit.PaintTextMaterial, new Vector3(90f, 0f, 0f));
        }

        // =====================================================================================
        // Buildings (dense mid-rise shops). Front faces the street.
        // =====================================================================================

        private static Biz[] Floors(params Biz[] b) => b;

        private static void Row(Transform parent, string prefix, float fixedCoord, bool alongZ, float from, float to, float yaw, float depth,
            Biz[] grounds, Biz[][] floors, int[] storeys, (float from, float to)[] gaps = null)
        {
            float cursor = from;
            int i = 0;
            float dir = Mathf.Sign(to - from);
            while (dir * (to - cursor) > 6f)
            {
                float width = 9f + (float)rng.NextDouble() * 5f;
                if (dir * (to - cursor) - width < 6f) width = dir * (to - cursor);
                float centre = cursor + dir * width / 2f;
                bool blocked = gaps != null && gaps.Any(g => Overlaps(cursor, cursor + dir * width, g.from, g.to));
                if (!blocked)
                {
                    Vector3 position = alongZ ? new Vector3(fixedCoord, Top, centre) : new Vector3(centre, Top, fixedCoord);
                    Biz ground = grounds[i % grounds.Length];
                    Biz[] stack = floors[i % floors.Length];
                    Biz? vertical = i % 3 == 0 ? stack.FirstOrDefault() : (Biz?)null;
                    mod.Commercial(parent, $"{prefix}_{i}", position, yaw, width - 0.4f, depth, storeys[i % storeys.Length], ground, stack, vertical,
                        i % 4 == 1 ? Dalbit : (Biz?)null);
                }

                cursor += dir * width;
                i++;
            }
        }

        private static bool Overlaps(float a0, float a1, float b0, float b1) => Mathf.Max(Mathf.Min(a0, a1), Mathf.Min(b0, b1)) < Mathf.Min(Mathf.Max(a0, a1), Mathf.Max(b0, b1));

        private static void BuildBuildings(Transform b)
        {
            Biz[] cosmetics = { Haneul, Bomnal, SkinLab, Sale, Exchange, Dalbit };
            Biz[][] stacks =
            {
                Floors(Optical, Nail, Derma, Exchange, Noraebang), Floors(Exchange, Photo, Nail, Derma),
                Floors(Derma, Optical, Exchange, Cafe), Floors(Noraebang, Photo, Exchange, Nail, Optical)
            };
            int[] heights = { 6, 5, 7, 4, 8, 5 };
            var alleys = new[] { (-23f, -19f) };   // service alleys into the block yards

            // 명동8길 (main pedestrian street): west and east rows.
            Row(b, "W8", -15f, true, -48f, 12f, 90f, 14f, cosmetics, stacks, heights, alleys);
            Row(b, "W8n", -15f, true, 24f, 78f, 90f, 14f, new[] { Youth, Cafe, Kalguksu, Haneul }, stacks, heights);
            Row(b, "E8", 15f, true, -48f, 12f, -90f, 14f, new[] { SkinLab, Exchange, Bomnal, Mandu, Dalbit }, stacks, heights, alleys);
            Row(b, "E8n", 15f, true, 24f, 78f, -90f, 14f, new[] { Shoes, Haneul, Conv, Sale }, stacks, heights);

            // 명동길 (cross street): south and north sides, west and east of the main street.
            Row(b, "CrossSW", 6f, false, -22f, -56f, 0f, 12f, new[] { Kalguksu, Exchange, Chicken }, stacks, heights);
            Row(b, "CrossSE", 6f, false, 22f, 46f, 0f, 12f, new[] { Cafe, Youth }, stacks, heights);
            Row(b, "CrossNW", 30f, false, -22f, -29f, 180f, 12f, new[] { Photo }, stacks, heights);
            Row(b, "CrossNW2", 30f, false, -43f, -56f, 180f, 12f, new[] { Mandu }, stacks, heights);
            Row(b, "CrossNE", 30f, false, 22f, 46f, 180f, 12f, new[] { Chicken, Bomnal }, stacks, heights);

            // Block backs along 남대문로 and the north edge (seen from the road / skyline).
            Row(b, "Namdaemun", -49f, true, -48f, 0f, -90f, 14f, new[] { Bank, Conv, Exchange }, stacks, heights, new[] { (-44f, -24f) });
            Row(b, "NamdaemunN", -49f, true, 38f, 78f, -90f, 14f, new[] { Conv, Exchange }, stacks, heights);
            Row(b, "EastBack", 39f, true, -48f, -2f, 90f, 14f, new[] { Bank, Cafe }, stacks, heights, new[] { (-44f, -24f) });
            Row(b, "EastBackN", 39f, true, 36f, 78f, -90f, 14f, new[] { Cafe, Youth }, stacks, heights);

            // South side of 을지로: department store and bank opposite the station exit.
            mod.GlassTower(b, "DeptStore", new Vector3(-30f, 0f, -84f), 0f, 40f, 18f, 34f, "SignMagenta", Dept, new[] { Dept, Cafe });
            mod.GlassTower(b, "HanbitBank", new Vector3(30f, 0f, -84f), 0f, 26f, 18f, 58f, "SignBlue", Bank, new[] { Bank, Exchange });
        }

        // =====================================================================================
        // Interiors: guesthouse (safehouse), cosmetics stockroom (contract 1), loading dock (contract 2)
        // =====================================================================================

        private static void Room(Transform parent, string name, Rect r, float height, string wall, string floor, bool doorNorth, bool doorSouth, float doorX)
        {
            Transform room = EnvKit.Group(parent, name);
            float y = Top;
            kit.Block(room, "Floor", new Vector3(r.center.x, y, r.center.y), new Vector3(r.width, 0.05f, r.height), floor, collider: false);
            kit.Block(room, "Ceiling", new Vector3(r.center.x, y + height, r.center.y), new Vector3(r.width, 0.3f, r.height), "Concrete");
            void WallX(float z, bool door)
            {
                if (!door)
                {
                    kit.Block(room, "Wall", new Vector3(r.center.x, y, z), new Vector3(r.width, height, 0.3f), wall);
                    return;
                }

                float left = doorX - 1.1f - r.xMin, right = r.xMax - (doorX + 1.1f);
                kit.Block(room, "WallL", new Vector3(r.xMin + left / 2f, y, z), new Vector3(left, height, 0.3f), wall);
                kit.Block(room, "WallR", new Vector3(r.xMax - right / 2f, y, z), new Vector3(right, height, 0.3f), wall);
                kit.Block(room, "Lintel", new Vector3(doorX, y + 2.6f, z), new Vector3(2.2f, height - 2.6f, 0.3f), wall);
            }

            WallX(r.yMax, doorNorth);
            WallX(r.yMin, doorSouth);
            kit.Block(room, "WallW", new Vector3(r.xMin, y, r.center.y), new Vector3(0.3f, height, r.height), wall);
            kit.Block(room, "WallE", new Vector3(r.xMax, y, r.center.y), new Vector3(0.3f, height, r.height), wall);
            Light lamp = new GameObject("Lamp").AddComponent<Light>();
            lamp.transform.SetParent(room, false);
            lamp.transform.position = new Vector3(r.center.x, y + height - 0.4f, r.center.y);
            lamp.type = LightType.Point;
            lamp.color = new Color(1f, 0.9f, 0.75f);
            lamp.intensity = 2f;
            lamp.range = Mathf.Max(r.width, r.height);
        }

        private static void BuildInteriors(Transform t)
        {
            // Guesthouse lobby on 명동길 (door on the south wall, facing the street).
            Room(t, "Guesthouse", Guesthouse, 3.4f, "Commercial", "Wood", doorNorth: false, doorSouth: true, doorX: -36f);
            kit.Sign(t, "Guesthouse", new Vector3(-36f, Top + 3.9f, Guesthouse.yMin - 0.25f), new Vector2(7f, 1f), Hostel.Board, Hostel.Ko, Hostel.Text, Hostel.En, yaw: 180f);
            kit.Block(t, "Desk", new Vector3(-39f, Top, 31f), new Vector3(2.4f, 1f, 0.8f), "Wood");
            kit.Block(t, "Bunk", new Vector3(-32f, Top, 32f), new Vector3(2f, 1.6f, 1f), "MetalDark");

            // Cosmetics stockroom behind the 명동8길 shops (contract 1). Door faces the yard (north).
            Room(t, "Stockroom", Stockroom, 4.2f, "Concrete", "Concrete", doorNorth: true, doorSouth: false, doorX: 27f);
            kit.Sign(t, "Stockroom", new Vector3(27f, Top + 3.4f, Stockroom.yMax + 0.25f), new Vector2(5f, 0.8f), "SignDark", "관계자 외 출입금지", W, "STAFF ONLY", yaw: 0f);
            for (int i = 0; i < 4; i++)
            {
                kit.Block(t, "Shelf", new Vector3(24.2f, Top, -42f + i * 3.4f), new Vector3(1f, 2.6f, 2.6f), "MetalLight");
                kit.Block(t, "Boxes", new Vector3(24.2f, Top + 2.6f, -42f + i * 3.4f), new Vector3(0.9f, 0.6f, 2.4f), "Tarp", collider: false);
            }

            kit.Block(t, "Bench", new Vector3(29.5f, Top, -38f), new Vector3(1.2f, 0.95f, 3f), "MetalDark");

            // Loading dock yard (contract 2): container, pallets, dock canopy.
            kit.Block(t, "DockApron", new Vector3(DockYard.center.x, Top, DockYard.center.y), new Vector3(DockYard.width, 0.02f, DockYard.height), "Concrete", collider: false);
            kit.Block(t, "Container", new Vector3(-36f, Top, -36f), new Vector3(2.5f, 2.6f, 6.1f), "PaintBlue");
            kit.Text(t, "ContainerId", "HNKU 482 911", new Vector3(-34.73f, Top + 2.1f, -36f), new Vector2(2.4f, 0.4f), W, kit.SignTextMaterial, new Vector3(0f, -90f, 0f));
            kit.Block(t, "Pallet1", new Vector3(-29f, Top, -40f), new Vector3(1.2f, 0.9f, 1.2f), "Wood");
            kit.Block(t, "Pallet2", new Vector3(-27.5f, Top, -40f), new Vector3(1.2f, 1.4f, 1.2f), "Tarp");
            kit.Block(t, "DockDesk", new Vector3(-30f, Top, -30f), new Vector3(1.8f, 1f, 0.8f), "MetalDark");
        }

        // =====================================================================================
        // Cathedral (inspired by Myeongdong Cathedral): red-brick Gothic nave, bell tower and spire
        // =====================================================================================

        private static void BuildCathedral(Transform c)
        {
            // Local frame: façade faces -X (towards 명동길), nave runs +X.
            kit.Block(c, "Nave", new Vector3(16f, 0f, 0f), new Vector3(28f, 13f, 14f), "PH_Brick", new Vector2(1.5f, 1.5f));
            kit.Block(c, "AisleN", new Vector3(16f, 0f, 8.5f), new Vector3(26f, 7f, 3f), "PH_Brick", new Vector2(1.5f, 1.5f));
            kit.Block(c, "AisleS", new Vector3(16f, 0f, -8.5f), new Vector3(26f, 7f, 3f), "PH_Brick", new Vector2(1.5f, 1.5f));
            // Pitched slate roof: two sloped slabs.
            kit.Block(c, "RoofN", new Vector3(16f, 12.6f, 3.4f), new Vector3(28.4f, 0.4f, 8.6f), "PH_RoofTiles", new Vector2(2f, 2f), euler: new Vector3(-40f, 0f, 0f));
            kit.Block(c, "RoofS", new Vector3(16f, 12.6f, -3.4f), new Vector3(28.4f, 0.4f, 8.6f), "PH_RoofTiles", new Vector2(2f, 2f), euler: new Vector3(40f, 0f, 0f));
            // Bell tower on the façade, tapering spire with a cross.
            kit.Block(c, "Tower", new Vector3(0f, 0f, 0f), new Vector3(7f, 24f, 7f), "PH_Brick", new Vector2(1.5f, 1.5f));
            kit.Block(c, "Belfry", new Vector3(0f, 24f, 0f), new Vector3(6f, 5f, 6f), "PH_Brick", new Vector2(1.5f, 1.5f));
            for (int i = 0; i < 6; i++)
            {
                float s = 5.4f - i * 0.85f;
                kit.Block(c, "Spire" + i, new Vector3(0f, 29f + i * 2.4f, 0f), new Vector3(s, 2.4f, s), "PH_RoofTiles", new Vector2(2f, 2f), collider: false);
            }

            kit.Block(c, "CrossV", new Vector3(0f, 43.4f, 0f), new Vector3(0.25f, 2.2f, 0.25f), "MetalLight", collider: false);
            kit.Block(c, "CrossH", new Vector3(0f, 44.6f, 0f), new Vector3(0.25f, 0.25f, 1.2f), "MetalLight", collider: false);
            // Portal and rose window on the façade (warm glow).
            kit.Block(c, "Portal", new Vector3(-3.55f, 0f, 0f), new Vector3(0.2f, 4.5f, 2.8f), "Shopfront", collider: false);
            kit.Primitive(c, PrimitiveType.Cylinder, "RoseWindow", new Vector3(-3.6f, 15f, 0f), new Vector3(3f, 0.08f, 3f), "LampWarm", euler: new Vector3(0f, 0f, 90f), castShadows: false);
            for (int i = 0; i < 7; i++)
                kit.Block(c, "Lancet", new Vector3(4f + i * 3.6f, 2.5f, -7.05f), new Vector3(0.9f, 3.6f, 0.15f), "LampWarm", collider: false, castShadows: false);
            kit.Sign(c, "Cathedral", new Vector3(-6f, 2.8f, 6f), new Vector2(4.4f, 0.9f), "SignDark", "명동 성당", Gold, "CATHEDRAL", yaw: -90f);
            Light glow = new GameObject("PortalLight").AddComponent<Light>();
            glow.transform.SetParent(c, false);
            glow.transform.localPosition = new Vector3(-6f, 4f, 0f);
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.75f, 0.45f);
            glow.intensity = 3f;
            glow.range = 14f;
        }

        // =====================================================================================
        // Street life: food carts, lights, trees, benches, subway exit, traffic
        // =====================================================================================

        private static void FoodCart(Transform parent, string name, string food, string en, Vector3 position, float yaw, string canopy)
        {
            Transform t = EnvKit.Group(parent, "Cart_" + name, position, yaw);
            kit.Block(t, "Body", Vector3.zero, new Vector3(2.4f, 1f, 1.3f), "MetalLight");
            kit.Block(t, "Counter", new Vector3(0f, 1f, 0f), new Vector3(2.4f, 0.08f, 1.3f), "Steel", collider: false);
            foreach (float x in new[] { -1.1f, 1.1f })
                kit.Block(t, "Post", new Vector3(x, 1f, -0.55f), new Vector3(0.06f, 1.3f, 0.06f), "MetalDark", collider: false);
            kit.Block(t, "Canopy", new Vector3(0f, 2.3f, 0.1f), new Vector3(2.8f, 0.1f, 1.8f), canopy, collider: false);
            kit.Sign(t, "Menu", new Vector3(0f, 2.25f, 1.05f), new Vector2(2.6f, 0.5f), "SignYellow", food, new Color(0.55f, 0.08f, 0.05f), en, yaw: 0f, depth: 0.06f);
            mod.Steam(t, new Vector3(0.4f, 1.2f, 0f));
            BoxCollider box = t.gameObject.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 1.1f, 0f);
            box.size = new Vector3(2.4f, 2.2f, 1.3f);
        }

        private static void BuildStreetLife(Transform s)
        {
            (string ko, string en)[] foods = { ("계란빵", "EGG BREAD"), ("떡볶이", "TTEOKBOKKI"), ("회오리감자", "TORNADO POTATO"), ("닭꼬치", "CHICKEN SKEWERS"), ("붕어빵", "BUNGEOPPANG"), ("호떡", "HOTTEOK"), ("어묵", "FISH CAKE"), ("탕후루", "TANGHULU") };
            int f = 0;
            for (float z = -40f; z <= 68f; z += 12f)
            {
                if (z > 8f && z < 28f) continue; // keep the crossing with 명동길 open
                (string ko, string en) = foods[f % foods.Length];
                FoodCart(s, $"{f}", ko, en, new Vector3(f % 2 == 0 ? -1.8f : 1.8f, Top, z), f % 2 == 0 ? 90f : -90f, f % 3 == 0 ? "Tarp" : "PlasticRed");
                f++;
            }

            foreach (float z in new[] { -42f, -26f, -6f, 32f, 48f, 64f })
            {
                mod.Place(s, "StreetLight", new Vector3(-7.4f, Top, z), 90f);
                mod.Place(s, "StreetLight", new Vector3(7.4f, Top, z), -90f);
            }

            foreach (float x in new[] { -50f, -36f, 32f, 44f })
                mod.Place(s, "StreetLight", new Vector3(x, Top, 22.6f), 180f);
            foreach (float z in new[] { -8f, 40f })
            {
                mod.Place(s, "GinkgoTree", new Vector3(52f, Top, z));
                mod.Place(s, "Bench", new Vector3(54f, Top, z + 4f), -90f);
            }

            mod.Place(s, "Bench", new Vector3(-7f, Top, -14f), 90f);
            mod.Place(s, "TrashBins", new Vector3(7.2f, Top, -16f), -90f);
            mod.Place(s, "DeliveryScooter", new Vector3(-20f, Top, 13.3f), 0f);
            mod.Place(s, "Planter", new Vector3(48f, Top, 18f), 90f);

            // Myeongdong Station (Line 4) exit on 을지로.
            Transform exit = EnvKit.Group(s, "SubwayExit6", new Vector3(14f, Top, -48f));
            kit.Block(exit, "Canopy", new Vector3(0f, 2.6f, 0f), new Vector3(3.6f, 0.15f, 5f), "GlassClear", collider: false);
            foreach (float x in new[] { -1.7f, 1.7f })
                kit.Block(exit, "Wall", new Vector3(x, 0f, 0f), new Vector3(0.2f, 2.6f, 5f), "Steel");
            kit.Sign(exit, "Station", new Vector3(0f, 3.4f, -2.6f), new Vector2(3.6f, 0.7f), "SignWhite", "4  명동역  6", new Color(0.1f, 0.55f, 0.85f), "MYEONGDONG STN · EXIT 6", yaw: 180f);

            // Traffic on 을지로 and 남대문로 (parked / stopped cars).
            mod.Place(s, "Taxi", new Vector3(-20f, 0f, -59f), 90f);
            mod.Place(s, "Car_White", new Vector3(26f, 0f, -59f), 90f);
            mod.Place(s, "Car_Black", new Vector3(46f, 0f, -66f), -90f);
            mod.Place(s, "Taxi", new Vector3(-66f, 0f, 0f), 0f);
            mod.Place(s, "BusStop", new Vector3(-26f, Top, -51.5f), 180f);
        }

        private static void BuildBackground(Transform s)
        {
            int n = 101;
            foreach (float x in new[] { -60f, 0f, 60f })
                mod.Apartment(s, "Apt_N", new Vector3(x, 0f, 118f), 180f, 42f, 13f, 22, $"{n++}동", "남산타운");
            mod.GlassTower(s, "Far_1", new Vector3(-120f, 0f, 30f), 90f, 26f, 26f, 90f, "SignBlue", B("한빛 타워", "HANBIT TOWER", "SignDark", new Color(0.65f, 0.8f, 1f)), new Biz[0]);
            mod.GlassTower(s, "Far_2", new Vector3(140f, 0f, 20f), -90f, 24f, 24f, 70f, "SignMagenta", B("명동 호텔", "MYEONGDONG HOTEL", "SignDark", Gold), new Biz[0]);
            // N Seoul Tower on the hill to the south-east (simple silhouette, far away).
            Transform tower = EnvKit.Group(s, "NSeoulTower", new Vector3(170f, 40f, -220f));
            kit.Primitive(tower, PrimitiveType.Cylinder, "Shaft", new Vector3(0f, 60f, 0f), new Vector3(5f, 60f, 5f), "PaintWhite", castShadows: false);
            kit.Primitive(tower, PrimitiveType.Cylinder, "Deck", new Vector3(0f, 118f, 0f), new Vector3(16f, 5f, 16f), "LampCool", castShadows: false);
            kit.Primitive(tower, PrimitiveType.Cylinder, "Antenna", new Vector3(0f, 140f, 0f), new Vector3(1.6f, 20f, 1.6f), "SignalRed", castShadows: false);
        }

        private static void BuildBounds(Transform root)
        {
            Transform b = EnvKit.Group(root, "Bounds");
            void Wall(string name, Vector3 centre, Vector3 size)
            {
                GameObject go = new GameObject(name);
                go.transform.SetParent(b, false);
                go.transform.position = centre;
                go.AddComponent<BoxCollider>().size = size;
            }

            Wall("S", new Vector3(10f, 5f, -75f), new Vector3(200f, 10f, 1f));
            Wall("N", new Vector3(10f, 5f, 82f), new Vector3(200f, 10f, 1f));
            Wall("W", new Vector3(-80f, 5f, 4f), new Vector3(1f, 10f, 170f));
            Wall("E", new Vector3(94f, 5f, 4f), new Vector3(1f, 10f, 170f));
        }

        // =====================================================================================
        // Gameplay: spawn, extraction, safehouse terminal, contract content, crowd
        // =====================================================================================

        private static void PlaceGameplay(Transform keep)
        {
            foreach (ContractBoardInteractable board in keep.GetComponentsInChildren<ContractBoardInteractable>(true))
                board.transform.SetPositionAndRotation(new Vector3(-39f, Top + 1.05f, 31.45f), Quaternion.Euler(0f, 180f, 0f));
            foreach (ExtractionInteractable extraction in keep.GetComponentsInChildren<ExtractionInteractable>(true))
            {
                extraction.transform.position = Extraction + Vector3.up * 0.1f;
                var so = new SerializedObject(extraction);
                so.FindProperty("extractionId").stringValue = "extraction_industrial";
                so.FindProperty("requiredItemId").stringValue = "";
                so.ApplyModifiedPropertiesWithoutUndo();
                MapIcon icon = extraction.GetComponent<MapIcon>() ?? extraction.gameObject.AddComponent<MapIcon>();
                icon.Configure("extraction_industrial", MapIconType.ExtractionPoint, "탈출 지점 · Extraction (명동역)", null, null, true);
            }

            Transform zone = keep.Find("ExtractionZone");
            if (zone != null) zone.position = Extraction;
            Transform spawn = keep.Find("PlayerSpawn");
            if (spawn != null) spawn.position = PlayerSpawn;
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) player.transform.SetPositionAndRotation(PlayerSpawn, Quaternion.Euler(0f, 90f, 0f));
            GameObject camera = GameObject.FindGameObjectWithTag("MainCamera");
            if (camera != null) camera.transform.position = PlayerSpawn + new Vector3(-4.5f, 2.6f, 0.5f);

            GameObject safehouse = new GameObject("Safehouse_Guesthouse");
            safehouse.transform.SetParent(keep, false);
            safehouse.transform.position = new Vector3(-36f, Top, 29f);
            safehouse.AddComponent<MapIcon>().Configure("safehouse_industrial", MapIconType.Safehouse, "안가 · Safehouse (게스트하우스)", null, null, true);
        }

        private static void BuildPointsOfInterest(Transform p)
        {
            foreach (Transform building in GameObject.Find(RootName + "/Buildings").transform.Cast<Transform>())
            {
                BoxCollider ground = building.Find("GroundFloor")?.GetComponent<BoxCollider>();
                if (ground == null) continue;
                Vector3 front = building.TransformPoint(new Vector3(0f, 0f, ground.size.z / 2f + 1.4f));
                if (front.z < -49.5f || front.x < -57f) continue; // fronts on the roads: no window shoppers
                P.Poi(p, building.name, front + Vector3.up * 0.05f, -building.forward, PoiCategory.Shop, new Vector2(4f, 12f), 1.5f);
            }

            foreach (Transform cart in GameObject.Find(RootName + "/StreetLife").transform.Cast<Transform>().Where(t => t.name.StartsWith("Cart_")))
                P.Poi(p, "Food_" + cart.name, cart.TransformPoint(new Vector3(0f, 0f, 1.6f)), -cart.forward, PoiCategory.Food, new Vector2(8f, 20f), 0.8f);

            P.Poi(p, "Cathedral_Steps", new Vector3(50f, Top, 18f), Vector3.right, PoiCategory.Viewpoint, new Vector2(6f, 14f), 2f);
            P.Poi(p, "Cathedral_Plaza", new Vector3(56f, Top, 0f), Vector3.forward, PoiCategory.Viewpoint, new Vector2(6f, 14f), 2.5f);
            P.Poi(p, "SubwayExit", new Vector3(14f, Top, -45f), Vector3.back, PoiCategory.Transit, new Vector2(4f, 10f), 1.5f);
            P.Poi(p, "BusStop", new Vector3(-26f, Top, -50.6f), Vector3.back, PoiCategory.Transit, new Vector2(8f, 20f), 1.8f);
            P.Poi(p, "Crossing", new Vector3(0f, Top, -49f), Vector3.back, PoiCategory.Crossing, new Vector2(2f, 6f), 1f);
            P.Poi(p, "Bench_Plaza", new Vector3(53f, Top, -4f), Vector3.left, PoiCategory.Bench, new Vector2(8f, 20f), 0.6f);
            P.Poi(p, "Bench_Main", new Vector3(-6f, Top, -14f), Vector3.right, PoiCategory.Bench, new Vector2(8f, 20f), 0.6f);
            foreach (float z in new[] { -34f, -2f, 40f, 60f })
                P.Poi(p, "Stroll_" + z, new Vector3(0f, Top, z), Vector3.forward, PoiCategory.Viewpoint, new Vector2(2f, 5f), 3f);

            // Stockroom staff posts (guards patrol between them).
            P.Poi(p, "Post_StockroomDoor", new Vector3(27f, Top, -26.5f), Vector3.forward, PoiCategory.Patrol, new Vector2(6f, 12f), 0.4f, "stockroom");
            P.Poi(p, "Post_StockroomShelves", new Vector3(28.5f, Top, -36f), Vector3.left, PoiCategory.Patrol, new Vector2(6f, 12f), 0.4f, "stockroom");
            P.Poi(p, "Post_DockGate", new Vector3(-26f, Top, -26f), Vector3.right, PoiCategory.Patrol, new Vector2(6f, 12f), 0.4f, "dock");
        }

        private static void BuildMissionContent(Transform m, Transform population)
        {
            Transform origin = new GameObject("Origin").transform; // identity frame for world-space triggers
            GameObject civilianPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/NPCs/Civilian.prefab");
            Bounds Box(Rect r, float height) => new Bounds(new Vector3(r.center.x, Top + height / 2f, r.center.y), new Vector3(r.width - 0.6f, height, r.height - 0.6f));

            // ---- mission_industrial_01 "Stockroom Job" -----------------------------------------
            LocationTrigger stock = P.Trigger<LocationTrigger>(m, "Loc_warehouse_main", origin, Box(Stockroom, 3f));
            stock.Configure("warehouse_main", "화장품 창고 · Cosmetics stockroom", false);
            P.Icon(stock.gameObject, "warehouse_main", MapIconType.Objective, "화장품 창고 · Stockroom", "warehouse_main", "mission_industrial_01");
            P.Trigger<RestrictedZone>(m, "Restricted_Stockroom", origin, Box(Stockroom, 3f)).Configure("stockroom");
            P.Icon(P.Pickup(m, "prototype_tech", "신제품 포뮬러 · PROTOTYPE FORMULA", "The stolen cosmetics formula, in a sealed sample case.",
                    new Vector3(24.2f, Top + 1.3f, -35f), Quaternion.Euler(0f, 90f, 0f), new Vector3(0.4f, 0.12f, 0.3f), "SignWhite"),
                "prototype_tech", MapIconType.Objective, "신제품 포뮬러 · Prototype formula", "prototype_tech", "mission_industrial_01");

            // ---- mission_industrial_02 "Counterfeit Trail" -------------------------------------
            LocationTrigger yard = P.Trigger<LocationTrigger>(m, "Loc_container_yard", origin, Box(DockYard, 3f));
            yard.Configure("container_yard", "하역장 · Loading dock", false);
            P.Icon(yard.gameObject, "container_yard", MapIconType.Objective, "하역장 · Loading dock", "container_yard", "mission_industrial_02");
            P.Icon(P.Terminal(m, "manifest_terminal", new Vector3(-30f, Top + 1.1f, -30.3f), Quaternion.Euler(0f, 180f, 0f),
                    "적하 목록 조회 · Read the shipping manifest", "Copy the counterfeit shipment list - takes 3 seconds.", 3f),
                "manifest_terminal", MapIconType.Objective, "적하 목록 단말기 · Manifest terminal", "manifest_terminal", "mission_industrial_02");
            Object.DestroyImmediate(origin.gameObject);

            // ---- people -----------------------------------------------------------------------------
            Transform security = EnvKit.Group(population, "Security");
            Transform[] Route(params string[] names) => names.Select(n => GameObject.Find("POI_" + n).transform).ToArray();
            void Guard(string id, Vector3 at, float yaw, bool female, string group, Transform[] route)
            {
                NpcController npc = P.PlaceNpc(security, civilianPrefab, id, at, yaw, P.NpcLayer);
                npc.Configure(id, NpcType.Guard, NpcArchetype.Security, P.Uniform(female), P.baseMaterial, group, route);
                P.Icon(npc.gameObject, id, MapIconType.Guard, "경비원 · Security guard");
                PrefabUtility.RecordPrefabInstancePropertyModifications(npc);
            }

            Guard("guard_stockroom_01", new Vector3(28.5f, Top, -36f), -90f, false, "stockroom", Route("Post_StockroomShelves", "Post_StockroomDoor"));
            Guard("guard_dock_01", new Vector3(-26f, Top, -26f), 90f, true, "dock", Route("Post_DockGate"));

            Transform people = EnvKit.Group(population, "MissionCharacters");
            MissionSubject Subject(string objectName, Vector3 position, float yaw, CharacterAppearance look, NpcType type)
            {
                NpcController npc = P.PlaceNpc(people, civilianPrefab, objectName, position, yaw, P.SubjectLayer);
                Object.DestroyImmediate(npc.GetComponent<CivilianInteractable>(), true);
                npc.Configure(objectName, type, NpcArchetype.Subject, look, P.baseMaterial, "", null);
                PrefabUtility.RecordPrefabInstancePropertyModifications(npc);
                return npc.gameObject.AddComponent<MissionSubject>();
            }

            MissionSubject captain = Subject("subject_captain_han", new Vector3(29.4f, Top, -40f), -90f, new CharacterAppearance
            {
                heightScale = 1.05f, shoulderWidth = 1.08f, skinTone = new Color(0.84f, 0.68f, 0.56f), hairStyle = HairStyle.ShortTextured, hairColor = Color.black,
                outfit = OutfitStyle.BusinessSuit, topColor = new Color(0.08f, 0.1f, 0.16f), bottomColor = new Color(0.08f, 0.1f, 0.16f),
                innerColor = Color.white, accentColor = new Color(0.8f, 0.7f, 0.2f), shoeColor = Color.black
            }, NpcType.MissionSubject);
            captain.Configure("subject_guard_captain", "guard_captain", "한대성", "Han Dae-sung", "Head of stockroom security. Keeps the formula case close.",
                "region_industrial", true, false, false, null, "여긴 관계자만 들어올 수 있어요.", "Staff only in here.");

            MissionSubject informant = Subject("subject_informant_kim", new Vector3(-31f, Top, -38f), 0f, new CharacterAppearance
            {
                heightScale = 0.96f, skinTone = new Color(0.9f, 0.76f, 0.64f), hairStyle = HairStyle.Long, hairColor = new Color(0.18f, 0.11f, 0.07f),
                outfit = OutfitStyle.Hoodie, topColor = new Color(0.85f, 0.85f, 0.82f), bottomColor = new Color(0.15f, 0.17f, 0.25f),
                innerColor = Color.white, accentColor = new Color(0.9f, 0.3f, 0.3f), shoeColor = Color.white, faceMask = true
            }, NpcType.MissionSubject);
            informant.Configure("subject_informant_kim", "informant_kim", "김하은", "Kim Ha-eun", "Dock clerk who saw the counterfeit shipments. Wants out quietly.",
                "region_industrial", true, false, true, null, "조용히 여기서 나가고 싶어요.", "I just want to get out of here quietly.");

            // The counterfeit smuggler from the contract dossier, checking his container at the dock.
            MissionSubject smuggler = Subject("subject_smuggler_choi", new Vector3(-33.6f, Top, -33f), -90f, new CharacterAppearance
            {
                heightScale = 1.02f, shoulderWidth = 1.04f, skinTone = new Color(0.8f, 0.64f, 0.5f), hairStyle = HairStyle.ShortTextured, hairColor = new Color(0.1f, 0.08f, 0.06f),
                outfit = OutfitStyle.Hoodie, topColor = new Color(0.12f, 0.12f, 0.12f), bottomColor = new Color(0.2f, 0.2f, 0.22f),
                innerColor = new Color(0.3f, 0.3f, 0.3f), accentColor = new Color(0.9f, 0.75f, 0.2f), shoeColor = Color.black
            }, NpcType.MissionSubject);
            smuggler.Configure("subject_smuggler_choi", "smuggler_choi", "최민호", "Choi Min-ho", "Moves counterfeit cosmetics through the dock behind the shops.",
                "region_industrial", true, false, false, null, "뭘 봐요? 갈 길 가세요.", "What are you looking at? Move along.");

            foreach ((MissionSubject s, string mission) in new[] { (captain, "mission_industrial_01"), (informant, "mission_industrial_02"), (smuggler, "mission_industrial_02") })
            {
                P.Icon(s.gameObject, s.SubjectId, MapIconType.MissionSubject, "미확인 인물 · Unidentified person", null, mission);
                s.gameObject.layer = P.SubjectLayer;
                PrefabUtility.RecordPrefabInstancePropertyModifications(s);
            }
        }

        // =====================================================================================
        // Map
        // =====================================================================================

        private static void BuildMap()
        {
            GangnamMapBuilder.PrepareShapes();
            Transform root = new GameObject("Myeongdong_Map").transform;
            Transform g = EnvKit.Group(root, "Geometry");
            GangnamMapBuilder.Shape(g, "Roads", Rect.MinMaxRect(-80f, -75f, 94f, 82f), GangnamMapBuilder.Road, 0);
            GangnamMapBuilder.Shape(g, "Pedestrian", Rect.MinMaxRect(-58f, -50f, 48f, 82f), GangnamMapBuilder.Alley, 1);
            GangnamMapBuilder.Shape(g, "Plaza", Rect.MinMaxRect(48f, -50f, 94f, 82f), new Color(0.16f, 0.17f, 0.2f), 1);
            for (float x = -3.5f; x <= 3.6f; x += 1.4f) GangnamMapBuilder.Shape(g, "Zebra", new Rect(x - 0.35f, -73f, 0.7f, 22f), GangnamMapBuilder.Marking, 2);
            var missionBuildings = new HashSet<string> { "Stockroom" };
            foreach (Transform building in GameObject.Find(RootName + "/Buildings").transform.Cast<Transform>()
                         .Concat(GameObject.Find(RootName + "/Interiors").transform.Cast<Transform>())
                         .Append(GameObject.Find(RootName + "/Cathedral").transform))
            {
                Bounds? footprint = GangnamMapBuilder.Footprint(building);
                if (footprint == null) continue;
                Bounds b = footprint.Value;
                Rect r = Rect.MinMaxRect(b.min.x, b.min.z, b.max.x, b.max.z);
                bool mission = missionBuildings.Contains(building.name);
                GangnamMapBuilder.Shape(g, building.name + "_Edge", GangnamMapBuilder.Inflate(r, 0.6f), mission ? GangnamMapBuilder.MissionEdge : GangnamMapBuilder.BuildingEdge, 4);
                GangnamMapBuilder.Shape(g, building.name, r, mission ? GangnamMapBuilder.MissionFill : GangnamMapBuilder.BuildingFill, 5);
            }

            Transform l = EnvKit.Group(root, "Labels");
            GangnamMapBuilder.Label(l, "명동", "MYEONGDONG", new Vector3(-30f, 0f, 60f), 0, 0f, 3.2f);
            GangnamMapBuilder.Label(l, "명동8길", "Myeongdong 8-gil", new Vector3(0f, 0f, 50f), 0, 90f, 1.1f);
            GangnamMapBuilder.Label(l, "명동길", "Myeongdong-gil", new Vector3(-30f, 0f, 18f), 0, 0f, 1.1f);
            GangnamMapBuilder.Label(l, "을지로", "Eulji-ro", new Vector3(40f, 0f, -62f), 0, 0f, 1.1f);
            GangnamMapBuilder.Label(l, "남대문로", "Namdaemun-ro", new Vector3(-69f, 0f, 20f), 0, 90f, 1.1f);
            GangnamMapBuilder.Label(l, "명동 성당", "Cathedral", new Vector3(72f, 0f, 30f), 1, 0f, 1.2f);
            Transform places = EnvKit.Group(root, "Places");
            void Place(string id, string label, Vector3 position)
            {
                GameObject go = new GameObject("Place_" + id);
                go.transform.SetParent(places, false);
                go.transform.position = position;
                go.AddComponent<MapIcon>().Configure(id, MapIconType.PointOfInterest, label);
            }

            Place("subway_myeongdong", "명동역 4호선 · Myeongdong Station", new Vector3(14f, Top, -48f));
            Place("cathedral", "명동 성당 · Cathedral", new Vector3(58f, Top, 18f));
            Place("street_food", "명동 먹거리 · Street food", new Vector3(0f, Top, -20f));

            var manager = Object.FindAnyObjectByType<MapManager>();
            var so = new SerializedObject(manager);
            so.FindProperty("worldBounds").rectValue = Rect.MinMaxRect(-80f, -80f, 96f, 96f);
            so.FindProperty("regionKorean").stringValue = "명동";
            so.FindProperty("regionEnglish").stringValue = "MYEONGDONG";
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // =====================================================================================
        // Data + build settings
        // =====================================================================================

        private static void RegisterDistrict()
        {
            RegionData region = AssetDatabase.LoadAssetAtPath<RegionData>("Assets/ScriptableObjects/Regions/region_industrial.asset");
            var so = new SerializedObject(region);
            so.FindProperty("<sceneName>k__BackingField").stringValue = "Myeongdong";
            so.FindProperty("<districtBuilt>k__BackingField").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(region);
            EnsureInBuild(ScenePath);
            AssetDatabase.SaveAssets();
        }

        /// <summary>Adds the scene to the build, or refreshes its entry when the stored GUID is stale.</summary>
        public static void EnsureInBuild(string path)
        {
            GUID guid = AssetDatabase.GUIDFromAssetPath(path);
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
            int index = scenes.FindIndex(s => s.path == path);
            if (index >= 0 && scenes[index].guid == guid) return;
            if (index >= 0) scenes[index] = new EditorBuildSettingsScene(guid, true);
            else scenes.Add(new EditorBuildSettingsScene(guid, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        /// <summary>Refreshes the build-settings entries of the generated scenes (GUIDs).</summary>
        [MenuItem("CONTRACT/Setup/Refresh Build Scene List")]
        public static void RefreshBuildScenes()
        {
            EnsureInBuild(ScenePath);
            EnsureInBuild("Assets/Scenes/WorldSelection.unity");
            // Rewrite every entry from its path so the stored GUIDs match the scenes' .meta files.
            EditorBuildSettings.scenes = EditorBuildSettings.scenes
                .Select(s => new EditorBuildSettingsScene(AssetDatabase.GUIDFromAssetPath(s.path), s.enabled)).ToArray();
            AssetDatabase.SaveAssets();
        }
    }
}
