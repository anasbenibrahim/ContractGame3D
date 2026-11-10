using System.Collections;
using System.Linq;
using Contract.Core;
using Contract.Data;
using Contract.Map;
using Contract.NPCs;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Contract.Tests
{
    /// <summary>
    /// Stage 5 verification: the pre-rendered map, the minimap and the full map in GameWorld, with
    /// input through the real pipeline (virtual gamepad: Select = map, Start = pause/back,
    /// North = interact/recenter, D-pad = zoom, left stick = pan).
    /// </summary>
    public sealed class MapTests
    {
        private Gamepad gamepad;
        private InputSettings.BackgroundBehavior savedBackground;
        private MapManager map;
        private MinimapView minimap;
        private FullMapView fullMap;
        private Transform player;

        [SetUp]
        public void Setup()
        {
            savedBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            gamepad = InputSystem.AddDevice<Gamepad>("MapTestGamepad");
        }

        [TearDown]
        public void TearDown()
        {
            if (GameManager.Instance != null)
            {
                if (GameManager.Instance.IsMapOpen) GameManager.Instance.CloseMap();
                GameManager.Instance.SetPaused(false);
                GameManager.Instance.Session.activeMissionId = string.Empty;
            }

            if (gamepad != null) InputSystem.RemoveDevice(gamepad);
            InputSystem.settings.backgroundBehavior = savedBackground;
        }

        private IEnumerator LoadWorld()
        {
            GameManager.Instance.SetPaused(false);
            SceneManager.LoadScene(GameConstants.Scenes.GameWorld, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;
            map = MapManager.Instance;
            minimap = Object.FindAnyObjectByType<MinimapView>();
            fullMap = Object.FindAnyObjectByType<FullMapView>();
            player = GameObject.FindGameObjectWithTag("Player").transform;
            Assert.IsNotNull(map, "MapManager missing");
            Assert.IsNotNull(minimap, "MinimapView missing");
            Assert.IsNotNull(fullMap, "FullMapView missing");
        }

        private IEnumerator Tap(GamepadButton button)
        {
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(button, true));
            yield return null;
            InputSystem.QueueStateEvent(gamepad, new GamepadState());
            yield return null;
            yield return null;
        }

        private IEnumerator Hold(GamepadState state, float seconds)
        {
            InputSystem.QueueStateEvent(gamepad, state);
            yield return new WaitForSecondsRealtime(seconds);
            InputSystem.QueueStateEvent(gamepad, new GamepadState());
            yield return null;
        }

        private Color MapPixel(Vector3 world)
        {
            Vector2 uv = map.WorldToUV(world);
            RenderTexture rt = map.MapTexture;
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(uv.x * rt.width, uv.y * rt.height, 1, 1), 0, 0);
            tex.Apply();
            RenderTexture.active = previous;
            Color c = tex.GetPixel(0, 0);
            Object.Destroy(tex);
            return c;
        }

        private static float Luma(Color c) => c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;

        // ---- static map ------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator MapTexture_IsRenderedFromTheDistrictLayout()
        {
            yield return LoadWorld();
            Assert.IsNotNull(map.MapTexture);
            Assert.GreaterOrEqual(map.MapTexture.width, 1024);

            Color road = MapPixel(new Vector3(-4f, 0f, -30f));
            Color block = MapPixel(new Vector3(-11f, 0f, -30f));
            Color building = MapPixel(new Vector3(-26f, 0f, -22.5f));       // SW_Comm_1 footprint
            Color missionBuilding = MapPixel(new Vector3(26f, 0f, 25f));     // Mirae Tower (mission building)

            Assert.Greater(Luma(road), Luma(block) + 0.01f, $"Road {road} not distinguishable from pavement {block}");
            Assert.Greater(Luma(building), Luma(block) + 0.01f, $"Building {building} not visible on the block {block}");
            Assert.Greater(missionBuilding.r, missionBuilding.g * 1.4f, $"Mission building not highlighted red: {missionBuilding}");
        }

        [UnityTest]
        public IEnumerator MapGeometry_IsInvisibleToTheGameCameraAndReflections()
        {
            yield return LoadWorld();
            int mapLayer = 1 << LayerMask.NameToLayer("Minimap");
            Assert.AreEqual(0, Camera.main.cullingMask & mapLayer, "Main camera renders the map geometry");
            foreach (ReflectionProbe probe in Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None))
                Assert.AreEqual(0, probe.cullingMask & mapLayer, $"{probe.name} reflects the map geometry");
        }

        // ---- minimap ---------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator Minimap_ShowsLiveMarkersThatFollowMovingNpcs()
        {
            yield return LoadWorld();
            yield return new WaitForSeconds(1f);

            float radius = minimap.MetersRadius * 0.85f;
            MapIcon[] inRange = MapIcon.All.Where(i => i.ShowOnMinimap && i.Type != MapIconType.Player
                && Vector2.Distance(new Vector2(i.Position.x, i.Position.z), new Vector2(player.position.x, player.position.z)) < radius).ToArray();
            Assert.IsNotEmpty(inRange, "Nothing near the spawn to show");
            foreach (MapIcon icon in inRange)
                Assert.IsTrue(minimap.Markers.TryGetValue(icon, out Image m) && m.gameObject.activeSelf, $"No minimap marker for {icon.Label}");

            // A civilian walking near the player: its marker moves with it.
            NpcController walker = NpcManager.Instance.Civilians.OrderBy(c => Vector3.Distance(c.transform.position, player.position)).First();
            walker.transform.position = player.position + new Vector3(6f, 0f, 6f);
            walker.GetComponent<UnityEngine.AI.NavMeshAgent>().Warp(walker.transform.position);
            yield return null;
            MapIcon walkerIcon = walker.GetComponent<MapIcon>();
            Vector2 before = minimap.Markers[walkerIcon].rectTransform.anchoredPosition;
            walker.GetComponent<UnityEngine.AI.NavMeshAgent>().Warp(player.position + new Vector3(-8f, 0f, 2f));
            yield return null;
            yield return null;
            Vector2 after = minimap.Markers[walkerIcon].rectTransform.anchoredPosition;
            Assert.Greater(Vector2.Distance(before, after), 20f, "Civilian marker did not follow the NPC");
        }

        [UnityTest]
        public IEnumerator Minimap_PinsDistantObjectivesToTheRim_AndRotatesWithTheCamera()
        {
            yield return LoadWorld();
            yield return null;
            MapIcon terminal = MapIcon.All.First(i => i.IconId == "secure_terminal");
            Image marker = minimap.Markers[terminal];
            Assert.IsTrue(marker.gameObject.activeSelf, "Distant objective not shown");
            float viewportRadius = ((RectTransform)minimap.transform).rect.width / 2f;
            Assert.AreEqual(viewportRadius * 0.88f, marker.rectTransform.anchoredPosition.magnitude, 2f, "Objective not pinned to the rim");

            // Direction on the rim must match the real direction relative to the camera.
            float yaw = minimap.CurrentYaw;
            Vector2 expected = minimap.OffsetFor(terminal.Position, player.position, yaw, 1f).normalized;
            Assert.Greater(Vector2.Dot(expected, marker.rectTransform.anchoredPosition.normalized), 0.99f);

            RawImage mapImage = minimap.GetComponentsInChildren<RawImage>().First();
            Assert.AreEqual(yaw, mapImage.rectTransform.localEulerAngles.z, 0.5f, "Map not rotated with the camera");
        }

        // ---- full map --------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator FullMap_OpensWithSelect_FreezesPlayer_ClosesWithBackWithoutPausing()
        {
            yield return LoadWorld();
            NpcManager.Instance.DespawnAllCivilians();
            yield return Tap(GamepadButton.Select);
            Assert.IsTrue(map.IsMapOpen, "Map did not open");
            Assert.IsTrue(fullMap.IsOpen, "Full map panel not shown");
            Assert.AreEqual(CursorLockMode.None, Cursor.lockState, "Cursor not released for the map");

            Vector3 start = player.position;
            yield return Hold(new GamepadState { leftStick = Vector2.up }, 0.8f);
            Assert.Less(Vector3.Distance(start, player.position), 0.05f, "Player moved while the map was open");

            yield return Tap(GamepadButton.Start); // Escape / Start = back
            Assert.IsFalse(map.IsMapOpen, "Back did not close the map");
            Assert.IsFalse(GameManager.Instance.IsPaused, "Closing the map paused the game");

            yield return Tap(GamepadButton.Select);
            Assert.IsTrue(map.IsMapOpen);
            yield return Tap(GamepadButton.Select);
            Assert.IsFalse(map.IsMapOpen, "Select did not toggle the map closed");
        }

        [UnityTest]
        public IEnumerator FullMap_ZoomPanAndRecenter()
        {
            yield return LoadWorld();
            map.OpenMap();
            yield return null;
            Vector2 playerUV = map.WorldToUV(player.position);
            float zoom = fullMap.Zoom;

            yield return Hold(new GamepadState().WithButton(GamepadButton.DpadUp, true), 0.6f);
            Assert.Greater(fullMap.Zoom, zoom * 1.2f, "D-pad up did not zoom in");

            Vector2 centre = fullMap.CentreUV;
            yield return Hold(new GamepadState { leftStick = Vector2.right }, 0.6f);
            Assert.Greater(fullMap.CentreUV.x, centre.x + 0.005f, "Stick did not pan the map");

            yield return Tap(GamepadButton.North); // Interact = recenter while the map is open
            Assert.Less(Vector2.Distance(fullMap.CentreUV, playerUV), 0.02f, "Recenter did not return to the player");
        }

        [UnityTest]
        public IEnumerator FullMap_UsesTheSameRegistry_AndFiltersWork()
        {
            yield return LoadWorld();
            yield return new WaitForSeconds(0.5f);
            map.OpenMap();
            fullMap.SetZoom(1f);
            fullMap.Pan(new Vector2(0.5f, 0.5f) - fullMap.CentreUV); // whole district in view
            yield return null;
            yield return null;

            int ActiveOf(MapIconType type) => fullMap.Markers.Count(p => p.Key != null && p.Key.Type == type && p.Value.gameObject.activeSelf);
            foreach (MapIconType type in new[] { MapIconType.Objective, MapIconType.MissionSubject, MapIconType.Guard, MapIconType.NPC, MapIconType.Safehouse, MapIconType.ExtractionPoint })
            {
                int registered = MapIcon.All.Count(i => i.Type == type && i.ShowOnFullMap);
                Assert.Greater(registered, 0, $"No {type} icons registered");
                Assert.AreEqual(registered, ActiveOf(type), $"{type}: full map shows {ActiveOf(type)} of {registered}");
            }

            fullMap.SetFilter(MapFilter.Civilians, false);
            yield return null;
            Assert.AreEqual(0, ActiveOf(MapIconType.NPC), "Civilian filter ignored");

            GameManager.Instance.Session.activeMissionId = "mission_downtown_02";
            fullMap.ActiveMissionOnly = true;
            yield return null;
            string[] objectives = fullMap.Markers.Where(p => p.Key != null && p.Key.Type == MapIconType.Objective && p.Value.gameObject.activeSelf)
                .Select(p => p.Key.IconId).ToArray();
            CollectionAssert.AreEquivalent(new[] { "data_drive" }, objectives, "Active-mission filter shows other missions' objectives");
        }

        [UnityTest]
        public IEnumerator CompletedObjective_StaysOnTheMapWithACompletedIndicator()
        {
            yield return LoadWorld();
            GameObject documents = GameObject.Find("Gangnam_MissionContent/Pickup_documents_evidence");
            MapIcon icon = documents.GetComponent<MapIcon>();

            GameEvents.RaiseObjectiveSignal(ObjectiveType.CollectItem, "documents_evidence");
            documents.SetActive(false); // what PickupInteractable does on collect
            yield return null;

            Assert.IsTrue(icon.Completed);
            Assert.Contains(icon, MapIcon.All.ToList(), "Completed objective vanished from the map");
            map.OpenMap();
            fullMap.SetZoom(1f);
            fullMap.Pan(new Vector2(0.5f, 0.5f) - fullMap.CentreUV);
            yield return null;
            yield return null;
            Image marker = fullMap.Markers[icon];
            Assert.IsTrue(marker.gameObject.activeSelf);
            Assert.AreEqual(MapIconSprites.Get(MapIconSprites.Shape.Check), marker.sprite, "Completed objective not shown with the check indicator");
            Assert.Greater(marker.color.g, marker.color.r, "Completed indicator is not green");
        }

        [UnityTest]
        public IEnumerator FullMap_ShowsStreetAndLandmarkLabelsByZoom()
        {
            yield return LoadWorld();
            map.OpenMap();
            fullMap.SetZoom(1f);
            yield return null;
            string[] visible = fullMap.Labels.Where(p => p.Value.gameObject.activeSelf).Select(p => p.Key.Korean).ToArray();
            CollectionAssert.Contains(visible, "강남대로");
            CollectionAssert.Contains(visible, "테헤란로");
            CollectionAssert.DoesNotContain(visible, "미래 타워", "Landmark labels should wait for closer zoom");

            fullMap.SetZoom(2f);
            yield return null;
            visible = fullMap.Labels.Where(p => p.Value.gameObject.activeSelf).Select(p => p.Key.Korean).ToArray();
            CollectionAssert.Contains(visible, "미래 타워");
        }

        [UnityTest]
        public IEnumerator IdentifiedMissionCharacter_IsHighlightedAndNamedOnTheMap()
        {
            yield return LoadWorld();
            GameManager.Instance.Session.Reset();
            Contract.Missions.MissionManager.Instance.StartMission("mission_downtown_01");
            MissionSubject kim = GameObject.Find("Gangnam_Population/MissionCharacters/subject_ceo_kim").GetComponent<MissionSubject>();
            MapIcon icon = kim.GetComponent<MapIcon>();
            Assert.IsFalse(icon.Highlighted);
            StringAssert.Contains("Unidentified", icon.Label);

            kim.Interact(player.gameObject); // identify
            Assert.IsTrue(icon.Highlighted);
            StringAssert.Contains("Kim Min-jun", icon.Label);
        }
    }
}
