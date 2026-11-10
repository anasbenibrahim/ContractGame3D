using System.Collections.Generic;
using System.Linq;
using Contract.Core;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Contract.Map
{
    /// <summary>
    /// Full-screen map (north-up). Same marker registry as the minimap. Zoom (wheel, d-pad, buttons,
    /// slider), pan (mouse drag, WASD / left stick), recenter on the player (button or E), category
    /// filters, "active mission only", legend, street/landmark labels and hover tooltips.
    /// The world keeps running while it is open, so NPC markers keep moving.
    /// </summary>
    public sealed class FullMapView : MonoBehaviour
    {
        [Header("Structure")]
        [SerializeField] private GameObject panel;
        [SerializeField] private RectTransform mapArea;
        [SerializeField] private RawImage mapImage;
        [SerializeField] private RectTransform labelLayer;
        [SerializeField] private RectTransform markerLayer;
        [SerializeField] private Image playerMarker;

        [Header("Info")]
        [SerializeField] private TMP_Text regionTitle;
        [SerializeField] private TMP_Text coordinates;
        [SerializeField] private TMP_Text tooltip;
        [SerializeField] private TMP_Text zoomLabel;
        [SerializeField] private RectTransform legendContainer;

        [Header("Controls")]
        [SerializeField] private Button closeButton;
        [SerializeField] private Button recenterButton;
        [SerializeField] private Button zoomInButton;
        [SerializeField] private Button zoomOutButton;
        [SerializeField] private Slider zoomSlider;
        [SerializeField] private Toggle[] filterToggles;      // one per MapFilter flag, in declaration order
        [SerializeField] private Toggle activeMissionOnly;
        [SerializeField] private InputReader inputReader;

        [Header("Behaviour")]
        [SerializeField] private float minZoom = 1f;
        [SerializeField] private float maxZoom = 6f;
        [SerializeField] private float defaultZoom = 2f;
        [SerializeField] private float panSpeed = 0.35f; // map widths per second at zoom 1

        private static readonly MapFilter[] FilterOrder =
            { MapFilter.Objectives, MapFilter.MissionCharacters, MapFilter.Civilians, MapFilter.Security, MapFilter.Collectibles, MapFilter.Places };

        private readonly Dictionary<MapIcon, Image> markers = new Dictionary<MapIcon, Image>();
        private readonly Dictionary<MapLabel, TextMeshProUGUI> labels = new Dictionary<MapLabel, TextMeshProUGUI>();
        private readonly HashSet<MapIcon> seen = new HashSet<MapIcon>();

        private float zoom;
        private Vector2 centreUV;
        private MapFilter filter = MapFilter.All;
        private bool dragging;

        public bool IsOpen => panel != null && panel.activeSelf;
        public float Zoom => zoom;
        public Vector2 CentreUV => centreUV;
        public MapFilter Filter => filter;
        public IReadOnlyDictionary<MapIcon, Image> Markers => markers;
        public IReadOnlyDictionary<MapLabel, TextMeshProUGUI> Labels => labels;

        private void Awake()
        {
            panel.SetActive(false);
            closeButton?.onClick.AddListener(() => MapManager.Instance?.CloseMap());
            recenterButton?.onClick.AddListener(Recenter);
            zoomInButton?.onClick.AddListener(() => SetZoom(zoom * 1.4f));
            zoomOutButton?.onClick.AddListener(() => SetZoom(zoom / 1.4f));
            if (zoomSlider != null)
            {
                zoomSlider.minValue = minZoom;
                zoomSlider.maxValue = maxZoom;
                zoomSlider.onValueChanged.AddListener(SetZoom);
            }

            for (int i = 0; i < filterToggles.Length && i < FilterOrder.Length; i++)
            {
                MapFilter flag = FilterOrder[i];
                filterToggles[i].onValueChanged.AddListener(on => SetFilter(flag, on));
            }

            if (playerMarker != null) playerMarker.sprite = MapIconSprites.Get(MapIconSprites.Shape.Arrow);
            BuildLegend();
        }

        private void OnEnable()
        {
            GameEvents.MapOpened += OnMapOpened;
            GameEvents.MapClosed += OnMapClosed;
            if (inputReader != null) inputReader.InteractPressed += OnInteract;
        }

        private void OnDisable()
        {
            GameEvents.MapOpened -= OnMapOpened;
            GameEvents.MapClosed -= OnMapClosed;
            if (inputReader != null) inputReader.InteractPressed -= OnInteract;
        }

        private void OnMapOpened(string id)
        {
            panel.SetActive(true);
            MapManager map = MapManager.Instance;
            if (regionTitle != null && map != null) regionTitle.text = $"{map.RegionKorean}  <size=60%>{map.RegionEnglish}</size>";
            SetZoom(defaultZoom);
            Recenter();
        }

        private void OnMapClosed(string id)
        {
            panel.SetActive(false);
            dragging = false;
        }

        private void OnInteract()
        {
            if (IsOpen) Recenter();
        }

        // ---- public controls (buttons, tests) ------------------------------------------------

        public void SetZoom(float value)
        {
            zoom = Mathf.Clamp(value, minZoom, maxZoom);
            if (zoomSlider != null) zoomSlider.SetValueWithoutNotify(zoom);
            if (zoomLabel != null) zoomLabel.text = $"x{zoom:F1}";
            ClampCentre();
        }

        public void Recenter()
        {
            MapManager map = MapManager.Instance;
            if (map != null && map.Player != null) centreUV = map.WorldToUV(map.Player.position);
            ClampCentre();
        }

        public void Pan(Vector2 uvDelta)
        {
            centreUV += uvDelta;
            ClampCentre();
        }

        public void SetFilter(MapFilter flag, bool on) => filter = on ? filter | flag : filter & ~flag;

        public bool ActiveMissionOnly
        {
            get => activeMissionOnly != null && activeMissionOnly.isOn;
            set { if (activeMissionOnly != null) activeMissionOnly.isOn = value; }
        }

        // ---- per frame ----------------------------------------------------------------------

        private void Update()
        {
            if (!IsOpen) return;
            MapManager map = MapManager.Instance;
            if (map == null) return;

            float requested = map.ConsumeRequestedZoom();
            if (requested > 0f) SetZoom(requested);
            HandleInput();
            Layout(map);
        }

        private void HandleInput()
        {
            float dt = Time.unscaledDeltaTime;

            // Zoom: mouse wheel (towards the cursor) and d-pad, both through the game's Zoom action.
            float zoomAxis = inputReader != null ? inputReader.Zoom : 0f;
            Mouse mouse = Mouse.current;
            if (Mathf.Abs(zoomAxis) > 0.01f)
            {
                bool wheel = mouse != null && Mathf.Abs(mouse.scroll.ReadValue().y) > 0.01f;
                float factor = wheel ? Mathf.Pow(1.15f, Mathf.Sign(zoomAxis)) : Mathf.Pow(2f, zoomAxis * dt);
                Vector2 before = wheel ? CursorUV() : centreUV;
                SetZoom(zoom * factor);
                if (wheel) centreUV += before - CursorUV(); // keep the point under the cursor fixed
                ClampCentre();
            }

            // Pan: WASD / left stick through the Move action (player movement is frozen while the map is open).
            Vector2 move = inputReader != null ? inputReader.Move : Vector2.zero;
            if (move.sqrMagnitude > 0.01f) Pan(move * panSpeed / zoom * dt);

            HandleTouch();

            // Pan: left-drag on the map.
            if (mouse != null)
            {
                if (mouse.leftButton.wasPressedThisFrame && RectTransformUtility.RectangleContainsScreenPoint(mapArea, mouse.position.ReadValue())) dragging = true;
                if (!mouse.leftButton.isPressed) dragging = false;
                if (dragging)
                {
                    Vector2 delta = mouse.delta.ReadValue() / ScaleFactor();
                    Pan(-delta / MapSizePixels());
                }
            }
        }

        private float pinchDistance = -1f;
        private bool touchPanning;

        /// <summary>Touchscreen: one finger drags the map, two fingers pinch-zoom around their midpoint.</summary>
        private void HandleTouch()
        {
            Touchscreen screen = Touchscreen.current;
            if (screen == null) return;
            var fingers = new System.Collections.Generic.List<UnityEngine.InputSystem.Controls.TouchControl>(2);
            foreach (var t in screen.touches)
                if (t.press.isPressed && fingers.Count < 2) fingers.Add(t);

            if (fingers.Count == 0)
            {
                touchPanning = false;
                pinchDistance = -1f;
                return;
            }

            if (fingers.Count == 1)
            {
                pinchDistance = -1f;
                Vector2 position = fingers[0].position.ReadValue();
                if (fingers[0].press.wasPressedThisFrame) touchPanning = RectTransformUtility.RectangleContainsScreenPoint(mapArea, position);
                if (touchPanning) Pan(-fingers[0].delta.ReadValue() / ScaleFactor() / MapSizePixels());
                return;
            }

            touchPanning = false;
            Vector2 a = fingers[0].position.ReadValue(), b = fingers[1].position.ReadValue();
            float distance = Vector2.Distance(a, b);
            if (pinchDistance > 0f && distance > 1f)
            {
                Vector2 before = ScreenUV((a + b) / 2f);
                SetZoom(zoom * distance / pinchDistance);
                centreUV += before - ScreenUV((a + b) / 2f); // keep the pinch centre fixed
                ClampCentre();
            }

            pinchDistance = distance;
        }

        private Vector2 ScreenUV(Vector2 screenPoint)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(mapArea, screenPoint, null, out Vector2 local);
            return centreUV + (local - mapArea.rect.center) / MapSizePixels();
        }

        private void Layout(MapManager map)
        {
            float size = MapSizePixels();
            mapImage.texture = map.MapTexture;
            mapImage.rectTransform.sizeDelta = new Vector2(size, size);
            mapImage.rectTransform.pivot = centreUV;
            mapImage.rectTransform.anchoredPosition = Vector2.zero;

            if (map.Player != null && playerMarker != null)
            {
                playerMarker.rectTransform.anchoredPosition = ToLocal(map.WorldToUV(map.Player.position));
                playerMarker.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -map.Player.eulerAngles.y);
                if (coordinates != null)
                {
                    Vector3 p = map.Player.position;
                    coordinates.text = $"X {p.x:F0}   Z {p.z:F0}";
                }
            }

            UpdateMarkers(map);
            UpdateLabels(map);
            UpdateTooltip();
        }

        public Vector2 ToLocal(Vector2 uv) => (uv - centreUV) * MapSizePixels();

        private float MapSizePixels() => mapArea.rect.height * zoom;

        private float ScaleFactor()
        {
            Canvas canvas = mapArea.GetComponentInParent<Canvas>();
            return canvas != null ? canvas.scaleFactor : 1f;
        }

        private Vector2 CursorUV()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return centreUV;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(mapArea, mouse.position.ReadValue(), null, out Vector2 local);
            Vector2 fromCentre = local - mapArea.rect.center;
            return centreUV + fromCentre / MapSizePixels();
        }

        private void ClampCentre()
        {
            float half = zoom > 0f ? 0.5f / zoom : 0.5f;
            centreUV = new Vector2(Mathf.Clamp(centreUV.x, half * 0.5f, 1f - half * 0.5f), Mathf.Clamp(centreUV.y, half * 0.5f, 1f - half * 0.5f));
        }

        private bool Passes(MapIcon icon)
        {
            if ((filter & icon.Category) == 0) return false;
            if (!ActiveMissionOnly) return true;
            string active = GameManager.Instance?.Session?.activeMissionId;
            // People and places stay visible; mission-specific icons only for the active mission.
            return string.IsNullOrEmpty(icon.MissionId) ? icon.Category != MapFilter.Objectives : icon.MissionId == active;
        }

        private void UpdateMarkers(MapManager map)
        {
            seen.Clear();
            Rect area = mapArea.rect;
            foreach (MapIcon icon in MapIcon.All)
            {
                if (icon == null || !icon.ShowOnFullMap || icon.Type == MapIconType.Player || !Passes(icon)) continue;
                Vector2 local = ToLocal(map.WorldToUV(icon.Position));
                if (Mathf.Abs(local.x) > area.width / 2f || Mathf.Abs(local.y) > area.height / 2f) continue;

                Image marker = GetMarker(icon);
                marker.rectTransform.anchoredPosition = local;
                marker.sprite = MapIconSprites.Get(MapIconSprites.ShapeFor(icon.Type, icon.Completed));
                marker.color = MapIconSprites.ColorFor(icon.Type, icon.Completed, icon.Highlighted);
                float s = MapIconSprites.SizeFor(icon.Type) * Mathf.Lerp(1f, 1.3f, (zoom - minZoom) / (maxZoom - minZoom));
                marker.rectTransform.sizeDelta = new Vector2(s, s);
                marker.gameObject.SetActive(true);
                seen.Add(icon);
            }

            foreach (var pair in markers)
            {
                if (!seen.Contains(pair.Key) && pair.Value != null && pair.Value.gameObject.activeSelf) pair.Value.gameObject.SetActive(false);
            }
        }

        private Image GetMarker(MapIcon icon)
        {
            if (markers.TryGetValue(icon, out Image image) && image != null) return image;
            GameObject go = new GameObject("Marker_" + icon.IconId, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(markerLayer, false);
            image = go.GetComponent<Image>();
            image.raycastTarget = false;
            markers[icon] = image;
            return image;
        }

        private void UpdateLabels(MapManager map)
        {
            int detail = zoom >= 3f ? 2 : zoom >= 1.6f ? 1 : 0;
            foreach (MapLabel label in MapLabel.All)
            {
                if (!labels.TryGetValue(label, out TextMeshProUGUI text) || text == null)
                {
                    GameObject go = new GameObject("Label_" + label.English, typeof(RectTransform));
                    go.transform.SetParent(labelLayer, false);
                    text = go.AddComponent<TextMeshProUGUI>();
                    text.alignment = TextAlignmentOptions.Center;
                    text.textWrappingMode = TextWrappingModes.NoWrap;
                    text.raycastTarget = false;
                    text.fontStyle = FontStyles.Bold;
                    text.text = $"{label.Korean}\n<size=55%><color=#9AA3AE>{label.English}</color></size>";
                    labels[label] = text;
                }

                bool show = label.DetailLevel <= detail;
                text.gameObject.SetActive(show);
                if (!show) continue;
                text.rectTransform.anchoredPosition = ToLocal(map.WorldToUV(label.transform.position));
                text.rectTransform.localRotation = Quaternion.Euler(0f, 0f, label.Angle);
                text.fontSize = 15f * label.Size * Mathf.Lerp(0.9f, 1.5f, (zoom - minZoom) / (maxZoom - minZoom));
                text.color = label.DetailLevel == 0 && label.Size > 1.5f ? new Color(1f, 1f, 1f, 0.35f) : new Color(0.92f, 0.94f, 0.96f, 0.9f);
            }
        }

        private void UpdateTooltip()
        {
            if (tooltip == null) return;
            Mouse mouse = Mouse.current;
            if (mouse == null || !RectTransformUtility.ScreenPointToLocalPointInRectangle(markerLayer, mouse.position.ReadValue(), null, out Vector2 cursor))
            {
                tooltip.gameObject.SetActive(false);
                return;
            }

            var nearest = markers.Where(p => p.Key != null && p.Value != null && p.Value.gameObject.activeSelf)
                .Select(p => (icon: p.Key, d: Vector2.Distance(p.Value.rectTransform.anchoredPosition, cursor)))
                .Where(p => p.d < 16f).OrderBy(p => p.d).FirstOrDefault();
            if (nearest.icon == null)
            {
                tooltip.gameObject.SetActive(false);
                return;
            }

            tooltip.gameObject.SetActive(true);
            tooltip.text = $"{nearest.icon.Label}{(nearest.icon.Completed ? "  <color=#4CD38A>✓ 완료 · Done</color>" : "")}";
            tooltip.rectTransform.anchoredPosition = markers[nearest.icon].rectTransform.anchoredPosition + new Vector2(0f, 22f);
        }

        private void BuildLegend()
        {
            if (legendContainer == null) return;
            foreach (MapIconType type in new[] { MapIconType.Player, MapIconType.Objective, MapIconType.MissionSubject, MapIconType.ExtractionPoint,
                                                 MapIconType.Safehouse, MapIconType.Collectible, MapIconType.Guard, MapIconType.NPC, MapIconType.PointOfInterest })
            {
                AddLegendRow(MapIconSprites.Get(MapIconSprites.ShapeFor(type, false)), MapIconSprites.ColorFor(type, false, false), MapIconSprites.LegendName(type));
            }

            AddLegendRow(MapIconSprites.Get(MapIconSprites.Shape.Check), MapIconSprites.ColorFor(MapIconType.Objective, true, false), "완료 · Completed");
        }

        private void AddLegendRow(Sprite sprite, Color color, string text)
        {
            GameObject row = new GameObject("LegendRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(legendContainer, false);
            HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = layout.childControlHeight = false;
            row.GetComponent<RectTransform>().sizeDelta = new Vector2(260f, 24f);

            Image icon = new GameObject("Icon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            icon.transform.SetParent(row.transform, false);
            icon.sprite = sprite;
            icon.color = color;
            icon.rectTransform.sizeDelta = new Vector2(18f, 18f);

            TextMeshProUGUI label = new GameObject("Text", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            label.transform.SetParent(row.transform, false);
            label.text = text;
            label.fontSize = 15f;
            label.color = new Color(0.85f, 0.87f, 0.9f);
            label.rectTransform.sizeDelta = new Vector2(230f, 22f);
        }
    }
}
