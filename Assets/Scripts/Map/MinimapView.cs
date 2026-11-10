using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Contract.Map
{
    /// <summary>
    /// Circular HUD minimap: the pre-rendered map texture under a circular mask, rotated with the
    /// camera (heading-up), with live markers from <see cref="MapIcon.All"/>. Objectives outside the
    /// radius stay pinned to the rim so the player always knows which way to go.
    /// </summary>
    public sealed class MinimapView : MonoBehaviour
    {
        [SerializeField] private RectTransform viewport;
        [SerializeField] private RawImage mapImage;
        [SerializeField] private RectTransform markerLayer;
        [SerializeField] private Image playerArrow;
        [SerializeField] private RectTransform northMarker;
        [SerializeField] private TMP_Text regionLabel;
        [SerializeField] private Image ring;
        [SerializeField] private Image backing;

        [SerializeField] private float metersRadius = 55f;
        [SerializeField] private bool rotateWithCamera = true;
        [SerializeField, Range(0.7f, 1f)] private float rimFraction = 0.88f;

        private readonly Dictionary<MapIcon, Image> markers = new Dictionary<MapIcon, Image>();
        private readonly HashSet<MapIcon> seen = new HashSet<MapIcon>();
        private CanvasGroup group;

        public float MetersRadius => metersRadius;
        public IReadOnlyDictionary<MapIcon, Image> Markers => markers;

        private void Awake()
        {
            group = GetComponent<CanvasGroup>();
            if (group == null) group = gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            Image mask = viewport.GetComponent<Image>();
            if (mask != null) mask.sprite = MapIconSprites.Get(MapIconSprites.Shape.Circle);
            if (ring != null) ring.sprite = MapIconSprites.Get(MapIconSprites.Shape.Outline);
            if (backing != null) backing.sprite = MapIconSprites.Get(MapIconSprites.Shape.Circle);
            if (playerArrow != null)
            {
                playerArrow.sprite = MapIconSprites.Get(MapIconSprites.Shape.Arrow);
                playerArrow.color = Color.white;
            }
        }

        private void Start()
        {
            MapManager map = MapManager.Instance;
            if (map != null && regionLabel != null) regionLabel.text = $"{map.RegionKorean} · {map.RegionEnglish}";
        }

        /// <summary>Screen-space offset (pixels from the minimap centre) for a world position.</summary>
        public Vector2 OffsetFor(Vector3 world, Vector3 centre, float yawDegrees, float pixelsPerMetre)
        {
            Vector2 delta = new Vector2(world.x - centre.x, world.z - centre.z);
            float a = yawDegrees * Mathf.Deg2Rad;
            // Rotate counter-clockwise by the camera yaw so the view direction points up.
            return new Vector2(delta.x * Mathf.Cos(a) - delta.y * Mathf.Sin(a), delta.x * Mathf.Sin(a) + delta.y * Mathf.Cos(a)) * pixelsPerMetre;
        }

        public float CurrentYaw
        {
            get
            {
                Camera cam = Camera.main;
                return rotateWithCamera && cam != null ? cam.transform.eulerAngles.y : 0f;
            }
        }

        private void LateUpdate()
        {
            // Hidden under full-screen overlays (full map, contract board, result screens).
            bool overlay = Contract.Core.GameManager.Instance != null &&
                           (Contract.Core.GameManager.Instance.IsMapOpen || Contract.Core.GameManager.Instance.IsMenuOpen);
            group.alpha = overlay ? 0f : 1f;
            if (overlay) return;

            MapManager map = MapManager.Instance;
            if (map == null || map.Player == null || map.MapTexture == null) return;

            float radius = viewport.rect.width / 2f;
            float pixelsPerMetre = radius / metersRadius;
            float yaw = CurrentYaw;
            Vector3 centre = map.Player.position;

            // Map texture: pivot on the player, rotate around it.
            mapImage.texture = map.MapTexture;
            mapImage.rectTransform.sizeDelta = map.WorldBounds.size * pixelsPerMetre;
            mapImage.rectTransform.pivot = map.WorldToUV(centre);
            mapImage.rectTransform.anchoredPosition = Vector2.zero;
            mapImage.rectTransform.localRotation = Quaternion.Euler(0f, 0f, yaw);

            if (playerArrow != null) playerArrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, yaw - map.Player.eulerAngles.y);
            if (northMarker != null) northMarker.anchoredPosition = OffsetFor(centre + Vector3.forward * 1000f, centre, yaw, 1f).normalized * radius * 0.95f;

            UpdateMarkers(centre, yaw, pixelsPerMetre, radius);
        }

        private void UpdateMarkers(Vector3 centre, float yaw, float pixelsPerMetre, float radius)
        {
            seen.Clear();
            foreach (MapIcon icon in MapIcon.All)
            {
                if (icon == null || !icon.ShowOnMinimap || icon.Type == MapIconType.Player) continue;

                Vector2 offset = OffsetFor(icon.Position, centre, yaw, pixelsPerMetre);
                bool outside = offset.magnitude > radius * rimFraction;
                if (outside && !(icon.ClampToMinimapEdge && !icon.Completed)) continue;
                if (outside) offset = offset.normalized * radius * rimFraction;

                Image marker = GetMarker(icon);
                marker.rectTransform.anchoredPosition = offset;
                marker.sprite = MapIconSprites.Get(MapIconSprites.ShapeFor(icon.Type, icon.Completed));
                marker.color = MapIconSprites.ColorFor(icon.Type, icon.Completed, icon.Highlighted);
                float size = MapIconSprites.SizeFor(icon.Type) * (outside ? 0.8f : 1f);
                marker.rectTransform.sizeDelta = new Vector2(size, size);
                marker.gameObject.SetActive(true);
                seen.Add(icon);
            }

            foreach (var pair in markers)
            {
                if (!seen.Contains(pair.Key) && pair.Value.gameObject.activeSelf) pair.Value.gameObject.SetActive(false);
            }

            if (markers.Count > seen.Count * 2 + 32) PruneDestroyed();
        }

        private Image GetMarker(MapIcon icon)
        {
            if (markers.TryGetValue(icon, out Image image) && image != null) return image;
            GameObject go = new GameObject("Marker", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(markerLayer, false);
            image = go.GetComponent<Image>();
            image.raycastTarget = false;
            markers[icon] = image;
            return image;
        }

        private void PruneDestroyed()
        {
            var dead = new List<MapIcon>();
            foreach (var pair in markers) if (pair.Key == null) dead.Add(pair.Key);
            foreach (MapIcon icon in dead)
            {
                Destroy(markers[icon].gameObject);
                markers.Remove(icon);
            }
        }
    }
}
