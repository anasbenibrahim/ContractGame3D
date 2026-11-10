using System.Collections.Generic;
using System.IO;
using System.Linq;
using Contract.Data;
using Contract.Interaction;
using Contract.Map;
using Contract.NPCs;
using Contract.Player;
using TMPro;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Contract.Editor.World
{
    /// <summary>
    /// Stage 4: NavMesh, crowd points of interest, guards, mission characters and mission
    /// interactables for Gangnam. Run after GangnamDistrictBuilder (it reads the built towers).
    /// Idempotent: replaces Gangnam_Population / Gangnam_MissionContent / NavMesh_Gangnam.
    /// Headless: -executeMethod Contract.Editor.World.GangnamPopulationBuilder.Build
    /// </summary>
    public static class GangnamPopulationBuilder
    {
        private const string ScenePath = "Assets/Scenes/GameWorld.unity";
        private const string NavMeshAssetPath = "Assets/Scenes/GameWorld/NavMesh_Gangnam.asset";
        private const string CivilianPrefabPath = "Assets/Prefabs/NPCs/Civilian.prefab";
        private const string PlayerPrefabPath = "Assets/Prefabs/Player/Player.prefab";
        internal const string BaseMaterialPath = "Assets/Art/Materials/Player_Suit.mat";

        public const string PopulationRoot = "Gangnam_Population";
        public const string MissionRoot = "Gangnam_MissionContent";
        public const string NavMeshRoot = "NavMesh_Gangnam";

        internal const int PlayerLayer = 8, NpcLayer = 9, InteractableLayer = 10, SubjectLayer = 11, IgnoreRaycastLayer = 2;
        private const float Top = GangnamDistrictBuilder.SidewalkTop;

        internal static EnvKit kit;
        internal static Material baseMaterial;

        [MenuItem("CONTRACT/World/Build Gangnam Population (NavMesh, NPCs, Missions)")]
        public static void Build()
        {
            kit = new EnvKit();
            baseMaterial = AssetDatabase.LoadAssetAtPath<Material>(BaseMaterialPath);
            GameObject civilianPrefab = BuildCivilianPrefab();
            ConfigurePlayerPrefab();

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (string name in new[] { PopulationRoot, MissionRoot, NavMeshRoot })
            {
                GameObject old = GameObject.Find(name);
                if (old != null) Object.DestroyImmediate(old);
            }

            RemoveObsoleteMarkers();
            Transform population = new GameObject(PopulationRoot).transform;
            Transform missions = new GameObject(MissionRoot).transform;

            Transform mirae = GameObject.Find(GangnamLayout.MiraeTower).transform;
            Transform hangyeol = GameObject.Find(GangnamLayout.HangyeolTower).transform;

            BuildPointsOfInterest(EnvKit.Group(population, "PointsOfInterest"), mirae, hangyeol);
            BuildMissionContent(missions, mirae, hangyeol);
            BuildGuards(EnvKit.Group(population, "Security"), civilianPrefab, mirae, hangyeol);
            BuildMissionCharacters(EnvKit.Group(population, "MissionCharacters"), civilianPrefab, mirae, hangyeol);
            ConfigureNpcManager(civilianPrefab);
            ConfigureExtraction();
            BakeNavMesh();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[GangnamPopulationBuilder] Done: {NpcPointOfInterestCount()} points of interest, " +
                      $"{population.GetComponentsInChildren<NpcController>(true).Length} placed NPCs, navmesh at {NavMeshAssetPath}.");
        }

        private static int NpcPointOfInterestCount() => Object.FindObjectsByType<NpcPointOfInterest>(FindObjectsSortMode.None).Length;

        // =====================================================================================
        // Prefabs
        // =====================================================================================

        internal static GameObject BuildCivilianPrefab()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CivilianPrefabPath));
            GameObject root = new GameObject("Civilian") { layer = NpcLayer };
            NavMeshAgent agent = root.AddComponent<NavMeshAgent>();
            agent.radius = 0.3f;
            agent.height = 1.8f;
            agent.speed = 1.4f;
            // Enabled by NpcController.Start, once the scene's NavMesh is registered: an agent enabled
            // during the scene load can come up before it ("no valid NavMesh", seen on the Galaxy A22).
            agent.enabled = false;
            CapsuleCollider body = root.AddComponent<CapsuleCollider>();
            body.radius = 0.3f;
            body.height = 1.8f;
            body.center = new Vector3(0f, 0.9f, 0f);
            Rigidbody rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true; // moved by the agent; kinematic so the physics engine treats it as a moving body
            rb.interpolation = RigidbodyInterpolation.None;
            root.AddComponent<NpcLocomotion>();
            root.AddComponent<NpcSpeech>();
            root.AddComponent<NpcController>();
            root.AddComponent<CivilianInteractable>();
            root.AddComponent<MapIcon>().Configure("civilian", MapIconType.NPC, "시민 · Civilian"); // live crowd dot on the maps
            new GameObject("Visual").transform.SetParent(root.transform, false);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, CivilianPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void ConfigurePlayerPrefab()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                if (root.GetComponent<PlayerGestureDriver>() == null) root.AddComponent<PlayerGestureDriver>();
                NavMeshObstacle obstacle = root.GetComponent<NavMeshObstacle>();
                if (obstacle == null) obstacle = root.AddComponent<NavMeshObstacle>(); // not '??': Unity fake-null
                obstacle.shape = NavMeshObstacleShape.Capsule;
                obstacle.center = new Vector3(0f, 0.9f, 0f);
                obstacle.radius = 0.4f;
                obstacle.height = 1.8f;
                obstacle.carving = false; // only used for crowd avoidance: NPCs steer around the player

                // The prompt scan only looked at the Interactable layer, so NPCs / mission characters
                // could never be talked to. Scan exactly the layers that carry interactions.
                SerializedObject interaction = new SerializedObject(root.GetComponent<InteractionController>());
                interaction.FindProperty("interactionMask").FindPropertyRelative("m_Bits").intValue =
                    (1 << NpcLayer) | (1 << InteractableLayer) | (1 << SubjectLayer);
                interaction.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // =====================================================================================
        // Points of interest - derived from the built district so they always match it
        // =====================================================================================

        internal static void Poi(Transform parent, string name, Vector3 position, Vector3 facing, PoiCategory category, Vector2 dwell, float radius = 1.2f, string group = "")
        {
            GameObject go = new GameObject("POI_" + name);
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            facing.y = 0f;
            if (facing.sqrMagnitude > 0.001f) go.transform.rotation = Quaternion.LookRotation(facing);
            go.AddComponent<NpcPointOfInterest>().Configure(category, dwell, radius, group);
        }

        private static void BuildPointsOfInterest(Transform p, Transform mirae, Transform hangyeol)
        {
            // Shopfronts: every commercial building's ground floor, 1.4 m in front, facing it.
            foreach (Transform building in GameObject.Find("District_Gangnam/Buildings").transform.Cast<Transform>()
                         .Concat(GameObject.Find("District_Gangnam/FoodAlley").transform.Cast<Transform>()))
            {
                BoxCollider ground = building.Find("GroundFloor")?.GetComponent<BoxCollider>();
                if (ground == null) continue;
                Vector3 front = building.TransformPoint(new Vector3(0f, 0f, ground.size.z / 2f + 1.4f));
                bool food = building.parent.name == "FoodAlley";
                Poi(p, building.name, front + Vector3.up * 0.05f, -building.forward, food ? PoiCategory.Food : PoiCategory.Shop, new Vector2(5f, 14f), 1.5f);
            }

            // Tower entrances.
            foreach (Transform tower in new[] { mirae, hangyeol })
            {
                Poi(p, tower.name + "_Entrance", tower.TransformPoint(new Vector3(0f, 0f, 13.5f)), -tower.forward, PoiCategory.Lobby, new Vector2(3f, 8f), 2f);
                Poi(p, tower.name + "_Lobby", tower.TransformPoint(new Vector3(-6f, 0f, 6f)), -tower.forward, PoiCategory.Lobby, new Vector2(6f, 15f), 1.5f);
            }

            // 포장마차 tables and rear shops in the alley.
            foreach (Transform tent in GameObject.Find("District_Gangnam/FoodAlley").transform.Cast<Transform>().Where(t => t.name.StartsWith("Pojangmacha")))
            {
                Poi(p, "Pocha", tent.TransformPoint(new Vector3(-1f, 0f, 3.4f)), -tent.forward, PoiCategory.Food, new Vector2(10f, 25f), 0.8f);
                Poi(p, "Pocha", tent.TransformPoint(new Vector3(1f, 0f, 3.4f)), -tent.forward, PoiCategory.Food, new Vector2(10f, 25f), 0.8f);
            }

            // Street furniture.
            foreach (Transform t in GameObject.Find("District_Gangnam").GetComponentsInChildren<Transform>())
            {
                if (t.name.StartsWith("BusStop")) Poi(p, "BusStop", t.TransformPoint(new Vector3(0.5f, 0f, 0.4f)), -t.forward, PoiCategory.Transit, new Vector2(8f, 20f), 1.8f);
                else if (t.name.StartsWith("Bench") && t.parent.name != "Interior") Poi(p, "Bench", t.TransformPoint(new Vector3(0f, 0f, 0.9f)), -t.forward, PoiCategory.Bench, new Vector2(8f, 20f), 0.6f);
            }

            Poi(p, "SubwayExit10", new Vector3(22f, Top, 10.5f), Vector3.right, PoiCategory.Transit, new Vector2(4f, 10f), 1.5f);

            // Crossings: wait at the kerb facing across.
            foreach ((Vector3 pos, Vector3 dir) in new[]
                     {
                         (new Vector3(9.4f, Top, 10.5f), Vector3.left), (new Vector3(-9.4f, Top, 10.5f), Vector3.right),
                         (new Vector3(9.4f, Top, -10.5f), Vector3.left), (new Vector3(-9.4f, Top, -10.5f), Vector3.right),
                         (new Vector3(10.5f, Top, 8.4f), Vector3.back), (new Vector3(-10.5f, Top, 8.4f), Vector3.back),
                         (new Vector3(10.5f, Top, -8.4f), Vector3.forward), (new Vector3(-10.5f, Top, -8.4f), Vector3.forward)
                     })
            {
                Poi(p, "Crossing", pos, dir, PoiCategory.Crossing, new Vector2(2f, 6f), 1f);
            }

            // Landmarks tourists stop to photograph.
            Poi(p, "View_Intersection", new Vector3(12f, Top, -12f), new Vector3(-1f, 0f, 1f), PoiCategory.Viewpoint, new Vector2(5f, 10f), 1.5f);
            Poi(p, "View_AlleyGate", new Vector3(11.5f, Top, 42f), Vector3.right, PoiCategory.Viewpoint, new Vector2(5f, 10f), 1.5f);
            Poi(p, "View_Hotel", new Vector3(-11.5f, Top, 48f), Vector3.left, PoiCategory.Viewpoint, new Vector2(5f, 10f), 1.5f);
            Poi(p, "View_MiraeTower", new Vector3(11.5f, Top, 25f), Vector3.right, PoiCategory.Viewpoint, new Vector2(5f, 10f), 1.5f);
            Poi(p, "View_Alley", new Vector3(60f, Top, 42f), Vector3.right, PoiCategory.Viewpoint, new Vector2(5f, 10f), 2f);

            // Security posts.
            Poi(p, "Post_MiraeReception", mirae.TransformPoint(new Vector3(0f, 0f, 2.4f)), mirae.forward, PoiCategory.Patrol, new Vector2(6f, 12f), 0.4f, "mirae");
            Poi(p, "Post_MiraeGates", mirae.TransformPoint(new Vector3(5.5f, 0f, -1f)), mirae.forward, PoiCategory.Patrol, new Vector2(6f, 12f), 0.4f, "mirae");
            Poi(p, "Post_MiraeOffice", mirae.TransformPoint(new Vector3(-1f, 0f, -6.5f)), -mirae.right, PoiCategory.Patrol, new Vector2(5f, 9f), 0.4f, "mirae");
            Poi(p, "Post_HangyeolDesk", hangyeol.TransformPoint(new Vector3(1.5f, 0f, 3.8f)), hangyeol.forward, PoiCategory.Patrol, new Vector2(6f, 12f), 0.4f, "hangyeol");
            Poi(p, "Post_HangyeolServers", hangyeol.TransformPoint(new Vector3(6.5f, 0f, -5.8f)), -hangyeol.right, PoiCategory.Patrol, new Vector2(5f, 10f), 0.4f, "hangyeol");
        }

        // =====================================================================================
        // Mission content (ids match the Gangnam mission data)
        // =====================================================================================

        private static void BuildMissionContent(Transform m, Transform mirae, Transform hangyeol)
        {
            // mission_downtown_01 "The Missing Documents"
            LocationTrigger office = Trigger<LocationTrigger>(m, "Loc_office_building", mirae, GangnamLayout.MiraeLobbyVolume);
            office.Configure("office_building", "미래 타워 사무실 · Mirae Tower office", false);
            Icon(office.gameObject, "office_building", MapIconType.Objective, "미래 타워 사무실 · Corporate office", "office_building", "mission_downtown_01");
            Trigger<RestrictedZone>(m, "Restricted_MiraeOffice", mirae, GangnamLayout.MiraeOfficeVolume).Configure("mirae");
            Icon(Terminal(m, "secure_terminal", mirae.TransformPoint(GangnamLayout.MiraeSecureTerminal), mirae.rotation,
                    "보안 단말기 접속 · Access secure terminal", "Copy the shipment records before they are wiped.", 3f),
                "secure_terminal", MapIconType.Objective, "보안 단말기 · Secure terminal", "secure_terminal", "mission_downtown_01");
            Icon(Pickup(m, "documents_evidence", "기밀 문서 · CLASSIFIED DOCUMENTS", "The paper trail the client is paying for.",
                    mirae.TransformPoint(GangnamLayout.MiraeDocuments), mirae.rotation, new Vector3(0.32f, 0.04f, 0.24f), "SignYellow"),
                "documents_evidence", MapIconType.Objective, "기밀 문서 · Classified documents", "documents_evidence", "mission_downtown_01");

            // mission_downtown_03 "Corporate Espionage"
            LocationTrigger servers = Trigger<LocationTrigger>(m, "Loc_server_room", hangyeol, GangnamLayout.HangyeolServerVolume);
            servers.Configure("server_room", "한결 그룹 서버실 · Hangyeol server room", false);
            Icon(servers.gameObject, "server_room", MapIconType.Objective, "한결 그룹 서버실 · Server room", "server_room", "mission_downtown_03");
            Trigger<RestrictedZone>(m, "Restricted_HangyeolServers", hangyeol, GangnamLayout.HangyeolServerVolume).Configure("hangyeol");
            Icon(Terminal(m, "server_rack", hangyeol.TransformPoint(GangnamLayout.HangyeolServerRack), hangyeol.rotation,
                    "감시 소프트웨어 설치 · Plant surveillance software", "Fictional monitoring tool - takes 4 seconds.", 4f),
                "server_rack", MapIconType.Objective, "서버 랙 · Server rack", "server_rack", "mission_downtown_03");

            // mission_downtown_02 "Silent Exchange": the data drive is a dead drop in the bin by the bus stop.
            Icon(Pickup(m, "data_drive", "데이터 드라이브 · DATA DRIVE", "Left in the bin after the handoff.",
                    new Vector3(-13.4f, Top + 1.05f, 60f), Quaternion.Euler(0f, 90f, 0f), new Vector3(0.12f, 0.04f, 0.3f), "SignBlue"),
                "data_drive", MapIconType.Objective, "데이터 드라이브 · Data drive", "data_drive", "mission_downtown_02");
        }

        /// <summary>Map marker for an object (minimap + full map read the same registry).</summary>
        internal static MapIcon Icon(GameObject go, string id, MapIconType type, string label, string track = null, string mission = null)
        {
            MapIcon icon = go.GetComponent<MapIcon>();
            if (icon == null) icon = go.AddComponent<MapIcon>();
            bool important = type == MapIconType.Objective || type == MapIconType.MissionSubject;
            icon.Configure(id, type, label, track, mission, clampToEdge: important);

            // Mission objects only work while their contract is active (no soft-locks before accepting it).
            if (!string.IsNullOrEmpty(mission))
            {
                TerminalInteractable terminal = go.GetComponent<TerminalInteractable>();
                if (terminal != null) terminal.SetMission(mission);
                PickupInteractable pickup = go.GetComponent<PickupInteractable>();
                if (pickup != null) pickup.SetMission(mission);
            }

            return icon;
        }

        internal static T Trigger<T>(Transform parent, string name, Transform owner, Bounds local) where T : Component
        {
            GameObject go = new GameObject(name) { layer = IgnoreRaycastLayer }; // never part of the navmesh
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(owner.TransformPoint(local.center), owner.rotation);
            BoxCollider box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = local.size;
            return go.AddComponent<T>();
        }

        internal static GameObject Terminal(Transform parent, string id, Vector3 position, Quaternion rotation, string label, string description, float seconds)
        {
            GameObject go = new GameObject("Terminal_" + id) { layer = InteractableLayer };
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, rotation);
            BoxCollider box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(0.8f, 0.6f, 0.3f);
            box.isTrigger = true;
            TextMeshPro screen = kit.Text(go.transform, "Screen", "", new Vector3(0f, 0.05f, 0.05f), new Vector2(0.6f, 0.35f),
                new Color(0.55f, 0.85f, 1f), kit.NeonTextMaterial);
            go.AddComponent<TerminalInteractable>().Configure(id, label, description, seconds, screen);
            return go;
        }

        internal static GameObject Pickup(Transform parent, string itemId, string label, string description, Vector3 position, Quaternion rotation, Vector3 size, string material)
        {
            GameObject go = new GameObject("Pickup_" + itemId) { layer = InteractableLayer };
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, rotation);
            kit.Block(go.transform, "Visual", Vector3.zero, size, material, collider: false);
            BoxCollider box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(0f, 0.15f, 0f);
            box.size = new Vector3(0.6f, 0.4f, 0.6f);
            PickupInteractable pickup = go.AddComponent<PickupInteractable>();
            SerializedObject so = new SerializedObject(pickup);
            so.FindProperty("itemId").stringValue = itemId;
            so.FindProperty("displayName").stringValue = label;
            so.FindProperty("description").stringValue = description;
            so.ApplyModifiedPropertiesWithoutUndo();
            return go;
        }

        // =====================================================================================
        // NPCs placed in the scene
        // =====================================================================================

        internal static NpcController PlaceNpc(Transform parent, GameObject prefab, string id, Vector3 position, float yaw, int layer)
        {
            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = id;
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            go.layer = layer;
            return go.GetComponent<NpcController>();
        }

        private static Transform[] Route(params string[] poiNames) =>
            poiNames.Select(n => GameObject.Find("POI_" + n).transform).ToArray();

        internal static CharacterAppearance Uniform(bool female) => new CharacterAppearance
        {
            heightScale = female ? 0.95f : 1.02f,
            shoulderWidth = female ? 0.9f : 1.02f,
            skinTone = new Color(0.86f, 0.7f, 0.58f),
            hairStyle = female ? HairStyle.Bob : HairStyle.ShortTextured,
            hairColor = new Color(0.05f, 0.04f, 0.04f),
            outfit = OutfitStyle.BusinessSuit,
            topColor = new Color(0.07f, 0.09f, 0.16f),
            bottomColor = new Color(0.07f, 0.09f, 0.16f),
            innerColor = new Color(0.85f, 0.86f, 0.88f),
            accentColor = new Color(0.12f, 0.14f, 0.25f),
            shoeColor = new Color(0.05f, 0.05f, 0.05f)
        };

        private static void BuildGuards(Transform parent, GameObject prefab, Transform mirae, Transform hangyeol)
        {
            void Guard(string id, Transform near, Vector3 local, bool female, string group, Transform[] route)
            {
                NpcController npc = PlaceNpc(parent, prefab, id, near.TransformPoint(local), near.eulerAngles.y, NpcLayer);
                npc.Configure(id, NpcType.Guard, NpcArchetype.Security, Uniform(female), baseMaterial, group, route);
                Icon(npc.gameObject, id, MapIconType.Guard, "경비원 · Security guard");
                PrefabUtility.RecordPrefabInstancePropertyModifications(npc);
            }

            Guard("guard_mirae_01", mirae, new Vector3(0f, 0f, 2.4f), false, "mirae", Route("Post_MiraeReception", "Post_MiraeGates"));
            Guard("guard_mirae_02", mirae, new Vector3(-1f, 0f, -6.5f), true, "mirae", Route("Post_MiraeOffice", "Post_MiraeGates"));
            Guard("guard_hangyeol_01", hangyeol, new Vector3(1.5f, 0f, 3.8f), false, "hangyeol", Route("Post_HangyeolDesk", "Post_HangyeolServers"));
        }

        private static void BuildMissionCharacters(Transform parent, GameObject prefab, Transform mirae, Transform hangyeol)
        {
            // Private route points for the courier handoff (not part of the public crowd pool).
            Transform routes = EnvKit.Group(parent, "Routes");
            Transform Point(string name, Vector3 pos, Vector3 facing, Vector2 dwell)
            {
                GameObject go = new GameObject("Route_" + name);
                go.transform.SetParent(routes, false);
                go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(facing));
                return go.transform;
            }

            Transform meet = Point("Handoff", new Vector3(-10.6f, Top, 35.2f), Vector3.forward, Vector2.zero);
            Transform wait = Point("CourierWait", new Vector3(-11.2f, Top, 62f), Vector3.right, Vector2.zero);
            Transform contactSpot = Point("ContactSpot", new Vector3(-10.4f, Top, 36.6f), Vector3.back, Vector2.zero);

            MissionSubject Subject(string objectName, Vector3 position, float yaw, CharacterAppearance look, Transform[] route, NpcType type)
            {
                NpcController npc = PlaceNpc(parent, prefab, objectName, position, yaw, SubjectLayer);
                Object.DestroyImmediate(npc.GetComponent<CivilianInteractable>(), true);
                npc.Configure(objectName, type, NpcArchetype.Subject, look, baseMaterial, "", route);
                PrefabUtility.RecordPrefabInstancePropertyModifications(npc);
                return npc.gameObject.AddComponent<MissionSubject>();
            }

            // CEO Kim Min-jun - mission_downtown_01's subject, works in the Mirae Tower lobby.
            MissionSubject kim = Subject("subject_ceo_kim", mirae.TransformPoint(new Vector3(-6f, 0f, 3f)), mirae.eulerAngles.y + 180f,
                new CharacterAppearance
                {
                    heightScale = 1f, shoulderWidth = 1f, skinTone = new Color(0.9f, 0.76f, 0.64f), hairStyle = HairStyle.SlickedBack,
                    hairColor = new Color(0.12f, 0.11f, 0.11f), outfit = OutfitStyle.BusinessSuit, topColor = new Color(0.12f, 0.12f, 0.13f),
                    bottomColor = new Color(0.12f, 0.12f, 0.13f), innerColor = Color.white, accentColor = new Color(0.1f, 0.2f, 0.45f),
                    shoeColor = new Color(0.08f, 0.05f, 0.03f), glasses = true
                },
                Route("MiraeTower_Lobby", "Post_MiraeGates"), NpcType.MissionSubject);
            kim.Configure("subject_ceo_kim", "ceo_kim", "김민준", "Kim Min-jun", "CEO of Mirae Holdings. Knows the documents are missing.",
                "region_downtown", true, false, false, null, "회의에 늦었어요. 실례합니다.", "I'm late for a meeting. Excuse me.");

            // The courier's contact waits at the bus stop; Courier Park walks over for the handoff.
            MissionSubject kang = Subject("subject_courier_kang", contactSpot.position, 180f,
                new CharacterAppearance
                {
                    heightScale = 1.03f, skinTone = new Color(0.82f, 0.66f, 0.54f), hairStyle = HairStyle.Undercut, hairColor = Color.black,
                    outfit = OutfitStyle.BomberJacket, topColor = new Color(0.2f, 0.25f, 0.18f), bottomColor = new Color(0.1f, 0.1f, 0.1f),
                    innerColor = new Color(0.3f, 0.3f, 0.3f), accentColor = new Color(0.6f, 0.5f, 0.2f), shoeColor = Color.black, faceMask = true
                },
                new[] { contactSpot }, NpcType.Target);
            kang.Configure("subject_courier_kang", "courier_kang", "강도윤", "Kang Do-yun", "The buyer's go-between. Waits at the bus stop.",
                "region_downtown", true, false, false, null, "버스 기다리는 중이에요.", "Just waiting for the bus.");

            MissionSubject park = Subject("subject_courier_park", wait.position, 180f,
                new CharacterAppearance
                {
                    heightScale = 0.94f, shoulderWidth = 0.88f, skinTone = new Color(0.92f, 0.78f, 0.66f), hairStyle = HairStyle.Bob,
                    hairColor = new Color(0.15f, 0.1f, 0.07f), outfit = OutfitStyle.Hoodie, topColor = new Color(0.55f, 0.12f, 0.15f),
                    bottomColor = new Color(0.08f, 0.08f, 0.1f), innerColor = Color.white, accentColor = Color.white,
                    shoeColor = Color.white, crossBodyBag = true
                },
                new[] { meet, wait }, NpcType.MissionSubject);
            park.GetComponent<NpcController>().SetRouteDwell(new Vector2(12f, 18f)); // long enough handoff to photograph
            park.Configure("subject_courier_park", "courier_park", "박수민", "Park Soo-min", "Courier carrying a data drive. Red hoodie, cross-body bag.",
                "region_downtown", true, true, false, kang.GetComponent<NpcController>(), "배달 중이라 바빠요.", "Busy with a delivery, sorry.");

            // CTO Lee Jin-woo - mission_downtown_03's subject, Hangyeol Group lobby.
            MissionSubject lee = Subject("subject_cto_lee", hangyeol.TransformPoint(new Vector3(-5f, 0f, 6.5f)), hangyeol.eulerAngles.y,
                new CharacterAppearance
                {
                    heightScale = 0.98f, skinTone = new Color(0.86f, 0.71f, 0.58f), hairStyle = HairStyle.ShortTextured, hairColor = Color.black,
                    outfit = OutfitStyle.Hoodie, topColor = new Color(0.3f, 0.32f, 0.36f), bottomColor = new Color(0.12f, 0.14f, 0.2f),
                    innerColor = new Color(0.7f, 0.7f, 0.7f), accentColor = Color.white, shoeColor = Color.white, glasses = true
                },
                Route("HangyeolGroup_Lobby", "Post_HangyeolDesk"), NpcType.MissionSubject);
            lee.Configure("subject_cto_lee", "cto_lee", "이진우", "Lee Jin-woo", "Hangyeol Group CTO. Guards the server room project.",
                "region_downtown", true, false, false, null, "보안 점검 중입니다.", "We're running a security check.");

            foreach ((MissionSubject s, string mission) in new[] { (kim, "mission_downtown_01"), (kang, "mission_downtown_02"), (park, "mission_downtown_02"), (lee, "mission_downtown_03") })
            {
                // Unidentified until the player identifies them; then the icon shows the real name.
                Icon(s.gameObject, s.SubjectId, MapIconType.MissionSubject, "미확인 인물 · Unidentified person", null, mission);
                s.gameObject.layer = SubjectLayer;
                PrefabUtility.RecordPrefabInstancePropertyModifications(s);
            }
        }

        // =====================================================================================
        // Scene services / cleanup
        // =====================================================================================

        internal static void ConfigureNpcManager(GameObject civilianPrefab)
        {
            NpcManager manager = Object.FindAnyObjectByType<NpcManager>();
            if (manager == null) manager = new GameObject("[NpcManager]").AddComponent<NpcManager>();
            SerializedObject so = new SerializedObject(manager);
            so.FindProperty("civilianNpcPrefab").objectReferenceValue = civilianPrefab;
            so.FindProperty("npcBaseMaterial").objectReferenceValue = baseMaterial;
            so.FindProperty("maxCivilians").intValue = 32;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureExtraction()
        {
            ExtractionInteractable extraction = Object.FindAnyObjectByType<ExtractionInteractable>();
            if (extraction == null) return;
            SerializedObject so = new SerializedObject(extraction);
            so.FindProperty("extractionId").stringValue = "extraction_downtown"; // id the Gangnam missions expect
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void RemoveObsoleteMarkers()
        {
            GameObject markers = GameObject.Find("WorldMarkers");
            if (markers != null)
            {
                foreach (Transform t in markers.transform.Cast<Transform>().ToList())
                {
                    if (t.name.StartsWith("NPC_Spawn_") || t.name.StartsWith("PatrolPoint_") || t.name.StartsWith("MissionTarget_"))
                        Object.DestroyImmediate(t.gameObject); // superseded by points of interest / mission characters
                }
            }

            GameObject evidence = GameObject.Find("EvidencePickup");
            if (evidence != null) evidence.transform.position = new Vector3(-12.8f, Top + 0.2f, 52f); // was in the middle of the boulevard
        }

        // =====================================================================================
        // NavMesh: pavements, plazas, interiors, stairs and ramp; roads only at the zebra crossings
        // =====================================================================================

        private static void BakeNavMesh() => BakeNavMesh(NavMeshRoot, NavMeshAssetPath, RoadCellsWithoutCrossings());

        /// <summary>Bakes a district navmesh: walkable everywhere except the given road areas.</summary>
        internal static void BakeNavMesh(string rootName, string assetPath, IEnumerable<Rect> noWalk)
        {
            GameObject go = new GameObject(rootName);
            NavMeshSurface surface = go.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = ~((1 << PlayerLayer) | (1 << NpcLayer) | (1 << SubjectLayer) | (1 << IgnoreRaycastLayer) | (1 << InteractableLayer));
            surface.defaultArea = 0;

            int notWalkable = NavMesh.GetAreaFromName("Not Walkable");
            foreach (Rect road in noWalk)
            {
                GameObject volume = new GameObject("NoWalk_Road");
                volume.transform.SetParent(go.transform, false);
                volume.transform.position = new Vector3(road.center.x, -0.2f, road.center.y);
                NavMeshModifierVolume modifier = volume.AddComponent<NavMeshModifierVolume>();
                modifier.center = Vector3.zero; // default centre is (0,1,0) - would float above the road
                modifier.size = new Vector3(road.width, 0.6f, road.height); // y -0.5..0.1: below the 0.15 kerb
                modifier.area = notWalkable;
            }

            surface.BuildNavMesh();
            Directory.CreateDirectory(Path.GetDirectoryName(assetPath));
            AssetDatabase.DeleteAsset(assetPath);
            AssetDatabase.CreateAsset(surface.navMeshData, assetPath);
        }

        /// <summary>The road cross (everything outside the four blocks) minus the four zebra crossings.</summary>
        private static IEnumerable<Rect> RoadCellsWithoutCrossings()
        {
            Rect area = Rect.MinMaxRect(-90f, -75f, 90f, 100f);
            Rect[] blocks =
            {
                Rect.MinMaxRect(8f, 7f, 90f, 100f), Rect.MinMaxRect(-90f, 7f, -8f, 100f),
                Rect.MinMaxRect(8f, -75f, 90f, -7f), Rect.MinMaxRect(-90f, -75f, -8f, -7f)
            };
            Rect[] crossings =
            {
                Rect.MinMaxRect(-8f, 8.5f, 8f, 12.5f), Rect.MinMaxRect(-8f, -12.5f, 8f, -8.5f),
                Rect.MinMaxRect(8.5f, -7f, 12.5f, 7f), Rect.MinMaxRect(-12.5f, -7f, -8.5f, 7f)
            };
            Rect[] excluded = blocks.Concat(crossings).ToArray();

            List<float> xs = new List<float> { area.xMin, area.xMax }, zs = new List<float> { area.yMin, area.yMax };
            foreach (Rect r in excluded) { xs.Add(r.xMin); xs.Add(r.xMax); zs.Add(r.yMin); zs.Add(r.yMax); }
            xs = xs.Where(x => x >= area.xMin && x <= area.xMax).Distinct().OrderBy(v => v).ToList();
            zs = zs.Where(z => z >= area.yMin && z <= area.yMax).Distinct().OrderBy(v => v).ToList();

            for (int j = 0; j < zs.Count - 1; j++)
            {
                for (int i = 0; i < xs.Count - 1; i++)
                {
                    Vector2 c = new Vector2((xs[i] + xs[i + 1]) / 2f, (zs[j] + zs[j + 1]) / 2f);
                    if (excluded.Any(r => r.Contains(c))) continue;
                    yield return Rect.MinMaxRect(xs[i], zs[j], xs[i + 1], zs[j + 1]);
                }
            }
        }
    }
}
